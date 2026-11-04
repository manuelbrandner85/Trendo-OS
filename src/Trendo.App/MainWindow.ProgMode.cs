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
        GroupLevels = new() { ["Schnittteile"] = 1 },
        ScrollWorkspace = true,
        Sentence = "Ein Produkt von der Figur bis zum Datenblatt – mit Größen, Varianten, Stoff und Preis.",
        FillHeight = true,
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
            new("svg", "Schnittlage (SVG, 1:1)", ".svg", (d, f) => { var w = d.Number("fabricWidth", 140); File.WriteAllText(f, Marker.Svg(d.Text("name"), MarkerPieces(d), MarkerLayout(d, w), w)); return f; }),
            new("csv", "Varianten (CSV)", ".csv", (d, f) => { File.WriteAllText(f, Fashion.VariantsCsv(ModeVariants(d)), new System.Text.UTF8Encoding(true)); return f; }),
        ],
        Workspace = ModeWorkspace,
        Properties = ModeProperties,
        Status = d => { var (m, rows) = ModeFabric(d); return $"{ModeVariants(d).Count} Varianten · Stoff {m:0.##} m bei {d.Number("fabricWidth", 140):0} cm Breite · Preis {ModePrice(d).Gross:0.00} € brutto"; },
        Help = "Links die Figur mit deinen Maßen, in der Mitte Größen, Varianten und Zuschnitt. Rechts trägst du Werte ein – jede Änderung lässt sich mit Strg+Z zurücknehmen.",
    }, "kleidung", "produkt", "schnitt", "datenblatt", "varianten", "nähen", "mode", "größe", "schuhgröße", "stoff", "sku", "variante", "kalkulation", "preis", "ean", "etikett");

    static List<string> Strings(ProgramDocument d, string key) => d.List(key).Select(x => x.AsText() ?? "").Where(x => x.Length > 0).ToList();
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
            var st = d.Number("gradeStep", 4);
            Cell(d.Number("chest") > 0 ? $"{d.Number("chest") + st * k:0}" : "–", 1, i + 1); Cell(d.Number("waist") > 0 ? $"{d.Number("waist") + st * k:0}" : "–", 2, i + 1); Cell(d.Number("hip") > 0 ? $"{d.Number("hip") + st * k:0}" : "–", 3, i + 1);
        }
        var variants = new WrapPanel { Name = "mode-variants" };
        string Swatch(string c) => c.ToLowerInvariant() switch { "schwarz" => "#1F1F1F", "weiß" or "weiss" => "#F5F5F5", "navy" => "#1E2F5C", "grau" => "#8A8F98", "bordeaux" => "#6E1A2D", "natur" => "#E8DCC4", "grün" => "#3C7D4A", "rot" => "#C0392B", "blau" => "#2F6BF6", _ => "#B0B6C0" };
        foreach (var v in ModeVariants(d).Take(24))
            variants.Children.Add(new Border { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 4), CornerRadius = new CornerRadius(8), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Ellipse { Width = 12, Height = 12, Fill = Brush.Parse(Swatch(v.Color)), Stroke = Edge }, new TextBlock { Text = $"{v.Size} · {v.Color}", Foreground = Text, FontSize = 12 } } } });

        // Schnittlagebild (Masterprompt 131): Teile ziehen, drehen, automatisch legen
        var width = d.Number("fabricWidth", 140);
        var layout = ModeMarker(d, width);
        var price = ModePrice(d);
        var card = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(14), Background = Brushes.White, MaxWidth = 380, HorizontalAlignment = HorizontalAlignment.Left, BoxShadow = BoxShadows.Parse("0 6 18 -6 #55000000"),
            Child = new StackPanel { Spacing = 3, Children = {
                new TextBlock { Name = "mode-card-name", Text = d.Text("name"), Foreground = Brush.Parse("#1F2933"), FontSize = 17, FontWeight = FontWeight.Bold },
                new TextBlock { Text = d.Text("material"), Foreground = Brush.Parse("#5C6B7A"), FontSize = 12 },
                new TextBlock { Name = "mode-card-price", Text = $"{price.Gross:0.00} €", Foreground = Brush.Parse("#1E4FD8"), FontSize = 22, FontWeight = FontWeight.Bold },
                new TextBlock { Text = $"Marge {price.MarginPct:0} % · netto {price.Net:0.00} €", Foreground = Brush.Parse("#5C6B7A"), FontSize = 11 } } } };

        // die Programm-Hülle scrollt die Arbeitsfläche schon (ScrollWorkspace) – kein zweiter Scrollbereich darin (Prüfung „Einfachheit“, 04.11.2026)
        return new Grid { Name = "mode-scroll", ColumnDefinitions = new ColumnDefinitions("240,16,*"), Margin = new Thickness(4, 4, 10, 4), Children = {
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
        void Num(string key, string label, double def = 0)
        {
            var t = new TextBox { Name = "mode-p-" + key, Text = d.Number(key, def).ToString("0.##", De), Width = 90 };
            void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && v != d.Number(key, def)) { d.Set(key, v, label); Show("mode-programm"); } }
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
        Txt("name", "Name"); Txt("sku", "Artikelnummer"); Txt("material", "Material");
        if (d.Text("material").Contains('%')) // Textiletikett sofort prüfen (EU-Textilverordnung)
        {
            var lab = FashionPlus.TextileLabel(d.Text("material"));
            sp.Children.Add(new TextBlock { Name = "mode-label-check", Text = lab.Ok ? "✓ Etikett zulässig" : "⚠ " + string.Join(" · ", lab.Problems) + (lab.Label == "" ? "" : $" – Vorschlag: {lab.Label}"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse(lab.Ok ? "#4ADE80" : "#F5A524") });
        }
        Txt("description", "Beschreibung", true); Txt("care", "Pflege");
        Csv("sizes", "Größen (mit Komma)"); Csv("colors", "Farben (mit Komma)");
        var women = new CheckBox { Name = "mode-p-women", Content = "Damengrößen", IsChecked = d.Flag("women") };
        women.IsCheckedChanged += (_, _) => { if ((women.IsChecked == true) != d.Flag("women")) { d.Set("women", women.IsChecked == true, "Damen/Herren"); Show("mode-programm"); } };
        sp.Children.Add(H("Maße", 12)); sp.Children.Add(women);
        Num("chest", "Brust cm"); Num("waist", "Taille cm"); Num("hip", "Hüfte cm"); Num("gradeStep", "Sprung je Größe cm", 4); Num("gradeLen", "Längensprung je Größe cm", 1); Num("fabricWidth", "Stoffbreite cm", 140);
        // Schnittteile direkt bearbeiten: Name, Breite × Höhe × Anzahl
        sp.Children.Add(H("Schnittteile", 12));
        var pieces = d.List("pieces").OfType<JsonObject>().ToList();
        for (var i = 0; i < pieces.Count; i++)
        {
            var idx = i; var pc = pieces[i];
            TextBox F(string key, string val, double width) => new() { Name = $"mode-pc-{key}-{idx}", Text = val, Width = width, Padding = new Thickness(4, 2), FontSize = 12 };
            var n = F("name", (string?)pc["name"] ?? "", 86); var w = F("w", ProgramDocument.ToNumber(pc["w"]).ToString("0.#", De), 40); var h = F("h", ProgramDocument.ToNumber(pc["h"]).ToString("0.#", De), 40); var q = F("qty", ProgramDocument.ToNumber(pc["qty"], 1).ToString("0", De), 30);
            void Commit()
            {
                double Val(TextBox t, double old) => double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && v > 0 ? v : old;
                var cur = d.List("pieces").OfType<JsonObject>().ElementAtOrDefault(idx); if (cur is null) return;
                var nn = n.Text?.Trim() is { Length: > 0 } s ? s : (string?)cur["name"] ?? "Teil";
                var (nw, nh, nq) = (Val(w, ProgramDocument.ToNumber(cur["w"])), Val(h, ProgramDocument.ToNumber(cur["h"])), (int)Val(q, ProgramDocument.ToNumber(cur["qty"], 1)));
                if (nn == (string?)cur["name"] && nw == ProgramDocument.ToNumber(cur["w"]) && nh == ProgramDocument.ToNumber(cur["h"]) && nq == (int)ProgramDocument.ToNumber(cur["qty"], 1)) return;
                d.Edit("Schnittteil ändern", x => { if (x["pieces"] is JsonArray l && l[idx] is JsonObject o) { o["name"] = nn; o["w"] = nw; o["h"] = nh; o["qty"] = nq; } }); Show("mode-programm");
            }
            foreach (var t in new[] { n, w, h, q }) { t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit); }
            var del = new Button { Name = $"mode-pc-del-{idx}", Content = "✕", Padding = new Thickness(5, 1) }; ToolTip.SetTip(del, "Teil entfernen");
            del.Click += (_, _) => { d.Edit("Teil entfernen", x => { if (x["pieces"] is JsonArray l && idx < l.Count) l.RemoveAt(idx); }); Show("mode-programm"); };
            TextBlock X() => new() { Text = "×", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0) };
            sp.Children.Add(new WrapPanel { Children = { n, w, X(), h, X(), q, del } });
        }
        sp.Children.Add(H("Kalkulation", 12));
        Num("matCost", "Material €"); Num("labour", "Arbeit €"); Num("overhead", "Gemeinkosten %"); Num("profit", "Gewinn %");
        // Rechner, die sofort bei der Eingabe rechnen (ersetzen die frühere Formularseite „Rechner“)
        var calcs = new StackPanel { Spacing = 6 };
        LiveCalc(calcs, "mode-c-shoe", "Schuhgröße", [("eu", "EU-Größe oder Fußlänge (z. B. 26 cm)", "42", null)],
            v => { var s = v("eu"); var eu = s.Contains("cm", StringComparison.OrdinalIgnoreCase) ? Fashion.ShoeFromFoot(Fach.Num(s.ToLowerInvariant().Replace("cm", ""), "Fußlänge")) : Fach.Num(s, "Größe"); var x = Fashion.Shoe(eu); return $"EU {Fach.F(x.Eu, 1)} · Fuß {Fach.F(x.Cm, 1)} cm · UK {Fach.F(x.Uk, 1)} · US Herren {Fach.F(x.UsMen, 1)} · US Damen {Fach.F(x.UsWomen, 1)}"; });
        LiveCalc(calcs, "mode-c-kid", "Kindergröße", [("h", "Körpergröße (cm)", "95", null)], v => { var k = FashionPlus.KidSize(Fach.Num(v("h"))); return $"Größe {k.Size} (etwa {k.Age})"; });
        LiveCalc(calcs, "mode-c-bra", "BH-Größe", [("ub", "Unterbrust (cm)", "75", null), ("b", "Brust (cm)", "91", null)], v => $"Größe {FashionPlus.BraSize(Fach.Num(v("ub")), Fach.Num(v("b")))} (EU)");
        LiveCalc(calcs, "mode-c-ring", "Ringgröße", [("c", "Innenumfang (mm)", "54", null)], v => { var r = FashionPlus.Ring(Fach.Num(v("c"))); return $"EU {Fach.F(r.Eu, 0)} · Ø {Fach.F(r.DiameterMm, 1)} mm · US {Fach.F(r.Us, 1)}"; });
        LiveCalc(calcs, "mode-c-grade", "Maß gradieren", [("val", "Maß der Basisgröße (cm)", "100", null), ("step", "Sprung (cm)", d.Number("gradeStep", 4).ToString("0.#", De), null)],
            v => { var sz = Strings(d, "sizes"); if (sz.Count == 0) throw new ArgumentException("Erst Größen eintragen."); return string.Join(" · ", FashionPlus.Grade(sz, sz[sz.Count / 2], Fach.Num(v("val")), Fach.Num(v("step"))).Select(g => $"{g.Size}: {Fach.F(g.Value, 1)} cm")); });
        LiveCalc(calcs, "mode-c-yarn", "Garnbedarf (Stricken)", [("w", "Breite (cm)", "50", null), ("h", "Länge (cm)", "60", null), ("s", "Maschen / 10 cm", "22", null), ("r", "Reihen / 10 cm", "30", null), ("m", "m je Knäuel", "125", null)],
            v => { var y = FashionPlus.Yarn(Fach.Num(v("w")), Fach.Num(v("h")), Fach.Num(v("s")), Fach.Num(v("r")), Fach.Num(v("m"))); return $"{y.Stitches} Maschen × {y.Rows} Reihen · ca. {Fach.F(y.Meters, 0)} m → {y.Balls} Knäuel"; });
        LiveCalc(calcs, "mode-c-ean", "EAN-Strichcode", [("e", "12 Ziffern oder EAN-13", "400638133393", null)],
            v => { var dg = new string(v("e").Where(char.IsDigit).ToArray()); return dg.Length == 13 ? (FashionPlus.EanValid(dg) ? "✓ Gültige EAN-13." : $"⚠ Prüfziffer falsch – richtig wäre {FashionPlus.EanCheckDigit(dg[..12])}.") : $"Vollständige EAN: {dg}{FashionPlus.EanCheckDigit(dg)}"; });
        LiveCalc(calcs, "mode-c-tier", "Staffelpreise (vom Ladenpreis)", [("q", "Menge", "50", null), ("t", "Stufen „ab Menge:Rabatt %“", "10:5 25:10 100:15", null)],
            v => { var tiers = v("t").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':')).Select(x => x.Length == 2 ? ((int)Fach.Num(x[0]), Fach.Num(x[1])) : throw new FormatException("Stufe bitte als „10:5“.")); var r = FashionPlus.Tier(ModePrice(d).Gross, (int)Fach.Num(v("q")), tiers); return $"{Fach.F(r.DiscountPct, 0)} % Rabatt → {Fach.F(r.Unit)} € je Stück, zusammen {Fach.F(r.Total)} €"; });
        sp.Children.Add(new Expander { Name = "mode-calcs", Header = "Rechner (Schuh, Kind, BH, Ring, Garn, EAN, Staffel)", IsExpanded = false, Content = calcs, Margin = new Thickness(0, 8, 0, 0) });
        return sp;
    }

    // ---------- Schnittlagebild ----------
    public int MarkerSelected { get; set; } = -1;
    public MarkerCanvas? MarkerForTests { get; private set; }
    static List<Marker.Piece> MarkerPieces(ProgramDocument d) => ModePieces(d).Select(p => new Marker.Piece(p.Name, p.W, p.H, p.Q)).ToList();

    /// <summary>Gespeicherte Lage, wenn sie zu den Teilen passt – sonst automatisch gelegt (noch nicht gespeichert).</summary>
    public static List<Marker.Placed> MarkerLayout(ProgramDocument d, double width)
    {
        var pieces = MarkerPieces(d);
        var saved = d.List("layout").OfType<JsonObject>().Select(o => new Marker.Placed((int)ProgramDocument.ToNumber(o["p"]), (int)ProgramDocument.ToNumber(o["c"]), ProgramDocument.ToNumber(o["x"]), ProgramDocument.ToNumber(o["y"]), o["r"].AsBool())).ToList();
        var want = pieces.SelectMany((p, i) => Enumerable.Range(0, p.Qty).Select(c => (i, c))).ToHashSet();
        if (saved.Count == want.Count && saved.All(x => want.Contains((x.Piece, x.Copy)))) return saved;
        try { return Marker.AutoLayout(pieces, width); } catch (ArgumentException) { return []; }
    }
    static JsonArray MarkerJson(IEnumerable<Marker.Placed> l) => new([.. l.Select(x => (JsonNode)new JsonObject { ["p"] = x.Piece, ["c"] = x.Copy, ["x"] = x.X, ["y"] = x.Y, ["r"] = x.Rot })]);

    /// <summary>Schnitt-Editor: Größe der Schnittlage ("" = Basisgröße, die bearbeitet wird; sonst abgeleitet und automatisch gelegt).</summary>
    public string MarkerSize { get; set; } = "";
    static string MarkerBase(ProgramDocument d) => Strings(d, "sizes") is { Count: > 0 } s ? s[s.Count / 2] : "";
    /// <summary>Teile und Lage für die gewählte Größe: Basis = bearbeitbare Lage, andere Größen gradiert und automatisch gelegt.</summary>
    (List<Marker.Piece> Pieces, List<Marker.Placed> Layout, bool Derived) MarkerFor(ProgramDocument d, double width)
    {
        var sizes = Strings(d, "sizes"); var idx = sizes.IndexOf(MarkerSize);
        if (MarkerSize.Length == 0 || idx < 0 || MarkerSize == MarkerBase(d)) return (MarkerPieces(d), MarkerLayout(d, width), false);
        var g = Marker.Grade(MarkerPieces(d), idx - sizes.Count / 2, d.Number("gradeStep", 4), d.Number("gradeLen", 1));
        try { return (g, Marker.AutoLayout(g, width), true); } catch (ArgumentException) { return (g, [], true); }
    }
    static string MarkerFile(ProgramDocument d, string size, string ext) => DevStudio.SafeName(d.Text("name")) + (size.Length > 0 ? $" {size}" : "") + " – Schnittlage" + ext;

    private Control ModeMarker(ProgramDocument d, double width)
    {
        const string K = "mode-programm";
        var (pieces, lay, derived) = MarkerFor(d, width);
        var sizeLabel = derived ? MarkerSize : MarkerBase(d);
        var canvas = new MarkerCanvas { Name = "mode-marker", Pieces = pieces, Layout = lay, FabricW = width, Scale = 3, Selected = derived ? -1 : MarkerSelected };
        MarkerForTests = canvas;
        if (!derived)
        {
            canvas.Moved = (i, dx, dy) => ProgramRun(K, () => { var l = MarkerLayout(d, width); l[i] = l[i] with { X = Math.Round(l[i].X + dx), Y = Math.Max(0, Math.Round(l[i].Y + dy)) }; d.Set("layout", MarkerJson(l), "Teil verschieben"); MarkerSelected = i; });
            canvas.Rotated = i => ProgramRun(K, () => { var l = MarkerLayout(d, width); l[i] = l[i] with { Rot = !l[i].Rot }; d.Set("layout", MarkerJson(l), "Teil drehen"); MarkerSelected = i; });
            canvas.Clicked = i => { MarkerSelected = i; canvas.Selected = i; canvas.InvalidateVisual(); }; // nicht neu aufbauen – sonst kommt ein Doppelklick nie an
        }
        var len = Marker.Length(pieces, lay); var eff = Marker.Efficiency(pieces, lay, width); var bad = Marker.Problems(pieces, lay, width);
        Button B(string name, string label, Action a, bool primary = false) { var b = new Button { Name = name, Content = label, FontSize = 12, Margin = new Thickness(0, 0, 4, 4) }; if (primary) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => ProgramRun(K, a); return b; }
        // Größe wählen: Basis bearbeiten oder abgeleitete Größe ansehen
        var sizes = Strings(d, "sizes");
        var pick = new WrapPanel { Name = "mode-marker-sizes" };
        foreach (var sz in sizes)
        {
            var size = sz; var on = sz == sizeLabel;
            var b = new Button { Name = "mode-marker-size-" + sz, Content = sz == MarkerBase(d) ? sz + " (Basis)" : sz, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(12),
                Background = on ? Accent : Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { MarkerSize = size == MarkerBase(d) ? "" : size; MarkerSelected = -1; Show(K); };
            pick.Children.Add(b);
        }
        var bar = new WrapPanel();
        if (!derived)
        {
            bar.Children.Add(B("mode-marker-auto", "Automatisch legen", () => { d.Set("layout", MarkerJson(Marker.AutoLayout(pieces, width)), "Automatisch legen"); MarkerSelected = -1; }, true));
            bar.Children.Add(B("mode-marker-rotate", "↻ Drehen", () => { if (MarkerSelected < 0 || MarkerSelected >= lay.Count) throw new InvalidOperationException("Erst ein Teil anklicken."); canvas.Rotated!(MarkerSelected); }));
        }
        var title = d.Text("name") + (sizeLabel.Length > 0 ? $" ({sizeLabel})" : "");
        bar.Children.Add(B("mode-marker-pdf", "Als PDF", () => { var f = Path.Combine(WorkFolder("Produkte"), MarkerFile(d, derived ? MarkerSize : "", ".pdf")); Marker.Pdf(f, title, pieces, lay, width); _progMsg[K] = "Gespeichert: " + f; NotifyFileResult(_progMsg[K]); }));
        bar.Children.Add(B("mode-marker-svg", "Als SVG (1:1)", () => { var f = Path.Combine(WorkFolder("Produkte"), MarkerFile(d, derived ? MarkerSize : "", ".svg")); File.WriteAllText(f, Marker.Svg(title, pieces, lay, width)); _progMsg[K] = "Gespeichert: " + f; NotifyFileResult(_progMsg[K]); }));
        var facts = new TextBlock { Name = "mode-marker-facts", Text = $"Verbrauch {len / 100:0.00} m bei {width:0} cm Breite · Ausnutzung {eff * 100:0} % · Verschnitt {100 - eff * 100:0} %" + (bad.Count > 0 ? $" · ⚠ {bad.Count} Teile überlappen oder ragen über den Rand" : "") + (lay.Count == 0 && pieces.Count > 0 ? " · ⚠ ein Teil ist breiter als der Stoff" : ""), Foreground = bad.Count > 0 || (lay.Count == 0 && pieces.Count > 0) ? Brush.Parse("#F87171") : Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
        // Stoffbedarf aller Größen
        var run = new Grid { Name = "mode-marker-run", ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("Auto", sizes.Count + 1))) };
        if (sizes.Count > 1 && pieces.Count > 0)
        {
            run.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); run.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            void Cell(string t, int r, int c, bool head) { var tb = new TextBlock { Text = t, Foreground = head ? Muted : Text, FontSize = 12, FontWeight = head ? FontWeight.SemiBold : FontWeight.Normal, Margin = new Thickness(6, 2) }; Grid.SetRow(tb, r); Grid.SetColumn(tb, c); run.Children.Add(tb); }
            Cell("Größe", 0, 0, true); Cell("Stoff", 1, 0, true);
            var all = Marker.SizeRun(MarkerPieces(d), sizes, width, d.Number("gradeStep", 4), d.Number("gradeLen", 1));
            for (var i = 0; i < all.Count; i++) { Cell(all[i].Size, 0, i + 1, true); Cell(double.IsNaN(all[i].Meters) ? "passt nicht" : $"{all[i].Meters:0.00} m", 1, i + 1, false); }
        }
        var hint = derived
            ? $"Größe {MarkerSize} ist aus der Basis {MarkerBase(d)} abgeleitet (Breite ± Sprung/4 = {d.Number("gradeStep", 4) / 4:0.#} cm, Länge ± {d.Number("gradeLen", 1):0.#} cm je Größe) und automatisch gelegt. Bearbeiten in der Basisgröße."
            : "Teil ziehen: verschieben (1 cm) · Doppelklick: drehen · gestrichelt: Nahtlinie";
        return new StackPanel { Spacing = 4, Children = { H("Schnittlagebild", 13), pick, bar, facts,
            new TextBlock { Name = "mode-marker-hint", Text = hint, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap },
            new ScrollViewer { Name = "mode-marker-strip", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = canvas }, // nur waagerecht wie eine Zeitleiste
            sizes.Count > 1 ? H("Stoffbedarf je Größe", 12) : new Border(), run } };
    }
}
