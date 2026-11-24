using System.IO.Compression;
using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0765, zweite Prüfrunde (L1–L4): reservierte Feldnamen, Kennungen ohne Vertauschung, gleiche Sammlung wiederverwenden, Bildpfade bei
/// wiederholter Rundreise nicht verschachteln.</summary>
public class T0765_Runde2Tests
{
    [Fact(DisplayName = "T-0765 L1: Felder „elements“, „reset“ & Co. bekommen eine freie Kennung – überall (Kurzschrift, Hinzufügen, Datei); Skript nutzt keine Formular-Eigenschaften")]
    public void L1_reservierte_feldnamen()
    {
        Assert.Equal(["elements-2", "reset-2", "submit-2", "name-2", "zahl"], SitePage.ParseFields("Elements*, Reset, Submit, Name, Zahl (Zahl)").Select(f => f.Id));
        var c = new DataCollection { Id = "x" };
        Assert.Equal("length-2", SitePage.AddField(c, "Length").Id);
        var json = new JsonObject { ["Version"] = 2, ["Pages"] = new JsonArray(new JsonObject { ["Id"] = "start", ["Name"] = "S", ["Blocks"] = new JsonArray() }),
            ["Collections"] = new JsonArray(new JsonObject { ["Id"] = "c", ["Name"] = "C", ["Fields"] = new JsonArray(
                new JsonObject { ["Id"] = "elements", ["Label"] = "Elements" }, new JsonObject { ["Id"] = "reset", ["Label"] = "Reset" }, new JsonObject { ["Id"] = "_t", ["Label"] = "T" }) }) }.ToJsonString();
        var p = SitePage.FromJson(json);
        Assert.All(p.Collections[0].Fields, f => Assert.DoesNotContain(f.Id, SitePage.ReservedFieldIds));
        Assert.Contains("HTMLFormElement.prototype.reset.call(form)", SitePage.AppScript);
        Assert.Contains("form.querySelectorAll('[name]')", SitePage.AppScript);
        Assert.DoesNotContain("form.elements", SitePage.AppScript);
        Assert.DoesNotContain("form.reset()", SitePage.AppScript);
        Assert.Contains(SitePage.ScriptHash, SitePage.Template("einkaufsliste")!.ToHtml()); // CSP-Hash folgt dem Skript
    }

    [Fact(DisplayName = "T-0765 L2: gültige Kennungen behalten ihren Wert, „Preis“ wird nicht zu „preis“, wenn es „preis“ schon gibt – Verweise vertauschen nicht")]
    public void L2_keine_vertauschung()
    {
        var json = new JsonObject
        {
            ["Version"] = 2,
            ["Pages"] = new JsonArray(new JsonObject { ["Id"] = "Start", ["Name"] = "Groß", ["Blocks"] = new JsonArray(
                    new JsonObject { ["Kind"] = "stat", ["Text"] = "a", ["Url"] = "summe:Preis", ["Data"] = "c" },
                    new JsonObject { ["Kind"] = "stat", ["Text"] = "b", ["Url"] = "summe:preis", ["Data"] = "c" },
                    new JsonObject { ["Kind"] = "button", ["Text"] = "zu klein", ["Url"] = "seite:start" },
                    new JsonObject { ["Kind"] = "button", ["Text"] = "zu groß", ["Url"] = "seite:Start" }) },
                new JsonObject { ["Id"] = "start", ["Name"] = "Klein", ["Blocks"] = new JsonArray() }),
            ["Collections"] = new JsonArray(new JsonObject { ["Id"] = "c", ["Name"] = "C", ["Fields"] = new JsonArray(
                new JsonObject { ["Id"] = "Preis", ["Label"] = "Preis groß", ["Type"] = "zahl" }, new JsonObject { ["Id"] = "preis", ["Label"] = "preis klein", ["Type"] = "zahl" }) }),
        }.ToJsonString();
        var p = SitePage.FromJson(json);
        Assert.Equal("start", p.Pages[1].Id);   // die schon gültige Kennung bleibt bei ihrer Seite („Klein“)
        Assert.Equal("start-2", p.Pages[0].Id); // „Start“ weicht aus
        Assert.Equal("preis", p.Collections[0].Fields.Single(f => f.Label == "preis klein").Id);
        Assert.Equal("preis-2", p.Collections[0].Fields.Single(f => f.Label == "Preis groß").Id);
        var b = p.Pages[0].Blocks;
        Assert.Equal("summe:preis-2", b[0].Url); Assert.Equal("summe:preis", b[1].Url);
        Assert.Equal("seite:start", b[2].Url); Assert.Equal("seite:start-2", b[3].Url);
    }

    [Fact(DisplayName = "T-0765 L3: Anhängen verwendet eine gleiche Sammlung (Name ohne Groß/klein, gleiche Felder) wieder; abweichende bekommt eine neue Kennung")]
    public void L3_gleiche_sammlung()
    {
        var app = SitePage.Template("einkaufsliste")!;
        var gleich = new SitePage(); var g = gleich.AddCollection("einkäufe", app.Collections[0].Fields); gleich.Blocks.Add(new Block("form", "x", "", g.Id));
        app.Absorb(gleich);
        Assert.Single(app.Collections);
        Assert.Equal("einkaeufe", app.Pages.Last().Blocks.Single(x => x.Kind == "form").Data);
        var anders = new SitePage(); var a = anders.AddCollection("Einkäufe", SitePage.ParseFields("Was*")); anders.Blocks.Add(new Block("form", "x", "", a.Id));
        app.Absorb(anders);
        Assert.Equal(["einkaeufe", "einkaeufe-2"], app.Collections.Select(c => c.Id));
        Assert.Equal("einkaeufe-2", app.Pages.Last().Blocks.Single(x => x.Kind == "form").Data);
    }

    [Fact(DisplayName = "T-0765 L4: wiederholte Rundreise verschachtelt Bildpfade nicht (importiert/… bleibt), vorhandene Datei bleibt unberührt")]
    public void L4_bildpfade()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0765r2-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            var assets = Path.Combine(dir, "baukasten"); Directory.CreateDirectory(assets);
            var p = SitePage.Template("einkaufsliste")!; p.Pages[0].Blocks.Add(new Block("img", "Logo", "logo.png"));
            File.WriteAllText(Path.Combine(assets, "logo.png"), "BILD");
            string Export(SitePage page, string name) { var o = Path.Combine(dir, name); page.Publish(o, assets); var z = o + ".zip"; ZipFile.CreateFromDirectory(o, z); return z; }
            var eins = SitePage.FromExport(Export(p, "a"), assets, "importiert/1");
            Assert.Equal("importiert/1/logo.png", eins.Pages[0].Blocks.Last().Url);
            var zwei = SitePage.FromExport(Export(eins, "b"), assets, "importiert/2");
            Assert.Equal("importiert/1/logo.png", zwei.Pages[0].Blocks.Last().Url); // nicht importiert/2/importiert/1/…
            Assert.False(Directory.Exists(Path.Combine(assets, "importiert", "2", "importiert")));
            Assert.Equal("BILD", File.ReadAllText(Path.Combine(assets, "importiert", "1", "logo.png")));
            // anderer Inhalt unter demselben importiert/-Pfad: vorhandene Datei bleibt, neuer Name
            var fremd = Path.Combine(dir, "fremd.zip");
            using (var a = ZipFile.Open(fremd, ZipArchiveMode.Create))
            {
                using (var w = new StreamWriter(a.CreateEntry("index.html").Open())) w.Write(eins.ToHtml());
                using (var w = new StreamWriter(a.CreateEntry("importiert/1/logo.png").Open())) w.Write("ANDERS");
            }
            var drei = SitePage.FromExport(fremd, assets, "importiert/3");
            Assert.Equal("importiert/1/logo-2.png", drei.Pages[0].Blocks.Last().Url);
            Assert.Equal("BILD", File.ReadAllText(Path.Combine(assets, "importiert", "1", "logo.png")));
        }
        finally { Directory.Delete(dir, true); }
    }
}
