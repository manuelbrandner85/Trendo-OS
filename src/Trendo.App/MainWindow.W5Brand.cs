using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>BUS-013/015/021/022: Marke & Kunden.</summary>
public partial class MainWindow
{
    public IdeaList<Touchpoint> Touchpoints => new(_core.DataRoot, BizIdea.Id, "kundenweg");
    public IdeaList<ProcessStep> ProcessSteps => new(_core.DataRoot, BizIdea.Id, "ablauf");
    private string _brandColor = "#2F86F6"; private string _seoKw = "", _seoText = "";
    private readonly double[] _funnel = [1000, 120, 30, 10];

    private void InitW5Brand() => RegisterTab(BusinessTabs, "biz-marke", "Marke & Kunden", "BUS-013", BrandView, "marke", "name", "logo", "farben", "kundenweg", "seo", "google", "trichter", "verkauf");

    private Control BrandView()
    {
        var i = BizIdea; var sp = new StackPanel { Spacing = 8, Children = { P("Gib deiner Idee ein Gesicht und plane, wie Kunden zu dir finden."), IdeaHeader("biz-marke") } };
        // BUS-013
        var nc = BusinessBrand.CheckName(i.Name);
        var color = new TextBox { Name = "br-color", Text = _brandColor, Width = 100 };
        var pal = BusinessBrand.Palette(_brandColor);
        var sw = new WrapPanel();
        foreach (var (role, hex) in pal) sw.Children.Add(new StackPanel { Margin = new Avalonia.Thickness(0, 0, 8, 0), Children = { new Rectangle { Width = 56, Height = 34, Fill = Brush.Parse(hex), RadiusX = 6, RadiusY = 6 }, P($"{role}\n{hex}", null, 10) } });
        var bsec = new StackPanel { Spacing = 4, Children = { H("Marke", 14), new TextBlock { Name = "br-name-score", Text = $"Name „{i.Name}“: {nc.Score}/100", Foreground = Text, FontWeight = FontWeight.SemiBold } } };
        foreach (var hnt in nc.Hints) bsec.Children.Add(P("• " + hnt, null, 11));
        bsec.Children.Add(P("Mögliche Adressen: " + string.Join(", ", nc.Domains), null, 11));
        bsec.Children.Add(new WrapPanel { Children = { P("Hauptfarbe  "), color, Act("br-palette", "Farben berechnen", () => { if (System.Text.RegularExpressions.Regex.IsMatch(color.Text ?? "", "^#[0-9A-Fa-f]{6}$")) _brandColor = color.Text!; Show("biz-marke"); }) } });
        bsec.Children.Add(sw);
        var ctr = BusinessBrand.Contrast(pal[4].Hex, pal[5].Hex);
        bsec.Children.Add(P($"Lesbarkeit Text auf Hintergrund: {ctr:0.0} : 1 {(ctr >= 4.5 ? "✓ gut lesbar" : "⚠ zu wenig Kontrast")}", null, 11));
        foreach (var t in BusinessBrand.Taglines(i)) bsec.Children.Add(P("„" + t + "“", Text, 11.5));
        bsec.Children.Add(Act("br-export", "Marke speichern (Farben als CSS)", () =>
        {
            var f = System.IO.Path.Combine(BusinessFolder, DevStudio.SafeName(i.Name) + "-Marke.css"); File.WriteAllText(f, BusinessBrand.BrandCss(pal)); Say("Gespeichert: " + System.IO.Path.GetFileName(f), "biz-marke");
        }));
        sp.Children.Add(Card(bsec));

        // BUS-015
        var stage = new ComboBox { Name = "br-t-stage", ItemsSource = BusinessBrand.Stages, SelectedIndex = 0, Width = 150 };
        var where = new TextBox { Name = "br-t-where", PlaceholderText = "Wo? z. B. Instagram", Width = 150 }; var feel = new TextBox { Name = "br-t-feel", PlaceholderText = "Wie fühlt es sich an?", Width = 150 }; var imp = new TextBox { Name = "br-t-imp", PlaceholderText = "Besser machen", Width = 160 };
        var pw = new TextBox { Name = "br-p-who", PlaceholderText = "Wer?", Width = 100 }; var pwhat = new TextBox { Name = "br-p-what", PlaceholderText = "macht was?", Width = 220 };
        var tps = Touchpoints.All(); var steps = ProcessSteps.All();
        var jsec = new StackPanel { Spacing = 4, Children = { H("Kundenweg und Ablauf", 14),
            new WrapPanel { Children = { stage, where, feel, imp, Act("br-t-add", "Hinzufügen", () => { Touchpoints.Add(new(BusinessBrand.Stages[Math.Max(0, stage.SelectedIndex)], where.Text ?? "", feel.Text ?? "", imp.Text ?? "")); Show("biz-marke"); }) } },
            new WrapPanel { Children = { pw, pwhat, Act("br-p-add", "Schritt anhängen", () => { if (string.IsNullOrWhiteSpace(pwhat.Text)) return; ProcessSteps.Add(new(pw.Text ?? "", pwhat.Text!)); Show("biz-marke"); }) } } } };
        foreach (var s in BusinessBrand.Stages) { var c = tps.Count(t => t.Stage == s); jsec.Children.Add(P($"{(c > 0 ? "✓" : "○")} {s}: {string.Join(" · ", tps.Where(t => t.Stage == s).Select(t => t.Where))}", c > 0 ? Text : null, 11)); }
        if (steps.Count > 0) jsec.Children.Add(P("Ablauf: " + string.Join(" → ", steps.Select(s => $"{s.Who}: {s.What}")), Text, 11));
        jsec.Children.Add(Act("br-journey-export", "Als Dokument speichern", () => { var f = System.IO.Path.Combine(BusinessFolder, DevStudio.SafeName(i.Name) + "-Kundenweg.md"); File.WriteAllText(f, BusinessBrand.JourneyMarkdown(i, tps, steps)); Launch(f); }));
        sp.Children.Add(Card(jsec));

        // BUS-021
        var kw = new TextBox { Name = "br-seo-kw", Text = _seoKw, PlaceholderText = "Suchwort, z. B. hundesitter berlin", Width = 240 };
        var txt = new TextBox { Name = "br-seo-text", Text = _seoText, PlaceholderText = "Text deiner Seite einfügen", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80 };
        var ssec = new StackPanel { Spacing = 4, Children = { H("Bei Google gefunden werden", 14), kw, txt, Act("br-seo-check", "Prüfen", () => { _seoKw = kw.Text ?? ""; _seoText = txt.Text ?? ""; Show("biz-marke"); }) } };
        if (_seoKw.Length > 0)
        {
            var s = BusinessBrand.Seo(_seoKw, i.Name, _seoText);
            ssec.Children.Add(new TextBlock { Name = "br-seo-title", Text = "Titel: " + s.Title, Foreground = Text, TextWrapping = TextWrapping.Wrap });
            ssec.Children.Add(P($"Beschreibung: {s.Description}\nAdresse: /{s.Slug} · Anteil Suchwort: {s.Density} %", null, 11));
            foreach (var x in s.Hints) ssec.Children.Add(P("• " + x, null, 11));
        }
        sp.Children.Add(Card(ssec));

        // BUS-022
        var fsec = new StackPanel { Spacing = 4, Children = { H("Verkaufstrichter", 14) } };
        var row = new WrapPanel();
        for (var k = 0; k < _funnel.Length; k++) { var idx = k; var nu = new NumericUpDown { Name = $"br-f-{k}", Value = (decimal)_funnel[k], Minimum = 0, Maximum = 10_000_000, Width = 130, FormatString = "0" }; nu.ValueChanged += (_, _) => _funnel[idx] = (double)(nu.Value ?? 0); row.Children.Add(new StackPanel { Margin = new Avalonia.Thickness(0, 0, 8, 0), Children = { P(BusinessBrand.DefaultFunnel[k], null, 10.5), nu } }); }
        fsec.Children.Add(row); fsec.Children.Add(Act("br-f-calc", "Berechnen", () => Show("biz-marke")));
        var fr = BusinessBrand.Funnel(BusinessBrand.DefaultFunnel.Select((n, k) => new BusinessBrand.FunnelStage(n, _funnel[k])).ToList(), i.Price);
        fsec.Children.Add(P(string.Join(" → ", fr.Stages.Select(s => $"{s.Name} {s.Count:0} ({s.Rate:P0})")), Text, 11));
        fsec.Children.Add(new TextBlock { Name = "br-f-result", Text = $"{fr.Bottleneck} Umsatz bei {i.Price:0.##} €: {fr.Revenue:N0} €", Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Card(fsec));
        return BusinessPage("Marke & Kunden", sp);
    }
}
