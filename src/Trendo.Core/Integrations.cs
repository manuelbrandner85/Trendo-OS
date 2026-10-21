using System.Diagnostics;

namespace Trendo.Core;

/// <summary>Startet Prozesse ausschließlich über einen Launcher. Echte Implementierung nutzt die Windows-Shell (Explorer/Standardbrowser).</summary>
public interface IProcessLauncher { void Open(string target); }

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
}

/// <summary>Integrationen, die nur nach Freigabe laufen.</summary>
public sealed class Integrations(ApprovalGate gate, ProjectStore projects, IProcessLauncher launcher, Func<bool>? isOffline = null)
{
    public const string OpenFolder = "explorer.open_project";
    public const string OpenUrl = "browser.open_url";

    public ApprovalRequest PlanOpenProjectFolder(string projectId)
    {
        var dir = projects.ProjectFilesRoot(projectId);
        return gate.Request(OpenFolder, $"Projektordner '{projectId}' im Explorer öffnen", dir);
    }

    public ApprovalRequest PlanOpenUrl(string url)
    {
        if (isOffline?.Invoke() == true) throw new OfflineException("Offline-Modus: Browser-/Netzwerkaktionen sind deaktiviert.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Nur https-URLs erlaubt.");
        return gate.Request(OpenUrl, $"URL im Standardbrowser öffnen: {u.Host}", u.AbsoluteUri);
    }

    public void Execute(ApprovalRequest r) =>
        gate.Execute(r.Id, r.Action, r.Target, () => { launcher.Open(r.Target); return true; });
}

public sealed class OfflineException(string msg) : Exception(msg);

