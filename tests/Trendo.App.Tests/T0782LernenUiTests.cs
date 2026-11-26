using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Lernen;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0782: „Nachhilfe mit KI“ und „Programmieren lernen“ – vom Klick bis zur Datei, KI-Fachaktion mit Vorschau/„Ja, behalten“/einem Rückgängig-Schritt,
/// nur lokale KI in der Nachhilfe, Kinderprofil, Ablegen und Rechtsklick. Die Tests von „Programmieren lernen“ laufen ECHT im AppContainer (mitgeliefertes Python dieses PCs);
/// die KI ist in diesen Tests ersetzt (feste Antwort) – der Echtlauf mit Ollama steht in <see cref="Real_local_ki"/> (nur mit TRENDO_T0782_ECHT).</summary>
[Collection("Claude")]
public class T0782Lernen_Ui
{
    const string Tu = MainWindow.TutorKey, Cl = MainWindow.CodeLearnKey;

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = []; public int Calls;
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }
    /// <summary>Steht für Claude über das Abo: darf in der Nachhilfe nie gefragt werden.</summary>
    sealed class SpyMain : IMainKiBackend
    {
        public int Calls;
        public string Name => "Claude (Ersatz)"; public string BaseUrl => "claude";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["claude"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; return Task.FromResult("{\"steps\":[]}"); }
    }

    static string Plan(string action, JsonObject args) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = action, ["args"] = args }) }.ToJsonString();
    static async Task Until(Func<bool> ok, int ms = 15000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();

    static async Task Ask(MainWindow w, string key, string prompt)
    {
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        Tap(w, "auftrag-pruefen");
        await Until(() => w.AssistLastKey == key && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen");
        Assert.True(w.AssistLast!.Value.Phase == "vorschau", $"{w.AssistLast.Value.Phase} – {w.AssistLast.Value.Message} | {string.Join(" | ", w.AssistLog)} | KI: {w.AssistLastKiAnswer}");
    }

    static ProgramDocument OpenTutor(MainWindow w, string tpl = "leer")
    {
        MainWindow.TutorSeed = 7;
        return BueroAusbau_Ui.StartEmpty(w, Tu, tpl);
    }

    [AvaloniaFact(DisplayName = "T-0782 Nachhilfe Klick bis Datei: leere Vorlage → erster Schritt → falsch → Hinweis → Lösungsweg erst nach Versuch → richtig → Fortschritt; Arbeitsblatt als ODT und DOCX")]
    public void Tutor_click_to_file()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = OpenTutor(w);
            BueroAusbau_Ui.Click(w, "prog-first-step");
            var u = Nachhilfe.Uebung(d)!; Assert.NotNull(u);
            Assert.False(V<Button>(w, "nh-loesung").IsEnabled); // sokratisch: vor dem ersten Versuch gesperrt
            BueroAusbau_Ui.TypeInto(w, "nh-eingabe", "-12345");
            Tap(w, "nh-pruefen");
            Assert.Contains("Noch nicht richtig", V<SelectableTextBlock>(w, "nh-rueckmeldung").Text);
            Assert.True(V<Button>(w, "nh-loesung").IsEnabled);
            Tap(w, "nh-hinweis");
            Assert.StartsWith("💡 Hinweis 1", V<TextBlock>(w, "nh-hinweis-0").Text);
            w.ProgramSetLevel(UserLevel.Profi); Settle();
            BueroAusbau_Ui.Cmd(w, "prog-cmd-nh-next");
            var soll = Nachhilfe.Uebung(d)!["antworten"]![0]!.GetValue<string>();
            BueroAusbau_Ui.TypeInto(w, "nh-eingabe", soll);
            var box = V<TextBox>(w, "nh-eingabe"); box.Focus(); Settle();
            w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle(); // Eingabetaste prüft
            Assert.StartsWith("✓", V<SelectableTextBlock>(w, "nh-rueckmeldung").Text);
            var st = Nachhilfe.Stand(d, Nachhilfe.AktuellesThema(d)!); Assert.Equal(1, st.Richtig);
            Assert.True(d.CanUndo);
            BueroAusbau_Ui.Cmd(w, "prog-export-odt");
            var odt = BueroAusbau_Ui.Exported(w, Tu);
            var text = Trendo.Core.Office.OdtFormat.Read(odt).PlainText();
            Assert.Contains("Arbeitsblatt: Kleines Einmaleins", text); Assert.Contains("Lösungen", text);
            BueroAusbau_Ui.Cmd(w, "prog-export-docx");
            Assert.Contains("Kleines Einmaleins", Trendo.Core.Office.DocxFormat.Read(BueroAusbau_Ui.Exported(w, Tu)).PlainText());
        }
        finally { MainWindow.TutorSeed = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0782 Nachhilfe KI-Fachaktion: „Gib mir eine Übung …“ → Plan uebung.erzeugen → Vorschau ändert nichts → Ja → Übung im Dokument, EIN Rückgängig-Schritt; nur die KI auf diesem PC (Claude nie gefragt)")]
    public async Task Tutor_ki_exercise_local_only()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var ki = new FixedKi(_ => Plan("uebung.erzeugen", new JsonObject { ["frage"] = "Wie viel sind 3 · 7?", ["antwort"] = "21", ["erklaerung"] = "Drei Siebenergruppen: 7 + 7 + 7 = 21.", ["hinweis"] = "Zähle in Siebenerschritten." }));
        var claude = new SpyMain();
        try
        {
            w.KiBackendsOverride = () => [claude, ki];
            var d = OpenTutor(w); w.ProgramSetLevel(UserLevel.Profi); w.Show(Tu); Settle();
            var before = d.Data.ToJsonString(); var history = d.History.Count;
            await Ask(w, Tu, "Gib mir eine neue Übung zum kleinen Einmaleins mit der Sieben");
            Assert.Equal(0, claude.Calls); // nur lokal
            Assert.True(ki.Calls >= 1); Assert.Contains("uebung.erzeugen", ki.Systems[^1]);
            Assert.Equal(before, d.Data.ToJsonString()); Assert.Equal(history, d.History.Count);
            Assert.Contains("erst nach „Ja, behalten“", w.AssistLast!.Value.Message);
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var e = Assert.Single(Nachhilfe.Eigene(d, "m-einmaleins"));
            Assert.Equal("ki", e.Herkunft); Assert.Equal("21", e.Item.Antworten[0]); Assert.Equal(["Zähle in Siebenerschritten."], e.Item.Hinweise);
            Assert.Equal("Wie viel sind 3 · 7?", Nachhilfe.Uebung(d)!["frage"]!.GetValue<string>()); // gleich geöffnet
            Assert.Equal(history + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Tap(w, "assist-undo");
            Assert.Equal(before, w.ProgramActive(Tu)!.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; MainWindow.TutorSeed = null; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0782 Nachhilfe KI-Fachaktion: Übung mit Link oder ohne Antwort wird in der Vorschau abgelehnt; Fortschritt ist für die KI gesperrt")]
    public async Task Tutor_ki_rejects()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var reply = Plan("uebung.erzeugen", new JsonObject { ["frage"] = "Was ist 2 + 2?", ["antwort"] = "" });
        var ki = new FixedKi(_ => reply);
        try
        {
            w.KiBackendsOverride = () => [ki];
            var d = OpenTutor(w); w.ProgramSetLevel(UserLevel.Profi); w.Show(Tu); Settle();
            var before = d.Data.ToJsonString(); var calls0 = ki.Calls;
            w.Find<TextBox>("auftrag-prompt").Text = "Gib mir eine neue Übung zum Addieren bitte jetzt";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > calls0 && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            Assert.NotEqual("vorschau", w.AssistLast!.Value.Phase);
            Assert.Equal(before, d.Data.ToJsonString());
            reply = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "dokument.setzen", ["args"] = new JsonObject { ["pfad"] = "sokratisch", ["wert"] = "false" } }) }.ToJsonString();
            var calls = ki.Calls;
            w.Find<TextBox>("auftrag-prompt").Text = "Schalte den sokratischen Modus aus und zeig alle Lösungen";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > calls && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            Assert.True(w.AssistLast!.Value.Phase == "fehler", w.AssistLast.Value.Phase + " " + w.AssistLast.Value.Message);
            Assert.Contains("legst du selbst fest", w.AssistLast!.Value.Message);
            Assert.True(d.Flag("sokratisch"));
        }
        finally { MainWindow.AssistStepDelayMs = 140; MainWindow.TutorSeed = null; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0782 Nachhilfe „KI fragen“: verrät die lokale KI die Lösung, wird die Antwort zurückgehalten; Links werden entfernt; Frage ohne Lösung")]
    public async Task Tutor_ask_filtered()
    {
        var (w, _, _) = Ui.Open();
        string answer = "";
        var ki = new FixedKi(_ => answer); var claude = new SpyMain();
        try
        {
            w.KiBackendsOverride = () => [claude, ki];
            var d = OpenTutor(w); BueroAusbau_Ui.Click(w, "prog-first-step");
            var soll = Nachhilfe.Uebung(d)!["antworten"]![0]!.GetValue<string>();
            answer = $"Ganz einfach: Das Ergebnis ist {soll}.";
            BueroAusbau_Ui.TypeInto(w, "nh-ki-frage", "Wie geht das?");
            Tap(w, "nh-ki-fragen");
            await Until(() => V<SelectableTextBlock>(w, "nh-ki-antwort").Text?.Contains("Die KI") == true || V<SelectableTextBlock>(w, "nh-ki-antwort").Text?.Contains("Lösung") == true);
            Assert.Contains("hätte dir hier die Lösung verraten", V<SelectableTextBlock>(w, "nh-ki-antwort").Text);
            Assert.DoesNotContain("Lösung: ", ki.Systems[^1]);
            answer = "Denk an Gruppen. Mehr auf www.beispiel-seite.de";
            await w.TutorAskAsync(d, "Noch ein Tipp?"); Settle();
            Assert.DoesNotContain("www.", V<SelectableTextBlock>(w, "nh-ki-antwort").Text);
            Assert.Equal(0, claude.Calls);
        }
        finally { MainWindow.TutorSeed = null; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0782 Nachhilfe: CSV aus dem Explorer ablegen → Übungen im Dokument (ein Schritt); Rechtsklick „Dieses Thema üben“; Kinderprofil kann sokratisch nicht ausschalten")]
    public void Tutor_drop_context_child()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var d = OpenTutor(w); w.ProgramSetLevel(UserLevel.Profi); w.Show(Tu); Settle();
            var csv = Path.Combine(root, "uebungen.csv");
            File.WriteAllText(csv, "Thema;Frage;Antwort;Erklärung;Hinweis 1\r\nKleines Einmaleins;Wie viel ist 6 · 6?;36;6 Sechsergruppen;Denk an 5 · 6\r\n", new UTF8Encoding(true));
            var history = d.History.Count;
            BueroAusbau_Ui.Drop(w, "nh-thema-karte", csv);
            Assert.Single(Nachhilfe.Eigene(d, "m-einmaleins")); Assert.Equal(history + 1, d.History.Count);
            BueroAusbau_Ui.Menu(w, "nh-thema-2", "nh-ctx-practice");
            Assert.Equal("m-teilen", d.Text("thema")); Assert.NotNull(Nachhilfe.Uebung(d));
        }
        finally { MainWindow.TutorSeed = null; w.Close(); }

        var kroot = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(kroot);
        new UserSettings { Child = true, Name = "Kind" }.Save(kroot);
        var core = new TrendoCore(kroot, new InMemorySecretStore(), new FakeLauncher(), offline: false);
        var k = new MainWindow(core, new AppOptions(kroot, null, 0, false, Isolated: true), new Probe("browser"));
        k.Show(); Settle();
        try
        {
            Assert.Equal(Principal.ChildProfile.Name, k.CurrentPrincipal.Name);
            var d = OpenTutor(k); k.ProgramSetLevel(UserLevel.Profi); k.Show(Tu); Settle();
            BueroAusbau_Ui.Cmd(k, "prog-cmd-nh-socratic");
            Assert.True(d.Flag("sokratisch")); Assert.Contains("Kinderprofil", k.ProgramMessage(Tu));
        }
        finally { MainWindow.TutorSeed = null; k.Close(); }
    }

    // ───────────── Programmieren lernen ─────────────

    static ProgramDocument OpenCode(MainWindow w)
    {
        MainWindow.CodeLearnToolsRoot = DataLocation.ToolsRootIn(null);
        var d = BueroAusbau_Ui.StartEmpty(w, Cl, "leer");
        w.ProgramSetLevel(UserLevel.Profi); w.Show(Cl); Settle();
        return d;
    }

    static void SetCode(MainWindow w, string code)
    {
        var ed = V<TextEditor>(w, "cl-editor"); ed.Document.Text = code; Settle(); // wie Einfügen aus der Zwischenablage
    }

    static async Task RunTests(MainWindow w)
    {
        Tap(w, "cl-run");
        await Until(() => w.CodeLearnRun?.IsCompleted == true, 30000); Settle();
        await Until(() => !w.ProgramMessage(Cl).StartsWith("Tests laufen", StringComparison.Ordinal), 5000); Settle();
    }

    [AvaloniaFact(DisplayName = "T-0782 Programmieren lernen ECHT: Klick → Startcode fällt durch → Code getippt → alle Tests grün (AppContainer) → nächste Aufgabe frei; Export .py läuft in fremdem Python")]
    public async Task Code_click_to_real_tests()
    {
        if (LernSandbox.BundledPython() is null) { Assert.Skip("Mitgeliefertes Python fehlt auf diesem PC."); return; }
        var (w, _, root) = Ui.Open();
        try
        {
            var d = OpenCode(w);
            Assert.False(V<Button>(w, "cl-aufgabe-1").IsEnabled); // „Zwei Zahlen addieren“ gesperrt
            await RunTests(w);
            Assert.StartsWith("✕ 0/3", V<TextBlock>(w, "cl-status").Text);
            Assert.Equal(1, CodeLernen.Versuche(d, "gruss"));
            SetCode(w, "def gruss(name):\n    return f'Hallo, {name}!'\n");
            await RunTests(w);
            Assert.StartsWith("✓ Bestanden – 3/3", V<TextBlock>(w, "cl-status").Text);
            Assert.True(CodeLernen.Geloest(d, "gruss"));
            Assert.Contains("Weiter mit „Zwei Zahlen addieren“", w.ProgramMessage(Cl));
            Assert.True(V<Button>(w, "cl-aufgabe-1").IsEnabled);
            Assert.Equal("def gruss(name):\n    return f'Hallo, {name}!'\n", CodeLernen.Code(d)); // der getippte Code ist im Dokument (rückgängig machbar)
            BueroAusbau_Ui.Cmd(w, "prog-export-py");
            var py = BueroAusbau_Ui.Exported(w, Cl);
            // Fremdprogramm: das System-Python (nicht Trendos Laufzeit) führt die exportierte Datei mit ihren Tests aus
            var sys = new ProcessStartInfo("python", $"\"{py}\"") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(sys)!; var err = p.StandardError.ReadToEnd(); p.WaitForExit(30000);
            Assert.True(p.ExitCode == 0, err); Assert.Contains("OK", err); Assert.Contains("Ran 3 tests", err);
        }
        finally { MainWindow.CodeLearnToolsRoot = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0782 Programmieren lernen ECHT: Endlosschleife → Zeitgrenze, Syntaxfehler → verständliche Meldung; .py ablegen ersetzt den Code (ein Schritt)")]
    public async Task Code_timeout_error_drop()
    {
        if (LernSandbox.BundledPython() is null) { Assert.Skip("Mitgeliefertes Python fehlt auf diesem PC."); return; }
        var (w, _, root) = Ui.Open();
        try
        {
            var d = OpenCode(w);
            SetCode(w, "def gruss(name)\n    return name\n");
            await RunTests(w);
            Assert.Equal("✕ Fehler", V<TextBlock>(w, "cl-status").Text);
            Assert.Contains("Schreibfehler", V<SelectableTextBlock>(w, "cl-meldung").Text);
            var f = Path.Combine(root, "meine.py"); File.WriteAllText(f, "def gruss(name):\n    return 'Hallo, ' + name + '!'\n");
            var history = d.History.Count;
            BueroAusbau_Ui.Drop(w, "cl-aufgabe-karte", f);
            Assert.Contains("return 'Hallo, ' + name", CodeLernen.Code(d)); Assert.Equal(history + 1, d.History.Count);
            Assert.Contains("meine.py", w.ProgramMessage(Cl));
        }
        finally { MainWindow.CodeLearnToolsRoot = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0782 Programmieren lernen KI-Fachaktion: hinweis.geben → Vorschau → Ja → Hinweis bei der Aufgabe, EIN Rückgängig-Schritt; fertiger Code als „Hinweis“ wird abgelehnt; die KI bewertet nicht")]
    public async Task Code_ki_hint()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var reply = Plan("hinweis.geben", new JsonObject { ["hinweis"] = "Schau, ob deine Funktion etwas mit return zurückgibt oder nur mit print anzeigt." });
        var ki = new FixedKi(_ => reply);
        try
        {
            w.KiBackendsOverride = () => [ki];
            MainWindow.CodeLearnToolsRoot = DataLocation.ToolsRootIn(null);
            var d = BueroAusbau_Ui.StartEmpty(w, Cl, "leer"); w.ProgramSetLevel(UserLevel.Profi); w.Show(Cl); Settle();
            var before = d.Data.ToJsonString(); var history = d.History.Count;
            await Ask(w, Cl, "Gib mir einen Hinweis, warum meine Begrüßung nicht klappt");
            Assert.Equal(before, d.Data.ToJsonString());
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.Equal("fertig", w.AssistLast!.Value.Phase);
            Assert.Single(CodeLernen.KiHinweise(d, "gruss"));
            Assert.Equal(history + 1, d.History.Count);
            Assert.False(CodeLernen.Geloest(d, "gruss")); Assert.Equal(0, CodeLernen.Versuche(d, "gruss")); // die KI hat nichts bewertet
            Assert.StartsWith("🤖", V<TextBlock>(w, "cl-kihinweis-0").Text);
            Tap(w, "assist-undo");
            Assert.Empty(CodeLernen.KiHinweise(w.ProgramActive(Cl)!, "gruss"));
            reply = Plan("hinweis.geben", new JsonObject { ["hinweis"] = "```python\ndef gruss(name):\n    return f'Hallo, {name}!'\n```" });
            w.Find<TextBox>("auftrag-prompt").Text = "Gib mir einen Hinweis, warum meine Begrüßung nicht klappt";
            Tap(w, "auftrag-pruefen");
            await Until(() => w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen");
            Assert.NotEqual("vorschau", w.AssistLast!.Value.Phase);
            Assert.Empty(CodeLernen.KiHinweise(w.ProgramActive(Cl)!, "gruss"));
        }
        finally { MainWindow.AssistStepDelayMs = 140; MainWindow.CodeLearnToolsRoot = null; w.KiBackendsOverride = null; w.Close(); }
    }

    // ───────────── Echtlauf mit lokaler KI ─────────────

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0782_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0782 ECHT: lokale KI (Ollama) – Nachhilfe „Übung erzeugen“ und Programmieren „Hinweis geben“ im offenen Dokument, Rückgängig; Code-Tests echt (nur mit TRENDO_T0782_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) { Assert.Skip("Echtlauf nur mit TRENDO_T0782_ECHT=<Belegordner> (braucht Ollama mit Modell auf diesem PC)."); return; }
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var log = new StringBuilder(); void L(string s) => log.AppendLine(s);
        var seen = 0;
        void KiAudit() { var all = w.Core.Audit.ReadAll(); foreach (var a in all.Skip(seen).Where(a => a.Action.StartsWith("ki.", StringComparison.Ordinal) || a.Action.StartsWith("assist.", StringComparison.Ordinal) || a.Action.StartsWith("lernen.", StringComparison.Ordinal) || a.Action.StartsWith("nachhilfe.", StringComparison.Ordinal))) L($"  - Protokoll: {a.Action} {a.Detail}"); seen = all.Count; }
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w);
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source);
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            var choice = w.KiModelChoice;
            L("# T-0782 ECHT: lokale KI schreibt ins offene Dokument (Nachhilfe mit KI, Programmieren lernen)"); L("");
            L($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            L("Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt."); L("");

            var d = OpenTutor(w); w.ProgramSetLevel(UserLevel.Profi); w.Show(Tu); Settle();
            Nachhilfe.ThemaWaehlen(d, "m-kuerzen"); w.Show(Tu); Settle(); seen = w.Core.Audit.ReadAll().Count;
            var history = d.History.Count;
            const string p1 = "Erzeuge mir eine neue Übung zum Brüche kürzen mit dem Bruch 12/18, mit Lösungsweg und einem Hinweis, der die Lösung nicht verrät.";
            var sw = Stopwatch.StartNew();
            w.Find<TextBox>("auftrag-prompt").Text = p1; Tap(w, "auftrag-pruefen");
            await Until(() => w.AssistLastKey == Tu && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen", 600_000); sw.Stop();
            L("## Nachhilfe mit KI (nachhilfe-programm)"); L(""); L($"- Auftrag: {p1}");
            L($"- Phase nach {sw.Elapsed.TotalSeconds:0} s: {w.AssistLast!.Value.Phase} · Quelle: {w.Find<TextBlock>("assist-source").Text}");
            L($"- KI-Antwort (roh, gekürzt): {(w.AssistLastKiAnswer ?? "").Replace("\n", " ")[..Math.Min(600, (w.AssistLastKiAnswer ?? "").Length)]}");
            KiAudit();
            Assert.True(w.AssistLast!.Value.Phase == "vorschau", w.AssistLast.Value.Message + " | " + w.AssistLastKiAnswer);
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.Equal("fertig", w.AssistLast!.Value.Phase);
            var e = Assert.Single(Nachhilfe.Eigene(d, "m-kuerzen"));
            L($"- Übernommen: Frage „{e.Item.Frage}“ · Antwort(en) {string.Join(" | ", e.Item.Antworten)} · Hinweise {e.Item.Hinweise.Count} · Art {e.Item.Art} · Herkunft {e.Herkunft}");
            L($"- Rückgängig-Schritte vorher/nachher: {history} → {d.History.Count} („{d.UndoLabel}“)");
            Assert.Equal(history + 1, d.History.Count);
            // Die Übung lässt sich sofort lösen (Antwort der KI geprüft wie jede Übung)
            // ohne Rückgängig-Schritt geprüft (Vergleich wie bei „Prüfen“), damit „Rückgängig“ danach genau den KI-Schritt trifft
            var (ok, msg) = Nachhilfe.Vergleiche(e.Item, e.Item.Antworten[0]);
            L($"- Antwort „{e.Item.Antworten[0]}“ geprüft: {(ok ? "richtig" : "falsch")} ({msg})");
            foreach (var other in e.Item.Antworten.Skip(1)) L($"- Weitere von der KI genannte Antwort „{other}“: {(Nachhilfe.Vergleiche(e.Item, other).Richtig ? "richtig" : "nicht richtig – " + Nachhilfe.Vergleiche(e.Item, other).Rueckmeldung)}");
            Tap(w, "assist-undo"); var after = w.ProgramActive(Tu)!;
            Assert.Empty(Nachhilfe.Eigene(after, "m-kuerzen"));
            L($"- Nach „Rückgängig“ (Assistent): eigene Übungen im Thema = {Nachhilfe.Eigene(after, "m-kuerzen").Count}");
            L("");

            // Programmieren lernen
            MainWindow.CodeLearnToolsRoot = DataLocation.ToolsRootIn(null);
            var c = BueroAusbau_Ui.StartEmpty(w, Cl, "leer"); w.ProgramSetLevel(UserLevel.Profi); w.Show(Cl); Settle();
            SetCode(w, "def gruss(name):\n    print('Hallo, ' + name + '!')\n");
            await RunTests(w);
            var l = CodeLernen.Letzter(c)!;
            L("## Programmieren lernen (programmieren-lernen-programm)"); L("");
            L($"- Code der lernenden Person: print statt return · echter Testlauf im AppContainer: {l.Status}, {l.Bestanden}/{l.Tests.Count} bestanden");
            seen = w.Core.Audit.ReadAll().Count; history = c.History.Count;
            const string p2 = "Meine Tests für die Begrüßung schlagen fehl. Gib mir einen Hinweis, was ich ändern muss, ohne mir die Lösung zu verraten.";
            sw.Restart();
            w.Find<TextBox>("auftrag-prompt").Text = p2; Tap(w, "auftrag-pruefen");
            var audits = w.Core.Audit.ReadAll().Count;
            bool NoPlan() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.ki.ohneplan" or "assist.ki.fehler" && a.Detail.StartsWith(Cl, StringComparison.Ordinal));
            await Until(() => NoPlan() || w.AssistLastKey == Cl && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen", 600_000); sw.Stop();
            L($"- Auftrag: {p2}");
            L($"- Phase nach {sw.Elapsed.TotalSeconds:0} s: {(NoPlan() ? "kein Plan" : w.AssistLast!.Value.Phase)} · Quelle: {(NoPlan() ? "–" : w.Find<TextBlock>("assist-source").Text)}");
            L($"- KI-Antwort (roh, gekürzt): {(w.AssistLastKiAnswer ?? "").Replace("\n", " ")[..Math.Min(600, (w.AssistLastKiAnswer ?? "").Length)]}");
            KiAudit();
            Assert.False(NoPlan(), "kein Plan aus der KI-Antwort: " + w.AssistLastKiAnswer);
            Assert.True(w.AssistLast!.Value.Phase == "vorschau", w.AssistLast.Value.Message + " | " + w.AssistLastKiAnswer);
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.Equal("fertig", w.AssistLast!.Value.Phase);
            var hint = Assert.Single(CodeLernen.KiHinweise(c, "gruss"));
            L($"- Übernommen: Hinweis „{hint}“ · Rückgängig-Schritte {history} → {c.History.Count}");
            L($"- Bewertung unverändert (die KI bewertet nicht): gelöst = {CodeLernen.Geloest(c, "gruss")}, Versuche = {CodeLernen.Versuche(c, "gruss")}");
            Assert.False(CodeLernen.Geloest(c, "gruss"));
            Tap(w, "assist-undo");
            L($"- Nach „Rückgängig“: KI-Hinweise = {CodeLernen.KiHinweise(w.ProgramActive(Cl)!, "gruss").Count}");
            L(""); L("Ergebnis: bestanden");
        }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-lokale-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"), new UTF8Encoding(false));
            MainWindow.AssistStepDelayMs = 140; MainWindow.TutorSeed = null; MainWindow.CodeLearnToolsRoot = null; w.KiBackendsOverride = null; w.Close();
        }
    }
}
