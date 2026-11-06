using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Bibliothek;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Freie 3D-Modelle aus weiteren Quellen (Q1–Q4) in der 3D-Bibliothek: Quellen-Leiste, eigener Schlüssel, NASA-Suche und Laden,
/// andere Seiten mit Download-Übernahme, Lizenz-Filter, „Ins 3D-Studio“ und „In den 3D-Druck“.</summary>
public class Library3DPro_Ui
{
    static void Pump() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    sealed class Fake : HttpMessageHandler
    {
        public List<string> Urls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var u = r.RequestUri!.AbsoluteUri; Urls.Add(u);
            string? body = u.StartsWith("https://api.github.com/repos/nasa/") ? """{"tree":[{"path":"3D Models/Mars Rover/rover.stl"}]}"""
                : u.StartsWith("https://raw.githubusercontent.com/nasa/") ? "solid rover\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 10 0 0\nvertex 0 10 0\nendloop\nendfacet\nendsolid rover\n" : null;
            return Task.FromResult(body is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) });
        }
    }
    sealed class NoFetch : Trendo.Core.IWebFetch { public Task<Trendo.Core.WebReply> GetAsync(string url, CancellationToken ct = default) => Task.FromResult(new Trendo.Core.WebReply(404, [])); }
    static async Task Wait(MainWindow w) { for (var i = 0; i < 200 && w.BibBusy; i++) { Pump(); await Task.Delay(20); } Pump(); }

    [AvaloniaFact(DisplayName = "CORE-05 Q1–Q4: Quellen-Leiste, Schlüssel geschützt speichern, NASA suchen und laden, in den 3D-Druck und ins 3D-Studio übernehmen, andere Seiten über den Download-Ordner, Filter „nur kommerziell“")]
    public async Task Freie_quellen()
    {
        var (w, _, root) = Ui.Open(); var fake = new Fake(); w.BibHttpOverride = fake;
        try
        {
            w.Show("bib3d"); Pump();
            Assert.NotNull(w.TryFind<WrapPanel>("bib-sources"));
            // Thingiverse: Schlüssel nötig, wird geschützt gespeichert
            w.Find<Button>("bib-src-thingiverse").Press(); Pump();
            w.Find<TextBox>("bib-key").Text = "tok-123"; w.Find<Button>("bib-key-save").Press(); Pump();
            Assert.Equal("tok-123", w.Core.Secrets.Get("thingiverse-schluessel"));
            Assert.DoesNotContain("tok-123", string.Join("|", Directory.GetFiles(w.Core.DataRoot, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText)));
            // NASA: ohne Schlüssel suchen und laden
            w.Find<Button>("bib-src-nasa").Press(); Pump();
            w.BibQuery = "rover"; await w.BibSearch(); await Wait(w);
            var hit = Assert.Single(w.BibHits); Assert.Equal("Mars Rover", hit.Name);
            await w.BibDownload(hit); await Wait(w);
            var lib = new AssetLibrary(new NoFetch(), Path.Combine(w.Core.DataRoot, "bibliothek3d"));
            Assert.Contains(lib.Items(), i => i.Source == FreeModels.Nasa && i.File.EndsWith("rover.stl"));
            w.Show("bib3d"); Pump();
            w.Find<Button>("bib-toprint-0").Press(); Pump();
            Assert.EndsWith("rover.stl", w.PrintFile);
            w.Show("bib3d"); Pump();
            w.Find<Button>("bib-to3d-0").Press(); Pump();
            Assert.Equal("3d", w.CurrentView); Assert.NotEmpty(w.Studio3DParts);
            // Filter „nur kommerziell“: NASA ist gemeinfrei und bleibt
            w.Show("bib3d"); Pump(); w.BibCommercialOnly = true; await w.BibSearch(); await Wait(w); Assert.Single(w.BibHits);
            // Andere Seiten: Download-Ordner
            var dl = Path.Combine(root, "Downloads"); Directory.CreateDirectory(dl); w.DownloadsOverride = dl;
            w.BibSource = MainWindow.BibOtherSites; w.Show("bib3d"); Pump();
            Assert.NotNull(w.TryFind<StackPanel>("bib-pickup"));
            File.WriteAllText(Path.Combine(dl, "vase.stl"), "solid vase\nendsolid vase\n");
            w.Find<Button>("bib-pick-refresh").Press(); Pump();
            w.Find<ComboBox>("bib-pick-lic-0").SelectedIndex = 0; w.Find<TextBox>("bib-pick-author-0").Text = "Erika Muster";
            w.Find<Button>("bib-pick-0").Press(); Pump();
            Assert.Contains(lib.Items(), i => i.Name == "vase" && i.License == "CC0 1.0" && i.Author == "Erika Muster" && i.Source == "Printables");
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "bibliothek3d.download");
        }
        finally { w.BibHttpOverride = null; w.DownloadsOverride = null; w.Close(); }
    }
}
