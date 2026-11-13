using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Claude 26 (05.11.2026): Claude als Produzent in Trendo Musik – sieht Lied und Prüfung, Plan zeigt Rhythmus vorher/nachher, erst „Ausführen“ übernimmt:
/// Text und Stil als Entwurf einer neuen Fassung, „Erstellen“ hängt sie ans Original (A/B-Vergleich), Mastering-Ziel mastert. Claude und ACE-Step sind nachgebildet.
[Collection("Claude")]
public class ClaudeMusik_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    const string Measure = "{\n \"input_i\" : \"-13.40\", \"input_tp\" : \"-0.70\", \"input_lra\" : \"15.70\", \"input_thresh\" : \"-24.00\", \"target_offset\" : \"0.20\"\n}";

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

    [AvaloniaFact(DisplayName = "STUDIO-MUSIK-01: Claude als Produzent – Briefing mit Silben und Reimen, Plan mit Rhythmus vorher/nachher, Ausführen füllt den Entwurf, Erstellen hängt die Fassung ans Original, Mastering-Ziel „laut“")]
    public async Task Claude_produziert()
    {
        var (w, _, root) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>(); var master = new List<string[]>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeStepDelay = 0;
        const string text = "{Strophe} / Die Straße glänzt im Licht / wir halten heute nicht / {Refrain} / Sommer nimm uns mit / weit im gleichen Schritt";
        MainWindow.ClaudeRunOverride = (args, _, line, _) =>
        {
            calls.Add(args);
            var reply = $"Als Produzent: Zeile 3 ist zu lang, ein Refrain fehlt, für Streaming lauter mastern.\n[[trendo:feld | ms-prod-lyrics | {text}]]\n[[trendo:feld | ms-prod-style | Indie-Pop, Akustikgitarre, warme Frauenstimme, 110 BPM]]\n[[trendo:feld | ms-prod-master | laut]]\n[[trendo:feld | ms-prod-master | superlaut]]";
            line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = reply } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = reply, session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        var fake = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(fake));
        w.MasterRunOverride = a =>
        {
            master.Add(a);
            if (a.Contains("null")) return Task.FromResult(new Cli.Result(0, "", "… loudnorm\n" + Measure));
            File.WriteAllBytes(a[^1], new byte[4000]); return Task.FromResult(new Cli.Result(0, "", ""));
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            var lib = new SongLibrary(w.Core.DataRoot);
            var f = Path.Combine(root, "roadtrip.mp3"); File.WriteAllText(f, "ID3");
            var orig = lib.Add(new MusicSong { Title = "Roadtrip", Style = "Pop", Seconds = 120, Bpm = 110, Key = "A minor", File = f,
                Lyrics = "[Strophe]\nDie Straße glänzt im Licht\nwir halten heute nicht\nund fahren immer weiter durch die ganze lange Nacht bis morgen früh" });
            w.MusicStudioSelect(orig.Id); w.Show("musik-studio");
            var state = w.ClaudeReadPage("musik-studio")!;
            Assert.Contains(state.Fields, x => x.Name == "ms-prod-lyrics" && x.Value.Contains("Die Straße glänzt im Licht / wir halten"));
            Assert.Contains("Silben zu viel", state.Title); Assert.Contains("Kein Refrain", state.Title); // Prüfung für Claude
            w.ClaudeDockToggle("STUDIO-MUSIK-01"); w.Show("musik-studio"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (w.Windows.Active?.Id is { } wid && w.ClaudeDockIsMax(wid)) w.Find<Button>("claude-dock-max").Press();
            w.Show("musik-studio");
            await Run(w.ClaudeChatSend("Mach das als Produzent besser"));
            Assert.Contains("ms-prod-master", calls[^1][1]);
            Assert.True(w.ClaudePlanPending());
            Assert.Contains(w.GetLogicalDescendantsText(), t => t.StartsWith("Nachher:") && t.Contains("Rhythmus gleichmäßig"));
            Assert.Contains(w.GetLogicalDescendantsText(), t => t.StartsWith("So nicht möglich") && t.Contains("superlaut"));
            Assert.Null(w.MsProducerParent); Assert.Empty(master); // noch nichts passiert
            w.Find<Button>("claude-plan-run").Press(); await Run(Task.Delay(150));
            Assert.Equal(orig.Id, w.MsProducerParent);
            Assert.NotNull(w.TryFind<Border>("ms-prod-banner"));
            Assert.Contains("Indie-Pop", w.Find<TextBox>("ms-style").Text); Assert.Contains("Sommer nimm uns mit", w.Find<TextBox>("ms-lyrics").Text);
            Assert.Equal(2, master.Count); Assert.Contains("I=-9", string.Join(" ", master[0])); // Mastering-Ziel laut
            Assert.Equal("laut", lib.Get(orig.Id)!.MasterTarget);
            Assert.Contains("Nicht möglich", w.ClaudeChatCurrent()!.Messages[^1].Text);
            // Erstellen: neue Fassung hängt am Original → Fassungen vergleichen
            w.Find<Button>("ms-create").Press();
            for (var i = 0; i < 300 && !lib.Visible().Any(s => s.Kind == "produzent"); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
            var made = lib.Visible().Single(s => s.Kind == "produzent");
            Assert.Equal(orig.Id, made.ParentId); Assert.Equal("Roadtrip – Produzenten-Fassung", made.Title.Split(" (")[0]);
            Assert.Contains("Indie-Pop", fake.Requests[^1]);
            Assert.Null(w.MsProducerParent);
            Assert.Equal(2, SongFollow.Versions(lib.All(), orig).Count);
            Assert.Equal("Produzenten-Fassung", SongFollow.KindLabel(made.Kind));
        }
        finally { MainWindow.ClaudeStepDelay = 350; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
