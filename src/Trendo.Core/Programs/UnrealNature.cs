using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>Fotoreale Natur für Unreal-Spiele (Nutzerwunsch 07.11.2026, „fotorealistisch, ohne Fachwissen“): freie Fotomaterialien und
/// Pflanzen-/Felsmodelle von Poly Haven (CC0, offizielle API) laden, im Werkzeugordner zwischenspeichern und ins Unreal-Projekt kopieren.
/// Die Welt richtet danach das Python-Skript in Unreal ein (<see cref="UnrealGame.WeltScript"/>).</summary>
public static class UnrealNature
{
    public const string Api = "https://api.polyhaven.com";
    public const string UserAgent = "Trendo-OS (Spiele-Studio)";
    /// <summary>Bodenkarten: Wiese und Waldboden, im Material großflächig gemischt.</summary>
    public static readonly string[] Grounds = ["leafy_grass", "forest_ground_04"];
    public static readonly string[] GroundMaps = ["Diffuse", "nor_dx", "Rough", "AO"];
    /// <summary>Modelle mit Art (Verteilungsregel im Spiel): Gras, Farn, Strauch, Baum, Holz, Stein.</summary>
    public static readonly (string Id, string Kind)[] Models =
    [
        ("grass_medium_01", "gras"), ("fern_02", "farn"), ("shrub_02", "strauch"), ("shrub_04", "strauch"), ("pine_sapling_small", "baum"), ("tree_small_02", "baum"),
        ("tree_stump_01", "holz"), ("dead_tree_trunk", "holz"), ("rock_moss_set_01", "stein"), ("boulder_01", "stein"),
    ];
    /// <summary>Spielobjekte als Fotomodelle in echter Größe (Rolle wie im Trendo-Raster): Fundstück o (Messingvase – eine Münze wäre im Gras
    /// unsichtbar), Gegenstand G, Ziel Z (Schatztruhe), Schlüssel K, Speicherpunkt C, Hindernis D, Feder F (Reifen), Waffe (Säbel), Wurfwaffe (Beil).
    /// Muss zu PROPS im Welt-Skript passen.</summary>
    public static readonly (string Role, string Id)[] Props =
    [
        ("o", "brass_vase_02"), ("G", "food_apple_01"), ("Z", "treasure_chest"), ("K", "brass_diya_lantern"), ("C", "wooden_lantern_01"),
        ("D", "wooden_crate_02"), ("F", "old_tyre"), ("waffe", "wooden_handle_saber"), ("wurf", "hatchet"),
    ];

    static long Size(System.Text.Json.Nodes.JsonNode n) => long.Parse(n.ToString(), System.Globalization.CultureInfo.InvariantCulture);

    public static string CacheDir(string toolsRoot) => Path.Combine(toolsRoot, "assets", "polyhaven");

    /// <summary>Dateiliste aus den API-Antworten („/files/{id}“): (URL, Pfad relativ zum Asset-Ordner, Größe). 2k, Bodenkarten als JPG,
    /// Modelle (Natur und Spielobjekte) als glTF mit allen verlinkten Dateien und zusätzlich die Blatt-Alpha-Karten (im glTF nicht verlinkt).</summary>
    public static List<(string Asset, string Url, string Rel, long Size)> Plan(Func<string, JsonObject> files)
    {
        var list = new List<(string, string, string, long)>();
        foreach (var g in Grounds)
        {
            var f = files(g);
            foreach (var k in GroundMaps)
                if (f[k]?["2k"]?["jpg"] is JsonObject e) list.Add((g, e["url"]!.ToString(), $"{g}_{k}_2k.jpg", Size(e["size"]!)));
        }
        foreach (var id in Models.Select(m => m.Id).Concat(Props.Select(p => p.Id)))
        {
            var f = files(id);
            if (f["gltf"]?["2k"]?["gltf"] is not JsonObject g) continue;
            var url = g["url"]!.ToString();
            list.Add((id, url, url[(url.LastIndexOf('/') + 1)..], Size(g["size"]!)));
            if (g["include"] is JsonObject inc)
                foreach (var (rel, e) in inc) list.Add((id, e!["url"]!.ToString(), rel, Size(e["size"]!)));
            foreach (var (k, v) in f)
                if (k.EndsWith("alpha", StringComparison.OrdinalIgnoreCase) && v?["2k"]?["png"] is JsonObject a)
                {
                    var au = a["url"]!.ToString();
                    list.Add((id, au, "textures/" + au[(au.LastIndexOf('/') + 1)..], Size(a["size"]!)));
                }
        }
        return list;
    }

    /// <summary>Fehlende oder unvollständige Dateien laden (Größe geprüft), Lizenzdatei schreiben. Gibt die Anzahl geladener Dateien zurück.</summary>
    public static int Fetch(string cacheDir, Func<string, JsonObject> files, Action<string, string> download, Action<string>? progress = null)
    {
        var plan = Plan(files);
        // erst alle Pfade prüfen, dann laden (nichts landet außerhalb des Zwischenspeichers)
        if (plan.FirstOrDefault(p => p.Rel.Contains("..") || Path.IsPathRooted(p.Rel) || p.Asset.Contains("..")) is { Rel: not null } bad)
            throw new InvalidOperationException("Ungültiger Dateipfad in der Poly-Haven-Antwort: " + bad.Rel);
        var n = 0;
        foreach (var (asset, url, rel, size) in plan)
        {
            var dest = Path.Combine(cacheDir, asset, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(dest) && new FileInfo(dest).Length == size) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            progress?.Invoke($"Lade {asset}: {Path.GetFileName(rel)}");
            download(url, dest + ".part");
            if (new FileInfo(dest + ".part").Length != size) { File.Delete(dest + ".part"); throw new IOException($"Download unvollständig: {Path.GetFileName(rel)}"); }
            File.Move(dest + ".part", dest, true); n++;
        }
        File.WriteAllText(Path.Combine(cacheDir, "LIZENZ.txt"), "Quelle: Poly Haven (polyhaven.com) – Lizenz CC0 1.0 (gemeinfrei, keine Namensnennung nötig).\nAssets: " + string.Join(", ", Grounds.Concat(Models.Select(m => m.Id)).Concat(Props.Select(p => p.Id))) + "\n");
        return n;
    }

    /// <summary>Zwischengespeicherte Assets nach Content/Trendo/Quellen des Projekts kopieren (nur geänderte Dateien).</summary>
    public static int CopyTo(string cacheDir, string projectDir)
    {
        var dest = Path.Combine(projectDir, "Content", "Trendo", "Quellen");
        var n = 0;
        foreach (var f in Directory.EnumerateFiles(cacheDir, "*", SearchOption.AllDirectories))
        {
            if (f.EndsWith(".part")) continue;
            var t = Path.Combine(dest, Path.GetRelativePath(cacheDir, f));
            if (File.Exists(t) && new FileInfo(t).Length == new FileInfo(f).Length) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(t)!); File.Copy(f, t, true); n++;
        }
        return n;
    }

    /// <summary>Nur Downloads von Poly Haven sind erlaubt (Netzzugriff selbst liegt in der App, der Core bleibt ohne Netz – QA-019).</summary>
    public static bool AllowedDownload(string url) => url.StartsWith("https://dl.polyhaven.org/", StringComparison.Ordinal);
    public static string FilesUrl(string id) => $"{Api}/files/{Uri.EscapeDataString(id)}";
}
