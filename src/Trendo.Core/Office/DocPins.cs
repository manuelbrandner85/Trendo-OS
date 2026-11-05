using System.Text.Json;

namespace Trendo.Core.Office;

/// <summary>Angeheftete Dokumente der Office-Startseite wie „Angeheftet“ in Microsoft 365 (Clean-Room, 05.11.2026): Pfade in einer kleinen Datei im Datenordner,
/// Reihenfolge = zuletzt angeheftet zuerst, gelöschte Dateien fallen beim Lesen heraus.</summary>
public sealed class DocPins(string dataRoot)
{
    public const int Max = 24;
    string F => Path.Combine(dataRoot, "office", "angeheftet.json");

    List<string> Load()
    {
        try { return File.Exists(F) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(F)) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    void Save(List<string> l) { Directory.CreateDirectory(Path.GetDirectoryName(F)!); File.WriteAllText(F, JsonSerializer.Serialize(l)); }

    /// <summary>Vorhandene angeheftete Dateien.</summary>
    public List<string> All() => [.. Load().Where(File.Exists)];

    public bool IsPinned(string path) => Load().Any(p => string.Equals(p, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));

    /// <summary>Anheften oder lösen; gibt den neuen Zustand zurück. Höchstens <see cref="Max"/> – das älteste fällt dann heraus.</summary>
    public bool Toggle(string path)
    {
        var full = Path.GetFullPath(path); var l = Load();
        if (l.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase)) > 0) { Save(l); return false; }
        if (!File.Exists(full)) throw new FileNotFoundException("Die Datei gibt es nicht mehr.");
        l.Insert(0, full); if (l.Count > Max) l.RemoveRange(Max, l.Count - Max);
        Save(l); return true;
    }

    /// <summary>Suche wie in Microsoft 365: alle Wörter müssen im Dateinamen vorkommen (Groß/klein egal).</summary>
    public static bool Matches(string fileName, string query) =>
        query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => fileName.Contains(w, StringComparison.CurrentCultureIgnoreCase));
}
