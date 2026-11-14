using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kie;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>kie.ai in jedem Studio (Freigabe 13.11.2026): Schlüssel je Person, Kosten vorher, Ergebnis lokal mit Nachweis, Claude fragt vor jedem Auftrag.</summary>
public class Kie_Ui
{
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    sealed class Fake : IWebSend
    {
        public readonly List<(string Method, string Url, IReadOnlyDictionary<string, string> Head)> Calls = [];
        public int Creates;
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            Calls.Add((method, url, headers));
            string json = url switch
            {
                var u when u.EndsWith("/chat/credit") => "{\"code\":200,\"data\":500}",
                var u when u.Contains("file-base64-upload") => "{\"code\":200,\"data\":{\"downloadUrl\":\"https://tempfile.redpandaai.co/up.png\"}}",
                var u when u.EndsWith("/jobs/createTask") => Interlocked.Increment(ref Creates) > 0 ? "{\"code\":200,\"data\":{\"taskId\":\"t9\"}}" : "",
                var u when u.Contains("recordInfo") => "{\"code\":200,\"data\":{\"state\":\"success\",\"resultJson\":\"{\\\"resultUrls\\\":[\\\"https://tempfile.aiquickdraw.com/bild.png\\\"]}\",\"creditsConsumed\":4}}",
                var u when u.StartsWith("https://tempfile.aiquickdraw.com/") => "\u0089PNG",
                _ => "{\"code\":404}",
            };
            if (json == "\u0089PNG") return Task.FromResult(new WebReply(200, Png));
            return Task.FromResult(new WebReply(200, Encoding.UTF8.GetBytes(json)));
        }
    }

    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }

    [AvaloniaFact(DisplayName = "kie.ai: Knopf in jeder Studio-Leiste, ohne Schlüssel nur Einrichten, nichts im Netz")]
    public void Bar_button_everywhere()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var fake = new Fake(); w.KieWebOverride = fake;
            foreach (var page in new[] { "bild-ki-programm", "film-programm", "podcast-programm" })
            {
                w.Show(page); Settle();
                Assert.Contains(w.GetVisualDescendants().OfType<Button>(), b => b.Name == "auftrag-kie");
            }
            var host = new Window { Content = w.KiePanelForTest("STUDIO-MUSIK-01"), Width = 600, Height = 700 }; host.Show(); Settle();
            Assert.Contains(host.GetVisualDescendants().OfType<TextBox>(), t => t.Name == "kie-key" && t.PasswordChar != default);
            Assert.Empty(fake.Calls);
            host.Close();
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "kie.ai: Verbinden prüft den Schlüssel, Auftrag lädt hoch, holt das Ergebnis ohne Schlüssel und schreibt den Herkunftsnachweis")]
    public async Task Connect_and_run()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var fake = new Fake(); w.KieWebOverride = fake; w.KieDelayOverride = (_, _) => Task.CompletedTask;
            Assert.False(await Run(w.KieConnect("kurz")));
            Assert.Empty(fake.Calls);
            Assert.True(await Run(w.KieConnect("kie-schluessel-1234567")));
            Assert.True(w.KieOn);
            var host = new Window { Content = w.KiePanelForTest("STUDIO-MUSIK-01"), Width = 600, Height = 800 }; host.Show(); Settle();
            Assert.Contains(host.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "kie-balance" && t.Text!.Contains("500"));
            Assert.Contains(host.GetVisualDescendants().OfType<TextBox>(), t => t.Name == "kie-style"); // Musik steht im Musikstudio zuerst
            host.Close();

            var img = Path.Combine(root, "eigen.png"); File.WriteAllBytes(img, [1, 2, 3]);
            var job = await Run(w.KieRun("STUDIO-IMAGEAI-01", KieModels.Find("google/nano-banana-edit")!, new KieInput(Prompt: "Himmel abends"), [img], null, false));
            Assert.Null(job.Error);
            var file = Assert.Single(job.Files);
            Assert.Equal(Png, File.ReadAllBytes(file));
            Assert.Contains(job.Id, Path.GetFileName(file)); // eindeutiger Name je Auftrag
            var prov = JsonNode.Parse(File.ReadAllText(file + ".json"))!;
            Assert.Equal("t9", (string?)prov["TaskId"]); Assert.True((bool)prov["AiGenerated"]!);
            Assert.DoesNotContain("kie-schluessel", File.ReadAllText(file + ".json"));
            var download = fake.Calls.Single(c => c.Url.StartsWith("https://tempfile.aiquickdraw.com/"));
            Assert.False(download.Head.ContainsKey("Authorization")); // Schlüssel nur an kie.ai
            Assert.All(fake.Calls.Where(c => c.Url.StartsWith(KieClient.Api) || c.Url.StartsWith(KieClient.UploadHost)), c => Assert.Equal("Bearer kie-schluessel-1234567", c.Head["Authorization"]));

            var before = fake.Creates;
            var blocked = await Run(w.KieRun("STUDIO-FILM-01", KieModels.Find("kling/ai-avatar-standard")!, new KieInput(), [img], img, consent: false));
            Assert.Contains("bestätige", blocked.Error);
            var notLocal = await Run(w.KieRun("STUDIO-IMAGEAI-01", KieModels.Find("recraft/remove-background")!, new KieInput(), [@"\\rechner\freigabe\a.png"], null, false));
            Assert.NotNull(notLocal.Error);
            Assert.Equal(before, fake.Creates); // nichts angelegt, nichts berechnet

            w.KieDisconnect();
            Assert.False(w.KieOn);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "kie.ai für Claude: Status ohne Schlüssel, Rückfrage mit Kosten vor jedem Auftrag, Nein erzeugt nichts")]
    public async Task Claude_asks_first()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var fake = new Fake(); w.KieWebOverride = fake; w.KieDelayOverride = (_, _) => Task.CompletedTask;
            var folder = Path.Combine(root, "claude"); Directory.CreateDirectory(folder);
            var m = w.ClaudeBridgeForTest(folder, "bild-ki-programm");
            async Task<JsonObject> Call(string tool, JsonObject? args = null) =>
                (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
            static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
            Assert.Contains("\"eingerichtet\":false", Text(await Call("kie_status")));
            Assert.Contains("nicht eingerichtet", Text(await Call("kie_erzeugen", new JsonObject { ["modell"] = "google/nano-banana", ["beschreibung"] = "Haus" })));

            await Run(w.KieConnect("kie-schluessel-1234567"));
            var asked = new List<string>();
            w.ClaudeConsentOverride = what => { asked.Add(what); return Task.FromResult(false); };
            var no = await Call("kie_erzeugen", new JsonObject { ["modell"] = "google/nano-banana", ["beschreibung"] = "Leuchtturm" });
            Assert.True((bool)no["isError"]!);
            var question = Assert.Single(asked);
            Assert.Contains("Credits", question); Assert.Contains("Leuchtturm", question); // Kosten und Beschreibung stehen in der Rückfrage
            Assert.Equal(0, fake.Creates);

            w.ClaudeConsentOverride = _ => Task.FromResult(true);
            var yes = await Call("kie_erzeugen", new JsonObject { ["modell"] = "google/nano-banana", ["beschreibung"] = "Leuchtturm" });
            var auftrag = (string)JsonNode.Parse(Text(yes))!["auftrag"]!;
            Assert.Contains(".png", Text(await Call("kie_ergebnis", new JsonObject { ["auftrag"] = auftrag })));
            Assert.Equal(1, fake.Creates);
            var bad = await Call("kie_erzeugen", new JsonObject { ["modell"] = "ai-music-api/generate", ["titel"] = "x", ["stil"] = "klingt wie ein bekannter Star" });
            Assert.True((bool)bad["isError"]!);
            Assert.Equal(1, fake.Creates);
        }
        finally { w.Close(); }
    }
}
