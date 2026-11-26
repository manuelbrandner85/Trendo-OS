using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Lernen;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0782 Gegenprüfung (App): H1 (nie Claude bei KiLocalOnly, auch mit „Claude ohne Klicks“), M2/M6 (was die KI zu sehen bekommt), N4 (entfernte „lokale“ Quelle),
/// App-Profil beim Deinstallieren und Hinweis in der App. Claude ist ersetzt (Anmeldestand und Lauf über die Test-Schnittstellen) – es wird gezählt, ob es gefragt wird.</summary>
[Collection("Claude")]
public class T0782Gegenpruefung_Ui
{
    const string Tu = MainWindow.TutorKey, Cl = MainWindow.CodeLearnKey;

    sealed class FixedKi(Func<string, string> reply, string url = "http://127.0.0.1:9") : IKiBackend
    {
        public string Name => "Ersatz-KI " + url; public string BaseUrl => url;
        public readonly List<string> All = []; public int Calls;
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
        { Calls++; All.Add(string.Join("\n", messages.Select(m => m.Content))); return Task.FromResult(reply(messages[^1].Content)); }
    }
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
    static async Task Run(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static Task<ClaudeCode.Status> Angemeldet() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));

    static ProgramDocument Open(MainWindow w, string key)
    {
        w.Width = 1600; w.Height = 1200; w.ProgramSetLevel(UserLevel.Profi); w.Show(key); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        return w.ProgramActive(key)!;
    }

    /// <summary>Claude „angemeldet“ und als Haupt-KI, „ohne Klicks“ an; jeder Claude-Lauf wird gezählt.</summary>
    static async Task<List<string>> ClaudeOn(MainWindow w)
    {
        var runs = new List<string>();
        MainWindow.ClaudeStatusOverride = Angemeldet; MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeRunOverride = (args, _, line, _) => { runs.Add(string.Join(" ", args)); line("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"ok\",\"session_id\":\"s\",\"num_turns\":1}"); return Task.FromResult(0); };
        await Run(w.ClaudeCheck(true));
        w.ClaudeNoClicks = true;
        return runs;
    }
    static void ClaudeOff() { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStepDelay = 350; }

    [AvaloniaFact(DisplayName = "H1: „Claude ohne Klicks“ und Claude angemeldet – Nachhilfe bleibt lokal (Auftragszeile, Menüband-Weg, Studio-Leiste): 0 Claude-Aufrufe; Gegenprobe: ein anderes Programm geht an Claude")]
    public async Task H1_tutor_never_claude()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var spy = new SpyMain();
        var ki = new FixedKi(_ => Plan("uebung.erzeugen", new JsonObject { ["frage"] = "Wie viel sind 6 · 7?", ["antwort"] = "42", ["hinweis"] = "Sechs Siebenergruppen." }));
        try
        {
            var runs = await ClaudeOn(w);
            w.KiBackendsOverride = () => [spy, ki];
            Assert.True(w.ClaudeTakesStudioPrompts); // Vorbedingung: Claude würde sonst übernehmen
            Assert.Equal(AssistRoute.ClaudeBereich, w.AssistRouteFor("text")); // Gegenprobe: normales Programm → Claude-Bereich
            var d = Open(w, Tu);
            Assert.Equal(AssistRoute.Dokument, w.AssistRouteFor(Tu));
            w.Find<TextBox>("auftrag-prompt").Text = "Gib mir eine neue Übung zum Einmaleins mit der Sieben bitte";
            Tap(w, "auftrag-pruefen");
            await Until(() => w.AssistLastKey == Tu && w.AssistLast?.Phase is "vorschau" or "fehler");
            Assert.Equal("vorschau", w.AssistLast!.Value.Phase);
            Tap(w, "assist-apply"); await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(await w.AssistFromProgram(Tu, "STUDIO-TUTOR-01", "Gib mir noch eine Übung zum Einmaleins mit der Acht") || w.AssistProgramNote is not null);
            Assert.False(await w.ClaudeFromStudioBar("STUDIO-TUTOR-01", "Erklär mir das"));
            Assert.Equal(0, spy.Calls); Assert.Empty(runs); Assert.True(ki.Calls >= 2);
        }
        finally { MainWindow.AssistStepDelayMs = 140; ClaudeOff(); w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "H1: Kinderprofil – „Programmieren lernen“ geht auch mit „Claude ohne Klicks“ nie an Claude (Route Dokument, Studio-Leiste lehnt ab)")]
    public async Task H1_child_codelearn_never_claude()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = true, Name = "Kind" }.Save(root);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false);
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.Show(); Settle();
        try
        {
            var runs = await ClaudeOn(w);
            var spy = new SpyMain(); var ki = new FixedKi(_ => "Denk an return.");
            w.KiBackendsOverride = () => [spy, ki];
            Open(w, Cl);
            Assert.Equal(AssistRoute.Dokument, w.AssistRouteFor(Cl));
            Assert.False(await w.ClaudeFromStudioBar("STUDIO-CODELEARN-01", "Hilf mir"));
            await w.CodeLearnAskAsync(w.ProgramActive(Cl)!, "Warum geht es nicht?");
            Assert.Equal(0, spy.Calls); Assert.Empty(runs); Assert.Equal(1, ki.Calls);
        }
        finally { ClaudeOff(); w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M6: die KI-Kette der Nachhilfe sieht die Frage, aber keine Antwort, keinen Lösungsweg und keinen Hinweis")]
    public async Task M6_ki_chain_sees_no_answer()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var ki = new FixedKi(_ => Plan("uebung.erzeugen", new JsonObject { ["frage"] = "Was ist 1 + 1?", ["antwort"] = "2" }));
        try
        {
            w.KiBackendsOverride = () => [ki];
            var d = Open(w, Tu);
            Nachhilfe.UebungAnlegen(d, "m-einmaleins", "Welcher Vogel ist gemeint?", "Zebrafink", "ERKL-GEHEIM-7", ["HINW-GEHEIM-8"], "eigen");
            w.Show(Tu); Settle();
            w.Find<TextBox>("auftrag-prompt").Text = "Gib mir eine weitere Übung zu diesem Thema bitte";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > 0 && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            var sent = string.Join("\n", ki.All);
            Assert.Contains("Welcher Vogel ist gemeint?", sent);
            Assert.DoesNotContain("Zebrafink", sent); Assert.DoesNotContain("ERKL-GEHEIM", sent); Assert.DoesNotContain("HINW-GEHEIM", sent);
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M2: Programmausgabe und Testmeldungen gehen weder über die KI-Kette noch über „Hinweis von der KI“ an eine nicht lokale KI; Hinweis in der App nennt Grenzen und App-Profil")]
    public async Task M2_no_output_to_claude()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var main = new SpyMain();
        var ki = new FixedKi(_ => Plan("hinweis.geben", new JsonObject { ["hinweis"] = "Prüfe, ob du etwas zurückgibst." }));
        try
        {
            w.KiBackendsOverride = () => [ki];
            var d = Open(w, Cl);
            CodeLernen.Eintragen(d, new("fail", [new("lea", "fail", "gruss(\"Lea\") ergab 'PC-GEHEIM-1', erwartet …", "AUSGABE-GEHEIM-2")], "MELDUNG-GEHEIM-3", 0.1));
            w.Show(Cl); Settle();
            w.Find<TextBox>("auftrag-prompt").Text = "Gib mir einen Hinweis, warum mein Test fehlschlägt";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > 0 && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            await w.CodeLearnAskAsync(d, "Warum?");
            var sent = string.Join("\n", ki.All);
            Assert.DoesNotContain("GEHEIM", sent); Assert.Contains("lea", sent);
            w.Show(Cl); Settle();
            var hint = w.Find<TextBlock>("cl-hinweis-sicherheit").Text!;
            Assert.Contains("Rechner- und Benutzername", hint); Assert.Contains("nicht an Claude", hint); Assert.Contains("Deinstallieren", hint); Assert.Contains("Trendo.Lernen.Code", hint);
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "N4: eine „lokale“ Quelle mit entfernter Adresse wird in der Nachhilfe nie gefragt")]
    public async Task N4_remote_url_not_local()
    {
        var (w, _, _) = Ui.Open();
        var fern = new FixedKi(_ => "fern", "http://192.168.178.20:11434");
        var nah = new FixedKi(_ => "Denk an Gruppen.");
        try
        {
            w.KiBackendsOverride = () => [fern, nah];
            var d = Open(w, Tu);
            Nachhilfe.NaechsteUebung(d, new Random(1));
            await w.TutorAskAsync(d, "Wie geht das?");
            Assert.Equal(0, fern.Calls); Assert.Equal(1, nah.Calls);
            Assert.False(MainWindow.IsLoopbackKi(fern)); Assert.True(MainWindow.IsLoopbackKi(nah));
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "App-Profil: Deinstallation entfernt das Profil (hier ein eigenes Test-Profil); eine Prüf-Instanz lässt es stehen")]
    public void Uninstall_removes_profile()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "Trendo.Lernen.Test" + Guid.NewGuid().ToString("N")[..8];
        var log = new List<string>();
        try
        {
            LernSandbox.Sid(name);
            UninstallPrompt.RemoveLearnProfile(@"C:\Pruef-Instanz", log.Add, name);
            Assert.True(LernSandbox.ProfileExists(name)); Assert.Contains(log, l => l.Contains("bleibt"));
            UninstallPrompt.RemoveLearnProfile(null, log.Add, name);
            Assert.False(LernSandbox.ProfileExists(name)); Assert.Contains(log, l => l.Contains("entfernt"));
        }
        finally { if (LernSandbox.ProfileExists(name)) LernSandbox.RemoveProfile(name); }
    }
}
