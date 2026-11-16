using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core.Kreativ;
using Trendo.Core.Sys;

namespace Trendo.Core.Business;

/// <summary>STUDIO-SOCIAL-01: Bildformat eines Netzwerks (Pixel).</summary>
public sealed record SocialFormat(string Id, string Name, int W, int H);

/// <summary>Ein Netzwerk mit Zeichengrenze, Hashtag-Grenze und Bildformaten. Werte aus öffentlichen Übersichten (Stand 11/2026) – Netzwerke ändern sie gelegentlich.</summary>
public sealed record SocialNetwork(string Id, string Name, int MaxChars, int MaxHashtags, int MaxImages, bool NeedsMedia, bool UrlShortened, string Note, IReadOnlyList<SocialFormat> Formats);

/// <summary>Ein geplanter Beitrag: gemeinsamer Text, Hashtags, Netzwerke, Bild und je Netzwerk ein eigener Text (optional).</summary>
public sealed record SocialPost(string Id, DateTime When, string Text, IReadOnlyList<string> Hashtags, IReadOnlyList<string> Networks, string Image = "", string Status = SocialPlan.Planned,
    IReadOnlyDictionary<string, string>? PerNetwork = null);

/// <summary>Eine Feststellung der Prüfung (Fehler = so nicht posten, sonst Hinweis).</summary>
public sealed record SocialFinding(string Network, string Text, bool Error);

/// <summary>Redaktionskalender wie in Buffer/Later/Metricool (Clean-Room): Netzwerke-Tabelle, Prüfung, Woche/Monat, Warteschlange mit festen Zeiten,
/// Bildzuschnitt, CSV/ICS und Bildpaket. Trendo veröffentlicht nichts – nur Plan, Kopieren und Erinnerung.</summary>
public static partial class SocialPlan
{
    public const string Planned = "geplant", Draft = "entwurf", Posted = "gepostet";
    public static readonly string[] Statuses = [Draft, Planned, Posted];

    public static readonly IReadOnlyList<SocialNetwork> Networks =
    [
        new("instagram", "Instagram", 2200, 5, 20, true, false, "Ohne Bild oder Video geht kein Beitrag. Links im Text sind nicht anklickbar. Höchstens 5 Hashtags.",
            [new("hoch", "Hochformat 4:5", 1080, 1350), new("quadrat", "Quadrat 1:1", 1080, 1080), new("story", "Story/Reel 9:16", 1080, 1920)]),
        new("facebook", "Facebook", 63206, 30, 10, false, false, "Lange Texte sind möglich, sichtbar sind die ersten etwa 125 Zeichen.",
            [new("quer", "Querformat 1,91:1", 1200, 630), new("hoch", "Hochformat 4:5", 1080, 1350), new("story", "Story 9:16", 1080, 1920)]),
        new("linkedin", "LinkedIn", 3000, 5, 9, false, false, "Nach etwa 210 Zeichen kommt „mehr anzeigen“ – das Wichtigste nach vorn.",
            [new("quer", "Querformat 1,91:1", 1200, 627), new("quadrat", "Quadrat 1:1", 1080, 1080), new("hoch", "Hochformat 4:5", 1080, 1350)]),
        new("x", "X", 280, 3, 4, false, true, "Ohne Abo 280 Zeichen; ein Link zählt immer 23 Zeichen.",
            [new("quer", "Querformat 16:9", 1600, 900), new("quadrat", "Quadrat 1:1", 1080, 1080)]),
        new("tiktok", "TikTok", 4000, 5, 35, true, false, "Nur mit Video oder Fotoserie. Hochformat füllt den Bildschirm.",
            [new("hoch", "Hochformat 9:16", 1080, 1920)]),
        new("pinterest", "Pinterest", 500, 5, 1, true, false, "Ein Bild je Pin, am besten 2:3. Beschreibung höchstens 500 Zeichen.",
            [new("pin", "Pin 2:3", 1000, 1500), new("quadrat", "Quadrat 1:1", 1000, 1000)]),
        new("mastodon", "Mastodon", 500, 10, 4, false, true, "Standard 500 Zeichen (je Server verschieden); ein Link zählt 23 Zeichen. Bildbeschreibung (Alt-Text) wird erwartet.",
            [new("quer", "Querformat 16:9", 1280, 720), new("quadrat", "Quadrat 1:1", 1080, 1080)]),
    ];

    public static SocialNetwork Network(string id) => Networks.FirstOrDefault(n => n.Id == id) ?? throw new ArgumentException("Unbekanntes Netzwerk: " + id);

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)] private static partial Regex UrlRx();
    [GeneratedRegex(@"[^\p{L}\p{N}_]")] private static partial Regex TagCharRx();
    [GeneratedRegex(@"(?<!\S)#[\p{L}\p{N}_]+")] private static partial Regex InlineTagRx();

    /// <summary>Hashtags aus einer Eingabe wie „#Bäckerei, frischesBrot Sonntag“: mit #, ohne Sonderzeichen, ohne Doppelte (Groß/klein egal).</summary>
    public static List<string> Tags(string raw)
    {
        var res = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (raw ?? "").Split([' ', ',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = TagCharRx().Replace(part.TrimStart('#'), "");
            if (t.Length == 0 || t.All(char.IsDigit)) continue;
            if (seen.Add(t)) res.Add("#" + t);
        }
        return res;
    }

    /// <summary>Text für ein Netzwerk: eigener Text des Netzwerks oder der gemeinsame Text, dahinter die Hashtags (höchstens so viele, wie das Netzwerk erlaubt).</summary>
    public static string Compose(SocialPost p, string network)
    {
        var n = Network(network);
        var text = p.PerNetwork is not null && p.PerNetwork.TryGetValue(network, out var own) && own.Trim().Length > 0 ? own.Trim() : p.Text.Trim();
        var inText = InlineTagRx().Matches(text).Select(m => m.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tags = p.Hashtags.Where(t => !inText.Contains(t)).Take(Math.Max(0, n.MaxHashtags - inText.Count)).ToList();
        return tags.Count == 0 ? text : text + "\n\n" + string.Join(" ", tags);
    }

    /// <summary>Zeichen so gezählt wie das Netzwerk: Schriftzeichen (Emoji = 1), bei X und Mastodon jeder Link 23.</summary>
    public static int Length(string text, string network)
    {
        var n = Network(network); var t = text ?? "";
        if (!n.UrlShortened) return new StringInfo(t).LengthInTextElements;
        var urls = UrlRx().Matches(t).Count;
        return new StringInfo(UrlRx().Replace(t, "")).LengthInTextElements + urls * 23;
    }

    public static int HashtagCount(string text) => InlineTagRx().Matches(text ?? "").Count;

    /// <summary>Prüft einen Beitrag für jedes gewählte Netzwerk.</summary>
    public static List<SocialFinding> Check(SocialPost p)
    {
        var f = new List<SocialFinding>();
        if (p.Networks.Count == 0) f.Add(new("", "Kein Netzwerk gewählt.", true));
        if (p.Text.Trim().Length == 0 && (p.PerNetwork is null || p.PerNetwork.Values.All(v => v.Trim().Length == 0))) f.Add(new("", "Der Beitrag hat noch keinen Text.", true));
        foreach (var id in p.Networks)
        {
            var n = Network(id); var t = Compose(p, id); var len = Length(t, id); var tags = HashtagCount(t);
            if (len > n.MaxChars) f.Add(new(n.Name, $"{len} von {n.MaxChars} Zeichen – {len - n.MaxChars} zu viel.", true));
            var own = p.PerNetwork is not null && p.PerNetwork.TryGetValue(id, out var o) && o.Trim().Length > 0 ? o : p.Text;
            var wanted = InlineTagRx().Matches(own).Select(m => m.Value).Concat(p.Hashtags).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (wanted > n.MaxHashtags) f.Add(new(n.Name, $"Nur {n.MaxHashtags} Hashtags passen – {wanted - Math.Min(tags, wanted)} werden weggelassen.", tags > n.MaxHashtags));
            if (n.NeedsMedia && p.Image.Trim().Length == 0) f.Add(new(n.Name, "Braucht ein Bild oder Video.", true));
            if (id == "instagram" && UrlRx().IsMatch(t)) f.Add(new(n.Name, "Links im Text sind bei Instagram nicht anklickbar – „Link in der Beschreibung“ schreiben.", false));
        }
        return f;
    }

    // ---------- Kalender ----------

    public static DateTime WeekStart(DateTime day) { var d = day.Date; var diff = ((int)d.DayOfWeek + 6) % 7; return d.AddDays(-diff); }

    /// <summary>Monatsansicht: 6 Wochen ab dem Montag vor dem Monatsersten (wie ein Wandkalender).</summary>
    public static List<DateTime> MonthGrid(DateTime anyDay)
    {
        var first = new DateTime(anyDay.Year, anyDay.Month, 1); var start = WeekStart(first);
        return [.. Enumerable.Range(0, 42).Select(i => start.AddDays(i))];
    }

    public static List<SocialPost> On(IEnumerable<SocialPost> posts, DateTime day) => [.. posts.Where(p => p.When.Date == day.Date).OrderBy(p => p.When)];

    /// <summary>Standard-Zeiten der Warteschlange (wie in Buffer frei änderbar): Mo–Fr 9:00 und 18:00, Sa 11:00.</summary>
    public static readonly IReadOnlyList<(DayOfWeek Day, TimeSpan Time)> DefaultSlots =
    [
        .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }.SelectMany(d => new[] { (d, new TimeSpan(9, 0, 0)), (d, new TimeSpan(18, 0, 0)) }),
        (DayOfWeek.Saturday, new TimeSpan(11, 0, 0)),
    ];

    /// <summary>Nächste freie Zeit der Warteschlange nach <paramref name="after"/>, an der noch kein Beitrag steht (höchstens 8 Wochen voraus).</summary>
    public static DateTime NextSlot(IEnumerable<SocialPost> posts, IReadOnlyList<(DayOfWeek Day, TimeSpan Time)> slots, DateTime after)
    {
        if (slots.Count == 0) throw new ArgumentException("Es sind keine festen Zeiten eingestellt.");
        var taken = posts.Select(p => p.When).ToHashSet();
        for (var day = after.Date; day < after.Date.AddDays(56); day = day.AddDays(1))
            foreach (var s in slots.Where(s => s.Day == day.DayOfWeek).OrderBy(s => s.Time))
            {
                var t = day + s.Time;
                if (t > after && !taken.Contains(t)) return t;
            }
        throw new InvalidOperationException("In den nächsten acht Wochen ist keine feste Zeit mehr frei.");
    }

    /// <summary>Feste Zeiten aus Text wie „Mo 09:00, Mi 18:30, Sa 11:00“.</summary>
    public static List<(DayOfWeek Day, TimeSpan Time)> ParseSlots(string raw)
    {
        var map = new Dictionary<string, DayOfWeek> { ["mo"] = DayOfWeek.Monday, ["di"] = DayOfWeek.Tuesday, ["mi"] = DayOfWeek.Wednesday, ["do"] = DayOfWeek.Thursday, ["fr"] = DayOfWeek.Friday, ["sa"] = DayOfWeek.Saturday, ["so"] = DayOfWeek.Sunday };
        var res = new List<(DayOfWeek, TimeSpan)>();
        foreach (var part in (raw ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = Regex.Match(part, @"^(mo|di|mi|do|fr|sa|so)\w*\s+(\d{1,2})[:.](\d{2})$", RegexOptions.IgnoreCase);
            if (!m.Success) throw new FormatException($"„{part}“ verstehe ich nicht – schreib z. B. „Mo 09:00“.");
            var h = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); var mi = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (h > 23 || mi > 59) throw new FormatException($"„{part}“ ist keine Uhrzeit.");
            res.Add((map[m.Groups[1].Value.ToLowerInvariant()], new TimeSpan(h, mi, 0)));
        }
        return [.. res.Distinct().OrderBy(s => ((int)s.Item1 + 6) % 7).ThenBy(s => s.Item2)];
    }

    // ---------- Export ----------

    static string Csv(string s) => s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    /// <summary>CSV mit einer Zeile je Beitrag und Netzwerk (Datum, Uhrzeit, Netzwerk, Text, Bild, Status) – zum Hochladen in einen Planungsdienst oder für eine Tabelle.</summary>
    public static string ToCsv(IEnumerable<SocialPost> posts)
    {
        var sb = new StringBuilder("Datum,Uhrzeit,Netzwerk,Text,Zeichen,Bild,Status\r\n");
        foreach (var p in posts.OrderBy(p => p.When))
            foreach (var id in p.Networks)
            {
                var t = Compose(p, id);
                sb.Append(p.When.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',').Append(p.When.ToString("HH:mm", CultureInfo.InvariantCulture)).Append(',')
                  .Append(Network(id).Name).Append(',').Append(Csv(t)).Append(',').Append(Length(t, id).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Csv(Path.GetFileName(p.Image))).Append(',').Append(p.Status).Append("\r\n");
            }
        return sb.ToString();
    }

    /// <summary>Kalenderdatei: je Beitrag ein Termin (15 min) mit Erinnerung 10 min vorher; Text steht in der Beschreibung zum Kopieren.</summary>
    public static string ToIcs(IEnumerable<SocialPost> posts) =>
        Ics.Export(posts.Where(p => p.Status != Posted).OrderBy(p => p.When).Select(p => new CalEvent("social-" + p.Id + "@trendo", p.When, p.When.AddMinutes(15),
            "Beitrag posten: " + string.Join(", ", p.Networks.Select(n => Network(n).Name)), "",
            string.Join("\n\n", p.Networks.Select(n => Network(n).Name + ":\n" + Compose(p, n))), RemindMin: 10)));

    /// <summary>Bild füllend auf ein Format zuschneiden (nicht verzerren). <paramref name="fx"/>/<paramref name="fy"/>: Bildmitte des Ausschnitts (0–1), z. B. für Gesichter oben.</summary>
    public static Pixmap Crop(Pixmap src, int w, int h, double fx = 0.5, double fy = 0.5)
    {
        if (w <= 0 || h <= 0) throw new ArgumentException("Ungültiges Format.");
        var s = Math.Max((double)w / src.W, (double)h / src.H);
        var sw = Math.Max(w, (int)Math.Round(src.W * s)); var sh = Math.Max(h, (int)Math.Round(src.H * s));
        var scaled = src.Resize(sw, sh);
        var ox = (int)Math.Round(Math.Clamp(fx, 0, 1) * (sw - w)); var oy = (int)Math.Round(Math.Clamp(fy, 0, 1) * (sh - h));
        var res = new Pixmap(w, h);
        for (var y = 0; y < h; y++) Buffer.BlockCopy(scaled.Px, ((y + oy) * sw + ox) * 4, res.Px, y * w * 4, w * 4);
        return res;
    }

    /// <summary>Wie viele Prozent des Bildes der Zuschnitt verliert (zum Warnen bei starkem Beschnitt).</summary>
    public static double CropLoss(int srcW, int srcH, int w, int h)
    {
        var s = Math.Max((double)w / srcW, (double)h / srcH);
        return Math.Round(100 * (1 - (double)w * h / (srcW * s * srcH * s)), 1);
    }

    /// <summary>Bildpaket als ZIP: je Beitrag und Netzwerk das zugeschnittene Bild im ersten Format des Netzwerks und eine Textdatei zum Kopieren.</summary>
    public static int Package(IEnumerable<SocialPost> posts, Func<string, Pixmap?> load, string zipFile)
    {
        var n = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipFile))!);
        using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);
        var texts = new StringBuilder();
        foreach (var p in posts.OrderBy(p => p.When))
        {
            var img = p.Image.Length > 0 ? load(p.Image) : null;
            var stamp = p.When.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);
            foreach (var id in p.Networks)
            {
                var net = Network(id);
                texts.Append("== ").Append(p.When.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)).Append(" · ").Append(net.Name).Append(" ==\r\n").Append(Compose(p, id).Replace("\n", "\r\n")).Append("\r\n\r\n");
                if (img is null) continue;
                var f = net.Formats[0];
                var e = zip.CreateEntry($"{stamp}_{id}_{f.Id}.png", CompressionLevel.Fastest);
                using (var s = e.Open()) s.Write(PngCodec.Encode(Crop(img, f.W, f.H), fast: false));
                n++;
            }
        }
        var t = zip.CreateEntry("texte.txt");
        using (var w = new StreamWriter(t.Open(), new UTF8Encoding(true))) w.Write(texts.ToString());
        return n;
    }

    // ---------- KI (über den vorhandenen KI-Kern) ----------

    public static string IdeaPrompt(string topic, IEnumerable<string> networks, int count = 5) =>
        $"Du planst Beiträge für soziale Netzwerke ({string.Join(", ", networks.Select(n => Network(n).Name))}). Thema: {topic}. " +
        $"Schreib {count} kurze, ehrliche Beitragsideen auf Deutsch, je eine Zeile, nummeriert, ohne Hashtags, ohne erfundene Zahlen oder Versprechen.";

    public static string VariantPrompt(string text, string network) =>
        $"Schreib diesen Beitrag für {Network(network).Name} um (höchstens {Network(network).MaxChars} Zeichen, Ton passend zum Netzwerk, gleiche Aussage, nichts erfinden). Gib nur den Text aus:\n\n{text}";

    /// <summary>Nummerierte oder Aufzählungs-Zeilen der KI-Antwort als Liste.</summary>
    public static List<string> ParseIdeas(string answer) =>
        [.. (answer ?? "").Replace("\r", "").Split('\n').Select(l => Regex.Replace(l.Trim(), @"^(\d+[.)]|[-*•])\s*", "").Trim().Trim('"', '„', '“'))
            .Where(l => l.Length >= 3 && !l.EndsWith(':')).Take(20)];
}
