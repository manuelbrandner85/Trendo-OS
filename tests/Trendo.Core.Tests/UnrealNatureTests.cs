using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Fotoreale Natur für Unreal: Dateiplan aus der Poly-Haven-API (2k, glTF mit verlinkten Dateien, Blatt-Alpha-Karten), Laden mit Größenprüfung,
/// Kopieren ins Projekt, Welt-Skript und Plugins im Projekt, Auswertung des Einrichtungslaufs.</summary>
public class UnrealNatureTests
{
    static JsonObject Files(string id)
    {
        string U(string f) => $"https://dl.polyhaven.org/file/ph-assets/{id}/{f}";
        if (UnrealNature.Grounds.Contains(id))
            return new JsonObject { ["Diffuse"] = M(U($"{id}_diff_2k.jpg"), 3), ["nor_dx"] = M(U("n.jpg"), 3), ["Rough"] = M(U("r.jpg"), 3), ["AO"] = M(U("ao.jpg"), 3), ["Displacement"] = M(U("d.jpg"), 3) };
        var o = new JsonObject
        {
            ["gltf"] = new JsonObject { ["2k"] = new JsonObject { ["gltf"] = new JsonObject { ["url"] = U($"{id}_2k.gltf"), ["size"] = 2, ["include"] = new JsonObject { [$"textures/{id}_diff_2k.jpg"] = new JsonObject { ["url"] = U("diff.jpg"), ["size"] = 4 }, [$"{id}.bin"] = new JsonObject { ["url"] = U("bin"), ["size"] = 5 } } } } },
        };
        if (id == "tree_small_02") o["leaves_alpha"] = new JsonObject { ["2k"] = new JsonObject { ["png"] = new JsonObject { ["url"] = U("tree_small_02_leaves_alpha_2k.png"), ["size"] = 6 } } };
        return o;
        static JsonObject M(string url, long size) => new() { ["2k"] = new JsonObject { ["jpg"] = new JsonObject { ["url"] = url, ["size"] = size } } };
    }

    [Fact(DisplayName = "Plan: Bodenkarten (4 je Boden), glTF mit verlinkten Dateien, Blatt-Alpha-Karte zusätzlich unter textures/")]
    public void Plan()
    {
        var p = UnrealNature.Plan(Files);
        Assert.Equal(UnrealNature.Grounds.Length * 4 + UnrealNature.Models.Length * 3 + 1, p.Count);
        Assert.Contains(p, x => x.Asset == "leafy_grass" && x.Rel == "leafy_grass_Diffuse_2k.jpg");
        Assert.DoesNotContain(p, x => x.Rel.Contains("Displacement"));
        Assert.Contains(p, x => x.Asset == "tree_small_02" && x.Rel == "textures/tree_small_02_leaves_alpha_2k.png" && x.Size == 6);
        Assert.Contains(p, x => x.Asset == "fern_02" && x.Rel == "fern_02.bin");
    }

    [Fact(DisplayName = "Laden: nur Fehlendes, Größe geprüft, Lizenzdatei; zweiter Lauf lädt nichts; falsche Größe → Fehler ohne Datei")]
    public void Fetch_and_copy()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-natur-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = Path.Combine(root, "cache"); var urls = new List<string>();
            void Dl(string url, string dest) { urls.Add(url); var s = UnrealNature.Plan(Files).First(x => x.Url == url).Size; File.WriteAllBytes(dest, new byte[s]); }
            var n = UnrealNature.Fetch(cache, Files, Dl);
            Assert.Equal(UnrealNature.Plan(Files).Count, n);
            Assert.Contains("CC0", File.ReadAllText(Path.Combine(cache, "LIZENZ.txt")));
            Assert.True(File.Exists(Path.Combine(cache, "tree_small_02", "textures", "tree_small_02_leaves_alpha_2k.png")));
            urls.Clear(); Assert.Equal(0, UnrealNature.Fetch(cache, Files, Dl)); Assert.Empty(urls);
            File.Delete(Path.Combine(cache, "boulder_01", "boulder_01.bin"));
            Assert.Throws<IOException>(() => UnrealNature.Fetch(cache, Files, (u, d) => File.WriteAllBytes(d, new byte[1])));
            Assert.False(File.Exists(Path.Combine(cache, "boulder_01", "boulder_01.bin")));
            UnrealNature.Fetch(cache, Files, Dl);
            var proj = Path.Combine(root, "Spiel");
            Assert.True(UnrealNature.CopyTo(cache, proj) > 0);
            Assert.True(File.Exists(Path.Combine(proj, "Content", "Trendo", "Quellen", "fern_02", "fern_02_2k.gltf")));
            Assert.Equal(0, UnrealNature.CopyTo(cache, proj));
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Unsichere Pfade aus der Antwort werden abgelehnt")]
    public void Rejects_bad_paths()
    {
        JsonObject Bad(string id) => UnrealNature.Grounds.Contains(id) ? Files(id) : new JsonObject { ["gltf"] = new JsonObject { ["2k"] = new JsonObject { ["gltf"] = new JsonObject { ["url"] = "https://dl.polyhaven.org/x.gltf", ["size"] = 1, ["include"] = new JsonObject { ["../../böse.txt"] = new JsonObject { ["url"] = "https://dl.polyhaven.org/b", ["size"] = 1 } } } } } };
        var root = Path.Combine(Path.GetTempPath(), "trendo-natur-" + Guid.NewGuid().ToString("N"));
        try { Assert.Throws<InvalidOperationException>(() => UnrealNature.Fetch(root, Bad, (u, d) => File.WriteAllBytes(d, new byte[1]))); }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Welt-Lauf: Ergebnis, Einzelzeilen und Python-Fehler; Karte wird umgestellt")]
    public void Welt_parse_and_map()
    {
        var (ok, n, info) = UnrealGame.ParseWelt("[x]LogPython: TRENDO-WELT Bodenmaterial: /Game/Trendo/Natur/M_TrendoBoden\n[x]LogPython: TRENDO-WELT-OK 39 Pflanzen/Felsen-Modelle\n");
        Assert.True(ok); Assert.Equal(39, n); Assert.Contains(info, l => l.StartsWith("Bodenmaterial"));
        var bad = UnrealGame.ParseWelt("LogPython: Error: Traceback\nLogPython: Error: AttributeError: kaputt\n");
        Assert.False(bad.Ok); Assert.Contains("Fehler: AttributeError: kaputt", bad.Lines);
        var dir = Path.Combine(Path.GetTempPath(), "trendo-welt-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Config"));
            File.WriteAllText(Path.Combine(dir, "Config", "DefaultEngine.ini"), "[/Script/EngineSettings.GameMapsSettings]\r\nGameDefaultMap=/Engine/Maps/Templates/OpenWorld\r\nEditorStartupMap=/Engine/Maps/Templates/OpenWorld\r\n");
            UnrealGame.UseWeltMap(dir);
            var ini = File.ReadAllText(Path.Combine(dir, "Config", "DefaultEngine.ini"));
            Assert.Contains("GameDefaultMap=/Game/Trendo/Welt", ini); Assert.Contains("EditorStartupMap=/Game/Trendo/Welt", ini); Assert.DoesNotContain("Templates/OpenWorld", ini);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Welt-Skript: Nanite mit Flächenerhalt für Pflanzen, eigene Materialien mit Instanz-Nutzung, Alpha-Karten, Fernmischung; Befehl mit -nullrhi")]
    public void Welt_script()
    {
        var s = UnrealGame.WeltScript;
        Assert.Contains("PRESERVE_AREA", s); Assert.Contains("MATUSAGE_INSTANCED_STATIC_MESHES", s); Assert.Contains("MSM_TWO_SIDED_FOLIAGE", s);
        Assert.Contains("_alpha_2k.png", s); Assert.Contains("PixelDepth", s); Assert.Contains("TRENDO-WELT-OK", s);
        foreach (var (id, kind) in UnrealNature.Models) Assert.Contains($"\"{id}\": \"{kind}\"", s);
        foreach (var g in UnrealNature.Grounds) Assert.Contains($"\"{g}\"", s);
        var a = UnrealGame.WeltArgs(@"C:\Spiele\Wald\Wald.uproject", @"C:\Spiele\Wald\Trendo-Pruefung\welt.txt");
        Assert.Contains("-nullrhi", a); Assert.Contains(a, x => x.StartsWith("-ExecutePythonScript=") && x.EndsWith("trendo_welt.py"));
    }
}
