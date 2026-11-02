using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Arch;
using Trendo.Core.Messdaten;

namespace Trendo.App;

/// <summary>T-0261: Datentabelle zum direkten Eintippen wie in einer Tabellenkalkulation – Pfeiltasten, Enter (nach unten), Tab (nach rechts),
/// Tippen überschreibt, F2/Doppelklick bearbeitet, Entf leert. Kopfzeile mit Name und Einheit; Klick auf den Kopf wählt die Spalte.</summary>
public sealed class MeasureGrid : Panel
{
    readonly MeasureSheet _s; readonly Action<int, int, string> _commit; readonly Action<int, int> _select;
    readonly TextBox _edit = new() { IsVisible = false, FontSize = 13, Padding = new Thickness(4, 2), BorderThickness = new Thickness(2), BorderBrush = Brush.Parse("#F5A524") };
    public int Col { get; private set; }
    public int Row { get; private set; }
    public int Top { get; set; }
    const double HeadH = 42, RowW = 44, CellW = 104, CellH = 26;

    public MeasureGrid(MeasureSheet s, int col, int row, Action<int, int, string> commit, Action<int, int> select)
    {
        _s = s; Col = Math.Clamp(col, 0, Math.Max(0, s.Columns.Count - 1)); Row = Math.Max(0, row); _commit = commit; _select = select;
        Focusable = true; ClipToBounds = true; Background = Brushes.Transparent;
        Children.Add(_surface = new Surface(Draw)); Children.Add(_edit);
        _edit.KeyDown += (_, e) =>
        {
            // Nach dem Speichern wird die Seite neu aufgebaut – dann darf die alte Tabelle den Fokus nicht mehr an sich ziehen
            if (e.Key is Key.Enter or Key.Tab) { Finish(true); e.Handled = true; if (VisualRoot is not null) { Move(e.Key == Key.Tab ? 1 : 0, e.Key == Key.Enter ? 1 : 0); Focus(); } }
            else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; if (VisualRoot is not null) Focus(); }
        };
        _edit.LostFocus += (_, _) => { if (_edit.IsVisible) Finish(true); };
    }

    public bool Editing => _edit.IsVisible;
    public bool IsAttached() => VisualRoot is not null;
    readonly Surface _surface; Rect _editRect;
    sealed class Surface(Action<DrawingContext> draw) : Control { public override void Render(DrawingContext ctx) => draw(ctx); }
    protected override Size ArrangeOverride(Size finalSize) { _surface.Arrange(new Rect(finalSize)); _edit.Arrange(_editRect); return finalSize; }
    new void InvalidateVisual() { _surface.InvalidateVisual(); base.InvalidateVisual(); }

    (double X, double Y) CellPos(int c, int r) => (RowW + c * CellW, HeadH + (r - Top) * CellH);
    int VisibleRows => Math.Max(1, (int)((Bounds.Height - HeadH) / CellH));

    public void Begin(string? initial = null)
    {
        if (_s.Columns.Count == 0) return;
        var (x, y) = CellPos(Col, Row);
        _editRect = new Rect(x, y - 1, CellW + 1, CellH + 2); InvalidateArrange();
        _edit.Text = initial ?? (_s.Get(Col, Row) is { } v ? v.ToString("0.##########", CultureInfo.GetCultureInfo("de-DE")) : "");
        _edit.IsVisible = true; _edit.Focus(); _edit.CaretIndex = _edit.Text?.Length ?? 0;
    }

    void Finish(bool keep)
    {
        if (!_edit.IsVisible) return; _edit.IsVisible = false;
        if (keep) { var t = _edit.Text ?? ""; var old = _s.Get(Col, Row); var same = old is null ? t.Trim() == "" : t.Trim() == old.Value.ToString("0.##########", CultureInfo.GetCultureInfo("de-DE")); if (!same) _commit(Col, Row, t); }
        InvalidateVisual();
    }

    void Move(int dc, int dr)
    {
        Col = Math.Clamp(Col + dc, 0, Math.Max(0, _s.Columns.Count - 1)); Row = Math.Max(0, Row + dr);
        if (Row < Top) Top = Row; if (Row >= Top + VisibleRows) Top = Row - VisibleRows + 1;
        _select(Col, Row); InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); if (Editing) Finish(true); Focus();
        var p = e.GetPosition(this);
        if (p.X < RowW) return;
        var c = (int)((p.X - RowW) / CellW); if (c >= _s.Columns.Count) return;
        if (p.Y < HeadH) { Col = c; _select(Col, -1); InvalidateVisual(); return; } // Spaltenkopf: Spalte wählen
        Col = c; Row = Top + (int)((p.Y - HeadH) / CellH); _select(Col, Row); InvalidateVisual();
        if (e.ClickCount == 2) Begin();
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); Top = Math.Max(0, Top - Math.Sign(e.Delta.Y) * 3); InvalidateVisual(); e.Handled = true; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (Editing) return;
        switch (e.Key)
        {
            case Key.Up: Move(0, -1); break; case Key.Down: case Key.Enter: Move(0, 1); break;
            case Key.Left: Move(-1, 0); break; case Key.Right: case Key.Tab: Move(1, 0); break;
            case Key.F2: Begin(); break;
            case Key.Delete: case Key.Back: if (_s.Get(Col, Row) is not null) _commit(Col, Row, ""); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!Editing && e.Text is { Length: > 0 } t && !char.IsControl(t[0])) { Begin(t); e.Handled = true; }
    }

    void Draw(DrawingContext ctx)
    {
        var size = Bounds.Size; ctx.FillRectangle(new SolidColorBrush(Color.Parse("#222222")), new Rect(size));
        var line = new Pen(new SolidColorBrush(Color.Parse("#22FFFFFF")), 1); var face = new Typeface("Inter"); var bold = new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold);
        var text = new SolidColorBrush(Color.Parse("#E6EDF7")); var muted = new SolidColorBrush(Color.Parse("#8FA3C2")); var de = CultureInfo.GetCultureInfo("de-DE");
        var rows = VisibleRows;
        // Spaltenkopf
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#343434")), new Rect(0, 0, size.Width, HeadH));
        for (var c = 0; c < _s.Columns.Count; c++)
        {
            var (x, _) = CellPos(c, 0); var col = _s.Columns[c];
            if (c == Col) ctx.FillRectangle(new SolidColorBrush(Color.Parse("#33C9A227")), new Rect(x, 0, CellW, size.Height));
            var dot = ChartDrawing.Palette[Math.Max(0, _s.Chart.Ys.IndexOf(c)) % ChartDrawing.Palette.Length];
            if (_s.Chart.Ys.Contains(c)) ctx.FillRectangle(new SolidColorBrush(Color.Parse(dot)), new Rect(x + 4, 6, 4, 14));
            if (_s.Chart.X == c) ctx.DrawText(new FormattedText("x", de, FlowDirection.LeftToRight, bold, 10, new SolidColorBrush(Color.Parse("#F5A524"))), new Point(x + 3, 4));
            ctx.DrawText(Clip(col.Name, bold, 12.5, text, CellW - 16), new Point(x + 12, 5));
            if (col.Unit.Length > 0) ctx.DrawText(Clip("[" + col.Unit + "]", face, 11, muted, CellW - 16), new Point(x + 12, 23));
            ctx.DrawLine(line, new Point(x + CellW, 0), new Point(x + CellW, size.Height));
        }
        ctx.DrawLine(line, new Point(0, HeadH), new Point(size.Width, HeadH));
        // Zeilen
        for (var r = Top; r < Top + rows + 1; r++)
        {
            var (_, y) = CellPos(0, r); if (y > size.Height) break;
            ctx.DrawText(new FormattedText((r + 1).ToString(), de, FlowDirection.LeftToRight, face, 11, muted), new Point(10, y + 6));
            ctx.DrawLine(line, new Point(0, y + CellH), new Point(RowW + _s.Columns.Count * CellW, y + CellH));
            for (var c = 0; c < _s.Columns.Count; c++)
                if (_s.Get(c, r) is { } v)
                {
                    var ft = new FormattedText(v.ToString("0.######", de), de, FlowDirection.LeftToRight, face, 12.5, text);
                    var (x, _) = CellPos(c, r); ctx.DrawText(ft, new Point(x + CellW - ft.Width - 8, y + 5));
                }
        }
        ctx.DrawLine(line, new Point(RowW, 0), new Point(RowW, size.Height));
        // Auswahl
        if (_s.Columns.Count > 0 && Row >= Top && Row < Top + rows + 1) { var (sx, sy) = CellPos(Col, Row); ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#F5A524")), 2), new Rect(sx + 1, sy + 1, CellW - 2, CellH - 2)); }
        if (_s.Columns.Count == 0) ctx.DrawText(new FormattedText("Keine Spalten – oben „Spalte +“", de, FlowDirection.LeftToRight, face, 13, muted), new Point(20, 60));
    }

    static FormattedText Clip(string s, Typeface f, double size, IBrush b, double max)
    {
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, f, size, b);
        while (ft.Width > max && s.Length > 2) { s = s[..^2] + "…"; ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, f, size, b); }
        return ft;
    }
}

/// <summary>Zeichnet Zeichenelemente in Bildpunkten (Diagramme aus dem Kern).</summary>
public sealed class ShapeView(Func<double, double, List<Shape>> build, string bg = "#FFFFFF") : Control
{
    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size; if (size.Width < 20) return;
        ctx.FillRectangle(new SolidColorBrush(Color.Parse(bg)), new Rect(size));
        static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex.Length == 9 ? "#" + hex[7..] + hex[1..7] : hex));
        foreach (var sh in build(size.Width, size.Height))
            switch (sh)
            {
                case DPoly p: ctx.DrawGeometry(p.Fill is null ? null : B(p.Fill), p.Stroke is null ? null : new Pen(B(p.Stroke), p.Width) { LineJoin = PenLineJoin.Round }, new PolylineGeometry(p.Pts.Select(q => new Point(q.X, q.Y)).ToList(), p.Closed)); break;
                case DLine l: ctx.DrawLine(new Pen(B(l.Stroke), l.Width), new Point(l.X1, l.Y1), new Point(l.X2, l.Y2)); break;
                case DText t:
                    var ft = new FormattedText(t.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, t.Bold ? new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold) : new Typeface("Inter"), t.Size, B(t.Color));
                    using (ctx.PushTransform(Matrix.CreateTranslation(-ft.Width / 2, -ft.Height / 2) * Matrix.CreateRotation(t.AngleDeg * Math.PI / 180) * Matrix.CreateTranslation(t.X, t.Y))) ctx.DrawText(ft, new Point(0, 0));
                    break;
            }
    }
}
