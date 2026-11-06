using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Bibliothek;

namespace Trendo.App;

/// <summary>VR & AR gestalten (Profi 3 + Claude 3, 06.11.2026): Vorschau aus Augenhöhe, Texturgrenze je Brille, Reparatur mit einem Klick über Blender.</summary>
public partial class MainWindow
{
    bool _xrEye;
    /// <summary>Vorschau aus Augenhöhe an/aus.</summary>
    public bool XrEye { get => _xrEye; set => _xrEye = value; }
    /// <summary>Für Tests: statt Blender (Eingabe, Ausgabe, Skript) → erfolgreich?</summary>
    public Func<string, string, string, Task<bool>>? XrFixOverride { get; set; }

    /// <summary>Für das gewählte Gerät reparieren: Dreiecke reduzieren, Texturen verkleinern, Maßstab auf Meter – als neue Datei „…-brille.glb“ neben dem Original.</summary>
    public async Task XrFix()
    {
        var (_, info, det, _) = XrLoad();
        if (info is null) { _xrMsg = "⚠ Erst ein Modell öffnen."; Show("xr"); return; }
        var plan = XrPro.Plan(info, det, XrDevice);
        if (!plan.Needed) { _xrMsg = "✓ Nichts zu reparieren – das Modell passt schon auf dieses Gerät."; Show("xr"); return; }
        if (!XrFile.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) && !XrFile.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)) { _xrMsg = "⚠ Reparieren geht mit glTF/GLB – andere Formate erst in der 3D-Bibliothek übernehmen."; Show("xr"); return; }
        var output = Path.Combine(WorkFolder("Modell-Prüfer"), Path.GetFileNameWithoutExtension(XrFile) + "-brille.glb");
        var script = XrPro.FixScript(Path.GetFullPath(XrFile), output, plan);
        _xrMsg = "Blender repariert im Hintergrund …"; Show("xr");
        bool ok;
        if (XrFixOverride is { } fake) ok = await fake(XrFile, output, script);
        else if (ModelConverter.FindBlender() is { } bl && !_opt.Isolated)
        {
            var py = Path.Combine(Path.GetTempPath(), $"trendo-xrfix-{Guid.NewGuid():N}.py"); await File.WriteAllTextAsync(py, script);
            try { var r = await Task.Run(() => Cli.Run(bl, ["--background", "--factory-startup", "--python-exit-code", "1", "--python", py], null, 600_000)); ok = r.Ok && File.Exists(output); if (!ok) { _xrMsg = "⚠ Blender konnte nicht reparieren: " + Short(r.Err.Length > 0 ? r.Err : r.Out); Show("xr"); return; } }
            finally { TryDelete(py); }
        }
        else { _xrMsg = "⚠ Blender ist nicht eingerichtet (Programme → Blender) – ohne Blender geht die Reparatur nicht."; Show("xr"); return; }
        if (!ok || !File.Exists(output)) { _xrMsg = "⚠ Reparatur fehlgeschlagen."; Show("xr"); return; }
        _core.Audit.Append(Principal.LocalOwner.Name, "xr.repariert", $"{Path.GetFileName(XrFile)} → {Path.GetFileName(output)}");
        XrFile = output; _xrMsg = $"✓ Repariert: {Path.GetFileName(output)} – das Original bleibt unverändert.";
        Show("xr");
    }

    /// <summary>Karte „Für die Brille reparieren“: was die Reparatur tun würde, dazu der Knopf.</summary>
    private Control XrFixCard(XrModelInfo info, XrDetail? det)
    {
        var plan = XrPro.Plan(info, det, XrDevice);
        var dev = XrInspector.Budgets[Math.Clamp(XrDevice, 0, XrInspector.Budgets.Count - 1)].Device.Split(" (")[0];
        var sp = new StackPanel { Name = "xr-fix-card", Spacing = 4 };
        sp.Children.Add(H("Für die Brille reparieren", 12));
        if (!plan.Needed && plan.Steps.Count == 0) { sp.Children.Add(P($"✓ Passt auf {dev}: Dreiecke, Texturen und Maßstab sind im Rahmen.", Brush.Parse("#4ADE80"), 11.5)); return sp; }
        foreach (var s in plan.Steps) sp.Children.Add(P("• " + s, s.StartsWith("Hinweis", StringComparison.Ordinal) ? Muted : Text, 11.5));
        if (plan.Needed)
        {
            var b = new Button { Name = "xr-fix", Content = $"🛠 Für {dev} reparieren", Padding = new Thickness(10, 4), Background = Accent, Foreground = Brushes.White };
            ToolTip.SetTip(b, "Blender rechnet eine neue Datei „…-brille.glb“ – das Original bleibt");
            b.Click += (_, _) => _ = XrFix();
            sp.Children.Add(b);
        }
        return sp;
    }
}
