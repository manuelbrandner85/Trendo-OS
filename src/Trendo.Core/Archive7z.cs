using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace Trendo.Core;

/// <summary>Selbst nachgebaut (04.11.2026, statt 7-Zip): .7z-Archive entpacken mit SharpCompress (MIT). Schutz: nichts außerhalb des Zielordners
/// (keine „..“-Pfade, keine absoluten Pfade) – wie bei ZIP.</summary>
public static class Archive7z
{
    public static int Extract(string archive, string target)
    {
        Directory.CreateDirectory(target);
        var root = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var n = 0;
        using var a = SevenZipArchive.OpenArchive(archive);
        foreach (var e in a.Entries.Where(e => !e.IsDirectory))
        {
            var key = (e.Key ?? "").Replace('\\', '/');
            var dest = Path.GetFullPath(Path.Combine(target, key));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(key)) throw new InvalidDataException("Unsicherer Pfad im Archiv: " + key);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            e.WriteToFile(dest, new ExtractionOptions { Overwrite = true });
            n++;
        }
        return n;
    }
}
