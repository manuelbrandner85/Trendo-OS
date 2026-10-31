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
        sp.Children.Add(P("Passt dein 3D-Modell in die Grenzen der Brille? Trendo liest glTF/GLB-Dateien (z. B. aus Blender) und vergleicht sie mit den Richtwerten der Geräte. Ob es auf dem Gerät wirklich flüssig läuft, zeigt nur ein Test auf der Brille.", Text));
        sp.Children.Add(FachTool("xr-check", "Modell prüfen", "Pfad zu einer .glb- oder .gltf-Datei.",
            [("file", "Datei", "")],
            v =>
            {
                var f = v("file").Trim().Trim('"'); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht.");
                var m = XrInspector.Inspect(f);
                var lines = new List<string> { $"{m.File}: {Fach.F(m.Triangles, 0)} Dreiecke, {Fach.F(m.Vertices, 0)} Punkte, {m.Primitives} Zeichenaufrufe, {m.Materials} Materialien, {m.Textures} Texturen, {m.Animations} Animationen, {m.Bytes / 1048576.0:0.##} MB" };
                foreach (var b in XrInspector.Budgets) { var c = XrInspector.Check(m, b); lines.Add($"{(c.Ok ? "✓" : "⚠")} {b.Device}{(c.Ok ? ": innerhalb der Richtwerte (auf dem Gerät nicht geprüft)" : ": " + string.Join(" ", c.Problems))}"); }
                return string.Join("\n", lines);
            }, "Prüfen"));
        sp.Children.Add(FachTool("xr-frame", "Bildzeit prüfen", "Trag die Zeit pro Bild ein, die du in der Leistungsanzeige der Brille oder Engine abgelesen hast – Trendo misst hier nicht selbst.",
            [("fps", "Bildwiederholrate (Hz)", "72"), ("ms", "Deine Messung (ms)", "12")],
            v => { var (b, ok, h) = XrInspector.FrameBudget((int)Fach.Num(v("fps")), Fach.Num(v("ms"))); return ok ? $"✓ Laut deiner Messung im Budget: {Fach.F(b)} ms je Bild, {Fach.F(h, 1)} % Reserve." : $"⚠ Laut deiner Messung zu langsam: Budget nur {Fach.F(b)} ms. Das kann übel machen – Szene vereinfachen."; }));
        sp.Children.Add(FachGroup("xr-more", "Modell genauer ansehen", false,
            FachTool("xr-detail", "Details & Bericht", "Maße, Namen, Texturen, Materialien, Erweiterungen. Der Bericht wird neben dem Modell gespeichert.",
                [("file", "Datei (.glb/.gltf)", "")],
                v =>
                {
                    var f = v("file").Trim().Trim('"'); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht.");
                    var d = XrPlus.Detail(f); var rep = Path.ChangeExtension(f, ".pruefbericht.md"); File.WriteAllText(rep, XrPlus.Report(d));
                    var l = new List<string> { XrPlus.ScaleVerdict(d), $"Knoten: {string.Join(", ", d.Nodes.Take(10))}{(d.Nodes.Count > 10 ? " …" : "")}", $"Animationen: {(d.Animations.Count == 0 ? "keine" : string.Join(", ", d.Animations))}" };
                    l.AddRange(d.Textures.Select(t => $"Textur {t.Name}: {t.Width} × {t.Height}, ca. {Fach.F(t.VramBytes / 1048576.0, 1)} MB Grafikspeicher"));
                    if (d.Textures.Count > 0) l.Add($"Grafikspeicher gesamt ca. {Fach.F(d.Textures.Sum(t => t.VramBytes) / 1048576.0, 1)} MB");
                    l.Add("Zeichenaufrufe je Material: " + string.Join(", ", d.DrawCallsPerMaterial.Select(kv => $"{kv.Key} {kv.Value}")));
                    l.AddRange(XrPlus.ExtensionNotes(d)); l.Add("Bericht: " + rep); return string.Join("\n", l);
                }, "Ansehen"),
            FachTool("xr-lod", "Detailstufen planen", "Wie viele Dreiecke je Stufe, ab welcher Entfernung umschalten?",
                [("tris", "Dreiecke (voll)", "200000"), ("size", "Objektgröße (m)", "2")],
                v => string.Join("\n", XrPlus.LodPlan((long)Fach.Num(v("tris")), Fach.Num(v("size"))).Select(x => $"Stufe {x.Level}: {Fach.F(x.Triangles, 0)} Dreiecke ab {Fach.F(x.FromM, 1)} m")), "Planen"),
            FachTool("xr-texel", "Textur scharf genug?", "Texturkante in Pixeln, Objektgröße in m, typischer Abstand zum Betrachter.",
                [("px", "Textur (px)", "2048"), ("size", "Objekt (m)", "2"), ("dist", "Abstand (m)", "1")],
                v => { var (d, t) = XrPlus.TexelDensity((int)Fach.Num(v("px")), Fach.Num(v("size")), Fach.Num(v("dist"))); return $"{Fach.F(d, 0)} Pixel pro Meter – {t}"; }, "Prüfen")));
        sp.Children.Add(FachGroup("xr-comfort", "Bequem für den Körper", false,
            FachTool("xr-move", "Wird jemandem übel?", "Werte aus deiner App bzw. Engine. Schritt-Drehen und Tunnelblick: j/n.",
                [("move", "Gehtempo (m/s)", "2"), ("turn", "Drehen (°/s)", "120"), ("snap", "Schritt-Drehen (j/n)", "n"), ("vig", "Tunnelblick (j/n)", "n"), ("acc", "Beschleunigung (m/s²)", "0")],
                v => { bool J(string k) => v(k).Trim().StartsWith("j", StringComparison.OrdinalIgnoreCase); var (sc, tips) = XrPlus.Comfort(Fach.Num(v("move")), Fach.Num(v("turn")), J("snap"), J("vig"), Fach.Num(v("acc"))); return $"Komfort: {sc} von 100{(tips.Count == 0 ? " ✓" : "\n• " + string.Join("\n• ", tips))}"; }, "Prüfen")));
        return FachPage("VR & AR", sp);
    }
}
