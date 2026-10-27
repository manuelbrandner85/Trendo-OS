using System.Globalization;

namespace Trendo.Core.Office;

/// <summary>E5: Art der bedingten Formatierung.</summary>
public enum CondKind { Regel, Farbskala, Datenbalken }

/// <summary>E5: Vergleich einer Regel – „größer als“, „zwischen“, „Text enthält“ …</summary>
public enum CondOp { Groesser, Kleiner, Zwischen, Gleich, Enthaelt }

/// <summary>E5: bedingte Formatierung für einen Bereich. Bei <see cref="CondKind.Regel"/> färbt <see cref="Fill"/> passende Zellen.</summary>
public sealed record CondRule(string Range, CondKind Kind, CondOp Op = CondOp.Groesser, double V1 = 0, double V2 = 0, string Fill = "#FFFDE2E2", string? Text = null);

/// <summary>E6: Auswahlliste für Zellen eines Bereichs.</summary>
public sealed record ValidationRule(string Range, IReadOnlyList<string> Items);

/// <summary>Wie eine Zelle durch bedingte Formatierung aussieht.</summary>
public readonly record struct CellLook(string? Fill, double? Bar, string BarColor);

/// <summary>T-0228 E5–E9: bedingte Formatierung, Auswahllisten, „Als Tabelle formatieren“, Duplikate entfernen, Text in Spalten, Blitzvorschau.</summary>
public static class SheetExtras
{
    static bool In(string range, int r, int c) { var (t, l, b, rr) = Addr.Range(range.Contains(':') ? range : range + ":" + range); return r >= t && r <= b && c >= l && c <= rr; }

    // ---------- E5: bedingte Formatierung ----------
    public static CellLook Look(Sheet s, SheetCalc calc, int r, int c)
    {
        string? fill = null; double? bar = null; var barColor = "#FF63A0F0";
        foreach (var rule in s.CondRules)
        {
            if (!In(rule.Range, r, c)) continue;
            var v = calc.Value(s, r, c);
            switch (rule.Kind)
            {
                case CondKind.Regel when fill is null && Matches(rule, v): fill = rule.Fill; break;
                case CondKind.Farbskala when v.IsNumber && fill is null:
                {
                    var (min, max) = MinMax(s, calc, rule.Range);
                    fill = Scale(max > min ? (v.Number - min) / (max - min) : 0.5); break;
                }
                case CondKind.Datenbalken when v.IsNumber && bar is null:
                {
                    var (min, max) = MinMax(s, calc, rule.Range);
                    var lo = Math.Min(0, min); bar = max > lo ? Math.Clamp((v.Number - lo) / (max - lo), 0, 1) : 0; barColor = rule.Fill; break;
                }
            }
        }
        return new CellLook(fill, bar, barColor);
    }

    public static bool Matches(CondRule rule, CellValue v) => rule.Op switch
    {
        CondOp.Enthaelt => (rule.Text ?? "").Length > 0 && (v.Kind == CellValue.K.Text ? v.Text : v.ToString()).Contains(rule.Text!, StringComparison.CurrentCultureIgnoreCase),
        _ when !v.IsNumber => false,
        CondOp.Groesser => v.Number > rule.V1,
        CondOp.Kleiner => v.Number < rule.V1,
        CondOp.Zwischen => v.Number >= Math.Min(rule.V1, rule.V2) && v.Number <= Math.Max(rule.V1, rule.V2),
        _ => v.Number == rule.V1,
    };

    static (double Min, double Max) MinMax(Sheet s, SheetCalc calc, string range)
    {
        var (t, l, b, r) = Addr.Range(range.Contains(':') ? range : range + ":" + range);
        double min = double.MaxValue, max = double.MinValue;
        for (var i = t; i <= Math.Min(b, Math.Max(t, s.MaxRow)); i++) for (var j = l; j <= Math.Min(r, Math.Max(l, s.MaxCol)); j++)
            if (calc.Value(s, i, j) is { IsNumber: true } v) { min = Math.Min(min, v.Number); max = Math.Max(max, v.Number); }
        return min > max ? (0, 0) : (min, max);
    }

    /// <summary>Farbskala Rot → Gelb → Grün (wie die Excel-Standardskala).</summary>
    public static string Scale(double f)
    {
        f = Math.Clamp(f, 0, 1);
        (int R, int G, int B) lo = (0xF8, 0x69, 0x6B), mid = (0xFF, 0xEB, 0x84), hi = (0x63, 0xBE, 0x7B);
        var (a, b, k) = f < 0.5 ? (lo, mid, f * 2) : (mid, hi, (f - 0.5) * 2);
        int L(int x, int y) => (int)Math.Round(x + (y - x) * k);
        return $"#FF{L(a.R, b.R):X2}{L(a.G, b.G):X2}{L(a.B, b.B):X2}";
    }

    // ---------- E6: Auswahllisten ----------
    public static ValidationRule? ValidationFor(Sheet s, int r, int c) => s.Validations.LastOrDefault(v => In(v.Range, r, c));

    /// <summary>Ist die Eingabe erlaubt? Leer ist immer erlaubt, sonst nur ein Eintrag der Liste (Groß/klein egal).</summary>
    public static bool Allowed(ValidationRule? rule, string raw) => rule is null || raw.Length == 0 || rule.Items.Any(i => i.Equals(raw.Trim(), StringComparison.CurrentCultureIgnoreCase));

    // ---------- E7: Als Tabelle formatieren ----------
    /// <summary>Kopf fett und farbig, Streifen, Rahmen; optional Summenzeile mit SUMME für Zahlenspalten. Liefert die Zeile der Summen oder -1.</summary>
    public static int FormatAsTable(Sheet s, SheetCalc calc, int top, int left, int bottom, int right, bool totals, string header = "#FFBDD7EE", string band = "#FFEEF4FB")
    {
        for (var c = left; c <= right; c++) { var h = s.GetOrAdd(top, c); h.Bold = true; h.Fill = header; h.Border = true; }
        for (var r = top + 1; r <= bottom; r++)
            for (var c = left; c <= right; c++) { var cell = s.GetOrAdd(r, c); cell.Border = true; cell.Fill = (r - top) % 2 == 0 ? band : null; }
        if (!totals) return -1;
        var tr = bottom + 1;
        for (var c = left; c <= right; c++)
        {
            var cell = s.GetOrAdd(tr, c); cell.Bold = true; cell.Border = true; cell.Fill = header;
            var numeric = Enumerable.Range(top + 1, Math.Max(0, bottom - top)).Any(r => calc.Value(s, r, c).IsNumber);
            if (numeric) { cell.Raw = $"=SUMME({Addr.Name(top + 1, c)}:{Addr.Name(bottom, c)})"; cell.Format = s.Get(top + 1, c)?.Format ?? NumFmt.Standard; }
            else if (c == left) cell.Raw = "Summe";
        }
        return tr;
    }

    // ---------- E8: Duplikate entfernen, Text in Spalten, Blitzvorschau ----------
    /// <summary>Entfernt Zeilen, die in den Spalten des Bereichs genau einer früheren Zeile gleichen. Darunter liegende Zeilen des Bereichs rücken auf.</summary>
    public static int RemoveDuplicates(Sheet s, SheetCalc calc, int top, int left, int bottom, int right)
    {
        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase); var keep = new List<List<Cell?>>();
        for (var r = top; r <= bottom; r++)
        {
            var key = string.Join('\u001f', Enumerable.Range(left, right - left + 1).Select(c => calc.Display(s, r, c)));
            if (seen.Add(key)) keep.Add(Enumerable.Range(left, right - left + 1).Select(c => s.Get(r, c)?.Clone()).ToList());
        }
        var removed = bottom - top + 1 - keep.Count;
        for (var r = top; r <= bottom; r++)
            for (var c = left; c <= right; c++)
            {
                var src = r - top < keep.Count ? keep[r - top][c - left] : null;
                if (src is null) s.Clear(r, c); else { var t = s.GetOrAdd(r, c); t.Raw = src.Raw; t.Bold = src.Bold; t.Fill = src.Fill; t.Border = src.Border; t.Format = src.Format; t.Note = src.Note; }
            }
        return removed;
    }

    /// <summary>Text in Spalten: teilt den Inhalt jeder Zelle der Spalte am Trennzeichen auf die Spalten rechts daneben auf. Liefert die Zahl der Spalten.</summary>
    public static int TextToColumns(Sheet s, int top, int bottom, int col, char sep)
    {
        var most = 0;
        for (var r = top; r <= bottom; r++)
        {
            var raw = s.Raw(r, col); if (raw.Length == 0 || raw.StartsWith('=')) continue;
            var parts = raw.Split(sep).Select(p => p.Trim()).ToArray();
            for (var i = 0; i < parts.Length; i++) { if (parts[i].Length == 0) s.Clear(r, col + i); else s.Set(r, col + i, parts[i]); }
            most = Math.Max(most, parts.Length);
        }
        return most;
    }

    /// <summary>Blitzvorschau: lernt aus den schon ausgefüllten Zellen der Spalte <paramref name="target"/> eine Regel
    /// (z. B. Vorname, Nachname, Initialen, Teil vor „@“, Groß-/Kleinschreibung, zwei Spalten verbinden) und füllt die übrigen Zeilen.</summary>
    public static int FlashFill(Sheet s, SheetCalc calc, int top, int bottom, int target)
    {
        var examples = Enumerable.Range(top, bottom - top + 1).Where(r => s.Raw(r, target).Length > 0).ToList();
        if (examples.Count == 0) return 0;
        var cols = Enumerable.Range(0, Math.Max(0, s.MaxCol + 1)).Where(c => c != target).ToList();
        string Src(int r, int c) => calc.Display(s, r, c);
        var rules = new List<Func<int, string>>();
        foreach (var c in cols)
        {
            var cc = c;
            foreach (var f in Transforms()) rules.Add(r => f(Src(r, cc)));
            foreach (var c2 in cols.Where(x => x != c))
                foreach (var sepStr in new[] { " ", ", ", "-", "", "." })
                {
                    var c2c = c2; var sp = sepStr;
                    rules.Add(r => Src(r, cc) + sp + Src(r, c2c));
                    rules.Add(r => Src(r, cc) + sp + First(Src(r, c2c)));
                }
        }
        var rule = rules.FirstOrDefault(f => examples.All(r => Safe(f, r) == s.Raw(r, target)));
        if (rule is null) return 0;
        var n = 0;
        for (var r = top; r <= bottom; r++)
            if (s.Raw(r, target).Length == 0 && Safe(rule, r) is { Length: > 0 } v) { s.Set(r, target, v); n++; }
        return n;
        static string Safe(Func<int, string> f, int r) { try { return f(r); } catch (ArgumentException) { return ""; } }
        static string First(string x) => x.Length > 0 ? x[..1] + "." : "";
    }

    static IEnumerable<Func<string, string>> Transforms()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        string[] W(string x) => x.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        yield return x => x;
        yield return x => W(x).FirstOrDefault() ?? "";
        yield return x => W(x).LastOrDefault() ?? "";
        yield return x => string.Concat(W(x).Select(w => char.ToUpper(w[0], de)));
        yield return x => string.Concat(W(x).Select(w => char.ToUpper(w[0], de) + "."));
        yield return x => x.ToUpper(de);
        yield return x => x.ToLower(de);
        yield return x => string.Join(' ', W(x).Select(w => char.ToUpper(w[0], de) + w[1..].ToLower(de)));
        foreach (var sep in new[] { "@", ",", "-", "/", ".", ":" })
        {
            var s = sep;
            yield return x => x.Contains(s) ? x[..x.IndexOf(s, StringComparison.Ordinal)].Trim() : "";
            yield return x => x.Contains(s) ? x[(x.IndexOf(s, StringComparison.Ordinal) + s.Length)..].Trim() : "";
            yield return x => x.Contains(s) ? x[(x.LastIndexOf(s, StringComparison.Ordinal) + s.Length)..].Trim() : "";
        }
        yield return x => new string(x.Where(char.IsDigit).ToArray());
        yield return x => (W(x).LastOrDefault() ?? "") + ", " + (W(x).FirstOrDefault() ?? "");
    }
}
