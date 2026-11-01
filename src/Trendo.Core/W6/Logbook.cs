using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core;

/// <summary>Monatssumme im Fahrtenbuch.</summary>
public sealed record LogMonth(int Year, int Month, double Business, double Private, double Commute, int Count, bool Locked);
/// <summary>Häufige Fahrt für die Schnelleingabe.</summary>
public sealed record FrequentTrip(string Destination, string Purpose, string Partner, string Kind, double Km, int Count);

/// <summary>Fahrtenbuch nach den Regeln des Finanzamts (Masterprompt 131): Fahrtart, Ziel, Zweck und Geschäftspartner, Korrektur und Storno nur mit Grund
/// (bleiben sichtbar), Monate festschreiben, Monatssummen, häufige Fahrten, PDF zum Ausdrucken.</summary>
public sealed partial class VehicleBook
{
    public static readonly string[] Kinds = ["dienstlich", "privat", "arbeitsweg"];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Fahrt mit Art, Ziel, Zweck und Partner eintragen. Dienstlich: Zweck ist Pflicht.</summary>
    public Trip Log(string vehicle, DateTime date, double start, double end, string kind, string destination = "", string purpose = "", string partner = "")
    {
        if (!Kinds.Contains(kind)) throw new ArgumentException("Unbekannte Fahrtart.");
        if (Get(vehicle).Trips.Any(t => t.Locked && t.Date.Year == date.Year && t.Date.Month == date.Month)) throw new InvalidOperationException($"Der {date:MMMM yyyy} ist festgeschrieben – dort lässt sich nichts mehr nachtragen.");
        var t = Drive(vehicle, date, start, end, kind == "dienstlich", kind == "dienstlich" && purpose.Trim().Length == 0 ? destination : purpose);
        t.Kind = kind; t.Destination = destination.Trim(); t.Partner = partner.Trim(); Save();
        return t;
    }

    static void Unlocked(Trip t) { if (t.Locked) throw new InvalidOperationException("Diese Fahrt ist festgeschrieben und lässt sich nicht mehr ändern."); }

    /// <summary>Korrektur mit Grund: die Änderung wird an der Fahrt vermerkt (alt → neu), nichts wird still überschrieben.</summary>
    public void Correct(string vehicle, Trip t, string reason, DateTime? date = null, double? start = null, double? end = null, string? kind = null, string? destination = null, string? purpose = null, string? partner = null)
    {
        Unlocked(t); if (t.Voided) throw new InvalidOperationException("Diese Fahrt ist storniert.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Bitte einen Grund für die Korrektur angeben.");
        var v = Get(vehicle); var s = start ?? t.StartKm; var e = end ?? t.EndKm; var k = kind ?? t.Art;
        if (e <= s) throw new ArgumentException("Der End-Kilometerstand muss größer als der Start sein.");
        if (v.Trips.Any(x => !ReferenceEquals(x, t) && !x.Voided && s < x.EndKm && e > x.StartKm)) throw new ArgumentException("Diese Kilometer überschneiden sich mit einer anderen Fahrt.");
        if (k == "dienstlich" && (purpose ?? t.Purpose).Trim().Length == 0) throw new ArgumentException("Für dienstliche Fahrten verlangt das Finanzamt Zweck und Ziel.");
        var changes = new List<string>();
        void Note(string field, string a, string b) { if (a != b) changes.Add($"{field}: {a} → {b}"); }
        Note("Datum", t.Date.ToString("dd.MM.yyyy"), (date ?? t.Date).ToString("dd.MM.yyyy")); Note("km", $"{t.StartKm:0}–{t.EndKm:0}", $"{s:0}–{e:0}"); Note("Art", t.Art, k);
        Note("Ziel", t.Destination, destination?.Trim() ?? t.Destination); Note("Zweck", t.Purpose, purpose?.Trim() ?? t.Purpose); Note("Partner", t.Partner, partner?.Trim() ?? t.Partner);
        if (changes.Count == 0) return;
        t.Date = date ?? t.Date; t.StartKm = s; t.EndKm = e; t.Kind = k; t.Business = k == "dienstlich";
        t.Destination = destination?.Trim() ?? t.Destination; t.Purpose = purpose?.Trim() ?? t.Purpose; t.Partner = partner?.Trim() ?? t.Partner;
        (t.Corrections ??= []).Add($"{DateTime.Now:dd.MM.yyyy HH:mm} {string.Join(", ", changes)} – Grund: {reason.Trim()}");
        v.Trips.Sort((a, b) => a.StartKm.CompareTo(b.StartKm)); Save();
    }

    /// <summary>Stornieren statt löschen: die Fahrt bleibt mit Grund sichtbar, zählt aber nicht mehr.</summary>
    public void Void(string vehicle, Trip t, string reason)
    {
        Unlocked(t); if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Bitte einen Grund für das Stornieren angeben.");
        t.Voided = true; t.VoidReason = reason.Trim(); Get(vehicle); Save();
    }

    /// <summary>Monat festschreiben: danach keine Änderungen mehr (wie im Papier-Fahrtenbuch). Gibt die Zahl festgeschriebener Fahrten zurück.</summary>
    public int LockMonth(string vehicle, int year, int month)
    {
        var l = Get(vehicle).Trips.Where(t => t.Date.Year == year && t.Date.Month == month && !t.Locked).ToList();
        foreach (var t in l) t.Locked = true; Save(); return l.Count;
    }

    public static List<LogMonth> Months(Vehicle v, int year) =>
        v.Trips.Where(t => !t.Voided && t.Date.Year == year).GroupBy(t => t.Date.Month).OrderByDescending(g => g.Key)
            .Select(g => new LogMonth(year, g.Key, g.Where(t => t.Art == "dienstlich").Sum(t => t.Km), g.Where(t => t.Art == "privat").Sum(t => t.Km), g.Where(t => t.Art == "arbeitsweg").Sum(t => t.Km), g.Count(), g.All(t => t.Locked))).ToList();

    /// <summary>Häufige Fahrten (mindestens zweimal gleiches Ziel bzw. gleicher Zweck), mit üblicher Strecke.</summary>
    public static List<FrequentTrip> Frequent(Vehicle v, int max = 6) =>
        v.Trips.Where(t => !t.Voided && (t.Destination.Length > 0 || t.Purpose.Length > 0)).GroupBy(t => (t.Destination.Length > 0 ? t.Destination : t.Purpose).ToLowerInvariant())
            .Where(g => g.Count() >= 2).OrderByDescending(g => g.Count())
            .Select(g => { var last = g.OrderBy(t => t.Date).Last(); return new FrequentTrip(last.Destination, last.Purpose, last.Partner, last.Art, Math.Round(g.Select(t => t.Km).OrderBy(x => x).ElementAt(g.Count() / 2)), g.Count()); })
            .Take(max).ToList();

    /// <summary>Fahrtenbuch eines Jahres als PDF (A4 quer): alle Pflichtangaben, Stornos und Korrekturen, Monats- und Jahressummen, Lücken.</summary>
    public static void LogPdf(string path, Vehicle v, int year)
    {
        Sys.PdfTools.Fonts.Ensure();
        using var doc = new PdfDocument(); var f = new XFont("Arial", 8); var b = new XFont("Arial", 8, XFontStyleEx.Bold); var h = new XFont("Arial", 13, XFontStyleEx.Bold);
        PdfPage page = null!; XGraphics g = null!; double y = 0; var pageNo = 0;
        double[] x = [30, 85, 130, 175, 205, 260, 400, 560, 700];
        void NewPage()
        {
            g?.Dispose(); page = doc.AddPage(); page.Width = XUnit.FromMillimeter(297); page.Height = XUnit.FromMillimeter(210); g = XGraphics.FromPdfPage(page); pageNo++; y = 36;
            g.DrawString($"Fahrtenbuch {year} · {v.Name}{(v.Plate.Length > 0 ? " · " + v.Plate : "")}", h, XBrushes.Black, new XPoint(30, y)); g.DrawString($"Seite {pageNo}", f, XBrushes.Gray, new XPoint(page.Width.Point - 70, y)); y += 20;
            string[] hd = ["Datum", "km Start", "km Ende", "km", "Art", "Ziel", "Zweck", "Geschäftspartner", "Vermerk"];
            for (var i = 0; i < hd.Length; i++) g.DrawString(hd[i], b, XBrushes.Black, new XPoint(x[i], y));
            y += 4; g.DrawLine(XPens.Black, 30, y, page.Width.Point - 30, y); y += 11;
        }
        string Cut(string s, double w) { while (s.Length > 1 && g.MeasureString(s, f).Width > w) s = s[..^2] + "…"; return s; }
        NewPage();
        foreach (var m in v.Trips.Where(t => t.Date.Year == year).GroupBy(t => t.Date.Month).OrderBy(gr => gr.Key))
        {
            foreach (var t in m.OrderBy(t => t.StartKm))
            {
                if (y > page.Height.Point - 50) NewPage();
                var brush = t.Voided ? XBrushes.Gray : XBrushes.Black;
                string[] cells = [t.Date.ToString("dd.MM.yyyy"), t.StartKm.ToString("0"), t.EndKm.ToString("0"), t.Km.ToString("0"), t.Art, t.Art == "dienstlich" ? t.Destination : "", t.Art == "dienstlich" ? t.Purpose : "", t.Art == "dienstlich" ? t.Partner : "",
                    t.Voided ? "storniert: " + t.VoidReason : t.Corrections is { Count: > 0 } c ? "korrigiert: " + c[^1] : t.Locked ? "festgeschrieben" : ""];
                for (var i = 0; i < cells.Length; i++) g.DrawString(Cut(cells[i], (i + 1 < x.Length ? x[i + 1] : page.Width.Point - 30) - x[i] - 4), f, brush, new XPoint(x[i], y));
                if (t.Voided) g.DrawLine(new XPen(XColors.Gray, 0.5), x[0], y - 3, x[4] + 40, y - 3);
                y += 11;
            }
            var lm = Months(v, year).FirstOrDefault(r => r.Month == m.Key);
            if (lm is not null) { g.DrawString($"Summe {new DateTime(year, m.Key, 1).ToString("MMMM", De)}: dienstlich {lm.Business:0} km · privat {lm.Private:0} km · Arbeitsweg {lm.Commute:0} km{(lm.Locked ? " · festgeschrieben" : "")}", b, XBrushes.Black, new XPoint(30, y)); y += 16; }
        }
        var (biz, priv, share) = TripShare(v, year); var commute = v.Trips.Where(t => !t.Voided && t.Date.Year == year && t.Art == "arbeitsweg").Sum(t => t.Km);
        if (y > page.Height.Point - 70) NewPage();
        y += 6; g.DrawString($"Jahr {year}: dienstlich {biz:0} km ({share:0.#} %) · privat {priv - commute:0} km · Arbeitsweg {commute:0} km", b, XBrushes.Black, new XPoint(30, y)); y += 14;
        foreach (var (a, e) in Gaps(v)) { g.DrawString($"Lücke: {a:0} → {e:0} km", f, XBrushes.DarkRed, new XPoint(30, y)); y += 11; }
        y += 24; g.DrawLine(XPens.Black, 30, y, 230, y); g.DrawString("Ort, Datum, Unterschrift", f, XBrushes.Gray, new XPoint(30, y + 10));
        g.Dispose(); doc.Save(path);
    }
}
