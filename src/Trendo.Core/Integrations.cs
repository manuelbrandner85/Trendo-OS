using System.Diagnostics;

namespace Trendo.Core;

/// <summary>Startet Prozesse ausschließlich über einen Launcher. Echte Implementierung nutzt die Windows-Shell (Explorer/Standardbrowser).</summary>
public interface IProcessLauncher
{
    void Open(string target);
    void OpenWith(string exe, string arg) => Open($"{exe} \"{arg}\"");
    /// <summary>T-0203: Programm mit mehreren Argumenten (ohne Shell-Parsing).</summary>
    void OpenWithArgs(string exe, IReadOnlyList<string> args) { if (args.Count == 1) OpenWith(exe, args[0]); else Open(exe + " " + string.Join(" ", args.Select(a => $"\"{a}\""))); }
}

public sealed class ShellProcessLauncher : IProcessLauncher
{
    public void Open(string target)
    {
        if (BlenderGui.IsBlenderExe(target))
        {
            Process.Start(ForGui(target, []));
            return;
        }
        if (OperatingSystem.IsWindows())
        {
            if (Directory.Exists(target)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = false });
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        else Process.Start(new ProcessStartInfo("xdg-open", target) { UseShellExecute = false });
    }
    public void OpenWith(string exe, string arg) => Process.Start(ForGui(exe, [arg]));
    public void OpenWithArgs(string exe, IReadOnlyList<string> args) => Process.Start(ForGui(exe, args));

    /// <summary>Programm mit Argumenten, ohne extra Konsolenfenster. Die eigene Oberfläche des Programms (z. B. Blender) bleibt sichtbar.</summary>
    public static ProcessStartInfo ForProgram(string exe, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Normal,
            WorkingDirectory = Path.GetTempPath(),
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    /// <summary>Wie <see cref="ForProgram"/>, auf Windows aber blender-launcher.exe, wenn die Datei neben blender.exe liegt.</summary>
    public static ProcessStartInfo ForGui(string exe, IReadOnlyList<string> args, bool? windows = null, Func<string, bool>? exists = null) =>
        ForProgram(BlenderGui.ForWindow(exe, windows ?? OperatingSystem.IsWindows(), exists), args);
}

/// <summary>Blender-Fenster ohne Konsole. Hintergrundläufe (--background) bleiben bei blender.exe über <see cref="Cli.Run"/>.</summary>
public static class BlenderGui
{
    public const string LauncherName = "blender-launcher.exe";

    public static bool IsBlenderExe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return Path.GetFileName(path.Trim().Trim('"')).Equals("blender.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Unter Windows die Startdatei ohne Konsole: blender-launcher.exe im selben Ordner, sonst unverändert blender.exe.</summary>
    public static string ForWindow(string blenderExe, bool windows, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        if (!windows || !IsBlenderExe(blenderExe)) return blenderExe;
        string full;
        try { full = Path.GetFullPath(blenderExe.Trim().Trim('"')); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return blenderExe; }
        var dir = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(dir)) return blenderExe;
        var launcher = Path.Combine(dir, LauncherName);
        return exists(launcher) ? launcher : blenderExe;
    }
}

/// <summary>Integrationen, die nur nach Freigabe laufen.</summary>
public sealed class Integrations(ApprovalGate gate, ProjectStore projects, IProcessLauncher launcher, Func<bool>? isOffline = null)
{
    public const string OpenFolder = "explorer.open_project";
    public const string OpenUrl = "browser.open_url";

    public ApprovalRequest PlanOpenProjectFolder(Principal by, string projectId)
    {
        var dir = projects.ProjectFilesRoot(projectId);
        return gate.Request(by, OpenFolder, $"Projektordner '{projectId}' im Explorer öffnen", dir, resumable: true); // T-0713: Ziel steht in der Anfrage – überlebt einen Neustart
    }

    public ApprovalRequest PlanOpenUrl(Principal by, string url)
    {
        if (isOffline?.Invoke() == true) throw new OfflineException("Offline-Modus: Browser-/Netzwerkaktionen sind deaktiviert.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Nur https-URLs erlaubt.");
        return gate.Request(by, OpenUrl, $"URL im Standardbrowser öffnen: {u.Host}", u.AbsoluteUri, resumable: true);
    }

    /// <summary>T-0764 (2. Prüfrunde N-C): Trendos eigene Programmordner (Werkzeugordner, Programmordner, Portable-Ordner) – .exe darin gelten
    /// als vertrauenswürdig. Setzt die App; ohne Angabe ist keine .exe vertrauenswürdig.</summary>
    public Func<string, bool>? TrustedProgramPath { get; set; }

    /// <summary>T-0764: Wer darf direkt starten, und braucht das Ziel das Recht „Programme verwalten“? Erwachsene (Besitzer) dürfen alles; das
    /// Kinderprofil darf Dokumente, Ordner, Adressen und Trendos eigene Programme öffnen, aber keine Installer, Skripte, Verknüpfungen und
    /// keine fremde .exe. Andere Rollen (Agenten, API) dürfen gar nicht direkt starten.</summary>
    void DemandLaunch(Principal by, AuditLog audit, IEnumerable<string?> userTargets, string? exe = null)
    {
        if (by != Principal.ChildProfile) Rbac.Demand(by, Permission.ApproveAction, audit);
        foreach (var t in userTargets)
            if (t is not null && Sys.Software.NeedsSoftwareRight(t, TrustedProgramPath))
                Rbac.DemandSoftware(by, "start " + Path.GetFileName(t.Trim('"')), audit, Rbac.NurErwachseneStart);
        if (exe is not null && Sys.Software.IsInstallerLike(exe)) Rbac.DemandSoftware(by, "start " + Path.GetFileName(exe), audit, Rbac.NurErwachseneStart);
    }

    /// <summary>Direkter Start per Klick (Dock/Schnellstart, Dateien öffnen). Kein Agent darf das; wird auditiert.
    /// T-0764: Rolle und Ziel werden hier geprüft (<see cref="DemandLaunch"/>) – alle Aufrufer profitieren.</summary>
    public void LaunchDirect(Principal by, string target, AuditLog audit)
    {
        DemandLaunch(by, audit, [target]);
        audit.Append(by.Name, "user.direct_launch", target);
        launcher.Open(target);
    }

    /// <summary>Startet ein Programm mit genau einer Datei/URL als Argument (kein Shell-Parsing).</summary>
    /// <param name="userExe">T-0764: Das Programm hat die Person selbst gewählt (eigene Verknüpfung) – dann gilt für die .exe dieselbe Prüfung wie für Dateien.</param>
    public void LaunchDirect(Principal by, string exe, AuditLog audit, string arg, bool userExe = false)
    {
        DemandLaunch(by, audit, userExe ? [exe, File.Exists(arg) ? arg : null] : [File.Exists(arg) ? arg : null], exe);
        audit.Append(by.Name, "user.direct_launch", $"{Path.GetFileName(exe)} {arg}");
        launcher.OpenWith(exe, arg);
    }

    /// <summary>T-0203: externes Programm mit der aktuellen Datei (geplant von <see cref="ExternalApps.Plan"/>).</summary>
    public void LaunchArgs(Principal by, string exe, AuditLog audit, IReadOnlyList<string> args)
    {
        DemandLaunch(by, audit, args.Where(File.Exists), exe);
        audit.Append(by.Name, "user.direct_launch", $"{Path.GetFileName(exe)} {string.Join(" ", args)}");
        launcher.OpenWithArgs(exe, args);
    }

    /// <summary>Nur die eigenen Integrationen (Projektordner, https-Adresse). B4 (T-0706): Andere Anfragen (Installieren, Ablauf, Roboter …)
    /// gehören zu ihrer Seite – ihr Ziel ist kein Startziel und wird hier nie geöffnet.</summary>
    public static bool Handles(string action) => action is OpenFolder or OpenUrl;

    /// <summary>T-0713: erlaubtes Startziel – bei „URL öffnen“ eine absolute https-Adresse, bei „Projektordner öffnen“ genau der Dateiordner eines Projekts.</summary>
    public bool IsValidTarget(string action, string target)
    {
        if (action == OpenUrl) return Uri.TryCreate(target, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.AbsoluteUri == target;
        if (action != OpenFolder) return false;
        try
        {
            var full = Path.GetFullPath(target);
            var id = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(full)));
            return id is { Length: > 0 } && string.Equals(Path.GetFullPath(projects.ProjectFilesRoot(id)), Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ProjectIsolationException or ArgumentException or IOException or NotSupportedException) { return false; }
    }

    public void Execute(ApprovalRequest r)
    {
        if (!Handles(r.Action)) throw new ApprovalException($"Ausführung blockiert: „{r.Action}“ wird auf der Seite ausgeführt, die gefragt hat.");
        // T-0713: Ziel vor dem Start erneut prüfen (Anfragen können nach einem Neustart aus der Ablage kommen) – nur https bzw. ein Projektordner
        if (!IsValidTarget(r.Action, r.Target)) throw new ApprovalException("Ausführung blockiert: Das Ziel ist kein erlaubtes Startziel.");
        gate.Execute(r.Id, r.Action, r.Target, () => { launcher.Open(r.Target); return true; });
    }
}

public sealed class OfflineException(string msg) : Exception(msg);

