using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core;

namespace Trendo.App;

/// <summary>0.9.47 (Wunsch: komplexe Figuren wie ein Drache): Kreatur-Baukasten im 3D-Studio. Blender baut die Kreatur in echten Maßen
/// mit Haut, Schuppenmaterial, Flughaut, Hörnern, Krallen und Knochengerüst (trendo_kreatur.py) – sichtbar im Blender-Fenster oder im Hintergrund
/// mit Vorschaubild (Cycles). Ergebnis: .blend, .glb mit Rig, .png. Ehrlich: eine Grundform zum Verfeinern, kein fertiges Hollywood-Modell.</summary>
public partial class MainWindow
{
    public static readonly (string Id, string Label)[] KreaturArten = [("drache", "Drache")];
    public static readonly (string Id, string Label)[] KreaturFarben = [("bronze", "Bronze"), ("gruen", "Grün"), ("rot", "Rot"), ("schwarz", "Schwarz")];
    /// <summary>Für Tests: statt Blender zu starten (Programm, Argumente, sichtbar) → Ausgabe von Blender.</summary>
    public static Func<string, IReadOnlyList<string>, bool, Task<string>>? KreaturRunOverride { get; set; }
    string _kreaturArt = "drache", _kreaturFarbe = "bronze", _kreaturMsg = "";
    public string? KreaturLast { get; private set; }
    bool _kreaturBusy;

    string KreaturScript()
    {
        var dir = Path.Combine(_core.DataRoot, "cache", "blender"); Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "trendo_kreatur.py");
        using (var s = AssetLoader.Open(new Uri("avares://TrendoOS/Assets/blender/trendo_kreatur.py"))) using (var o = File.Create(f)) s.CopyTo(o);
        return f;
    }

    /// <summary>Kreatur bauen. sichtbar = Blender öffnet sich und man sieht das Ergebnis dort (Fenster bleibt offen); sonst im Hintergrund mit Vorschaubild.</summary>
    public async Task<string?> KreaturBuild(bool sichtbar)
    {
        var bl = BlenderPath;
        if (bl is null && KreaturRunOverride is null) { await AskBlender(); return null; }
        if (_kreaturBusy) return null;
        _kreaturBusy = true;
        try
        {
            var name = KreaturArten.First(a => a.Id == _kreaturArt).Label;
            var basis = Path.Combine(Folder3D, DevStudio.SafeName($"{name}-{DateTime.Now:yyyyMMdd-HHmmss}"));
            var json = JsonSerializer.Serialize(new { art = _kreaturArt, farbe = _kreaturFarbe, ausgabe = basis.Replace('\\', '/'), bild = !sichtbar, samples = 96 });
            var args = new List<string>(); if (!sichtbar) args.Add("-b");
            args.AddRange(["--factory-startup", "--python", KreaturScript(), "--", json]);
            _kreaturMsg = sichtbar ? "Blender öffnet sich und baut die Kreatur – du siehst sie gleich im Blender-Fenster." : "Blender baut die Kreatur im Hintergrund und rendert ein Vorschaubild …";
            Show("3d");
            _core.Audit.Append(Principal.LocalOwner.Name, "3d.kreatur", $"{_kreaturArt} {(sichtbar ? "sichtbar" : "hintergrund")}");
            if (KreaturRunOverride is { } o) await o(bl ?? "blender", args, sichtbar);
            else if (sichtbar)
            {
                var psi = new ProcessStartInfo(bl!) { UseShellExecute = false };
                foreach (var a in args) psi.ArgumentList.Add(a);
                Process.Start(psi);
                for (var i = 0; i < 360 && !File.Exists(basis + ".glb"); i++) await Task.Delay(500); // Blender bleibt offen – warten, bis die Datei da ist
            }
            else
            {
                var r = await Task.Run(() => Cli.Run(bl!, args, Folder3D, 600_000));
                if (!r.Ok && !File.Exists(basis + ".glb")) { _kreaturMsg = "Blender hat abgebrochen: " + Short(r.Err.Length > 0 ? r.Err : r.Out); return null; }
            }
            if (!File.Exists(basis + ".glb")) { _kreaturMsg = "Blender hat noch keine Datei geschrieben. Sieh im Blender-Fenster nach."; return null; }
            KreaturLast = basis; _last3dFile = basis + ".glb";
            _kreaturMsg = $"Fertig: {Path.GetFileName(basis)}.glb mit Knochengerüst und .blend im 3D-Ordner. Das ist eine Grundform – für Fotorealismus in Blender verfeinern (Sculpting) oder Claude fragen.";
            return basis;
        }
        finally { _kreaturBusy = false; Show("3d"); }
    }

    /// <summary>Karte im 3D-Studio: Art und Farbe wählen, in Blender bauen (sichtbar) oder Vorschaubild rendern, Ergebnis ansehen.</summary>
    Control KreaturBlock()
    {
        var sp = new StackPanel { Name = "3d-kreatur", Spacing = 6 };
        sp.Children.Add(H("🐉 Kreatur bauen", 14));
        sp.Children.Add(P("Komplexe Figur in echten Maßen mit Haut, Schuppen, Flügeln und Knochengerüst – Blender baut sie.", Muted, 12));
        var art = new ComboBox { Name = "3d-kreatur-art", ItemsSource = KreaturArten.Select(a => a.Label).ToList(), SelectedIndex = Array.FindIndex(KreaturArten, a => a.Id == _kreaturArt), MinWidth = 120 };
        art.SelectionChanged += (_, _) => { if (art.SelectedIndex >= 0) _kreaturArt = KreaturArten[art.SelectedIndex].Id; };
        var farbe = new ComboBox { Name = "3d-kreatur-farbe", ItemsSource = KreaturFarben.Select(a => a.Label).ToList(), SelectedIndex = Array.FindIndex(KreaturFarben, a => a.Id == _kreaturFarbe), MinWidth = 110 };
        farbe.SelectionChanged += (_, _) => { if (farbe.SelectedIndex >= 0) _kreaturFarbe = KreaturFarben[farbe.SelectedIndex].Id; };
        sp.Children.Add(new WrapPanel { Children = { art, new Border { Width = 6 }, farbe } });
        var live = Act("3d-kreatur-live", "In Blender bauen (sichtbar)", () => _ = KreaturBuild(true), true);
        var shot = Act("3d-kreatur-bild", "Vorschaubild rendern", () => _ = KreaturBuild(false));
        live.IsEnabled = shot.IsEnabled = !_kreaturBusy;
        sp.Children.Add(new WrapPanel { Children = { live, new Border { Width = 4 }, shot } });
        if (_kreaturMsg.Length > 0) sp.Children.Add(P(_kreaturMsg, Text, 12));
        if (KreaturLast is { } k)
        {
            if (File.Exists(k + ".png"))
                try { sp.Children.Add(new Image { Name = "3d-kreatur-vorschau", Source = new Bitmap(k + ".png"), MaxHeight = 190, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left }); }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { }
            var open = Act("3d-kreatur-oeffnen", "In Blender öffnen", () => { if (File.Exists(k + ".blend")) Launch(k + ".blend"); });
            open.IsEnabled = File.Exists(k + ".blend");
            sp.Children.Add(new WrapPanel { Children = { open, new Border { Width = 4 }, Act("3d-kreatur-ordner", "Ordner zeigen", () => Launch(Folder3D)) } });
        }
        return sp;
    }
}
