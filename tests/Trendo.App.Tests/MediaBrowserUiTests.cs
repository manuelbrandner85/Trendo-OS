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
        w.MediaFetchOverride = fetch; w.MediaProvider = "commons"; var audio = new FakeAudio(); w.AudioOverride = () => audio;
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
    static string Ov(string id, string title, string lic, string ver, int wd, int ht, string src = "flickr") =>
        "{\"id\":\"" + id + "\",\"title\":\"" + title + "\",\"foreign_landing_url\":\"https://www.flickr.com/photos/x/" + id + "\",\"url\":\"https://live.staticflickr.com/1/" + id + ".jpg\",\"creator\":\"Erika Muster\",\"license\":\"" + lic + "\",\"license_version\":\"" + ver
        + "\",\"license_url\":\"https://creativecommons.org/licenses/" + lic + "/" + ver + "/\",\"source\":\"" + src + "\",\"filetype\":\"jpg\",\"filesize\":1000,\"width\":" + wd + ",\"height\":" + ht + ",\"thumbnail\":\"https://api.openverse.org/v1/images/" + id + "/thumb/\"}";

    [AvaloniaFact(DisplayName = "Medienbrowser wie Openverse/Unsplash/Google Fonts: beide Quellen gemischt, Ausrichtung filtern, Namensnennung als HTML, Bild in der Bildbearbeitung öffnen, Schriften suchen und laden")]
    public async Task Openverse_orientation_fonts()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var ov = "{\"results\":[" + Ov("a1", "Turm", "by", "4.0", 600, 1200) + "," + Ov("a2", "Feld", "cc0", "1.0", 1600, 900) + "," + Ov("a3", "Gesperrt", "by-nc", "4.0", 800, 800) + "]}";
            var fonts = "[{\"id\":\"lora\",\"family\":\"Lora\",\"subsets\":[\"latin\"],\"weights\":[400,700],\"category\":\"serif\",\"license\":\"OFL-1.1\",\"variable\":true},"
                + "{\"id\":\"geheim\",\"family\":\"Geheim Sans\",\"subsets\":[\"latin\"],\"weights\":[400],\"category\":\"sans-serif\",\"license\":\"Proprietär\"}]";
            var fetch = new FakeFetch(url => new WebReply(200,
                url.Contains("api.php") ? Encoding.UTF8.GetBytes(Answer(Hit(2, "See.png", "CC0", "Max Beispiel", 300_000)))
                : url.Contains("api.openverse.org/v1/images/?") ? Encoding.UTF8.GetBytes(ov)
                : url.Contains("api.fontsource.org") ? Encoding.UTF8.GetBytes(fonts)
                : url.EndsWith(".ttf") ? [0, 1, 0, 0, 9, 9, 9, 9]
                : Png));
            w.MediaFetchOverride = fetch; w.Show("assets");
            Assert.Equal(2, w.Find<ComboBox>("mb-provider").SelectedIndex); // Standard: beide Quellen
            w.Find<TextBox>("mb-query").Text = "Turm"; w.Find<Button>("mb-search").Press();
            await Until(() => w.MediaHits.Count == 3);
            Assert.Equal(["Wikimedia Commons", "Openverse · flickr", "Openverse · flickr"], w.MediaHits.Select(h => h.Provider)); // abwechselnd gemischt, CC BY-NC fällt heraus
            Assert.Contains(fetch.Urls, u => u.Contains("license=cc0,pdm,by,by-sa"));
            // Ausrichtung: Hochformat filtert sofort ohne neue Suche
            var before = fetch.Urls.Count(u => u.Contains("api.openverse.org/v1/images/?"));
            w.Find<ComboBox>("mb-orient").SelectedIndex = 2; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(["Turm"], w.MediaHits.Select(h => h.Title)); Assert.Equal(before, fetch.Urls.Count(u => u.Contains("api.openverse.org/v1/images/?")));
            Assert.Contains("CC BY 4.0", ((TextBlock)w.Find<Border>("mb-license-badge").Child!).Text);
            // Namensnennung als HTML mit Links
            w.Find<ComboBox>("mb-credit-fmt").SelectedIndex = 1; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.StartsWith("<a href=\"https://www.flickr.com/photos/x/a1\">", w.Find<SelectableTextBlock>("mb-credit").Text);
            Assert.Contains("über Openverse &#183; flickr", w.Find<SelectableTextBlock>("mb-credit").Text); // HTML-maskiert
            // laden und in der Bildbearbeitung öffnen
            w.Find<Button>("mb-get").Press();
            await Until(() => w.Find<TextBlock>("mb-msg").Text?.Contains("✓") == true);
            Assert.Contains("Quelle: Openverse · flickr", File.ReadAllText(Directory.GetFiles(Path.Combine(w.AssetFolder, "Medien", "Bilder"), "*.herkunft.txt").Single()));
            w.Find<Button>("mb-tab-bibliothek").Press(); w.Find<Button>("mb-lib-edit-0").Press();
            Assert.Equal("bild", w.CurrentView);
            Assert.Single(Trendo.Core.Media.ImageDoc.Layers(w.ProgramActive("bild")!.Data));
            // Schriften: nur offene Lizenzen, Laden mit Lizenzdatei
            w.Show("assets"); w.Find<Button>("mb-tab-schriften").Press();
            w.Find<TextBox>("mb-font-query").Text = ""; w.Find<Button>("mb-font-search").Press();
            await Until(() => w.FontHits.Count == 1);
            Assert.Equal("Lora", w.FontHits[0].Family); Assert.Equal("Mit Serifen", w.FontHits[0].Kategorie);
            w.Find<Button>("mb-font-get-0").Press();
            var dir = Path.Combine(w.AssetFolder, "Medien", "Schriften", "Lora");
            await Until(() => File.Exists(Path.Combine(dir, "LIZENZ.txt")));
            Assert.True(File.Exists(Path.Combine(dir, "Lora-400.ttf")) && File.Exists(Path.Combine(dir, "Lora-700.ttf")));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "medien.schrift");
        }
        finally { w.Close(); }
    }
}
