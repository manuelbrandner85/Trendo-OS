using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record OnAir(int Index, string Title, TimeSpan LeftInSegment, TimeSpan LeftInShow, string? Next);
public sealed record ShowSlot(DateTime Start, DateTime End, string Title);

/// <summary>SPEC-15 Ausbau: Live-Anzeige, Untertitel, Sprechzeit, Lautheit, Wochenplan, Bildformate, Moderationskarten, Timecode.</summary>
public static partial class BroadcastPlus
{
    /// <summary>F12: Was läuft zur Zeit <paramref name="now"/>? null = vor oder nach der Sendung.</summary>
    public static OnAir? NowPlaying(BroadcastDesk d, DateTime now)
    {
        var rows = d.Rows(); var cur = rows.FirstOrDefault(r => now >= r.Start && now < r.End); if (cur is null) return null;
        return new(cur.Index, cur.Title, cur.End - now, rows[^1].End - now, cur.Index + 1 < rows.Count ? rows[cur.Index + 1].Title : null);
    }

    [GeneratedRegex(@"^(\d{1,2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{1,2}):(\d{2}):(\d{2})[,.](\d{3})")] private static partial Regex SrtTime();
    static TimeSpan Ts(Match m, int o) => new(0, int.Parse(m.Groups[o].Value), int.Parse(m.Groups[o + 1].Value), int.Parse(m.Groups[o + 2].Value), int.Parse(m.Groups[o + 3].Value));
    static string Srt(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";

    /// <summary>F13: SRT um <paramref name="shift"/> versetzen, neu nummerieren und prüfen (Überlappung, Ende vor Anfang, negative Zeiten).</summary>
    public static (string Srt, int Cues, List<string> Problems) ShiftSrt(string srt, TimeSpan shift)
    {
        var blocks = Regex.Split(srt.Replace("\r", "").Trim(), @"\n\s*\n"); var sb = new StringBuilder(); var probs = new List<string>(); var n = 0; TimeSpan prevEnd = TimeSpan.MinValue;
        foreach (var b in blocks)
        {
            var lines = b.Split('\n').ToList(); var ti = lines.FindIndex(l => SrtTime().IsMatch(l.Trim())); if (ti < 0) continue;
            var m = SrtTime().Match(lines[ti].Trim()); var a = Ts(m, 1) + shift; var e = Ts(m, 5) + shift; n++;
            if (a < TimeSpan.Zero) { probs.Add($"Untertitel {n} läge vor 0:00 – auf 0 gesetzt."); a = TimeSpan.Zero; if (e < a) e = a; }
            if (e <= a) probs.Add($"Untertitel {n}: Ende liegt nicht nach dem Anfang.");
            if (a < prevEnd) probs.Add($"Untertitel {n} überlappt mit dem vorherigen.");
            prevEnd = e;
            sb.Append($"{n}\n{Srt(a)} --> {Srt(e)}\n{string.Join("\n", lines.Skip(ti + 1))}\n\n");
        }
        if (n == 0) throw new InvalidOperationException("Das ist keine SRT-Datei – keine Zeitangaben gefunden.");
        return (sb.ToString(), n, probs);
    }

    /// <summary>F14: Sprechzeit bei <paramref name="wpm"/> Wörtern pro Minute (ruhig ≈ 130).</summary>
    public static (int Words, TimeSpan Time) SpeakingTime(string text, double wpm = 130)
    {
        if (wpm <= 0) throw new ArgumentException("Wörter pro Minute müssen größer als 0 sein.");
        var w = Regex.Matches(text, @"[\p{L}\p{N}]+(?:[-'’][\p{L}\p{N}]+)*").Count; return (w, TimeSpan.FromSeconds(Math.Round(w / wpm * 60)));
    }

    public static readonly IReadOnlyDictionary<string, (double Lufs, double Peak)> LoudTargets = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
    { ["YouTube"] = (-14, -1), ["Spotify"] = (-14, -1), ["Apple Podcasts"] = (-16, -1), ["Podcast"] = (-16, -1), ["Twitch"] = (-14, -1), ["Fernsehen (EBU R128)"] = (-23, -1), ["Radio"] = (-23, -1) };

    /// <summary>F15: Verstärkung bis zum Ziel und Spitzenpegel danach.</summary>
    public static (double Target, double Gain, double PeakAfter, bool Clips) Loudness(string platform, double measuredLufs, double truePeak)
    {
        var key = LoudTargets.Keys.FirstOrDefault(k => k.StartsWith(platform.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Plattformen: " + string.Join(", ", LoudTargets.Keys));
        var (t, p) = LoudTargets[key]; var g = Math.Round(t - measuredLufs, 1); var after = Math.Round(truePeak + g, 1); return (t, g, after, after > p);
    }

    static readonly string[] Days = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
    /// <summary>F16: Wochenplan aus Zeilen „Mo 20:00 60 Titel“ für <paramref name="weeks"/> Wochen ab <paramref name="from"/>; meldet Überschneidungen.</summary>
    public static (List<ShowSlot> Slots, List<string> Conflicts) Weekly(string spec, DateTime from, int weeks)
    {
        var rules = new List<(int Day, TimeSpan At, int Min, string Title)>();
        foreach (var raw in spec.Replace("\r", "").Split('\n'))
        {
            var l = raw.Trim(); if (l == "") continue;
            var m = Regex.Match(l, @"^(\w\w)\w*\s+(\d{1,2}):(\d{2})\s+(\d+)\s+(.+)$"); var di = m.Success ? Array.FindIndex(Days, d => d.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) : -1;
            if (di < 0) throw new FormatException($"Zeile „{l}“: bitte so – „Mo 20:00 60 Titel“.");
            rules.Add((di, new(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), 0), int.Parse(m.Groups[4].Value), m.Groups[5].Value.Trim()));
        }
        var slots = new List<ShowSlot>(); var start = from.Date;
        for (var i = 0; i < weeks * 7; i++) { var d = start.AddDays(i); var wd = ((int)d.DayOfWeek + 6) % 7; foreach (var r in rules.Where(r => r.Day == wd)) slots.Add(new(d + r.At, d + r.At + TimeSpan.FromMinutes(r.Min), r.Title)); }
        slots.Sort((a, b) => a.Start.CompareTo(b.Start));
        var c = new List<string>(); for (var i = 1; i < slots.Count; i++) if (slots[i].Start < slots[i - 1].End) c.Add($"{slots[i].Start:ddd dd.MM. HH:mm}: „{slots[i].Title}“ überschneidet sich mit „{slots[i - 1].Title}“.");
        return (slots, c);
    }

    static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
    /// <summary>F17: Seitenverhältnis und Name.</summary>
    public static (int W, int H, string Name) Aspect(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Breite und Höhe müssen größer als 0 sein.");
        var g = Gcd(width, height); int w = width / g, h = height / g; var r = (double)width / height;
        var name = (w, h) switch { (16, 9) => "Querformat (YouTube, TV)", (9, 16) => "Hochkant (Shorts, Reels, TikTok)", (1, 1) => "Quadrat", (4, 3) => "klassisch 4:3", (4, 5) => "Hochformat (Instagram-Feed)", _ => Math.Abs(r - 2.39) < 0.02 ? "Kino-Breitbild" : Math.Abs(r - 21 / 9.0) < 0.02 ? "Ultrabreit 21:9" : "unüblich" };
        return (w, h, name);
    }

    /// <summary>F18: Ausschnitt mit Zielverhältnis aus einem Bild; <paramref name="centerPct"/> = horizontale Lage (0 = links, 50 = Mitte, 100 = rechts).</summary>
    public static (int W, int H, int X, int Y) Crop(int width, int height, int aw = 9, int ah = 16, double centerPct = 50)
    {
        if (width <= 0 || height <= 0 || aw <= 0 || ah <= 0) throw new ArgumentException("Alle Werte müssen größer als 0 sein.");
        int w, h; if ((double)width / height > (double)aw / ah) { h = height; w = (int)(height * aw / (double)ah) / 2 * 2; } else { w = width; h = (int)(width * ah / (double)aw) / 2 * 2; }
        var cp = Math.Clamp(centerPct, 0, 100) / 100; return (w, h, (int)Math.Round((width - w) * cp), (int)Math.Round((height - h) / 2.0));
    }

    /// <summary>F19: Moderationskarten als Markdown, eine Karte pro Programmpunkt.</summary>
    public static string CueCards(BroadcastDesk d)
    {
        var rows = d.Rows(); if (rows.Count == 0) throw new InvalidOperationException("Der Ablauf ist noch leer.");
        var sb = new StringBuilder($"# {d.Plan.Show} – Moderationskarten\n\n");
        foreach (var r in rows) sb.Append($"---\n\n## {r.Index + 1}. {r.Title}\n\n**{r.Start:HH:mm:ss} – {r.End:HH:mm:ss}** · Dauer {BroadcastDesk.Clock(TimeSpan.FromSeconds(r.Seconds))}\n\n{(r.Note == "" ? "_(keine Notiz)_" : r.Note)}\n\n" + (r.Index + 1 < rows.Count ? $"➜ Danach: {rows[r.Index + 1].Title}\n\n" : "➜ Ende der Sendung\n\n"));
        return sb.ToString();
    }

    /// <summary>F20: Timecode hh:mm:ss:ff (ganzzahlige Bildrate, ohne Drop-Frame).</summary>
    public static string ToTimecode(long frames, int fps)
    {
        if (fps <= 0 || frames < 0) throw new ArgumentException("Bildrate > 0 und Frames ≥ 0.");
        var s = frames / fps; return $"{s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}:{frames % fps:00}";
    }
    public static long FromTimecode(string tc, int fps)
    {
        var p = tc.Trim().Split(':', ';'); if (p.Length != 4 || p.Any(x => !int.TryParse(x, out _))) throw new FormatException("Timecode bitte als hh:mm:ss:ff.");
        var v = p.Select(int.Parse).ToArray(); if (v[1] > 59 || v[2] > 59 || v[3] >= fps) throw new FormatException("Minuten/Sekunden bis 59, Frames kleiner als die Bildrate.");
        return ((v[0] * 3600L + v[1] * 60 + v[2]) * fps) + v[3];
    }
}
