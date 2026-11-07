using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik V3/V4/V5/V6 (07.11.2026): Stimme aus Aufnahme lernen, Lieder in dieser Stimme, neuer Text auf vorhandene Musik, Stimmen-Bibliothek.</summary>
public class MusikVoices_Ui
{
    sealed class FakeAce : HttpMessageHandler
    {
        public readonly List<string> Requests = []; int _n;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage Js(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            static string W(string d) => "{\"data\":" + d + ",\"code\":200,\"error\":null}";
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return Js("{\"status\":\"ok\"}");
                case "/release_task": { var body = await r.Content!.ReadAsStringAsync(ct); lock (Requests) Requests.Add(body); }
                    return Js(W($"{{\"task_id\":\"t{++_n}\"}}"));
                case "/query_result": return Js(W($"[{{\"task_id\":\"t{_n}\",\"status\":1,\"result\":\"[{{\\\"file\\\":\\\"/v1/audio?path=c.mp3\\\"}}]\"}}]"));
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("ID3")) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    const int Rate = 22050;
    static void Wav(string f, double seconds, double hz, double amp = 0.2) =>
        AudioFile.WriteWav(new AudioBuf(Rate, [Enumerable.Range(0, (int)(seconds * Rate)).Select(i => (float)(amp * Math.Sin(2 * Math.PI * hz * i / Rate))).ToArray()]), f);

    [AvaloniaFact(DisplayName = "V3/V6: Stimme aus Gesang lernen nur mit Einwilligung (bekannte Person gesperrt, fremde Person braucht Erklärung), zu wenig Gesang abgelehnt, gelernte Stimme in der Bibliothek, Löschen entfernt alles")]
    public async Task Learn_voice_with_consent()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var vocalSeconds = 200.0;
            w.StemSplitOverride = (file, dir) => { Directory.CreateDirectory(dir); Wav(Path.Combine(dir, "gesang.wav"), vocalSeconds, 330); Wav(Path.Combine(dir, "begleitung.wav"), vocalSeconds, 110); foreach (var n in new[] { "schlagzeug", "bass", "rest" }) Wav(Path.Combine(dir, n + ".wav"), vocalSeconds, 60, 0.05); return Task.FromResult(true); };
            var runs = new List<string[]>();
            w.OwnVoiceRunOverride = a => { runs.Add(a); return Task.FromResult(new Cli.Result(0, "{\"ok\": true}", "")); };
            var src = Path.Combine(root, "Mein Gesang.wav"); Wav(src, 2, 330);
            w.Show("musik-studio"); Settle();
            var s = (await w.MsImport(src, split: true))!;
            for (var i = 0; i < 100 && w.MsStemDir(s) is null; i++) { await Task.Delay(20); Settle(); }
            var own = new VoiceBank.Consent("eigen", "", DateTimeOffset.Now, "Das ist meine eigene Stimme.");
            Assert.Null(await w.MsVoiceLearnFromSong(s, "Ich im Studio", own, notFamous: false)); // bekannte Person nicht ausgeschlossen
            Assert.Contains("gesperrt", w.OwnMessage); Assert.Empty(w.Bank.All());
            Assert.Null(await w.MsVoiceLearnFromSong(s, "Erika", new VoiceBank.Consent("person", "Erika Muster", DateTimeOffset.Now, ""), true));
            Assert.Contains("Erklärung", w.OwnMessage); Assert.Empty(w.Bank.All());
            var v = (await w.MsVoiceLearnFromSong(s, "Ich im Studio", own, true))!;
            Assert.True(v.Ready); Assert.Equal("aufnahme", v.Source);
            Assert.Equal(["lernen", v.Id], runs[^1].Take(2));
            Assert.True(Directory.GetFiles(w.Bank.TakesDir(v.Id), "teil*.wav").Length >= VoiceBank.MinSegments);
            Assert.Contains(w.MsVoiceChoices(), c => c.Id == v.Id);
            var p = (await w.MsVoiceLearnFromSong(s, "Erika", new VoiceBank.Consent("person", "Erika Muster", DateTimeOffset.Now, "Erika Muster erlaubt am 07.11.2026 das Lernen ihrer Stimme für eigene Lieder."), true))!;
            Assert.Equal("Erika Muster", w.Bank.Get(p.Id)!.Consent.Person);
            // Bibliothek in der Oberfläche, Löschen
            w.MsDetailTab("spuren"); w.Show("musik-studio"); Settle();
            Assert.NotNull(w.Find<StackPanel>("ms-voices"));
            w.Find<Button>("vb-del-" + p.Id).Press(); Settle();
            Assert.Null(w.Bank.Get(p.Id)); Assert.False(Directory.Exists(w.Bank.Dir(p.Id)));
            // zu wenig Gesang
            vocalSeconds = 20; var s2 = (await w.MsImport(Path.Combine(root, "Mein Gesang.wav"), split: true))!;
            for (var i = 0; i < 100 && w.MsStemDir(s2) is null; i++) { await Task.Delay(20); Settle(); }
            Assert.Null(await w.MsVoiceLearnFromSong(s2, "Kurz", own, true));
            Assert.Contains("Zu wenig Gesang", w.OwnMessage); Assert.Single(w.Bank.All());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V4/V5: neues Lied in der gelernten Stimme (Umwandeln mit der Stimm-Id, neues Lied „(Stimme …)“); neuer eigener Text auf die Begleitung – ACE-Step bekommt Cover-Auftrag mit dem Text")]
    public async Task Sing_and_new_lyrics()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.StemSplitOverride = (file, dir) => { Directory.CreateDirectory(dir); foreach (var n in new[] { "gesang", "begleitung", "schlagzeug", "bass", "rest" }) Wav(Path.Combine(dir, n + ".wav"), 2, 220); return Task.FromResult(true); };
            var runs = new List<string[]>();
            w.OwnVoiceRunOverride = a => { runs.Add(a); if (a[0] == "singen") File.WriteAllBytes(a[3], [1]); return Task.FromResult(new Cli.Result(0, "{\"ok\": true}", "")); };
            w.OwnVoiceMixOverride = (voc, acc, mix) => { File.WriteAllBytes(mix, "ID3"u8.ToArray()); return Task.FromResult(true); };
            var v = w.Bank.Add("Ich im Studio", "aufnahme", new VoiceBank.Consent("eigen", "", DateTimeOffset.Now, "Das ist meine eigene Stimme."), true); w.Bank.MarkReady(v.Id);
            var src = Path.Combine(root, "Lied.wav"); Wav(src, 2, 220);
            w.Show("musik-studio"); Settle();
            var s = (await w.MsImport(src, split: false))!;
            var sung = await w.MsOwnVoiceSing(s, 0, v.Id);
            Assert.NotNull(sung); Assert.Contains("(Stimme Ich im Studio)", sung!.Title);
            Assert.Contains(runs, a => a[0] == "singen" && a[1] == v.Id);
            Assert.Null(await w.MsOwnVoiceSing(s, 0, "stimme-gibt-es-nicht"));
            // V5
            var ace = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace)); w.GpuMemoryOverride = () => null;
            Assert.Empty(await w.MsNewLyricsOnMusic(s, "nur eine Zeile"));
            var made = await w.MsNewLyricsOnMusic(s, "[Strophe]\nMein neuer Text am Morgen\nund noch eine Zeile dazu");
            Assert.Single(made); Assert.Contains("neuer Text", made[0].Title);
            var req = ace.Requests.Last();
            Assert.Contains("Mein neuer Text am Morgen", req); Assert.Contains("cover", req);
            Assert.Equal(w.Songs.All().First(x => x.Kind == "begleitung").Id, made[0].ParentId); // aufgebaut auf der Begleitung
        }
        finally { w.Close(); }
    }
}
