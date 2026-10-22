using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>SYS-02 Alltag (freigegebener Katalog, 21.10.2026): Dateien, Bilder & Medien, Zeit, kleine Helfer.</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> AlltagTabs = [];
    private string? _lastUndo, _lastShot;
    private List<TrashItem> _trash = [];
    private bool _trashAsk;
    private string _renFolder = "", _renPattern = "{name}", _renFind = "", _renReplace = "";
    private List<Move> _renamePreview = [];
    private bool _renameReady;
    private string _renameSig = "";
    private string _photoFolder = "";
    private List<Move> _photoPreview = [];
    private bool _photoReady;
    private string _photoSig = "";
    private string _duFolder = "", _duSummary = "";
    private List<FileAnalysis.Usage> _duTop = [];
    private string _dupFolder = "";
    private string _trashNote = "";
    /// <summary>Tests setzen das, damit ein Ordner- oder Dateidialog ohne echtes Fenster gewählt werden kann.</summary>
    internal Func<string, Task<string?>>? FolderDialog;
    internal Func<string, Task<string?>>? FileDialog;
    public void UseFolderDialog(Func<string, Task<string?>>? pick) => FolderDialog = pick;
    public void UseFileDialog(Func<string, Task<string?>>? pick) => FileDialog = pick;
    private DateTime? _swStart; private TimeSpan _swAcc;
    private string _weather = "";
    private DispatcherTimer? _remTimer;
    public ClipHistory Clips => new(_core.DataRoot);
    public ErinnerungStore Erinnerungen => new(_core.DataRoot);
    public FocusMode Focus => new(_core.DataRoot);
    public Trendo.Core.Sys.Calendar Cal => new(_core.DataRoot);

    private void InitSys02()
    {
        RegisterTab(AlltagTabs, "alltag-dateien", "Dateien", "SYS-02", AlltagFilesView, "umbenennen", "doppelte dateien", "duplikate", "speicherplatz", "zip", "entpacken", "archiv", "fotos ordnen", "papierkorb");
        RegisterTab(AlltagTabs, "alltag-bilder", "Bilder & Medien", "SYS-02", AlltagMediaView, "bild", "drehen", "verkleinern", "screenshot", "bildschirmfoto", "schwärzen", "bildschirmaufnahme", "audio", "mediathek");
        RegisterTab(AlltagTabs, "alltag-zeit", "Zeit & Kalender", "SYS-02", AlltagTimeView, "kalender", "termin", "ics", "erinnerung", "stoppuhr", "fokus", "nicht stören", "wetter", "uhr");
        RegisterTab(AlltagTabs, "alltag-helfer", "Kleine Helfer", "SYS-02", AlltagHelperView, "zwischenablage", "einheiten", "umrechnen", "farbe", "hex", "rgb", "warnung");
        _remTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _remTimer.Tick += (_, _) => CheckReminders(DateTime.Now);
        _remTimer.Start();
    }

    /// <summary>Fällige Erinnerungen melden – im Fokusmodus leise (nur Eintrag, kein Hervorholen des Fensters).</summary>
    public List<Erinnerung> CheckReminders(DateTime now)
    {
        var due = Erinnerungen.TakeDue(now);
        if (due.Count == 0) return due;
        _core.Audit.Append("app", "reminder.due", string.Join(", ", due.Select(d => d.Text)));
        _w45Msg = "Erinnerung: " + string.Join(" · ", due.Select(d => d.Text));
        if (A11y.MayInterrupt(_settings.NotifyStyle, Focus.Until(now) is not null)) { if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
        return due;
    }

    private Control AlltagPage(string title, Control body) => W45Page(title, AlltagTabs, "alltag", body);
    private TextBox Field(string name, string hint, double w = 340, string? text = null) => new() { Name = name, PlaceholderText = hint, Width = w, Text = text };
    private static string Clean(TextBox t) => t.Text?.Trim().Trim('"') ?? "";
    private static Control Section(string title, string help, params Control[] body)
    {
        var sp = new StackPanel { Spacing = 5, Children = { H(title, 14), P(help, null, 11) } };
        foreach (var b in body) sp.Children.Add(b);
        return sp;
    }
    private WrapPanel Line(params Control[] c) { var w = new WrapPanel(); foreach (var x in c) { x.Margin = new Avalonia.Thickness(0, 0, 6, 6); x.VerticalAlignment = VerticalAlignment.Center; w.Children.Add(x); } return w; }

    private void Try(string view, Action a)
    {
        try { a(); }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException) { Say(ex.Message, view); }
        catch (FormatException) { Say("Bitte eine gültige Zahl eintragen.", view); }
    }

    // ---------- Dateien ----------
    private Control AlltagFilesView()
    {
        var sp = new StackPanel { Spacing = 10 };
        const string V = "alltag-dateien";

        var rd = Field("ren-folder", "Ordner mit den Dateien", text: _renFolder); var rp = Field("ren-pattern", "Muster, z. B. Urlaub_{n:000}", 220, _renPattern); var rf = Field("ren-find", "Suchen (optional)", 140, _renFind); var rr = Field("ren-replace", "Ersetzen durch", 140, _renReplace);
        var prev = new StackPanel { Name = "ren-preview" };
        foreach (var m in _renamePreview.Take(30)) prev.Children.Add(P($"{Path.GetFileName(m.From)} → {Path.GetFileName(m.To)}", Text, 11));
        void RememberRename() { _renFolder = Clean(rd); _renPattern = rp.Text ?? "{name}"; _renFind = rf.Text ?? ""; _renReplace = rr.Text ?? ""; }
        string RenSig() => _renFolder + "|" + _renPattern + "|" + _renFind + "|" + _renReplace;
        List<Move> Plan() => Renamer.Plan(Directory.GetFiles(_renFolder), _renPattern, _renFind, _renReplace);
        sp.Children.Add(Card(Section("Viele Dateien umbenennen", "Erst Vorschau ansehen, dann umbenennen. Mit „Rückgängig“ kommt alles zurück. Platzhalter: {name}, {n}, {n:000}, {datum}.",
            Line(rd, rp), Line(rf, rr,
                Act("ren-preview-btn", "Vorschau", () =>
                {
                    RememberRename();
                    if (!Directory.Exists(_renFolder)) { _renameReady = false; _renamePreview = []; Say("Bitte zuerst einen Ordner wählen, dessen Dateien du umbenennen willst.", V); return; }
                    Try(V, () => { _renamePreview = Plan(); _renameSig = RenSig(); _renameReady = _renamePreview.Count > 0; Say(_renamePreview.Count == 0 ? "Vorschau: in diesem Ordner liegt keine Datei." : $"Vorschau: {_renamePreview.Count} Dateien können umbenannt werden.", V); });
                }),
                Act("ren-apply", "Umbenennen", async () =>
                {
                    RememberRename();
                    if (!Directory.Exists(_renFolder)) { var picked = await PickFolderAsync("Ordner mit den Dateien"); if (!string.IsNullOrEmpty(picked)) { _renFolder = picked; rd.Text = picked; } }
                    if (!Directory.Exists(_renFolder)) { Say("Umbenennen braucht einen Ordner. Wähl ihn im Dialog oder trag ihn ins Feld.", V); return; }
                    try
                    {
                        if (!_renameReady || _renameSig != RenSig())
                        {
                            _renamePreview = Plan(); _renameSig = RenSig(); _renameReady = _renamePreview.Count > 0;
                            Say(_renamePreview.Count == 0 ? "In diesem Ordner gibt es nichts umzubenennen." : $"Vorschau: {_renamePreview.Count} Dateien. Nochmal auf „Umbenennen“ klicken, dann wird umbenannt.", V);
                            return;
                        }
                        var p = Plan(); _renameReady = false; _renamePreview = [];
                        _lastUndo = Renamer.Apply(p, Path.Combine(_core.DataRoot, "werkzeuge", "rueckgaengig"));
                        _core.Audit.Append("app", "files.rename", $"{p.Count} Dateien");
                        Say($"{p.Count} Dateien umbenannt.", V);
                    }
                    catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { Say(ex.Message, V); }
                }, true),
                Act("ren-undo", "Rückgängig", () => Try(V, () => { if (_lastUndo is null) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen."); Renamer.Undo(_lastUndo); _lastUndo = null; _renameReady = false; Say("Umbenennen rückgängig gemacht.", V); }))), prev)));

        var dd = Field("dup-folder", "Ordner durchsuchen", text: _dupFolder);
        var dupOut = new StackPanel { Name = "dup-result" };
        sp.Children.Add(Card(Section("Doppelte Dateien finden", "Vergleicht den Inhalt (nicht nur den Namen). Trendo löscht nichts – du entscheidest selbst.",
            Line(dd, Act("dup-run", "Suchen", async () =>
            {
                _dupFolder = Clean(dd);
                if (!Directory.Exists(_dupFolder)) { var picked = await PickFolderAsync("Ordner nach doppelten Dateien durchsuchen"); if (!string.IsNullOrEmpty(picked)) _dupFolder = picked; }
                if (!Directory.Exists(_dupFolder)) { Say("Bitte zuerst den Ordner angeben, in dem nach doppelten Dateien gesucht wird.", V); return; }
                try
                {
                    var g = FileAnalysis.Duplicates(_dupFolder);
                    _w45Msg = g.Count == 0 ? "Keine doppelten Dateien gefunden." : $"{g.Count} Gruppen mit gleichem Inhalt.";
                    dupOut.Children.Clear();
                    dupOut.Children.Add(P(_w45Msg, Text));
                    foreach (var grp in g.Take(20)) dupOut.Children.Add(P($"{ResourceMonitor.Size(new FileInfo(grp[0]).Length)}: " + string.Join("  ·  ", grp.Select(Path.GetFileName)), null, 11));
                    Show(V);
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { Say(ex.Message, V); }
            })), dupOut)));

        var sd = Field("du-folder", "Ordner, z. B. C:\\Users\\Name", text: _duFolder); var duOut = new StackPanel { Name = "du-result" };
        if (_duSummary.Length > 0) duOut.Children.Add(P(_duSummary, Text));
        foreach (var u in _duTop) duOut.Children.Add(P($"{(u.IsFolder ? "📁" : "📄")} {Path.GetFileName(u.Path)} – {ResourceMonitor.Size(u.Bytes)}", null, 11));
        sp.Children.Add(Card(Section("Speicherplatz", "Zeigt, welche Ordner und Dateien am meisten Platz brauchen.",
            Line(sd, Act("du-run", "Anzeigen", async () =>
            {
                _duFolder = Clean(sd);
                if (!Directory.Exists(_duFolder)) { var picked = await PickFolderAsync("Ordner, dessen Speicherplatz angezeigt wird"); if (!string.IsNullOrEmpty(picked)) _duFolder = picked; }
                if (!Directory.Exists(_duFolder)) { Say("Bitte zuerst den Ordner angeben, dessen Speicherplatz angezeigt werden soll.", V); return; }
                try { var (total, top) = FileAnalysis.Analyze(_duFolder); _duSummary = $"Zusammen {ResourceMonitor.Size(total)}"; _duTop = top; Say("Speicherplatz: " + _duSummary, V); }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { Say("Speicherplatz konnte nicht gelesen werden: " + ex.Message, V); }
            })), duOut)));

        var zs = Field("zip-src", "Ordner oder Datei zum Packen"); var zu = Field("zip-file", "ZIP-Datei zum Entpacken");
        sp.Children.Add(Card(Section("ZIP packen und entpacken", "Gepackt wird neben das Original. Unsichere Archive (die Dateien außerhalb des Zielordners ablegen wollen) werden abgelehnt.",
            Line(zs, Act("zip-pack", "Packen", async () =>
            {
                var s = Clean(zs).TrimEnd('/', '\\');
                if (s.Length == 0) { var picked = await PickFolderAsync("Ordner zum Packen"); if (!string.IsNullOrEmpty(picked)) s = picked; }
                if (s.Length == 0 || (!File.Exists(s) && !Directory.Exists(s))) { Say("Bitte zuerst einen Ordner oder eine Datei zum Packen wählen.", V); return; }
                try { var z = s + ".zip"; Archives.Pack([s], z); Say("Gepackt: " + Path.GetFileName(z), V); }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { Say(ex.Message, V); }
            })),
            Line(zu, Act("zip-unpack", "Entpacken", async () =>
            {
                var z = Clean(zu);
                if (z.Length == 0) { var picked = await PickFileAsync("ZIP-Datei", ["*.zip"]); if (!string.IsNullOrEmpty(picked)) z = picked; }
                if (!File.Exists(z)) { Say("Bitte zuerst die ZIP-Datei angeben, die entpackt werden soll.", V); return; }
                try { var d = Path.Combine(Path.GetDirectoryName(z)!, Path.GetFileNameWithoutExtension(z)); var n = Archives.Unpack(z, d); Say($"{n} Dateien entpackt nach {Path.GetFileName(d)}.", V); }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or InvalidDataException) { Say(ex.Message, V); }
            })))));

        var pf = Field("photo-folder", "Ordner mit Fotos", text: _photoFolder); var pOut = new StackPanel { Name = "photo-preview" };
        foreach (var m in _photoPreview.Take(20)) pOut.Children.Add(P($"{Path.GetFileName(m.From)} → {Path.GetRelativePath(_photoFolder.Length > 0 ? _photoFolder : ".", m.To)}", null, 11));
        sp.Children.Add(Card(Section("Fotos nach Datum ordnen", "Sortiert nach Aufnahmedatum in Ordner wie 2026/2026-10. Erst Probelauf, dann ausführen – rückgängig machbar.",
            Line(pf, Act("photo-sim", "Probelauf", () =>
            {
                _photoFolder = Clean(pf);
                if (!Directory.Exists(_photoFolder)) { _photoReady = false; Say("Bitte zuerst den Ordner mit den Fotos wählen.", V); return; }
                try { _photoPreview = PhotoSorter.Plan(_photoFolder); _photoSig = _photoFolder; _photoReady = true; Say(_photoPreview.Count == 0 ? "Probelauf: in diesem Ordner liegt kein Foto." : $"Probelauf: {_photoPreview.Count} Fotos würden verschoben.", V); }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { Say(ex.Message, V); }
            }),
                Act("photo-apply", "Ordnen", async () =>
                {
                    _photoFolder = Clean(pf);
                    if (!Directory.Exists(_photoFolder)) { var picked = await PickFolderAsync("Ordner mit Fotos"); if (!string.IsNullOrEmpty(picked)) _photoFolder = picked; }
                    if (!Directory.Exists(_photoFolder)) { Say("Ordnen braucht einen Ordner mit Fotos. Wähl ihn im Dialog oder trag ihn ins Feld.", V); return; }
                    try
                    {
                        if (!_photoReady || _photoSig != _photoFolder) { _photoPreview = PhotoSorter.Plan(_photoFolder); _photoSig = _photoFolder; _photoReady = true; Say(_photoPreview.Count == 0 ? "In diesem Ordner gibt es keine Fotos zum Ordnen." : $"Vorschau: {_photoPreview.Count} Fotos. Nochmal auf „Ordnen“ klicken, dann werden sie einsortiert.", V); return; }
                        var p = PhotoSorter.Plan(_photoFolder); _photoReady = false; _photoPreview = [];
                        if (p.Count == 0) { Say("Keine Fotos einsortiert.", V); return; }
                        _lastUndo = Renamer.Apply(p, Path.Combine(_core.DataRoot, "werkzeuge", "rueckgaengig"));
                        Say($"{p.Count} Fotos einsortiert. „Rückgängig“ oben setzt es zurück.", V);
                    }
                    catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { Say(ex.Message, V); }
                }, true)), pOut)));

        var trash = new StackPanel { Spacing = 3, Name = "trash-list" };
        if (_trash.Count > 0) trash.Children.Add(P($"{_trash.Count} Einträge, zusammen {ResourceMonitor.Size(_trash.Sum(t => t.Bytes))}", Text));
        else if (_trashNote.Length > 0) trash.Children.Add(new TextBlock { Name = "trash-note", Text = _trashNote, Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        foreach (var t in _trash.Take(15)) trash.Children.Add(P($"{Path.GetFileName(t.OriginalPath)} – {ResourceMonitor.Size(t.Bytes)} – gelöscht {t.Deleted:dd.MM.yyyy}", null, 11));
        var empty = _trashAsk
            ? Line(P("Wirklich endgültig löschen? Das lässt sich nicht rückgängig machen.", Text), Act("trash-yes", "Ja, endgültig leeren", () => Try(V, () => { var n = Trash.Empty(_trash, true, _core.Audit); _trash = []; _trashAsk = false; _trashNote = "Der Papierkorb ist jetzt leer."; Say($"{n} Einträge gelöscht.", V); }), true), Act("trash-no", "Abbrechen", () => { _trashAsk = false; Say("Leeren abgebrochen.", V); }))
            : Line(Act("trash-show", "Papierkorb anzeigen", () => { _trashNote = LoadTrash(); Say(_trashNote, V); }), Act("trash-empty", "Papierkorb leeren …", () =>
            {
                if (_trash.Count == 0) _trashNote = LoadTrash();
                if (_trash.Count == 0) { Say("Leeren geht nicht: " + (_trashNote.Contains("nicht verfügbar") ? _trashNote : "der Papierkorb ist leer."), V); return; }
                _trashAsk = true; Say("Bitte bestätigen: der Papierkorb wird endgültig geleert.", V);
            }));
        sp.Children.Add(Card(Section("Papierkorb", "Zeigt, was im Papierkorb liegt. Geleert wird nur nach deiner Bestätigung.", trash, empty)));
        return AlltagPage("Dateien", sp);
    }

    private static string? WindowsBin()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try { var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value; var d = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "$Recycle.Bin", sid ?? ""); return Directory.Exists(d) ? d : null; }
        catch (Exception) { return null; }
    }
    private static string? LinuxTrash() => OperatingSystem.IsWindows() ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash");

    private string LoadTrash()
    {
        if (!OperatingSystem.IsWindows())
        {
            var linux = LinuxTrash();
            if (linux is null || !Directory.Exists(linux)) { _trash = []; return "Papierkorb ist unter Linux nicht verfügbar."; }
            _trash = Trash.List(null, linux);
            return _trash.Count == 0 ? "Der Papierkorb ist leer." : $"{_trash.Count} Einträge im Papierkorb.";
        }
        var bin = WindowsBin();
        if (bin is null) { _trash = []; return "Papierkorb ist auf diesem Windows-Konto nicht verfügbar."; }
        _trash = Trash.List(bin, null);
        return _trash.Count == 0 ? "Der Papierkorb ist leer." : $"{_trash.Count} Einträge im Papierkorb.";
    }

    internal async Task<string?> PickFolderAsync(string title)
    {
        if (FolderDialog is { } over) return await over(title);
        try
        {
            var r = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return r.Count > 0 ? r[0].TryGetLocalPath() : null;
        }
        catch (Exception) { return null; }
    }

    internal async Task<string?> PickFileAsync(string title, string[]? patterns = null)
    {
        if (FileDialog is { } over) return await over(title);
        try
        {
            var opt = new FilePickerOpenOptions { Title = title, AllowMultiple = false };
            if (patterns is { Length: > 0 }) opt.FileTypeFilter = [new FilePickerFileType("Dateien") { Patterns = patterns.ToList() }];
            var r = await StorageProvider.OpenFilePickerAsync(opt);
            return r.Count > 0 ? r[0].TryGetLocalPath() : null;
        }
        catch (Exception) { return null; }
    }

    private async Task<string?> ImageOrDialog(TextBox box, string title)
    {
        var t = Clean(box);
        if (File.Exists(t)) return t;
        var p = await PickFileAsync(title, ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"]);
        if (p is not null && File.Exists(p)) { box.Text = p; return p; }
        return null;
    }

    // ---------- Bilder & Medien ----------
    private Control AlltagMediaView()
    {
        var sp = new StackPanel { Spacing = 10 };
        const string V = "alltag-bilder";
        var ffmpeg = ToolPath("ffmpeg") ?? Cli.Find("ffmpeg");

        var lib = new StackPanel { Name = "lib-list", Spacing = 2 };
        sp.Children.Add(Card(Section("Mediathek", "Musik, Videos und Bilder aus deinen Ordnern (bis 2000 Dateien). Klick spielt mit mpv oder VLC ab, sonst mit dem Windows-Programm.",
            Line(Act("lib-scan", "Ordner durchsuchen", () =>
            {
                lib.Children.Clear();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var items = MediaLibrary.Index([Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), ProjectsRoot]);
                lib.Children.Add(P($"{items.Count} Dateien gefunden ({items.Count(i => i.Kind == "Musik")} Musik, {items.Count(i => i.Kind == "Video")} Videos, {items.Count(i => i.Kind == "Bild")} Bilder) in {sw.Elapsed.TotalSeconds:0.0} s.", Text));
                var i = 0;
                foreach (var m in items.Where(x => x.Kind != "Bild").OrderByDescending(x => x.Changed).Take(25)) { var f = m.Path; lib.Children.Add(Act($"lib-play-{i++}", $"▶ {Path.GetFileName(f)}", () => Play(f))); }
            })), lib)));

        var img = Field("img-file", "Bilddatei (PNG, JPG …)");
        var size = Field("img-size", "längste Seite in Pixeln", 160, "1600");
        async Task Turn(int degrees, string missing)
        {
            var file = await ImageOrDialog(img, "Bild wählen");
            if (file is null) { Say(missing, V); return; }
            try { Say("Gespeichert: " + Path.GetFileName(ImageTools.Rotate(file, degrees)), V); }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { Say(ex.Message, V); }
        }
        sp.Children.Add(Card(Section("Bild drehen oder verkleinern", "Es entsteht immer eine Kopie neben dem Original – das Original bleibt unverändert.",
            Line(img, Act("img-rotate", "Nach rechts drehen", () => Turn(90, "Bitte zuerst ein Bild wählen, das nach rechts gedreht werden soll.")),
                Act("img-left", "Nach links drehen", () => Turn(270, "Bitte zuerst ein Bild wählen, das nach links gedreht werden soll."))),
            Line(size, Act("img-resize", "Verkleinern", async () =>
            {
                var file = await ImageOrDialog(img, "Bild zum Verkleinern");
                if (file is null) { Say("Bitte zuerst ein Bild wählen, das verkleinert werden soll.", V); return; }
                if (!int.TryParse(size.Text, out var px) || px < 16) { Say("Bitte die längste Seite in Pixeln angeben, mindestens 16.", V); return; }
                try { Say("Gespeichert: " + Path.GetFileName(ImageTools.Resize(file, px)), V); }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { Say(ex.Message, V); }
            })))));

        var shotAreas = Field("shot-areas", "Schwärzen: x,y,Breite,Höhe; weitere mit ; trennen", 360);
        var shotFile = Field("shot-file", "oder vorhandenes Bild zum Schwärzen", 340, _lastShot);
        sp.Children.Add(Card(Section("Bildschirmfoto und Schwärzen", "Nimmt das Trendo-Fenster oder den ganzen Bildschirm auf (Bildschirm braucht ffmpeg). Schwärzen übermalt die Stellen dauerhaft in einer Kopie.",
            Line(Act("shot-window", "Trendo-Fenster aufnehmen", () => Try(V, () => { var p = ShotPath(); SaveScreenshot(p); _lastShot = p; Say("Gespeichert: " + p, V); })),
                Act("shot-screen", "Ganzen Bildschirm aufnehmen", () => Try(V, () =>
                {
                    if (ffmpeg is null) throw new InvalidOperationException("Dafür fehlt ffmpeg. Unter „Zusatzprogramme“ kostenlos installieren.");
                    var p = ShotPath(); var a = FfmpegJobs.RecordArgs(OperatingSystem.IsWindows(), p, 1); a.RemoveRange(a.IndexOf("-t"), a.Count - a.IndexOf("-t")); a.AddRange(["-frames:v", "1", p]);
                    var r = Cli.Run(ffmpeg, a, timeoutMs: 20000); if (!r.Ok || !File.Exists(p)) throw new InvalidOperationException("Aufnahme hat nicht geklappt.");
                    _lastShot = p; Say("Gespeichert: " + p, V);
                }))),
            Line(shotFile, shotAreas, Act("shot-redact", "Schwärzen", () => Try(V, () =>
            {
                var areas = (shotAreas.Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split(',').Select(x => int.Parse(x.Trim())).ToArray()).Where(a => a.Length == 4).Select(a => (a[0], a[1], a[2], a[3])).ToList();
                if (areas.Count == 0) throw new ArgumentException("Bitte mindestens einen Bereich angeben, z. B. 10,10,200,40.");
                Say("Geschwärzte Kopie: " + Path.GetFileName(ImageTools.Redact(Clean(shotFile), areas)), V);
            }))))));

        var recSec = Field("rec-seconds", "Sekunden", 90, "30"); var recAudio = Field("rec-audio", "Tongerät (leer = ohne Ton)", 220);
        sp.Children.Add(Card(Section("Bildschirm aufnehmen", ffmpeg is null ? "Dafür fehlt ffmpeg. Unter „Zusatzprogramme“ kostenlos installieren." : "Nimmt den ganzen Bildschirm als MP4 auf und legt die Datei in den Projektordner.",
            Line(recSec, recAudio, Act("rec-start", "Aufnahme starten", () => Try(V, () =>
            {
                if (ffmpeg is null) throw new InvalidOperationException("Dafür fehlt ffmpeg.");
                var o = Path.Combine(WorkFolder("aufnahmen"), $"aufnahme-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
                var a = FfmpegJobs.RecordArgs(OperatingSystem.IsWindows(), o, int.TryParse(recSec.Text, out var s) ? s : 0, recAudio.Text);
                _ = Task.Run(() => { var r = Cli.Run(ffmpeg, a, timeoutMs: (s + 30) * 1000); Dispatcher.UIThread.Post(() => Say(r.Ok && File.Exists(o) ? "Aufnahme gespeichert: " + o : "Aufnahme hat nicht geklappt.", V)); });
                Say($"Aufnahme läuft {s} Sekunden …", V);
            }), true)))));

        var au = Field("audio-file", "Audiodatei"); var fmt = new ComboBox { Name = "audio-format", ItemsSource = FfmpegJobs.AudioFormats.Select(f => f.ToUpperInvariant()).ToList(), SelectedIndex = 0 };
        sp.Children.Add(Card(Section("Audio umwandeln", "MP3, OGG oder WAV. Die neue Datei liegt neben dem Original, ihre Prüfsumme steht im Protokoll.",
            Line(au, fmt,                 Act("audio-run", "Umwandeln", () => Try(V, () =>
            {
                if (ffmpeg is null) throw new InvalidOperationException("Dafür fehlt ffmpeg.");
                if (!File.Exists(Clean(au))) throw new ArgumentException("Bitte die Audiodatei angeben, die umgewandelt werden soll.");
                var (ok, msg, sha) = FfmpegJobs.ConvertAudio(ffmpeg, Clean(au), FfmpegJobs.AudioFormats[Math.Max(0, fmt.SelectedIndex)], _core.Audit);
                Say(ok ? $"Fertig: {msg} (Prüfsumme {sha![..12]}…)" : msg, V);
            }))))));
        return AlltagPage("Bilder & Medien", sp);
    }

    private string ShotPath() => Path.Combine(WorkFolder("bildschirmfotos"), $"bildschirmfoto-{DateTime.Now:yyyyMMdd-HHmmssfff}.png");

    private void Play(string file)
    {
        var p = MediaLibrary.Player(file, ToolPath("mpv"), Cli.Find("vlc"));
        if (p is { } x) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(x.Exe) { ArgumentList = { x.Args[0], x.Args[1] }, UseShellExecute = false }); _core.Audit.Append("app", "media.play", Path.GetFileName(file)); }
        else Launch(file);
    }

    // ---------- Zeit & Kalender ----------
    private Control AlltagTimeView()
    {
        var sp = new StackPanel { Spacing = 10 };
        const string V = "alltag-zeit";
        var now = DateTime.Now;

        var wx = new TextBlock { Name = "weather", Text = _weather.Length > 0 ? _weather : "Wetter: nicht geladen", Foreground = Muted, FontSize = 11 };
        var lat = Field("wx-lat", "Breite, z. B. 52,52", 110, "52,52"); var lon = Field("wx-lon", "Länge, z. B. 13,40", 110, "13,40");
        sp.Children.Add(Card(Section("Uhr und Wetter", "Wetter ist freiwillig und kommt nur nach Klick aus dem Internet (Open-Meteo, kostenlos, ohne Konto). Ohne Internet funktioniert alles andere weiter.",
            P(now.ToString("dddd, dd. MMMM yyyy · HH:mm", De), Text, 14), Line(lat, lon, Act("wx-load", "Wetter laden", () => _ = LoadWeather(lat.Text, lon.Text)), wx))));

        var fu = Focus.Until(now);
        sp.Children.Add(Card(Section("Fokus", "Während des Fokus holt sich Trendo nicht nach vorn – Erinnerungen werden nur still notiert. Nach Ablauf ist alles automatisch wieder normal.",
            fu is { } u ? Line(P($"Fokus läuft bis {u:HH:mm} Uhr.", Text), Act("focus-stop", "Beenden", () => { Focus.Stop(); Show(V); }))
                        : Line(Act("focus-25", "25 Minuten", () => { Focus.Start(TimeSpan.FromMinutes(25), DateTime.Now); Show(V); }, true), Act("focus-60", "1 Stunde", () => { Focus.Start(TimeSpan.FromHours(1), DateTime.Now); Show(V); })))));

        var swText = new TextBlock { Name = "sw-time", Foreground = Text, FontSize = 18, Text = SwNow().ToString(@"hh\:mm\:ss\.f") };
        sp.Children.Add(Card(Section("Stoppuhr", "Misst die Zeit, auch wenn du die Seite wechselst.", Line(swText,
            Act("sw-start", _swStart is null ? "Start" : "Pause", () => { if (_swStart is null) _swStart = DateTime.Now; else { _swAcc += DateTime.Now - _swStart.Value; _swStart = null; } Show(V); }, true),
            Act("sw-reset", "Zurücksetzen", () =>
            {
                var lief = _swStart is not null || _swAcc != TimeSpan.Zero;
                _swStart = null; _swAcc = TimeSpan.Zero;
                Say(lief ? "Stoppuhr zurückgesetzt." : "Die Stoppuhr steht schon auf null.", V);
            })))));

        var rd = Field("rem-when", "Wann? TT.MM.JJJJ HH:MM oder Minuten", 230); var rt = Field("rem-text", "Woran erinnern?", 260);
        var rl = new StackPanel { Spacing = 2 };
        var ri = 0;
        foreach (var r in Erinnerungen.All().Where(r => !r.Done)) { var id = r.Id; rl.Children.Add(Row(P($"{r.At:dd.MM. HH:mm} – {r.Text}", Text, 11.5), Act($"rem-del-{ri++}", "Löschen", () => { Erinnerungen.Delete(id); Show(V); }))); }
        sp.Children.Add(Card(Section("Erinnerungen", "Trendo meldet sich zur eingestellten Zeit (auf die Sekunde, solange Trendo läuft).",
            Line(rd, rt, Act("rem-add", "Erinnern", () => Try(V, () =>
            {
                var w = Clean(rd);
                DateTime at = int.TryParse(w, out var min) ? DateTime.Now.AddMinutes(min) : DateTime.TryParseExact(w, "dd.MM.yyyy HH:mm", De, System.Globalization.DateTimeStyles.None, out var d) ? d : throw new ArgumentException("Zeit bitte als Minuten (z. B. 15) oder TT.MM.JJJJ HH:MM.");
                if (string.IsNullOrWhiteSpace(rt.Text)) throw new ArgumentException("Bitte schreib dazu, woran erinnert werden soll.");
                Erinnerungen.Add(at, rt.Text); Say($"Erinnerung um {at:HH:mm} Uhr gespeichert.", V);
            }), true)), rl)));

        var cd = Field("cal-date", "TT.MM.JJJJ HH:MM", 150); var cl = Field("cal-len", "Dauer (Min.)", 100, "60"); var ct = Field("cal-title", "Termin", 220); var co = Field("cal-place", "Ort (optional)", 160);
        var cf = Field("cal-file", "ICS-Datei zum Einlesen", 300);
        var list = new StackPanel { Spacing = 2 }; var ci = 0;
        foreach (var e in Cal.All().Where(e => e.End >= DateTime.Today).Take(20)) { var uid = e.Uid; list.Children.Add(Row(P($"{e.Start:ddd dd.MM. HH:mm} – {e.Title}{(e.Location.Length > 0 ? " (" + e.Location + ")" : "")}", Text, 11.5), Act($"cal-del-{ci++}", "Löschen", () => { Cal.Delete(uid); Show(V); }))); }
        if (ci == 0) list.Children.Add(P("Keine anstehenden Termine."));
        sp.Children.Add(Card(Section("Kalender", "Deine Termine bleiben auf diesem Rechner. Als ICS-Datei kannst du sie mit Outlook, Google oder dem Handy austauschen.",
            Line(cd, cl, ct, co, Act("cal-add", "Eintragen", () => Try(V, () =>
            {
                if (!DateTime.TryParseExact(Clean(cd), "dd.MM.yyyy HH:mm", De, System.Globalization.DateTimeStyles.None, out var s)) throw new ArgumentException("Datum bitte als TT.MM.JJJJ HH:MM.");
                if (string.IsNullOrWhiteSpace(ct.Text)) throw new ArgumentException("Bitte einen Titel eintragen.");
                Cal.Add(new(Guid.NewGuid().ToString("N"), s, s.AddMinutes(int.TryParse(cl.Text, out var m) ? m : 60), ct.Text.Trim(), co.Text?.Trim() ?? "")); Show(V);
            }), true)),
            Line(cf, Act("cal-import", "Einlesen", async () =>
            {
                var f = Clean(cf);
                if (!File.Exists(f)) { var picked = await PickFileAsync("ICS-Datei", ["*.ics"]); if (!string.IsNullOrEmpty(picked)) f = picked; }
                if (!File.Exists(f)) { Say("Bitte zuerst die ICS-Datei wählen, die eingelesen werden soll.", V); return; }
                try { Say($"{Cal.ImportFile(f)} Termine eingelesen.", V); }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or InvalidDataException) { Say(ex.Message, V); }
            }),
                Act("cal-export", "Als ICS speichern", () => Try(V, () => { var f = Path.Combine(WorkFolder("kalender"), "termine.ics"); Cal.ExportFile(f); Say("Gespeichert: " + f, V); }))), list)));
        return AlltagPage("Zeit & Kalender", sp);
    }

    private TimeSpan SwNow() => _swAcc + (_swStart is { } s ? DateTime.Now - s : TimeSpan.Zero);

    private async Task LoadWeather(string? lat, string? lon)
    {
        if (_core.Offline) { _weather = "Offline-Modus: Wetter wird nicht geladen."; Show("alltag-zeit"); return; }
        if (!double.TryParse(lat, System.Globalization.NumberStyles.Float, De, out var la) || !double.TryParse(lon, System.Globalization.NumberStyles.Float, De, out var lo)) { _weather = "Bitte Breite und Länge als Zahl eingeben."; Show("alltag-zeit"); return; }
        _weather = "Wetter wird geladen …"; Show("alltag-zeit");
        try { using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) }; _weather = "Wetter: " + (Weather.Parse(await http.GetStringAsync(Weather.Url(la, lo))) ?? "keine Daten"); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { _weather = "Wetter gerade nicht erreichbar."; }
        Show("alltag-zeit");
    }

    // ---------- Kleine Helfer ----------
    private Control AlltagHelperView()
    {
        var sp = new StackPanel { Spacing = 10 };
        const string V = "alltag-helfer";
        var m = _metrics.Sample();
        long? free = null; try { free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_core.DataRoot))!).AvailableFreeSpace; } catch (Exception) { }
        var warn = ResourceWarn.Check(m.RamPercent, free);
        sp.Children.Add(Card(Section("Hinweise zum Rechner", "Trendo warnt nur – es schließt oder löscht nichts von selbst.",
            warn.Count == 0 ? P("Alles im grünen Bereich.", Text) : new StackPanel { Name = "res-warn", Children = { P(string.Join("\n", warn), Avalonia.Media.Brush.Parse("#F97316")) } })));

        var clips = new StackPanel { Spacing = 2 }; var i = 0;
        foreach (var c in Clips.All())
        {
            var idx = i; var text = c;
            clips.Children.Add(Row(P(c.Length > 90 ? c[..90] + " …" : c, Text, 11.5), Line(Act($"clip-copy-{idx}", "Kopieren", async () => { if (Clipboard is { } cb) await cb.SetTextAsync(text); }), Act($"clip-del-{idx}", "Löschen", () => { Clips.Delete(idx); Show(V); }))));
            i++;
        }
        if (i == 0) clips.Children.Add(P("Noch nichts gemerkt."));
        var manual = Field("clip-add-text", "Text, den du dir merken willst", 340);
        sp.Children.Add(Card(Section("Zwischenablage-Verlauf", "Bis zu 50 Texte, nur auf diesem Rechner. Passwörter und Schlüssel werden nie gespeichert.",
            Line(Act("clip-grab", "Aktuelle Zwischenablage merken", async () =>
                {
                    Say("Zwischenablage wird gelesen …", V);
                    try
                    {
                        var t = Clipboard is { } cb ? await cb.TryGetTextAsync() : null;
                        Say(t is not null && Clips.Add(t) ? "Aus der Zwischenablage gemerkt." : "Nicht gemerkt (leer oder sieht wie ein Passwort aus).", V);
                    }
                    catch (Exception ex) { Say("Zwischenablage ließ sich nicht lesen: " + ex.Message, V); }
                }, true),
                manual, Act("clip-add", "Merken", () =>
                {
                    var t = manual.Text ?? "";
                    if (string.IsNullOrWhiteSpace(t)) { Say("Bitte einen Text eintragen, den du dir merken willst.", V); return; }
                    Say(Clips.Add(t) ? "Text gemerkt." : "Nicht gemerkt: das sieht aus wie ein Passwort oder ein Schlüssel.", V);
                }), Act("clip-clear", "Alles löschen", () =>
                {
                    if (Clips.All().Count == 0) { Say("Es ist nichts gemerkt, das gelöscht werden könnte.", V); return; }
                    Clips.Clear(); Say("Alles Gemerkte ist gelöscht.", V);
                })), clips)));

        var uv = Field("unit-value", "Wert", 100, "1"); var units = Units.All.ToList();
        var uf = new ComboBox { Name = "unit-from", ItemsSource = units, SelectedItem = "km" }; var ut = new ComboBox { Name = "unit-to", ItemsSource = units, SelectedItem = "meile" };
        var uo = new TextBlock { Name = "unit-result", Foreground = Text, FontSize = 14 };
        var rate = Field("cur-rate", "Kurs, z. B. 1 € = 1,08 $", 160); var amt = Field("cur-amount", "Betrag", 100);
        sp.Children.Add(Card(Section("Einheiten umrechnen", "Länge, Gewicht, Volumen, Fläche, Tempo und Temperatur. Für Geld trägst du den Kurs selbst ein – Trendo holt keine Kurse aus dem Internet.",
            Line(uv, uf, P("in"), ut, Act("unit-run", "Umrechnen", () => Try(V, () => { var v = double.Parse(Clean(uv), De); uo.Text = $"= {Calc.Format(Units.Convert(v, (string)uf.SelectedItem!, (string)ut.SelectedItem!))} {ut.SelectedItem}"; })), uo),
            Line(amt, P("×"), rate, Act("cur-run", "Geld umrechnen", () => Try(V, () =>
            {
                var betrag = Calc.Zahl(Clean(amt), "den Betrag");
                var kurs = Calc.Zahl(Clean(rate), "den Kurs");
                uo.Text = "= " + Calc.Format(Units.Currency(betrag, kurs)) + " (mit deinem Kurs)";
            }))))));

        var hex = Field("color-hex", "#2F86F6", 120); var co = new TextBlock { Name = "color-result", Foreground = Text };
        var pf = Field("color-file", "Bild für die Pipette", 260); var px = Field("color-xy", "x,y", 90);
        void ShowColor((byte R, byte G, byte B) c) { var (h, s, l) = Trendo.Core.Sys.Colors.Hsl(c.R, c.G, c.B); co.Text = $"{Trendo.Core.Sys.Colors.Hex(c.R, c.G, c.B)} · {Trendo.Core.Sys.Colors.Rgb(c.R, c.G, c.B)} · Farbton {h}°, Sättigung {s} %, Helligkeit {l} %"; hex.Text = Trendo.Core.Sys.Colors.Hex(c.R, c.G, c.B); }
        sp.Children.Add(Card(Section("Farben", "Farbcode umrechnen oder mit der Pipette eine Stelle in einem Bild (z. B. Bildschirmfoto) auslesen. Der Wert landet in der Zwischenablage.",
            Line(hex, Act("color-run", "Umrechnen", () => Try(V, () => { if (string.IsNullOrWhiteSpace(hex.Text)) throw new ArgumentException("Bitte eine Farbe eintragen, zum Beispiel #2F86F6."); ShowColor(Trendo.Core.Sys.Colors.ParseHex(hex.Text!)); })),
                Act("color-copy", "Kopieren", async () =>
                {
                    if (string.IsNullOrWhiteSpace(hex.Text)) { Say("Bitte zuerst eine Farbe eintragen, die kopiert werden soll.", V); return; }
                    try
                    {
                        var c = Trendo.Core.Sys.Colors.ParseHex(hex.Text);
                        var text = Trendo.Core.Sys.Colors.Hex(c.R, c.G, c.B);
                        if (Clipboard is { } cb) await cb.SetTextAsync(text);
                        Say("Kopiert: " + text, V);
                    }
                    catch (ArgumentException ex) { Say(ex.Message, V); }
                })),
            Line(pf, px, Act("color-pick", "Pipette", async () =>
            {
                var file = Clean(pf);
                if (!File.Exists(file)) { var picked = await PickFileAsync("Bild für die Pipette", ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]); if (!string.IsNullOrEmpty(picked)) { file = picked; pf.Text = picked; } }
                if (!File.Exists(file)) { Say("Bitte zuerst ein Bild für die Pipette wählen.", V); return; }
                var raw = Clean(px);
                if (raw.Length == 0) { Say("Bitte die Stelle im Bild als x,y eintragen, zum Beispiel 10,20.", V); return; }
                var parts = raw.Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var x) || !int.TryParse(parts[1].Trim(), out var y)) { Say("Die Stelle bitte als x,y eintragen, zum Beispiel 10,20.", V); return; }
                try { ShowColor(ImageTools.ColorAt(file, x, y)); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException) { Say(ex.Message, V); }
            })), co)));
        return AlltagPage("Kleine Helfer", sp);
    }

    private Button Act(string name, string label, Func<Task> click, bool primary = false)
    {
        var b = new Button { Name = name, Content = label, FontSize = 11.5, Padding = new Avalonia.Thickness(10, 3), Background = primary ? Accent : null };
        b.Click += async (_, _) => await click();
        return b;
    }
}
