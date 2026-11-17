using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Api;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0728 (offene Punkte Gruppe C „Bedienung“ aus den Paketberichten T-0703 … T-0723): Fachkürzel in der Menüleiste, Auftragszeile im Programm
/// „Code mit KI“, Claude-Auftrag als ein Rückgängig-Schritt, Tagesplaner (Wochenansicht, „Tag planen“ behält verschobene Blöcke), Bereichsname ohne VR,
/// Rechner-Ergebnisse in Gold, Modell-Knöpfe auf „Trendo KI (lokal)“ ohne Abschneiden. Bedienung über echte Tasten- und Mausereignisse des Headless-Fensters.</summary>
[Collection("Claude")]
public class T0728_Bedienung_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    static void Key_(MainWindow w, Key k, PhysicalKey p, RawInputModifiers m = RawInputModifiers.None) { w.KeyPress(k, m, p, null); Pump(); w.KeyRelease(k, m, p, null); Pump(); }
    static T V<T>(MainWindow w, string name) where T : Control { Pump(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? TryV<T>(MainWindow w, string name) where T : Control { Pump(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Pump(); RealClick(w, V<Button>(w, name)); Pump(); }
    static Menu Bar(MainWindow w) => w.ActiveMenuBar() ?? throw new Xunit.Sdk.XunitException("keine Menüleiste auf " + w.CurrentView);
    static List<MenuTop> Model(MainWindow w) => (List<MenuTop>)Bar(w).Tag!;
    static MenuCmd Cmd(MainWindow w, string id) => Model(w).SelectMany(m => ProgramMenu.Flat(m.Items)).First(c => c.Id == id);

    static ProgramDocument OpenDoc(MainWindow w, string key, string? template = null)
    {
        while (w.ProgramDocs(key).Count > 0) w.ProgramClose(key, 0);
        w.Show(key);
        var d = w.ProgramNew(key, template ?? MainWindow.Programs[key].Templates.FirstOrDefault()?.Id);
        w.Show(key); Pump(); w.UpdateLayout(); Pump();
        return d;
    }

    // ───────────── C1: Fachkürzel in der Menüleiste ─────────────

    [AvaloniaFact(DisplayName = "T-0728 C1: Videoschnitt S/Entf/Leertaste und Wiedergabe J K L I O M in der Menüleiste und als echte Taste (auch ohne Fokus auf der Zeitleiste), nie beim Tippen; Bildbearbeitung B wählt den Pinsel")]
    public void Fachkuerzel_in_der_Menueleiste()
    {
        var (w, _, root) = Ui.Open(); w.Width = 3000; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var d = OpenDoc(w, "schnitt");
            // Menü zeigt die Kürzel rechts – derselbe Eintrag wie der Menüband-Knopf
            Assert.Equal(("Teilen", "S"), (Cmd(w, "prog-cmd-ve-split").Label, Cmd(w, "prog-cmd-ve-split").Shortcut));
            Assert.Equal("Entf", Cmd(w, "prog-cmd-ve-delete").Shortcut);
            Assert.Equal("Leertaste", Cmd(w, "prog-cmd-ve-play").Shortcut);
            var wiedergabe = Model(w).Single(m => m.Title == "Wiedergabe");
            Assert.Equal(["J", "K", "L", "I", "O", "M", "Pos1", "Ende", "F"], wiedergabe.Items.Where(i => !i.IsSeparator).Select(i => i.Shortcut).ToArray());
            Assert.Empty(ProgramMenu.Conflicts(Model(w)));
            Assert.Contains(Bar(w).GetLogicalDescendants().OfType<TextBlock>(), t => t.Name == "menu-keys" && t.Text == "S");

            // Clip auf die Zeitleiste (Länge angegeben: kein FFmpeg nötig), Abspielkopf in die Mitte
            var clip = Path.Combine(root, "clip.mp4"); File.WriteAllBytes(clip, new byte[64]);
            w.EditAdd(clip, length: 4, audio: false); w.Show("schnitt"); Pump();
            d.Set("playhead", 2.0, "Abspielkopf"); w.Show("schnitt"); Pump();
            Assert.Single(Trendo.Core.Media.VideoEdit.Clips(d.Data));

            // Beim Tippen in ein Feld der Seite gehört S dem Feld – nichts wird geteilt
            var box = w.ActivePageRoot!.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible && t.IsEffectivelyEnabled && t.Bounds.Width > 0);
            box.Focus(); Pump();
            Key_(w, Key.S, PhysicalKey.S);
            Assert.Single(Trendo.Core.Media.VideoEdit.Clips(d.Data));

            // Fokus auf einem Knopf (nicht auf der Zeitleiste): S teilt trotzdem – wie im Menü angegeben
            var undo = V<Button>(w, "prog-undo"); undo.Focus(); Pump();
            Assert.False(TypingFocus(w));
            Key_(w, Key.S, PhysicalKey.S);
            Assert.Equal(2, Trendo.Core.Media.VideoEdit.Clips(d.Data).Count);
            Assert.Equal("Teilen", d.UndoLabel);

            // Wiedergabe › Marke setzen über die Menüleiste: Alt+Buchstabe, Pfeil runter, Enter
            var top = Bar(w).Items.OfType<MenuItem>().First(m => m.Name == "menubar-Wiedergabe");
            var letter = (char)top.Tag!;
            var key = (Key)Enum.Parse(typeof(Key), letter.ToString());
            w.KeyPress(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null); Pump();
            w.KeyPress(key, RawInputModifiers.Alt, PhysicalKey.None, null); Pump(); w.KeyRelease(key, RawInputModifiers.Alt, PhysicalKey.None, null); Pump();
            w.KeyRelease(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null); Pump();
            Assert.True(top.IsSubMenuOpen, "Wiedergabe ist nicht aufgeklappt");
            for (var i = 0; i < 12 && (w.FocusManager?.GetFocusedElement() as Control)?.Name != "menu-ve-key-m"; i++) Key_(w, Key.Down, PhysicalKey.ArrowDown);
            Assert.Equal("menu-ve-key-m", (w.FocusManager?.GetFocusedElement() as Control)?.Name);
            Key_(w, Key.Enter, PhysicalKey.Enter);
            Assert.Equal("Marke", d.UndoLabel);

            // Bildbearbeitung: B wählt den Pinsel, V wieder Verschieben – im Menü „Werkzeuge“ mit Kürzel
            OpenDoc(w, "bild");
            Assert.Equal("B", Cmd(w, "tool-img-tool-brush").Shortcut);
            Assert.Empty(ProgramMenu.Conflicts(Model(w)));
            V<Button>(w, "prog-undo").Focus(); Pump();
            Assert.Equal("move", w.ImgTool);
            Key_(w, Key.B, PhysicalKey.B);
            Assert.Equal("brush", w.ImgTool);
            Key_(w, Key.V, PhysicalKey.V); // die Seite ist neu gezeichnet, der Knopf hat den Fokus verloren – das Kürzel gilt trotzdem
            Assert.Equal("move", w.ImgTool);
        }
        finally { w.Close(); }
    }

    static bool TypingFocus(MainWindow w) => w.FocusManager?.GetFocusedElement() is TextBox;

    // ───────────── C2: Auftragszeile im Programm „Code mit KI“ ─────────────

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    [AvaloniaFact(DisplayName = "T-0728 C2: „Los“ in der Auftragszeile von „Code mit KI“ geht über „Mit KI ändern“ – Plan mit Dateiblöcken, Vorschau als Unterschied, nichts geändert vor dem Ja (KI ersetzt)")]
    public async Task Code_mit_ki_auftragszeile()
    {
        var (w, _, _) = Ui.Open();
        const string K = "code-ki-programm";
        var ki = new FixedKi(_ => "PLAN:\n- Funktion multipliziere ergänzen\n\nDATEI: rechner.py\n```python\ndef addiere(a, b):\n    return a + b\n\n\ndef multipliziere(a, b):\n    return a * b\n```\n");
        try
        {
            w.KiBackendsOverride = () => [ki];
            w.Show(K); Pump();
            w.Find<Button>("prog-tpl-beispiel").Press(); Pump();
            var d = w.ProgramActive(K)!; var root = d.Text("root");
            var before = File.ReadAllText(Path.Combine(root, "rechner.py"));
            w.Find<TextBox>("auftrag-prompt").Text = "Ergänze eine Funktion multipliziere(a, b)";
            RealClick(w, w.Find<Button>("auftrag-pruefen")); Pump();
            for (var i = 0; i < 3000 && w.CodeAiKiPhase is not ("vorschau" or "fehler"); i++) { Pump(); await Task.Delay(20); }
            Assert.Equal("vorschau", w.CodeAiKiPhase);
            Assert.Equal("Ergänze eine Funktion multipliziere(a, b)", d.Text("wish")); // der Wunsch steht im Programm
            Assert.Contains("DATEI:", ki.Systems[^1]); // Aufbau von „Mit KI ändern“, nicht die allgemeine Kette (JSON-Schritte)
            Assert.Contains(w.CodeAiKiPreview!.Files, f => f.Path == "rechner.py");
            Assert.Equal(before, File.ReadAllText(Path.Combine(root, "rechner.py"))); // Vorschau ändert nichts
            Assert.Null(w.AssistLast); // die allgemeine Kette lief nicht
        }
        finally { w.Close(); }
    }

    // ───────────── C3: Claude-Brücke – ein Auftrag, ein Rückgängig-Schritt ─────────────

    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Pump(); await Task.Delay(2); } Pump(); return await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();

    [AvaloniaFact(DisplayName = "T-0728 C3: Claude ändert über die Brücke in drei Zügen – am Ende des Laufs ist es EIN Rückgängig-Schritt „Claude-Auftrag“; Strg+Z nimmt alles zurück (Claude selbst ersetzt durch die Werkzeugaufrufe)")]
    public async Task Claude_auftrag_ein_schritt()
    {
        var (w, _, _) = Ui.Open();
        var folder = Path.Combine(Path.GetTempPath(), "trendo-t0728-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(folder);
        try
        {
            w.ClaudeNoClicks = true;
            var m = w.ClaudeBridgeForTest(folder);
            Assert.False((bool)(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = "technik", ["vorlage"] = "einfeld" }))["isError"]!);
            var doc = w.ProgramActive("technik")!; var before = doc.Data.ToJsonString(); var h = doc.History.Count;
            await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "claude/a", ["wert"] = "eins" });
            await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "claude/b", ["wert"] = "zwei" });
            await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "claude/c", ["wert"] = "drei" });
            Assert.Equal(h + 3, doc.History.Count); // während des Laufs: Claudes eigenes „rueckgaengig“ bleibt schrittweise
            w.ClaudeRunSquash(); // Ende des Laufs (derselbe Aufruf wie im Claude-Chat nach dem Lauf)
            Assert.Equal(h + 1, doc.History.Count);
            Assert.Equal(MainWindow.ClaudeUndoLabel, doc.UndoLabel);
            Assert.Equal("drei", (string)doc.Data["claude"]!["c"]!);
            // Strg+Z auf der Programmseite nimmt den ganzen Auftrag zurück
            w.Show("technik"); Pump();
            V<Button>(w, "prog-undo").Focus(); Pump();
            Key_(w, Key.Z, PhysicalKey.Z, RawInputModifiers.Control);
            Assert.Equal(before, doc.Data.ToJsonString());
            Assert.Equal(h, doc.History.Count);
        }
        finally { w.Close(); }
    }

    // ───────────── C4: Tagesplaner ─────────────

    const string DK = "tagesplaner-programm";
    static DayTimeline Tl(MainWindow w) => V<DayTimeline>(w, "dp-timeline");
    static Point TlWin(MainWindow w, double x, double y) { w.UpdateLayout(); Pump(); return Tl(w).TranslatePoint(new Point(x, y), w)!.Value; }
    static void Drag(MainWindow w, Point a, Point b)
    {
        w.MouseMove(a); w.MouseDown(a, MouseButton.Left); Pump();
        for (var i = 1; i <= 6; i++) { w.MouseMove(a + (b - a) * (i / 6.0)); Pump(); }
        w.MouseUp(b, MouseButton.Left); Pump();
    }
    static void Max(MainWindow w, string key) { if (w.Windows.Active is { Maximized: false } win) { w.Windows.ToggleMaximize(win.Id); w.Show(key); } Pump(); w.UpdateLayout(); Pump(); }

    [AvaloniaFact(DisplayName = "T-0728 C4: Tagesplaner – von Hand verschobener Block bleibt bei „Tag planen“ stehen (echte Maus), „neu verteilen“ per Rechtsklick; Wochenansicht zeigt die Tage, Klick auf einen Tag legt ihn an")]
    public void Tagesplaner_woche_und_festhalten()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        MainWindow.DayNowOverride = () => new DateTime(2026, 11, 16, 7, 0, 0); // Montag
        try
        {
            w.Show(DK); Pump(); RealClick(w, V<Button>(w, "prog-tpl-beispiel")); Max(w, DK);
            var d = w.ProgramActive(DK)!;
            Click(w, "dp-plan-now");
            var plan0 = d.List("plan").OfType<JsonObject>().First(p => (string?)p["kind"] == "aufgabe");
            var title = (string)plan0["title"]!; var s0 = (string)plan0["s"]!;
            // den ersten Aufgabenblock mit der Maus auf 16:00 ziehen
            var tl = Tl(w); var key = tl.Blocks.First(b => b.Kind == "aufgabe" && b.Title == title).Key; var r = tl.BlockRect(key);
            var startMin = tl.Blocks.First(b => b.Key == key).Start;
            Drag(w, TlWin(w, r.Center.X, r.Y + 8), TlWin(w, r.Center.X, r.Y + 8 + (16 * 60 - startMin) * tl.PxPerMin));
            var moved = d.List("plan").OfType<JsonObject>().Single(p => (string?)p["title"] == title && (string?)p["kind"] == "aufgabe");
            Assert.Equal("2026-11-16T16:00:00", (string?)moved["s"]); Assert.Equal("ja", (string?)moved["pin"]);
            Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.EndsWith("· festgehalten") == true);

            // „Tag planen“ (Menüband) – der verschobene Block bleibt, die anderen Aufgaben planen um ihn herum; nichts doppelt
            Click(w, "prog-cmd-dp-plan");
            var same = d.List("plan").OfType<JsonObject>().Where(p => (string?)p["title"] == title && (string?)p["kind"] == "aufgabe").ToList();
            Assert.Single(same); Assert.Equal("2026-11-16T16:00:00", (string?)same[0]["s"]);
            Assert.Contains("von Hand gesetzter Block bleibt", w.ProgramMessage(DK));
            Assert.Equal("Tag geplant", d.UndoLabel);
            var work = d.List("plan").OfType<JsonObject>().Where(p => (string?)p["kind"] == "aufgabe").Select(p => (S: DateTime.Parse((string)p["s"]!), E: DateTime.Parse((string)p["e"]!))).OrderBy(x => x.S).ToList();
            for (var i = 1; i < work.Count; i++) Assert.True(work[i].S >= work[i - 1].E, "Blöcke überlappen");

            // Rechtsklick → „Beim Planen neu verteilen“, dann „Tag planen“: zurück an die geplante Stelle
            tl = Tl(w); var k2 = tl.Blocks.First(b => b.Title == title && b.Kind == "aufgabe").Key;
            tl.GetVisualDescendants().OfType<Border>().First(x => x.Tag as string == k2).BringIntoView(); Pump(); w.UpdateLayout(); Pump();
            tl = Tl(w); var p2 = tl.BlockRect(k2).Center;
            var pw = tl.TranslatePoint(p2, w)!.Value; w.MouseMove(pw); w.MouseDown(pw, MouseButton.Right); w.MouseUp(pw, MouseButton.Right); Pump();
            var menu = tl.ContextMenu!; Assert.True(menu.IsOpen);
            menu.Items.OfType<MenuItem>().Single(i => i.Name == "dp-menu-loesen").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Pump(); menu.Close(); Pump();
            Click(w, "prog-cmd-dp-plan");
            Assert.Equal(s0, (string?)d.List("plan").OfType<JsonObject>().Single(p => (string?)p["title"] == title && (string?)p["kind"] == "aufgabe")["s"]);

            // Wochenansicht: sieben Tage, Montag ist der offene Plan
            w.ProgramSave(DK, Path.Combine(root, "Montag.tdoc"));
            Click(w, "dp-view-woche");
            Assert.True(w.DayWeek);
            for (var i = 0; i < 7; i++) Assert.NotNull(TryV<StackPanel>(w, "dp-week-day-" + i));
            Assert.Contains(V<StackPanel>(w, "dp-week-day-0").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == title);
            Assert.Contains("1 Tag mit Plan", V<TextBlock>(w, "dp-week-sum").Text);
            var golds = w.ActivePageRoot!.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && MainWindow.IsGold(b)).Select(b => b.Name).ToList();
            Assert.True(golds.Count == 1, "goldene Knöpfe: " + string.Join(", ", golds)); // eine goldene Hauptaktion
            // Mittwoch anklicken → neuer Tag mit Datum, Tagesansicht
            Click(w, "dp-week-open-2");
            var wed = w.ProgramActive(DK)!;
            Assert.NotSame(d, wed); Assert.Equal("2026-11-18", wed.Text("day")); Assert.False(w.DayWeek);
            Assert.NotNull(TryV<DayTimeline>(w, "dp-timeline"));
            // zurück zur Woche: Montag weiter mit Plan, Mittwoch markiert
            Click(w, "dp-view-woche");
            Assert.Contains(V<StackPanel>(w, "dp-week-day-0").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == title);
            Click(w, "dp-week-open-0");
            Assert.Same(d, w.ProgramActive(DK));
        }
        finally { MainWindow.DayNowOverride = null; w.Close(); }
    }

    // ───────────── C5–C7 ─────────────

    [AvaloniaFact(DisplayName = "T-0728 C5: Bereich heißt „3D & Spiele“ – kein „VR“ mehr in Bereichen und Fähigkeiten")]
    public void Bereich_ohne_vr()
    {
        Assert.Contains("3D & Spiele", StudioCatalog.Areas);
        Assert.DoesNotContain(StudioCatalog.Areas, c => c.Contains("VR", StringComparison.Ordinal));
        Assert.DoesNotContain(Trendo.Core.PromptAction.CapabilityCatalog.Profiles, c => c.Area.Contains("VR", StringComparison.Ordinal));
        Assert.Equal("3D & Spiele", StudioCatalog.ById["SPEC-04"].Area);
    }

    [AvaloniaFact(DisplayName = "T-0728 C6: Ergebnisse der Rechner (LiveCalc) in Gold, nicht blau")]
    public void Rechner_in_gold()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.Show("sendung"); Pump();
            var o = V<SelectableTextBlock>(w, "bc-rate-out");
            var c = Assert.IsAssignableFrom<ISolidColorBrush>(o.Foreground).Color;
            Assert.False(c.B > c.R && c.B > c.G, $"Ergebnis ist blau: {c}");
            Assert.True(c.R > c.B + 40 && c.G > c.B, $"Ergebnis ist nicht golden: {c}");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0728 C7: „Trendo KI (lokal)“ – lange Modell-Knöpfe brechen um und bleiben in der Spalte (nichts abgeschnitten)")]
    public void Modellknoepfe_nicht_abgeschnitten()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.Show("ki"); if (w.Windows.Active is { Maximized: false } win) { w.Windows.ToggleMaximize(win.Id); w.Show("ki"); } Pump(); w.UpdateLayout(); Pump();
            var b = V<Button>(w, "btn-model-model-7b");
            var tb = Assert.IsType<TextBlock>(b.Content);
            Assert.Equal(TextWrapping.Wrap, tb.TextWrapping);
            Assert.Contains("besseres Deutsch", tb.Text);
            Assert.True(tb.TextLayout.TextLines.Count >= 2, "lange Beschriftung bricht nicht um");
            Assert.True(tb.Bounds.Width <= b.Bounds.Width, $"Text {tb.Bounds.Width:0} breiter als der Knopf {b.Bounds.Width:0}");
            var col = b.GetVisualParent<Control>()!;
            Assert.True(b.Bounds.Right <= col.Bounds.Width + 0.5, "Knopf ragt aus der Spalte");
        }
        finally { w.Close(); }
    }
}
