using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 03.11.2026: ein Lied, das gerade entsteht, mit „■ Abbrechen“ stoppen.</summary>
[Collection("Claude")]
public class MusikCancelUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    sealed class SlowAce : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            return Task.FromResult(r.RequestUri!.AbsolutePath switch
            {
                "/health" => J("{\"status\":\"ok\"}"),
                "/release_task" => J("{\"data\":{\"task_id\":\"t1\"},\"code\":200}"),
                "/query_result" => J("{\"data\":[{\"task_id\":\"t1\",\"status\":0}],\"code\":200}"), // wird nie fertig
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
        }
    }

    sealed class ProgressAce : HttpMessageHandler
    {
        public double Progress = 0.42;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            var p = Progress.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Task.FromResult(r.RequestUri!.AbsolutePath switch
            {
                "/health" => J("{\"status\":\"ok\"}"),
                "/release_task" => J("{\"data\":{\"task_id\":\"t1\"},\"code\":200}"),
                "/query_result" => J("{\"data\":[{\"task_id\":\"t1\",\"status\":0,\"result\":\"[{\\\"progress\\\": " + p + ", \\\"stage\\\": \\\"running\\\"}]\"}],\"code\":200}"),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
        }
    }

    [AvaloniaFact(DisplayName = "Fortschritt: Prozent aus der Musik-KI im Text und als Balken, steigt nur an; ohne Angabe läuft der Balken unbestimmt")]
    public async Task Progress_percent()
    {
        var (w, _, _) = Ui.Open();
        var ace = new ProgressAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace));
        try
        {
            w.Show("musik-studio"); await Pump();
            var run = w.MusicStudioCreate(new AceStepClient.Job("pop", "[Refrain]\nla", 30, "de"), "Sommer");
            for (var i = 0; i < 20; i++) await Pump();
            Assert.Contains("42 %", w.Find<TextBlock>("ms-running").Text);
            Assert.False(w.Find<ProgressBar>("ms-running-bar").IsIndeterminate);
            Assert.Equal(42, w.Find<ProgressBar>("ms-running-bar").Value, 3);
            ace.Progress = 0.3; for (var i = 0; i < 10; i++) await Pump(); // kein Rückschritt
            Assert.Contains("42 %", w.Find<TextBlock>("ms-running").Text);
            ace.Progress = 0.87; for (var i = 0; i < 10; i++) await Pump();
            Assert.Contains("87 %", w.Find<TextBlock>("ms-running").Text);
            w.MsCancelCreate("Sommer"); await run;
        }
        finally { w.Close(); }
    }

    [Fact(DisplayName = "Fortschritt lesen: Wert aus dem Ergebnis, sonst aus der Protokollzeile, fertig = 100 %")]
    public void Parse_progress()
    {
        Assert.Equal(0.42, AceStepClient.ParseState("{\"data\":[{\"task_id\":\"t\",\"status\":0,\"result\":\"[{\\\"progress\\\":0.42}]\"}],\"code\":200}", "t").Progress);
        Assert.Equal(0.63, AceStepClient.ParseState("{\"data\":[{\"task_id\":\"t\",\"status\":0,\"progress_text\":\"Diffusion:  63%|######\"}],\"code\":200}", "t").Progress);
        Assert.Null(AceStepClient.ParseState("{\"data\":[{\"task_id\":\"t\",\"status\":0}],\"code\":200}", "t").Progress);
        Assert.Equal(1, AceStepClient.ParseState("{\"data\":[{\"task_id\":\"t\",\"status\":1,\"result\":\"[{\\\"file\\\":\\\"/v1/audio?path=a.mp3\\\"}]\"}],\"code\":200}", "t").Progress);
    }

    [AvaloniaFact(DisplayName = "Abbrechen: voller Grafikspeicher wird gemeldet, hängendes Lied zeigt „■ Abbrechen“, der Klick stoppt es, nichts landet in der Bibliothek, ehrlicher Hinweis")]
    public async Task Cancel_running_song()
    {
        var (w, _, root) = Ui.Open();
        w.AceClientOverride = new AceStepClient(new HttpClient(new SlowAce()));
        w.GpuMemoryOverride = () => (0.4, 12);
        try
        {
            w.Show("musik-studio"); await Pump();
            var run = w.MusicStudioCreate(new AceStepClient.Job("pop", "[Refrain]\nla", 30, "de"), "Sommer");
            for (var i = 0; i < 20; i++) await Pump();
            Assert.False(run.IsCompleted);
            Assert.Contains("„Sommer“ entsteht", w.Find<TextBlock>("ms-running").Text);
            Assert.Contains("Grafikspeicher fast voll (nur 0,4 von 12 GB frei)", w.Find<TextBlock>("ms-msg").Text);
            w.Find<Button>("ms-cancel").Press();
            for (var i = 0; i < 50 && !run.IsCompleted; i++) await Pump();
            Assert.True(run.IsCompleted);
            Assert.Empty(await run);
            Assert.Empty(new SongLibrary(root).All());
            await Pump();
            Assert.Contains("„Sommer“ abgebrochen", w.Find<TextBlock>("ms-msg").Text);
            Assert.DoesNotContain(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<Button>(), b => b.Name == "ms-cancel");
        }
        finally { w.Close(); }
    }
}
