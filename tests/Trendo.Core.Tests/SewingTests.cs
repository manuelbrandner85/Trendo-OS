using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 7 – SPEC-08: Schnittmuster mit FreeSewing 4. Antworten in echter Form; Echtlauf nur nach Freigabe.</summary>
public class SewingTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-sewing-" + Guid.NewGuid().ToString("N"));
    public SewingTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    const string Answer = """{"schnitt":"teagan","groesse":"cisFemaleAdult36","ergaenzt":["biceps","neck","shoulderSlope"],"pflicht":["biceps","chest","hips","neck","shoulderSlope","waist"],"fehler":[],"breite_mm":812.4,"hoehe_mm":935.2}""";
    static readonly Dictionary<string, double> Cm = new() { ["chest"] = 92, ["waist"] = 76, ["hip"] = 100 };

    [Fact(DisplayName = "Schnitt +1: Trendo-Maße (cm) → FreeSewing (mm); „hip“ wird „hips“, Schulterneigung bleibt in Grad, leere Maße fallen weg")]
    public void Convert()
    {
        var m = Sewing.ToFreeSewing(new Dictionary<string, double> { ["chest"] = 92, ["hip"] = 100.5, ["shoulderSlope"] = 13, ["waist"] = 0, ["unbekannt"] = 5 });
        Assert.Equal(new Dictionary<string, double> { ["chest"] = 920, ["hips"] = 1005, ["shoulderSlope"] = 13 }, m);
    }

    [Fact(DisplayName = "Schnitt +2: Antwort lesen – Standardgröße, ergänzte Maße deutsch im Kurztext, Bogengröße in cm")]
    public void Parse()
    {
        var r = Sewing.Parse(Answer);
        Assert.Equal(("teagan", "cisFemaleAdult36", 3, 6), (r.Design, r.Size, r.Supplemented.Count, r.Required.Count));
        Assert.Equal("Schnittbogen 81 × 94 cm – aus Standardgröße ergänzt: Oberarmumfang, Halsumfang, Schulterneigung (Grad).", r.Summary);
        Assert.EndsWith("alle Pflichtmaße von dir.", (r with { Supplemented = [] }).Summary);
    }

    [Fact(DisplayName = "Schnitt +3: Lauf übergibt Startskript, Schnitt, Maßdatei (mm), Körperform, Nahtzugabe und Ziel; Maßdatei wird danach gelöscht")]
    public void Run_args()
    {
        IReadOnlyList<string>? seen = null; string? measures = null; var svg = Path.Combine(_dir, "out", "t.svg");
        var (r, msg) = Sewing.Draft("node", Path.Combine(_dir, "fs"), "teagan", Cm, false, 10, svg, run: (_, a, _) =>
        {
            seen = a; measures = File.ReadAllText(a[2]); File.WriteAllText(a[5], "<svg/>"); return new Cli.Result(0, Answer, "");
        });
        Assert.NotNull(r); Assert.StartsWith("Schnittbogen 81 × 94 cm", msg);
        Assert.Equal([Sewing.RunnerName, "teagan"], seen!.Take(2).Select(Path.GetFileName)); Assert.Equal(["frau", "10", svg], seen.Skip(3));
        Assert.Contains("\"chest\":920", measures); Assert.Contains("\"hips\":1000", measures);
        Assert.False(File.Exists(seen[2])); Assert.Equal(Sewing.Runner, File.ReadAllText(Path.Combine(_dir, "fs", Sewing.RunnerName)));
    }

    [Fact(DisplayName = "Schnitt +4: 11 Schnitte mit deutschem Namen; Einrichtung mit fester Fassung und ohne Installationsskripte; alle 38 Maße übersetzt")]
    public void Catalog()
    {
        Assert.Equal(11, Sewing.Designs.Length); Assert.Contains(("teagan", "T-Shirt"), Sewing.Designs);
        var a = Sewing.InstallArgs("x"); Assert.Contains("--ignore-scripts", a); Assert.Contains("@freesewing/core@" + Sewing.Version, a); Assert.Contains("@freesewing/sandy@" + Sewing.Version, a);
        Assert.Equal(38, Sewing.German.Count); Assert.Equal("Rückenlänge", Sewing.Name("hpsToWaistBack")); Assert.Equal("neuesMass", Sewing.Name("neuesMass"));
    }

    [Fact(DisplayName = "Schnitt −1: unbekannter Schnitt, fehlender oder unplausibler Brustumfang, fehlende Einrichtung, Fehler und kaputte Antwort → klare Meldung")]
    public void Errors()
    {
        var svg = Path.Combine(_dir, "t.svg");
        Assert.Equal("Unbekannter Schnitt.", Sewing.Draft("node", _dir, "../boese", Cm, false, 10, svg).Message);
        Assert.StartsWith("Bitte den Brustumfang", Sewing.Draft("node", _dir, "teagan", new Dictionary<string, double> { ["waist"] = 70 }, false, 10, svg).Message);
        Assert.StartsWith("Bitte den Brustumfang", Sewing.Draft("node", _dir, "teagan", new Dictionary<string, double> { ["chest"] = 900 }, false, 10, svg).Message);
        Assert.Equal("FreeSewing ist noch nicht eingerichtet.", Sewing.Draft("node", _dir, "teagan", Cm, false, 10, svg).Message);
        Assert.StartsWith("FreeSewing meldet: Cannot find", Sewing.Draft("node", _dir, "teagan", Cm, false, 10, svg, run: (_, _, _) => new Cli.Result(1, "", "Cannot find package")).Message);
        Assert.Equal("FreeSewing hat keine lesbare Antwort geliefert.", Sewing.Draft("node", _dir, "teagan", Cm, false, 10, svg, run: (_, _, _) => new Cli.Result(0, "kaputt", "")).Message);
        Assert.Equal("FreeSewing hat keine Datei geschrieben.", Sewing.Draft("node", _dir, "teagan", Cm, false, 10, svg, run: (_, _, _) => new Cli.Result(0, Answer, "")).Message);
    }
}

/// <summary>Echter Lauf mit FreeSewing 4.10.2 (findet den Ordner automatisch; ohne Freigabe/Einrichtung übersprungen).</summary>
public class SewingRealTests
{
    public sealed class SewingFactAttribute : FactAttribute { public SewingFactAttribute() { if (EchtWerkzeuge.SewingDir() is null || EchtWerkzeuge.Node() is null) Skip = "Übersprungen – FreeSewing oder Node nicht gefunden."; } }

    [SewingFact(DisplayName = "Schnitt echt: T-Shirt (Teagan) und Hose (Titan) für Brust 92/Taille 76/Hüfte 100 → SVG in Millimetern, plausible Bogengröße, ergänzte Maße genannt")]
    public void Real_draft()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-sewreal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cm = new Dictionary<string, double> { ["chest"] = 92, ["waist"] = 76, ["hip"] = 100 };
            foreach (var design in new[] { "teagan", "titan" })
            {
                var svg = Path.Combine(dir, design + ".svg");
                var (r, msg) = Sewing.Draft(EchtWerkzeuge.Node()!, EchtWerkzeuge.SewingDir()!, design, cm, false, 10, svg);
                Assert.True(r is not null, design + ": " + msg);
                Assert.Empty(r!.Errors); Assert.StartsWith("cisFemaleAdult", r.Size);
                Assert.InRange(r.WidthMm, 200, 3000); Assert.InRange(r.HeightMm, 200, 3000);
                var text = File.ReadAllText(svg); Assert.Contains("<svg", text); Assert.Contains("mm\"", text);
                Assert.DoesNotContain("chest", r.Supplemented); Assert.All(r.Supplemented, m => Assert.Contains(m, r.Required));
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
