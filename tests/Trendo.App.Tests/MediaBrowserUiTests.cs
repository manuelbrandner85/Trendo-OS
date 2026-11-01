using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Runde 7: Freie Bilder & Klänge als Medienbrowser – Raster mit Vorschaubild, Vorschau mit Lizenz und Urheber, Filter, Vorhören, Laden mit Herkunft, Bibliothek. Netz nachgebildet.
public class MediaBrowser_Ui
{
    sealed class FakeFetch(Func<string, WebReply> f) : IWebFetch
    {
        public List<string> Urls { get; } = [];
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default) { Urls.Add(url); return Task.FromResult(f(url)); }
    }
    sealed class FakeAudio : IAudioOut
    {
        public readonly List<string> Loads = [];
        public event Action<double, double>? Progress { add { } remove { } }
        public event Action? Ended { add { } remove { } }
        public void Load(string file) => Loads.Add(file);
        public void SetPause(bool pause) { } public void Seek(double seconds) { } public void SetVolume(int percent) { } public void Stop() { } public void Dispose() { }
    }

    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    static string Hit(int id, string title, string lic, string artist, long size, double dur = 0, bool thumb = true) =>
        "\"" + id + "\":{\"pageid\":" + id + ",\"title\":\"File:" + title + "\",\"index\":" + id + ",\"imageinfo\":[{\"size\":" + size + ",\"width\":640,\"height\":480,\"duration\":" + dur.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + (thumb ? ",\"thumburl\":\"https://upload.wikimedia.org/thumb/" + id + ".png\"" : "")
        + ",\"url\":\"https://upload.wikimedia.org/wikipedia/commons/" + Uri.EscapeDataString(title) + "\",\"descriptionurl\":\"https://commons.wikimedia.org/wiki/File:" + Uri.EscapeDataString(title) + "\""
        + ",\"extmetadata\":{\"LicenseShortName\":{\"value\":\"" + lic + "\"},\"Artist\":{\"value\":\"" + artist + "\"}}}]}";
    static string Answer(params string[] hits) => "{\"query\":{\"pages\":{" + string.Join(",", hits) + "}}}";

    static async Task Until(Func<bool> ok, int ms = 6000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    [AvaloniaFact(DisplayName = "Medienbrowser: Bilder suchen, Raster mit Vorschaubild, Vorschau mit Lizenz und Urheber, nur freie Lizenzen, Laden mit Herkunft, Namensnennung kopieren; Klänge vorhören; Bibliothek")]
    public async Task Media_browser()
    {
        var (w, launcher, _) = Ui.Open();
        var fetch = new FakeFetch(url => new WebReply(200, url.Contains("api.php")
            ? Encoding.UTF8.GetBytes(url.Contains("audio")
                ? Answer(Hit(5, "Regen am Fenster.ogg", "CC0", "Max Beispiel", 40_000, 12.5, thumb: false))
                : Answer(Hit(1, "Berg im Nebel.jpg", "CC BY-SA 4.0", "Erika Muster", 2_000_000), Hit(2, "See.png", "CC0", "Max Beispiel", 300_000), Hit(3, "Gesperrt.jpg", "CC BY-NC 4.0", "X", 1)))
            : url.Contains("/thumb/") ? Png : Encoding.UTF8.GetBytes("DATEI")));
        w.MediaFetchOverride = fetch; var audio = new FakeAudio(); w.AudioOverride = () => audio;
        w.Show("assets");
        Assert.NotNull(w.TryFind<WrapPanel>("mb-tabs"));
        w.Find<TextBox>("mb-query").Text = "Berg"; w.Find<Button>("mb-search").Press();
        await Until(() => w.MediaHits.Count == 2 && w.TryFind<Image>("mb-big") is not null);
        Assert.NotNull(w.TryFind<Button>("mb-hit-1")); Assert.Null(w.TryFind<Button>("mb-hit-2")); // nicht-kommerzielle Lizenz fehlt
        Assert.Contains("Urheber nennen", ((TextBlock)w.Find<Border>("mb-license-badge").Child!).Text);
        Assert.Contains(w.GetLogicalTexts(), t => t == "Urheber: Erika Muster");
        w.Find<Button>("mb-hit-1").Press(); Assert.Contains("frei, ohne Bedingungen", ((TextBlock)w.Find<Border>("mb-license-badge").Child!).Text);
        // Filter: nur Namensnennung
        w.Find<ComboBox>("mb-license").SelectedIndex = 2; w.Find<Button>("mb-search").Press();
        await Until(() => w.MediaHits.Count == 1 && w.MediaHits[0].Title == "Berg im Nebel");
        // laden
        w.Find<Button>("mb-get").Press();
        var file = Path.Combine(w.AssetFolder, "Medien", "Bilder", "Berg_im_Nebel.jpg");
        await Until(() => File.Exists(file + ".herkunft.txt"));
        Assert.Contains("Lizenz: CC BY-SA 4.0", File.ReadAllText(file + ".herkunft.txt"));
        Assert.Contains("✓", w.Find<TextBlock>("mb-msg").Text);
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "medien.download");
        w.Find<Button>("mb-credit-copy").Press(); await Until(() => w.LastCopied.Contains("von Erika Muster, CC BY-SA 4.0"));
        Assert.Equal("assets", w.CurrentView);
        // Klänge: vorhören
        w.Find<Button>("mb-tab-klaenge").Press(); Assert.Empty(w.MediaHits);
        Assert.Equal(2, w.Find<ComboBox>("mb-license").SelectedIndex); // Lizenzfilter bleibt beim Reiterwechsel
        w.Find<ComboBox>("mb-license").SelectedIndex = 0;
        w.Find<TextBox>("mb-query").Text = "Regen"; w.Find<Button>("mb-search").Press();
        await Until(() => w.MediaHits.Count == 1 && w.TryFind<Button>("mb-listen") is not null);
        Assert.Contains("0:12", string.Join("|", w.GetLogicalTexts()));
        w.Find<Button>("mb-listen").Press();
        await Until(() => audio.Loads.Count == 1);
        Assert.EndsWith(".ogg", audio.Loads[0]); Assert.Equal("DATEI", File.ReadAllText(audio.Loads[0]));
        // Bibliothek
        w.Find<Button>("mb-tab-bibliothek").Press();
        w.Find<Button>("mb-lib-0").Press(); Assert.Contains(launcher.Opened, o => o == file);
        // 3D & Texturen (Poly Haven) bleibt erreichbar
        w.Find<Button>("mb-tab-3d").Press(); Assert.NotNull(w.TryFind<Button>("asset-search"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Medienbrowser: Testmodus ohne nachgebildetes Netz sucht nicht im Internet")]
    public async Task No_network_in_test_mode()
    {
        var (w, _, _) = Ui.Open();
        w.Show("assets"); await w.MediaSearch("Berg");
        Assert.Empty(w.MediaHits); Assert.Contains("Offline", w.Find<TextBlock>("mb-msg").Text);
        w.Close();
    }
}
