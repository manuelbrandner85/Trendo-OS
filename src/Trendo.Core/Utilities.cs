using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>X-09: Was belegt gerade Arbeitsspeicher? Nur Lesen – Trendo beendet keine fremden Programme.</summary>
public sealed record ProcInfo(string Name, int Count, long Bytes, string? Studio);

public static class ResourceMonitor
{
    public static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ollama"] = "KI", ["ollama app"] = "KI", ["blender"] = "3D Studio", ["UnrealEditor"] = "Game Studio", ["soffice"] = "Office", ["soffice.bin"] = "Office",
        ["mpv"] = "Medien", ["kdenlive"] = "Medien", ["TrendoOS"] = "Trendo OS", ["Code"] = "Entwickler", ["SumatraPDF"] = "PDF",
    };

    public static IReadOnlyList<ProcInfo> Group(IEnumerable<(string Name, long Bytes)> procs, int top = 12) =>
        procs.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProcInfo(g.Key, g.Count(), g.Sum(x => x.Bytes), Known.GetValueOrDefault(g.Key)))
            .OrderByDescending(p => p.Bytes).Take(top).ToList();

    public static IReadOnlyList<ProcInfo> Snapshot(int top = 12)
    {
        var list = new List<(string, long)>();
        foreach (var p in Process.GetProcesses()) { try { list.Add((p.ProcessName, p.WorkingSet64)); } catch { } finally { p.Dispose(); } }
        return Group(list, top);
    }

    public static string Size(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" : $"{b / (double)(1L << 20):0} MB";
}

/// <summary>SYS-02: Notizen (notizen.json im Benutzerdatenordner).</summary>
public sealed record Note(string Id, string Text, DateTime Changed);

public sealed class NoteStore(string dataRoot)
{
    private string File_ => Path.Combine(dataRoot, "notizen.json");
    public List<Note> All() { try { return File.Exists(File_) ? JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(File_)) ?? [] : []; } catch (JsonException) { return []; } }
    private void Save(List<Note> l) { var t = File_ + ".tmp"; File.WriteAllText(t, JsonSerializer.Serialize(l)); File.Move(t, File_, true); }
    public Note Add(string text) { var l = All(); var n = new Note(Guid.NewGuid().ToString("N")[..8], text.Trim(), DateTime.Now); l.Insert(0, n); Save(l); return n; }
    public void Update(string id, string text) { var l = All(); var i = l.FindIndex(n => n.Id == id); if (i >= 0) { l[i] = l[i] with { Text = text.Trim(), Changed = DateTime.Now }; Save(l); } }
    public void Delete(string id) { var l = All(); l.RemoveAll(n => n.Id == id); Save(l); }
}

/// <summary>SYS-02 / N11: Rechner – + − × ÷ ^ %, Klammern, Komma oder Punkt; wissenschaftlich mit Funktionen (sin, cos, tan, asin, acos, atan, sqrt/√, ln, log, abs, exp), n!, π, e
/// und einer Variablen x für die Grafikansicht. Winkel in Grad oder Bogenmaß. Kein eval, eigener Parser.</summary>
public static class Calc
{
    public static readonly string[] Functions = ["sin", "cos", "tan", "asin", "acos", "atan", "sqrt", "ln", "log", "abs", "exp", "round"];

    public static double Eval(string expr) => Eval(expr, true, null);

    public static double Eval(string expr, bool degrees, double? x)
    {
        var s = expr.Replace(" ", "").Replace("×", "*").Replace("÷", "/").Replace("−", "-").Replace("√", "sqrt").Replace("π", "pi").Replace(',', '.').ToLowerInvariant();
        var i = 0; var v = Sum(); if (i != s.Length) throw new FormatException($"Unerwartetes Zeichen „{s[i]}“");
        return v;
        double Sum() { var a = Prod(); while (i < s.Length && s[i] is '+' or '-') { var o = s[i++]; var b = Prod(); a = o == '+' ? a + b : a - b; } return a; }
        double Prod()
        {
            var a = Pow();
            while (i < s.Length && (s[i] is '*' or '/' || char.IsLetter(s[i]) || s[i] == '('))
            {
                var o = s[i] is '*' or '/' ? s[i++] : '*'; // 2x, 2pi, 3(4+1): Malzeichen darf fehlen
                var b = Pow(); if (o == '/' && b == 0) throw new DivideByZeroException("Teilen durch 0 geht nicht");
                a = o == '*' ? a * b : a / b;
            }
            return a;
        }
        double Pow() { var a = Unary(); if (i < s.Length && s[i] == '^') { i++; a = Math.Pow(a, Pow()); } return a; }
        double Unary() { if (i < s.Length && s[i] == '-') { i++; return -Pow(); } if (i < s.Length && s[i] == '+') { i++; return Pow(); } return Post(Atom()); }
        double Post(double a)
        {
            while (i < s.Length && s[i] is '%' or '!')
                if (s[i++] == '%') a /= 100;
                else { if (a < 0 || a != Math.Floor(a) || a > 170) throw new FormatException("Fakultät nur für ganze Zahlen von 0 bis 170"); var f = 1.0; for (var k = 2; k <= a; k++) f *= k; a = f; }
            return a;
        }
        double Atom()
        {
            if (i < s.Length && s[i] == '(') { i++; var a = Sum(); if (i >= s.Length || s[i] != ')') throw new FormatException("Klammer nicht geschlossen"); i++; return a; }
            if (i < s.Length && char.IsLetter(s[i]))
            {
                var st = i; while (i < s.Length && char.IsLetter(s[i])) i++;
                var name = s[st..i];
                switch (name)
                {
                    case "pi": return Math.PI;
                    case "e": return Math.E;
                    case "x": return x ?? throw new FormatException("x ist nur in der Grafikansicht erlaubt");
                }
                if (!Functions.Contains(name)) throw new FormatException($"Unbekannte Funktion „{name}“");
                var arg = Unary();
                double Rad(double d) => degrees ? d * Math.PI / 180 : d;
                double Deg(double r) => degrees ? r * 180 / Math.PI : r;
                return name switch
                {
                    "sin" => Clean(Math.Sin(Rad(arg))), "cos" => Clean(Math.Cos(Rad(arg))), "tan" => Tan(Rad(arg)),
                    "asin" => Deg(Math.Asin(arg)), "acos" => Deg(Math.Acos(arg)), "atan" => Deg(Math.Atan(arg)),
                    "sqrt" => arg < 0 ? throw new FormatException("Keine Wurzel aus negativen Zahlen") : Math.Sqrt(arg),
                    "ln" => arg <= 0 ? throw new FormatException("Logarithmus nur für Zahlen größer 0") : Math.Log(arg),
                    "log" => arg <= 0 ? throw new FormatException("Logarithmus nur für Zahlen größer 0") : Math.Log10(arg),
                    "abs" => Math.Abs(arg), "exp" => Math.Exp(arg), _ => Math.Round(arg, MidpointRounding.AwayFromZero),
                };
            }
            var st2 = i; while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (st2 == i) throw new FormatException("Zahl erwartet");
            if (i < s.Length && s[i] == 'e' && i + 1 < s.Length && (char.IsDigit(s[i + 1]) || s[i + 1] is '-' or '+')) { i += 2; while (i < s.Length && char.IsDigit(s[i])) i++; }
            return double.Parse(s[st2..i], NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        static double Clean(double v) => Math.Abs(v) < 1e-12 ? 0 : v;
        static double Tan(double r) { var c = Math.Cos(r); if (Math.Abs(c) < 1e-12) throw new FormatException("Tangens ist hier nicht definiert"); return Clean(Math.Sin(r) / c); }
    }

    /// <summary>Grafikansicht: Funktion f(x) im Bereich abtasten. Nicht definierte Stellen werden NaN (Lücke in der Kurve).</summary>
    public static List<(double X, double Y)> Plot(string expr, double from, double to, int n = 400, bool degrees = false)
    {
        if (!(to > from)) throw new ArgumentException("Der Bereich muss von klein nach groß gehen.");
        Eval(expr, degrees, from + (to - from) / 3); // Fehler im Ausdruck sofort melden
        var res = new List<(double, double)>(n + 1);
        for (var k = 0; k <= n; k++)
        {
            var xv = from + (to - from) * k / n;
            double y; try { y = Eval(expr, degrees, xv); } catch (Exception e) when (e is FormatException or DivideByZeroException) { y = double.NaN; }
            res.Add((xv, double.IsFinite(y) ? y : double.NaN));
        }
        return res;
    }

    public static string Format(double v) => Math.Abs(v) is >= 1e15 or (> 0 and < 1e-9) ? v.ToString("0.##########E+0", CultureInfo.GetCultureInfo("de-DE")) : Math.Round(v, 10).ToString("#,##0.##########", CultureInfo.GetCultureInfo("de-DE"));
}

/// <summary>N11 Programmierer-Rechner: 64-Bit-Ganzzahlen in HEX/DEC/OCT/BIN, Bit-Operationen.</summary>
public static class ProgCalc
{
    public static long Parse(string text, int radix)
    {
        var t = text.Replace(" ", "").Replace("_", "").Replace(".", "");
        if (t.Length == 0) return 0;
        var neg = t.StartsWith('-'); if (neg) t = t[1..];
        if (radix == 16 && t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        if (radix == 2 && t.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        try { var v = radix == 10 ? long.Parse(t, CultureInfo.InvariantCulture) : (long)Convert.ToUInt64(t, radix); return neg ? -v : v; }
        catch (Exception e) when (e is FormatException or ArgumentException or OverflowException) { throw new FormatException($"„{text}“ ist keine gültige Zahl im {Name(radix)}-System."); }
    }

    public static string Name(int radix) => radix switch { 16 => "Hexadezimal", 8 => "Oktal", 2 => "Binär", _ => "Dezimal" };

    public static string Show(long v, int radix) => radix switch
    {
        10 => v.ToString(CultureInfo.InvariantCulture),
        16 => v.ToString("X"),
        8 => Convert.ToString(v, 8),
        _ => Group(Convert.ToString(v, 2)),
    };
    static string Group(string bin) { bin = bin.PadLeft((bin.Length + 3) / 4 * 4, '0'); return string.Join(' ', Enumerable.Range(0, bin.Length / 4).Select(k => bin.Substring(k * 4, 4))); }

    public static long Apply(string op, long a, long b) => op switch
    {
        "AND" => a & b, "OR" => a | b, "XOR" => a ^ b, "NAND" => ~(a & b), "NOR" => ~(a | b),
        "<<" => a << (int)b, ">>" => a >> (int)b, "+" => a + b, "-" => a - b, "*" => a * b,
        "/" => b == 0 ? throw new DivideByZeroException("Teilen durch 0 geht nicht") : a / b,
        "MOD" => b == 0 ? throw new DivideByZeroException("Teilen durch 0 geht nicht") : a % b,
        _ => throw new ArgumentException("Unbekannte Rechenart " + op),
    };
}

/// <summary>N11 Datumsrechner: Abstand zweier Tage und Tage addieren.</summary>
public static class DateCalc
{
    public static (int Years, int Months, int Days, int TotalDays, int Workdays) Between(DateTime a, DateTime b)
    {
        if (b < a) (a, b) = (b, a);
        a = a.Date; b = b.Date;
        var m = (b.Year - a.Year) * 12 + b.Month - a.Month; if (a.AddMonths(m) > b) m--; // wie Kalender: 31.01. + 1 Monat = 28.02.
        var d = (b - a.AddMonths(m)).Days; var y = m / 12; m %= 12;
        var work = 0; for (var t = a; t < b; t = t.AddDays(1)) if (t.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) work++;
        return (y, m, d, (b - a).Days, work);
    }
    public static DateTime Add(DateTime d, int years, int months, int days) => d.AddYears(years).AddMonths(months).AddDays(days);
}

/// <summary>SYS-02 Medien / SYS-10 Office / SYS-03 Downloads: Dateien eines Ordners nach Art, neueste zuerst.</summary>
public static class FileShelf
{
    public static readonly string[] Media = [".mp3", ".m4a", ".flac", ".wav", ".ogg", ".mp4", ".mkv", ".mov", ".avi", ".webm"];
    public static readonly string[] Office = [".docx", ".doc", ".odt", ".xlsx", ".xls", ".ods", ".pptx", ".odp", ".pdf", ".txt", ".md", ".csv", ".rtf"];

    public static IReadOnlyList<FileInfo> Recent(IEnumerable<string> folders, string[]? ext, int max = 40, int depth = 2)
    {
        var res = new List<FileInfo>();
        foreach (var f in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var q = new Queue<(string, int)>(); q.Enqueue((f, 0));
            while (q.Count > 0 && res.Count < 5000)
            {
                var (d, lv) = q.Dequeue();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(d))
                        if (ext is null || ext.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) res.Add(new FileInfo(file));
                    if (lv < depth) foreach (var s in Directory.EnumerateDirectories(d)) if (!Path.GetFileName(s).StartsWith('.')) q.Enqueue((s, lv + 1));
                }
                catch (UnauthorizedAccessException) { } catch (IOException) { }
            }
        }
        return res.Where(f => !f.Name.StartsWith("~$")).OrderByDescending(f => f.LastWriteTimeUtc).Take(max).ToList();
    }

    public static string Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".docx" or ".doc" or ".odt" or ".rtf" or ".txt" or ".md" => "Text", ".xlsx" or ".xls" or ".ods" or ".csv" => "Tabelle", ".pptx" or ".odp" => "Präsentation", ".pdf" => "PDF",
        ".mp3" or ".m4a" or ".flac" or ".wav" or ".ogg" => "Musik", ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm" => "Video",
        ".exe" or ".msi" => "Programm", ".zip" or ".7z" or ".rar" => "Archiv", ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => "Bild", _ => "Datei",
    };
}
