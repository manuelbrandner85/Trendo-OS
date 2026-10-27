using System.Globalization;
using System.Text;

namespace Trendo.Core.Office;

/// <summary>Ergebnis einer Zelle: leer, Zahl, Text, Wahrheitswert oder Fehler (#DIV/0!, #WERT!, #NAME?, #BEZUG!, #ZIRKEL!, #NV).</summary>
public readonly record struct CellValue(CellValue.K Kind, double Number, string Text)
{
    public enum K { Empty, Number, Text, Bool, Error }
    public static readonly CellValue Empty = new(K.Empty, 0, "");
    public static CellValue Num(double d) => double.IsNaN(d) || double.IsInfinity(d) ? Err("#ZAHL!") : new(K.Number, d, "");
    public static CellValue Str(string s) => new(K.Text, 0, s);
    public static CellValue Bool(bool b) => new(K.Bool, b ? 1 : 0, "");
    public static CellValue Err(string code) => new(K.Error, 0, code);
    public bool IsEmpty => Kind == K.Empty;
    public bool IsNumber => Kind is K.Number;
    public bool IsError => Kind == K.Error;
    public override string ToString() => Kind switch { K.Number => Number.ToString("R", CultureInfo.InvariantCulture), K.Bool => Number != 0 ? "WAHR" : "FALSCH", K.Empty => "", _ => Text };
}

/// <summary>Rechnet Zellen einer Mappe aus (C3). Ergebnisse werden je Berechnung zwischengespeichert; Zirkelbezüge ergeben #ZIRKEL!.</summary>
public sealed partial class SheetCalc(SheetDoc doc)
{
    readonly Dictionary<(Sheet, int, int), CellValue> _memo = [];
    readonly HashSet<(Sheet, int, int)> _busy = [];
    /// <summary>Für Tests: fester „heute“-Wert.</summary>
    public DateTime Now { get; init; } = DateTime.Now;

    public SheetDoc Doc => doc;
    public void Invalidate() => _memo.Clear();

    public CellValue Value(Sheet s, string a1) { var (r, c) = Addr.Parse(a1); return Value(s, r, c); }

    public CellValue Value(Sheet s, int row, int col)
    {
        var key = (s, row, col);
        if (_memo.TryGetValue(key, out var v)) return v;
        var cell = s.Get(row, col);
        if (cell is null || cell.Raw.Length == 0) return CellValue.Empty;
        if (!cell.IsFormula) return _memo[key] = Literal(cell.Raw, cell.Format).Value;
        if (!_busy.Add(key)) return CellValue.Err("#ZIRKEL!");
        try
        {
            CellValue result;
            try { result = new Evaluator(this, s) { Row = row, Col = col }.Eval(Formula.Parse(cell.Raw[1..], Formula.Dialect.De)); }
            catch (FormulaError e) { result = CellValue.Err(e.Code); }
            return _memo[key] = result.Kind == CellValue.K.Empty ? CellValue.Num(0) : result;
        }
        finally { _busy.Remove(key); }
    }

    /// <summary>Anzeige wie in der Zelle (C5), deutsch formatiert.</summary>
    public string Display(Sheet s, int row, int col) => SheetFormat.Display(Value(s, row, col), s.Get(row, col)?.Format ?? NumFmt.Standard);

    /// <summary>Eingabe ohne „=“ deuten: Zahl (deutsch), Prozent, Euro, Datum, WAHR/FALSCH oder Text. Liefert das passende Format als Vorschlag.</summary>
    public static (CellValue Value, NumFmt? Suggest) Literal(string raw, NumFmt fmt = NumFmt.Standard)
    {
        if (raw.StartsWith('\'')) return (CellValue.Str(raw[1..]), null);
        if (fmt == NumFmt.Text) return (CellValue.Str(raw), null);
        var t = raw.Trim();
        if (t.Length == 0) return (CellValue.Empty, null);
        if (t.Equals("WAHR", StringComparison.OrdinalIgnoreCase) || t.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return (CellValue.Bool(true), null);
        if (t.Equals("FALSCH", StringComparison.OrdinalIgnoreCase) || t.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return (CellValue.Bool(false), null);
        if (t.EndsWith('%') && SheetFormat.TryNumber(t[..^1], out var p)) return (CellValue.Num(p / 100), NumFmt.Prozent);
        if (t.EndsWith('€') && SheetFormat.TryNumber(t[..^1], out var e)) return (CellValue.Num(e), NumFmt.Euro);
        if (t.StartsWith('€') && SheetFormat.TryNumber(t[1..], out var e2)) return (CellValue.Num(e2), NumFmt.Euro);
        if (SheetFormat.TryNumber(t, out var n)) return (CellValue.Num(n), null);
        if (DateTime.TryParseExact(t, ["d.M.yyyy", "d.M.yy", "yyyy-MM-dd", "d.M.yyyy H:mm", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var d))
            return (CellValue.Num(SheetFormat.ToSerial(d)), d.TimeOfDay == TimeSpan.Zero ? NumFmt.Datum : NumFmt.DatumZeit);
        return (CellValue.Str(raw), null);
    }

    sealed partial class Evaluator(SheetCalc calc, Sheet sheet)
    {
        public CellValue Eval(Formula.Node n) => n switch
        {
            Formula.Num x => CellValue.Num(x.Value),
            Formula.Str x => CellValue.Str(x.Value),
            Formula.Bool x => CellValue.Bool(x.Value),
            Formula.ErrNode x => CellValue.Err(x.Code),
            Formula.Ref x => calc.Value(SheetOf(x.Sheet), x.Row, x.Col),
            Formula.RangeRef => throw new FormulaError("#WERT!"),
            Formula.Unary x => Unary(x),
            Formula.Binary x => Binary(x),
            Formula.Call x => Call(x),
            _ => throw new FormulaError("#WERT!"),
        };

        Sheet SheetOf(string? name) => name is null ? sheet : calc.Doc.Find(name) ?? throw new FormulaError("#BEZUG!");

        CellValue Unary(Formula.Unary u)
        {
            var v = Eval(u.Arg);
            if (v.IsError) return v;
            var d = ToNumber(v);
            return u.Op switch { '-' => CellValue.Num(-d), '%' => CellValue.Num(d / 100), _ => CellValue.Num(d) };
        }

        CellValue Binary(Formula.Binary b)
        {
            var l = Eval(b.Left); if (l.IsError) return l;
            var r = Eval(b.Right); if (r.IsError) return r;
            switch (b.Op)
            {
                case "&": return CellValue.Str(ToText(l) + ToText(r));
                case "=" or "<>" or "<" or ">" or "<=" or ">=":
                    var cmp = Compare(l, r);
                    return CellValue.Bool(b.Op switch { "=" => cmp == 0, "<>" => cmp != 0, "<" => cmp < 0, ">" => cmp > 0, "<=" => cmp <= 0, _ => cmp >= 0 });
            }
            var x = ToNumber(l); var y = ToNumber(r);
            return b.Op switch
            {
                "+" => CellValue.Num(x + y),
                "-" => CellValue.Num(x - y),
                "*" => CellValue.Num(x * y),
                "/" => y == 0 ? CellValue.Err("#DIV/0!") : CellValue.Num(x / y),
                "^" => CellValue.Num(Math.Pow(x, y)),
                _ => throw new FormulaError("#WERT!"),
            };
        }

        static int Compare(CellValue a, CellValue b)
        {
            if (a.IsEmpty && b.IsEmpty) return 0;
            if ((a.IsNumber || a.Kind == CellValue.K.Bool || a.IsEmpty) && (b.IsNumber || b.Kind == CellValue.K.Bool || b.IsEmpty)) return a.Number.CompareTo(b.Number);
            if (a.IsNumber && b.Kind == CellValue.K.Text) return -1;
            if (a.Kind == CellValue.K.Text && b.IsNumber) return 1;
            return string.Compare(ToText(a), ToText(b), StringComparison.CurrentCultureIgnoreCase);
        }

        public static double ToNumber(CellValue v) => v.Kind switch
        {
            CellValue.K.Number or CellValue.K.Bool => v.Number,
            CellValue.K.Empty => 0,
            CellValue.K.Text => SheetFormat.TryNumber(v.Text, out var d) ? d : throw new FormulaError("#WERT!"),
            _ => throw new FormulaError(v.Text),
        };

        public static string ToText(CellValue v) => v.Kind switch
        {
            CellValue.K.Number => SheetFormat.Display(v, NumFmt.Standard),
            CellValue.K.Bool => v.Number != 0 ? "WAHR" : "FALSCH",
            _ => v.Text,
        };

        static bool ToBool(CellValue v) => v.Kind switch
        {
            CellValue.K.Bool or CellValue.K.Number => v.Number != 0,
            CellValue.K.Empty => false,
            CellValue.K.Text => v.Text.Equals("WAHR", StringComparison.OrdinalIgnoreCase) || v.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
                ? true : v.Text.Equals("FALSCH", StringComparison.OrdinalIgnoreCase) || v.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase) ? false : throw new FormulaError("#WERT!"),
            _ => throw new FormulaError(v.Text),
        };

        /// <summary>Alle Werte eines Arguments: Bereich → jede Zelle, sonst der eine Wert.</summary>
        IEnumerable<CellValue> Values(Formula.Node n)
        {
            if (n is Formula.RangeRef rr)
            {
                var s = SheetOf(rr.Sheet);
                var bottom = Math.Min(rr.Bottom, Math.Max(rr.Top, s.MaxRow)); var right = Math.Min(rr.Right, Math.Max(rr.Left, s.MaxCol));
                for (var r = rr.Top; r <= bottom; r++) for (var c = rr.Left; c <= right; c++) yield return calc.Value(s, r, c);
                yield break;
            }
            yield return Eval(n);
        }

        /// <summary>Zahlen für SUMME &amp; Co.: Text und leere Zellen in Bereichen zählen nicht, Fehler schlagen durch.</summary>
        List<double> Numbers(IEnumerable<Formula.Node> args)
        {
            var list = new List<double>();
            foreach (var a in args)
                foreach (var v in Values(a))
                {
                    if (v.IsError) throw new FormulaError(v.Text);
                    if (v.IsNumber) list.Add(v.Number);
                    else if (a is not Formula.RangeRef && v.Kind is CellValue.K.Bool or CellValue.K.Text) list.Add(ToNumber(v));
                }
            return list;
        }

        CellValue Call(Formula.Call f)
        {
            var a = f.Args;
            void Need(int min, int max = int.MaxValue) { if (a.Count < min || a.Count > max) throw new FormulaError("#WERT!"); }
            double N(int i) => ToNumber(Eval(a[i]));
            string T(int i) => ToText(Eval(a[i]));
            switch (f.Name)
            {
                case "SUM": return CellValue.Num(Numbers(a).Sum());
                case "AVERAGE": { var l = Numbers(a); return l.Count == 0 ? CellValue.Err("#DIV/0!") : CellValue.Num(l.Average()); }
                case "MIN": { var l = Numbers(a); return CellValue.Num(l.Count == 0 ? 0 : l.Min()); }
                case "MAX": { var l = Numbers(a); return CellValue.Num(l.Count == 0 ? 0 : l.Max()); }
                case "COUNT": return CellValue.Num(a.SelectMany(Values).Count(v => v.IsNumber));
                case "COUNTA": return CellValue.Num(a.SelectMany(Values).Count(v => !v.IsEmpty));
                case "COUNTBLANK": return CellValue.Num(a.SelectMany(Values).Count(v => v.IsEmpty || v.Kind == CellValue.K.Text && v.Text.Length == 0));
                case "PRODUCT": return CellValue.Num(Numbers(a).Aggregate(1.0, (x, y) => x * y));
                case "MEDIAN": { var l = Numbers(a).OrderBy(x => x).ToList(); if (l.Count == 0) return CellValue.Err("#ZAHL!"); return CellValue.Num(l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2); }
                case "IF": { Need(1, 3); var c = Eval(a[0]); if (c.IsError) return c; return ToBool(c) ? (a.Count > 1 ? Eval(a[1]) : CellValue.Bool(true)) : (a.Count > 2 ? Eval(a[2]) : CellValue.Bool(false)); }
                case "IFERROR": { Need(2, 2); var v = Eval(a[0]); return v.IsError ? Eval(a[1]) : v; }
                case "AND": return CellValue.Bool(a.SelectMany(Values).Where(v => !v.IsEmpty).All(ToBool));
                case "OR": return CellValue.Bool(a.SelectMany(Values).Where(v => !v.IsEmpty).Any(ToBool));
                case "NOT": Need(1, 1); return CellValue.Bool(!ToBool(Eval(a[0])));
                case "ROUND": Need(1, 2); return CellValue.Num(RoundTo(N(0), a.Count > 1 ? (int)N(1) : 0, MidpointRounding.AwayFromZero));
                case "ROUNDUP": Need(1, 2); return CellValue.Num(RoundDir(N(0), a.Count > 1 ? (int)N(1) : 0, up: true));
                case "ROUNDDOWN": Need(1, 2); return CellValue.Num(RoundDir(N(0), a.Count > 1 ? (int)N(1) : 0, up: false));
                case "INT": Need(1, 1); return CellValue.Num(Math.Floor(N(0)));
                case "ABS": Need(1, 1); return CellValue.Num(Math.Abs(N(0)));
                case "SQRT": { Need(1, 1); var x = N(0); return x < 0 ? CellValue.Err("#ZAHL!") : CellValue.Num(Math.Sqrt(x)); }
                case "POWER": Need(2, 2); return CellValue.Num(Math.Pow(N(0), N(1)));
                case "MOD": { Need(2, 2); var d = N(1); if (d == 0) return CellValue.Err("#DIV/0!"); var x = N(0); return CellValue.Num(x - d * Math.Floor(x / d)); }
                case "PI": Need(0, 0); return CellValue.Num(Math.PI);
                case "TODAY": Need(0, 0); return CellValue.Num(SheetFormat.ToSerial(calc.Now.Date));
                case "NOW": Need(0, 0); return CellValue.Num(SheetFormat.ToSerial(calc.Now));
                case "DATE": { Need(3, 3); try { return CellValue.Num(SheetFormat.ToSerial(new DateTime((int)N(0), 1, 1).AddMonths((int)N(1) - 1).AddDays((int)N(2) - 1))); } catch (ArgumentOutOfRangeException) { return CellValue.Err("#ZAHL!"); } }
                case "YEAR": Need(1, 1); return CellValue.Num(SheetFormat.FromSerial(N(0)).Year);
                case "MONTH": Need(1, 1); return CellValue.Num(SheetFormat.FromSerial(N(0)).Month);
                case "DAY": Need(1, 1); return CellValue.Num(SheetFormat.FromSerial(N(0)).Day);
                case "WEEKDAY": Need(1, 1); return CellValue.Num((int)SheetFormat.FromSerial(N(0)).DayOfWeek + 1);
                case "CONCATENATE": return CellValue.Str(string.Concat(a.SelectMany(Values).Select(ToText)));
                case "LEN": Need(1, 1); return CellValue.Num(T(0).Length);
                case "LEFT": { Need(1, 2); var s = T(0); var k = a.Count > 1 ? (int)N(1) : 1; return k < 0 ? CellValue.Err("#WERT!") : CellValue.Str(s[..Math.Min(k, s.Length)]); }
                case "RIGHT": { Need(1, 2); var s = T(0); var k = a.Count > 1 ? (int)N(1) : 1; return k < 0 ? CellValue.Err("#WERT!") : CellValue.Str(s[Math.Max(0, s.Length - k)..]); }
                case "MID": { Need(3, 3); var s = T(0); var st = (int)N(1) - 1; var k = (int)N(2); if (st < 0 || k < 0) return CellValue.Err("#WERT!"); return CellValue.Str(st >= s.Length ? "" : s.Substring(st, Math.Min(k, s.Length - st))); }
                case "UPPER": Need(1, 1); return CellValue.Str(T(0).ToUpper(CultureInfo.GetCultureInfo("de-DE")).Replace("ß", "SS")); // wie Calc (ICU)
                case "LOWER": Need(1, 1); return CellValue.Str(T(0).ToLower(CultureInfo.GetCultureInfo("de-DE")));
                case "TRIM": Need(1, 1); return CellValue.Str(string.Join(' ', T(0).Split(' ', StringSplitOptions.RemoveEmptyEntries)));
                case "ISBLANK": Need(1, 1); return CellValue.Bool(Eval(a[0]).IsEmpty);
                case "ISNUMBER": Need(1, 1); return CellValue.Bool(Eval(a[0]).IsNumber);
                case "ISERROR": Need(1, 1); return CellValue.Bool(Eval(a[0]).IsError);
                case "SUMIF": case "COUNTIF": case "AVERAGEIF": return CondAgg(f.Name, a);
                case "VLOOKUP": return VLookup(a);
                default: return Extra(f.Name, a) ?? throw new FormulaError("#NAME?");
            }
        }

        static double RoundTo(double x, int digits, MidpointRounding m)
        {
            if (digits >= 0) return Math.Round(x, Math.Min(digits, 15), m);
            var f = Math.Pow(10, -digits); return Math.Round(x / f, m) * f;
        }

        static double RoundDir(double x, int digits, bool up)
        {
            var f = Math.Pow(10, digits); var y = Math.Abs(x) * f;
            y = up ? Math.Ceiling(y - 1e-9) : Math.Floor(y + 1e-9);
            return Math.Sign(x) * y / f;
        }

        /// <summary>Bedingung wie in Calc: „&gt;5“, „&lt;&gt;Miete“, „=0“, „Miete“ oder eine Zahl.</summary>
        static Func<CellValue, bool> Criterion(CellValue crit)
        {
            if (crit.IsNumber) return v => v.IsNumber && v.Number == crit.Number;
            var s = ToText(crit);
            foreach (var op in new[] { "<=", ">=", "<>", "<", ">", "=" })
                if (s.StartsWith(op, StringComparison.Ordinal))
                {
                    var rest = s[op.Length..];
                    if (SheetFormat.TryNumber(rest, out var n))
                        return v => v.IsNumber && op switch { "<=" => v.Number <= n, ">=" => v.Number >= n, "<>" => v.Number != n, "<" => v.Number < n, ">" => v.Number > n, _ => v.Number == n } || op == "<>" && !v.IsNumber;
                    return v => op == "<>" ? !string.Equals(ToText(v), rest, StringComparison.CurrentCultureIgnoreCase) : op == "=" && string.Equals(ToText(v), rest, StringComparison.CurrentCultureIgnoreCase);
                }
            if (SheetFormat.TryNumber(s, out var num)) return v => v.IsNumber && v.Number == num;
            return v => string.Equals(ToText(v), s, StringComparison.CurrentCultureIgnoreCase);
        }

        CellValue CondAgg(string name, IReadOnlyList<Formula.Node> a)
        {
            if (a.Count < 2 || a.Count > 3 || a[0] is not Formula.RangeRef range) throw new FormulaError("#WERT!");
            var test = Criterion(Eval(a[1]));
            var keys = Values(range).ToList();
            var vals = a.Count == 3 && a[2] is Formula.RangeRef sum ? Values(sum with { Bottom = sum.Top + (range.Bottom - range.Top), Right = sum.Left + (range.Right - range.Left) }).ToList() : keys;
            var hit = keys.Select((k, i) => (k, i)).Where(x => test(x.k)).Select(x => x.i < vals.Count ? vals[x.i] : CellValue.Empty).ToList();
            if (name == "COUNTIF") return CellValue.Num(hit.Count);
            var nums = hit.Where(v => v.IsNumber).Select(v => v.Number).ToList();
            if (name == "SUMIF") return CellValue.Num(nums.Sum());
            return nums.Count == 0 ? CellValue.Err("#DIV/0!") : CellValue.Num(nums.Average());
        }

        CellValue VLookup(IReadOnlyList<Formula.Node> a)
        {
            if (a.Count < 3 || a.Count > 4 || a[1] is not Formula.RangeRef t) throw new FormulaError("#WERT!");
            var key = Eval(a[0]); var col = (int)ToNumber(Eval(a[2])) - 1;
            var sorted = a.Count < 4 || ToBool(Eval(a[3]));
            if (col < 0 || t.Left + col > t.Right) return CellValue.Err("#BEZUG!");
            var s = SheetOf(t.Sheet); var bottom = Math.Min(t.Bottom, s.MaxRow);
            int? found = null;
            for (var r = t.Top; r <= bottom; r++)
            {
                var k = calc.Value(s, r, t.Left);
                var cmp = Compare(k, key);
                if (cmp == 0) { found = r; break; }
                if (sorted) { if (cmp < 0) found = r; else break; }
            }
            return found is int row ? calc.Value(s, row, t.Left + col) : CellValue.Err("#NV");
        }
    }
}

sealed class FormulaError(string code) : Exception(code) { public string Code { get; } = code; }

/// <summary>Anzeige und Eingabe von Zahlen auf Deutsch (C5).</summary>
public static class SheetFormat
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly DateTime Epoch = new(1899, 12, 30);

    public static double ToSerial(DateTime d) => (d - Epoch).TotalDays;
    public static DateTime FromSerial(double s) => Epoch.AddDays(s);

    /// <summary>Deutsche Zahl: „1.234,5“, „1234,5“, „-3“, auch „1234.5“ (mit Punkt als Dezimalzeichen, wenn kein Komma dabei ist).</summary>
    public static bool TryNumber(string s, out double d)
    {
        s = s.Trim().Replace(" ", "").Replace(" ", "");
        if (s.Length == 0) { d = 0; return false; }
        if (s.Contains(','))
            return double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowExponent, De, out d);
        // ohne Komma: „1.234.567“ ist Tausendertrennung, „1.5“ ein Dezimalpunkt
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^-?\d{1,3}(\.\d{3})+$")) return double.TryParse(s.Replace(".", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out d);
        return double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out d);
    }

    public static string Display(CellValue v, NumFmt f)
    {
        switch (v.Kind)
        {
            case CellValue.K.Empty: return "";
            case CellValue.K.Text: return v.Text;
            case CellValue.K.Error: return v.Text;
            case CellValue.K.Bool: return v.Number != 0 ? "WAHR" : "FALSCH";
        }
        var x = v.Number;
        return f switch
        {
            NumFmt.Zahl => x.ToString("#,##0.00", De),
            NumFmt.Ganzzahl => x.ToString("#,##0", De),
            NumFmt.Euro => x.ToString("#,##0.00", De) + " €",
            NumFmt.Prozent => (x * 100).ToString("#,##0.##", De) + " %",
            NumFmt.Datum => SafeDate(x, "dd.MM.yyyy"),
            NumFmt.DatumZeit => SafeDate(x, "dd.MM.yyyy HH:mm"),
            _ => General(x),
        };
    }

    static string SafeDate(double x, string fmt) { try { return FromSerial(x).ToString(fmt, De); } catch (ArgumentOutOfRangeException) { return "###"; } }

    /// <summary>„Standard“: bis zu 10 gültige Stellen, ohne überflüssige Nullen; sehr große/kleine Zahlen wissenschaftlich.</summary>
    public static string General(double x)
    {
        if (x == 0) return "0";
        var a = Math.Abs(x);
        if (a >= 1e11 || a < 1e-9) return x.ToString("0.#####E+00", De);
        return Math.Round(x, Math.Max(0, 10 - (int)Math.Floor(Math.Log10(a)) - 1)).ToString("0.##########", De);
    }
}
