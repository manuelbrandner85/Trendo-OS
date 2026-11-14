using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Kachelkarte des Speicherplatzes: eine Ebene des Baums, Fläche ∝ Größe, Farbe nach Dateiart, Ordner mit Rahmen.
/// Klick wählt aus, Klick auf einen Ordner zoomt hinein (über <see cref="Open"/>).</summary>
public sealed class DiskMapView : Control
{
    public DiskMap.Node? Root { get; set; }
    public DiskMap.Node? Selected { get; set; }
    public HashSet<string> Marked { get; set; } = [];
    public Action<DiskMap.Node>? Open { get; set; }
    public Action<DiskMap.Node>? Select { get; set; }
    readonly List<(DiskMap.Node Node, Rect R)> _boxes = [];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static readonly Dictionary<string, string> KindColor = new()
    {
        ["Video"] = "#7C3AED", ["Bild"] = "#16A34A", ["Musik"] = "#DB2777", ["Dokument"] = "#2563EB", ["Archiv"] = "#EA580C", ["Programm"] = "#DC2626", ["Sonstiges"] = "#64748B", ["Ordner"] = "#0E7490",
    };

    public DiskMapView() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Hand); }
    public Rect? BoxOf(DiskMap.Node n) => _boxes.FirstOrDefault(b => b.Node == n) is { Node: not null } x ? x.R : null;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this); var hit = _boxes.LastOrDefault(b => b.R.Contains(p));
        if (hit.Node is null) return;
        if (hit.Node.IsDir && hit.Node.Children.Count > 0 && hit.Node == Selected) Open?.Invoke(hit.Node); // zweiter Klick auf denselben Ordner: hinein
        else Select?.Invoke(hit.Node);
    }

    public static string Size(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" : b >= 1L << 20 ? $"{b / (double)(1L << 20):0} MB" : b >= 1024 ? $"{b / 1024.0:0} KB" : $"{b} B";

    public override void Render(DrawingContext ctx)
    {
        _boxes.Clear(); ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FF0B1424")), new Rect(Bounds.Size));
        if (Root is not { } root || root.Children.Count == 0) return;
        var kids = root.Children.Where(c => c.Bytes > 0).ToList();
        var rects = DiskMap.Squarify([.. kids.Select(k => (double)k.Bytes)], new DiskMap.Rect(2, 2, Bounds.Width - 4, Bounds.Height - 4));
        var font = new Typeface("Inter"); var bold = new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold);
        for (var i = 0; i < kids.Count; i++)
        {
            var n = kids[i]; var rr = rects[i]; var r = new Rect(rr.X, rr.Y, rr.W, rr.H).Deflate(1); if (r.Width < 1 || r.Height < 1) continue;
            _boxes.Add((n, r));
            var kind = n.IsDir ? "Ordner" : DiskMap.Kind(n.Name); var col = Color.Parse(KindColor[kind]);
            var fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(255, (byte)Math.Min(255, col.R + 40), (byte)Math.Min(255, col.G + 40), (byte)Math.Min(255, col.B + 40)), 0), new GradientStop(col, 1) } };
            ctx.DrawRectangle(fill, new Pen(new SolidColorBrush(Color.Parse(n == Selected ? "#FFFACC15" : "#AA0B1424")), n == Selected ? 3 : 1), new RoundedRect(r, 4));
            if (n.IsDir && r.Width > 30 && r.Height > 30)
            {
                // Ordner: Inhalt angedeutet (nächste Ebene in kleinen Kacheln)
                var inner = n.Children.Where(c => c.Bytes > 0).Take(40).ToList(); var ir = r.Deflate(new Thickness(4, 20, 4, 4));
                if (inner.Count > 1 && ir.Width > 20 && ir.Height > 20)
                {
                    var sub = DiskMap.Squarify([.. inner.Select(c => (double)c.Bytes)], new DiskMap.Rect(ir.X, ir.Y, ir.Width, ir.Height));
                    for (var k = 0; k < inner.Count; k++) { var s = new Rect(sub[k].X, sub[k].Y, sub[k].W, sub[k].H).Deflate(0.5); if (s.Width < 1 || s.Height < 1) continue; ctx.DrawRectangle(new SolidColorBrush(Color.Parse(KindColor[inner[k].IsDir ? "Ordner" : DiskMap.Kind(inner[k].Name)]), inner[k].IsDir ? 0.35 : 0.8), new Pen(new SolidColorBrush(Color.Parse("#66000000")), 1), s); }
                }
            }
            if (Marked.Contains(n.Path)) ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#FFF87171")), 2, DashStyle.Dash), new RoundedRect(r.Deflate(2), 4));
            if (r.Width > 54 && r.Height > 16)
                using (ctx.PushClip(r.Deflate(3)))
                {
                    ctx.DrawText(new FormattedText((n.IsDir ? "• " : "") + n.Name, De, FlowDirection.LeftToRight, bold, 11.5, Brushes.White), new Point(r.X + 5, r.Y + 3));
                    if (r.Height > 32) ctx.DrawText(new FormattedText(Size(n.Bytes), De, FlowDirection.LeftToRight, font, 10.5, new SolidColorBrush(Color.Parse("#DDFFFFFF"))), new Point(r.X + 5, r.Y + 18));
                }
        }
    }
}

/// <summary>In den Papierkorb verschieben (nicht endgültig löschen): Windows über die Shell (rückholbar), Linux nach ~/.local/share/Trash.</summary>
public static class RecycleBin
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShFileOp { public IntPtr hwnd; public uint wFunc; public string pFrom; public string? pTo; public ushort fFlags; public int fAnyOperationsAborted; public IntPtr hNameMappings; public string? lpszProgressTitle; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperationW(ref ShFileOp op);
    const uint FO_DELETE = 3; const ushort FOF_ALLOWUNDO = 0x40, FOF_NOCONFIRMATION = 0x10, FOF_SILENT = 0x4, FOF_NOERRORUI = 0x400;

    public static void Move(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("Nicht gefunden: " + Path.GetFileName(path));
        if (OperatingSystem.IsWindows())
        {
            var op = new ShFileOp { wFunc = FO_DELETE, pFrom = Path.GetFullPath(path) + "\0\0", fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI };
            var r = SHFileOperationW(ref op);
            if (r != 0 || op.fAnyOperationsAborted != 0) throw new IOException($"Konnte nicht in den Papierkorb verschieben (Code {r}).");
            return;
        }
        var trash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash");
        Directory.CreateDirectory(Path.Combine(trash, "files")); Directory.CreateDirectory(Path.Combine(trash, "info"));
        var name = Path.GetFileName(path.TrimEnd('/')); var target = Path.Combine(trash, "files", name);
        for (var i = 2; File.Exists(target) || Directory.Exists(target); i++) target = Path.Combine(trash, "files", $"{name}.{i}");
        File.WriteAllText(Path.Combine(trash, "info", Path.GetFileName(target) + ".trashinfo"), $"[Trash Info]\nPath={Path.GetFullPath(path)}\nDeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}\n");
        if (Directory.Exists(path)) Directory.Move(path, target); else File.Move(path, target);
    }
}
