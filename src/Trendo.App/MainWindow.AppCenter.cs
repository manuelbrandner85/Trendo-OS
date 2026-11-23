using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-04: Programme suchen, installieren, aktualisieren, entfernen – über die Paketverwaltung des Systems, jede Änderung mit Freigabe.</summary>
public partial class MainWindow
{
    public IPackageBackend? AppBackendOverride { get; set; }
    private AppCenter? _appCenter;
    public AppCenter AppCenter => _appCenter ??= new AppCenter(AppBackendOverride ?? AppCenter.Default(new RealProcessRunner()), _core.Approvals, _core.Audit);
    private IReadOnlyList<PackageInfo> _appHits = [], _appInstalled = [], _appUpdates = [];
    private string _appQuery = "", _appMsg = ""; private bool _appBusy;

    private void InitSys04()
    {
        _showMap["appcenter"] = AppCenterView; SysTabs.Add(("appcenter", "Programme verwalten"));
        StudioPages["SYS-04"] = "appcenter";
        ExtraPages.Add(("Programme verwalten", "appcenter", ["installieren", "deinstallieren", "entfernen", "aktualisieren", "update", "winget", "flatpak"]));
    }

    /// <summary>T-0681: Was Trendo gleich an den Programmen dieses PCs ändern will – Inhalt der Rückfrage.</summary>
    public sealed record AppChange(string Action, PackageInfo Package, string Title, string Text, string YesLabel);
    /// <summary>Tests: ersetzt den Rückfrage-Dialog. Ohne Zustimmung (false) wird nichts geändert.</summary>
    public Func<AppChange, Task<bool>>? AppConfirm { get; set; }

    AppChange AppChangeFor(string action, PackageInfo p)
    {
        var source = (p.Source.Length > 0 ? p.Source + " – " : "") + AppCenter.Backend.Name;
        var version = p.Version.Length > 0 ? p.Version : "nicht bekannt";
        // T-0755: der genaue Befehl steht schon in der Rückfrage – der Knopf startet genau diesen Befehl, sonst nichts.
        var cmd = $"\nBefehl: {AppCenter.Backend.CommandLine(action, p.Id)}";
        const string Run = "\n\nDu startest den Lauf selbst mit dem Knopf unten. Trendo zeigt danach Befehl, Fortschritt und Ausgabe, schreibt ein Protokoll und prüft am Ende nach. „Abbrechen“ beendet den Lauf jederzeit.";
        return action switch
        {
            "install" => new(action, p, $"„{p.Name}“ installieren?", $"Programm: {p.Name}\nPaket: {p.Id}\nVersion: {version}\nWoher: {source}\nGröße: vorab nicht bekannt – die Paketverwaltung nennt sie erst beim Laden.{cmd}\n\nTrendo lädt das Programm und installiert es auf diesem PC.{Run}", "Installieren"),
            "upgrade" => new(action, p, $"„{p.Name}“ aktualisieren?", $"Programm: {p.Name}\nPaket: {p.Id}\nVersion: {version}{(p.Available is { } v ? " → " + v : "")}\nWoher: {source}\nGröße: vorab nicht bekannt.{cmd}\n\nTrendo lädt die neue Version und ersetzt die bisherige.{Run}", "Aktualisieren"),
            _ => new(action, p, $"„{p.Name}“ entfernen?", $"Programm: {p.Name}\nPaket: {p.Id}\nVersion: {version}\nVerwaltet von: {source}{cmd}\n\nDas Programm wird von diesem PC entfernt. Das lässt sich nur durch erneutes Installieren rückgängig machen.{Run}", "Entfernen"),
        };
    }

    /// <summary>T-0755: der laufende bzw. letzte Lauf der Paketverwaltung – Befehl, Ausgabe, Fortschritt, Abbruch, Ergebnis mit Nachprüfung.</summary>
    public sealed class AppRunState(string action, PackageInfo package, string command)
    {
        public string Action { get; } = action; public PackageInfo Package { get; } = package; public string Command { get; } = command;
        readonly System.Text.StringBuilder _out = new();
        public string Output { get { lock (_out) return _out.ToString(); } }
        public void Add(string line) { lock (_out) _out.AppendLine(line); }
        public double? Progress { get; set; }
        public bool Running { get; set; } = true;
        public CancellationTokenSource Cts { get; } = new();
        public AppCenter.RunResult? Result { get; set; }
    }
    AppRunState? _appRun; (TextBox Output, ProgressBar Bar, TextBlock Status, AppRunState Run)? _appRunView;
    public AppRunState? AppRun => _appRun;
    /// <summary>Ordner der Lauf-Protokolle (eine Datei je Lauf).</summary>
    public string AppRunLogFolder => Path.Combine(_core.DataRoot, "protokolle", "programme");
    /// <summary>T-0755: „Abbrechen“ – beendet den eigenen Lauf (Prozess samt Kindprozessen).</summary>
    public void AppRunCancel() { if (_appRun is { Running: true } r && !r.Cts.IsCancellationRequested) { r.Cts.Cancel(); AppRunRefresh(r); } }

    void AppRunLine(AppRunState run, string raw)
    {
        var line = PackageParsers.CleanLine(raw);
        if (line.Length > 0) run.Add(line);
        if (PackageParsers.Progress(raw) is { } pr) run.Progress = pr;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => AppRunRefresh(run));
    }

    void AppRunRefresh(AppRunState run)
    {
        if (_appRunView is not { } v || !ReferenceEquals(v.Run, run)) return;
        v.Output.Text = run.Output; v.Output.CaretIndex = v.Output.Text?.Length ?? 0;
        v.Bar.IsIndeterminate = run.Running && run.Progress is null; v.Bar.Value = (run.Progress ?? (run.Running ? 0 : 1)) * 100;
        v.Status.Text = AppRunStatus(run);
    }

    static string Verb(string action) => action == "install" ? "installiert" : action == "upgrade" ? "aktualisiert" : "entfernt";

    static string AppRunStatus(AppRunState run) => run.Result is not { } r
        ? (run.Cts.IsCancellationRequested ? "Wird abgebrochen …" : run.Progress is { } p ? $"Läuft … {Math.Round(p * 100)} %" : "Läuft …")
        : r.Cancelled ? "Abgebrochen" : r.Verified ? "Fertig – nachgeprüft" : r.ExitOk ? "Fertig – Nachprüfung passt nicht" : "Fehlgeschlagen";

    /// <summary>T-0755: Ergebnis in Alltagssprache – der Rückgabewert allein reicht nicht, entscheidend ist die Nachprüfung.</summary>
    public static string AppRunText(PackageInfo p, AppCenter.RunResult r)
    {
        var verb = Verb(r.Action); var un = r.Action == "uninstall";
        var exit = r.ExitCode is { } e ? PackageParsers.ExitText(e) : "unbekannt";
        if (r.Cancelled) return $"{p.Name}: abgebrochen. " + (r.Installed is { } i ? (i ? "Das Programm ist installiert." : "Das Programm ist nicht installiert.") : "Ob das Programm installiert ist, ließ sich nicht prüfen.") + " Einzelheiten im Protokoll.";
        if (r.ExitOk && r.Verified) return $"{p.Name}: {verb} – nachgeprüft: {(un ? "steht nicht mehr in der Liste der installierten Programme" : "steht in der Liste der installierten Programme")}.";
        if (r.ExitOk && r.Installed is null) return $"{p.Name}: {verb} laut Paketverwaltung – die Nachprüfung war nicht möglich.";
        if (r.ExitOk) return $"{p.Name}: Die Paketverwaltung meldet Erfolg, aber Trendo findet das Programm {(un ? "noch" : "nicht")} in der Liste der installierten Programme. Sieh ins Protokoll.";
        if (r.Verified) return $"{p.Name}: Die Paketverwaltung meldet einen Fehler ({exit}), das Programm ist aber {verb}.";
        return $"{p.Name}: hat nicht geklappt ({exit}). Windows hat vielleicht nach Erlaubnis gefragt – versuch es noch einmal. Einzelheiten im Protokoll.";
    }

    Control AppRunCard(AppRunState run)
    {
        var status = new TextBlock { Name = "appc-run-status", Text = AppRunStatus(run), Foreground = Text, FontWeight = FontWeight.SemiBold };
        var bar = new ProgressBar { Name = "appc-run-progress", Minimum = 0, Maximum = 100, Height = 6, IsIndeterminate = run.Running && run.Progress is null, Value = (run.Progress ?? (run.Running ? 0 : 1)) * 100 };
        var output = new TextBox { Name = "appc-run-output", Text = run.Output, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = Mono, FontSize = 11.5, MinHeight = 80, MaxHeight = 220, PlaceholderText = "Noch keine Ausgabe …" };
        _appRunView = (output, bar, status, run);
        var btn = new WrapPanel();
        if (run.Running) btn.Children.Add(Act("appc-run-cancel", "Abbrechen", AppRunCancel));
        else
        {
            if (run.Result is { } r) btn.Children.Add(Act("appc-run-log", "Protokoll öffnen", () => Launch(r.LogFile)));
            var hide = Act("appc-run-close", "Ausblenden", () => { _appRun = null; Show("appcenter"); }); hide.Margin = new Thickness(6, 0, 0, 0); btn.Children.Add(hide);
        }
        var verb = run.Action == "install" ? "Installieren" : run.Action == "upgrade" ? "Aktualisieren" : "Entfernen";
        return Card(new StackPanel { Name = "appc-run", Spacing = 6, Children = {
            Row(new TextBlock { Text = $"{verb}: {run.Package.Name}", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold }, status),
            P("Befehl, den Trendo gestartet hat:", Muted, 11),
            new SelectableTextBlock { Name = "appc-run-cmd", Text = run.Command, FontFamily = Mono, FontSize = 11.5, Foreground = Text, TextWrapping = TextWrapping.Wrap },
            bar, output,
            run.Result is { } rr ? new SelectableTextBlock { Name = "appc-run-result", Text = AppRunText(run.Package, rr) + $"\nProtokoll: {rr.LogFile}", Foreground = rr.Verified ? Brush.Parse("#86EFAC") : Brush.Parse("#FACC15"), TextWrapping = TextWrapping.Wrap, FontSize = 12 } : new Panel(),
            btn } });
    }

    /// <summary>Rückfrage wie beim Teilen eines Ordners: eigenes Fenster, „Abbrechen“ ist vorbelegt, Esc und Schließen lehnen ab.</summary>
    async Task<bool> AppConfirmDialog(AppChange c)
    {
        var dlg = new Window { Title = c.Title, Width = 480, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Name = "appc-confirm-yes", Content = c.YesLabel };
        var no = new Button { Name = "appc-confirm-no", Content = "Abbrechen", IsDefault = true, IsCancel = true };
        yes.Click += (_, _) => dlg.Close(true);
        no.Click += (_, _) => dlg.Close(false);
        dlg.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; dlg.Close(false); } };
        dlg.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10, Children = {
            new TextBlock { Text = c.Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Name = "appc-confirm-text", Text = c.Text, TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { no, yes } } } };
        dlg.Opened += (_, _) => no.Focus();
        return await dlg.ShowDialog<bool>(this);
    }

    public async Task AppActionAsync(string action, PackageInfo p)
    {
        _appBusy = true;
        try
        {
            var change = AppChangeFor(action, p);
            var r = AppCenter.Plan(action, p, CurrentPrincipal); // T-0713: Rolle der angemeldeten Person
            // T-0681: Die Freigabe erteilt der Mensch, nicht derselbe Klick. Ohne ausdrückliches Ja wird sie abgelehnt und nichts geändert.
            bool yes;
            try { yes = await (AppConfirm ?? AppConfirmDialog)(change); }
            catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { yes = false; }
            if (!yes)
            {
                Ablehnen(r.Id);
                _core.Audit.Append(CurrentPrincipal.Name, "app.abgelehnt", $"{action} {p.Id}");
                _appMsg = $"{p.Name}: abgebrochen – es wurde nichts geändert.";
                return;
            }
            if (!FreigabeErteilen(r.Id)) { _appMsg = $"{p.Name}: nicht freigegeben – es wurde nichts geändert."; return; } // B4: Rolle der angemeldeten Person
            // T-0755: Lauf sichtbar – Befehl, Ausgabe, Fortschritt, Abbruch, Protokoll und Nachprüfung
            var run = _appRun = new AppRunState(action, p, AppCenter.Backend.CommandLine(action, p.Id));
            _appMsg = $"{p.Name}: läuft …";
            if (CurrentView == "appcenter") Show("appcenter");
            AppCenter.RunResult res;
            try { res = await AppCenter.RunAsync(r, AppRunLogFolder, l => AppRunLine(run, l), run.Cts.Token); }
            finally { run.Running = false; }
            run.Result = res;
            _appMsg = AppRunText(p, res);
            if ((res.ExitOk || res.Verified) && action != "install") _appUpdates = _appUpdates.Where(x => x.Id != p.Id).ToList();
            if (action == "uninstall" && (res.Verified || res.ExitOk && res.Installed is null)) _appInstalled = _appInstalled.Where(x => x.Id != p.Id).ToList();
        }
        catch (Exception ex) { _appMsg = "Hat nicht geklappt: " + Trendo.Core.FriendlyError.Say(ex); }
        finally { _appBusy = false; }
    }

    public async Task AppSearchAsync(string q) { _appQuery = q; _appHits = q.Trim().Length < 2 ? [] : await AppCenter.Backend.SearchAsync(q.Trim()); _appMsg = _appHits.Count == 0 ? $"Nichts gefunden für „{q}“." : $"{_appHits.Count} Treffer."; }
    public async Task AppLoadAsync() { _appInstalled = await AppCenter.Backend.InstalledAsync(); _appUpdates = await AppCenter.Backend.UpgradesAsync(); _appMsg = $"{_appInstalled.Count} Programme installiert, {_appUpdates.Count} Aktualisierung(en) verfügbar."; }

    private Control PkgRow(PackageInfo p, string prefix, int i, params (string Action, string Label)[] acts)
    {
        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (a, l) in acts)
        {
            var b = new Button { Name = $"{prefix}-{a}-{i}", Content = l, Padding = new Thickness(8, 2), IsEnabled = !_appBusy && !(_core.Offline && a != "uninstall") };
            b.Click += async (_, _) => { b.IsEnabled = false; b.Content = "Läuft …"; await AppActionAsync(a, p); Show("appcenter"); };
            btns.Children.Add(b);
        }
        return Row2(new StackPanel { Children = { new TextBlock { Text = p.Name, Foreground = Text, FontSize = 12.5 }, P($"{p.Id} · {p.Version}{(p.Available is { } v ? " → " + v : "")}", null, 10.5) } }, btns);
    }

    /// <summary>Y6 (07.11.2026): beliebte freie Programme nach Bereich als Karten. Ein Klick sucht das Programm in der Paketverwaltung –
    /// installiert wird erst über „Installieren“ im Treffer (mit Freigabe), wie bisher.</summary>
    public static readonly (string Area, (string Name, string Sub, string Icon, string C1, string C2)[] Apps)[] AppShelf =
    [
        ("Medien", [("VLC", "Spielt fast jedes Video ab", Mdi.PlayBox, "#FDBA74", "#C2410C"), ("Audacity", "Ton aufnehmen und schneiden", Mdi.Record, "#93C5FD", "#1D4ED8"), ("OBS Studio", "Aufnehmen und streamen", Mdi.Camera, "#C4B5FD", "#6D28D9")]),
        ("Grafik", [("GIMP", "Bilder bearbeiten", Mdi.Image, "#FCA5A5", "#B91C1C"), ("Inkscape", "Vektorgrafik", Mdi.Draw, "#86EFAC", "#15803D"), ("Krita", "Digital malen", Mdi.Palette, "#F9A8D4", "#BE185D")]),
        ("Büro & Werkzeuge", [("LibreOffice", "Texte, Tabellen, Präsentationen", Mdi.FileDocument, "#8FC2FF", "#2F6FE0"), ("7-Zip", "Archive packen und entpacken", Mdi.Folder, "#F5D27A", "#9C7419"), ("Notepad++", "Texteditor für alles", Mdi.NoteEdit, "#CBD5E1", "#475569"), ("KeePassXC", "Passwörter sicher speichern", Mdi.ShieldCheck, "#6EE7B7", "#047857")]),
    ];

    Control AppStoreShelf(TextBox query)
    {
        var sp = new StackPanel { Name = "appc-shelf", Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        foreach (var (area, apps) in AppShelf)
        {
            sp.Children.Add(new TextBlock { Text = area.ToUpperInvariant(), Foreground = Muted, FontSize = 11, FontWeight = FontWeight.Bold, LetterSpacing = 0.6, Margin = new Thickness(2, 6, 0, 2) });
            var row = new WrapPanel();
            foreach (var (name, sub, icon, c1, c2) in apps)
            {
                var nm = name;
                var b = new Button { Name = "appc-shelf-" + DevStudio.SafeName(name).ToLowerInvariant().Replace(' ', '-'), Width = 230, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(14),
                    Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch, IsEnabled = !_core.Offline,
                    Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, Children = { Chip(icon, c1, c2, 40),
                        Pos(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                            new TextBlock { Text = name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = sub, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap },
                            new TextBlock { Text = "kostenlos · freie Lizenz", Foreground = Brush.Parse("#86EFAC"), FontSize = 10 } } }, 1) } } };
                ToolTip.SetTip(b, $"„{name}“ in der Paketverwaltung suchen – installiert wird erst nach deinem Klick auf „Installieren“");
                b.Click += async (_, _) => { query.Text = nm; b.IsEnabled = false; try { await AppSearchAsync(nm); } catch (Exception ex) { _appMsg = $"Die Paketverwaltung antwortet nicht ({ex.Message})."; } Show("appcenter"); };
                Hover(b); row.Children.Add(b);
            }
            sp.Children.Add(row);
        }
        return sp;
    }

    private Control AppCenterView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P($"Trendo nutzt die {AppCenter.Backend.Name}. Vor jeder Änderung fragt Trendo dich – und schreibt alles ins Protokoll.") } };
        var q = new TextBox { Name = "appc-query", PlaceholderText = "Programm suchen, z. B. VLC", Text = _appQuery, Width = 300 };
        var go = new Button { Name = "appc-search", Content = "Suchen", Background = Accent, IsEnabled = !_core.Offline };
        go.Click += async (_, _) => { go.IsEnabled = false; try { await AppSearchAsync(q.Text ?? ""); } catch (Exception ex) { _appMsg = $"Die Paketverwaltung antwortet nicht ({ex.Message})."; } Show("appcenter"); };
        var load = new Button { Name = "appc-load", Content = "Installierte & Updates anzeigen" };
        load.Click += async (_, _) => { load.IsEnabled = false; try { await AppLoadAsync(); } catch (Exception ex) { _appMsg = $"Die Paketverwaltung antwortet nicht ({ex.Message})."; } Show("appcenter"); };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { q, go, load } });
        sp.Children.Add(new TextBlock { Name = "appc-msg", Text = _appMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        _appRunView = null;
        if (_appRun is { } run) sp.Children.Add(AppRunCard(run)); // T-0755
        if (_appHits.Count == 0 && _appInstalled.Count == 0 && _appUpdates.Count == 0) sp.Children.Add(AppStoreShelf(q)); // Y6: Ansicht wie ein App-Store
        var i = 0;
        foreach (var p in _appHits.Take(25)) sp.Children.Add(PkgRow(p, "appc-hit", i++, ("install", "Installieren")));
        if (_appUpdates.Count > 0)
        {
            sp.Children.Add(H("Aktualisierungen", 14)); i = 0;
            foreach (var p in _appUpdates) sp.Children.Add(PkgRow(p, "appc-upd", i++, ("upgrade", "Aktualisieren")));
        }
        if (_appInstalled.Count > 0)
        {
            sp.Children.Add(H("Installiert", 14)); i = 0;
            foreach (var p in _appInstalled.Take(100)) sp.Children.Add(PkgRow(p, "appc-inst", i++, ("uninstall", "Entfernen")));
        }
        return SysPage("Programme verwalten", sp);
    }
}
