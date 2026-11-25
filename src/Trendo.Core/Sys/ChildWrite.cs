using System.Collections.Concurrent;

namespace Trendo.Core.Sys;

/// <summary>T-0764 (11. Prüfrunde): FREIGABELISTE für Schreibzugriffe im Kinderprofil – statt einer Sperrliste, die nie vollständig wird (Befund:
/// Kopieren einer setup.exe, .lnk oder .url in den Autostart-Ordner unter %APPDATA% – Windows startet sie bei der nächsten Anmeldung).
/// Ein Kind schreibt nur in freigegebene Orte: Dokumente, Bilder, Musik, Videos, Downloads und Desktop des Kontos (bekannte Ordner, aufgelöst –
/// auch bei OneDrive-Umleitung zählt das Ziel), seinen eigenen Ordner in Trendo (nur, wenn er selbst kein Systemordner ist) und „projekte“ im
/// Datenordner. Alles andere (AppData, Autostart, Startmenü, SendTo, ProgramData, Programme, Windows, Temp, andere Benutzer, Laufwerkswurzeln,
/// Wechseldatenträger …) ist für Kinder schreibgeschützt – fail-closed. Junctions und Symlinks werden aufgelöst: zählt nur, wenn Pfad UND Ziel in
/// der Freigabe liegen. <see cref="ProtectedFolders"/> wirkt zusätzlich. Und: Kinder legen nie Dateien mit ausführbarer oder auslösender Endung an
/// (<see cref="BlockedName"/>) – egal wohin. Erwachsene: unverändert.</summary>
public static class ChildWrite
{
    /// <summary>Endungen, die ein Kind nie anlegt (Programme, Skripte, Verknüpfungen, Auslöser, Installer, Pakete …).</summary>
    public static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lnk", ".url", ".scf", ".library-ms", ".settingcontent-ms", ".appref-ms", ".hta", ".chm", ".msc", ".cpl", ".jar", ".reg", ".ps1", ".psm1", ".psd1", ".ps1xml",
        ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".wsc", ".sct", ".bat", ".cmd", ".com", ".pif", ".scr", ".msi", ".msp", ".mst", ".msu", ".appx", ".appxbundle",
        ".msix", ".msixbundle", ".appinstaller", ".xll", ".application", ".inf", ".exe", ".dll", ".sys", ".ocx", ".drv", ".gadget", ".diagcab", ".search-ms", ".searchconnector-ms",
        ".desktop", ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".potm", ".ppam", ".ppsm", ".sldm", ".iqy", ".slk", ".website", ".theme", ".themepack", ".deskthemepack",
    };

    static readonly ConcurrentDictionary<string, byte> Extra = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, byte> Inner = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Freigabe für einen Trendo-eigenen Ort, der selbst in einem Systemordner liegen darf („projekte“ im Datenordner unter %APPDATA%, im
    /// Testmodus der Testordner unter Temp): zulässig, solange er keine Laufwerks- oder Profilwurzel ist und keinen Systemort ENTHÄLT.</summary>
    public static void AllowInside(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;
        try { Inner[Norm(dir)] = 0; _roots = null; } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { }
    }
    static bool InnerOk(string root)
    {
        if (System.IO.Path.GetPathRoot(root) is { } pr && string.Equals(Norm(pr), root, StringComparison.OrdinalIgnoreCase)) return false;
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length > 0 && string.Equals(Norm(profile), root, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var n in SystemFinal.Value)
            if (Under(n, root)) return false; // enthält einen Systemort
        return true;
    }

    static string Norm(string p) => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(p));
    static string Final(string p)
    {
        var full = Norm(p);
        if (!OperatingSystem.IsWindows()) return full;
        try { return Norm(new Trendo.Core.WindowsDiskResolver().FinalPath(full)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return full; }
    }
    static bool Under(string path, string root) => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>Systemorte, die nie eine Freigabe-Wurzel sein dürfen (und keine Wurzel darf sie enthalten).</summary>
    /// <summary>Systemorte, einmal aufgelöst (Leistung: sie ändern sich zur Laufzeit nicht).</summary>
    static readonly Lazy<List<string>> SystemFinal = new(() => SystemDirs().SelectMany(d => { try { return new[] { Norm(d), Final(d) }; } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return []; } }).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    static volatile IReadOnlyList<string>? _roots;

    static IEnumerable<string> SystemDirs()
    {
        foreach (var f in new[] { Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.CommonApplicationData,
                     Environment.SpecialFolder.Startup, Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.SendTo, Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.Windows, Environment.SpecialFolder.System, Environment.SpecialFolder.CommonStartup,
                     Environment.SpecialFolder.CommonStartMenu, Environment.SpecialFolder.Templates })
        {
            var d = Environment.GetFolderPath(f);
            if (d.Length > 0) yield return d;
        }
        yield return System.IO.Path.GetTempPath();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length > 0) { yield return System.IO.Path.Combine(profile, "AppData"); yield return System.IO.Path.Combine(profile, "AppData", "LocalLow"); }
    }

    /// <summary>Eine Wurzel ist zulässig, wenn sie kein Laufwerkswurzel ist und keinen Systemort enthält oder in einem liegt.</summary>
    static bool RootOk(string root)
    {
        if (System.IO.Path.GetPathRoot(root) is { } pr && string.Equals(Norm(pr), root, StringComparison.OrdinalIgnoreCase)) return false;
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length > 0 && string.Equals(Norm(profile), root, StringComparison.OrdinalIgnoreCase)) return false; // ganzer Benutzerordner enthält AppData
        foreach (var n in SystemFinal.Value)
            if (Under(n, root) || Under(root, n)) return false;
        return true;
    }

    /// <summary>Zusätzliche Freigabe (eigener Trendo-Ordner des Kindes, „projekte“). Unzulässige Orte (System, Profilwurzel, Laufwerk) werden ignoriert.</summary>
    public static void Allow(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;
        try { Extra[Norm(dir)] = 0; _roots = null; } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { }
    }

    /// <summary>Die Freigabe-Wurzeln – bekannte Ordner des Kontos (aufgelöst) und die zusätzlichen Freigaben, jeweils nur zulässige.</summary>
    /// <remarks>12. Prüfrunde: zwischengespeichert – neu berechnet bei jeder weiteren Freigabe, wenn sich ein bekannter Ordner ändert (z. B. OneDrive-
    /// Umleitung, billiger Vergleich der Pfade beim Abruf) und spätestens nach 60 Sekunden. <see cref="RootOk"/> gilt bei jeder Neuberechnung.</remarks>
    public static IReadOnlyList<string> Roots()
    {
        var sig = KnownSignature();
        if (_roots is { } r && _rootsSig == sig && DateTime.UtcNow - _rootsAt < TimeSpan.FromSeconds(60)) return r;
        var fresh = ComputeRoots(); _rootsSig = sig; _rootsAt = DateTime.UtcNow; _roots = fresh;
        return fresh;
    }
    static volatile string? _rootsSig; static DateTime _rootsAt;
    static string KnownSignature() => string.Join("|", new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic,
        Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.DesktopDirectory }.Select(Environment.GetFolderPath)) + "|" + KnownDownloads();

    /// <summary>NUR FÜR TESTS (und den isolierten Testmodus der App): Freigabe für die Dauer des Bereichs – <c>using (ChildWrite.TestScope(root))</c>.
    /// Danach ist sie wieder entfernt; der globale Zustand bleibt sauber.</summary>
    public static IDisposable TestScope(string root, bool inside = true)
    {
        var key = Norm(root);
        if (inside) AllowInside(root); else Allow(root);
        return new Scope(() => { if (inside) Inner.TryRemove(key, out _); else Extra.TryRemove(key, out _); _roots = null; });
    }
    sealed class Scope(Action done) : IDisposable { int _d; public void Dispose() { if (Interlocked.Exchange(ref _d, 1) == 0) done(); } }

    static IReadOnlyList<string> ComputeRoots()
    {
        var list = new List<string>();
        foreach (var f in new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.DesktopDirectory })
            if (Environment.GetFolderPath(f) is { Length: > 0 } d) list.Add(d);
        if (KnownDownloads() is { } dl) list.Add(dl);
        list.AddRange(Extra.Keys);
        return list.SelectMany(r => new[] { Norm(r), Final(r) }).Distinct(StringComparer.OrdinalIgnoreCase).Where(RootOk)
            .Concat(Inner.Keys.SelectMany(r => new[] { Norm(r), Final(r) }).Distinct(StringComparer.OrdinalIgnoreCase).Where(InnerOk)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Downloads-Ordner des Kontos (bekannter Ordner FOLDERID_Downloads, sonst „Downloads“ im Profil).</summary>
    static string? KnownDownloads()
    {
        if (OperatingSystem.IsWindows())
            try { if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out var p) == 0 && p.Length > 0) return p; }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return profile.Length > 0 ? System.IO.Path.Combine(profile, "Downloads") : null;
    }
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SHGetKnownFolderPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token,
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] out string path);

    /// <summary>Liegt <paramref name="path"/> in einer Freigabe – als angegebener UND als aufgelöster Pfad (Junction/Symlink mit Ziel draußen: nein)?</summary>
    public static bool InAllowedRoot(string path)
    {
        string full, fin;
        try { full = Norm(path); fin = Final(path); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        var roots = Roots();
        return roots.Any(r => Under(full, r)) && roots.Any(r => Under(fin, r));
    }

    /// <summary>Name, den ein Kind nie anlegt: ausführbare/auslösende Endung (auch Doppelendungen wie „bild.exe.png“ und mit Punkt oder Leerzeichen am Ende),
    /// Programm-Endungen (<see cref="ProgramTrust.CodeExtensions"/>), Installer, alternative Datenströme, Gerätenamen.</summary>
    public static bool BlockedName(string name)
    {
        var n = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(name)).TrimEnd(' ', '.');
        if (n.Length == 0 || n.Contains(':') || n.Any(char.IsControl)) return true;
        if (Archives.UnsafeEntryName(n) && System.IO.Path.GetExtension(n).Length > 0) return true; // Gerätenamen, Code, Installer, Abbilder (Dateien ohne Endung: erlaubt)
        if (System.Text.RegularExpressions.Regex.IsMatch(n.Split('.')[0], @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
        var parts = n.Split('.');
        for (var i = 1; i < parts.Length; i++)
        {
            var ext = "." + parts[i].Trim();
            if (BlockedExtensions.Contains(ext) || ProgramTrust.CodeExtensions.Contains(ext)) return true;
        }
        return Software.IsInstallerLike(n);
    }

    /// <summary>Darf <paramref name="by"/> nach <paramref name="path"/> schreiben (Datei oder Ordner anlegen, ändern, löschen)? Meldung oder null.</summary>
    public static string? Problem(Principal by, string path, bool checkName = true)
    {
        if (Rbac.Has(by, Permission.ManageSoftware)) return null;
        if (checkName && BlockedName(path)) return Rbac.NurErwachseneEndung;
        if (!InAllowedRoot(path) || ProtectedFolders.IsProtected(path)) return Rbac.NurErwachseneOrt;
        return null;
    }

    /// <summary>Wie <see cref="Problem"/>, aber mit „rbac.denied“ und Ausnahme (Kinderprofil).</summary>
    public static void Demand(Principal by, string path, AuditLog audit, bool checkName = true)
    {
        if (Problem(by, path, checkName) is { } msg) Rbac.DemandSoftware(by, "schreiben " + System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path)), audit, msg);
    }

    /// <summary>T-0764 (13. Prüfrunde): Verschieben, Umbenennen oder Kopieren von <paramref name="from"/> nach <paramref name="to"/> (Ordner-Werkzeuge im Kern:
    /// Umbenennen, Sortieren, Fotos ordnen, Rückgängig). Beide Orte müssen in der Freigabe liegen (<paramref name="fromReadOnly"/>: die Quelle wird nur gelesen –
    /// dann zählt nur das Ziel). Eine ausführbare oder auslösende Endung ist nur erlaubt, wenn der Name gleich bleibt (Downloads nach Art sortieren:
    /// „setup.exe“ bleibt „setup.exe“) – „spiel“ → „spiel.cmd“ ist für Kinder gesperrt. Erwachsene: unverändert.</summary>
    public static void DemandMove(Principal by, string from, string to, AuditLog audit, bool fromReadOnly = false)
    {
        if (Rbac.Has(by, Permission.ManageSoftware)) return;
        if (!fromReadOnly) Demand(by, from, audit, checkName: false);
        var same = string.Equals(System.IO.Path.GetFileName(from), System.IO.Path.GetFileName(to), StringComparison.OrdinalIgnoreCase);
        Demand(by, to, audit, checkName: !same);
    }

    /// <summary>Für Rückruf-Prüfungen (Dateimanager): Meldung oder null; protokolliert die Ablehnung.</summary>
    public static string? WriteProblem(Principal by, string path, AuditLog audit, bool checkName = true)
    {
        try { Demand(by, path, audit, checkName); return null; }
        catch (UnauthorizedAccessException e) { return e.Message; }
    }
}
