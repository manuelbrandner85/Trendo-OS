using System.Diagnostics;

namespace Trendo.Core;

/// <summary>Wie eine Komponente zum Nutzer kommt.</summary>
public enum DeliveryMode
{
    /// <summary>Im Setup.exe enthalten.</summary>
    Bundled,
    /// <summary>Nach einmaliger Zustimmung automatisch pro Benutzer installiert (winget --scope user, ohne Admin).</summary>
    AutoUser,
    /// <summary>Nach Zustimmung automatisch installiert, Windows fragt jedoch nach Administratorrechten (Herstellerinstaller nur systemweit).</summary>
    AutoElevated,
    /// <summary>Nicht weitergebbar oder kontogebunden – Ein-Klick öffnet den offiziellen Installer/Download.</summary>
    Guided,
    /// <summary>Auf dieser Plattform nicht nötig.</summary>
    NotNeeded,
}

public sealed record Component(
    string Id, string Name, string Purpose, string License, DeliveryMode Mode, string Reason,
    string? WingetId = null, int ApproxMb = 0, string? OfficialUrl = null, string[]? Tools = null, string[]? DetectPaths = null,
    string? PostInstall = null, string? PortableZip = null, string? PortableSha256 = null);

/// <summary>Katalog aller Abhängigkeiten (Quelle für Zustimmungsbildschirm, THIRD_PARTY und Tests).</summary>
public static class DependencyCatalog
{
    public const string LocalModel = "qwen2.5:0.5b";
    public const string LargeModel = "qwen2.5:3b";

    public static readonly IReadOnlyList<Component> All =
    [
        new("dotnet", ".NET 10 Runtime", "Laufzeit der App", "MIT", DeliveryMode.Bundled, "Self-contained im Setup enthalten"),
        new("avalonia", "Avalonia UI + SkiaSharp + HarfBuzzSharp", "Oberfläche", "MIT / BSD-3-Clause", DeliveryMode.Bundled, "NuGet-Bibliotheken, frei weitergebbar"),
        new("inter", "Schrift Inter", "UI-Schrift", "SIL OFL 1.1", DeliveryMode.Bundled, "Avalonia.Fonts.Inter, frei weitergebbar"),
        new("velopack", "Velopack", "Installer/Updater", "MIT", DeliveryMode.Bundled, "Bestandteil von Setup.exe"),
        new("yamldotnet", "YamlDotNet", "Spezifikation lesen", "MIT", DeliveryMode.Bundled, "NuGet-Bibliothek"),
        new("wallpaper", "Hintergrund (KI-generiert) + Trendo-Logo (eigener Entwurf)", "Desktop", "Eigenes Projekt-Asset (KI-generiert)", DeliveryMode.Bundled, "Für Trendo OS erstellt"),
        new("webview2", "Microsoft Edge WebView2", "—", "Microsoft", DeliveryMode.NotNeeded, "Trendo OS nutzt keine Web-Ansichten; nicht erforderlich"),
        new("ollama", "Ollama (lokale KI-Laufzeit)", "Trendo KI Assistent offline", "MIT", DeliveryMode.AutoUser,
            "Installer ~1 GB → nicht ins Setup gepackt; pro Benutzer ohne Admin installierbar",
            "Ollama.Ollama", 1100, "https://ollama.com/download", ["ollama"], [@"%LOCALAPPDATA%\Programs\Ollama\ollama.exe"]),
        new("model", $"KI-Modell Qwen2.5 0.5B ({LocalModel})", "Lokales Sprachmodell", "Apache-2.0", DeliveryMode.AutoUser,
            "Gewichte ~400 MB, über Ollama geladen", null, 400, "https://ollama.com/library/qwen2.5", ["model"], null, $"ollama pull {LocalModel}"),
        new("model-3b", $"KI-Modell Qwen2.5 3B ({LargeModel}, optional)", "Größeres lokales Sprachmodell, besseres Deutsch", "Qwen Research License (nicht-kommerziell frei)", DeliveryMode.AutoUser,
            "Gewichte ~1,9 GB, nur nach Zustimmung über Ollama geladen; empfohlen ab 16 GB RAM", null, 1900, "https://ollama.com/library/qwen2.5:3b", ["model-3b"], null, $"ollama pull {LargeModel}"),
        new("git", "Git für Windows", "Web & App Studio, Versionierung", "GPL-2.0", DeliveryMode.AutoUser, "Pro Benutzer installierbar",
            "Git.Git", 350, "https://git-scm.com", ["git"], [@"%LOCALAPPDATA%\Programs\Git\cmd\git.exe", @"%ProgramFiles%\Git\cmd\git.exe"]),
        new("python", "Python 3.13", "Automatisierung, Hilfswerkzeuge", "PSF-2.0", DeliveryMode.AutoUser, "Pro Benutzer installierbar",
            "Python.Python.3.13", 120, "https://python.org", ["python"], [@"%LOCALAPPDATA%\Programs\Python\Python313\python.exe", @"%ProgramFiles%\Python313\python.exe"]),
        new("mpv", "mpv Player", "Medien Studio – Wiedergabe", "GPL-2.0-or-later", DeliveryMode.AutoUser, "winget-Paket ohne Scope-Angabe (Inno, Standard pro Benutzer)",
            "shinchiro.mpv", 60, "https://mpv.io", ["mpv"], [@"%LOCALAPPDATA%\Programs\mpv\mpv.exe", @"%ProgramFiles%\MPV Player\mpv.exe", @"%ProgramFiles%\mpv\mpv.exe"]),
        new("sumatra", "SumatraPDF", "PDF-Ansicht (Basispaket)", "GPL-3.0", DeliveryMode.AutoUser, "Pro Benutzer installierbar (winget --scope user)",
            "SumatraPDF.SumatraPDF", 15, "https://www.sumatrapdfreader.org", ["sumatrapdf"], [@"%LOCALAPPDATA%\SumatraPDF\SumatraPDF.exe", @"%ProgramFiles%\SumatraPDF\SumatraPDF.exe"]),
        new("7zip", "7-Zip", "Archive ZIP/7z (Basispaket)", "LGPL-2.1 + unRAR-Einschränkung", DeliveryMode.AutoElevated, "Nur systemweiter Installer – Windows fragt einmal nach Admin",
            "7zip.7zip", 6, "https://www.7-zip.org", ["7z"], [@"%ProgramFiles%\7-Zip\7z.exe"]),
        new("libreoffice", "LibreOffice", "Office Studio", "MPL-2.0", DeliveryMode.AutoElevated, "Nur systemweiter MSI-Installer – Windows fragt nach Admin",
            "TheDocumentFoundation.LibreOffice", 350, "https://libreoffice.org", ["libreoffice"], [@"%ProgramFiles%\LibreOffice\program\soffice.exe"]),
        new("ffmpeg", "FFmpeg", "Medien-Studio: umwandeln, schneiden, Ton herausholen", "LGPL-2.1-or-later / GPL (Build)", DeliveryMode.AutoUser,
            "Pro Benutzer ohne Admin über winget", "Gyan.FFmpeg", 100, "https://ffmpeg.org", ["ffmpeg"], [@"%LOCALAPPDATA%\Microsoft\WinGet\Links\ffmpeg.exe"]),
        new("blender", "Blender", "3D Studio", "GPL-3.0-or-later", DeliveryMode.AutoElevated, "Nur systemweiter Installer – Windows fragt nach Admin",
            "BlenderFoundation.Blender", 400, "https://blender.org", ["blender"], [@"%ProgramFiles%\Blender Foundation\*\blender.exe"]),
        new("krita", "Krita", "Medien Studio – Grafik", "GPL-3.0", DeliveryMode.AutoElevated, "Systemweiter Installer", "KDE.Krita", 200, "https://krita.org", ["krita"], [@"%ProgramFiles%\Krita (x64)\bin\krita.exe"]),
        new("inkscape", "Inkscape", "Medien Studio – Vektor", "GPL-2.0", DeliveryMode.AutoElevated, "Systemweiter Installer", "Inkscape.Inkscape", 200, "https://inkscape.org", ["inkscape"], [@"%ProgramFiles%\Inkscape\bin\inkscape.exe"]),
        new("kdenlive", "Kdenlive", "Medien Studio – Video", "GPL-3.0", DeliveryMode.AutoUser, "Pro Benutzer installierbar (winget-Benutzerinstaller vorhanden)", "KDE.Kdenlive", 300, "https://kdenlive.org", ["kdenlive"], [@"%LOCALAPPDATA%\Programs\kdenlive\bin\kdenlive.exe", @"%ProgramFiles%\kdenlive\bin\kdenlive.exe"]),
        new("audacity", "Audacity", "Medien Studio – Audio", "GPL-3.0-or-later", DeliveryMode.AutoElevated, "Systemweiter Installer", "Audacity.Audacity", 100, "https://audacityteam.org", ["audacity"], [@"%ProgramFiles%\Audacity\Audacity.exe"]),
        new("qpdf", "qpdf", "Office – PDF zusammenfügen/teilen", "Apache-2.0", DeliveryMode.AutoUser, "Offizielles Portable-ZIP (GitHub-Release), Prüfsumme fest – ohne Admin, nur im Benutzerordner", null, 25, "https://github.com/qpdf/qpdf/releases/tag/v12.4.2", ["qpdf"],
            [@"%LOCALAPPDATA%\TrendoWerkzeuge\qpdf\qpdf-*\bin\qpdf.exe", "/usr/bin/qpdf"], null,
            "https://github.com/qpdf/qpdf/releases/download/v12.4.2/qpdf-12.4.2-msvc64.zip", "db87077e683630c1217e0e8f9a20a9749d952ab676e881c3689187763a5de25d"),
        new("tesseract", "Tesseract OCR", "Letter Studio – Texterkennung", "Apache-2.0", DeliveryMode.AutoElevated, "Systemweiter Installer", "UB-Mannheim.TesseractOCR", 100, "https://github.com/UB-Mannheim/tesseract", ["tesseract"], [@"%ProgramFiles%\Tesseract-OCR\tesseract.exe"]),
        new("unreal", "Unreal Engine", "Game Studio", "Epic EULA (nicht weitergebbar)", DeliveryMode.Guided,
            "Nur über Epic Games Launcher mit Epic-Konto erlaubt – keine automatische Installation", null, 0, "https://www.unrealengine.com/download", ["unreal"], [@"%ProgramFiles%\Epic Games\UE_*\Engine\Binaries\Win64\UnrealEditor.exe"]),
        new("chrome", "Google Chrome (optional)", "Browser", "Google-Lizenz (proprietär)", DeliveryMode.Guided, "Proprietär – nur offizieller Download", null, 0, "https://www.google.com/chrome/"),
        new("grokbuild", "Grok Build", "KI-Programmieragent", "Konto/Berechtigung erforderlich", DeliveryMode.Guided, "Kontogebunden – nur offizieller Zugang", null, 0, "https://x.ai"),
        new("tor", "Tor Browser", "Recherche (read-only)", "BSD-3-Clause u. a.", DeliveryMode.Guided, "Sicherheits-QA ausstehend – nur offizielle Quelle", null, 0, "https://www.torproject.org/download/"),
        new("chromium", "Chromium", "Browser (Linux-Edition)", "BSD-3-Clause", DeliveryMode.NotNeeded, "Windows: Microsoft Edge ist vorhanden; in der Linux-Edition enthalten"),
        new("grokbot", "Grok Bot", "Cloud-Projektleitung", "Kontotarif", DeliveryMode.NotNeeded, "Optionaler Cloudbereich, nicht Teil des OS-Bundles"),
    ];

    public static Component Get(string id) => All.First(c => c.Id == id);
}

public interface IProcessRunner
{
    /// <summary>Startet ein Programm und liefert ExitCode + Ausgabe; meldet Fortschrittszeilen.</summary>
    Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default);
}

public sealed class RealProcessRunner : IProcessRunner
{
    public async Task<(int, string)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() }; // nie im Programmordner: hält sonst Updates auf
        using var p = Process.Start(psi)!;
        var sb = new System.Text.StringBuilder();
        void On(object _, DataReceivedEventArgs e) { if (e.Data is null) return; lock (sb) sb.AppendLine(e.Data); line?.Invoke(e.Data); }
        p.OutputDataReceived += On; p.ErrorDataReceived += On; p.BeginOutputReadLine(); p.BeginErrorReadLine();
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { p.Kill(entireProcessTree: true); } catch { } throw; }
        return (p.ExitCode, sb.ToString());
    }
}

/// <summary>Erkennt und installiert Abhängigkeiten. Installation nur nach Zustimmung des Owners, jede Aktion im Audit-Log.</summary>
public sealed class DependencyManager(AuditLog audit, IProcessRunner runner, Func<string, string>? expand = null, Func<string, bool>? fileExists = null)
{
    private readonly Func<string, string> _expand = expand ?? Environment.ExpandEnvironmentVariables;
    private readonly Func<string, bool> _exists = fileExists ?? FileExistsGlob;
    private readonly bool _customExists = fileExists is not null;
    public string? ConsentFile { get; init; }
    public IProcessRunner Runner { get; set; } = runner;

    public string? ToolsFile { get; init; }
    private Dictionary<string, string> LoadTools() => ToolsFile is not null && File.Exists(ToolsFile)
        ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ToolsFile)) ?? [] : [];
    /// <summary>Gespeicherter (automatisch konfigurierter) Pfad eines bereits vorhandenen Programms.</summary>
    public string? Integrated(string id) => LoadTools().GetValueOrDefault(id);
    public void Integrate(string id, string path)
    {
        if (ToolsFile is null) return;
        var t = LoadTools(); if (t.GetValueOrDefault(id) == path) return;
        t[id] = path; File.WriteAllText(ToolsFile, System.Text.Json.JsonSerializer.Serialize(t, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        audit.Append("app", "deps.integrated", $"{id} → {System.IO.Path.GetFileName(path)}");
    }
    public bool Exists(string p) => _exists(p);
    public string? FileVersion(string p) { try { return System.Diagnostics.FileVersionInfo.GetVersionInfo(p).FileVersion; } catch { return null; } }

    /// <summary>PATH und Registry „App Paths“ (HKCU/HKLM) – findet auch Installationen an ungewöhnlichen Orten.</summary>
    public string? FindOnSystem(Component c)
    {
        if (!StudioTools.Verify.TryGetValue(c.Id, out var v)) return null;
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        { var f = System.IO.Path.Combine(d, v.Exe); if (_exists(f)) return f; }
        if (OperatingSystem.IsWindows())
            foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
                if (hive.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{v.Exe}")?.GetValue(null) is string p && _exists(p.Trim('"'))) return p.Trim('"');
        return null;
    }

    public bool HasConsent => ConsentFile is not null && File.Exists(ConsentFile);

    public void GiveConsent(Principal by)
    {
        Rbac.Demand(by, Permission.ApproveAction);
        if (ConsentFile is null) throw new InvalidOperationException("Kein Speicherort für Zustimmung");
        File.WriteAllText(ConsentFile, $"Zustimmung Lizenzen {DateTimeOffset.Now:o} durch {by.Name}\n");
        audit.Append(by.Name, "deps.consent", string.Join(",", DependencyCatalog.All.Where(c => c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated).Select(c => c.Id)));
    }

    public bool IsInstalled(Component c)
    {
        if (c.Mode is DeliveryMode.Bundled or DeliveryMode.NotNeeded) return true;
        if (c.Id == "model") return ModelPresent;
        if (c.Id == "model-3b") return HasModel(DependencyCatalog.LargeModel);
        return c.DetectPaths?.Any(p => _exists(_expand(p))) == true;
    }

    /// <summary>Modell vorhanden, wenn Ollama-Manifest existiert (echte Prüfung im Dateisystem).</summary>
    public bool ModelPresent => HasModel(DependencyCatalog.LocalModel);

    public bool HasModel(string model)
    {
        var name = model.Split(':');
        var manifest = Path.Combine(_expand("%USERPROFILE%"), ".ollama", "models", "manifests", "registry.ollama.ai", "library", name[0], name[1]);
        return _exists(manifest);
    }

    /// <summary>Ziel für Portable-Werkzeuge (Benutzerordner, kein Admin) – bewusst außerhalb von %LOCALAPPDATA%\\TrendoOS, das Setup/Rückfall komplett ersetzt.</summary>
    public string ToolsRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge");

    public string? ResolvePath(Component c) => c.DetectPaths?.Select(_expand).Where(_exists).Select(p => p.Contains('*') ? ResolveGlob(p) ?? (_customExists ? p : null) : p).FirstOrDefault(p => p is not null);

    /// <summary>Installiert automatisch (AutoUser/AutoElevated). Guided/Bundled werfen.</summary>
    public async Task<bool> InstallAsync(Principal by, Component c, Action<string>? progress = null, CancellationToken ct = default)
    {
        Rbac.Demand(by, Permission.ApproveAction);
        if (!HasConsent) throw new InvalidOperationException("Lizenz-Zustimmung fehlt");
        if (c.Mode is not (DeliveryMode.AutoUser or DeliveryMode.AutoElevated)) throw new InvalidOperationException($"{c.Name} wird nicht automatisch installiert ({c.Mode})");
        if (IsInstalled(c)) { progress?.Invoke($"{c.Name}: bereits vorhanden – nichts geändert"); return true; }
        audit.Append(by.Name, "deps.install.start", c.Id);
        (int exit, string output) r;
        if (c.PostInstall is { } cmd)
        {
            var ollama = ResolvePath(DependencyCatalog.Get("ollama")) ?? throw new InvalidOperationException("Ollama fehlt – zuerst Ollama installieren");
            // Modell-Download braucht den lokalen Dienst: falls nicht aktiv, „ollama serve“ starten (nur 127.0.0.1)
            if (!await OllamaUp())
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ollama, "serve") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(ollama)! }); audit.Append("app", "ki.runtime_started", "ollama serve"); }
                catch { }
                for (var i = 0; i < 30 && !await OllamaUp(); i++) await Task.Delay(500, ct);
            }
            r = await Runner.RunAsync(ollama, cmd["ollama ".Length..], progress, ct);
        }
        else if (c.PortableZip is { } url && c.PortableSha256 is { } sha)
        {
            // Portable: Download mit Windows-eigenem curl.exe (Core bleibt ohne Netzbibliothek), Prüfsumme fest, Entpacken nur in den Benutzerordner
            var tmp = Path.Combine(Path.GetTempPath(), $"trendo-dl-{c.Id}-{Guid.NewGuid():N}.zip");
            try
            {
                r = await Runner.RunAsync(OperatingSystem.IsWindows() ? "curl.exe" : "curl", $"-L --fail --silent --show-error -o \"{tmp}\" {url}", progress, ct);
                if (r.exit == 0) { global::Trendo.Core.Sys.Software.InstallPortable(tmp, sha, ToolsRoot, c.Id, audit); progress?.Invoke($"{c.Name}: Prüfsumme stimmt, entpackt"); }
            }
            catch (InvalidOperationException e) { r = (1, e.Message); progress?.Invoke(e.Message); }
            finally { try { File.Delete(tmp); } catch (IOException) { } }
        }
        else
        {
            var scope = c.Mode == DeliveryMode.AutoUser && c.Id != "mpv" ? " --scope user" : "";
            r = await Runner.RunAsync("winget", $"install --id {c.WingetId} -e --silent{scope} --accept-package-agreements --accept-source-agreements --disable-interactivity", progress, ct);
        }
        var ok = r.exit == 0 && IsInstalled(c);
        audit.Append(by.Name, ok ? "deps.install.ok" : "deps.install.fail", $"{c.Id} exit={r.exit}");
        return ok;
    }

    /// <summary>Prüft, ob der lokale Ollama-Dienst antwortet (reiner TCP-Test auf 127.0.0.1:11434).</summary>
    /// <summary>Vom Host gesetzt (App prüft 127.0.0.1:11434); Core selbst hat keine Netz-Abhängigkeit.</summary>
    public Func<Task<bool>> OllamaUp { get; set; } = () => Task.FromResult(true);

    private static string? ResolveGlob(string p)
    {
        if (!p.Contains('*')) return File.Exists(p) ? p : null;
        var parts = p.Split(Path.DirectorySeparatorChar, '/');
        IEnumerable<string> cur = [parts[0] + Path.DirectorySeparatorChar];
        foreach (var part in parts.Skip(1))
            cur = cur.SelectMany(d => !Directory.Exists(d) ? [] : part.Contains('*')
                ? Directory.EnumerateFileSystemEntries(d, part) : [Path.Combine(d, part)]);
        return cur.FirstOrDefault(File.Exists);
    }
    private static bool FileExistsGlob(string p) => ResolveGlob(p) is not null;
}
