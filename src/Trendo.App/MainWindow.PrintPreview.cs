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

    /// <summary>Datei als Netz laden (STL, OBJ, 3MF, GLB) und auf die Platte legen; andere Formate (z. B. STEP) bleiben ohne Vorschau.</summary>
    void PrintLoadMesh()
    {
        if (PrintFile is not { } f || f == _prMeshFile) return;
        _prMeshFile = f; _prMesh = null; _prReport = null; _prLayer = -1; _prOrient = "";
        if (Path.GetExtension(f).ToLowerInvariant() is not (".stl" or ".obj" or ".3mf" or ".glb" or ".gltf")) return;
        try { PrintSet(PrintPrep.Load(f), "wie geladen"); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or InvalidDataException or System.Xml.XmlException or FormatException) { _prMsg = "Vorschau nicht möglich: " + ex.Message; }
    }

    void PrintSet(Mesh m, string orient)
    {
        _prMesh = PrintPrep.Place(m, PrintPrep.BambuA1); _prOrient = orient;
        _prReport = PrintPrep.Analyze(_prMesh, PrintPrep.BambuA1);
        _prLayer = Math.Max(1, _prReport.Layers / 2); // neue Lage: Schichtansicht in der Mitte
    }

    public void PrintAutoOrient() { if (_prMesh is null) return; var (m, l) = PrintPrep.AutoOrient(_prMesh); PrintSet(m, "automatisch: " + l); _prMsg = $"Aufgestellt ({l}) – Überhang jetzt {_prReport!.OverhangCm2:0.#} cm²."; Show("3d-druck"); }
    public void PrintRotate(char axis) { if (_prMesh is null) return; PrintSet(PrintPrep.Rotate(_prMesh, axis, 1), $"um {char.ToUpperInvariant(axis)} gedreht"); Show("3d-druck"); }

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
        var view = new CadView { Name = "print-3d", Parts = [("#3A3F47", plateSolid, false), ("#F59E0B", solid, false)], Height = 300, Frame = (new Vec3(plate.W / 2, plate.D / 2, r.SizeZ / 2), Math.Max(180, Math.Max(r.SizeX, r.SizeY) * 1.6)) };
        // Schichten
        var z = Math.Min(r.SizeZ - 0.01, (_prLayer - 0.5) * PrintPrep.LayerHeight);
        var segs = PrintPrep.Slice(m, z); var prev = _prLayer > 1 ? PrintPrep.Slice(m, z - PrintPrep.LayerHeight) : [];
        var layer = new ShapeView((w, h) =>
        {
            var k = Math.Min(w / plate.W, h / plate.D) * 0.94; var ox = (w - plate.W * k) / 2; var oy = (h - plate.D * k) / 2;
            var list = new List<Trendo.Core.Arch.Shape> { new Trendo.Core.Arch.DPoly([(ox, oy), (ox + plate.W * k, oy), (ox + plate.W * k, oy + plate.D * k), (ox, oy + plate.D * k)], "#1F2937", "#475569", 1, "platte") };
            foreach (var s in prev) list.Add(new Trendo.Core.Arch.DLine(ox + s.X1 * k, oy + (plate.D - s.Y1) * k, ox + s.X2 * k, oy + (plate.D - s.Y2) * k, "#334155", 1, "vorher"));
            foreach (var s in segs) list.Add(new Trendo.Core.Arch.DLine(ox + s.X1 * k, oy + (plate.D - s.Y1) * k, ox + s.X2 * k, oy + (plate.D - s.Y2) * k, "#22D3EE", 1.6, "schicht"));
            return list;
        }, "#0B1424") { Name = "print-layer-view", Height = 300 };
        var slider = new Slider { Name = "print-layer", Minimum = 1, Maximum = Math.Max(1, r.Layers), Value = _prLayer, IsSnapToTickEnabled = true, TickFrequency = 1, Width = 300 };
        slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty && (int)slider.Value != _prLayer) { _prLayer = (int)slider.Value; Show("3d-druck"); } };
        var warn = new StackPanel { Name = "print-warnings", Spacing = 2 };
        if (!r.Fits) warn.Children.Add(P($"⚠ Passt nicht auf die Platte ({plate.W:0} × {plate.D:0} × {plate.H:0} mm) – verkleinern oder anders aufstellen.", Brush.Parse("#F87171"), 12));
        if (r.OverhangCm2 > 0.5) warn.Children.Add(P($"⚠ {r.OverhangCm2:0.#} cm² hängen steiler als 45° über – Stützen einschalten oder „Automatisch aufstellen“.", Brush.Parse("#F5A524"), 12));
        if (warn.Children.Count == 0) warn.Children.Add(P("✓ Passt auf die Platte, kein nennenswerter Überhang.", Brush.Parse("#2FBF71"), 12));
        Button B(string name, string label, Action a, bool primary = false) { var b = new Button { Name = name, Content = label, Margin = new Thickness(0, 0, 6, 0) }; if (primary) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => a(); return b; }
        var facts = P($"{r.SizeX:0.#} × {r.SizeY:0.#} × {r.SizeZ:0.#} mm · {r.Layers} Schichten à {PrintPrep.LayerHeight:0.0} mm · {r.VolumeCm3:0.#} cm³ · ca. {r.FilamentG:0} g PLA ({r.FilamentM:0.#} m) · ca. {(int)r.Time.TotalHours} h {r.Time.Minutes:00} min (grobe Schätzung – genau rechnet Bambu Studio)", Text, 12.5);
        facts.Name = "print-facts";
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), Children = { view, Pos(new StackPanel { Spacing = 4, Children = { layer, new WrapPanel { Children = { new TextBlock { Text = "Schicht ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, slider, new TextBlock { Name = "print-layer-label", Text = $" {_prLayer} / {r.Layers} · Höhe {z + PrintPrep.LayerHeight / 2:0.0} mm", Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } } } }, 2) } };
        return new StackPanel { Name = "print-preview", Spacing = 8, Children = {
            H("Vorschau auf der Druckplatte · " + PrintPrep.BambuA1.Name, 15), grid, facts, warn,
            new WrapPanel { Children = { B("print-orient", "Automatisch aufstellen", PrintAutoOrient, true), B("print-rot-x", "Um X drehen", () => PrintRotate('x')), B("print-rot-y", "Um Y drehen", () => PrintRotate('y')),
                new TextBlock { Text = "Lage: " + _prOrient, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } } } };
    }
}
