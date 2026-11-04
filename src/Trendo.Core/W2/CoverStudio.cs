using System.Globalization;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Trendo Musik – Cover-Werkstatt M1–M8 (freigegeben 04.11.2026, „ja alles“).
/// M1 Lied suchen (MusicBrainz, ohne Liedtext) · M2 Bauplan des eigenen Originals · M3 Auftrag fürs Cover · M4 Vergleich mit Ampel ·
/// M5 erfundene Epochen-Stimmen · M8 Klang wie damals. Grenzen (fest): keine Stimme einer echten Person, Liedtexte fügt der Nutzer selbst ein.</summary>
public static class CoverStudio
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ───────────── M1: Lied suchen ─────────────
    public sealed record Hit(string Mbid, string Title, string Artist, int? Year, string Album, double Seconds)
    {
        public string Page => $"https://musicbrainz.org/recording/{Mbid}";
    }

    /// <summary>Suche nach Aufnahmen; „Künstler – Titel“ wird getrennt gesucht.</summary>
    public static Uri SearchUri(string query)
    {
        var parts = query.Split(['–', '—', '-'], 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var q = parts.Length == 2 ? $"recording:\"{parts[1]}\" AND artist:\"{parts[0]}\"" : $"recording:\"{query.Trim()}\"";
        return new("https://musicbrainz.org/ws/2/recording/?fmt=json&limit=8&query=" + Uri.EscapeDataString(q));
    }

    public static List<Hit> ParseHits(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            if (!d.RootElement.TryGetProperty("recordings", out var recs)) return [];
            var list = new List<Hit>();
            foreach (var r in recs.EnumerateArray())
            {
                var id = r.TryGetProperty("id", out var i) ? i.GetString() ?? "" : ""; if (id.Length == 0) continue;
                var artist = r.TryGetProperty("artist-credit", out var ac) && ac.GetArrayLength() > 0 && ac[0].TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                int? year = r.TryGetProperty("first-release-date", out var fr) && fr.GetString() is { Length: >= 4 } fd && int.TryParse(fd[..4], out var y) ? y : null;
                var album = r.TryGetProperty("releases", out var rel) && rel.GetArrayLength() > 0 && rel[0].TryGetProperty("title", out var rt) ? rt.GetString() ?? "" : "";
                var sec = r.TryGetProperty("length", out var len) && len.ValueKind == JsonValueKind.Number ? len.GetDouble() / 1000 : 0;
                list.Add(new(id, r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "", artist, year, album, Math.Round(sec)));
            }
            // gleiche Aufnahme mehrfach (Wiederveröffentlichungen): die älteste zuerst behalten
            return [.. list.GroupBy(h => (h.Title.ToLowerInvariant(), h.Artist.ToLowerInvariant())).Select(g => g.OrderBy(h => h.Year ?? 9999).First())];
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return []; }
    }

    /// <summary>Wo der Text legal steht – Trendo holt ihn nicht (urheberrechtlich geschützt), der Nutzer fügt ihn selbst ein.</summary>
    public const string LyricsHint = "Den Liedtext findest du im CD-Booklet, auf der offiziellen Seite des Künstlers oder bei lizenzierten Diensten. Füge ihn unten ein – er bleibt nur auf diesem PC.";

    // ───────────── M2: Bauplan des Originals ─────────────
    public sealed record Section(double Start, double End, double Energy);
    /// <summary>Melodie: MIDI-Töne alle <paramref name="Hop"/> Sekunden (null = kein Gesang).</summary>
    public sealed record Blueprint(double Duration, double Bpm, string Key, string Meter, IReadOnlyList<string> Chords, string? Range, IReadOnlyList<Section> Sections, double Hop, IReadOnlyList<double?> Melody)
    {
        public string KeyDe => SongTools.KeyDe(Key);
        public double MelodyLow => Melody.Where(m => m is not null).Select(m => m!.Value).DefaultIfEmpty(0).Min();
        public double MelodyHigh => Melody.Where(m => m is not null).Select(m => m!.Value).DefaultIfEmpty(0).Max();
    }

    /// <summary>Aus den drei Analysen (Grunddaten, Abschnitte, Melodie) einen Bauplan machen.</summary>
    public static Blueprint Build(SongAnalysis a, string? sectionsJson, string? melodyJson)
    {
        var secs = new List<Section>();
        if (sectionsJson is not null)
            try
            {
                var (_, _, _, parts) = SongUnderstanding.ParseSections(sectionsJson);
                secs.AddRange(parts.Select(p => new Section(p.Start, p.End, p.Energy)));
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
        double hop = 0.05; var mel = new List<double?>();
        if (melodyJson is not null)
            try
            {
                using var d = JsonDocument.Parse(melodyJson);
                hop = d.RootElement.GetProperty("hop").GetDouble();
                mel.AddRange(d.RootElement.GetProperty("midi").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Number ? (double?)x.GetDouble() : null));
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
        var range = a.Range is { } r ? $"{r.Low}–{r.High} ({r.Voice})" : null;
        return new(a.Duration, a.Bpm, a.Key, a.Meter, a.Chords, range, secs, hop, mel);
    }

    public static string Note(double midi) { var n = (int)Math.Round(midi); return new[] { "C", "Cis", "D", "Es", "E", "F", "Fis", "G", "As", "A", "B", "H" }[((n % 12) + 12) % 12] + (n / 12 - 1); }

    // ───────────── M5: erfundene Epochen-Stimmen ─────────────
    /// <summary>Stimmcharakter einer Zeit – beschrieben, nie eine echte Person. Sound: passender Klang aus M8.</summary>
    public sealed record EraVoice(string Id, string Label, string Hint, string Prompt, string Sound);

    public static readonly EraVoice[] Voices =
    [
        new("frei", "Eigene Wahl", "Stimme nach deinem Stil-Text", "", "heute"),
        new("rocknroll56", "Rock'n'Roll-Bariton 1956", "warm, tief, mit Schluckauf-Akzenten und Slapback-Echo", "male baritone rock and roll vocal, 1956 style, warm low voice, hiccup inflections, slapback echo, energetic", "1956"),
        new("croon58", "Crooner-Ballade 1958", "samtig, nah am Mikro, viel Vibrato", "male crooner vocal, late 1950s ballad style, velvety close-mic voice, rich vibrato, smooth phrasing", "1956"),
        new("gospel68", "Gospel-Soul 1968", "kraftvoll, rau, große Bögen", "powerful soulful gospel vocal, 1968 style, raspy belting, emotional melisma, church choir backing", "1968"),
        new("vegas72", "Show-Stimme 1970er", "groß, dramatisch, mit Orchester", "big dramatic male showman vocal, early 1970s Las Vegas show style, wide vibrato, brass and orchestra", "1972"),
        new("disco78", "Disco-Falsett 1978", "hoch, leicht, tanzbar", "airy falsetto disco vocal, 1978 style, tight harmonies, four on the floor groove", "1978"),
        new("wave85", "New-Wave-Stimme 1985", "kühl, hallig, Synthesizer", "cool detached new wave vocal, 1985 style, gated reverb, analog synthesizers", "1985"),
        new("grunge93", "Rockstimme 1990er", "rau, laut, verzerrte Gitarren", "gritty raspy rock vocal, early 1990s style, loud distorted guitars", "1993"),
        new("diva98", "Pop-Diva 1998", "hell, große Läufe", "bright female pop diva vocal, late 1990s style, powerful runs and riffs", "1998"),
        new("schlager75", "Schlager-Stimme 1970er", "freundlich, klar, mitsingbar", "friendly clear german schlager vocal, 1970s style, catchy sing-along chorus", "1972"),
    ];

    public static EraVoice Voice(string id) => Voices.FirstOrDefault(v => v.Id == id) ?? Voices[0];

    // ───────────── M3: Auftrag fürs Cover ─────────────
    /// <summary>Stil-Text für ACE-Step: Tempo, Tonart und Takt des Originals, Genre und Stimme. <paramref name="closeness"/> 0–1: wie nah am Original.</summary>
    public static string CoverStyle(Blueprint b, string genre, EraVoice v)
    {
        var parts = new List<string>();
        if (genre.Trim().Length > 0) parts.Add(genre.Trim().TrimEnd(','));
        if (v.Prompt.Length > 0) parts.Add(v.Prompt);
        parts.Add($"{Math.Round(b.Bpm).ToString(Inv)} BPM");
        if (b.Key.Length > 0) parts.Add("key of " + b.Key);
        if (b.Meter.Length > 0) parts.Add(b.Meter + " time");
        parts.Add("same melody and song structure as the source, original fictional singer");
        return string.Join(", ", parts);
    }

    /// <summary>Nähe zum Original (Schieber) → ACE-Step-Stärke. Gemessen: unter 0,5 geht die Melodie verloren, über 0,9 klingt es nach Kopie des Arrangements.</summary>
    public static double Strength(double closeness) => Math.Round(0.5 + Math.Clamp(closeness, 0, 1) * 0.4, 2);

    // ───────────── M4: Vergleich mit Ampel ─────────────
    public sealed record Point(string Name, string Original, string Cover, string Light, string Text);
    public sealed record Report(IReadOnlyList<Point> Points, double Score)
    {
        /// <summary>Grün nur ohne roten Punkt – eine falsche Melodie darf nicht durch gleiches Tempo „grün“ werden (echter Lauf 04.11.2026: 0,83 bei 32 % Melodie).</summary>
        public string Light => Score >= 0.8 && Points.All(p => p.Light != "rot") ? "gruen" : Score >= 0.6 ? "gelb" : "rot";
    }

    static readonly string[] Pitch = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    static (int Tonic, bool Minor) ParseKey(string k)
    {
        var p = k.Split(' '); var i = Array.IndexOf(Pitch, p[0]);
        return (i, p.Length > 1 && p[1].StartsWith("min", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Tonarten: gleich = grün; Parallele (A-Moll ↔ C-Dur) oder Quinte daneben = gelb; sonst rot.</summary>
    public static string KeyLight(string a, string b)
    {
        var (ta, ma) = ParseKey(a); var (tb, mb) = ParseKey(b);
        if (ta < 0 || tb < 0) return "gelb";
        if (ta == tb && ma == mb) return "gruen";
        var rel = ma != mb && ((ma ? ta + 3 : ta + 9) % 12 == tb);
        var fifth = ma == mb && (((ta - tb) % 12 + 12) % 12 is 5 or 7);
        return rel || fifth ? "gelb" : "rot";
    }

    /// <summary>Ähnlichkeit zweier Melodien (Höhen und Tiefen), unabhängig von Lage und Oktave, verschiebbar ±2 s.
    /// Gemessen 04.11.2026 an echten Covern: aus dem ganzen Mix sind nur ~25 % der Werte gesungen und Zufall trifft schon 25 % –
    /// darum: jeder Wert zählt, Zufall wird abgezogen (0 = Zufall, 1 = gleich), und unter 7,5 s gemeinsamem Gesang gibt es kein Urteil (null).</summary>
    public const int MinSharedFrames = 150;
    public static double? MelodySimilarity(Blueprint a, Blueprint b)
    {
        static double[] Norm(Blueprint x)
        {
            var v = x.Melody.Select(m => m ?? double.NaN).ToArray();
            var f = v.Where(double.IsFinite).Order().ToArray();
            if (f.Length == 0) return v;
            var med = Math.Round(f[f.Length / 2]);
            return [.. v.Select(m => m - med)];
        }
        var ca = Norm(a); var cb = Norm(b);
        // gleiches Zeitraster: b auf die Werte-Abstände von a bringen
        if (Math.Abs(a.Hop - b.Hop) > 1e-6 && b.Hop > 0) cb = [.. Enumerable.Range(0, (int)(cb.Length * b.Hop / a.Hop)).Select(i => cb[Math.Min(cb.Length - 1, (int)(i * a.Hop / b.Hop))])];
        var maxShift = (int)Math.Round(2 / Math.Max(0.01, a.Hop)); double? best = null;
        for (var s = -maxShift; s <= maxShift; s += 2)
        {
            int n = 0, hits = 0;
            for (var i = 0; i < ca.Length; i++)
            {
                var j = i + s; if (j < 0 || j >= cb.Length || !double.IsFinite(ca[i]) || !double.IsFinite(cb[j])) continue;
                n++; var d = ((ca[i] - cb[j]) % 12 + 18) % 12 - 6; if (Math.Abs(d) <= 1) hits++;
            }
            if (n < MinSharedFrames) continue;
            var score = Math.Clamp((hits / (double)n - 0.25) / 0.75, 0, 1); // 3 von 12 Tönen liegen zufällig innerhalb ±1 Halbton
            if (best is null || score > best) best = score;
        }
        return best;
    }

    public static Report Compare(Blueprint o, Blueprint c)
    {
        var pts = new List<Point>();
        string L(double v, double g, double y) => v >= g ? "gruen" : v >= y ? "gelb" : "rot";
        // Tempo (halbes/doppeltes Tempo zählt als gleich – Messung verwechselt das gern)
        var ratio = c.Bpm <= 0 || o.Bpm <= 0 ? 0 : new[] { c.Bpm / o.Bpm, c.Bpm * 2 / o.Bpm, c.Bpm / 2 / o.Bpm }.MinBy(r => Math.Abs(Math.Log(r)));
        var tempo = ratio <= 0 ? 0 : 1 - Math.Min(1, Math.Abs(Math.Log(ratio)) / Math.Log(1.12));
        pts.Add(new("Tempo", $"{o.Bpm:0} BPM", $"{c.Bpm:0} BPM", L(tempo, 0.7, 0.35), tempo >= 0.7 ? "gleich schnell" : "Tempo weicht ab"));
        var kl = KeyLight(o.Key, c.Key);
        pts.Add(new("Tonart", o.KeyDe, c.KeyDe, kl, kl == "gruen" ? "gleiche Tonart" : kl == "gelb" ? "verwandte Tonart" : "andere Tonart"));
        var meter = o.Meter == c.Meter ? 1 : 0;
        pts.Add(new("Takt", o.Meter, c.Meter, meter == 1 ? "gruen" : "rot", meter == 1 ? "gleicher Takt" : "anderer Takt"));
        var len = o.Duration <= 0 ? 0 : 1 - Math.Min(1, Math.Abs(c.Duration - o.Duration) / o.Duration / 0.15);
        pts.Add(new("Länge", Time(o.Duration), Time(c.Duration), L(len, 0.66, 0.33), len >= 0.66 ? "fast gleich lang" : "Länge weicht ab"));
        var mel = MelodySimilarity(o, c);
        pts.Add(new("Melodie", o.Melody.Count > 0 ? $"{Note(o.MelodyLow)}–{Note(o.MelodyHigh)}" : "–", c.Melody.Count > 0 ? $"{Note(c.MelodyLow)}–{Note(c.MelodyHigh)}" : "–",
            mel is { } mv ? L(mv, 0.5, 0.25) : "gelb", mel is { } m2 ? $"{m2 * 100:0} % gleicher Verlauf (Höhen und Tiefen, Zufall abgezogen)" : "nicht sicher messbar – zu wenig Gesang erkannt"));
        var sec = o.Sections.Count == 0 ? 1 : 1 - Math.Min(1, Math.Abs(c.Sections.Count - o.Sections.Count) / (double)Math.Max(2, o.Sections.Count));
        pts.Add(new("Aufbau", $"{o.Sections.Count} Teile", $"{c.Sections.Count} Teile", L(sec, 0.75, 0.5), sec >= 0.75 ? "gleicher Aufbau" : "Aufbau weicht ab"));
        var score = 0.25 * tempo + 0.2 * (kl == "gruen" ? 1 : kl == "gelb" ? 0.6 : 0) + 0.1 * meter + 0.1 * len + 0.25 * (mel is { } ms ? Math.Min(1, ms / 0.6) : 0.5) + 0.1 * sec;
        return new(pts, Math.Round(score, 2));
    }

    /// <summary>Selbstverbesserung: was ändern, wenn die Ampel nicht grün ist. Null = gut genug oder kein Versuch mehr.</summary>
    public static (double Strength, string Why)? NextTry(Report r, double strength, int attempt, int maxAttempts = 3)
    {
        if (r.Light == "gruen" || attempt >= maxAttempts) return null;
        var weak = r.Points.Where(p => p.Light != "gruen").Select(p => p.Name).ToList();
        var up = weak.Any(n => n is "Melodie" or "Tempo" or "Aufbau" or "Takt") ? 0.08 : 0.03;
        return (Math.Min(0.95, Math.Round(strength + up, 2)), "näher am Original, weil " + string.Join(", ", weak) + " abweicht");
    }

    public static string Time(double s) => $"{(int)(s / 60)}:{(int)(s % 60):00}";

    // ───────────── M8: Klang wie damals ─────────────
    public sealed record EraSound(string Id, string Label, string Hint, string Filter);

    public static readonly EraSound[] Sounds =
    [
        new("heute", "Heute (unverändert)", "kein Effekt", ""),
        new("1956", "1956 – Mono, Röhre, Slapback", "schmales Band, warme Sättigung, kurzes Echo, Mono",
            "highpass=f=90,lowpass=f=7000,acompressor=threshold=-18dB:ratio=3:attack=10:release=200,asoftclip=type=tanh,aecho=0.8:0.4:110:0.25,pan=mono|c0=0.5*c0+0.5*c1,aformat=channel_layouts=stereo"),
        new("1968", "1968 – frühes Stereo, Bandmaschine", "breiter, aber weich, Band-Sättigung",
            "highpass=f=50,lowpass=f=11000,acompressor=threshold=-16dB:ratio=2.5:attack=15:release=250,asoftclip=type=atan,extrastereo=m=1.4,aecho=0.6:0.3:60:0.15"),
        new("1972", "1970er – Studio-Hall, Orchester", "warm, großer Raum",
            "highpass=f=40,lowpass=f=13000,acompressor=threshold=-15dB:ratio=2:attack=20:release=300,aecho=0.7:0.5:90|180:0.2|0.12"),
        new("1978", "1978 – Disco, Vinyl", "satter Bass, glänzende Höhen, leichtes Knistern möglich",
            "bass=g=3:f=90,treble=g=2:f=9000,acompressor=threshold=-14dB:ratio=3:attack=5:release=120"),
        new("1985", "1985 – Hall-Schlagzeug, Digital", "kühl, groß, gated Reverb",
            "highpass=f=40,treble=g=3:f=10000,aecho=0.6:0.5:40|75:0.35|0.25,acompressor=threshold=-12dB:ratio=4:attack=2:release=80"),
        new("1993", "1990er – laut und rau", "kräftig komprimiert",
            "highpass=f=35,acompressor=threshold=-10dB:ratio=5:attack=3:release=60,asoftclip=type=tanh"),
        new("1998", "Ende 1990er – Glanz-Pop", "brillant, breit",
            "highpass=f=30,treble=g=2.5:f=11000,extrastereo=m=1.2,acompressor=threshold=-12dB:ratio=3:attack=5:release=100"),
    ];

    public static EraSound Sound(string id) => Sounds.FirstOrDefault(s => s.Id == id) ?? Sounds[0];

    /// <summary>FFmpeg-Aufruf: Klang anwenden, Lautheit danach auf −14 LUFS (damit „alt“ nicht leiser klingt).</summary>
    public static List<string> SoundArgs(string input, string output, string soundId)
    {
        var s = Sound(soundId);
        var chain = (s.Filter.Length > 0 ? s.Filter + "," : "") + "loudnorm=I=-14:TP=-1:LRA=11";
        return ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-af", chain, "-ar", "44100", .. output.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? new[] { "-c:a", "libmp3lame", "-q:a", "2" } : [], output];
    }
}
