using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0279: 3D-Bibliothek über den echten Eingabeweg – Suche, Vorschau, Laden, Übernahme aus anderen Programmen, Ansehen, Entfernen, Offline-Sperre.</summary>
public class Library3D_Ui
{
    const string BIB = "bib3d";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static async Task Wait(Func<bool> done) { for (var i = 0; i < 300 && !done(); i++) { await Task.Delay(10); Settle(); } }
    static string Msg(MainWindow w) => Try<TextBlock>(w, "bib-msg")?.Text ?? "";

    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    sealed class Fake : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var u = r.RequestUri!; Urls.Add(u.ToString());
            HttpContent c = u.Host == "api.polyhaven.com" && u.AbsolutePath.StartsWith("/files")
                ? new StringContent("""{"gltf":{"1k":{"gltf":{"url":"https://dl.polyhaven.org/file/ph-assets/Models/gltf/1k/wooden_chair/wooden_chair_1k.gltf","include":{}}}}}""")
                : u.Host == "api.polyhaven.com" ? new StringContent("""{"wooden_chair":{"name":"Wooden Chair","tags":["chair"],"categories":["furniture"],"authors":{"Erika Muster":"All"},"dimensions":[450,520,900]},"rock_01":{"name":"Rock 01","tags":["stone"],"categories":["nature"],"authors":{"Max Muster":"All"}}}""")
                : u.Host == "cdn.polyhaven.com" ? new ByteArrayContent(Png) : new ByteArrayContent([1, 2, 3]);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = c });
        }
    }

    [AvaloniaFact(DisplayName = "T-0279 UI: Deutsch suchen → Treffer mit Vorschau, Maßen, Lizenz; Laden legt es in die Bibliothek mit Namensnennung")]
    public async Task Search_download()
    {
        var (w, _, _) = Ui.Open(); var fake = new Fake(); w.BibHttpOverride = fake;
        w.Show(BIB); Settle();
        Get<TextBox>(w, "bib-query").Text = "Stuhl"; Press(w, "bib-search");
        await Wait(() => Msg(w) == "1 Treffer" && w.GetVisualDescendants().OfType<Image>().Any());
        Assert.Single(w.BibHits); Assert.NotNull(Try<Border>(w, "bib-hit-0")); Assert.Null(Try<Border>(w, "bib-hit-1"));
        var hit = Get<Border>(w, "bib-hit-0"); var texts = hit.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Wooden Chair", texts); Assert.Contains("45 × 52 × 90 cm", texts); Assert.Contains("CC0 1.0 · Erika Muster", texts);
        Assert.NotEmpty(hit.GetVisualDescendants().OfType<Image>()); // Vorschaubild geladen
        Assert.DoesNotContain(fake.Urls, x => x.Contains("/files/")); // nichts geladen ohne Klick
        Press(w, "bib-get-0");
        await Wait(() => Msg(w).StartsWith('✓'));
        Assert.Contains("Wooden Chair", Msg(w)); Assert.NotNull(Try<TextBlock>(w, "bib-have-0")); Assert.NotNull(Try<Border>(w, "bib-item-0"));
        Assert.Contains("Wooden Chair – Erika Muster – Poly Haven (CC0 1.0)", Get<TextBox>(w, "bib-credits").Text);
        Press(w, "bib-del-0"); Assert.Null(Try<Border>(w, "bib-item-0")); Assert.Contains("entfernt", Msg(w));
    }

    [AvaloniaFact(DisplayName = "T-0279 UI: Modell aus anderem Programm übernehmen (GLB) und im Modell-Prüfer ansehen; fremdes Format klar abgewiesen")]
    public async Task Import_and_view()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var glb = Trendo.Core.XrSample.Write(Path.Combine(root, "mein-sessel.glb")); var exe = Path.Combine(root, "x.exe"); File.WriteAllText(exe, "x");
        w.Show(BIB); Settle();
        Get<TextBox>(w, "bib-import-path").Text = exe; Press(w, "bib-import");
        await Wait(() => Msg(w).StartsWith('⚠')); Assert.Contains("kann Trendo nicht übernehmen", Msg(w));
        Get<TextBox>(w, "bib-import-path").Text = glb; Press(w, "bib-import");
        await Wait(() => Msg(w).StartsWith('✓'));
        Assert.Contains("mein-sessel", Msg(w)); Assert.Contains("Eigene Datei", Get<Border>(w, "bib-item-0").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Aggregate((a, b) => a + b));
        Press(w, "bib-open-0"); Settle();
        Assert.NotNull(Try<Control>(w, "xr-view")); Assert.Contains("3D-Bibliothek", Get<TextBlock>(w, "xr-msg").Text);
    }

    [AvaloniaFact(DisplayName = "T-0279 UI: Ohne Internet ist Suchen gesperrt und es wird nichts gesendet")]
    public async Task Offline_blocks()
    {
        var (w, _, _) = Ui.Open(offline: true); var fake = new Fake(); w.BibHttpOverride = fake;
        w.Show(BIB); Settle();
        Assert.Contains("Offline", Get<Border>(w, "bib-empty").GetVisualDescendants().OfType<TextBlock>().First().Text);
        Get<TextBox>(w, "bib-query").Text = "Stuhl"; Press(w, "bib-search");
        await Wait(() => Msg(w).StartsWith('⚠'));
        Assert.Contains("Offline", Msg(w)); Assert.Empty(fake.Urls);
    }
}
