using System.Globalization;
using System.Text;

namespace Trendo.Core.Office;

/// <summary>T-0701: Zahlenformat-Codes wie in Excel und LibreOffice („0.000\" kg\"“, „#,##0.00 €;[Red]-#,##0.00 €“, „TT.MM.JJJJ“, „h:mm“ …)
/// anzeigen – auf Deutsch (Komma als Dezimalzeichen, Punkt als Tausender, deutsche Monats- und Tagesnamen). Der Code selbst bleibt unverändert in der Zelle
/// und wird so zurückgeschrieben. Abschnitte (positiv;negativ;null;Text), Bedingungen ([&gt;=100]), Farben, Prozent, Tausender-Skalierung,
/// wissenschaftliche Schreibweise, Brüche, Datum/Uhrzeit und verstrichene Zeit ([h]) werden ausgewertet.</summary>
public static class SheetNumberFormat
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Die eingebauten Formate von Excel (numFmtId ohne eigenen Eintrag in styles.xml), wie Excel sie schreibt.</summary>
    public static readonly IReadOnlyDictionary<int, string> Builtin = new Dictionary<int, string>
    {
        [0] = "General", [1] = "0", [2] = "0.00", [3] = "#,##0", [4] = "#,##0.00", [9] = "0%", [10] = "0.00%", [11] = "0.00E+00", [12] = "# ?/?", [13] = "# ??/??",
        [14] = "mm-dd-yy", [15] = "d-mmm-yy", [16] = "d-mmm", [17] = "mmm-yy", [18] = "h:mm AM/PM", [19] = "h:mm:ss AM/PM", [20] = "h:mm", [21] = "h:mm:ss", [22] = "m/d/yy h:mm",
        [37] = "#,##0 ;(#,##0)", [38] = "#,##0 ;[Red](#,##0)", [39] = "#,##0.00;(#,##0.00)", [40] = "#,##0.00;[Red](#,##0.00)", [45] = "mm:ss", [46] = "[h]:mm:ss", [47] = "mmss.0", [48] = "##0.0E+0", [49] = "@",
    };

    /// <summary>Eingebautes Format zum Code, falls es eines gibt (für das Zurückschreiben ohne eigenen Eintrag).</summary>
    public static int? BuiltinId(string code) { foreach (var (id, c) in Builtin) if (c == code) return id; return null; }

    /// <summary>Text, wie die Zahl mit diesem Code angezeigt wird. Farbe aus „[Rot]“/„[Red]“ als #RRGGBB in <paramref name="color"/>.</summary>
    public static string Format(double x, string code, out string? color) => FormatCore(x, code, out color).Replace(' ', ' '); // geschütztes Leerzeichen (LibreOffice: „0,00 %“) wie ein Leerzeichen zeigen

    static string FormatCore(double x, string code, out string? color)
    {
        color = null;
        // Excels eingebautes Kurzdatum (Nr. 14 und 22) richtet sich nach dem Land – auf Deutsch TT.MM.JJJJ
        code = code switch { "mm-dd-yy" => "dd.mm.yyyy", "m/d/yy h:mm" => "dd.mm.yyyy hh:mm", _ => code };
        var sections = Split(code);
        if (sections.Count == 0) return SheetFormat.General(x);
        string sec; var abs = false;
        var conds = sections.Select(Condition).ToList();
        if (conds.Any(c => c is not null))
        {
            // Mit Bedingungen: erster passender Abschnitt, sonst der letzte ohne Bedingung
            var idx = conds.FindIndex(c => c is { } cc && Holds(cc, x));
            if (idx < 0) idx = conds.FindIndex(c => c is null);
            if (idx < 0) return new string('#', 6);
            sec = sections[idx]; abs = idx > 0 && x < 0;
        }
        else if (x > 0 || sections.Count == 1 || x == 0 && sections.Count == 2) sec = sections[0];
        else if (x < 0) { sec = sections[1]; abs = true; }
        else sec = sections.Count > 2 ? sections[2] : sections[0];
        if (sections.Count > 1 && x < 0 && sec == sections[0] && !abs && conds.All(c => c is null)) { } // nur ein Abschnitt für alles: Minus bleibt
        var tokens = Tokenize(sec, out color);
        if (tokens.All(t => t.Kind == Tk.Text && t.Text == "@")) return SheetFormat.General(x);
        if (tokens.Any(t => t.Kind == Tk.General)) return Join(tokens, SheetFormat.General(abs ? Math.Abs(x) : x));
        if (tokens.Any(t => t.Kind is Tk.Date)) return Dates(tokens, abs ? Math.Abs(x) : x);
        if (tokens.Any(t => t.Kind == Tk.Slash) && !tokens.Any(t => t.Kind == Tk.Digit)) return Join(tokens, SheetFormat.General(x));
        return Number(tokens, abs ? Math.Abs(x) : x, abs);
    }

    /// <summary>Text in einer Zelle mit Code: Ein Textabschnitt („@“) setzt ihn ein, sonst bleibt er, wie er ist.</summary>
    public static string FormatText(string text, string code)
    {
        var sections = Split(code);
        var sec = sections.Count >= 4 ? sections[3] : sections.Count == 1 && sections[0].Contains('@') ? sections[0] : null;
        if (sec is null) return text;
        var tokens = Tokenize(sec, out _);
        var sb = new StringBuilder();
        foreach (var t in tokens) sb.Append(t.Kind == Tk.Text && t.Text == "@" ? text : t.Kind == Tk.Text ? t.Text : "");
        return sb.ToString();
    }

    /// <summary>Ist es ein Datums- oder Uhrzeitformat? (Dann legt Trendo den Wert als Datum ab.)</summary>
    public static bool IsDate(string code) => Split(code).Take(1).Any(s => Tokenize(s, out _).Any(t => t.Kind == Tk.Date));

    // ---------------- Zerlegen ----------------
    enum Tk { Text, Digit, Point, Comma, Percent, Exp, Slash, Date, General }
    readonly record struct Tok(Tk Kind, string Text);

    static List<string> Split(string code)
    {
        var list = new List<string>(); var sb = new StringBuilder(); var quote = false; var bracket = false;
        for (var i = 0; i < code.Length; i++)
        {
            var ch = code[i];
            if (quote) { sb.Append(ch); if (ch == '"') quote = false; continue; }
            if (ch == '\\' && i + 1 < code.Length) { sb.Append(ch).Append(code[++i]); continue; }
            if (ch == '"') quote = true;
            else if (ch == '[') bracket = true;
            else if (ch == ']') bracket = false;
            else if (ch == ';' && !bracket) { list.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(ch);
        }
        list.Add(sb.ToString());
        return list;
    }

    static (string Op, double V)? Condition(string sec)
    {
        var m = System.Text.RegularExpressions.Regex.Match(sec, @"\[(<=|>=|<>|<|>|=)(-?[0-9.]+)\]");
        return m.Success && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? (m.Groups[1].Value, v) : null;
    }

    static bool Holds((string Op, double V) c, double x) => c.Op switch { "<" => x < c.V, "<=" => x <= c.V, ">" => x > c.V, ">=" => x >= c.V, "=" => x == c.V, _ => x != c.V };

    static readonly Dictionary<string, string> ColorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Red"] = "#FF0000", ["Rot"] = "#FF0000", ["Blue"] = "#0000FF", ["Blau"] = "#0000FF", ["Green"] = "#00FF00", ["Grün"] = "#00FF00", ["Black"] = "#000000", ["Schwarz"] = "#000000",
        ["White"] = "#FFFFFF", ["Weiß"] = "#FFFFFF", ["Yellow"] = "#FFFF00", ["Gelb"] = "#FFFF00", ["Magenta"] = "#FF00FF", ["Cyan"] = "#00FFFF", ["Zyan"] = "#00FFFF",
    };

    static List<Tok> Tokenize(string sec, out string? color)
    {
        color = null;
        var list = new List<Tok>(); var dateCtx = false;
        for (var i = 0; i < sec.Length; i++)
        {
            var ch = sec[i];
            switch (ch)
            {
                case '"':
                {
                    var end = sec.IndexOf('"', i + 1); if (end < 0) end = sec.Length;
                    list.Add(new(Tk.Text, sec[(i + 1)..Math.Min(end, sec.Length)])); i = end; break;
                }
                case '\\': if (i + 1 < sec.Length) list.Add(new(Tk.Text, sec[++i].ToString())); break;
                case '_': i++; list.Add(new(Tk.Text, " ")); break;   // Platz in der Breite des nächsten Zeichens
                case '*': i++; break;                              // Füllzeichen: in einer Zelle ohne feste Breite weglassen
                case '[':
                {
                    var end = sec.IndexOf(']', i + 1); if (end < 0) end = sec.Length - 1;
                    var inner = sec[(i + 1)..end];
                    if (ColorNames.TryGetValue(inner, out var c)) color = c;
                    else if (inner.Length > 0 && inner.TrimStart('h', 'H', 'm', 'M', 's', 'S').Length == 0) { list.Add(new(Tk.Date, "[" + inner.ToLowerInvariant() + "]")); dateCtx = true; }
                    else if (inner.StartsWith('$')) { var sym = inner[1..].Split('-')[0]; if (sym.Length > 0) list.Add(new(Tk.Text, sym)); }
                    i = end; break;
                }
                case '0' or '#' or '?': list.Add(new(Tk.Digit, ch.ToString())); break;
                case '.': list.Add(new(dateCtx && i + 1 < sec.Length && sec[i + 1] == '0' ? Tk.Date : Tk.Point, ".")); if (list[^1].Kind == Tk.Date) { var n = 0; while (i + 1 < sec.Length && sec[i + 1] == '0') { n++; i++; } list[^1] = new(Tk.Date, "." + new string('0', n)); } break;
                case ',': list.Add(new(Tk.Comma, ",")); break;
                case '%': list.Add(new(Tk.Percent, "%")); break;
                case '/' when dateCtx || list.Any(t => t.Kind == Tk.Digit): list.Add(new(Tk.Slash, "/")); break; // im Datum: Datumstrennzeichen des Landes
                case 'E' or 'e' when i + 1 < sec.Length && sec[i + 1] is '+' or '-': list.Add(new(Tk.Exp, sec[i + 1].ToString())); i++; break;
                case '@': list.Add(new(Tk.Text, "@")); break;
                default:
                {
                    if (string.Compare(sec, i, "General", 0, 7, StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(sec, i, "Standard", 0, 8, StringComparison.OrdinalIgnoreCase) == 0)
                    { list.Add(new(Tk.General, "")); i += sec[i] is 'G' or 'g' ? 6 : 7; break; }
                    if (string.Compare(sec, i, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) == 0) { list.Add(new(Tk.Date, "AM/PM")); i += 4; dateCtx = true; break; }
                    if (string.Compare(sec, i, "A/P", 0, 3, StringComparison.OrdinalIgnoreCase) == 0) { list.Add(new(Tk.Date, "A/P")); i += 2; dateCtx = true; break; }
                    var lower = char.ToLowerInvariant(ch);
                    // Deutsche Codes aus LibreOffice/Excel: J = Jahr, T = Tag
                    if (lower is 'y' or 'm' or 'd' or 'h' or 's' or 'j' or 't')
                    {
                        var j = i; while (j < sec.Length && char.ToLowerInvariant(sec[j]) == lower) j++;
                        var norm = lower switch { 'j' => 'y', 't' => 'd', _ => lower };
                        list.Add(new(Tk.Date, new string(norm, j - i))); i = j - 1; dateCtx = true; break;
                    }
                    list.Add(new(Tk.Text, ch.ToString()));
                    break;
                }
            }
        }
        return list;
    }

    static string Join(List<Tok> tokens, string value)
    {
        var sb = new StringBuilder(); var done = false;
        foreach (var t in tokens) { if (t.Kind == Tk.General && !done) { sb.Append(value); done = true; } else if (t.Kind == Tk.Text) sb.Append(t.Text); }
        return sb.ToString();
    }

    // ---------------- Zahlen ----------------
    static string Number(List<Tok> tokens, double x, bool absSection)
    {
        var neg = x < 0 && !absSection; var v = Math.Abs(x);
        var pct = tokens.Count(t => t.Kind == Tk.Percent); for (var i = 0; i < pct; i++) v *= 100;
        var expAt = tokens.FindIndex(t => t.Kind == Tk.Exp);
        var slashAt = tokens.FindIndex(t => t.Kind == Tk.Slash);
        var pointAt = tokens.FindIndex(t => t.Kind == Tk.Point);
        var numEnd = expAt >= 0 ? expAt : tokens.Count;
        // Kommas: zwischen Ziffern = Tausendertrennung; direkt nach der letzten Ganzzahl-Ziffer = durch 1000 teilen
        var intEnd = pointAt >= 0 && pointAt < numEnd ? pointAt : numEnd;
        var lastIntDigit = tokens.FindLastIndex(intEnd - 1, intEnd, t => t.Kind == Tk.Digit);
        var firstDigit = tokens.FindIndex(t => t.Kind == Tk.Digit);
        var thousands = false; var scale = 0;
        for (var i = 0; i < intEnd; i++)
            if (tokens[i].Kind == Tk.Comma)
            {
                if (i > lastIntDigit && lastIntDigit >= 0) scale++;
                else if (i > firstDigit && firstDigit >= 0) thousands = true;
            }
        for (var i = 0; i < scale; i++) v /= 1000;

        if (slashAt >= 0) return Fraction(tokens, v, neg, slashAt);

        var intTok = Enumerable.Range(0, intEnd).Where(i => tokens[i].Kind == Tk.Digit).ToList();
        var fracTok = pointAt >= 0 ? Enumerable.Range(pointAt + 1, numEnd - pointAt - 1).Where(i => tokens[i].Kind == Tk.Digit).ToList() : [];
        var exp = 0;
        if (expAt >= 0 && v != 0)
        {
            var step = Math.Max(1, intTok.Count(i => tokens[i].Text != "0") > 0 && intTok.Count > 1 ? intTok.Count : 1); // „##0.0E+0“: Ingenieursschreibweise in 3er-Schritten
            exp = (int)Math.Floor(Math.Log10(v));
            if (step > 1) exp = (int)Math.Floor(exp / (double)step) * step;
            v /= Math.Pow(10, exp);
        }
        // wie Excel: auf 15 Stellen dezimal runden (1,2345 → 1,235 und nicht 1,234 wegen der binären Darstellung)
        v = v < 1e15 && fracTok.Count <= 15 ? (double)Math.Round((decimal)v, fracTok.Count, MidpointRounding.AwayFromZero) : Math.Round(v, Math.Min(15, fracTok.Count), MidpointRounding.AwayFromZero);
        if (expAt >= 0 && v >= 10 && intTok.Count <= 1) { v /= 10; exp++; }
        var digits = Math.Floor(v).ToString("0", CultureInfo.InvariantCulture);
        if (digits == "0") digits = "";
        var frac = fracTok.Count > 0 ? Math.Round((v - Math.Floor(v)) * Math.Pow(10, fracTok.Count)).ToString("0", CultureInfo.InvariantCulture).PadLeft(fracTok.Count, '0') : "";
        if (frac.Length > fracTok.Count) { frac = new string('0', fracTok.Count); digits = (Math.Floor(v) + 1).ToString("0", CultureInfo.InvariantCulture); }

        // Ganzzahlteil von rechts nach links in die Platzhalter legen; der linke bekommt alle übrigen Ziffern
        var outInt = new string[intEnd]; var di = digits.Length; var placed = 0;
        string Group(string d, int posFromRight)
        {
            if (!thousands) return d;
            var sb = new StringBuilder();
            for (var k = 0; k < d.Length; k++)
            {
                var pos = posFromRight + (d.Length - 1 - k);
                sb.Append(d[k]);
                if (pos > 0 && pos % 3 == 0) sb.Append('.');
            }
            return sb.ToString();
        }
        for (var n = intTok.Count - 1; n >= 0; n--)
        {
            var ti = intTok[n]; var ph = tokens[ti].Text[0];
            string piece;
            if (n == 0) { var rest = digits[..Math.Max(0, di)]; piece = rest.Length > 0 ? Group(rest, placed) : ph == '0' ? Group("0", placed) : ph == '?' ? " " : ""; placed += rest.Length; di = 0; }
            else if (di > 0) { piece = Group(digits[di - 1].ToString(), placed); di--; placed++; }
            else { piece = ph == '0' ? Group("0", placed) : ph == '?' ? " " : ""; if (ph == '0') placed++; }
            outInt[ti] = piece;
        }
        var outFrac = new Dictionary<int, string>();
        var keepUntil = frac.Length; // „#“ hinten: Nullen weglassen
        for (var n = fracTok.Count - 1; n >= 0; n--) { if (tokens[fracTok[n]].Text == "0" || frac[n] != '0') break; keepUntil = n; }
        for (var n = 0; n < fracTok.Count; n++)
        {
            var ph = tokens[fracTok[n]].Text[0];
            outFrac[fracTok[n]] = n < keepUntil || ph == '0' ? frac[n].ToString() : ph == '?' ? " " : "";
        }
        var sbOut = new StringBuilder();
        if (neg && !(digits.Length == 0 && frac.All(c => c == '0') && expAt < 0)) sbOut.Append('-');
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (i < intEnd && t.Kind == Tk.Digit) sbOut.Append(outInt[i]);
            else if (t.Kind == Tk.Digit && outFrac.TryGetValue(i, out var f)) sbOut.Append(f);
            else if (t.Kind == Tk.Point && i == pointAt) sbOut.Append(',');
            else if (t.Kind == Tk.Percent) sbOut.Append('%');
            else if (t.Kind == Tk.Exp)
            {
                var expDigits = tokens.Skip(i + 1).Count(x => x.Kind == Tk.Digit);
                sbOut.Append('E').Append(exp < 0 ? "-" : t.Text == "+" ? "+" : "").Append(Math.Abs(exp).ToString(new string('0', Math.Max(1, expDigits)), CultureInfo.InvariantCulture));
                for (var k = i + 1; k < tokens.Count; k++) if (tokens[k].Kind != Tk.Digit) sbOut.Append(tokens[k].Kind == Tk.Text ? tokens[k].Text : "");
                break;
            }
            else if (t.Kind == Tk.Text) sbOut.Append(t.Text);
        }
        return sbOut.ToString();
    }

    static string Fraction(List<Tok> tokens, double v, bool neg, int slashAt)
    {
        var denTok = tokens.Skip(slashAt + 1).TakeWhile(t => t.Kind == Tk.Digit).ToList();
        var fixedDen = int.TryParse(string.Concat(tokens.Skip(slashAt + 1).TakeWhile(t => t.Kind is Tk.Digit or Tk.Text && char.IsDigit(t.Text.FirstOrDefault())).Select(t => t.Text)), out var fd) && fd > 0 && denTok.All(t => t.Text != "?" && t.Text != "#") ? fd : 0;
        var maxDen = fixedDen > 0 ? fixedDen : (int)Math.Pow(10, Math.Max(1, denTok.Count)) - 1;
        // Ganzzahl vorn nur, wenn vor dem Zähler eine eigene Ziffergruppe steht („# ?/?“)
        var numStart = slashAt - 1; while (numStart > 0 && tokens[numStart - 1].Kind == Tk.Digit) numStart--;
        var hasWhole = tokens.Take(numStart).Any(t => t.Kind == Tk.Digit);
        var whole = hasWhole ? Math.Floor(v) : 0; var rest = v - whole;
        int bestN = 0, bestD = 1; var bestErr = double.MaxValue;
        if (fixedDen > 0) { bestD = fixedDen; bestN = (int)Math.Round(rest * fixedDen); }
        else
            for (var d = 1; d <= maxDen; d++) { var n = (int)Math.Round(rest * d); var err = Math.Abs(rest - n / (double)d); if (err < bestErr - 1e-12) { bestErr = err; bestN = n; bestD = d; } }
        if (bestN == bestD && hasWhole) { whole++; bestN = 0; }
        var sb = new StringBuilder(); if (neg) sb.Append('-');
        if (hasWhole && whole > 0) sb.Append(whole.ToString("0", CultureInfo.InvariantCulture)).Append(' ');
        if (bestN > 0 || !hasWhole || whole == 0) sb.Append(bestN.ToString(CultureInfo.InvariantCulture)).Append('/').Append(bestD.ToString(CultureInfo.InvariantCulture));
        return sb.ToString().TrimEnd();
    }

    // ---------------- Datum und Uhrzeit ----------------
    static string Dates(List<Tok> tokens, double x)
    {
        if (x < 0 || x >= 2958466) return new string('#', 6);
        var ampm = tokens.Any(t => t.Text is "AM/PM" or "A/P");
        var fracSec = tokens.FirstOrDefault(t => t.Kind == Tk.Date && t.Text.StartsWith('.')).Text?.Length - 1 ?? 0;
        var secs = Math.Round(x * 86400 * Math.Pow(10, fracSec)) / Math.Pow(10, fracSec); // auf ganze (Bruchteile von) Sekunden runden, wie Excel
        var dt = new DateTime(1899, 12, 30).AddTicks((long)Math.Round(secs * TimeSpan.TicksPerSecond));
        var sb = new StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Kind != Tk.Date) { sb.Append(t.Kind switch { Tk.Text => t.Text, Tk.Point => ".", Tk.Comma => ",", Tk.Slash => ".", Tk.Digit => t.Text, Tk.Percent => "%", _ => "" }); continue; }
            var s = t.Text;
            // „m“ ist Minute, wenn ein „h“ davor oder ein „s“ danach steht
            bool Minute()
            {
                for (var k = i - 1; k >= 0; k--) if (tokens[k].Kind == Tk.Date) return tokens[k].Text[0] is 'h' or '[';
                for (var k = i + 1; k < tokens.Count; k++) if (tokens[k].Kind == Tk.Date) return tokens[k].Text[0] == 's';
                return false;
            }
            sb.Append(s switch
            {
                "[h]" or "[hh]" => Math.Floor(x * 24).ToString(s.Length > 3 ? "00" : "0", CultureInfo.InvariantCulture),
                "[m]" or "[mm]" => Math.Floor(x * 1440).ToString(s.Length > 3 ? "00" : "0", CultureInfo.InvariantCulture),
                "[s]" or "[ss]" => Math.Floor(x * 86400).ToString(s.Length > 3 ? "00" : "0", CultureInfo.InvariantCulture),
                "AM/PM" => dt.Hour < 12 ? "AM" : "PM",
                "A/P" => dt.Hour < 12 ? "A" : "P",
                _ when s.StartsWith('.') => dt.ToString("." + new string('f', s.Length - 1), CultureInfo.InvariantCulture).Replace('.', ','),
                _ => s[0] switch
                {
                    'y' => s.Length <= 2 ? dt.ToString("yy", De) : dt.ToString("yyyy", De),
                    'd' => s.Length switch { 1 => dt.Day.ToString(De), 2 => dt.ToString("dd", De), 3 => dt.ToString("ddd", De), _ => dt.ToString("dddd", De) },
                    'h' => (ampm ? (dt.Hour % 12 == 0 ? 12 : dt.Hour % 12) : dt.Hour).ToString(s.Length > 1 ? "00" : "0", De),
                    's' => dt.Second.ToString(s.Length > 1 ? "00" : "0", De),
                    'm' when s.Length <= 2 && Minute() => dt.Minute.ToString(s.Length > 1 ? "00" : "0", De),
                    'm' => s.Length switch { 1 => dt.Month.ToString(De), 2 => dt.ToString("MM", De), 3 => De.DateTimeFormat.GetAbbreviatedMonthName(dt.Month).TrimEnd('.'), 5 => De.DateTimeFormat.GetMonthName(dt.Month)[..1], _ => De.DateTimeFormat.GetMonthName(dt.Month) },
                    _ => s,
                },
            });
        }
        return sb.ToString();
    }
}
