using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0281: Licht für die 3D-Vorschauen (Modell-Prüfer, CAD, Grundriss) – linear gerechnet wie ein Renderer:
/// Hauptlicht (Sonne, warm), Himmel/Boden-Umgebung je nach Flächenneigung, kühles Fülllicht von der Gegenseite, feiner Glanz,
/// Kontaktverdunklung zum Boden hin; danach weiche Kompression der Lichter und sRGB. Welt: Z oben.</summary>
public static class Shading
{
    public static readonly Vec3 Key = new Vec3(-0.55, -0.40, 0.73).Norm(); // vorn links oben – schräg zur üblichen Kamera (vorn rechts), damit Schatten sichtbar fallen
    static readonly Vec3 Fill = new Vec3(0.65, -0.45, 0.30).Norm();

    static double Lin(byte c) { var v = c / 255.0; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
    static byte Srgb(double v)
    {
        v = v / (1 + 0.18 * v); // Lichter weich zusammendrücken statt hart abschneiden
        v = Math.Clamp(v, 0, 1); var s = v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
        return (byte)Math.Clamp((int)Math.Round(s * 255), 0, 255);
    }

    /// <summary>Farbe einer Fläche. <paramref name="n"/>: Einheitsnormale (zur Kamera gedreht), <paramref name="view"/>: Richtung zur Kamera,
    /// <paramref name="ao"/>: 0–1 Umgebungsverdeckung, <paramref name="gloss"/>: Glanzstärke (0 matt … 1 lackiert).</summary>
    public static Color Shade(Color baseSrgb, Vec3 n, Vec3 view, double ao = 1, double gloss = 0.35)
    {
        var k = Math.Max(0, Vec3.Dot(n, Key));
        var hemi = 0.5 + 0.5 * n.Z;
        var fill = Math.Max(0, Vec3.Dot(n, Fill)) * 0.16;
        var h = (Key + view).Norm(); var spec = gloss * 0.09 * Math.Pow(Math.Max(0, Vec3.Dot(n, h)), 36) * (k > 0 ? 1 : 0);
        double C(byte c, double keyTint, double sky, double ground, double fillTint)
        {
            var light = k * 1.25 * keyTint + (ground + (sky - ground) * hemi) * 0.62 * ao + fill * fillTint;
            return Lin(c) * light * (0.55 + 0.45 * ao) + spec;
        }
        return Color.FromRgb(Srgb(C(baseSrgb.R, 1.00, 0.60, 0.30, 0.85)), Srgb(C(baseSrgb.G, 0.97, 0.66, 0.28, 0.92)), Srgb(C(baseSrgb.B, 0.91, 0.78, 0.25, 1.05)));
    }

    /// <summary>Kontaktverdunklung: nah am Boden dunkler (bis 60 %), ab <paramref name="reach"/> über dem Boden keine.</summary>
    public static double GroundAo(double heightAboveGround, double reach)
    {
        var t = Math.Clamp(heightAboveGround / Math.Max(1e-9, reach), 0, 1); t = t * t * (3 - 2 * t);
        return 0.6 + 0.4 * t;
    }

    /// <summary>Punkt entlang des Hauptlichts auf die Bodenebene z = <paramref name="ground"/> werfen (für Schatten).</summary>
    public static Vec3 OnGround(Vec3 p, double ground, Vec3? light = null)
    {
        var l = light ?? Key; var t = (p.Z - ground) / Math.Max(0.05, l.Z);
        return new Vec3(p.X - l.X * t, p.Y - l.Y * t, ground);
    }
}
