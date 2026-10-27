using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Office;

/// <summary>T-0228 E4: weitere Tabellenfunktionen (die meistgenutzten aus Excel/Calc) – Namen deutsch wie in Excel, gespeichert englisch.</summary>
public static class SheetFunctionNames
{
    /// <summary>Englisch (XLSX) → Deutsch (Anzeige).</summary>
    public static readonly IReadOnlyDictionary<string, string> Extra = new Dictionary<string, string>
    {
        // Mathematik
        ["SUMIFS"] = "SUMMEWENNS", ["COUNTIFS"] = "ZÄHLENWENNS", ["AVERAGEIFS"] = "MITTELWERTWENNS", ["MAXIFS"] = "MAXWENNS", ["MINIFS"] = "MINWENNS",
        ["SUMPRODUCT"] = "SUMMENPRODUKT", ["SUMSQ"] = "QUADRATESUMME", ["CEILING"] = "OBERGRENZE", ["FLOOR"] = "UNTERGRENZE", ["MROUND"] = "VRUNDEN",
        ["TRUNC"] = "KÜRZEN", ["SIGN"] = "VORZEICHEN", ["EXP"] = "EXP", ["LN"] = "LN", ["LOG"] = "LOG", ["LOG10"] = "LOG10", ["RAND"] = "ZUFALLSZAHL",
        ["RANDBETWEEN"] = "ZUFALLSBEREICH", ["EVEN"] = "GERADE", ["ODD"] = "UNGERADE", ["FACT"] = "FAKULTÄT", ["GCD"] = "GGT", ["LCM"] = "KGV",
        ["QUOTIENT"] = "QUOTIENT", ["SIN"] = "SIN", ["COS"] = "COS", ["TAN"] = "TAN", ["ASIN"] = "ARCSIN", ["ACOS"] = "ARCCOS", ["ATAN"] = "ARCTAN",
        ["ATAN2"] = "ARCTAN2", ["RADIANS"] = "BOGENMASS", ["DEGREES"] = "GRAD", ["ROMAN"] = "RÖMISCH", ["COMBIN"] = "KOMBINATIONEN",
        // Statistik
        ["LARGE"] = "KGRÖSSTE", ["SMALL"] = "KKLEINSTE", ["RANK"] = "RANG", ["RANK.EQ"] = "RANG.GLEICH", ["STDEV"] = "STABW", ["STDEV.S"] = "STABW.S",
        ["STDEV.P"] = "STABW.N", ["VAR"] = "VARIANZ", ["VAR.S"] = "VAR.S", ["VAR.P"] = "VAR.P", ["MODE"] = "MODALWERT", ["PERCENTILE"] = "QUANTIL",
        ["QUARTILE"] = "QUARTILE", ["AVEDEV"] = "MITTELABW", ["GEOMEAN"] = "GEOMITTEL", ["HARMEAN"] = "HARMITTEL", ["CORREL"] = "KORREL",
        ["SLOPE"] = "STEIGUNG", ["INTERCEPT"] = "ACHSENABSCHNITT", ["RSQ"] = "BESTIMMTHEITSMASS", ["MAXA"] = "MAXA", ["MINA"] = "MINA", ["AVERAGEA"] = "MITTELWERTA",
        // Text
        ["TEXT"] = "TEXT", ["CONCAT"] = "TEXTKETTE", ["TEXTJOIN"] = "TEXTVERKETTEN", ["SUBSTITUTE"] = "WECHSELN", ["REPLACE"] = "ERSETZEN", ["FIND"] = "FINDEN",
        ["SEARCH"] = "SUCHEN", ["REPT"] = "WIEDERHOLEN", ["PROPER"] = "GROSS2", ["VALUE"] = "WERT", ["EXACT"] = "IDENTISCH", ["CLEAN"] = "SÄUBERN",
        ["CHAR"] = "ZEICHEN", ["CODE"] = "CODE", ["FIXED"] = "FEST", ["T"] = "T", ["NUMBERVALUE"] = "ZAHLENWERT",
        // Nachschlagen
        ["HLOOKUP"] = "WVERWEIS", ["LOOKUP"] = "VERWEIS", ["INDEX"] = "INDEX", ["MATCH"] = "VERGLEICH", ["XLOOKUP"] = "XVERWEIS", ["XMATCH"] = "XVERGLEICH",
        ["CHOOSE"] = "WAHL", ["ROW"] = "ZEILE", ["COLUMN"] = "SPALTE", ["ROWS"] = "ZEILEN", ["COLUMNS"] = "SPALTEN", ["INDIRECT"] = "INDIREKT", ["ADDRESS"] = "ADRESSE",
        // Datum und Zeit
        ["DATEDIF"] = "DATEDIF", ["WORKDAY"] = "ARBEITSTAG", ["NETWORKDAYS"] = "NETTOARBEITSTAGE", ["EDATE"] = "EDATUM", ["EOMONTH"] = "MONATSENDE",
        ["WEEKNUM"] = "KALENDERWOCHE", ["ISOWEEKNUM"] = "ISOKALENDERWOCHE", ["HOUR"] = "STUNDE", ["MINUTE"] = "MINUTE", ["SECOND"] = "SEKUNDE", ["TIME"] = "ZEIT",
        ["DATEVALUE"] = "DATWERT", ["TIMEVALUE"] = "ZEITWERT", ["DAYS"] = "TAGE", ["DAYS360"] = "TAGE360", ["YEARFRAC"] = "BRTEILJAHRE",
        // Logik und Info
        ["IFS"] = "WENNS", ["SWITCH"] = "ERSTERWERT", ["XOR"] = "XODER", ["IFNA"] = "WENNNV", ["ISTEXT"] = "ISTTEXT", ["ISLOGICAL"] = "ISTLOG", ["ISNA"] = "ISTNV",
        ["ISERR"] = "ISTFEHL", ["ISEVEN"] = "ISTGERADE", ["ISODD"] = "ISTUNGERADE", ["ISNONTEXT"] = "ISTKTEXT", ["NA"] = "NV", ["TYPE"] = "TYP", ["N"] = "N",
        // Finanzen
        ["PMT"] = "RMZ", ["FV"] = "ZW", ["PV"] = "BW", ["RATE"] = "ZINS", ["NPER"] = "ZZR", ["NPV"] = "NBW", ["IRR"] = "IKV", ["IPMT"] = "ZINSZ", ["PPMT"] = "KAPZ",
        ["EFFECT"] = "EFFEKTIV", ["NOMINAL"] = "NOMINAL", ["SLN"] = "LIA", ["SYD"] = "DIA", ["DDB"] = "GDA",
    };

    /// <summary>Neuere Excel-Funktionen stehen in XLSX mit Präfix „_xlfn.“.</summary>
    public static readonly HashSet<string> XlFn = ["MAXIFS", "MINIFS", "CONCAT", "TEXTJOIN", "XLOOKUP", "XMATCH", "IFS", "SWITCH", "XOR", "IFNA", "ISOWEEKNUM",
        "DAYS", "NUMBERVALUE", "STDEV.S", "STDEV.P", "VAR.S", "VAR.P", "RANK.EQ"];
}

public sealed partial class SheetCalc
{
    sealed partial class Evaluator
    {
        public int Row { get; init; }
        public int Col { get; init; }

        static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

        sealed record G(int Rows, int Cols, int EffRows, int EffCols, Func<int, int, CellValue> At, int Top, int Left)
        {
            public IEnumerable<CellValue> All() { for (var r = 0; r < EffRows; r++) for (var c = 0; c < EffCols; c++) yield return At(r, c); }
            public CellValue Flat(int i) => Cols == 1 ? At(i, 0) : Rows == 1 ? At(0, i) : At(i / Cols, i % Cols);
            public int Len => Cols == 1 ? Rows : Rows == 1 ? Cols : Rows * Cols;
            public int EffLen => Cols == 1 ? EffRows : Rows == 1 ? EffCols : EffRows * EffCols;
        }

        G Grid(Formula.Node n)
        {
            if (n is Formula.RangeRef rr)
            {
                var s = SheetOf(rr.Sheet);
                int rows = rr.Bottom - rr.Top + 1, cols = rr.Right - rr.Left + 1;
                return new G(rows, cols, Math.Clamp(s.MaxRow - rr.Top + 1, 0, rows), Math.Clamp(s.MaxCol - rr.Left + 1, 0, cols), (r, c) => calc.Value(s, rr.Top + r, rr.Left + c), rr.Top, rr.Left);
            }
            if (n is Formula.Ref rf) return new G(1, 1, 1, 1, (_, _) => calc.Value(SheetOf(rf.Sheet), rf.Row, rf.Col), rf.Row, rf.Col);
            var v = Eval(n);
            return new G(1, 1, 1, 1, (_, _) => v, Row, Col);
        }

        static CellValue Num(double d) => CellValue.Num(d);
        static DateTime D(double serial) => SheetFormat.FromSerial(Math.Floor(serial));

        /// <summary>Zusätzliche Funktionen; null = unbekannt.</summary>
        CellValue? Extra(string name, IReadOnlyList<Formula.Node> a)
        {
            void Need(int min, int max = int.MaxValue) { if (a.Count < min || a.Count > max) throw new FormulaError("#WERT!"); }
            double N(int i) => ToNumber(Eval(a[i]));
            double Opt(int i, double def) => a.Count > i && a[i] is not null ? N(i) : def;
            string T(int i) => ToText(Eval(a[i]));
            switch (name)
            {
                // ---------- Mathematik ----------
                case "SUMIFS": case "AVERAGEIFS": case "MAXIFS": case "MINIFS":
                {
                    if (a.Count < 3 || a.Count % 2 == 0) throw new FormulaError("#WERT!");
                    var target = Grid(a[0]); var hits = MultiMatch(a.Skip(1).ToList(), target.Rows, target.Cols).Select(i => target.At(i / target.Cols, i % target.Cols)).Where(v => v.IsNumber).Select(v => v.Number).ToList();
                    return name switch
                    {
                        "SUMIFS" => Num(hits.Sum()),
                        "AVERAGEIFS" => hits.Count == 0 ? CellValue.Err("#DIV/0!") : Num(hits.Average()),
                        "MAXIFS" => Num(hits.Count == 0 ? 0 : hits.Max()),
                        _ => Num(hits.Count == 0 ? 0 : hits.Min()),
                    };
                }
                case "COUNTIFS":
                {
                    if (a.Count < 2 || a.Count % 2 == 1) throw new FormulaError("#WERT!");
                    var first = Grid(a[0]); return Num(MultiMatch(a, first.Rows, first.Cols).Count());
                }
                case "SUMPRODUCT":
                {
                    Need(1); var gs = a.Select(Grid).ToList();
                    if (gs.Any(g => g.Rows != gs[0].Rows || g.Cols != gs[0].Cols)) return CellValue.Err("#WERT!");
                    double sum = 0; var er = gs.Max(g => g.EffRows); var ec = gs.Max(g => g.EffCols);
                    for (var r = 0; r < er; r++) for (var c = 0; c < ec; c++)
                    {
                        double p = 1;
                        foreach (var g in gs) { var v = g.At(r, c); if (v.IsError) return v; p *= v.IsNumber ? v.Number : 0; }
                        sum += p;
                    }
                    return Num(sum);
                }
                case "SUMSQ": return Num(Numbers(a).Sum(x => x * x));
                case "CEILING": { Need(1, 2); var x = N(0); var s = Opt(1, x < 0 ? -1 : 1); if (s == 0) return Num(0); if (x > 0 && s < 0) return CellValue.Err("#ZAHL!"); return Num(Math.Ceiling(x / s - 1e-12) * s); }
                case "FLOOR": { Need(1, 2); var x = N(0); var s = Opt(1, x < 0 ? -1 : 1); if (s == 0) return CellValue.Err("#DIV/0!"); if (x > 0 && s < 0) return CellValue.Err("#ZAHL!"); return Num(Math.Floor(x / s + 1e-12) * s); }
                case "MROUND": { Need(2, 2); var x = N(0); var m = N(1); if (m == 0) return Num(0); if (Math.Sign(x) * Math.Sign(m) < 0) return CellValue.Err("#ZAHL!"); return Num(Math.Round(x / m, MidpointRounding.AwayFromZero) * m); }
                case "TRUNC": { Need(1, 2); var f = Math.Pow(10, (int)Opt(1, 0)); return Num(Math.Truncate(N(0) * f) / f); }
                case "SIGN": Need(1, 1); return Num(Math.Sign(N(0)));
                case "EXP": Need(1, 1); return Num(Math.Exp(N(0)));
                case "LN": { Need(1, 1); var x = N(0); return x <= 0 ? CellValue.Err("#ZAHL!") : Num(Math.Log(x)); }
                case "LOG": { Need(1, 2); var x = N(0); var b = Opt(1, 10); return x <= 0 || b <= 0 || b == 1 ? CellValue.Err("#ZAHL!") : Num(Math.Log(x, b)); }
                case "LOG10": { Need(1, 1); var x = N(0); return x <= 0 ? CellValue.Err("#ZAHL!") : Num(Math.Log10(x)); }
                case "RAND": Need(0, 0); return Num(Random.Shared.NextDouble());
                case "RANDBETWEEN": { Need(2, 2); var lo = (long)Math.Ceiling(N(0)); var hi = (long)Math.Floor(N(1)); return hi < lo ? CellValue.Err("#ZAHL!") : Num(Random.Shared.NextInt64(lo, hi + 1)); }
                case "EVEN": { Need(1, 1); var x = N(0); var y = Math.Ceiling(Math.Abs(x)); if (y % 2 == 1) y++; return Num(Math.Sign(x) * y); }
                case "ODD": { Need(1, 1); var x = N(0); var y = Math.Ceiling(Math.Abs(x)); if (y % 2 == 0) y++; return Num(x < 0 ? -y : y); }
                case "FACT": { Need(1, 1); var n = Math.Floor(N(0)); if (n < 0) return CellValue.Err("#ZAHL!"); double f = 1; for (var i = 2; i <= n; i++) f *= i; return Num(f); }
                case "GCD": { var l = Numbers(a).Select(x => (long)Math.Floor(x)).ToList(); if (l.Any(x => x < 0)) return CellValue.Err("#ZAHL!"); return Num(l.Aggregate(0L, Gcd)); }
                case "LCM": { var l = Numbers(a).Select(x => (long)Math.Floor(x)).ToList(); if (l.Any(x => x < 0)) return CellValue.Err("#ZAHL!"); return Num(l.Aggregate(1L, (x, y) => x == 0 || y == 0 ? 0 : x / Gcd(x, y) * y)); }
                case "QUOTIENT": { Need(2, 2); var d = N(1); return d == 0 ? CellValue.Err("#DIV/0!") : Num(Math.Truncate(N(0) / d)); }
                case "SIN": Need(1, 1); return Num(Math.Sin(N(0)));
                case "COS": Need(1, 1); return Num(Math.Cos(N(0)));
                case "TAN": Need(1, 1); return Num(Math.Tan(N(0)));
                case "ASIN": { Need(1, 1); var x = N(0); return Math.Abs(x) > 1 ? CellValue.Err("#ZAHL!") : Num(Math.Asin(x)); }
                case "ACOS": { Need(1, 1); var x = N(0); return Math.Abs(x) > 1 ? CellValue.Err("#ZAHL!") : Num(Math.Acos(x)); }
                case "ATAN": Need(1, 1); return Num(Math.Atan(N(0)));
                case "ATAN2": { Need(2, 2); var x = N(0); var y = N(1); return x == 0 && y == 0 ? CellValue.Err("#DIV/0!") : Num(Math.Atan2(y, x)); } // Excel: ARCTAN2(x; y)
                case "RADIANS": Need(1, 1); return Num(N(0) * Math.PI / 180);
                case "DEGREES": Need(1, 1); return Num(N(0) * 180 / Math.PI);
                case "ROMAN": { Need(1, 2); var n = (int)N(0); return n is < 0 or > 3999 ? CellValue.Err("#WERT!") : CellValue.Str(Roman(n)); }
                case "COMBIN": { Need(2, 2); var n = Math.Floor(N(0)); var k = Math.Floor(N(1)); if (n < 0 || k < 0 || k > n) return CellValue.Err("#ZAHL!"); double r = 1; for (var i = 1; i <= k; i++) r = r * (n - k + i) / i; return Num(Math.Round(r)); }

                // ---------- Statistik ----------
                case "LARGE": case "SMALL":
                {
                    Need(2, 2); var l = Numbers([a[0]]).OrderBy(x => x).ToList(); var k = (int)Math.Ceiling(N(1));
                    if (k < 1 || k > l.Count) return CellValue.Err("#ZAHL!");
                    return Num(name == "LARGE" ? l[^k] : l[k - 1]);
                }
                case "RANK": case "RANK.EQ":
                {
                    Need(2, 3); var x = N(0); var l = Numbers([a[1]]); var asc = Opt(2, 0) != 0;
                    if (!l.Contains(x)) return CellValue.Err("#NV");
                    return Num(1 + l.Count(v => asc ? v < x : v > x));
                }
                case "STDEV": case "STDEV.S": case "STDEV.P": case "VAR": case "VAR.S": case "VAR.P":
                {
                    var l = Numbers(a); var pop = name.EndsWith(".P", StringComparison.Ordinal);
                    if (l.Count < (pop ? 1 : 2)) return CellValue.Err("#DIV/0!");
                    var m = l.Average(); var v = l.Sum(x => (x - m) * (x - m)) / (l.Count - (pop ? 0 : 1));
                    return Num(name.StartsWith("STDEV", StringComparison.Ordinal) ? Math.Sqrt(v) : v);
                }
                case "MODE":
                {
                    var l = Numbers(a); var best = l.GroupBy(x => x).Where(g => g.Count() > 1).OrderByDescending(g => g.Count()).ThenBy(g => l.IndexOf(g.Key)).FirstOrDefault();
                    return best is null ? CellValue.Err("#NV") : Num(best.Key);
                }
                case "PERCENTILE": case "QUARTILE":
                {
                    Need(2, 2); var l = Numbers([a[0]]).OrderBy(x => x).ToList(); var p = name == "QUARTILE" ? Math.Floor(N(1)) / 4 : N(1);
                    if (l.Count == 0 || p < 0 || p > 1) return CellValue.Err("#ZAHL!");
                    var rank = p * (l.Count - 1); var lo = (int)Math.Floor(rank); var hi = Math.Min(lo + 1, l.Count - 1);
                    return Num(l[lo] + (rank - lo) * (l[hi] - l[lo]));
                }
                case "AVEDEV": { var l = Numbers(a); if (l.Count == 0) return CellValue.Err("#ZAHL!"); var m = l.Average(); return Num(l.Average(x => Math.Abs(x - m))); }
                case "GEOMEAN": { var l = Numbers(a); if (l.Count == 0 || l.Any(x => x <= 0)) return CellValue.Err("#ZAHL!"); return Num(Math.Exp(l.Average(Math.Log))); }
                case "HARMEAN": { var l = Numbers(a); if (l.Count == 0 || l.Any(x => x <= 0)) return CellValue.Err("#ZAHL!"); return Num(l.Count / l.Sum(x => 1 / x)); }
                case "CORREL": case "SLOPE": case "INTERCEPT": case "RSQ":
                {
                    Need(2, 2);
                    var (ys, xs) = Pairs(a[0], a[1]);
                    if (xs.Count < 2) return CellValue.Err("#DIV/0!");
                    double mx = xs.Average(), my = ys.Average(), sxy = 0, sxx = 0, syy = 0;
                    for (var i = 0; i < xs.Count; i++) { sxy += (xs[i] - mx) * (ys[i] - my); sxx += (xs[i] - mx) * (xs[i] - mx); syy += (ys[i] - my) * (ys[i] - my); }
                    if (sxx == 0 || (name is "CORREL" or "RSQ" && syy == 0)) return CellValue.Err("#DIV/0!");
                    var r = sxy / Math.Sqrt(sxx * syy); var slope = sxy / sxx;
                    return Num(name switch { "CORREL" => r, "RSQ" => r * r, "SLOPE" => slope, _ => my - slope * mx });
                }
                case "MAXA": case "MINA": case "AVERAGEA":
                {
                    var l = new List<double>();
                    foreach (var arg in a) foreach (var v in Values(arg)) { if (v.IsError) return v; if (v.IsEmpty) continue; l.Add(v.Kind is CellValue.K.Number or CellValue.K.Bool ? v.Number : 0); }
                    if (l.Count == 0) return name == "AVERAGEA" ? CellValue.Err("#DIV/0!") : Num(0);
                    return Num(name == "MAXA" ? l.Max() : name == "MINA" ? l.Min() : l.Average());
                }

                // ---------- Text ----------
                case "TEXT": { Need(2, 2); var v = Eval(a[0]); if (v.IsError) return v; return CellValue.Str(FormatText(v, T(1))); }
                case "CONCAT": return CellValue.Str(string.Concat(a.SelectMany(Values).Select(ToText)));
                case "TEXTJOIN":
                {
                    Need(3); var sep = T(0); var skip = ToBool(Eval(a[1]));
                    var parts = a.Skip(2).SelectMany(Values).Select(v => v.IsError ? throw new FormulaError(v.Text) : ToText(v)).Where(s => !skip || s.Length > 0);
                    return CellValue.Str(string.Join(sep, parts));
                }
                case "SUBSTITUTE":
                {
                    Need(3, 4); var s = T(0); var old = T(1); var neu = T(2);
                    if (old.Length == 0) return CellValue.Str(s);
                    if (a.Count < 4) return CellValue.Str(s.Replace(old, neu, StringComparison.Ordinal));
                    var n = (int)N(3); if (n < 1) return CellValue.Err("#WERT!");
                    var idx = -1; for (var k = 0; k < n; k++) { idx = s.IndexOf(old, idx + 1, StringComparison.Ordinal); if (idx < 0) return CellValue.Str(s); }
                    return CellValue.Str(s[..idx] + neu + s[(idx + old.Length)..]);
                }
                case "REPLACE":
                {
                    Need(4, 4); var s = T(0); var st = (int)N(1) - 1; var len = (int)N(2); if (st < 0 || len < 0) return CellValue.Err("#WERT!");
                    st = Math.Min(st, s.Length); len = Math.Min(len, s.Length - st);
                    return CellValue.Str(s[..st] + T(3) + s[(st + len)..]);
                }
                case "FIND": case "SEARCH":
                {
                    Need(2, 3); var what = T(0); var within = T(1); var st = (int)Opt(2, 1) - 1;
                    if (st < 0 || st > within.Length) return CellValue.Err("#WERT!");
                    int pos;
                    if (name == "FIND") pos = within.IndexOf(what, st, StringComparison.Ordinal);
                    else
                    {
                        var rx = "^" + Regex.Escape(what).Replace(@"\?", ".").Replace(@"\*", ".*").Replace("~.", @"\?").Replace("~.*", @"\*");
                        var m = Regex.Match(within[st..], rx.TrimStart('^'), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                        pos = m.Success ? st + m.Index : -1;
                    }
                    return pos < 0 ? CellValue.Err("#WERT!") : Num(pos + 1);
                }
                case "REPT": { Need(2, 2); var n = (int)N(1); if (n < 0) return CellValue.Err("#WERT!"); var s = T(0); if ((long)s.Length * n > 32767) return CellValue.Err("#WERT!"); return CellValue.Str(string.Concat(Enumerable.Repeat(s, n))); }
                case "PROPER":
                {
                    Need(1, 1); var s = T(0).ToLower(De); var sb = new StringBuilder(s.Length); var up = true;
                    foreach (var ch in s) { sb.Append(up && char.IsLetter(ch) ? char.ToUpper(ch, De) : ch); up = !char.IsLetter(ch); }
                    return CellValue.Str(sb.ToString());
                }
                case "VALUE": { Need(1, 1); var v = Eval(a[0]); if (v.IsNumber) return v; var lit = Literal(ToText(v).Trim()).Value; return lit.IsNumber ? lit : CellValue.Err("#WERT!"); }
                case "EXACT": Need(2, 2); return CellValue.Bool(string.Equals(T(0), T(1), StringComparison.Ordinal));
                case "CLEAN": Need(1, 1); return CellValue.Str(new string(T(0).Where(ch => ch >= 32).ToArray()));
                case "CHAR": { Need(1, 1); var n = (int)N(0); return n is < 1 or > 255 ? CellValue.Err("#WERT!") : CellValue.Str(Encoding.Latin1.GetString([(byte)n])); }
                case "CODE": { Need(1, 1); var s = T(0); if (s.Length == 0) return CellValue.Err("#WERT!"); var b = Encoding.Latin1.GetBytes(s[..1]); return Num(b[0]); }
                case "FIXED":
                {
                    Need(1, 3); var dec = (int)Opt(1, 2); var noSep = a.Count > 2 && ToBool(Eval(a[2]));
                    var x = RoundTo(N(0), dec, MidpointRounding.AwayFromZero);
                    return CellValue.Str(x.ToString((noSep ? "0" : "#,##0") + (dec > 0 ? "." + new string('0', dec) : ""), De));
                }
                case "T": { Need(1, 1); var v = Eval(a[0]); return v.Kind == CellValue.K.Text ? v : CellValue.Str(""); }
                case "NUMBERVALUE":
                {
                    Need(1, 3); var s = T(0).Trim(); var dec = a.Count > 1 ? T(1) : ","; var grp = a.Count > 2 ? T(2) : ".";
                    if (grp.Length > 0) s = s.Replace(grp[..1], "", StringComparison.Ordinal);
                    if (dec.Length > 0) s = s.Replace(dec[..1], ".", StringComparison.Ordinal);
                    var pct = s.EndsWith('%'); if (pct) s = s[..^1];
                    return double.TryParse(s.Replace(" ", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? Num(pct ? d / 100 : d) : CellValue.Err("#WERT!");
                }

                // ---------- Nachschlagen ----------
                case "HLOOKUP":
                {
                    if (a.Count < 3 || a.Count > 4 || a[1] is not Formula.RangeRef t) throw new FormulaError("#WERT!");
                    var key = Eval(a[0]); var row = (int)N(2) - 1; var sorted = a.Count < 4 || ToBool(Eval(a[3]));
                    if (row < 0 || t.Top + row > t.Bottom) return CellValue.Err("#BEZUG!");
                    var s = SheetOf(t.Sheet); var right = Math.Min(t.Right, Math.Max(t.Left, s.MaxCol)); int? found = null;
                    for (var c = t.Left; c <= right; c++) { var cmp = Compare(calc.Value(s, t.Top, c), key); if (cmp == 0) { found = c; break; } if (sorted) { if (cmp < 0) found = c; else break; } }
                    return found is int col ? calc.Value(s, t.Top + row, col) : CellValue.Err("#NV");
                }
                case "LOOKUP":
                {
                    Need(2, 3); var key = Eval(a[0]); var look = Grid(a[1]); var res = a.Count > 2 ? Grid(a[2]) : look.Rows > 1 && look.Cols > 1 ? null : look;
                    int? hit = null;
                    for (var i = 0; i < (look.Rows > 1 && look.Cols > 1 ? look.EffRows : look.EffLen); i++)
                    {
                        var v = look.Rows > 1 && look.Cols > 1 ? look.At(i, 0) : look.Flat(i);
                        if (v.IsEmpty) continue;
                        if (Compare(v, key) <= 0 && SameKind(v, key)) hit = i; else if (Compare(v, key) > 0) break;
                    }
                    if (hit is not int h) return CellValue.Err("#NV");
                    return res is null ? look.At(h, look.Cols - 1) : res.Flat(h);
                }
                case "INDEX":
                {
                    Need(2, 3); var g = Grid(a[0]); var r = (int)N(1); var c = (int)Opt(2, 0);
                    if (g.Rows == 1 && a.Count == 2) { c = r; r = 1; }
                    if (c == 0) c = 1; if (r == 0) r = 1;
                    if (r < 1 || c < 1 || r > g.Rows || c > g.Cols) return CellValue.Err("#BEZUG!");
                    return g.At(r - 1, c - 1);
                }
                case "MATCH":
                {
                    Need(2, 3); var key = Eval(a[0]); var g = Grid(a[1]); var type = (int)Opt(2, 1);
                    if (g.Rows > 1 && g.Cols > 1) return CellValue.Err("#NV");
                    return Find(key, g, type == 0 ? 0 : type > 0 ? -1 : 1, 1) is int i ? Num(i + 1) : CellValue.Err("#NV");
                }
                case "XMATCH":
                {
                    Need(2, 4); var key = Eval(a[0]); var g = Grid(a[1]);
                    return Find(key, g, (int)Opt(2, 0), (int)Opt(3, 1)) is int i ? Num(i + 1) : CellValue.Err("#NV");
                }
                case "XLOOKUP":
                {
                    Need(3, 6); var key = Eval(a[0]); var look = Grid(a[1]); var ret = Grid(a[2]);
                    if (look.Len != (ret.Rows == 1 || ret.Cols == 1 ? ret.Len : look.Cols == 1 ? ret.Rows : ret.Cols)) return CellValue.Err("#WERT!");
                    if (Find(key, look, (int)Opt(4, 0), (int)Opt(5, 1)) is int i) return ret.Flat(i);
                    return a.Count > 3 ? Eval(a[3]) : CellValue.Err("#NV");
                }
                case "CHOOSE": { Need(2); var i = (int)N(0); return i < 1 || i >= a.Count ? CellValue.Err("#WERT!") : Eval(a[i]); }
                case "ROW": Need(0, 1); return Num((a.Count == 0 ? Row : Grid(a[0]).Top) + 1);
                case "COLUMN": Need(0, 1); return Num((a.Count == 0 ? Col : Grid(a[0]).Left) + 1);
                case "ROWS": Need(1, 1); return Num(Grid(a[0]).Rows);
                case "COLUMNS": Need(1, 1); return Num(Grid(a[0]).Cols);
                case "INDIRECT":
                {
                    Need(1, 2);
                    try
                    {
                        var node = Formula.Parse(T(0), Formula.Dialect.De);
                        return node is Formula.Ref or Formula.RangeRef ? (node is Formula.Ref r ? calc.Value(SheetOf(r.Sheet), r.Row, r.Col) : CellValue.Err("#WERT!")) : CellValue.Err("#BEZUG!");
                    }
                    catch (FormulaError) { return CellValue.Err("#BEZUG!"); }
                }
                case "ADDRESS":
                {
                    Need(2, 5); var r = (int)N(0); var c = (int)N(1); var abs = (int)Opt(2, 1);
                    if (r < 1 || c < 1 || r > Addr.MaxRows || c > Addr.MaxCols) return CellValue.Err("#WERT!");
                    var col = (abs is 1 or 3 ? "$" : "") + Addr.Col(c - 1); var row = (abs is 1 or 2 ? "$" : "") + r.ToString(CultureInfo.InvariantCulture);
                    var txt = col + row; if (a.Count > 4) txt = "'" + T(4).Replace("'", "''") + "'!" + txt;
                    return CellValue.Str(txt);
                }

                // ---------- Datum und Zeit ----------
                case "DATEDIF":
                {
                    Need(3, 3); var s = D(N(0)); var e = D(N(1)); if (e < s) return CellValue.Err("#ZAHL!");
                    var months = (e.Year - s.Year) * 12 + e.Month - s.Month - (e.Day < s.Day ? 1 : 0);
                    return T(2).ToUpperInvariant() switch
                    {
                        "Y" => Num(months / 12), "M" => Num(months), "D" => Num((e - s).Days),
                        "MD" => Num((e - s.AddMonths(months)).Days),
                        "YM" => Num(months % 12),
                        "YD" => Num((e - s.AddYears(months / 12)).Days),
                        _ => CellValue.Err("#ZAHL!"),
                    };
                }
                case "WORKDAY":
                {
                    Need(2, 3); var d = D(N(0)); var n = (int)N(1); var hol = Holidays(a, 2); var step = Math.Sign(n);
                    while (n != 0) { d = d.AddDays(step); if (IsWorkday(d, hol)) n -= step; }
                    return Num(SheetFormat.ToSerial(d));
                }
                case "NETWORKDAYS":
                {
                    Need(2, 3); var s = D(N(0)); var e = D(N(1)); var hol = Holidays(a, 2); var sign = 1;
                    if (e < s) { (s, e) = (e, s); sign = -1; }
                    var n = 0; for (var d = s; d <= e; d = d.AddDays(1)) if (IsWorkday(d, hol)) n++;
                    return Num(sign * n);
                }
                case "EDATE": Need(2, 2); return Num(SheetFormat.ToSerial(D(N(0)).AddMonths((int)N(1))));
                case "EOMONTH": { Need(2, 2); var d = D(N(0)).AddMonths((int)N(1)); return Num(SheetFormat.ToSerial(new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)))); }
                case "WEEKNUM":
                {
                    Need(1, 2); var d = D(N(0)); var type = (int)Opt(1, 1);
                    if (type == 21) return Num(ISOWeek.GetWeekOfYear(d));
                    var first = type is 2 or 11 ? DayOfWeek.Monday : DayOfWeek.Sunday;
                    return Num(CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(d, CalendarWeekRule.FirstDay, first));
                }
                case "ISOWEEKNUM": Need(1, 1); return Num(ISOWeek.GetWeekOfYear(D(N(0))));
                case "HOUR": Need(1, 1); return Num(SheetFormat.FromSerial(N(0)).Hour);
                case "MINUTE": Need(1, 1); return Num(SheetFormat.FromSerial(N(0) + 1e-9).Minute);
                case "SECOND": Need(1, 1); return Num(Math.Round(SheetFormat.FromSerial(N(0)).TimeOfDay.TotalSeconds) % 60);
                case "TIME": { Need(3, 3); var t = (N(0) * 3600 + N(1) * 60 + N(2)) / 86400; return t < 0 ? CellValue.Err("#ZAHL!") : Num(t - Math.Floor(t)); }
                case "DATEVALUE": { Need(1, 1); var v = Literal(T(0).Trim()).Value; return v.IsNumber ? Num(Math.Floor(v.Number)) : CellValue.Err("#WERT!"); }
                case "TIMEVALUE":
                {
                    Need(1, 1);
                    return TimeSpan.TryParse(T(0).Trim(), CultureInfo.InvariantCulture, out var ts) ? Num(ts.TotalDays % 1) : CellValue.Err("#WERT!");
                }
                case "DAYS": Need(2, 2); return Num(Math.Floor(N(0)) - Math.Floor(N(1)));
                case "DAYS360":
                {
                    Need(2, 3); var s = D(N(0)); var e = D(N(1)); var eu = a.Count > 2 && ToBool(Eval(a[2]));
                    int sd = s.Day, ed = e.Day;
                    if (eu) { sd = Math.Min(sd, 30); ed = Math.Min(ed, 30); }
                    else
                    {
                        if (sd == 31 || (s.Month == 2 && sd == DateTime.DaysInMonth(s.Year, 2))) sd = 30;
                        if (ed == 31 && sd >= 30) ed = 30;
                    }
                    return Num((e.Year - s.Year) * 360 + (e.Month - s.Month) * 30 + ed - sd);
                }
                case "YEARFRAC":
                {
                    Need(2, 3); var s = D(N(0)); var e = D(N(1)); if (e < s) (s, e) = (e, s); var basis = (int)Opt(2, 0);
                    return basis switch
                    {
                        1 => Num((e - s).Days / (s.Year == e.Year ? (DateTime.IsLeapYear(s.Year) ? 366.0 : 365.0) : Enumerable.Range(s.Year, e.Year - s.Year + 1).Average(y => DateTime.IsLeapYear(y) ? 366.0 : 365.0))),
                        2 => Num((e - s).Days / 360.0),
                        3 => Num((e - s).Days / 365.0),
                        _ => Num(Days360Us(s, e) / 360.0),
                    };
                }

                // ---------- Logik und Info ----------
                case "IFS":
                {
                    if (a.Count < 2 || a.Count % 2 == 1) throw new FormulaError("#WERT!");
                    for (var i = 0; i < a.Count; i += 2) { var c = Eval(a[i]); if (c.IsError) return c; if (ToBool(c)) return Eval(a[i + 1]); }
                    return CellValue.Err("#NV");
                }
                case "SWITCH":
                {
                    Need(3); var v = Eval(a[0]); if (v.IsError) return v;
                    for (var i = 1; i + 1 < a.Count; i += 2) if (Compare(v, Eval(a[i])) == 0 && SameKind(v, Eval(a[i]))) return Eval(a[i + 1]);
                    return a.Count % 2 == 0 ? Eval(a[^1]) : CellValue.Err("#NV");
                }
                case "XOR": return CellValue.Bool(a.SelectMany(Values).Where(v => !v.IsEmpty).Count(ToBool) % 2 == 1);
                case "IFNA": { Need(2, 2); var v = Eval(a[0]); return v.IsError && v.Text == "#NV" ? Eval(a[1]) : v; }
                case "ISTEXT": Need(1, 1); return CellValue.Bool(Eval(a[0]).Kind == CellValue.K.Text);
                case "ISNONTEXT": Need(1, 1); return CellValue.Bool(Eval(a[0]).Kind != CellValue.K.Text);
                case "ISLOGICAL": Need(1, 1); return CellValue.Bool(Eval(a[0]).Kind == CellValue.K.Bool);
                case "ISNA": { Need(1, 1); var v = Eval(a[0]); return CellValue.Bool(v.IsError && v.Text == "#NV"); }
                case "ISERR": { Need(1, 1); var v = Eval(a[0]); return CellValue.Bool(v.IsError && v.Text != "#NV"); }
                case "ISEVEN": Need(1, 1); return CellValue.Bool(Math.Floor(Math.Abs(N(0))) % 2 == 0);
                case "ISODD": Need(1, 1); return CellValue.Bool(Math.Floor(Math.Abs(N(0))) % 2 == 1);
                case "NA": Need(0, 0); return CellValue.Err("#NV");
                case "TYPE": { Need(1, 1); var v = Eval(a[0]); return Num(v.Kind switch { CellValue.K.Number or CellValue.K.Empty => 1, CellValue.K.Text => 2, CellValue.K.Bool => 4, _ => 16 }); }
                case "N": { Need(1, 1); var v = Eval(a[0]); return v.IsError ? v : Num(v.Kind is CellValue.K.Number or CellValue.K.Bool ? v.Number : 0); }

                // ---------- Finanzen ----------
                case "PMT": Need(3, 5); return Num(Pmt(N(0), N(1), N(2), Opt(3, 0), Opt(4, 0)));
                case "FV": Need(3, 5); return Num(Fv(N(0), N(1), N(2), Opt(3, 0), Opt(4, 0)));
                case "PV":
                {
                    Need(3, 5); double r = N(0), n = N(1), p = N(2), fv = Opt(3, 0), t = Opt(4, 0);
                    return Num(r == 0 ? -(fv + p * n) : -(fv + p * (1 + r * t) * (Math.Pow(1 + r, n) - 1) / r) / Math.Pow(1 + r, n));
                }
                case "NPER":
                {
                    Need(3, 5); double r = N(0), p = N(1), pv = N(2), fv = Opt(3, 0), t = Opt(4, 0);
                    if (r == 0) return p == 0 ? CellValue.Err("#ZAHL!") : Num(-(pv + fv) / p);
                    var num = p * (1 + r * t) - fv * r; var den = p * (1 + r * t) + pv * r;
                    return num / den <= 0 ? CellValue.Err("#ZAHL!") : Num(Math.Log(num / den) / Math.Log(1 + r));
                }
                case "RATE":
                {
                    Need(3, 6); double n = N(0), p = N(1), pv = N(2), fv = Opt(3, 0), t = Opt(4, 0), r = Opt(5, 0.1);
                    for (var i = 0; i < 100; i++)
                    {
                        double F(double x) => Math.Abs(x) < 1e-12 ? pv + p * n + fv : pv * Math.Pow(1 + x, n) + p * (1 + x * t) * (Math.Pow(1 + x, n) - 1) / x + fv;
                        var f = F(r); var df = (F(r + 1e-7) - f) / 1e-7;
                        if (df == 0) break;
                        var next = r - f / df; if (Math.Abs(next - r) < 1e-10) return Num(next); r = next;
                    }
                    return CellValue.Err("#ZAHL!");
                }
                case "NPV":
                {
                    Need(2); var r = N(0); var l = Numbers(a.Skip(1)); double s = 0;
                    for (var i = 0; i < l.Count; i++) s += l[i] / Math.Pow(1 + r, i + 1);
                    return Num(s);
                }
                case "IRR":
                {
                    Need(1, 2); var l = Numbers([a[0]]); var r = Opt(1, 0.1);
                    if (!l.Any(x => x > 0) || !l.Any(x => x < 0)) return CellValue.Err("#ZAHL!");
                    for (var it = 0; it < 200; it++)
                    {
                        double f = 0, df = 0;
                        for (var i = 0; i < l.Count; i++) { f += l[i] / Math.Pow(1 + r, i); df -= i * l[i] / Math.Pow(1 + r, i + 1); }
                        if (df == 0) break;
                        var next = r - f / df; if (Math.Abs(next - r) < 1e-12) return Num(next); r = next;
                    }
                    return CellValue.Err("#ZAHL!");
                }
                case "IPMT": case "PPMT":
                {
                    Need(4, 6); double r = N(0), per = N(1), n = N(2), pv = N(3), fv = Opt(4, 0), t = Opt(5, 0);
                    if (per < 1 || per > n) return CellValue.Err("#ZAHL!");
                    var pmt = Pmt(r, n, pv, fv, t);
                    var ipmt = t == 1 ? (per == 1 ? 0 : (Fv(r, per - 2, pmt, pv, 1) - pmt) * r) : Fv(r, per - 1, pmt, pv, 0) * r;
                    return Num(name == "IPMT" ? ipmt : pmt - ipmt);
                }
                case "EFFECT": { Need(2, 2); var nom = N(0); var p = Math.Floor(N(1)); return nom <= 0 || p < 1 ? CellValue.Err("#ZAHL!") : Num(Math.Pow(1 + nom / p, p) - 1); }
                case "NOMINAL": { Need(2, 2); var eff = N(0); var p = Math.Floor(N(1)); return eff <= 0 || p < 1 ? CellValue.Err("#ZAHL!") : Num(p * (Math.Pow(1 + eff, 1 / p) - 1)); }
                case "SLN": { Need(3, 3); var life = N(2); return life == 0 ? CellValue.Err("#DIV/0!") : Num((N(0) - N(1)) / life); }
                case "SYD": { Need(4, 4); double c = N(0), s = N(1), l = N(2), p = N(3); if (l <= 0 || p < 1 || p > l) return CellValue.Err("#ZAHL!"); return Num((c - s) * (l - p + 1) * 2 / (l * (l + 1))); }
                case "DDB":
                {
                    Need(4, 5); double c = N(0), s = N(1), l = N(2), p = N(3), f = Opt(4, 2);
                    if (l <= 0 || p < 1 || p > l || f <= 0) return CellValue.Err("#ZAHL!");
                    double book = c, dep = 0;
                    for (var i = 1; i <= p; i++) { dep = Math.Min(book * f / l, Math.Max(0, book - s)); book -= dep; }
                    return Num(dep);
                }
            }
            return null;
        }

        // ---------- Hilfen ----------
        static long Gcd(long x, long y) { while (y != 0) (x, y) = (y, x % y); return Math.Abs(x); }

        static string Roman(int n)
        {
            if (n == 0) return "";
            var vals = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 }; var syms = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new StringBuilder(); for (var i = 0; i < vals.Length; i++) while (n >= vals[i]) { sb.Append(syms[i]); n -= vals[i]; }
            return sb.ToString();
        }

        static bool SameKind(CellValue x, CellValue y) => (x.Kind == CellValue.K.Text) == (y.Kind == CellValue.K.Text);

        /// <summary>Indizes (zeilenweise im Zielbereich), bei denen alle Bedingungspaare (Bereich; Kriterium) zutreffen.</summary>
        IEnumerable<int> MultiMatch(IReadOnlyList<Formula.Node> pairs, int rows, int cols)
        {
            var tests = new List<(G Range, Func<CellValue, bool> Test)>();
            for (var i = 0; i + 1 < pairs.Count; i += 2)
            {
                var g = Grid(pairs[i]); if (g.Rows != rows || g.Cols != cols) throw new FormulaError("#WERT!");
                tests.Add((g, Criterion(Eval(pairs[i + 1]))));
            }
            var er = tests.Max(t => t.Range.EffRows); var ec = tests.Max(t => t.Range.EffCols);
            for (var r = 0; r < Math.Max(er, 0); r++) for (var c = 0; c < Math.Max(ec, 0); c++)
                if (tests.All(t => t.Test(t.Range.At(r, c)))) yield return r * cols + c;
        }

        /// <summary>Wertepaare (y, x), nur wo beide Zahlen sind.</summary>
        (List<double> Y, List<double> X) Pairs(Formula.Node ys, Formula.Node xs)
        {
            var gy = Grid(ys); var gx = Grid(xs);
            if (gy.Len != gx.Len) throw new FormulaError("#NV");
            var ly = new List<double>(); var lx = new List<double>();
            for (var i = 0; i < Math.Max(gy.EffLen, gx.EffLen); i++)
            {
                var y = gy.Flat(i); var x = gx.Flat(i);
                if (y.IsError) throw new FormulaError(y.Text); if (x.IsError) throw new FormulaError(x.Text);
                if (y.IsNumber && x.IsNumber) { ly.Add(y.Number); lx.Add(x.Number); }
            }
            return (ly, lx);
        }

        /// <summary>Suche wie VERGLEICH/XVERGLEICH. mode 0 genau (mit * ? bei Text), -1 genau oder nächstkleiner, 1 genau oder nächstgrößer; search 1 vorwärts, -1 rückwärts.</summary>
        static int? Find(CellValue key, G g, int mode, int search)
        {
            var n = g.EffLen; int? best = null; CellValue bestV = default;
            var wild = mode == 0 && key.Kind == CellValue.K.Text && (key.Text.Contains('*') || key.Text.Contains('?'));
            Regex? rx = wild ? new Regex("^" + Regex.Escape(key.Text).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) : null;
            for (var k = 0; k < n; k++)
            {
                var i = search < 0 ? n - 1 - k : k; var v = g.Flat(i);
                if (v.IsEmpty) continue;
                if (rx is not null) { if (v.Kind == CellValue.K.Text && rx.IsMatch(v.Text)) return i; continue; }
                if (!SameKind(v, key)) continue;
                var cmp = Compare(v, key);
                if (cmp == 0) return i;
                if (mode == -1 && cmp < 0 && (best is null || Compare(v, bestV) > 0)) { best = i; bestV = v; }
                if (mode == 1 && cmp > 0 && (best is null || Compare(v, bestV) < 0)) { best = i; bestV = v; }
            }
            return best;
        }

        HashSet<DateTime> Holidays(IReadOnlyList<Formula.Node> a, int i) =>
            a.Count > i ? Values(a[i]).Where(v => v.IsNumber).Select(v => D(v.Number)).ToHashSet() : [];

        static bool IsWorkday(DateTime d, HashSet<DateTime> hol) => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !hol.Contains(d.Date);

        static double Days360Us(DateTime s, DateTime e)
        {
            int sd = s.Day, ed = e.Day;
            if (sd == 31) sd = 30;
            if (ed == 31 && sd == 30) ed = 30;
            return (e.Year - s.Year) * 360 + (e.Month - s.Month) * 30 + ed - sd;
        }

        static double Pmt(double r, double n, double pv, double fv, double t) =>
            r == 0 ? -(pv + fv) / n : -(r * (fv + pv * Math.Pow(1 + r, n))) / ((1 + r * t) * (Math.Pow(1 + r, n) - 1));

        static double Fv(double r, double n, double pmt, double pv, double t) =>
            r == 0 ? -(pv + pmt * n) : -(pv * Math.Pow(1 + r, n) + pmt * (1 + r * t) * (Math.Pow(1 + r, n) - 1) / r);

        /// <summary>TEXT(Wert; Format) mit deutschen Formatcodes: „0,00“, „#.##0,00 €“, „0%“, „TT.MM.JJJJ“, „TTTT“, „MMMM JJJJ“, „hh:mm“.</summary>
        static string FormatText(CellValue v, string fmt)
        {
            if (v.Kind == CellValue.K.Text) return v.Text;
            var x = v.Number;
            var upper = fmt.ToUpperInvariant();
            var isDate = upper.Contains('J') || upper.Contains('T') || upper.Contains('Y') || upper.Contains('D') || upper.Contains("HH") || upper.Contains("SS");
            if (isDate)
            {
                var d = SheetFormat.FromSerial(x);
                var sb = new StringBuilder(); var i = 0;
                while (i < fmt.Length)
                {
                    var ch = fmt[i]; var run = 1; while (i + run < fmt.Length && char.ToUpperInvariant(fmt[i + run]) == char.ToUpperInvariant(ch)) run++;
                    var u = char.ToUpperInvariant(ch);
                    var hasHourBefore = sb.Length > 0 && fmt[..i].ToUpperInvariant().Contains('H');
                    sb.Append(u switch
                    {
                        'J' or 'Y' => run >= 3 ? d.ToString("yyyy", De) : d.ToString("yy", De),
                        'T' or 'D' => run >= 4 ? d.ToString("dddd", De) : run == 3 ? d.ToString("ddd", De) : run == 2 ? d.ToString("dd", De) : d.Day.ToString(De),
                        'M' when hasHourBefore => d.ToString(run >= 2 ? "mm" : "%m", De),
                        'M' => run >= 4 ? d.ToString("MMMM", De) : run == 3 ? d.ToString("MMM", De) : run == 2 ? d.ToString("MM", De) : d.Month.ToString(De),
                        'H' => d.ToString(run >= 2 ? "HH" : "%H", De),
                        'S' => d.ToString(run >= 2 ? "ss" : "%s", De),
                        _ => new string(ch, run),
                    });
                    i += run;
                }
                return sb.ToString();
            }
            // Zahlen: deutsches Format (Punkt = Tausender, Komma = Dezimal) in .NET-Muster übersetzen
            var net = new StringBuilder();
            foreach (var ch in fmt) net.Append(ch switch { '.' => ',', ',' => '.', _ => ch });
            var pattern = net.ToString();
            return x.ToString(pattern, De);
        }
    }
}
