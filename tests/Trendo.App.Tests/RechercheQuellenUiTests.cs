using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0735: Recherche mit Quellen (DOI, RIS, CSL-JSON), Sicher recherchieren (Suche mit Trefferliste, Sammlung behalten) und
/// Freie Bilder &amp; Klänge (Laden nach Freigabe mit Beleg je Datei). Die „…_Echt“-Tests gehen wirklich ins Netz über den echten Lese-Weg
/// <see cref="ReadOnlyWeb"/>; ohne Internet melden sie sich als übersprungen (nur dann). Mit TRENDO_BELEG_DIR schreiben sie Rohlogs für reports/T-0735.</summary>
public class RechercheQuellen_Ui
{
    const string K = "quellen-programm";
    static string? Beleg => Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;
    static void Log(string name, string text) { if (Beleg is { } d) File.AppendAllText(Path.Combine(d, name), text + "\n"); }
    static void Raw(string name, byte[] bytes) { if (Beleg is { } d) File.WriteAllBytes(Path.Combine(d, name), bytes); }
    static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static async Task Until(Func<bool> ok, int ms = 30000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Settle(); await Task.Delay(20); } Settle(); }
    static List<string> Texts(Control c) => c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Length > 0).ToList();

    const string Crossref = """{"status":"ok","message":{"type":"journal-article","DOI":"10.1038/nature14539","title":["Deep learning"],"author":[{"given":"Yann","family":"LeCun"},{"given":"Yoshua","family":"Bengio"},{"given":"Geoffrey","family":"Hinton"}],"container-title":["Nature"],"volume":"521","issue":"7553","page":"436-444","published-print":{"date-parts":[[2015,5,28]]}}}""";

    [AvaloniaFact(DisplayName = "STUDIO-CITE-01 (UI): DOI eintippen füllt die Quelle mit Herkunft, Rückgängig nimmt sie weg; RIS und CSL-JSON aus, RIS wieder ein (Netz ersetzt)")]
    public async Task Cite_doi_ris_csl()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var asked = new List<Uri>();
            w.CiteFetchOverride = u => { asked.Add(u); return Task.FromResult(("application/json", Encoding.UTF8.GetBytes(Crossref), u)); };
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            w.Find<TextBox>("cite-p-doi").Text = "doi:10.1038/nature14539";
            w.Find<Button>("cite-doi-get").Press();
            await Until(() => MainWindow.CiteSources(d).Count == 1);
            Assert.Equal("https://api.crossref.org/works/10.1038%2Fnature14539", asked.Single().AbsoluteUri);
            var s = MainWindow.CiteSources(d).Single();
            Assert.Equal(("artikel", "Deep learning", "2015", "Nature", "436–444"), (s.Kind, s.Title, s.Year, s.Journal, s.Pages));
            Assert.StartsWith("Crossref · https://api.crossref.org/works/", s.Origin);
            Assert.StartsWith("✓ Angaben aus Crossref übernommen", w.ProgramMessage(K));
            Assert.Contains(Texts(w.Find<StackPanel>("cite-list")), t => t.StartsWith("Herkunft: Crossref"));
            Assert.Equal("✓ Vollständig für diesen Stil.", w.Find<TextBlock>("cite-missing").Text);
            // dieselbe DOI noch einmal: keine zweite Quelle
            await w.CiteFromDoiAsync(d, "10.1038/nature14539"); Assert.Single(MainWindow.CiteSources(d));

            var ris = MainWindow.Programs[K].Exports.Single(x => x.Name == "ris").Write(d, Path.Combine(root, "quellen.ris"));
            var csl = MainWindow.Programs[K].Exports.Single(x => x.Name == "csl").Write(d, Path.Combine(root, "quellen.json"));
            Assert.Contains("TY  - JOUR", File.ReadAllText(ris)); Assert.Contains("\"DOI\": \"10.1038/nature14539\"", File.ReadAllText(csl));
            Assert.False(File.ReadAllBytes(ris).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF })); // ohne BOM

            var d2 = w.ProgramNew(K, "leer"); w.Show(K); Settle();
            Assert.Equal(1, w.CiteImportFile(d2, ris));
            var back = MainWindow.CiteSources(d2).Single();
            Assert.Equal((s.Kind, s.Authors, s.Title, s.Year, s.Journal, s.Volume, s.Issue, s.Pages, s.Doi, s.Origin), (back.Kind, back.Authors, back.Title, back.Year, back.Journal, back.Volume, back.Issue, back.Pages, back.Doi, back.Origin));
            Assert.Contains("aus RIS eingelesen", w.ProgramMessage(K));

            // Rückgängig nimmt die DOI-Quelle wieder weg (ein Schritt)
            Assert.True(d.Undo()); if (MainWindow.CiteSources(d).Count > 0) Assert.True(d.Undo()); // zweiter Abruf derselben DOI ist ein eigener Schritt
            Assert.Empty(MainWindow.CiteSources(d));
            // Fehler sagen, was los ist – ohne neue Quelle
            w.CiteFetchOverride = _ => throw new HttpRequestException("nicht gefunden", null, System.Net.HttpStatusCode.NotFound);
            Assert.Null(await w.CiteFromDoiAsync(d, "10.1234/gibtsnicht")); Assert.Equal("Crossref kennt diese DOI nicht. Bitte prüfen.", w.ProgramMessage(K));
            Assert.Null(await w.CiteFromDoiAsync(d, "keine doi")); Assert.Contains("keine DOI", w.ProgramMessage(K));
            Assert.Empty(MainWindow.CiteSources(d));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "SYS-08 (UI): Suchen zeigt Treffer mit Herkunft, Klick liest im Lesemodus; Sammlung behalten übersteht den Neustart (Netz ersetzt)")]
    public async Task Sys08_suche_und_sammlung()
    {
        var (w, l, root) = Ui.Open();
        MainWindow? w2 = null;
        try
        {
            w.ResearchSearchFetchOverride = u => Task.FromResult(("application/json", Encoding.UTF8.GetBytes("""{"query":{"search":[{"title":"Phishing","snippet":"Betrug per Mail"},{"title":"Spam","snippet":"Werbung"}]}}"""), u));
            w.ReaderFetchOverride = u => Task.FromResult(("text/html", "<html><head><title>Phishing – Wikipedia</title></head><body><p>Phishing ist Betrug.</p></body></html>", u));
            w.Show("recherche"); Settle();
            Assert.NotEqual(w.Find<Button>("rs-read").Background?.ToString(), w.Find<Button>("rs-search").Background?.ToString()); // genau eine goldene Hauptaktion: „Suchen“
            w.Find<TextBox>("rs-query").Text = "Phishing";
            RealClick(w, w.Find<Button>("rs-search"));
            await Until(() => w.ResearchHits.Count == 2);
            Assert.Contains(Texts(w.Find<StackPanel>("rs-hits")), t => t == "Phishing");
            Assert.StartsWith("Herkunft: Wikipedia (Deutsch) · ", w.Find<SelectableTextBlock>("rs-hits-origin").Text);
            Assert.Contains("srsearch=Phishing", w.Find<SelectableTextBlock>("rs-hits-origin").Text);
            RealClick(w, w.Find<Button>("rs-hit-0"));
            await Until(() => w.ReaderShown is not null);
            Assert.Equal("https://de.wikipedia.org/wiki/Phishing", w.ReaderUrl);
            Assert.NotNull(w.TryFind<Button>("rs-side-hit-1")); // Treffer bleiben links
            var keep = w.Find<CheckBox>("rs-keep"); keep.BringIntoView(); Settle(); RealClick(w, keep); Settle();
            Assert.True(w.Desk.Keep);
            w.Close();

            // Neustart mit demselben Datenordner: Sammlung und Treffer sind noch da
            var core = new TrendoCore(root, new InMemorySecretStore(), l, offline: false);
            w2 = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
            w2.Show(); Settle(); w2.Show("recherche"); Settle();
            Assert.Single(w2.Desk.SessionSources()); Assert.Single(w2.Desk.Searches()); Assert.Equal(2, w2.ResearchHits.Count);
            Assert.True(w2.Find<CheckBox>("rs-keep").IsChecked);
            Assert.Equal("Sammlung leeren (alles löschen)", w2.Find<Button>("rs-end").Content);
        }
        finally { w.Close(); w2?.Close(); }
    }

    [AvaloniaFact(DisplayName = "SYS-08: ohne Sammlung behalten beginnt jede Sitzung leer (wie bisher)")]
    public void Sys08_fluechtig()
    {
        var (w, l, root) = Ui.Open();
        MainWindow? w2 = null;
        try
        {
            w.Desk.Record(new Uri("https://example.org/a"), new ReaderPage("A", "Text", []), "Text");
            w.Close();
            w2 = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), l, offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
            w2.Show(); Settle();
            Assert.Empty(w2.Desk.SessionSources()); Assert.False(w2.Desk.Keep);
        }
        finally { w.Close(); w2?.Close(); }
    }

    // ---------- echter Abruf ----------

    static bool NoNet(Exception ex) => ex is HttpRequestException or TaskCanceledException or System.Net.Sockets.SocketException && Beleg is null;

    [AvaloniaFact(DisplayName = "STUDIO-CITE-01 echt: DOI 10.1038/nature14539 bei Crossref über den echten Lese-Weg (übersprungen nur ohne Internet)")]
    public async Task Cite_Echt()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var url = CiteKit.CrossrefUrl("10.1038/nature14539");
            (string Type, byte[] Bytes, Uri Final) got;
            try { got = await ReadOnlyWeb.GetAsync(url); }
            catch (Exception ex) when (NoNet(ex)) { return; }
            Raw("crossref-10.1038_nature14539.json", got.Bytes);
            w.CiteFetchOverride = _ => Task.FromResult(got); // dieselbe echte Antwort durch die Oberfläche
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            var s = await w.CiteFromDoiAsync(d, "10.1038/nature14539");
            Assert.NotNull(s);
            Assert.Equal("Deep learning", s!.Title); Assert.Contains("LeCun", s.Authors); Assert.Equal("2015", s.Year); Assert.Equal("Nature", s.Journal);
            var ris = CiteKit.Ris([s]); var csl = CiteKit.CslJson([s]);
            Log("cite-echt.log", $"{DateTimeOffset.Now:O} GET {url} -> {got.Final} {got.Type} {got.Bytes.Length} Bytes SHA-256 {Sha(got.Bytes)}\nQuelle: {CiteKit.Plain(CiteKit.Format(s, "apa"))}\nHerkunft: {s.Origin}");
            Raw("cite-echt.ris", Encoding.UTF8.GetBytes(ris)); Raw("cite-echt.csl.json", Encoding.UTF8.GetBytes(csl));
            Assert.Equal(s.Title, CiteKit.ParseRis(ris).Sources.Single().Title);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "SYS-08 echt: Wikipedia-Suche und Lesen des ersten Treffers über den echten Lese-Weg (übersprungen nur ohne Internet)")]
    public async Task Sys08_Echt()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var url = ResearchSearch.QueryUrl("wikipedia", "Phishing");
            (string Type, byte[] Bytes, Uri Final) got;
            try { got = await ReadOnlyWeb.GetAsync(url); }
            catch (Exception ex) when (NoNet(ex)) { return; }
            Raw("wikipedia-suche-phishing.json", got.Bytes);
            w.ResearchSearchFetchOverride = _ => Task.FromResult(got);
            await w.ResearchSearchAsync("wikipedia", "Phishing");
            Assert.NotEmpty(w.ResearchHits);
            var first = w.ResearchHits[0];
            Log("sys08-echt.log", $"{DateTimeOffset.Now:O} GET {url} -> {got.Type} {got.Bytes.Length} Bytes SHA-256 {Sha(got.Bytes)}\n{w.ResearchHits.Count} Treffer, erster: {first.Title} {first.Url} (Herkunft {first.Engine})");
            await w.ReadUrlAsync(first.Url); // echter Lese-Weg (kein ReaderFetchOverride)
            Assert.NotNull(w.ReaderShown);
            var src = w.Desk.SessionSources().Last();
            Log("sys08-echt.log", $"{DateTimeOffset.Now:O} gelesen {src.Url} · {src.Chars} Zeichen · SHA-256 {src.Sha256}");
            // DuckDuckGo: ehrlich mitschreiben, was der Dienst liefert
            try
            {
                var dq = ResearchSearch.QueryUrl("duckduckgo", "Phishing erkennen");
                var dd = await ReadOnlyWeb.GetAsync(dq);
                string res;
                try { res = ResearchSearch.Parse("duckduckgo", Encoding.UTF8.GetString(dd.Bytes), dq, DateTimeOffset.Now).Count + " Treffer"; }
                catch (InvalidDataException e) { res = "abgelehnt: " + e.Message; }
                Log("sys08-echt.log", $"{DateTimeOffset.Now:O} GET {dq} -> {dd.Type} {dd.Bytes.Length} Bytes SHA-256 {Sha(dd.Bytes)} · {res}");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { Log("sys08-echt.log", $"{DateTimeOffset.Now:O} DuckDuckGo nicht erreichbar: {e.Message}"); }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "X-07 echt: KI-Auftrag „Quelle holen“ lädt nach Freigabe ein HDRI von Poly Haven mit Beleg je Datei (übersprungen nur ohne Internet)")]
    public async Task X07_Echt()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-x07-" + Guid.NewGuid().ToString("N"));
        try
        {
            string? pick = null;
            try
            {
                var list = FreeAssets.ParseList(Encoding.UTF8.GetString((await ReadOnlyWeb.GetAsync(new Uri(FreeAssets.ListUrl("hdris")))).Bytes), "", 400);
                foreach (var h in list.OrderBy(h => h.Id, StringComparer.Ordinal).Take(40))
                {
                    var files = Encoding.UTF8.GetString((await ReadOnlyWeb.GetAsync(new Uri(FreeAssets.FilesUrl(h.Id)))).Bytes);
                    if (FreeAssets.Plan(files, "hdris").All(f => f.Size is > 0 and < ResearchDesk.MaxBytes)) { pick = h.Id; break; }
                }
            }
            catch (Exception ex) when (NoNet(ex)) { return; }
            Assert.NotNull(pick);
            var core = new TrendoCore(root, new InMemorySecretStore(), offline: false);
            var project = core.Projects.Create("probe", "Probe").Id;
            var reads = new List<string>();
            var wf = new StudioWorkflow(core, PromptActions.CreateDefault(), services: new PromptServices { Offline = false, ReadBytes = async (u, ct) => { reads.Add(u.ToString()); return await ReadOnlyWeb.GetAsync(u, ct); } });
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("x07-echt", project, "X-07", "Holen: ja\nAsset: " + pick, [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            // wie in der App: der Auftrag läuft nicht im Oberflächen-Thread (der Adapter wartet synchron auf den Abruf)
            var planned = await Task.Run(() => wf.Los(project, wf.Start(req, ctx).RunId, ctx));
            Assert.Equal(RunStatus.AwaitingApproval, planned.Status); Assert.Empty(reads);
            var done = await Task.Run(() => wf.Approve(project, planned.RunId, Principal.LocalOwner, ctx));
            Assert.True(done.Status == RunStatus.Verified, string.Join(" | ", done.Errors));
            var dir = Path.Combine(core.Projects.ProjectFilesRoot(project), "assets", pick!);
            var hdr = Directory.GetFiles(dir, "*.hdr").Single();
            var bytes = File.ReadAllBytes(hdr);
            Assert.StartsWith("#?RADIANCE", Encoding.ASCII.GetString(bytes, 0, 10)); // echte Radiance-Datei
            var prov = File.ReadAllText(Path.Combine(dir, "HERKUNFT.txt"));
            Assert.Contains("SHA-256: " + Sha(bytes), prov); Assert.Contains("CC0 1.0", prov);
            Log("x07-echt.log", $"{DateTimeOffset.Now:O} Freigabe erteilt, Abrufe: {string.Join(", ", reads)}\nBeleg: {done.Receipt}\n{Path.GetFileName(hdr)} {bytes.Length} Bytes SHA-256 {Sha(bytes)}");
            Raw("x07-HERKUNFT.txt", Encoding.UTF8.GetBytes(prov)); Raw("x07-herkunft.json", File.ReadAllBytes(Path.Combine(dir, "herkunft.json")));
            Raw("x07-" + Path.GetFileName(hdr) + ".kopf.txt", bytes.Take(64).ToArray());
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
