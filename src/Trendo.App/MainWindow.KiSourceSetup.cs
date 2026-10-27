using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026: KI-Programme, die es nur auf GitHub oder beim Hersteller gibt, lädt und richtet Trendo selbst ein –
/// nach Bestätigung, mit Fortschritt, Abbrechen und Protokoll. Portable Programme landen im Werkzeugordner, Installer werden gestartet und danach erkannt.</summary>
public partial class MainWindow
{
    private string? _srcAsk;
    /// <summary>Für Tests: Inhalt von /releases statt GitHub.</summary>
    public Func<string, Task<string>>? KiSourceReleasesOverride { get; set; }
    /// <summary>Für Tests: Installer nicht wirklich starten.</summary>
    public Func<string, Task>? KiInstallerRunOverride { get; set; }
    /// <summary>Für Tests: Server nicht wirklich starten (Programm, Argumente).</summary>
    public Action<string, string>? KiServerStartOverride { get; set; }
    public string LocalAppDataDir { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>Wo liegt das eingerichtete Programm? Portable: Werkzeugordner (aus der GitHub-Liste), Installer: unter %LOCALAPPDATA%.</summary>
    public string? KiSourceExe(string id)
    {
        if (KiSourceSetups.For(id) is not { } s) return null;
        if (!s.Portable) return KiSourceSetups.FindInstalled(s, LocalAppDataDir);
        var e = GitHubApps.List(_deps.ToolsRoot).FirstOrDefault(i => string.Equals(i.Repo, s.Repo, StringComparison.OrdinalIgnoreCase));
        if (e is null) return null;
        var dir = Path.Combine(_deps.ToolsRoot, e.Id);
        var exe = Directory.Exists(dir) ? Directory.EnumerateFiles(dir, s.Exe, SearchOption.AllDirectories).FirstOrDefault() : null;
        return exe ?? (File.Exists(Path.Combine(dir, e.Exe)) ? Path.Combine(dir, e.Exe) : null);
    }

    /// <summary>Herunterladen und einrichten (nach Bestätigung im Knopf). Liefert true, wenn das Programm danach da ist.</summary>
    public async Task<bool> KiSourceInstall(string id)
    {
        var s = KiSourceSetups.For(id) ?? throw new ArgumentException("Unbekannte KI-Quelle.");
        var info = KiCatalog.Sources.First(x => x.Id == id);
        if (_core.Offline) { _srcMsg = "Trendo ist offline – zum Laden braucht es Internet."; return false; }
        if (_depBusy) return false;
        _depBusy = true; string? file = null;
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.source.install.start", $"{id} {info.License} {s.Host}");
        BeginSetupOverlay($"{info.Name} wird eingerichtet");
        try
        {
            string url, name; long size; GitHubApps.Release? rel = null; GitHubApps.Asset? asset = null;
            if (s.Repo is { } repo)
            {
                SetupOverlayProgress(0, 3, "Neueste offizielle Version auf GitHub suchen …");
                var json = KiSourceReleasesOverride is { } o ? await o(repo) : await (await GitHubGet(KiSourceSetups.ReleasesUrl(repo), "application/vnd.github+json")).Content.ReadAsStringAsync(SetupToken);
                (rel, asset) = KiSourceSetups.Pick(s, KiSourceSetups.ParseReleases(json)) ?? throw new InvalidOperationException($"Auf GitHub gibt es gerade keine passende Windows-Datei für {info.Name}.");
                (url, name, size) = (asset.Url, asset.Name, asset.Size);
            }
            else (url, name, size) = (s.VendorUrl!, $"{info.Name.Replace(' ', '-')}-Setup.exe", 0);
            SetupOverlayProgress(1, 3, $"{name} wird geladen …");
            var (dl, _) = await KiDownload(url, name, size, 1, 3);
            file = dl;
            SetupToken.ThrowIfCancellationRequested();
            SetupOverlayProgress(2, 3, s.Portable ? "Prüfen und einrichten …" : "Installer des Herstellers wird gestartet – bitte dort bestätigen …");
            if (s.Portable)
                await Task.Run(() => GitHubApps.Install(_deps.ToolsRoot, s.Repo!, rel!, asset!, dl, info.License, info.Hint, _core.Audit));
            else
            {
                if (KiInstallerRunOverride is { } run) await run(dl);
                else
                {
                    using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dl) { UseShellExecute = true });
                    if (p is not null) await p.WaitForExitAsync(SetupToken);
                }
            }
            var exe = KiSourceExe(id);
            if (exe is null) throw new InvalidOperationException(s.Portable ? "Das Programm wurde nicht gefunden." : $"{info.Name} wurde nicht fertig installiert (Installer abgebrochen?).");
            KiSourceSettings.Update(_core.DataRoot, set => set.Disabled.Remove(id));
            _core.Audit.Append(Principal.LocalOwner.Name, "ki.source.install.ok", $"{id} {Path.GetFileName(exe)}");
            SetupOverlayProgress(3, 3, "Fertig ✓");
            _srcMsg = s.Portable ? $"{info.Name} ist eingerichtet. Mit „Starten“ läuft der lokale Server." : $"{info.Name} ist installiert. {info.Hint}";
            FinishSetupOverlay($"{info.Name} ist bereit", _srcMsg, []);
            return true;
        }
        catch (OperationCanceledException) when (SetupCancelRequested) { _srcMsg = $"{info.Name}: abgebrochen."; return false; }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            _core.Audit.Append(Principal.LocalOwner.Name, "ki.source.install.failed", $"{id}: {ex.Message}");
            _srcMsg = $"{info.Name}: Einrichten hat nicht geklappt – {ex.Message}";
            FinishSetupOverlay("Einrichten nicht möglich", _srcMsg, [ex.Message]);
            return false;
        }
        finally { _depBusy = false; if (file is not null) TryDelete(file); }
    }

    /// <summary>Lokalen Server starten (unsichtbar, nur 127.0.0.1). llama.cpp und KoboldCpp nehmen das größte Sprachmodell aus der KI-Bibliothek.</summary>
    public bool KiSourceStart(string id)
    {
        var s = KiSourceSetups.For(id) ?? throw new ArgumentException("Unbekannte KI-Quelle.");
        var exe = KiSourceExe(id) ?? throw new InvalidOperationException("Zuerst herunterladen und einrichten.");
        if (!s.Portable) { Launch(exe); return true; } // LM Studio, Jan: Programm öffnen, dort den lokalen Server einschalten
        var args = KiSourceSetups.Args(s, s.NeedsModel ? KiSourceSetups.BestModel(KiStore.Root) : null);
        if (KiServerStartOverride is { } o) { o(exe, args); }
        else
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)! };
            if (id == "ollama") Vollpaket.ConfigureOllama(psi, _deps.ToolsRoot);
            System.Diagnostics.Process.Start(psi);
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.source.start", $"{id} {Path.GetFileName(exe)}");
        _srcMsg = $"{KiCatalog.Sources.First(x => x.Id == id).Name} startet – gleich unter „Suchen“ sichtbar.";
        return true;
    }

    /// <summary>Wunsch 27.10.2026: KI-Programm wieder entfernen – portable aus dem Werkzeugordner, Installer über ihren Deinstaller.</summary>
    public bool KiSourceRemove(string id)
    {
        if (KiSourceSetups.For(id) is not { } s) return false;
        if (s.Portable)
        {
            var e = GitHubApps.List(_deps.ToolsRoot).FirstOrDefault(i => string.Equals(i.Repo, s.Repo, StringComparison.OrdinalIgnoreCase));
            return e is not null && GitHubApps.Uninstall(_deps.ToolsRoot, e.Id, _core.Audit);
        }
        if (KiSourceExe(id) is not { } exe) return false;
        GitHubRunUninstaller(exe);
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.source.remove", id);
        return true;
    }

    /// <summary>Knöpfe je Quelle: „Herunterladen & einrichten“ (mit Bestätigung), danach „Starten“ und „Neu laden“.</summary>
    private Control KiSourceButtons(KiSourceInfo src)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        if (KiSourceSetups.For(src.Id) is not { } s) return sp;
        var exe = KiSourceExe(src.Id);
        if (_srcAsk == src.Id)
        {
            var yes = new Button { Name = "src-install-yes-" + src.Id, Content = "Ja, laden", Background = Accent, Padding = new Thickness(8, 2) };
            yes.Click += async (_, _) => { _srcAsk = null; await KiSourceInstall(src.Id); await ScanSourcesAsync(); Show("ki-quellen"); };
            var no = new Button { Name = "src-install-no-" + src.Id, Content = "Nein", Padding = new Thickness(8, 2) };
            no.Click += (_, _) => { _srcAsk = null; Show("ki-quellen"); };
            sp.Children.Add(new TextBlock { Text = $"{src.Name} ({src.License}) von {s.Host} laden{(s.Portable ? " und einrichten" : " und den Installer starten")}?", Foreground = Text, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 300, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(yes); sp.Children.Add(no);
            return sp;
        }
        var get = new Button { Name = "src-install-" + src.Id, Content = exe is null ? "Herunterladen & einrichten" : "Neu laden", Padding = new Thickness(8, 2), IsEnabled = !_core.Offline && !_depBusy,
            Background = exe is null ? Accent : Brushes.Transparent };
        ToolTip.SetTip(get, _core.Offline ? "Im Offline-Modus wird nichts geladen." : $"Offizielle Version von {s.Host}");
        get.Click += (_, _) => { _srcAsk = src.Id; Show("ki-quellen"); };
        sp.Children.Add(get);
        if (exe is not null)
        {
            var start = new Button { Name = "src-start-" + src.Id, Content = s.Portable ? "Starten" : "Öffnen", Padding = new Thickness(8, 2) };
            start.Click += async (_, _) =>
            {
                try { KiSourceStart(src.Id); } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception) { _srcMsg = ex.Message; }
                Show("ki-quellen"); await Task.Delay(_opt.Isolated ? 0 : 3000); await ScanSourcesAsync(); Show("ki-quellen");
            };
            sp.Children.Add(start);
            sp.Children.Add(ConfirmRemoveButton("src:" + src.Id, "src-remove-" + src.Id, "ki-quellen", () =>
            {
                try { _srcMsg = KiSourceRemove(src.Id) ? $"{src.Name} wurde entfernt." : $"{src.Name} war nicht eingerichtet."; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _srcMsg = $"{src.Name} läuft noch oder ist gesperrt – bitte schließen und erneut versuchen. ({ex.Message})"; }
                return Task.CompletedTask;
            }));
        }
        return sp;
    }
}
