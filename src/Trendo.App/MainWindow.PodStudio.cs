using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Pod;
using Trendo.Core.Programs;
using IoPath = System.IO.Path;

namespace Trendo.App;

/// <summary>Print on Demand (STUDIO-POD-01, freigegeben 08.11.2026), Teil 1: P1 Ablauf, P3 Druckdatei prüfen, P4 Vorschau auf dem PC, P6 Preisrechner, P7 Markenwarnung, P8 Verkäufer-Checkliste, P9 Verkaufstext.
/// Teil 1 arbeitet ganz auf dem PC. Anbieter-Konten (P5, P10) kommen erst, wenn die Schnittstellen an der Originalquelle geprüft sind.</summary>
public partial class MainWindow
{
    const string PodKey = "pod-programm";
    string _podStep = "entwurf";

    static JsonObject PodDoc(string product, string title, string description) => new()
    {
        ["product"] = product, ["title"] = title, ["description"] = description, ["tags"] = "", ["motif"] = "", ["motifText"] = "",
        ["cost"] = 12.0, ["shipping"] = 4.0, ["feePercent"] = 6.5, ["feeFixed"] = 0.2, ["vat"] = 19.0, ["gross"] = 29.99, ["checks"] = new JsonArray(),
    };

    static decimal PodDec(ProgramDocument d, string key) => (decimal)d.Number(key);
    static PodPriceInput PodPriceOf(ProgramDocument d) => new(PodDec(d, "cost"), PodDec(d, "shipping"), PodDec(d, "feePercent"), PodDec(d, "feeFixed"), PodDec(d, "vat"));
    static PodProduct PodProductOf(ProgramDocument d) => PodCheck.Product(d.Text("product"));

    static PodCheckResult? PodResult(ProgramDocument d)
    {
        var f = d.Text("motif");
        if (f.Length == 0 || !File.Exists(f)) return null;
        var bytes = File.ReadAllBytes(f); var (w, h) = XrPlus.ImageSize(bytes);
        return PodCheck.Check(PodProductOf(d), w, h, PodCheck.HasAlpha(bytes));
    }

    private void InitPod() => RegisterProgram(FachTabs, new ProgramSpec(PodKey, "STUDIO-POD-01", "Print on Demand")
    {
        Sentence = "Eigene Motive auf Produkte bringen: Druckdatei prüfen, Preis rechnen, Rechtliches klären, Verkaufstext schreiben.",
        Folder = "Print on Demand",
        FillHeight = true,
        Templates =
        [
            new("tshirt", "T-Shirt", "Motiv mit durchsichtigem Hintergrund", () => PodDoc("tshirt", "Neues T-Shirt", "")),
            new("tasse", "Tasse", "Motiv rundum, ohne Transparenz", () => PodDoc("tasse", "Neue Tasse", "")),
            new("poster", "Poster", "30 × 40 cm, randlos", () => PodDoc("poster", "Neues Poster", "")),
        ],
        Exports =
        [
            new("png", "Entwurf als PNG (Druckgröße)", ".png", (d, f) => PodExportPng(d, f)),
            new("svg", "Entwurf als SVG", ".svg", (d, f) => { var (w, h) = PodDesign.Board(PodProductOf(d)); File.WriteAllText(f, PodDesign.Svg(PodDesign.Items(d.Data), w, h)); return f; }),
            new("listing", "Verkaufstext", ".md", (d, f) => { File.WriteAllText(f, PodLegal.Listing(d.Title, d.Text("description"), d.Text("tags").Split(','), PodProductOf(d), PodDec(d, "gross"))); return f; }),
        ],
        Workspace = PodWorkspace,
        Status = d => { var p = PodPrice.Calc(PodPriceOf(d), PodDec(d, "gross")); return $"{PodProductOf(d).Name} · Preis {p.Gross.ToString("0.00", De)} € · Gewinn je Stück {p.Profit.ToString("0.00", De)} €"; },
        Help = "Geh die Schritte oben der Reihe nach durch. Alles bleibt auf deinem PC. Trendo stellt nichts online.",
    }, "print on demand", "pod", "merch", "t-shirt druck", "tasse", "poster", "mockup", "druckdatei", "merchandise");

    private Control PodWorkspace(ProgramDocument d)
    {
        var steps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(4, 4, 4, 10), [DockPanel.DockProperty] = Avalonia.Controls.Dock.Top };
        foreach (var (id, label) in new[] { ("entwurf", "1 Entwerfen"), ("motiv", "2 Druck prüfen"), ("preis", "3 Preis"), ("recht", "4 Rechtliches"), ("text", "5 Verkaufstext") })
        {
            var b = new Button { Name = "pod-step-" + id, Content = label, Padding = new Thickness(14, 6), FontWeight = _podStep == id ? FontWeight.SemiBold : FontWeight.Normal };
            if (_podStep == id) b.Classes.Add("accent");
            b.Click += (_, _) => { _podStep = id; Show(PodKey); };
            steps.Children.Add(b);
        }
        if (_podStep == "entwurf") return new DockPanel { Name = "pod-workspace", Children = { steps, PodDesigner(d) } }; // eigene Flächen scrollen und zoomen selbst
        Control body = _podStep switch { "preis" => PodPricePane(d), "recht" => PodLegalPane(d), "text" => PodTextPane(d), _ => PodMotifPane(d) };
        return new DockPanel { Name = "pod-workspace", Children = { steps, new ScrollViewer { Content = body } } };
    }

    /// <summary>Texteingabe, die beim Verlassen in das Dokument schreibt und die Seite neu zeichnet.</summary>
    TextBox PodField(ProgramDocument d, string key, string placeholder, bool multi = false)
    {
        var t = new TextBox { Name = "pod-" + key, Text = d.Text(key), PlaceholderText = placeholder, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 110 : 0 };
        t.LostFocus += (_, _) => { if ((t.Text ?? "") != d.Text(key)) ProgramRun(PodKey, () => d.Set(key, t.Text ?? "", placeholder)); };
        return t;
    }

    Control PodMotifPane(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "pod-motif", Spacing = 8, Margin = new Thickness(4, 0, 10, 4) };
        var product = new ComboBox { Name = "pod-product", ItemsSource = PodCheck.Products.Select(p => p.Name).ToList(), SelectedIndex = PodCheck.Products.ToList().FindIndex(p => p.Id == PodProductOf(d).Id), MinWidth = 220 };
        product.SelectionChanged += (_, _) => { if (product.SelectedIndex >= 0 && PodCheck.Products[product.SelectedIndex].Id != PodProductOf(d).Id) ProgramRun(PodKey, () => d.Set("product", PodCheck.Products[product.SelectedIndex].Id, "Produkt")); };
        var pick = new Button { Name = "pod-pick", Content = "Motiv wählen …", Padding = new Thickness(12, 5) };
        pick.Classes.Add("accent");
        pick.Click += async (_, _) => { if (await PickMediaFile("pod-motiv", "Motiv (PNG oder JPEG)", ["*.png", "*.jpg", "*.jpeg"]) is { } f) ProgramRun(PodKey, () => d.Set("motif", f, "Motiv")); };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { product, pick } });

        var result = PodResult(d); var p = PodProductOf(d);
        var preview = PodPreview(d, p, result);
        var info = new StackPanel { Name = "pod-check", Spacing = 6 };
        if (d.Text("motif").Length > 0 && !File.Exists(d.Text("motif"))) info.Children.Add(P("Die Motiv-Datei wurde nicht gefunden: " + IoPath.GetFileName(d.Text("motif")), Brush.Parse("#F5A524"), 12));
        if (result is null) info.Children.Add(P("Wähle ein Motiv, dann prüft Trendo die Druckqualität. Ein Bild erzeugst du auch über die Zeile ganz oben.", Muted, 12));
        else
        {
            info.Children.Add(H(result.Level switch { PodLevel.Gut => "Druckdatei passt", PodLevel.Pruefen => "Bitte prüfen", _ => "So nicht drucken" }, 14));
            info.Children.Add(P($"{result.Dpi.ToString("0", De)} DPI · Motiv {result.PrintedWidthCm.ToString("0.#", De)} × {result.PrintedHeightCm.ToString("0.#", De)} cm auf {p.PrintWidthCm.ToString("0.#", De)} × {p.PrintHeightCm.ToString("0.#", De)} cm Druckbereich", Text, 12));
            foreach (var f in result.Findings) info.Children.Add(P((f.Level == PodLevel.Problem ? "• " : "") + f.Text, Brush.Parse(f.Level == PodLevel.Problem ? "#FF6B6B" : "#F5A524"), 12));
            info.Children.Add(P("Die Druckbereiche sind Richtwerte. Die genauen Maße nennt der Anbieter je Artikel.", Muted, 11));
        }
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("340,16,*"), Children = { preview, Pos(info, 2) } });
        return sp;
    }

    /// <summary>P4: vereinfachte Vorschau auf einer gezeichneten Form, maßstäblich zum Druckbereich. Fertige Produktfotos liefert später der Anbieter.</summary>
    Control PodPreview(ProgramDocument d, PodProduct p, PodCheckResult? result)
    {
        const double W = 320, H0 = 360;
        var c = new Canvas { Name = "pod-preview", Width = W, Height = H0, Background = Brush.Parse("#20262F") };
        var shape = Brush.Parse("#E8EAED");
        (double X, double Y, double W, double H) area;
        switch (p.Id)
        {
            case "tasse":
                c.Children.Add(new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M40,120 L240,120 L240,270 Q240,290 220,290 L60,290 Q40,290 40,270 Z M240,150 Q300,150 300,195 Q300,240 240,240"), Fill = shape, Stroke = shape, StrokeThickness = 12 });
                area = (56, 136, 168, 71.4); break;
            case "poster":
                c.Children.Add(new Rectangle { Width = 240, Height = 320, Fill = Brushes.White, [Canvas.LeftProperty] = 40.0, [Canvas.TopProperty] = 20.0 });
                area = (40, 20, 240, 320); break;
            case "beutel":
                c.Children.Add(new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M60,100 L260,100 L270,340 L50,340 Z M110,100 Q110,30 160,30 Q210,30 210,100"), Fill = shape, Stroke = shape, StrokeThickness = 8 });
                area = (90, 130, 140, 163.3); break;
            default:
                c.Children.Add(new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M110,30 Q160,60 210,30 L300,75 L270,135 L240,118 L240,345 L80,345 L80,118 L50,135 L20,75 Z"), Fill = shape });
                area = (100, 90, 120, 160); break;
        }
        c.Children.Add(new Rectangle { Name = "pod-area", Width = area.W, Height = area.H, Stroke = Brush.Parse("#2F6BF6"), StrokeThickness = 1, StrokeDashArray = [4, 3], [Canvas.LeftProperty] = area.X, [Canvas.TopProperty] = area.Y });
        var f = d.Text("motif");
        if (result is { Dpi: > 0 } && File.Exists(f))
        {
            try
            {
                var w = area.W * result.PrintedWidthCm / p.PrintWidthCm; var h = area.H * result.PrintedHeightCm / p.PrintHeightCm;
                c.Children.Add(new Image { Name = "pod-art", Source = new Bitmap(f), Width = w, Height = h, Stretch = Stretch.Fill, [Canvas.LeftProperty] = area.X + (area.W - w) / 2, [Canvas.TopProperty] = area.Y + (area.H - h) / 2 });
            }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NullReferenceException or NotSupportedException) { } // Prüfung bleibt nutzbar, auch wenn die Vorschau das Bild nicht lesen kann
        }
        return new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Width = W, Height = H0, Child = c };
    }

    Control PodPricePane(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "pod-price", Spacing = 6, Margin = new Thickness(4, 0, 10, 4), MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left };
        void Num(string key, string label)
        {
            var t = new TextBox { Name = "pod-" + key, Text = d.Number(key).ToString("0.##", De), Width = 100 };
            t.LostFocus += (_, _) => { if (double.TryParse(t.Text, NumberStyles.Float, De, out var v) && v >= 0 && v != d.Number(key)) ProgramRun(PodKey, () => d.Set(key, v, label)); };
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,110"), Children = { new TextBlock { Text = label, Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, Pos(t, 1) } });
        }
        Num("cost", "Herstellung beim Anbieter (netto, €)");
        Num("shipping", "Versand, den du trägst (netto, €)");
        Num("feePercent", "Gebühr der Verkaufsplattform (%)");
        Num("feeFixed", "Feste Gebühr je Verkauf (€)");
        Num("vat", "Umsatzsteuer (%), bei Kleinunternehmer 0");
        Num("gross", "Verkaufspreis (brutto, €)");
        var r = PodPrice.Calc(PodPriceOf(d), PodDec(d, "gross"));
        var profit = new TextBlock { Name = "pod-profit", Text = r.Profit.ToString("0.00", De) + " €", FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(r.Profit > 0 ? "#3DDC84" : "#FF6B6B") };
        sp.Children.Add(H("Gewinn je Stück", 14)); sp.Children.Add(profit);
        sp.Children.Add(P($"Netto {r.Net.ToString("0.00", De)} € · Steuer {r.Vat.ToString("0.00", De)} € · Gebühren {r.Fees.ToString("0.00", De)} € · Spanne {r.MarginPercent.ToString("0.#", De)} %", Muted, 12));
        var want = new TextBox { Name = "pod-want", Text = "8", Width = 80 };
        var apply = new Button { Name = "pod-apply", Content = "Preis dafür berechnen", Padding = new Thickness(12, 5) };
        apply.Click += (_, _) =>
        {
            if (!decimal.TryParse(want.Text, NumberStyles.Number, De, out var w)) return;
            if (PodPrice.ForProfit(PodPriceOf(d), w) is { } g) ProgramRun(PodKey, () => d.Set("gross", (double)g, "Preis berechnet"));
            else Toast("Mit diesen Gebühren lässt sich kein Preis berechnen.");
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = { new TextBlock { Text = "Gewünschter Gewinn (€)", Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, want, apply } });
        return sp;
    }

    Control PodLegalPane(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "pod-legal", Spacing = 8, Margin = new Thickness(4, 0, 10, 4) };
        sp.Children.Add(P("Trendo hilft beim Prüfen, ersetzt aber keine Rechtsberatung. Verantwortlich bleibt, wer verkauft.", Muted, 12));
        sp.Children.Add(H("Marken und fremde Figuren", 14));
        sp.Children.Add(PodField(d, "motifText", "Text oder Name des Motivs, z. B. der Spruch auf dem Shirt"));
        var hits = PodLegal.MarkHits($"{d.Text("motifText")} {d.Title} {d.Text("description")} {d.Text("tags")}");
        sp.Children.Add(new TextBlock { Name = "pod-marks", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brush.Parse(hits.Count > 0 ? "#FF6B6B" : "#3DDC84"),
            Text = hits.Count > 0 ? "Bekannte Marke oder Figur gefunden: " + string.Join(", ", hits) : "Keine der häufigsten Marken gefunden. Das heißt nicht, dass der Text frei ist. Bitte im Register suchen." });
        var dpma = new Button { Name = "pod-dpma", Content = "Deutsche Marken suchen (DPMA)", Padding = new Thickness(12, 5) };
        dpma.Click += (_, _) => Launch(PodLegal.DpmaSearch);
        var euipo = new Button { Name = "pod-euipo", Content = "EU-Marken suchen (EUIPO)", Padding = new Thickness(12, 5) };
        euipo.Click += (_, _) => Launch(PodLegal.EuipoSearch);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { dpma, euipo } });
        sp.Children.Add(H("Vor dem ersten Verkauf", 14));
        var done = d.List("checks").Select(x => x?.ToString() ?? "").ToHashSet();
        foreach (var (id, label) in PodLegal.Checklist)
        {
            var c = new CheckBox { Name = "pod-check-" + id, IsChecked = done.Contains(id), Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Foreground = Text, FontSize = 12 } };
            c.IsCheckedChanged += (_, _) => d.Edit("Checkliste", x =>
            {
                var list = (x["checks"] as JsonArray ?? []).Select(n => n?.ToString() ?? "").Where(v => v != id).ToList();
                if (c.IsChecked == true) list.Add(id);
                x["checks"] = new JsonArray([.. list.Select(v => (JsonNode)v)]);
            });
            sp.Children.Add(c);
        }
        return sp;
    }

    Control PodTextPane(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "pod-text", Spacing = 6, Margin = new Thickness(4, 0, 10, 4), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(P("Schreib selbst oder sag oben in der Zeile, was der Text sagen soll. Über „Exportieren“ bekommst du ihn als Datei für deinen Shop.", Muted, 12));
        sp.Children.Add(P("Beschreibung", null, 11)); sp.Children.Add(PodField(d, "description", "Beschreibung für die Verkaufsseite", true));
        sp.Children.Add(P("Schlagwörter, mit Komma getrennt", null, 11)); sp.Children.Add(PodField(d, "tags", "z. B. Geschenk, Natur, Wandern"));
        return sp;
    }
}
