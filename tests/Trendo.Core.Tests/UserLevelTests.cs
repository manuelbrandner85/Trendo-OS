using Trendo.Core.Programs;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>Masterprompt 131 / 7: Bedienstufen und Projektdaten-Bearbeitung der Profi-Stufe.</summary>
public class MP131_Levels
{
    [Fact(DisplayName = "Bedienstufen: unbekannte Stufe → Erweitert, Werkzeug sichtbar ab seiner Stufe, Kinderprofil startet einfach")]
    public void Levels()
    {
        Assert.Equal(UserLevel.Einfach, UserLevel.Of("EINFACH")); Assert.Equal(UserLevel.Profi, UserLevel.Of("profi")); Assert.Equal(UserLevel.Erweitert, UserLevel.Of("quatsch")); Assert.Equal(UserLevel.Erweitert, UserLevel.Of(null));
        Assert.True(UserLevel.Shows(UserLevel.Einfach, 0)); Assert.False(UserLevel.Shows(UserLevel.Einfach, 1)); Assert.True(UserLevel.Shows(UserLevel.Profi, 1));
        Assert.Equal("erweitert", new UserSettings().Level);
        var root = Path.Combine(Path.GetTempPath(), "trendo-lvl-" + Guid.NewGuid().ToString("N"));
        try { var p = new PersonStore(root).Add("Erika Muster", "emoji:🙂", child: true); Assert.Equal("einfach", UserSettings.Load(new PersonStore(root).RootOf(p.Id)).Level); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Projektdaten ersetzen: nur JSON-Objekte, falsche Eingabe lässt Daten und Verlauf unverändert, ein Schritt rückgängig")]
    public void Replace_data()
    {
        var d = new ProgramDocument("SPEC-08", "Test", new() { ["name"] = "A", ["n"] = 1 });
        Assert.Throws<InvalidDataException>(() => d.ReplaceData("{kaputt")); Assert.Throws<InvalidDataException>(() => d.ReplaceData("[1]"));
        Assert.Empty(d.History); Assert.Equal("A", d.Text("name"));
        d.ReplaceData("{\"name\":\"B\",\"neu\":true}");
        Assert.Equal("B", d.Text("name")); Assert.True(d.Flag("neu")); Assert.Null(d.Data["n"]);
        Assert.Equal(["Projektdaten bearbeitet"], d.History);
        Assert.True(d.Undo()); Assert.Equal("A", d.Text("name")); Assert.Equal(1, d.Number("n"));
    }
}

/// <summary>Masterprompt 131 / 17 + 19: Programmkatalog und Produktionspakete – aus dem Repository abgeleitet, Testergebnisse nur aus echtem Lauf.</summary>
public class MP131_Catalog
{
    static string Repo() { var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!; return d; }

    [Fact(DisplayName = "Programmkatalog: Karten-Programm aus dem Code – Engine MapDoc, Formate GPX/KML, OSM-Kacheln, 20 Katalogfunktionen, Vorbild-Analyse, Tests")]
    public void Enrich_from_repo()
    {
        var e = ProgramCatalog.Enrich(new ProgramEntry { Id = "karten", Name = "Karten & Touren", Studio = "SPEC-13", StudioName = "Routen & Karten", Formats = [".tdoc"] }, Repo());
        Assert.Contains("MapDoc", e.Engine); Assert.Contains(".gpx", e.Formats); Assert.Contains(".kml", e.Formats); Assert.Contains(".tdoc", e.Formats);
        Assert.Contains(e.Dependencies, d => d.Contains("OpenStreetMap")); Assert.DoesNotContain(e.Dependencies, d => d.Contains("Node.js"));
        Assert.Equal(20, e.Functions.Count); Assert.Equal("F01", e.Functions[0].Nr); Assert.NotNull(e.Functions[0].Test);
        Assert.EndsWith("karten-gpxsee.md", e.Rea); Assert.Contains("src/Trendo.App/MainWindow.Spec13.cs", e.SourceFiles);
        Assert.Contains(e.Tests, t => t.StartsWith("SPEC-13")); Assert.StartsWith("IMPLEMENTED", e.Status);
    }

    [Fact(DisplayName = "Produktionspaket: elf Dateien; ohne Testlauf keine Ergebnisse und nichts „Getestet“; mit TRX nur bestandene Funktionen getestet")]
    public void Docs_are_honest()
    {
        var e = new ProgramEntry { Id = "x", Name = "Probe", Studio = "SPEC-99", StudioName = "Probe", Area = "Technik", Tests = ["T1", "T2"],
            Functions = [new("F01", "Eins", "gebaut", "T1"), new("F02", "Zwei", "gebaut", "T2"), new("F03", "Drei", "offen", null)] };
        var d = ProgramCatalog.Docs(e);
        Assert.Equal(ProgramCatalog.DocFiles.OrderBy(x => x), d.Keys.OrderBy(x => x));
        Assert.Contains("Noch kein Testlauf", d["TEST_RESULTS.md"]); Assert.DoesNotContain("| Getestet |", d["IMPLEMENTATION_STATUS.md"]);
        Assert.Contains("| F03 | Drei | Geplant |", d["IMPLEMENTATION_STATUS.md"]); Assert.Contains("keine eigene Arbeitsfläche", d["README.md"]);
        var dir = Path.Combine(Path.GetTempPath(), "trendo-trx-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var trx = Path.Combine(dir, "a.trx");
            File.WriteAllText(trx, "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results><UnitTestResult testName=\"T1\" outcome=\"Passed\" /><UnitTestResult testName=\"T2\" outcome=\"Failed\" /></Results></TestRun>");
            var o = ProgramCatalog.ReadTrx([trx]);
            var d2 = ProgramCatalog.Docs(e, o, "a.trx");
            Assert.Contains("| F01 | Eins | Getestet |", d2["IMPLEMENTATION_STATUS.md"]); Assert.Contains("| F02 | Zwei | Implementiert |", d2["IMPLEMENTATION_STATUS.md"]);
            Assert.Contains("Bestanden 1 · Fehlgeschlagen 1", d2["TEST_RESULTS.md"]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Programmkatalog: mitgeliefert, jedes Programm mit Studio, Bereich und Produktionspaket im Repository")]
    public void Shipped_catalog()
    {
        var all = ProgramCatalog.Load(); Assert.True(all.Count > 50, $"{all.Count}");
        Assert.All(all, e => { Assert.False(string.IsNullOrEmpty(e.StudioName)); Assert.False(string.IsNullOrEmpty(e.Area)); });
        Assert.Equal(all.Count, all.Select(e => e.Id).Distinct().Count());
        foreach (var e in all) foreach (var f in ProgramCatalog.DocFiles) Assert.True(File.Exists(Path.Combine(Repo(), "docs", "programme", e.Id, f)), $"{e.Id}/{f}");
        Assert.Contains(all, e => e.Id == "architektur" && e.Type.Contains("Grundriss-Zeichenfläche") && e.Shell);
    }
}
