using System.Security.Cryptography;

namespace Trendo.Core;

/// <summary>Ablage der KI-Bibliothek im Werkzeugordner: „ki/laufzeit/&lt;laufzeit&gt;“ und „ki/modelle/&lt;modell&gt;“.
/// Alles liegt unter „TrendoWerkzeuge“ und geht bei der Deinstallation von Trendo OS mit. Jede Datei nur mit passender Größe
/// und, wo bekannt, passender SHA-256-Prüfsumme.</summary>
public sealed class KiLibraryStore(string toolsRoot)
{
    public string ToolsRoot { get; } = toolsRoot;
    public string Root => Path.Combine(ToolsRoot, "ki");
    public string RuntimeDir(KiRuntime r) => Path.Combine(Root, "laufzeit", r.ToString().ToLowerInvariant());
    public string ModelDir(KiLibraryModel m) => Path.Combine(Root, "modelle", m.Id);

    /// <summary>Hauptprogramm einer Laufzeit (Ollama und Python aus dem Vollpaket).</summary>
    public string RuntimeExe(KiRuntime r)
    {
        var info = KiLibrary.RuntimeInfo(r);
        return Path.Combine(info.FromVollpaket ? ToolsRoot : RuntimeDir(r), info.Exe.Replace('/', Path.DirectorySeparatorChar));
    }

    public bool RuntimeReady(KiRuntime r) => File.Exists(RuntimeExe(r));

    public string ModelFile(KiLibraryModel m, KiFile f) => Path.Combine(ModelDir(m), f.FileName);

    /// <summary>Modell vollständig da? (Ollama-Modelle fragt die App bei Ollama selbst ab.)</summary>
    public bool ModelReady(KiLibraryModel m) =>
        m.Runtime != KiRuntime.Ollama && (m.Files.Count == 0 ? RuntimeReady(m.Runtime) : m.Files.All(f => new FileInfo(ModelFile(m, f)) is { Exists: true } fi && fi.Length == f.Bytes));

    /// <summary>Heruntergeladenes Laufzeit-ZIP prüfen (Größe, SHA-256) und sicher entpacken.</summary>
    public void InstallRuntime(KiRuntime r, string zip, AuditLog? audit = null, Action<string, string>? extract7z = null)
    {
        var info = KiLibrary.RuntimeInfo(r);
        if (info.FromVollpaket) throw new InvalidOperationException($"{info.Name} kommt aus dem Vollpaket.");
        if (new FileInfo(zip).Length != info.Bytes) throw new InvalidOperationException($"{info.Asset}: Größe passt nicht – nicht installiert.");
        var sha = Sha256(zip);
        if (!string.Equals(sha, info.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"{info.Asset}: Prüfsumme passt nicht – nicht installiert.");
        var dir = RuntimeDir(r); var tmp = dir + ".neu";
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        Directory.CreateDirectory(tmp);
        try
        {
            if (info.Is7z)
            {
                if (extract7z is null) throw new InvalidOperationException("Zum Entpacken wird 7-Zip aus dem Vollpaket gebraucht.");
                extract7z(zip, tmp);
                // Wie bei ZIP: nichts darf außerhalb landen oder auf fremde Orte verweisen
                var rootFull = Path.GetFullPath(tmp).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var e in new DirectoryInfo(tmp).EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
                    if (e.Attributes.HasFlag(FileAttributes.ReparsePoint) || !Path.GetFullPath(e.FullName).StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Unsicherer Eintrag im Paket: " + e.Name);
            }
            else GitHubApps.ExtractSafe(zip, tmp);
            if (!File.Exists(Path.Combine(tmp, info.Exe.Replace('/', Path.DirectorySeparatorChar))))
            {
                // Manche ZIPs haben einen Oberordner: dann diesen nehmen
                var sub = Directory.GetDirectories(tmp).SingleOrDefault(d => File.Exists(Path.Combine(d, info.Exe.Replace('/', Path.DirectorySeparatorChar))));
                if (sub is null) throw new InvalidOperationException($"{info.Exe} fehlt im Paket.");
                var flat = tmp + "2"; Directory.Move(sub, flat); Directory.Delete(tmp, true); Directory.Move(flat, tmp);
            }
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(Path.GetDirectoryName(dir)!);
            Directory.Move(tmp, dir);
            audit?.Append("app", "ki.runtime.install", $"{info.Name} {info.Tag} sha256={sha}");
        }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
    }

    /// <summary>Heruntergeladene Modelldatei prüfen (Größe, SHA-256 falls bekannt) und an ihren Platz legen.</summary>
    public void InstallModelFile(KiLibraryModel m, KiFile f, string downloaded, string? expectedSha256, AuditLog? audit = null)
    {
        if (new FileInfo(downloaded).Length != f.Bytes) throw new InvalidOperationException($"{f.FileName}: Größe passt nicht – nicht übernommen.");
        string? sha = null;
        if (expectedSha256 is { Length: 64 })
        {
            sha = Sha256(downloaded);
            if (!string.Equals(sha, expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"{f.FileName}: Prüfsumme passt nicht – nicht übernommen.");
        }
        Directory.CreateDirectory(ModelDir(m));
        File.Move(downloaded, ModelFile(m, f), true);
        audit?.Append("app", "ki.model.file", $"{m.Id} {f.FileName} {f.Bytes}{(sha is null ? "" : " sha256=" + sha)}");
    }

    /// <summary>Modell entfernen (nur der eigene Ordner unter „ki/modelle“).</summary>
    public bool RemoveModel(KiLibraryModel m, AuditLog? audit = null)
    {
        var d = new DirectoryInfo(ModelDir(m));
        if (!d.Exists) return false;
        if (d.Attributes.HasFlag(FileAttributes.ReparsePoint)) d.Delete(); else d.Delete(true);
        audit?.Append("app", "ki.model.remove", m.Id);
        return true;
    }

    static string Sha256(string file)
    {
        using var s = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }
}
