using Trendo.Core;

namespace Trendo.App;

/// <summary>U1–U6: Update-Weg. Echt: Velopack mit den offiziellen GitHub-Releases von Trendo OS (U6, nur geprüfte Versionen,
/// jede Veröffentlichung nur nach OK). Tests ersetzen ihn durch eine Nachbildung.</summary>
public interface ITrendoUpdater
{
    bool IsInstalled { get; }
    string Current { get; }
    /// <summary>Neue Version (Nummer, Neuerungen als Markdown) oder null.</summary>
    Task<(string Version, string? Notes)?> CheckAsync();
    Task DownloadAsync(Action<int> progress, CancellationToken ct);
    /// <summary>Jetzt installieren und Trendo neu starten (beendet den Prozess).</summary>
    void ApplyAndRestart();
    /// <summary>Beim Beenden installieren, beim nächsten Start ist die neue Version da.</summary>
    void ApplyOnExit();
    /// <summary>U5: Sicherung der laufenden Version anlegen (Paket der aktuellen Version).</summary>
    bool BackupCurrent(string dir);
    /// <summary>U5: zur gesicherten Version zurück (beendet den Prozess).</summary>
    bool RollbackTo(string dir, string version);
}

public sealed class VelopackUpdater : ITrendoUpdater
{
    const string Repo = "https://github.com/manuelbrandner85/Trendo-OS";
    Velopack.UpdateManager? _umCache;
    bool _umFailed;
    Velopack.UpdateInfo? _info;
    /// <summary>Velopack gibt es nur in der installierten Version (nach VelopackApp.Run); sonst bleibt der Weg leer.</summary>
    Velopack.UpdateManager? Um
    {
        get
        {
            if (_umCache is not null || _umFailed) return _umCache;
            try { _umCache = new(new Velopack.Sources.GithubSource(Repo, null, true)); } catch (Exception) { _umFailed = true; }
            return _umCache;
        }
    }
    Velopack.UpdateManager _um => Um ?? throw new InvalidOperationException("Nur in der installierten Version");

    public bool IsInstalled { get { try { return Um?.IsInstalled == true; } catch (Exception) { return false; } } }
    public string Current { get { try { return Um?.CurrentVersion?.ToString() ?? CrashLog.AppVersion(); } catch (Exception) { return CrashLog.AppVersion(); } } }

    public async Task<(string Version, string? Notes)?> CheckAsync()
    {
        _info = await _um.CheckForUpdatesAsync();
        return _info is null ? null : (_info.TargetFullRelease.Version.ToString(), _info.TargetFullRelease.NotesMarkdown);
    }

    public Task DownloadAsync(Action<int> progress, CancellationToken ct) =>
        _info is null ? Task.CompletedTask : _um.DownloadUpdatesAsync(_info, progress, ct);

    public void ApplyAndRestart() { if (_info is not null) _um.ApplyUpdatesAndRestart(_info.TargetFullRelease, ["--nach-update"]); }
    public void ApplyOnExit() { if (_info is not null) _um.WaitExitThenApplyUpdates(_info.TargetFullRelease, silent: true, restart: false); }

    /// <summary>Paket der laufenden Version liegt im Velopack-Ordner „packages“ neben „current“ – Kopie für den Notfall.</summary>
    public bool BackupCurrent(string dir)
    {
        try
        {
            var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
            var pkgs = root is null ? null : Path.Combine(root, "packages");
            var cur = Current;
            var file = pkgs is null || !Directory.Exists(pkgs) ? null : Directory.GetFiles(pkgs, $"*-{cur}-full.nupkg").FirstOrDefault();
            if (file is null) return false;
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.nupkg")) File.Delete(old);
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public bool RollbackTo(string dir, string version)
    {
        var file = Directory.Exists(dir) ? Directory.GetFiles(dir, $"*-{version}-full.nupkg").FirstOrDefault() : null;
        if (file is null) return false;
        var um = new Velopack.UpdateManager(new Velopack.Sources.GithubSource(Repo, null, true), new Velopack.UpdateOptions { AllowVersionDowngrade = true });
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var pkgs = root is null ? null : Path.Combine(root, "packages");
        if (pkgs is null) return false;
        Directory.CreateDirectory(pkgs);
        var target = Path.Combine(pkgs, Path.GetFileName(file));
        File.Copy(file, target, true);
        um.ApplyUpdatesAndRestart(Velopack.VelopackAsset.FromNupkgNoChecksum(target), ["--nach-rueckkehr"]);
        return true;
    }
}
