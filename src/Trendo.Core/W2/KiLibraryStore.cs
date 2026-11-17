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

    public bool RuntimeReady(KiRuntime r) => KiPyApps.For(r) is { } a ? KiPyApps.Ready(a, PyPaths(r)) : File.Exists(RuntimeExe(r));

    /// <summary>Pfade einer Python-KI (Paket E): uv und Git aus Trendos Werkzeugen, eigener Ordner je KI.</summary>
    public KiPyApps.Paths PyPaths(KiRuntime r) => new(RuntimeExe(KiRuntime.Uv), Path.Combine(ToolsRoot, "git", "cmd", "git.exe"), RuntimeDir(r), RuntimeDir(KiRuntime.Uv));

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

    /// <summary>Heruntergeladene Modelldatei prüfen und an ihren Platz legen. Maßgeblich ist die feste SHA-256 aus dem Katalog (<see cref="KiFile.Sha256"/>);
    /// nur ohne sie zählt die Summe, die der Server im Kopf mitschickt (<paramref name="expectedSha256"/>). T-0739 (X-05): Passt Größe oder Summe nicht,
    /// wird die geladene Datei verworfen (gelöscht) und nichts installiert; widersprechen sich Katalog und Server, ebenso.</summary>
    public void InstallModelFile(KiLibraryModel m, KiFile f, string downloaded, string? expectedSha256, AuditLog? audit = null)
    {
        void Reject(string why, string detail)
        {
            try { if (File.Exists(downloaded)) File.Delete(downloaded); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            audit?.Append("app", "ki.model.file.rejected", $"{m.Id} {f.FileName} {detail}");
            throw new InvalidOperationException($"{f.FileName}: {why} – die Datei wurde verworfen, nichts installiert.");
        }
        var len = new FileInfo(downloaded).Length;
        if (len != f.Bytes) Reject("Größe passt nicht", $"bytes={len} erwartet={f.Bytes}");
        var server = expectedSha256 is { Length: 64 } h && h.All(Uri.IsHexDigit) ? h.ToLowerInvariant() : null;
        if (f.HasSha && server is not null && !string.Equals(server, f.Sha256, StringComparison.OrdinalIgnoreCase))
            Reject("Prüfsumme des Servers weicht vom Katalog ab", $"server={server} katalog={f.Sha256}");
        var expected = f.HasSha ? f.Sha256!.ToLowerInvariant() : server;
        string? sha = null;
        if (expected is not null)
        {
            sha = Sha256(downloaded);
            if (!string.Equals(sha, expected, StringComparison.OrdinalIgnoreCase)) Reject("Prüfsumme passt nicht (Datei beschädigt oder verändert)", $"sha256={sha} erwartet={expected}");
        }
        Directory.CreateDirectory(ModelDir(m));
        SafeFile.Move(downloaded, ModelFile(m, f));
        audit?.Append("app", "ki.model.file", $"{m.Id} {f.FileName} {f.Bytes}{(sha is null ? " ohne-pruefsumme" : " sha256=" + sha + (f.HasSha ? " quelle=katalog" : " quelle=server"))}");
    }

    /// <summary>T-0739: Liegt die Modelldatei schon vollständig und unverändert da? Größe muss stimmen; mit fester Katalog-Summe wird zusätzlich
    /// die SHA-256 gerechnet (einmal beim Laden – so überspringt Trendo keine veränderte Datei, die nur zufällig gleich groß ist).</summary>
    public bool ModelFileOk(KiLibraryModel m, KiFile f) => ModelFileOk(m, f, CancellationToken.None);

    /// <summary>Wie <see cref="ModelFileOk(KiLibraryModel, KiFile)"/>, aber abbrechbar: Die SHA-256 läuft in Blöcken zu 1 MB und prüft dazwischen
    /// <paramref name="ct"/> (Gegenprüfung T-0739: Modelldateien bis 13,8 GB – „Abbrechen“ muss auch mitten im Rechnen greifen).</summary>
    public bool ModelFileOk(KiLibraryModel m, KiFile f, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var fi = new FileInfo(ModelFile(m, f));
        if (!fi.Exists || fi.Length != f.Bytes) return false;
        return !f.HasSha || string.Equals(Sha256(fi.FullName, ct), f.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    static string Sha256(string file, CancellationToken ct)
    {
        using var s = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buf = new byte[1 << 20]; int n;
        while ((n = s.Read(buf, 0, buf.Length)) > 0) { ct.ThrowIfCancellationRequested(); h.AppendData(buf, 0, n); }
        return Convert.ToHexStringLower(h.GetHashAndReset());
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

    /// <summary>Wunsch 27.10.2026 („alles wieder deinstallierbar“): KI-Laufzeit entfernen (nur ihr Ordner unter „ki/laufzeit“).</summary>
    public bool RemoveRuntime(KiRuntime r, AuditLog? audit = null)
    {
        var d = new DirectoryInfo(RuntimeDir(r));
        if (!d.Exists) return false;
        foreach (var f in d.EnumerateFiles("*", SearchOption.AllDirectories)) if (f.IsReadOnly) f.IsReadOnly = false;
        if (d.Attributes.HasFlag(FileAttributes.ReparsePoint)) d.Delete(); else d.Delete(true);
        audit?.Append("app", "ki.runtime.remove", r.ToString());
        return true;
    }

    static string Sha256(string file)
    {
        using var s = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }
}
