using System.Text;

namespace Trendo.Core;

public sealed class Segment { public string Title { get; set; } = ""; public int Seconds { get; set; } public string Note { get; set; } = ""; }
public sealed class Rundown { public string Show { get; set; } = "Meine Sendung"; public DateTime Start { get; set; } = DateTime.Today.AddHours(20); public int SlotMinutes { get; set; } = 60; public List<Segment> Segments { get; set; } = []; }
public sealed record RundownRow(int Index, string Title, DateTime Start, DateTime End, TimeSpan Offset, int Seconds, string Note);
public sealed record StreamPlan(string Platform, int VideoKbps, int AudioKbps, double NeededUploadMbps, bool Fits, string Advice);

/// <summary>SPEC-15: Sendung & Streaming – Sendeablauf mit Zeiten und Kapitelmarken, Bitraten-Planer, Speicherbedarf.</summary>
public sealed class BroadcastDesk(string dataRoot)
{
    public Rundown Plan { get; } = Fach.Load<Rundown>(dataRoot, "sendung", "ablauf");
    public void Save() => Fach.Save(dataRoot, "sendung", "ablauf", Plan);

    /// <summary>Dauer als „90“, „1:30“ oder „1:02:00“ → Sekunden.</summary>
    public static int ParseDuration(string s)
    {
        var p = s.Trim().Split(':'); if (p.Length > 3 || p.Any(x => !int.TryParse(x, out var n) || n < 0)) throw new FormatException("Dauer bitte als Sekunden, „m:ss“ oder „h:mm:ss“.");
        var v = p.Select(int.Parse).Aggregate(0, (a, b) => a * 60 + b); return v > 0 ? v : throw new FormatException("Die Dauer muss größer als 0 sein.");
    }

    public static string Clock(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";

    public void Add(string title, int seconds, string note = "", int? at = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Bitte einen Titel für den Programmpunkt angeben.");
        var s = new Segment { Title = title.Trim(), Seconds = seconds, Note = note.Trim() };
        if (at is { } i && i >= 0 && i <= Plan.Segments.Count) Plan.Segments.Insert(i, s); else Plan.Segments.Add(s); Save();
    }
    public void Remove(int i) { if (i >= 0 && i < Plan.Segments.Count) { Plan.Segments.RemoveAt(i); Save(); } }
    public void Move(int i, int delta) { var j = i + delta; if (i < 0 || j < 0 || i >= Plan.Segments.Count || j >= Plan.Segments.Count) return; (Plan.Segments[i], Plan.Segments[j]) = (Plan.Segments[j], Plan.Segments[i]); Save(); }

    public IReadOnlyList<RundownRow> Rows()
    {
        var t = Plan.Start; var off = TimeSpan.Zero; var res = new List<RundownRow>();
        for (var i = 0; i < Plan.Segments.Count; i++) { var s = Plan.Segments[i]; res.Add(new(i, s.Title, t, t.AddSeconds(s.Seconds), off, s.Seconds, s.Note)); t = t.AddSeconds(s.Seconds); off += TimeSpan.FromSeconds(s.Seconds); }
        return res;
    }

    public TimeSpan Total => TimeSpan.FromSeconds(Plan.Segments.Sum(s => s.Seconds));
    public TimeSpan Over => Total - TimeSpan.FromMinutes(Plan.SlotMinutes);

    /// <summary>Kapitelmarken für YouTube (beginnt bei 0:00, mind. 3 Kapitel à 10 s nötig).</summary>
    public string Chapters()
    {
        var rows = Rows(); if (rows.Count < 3) throw new InvalidOperationException("YouTube braucht mindestens 3 Programmpunkte für Kapitel.");
        if (rows.Any(r => r.Seconds < 10)) throw new InvalidOperationException("Jedes Kapitel muss mindestens 10 Sekunden lang sein.");
        return string.Join("\n", rows.Select(r => $"{Clock(r.Offset)} {r.Title}"));
    }

    public string Sheet()
    {
        var sb = new StringBuilder($"{Plan.Show} – Sendeablauf {Plan.Start:dd.MM.yyyy}\nNr;Beginn;Ende;Dauer;Programmpunkt;Hinweis\n");
        foreach (var r in Rows()) sb.Append($"{r.Index + 1};{r.Start:HH:mm:ss};{r.End:HH:mm:ss};{Clock(TimeSpan.FromSeconds(r.Seconds))};{r.Title};{r.Note}\n");
        sb.Append($";;;{Clock(Total)};Gesamt (Sendeplatz {Plan.SlotMinutes} min);\n"); return sb.ToString();
    }

    // Empfehlungen (Video kbit/s) nach Plattform-Hilfeseiten, Stand 2026
    private static readonly Dictionary<string, Dictionary<string, (int Min, int Max)>> Rates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["YouTube"] = new() { ["720p30"] = (1500, 4000), ["720p60"] = (2250, 6000), ["1080p30"] = (3000, 6000), ["1080p60"] = (4500, 9000), ["1440p30"] = (6000, 13000), ["1440p60"] = (9000, 18000), ["2160p30"] = (13000, 34000), ["2160p60"] = (20000, 51000) },
        ["Twitch"] = new() { ["720p30"] = (2500, 4000), ["720p60"] = (3500, 5000), ["1080p30"] = (4500, 6000), ["1080p60"] = (6000, 6000) },
        ["Facebook"] = new() { ["720p30"] = (3000, 4000), ["720p60"] = (3000, 4000), ["1080p30"] = (4000, 6000), ["1080p60"] = (4000, 8000) },
    };

    public static StreamPlan PlanStream(string platform, string quality, double uploadMbps)
    {
        if (!Rates.TryGetValue(platform.Trim(), out var p)) throw new ArgumentException("Plattformen: " + string.Join(", ", Rates.Keys));
        var q = quality.Trim().ToLowerInvariant().Replace(" ", "");
        if (!p.TryGetValue(q, out var r)) throw new ArgumentException($"{platform}: möglich sind {string.Join(", ", p.Keys)}.");
        var audio = 160; var video = (r.Min + r.Max) / 2; var need = (video + audio) / 1000.0 * 1.5; // 50 % Reserve
        var fits = uploadMbps >= need;
        string advice;
        if (fits) advice = "Passt. Im Encoder „CBR“ einstellen, Keyframe alle 2 Sekunden.";
        else
        {
            var lower = p.Where(kv => (((kv.Value.Min + kv.Value.Max) / 2) + audio) / 1000.0 * 1.5 <= uploadMbps).OrderByDescending(kv => kv.Value.Min + kv.Value.Max).FirstOrDefault().Key;
            advice = lower is null ? "Die Leitung reicht für keinen Livestream – per Kabel statt WLAN versuchen oder vorab aufnehmen." : $"Zu wenig Upload. Nimm besser {lower}.";
        }
        return new(platform, video, audio, Math.Round(need, 1), fits, advice);
    }

    /// <summary>Speicherbedarf einer Aufnahme in GB.</summary>
    public static double StorageGb(double kbps, double minutes) => Math.Round(kbps * 1000 / 8 * minutes * 60 / 1e9, 2);
}
