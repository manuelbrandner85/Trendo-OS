using System.IO.Compression;
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
        var bad = new List<string>(); var kept = new List<string>();
        var restored = Restored(dataRoot); // T-0763: vom Nutzer zurückgeholte Dateien bleiben liegen, solange sie unverändert sind
        foreach (var f in Directory.Exists(dataRoot) ? Directory.EnumerateFiles(dataRoot, "*.json", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Where(f => !f.Contains(Path.DirectorySeparatorChar + QuarantineFolder + Path.DirectorySeparatorChar)).ToList() : [])
        {
            try { using var d = JsonDocument.Parse(File.ReadAllText(f)); }
            catch (JsonException)
            {
                if (restored.TryGetValue(Rel(f), out var h) && h == Sha(f)) kept.Add(f); else bad.Add(f);
            }
            catch (IOException) { }
        }
        var keptNote = kept.Count == 0 ? "" : $" Zurückgeholt und weiter nicht lesbar (bleibt wie gewünscht liegen): {string.Join(", ", kept.Select(Path.GetFileName))}.";
        if (bad.Count == 0) r.Add(new("json", "Einstellungsdateien", kept.Count == 0 ? HealState.Ok : HealState.NeedsYou, kept.Count == 0 ? "Alle lesbar." : keptNote.Trim()));
        else if (repair)
        {
            var stamp = now.ToString("yyyyMMdd-HHmmss");
            var q = Path.Combine(dataRoot, QuarantineFolder, stamp); Directory.CreateDirectory(q);
            var origin = ReadMap(OriginPath(dataRoot));
            foreach (var b in bad)
            {
                var name = Path.GetRelativePath(dataRoot, b).Replace(Path.DirectorySeparatorChar, '_');
                SafeFile.Move(b, Path.Combine(q, name));
                origin[stamp + "/" + name] = Rel(b); // T-0763: Herkunft festhalten, damit „Zurückholen“ den richtigen Ort kennt
            }
            Directory.CreateDirectory(Path.Combine(dataRoot, "logs"));
            AtomicFile.WriteAllText(OriginPath(dataRoot), JsonSerializer.Serialize(origin));
            r.Add(new("json", "Einstellungsdateien", HealState.Repaired, $"{bad.Count} kaputte Datei(en) beiseitegelegt (Ordner quarantaene), Standardwerte gelten wieder: {string.Join(", ", bad.Select(Path.GetFileName))}. Zurückholen kannst du sie unter „Einrichten & Reparieren“.{keptNote}"));
        }
        else r.Add(new("json", "Einstellungsdateien", HealState.NeedsYou, $"{bad.Count} Datei(en) beschädigt.{keptNote}"));
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

    // ---------- T-0763 (X-02): beiseitegelegte Dateien zurückholen ----------
    /// <summary>Der Quarantäne-Ordner enthält nur die beiseitegelegten Dateien; Herkunft und Zurückgeholtes stehen in zwei kleinen Listen im Ordner „logs“.</summary>
    public const string QuarantineFolder = "quarantaene", OriginFile = "quarantaene-herkunft.json", RestoredFile = "quarantaene-zurueckgeholt.json";
    static string OriginPath(string root) => Path.Combine(root, "logs", OriginFile);
    /// <summary>Eine beiseitegelegte Datei: Ordner (Zeitstempel), Datei im Quarantäne-Ordner, ursprünglicher Ort (relativ zum Datenordner, null = unbekannt).</summary>
    public sealed record QuarantineItem(string Stamp, string File, string? Original);

    string Rel(string f) => RelOf(dataRoot, f);
    static string RelOf(string root, string f) => Path.GetRelativePath(root, f).Replace('\\', '/');
    static string Sha(string f) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)));
    static Dictionary<string, string> ReadMap(string f)
    {
        try { return File.Exists(f) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f)) ?? [] : []; }
        catch (Exception e) when (e is JsonException or IOException) { return []; }
    }
    static Dictionary<string, string> Restored(string root) => ReadMap(Path.Combine(root, "logs", RestoredFile));

    /// <summary>Alle beiseitegelegten Dateien, neueste zuerst. Alte Quarantäne-Ordner (vor T-0763) kennen den Ursprung nur, wenn die Datei direkt im
    /// Datenordner lag (Name ohne „_“); sonst bleibt der Ort ehrlich unbekannt.</summary>
    public static IReadOnlyList<QuarantineItem> Quarantined(string dataRoot)
    {
        var q = Path.Combine(dataRoot, QuarantineFolder);
        if (!Directory.Exists(q)) return [];
        var res = new List<QuarantineItem>();
        var map = ReadMap(OriginPath(dataRoot));
        foreach (var dir in Directory.GetDirectories(q).OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal))
        {
            foreach (var f in Directory.GetFiles(dir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(f);
                res.Add(new(Path.GetFileName(dir), f, map.TryGetValue(Path.GetFileName(dir) + "/" + name, out var o) ? o : name.Contains('_') ? null : name));
            }
        }
        return res;
    }

    /// <summary>Datei an ihren alten Ort zurücklegen. Liegt dort inzwischen eine neue Datei (z. B. die Standardwerte), wird sie als „….vor-rueckholung-…bak“
    /// daneben aufbewahrt – nichts geht verloren. Die zurückgeholte Datei wird beim nächsten Prüfen nicht erneut beiseitegelegt, solange sie unverändert ist.</summary>
    public static string Restore(string dataRoot, QuarantineItem item, AuditLog audit, DateTime now)
    {
        if (item.Original is null) throw new InvalidOperationException($"Für „{Path.GetFileName(item.File)}“ ist der alte Ort nicht bekannt. Öffne den Ordner quarantaene und lege die Datei selbst zurück.");
        var root = Path.GetFullPath(dataRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(dataRoot, item.Original.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Der alte Ort liegt außerhalb des Datenordners – Trendo legt dort nichts ab.");
        if (!File.Exists(item.File)) throw new FileNotFoundException("Die Datei liegt nicht mehr im Ordner quarantaene.", item.File);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string? kept = null;
        if (File.Exists(target)) { kept = target + $".vor-rueckholung-{now:yyyyMMdd-HHmmss}.bak"; SafeFile.Move(target, kept); }
        SafeFile.Move(item.File, target);
        var dir = Path.GetDirectoryName(item.File)!;
        var map = ReadMap(OriginPath(dataRoot));
        if (map.Remove(item.Stamp + "/" + Path.GetFileName(item.File))) AtomicFile.WriteAllText(OriginPath(dataRoot), JsonSerializer.Serialize(map));
        if (Directory.GetFiles(dir).Length == 0) try { Directory.Delete(dir, false); } catch (IOException) { }
        var restored = Restored(dataRoot); restored[RelOf(dataRoot, target)] = Sha(target);
        Directory.CreateDirectory(Path.Combine(dataRoot, "logs"));
        AtomicFile.WriteAllText(Path.Combine(dataRoot, "logs", RestoredFile), JsonSerializer.Serialize(restored));
        audit.Append("selbstheilung", "heal.restore", $"{item.Original} aus {item.Stamp}" + (kept is null ? "" : $"; bisherige Datei als {Path.GetFileName(kept)}"));
        return $"„{Path.GetFileName(target)}“ liegt wieder an ihrem Platz ({item.Original})." + (kept is null ? "" : $" Die bisherige Datei bleibt als {Path.GetFileName(kept)} daneben.");
    }

    public static (DateTime At, List<HealResult> Results)? Last(string dataRoot)
    {
        var f = Path.Combine(dataRoot, "logs", "selbstheilung-letzte.json");
        try { var d = JsonDocument.Parse(File.ReadAllText(f)).RootElement; return (d.GetProperty("at").GetDateTime(), d.GetProperty("results").Deserialize<List<HealResult>>()!); } catch { return null; }
    }
}

/// <summary>T-0763 (X-02): Installation gegen das Installationspaket prüfen und reparieren. Velopack legt neben dem Programmordner „current“ das Paket
/// der laufenden Version ab (packages\…-&lt;Version&gt;-full.nupkg, ein ZIP mit allen Programmdateien unter lib/app/). Für jede Datei vergleicht Trendo
/// Größe und Prüfsumme (CRC-32 aus dem Paketverzeichnis) mit der Datei im Programmordner. Fehlende oder veränderte Dateien lassen sich aus diesem Paket
/// ersetzen. Ehrliche Grenze: CRC-32 erkennt Beschädigung und versehentliches Ändern sicher, ist aber kein Schutz gegen gezielte Manipulation
/// (dafür fehlt eine Codesignatur). Ohne Paket (z. B. aus dem Quelltext gestartet) gibt es nichts zu vergleichen.</summary>
public static class InstallIntegrity
{
    public const string AppPrefix = "lib/app/";
    /// <summary>Dateien im Paket, die Velopack beim Installieren bewusst nicht in den Programmordner legt (gemessen an der echten Installation 0.9.108:
    /// „Squirrel.exe“ nur für alte Squirrel-Kompatibilität, „&lt;Name&gt;_ExecutionStub.exe“ wird zum Startprogramm im Ordner darüber).</summary>
    public static bool NotInAppFolder(string rel) => rel.Equals("Squirrel.exe", StringComparison.OrdinalIgnoreCase) || (!rel.Contains('/') && rel.EndsWith("_ExecutionStub.exe", StringComparison.OrdinalIgnoreCase));
    public sealed record Finding(string File, string Problem);
    public sealed record Report(bool Installed, string? Package, string? Version, int Checked, IReadOnlyList<Finding> Findings, string Note)
    {
        public bool Ok => Installed && Package is not null && Findings.Count == 0;
    }

    /// <summary>Version aus „sq.version“ (legt Velopack beim Installieren in den Programmordner).</summary>
    public static string? InstalledVersion(string appDir)
    {
        var f = Path.Combine(appDir, "sq.version");
        if (!File.Exists(f)) return null;
        try { return System.Xml.Linq.XDocument.Load(f).Descendants().FirstOrDefault(e => e.Name.LocalName == "version")?.Value.Trim(); }
        catch (Exception e) when (e is System.Xml.XmlException or IOException) { return null; }
    }

    /// <summary>Paket der installierten Version im Ordner „packages“ neben dem Programmordner.</summary>
    public static string? FindPackage(string appDir, string? version)
    {
        if (version is null) return null;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDir)));
        var pkgs = root is null ? null : Path.Combine(root, "packages");
        return pkgs is null || !Directory.Exists(pkgs) ? null : Directory.GetFiles(pkgs, $"*-{version}-full.nupkg").FirstOrDefault();
    }

    public static Report Check(string appDir)
    {
        var ver = InstalledVersion(appDir);
        if (ver is null) return new(false, null, null, 0, [], "Trendo läuft hier ohne Installation (z. B. aus dem Quelltext). Die Programmdateien lassen sich nur in der installierten Version prüfen.");
        var pkg = FindPackage(appDir, ver);
        if (pkg is null) return new(true, null, ver, 0, [], $"Das Installationspaket der Version {ver} fehlt im Ordner „packages“. Ohne Paket kann Trendo die Programmdateien nicht vergleichen – führe das Setup erneut aus.");
        var findings = new List<Finding>(); var n = 0;
        try
        {
            using var z = ZipFile.OpenRead(pkg);
            foreach (var e in z.Entries.Where(e => e.FullName.StartsWith(AppPrefix, StringComparison.Ordinal) && !e.FullName.EndsWith('/')))
            {
                var rel = e.FullName[AppPrefix.Length..];
                if (NotInAppFolder(rel)) continue;
                n++;
                if (!Inside(appDir, rel, out var file)) continue;
                if (!File.Exists(file)) { findings.Add(new(rel, "fehlt")); continue; }
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.Length != e.Length) { findings.Add(new(rel, "verändert")); continue; }
                    using var s = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (Crc32(s) != e.Crc32) findings.Add(new(rel, "verändert"));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { findings.Add(new(rel, "nicht lesbar")); }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new(true, null, ver, 0, [], $"Das Installationspaket ist selbst beschädigt ({ex.GetType().Name}). Führe das Setup erneut aus.");
        }
        return new(true, pkg, ver, n, findings, findings.Count == 0 ? $"Alle {n} Programmdateien stimmen mit dem Installationspaket überein." : $"{findings.Count} von {n} Programmdateien weichen vom Installationspaket ab.");
    }

    /// <summary>Dateien aus dem Paket zurückschreiben (erst Zwischendatei, dann ersetzen). Dateien, die das laufende Trendo gerade benutzt, lassen sich unter
    /// Windows nicht ersetzen – sie kommen in „Failed“ mit dem Hinweis, das Setup erneut auszuführen.</summary>
    public static (List<string> Repaired, List<string> Failed) Repair(string appDir, string package, IEnumerable<string> files, AuditLog? audit = null)
    {
        var ok = new List<string>(); var bad = new List<string>();
        using (var z = ZipFile.OpenRead(package))
            foreach (var rel in files.Distinct())
            {
                var e = z.GetEntry(AppPrefix + rel.Replace('\\', '/'));
                if (e is null || !Inside(appDir, rel, out var file)) { bad.Add(rel); continue; }
                var tmp = file + ".trendo-reparatur";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    e.ExtractToFile(tmp, true);
                    File.Move(tmp, file, true);
                    ok.Add(rel);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    bad.Add(rel); try { File.Delete(tmp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
                }
            }
        audit?.Append("selbstheilung", "installation.reparatur", $"ersetzt={ok.Count} nicht={bad.Count}" + (bad.Count > 0 ? ": " + string.Join(", ", bad.Take(10)) : ""));
        return (ok, bad);
    }

    static bool Inside(string appDir, string rel, out string file)
    {
        var root = Path.GetFullPath(appDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        file = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
        return file.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    static readonly uint[] Table = Enumerable.Range(0, 256).Select(i => { var c = (uint)i; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; return c; }).ToArray();
    /// <summary>CRC-32 (wie im ZIP-Verzeichnis).</summary>
    public static uint Crc32(Stream s)
    {
        var crc = 0xFFFFFFFFu; var buf = new byte[1 << 16]; int r;
        while ((r = s.Read(buf, 0, buf.Length)) > 0)
            for (var i = 0; i < r; i++) crc = Table[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }
}
