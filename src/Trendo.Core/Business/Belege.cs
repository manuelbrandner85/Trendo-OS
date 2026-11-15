using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Business;

// ===================== STUDIO-RECEIPTS-01 Belege & Spesen (T-0675) =====================
// Clean-Room nach dem Bedienkonzept von Candis, GetMyInvoices und Spesen-Apps: Beleg (Bild/PDF) erfassen, Betrag/Datum/Händler/USt aus dem Text erkennen
// (Text aus dem PDF; Texterkennung nur, wenn Tesseract eingerichtet ist), Kategorien mit Konto-Vorschlag (SKR03-ähnlich, eigene Liste),
// Reisekosten mit Verpflegungspauschalen (einstellbare Tabelle mit Stand), Kilometerpauschale, Monatsbericht als PDF/CSV, Ablage mit Dateiname
// „Datum Händler Betrag“. Keine Steuerberatung.

public sealed class Kategorie
{
    public string Name { get; set; } = "";
    /// <summary>Konto-Vorschlag (SKR03-ähnlich) – mit der Steuerberatung abstimmen.</summary>
    public string Konto { get; set; } = "";
    /// <summary>Stichwörter, an denen der Händler/Text erkannt wird.</summary>
    public string Woerter { get; set; } = "";
}

public sealed class Beleg
{
    public string Id { get; set; } = "";
    public string Datei { get; set; } = "";
    public string Datum { get; set; } = "";
    public string Haendler { get; set; } = "";
    public decimal Brutto { get; set; }
    public decimal UstSatz { get; set; } = 19;
    /// <summary>Ausgewiesene Umsatzsteuer (Vorsteuer) in Euro.</summary>
    public decimal Ust { get; set; }
    public string Kategorie { get; set; } = "Sonstiges";
    /// <summary>bar, karte, ueberweisung, privat (vorgestreckt → Erstattung).</summary>
    public string Zahlart { get; set; } = "karte";
    public string Notiz { get; set; } = "";
    /// <summary>Wie die Werte entstanden sind: „Text aus dem PDF“, „Texterkennung“, „von Hand“.</summary>
    public string Erkannt { get; set; } = "von Hand";
    public string Abgelegt { get; set; } = "";
    public decimal Netto => Brutto - Ust;
}

public sealed class Reise
{
    public string Id { get; set; } = "";
    public string Anlass { get; set; } = "";
    public string Ziel { get; set; } = "";
    /// <summary>Beginn und Ende als JJJJ-MM-TT HH:mm.</summary>
    public string Beginn { get; set; } = "";
    public string Ende { get; set; } = "";
    public int Fruehstueck { get; set; }
    public int Mittag { get; set; }
    public int Abend { get; set; }
    public decimal Km { get; set; }
    /// <summary>pkw, motor (Motorrad/Roller) oder keins.</summary>
    public string Fahrzeug { get; set; } = "pkw";
    public decimal Uebernachtung { get; set; }
}

/// <summary>Pauschalen als einstellbare Tabelle mit Quelle und Stand.</summary>
public sealed class Pauschalen
{
    /// <summary>§ 9 Abs. 4a Satz 3 Nr. 2 und 3 EStG: An-/Abreisetag und Abwesenheit über 8 Stunden.</summary>
    public decimal Kurz { get; set; } = 14;
    /// <summary>§ 9 Abs. 4a Satz 3 Nr. 1 EStG: 24 Stunden abwesend.</summary>
    public decimal Voll { get; set; } = 28;
    /// <summary>§ 9 Abs. 4a Satz 8 EStG: Kürzung bei gestellten Mahlzeiten – Frühstück 20 %, Mittag- und Abendessen je 40 % der vollen Pauschale.</summary>
    public decimal FruehstueckProzent { get; set; } = 20;
    public decimal MahlzeitProzent { get; set; } = 40;
    /// <summary>§ 9 Abs. 1 Satz 3 Nr. 4a EStG (Werte aus <see cref="LegalRates"/>).</summary>
    public decimal KmPkw { get; set; } = (decimal)LegalRates.TravelCar;
    public decimal KmMotor { get; set; } = (decimal)LegalRates.TravelOtherMotor;
    public string Quelle { get; set; } = "§ 9 Abs. 4a EStG (Verpflegung, Inland), § 9 Abs. 1 Satz 3 Nr. 4a EStG (Fahrtkosten) – gesetze-im-internet.de; BMF-Schreiben vom 05.12.2025 (Reisekosten 2026)";
    public string Stand { get; set; } = "16.11.2026";
}

public sealed class Spesenbuch
{
    public string Person { get; set; } = "";
    public List<Beleg> Belege { get; set; } = [];
    public List<Reise> Reisen { get; set; } = [];
    public List<Kategorie> Kategorien { get; set; } = [.. BelegKit.Standard.Select(BizJson.Clone)];
    public Pauschalen Pauschalen { get; set; } = new();
    public JsonObject ToJson() => BizJson.To(this);
    public static Spesenbuch FromJson(JsonNode? n) => BizJson.From<Spesenbuch>(n);
}

public sealed record Erkennung(DateOnly? Datum, decimal? Betrag, string Haendler, decimal? UstSatz, decimal? Ust, string Kategorie, List<string> Hinweise);
public sealed record ReiseTag(DateOnly Tag, double Stunden, decimal Pauschale, decimal Kuerzung, decimal Betrag, string Art);
public sealed record ReiseAbrechnung(List<ReiseTag> Tage, decimal Verpflegung, decimal Fahrt, decimal Uebernachtung, decimal Summe);

public static partial class BelegKit
{
    /// <summary>Kategorien mit Konto-Vorschlag nach SKR03 (häufig verwendete Konten; eigene Liste, mit der Steuerberatung abstimmen).</summary>
    public static readonly Kategorie[] Standard =
    [
        new() { Name = "Bürobedarf", Konto = "4930", Woerter = "papier büro toner drucker ordner stift" },
        new() { Name = "Porto", Konto = "4910", Woerter = "post porto briefmarke dhl hermes paket" },
        new() { Name = "Telefon & Internet", Konto = "4920", Woerter = "telefon mobilfunk internet dsl handy" },
        new() { Name = "Software & Lizenzen", Konto = "4806", Woerter = "software lizenz abo cloud hosting domain" },
        new() { Name = "Fachliteratur", Konto = "4940", Woerter = "buch buchhandlung zeitschrift fachbuch" },
        new() { Name = "Werbung", Konto = "4600", Woerter = "werbung anzeige flyer druckerei" },
        new() { Name = "Bewirtung", Konto = "4650", Woerter = "restaurant gaststätte bewirtung café pizzeria" },
        new() { Name = "Reisekosten", Konto = "4670", Woerter = "hotel bahn db fernverkehr flug taxi übernachtung" },
        new() { Name = "Kfz-Kosten", Konto = "4530", Woerter = "tankstelle tank aral shell esso diesel benzin werkstatt" },
        new() { Name = "Geldverkehr", Konto = "4970", Woerter = "kontoführung gebühr bank" },
        new() { Name = "Sonstiges", Konto = "4900", Woerter = "" },
    ];

    public static readonly (string Id, string Name)[] Zahlarten = [("karte", "Karte"), ("bar", "Bar"), ("ueberweisung", "Überweisung"), ("privat", "Privat vorgestreckt")];

    [GeneratedRegex(@"(?<!\d)(\d{1,2})[.](\d{1,2})[.](\d{4}|\d{2})(?!\d)")] private static partial Regex DeDateRx();
    [GeneratedRegex(@"(?<!\d)(\d{4})-(\d{2})-(\d{2})(?!\d)")] private static partial Regex IsoDateRx();
    [GeneratedRegex(@"(?<![\d,.])(\d{1,3}(?:[.\s]\d{3})*|\d+)[,.](\d{2})(?![\d])")] private static partial Regex AmountRx();
    [GeneratedRegex(@"(19|7|16|5)[,.]?0*\s*%")] private static partial Regex RateRx();
    [GeneratedRegex(@"summe|gesamt|total|zu zahlen|zahlbetrag|endbetrag|brutto|betrag|\bbar\b|ec-karte|girocard|kartenzahlung", RegexOptions.IgnoreCase)] private static partial Regex TotalRx();
    [GeneratedRegex(@"mwst|ust|umsatzsteuer|mehrwertsteuer|steuer", RegexOptions.IgnoreCase)] private static partial Regex TaxRx();
    [GeneratedRegex(@"netto|zwischensumme|rückgeld|rueckgeld|gegeben|wechselgeld", RegexOptions.IgnoreCase)] private static partial Regex NotTotalRx();
    [GeneratedRegex(@"^(rechnung|quittung|beleg|kassenbon|bon|kasse|datum|uhrzeit|tel|telefon|fax|www\.|http|ust-?id|steuer|st\.?-?nr|filiale|kundenbeleg|vielen dank|danke)", RegexOptions.IgnoreCase)] private static partial Regex NoMerchantRx();

    static decimal? Amount(Match m) =>
        decimal.TryParse(m.Groups[1].Value.Replace(".", "").Replace(" ", "") + "." + m.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>Betrag, Datum, Händler und Umsatzsteuer aus dem Belegtext erkennen. Unsicheres steht in <see cref="Erkennung.Hinweise"/> – nichts wird still geraten.</summary>
    public static Erkennung Erkennen(string text, IReadOnlyList<Kategorie>? kategorien = null, DateOnly? heute = null)
    {
        var hints = new List<string>();
        var lines = (text ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        // Datum: erstes plausibles (nicht in der Zukunft)
        DateOnly? date = null; var today = heute ?? DateOnly.FromDateTime(DateTime.Today);
        foreach (var l in lines)
        {
            foreach (Match m in DeDateRx().Matches(l))
            {
                var y = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture); if (y < 100) y += 2000;
                if (int.TryParse(m.Groups[2].Value, out var mo) && int.TryParse(m.Groups[1].Value, out var dd) && mo is >= 1 and <= 12 && dd >= 1 && dd <= DateTime.DaysInMonth(y, mo))
                { var d = new DateOnly(y, mo, dd); if (d <= today.AddDays(1) && d.Year > 2000) { date = d; break; } }
            }
            if (date is null && IsoDateRx().Match(l) is { Success: true } im && DateOnly.TryParse(im.Value, CultureInfo.InvariantCulture, out var iso) && iso <= today.AddDays(1)) date = iso;
            if (date is not null) break;
        }
        if (date is null) hints.Add("Kein Datum gefunden – bitte eintragen.");

        // Betrag: größte Zahl auf einer Summen-Zeile, sonst größte Zahl überhaupt
        decimal? total = null; decimal? fallback = null;
        foreach (var l in lines)
        {
            var amounts = AmountRx().Matches(l).Select(Amount).OfType<decimal>().ToList(); if (amounts.Count == 0) continue;
            var max = amounts.Max(); fallback = fallback is null ? max : Math.Max(fallback.Value, max);
            if (TotalRx().IsMatch(l) && !NotTotalRx().IsMatch(l) && !(TaxRx().IsMatch(l) && !Regex.IsMatch(l, "brutto|summe|gesamt", RegexOptions.IgnoreCase))) total = total is null ? max : Math.Max(total.Value, max);
        }
        if (total is null && fallback is not null) { total = fallback; hints.Add("Gesamtbetrag nicht eindeutig – der größte Betrag wurde genommen, bitte prüfen."); }
        if (total is null) hints.Add("Kein Betrag gefunden – bitte eintragen.");

        // Umsatzsteuer: Satz und ausgewiesener Betrag
        decimal? rate = null, tax = null;
        foreach (var l in lines.Where(l => TaxRx().IsMatch(l) || RateRx().IsMatch(l)))
        {
            if (rate is null && RateRx().Match(l) is { Success: true } rm) rate = decimal.Parse(rm.Groups[1].Value, CultureInfo.InvariantCulture);
            if (TaxRx().IsMatch(l) && !Regex.IsMatch(l, "brutto|summe|gesamt|netto", RegexOptions.IgnoreCase))
            {
                var a = AmountRx().Matches(l).Select(Amount).OfType<decimal>().Where(x => total is null || x < total).ToList();
                if (a.Count > 0) tax = (tax ?? 0) + a.Last();
            }
        }
        if (rate is null && tax is not null && total is { } t && t - tax.Value > 0)
        {
            var r = Math.Round(tax.Value / (t - tax.Value) * 100m);
            if (r is 19 or 7) rate = r;
        }
        if (rate is not null && tax is null && total is { } t2) { tax = Math.Round(t2 - t2 / (1 + rate.Value / 100m), 2); hints.Add($"Umsatzsteuer aus dem Satz {rate:0} % berechnet."); }
        if (rate is null) hints.Add("Kein Steuersatz erkannt – bitte prüfen (19 %, 7 % oder 0 %).");

        // Händler: erste Zeile mit Buchstaben, die keine Kopfzeile/Adresse/Zahl ist
        var merchant = lines.Take(6).FirstOrDefault(l => l.Count(char.IsLetter) >= 3 && !NoMerchantRx().IsMatch(l) && !AmountRx().IsMatch(l) && !DeDateRx().IsMatch(l) && !Regex.IsMatch(l, @"^\d{5}\s") && !Regex.IsMatch(l, @"str\.|straße|weg \d|platz \d", RegexOptions.IgnoreCase)) ?? "";
        if (merchant.Length == 0) hints.Add("Händler nicht erkannt – bitte eintragen.");
        merchant = merchant.Length > 60 ? merchant[..60].Trim() : merchant;
        var cat = Kategorisieren(merchant + "\n" + text, kategorien ?? Standard);
        return new(date, total, merchant, rate, tax, cat, hints);
    }

    /// <summary>Kategorie über Stichwörter der Kategorie (Händler und Text) – sonst „Sonstiges“.</summary>
    public static string Kategorisieren(string text, IReadOnlyList<Kategorie> kats)
    {
        var t = (text ?? "").ToLowerInvariant();
        var best = kats.Select(k => (k.Name, Hits: k.Woerter.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(w => t.Contains(w.ToLowerInvariant())))).Where(x => x.Hits > 0).OrderByDescending(x => x.Hits).FirstOrDefault();
        return best.Name ?? (kats.Any(k => k.Name == "Sonstiges") ? "Sonstiges" : kats.FirstOrDefault()?.Name ?? "Sonstiges");
    }

    public static Beleg Uebernehmen(Beleg b, Erkennung e, string wie)
    {
        if (e.Datum is { } d) b.Datum = BizJson.Iso(d);
        if (e.Betrag is { } a) b.Brutto = a;
        if (e.Haendler.Length > 0) b.Haendler = e.Haendler;
        if (e.UstSatz is { } r) b.UstSatz = r;
        if (e.Ust is { } u) b.Ust = u; else if (e.Betrag is { } br) b.Ust = UstAus(br, b.UstSatz);
        b.Kategorie = e.Kategorie; b.Erkannt = wie;
        return b;
    }

    public static decimal UstAus(decimal brutto, decimal satz) => satz <= 0 ? 0 : Math.Round(brutto - brutto / (1 + satz / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>Dateiname für die Ablage: „JJJJ-MM-TT Händler 23,80 €.pdf“ (nur sichere Zeichen).</summary>
    public static string Ablagename(Beleg b, string ext)
    {
        var date = BizJson.Datum(b.Datum) is { } d ? BizJson.Iso(d) : "ohne-Datum";
        var who = string.Concat((b.Haendler.Trim().Length > 0 ? b.Haendler.Trim() : "Beleg").Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '&' ? c : ' '));
        who = Regex.Replace(who, @"\s+", " ").Trim(); if (who.Length > 40) who = who[..40].Trim();
        return $"{date} {who} {b.Brutto.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"))} EUR{ext.ToLowerInvariant()}";
    }

    /// <summary>Kopie in den Ablage-Ordner (Unterordner JJJJ-MM), nie überschreiben. Das Original bleibt.</summary>
    public static string Ablegen(Beleg b, string root)
    {
        if (!File.Exists(b.Datei)) throw new FileNotFoundException("Die Belegdatei gibt es nicht mehr.", b.Datei);
        var month = BizJson.Datum(b.Datum) is { } d ? $"{d:yyyy-MM}" : "ohne-Datum";
        var dir = Path.Combine(root, month); Directory.CreateDirectory(dir);
        var name = Ablagename(b, Path.GetExtension(b.Datei)); var target = Path.Combine(dir, name);
        for (var n = 2; File.Exists(target); n++) target = Path.Combine(dir, Path.GetFileNameWithoutExtension(name) + $" ({n})" + Path.GetExtension(name));
        File.Copy(b.Datei, target);
        b.Abgelegt = target; return target;
    }

    static DateTime? Zeit(string s) => DateTime.TryParseExact((s ?? "").Trim(), ["yyyy-MM-dd HH:mm", "dd.MM.yyyy HH:mm", "yyyy-MM-ddTHH:mm"], CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Verpflegungsmehraufwand Inland nach § 9 Abs. 4a EStG: eintägig über 8 h → kleine Pauschale; mehrtägig An- und Abreisetag je kleine Pauschale,
    /// volle Kalendertage die große. Gestellte Mahlzeiten kürzen um 20 % (Frühstück) bzw. 40 % (Mittag/Abend) der großen Pauschale, höchstens bis 0 €.
    /// Die Mahlzeiten werden von vorn auf die Tage verteilt (höchstens je eine pro Tag, Frühstück bei mehrtägigen Reisen ab dem zweiten Tag – nach der Übernachtung). Kilometer × Satz je Fahrzeug, dazu Übernachtung.</summary>
    public static ReiseAbrechnung Abrechnen(Reise r, Pauschalen p)
    {
        var b = Zeit(r.Beginn) ?? throw new FormatException("Beginn bitte als „TT.MM.JJJJ HH:mm“.");
        var e = Zeit(r.Ende) ?? throw new FormatException("Ende bitte als „TT.MM.JJJJ HH:mm“.");
        if (e <= b) throw new ArgumentException("Das Ende muss nach dem Beginn liegen.");
        if (r.Km < 0 || r.Uebernachtung < 0 || r.Fruehstueck < 0 || r.Mittag < 0 || r.Abend < 0) throw new ArgumentException("Kilometer, Übernachtung und Mahlzeiten dürfen nicht negativ sein.");
        var days = new List<ReiseTag>();
        int fr = r.Fruehstueck, mi = r.Mittag, ab = r.Abend;
        decimal Cut(decimal pausch, bool breakfast = true)
        {
            decimal k = 0;
            if (fr > 0 && breakfast) { k += Math.Round(p.Voll * p.FruehstueckProzent / 100m, 2); fr--; }
            if (mi > 0) { k += Math.Round(p.Voll * p.MahlzeitProzent / 100m, 2); mi--; }
            if (ab > 0) { k += Math.Round(p.Voll * p.MahlzeitProzent / 100m, 2); ab--; }
            return Math.Min(k, pausch);
        }
        if (b.Date == e.Date)
        {
            var h = (e - b).TotalHours; var pa = h > 8 ? p.Kurz : 0; var k = pa > 0 ? Cut(pa) : 0;
            days.Add(new(DateOnly.FromDateTime(b), Math.Round(h, 2), pa, k, pa - k, h > 8 ? "eintägig über 8 Stunden" : "eintägig bis 8 Stunden – keine Pauschale"));
        }
        else
        {
            for (var d = b.Date; d <= e.Date; d = d.AddDays(1))
            {
                var first = d == b.Date; var last = d == e.Date;
                var h = first ? (d.AddDays(1) - b).TotalHours : last ? (e - d).TotalHours : 24;
                var pa = first || last ? p.Kurz : p.Voll; var k = Cut(pa, !first);
                days.Add(new(DateOnly.FromDateTime(d), Math.Round(h, 2), pa, k, pa - k, first ? "Anreisetag" : last ? "Abreisetag" : "voller Tag (24 Stunden)"));
            }
        }
        var rate = r.Fahrzeug switch { "pkw" => p.KmPkw, "motor" => p.KmMotor, _ => 0 };
        var fahrt = Math.Round(r.Km * rate, 2);
        var verpf = days.Sum(x => x.Betrag);
        return new(days, verpf, fahrt, r.Uebernachtung, verpf + fahrt + r.Uebernachtung);
    }

    public sealed record Monat(int Jahr, int Monatszahl, List<Beleg> Belege, List<(string Kategorie, string Konto, decimal Brutto, decimal Ust, int Anzahl)> JeKategorie,
        List<(Reise Reise, ReiseAbrechnung Abr)> Reisen, decimal Brutto, decimal Ust, decimal Erstattung);

    /// <summary>Monatsbericht: Belege nach Kategorie, Vorsteuer, Reisen mit Pauschalen und Erstattung (privat vorgestreckt + Reisekosten).</summary>
    public static Monat Bericht(Spesenbuch s, int jahr, int monat)
    {
        var bel = s.Belege.Where(b => BizJson.Datum(b.Datum) is { } d && d.Year == jahr && d.Month == monat).OrderBy(b => b.Datum).ToList();
        var cats = bel.GroupBy(b => b.Kategorie).Select(g => (g.Key, s.Kategorien.FirstOrDefault(k => k.Name == g.Key)?.Konto ?? "", g.Sum(x => x.Brutto), g.Sum(x => x.Ust), g.Count())).OrderByDescending(x => x.Item3).ToList();
        var trips = s.Reisen.Select(r => (R: r, Start: Zeit(r.Beginn))).Where(x => x.Start is { } st && st.Year == jahr && st.Month == monat).OrderBy(x => x.Start)
            .Select(x => (x.R, Abrechnen(x.R, s.Pauschalen))).ToList();
        var erst = bel.Where(b => b.Zahlart == "privat").Sum(b => b.Brutto) + trips.Sum(t => t.Item2.Summe);
        return new(jahr, monat, bel, cats, trips, bel.Sum(b => b.Brutto), bel.Sum(b => b.Ust), erst);
    }

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static string MonatName(int jahr, int monat) => new DateTime(jahr, monat, 1).ToString("MMMM yyyy", De);

    public static string Csv(Spesenbuch s, int jahr, int monat)
    {
        var m = Bericht(s, jahr, monat);
        var sb = new StringBuilder("Datum;Händler;Kategorie;Konto;Brutto;USt-Satz;USt;Netto;Zahlart;Erkannt;Datei\r\n");
        foreach (var b in m.Belege)
            sb.Append(string.Join(";", BizJson.TextOf(b.Datum), BizJson.Csv(b.Haendler), BizJson.Csv(b.Kategorie), BizJson.Csv(s.Kategorien.FirstOrDefault(k => k.Name == b.Kategorie)?.Konto ?? ""),
                b.Brutto.ToString("0.00", De), b.UstSatz.ToString("0.##", De), b.Ust.ToString("0.00", De), b.Netto.ToString("0.00", De), BizJson.Csv(Zahlarten.FirstOrDefault(z => z.Id == b.Zahlart).Name ?? b.Zahlart),
                BizJson.Csv(b.Erkannt), BizJson.Csv(Path.GetFileName(b.Abgelegt.Length > 0 ? b.Abgelegt : b.Datei)))).Append("\r\n");
        foreach (var (r, a) in m.Reisen)
            sb.Append(string.Join(";", BizJson.Csv(r.Beginn[..Math.Min(10, r.Beginn.Length)]), BizJson.Csv("Reise: " + r.Ziel), BizJson.Csv("Reisekosten"), "", a.Summe.ToString("0.00", De), "0", "0,00", a.Summe.ToString("0.00", De),
                BizJson.Csv("Pauschale"), BizJson.Csv($"Verpflegung {a.Verpflegung.ToString("0.00", De)} + Fahrt {a.Fahrt.ToString("0.00", De)} + Übernachtung {a.Uebernachtung.ToString("0.00", De)}"), "\"\"")).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>Spesenabrechnung als PDF (Belege, Kategorien, Reisen mit Tagen) – gibt die Seitenzahl zurück.</summary>
    public static int Pdf(Spesenbuch s, int jahr, int monat, string path)
    {
        var m = Bericht(s, jahr, monat);
        using var pdf = new BizPdf("Spesenabrechnung " + MonatName(jahr, monat), s.Person) { Footer = "Pauschalen: " + s.Pauschalen.Quelle + " · Stand " + s.Pauschalen.Stand + " · Keine Steuerberatung." };
        pdf.Write("Spesenabrechnung " + MonatName(jahr, monat), 16, true, pdf.Accent);
        if (s.Person.Trim().Length > 0) pdf.Write(s.Person.Trim(), 10, false, BizPdf.Grey);
        pdf.Y += 3;
        pdf.Write($"{m.Belege.Count} Belege · {BizJson.Euro(m.Brutto)} brutto · Vorsteuer {BizJson.Euro(m.Ust)} · {m.Reisen.Count} Reisen · Erstattung an dich {BizJson.Euro(m.Erstattung)}", 10, true);
        pdf.Y += 3;
        if (m.Belege.Count > 0)
            pdf.Table(["Datum", "Händler", "Kategorie", "Zahlart", "USt", "Brutto"], [20, 50, 38, 26, 16, 20], "llllrr",
                m.Belege.Select(b => new[] { BizJson.TextOf(b.Datum), b.Haendler, b.Kategorie, Zahlarten.FirstOrDefault(z => z.Id == b.Zahlart).Name ?? b.Zahlart, BizJson.Euro(b.Ust), BizJson.Euro(b.Brutto) }));
        pdf.Y += 4;
        if (m.JeKategorie.Count > 0)
        {
            pdf.Write("Nach Kategorie", 11, true); pdf.Y += 1;
            pdf.Table(["Kategorie", "Konto", "Anzahl", "USt", "Brutto"], [70, 25, 20, 25, 30], "llrrr", m.JeKategorie.Select(c => new[] { c.Kategorie, c.Konto, c.Anzahl.ToString(), BizJson.Euro(c.Ust), BizJson.Euro(c.Brutto) }));
            pdf.Y += 4;
        }
        foreach (var (r, a) in m.Reisen)
        {
            pdf.Need(30); pdf.Write($"Reise: {r.Ziel} – {r.Anlass}", 11, true); pdf.Write($"{r.Beginn} bis {r.Ende}", 9, false, BizPdf.Grey); pdf.Y += 1;
            pdf.Table(["Tag", "Art", "Stunden", "Pauschale", "Kürzung", "Betrag"], [22, 60, 18, 24, 22, 24], "llrrrr",
                a.Tage.Select(t => new[] { BizJson.Text(t.Tag), t.Art, t.Stunden.ToString("0.#", De), BizJson.Euro(t.Pauschale), t.Kuerzung > 0 ? "− " + BizJson.Euro(t.Kuerzung) : "", BizJson.Euro(t.Betrag) }));
            pdf.Write($"Verpflegung {BizJson.Euro(a.Verpflegung)} · Fahrt {BizJson.Num(r.Km)} km = {BizJson.Euro(a.Fahrt)} · Übernachtung {BizJson.Euro(a.Uebernachtung)} · Summe {BizJson.Euro(a.Summe)}", 9.5, true);
            pdf.Y += 4;
        }
        pdf.Y += 6; pdf.Need(20);
        pdf.Write("Datum, Unterschrift: ________________________________", 10);
        return pdf.Save(path);
    }

    public static Spesenbuch Beispiel(DateOnly heute)
    {
        var m = new DateOnly(heute.Year, heute.Month, 1);
        return new Spesenbuch
        {
            Person = "Erika Muster",
            Belege =
            [
                new() { Id = "b1", Datum = BizJson.Iso(m.AddDays(2)), Haendler = "Bürobedarf Muster", Brutto = 23.80m, UstSatz = 19, Ust = 3.80m, Kategorie = "Bürobedarf", Zahlart = "karte" },
                new() { Id = "b2", Datum = BizJson.Iso(m.AddDays(5)), Haendler = "Buchhandlung Beispiel", Brutto = 32.10m, UstSatz = 7, Ust = 2.10m, Kategorie = "Fachliteratur", Zahlart = "privat" },
                new() { Id = "b3", Datum = BizJson.Iso(m.AddDays(8)), Haendler = "Hotel Musterhof", Brutto = 107m, UstSatz = 7, Ust = 7m, Kategorie = "Reisekosten", Zahlart = "privat" },
            ],
            Reisen = [new() { Id = "r1", Anlass = "Kundentermin", Ziel = "Beispielhausen", Beginn = $"{m.AddDays(7):yyyy-MM-dd} 07:30", Ende = $"{m.AddDays(8):yyyy-MM-dd} 18:00", Fruehstueck = 1, Km = 240, Fahrzeug = "pkw" }],
        };
    }
}
