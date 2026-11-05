using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Verkaufen › Auswertung (wie die Berichte in SumUp, Square, Shopify POS; Clean-Room, 04.11.2026): Zeitraum wählen,
/// Umsatz mit Vergleich zum Zeitraum davor, Bons, Durchschnittsbon, Rabatte, Bestseller, Wochentage und Stoßzeiten.</summary>
public partial class MainWindow
{
    public SalesStats.Report KasseReport() { var to = DateOnly.FromDateTime(DateTime.Today); return SalesStats.Of(Kasse.Sales.All(), to.AddDays(-(_statDays - 1)), to); }
    public int KasseStatDays { get => _statDays; set => _statDays = Math.Clamp(value, 1, 366); }

    Control SalesStatsView()
    {
        const string V = "verkaufen";
        var r = KasseReport();
        var range = new WrapPanel { Name = "stat-range" };
        foreach (var (d, label) in new[] { (7, "7 Tage"), (30, "30 Tage"), (90, "90 Tage"), (365, "1 Jahr") })
        { var b = Act($"stat-days-{d}", label, () => { _statDays = d; Show(V); }, _statDays == d); b.Margin = new Thickness(0, 0, 6, 6); range.Children.Add(b); }
        var change = r.Change is { } c ? $"{(c >= 0 ? "+" : "")}{c:0.#} % zum Zeitraum davor" : "kein Vergleich (davor nichts verkauft)";
        var kpis = new WrapPanel { Name = "stat-kpis", Children = {
            Kpi("stat-gross", "Umsatz", Eu(r.Gross) + " €", change, "#2FBF71", "#167A45"),
            Kpi("stat-bons", "Bons", r.Bons.ToString(), r.Cancelled > 0 ? $"{r.Cancelled} storniert" : "ohne Stornos", "#3D8BFF", "#1E4FD8"),
            Kpi("stat-avg", "Durchschnittsbon", Eu(r.Average) + " €", "je Verkauf", "#F5A524", "#C46A0A"),
            Kpi("stat-disc", "Rabatte", Eu(r.Discounts) + " €", "gewährt", "#A78BFA", "#6D28D9") } };
        // Bestseller
        var top = new StackPanel { Name = "stat-top", Spacing = 3 };
        var maxTop = r.Top.Count == 0 ? 1 : r.Top.Max(t => t.Gross);
        for (var i = 0; i < r.Top.Count; i++)
        {
            var t = r.Top[i];
            top.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("26,180,*,110"), Children = {
                new TextBlock { Text = $"{i + 1}.", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Text = t.Product, Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, 1),
                Pos(new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = GoldText(), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Width = Math.Max(4, (double)(t.Gross / maxTop) * 220) }, 2),
                Pos(new TextBlock { Text = $"{t.Qty:0.##}× · {Eu(t.Gross)} €", Foreground = Text, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }, 3) } });
        }
        if (r.Top.Count == 0) top.Children.Add(P("In diesem Zeitraum noch keine Verkäufe.", Muted, 12));
        Control Bars(string name, IReadOnlyList<decimal> values, Func<int, string> label, int every)
        {
            var max = values.Count == 0 || values.Max() <= 0 ? 1 : values.Max();
            var g = new Grid { Name = name, Height = 130, ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", values.Count))), RowDefinitions = new RowDefinitions("*,Auto") };
            for (var i = 0; i < values.Count; i++)
            {
                var bar = new Border { Margin = new Thickness(2, 0), CornerRadius = new CornerRadius(4, 4, 0, 0), Background = values[i] == max && max > 0 ? GoldText() : Brush.Parse("#553D8BFF"), VerticalAlignment = VerticalAlignment.Bottom, Height = (double)(values[i] / max) * 100 };
                ToolTip.SetTip(bar, $"{label(i)}: {Eu(values[i])} €"); Grid.SetColumn(bar, i); g.Children.Add(bar);
                if (i % every == 0) { var tb = new TextBlock { Text = label(i), Foreground = Muted, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(tb, i); Grid.SetRow(tb, 1); g.Children.Add(tb); }
            }
            return g;
        }
        var peak = r.PeakHour >= 0 ? $"Stoßzeit: {r.PeakHour}–{r.PeakHour + 1} Uhr" : "Noch keine Stoßzeit";
        return new StackPanel { Spacing = 10, Children = {
            range, kpis,
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Children = {
                Card(new StackPanel { Spacing = 6, Children = { H("Bestseller", 14), top } }),
                Pos(Card(new StackPanel { Spacing = 6, Children = { H("Umsatz je Wochentag", 14), Bars("stat-weekdays", r.ByWeekday, i => SalesStats.Weekdays[i], 1),
                    new TextBlock { Name = "stat-peak", Text = peak, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) },
                    Bars("stat-hours", r.ByHour.Skip(6).Take(17).ToList(), i => (i + 6).ToString(), 3) } }), 2) } },
            P($"Zeitraum {r.From:dd.MM.yyyy} – {r.To:dd.MM.yyyy}. Stornierte Bons zählen nicht mit.", Muted, 11) } };
    }
}
