using System.Text.Json;

namespace Trendo.Core;

public enum HealState { Ok, Repaired, NeedsYou, Failed }
public sealed record HealResult(string Id, string Title, HealState State, string Detail);

/// <summary>X-02 Zero Touch Setup & Self Healing: prüft beim Start und auf Knopfdruck typische Schäden und repariert sichere Fälle selbst
/// (fehlende Ordner, kaputte Einstellungsdateien → in Quarantäne + Standard, verwaiste Programmpfade, alte Zwischendateien).
/// Was Trendo nicht selbst lösen darf (z. B. wenig Speicherplatz), wird klar benannt.</summary>
public sealed class SelfHealer(string dataRoot, AuditLog audit)
{
    public static readonly string[] Folders = ["projects", "audit", "backups", "ki", "wissen", "prompts", "ablaeufe", "logs", "tmp"];
    public long MinFreeBytes { get; init; } = 2L * 1024 * 1024 * 1024;
    public Func<string, long>? FreeSpace { get; init; }

    public IReadOnlyList<HealResult> Run(bool repair, DateTime now)
    {
        var r = new List<HealResult>();
        // 1 Ordner
        var missing = Folders.Where(f => !Directory.Exists(Path.Combine(dataRoot, f))).ToList();
        if (missing.Count == 0) r.Add(new("folders", "Ordner von Trendo", HealState.Ok, "Alle da."));
        else if (repair) { foreach (var m in missing) Directory.CreateDirectory(Path.Combine(dataRoot, m)); r.Add(new("folders", "Ordner von Trendo", HealState.Repaired, $"Neu angelegt: {string.Join(", ", missing)}")); }
        else r.Add(new("folders", "Ordner von Trendo", HealState.NeedsYou, $"Fehlen: {string.Join(", ", missing)}"));
        // 2 JSON-Dateien
        var bad = new List<string>();
        foreach (var f in Directory.Exists(dataRoot) ? Directory.EnumerateFiles(dataRoot, "*.json", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Where(f => !f.Contains(Path.DirectorySeparatorChar + "quarantaene" + Path.DirectorySeparatorChar)).ToList() : [])
        {
            try { using var d = JsonDocument.Parse(File.ReadAllText(f)); }
            catch (JsonException) { bad.Add(f); }
            catch (IOException) { }
        }
        if (bad.Count == 0) r.Add(new("json", "Einstellungsdateien", HealState.Ok, "Alle lesbar."));
        else if (repair)
        {
            var q = Path.Combine(dataRoot, "quarantaene", now.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(q);
            foreach (var b in bad) File.Move(b, Path.Combine(q, Path.GetRelativePath(dataRoot, b).Replace(Path.DirectorySeparatorChar, '_')), true);
            r.Add(new("json", "Einstellungsdateien", HealState.Repaired, $"{bad.Count} kaputte Datei(en) beiseitegelegt (Ordner quarantaene), Standardwerte gelten wieder: {string.Join(", ", bad.Select(Path.GetFileName))}"));
        }
        else r.Add(new("json", "Einstellungsdateien", HealState.NeedsYou, $"{bad.Count} Datei(en) beschädigt."));
        // 3 verwaiste Programmpfade (tools.json: id -> Pfad)
        var tools = Path.Combine(dataRoot, "tools.json");
        if (File.Exists(tools))
        {
            try
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(tools)) ?? [];
                var gone = map.Where(kv => !File.Exists(kv.Value) && !Directory.Exists(kv.Value)).Select(kv => kv.Key).ToList();
                if (gone.Count == 0) r.Add(new("tools", "Verknüpfte Programme", HealState.Ok, $"{map.Count} verknüpft, alle gefunden."));
                else if (repair) { foreach (var g in gone) map.Remove(g); File.WriteAllText(tools, JsonSerializer.Serialize(map)); r.Add(new("tools", "Verknüpfte Programme", HealState.Repaired, $"Nicht mehr vorhanden, Verknüpfung gelöst: {string.Join(", ", gone)}. Beim nächsten Öffnen sucht Trendo neu.")); }
                else r.Add(new("tools", "Verknüpfte Programme", HealState.NeedsYou, $"Nicht mehr gefunden: {string.Join(", ", gone)}"));
            }
            catch (JsonException) { }
        }
        // 4 alte Zwischendateien
        var tmp = Path.Combine(dataRoot, "tmp");
        var old = Directory.Exists(tmp) ? Directory.GetFiles(tmp, "*", SearchOption.AllDirectories).Where(f => (now - File.GetLastWriteTime(f)).TotalDays > 7).ToList() : [];
        if (old.Count == 0) r.Add(new("tmp", "Zwischendateien", HealState.Ok, "Aufgeräumt."));
        else if (repair) { foreach (var o in old) try { File.Delete(o); } catch (IOException) { } r.Add(new("tmp", "Zwischendateien", HealState.Repaired, $"{old.Count} alte Datei(en) gelöscht.")); }
        else r.Add(new("tmp", "Zwischendateien", HealState.NeedsYou, $"{old.Count} alte Datei(en)."));
        // 5 Speicherplatz (nur melden)
        long free;
        try { free = FreeSpace?.Invoke(dataRoot) ?? new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dataRoot))!).AvailableFreeSpace; } catch { free = -1; }
        if (free < 0) r.Add(new("disk", "Speicherplatz", HealState.Failed, "Konnte nicht gemessen werden."));
        else if (free < MinFreeBytes) r.Add(new("disk", "Speicherplatz", HealState.NeedsYou, $"Nur noch {free / 1024 / 1024} MB frei. Unter „Speicher & Leistung“ siehst du, was viel Platz braucht."));
        else r.Add(new("disk", "Speicherplatz", HealState.Ok, $"{free / 1024 / 1024 / 1024} GB frei."));
        // 6 Protokoll
        var a = new AuditLog(Path.Combine(dataRoot, "audit", "audit.jsonl"));
        r.Add(a.Verify() ? new("audit", "Protokoll", HealState.Ok, "Vollständig und unverändert.") : new("audit", "Protokoll", HealState.NeedsYou, "Verändert. Das repariert Trendo bewusst nicht – sonst wären Spuren weg."));
        if (repair || r.Any(x => x.State != HealState.Ok)) audit.Append("selbstheilung", repair ? "heal.repair" : "heal.check", string.Join(", ", r.Select(x => $"{x.Id}={x.State}")));
        var log = Path.Combine(dataRoot, "logs"); Directory.CreateDirectory(log);
        File.WriteAllText(Path.Combine(log, "selbstheilung-letzte.json"), JsonSerializer.Serialize(new { at = now, repair, results = r }));
        return r;
    }

    public static (DateTime At, List<HealResult> Results)? Last(string dataRoot)
    {
        var f = Path.Combine(dataRoot, "logs", "selbstheilung-letzte.json");
        try { var d = JsonDocument.Parse(File.ReadAllText(f)).RootElement; return (d.GetProperty("at").GetDateTime(), d.GetProperty("results").Deserialize<List<HealResult>>()!); } catch { return null; }
    }
}
