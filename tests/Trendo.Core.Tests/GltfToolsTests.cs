using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 5 – SPEC-04: Khronos glTF-Validator und glTF-Transform. Berichte im Format des echten Validators (2.0.0-dev.3.10);
/// echte Läufe finden die Programme automatisch (<see cref="EchtWerkzeuge"/>). Beleg: reports/alle-studios/gltf-e2e.md.</summary>
public class GltfToolsTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-gltf-" + Guid.NewGuid().ToString("N"));
    public GltfToolsTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }
    const string Gut = "{\"uri\":\"regal.glb\",\"mimeType\":\"model/gltf-binary\",\"validatorVersion\":\"2.0.0-dev.3.10\",\"issues\":{\"numErrors\":0,\"numWarnings\":0,\"numInfos\":0,\"numHints\":0,\"messages\":[],\"truncated\":false},\"info\":{\"materialCount\":8,\"totalVertexCount\":19680,\"totalTriangleCount\":9840,\"extensionsUsed\":[\"KHR_materials_clearcoat\"]}}";
    const string Kaputt = "{\"uri\":\"kaputt.glb\",\"validatorVersion\":\"2.0.0-dev.3.10\",\"issues\":{\"numErrors\":1,\"numWarnings\":0,\"numInfos\":0,\"numHints\":0,\"messages\":[{\"code\":\"GLB_UNEXPECTED_END_OF_CHUNK_DATA\",\"message\":\"Unexpected end of chunk data.\",\"severity\":0,\"offset\":200000},{\"code\":\"UNUSED_OBJECT\",\"message\":\"x\",\"severity\":2}],\"truncated\":false}}";
    string Glb(string name = "m.glb") { var f = Path.Combine(_dir, name); File.WriteAllBytes(f, [0x67, 0x6C, 0x54, 0x46, 2, 0, 0, 0]); return f; }
    static Cli.Result R(int code, string o = "", string e = "") => new(code, o, e);

    [Fact(DisplayName = "glTF +1: Bericht des Validators – Zahlen, Dreiecke, Materialien, Erweiterungen, Text „bestanden“")]
    public void Parse_ok()
    {
        var r = GltfTools.ParseReport(Gut);
        Assert.True(r.Ok); Assert.Equal((9840L, 19680L, 8), (r.Triangles, r.Vertices, r.Materials)); Assert.Equal("2.0.0-dev.3.10", r.Version);
        Assert.Equal(["KHR_materials_clearcoat"], r.Extensions); Assert.StartsWith("✓ Khronos-Prüfung bestanden", r.Text);
    }

    [Fact(DisplayName = "glTF +2: kaputte Datei – nur Fehlercodes der Schwere „Fehler“ im Text")]
    public void Parse_errors()
    {
        var r = GltfTools.ParseReport(Kaputt);
        Assert.False(r.Ok); Assert.Equal(["GLB_UNEXPECTED_END_OF_CHUNK_DATA"], r.ErrorCodes); Assert.Contains("1 Fehler", r.Text);
    }

    [Fact(DisplayName = "glTF +3: Exit 1 mit Bericht ist ein Ergebnis (Modell hat Fehler), kein Programmfehler")]
    public void Validate_exit1()
    {
        var (rep, msg) = GltfTools.Validate(Glb("v.exe"), Glb(), run: (_, a, _) => { Assert.Equal("-o", a[0]); return R(1, Kaputt); });
        Assert.NotNull(rep); Assert.False(rep!.Ok); Assert.Contains("GLB_UNEXPECTED_END_OF_CHUNK_DATA", msg);
    }

    [Fact(DisplayName = "glTF −1: kein Bericht, falsche Datei oder fehlender Prüfer werden verständlich gemeldet")]
    public void Validate_refuses()
    {
        Assert.Contains("keinen lesbaren Bericht", GltfTools.Validate(Glb("v.exe"), Glb(), run: (_, _, _) => R(2, "", "Absturz")).Message);
        var obj = Path.Combine(_dir, "a.obj"); File.WriteAllText(obj, "v 0 0 0");
        Assert.Contains(".glb", GltfTools.Validate(Glb("v.exe"), obj).Message);
        Assert.Contains("nicht eingerichtet", GltfTools.Validate(Path.Combine(_dir, "fehlt.exe"), Glb()).Message);
    }

    [Fact(DisplayName = "glTF +4: Packen – node mit cli.js direkt (keine .cmd), meshopt + WebP, Größen im Ergebnis")]
    public void Pack_args()
    {
        var cli = Glb("cli.js"); var node = Glb("node.exe"); var input = Glb("regal & co.glb"); var output = Path.Combine(_dir, "regal-web.glb"); IReadOnlyList<string>? seen = null;
        var (ok, msg) = GltfTools.Pack(node, cli, input, output, run: (e, a, _) => { seen = a; File.WriteAllBytes(a[3], new byte[4]); return R(0); });
        Assert.True(ok, msg); Assert.Equal([cli, "optimize", input, output, "--compress", "meshopt", "--texture-compress", "webp"], seen); Assert.Contains("KB →", msg);
    }

    [Fact(DisplayName = "glTF −2: Packen überschreibt nie das Original, braucht .glb-Ziel, Node und glTF-Transform")]
    public void Pack_refuses()
    {
        var input = Glb();
        Assert.Contains("Original", GltfTools.Pack(Glb("n.exe"), Glb("c.js"), input, input).Message);
        Assert.Contains(".glb", GltfTools.Pack(Glb("n.exe"), Glb("c.js"), input, Path.Combine(_dir, "x.gltf")).Message);
        Assert.Contains("Node.js", GltfTools.Pack(Path.Combine(_dir, "nix.exe"), Glb("c.js"), input, Path.Combine(_dir, "o.glb")).Message);
        Assert.Contains("glTF-Transform", GltfTools.Pack(Glb("n.exe"), Path.Combine(_dir, "nix.js"), input, Path.Combine(_dir, "o.glb")).Message);
    }

    [Fact(DisplayName = "glTF +5: Einrichten per npm – feste Fassung, eigener Ordner, ohne Installationsskripte")]
    public void Install_args()
    {
        var a = GltfTools.InstallArgs("C:/w/gltf-transform");
        Assert.Contains("@gltf-transform/cli@4.5.1", a); Assert.Contains("--ignore-scripts", a); Assert.Contains("--prefix", a); Assert.DoesNotContain("-g", a);
        Assert.EndsWith(Path.Combine("@gltf-transform", "cli", "bin", "cli.js"), GltfTools.CliJs("x"));
    }

    // ---------- echte Läufe ----------
    public sealed class ValidatorFactAttribute : FactAttribute { public ValidatorFactAttribute() { if (EchtWerkzeuge.GltfValidator() is null) Skip = "Übersprungen – glTF-Validator nicht gefunden."; } }
    public sealed class TransformFactAttribute : FactAttribute { public TransformFactAttribute() { if (EchtWerkzeuge.GltfValidator() is null || EchtWerkzeuge.GltfTransformCli() is null || EchtWerkzeuge.Node() is null) Skip = "Übersprungen – glTF-Transform, Node oder Validator nicht gefunden."; } }
    static string Regal() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent; return Path.Combine(d!.FullName, "src", "Trendo.App", "Assets", "moebel", "regal.glb"); }

    [ValidatorFact(DisplayName = "glTF echt: Khronos-Prüfer – Trendo-Regal fehlerfrei (9840 Dreiecke), abgeschnittene Datei mit GLB_UNEXPECTED_END_OF_CHUNK_DATA")]
    public void Real_validate()
    {
        var (ok, _) = GltfTools.Validate(EchtWerkzeuge.GltfValidator()!, Regal());
        Assert.NotNull(ok); Assert.True(ok!.Ok); Assert.Equal(9840, ok.Triangles); Assert.Equal(8, ok.Materials);
        var cut = Path.Combine(_dir, "kaputt.glb"); File.WriteAllBytes(cut, File.ReadAllBytes(Regal())[..200_000]);
        var (bad, _) = GltfTools.Validate(EchtWerkzeuge.GltfValidator()!, cut);
        Assert.NotNull(bad); Assert.False(bad!.Ok); Assert.Contains("GLB_UNEXPECTED_END_OF_CHUNK_DATA", bad.ErrorCodes);
    }

    [TransformFact(DisplayName = "glTF echt: Packen für Web & Brille – Regal deutlich kleiner, danach weiter fehlerfrei mit meshopt und WebP")]
    public void Real_pack()
    {
        var input = Path.Combine(_dir, "regal.glb"); File.Copy(Regal(), input); var output = Path.Combine(_dir, "regal-web.glb");
        var (ok, msg) = GltfTools.Pack(EchtWerkzeuge.Node()!, EchtWerkzeuge.GltfTransformCli()!, input, output);
        Assert.True(ok, msg); Assert.True(new FileInfo(output).Length < new FileInfo(input).Length / 2, msg);
        var (rep, _) = GltfTools.Validate(EchtWerkzeuge.GltfValidator()!, output);
        Assert.True(rep!.Ok); Assert.Contains("EXT_meshopt_compression", rep.Extensions); Assert.Contains("EXT_texture_webp", rep.Extensions);
    }
}
