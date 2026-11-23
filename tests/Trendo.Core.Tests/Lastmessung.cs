using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0760: lastrobuste Zeitmessung für Zeittests. Die Grenze bleibt, wie sie ist – sie wird nur um den Faktor verlängert, um den der
/// Rechner gerade nachweislich langsamer ist: unmittelbar vor und nach der gemessenen Arbeit läuft eine feste Vergleichsarbeit (Ganzzahl-Rechnung,
/// einfädig oder auf allen Kernen – wie die gemessene Arbeit), und deren Dauer wird mit dem ungestört gemessenen Bezugswert dieses Rechners
/// verglichen. Es zählt die kleinere der beiden Vergleichsmessungen (vorsichtig: nur Last, die vorher UND nachher da war). Faktor ≥ 1.
/// <para>Warum so: Wandzeit allein schlägt unter Fremdlast (Volltest, parallele Sitzungen) fehl; reine CPU-Zeit übersähe Verlangsamungen durch
/// Warten (Sperren, Schlafen) und misst bei paralleler Arbeit die Summe aller Kerne; eine eigene nicht parallele Sammlung hilft nur gegen Last im
/// eigenen Testprozess (die Fix16/Fix20-Tests lagen schon darin und fielen trotzdem). Die Vergleichsarbeit merkt jede Last – egal woher – und
/// bleibt von einer Verlangsamung des geprüften Codes unberührt: ein Rückschritt im Code wird weiter rot.</para></summary>
public static class Lastmessung
{
    /// <summary>Dauer der Vergleichsarbeit auf dem Bezugsrechner (16 logische Kerne): kleinster Wert aus 20 Messungen am 23.11.2026
    /// (Rohwerte in reports/T-0760/zeittests/bezug-ruhig.txt). Gemessen wurde bei etwa 35 % Fremdlast – ungestört wäre die Vergleichsarbeit eher
    /// schneller; der Lastfaktor fällt dadurch eher zu klein aus (strenger), nie zu groß. Auf schnelleren Rechnern ist der Faktor 1 – die Grenze gilt unverändert.</summary>
    public const double BaseSingleMs = 55, BaseParallelMs = 86;
    const long Work = 40_000_000;

    static long Spin(long n) { ulong x = 88172645463325252UL; for (long i = 0; i < n; i++) { x ^= x << 13; x ^= x >> 7; x ^= x << 17; } return (long)(x & 1); }

    /// <summary>Dauer der Vergleichsarbeit in ms – einfädig, oder parallel wie der geprüfte Code (Parallel.For über kleine Stücke der Gesamtarbeit,
    /// also dieselbe Thread-Pool-Planung: was den Code unter Last ausbremst, bremst auch die Vergleichsarbeit).</summary>
    public static double Reference(bool parallel)
    {
        var sw = Stopwatch.StartNew(); long sink = 0;
        if (!parallel) sink = Spin(Work);
        else { const int Pieces = 64; Parallel.For(0, Pieces * Environment.ProcessorCount, _ => Interlocked.Add(ref sink, Spin(Work / Pieces))); }
        GC.KeepAlive(sink);
        return sw.Elapsed.TotalMilliseconds;
    }

    public sealed record Messung(long Ms, double Faktor, double RefVor, double RefNach)
    {
        public double Grenze(long limit) => limit * Faktor;
        public override string ToString() => $"{Ms} ms (Lastfaktor {Faktor:0.00}, Vergleich {RefVor:0} / {RefNach:0} ms)";
    }

    /// <summary>Misst <paramref name="a"/> mit Vergleichsmessung davor und danach.</summary>
    public static Messung Measure(Action a, bool parallel)
    {
        var r0 = Reference(parallel);
        var sw = Stopwatch.StartNew(); a(); var ms = sw.ElapsedMilliseconds;
        var r1 = Reference(parallel);
        return new Messung(ms, Math.Max(1, Math.Min(r0, r1) / (parallel ? BaseParallelMs : BaseSingleMs)), r0, r1);
    }

    /// <summary>Kleinste von <paramref name="n"/> Messungen (bezogen auf ihre Grenze) – wie die bisherigen Min2/Min3.</summary>
    public static Messung Best(int n, Action a, bool parallel)
    {
        Messung? best = null;
        for (var i = 0; i < n; i++) { var m = Measure(a, parallel); if (best is null || m.Ms / m.Faktor < best.Ms / best.Faktor) best = m; }
        return best!;
    }

    /// <summary>Prüft Dauer &lt; Grenze × Lastfaktor und schreibt Messwert, Faktor und Grenze ins Protokoll.</summary>
    public static void Below(ITestOutputHelper log, string name, Messung m, long limit)
    {
        log.WriteLine($"{name}: {m}, Grenze {limit} ms × {m.Faktor:0.00} = {m.Grenze(limit):0} ms");
        Assert.True(m.Ms < m.Grenze(limit), $"{name}: {m} – Grenze {limit} ms × {m.Faktor:0.00}");
    }
}
