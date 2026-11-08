using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>Alle Studios, Runde 7 – CORE-06: Trendo-Level als Tiled-Karte (.tmj, JSON-Format von Tiled 1.10+).
/// Das Kartenformat ist offen dokumentiert (libtiled BSD-2); Tiled selbst (GPL) wird nicht eingebaut. Godot (über Importer wie YATI),
/// Phaser, Defold, LÖVE (STI) und Tiled lesen die Datei. Ebenen: „Hintergrund“ (Deko-Kacheln), „Boden“ (feste Kacheln),
/// „Objekte“ (Spielfigur, Münzen, Gegner … als Kachel-Objekte mit Art und Eigenschaften).</summary>
public static class TiledExport
{
    public const int Tile = 32;
    public const string TilesetImage = "trendo-kacheln.png";

    /// <summary>Alle Kachelarten: Zeichen im Trendo-Level, Name (Tiled „type“), Farbe, Ebene (Hintergrund/Boden/Objekt).</summary>
    public static readonly (char Ch, string Name, string Color, string Layer)[] Kinds =
    [
        ('#', "Boden", "#596680", "Boden"), ('D', "Tür", "#8C5933", "Boden"),
        ('S', "Start", "#2F6FEB", "Objekte"), ('Z', "Ziel", "#2DA44E", "Objekte"), ('o', "Münze", "#F5C518", "Objekte"), ('E', "Gegner", "#CC3399", "Objekte"),
        ('^', "Stacheln", "#E5484D", "Objekte"), ('F', "Sprungfeder", "#FF9919", "Objekte"), ('C', "Speicherpunkt", "#19D3DA", "Objekte"), ('K', "Schlüssel", "#FFD933", "Objekte"),
        ('G', "Gegenstand", "#A855F7", "Objekte"), ('N', "Figur", "#14B8A6", "Objekte"), ('V', "Fahrzeug", "#FA7316", "Objekte"),
        ('w', "Wolke", "#E6EEF7", "Hintergrund"), ('m', "Berg", "#7A8699", "Hintergrund"), ('t', "Baum", "#2E7D32", "Hintergrund"), ('b', "Busch", "#4CAF50", "Hintergrund"), ('g', "Gras", "#8BC34A", "Hintergrund"),
    ];

    static int Gid(char c) => Array.FindIndex(Kinds, k => k.Ch == c) + 1; // 0 = leer; firstgid 1

    /// <summary>Kachelbild: eine Zeile mit je 32×32 Pixeln pro Art (Farbfläche mit dunklerem Rand, damit Kacheln im Editor unterscheidbar sind).</summary>
    public static byte[] TilesetPng()
    {
        int w = Kinds.Length * Tile, h = Tile; var rgb = new byte[w * h * 3];
        for (var k = 0; k < Kinds.Length; k++)
        {
            var c = Kinds[k].Color; byte r = Convert.ToByte(c[1..3], 16), g = Convert.ToByte(c[3..5], 16), b = Convert.ToByte(c[5..7], 16);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < Tile; x++)
                {
                    var edge = x < 2 || y < 2 || x >= Tile - 2 || y >= Tile - 2; var f = edge ? 0.6 : 1.0;
                    var i = (y * w + k * Tile + x) * 3; rgb[i] = (byte)(r * f); rgb[i + 1] = (byte)(g * f); rgb[i + 2] = (byte)(b * f);
                }
        }
        return Mesh3D.Png(w, h, rgb);
    }

    /// <summary>Eine Karte bauen. <paramref name="props"/>: Objekt-Eigenschaften „x,y:name“ → Wert (z. B. „3,4:hp“ = 2) dieses Levels.</summary>
    public static JsonObject Map(IReadOnlyList<string> rows, IReadOnlyList<string>? decor, string levelName, IReadOnlyDictionary<string, double>? props = null, IReadOnlyDictionary<string, string>? texts = null)
    {
        if (rows.Count == 0 || rows.All(r => r.Length == 0)) throw new ArgumentException("Das Level ist leer.");
        int h = rows.Count, w = rows.Max(r => r.Length);
        if (w > 1000 || h > 1000) throw new ArgumentException("Das Level ist zu groß (höchstens 1000 × 1000 Kacheln).");
        char At(IReadOnlyList<string>? l, int x, int y) => l is not null && y < l.Count && x < l[y].Length ? l[y][x] : '.';
        JsonArray Data(Func<int, int, char> pick, string layer)
        {
            var a = new JsonArray();
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) { var c = pick(x, y); var gid = Gid(c); a.Add(gid > 0 && Kinds[gid - 1].Layer == layer ? gid : 0); }
            return a;
        }
        JsonObject TileLayer(int id, string name, JsonArray data) => new()
        { ["id"] = id, ["name"] = name, ["type"] = "tilelayer", ["width"] = w, ["height"] = h, ["x"] = 0, ["y"] = 0, ["opacity"] = 1, ["visible"] = true, ["data"] = data };

        var objects = new JsonArray(); var nextId = 1;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var c = At(rows, x, y); var gid = Gid(c); if (gid == 0 || Kinds[gid - 1].Layer != "Objekte") continue;
                var pl = new JsonArray();
                foreach (var (k, v) in props ?? new Dictionary<string, double>())
                    if (k.StartsWith($"{x},{y}:", StringComparison.Ordinal)) pl.Add(new JsonObject { ["name"] = k[(k.IndexOf(':') + 1)..], ["type"] = "float", ["value"] = v });
                foreach (var (k, v) in texts ?? new Dictionary<string, string>())
                    if (k.StartsWith($"{x},{y}:", StringComparison.Ordinal)) pl.Add(new JsonObject { ["name"] = k[(k.IndexOf(':') + 1)..], ["type"] = "string", ["value"] = v });
                var o = new JsonObject
                {
                    ["id"] = nextId++, ["name"] = Kinds[gid - 1].Name, ["type"] = Kinds[gid - 1].Name, ["gid"] = gid,
                    ["x"] = x * Tile, ["y"] = (y + 1) * Tile, ["width"] = Tile, ["height"] = Tile, ["rotation"] = 0, ["visible"] = true, // Kachel-Objekte: Bezugspunkt unten links
                };
                if (pl.Count > 0) o["properties"] = pl;
                objects.Add(o);
            }

        var tiles = new JsonArray(); for (var i = 0; i < Kinds.Length; i++) tiles.Add(new JsonObject { ["id"] = i, ["type"] = Kinds[i].Name });
        return new JsonObject
        {
            ["compressionlevel"] = -1, ["height"] = h, ["width"] = w, ["infinite"] = false, ["orientation"] = "orthogonal", ["renderorder"] = "right-down",
            ["tiledversion"] = "1.11.2", ["version"] = "1.10", ["type"] = "map", ["tilewidth"] = Tile, ["tileheight"] = Tile, ["nextlayerid"] = 4, ["nextobjectid"] = nextId,
            ["properties"] = new JsonArray { new JsonObject { ["name"] = "trendo-level", ["type"] = "string", ["value"] = levelName } },
            ["layers"] = new JsonArray
            {
                TileLayer(1, "Hintergrund", Data((x, y) => At(decor, x, y), "Hintergrund")),
                TileLayer(2, "Boden", Data((x, y) => At(rows, x, y), "Boden")),
                new JsonObject { ["id"] = 3, ["name"] = "Objekte", ["type"] = "objectgroup", ["draworder"] = "topdown", ["opacity"] = 1, ["visible"] = true, ["x"] = 0, ["y"] = 0, ["objects"] = objects },
            },
            ["tilesets"] = new JsonArray
            {
                new JsonObject
                {
                    ["firstgid"] = 1, ["name"] = "Trendo", ["image"] = TilesetImage, ["imagewidth"] = Kinds.Length * Tile, ["imageheight"] = Tile,
                    ["tilewidth"] = Tile, ["tileheight"] = Tile, ["tilecount"] = Kinds.Length, ["columns"] = Kinds.Length, ["margin"] = 0, ["spacing"] = 0, ["tiles"] = tiles,
                },
            },
        };
    }

    public sealed record Level(string Name, IReadOnlyList<string> Rows, IReadOnlyList<string>? Decor, IReadOnlyDictionary<string, double>? Props = null, IReadOnlyDictionary<string, string>? Texts = null);

    static string Safe(string s) { var n = string.Concat(s.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')).Trim('-'); return n.Length == 0 ? "level" : n; }

    /// <summary>Alle Level in einen Ordner schreiben: je Level „NN-name.tmj“, das Kachelbild und „spiel.world“ (Level nebeneinander, Tiled-Welt).
    /// Rückgabe: geschriebene Dateien.</summary>
    public static IReadOnlyList<string> Write(string folder, IReadOnlyList<Level> levels)
    {
        if (levels.Count == 0) throw new ArgumentException("Keine Level.");
        Directory.CreateDirectory(folder);
        var files = new List<string>(); var maps = new JsonArray(); var offset = 0;
        var opt = new JsonSerializerOptions { WriteIndented = true };
        for (var i = 0; i < levels.Count; i++)
        {
            var l = levels[i]; var map = Map(l.Rows, l.Decor, l.Name, l.Props, l.Texts);
            var name = $"{i + 1:00}-{Safe(l.Name)}.tmj"; var f = Path.Combine(folder, name);
            File.WriteAllText(f, map.ToJsonString(opt)); files.Add(f);
            maps.Add(new JsonObject { ["fileName"] = name, ["x"] = offset, ["y"] = 0, ["width"] = map["width"]!.GetValue<int>() * Tile, ["height"] = map["height"]!.GetValue<int>() * Tile });
            offset += (map["width"]!.GetValue<int>() + 4) * Tile;
        }
        var png = Path.Combine(folder, TilesetImage); File.WriteAllBytes(png, TilesetPng()); files.Add(png);
        var world = Path.Combine(folder, "spiel.world");
        File.WriteAllText(world, new JsonObject { ["type"] = "world", ["onlyShowAdjacentMaps"] = false, ["maps"] = maps }.ToJsonString(opt)); files.Add(world);
        return files;
    }

    /// <summary>Kontrolle: Karte wieder einlesen und in Trendo-Zeichenreihen zurückverwandeln (Boden + Objekte).</summary>
    public static List<string> ToRows(string tmj)
    {
        var m = JsonNode.Parse(tmj)!.AsObject(); int w = m["width"]!.GetValue<int>(), h = m["height"]!.GetValue<int>();
        var g = new char[h][]; for (var y = 0; y < h; y++) g[y] = Enumerable.Repeat('.', w).ToArray();
        foreach (var layer in m["layers"]!.AsArray().OfType<JsonObject>())
        {
            if (layer["name"]!.GetValue<string>() == "Boden")
            {
                var d = layer["data"]!.AsArray();
                for (var i = 0; i < d.Count; i++) { var gid = d[i]!.GetValue<int>(); if (gid > 0) g[i / w][i % w] = Kinds[gid - 1].Ch; }
            }
            if (layer["type"]!.GetValue<string>() == "objectgroup")
                foreach (var o in layer["objects"]!.AsArray().OfType<JsonObject>())
                {
                    var gid = o["gid"]!.GetValue<int>(); var x = o["x"]!.GetValue<int>() / Tile; var y = o["y"]!.GetValue<int>() / Tile - 1;
                    g[y][x] = Kinds[gid - 1].Ch;
                }
        }
        return [.. g.Select(r => new string(r).TrimEnd('.'))];
    }
}
