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
        // T-0701: in eine verbundene Zelle springen markiert den ganzen Bereich, bearbeitet wird oben links
        if (!extend && Sheet.MergeAt(row, col) is { } m) { Active = (m.Top, m.Left); Anchor = (m.Bottom, m.Right); }
        EnsureVisible(); InvalidateVisual(); SelectionChanged?.Invoke();
    }

    /// <summary>Auswahl und Bildlauf nach dem Neuzeichnen der Seite wiederherstellen.</summary>
    public void Restore((int Row, int Col) active, (int Row, int Col) anchor, int top, int left)
    {
        Active = active; Anchor = anchor; TopRow = top; LeftCol = left;
        if (active == anchor && Sheet.MergeAt(active.Row, active.Col) is { } m) { Active = (m.Top, m.Left); Anchor = (m.Bottom, m.Right); } // T-0701: verbundene Zelle ganz markieren
        InvalidateVisual();
    }

    public void SelectRange(int top, int left, int bottom, int right)
    {
        // T-0701: eine einzelne Zelle in einem Verbund markiert den ganzen Verbund (aktiv bleibt oben links); ein Bereich wächst um angeschnittene Verbünde
        if (top == bottom && left == right && Sheet.MergeAt(top, left) is { } one) { Active = (one.Top, one.Left); Anchor = (one.Bottom, one.Right); }
        else
        {
            for (var grew = true; grew;)
            {
                grew = false;
                foreach (var m in Sheet.Merges)
                    if (m.Overlaps(new MergeRange(top, left, bottom, right)) && (m.Top < top || m.Left < left || m.Bottom > bottom || m.Right > right))
                    { top = Math.Min(top, m.Top); left = Math.Min(left, m.Left); bottom = Math.Max(bottom, m.Bottom); right = Math.Max(right, m.Right); grew = true; }
            }
            Anchor = (top, left); Active = (bottom, right);
        }
        EnsureVisible(); InvalidateVisual(); SelectionChanged?.Invoke();
    }

    double ColW(int c) => Math.Max(3, Sheet.ColWidth(c)) * CharW + 10;
    /// <summary>Punkt (Zeilenhöhe in der Datei) → Bildpunkte im Raster: Excels Standardhöhe 15 pt ergibt die gewohnten 24 Bildpunkte.</summary>
    public const double PtPx = 1.6;
    /// <summary>Höhe einer Zeile in Bildpunkten (T-0701: eigene Zeilenhöhe aus der Datei).</summary>
    public double RowHeight(int r) => Sheet.RowHeights.TryGetValue(r, out var pt) ? Math.Max(4, pt * PtPx) : RowH;
    bool RowHidden(int r) => Sheet.HiddenRows.Contains(r) || Sheet.RowHeights.TryGetValue(r, out var pt) && pt <= 0;
    int FR => Math.Max(0, Sheet.FreezeRows);
    int FC => Math.Max(0, Sheet.FreezeCols);

    /// <summary>Sichtbare Zeilen mit Lage: erst die fixierten (E1 „Fenster fixieren“), dann ab <see cref="TopRow"/>.</summary>
    List<(int Row, double Y)> RowLayout()
    {
        var list = new List<(int, double)>(); double y = HeaderH; var h = Math.Max(Bounds.Height, HeaderH + RowH);
        for (var r = 0; r < FR && y < h; r++) if (!RowHidden(r)) { list.Add((r, y)); y += RowHeight(r); }
        for (var r = Math.Max(TopRow, FR); y < h && r < Addr.MaxRows; r++) if (!RowHidden(r)) { list.Add((r, y)); y += RowHeight(r); }
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
            for (var guard = 0; guard < 10_000 && !FullyVisibleRow(Active.Row) && TopRow < Active.Row; guard++) TopRow++;
        }
        if (Active.Col >= FC)
        {
            if (Active.Col < LeftCol) LeftCol = Active.Col;
            for (var guard = 0; guard < 10_000 && !FullyVisibleCol(Active.Col) && LeftCol < Active.Col; guard++) LeftCol++;
        }
    }

    bool FullyVisibleRow(int row) => RowLayout().Any(x => x.Row == row && x.Y + RowHeight(row) <= Bounds.Height);
    bool FullyVisibleCol(int col) => ColLayout().Any(x => x.Col == col && x.X + x.W <= Bounds.Width);

    /// <summary>Bildschirm-Rechteck einer Zelle (für das Bearbeitungsfeld) – bei verbundenen Zellen der sichtbare Teil des ganzen Bereichs. Null, wenn nicht sichtbar.</summary>
    public Rect? CellRect(int row, int col)
    {
        var rows = RowLayout(); var cols = ColLayout();
        var r = rows.FirstOrDefault(x => x.Row == row); var c = cols.FirstOrDefault(x => x.Col == col);
        if (r.Y <= 0 || c.X <= 0 || r.Row != row || c.Col != col) return null;
        if (Sheet.MergeAt(row, col) is { } m) return MergeRect(m, rows, cols);
        return new Rect(c.X, r.Y, c.W, RowHeight(row));
    }

    Rect? MergeRect(MergeRange m, List<(int Row, double Y)> rows, List<(int Col, double X, double W)> cols)
    {
        var rs = rows.Where(x => x.Row >= m.Top && x.Row <= m.Bottom).ToList(); var cs = cols.Where(x => x.Col >= m.Left && x.Col <= m.Right).ToList();
        if (rs.Count == 0 || cs.Count == 0) return null;
        return new Rect(new Point(cs[0].X, rs[0].Y), new Point(cs[^1].X + cs[^1].W, rs[^1].Y + RowHeight(rs[^1].Row)));
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
        var right = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        var edge = ColEdgeAt(p);
        if (edge >= 0 && !right) { _dragCol = edge; _dragStartX = p.X; _dragStartW = Sheet.ColWidth(edge); e.Pointer.Capture(this); return; }
        if (HitCell(p) is not { } hit) { SelectRange(0, 0, Math.Max(0, Sheet.MaxRow), Math.Max(0, Sheet.MaxCol)); return; }
        // T-0701: Rechtsklick in die Markierung lässt sie stehen (das Kontextmenü gilt für die ganze Markierung)
        var (st, sl, sb, sr) = Selection;
        if (right && hit.Row >= st && hit.Row <= sb && hit.Col >= sl && hit.Col <= sr) return;
        if (hit.Row < 0) { SelectRange(0, hit.Col, Math.Max(Sheet.MaxRow, RowLayout().Count), hit.Col); return; }    // Spaltenkopf
        if (hit.Col < 0) { SelectRange(hit.Row, 0, hit.Row, Math.Max(Sheet.MaxCol, 10)); return; }              // Zeilenkopf
        if (e.ClickCount == 2 && !right) { Select(hit.Row, hit.Col); EditRequested?.Invoke(null); return; }
        Select(hit.Row, hit.Col, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        if (!right) { _selecting = true; e.Pointer.Capture(this); }
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
        // T-0701: aus einer verbundenen Zelle heraus springt man über den ganzen Bereich
        var m = shift ? null : Sheet.MergeAt(r, c);
        switch (e.Key)
        {
            case Key.Up: Select(r - 1, c, shift); break;
            case Key.Down: Select((m?.Bottom ?? r) + 1, c, shift); break;
            case Key.Left: Select(r, c - 1, shift); break;
            case Key.Right: Select(r, (m?.Right ?? c) + 1, shift); break;
            case Key.Enter: Select(shift ? r - 1 : (m?.Bottom ?? r) + 1, c); break;
            case Key.Tab: Select(r, shift ? c - 1 : (m?.Right ?? c) + 1); break;
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

    /// <summary>T-0701: Linie eines Zellrahmens nach Art und Farbe aus der Datei.</summary>
    static Pen? BorderPen(BorderLine? l, IBrush dflt)
    {
        if (l is null) return null;
        IBrush brush = l.Color is { } c && Color.TryParse(c, out var col) ? new SolidColorBrush(col) : dflt;
        return l.Style switch
        {
            "medium" => new Pen(brush, 2), "thick" => new Pen(brush, 3), "hair" => new Pen(brush, 0.6), "double" => new Pen(brush, 3),
            "dashed" or "mediumDashed" => new Pen(brush, l.Style == "dashed" ? 1 : 2, new DashStyle([4, 2], 0)),
            "dotted" => new Pen(brush, 1, new DashStyle([1, 2], 0)),
            "dashDot" or "mediumDashDot" or "slantDashDot" => new Pen(brush, l.Style == "dashDot" ? 1 : 2, new DashStyle([4, 2, 1, 2], 0)),
            "dashDotDot" or "mediumDashDotDot" => new Pen(brush, l.Style == "dashDotDot" ? 1 : 2, new DashStyle([4, 2, 1, 2, 1, 2], 0)),
            _ => new Pen(brush, 1),
        };
    }

    /// <summary>T-0701: Zellinhalt mit Schrift, Farbe (auch aus dem Formatcode), Ausrichtung, Umbruch, Einzug und Drehung zeichnen.</summary>
    void DrawValue(DrawingContext ctx, Cell cell, int r, int c, Rect rect, string? condFill, IBrush fg, IBrush err, CultureInfo de)
    {
        var v = Calc.Value(Sheet, r, c);
        string? codeColor = null;
        var text = cell.NumCode is { } code && v.IsNumber ? SheetNumberFormat.Format(v.Number, code, out codeColor) : SheetFormat.Display(v, cell);
        IBrush autoColor = (condFill ?? cell.Fill) is not null ? new SolidColorBrush(Color.Parse("#FF101828")) : fg;
        IBrush brush = v.IsError ? err : codeColor is { } cc && Color.TryParse(cc, out var ccol) ? new SolidColorBrush(ccol) : cell.FontColor is { } fcl && Color.TryParse(fcl, out var fcol) ? new SolidColorBrush(fcol) : autoColor;
        var size = Math.Max(6, (cell.FontSize ?? Doc.FontSize) * 13.0 / 11.0);
        var tf = new Typeface(cell.FontName is { Length: > 0 } fn ? new FontFamily(fn + ", Inter") : new FontFamily("Inter"), cell.Italic ? FontStyle.Italic : FontStyle.Normal, cell.Bold ? FontWeight.SemiBold : FontWeight.Normal);
        var indent = cell.Indent * 8.0;
        var ft = new FormattedText(text, de, FlowDirection.LeftToRight, tf, size, brush)
        {
            MaxTextWidth = Math.Max(1, rect.Width - 8 - indent), MaxLineCount = cell.Wrap ? Math.Max(1, (int)(rect.Height / (size * 1.25))) : 1,
            Trimming = cell.Wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
        };
        var deco = new TextDecorationCollection();
        if (cell.Underline is not null) deco.AddRange(TextDecorations.Underline);
        if (cell.Strike) deco.AddRange(TextDecorations.Strikethrough);
        if (deco.Count > 0) ft.SetTextDecorations(deco);
        var align = cell.HAlign ?? (v.IsNumber || v.Kind == CellValue.K.Bool ? "right" : v.IsError ? "center" : "left");
        var tx = align switch
        {
            "center" or "centerContinuous" or "distributed" => rect.X + (rect.Width - ft.WidthIncludingTrailingWhitespace) / 2,
            "right" => rect.Right - 4 - indent - ft.WidthIncludingTrailingWhitespace,
            _ => rect.X + 4 + indent,
        };
        var ty = cell.VAlign switch { "top" => rect.Y + 2, "bottom" => rect.Bottom - 2 - ft.Height, _ => rect.Y + (rect.Height - ft.Height) / 2 };
        using (ctx.PushClip(rect))
        {
            if (cell.Rotation is > 0 and <= 180)
            {
                var deg = cell.Rotation <= 90 ? -cell.Rotation : cell.Rotation - 90;
                var center = new Point(tx + ft.Width / 2, ty + ft.Height / 2);
                using (ctx.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateRotation(deg * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y)))
                    ctx.DrawText(ft, new Point(tx, ty));
            }
            else ctx.DrawText(ft, new Point(tx, ty));
        }
    }

    public override void Render(DrawingContext ctx)
    {
        var bg = Dark ? Color.Parse("#1F1F1F") : Colors.White;
        var line = new Pen(new SolidColorBrush(Dark ? Color.Parse("#22FFFFFF") : Color.Parse("#FFD8DEE8")), 1);
        var headBg = new SolidColorBrush(Dark ? Color.Parse("#323232") : Color.Parse("#FFEEF2F8"));
        var headSel = new SolidColorBrush(Dark ? Color.Parse("#4A3F1A") : Color.Parse("#FFF3E7BF"));
        var fg = new SolidColorBrush(Dark ? Color.Parse("#E6EDF7") : Color.Parse("#FF101828"));
        var muted = new SolidColorBrush(Dark ? Color.Parse("#9FB1CF") : Color.Parse("#FF5B6B82"));
        var err = new SolidColorBrush(Color.Parse("#FFF87171"));
        var accent = Color.Parse("#C9A227"); // Gold statt Blau (Dunkel-Gold-Look)
        ctx.FillRectangle(new SolidColorBrush(bg), new Rect(Bounds.Size));
        var (sTop, sLeft, sBottom, sRight) = Selection;
        var face = new Typeface("Inter");
        // Spaltenköpfe
        var colX = ColLayout(); var rowY = RowLayout();
        // Zellen
        var hasRules = Sheet.CondRules.Count > 0; var noteBrush = new SolidColorBrush(Color.Parse("#FFE11D48"));
        var drawn = new HashSet<MergeRange>(); var de = CultureInfo.GetCultureInfo("de-DE");
        foreach (var (r0, ry0) in rowY)
            foreach (var (c0, cx0, cw0) in colX)
            {
                var r = r0; var c = c0; var rect = new Rect(cx0, ry0, cw0, RowHeight(r0));
                // T-0701: verbundene Zellen als ein Feld zeichnen – Inhalt und Aussehen aus der Zelle oben links
                if (Sheet.MergeAt(r0, c0) is { } merge)
                {
                    if (!drawn.Add(merge) || MergeRect(merge, rowY, colX) is not { } mr) continue;
                    rect = mr; r = merge.Top; c = merge.Left;
                }
                var cell = Sheet.Get(r, c);
                var look = hasRules ? SheetExtras.Look(Sheet, Calc, r, c) : default;
                if ((look.Fill ?? cell?.Fill) is { } fill && Color.TryParse(fill, out var fc)) ctx.FillRectangle(new SolidColorBrush(fc), rect);
                if (look.Bar is { } bar && bar > 0 && Color.TryParse(look.BarColor, out var bc)) ctx.FillRectangle(new SolidColorBrush(bc, 0.55), new Rect(rect.X + 2, rect.Y + 3, Math.Max(1, (rect.Width - 4) * bar), Math.Max(1, rect.Height - 6)));
                if (cell?.Note is { Length: > 0 }) ctx.DrawGeometry(noteBrush, null, new PolylineGeometry([new Point(rect.Right - 7, rect.Y), new Point(rect.Right, rect.Y), new Point(rect.Right, rect.Y + 7)], true));
                if (r0 >= sTop && r0 <= sBottom && c0 >= sLeft && c0 <= sRight && (sTop != sBottom || sLeft != sRight)) ctx.FillRectangle(new SolidColorBrush(accent, 0.18), rect);
                ctx.DrawLine(line, rect.BottomLeft, rect.BottomRight); ctx.DrawLine(line, rect.TopRight, rect.BottomRight);
                if (cell is not null && cell.Raw.Length > 0) DrawValue(ctx, cell, r, c, rect, look.Fill, fg, err, de);
                // Rahmen: dünn rundum (Trendo) oder je Seite mit Linienart und Farbe (aus der Datei)
                if (cell?.Borders is { } b)
                {
                    if (BorderPen(b.Top, fg) is { } pt) ctx.DrawLine(pt, rect.TopLeft, rect.TopRight);
                    if (BorderPen(b.Bottom, fg) is { } pb) ctx.DrawLine(pb, rect.BottomLeft - new Point(0, 0.5), rect.BottomRight - new Point(0, 0.5));
                    if (BorderPen(b.Left, fg) is { } pl) ctx.DrawLine(pl, rect.TopLeft, rect.BottomLeft);
                    if (BorderPen(b.Right, fg) is { } pr) ctx.DrawLine(pr, rect.TopRight - new Point(0.5, 0), rect.BottomRight - new Point(0.5, 0));
                }
                else if (cell?.Border == true) ctx.DrawRectangle(new Pen(fg, 1), rect.Deflate(0.5));
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
            var rh = RowHeight(r);
            if (r >= sTop && r <= sBottom) ctx.FillRectangle(headSel, new Rect(0, ry, HeaderW, rh));
            var ft = new FormattedText((r + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 12, muted);
            ctx.DrawText(ft, new Point(HeaderW - 6 - ft.Width, ry + (rh - ft.Height) / 2));
            ctx.DrawLine(line, new Point(0, ry + rh), new Point(HeaderW, ry + rh));
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
            ctx.DrawRectangle(new Pen(new SolidColorBrush(accent), 2), new Rect(new Point(selCols.Min(v => v.X), selRows.Min(v => v.Y)), new Point(selCols.Max(v => v.X + v.W), selRows.Max(v => v.Y + RowHeight(v.Row)))).Deflate(1));
        if (CellRect(Active.Row, Active.Col) is { } act) ctx.DrawRectangle(new Pen(new SolidColorBrush(accent), 2.5), act.Deflate(1));
    }
}
