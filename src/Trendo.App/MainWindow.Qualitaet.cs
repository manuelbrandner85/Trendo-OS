using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Systemumbau 2, A1 (freigegeben 03.11.2026): Qualitäts-Ampel aller Programme – schwächstes zuerst, mit Gründen.
/// Belegt aus dem mitgelieferten Katalog (Arbeitsfläche, Vorbild, Funktionen, Tests, Öffnungszeit, läuft unter Trendo OS).</summary>
public partial class MainWindow
{
    string _qlFilter = "alle";

    public List<(ProgramEntry Entry, QualityLight.Rating Rating)> QualityRanking() => QualityLight.Ranked(ProgramCatalog.Load());

    Control QualityView()
    {
        var all = QualityRanking();
        var sp = new StackPanel { Name = "ql-root", Spacing = 8 };
        int N(string l) => all.Count(x => x.Rating.Light == l);
        Control Dot(string l) => new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0),
            Background = Brush.Parse(l == "grün" ? "#22C55E" : l == "gelb" ? "#FACC15" : l == "linux" ? "#60A5FA" : "#EF4444") };
        TextBlock T(string t) => new() { Text = t, Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        sp.Children.Add(new WrapPanel { Name = "ql-summary", Children = { T($"{all.Count} Programme:"), Dot("rot"), T($"{N("rot")} rot"), Dot("gelb"), T($"{N("gelb")} gelb"), Dot("grün"), T($"{N("grün")} grün"),
            Dot("linux"), T($"{all.Count(x => x.Rating.Linux != "ja")} unter Trendo OS eingeschränkt") } });
        sp.Children.Add(P("Belegt aus dem Code und den Tests (nicht von Hand). Die Liste ist zugleich die Reihenfolge des Umbaus: schwächstes Programm zuerst.", Muted, 11.5));
        var filters = new WrapPanel { Name = "ql-filters" };
        foreach (var (id, label) in new[] { ("alle", "Alle"), ("rot", "Rot"), ("gelb", "Gelb"), ("grün", "Grün"), ("linux", "Trendo OS eingeschränkt") })
        {
            var on = _qlFilter == id; var fid = id;
            var b = new Button { Name = "ql-filter-" + (id == "grün" ? "gruen" : id), Content = id == "alle" ? label : new StackPanel { Orientation = Orientation.Horizontal, Children = { Dot(id), new TextBlock { Text = label } } }, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(10),
                Background = on ? Brush.Parse("#33D4A84B") : Brush.Parse("#10FFFFFF"), BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { _qlFilter = fid; Show("qualitaet"); };
            filters.Children.Add(b);
        }
        sp.Children.Add(filters);
        var rows = all.Where(x => _qlFilter switch { "alle" => true, "linux" => x.Rating.Linux != "ja", var l => x.Rating.Light == l }).ToList();
        var list = new StackPanel { Name = "ql-list", Spacing = 4 };
        var i = 0;
        foreach (var (e, r) in rows)
        {
            var n = i++; var id = e.Id;
            var dot = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0),
                Background = Brush.Parse(r.Light == "grün" ? "#22C55E" : r.Light == "gelb" ? "#FACC15" : "#EF4444") };
            var info = new StackPanel { Spacing = 1, Children = {
                new TextBlock { Text = $"{e.Name}  ·  {r.Score} Punkte", Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = $"{e.StudioName} · {e.Area} · {(e.Type.Count == 0 ? "keine eigene Arbeitsfläche" : string.Join(", ", e.Type))} · {e.Tests.Count} Tests · Vorbild: {(e.Rea is null ? "fehlt" : "ja")}"
                    + (e.WarmMs is { } ms ? $" · öffnet in {ms:0} ms" : "") + $" · Trendo OS: {r.Linux}", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = r.Missing.Count == 0 ? "✓ nichts offen" : "Offen: " + string.Join(" · ", r.Missing), Foreground = r.Missing.Count == 0 ? Brush.Parse("#4ADE80") : Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap } } };
            var open = new Button { Name = $"ql-open-{n}", Content = "Öffnen", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            open.Click += (_, _) => Show(id);
            list.Children.Add(new Border { Background = Brush.Parse("#10FFFFFF"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8),
                Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, Children = { dot, Pos(info, 1), Pos(open, 2) } } });
        }
        if (rows.Count == 0) list.Children.Add(P("Keine Programme in dieser Auswahl.", Muted, 12));
        sp.Children.Add(list);
        return Page("Qualitäts-Ampel", sp);
    }
}
