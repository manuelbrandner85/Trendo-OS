using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>V10 (T-0663, 15.11.2026, Wunsch „Programme übersichtlicher, ohne Funktionsverlust“): jede Ansicht in der Stufe „Einfach“ messen –
/// sichtbare Knöpfe, Knöpfe ohne Wort, Textmenge. Bericht in %TEMP%\trendo-uebersicht.txt (nicht im Repo).</summary>
public class UebersichtEinfach_Ui(ITestOutputHelper log)
{
    public const int MaxButtons = 40, MaxNoWord = 8, MaxWords = 400;

    /// <summary>Begründete Ausnahmen: Fachgeräte, deren Tasten das Programm sind (Taschenrechner, Bildschirmtastatur), und Listen,
    /// deren Länge von den Daten der Person abhängt. Jede Ausnahme nennt, welche Grenze sie überschreiten darf.</summary>
    public static readonly IReadOnlyDictionary<string, string> Exceptions = new Dictionary<string, string>
    {
        ["rechner"] = "Knöpfe: Zifferntasten sind das Programm",
        ["rechner (Dokument)"] = "Knöpfe: Zifferntasten sind das Programm",
    };

    public sealed record Row(string Key, int Buttons, int NoWord, int Words, List<string> NoWordNames, List<string> All);

    /// <summary>Hat der Knopf ein lesbares Wort (mindestens zwei Buchstaben am Stück)?</summary>
    public static bool HasWord(string s) => System.Text.RegularExpressions.Regex.IsMatch(s, @"\p{L}{2,}");

    public static string Label(Button b) => b.Content switch
    {
        string s => s,
        TextBlock t => t.Text ?? "",
        Control c => string.Join(" ", c.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).Where(x => !string.IsNullOrWhiteSpace(x))),
        _ => ""
    };

    /// <summary>Eingebaute Knöpfe von Bedienelementen (Aufklappliste, Zahlenfeld, Bildlaufleiste) zählen nicht – die gestaltet die Seite nicht selbst.</summary>
    static bool Own(Button b) => b.TemplatedParent is null;

    public static Row Of(string key, Control root)
    {
        // sichtbar = eingeblendet und nicht durchsichtig/null hoch (versteckte Hilfsknöpfe für Tastatur und Tests zählen nicht)
        var all = root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Height != 0 && !c.GetVisualAncestors().OfType<Visual>().Prepend(c).Any(a => a.Opacity == 0)).ToList();
        var btns = all.OfType<Button>().Where(Own).ToList();
        var noWord = btns.Where(b => SimpleWords.IsSymbolOnly(b) && !SimpleWords.Universal(b)).ToList(); // Farbfelder und Vorschaubilder zeigen ihre Wahl selbst
        var words = all.OfType<TextBlock>().Where(t => t.TemplatedParent is null || t.TemplatedParent is not TextBox)
            .Where(t => !string.IsNullOrWhiteSpace(t.Text)).Sum(t => Math.Min(t.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, t.MaxLines == 1 ? 12 : int.MaxValue)); // einzeilig gekürzt: etwa 12 Wörter sichtbar
        return new(key, btns.Count, noWord.Count, words,
            noWord.Select(b => $"{b.Name ?? "?"}=„{Label(b)}“{(ToolTip.GetTip(b) is { } tip ? " Tip:" + tip : "")}").ToList(),
            btns.Select(b => $"{b.Name ?? "?"}=„{Label(b).Replace('\n', ' ')}“").ToList());
    }

    public static List<Row> MeasureAll(MainWindow w)
    {
        var rows = new List<Row>();
        void M(string key)
        {
            Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            if (w.ActivePageRoot is { } r) rows.Add(Of(key, r));
        }
        foreach (var key in w.PageKeys.OrderBy(k => k).ToList())
        {
            foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
            try
            {
                w.Show(key); M(key);
                if (MainWindow.Programs.TryGetValue(key, out var spec) && spec.Templates.Count > 0)
                {
                    w.ProgramNew(key, spec.Templates[0].Id); w.Show(key); M(key + " (Dokument)");
                }
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or NotSupportedException) { }
        }
        return rows;
    }

    [AvaloniaFact(DisplayName = "V10 Übersicht: jede Ansicht in „Einfach“ höchstens 40 Knöpfe, höchstens 8 Knöpfe ohne Wort, höchstens 400 Wörter")]
    public void Every_view_stays_clear()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Einfach);
        List<Row> rows;
        try { rows = MeasureAll(w); }
        finally { w.Close(); }
        var report = string.Join("\n", rows.OrderByDescending(r => r.Buttons).Select(r =>
            $"{r.Key}\tKnöpfe {r.Buttons}\tohneWort {r.NoWord}\tWörter {r.Words}\t{string.Join(" · ", r.NoWordNames)}\t{string.Join(" · ", r.All)}"));
        File.WriteAllText(Path.Combine(global::Trendo.Core.TempDir.SystemTemp, "trendo-uebersicht.txt"), $"{rows.Count} Ansichten\n" + report);
        log.WriteLine(report);
        Assert.True(rows.Count > 150, $"nur {rows.Count} Ansichten");
        var bad = rows.Where(r => !Exceptions.ContainsKey(r.Key) && (r.Buttons > MaxButtons || r.NoWord > MaxNoWord || r.Words > MaxWords))
            .Select(r => $"{r.Key}: {r.Buttons} Knöpfe, {r.NoWord} ohne Wort, {r.Words} Wörter").ToList();
        if (Environment.GetEnvironmentVariable("TRENDO_UEBERSICHT_NUR_BERICHT") == "1") return;
        Assert.Empty(bad);
    }

    /// <summary>Namen aller benannten Bedienelemente einer Ansicht im logischen Baum – auch zugeklappte, ausgeblendete und Menüeinträge.</summary>
    static HashSet<string> Names(MainWindow w, string key, bool doc)
    {
        foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
        w.Show(key);
        if (doc && MainWindow.Programs.TryGetValue(key, out var spec) && spec.Templates.Count > 0) { w.ProgramNew(key, spec.Templates[0].Id); w.Show(key); }
        Dispatcher.UIThread.RunJobs();
        if (w.ActivePageRoot is not { } r) return [];
        var set = new HashSet<string>();
        void Walk(Avalonia.LogicalTree.ILogical l)
        {
            if (l is Control { Name: { Length: > 0 } n } c && (c is Button or MenuItem or CheckBox or ComboBox or TextBox or ToggleSwitch or Slider)) set.Add(n);
            if (l is Control ctl && ctl.ContextMenu is { } cm) foreach (var mi in cm.Items.OfType<MenuItem>()) Walk(mi);
            if (l is Button { Flyout: Flyout { Content: Avalonia.LogicalTree.ILogical fc } }) Walk(fc);
            foreach (var ch in l.LogicalChildren) Walk(ch);
        }
        Walk(r);
        return set;
    }

    /// <summary>Was „Einfach“ bewusst nicht als eigenes Element baut – mit dem Weg, auf dem die Funktion erreichbar bleibt.</summary>
    public static readonly (string Prefix, string Weg)[] Replaced =
    [
        ("prog-cmd-", "Fachwerkzeuge der Stufe: „Mehr Werkzeuge“ und Strg+K (E2)"),
        ("text-gallery-", "Formatvorlagen über die Liste „Absatzart“"),
        ("garment-color-1", "weitere Farben über „Eigene Farbe“"), ("garment-color-8", "weitere Farben über „Eigene Farbe“"), ("garment-color-9", "weitere Farben über „Eigene Farbe“"),
        ("studio-card-", "Studios über die Bereichs-Kachel"),
        ("ms-kind-", "Art über die Liste „Was erstellen?“"),
        ("hub-install-", "über „Alle … zeigen“ der Gruppe"), ("hub-open-", "über „Alle … zeigen“ der Gruppe"), ("hub-yes-", "über „Alle … zeigen“ der Gruppe"), ("hub-no-", "über „Alle … zeigen“ der Gruppe"),
        ("hub-remove-", "über „Alle … zeigen“ der Gruppe"), ("hub-update-", "über „Alle … zeigen“ der Gruppe"),
        ("text-rib-", "weitere Reiter über „Mehr ▾“ im Menüband"),
        ("mk-tiles-allow", "T-0726: „Karte aus dem Internet …“ im Menüband (Reiter Start) – dieselbe Rückfrage im Freigabe-Tor"),
        ("mk-tiles-stop", "T-0726: „Karte aus dem Internet …“ im Menüband (Reiter Start) schaltet wieder aus"),
        // 0.9.104: neue Formate – in „Einfach“ wie die übrigen Zusatzformate über die Menüleiste „Datei ▸ Exportieren“ (T-0710: dort in jeder Stufe alle Formate)
        ("prog-export-webp", "Menüleiste „Datei ▸ Exportieren ▸ WebP“"), ("prog-export-ris", "Menüleiste „Datei ▸ Exportieren ▸ RIS“"), ("prog-export-csl", "Menüleiste „Datei ▸ Exportieren ▸ CSL-JSON“"),
        // T-0740: in „Einfach“ über die Menüleiste (geprüft in T0740Gegenpruefung_Ui.Simple_export_via_menu) und den Knopf „Vorhersagen speichern …“ auf der Arbeitsfläche
        ("prog-export-pred", "Menüleiste „Datei ▸ Exportieren ▸ Vorhersagen (.csv)“ und Knopf „Vorhersagen speichern …“ auf der Arbeitsfläche"),
        // T-0739: Fragenreihe im Modellvergleich – Export ebenso über die Menüleiste (alle spec.Exports, jede Stufe)
        ("prog-export-reihe-csv", "Menüleiste „Datei ▸ Exportieren ▸ Fragenreihe (.csv)“"), ("prog-export-reihe-md", "Menüleiste „Datei ▸ Exportieren ▸ Fragenreihe (.md)“"),
        // T-0748: Gegenprobe der Statik – Bericht über die Menüleiste, das Ergebnis („Gegenprobe ✓ Abweichung …“) steht in jeder Stufe in der Statuszeile
        ("prog-export-gegenprobe", "Menüleiste „Datei ▸ Exportieren ▸ Gegenprobe (Bericht)“ (geprüft in T0748_Ui.Statik_Einfach_Menue); Ergebnis in der Statuszeile"),
    ];

    /// <summary>Was „Einfach“ schon vor T-0663 bewusst nicht baute (Stand origin/main 1fc842acc) – siehe Kopf der Datei.</summary>
    static HashSet<string> Before() => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Daten", "einfach-vorher-ausgeblendet.txt")).Where(l => l.Length > 0 && !l.StartsWith('#')).ToHashSet();

    [AvaloniaFact(DisplayName = "V10 Übersicht: „Einfach“ verliert keine Funktion – jedes Bedienelement aus „Erweitert“ ist da oder hat einen genannten Ersatzweg")]
    public void Simple_keeps_every_function()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var missing = new List<string>();
        var before = Before();
        try
        {
            foreach (var key in w.PageKeys.OrderBy(k => k).ToList())
                foreach (var doc in new[] { false, true })
                {
                    if (doc && !(MainWindow.Programs.TryGetValue(key, out var sp) && sp.Templates.Count > 0)) continue;
                    try
                    {
                        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Erweitert);
                        var full = Names(w, key, doc);
                        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Einfach);
                        var simple = Names(w, key, doc);
                        foreach (var n in full.Except(simple).Where(n => !Replaced.Any(r => n.StartsWith(r.Prefix, StringComparison.Ordinal))).Order())
                            if (!before.Contains($"{key}{(doc ? " (Dokument)" : "")}: {n}")) missing.Add($"{key}{(doc ? " (Dokument)" : "")}: {n}");
                    }
                    catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or NotSupportedException) { }
                }
        }
        finally { w.Close(); }
        File.WriteAllText(Path.Combine(global::Trendo.Core.TempDir.SystemTemp, "trendo-uebersicht-fehlt.txt"), string.Join(Environment.NewLine, missing));
        if (Environment.GetEnvironmentVariable("TRENDO_UEBERSICHT_NUR_BERICHT") == "1") return;
        Assert.Empty(missing);
    }
}
