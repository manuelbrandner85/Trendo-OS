using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
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
        if (Focus.Until(now) is null) { if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
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
    }

    // ---------- Dateien ----------
    private Control AlltagFilesView()
    {
        var sp = new StackPanel { Spacing = 10 };
        const string V = "alltag-dateien";

        var rd = Field("ren-folder", "Ordner mit den Dateien"); var rp = Field("ren-pattern", "Muster, z. B. Urlaub_{n:000}", 220, "{name}"); var rf = Field("ren-find", "Suchen (optional)", 140); var rr = Field("ren-replace", "Ersetzen durch", 140);
        var prev = new StackPanel { Name = "ren-preview" };
        List<Move> Plan() => Renamer.Plan(Directory.GetFiles(Clean(rd)), rp.Text ?? "{name}", rf.Text ?? "", rr.Text ?? "");
        sp.Children.Add(Card(Section("Viele Dateien umbenennen", "Erst Vorschau ansehen, dann umbenennen. Mit „Rückgängig“ kommt alles zurück. Platzhalter: {name}, {n}, {n:000}, {datum}.",
            Line(rd, rp), Line(rf, rr,
                Act("ren-preview-btn", "Vorschau", () => Try(V, () => { prev.Children.Clear(); foreach (var m in Plan().Take(30)) prev.Children.Add(P($"{Path.GetFileName(m.From)} → {Path.GetFileName(m.To)}", Text, 11)); })),
                Act("ren-apply", "Umbenennen", () => Try(V, () => { var p = Plan(); _lastUndo = Renamer.Apply(p, Path.Combine(_core.DataRoot, "werkzeuge", "rueckgaengig")); _core.Audit.Append("app", "files.rename", $"{p.Count} Dateien"); Say($"{p.Count} Dateien umbenannt.", V); }), true),
                Act("ren-undo", "Rückgängig", () => Try(V, () => { if (_lastUndo is null) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen."); Renamer.Undo(_lastUndo); _lastUndo = null; Say("Zurückgesetzt.", V); }))), prev)));

        var dd = Field("dup-folder", "Ordner durchsuchen"); var dupOut = new StackPanel { Name = "dup-result" };
        sp.Children.Add(Card(Section("Doppelte Dateien finden", "Vergleicht den Inhalt (nicht nur den Namen). Trendo löscht nichts – du entscheidest selbst.",
            Line(dd, Act("dup-run", "Suchen", () => Try(V, () =>
            {
                dupOut.Children.Clear(); var g = FileAnalysis.Duplicates(Clean(dd));
                dupOut.Children.Add(P(g.Count == 0 ? "Keine doppelten Dateien gefunden." : $"{g.Count} Gruppen mit gleichem Inhalt:", Text));
                foreach (var grp in g.Take(20)) dupOut.Children.Add(P($"{ResourceMonitor.Size(new FileInfo(grp[0]).Length)}: " + string.Join("  ·  ", grp.Select(Path.GetFileName)), null, 11));
            }))), dupOut)));

        var sd = Field("du-folder", "Ordner, z. B. C:\\Users\\Name"); var duOut = new StackPanel { Name = "du-result" };
        sp.Children.Add(Card(Section("Speicherplatz", "Zeigt, welche Ordner und Dateien am meisten Platz brauchen.",
            Line(sd, Act("du-run", "Anzeigen", () => Try(V, () =>
            {
                duOut.Children.Clear(); var (total, top) = FileAnalysis.Analyze(Clean(sd));
                duOut.Children.Add(P($"Zusammen {ResourceMonitor.Size(total)}", Text));
                foreach (var u in top) duOut.Children.Add(P($"{(u.IsFolder ? "📁" : "📄")} {Path.GetFileName(u.Path)} – {ResourceMonitor.Size(u.Bytes)}", null, 11));
            }))), duOut)));

        var zs = Field("zip-src", "Ordner oder Datei zum Packen"); var zu = Field("zip-file", "ZIP-Datei zum Entpacken");
        sp.Children.Add(Card(Section("ZIP packen und entpacken", "Gepackt wird neben das Original. Unsichere Archive (die Dateien außerhalb des Zielordners ablegen wollen) werden abgelehnt.",
            Line(zs, Act("zip-pack", "Packen", () => Try(V, () => { var s = Clean(zs).TrimEnd('/', '\\'); var z = s + ".zip"; Archives.Pack([s], z); Say("Gepackt: " + Path.GetFileName(z), V); }))),
            Line(zu, Act("zip-unpack", "Entpacken", () => Try(V, () => { var z = Clean(zu); var d = Path.Combine(Path.GetDirectoryName(z)!, Path.GetFileNameWithoutExtension(z)); var n = Archives.Unpack(z, d); Say($"{n} Dateien entpackt nach {Path.GetFileName(d)}.", V); }))))));

        var pf = Field("photo-folder", "Ordner mit Fotos"); var pOut = new StackPanel { Name = "photo-preview" };
        sp.Children.Add(Card(Section("Fotos nach Datum ordnen", "Sortiert nach Aufnahmedatum in Ordner wie 2026/2026-10. Erst Probelauf, dann ausführen – rückgängig machbar.",
            Line(pf, Act("photo-sim", "Probelauf", () => Try(V, () => { pOut.Children.Clear(); var p = PhotoSorter.Plan(Clean(pf)); pOut.Children.Add(P($"{p.Count} Fotos würden verschoben:", Text)); foreach (var m in p.Take(20)) pOut.Children.Add(P($"{Path.GetFileName(m.From)} → {Path.GetRelativePath(Clean(pf), m.To)}", null, 11)); })),
                Act("photo-apply", "Ordnen", () => Try(V, () => { var p = PhotoSorter.Plan(Clean(pf)); _lastUndo = Renamer.Apply(p, Path.Combine(_core.DataRoot, "werkzeuge", "rueckgaengig")); Say($"{p.Count} Fotos einsortiert. „Rückgängig“ oben setzt es zurück.", V); }), true)), pOut)));

        var trash = new StackPanel { Spacing = 3, Name = "trash-list" };
        if (_trash.Count > 0) trash.Children.Add(P($"{_trash.Count} Einträge, zusammen {ResourceMonitor.Size(_trash.Sum(t => t.Bytes))}", Text));
        foreach (var t in _trash.Take(15)) trash.Children.Add(P($"{Path.GetFileName(t.OriginalPath)} – {ResourceMonitor.Size(t.Bytes)} – gelöscht {t.Deleted:dd.MM.yyyy}", null, 11));
        var empty = _trashAsk
            ? Line(P("Wirklich endgültig löschen? Das lässt sich nicht rückgängig machen.", Text), Act("trash-yes", "Ja, endgültig leeren", () => Try(V, () => { var n = Trash.Empty(_trash, true, _core.Audit); _trash = []; _trashAsk = false; Say($"{n} Einträge gelöscht.", V); }), true), Act("trash-no", "Abbrechen", () => { _trashAsk = false; Show(V); }))
            : Line(Act("trash-show", "Papierkorb anzeigen", () => { _trash = Trash.List(WindowsBin(), LinuxTrash()); Show(V); }), Act("trash-empty", "Papierkorb leeren …", () => { if (_trash.Count > 0) { _trashAsk = true; Show(V); } }));
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
        sp.Children.Add(Card(Section("Bild drehen oder verkleinern", "Es entsteht immer eine Kopie neben dem Original – das Original bleibt unverändert.",
            Line(img, Act("img-rotate", "Nach rechts drehen", () => Try(V, () => Say("Gespeichert: " + Path.GetFileName(ImageTools.Rotate(Clean(img), 90)), V))),
                Act("img-left", "Nach links drehen", () => Try(V, () => Say("Gespeichert: " + Path.GetFileName(ImageTools.Rotate(Clean(img), 270)), V)))),
            Line(size, Act("img-resize", "Verkleinern", () => Try(V, () => Say("Gespeichert: " + Path.GetFileName(ImageTools.Resize(Clean(img), int.TryParse(size.Text, out var s) ? s : 0)), V)))))));

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
            Line(au, fmt, Act("audio-run", "Umwandeln", () => Try(V, () =>
            {
                if (ffmpeg is null) throw new InvalidOperationException("Dafür fehlt ffmpeg.");
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
            Act("sw-reset", "Zurücksetzen", () => { _swStart = null; _swAcc = TimeSpan.Zero; Show(V); })))));

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
            Line(cf, Act("cal-import", "Einlesen", () => Try(V, () => Say($"{Cal.ImportFile(Clean(cf))} Termine eingelesen.", V))),
                Act("cal-export", "Als ICS speichern", () => Try(V, () => { var f = Path.Combine(WorkFolder("kalender"), "termine.ics"); Cal.ExportFile(f); Say("Gespeichert: " + f, V); }))), list)));
        return AlltagPage("Zeit & Kalender", sp);
    }

    private TimeSpan SwNow() => _swAcc + (_swStart is { } s ? DateTime.Now - s : TimeSpan.Zero);

    private async Task LoadWeather(string? lat, string? lon)
    {
        if (_core.Offline) { _weather = "Offline-Modus: Wetter wird nicht geladen."; Show("alltag-zeit"); return; }
        if (!double.TryParse(lat, System.Globalization.NumberStyles.Float, De, out var la) || !double.TryParse(lon, System.Globalization.NumberStyles.Float, De, out var lo)) { _weather = "Bitte Breite und Länge als Zahl eingeben."; Show("alltag-zeit"); return; }
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
            Line(Act("clip-grab", "Aktuelle Zwischenablage merken", async () => { var t = Clipboard is { } cb ? await cb.TryGetTextAsync() : null; Say(t is not null && Clips.Add(t) ? "Gemerkt." : "Nicht gemerkt (leer oder sieht wie ein Passwort aus).", V); }, true),
                manual, Act("clip-add", "Merken", () => Say(Clips.Add(manual.Text ?? "") ? "Gemerkt." : "Nicht gemerkt (leer oder sieht wie ein Passwort aus).", V)), Act("clip-clear", "Alles löschen", () => { Clips.Clear(); Show(V); })), clips)));

        var uv = Field("unit-value", "Wert", 100, "1"); var units = Units.All.ToList();
        var uf = new ComboBox { Name = "unit-from", ItemsSource = units, SelectedItem = "km" }; var ut = new ComboBox { Name = "unit-to", ItemsSource = units, SelectedItem = "meile" };
        var uo = new TextBlock { Name = "unit-result", Foreground = Text, FontSize = 14 };
        var rate = Field("cur-rate", "Kurs, z. B. 1 € = 1,08 $", 160); var amt = Field("cur-amount", "Betrag", 100);
        sp.Children.Add(Card(Section("Einheiten umrechnen", "Länge, Gewicht, Volumen, Fläche, Tempo und Temperatur. Für Geld trägst du den Kurs selbst ein – Trendo holt keine Kurse aus dem Internet.",
            Line(uv, uf, P("in"), ut, Act("unit-run", "Umrechnen", () => Try(V, () => { var v = double.Parse(Clean(uv), De); uo.Text = $"= {Calc.Format(Units.Convert(v, (string)uf.SelectedItem!, (string)ut.SelectedItem!))} {ut.SelectedItem}"; })), uo),
            Line(amt, P("×"), rate, Act("cur-run", "Geld umrechnen", () => Try(V, () => uo.Text = "= " + Calc.Format(Units.Currency(double.Parse(Clean(amt), De), double.Parse(Clean(rate), De)))))))));

        var hex = Field("color-hex", "#2F86F6", 120); var co = new TextBlock { Name = "color-result", Foreground = Text };
        var pf = Field("color-file", "Bild für die Pipette", 260); var px = Field("color-xy", "x,y", 90);
        void ShowColor((byte R, byte G, byte B) c) { var (h, s, l) = Trendo.Core.Sys.Colors.Hsl(c.R, c.G, c.B); co.Text = $"{Trendo.Core.Sys.Colors.Hex(c.R, c.G, c.B)} · {Trendo.Core.Sys.Colors.Rgb(c.R, c.G, c.B)} · Farbton {h}°, Sättigung {s} %, Helligkeit {l} %"; hex.Text = Trendo.Core.Sys.Colors.Hex(c.R, c.G, c.B); }
        sp.Children.Add(Card(Section("Farben", "Farbcode umrechnen oder mit der Pipette eine Stelle in einem Bild (z. B. Bildschirmfoto) auslesen. Der Wert landet in der Zwischenablage.",
            Line(hex, Act("color-run", "Umrechnen", () => Try(V, () => ShowColor(Trendo.Core.Sys.Colors.ParseHex(hex.Text ?? "")))),
                Act("color-copy", "Kopieren", async () => { if (Clipboard is { } cb && hex.Text is { } t) await cb.SetTextAsync(t); })),
            Line(pf, px, Act("color-pick", "Pipette", () => Try(V, () => { var xy = Clean(px).Split(',').Select(int.Parse).ToArray(); if (xy.Length != 2) throw new ArgumentException("Stelle bitte als x,y."); ShowColor(ImageTools.ColorAt(Clean(pf), xy[0], xy[1])); }))), co)));
        return AlltagPage("Kleine Helfer", sp);
    }

    private Button Act(string name, string label, Func<Task> click, bool primary = false)
    {
        var b = new Button { Name = name, Content = label, FontSize = 11.5, Padding = new Avalonia.Thickness(10, 3), Background = primary ? Accent : null };
        b.Click += async (_, _) => await click();
        return b;
    }
}
