using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Cad;

namespace Trendo.App;

/// <summary>CORE-05 (Masterprompt 131): 3D-Arbeitsfläche von „3D-Modelle erstellen“ – Teile anklicken, ausgewähltes Teil mit der Maus verschieben
/// (Bodenebene, 0,5-cm-Raster; bei waagrechtem Blick hoch/runter), Ansicht drehen und zoomen, Formen mit einem Klick einsetzen, Tastenkürzel.
/// Gleiche Szene und gleiches Rückgängig wie die Eingabefelder.</summary>
public partial class MainWindow
{
    private readonly Dictionary<string, Solid> _partSolids = [];
    private CadView? _viewport3d;
    private double _vpYaw = -35, _vpPitch = 28, _vpZoom = 1;

    /// <summary>Teil als Körper für die Arbeitsfläche (zwischengespeichert, solange sich das Teil nicht ändert).</summary>
    private Solid PartSolid(Part3D p)
    {
        var key = System.Text.Json.JsonSerializer.Serialize(p);
        if (_partSolids.TryGetValue(key, out var s)) return s;
        var m = Mesh3D.BuildWorld(p); s = new Solid { Smooth = p.Shape is "sphere" or "torus" || p.Ops.Any(o => o.Kind == "smooth") };
        for (var t = 0; t + 2 < m.I.Count; t += 3)
            if (Poly.TryCreate([m.V[m.I[t]], m.V[m.I[t + 1]], m.V[m.I[t + 2]]]) is { } poly) s.Polys.Add(poly);
        if (_partSolids.Count > 200) _partSolids.Clear();
        return _partSolids[key] = s;
    }

    public IReadOnlyList<Part3D> Studio3DParts => _scene.Parts;
    public string? Studio3DSelected => _selId;

    public void Studio3DSelect(int index) { _selId = index >= 0 && index < _scene.Parts.Count ? _scene.Parts[index].Id : null; Show("3d"); }

    /// <summary>Ausgewähltes Teil verschieben (cm) – ein Schritt im Rückgängig.</summary>
    public void Studio3DMove(int index, Vec3 by)
    {
        if (index < 0 || index >= _scene.Parts.Count || by.Len < 1e-9) return;
        Push(); var p = _scene.Parts[index];
        p.X = Math.Round(p.X + by.X, 2); p.Y = Math.Round(p.Y + by.Y, 2); p.Z = Math.Round(p.Z + by.Z, 2);
        _selId = p.Id;
        Say($"{p.Name} verschoben auf X {Part3D.Num(p.X)} · Y {Part3D.Num(p.Y)} · Z {Part3D.Num(p.Z)} cm.", "3d");
    }

    /// <summary>Form einsetzen: neben die vorhandenen Teile, auf den Boden gestellt, ausgewählt.</summary>
    public void Studio3DAddShape(string shape)
    {
        if (!Scene3D.PartShapes.Any(s => s.Id == shape)) return;
        var right = _scene.Parts.Count == 0 ? -10 : _scene.Parts.Max(q => Bounds3D(q).Max.X);
        var p = new Part3D { Shape = shape, Name = Scene3D.ShapeLabel(shape), Text = shape == "text" ? "Trendo" : "", X = Math.Round(right + 10, 1), Color = Scene3D.Colors[_scene.Parts.Count % Scene3D.Colors.Length].Hex };
        if (shape == "plane") p.Sz = 1;
        if (shape == "text") { p.Sx = 30; p.Sz = 6; p.Sy = 2; }
        p.Z = Math.Round(p.Z - Bounds3D(p).Min.Z, 2); // auf den Boden stellen
        Push(); _scene.Parts.Add(p); _selId = p.Id;
        Say($"{p.Name} ist dabei – ziehen zum Verschieben, Werte rechts.", "3d");
    }

    public void Studio3DDuplicate()
    {
        if (Selected is not { } s) { Say("Wähle zuerst ein Teil.", "3d"); return; }
        var c = System.Text.Json.JsonSerializer.Deserialize<Part3D>(System.Text.Json.JsonSerializer.Serialize(s))!; c.Id = Scene3D.NewId(); c.Name = s.Name + " (Kopie)"; c.X = Math.Round(s.X + Math.Max(5, Bounds3D(s).Max.X - Bounds3D(s).Min.X + 2), 2);
        Push(); _scene.Parts.Add(c); _selId = c.Id; Say($"{c.Name} angelegt.", "3d");
    }

    public void Studio3DDeleteSelected()
    {
        if (Selected is not { } s) { Say("Wähle zuerst ein Teil.", "3d"); return; }
        Push(); _scene.Parts.Remove(s); _selId = _scene.Parts.LastOrDefault()?.Id; Say($"{s.Name} entfernt.", "3d");
    }

    /// <summary>Ausgewähltes Teil auf den Boden stellen (Unterkante z = 0).</summary>
    public void Studio3DDrop()
    {
        if (Selected is not { } s) { Say("Wähle zuerst ein Teil.", "3d"); return; }
        var dz = -Bounds3D(s).Min.Z; if (Math.Abs(dz) < 1e-6) { Say($"{s.Name} steht schon auf dem Boden.", "3d"); return; }
        Push(); s.Z = Math.Round(s.Z + dz, 2); Say($"{s.Name} steht jetzt auf dem Boden.", "3d");
    }

    (Vec3 Min, Vec3 Max) Bounds3D(Part3D p) => PartSolid(p) is { Polys.Count: > 0 } s ? s.Bounds() : (new(p.X, p.Y, p.Z), new(p.X, p.Y, p.Z));

    private Control Viewport3D()
    {
        var parts = _scene.Parts.Select(p => (p.Color, PartSolid(p), p.Id == _selId)).ToList();
        var vp = new CadView { Name = "3d-viewport", Parts = parts, Yaw = _vpYaw, Pitch = _vpPitch, Zoom = _vpZoom, MinHeight = 320, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        _viewport3d = vp;
        if (parts.Count > 0)
        {
            var bs = parts.Select(x => x.Item2.Bounds()).ToList();
            Vec3 lo = new(bs.Min(b => b.Min.X), bs.Min(b => b.Min.Y), Math.Min(0, bs.Min(b => b.Min.Z))), hi = new(bs.Max(b => b.Max.X), bs.Max(b => b.Max.Y), bs.Max(b => b.Max.Z));
            vp.Frame = ((lo + hi) * 0.5, Math.Max(25, (hi - lo).Len * 0.75));
        }
        vp.PartClicked = i => { SaveCam(vp); _selId = i >= 0 ? _scene.Parts[i].Id : null; Show("3d"); };
        vp.PartMoved = (i, by) => { SaveCam(vp); Studio3DMove(i, by); };
        vp.PointerWheelChanged += (_, _) => SaveCam(vp);
        vp.PointerReleased += (_, _) => SaveCam(vp);
        vp.KeyDown += (_, e) =>
        {
            var idx = _scene.Parts.FindIndex(p => p.Id == _selId);
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
            Vec3? by = e.Key switch { Key.Left => new(-step, 0, 0), Key.Right => new(step, 0, 0), Key.Up => new(0, step, 0), Key.Down => new(0, -step, 0), Key.PageUp => new(0, 0, step), Key.PageDown => new(0, 0, -step), _ => null };
            if (by is { } b && idx >= 0) { SaveCam(vp); Studio3DMove(idx, b); e.Handled = true; }
            else if (e.Key == Key.Delete && idx >= 0) { SaveCam(vp); Studio3DDeleteSelected(); e.Handled = true; }
            else if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control) && idx >= 0) { SaveCam(vp); Studio3DDuplicate(); e.Handled = true; }
            else if (e.Key == Key.Escape) { _selId = null; Show("3d"); e.Handled = true; }
        };

        Button Tool(string name, string label, string tip, Action go)
        {
            var b = Act(name, label, () => { SaveCam(vp); go(); }); b.Padding = new Thickness(8, 3); b.FontSize = 12; b.Margin = new Thickness(0, 0, 4, 4); ToolTip.SetTip(b, tip); return b;
        }
        var add = new WrapPanel { Name = "3d-shapes" };
        add.Children.Add(new TextBlock { Text = "Einsetzen", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 4) });
        foreach (var (id, label) in Scene3D.PartShapes) add.Children.Add(Tool("3d-new-" + id, label, $"{label} neben die vorhandenen Teile setzen", () => Studio3DAddShape(id)));
        var edit = new WrapPanel { Name = "3d-vp-tools" };
        edit.Children.Add(Tool("3d-dup", "Verdoppeln", "Ausgewähltes Teil kopieren (Strg+D)", Studio3DDuplicate));
        edit.Children.Add(Tool("3d-drop-floor", "Auf den Boden", "Unterkante des ausgewählten Teils auf den Boden setzen", Studio3DDrop));
        edit.Children.Add(Tool("3d-remove", "Löschen", "Ausgewähltes Teil löschen (Entf)", Studio3DDeleteSelected));
        foreach (var (id, label, view) in new[] { ("angle", "Schräg", "iso"), ("front", "Vorne", "vorne"), ("top", "Oben", "oben"), ("right", "Rechts", "rechts") })
        {
            var b = Tool("3d-view-" + id, label, "Ansicht " + label.ToLowerInvariant(), () => { _view3d = id; vp.SetView(view); SaveCam(vp); Say("Ansicht " + label.ToLowerInvariant() + ".", "3d"); });
            if (_view3d == id) { b.Background = Accent; b.Foreground = Brushes.White; }
            edit.Children.Add(b);
        }
        // weitere Ansichten wie bisher (Bild für Export und Blender)
        foreach (var (id, label) in new[] { ("back", "Hinten"), ("left", "Links") }) edit.Children.Add(Tool("3d-view-" + id, label, "Ansicht " + label.ToLowerInvariant(), () => SetView(id, "Ansicht von " + (id == "back" ? "hinten." : "links."))));
        var sel = Selected;
        var info = new TextBlock { Name = "3d-vp-info", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
            Text = sel is null ? (_scene.Parts.Count == 0 ? "Oben eine Form einsetzen. Klick wählt ein Teil, Ziehen am gewählten Teil verschiebt es, Ziehen daneben dreht die Ansicht, Mausrad zoomt." : "Teil anklicken zum Auswählen · Ziehen dreht die Ansicht · Mausrad zoomt · Doppelklick: Ansicht zurücksetzen")
                : $"{sel.Name}: {Part3D.Num(sel.Sx)} × {Part3D.Num(sel.Sy)} × {Part3D.Num(sel.Sz)} cm bei X {Part3D.Num(sel.X)} · Y {Part3D.Num(sel.Y)} · Z {Part3D.Num(sel.Z)} – ziehen verschiebt (0,5-cm-Raster), Pfeiltasten 1 cm (Umschalt 10 cm), Bild↑/↓ Höhe, Entf löscht" };
        var stage = new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = vp };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Children = { add, Pos2(edit, 1), Pos2(stage, 2), Pos2(info, 3) } };
        return grid;
        static Control Pos2(Control c, int row) { Grid.SetRow(c, row); if (row == 3) c.Margin = new Thickness(2, 6, 0, 0); return c; }
    }

    void SaveCam(CadView vp) { _vpYaw = vp.Yaw; _vpPitch = vp.Pitch; _vpZoom = vp.Zoom; }
}
