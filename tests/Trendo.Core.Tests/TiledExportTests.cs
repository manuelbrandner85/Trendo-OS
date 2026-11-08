using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 7 – CORE-06: Trendo-Level als Tiled-Karte (.tmj). Echter Gegenlese-Test mit pytiled-parser in TiledReferenzTests.</summary>
public class TiledExportTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-tiled-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static readonly string[] Rows = ["S....o..E...Z", "...####...^..", "#############"];
    static readonly string[] Decor = ["ww......m....", "..t.....b..gg", "............."];

    [Fact(DisplayName = "Tiled +1: Karte 13 × 3 Kacheln à 32 px, drei Ebenen (Hintergrund, Boden, Objekte), Tileset eingebettet mit allen Arten")]
    public void Structure()
    {
        var m = TiledExport.Map(Rows, Decor, "Wiese");
        Assert.Equal((13, 3, 32, "orthogonal", "map"), (m["width"]!.GetValue<int>(), m["height"]!.GetValue<int>(), m["tilewidth"]!.GetValue<int>(), m["orientation"]!.GetValue<string>(), m["type"]!.GetValue<string>()));
        Assert.Equal(["Hintergrund", "Boden", "Objekte"], m["layers"]!.AsArray().Select(l => l!["name"]!.GetValue<string>()));
        var ts = m["tilesets"]![0]!; Assert.Equal(TiledExport.Kinds.Length, ts["tilecount"]!.GetValue<int>()); Assert.Equal(TiledExport.TilesetImage, ts["image"]!.GetValue<string>());
        Assert.Equal("Wiese", m["properties"]![0]!["value"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Tiled +2: Boden-Ebene hat genau die #-Kacheln (17), Hintergrund die Deko (7), Objekte 5 (Start, Münze, Gegner, Stacheln, Ziel)")]
    public void Layer_contents()
    {
        var m = TiledExport.Map(Rows, Decor, "Wiese"); var layers = m["layers"]!.AsArray();
        int Count(int i) => layers[i]!["data"]!.AsArray().Count(x => x!.GetValue<int>() > 0);
        Assert.Equal(7, Count(0)); Assert.Equal(17, Count(1));
        var objs = layers[2]!["objects"]!.AsArray().Select(o => o!["type"]!.GetValue<string>()).ToList();
        Assert.Equal(["Start", "Münze", "Gegner", "Ziel", "Stacheln"], objs);
    }

    [Fact(DisplayName = "Tiled +3: Kachel-Objekte stehen unten links ihrer Zelle (x = Spalte·32, y = (Zeile+1)·32) mit fortlaufender ID")]
    public void Object_positions()
    {
        var objs = TiledExport.Map(Rows, null, "x")["layers"]![2]!["objects"]!.AsArray();
        var enemy = objs.First(o => o!["type"]!.GetValue<string>() == "Gegner")!;
        Assert.Equal((8 * 32, 32), (enemy["x"]!.GetValue<int>(), enemy["y"]!.GetValue<int>()));
        Assert.Equal([1, 2, 3, 4, 5], objs.Select(o => o!["id"]!.GetValue<int>()));
    }

    [Fact(DisplayName = "Tiled +4: Eigenschaften gehen mit – Gegner mit 3 Treffern und Tempo 1,5, Figur mit Text")]
    public void Properties()
    {
        var m = TiledExport.Map(["S.E.N"], null, "x", new Dictionary<string, double> { ["2,0:hp"] = 3, ["2,0:speed"] = 1.5, ["9,9:hp"] = 1 }, new Dictionary<string, string> { ["4,0:say"] = "Hallo!" });
        var objs = m["layers"]![2]!["objects"]!.AsArray();
        var e = objs.First(o => o!["type"]!.GetValue<string>() == "Gegner")!["properties"]!.AsArray();
        Assert.Equal(3, e.First(p => p!["name"]!.GetValue<string>() == "hp")!["value"]!.GetValue<double>());
        Assert.Equal(1.5, e.First(p => p!["name"]!.GetValue<string>() == "speed")!["value"]!.GetValue<double>());
        Assert.Equal("Hallo!", objs.First(o => o!["type"]!.GetValue<string>() == "Figur")!["properties"]![0]!["value"]!.GetValue<string>());
        Assert.Null(objs.First(o => o!["type"]!.GetValue<string>() == "Start")!["properties"]);
    }

    [Fact(DisplayName = "Tiled +5: Rückweg – Karte wieder eingelesen ergibt dasselbe Level (Zeichen für Zeichen)")]
    public void Roundtrip()
    {
        var back = TiledExport.ToRows(TiledExport.Map(Rows, Decor, "x").ToJsonString());
        Assert.Equal(Rows.Select(r => r.TrimEnd('.')), back);
    }

    [Fact(DisplayName = "Tiled +6: Schreiben – je Level eine .tmj, Kachelbild als echtes PNG (576 × 32), Welt-Datei mit Level nebeneinander")]
    public void Write_files()
    {
        var files = TiledExport.Write(_dir, [new("Wiese", Rows, Decor), new("Höhle / Ende", ["S..Z", "####"], null)]);
        Assert.Equal(["01-Wiese.tmj", "02-Höhle---Ende.tmj", TiledExport.TilesetImage, "spiel.world"], files.Select(Path.GetFileName));
        var png = File.ReadAllBytes(Path.Combine(_dir, TiledExport.TilesetImage));
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, png[..4]);
        Assert.Equal(TiledExport.Kinds.Length * 32, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
        var world = JsonNode.Parse(File.ReadAllText(Path.Combine(_dir, "spiel.world")))!;
        Assert.Equal([0, (13 + 4) * 32], world["maps"]!.AsArray().Select(x => x!["x"]!.GetValue<int>()));
    }

    [Fact(DisplayName = "Tiled −1: leeres oder riesiges Level wird verständlich abgelehnt; unbekannte Zeichen bleiben leer")]
    public void Invalid()
    {
        Assert.Throws<ArgumentException>(() => TiledExport.Map([], null, "x"));
        Assert.Throws<ArgumentException>(() => TiledExport.Map([new string('#', 1001)], null, "x"));
        Assert.Throws<ArgumentException>(() => TiledExport.Write(_dir, []));
        var m = TiledExport.Map(["S?§#"], null, "x");
        Assert.Equal([0, 0, 0, 1], m["layers"]![1]!["data"]!.AsArray().Select(x => x!.GetValue<int>()));
    }
}

/// <summary>Echter Gegenlese-Test mit pytiled-parser (MIT, Tiled-Leser der Spielbibliothek Arcade) aus der Prüfumgebung.</summary>
public class TiledReferenzTests
{
    public sealed class PytiledFactAttribute : FactAttribute
    {
        public PytiledFactAttribute()
        {
            var py = EchtWerkzeuge.PruefPython();
            if (py is null || !Cli.Run(py, ["-I", "-c", "import pytiled_parser"], timeoutMs: 60000).Ok) Skip = "Übersprungen – pytiled-parser nicht in der Prüfumgebung.";
        }
    }

    const string Script = """
import json, pathlib, sys
import pytiled_parser
folder = pathlib.Path(sys.argv[1]); out = {}
for f in sorted(folder.glob("*.tmj")):
    m = pytiled_parser.parse_map(f); ts = m.tilesets[1]; layers = {}
    for layer in m.layers:
        if isinstance(layer, pytiled_parser.TileLayer): layers[layer.name] = sum(1 for row in layer.data for g in row if g)
        else: layers[layer.name] = [[o.class_, int(o.coordinates.x), int(o.coordinates.y), {k: float(v) for k, v in (o.properties or {}).items()}] for o in layer.tiled_objects]
    out[f.name] = {"w": int(m.map_size.width), "h": int(m.map_size.height), "tiles": ts.tile_count, "imgw": ts.image_width, "layers": layers}
w = pytiled_parser.parse_world(folder / "spiel.world")
out["world"] = [[m.map_file.name, int(m.coordinates.x)] for m in w.maps]
print(json.dumps(out))  # nur ASCII (Umlaute als Escapes): unabhängig von der Konsolen-Codepage
""";

    [PytiledFact(DisplayName = "Tiled echt: pytiled-parser liest Trendos Karten – Größe, Tileset, Ebenen, Objekte mit Eigenschaften und Welt stimmen")]
    public void Read_by_pytiled()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-tiledref-" + Guid.NewGuid().ToString("N"));
        try
        {
            TiledExport.Write(dir, [new("Wiese", ["S....o..E...Z", "...####...^..", "#############"], ["ww......m....", "..t.....b..gg"], new Dictionary<string, double> { ["8,0:hp"] = 3 }), new("Höhle", ["S.Z", "###"], null)]);
            var r = Cli.Run(EchtWerkzeuge.PruefPython()!, ["-I", "-c", Script, dir], timeoutMs: 120000);
            Assert.True(r.Ok, r.Err);
            var j = System.Text.Json.Nodes.JsonNode.Parse(r.Out)!;
            var w = j["01-Wiese.tmj"]!;
            Assert.Equal((13, 3, TiledExport.Kinds.Length, TiledExport.Kinds.Length * 32), (w["w"]!.GetValue<int>(), w["h"]!.GetValue<int>(), w["tiles"]!.GetValue<int>(), w["imgw"]!.GetValue<int>()));
            Assert.Equal(7, w["layers"]!["Hintergrund"]!.GetValue<int>()); Assert.Equal(17, w["layers"]!["Boden"]!.GetValue<int>());
            var objs = w["layers"]!["Objekte"]!.AsArray();
            Assert.Equal(["Start", "Münze", "Gegner", "Ziel", "Stacheln"], objs.Select(o => o![0]!.GetValue<string>()));
            var enemy = objs[2]!; Assert.Equal((256, 32), (enemy[1]!.GetValue<int>(), enemy[2]!.GetValue<int>())); Assert.Equal(3, enemy[3]!["hp"]!.GetValue<double>());
            Assert.Equal(["01-Wiese.tmj", "02-Höhle.tmj"], j["world"]!.AsArray().Select(m => m![0]!.GetValue<string>()));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
