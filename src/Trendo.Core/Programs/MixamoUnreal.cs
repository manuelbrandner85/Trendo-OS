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
    public static int Stage(string projectDir, IEnumerable<string> fbxFiles, string motion = MixamoBlender.InPlace)
    {
        var dir = Path.Combine(projectDir, FilesRel.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.fbx")) File.Delete(f);
        Directory.CreateDirectory(dir);
        var n = 0;
        foreach (var f in fbxFiles.Where(File.Exists)) { File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true); n++; }
        File.WriteAllText(Path.Combine(dir, "bewegung.txt"), motion == MixamoBlender.RootMotion ? MixamoBlender.RootMotion : MixamoBlender.InPlace); // für das Unreal-Skript
        File.WriteAllText(Path.Combine(projectDir, ScriptRel.Replace('/', Path.DirectorySeparatorChar)), Script, new UTF8Encoding(false));
        return n;
    }

    /// <summary>Größe der Figur in Unreal (cm) aus dem Protokoll, 0 wenn nicht gemessen.</summary>
    public static int HeightCm(string log) => System.Text.RegularExpressions.Regex.Matches(log ?? "", @"TRENDO-MIXAMO hoehe_cm=(\d+)").Select(m => int.Parse(m.Groups[1].Value)).LastOrDefault();

    /// <summary>Nach erfolgreichem Import: die Rohdateien im Projektordner löschen (die Assets liegen dann im Content-Ordner).</summary>
    public static void Unstage(string projectDir)
    {
        var dir = Path.Combine(projectDir, FilesRel.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.fbx")) File.Delete(f);
    }

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
    # Wiederholter Lauf: vorhandene Assets wiederverwenden und neu einstellen (create_asset liefert sonst nichts); alte Ergebnisse ersetzen
    def get(name, cls, factory):
        p = ROOT + "/" + name
        return unreal.load_asset(p) if eal.does_asset_exist(p) else tools.create_asset(name, ROOT, cls, factory)
    NEU = ROOT + "/Mannequin_neu"
    if eal.does_directory_exist(NEU): eal.delete_directory(NEU)  # nur der eigene Zwischenordner
    rig = get("IK_Mixamo", unreal.IKRigDefinition, unreal.IKRigDefinitionFactory())
    if rig is None: return "IK Rig ließ sich nicht anlegen"
    rc = unreal.IKRigController.get_controller(rig); rc.set_skeletal_mesh(mesh); rc.apply_auto_generated_retarget_definition(); eal.save_loaded_asset(rig)
    rtg = get("RTG_Mixamo_Mannequin", unreal.IKRetargeter, unreal.IKRetargetFactory())
    if rtg is None: return "IK Retargeter ließ sich nicht anlegen"
    c = unreal.IKRetargeterController.get_controller(rtg)
    dst_rig = unreal.load_asset(SRC + "Rigs/IK_Mannequin"); manny = unreal.load_asset(SRC + "Meshes/SKM_Manny_Simple")
    c.set_ik_rig(unreal.RetargetSourceOrTarget.SOURCE, rig); c.set_ik_rig(unreal.RetargetSourceOrTarget.TARGET, dst_rig)
    c.remove_all_ops(); c.add_default_ops()
    c.assign_ik_rig_to_all_ops(unreal.RetargetSourceOrTarget.SOURCE, rig); c.assign_ik_rig_to_all_ops(unreal.RetargetSourceOrTarget.TARGET, dst_rig)
    # UE 5.8: jede Operation (Becken, Ketten, IK, Wurzelbewegung) braucht ihre Grundeinrichtung, sonst bleibt z. B. das Becken der Engine-Figur unbewegt
    for i in range(c.get_num_retarget_ops()):
        try: c.run_op_initial_setup(i)
        except Exception: log("Hinweis: Operation %s nicht eingerichtet" % c.get_op_name(i))
    # Becken ausdrücklich setzen (Wurzel der IK Rigs): sonst steht das Becken der Engine-Figur auf dem Boden, die Oberschenkel aber oben –
    # das am Becken hängende Mesh zieht sich wie ein Rock nach unten (an echter Mixamo-Datei gemessen: Becken z=0, Oberschenkel z≈90 cm)
    i = c.get_index_of_op_by_name("Pelvis Motion")
    if i >= 0:
        oc = c.get_op_controller(i)
        oc.set_source_pelvis_bone(rc.get_retarget_root()); oc.set_target_pelvis_bone(unreal.IKRigController.get_controller(dst_rig).get_retarget_root())
        log("becken=%s->%s" % (oc.get_source_pelvis_bone(), oc.get_target_pelvis_bone()))
    # Exakte Namen statt FUZZY: FUZZY hängte Drehknochen an das Schlüsselbein und die Wurzel an den rechten Fuß (an echter Mixamo-Datei gesehen).
    # Danach gezielt: Fuß-Ziele an die Füße; alles andere ohne Quelle bleibt unbewegt statt falsch.
    c.auto_map_chains(unreal.AutoMapChainType.EXACT, True)
    for src, dst in (("LeftFoot", "LeftFootIK"), ("RightFoot", "RightFootIK")):
        try: c.set_source_chain(src, dst)
        except Exception: log("Hinweis: Kette %s nicht zugeordnet" % dst)
    # Auf der Stelle: die Wurzelbewegungs-Operation aus. In UE 5.8 setzte sie das Becken der Engine-Figur auf den Boden (z=0),
    # während Wirbelsäule und Oberschenkel oben blieben – an echter Mixamo-Datei gemessen und im Bild gesehen.
    motion = open(os.path.join(FILES, "bewegung.txt"), encoding="utf-8").read().strip() if os.path.exists(os.path.join(FILES, "bewegung.txt")) else "inplace"
    i = c.get_index_of_op_by_name("Root Motion")
    if i >= 0 and motion != "root": c.set_retarget_op_enabled(i, False)
    elif i >= 0:
        # Wurzelbewegung: Knochen ausdrücklich setzen (Mixamo-Wurzel „root“ aus Blender, Engine-Wurzel und -Becken)
        ro = c.get_op_controller(i)
        ro.set_source_root_bone("root"); ro.set_target_root_bone("root"); ro.set_target_pelvis_bone(unreal.IKRigController.get_controller(dst_rig).get_retarget_root())
        log("wurzel=%s->%s becken=%s" % (ro.get_source_root_bone(), ro.get_target_root_bone(), ro.get_target_pelvis_bone()))
    log("bewegung=%s" % motion)
    # Grundhaltungen angleichen: Mixamo steht in T-Haltung, die Engine-Figur in A-Haltung. Ohne Angleich ziehen sich die Oberschenkel
    # der Engine-Figur wie ein Rock auseinander (an echter Mixamo-Datei im Bild gesehen).
    try:
        c.create_retarget_pose("Trendo-Angleich", unreal.RetargetSourceOrTarget.TARGET)
        c.auto_align_all_bones(unreal.RetargetSourceOrTarget.TARGET, unreal.RetargetAutoAlignMethod.CHAIN_TO_CHAIN)
        log("haltung=angeglichen")
    except Exception: log("Hinweis: Grundhaltung nicht angeglichen – " + traceback.format_exc().strip().splitlines()[-1][:150])
    tc = unreal.IKRigController.get_controller(dst_rig)
    names = [ch.get_editor_property("chain_name") for ch in tc.get_retarget_chains()]
    mapped = [n for n in names if str(c.get_source_chain(n)) not in ("None", "")]
    log("ketten=%d/%d %s" % (len(mapped), len(names), ",".join("%s<-%s" % (n, c.get_source_chain(n)) for n in mapped)))
    eal.save_loaded_asset(rtg)
    data = [eal.find_asset_data(p) for p in anims]
    # UE 5.8: run_batch_retarget (duplicate_and_retarget ist veraltet und lieferte an einer echten Mixamo-Datei eine leere Animation)
    inp = unreal.IKRetargetBatchOperationInputs()
    inp.set_editor_property("assets_to_retarget", data); inp.set_editor_property("source_mesh", mesh); inp.set_editor_property("target_mesh", manny)
    inp.set_editor_property("ik_retarget_asset", rtg); inp.set_editor_property("prefix", "MN_"); inp.set_editor_property("target_path", NEU)
    inp.set_editor_property("include_referenced_assets", False); inp.set_editor_property("overwrite_existing_files", True)
    out = unreal.IKRetargetBatchOperation.run_batch_retarget(inp)
    # Ergebnis nach Mannequin/: gleichnamige alte Animationen werden ersetzt, Verweise darauf (AnimBP, Blendspace) zeigen danach auf die neue; anderes bleibt unberührt.
    # Gezählt wird nur, was wirklich Bewegung enthält (Bilder und Knochenspuren).
    done = 0; empty = []
    for a in (out or []):
        name = str(a.get_editor_property("asset_name"))
        if not name.startswith("MN_"): continue
        src = NEU + "/" + name; dst = ROOT + "/Mannequin/" + name
        seq = unreal.load_asset(src)
        if not isinstance(seq, unreal.AnimSequence): continue
        frames = unreal.AnimationLibrary.get_num_frames(seq); tracks = len(unreal.AnimationLibrary.get_animation_track_names(seq))
        log("ergebnis %s bilder=%d spuren=%d" % (name, frames, tracks))
        if frames < 2 or tracks == 0: empty.append(name); continue
        # Qualitätsprüfung: das Becken der Engine-Figur muss in Hüfthöhe stehen, nicht am Boden
        pelvis = unreal.IKRigController.get_controller(dst_rig).get_retarget_root()
        pz = unreal.AnimationLibrary.get_bone_pose_for_time(seq, pelvis, 0.0, False).translation.length()
        log("becken_cm %s=%d" % (name, int(round(pz))))
        if pz < 30: empty.append(name + " (Becken am Boden)"); continue
        if eal.does_asset_exist(dst):
            eal.consolidate_assets(seq, [unreal.load_asset(dst)])  # Verweise auf die alte zeigen jetzt auf die neue
            if eal.does_asset_exist(dst): eal.delete_asset(dst)
        if not eal.rename_asset(src, dst): empty.append(name + " (nicht verschoben)"); continue
        moved = unreal.load_asset(dst)
        if unreal.AnimationLibrary.get_num_frames(moved) < 2: empty.append(name + " (nach dem Verschieben leer)"); continue
        done += 1
    if eal.does_directory_exist(NEU): eal.delete_directory(NEU)
    eal.save_directory(ROOT)
    log("uebertragen=%d" % done)  # nur dieser Lauf, nur mit Inhalt
    if empty: return "Übertragung auf die Engine-Figur ergab leere Animation(en): " + ", ".join(empty)
    return ""

try:
    err = run()
    unreal.log(("TRENDO-MIXAMO-FEHLER " + err) if err else "TRENDO-MIXAMO-OK")
except Exception:
    unreal.log("TRENDO-MIXAMO-FEHLER " + traceback.format_exc().strip().splitlines()[-1][:300])
""";
}
