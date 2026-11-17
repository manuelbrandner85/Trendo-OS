using Avalonia.Platform.Storage;
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
        var key = PartKey(p);
        if (_partSolids.TryGetValue(key, out var s)) return s;
        var m = Mesh3D.BuildWorld(p); s = new Solid { Smooth = p.Shape is "sphere" or "torus" || p.Ops.Any(o => o.Kind == "smooth") };
        for (var t = 0; t + 2 < m.I.Count; t += 3)
            if (Poly.TryCreate([m.V[m.I[t]], m.V[m.I[t + 1]], m.V[m.I[t + 2]]]) is { } poly) s.Polys.Add(poly);
        if (_partSolids.Count > 200) _partSolids.Clear();
        return _partSolids[key] = s;
    }

    /// <summary>T-0727: Schlüssel für <see cref="_partSolids"/> – alle Werte des Teils als JSON, das Netz (Punkte, Dreiecke) aber nur als SHA-256 seines Inhalts.
    /// Vorher lief das ganze Netz bei jedem Aufruf durch JsonSerializer (40 000 Dreiecke ≈ 50 ms, mehrmals je Seitenaufbau); gleicher Inhalt = gleicher Schlüssel wie bisher.</summary>
    internal static string PartKey(Part3D p)
    {
        var head = System.Text.Json.JsonSerializer.Serialize(p, PartKeyJson);
        if (p.Positions is null && p.Indices is null) return head;
        var sb = new System.Text.StringBuilder(head);
        if (p.Positions is { } pos) sb.Append("|v").Append(pos.Length).Append(':').Append(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pos.AsSpan()))));
        if (p.Indices is { } idx) sb.Append("|i").Append(idx.Length).Append(':').Append(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(idx.AsSpan()))));
        return sb.ToString();
    }
    static readonly System.Text.Json.JsonSerializerOptions PartKeyJson = new()
    {
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
        {
            Modifiers = { ti => { if (ti.Type == typeof(Part3D)) foreach (var prop in ti.Properties) if (prop.Name is nameof(Part3D.Positions) or nameof(Part3D.Indices)) prop.ShouldSerialize = (_, _) => false; } }
        }
    };

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
        var visible = Visible3D; // ausgeblendete Teile (Szenenbaum) zeigt die Ansicht nicht
        var inGroup = _selGroup is { } sg ? Studio3DPro.InGroup(_scene, sg).Select(p => p.Id).ToHashSet() : [];
        var parts = visible.Select(p => (p.Color, PartSolid(p), p.Id == _selId || inGroup.Contains(p.Id))).ToList();
        var vp = new CadView { Name = "3d-viewport", Parts = parts, Yaw = _vpYaw, Pitch = _vpPitch, Zoom = _vpZoom, MinHeight = 320, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
            Looks = visible.Select(p => string.IsNullOrEmpty(p.Material) ? (0.0, 0.5, 0.0) : (Studio3DPro.MaterialOf(p.Material).Metallic, Studio3DPro.MaterialOf(p.Material).Roughness, Studio3DPro.MaterialOf(p.Material).Transmission)).ToList() };
        if (Targets3D is { Count: > 0 } tg) { vp.GizmoMode = _gizmo3d; vp.GizmoAt = Center3D(tg); vp.GizmoDragged = (ax, mode, amount) => { SaveCam(vp); Studio3DGizmo(ax, mode, amount); }; }
        _viewport3d = vp;
        if (parts.Count > 0)
        {
            var bs = parts.Select(x => x.Item2.Bounds()).ToList();
            Vec3 lo = new(bs.Min(b => b.Min.X), bs.Min(b => b.Min.Y), Math.Min(0, bs.Min(b => b.Min.Z))), hi = new(bs.Max(b => b.Max.X), bs.Max(b => b.Max.Y), bs.Max(b => b.Max.Z));
            vp.Frame = ((lo + hi) * 0.5, Math.Max(25, (hi - lo).Len * 0.75));
        }
        vp.PartClicked = i => { SaveCam(vp); _selGroup = null; _selId = i >= 0 ? visible[i].Id : null; Show("3d"); };
        vp.PartMoved = (i, by) => { SaveCam(vp); if (_selGroup is null) Studio3DMove(_scene.Parts.IndexOf(visible[i]), by); else Studio3DMoveTargets(by); };
        if (MeshEditing) MeshEditToView(vp); // T-0691: Punkte, Kanten, Flächen statt ganzer Teile
        vp.ContextMenu = Menu3D(vp); // T-0691: Rechtsklick
        vp.PointerWheelChanged += (_, _) => SaveCam(vp);
        vp.PointerReleased += (_, _) => SaveCam(vp);
        vp.KeyDown += (_, e) =>
        {
            // T-0691: Rückgängig/Wiederholen und der Bearbeitungsmodus haben Vorrang
            if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control) { SaveCam(vp); UndoScene(); e.Handled = true; return; }
            if (e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control || e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)) { SaveCam(vp); RedoScene(); e.Handled = true; return; }
            if (MeshEditKey(e)) { SaveCam(vp); e.Handled = true; Show("3d"); return; }
            var idx = _scene.Parts.FindIndex(p => p.Id == _selId);
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
            Vec3? by = e.Key switch { Key.Left => new(-step, 0, 0), Key.Right => new(step, 0, 0), Key.Up => new(0, step, 0), Key.Down => new(0, -step, 0), Key.PageUp => new(0, 0, step), Key.PageDown => new(0, 0, -step), _ => null };
            if (by is { } b && idx >= 0) { SaveCam(vp); Studio3DMove(idx, b); e.Handled = true; }
            else if (e.Key == Key.Delete && idx >= 0) { SaveCam(vp); Studio3DDeleteSelected(); e.Handled = true; }
            else if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control) && idx >= 0) { SaveCam(vp); Studio3DDuplicate(); e.Handled = true; }
            else if (e.Key == Key.Escape) { _selId = null; _selGroup = null; Show("3d"); e.Handled = true; }
            else if (e.Key is Key.G or Key.R or Key.S && e.KeyModifiers == KeyModifiers.None) { SaveCam(vp); _gizmo3d = e.Key == Key.G ? "move" : e.Key == Key.R ? "rotate" : "scale"; Show("3d"); e.Handled = true; } // wie in Blender
        };

        Button Tool(string name, string label, string tip, Action go)
        {
            var b = Act(name, label, () => { SaveCam(vp); go(); }); b.Padding = new Thickness(8, 3); b.FontSize = 12; b.Margin = new Thickness(0, 0, 4, 4); ToolTip.SetTip(b, tip); return b;
        }
        var add = new WrapPanel { Name = "3d-shapes" };
        add.Children.Add(new TextBlock { Text = "Einsetzen", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 4) });
        foreach (var (id, label) in Scene3D.PartShapes) add.Children.Add(Tool("3d-new-" + id, label, $"{label} neben die vorhandenen Teile setzen", () => Studio3DAddShape(id)));
        var edit = new WrapPanel { Name = "3d-vp-tools" };
        // Anfasser-Leiste schwebend in der Ansicht (oben rechts, wie die Werkzeuge in Blender) – kostet keine Höhe
        var gizmoBar = new StackPanel { Name = "3d-gizmo-bar", Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 8, 0) };
        foreach (var (id, icon, label, key) in new[] { ("move", "↔", "Verschieben", "G"), ("rotate", "⟳", "Drehen", "R"), ("scale", "⤢", "Skalieren", "S") })
        {
            var b = Tool("3d-gizmo-" + id, icon, $"Anfasser {label}: an den farbigen Griffen ziehen (Taste {key} wie in Blender)", () => { _gizmo3d = id; Show("3d"); });
            b.MinWidth = 30; b.Margin = new Thickness(0, 0, 3, 0); Avalonia.Automation.AutomationProperties.SetName(b, label);
            b.Background = _gizmo3d == id ? Accent : Brush.Parse("#CC101826"); b.Foreground = Brushes.White;
            gizmoBar.Children.Add(b);
        }
        // T-0691: Objektmodus / Bearbeitungsmodus wie in Blender (Tab)
        var modeObj = Tool("3d-mode-object", "Objekt", "Ganze Teile wählen und verschieben (Tab wechselt)", () => Studio3DEditMode(false));
        var modeEdit = Tool("3d-mode-edit", "Bearbeiten", "Punkte, Kanten und Flächen des gewählten Teils bearbeiten (Tab)", () => Studio3DEditMode(true));
        foreach (var mb in new[] { modeObj, modeEdit }) { mb.Background = Brush.Parse("#CC101826"); mb.Foreground = Brushes.White; mb.Margin = new Thickness(0, 0, 3, 0); }
        (MeshEditing ? modeEdit : modeObj).Background = Accent;
        // schwebend oben links in der Ansicht (wie der Moduswähler in Blender) – kostet keine Höhe
        var modeBar = new StackPanel { Name = "3d-mode-bar", Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 8, 0, 0), Children = { modeObj, modeEdit } };
        edit.Children.Add(Tool("3d-dup", "Verdoppeln", "Ausgewähltes Teil kopieren (Strg+D)", Studio3DDuplicate));
        edit.Children.Add(Tool("3d-drop-floor", "Auf den Boden", "Unterkante des ausgewählten Teils auf den Boden setzen", Studio3DDrop));
        edit.Children.Add(Tool("3d-remove", "Löschen", "Ausgewähltes Teil löschen (Entf)", Studio3DDeleteSelected));
        // T-0704: in „Einfach“ stecken die sechs Ansichten in einem Knopf „Ansicht: … ▾“ (höchstens 40 Knöpfe je Ansicht), in den höheren Stufen stehen sie einzeln da
        Panel views = IsSimple ? new WrapPanel { Name = "3d-view-list", MaxWidth = 280 } : edit;
        var viewLabel = "Schräg";
        foreach (var (id, label, view) in new[] { ("angle", "Schräg", "iso"), ("front", "Vorne", "vorne"), ("top", "Oben", "oben"), ("right", "Rechts", "rechts") })
        {
            var b = Tool("3d-view-" + id, label, "Ansicht " + label.ToLowerInvariant(), () => { _view3d = id; vp.SetView(view); SaveCam(vp); Say("Ansicht " + label.ToLowerInvariant() + ".", "3d"); });
            if (_view3d == id) { b.Background = Accent; b.Foreground = Brushes.White; viewLabel = label; }
            views.Children.Add(b);
        }
        // weitere Ansichten wie bisher (Bild für Export und Blender)
        foreach (var (id, label) in new[] { ("back", "Hinten"), ("left", "Links") }) views.Children.Add(Tool("3d-view-" + id, label, "Ansicht " + label.ToLowerInvariant(), () => SetView(id, "Ansicht von " + (id == "back" ? "hinten." : "links."))));
        if (IsSimple)
        {
            var viewMenu = Tool("3d-view-menu", "Ansicht: " + viewLabel + " ▾", "Blickrichtung wählen: schräg, vorne, oben, rechts, hinten, links", () => { });
            viewMenu.Flyout = new Flyout { Content = views };
            foreach (var vb in views.Children.OfType<Button>()) vb.Click += (_, _) => viewMenu.Flyout?.Hide();
            edit.Children.Add(viewMenu);
        }
        var sel = Selected;
        var info = new TextBlock { Name = "3d-vp-info", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
            Text = MeshEditing ? "Klick wählt, Umschalt erweitert · 1/2/3 Punkte/Kanten/Flächen · E extrudieren · Rechtsklick: Werkzeuge · Tab: zurück"
            : sel is null ? (_scene.Parts.Count == 0 ? "Oben eine Form einsetzen. Klick wählt ein Teil, Ziehen am gewählten Teil verschiebt es, Ziehen daneben dreht die Ansicht, Mausrad zoomt." : "Teil anklicken zum Auswählen · Ziehen dreht die Ansicht · Mausrad zoomt · Doppelklick: Ansicht zurücksetzen")
                : $"{sel.Name}: {Part3D.Num(sel.Sx)} × {Part3D.Num(sel.Sy)} × {Part3D.Num(sel.Sz)} cm bei X {Part3D.Num(sel.X)} · Y {Part3D.Num(sel.Y)} · Z {Part3D.Num(sel.Z)} – ziehen verschiebt (0,5-cm-Raster), Pfeiltasten 1 cm (Umschalt 10 cm), Bild↑/↓ Höhe, Entf löscht" };
        var stage = new Border { Name = "3d-stage-drop", CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = new Grid { Children = { vp, gizmoBar, modeBar } } };
        // T-0691: Datei aus dem Explorer auf die 3D-Fläche ziehen = einlesen (als bearbeitbares Netz)
        DragDrop.SetAllowDrop(stage, true);
        stage.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DragDropEffects.Copy; e.Handled = true; });
        stage.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            SaveCam(vp);
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).ToList() ?? [];
            if (files.Count == 0) { Say("Diese Angabe ist keine Datei.", "3d"); return; }
            foreach (var f in files) _ = ImportPath(f!);
            e.Handled = true;
        });
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Children = { add, Pos2(edit, 1), Pos2(stage, 2), Pos2(info, 3) } };
        return grid;
        static Control Pos2(Control c, int row) { Grid.SetRow(c, row); if (row == 3) c.Margin = new Thickness(2, 6, 0, 0); return c; }
    }

    void SaveCam(CadView vp) { _vpYaw = vp.Yaw; _vpPitch = vp.Pitch; _vpZoom = vp.Zoom; }

    /// <summary>T-0727: so viel 3D-Ansicht bleibt mindestens sichtbar (darunter wird die Seite verschiebbar) – 200 px wie die Abnahme in
    /// W4_Core05_Ui.Preview_and_export_fit („bei üblichen Fenstergrößen ohne Scrollen“, Arbeitsfläche ≥ 200 px). Die Ansicht selbst ist weiter mindestens 320 px hoch
    /// angeordnet wie bisher; ab 200 px sichtbarer Höhe bleibt alles wie vorher.</summary>
    internal const double MinStage3D = 200;

    /// <summary>T-0727: Seite, die die Fensterhöhe füllt – bliebe von <paramref name="stage"/> aber weniger als <paramref name="minVisible"/> sichtbar
    /// wird die Ansicht nicht weiter zusammengedrückt (vorher: 3D-Ansicht im Programmfenster bei 1600 × 900 rund 180 px, bei 1366 × 768 rund 150 px hoch,
    /// Seitenleisten unten abgeschnitten), sondern die Seite lässt sich senkrecht verschieben. Bei genug Höhe ist alles wie vorher.</summary>
    static Control MinFill(Control page, Control stage, double minVisible = MinStage3D)
    {
        var sv = new ScrollViewer { Name = "page-minfill", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        sv.Content = new MinFillHost(sv, stage, minVisible) { Child = page };
        // Klick in die Ansicht fokussiert sie – die Seite soll dabei nicht springen (Maus-Loslassen läge sonst an anderer Stelle der Ansicht)
        stage.AddHandler(RequestBringIntoViewEvent, (_, e) => { if (ReferenceEquals(e.TargetObject, stage)) e.Handled = true; });
        return sv;
    }

    /// <summary>Misst die Seite mit der sichtbaren Höhe; bekommt <c>stage</c> dabei weniger als <c>minVisible</c>, wird die Seite um genau den Fehlbetrag höher.</summary>
    sealed class MinFillHost(ScrollViewer owner, Control stage, double minVisible) : Decorator
    {
        bool _hooked;
        public double Needed { get; private set; }

        protected override Size MeasureOverride(Size available)
        {
            if (Child is not { } c) return default;
            var h = owner.Bounds.Height > 1 ? owner.Bounds.Height : double.IsInfinity(available.Height) ? 900 : available.Height;
            c.Measure(new Size(available.Width, h));
            // die Ansicht meldet höchstens, was sie bekommt – fehlt ihr etwas bis minVisible, wird die Seite um genau so viel höher
            Needed = c.DesiredSize.Height + Math.Max(0, minVisible - stage.DesiredSize.Height);
            if (Needed > h + 0.5) { h = Math.Ceiling(Needed); c.Measure(new Size(available.Width, h)); }
            return new Size(c.DesiredSize.Width, h);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Child?.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (_hooked) return;
            _hooked = true; owner.SizeChanged += (_, _) => InvalidateMeasure(); // neue Fensterhöhe → neu verteilen
        }
    }
}
