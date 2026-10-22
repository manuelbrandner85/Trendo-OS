using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Welle 0: Ersteinrichtung, Basispaket, Studio-Pakete, KI nach Hardware, Updates & Sicherung, Systemcheck.</summary>
public sealed partial class MainWindow
{
    private UserSettings _settings = new();
    private int _wizStep;
    private readonly HashSet<string> _wizPacks = [];
    private string _updateInfo = "Noch nicht geprüft";

    private Backup Backups => new(_core.DataRoot, Path.Combine(_core.DataRoot, "backups"), _core.Audit);

    private void InitSetup(AppOptions opt)
    {
        if (string.IsNullOrEmpty(_settings.ProjectsFolder)) _settings.ProjectsFolder = opt.Isolated ? Path.Combine(_core.DataRoot, "Projekte") : UserSettings.DefaultProjectsFolder();
        foreach (var p in SetupPacks.Studios.Where(p => p.Default)) _wizPacks.Add(p.Id);
        _showMap["einrichtung"] = SetupWizardView; _showMap["systemcheck"] = SystemCheckView; _showMap["sicherung"] = BackupView;
        for (var i = 0; i < 5; i++) { var step = i; _showMap[$"einrichtung-{i}"] = () => { _wizStep = step; return SetupWizardView(); }; }
        // Geplante Sicherung beim Start (im Hintergrund, ohne Rückfrage – betrifft nur eigene Daten)
        if (_settings.SetupDone && Backups.Due(DateTime.Now, _settings.BackupEveryHours))
            _ = Task.Run(() => { try { Backups.Create(DateTime.Now); } catch (Exception ex) { _core.Audit.Append("app", "backup.failed", ex.GetType().Name); } });
        if (_settings.AutoUpdate && !_core.Offline) _ = CheckUpdatesAsync(apply: true);
    }

    private bool NeedsSetup(AppOptions opt) => opt.FirstRun && !_settings.SetupDone && opt.Page is null;

    // ---------- Ersteinrichtung (5 kurze Schritte) ----------
    private Control SetupWizardView()
    {
        var sp = new StackPanel { Spacing = 10, MaxWidth = 620, HorizontalAlignment = HorizontalAlignment.Left };
        string[] titles = ["Willkommen", "Deine Ordner", "Aussehen", "Was möchtest du nutzen?", "Fertig"];
        sp.Children.Add(P($"Schritt {_wizStep + 1} von {titles.Length} · dauert etwa 2 Minuten", null, 11));
        sp.Children.Add(H(titles[_wizStep], 18));
        Button Next(string label = "Weiter") { var b = new Button { Name = "wiz-next", Content = label, Background = Accent, Padding = new Thickness(16, 6) }; b.Click += (_, _) => { _wizStep++; _settings.Save(_core.DataRoot); Show("einrichtung"); }; return b; }
        var back = new Button { Name = "wiz-back", Content = "Zurück", IsVisible = _wizStep > 0 }; back.Click += (_, _) => { _wizStep--; Show("einrichtung"); };
        switch (_wizStep)
        {
            case 0:
                sp.Children.Add(P("Schön, dass du da bist! Trendo OS richtet sich jetzt in ein paar Schritten für dich ein. Du kannst alles später unter „Einstellungen“ ändern.", Text, 13));
                var name = new TextBox { Name = "wiz-name", Text = _settings.Name, PlaceholderText = "Wie dürfen wir dich nennen?", Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
                name.TextChanged += (_, _) => _settings.Name = name.Text?.Trim() ?? "";
                sp.Children.Add(name);
                sp.Children.Add(P("Sprache: Deutsch (weitere Sprachen folgen)", Text));
                break;
            case 1:
                sp.Children.Add(P("Hier speichert Trendo deine Projekte, Briefe und Vorlagen. Deine Daten bleiben auf diesem PC.", Text, 13));
                var folder = new TextBox { Name = "wiz-folder", Text = _settings.ProjectsFolder, Width = 460, HorizontalAlignment = HorizontalAlignment.Left };
                folder.TextChanged += (_, _) => _settings.ProjectsFolder = folder.Text?.Trim() ?? "";
                var pick = new Button { Content = "Ordner wählen …" };
                pick.Click += async (_, _) =>
                {
                    var r = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = "Ordner für Trendo OS" });
                    if (r.FirstOrDefault()?.TryGetLocalPath() is { } p) folder.Text = p;
                };
                sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { folder, pick } });
                sp.Children.Add(P($"Einstellungen und Sicherungen liegen getrennt in {_core.DataRoot} und bleiben auch bei einer Deinstallation erhalten.", null, 11));
                break;
            case 2:
                sp.Children.Add(P("Wie soll Trendo aussehen?", Text, 13));
                foreach (var (id, label) in new[] { ("dunkel", "Dunkel Premium (empfohlen)"), ("hell", "Hell"), ("kontrast", "Hoher Kontrast (gut lesbar)") })
                {
                    var rb = new RadioButton { Name = "wiz-theme-" + id, Content = label, GroupName = "theme", IsChecked = _settings.Theme == id, Foreground = Text };
                    rb.IsCheckedChanged += (_, _) => { if (rb.IsChecked == true && _settings.Theme != id) { _settings.Theme = id; Show("einrichtung"); } };
                    sp.Children.Add(rb);
                }
                // T-0210: sichtbare Vorschau der Wahl (wird nach „Weiter“ für ganz Trendo übernommen)
                var (bg, fg, edge) = _settings.Theme switch { "hell" => ("#F4F7FC", "#0F1B2E", "#335C99"), "kontrast" => ("#000000", "#FFFFFF", "#FFFF00"), _ => ("#0B1730", "#E6EDF7", "#2F86F6") };
                var themeLabel = _settings.Theme switch { "hell" => "Hell", "kontrast" => "Hoher Kontrast", _ => "Dunkel Premium" };
                sp.Children.Add(new Border { Name = "wiz-theme-preview", Background = Brush.Parse(bg), BorderBrush = Brush.Parse(edge), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10), Width = 320, HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Vorschau: " + themeLabel, Foreground = Brush.Parse(fg), FontWeight = FontWeight.SemiBold }, new TextBlock { Text = "So sehen Fenster und Texte aus.", Foreground = Brush.Parse(fg), FontSize = 11 } } } });
                sp.Children.Add(new TextBlock { Name = "wiz-theme-choice", Text = $"Gewählt: {themeLabel} – gilt nach „Weiter“ für ganz Trendo.", Foreground = Text, FontSize = 11 });
                break;
            case 3:
                var hw = HardwareProbe.Read(); var (model, why) = KiAdvisor.Choose(hw);
                sp.Children.Add(P("Das Basispaket ist immer dabei: " + SetupPacks.Base.Description + ". Wähle zusätzlich, was du brauchst – Trendo installiert es gleich mit.", Text, 13));
                foreach (var p in SetupPacks.Studios)
                {
                    var cb = new CheckBox { Name = "wiz-pack-" + p.Id, IsChecked = _wizPacks.Contains(p.Id), Foreground = Text,
                        Content = new StackPanel { Children = { new TextBlock { Text = p.Name, Foreground = Text, FontWeight = FontWeight.SemiBold }, P(p.Description + (p.Id == "ki" ? $" · Modell: {model}" : ""), null, 11) } } };
                    cb.IsCheckedChanged += (_, _) => { var changed = cb.IsChecked == true ? _wizPacks.Add(p.Id) : _wizPacks.Remove(p.Id); if (changed) Show("einrichtung"); };
                    sp.Children.Add(cb);
                }
                sp.Children.Add(new TextBlock { Name = "wiz-pack-summary", Text = "Gewählt: " + string.Join(", ", new[] { SetupPacks.Base.Name }.Concat(SetupPacks.Studios.Where(x => _wizPacks.Contains(x.Id)).Select(x => x.Name))), Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
                sp.Children.Add(P("KI-Empfehlung für diesen PC: " + why, null, 11));
                var comps = SetupPacks.ComponentsFor(_wizPacks).Where(c => !_deps.IsInstalled(c)).ToList();
                sp.Children.Add(P(comps.Count == 0 ? "Alles Ausgewählte ist schon vorhanden." :
                    $"Wird installiert: {string.Join(", ", comps.Select(c => c.Name))} (ca. {comps.Sum(c => c.ApproxMb)} MB). Mit „Zustimmen & installieren“ akzeptierst du die jeweiligen Lizenzen (siehe THIRD_PARTY.md). Programme, die nur systemweit installierbar sind, fragen einmal nach Administratorrechten.", null, 11));
                var go = new Button { Name = "wiz-install", Content = comps.Count == 0 ? "Weiter" : "Zustimmen & installieren", Background = Accent, Padding = new Thickness(16, 6), IsEnabled = !_depBusy };
                go.Click += async (_, _) => { _settings.StudioPacks = _wizPacks.ToList(); await RunSetupInstall(model); _wizStep = 4; Show("einrichtung"); };
                sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { back, go } });
                sp.Children.Add(_depProgress.Parent is null ? _depProgress : new Border()); sp.Children.Add(Detach(_depLog));
                return Page("Ersteinrichtung", sp);
            case 4:
                FinishSetup();
                sp.Children.Add(P($"Alles bereit{(string.IsNullOrEmpty(_settings.Name) ? "" : ", " + _settings.Name)}! Hier ist dein Systemcheck:", Text, 13));
                sp.Children.Add(SystemCheckPanel());
                var done = new Button { Name = "wiz-done", Content = "Los geht's", Background = Accent, Padding = new Thickness(16, 6) }; done.Click += (_, _) => Show("start");
                sp.Children.Add(done);
                return Page("Ersteinrichtung", sp);
        }
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { back, Next() } });
        return Page("Ersteinrichtung", sp);
    }

    private static Control Detach(Control c) { if (c.Parent is Panel p) p.Children.Remove(c); return c; }

    private async Task RunSetupInstall(string model)
    {
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        var list = SetupPacks.ComponentsFor(_settings.StudioPacks).ToList();
        if (_settings.StudioPacks.Contains("ki") && model == DependencyCatalog.LargeModel) list.Add(DependencyCatalog.Get("model-3b"));
        var todo = list.Where(c => !_deps.IsInstalled(c) && c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated).ToList();
        for (var i = 0; i < todo.Count; i++)
        {
            _depLog.Text = $"{i + 1}/{todo.Count}: {todo[i].Name} …";
            await InstallOne(todo[i], refresh: false);
        }
        if (_deps.HasModel(model)) KiModel.Select(_core.DataRoot, model, _core.Audit);
        ReloadStudios();
    }

    private void FinishSetup()
    {
        if (_settings.SetupDone) return;
        Directory.CreateDirectory(_settings.ProjectsFolder);
        var tpl = Path.Combine(_settings.ProjectsFolder, "Vorlagen");
        Directory.CreateDirectory(tpl);
        foreach (var n in new[] { "DIN5008-Geschaeftsbrief-FormB.docx", "DIN5008-Privatbrief-FormB.docx" })
        {
            var dest = Path.Combine(tpl, n);
            if (File.Exists(dest)) continue;
            using var s = AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/templates/{n}"));
            using var f = File.Create(dest); s.CopyTo(f);
        }
        _settings.SetupDone = true; _settings.Save(_core.DataRoot);
        _core.Audit.Append(Principal.LocalOwner.Name, "setup.done", $"Pakete: {string.Join(",", _settings.StudioPacks)}");
        try { Backups.Create(DateTime.Now); } catch { /* Sicherung später erneut */ }
    }

    // ---------- Systemcheck: bereit / fehlt / Ein-Klick-Reparatur ----------
    private Control SystemCheckView() => Page("Systemcheck", new StackPanel { Spacing = 8, Children = {
        P("Was Trendo auf diesem PC braucht – und ob es funktioniert. Fehlt etwas, reicht ein Klick auf „Reparieren“.", Text, 13), SystemCheckPanel() } });

    private Control SystemCheckPanel()
    {
        var host = new StackPanel { Spacing = 6, Name = "syscheck-host", Children = { P("Prüfe …", Text) } };
        var comps = SetupPacks.ComponentsFor(_settings.StudioPacks.Count > 0 ? _settings.StudioPacks : _wizPacks).ToList();
        // Prüfen im Hintergrund (startet Programme mit --version) – nie auf dem UI-Thread blockieren
        _ = Task.Run(async () =>
        {
            var checks = (await Task.WhenAll(comps.Select(async c => (C: c, R: await _deps.CheckAsync(c))))).ToList();
            await Dispatcher.UIThread.InvokeAsync(() => { host.Children.Clear(); host.Children.Add(BuildCheckGrid(checks)); });
        });
        return host;
    }

    private string _repairMsg = "";
    private Control BuildCheckGrid(List<(Component C, ToolCheck R)> checks)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*,130"), Name = "syscheck" };
        var rows = new List<(string Name, string State, string Detail, Component? Fix)>();
        foreach (var (c, ch) in checks)
            rows.Add((c.Name, ch.State switch { ToolState.Working => "Bereit", ToolState.Broken => "Defekt", _ => "Fehlt" }, ch.Version ?? ch.Detail ?? "",
                ch.State == ToolState.Working || c.Mode is not (DeliveryMode.AutoUser or DeliveryMode.AutoElevated) ? null : c));
        rows.Add(("Benutzerdaten", Directory.Exists(_core.DataRoot) ? "Bereit" : "Fehlt", _core.DataRoot, null));
        rows.Add(("Projektordner", Directory.Exists(_settings.ProjectsFolder) ? "Bereit" : "Fehlt", _settings.ProjectsFolder, null));
        var last = Backups.List().FirstOrDefault();
        rows.Add(("Sicherung", last is null ? "Fehlt" : "Bereit", last is null ? "noch keine" : $"zuletzt {last.LastWriteTime:dd.MM.yyyy HH:mm}", null));
        rows.Add(("Updates", !_settings.AutoUpdate ? "Aus" : _updateInfo.StartsWith("Aktuell") || _updateInfo.StartsWith("Update") ? "Bereit" : "Hinweis", _updateInfo, null));
        rows.Add(("Protokoll", _core.Audit.Verify() ? "Bereit" : "Defekt", "unverändert und vollständig", null));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var color = r.State switch { "Bereit" => "#4ADE80", "Aus" => "#93A4C3", "Hinweis" => "#FACC15", _ => "#F87171" };
            Control fix = r.Fix is { } c ? MakeFix(c) : new TextBlock();
            var cells = new Control[] { P(r.Name, Text, 12), new TextBlock { Text = $"● {r.State}  {r.Detail}", Foreground = Brush.Parse(color), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, fix };
            for (var k = 0; k < 3; k++) { Grid.SetRow(cells[k], i); Grid.SetColumn(cells[k], k); cells[k].Margin = new Thickness(0, 2); cells[k].VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(cells[k]); }
        }
        var sum = rows.Count(r => r.State == "Bereit");
        var panel = new StackPanel { Spacing = 6, Children = { P($"{sum} von {rows.Count} Punkten bereit.", Text), grid } };
        if (_repairMsg.Length > 0) panel.Children.Insert(0, new TextBlock { Name = "repair-msg", Text = _repairMsg, Foreground = _repairMsg.Contains('✓') ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), TextWrapping = TextWrapping.Wrap });
        return panel;
        Control MakeFix(Component c)
        {
            var b = new Button { Name = "fix-" + c.Id, Content = "Reparieren", FontSize = 11, IsEnabled = !_depBusy };
            b.Click += async (_, _) =>
            {
                _repairMsg = $"{c.Name}: wird repariert …"; Show(CurrentView);
                bool ok; try { ok = InstallOverride is { } fake ? await fake(c) : await InstallOne(c, refresh: false); } catch (Exception ex) { ok = false; _depLog.Text = ex.Message; }
                _repairMsg = ok ? $"{c.Name}: repariert ✓" : $"{c.Name}: hat nicht geklappt{(_core.Offline ? " – keine Internetverbindung" : "")}. {_depLog.Text}".Trim();
                ReloadStudios(); Show(CurrentView);
            };
            return b;
        }
    }

    // ---------- Sicherung & Wiederherstellung ----------
    private string _backupMsg = "";
    private string? _backupConfirm;
    private Control BackupView()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(P($"Trendo sichert deine Projekte und Einstellungen automatisch alle {_settings.BackupEveryHours} Stunden (die letzten 10 bleiben). Ablage: {Backups.Folder}", Text));
        var now = new Button { Name = "btn-backup-now", Content = "Jetzt sichern", Background = Accent };
        now.Click += (_, _) => { try { _backupMsg = "Gesichert: " + Path.GetFileName(Backups.Create(DateTime.Now)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _backupMsg = "Sichern hat nicht geklappt: " + ex.Message; } Show("sicherung"); };
        sp.Children.Add(now);
        if (_backupMsg.Length > 0) sp.Children.Add(P(_backupMsg, Brush.Parse("#4ADE80")));
        var bi = 0;
        foreach (var f in Backups.List())
        {
            var file = f.FullName; var idx = bi++;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { P($"{f.LastWriteTime:dd.MM.yyyy HH:mm} · {f.Length / 1024} KB", Text) } };
            if (_backupConfirm == file)
            {
                var yes = new Button { Name = $"backup-restore-yes-{idx}", Content = "Ja, wiederherstellen", FontSize = 11, Background = Accent };
                yes.Click += (_, _) =>
                {
                    _backupConfirm = null;
                    try { var n = Backups.Restore(file, DateTime.Now); _settings = UserSettings.Load(_core.DataRoot); _backupMsg = $"Wiederhergestellt: {n} Dateien. Der vorherige Stand wurde vorher gesichert."; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { _backupMsg = "Wiederherstellen hat nicht geklappt: " + ex.Message; }
                    Show("sicherung");
                };
                var no = new Button { Name = $"backup-restore-no-{idx}", Content = "Abbrechen", FontSize = 11 };
                no.Click += (_, _) => { _backupConfirm = null; _backupMsg = "Abgebrochen – nichts geändert."; Show("sicherung"); };
                row.Children.Add(P("Der aktuelle Stand wird ersetzt (vorher gesichert). Sicher?", Brush.Parse("#FACC15"), 11)); row.Children.Add(yes); row.Children.Add(no);
            }
            else
            {
                var rb = new Button { Name = $"backup-restore-{idx}", Content = "Wiederherstellen", FontSize = 11 };
                rb.Click += (_, _) => { _backupConfirm = file; _backupMsg = ""; Show("sicherung"); };
                row.Children.Add(rb);
            }
            sp.Children.Add(row);
        }
        sp.Children.Add(P("Beim Wiederherstellen wird der aktuelle Stand vorher gesichert. Das Protokoll, was Trendo getan hat, bleibt immer erhalten.", null, 11));
        return Page("Sicherung", sp);
    }

    // ---------- Auto-Updates über GitHub Releases (Velopack) ----------
    private async Task CheckUpdatesAsync(bool apply)
    {
        try
        {
            var um = new Velopack.UpdateManager(new Velopack.Sources.GithubSource("https://github.com/manuelbrandner85/Trendo-OS", null, true));
            if (!um.IsInstalled) { _updateInfo = "Nur in der installierten Version"; return; }
            var u = await um.CheckForUpdatesAsync();
            if (u is null) { _updateInfo = $"Aktuell (v{um.CurrentVersion})"; return; }
            _updateInfo = $"Update {u.TargetFullRelease.Version} wird geladen …";
            await um.DownloadUpdatesAsync(u);
            _core.Audit.Append("app", "update.downloaded", u.TargetFullRelease.Version.ToString());
            _updateInfo = $"Update {u.TargetFullRelease.Version} bereit – wird beim Beenden installiert";
            if (apply) um.WaitExitThenApplyUpdates(u.TargetFullRelease, silent: true, restart: false);
        }
        catch (Exception ex) { _updateInfo = ex is InvalidOperationException ? "Nur in der installierten Version" : "Gerade nicht erreichbar – später erneut"; }
    }
}

/// <summary>Liest RAM und Grafikkarte (nur lesend; Windows-Registry bzw. /proc).</summary>
internal static class HardwareProbe
{
    public static HardwareInfo Read()
    {
        var ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; string? gpu = null; long vram = 0;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var cls = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                foreach (var n in cls?.GetSubKeyNames().Where(n => n.All(char.IsDigit)) ?? [])
                {
                    using var k = cls!.OpenSubKey(n);
                    var mem = k?.GetValue("HardwareInformation.qwMemorySize") is long l ? l : k?.GetValue("HardwareInformation.MemorySize") is int i ? (uint)i : 0;
                    if (mem > vram) { vram = mem; gpu = k?.GetValue("DriverDesc") as string; }
                }
            }
            catch { /* ohne GPU-Infos weiter */ }
        }
        return new HardwareInfo(ram, gpu, vram);
    }
}
