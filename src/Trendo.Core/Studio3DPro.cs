using System.Globalization;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>3D-Modelle erstellen wie Profis (Profi-Ausbau 2, 05.11.2026): Arbeitsschritte Modellieren → UV → Material (PBR) → Licht → Kamera → Render,
/// PBR-Materialien, Licht- und Kamera-Vorgaben, Gruppen im Szenenbaum, Render-Verlauf für Vorher/Nachher. Vorbild: Blender-Arbeitsablauf (nur Funktionsideen).</summary>
public static class Studio3DPro
{
    public sealed record Step(string Id, string Label, string Hint);
    public static readonly Step[] Steps =
    [
        new("modell", "Modellieren", "Teile setzen, verschieben, drehen, skalieren – mit echten Maßen in Zentimetern."),
        new("uv", "UV", "Abwicklung für Texturen: Trendo legt sie beim Bauen in Blender automatisch an (Smart UV)."),
        new("material", "Material", "PBR-Material je Teil: Metall, Rauheit, Glas – die Ansicht zeigt den Glanz."),
        new("licht", "Licht", "Licht mit echter Quelle: Studio, Tageslicht, Abendsonne oder Innenraum."),
        new("kamera", "Kamera", "Brennweite und Blickhöhe wie bei einer echten Kamera."),
        new("render", "Render", "Fotorealistisch in Blender (Cycles) rendern und mit dem letzten Bild vergleichen."),
    ];

    /// <summary>PBR-Material: Metall 0/1, Rauheit 0–1, Durchlässigkeit (Glas) 0–1, typische Farbe (null = Farbe des Teils).</summary>
    /// <param name="TextureId">Echte Oberfläche von ambientCG (CC0); ohne bleibt es bei Farbe und Rauheit.</param>
    /// <param name="TileM">Kantenlänge einer Texturkachel in Metern.</param>
    public sealed record Material(string Id, string Label, double Metallic, double Roughness, double Transmission = 0, string? Tint = null, string? TextureId = null, string? Finish = null, double TileM = 0.5);
    public static readonly Material[] Materials =
    [
        new("kunststoff-matt", "Kunststoff matt", 0, 0.6),
        new("kunststoff", "Kunststoff glänzend", 0, 0.25),
        new("lack", "Lack", 0, 0.1),
        new("holz", "Holz", 0, 0.65, Tint: "#B07A48", TextureId: "Wood092", TileM: 0.5),
        new("holz-hell", "Holz hell", 0, 0.65, Tint: "#D2B48C", TextureId: "Wood096", TileM: 0.5),
        new("stein", "Stein", 0, 0.85, Tint: "#8F8A82", TextureId: "Concrete034", TileM: 0.8),
        new("marmor", "Marmor", 0, 0.2, Tint: "#E6E2DA", TextureId: "Marble012", TileM: 0.6),
        new("ziegel", "Ziegel", 0, 0.85, Tint: "#A0522D", TextureId: "Bricks060", TileM: 0.8),
        new("leder", "Leder", 0, 0.5, Tint: "#6B4A2F", TextureId: "Leather039", TileM: 0.2),
        new("keramik", "Keramik", 0, 0.15, Tint: "#EDEAE4"),
        new("gummi", "Gummi", 0, 0.9, Tint: "#26282B"),
        new("stoff", "Stoff", 0, 1.0, TextureId: "Fabric019", TileM: 0.12),
        new("metall", "Metall gebürstet", 1, 0.35, Tint: "#D6D9DD", Finish: "gebuerstet"),
        new("alu-gefraest", "Aluminium gefräst", 1, 0.36, Tint: "#F0F1F2", Finish: "gefraest"),
        new("chrom", "Chrom", 1, 0.06, Tint: "#D9DCE0"),
        new("gold", "Gold", 1, 0.2, Tint: "#D4A93C", TextureId: "Metal048A", TileM: 0.3),
        new("kupfer", "Kupfer", 1, 0.3, Tint: "#B87333"),
        new("glas", "Glas", 0, 0.02, 1, "#F2F6F8"),
    ];
    public static Material MaterialOf(string? id) => Materials.FirstOrDefault(m => m.Id == id) ?? Materials[0];

    public sealed record LightSetup(string Id, string Label, string Hint);
    public static readonly LightSetup[] Lights =
    [
        new("studio", "Studio", "Drei Flächenleuchten (Haupt-, Füll-, Kantenlicht) – wie im Fotostudio"),
        new("tag", "Tageslicht", "Sonne mittags und blauer Himmel"),
        new("abend", "Abendsonne", "Tiefe, warme Sonne mit langen Schatten"),
        new("innen", "Innenraum", "Warmes Deckenlicht und Fensterlicht"),
    ];

    /// <summary>Stand der Arbeitsschritte: erledigt oder offen, mit kurzem Grund.</summary>
    public static List<(Step Step, bool Done, string Why)> Progress(Scene3D s, int renders)
    {
        var parts = s.Parts.Where(p => !p.Hidden).ToList();
        var noMat = parts.Count(p => string.IsNullOrEmpty(p.Material));
        return
        [
            (Steps[0], parts.Count > 0, parts.Count > 0 ? $"{parts.Count} Teil(e)" : "noch keine Teile"),
            (Steps[1], parts.Count > 0, parts.Count > 0 ? "automatisch beim Bauen" : "folgt nach dem Modellieren"),
            (Steps[2], parts.Count > 0 && noMat == 0, noMat == 0 ? "alle Teile haben ein Material" : $"{noMat} Teil(e) ohne Material"),
            (Steps[3], !string.IsNullOrEmpty(s.Light), string.IsNullOrEmpty(s.Light) ? "noch kein Licht gewählt" : Lights.FirstOrDefault(l => l.Id == s.Light)?.Label ?? s.Light!),
            (Steps[4], s.CameraLens > 0, s.CameraLens > 0 ? $"{s.CameraLens:0} mm" : "noch keine Kamera"),
            (Steps[5], renders > 0, renders > 0 ? $"{renders} Render(s)" : "noch nicht gerendert"),
        ];
    }

    /// <summary>Teile einer Gruppe (Szenenbaum). Leerer Name = Teile ohne Gruppe.</summary>
    public static List<Part3D> InGroup(Scene3D s, string group) => [.. s.Parts.Where(p => (p.Group ?? "") == group)];
    public static List<string> Groups(Scene3D s) => [.. s.Parts.Select(p => p.Group ?? "").Where(g => g.Length > 0).Distinct()];

    // ───────────── Render-Verlauf ─────────────
    public static string RenderDir(string projectDir) => Path.Combine(projectDir, "renders");
    /// <summary>Gerenderte Bilder, neuestes zuerst.</summary>
    public static List<string> Renders(string projectDir) =>
        Directory.Exists(RenderDir(projectDir)) ? [.. Directory.GetFiles(RenderDir(projectDir), "render-*.png").OrderByDescending(f => f, StringComparer.Ordinal)] : [];
    public static string NextRenderPath(string projectDir, DateTime now)
    {
        Directory.CreateDirectory(RenderDir(projectDir));
        var p = Path.Combine(RenderDir(projectDir), $"render-{now:yyyyMMdd-HHmmss}.png");
        for (var i = 2; File.Exists(p); i++) p = Path.Combine(RenderDir(projectDir), $"render-{now:yyyyMMdd-HHmmss}-{i}.png");
        return p;
    }

    /// <summary>Blender-Skript: Szene wie beim Bauen (Teile, PBR, UV, Licht, Kamera) und fotorealistisch mit Cycles rendern (AgX, Entrauschen).</summary>
    public static (Cli.Result R, string Out) Render(string blender, string specPath, string outPng, int samples = 128)
    {
        var blend = Path.ChangeExtension(outPng, ".blend");
        var script = Path.Combine(Path.GetTempPath(), "trendo-3d-render-" + Environment.ProcessId + ".py");
        File.WriteAllText(script, Studio3D.SceneScript + RenderTail, new System.Text.UTF8Encoding(false));
        var r = Cli.Run(blender, ["--background", "--factory-startup", "--python-exit-code", "1", "--python", script, "--", specPath, blend, "", outPng, samples.ToString(CultureInfo.InvariantCulture)], null, 1800000);
        return (r, outPng);
    }

    /// <summary>Anhang an das Bau-Skript: Cycles-Render ins PNG (GPU, wenn vorhanden).</summary>
    public const string RenderTail = """

out_png = a[3] if len(a) > 3 else ""
if out_png:
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "OPTIX"
        prefs.get_devices()
        for d in prefs.devices: d.use = True
        sc.cycles.device = "GPU"
    except Exception:
        sc.cycles.device = "CPU"
    sc.cycles.samples = int(a[4]) if len(a) > 4 else 128
    sc.cycles.use_denoising = True
    try: sc.view_settings.view_transform = "AgX"
    except Exception: pass
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 1280, 960, 100
    sc.render.filepath = out_png; sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    print("TRENDO_RENDER ok")
""";
}
