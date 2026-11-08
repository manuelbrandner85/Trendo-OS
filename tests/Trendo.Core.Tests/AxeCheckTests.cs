using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 7 – CORE-07: Barrierefreiheit mit axe-core in jsdom. Antworten in echter Form; echte Läufe finden axe automatisch.</summary>
public class AxeCheckTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-axe-" + Guid.NewGuid().ToString("N"));
    public AxeCheckTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    const string Answer = """{"axe":"4.14.0","results":[{"file":"index.html","passes":21,"violations":[{"id":"image-alt","impact":"critical","help":"Images must have alternative text","nodes":2,"target":"img"},{"id":"heading-order","impact":"moderate","help":"Heading levels should only increase by one","nodes":1,"target":"h3"},{"id":"neue-regel","impact":"serious","help":"Something new","nodes":1,"target":""}]}]}""";

    [Fact(DisplayName = "axe +1: Antwort lesen – deutsche Texte, schwer = Fehler, mittel = Hinweis, Anzahl und Fundstelle, unbekannte Regel mit axe-Kennung")]
    public void Parse()
    {
        var r = AxeCheck.Parse(Answer);
        Assert.Equal(("4.14.0", 1, 21, 3), (r.Version, r.Files, r.Passes, r.Findings.Count));
        Assert.Equal(("Fehler", "axe-image-alt"), (r.Findings[0].Level, r.Findings[0].Code));
        Assert.Equal("index.html: Bild ohne Alternativtext – Screenreader können es nicht beschreiben. (2×) – z. B. „img“", r.Findings[0].Text);
        Assert.Equal("Hinweis", r.Findings[1].Level); Assert.Contains("Überschriften springen", r.Findings[1].Text);
        Assert.Contains("Something new (axe: neue-regel)", r.Findings[2].Text); Assert.All(r.Findings, f => Assert.Equal("Barrierefreiheit", f.Area));
    }

    [Fact(DisplayName = "axe +2: Lauf übergibt Startskript und alle HTML-Seiten (ohne .vorschau), schreibt das eigene Startskript in den Werkzeugordner")]
    public void Run_args()
    {
        var site = Path.Combine(_dir, "seite"); Directory.CreateDirectory(Path.Combine(site, ".vorschau")); Directory.CreateDirectory(Path.Combine(site, "unter"));
        File.WriteAllText(Path.Combine(site, "index.html"), "<html></html>"); File.WriteAllText(Path.Combine(site, "unter", "b.html"), "x"); File.WriteAllText(Path.Combine(site, ".vorschau", "c.html"), "x");
        IReadOnlyList<string>? seen = null; var tools = Path.Combine(_dir, "axe");
        var (res, msg) = AxeCheck.Run("node", tools, site, run: (_, a, _) => { seen = a; return new Cli.Result(0, Answer, ""); });
        Assert.NotNull(res); Assert.StartsWith("axe-core 4.14.0: 1 Seite(n), 21 Regeln bestanden, 3 Befund(e).", msg);
        Assert.Equal(3, seen!.Count); Assert.EndsWith(AxeCheck.RunnerName, seen[0]); Assert.DoesNotContain(seen, s => s.Contains(".vorschau"));
        Assert.Equal(AxeCheck.Runner, File.ReadAllText(Path.Combine(tools, AxeCheck.RunnerName)));
    }

    [Fact(DisplayName = "axe +3: Startskript lässt Seiten-Skripte nicht laufen (outside-only), schaltet Farbkontrast ab und hat keine Adresse (lädt nichts nach)")]
    public void Runner_safety()
    {
        Assert.Contains("runScripts: 'outside-only'", AxeCheck.Runner); Assert.Contains("'color-contrast': { enabled: false }", AxeCheck.Runner);
        Assert.DoesNotContain("url:", AxeCheck.Runner); Assert.DoesNotContain("resources:", AxeCheck.Runner);
        Assert.Contains("--ignore-scripts", AxeCheck.InstallArgs("x")); Assert.Contains("axe-core@" + AxeCheck.AxeVersion, AxeCheck.InstallArgs("x"));
    }

    [Fact(DisplayName = "axe −1: ohne Einrichtung, ohne HTML, bei Fehler oder kaputter Antwort klare Meldung statt Absturz")]
    public void Errors()
    {
        var site = Path.Combine(_dir, "s"); Directory.CreateDirectory(site);
        Assert.Equal("Es liegt keine HTML-Datei im Ordner.", AxeCheck.Run("node", _dir, site).Message);
        File.WriteAllText(Path.Combine(site, "index.html"), "x");
        Assert.Equal("axe-core ist noch nicht eingerichtet.", AxeCheck.Run("node", Path.Combine(_dir, "leer"), site).Message);
        Assert.StartsWith("axe-core meldet: Cannot find module", AxeCheck.Run("node", _dir, site, run: (_, _, _) => new Cli.Result(1, "", "Cannot find module 'jsdom'")).Message);
        Assert.Equal("axe-core hat keine lesbare Antwort geliefert.", AxeCheck.Run("node", _dir, site, run: (_, _, _) => new Cli.Result(0, "kein json", "")).Message);
        Assert.Equal("Der Ordner der Seite fehlt.", AxeCheck.Run("node", _dir, Path.Combine(_dir, "weg")).Message);
    }

    // ---------- echter Lauf ----------
    public sealed class AxeFactAttribute : FactAttribute { public AxeFactAttribute() { if (EchtWerkzeuge.AxeDir() is null || EchtWerkzeuge.Node() is null) Skip = "Übersprungen – axe-core/jsdom oder Node nicht gefunden."; } }

    const string BadPage = """
        <!doctype html><html><head><meta charset="utf-8"></head><body>
        <h1>Laden</h1><h3>Angebote</h3>
        <img src="regal.jpg"><a href="/x"></a><button></button>
        <form><input type="text" name="mail"></form>
        <script>document.querySelector('img').setAttribute('alt', 'Regal'); document.documentElement.lang = 'de';</script>
        </body></html>
        """;
    const string GoodPage = """
        <!doctype html><html lang="de"><head><meta charset="utf-8"><title>Laden</title></head><body>
        <header><nav><a href="/">Start</a></nav></header>
        <main><h1>Laden</h1><h2>Angebote</h2><img src="regal.jpg" alt="Regal aus Eiche">
        <form><label for="m">E-Mail</label><input id="m" type="email"><button type="submit">Senden</button></form></main>
        <footer><p>Impressum</p></footer></body></html>
        """;

    [AxeFact(DisplayName = "axe echt: fehlerhafte Seite → Bild-Alt, Sprache, Titel, Link, Knopf, Beschriftung, Überschriftenfolge gefunden (Seiten-Skript lief nicht); saubere Seite ohne Fehler")]
    public void Real_run()
    {
        var site = Path.Combine(_dir, "echt"); Directory.CreateDirectory(site);
        File.WriteAllText(Path.Combine(site, "schlecht.html"), BadPage); File.WriteAllText(Path.Combine(site, "gut.html"), GoodPage);
        var (r, msg) = AxeCheck.Run(EchtWerkzeuge.Node()!, EchtWerkzeuge.AxeDir()!, site);
        Assert.True(r is not null, msg);
        Assert.Equal((AxeCheck.AxeVersion, 2), (r!.Version, r.Files)); Assert.True(r.Passes > 20);
        var bad = r.Findings.Where(f => f.Text.StartsWith("schlecht.html")).Select(f => f.Code).ToHashSet();
        foreach (var id in new[] { "image-alt", "html-has-lang", "document-title", "link-name", "button-name", "label", "heading-order" }) Assert.Contains("axe-" + id, bad);
        Assert.DoesNotContain(r.Findings, f => f.Text.StartsWith("gut.html") && f.Level == "Fehler");
    }
}
