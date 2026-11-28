using System.Text;

namespace Trendo.Core;

/// <summary>T-0680: Datei so schreiben, dass nach einem Absturz oder Stromausfall entweder die alte oder die neue Fassung vollständig da ist – nie eine halbe.
/// Ablauf: Zwischendatei im selben Ordner schreiben → auf den Datenträger leeren → über die Zieldatei schieben (<see cref="SafeFile.Move"/> wartet bei
/// kurz gesperrten Dateien). Die Zwischendatei hat einen eigenen Namen je Schreibvorgang, damit sich zwei Fenster nicht in die Quere kommen.</summary>
public static class AtomicFile
{
    static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Reste abgebrochener Schreibvorgänge, die älter sind, werden beim nächsten Schreiben derselben Datei entfernt.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    public static void WriteAllText(string path, string text) => WriteAllBytes(path, Utf8.GetBytes(text));

    public static void WriteAllBytes(string path, byte[] bytes)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);
        var tmp = full + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // bis auf den Datenträger – sonst kann nach einem Stromausfall eine leere Datei unter dem richtigen Namen liegen
            }
            SafeFile.Move(tmp, full);
        }
        catch
        {
            try { File.Delete(tmp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            throw;
        }
        Sweep(dir, Path.GetFileName(full));
    }

    /// <summary>T-0788 (5. Prüfrunde): Ist <paramref name="fileName"/> genau eine Zwischendatei dieser Schreibweise für <paramref name="target"/> –
    /// „&lt;Name&gt;.&lt;8 Hexziffern&gt;.tmp“ (siehe <see cref="WriteAllBytes"/>)? Fremde Dateien wie „Name.notiz.tmp“ zählen nicht.</summary>
    public static bool IsOwnTemp(string fileName, string target)
    {
        var name = Path.GetFileName(target);
        if (fileName.Length != name.Length + 13 || !fileName.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase) || !fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return false;
        var mid = fileName.AsSpan(name.Length + 1, 8);
        foreach (var c in mid) if (!char.IsAsciiHexDigitLower(c)) return false;
        return true;
    }

    /// <summary>Zwischendateien früherer, abgebrochener Schreibvorgänge dieser Datei aufräumen (nur ältere – ein anderes Fenster kann gerade schreiben).</summary>
    static void Sweep(string dir, string name)
    {
        try
        {
            var limit = DateTime.UtcNow - StaleAfter;
            foreach (var f in Directory.EnumerateFiles(dir, name + ".*.tmp"))
                if (IsOwnTemp(Path.GetFileName(f), name) && File.GetLastWriteTimeUtc(f) < limit) File.Delete(f); // T-0788: nur die eigenen Zwischendateien, nie „Name.notiz.tmp“
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
