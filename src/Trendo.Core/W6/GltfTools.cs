using System.Text.Json;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 5 – SPEC-04 „VR & AR gestalten“: offizielle Prüfung mit dem Khronos glTF-Validator (Apache-2.0, 2.0.0-dev.3.10)
/// und Packen für Web & Brille mit glTF-Transform (MIT, 4.5.1: meshopt + WebP). Beide laufen als eigene Prozesse mit Argumenten als Liste;
/// glTF-Transform wird mit node direkt gestartet (nicht über die .cmd-Datei, deren cmd.exe Zeichen wie „&amp;“ in Dateinamen auswerten würde).</summary>
public static class GltfTools
{
    public const string TransformVersion = "4.5.1";
    public sealed record Report(int Errors, int Warnings, int Infos, int Hints, long Triangles, long Vertices, int Materials, IReadOnlyList<string> ErrorCodes, string Version, IReadOnlyList<string> Extensions)
    {
        public bool Ok => Errors == 0;
        public string Text => Ok ? $"✓ Khronos-Prüfung bestanden ({Warnings} Warnungen, {Infos} Hinweise) – {Triangles:N0} Dreiecke, {Materials} Materialien"
            : $"⚠ {Errors} Fehler laut Khronos-Prüfung: {string.Join(", ", ErrorCodes)}";
    }

    public static bool IsGltf(string file) => file.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase);

    /// <summary>Validator-Argumente: Bericht als JSON auf die Standardausgabe.</summary>
    public static IReadOnlyList<string> ValidatorArgs(string file) => ["-o", file];

    /// <summary>Bericht des Validators lesen (Fehlercodes nur der Schwere „Fehler“, höchstens 8).</summary>
    public static Report ParseReport(string json)
    {
        using var d = JsonDocument.Parse(json); var r = d.RootElement;
        var i = r.GetProperty("issues"); int N(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.TryGetInt32(out var n) ? n : 0;
        var codes = i.TryGetProperty("messages", out var ms) && ms.ValueKind == JsonValueKind.Array
            ? ms.EnumerateArray().Where(m => m.TryGetProperty("severity", out var s) && s.GetInt32() == 0).Select(m => m.GetProperty("code").GetString() ?? "").Distinct().Take(8).ToList() : [];
        var info = r.TryGetProperty("info", out var inf) ? inf : default;
        long L(string k) => info.ValueKind == JsonValueKind.Object && info.TryGetProperty(k, out var v) && v.TryGetInt64(out var n) ? n : 0;
        var ext = info.ValueKind == JsonValueKind.Object && info.TryGetProperty("extensionsUsed", out var ex) && ex.ValueKind == JsonValueKind.Array ? ex.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
        return new Report(N(i, "numErrors"), N(i, "numWarnings"), N(i, "numInfos"), N(i, "numHints"), L("totalTriangleCount"), L("totalVertexCount"), (int)L("materialCount"), codes,
            r.TryGetProperty("validatorVersion", out var ver) ? ver.GetString() ?? "" : "", ext);
    }

    /// <summary>Prüfen. Der Validator endet bei Fehlern im Modell mit Exit 1 – das ist ein Ergebnis, kein Programmfehler; nur ohne lesbaren Bericht ist es einer.</summary>
    public static (Report? Report, string Message) Validate(string exe, string file, int timeoutMs = 120_000, Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (!File.Exists(file) || !IsGltf(file)) return (null, "Bitte eine .glb- oder .gltf-Datei wählen.");
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return (null, "Der glTF-Validator ist noch nicht eingerichtet (kostenlos, Apache-2.0).");
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(exe, ValidatorArgs(file), timeoutMs);
        try { var rep = ParseReport(r.Out); return (rep, rep.Text); }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return (null, "Der Validator lieferte keinen lesbaren Bericht: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0]); }
    }

    /// <summary>glTF-Transform-Argumente für „Web &amp; Brille“: meshopt-Geometrie und WebP-Texturen (vom Paket empfohlene Optimierung).</summary>
    public static IReadOnlyList<string> PackArgs(string cliJs, string input, string output) =>
        [cliJs, "optimize", input, output, "--compress", "meshopt", "--texture-compress", "webp"];

    /// <summary>npm-Argumente zum Einrichten: feste Fassung, eigener Ordner, ohne Installationsskripte.</summary>
    public static IReadOnlyList<string> InstallArgs(string dir) =>
        ["install", "@gltf-transform/cli@" + TransformVersion, "--prefix", dir, "--no-fund", "--no-audit", "--omit=dev", "--ignore-scripts"];

    /// <summary>Startskript des Pakets im Einrichtungsordner.</summary>
    public static string CliJs(string dir) => Path.Combine(dir, "node_modules", "@gltf-transform", "cli", "bin", "cli.js");

    public static (bool Ok, string Message) Pack(string node, string cliJs, string input, string output, int timeoutMs = 300_000, Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (!File.Exists(input) || !IsGltf(input)) return (false, "Bitte eine .glb- oder .gltf-Datei wählen.");
        if (!output.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) return (false, "Ziel muss eine .glb-Datei sein.");
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) return (false, "Das Original wird nicht überschrieben – bitte anderes Ziel.");
        if (string.IsNullOrWhiteSpace(node) || !File.Exists(node)) return (false, "Node.js fehlt – zuerst unter Programme „Node.js“ einrichten.");
        if (!File.Exists(cliJs)) return (false, "glTF-Transform ist noch nicht eingerichtet (kostenlos, MIT).");
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(node, PackArgs(cliJs, input, output), timeoutMs);
        if (!r.Ok || !File.Exists(output)) return (false, "glTF-Transform: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[^1]);
        return (true, $"{new FileInfo(input).Length / 1024.0:0} KB → {new FileInfo(output).Length / 1024.0:0} KB");
    }
}
