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
        if (OperatingSystem.IsWindows())
        {
            if (Directory.Exists(target)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = false });
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        else Process.Start(new ProcessStartInfo("xdg-open", target) { UseShellExecute = false });
    }
    public void OpenWith(string exe, string arg) => Process.Start(ForProgram(exe, [arg]));
    public void OpenWithArgs(string exe, IReadOnlyList<string> args) => Process.Start(ForProgram(exe, args));

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
}

/// <summary>Integrationen, die nur nach Freigabe laufen.</summary>
public sealed class Integrations(ApprovalGate gate, ProjectStore projects, IProcessLauncher launcher, Func<bool>? isOffline = null)
{
    public const string OpenFolder = "explorer.open_project";
    public const string OpenUrl = "browser.open_url";

    public ApprovalRequest PlanOpenProjectFolder(Principal by, string projectId)
    {
        var dir = projects.ProjectFilesRoot(projectId);
        return gate.Request(by, OpenFolder, $"Projektordner '{projectId}' im Explorer öffnen", dir, preview:
            [$"Ordner öffnen: {dir}", "Es wird nichts gelöscht oder verändert."]);
    }

    public ApprovalRequest PlanOpenUrl(Principal by, string url)
    {
        if (isOffline?.Invoke() == true) throw new OfflineException("Offline-Modus: Browser-/Netzwerkaktionen sind deaktiviert.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Nur https-URLs erlaubt.");
        return gate.Request(by, OpenUrl, $"URL im Standardbrowser öffnen: {u.Host}", u.AbsoluteUri, preview:
            [$"Adresse öffnen: {u.AbsoluteUri}", "Es wird nichts heruntergeladen und nichts gesendet."]);
    }

    /// <summary>Direkter Start durch den lokalen Owner per Klick (Dock/Schnellstart). Kein Agent darf das; wird auditiert.</summary>
    public void LaunchDirect(Principal by, string target, AuditLog audit)
    {
        Rbac.Demand(by, Permission.ApproveAction, audit);
        audit.Append(by.Name, "user.direct_launch", target);
        launcher.Open(target);
    }

    /// <summary>Startet ein Programm mit genau einer Datei/URL als Argument (kein Shell-Parsing).</summary>
    public void LaunchDirect(Principal by, string exe, AuditLog audit, string arg)
    {
        Rbac.Demand(by, Permission.ApproveAction, audit);
        audit.Append(by.Name, "user.direct_launch", $"{Path.GetFileName(exe)} {arg}");
        launcher.OpenWith(exe, arg);
    }

    /// <summary>T-0203: externes Programm mit der aktuellen Datei (geplant von <see cref="ExternalApps.Plan"/>).</summary>
    public void LaunchArgs(Principal by, string exe, AuditLog audit, IReadOnlyList<string> args)
    {
        Rbac.Demand(by, Permission.ApproveAction, audit);
        audit.Append(by.Name, "user.direct_launch", $"{Path.GetFileName(exe)} {string.Join(" ", args)}");
        launcher.OpenWithArgs(exe, args);
    }

    public void Execute(ApprovalRequest r) =>
        gate.Execute(r.Id, r.Action, r.Target, () => { launcher.Open(r.Target); return true; });
}

public sealed class OfflineException(string msg) : Exception(msg);

