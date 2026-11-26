using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0772 Punkte 2 und 3: Vorauswahl der Doppelprüfung bei vielen fast gleichen Umrissen, Abstandsprüfung langer Gravurlinien entlang der Linie
/// und die schnelle Absage „nichts Fremdes im Rahmen der Bahn“. Die Ergebnisgleichheit mit 0.9.110 belegt der Vergleichslauf in reports/T-0772;
/// hier wird geprüft, dass dabei nichts verloren geht (echte Doppel, echte Abstandsfehler).</summary>
public class T0772FraesenTests
{
    static List<(double X, double Y)> Notched(double x, double depth) =>
        [(0, 0), (x - 0.0004, 0), (x, depth), (x + 0.0004, 0), (100, 0), (100, 100), (0, 100)];

    [Fact(DisplayName = "T-0772: 300 fast gleiche Umrisse mit versteckter Kerbe – echte Doppel (auch umgedreht, anderer Anfang) werden weiter gefunden")]
    public void Doppel_unter_vielen_fast_gleichen()
    {
        var cs = new List<CadContour>();
        for (var i = 0; i < 300; i++) cs.Add(new(Notched(1 + 98 * (i + 0.5) / 300, 0.5), true, "0"));
        var orig = Notched(1 + 98 * 17.5 / 300, 0.5);
        cs.Add(new(new(orig), true, "0"));
        var rev = new List<(double X, double Y)>(orig); rev.Reverse(); cs.Add(new(rev, true, "0"));
        var rot = orig.Skip(3).Concat(orig.Take(3)).ToList(); cs.Add(new(rot, true, "0"));
        var r = CncCam.Generate(cs, new CamSettings { ToolD = 6 });
        var dups = r.Warnings.Where(w => w.Contains("doppelt gezeichnet")).ToList();
        Assert.Equal(3, dups.Count);
        Assert.All(dups, w => Assert.Contains("deckungsgleich mit Kontur 18)", w));
        Assert.Equal(new[] { "Kontur 301:", "Kontur 302:", "Kontur 303:" }, dups.Select(w => w[..(w.IndexOf(':') + 1)]).ToArray());
    }

    [Fact(DisplayName = "T-0772: lange schräge Gravurlinie – zu nahe Löcher werden weiter erkannt, freie Linie wird gefräst")]
    public void Lange_gravurlinie_abstand()
    {
        var cs = new List<CadContour>
        {
            new([(0, 0), (2000, 0), (2000, 1500), (0, 1500)], true, "0"),
            new([(10, 10), (1990, 1490)], false, "0"), // schräg über die ganze Platte, kreuzt das Loch bei (1000, 752)
            new([(10, 1400), (600, 1450)], false, "0"), // frei
        };
        for (var i = 0; i < 400; i++) // viele Löcher fern der Linien
        {
            var (x, y) = (60 + 47.0 * (i % 40), 300 + 20.0 * (i / 40));
            if (Math.Abs(y - (10 + (x - 10) * 1480 / 1980)) > 20 && y < 1380) cs.Add(new(Circle(x, y, 2), true, "0"));
        }
        cs.Add(new(Circle(1000, 10 + 990 * 1480.0 / 1980, 2), true, "0")); // auf der Linie
        var r = CncCam.Generate(cs, new CamSettings { ToolD = 3, Mode = "auf" });
        Assert.Contains(r.Warnings, w => w.StartsWith("Kontur 2 ausgelassen: zu nah an Kontur " + cs.Count + " ") || w.StartsWith("Kontur " + cs.Count + " ausgelassen: zu nah an Kontur 2 "));
        Assert.Contains(r.Paths, p => p.Source == 2);
    }

    static List<(double X, double Y)> Circle(double x, double y, double rr) => Enumerable.Range(0, 64).Select(k => (x + rr * Math.Cos(k * Math.PI / 32), y + rr * Math.Sin(k * Math.PI / 32))).ToList();
}
