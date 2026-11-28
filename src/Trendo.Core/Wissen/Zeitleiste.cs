using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Wissen;

/// <summary>T-0780: Wie genau ein Datum bekannt ist (Tag … Jahrtausend).</summary>
public enum DatePrecision { Day, Month, Year, Decade, Century, Millennium }

/// <summary>T-0780 (Studio „Zeitleisten“): ein geschichtliches Datum – auch vor Christus und ungefähr („ca. 1200“, „3. Jh. v. Chr.“, „1990er“).
/// <see cref="Year"/> zählt wie in Geschichtsbüchern ohne Jahr 0: 44 v. Chr. = −44, 1 v. Chr. = −1, 1 n. Chr. = 1.
/// Für die Lage auf der Zeitachse gilt die astronomische Zählung (<see cref="Position"/>): 1 v. Chr. = 0, 44 v. Chr. = −43.</summary>
public readonly partial record struct HistDate(int Year, int Month = 0, int Day = 0, DatePrecision Precision = DatePrecision.Year, bool Approx = false)
{
    public static readonly string[] Months = ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];
    static readonly string[] MonthShort = ["jan", "feb", "mär", "apr", "mai", "jun", "jul", "aug", "sep", "okt", "nov", "dez"];

    public bool BeforeChrist => Year < 0;

    /// <summary>Astronomisches Jahr (mit Jahr 0).</summary>
    public int AstroYear => Year < 0 ? Year + 1 : Year;

    /// <summary>Beginn als Dezimaljahr (astronomisch) – Jahrzehnt/Jahrhundert beginnen an ihrem ersten Jahr.</summary>
    public double Position => AstroYear + (Month > 0 ? (Month - 1) / 12.0 : 0) + (Day > 0 ? (Day - 1) / 365.25 : 0);

    /// <summary>Ende des bekannten Bereichs (z. B. Ende des Jahrhunderts) als Dezimaljahr.</summary>
    public double PositionEnd => Precision switch
    {
        DatePrecision.Day => Position + 1 / 365.25,
        DatePrecision.Month => Position + 1 / 12.0,
        DatePrecision.Year => Position + 1,
        DatePrecision.Decade => Position + 10,
        DatePrecision.Century => Position + 100,
        _ => Position + 1000,
    };

    /// <summary>Mitte des bekannten Bereichs – dort steht ein Ereignis mit grober Angabe („3. Jh.“) auf der Achse.</summary>
    public double Center => Precision is DatePrecision.Day or DatePrecision.Month or DatePrecision.Year ? Position : (Position + PositionEnd) / 2;

    /// <summary>Deutsche Schreibweise, die <see cref="Parse"/> wieder genau so liest („14. Juli 1789“, „ca. 44 v. Chr.“, „3. Jh. v. Chr.“, „1990er“).</summary>
    public override string ToString()
    {
        var bc = Year < 0 ? " v. Chr." : "";
        var y = Math.Abs(Year);
        var core = Precision switch
        {
            DatePrecision.Day => $"{Day}. {Months[Month - 1]} {y}",
            DatePrecision.Month => $"{Months[Month - 1]} {y}",
            DatePrecision.Decade => $"{DecadeNo()}er",
            DatePrecision.Century => $"{CenturyNo()}. Jh.",
            DatePrecision.Millennium => $"{MillenniumNo()}. Jt.",
            _ => y.ToString(CultureInfo.InvariantCulture),
        };
        return (Approx ? "ca. " : "") + core + bc;
    }

    /// <summary>T-0780 (Gegenprüfung H5): Jahrzehnt wie gelesen – „50er v. Chr.“ steht als −59 (59 … 50 v. Chr.), „0er“ n. Chr. als Jahr 1 (1 … 9).</summary>
    int DecadeNo() => Year < 0 ? -Year - 9 : Year - Year % 10;

    /// <summary>Kurz für die Achse und Tabellen: „14.07.1789“, „07.1789“, „1789“, „44 v. Chr.“.</summary>
    public string Short()
    {
        var bc = Year < 0 ? " v. Chr." : ""; var y = Math.Abs(Year);
        var core = Precision switch
        {
            DatePrecision.Day => $"{Day:00}.{Month:00}.{y}",
            DatePrecision.Month => $"{Month:00}.{(y < 100 ? y.ToString("000", CultureInfo.InvariantCulture) : y.ToString(CultureInfo.InvariantCulture))}", // „02.001“: kurze Jahre dreistellig, sonst wäre es ein Tag
            DatePrecision.Decade => $"{DecadeNo()}er",
            DatePrecision.Century => $"{CenturyNo()}. Jh.",
            DatePrecision.Millennium => $"{MillenniumNo()}. Jt.",
            _ => y.ToString(CultureInfo.InvariantCulture),
        };
        return (Approx ? "ca. " : "") + core + bc;
    }

    int CenturyNo() => Year < 0 ? (-Year + 99) / 100 : (Year + 99) / 100;
    int MillenniumNo() => Year < 0 ? (-Year + 999) / 1000 : (Year + 999) / 1000;

    [GeneratedRegex(@"\b(v\.?\s*chr\.?|vor\s+christus|v\.?\s*u\.?\s*z\.?|vor\s+unserer\s+zeitrechnung|bc|bce)\s*$", RegexOptions.IgnoreCase)] private static partial Regex BcSuffix();
    [GeneratedRegex(@"\b(n\.?\s*chr\.?|nach\s+christus|u\.?\s*z\.?|ad|ce)\s*$", RegexOptions.IgnoreCase)] private static partial Regex AdSuffix();
    [GeneratedRegex(@"^(ca\.?|circa|um|etwa|ungefähr|~|≈)\s*", RegexOptions.IgnoreCase)] private static partial Regex ApproxPrefix();
    [GeneratedRegex(@"^(\d{1,2})\.\s*(jh|jhd|jahrhundert)\.?$", RegexOptions.IgnoreCase)] private static partial Regex Century();
    [GeneratedRegex(@"^(\d{1,2})\.\s*(jt|jtsd|jahrtausend)\.?$", RegexOptions.IgnoreCase)] private static partial Regex Millennium();
    [GeneratedRegex(@"^(\d{1,4})er(\s*jahre)?$", RegexOptions.IgnoreCase)] private static partial Regex Decade();
    [GeneratedRegex(@"^(\d{1,2})\.(\d{1,2})\.(\d{1,5})$")] private static partial Regex DayDot();
    [GeneratedRegex(@"^(\d{1,2})[./](\d{1,5})$")] private static partial Regex MonthDot();
    [GeneratedRegex(@"^(-?)(\d{1,5})-(\d{1,2})(?:-(\d{1,2}))?$")] private static partial Regex Iso();
    [GeneratedRegex(@"^(\d{1,2})\.?\s+([\p{L}]+)\.?\s+(\d{1,5})$")] private static partial Regex DayName();
    [GeneratedRegex(@"^([\p{L}]+)\.?\s+(\d{1,5})$")] private static partial Regex MonthName();
    [GeneratedRegex(@"^(-?)(\d{1,5})$")] private static partial Regex YearOnly();
    [GeneratedRegex(@"^(-?)(0\d{3})$")] private static partial Regex IsoYear();

    /// <summary>Ein Datum lesen; null = nicht lesbar. Erlaubt: „1789“, „14.07.1789“, „14. Juli 1789“, „Juli 1789“, „07.1789“, „1789-07-14“, „44 v. Chr.“, „-44“,
    /// „ca. 1200“, „um 800“, „~1500“, „1990er“, „3. Jh. v. Chr.“, „12. Jahrhundert“, „2. Jt. v. Chr.“, „500 BC“.</summary>
    public static HistDate? Parse(string? text)
    {
        var s = (text ?? "").Trim().Replace(' ', ' ');
        if (s.Length == 0 || s.Length > 60) return null;
        var approx = false;
        if (ApproxPrefix().Match(s) is { Success: true } am) { approx = true; s = s[am.Length..].Trim(); }
        if (s.EndsWith('?')) { approx = true; s = s.TrimEnd('?').Trim(); }
        var bc = false;
        if (BcSuffix().Match(s) is { Success: true } bm) { bc = true; s = s[..bm.Index].Trim(); }
        else if (AdSuffix().Match(s) is { Success: true } adm) s = s[..adm.Index].Trim();
        if (s.Length == 0) return null;
        int Sign(int y) => bc ? -y : y;
        static bool OkYear(int y) => y is >= 1 and <= 99_999;

        if (Century().Match(s) is { Success: true } cm && int.TryParse(cm.Groups[1].Value, out var c) && c is >= 1 and <= 99)
            return new HistDate(bc ? -(c * 100) : (c - 1) * 100 + 1, 0, 0, DatePrecision.Century, approx);
        if (Millennium().Match(s) is { Success: true } mm && int.TryParse(mm.Groups[1].Value, out var mi) && mi is >= 1 and <= 99)
            return new HistDate(bc ? -(mi * 1000) : (mi - 1) * 1000 + 1, 0, 0, DatePrecision.Millennium, approx);
        if (Decade().Match(s) is { Success: true } dm && int.TryParse(dm.Groups[1].Value, out var dy) && dy % 10 == 0)
            return new HistDate(bc ? -(dy + 9) : dy == 0 ? 1 : dy, 0, 0, DatePrecision.Decade, approx); // „0er“ n. Chr. = 1 … 9 (es gibt kein Jahr 0)
        if (DayDot().Match(s) is { Success: true } dd && int.TryParse(dd.Groups[1].Value, out var d1) && int.TryParse(dd.Groups[2].Value, out var m1) && int.TryParse(dd.Groups[3].Value, out var y1))
            return Valid(Sign(y1), m1, d1, approx);
        if (Iso().Match(s) is { Success: true } im && int.TryParse(im.Groups[2].Value, out var y2) && int.TryParse(im.Groups[3].Value, out var m2))
        {
            // ISO 8601 zählt astronomisch (mit Jahr 0): „-0044-03-15“ = 15. März 45 v. Chr., „0000“ = 1 v. Chr.; mit „v. Chr.“ dahinter gilt die Geschichtszählung
            var hy = bc ? -y2 : im.Groups[1].Value == "-" ? -(y2 + 1) : y2 == 0 ? -1 : y2;
            var day = im.Groups[4].Success && int.TryParse(im.Groups[4].Value, out var d2) ? d2 : 0;
            return day > 0 ? Valid(hy, m2, day, approx) : ValidMonth(hy, m2, approx);
        }
        if (MonthDot().Match(s) is { Success: true } md && int.TryParse(md.Groups[1].Value, out var m3) && int.TryParse(md.Groups[2].Value, out var y3) && md.Groups[2].Value.Length >= 3)
            return ValidMonth(Sign(y3), m3, approx);
        if (DayName().Match(s) is { Success: true } dn && int.TryParse(dn.Groups[1].Value, out var d4) && MonthOf(dn.Groups[2].Value) is { } m4 && int.TryParse(dn.Groups[3].Value, out var y4))
            return Valid(Sign(y4), m4, d4, approx);
        if (MonthName().Match(s) is { Success: true } mn && MonthOf(mn.Groups[1].Value) is { } m5 && int.TryParse(mn.Groups[2].Value, out var y5))
            return ValidMonth(Sign(y5), m5, approx);
        // ISO-Jahr mit führenden Nullen („-0044“, „0000“) zählt astronomisch wie oben
        if (IsoYear().Match(s) is { Success: true } iy && int.TryParse(iy.Groups[2].Value, out var y7) && !bc)
            return iy.Groups[1].Value == "-" ? new HistDate(-(y7 + 1), 0, 0, DatePrecision.Year, approx) : y7 == 0 ? new HistDate(-1, 0, 0, DatePrecision.Year, approx) : new HistDate(y7, 0, 0, DatePrecision.Year, approx);
        if (YearOnly().Match(s) is { Success: true } ym && int.TryParse(ym.Groups[2].Value, out var y6) && OkYear(y6))
            return new HistDate(ym.Groups[1].Value == "-" || bc ? -y6 : y6, 0, 0, DatePrecision.Year, approx);
        return null;

        static HistDate? Valid(int y, int m, int d, bool approx)
        {
            if (y == 0 || Math.Abs(y) > 99_999 || m is < 1 or > 12 || d < 1) return null;
            var max = m == 2 ? (IsLeap(y) ? 29 : 28) : m is 4 or 6 or 9 or 11 ? 30 : 31;
            return d > max ? null : new HistDate(y, m, d, DatePrecision.Day, approx);
        }
        static HistDate? ValidMonth(int y, int m, bool approx) => y == 0 || Math.Abs(y) > 99_999 || m is < 1 or > 12 ? null : new HistDate(y, m, 0, DatePrecision.Month, approx);
    }

    /// <summary>Schaltjahr im proleptischen gregorianischen Kalender (astronomisch gezählt). Für vor 1582 nur eine Näherung – reicht für Tagesprüfung.</summary>
    static bool IsLeap(int histYear) { var a = histYear < 0 ? histYear + 1 : histYear; return a % 4 == 0 && (a % 100 != 0 || a % 400 == 0); }

    static int? MonthOf(string name)
    {
        var n = name.Trim().TrimEnd('.').ToLowerInvariant().Replace("ä", "ä");
        if (n == "maerz" || n == "märz" || n == "mrz") return 3;
        for (var i = 0; i < Months.Length; i++) if (Months[i].ToLowerInvariant() == n) return i + 1;
        for (var i = 0; i < MonthShort.Length; i++) if (n.Length >= 3 && n.StartsWith(MonthShort[i], StringComparison.Ordinal)) return i + 1;
        string[] en = ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];
        for (var i = 0; i < en.Length; i++) if (en[i] == n || (n.Length >= 3 && en[i].StartsWith(n, StringComparison.Ordinal))) return i + 1;
        return null;
    }

    /// <summary>Lesbare Achsbeschriftung eines astronomischen Dezimaljahrs („500 v. Chr.“, „1 n. Chr.“, „1789“).</summary>
    public static string AxisLabel(double astro, double step)
    {
        var y = (int)Math.Floor(astro + 1e-9);
        var hist = y <= 0 ? y - 1 : y;
        if (step < 1)
        {
            var month = (int)Math.Round((astro - y) * 12) + 1;
            if (month > 12) { month = 1; hist = hist == -1 ? 1 : hist + 1; }
            return $"{MonthShort[month - 1][..1].ToUpperInvariant()}{MonthShort[month - 1][1..]} {Math.Abs(hist)}" + (hist < 0 ? " v. Chr." : "");
        }
        return hist < 0 ? $"{-hist} v. Chr." : hist.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>Spur (Zeile) der Zeitleiste – z. B. „Politik“, „Kunst“, „Technik“.</summary>
public sealed record TlTrack(string Id, string Name, string Color);

/// <summary>Ein Ereignis (nur <see cref="Start"/>) oder ein Zeitraum/eine Epoche (mit <see cref="End"/>). <see cref="Image"/> = PNG/JPEG-Bytes (klein, im Dokument eingebettet).</summary>
public sealed record TlItem(string Id, string Title, HistDate Start, HistDate? End, string Track, string Text = "", byte[]? Image = null, string Color = "")
{
    public bool IsPeriod => End is not null;
    public double From => Start.Position;
    public double To => End is { } e ? Math.Max(e.PositionEnd, Start.Position) : Start.PositionEnd;
}

/// <summary>Die ganze Zeitleiste.</summary>
public sealed class Timeline
{
    public string Title { get; set; } = "";
    public List<TlTrack> Tracks { get; } = [];
    public List<TlItem> Items { get; } = [];
    public string TrackName(string id) => Tracks.FirstOrDefault(t => t.Id == id)?.Name ?? (Tracks.FirstOrDefault()?.Name ?? "");
    /// <summary>T-0780 (Gegenprüfung H5): was beim Lesen verworfen oder ersetzt wurde – für die Meldung im Programm.</summary>
    public List<string> Problems { get; } = [];
}

/// <summary>T-0780: Fachkern des Studios „Zeitleisten“ – Daten aus dem Dokument lesen und schreiben, CSV/JSON ein und aus (auch TimelineJS-Spalten),
/// Ereignisse aus Zeilen („1789 | Französische Revolution“) und das Bild der Zeitleiste (Achse, Spuren, Ereignisse, Zeiträume) für Bildschirm, SVG, PNG und PDF.</summary>
public static partial class TimelineKit
{
    public const int MaxItems = 5000, MaxTracks = 40, MaxImageBytes = 600_000, MaxImportBytes = 5_000_000;
    public static readonly string[] Palette = ["#D4A017", "#4ADE80", "#60A5FA", "#F472B6", "#F97316", "#A78BFA", "#2DD4BF", "#F87171"];

    // ---------------- Dokument (JSON) ----------------

    /// <summary>Gültige Farbe: nur „#RRGGBB“ oder „#AARRGGBB“ (Gegenprüfung H4) – alles andere (Text, Skript, Anführungszeichen) wird durch den Standard ersetzt.</summary>
    public static bool ValidColor(string? c) => c is { Length: 7 or 9 } && c[0] == '#' && c.Skip(1).All(Uri.IsHexDigit);
    public static string SafeColor(string? c, string fallback) => ValidColor(c) ? c! : fallback;

    /// <summary>Nur echte Bilder (PNG/JPEG am Kopf erkannt) bis <see cref="MaxImageBytes"/> (Gegenprüfung M1). Neu kodiert wird beim Einlesen in der App.</summary>
    public static bool ValidImage(byte[]? b) => b is { Length: > 8 and <= MaxImageBytes } &&
        (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A || b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF);

    public static Timeline FromJson(JsonObject o)
    {
        var t = new Timeline { Title = Str(o["title"]) };
        foreach (var n in (o["tracks"] as JsonArray ?? []).OfType<JsonObject>().Take(MaxTracks))
            if (Str(n["id"]) is { Length: > 0 } id && t.Tracks.All(x => x.Id != id))
            {
                var c = Str(n["color"]);
                if (c.Length > 0 && !ValidColor(c)) t.Problems.Add($"Spur „{Cut(Str(n["name"]), 40)}“: Farbe „{Cut(c, 20)}“ ungültig – Standardfarbe genommen");
                t.Tracks.Add(new(id, Str(n["name"]), SafeColor(c, Palette[t.Tracks.Count % Palette.Length])));
            }
        var items = (o["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (items.Count > MaxItems) t.Problems.Add($"{items.Count - MaxItems} Einträge über der Höchstzahl {MaxItems} nicht gelesen");
        foreach (var n in items.Take(MaxItems))
        {
            var title = Str(n["title"]);
            if (HistDate.Parse(Str(n["start"])) is not { } s) { t.Problems.Add($"„{Cut(title, 40)}“: Beginn „{Cut(Str(n["start"]), 30)}“ nicht lesbar – Eintrag ausgelassen"); continue; }
            var e = HistDate.Parse(Str(n["end"]));
            if (Str(n["end"]).Length > 0 && e is null) t.Problems.Add($"„{Cut(title, 40)}“: Ende „{Cut(Str(n["end"]), 30)}“ nicht lesbar – als Ereignis gelesen");
            byte[]? img = null;
            if (Str(n["image"]) is { Length: > 0 } b64)
            {
                try { img = Convert.FromBase64String(b64); } catch (FormatException) { }
                if (!ValidImage(img)) { t.Problems.Add($"„{Cut(title, 40)}“: Bild ist kein PNG/JPEG oder zu groß – weggelassen"); img = null; }
            }
            var color = Str(n["color"]);
            if (color.Length > 0 && !ValidColor(color)) { t.Problems.Add($"„{Cut(title, 40)}“: Farbe „{Cut(color, 20)}“ ungültig – Farbe der Spur genommen"); color = ""; }
            t.Items.Add(new(Str(n["id"]) is { Length: > 0 } id ? id : NewId("e"), title, s, e, Str(n["track"]), Str(n["text"]), img, color));
        }
        return t;
    }

    public static JsonObject ItemJson(TlItem i)
    {
        var o = new JsonObject { ["id"] = i.Id, ["title"] = i.Title, ["start"] = i.Start.ToString(), ["end"] = i.End?.ToString() ?? "", ["track"] = i.Track, ["text"] = i.Text };
        if (i.Image is { Length: > 0 } img) o["image"] = Convert.ToBase64String(img);
        if (i.Color.Length > 0) o["color"] = i.Color;
        return o;
    }

    public static JsonObject TrackJson(TlTrack t) => new() { ["id"] = t.Id, ["name"] = t.Name, ["color"] = t.Color };

    public static JsonObject ToJson(Timeline t) => new()
    {
        ["title"] = t.Title,
        ["tracks"] = new JsonArray([.. t.Tracks.Select(x => (JsonNode)TrackJson(x))]),
        ["items"] = new JsonArray([.. t.Items.Select(x => (JsonNode)ItemJson(x))]),
    };

    static string Str(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : n is JsonValue v2 && v2.GetValueKind() == JsonValueKind.Number ? v2.ToJsonString() : "";
    public static string NewId(string p) => p + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Ein Eintrag aus Feldern prüfen. Wirft <see cref="ArgumentException"/> mit einem Satz für die Person.</summary>
    public static (HistDate Start, HistDate? End) CheckDates(string start, string end)
    {
        var s = HistDate.Parse(start) ?? throw new ArgumentException($"„{start}“ ist kein Datum, das Trendo lesen kann – z. B. „1789“, „14.07.1789“, „44 v. Chr.“, „ca. 1200“ oder „3. Jh. v. Chr.“.");
        if (string.IsNullOrWhiteSpace(end)) return (s, null);
        var e = HistDate.Parse(end) ?? throw new ArgumentException($"„{end}“ ist kein Datum für das Ende.");
        if (e.PositionEnd < s.Position) throw new ArgumentException("Das Ende liegt vor dem Beginn.");
        return (s, e);
    }

    // ---------------- Zeilen („Datum | Titel | …“) – auch für die KI ----------------

    [GeneratedRegex(@"^\s*(?<s>.+?)\s*(?:[–—-]|bis)\s*(?<e>.+?)\s*$")] private static partial Regex RangeRx();

    /// <summary>Zeilen „Datum | Titel [| Spur] [| Beschreibung]“ oder „Beginn – Ende | Titel …“ lesen. Unlesbare Zeilen kommen mit Grund in die zweite Liste.</summary>
    public static (List<(HistDate Start, HistDate? End, string Title, string Track, string Text)> Items, List<string> Problems) ParseLines(string text)
    {
        var ok = new List<(HistDate, HistDate?, string, string, string)>(); var bad = new List<string>();
        foreach (var raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim().TrimStart('-', '*', '•').Trim();
            if (line.Length == 0) continue;
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 2) { var colon = line.IndexOf(':'); if (colon > 0) parts = [line[..colon].Trim(), line[(colon + 1)..].Trim()]; }
            if (parts.Length < 2 || parts[1].Length == 0) { bad.Add($"„{Cut(line, 60)}“: Datum und Titel mit „|“ trennen"); continue; }
            var when = parts[0];
            HistDate? s = HistDate.Parse(when), e = null;
            if (s is null && RangeRx().Match(when) is { Success: true } rm)
            {
                var sText = rm.Groups["s"].Value; var eText = rm.Groups["e"].Value;
                e = HistDate.Parse(eText);
                // „500–300 v. Chr.“: der Zusatz gilt für beide Enden
                if (e is { BeforeChrist: true } && !Regex.IsMatch(sText, @"chr|bc|v\.", RegexOptions.IgnoreCase)) s = HistDate.Parse(sText + " v. Chr.");
                else s = HistDate.Parse(sText);
                if (s is not null && e is not null && e.Value.PositionEnd < s.Value.Position) { bad.Add($"„{Cut(line, 60)}“: Ende vor Beginn"); continue; }
                if (s is null || e is null) s = null;
            }
            if (s is null) { bad.Add($"„{Cut(when, 40)}“ ist kein lesbares Datum"); continue; }
            ok.Add((s.Value, e, Cut(parts[1], 200), parts.Length > 2 ? Cut(parts[2], 60) : "", parts.Length > 3 ? Cut(string.Join(" | ", parts[3..]), 2000) : ""));
        }
        return (ok, bad);
    }

    static string Cut(string s, int n) => s.Length <= n ? s : s[..n];

    // ---------------- CSV ----------------

    public static readonly string[] CsvHeader = ["Art", "Titel", "Beginn", "Ende", "Spur", "Beschreibung", "Farbe"];

    /// <summary>CSV mit Semikolon (Excel/LibreOffice in Deutsch), UTF-8 mit BOM.</summary>
    public static string ToCsv(Timeline t)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(';', CsvHeader)).Append("\r\n");
        foreach (var i in Sorted(t.Items))
            sb.Append(string.Join(';', new[] { i.IsPeriod ? "Zeitraum" : "Ereignis", i.Title, i.Start.ToString(), i.End?.ToString() ?? "", t.TrackName(i.Track), i.Text, i.Color }.Select(CsvCell))).Append("\r\n");
        return sb.ToString();
    }

    static string CsvCell(string s) => s.IndexOfAny([';', '"', '\n', '\r', ',']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    /// <summary>CSV einlesen: eigene Spalten (Art;Titel;Beginn;Ende;Spur;Beschreibung) oder die Spalten der TimelineJS-Tabelle
    /// (Year, Month, Day, End Year, End Month, End Day, Headline, Text, Group). Trennzeichen „;“, „,“ oder Tab werden erkannt.</summary>
    public static (Timeline Data, List<string> Problems) FromCsv(string csv)
    {
        if (csv.Length > MaxImportBytes) throw new InvalidDataException("Die Datei ist zu groß (höchstens 5 MB).");
        var rows = CsvRows(csv.TrimStart('﻿'));
        var t = new Timeline(); var bad = new List<string>();
        if (rows.Count == 0) return (t, ["Die Datei ist leer."]);
        var head = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) { foreach (var n in names) { var i = head.IndexOf(n); if (i >= 0) return i; } return -1; }
        string Cell(List<string> r, int i) => i >= 0 && i < r.Count ? r[i].Trim() : "";
        var tjs = Col("year") >= 0 && Col("headline") >= 0;
        int cTitle = tjs ? Col("headline") : Col("titel", "title", "name"), cStart = Col("beginn", "start", "datum", "date", "von"), cEnd = Col("ende", "end", "bis"),
            cTrack = tjs ? Col("group", "gruppe") : Col("spur", "gruppe", "group", "track"), cText = Col("beschreibung", "text", "notiz"), cColor = Col("farbe", "color"),
            cY = Col("year"), cM = Col("month"), cD = Col("day"), cEY = Col("end year"), cEM = Col("end month"), cED = Col("end day"), cType = Col("type");
        if (!tjs && (cTitle < 0 || cStart < 0)) return (t, ["Spalten „Titel“ und „Beginn“ fehlen (oder TimelineJS-Spalten „Year“ und „Headline“)."]);
        for (var r = 1; r < rows.Count && t.Items.Count < MaxItems; r++)
        {
            var row = rows[r];
            if (row.All(c => c.Trim().Length == 0)) continue;
            if (tjs && Cell(row, cType).Equals("title", StringComparison.OrdinalIgnoreCase)) { t.Title = Cell(row, cTitle); continue; }
            string sText = tjs ? TjsDate(Cell(row, cY), Cell(row, cM), Cell(row, cD)) : Cell(row, cStart);
            string eText = tjs ? TjsDate(Cell(row, cEY), Cell(row, cEM), Cell(row, cED)) : Cell(row, cEnd);
            var title = Cell(row, cTitle);
            if (HistDate.Parse(sText) is not { } s) { bad.Add($"Zeile {r + 1}: „{Cut(sText, 30)}“ ist kein lesbares Datum"); continue; }
            HistDate? e = eText.Length == 0 ? null : HistDate.Parse(eText);
            if (eText.Length > 0 && e is null) { bad.Add($"Zeile {r + 1}: Ende „{Cut(eText, 30)}“ nicht lesbar – als Ereignis übernommen"); }
            if (e is { } ee && ee.PositionEnd < s.Position) { bad.Add($"Zeile {r + 1}: Ende vor Beginn – als Ereignis übernommen"); e = null; }
            var trackName = Cell(row, cTrack); var track = TrackFor(t, trackName.Length > 0 ? trackName : "Allgemein");
            t.Items.Add(new(NewId("e"), title.Length > 0 ? Cut(title, 200) : "Ohne Titel", s, e, track, Cut(Cell(row, cText), 4000), null, ValidColor(Cell(row, cColor)) ? Cell(row, cColor) : ""));
        }
        return (t, bad);
    }

    static string TjsDate(string y, string m, string d)
    {
        if (y.Length == 0) return "";
        if (!int.TryParse(y, NumberStyles.Integer, CultureInfo.InvariantCulture, out var yi) || yi == 0) return y;
        var hist = yi; // TimelineJS zählt negative Jahre als v. Chr. (−44 = 44 v. Chr.)
        var yy = Math.Abs(hist).ToString(CultureInfo.InvariantCulture) + (hist < 0 ? " v. Chr." : "");
        if (int.TryParse(m, out var mi) && mi is >= 1 and <= 12)
            return int.TryParse(d, out var di) && di is >= 1 and <= 31 ? $"{di}. {HistDate.Months[mi - 1]} {yy}" : $"{HistDate.Months[mi - 1]} {yy}";
        return yy;
    }

    /// <summary>Spur zum Namen finden oder anlegen.</summary>
    public static string TrackFor(Timeline t, string name)
    {
        var n = Cut(name.Trim(), 60);
        if (t.Tracks.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase)) is { } have) return have.Id;
        if (t.Tracks.FirstOrDefault(x => x.Id == n) is { } byId) return byId.Id; // Befund Echtlauf: die KI nennt die Spur mit ihrer Kennung aus dem Dokument („t-allg“)
        if (t.Tracks.Count >= MaxTracks) return t.Tracks[^1].Id;
        var tr = new TlTrack(NewId("t"), n, Palette[t.Tracks.Count % Palette.Length]); t.Tracks.Add(tr); return tr.Id;
    }

    static List<List<string>> CsvRows(string csv)
    {
        var first = csv.Split('\n')[0];
        var sep = first.Count(c => c == ';') >= first.Count(c => c == ',') ? (first.Contains('\t') && !first.Contains(';') ? '\t' : ';') : ',';
        if (first.Count(c => c == '\t') > Math.Max(first.Count(c => c == ';'), first.Count(c => c == ','))) sep = '\t';
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; } else quoted = false; }
                else cell.Append(c);
                continue;
            }
            if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { row.Add(cell.ToString().TrimEnd('\r')); cell.Clear(); rows.Add(row); row = []; }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString().TrimEnd('\r')); rows.Add(row); }
        return rows;
    }

    // ---------------- JSON-Austausch ----------------

    /// <summary>Austausch-JSON (eigene Form, lesbar, Bilder als Base64).</summary>
    public static string ToExchangeJson(Timeline t)
    {
        var o = ToJson(t); o["format"] = "trendo-zeitleiste"; o["version"] = 1;
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>JSON einlesen: eigene Form oder die JSON-Form von TimelineJS („events“ mit start_date/end_date/text.headline/group).</summary>
    public static (Timeline Data, List<string> Problems) FromExchangeJson(string json)
    {
        if (json.Length > MaxImportBytes) throw new InvalidDataException("Die Datei ist zu groß (höchstens 5 MB).");
        JsonObject o;
        try { o = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject ?? throw new InvalidDataException("Das ist keine Zeitleiste (JSON-Objekt erwartet)."); }
        catch (JsonException e) { throw new InvalidDataException("Die JSON-Datei ist beschädigt: " + e.Message); }
        if (o["events"] is JsonArray events)
        {
            var t = new Timeline { Title = Str(o["title"]?["text"]?["headline"]) }; var bad = new List<string>();
            foreach (var ev in events.OfType<JsonObject>().Take(MaxItems))
            {
                string D(JsonNode? n) => n is JsonObject d ? TjsDate(Str(d["year"]), Str(d["month"]), Str(d["day"])) : "";
                var sText = D(ev["start_date"]); var eText = D(ev["end_date"]);
                if (HistDate.Parse(sText) is not { } s) { bad.Add($"„{Cut(sText, 30)}“ ist kein lesbares Datum"); continue; }
                var e = eText.Length > 0 ? HistDate.Parse(eText) : null;
                var track = TrackFor(t, Str(ev["group"]) is { Length: > 0 } g ? g : "Allgemein");
                t.Items.Add(new(NewId("e"), Cut(StripTags(Str(ev["text"]?["headline"])), 200), s, e is { } ee && ee.PositionEnd >= s.Position ? e : null, track, Cut(StripTags(Str(ev["text"]?["text"])), 4000)));
            }
            return (t, bad);
        }
        var own = FromJson(o); var probs = new List<string>();
        var raw = (o["items"] as JsonArray ?? []).Count;
        if (raw > own.Items.Count) probs.Add($"{raw - own.Items.Count} Einträge ohne lesbares Datum übersprungen");
        foreach (var i in own.Items.Where(i => own.Tracks.All(tr => tr.Id != i.Track)).ToList()) { own.Items[own.Items.IndexOf(i)] = i with { Track = TrackFor(own, "Allgemein") }; }
        return (own, probs);
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex Tags();
    static string StripTags(string s) => System.Net.WebUtility.HtmlDecode(Tags().Replace(s, "")).Trim();

    public static IEnumerable<TlItem> Sorted(IEnumerable<TlItem> items) => items.OrderBy(i => i.From).ThenBy(i => i.Title, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true));

    // ---------------- Bild der Zeitleiste ----------------

    /// <summary>Zeichenbausteine (Pixel, oben links 0/0) – gleich für Bildschirm, SVG, PNG und PDF.</summary>
    public abstract record Prim;
    public sealed record PRect(double X, double Y, double W, double H, string Fill, string? Stroke = null, double Radius = 0, bool Dashed = false, double Opacity = 1, string? ItemId = null) : Prim;
    public sealed record PLine(double X1, double Y1, double X2, double Y2, string Color, double Width = 1, bool Dashed = false) : Prim;
    public sealed record PText(double X, double Y, string Text, double Size, string Color, bool Bold = false, int Anchor = 0, string? ItemId = null) : Prim; // Anchor −1 links … 0 Mitte … 1 rechts; Y = Grundlinie
    public sealed record PCircle(double Cx, double Cy, double R, string Fill, string? Stroke = null, bool Dashed = false, string? ItemId = null) : Prim;
    public sealed record PImage(double X, double Y, double W, double H, byte[] Data, string? ItemId = null) : Prim;

    /// <summary>Fertiges Bild: Größe, Zeichenbausteine und Trefferflächen je Eintrag.</summary>
    public sealed record Scene(double Width, double Height, double From, double To, List<Prim> Prims, Dictionary<string, (double X, double Y, double W, double H)> Hits)
    {
        public double XOf(double year, double left, double right) => left + (year - From) / Math.Max(1e-9, To - From) * (right - left);
    }

    public const double Left = 150, RightPad = 30, AxisTop = 64, LaneGap = 10, RowH = 26, EventRowH = 34;
    public const string Ink = "#E5E7EB", Muted = "#9CA3AF", Grid = "#33FFFFFF", Paper = "#14161B";

    /// <summary>Ganzer Bereich aller Einträge mit etwas Rand (für „Alles zeigen“ und den Export).</summary>
    public static (double From, double To) Fit(Timeline t)
    {
        if (t.Items.Count == 0) return (1900, 2030);
        var from = t.Items.Min(i => i.From); var to = t.Items.Max(i => i.To);
        var span = Math.Max(to - from, 1); return (from - span * 0.06, to + span * 0.06);
    }

    /// <summary>Gut lesbare Schrittweite der Achse (1, 2, 5 · 10ⁿ Jahre; unter einem Jahr Monate).</summary>
    public static double Step(double span, double pixels)
    {
        var target = span / Math.Max(1, pixels / 110);
        if (target < 1) return target < 1 / 12.0 * 1.5 ? 1 / 12.0 : target < 0.25 * 1.5 ? 0.25 : 0.5;
        var p = Math.Pow(10, Math.Floor(Math.Log10(target)));
        foreach (var m in new[] { 1, 2, 5, 10 }) if (target <= m * p) return m * p;
        return 10 * p;
    }

    /// <summary>Zeitleiste zeichnen: Titel, Achse mit Jahreszahlen (auch v. Chr.), je Spur eine Bahn; Zeiträume als Balken (überlappende in eigenen Reihen),
    /// Ereignisse als Punkt mit Beschriftung (ohne Überdeckung gestapelt). Ungefähre Angaben haben einen gestrichelten Rand und „ca.“ in der Beschriftung.</summary>
    public static Scene Layout(Timeline t, double from, double to, double width, string? selected = null, bool images = true, bool paper = false)
    {
        width = Math.Max(500, width);
        if (to - from < 1 / 24.0) { var mid = (from + to) / 2; from = mid - 1 / 48.0; to = mid + 1 / 48.0; }
        var prims = new List<Prim>(); var hits = new Dictionary<string, (double, double, double, double)>();
        var right = width - RightPad;
        double X(double y) => Left + (y - from) / (to - from) * (right - Left);
        var ink = paper ? "#1F2937" : Ink; var muted = paper ? "#6B7280" : Muted; var grid = paper ? "#E5E7EB" : Grid;
        prims.Add(new PText(16, 30, t.Title.Length > 0 ? t.Title : "Zeitleiste", 20, ink, true, -1));
        var step = Step(to - from, right - Left);
        // Achse
        var axisY = AxisTop;
        var tickStart = Math.Ceiling(from / step) * step;
        var ticks = new List<double>();
        for (var v = tickStart; v <= to + 1e-9 && ticks.Count < 400; v += step) ticks.Add(v);
        var y0 = axisY + 18.0;
        var lanes = t.Tracks.Count > 0 ? t.Tracks : [new TlTrack("", "Allgemein", Palette[0])];
        var laneLayouts = new List<(TlTrack Track, double Top, double Height, List<Prim> Items)>();
        var top = y0;
        var visible = t.Items.Where(i => i.To >= from && i.From <= to).ToList();
        foreach (var lane in lanes)
        {
            var mine = visible.Where(i => i.Track == lane.Id || (lane.Id == "" || t.Tracks.All(x => x.Id != i.Track)) && lane == lanes[0]).ToList();
            var periods = Sorted(mine.Where(i => i.IsPeriod)).ToList(); var events = Sorted(mine.Where(i => !i.IsPeriod)).ToList();
            var items = new List<Prim>();
            // Zeiträume in Reihen
            var rowEnds = new List<double>();
            var periodRows = new List<(TlItem It, int Row)>();
            foreach (var p in periods)
            {
                var x1 = X(p.From); var x2 = Math.Max(X(p.To), x1 + 4);
                var label = p.Title; var need = Math.Max(x2, x1 + 7 * label.Length + 12);
                var row = rowEnds.FindIndex(e => e + 4 < x1);
                if (row < 0) { rowEnds.Add(need); row = rowEnds.Count - 1; } else rowEnds[row] = need;
                periodRows.Add((p, row));
            }
            // Ereignisse in Reihen (Beschriftung rechts neben dem Punkt)
            // Beschriftung rechts neben dem Punkt; reicht sie über den rechten Rand, steht sie links davon
            var evEnds = new List<double>(); var evRows = new List<(TlItem It, int Row, double W, bool Flip)>();
            foreach (var e in events)
            {
                var x = X(e.Start.Center);
                var hasImg = images && e.Image is { Length: > 0 };
                var w = Math.Max(TextW(e.Title, 12.5), TextW(e.Start.Short(), 10.5)) + 16 + (hasImg ? 52 : 0);
                var flip = x + w > width - 6 && x - w > Left;
                var (s0, e0) = flip ? (x - w, x + 8) : (x - 6, x + w);
                var row = evEnds.FindIndex(end => end + 6 < s0);
                if (row < 0) { evEnds.Add(e0); row = evEnds.Count - 1; } else evEnds[row] = e0;
                evRows.Add((e, row, w, flip));
            }
            var periodH = rowEnds.Count * RowH; var evH = evRows.Count == 0 ? 0 : evEnds.Count * (images && events.Any(e => e.Image is { Length: > 0 }) ? 44 : EventRowH);
            var laneH = Math.Max(44, periodH + evH + 16);
            var laneTop = top;
            foreach (var (p, row) in periodRows)
            {
                var x1 = X(p.From); var x2 = Math.Max(X(p.To), x1 + 4);
                var yy = laneTop + 8 + row * RowH;
                var color = SafeColor(p.Color.Length > 0 ? p.Color : lane.Color, Palette[0]);
                var approx = p.Start.Approx || p.End is { Approx: true } || p.Start.Precision >= DatePrecision.Decade || p.End is { Precision: >= DatePrecision.Decade };
                var sel = p.Id == selected;
                items.Add(new PRect(x1, yy, x2 - x1, RowH - 6, color, sel ? "#FFFFFF" : approx ? color : null, 4, approx, paper ? 0.85 : 0.75, p.Id));
                var lx = Math.Max(x1 + 6, Left + 4);
                items.Add(new PText(lx, yy + RowH - 11, (approx ? "ca. " : "") + p.Title, 12, paper ? "#111827" : "#0B0C10", true, -1, p.Id));
                hits[p.Id] = (x1, yy, Math.Max(x2 - x1, TextW(p.Title, 12) + 12), RowH - 6);
            }
            var evTop = laneTop + 8 + periodH;
            var evRowH = images && events.Any(e => e.Image is { Length: > 0 }) ? 44 : EventRowH;
            foreach (var (e, row, w, flip) in evRows)
            {
                var x = X(e.Start.Center);
                var yy = evTop + row * evRowH;
                var color = SafeColor(e.Color.Length > 0 ? e.Color : lane.Color, Palette[0]);
                var sel = e.Id == selected;
                var approx = e.Start.Approx || e.Start.Precision >= DatePrecision.Decade;
                if (e.Start.Precision >= DatePrecision.Decade) // grobe Angabe: Bereich als zarter Balken unter dem Punkt
                    items.Add(new PRect(X(e.Start.Position), yy + 14, Math.Max(2, X(e.Start.PositionEnd) - X(e.Start.Position)), 4, color, null, 2, false, 0.35, e.Id));
                items.Add(new PLine(x, laneTop + 2, x, yy + 10, grid, 1, approx));
                items.Add(new PCircle(x, yy + 16, sel ? 7 : 5.5, color, sel ? "#FFFFFF" : approx ? ink : null, approx, e.Id));
                if (flip)
                {
                    var tx = x - 12;
                    if (images && ValidImage(e.Image)) { items.Add(new PImage(tx - 46, yy + 2, 46, 34, e.Image!, e.Id)); tx -= 52; }
                    items.Add(new PText(tx, yy + 15, e.Title, 12.5, ink, sel, 1, e.Id));
                    items.Add(new PText(tx, yy + 28, e.Start.Short(), 10.5, muted, false, 1, e.Id));
                    hits[e.Id] = (x - w, yy + 2, w + 8, evRowH - 4);
                }
                else
                {
                    var tx = x + 12;
                    if (images && ValidImage(e.Image)) { items.Add(new PImage(tx, yy + 2, 46, 34, e.Image!, e.Id)); tx += 52; }
                    items.Add(new PText(tx, yy + 15, e.Title, 12.5, ink, sel, -1, e.Id));
                    items.Add(new PText(tx, yy + 28, e.Start.Short(), 10.5, muted, false, -1, e.Id));
                    hits[e.Id] = (x - 8, yy + 2, w + 8, evRowH - 4);
                }
            }
            laneLayouts.Add((lane, laneTop, laneH, items));
            top += laneH + LaneGap;
        }
        var height = top + 24;
        // Hintergrund, Bahnen, Gitter
        prims.Insert(0, new PRect(0, 0, width, height, paper ? "#FFFFFF" : Paper));
        var band = paper ? "#F3F4F6" : "#0AFFFFFF";
        foreach (var (lane, laneTop, laneH, _) in laneLayouts)
        {
            prims.Add(new PRect(0, laneTop, width, laneH, band));
            prims.Add(new PRect(0, laneTop, 5, laneH, SafeColor(lane.Color, Palette[0])));
            prims.Add(new PText(14, laneTop + 22, lane.Name, 13, ink, true, -1));
        }
        foreach (var v in ticks)
        {
            // Jahresraster in Geschichtszählung: −500 steht für „500 v. Chr.“ (Beginn dieses Jahres = astronomisch −499), 0 für die Zeitenwende (Beginn von 1 n. Chr.)
            var at = step >= 1 && v <= 0 ? v + 1 : v;
            if (at < from || at > to) continue;
            var x = X(at);
            var label = step < 1 ? HistDate.AxisLabel(v, step) : v < 0 ? $"{(-v).ToString("0", CultureInfo.InvariantCulture)} v. Chr." : v == 0 ? "Chr. Geb." : v.ToString("0", CultureInfo.InvariantCulture);
            prims.Add(new PLine(x, axisY + 4, x, height - 20, grid, 1));
            prims.Add(new PText(x, axisY - 4, label, 11, muted, false, 0));
        }
        if (from < 1.0 && to > 1.0) prims.Add(new PLine(X(1), axisY - 18, X(1), height - 20, paper ? "#9CA3AF" : "#66D4A017", 1.5, true)); // Zeitenwende (Beginn von 1 n. Chr.)
        prims.Add(new PLine(Left, axisY + 4, right, axisY + 4, muted, 1.2));
        foreach (var (_, _, _, items) in laneLayouts) prims.AddRange(items);
        return new Scene(width, height, from, to, prims, hits);
    }

    /// <summary>Geschätzte Textbreite in Pixeln (Durchschnitt für Sans-Schriften).</summary>
    public static double TextW(string s, double size) => (s ?? "").Length * size * 0.56;

    // ---------------- SVG ----------------

    static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static string X(string s) => Office.XmlSafe.Escape(s);
    static (string Color, double Alpha) Svg(string c)
    {
        var h = c.TrimStart('#');
        if (h.Length == 8) return ("#" + h[2..], int.Parse(h[..2], NumberStyles.HexNumber) / 255.0);
        return ("#" + h, 1);
    }

    /// <summary>Echtes SVG 1.1 (öffnet in Inkscape und jedem Browser), Bilder als eingebettete Daten.</summary>
    public static string ToSvg(Scene s, string title = "Zeitleiste") // T-0788: Titel wählbar (Stammbaum nutzt dieselben Bausteine)
    {
        var sb = new StringBuilder();
        sb.Append($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" version=\"1.1\" width=\"{F(s.Width)}\" height=\"{F(s.Height)}\" viewBox=\"0 0 {F(s.Width)} {F(s.Height)}\" font-family=\"Segoe UI, Arial, sans-serif\">\n");
        sb.Append("<title>").Append(X(title)).Append("</title>\n");
        foreach (var p in s.Prims)
        {
            switch (p)
            {
                case PRect r:
                {
                    var (fc, fa) = Svg(r.Fill);
                    sb.Append($"<rect x=\"{F(r.X)}\" y=\"{F(r.Y)}\" width=\"{F(r.W)}\" height=\"{F(r.H)}\" rx=\"{F(r.Radius)}\" fill=\"{fc}\" fill-opacity=\"{F(fa * r.Opacity)}\"");
                    if (r.Stroke is { } st) { var (sc, sa) = Svg(st); sb.Append($" stroke=\"{sc}\" stroke-opacity=\"{F(sa)}\" stroke-width=\"1.5\"" + (r.Dashed ? " stroke-dasharray=\"5 3\"" : "")); }
                    sb.Append("/>\n"); break;
                }
                case PLine l:
                {
                    var (c, a) = Svg(l.Color);
                    sb.Append($"<line x1=\"{F(l.X1)}\" y1=\"{F(l.Y1)}\" x2=\"{F(l.X2)}\" y2=\"{F(l.Y2)}\" stroke=\"{c}\" stroke-opacity=\"{F(a)}\" stroke-width=\"{F(l.Width)}\"" + (l.Dashed ? " stroke-dasharray=\"4 3\"" : "") + "/>\n"); break;
                }
                case PCircle c:
                {
                    var (fc, fa) = Svg(c.Fill);
                    sb.Append($"<circle cx=\"{F(c.Cx)}\" cy=\"{F(c.Cy)}\" r=\"{F(c.R)}\" fill=\"{fc}\" fill-opacity=\"{F(fa)}\"");
                    if (c.Stroke is { } st) { var (sc, sa) = Svg(st); sb.Append($" stroke=\"{sc}\" stroke-opacity=\"{F(sa)}\" stroke-width=\"1.5\"" + (c.Dashed ? " stroke-dasharray=\"2 2\"" : "")); }
                    sb.Append("/>\n"); break;
                }
                case PText tx:
                {
                    var (c, a) = Svg(tx.Color);
                    var anchor = tx.Anchor < 0 ? "start" : tx.Anchor > 0 ? "end" : "middle";
                    sb.Append($"<text x=\"{F(tx.X)}\" y=\"{F(tx.Y)}\" font-size=\"{F(tx.Size)}\" fill=\"{c}\" fill-opacity=\"{F(a)}\" text-anchor=\"{anchor}\"" + (tx.Bold ? " font-weight=\"bold\"" : "") + $">{X(tx.Text)}</text>\n"); break;
                }
                case PImage im:
                    sb.Append($"<image x=\"{F(im.X)}\" y=\"{F(im.Y)}\" width=\"{F(im.W)}\" height=\"{F(im.H)}\" preserveAspectRatio=\"xMidYMid slice\" xlink:href=\"data:{Mime(im.Data)};base64,{Convert.ToBase64String(im.Data)}\"/>\n"); break;
            }
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    public static string Mime(byte[] data) => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 ? "image/jpeg" : "image/png";

    // ---------------- PDF ----------------

    /// <summary>Zeitleiste als PDF (eine Seite in Bildgröße, Vektoren und echter Text – durchsuchbar).</summary>
    public static int ToPdf(Scene s, string path, string title = "Zeitleiste") // T-0788: Titel wählbar
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle); // Schriften einrichten
        using var pdf = new PdfSharp.Pdf.PdfDocument();
        pdf.Info.Title = title;
        var page = pdf.AddPage();
        const double k = 0.75; // px → pt
        page.Width = PdfSharp.Drawing.XUnit.FromPoint(s.Width * k); page.Height = PdfSharp.Drawing.XUnit.FromPoint(s.Height * k);
        using (var g = PdfSharp.Drawing.XGraphics.FromPdfPage(page))
        {
            g.ScaleTransform(k);
            foreach (var p in s.Prims)
            {
                switch (p)
                {
                    case PRect r:
                    {
                        var brush = new PdfSharp.Drawing.XSolidBrush(XC(r.Fill, r.Opacity));
                        var rect = new PdfSharp.Drawing.XRect(r.X, r.Y, Math.Max(0.1, r.W), Math.Max(0.1, r.H));
                        PdfSharp.Drawing.XPen? pen = r.Stroke is { } st ? new PdfSharp.Drawing.XPen(XC(st), 1.5) { DashStyle = r.Dashed ? PdfSharp.Drawing.XDashStyle.Dash : PdfSharp.Drawing.XDashStyle.Solid } : null;
                        if (r.Radius > 0) { var sz = new PdfSharp.Drawing.XSize(r.Radius * 2, r.Radius * 2); if (pen is null) g.DrawRoundedRectangle(brush, rect, sz); else g.DrawRoundedRectangle(pen, brush, rect, sz); }
                        else if (pen is null) g.DrawRectangle(brush, rect); else g.DrawRectangle(pen, brush, rect);
                        break;
                    }
                    case PLine l:
                        g.DrawLine(new PdfSharp.Drawing.XPen(XC(l.Color), l.Width) { DashStyle = l.Dashed ? PdfSharp.Drawing.XDashStyle.Dash : PdfSharp.Drawing.XDashStyle.Solid }, l.X1, l.Y1, l.X2, l.Y2); break;
                    case PCircle c:
                    {
                        var rect = new PdfSharp.Drawing.XRect(c.Cx - c.R, c.Cy - c.R, c.R * 2, c.R * 2);
                        if (c.Stroke is { } st) g.DrawEllipse(new PdfSharp.Drawing.XPen(XC(st), 1.5), new PdfSharp.Drawing.XSolidBrush(XC(c.Fill)), rect); else g.DrawEllipse(new PdfSharp.Drawing.XSolidBrush(XC(c.Fill)), rect);
                        break;
                    }
                    case PText tx:
                    {
                        var font = new PdfSharp.Drawing.XFont("Arial", tx.Size * 0.95, tx.Bold ? PdfSharp.Drawing.XFontStyleEx.Bold : PdfSharp.Drawing.XFontStyleEx.Regular);
                        var w = g.MeasureString(tx.Text, font).Width;
                        var x = tx.Anchor < 0 ? tx.X : tx.Anchor > 0 ? tx.X - w : tx.X - w / 2;
                        g.DrawString(tx.Text, font, new PdfSharp.Drawing.XSolidBrush(XC(tx.Color)), x, tx.Y);
                        break;
                    }
                    case PImage im:
                        try { using var ms = new MemoryStream(im.Data); var xi = PdfSharp.Drawing.XImage.FromStream(ms); g.DrawImage(xi, im.X, im.Y, im.W, im.H); }
                        catch (Exception e) when (e is not OutOfMemoryException) { } // T-0780 (M2): kaputtes Bild – PDF ohne dieses Bild
                        break;
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        pdf.Save(tmp);
        SafeFile.Move(tmp, path);
        return 1;
    }

    static PdfSharp.Drawing.XColor XC(string c, double opacity = 1)
    {
        var h = c.TrimStart('#');
        int a = 255, r, g, b;
        if (h.Length == 8) { a = Convert.ToInt32(h[..2], 16); h = h[2..]; }
        r = Convert.ToInt32(h[..2], 16); g = Convert.ToInt32(h[2..4], 16); b = Convert.ToInt32(h[4..6], 16);
        return PdfSharp.Drawing.XColor.FromArgb((int)Math.Round(a * Math.Clamp(opacity, 0, 1)), r, g, b);
    }
}
