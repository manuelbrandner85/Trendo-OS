using System.Globalization;

namespace Trendo.Core;

/// <summary>Trendo Musik wie Suno (V2/V3/V17, freigegeben 07.11.2026, Clean-Room): Liedideen-Würfel, Stimmung aus einem Bild,
/// „Mehr Optionen“ (Stile ausschließen, Stimme, Dauer, Max, Verrücktheit, Stil-Treue, Audio-Treue, Vielfalt) als Einstellungen der lokalen Musik-KI ACE-Step 1.5.
/// Zuordnung: Verrücktheit → Temperatur des Planungsmodells, Stil-Treue → dessen Führungsstärke, Vielfalt → top-p, Audio-Treue → Stärke der Klang-Referenz,
/// Stile ausschließen → Ausschluss-Prompt des Planungsmodells und aus dem Stil gestrichen. Was nach Messung nicht wirkt, steht nicht in der Oberfläche.</summary>
public static class MusicSuno
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ───────── V2: Liedideen ─────────
    static readonly string[] Moods = ["fröhliches", "melancholisches", "kraftvolles", "verträumtes", "nachdenkliches", "tanzbares", "ruhiges", "episches", "verspieltes", "sehnsüchtiges"];
    static readonly string[] Genres = ["Indie-Pop-Lied", "Akustik-Folk-Lied", "Synthwave-Stück", "Soul-Ballade", "Deutschrap-Track", "Country-Song", "Lo-Fi-Beat mit Gesang", "Rocksong", "House-Track mit Gesang", "Jazz-Ballade", "Reggae-Lied", "Schlager", "Chanson", "Funk-Song", "Dream-Pop-Lied"];
    static readonly string[] Topics = ["über einen Roadtrip ans Meer", "über die erste Wohnung", "über alte Freunde", "über einen verregneten Sonntag", "über den Mut, neu anzufangen", "über eine Nacht in der Großstadt",
        "über den Sommer am See", "über Heimweh", "über ein Lagerfeuer im Herbst", "über das Warten auf eine Nachricht", "über einen Spaziergang im Schnee", "über das Fahrradfahren bei Sonnenaufgang"];
    static readonly string[] Colors = ["mit Akustikgitarre", "mit warmem Klavier", "mit Streichern", "mit Bläsern", "mit analogem Synthesizer", "mit Frauenstimme", "mit Männerstimme", "mit Chor im Refrain", "mit E-Gitarre", "mit Handclaps"];

    /// <summary>Eine Liedidee in einem Satz (gleicher Zufallswert → gleiche Idee).</summary>
    public static string Idea(int seed)
    {
        var r = new Random(seed);
        return $"{Moods[r.Next(Moods.Length)]} {Genres[r.Next(Genres.Length)]} {Topics[r.Next(Topics.Length)]} {Colors[r.Next(Colors.Length)]}";
    }

    /// <summary>V13: ein Thema (für „Mein Geschmack“).</summary>
    public static string Topic(int seed) => Topics[new Random(seed).Next(Topics.Length)];

    /// <summary>V17: die Idee des Tages – für alle gleich an diesem Datum, morgen eine andere.</summary>
    public static string DailyIdea(DateOnly day) => Idea(day.Year * 1000 + day.DayOfYear);

    // ───────── V2: Stimmung aus einem Bild ─────────
    /// <summary>Bildwerte (Mittel über alle Pixel): Farbton 0–360, Sättigung 0–1, Helligkeit 0–1, Kontrast (Standardabweichung der Helligkeit) 0–0,5.</summary>
    public sealed record ImageStats(double Hue, double Saturation, double Lightness, double Contrast);

    /// <summary>Bild → Stimmungs- und Klangwörter für den Stil (nachvollziehbar, ohne KI): warm/kalt aus dem Farbton, hell/düster aus der Helligkeit,
    /// lebhaft/ruhig aus Sättigung und Kontrast.</summary>
    public static string ImageMood(ImageStats s)
    {
        var words = new List<string>();
        var warm = s.Hue is < 70 or > 320; var cold = s.Hue is > 170 and < 280;
        if (s.Lightness < 0.28) words.Add(warm ? "dunkel und warm, nächtlich" : "düster, nächtlich");
        else if (s.Lightness > 0.68) words.Add(warm ? "hell und sonnig" : "hell und luftig");
        else words.Add(warm ? "warm" : cold ? "kühl" : "natürlich");
        if (s.Saturation > 0.45 && s.Contrast > 0.18) words.Add("lebhaft, energiegeladen, kräftiger Groove");
        else if (s.Saturation < 0.18) words.Add("zurückhaltend, nachdenklich, sparsam instrumentiert");
        else words.Add("ruhig fließend");
        if (cold && s.Lightness > 0.45) words.Add("weite Hallräume, schwebende Flächen");
        if (warm && s.Lightness > 0.45) words.Add("Akustikgitarre, sommerlich");
        if (s.Hue is > 80 and < 170 && s.Saturation > 0.25) words.Add("naturnah, organisch");
        return string.Join(", ", words);
    }

    // ───────── V3: Mehr Optionen ─────────
    public enum Voice { Auto, Female, Male }

    /// <summary>Einstellungen wie bei Suno („Advanced“). Regler 0–100, 50 = Grundeinstellung der Musik-KI.</summary>
    public sealed record Options(string Exclude = "", Voice VoiceGender = Voice.Auto, int? Seconds = null, bool Max = false,
        int Weirdness = 50, int StyleInfluence = 50, int AudioInfluence = 50, int Variety = 50)
    {
        public bool IsDefault => this == new Options();
    }

    /// <summary>Ausschluss-Wörter (Komma/Strichpunkt getrennt).</summary>
    public static List<string> ExcludeTerms(string exclude) =>
        [.. (exclude ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(t => t.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Stil ohne ausgeschlossene Begriffe und mit der gewählten Stimme.</summary>
    public static string ApplyToStyle(string style, Options o)
    {
        var parts = (style ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var ex = ExcludeTerms(o.Exclude);
        parts.RemoveAll(p => ex.Any(e => p.Contains(e, StringComparison.OrdinalIgnoreCase)));
        if (o.VoiceGender != Voice.Auto)
        {
            parts.RemoveAll(p => p.Contains("female vocal", StringComparison.OrdinalIgnoreCase) || p.Contains("male vocal", StringComparison.OrdinalIgnoreCase) || p.Contains("Frauenstimme", StringComparison.OrdinalIgnoreCase) || p.Contains("Männerstimme", StringComparison.OrdinalIgnoreCase));
            parts.Add(o.VoiceGender == Voice.Female ? "female vocals" : "male vocals");
        }
        return string.Join(", ", parts);
    }

    static double Lerp(double a, double b, int v) => a + (b - a) * Math.Clamp(v, 0, 100) / 100.0;
    static string F(double v) => v.ToString("0.###", Inv);

    /// <summary>Zusätzliche Felder für ACE-Step. Nur geänderte Regler werden gesendet (sonst gilt die Grundeinstellung der Musik-KI).
    /// Grundwerte von ACE-Step 1.5: Temperatur 0,85, Führung 2,5, top-p 0,9; Klang-Referenz 1,0.</summary>
    public static Dictionary<string, string> AceFields(Options o, bool hasReference)
    {
        var f = new Dictionary<string, string>();
        if (o.Weirdness != 50) f["lm_temperature"] = F(o.Weirdness < 50 ? Lerp(0.55, 0.85, o.Weirdness * 2) : Lerp(0.85, 1.35, (o.Weirdness - 50) * 2));
        if (o.StyleInfluence != 50) f["lm_cfg_scale"] = F(o.StyleInfluence < 50 ? Lerp(1.0, 2.5, o.StyleInfluence * 2) : Lerp(2.5, 5.0, (o.StyleInfluence - 50) * 2));
        if (o.Variety != 50) f["lm_top_p"] = F(o.Variety < 50 ? Lerp(0.6, 0.9, o.Variety * 2) : Lerp(0.9, 1.0, (o.Variety - 50) * 2));
        if (hasReference && o.AudioInfluence != 50) f["audio_cover_strength"] = F(Lerp(0.2, 1.0, o.AudioInfluence));
        var ex = ExcludeTerms(o.Exclude);
        if (ex.Count > 0) f["lm_negative_prompt"] = string.Join(", ", ex);
        if (o.Max) { f["inference_steps"] = "64"; f["guidance_scale"] = "7.0"; }
        return f;
    }

    /// <summary>Kurzbeschreibung der gesetzten Optionen (für die Lied-Notiz und den Herkunftsnachweis).</summary>
    public static string Describe(Options o)
    {
        var p = new List<string>();
        if (ExcludeTerms(o.Exclude) is { Count: > 0 } ex) p.Add("ohne " + string.Join("/", ex));
        if (o.VoiceGender != Voice.Auto) p.Add(o.VoiceGender == Voice.Female ? "Frauenstimme" : "Männerstimme");
        if (o.Seconds is int s) p.Add($"{s / 60}:{s % 60:00} min");
        if (o.Max) p.Add("Max");
        if (o.Weirdness != 50) p.Add($"Verrücktheit {o.Weirdness}");
        if (o.StyleInfluence != 50) p.Add($"Stil-Treue {o.StyleInfluence}");
        if (o.AudioInfluence != 50) p.Add($"Audio-Treue {o.AudioInfluence}");
        if (o.Variety != 50) p.Add($"Vielfalt {o.Variety}");
        return string.Join(" · ", p);
    }
}
