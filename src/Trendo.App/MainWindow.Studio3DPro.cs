using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Cad;

namespace Trendo.App;

/// <summary>3D-Modelle erstellen wie Profis (Profi-Ausbau 2, 05.11.2026): Arbeitsschritte-Leiste Modellieren → UV → Material → Licht → Kamera → Render,
/// Anfasser (Verschieben/Drehen/Skalieren wie G/R/S in Blender), Szenenbaum mit Gruppen und Ausblenden, PBR-Materialien, Licht- und Kamera-Vorgaben,
/// Render in Blender (Cycles) mit Vorher/Nachher-Vergleich.</summary>
public partial class MainWindow
{
    string _step3d = "modell";
    string _gizmo3d = "move";
    string? _selGroup;
    double _compare3d = 0.5;
    /// <summary>Aktiver Arbeitsschritt (modell, uv, material, licht, kamera, render).</summary>
    public string Studio3DStep { get => _step3d; set => _step3d = Studio3DPro.Steps.Any(s => s.Id == value) ? value : "modell"; }
    /// <summary>Anfasser-Art: move, rotate, scale.</summary>
    public string Studio3DGizmoMode { get => _gizmo3d; set => _gizmo3d = value is "move" or "rotate" or "scale" ? value : "move"; }
    public string? Studio3DGroup => _selGroup;
    /// <summary>Für Tests: die ganze Szene als JSON (Teile, Licht, Kamera).</summary>
    public string Studio3DSceneJson => _scene.ToJson();
    public CadView? Studio3DViewport => _viewport3d;
    /// <summary>Für Tests: statt Blender ein Bild schreiben (Spec-Pfad, Ziel-PNG) → erfolgreich?</summary>
    public Func<string, string, Task<bool>>? Render3DOverride { get; set; }

    List<Part3D> Visible3D => [.. _scene.Parts.Where(p => !p.Hidden)];
    /// <summary>Was die Anfasser bewegen: das ausgewählte Teil oder alle Teile der ausgewählten Gruppe.</summary>
    List<Part3D> Targets3D => _selGroup is { } g ? Studio3DPro.InGroup(_scene, g).Where(p => !p.Hidden).ToList() : Selected is { Hidden: false } s ? [s] : [];

    /// <summary>Anfasser anwenden: Verschieben (cm), Drehen (Grad), Skalieren (Faktor) – ein Schritt im Rückgängig.</summary>
    public void Studio3DGizmo(char axis, string mode, double amount)
    {
        var parts = Targets3D; if (parts.Count == 0) return;
        Push();
        var center = Center3D(parts);
        foreach (var p in parts)
        {
            switch (mode)
            {
                case "move":
                    if (axis == 'x') p.X = Math.Round(p.X + amount, 2); else if (axis == 'y') p.Y = Math.Round(p.Y + amount, 2); else p.Z = Math.Round(p.Z + amount, 2);
                    break;
                case "rotate":
                    static double N(double d) { d %= 360; return d > 180 ? d - 360 : d <= -180 ? d + 360 : d; }
                    if (axis == 'x') p.Rx = N(p.Rx + amount); else if (axis == 'y') p.Ry = N(p.Ry + amount); else p.Rz = N(p.Rz + amount);
                    if (parts.Count > 1 && axis == 'z') // Gruppe dreht um ihre Mitte
                    {
                        var r = amount * Math.PI / 180; var dx = p.X - center.X; var dy = p.Y - center.Y;
                        p.X = Math.Round(center.X + dx * Math.Cos(r) - dy * Math.Sin(r), 2); p.Y = Math.Round(center.Y + dx * Math.Sin(r) + dy * Math.Cos(r), 2);
                    }
                    break;
                case "scale":
                    var f = Math.Clamp(amount, 0.05, 20);
                    if (axis == 'x') p.Sx = Math.Max(0.1, Math.Round(p.Sx * f, 2)); else if (axis == 'y') p.Sy = Math.Max(0.1, Math.Round(p.Sy * f, 2)); else p.Sz = Math.Max(0.1, Math.Round(p.Sz * f, 2));
                    break;
            }
        }
        var what = _selGroup is { } g ? $"Gruppe „{g}“" : parts[0].Name;
        Say(mode switch { "move" => $"{what} um {Part3D.Num(amount)} cm entlang {char.ToUpperInvariant(axis)} verschoben.", "rotate" => $"{what} um {Part3D.Num(amount)}° um {char.ToUpperInvariant(axis)} gedreht.", _ => $"{what} entlang {char.ToUpperInvariant(axis)} × {Part3D.Num(amount)} skaliert." }, "3d");
    }

    /// <summary>Alle Ziele (Teil oder Gruppe) frei verschieben – ein Schritt im Rückgängig.</summary>
    public void Studio3DMoveTargets(Vec3 by)
    {
        var parts = Targets3D; if (parts.Count == 0 || by.Len < 1e-9) return;
        Push();
        foreach (var p in parts) { p.X = Math.Round(p.X + by.X, 2); p.Y = Math.Round(p.Y + by.Y, 2); p.Z = Math.Round(p.Z + by.Z, 2); }
        Say(_selGroup is { } g ? $"Gruppe „{g}“ verschoben." : $"{parts[0].Name} verschoben.", "3d");
    }

    Vec3 Center3D(IEnumerable<Part3D> parts)
    {
        var bs = parts.Select(Bounds3D).ToList(); if (bs.Count == 0) return new(0, 0, 0);
        return new((bs.Min(b => b.Min.X) + bs.Max(b => b.Max.X)) / 2, (bs.Min(b => b.Min.Y) + bs.Max(b => b.Max.Y)) / 2, (bs.Min(b => b.Min.Z) + bs.Max(b => b.Max.Z)) / 2);
    }

    // ───────────── Szenenbaum mit Gruppen ─────────────
    public void Studio3DSetGroup(string? group)
    {
        if (Selected is not { } s) { Say("Wähle zuerst ein Teil.", "3d"); return; }
        group = string.IsNullOrWhiteSpace(group) ? null : group.Trim().Length > 40 ? group.Trim()[..40] : group.Trim();
        Push(); s.Group = group; Say(group is null ? $"{s.Name} ist in keiner Gruppe mehr." : $"{s.Name} gehört jetzt zur Gruppe „{group}“.", "3d");
    }
    public void Studio3DSelectGroup(string? group) { _selGroup = group; if (group is not null) _selId = Studio3DPro.InGroup(_scene, group).FirstOrDefault()?.Id; Show("3d"); }
    public void Studio3DToggleHidden(string partId)
    {
        if (_scene.Parts.FirstOrDefault(p => p.Id == partId) is not { } p) return;
        Push(); p.Hidden = !p.Hidden; if (p.Hidden && _selId == p.Id) _selId = null;
        Say(p.Hidden ? $"{p.Name} ausgeblendet – zählt nicht für Render und Export." : $"{p.Name} wieder sichtbar.", "3d");
    }

    private Control SceneTree(TextBox objName)
    {
        if (_scene.Parts.Count == 0) return P("Noch keine Teile. Oben in der Ansicht eine Form einsetzen.");
        var tree = new StackPanel { Name = "3d-tree", Spacing = 2 };
        Control Row(Part3D part, double indent)
        {
            var id = part.Id; var i = _scene.Parts.IndexOf(part);
            var eye = Act($"3d-eye-{i}", part.Hidden ? "◌" : "👁", () => Studio3DToggleHidden(id)); eye.Padding = new Thickness(5, 1); eye.FontSize = 11; ToolTip.SetTip(eye, part.Hidden ? "Einblenden" : "Ausblenden");
            var sel = Act($"3d-sel-{i}", (part.Id == _selId && _selGroup is null ? "● " : "") + part.Name + (string.IsNullOrEmpty(part.Material) ? "" : " · " + Studio3DPro.MaterialOf(part.Material).Label), () => { Keep(objName); _selGroup = null; _selId = id; Show("3d"); });
            sel.Padding = new Thickness(8, 2); sel.FontSize = 12; sel.Opacity = part.Hidden ? 0.5 : 1; sel.HorizontalContentAlignment = HorizontalAlignment.Left;
            var del = Act($"3d-del-{i}", "✕", () => DeletePart(objName, id)); del.Padding = new Thickness(5, 1); del.FontSize = 11; ToolTip.SetTip(del, "Entfernen");
            return new DockPanel { Margin = new Thickness(indent, 0, 0, 0), Children = { Dk(eye, Avalonia.Controls.Dock.Left), Dk(del, Avalonia.Controls.Dock.Right), sel } };
        }
        foreach (var g in Studio3DPro.Groups(_scene))
        {
            var gg = g; var on = _selGroup == g;
            var head = Act("3d-group-" + Scene3D.Slug(g), (on ? "● " : "") + "▾ " + g + $" ({Studio3DPro.InGroup(_scene, g).Count})", () => Studio3DSelectGroup(on ? null : gg));
            head.FontWeight = FontWeight.SemiBold; head.FontSize = 12.5; head.Padding = new Thickness(8, 2); ToolTip.SetTip(head, "Gruppe auswählen – die Anfasser bewegen dann alle ihre Teile");
            tree.Children.Add(head);
            foreach (var p in Studio3DPro.InGroup(_scene, g)) tree.Children.Add(Row(p, 16));
        }
        foreach (var p in Studio3DPro.InGroup(_scene, "")) tree.Children.Add(Row(p, 0));
        if (Selected is { } s)
        {
            var box = new TextBox { Name = "3d-group-name", Text = s.Group ?? "", PlaceholderText = "Gruppe (z. B. Lampe)", Width = 140 };
            void Commit() { if ((box.Text ?? "").Trim() != (s.Group ?? "")) Studio3DSetGroup(box.Text); }
            OnEnter(box, Commit);
            tree.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { box, new Border { Width = 4 }, Act("3d-group-set", "Zuordnen", Commit) } });
        }
        return tree;
    }

    // ───────────── Arbeitsschritte ─────────────
    private Control StepsBar3D()
    {
        var bar = new WrapPanel { Name = "3d-steps" };
        var prog = Studio3DPro.Progress(_scene, Studio3DPro.Renders(Dir3D()).Count);
        for (var i = 0; i < prog.Count; i++)
        {
            var (step, done, why) = prog[i]; var on = step.Id == _step3d; var id = step.Id;
            var b = new Button { Name = "3d-step-" + step.Id, Content = (done ? "✓ " : $"{i + 1} ") + step.Label, FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 4, 6), CornerRadius = new CornerRadius(14),
                Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : done ? Brush.Parse("#2FBF71") : Text, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1) };
            ToolTip.SetTip(b, step.Hint + " – " + why);
            b.Click += (_, _) => { _step3d = id; Show("3d"); };
            bar.Children.Add(b);
            if (i < prog.Count - 1) bar.Children.Add(new TextBlock { Text = "→", Foreground = Muted, Margin = new Thickness(0, 4, 4, 0) });
        }
        return bar;
    }

    /// <summary>Rechte Spalte je Arbeitsschritt.</summary>
    private Control? StepPanel3D()
    {
        switch (_step3d)
        {
            case "uv":
                return new StackPanel { Spacing = 6, Children = { H("UV-Abwicklung", 14), P("Damit Texturen (Holzmaserung, Stein, Schrift) richtig auf den Teilen liegen, wird jedes Teil abgewickelt. Trendo macht das beim Bauen und Rendern in Blender automatisch (Smart UV, kleine Abstände zwischen den Inseln).", null, 12),
                    P(_scene.Parts.Count > 0 ? $"✓ {Visible3D.Count} Teil(e) werden abgewickelt." : "Erst Teile modellieren.", _scene.Parts.Count > 0 ? Brush.Parse("#2FBF71") : Muted, 12) } };
            case "material": return MaterialPanel3D();
            case "licht": return LightPanel3D();
            case "kamera": return CameraPanel3D();
            case "render": return RenderPanel3D();
            default: return null;
        }
    }

    /// <summary>Material setzen: für das ausgewählte Teil oder die ganze Gruppe (öffentlich für Tests und Claude).</summary>
    public void Studio3DSetMaterial(string materialId)
    {
        var parts = _selGroup is { } g ? Studio3DPro.InGroup(_scene, g) : Selected is { } s ? [s] : [];
        if (parts.Count == 0) { Say("Wähle zuerst ein Teil oder eine Gruppe.", "3d"); return; }
        var m = Studio3DPro.MaterialOf(materialId);
        Push();
        foreach (var p in parts) { p.Material = m.Id; if (m.Tint is { } t) p.Color = t; }
        Say($"{(parts.Count > 1 ? $"{parts.Count} Teile" : parts[0].Name)}: {m.Label}.", "3d");
    }

    private Control MaterialPanel3D()
    {
        var sp = new StackPanel { Spacing = 6, Children = { H("Material (PBR)", 14), P("Wie im Principled-BSDF von Blender: Metall ja/nein, Rauheit von spiegelglatt bis matt, Glas durchsichtig. Die Ansicht zeigt den Glanz sofort.", null, 12) } };
        var target = _selGroup is { } g ? $"Gruppe „{g}“" : Selected?.Name;
        sp.Children.Add(P(target is null ? "Wähle links ein Teil oder eine Gruppe." : "Für: " + target, Text, 12));
        var grid = new WrapPanel { Name = "3d-materials" };
        var cur = Selected?.Material;
        foreach (var m in Studio3DPro.Materials)
        {
            var mm = m; var col = Color.Parse(m.Tint ?? Selected?.Color ?? "#2F86F6");
            var swatch = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Margin = new Thickness(0, 0, 0, 2),
                Background = new RadialGradientBrush { Center = new RelativePoint(0.35, 0.3, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.35, 0.3, RelativeUnit.Relative), RadiusX = new RelativeScalar(0.7, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.7, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Shading.ShadePbr(col, new Vec3(-0.3, -0.5, 0.8).Norm(), new Vec3(0.2, -0.8, 0.5).Norm(), 1, m.Metallic, m.Roughness), 0), new GradientStop(Shading.ShadePbr(col, new Vec3(0.6, -0.3, -0.2).Norm(), new Vec3(0.2, -0.8, 0.5).Norm(), 0.8, m.Metallic, m.Roughness), 1) } },
                Opacity = m.Transmission > 0.5 ? 0.6 : 1 };
            var b = new Button { Name = "3d-mat-" + m.Id, Width = 92, Padding = new Thickness(4), Margin = new Thickness(0, 0, 4, 4), Background = cur == m.Id ? Accent : Brushes.Transparent,
                Content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { swatch, new TextBlock { Text = m.Label, FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Foreground = cur == m.Id ? Brushes.White : Text } } } };
            ToolTip.SetTip(b, $"{m.Label}: Metall {(m.Metallic > 0.5 ? "ja" : "nein")}, Rauheit {Part3D.Num(m.Roughness)}" + (m.Transmission > 0 ? ", durchsichtig" : ""));
            b.Click += (_, _) => Studio3DSetMaterial(mm.Id);
            grid.Children.Add(b);
        }
        sp.Children.Add(grid);
        return sp;
    }

    public void Studio3DSetLight(string id, double power = 1)
    {
        if (!Studio3DPro.Lights.Any(l => l.Id == id)) throw new ArgumentException($"Unbekanntes Licht „{id}“.");
        Push(); _scene.Light = id; _scene.LightPower = Math.Clamp(power, 0.2, 5);
        Say($"Licht: {Studio3DPro.Lights.First(l => l.Id == id).Label}.", "3d");
    }

    private Control LightPanel3D()
    {
        var sp = new StackPanel { Spacing = 6, Children = { H("Licht", 14), P("Jedes Licht hat eine echte Quelle – so wirken Schatten und Glanz glaubwürdig.", null, 12) } };
        foreach (var l in Studio3DPro.Lights)
        {
            var ll = l; var on = _scene.Light == l.Id;
            var b = new Button { Name = "3d-light-" + l.Id, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 0, 4), Background = on ? Accent : Brushes.Transparent,
                Content = new StackPanel { Children = { new TextBlock { Text = (on ? "● " : "") + l.Label, FontWeight = FontWeight.SemiBold, Foreground = on ? Brushes.White : Text }, new TextBlock { Text = l.Hint, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = on ? Brushes.White : Muted } } } };
            b.Click += (_, _) => Studio3DSetLight(ll.Id, _scene.LightPower);
            sp.Children.Add(b);
        }
        var power = new Slider { Name = "3d-light-power", Minimum = 0.2, Maximum = 3, Value = _scene.LightPower, Width = 200 };
        power.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty && _scene.Light is { } id && Math.Abs(power.Value - _scene.LightPower) > 0.05) { _scene.LightPower = Math.Round(power.Value, 2); _pngKey = ""; } };
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Helligkeit", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, power } });
        return sp;
    }

    public void Studio3DSetCamera(double lens, double height)
    {
        if (lens is < 10 or > 300) throw new ArgumentException("Brennweite zwischen 10 und 300 mm.");
        Push(); _scene.CameraLens = lens; _scene.CameraHeight = Math.Clamp(height, 0, 10000);
        Say($"Kamera: {lens:0} mm" + (height > 0 ? $", Blickhöhe {Part3D.Num(height)} cm." : ", Höhe automatisch."), "3d");
    }

    private Control CameraPanel3D()
    {
        var sp = new StackPanel { Spacing = 6, Children = { H("Kamera", 14), P("Wie mit einer echten Vollformat-Kamera: 24–35 mm für Räume und Überblick, 50 mm wie das Auge, 85–135 mm für Produkte ohne Verzerrung.", null, 12) } };
        var row = new WrapPanel { Name = "3d-lenses" };
        foreach (var (mm, hint) in new[] { (24.0, "Weitwinkel"), (35.0, "Reportage"), (50.0, "Normal"), (85.0, "Produkt"), (135.0, "Tele") })
        {
            var on = Math.Abs(_scene.CameraLens - mm) < 0.5; var m = mm;
            var b = Act($"3d-lens-{mm:0}", $"{mm:0} mm", () => Studio3DSetCamera(m, _scene.CameraHeight), on); ToolTip.SetTip(b, hint); b.Margin = new Thickness(0, 0, 4, 4);
            row.Children.Add(b);
        }
        sp.Children.Add(row);
        var h = new TextBox { Name = "3d-cam-height", Text = _scene.CameraHeight > 0 ? Part3D.Num(_scene.CameraHeight) : "", PlaceholderText = "automatisch", Width = 90 };
        void Commit() { var v = Read(h.Text); Studio3DSetCamera(_scene.CameraLens > 0 ? _scene.CameraLens : 50, double.IsNaN(v) ? 0 : v); }
        OnEnter(h, Commit);
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Blickhöhe (cm)", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, h, new Border { Width = 4 }, Act("3d-cam-apply", "Übernehmen", Commit) } });
        return sp;
    }

    // ───────────── Render mit Vorher/Nachher ─────────────
    string Dir3D() => Path.Combine(Folder3D, DevStudio.SafeName(_scene.Name));
    public bool Studio3DRendering { get; private set; }

    /// <summary>In Blender fotorealistisch rendern (Cycles, Entrauschen, AgX); das Bild kommt in den Render-Verlauf des Objekts.</summary>
    public async Task Studio3DRender()
    {
        if (Visible3D.Count == 0) { Say("Noch nichts zu rendern – erst Teile modellieren.", "3d"); return; }
        if (Studio3DRendering) return;
        var outPng = Studio3DPro.NextRenderPath(Dir3D(), DateTime.Now);
        Studio3DRendering = true; Say("Blender rendert im Hintergrund – das dauert je nach Grafikkarte ein bis einige Minuten.", "3d");
        try
        {
            var textures = new Dictionary<string, string>();
            foreach (var id in _scene.Parts.Where(p => !p.Hidden && !string.IsNullOrEmpty(p.Material)).Select(p => Studio3DPro.MaterialOf(p.Material).TextureId).OfType<string>().Distinct())
                try { if (await AmbientTexture(id) is { } dir) textures[id] = dir; }
                catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException) { Say("Die echte Oberfläche ließ sich nicht laden, es wird mit Farbe gerendert: " + Short(e.Message), "3d"); }
            var spec = _scene.WriteBlenderSpec(Path.Combine(Dir3D(), "blender"), textures);
            bool ok;
            if (Render3DOverride is { } fake) ok = await fake(spec, outPng);
            else
            {
                var bl = await ReadyBlender(); if (bl is null) return;
                var (r, _) = await Task.Run(() => Studio3DPro.Render(bl, spec, outPng));
                ok = r.Ok && File.Exists(outPng);
                if (!ok) { Say("Blender konnte nicht rendern: " + Short(r.Err.Length > 0 ? r.Err : r.Out), "3d"); return; }
            }
            if (!ok || !File.Exists(outPng)) { Say("Render fehlgeschlagen.", "3d"); return; }
            _core.Audit.Append(Principal.LocalOwner.Name, "3d.render", Path.GetFileName(outPng));
            _step3d = "render"; _compare3d = 0.5;
            Say("✓ Gerendert: " + Path.GetFileName(outPng) + (Studio3DPro.Renders(Dir3D()).Count > 1 ? " – unten mit dem letzten Bild vergleichen." : "."), "3d");
        }
        finally { Studio3DRendering = false; }
    }

    private Control RenderPanel3D()
    {
        var sp = new StackPanel { Spacing = 6, Children = { H("Render", 14), P("Fotorealistisch in Blender (Cycles, Entrauschen, AgX-Farbumfang) mit deinem Licht und deiner Kamera. Jedes Bild bleibt im Verlauf.", null, 12) } };
        var go = Act("3d-render", Studio3DRendering ? "Rendert …" : "▶ Rendern", () => _ = Studio3DRender(), true); go.IsEnabled = !Studio3DRendering;
        sp.Children.Add(go);
        var renders = Studio3DPro.Renders(Dir3D());
        if (renders.Count == 0) { sp.Children.Add(P("Noch kein Bild.", Muted, 12)); return sp; }
        Bitmap? Load(string f) { try { return new Bitmap(f); } catch { return null; } }
        if (renders.Count == 1)
        {
            sp.Children.Add(new Image { Name = "3d-render-now", Source = Load(renders[0]), Width = 290, Stretch = Stretch.Uniform });
            sp.Children.Add(P(Path.GetFileName(renders[0]), Muted, 11));
            return sp;
        }
        // Vorher/Nachher: links das neue Bild, rechts das vorherige – mit dem Regler verschieben
        const double W = 290; var now = Load(renders[0]); var before = Load(renders[1]);
        var hgt = now is { } n0 ? W * n0.PixelSize.Height / Math.Max(1, n0.PixelSize.Width) : W * 0.75;
        var clip = new Border { Name = "3d-compare-clip", Width = W * _compare3d, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, Child = new Image { Source = now, Width = W, Height = hgt, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left } };
        var line = new Border { Width = 2, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(W * _compare3d - 1, 0, 0, 0) };
        var stage = new Grid { Name = "3d-compare", Width = W, Height = hgt, Children = { new Image { Source = before, Width = W, Height = hgt, Stretch = Stretch.Uniform }, clip, line } };
        var slider = new Slider { Name = "3d-compare-slider", Minimum = 0, Maximum = 1, Value = _compare3d, Width = W };
        slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { _compare3d = slider.Value; clip.Width = W * _compare3d; line.Margin = new Thickness(W * _compare3d - 1, 0, 0, 0); } };
        sp.Children.Add(stage); sp.Children.Add(slider);
        sp.Children.Add(new DockPanel { Children = { Dk(new TextBlock { Text = "vorher: " + Path.GetFileName(renders[1]), Foreground = Muted, FontSize = 11 }, Avalonia.Controls.Dock.Right), new TextBlock { Text = "nachher: " + Path.GetFileName(renders[0]), Foreground = Muted, FontSize = 11 } } });
        return sp;
    }

    /// <summary>Für Tests: aktueller Anteil des neuen Bildes im Vergleich.</summary>
    public double Studio3DCompare => _compare3d;
}
