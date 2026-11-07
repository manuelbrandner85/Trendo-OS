using System.Globalization;

namespace Trendo.Core;

/// <summary>VR & AR gestalten (Profi 3 + Claude 3, 06.11.2026): Texturgrenze je Brille, Vorschau aus Augenhöhe, Brillen-Tauglichkeit prüfen und mit einem Klick
/// reparieren (Polygone reduzieren, Texturen verkleinern, Maßstab auf Meter). Vorbild: Leistungsrichtlinien der Brillen-Hersteller (nur Richtwerte).</summary>
public static class XrPro
{
    /// <summary>Größte sinnvolle Texturkante je Gerät (gleiche Reihenfolge wie <see cref="XrInspector.Budgets"/>).</summary>
    public static int MaxTextureSide(int device) => device switch { 0 => 2048, 1 => 1024, 2 => 2048, _ => 4096 };

    /// <summary>Augenhöhe stehend (cm) und Abstand zum Objekt für die Vorschau.</summary>
    public const double EyeCm = 160, ViewDistanceCm = 200;

    /// <summary>S3 (Modernisierung 07.11.2026): Brillen-Ansicht – Sichtfeld (waagrecht × senkrecht, Grad) und Augenabstand (mm) je Zielgerät,
    /// Herstellerangaben gerundet (Meta Quest 3 110° × 96°, Quest 2 97° × 93°, PC-VR typisch 108° × 100°); Handy-AR hat keine Brille → null.</summary>
    public sealed record Headset(string Name, double HFovDeg, double VFovDeg, double IpdMm);
    public static Headset? HeadsetOf(int device) => device switch
    {
        0 => new("Meta Quest 3 / Pico 4", 110, 96, 63),
        1 => new("Meta Quest 2", 97, 93, 63),
        3 => new("PC-VR", 108, 100, 63),
        _ => null,
    };

    public sealed record FixPlan(double DecimateRatio, int MaxTexture, double ScaleToMeters, IReadOnlyList<string> Steps)
    {
        public bool Needed => DecimateRatio < 0.999 || ScaleToMeters is < 0.999 or > 1.001 || Steps.Any(s => s.StartsWith("Texturen", StringComparison.Ordinal));
    }

    /// <summary>Was die Reparatur für das Gerät tun würde: Dreiecke auf 90 % des Budgets, zu große Texturen auf die Gerätegrenze, falsche Einheit (cm/mm) auf Meter.</summary>
    public static FixPlan Plan(XrModelInfo info, XrDetail? detail, int device)
    {
        var b = XrInspector.Budgets[Math.Clamp(device, 0, XrInspector.Budgets.Count - 1)];
        var steps = new List<string>();
        var ratio = info.Triangles > b.MaxTriangles ? Math.Round(b.MaxTriangles * 0.9 / info.Triangles, 3) : 1.0;
        if (ratio < 1) steps.Add($"Dreiecke reduzieren: {Fach.F(info.Triangles, 0)} → etwa {Fach.F(info.Triangles * ratio, 0)} (Form bleibt, feine Details werden gröber)");
        var maxTex = MaxTextureSide(device);
        var big = detail?.Textures.Where(t => Math.Max(t.Width, t.Height) > maxTex).ToList() ?? [];
        if (big.Count > 0) steps.Add($"Texturen verkleinern: {big.Count} größer als {maxTex} px → {maxTex} px");
        var scale = 1.0;
        if (detail is not null)
        {
            var m = Math.Max(detail.SizeX, Math.Max(detail.SizeY, detail.SizeZ));
            if (m > 500)
            {
                // Zentimeter oder Millimeter? Den Faktor nehmen, der eine typische Gegenstandsgröße ergibt (um 1,5 m, logarithmisch gemessen)
                scale = Math.Abs(Math.Log(m * 0.001 / 1.5)) <= Math.Abs(Math.Log(m * 0.01 / 1.5)) ? 0.001 : 0.01;
                steps.Add($"Maßstab: {m:0} m ist zu groß – als {(scale == 0.01 ? "Zentimeter" : "Millimeter")} erkannt, auf Meter umrechnen");
            }
            else if (m is > 0 and < 0.01) { scale = 100; steps.Add($"Maßstab: nur {m * 1000:0.#} mm – auf Meter umrechnen (× 100)"); }
        }
        if (info.Primitives > b.MaxDrawCalls) steps.Add($"Hinweis: {info.Primitives} Zeichenaufrufe (Budget {b.MaxDrawCalls}) – Materialien in Blender zusammenfassen; das macht die Reparatur nicht automatisch");
        return new(ratio, maxTex, scale, steps);
    }

    /// <summary>Blender-Skript für die Reparatur: GLB laden, Maßstab anwenden, Netze dezimieren, große Bilder verkleinern, als GLB speichern.</summary>
    public static string FixScript(string input, string output, FixPlan plan)
    {
        static string Py(string s) => "r'" + s.Replace("'", "") + "'";
        var inv = CultureInfo.InvariantCulture;
        return "import bpy, sys\n"
            + "bpy.ops.wm.read_homefile(use_empty=True)\n"
            + $"bpy.ops.import_scene.gltf(filepath={Py(input)})\n"
            + "meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']\n"
            + "if not meshes:\n    print('TRENDO_FEHLER keine Netze'); sys.exit(3)\n"
            + $"s = {plan.ScaleToMeters.ToString("R", inv)}\n"
            + "if abs(s - 1) > 1e-6:\n    for o in bpy.context.scene.objects:\n        if o.parent is None: o.scale = tuple(v * s for v in o.scale); o.location = tuple(v * s for v in o.location)\n"
            + $"r = {plan.DecimateRatio.ToString("R", inv)}\n"
            + "if r < 0.999:\n    for o in meshes:\n        m = o.modifiers.new('Reduzieren', 'DECIMATE'); m.ratio = max(0.01, r)\n"
            + $"mx = {plan.MaxTexture}\n"
            + "for img in bpy.data.images:\n    w, h = img.size[0], img.size[1]\n    if max(w, h) > mx and w > 0 and h > 0:\n        f = mx / max(w, h); img.scale(max(1, int(w * f)), max(1, int(h * f))); img.pack()\n"
            + $"bpy.ops.export_scene.gltf(filepath={Py(output)}, export_format='GLB', export_apply=True, export_yup=True)\n"
            + "print('TRENDO_OK')\n";
    }
}
