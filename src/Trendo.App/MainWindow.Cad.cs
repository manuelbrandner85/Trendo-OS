using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Cad;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>N23 Trendo CAD: parametrische Teile wie in FreeCAD, aber leicht verständlich. Bauplan aus Schritten, Maße als Formeln mit Parametern,
/// mehrere Teile als Baugruppe, Export STL/3MF/STEP und technische Zeichnung. Weiter zum 3D-Druck (N24).</summary>
public partial class MainWindow
{
    public CadDoc Cad { get; private set; } = CadStarter();
    public string? CadPath { get; private set; }
    public bool CadDirty { get; private set; }
    public int CadBodyIndex { get; private set; }
    public int CadFeatureIndex { get; private set; }
    public CadView? CadCanvas { get; private set; }
    private readonly List<CadDoc> _cadUndo = [], _cadRedo = [];
    private string _cadMsg = "";
    private List<(CadBody Body, Solid Solid, string? Error)> _cadBuilt = [];

    static CadDoc CadStarter()
    {
        var d = new CadDoc { Params = { ["Breite"] = 60, ["Tiefe"] = 40, ["Dicke"] = 8 } };
        d.Bodies[0].Name = "Halter";
        d.Bodies[0].Features.Add(CadDoc.Feature(CadOp.Quader, ("breite", "Breite"), ("tiefe", "Tiefe"), ("hoehe", "Dicke")));
        d.Bodies[0].Features.Add(CadDoc.Feature(CadOp.Bohrung, ("durchmesser", "5"), ("x", "Breite/4"), ("y", "Tiefe/2")));
        d.Bodies[0].Features.Add(CadDoc.Feature(CadOp.Bohrung, ("durchmesser", "5"), ("x", "Breite*3/4"), ("y", "Tiefe/2")));
        d.Bodies[0].Features.Add(CadDoc.Feature(CadOp.Rundung, ("radius", "4")));
        return d;
    }

    private void InitCad()
    {
        _showMap["cad"] = CadPageView;
        PageModules["cad"] = "CORE-05";
        ExtraPages.Add(("Trendo CAD", "cad", ["cad", "freecad", "konstruieren", "bauteil", "technische zeichnung", "step", "3mf", "stl", "parametrisch", "3d-druck"]));
    }

    CadBody CurBody => Cad.Bodies[Math.Clamp(CadBodyIndex, 0, Cad.Bodies.Count - 1)];
    public CadFeature? CadSelected => CadFeatureIndex >= 0 && CadFeatureIndex < CurBody.Features.Count ? CurBody.Features[CadFeatureIndex] : null;

    // ---------- Öffentliche Wege (auch für Tests und die KI) ----------
    public void CadNew() { Cad = new CadDoc(); CadPath = null; CadDirty = false; _cadUndo.Clear(); _cadRedo.Clear(); CadBodyIndex = 0; CadFeatureIndex = -1; _cadMsg = "Neues Bauteil – mit „Quader“ oder einer Skizze beginnen."; }

    public CadFeature CadAdd(CadOp op)
    {
        CadSnapshot();
        var f = CadDoc.Feature(op, CadDoc.Fields(op).Select(x => (x.Key, x.Default)).ToArray());
        if (op is CadOp.Extrusion) f.Sketch = new CadSketch { Kind = "rechteck", W = "30", H = "20" };
        if (op is CadOp.Drehteil) f.Sketch = new CadSketch { Kind = "vieleck", Points = ["0;0", "15;0", "15;5", "8;5", "8;25", "0;25"] };
        // Bohrung und Tasche mittig auf den bisherigen Körper setzen
        if (op is CadOp.Bohrung && Built(CurBody) is { Polys.Count: > 0 } s) { var (mn, mx) = s.Bounds(); f["x"] = Num((mn.X + mx.X) / 2); f["y"] = Num((mn.Y + mx.Y) / 2); }
        CurBody.Features.Insert(CadFeatureIndex < 0 ? CurBody.Features.Count : CadFeatureIndex + 1, f);
        CadFeatureIndex = CurBody.Features.IndexOf(f);
        CadChanged(); _cadMsg = $"„{f.Name}“ hinzugefügt – Maße rechts anpassen.";
        return f;
    }

    static string Num(double v) => Math.Round(v, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);

    Solid? Built(CadBody b) => _cadBuilt.FirstOrDefault(x => ReferenceEquals(x.Body, b)).Solid;

    public void CadSet(string key, string value) { if (CadSelected is not { } f) return; CadSnapshot(); f[key] = value; CadChanged(); }
    public void CadSetParam(string name, double value) { CadSnapshot(); Cad.Params[name] = value; CadChanged(); _cadMsg = $"{name} = {value} – alles neu berechnet."; }
    public void CadSelect(int body, int feature) { CadBodyIndex = Math.Clamp(body, 0, Cad.Bodies.Count - 1); CadFeatureIndex = feature; }

    public void CadDeleteFeature() { if (CadSelected is null) return; CadSnapshot(); CurBody.Features.RemoveAt(CadFeatureIndex); CadFeatureIndex = Math.Min(CadFeatureIndex, CurBody.Features.Count - 1); CadChanged(); }
    public void CadMoveFeature(int d) { var i = CadFeatureIndex; var j = i + d; if (CadSelected is null || j < 0 || j >= CurBody.Features.Count) return; CadSnapshot(); (CurBody.Features[i], CurBody.Features[j]) = (CurBody.Features[j], CurBody.Features[i]); CadFeatureIndex = j; CadChanged(); }

    public CadBody CadAddBody()
    {
        CadSnapshot();
        var colors = new[] { "#2F86F6", "#F59E0B", "#10B981", "#EF4444", "#8B5CF6", "#06B6D4" };
        var (_, mx) = Cad.Build().Where(b => b.Solid.Polys.Count > 0).Select(b => b.Solid.Bounds()).DefaultIfEmpty((new Vec3(0, 0, 0), new Vec3(0, 0, 0))).Aggregate((a, b) => (a.Item1, new Vec3(Math.Max(a.Item2.X, b.Item2.X), 0, 0)));
        var b = new CadBody { Name = $"Teil {Cad.Bodies.Count + 1}", Color = colors[Cad.Bodies.Count % colors.Length], X = Num(mx.X + 10), Features = [CadDoc.Feature(CadOp.Quader, ("breite", "20"), ("tiefe", "20"), ("hoehe", "20"))] };
        Cad.Bodies.Add(b); CadBodyIndex = Cad.Bodies.Count - 1; CadFeatureIndex = 0; CadChanged(); _cadMsg = $"„{b.Name}“ zur Baugruppe hinzugefügt.";
        return b;
    }

    public void CadUndo() { if (_cadUndo.Count == 0) return; _cadRedo.Add(Cad); Cad = _cadUndo[^1]; _cadUndo.RemoveAt(_cadUndo.Count - 1); CadAfterSwap(); }
    public void CadRedo() { if (_cadRedo.Count == 0) return; _cadUndo.Add(Cad); Cad = _cadRedo[^1]; _cadRedo.RemoveAt(_cadRedo.Count - 1); CadAfterSwap(); }
    void CadAfterSwap() { CadBodyIndex = Math.Min(CadBodyIndex, Cad.Bodies.Count - 1); CadFeatureIndex = Math.Min(CadFeatureIndex, CurBody.Features.Count - 1); CadDirty = true; _cadBuilt = Cad.Build(); }
    void CadSnapshot() { _cadUndo.Add(Cad.Clone()); if (_cadUndo.Count > 80) _cadUndo.RemoveAt(0); _cadRedo.Clear(); }
    void CadChanged() { CadDirty = true; _cadBuilt = Cad.Build(); }

    public void CadSave(string path) { Cad.Save(path); CadPath = path; CadDirty = false; _cadMsg = $"Gespeichert: {Path.GetFileName(path)}"; _core.Audit.Append(Principal.LocalOwner.Name, "cad.save", Path.GetFileName(path)); }
    public void CadOpen(string path) { Cad = CadDoc.Load(path); CadPath = path; CadDirty = false; _cadUndo.Clear(); _cadRedo.Clear(); CadBodyIndex = 0; CadFeatureIndex = -1; _cadBuilt = Cad.Build(); _cadMsg = $"Geöffnet: {Path.GetFileName(path)}"; }

    string CadFolder { get { var d = Path.Combine(ProjectsRoot, "3D", "CAD"); Directory.CreateDirectory(d); return d; } }
    string CadName => Path.GetFileNameWithoutExtension(CadPath) is { Length: > 0 } n ? n : CurBody.Name;

    /// <summary>Export: stl, 3mf, step, pdf (Zeichnung). Liefert den Pfad.</summary>
    public string CadExportAs(string kind)
    {
        var parts = Cad.Build().Where(b => b.Solid.Polys.Count > 0).ToList();
        if (parts.Count == 0) throw new CadError("Es gibt noch nichts zu speichern.");
        var f = Mesh3D.Unique(Path.Combine(CadFolder, $"{CadName}.{(kind == "pdf" ? "pdf" : kind)}"));
        switch (kind)
        {
            case "stl": CadExport.WriteStl(parts.Select(p => p.Solid), f); break;
            case "3mf": CadExport.Write3mf(parts.Select(p => (p.Body.Name, p.Body.Color, p.Solid)).ToList(), f); break;
            case "step": CadExport.WriteStep(parts.Select(p => (p.Body.Name, p.Solid)).ToList(), f); break;
            default: CadExport.WriteDrawing(parts.Select(p => (p.Body.Name, p.Solid)).ToList(), CadName, f); break;
        }
        _cadMsg = $"Gespeichert: {Path.GetFileName(f)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "cad.export", Path.GetFileName(f));
        return f;
    }

    void CadExportClick(string kind)
    {
        try { var f = CadExportAs(kind); Toast(_cadMsg, ("Öffnen", new Action(() => Launch(f))), ("Ordner öffnen", new Action(() => Launch(Path.GetDirectoryName(f)!)))); }
        catch (Exception ex) when (ex is CadError or IOException or UnauthorizedAccessException) { _cadMsg = "Speichern ging nicht: " + ex.Message; }
    }

    async Task CadOpenClick()
    {
        var req = new PickRequest("cad-open", false, false, ["tcad"]);
        var f = PickOverride is { } fake ? (await fake(req))?.FirstOrDefault() : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Bauteil öffnen", FileTypeFilter = [new FilePickerFileType("Trendo CAD") { Patterns = ["*.tcad"] }] })).Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
        if (f is null) return;
        try { CadOpen(f); } catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException) { _cadMsg = "Öffnen ging nicht: " + ex.Message; }
        Show("cad");
    }

    // ---------- Oberfläche ----------
    Control CadPageView()
    {
        if (_cadBuilt.Count == 0 && Cad.Bodies.Any(b => b.Features.Count > 0)) _cadBuilt = Cad.Build();
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var head = PageHead("Trendo CAD"); Grid.SetColumnSpan(head, 3); g.Children.Add(head);
        var rib = CadRibbon(); Grid.SetRow(rib, 1); Grid.SetColumnSpan(rib, 3); g.Children.Add(rib);
        var tree = CadTree(); Grid.SetRow(tree, 2); g.Children.Add(tree);
        var view = new CadView { Name = "cad-view", MinHeight = 380, Parts = _cadBuilt.Select(b => (b.Body.Color, b.Solid, ReferenceEquals(b.Body, CurBody))).ToList() };
        CadCanvas = view;
        var stage = new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = view, Margin = new Thickness(8, 0) }; Grid.SetRow(stage, 2); Grid.SetColumn(stage, 1); g.Children.Add(stage);
        var side = CadSide(); Grid.SetRow(side, 2); Grid.SetColumn(side, 2); g.Children.Add(side);
        var errors = _cadBuilt.Where(b => b.Error is not null).Select(b => $"{b.Body.Name}: {b.Error}").ToList();
        var cur = Built(CurBody);
        var info = cur is { Polys.Count: > 0 } s ? $"{CurBody.Name}: {Dim(s)} mm · {s.Volume() / 1000:0.##} cm³ · ca. {s.Volume() / 1000 * 1.24:0.#} g PLA" : CurBody.Name;
        var status = new TextBlock { Name = "cad-status", Foreground = errors.Count > 0 ? Brush.Parse("#F87171") : Muted, FontSize = 12, Margin = new Thickness(4, 6, 0, 0), TextWrapping = TextWrapping.Wrap,
            Text = string.Join(" · ", new[] { CadPath is null ? "Noch nicht gespeichert" : Path.GetFileName(CadPath) + (CadDirty ? " · geändert" : ""), info }.Concat(errors).Append(_cadMsg).Where(x => x.Length > 0)) };
        Grid.SetRow(status, 3); Grid.SetColumnSpan(status, 3); g.Children.Add(status);
        return new Border { Name = "cad-page-root", Background = Brush.Parse(C("#F2081329", "#F7F4F7FC")), CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Child = g };
    }

    static string Dim(Solid s) { var (mn, mx) = s.Bounds(); return $"{mx.X - mn.X:0.##} × {mx.Y - mn.Y:0.##} × {mx.Z - mn.Z:0.##}"; }

    Button CB(string name, string icon, string label, Action go, string tip, bool on = false) => SB(name, icon, label, go, tip, on, "cad");

    Control CadRibbon()
    {
        var file = new Button { Name = "cad-file", Content = "Datei ▾", Focusable = false, Padding = new Thickness(10, 4), Background = Accent, VerticalAlignment = VerticalAlignment.Top };
        var fm = new ContextMenu();
        MenuItem Mi(string name, string header, Action a) { var m = new MenuItem { Name = name, Header = header }; m.Click += (_, _) => { a(); Show("cad"); }; return m; }
        fm.Items.Add(Mi("cad-m-new", "Neues Bauteil", CadNew));
        fm.Items.Add(Mi("cad-m-open", "Öffnen …", () => _ = CadOpenClick()));
        fm.Items.Add(Mi("cad-m-save", "Speichern", () => { try { CadSave(CadPath ?? Mesh3D.Unique(Path.Combine(CadFolder, CadName + ".tcad"))); } catch (IOException ex) { _cadMsg = "Speichern ging nicht: " + ex.Message; } }));
        fm.Items.Add(Mi("cad-m-stl", "Als STL (3D-Druck)", () => CadExportClick("stl")));
        fm.Items.Add(Mi("cad-m-3mf", "Als 3MF (3D-Druck mit Farben, Bambu Studio)", () => CadExportClick("3mf")));
        fm.Items.Add(Mi("cad-m-step", "Als STEP (andere CAD-Programme)", () => CadExportClick("step")));
        fm.Items.Add(Mi("cad-m-pdf", "Technische Zeichnung (PDF)", () => CadExportClick("pdf")));
        file.ContextMenu = fm; file.Click += (_, _) => fm.Open(file);
        var groups = new WrapPanel();
        groups.Children.Add(new Border { Child = file, Margin = new Thickness(0, 0, 8, 0) });
        groups.Children.Add(RibbonGroup("Grundkörper",
            CB("cad-add-quader", "▣", "Quader", () => CadAdd(CadOp.Quader), "Quader hinzufügen"),
            CB("cad-add-zylinder", "◯", "Zylinder", () => CadAdd(CadOp.Zylinder), "Zylinder hinzufügen"),
            CB("cad-add-kegel", "△", "Kegel", () => CadAdd(CadOp.Kegel), "Kegel/Kegelstumpf"),
            CB("cad-add-kugel", "●", "Kugel", () => CadAdd(CadOp.Kugel), "Kugel")));
        groups.Children.Add(RibbonGroup("Skizze",
            CB("cad-add-extrusion", "⬒", "Extrudieren", () => CadAdd(CadOp.Extrusion), "Umriss zeichnen und hochziehen (oder abziehen)"),
            CB("cad-add-drehteil", "◍", "Drehteil", () => CadAdd(CadOp.Drehteil), "Umriss um die Achse drehen (Flasche, Knopf, Rad)")));
        groups.Children.Add(RibbonGroup("Bearbeiten",
            CB("cad-add-bohrung", "⊙", "Bohrung", () => CadAdd(CadOp.Bohrung), "Loch bohren (durchgehend oder mit Tiefe, mit Senkung)"),
            CB("cad-add-tasche", "⊟", "Tasche", () => CadAdd(CadOp.Tasche), "Rechteckige Vertiefung"),
            CB("cad-add-rundung", "◜", "Rundung", () => CadAdd(CadOp.Rundung), "Kanten runden"),
            CB("cad-add-fase", "◸", "Fase", () => CadAdd(CadOp.Fase), "Kanten abschrägen"),
            CB("cad-add-muster", "⋯", "Muster", () => CadAdd(CadOp.Muster), "Mehrfach in Reihe"),
            CB("cad-add-spiegeln", "⇋", "Spiegeln", () => CadAdd(CadOp.Spiegeln), "An einer Ebene spiegeln"),
            CB("cad-add-verschieben", "✥", "Verschieben", () => CadAdd(CadOp.Verschieben), "Körper verschieben"),
            CB("cad-add-drehen", "⟳", "Drehen", () => CadAdd(CadOp.Drehen), "Körper drehen")));
        groups.Children.Add(RibbonGroup("Baugruppe & Ansicht",
            CB("cad-add-body", "＋", "Neues Teil", () => CadAddBody(), "Weiteres Teil der Baugruppe"),
            CB("cad-view-iso", "◇", "Schräg", () => CadCanvas?.SetView("iso"), "Ansicht schräg von oben"),
            CB("cad-view-front", "▭", "Vorne", () => CadCanvas?.SetView("vorne"), "Vorderansicht"),
            CB("cad-view-top", "▦", "Oben", () => CadCanvas?.SetView("oben"), "Draufsicht"),
            CB("cad-undo", "↶", "Zurück", CadUndo, "Rückgängig"),
            CB("cad-redo", "↷", "Vor", CadRedo, "Wiederholen"),
            CB("cad-print", "🖨", "3D-Druck", () => { try { var f = CadExportAs("3mf"); Open3dPrint(f); } catch (CadError ex) { _cadMsg = ex.Message; } }, "Als 3MF speichern und zum 3D-Druck (Bambu Lab) weitergeben")));
        return groups;
    }

    /// <summary>Bauplan: Teile mit ihren Schritten. Klick wählt, ▲▼ sortiert, ⏻ schaltet aus, ✕ löscht.</summary>
    Control CadTree()
    {
        var sp = new StackPanel { Name = "cad-tree", Width = 230, Spacing = 4 };
        for (var bi = 0; bi < Cad.Bodies.Count; bi++)
        {
            var body = Cad.Bodies[bi]; var bIdx = bi;
            var err = _cadBuilt.FirstOrDefault(x => ReferenceEquals(x.Body, body)).Error;
            var head = new Button { Name = $"cad-body-{bi}", Focusable = false, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(6, 4),
                Background = bi == CadBodyIndex ? Brush.Parse("#332F86F6") : Brushes.Transparent,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Background = Brush.Parse(body.Color) }, new TextBlock { Text = body.Name, Foreground = Text, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = err is null ? "" : "⚠", Foreground = Brush.Parse("#F87171") } } } };
            head.Click += (_, _) => { CadSelect(bIdx, -1); Show("cad"); };
            sp.Children.Add(head);
            for (var fi = 0; fi < body.Features.Count; fi++)
            {
                var f = body.Features[fi]; var fIdx = fi;
                var b = new Button { Name = $"cad-feature-{bi}-{fi}", Focusable = false, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(20, 3, 6, 3),
                    Background = bi == CadBodyIndex && fi == CadFeatureIndex ? Accent : Brushes.Transparent, Opacity = f.Off ? 0.45 : 1,
                    Content = new TextBlock { Text = $"{fi + 1}. {(f.Name.Length > 0 ? f.Name : f.Op.ToString())}", Foreground = Text, FontSize = 13 } };
                b.Click += (_, _) => { CadSelect(bIdx, fIdx); Show("cad"); };
                sp.Children.Add(b);
            }
        }
        var tools = new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = {
            CB("cad-up", "▲", "Hoch", () => CadMoveFeature(-1), "Schritt nach oben"),
            CB("cad-down", "▼", "Runter", () => CadMoveFeature(1), "Schritt nach unten"),
            CB("cad-toggle", "⏻", "Ein/Aus", () => { if (CadSelected is { } f) { CadSnapshot(); f.Off = !f.Off; CadChanged(); } }, "Schritt vorübergehend ausschalten"),
            CB("cad-delete", "✕", "Löschen", CadDeleteFeature, "Schritt löschen") } };
        sp.Children.Add(tools);
        return new ScrollViewer { Content = sp };
    }

    /// <summary>G4: Maße des gewählten Schritts (Formeln erlaubt), Skizze, Teil-Lage und Farbe, Parameter.</summary>
    Control CadSide()
    {
        var sp = new StackPanel { Name = "cad-side", Width = 270, Spacing = 6 };
        TextBox Field(string name, string label, string value, Action<string> set)
        {
            var tb = new TextBox { Name = name, Text = value, Width = 120 };
            tb.LostFocus += (_, _) => { if ((tb.Text ?? "") != value) { set(tb.Text ?? ""); Show("cad"); } };
            tb.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && (tb.Text ?? "") != value) { set(tb.Text ?? ""); Show("cad"); } };
            sp.Children.Add(new DockPanel { Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Width = 140 }, tb } });
            return tb;
        }
        if (CadSelected is { } f)
        {
            sp.Children.Add(new TextBlock { Text = $"Schritt: {f.Name}", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
            foreach (var (key, label, def) in CadDoc.Fields(f.Op)) { var k = key; Field($"cad-p-{key}", label, f.P.GetValueOrDefault(key, def), v => CadSet(k, v)); }
            if (f.Sketch is { } sk)
            {
                sp.Children.Add(new TextBlock { Text = "Skizze", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
                var kind = new ComboBox { Name = "cad-sketch-kind", ItemsSource = new[] { "rechteck", "kreis", "vieleck" }, SelectedItem = sk.Kind, Width = 140 };
                kind.SelectionChanged += (_, _) => { if (kind.SelectedItem is string k && k != sk.Kind) { CadSnapshot(); sk.Kind = k; if (k == "vieleck" && sk.Points.Count == 0) sk.Points = ["0;0", "30;0", "15;20"]; CadChanged(); Show("cad"); } };
                sp.Children.Add(kind);
                void SkSet(Action a) { CadSnapshot(); a(); CadChanged(); }
                if (sk.Kind == "vieleck")
                {
                    var pts = new TextBox { Name = "cad-sketch-points", Text = string.Join("\n", sk.Points), AcceptsReturn = true, Height = 110, Width = 250 };
                    pts.LostFocus += (_, _) => { var list = (pts.Text ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); if (!list.SequenceEqual(sk.Points)) { SkSet(() => sk.Points = list); Show("cad"); } };
                    sp.Children.Add(new TextBlock { Text = "Punkte „x;y“, je Zeile einer (Formeln erlaubt):", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                    sp.Children.Add(pts);
                }
                else
                {
                    Field("cad-sk-x", sk.Kind == "kreis" ? "Mitte X" : "Ecke X", sk.X, v => SkSet(() => sk.X = v));
                    Field("cad-sk-y", sk.Kind == "kreis" ? "Mitte Y" : "Ecke Y", sk.Y, v => SkSet(() => sk.Y = v));
                    if (sk.Kind == "kreis") Field("cad-sk-r", "Radius", sk.R, v => SkSet(() => sk.R = v));
                    else { Field("cad-sk-w", "Breite", sk.W, v => SkSet(() => sk.W = v)); Field("cad-sk-h", "Höhe", sk.H, v => SkSet(() => sk.H = v)); }
                }
            }
        }
        else
        {
            var b = CurBody;
            sp.Children.Add(new TextBlock { Text = $"Teil: {b.Name}", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
            Field("cad-body-name", "Name", b.Name, v => { CadSnapshot(); b.Name = v.Trim().Length > 0 ? v.Trim() : b.Name; CadChanged(); });
            Field("cad-body-color", "Farbe (#RRGGBB)", b.Color, v => { if (Color.TryParse(v, out _)) { CadSnapshot(); b.Color = v; CadChanged(); } });
            Field("cad-body-x", "Lage X", b.X, v => { CadSnapshot(); b.X = v; CadChanged(); });
            Field("cad-body-y", "Lage Y", b.Y, v => { CadSnapshot(); b.Y = v; CadChanged(); });
            Field("cad-body-z", "Lage Z", b.Z, v => { CadSnapshot(); b.Z = v; CadChanged(); });
        }
        // Parameter
        sp.Children.Add(new TextBlock { Text = "Parameter", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        sp.Children.Add(new TextBlock { Text = "Benannte Maße – in jedem Feld als Formel nutzbar, z. B. „Breite/2“.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        foreach (var (name, value) in Cad.Params.ToList())
        {
            var n = name;
            Field($"cad-param-{name}", name, value.ToString(System.Globalization.CultureInfo.GetCultureInfo("de-DE")), v => { if (double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) CadSetParam(n, d); else _cadMsg = $"„{v}“ ist keine Zahl."; });
        }
        var newName = new TextBox { Name = "cad-param-new", PlaceholderText = "Name = Wert, z. B. Höhe = 25", Width = 250 };
        newName.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Enter) return;
            var parts = (newName.Text ?? "").Split('=');
            if (parts.Length == 2 && parts[0].Trim().Length > 0 && double.TryParse(parts[1].Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) CadSetParam(parts[0].Trim(), d);
            else _cadMsg = "Bitte als „Name = Wert“ eingeben.";
            Show("cad");
        };
        sp.Children.Add(newName);
        sp.Children.Add(new TextBlock { Text = "So geht's: Mit einem Grundkörper beginnen, dann Bohrungen, Taschen und Rundungen hinzufügen. Jeder Schritt bleibt änderbar – Maße rechts ändern, alles rechnet neu. Ziehen dreht die Ansicht, Mausrad zoomt.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        return new ScrollViewer { Content = sp };
    }
}
