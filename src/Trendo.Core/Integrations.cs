using System.Diagnostics;

namespace Trendo.Core;

/// <summary>Startet Prozesse ausschließlich über einen Launcher. Echte Implementierung nutzt die Windows-Shell (Explorer/Standardbrowser).</summary>
public interface IProcessLauncher { void Open(string target); void OpenWith(string exe, string arg) => Open($"{exe} \"{arg}\""); }

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
    public void OpenWith(string exe, string arg)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetTempPath() };
        psi.ArgumentList.Add(arg); Process.Start(psi);
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
        return gate.Request(by, OpenFolder, $"Projektordner '{projectId}' im Explorer öffnen", dir);
    }

    public ApprovalRequest PlanOpenUrl(Principal by, string url)
    {
        if (isOffline?.Invoke() == true) throw new OfflineException("Offline-Modus: Browser-/Netzwerkaktionen sind deaktiviert.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Nur https-URLs erlaubt.");
        return gate.Request(by, OpenUrl, $"URL im Standardbrowser öffnen: {u.Host}", u.AbsoluteUri);
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

    /// <summary>Öffnet ein externes Programm mit der aktuellen Datei oder dem Projektordner.</summary>
    public void OpenWithFile(Principal by, string exe, string fileOrFolder, AuditLog audit)
    {
        if (string.IsNullOrWhiteSpace(exe)) throw new ArgumentException("Kein Programm angegeben.");
        if (string.IsNullOrWhiteSpace(fileOrFolder)) throw new ArgumentException("Keine Datei oder kein Projekt angegeben.");
        LaunchDirect(by, exe, audit, fileOrFolder);
    }

    public void Execute(ApprovalRequest r) =>
        gate.Execute(r.Id, r.Action, r.Target, () => { launcher.Open(r.Target); return true; });
}

public sealed class OfflineException(string msg) : Exception(msg);

