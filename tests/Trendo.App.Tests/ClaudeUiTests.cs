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

    [AvaloniaFact(DisplayName = "T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“")]
    public async Task Card_states()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (inst, on, button, text) in new[] { (false, false, "claude-install", "nicht installiert"), (true, false, "claude-connect", "nicht angemeldet"), (true, true, (string?)null, "✓ Mit deinem Claude-Abo verbunden (Max)") })
            {
                MainWindow.ClaudeStatusOverride = () => St(inst, on);
                await Run(w.ClaudeCheck(true));
                w.Show("ki-quellen"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains(text, w.Find<TextBlock>("claude-state").Text);
                Assert.Equal(button is null, w.TryFind<Button>("claude-install") is null && w.TryFind<Button>("claude-connect") is null);
                if (button is not null) Assert.NotNull(w.TryFind<Button>(button));
            }
            Assert.True(w.Find<CheckBox>("claude-main").IsChecked);
            w.Show("ki-fragen"); Assert.NotNull(w.TryFind<Border>("claude-card"));
        }
        finally { MainWindow.ClaudeStatusOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0316: Abo-Token – API-Schlüssel wird abgelehnt (nur Abo), Abo-Token landet im sicheren Speicher und lässt sich entfernen")]
    public async Task Abo_token()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeStatusOverride = () => St(true, true);
        try
        {
            w.Show("ki-quellen");
            Assert.False(await w.ClaudeSetToken("sk-ant-api03-" + new string('x', 40)));
            Assert.Contains("nur über dein Abo", w.Find<TextBlock>("claude-msg").Text);
            Assert.Null(w.Core.Secrets.Get(MainWindow.ClaudeTokenKey));
            Assert.True(await w.ClaudeSetToken(" sk-ant-oat01-" + new string('y', 40) + " "));
            Assert.Equal("sk-ant-oat01-" + new string('y', 40), w.Core.Secrets.Get(MainWindow.ClaudeTokenKey));
            w.ClaudeRemoveToken(); Assert.Null(w.Core.Secrets.Get(MainWindow.ClaudeTokenKey));
        }
        finally { MainWindow.ClaudeStatusOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt")]
    public async Task Main_ki()
    {
        var (w, _, root) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
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
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; }
    }
}
