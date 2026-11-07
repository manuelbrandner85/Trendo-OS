namespace Trendo.Core;

/// <summary>Alle Studios, Runde 4 – SPEC-06: FSRS-Planer (Free Spaced Repetition Scheduler, Version 5) als eigener C#-Code.
/// Algorithmus und Standardgewichte stammen von Open Spaced Repetition (MIT, Vorlage py-fsrs, siehe THIRD_PARTY.md).
/// Rechnet in ganzen Tagen wie der übrige Lernplan; „Falsch“ kommt am nächsten Tag wieder.</summary>
public static class Fsrs
{
    /// <summary>FSRS-5-Standardgewichte w0…w18.</summary>
    public static readonly double[] W = [0.40255, 1.18385, 3.173, 15.69105, 7.1949, 0.5345, 1.4604, 0.0046, 1.54575, 0.1192, 1.01925, 1.9395, 0.11, 0.29605, 2.2698, 0.2315, 2.9898, 0.51655, 0.6621];
    const double Decay = -0.5;
    static readonly double Factor = 19.0 / 81.0;
    public const int MaxDays = 36500;

    /// <summary>Bewertung 1–4 (Nochmal, Schwer, Gut, Leicht) aus der Trendo-Bewertung.</summary>
    public static int Rating(Grade g) => g switch { Grade.Falsch => 1, Grade.Schwer => 2, Grade.Gut => 3, _ => 4 };

    /// <summary>Abrufwahrscheinlichkeit nach <paramref name="days"/> Tagen bei Stabilität <paramref name="s"/>.</summary>
    public static double Retrievability(double days, double s) => Math.Pow(1 + Factor * Math.Max(0, days) / s, Decay);

    /// <summary>Abstand in Tagen, nach dem die Abrufwahrscheinlichkeit auf <paramref name="retention"/> gesunken ist (bei 0,9 = Stabilität).</summary>
    public static int Interval(double s, double retention = 0.9)
    {
        if (retention is <= 0.5 or >= 0.99) throw new ArgumentOutOfRangeException(nameof(retention), "Die Behaltensrate muss zwischen 0,5 und 0,99 liegen.");
        var d = s / Factor * (Math.Pow(retention, 1 / Decay) - 1);
        return (int)Math.Clamp(Math.Round(d), 1, MaxDays);
    }

    static double ClampD(double d) => Math.Clamp(d, 1, 10);
    public static double InitStability(int r) => Math.Max(W[r - 1], 0.1);
    public static double InitDifficulty(int r) => ClampD(W[4] - Math.Exp(W[5] * (r - 1)) + 1);

    public static double NextDifficulty(double d, int r)
    {
        var delta = -W[6] * (r - 3);
        var next = d + delta * (10 - d) / 9; // lineare Dämpfung (FSRS-5)
        return ClampD(W[7] * InitDifficulty(4) + (1 - W[7]) * next); // Rückkehr zum Mittel
    }

    public static double RecallStability(double d, double s, double rr, int r)
    {
        var hard = r == 2 ? W[15] : 1; var easy = r == 4 ? W[16] : 1;
        return s * (1 + Math.Exp(W[8]) * (11 - d) * Math.Pow(s, -W[9]) * (Math.Exp(W[10] * (1 - rr)) - 1) * hard * easy);
    }

    public static double ForgetStability(double d, double s, double rr) =>
        Math.Min(s, W[11] * Math.Pow(d, -W[12]) * (Math.Pow(s + 1, W[13]) - 1) * Math.Exp(W[14] * (1 - rr)));

    /// <summary>Wiederholung am selben Tag (Kurzzeit-Formel aus FSRS-5).</summary>
    public static double SameDayStability(double s, int r) => s * Math.Exp(W[17] * (r - 3 + W[18]));

    /// <summary>Ein Schritt: neuer Zustand (Stabilität, Schwierigkeit) und Abstand in Tagen. <paramref name="s"/> null = neue Karte.</summary>
    public static (double S, double D, int Days) Step(double? s, double? d, double elapsedDays, Grade g, double retention = 0.9)
    {
        var r = Rating(g);
        double ns, nd;
        if (s is not { } s0 || d is not { } d0) { ns = InitStability(r); nd = InitDifficulty(r); }
        else
        {
            nd = NextDifficulty(d0, r);
            if (elapsedDays < 1) ns = SameDayStability(s0, r);
            else
            {
                var rr = Retrievability(elapsedDays, s0);
                ns = r == 1 ? ForgetStability(d0, s0, rr) : RecallStability(d0, s0, rr, r);
            }
        }
        ns = Math.Clamp(ns, 0.1, MaxDays);
        return (ns, nd, r == 1 ? 1 : Interval(ns, retention));
    }

    /// <summary>Übernahme einer SM-2-Karte: Stabilität ≈ bisheriger Abstand, Schwierigkeit aus der Leichtigkeit (2,5 → 5; 1,3 → 10). Näherung.</summary>
    public static (double S, double D) FromSm2(int intervalDays, double ease) =>
        (Math.Clamp(intervalDays, 0.5, MaxDays), ClampD(5 + (2.5 - ease) * (5 / 1.2)));
}
