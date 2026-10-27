using System.Globalization;
using System.Text;

namespace Trendo.Core.Office;

/// <summary>Formeln in Trendo Tabelle (C3): lesen (deutsch „=SUMME(A1:A3;2,5)“ oder englisch wie in XLSX „SUM(A1:A3,2.5)“),
/// übersetzen und Bezüge verschieben, wenn Zeilen oder Spalten eingefügt oder gelöscht werden.</summary>
public static class Formula
{
    public enum Dialect { De, En }

    /// <summary>Englischer Name (wie in XLSX gespeichert) → deutscher Name (wie in Calc/Excel auf Deutsch).</summary>
    public static readonly IReadOnlyDictionary<string, string> German = new Dictionary<string, string>
    {
        ["SUM"] = "SUMME", ["AVERAGE"] = "MITTELWERT", ["MIN"] = "MIN", ["MAX"] = "MAX", ["COUNT"] = "ANZAHL", ["COUNTA"] = "ANZAHL2",
        ["COUNTBLANK"] = "ANZAHLLEEREZELLEN", ["PRODUCT"] = "PRODUKT", ["MEDIAN"] = "MEDIAN", ["IF"] = "WENN", ["IFERROR"] = "WENNFEHLER",
        ["AND"] = "UND", ["OR"] = "ODER", ["NOT"] = "NICHT", ["ROUND"] = "RUNDEN", ["ROUNDUP"] = "AUFRUNDEN", ["ROUNDDOWN"] = "ABRUNDEN",
        ["INT"] = "GANZZAHL", ["ABS"] = "ABS", ["SQRT"] = "WURZEL", ["POWER"] = "POTENZ", ["MOD"] = "REST", ["PI"] = "PI",
        ["TODAY"] = "HEUTE", ["NOW"] = "JETZT", ["DATE"] = "DATUM", ["YEAR"] = "JAHR", ["MONTH"] = "MONAT", ["DAY"] = "TAG", ["WEEKDAY"] = "WOCHENTAG",
        ["CONCATENATE"] = "VERKETTEN", ["LEN"] = "LÄNGE", ["LEFT"] = "LINKS", ["RIGHT"] = "RECHTS", ["MID"] = "TEIL", ["UPPER"] = "GROSS",
        ["LOWER"] = "KLEIN", ["TRIM"] = "GLÄTTEN", ["ISBLANK"] = "ISTLEER", ["ISNUMBER"] = "ISTZAHL", ["ISERROR"] = "ISTFEHLER",
        ["SUMIF"] = "SUMMEWENN", ["COUNTIF"] = "ZÄHLENWENN", ["AVERAGEIF"] = "MITTELWERTWENN", ["VLOOKUP"] = "SVERWEIS",
    };
    static readonly Dictionary<string, string> English = German.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string> ErrDeEn = new() { ["#BEZUG!"] = "#REF!", ["#WERT!"] = "#VALUE!", ["#NV"] = "#N/A", ["#ZAHL!"] = "#NUM!", ["#DIV/0!"] = "#DIV/0!", ["#NAME?"] = "#NAME?" };

    /// <summary>Kanonischer (englischer) Funktionsname aus deutscher oder englischer Schreibweise; unbekannte Namen bleiben (→ #NAME?).</summary>
    public static string Canonical(string name)
    {
        var u = name.ToUpperInvariant();
        return German.ContainsKey(u) ? u : English.TryGetValue(name, out var en) ? en : u;
    }

    public static IEnumerable<string> GermanNames => German.Values.OrderBy(x => x, StringComparer.Ordinal);

    // ---------- Syntaxbaum ----------
    public abstract record Node;
    public sealed record Num(double Value) : Node;
    public sealed record Str(string Value) : Node;
    public sealed record Bool(bool Value) : Node;
    public sealed record ErrNode(string Code) : Node;
    public sealed record Ref(string? Sheet, int Row, int Col) : Node;
    public sealed record RangeRef(string? Sheet, int Top, int Left, int Bottom, int Right) : Node;
    public sealed record Unary(char Op, Node Arg) : Node;
    public sealed record Binary(string Op, Node Left, Node Right) : Node;
    public sealed record Call(string Name, IReadOnlyList<Node> Args) : Node;

    // ---------- Zerlegen ----------
    enum T { Num, Str, Bool, Err, Ref, Range, Func, Op, LParen, RParen, Sep, End }

    sealed record Tok(T Kind, string Text, int Start, int Length)
    {
        public double Number; public string? Sheet; public (int Row, int Col, bool AR, bool AC) A, B;
    }

    static List<Tok> Lex(string s, Dialect d)
    {
        var list = new List<Tok>(); var i = 0;
        char sep = d == Dialect.De ? ';' : ',';
        while (i < s.Length)
        {
            var ch = s[i];
            if (char.IsWhiteSpace(ch)) { i++; continue; }
            var start = i;
            if (ch == '"')
            {
                var sb = new StringBuilder(); i++;
                while (true)
                {
                    if (i >= s.Length) throw new FormulaError("#WERT!");
                    if (s[i] == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { sb.Append('"'); i += 2; continue; } i++; break; }
                    sb.Append(s[i++]);
                }
                list.Add(new Tok(T.Str, sb.ToString(), start, i - start)); continue;
            }
            if (ch == '#')
            {
                i++; while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '/' or '!' or '?')) i++;
                list.Add(new Tok(T.Err, s[start..i].ToUpperInvariant(), start, i - start)); continue;
            }
            if (char.IsDigit(ch) || (ch is '.' or ',' && i + 1 < s.Length && char.IsDigit(s[i + 1]) && (ch == '.' || d == Dialect.De)))
            {
                while (i < s.Length && char.IsDigit(s[i])) i++;
                if (i < s.Length && (s[i] == '.' || (d == Dialect.De && s[i] == ',')) && i + 1 < s.Length && char.IsDigit(s[i + 1])) { i++; while (i < s.Length && char.IsDigit(s[i])) i++; }
                if (i < s.Length && s[i] is 'e' or 'E' && i + 1 < s.Length && (char.IsDigit(s[i + 1]) || s[i + 1] is '+' or '-'))
                { i += 2; while (i < s.Length && char.IsDigit(s[i])) i++; }
                // Zahl direkt vor einem Buchstaben wäre eine Zeilen-/Spaltenmischung („1A“) – das ist kein gültiger Ausdruck
                var text = s[start..i];
                if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var num)) throw new FormulaError("#WERT!");
                list.Add(new Tok(T.Num, text, start, i - start) { Number = num }); continue;
            }
            if (char.IsLetter(ch) || ch is '$' or '_' or '\'')
            {
                string? sheet = null;
                if (ch == '\'')
                {
                    var end = s.IndexOf("'!", i + 1, StringComparison.Ordinal);
                    if (end < 0) throw new FormulaError("#BEZUG!");
                    sheet = s[(i + 1)..end].Replace("''", "'"); i = end + 2;
                }
                var w0 = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '$' or '_' or '.')) i++;
                var word = s[w0..i];
                if (sheet is null && i < s.Length && s[i] == '!') { sheet = word; i++; w0 = i; while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '$')) i++; word = s[w0..i]; }
                if (sheet is null && i < s.Length && s[i] == '(') { list.Add(new Tok(T.Func, word, start, i - start)); continue; }
                if (sheet is null && word.ToUpperInvariant() is "WAHR" or "TRUE" or "FALSCH" or "FALSE")
                { list.Add(new Tok(T.Bool, word.ToUpperInvariant() is "WAHR" or "TRUE" ? "1" : "0", start, i - start)); continue; }
                if (!TryCell(word, out var a)) throw new FormulaError(sheet is null ? "#NAME?" : "#BEZUG!");
                if (i < s.Length && s[i] == ':')
                {
                    var j = i + 1; var w1 = j;
                    while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '$')) j++;
                    if (TryCell(s[w1..j], out var b)) { i = j; list.Add(new Tok(T.Range, s[start..i], start, i - start) { Sheet = sheet, A = a, B = b }); continue; }
                }
                list.Add(new Tok(T.Ref, s[start..i], start, i - start) { Sheet = sheet, A = a }); continue;
            }
            if (ch == '(') { list.Add(new Tok(T.LParen, "(", i++, 1)); continue; }
            if (ch == ')') { list.Add(new Tok(T.RParen, ")", i++, 1)); continue; }
            if (ch == sep) { list.Add(new Tok(T.Sep, ch.ToString(), i++, 1)); continue; }
            if (i + 1 < s.Length && s.Substring(i, 2) is "<=" or ">=" or "<>") { list.Add(new Tok(T.Op, s.Substring(i, 2), i, 2)); i += 2; continue; }
            if (ch is '+' or '-' or '*' or '/' or '^' or '&' or '=' or '<' or '>' or '%') { list.Add(new Tok(T.Op, ch.ToString(), i++, 1)); continue; }
            throw new FormulaError("#WERT!");
        }
        list.Add(new Tok(T.End, "", s.Length, 0));
        return list;
    }

    static bool TryCell(string w, out (int Row, int Col, bool AR, bool AC) a)
    {
        a = default;
        var i = 0; var ac = false; var ar = false;
        if (i < w.Length && w[i] == '$') { ac = true; i++; }
        var l0 = i; while (i < w.Length && char.IsAsciiLetter(w[i])) i++;
        if (i == l0 || i - l0 > 3) return false;
        var letters = w[l0..i];
        if (i < w.Length && w[i] == '$') { ar = true; i++; }
        var d0 = i; while (i < w.Length && char.IsAsciiDigit(w[i])) i++;
        if (i == d0 || i != w.Length) return false;
        if (!int.TryParse(w[d0..i], NumberStyles.None, CultureInfo.InvariantCulture, out var r) || r < 1 || r > Addr.MaxRows) return false;
        var c = Addr.ColIndex(letters); if (c >= Addr.MaxCols) return false;
        a = (r - 1, c, ar, ac); return true;
    }

    // ---------- Lesen (Vorrang wie Calc/Excel) ----------
    public static Node Parse(string formula, Dialect d)
    {
        var p = new Parser(Lex(formula, d));
        var n = p.Compare();
        if (p.Peek.Kind != T.End) throw new FormulaError("#WERT!");
        return n;
    }

    sealed class Parser(List<Tok> toks)
    {
        int _i;
        public Tok Peek => toks[_i];
        Tok Next() => toks[_i++];
        bool IsOp(params string[] ops) => Peek.Kind == T.Op && ops.Contains(Peek.Text);

        public Node Compare() { var l = Concat(); while (IsOp("=", "<>", "<", ">", "<=", ">=")) { var op = Next().Text; l = new Binary(op, l, Concat()); } return l; }
        Node Concat() { var l = Add(); while (IsOp("&")) { Next(); l = new Binary("&", l, Add()); } return l; }
        Node Add() { var l = Mul(); while (IsOp("+", "-")) { var op = Next().Text; l = new Binary(op, l, Mul()); } return l; }
        Node Mul() { var l = Pow(); while (IsOp("*", "/")) { var op = Next().Text; l = new Binary(op, l, Pow()); } return l; }
        Node Pow() { var l = Neg(); while (IsOp("^")) { Next(); l = new Binary("^", l, Neg()); } return l; }
        Node Neg() { if (IsOp("-", "+")) { var op = Next().Text[0]; return new Unary(op, Neg()); } return Percent(); }
        Node Percent() { var n = Primary(); while (IsOp("%")) { Next(); n = new Unary('%', n); } return n; }

        Node Primary()
        {
            var t = Next();
            switch (t.Kind)
            {
                case T.Num: return new Num(t.Number);
                case T.Str: return new Str(t.Text);
                case T.Bool: return new Bool(t.Text == "1");
                case T.Err: return new ErrNode(ErrDeEn.FirstOrDefault(kv => kv.Value == t.Text).Key ?? t.Text);
                case T.Ref: return new Ref(t.Sheet, t.A.Row, t.A.Col);
                case T.Range: return new RangeRef(t.Sheet, Math.Min(t.A.Row, t.B.Row), Math.Min(t.A.Col, t.B.Col), Math.Max(t.A.Row, t.B.Row), Math.Max(t.A.Col, t.B.Col));
                case T.LParen: { var n = Compare(); if (Next().Kind != T.RParen) throw new FormulaError("#WERT!"); return n; }
                case T.Func:
                {
                    Next(); // (
                    var args = new List<Node>();
                    if (Peek.Kind != T.RParen)
                        while (true)
                        {
                            args.Add(Peek.Kind is T.Sep or T.RParen ? new Str("") : Compare());
                            if (Peek.Kind == T.Sep) { Next(); continue; }
                            break;
                        }
                    if (Next().Kind != T.RParen) throw new FormulaError("#WERT!");
                    return new Call(Canonical(t.Text), args);
                }
                default: throw new FormulaError("#WERT!");
            }
        }
    }

    /// <summary>Prüft eine Formel (ohne „=“) und liefert eine verständliche Meldung oder null.</summary>
    public static string? Check(string formula)
    {
        try
        {
            var n = Parse(formula, Dialect.De);
            var unknown = Walk(n).OfType<Call>().FirstOrDefault(c => !German.ContainsKey(c.Name));
            return unknown is null ? null : $"Die Funktion „{unknown.Name}“ kennt Trendo Tabelle nicht.";
        }
        catch (FormulaError e) { return e.Code switch { "#NAME?" => "Unbekannter Name in der Formel.", "#BEZUG!" => "Ungültiger Zellbezug.", _ => "Die Formel ist unvollständig oder falsch geschrieben." }; }
    }

    static IEnumerable<Node> Walk(Node n)
    {
        yield return n;
        var kids = n switch { Unary u => [u.Arg], Binary b => [b.Left, b.Right], Call c => c.Args, _ => (IEnumerable<Node>)[] };
        foreach (var k in kids) foreach (var x in Walk(k)) yield return x;
    }

    // ---------- Übersetzen (XLSX speichert englisch mit „,“ und „.“) ----------
    /// <summary>Deutsch (ohne „=“) → wie in XLSX gespeichert.</summary>
    public static string ToExcel(string de) => Rewrite(de, Dialect.De, Dialect.En, null);
    /// <summary>Aus XLSX (englisch, ohne „=“) → deutsch.</summary>
    public static string FromExcel(string en) => Rewrite(en, Dialect.En, Dialect.De, null);

    /// <summary>Formel (mit „=“) nach Einfügen/Löschen von Zeilen oder Spalten anpassen. Bezüge in gelöschte Zellen werden #BEZUG!.
    /// <paramref name="onlyRow"/>: nur Bezüge auf genau diese Zeile verschieben (beim Sortieren).</summary>
    public static string ShiftRefs(string raw, string sheet, int rowAt, int rowDelta, int colAt, int colDelta, int? onlyRow = null)
    {
        if (!raw.StartsWith('=')) return raw;
        try { return "=" + Rewrite(raw[1..], Dialect.De, Dialect.De, new Shift(sheet, rowAt, rowDelta, colAt, colDelta, onlyRow)); }
        catch (FormulaError) { return raw; }
    }

    sealed record Shift(string Sheet, int RowAt, int RowDelta, int ColAt, int ColDelta, int? OnlyRow);

    static string Rewrite(string s, Dialect from, Dialect to, Shift? shift)
    {
        var toks = Lex(s, from);
        var sb = new StringBuilder(); var pos = 0;
        foreach (var t in toks)
        {
            if (t.Kind == T.End) break;
            sb.Append(s, pos, t.Start - pos);
            sb.Append(t.Kind switch
            {
                T.Num when from != to => to == Dialect.En ? t.Text.Replace(',', '.') : t.Text.Replace('.', ','),
                T.Sep => to == Dialect.De ? ";" : ",",
                T.Func => to == Dialect.De ? (German.TryGetValue(Canonical(t.Text), out var de) ? de : t.Text) : Canonical(t.Text),
                T.Bool when from != to => to == Dialect.De ? (t.Text == "1" ? "WAHR" : "FALSCH") : (t.Text == "1" ? "TRUE" : "FALSE"),
                T.Err when from != to => to == Dialect.En ? ErrDeEn.GetValueOrDefault(t.Text, t.Text) : ErrDeEn.FirstOrDefault(kv => kv.Value == t.Text).Key ?? t.Text,
                T.Str => "\"" + t.Text.Replace("\"", "\"\"") + "\"",
                T.Ref or T.Range when shift is not null && (t.Sheet is null || string.Equals(t.Sheet, shift.Sheet, StringComparison.OrdinalIgnoreCase)) => ShiftTok(t, shift),
                _ => s.Substring(t.Start, t.Length),
            });
            pos = t.Start + t.Length;
        }
        return sb.ToString();
    }

    static string ShiftTok(Tok t, Shift sh)
    {
        string? Cell((int Row, int Col, bool AR, bool AC) a, bool isEnd)
        {
            var r = a.Row; var c = a.Col;
            if (sh.OnlyRow is int only) { if (r == only) r += sh.RowDelta; }
            else
            {
                if (sh.RowDelta != 0 && r >= sh.RowAt)
                {
                    if (sh.RowDelta < 0 && r < sh.RowAt - sh.RowDelta) { if (t.Kind != T.Range) return null; r = isEnd ? sh.RowAt - 1 : sh.RowAt; }
                    else r += sh.RowDelta;
                }
                if (sh.ColDelta != 0 && c >= sh.ColAt)
                {
                    if (sh.ColDelta < 0 && c < sh.ColAt - sh.ColDelta) { if (t.Kind != T.Range) return null; c = isEnd ? sh.ColAt - 1 : sh.ColAt; }
                    else c += sh.ColDelta;
                }
            }
            if (r < 0 || c < 0) return null;
            return (a.AC ? "$" : "") + Addr.Col(c) + (a.AR ? "$" : "") + (r + 1).ToString(CultureInfo.InvariantCulture);
        }
        var prefix = t.Sheet is null ? "" : (t.Sheet.Any(ch => !char.IsLetterOrDigit(ch)) ? "'" + t.Sheet.Replace("'", "''") + "'" : t.Sheet) + "!";
        if (t.Kind == T.Ref) return Cell(t.A, false) is { } one ? prefix + one : "#BEZUG!";
        var a = Cell(t.A, false); var b = Cell(t.B, true);
        if (a is null || b is null) return "#BEZUG!";
        var (ra, _) = Addr.Parse(a); var (rb, _) = Addr.Parse(b);
        if (ra > rb) return "#BEZUG!"; // ganzer Bereich gelöscht
        return prefix + a + ":" + b;
    }
}
