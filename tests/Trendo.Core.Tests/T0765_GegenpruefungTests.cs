using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0765 Nacharbeit nach der unabhängigen Gegenprüfung (M1–M5, N1–N3): präparierte Kennungen, Zeitstempel-Schlüssel, KI hängt an statt zu
/// ersetzen, Größengrenzen beim Wiederöffnen, Import überschreibt nichts, Nullwerte in alten Dateien, doppelte Kennungen, ehrlicher Speicher-Hinweis.</summary>
public class T0765_GegenpruefungTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0765g-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    const string Evil = "x\"><meta http-equiv=\"refresh\" content=\"0;url=https://example.org/\"><x y=\"";

    /// <summary>Präparierte seite.json: Kennungen von Seite, Sammlung und Feld enthalten Markup.</summary>
    internal static string EvilJson() => new JsonObject
    {
        ["Version"] = 2, ["Title"] = "Falle", ["Accent"] = "#C8A04A", ["AppId"] = "a\"b",
        ["Pages"] = new JsonArray(
            new JsonObject { ["Id"] = Evil, ["Name"] = "Eins", ["Blocks"] = new JsonArray(
                new JsonObject { ["Kind"] = "form", ["Text"] = "Los", ["Url"] = "", ["Data"] = Evil },
                new JsonObject { ["Kind"] = "datalist", ["Text"] = "", ["Url"] = "", ["Data"] = Evil },
                new JsonObject { ["Kind"] = "stat", ["Text"] = "", ["Url"] = "summe:" + Evil, ["Data"] = Evil },
                new JsonObject { ["Kind"] = "button", ["Text"] = "Weiter", ["Url"] = "seite:" + Evil }) },
            new JsonObject { ["Id"] = Evil, ["Name"] = "Zwei (gleiche Kennung)", ["Blocks"] = new JsonArray() }),
        ["Collections"] = new JsonArray(new JsonObject { ["Id"] = Evil, ["Name"] = "S", ["Fields"] = new JsonArray(
            new JsonObject { ["Id"] = Evil, ["Label"] = "Betrag", ["Type"] = "zahl", ["Required"] = true },
            new JsonObject { ["Id"] = Evil, ["Label"] = "Noch eins", ["Type"] = "text", ["Required"] = false }) }),
    }.ToJsonString();

    [Fact(DisplayName = "T-0765 M1/N2: präparierte Kennungen werden sichere, eindeutige Slugs – kein <meta refresh>, kein Attribut-Ausbruch, Verweise folgen")]
    public void M1_kennungen_maskiert()
    {
        var p = SitePage.FromJson(EvilJson());
        var ids = p.Pages.Select(s => s.Id).Concat(p.Collections.Select(c => c.Id)).Concat(p.Collections[0].Fields.Select(f => f.Id)).ToList();
        Assert.All(ids, id => Assert.Matches("^[a-z0-9-]+$", id));
        Assert.Equal(2, p.Pages.Select(s => s.Id).Distinct().Count());               // N2: doppelte Seiten-Kennung eindeutig
        Assert.Equal(2, p.Collections[0].Fields.Select(f => f.Id).Distinct().Count()); // doppelte Feld-Kennung eindeutig
        Assert.Equal(p.Collections[0].Id, p.Pages[0].Blocks[0].Data);                // Verweis auf die Sammlung folgt
        Assert.Equal("seite:" + p.Pages[0].Id, p.Pages[0].Blocks[3].Url);            // Verweis auf die Seite folgt
        Assert.Equal("summe:" + p.Collections[0].Fields[0].Id, p.Pages[0].Blocks[2].Url);
        Assert.Matches("^[A-Za-z0-9-]+$", p.AppId); // keine Anführungszeichen in der App-Kennung
        var html = p.ToHtml();
        Assert.DoesNotContain("http-equiv=\"refresh\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<meta http-equiv=\\u0022refresh", html);
        Assert.Single(Regex.Matches(html, "<meta http-equiv", RegexOptions.IgnoreCase)); // nur die eigene CSP
        Assert.DoesNotContain("<x y=", html);
        Assert.Contains("<input name=\"", html);
        Assert.Contains("form.elements.namedItem(f.Id)", SitePage.AppScript); // kein zusammengesetzter Selektor mehr
        Assert.DoesNotContain("querySelector('[name=", SitePage.AppScript);
    }

    [Fact(DisplayName = "T-0765 M2/N2: Zeitstempel unter eigenem Schlüssel (_t), Feld „Zeit“ bleibt; große Zahlen werden abgelehnt und lesbar begrenzt")]
    public void M2_zeit_feld()
    {
        Assert.Contains("e._t = Date.now();", SitePage.AppScript);
        Assert.DoesNotContain("e.zeit", SitePage.AppScript);
        Assert.Equal("zeit", SitePage.ParseFields("Zeit")[0].Id); // ein Feld „Zeit“ ist erlaubt – Slugs enthalten nie „_“, also nie „_t“
        Assert.Contains("GRENZE = 1e12", SitePage.AppScript);
        Assert.Contains("zu groß", SitePage.AppScript);
    }

    [Fact(DisplayName = "T-0765 N1: Fassung-1-Datei mit \"Url\": null / \"Text\": null / \"Kind\": null öffnet ohne Absturz, Seite bleibt einfach")]
    public void N1_nullwerte()
    {
        var f = "{ \"Title\": null, \"Accent\": null, \"Blocks\": [ { \"Kind\": \"h1\", \"Text\": null, \"Url\": null }, { \"Kind\": \"button\", \"Text\": \"Mehr\", \"Url\": null }, { \"Kind\": null, \"Text\": \"x\", \"Url\": \"\" }, null ] }";
        var p = SitePage.FromJson(f);
        Assert.False(p.IsApp);
        Assert.Equal(3, p.Blocks.Count);
        Assert.All(p.Blocks, b => { Assert.NotNull(b.Kind); Assert.NotNull(b.Text); Assert.NotNull(b.Url); });
        Assert.Contains("<h1></h1>", p.ToHtml());
        Assert.StartsWith("{\n  \"Version\": 1,", p.ToJson().ReplaceLineEndings("\n"));
        var g = "{ \"Version\": 2, \"Pages\": [ { \"Id\": null, \"Name\": null, \"Blocks\": null } ], \"Collections\": [ { \"Id\": null, \"Name\": null, \"Fields\": [ { \"Id\": null, \"Label\": null } ] } ] }";
        var q = SitePage.FromJson(g);
        Assert.True(q.IsApp); Assert.NotNull(q.ToHtml());
    }

    [Fact(DisplayName = "T-0765 N3: ehrlicher Speicher-Hinweis als Fußzeile der App mit Daten, nicht bei einfacher Seite")]
    public void N3_hinweis()
    {
        var app = SitePage.Template("einkaufsliste")!.ToHtml();
        Assert.Contains("<footer class=\"t-hinweis\">Deine Eintr&#228;ge liegen nur in diesem Browser auf diesem Ger&#228;t", app);
        Assert.Contains("file://", app);
        Assert.DoesNotContain("t-hinweis\">", new SitePage { Blocks = [new("h1", "x")] }.ToHtml());
    }

    static string Zip(string dir, string name, Action<ZipArchive> fill)
    {
        var z = Path.Combine(dir, name);
        using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) fill(a);
        return z;
    }
    static void Put(ZipArchive a, string name, string text) { using var w = new StreamWriter(a.CreateEntry(name).Open()); w.Write(text); }
    static void Zeros(ZipArchive a, string name, long bytes) { using var s = a.CreateEntry(name, CompressionLevel.Optimal).Open(); var buf = new byte[1 << 20]; for (long n = 0; n < bytes; n += buf.Length) s.Write(buf, 0, (int)Math.Min(buf.Length, bytes - n)); }

    [Fact(DisplayName = "T-0765 M4: Größengrenzen beim Wiederöffnen – riesige index.html, riesiges Bild, zu viele Einträge: verständlich abgelehnt, nichts geschrieben")]
    public void M4_grenzen()
    {
        var dir = Tmp();
        try
        {
            var page = SitePage.Template("einkaufsliste")!; page.Pages[0].Blocks.Add(new Block("img", "Bild", "gross.png"));
            var html = page.ToHtml();
            // 21 MB index.html aus Nullen (ZIP selbst klein)
            var bigHtml = Zip(dir, "html.zip", a => Zeros(a, "index.html", 21L * 1024 * 1024));
            Assert.True(new FileInfo(bigHtml).Length < 2 * 1024 * 1024);
            Assert.Contains("zu groß", Assert.Throws<InvalidDataException>(() => SitePage.FromExport(bigHtml)).Message);
            // 30-MB-Bild
            var assets = Path.Combine(dir, "ziel"); Directory.CreateDirectory(assets);
            var bigImg = Zip(dir, "bild.zip", a => { Put(a, "index.html", html); Zeros(a, "gross.png", 30L * 1024 * 1024); });
            Assert.Contains("zu groß", Assert.Throws<InvalidDataException>(() => SitePage.FromExport(bigImg, assets)).Message);
            Assert.Empty(Directory.GetFileSystemEntries(assets)); // nichts geschrieben
            // zu viele Einträge
            var many = Zip(dir, "viele.zip", a => { Put(a, "index.html", html); for (var i = 0; i <= SitePage.MaxImportEntries; i++) a.CreateEntry("x/" + i + ".txt"); });
            Assert.Contains("Einträge", Assert.Throws<InvalidDataException>(() => SitePage.FromExport(many, assets)).Message);
            // zu große index.html als Datei
            var bigFile = Path.Combine(dir, "gross.html"); using (var fs = File.Create(bigFile)) fs.SetLength(21L * 1024 * 1024);
            Assert.Contains("zu groß", Assert.Throws<InvalidDataException>(() => SitePage.FromExport(bigFile)).Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0765 M5: Import überschreibt nie vorhandene Dateien, nur Bilder, eigener Unterordner, Verweise folgen")]
    public void M5_nichts_ueberschreiben()
    {
        var dir = Tmp();
        try
        {
            var page = SitePage.Template("einkaufsliste")!;
            page.Pages[0].Blocks.Add(new Block("img", "Logo", "logo.png"));
            page.Pages[0].Blocks.Add(new Block("img", "Falle", "seite.json")); // keine Bilddatei → wird nie kopiert
            var zip = Zip(dir, "app.zip", a => { Put(a, "index.html", page.ToHtml()); Put(a, "logo.png", "NEU"); Put(a, "seite.json", "{\"boese\":1}"); });
            var assets = Path.Combine(dir, "baukasten"); Directory.CreateDirectory(assets);
            File.WriteAllText(Path.Combine(assets, "logo.png"), "ALT"); File.WriteAllText(Path.Combine(assets, "seite.json"), "{\"meins\":1}");
            // ohne Unterordner: vorhandenes logo.png bleibt, das neue Bild bekommt einen eigenen Namen
            var back = SitePage.FromExport(zip, assets);
            Assert.Equal("ALT", File.ReadAllText(Path.Combine(assets, "logo.png")));
            Assert.Equal("{\"meins\":1}", File.ReadAllText(Path.Combine(assets, "seite.json")));
            Assert.Equal("logo-2.png", back.Pages[0].Blocks.Single(b => b.Text == "Logo").Url);
            Assert.Equal("NEU", File.ReadAllText(Path.Combine(assets, "logo-2.png")));
            // mit Unterordner (so ruft der Baukasten): alles unter importiert/…
            var again = SitePage.FromExport(zip, assets, "importiert/1");
            Assert.Equal("importiert/1/logo.png", again.Pages[0].Blocks.Single(b => b.Text == "Logo").Url);
            Assert.Equal("NEU", File.ReadAllText(Path.Combine(assets, "importiert", "1", "logo.png")));
            Assert.False(File.Exists(Path.Combine(assets, "importiert", "1", "seite.json")));
            Assert.Throws<ArgumentException>(() => SitePage.FromExport(zip, assets, "../raus"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0765 M3: KI mit Daten/Seite auf vorhandener App hängt an – vorhandene Seiten und Sammlungen bleiben unverändert, Kennungen eindeutig")]
    public void M3_ki_haengt_an()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0765g-ki-" + Guid.NewGuid().ToString("N")[..8]);
        var core = new TrendoCore(root, new InMemorySecretStore(), offline: true);
        try
        {
            var project = core.Projects.Create("probe", "Probe").Id;
            var tpl = SitePage.Template("einkaufsliste")!;
            var json = core.Projects.ResolveInProject(project, "baukasten/seite.json");
            Directory.CreateDirectory(Path.GetDirectoryName(json)!); tpl.Save(json);
            var before = SitePage.Load(json);
            var wf = new StudioWorkflow(core, PromptActions.CreateDefault());
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("ki-m3", project, "X-03", "Titel: Haushalt\nText: Alles im Blick\nDaten: Einkäufe: Was*, Betrag (Zahl)\nSeite: Neu", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            Assert.Equal(RunStatus.Verified, wf.Los(project, wf.Start(req, ctx).RunId, ctx).Status);
            var after = SitePage.Load(json);
            Assert.Equal(before.AppId, after.AppId);
            // vorhandene Seiten unverändert vorn
            Assert.Equal(before.Pages.Select(s => System.Text.Json.JsonSerializer.Serialize(s)), after.Pages.Take(2).Select(s => System.Text.Json.JsonSerializer.Serialize(s)));
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Collections[0]), System.Text.Json.JsonSerializer.Serialize(after.Collections[0]));
            // neue Sammlung „Einkäufe“ mit eindeutiger Kennung, neue Seiten angehängt, Kennungen eindeutig
            Assert.Equal(["einkaeufe", "einkaeufe-2"], after.Collections.Select(c => c.Id));
            Assert.Equal(after.Pages.Count, after.Pages.Select(s => s.Id).Distinct().Count());
            Assert.True(after.Pages.Count >= 5);
            Assert.Contains(after.Pages.Skip(2).SelectMany(s => s.Blocks), b => b.Kind == "form" && b.Data == "einkaeufe-2");
            Assert.DoesNotContain(after.Pages.Take(2).SelectMany(s => s.Blocks), b => b.Data == "einkaeufe-2");
            var neuSeite = after.Pages.Single(s => s.Name == "Neu");
            Assert.NotEqual("neu", neuSeite.Id); // „neu“ gibt es schon (Vorlage) → eindeutig
            Assert.Equal("Haushalt", after.Title);
        }
        finally { try { foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(root, true); } catch (IOException) { } }
    }
}
