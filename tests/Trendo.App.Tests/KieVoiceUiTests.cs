using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0618: eigene Stimme für Suno – nur mit Bestätigung, zwei kostenlose Schritte, Stimme wird nur bei bestandener Prüfung gespeichert.</summary>
public class KieVoice_Ui
{
    sealed class Fake(bool pass) : IWebSend
    {
        public readonly List<(string Url, string Body)> Calls = [];
        int _polls;
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            var b = body is null ? "" : Encoding.UTF8.GetString(body);
            Calls.Add((url, b));
            string json = url switch
            {
                var u when u.EndsWith("/chat/credit") => "{\"code\":200,\"data\":100}",
                var u when u.Contains("file-base64-upload") => "{\"code\":200,\"data\":{\"downloadUrl\":\"https://tempfile.redpandaai.co/stimme.mp3\"}}",
                var u when u.EndsWith("/jobs/createTask") && b.Contains("validation-phrase") => "{\"code\":200,\"data\":{\"taskId\":\"phrase-7\"}}",
                var u when u.EndsWith("/jobs/createTask") && b.Contains("create-voice") => "{\"code\":200,\"data\":{\"taskId\":\"create-3\"}}",
                // erst ohne Satz, dann mit Satz (Auftrag bleibt „generating“), nach dem Anlegen „success“ oder „fail“
                var u when u.Contains("recordInfo") && !Calls.Any(c => c.Body.Contains("create-voice")) =>
                    ++_polls < 2 ? "{\"code\":200,\"data\":{\"state\":\"generating\"}}"
                                 : "{\"code\":200,\"data\":{\"state\":\"generating\",\"resultJson\":\"{\\\"data\\\":{\\\"verify_phrase_text\\\":\\\"Heute scheint die Sonne\\\"}}\"}}",
                var u when u.Contains("recordInfo") => pass ? "{\"code\":200,\"data\":{\"state\":\"success\",\"resultJson\":\"{}\"}}" : "{\"code\":200,\"data\":{\"state\":\"fail\",\"failMsg\":\"verify failed\"}}",
                _ => "{\"code\":404}",
            };
            return Task.FromResult(new WebReply(200, Encoding.UTF8.GetBytes(json)));
        }
    }

    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }

    [AvaloniaFact(DisplayName = "kie.ai eigene Stimme: ohne Bestätigung nichts im Netz, Prüfsatz holen, Stimme anlegen, danach im Lied wählbar und entfernbar")]
    public async Task Stimme_anlegen()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var fake = new Fake(pass: true); w.KieWebOverride = fake; w.KieDelayOverride = (_, _) => Task.CompletedTask;
            Assert.True(await Run(w.KieConnect("kie-schluessel-1234567")));
            var rec = Path.Combine(root, "meine-stimme.mp3"); File.WriteAllBytes(rec, [0x49, 0x44, 0x33, 1, 2]);
            var before = fake.Calls.Count;
            Assert.False(await Run(w.KieVoicePhrase(rec, 0, 20, "de", ownVoice: false)));
            Assert.Equal(before, fake.Calls.Count); // ohne Bestätigung kein Upload
            Assert.Contains("eigene Stimme", w.KieVoiceMessage);

            Assert.True(await Run(w.KieVoicePhrase(rec, 0, 20, "de", ownVoice: true)));
            Assert.Equal("Heute scheint die Sonne", w.KieVoicePhraseText);
            Assert.Contains(fake.Calls, c => c.Body.Contains("\"vocal_end_s\":20") && c.Body.Contains("\"language\":\"de\""));
            Assert.Null(w.KieVoiceId);

            var satz = Path.Combine(root, "satz.wav"); File.WriteAllBytes(satz, [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45]);
            Assert.True(await Run(w.KieVoiceCreate(satz, "Meine Stimme", "Pop")));
            Assert.Equal("phrase-7", w.KieVoiceId); // Stimm-ID = Auftrag des Prüfsatzes
            Assert.Contains(fake.Calls, c => c.Body.Contains("create-voice") && c.Body.Contains("\"task_id\":\"phrase-7\""));

            var host = new Window { Content = w.KiePanelForTest("STUDIO-MUSIK-01"), Width = 600, Height = 1000 }; host.Show(); Settle();
            Assert.Contains(host.GetVisualDescendants().OfType<CheckBox>(), c => c.Name == "kie-own-voice");
            host.Close();

            w.KieVoiceForget();
            Assert.Null(w.KieVoiceId);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "kie.ai eigene Stimme: nicht bestandene Prüfung speichert keine Stimme und sagt, was zu tun ist")]
    public async Task Pruefung_nicht_bestanden()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var fake = new Fake(pass: false); w.KieWebOverride = fake; w.KieDelayOverride = (_, _) => Task.CompletedTask;
            Assert.True(await Run(w.KieConnect("kie-schluessel-1234567")));
            var rec = Path.Combine(root, "stimme.mp3"); File.WriteAllBytes(rec, [0x49, 0x44, 0x33, 1]);
            Assert.False(await Run(w.KieVoiceCreate(rec, "", ""))); // ohne Prüfsatz geht es nicht
            Assert.True(await Run(w.KieVoicePhrase(rec, 0, 15, "de", true)));
            Assert.False(await Run(w.KieVoiceCreate(rec, "", "")));
            Assert.Null(w.KieVoiceId);
            Assert.Contains("noch einmal", w.KieVoiceMessage);
            var bad = Path.Combine(root, "text.txt"); File.WriteAllText(bad, "x");
            Assert.False(await Run(w.KieVoicePhrase(bad, 0, 15, "de", true)));
            Assert.Contains("MP3", w.KieVoiceMessage);
        }
        finally { w.Close(); }
    }
}
