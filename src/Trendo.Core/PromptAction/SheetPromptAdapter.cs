using System.Globalization;
using System.Text.RegularExpressions;
using Trendo.Core.Sys;

namespace Trendo.Core.PromptAction;

/// <summary>SYS-10: Tabelle mit Grundrechenarten und SUMME. Gerechnet in Decimal, nicht von LibreOffice.</summary>
public sealed class SheetPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-10";
    public string ActionId => "tabelle.rechnen";
    public string Label => "Tabelle rechnen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Tabelle_rechnet_formel";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["zelle"];
    public IReadOnlyList<string> OutputKinds => ["xlsx"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        if (Lines(parameters).Count == 0)
            return [new("zelle", "zelle", "Welche Zellen? Zum Beispiel „Zelle: A1=2“ und „Zelle: A2=A1+1“.", true)];
        return [];
    }

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var findings = new List<ValidationFinding>();
        try { _ = Compute(parameters); }
        catch (SheetProblem ex) { findings.Add(new(ex.Cell, ex.Message, true)); }
        return findings;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        Sheet sheet;
        try { sheet = Compute(call.Parameters); }
        catch (SheetProblem ex)
        {
            return new(false, [], [], null, [], [ex.Message], null);
        }
        var relative = "tabellen/v" + call.Version + "/tabelle.xlsx";
        var full = call.Projects.ResolveInProject(call.ProjectId, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        Ooxml.NewXlsx(full, sheet.Rows.Select(r => (IList<string>)r).ToList());
        var back = Ooxml.ReadXlsx(full);
        var ok = true;
        foreach (var (addr, value) in sheet.Numbers)
        {
            var (col, row) = Addr(addr);
            var got = row < back.Count && col < back[row].Count ? back[row][col] : "";
            if (!decimal.TryParse(got, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed != value)
                ok = false;
        }
        var preview = string.Join("\n", sheet.Numbers.Select(kv => kv.Addr + " = " + kv.Value.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))));
        return new(true, [new ProducedFile(relative, "xlsx", true)],
            [new QaResult("tabelle.werte", ok, ok ? "Die gespeicherten Zahlen stimmen mit der Rechnung überein." : "Die Datei enthält andere Zahlen als die Rechnung.", null, preview, "Ooxml.ReadXlsx")],
            preview,
            ["Gerechnet mit Decimal in Trendo. LibreOffice hat die Datei nicht nachgerechnet. Nur Grundrechenarten und SUMME."],
            [], null);
    }

    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Eine Tabelle wird nicht versendet."], null);

    static List<string> Lines(IReadOnlyDictionary<string, string> parameters) =>
        (parameters.TryGetValue("zelle", out var raw) ? raw : "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    sealed record Sheet(List<List<string>> Rows, List<(string Addr, decimal Value)> Numbers);

    sealed class SheetProblem(string cell, string message) : Exception(message)
    {
        public string Cell { get; } = cell;
    }

    static Sheet Compute(IReadOnlyDictionary<string, string> parameters)
    {
        var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Lines(parameters))
        {
            var eq = line.IndexOf('=');
            if (eq <= 0 || !TryAddr(line[..eq].Trim(), out var col, out var row))
                throw new SheetProblem("zelle", "„" + line + "“ ist keine Zelle. Beispiel: A1=2");
            raw[Name(col, row)] = line[(eq + 1)..].Trim();
        }
        if (raw.Count == 0) throw new SheetProblem("zelle", "Keine Zelle angegeben.");
        var numbers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        decimal Need(string addr)
        {
            if (numbers.TryGetValue(addr, out var have)) return have;
            if (labels.ContainsKey(addr)) throw new SheetProblem(addr, addr + " ist Text und kann nicht gerechnet werden.");
            if (!raw.TryGetValue(addr, out var expr)) throw new SheetProblem(addr, "Die Zelle " + addr + " fehlt.");
            if (!stack.Add(addr)) throw new SheetProblem(addr, "Die Formel in " + addr + " verweist im Kreis.");
            if (TryNumber(expr, out var literal))
            {
                numbers[addr] = literal;
                stack.Remove(addr);
                return literal;
            }
            if (!LooksLikeFormula(expr))
            {
                labels[addr] = expr;
                stack.Remove(addr);
                throw new SheetProblem(addr, addr + " ist Text und kann nicht gerechnet werden.");
            }
            string expanded;
            try { expanded = ExpandSum(expr); }
            catch (FormatException ex) { throw new SheetProblem(addr, addr + ": " + ex.Message); }
            var replaced = Ref.Replace(expanded, m =>
            {
                var key = m.Value.ToUpperInvariant();
                return "(" + Need(key).ToString(CultureInfo.InvariantCulture) + ")";
            });
            try { numbers[addr] = Eval(replaced); }
            catch (DivideByZeroException) { throw new SheetProblem(addr, "In " + addr + " wird durch 0 geteilt."); }
            catch (FormatException ex) { throw new SheetProblem(addr, addr + ": " + ex.Message); }
            stack.Remove(addr);
            return numbers[addr];
        }

        foreach (var addr in raw.Keys.ToList())
        {
            if (numbers.ContainsKey(addr) || labels.ContainsKey(addr)) continue;
            var expr = raw[addr];
            if (TryNumber(expr, out var literal)) numbers[addr] = literal;
            else if (!LooksLikeFormula(expr)) labels[addr] = expr;
            else Need(addr);
        }

        var maxCol = 0;
        var maxRow = 0;
        foreach (var addr in raw.Keys)
        {
            TryAddr(addr, out var c, out var r);
            maxCol = Math.Max(maxCol, c);
            maxRow = Math.Max(maxRow, r);
        }
        var rows = new List<List<string>>();
        for (var r = 0; r <= maxRow; r++)
        {
            var line = new List<string>();
            for (var c = 0; c <= maxCol; c++)
            {
                var addr = Name(c, r);
                line.Add(numbers.TryGetValue(addr, out var n) ? n.ToString(CultureInfo.InvariantCulture) : labels.GetValueOrDefault(addr) ?? "");
            }
            rows.Add(line);
        }
        return new Sheet(rows, numbers.Select(kv => (kv.Key.ToUpperInvariant(), kv.Value)).OrderByDescending(x => x.Item1).ToList());
    }

    static bool LooksLikeFormula(string expr) =>
        SumRange.IsMatch(expr) || Ref.IsMatch(expr) || expr.IndexOfAny(['+', '*', '/']) >= 0 || expr.Contains('-');

    static string ExpandSum(string expr) => SumRange.Replace(expr, m =>
    {
        var c1 = m.Groups[1].Value.ToUpperInvariant();
        var r1 = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var c2 = m.Groups[3].Value.ToUpperInvariant();
        var r2 = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        if (!string.Equals(c1, c2, StringComparison.Ordinal)) throw new FormatException("SUMME geht hier nur in einer Spalte.");
        var parts = new List<string>();
        for (var r = Math.Min(r1, r2); r <= Math.Max(r1, r2); r++) parts.Add(c1 + r);
        if (parts.Count == 0) throw new FormatException("Der Bereich ist leer.");
        return "(" + string.Join("+", parts) + ")";
    });

    static bool TryNumber(string raw, out decimal value)
    {
        var s = raw.Trim().Replace("€", "").Trim();
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out value)) return true;
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    static bool TryAddr(string raw, out int col, out int row)
    {
        col = 0; row = 0;
        var m = AddrRx.Match(raw.Trim());
        if (!m.Success) return false;
        col = m.Groups[1].Value.ToUpperInvariant().Aggregate(0, (a, ch) => a * 26 + ch - 'A' + 1) - 1;
        row = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) - 1;
        return col >= 0 && row >= 0 && col < 32 && row < 200;
    }

    static (int Col, int Row) Addr(string raw)
    {
        if (!TryAddr(raw, out var c, out var r)) throw new SheetProblem(raw, "Unbekannte Zelle " + raw);
        return (c, r);
    }

    static string Name(int col, int row)
    {
        var s = "";
        for (var i = col + 1; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
        return s + (row + 1).ToString(CultureInfo.InvariantCulture);
    }

    static decimal Eval(string expr)
    {
        var s = expr.Replace(" ", "").Replace("×", "*").Replace("÷", "/").Replace("−", "-").Replace(',', '.');
        var i = 0;
        var value = Sum();
        if (i != s.Length) throw new FormatException("Unerwartetes Zeichen in der Formel.");
        return value;
        decimal Sum() { var x = Prod(); while (i < s.Length && s[i] is '+' or '-') { var op = s[i++]; var y = Prod(); x = op == '+' ? x + y : x - y; } return x; }
        decimal Prod()
        {
            var x = Atom();
            while (i < s.Length && s[i] is '*' or '/')
            {
                var op = s[i++];
                var y = Atom();
                if (op == '/' && y == 0) throw new DivideByZeroException();
                x = op == '*' ? x * y : x / y;
            }
            return x;
        }
        decimal Atom()
        {
            if (i < s.Length && s[i] == '-') { i++; return -Atom(); }
            if (i < s.Length && s[i] == '+') { i++; return Atom(); }
            if (i < s.Length && s[i] == '(')
            {
                i++;
                var x = Sum();
                if (i >= s.Length || s[i] != ')') throw new FormatException("Klammer nicht geschlossen.");
                i++;
                return x;
            }
            var start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (start == i) throw new FormatException("Zahl erwartet.");
            return decimal.Parse(s[start..i], CultureInfo.InvariantCulture);
        }
    }

    static readonly Regex AddrRx = new(@"^([A-Za-z]+)(\d+)$", RegexOptions.CultureInvariant);
    static readonly Regex Ref = new(@"\b([A-Za-z]+)(\d+)\b", RegexOptions.CultureInvariant);
    static readonly Regex SumRange = new(@"(?i)\b(?:SUMME|SUM)\(\s*([A-Za-z]+)(\d+)\s*:\s*([A-Za-z]+)(\d+)\s*\)", RegexOptions.CultureInvariant);
}
