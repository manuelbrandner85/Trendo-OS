using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0622 Z3: 3D-Baustein. Prüfung der Dateien, Export, echter Browser (WebGPU/WebGL/ohne 3D/Splat) und PC-Lauf mit echtem Modell über Blender.</summary>
public class WebDesignZ3
{
    static string Editor() => Path.Combine(WebDesignKi_Ui.Repo(), "src", "Trendo.App", "Assets", "webdesign");
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    /// <summary>Kleines GLB (Würfel 1 m, grau, rau) – reicht, um Laden, Schatten und Darstellung zu prüfen.</summary>
    public static byte[] BoxGlb(string? extraRequired = null)
    {
        var pos = new List<float>(); var nor = new List<float>(); var idx = new List<ushort>();
        float[][] faces = [[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]];
        foreach (var n in faces)
        {
            var (u, v) = n[0] != 0 ? (new[] { 0f, 1, 0 }, new[] { 0f, 0, 1 }) : n[1] != 0 ? (new[] { 1f, 0, 0 }, new[] { 0f, 0, 1 }) : (new[] { 1f, 0, 0 }, new[] { 0f, 1, 0 });
            var b = (ushort)(pos.Count / 3);
            foreach (var (su, sv) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                for (var k = 0; k < 3; k++) pos.Add(0.5f * n[k] + 0.5f * su * u[k] + 0.5f * sv * v[k]);
                nor.AddRange(n);
            }
            var flip = Vector3Cross(u, v).Zip(n).Sum(p => p.First * p.Second) < 0;
            idx.AddRange(flip ? [b, (ushort)(b + 2), (ushort)(b + 1), b, (ushort)(b + 3), (ushort)(b + 2)] : [b, (ushort)(b + 1), (ushort)(b + 2), b, (ushort)(b + 2), (ushort)(b + 3)]);
        }
        static float[] Vector3Cross(float[] a, float[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
        var bin = new MemoryStream(); var w = new BinaryWriter(bin);
        foreach (var f in pos) w.Write(f); var posLen = (int)bin.Length;
        foreach (var f in nor) w.Write(f); var norLen = (int)bin.Length - posLen;
        foreach (var i in idx) w.Write(i); var idxLen = (int)bin.Length - posLen - norLen;
        while (bin.Length % 4 != 0) w.Write((byte)0);
        var json = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0" },
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }), ["scene"] = 0,
            ["nodes"] = new JsonArray(new JsonObject { ["mesh"] = 0 }),
            ["meshes"] = new JsonArray(new JsonObject { ["primitives"] = new JsonArray(new JsonObject { ["attributes"] = new JsonObject { ["POSITION"] = 0, ["NORMAL"] = 1 }, ["indices"] = 2, ["material"] = 0 }) }),
            ["materials"] = new JsonArray(new JsonObject { ["pbrMetallicRoughness"] = new JsonObject { ["baseColorFactor"] = new JsonArray(0.45, 0.42, 0.38, 1.0), ["metallicFactor"] = 0.0, ["roughnessFactor"] = 0.6 } }),
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = bin.Length }),
            ["bufferViews"] = new JsonArray(new JsonObject { ["buffer"] = 0, ["byteOffset"] = 0, ["byteLength"] = posLen }, new JsonObject { ["buffer"] = 0, ["byteOffset"] = posLen, ["byteLength"] = norLen }, new JsonObject { ["buffer"] = 0, ["byteOffset"] = posLen + norLen, ["byteLength"] = idxLen }),
            ["accessors"] = new JsonArray(
                new JsonObject { ["bufferView"] = 0, ["componentType"] = 5126, ["count"] = pos.Count / 3, ["type"] = "VEC3", ["min"] = new JsonArray(-0.5, -0.5, -0.5), ["max"] = new JsonArray(0.5, 0.5, 0.5) },
                new JsonObject { ["bufferView"] = 1, ["componentType"] = 5126, ["count"] = nor.Count / 3, ["type"] = "VEC3" },
                new JsonObject { ["bufferView"] = 2, ["componentType"] = 5123, ["count"] = idx.Count, ["type"] = "SCALAR" }),
        };
        if (extraRequired is not null) { json["extensionsUsed"] = new JsonArray(extraRequired); json["extensionsRequired"] = new JsonArray(extraRequired); }
        var j = Encoding.UTF8.GetBytes(json.ToJsonString()); var jPad = (4 - j.Length % 4) % 4;
        var o = new MemoryStream(); var ow = new BinaryWriter(o);
        ow.Write(0x46546C67u); ow.Write(2u); ow.Write((uint)(12 + 8 + j.Length + jPad + 8 + bin.Length));
        ow.Write((uint)(j.Length + jPad)); ow.Write(0x4E4F534Au); ow.Write(j); for (var i = 0; i < jPad; i++) ow.Write((byte)0x20);
        ow.Write((uint)bin.Length); ow.Write(0x004E4942u); ow.Write(bin.ToArray());
        return o.ToArray();
    }

    /// <summary>Kleiner Gaussian Splat im .splat-Format (32 Byte je Punkt): Kugel aus farbigen Punkten.</summary>
    static byte[] Splat(int n = 4000)
    {
        var r = new Random(7); var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        for (var i = 0; i < n; i++)
        {
            double th = r.NextDouble() * Math.PI * 2, ph = Math.Acos(2 * r.NextDouble() - 1);
            w.Write((float)(Math.Sin(ph) * Math.Cos(th) * 0.5)); w.Write((float)(Math.Cos(ph) * 0.5 + 0.5)); w.Write((float)(Math.Sin(ph) * Math.Sin(th) * 0.5));
            w.Write(0.02f); w.Write(0.02f); w.Write(0.02f);
            w.Write((byte)(150 + r.Next(80))); w.Write((byte)(110 + r.Next(60))); w.Write((byte)(80 + r.Next(40))); w.Write((byte)230);
            w.Write((byte)255); w.Write((byte)128); w.Write((byte)128); w.Write((byte)128);
        }
        return ms.ToArray();
    }

    static string Page(string datei, string quelle, string standbild = "medien/bild/standbild.png", string umgebung = "studio", string hdri = "") =>
        new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject { ["title"] = "3D", ["description"] = "Modell" } }, ["content"] = new JsonArray(new JsonObject { ["type"] = "Modell3D", ["props"] = new JsonObject { ["id"] = "m", ["quelle"] = quelle, ["datei"] = datei, ["standbild"] = standbild, ["beschreibung"] = "Testmodell", ["hoehe"] = 480, ["brennweite"] = 50, ["winkel"] = 35, ["neigung"] = 18, ["umgebung"] = umgebung, ["hdri"] = hdri, ["sonne"] = 3, ["sonnenWinkel"] = -40, ["sonnenHoehe"] = 40, ["schatten"] = "ja", ["belichtung"] = 1, ["material"] = "modell", ["drehen"] = 0, ["bedienen"] = "ja" } }), ["zones"] = new JsonObject() }.ToJsonString();

    static string Projekt()
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-z3-" + Guid.NewGuid().ToString("N"))).FullName;
        var tmp = Directory.CreateDirectory(Path.Combine(d, "_quelle")).FullName;
        File.WriteAllBytes(Path.Combine(tmp, "wuerfel.glb"), BoxGlb()); File.WriteAllBytes(Path.Combine(tmp, "kugel.splat"), Splat()); File.WriteAllBytes(Path.Combine(tmp, "standbild.png"), Png);
        Assert.Null(WebDesignAssets.Import(d, Path.Combine(tmp, "wuerfel.glb"), "modell", "Würfel", "selbst erzeugt", "CC0-1.0").Error);
        Assert.Null(WebDesignAssets.Import(d, Path.Combine(tmp, "kugel.splat"), "splat", "Kugel", "selbst erzeugt", "CC0-1.0").Error);
        Assert.Null(WebDesignAssets.Import(d, Path.Combine(tmp, "standbild.png"), "bild", "Standbild", "selbst erzeugt", "CC0-1.0").Error);
        Directory.Delete(tmp, true);
        return d;
    }

    [Fact(DisplayName = "Z3: GLB wird geprüft (Draco/KTX2/externe Dateien abgelehnt); Export enthält Modell, Bündel und Standbild nur auf der Seite mit 3D")]
    public void Glb_check_and_export()
    {
        var d = Projekt(); try
        {
            Assert.Null(WebDesignModel.CheckGlb(Path.Combine(d, "medien", "modell", "wuerfel.glb")));
            var f = Path.Combine(d, "draco.glb"); File.WriteAllBytes(f, BoxGlb("KHR_draco_mesh_compression"));
            Assert.Contains("Draco", WebDesignModel.CheckGlb(f));
            File.WriteAllBytes(f, BoxGlb("KHR_texture_basisu")); Assert.Contains("KTX2", WebDesignModel.CheckGlb(f));
            File.WriteAllText(f, "kein glb"); Assert.NotNull(WebDesignModel.CheckGlb(f));
            Assert.Null(WebDesign.Save(d, Page("medien/modell/wuerfel.glb", "modell")));
            Assert.Equal("", JsonNode.Parse(WebDesign.Validate(Page("../geheim.glb", "modell")).Json!)!["content"]![0]!["props"]!["datei"]!.ToString()); // nur Medien des Projekts
            var r = Trendo.Core.WebDesignExport.ExportSite(d, Editor()); Assert.Null(r.Error);
            var html = File.ReadAllText(Path.Combine(r.Dir!, "index.html"));
            Assert.Contains("assets/drei.js", html); Assert.DoesNotContain("assets/splat.js", html);
            Assert.Contains("daten/dateien/", html);
            Assert.True(File.Exists(Path.Combine(r.Dir!, "medien", "bild", "standbild.png")));
            Assert.DoesNotContain(WebDesignSeo.Check(d), x => x.Ebene == "Fehler");
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Z3 (QA 1, 2, 4): Seite mit 3D lässt sich veröffentlichen; Editor bekommt Modelle zur Auswahl; Lizenzprüfung sieht Modell, Umgebung und Standbild")]
    public void Publish_list_and_license()
    {
        var d = Projekt(); try
        {
            Assert.Null(WebDesign.Save(d, Page("medien/modell/wuerfel.glb", "modell")));
            Assert.Null(WebDesignSite.Create(d, "splat", "Splat")); Assert.Null(WebDesign.Save(d, Page("medien/splat/kugel.splat", "splat")));
            Assert.Null(Trendo.Core.WebDesignExport.ExportSite(d, Editor()).Error);
            var plan = WebDesignPublish.Prepare(d, []);
            Assert.DoesNotContain(plan.Blockers, b => b.Contains("drei.js") || b.Contains("splat.js"));
            var list = WebDesignAssets.EditorList(d).Select(x => x!["art"]!.ToString()).ToList();
            Assert.Contains("modell", list); Assert.Contains("splat", list); Assert.Contains("bild", list);
            var used = WebDesignAssets.Used(JsonNode.Parse(Page("medien/modell/wuerfel.glb", "modell", umgebung: "hdri", hdri: "medien/umgebung/licht.hdr")), []);
            Assert.Contains("medien/modell/wuerfel.glb", used); Assert.Contains("medien/umgebung/licht.hdr", used); Assert.Contains("medien/bild/standbild.png", used);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Z3 (QA 9, Security 1, 6, 7): Netzwerkpfad, zu großes HDR, zu viele Splat-Punkte und abgeschnittenes GLB werden mit klarer Meldung abgelehnt")]
    public void Import_checks()
    {
        var d = Projekt(); var tmp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-z3q-" + Guid.NewGuid().ToString("N"))).FullName; try
        {
            Assert.Contains("Netzwerk", WebDesignAssets.Import(d, @"\\server\freigabe\licht.hdr", "umgebung", "Licht", "x", "CC0-1.0").Error);
            var hdr = Path.Combine(tmp, "gross.hdr"); File.WriteAllText(hdr, "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 30000 +X 30000\n");
            Assert.Contains("zu groß", WebDesignAssets.Import(d, hdr, "umgebung", "Licht", "x", "CC0-1.0").Error);
            File.WriteAllText(hdr, "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 1024 +X 2048\n"); Assert.Null(WebDesignModel.CheckHdr(hdr));
            var ply = Path.Combine(tmp, "viel.ply"); File.WriteAllText(ply, "ply\nformat binary_little_endian 1.0\nelement vertex 6000000\nproperty float x\nend_header\n");
            Assert.Contains("zu viele Punkte", WebDesignAssets.Import(d, ply, "splat", "Viel", "x", "CC0-1.0").Error);
            var glb = Path.Combine(tmp, "kurz.glb"); var bytes = BoxGlb(); File.WriteAllBytes(glb, bytes[..(bytes.Length - 40)]);
            Assert.Contains("unvollständig", WebDesignModel.CheckGlb(glb)); // keine Ausnahme
            var head = bytes[..20].ToArray(); BitConverter.GetBytes((uint)24).CopyTo(head, 8); BitConverter.GetBytes((uint)200).CopyTo(head, 12); File.WriteAllBytes(glb, [.. head, 0, 0, 0, 0]);
            Assert.NotNull(WebDesignModel.CheckGlb(glb));
        }
        finally { Directory.Delete(d, true); Directory.Delete(tmp, true); }
    }

    [AvaloniaFact(DisplayName = "K7 (QA 13): eigenes Supabase-Projekt in der Oberfläche verbinden; geheimer Schlüssel wird abgelehnt; SQL ohne Leseregel")]
    public void Supabase_ui()
    {
        var (w, _, _) = Ui.Open();
        Assert.Null(WebDesign.Save(w.WebDesignDir, """{"root":{"props":{"title":"Start","description":"Gästebuch der Seite"}},"content":[{"type":"Datenliste","props":{"id":"dl","titel":"G","tabelle":"gaeste","felder":[{"name":"name","beschriftung":"Name","art":"text","pflicht":"ja"}],"knopf":"Speichern","nurAngemeldet":"nein","liste":"alle"}}],"zones":{}}"""));
        w.WebDesignSupabaseSave("https://meinprojekt.supabase.co", "sb_secret_abcdefghijklmnopqrstuvwxyz123456");
        Assert.Null(WebDesignData.Load(w.WebDesignDir));
        w.WebDesignSupabaseSave("https://meinprojekt.supabase.co", "sb_publishable_abcdefghijklmnopqrstuvwxyz123456");
        Assert.NotNull(WebDesignData.Load(w.WebDesignDir));
        var sql = w.WebDesignSupabaseSqlText();
        Assert.Contains("public.gaeste", sql); Assert.DoesNotContain("for select", sql);
        w.WebDesignSupabaseRemove(); Assert.Null(WebDesignData.Load(w.WebDesignDir));
    }

    [Fact(DisplayName = "Z3 in echtem Chrome (Software-Grafik): Modell erscheint, ohne WebGPU läuft WebGL, ohne 3D erscheint das Standbild, Splat erscheint (übersprungen ohne Chrome/Node)")]
    public void Real_browser_3d()
    {
        if (WebsitePreview.FindBrowser() is not { } chrome || Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Chrome/Edge oder Node.js fehlt auf diesem PC."); return; }
        var d = Projekt(); try
        {
            Assert.Null(WebDesign.Save(d, Page("medien/modell/wuerfel.glb", "modell")));
            Assert.Null(WebDesignSite.Create(d, "splat", "Splat")); Assert.Null(WebDesign.Save(d, Page("medien/splat/kugel.splat", "splat")));
            var r = Trendo.Core.WebDesignExport.ExportSite(d, Editor()); Assert.Null(r.Error);
            var run = Cli.Run(node, [Path.Combine(WebDesignKi_Ui.Repo(), "tools", "webdesign-editor", "pruefe-3d.mjs"), chrome, r.Dir!, "", "software"], timeoutMs: 240_000);
            Assert.True(run.Ok, run.Out + run.Err);
        }
        finally { Directory.Delete(d, true); }
    }

    [AvaloniaFact(DisplayName = "Z3: Modell aus Blender-Format wird über die Umwandlung zu GLB, geprüft und mit Herkunft in die Medien gelegt")]
    public async Task Blender_import_path()
    {
        MainWindow.WebDesignGlbConvertOverride = (src, glb) => { File.WriteAllBytes(glb, BoxGlb()); return Task.CompletedTask; };
        var src = Path.Combine(Path.GetTempPath(), "trendo-z3-" + Guid.NewGuid().ToString("N") + ".blend"); File.WriteAllText(src, "BLENDER-v400");
        try
        {
            var (w, _, _) = Ui.Open();
            await w.WebDesignModelImport(src, "Stuhl", "", "CC0-1.0");
            var a = WebDesignAssets.Load(w.WebDesignDir).Single(x => x.Art == "modell");
            Assert.EndsWith(".glb", a.Datei); Assert.Equal("eigenes Blender-Modell", a.Herkunft);
            Assert.Contains("aus Blender umgewandelt", w.WebDesignNote);
            MainWindow.WebDesignGlbConvertOverride = (s, glb) => { File.WriteAllBytes(glb, BoxGlb("KHR_draco_mesh_compression")); return Task.CompletedTask; };
            await w.WebDesignModelImport(src, "Draco", "", "CC0-1.0");
            Assert.Contains("Draco", w.WebDesignNote);
            Assert.Single(WebDesignAssets.Load(w.WebDesignDir), x => x.Art == "modell");
        }
        finally { MainWindow.WebDesignGlbConvertOverride = null; File.Delete(src); }
    }

    [Fact(DisplayName = "Z3 PC-Lauf: echtes Modell (Poly Haven, CC0) über Blender nach GLB, HDR-Licht, Grafikkarte, Bilder und gemessene Bildrate unter reports/ (nur mit TRENDO_PC_LAUF=1)")]
    public async Task Z3_pc_run()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_PC_LAUF") != "1") { Assert.Skip("Übersprungen – nur mit TRENDO_PC_LAUF=1 (echte Grafikkarte, Blender, Quellen unter %TEMP%\\trendo-z3-quellen)."); return; }
        var quellen = Path.Combine(Path.GetTempPath(), "trendo-z3-quellen");
        var chrome = WebsitePreview.FindBrowser()!; var node = Cli.Find("node")!;
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-z3-pc-" + Guid.NewGuid().ToString("N"))).FullName;
        var glb = Path.Combine(d, "kamera.glb");
        await Trendo.Core.Bibliothek.ModelConverter.ToGlb(Path.Combine(quellen, "vintage_video_camera_2k.gltf"), glb); // echte Umwandlung in Blender
        Assert.Null(WebDesignModel.CheckGlb(glb));
        Assert.Null(WebDesignAssets.Import(d, glb, "modell", "Vintage Video Camera", "Poly Haven (Jenelle van Heerden, James Ray Cock)", "CC0-1.0", "", "https://polyhaven.com/a/vintage_video_camera").Error);
        Assert.Null(WebDesignAssets.Import(d, Path.Combine(quellen, "studio_small_09_2k.hdr"), "umgebung", "Studio Small 09", "Poly Haven", "CC0-1.0", "", "https://polyhaven.com/a/studio_small_09").Error);
        File.WriteAllBytes(Path.Combine(d, "s.png"), Png); Assert.Null(WebDesignAssets.Import(d, Path.Combine(d, "s.png"), "bild", "Standbild", "selbst", "CC0-1.0").Error);
        var modell = WebDesignAssets.Load(d).Single(a => a.Art == "modell").Datei; var hdr = WebDesignAssets.Load(d).Single(a => a.Art == "umgebung").Datei;
        Assert.Null(WebDesign.Save(d, Page(modell, "modell", "medien/bild/s.png", "hdri", hdr)));
        var tmp = Directory.CreateDirectory(Path.Combine(d, "_s")).FullName; File.WriteAllBytes(Path.Combine(tmp, "kugel.splat"), Splat(60000));
        Assert.Null(WebDesignAssets.Import(d, Path.Combine(tmp, "kugel.splat"), "splat", "Kugel", "selbst erzeugt", "CC0-1.0").Error);
        Assert.Null(WebDesignSite.Create(d, "splat", "Splat")); Assert.Null(WebDesign.Save(d, Page("medien/splat/kugel.splat", "splat")));
        var r = Trendo.Core.WebDesignExport.ExportSite(d, Editor()); Assert.Null(r.Error);
        var report = Path.Combine(WebDesignKi_Ui.Repo(), "reports", $"web-3d-pruefung-{DateTime.Now:yyyy-MM-dd}");
        var run = Cli.Run(node, [Path.Combine(WebDesignKi_Ui.Repo(), "tools", "webdesign-editor", "pruefe-3d.mjs"), chrome, r.Dir!, report, "gpu"], timeoutMs: 300_000);
        File.WriteAllText(Path.Combine(report, "messung.json"), run.Out);
        Assert.True(run.Ok, run.Out + run.Err);
    }
}
