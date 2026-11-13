using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// T-0316: Claude über das Abo – Karte mit einem Knopf je Zustand; Claude als Haupt-KI beantwortet Fragen (eingegrenzt, ohne Dateiänderungen); abschaltbar.
/// Claude wird vollständig nachgebildet – kein Test nutzt das echte Abo.
[Collection("Claude")]
public class Claude_Ui
{
    static Task<ClaudeCode.Status> St(bool inst, bool on) => Task.FromResult(inst ? new ClaudeCode.Status(true, on, on ? "claude.ai" : "", on ? "max" : "", @"C:\x\claude.exe") : ClaudeCode.Status.Missing);
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }

    [AvaloniaFact(DisplayName = "T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „Mit deinem Claude-Abo verbunden (Max)“")]
    public async Task Card_states()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (inst, on, button, text) in new[] { (false, false, "claude-install", "nicht installiert"), (true, false, "claude-connect", "nicht angemeldet"), (true, true, (string?)null, "Mit deinem Claude-Abo verbunden (Max)") })
            {
                MainWindow.ClaudeStatusOverride = () => St(inst, on);
                await Run(w.ClaudeCheck(true));
                w.Show("ki-quellen"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains(text, w.Find<TextBlock>("claude-state").Text);
                Assert.Equal(button is null, w.TryFind<Button>("claude-install") is null && w.TryFind<Button>("claude-connect") is null);
                if (button is not null) Assert.NotNull(w.TryFind<Button>(button));
            }
            Assert.True(w.Find<CheckBox>("claude-main").IsChecked); // Einstellung bleibt gespeichert, sichtbar erst mit Freigabe
            w.Show("ki-fragen"); Assert.NotNull(w.TryFind<Border>("claude-card"));
        }
        finally { MainWindow.ClaudeStatusOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Claude Code öffnen“ starten nie ein echtes Programm")]
    public async Task Test_mode_starts_nothing()
    {
        var (w, _, _) = Ui.Open();
        var before = System.Diagnostics.Process.GetProcesses().Select(p => p.Id).ToHashSet();
        await w.ClaudeInstall(); await w.ClaudeConnect(); w.ClaudeOpen(Path.GetTempPath(), "x");
        Assert.Equal(3, w.Core.Audit.ReadAll().Count(a => a.Action == "claude.testmodus"));
        var neu = System.Diagnostics.Process.GetProcesses().Where(p => !before.Contains(p.Id)).Select(p => { try { return p.ProcessName; } catch (InvalidOperationException) { return ""; } }).ToList();
        Assert.DoesNotContain(neu, n => n is "claude" or "powershell");
        w.Show("ki-quellen"); Assert.Contains("Testmodus", w.Find<TextBlock>("claude-msg").Text);
    }

    [AvaloniaFact(DisplayName = "Claude-Regeln 01.11.2026: Trendo speichert keine Claude-Tokens – ein alter wird beim Start gelöscht, kein Token-Feld mehr")]
    public void No_token_storage()
    {
        var (w, _, _) = Ui.Open();
        w.Core.Secrets.Set(MainWindow.ClaudeTokenKey, "sk-ant-oat01-" + new string('y', 40));
        Assert.True(w.ClaudeForgetStoredToken()); Assert.Null(w.Core.Secrets.Get(MainWindow.ClaudeTokenKey));
        Assert.False(w.ClaudeForgetStoredToken());
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.token.entfernt");
        w.Show("ki-quellen"); Assert.Null(w.TryFind<TextBox>("claude-token")); Assert.Null(w.TryFind<Expander>("claude-token-box"));
    }

    [AvaloniaFact(DisplayName = "Claude-Regeln 01.11.2026: Claude Code aus Trendo erlaubt (eigene Anmeldung je Nutzer) – kein Sperr-Hinweis; „Claude Code öffnen“ startet das echte Claude Code im Projektordner")]
    public async Task Embedding_allowed()
    {
        Assert.True(ClaudeCode.EmbeddingApproved);
        var (w, _, _) = Ui.Open();
        (string Folder, IReadOnlyList<string> Args)? opened = null;
        MainWindow.ClaudeStatusOverride = () => St(true, true);
        MainWindow.ClaudeOpenOverride = (f, a) => opened = (f, a);
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("ki-quellen"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(w.Find<TextBlock>("claude-gate").IsVisible); Assert.True(w.Find<CheckBox>("claude-main").IsVisible);
            w.Find<Button>("claude-open").Press();
            Assert.NotNull(opened); Assert.DoesNotContain("-p", opened!.Value.Args);
            w.ClaudeOpen(Path.GetTempPath(), "Mach die Überschrift blau", "Du bist Webdesignerin.");
            Assert.Equal(["--append-system-prompt", "Du bist Webdesignerin.", "Mach die Überschrift blau"], opened!.Value.Args);
            Assert.Contains(w.Core.Audit.ReadAll(), x => x.Action == "claude.open");
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeOpenOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0316: Studio – „Claude“ immer in der Auftragsleiste; das Claude-Projekt des Studios trägt seine Rolle, kein Terminal")]
    public async Task Open_from_code_studio()
    {
        var (w, _, _) = Ui.Open();
        (string Folder, IReadOnlyList<string> Args)? opened = null;
        MainWindow.ClaudeOpenOverride = (f, a) => opened = (f, a);
        try
        {
            MainWindow.ClaudeStatusOverride = () => St(false, false); await Run(w.ClaudeCheck(true));
            w.Show("baukasten"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.TryFind<Button>("auftrag-claude")); // auch ohne Claude – dann mit „einrichten“
            MainWindow.ClaudeStatusOverride = () => St(true, true); await Run(w.ClaudeCheck(true));
            w.Show("start"); w.Show("baukasten"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            w.Find<TextBox>("auftrag-prompt").Text = "Baue eine Kontaktseite mit Formular";
            w.Find<Button>("auftrag-claude").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            w.ClaudeWebNavigated("https://claude.ai/new"); // angemeldet
            var instr = await w.ClaudeProjectSetup("Kontaktseite");
            Assert.Contains("Apps zusammenklicken", instr); // Rolle des Studios in den Anweisungen des Claude-Projekts
            Assert.Null(opened); // kein Terminal, kein Claude-Code-Fenster
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeOpenOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt")]
    public async Task Main_ki()
    {
        var (w, _, root) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeEmbedApproved = true;
        MainWindow.ClaudeStatusOverride = () => St(true, true);
        MainWindow.ClaudeRunOverride = (args, dir, line, _) =>
        {
            lock (calls) calls.Add(args);
            line("""{"type":"system","subtype":"init","session_id":"s-9","model":"claude-opus-5-5"}""");
            line("""{"type":"assistant","message":{"content":[{"type":"text","text":"Ein Rechnungsbetrag ist die Summe, die zu zahlen ist."}]}}""");
            line("""{"type":"result","subtype":"success","is_error":false,"result":"Ein Rechnungsbetrag ist die Summe, die zu zahlen ist.","session_id":"s-9","num_turns":1}""");
            return Task.FromResult(0);
        };
        try
        {
            var a = await w.Intel.AskAsync(new KiRequest("Was ist ein Rechnungsbetrag?", "Du bist Buchhalterin in Trendo."));
            Assert.True(a.Ok, a.Text); Assert.Equal("Claude (dein Abo)", a.Backend); Assert.Contains("Summe, die zu zahlen ist", a.Text);
            var args = Assert.Single(calls).ToList();
            Assert.Contains("--restricted", args); Assert.Contains("--strict-mcp-config", args);
            Assert.Equal("Edit Write NotebookEdit MultiEdit", args[args.IndexOf("--disallowedTools") + 1]);
            Assert.Contains("Buchhalterin", args[args.IndexOf("--append-system-prompt") + 1]);
            KiSourceSettings.Update(root, s => s.ClaudeMain = false);
            var b = await w.Intel.AskAsync(new KiRequest("Noch eine Frage"));
            Assert.Single(calls); Assert.NotEqual("Claude (dein Abo)", b.Backend);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeEmbedApproved = ClaudeCode.EmbeddingApproved; }
    }
}
