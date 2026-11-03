using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik M10: Verlängern mit dem Grundmodell (am 03.11.2026 echt geprüft); ohne Grundmodell erst „Grundmodell laden“.</summary>
[Collection("Claude")]
public class MusikExtendUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    sealed class FakeAce : HttpMessageHandler
    {
        public readonly List<string> Bodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return J("{\"status\":\"ok\"}");
                case "/release_task": var b = await r.Content!.ReadAsStringAsync(ct); lock (Bodies) Bodies.Add(b); return J("{\"data\":{\"task_id\":\"t1\"},\"code\":200}");
                case "/query_result": return J("{\"data\":[{\"task_id\":\"t1\",\"status\":1,\"result\":\"[{\\\"file\\\":\\\"/v1/audio?path=a.mp3\\\"}]\"}],\"code\":200}");
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3"u8.ToArray()) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    static MusicSong Song(string root)
    {
        var f = Path.Combine(root, "lied.mp3"); File.WriteAllBytes(f, "ID3"u8.ToArray());
        return new SongLibrary(root).Add(new MusicSong { Title = "Sommer", Style = "pop, female vocals", Lyrics = "[Strophe]\nDie Straße glänzt\n[Refrain]\nSommer nimm uns mit", Seconds = 30, File = f });
    }

    [AvaloniaFact(DisplayName = "M10 Verlängern: Abschnitt ab dem Ende (30 → 90 s) mit dem Grundmodell, Refrain kommt dazu, neues Lied „– länger“")]
    public async Task Extend_song()
    {
        var (w, _, root) = Ui.Open();
        var ace = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace)); w.AceBaseReadyOverride = true;
        try
        {
            var s = Song(root); w.MusicStudioSelect(s.Id); w.Show("musik-studio"); await Pump();
            Assert.Equal("um 1 Minute", w.Find<ComboBox>("ms-extend-by").SelectedItem);
            var made = await w.MsExtend(s.Id, 60); await Pump();
            var body = ace.Bodies.Single();
            Assert.Contains("repaint", body); Assert.Contains("30.0", body); Assert.Contains("90.0", body);
            Assert.Contains("name=audio_duration", body); Assert.Contains("\r\n\r\n90\r\n", body);
            Assert.Contains("Sommer nimm uns mit\n\n[Refrain]\nSommer nimm uns mit", body.Replace("\r\n", "\n"));
            Assert.Equal(AceStepClient.BaseModel, w.AceModel);
            var n = new SongLibrary(root).Get(made.Single().Id)!;
            Assert.Equal(("laenger", 90.0, "Sommer – länger"), (n.Kind, n.Seconds, n.Title));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M10 ohne Grundmodell: Hinweis auf das Grundmodell und „Grundmodell laden“ über das Download-Werkzeug von ACE-Step")]
    public async Task Base_model_setup()
    {
        var (w, _, root) = Ui.Open();
        var calls = new List<string>();
        w.AceBaseReadyOverride = false;
        w.KiStepOverride = (exe, args, cwd, env) => { calls.Add(string.Join(" ", args)); return Task.FromResult(new Cli.Result(0, "ok", "")); };
        try
        {
            var s = Song(root); w.MusicStudioSelect(s.Id); w.Show("musik-studio"); await Pump();
            Assert.DoesNotContain(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<Button>(), b => b.Name == "ms-extend");
            Assert.Contains("Grundmodell von ACE-Step", string.Join("|", w.GetLogicalTexts()));
            w.Find<Button>("ms-extend-setup").Press();
            for (var i = 0; i < 50 && calls.Count == 0; i++) await Pump();
            Assert.Contains("download_submodel('acestep-v15-base'", calls.Single());
        }
        finally { w.KiStepOverride = null; w.Close(); }
    }
}
