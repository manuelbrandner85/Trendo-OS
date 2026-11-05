using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Trendo Musik Claude 26 (05.11.2026): Claude arbeitet als Produzent – Liedstruktur, Text-Rhythmus, Stilwahl, Mastering-Ziel.
/// Hier die Prüfung, die Trendo selbst rechnet (Claude bekommt sie als Grundlage und Trendo zeigt sie in der Vorschau): Aufbau, Silben je Zeile
/// im Vergleich zum Abschnitt, Reimschema, Silbendichte fürs Tempo. Neue Fassungen entstehen nur als Entwurf – erzeugt wird erst mit „Erstellen“.</summary>
public static partial class SongProducer
{
    /// <summary>Mastering-Ziele wie bei Profi-Mastering-Diensten: integrierte Lautheit (LUFS) und Spitze (dBTP).</summary>
    public sealed record Target(string Id, string Label, double Lufs, double TruePeak, string Note);

    public static readonly Target[] Targets =
    [
        new("streaming", "Streaming", -14, -1.2, "Spotify, YouTube, Apple Music – die übliche Wahl"),
        new("laut", "Laut", -9, -0.8, "Club, Radio, Werbung – kräftig, weniger Dynamik"),
        new("podcast", "Sprache & Podcast", -16, -1.5, "Hörbücher, Podcasts, ruhige Videos"),
        new("dynamisch", "Dynamisch", -18, -1.0, "Akustik, Klassik, Jazz – viel Luft zwischen leise und laut"),
    ];

    public static Target TargetOf(string? id) => Targets.FirstOrDefault(t => t.Id == id) ?? Targets[0];

    public sealed record LineCheck(string Text, int Syllables, string Rhyme, string? Hint);
    public sealed record SectionCheck(string Name, IReadOnlyList<LineCheck> Lines, int Median);
    public sealed record Review(IReadOnlyList<SectionCheck> Sections, IReadOnlyList<string> Structure, IReadOnlyList<string> Notes)
    {
        public int Issues => Sections.Sum(s => s.Lines.Count(l => l.Hint is not null)) + Notes.Count;
    }

    static string LastWord(string line) => Regex.Matches(line, @"\p{L}+") is { Count: > 0 } m ? m[^1].Value : "";

    /// <summary>Liedtext prüfen wie ein Produzent: je Abschnitt die mittlere Silbenzahl, Ausreißer (über 35 % daneben), Reimschema (a, b, …),
    /// zu dichte Zeilen fürs Tempo (mehr als zwei Silben je Schlag bei zwei Takten je Zeile), fehlender oder einmaliger Refrain.</summary>
    public static Review Check(string lyrics, double bpm = 0, string? meter = null)
    {
        var secs = new List<(string Name, List<string> Lines)>(); var cur = ("", new List<string>());
        foreach (var raw in (lyrics ?? "").Replace("\r", "").Split('\n'))
        {
            var l = raw.Trim(); if (l.Length == 0) continue;
            if (l.StartsWith('[') && l.EndsWith(']')) { if (cur.Item2.Count > 0 || cur.Item1.Length > 0) secs.Add(cur); cur = (l.Trim('[', ']').Trim(), []); continue; }
            cur.Item2.Add(l);
        }
        if (cur.Item2.Count > 0 || cur.Item1.Length > 0) secs.Add(cur);
        var beatsPerLine = 2 * (int.TryParse((meter ?? "4/4").Split('/')[0], out var m) && m is > 0 and <= 12 ? m : 4);
        var res = new List<SectionCheck>();
        foreach (var (name, lines) in secs)
        {
            var syl = lines.Select(SongTools.Syllables).ToList();
            var median = syl.Count == 0 ? 0 : syl.OrderBy(x => x).ElementAt(syl.Count / 2);
            var keys = new List<string>(); var checks = new List<LineCheck>();
            for (var i = 0; i < lines.Count; i++)
            {
                var key = SongTools.RhymeKey(LastWord(lines[i]));
                var letterIdx = keys.FindIndex(k => k.Length >= 2 && k == key); if (letterIdx < 0) { keys.Add(key); letterIdx = keys.Count - 1; }
                string? hint = null;
                if (median >= 4 && syl[i] > median * 1.35) hint = $"{syl[i] - median} Silben zu viel für den Rhythmus";
                else if (median >= 4 && syl[i] < median * 0.65) hint = $"{median - syl[i]} Silben zu wenig – wirkt abgehackt";
                if (hint is null && bpm >= 40 && syl[i] > beatsPerLine * 2.5 * 120 / Math.Max(60, bpm)) hint = "zu dicht fürs Tempo – kürzen oder auf zwei Zeilen";
                checks.Add(new LineCheck(lines[i], syl[i], ((char)('a' + Math.Min(letterIdx, 25))).ToString(), hint));
            }
            res.Add(new SectionCheck(name.Length > 0 ? name : "ohne Abschnitt", checks, median));
        }
        var structure = res.Select(s => s.Name).ToList();
        var notes = new List<string>();
        bool Is(string n, params string[] k) => k.Any(x => n.Contains(x, StringComparison.OrdinalIgnoreCase));
        var choruses = structure.Count(n => Is(n, "refrain", "chorus"));
        if (res.Sum(s => s.Lines.Count) > 0)
        {
            if (choruses == 0) notes.Add("Kein Refrain – ein wiederkehrender Refrain macht das Lied einprägsam.");
            else if (choruses == 1 && structure.Count >= 3) notes.Add("Der Refrain kommt nur einmal – meist trägt er zwei- bis dreimal.");
            if (structure.Count > 0 && Is(structure[0], "refrain", "chorus") && structure.Count < 3) notes.Add("Das Lied beginnt mit dem Refrain und hat kaum Strophen – für Spannung erst eine Strophe.");
            if (res.Any(s => s.Lines.Count > 10)) notes.Add("Ein Abschnitt hat mehr als zehn Zeilen – teilen oder kürzen.");
            if (structure.Count == 1 && structure[0] == "ohne Abschnitt") notes.Add("Keine Abschnitts-Marken – [Strophe], [Refrain] helfen der Musik-KI beim Aufbau.");
        }
        return new Review(res, structure, notes);
    }

    /// <summary>Kurzfassung für die Vorschau: „4 Abschnitte · 2 Rhythmus-Hinweise · Reime aabb“.</summary>
    public static string Summary(Review r)
    {
        var hints = r.Sections.Sum(s => s.Lines.Count(l => l.Hint is not null));
        var rhymes = r.Sections.FirstOrDefault(s => s.Lines.Count >= 2) is { } f ? string.Concat(f.Lines.Select(l => l.Rhyme)) : "";
        return $"{r.Sections.Count} Abschnitte · {(hints == 0 ? "Rhythmus gleichmäßig" : $"{hints} Rhythmus-Hinweis{(hints == 1 ? "" : "e")}")}" + (rhymes.Length > 0 ? $" · Reime {rhymes}" : "") + (r.Notes.Count > 0 ? $" · {r.Notes.Count} Aufbau-Hinweis{(r.Notes.Count == 1 ? "" : "e")}" : "");
    }

    /// <summary>Grundlage für Claude: das Lied, die Prüfung oben, Fassungen und was Claude vorschlagen darf.</summary>
    public static string Briefing(MusicSong s, IReadOnlyList<MusicSong> versions)
    {
        var inv = CultureInfo.InvariantCulture;
        var bpm = s.Analysis?.Bpm ?? s.Bpm ?? 0; var key = s.Analysis?.Key ?? s.Key; var meter = s.Analysis?.Meter ?? s.TimeSignature ?? "4/4";
        var sb = new StringBuilder();
        sb.Append($"Trendo Musik – du bist Produzent. Lied „{s.Title}“, Stil „{s.Style}“, {SongTime(s.Seconds)}, Sprache {s.Language}");
        if (bpm > 0) sb.Append(inv, $", {bpm:0} BPM"); if (!string.IsNullOrEmpty(key)) sb.Append($", Tonart {SongTools.KeyDe(key)}"); sb.Append($", {meter}-Takt");
        sb.Append(s.Mastered ? ", gemastert." : ", noch nicht gemastert.");
        var r = Check(s.Lyrics, bpm, meter);
        if (s.Lyrics.Trim().Length > 0 && s.Lyrics != "[Instrumental]")
        {
            sb.Append("\nPrüfung des Liedtexts (Silben je Zeile, Reim, Hinweis): ");
            foreach (var sec in r.Sections)
            {
                sb.Append($"\n[{sec.Name}] Mittel {sec.Median} Silben");
                foreach (var l in sec.Lines) sb.Append($"\n  {l.Syllables} · {l.Rhyme} · {l.Text}" + (l.Hint is { } h ? $"  ← {h}" : ""));
            }
            foreach (var n in r.Notes) sb.Append("\nAufbau: " + n);
        }
        else sb.Append("\nInstrumental bzw. ohne eigenen Text.");
        if (versions.Count > 1) sb.Append("\nFassungen: " + string.Join("; ", versions.Select(v => $"„{v.Title}“ ({v.Style})")));
        sb.Append("\nSo arbeitest du: Erst kurz sagen, was du als Produzent ändern würdest und warum (Aufbau, Rhythmus, Reime, Stil, Mastering-Ziel). Dann als Vorschlag:");
        sb.Append("\n- neuer Liedtext: [[trendo:feld | ms-prod-lyrics | ganzer Text, Abschnitte in geschweiften Klammern wie {Strophe} und {Refrain}, Zeilen mit „ / “ getrennt]]");
        sb.Append("\n- neuer Stil: [[trendo:feld | ms-prod-style | Genre, Instrumente, Stimme, Stimmung, Tempo]]");
        sb.Append("\n- Mastering-Ziel: [[trendo:feld | ms-prod-master | " + string.Join(" oder ", Targets.Select(t => t.Id)) + "]] (" + string.Join("; ", Targets.Select(t => $"{t.Id} = {t.Lufs.ToString(inv)} LUFS, {t.Note}")) + ")");
        sb.Append("\nText und Stil werden eine neue Fassung (der Nutzer erzeugt sie mit „Erstellen“ und vergleicht sie mit A/B neben dem Original); das Mastering wirkt auf dieses Lied. Keine Namen echter Künstler, keine Zeilen aus bekannten Liedern, Liedtext nur ändern, wenn der Nutzer einen eigenen Text hat oder darum bittet.");
        return sb.ToString();
        static string SongTime(double sec) => TimeSpan.FromSeconds(sec).ToString(sec >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
    }

    /// <summary>Claudes Liedtext lesen: „ / “ als Zeilenwechsel, Marken auf eigene Zeilen; prüft Länge und Abschnitts-Marken.</summary>
    public static (string? Text, string? Error) ParseLyrics(string value)
    {
        var t = Regex.Replace(value.Replace("\r", ""), @"\{([^{}\n]{1,30})\}", "[$1]"); // im Vorschlag sind eckige Klammern nicht möglich
        t = Regex.Replace(t, @"\s+/\s+", "\n");
        t = Regex.Replace(t, @"\s*(\[[^\]\n]{1,30}\])\s*", "\n$1\n").Trim();
        t = Regex.Replace(t, @"\n{3,}", "\n\n");
        if (t.Length < 10) return (null, "kein Liedtext");
        if (t.Length > 4000) return (null, "länger als 4000 Zeichen");
        if (!t.Contains('[')) return (null, "ohne Abschnitts-Marken wie {Strophe} und {Refrain}");
        return (t, null);
    }

    /// <summary>Stil prüfen: höchstens 300 Zeichen, nicht leer.</summary>
    public static (string? Text, string? Error) ParseStyle(string value)
    {
        var t = Regex.Replace(value, @"\s+", " ").Trim().Trim(',');
        if (t.Length < 3) return (null, "kein Stil");
        if (t.Length > 300) return (null, "länger als 300 Zeichen");
        return (t, null);
    }
}
