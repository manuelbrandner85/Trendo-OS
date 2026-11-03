using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>Trendo Musik V6 (freigegeben 03.11.2026): Liedtext im Musikvideo. Die Zeilen des (eigenen bzw. erzeugten) Liedtexts werden den
/// gesungenen Phrasen der Gesangsspur zugeordnet – Phrase = zusammenhängender Gesang, getrennt durch Pausen ab 0,35 s. Das ist eine Schätzung
/// aus dem Klang, kein Wort-für-Wort-Abgleich. Ausgabe als ASS-Untertitel (sanft ein- und ausgeblendet).</summary>
public static class LyricTiming
{
    public sealed record Line(double Start, double End, string Text);

    /// <summary>Gesungene Phrasen aus der Zielmelodie (siehe <see cref="SingAlong.Melody"/>).</summary>
    public static List<(double Start, double End)> Phrases(SingAlong.Melody m, double gap = 0.35, double minLen = 0.6)
    {
        var res = new List<(double, double)>(); double? start = null; double last = 0;
        gap = Math.Max(gap, 1.5 * m.Hop); // eine Pause ist mindestens anderthalb Schritte lang
        for (var i = 0; i < m.Midi.Length; i++)
        {
            var t = i * m.Hop;
            if (m.Midi[i] is null) continue;
            if (start is null) start = t;
            else if (t - last > gap) { if (last + m.Hop - start.Value >= minLen) res.Add((start.Value, last + m.Hop)); start = t; }
            last = t;
        }
        if (start is not null && last + m.Hop - start.Value >= minLen) res.Add((start.Value, last + m.Hop));
        return res;
    }

    /// <summary>Singbare Zeilen des Liedtexts (ohne Abschnitts-Marken und Leerzeilen).</summary>
    public static List<string> Lines(string lyrics) =>
        [.. lyrics.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !(l.StartsWith('[') && l.EndsWith(']')))];

    /// <summary>Zeilen den Phrasen zuordnen: gleich viele = eins zu eins; mehr Phrasen = kurze Phrasen verbinden; mehr Zeilen = lange Phrasen teilen.</summary>
    public static List<Line> Align(IReadOnlyList<string> lines, List<(double Start, double End)> phrases)
    {
        if (lines.Count == 0 || phrases.Count == 0) return [];
        var ph = phrases.ToList();
        while (ph.Count > lines.Count) // die kürzeste Lücke schließen
        {
            var best = 0; var bestGap = double.MaxValue;
            for (var i = 0; i < ph.Count - 1; i++) { var g = ph[i + 1].Start - ph[i].End; if (g < bestGap) { bestGap = g; best = i; } }
            ph[best] = (ph[best].Start, ph[best + 1].End); ph.RemoveAt(best + 1);
        }
        while (ph.Count < lines.Count) // die längste Phrase halbieren
        {
            var i = Enumerable.Range(0, ph.Count).MaxBy(k => ph[k].End - ph[k].Start);
            var mid = (ph[i].Start + ph[i].End) / 2;
            ph.Insert(i + 1, (mid, ph[i].End)); ph[i] = (ph[i].Start, mid);
        }
        return [.. lines.Select((l, i) => new Line(Math.Round(ph[i].Start, 2), Math.Round(ph[i].End + 0.25, 2), l))];
    }

    static string T(double s) { var ts = TimeSpan.FromSeconds(Math.Max(0, s)); return $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}"; }

    /// <summary>ASS-Untertitel: unten mittig (Breitbild) bzw. im unteren Drittel (Hochformat), klare Schrift mit weichem Schatten, Ein-/Ausblenden.</summary>
    public static string Ass(IEnumerable<Line> lines, int w, int h, string font = "Segoe UI")
    {
        var size = (int)Math.Round(Math.Min(w, h) * (w > h ? 0.055 : 0.06));
        var margin = (int)Math.Round(h * (w > h ? 0.16 : 0.22));
        var sb = new StringBuilder();
        sb.Append($"[Script Info]\nScriptType: v4.00+\nPlayResX: {w}\nPlayResY: {h}\nWrapStyle: 0\n\n[V4+ Styles]\n");
        sb.Append("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n");
        sb.Append(CultureInfo.InvariantCulture, $"Style: Lied,{font},{size},&H00FFFFFF,&H00FFFFFF,&H64000000,&H96000000,0,0,0,0,100,100,1,0,1,1.5,2,2,{w / 12},{w / 12},{margin},1\n\n");
        sb.Append("[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n");
        foreach (var l in lines) sb.Append($"Dialogue: 0,{T(l.Start)},{T(l.End)},Lied,,0,0,0,,{{\\fad(180,220)}}{l.Text.Replace("{", "(").Replace("}", ")")}\n");
        return sb.ToString();
    }
}
