using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>SPEC-01 / T-0256: Architektur & Innenraum als echtes Planungsprogramm „Grundriss“ – Zeichenfläche mit Werkzeugen (Wand, Raum,
/// Tür, Fenster, Möbel), Fang und Maßen, 3D-Ansicht, Eigenschaften der Auswahl mit Flächen, Material, Heizung und Licht, Planprüfung,
/// Export als PDF, SVG, DXF, Raumbuch und Materialliste. Ersetzt die frühere Formularseite.</summary>
public partial class MainWindow
{
    const string AK = "architektur";
    private RoomPlanner? _rooms;
    /// <summary>Frühere Raumliste (nur noch zum Übernehmen in einen Grundriss).</summary>
    public RoomPlanner Rooms => _rooms ??= new RoomPlanner(_core.DataRoot);
    public readonly PlanViewState ArchState = new();
    private bool _arch3D;
    private List<PlanIssue>? _archIssues;

    static readonly (string Id, string Label, string Glyph, string Tip)[] ArchTools =
    [
        ("auswahl", "Auswahl", "M4,2 L4,20 L9,15 L12,22 L15,21 L12,14 L19,14 Z", "Auswählen und verschieben (Esc)"),
        ("wand", "Wand", "M2,8 L22,8 L22,16 L2,16 Z", "Wände zeichnen – Klick für Klick, Länge tippen + Enter"),
        ("raum", "Raum", "M3,3 L21,3 L21,21 L3,21 Z M6,6 L6,18 L18,18 L18,6 Z", "Raum aufziehen oder per Doppelklick erkennen"),
        ("tuer", "Tür", "M4,20 L4,4 L12,4 L12,20 M12,20 A8,8 0 0 0 4,12", "Tür in eine Wand setzen"),
        ("fenster", "Fenster", "M3,9 L21,9 L21,15 L3,15 Z M3,12 L21,12", "Fenster in eine Wand setzen"),
        ("moebel", "Möbel", "M3,12 L21,12 L21,19 L3,19 Z M5,7 L19,7 L19,12 L5,12 Z", "Möbel aus dem Katalog stellen"),
    ];

    private void InitSpec01() => RegisterProgram(FachTabs, new ProgramSpec(AK, "SPEC-01", "Grundriss & Wohnen")
    {
        Sentence = "Wohnung oder Haus planen: Wände, Räume, Türen, Fenster und Möbel – mit Maßen, 3D und Materialliste.",
        Folder = "Grundrisse",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leerer Grundriss", "Selbst Räume aufziehen und Wände zeichnen", () => ArchDoc(new FloorPlan())),
            new("wohnung", "2-Zimmer-Wohnung", "Wohnen, Schlafen, Küche, Bad, Flur – eingerichtet", () => ArchDoc(FloorPlan.Template("wohnung"))),
            new("haus", "Einfamilienhaus EG", "Wohnen & Essen, Küche, Arbeiten, Gäste-WC, Terrasse", () => ArchDoc(FloorPlan.Template("haus"))),
            new("bad", "Badezimmer", "Bad mit Wanne, WC und Waschbecken", () => ArchDoc(FloorPlan.Template("bad"))),
            new("raumliste", "Aus Raumliste übernehmen", "Räume aus der früheren Raumliste als Rechtecke", () => ArchDoc(FloorPlan.FromRoomList(Rooms.Project.Rooms))),
        ],
        Ribbon =
        [
            ("Ansicht", [
                new ProgramCmd("arch-2d3d", "2D / 3D", _ => _arch3D = !_arch3D, false, "Zwischen Grundriss und 3D-Modell wechseln"),
                new ProgramCmd("arch-fit", "Einpassen", _ => { ArchState.Fitted = false; ArchState.UserView = false; }, false, "Ganzen Plan zeigen"),
                new ProgramCmd("arch-grid", "Raster", _ => ArchState.Grid = !ArchState.Grid, false, "10-cm-Raster und Fang an/aus"),
                new ProgramCmd("arch-dims", "Maße", _ => ArchState.Dims = !ArchState.Dims, false, "Wandmaße zeigen/ausblenden"),
            ]),
            ("Prüfen", [
                new ProgramCmd("arch-check", "Plan prüfen", d => { _archIssues = FloorPlan.From(d.Data).Check(); _progMsg[AK] = _archIssues.Count == 0 ? "✓ Keine Probleme gefunden: alle Räume mit Tür, genug Fensterfläche, nichts steht im Weg." : $"{_archIssues.Count} Hinweis(e) – rechts unter „Prüfung“."; }, true),
            ]),
        ],
        Exports =
        [
            new("pdf", "PDF (Plan)", ".pdf", (d, f) => PlanDrawing.Pdf(FloorPlan.From(d.Data), f)),
            new("svg", "SVG", ".svg", (d, f) => { File.WriteAllText(f, PlanDrawing.Svg(FloorPlan.From(d.Data))); return f; }),
            new("dxf", "DXF (CAD)", ".dxf", (d, f) => { File.WriteAllText(f, PlanDrawing.Dxf(FloorPlan.From(d.Data))); return f; }),
            new("raumbuch", "Raumbuch (CSV)", ".csv", (d, f) => { File.WriteAllText(f, PlanDrawing.RoomBookCsv(FloorPlan.From(d.Data)), new System.Text.UTF8Encoding(true)); return f; }),
            new("material", "Materialliste", ".md", (d, f) => { File.WriteAllText(f, PlanDrawing.MaterialList(FloorPlan.From(d.Data))); return f; }),
        ],
        Workspace = ArchWorkspace,
        Properties = ArchProperties,
        Status = d => { var p = FloorPlan.From(d.Data); return $"{ArchTools.First(t => t.Id == ArchState.Tool).Label} · {p.Rooms.Count} Räume · Wohnfläche {p.LivingArea():0.00} m² · {p.Walls.Count} Wände · {p.Openings.Count(o => o.Kind == "tuer")} Türen · {p.Openings.Count(o => o.Kind == "fenster")} Fenster · {p.Items.Count} Möbel"; },
        Help = "Links das Werkzeug wählen. Raum: Rechteck aufziehen – die Wände entstehen außen herum. Wand: Punkt für Punkt klicken, Länge tippen und Enter. Tür/Fenster: auf eine Wand klicken. "
             + "Auswahl: anklicken und ziehen, Ecken an den weißen Griffen. Entf löscht, R dreht Möbel, Strg+Z nimmt zurück. Mausrad zoomt, mittlere Maustaste oder Leertaste + Ziehen verschiebt.",
    }, "architektur", "raum", "wohnung", "grundriss", "farbe", "streichen", "boden", "laminat", "innenraum", "haus", "möbel", "einrichten", "wand", "tür", "fenster");

    static System.Text.Json.Nodes.JsonObject ArchDoc(FloorPlan p) { var o = new System.Text.Json.Nodes.JsonObject(); p.WriteTo(o); return o; }

    /// <summary>Änderung am Plan über das Dokument (Rückgängig, automatisch sichern), danach neu zeichnen.</summary>
    public void ArchEdit(string label, Action<FloorPlan> change)
    {
        var d = ProgramActive(AK) ?? throw new InvalidOperationException("Es ist kein Grundriss offen.");
        _archIssues = null; ArchState.Hint = "";
        ProgramRun(AK, () => d.Edit(label, x => { var p = FloorPlan.From(x); change(p); p.WriteTo(x); }));
    }

    public void ArchTool(string tool) { ArchState.Tool = tool; ArchState.Chain = null; ArchState.Hint = ""; if (tool != "auswahl") ArchState.Selected = null; Show(AK); }
    public void ArchSelect(string? id) { ArchState.Selected = id; Show(AK); }
    public void ArchToggle3D() { _arch3D = !_arch3D; Show(AK); }
    public bool Arch3D => _arch3D;

    private Control ArchWorkspace(ProgramDocument d)
    {
        var plan = FloorPlan.From(d.Data);
        // Werkzeugleiste links
        var tools = new StackPanel { Name = "arch-tools", Spacing = 3, Width = 66 };
        foreach (var (id, label, glyph, tip) in ArchTools)
        {
            var on = ArchState.Tool == id; var tid = id;
            var b = new Button { Name = "arch-tool-" + id, Width = 62, Height = 48, Padding = new Thickness(2), CornerRadius = new CornerRadius(10), Focusable = false,
                Background = on ? Accent : Brush.Parse(C("#1AFFFFFF", "#0D000000")), BorderBrush = on ? Brush.Parse("#F5A524") : Edge, BorderThickness = new Thickness(on ? 1.5 : 1),
                Content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(glyph), Stroke = on ? Brushes.White : Text, StrokeThickness = 1.6, Fill = Brushes.Transparent, Stretch = Stretch.Uniform, Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = label, FontSize = 11, Foreground = on ? Brushes.White : Text, HorizontalAlignment = HorizontalAlignment.Center } } } };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => ArchTool(tid);
            tools.Children.Add(b);
        }
        var toolScroll = new ScrollViewer { Content = tools, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var thick = new ComboBox { Name = "arch-thickness", Width = 62, FontSize = 11, ItemsSource = new[] { "11,5", "17,5", "24", "30", "36,5" }, SelectedItem = ArchState.WallThickness.ToString("0.#", De) };
        thick.SelectionChanged += (_, _) => { if (thick.SelectedItem is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, De, out var v)) ArchState.WallThickness = v; };
        ToolTip.SetTip(thick, "Wandstärke für neue Wände (cm)");
        tools.Children.Add(new TextBlock { Text = "Wand cm", FontSize = 10, Foreground = Muted, Margin = new Thickness(2, 4, 0, 0) });
        tools.Children.Add(thick);

        Control view;
        if (_arch3D)
            view = new CadView { Name = "arch-3d", MinHeight = 320, Parts = Plan3D.Build(plan).Select(x => (x.Color, x.Solid, x.Id is not null && x.Id == ArchState.Selected)).ToList() };
        else
        {
            var fv = new FloorPlanView(plan, ArchState, ArchEdit, ArchSelect) { Name = "arch-canvas", MinHeight = 320 };
            if (!ArchState.Fitted) fv.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (fv.Bounds.Width > 10) { fv.Fit(); ArchState.Fitted = true; } });
            view = fv;
        }
        var frame = new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = view };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,10,*"), Children = { toolScroll, Pos(frame, 2) } };
    }

    private Control ArchProperties(ProgramDocument d)
    {
        var p = FloorPlan.From(d.Data); var sp = new StackPanel { Spacing = 5 };
        var sel = ArchState.Selected;
        TextBox Num(string name, string label, double value, Action<FloorPlan, double> set, string unit = "cm")
        {
            var t = new TextBox { Name = "arch-p-" + name, Text = value.ToString("0.#", De), Width = 90 };
            void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && Math.Abs(v - value) > 1e-6) ArchEdit(label, pl => set(pl, v)); }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(Row(P($"{label} ({unit})", Text, 12), t)); return t;
        }
        void Pick(string name, string label, string value, string[] options, Func<string, string> text, Action<FloorPlan, string> set)
        {
            var c = new ComboBox { Name = "arch-p-" + name, Width = 150, ItemsSource = options.Select(text).ToList(), SelectedIndex = Math.Max(0, Array.IndexOf(options, value)) };
            c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0 && options[c.SelectedIndex] != value) ArchEdit(label, pl => set(pl, options[c.SelectedIndex])); };
            sp.Children.Add(Row(P(label, Text, 12), c));
        }
        Button Act2(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 4, 0) }; b.Click += (_, _) => go(); return b; }
        void Fact(string label, string value) => sp.Children.Add(Row(P(label, null, 12), new TextBlock { Text = value, Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold }));

        if (sel is not null && p.WallOf(sel) is { } w)
        {
            sp.Children.Add(H("Wand", 13));
            Num("length", "Länge", Math.Round(w.Length, 1), (pl, v) => pl.SetWallLength(sel, v));
            Num("thickness", "Stärke", w.Thickness, (pl, v) => { if (v is < 5 or > 100) throw new ArgumentException("Die Wandstärke muss zwischen 5 und 100 cm liegen."); pl.WallOf(sel)!.Thickness = v; });
            Num("height", "Höhe", w.Height, (pl, v) => pl.WallOf(sel)!.Height = Math.Clamp(v, 50, 600));
            Fact("Fläche je Seite", $"{w.Length * w.Height / 10000:0.00} m²");
            sp.Children.Add(Act2("arch-del", "Wand löschen", () => { ArchState.Selected = null; ArchEdit("Wand löschen", pl => pl.Remove(sel)); }));
        }
        else if (sel is not null && p.Openings.FirstOrDefault(o => o.Id == sel) is { } o)
        {
            sp.Children.Add(H(o.Kind == "tuer" ? "Tür" : "Fenster", 13));
            Num("width", "Breite", o.Width, (pl, v) => { var x = pl.Openings.First(y => y.Id == sel); var wl = pl.WallOf(x.WallId)!.Length; if (v < 30 || v + 10 > wl) throw new ArgumentException($"Die Breite muss zwischen 30 und {wl - 10:0} cm liegen."); x.Width = v; x.At = Math.Clamp(x.At, v / 2 + 5, wl - v / 2 - 5); });
            Num("oheight", "Höhe", o.Height, (pl, v) => pl.Openings.First(y => y.Id == sel).Height = Math.Clamp(v, 30, 300));
            if (o.Kind == "fenster") Num("sill", "Brüstung", o.Sill, (pl, v) => pl.Openings.First(y => y.Id == sel).Sill = Math.Clamp(v, 0, 200));
            else
            {
                sp.Children.Add(new WrapPanel { Children = {
                    Act2("arch-hinge", o.HingeLeft ? "Anschlag: links ⇄" : "Anschlag: rechts ⇄", () => ArchEdit("Anschlag wechseln", pl => { var x = pl.Openings.First(y => y.Id == sel); x.HingeLeft = !x.HingeLeft; })),
                    Act2("arch-swing", "Richtung ⇅", () => ArchEdit("Öffnungsrichtung", pl => { var x = pl.Openings.First(y => y.Id == sel); x.Inward = !x.Inward; })) } });
            }
            Fact("Fläche", $"{o.Width * o.Height / 10000:0.00} m²");
            sp.Children.Add(Act2("arch-del", "Entfernen", () => { ArchState.Selected = null; ArchEdit("Öffnung entfernen", pl => pl.Remove(sel)); }));
        }
        else if (sel is not null && p.Items.FirstOrDefault(i => i.Id == sel) is { } it)
        {
            sp.Children.Add(H(it.Name, 13));
            var name = new TextBox { Name = "arch-p-name", Text = it.Name };
            void Rename() { if (!string.IsNullOrWhiteSpace(name.Text) && name.Text != it.Name) ArchEdit("Umbenennen", pl => pl.Items.First(x => x.Id == sel).Name = name.Text!.Trim()); }
            name.LostFocus += (_, _) => Rename(); OnEnter(name, Rename); sp.Children.Add(name);
            Num("iw", "Breite", it.W, (pl, v) => pl.Items.First(x => x.Id == sel).W = Math.Clamp(v, 10, 600));
            Num("id", "Tiefe", it.D, (pl, v) => pl.Items.First(x => x.Id == sel).D = Math.Clamp(v, 10, 600));
            Num("rot", "Drehung", it.Rot, (pl, v) => pl.Items.First(x => x.Id == sel).Rot = ((v % 360) + 360) % 360, "°");
            if (it.Type == "treppe")
            {
                var st = Interior.Stairs(275, it.D);
                sp.Children.Add(H("Treppe (Geschoss 2,75 m)", 12));
                Fact("Steigungen", $"{st.Steps} à {st.RiseCm:0.0} cm"); Fact("Auftritt", $"{st.TreadCm:0.0} cm"); Fact("Neigung", $"{st.AngleDeg:0.0}°");
                sp.Children.Add(P(st.Verdict, null, 11));
            }
            sp.Children.Add(new WrapPanel { Children = {
                Act2("arch-rotate", "↻ 90°", () => ArchEdit("Drehen", pl => { var x = pl.Items.First(y => y.Id == sel); x.Rot = (x.Rot + 90) % 360; })),
                Act2("arch-dup", "Duplizieren", () => ArchEdit("Duplizieren", pl => { var x = pl.Items.First(y => y.Id == sel); var c = new Item { Type = x.Type, Name = x.Name, X = x.X + 30, Y = x.Y + 30, W = x.W, D = x.D, Rot = x.Rot }; pl.Items.Add(c); ArchState.Selected = c.Id; })),
                Act2("arch-del", "Löschen", () => { ArchState.Selected = null; ArchEdit("Möbel löschen", pl => pl.Remove(sel)); }) } });
        }
        else if (sel is not null && p.Rooms.FirstOrDefault(r => r.Id == sel) is { } r)
        {
            var f = p.Figures(r); var eq = p.AsRoom(r);
            sp.Children.Add(H("Raum", 13));
            var name = new TextBox { Name = "arch-p-name", Text = r.Name };
            void Rename() { if (!string.IsNullOrWhiteSpace(name.Text) && name.Text != r.Name) ArchEdit("Umbenennen", pl => pl.Rooms.First(x => x.Id == sel).Name = name.Text!.Trim()); }
            name.LostFocus += (_, _) => Rename(); OnEnter(name, Rename); sp.Children.Add(name);
            Pick("kind", "Nutzung", r.Kind, PlanDrawing.Kinds, PlanDrawing.KindName, (pl, v) => pl.Rooms.First(x => x.Id == sel).Kind = v);
            Pick("floor", "Boden", r.Floor, PlanDrawing.Floors, PlanDrawing.FloorName, (pl, v) => pl.Rooms.First(x => x.Id == sel).Floor = v);
            Pick("finish", "Wände", r.WallFinish, PlanDrawing.Finishes, PlanDrawing.FinishName, (pl, v) => pl.Rooms.First(x => x.Id == sel).WallFinish = v);
            Num("rheight", "Raumhöhe", r.Height, (pl, v) => pl.Rooms.First(x => x.Id == sel).Height = Math.Clamp(v, 50, 600));
            sp.Children.Add(H("Kennzahlen", 12));
            Fact("Fläche", $"{f.Area:0.00} m²"); Fact("Umfang", $"{f.Perimeter:0.00} m"); Fact("Wandfläche netto", $"{f.WallNet:0.00} m²");
            Fact("Fenster", $"{f.Windows} · {f.WindowArea:0.00} m² ({(f.Area > 0 ? f.WindowArea / f.Area * 100 : 0):0} % der Fläche)"); Fact("Türen", f.Doors.ToString());
            sp.Children.Add(H("Material", 12));
            if (r.Floor == "fliesen") { var (tiles, packs, grout) = Interior.Tiles(f.Area, 30, 60, 3, 10, 1.44); Fact("Fliesen 30 × 60", $"{tiles} St. = {packs} Pakete"); Fact("Fugenmörtel", $"ca. {grout:0.0} kg"); }
            else { Fact(PlanDrawing.FloorName(r.Floor), $"{Math.Ceiling(f.Area * 1.1 / 2.2):0} Pakete à 2,2 m²"); Fact("Sockelleiste", $"{f.Skirting:0.00} m"); }
            if (r.WallFinish == "tapete") { var (strips, _, rolls) = Interior.Wallpaper(eq); Fact("Tapete", $"{strips} Bahnen → {rolls} Rollen"); }
            else if (r.WallFinish == "farbe") Fact("Farbe (2×, Wand + Decke)", $"{(f.WallNet + f.Area) * 2 / 10:0.0} l");
            sp.Children.Add(H("Technik (grob)", 12));
            Fact("Heizleistung", $"ca. {Interior.HeatLoad(eq, "teilsaniert"):0} W");
            var light = Interior.Light(eq, r.Kind switch { "kueche" => "küche", "bad" => "bad", "arbeiten" => "büro", "kind" => "kinderzimmer", "flur" => "flur", "schlafen" => "schlafzimmer", _ => "wohnzimmer" });
            Fact("Licht", $"{light.Lumen:0} lm ≈ {light.Bulbs806} LED à 806 lm");
            sp.Children.Add(Act2("arch-del", "Raum löschen", () => { ArchState.Selected = null; ArchEdit("Raum löschen", pl => pl.Remove(sel)); }));
        }
        else if (ArchState.Tool == "moebel")
        {
            sp.Children.Add(H("Möbelkatalog", 13));
            foreach (var g in FurnitureCatalog.All.GroupBy(x => x.Group))
            {
                sp.Children.Add(P(g.Key, null, 11));
                var wrap = new WrapPanel();
                foreach (var f in g)
                {
                    var ft = f; var on = ArchState.ItemType == f.Type;
                    var b = new Button { Name = "arch-item-" + f.Type, Content = f.Name, FontSize = 11.5, Padding = new Thickness(8, 4), Margin = new Thickness(0, 0, 4, 4), Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
                    ToolTip.SetTip(b, $"{f.W:0} × {f.D:0} cm, Höhe {f.H:0} cm");
                    b.Click += (_, _) => { ArchState.ItemType = ft.Type; Show(AK); };
                    wrap.Children.Add(b);
                }
                sp.Children.Add(wrap);
            }
        }
        else
        {
            var n = p.Need();
            sp.Children.Add(H("Projekt", 13));
            Fact("Wohnfläche (WoFlV)", $"{p.LivingArea():0.00} m²"); Fact("Räume", p.Rooms.Count.ToString());
            foreach (var room in p.Rooms) { var rr = room; var b = new Button { Name = "arch-room-" + p.Rooms.IndexOf(room), HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(6, 3),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = room.Name, Foreground = Text, FontSize = 12 }, Pos(P($"{room.Area / 10000:0.00} m²", null, 11), 1) } } };
                b.Click += (_, _) => ArchSelect(rr.Id); sp.Children.Add(b); }
            sp.Children.Add(H("Material gesamt", 12));
            Fact("Boden", $"{n.Floor:0.00} m² → {n.FloorPacks} Pakete"); Fact("Farbe", $"{n.PaintLiters:0.0} l = {n.PaintBuckets} Eimer"); Fact("Sockelleisten", $"{n.SkirtingMeters:0.00} m");
            sp.Children.Add(P("Tipp: Element anklicken, um es zu bearbeiten. Export über das Menüband.", null, 11));
        }
        // Prüfergebnis oben, damit es nach „Plan prüfen“ sofort zu sehen ist
        if (_archIssues is { Count: > 0 } issues)
        {
            var box = new StackPanel { Name = "arch-issues", Spacing = 0, Margin = new Thickness(0, 0, 0, 8) };
            box.Children.Add(H($"Prüfung ({issues.Count})", 12));
            var k = 0;
            foreach (var i in issues.Take(12))
            {
                var target = i.TargetId;
                var b = new Button { Name = $"arch-issue-{k++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brush.Parse(i.Level == "fehler" ? "#33EF4444" : i.Level == "warnung" ? "#33F5A524" : "#227FD3FF"), Padding = new Thickness(8, 4), Margin = new Thickness(0, 0, 0, 3),
                    Content = new TextBlock { Text = (i.Level == "fehler" ? "✕ " : i.Level == "warnung" ? "⚠ " : "ℹ ") + i.Message, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap } };
                b.Click += (_, _) => { ArchState.Tool = "auswahl"; ArchSelect(target); };
                box.Children.Add(b);
            }
            sp.Children.Insert(0, box);
        }
        return sp;
    }
}
