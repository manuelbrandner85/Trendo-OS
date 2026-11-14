using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0660: Mixamo im Spiele-Studio – Erkennen und Übernehmen (M2), Zuordnung zu Spielaktionen (M5), Lizenzwächter (M6), Blender-Aufbereitung (M3, M4).</summary>
public class MixamoTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-mixamo-" + Guid.NewGuid().ToString("N"));
    public MixamoTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    // Nachgebaute FBX-Bytes (Binär-Muster „Name\0\x01Klasse“), keine echten Mixamo-Dateien
    string Fbx(string name, bool mesh, bool rig = true, bool anim = false)
    {
        var sb = new List<byte>(Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0"));
        sb.AddRange(new byte[3000]);
        if (rig) sb.AddRange(Encoding.ASCII.GetBytes("mixamorig:Hips\0\u0001Model"));
        else sb.AddRange(Encoding.ASCII.GetBytes("Bip01 Pelvis\0\u0001Model"));
        if (mesh) sb.AddRange(Encoding.ASCII.GetBytes("Body\0\u0001Geometry"));
        if (anim) sb.AddRange(Encoding.ASCII.GetBytes("\0\u0001AnimCurve"));
        var f = Path.Combine(_dir, name); File.WriteAllBytes(f, [.. sb]); return f;
    }

    [Fact(DisplayName = "M2: Mixamo-Skelett am Inhalt erkannt; Animation „With Skin“ (Mesh und Kurven) ist Animation, Figur bleibt Figur; fremde Dateien nicht")]
    public void Detect()
    {
        var fig = Mixamo.Detect(Fbx("Ch01.fbx", true)); Assert.Equal(new Mixamo.Info(true, true), fig);
        Assert.Equal("figur", Mixamo.KindOf("Ch01.fbx", fig));
        Assert.Equal("animation", Mixamo.KindOf("Walking.fbx", Mixamo.Detect(Fbx("Walking.fbx", false))));
        var withSkin = Mixamo.Detect(Fbx("Running.fbx", true, anim: true));
        Assert.True(withSkin.HasMesh && withSkin.HasAnim);
        Assert.Equal("animation", Mixamo.KindOf("Running.fbx", withSkin)); // Mixamos Standard „With Skin“
        Assert.Equal("figur", Mixamo.KindOf("Y Bot.fbx", Mixamo.Detect(Fbx("Y Bot.fbx", true, anim: true)))); // Figur-Download mit T-Haltung
        Assert.False(Mixamo.Detect(Fbx("andere.fbx", true, rig: false)).IsMixamo);
        File.WriteAllText(Path.Combine(_dir, "notiz.txt"), "mixamorig");
        Assert.False(Mixamo.Detect(Path.Combine(_dir, "notiz.txt")).IsMixamo); // nur 3D-Formate
    }

    [Fact(DisplayName = "M2: Art umschalten passt Zuordnung und gewählte Figur an; Pakete (ZIP) liefern ihre Mixamo-FBX")]
    public void Kind_and_zip()
    {
        var d = new JsonObject(); var assets = Path.Combine(_dir, "assets");
        var e = Mixamo.Take(d, assets, Fbx("Seltsam Walk.fbx", true))!; // Mesh ohne Kurven: Figur
        Assert.Equal("figur", (string?)e["kind"]); Assert.Equal((string?)e["file"], (string?)d["mixamoFigure"]);
        Mixamo.SetKind(d, (string)e["file"]!, "animation");
        Assert.Equal("animation", (string?)e["kind"]); Assert.Null(d["mixamoFigure"]); Assert.Equal((string?)e["file"], Mixamo.Assignments(d)["gehen"]);
        Mixamo.SetKind(d, (string)e["file"]!, "figur");
        Assert.Empty(Mixamo.Assignments(d));
        var zip = Path.Combine(_dir, "pack.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) { z.CreateEntryFromFile(Fbx("Jump.fbx", false), "pack/Jump.fbx"); z.CreateEntryFromFile(Fbx("x.fbx", true, rig: false), "pack/x.fbx"); z.CreateEntry("../../boese.fbx"); }
        var got = Mixamo.FromZip(zip, Path.Combine(_dir, "aus-zip"));
        Assert.Equal(["Jump.fbx"], got.Select(Path.GetFileName));
        Assert.All(got, g => Assert.StartsWith(Path.Combine(_dir, "aus-zip"), g));
    }

    [Fact(DisplayName = "M1: Im eingebauten Browser bleiben nur Mixamo und die Adobe-Anmeldung")]
    public void Inside()
    {
        Assert.True(Mixamo.Inside(new Uri("https://www.mixamo.com/#/")));
        Assert.True(Mixamo.Inside(new Uri("https://auth.services.adobe.com/de_DE/index.html")));
        Assert.False(Mixamo.Inside(new Uri("https://mixamo.com.evil.example/")));
        Assert.False(Mixamo.Inside(new Uri("http://www.mixamo.com/")));
        Assert.StartsWith("https://www.mixamo.com/#/?page=1&query=Falling%20Idle", Mixamo.SearchUrl("Falling Idle"));
    }

    [Fact(DisplayName = "M5: Aktion aus dem Dateinamen, jede Aktion mit Suchbegriffen")]
    public void Guess()
    {
        Assert.Equal("gehen", Mixamo.Guess("Walking.fbx"));
        Assert.Equal("laufen", Mixamo.Guess("Fast Run.fbx"));
        Assert.Equal("springen", Mixamo.Guess("Running Jump.fbx"));
        Assert.Equal("fallen", Mixamo.Guess("Falling Idle.fbx"));
        Assert.Equal("stehen", Mixamo.Guess("Breathing Idle.fbx"));
        Assert.Equal("getroffen", Mixamo.Guess("Hit Reaction.fbx"));
        Assert.Null(Mixamo.Guess("Ch01_nonPBR.fbx"));
        Assert.Equal(["stehen", "gehen", "laufen", "springen", "fallen", "getroffen"], Mixamo.Actions.Select(a => a.Id));
        Assert.All(Mixamo.Actions, a => Assert.NotEmpty(a.Search));
    }

    [Fact(DisplayName = "M2 + M5: Übernahme mit Quelle Mixamo und Lizenz; Animation wird ihrer Aktion zugeordnet; doppelt und fremd nicht")]
    public void Take()
    {
        var d = new JsonObject { ["title"] = "Test" }; var assets = Path.Combine(_dir, "assets");
        var fig = Mixamo.Take(d, assets, Fbx("Ch01_nonPBR.fbx", true))!;
        Assert.Equal("figur", (string?)fig["kind"]); Assert.Equal(Mixamo.License, (string?)fig["license"]); Assert.Equal("Mixamo", (string?)fig["source"]);
        Assert.Equal((string?)fig["file"], (string?)d["mixamoFigure"]);
        var walk = Mixamo.Take(d, assets, Fbx("Walking.fbx", false))!;
        Assert.Equal("animation", (string?)walk["kind"]); Assert.Equal("gehen", (string?)walk["action"]);
        Assert.Equal((string?)walk["file"], Mixamo.Assignments(d)["gehen"]);
        Assert.Null(Mixamo.Take(d, assets, Path.Combine(_dir, "Walking.fbx"))); // schon übernommen
        Assert.Null(Mixamo.Take(d, assets, Fbx("andere.fbx", true, rig: false)));
        Assert.Throws<ArgumentException>(() => Mixamo.Assign(d, "laufen", (string?)fig["file"])); // eine Figur ist keine Animation
        Mixamo.Assign(d, "laufen", (string?)walk["file"]); Assert.Equal(2, Mixamo.Assignments(d).Count);
        Mixamo.Assign(d, "laufen", null); Assert.Single(Mixamo.Assignments(d));
        Assert.Contains("1 Figur(en), 1 Animation(en), 1 von 6", Mixamo.Summary(d));
        Assert.Empty(GameAssets.Check(d, commercial: true).Where(w => w.Contains("Lizenz fehlt")));
    }

    [Fact(DisplayName = "M6: Lizenzwächter bricht Export, Asset-Paket und Weitergabe ab – auch in ZIP und umbenannt; fertiger Build ist erlaubt")]
    public void Guard()
    {
        var game = Path.Combine(_dir, "spiel"); Directory.CreateDirectory(Path.Combine(game, "modelle"));
        File.Copy(Fbx("Walking.fbx", false), Path.Combine(game, "modelle", "figur-lauf.bin.fbx"));
        File.WriteAllText(Path.Combine(game, "index.html"), "<html></html>");
        var e = Assert.Throws<MixamoLicenseException>(() => MixamoGuard.Ensure(MixamoGuard.Export, [game]));
        Assert.Contains("nur im fertigen Spiel", e.Message); Assert.Single(e.Files);
        Assert.Throws<MixamoLicenseException>(() => MixamoGuard.Ensure(MixamoGuard.Package, [game]));
        MixamoGuard.Ensure(MixamoGuard.Build, [game]); // fertiger Build: erlaubt
        var zip = Path.Combine(_dir, "paket.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) z.CreateEntryFromFile(Path.Combine(_dir, "Walking.fbx"), "anim/lauf.fbx");
        Assert.Contains("paket.zip › anim/lauf.fbx", Assert.Throws<MixamoLicenseException>(() => MixamoGuard.Ensure(MixamoGuard.Share, [zip])).Files);
        var clean = Path.Combine(_dir, "sauber"); Directory.CreateDirectory(clean); File.Copy(Fbx("andere.fbx", true, rig: false), Path.Combine(clean, "x.fbx"));
        MixamoGuard.Ensure(MixamoGuard.Share, [clean]);
    }

    [Fact(DisplayName = "M6: Wächter erkennt umbenannte Dateien (.bin), verschachtelte ZIPs; nicht prüfbares Archiv gilt als nicht freigegeben")]
    public void Guard_hard_cases()
    {
        var dir = Path.Combine(_dir, "weitergabe"); Directory.CreateDirectory(dir);
        File.Copy(Fbx("Walking.fbx", false), Path.Combine(dir, "daten.bin"));
        Assert.Contains("daten.bin", Assert.Throws<MixamoLicenseException>(() => MixamoGuard.Ensure(MixamoGuard.Share, [dir])).Message);
        File.Delete(Path.Combine(dir, "daten.bin"));
        var inner = Path.Combine(_dir, "innen.zip"); using (var z = ZipFile.Open(inner, ZipArchiveMode.Create)) z.CreateEntryFromFile(Path.Combine(_dir, "Walking.fbx"), "a.dat");
        var outer = Path.Combine(dir, "aussen.zip"); using (var z = ZipFile.Open(outer, ZipArchiveMode.Create)) z.CreateEntryFromFile(inner, "pakete/innen.zip");
        Assert.Contains("aussen.zip › pakete/innen.zip › a.dat", Assert.Throws<MixamoLicenseException>(() => MixamoGuard.Ensure(MixamoGuard.Package, [dir])).Files);
        File.Delete(outer);
        File.WriteAllBytes(Path.Combine(dir, "kaputt.zip"), [(byte)'P', (byte)'K', 3, 4, 1, 2, 3]);
        Assert.Contains("ließen sich nicht", Assert.Throws<MixamoLicenseException>(() => MixamoGuard.Ensure(MixamoGuard.Share, [dir])).Message);
    }

    [Fact(DisplayName = "M6: Browser-Paket und ZIP-Weitergabe des Spiels brechen mit Mixamo-Rohdatei ab")]
    public void Guard_in_pack_and_export()
    {
        var built = Path.Combine(_dir, "gebaut"); Directory.CreateDirectory(built);
        File.WriteAllText(Path.Combine(built, "index.html"), "<html></html>");
        File.Copy(Fbx("Jump.fbx", false), Path.Combine(built, "Jump.fbx"));
        var d = new JsonObject { ["title"] = "Test" };
        Assert.Throws<MixamoLicenseException>(() => GameRelease.Pack(d, built, Path.Combine(_dir, "out"), ["browser"]));
        Assert.Throws<MixamoLicenseException>(() => GameStudio.Export(built, Path.Combine(_dir, "export")));
        File.Delete(Path.Combine(built, "Jump.fbx"));
        Assert.NotEmpty(GameRelease.Pack(d, built, Path.Combine(_dir, "out"), ["browser"]).Files);
    }

    [Fact(DisplayName = "M6: Trendo liefert keine Mixamo-Dateien mit (Quelltext, Installer, Testdaten)")]
    public void No_mixamo_in_repo()
    {
        var root = AppContext.BaseDirectory; while (root is not null && !File.Exists(Path.Combine(root, "Trendo.sln"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var dirs = new[] { "src", "installer", "tests", "tools" }.Select(x => Path.Combine(root!, x)).Where(Directory.Exists)
            .Select(x => Directory.EnumerateFiles(x, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
            .SelectMany(x => x);
        Assert.Empty(MixamoGuard.Find(dirs.Where(f => Path.GetExtension(f).ToLowerInvariant() is ".fbx" or ".glb" or ".gltf" or ".zip" or ".bvh" or ".dae")));
    }

    [Fact(DisplayName = "M4: Unreal-Protokoll – Erfolg mit Größe; Faktor-100-Fehler und Abbruch werden gemeldet")]
    public void Unreal_log()
    {
        const string ok = "LogPython: TRENDO-MIXAMO hoehe_cm=177\nLogPython: TRENDO-MIXAMO importiert=1\nLogPython: TRENDO-MIXAMO uebertragen=1\nLogPython: TRENDO-MIXAMO-OK";
        Assert.Equal((true, 1, 1, ""), MixamoUnreal.Parse(ok)); Assert.Equal(177, MixamoUnreal.HeightCm(ok));
        Assert.False(MixamoUnreal.Parse(ok.Replace("=177", "=17700")).Ok);
        Assert.Contains("Figur ließ sich nicht", MixamoUnreal.Parse("LogPython: TRENDO-MIXAMO-FEHLER Figur ließ sich nicht importieren (x.fbx)").Error);
        Assert.Contains("Kein Bericht", MixamoUnreal.Parse("").Error);
        var proj = Path.Combine(_dir, "Spiel"); Directory.CreateDirectory(proj);
        Assert.Equal(1, MixamoUnreal.Stage(proj, [Fbx("gehen.fbx", true), Path.Combine(_dir, "gibtsnicht.fbx")]));
        Assert.True(File.Exists(Path.Combine(proj, "Trendo", "trendo_mixamo.py")));
        Assert.Contains("IKRetargeter", File.ReadAllText(Path.Combine(proj, "Trendo", "trendo_mixamo.py")));
    }

    // ───────────── Blender (echt, mit selbst gebauter Testfigur) ─────────────
    static string? BlenderExe() => Cli.Find("blender") ?? (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation"))
        ? Directory.EnumerateDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation")).Select(d => Path.Combine(d, "blender.exe")).FirstOrDefault(File.Exists) : null);
    public sealed class BlenderFactAttribute : FactAttribute { public BlenderFactAttribute() { if (BlenderExe() is null) Skip = "Übersprungen – Blender ist nicht installiert."; } }

    string Rig()
    {
        var root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "Trendo.sln"))) root = Path.GetDirectoryName(root)!;
        var r = Cli.Run(BlenderExe()!, ["-b", "--factory-startup", "--python", Path.Combine(root, "tests", "fixtures", "mixamo", "testrig.py"), "--", _dir], _dir, 300_000);
        Assert.True(File.Exists(Path.Combine(_dir, "walking.fbx")), "Testfigur nicht gebaut: " + r.Err);
        return _dir;
    }

    [BlenderFact(DisplayName = "M3: Blender bereitet die eigene Figur für den Auto-Rigger vor (ohne Skelett, Füße auf dem Boden, FBX)")]
    public void Rigger()
    {
        var dir = Rig();
        var r = MixamoBlender.Rigger(BlenderExe()!, Path.Combine(dir, "modell.glb"), Path.Combine(dir, "upload", "Test-fuer-mixamo.fbx"));
        Assert.True(File.Exists(r.File)); Assert.Equal(1, r.Meshes);
        Assert.InRange(r.Height, 1.6, 1.9);
        Assert.False(Mixamo.Detect(r.File).IsMixamo); // ohne Skelett
    }

    [BlenderFact(DisplayName = "M4: Blender gleicht Namen an, setzt Meter-Maßstab, Laufen auf der Stelle, überträgt auf die Figur und exportiert FBX und GLB")]
    public void Prep_in_place()
    {
        var dir = Rig();
        var r = MixamoBlender.Prep(BlenderExe()!, Path.Combine(dir, "figur.fbx"), new Dictionary<string, string> { ["gehen"] = Path.Combine(dir, "walking.fbx") }, Path.Combine(dir, "fertig"), MixamoBlender.InPlace);
        var a = Assert.Single(r.Animations);
        Assert.InRange(r.FigureHeight, 1.6, 1.9);
        Assert.InRange(a.Travel, 1.9, 2.1); // die Vorlage läuft 2 m
        Assert.True(a.Drift < 0.01, $"bewegt sich noch {a.Drift} m");
        Assert.Equal(a.Bones, a.Matched); // alle Knochen gleich benannt (auch der neue Wurzelknochen)
        Assert.Empty(MixamoBlender.Check(r));
        Assert.True(File.Exists(a.Fbx)); Assert.True(File.Exists(r.Glb));
        Assert.True(Mixamo.Detect(a.Fbx).IsMixamo); // Namen bleiben „mixamorig:“ – der Lizenzwächter erkennt auch die Aufbereitung
        var root = MixamoBlender.Prep(BlenderExe()!, "", new Dictionary<string, string> { ["gehen"] = Path.Combine(dir, "walking.fbx") }, Path.Combine(dir, "fertig-wurzel"), MixamoBlender.RootMotion);
        Assert.Equal(MixamoBlender.RootMotion, root.Motion);
        Assert.InRange(root.Animations[0].Height, 1.6, 1.9);
    }

    static List<string> GlbAnimations(string glb)
    {
        var b = File.ReadAllBytes(glb); var len = BitConverter.ToInt32(b, 12);
        var json = JsonNode.Parse(Encoding.UTF8.GetString(b, 20, len))!;
        return [.. (json["animations"] as JsonArray ?? []).Select(a => (string?)a?["name"] ?? "")];
    }

    [BlenderFact(DisplayName = "M2 + M4: echte Testdateien richtig eingestuft; zwei Animationen mit und ohne Figur heißen in der GLB genau wie die Spielaktionen")]
    public void Prep_two_actions()
    {
        var dir = Rig();
        var walk = Path.Combine(dir, "walking.fbx"); var run = Path.Combine(dir, "running.fbx"); File.Copy(walk, run);
        Assert.Equal("animation", Mixamo.KindOf(walk, Mixamo.Detect(walk))); // „With Skin“: Mesh und Kurven
        Assert.Equal("figur", Mixamo.KindOf(Path.Combine(dir, "figur.fbx"), Mixamo.Detect(Path.Combine(dir, "figur.fbx"))));
        var anims = new Dictionary<string, string> { ["gehen"] = walk, ["laufen"] = run };
        var withFig = MixamoBlender.Prep(BlenderExe()!, Path.Combine(dir, "figur.fbx"), anims, Path.Combine(dir, "mit"), MixamoBlender.InPlace);
        Assert.Equal(["gehen", "laufen"], GlbAnimations(withFig.Glb).Order());
        Assert.All(withFig.Animations, a => Assert.True(a.Drift < 0.01));
        var without = MixamoBlender.Prep(BlenderExe()!, "", anims, Path.Combine(dir, "ohne"), MixamoBlender.InPlace);
        Assert.Equal(["gehen", "laufen"], GlbAnimations(without.Glb).Order());
        Assert.Empty(MixamoBlender.Check(without));
    }
}
