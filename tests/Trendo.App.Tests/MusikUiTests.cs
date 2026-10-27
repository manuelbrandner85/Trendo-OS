using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Musik-KI (Stufe 2): Stil und Liedtext eingeben, „Musik erzeugen“, Lied landet in „KI-Ergebnisse“.</summary>
public class MusikUiTests
{
    sealed class FakeAce : HttpMessageHandler
    {
        public string? Request; int _q;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            static string W(string d) => "{\"data\":" + d + ",\"code\":200,\"error\":null}";
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return J("{\"status\":\"ok\"}");
                case "/release_task": Request = await r.Content!.ReadAsStringAsync(ct); return J(W("{\"task_id\":\"a\"}"));
                case "/query_result":
                    return J(W(++_q < 2 ? "[{\"task_id\":\"a\",\"status\":0}]" : "[{\"task_id\":\"a\",\"status\":1,\"result\":\"[{\\\"file\\\":\\\"/v1/audio?path=x.mp3\\\"}]\"}]"));
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("ID3")) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    [AvaloniaFact(DisplayName = "Musik-KI (UI): Stil + Liedtext → Lied gespeichert, Anhören-Knopf, Audit-Eintrag")]
    public async Task Generate_song()
    {
        var (w, _, root) = Ui.Open();
        var m = KiLibrary.Models.Single(x => x.Kind == KiKind.Musik);
        Assert.True(m.Ready);
        var exe = AceStepClient.ServerCommand(w.KiStore.RuntimeDir(KiRuntime.AceStep)).File;
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!); File.WriteAllText(exe, "MZ");
        Assert.True(w.KiStore.ModelReady(m));
        var fake = new FakeAce();
        w.AceClientOverride = new AceStepClient(new HttpClient(fake));
        w.KiLibraryShow(KiKind.Musik, m.Id);
        w.Find<TextBox>("kilib-prompt").Text = "fröhlicher Pop mit Klavier";
        w.Find<TextBox>("kilib-lyrics").Text = "[Verse]\nHeute ist ein schöner Tag";
        w.Find<Button>("kilib-run").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var i = 0; i < 200 && w.TryFind<Button>("kilib-play-result") is null; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.NotNull(w.TryFind<Button>("kilib-play-result"));
        Assert.Contains("Pop mit Klavier", fake.Request);
        Assert.Contains("\"vocal_language\":\"de\"", fake.Request);
        var song = Directory.GetFiles(root, "musik-*.mp3", SearchOption.AllDirectories).Single();
        Assert.Equal("ID3", File.ReadAllText(song));
        Assert.Contains("ki.musik.done", File.ReadAllText(Path.Combine(root, "audit", "audit.jsonl")));
        w.Close();
    }

    [Fact(DisplayName = "Musik-KI: Server nur auf 127.0.0.1, Start ohne Batch-Datei (keine Updateprüfung, keine Rückfragen)")]
    public void Server_command()
    {
        var (file, args) = AceStepClient.ServerCommand(@"C:\x\acestep");
        Assert.EndsWith(Path.Combine("python_embeded", "python.exe"), file);
        Assert.Equal("acestep/api_server.py", args[0]);
        Assert.Equal("127.0.0.1", args[Array.IndexOf(args, "--host") + 1]);
        Assert.DoesNotContain(args, a => a.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));
    }
}
