using System.Text.Json;

namespace Trendo.Core;

/// <summary>Studio „Trendo Musik“ (freigegeben 03.11.2026): ein erzeugtes Lied in der Bibliothek.
/// <paramref name="Kind"/>: „neu“, „abschnitt“ (Sekunden neu erzeugt), „cover“ (anderer Stil). <paramref name="ParentId"/> verweist auf das Ausgangslied.</summary>
public sealed class MusicSong
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Title { get; set; } = "";
    public string Style { get; set; } = "";
    public string Lyrics { get; set; } = "";
    public string Language { get; set; } = "de";
    public double Seconds { get; set; }
    public int? Bpm { get; set; }
    public string? Key { get; set; }
    public string? TimeSignature { get; set; }
    public string File { get; set; } = "";
    public string Kind { get; set; } = "neu";
    public string? ParentId { get; set; }
    public bool Favorite { get; set; }
    /// <summary>Cover-Bild (JPEG, 1400 × 1400) – siehe <see cref="SongCover"/>; leer = noch keins.</summary>
    public string CoverFile { get; set; } = "";
    public string CoverLayout { get; set; } = "unten";
    /// <summary>Name auf dem Cover (Künstlername); leer = ohne Namen.</summary>
    public string Artist { get; set; } = "";
    public int CoverSeed { get; set; }
    /// <summary>M5: Ergebnis der Song-Analyse (oder null).</summary>
    public SongAnalysis? Analysis { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
}

/// <summary>Liederbibliothek: Liste in „musik/lieder.json“, die Lieder selbst im Projektordner „Musik“. Löschen = Papierkorb von Windows (nie endgültig).</summary>
public sealed class SongLibrary(string dataRoot)
{
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    string FilePath => Path.Combine(dataRoot, "musik", "lieder.json");

    public List<MusicSong> All()
    {
        try { return System.IO.File.Exists(FilePath) ? JsonSerializer.Deserialize<List<MusicSong>>(System.IO.File.ReadAllText(FilePath), Opt) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    void Save(List<MusicSong> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".neu"; System.IO.File.WriteAllText(tmp, JsonSerializer.Serialize(list, Opt)); SafeFile.Move(tmp, FilePath);
    }

    /// <summary>Neueste zuerst; nur Lieder, deren Datei noch da ist.</summary>
    public List<MusicSong> Visible(bool favoritesOnly = false, string? search = null) =>
        All().Where(s => System.IO.File.Exists(s.File) && (!favoritesOnly || s.Favorite)
                && (string.IsNullOrWhiteSpace(search) || s.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || s.Style.Contains(search, StringComparison.OrdinalIgnoreCase) || s.Lyrics.Contains(search, StringComparison.OrdinalIgnoreCase)))
             .OrderByDescending(s => s.Created).ToList();

    public MusicSong Add(MusicSong s) { var l = All(); l.Add(s); Save(l); return s; }
    public MusicSong? Get(string id) => All().FirstOrDefault(s => s.Id == id);

    public void Update(string id, Action<MusicSong> change)
    {
        var l = All(); var s = l.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Dieses Lied gibt es nicht mehr.");
        change(s); Save(l);
    }

    public void Rename(string id, string title) { var t = title.Trim(); if (t.Length == 0) return; Update(id, s => s.Title = t.Length > 80 ? t[..80] : t); }
    public void ToggleFavorite(string id) => Update(id, s => s.Favorite = !s.Favorite);

    /// <summary>Aus der Liste nehmen; die Datei geht an <paramref name="toRecycleBin"/> (Papierkorb von Windows).</summary>
    public void Remove(string id, Action<string> toRecycleBin)
    {
        var l = All(); var s = l.FirstOrDefault(x => x.Id == id); if (s is null) return;
        if (System.IO.File.Exists(s.File)) toRecycleBin(s.File);
        l.Remove(s); Save(l);
    }

    /// <summary>Titel aus der Beschreibung: erste Liedzeile oder die ersten Wörter der Beschreibung.</summary>
    public static string TitleFrom(string style, string lyrics)
    {
        var line = lyrics.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0 && !x.StartsWith('['));
        var t = line ?? string.Join(' ', style.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Take(5));
        t = t.Trim(' ', ',', '.'); if (t.Length == 0) t = "Neues Lied";
        return t.Length > 48 ? t[..48].TrimEnd() + "…" : char.ToUpper(t[0]) + t[1..];
    }

    /// <summary>Abschnitts-Marken für den Liedtext (deutsch angezeigt, an ACE-Step in der englischen Form, die das Modell kennt).</summary>
    public static readonly (string Label, string Tag)[] Sections = [("Intro", "[Intro]"), ("Strophe", "[Verse]"), ("Vor-Refrain", "[Pre-Chorus]"), ("Refrain", "[Chorus]"), ("Bridge", "[Bridge]"), ("Solo", "[Instrumental]"), ("Outro", "[Outro]")];

    /// <summary>Deutsche Marken ([Strophe], [Refrain] …) in die Form bringen, die ACE-Step kennt.</summary>
    public static string ToAceLyrics(string lyrics)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["[Strophe]"] = "[Verse]", ["[Refrain]"] = "[Chorus]", ["[Vor-Refrain]"] = "[Pre-Chorus]", ["[Solo]"] = "[Instrumental]", ["[Instrumental]"] = "[Instrumental]" };
        return string.Join('\n', lyrics.Split('\n').Select(l => map.TryGetValue(l.Trim(), out var t) ? t : l));
    }

    /// <summary>Schnelle Stil-Bausteine zum Antippen.</summary>
    public static readonly string[] StyleChips = ["Pop", "Rock", "Schlager", "Hip-Hop", "Rap", "Elektro", "House", "Jazz", "Blues", "Soul", "Funk", "Klassik", "Filmmusik", "Akustik", "Folk", "Country", "Reggae", "Metal", "Lo-Fi", "Ambient",
        "fröhlich", "traurig", "romantisch", "episch", "ruhig", "tanzbar", "Frauenstimme", "Männerstimme", "Chor", "Klavier", "Gitarre", "Streicher", "Synthesizer"];
}
