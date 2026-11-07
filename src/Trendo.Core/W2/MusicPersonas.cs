using System.Text.Json;

namespace Trendo.Core;

/// <summary>Trendo Musik V11 wie Suno (07.11.2026): Persona – „Klang dieses Lieds merken“. Eine Persona hält Klangvorbild (Kopie der Tondatei),
/// Stil-Wörter und Sprache eines in Trendo erstellten Lieds fest und wird beim Erstellen als Klangvorbild (ACE-Step reference_audio) und Stil vorgegeben.
/// Wie bei Suno nur aus selbst erstellten Liedern – eingeführte Dateien (Art „eigen“) und Fremdstimmen sind ausgeschlossen.
/// Gespeichert in „musik/personas.json“, Tondateien in „musik/personas/“.</summary>
public sealed class MusicPersonas(string dataRoot)
{
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    /// <summary>Arten, aus denen eine Persona entstehen darf (in Trendo erzeugt).</summary>
    public static readonly string[] AllowedKinds = ["neu", "abschnitt", "cover", "remaster", "gekuerzt", "sample", "beat", "laenger", "produzent", "aufnahme"];

    public sealed class Persona
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public string Name { get; set; } = "";
        public string Style { get; set; } = "";
        public string? Language { get; set; }
        public string SongId { get; set; } = "";
        public string SongTitle { get; set; } = "";
        public string File { get; set; } = "";
        public int Uses { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    }

    string Root => Path.Combine(dataRoot, "musik");
    string FilePath => Path.Combine(Root, "personas.json");
    public string AudioDir => Path.Combine(Root, "personas");

    public List<Persona> All()
    {
        try { return System.IO.File.Exists(FilePath) ? JsonSerializer.Deserialize<List<Persona>>(System.IO.File.ReadAllText(FilePath), Opt) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    void Save(List<Persona> l)
    {
        Directory.CreateDirectory(Root);
        var tmp = FilePath + ".neu"; System.IO.File.WriteAllText(tmp, JsonSerializer.Serialize(l, Opt)); SafeFile.Move(tmp, FilePath);
    }

    public Persona? Get(string id) => All().FirstOrDefault(p => p.Id == id);

    /// <summary>Warum aus diesem Lied keine Persona werden kann – null = geht.</summary>
    public static string? Problem(MusicSong s)
    {
        if (!System.IO.File.Exists(s.File)) return "Die Tondatei dieses Lieds fehlt.";
        if (s.Kind == "eigen") return "Personas entstehen nur aus Liedern, die in Trendo erstellt wurden – für deine eigene Stimme die Seite „Stimmen“ nutzen.";
        if (!AllowedKinds.Contains(s.Kind)) return $"Aus dieser Art ({s.Kind}) entsteht keine Persona – nur aus erstellten Liedern.";
        if (s.Seconds is > 0 and < 5) return "Das Lied ist zu kurz für eine Persona (mindestens 5 Sekunden).";
        return null;
    }

    /// <summary>Persona aus einem Lied anlegen: Tondatei wird kopiert (das Lied darf danach gelöscht werden).</summary>
    public Persona Create(MusicSong s, string name)
    {
        if (Problem(s) is { } p) throw new ArgumentException(p);
        var n = name.Trim(); if (n.Length == 0) n = s.Title;
        if (n.Length > 40) n = n[..40];
        var l = All();
        if (l.Any(x => x.Name.Equals(n, StringComparison.CurrentCultureIgnoreCase))) throw new ArgumentException($"Eine Persona „{n}“ gibt es schon.");
        var per = new Persona { Name = n, Style = (s.Style ?? "").Trim(), Language = s.Language, SongId = s.Id, SongTitle = s.Title };
        Directory.CreateDirectory(AudioDir);
        per.File = Path.Combine(AudioDir, per.Id + Path.GetExtension(s.File).ToLowerInvariant());
        System.IO.File.Copy(s.File, per.File, true);
        l.Add(per); Save(l); return per;
    }

    public void Rename(string id, string name) { var n = name.Trim(); if (n.Length == 0) return; Change(id, p => p.Name = n.Length > 40 ? n[..40] : n); }
    public void SetStyle(string id, string style) => Change(id, p => p.Style = style.Trim());
    public void Used(string id) => Change(id, p => p.Uses++);

    public void Delete(string id)
    {
        var l = All(); var p = l.FirstOrDefault(x => x.Id == id); if (p is null) return;
        l.Remove(p); Save(l);
        try { if (System.IO.File.Exists(p.File)) System.IO.File.Delete(p.File); } catch (IOException) { }
    }

    void Change(string id, Action<Persona> a) { var l = All(); var p = l.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Diese Persona gibt es nicht mehr."); a(p); Save(l); }

    /// <summary>Stil beim Erstellen: Persona-Stil vorn, eigene Wörter dahinter, doppelte Wörter nur einmal.</summary>
    public static string MergeStyle(string personaStyle, string own)
    {
        var parts = new List<string>();
        foreach (var w in (personaStyle + "," + own).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!parts.Contains(w, StringComparer.OrdinalIgnoreCase)) parts.Add(w);
        return string.Join(", ", parts);
    }
}
