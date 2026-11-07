using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Modernisierung W3 (07.11.2026): „Wissensnetz“ – alle Seiten als Punkte auf einem Kreis, die [[Verweise]] als Linien. Größe = Anzahl der
/// Verbindungen, Klick öffnet die Seite. Fehlende Zielseiten (rote Verweise) erscheinen nicht als Punkt, nur echte Seiten.</summary>
public partial class MainWindow
{
    bool _wikiGraph;
    public const int WikiGraphMax = 60;

    /// <summary>Kanten des Netzes (Titel → Titel), nur zwischen vorhandenen Seiten, ungerichtet und ohne Doppelte.</summary>
    public static List<(string A, string B)> WikiEdges(IReadOnlyList<KnowledgeItem> pages, KnowledgeStore store)
    {
        var titles = pages.Select(p => p.Title).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var set = new HashSet<string>(); var list = new List<(string, string)>();
        foreach (var p in pages)
            foreach (var t in KnowledgeStore.Links(p.Text))
            {
                if (store.ByTitle(t) is not { } target || !titles.Contains(target.Title) || string.Equals(target.Title, p.Title, StringComparison.CurrentCultureIgnoreCase)) continue;
                var (a, b) = string.Compare(p.Title, target.Title, StringComparison.CurrentCultureIgnoreCase) < 0 ? (p.Title, target.Title) : (target.Title, p.Title);
                if (set.Add(a.ToLowerInvariant() + "\u0001" + b.ToLowerInvariant())) list.Add((a, b));
            }
        return list;
    }

    Control WikiGraphPanel()
    {
        const string V = "ki-wissen";
        var pages = Wissen.All().Take(WikiGraphMax).ToList();
        if (pages.Count == 0) return EmptyState("wissen-graph-empty", "teilen", "Noch kein Wissensnetz", "Leg Seiten an und verbinde sie mit [[Titel]] – hier siehst du dann, wie dein Wissen zusammenhängt.");
        var edges = WikiEdges(pages, Wissen);
        var degree = pages.ToDictionary(p => p.Title, p => edges.Count(e => e.A.Equals(p.Title, StringComparison.CurrentCultureIgnoreCase) || e.B.Equals(p.Title, StringComparison.CurrentCultureIgnoreCase)), StringComparer.CurrentCultureIgnoreCase);
        // stark verbundene Seiten nach innen: zwei Ringe
        var ordered = pages.OrderByDescending(p => degree[p.Title]).ThenBy(p => p.Title).ToList();
        const double GW = 760, GH = 520, Cx = GW / 2, Cy = GH / 2;
        var inner = ordered.Take(Math.Min(8, ordered.Count)).ToList(); var outer = ordered.Skip(inner.Count).ToList();
        var pos = new Dictionary<string, Point>(StringComparer.CurrentCultureIgnoreCase);
        void Ring(List<KnowledgeItem> l, double r) { for (var k = 0; k < l.Count; k++) { var a = 2 * Math.PI * k / Math.Max(1, l.Count) - Math.PI / 2; pos[l[k].Title] = new Point(Cx + r * Math.Cos(a), Cy + r * Math.Sin(a)); } }
        Ring(inner, outer.Count == 0 ? 190 : 110); Ring(outer, 230);
        var canvas = new Canvas { Name = "wissen-graph", Width = GW, Height = GH };
        foreach (var (a, b) in edges)
            canvas.Children.Add(new Line { StartPoint = pos[a], EndPoint = pos[b], Stroke = Brush.Parse("#66D4AF37"), StrokeThickness = 1.4 });
        var n = 0;
        foreach (var p in ordered)
        {
            var id = p.Id; var d = degree[p.Title]; var size = 14 + Math.Min(26, d * 5); var on = id == _wikiId;
            var dot = new Button { Name = $"wissen-node-{n++}", Width = size, Height = size, Padding = new Thickness(0), CornerRadius = new CornerRadius(size / 2), BorderThickness = new Thickness(on ? 3 : 1.5),
                BorderBrush = on ? Brushes.White : Brush.Parse("#F5D27A"), Background = d == 0 ? Brush.Parse("#555555") : Gold() };
            ToolTip.SetTip(dot, $"{p.Title} · {d} Verbindung(en)");
            dot.Click += (_, _) => { _wikiId = id; _wikiGraph = false; _wikiEdit = false; _wikiTrashView = false; _wikiWeek = false; Show(V); };
            Canvas.SetLeft(dot, pos[p.Title].X - size / 2); Canvas.SetTop(dot, pos[p.Title].Y - size / 2);
            var label = new TextBlock { Text = p.Title.Length > 22 ? p.Title[..22] + "…" : p.Title, Foreground = Text, FontSize = 11, IsHitTestVisible = false };
            Canvas.SetLeft(label, pos[p.Title].X + size / 2 + 4); Canvas.SetTop(label, pos[p.Title].Y - 8);
            canvas.Children.Add(dot); canvas.Children.Add(label);
        }
        var lonely = pages.Count(p => degree[p.Title] == 0);
        return new StackPanel { Spacing = 8, Children = {
            H("Wissensnetz", 16),
            P($"{pages.Count} Seiten · {edges.Count} Verbindungen{(lonely > 0 ? $" · {lonely} Seite(n) ohne Verweis (grau) – mit [[Titel]] verbinden" : "")}. Größere Punkte sind stärker verbunden; ein Klick öffnet die Seite.", Muted, 12),
            new Border { CornerRadius = new CornerRadius(14), Background = Brush.Parse("#0E111A"), BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(8), ClipToBounds = true,
                Child = new Viewbox { Stretch = Stretch.Uniform, MaxHeight = 560, Child = canvas } } } };
    }
}
