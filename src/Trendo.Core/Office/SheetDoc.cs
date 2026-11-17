using System.Globalization;

namespace Trendo.Core.Office;

/// <summary>T-0228 Trendo Tabelle: Dokumentmodell. Eine Mappe hat Blätter, ein Blatt hat Zellen (nur die belegten werden gespeichert),
/// Spaltenbreiten und ausgeblendete Zeilen (Filter). Eine Zelle merkt sich, was der Nutzer eingetippt hat (Zahl, Text oder Formel mit „=“),
/// dazu Zahlenformat und Aussehen. Gerechnet wird in <see cref="SheetCalc"/>.</summary>
public sealed class SheetDoc
{
    public List<Sheet> Sheets { get; } = [new Sheet("Tabelle1")];
    /// <summary>T-0682: Was beim Öffnen einer fremden Datei nicht übernommen wurde (null = neu angelegt oder nicht geprüft).</summary>
    public ImportReport? Import { get; set; }
    /// <summary>T-0701: Grundschrift der Mappe (in XLSX die erste Schrift, die jede Zelle ohne eigene Schrift nutzt).</summary>
    public string FontName { get; set; } = "Calibri";
    public double FontSize { get; set; } = 11;

    public Sheet this[string name] => Sheets.First(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    public Sheet? Find(string name) => Sheets.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Neues Blatt mit freiem Namen „TabelleN“.</summary>
    public Sheet AddSheet(string? name = null)
    {
        name ??= Enumerable.Range(1, 999).Select(i => "Tabelle" + i).First(n => Find(n) is null);
        if (Find(name) is not null) throw new ArgumentException($"Ein Blatt „{name}“ gibt es schon.");
        var s = new Sheet(name); Sheets.Add(s); return s;
    }

    /// <summary>Tiefe Kopie für Rückgängig/Wiederholen.</summary>
    public SheetDoc Clone()
    {
        var d = new SheetDoc { FontName = FontName, FontSize = FontSize }; d.Sheets.Clear();
        foreach (var s in Sheets) d.Sheets.Add(s.Clone());
        return d;
    }

    public void RemoveSheet(Sheet s)
    {
        if (Sheets.Count == 1) throw new InvalidOperationException("Eine Mappe braucht mindestens ein Blatt.");
        Sheets.Remove(s);
    }

    public void RenameSheet(Sheet s, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.IndexOfAny(['[', ']', '*', '?', '/', '\\', ':']) >= 0) throw new ArgumentException("Ungültiger Blattname.");
        if (Find(name) is { } other && other != s) throw new ArgumentException($"Ein Blatt „{name}“ gibt es schon.");
        s.Name = name;
    }
}

/// <summary>Zahlenformate (C5): wie die Zelle angezeigt wird. Der gespeicherte Wert bleibt immer die genaue Zahl.</summary>
public enum NumFmt { Standard, Zahl, Ganzzahl, Euro, Prozent, Datum, DatumZeit, Text }

public sealed class Cell
{
    /// <summary>Was der Nutzer eingetippt hat: „12,5“, „Miete“ oder „=SUMME(A1:A3)“.</summary>
    public string Raw { get; set; } = "";
    public NumFmt Format { get; set; }
    public bool Bold { get; set; }
    /// <summary>Füllfarbe als #RRGGBB, null = keine.</summary>
    public string? Fill { get; set; }
    public bool Border { get; set; }
    /// <summary>E9: Notiz zur Zelle (wie Kommentar/Notiz in Excel), null = keine.</summary>
    public string? Note { get; set; }
    /// <summary>E9: bleibt bei geschütztem Blatt bearbeitbar (in Excel „Gesperrt“ aus).</summary>
    public bool Unlocked { get; set; }
    /// <summary>T-0701: eigener Zahlenformat-Code wie in Excel (z. B. <c>0.000" kg"</c>). Hat Vorrang vor <see cref="Format"/>
    /// und wird unverändert zurückgeschrieben; null = Trendos Format aus <see cref="Format"/>.</summary>
    public string? NumCode { get; set; }
    /// <summary>T-0701: Schrift – kursiv, Unterstreichung („single“, „double“ …), durchgestrichen, Farbe #RRGGBB, Größe in Punkt, Schriftart (null = Grundschrift der Mappe).</summary>
    public bool Italic { get; set; }
    public string? Underline { get; set; }
    public bool Strike { get; set; }
    public string? FontColor { get; set; }
    public double? FontSize { get; set; }
    public string? FontName { get; set; }
    /// <summary>T-0701: Ausrichtung wie in Excel: waagerecht „left“, „center“, „right“, „justify“, „fill“, „centerContinuous“, „distributed“; senkrecht „top“, „center“, „justify“, „distributed“ (null = unten).</summary>
    public string? HAlign { get; set; }
    public string? VAlign { get; set; }
    public bool Wrap { get; set; }
    public int Indent { get; set; }
    /// <summary>Textdrehung in Grad wie in Excel (0–180, 255 = senkrecht gestapelt).</summary>
    public int Rotation { get; set; }
    public bool Shrink { get; set; }
    /// <summary>T-0701: Rahmen je Seite mit Linienart und Farbe. Null = nur <see cref="Border"/> (dünn rundum) oder kein Rahmen.</summary>
    public CellBorders? Borders { get; set; }
    public bool IsFormula => Raw.StartsWith('=') && Raw.Length > 1;
    public bool HasLook => Bold || Fill is not null || Border || Format != NumFmt.Standard || Unlocked || NumCode is not null || Italic || Underline is not null || Strike || FontColor is not null
        || FontSize is not null || FontName is not null || HAlign is not null || VAlign is not null || Wrap || Indent != 0 || Rotation != 0 || Shrink || Borders is not null;
    public bool IsEmpty => Raw.Length == 0 && Note is null && !HasLook;
    public Cell Clone() => (Cell)MemberwiseClone();

    /// <summary>Inhalt, Notiz und Aussehen einer anderen Zelle übernehmen.</summary>
    public void CopyFrom(Cell o)
    {
        Raw = o.Raw; Note = o.Note; Format = o.Format; NumCode = o.NumCode; Bold = o.Bold; Italic = o.Italic; Underline = o.Underline; Strike = o.Strike; FontColor = o.FontColor; FontSize = o.FontSize; FontName = o.FontName;
        Fill = o.Fill; Border = o.Border; Borders = o.Borders; HAlign = o.HAlign; VAlign = o.VAlign; Wrap = o.Wrap; Indent = o.Indent; Rotation = o.Rotation; Shrink = o.Shrink; Unlocked = o.Unlocked;
    }
}

/// <summary>T-0701: eine Rahmenlinie – Art wie in Excel („thin“, „medium“, „thick“, „dashed“, „dotted“, „double“, „hair“ …) und Farbe #RRGGBB (null = automatisch).</summary>
public sealed record BorderLine(string Style, string? Color = null);

/// <summary>T-0701: Rahmen einer Zelle je Seite.</summary>
public sealed record CellBorders(BorderLine? Left, BorderLine? Right, BorderLine? Top, BorderLine? Bottom)
{
    public bool Any => Left is not null || Right is not null || Top is not null || Bottom is not null;
}

/// <summary>T-0701: verbundener Bereich (Zeilen/Spalten ab 0). Inhalt und Aussehen stehen in der Zelle oben links.</summary>
public sealed record MergeRange(int Top, int Left, int Bottom, int Right)
{
    public bool Contains(int row, int col) => row >= Top && row <= Bottom && col >= Left && col <= Right;
    public bool Overlaps(MergeRange o) => o.Top <= Bottom && o.Bottom >= Top && o.Left <= Right && o.Right >= Left;
    public string Name => Addr.Name(Top, Left) + ":" + Addr.Name(Bottom, Right);
}

public sealed class Sheet(string name)
{
    public string Name { get; set; } = name;
    readonly Dictionary<(int Row, int Col), Cell> _cells = [];
    /// <summary>Spaltenbreite in Zeichen (Standard 10), C6.</summary>
    public Dictionary<int, double> ColWidths { get; } = [];
    /// <summary>Durch den Filter ausgeblendete Zeilen (C4).</summary>
    public HashSet<int> HiddenRows { get; } = [];
    public List<SheetChart> Charts { get; } = [];
    /// <summary>E1: fixierte Zeilen oben und Spalten links („Fenster fixieren“).</summary>
    public int FreezeRows { get; set; }
    public int FreezeCols { get; set; }
    /// <summary>E5: bedingte Formatierung.</summary>
    public List<CondRule> CondRules { get; } = [];
    /// <summary>E6: Auswahllisten.</summary>
    public List<ValidationRule> Validations { get; } = [];
    /// <summary>E9: Blattschutz – nur Zellen mit <see cref="Cell.Unlocked"/> lassen sich ändern.</summary>
    public bool Protected { get; set; }
    /// <summary>F3 Tabelle: Autofilter mit Werteliste je Spalte (null = aus).</summary>
    public SheetAutoFilter? AutoFilter { get; set; }
    /// <summary>T-0701: verbundene Zellen.</summary>
    public List<MergeRange> Merges { get; } = [];
    /// <summary>T-0701: eigene Zeilenhöhen in Punkt (Standard ohne Eintrag).</summary>
    public Dictionary<int, double> RowHeights { get; } = [];

    /// <summary>Verbundener Bereich, in dem die Zelle liegt (oder null).</summary>
    public MergeRange? MergeAt(int row, int col) { foreach (var m in Merges) if (m.Contains(row, col)) return m; return null; }

    /// <summary>Zellen verbinden wie in Excel: Bereiche, die sich überschneiden, gehen im neuen auf; Inhalte außer oben links werden geleert.</summary>
    public MergeRange? Merge(int top, int left, int bottom, int right)
    {
        var m = new MergeRange(top, left, bottom, right);
        foreach (var o in Merges.Where(o => o.Overlaps(m)).ToList())
        {
            Merges.Remove(o);
            m = new MergeRange(Math.Min(m.Top, o.Top), Math.Min(m.Left, o.Left), Math.Max(m.Bottom, o.Bottom), Math.Max(m.Right, o.Right));
        }
        if (m.Top == m.Bottom && m.Left == m.Right) return null;
        foreach (var ((r, c), cell) in _cells) if (m.Contains(r, c) && (r, c) != (m.Top, m.Left)) cell.Raw = "";
        Merges.Add(m); Compact();
        return m;
    }

    /// <summary>Verbund aufheben, wo er die Auswahl berührt; liefert die Zahl der aufgehobenen Bereiche.</summary>
    public int Unmerge(int top, int left, int bottom, int right)
    {
        var sel = new MergeRange(top, left, bottom, right);
        return Merges.RemoveAll(m => m.Overlaps(sel));
    }

    public IEnumerable<KeyValuePair<(int Row, int Col), Cell>> Cells => _cells;

    public Sheet Clone()
    {
        var s = new Sheet(Name);
        foreach (var (k, c) in _cells) s._cells[k] = c.Clone();
        foreach (var (k, w) in ColWidths) s.ColWidths[k] = w;
        foreach (var r in HiddenRows) s.HiddenRows.Add(r);
        s.Charts.AddRange(Charts);
        s.FreezeRows = FreezeRows; s.FreezeCols = FreezeCols; s.Protected = Protected;
        s.CondRules.AddRange(CondRules); s.Validations.AddRange(Validations);
        s.AutoFilter = AutoFilter?.Clone();
        s.Merges.AddRange(Merges);
        foreach (var (k, h) in RowHeights) s.RowHeights[k] = h;
        return s;
    }
    public int MaxRow => _cells.Count == 0 ? -1 : _cells.Keys.Max(k => k.Row);
    public int MaxCol => _cells.Count == 0 ? -1 : _cells.Keys.Max(k => k.Col);

    public Cell? Get(int row, int col) => _cells.GetValueOrDefault((row, col));
    public Cell Get(string a1) { var (r, c) = Addr.Parse(a1); return GetOrAdd(r, c); }
    public Cell GetOrAdd(int row, int col)
    {
        if (row < 0 || col < 0 || row >= Addr.MaxRows || col >= Addr.MaxCols) throw new ArgumentOutOfRangeException(nameof(row), "Zelle außerhalb der Tabelle.");
        if (!_cells.TryGetValue((row, col), out var c)) _cells[(row, col)] = c = new Cell();
        return c;
    }

    public void Set(int row, int col, string raw) => GetOrAdd(row, col).Raw = raw;
    public void Set(string a1, string raw) => Get(a1).Raw = raw;
    public string Raw(int row, int col) => Get(row, col)?.Raw ?? "";

    public void Clear(int row, int col) => _cells.Remove((row, col));
    public void Compact() { foreach (var k in _cells.Where(kv => kv.Value.IsEmpty).Select(kv => kv.Key).ToList()) _cells.Remove(k); }

    public double ColWidth(int col) => ColWidths.GetValueOrDefault(col, 10);

    // ---------- C2: Zeilen und Spalten einfügen/löschen – Formeln wandern mit ----------

    public void InsertRows(int at, int count = 1) => Shift(rowAt: at, rowDelta: count);
    public void DeleteRows(int at, int count = 1) => Shift(rowAt: at, rowDelta: -count);
    public void InsertCols(int at, int count = 1) => Shift(colAt: at, colDelta: count);
    public void DeleteCols(int at, int count = 1) => Shift(colAt: at, colDelta: -count);

    void Shift(int rowAt = int.MaxValue, int rowDelta = 0, int colAt = int.MaxValue, int colDelta = 0)
    {
        var moved = new Dictionary<(int, int), Cell>();
        foreach (var ((r, c), cell) in _cells)
        {
            if (rowDelta < 0 && r >= rowAt && r < rowAt - rowDelta) continue;
            if (colDelta < 0 && c >= colAt && c < colAt - colDelta) continue;
            var nr = r >= rowAt ? r + rowDelta : r;
            var nc = c >= colAt ? c + colDelta : c;
            if (nr < 0 || nc < 0) continue;
            moved[(nr, nc)] = cell;
        }
        _cells.Clear();
        foreach (var kv in moved) _cells[kv.Key] = kv.Value;
        foreach (var cell in _cells.Values.Where(c => c.IsFormula))
            cell.Raw = Formula.ShiftRefs(cell.Raw, Name, rowAt, rowDelta, colAt, colDelta);
        if (colDelta != 0)
        {
            var w = ColWidths.ToList(); ColWidths.Clear();
            foreach (var (c, v) in w) { if (colDelta < 0 && c >= colAt && c < colAt - colDelta) continue; ColWidths[c >= colAt ? c + colDelta : c] = v; }
        }
        if (rowDelta != 0)
        {
            var h = RowHeights.ToList(); RowHeights.Clear();
            foreach (var (r, v) in h) { if (rowDelta < 0 && r >= rowAt && r < rowAt - rowDelta) continue; RowHeights[r >= rowAt ? r + rowDelta : r] = v; }
        }
        // T-0701: verbundene Bereiche wandern mit, schrumpfen beim Löschen und verschwinden, wenn nur noch eine Zelle übrig ist
        var merges = Merges.ToList(); Merges.Clear();
        foreach (var m in merges)
            if (MergeSpan(m.Top, m.Bottom, rowAt, rowDelta) is { } rs && MergeSpan(m.Left, m.Right, colAt, colDelta) is { } cs && (rs.A != rs.B || cs.A != cs.B))
                Merges.Add(new MergeRange(rs.A, cs.A, rs.B, cs.B));
        HiddenRows.Clear();
    }

    static (int A, int B)? MergeSpan(int a, int b, int at, int delta)
    {
        if (delta == 0) return (a, b);
        if (delta > 0) return (a >= at ? a + delta : a, b >= at ? b + delta : b);
        var end = at - delta; // gelöscht: at … end-1
        var na = a < at ? a : a >= end ? a + delta : at;
        var nb = b < at ? b : b >= end ? b + delta : at - 1;
        return nb < na ? null : (na, nb);
    }

    // ---------- C4: Sortieren und Filtern ----------

    /// <summary>Sortiert die Zeilen <paramref name="top"/>..<paramref name="bottom"/> nach Spalte <paramref name="byCol"/>. Zahlen vor Text,
    /// leere Zellen ans Ende; gleiche Werte behalten ihre Reihenfolge. Formeln mit Bezügen in der eigenen Zeile wandern mit.</summary>
    public void SortRows(int top, int bottom, int left, int right, int byCol, bool ascending, SheetDoc? doc = null)
    {
        var calc = new SheetCalc(doc ?? new SheetDoc().With(this));
        var rows = Enumerable.Range(top, bottom - top + 1)
            .Select(r => (Row: r, Key: calc.Value(this, r, byCol), Cells: Enumerable.Range(left, right - left + 1).Select(c => Get(r, c)?.Clone()).ToList()))
            .ToList();
        var ordered = rows.OrderBy(x => x.Key.IsEmpty ? 2 : x.Key.IsNumber ? 0 : 1)
            .ThenBy(x => x.Key.IsNumber ? x.Key.Number : 0)
            .ThenBy(x => x.Key.IsNumber ? "" : x.Key.Text, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true)).ToList();
        if (!ascending)
        {
            var empties = ordered.Where(x => x.Key.IsEmpty).ToList();
            ordered = ordered.Where(x => !x.Key.IsEmpty).Reverse().Concat(empties).ToList();
        }
        for (var i = 0; i < ordered.Count; i++)
        {
            var target = top + i; var from = ordered[i].Row;
            for (var c = left; c <= right; c++)
            {
                var cell = ordered[i].Cells[c - left];
                if (cell is null) { Clear(target, c); continue; }
                if (cell.IsFormula && from != target) cell.Raw = Formula.ShiftRefs(cell.Raw, Name, int.MinValue, target - from, int.MaxValue, 0, onlyRow: from);
                _cells[(target, c)] = cell;
            }
        }
    }

    /// <summary>Filter: blendet Zeilen unterhalb der Kopfzeile aus, deren Wert in Spalte <paramref name="col"/> nicht passt.</summary>
    public int Filter(int header, int bottom, int col, Func<CellValue, bool> keep, SheetDoc? doc = null)
    {
        var calc = new SheetCalc(doc ?? new SheetDoc().With(this));
        HiddenRows.Clear();
        for (var r = header + 1; r <= bottom; r++) if (!keep(calc.Value(this, r, col))) HiddenRows.Add(r);
        return HiddenRows.Count;
    }

    public void ClearFilter() { HiddenRows.Clear(); AutoFilter = null; }
}

/// <summary>C7: Diagramm über einen Bereich. Erste Spalte = Beschriftung, weitere Spalten = Reihen (erste Zeile = Reihennamen).
/// T-0701: dazu Achsentitel, Farben der Reihen (#RRGGBB je Reihe, null = automatisch) und die Lage der Legende („b“, „r“, „t“, „l“, „tr“; „none“ = keine).</summary>
public sealed record SheetChart(ChartKind Kind, string Range, string Title, string? XTitle = null, string? YTitle = null, IReadOnlyList<string?>? Colors = null, string? Legend = null);
public enum ChartKind { Balken, Linie, Kreis }

static class SheetDocExt
{
    /// <summary>Hilfsmappe, die genau dieses Blatt enthält (für Rechnen ohne Mappe).</summary>
    public static SheetDoc With(this SheetDoc d, Sheet s) { d.Sheets.Clear(); d.Sheets.Add(s); return d; }
}

/// <summary>Zelladressen: „A1“ ↔ (Zeile 0, Spalte 0). Spalten A…Z, AA…XFD wie Excel/Calc.</summary>
public static class Addr
{
    public const int MaxRows = 1_048_576, MaxCols = 16_384;

    public static string Col(int c)
    {
        var s = "";
        for (c++; c > 0; c = (c - 1) / 26) s = (char)('A' + (c - 1) % 26) + s;
        return s;
    }

    public static int ColIndex(string letters)
    {
        var c = 0;
        foreach (var ch in letters.ToUpperInvariant()) { if (ch < 'A' || ch > 'Z') throw new FormatException("Spalte: " + letters); c = c * 26 + (ch - 'A' + 1); }
        return c - 1;
    }

    public static string Name(int row, int col) => Col(col) + (row + 1).ToString(CultureInfo.InvariantCulture);

    public static (int Row, int Col) Parse(string a1)
    {
        a1 = a1.Replace("$", "").Trim();
        var i = 0; while (i < a1.Length && char.IsLetter(a1[i])) i++;
        if (i == 0 || i == a1.Length || !int.TryParse(a1[i..], NumberStyles.None, CultureInfo.InvariantCulture, out var r) || r < 1) throw new FormatException("Ungültige Zelle: " + a1);
        var c = ColIndex(a1[..i]);
        if (r > MaxRows || c >= MaxCols) throw new FormatException("Zelle außerhalb der Tabelle: " + a1);
        return (r - 1, c);
    }

    public static bool TryParse(string a1, out (int Row, int Col) rc)
    {
        try { rc = Parse(a1); return true; } catch (FormatException) { rc = default; return false; }
    }

    /// <summary>Bereich „A1:B5“ (auch einzelne Zelle) → oben links / unten rechts.</summary>
    public static (int Top, int Left, int Bottom, int Right) Range(string range)
    {
        var p = range.Split(':');
        var a = Parse(p[0]); var b = p.Length > 1 ? Parse(p[1]) : a;
        return (Math.Min(a.Row, b.Row), Math.Min(a.Col, b.Col), Math.Max(a.Row, b.Row), Math.Max(a.Col, b.Col));
    }
}
