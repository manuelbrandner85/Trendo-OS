using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core.Sys;

/// <summary>T-0764: Vertrauen in ein Programm hängt an seiner Herkunft, nicht am Ordnernamen.
/// 3. Prüfrunde (P3-1): Beim Einrichten (Portable-ZIP, Vollpaket, Zusatzprogramm als ZIP, GitHub-Programm) erfasst Trendo die SHA-256 jeder
/// Programmdatei. 4. Prüfrunde (M1, N1, N2): Diese Vertrauensliste liegt nicht mehr im Programmordner, sondern in Trendos Datenbereich
/// (Ordner „sicherheit“). Jeder Eintrag (Pfad + Prüfsumme) trägt einen HMAC-SHA256 mit einem zufälligen Schlüssel, der unter Windows per DPAPI
/// (CurrentUser) geschützt ist. Der Schlüssel wird nur beim Einrichten durch Trendo erzeugt; das Prüfen liest ihn nur.
/// Vertrauenswürdig ist eine .exe nur, wenn ihr Eintrag gültig ist, ihr Inhalt jetzt dieselbe Prüfsumme hat (vor jedem Start neu gerechnet – kein
/// Zwischenspeicher, der sich mit gleich großer, gleich datierter Datei überlisten ließe) und jede .dll in ihrem Ordner ebenso eingetragen und
/// unverändert ist. Die Kennungsdatei „.trendo-portable.json“ im Programmordner ist nur noch ein Hinweis (für die Programm-Seite).
/// GRENZE: Unter einem gemeinsamen Windows-Konto kann ein Programm dieses Kontos den Schlüssel ebenfalls entschlüsseln – das ist kein harter
/// Schutz gegen Absicht und Fachwissen. Wirksamer Kinderschutz braucht ein eigenes Windows-Standardkonto für das Kind.</summary>
public static class ProgramTrust
{
    sealed record Marker(string? Name, string? Sha256, string? Am, Dictionary<string, string>? Dateien);
    sealed record Entry(string Path, string Sha256, string Mac);
    sealed record StoreFile(int Version, List<Entry> Entries);

    /// <summary>Programmordner (Werkzeugordner, Portable-Ordner) → Ordner ihrer Vertrauensliste in Trendos Datenbereich (z. B. „&lt;Daten&gt;\sicherheit“).
    /// Die App meldet ihre Ordner beim Start an; für nicht angemeldete Ordner wird nichts erfasst und nichts vertraut.</summary>
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Stores = new(StringComparer.OrdinalIgnoreCase);
    public static void RegisterStore(string programRoot, string storeDir) =>
        Stores[System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(programRoot)) + System.IO.Path.DirectorySeparatorChar] = System.IO.Path.GetFullPath(storeDir);
    /// <summary>Ordner der Vertrauensliste für eine Datei: der des längsten angemeldeten Programmordners, der sie enthält.</summary>
    static string? DirFor(string file)
    {
        var full = System.IO.Path.GetFullPath(file);
        return Stores.Where(kv => full.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)).OrderByDescending(kv => kv.Key.Length).Select(kv => kv.Value).FirstOrDefault();
    }
    /// <summary>Alle angemeldeten Listen (für Programme außerhalb der Programmordner, z. B. %LOCALAPPDATA%\Programs).</summary>
    static IEnumerable<string> AllDirs => Stores.Values.Distinct(StringComparer.OrdinalIgnoreCase);
    static readonly object Gate = new();

    static string Norm(string p) => System.IO.Path.GetFullPath(p).ToUpperInvariant();
    static string Sha(string file) { using var s = File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(s)); }
    static string Mac(byte[] key, string path, string sha) => Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Norm(path) + "|" + sha.ToLowerInvariant())));

    /// <summary>Kennungsdatei (Hinweis für die Programm-Seite) schreiben und alle .exe und .dll des Ordners in die geschützte Vertrauensliste eintragen.
    /// Nur aus Einrichtungswegen aufrufen, die Trendo selbst auslöst.</summary>
    public static void WriteMarker(string dir, string name, string sourceSha)
    {
        var files = Programs(dir).ToDictionary(f => System.IO.Path.GetRelativePath(dir, f).Replace('\\', '/'), Sha, StringComparer.OrdinalIgnoreCase);
        File.WriteAllText(System.IO.Path.Combine(dir, Software.PortableMarker), JsonSerializer.Serialize(new Marker(name, sourceSha, DateTime.Now.ToString("O"), files)));
        Record(files.Select(kv => (System.IO.Path.Combine(dir, kv.Key.Replace('/', System.IO.Path.DirectorySeparatorChar)), kv.Value)));
    }

    static IEnumerable<string> Programs(string dir)
    {
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 8 };
        return Directory.EnumerateFiles(dir, "*.exe", opt).Concat(Directory.EnumerateFiles(dir, "*.dll", opt)).Take(5000);
    }

    /// <summary>Einträge (Pfad, Prüfsumme) in die geschützte Liste schreiben – erzeugt den Schlüssel, falls es noch keinen gibt.</summary>
    static void Record(IEnumerable<(string Path, string Sha)> items)
    {
        foreach (var g in items.GroupBy(i => DirFor(i.Path)))
            if (g.Key is { } d) RecordIn(d, g);
    }

    static void RecordIn(string d, IEnumerable<(string Path, string Sha)> items)
    {
        lock (Gate)
        {
            var key = Key(d, create: true)!;
            var map = Load(d).ToDictionary(e => Norm(e.Path), StringComparer.Ordinal);
            foreach (var (p, sha) in items) map[Norm(p)] = new Entry(System.IO.Path.GetFullPath(p), sha.ToLowerInvariant(), Mac(key, p, sha));
            Directory.CreateDirectory(d);
            AtomicFile.WriteAllText(System.IO.Path.Combine(d, "programm-vertrauen.json"), JsonSerializer.Serialize(new StoreFile(1, map.Values.ToList())));
        }
    }

    static List<Entry> Load(string d)
    {
        var f = System.IO.Path.Combine(d, "programm-vertrauen.json");
        try { return File.Exists(f) ? JsonSerializer.Deserialize<StoreFile>(File.ReadAllText(f))?.Entries ?? [] : []; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>Gültiger Eintrag für genau diese Datei mit dieser (jetzt gerechneten) Prüfsumme?</summary>
    static bool Listed(string d, byte[] key, Dictionary<string, Entry> map, string file)
    {
        if (!map.TryGetValue(Norm(file), out var e)) return false;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(e.Mac), Encoding.ASCII.GetBytes(Mac(key, file, e.Sha256)))) return false;
        try { return string.Equals(Sha(file), e.Sha256, StringComparison.OrdinalIgnoreCase); }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Ist diese .exe ein von Trendo eingerichtetes, unverändertes Programm (unterhalb eines der <paramref name="roots"/>)? Alle .dll im
    /// Ordner der .exe müssen ebenso eingetragen und unverändert sein. Unterordner „.entfernt“ (Sicherung) zählen nie.</summary>
    public static bool Verified(string exe, IEnumerable<string> roots)
    {
        string full;
        try { full = System.IO.Path.GetFullPath(exe); if (!File.Exists(full)) return false; }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        var inside = roots.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(r)) + System.IO.Path.DirectorySeparatorChar)
            .Any(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase) && !full[r.Length..].StartsWith('.') && full[r.Length..].Contains(System.IO.Path.DirectorySeparatorChar));
        return inside && ListedWithDlls(full);
    }

    static bool ListedWithDlls(string full) => (DirFor(full) is { } own ? [own] : AllDirs).Any(d => ListedWithDlls(d, full));

    static bool ListedWithDlls(string d, string full)
    {
        if (Key(d, create: false) is not { } key) return false;
        var map = Load(d).ToDictionary(e => Norm(e.Path), StringComparer.Ordinal);
        if (!Listed(d, key, map, full)) return false;
        try
        {
            foreach (var dll in Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(full)!, "*.dll"))
                if (!Listed(d, key, map, dll)) return false; // N2: hineingelegte oder ausgetauschte DLL neben dem Programm
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
        return true;
    }

    /// <summary>M1: Einmalige Übernahme für Erwachsene – alte Kennungsdateien (mit Prüfsummen) in den Programmordnern werden in die geschützte Liste
    /// übernommen, soweit die Dateien noch genau so sind. Nur in einem Erwachsenen-Profil aufrufen; danach zählt nur noch die Liste.</summary>
    public static int MigrateMarkers(Principal by, IEnumerable<string> roots)
    {
        if (!Rbac.Has(by, Permission.ManageSoftware)) return 0;
        var items = new List<(string, string)>();
        foreach (var root in roots.Where(Directory.Exists).Where(r => DirFor(System.IO.Path.Combine(r, "x")) is { } d && !File.Exists(System.IO.Path.Combine(d, "kennungen-uebernommen-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Norm(r))))[..12]))))
            foreach (var dir in Directory.GetDirectories(root).Where(x => !System.IO.Path.GetFileName(x).StartsWith('.')))
            {
                var mf = System.IO.Path.Combine(dir, Software.PortableMarker);
                if (!File.Exists(mf)) continue;
                Marker? m;
                try { m = JsonSerializer.Deserialize<Marker>(File.ReadAllText(mf)); } catch (Exception e) when (e is JsonException or IOException) { continue; }
                foreach (var (rel, sha) in m?.Dateien ?? [])
                {
                    var f = System.IO.Path.Combine(dir, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    try { if (File.Exists(f) && string.Equals(Sha(f), sha, StringComparison.OrdinalIgnoreCase)) items.Add((f, sha)); } catch (IOException) { }
                }
            }
        if (items.Count > 0) Record(items);
        foreach (var root in roots.Where(Directory.Exists))
            if (DirFor(System.IO.Path.Combine(root, "x")) is { } d) { Directory.CreateDirectory(d); File.WriteAllText(System.IO.Path.Combine(d, "kennungen-uebernommen-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Norm(root))))[..12]), DateTime.Now.ToString("O")); }
        return items.Count;
    }

    // ---------- Schlüssel (DPAPI) ----------
    static byte[]? Key(string d, bool create)
    {
        var f = System.IO.Path.Combine(d, "vertrauen.key");
        try
        {
            if (File.Exists(f)) return Unprotect(File.ReadAllBytes(f));
            if (!create) return null;
            var k = RandomNumberGenerator.GetBytes(32);
            Directory.CreateDirectory(d);
            File.WriteAllBytes(f, Protect(k));
            return k;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException) { return null; }
    }

    static byte[] Protect(byte[] data) => OperatingSystem.IsWindows() ? Dpapi(data, protect: true) : data;
    static byte[] Unprotect(byte[] data) => OperatingSystem.IsWindows() ? Dpapi(data, protect: false) : data;

    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref Blob input, string? desc, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref Blob input, IntPtr desc, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);

    static byte[] Dpapi(byte[] data, bool protect)
    {
        var h = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var input = new Blob { Size = data.Length, Data = h.AddrOfPinnedObject() };
            const int UiForbidden = 0x1;
            var ok = protect ? CryptProtectData(ref input, "Trendo Vertrauensliste", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out var o)
                             : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out o);
            if (!ok) throw new CryptographicException("DPAPI: " + Marshal.GetLastWin32Error());
            try { var r = new byte[o.Size]; Marshal.Copy(o.Data, r, 0, o.Size); return r; }
            finally { LocalFree(o.Data); }
        }
        finally { h.Free(); }
    }

    // ---------- Interpreter, feste Orte, Adressen ----------
    /// <summary>T-0764 (P3-2, N3): Interpreter und Werkzeuge, mit denen sich beliebiger Code ausführen oder etwas installieren lässt – im Kinderprofil
    /// nie vertrauenswürdig, wenn Person oder Datei sie wählen. Blender und LibreOffice (soffice) nur mit fest von Trendo gesetzten Parametern.</summary>
    public static readonly HashSet<string> Interpreters = new(StringComparer.OrdinalIgnoreCase)
    {
        "python", "pythonw", "py", "pyw", "uv", "uvx", "pip", "pip3", "git", "git-bash", "bash", "sh", "node", "npm", "npx", "deno", "bun", "7z", "7za", "7zfm",
        "code", "codium", "vscodium", "adb", "fastboot", "powershell", "pwsh", "cmd", "wscript", "cscript", "mshta", "rundll32", "regsvr32", "regedit", "reg",
        "msiexec", "winget", "choco", "scoop", "java", "javaw", "ruby", "perl", "php", "lua", "conhost", "wt", "schtasks", "sc", "bitsadmin", "certutil", "curl", "wget", "ollama",
        "hh", "mmc", "msbuild", "installutil", "regasm", "regsvcs", "forfiles", "wsl", "wslg", "explorer", "pcalua", "cmstp", "msdt", "odbcconf", "dotnet", "csc",
        "tclsh", "rscript", "blender", "soffice", "soffice.bin",
    };

    public static bool IsInterpreter(string path) => Interpreters.Contains(System.IO.Path.GetFileNameWithoutExtension(path.Trim().Trim('"')));

    /// <summary>T-0764 (M2): Erwarteter Herausgeber (Authenticode) je Programmdatei, die in Benutzerordnern liegen darf (%LOCALAPPDATA%\Programs,
    /// winget-Ablage). Nur bekannte, sicher zugeordnete Einträge – alles andere braucht einen Eintrag in der Vertrauensliste.</summary>
    public static readonly IReadOnlyDictionary<string, string> ExpectedPublishers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Code.exe"] = "Microsoft Corporation",
    };

    /// <summary>Tests: ersetzt die Signaturprüfung (Datei → Herausgeber oder null).</summary>
    public static Func<string, string?>? SignatureOverride { get; set; }

    static bool SignedByExpected(string full)
    {
        if (!ExpectedPublishers.TryGetValue(System.IO.Path.GetFileName(full), out var want)) return false;
        string? pub;
        if (SignatureOverride is { } o) pub = o(full);
        else { var s = Authenticode.Check(full); pub = s.State == SigState.Gueltig ? s.Publisher : null; }
        return pub is not null && string.Equals(pub, want, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>T-0764 (P3-4, M2): Fester Ort für Programme, die Trendo selbst auswählt (z. B. Blender aus der Erkennung) – im Kinderprofil.
    /// „Programme“ und „Programme (x86)“ (nur mit Administratorrechten beschreibbar): ja. „%LOCALAPPDATA%\Programs“ und die winget-Ablage
    /// (für die Person beschreibbar): nur mit gültigem Eintrag in der Vertrauensliste oder gültiger Signatur des erwarteten Herausgebers.</summary>
    public static bool InFixedLocation(string exe)
    {
        string full;
        try { full = System.IO.Path.GetFullPath(exe.Trim().Trim('"')); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        static bool Under(string full, string d) => d.Length > 0 && full.StartsWith(System.IO.Path.TrimEndingDirectorySeparator(d) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (Under(full, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) || Under(full, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86))) return true;
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (local.Length > 0 && (Under(full, System.IO.Path.Combine(local, "Programs")) || Under(full, System.IO.Path.Combine(local, "Microsoft", "WinGet", "Packages"))))
            return File.Exists(full) && (ListedWithDlls(full) || SignedByExpected(full));
        return false;
    }

    /// <summary>T-0764 (P3-3, M3): Adressen, die ein Kinderprofil öffnen darf. Alle anderen Schemata (ms-appinstaller:, search-ms:, ms-msdt:, shell: …)
    /// nicht – auch nicht den Papierkorb im Windows-Explorer (Kinder nutzen Trendos eigenen Dateimanager).</summary>
    public static bool ChildSchemeAllowed(string target)
    {
        var t = target.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return true;
        return t.ToLowerInvariant() is "ms-settings:appsfeatures" or "ms-settings:defaultapps" or "ms-settings:easeofaccess-mousepointer";
    }
}
