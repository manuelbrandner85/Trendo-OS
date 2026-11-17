using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>SPEC-08 (Freigabe 08.11.2026): echter Schnitt, echte Stoffe, Zuschnitt, 3D-Anprobe und Produktion im Programm „Kleidung &amp; Produkte“.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Stoffphysik nachbilden. Argumente: Auftragsdatei; Rückgabe: Meldung bei Fehler, sonst null.</summary>
    public Func<string, Task<string?>>? GarmentRunOverride { get; set; }
    public string GarmentMessage { get; private set; } = "";
    public IReadOnlyList<string> GarmentImages { get; private set; } = [];
    private bool _garmentBusy;

    private static BodyMeasures GarmentBody(ProgramDocument d)
    {
        var women = d.Flag("women");
        return new BodyMeasures(d.Number("height", women ? 168 : 180), d.Number("chest", women ? 92 : 100), d.Number("waist", women ? 74 : 86), d.Number("hip", women ? 98 : 102), women);
    }

    private static string GarmentKind(ProgramDocument d) => d.Text("gKind") is { Length: > 0 } k ? k : d.Text("name").Contains("rock", StringComparison.OrdinalIgnoreCase) ? "rock" : "tshirt";

    private Garment? GarmentOf(ProgramDocument d)
    {
        // T-0745: frei bearbeitete Teile ersetzen die Vorlage – für Fläche, Zuschnitt, PDF/DXF/SVG und 3D-Anprobe
        try { return GarmentEdit.Apply(GarmentBlocks.Build(GarmentKind(d), GarmentBody(d)), GarmentEdits(d)); }
        catch (ArgumentException e) { GarmentMessage = e.Message; return null; }
    }

    private static JsonObject? GarmentEdits(ProgramDocument d) => d.Data[GarmentEdit.Field] as JsonObject;

    private static string GarmentColor(ProgramDocument d) => d.Text("gColor") is { Length: > 0 } c ? c : Strings(d, "colors").FirstOrDefault() ?? "Grau";

    private readonly Dictionary<string, GarmentEditState> _garmentEdit = [];
    /// <summary>T-0745: der Schnitt-Editor der zuletzt gezeichneten Seite (für Tests und die Leiste).</summary>
    public GarmentPatternEditor? GarmentEditor { get; private set; }

    /// <summary>T-0745: Schnittteile frei bearbeiten – Leiste, Arbeitsfläche, Hinweiszeile und Maße. Jede Änderung ist ein Rückgängig-Schritt im Dokument.</summary>
    private Control GarmentPatternCanvas(ProgramDocument d, Garment g)
    {
        var st = _garmentEdit.TryGetValue(d.Id, out var s0) ? s0 : _garmentEdit[d.Id] = new GarmentEditState();
        var kind = g.Kind; var edits = GarmentEdits(d);
        var edited = GarmentEdit.Edited(edits, g); // nur wirksame Bearbeitungen (Gegenprüfung T-0745)
        var hint = new TextBlock { Name = "garment-edit-hint", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Text = "Klicke ein Schnittteil an, um es zu bearbeiten." };
        var measures = new StackPanel { Name = "garment-measures", Spacing = 3 };
        var tools = new WrapPanel { Name = "garment-edit-tools" };
        GarmentPatternEditor ed = null!;
        List<PatNode> NodesOf(int i, PatPart p)
        {
            if ((edits?[kind] as JsonObject)?[p.Name] is { } j && GarmentEdit.FromJson(j) is { } n)
                try { GarmentEdit.FromNodes(p, n); return n; } catch (ArgumentException) { }
            return GarmentEdit.ToNodes(p);
        }
        void Commit(int i, List<PatNode>? nodes, string label, string? group)
        {
            // Gegenprüfung T-0745: Pfeil-Schritte am selben Punkt innerhalb einer Sekunde, ohne andere Änderung dazwischen, werden EIN Rückgängig-Schritt
            var now = st.Clock(); var cur = d.Remember();
            var join = group is not null && st.Group == group && now - st.GroupAt <= GarmentEditState.GroupWindow
                && st.GroupAfter is { } after && after.Seq == cur.Seq && after.Undo == cur.Undo && after.Data == cur.Data;
            var start = join ? st.GroupStart : cur;
            ProgramRun(ModeKey, () =>
            {
                if (nodes is not null)
                {
                    nodes = GarmentEdit.Merge(nodes); // (fast) doppelte Punkte zusammenführen; Kante ohne Länge → Meldung
                    GarmentEdit.FromNodes(g.Parts[i], nodes); // ungültig (z. B. Fläche 0) → Meldung statt kaputter Daten
                }
                var all = GarmentEdit.With(GarmentEdits(d), kind, g.Parts[i].Name, nodes);
                d.Set(GarmentEdit.Field, all.Count == 0 ? null : all, label);
                if (join) d.Squash(start, label);
            });
            st.Group = group; st.GroupStart = start; st.GroupAt = now; st.GroupAfter = group is null ? null : d.Remember();
        }
        ed = new GarmentPatternEditor(g, st, NodesOf, Commit, t => { hint.Text = t; }, Text, Muted);
        GarmentEditor = ed;

        var advanced = ProgramLevel != Trendo.Core.Programs.UserLevel.Einfach;
        Button T(string name, string label, string tip, string cmd)
        {
            var b = Act(name, label, () => ed.Act(cmd));
            ToolTip.SetTip(b, tip);
            return b;
        }
        var bIns = T("garment-edit-insert", "Punkt einfügen", "Neuer Punkt auf der gewählten Kante (Einfg oder Doppelklick auf die Kante)", "insert");
        var bDel = T("garment-edit-delete", "Punkt löschen", "Gewählten Punkt entfernen (Entf)", "delete");
        var bCurve = T("garment-edit-toggle", "Gerade ↔ Kurve", "Gewählte Kante als Gerade oder als Kurve mit Griffen (K)", "toggle");
        var bReset = T("garment-edit-reset", "Teil zurücksetzen", "Gewähltes Teil wieder aus den Maßen berechnen", "reset");
        // „Einfach“ bleibt übersichtlich (V10: höchstens 40 Knöpfe je Ansicht): dort geht alles mit der Maus und über den Rechtsklick, die Leiste ab „Erweitert“
        if (advanced) foreach (var b in new[] { bIns, bDel, bCurve, bReset }) tools.Children.Add(MSp(b));

        void Refresh()
        {
            var i = st.Part; var sel = i >= 0 && i < g.Parts.Count;
            var n = sel ? ed.Nodes(i) : [];
            bIns.IsEnabled = sel && (st.Seg >= 0 || st.Node >= 0); bDel.IsEnabled = sel && st.Node >= 0;
            bCurve.IsEnabled = sel && (st.Seg >= 0 || st.Node >= 0); bReset.IsEnabled = sel && edited.Contains(g.Parts[i].Name);
            measures.Children.Clear();
            if (!sel)
            {
                measures.Children.Add(new TextBlock { Name = "garment-measure-part", Text = $"{g.Parts.Count} Teile · zusammen {g.Parts.Sum(p => p.Area * p.Qty):0} cm² Stoff (ohne Nahtzugabe)", Foreground = Text, FontSize = 12 });
                return;
            }
            var p = g.Parts[i];
            measures.Children.Add(new TextBlock { Name = "garment-measure-part", Text = $"{p.Name}{(edited.Contains(p.Name) ? " (bearbeitet)" : "")}: Fläche {p.Area:0} cm² · Umfang {GarmentEdit.Cm(p.Edges.Sum(e => e.Length))} cm · {n.Count} Punkte", Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold });
            measures.Children.Add(new TextBlock { Name = "garment-measure-edges", Text = string.Join(" · ", p.Edges.Select(e => $"{GarmentEdit.EdgeLabel(e.Name)} {GarmentEdit.Cm(e.Length)} cm")), Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            // Gegenprüfung T-0745: Selbstüberschneidung und zu enge Stellen (Zugabe reicht an die Naht) klar melden
            if (GarmentEdit.SelfIntersects(p)) measures.Children.Add(new TextBlock { Name = "garment-measure-cross", Text = "Achtung: Der Umriss überschneidet sich. Fläche, Nahtzugabe und Zuschnitt stimmen so nicht – Punkt zurückziehen oder Strg+Z.", Foreground = Brush.Parse("#FACC15"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            else if (GarmentEdit.Problems(p) is { Count: > 0 } probs) measures.Children.Add(new TextBlock { Name = "garment-measure-gap", Text = "Achtung: " + string.Join(" ", probs), Foreground = Brush.Parse("#FACC15"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            if (!advanced) return;
            // Profi: Nahtlängen gegeneinander prüfen (wie „Nähte abgehen“ in Schnitt-Programmen)
            var seams = GarmentEdit.SeamLengths(g).Where(s => s.Seam.PartA == p.Name || s.Seam.PartB == p.Name).ToList();
            if (seams.Count > 0)
            {
                var lines = seams.Select(s =>
                {
                    var diff = s.A - s.B; var cap = s.Seam.EdgeA.StartsWith("cap", StringComparison.Ordinal) || s.Seam.EdgeB.StartsWith("cap", StringComparison.Ordinal);
                    var ok = Math.Abs(diff) <= (cap ? 0.06 * Math.Max(s.A, s.B) : 0.3);
                    return $"{(ok ? "✓" : "⚠")} {s.Seam.PartA} {GarmentEdit.EdgeLabel(s.Seam.EdgeA)} {GarmentEdit.Cm(s.A)} cm ↔ {s.Seam.PartB} {GarmentEdit.EdgeLabel(s.Seam.EdgeB)} {GarmentEdit.Cm(s.B)} cm" + (ok ? "" : $" (Unterschied {GarmentEdit.Cm(Math.Abs(diff))} cm)");
                });
                measures.Children.Add(new TextBlock { Name = "garment-measure-seams", Text = "Nähte:\n" + string.Join("\n", lines), Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            }
            if (st.Node >= 0 && st.Node < n.Count)
            {
                var q = n[st.Node];
                var bx = new TextBox { Name = "garment-node-x", Width = 80, Text = q.X.ToString("0.##", De) }; var by = new TextBox { Name = "garment-node-y", Width = 80, Text = q.Y.ToString("0.##", De) };
                void Apply(string? field)
                {
                    if (double.TryParse(bx.Text, System.Globalization.NumberStyles.Float, De, out var x) && double.TryParse(by.Text, System.Globalization.NumberStyles.Float, De, out var y)) ed.MoveTo(x, y, field);
                    else hint.Text = "Bitte Zahlen in Zentimetern eingeben, z. B. 24,5.";
                }
                // Enter im Feld: nach dem Neuaufbau bleibt der Fokus in diesem Feld (Gegenprüfung T-0745), die Fläche holt ihn nicht zurück
                foreach (var box in new[] { bx, by })
                {
                    var b = box;
                    OnEnter(b, () => Apply(b.Name));
                    b.GotFocus += (_, _) => st.WantFocus = false;
                    if (st.FocusField == b.Name)
                        b.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (st.FocusField != b.Name || TopLevel.GetTopLevel(b) is null) return; st.FocusField = null; b.Focus(); b.CaretIndex = b.Text?.Length ?? 0; });
                }
                measures.Children.Add(new WrapPanel { Children = {
                    MSp(new TextBlock { Text = $"Punkt {st.Node + 1} genau setzen (cm, Fadenlauf = 0):", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
                    MSp(new TextBlock { Text = "waagrecht", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }), MSp(bx),
                    MSp(new TextBlock { Text = "senkrecht", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }), MSp(by),
                    MSp(Act("garment-node-set", "Übernehmen", () => Apply(null))) } });
            }
        }
        ed.Changed += Refresh;
        Refresh();
        var sp = new StackPanel { Name = "garment-editor", Spacing = 6 };
        if (advanced) sp.Children.Add(tools);
        // eigener waagrechter Bildlauf: die Teile liegen nebeneinander (die Hülle scrollt nur senkrecht)
        sp.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = ed });
        sp.Children.Add(hint);
        if (edited.Count > 0) sp.Children.Add(new TextBlock { Name = "garment-edited-note", Text = $"Frei bearbeitet: {string.Join(", ", edited)}. Diese Teile folgen den Körpermaßen nicht mehr – „Teil zurücksetzen“ rechnet sie wieder aus den Maßen.", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(measures);
        return sp;
    }

    private Control GarmentNestCanvas(Garment g, NestResult nest, double fabricWidth)
    {
        const double scale = 3.0;
        var canvas = new Canvas { Name = "garment-nest", Width = nest.LengthCm * scale, Height = fabricWidth * scale, Background = Brush.Parse("#18FFFFFF"), HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var n in nest.Placed)
        {
            var pts = GarmentProduction.WithAllowance(g.Parts[n.Part]).Select(q => GarmentProduction.Place(q, n)).Select(q => new Point(q.Y * scale, q.X * scale));
            canvas.Children.Add(new Polygon { Points = new Points(pts), Fill = Brush.Parse("#44C9A227"), Stroke = Brush.Parse("#C9A227"), StrokeThickness = 1 });
        }
        return new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = canvas };
    }

    private Control GarmentPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "garment-panel", Spacing = 8 };
        var g = GarmentOf(d);
        var kinds = new WrapPanel();
        foreach (var (id, label) in GarmentBlocks.Kinds)
        {
            var pick = id;
            kinds.Children.Add(MSp(Act("garment-kind-" + id, label, () => ProgramRun("mode-programm", () => d.Set("gKind", pick, "Schnitt wählen")), GarmentKind(d) == id)));
        }
        var fabric = Fabrics.Of(d.Text("gFabric"));
        var combo = new ComboBox { Name = "garment-fabric", ItemsSource = Fabrics.All.Select(f => f.Name).ToList(), SelectedIndex = Array.IndexOf(Fabrics.All, fabric), Width = 220 };
        combo.SelectionChanged += (_, _) => { if (combo.SelectedIndex >= 0 && Fabrics.All[combo.SelectedIndex] != fabric) ProgramRun("mode-programm", () => d.Set("gFabric", Fabrics.All[combo.SelectedIndex].Id, "Stoff wählen")); };
        sp.Children.Add(H("Schnitt und Stoff", 14)); sp.Children.Add(kinds);
        sp.Children.Add(new WrapPanel { Children = { MSp(combo), new TextBlock { Name = "garment-fabric-facts", Text = $"{fabric.Note} · {fabric.Gsm:0} g/m² · Bahn {fabric.WidthCm:0} cm · Dehnung {fabric.StretchPct:0} % · {fabric.PricePerM:0} €/m", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 } } });
        sp.Children.Add(GarmentLook(d));
        if (g is null) { sp.Children.Add(P(GarmentMessage, Brush.Parse("#FACC15"), 12)); return WCard(sp); }

        sp.Children.Add(H("Schnittteile", 13));
        sp.Children.Add(P("Durchgezogen: Schnittkante · gestrichelt: Nahtlinie · Pfeil: Fadenlauf. Teil anklicken, Punkte ziehen (Umschalt rastet auf 5 mm), Doppelklick auf eine Kante setzt einen Punkt, Rechtsklick zeigt alle Befehle.", Muted, 11));
        sp.Children.Add(GarmentPatternCanvas(d, g));
        try
        {
            var nest = GarmentProduction.Nest(g, fabric.WidthCm);
            var meters = nest.LengthCm / 100;
            sp.Children.Add(H("Zuschnitt auf der Stoffbahn", 13));
            sp.Children.Add(new TextBlock { Name = "garment-nest-facts", Text = $"{meters:0.00} m Stoff · Ausnutzung {nest.Efficiency * 100:0} % · Stoffkosten {meters * fabric.PricePerM:0.00} €", Foreground = Text, FontSize = 12.5 });
            sp.Children.Add(GarmentNestCanvas(g, nest, fabric.WidthCm));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { sp.Children.Add(P("Zuschnitt nicht möglich: " + Trendo.Core.FriendlyError.Say(e), Brush.Parse("#FACC15"), 12)); }

        sp.Children.Add(H("3D-Anprobe", 13));
        var run = Act("garment-tryon", _garmentBusy ? "Rechnet …" : "In 3D anprobieren", () => _ = GarmentTryOn(d), true); run.IsEnabled = !_garmentBusy;
        var actions = new WrapPanel { Children = { MSp(run) } };
        if (BlenderPath is null && GarmentRunOverride is null) actions.Children.Add(new TextBlock { Text = "Dafür braucht Trendo Blender. Es lässt sich im Studio „3D-Modelle erstellen“ einrichten.", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
        if (GarmentEdit.Edited(GarmentEdits(d), g).Count > 0) actions.Children.Add(new TextBlock { Name = "garment-tryon-edited", Text = "Die Anprobe näht die frei bearbeiteten Teile.", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(actions);
        if (GarmentMessage.Length > 0) sp.Children.Add(new TextBlock { Name = "garment-msg", Text = GarmentMessage, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        if (GarmentImages.Count > 0)
        {
            var row = new WrapPanel { Name = "garment-images" };
            foreach (var f in GarmentImages.Where(File.Exists))
                try { row.Children.Add(new Image { Source = new Bitmap(f), Width = 300, Margin = new Thickness(0, 0, 8, 8) }); } catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { }
            sp.Children.Add(row);
        }

        sp.Children.Add(H("Für die Produktion", 13));
        if (ProgramLevel == Trendo.Core.Programs.UserLevel.Einfach)
        {
            // V2 (T-0663): in „Einfach“ ein Knopf mit den drei Ausgaben im Menü
            var prod = Act("garment-production", "Für die Produktion speichern …", () => { });
            var list = new StackPanel { Spacing = 2 };
            var fly = new Flyout { Content = list };
            foreach (var (n, h, ext) in new[] { ("garment-pdf", "Schnitt als PDF (A4, 1:1)", ".pdf"), ("garment-dxf", "Datei für den Schneideplotter (DXF)", ".dxf"), ("garment-svg", "Schnitt als Zeichnung (SVG, 1:1)", ".svg"), ("garment-techpack", "Technisches Datenblatt", ".md") })
            {
                var e = ext;
                var item = new Button { Name = n, Content = h, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                item.Click += (_, _) => { fly.Hide(); GarmentExport(d, e); };
                list.Children.Add(item);
            }
            prod.Flyout = fly; // Claude und Strg+K finden die Einträge über das Klappmenü
            sp.Children.Add(new WrapPanel { Children = { MSp(prod) } });
        }
        else sp.Children.Add(new WrapPanel { Children = {
            MSp(Act("garment-pdf", "Schnitt als PDF (A4, 1:1)", () => GarmentExport(d, ".pdf"))),
            MSp(Act("garment-dxf", "Plotter-Datei (DXF)", () => GarmentExport(d, ".dxf"))),
            MSp(Act("garment-svg", "Schnitt als SVG (1:1)", () => GarmentExport(d, ".svg"))),
            MSp(Act("garment-techpack", "Technisches Datenblatt", () => GarmentExport(d, ".md"))) } });
        return WCard(sp);
    }

    private const string ModeKey = "mode-programm";

    /// <summary>Farbe und Muster frei wählen.</summary>
    private Control GarmentLook(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "garment-look", Spacing = 6 };
        WrapPanel Swatches(string prefix, string field, string current)
        {
            var row = new WrapPanel();
            // V2 (T-0663): in „Einfach“ acht Grundfarben (und die gewählte); jede andere Farbe geht über „Eigene Farbe“
            var simple = ProgramLevel == Trendo.Core.Programs.UserLevel.Einfach;
            for (var i = 0; i < Fabrics.Palette.Length; i++)
            {
                var hex = Fabrics.Palette[i]; var on = string.Equals(hex, current, StringComparison.OrdinalIgnoreCase);
                if (simple && i >= 8 && !on) continue;
                var b = new Button { Name = $"{prefix}-{i}", Width = 26, Height = 26, Padding = new Thickness(0), CornerRadius = new CornerRadius(13), Margin = new Thickness(0, 0, 5, 5), Background = Brush.Parse(hex), BorderBrush = on ? Accent : Edge, BorderThickness = new Thickness(on ? 3 : 1) };
                ToolTip.SetTip(b, hex);
                b.Click += (_, _) => ProgramRun(ModeKey, () => d.Set(field, hex, "Farbe wählen"));
                row.Children.Add(b);
            }
            return row;
        }
        var color = GarmentColor(d);
        sp.Children.Add(H("Farbe", 13));
        sp.Children.Add(Swatches("garment-color", "gColor", color));
        var own = new TextBox { Name = "garment-color-own", Width = 130, PlaceholderText = "#RRGGBB", Text = color.StartsWith('#') ? color : "" };
        sp.Children.Add(new WrapPanel { Children = { MSp(own), MSp(Act("garment-color-apply", "Eigene Farbe", () => { var v = (own.Text ?? "").Trim(); if (System.Text.RegularExpressions.Regex.IsMatch(v, "^#[0-9A-Fa-f]{6}$")) ProgramRun(ModeKey, () => d.Set("gColor", v, "Farbe wählen")); else { GarmentMessage = "Bitte eine Farbe wie #A8232D eingeben."; Show(ModeKey); } })) } });
        var kind = d.Text("gPattern", "uni");
        var kinds = new WrapPanel();
        foreach (var (id, label) in Fabrics.Patterns) { var pick = id; kinds.Children.Add(MSp(Act("garment-pattern-" + id, label, () => ProgramRun(ModeKey, () => d.Set("gPattern", pick, "Muster wählen")), kind == id))); }
        sp.Children.Add(H("Muster", 13)); sp.Children.Add(kinds);
        if (kind is "streifen" or "karo" or "punkte" or "bild")
        {
            var size = new TextBox { Name = "garment-pattern-size", Width = 80, Text = d.Number("gPatternSize", kind == "bild" ? 30 : 3).ToString("0.#") };
            var row = new WrapPanel { Children = { MSp(new TextBlock { Text = kind == "bild" ? "Bildbreite in cm" : "Mustergröße in cm", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }), MSp(size),
                MSp(Act("garment-pattern-size-set", "Übernehmen", () => { if (double.TryParse((size.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0) ProgramRun(ModeKey, () => d.Set("gPatternSize", v, "Mustergröße")); })) } };
            sp.Children.Add(row);
            if (kind == "bild")
            {
                var file = d.Text("gPatternImage");
                sp.Children.Add(new WrapPanel { Children = { MSp(Act("garment-pattern-image", "Bild wählen …", () => _ = GarmentPickImage(d))), new TextBlock { Name = "garment-pattern-file", Text = file.Length == 0 ? "Noch kein Bild gewählt." : Path.GetFileName(file), Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center } } });
            }
            else { sp.Children.Add(P("Zweite Farbe des Musters", Muted, 11.5)); sp.Children.Add(Swatches("garment-pattern-color", "gPatternColor", d.Text("gPatternColor", "#F2F0EA"))); }
        }
        return sp;
    }

    private async Task GarmentPickImage(ProgramDocument d)
    {
        var req = new PickRequest("mode-muster", false, false, ["png", "jpg", "jpeg"]);
        string? f;
        if (PickOverride is { } fake) f = (await fake(req))?.FirstOrDefault();
        else
        {
            var got = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Bild für den Stoffdruck", FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }] });
            f = got.Count > 0 ? got[0].TryGetLocalPath() : null;
        }
        if (f is not null) ProgramRun(ModeKey, () => d.Set("gPatternImage", f, "Stoffdruck wählen"));
    }

    /// <summary>Produktionsdatei schreiben (öffentlich für Tests). Rückgabe: Pfad oder null.</summary>
    public string? GarmentExport(ProgramDocument d, string ext)
    {
        var g = GarmentOf(d); if (g is null) { Show("mode-programm"); return null; }
        var folder = Path.Combine(WorkFolder("Mode"), "Produktion"); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, DevStudio.SafeName(d.Text("name", "Entwurf")) + "-" + g.Kind + ext);
        try
        {
            var note = "";
            if (ext == ".pdf") note = $" ({GarmentProduction.PdfTiled(file, g, d.Text("name", "Entwurf"))} Seiten, mit 100 % drucken)";
            else if (ext == ".dxf") File.WriteAllText(file, GarmentProduction.Dxf(g));
            else if (ext == ".svg") File.WriteAllText(file, GarmentProduction.Svg(g, d.Text("name", "Entwurf")));
            else File.WriteAllText(file, GarmentProduction.TechPack(d.Text("name", "Entwurf"), g, GarmentBody(d), Fabrics.Of(d.Text("gFabric")), GarmentColor(d), Strings(d, "sizes"), GarmentEdit.Edited(GarmentEdits(d), g)));
            // Gegenprüfung T-0745: Teile, die sich so nicht sauber zuschneiden lassen, beim Export deutlich nennen
            var warn = g.Parts.SelectMany(GarmentEdit.Problems).ToList();
            if (warn.Count > 0) note += " · Achtung: " + string.Join(" ", warn);
            GarmentMessage = "Gespeichert: " + file + note;
            _core.Audit.Append(Principal.LocalOwner.Name, "mode.produktion", Path.GetFileName(file));
            NotifyFileResult(GarmentMessage);
            Show("mode-programm");
            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { GarmentMessage = "Speichern ging nicht: " + Trendo.Core.FriendlyError.Say(e); Show("mode-programm"); return null; }
    }

    private Task<string?> GarmentTextures(FabricInfo fabric) => AmbientTexture(fabric.TextureId);

    /// <summary>Kostenlose Oberfläche von ambientCG (CC0) holen; schon geladene werden wiederverwendet. Offline oder im Testmodus: null.</summary>
    private async Task<string?> AmbientTexture(string id)
    {
        if (_opt.Isolated || _core.Offline) return null;
        var existing = Bib.Items().FirstOrDefault(i => i.Source == Trendo.Core.Bibliothek.AssetLibrary.AmbientCg && i.Id == id && Directory.Exists(i.File));
        if (existing is not null) return existing.File;
        var item = await Bib.Download(new Trendo.Core.Bibliothek.AssetHit(Trendo.Core.Bibliothek.AssetLibrary.AmbientCg, id, id, "material", "Material", "", "CC0", "ambientCG", "https://ambientcg.com/a/" + id));
        return item.File;
    }

    /// <summary>Anprobe starten (öffentlich für Tests): Auftrag schreiben, Stoffoberfläche laden, Blender ohne Fenster rechnen lassen.</summary>
    public async Task GarmentTryOn(ProgramDocument d)
    {
        if (_garmentBusy) return;
        var g = GarmentOf(d); if (g is null) { Show("mode-programm"); return; }
        var blender = BlenderPath;
        if (blender is null && GarmentRunOverride is null) { GarmentMessage = "Für die 3D-Anprobe wird Blender gebraucht. Richte es im Studio „3D-Modelle erstellen“ ein."; Show("mode-programm"); return; }
        _garmentBusy = true; GarmentMessage = "Stoff wird berechnet und fotografiert. Das dauert einige Minuten."; GarmentImages = []; Show("mode-programm");
        try
        {
            var dir = Path.Combine(WorkFolder("Mode"), "Anprobe", DevStudio.SafeName(d.Text("name", "Entwurf"))); Directory.CreateDirectory(dir);
            var fabric = Fabrics.Of(d.Text("gFabric"));
            var job = GarmentBlocks.ToJson(g, GarmentBody(d));
            var jobFabric = Fabrics.ToJob(fabric, GarmentColor(d), await GarmentTextures(fabric));
            if (Fabrics.ToJobPattern(d.Text("gPattern"), d.Text("gPatternColor"), d.Number("gPatternSize", 3), d.Text("gPatternImage")) is { } pattern) jobFabric["pattern"] = pattern;
            job["fabric"] = jobFabric;
            job["frames"] = 110; job["samples"] = 200; job["out"] = Path.Combine(dir, "bild");
            var jobFile = Path.Combine(dir, "auftrag.json"); File.WriteAllText(jobFile, job.ToJsonString());
            _core.Audit.Append(Principal.LocalOwner.Name, "mode.anprobe", g.Kind + " " + fabric.Id);
            string? problem;
            if (GarmentRunOverride is { } fake) problem = await fake(jobFile);
            else
            {
                var script = Path.Combine(dir, "trendo_anprobe.py");
                using (var s = AssetLoader.Open(new Uri("avares://TrendoOS/Assets/ki/trendo_anprobe.py"))) using (var t = File.Create(script)) await s.CopyToAsync(t);
                var r = await Task.Run(() => Cli.Run(blender!, ["--python", script, "--", jobFile], dir, 1_800_000));
                problem = r.Ok && r.Out.Contains("TRENDO_FERTIG") ? null : (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n').LastOrDefault(l => l.Trim().Length > 0) ?? "Blender hat abgebrochen.";
            }
            GarmentImages = new[] { "nah", "vorn", "schraeg", "hinten" }.Select(v => Path.Combine(dir, $"bild-{v}.png")).Where(File.Exists).ToList();
            GarmentMessage = problem is not null ? "Anprobe abgebrochen: " + problem : GarmentImages.Count == 0 ? "Blender hat kein Bild geliefert." : "Fertig. Die Bilder liegen in: " + dir;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or HttpRequestException or UnauthorizedAccessException or System.Text.Json.JsonException) { GarmentMessage = "Anprobe ging nicht: " + Trendo.Core.FriendlyError.Say(e); }
        finally { _garmentBusy = false; Show("mode-programm"); }
    }
}
