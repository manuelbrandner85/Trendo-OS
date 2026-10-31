using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Level-Editor der Spiele-Werkstatt: Kachelraster zum Malen. Linke Maustaste malt die gewählte Kachel (auch ziehend),
/// rechte Maustaste radiert; ein Strich ist beim Loslassen ein Schritt im Rückgängig. Start (S) gibt es nur einmal.</summary>
public sealed class LevelCanvas : Control
{
    public List<string> Rows { get; set; } = [];
    public char Tile { get; set; } = '#';
    public double Cell { get; set; } = 22;
    public Action<List<string>>? LevelChanged { get; set; }
    char[][] _grid = [];
    bool _painting, _changed; char _brush;

    public LevelCanvas() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Cross); }
    int Cols => Rows.Count == 0 ? 0 : Rows.Max(r => r.Length);
    protected override Size MeasureOverride(Size available) => new(Math.Max(1, Cols) * Cell + 1, Math.Max(1, Rows.Count) * Cell + 1);
    public Point CenterOf(int col, int row) => new(col * Cell + Cell / 2, row * Cell + Cell / 2);

    void Load() => _grid = Rows.Select(r => r.PadRight(Cols, '.').ToCharArray()).ToArray();
    void Paint(Point p)
    {
        int cx = (int)(p.X / Cell), cy = (int)(p.Y / Cell);
        if (cy < 0 || cy >= _grid.Length || cx < 0 || cx >= _grid[cy].Length || _grid[cy][cx] == _brush) return;
        if (_brush == 'S') foreach (var row in _grid) for (var i = 0; i < row.Length; i++) if (row[i] == 'S') row[i] = '.';
        _grid[cy][cx] = _brush; _changed = true; InvalidateVisual();
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Load(); // kein Focus(): der Fokus würde die Seite scrollen und den Strich versetzen
        _brush = e.GetCurrentPoint(this).Properties.IsRightButtonPressed ? '.' : Tile; _painting = true; _changed = false;
        Paint(e.GetPosition(this)); e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (_painting) Paint(e.GetPosition(this)); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null); if (!_painting) return; _painting = false;
        if (_changed) { Rows = _grid.Select(r => new string(r)).ToList(); LevelChanged?.Invoke(Rows); }
    }

    static readonly IBrush Sky = new SolidColorBrush(Color.Parse("#FF142544")), Ground = new SolidColorBrush(Color.Parse("#FF5B3A1E")), Grass = new SolidColorBrush(Color.Parse("#FF3FBF5A")),
        Coin = new SolidColorBrush(Color.Parse("#FFFACC15")), Foe = new SolidColorBrush(Color.Parse("#FFE0475B")), Spike = new SolidColorBrush(Color.Parse("#FFC9CED6")), Start = new SolidColorBrush(Color.Parse("#FF4FB3FF")), White = Brushes.White;

    public override void Render(DrawingContext ctx)
    {
        var rows = _painting ? _grid.Select(r => new string(r)).ToList() : Rows;
        var c = Cell; ctx.FillRectangle(Sky, new Rect(0, 0, Bounds.Width, Bounds.Height));
        var grid = new Pen(new SolidColorBrush(Color.Parse("#14FFFFFF")), 1);
        for (var y = 0; y < rows.Count; y++)
            for (var x = 0; x < rows[y].Length; x++)
            {
                var r = new Rect(x * c, y * c, c, c); var ch = rows[y][x];
                switch (ch)
                {
                    case '#': ctx.FillRectangle(Ground, r); if (y == 0 || x >= rows[y - 1].Length || rows[y - 1][x] != '#') ctx.FillRectangle(Grass, new Rect(r.X, r.Y, c, c * 0.22)); break;
                    case 'o': ctx.DrawEllipse(Coin, null, r.Center, c * 0.26, c * 0.26); break;
                    case 'E': ctx.FillRectangle(Foe, r.Deflate(c * 0.12)); ctx.FillRectangle(White, new Rect(r.X + c * 0.55, r.Y + c * 0.3, c * 0.18, c * 0.18)); break;
                    case '^': for (var i = 0; i < 3; i++) ctx.DrawGeometry(Spike, null, new PolylineGeometry([new Point(r.X + i * c / 3, r.Bottom), new Point(r.X + i * c / 3 + c / 6, r.Y + c * 0.35), new Point(r.X + (i + 1) * c / 3, r.Bottom)], true)); break;
                    case 'S': ctx.DrawRectangle(Start, null, new RoundedRect(r.Deflate(c * 0.18), 4)); ctx.DrawText(new FormattedText("S", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.Bold), c * 0.5, White), new Point(r.X + c * 0.33, r.Y + c * 0.18)); break;
                    case 'Z': ctx.FillRectangle(White, new Rect(r.X + c * 0.25, r.Y + c * 0.1, c * 0.1, c * 0.85)); ctx.DrawGeometry(Coin, null, new PolylineGeometry([new Point(r.X + c * 0.35, r.Y + c * 0.1), new Point(r.X + c * 0.9, r.Y + c * 0.3), new Point(r.X + c * 0.35, r.Y + c * 0.5)], true)); break;
                }
                ctx.DrawRectangle(null, grid, r);
            }
    }
}
