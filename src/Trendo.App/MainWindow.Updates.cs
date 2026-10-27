using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026 (U1–U6): Trendo bleibt von selbst aktuell – wie Windows, aber angenehmer.
/// U1 im Hintergrund suchen und laden · U2 kurzer Einrichtungsbildschirm beim Installieren, danach kommen die Fenster zurück ·
/// U3 Zeitpunkt wählbar (sofort, beim nächsten Start, nachts) · U4 „Was ist neu?“ · U5 Rückkehr zur alten Version, wenn die neue nicht startet ·
/// U6 Quelle sind nur die offiziellen, geprüften GitHub-Releases (Veröffentlichen nur nach OK).</summary>
public partial class MainWindow
{
    ITrendoUpdater? _updater;
    /// <summary>Für Tests: Update-Weg nachbilden.</summary>
    public ITrendoUpdater Updater { get => _updater ??= new VelopackUpdater(); set => _updater = value; }
    string? _updateReady;
    string _updateMsg = "";
    DispatcherTimer? _updateTimer;
    bool _updateBusy;
    public string? UpdateReady => _updateReady;
    public BootGuard Boot => new(_core.DataRoot);
    string WindowRestoreFile => Path.Combine(_core.DataRoot, "updates", "fenster.json");

    void InitUpdates()
    {
        _showMap["updates"] = UpdatesView;
        _showMap["neu"] = WhatsNewView;
        if (!ExtraPages.Any(p => p.Key == "updates")) ExtraPages.Add(("Trendo-Updates", "updates", ["update", "aktualisieren", "version", "neu", "was ist neu", "zurück", "rückkehr"]));
    }

    /// <summary>Beim Start: Fenster nach einem Update wiederherstellen, „Was ist neu?“ einmal zeigen, Startwächter nach 30 s als gut markieren, regelmäßig suchen.</summary>
    void UpdatesOnStart()
    {
        var cur = Updater.Current;
        if (File.Exists(WindowRestoreFile))
        {
            try
            {
                var saved = JsonSerializer.Deserialize<SavedLayout>(File.ReadAllText(WindowRestoreFile));
                if (saved is not null)
                {
                    foreach (var w in saved.Windows) Show(w.Page);
                    Windows.Apply((saved.Windows, saved.Stack)); LayoutWindows();
                    if (saved.Active is { } a) Show(a);
                }
            }
            catch (Exception e) when (e is JsonException or IOException or KeyNotFoundException) { }
            try { File.Delete(WindowRestoreFile); } catch (IOException) { }
        }
        if (ReleaseNotes.ShowFor(_settings.LastSeenVersion, cur, ReleaseNotes.For(_core.DataRoot, cur)))
            Dispatcher.UIThread.Post(() => Show("neu"), DispatcherPriority.Background);
        if (_settings.LastSeenVersion != cur) { _settings.LastSeenVersion = cur; SaveSettings(); }
        DispatcherTimer.RunOnce(() => Boot.MarkOk(cur), TimeSpan.FromSeconds(30));
        if (_opt.Isolated || _core.Offline || !_settings.AutoUpdate) return;
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(20) };
        var lastCheck = DateTime.MinValue;
        _updateTimer.Tick += async (_, _) =>
        {
            if (_updateReady is not null) { if (UpdateSchedule.ApplyNow(_settings.UpdateWhen, _settings.UpdateHour, DateTime.Now)) InstallUpdateNow(); return; }
            if (DateTime.UtcNow - lastCheck < UpdateSchedule.CheckEvery) return;
            lastCheck = DateTime.UtcNow;
            await UpdateCheckAsync();
        };
        _updateTimer.Start();
        _ = UpdateCheckAsync();
    }

    /// <summary>Für Tests: Start-Ablauf der Updates erneut ausführen.</summary>
    public void UpdatesOnStartForTests() => UpdatesOnStart();

    public sealed record SavedLayout(List<Trendo.Core.Shell.WindowManager.Saved> Windows, List<string> Stack, string? Active);

    /// <summary>U1: suchen und im Hintergrund laden. Danach je nach Wahl (U3) sofort installieren oder warten.</summary>
    public async Task<bool> UpdateCheckAsync(bool userAsked = false)
    {
        if (_updateBusy) return false;
        if (!Updater.IsInstalled) { _updateMsg = "Updates gibt es in der installierten Version (Setup)."; _updateInfo = _updateMsg; return false; }
        _updateBusy = true;
        try
        {
            _updateMsg = "Suche nach Updates …";
            var found = await Updater.CheckAsync();
            if (found is not { } u) { _updateMsg = $"Trendo ist aktuell (Version {Updater.Current})."; _updateInfo = $"Aktuell (v{Updater.Current})"; return false; }
            _updateMsg = $"Version {u.Version} wird im Hintergrund geladen …";
            await Updater.DownloadAsync(p => Dispatcher.UIThread.Post(() => { _updateMsg = $"Version {u.Version} wird geladen … {p} %"; if (CurrentView == "updates") Show("updates"); }), CancellationToken.None);
            ReleaseNotes.Save(_core.DataRoot, u.Version, u.Notes);
            _updateReady = u.Version;
            _core.Audit.Append("app", "update.downloaded", u.Version);
            _updateInfo = $"Update {u.Version} bereit";
            if (UpdateSchedule.ApplyNow(_settings.UpdateWhen, _settings.UpdateHour, DateTime.Now) && _settings.UpdateWhen == "sofort") InstallUpdateNow();
            else
            {
                if (_settings.UpdateWhen == "start") Updater.ApplyOnExit();
                _updateMsg = _settings.UpdateWhen == "start" ? $"Version {u.Version} ist geladen und wird beim nächsten Start von Trendo installiert."
                    : $"Version {u.Version} ist geladen und wird heute Nacht um {_settings.UpdateHour} Uhr installiert.";
                Toast($"⬆ Trendo {u.Version} ist bereit.", ("Jetzt installieren", new Action(InstallUpdateNow)), ("Was ist neu?", new Action(() => Show("neu"))));
            }
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            _updateMsg = "Updates sind gerade nicht erreichbar – Trendo versucht es später wieder.";
            _updateInfo = "Gerade nicht erreichbar – später erneut";
            if (userAsked) _core.Audit.Append("app", "update.check.failed", e.GetType().Name);
            return false;
        }
        finally { _updateBusy = false; if (CurrentView == "updates") Show("updates"); }
    }

    /// <summary>U2 + U5: Sicherung der laufenden Version, Fenster merken, Einrichtungsbildschirm zeigen, installieren, neu starten.</summary>
    public void InstallUpdateNow()
    {
        if (_updateReady is not { } v) return;
        _core.Audit.Append(Principal.LocalOwner.Name, "update.apply", v);
        Updater.BackupCurrent(Boot.BackupDir);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WindowRestoreFile)!);
            var snap = Windows.Snapshot();
            File.WriteAllText(WindowRestoreFile, JsonSerializer.Serialize(new SavedLayout(snap.Windows, snap.Stack, Windows.Active?.Page)));
        }
        catch (IOException) { }
        BeginSetupOverlay($"Trendo wird aktualisiert auf {v}");
        SetupOverlayProgress(1, 2, "Bitte kurz warten – deine Fenster sind gleich wieder da.");
        UpdateApplying = true;
        Dispatcher.UIThread.Post(() => Updater.ApplyAndRestart(), DispatcherPriority.Background);
    }

    /// <summary>Für Tests: Installation wurde angestoßen.</summary>
    public bool UpdateApplying { get; private set; }

    Control UpdatesView()
    {
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(P($"Du hast Trendo OS {Updater.Current}. Trendo sucht selbst nach neuen Versionen, lädt sie leise im Hintergrund und installiert sie, wann du willst. Updates kommen nur aus den offiziellen, geprüften Veröffentlichungen.", Text, 13));
        if (_updateMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "upd-msg", Text = _updateMsg, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var auto = new CheckBox { Name = "upd-auto", Content = "Automatisch nach Updates suchen und laden", IsChecked = _settings.AutoUpdate, Foreground = Text };
        auto.IsCheckedChanged += (_, _) => { _settings.AutoUpdate = auto.IsChecked == true; SaveSettings(); };
        sp.Children.Add(auto);
        var when = new StackPanel { Name = "upd-when", Spacing = 4 };
        when.Children.Add(H("Wann installieren?", 14));
        foreach (var (id, label) in UpdateSchedule.Modes)
        {
            var rb = new RadioButton { Name = "upd-when-" + id, GroupName = "upd-when", Content = id == "nacht" ? $"Nachts um {_settings.UpdateHour} Uhr (wenn du nicht am PC bist)" : label, IsChecked = _settings.UpdateWhen == id, Foreground = Text };
            var m = id; rb.IsCheckedChanged += (_, _) => { if (rb.IsChecked == true && _settings.UpdateWhen != m) { _settings.UpdateWhen = m; SaveSettings(); } };
            when.Children.Add(rb);
        }
        when.Children.Add(P("Beim Installieren zeigt Trendo kurz einen Bildschirm „Trendo wird aktualisiert“. Danach startet es neu und deine Fenster sind wieder da.", null, 11.5));
        sp.Children.Add(WCard(when));
        var row = new WrapPanel();
        var check = new Button { Name = "upd-check", Content = "Jetzt nach Updates suchen", Background = Accent, IsEnabled = !_core.Offline && !_updateBusy };
        check.Click += async (_, _) => { await UpdateCheckAsync(userAsked: true); Show("updates"); };
        row.Children.Add(Sp(check));
        if (_updateReady is { } v)
        {
            var now = new Button { Name = "upd-install", Content = $"Version {v} jetzt installieren" };
            now.Click += (_, _) => InstallUpdateNow();
            row.Children.Add(Sp(now));
        }
        var news = new Button { Name = "upd-news", Content = "Was ist neu?" }; news.Click += (_, _) => Show("neu");
        row.Children.Add(Sp(news));
        sp.Children.Add(row);
        // U5
        var back = new StackPanel { Spacing = 4 };
        back.Children.Add(H("Sicherheitsnetz", 14));
        var good = Boot.LastGood;
        back.Children.Add(P($"Vor jedem Update sichert Trendo die laufende Version. Startet eine neue Version dreimal nicht richtig, kehrt Trendo von selbst zur letzten guten Version zurück{(good is null ? "." : $" (zuletzt gut: {good}).")}", Text, 12.5));
        var hasBackup = Directory.Exists(Boot.BackupDir) && Directory.GetFiles(Boot.BackupDir, "*.nupkg").Length > 0;
        if (hasBackup && good is not null && good != Updater.Current)
        {
            var rb = new Button { Name = "upd-rollback", Content = $"Zurück zu Version {good}" };
            rb.Click += (_, _) => { _core.Audit.Append(Principal.LocalOwner.Name, "update.rollback", good); if (!Updater.RollbackTo(Boot.BackupDir, good)) { _updateMsg = "Die vorherige Version ist nicht mehr da."; Show("updates"); } };
            back.Children.Add(rb);
        }
        sp.Children.Add(WCard(back));
        return Page("Trendo-Updates", sp);
    }

    /// <summary>U4: Neuerungen der aktuellen Version in einfachen Worten.</summary>
    Control WhatsNewView()
    {
        var cur = Updater.Current;
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { Logo(56), new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
            new TextBlock { Text = $"Neu in Trendo OS {cur}", Foreground = GoldText(), FontSize = 22, FontWeight = FontWeight.Bold },
            P("Das hat sich mit dem letzten Update verbessert:", Text, 13) } } } });
        var notes = ReleaseNotes.For(_core.DataRoot, cur) ?? ReleaseNotes.For(_core.DataRoot, _updateReady ?? "");
        var points = notes is null ? [] : ReleaseNotes.Points(notes);
        var list = new StackPanel { Name = "neu-list", Spacing = 6 };
        foreach (var p in points.Take(30)) list.Children.Add(new TextBlock { Text = "✦  " + p, Foreground = Text, FontSize = 13.5, TextWrapping = TextWrapping.Wrap });
        if (points.Count == 0) list.Children.Add(P("Für diese Version gibt es noch keine Beschreibung.", null, 12.5));
        sp.Children.Add(WCard(list));
        var ok = new Button { Name = "neu-ok", Content = "Super, weiter", Background = Accent }; ok.Click += (_, _) => Show("start");
        sp.Children.Add(ok);
        return Page("Was ist neu?", sp);
    }
}
