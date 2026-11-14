using System.Globalization;

namespace Trendo.Core.Programs;

/// <summary>T-0650, E5: Bilder aus dem Testlauf automatisch auf sichtbare Fehler prüfen – schwarzes Bild, einfarbiges Bild (Welt oder Kamera fehlt),
/// leuchtend pinke Flächen (so zeigen Godot und Unity fehlende Materialien; Unreals graues Schachbrett wird nicht erkannt) und starke Abweichung vom Bild
/// desselben Schritts im Vergleichslauf (nur ein Hinweis: eine kleine fehlende Figur bleibt darunter, eine andere Kameralage kann darüber liegen).
/// Jeder Befund nennt Bild und Messwert. Reine Pixel-Messung, keine KI: Flackern misst der Testspieler-Bot im Spiel selbst (TRENDO-FLACKERN).</summary>
public static class GameShotCheck
{
    public sealed record Stats(int Width, int Height, double Mean, double Spread, double DarkShare, double PinkShare);
    public sealed record Finding(string Shot, string File, string Kind, bool Ok, string Text, double Value);

    /// <summary>Grenzwerte: Helligkeit 0–255, Anteile 0–1.</summary>
    public const double BlackMean = 8, BlackShare = 0.97, FlatSpread = 2.5, PinkLimit = 0.01, ChangedLimit = 0.25, ChangedStep = 40;

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Höchstens etwa 250 000 Messpunkte, gleichmäßig über das Bild verteilt.</summary>
    static int Step(RgbImage i) => Math.Max(1, (int)Math.Sqrt((double)i.Width * i.Height / 250_000));

    static double Luma(byte[] p, int o) => 0.299 * p[o] + 0.587 * p[o + 1] + 0.114 * p[o + 2];

    public static Stats Measure(RgbImage img)
    {
        var s = Step(img); long n = 0; double sum = 0, sq = 0; long dark = 0, pink = 0;
        for (var y = 0; y < img.Height; y += s)
            for (var x = 0; x < img.Width; x += s)
            {
                var o = (y * img.Width + x) * 3; var l = Luma(img.Rgb, o);
                n++; sum += l; sq += l * l;
                if (l < 16) dark++;
                int r = img.Rgb[o], g = img.Rgb[o + 1], b = img.Rgb[o + 2];
                if (r > 190 && b > 190 && g < 80) pink++;
            }
        if (n == 0) return new(img.Width, img.Height, 0, 0, 1, 0);
        var mean = sum / n;
        return new(img.Width, img.Height, mean, Math.Sqrt(Math.Max(0, sq / n - mean * mean)), (double)dark / n, (double)pink / n);
    }

    /// <summary>Befunde zu einem Bild. Ist alles unauffällig, steht genau ein Befund „in Ordnung“ mit den Messwerten da.</summary>
    public static List<Finding> Check(string label, string file)
    {
        RgbImage img;
        try { img = Vision.Load(file); }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException)
        { return [new(label, file, "lesbar", false, "Bild nicht lesbar: " + e.Message, double.NaN)]; }
        return Check(label, file, img);
    }

    public static List<Finding> Check(string label, string file, RgbImage img)
    {
        var st = Measure(img); var f = new List<Finding>();
        if (st.Mean < BlackMean && st.DarkShare > BlackShare)
            f.Add(new(label, file, "schwarz", false, $"Bild ist schwarz (Helligkeit {st.Mean.ToString("0.#", De)} von 255)", st.Mean));
        else if (st.Spread < FlatSpread)
            f.Add(new(label, file, "einfarbig", false, $"Bild ist einfarbig (Schwankung {st.Spread.ToString("0.#", De)}) – Welt oder Kamera fehlt?", st.Spread));
        if (st.PinkShare > PinkLimit)
            f.Add(new(label, file, "textur", false, $"{(st.PinkShare * 100).ToString("0.#", De)} % leuchtend pinke Fläche – so zeigen Godot und Unity fehlende Materialien (Hinweis)", st.PinkShare));
        if (f.Count == 0)
            f.Add(new(label, file, "ok", true, $"unauffällig (Helligkeit {st.Mean.ToString("0", De)}, Schwankung {st.Spread.ToString("0", De)})", st.Mean));
        return f;
    }

    /// <summary>Anteil der Fläche, die sich zwischen zwei Bildern deutlich ändert (Helligkeit um mehr als <see cref="ChangedStep"/>), auf gemeinsamem Raster.</summary>
    public static double Changed(RgbImage a, RgbImage b)
    {
        const int Grid = 160; long n = 0, changed = 0;
        var gh = Math.Max(1, Grid * Math.Min(a.Height, b.Height) / Math.Max(1, Math.Min(a.Width, b.Width)));
        for (var gy = 0; gy < gh; gy++)
            for (var gx = 0; gx < Grid; gx++)
            {
                int ax = gx * a.Width / Grid, ay = gy * a.Height / gh, bx = gx * b.Width / Grid, by = gy * b.Height / gh;
                var d = Math.Abs(Luma(a.Rgb, (ay * a.Width + ax) * 3) - Luma(b.Rgb, (by * b.Width + bx) * 3));
                n++; if (d > ChangedStep) changed++;
            }
        return n == 0 ? 0 : (double)changed / n;
    }

    /// <summary>Gleicher Schritt, gleicher Startwert: weicht das Bild stark vom Vergleichslauf ab, fehlt womöglich etwas (z. B. die Figur). Nur ein Hinweis.</summary>
    public static Finding? AgainstBefore(string label, string now, string before)
    {
        try
        {
            var share = Changed(Vision.Load(now), Vision.Load(before));
            return share > ChangedLimit ? new(label, now, "abweichung", false, $"weicht stark vom Vergleichslauf ab ({(share * 100).ToString("0", De)} % der Fläche) – bitte ansehen (Hinweis, kein Nachweis)", share) : null;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }
}
