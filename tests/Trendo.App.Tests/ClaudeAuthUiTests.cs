using System.Text.Json;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0621: Abgelaufene Claude-Anmeldung („Failed to authenticate … cannot be renewed“) darf nie als Ergebnis – etwa als Liedtext – ankommen.
/// Trendo merkt sie, die Ersatz-KI übernimmt, und „Claude neu anmelden“ wird angeboten. Claude ist nachgebildet.</summary>
[Collection("Claude")]
public class ClaudeAuth_Ui
{
    const string Expired = "Failed to authenticate: this login reached its time limit and cannot be renewed. Run `claude auth login` to sign in again, or use `claude setup-token` and set CLAUDE_CODE_OAUTH_TOKEN for unattended jobs";

    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static string J(object o) => JsonSerializer.Serialize(o);
    static async Task<T> Run<T>(Task<T> t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } return await t; }

    sealed class Ersatz : IKiBackend
    {
        public string Name => "Ersatz-KI (Test)";
        public string BaseUrl => "http://127.0.0.1:1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["klein"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => Task.FromResult("[Strophe]\nWir fahren an den See");
    }

    [AvaloniaFact(DisplayName = "T-0621: Abgelaufene Claude-Anmeldung wird kein Liedtext – Ersatz-KI antwortet, Claude gilt bis zur neuen Anmeldung als nicht bereit")]
    public async Task Abgelaufen_wird_kein_Ergebnis()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeStatusOverride = St;
        var runs = 0;
        MainWindow.ClaudeRunOverride = (args, _, line, _) =>
        {
            runs++;
            line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
            line(J(new { type = "result", subtype = "success", is_error = true, result = Expired, session_id = "s1", num_turns = 1 }));
            return Task.FromResult(1);
        };
        try
        {
            var claude = new ClaudeKiBackend(w, () => "claude");
            Assert.True(await Run(claude.IsUpAsync()));
            var ex = await Assert.ThrowsAsync<KiModelBusyException>(() => claude.ChatAsync("claude", [new KiMessage("user", "Schreib einen Liedtext")]));
            Assert.Equal(ClaudeCode.AuthText, ex.Message);
            Assert.True(w.ClaudeAuthLost);
            Assert.False(await Run(claude.IsUpAsync())); // bis zur neuen Anmeldung nicht mehr fragen
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.anmeldung.abgelaufen");

            // Über den KI-Kern (wie Trendo Musik): Claude zuerst, dann die Ersatz-KI – der Liedtext kommt von der Ersatz-KI, nie die Fehlermeldung
            w.KiBackendsOverride = () => [claude, new Ersatz()];
            var a = await Run(w.Intel.AskAsync(new KiRequest("Schreib einen Liedtext über einen Sommertag", UseKnowledge: false, AllowRemote: true)));
            Assert.True(a.Ok);
            Assert.DoesNotContain("authenticate", a.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("See", a.Text);
            Assert.Equal(1, runs); // nach dem ersten Fehlschlag fragt Trendo Claude nicht erneut
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.KiBackendsOverride = null; w.Close(); }
    }
}
