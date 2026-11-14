using System.Text;

namespace Trendo.Core.Programs;

/// <summary>T-0660, M4 (Teil Unreal): die in Blender aufbereiteten Mixamo-Dateien ins Unreal-Projekt holen – Figur als Skeletal Mesh, Aktionen als Animationen
/// auf ihrem Skelett – und per IK Retargeter auf die Engine-Figur (Mannequin aus dem Mover-Beispiel) übertragen, damit sie auch auf deren Skelett
/// (und auf MetaHumans, die von dort erben) laufen. Die Rohdateien liegen nur im Projektordner „Trendo/Mixamo“; der Lizenzwächter verhindert ihre Weitergabe.</summary>
public static class MixamoUnreal
{
    public const string ScriptRel = "Trendo/trendo_mixamo.py";
    public const string FilesRel = "Trendo/Mixamo";

    public static List<string> Args(string uproject, string log) =>
        [uproject, "-ExecutePythonScript=" + Path.Combine(Path.GetDirectoryName(uproject)!, ScriptRel.Replace('/', Path.DirectorySeparatorChar)), "-unattended", "-nullrhi", "-nosplash", "-nopause", "-log", "-abslog=" + log];

    /// <summary>Aufbereitete FBX (eine je Aktion) ins Projekt kopieren und das Skript schreiben. Liefert die Zahl der Dateien.</summary>
    public static int Stage(string projectDir, IEnumerable<string> fbxFiles)
    {
        var dir = Path.Combine(projectDir, FilesRel.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.fbx")) File.Delete(f);
        Directory.CreateDirectory(dir);
        var n = 0;
        foreach (var f in fbxFiles.Where(File.Exists)) { File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true); n++; }
        File.WriteAllText(Path.Combine(projectDir, ScriptRel.Replace('/', Path.DirectorySeparatorChar)), Script, new UTF8Encoding(false));
        return n;
    }

    /// <summary>Größe der Figur in Unreal (cm) aus dem Protokoll, 0 wenn nicht gemessen.</summary>
    public static int HeightCm(string log) => System.Text.RegularExpressions.Regex.Matches(log ?? "", @"TRENDO-MIXAMO hoehe_cm=(\d+)").Select(m => int.Parse(m.Groups[1].Value)).LastOrDefault();

    /// <summary>Ergebnis aus dem Protokoll: ok, importierte Animationen, übertragene Animationen, Fehlertext.
    /// Eine Figur außerhalb von 50 bis 300 cm gilt als Maßstabsfehler (bekannte Faktor-100-Falle).</summary>
    public static (bool Ok, int Imported, int Retargeted, string Error) Parse(string log)
    {
        var h = HeightCm(log);
        if (h > 0 && (h < 50 || h > 300)) return (false, 0, 0, $"Figur ist in Unreal {h} cm groß – Maßstab stimmt nicht");
        var lines = (log ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Contains("TRENDO-MIXAMO")).ToList();
        int Num(string key) => lines.Select(l => System.Text.RegularExpressions.Regex.Match(l, key + @"=(\d+)")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).LastOrDefault();
        var ok = lines.Any(l => l.Contains("TRENDO-MIXAMO-OK"));
        var err = lines.LastOrDefault(l => l.Contains("TRENDO-MIXAMO-FEHLER")) is { } e ? e[(e.IndexOf("TRENDO-MIXAMO-FEHLER", StringComparison.Ordinal) + 20)..].Trim() : ok ? "" : "Kein Bericht von Unreal – Protokoll mixamo.txt";
        return (ok, Num("importiert"), Num("uebertragen"), err);
    }

    public const string Script = """
import unreal, os, traceback
eal = unreal.EditorAssetLibrary; tools = unreal.AssetToolsHelpers.get_asset_tools()
ROOT = "/Game/Trendo/Mixamo"
SRC = "/MoverExamples/Characters/Mannequins/"
FILES = os.path.join(unreal.Paths.project_dir(), "Trendo", "Mixamo")
def log(m): unreal.log("TRENDO-MIXAMO " + str(m).replace("\n", " | "))

def task(path, dest, name, skeleton=None):
    t = unreal.AssetImportTask(); t.filename = path; t.destination_path = dest; t.destination_name = name
    t.automated = True; t.save = True; t.replace_existing = True
    o = unreal.FbxImportUI(); o.import_materials = skeleton is None; o.import_textures = skeleton is None
    o.import_as_skeletal = True; o.import_mesh = skeleton is None; o.import_animations = skeleton is not None
    o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH if skeleton is None else unreal.FBXImportType.FBXIT_ANIMATION
    if skeleton is not None: o.skeleton = skeleton
    t.options = o
    tools.import_asset_tasks([t])
    return [p for p in t.imported_object_paths]

def run():
    fbx = sorted(f for f in os.listdir(FILES) if f.lower().endswith(".fbx"))
    if not fbx: return "Keine aufbereiteten Dateien in Trendo/Mixamo"
    # Figur aus der ersten Datei (Skelett und Mesh), dann jede Aktion als Animation auf diesem Skelett
    mesh_paths = task(os.path.join(FILES, fbx[0]), ROOT + "/Figur", "SK_Mixamo")
    mesh = next((unreal.load_asset(p) for p in mesh_paths if isinstance(unreal.load_asset(p), unreal.SkeletalMesh)), None)
    if mesh is None: return "Figur ließ sich nicht importieren (" + fbx[0] + ")"
    skel = mesh.get_editor_property("skeleton")
    b = mesh.get_bounds(); log("hoehe_cm=%d" % int(round(b.box_extent.z * 2)))  # Maßstabsprüfung: ein Mensch ist 150 bis 200 cm
    anims = []
    for f in fbx:
        name = "MX_" + os.path.splitext(f)[0]
        for p in task(os.path.join(FILES, f), ROOT + "/Bewegung", name, skel):
            a = unreal.load_asset(p)
            if isinstance(a, unreal.AnimSequence):
                anims.append(p); log("animation %s laenge_ms=%d" % (name, int(round(unreal.AnimationLibrary.get_sequence_length(a) * 1000))))
    log("importiert=%d" % len(anims))
    if not anims: return "Keine Animation importiert"
    # IK Retargeter Mixamo -> Engine-Figur
    if not eal.does_asset_exist(SRC + "Rigs/IK_Mannequin"):
        log("Hinweis: Engine-Figur (MoverExamples) fehlt – Animationen bleiben auf dem Mixamo-Skelett"); log("uebertragen=0"); return ""
    rig = tools.create_asset("IK_Mixamo", ROOT, unreal.IKRigDefinition, unreal.IKRigDefinitionFactory())
    rc = unreal.IKRigController.get_controller(rig); rc.set_skeletal_mesh(mesh); rc.apply_auto_generated_retarget_definition(); eal.save_loaded_asset(rig)
    rtg = tools.create_asset("RTG_Mixamo_Mannequin", ROOT, unreal.IKRetargeter, unreal.IKRetargetFactory())
    c = unreal.IKRetargeterController.get_controller(rtg)
    dst_rig = unreal.load_asset(SRC + "Rigs/IK_Mannequin"); manny = unreal.load_asset(SRC + "Meshes/SKM_Manny_Simple")
    c.set_ik_rig(unreal.RetargetSourceOrTarget.SOURCE, rig); c.set_ik_rig(unreal.RetargetSourceOrTarget.TARGET, dst_rig)
    c.remove_all_ops(); c.add_default_ops()
    c.assign_ik_rig_to_all_ops(unreal.RetargetSourceOrTarget.SOURCE, rig); c.assign_ik_rig_to_all_ops(unreal.RetargetSourceOrTarget.TARGET, dst_rig)
    c.auto_map_chains(unreal.AutoMapChainType.FUZZY, True); eal.save_loaded_asset(rtg)
    data = [eal.find_asset_data(p) for p in anims]
    out = unreal.IKRetargetBatchOperation.duplicate_and_retarget(assets_to_retarget=data, source_mesh=mesh, target_mesh=manny, ik_retarget_asset=rtg,
                                                                 prefix="MN_", target_path=ROOT + "/Mannequin", include_referenced_assets=False)
    eal.save_directory(ROOT)
    log("uebertragen=%d" % len([p for p in eal.list_assets(ROOT + "/Mannequin", recursive=True) if "MN_" in p]))
    return ""

try:
    err = run()
    unreal.log(("TRENDO-MIXAMO-FEHLER " + err) if err else "TRENDO-MIXAMO-OK")
except Exception:
    unreal.log("TRENDO-MIXAMO-FEHLER " + traceback.format_exc().strip().splitlines()[-1][:300])
""";
}
