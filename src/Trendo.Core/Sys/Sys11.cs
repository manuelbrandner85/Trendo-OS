using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Sys;

/// <summary>SYS-11 (Katalog freigegeben 21.10.2026): Programme & Integration. Nur lesen, anzeigen, anleiten – Änderungen nur nach Bestätigung.</summary>
public static class Software
{
    // P06 Updates nur anzeigen
    public static string UpdateSummary(IReadOnlyList<PackageInfo> ups) => ups.Count == 0 ? "Alles aktuell." : $"{ups.Count} Aktualisierungen verfügbar. Trendo aktualisiert nichts automatisch – du entscheidest je Programm.";

    // P07 Portable Tools
    /// <summary>Entpackt eine Portable-ZIP nach „Werkzeuge/&lt;name&gt;“ im Benutzerordner – nur bei passender Prüfsumme.</summary>
    /// <param name="by">T-0764: angemeldete Person – ein Kinderprofil richtet nichts ein (<see cref="Rbac.DemandSoftware"/>).</param>
    public static string InstallPortable(Principal by, string zip, string expectedSha256, string toolsRoot, string name, AuditLog? audit = null)
    {
        Rbac.DemandSoftware(by, "portable.install " + Path.GetFileName(zip), audit);
        var sha = J.Sha(zip);
        if (!sha.Equals(Regex.Replace(expectedSha256, @"\s", ""), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Prüfsumme stimmt nicht – nichts entpackt.");
        var safe = SafeName(name);
        var dest = Path.Combine(toolsRoot, safe); if (Directory.Exists(dest)) Directory.Delete(dest, true);
        Archives.Unpack(zip, dest);
        File.WriteAllText(Path.Combine(dest, PortableMarker), $"{{\"name\":\"{safe}\",\"sha256\":\"{sha}\",\"am\":\"{DateTime.Now:O}\"}}"); // T-0764: Kennung „von Trendo eingerichtet“
        audit?.Append(by.Name, "portable.install", $"{safe} sha256={sha}"); return dest;
    }

    /// <summary>T-0764: Kennungsdatei, die <see cref="InstallPortable"/> in jeden Programmordner schreibt – nur solche Ordner führt die Programm-Seite.</summary>
    public const string PortableMarker = ".trendo-portable.json";

    /// <summary>Ordnername für ein Portable-Programm: Buchstaben, Ziffern, „-“, „_“ und „.“ – aber nie „.“, „..“ oder mit Punkt am Anfang
    /// (T-0764: „..“ zeigte vorher auf den übergeordneten Ordner, und „Ersetzen“ hätte ihn gelöscht).</summary>
    public static string SafeName(string name)
    {
        var safe = string.Concat((name ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')).TrimEnd('.');
        if (safe.Length == 0 || safe[0] == '.') throw new ArgumentException("Bitte einen Namen aus Buchstaben oder Ziffern.");
        return safe;
    }

    /// <summary>T-0764 (Gegenprüfung M3): Dateien, deren Öffnen etwas installiert, entfernt oder Befehle ausführt – Installer, Pakete, Skripte,
    /// Registrierungsdateien und .exe mit „setup“, „install“, „unins“ oder „update“ im Namen. Sie brauchen das Recht „Programme verwalten“.
    /// Normale Dokumente, Bilder und gewöhnliche Programme öffnen bleibt für alle erlaubt.</summary>
    public static bool IsInstallerLike(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".msi" or ".msp" or ".msix" or ".msixbundle" or ".appx" or ".appxbundle" or ".appinstaller" or ".bat" or ".cmd" or ".ps1" or ".psm1" or ".vbs" or ".vbe" or ".wsf" or ".wsh" or ".js" or ".jse" or ".reg" or ".scr" or ".com" or ".pif"
            or ".lnk" or ".url" or ".cpl" or ".hta" or ".application" or ".appref-ms" or ".msu" or ".jar" or ".diagcab" or ".settingcontent-ms" or ".gadget" or ".msc") return true; // T-0764 N-D
        if (ext is not ".exe") return false;
        var n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return n.Contains("setup") || n.Contains("install") || n.Contains("unins") || n.Contains("update") || n.Contains("deinstall");
    }

    /// <summary>T-0764 (2. Prüfrunde N-C/N-D): Braucht das Öffnen dieses Ziels das Recht „Programme verwalten“? Ja bei Installern, Paketen, Skripten,
    /// Verknüpfungen (.lnk/.url – ihr Ziel ist nicht geprüft) und bei jeder .exe außerhalb von Trendos eigenen Programmordnern
    /// (<paramref name="trusted"/>). Adressen (https:, ms-settings:, shell: …), Ordner und Dokumente: nein.</summary>
    public static bool NeedsSoftwareRight(string target, Func<string, bool>? trusted = null)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        var t = target.Trim().Trim('"');
        if (Regex.IsMatch(t, @"^[A-Za-z][A-Za-z0-9+.\-]+:(?![\\/])") || t.Contains("://", StringComparison.Ordinal)) return false; // Adresse, kein Dateipfad (C:\ ist eine Datei)
        string full;
        try { full = Path.GetFullPath(t); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return true; }
        if (Directory.Exists(full)) return false;
        if (IsInstallerLike(full)) return true;
        return Path.GetExtension(full).Equals(".exe", StringComparison.OrdinalIgnoreCase) && !(trusted?.Invoke(full) ?? false);
    }

    /// <summary>T-0764: Offline-Installer starten – nur Erwachsene, und nur, wenn die Prüfsumme jetzt (beim Start) noch stimmt.
    /// Zwischen „Prüfen“ und „Ja, starten“ ausgetauschte Dateien werden so nicht gestartet.</summary>
    public static void StartOfflineInstaller(Principal by, string file, Integrations integrations, AuditLog audit)
    {
        Rbac.DemandSoftware(by, "offline.installer " + Path.GetFileName(file), audit);
        var full = Path.GetFullPath(file);
        var pkg = OfflinePackages(Path.GetDirectoryName(full)!).FirstOrDefault(p => string.Equals(Path.GetFullPath(p.File), full, StringComparison.OrdinalIgnoreCase));
        if (pkg is not { Ok: true }) throw new InvalidOperationException("Die Prüfsumme stimmt nicht (mehr) – der Installer wird nicht gestartet.");
        audit.Append(by.Name, "offline.installer.start", $"{Path.GetFileName(full)} sha256={pkg.ActualSha}");
        integrations.LaunchDirect(by, full, audit);
    }

    // P09 Deinstallation anleiten
    public static readonly string[] UninstallSteps = ["Windows-Einstellungen öffnen (Knopf unten)", "„Apps“ → „Installierte Apps“ wählen", "Programm in der Liste suchen", "Auf „…“ klicken und „Deinstallieren“ wählen", "Den Anweisungen des Programms folgen"];
    public const string UninstallSettings = "ms-settings:appsfeatures";

    // P10 Verknüpfungen mit Parametern
    public sealed record Shortcut(string Name, string Exe, string[] Args);
    public static string ShortcutFile(string dataRoot) => Path.Combine(dataRoot, "programme", "verknuepfungen.json");
    public static List<Shortcut> Shortcuts(string dataRoot) => J.Load<Shortcut>(ShortcutFile(dataRoot));
    public static void AddShortcut(string dataRoot, string name, string exe, string args)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bitte einen Namen.");
        if (!File.Exists(exe)) throw new ArgumentException("Das Programm gibt es nicht: " + exe);
        var l = Shortcuts(dataRoot); l.RemoveAll(s => s.Name == name); l.Add(new(name.Trim(), exe, SplitArgs(args).ToArray())); J.Save(ShortcutFile(dataRoot), l);
    }
    public static void RemoveShortcut(string dataRoot, string name) { var l = Shortcuts(dataRoot); l.RemoveAll(s => s.Name == name); J.Save(ShortcutFile(dataRoot), l); }
    /// <summary>Teilt Parameter wie die Kommandozeile (Anführungszeichen halten Leerzeichen zusammen).</summary>
    public static IEnumerable<string> SplitArgs(string s) => Regex.Matches(s ?? "", @"""([^""]*)""|(\S+)").Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);

    // P11 Autostart
    public sealed record AutostartItem(string Name, string Command, string Where, bool Enabled);
    public static List<AutostartItem> StartupFolder(string folder, string? disabledFolder = null)
    {
        var l = new List<AutostartItem>();
        if (Directory.Exists(folder)) l.AddRange(Directory.GetFiles(folder).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)).Select(f => new AutostartItem(Path.GetFileNameWithoutExtension(f), f, "Autostart-Ordner", true)));
        if (disabledFolder is not null && Directory.Exists(disabledFolder)) l.AddRange(Directory.GetFiles(disabledFolder).Select(f => new AutostartItem(Path.GetFileNameWithoutExtension(f), f, "Autostart-Ordner", false)));
        return l;
    }
    /// <summary>Ein-/Ausschalten nur mit Bestätigung: Verknüpfung wird in einen „deaktiviert“-Ordner verschoben (nichts gelöscht).</summary>
    public static void SetStartupEnabled(AutostartItem it, string folder, string disabledFolder, bool on, bool confirmed, AuditLog? audit = null)
    {
        if (!confirmed) throw new InvalidOperationException("Ändern braucht deine Bestätigung.");
        Directory.CreateDirectory(on ? folder : disabledFolder);
        var to = Path.Combine(on ? folder : disabledFolder, Path.GetFileName(it.Command)); File.Move(it.Command, to);
        audit?.Append("app", "autostart." + (on ? "on" : "off"), it.Name);
    }

    // P14 Offline-Pakete
    public sealed record OfflinePackage(string File, string? ExpectedSha, string? ActualSha, bool Ok);
    /// <summary>Installer in einem Ordner; Prüfsumme aus „datei.sha256“ oder „SHA256SUMS(.txt)“.</summary>
    public static List<OfflinePackage> OfflinePackages(string folder)
    {
        var sums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in Directory.GetFiles(folder, "SHA256SUMS*"))
            foreach (var l in File.ReadAllLines(s)) if (Regex.Match(l, @"^([0-9a-fA-F]{64})\s+\*?(.+)$") is { Success: true } m) sums[Path.GetFileName(m.Groups[2].Value.Trim())] = m.Groups[1].Value.ToLowerInvariant();
        var res = new List<OfflinePackage>();
        foreach (var f in Directory.GetFiles(folder).Where(f => Path.GetExtension(f).ToLowerInvariant() is ".exe" or ".msi" or ".msix" or ".zip"))
        {
            var side = f + ".sha256"; var exp = File.Exists(side) ? Regex.Match(File.ReadAllText(side), "[0-9a-fA-F]{64}").Value.ToLowerInvariant() : sums.GetValueOrDefault(Path.GetFileName(f));
            if (string.IsNullOrEmpty(exp)) { res.Add(new(f, null, null, false)); continue; }
            var act = J.Sha(f); res.Add(new(f, exp, act, act == exp));
        }
        return res;
    }

    // P15 Systemvoraussetzungen
    public sealed record Requirement(string Name, long MinRamBytes, long MinDiskBytes, int MinWindowsBuild);
    public static readonly Requirement[] Known =
    [
        new("Blender", 8L << 30, 2L << 30, 17763), new("Unreal Engine 5", 32L << 30, 150L << 30, 19041), new("LibreOffice", 2L << 30, 2L << 30, 17763),
        new("Visual Studio Code", 2L << 30, 1L << 30, 17763), new("Ollama + KI-Modell 3B", 8L << 30, 6L << 30, 19041), new("OBS Studio", 4L << 30, 1L << 30, 19041),
    ];
    /// <summary>Leer = passt. Sonst Gründe (dann BLOCKED – Trendo installiert nicht).</summary>
    public static List<string> Check(Requirement r, long ramBytes, long freeDiskBytes, int windowsBuild)
    {
        var l = new List<string>();
        if (ramBytes < r.MinRamBytes) l.Add($"zu wenig Arbeitsspeicher ({ramBytes / (double)(1L << 30):0.#} GB, nötig {r.MinRamBytes >> 30} GB)");
        if (freeDiskBytes < r.MinDiskBytes) l.Add($"zu wenig freier Platz ({freeDiskBytes / (double)(1L << 30):0.#} GB, nötig {r.MinDiskBytes >> 30} GB)");
        if (windowsBuild > 0 && windowsBuild < r.MinWindowsBuild) l.Add($"Windows zu alt (Build {windowsBuild}, nötig {r.MinWindowsBuild})");
        return l;
    }

    // P18 Konfiguration zurücksetzen – vorher Sicherung
    public static string ResetSettings(string dataRoot, AuditLog? audit = null)
    {
        var f = UserSettings.PathIn(dataRoot); var dir = Path.Combine(dataRoot, "sicherungen"); Directory.CreateDirectory(dir);
        var bak = Path.Combine(dir, $"settings-{DateTime.Now:yyyyMMdd-HHmmssfff}.json");
        if (File.Exists(f)) { File.Copy(f, bak); File.Delete(f); } else File.WriteAllText(bak, "{}");
        audit?.Append("app", "settings.reset", Path.GetFileName(bak)); return bak;
    }
    public static void RestoreSettings(string dataRoot, string backup) => File.Copy(backup, UserSettings.PathIn(dataRoot), true);

    // P19 Dateizuordnungen
    public static readonly string[] CommonExtensions = [".pdf", ".docx", ".xlsx", ".txt", ".jpg", ".png", ".mp3", ".mp4", ".zip", ".html", ".odt", ".blend"];
    public static string ProgIdName(string? progId) => progId switch
    {
        null or "" => "Windows-Standard", var p when p.StartsWith("AcroExch") => "Adobe Acrobat", var p when p.StartsWith("SumatraPDF") => "SumatraPDF",
        var p when p.StartsWith("Word.") => "Microsoft Word", var p when p.StartsWith("Excel.") => "Microsoft Excel", var p when p.StartsWith("LibreOffice") => "LibreOffice",
        var p when p.StartsWith("VLC") => "VLC", var p when p.StartsWith("mpv") => "mpv", var p when p.StartsWith("ChromeHTML") => "Google Chrome", var p when p.StartsWith("MSEdge") => "Microsoft Edge",
        var p when p.StartsWith("FirefoxHTML") => "Firefox", var p when p.StartsWith("7-Zip") => "7-Zip", var p when p.StartsWith("AppX") => "Windows-App", var p => p,
    };

    // P20 Kompatibilitätshinweise
    public static readonly Dictionary<string, string> CompatNotes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blender"] = "Braucht eine Grafikkarte mit OpenGL 4.3. Auf alten Laptops mit Intel-Grafik oft langsam.",
        ["libreoffice"] = "Öffnet Word-Dateien gut; aufwendige Layouts und Makros können abweichen.",
        ["ollama"] = "Läuft ohne Grafikkarte, ist dann aber langsamer. 3B-Modell braucht etwa 4 GB freien Arbeitsspeicher.",
        ["ffmpeg"] = "Reines Kommandozeilenprogramm – Trendo bedient es für dich.",
        ["git"] = "Bei der Installation die Standardeinstellungen lassen.",
        ["mpv"] = "Spielt fast alles ab; keine Mediathek – die bringt Trendo mit.",
        ["sumatra"] = "Sehr schneller PDF-Betrachter; kann keine PDF-Formulare speichern.",
        ["7zip"] = "Installer verlangt Administratorrechte (Windows fragt einmal).",
        ["unreal"] = "Sehr groß (über 100 GB) und braucht einen Epic-Account – Trendo installiert es nie selbst.",
        ["python"] = "Bei der Installation „Add to PATH“ angehakt lassen.",
    };
    public static string CompatFor(string id) => CompatNotes.GetValueOrDefault(id) ?? "Keine bekannten Probleme.";

    // P21 Integrationstests
    public sealed record ToolTest(string Tool, bool Ok, string Version, int Ms);
    public static ToolTest Test(string tool, string exe, string args = "--version")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { var r = Cli.Run(exe, Software.SplitArgs(args), timeoutMs: 20000); var v = (r.Out + r.Err).Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? ""; return new(tool, r.Ok, v.Length > 120 ? v[..120] : v, (int)sw.ElapsedMilliseconds); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { return new(tool, false, e.Message, (int)sw.ElapsedMilliseconds); }
    }
    public static string TestLog(IEnumerable<ToolTest> tests) => $"Integrationstest {DateTime.Now:dd.MM.yyyy HH:mm}\n" + string.Join("\n", tests.Select(t => $"{(t.Ok ? "OK  " : "FEHLER")} {t.Tool,-12} {t.Ms,5} ms  {t.Version}"));

    // P22 Softwareliste CSV, P23 Wiederherstellungsliste
    public static string ToCsv(IEnumerable<PackageInfo> list)
    {
        static string Q(string s) => s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        return "\uFEFFName;Kennung;Version;Quelle\r\n" + string.Concat(list.Select(p => $"{Q(p.Name)};{Q(p.Id)};{Q(p.Version)};{Q(p.Source)}\r\n"));
    }
    /// <summary>Datei im Format von „winget export“ – auf dem neuen Rechner mit „winget import -i datei.json“ einlesbar.</summary>
    public static string WingetExport(IEnumerable<PackageInfo> list) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["$schema"] = "https://aka.ms/winget-packages.schema.2.0.json", ["CreationDate"] = DateTime.UtcNow.ToString("O"), ["WinGetVersion"] = "1.9",
        ["Sources"] = new[] { new Dictionary<string, object> { ["Packages"] = list.Where(p => PackageParsers.SafeId(p.Id) && p.Id.Contains('.') && (p.Source is "" or "winget")).Select(p => new Dictionary<string, string> { ["PackageIdentifier"] = p.Id }).ToArray(),
            ["SourceDetails"] = new Dictionary<string, string> { ["Argument"] = "https://cdn.winget.microsoft.com/cache", ["Identifier"] = "Microsoft.Winget.Source_8wekyb3d8bbwe", ["Name"] = "winget", ["Type"] = "Microsoft.PreIndexed.Package" } } },
    }, new JsonSerializerOptions { WriteIndented = true });
    public static List<string> ImportIds(string json)
    {
        using var d = JsonDocument.Parse(json);
        return d.RootElement.GetProperty("Sources").EnumerateArray().SelectMany(s => s.GetProperty("Packages").EnumerateArray()).Select(p => p.GetProperty("PackageIdentifier").GetString()!).ToList();
    }

    // P24 Speicherbedarf
    public static List<(string Name, long Bytes)> ToolSizes(IEnumerable<string> folders) =>
        folders.Where(Directory.Exists).SelectMany(Directory.GetDirectories).Select(d => (Path.GetFileName(d), FileAnalysis.FolderSize(d))).OrderByDescending(x => x.Item2).ToList();

    // P25 Selbsttest-Bericht ohne private Pfade
    public static string Redact(string text)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); var user = Environment.UserName;
        if (home.Length > 3) text = text.Replace(home, "~", StringComparison.OrdinalIgnoreCase);
        text = Regex.Replace(text, @"([A-Za-z]:\\Users\\|/home/|/Users/)[^\\/\s]+", m => m.Groups[1].Value + "<BENUTZER>");
        if (user.Length > 2) text = Regex.Replace(text, $@"\b{Regex.Escape(user)}\b", "<BENUTZER>", RegexOptions.IgnoreCase);
        return Regex.Replace(text, @"[\w.+-]+@[\w-]+\.[\w.]+", "<E-MAIL>");
    }
    public static string SelfTestReport(TrendoCore core, IEnumerable<ToolTest> tools, string version)
    {
        var sb = new StringBuilder($"Trendo OS Selbsttest – Version {version} – {DateTime.Now:dd.MM.yyyy HH:mm}\n");
        sb.AppendLine($"Betriebssystem: {Environment.OSVersion.VersionString}, {Environment.ProcessorCount} Kerne, .NET {Environment.Version}");
        sb.AppendLine($"Datenordner: {core.DataRoot} – beschreibbar: {Writable(core.DataRoot)}");
        sb.AppendLine($"Protokoll-Kette unverändert: {(core.Audit.Verify() ? "ja" : "NEIN")}");
        sb.AppendLine($"Offline-Modus: {(core.Offline ? "an" : "aus")}");
        sb.AppendLine(TestLog(tools));
        return Redact(sb.ToString());
    }
    static bool Writable(string d) { try { var f = Path.Combine(d, ".schreibtest"); File.WriteAllText(f, "x"); File.Delete(f); return true; } catch (Exception) { return false; } }
}
