using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class T0225_KiUi
{
    sealed class RemoteFake(string name, bool up, string answer = "") : Fake(name, up, answer), IRemoteKiBackend;

    class Fake(string name, bool up, string answer = "") : IKiBackend
    {
        public string Name => name; public string BaseUrl => "https://example";
        public List<string> Asked = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(up);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(up ? ["m"] : []);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Asked.Add(messages[^1].Content); return Task.FromResult(answer); }
    }

    [AvaloniaFact(DisplayName = "T-0225 UI: Schlüssel holen öffnet die Anbieterseite, Ersatz-KI erst mit gültigem Schlüssel, springt nur ein, wenn die lokale KI schweigt")]
    public async Task Cloud_key_and_fallback()
    {
        var (w, l, root) = Ui.Open();
        w.Show("keys");
        var use = w.Find<CheckBox>("ki-cloud-use-mistral");
        Assert.False(use.IsEnabled); // ohne Schlüssel nicht einschaltbar
        w.Find<Button>("btn-ki-getkey-mistral").Press();
        Assert.Contains("https://console.mistral.ai/api-keys", l.Opened);
        w.KeyValidatorOverride = k => Task.FromResult(k == "gut");
        w.Find<TextBox>("txt-ki-mistral").Text = "schlecht"; w.Find<Button>("btn-ki-savekey-mistral").Press(); await Task.Delay(50);
        Assert.Null(w.Core.Secrets.Get("TrendoOS:ki-mistral"));
        w.Find<TextBox>("txt-ki-mistral").Text = "gut"; w.Find<Button>("btn-ki-savekey-mistral").Press(); await Task.Delay(50);
        Assert.Equal("gut", w.Core.Secrets.Get("TrendoOS:ki-mistral"));
        Assert.True(use.IsEnabled);
        use.IsChecked = true;
        Assert.Equal(["mistral"], KiSourceSettings.Load(root).Cloud);
        Assert.DoesNotContain(File.ReadAllText(Path.Combine(root, "audit", "audit.jsonl")), "\"gut\"");

        var local = new Fake("Ollama", up: true, "lokal");
        var cloud = new RemoteFake("Mistral AI", up: true, "aus der Cloud");
        w.KiBackendFactory = _ => local; w.CloudBackendFactory = (svc, key) => { Assert.Equal(("mistral", "gut"), (svc.Id, key)); return cloud; };
        Assert.Equal("lokal", (await w.Intel.AskAsync(new KiRequest("Hallo"))).Text);
        Assert.Empty(cloud.Asked); // lokale KI zuerst
        var down = new Fake("Ollama", up: false);
        w.KiBackendFactory = _ => down;
        Assert.False((await w.Intel.AskAsync(new KiRequest("Inhalt einer Datei …"))).Ok); // Abläufe, Mail, Agenten: nie nach außen
        Assert.Empty(cloud.Asked);
        var a = await w.Intel.AskAsync(new KiRequest("Wie spät?", History: [new("user", "alte Frage"), new("assistant", "alte Antwort mit Wissen")], AllowRemote: true));
        Assert.Equal(("aus der Cloud", "Mistral AI"), (a.Text, a.Backend));
        Assert.Equal(["Wie spät?"], cloud.Asked);
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "approval.requested" && e.Detail.EndsWith("ki.cloud.on mistral"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0225 UI: Freie Modelle werden ohne Klick nacheinander geladen, offline oder ausgeschaltet nichts")]
    public async Task Auto_models()
    {
        var (w, _, root) = Ui.Open();
        var pulled = new List<string>();
        w.FreeBytesForModels = () => 2L * 1024 * 1024 * 1024; // 2 GB frei: nichts laden
        Assert.Empty(await w.AutoInstallModelsAsync(() => Task.FromResult<IReadOnlyList<string>>([]), _ => throw new InvalidOperationException("zu wenig Platz")));
        w.FreeBytesForModels = () => long.MaxValue;
        var got = await w.AutoInstallModelsAsync(() => Task.FromResult<IReadOnlyList<string>>(["qwen2.5:0.5b"]), t => { pulled.Add(t); return Task.FromResult(true); });
        Assert.Equal(pulled, got);
        Assert.DoesNotContain("qwen2.5:0.5b", pulled);
        Assert.All(pulled, t => Assert.True(KiCatalog.AutoLicense(KiCatalog.Models.First(m => m.Tag == t))));
        Assert.Equal(pulled.Count, w.Core.Audit.ReadAll().Count(e => e.Action == "ki.model.auto.ok"));
        Assert.Equal(pulled.Count, w.Core.Audit.ReadAll().Count(e => e.Action == "approval.requested" && e.Detail.Contains(" ki.pull ")));
        // Fehlschläge: nach drei Versuchen nur noch per Knopf
        for (var i = 0; i < 4; i++) await w.AutoInstallModelsAsync(() => Task.FromResult<IReadOnlyList<string>>(["qwen2.5:0.5b"]), _ => Task.FromResult(false));
        Assert.All(KiSourceSettings.Load(root).AutoFailed.Values, n => Assert.Equal(3, n));
        var s = KiSourceSettings.Load(root); s.AutoModels = false; s.Save(root);
        Assert.Empty(await w.AutoInstallModelsAsync(() => Task.FromResult<IReadOnlyList<string>>([]), _ => throw new InvalidOperationException("darf nicht laden")));
        w.Close();
        var (o, _, _) = Ui.Open(offline: true);
        Assert.Empty(await o.AutoInstallModelsAsync(() => Task.FromResult<IReadOnlyList<string>>([]), _ => throw new InvalidOperationException("offline")));
        o.Close();
    }

    [AvaloniaFact(DisplayName = "T-0225: KI-Dienst nur über HTTPS und nur an die bekannte Adresse")]
    public void Cloud_backend_guard()
    {
        var svc = KiCatalog.Cloud.First(c => c.Id == "mistral");
        Assert.Throws<InvalidOperationException>(() => new CloudKiBackend(svc with { BaseUrl = "http://api.mistral.ai/v1" }, "k"));
        Assert.Throws<InvalidOperationException>(() => new CloudKiBackend(svc with { BaseUrl = "https://boese.example/v1" }, "k"));
        Assert.Throws<ArgumentException>(() => new CloudKiBackend(svc, " "));
        Assert.Equal("Mistral AI", new CloudKiBackend(svc, "k").Name);
    }
}
