using System.Text.Json;

namespace Trendo.Core;

/// <summary>Wunsch 02.11.2026: alle Briefe, Akten, Briefköpfe und Eingaben lassen sich löschen – sicher. Gelöschtes kommt zuerst in den
/// Trendo-Papierkorb (Briefe/.papierkorb) und lässt sich mit „Rückgängig“ zurückholen. „Papierkorb leeren“ gibt es an den Windows-Papierkorb weiter –
/// endgültig gelöscht wird nie.</summary>
public sealed class LetterTrash(string projectsFolder)
{
    public sealed record Entry(string Id, string Label, string Kind, DateTime At, List<Item> Items);
    public sealed record Item(string From, string Stored);

    public string Root => Path.Combine(projectsFolder, "Briefe", ".papierkorb");
    static readonly JsonSerializerOptions J = new() { WriteIndented = true };

    /// <summary>Dateien und Ordner in den Papierkorb legen. Fehlendes wird übersprungen; ohne vorhandene Teile passiert nichts (null).</summary>
    public Entry? Move(string label, string kind, params string[] paths)
    {
        var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).Distinct().ToList();
        if (existing.Count == 0) return null;
        var id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..6];
        var dir = Path.Combine(Root, id); Directory.CreateDirectory(dir);
        var items = new List<Item>();
        foreach (var p in existing)
        {
            var name = Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar)); var stored = Path.Combine(dir, name);
            for (var n = 2; File.Exists(stored) || Directory.Exists(stored); n++) stored = Path.Combine(dir, $"{n}-{name}");
            if (Directory.Exists(p)) Directory.Move(p, stored); else File.Move(p, stored);
            items.Add(new Item(Path.GetFullPath(p), Path.GetFileName(stored)));
        }
        var e = new Entry(id, label, kind, DateTime.UtcNow, items);
        File.WriteAllText(Path.Combine(dir, "eintrag.json"), JsonSerializer.Serialize(e, J));
        return e;
    }

    /// <summary>Ein Datenstück (z. B. Briefkopf aus einer Liste) als kleine Datei in den Papierkorb legen.</summary>
    public Entry Keep(string label, string kind, string json)
    {
        var tmp = Path.Combine(Root, "neu-" + Guid.NewGuid().ToString("N") + ".json"); Directory.CreateDirectory(Root); File.WriteAllText(tmp, json);
        var e = Move(label, kind, tmp)!;
        return e with { Items = [e.Items[0] with { From = "" }] };
    }

    public IReadOnlyList<Entry> List() => !Directory.Exists(Root) ? [] : Directory.GetDirectories(Root)
        .Select(d => Path.Combine(d, "eintrag.json")).Where(File.Exists)
        .Select(f => { try { return JsonSerializer.Deserialize<Entry>(File.ReadAllText(f)); } catch (JsonException) { return null; } }).OfType<Entry>()
        .OrderByDescending(e => e.At).ToList();

    /// <summary>Zurückholen an den alten Ort (ist dort inzwischen etwas Neues, bekommt das Alte einen Zusatz). Liefert die wiederhergestellten Pfade bzw. bei Datenstücken den Inhalt.</summary>
    public IReadOnlyList<string> Restore(string id)
    {
        var dir = Path.Combine(Root, id); var f = Path.Combine(dir, "eintrag.json");
        if (!File.Exists(f) || JsonSerializer.Deserialize<Entry>(File.ReadAllText(f)) is not { } e) throw new FileNotFoundException("Nicht mehr im Papierkorb.");
        var back = new List<string>();
        foreach (var it in e.Items)
        {
            var src = Path.Combine(dir, it.Stored);
            if (it.From.Length == 0 || it.From.Contains(Path.Combine(".papierkorb", "neu-"))) { back.Add(File.ReadAllText(src)); continue; } // Datenstück: Inhalt zurückgeben
            var target = it.From;
            if (File.Exists(target) || Directory.Exists(target))
                target = Path.Combine(Path.GetDirectoryName(target)!, Path.GetFileNameWithoutExtension(target) + " (wiederhergestellt)" + Path.GetExtension(target));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (Directory.Exists(src)) Directory.Move(src, target); else File.Move(src, target);
            back.Add(target);
        }
        Directory.Delete(dir, true); // nur der leere Eintrag samt Beschreibung
        return back;
    }

    /// <summary>Papierkorb leeren: alles an den Windows-Papierkorb weitergeben (Aufrufer liefert die Funktion). Endgültig gelöscht wird hier nichts.</summary>
    public int Empty(Action<string> toRecycleBin)
    {
        var n = 0;
        foreach (var e in List()) { toRecycleBin(Path.Combine(Root, e.Id)); n++; }
        return n;
    }
}
