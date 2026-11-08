namespace Trendo.Core.Tests;

/// <summary>Findet die Fremdprogramme für die Echtlauf-Tests automatisch (Wunsch 08.11.2026: „kannst das automatisch“).
/// Reihenfolge: Umgebungsvariable (Vorrang) → wo Trendo die Werkzeuge selbst einrichtet (%LOCALAPPDATA%\TrendoWerkzeuge) → Prüfordner %TEMP%\trendo-tools-b.
/// Fehlt ein Programm, wird der Test übersprungen – nie heruntergeladen.</summary>
public static class EchtWerkzeuge
{
    static string Werkzeuge => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge");
    static string Pruef => Path.Combine(Path.GetTempPath(), "trendo-tools-b");
    static string Py => OperatingSystem.IsWindows() ? Path.Combine("Scripts", "python.exe") : Path.Combine("bin", "python");

    static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
    static string? First(params string?[] candidates) => candidates.FirstOrDefault(c => c is not null && (File.Exists(c) || Directory.Exists(c)));

    /// <summary>Ordner, unter dem typst/, mediamtx/, streamlink/ liegen.</summary>
    public static IEnumerable<string> ToolRoots() => new[] { Env("TRENDO_WERKZEUGE_TEST"), Werkzeuge, Pruef }.OfType<string>().Where(Directory.Exists).Distinct();

    /// <summary>Programm <paramref name="tool"/> (z. B. „typst“) im ersten Wurzelordner, der es enthält; Python-Hilfsprogramme in Paketen werden übergangen.</summary>
    public static string? Tool(string tool)
    {
        var name = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
        foreach (var root in ToolRoots())
        {
            var dir = Path.Combine(root, tool); if (!Directory.Exists(dir)) continue;
            var f = Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault(p => !p.Contains(Path.DirectorySeparatorChar + "Python" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            if (f is not null) return f;
        }
        return null;
    }

    public static string? IfcPython() => First(Env("TRENDO_IFC_PYTHON"), Path.Combine(Werkzeuge, "ki", "laufzeit", "ifc", "env", Py), Path.Combine(Pruef, "py-ifc", ".venv", Py));
    public static string? Open3dPython() => First(Env("TRENDO_OPEN3D_PYTHON"), Path.Combine(Werkzeuge, "ki", "laufzeit", "scannetz", "env", Py), Path.Combine(Pruef, "py-open3d", ".venv", Py));
    public static string? PmtilesExe() => First(Env("TRENDO_PMTILES_EXE"), Path.Combine(Pruef, "pmtiles", "x", OperatingSystem.IsWindows() ? "pmtiles.exe" : "pmtiles"));
    public static string? VektorPmtiles() => First(Env("TRENDO_VEKTOR_PMTILES"), Path.Combine(Pruef, "pmtiles", "ort.pmtiles"));
    public static string? AssimpDll() => First(Env("TRENDO_ASSIMP_DLL"), Path.Combine(Werkzeuge, "assimp", "Release", "assimp-vc143-mt.dll"), Path.Combine(Pruef, "assimp", "x", "Release", "assimp-vc143-mt.dll"));
    public static string? Melt() => First(Env("TRENDO_MELT_EXE"), Path.Combine(Werkzeuge, "kdenlive", "bin", "melt.exe"));
    public static string? FfmpegDir() => First(Env("TRENDO_FFMPEG_DIR"), Path.Combine(Werkzeuge, "ffmpeg", "bin")) is { } d && File.Exists(Path.Combine(d, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg")) ? d : null;
}
