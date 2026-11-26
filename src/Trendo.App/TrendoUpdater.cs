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
    /// <summary>T-0770 (M3): aktive Test-Update-Quelle (nur in einer Prüf-Instanz), sonst null = offizielle Veröffentlichungen.</summary>
    string? TestSource => null;
    /// <summary>T-0770 (M5b): Grund, warum die letzte Sicherung nicht angelegt wurde (z. B. zu wenig Speicher), sonst null.</summary>
    string? BackupNote => null;
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
            try { _umCache = new(Source()); } catch (Exception) { _umFailed = true; }
            return _umCache;
        }
    }
    Velopack.UpdateManager _um => Um ?? throw new InvalidOperationException("Nur in der installierten Version");

    /// <summary>T-0770: Umgebungsvariable für Abnahmen – ein lokaler Ordner mit Velopack-Paketen (releases.win.json + .nupkg) statt GitHub.
    /// Korrektur (M3): wirkt nur zusammen mit einer gültigen Prüf-Instanz (<see cref="DataLocation.InstanceVar"/>); eine normale Installation
    /// ignoriert sie. Nur ein vorhandener lokaler Ordner mit vollständigem Pfad, keine Netzpfade. Sonst offizielle GitHub-Releases (U6).</summary>
    public const string LocalSourceVar = "TRENDO_UPDATE_QUELLE";
    public static string? LocalSourceDir() => LocalSourceDir(Environment.GetEnvironmentVariable(LocalSourceVar), DataLocation.Instance());
    public static string? LocalSourceDir(string? value, string? instance)
    {
        var v = value?.Trim().Trim('"');
        if (instance is null || string.IsNullOrEmpty(v) || !Path.IsPathFullyQualified(v) || DataLocation.IsNetwork(v)) return null;
        // Korrektur 2 (M-A): endgültigen Pfad nehmen – auch eine Verknüpfung auf einen Netzordner zählt als Netz
        return Directory.Exists(v) && DataLocation.FinalPath(v) is { } fin && !DataLocation.IsNetwork(fin) ? fin : null;
    }
    public string? TestSource => LocalSourceDir();
    public string? BackupNote { get; private set; }
    static Velopack.Sources.IUpdateSource Source() =>
        LocalSourceDir() is { } dir ? new Velopack.Sources.SimpleFileSource(new DirectoryInfo(dir)) : new Velopack.Sources.GithubSource(Repo, null, true);

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
            if (file is null) { BackupNote = "Das Paket der laufenden Version ist nicht mehr da – die bisherige Sicherung bleibt."; return false; }
            var ok = ReplaceBackup(file, dir, FreeBytes, out var note); BackupNote = note; return ok;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { BackupNote = "Sicherung nicht möglich: " + e.Message; return false; }
    }

    static long FreeBytes(string dir) { try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dir))!).AvailableFreeSpace; } catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return -1; } }

    /// <summary>Spielraum, der nach der Sicherung frei bleiben muss (Updates brauchen selbst Platz).</summary>
    public const long SpareBytes = 512L * 1024 * 1024;

    /// <summary>T-0770 (M5a/b): Sicherung ersetzen, ohne die alte vorher zu verlieren. Erst Platz prüfen, dann vollständig in eine
    /// Zwischendatei kopieren, dann atomar an den Zielnamen tauschen und erst danach ältere Sicherungen löschen. Bei jedem Fehler bleibt
    /// die alte Sicherung unverändert und es bleibt keine Zwischendatei liegen. <paramref name="note"/> sagt ehrlich, was nicht ging.</summary>
    public static bool ReplaceBackup(string source, string dir, Func<string, long> freeBytes, out string? note)
    {
        note = null;
        string? tmp = null;
        try
        {
            // Korrektur 2 (N-a): Zwischendateien eines abgebrochenen Laufs (Absturz, Strom weg) zuerst wegräumen
            if (Directory.Exists(dir)) foreach (var rest in Directory.GetFiles(dir, "*.nupkg.neu-*")) File.Delete(rest);
            if (!File.Exists(source)) { note = "Das Paket der laufenden Version fehlt – die bisherige Sicherung bleibt."; return false; }
            Directory.CreateDirectory(dir);
            var need = new FileInfo(source).Length;
            var free = freeBytes(dir);
            if (free >= 0 && free < need + Math.Min(SpareBytes, need * 2))
            {
                note = $"Zu wenig freier Speicher für die Sicherung ({need / 1048576} MB nötig, {free / 1048576} MB frei) – die bisherige Sicherung bleibt.";
                return false;
            }
            var target = Path.Combine(dir, Path.GetFileName(source));
            tmp = Path.Combine(dir, Path.GetFileName(source) + ".neu-" + Guid.NewGuid().ToString("N")[..8]);
            File.Copy(source, tmp);
            if (new FileInfo(tmp).Length != need) throw new IOException("Kopie unvollständig.");
            File.Move(tmp, target, overwrite: true); // gleicher Ordner: atomarer Tausch
            tmp = null;
            foreach (var old in Directory.GetFiles(dir, "*.nupkg")) if (!string.Equals(old, target, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            note = "Sicherung nicht möglich (" + e.Message + ") – die bisherige Sicherung bleibt.";
            return false;
        }
        finally { if (tmp is not null) try { File.Delete(tmp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }

    public bool RollbackTo(string dir, string version)
    {
        var file = Directory.Exists(dir) ? Directory.GetFiles(dir, $"*-{version}-full.nupkg").FirstOrDefault() : null;
        if (file is null) return false;
        var um = new Velopack.UpdateManager(Source(), new Velopack.UpdateOptions { AllowVersionDowngrade = true });
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
