using System.Globalization;
using System.Text;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – S3-16 und S4-18 (07.11.2026): Zeitgenaue Texte – Untertitel als SRT und WebVTT, Sprachfassungen (Deutsch, Italienisch, Englisch)
/// mit exakt gleichen Zeiten wie das Original, Liedtext als LRC (Karaoke). Fehlerhaftes Timing wird gemeldet statt still geschrieben.</summary>
public static class TimedText
{
    public sealed record Cue(double Start, double End, string Text);

    public static readonly (string Code, string Label)[] Languages = [("de", "Deutsch"), ("it", "Italienisch"), ("en", "Englisch")];

    static string T(double s, char sep)
    {
        var ms = (long)Math.Round(Math.Max(0, s) * 1000, MidpointRounding.AwayFromZero);
        return $"{ms / 3600000:00}:{ms / 60000 % 60:00}:{ms / 1000 % 60:00}{sep}{ms % 1000:000}";
    }

    public static List<Cue> FromSrt(string srt) => [.. VideoExport.ReadSrt(srt).Select(c => new Cue(c.Start, c.End, c.Text))];

    /// <summary>Timing prüfen: Beginn vor Ende, keine negativen Zeiten, der Reihe nach, keine Überlappung, optional nicht über die Länge hinaus.</summary>
    public static List<string> Problems(IReadOnlyList<Cue> cues, double length = 0)
    {
        var p = new List<string>();
        for (var i = 0; i < cues.Count; i++)
        {
            var c = cues[i];
            if (c.Start < 0 || c.End <= c.Start) p.Add($"Nr. {i + 1}: Ende ({c.End:0.###} s) liegt nicht nach dem Beginn ({c.Start:0.###} s).");
            if (i > 0 && c.Start < cues[i - 1].End - 1e-6) p.Add($"Nr. {i + 1} beginnt, bevor Nr. {i} endet.");
            if (length > 0 && c.End > length + 0.05) p.Add($"Nr. {i + 1} endet nach dem Ende ({c.End:0.##} s > {length:0.##} s).");
            if (string.IsNullOrWhiteSpace(c.Text)) p.Add($"Nr. {i + 1} ist leer.");
        }
        return p;
    }

    static void Check(IReadOnlyList<Cue> cues, double length) { var p = Problems(cues, length); if (p.Count > 0) throw new ArgumentException("Timing fehlerhaft: " + string.Join(" ", p.Take(3))); }

    public static string ToSrt(IReadOnlyList<Cue> cues, double length = 0)
    {
        Check(cues, length);
        var sb = new StringBuilder();
        for (var i = 0; i < cues.Count; i++) sb.Append($"{i + 1}\n{T(cues[i].Start, ',')} --> {T(cues[i].End, ',')}\n{cues[i].Text.Trim()}\n\n");
        return sb.ToString();
    }

    /// <summary>WebVTT (für Browser, YouTube, Videoplayer); Sprache im Kopf.</summary>
    public static string ToVtt(IReadOnlyList<Cue> cues, string lang = "de", double length = 0)
    {
        Check(cues, length);
        var sb = new StringBuilder($"WEBVTT\nLanguage: {lang}\n\n");
        for (var i = 0; i < cues.Count; i++) sb.Append($"{i + 1}\n{T(cues[i].Start, '.')} --> {T(cues[i].End, '.')}\n{cues[i].Text.Trim().Replace("-->", "→")}\n\n");
        return sb.ToString();
    }

    /// <summary>Sprachfassung: übersetzte Texte auf die Zeiten des Originals legen. Gleiche Anzahl Zeilen ist Pflicht – sonst laufen Bild und Text auseinander.</summary>
    public static List<Cue> Translate(IReadOnlyList<Cue> original, IReadOnlyList<string> texts)
    {
        if (texts.Count != original.Count) throw new ArgumentException($"Die Übersetzung hat {texts.Count} Zeilen, das Original {original.Count} – so wären die Untertitel nicht synchron.");
        return [.. original.Select((c, i) => c with { Text = texts[i].Trim() })];
    }

    /// <summary>Synchron heißt: gleiche Anzahl und exakt gleiche Zeiten (auf die Millisekunde).</summary>
    public static bool InSync(IReadOnlyList<Cue> a, IReadOnlyList<Cue> b) =>
        a.Count == b.Count && a.Zip(b).All(x => Math.Abs(x.First.Start - x.Second.Start) < 0.0005 && Math.Abs(x.First.End - x.Second.End) < 0.0005);

    /// <summary>LRC für Karaoke-Player: [mm:ss.xx] je Zeile, Kopf mit Titel, Sprache und Länge.</summary>
    public static string ToLrc(IReadOnlyList<Cue> lines, string title, string lang = "de", double length = 0)
    {
        Check(lines, length);
        string L(double s) { var cs = (long)Math.Round(s * 100, MidpointRounding.AwayFromZero); return $"[{cs / 6000:00}:{cs / 100 % 60:00}.{cs % 100:00}]"; }
        var sb = new StringBuilder($"[ti:{title.Replace("]", ")")}]\n[la:{lang}]\n");
        if (length > 0) sb.Append($"[length:{(int)(length / 60):00}:{(int)(length % 60):00}]\n");
        foreach (var c in lines) sb.Append($"{L(c.Start)}{c.Text.Trim()}\n");
        if (lines.Count > 0) sb.Append($"{L(lines[^1].End)}\n");
        return sb.ToString();
    }

    /// <summary>LRC lesen (für die Prüfung): Zeiten und Text je Zeile.</summary>
    public static List<(double Time, string Text)> ReadLrc(string lrc)
    {
        var r = new List<(double, string)>();
        foreach (var raw in lrc.Replace("\r\n", "\n").Split('\n'))
        {
            var m = System.Text.RegularExpressions.Regex.Match(raw, @"^\[(\d{2}):(\d{2})\.(\d{2})\](.*)$");
            if (m.Success) r.Add((int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) / 100.0, m.Groups[4].Value));
        }
        return r;
    }
}
