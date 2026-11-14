using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Cad;

namespace Trendo.App;

/// <summary>3D-Druck (Masterprompt 131): Modell auf der Druckplatte, automatisch aufstellen, Schichten-Vorschau, Platten- und Überhang-Prüfung,
/// Filament und Zeit als Schätzung. Die genauen Bahnen rechnet weiterhin Bambu Studio.</summary>
public partial class MainWindow
{
    private Mesh? _prMesh; private string _prOrient = ""; private int _prLayer = -1; private PrintPrep.Report? _prReport; private string? _prMeshFile;
    public Mesh? PrintMesh => _prMesh;
    public PrintPrep.Report? PrintReport => _prReport;
    public int PrintLayer { get => _prLayer; set => _prLayer = value; }
    /// <summary>Slicer-Einstellungen wie in PrusaSlicer/Cura: Schichthöhe, Wände, Füllung, Material.</summary>
    public Slicer.Settings PrintSettings { get; private set; } = new();
    /// <summary>Schätzung aus den eigenen Bahnen (Zeit, Filament, Gewicht, Kosten, Stützen).</summary>
    public Slicer.Result? PrintEstimate { get; private set; }
    Mesh? _prSingle; int _prCopies = 1; double _prScale = 100;

    /// <summary>Einstellungen ändern und neu schätzen.</summary>
    public void PrintSetSettings(Slicer.Settings st)
    {
        PrintSettings = st;
        if (_prMesh is { } m) { PrintEstimate = Slicer.Estimate(m, st); _prLayer = Math.Clamp(_prLayer, 1, PrintEstimate.Layers); }
        Show("3d-druck");
    }

    /// <summary>Gleichmäßig skalieren (Prozent vom geladenen Modell).</summary>
    public void PrintScale(double percent)
    {
        if (_prSingle is null) return;
        var baseMesh = Slicer.Scale(_prSingle, percent / _prScale * 100); _prScale = percent;
        PrintSet(baseMesh, _prOrient); _prMsg = $"Auf {percent:0.#} % skaliert."; Show("3d-druck");
    }

    /// <summary>Kopien auf der Platte anordnen (wie „Instanzen“ + „Anordnen“ in PrusaSlicer).</summary>
    public void PrintCopies(int count)
    {
        if (_prSingle is null) return;
        var arranged = Slicer.Copies(_prSingle, count, PrintPrep.BambuA1); var single = _prSingle;
        _prCopies = count; PrintSet(arranged, _prOrient, keepSingle: true); _prSingle = single;
        _prMsg = count == 1 ? "Eine Kopie auf der Platte." : $"{count} Kopien angeordnet."; Show("3d-druck");
    }

    /// <summary>Datei als Netz laden (STL, OBJ, 3MF, GLB) und auf die Platte legen; andere Formate (z. B. STEP) bleiben ohne Vorschau.</summary>
    void PrintLoadMesh()
    {
        if (PrintFile is not { } f || f == _prMeshFile) return;
        _prMeshFile = f; _prMesh = null; _prReport = null; _prLayer = -1; _prOrient = ""; _prSingle = null; _prScale = 100; PrintEstimate = null;
        if (Path.GetExtension(f).ToLowerInvariant() is not (".stl" or ".obj" or ".3mf" or ".glb" or ".gltf")) return;
        try { PrintSet(PrintPrep.Load(f), "wie geladen"); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or InvalidDataException or System.Xml.XmlException or FormatException) { _prMsg = "Vorschau nicht möglich: " + ex.Message; }
    }

    void PrintSet(Mesh m, string orient, bool keepSingle = false)
    {
        if (!keepSingle) { _prSingle = m; _prCopies = 1; }
        _prMesh = keepSingle ? m : PrintPrep.Place(m, PrintPrep.BambuA1); _prOrient = orient;
        PrintEstimate = Slicer.Estimate(_prMesh, PrintSettings);
        _prReport = PrintPrep.Analyze(_prMesh, PrintPrep.BambuA1);
        _prLayer = Math.Max(1, PrintEstimate.Layers / 2); // neue Lage: Schichtansicht in der Mitte
    }

    public void PrintAutoOrient() { if (_prMesh is null) return; var (m, l) = PrintPrep.AutoOrient(_prMesh); PrintSet(m, "automatisch: " + l); _prMsg = $"Aufgestellt ({l}) – Überhang jetzt {_prReport!.OverhangCm2:0.#} cm²."; Show("3d-druck"); }
    public void PrintRotate(char axis) { if (_prMesh is null) return; PrintSet(PrintPrep.Rotate(_prMesh, axis, 1), $"um {char.ToUpperInvariant(axis)} gedreht"); Show("3d-druck"); }

    /// <summary>Druckplatte in 3D – auch leer (wie in jedem Slicer), mit Modell sobald eine Datei geladen ist.</summary>
    Control PrintPlate()
    {
        PrintLoadMesh();
        var plate = PrintPrep.BambuA1;
        var plateSolid = new Solid();
        if (Poly.TryCreate([new Vec3(0, 0, 0), new Vec3(plate.W, 0, 0), new Vec3(plate.W, plate.D, 0), new Vec3(0, plate.D, 0)]) is { } pp) plateSolid.Polys.Add(pp);
        if (_prMesh is not { } m || _prReport is not { } r)
        {
            var empty = new CadView { Name = "print-3d", Parts = [("#3A3F47", plateSolid, false)], Frame = (new Vec3(plate.W / 2, plate.D / 2, 20), 300) };
            return new Grid { Children = { empty, new Border { Name = "print-empty", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Background = Brush.Parse("#CC0B1424"), CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 12),
                Child = new StackPanel { Spacing = 6, Children = { new TextBlock { Name = "print-empty-title", Text = PrintFile is { } pf ? $"{Path.GetFileName(pf)}: keine Vorschau für dieses Format" : "Leere Druckplatte · " + plate.Name, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                    P(PrintFile is not null ? "STEP und ähnliche Formate zeigt Bambu Studio – links „Slicen in Bambu Studio“. Für die Vorschau hier: STL, 3MF, OBJ oder GLB." : "Oben „Datei wählen …“ (STL, 3MF, OBJ, GLB) oder „Aus Trendo CAD“ – das Modell erscheint hier auf der Platte.", Muted, 12) } } } } };
        }
        var shown = m.Triangles > 30000 ? Trendo.Core.Arch.Plan3D.Simplify(m, Math.Max(r.SizeX, Math.Max(r.SizeY, r.SizeZ)) / 120) : m;
        var solid = new Solid { Smooth = true };
        for (var t = 0; t + 2 < shown.I.Count; t += 3) if (Poly.TryCreate([shown.V[shown.I[t]], shown.V[shown.I[t + 1]], shown.V[shown.I[t + 2]]]) is { } p) solid.Polys.Add(p);
        return new CadView { Name = "print-3d", Parts = [("#3A3F47", plateSolid, false), ("#F59E0B", solid, false)], Frame = (new Vec3(plate.W / 2, plate.D / 2, r.SizeZ / 2), Math.Max(180, Math.Max(r.SizeX, r.SizeY) * 1.6)) };
    }

    Control? PrintPreviewCard()
    {
        PrintLoadMesh();
        if (_prMesh is not { } m || _prReport is not { } r) return null;
        var plate = PrintPrep.BambuA1;
        // 3D: Platte + Modell
        var shown = m.Triangles > 30000 ? Trendo.Core.Arch.Plan3D.Simplify(m, Math.Max(r.SizeX, Math.Max(r.SizeY, r.SizeZ)) / 120) : m;
        var solid = new Solid { Smooth = true };
        for (var t = 0; t + 2 < shown.I.Count; t += 3) if (Poly.TryCreate([shown.V[shown.I[t]], shown.V[shown.I[t + 1]], shown.V[shown.I[t + 2]]]) is { } p) solid.Polys.Add(p);
        var plateSolid = new Solid();
        foreach (var q in new[] { new[] { new Vec3(0, 0, 0), new Vec3(plate.W, 0, 0), new Vec3(plate.W, plate.D, 0), new Vec3(0, plate.D, 0) } }) if (Poly.TryCreate([.. q]) is { } pp) plateSolid.Polys.Add(pp);
        _ = solid; _ = plateSolid; // 3D-Platte zeigt jetzt die Mitte der Arbeitsfläche (PrintPlate)
        // Schichten
        var st = PrintSettings; var est = PrintEstimate ?? Slicer.Estimate(m, st);
        var z = Math.Min(r.SizeZ - 0.01, (_prLayer - 0.5) * st.Layer);
        // eigene Bahnen wie in der Slicer-Vorschau: Umrisse (Wände) und Füllung
        var (loops, fill) = Slicer.Layer(m, _prLayer, est.Layers, st);
        var prev = _prLayer > 1 ? PrintPrep.Slice(m, z - st.Layer) : [];
        var layer = new ShapeView((w, h) =>
        {
            var k = Math.Min(w / plate.W, h / plate.D) * 0.94; var ox = (w - plate.W * k) / 2; var oy = (h - plate.D * k) / 2;
            var list = new List<Trendo.Core.Arch.Shape> { new Trendo.Core.Arch.DPoly([(ox, oy), (ox + plate.W * k, oy), (ox + plate.W * k, oy + plate.D * k), (ox, oy + plate.D * k)], "#1F2937", "#475569", 1, "platte") };
            foreach (var s in prev) list.Add(new Trendo.Core.Arch.DLine(ox + s.X1 * k, oy + (plate.D - s.Y1) * k, ox + s.X2 * k, oy + (plate.D - s.Y2) * k, "#334155", 1, "vorher"));
            foreach (var s in fill) list.Add(new Trendo.Core.Arch.DLine(ox + s.X1 * k, oy + (plate.D - s.Y1) * k, ox + s.X2 * k, oy + (plate.D - s.Y2) * k, "#F59E0B", 0.8, "fuellung"));
            foreach (var lp in loops)
                for (var i = 0; i < lp.Count; i++) { var a = lp[i]; var b = lp[(i + 1) % lp.Count]; list.Add(new Trendo.Core.Arch.DLine(ox + a.X * k, oy + (plate.D - a.Y) * k, ox + b.X * k, oy + (plate.D - b.Y) * k, "#22D3EE", 1.6, "wand")); }
            return list;
        }, "#0B1424") { Name = "print-layer-view", Height = 220 };
        var slider = new Slider { Name = "print-layer", Minimum = 1, Maximum = Math.Max(1, est.Layers), Value = _prLayer, IsSnapToTickEnabled = true, TickFrequency = 1, Width = 300 };
        slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty && (int)slider.Value != _prLayer) { _prLayer = (int)slider.Value; Show("3d-druck"); } };
        var warn = new StackPanel { Name = "print-warnings", Spacing = 2 };
        if (!r.Fits) warn.Children.Add(P($"⚠ Passt nicht auf die Platte ({plate.W:0} × {plate.D:0} × {plate.H:0} mm) – verkleinern oder anders aufstellen.", Brush.Parse("#F87171"), 12));
        if (r.OverhangCm2 > 0.5) warn.Children.Add(P($"⚠ {r.OverhangCm2:0.#} cm² hängen steiler als 45° über – Stützen einschalten (ca. {est.SupportGrams:0.#} g) oder „Automatisch aufstellen“.", Brush.Parse("#F5A524"), 12));
        if (warn.Children.Count == 0) warn.Children.Add(P("✓ Passt auf die Platte, kein nennenswerter Überhang.", Brush.Parse("#2FBF71"), 12));
        Button B(string name, string label, Action a, bool primary = false) { var b = new Button { Name = name, Content = label, Margin = new Thickness(0, 0, 6, 0) }; if (primary) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => a(); return b; }
        var facts = P($"{r.SizeX:0.#} × {r.SizeY:0.#} × {r.SizeZ:0.#} mm · {est.Layers} Schichten à {st.Layer:0.00} mm · {r.VolumeCm3:0.#} cm³ · ca. {est.Grams:0.#} g {st.Mat.Name} ({est.FilamentM:0.#} m) · ca. {est.Euro:0.00} € · ca. {(int)est.Time.TotalHours} h {est.Time.Minutes:00} min"
            + (est.SupportGrams > 0.5 ? $" · davon Stützen ca. {est.SupportGrams:0.#} g" : "") + $" · Düse {st.Mat.Nozzle} °C, Bett {st.Mat.Bed} °C (Schätzung aus den Bahnen – genau rechnet Bambu Studio)", Text, 12.5);
        facts.Name = "print-facts";
        slider.Width = 250;
        // Druckeinstellungen wie im Slicer
        var mat = new ComboBox { Name = "print-material", ItemsSource = Slicer.Materials.Select(x => $"{x.Name} · {x.EuroPerKg:0} €/kg").ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(Slicer.Materials, x => x.Id == st.Material)), MinWidth = 150 };
        mat.SelectionChanged += (_, _) => { var id = Slicer.Materials[Math.Max(0, mat.SelectedIndex)].Id; if (id != PrintSettings.Material) PrintSetSettings(PrintSettings with { Material = id }); };
        var lh = new ComboBox { Name = "print-layer-height", ItemsSource = Slicer.LayerHeights.Select(x => $"{x:0.00} mm").ToList(), SelectedIndex = Math.Max(0, Array.IndexOf(Slicer.LayerHeights, st.Layer)), MinWidth = 100 };
        lh.SelectionChanged += (_, _) => { var v = Slicer.LayerHeights[Math.Max(0, lh.SelectedIndex)]; if (v != PrintSettings.Layer) PrintSetSettings(PrintSettings with { Layer = v }); };
        var infill = new Slider { Name = "print-infill", Minimum = 0, Maximum = 100, Value = st.Infill, Width = 160, TickFrequency = 5, IsSnapToTickEnabled = true };
        infill.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty && (int)infill.Value != PrintSettings.Infill) PrintSetSettings(PrintSettings with { Infill = (int)infill.Value }); };
        var walls = new ComboBox { Name = "print-walls", ItemsSource = Enumerable.Range(1, 5).Select(x => $"{x} Wände").ToList(), SelectedIndex = Math.Clamp(st.Walls - 1, 0, 4), MinWidth = 100 };
        walls.SelectionChanged += (_, _) => { var v = Math.Max(0, walls.SelectedIndex) + 1; if (v != PrintSettings.Walls) PrintSetSettings(PrintSettings with { Walls = v }); };
        var scale = new TextBox { Name = "print-scale", Text = _prScale.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture), Width = 70 };
        var copies = new TextBox { Name = "print-copies", Text = _prCopies.ToString(), Width = 50 };
        Button Apply(string name, string label, Action<string> a, TextBox box) { var b = new Button { Name = name, Content = label, Margin = new Thickness(4, 0, 10, 0) }; b.Click += (_, _) => { try { a(box.Text ?? ""); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException) { _prMsg = ex.Message; Show("3d-druck"); } }; return b; }
        var settings = new StackPanel { Name = "print-settings", Spacing = 6, Children = {
            new WrapPanel { Children = { mat, new Border { Width = 6 }, lh, new Border { Width = 6 }, walls } },
            new WrapPanel { Children = { new TextBlock { Text = $"Füllung {st.Infill} %", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Width = 90 }, infill } },
            new WrapPanel { Children = { new TextBlock { Text = "Größe %", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, scale,
                Apply("print-scale-apply", "Skalieren", t => PrintScale(double.Parse(t.Replace('.', ','), System.Globalization.CultureInfo.GetCultureInfo("de-DE"))), scale),
                new TextBlock { Text = "Kopien", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, copies,
                Apply("print-arrange", "Anordnen", t => PrintCopies(int.Parse(t)), copies) } } } };
        return new StackPanel { Name = "print-preview", Spacing = 8, Children = {
            H("Schichten", 14), layer,
            new StackPanel { Children = { slider, new TextBlock { Name = "print-layer-label", Text = $"Schicht {_prLayer} / {est.Layers} · Höhe {z + st.Layer / 2:0.0} mm · {loops.Count} Umrisse, {fill.Count} Füllbahnen", Foreground = Text, FontSize = 12 } } },
            H("Druckeinstellungen", 14), settings,
            H("Modell", 14), facts, warn,
            new WrapPanel { Children = { B("print-orient", "Automatisch aufstellen", PrintAutoOrient, true), B("print-rot-x", "Um X drehen", () => PrintRotate('x')), B("print-rot-y", "Um Y drehen", () => PrintRotate('y')) } },
            new TextBlock { Text = "Lage: " + _prOrient, Foreground = Muted, FontSize = 12 } } };
    }
}
