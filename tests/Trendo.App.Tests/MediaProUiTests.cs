using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Freie Bilder & Klänge – Profi 29 und Claude 28 (05.11.2026): ähnliche Bilder, Merklisten, Schriftvorschau, Claude als Rechercheur. Netz und Claude nachgebildet.</summary>
[Collection("Claude")]
public class MediaPro_Ui
{
    sealed class FakeFetch(Func<string, byte[]> f) : IWebFetch
    {
        public List<string> Urls { get; } = [];
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default) { lock (Urls) Urls.Add(url); return Task.FromResult(new WebReply(200, f(url))); }
    }
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");
    const string Ov = """{"results":[{"id":"a1","title":"Berg im Nebel","url":"https://live.staticflickr.com/a1.jpg","thumbnail":"https://api.openverse.org/v1/images/a1/thumb/","license":"by-sa","license_version":"4.0","license_url":"https://creativecommons.org/licenses/by-sa/4.0/","filetype":"jpg","creator":"Erika Muster","foreign_landing_url":"https://flickr.com/a1","width":800,"height":600},{"id":"a2","title":"See","url":"https://live.staticflickr.com/a2.jpg","license":"cc0","license_version":"1.0","filetype":"jpg","creator":"Max Beispiel","foreign_landing_url":"https://flickr.com/a2"}]}""";
    const string Rel = """{"results":[{"id":"r9","title":"Nebelwald","url":"https://live.staticflickr.com/r9.jpg","license":"cc0","license_version":"1.0","filetype":"jpg","creator":"Erika Muster","foreign_landing_url":"https://flickr.com/r9"}]}""";
    static async Task Until(Func<bool> ok, int ms = 6000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static string J(object o) => JsonSerializer.Serialize(o);

    [AvaloniaFact(DisplayName = "Freie Bilder & Klänge Profi: ähnliche Bilder, Merken in neue Liste, Merklisten-Reiter mit Namensnennungen, Schriftvorschau mit eigenem Text; Claude sucht, prüft Lizenzen, merkt und schreibt die Namensnennung")]
    public async Task Similar_lists_fonts_claude()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        var ttf = File.ReadAllBytes(new[] { @"C:\Windows\Fonts\arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" }.First(File.Exists));
        var fetch = new FakeFetch(u => u.Contains("/related/") ? Encoding.UTF8.GetBytes(Rel) : u.Contains("api.openverse.org/v1/images/?") ? Encoding.UTF8.GetBytes(Ov)
            : u.Contains("/thumb") ? Png : u.Contains("fontsource.org/v1/fonts") ? Encoding.UTF8.GetBytes("""[{"id":"lora","family":"Lora","category":"serif","license":"OFL-1.1","weights":[400,700],"subsets":["latin"]}]""")
            : u.EndsWith(".ttf") ? ttf : Encoding.UTF8.GetBytes("{}"));
        w.MediaFetchOverride = fetch; w.MediaProvider = "openverse";
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe")); MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeRunOverride = (args, _, line, _) =>
        {
            calls.Add(args);
            const string reply = "Für das Sommerfest passen der See (frei) und der Berg (Namensnennung).\n[[trendo:feld | mb-claude-merken | Sommerfest: 1, 2]]";
            line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = reply } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = reply, session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        try
        {
            w.Show("assets");
            await w.MediaSearch("Berg"); await Until(() => w.MediaHits.Count == 2);
            w.Show("assets");
            // ähnliche Bilder
            w.Find<Button>("mb-related").Press(); await Until(() => w.MediaHits.Count == 1 && w.MediaHits[0].Title == "Nebelwald");
            Assert.Contains(fetch.Urls, u => u.Contains("/v1/images/a1/related/"));
            // merken in neue Liste
            w.Show("assets");
            w.Find<TextBox>("mb-collect-new").Text = "Projekt Sommerfest"; w.Find<Button>("mb-collect").Press();
            Assert.Single(w.MediaCollectionsForTests.All()); Assert.Contains("gemerkt", w.Find<TextBlock>("mb-msg").Text);
            w.Find<Button>("mb-tab-merklisten").Press();
            Assert.NotNull(w.TryFind<Border>("mb-list-item-0"));
            w.Find<Button>("mb-list-credits").Press(); await Until(() => w.LastCopied.Contains("Nebelwald"));
            // Schriftvorschau mit eigenem Text
            w.Find<Button>("mb-tab-schriften").Press(); await w.FontSearch("Lora");
            for (var k = 0; k < 100 && w.TryFind<Image>("mb-font-preview-0") is null; k++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
            Assert.True(w.TryFind<Image>("mb-font-preview-0") is not null, "DBG " + string.Join(" | ", w.GetLogicalDescendantsText().Where(t => t.Contains("Vorschau") || t.Contains("Lora") || t.Contains("Schrift"))) + " :: " + string.Join(",", fetch.Urls.Where(u => u.Contains("font"))));
            w.Find<TextBox>("mb-font-sample").Text = "Sommerfest 2026"; w.Find<TextBox>("mb-font-sample").Focus(); w.Find<Button>("mb-font-search").Focus();
            await Until(() => w.TryFind<Image>("mb-font-preview-0") is not null);
            // Claude als Rechercheur
            w.Find<Button>("mb-tab-bilder").Press(); await w.MediaSearch("Berg"); w.Show("assets");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
            await RunC(w.ClaudeCheck(true));
            var state = w.ClaudeReadPage("assets")!;
            Assert.Contains("Rechercheur", state.Title); Assert.Contains("1 · Berg im Nebel · Erika Muster · CC BY-SA 4.0", state.Title);
            w.ClaudeDockToggle("X-07"); w.Show("assets"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (w.Windows.Active?.Id is { } wid && w.ClaudeDockIsMax(wid)) w.Find<Button>("claude-dock-max").Press();
            w.Show("assets");
            await RunC(w.ClaudeChatSend("Such Bilder fürs Sommerfest"));
            Assert.True(w.ClaudePlanPending()); Assert.NotNull(w.TryFind<StackPanel>("claude-plan-media"));
            w.Find<Button>("claude-plan-run").Press(); await RunC(Task.Delay(150));
            var list = w.MediaCollectionsForTests.All().Single(c => c.Name == "Sommerfest");
            Assert.Equal(2, list.Entries.Count);
            Assert.Contains("„Berg im Nebel“ von Erika Muster", w.LastCopied);
            Assert.True(File.Exists(Path.Combine(w.AssetFolder, "Medien", "Namensnennungen – Sommerfest.txt")));
        }
        finally { MainWindow.ClaudeStepDelay = 350; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    static async Task RunC(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
}
