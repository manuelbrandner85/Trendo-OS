using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>T-0228 Stufe C: Tabellenraster von Trendo Tabelle. Zeichnet nur den sichtbaren Ausschnitt (schnell auch bei großen Tabellen),
/// mit Spalten-/Zeilenköpfen, Gitter, Auswahl, Fett, Füllfarbe, Rahmen, Zahlenformaten. Bedienung wie in Calc/Excel.</summary>
public sealed class SheetGridView : Control
{
    public const double HeaderW = 46, HeaderH = 24, RowH = 24, CharW = 7.4;
    public SheetDoc Doc { get; set; } = new();
    public Sheet Sheet { get; set; } = new("Tabelle1");
    public SheetCalc Calc { get; set; } = new(new SheetDoc());
    /// <summary>Aktive Zelle und das andere Ende der Auswahl.</summary>
    public (int Row, int Col) Active { get; private set; }
    public (int Row, int Col) Anchor { get; private set; }
    public int TopRow { get; private set; }
    public int LeftCol { get; private set; }
    public bool Dark { get; set; } = true;

    /// <summary>Auswahl hat sich geändert (Formelleiste, Statusleiste aktualisieren).</summary>
    public event Action? SelectionChanged;
    /// <summary>Bearbeiten einer Zelle beginnen (Doppelklick, F2, Tippen); Text = erstes getipptes Zeichen oder null.</summary>
    public event Action<string?>? EditRequested;
    /// <summary>Taste, die das Raster nicht selbst behandelt (Strg+C/V/Z/Y, Entf).</summary>
    public event Action<KeyEventArgs>? Command;
    /// <summary>Spaltenbreite wurde gezogen.</summary>
    public event Action? Changed;

    int _dragCol = -1; double _dragStartX, _dragStartW; bool _selecting;

    public SheetGridView() { Focusable = true; ClipToBounds = true; }

    public (int Top, int Left, int Bottom, int Right) Selection =>
        (Math.Min(Active.Row, Anchor.Row), Math.Min(Active.Col, Anchor.Col), Math.Max(Active.Row, Anchor.Row), Math.Max(Active.Col, Anchor.Col));

    public void Select(int row, int col, bool extend = false)
    {
        row = Math.Clamp(row, 0, Addr.MaxRows - 1); col = Math.Clamp(col, 0, Addr.MaxCols - 1);
        Active = (row, col); if (!extend) Anchor = Active;
        EnsureVisible(); InvalidateVisual(); SelectionChanged?.Invoke();
    }

    /// <summary>Auswahl und Bildlauf nach dem Neuzeichnen der Seite wiederherstellen.</summary>
    public void Restore((int Row, int Col) active, (int Row, int Col) anchor, int top, int left) { Active = active; Anchor = anchor; TopRow = top; LeftCol = left; InvalidateVisual(); }

    public void SelectRange(int top, int left, int bottom, int right) { Anchor = (top, left); Active = (bottom, right); EnsureVisible(); InvalidateVisual(); SelectionChanged?.Invoke(); }

    double ColW(int c) => Math.Max(3, Sheet.ColWidth(c)) * CharW + 10;
    bool RowHidden(int r) => Sheet.HiddenRows.Contains(r);
    int FR => Math.Max(0, Sheet.FreezeRows);
    int FC => Math.Max(0, Sheet.FreezeCols);

    /// <summary>Sichtbare Zeilen mit Lage: erst die fixierten (E1 „Fenster fixieren“), dann ab <see cref="TopRow"/>.</summary>
    List<(int Row, double Y)> RowLayout()
    {
        var list = new List<(int, double)>(); double y = HeaderH; var h = Math.Max(Bounds.Height, HeaderH + RowH);
        for (var r = 0; r < FR && y < h; r++) if (!RowHidden(r)) { list.Add((r, y)); y += RowH; }
        for (var r = Math.Max(TopRow, FR); y < h && r < Addr.MaxRows; r++) if (!RowHidden(r)) { list.Add((r, y)); y += RowH; }
        return list;
    }

    List<(int Col, double X, double W)> ColLayout()
    {
        var list = new List<(int, double, double)>(); double x = HeaderW; var w = Math.Max(Bounds.Width, HeaderW + 80);
        for (var c = 0; c < FC && x < w; c++) { var cw = ColW(c); list.Add((c, x, cw)); x += cw; }
        for (var c = Math.Max(LeftCol, FC); x < w && c < Addr.MaxCols; c++) { var cw = ColW(c); list.Add((c, x, cw)); x += cw; }
        return list;
    }

    void EnsureVisible()
    {
        if (Bounds.Height <= 0) return;
        TopRow = Math.Max(TopRow, FR); LeftCol = Math.Max(LeftCol, FC);
        if (Active.Row >= FR)
        {
            if (Active.Row < TopRow) TopRow = Active.Row;
            for (var guard = 0; guard < 10_000 && !FullyVisibleRow(Active.Row); guard++) TopRow++;
        }
        if (Active.Col >= FC)
        {
            if (Active.Col < LeftCol) LeftCol = Active.Col;
            for (var guard = 0; guard < 10_000 && !FullyVisibleCol(Active.Col) && LeftCol < Active.Col; guard++) LeftCol++;
        }
    }

    bool FullyVisibleRow(int row) => RowLayout().Any(x => x.Row == row && x.Y + RowH <= Bounds.Height);
    bool FullyVisibleCol(int col) => ColLayout().Any(x => x.Col == col && x.X + x.W <= Bounds.Width);

    /// <summary>Bildschirm-Rechteck einer Zelle (für das Bearbeitungsfeld). Null, wenn nicht sichtbar.</summary>
    public Rect? CellRect(int row, int col)
    {
        var r = RowLayout().FirstOrDefault(x => x.Row == row); var c = ColLayout().FirstOrDefault(x => x.Col == col);
        if (r.Y <= 0 || c.X <= 0 || r.Row != row || c.Col != col) return null;
        return new Rect(c.X, r.Y, c.W, RowH);
    }

    (int Row, int Col)? HitCell(Point p)
    {
        if (p.X < HeaderW && p.Y < HeaderH) return null;
        var rows = RowLayout(); var cols = ColLayout();
        var row = p.Y < HeaderH ? -1 : rows.Count == 0 ? 0 : rows.LastOrDefault(x => x.Y <= p.Y, rows[0]).Row;
        var col = p.X < HeaderW ? -1 : cols.Count == 0 ? 0 : cols.LastOrDefault(x => x.X <= p.X, cols[0]).Col;
        return (row, col);
    }

    int ColEdgeAt(Point p)
    {
        if (p.Y > HeaderH) return -1;
        foreach (var (c, x, w) in ColLayout()) if (Math.Abs(p.X - (x + w)) <= 4) return c;
        return -1;
    }

    /// <summary>Zeilen/Spalten oberhalb bzw. links der aktiven Zelle fixieren; erneut = aufheben.</summary>
    public void ToggleFreeze()
    {
        if (Sheet.FreezeRows > 0 || Sheet.FreezeCols > 0) { Sheet.FreezeRows = Sheet.FreezeCols = 0; }
        else { Sheet.FreezeRows = Active.Row == 0 && Active.Col == 0 ? 1 : Active.Row; Sheet.FreezeCols = Active.Col; }
        TopRow = Math.Max(TopRow, FR); LeftCol = Math.Max(LeftCol, FC);
        InvalidateVisual(); Changed?.Invoke();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var p = e.GetPosition(this);
        var edge = ColEdgeAt(p);
        if (edge >= 0) { _dragCol = edge; _dragStartX = p.X; _dragStartW = Sheet.ColWidth(edge); e.Pointer.Capture(this); return; }
        if (HitCell(p) is not { } hit) { SelectRange(0, 0, Math.Max(0, Sheet.MaxRow), Math.Max(0, Sheet.MaxCol)); return; }
        if (hit.Row < 0) { SelectRange(0, hit.Col, Math.Max(Sheet.MaxRow, RowLayout().Count), hit.Col); return; }    // Spaltenkopf
        if (hit.Col < 0) { SelectRange(hit.Row, 0, hit.Row, Math.Max(Sheet.MaxCol, 10)); return; }              // Zeilenkopf
        if (e.ClickCount == 2) { Select(hit.Row, hit.Col); EditRequested?.Invoke(null); return; }
        Select(hit.Row, hit.Col, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        _selecting = true; e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_dragCol >= 0) { Sheet.ColWidths[_dragCol] = Math.Round(Math.Clamp(_dragStartW + (p.X - _dragStartX) / CharW, 2, 120), 1); InvalidateVisual(); return; }
        Cursor = ColEdgeAt(p) >= 0 ? new Cursor(StandardCursorType.SizeWestEast) : Cursor.Default;
        if (_selecting && HitCell(p) is { Row: >= 0, Col: >= 0 } hit && hit != Active) { Active = hit; InvalidateVisual(); SelectionChanged?.Invoke(); }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragCol >= 0) { _dragCol = -1; Changed?.Invoke(); }
        _selecting = false; e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y)) LeftCol = Math.Max(0, LeftCol - Math.Sign(e.Delta.X != 0 ? e.Delta.X : e.Delta.Y) * 2);
        else TopRow = Math.Max(FR, TopRow - (int)Math.Round(e.Delta.Y * 3));
        LeftCol = Math.Max(FC, LeftCol);
        InvalidateVisual(); e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift); var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var (r, c) = Active;
        switch (e.Key)
        {
            case Key.Up: Select(r - 1, c, shift); break;
            case Key.Down: Select(r + 1, c, shift); break;
            case Key.Left: Select(r, c - 1, shift); break;
            case Key.Right: Select(r, c + 1, shift); break;
            case Key.Enter: Select(shift ? r - 1 : r + 1, c); break;
            case Key.Tab: Select(r, shift ? c - 1 : c + 1); break;
            case Key.PageDown: Select(r + Math.Max(1, RowLayout().Count - FR - 1), c, shift); break;
            case Key.PageUp: Select(r - Math.Max(1, RowLayout().Count - FR - 1), c, shift); break;
            case Key.Home: Select(ctrl ? 0 : r, 0, shift); break;
            case Key.End when ctrl: Select(Math.Max(0, Sheet.MaxRow), Math.Max(0, Sheet.MaxCol), shift); break;
            case Key.F2: EditRequested?.Invoke(null); break;
            default:
                if (ctrl || e.Key is Key.Delete or Key.Back) { Command?.Invoke(e); return; }
                return;
        }
        e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0])) { EditRequested?.Invoke(e.Text); e.Handled = true; }
    }

    public override void Render(DrawingContext ctx)
    {
        var bg = Dark ? Color.Parse("#0B1730") : Colors.White;
        var line = new Pen(new SolidColorBrush(Dark ? Color.Parse("#22FFFFFF") : Color.Parse("#FFD8DEE8")), 1);
        var headBg = new SolidColorBrush(Dark ? Color.Parse("#14254A") : Color.Parse("#FFEEF2F8"));
        var headSel = new SolidColorBrush(Dark ? Color.Parse("#2F4F86") : Color.Parse("#FFCFE0FA"));
        var fg = new SolidColorBrush(Dark ? Color.Parse("#E6EDF7") : Color.Parse("#FF101828"));
        var muted = new SolidColorBrush(Dark ? Color.Parse("#9FB1CF") : Color.Parse("#FF5B6B82"));
        var err = new SolidColorBrush(Color.Parse("#FFF87171"));
        var accent = Color.Parse("#2F86F6");
        ctx.FillRectangle(new SolidColorBrush(bg), new Rect(Bounds.Size));
        var (sTop, sLeft, sBottom, sRight) = Selection;
        var face = new Typeface("Inter"); var bold = new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold);
        // Spaltenköpfe
        var colX = ColLayout(); var rowY = RowLayout();
        // Zellen
        foreach (var (r, ry) in rowY)
            foreach (var (c, cx, cw) in colX)
            {
                var cell = Sheet.Get(r, c);
                var rect = new Rect(cx, ry, cw, RowH);
                if (cell?.Fill is { } fill && Color.TryParse(fill, out var fc)) ctx.FillRectangle(new SolidColorBrush(fc), rect);
                if (r >= sTop && r <= sBottom && c >= sLeft && c <= sRight && (sTop != sBottom || sLeft != sRight)) ctx.FillRectangle(new SolidColorBrush(accent, 0.18), rect);
                ctx.DrawLine(line, rect.BottomLeft, rect.BottomRight); ctx.DrawLine(line, rect.TopRight, rect.BottomRight);
                if (cell is null || cell.Raw.Length == 0) { if (cell?.Border == true) ctx.DrawRectangle(new Pen(fg, 1), rect.Deflate(0.5)); continue; }
                var v = Calc.Value(Sheet, r, c);
                var text = SheetFormat.Display(v, cell.Format);
                var brush = v.IsError ? err : cell.Fill is not null && !Dark ? new SolidColorBrush(Color.Parse("#FF101828")) : cell.Fill is not null ? new SolidColorBrush(Color.Parse("#FF101828")) : fg;
                var ft = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, cell.Bold ? bold : face, 13, brush) { MaxTextWidth = Math.Max(1, cw - 8), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                var rightAlign = v.IsNumber || v.Kind == CellValue.K.Bool;
                var tx = rightAlign ? cx + cw - 4 - ft.Width : cx + 4;
                using (ctx.PushClip(rect)) ctx.DrawText(ft, new Point(tx, ry + (RowH - ft.Height) / 2));
                if (cell.Border) ctx.DrawRectangle(new Pen(fg, 1), rect.Deflate(0.5));
            }
        // Köpfe
        ctx.FillRectangle(headBg, new Rect(0, 0, Bounds.Width, HeaderH));
        ctx.FillRectangle(headBg, new Rect(0, 0, HeaderW, Bounds.Height));
        foreach (var (c, cx, cw) in colX)
        {
            if (c >= sLeft && c <= sRight) ctx.FillRectangle(headSel, new Rect(cx, 0, cw, HeaderH));
            var ft = new FormattedText(Addr.Col(c), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 12, muted);
            ctx.DrawText(ft, new Point(cx + (cw - ft.Width) / 2, (HeaderH - ft.Height) / 2));
            ctx.DrawLine(line, new Point(cx + cw, 0), new Point(cx + cw, HeaderH));
        }
        foreach (var (r, ry) in rowY)
        {
            if (r >= sTop && r <= sBottom) ctx.FillRectangle(headSel, new Rect(0, ry, HeaderW, RowH));
            var ft = new FormattedText((r + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 12, muted);
            ctx.DrawText(ft, new Point(HeaderW - 6 - ft.Width, ry + (RowH - ft.Height) / 2));
            ctx.DrawLine(line, new Point(0, ry + RowH), new Point(HeaderW, ry + RowH));
        }
        ctx.DrawLine(line, new Point(HeaderW, 0), new Point(HeaderW, Bounds.Height));
        ctx.DrawLine(line, new Point(0, HeaderH), new Point(Bounds.Width, HeaderH));
        // Trennlinie unter/neben fixierten Zeilen und Spalten
        var freezePen = new Pen(new SolidColorBrush(Color.Parse("#FF7A8699")), 2);
        if (FR > 0 && rowY.FirstOrDefault(v => v.Row >= FR) is { Y: > 0 } fy) ctx.DrawLine(freezePen, new Point(0, fy.Y), new Point(Bounds.Width, fy.Y));
        if (FC > 0 && colX.FirstOrDefault(v => v.Col >= FC) is { X: > 0 } fx) ctx.DrawLine(freezePen, new Point(fx.X, 0), new Point(fx.X, Bounds.Height));
        // Auswahlrahmen um den sichtbaren Teil der Markierung
        var selRows = rowY.Where(v => v.Row >= sTop && v.Row <= sBottom).ToList(); var selCols = colX.Where(v => v.Col >= sLeft && v.Col <= sRight).ToList();
        if (selRows.Count > 0 && selCols.Count > 0)
            ctx.DrawRectangle(new Pen(new SolidColorBrush(accent), 2), new Rect(new Point(selCols.Min(v => v.X), selRows.Min(v => v.Y)), new Point(selCols.Max(v => v.X + v.W), selRows.Max(v => v.Y) + RowH)).Deflate(1));
        if (CellRect(Active.Row, Active.Col) is { } act) ctx.DrawRectangle(new Pen(new SolidColorBrush(accent), 2.5), act.Deflate(1));
    }
}
