using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-04: VR/AR/MR – Modell-Prüfung für Brillen und Handy-AR.</summary>
public partial class MainWindow
{
    private void InitSpec04() => RegisterTab(FachTabs, "xr", "VR & AR", "SPEC-04", XrView, "vr", "ar", "mr", "xr", "brille", "quest", "gltf", "glb", "virtuelle realität");

    private Control XrView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Läuft dein 3D-Modell flüssig auf der Brille? Trendo liest glTF/GLB-Dateien (z. B. aus Blender) und vergleicht sie mit den Grenzen der Geräte.", Text));
        sp.Children.Add(FachTool("xr-check", "Modell prüfen", "Pfad zu einer .glb- oder .gltf-Datei.",
            [("file", "Datei", "")],
            v =>
            {
                var f = v("file").Trim().Trim('"'); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht.");
                var m = XrInspector.Inspect(f);
                var lines = new List<string> { $"{m.File}: {Fach.F(m.Triangles, 0)} Dreiecke, {Fach.F(m.Vertices, 0)} Punkte, {m.Primitives} Zeichenaufrufe, {m.Materials} Materialien, {m.Textures} Texturen, {m.Animations} Animationen, {m.Bytes / 1048576.0:0.##} MB" };
                foreach (var b in XrInspector.Budgets) { var c = XrInspector.Check(m, b); lines.Add($"{(c.Ok ? "✓" : "⚠")} {b.Device}{(c.Ok ? ": läuft flüssig" : ": " + string.Join(" ", c.Problems))}"); }
                return string.Join("\n", lines);
            }, "Prüfen"));
        sp.Children.Add(FachTool("xr-frame", "Bildzeit prüfen", "Gemessene Zeit pro Bild (ms) aus der Leistungsanzeige der Brille oder Engine.",
            [("fps", "Bildwiederholrate (Hz)", "72"), ("ms", "Gemessen (ms)", "12")],
            v => { var (b, ok, h) = XrInspector.FrameBudget((int)Fach.Num(v("fps")), Fach.Num(v("ms"))); return ok ? $"✓ Flüssig: Budget {Fach.F(b)} ms, {Fach.F(h, 1)} % Reserve." : $"⚠ Ruckelt: Budget nur {Fach.F(b)} ms. Das kann übel machen – Szene vereinfachen."; }));
        return FachPage("VR & AR", sp);
    }
}
