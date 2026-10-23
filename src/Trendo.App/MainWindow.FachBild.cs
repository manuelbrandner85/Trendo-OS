using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

public partial class MainWindow
{
    /// <summary>Dieselbe Rechnung wie das Werkzeug, nur als Balken, Histogramm oder Strecke.</summary>
    private Control? FachBild(string id, Func<string, string> read) => id switch
    {
        "eng-beam" => BalkenBild(read),
        "sci-quart" => QuartBild(read),
        "geo-gpx" => KartenBild(read),
        _ => null,
    };

    private Control BalkenBild(Func<string, string> read)
    {
        var m = Engineering.Mat(read("mat"));
        var r = Engineering.Beam(BeamFrom(read("case")), Fach.Num(read("len"), "Länge"), Fach.Num(read("load"), "Last"), Engineering.Rect(Fach.Num(read("b"), "Breite"), Fach.Num(read("h"), "Höhe")), m);
        var canvas = new Canvas { Name = "eng-beam-bars", Width = 280, Height = 96 };
        var max = Math.Max(1, Math.Max(r.Stress_MPa, m.Yield_MPa));
        void Bar(int i, double wert, double obergrenze, string farbe, string label)
        {
            var h = Math.Clamp(wert / obergrenze * 64, 2, 64);
            var rect = new Border { Width = 28, Height = h, Background = Brush.Parse(farbe), CornerRadius = new Avalonia.CornerRadius(2) };
            Canvas.SetLeft(rect, 24 + i * 88); Canvas.SetTop(rect, 70 - h); canvas.Children.Add(rect);
            var t = new TextBlock { Text = label, FontSize = 10, Foreground = Text };
            Canvas.SetLeft(t, 12 + i * 88); Canvas.SetTop(t, 74); canvas.Children.Add(t);
        }
        Bar(0, r.Stress_MPa, max, "#F87171", "Spannung");
        Bar(1, m.Yield_MPa, max, "#4ADE80", "Grenze");
        Bar(2, r.Deflection_mm, Math.Max(r.Deflection_mm, 1), "#60A5FA", "Biegung mm");
        return canvas;
    }

    private static Control? QuartBild(Func<string, string> read)
    {
        var f = read("file").Trim().Trim('"');
        if (!File.Exists(f)) return null;
        var t = DataTable.Load(f);
        var x = t.Values(read("col"));
        var bins = StatsPlus.Histogram(x);
        var canvas = new Canvas { Name = "sci-quart-bars", Width = 280, Height = 90 };
        var max = Math.Max(1, bins.Max(b => b.Count));
        var bw = Math.Max(4, (260.0) / Math.Max(1, bins.Count));
        for (var i = 0; i < bins.Count; i++)
        {
            var h = Math.Max(2, 64.0 * bins[i].Count / max);
            var rect = new Border { Width = Math.Max(2, bw - 2), Height = h, Background = Brush.Parse("#3B82F6") };
            Canvas.SetLeft(rect, 8 + i * bw); Canvas.SetTop(rect, 70 - h); canvas.Children.Add(rect);
        }
        return canvas;
    }

    private static Control? KartenBild(Func<string, string> read)
    {
        var f = read("file").Trim().Trim('"');
        if (!File.Exists(f)) return null;
        var (trk, _) = Geo.ReadGpx(f);
        if (trk.Count < 2) return null;
        var canvas = new Canvas { Name = "geo-map", Width = 280, Height = 140, Background = Brush.Parse("#0E1C33") };
        double minLat = trk.Min(p => p.Lat), maxLat = trk.Max(p => p.Lat), minLon = trk.Min(p => p.Lon), maxLon = trk.Max(p => p.Lon);
        var dLat = Math.Max(1e-9, maxLat - minLat); var dLon = Math.Max(1e-9, maxLon - minLon);
        Avalonia.Point P(GeoPoint g) => new(12 + (g.Lon - minLon) / dLon * 250, 12 + (maxLat - g.Lat) / dLat * 110);
        for (var i = 1; i < trk.Count; i++)
        {
            var a = P(trk[i - 1]); var b = P(trk[i]);
            canvas.Children.Add(new Avalonia.Controls.Shapes.Line { StartPoint = a, EndPoint = b, Stroke = Brush.Parse("#7DD3FC"), StrokeThickness = 2 });
        }
        return canvas;
    }

    private Control Grundriss()
    {
        var canvas = new Canvas { Name = "arch-plan", Width = 460, Height = 170 };
        double x = 8;
        foreach (var r in Rooms.Project.Rooms)
        {
            double w = Math.Clamp(r.Length * 28, 40, 180);
            double h = Math.Clamp(r.Width * 28, 32, 130);
            var rect = new Border { Width = w, Height = h, Background = Brush.Parse("#F4F1EA"), BorderBrush = Brush.Parse("#333333"), BorderThickness = new Avalonia.Thickness(2) };
            Canvas.SetLeft(rect, x); Canvas.SetTop(rect, 16); canvas.Children.Add(rect);
            var lab = new TextBlock { Text = r.Name, FontSize = 11, Foreground = Brush.Parse("#333333") };
            Canvas.SetLeft(lab, x + 4); Canvas.SetTop(lab, 20); canvas.Children.Add(lab);
            x += w + 8;
        }
        if (Rooms.Project.Rooms.Count == 0)
            canvas.Children.Add(new TextBlock { Text = "Noch kein Raum im Grundriss.", Foreground = Muted, FontSize = 12 });
        return canvas;
    }
}
