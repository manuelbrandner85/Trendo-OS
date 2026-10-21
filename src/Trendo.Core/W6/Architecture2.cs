using System.Text;

namespace Trendo.Core;

/// <summary>SPEC-01 Ausbau: Fliesen, Tapete, Möbel, Heizlast, Licht, Kosten, Treppe, Wohnfläche, CSV.</summary>
public static class Interior
{
    /// <summary>F08: Fliesen (cm) für Fläche (m²) inkl. Fuge (mm) und Verschnitt (%); Pakete (m² je Paket); Fugenmörtel kg (Faustformel).</summary>
    public static (int Tiles, int Packs, double GroutKg) Tiles(double areaM2, double tileWcm, double tileHcm, double jointMm = 3, double wastePct = 10, double packM2 = 1.0, double tileThickMm = 8)
    {
        if (areaM2 <= 0 || tileWcm <= 0 || tileHcm <= 0) throw new ArgumentException("Fläche und Fliesenmaß müssen größer als 0 sein.");
        var cell = (tileWcm + jointMm / 10) * (tileHcm + jointMm / 10) / 10000; var n = areaM2 / cell * (1 + wastePct / 100);
        var tiles = (int)Math.Ceiling(Math.Round(n, 6)); var packs = packM2 <= 0 ? 0 : (int)Math.Ceiling(Math.Round(tiles * tileWcm * tileHcm / 10000 / packM2, 6));
        var grout = (tileWcm + tileHcm) * 10 / (tileWcm * 10 * tileHcm * 10) * jointMm * tileThickMm * 1.6 * areaM2; // kg
        return (tiles, packs, Math.Round(grout, 2));
    }

    /// <summary>F09: Tapetenrollen: Bahnen je Rolle = floor(Rollenlänge / (Höhe + Rapport + 5 cm)), Bahnen = ceil(Umfang abzügl. Türen/Fenster-Breite / Rollenbreite).</summary>
    public static (int Strips, int StripsPerRoll, int Rolls) Wallpaper(Room r, double rollLenM = 10.05, double rollWidthM = 0.53, double repeatCm = 0)
    {
        var len = r.Height + repeatCm / 100 + 0.05; var per = (int)Math.Floor(rollLenM / len);
        if (per < 1) throw new ArgumentException("Die Rolle ist kürzer als eine Bahn.");
        var width = Math.Max(0, r.Perimeter - r.Doors * Room.DoorWidth - r.Windows * 1.2 * 0.5); // Fenster: halbe Breite abziehbar
        var strips = (int)Math.Ceiling(Math.Round(width / rollWidthM, 6));
        return (strips, per, (int)Math.Ceiling((double)strips / per));
    }

    /// <summary>F10: Möbel (Breite × Tiefe in m) passt in den Raum, wenn davor/daneben der Gehfreiraum (Standard 0,9 m) bleibt.</summary>
    public static (bool Fits, string Why) FurnitureFits(Room r, double w, double d, double clearance = 0.9)
    {
        if (w <= 0 || d <= 0) throw new ArgumentException("Möbelmaße müssen größer als 0 sein.");
        foreach (var (a, b) in new[] { (r.Length, r.Width), (r.Width, r.Length) })
            if (w <= a && d + clearance <= b) return (true, $"Passt an die {Fach.F(a)}-m-Wand, davor bleiben {Fach.F(b - d)} m frei.");
        return w > Math.Max(r.Length, r.Width) || d > Math.Max(r.Length, r.Width) ? (false, "Das Möbel ist größer als der Raum.") : (false, $"Passt nur ohne die {Fach.F(clearance)} m Gehfreiraum.");
    }

    private static readonly Dictionary<string, double> HeatWm2 = new(StringComparer.OrdinalIgnoreCase) { ["altbau"] = 120, ["unsaniert"] = 120, ["teilsaniert"] = 90, ["neubau"] = 60, ["kfw"] = 40, ["passivhaus"] = 15 };
    /// <summary>F11: Heizlast grob: W/m² nach Standard, auf 2,5 m Raumhöhe bezogen.</summary>
    public static double HeatLoad(Room r, string standard)
    {
        if (!HeatWm2.TryGetValue(standard.Trim(), out var w)) throw new ArgumentException("Standard: " + string.Join(", ", HeatWm2.Keys));
        return Math.Round(r.Floor * w * r.Height / 2.5);
    }

    private static readonly Dictionary<string, double> Lux = new(StringComparer.OrdinalIgnoreCase) { ["wohnzimmer"] = 150, ["schlafzimmer"] = 100, ["küche"] = 300, ["bad"] = 200, ["büro"] = 500, ["flur"] = 100, ["kinderzimmer"] = 300, ["werkstatt"] = 500 };
    /// <summary>F12: Lumen = Lux × Fläche / Wirkungsgrad (0,6).</summary>
    public static (double Lux, double Lumen, int Bulbs806) Light(Room r, string kind)
    {
        if (!Lux.TryGetValue(kind.Trim(), out var lx)) throw new ArgumentException("Raumart: " + string.Join(", ", Lux.Keys));
        var lm = Math.Round(lx * r.Floor / 0.6); return (lx, lm, (int)Math.Ceiling(lm / 806));
    }

    /// <summary>F13: Kostenschätzung aus Bedarf und Stückpreisen.</summary>
    public static (double Paint, double Floor, double Skirting, double Tiles, double Total) Cost(MaterialNeed n, double bucketPrice, double packPrice, double skirtingPerM, int tilePacks = 0, double tilePackPrice = 0)
    {
        double p = n.PaintBuckets * bucketPrice, f = n.FloorPacks * packPrice, s = Math.Ceiling(n.SkirtingMeters / 2.5) * 2.5 * skirtingPerM, t = tilePacks * tilePackPrice;
        return (Math.Round(p, 2), Math.Round(f, 2), Math.Round(s, 2), Math.Round(t, 2), Math.Round(p + f + s + t, 2));
    }

    /// <summary>F14: Treppe: Steigungen so, dass h ≈ 17–19 cm; Auftritt aus 2h + a = 63 cm; Lauflänge; Bewertung.</summary>
    public static (int Steps, double RiseCm, double TreadCm, double RunCm, double AngleDeg, string Verdict) Stairs(double heightCm, double? maxRunCm = null)
    {
        if (heightCm < 30 || heightCm > 600) throw new ArgumentException("Geschosshöhe zwischen 30 und 600 cm.");
        var n = (int)Math.Round(heightCm / 17.5); var h = heightCm / n; var a = 63 - 2 * h; var run = a * (n - 1);
        if (maxRunCm is { } m && run > m) { a = m / (n - 1); }
        run = a * (n - 1); var ang = Math.Atan(h / a) * 180 / Math.PI;
        var v = h > 19 ? "Steil – über 19 cm Steigung (DIN 18065 Wohnhaus max. 20 cm)." : a < 26 ? "Auftritt unter 26 cm – unbequem." : Math.Abs(2 * h + a - 63) <= 2 ? "Bequem (Schrittmaßregel erfüllt)." : "Geht, aber Schrittmaß außerhalb 61–65 cm.";
        return (n, Math.Round(h, 1), Math.Round(a, 1), Math.Round(run, 1), Math.Round(ang, 1), v);
    }

    /// <summary>F15: Wohnfläche nach WoFlV: Flächenanteile unter 1 m Höhe 0 %, 1–2 m 50 %, ab 2 m 100 %.</summary>
    public static double LivingArea(double fullM2, double halfM2 = 0, double zeroM2 = 0, double balconyM2 = 0, double balconyFactor = 0.25)
    {
        if (fullM2 < 0 || halfM2 < 0 || zeroM2 < 0 || balconyM2 < 0) throw new ArgumentException("Flächen dürfen nicht negativ sein.");
        return Math.Round(fullM2 + halfM2 * 0.5 + balconyM2 * balconyFactor, 2);
    }

    /// <summary>F16: Raumliste CSV.</summary>
    public static string RoomsCsv(IEnumerable<Room> rooms)
    {
        var sb = new StringBuilder("Raum;Länge m;Breite m;Höhe m;Boden m²;Wände netto m²;Volumen m³;Sockelleiste m\n");
        foreach (var r in rooms) sb.Append($"{r.Name};{Fach.F(r.Length)};{Fach.F(r.Width)};{Fach.F(r.Height)};{Fach.F(r.Floor)};{Fach.F(r.WallNet)};{Fach.F(r.Volume)};{Fach.F(r.Skirting)}\n");
        return sb.ToString();
    }
}
