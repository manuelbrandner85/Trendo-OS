using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Cad;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0691: Trendo CAD liest fremde Dateien – DXF als bearbeitbare 2D-Zeichnung (Ebenen bleiben), STL und facettiertes STEP als
/// Referenzkörper – und schreibt die Zeichnung als DXF. Dazu Wiederherstellung nach Absturz, Tastenkürzel, Rechtsklick und Ziehen und Ablegen.</summary>
public partial class MainWindow
{
    public static readonly string[] CadImportPatterns = ["*.dxf", "*.stl", "*.step", "*.stp"];

    /// <summary>Fremde Datei ins aktuelle Bauteil holen (ein Rückgängig-Schritt). Rückgabe: Meldung.</summary>
    public string CadImport(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant(); var name = Path.GetFileNameWithoutExtension(path);
        if (!File.Exists(path)) throw new CadError("Die Datei gibt es nicht.");
        switch (ext)
        {
            case ".dxf":
            {
                var rd = CadDxf.Read(path); var (d, skipped) = rd;
                CadSnapshot();
                if (Cad.Drawing is null) Cad.Drawing = d;
                else
                {
                    foreach (var l in d.Layers) { var mine = Cad.Drawing.Layer(l.Name); mine.Color = l.Color; mine.Visible = l.Visible; }
                    Cad.Drawing.Entities.AddRange(d.Entities);
                }
                CadChanged();
                var skip = skipped.Count == 0 ? "" : " Nicht übernommen: " + string.Join(", ", skipped.Select(s => $"{s.Value}× {s.Key}")) + ".";
                var unit = rd.Units == 4 ? "" : rd.Units == 0 ? " Einheit nicht angegeben – als mm gelesen." : $" Maße in mm umgerechnet aus {rd.UnitName} (×{rd.MmPerUnit.ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))})."; // T-0747-fix2: Umrechnung sichtbar
                _cadMsg = $"DXF „{name}“ eingelesen: {CadDxf.Summary(d)}.{unit}{skip}";
                break;
            }
            case ".stl":
            {
                var m = Mesh3D.ReadMeshFile(path); // STL in Millimetern – wie Trendo CAD
                CadSnapshot(); Cad.References.Add(CadReference.FromMesh(name, Path.GetFileName(path), m)); CadChanged();
                _cadMsg = $"STL „{name}“ als Referenzkörper eingelesen ({m.Triangles:N0} Dreiecke) – sichtbar und messbar, nicht Teil des Bauplans.";
                break;
            }
            case ".step" or ".stp":
            {
                var m = CadStepRead.ReadFacetted(path);
                CadSnapshot(); Cad.References.Add(CadReference.FromMesh(name, Path.GetFileName(path), m)); CadChanged();
                _cadMsg = $"STEP „{name}“ als Referenzkörper eingelesen ({m.Triangles:N0} Dreiecke, facettiert).";
                break;
            }
            default: throw new CadError("Trendo CAD liest DXF, STL und STEP (facettiert). Eigene Bauteile: „Öffnen …“ (.tcad).");
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "cad.import", Path.GetFileName(path));
        return _cadMsg;
    }

    void CadImportSafe(string path)
    {
        try { CadImport(path); }
        catch (Exception ex) when (ex is CadError or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or FormatException)
        { _cadMsg = $"„{Path.GetFileName(path)}“ ließ sich nicht einlesen: " + Trendo.Core.FriendlyError.Say(ex); }
    }

    async Task CadImportClick()
    {
        var req = new PickRequest("cad-import", false, false, ["dxf", "stl", "step", "stp"]);
        var f = PickOverride is { } fake ? (await fake(req))?.FirstOrDefault()
            : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Zeichnung oder Körper einlesen", FileTypeFilter = [new FilePickerFileType("DXF, STL, STEP") { Patterns = CadImportPatterns }] })).Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
        if (f is null) return;
        CadImportSafe(f);
        Show("cad");
    }

    /// <summary>Zeichnung als DXF; dazu die Körperkanten von oben (Ebene „Umriss“), damit auch ein reines 3D-Bauteil eine nutzbare DXF ergibt.</summary>
    public string CadExportDxf()
    {
        var parts = Cad.Build().Where(b => b.Solid.Polys.Count > 0).ToList();
        var outline = new List<(string, double, double, double, double)>();
        foreach (var (_, solid, _) in parts)
        {
            var seen = new HashSet<(long, long, long, long)>();
            foreach (var (a, b) in CadExport.FeatureEdges(solid))
            {
                if (Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6) continue; // senkrechte Kanten fallen von oben auf einen Punkt
                var k1 = ((long)Math.Round(a.X * 1000), (long)Math.Round(a.Y * 1000)); var k2 = ((long)Math.Round(b.X * 1000), (long)Math.Round(b.Y * 1000));
                var key = k1.CompareTo(k2) < 0 ? (k1.Item1, k1.Item2, k2.Item1, k2.Item2) : (k2.Item1, k2.Item2, k1.Item1, k1.Item2);
                if (seen.Add(key)) outline.Add(("Umriss", a.X, a.Y, b.X, b.Y));
            }
        }
        if ((Cad.Drawing is null || Cad.Drawing.Entities.Count == 0) && outline.Count == 0) throw new CadError("Es gibt noch nichts zu speichern.");
        var f = Mesh3D.Unique(Path.Combine(CadFolder, $"{CadName}.dxf"));
        AtomicFile.WriteAllText(f, CadDxf.Write(Cad.Drawing ?? new CadDrawing(), outline));
        _cadMsg = $"Gespeichert: {Path.GetFileName(f)} (DXF, mm)";
        _core.Audit.Append(Principal.LocalOwner.Name, "cad.export", Path.GetFileName(f));
        return f;
    }

    /// <summary>Geschlossene Konturen einer Ebene (Polylinien, Kreise) als Extrusion in ein neues Teil übernehmen – so wird aus der Zeichnung ein Körper.</summary>
    public CadBody? CadExtrudeLayer(string layer, double height)
    {
        if (Cad.Drawing is not { } d) { _cadMsg = "Es ist keine Zeichnung geladen."; return null; }
        var shapes = d.Entities.Where(e => e.Layer.Equals(layer, StringComparison.OrdinalIgnoreCase) && e.IsClosedContour).ToList();
        if (shapes.Count == 0) { _cadMsg = $"Auf der Ebene „{layer}“ gibt es keine geschlossene Kontur (geschlossene Polylinie oder Kreis)."; return null; }
        if (height <= 0) { _cadMsg = "Die Höhe muss größer als 0 sein."; return null; }
        CadSnapshot();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var body = new CadBody { Name = layer, Color = "#F59E0B" };
        // größte Kontur hinzufügen, die anderen (innenliegenden) abziehen – wie Bohrungen in einer Platte
        var ordered = shapes.OrderByDescending(e => Area(e.Outline(48))).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var e = ordered[i];
            var f = CadDoc.Feature(CadOp.Extrusion, ("hoehe", Num(height)), ("z", "0"), ("modus", i == 0 ? "hinzufügen" : "abziehen"));
            f.Name = $"{(i == 0 ? "Kontur" : "Ausschnitt")} aus „{layer}“";
            f.Sketch = e.Kind == "kreis"
                ? new CadSketch { Kind = "kreis", X = Num(e.Cx), Y = Num(e.Cy), R = Num(e.R) }
                : new CadSketch { Kind = "vieleck", Points = e.Outline(48).Select(q => q.X.ToString("0.####", inv) + ";" + q.Y.ToString("0.####", inv)).ToList() };
            if (i > 0) { f["hoehe"] = Num(height + 2); f["z"] = "-1"; } // sicher ganz durch
            body.Features.Add(f);
        }
        Cad.Bodies.Add(body); CadBodyIndex = Cad.Bodies.Count - 1; CadFeatureIndex = 0; CadChanged();
        _cadMsg = $"Aus der Ebene „{layer}“ entstand das Teil „{body.Name}“ ({ordered.Count} Konturen, {Num(height)} mm hoch) – jede Kontur bleibt als Schritt änderbar.";
        return body;
        static double Area(List<(double X, double Y)> p) { var a = 0.0; for (var i = 0; i < p.Count; i++) { var (x1, y1) = p[i]; var (x2, y2) = p[(i + 1) % p.Count]; a += x1 * y2 - x2 * y1; } return Math.Abs(a / 2); }
    }

    public void CadLayerVisible(string layer, bool on)
    {
        if (Cad.Drawing?.Layers.FirstOrDefault(l => l.Name == layer) is not { } l || l.Visible == on) return;
        CadSnapshot(); l.Visible = on; CadChanged();
    }

    public void CadLayerDelete(string layer)
    {
        if (Cad.Drawing is not { } d || d.Layers.All(l => l.Name != layer)) return;
        CadSnapshot(); d.Entities.RemoveAll(e => e.Layer == layer); d.Layers.RemoveAll(l => l.Name == layer);
        if (d.Entities.Count == 0) Cad.Drawing = null;
        CadChanged(); _cadMsg = $"Ebene „{layer}“ gelöscht.";
    }

    public void CadReferenceDelete(int index)
    {
        if (index < 0 || index >= Cad.References.Count) return;
        CadSnapshot(); var n = Cad.References[index].Name; Cad.References.RemoveAt(index); CadChanged(); _cadMsg = $"Referenzkörper „{n}“ entfernt.";
    }

    /// <summary>Nach einem Absturz: gesichertes Bauteil laden (nicht als gespeichert markiert – bitte speichern).</summary>
    void CadRestore(string file)
    {
        Cad = CadDoc.Load(file); CadPath = null; CadDirty = true; _cadUndo.Clear(); _cadRedo.Clear(); CadBodyIndex = 0; CadFeatureIndex = -1; _cadBuilt = Cad.Build();
        _cadMsg = "Wiederhergestellt – bitte speichern.";
    }

    static string AciHex(int aci) => aci switch { 1 => "#EF4444", 2 => "#FACC15", 3 => "#22C55E", 4 => "#22D3EE", 5 => "#60A5FA", 6 => "#E879F9", 8 => "#9CA3AF", 9 => "#D1D5DB", _ => "#F3F4F6" };

    /// <summary>Zeichnung und Referenzkörper in die Ansicht geben.</summary>
    void CadDrawingToView(CadView view)
    {
        if (Cad.Drawing is { } d)
        {
            var wires = new List<(Vec3, Vec3, string)>(); var labels = new List<(Vec3, string, string, double)>();
            foreach (var e in d.Entities)
            {
                var layer = d.Layers.FirstOrDefault(l => l.Name == e.Layer); if (layer is { Visible: false }) continue;
                var col = AciHex(layer?.Color ?? 7);
                if (e.Kind == "text") { if (e.Points.Count > 0) labels.Add((new Vec3(e.Points[0][0], e.Points[0][1], 0), e.Text.Replace('\n', ' '), col, e.Height)); continue; }
                var pts = e.Outline(48); var closed = e.Kind == "kreis" || e.Closed;
                for (var i = 0; i + 1 < pts.Count + (closed ? 1 : 0); i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; wires.Add((new Vec3(a.X, a.Y, 0), new Vec3(b.X, b.Y, 0), col)); }
            }
            view.Wires = wires; view.Labels = labels;
        }
        if (Cad.References.Any(r => r.Visible))
            view.Parts = view.Parts.Concat(Cad.References.Where(r => r.Visible).Select(r => (r.Color, r.ToSolid(), false))).ToList();
    }

    /// <summary>Seitenleiste: Ebenen der Zeichnung und Referenzkörper.</summary>
    Control? CadDrawingPanel()
    {
        if (Cad.Drawing is null && Cad.References.Count == 0) return null;
        var sp = new StackPanel { Name = "cad-drawing-panel", Spacing = 4, Margin = new Thickness(0, 10, 0, 0) };
        if (Cad.Drawing is { } d)
        {
            sp.Children.Add(new TextBlock { Text = "Zeichnung (DXF)", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold });
            sp.Children.Add(new TextBlock { Name = "cad-drawing-summary", Text = CadDxf.Summary(d), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var height = new TextBox { Name = "cad-layer-height", Text = "6", Width = 60 };
            sp.Children.Add(new DockPanel { Children = { new TextBlock { Text = "Höhe beim Extrudieren (mm)", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Width = 170 }, height } });
            for (var i = 0; i < d.Layers.Count; i++)
            {
                var l = d.Layers[i]; var count = d.Entities.Count(e => e.Layer == l.Name); if (count == 0) continue;
                var name = l.Name;
                var vis = new CheckBox { Name = $"cad-layer-vis-{i}", IsChecked = l.Visible, Content = new TextBlock { Text = $"{name} ({count})", Foreground = Brush.Parse(AciHex(l.Color)) } };
                vis.IsCheckedChanged += (_, _) => { CadLayerVisible(name, vis.IsChecked == true); Show("cad"); };
                var hasContour = d.Entities.Any(e => e.Layer == name && e.IsClosedContour);
                var ext = CB($"cad-layer-extrude-{i}", "⬒", "Körper", () => { var h = double.TryParse((height.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0; CadExtrudeLayer(name, h); }, "Geschlossene Konturen dieser Ebene als Körper hochziehen");
                ext.IsEnabled = hasContour;
                var del = CB($"cad-layer-del-{i}", "✕", "Löschen", () => CadLayerDelete(name), "Ebene mit allen Elementen löschen");
                sp.Children.Add(new WrapPanel { Children = { vis, ext, del } });
            }
        }
        for (var i = 0; i < Cad.References.Count; i++)
        {
            if (i == 0) sp.Children.Add(new TextBlock { Text = "Referenzkörper", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
            var r = Cad.References[i]; var idx = i; var (mn, mx) = r.ToSolid().Bounds();
            sp.Children.Add(new WrapPanel { Children = {
                new TextBlock { Text = $"{r.Name}: {mx.X - mn.X:0.#} × {mx.Y - mn.Y:0.#} × {mx.Z - mn.Z:0.#} mm", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 190 },
                CB($"cad-ref-del-{i}", "✕", "Entfernen", () => CadReferenceDelete(idx), "Referenzkörper entfernen") } });
        }
        return sp;
    }

    /// <summary>Tastenkürzel, Rechtsklick und Ziehen und Ablegen für die CAD-Ansicht.</summary>
    void CadWireInput(CadView view, Control dropTarget)
    {
        view.KeyDown += (_, e) =>
        {
            var m = e.KeyModifiers;
            if (e.Key == Key.Z && m == KeyModifiers.Control) CadUndo();
            else if (e.Key == Key.Y && m == KeyModifiers.Control || e.Key == Key.Z && m == (KeyModifiers.Control | KeyModifiers.Shift)) CadRedo();
            else if (e.Key == Key.S && m == KeyModifiers.Control) CadSaveHere();
            else if (e.Key == Key.Delete && m == KeyModifiers.None && CadSelected is not null) CadDeleteFeature();
            else return;
            e.Handled = true; Show("cad");
        };
        var menu = new ContextMenu { Name = "cad-context" };
        void Item(string name, string header, string? key, Action a, bool enabled = true)
        {
            var mi = new MenuItem { Name = name, Header = header, InputGesture = key is null ? null : KeyGesture.Parse(key), IsEnabled = enabled };
            mi.Click += (_, _) => { a(); Show("cad"); }; menu.Items.Add(mi);
        }
        Item("cad-ctx-undo", "Rückgängig", "Ctrl+Z", CadUndo, _cadUndo.Count > 0);
        Item("cad-ctx-redo", "Wiederholen", "Ctrl+Y", CadRedo, _cadRedo.Count > 0);
        menu.Items.Add(new Separator());
        Item("cad-ctx-delete", "Schritt löschen", "Delete", CadDeleteFeature, CadSelected is not null);
        Item("cad-ctx-body", "Neues Teil", null, () => CadAddBody());
        Item("cad-ctx-import", "Einlesen (DXF, STL, STEP) …", null, () => _ = CadImportClick());
        menu.Items.Add(new Separator());
        Item("cad-ctx-iso", "Ansicht schräg", null, () => view.SetView("iso"));
        Item("cad-ctx-top", "Ansicht von oben", null, () => view.SetView("oben"));
        view.ContextMenu = menu;
        DragDrop.SetAllowDrop(dropTarget, true);
        dropTarget.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DragDropEffects.Copy; e.Handled = true; });
        dropTarget.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
            foreach (var f in files)
            {
                if (Path.GetExtension(f).Equals(".tcad", StringComparison.OrdinalIgnoreCase)) { try { CadOpen(f); } catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException) { _cadMsg = "Öffnen ging nicht: " + Trendo.Core.FriendlyError.Say(ex); } }
                else CadImportSafe(f);
            }
            e.Handled = true; Show("cad");
        });
    }

    void CadSaveHere()
    {
        try { CadSave(CadPath ?? Mesh3D.Unique(Path.Combine(CadFolder, CadName + ".tcad"))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _cadMsg = "Speichern ging nicht: " + Trendo.Core.FriendlyError.Say(ex); }
    }
}
