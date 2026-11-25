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
        // T-0764 (5. Prüfrunde M2): alle Dateien des Programmordners samt Unterordnern (Skripte, app.asar, Daten …) – außer der Kennungsdatei
        return Directory.EnumerateFiles(dir, "*", opt).Where(f => !string.Equals(System.IO.Path.GetFileName(f), Software.PortableMarker, StringComparison.OrdinalIgnoreCase)).Take(50_000);
    }

    /// <summary>T-0764 (6. Prüfrunde C): Dateiendungen, die als Code gelten (ausführbar, ladbar oder von einem Interpreter gelesen) – sie bleiben in der
    /// Ordnerprüfung immer streng, auch in Zwischenspeicher- und Protokollordnern. Ebenso Dateien ohne Endung. <c>.pyc</c> ist bewusst Code: Python führt
    /// eine .pyc auch dann aus, wenn ihre Kopfdaten zur .py passen, der Inhalt aber ein anderer ist.</summary>
    public static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pyd", ".py", ".pyc", ".pyo", ".pyw", ".asar", ".js", ".mjs", ".cjs", ".lua", ".ps1", ".psm1", ".bat", ".cmd", ".vbs", ".vbe", ".wsf", ".wsh",
        ".jar", ".class", ".com", ".scr", ".cpl", ".ocx", ".sys", ".msi", ".msp", ".hta", ".lnk", ".url", ".node", ".so", ".dylib", ".sh", ".rb", ".pl", ".php", ".tcl", ".gd", ".pck",
        ".pth", // 7. Prüfrunde: Python führt „import …“-Zeilen in .pth-Dateien beim Start aus
    };

    /// <summary>T-0764 (7. Prüfrunde): reine Datenendungen, die in <c>cache</c>/<c>logs</c> ausgenommen sind (Bilder, Zwischenstände). Nicht dabei:
    /// .json, .ini, .txt, .cfg, .xml, .yaml (Einstellungen können Befehlszeilen enthalten), .bin/.dat (Electron lädt z. B. snapshot_blob.bin).</summary>
    public static readonly HashSet<string> DataExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tmp", ".cache",
    };

    /// <summary>T-0764 (6./7. Prüfrunde C): Dateien, die ein Programm beim Laufen selbst anlegt und die die Ordnerprüfung nicht sperren sollen – Protokolle
    /// (<c>*.log</c>) überall und in Ordnern namens <c>cache</c> oder <c>logs</c> nur reine Daten (<see cref="DataExtensions"/>); Code und Dateien ohne
    /// Endung nie.
    /// Nicht ausgenommen: Einstellungsdateien und -ordner (z. B. SumatraPDF-settings.txt, mpv „portable_config“), denn sie können Befehlszeilen oder
    /// Skripte enthalten, und <c>__pycache__</c>/<c>.pyc</c> (siehe <see cref="CodeExtensions"/>).</summary>
    public static bool IgnoredInFolderCheck(string folder, string file)
    {
        var ext = System.IO.Path.GetExtension(file);
        if (ext.Length == 0 || CodeExtensions.Contains(ext)) return false;
        if (string.Equals(ext, ".log", StringComparison.OrdinalIgnoreCase)) return true;
        var rel = System.IO.Path.GetRelativePath(folder, file);
        var parts = rel.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        return DataExtensions.Contains(ext) && parts[..^1].Any(p => p.Equals("cache", StringComparison.OrdinalIgnoreCase) || p.Equals("logs", StringComparison.OrdinalIgnoreCase));
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
        return inside && ListedFolder(full, roots);
    }

    /// <summary>T-0764 (5. Prüfrunde M2): Programmordner (direkt unter einem Wurzelordner) – jede Datei darin, auch in Unterordnern, muss mit
    /// gültigem Eintrag und unveränderter Prüfsumme in der Liste stehen; jede neue oder ausgetauschte Datei führt zur Ablehnung (fail-closed).</summary>
    static bool ListedFolder(string full, IEnumerable<string> roots)
    {
        if (ProgramFolder(full, roots) is not { } folder) return false;
        if (DirFor(full) is not { } d || Key(d, create: false) is not { } key) return false;
        var map = Load(d).ToDictionary(e => Norm(e.Path), StringComparer.Ordinal);
        if (!Listed(d, key, map, full)) return false;
        try { return Programs(folder).All(f => Listed(d, key, map, f) || IgnoredInFolderCheck(folder, f)); } // 6. Prüfrunde C: Protokolle, Zwischenspeicher
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Programmordner einer Datei: der Ordner direkt unter dem Wurzelordner, der sie enthält.</summary>
    static string? ProgramFolder(string full, IEnumerable<string> roots)
    {
        foreach (var r in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(r)) + System.IO.Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            var top = full[root.Length..].Split(System.IO.Path.DirectorySeparatorChar)[0];
            if (top.Length > 0 && !top.StartsWith('.')) return System.IO.Path.Combine(root, top);
        }
        return null;
    }

    /// <summary>Steht für diese Datei überhaupt ein Eintrag in der geschützten Liste (unabhängig vom jetzigen Inhalt)?</summary>
    public static bool IsKnown(string path)
    {
        try { return DirFor(path) is { } d && Load(d).Any(e => Norm(e.Path) == Norm(path)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>T-0764 (5. Prüfrunde M1): Liegt das Programm in einem Ordner mit alter Kennungsdatei (älterer Stand, nicht in der geschützten Liste)?
    /// Dann soll ein Erwachsener es einmal neu einrichten.</summary>
    public static bool HasLegacyMarker(string path)
    {
        if (IsKnown(path)) return false; // in der geschützten Liste (dann ist der Ordner nur verändert – keine Neu-Einrichtungs-Meldung)
        try
        {
            for (var d = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)); d is not null; d = System.IO.Path.GetDirectoryName(d))
            {
                if (File.Exists(System.IO.Path.Combine(d, Software.PortableMarker))) return true;
                if (Stores.Keys.Any(k => string.Equals(System.IO.Path.TrimEndingDirectorySeparator(k), d, StringComparison.OrdinalIgnoreCase))) break;
            }
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { }
        return false;
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

    // T-0764 (5. Prüfrunde M1): Die Übernahme alter Kennungsdateien ist ersatzlos gestrichen – ältere Stände richtet ein Erwachsener einmal neu ein.

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
    public sealed record ExpectedSigner(string Publisher, string OriginalFilename, string ProductName);
    public static readonly IReadOnlyDictionary<string, ExpectedSigner> ExpectedPublishers = new Dictionary<string, ExpectedSigner>(StringComparer.OrdinalIgnoreCase)
    {
        ["Code.exe"] = new("Microsoft Corporation", "electron.exe", "Visual Studio Code"),
    };

    /// <summary>T-0764 (5. Prüfrunde H2): nur eingebettete, gültige Signatur (keine Katalog-Signatur, Sperrlisten geprüft) des erwarteten Herausgebers
    /// UND passende Versionsinfos (OriginalFilename, ProductName). Eine kopierte, umbenannte Windows-Datei (z. B. cmd.exe als Code.exe) zählt nicht.</summary>
    public static bool SignedByExpected(string full)
    {
        if (!ExpectedPublishers.TryGetValue(System.IO.Path.GetFileName(full), out var want)) return false;
        var s = Authenticode.CheckEmbeddedStrict(full);
        if (s.State != SigState.Gueltig || s.ViaCatalog || !string.Equals(s.Publisher, want.Publisher, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(full);
            return string.Equals(v.OriginalFilename?.Trim(), want.OriginalFilename, StringComparison.OrdinalIgnoreCase)
                && string.Equals(v.ProductName?.Trim(), want.ProductName, StringComparison.OrdinalIgnoreCase);
        }
        catch (FileNotFoundException) { return false; }
    }

    /// <summary>T-0764 (5. Prüfrunde H1): Programme mit eigener Skript-Konsole bzw. Makros (IDEs, Blender, Unreal-, Godot-Editor, LibreOffice …).
    /// Im Kinderprofil starten sie nie mit Oberfläche – auch nicht bei festen Aufrufen von Trendo. Ausnahme außerhalb dieser Prüfung:
    /// Blender im Hintergrund (-b) mit Trendos eigenen festen Skripten.</summary>
    public static readonly HashSet<string> ConsolePrograms = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "code-insiders", "codium", "vscodium", "cursor", "windsurf", "zed", "blender", "blender-launcher", "unrealeditor", "unrealeditor-cmd", "ue4editor",
        "soffice", "soffice.bin", "swriter", "scalc", "simpress", "sdraw", "sbase", "smath", "devenv", "rider64", "idea64", "pycharm64", "webstorm64", "clion64",
        "studio64", "eclipse", "notepad++", "sublime_text", "atom", "arduino", "arduino ide", "processing", "spyder", "jupyter", "jupyter-lab", "thonny", "idle", "kate", "geany",
        "unity", "unity hub", "excel", "winword", "powerpnt", "msaccess",
    };

    /// <summary>T-0764 (6. Prüfrunde A): ERLAUBNISLISTE für das Kinderprofil – statt einer Sperrliste, die nie vollständig wird. Ein Kind startet ein
    /// fremdes Programm nur, wenn es genau diese Vollpaket-Komponente ist (Ordner direkt unter einem angemeldeten Programmordner + Pfad darin) UND
    /// geprüft ist (geschützte Liste, ganzer Ordner unverändert). Aufgenommen sind nur reine Betrachter ohne Skript-Konsole, Makros oder Erweiterungen:
    /// 7. Prüfrunde: Die Liste ist LEER – Kinder starten keine Fremdprogramme mehr. SumatraPDF wurde entfernt (liest „ExternalViewers“ und
    /// „InverseSearchCmdLine“ aus %LOCALAPPDATA%, startet damit beliebige Befehle); PDFs öffnen Kinder in Trendos eigenem PDF-Betrachter.
    /// Der Mechanismus bleibt, damit ein späterer, geprüfter Eintrag möglich ist.
    /// Bewusst NICHT aufgenommen (geprüft): SumatraPDF (siehe oben), mpv (eingebaute Konsole mit Befehl „run“ startet beliebige Programme; Lua-Skripte aus dem Benutzerordner),
    /// VLC (Lua-Erweiterungen), Krita, GIMP, Inkscape (Python-Erweiterungen/Skript-Konsole), OBS (Python/Lua-Skripte), Audacity (Makros, Nyquist,
    /// mod-script-pipe), Kdenlive, LibreOffice (Makros), Blender, Godot, Unreal, Unity, alle Editoren, f3d (Befehlskonsole), mGBA (Lua-Skripte),
    /// scrcpy/adb (steuert das Handy), 7-Zip, Git, Python. Trendos eigene Werkstätten laufen in Trendo selbst und brauchen keinen Eintrag.</summary>
    public static readonly IReadOnlyList<(string Component, string Exe)> ChildPrograms = [];

    /// <summary>T-0764 (6. Prüfrunde A): Steht <paramref name="exe"/> in der Erlaubnisliste für Kinder – Abgleich am Programmordner (Komponente) und am
    /// genauen Pfad darin, nicht nur am Dateinamen? (Die Prüfung „unverändert“ macht der Aufrufer über die geschützte Liste.)</summary>
    public static bool InChildAllowList(string exe) => InAllowList(exe, ChildPrograms);

    /// <summary>Abgleich gegen eine Liste (Komponente, Pfad darin) unter den angemeldeten Programmordnern – getrennt prüfbar, ohne die Liste zu ändern.</summary>
    public static bool InAllowList(string exe, IEnumerable<(string Component, string Exe)> list)
    {
        string full;
        try { full = System.IO.Path.GetFullPath(exe.Trim().Trim('"')); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        foreach (var root in Stores.Keys)
        {
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            var rel = full[root.Length..];
            if (list.Any(c => string.Equals(rel, System.IO.Path.Combine(c.Component, c.Exe), StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }

    public static bool IsConsoleProgram(string path)
    {
        var n = System.IO.Path.GetFileNameWithoutExtension(path.Trim().Trim('"'));
        return ConsolePrograms.Contains(n) || n.StartsWith("godot", StringComparison.OrdinalIgnoreCase);
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
