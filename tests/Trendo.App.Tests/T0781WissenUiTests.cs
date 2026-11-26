using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0781 (Etappe 0.9.113, Wissen & Lernen): „Mathe-Helfer“ (STUDIO-MATH-01) und „Naturwissenschaft“ (STUDIO-SCIENCE-01) als Programme der Hülle.
/// Vom Klick bis zur Datei: Vorlage → Eingabe → „Rechnen“/„Ins Laborheft“ → Ergebnis, Lösungsweg, Probe → Export (PDF, LaTeX, Markdown, CSV).
/// KI-Fachaktion „Textaufgabe → Rechnung“ mit Vorschau, „Ja, behalten“ und genau einem Rückgängig-Schritt; das Ergebnis rechnet immer der eigene Kern.
/// Die KI ist in den ersten Tests ersetzt (feste Antwort); <see cref="Real_local_ki"/> läuft mit dem Ollama-Modell dieses PCs (nur mit TRENDO_T0781_ECHT=&lt;Belegordner&gt;).
[Collection("Claude")]
public partial class T0781Wissen_Ui
{
    const string M = MainWindow.MatheKey, N = MainWindow.NaturKey;

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static string Plan(string action, JsonObject args) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = action, ["args"] = args }) }.ToJsonString();
    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();
    static void Pump() { for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs(); }
}

static class T0781Hilfe
{
    public static List<string> Texte(this Control c) => [.. c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];
}

public partial class T0781Wissen_Ui
{

    static ProgramDocument OpenMathe(MainWindow w, string tpl = "leer", int level = UserLevel.Erweitert)
    {
        w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(level);
        w.Show(M); Settle(); RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(M)!;
    }

    static ProgramDocument OpenNatur(MainWindow w, string tpl = "leer", int level = UserLevel.Erweitert)
    {
        w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(level);
        w.Show(N); Settle(); RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(N)!;
    }

    static void Tippe(MainWindow w, string name, string text) { var t = w.Find<TextBox>(name); t.Text = text; Settle(); }

    static async Task<string> Ask(MainWindow w, string key, string prompt, int ms = 10000)
    {
        var audits = w.Core.Audit.ReadAll().Count;
        bool NoPlan() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.ki.ohneplan" or "assist.ki.fehler" && a.Detail.StartsWith(key));
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        Tap(w, "auftrag-pruefen");
        for (var i = 0; i < ms / 20 && !(NoPlan() || w.AssistLastKey == key && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen"); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.True(NoPlan() || w.AssistLastKey == key && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen", $"{key}: Phase {w.AssistLast?.Phase} ({w.AssistLastKey}) – {w.AssistLast?.Message} | {string.Join(" | ", w.AssistLog)} | KI: {w.AssistLastKiAnswer}");
        Assert.False(NoPlan(), $"{key}: kein Plan aus der KI-Antwort: {w.AssistLastKiAnswer}");
        return w.AssistLast!.Value.Phase;
    }

    static string Exportiere(MainWindow w, string key, string name)
    {
        RealClick(w, w.Find<Button>("prog-export-" + name)); Settle();
        Assert.Contains("Exportiert:", w.ProgramMessage(key));
        var file = w.ProgramMessage(key)["Exportiert: ".Length..].Trim();
        Assert.True(File.Exists(file) && new FileInfo(file).Length > 100, name + ": " + file);
        return file;
    }

    // =============================== Mathe-Helfer ===============================

    [AvaloniaFact(DisplayName = "T-0781 Mathe-Helfer: leeres Rechenblatt → Aufgabe tippen → „Rechnen“ → Ergebnis, Lösungsweg, Probe und Graph; Rückgängig; Export PDF, LaTeX, Markdown")]
    public void Mathe_klick_bis_datei()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = OpenMathe(w);
            Assert.NotNull(w.Find<StackPanel>("mh-workspace")); Assert.NotNull(w.TryFind<Border>("mh-leer")); // erster Schritt im Leerzustand sichtbar
            Tippe(w, "mh-eingabe", "x^2 - 5x + 6 = 0");
            Tap(w, "prog-cmd-mh-run-rechnen");
            Assert.Single(d.List("rechnungen"));
            Assert.Equal("x₁ = 2;  x₂ = 3", w.Find<TextBlock>("mh-ergebnis").Text);
            var weg = w.Find<StackPanel>("mh-weg");
            Assert.Contains(weg.Texte(), t => t.Contains("p-q-Formel"));
            Assert.All(w.Find<StackPanel>("mh-probe").Texte(), t => Assert.StartsWith("✓", t));
            var g = w.Find<GraphView>("mh-graph"); Assert.Equal(2, g.Series.Count); Assert.Equal("#D4A017", g.Farben[0]); // Graph in Gold, nicht Blau

            // zweite Aufgabe: Art wählen, Ableiten
            Tap(w, "mh-art-ableiten"); Assert.Equal("ableiten", d.Text("art"));
            Tippe(w, "mh-eingabe", "x^2 * sin(x)");
            Tap(w, "prog-cmd-mh-run-rechnen");
            Assert.Equal(2, d.List("rechnungen").Count);
            Assert.StartsWith("f'(x) = ", w.Find<TextBlock>("mh-ergebnis").Text);
            Assert.Contains(w.Find<StackPanel>("mh-weg").Texte(), t => t.Contains("Produktregel"));

            // Liste: erste Rechnung wählen; Rückgängig nimmt die zweite wieder heraus
            Tap(w, "mh-zeile-0"); Assert.Equal(0, w.MhAuswahl(d));
            Tap(w, "prog-undo"); Assert.Single(d.List("rechnungen"));
            Tap(w, "prog-redo"); Assert.Equal(2, d.List("rechnungen").Count);

            // Exporte
            var pdf = Exportiere(w, M, "pdf");
            using (var p = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.True(p.PageCount >= 1);
            var txt = PdfView.Text(pdf, 0);
            Assert.Contains("p-q-Formel", txt); Assert.Contains("Produktregel", txt);
            var tex = File.ReadAllText(Exportiere(w, M, "tex"));
            Assert.Contains("\\documentclass", tex); Assert.Contains("x_{1} = 2", tex); Assert.Contains("\\end{document}", tex);
            var md = File.ReadAllText(Exportiere(w, M, "md"));
            Assert.Contains("**Lösungsweg**", md); Assert.Contains("✓", md);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 Mathe-Helfer: Fehler verständlich, nichts wird angelegt; Entf und Rechtsklick „Kopieren“ wirken")]
    public void Mathe_fehler_und_bedienung()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = OpenMathe(w, "gleichungen");
            var n = d.List("rechnungen").Count;
            Tippe(w, "mh-eingabe", "(x + 1");
            Tap(w, "prog-cmd-mh-run-rechnen");
            Assert.Equal(n, d.List("rechnungen").Count);
            Assert.Contains("Klammer", w.ProgramMessage(M));
            // Rechtsklick-Menü: Kopieren
            var item = w.Find<Button>("mh-zeile-0").ContextMenu!.Items.OfType<MenuItem>().First(m => m.Name == "mh-ctx-kopie-0");
            item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Equal(n + 1, d.List("rechnungen").Count);
            // Entf auf der Liste löscht die gewählte Rechnung
            var liste = w.Find<StackPanel>("mh-liste");
            liste.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Delete }); Settle();
            Assert.Equal(n, d.List("rechnungen").Count);
            // Enter in der Eingabezeile rechnet
            Tippe(w, "mh-eingabe", "3/4 + 5/6");
            w.Find<TextBox>("mh-eingabe").RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
            Assert.Equal(n + 1, d.List("rechnungen").Count);
            Assert.StartsWith("19/12", w.Find<TextBlock>("mh-ergebnis").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 Mathe-Helfer: Aufgabenblatt aus einer Fremddatei (Text) über „Aufgabenblatt öffnen …“ – jede Zeile gerechnet, Unverständliches übergangen, Datei unverändert")]
    public void Mathe_import()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var d = OpenMathe(w);
            var f = Path.Combine(root, "aufgabenblatt.txt");
            File.WriteAllText(f, "# Hausaufgabe\n1. 2x + 4 = 10\n2) gleichung: x^2 = 9\n- bruch: 1/2 + 1/3\nSeite 3 ???\nableiten: x^3\n", new UTF8Encoding(false));
            var vorher = Hash(f);
            w.PickOverride = _ => Task.FromResult<string[]?>([f]);
            Tap(w, "prog-cmd-mh-import");
            Assert.Equal(4, d.List("rechnungen").Count);
            Assert.Contains("1 Zeile(n) übergangen", w.ProgramMessage(M));
            Assert.Equal("bruch", d.List("rechnungen")[2]!["art"]!.GetValue<string>());
            Assert.Equal(vorher, Hash(f));
            Assert.Equal("aufgabenblatt", d.Text("title"));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 Mathe-Helfer: KI übersetzt eine Textaufgabe – Vorschau ändert nichts, „Ja, behalten“ legt die vom Kern gerechnete Rechnung an (ein Rückgängig-Schritt), Rückgängig nimmt sie heraus (KI ersetzt)")]
    public async Task Mathe_ki_textaufgabe()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var ki = new FixedKi(_ => Plan("mathe.rechnung", new JsonObject { ["art"] = "gleichung", ["eingabe"] = "x*(x+3) = 40" }));
        try
        {
            w.KiBackendsOverride = () => [ki];
            var d = OpenMathe(w);
            var vorher = d.Data.ToJsonString(); var hist = d.History.Count;
            Assert.Equal(AssistRoute.Dokument, w.AssistRouteFor(M));
            Assert.Equal("vorschau", await Ask(w, M, "Ein Rechteck ist 3 cm länger als breit und hat eine Fläche von 40 cm². Wie breit ist es?"));
            Assert.Contains("mathe.rechnung", ki.Systems[^1]); Assert.Contains("nie das Ergebnis", ki.Systems[^1]);
            Assert.Equal(vorher, d.Data.ToJsonString()); Assert.Equal(hist, d.History.Count);
            Assert.Contains("Gerechnet hat der Rechenkern", w.AssistLast!.Value.Message);
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            Assert.Single(d.List("rechnungen"));
            Assert.Equal(hist + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            var (l, _) = w.MhLoesung(d, 0);
            Assert.Equal("x₁ = −8;  x₂ = 5", l!.Ergebnis); Assert.True(l.ProbeOk);
            Assert.Equal("KI: Textaufgabe", d.List("rechnungen")[0]!["quelle"]!.GetValue<string>());
            Tap(w, "assist-undo");
            Assert.Equal(vorher, w.ProgramActive(M)!.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 Mathe-Helfer: die KI darf kein Ergebnis liefern – „x = 5“ und direkt gesetzte Rechnungen werden abgelehnt, das Dokument bleibt unverändert (KI ersetzt)")]
    public async Task Mathe_ki_rechnet_nie()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var antwort = Plan("mathe.rechnung", new JsonObject { ["art"] = "gleichung", ["eingabe"] = "x = 5" });
        var ki = new FixedKi(_ => antwort);
        try
        {
            w.KiBackendsOverride = () => [ki];
            var d = OpenMathe(w);
            var vorher = d.Data.ToJsonString();
            Assert.Equal("fehler", await Ask(w, M, "Wie breit ist das Rechteck?"));
            Assert.Contains("Ergebnis eingesetzt", w.AssistLast!.Value.Message);
            Assert.Equal(vorher, d.Data.ToJsonString());
            // „dokument.setzen“ auf die Rechnungen ist gesperrt (KiLocked) – die KI kann nichts an der Rechenkette vorbei eintragen
            var t = Assert.IsType<ProgramAssistTarget>(w.AssistTargetFor(M));
            var sitzung = t.Begin();
            var ex = Assert.Throws<InvalidOperationException>(() => sitzung.Run(new AssistStep(new AssistAction(AssistChain.EditAction, "Inhalt setzen", []), new() { ["pfad"] = "rechnungen", ["wert"] = "[{\"art\":\"gleichung\",\"eingabe\":\"x = 5\"}]" }, [])));
            Assert.Contains("legst du selbst fest", ex.Message);
            Assert.Equal(vorher, d.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 Mathe-Helfer: Absturz vor dem Speichern – die automatische Sicherung (20-s-Takt der Hülle) bringt die Rechnungen zurück")]
    public void Mathe_absturz()
    {
        var (w, _, root) = Ui.Open();
        var d = OpenMathe(w);
        Tippe(w, "mh-eingabe", "2x + 3y = 12; x - y = 1"); Tap(w, "prog-cmd-mh-run-rechnen");
        Tippe(w, "mh-eingabe", "(x+3)^2"); Tap(w, "prog-cmd-mh-run-rechnen");
        Assert.Equal(2, d.List("rechnungen").Count);
        w.ProgramAutosaveAll(); // der feste Takt (alle 20 s)
        w.ProgLocks.Abandon(); // „Absturz“: kein Schließen, kein Speichern
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w2 = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        try
        {
            w2.ProgramSetLevel(UserLevel.Erweitert); w2.Show(); Pump();
            w2.Show(M); Pump();
            w2.Find<Button>("prog-restore-0").Press(); Pump();
            var d2 = w2.ProgramActive(M)!;
            Assert.Equal(2, d2.List("rechnungen").Count);
            Assert.Equal("x = 3;  y = 2", w2.MhLoesung(d2, 0).L!.Ergebnis);
        }
        finally { w2.Close(); }
    }

    // =============================== Naturwissenschaft ===============================

    [AvaloniaFact(DisplayName = "T-0781 Naturwissenschaft: Periodensystem anklicken → Steckbrief; Reaktion, Molmasse, Physik und Einheiten → „Ins Laborheft“; Export PDF und CSV")]
    public void Natur_klick_bis_datei()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = OpenNatur(w);
            Tap(w, "nw-el-Cu");
            Assert.Equal("Cu", w.NwElement(d).Symbol);
            Assert.Equal("Kupfer", w.Find<TextBlock>("nw-el-name").Text);
            Tap(w, "prog-cmd-nw-run-laborheft");
            Assert.Single(d.List("eintraege"));

            Tap(w, "nw-tab-reaktion");
            Tippe(w, "nw-rx", "Fe + O2 -> Fe2O3");
            Assert.Equal("4 Fe + 3 O₂ → 2 Fe₂O₃", w.Find<TextBlock>("nw-rx-live-wert").Text); // live gerechnet
            Tap(w, "prog-cmd-nw-run-laborheft");
            Assert.Equal(2, d.List("eintraege").Count);

            Tap(w, "nw-tab-molmasse");
            Tippe(w, "nw-mol", "C6H12O6");
            Assert.Equal("M = 180,156 g/mol", w.Find<TextBlock>("nw-mol-live-wert").Text);
            Tap(w, "prog-cmd-nw-run-laborheft");

            Tap(w, "nw-tab-physik");
            var formel = w.Find<ComboBox>("nw-ph-formel");
            formel.SelectedIndex = Trendo.Core.Wissen.Formelsammlung.Alle.ToList().FindIndex(f => f.Id == "geschwindigkeit"); Settle();
            Assert.Equal("geschwindigkeit", d.Text("phFormel"));
            Tippe(w, "nw-ph-werte", "s = 42,195 km; t = 2,5 h");
            Tippe(w, "nw-ph-ziel", "km/h");
            Assert.Equal("v = 16,878 km/h", w.Find<TextBlock>("nw-ph-live-wert").Text);
            Tap(w, "prog-cmd-nw-run-laborheft");

            Tap(w, "nw-tab-einheiten");
            Tippe(w, "nw-ein", "3 m + 2 s");
            Assert.Contains("Einheiten passen nicht", string.Join(" ", w.Find<ContentControl>("nw-ein-ergebnis").Texte()));
            Tippe(w, "nw-ein", "12 V · 0,5 A");
            Tap(w, "prog-cmd-nw-run-laborheft");
            Assert.Equal(5, d.List("eintraege").Count);
            Assert.All(d.List("eintraege"), e => Assert.Null(MainWindow.NwRechne(e).Fehler));

            var csv = File.ReadAllText(Exportiere(w, N, "csv"));
            Assert.Contains("4 Fe + 3 O₂ → 2 Fe₂O₃", csv); Assert.Contains("v = 16,878 km/h", csv); Assert.DoesNotContain("NICHT", csv);
            var pdf = Exportiere(w, N, "pdf");
            Assert.Contains("Fe", PdfView.Text(pdf, 0));
            var el = File.ReadAllText(Exportiere(w, N, "elemente"));
            Assert.Contains("29;Cu;Kupfer;63,546;", el);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 Naturwissenschaft: KI übersetzt „Wie lange fällt ein Stein aus 20 m?“ in die Formel – gerechnet vom Programm, ein Rückgängig-Schritt; Faktoren der KI werden abgelehnt (KI ersetzt)")]
    public async Task Natur_ki_textaufgabe()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var antwort = Plan("natur.rechnung", new JsonObject { ["art"] = "physik", ["formel"] = "freier-fall", ["gesucht"] = "t", ["eingabe"] = "h = 20 m" });
        var ki = new FixedKi(_ => antwort);
        try
        {
            w.KiBackendsOverride = () => [ki];
            var d = OpenNatur(w, "leer");
            var vorher = d.Data.ToJsonString(); var hist = d.History.Count;
            Assert.Equal("vorschau", await Ask(w, N, "Wie lange fällt ein Stein aus 20 m Höhe?"));
            Assert.Equal(vorher, d.Data.ToJsonString());
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var e = Assert.Single(d.List("eintraege"));
            Assert.Equal("t = 2,01962 s", MainWindow.NwRechne(e).Ergebnis);
            Assert.Equal(hist + 1, d.History.Count);
            Tap(w, "assist-undo");
            Assert.Equal(vorher, w.ProgramActive(N)!.Data.ToJsonString());
            antwort = Plan("natur.rechnung", new JsonObject { ["art"] = "reaktion", ["eingabe"] = "4 Fe + 3 O2 -> 2 Fe2O3" });
            Assert.Equal("fehler", await Ask(w, N, "Gleiche Eisen und Sauerstoff zu Eisenoxid aus"));
            Assert.Contains("Faktoren", w.AssistLast!.Value.Message);
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 Studios eingebunden: Katalog (Wissen & Lernen), Symbol, Profil, Team, Fähigkeiten, KI-Fachaktion, Bild")]
    public void Eingebunden()
    {
        foreach (var (id, key) in new[] { (MainWindow.MatheStudio, M), (MainWindow.NaturStudio, N) })
        {
            Assert.Equal("Wissen & Lernen", StudioCatalog.For(id)!.Area);
            Assert.True(StudioIcons.ById.ContainsKey(id));
            Assert.True(StudioProfiles.Has(id));
            Assert.NotNull(StudioTeams.For(id));
            Assert.Equal("Wissen & Lernen", Trendo.Core.PromptAction.CapabilityCatalog.Area(id));
            Assert.Equal(Trendo.Core.PromptAction.CapabilityLevel.Partial, Trendo.Core.PromptAction.CapabilityCatalog.Level(id, Trendo.Core.PromptAction.PromptActions.CreateDefault()));
            Assert.Single(MainWindow.Programs[key].KiActions);
            // Echtlauf 3 (26.11.2026): die KI nahm „Ins Laborheft“ – das rechnet die Eingabe der Ansicht, nicht die der KI. Solche Befehle sind für die KI tabu.
            var ziel = new ProgramAssistTarget(MainWindow.Programs[key], new ProgramDocument(id, "t", []));
            Assert.Equal(key == M ? "mathe.rechnung" : "natur.rechnung", ziel.Actions[0].Id);
            Assert.DoesNotContain(ziel.Actions, a => a.Id is "mh-run-rechnen" or "nw-run-laborheft" or "mh-import");
            Assert.Equal(id, MainWindow.Programs[key].Studio);
        }
    }

    // =============================== Fremddatei rein, Export raus ===============================

    [AvaloniaFact(DisplayName = "T-0781 FREMD: Aufgabenblatt aus LibreOffice (Text) über die Oberfläche einlesen, rechnen, als PDF/Markdown exportieren; Laborheft als CSV/PDF – Dateien für die Gegenprobe im Fremdprogramm (nur mit TRENDO_T0781_FREMD)")]
    public void Fremddatei_ende_zu_ende()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_T0781_FREMD") is not { Length: > 0 } dir || !File.Exists(Path.Combine(dir, "aufgaben-libreoffice.txt"))) { Assert.Skip("Nur mit TRENDO_T0781_FREMD=<Ordner mit aufgaben-libreoffice.txt aus LibreOffice>."); return; }
        var (w, _, _) = Ui.Open();
        try
        {
            var f = Path.Combine(dir, "aufgaben-libreoffice.txt");
            var d = OpenMathe(w);
            w.PickOverride = _ => Task.FromResult<string[]?>([f]);
            Tap(w, "prog-cmd-mh-import");
            var n = d.List("rechnungen").Count;
            Assert.True(n >= 4, w.ProgramMessage(M));
            Assert.All(Enumerable.Range(0, n), i => Assert.True(w.MhLoesung(d, i).L?.ProbeOk ?? false, $"Rechnung {i}"));
            Tippe(w, "mh-eingabe", "x^2 * e^x"); Tap(w, "mh-art-ableiten"); Tippe(w, "mh-eingabe", "x^2 * e^x"); Tap(w, "prog-cmd-mh-run-rechnen"); // bearbeitet: eine Rechnung dazu
            foreach (var (x, name) in new[] { ("pdf", "mathe-loesungswege.pdf"), ("md", "mathe-loesungswege.md"), ("tex", "mathe-loesungswege.tex") }) File.Copy(Exportiere(w, M, x), Path.Combine(dir, name), true);
            var nd = OpenNatur(w, "chemie");
            Tap(w, "prog-cmd-nw-run-laborheft");
            foreach (var (x, name) in new[] { ("pdf", "laborheft.pdf"), ("csv", "laborheft.csv"), ("elemente", "periodensystem.csv") }) File.Copy(Exportiere(w, N, x), Path.Combine(dir, name), true);
            File.WriteAllText(Path.Combine(dir, "ui-lauf.txt"), $"Mathe-Helfer: {n} Aufgaben aus aufgaben-libreoffice.txt eingelesen, alle Proben bestanden, +1 Ableitung; Export PDF/MD/TEX.\nNaturwissenschaft: Vorlage Chemie + 1 Eintrag, {nd.List("eintraege").Count} Einträge; Export PDF/CSV, Periodensystem-CSV.\n");
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    // =============================== Echtlauf mit lokaler KI ===============================

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0781_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0781 ECHT: lokale KI (Ollama) übersetzt Textaufgaben – Mathe-Helfer (Rechteck) und Naturwissenschaft (Glucose), gerechnet vom Kern, Rückgängig (nur mit TRENDO_T0781_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) { Assert.Skip("Echtlauf nur mit TRENDO_T0781_ECHT=<Belegordner> (braucht Ollama mit Modell auf diesem PC)."); return; }
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var log = new StringBuilder();
        void L(string s) => log.AppendLine(s);
        var seen = 0;
        void KiAudit() { var all = w.Core.Audit.ReadAll(); foreach (var a in all.Skip(seen).Where(a => a.Action.StartsWith("ki.", StringComparison.Ordinal) || a.Action.StartsWith("assist.", StringComparison.Ordinal))) L($"  - Protokoll: {a.Action} {a.Detail}"); seen = all.Count; }
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w);
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source);
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            var choice = w.KiModelChoice;
            L("# T-0781 ECHT: Textaufgabe → Rechnung im offenen Dokument (Mathe-Helfer, Naturwissenschaft)"); L("");
            L($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            L("Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt. Gerechnet hat jeweils der eigene Kern."); L("");

            // Mathe-Helfer
            var d = OpenMathe(w); seen = w.Core.Audit.ReadAll().Count;
            var vorher = d.Data.ToJsonString(); var hist = d.History.Count;
            const string p1 = "Ein Rechteck ist 3 cm länger als breit und hat eine Fläche von 40 cm². Wie breit ist das Rechteck?";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ph1 = await Ask(w, M, p1, 600_000); sw.Stop();
            L("## Mathe-Helfer (mathe-helfer-programm)"); L("");
            L($"- Auftrag: {p1}");
            L($"- Phase: {ph1} – {w.AssistLast?.Message}");
            L($"- Plan ({w.Find<TextBlock>("assist-source").Text?.Trim()}, {sw.Elapsed.TotalSeconds:F0} s): {string.Join(" → ", w.AssistLast!.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            Assert.Equal("vorschau", ph1);
            Assert.Equal(vorher, d.Data.ToJsonString());
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var (l, err) = w.MhLoesung(d, 0);
            Assert.Null(err);
            L($"- Nach „Ja, behalten“: Rechnung „{d.List("rechnungen")[0]!["eingabe"]}“ ({d.List("rechnungen")[0]!["art"]}) → Kern: {l!.Ergebnis}; Probe: {string.Join(" | ", l.Proben.Select(p => p.Text))}");
            L($"- Rückgängig-Liste: {hist} → {d.History.Count}, oben „{d.UndoLabel}“");
            Assert.True(l.ProbeOk); Assert.Contains("5", l.Ergebnis); Assert.Equal(hist + 1, d.History.Count);
            Tap(w, "assist-undo");
            L($"- Rückgängig: Dokument wie vorher = {vorher == w.ProgramActive(M)!.Data.ToJsonString()}");
            Assert.Equal(vorher, w.ProgramActive(M)!.Data.ToJsonString());
            L("");

            // Naturwissenschaft
            var n = OpenNatur(w); seen = w.Core.Audit.ReadAll().Count;
            var nv = n.Data.ToJsonString();
            const string p2 = "Wie groß ist die molare Masse von Traubenzucker (Glucose, Summenformel C6H12O6)?";
            sw.Restart();
            var ph2 = await Ask(w, N, p2, 600_000); sw.Stop();
            L("## Naturwissenschaft (naturwissenschaft-programm)"); L("");
            L($"- Auftrag: {p2}");
            L($"- Phase: {ph2} – {w.AssistLast?.Message}");
            L($"- Plan ({w.Find<TextBlock>("assist-source").Text?.Trim()}, {sw.Elapsed.TotalSeconds:F0} s): {string.Join(" → ", w.AssistLast!.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            Assert.Equal("vorschau", ph2);
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var b = MainWindow.NwRechne(n.List("eintraege")[0]);
            L($"- Nach „Ja, behalten“: Eintrag {n.List("eintraege")[0]!.ToJsonString()} → Programm: {b.Ergebnis}; Probe: {string.Join(" | ", b.Proben.Select(p => p.Text))}");
            Assert.Equal("M = 180,156 g/mol", b.Ergebnis);
            Tap(w, "assist-undo");
            L($"- Rückgängig: Laborheft wie vorher = {nv == w.ProgramActive(N)!.Data.ToJsonString()}");
            Assert.Equal(nv, w.ProgramActive(N)!.Data.ToJsonString());
            L(""); L("Ergebnis: beide Programme bestanden.");
        }
        catch (Exception e) { L(""); L("FEHLER: " + e.Message); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-lokale-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"), new UTF8Encoding(false));
            MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.VramOverride = null; w.InstalledModelsOverride = null; w.Close();
        }
    }
}
