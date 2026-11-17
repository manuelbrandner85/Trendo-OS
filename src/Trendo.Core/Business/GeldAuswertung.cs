using System.Globalization;
using System.Text;
using System.Text.Json;
using PdfSharp.Drawing;
using Trendo.Core.Office;

namespace Trendo.Core.Business;

// ===================== Auswertung für „Geld“ (SPEC-11, T-0694) =====================
// Haushaltsbuch / Einnahmen-Überschuss je Monat, Jahr und Kategorie; Export als CSV, XLSX (Trendo Tabelle/Excel/Calc) und PDF;
// DATEV-Buchungsstapel als CSV für den Steuerberater. Keine Steuerberatung, kein ELSTER (bewusst nicht gebaut).

/// <summary>Eine Zeile der Auswertung je Kategorie: netto, Steuer, brutto, Anzahl Buchungen.</summary>
public sealed record KategorieSumme(string Kategorie, decimal Netto, decimal Steuer, decimal Brutto, int Anzahl);
public sealed record MonatsSumme(int Monat, decimal Einnahmen, decimal Ausgaben) { public decimal Ergebnis => Einnahmen - Ausgaben; }

/// <summary>Auswertung eines Zeitraums. Beträge brutto wie gebucht; netto und Steuer aus dem Steuersatz der Buchung.</summary>
public sealed record Auswertung(int Jahr, int? Monat, IReadOnlyList<Entry> Buchungen, IReadOnlyList<KategorieSumme> Einnahmen, IReadOnlyList<KategorieSumme> Ausgaben, IReadOnlyList<MonatsSumme> Monate)
{
    public decimal EinnahmenNetto => Einnahmen.Sum(k => k.Netto);
    public decimal AusgabenNetto => Ausgaben.Sum(k => k.Netto);
    public decimal EinnahmenBrutto => Einnahmen.Sum(k => k.Brutto);
    public decimal AusgabenBrutto => Ausgaben.Sum(k => k.Brutto);
    /// <summary>Überschuss netto (Einnahmen − Ausgaben).</summary>
    public decimal Ueberschuss => EinnahmenNetto - AusgabenNetto;
    public decimal Umsatzsteuer => Einnahmen.Sum(k => k.Steuer);
    public decimal Vorsteuer => Ausgaben.Sum(k => k.Steuer);
    public decimal Zahllast => Umsatzsteuer - Vorsteuer;
    public string Titel => Monat is { } m ? new DateTime(Jahr, m, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("de-DE")) : Jahr.ToString(CultureInfo.InvariantCulture);
}

public static class GeldAuswertung
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public const string Hinweis = "Zusammenstellung aus deinen Buchungen. Ersetzt keine Steuerberatung; ELSTER-Übermittlung ist bewusst nicht eingebaut.";

    public static Auswertung Rechnen(IEnumerable<Entry> all, int jahr, int? monat = null)
    {
        var list = all.Where(e => e.Date.Year == jahr && (monat is null || e.Date.Month == monat)).OrderBy(e => e.Date).ToList();
        static List<KategorieSumme> Group(IEnumerable<Entry> e, int sign) =>
            [.. e.GroupBy(x => x.Category).Select(g => new KategorieSumme(g.Key, sign * g.Sum(x => x.Net), sign * g.Sum(x => x.Vat), sign * g.Sum(x => x.Amount), g.Count())).OrderByDescending(k => k.Brutto)];
        var months = Enumerable.Range(1, 12).Select(m => new MonatsSumme(m, list.Where(e => e.Date.Month == m && e.Amount > 0).Sum(e => e.Amount), -list.Where(e => e.Date.Month == m && e.Amount < 0).Sum(e => e.Amount))).ToList();
        return new Auswertung(jahr, monat, list, Group(list.Where(e => e.Amount > 0), 1), Group(list.Where(e => e.Amount < 0), -1), months);
    }

    static string N(decimal d) => d.ToString("0.00", De);
    static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    /// <summary>Auswertung als CSV (Semikolon, UTF-8 mit Kennung beim Schreiben): Kategorien, Monate, Summen.</summary>
    public static string Csv(Auswertung a)
    {
        var sb = new StringBuilder($"Auswertung {a.Titel}\r\n\r\nArt;Kategorie;Netto;Steuer;Brutto;Buchungen\r\n");
        foreach (var k in a.Einnahmen) sb.Append($"Einnahme;{Q(k.Kategorie)};{N(k.Netto)};{N(k.Steuer)};{N(k.Brutto)};{k.Anzahl}\r\n");
        foreach (var k in a.Ausgaben) sb.Append($"Ausgabe;{Q(k.Kategorie)};{N(k.Netto)};{N(k.Steuer)};{N(k.Brutto)};{k.Anzahl}\r\n");
        sb.Append($"\r\nEinnahmen netto;{N(a.EinnahmenNetto)}\r\nAusgaben netto;{N(a.AusgabenNetto)}\r\nÜberschuss;{N(a.Ueberschuss)}\r\nUmsatzsteuer eingenommen;{N(a.Umsatzsteuer)}\r\nVorsteuer gezahlt;{N(a.Vorsteuer)}\r\nDifferenz (etwa ans Finanzamt);{N(a.Zahllast)}\r\n");
        if (a.Monat is null)
        {
            sb.Append("\r\nMonat;Einnahmen;Ausgaben;Ergebnis\r\n");
            foreach (var m in a.Monate) sb.Append($"{De.DateTimeFormat.GetMonthName(m.Monat)};{N(m.Einnahmen)};{N(m.Ausgaben)};{N(m.Ergebnis)}\r\n");
        }
        sb.Append($"\r\n{Q(Hinweis)}\r\n");
        return sb.ToString();
    }

    /// <summary>Auswertung als XLSX: Blatt „Übersicht“ (Kategorien, Summen mit Formeln), „Monate“ und „Buchungen“ – über den vorhandenen Tabellen-Kern.</summary>
    public static void Xlsx(Auswertung a, string path)
    {
        var doc = new SheetDoc(); doc.Sheets.Clear();
        var s = new Sheet("Übersicht"); doc.Sheets.Add(s);
        void Txt(Sheet sh, int r, int c, string t, bool bold = false) { var x = sh.GetOrAdd(r, c); x.Raw = t; x.Bold = bold; }
        void Eur(Sheet sh, int r, int c, decimal v, bool bold = false) { var x = sh.GetOrAdd(r, c); x.Raw = v.ToString("0.00", De); x.Format = NumFmt.Euro; x.Bold = bold; }
        void Formel(Sheet sh, int r, int c, string f) { var x = sh.GetOrAdd(r, c); x.Raw = f; x.Format = NumFmt.Euro; x.Bold = true; }
        Txt(s, 0, 0, "Auswertung " + a.Titel, true);
        var row = 2; foreach (var (h, i) in new[] { "Art", "Kategorie", "Netto", "Steuer", "Brutto", "Buchungen" }.Select((h, i) => (h, i))) Txt(s, row, i, h, true);
        var first = row + 1;
        foreach (var (art, list) in new[] { ("Einnahme", a.Einnahmen), ("Ausgabe", a.Ausgaben) })
            foreach (var k in list)
            {
                row++; Txt(s, row, 0, art); Txt(s, row, 1, k.Kategorie); Eur(s, row, 2, art == "Einnahme" ? k.Netto : -k.Netto); Eur(s, row, 3, art == "Einnahme" ? k.Steuer : -k.Steuer); Eur(s, row, 4, art == "Einnahme" ? k.Brutto : -k.Brutto);
                s.GetOrAdd(row, 5).Raw = k.Anzahl.ToString(CultureInfo.InvariantCulture);
            }
        var last = row; row += 2;
        Txt(s, row, 1, "Überschuss (Einnahmen − Ausgaben)", true);
        if (last >= first) { Formel(s, row, 2, $"=SUMME(C{first + 1}:C{last + 1})"); Formel(s, row, 3, $"=SUMME(D{first + 1}:D{last + 1})"); Formel(s, row, 4, $"=SUMME(E{first + 1}:E{last + 1})"); }
        row += 2; Txt(s, row, 1, "Umsatzsteuer eingenommen"); Eur(s, row, 2, a.Umsatzsteuer);
        row++; Txt(s, row, 1, "Vorsteuer gezahlt"); Eur(s, row, 2, a.Vorsteuer);
        row++; Txt(s, row, 1, "Differenz (etwa ans Finanzamt)", true); Eur(s, row, 2, a.Zahllast, true);
        row += 2; Txt(s, row, 0, Hinweis);
        s.ColWidths[0] = 12; s.ColWidths[1] = 34; s.ColWidths[2] = 14; s.ColWidths[3] = 14; s.ColWidths[4] = 14; s.ColWidths[5] = 11;

        var m = new Sheet("Monate"); doc.Sheets.Add(m);
        foreach (var (h, i) in new[] { "Monat", "Einnahmen", "Ausgaben", "Ergebnis" }.Select((h, i) => (h, i))) Txt(m, 0, i, h, true);
        foreach (var x in a.Monate) { Txt(m, x.Monat, 0, De.DateTimeFormat.GetMonthName(x.Monat)); Eur(m, x.Monat, 1, x.Einnahmen); Eur(m, x.Monat, 2, x.Ausgaben); Formel(m, x.Monat, 3, $"=B{x.Monat + 1}-C{x.Monat + 1}"); m.GetOrAdd(x.Monat, 3).Bold = false; }
        Txt(m, 13, 0, "Summe", true); Formel(m, 13, 1, "=SUMME(B2:B13)"); Formel(m, 13, 2, "=SUMME(C2:C13)"); Formel(m, 13, 3, "=SUMME(D2:D13)");
        m.ColWidths[0] = 14; m.ColWidths[1] = 14; m.ColWidths[2] = 14; m.ColWidths[3] = 14; m.FreezeRows = 1;

        var b = new Sheet("Buchungen"); doc.Sheets.Add(b);
        foreach (var (h, i) in new[] { "Datum", "Text", "Kategorie", "Betrag", "MwSt-Satz", "Netto", "MwSt" }.Select((h, i) => (h, i))) Txt(b, 0, i, h, true);
        var r2 = 0;
        foreach (var e in a.Buchungen)
        {
            r2++; var d = b.GetOrAdd(r2, 0); d.Raw = e.Date.ToString("dd.MM.yyyy", De); d.Format = NumFmt.Datum;
            Txt(b, r2, 1, e.Text); Txt(b, r2, 2, e.Category); Eur(b, r2, 3, e.Amount); b.GetOrAdd(r2, 4).Raw = e.VatRate.ToString("0.##", De); Eur(b, r2, 5, e.Net); Eur(b, r2, 6, e.Vat);
        }
        b.ColWidths[0] = 12; b.ColWidths[1] = 44; b.ColWidths[2] = 16; b.ColWidths[3] = 13; b.ColWidths[5] = 13; b.ColWidths[6] = 12; b.FreezeRows = 1;
        SheetFiles.Save(doc, path);
    }

    /// <summary>Auswertung als PDF: Kennzahlen, Balkendiagramm der Monate (bei Jahresauswertung), Tabellen je Kategorie.</summary>
    public static int Pdf(Auswertung a, string path, string firma = "")
    {
        const double mm = 72 / 25.4;
        using var pdf = new BizPdf("Auswertung " + a.Titel, firma.Length > 0 ? firma : "Trendo OS") { Footer = Hinweis };
        pdf.Write("Auswertung " + a.Titel, 16, true, pdf.Accent);
        if (firma.Trim().Length > 0) pdf.Write(firma.Trim(), 10, false, BizPdf.Grey);
        pdf.Y += 3;
        pdf.Write($"Einnahmen netto {BizJson.Euro(a.EinnahmenNetto)}   ·   Ausgaben netto {BizJson.Euro(a.AusgabenNetto)}   ·   Überschuss {BizJson.Euro(a.Ueberschuss)}", 11, true);
        pdf.Write($"Umsatzsteuer eingenommen {BizJson.Euro(a.Umsatzsteuer)}   ·   Vorsteuer gezahlt {BizJson.Euro(a.Vorsteuer)}   ·   Differenz {BizJson.Euro(a.Zahllast)}", 10);
        pdf.Y += 4;
        if (a.Monat is null)
        {
            // Balkendiagramm: je Monat Einnahmen (grün) und Ausgaben (rot), Achse mit Höchstwert
            var max = Math.Max(1m, a.Monate.Max(x => Math.Max(x.Einnahmen, x.Ausgaben))); const double h = 42, x0 = 20, w = 170;
            var top = pdf.Y; var slot = w / 12;
            pdf.G.DrawLine(new XPen(BizPdf.Line, 0.2 * mm), x0 * mm, (top + h) * mm, (x0 + w) * mm, (top + h) * mm);
            pdf.At("max. " + BizJson.Euro(max), x0, top - 4, 60, 7, false, BizPdf.Grey);
            foreach (var x in a.Monate)
            {
                var bx = x0 + (x.Monat - 1) * slot; var hi = (double)(x.Einnahmen / max) * h; var ho = (double)(x.Ausgaben / max) * h;
                if (hi > 0) pdf.Box(bx + 1.5, top + h - hi, slot / 2 - 2, hi, XColor.FromArgb(0x2F, 0x9E, 0x62));
                if (ho > 0) pdf.Box(bx + slot / 2, top + h - ho, slot / 2 - 2, ho, XColor.FromArgb(0xD9, 0x53, 0x3F));
                pdf.At(De.DateTimeFormat.GetAbbreviatedMonthName(x.Monat).TrimEnd('.'), bx, top + h + 1, slot, 7, false, BizPdf.Grey, XStringFormats.TopCenter);
            }
            pdf.Y = top + h + 7;
            pdf.Box(20, pdf.Y, 3, 3, XColor.FromArgb(0x2F, 0x9E, 0x62)); pdf.At("Einnahmen", 24.5, pdf.Y - 0.5, 30, 8);
            pdf.Box(50, pdf.Y, 3, 3, XColor.FromArgb(0xD9, 0x53, 0x3F)); pdf.At("Ausgaben", 54.5, pdf.Y - 0.5, 30, 8);
            pdf.Y += 8;
        }
        void Block(string title, IReadOnlyList<KategorieSumme> list)
        {
            pdf.Need(20); pdf.Write(title, 12, true); pdf.Y += 1;
            var rows = list.Select(k => new[] { k.Kategorie, BizJson.Euro(k.Netto), BizJson.Euro(k.Steuer), BizJson.Euro(k.Brutto), k.Anzahl.ToString(De) }).ToList();
            rows.Add(["Summe", BizJson.Euro(list.Sum(k => k.Netto)), BizJson.Euro(list.Sum(k => k.Steuer)), BizJson.Euro(list.Sum(k => k.Brutto)), list.Sum(k => k.Anzahl).ToString(De)]);
            pdf.Table(["Kategorie", "Netto", "Steuer", "Brutto", "Buchungen"], [66, 28, 26, 28, 22], "lrrrr", rows, 9, i => i == rows.Count - 1);
            pdf.Y += 4;
        }
        Block("Einnahmen", a.Einnahmen); Block("Ausgaben", a.Ausgaben);
        if (a.Monat is null)
        {
            pdf.Need(20); pdf.Write("Monate", 12, true); pdf.Y += 1;
            pdf.Table(["Monat", "Einnahmen", "Ausgaben", "Ergebnis"], [50, 40, 40, 40], "lrrr",
                a.Monate.Select(x => new[] { De.DateTimeFormat.GetMonthName(x.Monat), BizJson.Euro(x.Einnahmen), BizJson.Euro(x.Ausgaben), BizJson.Euro(x.Ergebnis) }), 9);
        }
        return pdf.Save(path);
    }
}

// ---------------------------------------------------------------- DATEV-Buchungsstapel
/// <summary>Kontenzuordnung für den DATEV-Export: Kategorie → Sachkonto. Die Vorgaben sind übliche SKR-03-Konten als VORSCHLAG – der Steuerberater legt fest,
/// was gilt; alles ist einstellbar (finanzen/datev-konten.json).</summary>
public sealed class DatevKonten
{
    public string Berater { get; set; } = "";
    public string Mandant { get; set; } = "";
    /// <summary>Geldkonto (SKR 03: 1200 Bank, 1000 Kasse).</summary>
    public string Bank { get; set; } = "1200";
    public int Sachkontenlaenge { get; set; } = 4;
    public string Kontenrahmen { get; set; } = "SKR 03 (Vorschlag)";
    public string Stand { get; set; } = "16.11.2026";
    public string Erloes19 { get; set; } = "8400";
    public string Erloes7 { get; set; } = "8300";
    public string ErloesOhne { get; set; } = "8195";
    public string Privat { get; set; } = "1800";
    public string Sonst { get; set; } = "4900";
    public Dictionary<string, string> Kategorie { get; set; } = new()
    {
        ["Material"] = "3400", ["Software"] = "4964", ["Werbung"] = "4610", ["Büro"] = "4930", ["Fahrt"] = "4673", ["Gebühren"] = "4970", ["Sonstiges"] = "4900",
    };
    /// <summary>Automatikkonten: DATEV rechnet die Steuer aus dem Konto selbst – dort darf kein Steuerschlüssel stehen.</summary>
    public List<string> Automatik { get; set; } = ["8400", "8300", "3400", "3300"];

    public static string Datei(string dataRoot) => Path.Combine(dataRoot, "finanzen", "datev-konten.json");
    public static DatevKonten Laden(string dataRoot)
    {
        try { var f = Datei(dataRoot); return File.Exists(f) ? JsonSerializer.Deserialize<DatevKonten>(File.ReadAllText(f)) ?? new() : new(); }
        catch (JsonException) { return new(); }
    }
    public void Speichern(string dataRoot) => AtomicFile.WriteAllText(Datei(dataRoot), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Gegenkonto einer Buchung: Einnahmen nach Steuersatz, Ausgaben nach Kategorie; private Haushalts-Kategorien auf das Privatkonto.</summary>
    public string Gegenkonto(Entry e)
    {
        if (e.Amount > 0 && e.Category == "Einnahmen") return e.VatRate == LegalRates.VatStandard ? Erloes19 : e.VatRate == LegalRates.VatReduced ? Erloes7 : ErloesOhne;
        if (Kategorie.TryGetValue(e.Category, out var k) && k.Trim().Length > 0) return k.Trim();
        return FinanceExtra.Household.Contains(e.Category) ? Privat : e.Amount > 0 ? ErloesOhne : Sonst;
    }
}

/// <summary>DATEV-Format „Buchungsstapel“ (ASCII/CSV, Kennung EXTF) nach der öffentlichen Schnittstellenbeschreibung von DATEV (developer.datev.de, „DATEV-Format“):
/// Zeile 1 Vorlaufsatz, Zeile 2 Spaltenüberschriften, ab Zeile 3 Buchungen; Semikolon, Text in Anführungszeichen, Dezimalkomma, Belegdatum TTMM, Windows-1252, CRLF.
/// Geschrieben werden die 14 führenden Spalten (Umsatz bis Buchungstext). EHRLICH: nicht gegen DATEV-Software geprüft – vor dem ersten Einsatz mit dem Steuerberater testen.</summary>
public static class Datev
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public const string Kennzeichnung = "Für den Steuerberater – ungeprüft gegen DATEV-Software. Konten sind Vorschläge und müssen zum Kontenrahmen der Kanzlei passen.";
    public static readonly string[] Spalten = ["Umsatz (ohne Soll/Haben-Kz)", "Soll/Haben-Kennzeichen", "WKZ Umsatz", "Kurs", "Basis-Umsatz", "WKZ Basis-Umsatz", "Konto", "Gegenkonto (ohne BU-Schlüssel)",
        "BU-Schlüssel", "Belegdatum", "Belegfeld 1", "Belegfeld 2", "Skonto", "Buchungstext"];

    /// <summary>Steuerschlüssel (BU) für Nicht-Automatikkonten: Umsatzsteuer 19 % = 3, 7 % = 2; Vorsteuer 19 % = 9, 7 % = 8. Sonst leer.</summary>
    public static string Bu(Entry e, string gegenkonto, DatevKonten k)
    {
        if (e.VatRate == 0 || k.Automatik.Contains(gegenkonto)) return "";
        var income = e.Amount > 0;
        return e.VatRate == LegalRates.VatStandard ? (income ? "3" : "9") : e.VatRate == LegalRates.VatReduced ? (income ? "2" : "8") : "";
    }

    static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    static string Beleg(string text)
    {
        // Rechnungsnummer aus dem Buchungstext als Belegfeld 1 (nur Zeichen, die DATEV dort erlaubt: Buchstaben, Ziffern und $ & % * + - /)
        var m = System.Text.RegularExpressions.Regex.Match(text, @"[A-Z]{2}-\d{4}-\d{3,}");
        var s = m.Success ? m.Value : "";
        return s.Length > 36 ? s[..36] : s;
    }

    /// <summary>Buchungsstapel eines Jahres als Text. Storno-Buchungen und ihre Ursprungsbuchung bleiben enthalten (beide Richtungen), damit nichts fehlt.</summary>
    public static string Stapel(IEnumerable<Entry> all, int jahr, DatevKonten k, DateTime? erzeugt = null)
    {
        var list = all.Where(e => e.Date.Year == jahr && e.Amount != 0).OrderBy(e => e.Date).ToList();
        var now = erzeugt ?? DateTime.Now;
        int berater = int.TryParse(k.Berater.Trim(), out var bn) ? bn : 0, mandant = int.TryParse(k.Mandant.Trim(), out var mn) ? mn : 0;
        var sb = new StringBuilder();
        // Vorlaufsatz: Kennung, Versionsnummer 700, Formatkategorie 21 = Buchungsstapel, Formatname, Formatversion, erzeugt am, …, Berater, Mandant, WJ-Beginn, Sachkontenlänge, Datum von/bis, Bezeichnung, Diktatkürzel, Buchungstyp 1 = Finanzbuchführung, Rechnungslegungszweck, Festschreibung 0, Währung
        sb.Append(string.Join(";", Q("EXTF"), "700", "21", Q("Buchungsstapel"), "13", now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture), "", Q("TR"), Q(""), Q(""),
            berater.ToString(CultureInfo.InvariantCulture), mandant.ToString(CultureInfo.InvariantCulture), $"{jahr}0101", k.Sachkontenlaenge.ToString(CultureInfo.InvariantCulture), $"{jahr}0101", $"{jahr}1231",
            Q("Trendo Geld " + jahr), Q(""), "1", "0", "0", Q("EUR"), "", "", "", "", "", "", "", "", "")).Append("\r\n");
        sb.Append(string.Join(";", Spalten)).Append("\r\n");
        foreach (var e in list)
        {
            var gegen = k.Gegenkonto(e);
            var text = e.Text.Length > 60 ? e.Text[..60] : e.Text;
            // Geldkonto im Soll bei Einnahme, im Haben bei Ausgabe; der Umsatz steht immer ohne Vorzeichen
            sb.Append(string.Join(";", Math.Abs(e.Amount).ToString("0.00", De), Q(e.Amount > 0 ? "S" : "H"), Q("EUR"), "", "", "", k.Bank, gegen, Q(Bu(e, gegen, k)),
                e.Date.ToString("ddMM", CultureInfo.InvariantCulture), Q(Beleg(e.Text)), Q(""), "", Q(text))).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Datei schreiben (Windows-1252, wie DATEV es erwartet); Name nach DATEV-Gewohnheit „EXTF_Buchungsstapel_JJJJ.csv“.</summary>
    public static string Schreiben(IEnumerable<Entry> all, int jahr, DatevKonten k, string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"EXTF_Buchungsstapel_{jahr}.csv");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var enc = Encoding.GetEncoding(1252, new EncoderReplacementFallback("?"), new DecoderReplacementFallback("?"));
        AtomicFile.WriteAllBytes(path, enc.GetBytes(Stapel(all, jahr, k)));
        return path;
    }
}
