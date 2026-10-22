using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Kleines GLB (Würfel) ohne Blender, plus Ablage von Modellen und Medien im Projektordner.</summary>
public static class WebsiteGlb
{
    public static byte[] Cube()
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var indices = new List<int>();
        void Face(float nx, float ny, float nz, (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c, (float X, float Y, float Z) d)
        {
            var start = positions.Count / 3;
            foreach (var p in new[] { a, b, c, d })
            {
                positions.Add(p.X); positions.Add(p.Y); positions.Add(p.Z);
                normals.Add(nx); normals.Add(ny); normals.Add(nz);
            }
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }
        Face(0, 0, 1, (-.5f, -.5f, .5f), (.5f, -.5f, .5f), (.5f, .5f, .5f), (-.5f, .5f, .5f));
        Face(0, 0, -1, (.5f, -.5f, -.5f), (-.5f, -.5f, -.5f), (-.5f, .5f, -.5f), (.5f, .5f, -.5f));
        Face(1, 0, 0, (.5f, -.5f, .5f), (.5f, -.5f, -.5f), (.5f, .5f, -.5f), (.5f, .5f, .5f));
        Face(-1, 0, 0, (-.5f, -.5f, -.5f), (-.5f, -.5f, .5f), (-.5f, .5f, .5f), (-.5f, .5f, -.5f));
        Face(0, 1, 0, (-.5f, .5f, .5f), (.5f, .5f, .5f), (.5f, .5f, -.5f), (-.5f, .5f, -.5f));
        Face(0, -1, 0, (-.5f, -.5f, -.5f), (.5f, -.5f, -.5f), (.5f, -.5f, .5f), (-.5f, -.5f, .5f));

        var bin = new byte[648];
        var o = 0;
        void F(float v) { BitConverter.TryWriteBytes(bin.AsSpan(o), v); o += 4; }
        foreach (var v in positions) F(v);
        foreach (var v in normals) F(v);
        foreach (var v in indices) { BitConverter.TryWriteBytes(bin.AsSpan(o), (ushort)v); o += 2; }

        var json = """
{"asset":{"version":"2.0","generator":"Trendo OS"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0,"name":"Wuerfel"}],"meshes":[{"primitives":[{"attributes":{"POSITION":0,"NORMAL":1},"indices":2,"material":0}]}],"materials":[{"name":"Farbe","pbrMetallicRoughness":{"baseColorFactor":[0.12,0.45,0.95,1],"metallicFactor":0.05,"roughnessFactor":0.4}}],"accessors":[{"bufferView":0,"componentType":5126,"count":24,"type":"VEC3","min":[-0.5,-0.5,-0.5],"max":[0.5,0.5,0.5]},{"bufferView":1,"componentType":5126,"count":24,"type":"VEC3"},{"bufferView":2,"componentType":5123,"count":36,"type":"SCALAR"}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":288,"target":34962},{"buffer":0,"byteOffset":288,"byteLength":288,"target":34962},{"buffer":0,"byteOffset":576,"byteLength":72,"target":34963}],"buffers":[{"byteLength":648}]}
""";
        var jsonBytes = Encoding.UTF8.GetBytes(json.Trim());
        var jsonPad = (4 - jsonBytes.Length % 4) % 4;
        var total = 12 + 8 + jsonBytes.Length + jsonPad + 8 + bin.Length;
        using var ms = new MemoryStream(total);
        using var w = new BinaryWriter(ms);
        w.Write(0x46546C67); w.Write(2); w.Write(total);
        w.Write(jsonBytes.Length + jsonPad); w.Write(0x4E4F534A);
        w.Write(jsonBytes); for (var n = 0; n < jsonPad; n++) w.Write((byte)0x20);
        w.Write(bin.Length); w.Write(0x004E4942); w.Write(bin);
        return ms.ToArray();
    }
}

public static class WebsiteModels
{
    public sealed record Result(string? Relative, string Note, bool Example);

    public static Result Place(string projectDir, string? source, string? blender, bool exampleCube)
    {
        var assets = Path.Combine(projectDir, "assets");
        Directory.CreateDirectory(assets);
        if (!string.IsNullOrWhiteSpace(source))
        {
            if (!File.Exists(source)) return new(null, "Die Modelldatei wurde nicht gefunden.", false);
            var ext = Path.GetExtension(source).ToLowerInvariant();
            if (ext is ".glb" or ".gltf") return CopyShipped(source, assets);
            if (Studio3D.Imports.Contains(ext))
            {
                if (string.IsNullOrWhiteSpace(blender) || !File.Exists(blender))
                    return new(null, "Blender ist nicht installiert. „" + Path.GetFileName(source) + "“ ist kein GLB. Speichere im 3D-Studio eine GLB-Datei. Es wurde kein Ersatzmodell erfunden.", false);
                var dest = Path.Combine(assets, "modell.glb");
                var (run, _) = Studio3D.Convert(blender, source, ".glb", dest);
                if (!run.Ok || !File.Exists(dest))
                    return new(null, "Blender konnte keine GLB schreiben. " + OneLine(run.Err + " " + run.Out), false);
                return new("assets/modell.glb", "Aus „" + Path.GetFileName(source) + "“ mit Blender nach GLB gewandelt.", false);
            }
            return new(null, "Dieses Format kann die Webseite nicht einbetten.", false);
        }
        if (!exampleCube) return new(null, "", false);
        File.WriteAllBytes(Path.Combine(assets, "modell.glb"), WebsiteGlb.Cube());
        return new("assets/modell.glb", "Beispielwürfel von Trendo. Kein Export aus Blender.", true);
    }

    static Result CopyShipped(string source, string assets)
    {
        var ext = Path.GetExtension(source).ToLowerInvariant();
        var dest = Path.Combine(assets, "modell" + ext);
        File.Copy(source, dest, true);
        if (ext == ".gltf") CopyCompanions(source, assets);
        return new("assets/modell" + ext, "Modell „" + Path.GetFileName(source) + "“ liegt in der Seite.", false);
    }

    /// <summary>Kopiert nur Dateien neben der glTF-Datei. Pfade mit .. oder einer Adresse werden ausgelassen.</summary>
    public static void CopyCompanions(string source, string assets)
    {
        var srcDir = Path.GetFullPath(Path.GetDirectoryName(source)!);
        using var doc = JsonDocument.Parse(File.ReadAllText(source));
        void Take(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("uri", out var uriEl)) return;
            var uri = uriEl.GetString();
            if (string.IsNullOrWhiteSpace(uri) || uri.Contains(':') || uri.StartsWith('/') || uri.Contains('\\') || uri.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..")) return;
            var from = Path.GetFullPath(Path.Combine(srcDir, uri.Replace('/', Path.DirectorySeparatorChar)));
            var root = srcDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!from.StartsWith(root, StringComparison.Ordinal) || !File.Exists(from)) return;
            var to = Path.Combine(assets, uri.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to, true);
        }
        if (doc.RootElement.TryGetProperty("buffers", out var buffers)) foreach (var b in buffers.EnumerateArray()) Take(b);
        if (doc.RootElement.TryGetProperty("images", out var images)) foreach (var b in images.EnumerateArray()) Take(b);
    }

    static string OneLine(string s)
    {
        s = s.Replace('\n', ' ').Trim();
        return s.Length > 180 ? s[..180] + "…" : s;
    }
}

public static class WebsiteMedia
{
    public sealed record Result(string? Video, string? Poster, IReadOnlyList<string> Frames, string Note);

    static readonly string[] Videos = [".mp4", ".webm"];
    static readonly string[] Images = [".png", ".jpg", ".jpeg", ".webp"];

    public static Result Place(string projectDir, string? source, string accent)
    {
        if (string.IsNullOrWhiteSpace(source)) return new(null, null, [], "Keine Mediendatei angegeben.");
        var assets = Path.Combine(projectDir, "assets");
        Directory.CreateDirectory(assets);
        if (File.Exists(source))
        {
            var ext = Path.GetExtension(source).ToLowerInvariant();
            if (Videos.Contains(ext))
            {
                var rel = "assets/video" + ext;
                File.Copy(source, Path.Combine(projectDir, rel), true);
                var poster = PosterFor(Path.Combine(projectDir, rel), assets, accent);
                return new(rel, poster, [], "Video „" + Path.GetFileName(source) + "“ liegt lokal in der Seite. Es ist kein Render von Unreal, außer du hast es selbst so erzeugt.");
            }
            if (Images.Contains(ext))
            {
                var rel = CopyFrame(source, Path.Combine(assets, "frames"), 1);
                return new(null, null, [rel], "Ein einzelnes Bild liegt als Standbild da, keine Bildfolge.");
            }
            return new(null, null, [], "Diese Mediendatei kann die Seite nicht abspielen. Nimm mp4, webm, png oder jpg.");
        }
        if (!Directory.Exists(source)) return new(null, null, [], "Die Mediendatei wurde nicht gefunden.");
        var files = Directory.GetFiles(source).Where(f => Images.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 0) return new(null, null, [], "In dem Ordner liegen keine Bilder (png, jpg, webp).");
        var frames = files.Select((f, i) => CopyFrame(f, Path.Combine(assets, "frames"), i + 1)).ToList();
        return new(null, null, frames, files.Count + " Bilder für den Scroll-Effekt. Trendo hat sie nicht selbst gerendert.");
    }

    static string CopyFrame(string source, string dir, int index)
    {
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(source).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";
        var name = index.ToString("D4") + ext;
        File.Copy(source, Path.Combine(dir, name), true);
        return "assets/frames/" + name;
    }

    static string PosterFor(string video, string assets, string accent)
    {
        var jpg = Path.Combine(assets, "poster.jpg");
        if (Cli.Find("ffmpeg") is { } ff)
        {
            var run = Cli.Run(ff, ["-y", "-i", video, "-frames:v", "1", jpg], Path.GetDirectoryName(video), 20000);
            if (run.Ok && File.Exists(jpg)) return "assets/poster.jpg";
        }
        var hex = System.Text.RegularExpressions.Regex.IsMatch(accent, "^#[0-9A-Fa-f]{6}$") ? accent : "#2f6fed";
        File.WriteAllText(Path.Combine(assets, "poster.svg"), PosterSvg(hex), new UTF8Encoding(false));
        return "assets/poster.svg";
    }

    public static string PosterSvg(string accent) =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1280\" height=\"720\"><rect width=\"1280\" height=\"720\" fill=\"#142033\"/><rect x=\"0\" y=\"520\" width=\"1280\" height=\"200\" fill=\"" + accent + "\" opacity=\"0.35\"/><circle cx=\"640\" cy=\"340\" r=\"54\" fill=\"none\" stroke=\"#ffffff\" stroke-width=\"6\"/><polygon points=\"628,316 628,364 676,340\" fill=\"#ffffff\"/></svg>\n";
}

/// <summary>Unreal nur als ehrlicher Status. Pixel Streaming wird dokumentiert und nicht gestartet.</summary>
public static class WebsiteUnreal
{
    public static string Describe(IReadOnlyList<GameStudio.Engine> engines)
    {
        if (engines.Count == 0)
            return "Unreal Engine ist auf diesem PC nicht installiert. Trendo kann kein Video und keine Bildfolge rendern. Wenn du schon Bilder oder ein Video hast, lege sie beim Bau ab.";
        var versions = string.Join(", ", engines.Select(e => e.Version));
        return "Unreal " + versions + " ist installiert. Trendo startet keinen Render und keinen Server. Lege ein fertiges Video (mp4, webm) oder einen Ordner mit Bildern (png, jpg) ab. Die Seite nutzt sie für den Hintergrund oder den Scroll-Effekt.";
    }

    public static void WritePixelDoc(string projectDir)
    {
        File.WriteAllText(Path.Combine(projectDir, "PIXEL-STREAMING.md"), """
# Pixel Streaming – nur eine Option

Pixel Streaming zeigt ein Unreal-Bild über einen eigenen Server im Browser.
Trendo startet diesen Server nicht und verbindet sich nicht.

Das brauchst du selbst, wenn du es wirklich willst:

- einen laufenden Unreal-Pixel-Streaming-Server
- eine Adresse, die du kennst
- einen starken PC und genug Netz

Für die meisten Seiten reicht ein gerendertes Video oder eine Bildfolge.
Die legst du im Studio „Webseite bauen“ ab. Dafür ist kein Server nötig.
""", new UTF8Encoding(false));
    }
}
