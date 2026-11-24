using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>SYS-13 Qualität & Betrieb (freigegebener Katalog, 21.10.2026): Fehlerseite, Modul-Register, Leistung, Rückfall, Lizenzen.</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> QualTabs = [];
    public readonly Dictionary<string, string> PageModules = new(StringComparer.OrdinalIgnoreCase);
    private string _qualOut = "";
    /// <summary>Für Tests: lässt die nächste Seite absichtlich abstürzen.</summary>
    public string? FailNextPage { get; set; }
    public const string Repo = "manuelbrandner85/Trendo-OS";

    private void InitSys13()
    {
        // T-0763: „Alles prüfen“ ist die Startseite des Studios (erster Reiter) – für Endnutzer, mit Ampel und nächstem Schritt
        RegisterTab(QualTabs, "qual-check", "Alles prüfen", "SYS-13", QualCheckView, "alles prüfen", "prüfen", "ampel", "zustand", "check", "ist alles in ordnung");
        StudioPages["SYS-13"] = "qual-check";
        RegisterTab(QualTabs, "qual-leistung", "Leistung", "SYS-13", QualPerfView, "leistung", "messen", "schnell", "speicher", "protokoll prüfen", "manipulation");
        RegisterTab(QualTabs, "qual-rueckfall", "Rückfall & Lizenzen", "SYS-13", QualBackView, "rückfall", "downgrade", "vorherige version", "lizenzen", "nuget", "telemetrie");
    }
    private Control QualPage(string title, Control body) => W45Page(title, QualTabs, "qual", body);

    // P14 Fehlerseite statt Absturz
    private Control SafeView(string key)
    {
        try
        {
            if (FailNextPage == key) { FailNextPage = null; throw new InvalidOperationException("Testfehler"); }
            _titleMenu = null;
            return WithPromptBar(key, InAppShell(key) ? SettingsShell(key, WithExternalMenu(key, _showMap[key]())) : WithExternalMenu(key, _showMap[key]())); // T-0710: Menüleiste der Programme außerhalb der Hülle // E1/E2: Einstellungen wie Windows 11, KI-Zentrale wie LM Studio
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            string file; try { file = Path.GetFileName(CrashLog.Write(_core.DataRoot, ex, "Seite " + key)); } catch (IOException) { file = "–"; }
            _core.Audit.Append("app", "page.error", $"{key}: {ex.GetType().Name}");
            var back = new Button { Name = "error-home", Content = "Zum Startbildschirm", Background = Accent }; back.Click += (_, _) => Show("start");
            var again = new Button { Name = "error-retry", Content = "Nochmal versuchen" }; again.Click += (_, _) => Show(key);
            return new Border { Name = "error-page", Background = TileBg, CornerRadius = new Avalonia.CornerRadius(10), Padding = new Avalonia.Thickness(20), Child = new StackPanel { Spacing = 10, Children = {
                H("Diese Seite konnte nicht geöffnet werden", 18),
                P("Das ist ein Fehler in Trendo, nicht deiner. Deine Daten sind sicher. Ein Fehlerbericht ohne persönliche Angaben wurde gespeichert: " + file, Text),
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { back, again } } } } };
        }
    }

    // P13 Modul-Register
    public static readonly Dictionary<string, string> BasePageModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["start"] = "CORE-01", ["studios"] = "CORE-01", ["suche"] = "CORE-01", ["einrichtung"] = "CORE-01", ["projects"] = "CORE-01", ["approvals"] = "CORE-11",
        ["system"] = "SYS-13", ["systemcheck"] = "SYS-13", ["notbuilt"] = "SYS-13", ["fortschritt"] = "SYS-13", ["sicherung"] = "CORE-11", ["ressourcen"] = "SYS-07",
        ["ki"] = "CORE-02", ["keys"] = "CORE-02", ["settings"] = "SYS-12", ["updates"] = "SYS-12", ["neu"] = "SYS-12", ["personen"] = "SYS-12", ["desktop-symbole"] = "CORE-01", ["hinzufuegen"] = "SYS-11", ["ki-internet"] = "X-05", ["apps"] = "SYS-11", ["components"] = "SYS-11", ["studio-deps"] = "SYS-11",
        ["werkzeuge"] = "SYS-02", ["medien"] = "SYS-02", ["office"] = "SYS-10", ["downloads"] = "SYS-03",
        ["brief"] = "STUDIO-LETTER-01", ["akten"] = "STUDIO-LETTER-01", ["akte"] = "STUDIO-LETTER-01", ["ausgabe"] = "STUDIO-LETTER-01", ["briefkoepfe"] = "STUDIO-LETTER-01",
    };
    public string? ModuleOf(string key) => PageModules.TryGetValue(key, out var m) ? m : StudioPages.FirstOrDefault(kv => string.Equals(kv.Value, key, StringComparison.OrdinalIgnoreCase)).Key ?? BasePageModules.GetValueOrDefault(key) ?? (key.StartsWith("einrichtung-") ? "CORE-01" : null);
    public List<string> OrphanPages() => _showMap.Keys.Where(k => ModuleOf(k) is null).OrderBy(k => k).ToList();
    public IReadOnlyCollection<string> PageKeys => _showMap.Keys;

    // P15 Leistung
    public List<Quality.Measure> MeasurePages(IEnumerable<string> keys)
    {
        var res = new List<Quality.Measure>();
        foreach (var k in keys) { var sw = Stopwatch.StartNew(); Show(k); sw.Stop(); res.Add(new(k, sw.Elapsed, Quality.PageBudget)); }
        return res;
    }

    // ---------- T-0763 (SYS-13): „Alles prüfen“ für Endnutzer ----------
    IReadOnlyList<Checkup.Area> _checkAreas = []; DateTime? _checkAt;
    /// <summary>Letztes Ergebnis von „Alles prüfen“ (leer = noch nicht geprüft).</summary>
    public IReadOnlyList<Checkup.Area> CheckupAreas => _checkAreas;
    public Task<IReadOnlyList<Checkup.Area>>? CheckupTask { get; private set; }
    /// <summary>Für Tests: Programmordner der Installation (sonst der Ordner, aus dem Trendo läuft).</summary>
    public string? InstallDirOverride { get; set; }
    string InstallDir => InstallDirOverride ?? AppContext.BaseDirectory;
    bool CheckBusy => CheckupTask is { IsCompleted: false };

    /// <summary>Alle Bereiche prüfen – im Hintergrund, verändert nichts (Selbstprüfung ohne Reparatur), sendet nichts.</summary>
    public Task<IReadOnlyList<Checkup.Area>> RunCheckupAsync()
    {
        if (CheckupTask is { IsCompleted: false } running) return running;
        var comps = SetupPacks.ComponentsFor(_settings.StudioPacks.Count > 0 ? _settings.StudioPacks : _wizPacks).ToList();
        var root = _core.DataRoot; var dir = InstallDir; var audit = _core.Audit; var backups = Backups;
        CheckupTask = Task.Run(async () =>
        {
            var now = DateTime.Now; var areas = new List<Checkup.Area>();
            areas.Add(Checkup.Installation(InstallIntegrity.Check(dir)));
            var checks = await Task.WhenAll(comps.Select(async c => (C: c, R: await _deps.CheckAsync(c))));
            areas.Add(Checkup.Programs(checks.Length, checks.Where(x => x.R.State == ToolState.Missing).Select(x => x.C.Name).ToList(), checks.Where(x => x.R.State == ToolState.Broken).Select(x => x.C.Name).ToList()));
            areas.Add(Checkup.Data(new SelfHealer(root, audit).Run(false, now)));
            var (broken, _, _, _) = audit.Check();
            areas.Add(Checkup.Audit(broken, audit.ReadAll().Count));
            var last = backups.Latest();
            areas.Add(Checkup.Backup(last?.LastWriteTime, last is null || backups.Describe(last.FullName) is not { Complete: false }, now));
            long free; try { free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))!).AvailableFreeSpace; } catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { free = -1; }
            areas.Add(Checkup.Disk(free));
            areas.Add(Checkup.Crashes(CrashLog.List(root).Select(File.GetLastWriteTime).ToList(), now));
            if (broken is null) audit.Append(Principal.LocalOwner.Name, "pruefung.alles", $"{Checkup.Overall(areas)}: " + string.Join(", ", areas.Select(a => $"{a.Id}={a.Light}")));
            _ui.Post(() => { _checkAreas = areas; _checkAt = now; if (CurrentView == "qual-check") Show("qual-check"); });
            return (IReadOnlyList<Checkup.Area>)areas;
        });
        return CheckupTask;
    }

    static string LightColor(Checkup.Light l) => l switch { Checkup.Light.Gruen => "#4ADE80", Checkup.Light.Gelb => "#FACC15", _ => "#F87171" };
    static string LightWord(Checkup.Light l) => l switch { Checkup.Light.Gruen => "grün", Checkup.Light.Gelb => "gelb", _ => "rot" };

    private Control QualCheckView()
    {
        var sp = new StackPanel { Spacing = 10 };
        var busy = CheckBusy;
        var run = Act("check-run", busy ? "Prüft …" : "Alles prüfen", () => { _ = RunCheckupAsync(); Show("qual-check"); }, true); run.IsEnabled = !busy;
        sp.Children.Add(ToolCard("check-card", Mdi.ShieldCheck, "#FDE68A", "#B45309", "Ist alles in Ordnung?",
            "Ein Klick prüft Programmdateien, Zusatzprogramme, deine Daten, das Protokoll, die Sicherung, den Speicherplatz und Abstürze. Es wird nichts verändert und nichts gesendet.", Line(run)));
        if (_checkAreas.Count == 0)
        {
            sp.Children.Add(new TextBlock { Name = "check-empty", Text = busy ? "Trendo prüft gerade …" : "Noch nicht geprüft. Klicke auf „Alles prüfen“ – das dauert meist nur ein paar Sekunden.", Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            var all = Checkup.Overall(_checkAreas);
            sp.Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = {
                new Avalonia.Controls.Shapes.Ellipse { Width = 14, Height = 14, Fill = Brush.Parse(LightColor(all)), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                new TextBlock { Name = "check-summary", Text = Checkup.Sentence(_checkAreas), Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } } });
            sp.Children.Add(P($"Geprüft am {_checkAt:dd.MM.yyyy} um {_checkAt:HH:mm} Uhr.", Muted, 11));
            foreach (var a in _checkAreas.OrderByDescending(a => a.Light))
            {
                var info = new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = a.Title, Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Name = "check-finding-" + a.Id, Text = a.Finding, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Name = "check-next-" + a.Id, Text = "Nächster Schritt: " + a.Next, Foreground = a.Light == Checkup.Light.Gruen ? Muted : Brush.Parse(LightColor(a.Light)), FontSize = 12, TextWrapping = TextWrapping.Wrap } } };
                var dot = new Avalonia.Controls.Shapes.Ellipse { Name = "check-light-" + a.Id, Width = 12, Height = 12, Fill = Brush.Parse(LightColor(a.Light)), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Margin = new Avalonia.Thickness(0, 4, 0, 0) };
                ToolTip.SetTip(dot, "Ampel: " + LightWord(a.Light));
                Control go = a.Page is { } page && a.Light != Checkup.Light.Gruen ? Act("check-go-" + a.Id, "Hingehen", () => Show(page)) : new Panel();
                sp.Children.Add(Card(new Grid { Name = "check-area-" + a.Id, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, Children = { dot, Pos(info, 1), Pos(go, 2) } }));
            }
        }
        // Trennung Nutzer-/Entwickler-Prüfung: was den Bauzustand von Trendo zeigt (nicht deinen PC), steht nur in „Professionell“ – ehrlich beschriftet
        if (ProgramLevel == Trendo.Core.Programs.UserLevel.Profi)
            sp.Children.Add(Card(Section("Nur für Entwickler", "Diese Seiten zeigen, wie weit Trendo gebaut ist – nicht, ob dein PC in Ordnung ist.",
                Line(Act("check-dev-ampel", "Qualitäts-Ampel der Programme", () => Show("qualitaet")), Act("check-dev-progress", "Bau-Fortschritt", () => Show("fortschritt")), Act("check-dev-perf", "Seiten-Tempo messen", () => Show("qual-leistung"))))));
        else sp.Children.Add(new TextBlock { Name = "check-dev-hint", Text = "Prüfungen für Entwickler (Bauzustand von Trendo) zeigt die Bedienstufe „Professionell“.", Foreground = Muted, FontSize = 11.5 });
        return QualPage("Qualität – Alles prüfen", sp);
    }

    private Control QualPerfView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "qual-leistung";
        // Y11 (07.11.2026): Werkzeug-Karten mit Symbol
        sp.Children.Add(ToolCard("perf-card", Mdi.ChartLine, "#8FC2FF", "#2F6FE0", "Wie schnell ist Trendo?", "Öffnet die Hauptseiten nacheinander und misst die Zeit. Ziel: jede Seite unter 0,3 Sekunden.",
            Line(Act("perf-run", "Jetzt messen", () =>
            {
                long before = GC.GetTotalMemory(true);
                var m = MeasurePages(BasePageModules.Keys.Where(_showMap.ContainsKey).Where(k => k is not ("akte" or "ausgabe" or "einrichtung")));
                long after = GC.GetTotalMemory(true);
                var slow = m.Where(x => !x.Ok).ToList();
                _qualOut = $"{m.Count} Seiten · Mittelwert {Quality.Median(m.Select(x => x.Took)).TotalMilliseconds:0} ms · {(slow.Count == 0 ? "✓ alle im Ziel" : "⚠ zu langsam: " + string.Join(", ", slow.Select(s => $"{s.What} ({s.Took.TotalMilliseconds:0} ms)")))}\nSpeicher danach: {(after - before) / 1048576.0:+0.0;-0.0} MB {(Quality.Leaks(before, after) ? "⚠ auffällig" : "✓ unauffällig")}";
                Say("Messung fertig.", V);
            }, true)), new TextBlock { Name = "perf-out", Text = _qualOut, Foreground = Text, TextWrapping = TextWrapping.Wrap }));
        sp.Children.Add(ToolCard("audit-card", Mdi.CheckboxMarkedOutline, "#86EFAC", "#15803D", "Protokoll prüfen", "Jede Aktion steht in einer Kette, in der jeder Eintrag den vorigen absichert. Wird etwas verändert oder gelöscht, fällt es hier auf.",
            Line(Act("audit-check", "Kette prüfen", () => { var c = _core.Audit.Check(); var b = c.Broken; // T-0726: mit Schlüssel auch die Signaturen; Abschnitte ohne Signatur werden genannt
                Say(b is null ? $"✓ Alle {_core.Audit.ReadAll().Count} Einträge unverändert{(_core.Audit.Keyed ? " und signiert" : "")}."
                    + (c.Unsigned.Count > 0 ? $" Davon {c.Unsigned.Count} ohne Signatur (ältere Trendo-Fassung oder Tresor beim Start nicht erreichbar) – ab Eintrag {c.Unsigned[0]}, im Protokoll festgehalten." : "")
                    : $"✕ Ab Eintrag {b} wurde das Protokoll verändert.", V); }))));
        return QualPage("Qualität – Leistung", sp);
    }

    private Control QualBackView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "qual-rueckfall";
        var cur = _core.Version; var vf = Field("rb-versions", "Veröffentlichte Versionen, z. B. 0.9.0, 0.9.1, 0.10.0", 360);
        sp.Children.Add(Card(Section("Zurück zur vorherigen Version", "Falls eine neue Version Probleme macht. Erst sichern, dann die ältere Version von GitHub installieren – Schritt für Schritt in RUECKFALL.md.",
            Line(Act("rb-prepare", "Sicherung für Rückfall anlegen", () => Try(V, () => Say("Gesichert in " + Quality.PrepareDowngrade(_core.DataRoot, cur), V)), true)),
            Line(vf, Act("rb-find", "Vorige Version finden", () => Try(V, () =>
            {
                var prev = Quality.PreviousVersion((vf.Text ?? "").Split(',', ' ', ';').Where(s => s.Length > 0), cur) ?? throw new ArgumentException("Keine ältere Version in der Liste.");
                _qualOut = $"Vorige Version: {prev}\nHerunterladen: {Quality.DowngradeUrl(Repo, prev)}\nDanach automatische Updates ausschalten."; Say("Gefunden.", V);
            }))), new TextBlock { Name = "rb-out", Text = _qualOut, Foreground = Text, TextWrapping = TextWrapping.Wrap })));
        var lic = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NUGET.md");
        var rows = File.Exists(lic) ? File.ReadAllLines(lic).Where(l => l.StartsWith("| ") && !l.StartsWith("| Paket")).ToList() : [];
        // T-0763: keine leeren Karten – fehlt die Liste im Programmordner, steht das ehrlich da statt „0 Bibliotheken“
        sp.Children.Add(Card(rows.Count > 0 ? Section("Bibliotheken und Lizenzen", $"{rows.Count} Bibliotheken sind in Trendo eingebaut. Die vollständige Liste liegt als THIRD_PARTY_NUGET.md im Programmordner.",
            new TextBlock { Name = "lic-list", Text = string.Join("\n", rows.Take(40).Select(r => string.Join(" · ", r.Trim('|').Split('|').Select(x => x.Trim())))), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap })
            : Section("Bibliotheken und Lizenzen", "Die Lizenzliste (THIRD_PARTY_NUGET.md) fehlt im Programmordner. Führe das Setup erneut aus, dann ist sie wieder da.", new TextBlock { Name = "lic-missing" })));
        // T-0763: geprüft statt nur behauptet – in der installierten Version über die Paketlisten (*.deps.json) im Programmordner
        var deps = Quality.DepsFiles(InstallDir); var hits = deps == 0 ? [] : Quality.TelemetryHitsInstalled(InstallDir);
        sp.Children.Add(Card(Section("Keine Telemetrie", "Trendo schickt keine Nutzungsdaten. Netzwerk nur, wenn du etwas lädst oder einen Online-Dienst nutzt.",
            new TextBlock { Name = "telemetry-result", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = hits.Count > 0 ? Brush.Parse("#F87171") : Text,
                Text = deps == 0 ? "Im Programmordner liegt keine Paketliste – hier lässt sich das nicht nachprüfen."
                    : hits.Count == 0 ? $"✓ Geprüft: keine Analyse- oder Telemetrie-Bibliothek in {deps} Paketliste(n) und den Programmbibliotheken gefunden."
                    : "✕ Gefunden: " + string.Join(", ", hits) })));
        return QualPage("Qualität – Rückfall & Lizenzen", sp);
    }
}
