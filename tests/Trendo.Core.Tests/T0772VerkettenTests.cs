using System.Diagnostics;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0772 Punkt 1: Verketten mit vielen Spalten („k5_dicht“) hing – jede Runde rechnete alle Enden, Paare und Mehrdeutigkeiten neu.
/// Jetzt schrittweise; das Ergebnis muss Zeichen für Zeichen dem bisherigen Verfahren (<see cref="CadContoursAlt"/>, Kopie aus 0.9.110) gleichen.</summary>
public class T0772VerkettenTests(ITestOutputHelper output)
{
    static string Dump(List<CadContour> cs, List<string> notes) =>
        string.Join("\n", cs.Select(c => $"{c.Closed}|{c.Layer}|{c.JunctionGroup}|" + string.Join(";", c.Pts.Select(p => p.X.ToString("R") + "," + p.Y.ToString("R")))))
        + "\n--\n" + string.Join("\n", notes);

    /// <summary>Zufällige Zeichnungen, die alle Fälle treffen: genau passende Enden, Spalten unter/über der Grenze, gleich weite Spalten (Raster),
    /// mehrdeutige Häufungen, zwei Ebenen, Linien-Stöße, Ketten, die sich schließen.</summary>
    static List<CadContour> Random(Random r, int n, double tol)
    {
        var res = new List<CadContour>();
        string Lay() => r.Next(5) == 0 ? "B" : "0";
        double Snap(double v) => r.Next(3) == 0 ? Math.Round(v / tol) * tol : v; // Raster: gleich weite Spalten
        for (var i = 0; i < n; i++)
        {
            var kind = r.Next(6);
            if (kind == 0) // Rechteck aus vier Linien mit gleich großen Spalten
            {
                var (x, y, w, h, g) = (r.Next(40) * 3.0, r.Next(40) * 3.0, 1 + r.Next(3), 1 + r.Next(3), tol * (r.Next(4) * 0.25));
                var l = Lay();
                res.Add(new([(x + g, y), (x + w, y)], false, l)); res.Add(new([(x + w, y + g), (x + w, y + h)], false, l));
                res.Add(new([(x + w - g, y + h), (x, y + h)], false, l)); res.Add(new([(x, y + h - g), (x, y)], false, l));
            }
            else
            {
                var m = 2 + (kind == 1 ? r.Next(3) : 0);
                var pts = new List<(double X, double Y)>();
                var (x, y) = (Snap(r.NextDouble() * 12), Snap(r.NextDouble() * 12));
                for (var k = 0; k < m; k++) { pts.Add((x, y)); x = Snap(x + (r.NextDouble() - 0.5) * 6); y = Snap(y + (r.NextDouble() - 0.5) * 6); }
                res.Add(new(pts, false, Lay()));
                if (r.Next(4) == 0) // ein zweites Stück, das am Ende fast oder genau anschließt
                {
                    var e = pts[^1]; var j = r.Next(3) switch { 0 => 0, 1 => tol * r.NextDouble(), _ => tol * (1 + r.NextDouble()) };
                    res.Add(new([(e.X + j, e.Y), (e.X + 2, e.Y + 1)], false, res[^1].Layer));
                }
            }
        }
        return res;
    }

    [Fact(DisplayName = "T-0772: Verketten gleich wie 0.9.110 – 1200 Zufallszeichnungen, mit und ohne Stoß-Erkennung, Ketten, Meldungen, Reihenfolge")]
    public void Gleiches_ergebnis_wie_bisher()
    {
        int rounds = 0, exact = 0, cases = 0, notesSeen = 0, bridged = 0;
        for (var seed = 0; seed < 1200; seed++)
        {
            var r = new Random(seed);
            var tol = seed % 3 == 0 ? 0.1 : seed % 3 == 1 ? 0.01 : 0.5;
            var pieces = Random(r, 5 + r.Next(seed % 10 == 0 ? 400 : 60), tol);
            foreach (var junctions in new[] { true, false })
            {
                var n1 = new List<string>(); var n2 = new List<string>();
                var alt = CadContoursAlt.Chain(pieces, tol, n1, junctions, default);
                var neu = CadContours.Chain(pieces, tol, n2, junctions);
                var (ro, ex, _, bo) = CadContours.LastBridgeStats;
                Assert.False(bo);
                rounds += ro; exact += ex; cases++; notesSeen += n2.Count; bridged += alt.Count(c => c.Pts.Count > 0) < pieces.Count ? 1 : 0;
                var (a, b) = (Dump(alt, n1), Dump(neu, n2));
                Assert.True(a == b, $"Abweichung bei Zufall {seed}, Stoß-Erkennung {junctions}, tol {tol}");
            }
        }
        output.WriteLine($"{cases} Fälle, Runden {rounds}, davon genau wie bisher gerechnet {exact}, Meldungen {notesSeen}, mit Verbindungen {bridged}");
        Assert.True(rounds - exact > rounds / 5 && exact > rounds / 20, $"beide Wege prüfen: {rounds - exact} schnell, {exact} genau wie bisher von {rounds}");
        Assert.True(notesSeen > 100);
    }

    /// <summary>Nachbau von „k5_dicht“: Platte, dichtes kleines Vieleck, n Linien von X 0 nach X 1000 in einem Band um Y 500 (mittlerer Abstand 0,05 mm).</summary>
    static List<CadContour> K5(int n)
    {
        var r = new Random(7000 + n); var res = new List<CadContour>();
        var hb = n * 0.05;
        for (var i = 0; i < n; i++) res.Add(new([(0, 500 + hb * r.NextDouble()), (1000, 500 + hb * r.NextDouble())], false, "0"));
        return res;
    }

    [Fact(DisplayName = "T-0772: k5_dicht mit 3000 Linien gleich wie bisher und mindestens 5-mal schneller (Median aus 3, relativ gemessen)")]
    public void K5_3000_gleich_und_schneller()
    {
        var pieces = K5(3000);
        var tA = new List<long>(); var tN = new List<long>(); string? a = null, b = null;
        for (var k = 0; k < 3; k++)
        {
            var n1 = new List<string>(); var sw = Stopwatch.StartNew(); var alt = CadContoursAlt.Chain(pieces, 0.1, n1, true, default); tA.Add(sw.ElapsedMilliseconds);
            var n2 = new List<string>(); sw.Restart(); var neu = CadContours.Chain(pieces, 0.1, n2, true); tN.Add(sw.ElapsedMilliseconds);
            a = Dump(alt, n1); b = Dump(neu, n2);
            Assert.Equal(a, b);
        }
        long Med(List<long> l) => l.Order().ElementAt(1);
        output.WriteLine($"alt {string.Join("/", tA)} ms, neu {string.Join("/", tN)} ms, Runden {CadContours.LastBridgeStats.Rounds}, genau {CadContours.LastBridgeStats.Exact}");
        Assert.True(Med(tN) * 5 <= Med(tA) + 5, $"alt Median {Med(tA)} ms, neu Median {Med(tN)} ms");
    }

    [Fact(DisplayName = "T-0772: k5_dicht mit 10 000 Linien wird in begrenzter Zeit fertig (Grenze 120 s; bisher quadratisch)")]
    public void K5_30000_fertig()
    {
        var pieces = K5(10000); var notes = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var sw = Stopwatch.StartNew();
        var res = CadContours.Chain(pieces, 0.1, notes, true, cts.Token);
        output.WriteLine($"{sw.ElapsedMilliseconds} ms, {res.Count} Konturen, {notes.Count} Meldungen, Runden {CadContours.LastBridgeStats.Rounds}, genau {CadContours.LastBridgeStats.Exact}, Schritte {CadContours.LastBridgeStats.Work}");
        Assert.True(res.Count > 0 && notes.Count > 0);
    }

    [Fact(DisplayName = "T-0772: Budget erschöpft – keine weiteren Spalten, klare Meldung, kein Hängen; Abbruch wirkt sofort")]
    public void Budget_und_abbruch()
    {
        var pieces = K5(3000);
        try
        {
            CadContours.BridgeBudget = 100_000; var notes = new List<string>();
            var res = CadContours.Chain(pieces, 0.1, notes, true);
            Assert.Contains(CadContours.BridgeBudgetNote, notes);
            Assert.True(CadContours.LastBridgeStats.BudgetOut);
            Assert.Equal(2 * pieces.Count, res.Sum(c => c.Pts.Count)); // nichts geht verloren: alle Linienenden noch da
        }
        finally { CadContours.BridgeBudget = null; }
        using var cts = new CancellationTokenSource(); cts.CancelAfter(200);
        var sw = Stopwatch.StartNew();
        Assert.ThrowsAny<OperationCanceledException>(() => CadContours.Chain(K5(30000), 0.1, new List<string>(), true, cts.Token));
        Assert.True(sw.ElapsedMilliseconds < 5000, $"{sw.ElapsedMilliseconds} ms");
    }
}
