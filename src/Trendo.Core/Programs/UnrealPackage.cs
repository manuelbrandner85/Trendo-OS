using System.IO.Compression;

namespace Trendo.Core.Programs;

/// <summary>Vorschlag 12 (freigegeben 07.11.2026): Ein-Klick-Veröffentlichung eines Unreal-Spiels. Trendo ruft Unreals offiziellen Paketierer auf
/// (RunUAT BuildCookRun: Spiel-Code als Shipping bauen, Inhalte kochen, in .pak packen, in einen Ordner ablegen) und macht daraus ein ZIP für
/// Windows. Hochladen bleibt ein eigener Schritt nach Ja.</summary>
public static class UnrealPackage
{
    public static string RunUat(string engineDir) => Path.Combine(engineDir, "Engine", "Build", "BatchFiles", "RunUAT.bat");

    /// <summary>Befehl für ein fertiges Windows-Spiel (Shipping, ohne Editor-Neubau, ohne Rückfragen).</summary>
    public static IReadOnlyList<string> Args(string uproject, string archiveDir) =>
        ["BuildCookRun", "-project=" + uproject, "-noP4", "-platform=Win64", "-clientconfig=Shipping", "-build", "-cook", "-stage", "-pak", "-archive",
         "-archivedirectory=" + archiveDir, "-utf8output", "-unattended", "-nocompileeditor"];

    public static bool Ok(string log) => (log ?? "").Contains("BUILD SUCCESSFUL");

    /// <summary>Grund in einem Satz: letzte Fehlerzeile des Paketierers, sonst ein allgemeiner Hinweis.</summary>
    public static string Reason(string log)
    {
        var all = (log ?? "").Split('\n').Select(x => x.Trim()).ToList();
        var l = all.LastOrDefault(x => x.Contains("Error:") || x.Contains("error ")) ?? all.LastOrDefault(x => x.Contains("BUILD FAILED")); // die eigentliche Fehlerzeile vor „BUILD FAILED“
        return l is null ? "Unreal hat das Paket nicht fertig gemeldet – Protokoll paket.txt" : l.Length > 220 ? l[..220] + " …" : l;
    }

    /// <summary>Das gestartete Spiel im abgelegten Ordner (Windows/&lt;Name&gt;.exe), sonst null.</summary>
    public static string? Exe(string archiveDir)
    {
        var win = Path.Combine(archiveDir, "Windows");
        return Directory.Exists(win) ? Directory.GetFiles(win, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault() : null;
    }

    /// <summary>Windows-Ordner als ZIP (vorhandenes wird ersetzt). Liefert den ZIP-Pfad.</summary>
    public static string Zip(string archiveDir, string zipPath)
    {
        var win = Path.Combine(archiveDir, "Windows");
        if (!Directory.Exists(win)) throw new InvalidOperationException("Im Paketordner fehlt das Windows-Spiel.");
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(win, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return zipPath;
    }
}
