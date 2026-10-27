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
    int VisibleRows() => Math.Max(1, (int)((Bounds.Height - HeaderH) / RowH));
    int VisibleCols() { var w = Bounds.Width - HeaderW; var n = 0; for (var c = LeftCol; w > 0 && n < 200; c++, n++) w -= ColW(c); return Math.Max(1, n - 1); }

    bool RowHidden(int r) => Sheet.HiddenRows.Contains(r);

    void EnsureVisible()
    {
        if (Bounds.Height <= 0) return;
        if (Active.Row < TopRow) TopRow = Active.Row;
        var rows = VisibleRows(); if (Active.Row >= TopRow + rows) TopRow = Active.Row - rows + 1;
        if (Active.Col < LeftCol) LeftCol = Active.Col;
        while (Active.Col >= LeftCol + VisibleCols() && LeftCol < Active.Col) LeftCol++;
    }

    /// <summary>Bildschirm-Rechteck einer Zelle (für das Bearbeitungsfeld). Null, wenn nicht sichtbar.</summary>
    public Rect? CellRect(int row, int col)
    {
        if (row < TopRow || col < LeftCol) return null;
        double y = HeaderH; for (var r = TopRow; r < row; r++) if (!RowHidden(r)) y += RowH;
        double x = HeaderW; for (var c = LeftCol; c < col; c++) x += ColW(c);
        if (y > Bounds.Height || x > Bounds.Width) return null;
        return new Rect(x, y, ColW(col), RowH);
    }

    (int Row, int Col)? HitCell(Point p)
    {
        if (p.X < HeaderW && p.Y < HeaderH) return null;
        int row = TopRow; double y = HeaderH;
        if (p.Y >= HeaderH) { while (true) { if (RowHidden(row)) { row++; continue; } if (y + RowH > p.Y) break; y += RowH; row++; if (row > TopRow + 500) break; } }
        int col = LeftCol; double x = HeaderW;
        if (p.X >= HeaderW) { while (x + ColW(col) <= p.X && col < LeftCol + 300) { x += ColW(col); col++; } }
        return (p.Y < HeaderH ? -1 : row, p.X < HeaderW ? -1 : col);
    }

    int ColEdgeAt(Point p)
    {
        if (p.Y > HeaderH) return -1;
        double x = HeaderW;
        for (var c = LeftCol; x < Bounds.Width; c++) { x += ColW(c); if (Math.Abs(p.X - x) <= 4) return c; }
        return -1;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var p = e.GetPosition(this);
        var edge = ColEdgeAt(p);
        if (edge >= 0) { _dragCol = edge; _dragStartX = p.X; _dragStartW = Sheet.ColWidth(edge); e.Pointer.Capture(this); return; }
        if (HitCell(p) is not { } hit) { SelectRange(0, 0, Math.Max(0, Sheet.MaxRow), Math.Max(0, Sheet.MaxCol)); return; }
        if (hit.Row < 0) { SelectRange(0, hit.Col, Math.Max(Sheet.MaxRow, VisibleRows()), hit.Col); return; }    // Spaltenkopf
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
        else TopRow = Math.Max(0, TopRow - (int)Math.Round(e.Delta.Y * 3));
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
            case Key.PageDown: Select(r + VisibleRows(), c, shift); break;
            case Key.PageUp: Select(r - VisibleRows(), c, shift); break;
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
        double x = HeaderW; var colX = new List<(int Col, double X, double W)>();
        for (var c = LeftCol; x < Bounds.Width; c++) { var w = ColW(c); colX.Add((c, x, w)); x += w; }
        double y = HeaderH; var rowY = new List<(int Row, double Y)>();
        for (var r = TopRow; y < Bounds.Height && r < Addr.MaxRows; r++) { if (RowHidden(r)) continue; rowY.Add((r, y)); y += RowH; }
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
        // Auswahlrahmen
        if (CellRect(sTop < TopRow ? TopRow : sTop, sLeft < LeftCol ? LeftCol : sLeft) is { } a && CellRect(Math.Max(sBottom, TopRow), Math.Max(sRight, LeftCol)) is { } b)
            ctx.DrawRectangle(new Pen(new SolidColorBrush(accent), 2), new Rect(a.TopLeft, b.BottomRight).Deflate(1));
        if (CellRect(Active.Row, Active.Col) is { } act) ctx.DrawRectangle(new Pen(new SolidColorBrush(accent), 2.5), act.Deflate(1));
    }
}
