using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0660: Mixamo unter „Figuren“ – Browser öffnen, Downloads übernehmen, Aktionen zuordnen, Blender, Godot, eigene Figur zum Auto-Rigger.</summary>
public class Mixamo_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static string Fbx(string dir, string name, bool mesh, bool anim = false)
    {
        var b = new List<byte>(Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0")); b.AddRange(new byte[500]);
        b.AddRange(Encoding.ASCII.GetBytes("mixamorig:Hips\0\u0001Model"));
        if (mesh) b.AddRange(Encoding.ASCII.GetBytes("Body\0\u0001Geometry"));
        if (anim) b.AddRange(Encoding.ASCII.GetBytes("\0\u0001AnimCurve"));
        var f = Path.Combine(dir, name); File.WriteAllBytes(f, [.. b]); return f;
    }

    [AvaloniaFact(DisplayName = "M1–M5: Mixamo öffnen, neue Downloads übernehmen (ohne Doppelte), Aktionen zuordnen, Blender aufbereiten, nach Godot")]
    public async Task Mixamo_flow()
    {
        var (w, _, _) = Ui.Open();
        var root = Path.Combine(Path.GetTempPath(), "trendo-mx-ui-" + Guid.NewGuid().ToString("N"));
        var dl = Path.Combine(root, "Downloads"); Directory.CreateDirectory(dl); w.DownloadsOverride = dl;
        try
        {
            var d = w.ProgramNew(GK, "topdown");
            w.GameSection = "Figuren"; w.Show(GK); Pump();
            Assert.Contains("0 Figur(en), 0 Animation(en)", w.Find<StackPanel>("game-mixamo").Children.OfType<TextBlock>().Select(t => t.Text).First(t => t!.Contains("Figur(en)")));
            w.Find<Button>("game-mixamo-open").Press(); Pump();
            Assert.Equal("Testmodus: mixamo.com wird nicht geladen.", w.Find<TextBlock>("game-mixamo-web").Text);
            // M2: neue Dateien im Download-Ordner
            Fbx(dl, "Y Bot.fbx", true); Fbx(dl, "Walking.fbx", true, anim: true); // Mixamos Standard „With Skin“
            var pack = Path.Combine(dl, "Jump Pack.zip");
            using (var z = System.IO.Compression.ZipFile.Open(pack, System.IO.Compression.ZipArchiveMode.Create)) z.CreateEntryFromFile(Fbx(root, "Jumping.fbx", false), "Jumping.fbx");
            File.WriteAllText(Path.Combine(dl, "fremd.fbx"), "kein Mixamo");
            Assert.Equal(3, (await w.MixamoScan(d)).Count);
            Assert.Empty(await w.MixamoScan(d)); // nichts doppelt, auch das Paket nicht
            Assert.Equal(3, Mixamo.Assets(d.Data).Count);
            Assert.Equal("animation", (string?)Mixamo.Assets(d.Data).First(a => ((string)a["file"]!).EndsWith("Walking.fbx"))["kind"]);
            // gleichnamige neue Datei: eigener Name, alte bleibt
            Fbx(dl, "Walking.fbx", false); File.SetLastWriteTime(Path.Combine(dl, "Walking.fbx"), DateTime.Now);
            Assert.Equal(["Walking-2.fbx"], await w.MixamoScan(d));
            Assert.Empty(await w.MixamoScan(d));
            d.Edit("weg", x => { var a = (System.Text.Json.Nodes.JsonArray)x["assets"]!; a.Remove(a.OfType<JsonObject>().First(o => ((string)o["file"]!).EndsWith("Walking-2.fbx"))); });
            Assert.All(Mixamo.Assets(d.Data), a => Assert.Equal(Mixamo.License, (string?)a["license"]));
            Assert.Equal(["gehen", "springen"], Mixamo.Assignments(d.Data).Keys.Order());
            w.Show(GK); Pump();
            Assert.Equal("Walking.fbx", w.Find<ComboBox>("game-mixamo-act-gehen").SelectedItem);
            // M5: per Auswahl umhängen
            w.Find<ComboBox>("game-mixamo-act-laufen").SelectedItem = "Walking.fbx"; Pump();
            Assert.EndsWith("Walking.fbx", Mixamo.Assignments(d.Data)["laufen"]);
            Assert.NotNull(w.Find<Button>("game-mixamo-act-fallen-s0"));
            Assert.NotNull(w.Find<Button>("game-mixamo-signout")); // Browser war offen
            Assert.NotNull(w.Find<StackPanel>("game-mixamo-files"));
            // M4: Blender (Testersatz)
            IReadOnlyDictionary<string, string>? asked = null; string? fig = null;
            w.MixamoPrepOverride = (figure, anims, outDir, motion) =>
            {
                asked = anims; fig = figure; Directory.CreateDirectory(outDir);
                var glb = Path.Combine(outDir, "mixamo-godot.glb"); File.WriteAllText(glb, "glTF");
                return new MixamoBlender.PrepReport(motion, 1.78, glb, [.. anims.Keys.Select(k => new MixamoBlender.AnimResult(k, k + ".fbx", 0, 0.01, 30, 30, 2.0, 0, 66, 66, 1, 0, Path.Combine(outDir, k + ".fbx")))]);
            };
            await w.MixamoPrep(d); Pump();
            Assert.Equal(3, asked!.Count); Assert.EndsWith("Y Bot.fbx", fig);
            Assert.StartsWith("✓ 3 Animation(en) aufbereitet, Figur 1,78 m", w.Find<TextBlock>("game-mixamo-msg").Text);
            Assert.True(w.Find<Button>("game-mixamo-godot").IsEnabled);
            // Godot
            var godot = Path.Combine(root, "Spiel-Godot"); Directory.CreateDirectory(godot); d.Set("godotProject", godot);
            w.Find<Button>("game-mixamo-godot").Press(); Pump();
            Assert.True(File.Exists(Path.Combine(godot, "assets", "mixamo", "mixamo.glb")));
            // M3: eigene Figur (Testersatz)
            w.MixamoRiggerOverride = (src, outFbx) => { Directory.CreateDirectory(Path.GetDirectoryName(outFbx)!); File.WriteAllText(outFbx, "fbx"); return new MixamoBlender.RiggerReport(outFbx, 1, 12000, 1.74); };
            await w.MixamoRigger(d, Path.Combine(root, "figur.glb")); Pump();
            Assert.Contains("Upload Character", w.Find<TextBlock>("game-mixamo-msg").Text);
            // Unreal ohne Bau: klare Meldung
            await w.MixamoToUnreal(d); Pump();
            Assert.Contains("Erst „Bauen“", w.Find<TextBlock>("game-mixamo-msg").Text);
        }
        finally { w.DownloadsOverride = null; w.MixamoPrepOverride = null; w.MixamoRiggerOverride = null; w.Close(); try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
