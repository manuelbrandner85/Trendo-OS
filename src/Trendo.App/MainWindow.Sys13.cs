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
            return WithPromptBar(key, _showMap[key]());
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
        ["start"] = "CORE-01", ["studios"] = "CORE-01", ["suche"] = "CORE-01", ["einrichtung"] = "CORE-01", ["projects"] = "CORE-01", ["approvals"] = "CORE-11", ["arbeitsplatz"] = "X-11",
        ["system"] = "SYS-13", ["systemcheck"] = "SYS-13", ["notbuilt"] = "SYS-13", ["fortschritt"] = "SYS-13", ["sicherung"] = "CORE-11", ["ressourcen"] = "SYS-07",
        ["ki"] = "CORE-02", ["keys"] = "CORE-02", ["settings"] = "SYS-12", ["apps"] = "SYS-11", ["components"] = "SYS-11", ["studio-deps"] = "SYS-11",
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

    private Control QualPerfView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "qual-leistung";
        sp.Children.Add(Card(Section("Wie schnell ist Trendo?", "Öffnet die Hauptseiten nacheinander und misst die Zeit. Ziel: jede Seite unter 0,3 Sekunden.",
            Line(Act("perf-run", "Jetzt messen", () =>
            {
                long before = GC.GetTotalMemory(true);
                var m = MeasurePages(BasePageModules.Keys.Where(_showMap.ContainsKey).Where(k => k is not ("akte" or "ausgabe" or "einrichtung")));
                long after = GC.GetTotalMemory(true);
                var slow = m.Where(x => !x.Ok).ToList();
                _qualOut = $"{m.Count} Seiten · Mittelwert {Quality.Median(m.Select(x => x.Took)).TotalMilliseconds:0} ms · {(slow.Count == 0 ? "✓ alle im Ziel" : "⚠ zu langsam: " + string.Join(", ", slow.Select(s => $"{s.What} ({s.Took.TotalMilliseconds:0} ms)")))}\nSpeicher danach: {(after - before) / 1048576.0:+0.0;-0.0} MB {(Quality.Leaks(before, after) ? "⚠ auffällig" : "✓ unauffällig")}";
                Say("Messung fertig.", V);
            }, true)), new TextBlock { Name = "perf-out", Text = _qualOut, Foreground = Text, TextWrapping = TextWrapping.Wrap })));
        sp.Children.Add(Card(Section("Protokoll prüfen", "Jede Aktion steht in einer Kette, in der jeder Eintrag den vorigen absichert. Wird etwas verändert oder gelöscht, fällt es hier auf.",
            Line(Act("audit-check", "Kette prüfen", () => { var b = _core.Audit.FirstBroken(); Say(b is null ? $"✓ Alle {_core.Audit.ReadAll().Count} Einträge unverändert." : $"✕ Ab Eintrag {b} wurde das Protokoll verändert.", V); })))));
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
        sp.Children.Add(Card(Section("Bibliotheken und Lizenzen", $"{rows.Count} Bibliotheken sind in Trendo eingebaut. Die vollständige Liste liegt als THIRD_PARTY_NUGET.md im Programmordner.",
            new TextBlock { Name = "lic-list", Text = string.Join("\n", rows.Take(40).Select(r => string.Join(" · ", r.Trim('|').Split('|').Select(x => x.Trim())))), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap })));
        sp.Children.Add(Card(Section("Keine Telemetrie", "Trendo schickt keine Nutzungsdaten. Es gibt keine Analyse-Bibliothek; Netzwerk nur, wenn du etwas lädst oder einen Online-Dienst nutzt.", new TextBlock())));
        return QualPage("Qualität – Rückfall & Lizenzen", sp);
    }
}
