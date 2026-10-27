using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0231: „Kleidung & Produkte“ (SPEC-08) als Programm – Produktdokument mit Figur und Maßlinien, Größentabelle,
/// Varianten, Stoffbedarf mit Zuschnitt-Vorschau, Kalkulation; Export als Datenblatt und Varianten-Liste.</summary>
public partial class MainWindow
{
    static JsonObject ModeDoc(string name, string material, double chest, double waist, double hip, bool women, string[] sizes, string[] colors, params (string N, double W, double H, int Q)[] pieces) => new()
    {
        ["name"] = name, ["sku"] = DevStudio.SafeName(name).ToUpperInvariant().Replace(" ", "-"), ["material"] = material, ["description"] = "", ["care"] = "30 °C Schonwaschgang",
        ["women"] = women, ["chest"] = chest, ["waist"] = waist, ["hip"] = hip,
        ["sizes"] = new JsonArray([.. sizes.Select(s => (JsonNode)s)]), ["colors"] = new JsonArray([.. colors.Select(c => (JsonNode)c)]),
        ["pieces"] = new JsonArray([.. pieces.Select(p => (JsonNode)new JsonObject { ["name"] = p.N, ["w"] = p.W, ["h"] = p.H, ["qty"] = p.Q })]),
        ["fabricWidth"] = 140, ["matCost"] = 8, ["labour"] = 12, ["overhead"] = 20, ["profit"] = 30,
    };

    private void InitProgMode() => RegisterProgram(FachTabs, new ProgramSpec("mode-programm", "SPEC-08", "Kleidung & Produkte")
    {
        Sentence = "Ein Produkt von der Figur bis zum Datenblatt – mit Größen, Varianten, Stoff und Preis.",
        Folder = "Produkte",
        Templates =
        [
            new("leer", "Leeres Produkt", "Alles selbst eintragen", () => ModeDoc("Neues Produkt", "", 96, 80, 100, true, ["S", "M", "L"], ["Schwarz"])),
            new("tshirt", "T-Shirt", "Vorder-/Rückenteil, Ärmel; Größen S–XL", () => ModeDoc("T-Shirt Basic", "100 % Baumwolle", 100, 86, 102, false, ["S", "M", "L", "XL"], ["Weiß", "Schwarz", "Navy"], ("Vorderteil", 55, 75, 1), ("Rückenteil", 55, 75, 1), ("Ärmel", 40, 25, 2))),
            new("rock", "Rock", "Zwei Bahnen und Bund; Größen 36–42", () => ModeDoc("Faltenrock", "Wollmischung", 90, 72, 98, true, ["36", "38", "40", "42"], ["Grau", "Bordeaux"], ("Bahn vorn", 70, 65, 1), ("Bahn hinten", 70, 65, 1), ("Bund", 80, 8, 1))),
            new("tasche", "Stofftasche", "Zwei Seiten, Henkel; eine Größe", () => ModeDoc("Einkaufstasche", "Canvas", 0, 0, 0, true, ["Einheitsgröße"], ["Natur", "Grün"], ("Seite", 40, 45, 2), ("Henkel", 8, 65, 2))),
        ],
        Ribbon =
        [
            ("Produkt", [
                new ProgramCmd("variants", "Varianten erzeugen", d => d.Set("variants", new JsonArray([.. Fashion.Variants(d.Text("sku"), Strings(d, "sizes"), Strings(d, "colors"), d.Text("material")).Select(v => (JsonNode)v.Sku)]), "Varianten"), true),
                new ProgramCmd("size", "Größe aus Maßen", d => { var (eu, letter, note) = Fashion.FromBody(d.Flag("women"), d.Number("chest"), d.Number("waist"), d.Number("hip")); d.Set("bodySize", $"{eu} ({letter}){(note.Length > 0 ? " – " + note : "")}", "Größe"); }),
            ]),
            ("Schnittteile", [
                new ProgramCmd("piece-add", "Teil hinzufügen", d => d.Edit("Teil hinzufügen", x => { var l = x["pieces"] as JsonArray ?? []; l.Add(new JsonObject { ["name"] = $"Teil {l.Count + 1}", ["w"] = 30, ["h"] = 40, ["qty"] = 1 }); x["pieces"] = l; })),
                new ProgramCmd("piece-del", "Letztes Teil entfernen", d => d.Edit("Teil entfernen", x => { if (x["pieces"] is JsonArray { Count: > 0 } l) l.RemoveAt(l.Count - 1); })),
            ]),
        ],
        Exports =
        [
            new("sheet", "Datenblatt", ".md", (d, f) => { File.WriteAllText(f, FashionPlus.DataSheet(d.Text("name"), d.Text("sku"), d.Text("description"), d.Text("material"), ModeVariants(d), ModePrice(d), d.Text("care"))); return f; }),
            new("csv", "Varianten (CSV)", ".csv", (d, f) => { File.WriteAllText(f, Fashion.VariantsCsv(ModeVariants(d)), new System.Text.UTF8Encoding(true)); return f; }),
        ],
        Workspace = ModeWorkspace,
        Properties = ModeProperties,
        Status = d => { var (m, rows) = ModeFabric(d); return $"{ModeVariants(d).Count} Varianten · Stoff {m:0.##} m bei {d.Number("fabricWidth", 140):0} cm Breite · Preis {ModePrice(d).Gross:0.00} € brutto"; },
        Help = "Links die Figur mit deinen Maßen, in der Mitte Größen, Varianten und Zuschnitt. Rechts trägst du Werte ein – jede Änderung lässt sich mit Strg+Z zurücknehmen.",
    }, "kleidung", "produkt", "schnitt", "datenblatt", "varianten", "nähen");

    static List<string> Strings(ProgramDocument d, string key) => d.List(key).Select(x => x?.GetValue<string>() ?? "").Where(x => x.Length > 0).ToList();
    static IReadOnlyList<ProductVariant> ModeVariants(ProgramDocument d) => Fashion.Variants(d.Text("sku"), Strings(d, "sizes"), Strings(d, "colors"), d.Text("material"));
    static PriceCalc ModePrice(ProgramDocument d) => Fashion.Price(d.Number("matCost"), d.Number("labour"), d.Number("overhead"), d.Number("profit"));
    static List<(string Name, double W, double H, int Q)> ModePieces(ProgramDocument d) =>
        d.List("pieces").OfType<JsonObject>().Select(p => ((string?)p["name"] ?? "Teil", ProgramDocument.ToNumber(p["w"]), ProgramDocument.ToNumber(p["h"]), (int)ProgramDocument.ToNumber(p["qty"], 1))).ToList();
    static (double Meters, int Rows) ModeFabric(ProgramDocument d) { var p = ModePieces(d); return p.Count == 0 ? (0, 0) : Fashion.Fabric(p.Select(x => (x.W, x.H, x.Q)), d.Number("fabricWidth", 140)); }

    private Control ModeWorkspace(ProgramDocument d)
    {
        // Figur mit Maßlinien (U1)
        var fig = new Canvas { Name = "mode-figure", Width = 220, Height = 360 };
        var skin = Brush.Parse(C("#3A5C8F", "#C8D6EA")); var line = Brush.Parse("#F5A524");
        fig.Children.Add(new Ellipse { Width = 46, Height = 54, Fill = skin, [Canvas.LeftProperty] = 87.0, [Canvas.TopProperty] = 6.0 });
        fig.Children.Add(new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(d.Flag("women")
            ? "M80,64 L140,64 L160,120 L150,124 L146,150 L134,170 L156,300 L64,300 L86,170 L74,150 L70,124 L60,120 Z"
            : "M74,64 L146,64 L166,124 L154,128 L148,170 L150,300 L70,300 L72,170 L66,128 L54,124 Z"), Fill = skin });
        void Measure(string label, double y, double half, double value)
        {
            if (value <= 0) return;
            fig.Children.Add(new Line { StartPoint = new Point(110 - half, y), EndPoint = new Point(110 + half, y), Stroke = line, StrokeThickness = 2, StrokeDashArray = [3, 2] });
            fig.Children.Add(new TextBlock { Text = $"{label} {value:0} cm", Foreground = Text, FontSize = 11, FontWeight = FontWeight.SemiBold, [Canvas.LeftProperty] = 110 + half + 4, [Canvas.TopProperty] = y - 8 });
        }
        Measure("Brust", 100, 42, d.Number("chest")); Measure("Taille", 165, 34, d.Number("waist")); Measure("Hüfte", 205, 44, d.Number("hip"));
        var (eu, letter, _) = d.Number("chest") > 0 ? Fashion.FromBody(d.Flag("women"), d.Number("chest"), d.Number("waist"), d.Number("hip")) : (0, "", "");
        var figure = new StackPanel { Spacing = 6, Children = { H("Figur", 13), fig, new TextBlock { Name = "mode-size", Text = eu > 0 ? $"Passende Größe: {eu} ({letter})" : "Maße rechts eintragen", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold } } };

        // Größentabelle (F3) und Varianten (F2)
        var sizes = Strings(d, "sizes"); var colors = Strings(d, "colors");
        var table = new Grid { Name = "mode-sizes", ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("Auto", sizes.Count + 1))) };
        void Cell(string t, int r, int c, bool head = false) { var tb = new TextBlock { Text = t, Foreground = head ? Muted : Text, FontSize = 12, FontWeight = head ? FontWeight.SemiBold : FontWeight.Normal, Margin = new Thickness(6, 3) }; Grid.SetRow(tb, r); Grid.SetColumn(tb, c); table.Children.Add(tb); }
        for (var r = 0; r < 4; r++) table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Cell("Größe", 0, 0, true); Cell("Brust", 1, 0, true); Cell("Taille", 2, 0, true); Cell("Hüfte", 3, 0, true);
        var baseIdx = Math.Max(0, sizes.Count / 2);
        for (var i = 0; i < sizes.Count; i++)
        {
            Cell(sizes[i], 0, i + 1, true);
            var k = i - baseIdx;
            Cell(d.Number("chest") > 0 ? $"{d.Number("chest") + 4 * k:0}" : "–", 1, i + 1); Cell(d.Number("waist") > 0 ? $"{d.Number("waist") + 4 * k:0}" : "–", 2, i + 1); Cell(d.Number("hip") > 0 ? $"{d.Number("hip") + 4 * k:0}" : "–", 3, i + 1);
        }
        var variants = new WrapPanel { Name = "mode-variants" };
        string Swatch(string c) => c.ToLowerInvariant() switch { "schwarz" => "#1F1F1F", "weiß" or "weiss" => "#F5F5F5", "navy" => "#1E2F5C", "grau" => "#8A8F98", "bordeaux" => "#6E1A2D", "natur" => "#E8DCC4", "grün" => "#3C7D4A", "rot" => "#C0392B", "blau" => "#2F6BF6", _ => "#B0B6C0" };
        foreach (var v in ModeVariants(d).Take(24))
            variants.Children.Add(new Border { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 4), CornerRadius = new CornerRadius(8), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Ellipse { Width = 12, Height = 12, Fill = Brush.Parse(Swatch(v.Color)), Stroke = Edge }, new TextBlock { Text = $"{v.Size} · {v.Color}", Foreground = Text, FontSize = 12 } } } });

        // Zuschnitt auf der Stoffbahn (V4: Leinwand)
        var width = d.Number("fabricWidth", 140); var (meters, _) = ModeFabric(d);
        var scale = 3.0; var cut = new Canvas { Name = "mode-layout", Width = width * scale, Height = Math.Max(60, meters * 100 * scale), Background = Brush.Parse(C("#22FFFFFF", "#11000000")) };
        double x = 0, y = 0, rowH = 0; var colorsCut = new[] { "#2F86F6", "#22B35E", "#F08A3C", "#7C4DFF", "#E5484D" }; var ci = 0;
        foreach (var p in ModePieces(d))
            for (var q = 0; q < p.Q; q++)
            {
                double w = p.W + 3, h = p.H + 3;
                if (x + w > width) { x = 0; y += rowH; rowH = 0; }
                var rect = new Border { Width = w * scale - 2, Height = h * scale - 2, CornerRadius = new CornerRadius(3), Background = Brush.Parse(colorsCut[ci % colorsCut.Length]), Opacity = 0.8,
                    Child = new TextBlock { Text = p.Name, Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(3) } };
                Canvas.SetLeft(rect, x * scale); Canvas.SetTop(rect, y * scale); cut.Children.Add(rect);
                x += w; rowH = Math.Max(rowH, h);
                if (q == p.Q - 1) ci++;
            }
        var layout = new StackPanel { Spacing = 4, Children = { H($"Zuschnitt · {meters:0.##} m Stoff", 13), new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, MaxHeight = 320, Content = new Viewbox { MaxWidth = 520, Stretch = Stretch.Uniform, Child = cut } } } };

        var price = ModePrice(d);
        var card = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(14), Background = Brushes.White, BoxShadow = BoxShadows.Parse("0 6 18 -6 #55000000"),
            Child = new StackPanel { Spacing = 3, Children = {
                new TextBlock { Name = "mode-card-name", Text = d.Text("name"), Foreground = Brush.Parse("#1F2933"), FontSize = 17, FontWeight = FontWeight.Bold },
                new TextBlock { Text = d.Text("material"), Foreground = Brush.Parse("#5C6B7A"), FontSize = 12 },
                new TextBlock { Name = "mode-card-price", Text = $"{price.Gross:0.00} €", Foreground = Brush.Parse("#1E4FD8"), FontSize = 22, FontWeight = FontWeight.Bold },
                new TextBlock { Text = $"Marge {price.MarginPct:0} % · netto {price.Net:0.00} €", Foreground = Brush.Parse("#5C6B7A"), FontSize = 11 } } } };

        return new Grid { ColumnDefinitions = new ColumnDefinitions("240,16,*"), Children = {
            figure,
            Pos(new StackPanel { Spacing = 12, Children = { card, H("Größentabelle", 13), table, H($"Varianten ({ModeVariants(d).Count})", 13), variants, layout } }, 2) } };
    }

    private Control ModeProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        void Txt(string key, string label, bool multi = false)
        {
            var t = new TextBox { Name = "mode-p-" + key, Text = d.Text(key), PlaceholderText = label, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 50 : 0 };
            void Commit() { if ((t.Text ?? "") != d.Text(key)) { d.Set(key, t.Text ?? "", label); Show("mode-programm"); } }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(P(label, null, 11)); sp.Children.Add(t);
        }
        void Num(string key, string label)
        {
            var t = new TextBox { Name = "mode-p-" + key, Text = d.Number(key).ToString("0.##", De), Width = 90 };
            void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && v != d.Number(key)) { d.Set(key, v, label); Show("mode-programm"); } }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(Row(P(label, Text, 12), t));
        }
        void Csv(string key, string label)
        {
            var t = new TextBox { Name = "mode-p-" + key, Text = string.Join(", ", Strings(d, key)) };
            void Commit() { var l = (t.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); if (string.Join(",", l) != string.Join(",", Strings(d, key))) { d.Set(key, new JsonArray([.. l.Select(x => (JsonNode)x)]), label); Show("mode-programm"); } }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(P(label, null, 11)); sp.Children.Add(t);
        }
        Txt("name", "Name"); Txt("sku", "Artikelnummer"); Txt("material", "Material"); Txt("description", "Beschreibung", true); Txt("care", "Pflege");
        Csv("sizes", "Größen (mit Komma)"); Csv("colors", "Farben (mit Komma)");
        var women = new CheckBox { Name = "mode-p-women", Content = "Damengrößen", IsChecked = d.Flag("women") };
        women.IsCheckedChanged += (_, _) => { if ((women.IsChecked == true) != d.Flag("women")) { d.Set("women", women.IsChecked == true, "Damen/Herren"); Show("mode-programm"); } };
        sp.Children.Add(H("Maße", 12)); sp.Children.Add(women);
        Num("chest", "Brust cm"); Num("waist", "Taille cm"); Num("hip", "Hüfte cm"); Num("fabricWidth", "Stoffbreite cm");
        sp.Children.Add(H("Kalkulation", 12));
        Num("matCost", "Material €"); Num("labour", "Arbeit €"); Num("overhead", "Gemeinkosten %"); Num("profit", "Gewinn %");
        return sp;
    }
}
