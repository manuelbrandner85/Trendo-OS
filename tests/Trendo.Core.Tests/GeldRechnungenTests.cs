using System.Diagnostics;
using System.Text;
using Trendo.Core.Business;
using Trendo.Core.Office;
using Trendo.Core.Sys;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0694 „Geld &amp; Rechnungen“ als vollwertiges Programm – Kerne ohne Oberfläche.
/// INV-01 bis INV-05 stammen aus dem unfertigen Zweig sicherung/T-0675-business-a (dort BusinessGeldTests) und prüfen den von dort übernommenen Rechnungs-Kern.
/// Die Bank-Beispiele sind handgeschrieben nach Spezifikation (ISO 20022 camt.053, SWIFT MT940, bekannte CSV-Kopfzeilen) – keine echten Kontodaten;
/// die IBANs sind öffentliche Beispiel-IBANs mit gültiger Prüfziffer.</summary>
public class GeldRechnungenTests(ITestOutputHelper output)
{
    static readonly DateOnly Today = new(2026, 11, 15);
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-geld-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    /// <summary>pdftotext (Poppler/Xpdf) aus dem Suchpfad – ein fremdes Programm, das das PDF unabhängig von PDFsharp liest. null = auf diesem PC nicht vorhanden.</summary>
    internal static string? PdfToText()
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var name in new[] { "pdftotext.exe", "pdftotext" })
                try { var f = Path.Combine(dir.Trim('"'), name); if (File.Exists(f)) return f; } catch (ArgumentException) { }
        return null;
    }

    internal static string ReadPdfText(string tool, string pdf)
    {
        var psi = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8 };
        foreach (var a in new[] { "-enc", "UTF-8", "-layout", pdf, "-" }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!; var text = p.StandardOutput.ReadToEnd(); p.WaitForExit(30000);
        Assert.True(p.ExitCode == 0, "pdftotext meldet Fehler: " + p.StandardError.ReadToEnd());
        return text;
    }

    // ===================== Rechnungen & Angebote (Kern aus sicherung/T-0675-business-a) =====================

    [Fact(DisplayName = "INV-01: Summen je Steuersatz (19 % und 7 %), Rabatt je Position, Steuer je Satz gerundet")]
    public void Rechnung_summen()
    {
        var b = Rechnungen.Beispiel(Today); var v = b.Vorgaenge[0];
        Assert.Equal(459.00m, Rechnungen.PosNetto(v.Positionen[1])); // 6 × 85 − 10 %
        var s = Rechnungen.Summe(v);
        Assert.Equal(1715.08m, s.Netto); Assert.Equal(319.14m, s.Steuer); Assert.Equal(2034.22m, s.Brutto);
        Assert.Equal([(19m, 1659.00m, 315.21m), (7m, 56.08m, 3.93m)], s.JeSatz.Select(g => (g.Satz, g.Netto, g.Steuer)));
        v.Kleinunternehmer = true;
        Assert.Equal((1715.08m, 0m), (Rechnungen.Summe(v).Brutto, Rechnungen.Summe(v).Steuer));
    }

    [Fact(DisplayName = "INV-02: Pflichtangaben nach § 14 Abs. 4 UStG – Steuernummer, Anschrift, Leistungsdatum, 0 % braucht Befreiungsgrund, Kleinbetragsrechnung bis 250 €; das Programm sagt genau, was fehlt")]
    public void Rechnung_pflichtangaben()
    {
        var b = Rechnungen.Beispiel(Today); var v = b.Vorgaenge[0];
        Assert.True(Rechnungen.Vollstaendig(b, v)); Assert.Empty(Rechnungen.Fehlend(b, v));
        b.Firma.SteuerNr = ""; v.Leistung = "";
        var miss = Rechnungen.Pruefen(b, v).Where(p => !p.Ok).Select(p => p.Id).ToList();
        Assert.Equal(["steuernr", "leistung"], miss);
        var saetze = Rechnungen.Fehlend(b, v);
        Assert.Equal(2, saetze.Count); Assert.Contains("Steuernummer oder USt-IdNr. fehlt", saetze[0]); Assert.Contains("Meine Firma", saetze[0]); Assert.Contains("Leistungsdatum", saetze[1]);
        b.Firma.SteuerNr = "12/345/67890"; v.Leistung = "November 2026";
        v.Positionen[2].Ust = 0; Assert.Contains(Rechnungen.Pruefen(b, v), p => p.Id == "befreiung" && !p.Ok);
        v.Befreiung = "Steuerfreie Leistung nach § 4 Nr. 21 UStG"; Assert.True(Rechnungen.Vollstaendig(b, v));
        v.Positionen[2].Ust = 16; Assert.Contains(Rechnungen.Pruefen(b, v), p => p.Id == "saetze" && !p.Ok);
        // Kleinbetrag: ohne Steuernummer und Kundenanschrift zulässig
        var k = Rechnungen.Neu(b, "rechnung", Today); k.Positionen = [new() { Text = "Kleinteil", Menge = 1, Preis = 100 }]; b.Firma.SteuerNr = "";
        Assert.True(Rechnungen.Vollstaendig(b, k)); Assert.Contains(Rechnungen.Pruefen(b, k), p => p.Id == "klein");
        k.Positionen[0].Preis = 300; Assert.False(Rechnungen.Vollstaendig(b, k));
        // fehlender Absender und Kunde werden einzeln benannt
        var leer = new RechnungsBuch(); var e = Rechnungen.Neu(leer, "rechnung", Today); e.Positionen = [new() { Text = "Webseite", Menge = 1, Preis = 1000 }];
        var ids = Rechnungen.Pruefen(leer, e).Where(p => !p.Ok).Select(p => p.Id).ToList();
        Assert.Equal(["absender", "empfaenger", "steuernr"], ids);
    }

    [Fact(DisplayName = "T-0694: Steuersätze sind eine einstellbare Tabelle mit Stand-Datum – ein neuer Satz gilt, ein gestrichener wird abgewiesen; Unsinn wird verständlich abgelehnt")]
    public void Steuersaetze_tabelle()
    {
        var b = Rechnungen.Beispiel(Today); var v = b.Vorgaenge[0];
        Assert.Equal([19m, 7m, 0m], b.Steuer.Erlaubt()); Assert.Equal(LegalRates.Checked, b.Steuer.Stand);
        v.Positionen[0].Ust = 16; Assert.False(Rechnungen.Vollstaendig(b, v));
        b.Steuer.Saetze = Steuersaetze.Lesen("19; 16 %; 7; 0"); b.Steuer.Stand = "01.07.2027";
        Assert.True(Rechnungen.Vollstaendig(b, v));
        Assert.Equal(16m, Rechnungen.Summe(v).JeSatz.Single(g => g.Satz == 16m).Satz);
        var back = RechnungsBuch.FromJson(b.ToJson()); Assert.Equal([19m, 16m, 7m, 0m], back.Steuer.Erlaubt()); Assert.Equal("01.07.2027", back.Steuer.Stand);
        Assert.Contains("kein Steuersatz", Assert.Throws<FormatException>(() => Steuersaetze.Lesen("19; viel")).Message);
        Assert.Throws<FormatException>(() => Steuersaetze.Lesen(" "));
    }

    [Fact(DisplayName = "INV-03: Nummernkreis lückenlos je Art und Jahr, gemeinsam mit den alten HTML-Rechnungen; Archiv; Lückenprüfung")]
    public void Rechnung_nummern()
    {
        var root = Tmp();
        try
        {
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(Today);
            Assert.Equal("RE-2026-001", kreis.Vorschau("rechnung", Today));
            // eine ältere Rechnung (HTML) belegt schon die 5
            var led = new Ledger(root); Directory.CreateDirectory(led.InvoiceDir); File.WriteAllText(Path.Combine(led.InvoiceDir, "RE-2026-005.html"), "<html></html>");
            Assert.Equal("RE-2026-006", Rechnungen.Festschreiben(b, b.Vorgaenge[0], kreis));
            Assert.True(File.Exists(Path.Combine(kreis.Archiv, "RE-2026-006.json")));
            Assert.Contains("RE-2026-006", led.InvoiceNumbers()); // „Online verkaufen“ sieht die Nummer – nie doppelt
            Assert.Equal("RE-2026-007", Finance.NextInvoiceNumber(led.InvoiceNumbers(), Today));
            Assert.Throws<InvalidOperationException>(() => Rechnungen.Festschreiben(b, b.Vorgaenge[0], kreis));
            var a = Rechnungen.Neu(b, "angebot", Today); Rechnungen.KundeSetzen(a, b.Kunden[1]); a.Positionen = [new() { Text = "Beratung", Menge = 2, Preis = 85, Einheit = "Std." }];
            Assert.Equal("AN-2026-001", Rechnungen.Festschreiben(b, a, kreis));
            Assert.Equal("RE-2027-001", kreis.Vorschau("rechnung", new DateOnly(2027, 1, 2)));
            Assert.Single(kreis.Abgelegt().Where(x => x.Art == "rechnung"));
            Assert.Equal(["RE-2026-002"], Rechnungen.Luecken(["RE-2026-001", "RE-2026-003", "AN-2026-001"], "RE"));
            Assert.Throws<InvalidOperationException>(() => Rechnungen.KundeSetzen(a, b.Kunden[0]));
            // unvollständig: keine Nummer wird verbraucht
            var u = Rechnungen.Neu(b, "rechnung", Today); u.Positionen = [new() { Text = "Großauftrag", Menge = 1, Preis = 5000 }];
            var ex = Assert.Throws<InvalidOperationException>(() => Rechnungen.Festschreiben(b, u, kreis));
            Assert.Contains("Kunde", ex.Message); Assert.Equal("RE-2026-007", kreis.Vorschau("rechnung", Today));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "INV-04: Angebot → Auftragsbestätigung → Rechnung, Storno mit eigener Nummer, Status offen/überfällig/bezahlt, Zahlungseingang zuordnen, Buchungen je Satz")]
    public void Rechnung_kette()
    {
        var root = Tmp();
        try
        {
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(Today);
            var an = Rechnungen.Neu(b, "angebot", new DateOnly(2026, 9, 20)); Rechnungen.KundeSetzen(an, b.Kunden[0]); an.Positionen = [.. b.Vorgaenge[0].Positionen.Select(BizJson.Clone)];
            Assert.Throws<InvalidOperationException>(() => Rechnungen.Weiter(b, an, "auftrag", Today));
            Rechnungen.Festschreiben(b, an, kreis);
            Assert.Equal("offen", Rechnungen.Status(an, new DateOnly(2026, 10, 1)).Id);
            Assert.Equal("abgelaufen", Rechnungen.Status(an, Today).Id);
            var ab = Rechnungen.Weiter(b, an, "auftrag", new DateOnly(2026, 9, 25));
            Assert.True(an.Angenommen); Assert.Equal(an.Nummer, ab.Herkunft); Assert.True(ab.Entwurf); Assert.Equal(3, ab.Positionen.Count);
            Assert.Equal("AB-2026-001", Rechnungen.Festschreiben(b, ab, kreis));
            var re = Rechnungen.Weiter(b, ab, "rechnung", new DateOnly(2026, 10, 1)); re.Leistung = "September 2026";
            Rechnungen.Festschreiben(b, re, kreis);
            Assert.Equal(new DateOnly(2026, 10, 15), Rechnungen.Faellig(re));
            Assert.Equal("Überfällig seit 31 Tagen", Rechnungen.Status(re, Today).Text);
            var teil = Rechnungen.ZahlungZuordnen(re, 1000m, Today, "Kontoauszug");
            Assert.Equal(1000m, teil.Sum(e => e.Amount));
            Assert.Equal("ueberfaellig", Rechnungen.Status(re, Today).Id); Assert.Equal(1034.22m, Rechnungen.Offen(re));
            Assert.Contains("Offen sind nur", Assert.Throws<ArgumentException>(() => Rechnungen.Zahlung(re, 2000m, Today)).Message); // mehr als offen wird abgewiesen
            Rechnungen.ZahlungZuordnen(re, 1034.22m, Today);
            Assert.Equal("Bezahlt am 15.11.2026", Rechnungen.Status(re, Today).Text); Assert.Equal(2, re.Zahlungen.Count); Assert.Equal("Kontoauszug", re.Zahlungen[0].Hinweis);
            var book = Rechnungen.Buchungen(re, 2034.22m, Today);
            Assert.Equal([(1974.21m, 19m), (60.01m, 7m)], book.Select(e => (e.Amount, e.VatRate)));
            var st = Rechnungen.Stornieren(b, re, Today, kreis);
            Assert.Equal("SR-2026-001", st.Nummer); Assert.Equal(-2034.22m, Rechnungen.Summe(st).Brutto); Assert.True(re.Storniert);
            Assert.Equal("storniert", Rechnungen.Status(re, Today).Id);
            Assert.Throws<InvalidOperationException>(() => Rechnungen.Stornieren(b, re, Today, kreis));
            b.Vorgaenge.AddRange([an, ab, re, st]);
            var csv = Rechnungen.Csv(b, Today);
            Assert.Contains("\"Stornorechnung\";\"SR-2026-001\";15.11.2026;\"Beispiel GmbH\";-1715,08;-319,14;-2034,22", csv);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "INV-05: PDF der Rechnung und XRechnung (CII) mit zwei Steuersätzen – wieder eingelesen stimmen Nummer, Summe, Steuer und Positionen")]
    public void Rechnung_pdf_xrechnung()
    {
        var root = Tmp();
        try
        {
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(Today); var v = b.Vorgaenge[0];
            Assert.Throws<InvalidOperationException>(() => Rechnungen.XRechnung(b, v)); // Entwurf
            var draft = Path.Combine(root, "entwurf.pdf"); Assert.True(RechnungPdf.Write(b, v, draft) >= 1);
            Rechnungen.Festschreiben(b, v, kreis);
            var pdf = Path.Combine(root, "re.pdf"); Assert.Equal(1, RechnungPdf.Write(b, v, pdf)); Assert.Equal(1, PdfTools.Read(pdf).Pages);
            var xml = Rechnungen.XRechnung(b, v);
            var r = ERechnung.Read(xml);
            Assert.Equal((v.Nummer, 2034.22m, 319.14m, 3, "Erika Muster Webdesign", "Beispiel GmbH", "BEST-2026-17"), (r.Number, r.Gross, r.Vat, r.Lines, r.Seller, r.Buyer, r.Reference));
            var text = ERechnung.Utf8(xml);
            Assert.Contains("HUR", text); Assert.Contains("<ram:RateApplicablePercent>7", text);
            Assert.Contains("Hauptstraße 5", text); // Straße des Kunden – nicht die Zusatzzeile „z. Hd.“
            // Kleinunternehmer: Kategorie E mit § 19-Hinweis
            b.Firma.Kleinunternehmer = true; var k = Rechnungen.Neu(b, "rechnung", Today); Rechnungen.KundeSetzen(k, b.Kunden[0]); k.Positionen = [new() { Text = "Beratung", Menge = 2, Einheit = "Std.", Preis = 85 }];
            Rechnungen.Festschreiben(b, k, kreis);
            var kx = ERechnung.Utf8(Rechnungen.XRechnung(b, k)); Assert.Contains("§ 19 UStG", kx); Assert.Equal(170m, ERechnung.Read(Rechnungen.XRechnung(b, k)).Gross);
            b.Firma.Iban = "DE00"; Assert.Throws<ArgumentException>(() => Rechnungen.XRechnung(b, k));
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== T-0694: E-Rechnung als ZUGFeRD-PDF =====================

    [Fact(DisplayName = "T-0694 E-Rechnung: ZUGFeRD-PDF trägt factur-x.xml – mit der Bibliothek wieder eingelesen stimmen Nummer, Netto, Steuer je Satz, Brutto und alle Positionen; XMP nennt PDF/A-3 und Factur-X")]
    public void Zugferd_pdf_rundreise()
    {
        var root = Tmp();
        try
        {
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(Today); var v = b.Vorgaenge[0];
            Rechnungen.Festschreiben(b, v, kreis);
            var file = Path.Combine(root, v.Nummer + ".pdf");
            File.WriteAllBytes(file, ZugferdPdf.Build(b, v));
            Assert.Equal(1, PdfTools.Read(file).Pages); // PDFsharp öffnet die angehängte Fassung (inkrementelle Aktualisierung)
            var xml = ZugferdPdf.Extract(file); Assert.NotNull(xml);
            var g = ERechnung.ReadDetailed(xml!);
            var s = Rechnungen.Summe(v);
            Assert.Equal((v.Nummer, s.Netto, s.Steuer, s.Brutto), (g.Number, g.Net, g.Vat, g.Gross));
            Assert.Equal((1715.08m, 319.14m, 2034.22m), (g.Net, g.Vat, g.Gross));
            Assert.Equal(Today, g.Date); Assert.Equal(Today, g.Delivery);
            Assert.Equal("Erika Muster Webdesign", g.Seller); Assert.Equal("12/345/67890", g.SellerTaxId); Assert.Equal("Beispiel GmbH", g.Buyer); Assert.Equal("DE89370400440532013000", g.Iban);
            Assert.Equal([(19m, 1659.00m, 315.21m), (7m, 56.08m, 3.93m)], g.Taxes.OrderByDescending(t => t.Rate).Select(t => (t.Rate, t.Basis, t.Tax)));
            Assert.Equal(v.Positionen.Select(p => (p.Text, p.Menge, Rechnungen.PosNetto(p), p.Ust)), g.Lines.Select(l => (l.Text, l.Menge, l.NettoGesamt, l.Ust)));
            Assert.Equal("Comfort", g.Profile);
            var xmp = ZugferdPdf.Metadata(file);
            Assert.Contains("<pdfaid:part>3</pdfaid:part>", xmp); Assert.Contains("<pdfaid:conformance>B</pdfaid:conformance>", xmp);
            Assert.Contains("<fx:DocumentFileName>factur-x.xml</fx:DocumentFileName>", xmp); Assert.Contains("<fx:ConformanceLevel>EN 16931</fx:ConformanceLevel>", xmp);
            var raw = Encoding.Latin1.GetString(File.ReadAllBytes(file));
            Assert.Matches(@"/AFRelationship\s*/Alternative", raw); Assert.Matches(@"/AF\s*\[", raw); Assert.Matches(@"/Subtype\s*/text#2Fxml", raw); Assert.Contains("/GTS_PDFA1", raw); Assert.Contains("/Prev ", raw);
            // ZUGFeRD ist nachsichtiger als XRechnung: ohne E-Mail und Käuferreferenz möglich, XRechnung sagt genau, was fehlt
            b.Firma.Email = ""; b.Kunden[0].Email = ""; b.Kunden[0].Referenz = ""; v.KundeNummer = "";
            Assert.Empty(Rechnungen.ERechnungFehlend(b, v, xrechnung: false));
            var fehlt = Rechnungen.ERechnungFehlend(b, v, xrechnung: true);
            Assert.Contains(fehlt, x => x.Contains("E-Mail")); Assert.Contains(fehlt, x => x.Contains("Käuferreferenz"));
            Assert.NotNull(ZugferdPdf.Build(b, v));
            // normales PDF ohne Anhang: Extract meldet „kein Anhang“
            var plain = Path.Combine(root, "ohne.pdf"); RechnungPdf.Write(b, v, plain); Assert.Null(ZugferdPdf.Extract(plain));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0694 PDF: ein fremdes Programm (pdftotext) liest aus dem Rechnungs-PDF alle Pflichtangaben nach § 14 UStG – Namen und Anschriften, Steuernummer, Datum, Nummer, Leistung, Leistungsdatum, Entgelt, Steuersatz und -betrag")]
    public void Pdf_text_pflichtangaben()
    {
        var tool = PdfToText();
        if (tool is null) { output.WriteLine("NICHT GETESTET: pdftotext ist auf diesem PC nicht vorhanden."); return; }
        var root = Tmp();
        try
        {
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(Today); var v = b.Vorgaenge[0]; v.Leistung = "03.11.2026";
            Rechnungen.Festschreiben(b, v, kreis);
            foreach (var zugferd in new[] { false, true })
            {
                var file = Path.Combine(root, zugferd ? "zugferd.pdf" : "rechnung.pdf");
                if (zugferd) File.WriteAllBytes(file, ZugferdPdf.Build(b, v)); else RechnungPdf.Write(b, v, file);
                var text = ReadPdfText(tool, file);
                foreach (var must in new[] { "Erika Muster Webdesign", "Musterstraße 1", "12345 Musterstadt", "Beispiel GmbH", "Hauptstraße 5", "54321 Beispielhausen", "12/345/67890", "15.11.2026", "RE-2026-001",
                    "Webseite Grundpaket", "Beratung", "Leistungsdatum", "03.11.2026", "1.715,08 €", "19 %", "315,21 €", "7 %", "3,93 €", "2.034,22 €", "DE89370400440532013000", "29.11.2026" })
                    Assert.True(text.Contains(must, StringComparison.Ordinal), $"„{must}“ fehlt im Text von {Path.GetFileName(file)}:\n{text}");
            }
            // Kleinunternehmer: Hinweis auf § 19 UStG im PDF, keine Umsatzsteuerzeile
            b.Firma.Kleinunternehmer = true; var k = Rechnungen.Neu(b, "rechnung", Today); Rechnungen.KundeSetzen(k, b.Kunden[0]); k.Positionen = [new() { Text = "Beratung", Menge = 2, Einheit = "Std.", Preis = 85 }];
            Rechnungen.Festschreiben(b, k, kreis);
            var kf = Path.Combine(root, "klein.pdf"); RechnungPdf.Write(b, k, kf); var kt = ReadPdfText(tool, kf);
            Assert.Contains("§ 19 UStG", kt); Assert.DoesNotContain("zzgl.", kt); Assert.Contains("170,00 €", kt);
            output.WriteLine("pdftotext: " + tool);
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== T-0694: kein Datenverlust =====================

    [Fact(DisplayName = "T-0694 Datensicherheit: beschädigte Buchungs- oder Rechnungsdatei wird nicht still geleert – beiseitegelegt, letzter gesicherter Stand zurück, Hinweis für die Oberfläche; Rückgängig stellt den genauen Stand wieder her")]
    public void Wiederherstellung()
    {
        var root = Tmp();
        try
        {
            var led = new Ledger(root);
            led.Add(new(Today, "Miete", -800m, "Wohnen")); led.Add(new(Today, "Gehalt", 2500m, "Einnahmen"));
            var file = Path.Combine(root, "finanzen", "buchungen.json");
            Assert.True(File.Exists(file + ".bak")); // vorige Fassung (eine Buchung) liegt daneben
            var before = led.Raw();
            File.WriteAllText(file, before[..(before.Length / 2)]); // abgeschnitten wie nach einem Abbruch eines fremden Schreibers
            var all = new Ledger(root).All();
            Assert.Single(all); Assert.Equal("Miete", all[0].Text); // Stand vor der letzten Buchung – höchstens eine Buchung fehlt
            Assert.Contains("beschädigt", new Ledger(root).RecoveryNote()); Assert.Single(Directory.GetFiles(Path.Combine(root, "finanzen"), "buchungen.beschaedigt-*.json"));
            new Ledger(root).RecoveryNoteSeen(); Assert.Null(new Ledger(root).RecoveryNote());
            // Rückgängig über Rohstand
            led.RawSet(before); Assert.Equal(2, led.All().Count); led.RawSet(""); Assert.Empty(led.All());
            // Rechnungsbuch genauso
            var store = new RechnungsBuchStore(root); var b = Rechnungen.Beispiel(Today);
            store.Speichern(b); b.Firma.Name = "Erika Muster Webdesign und Druck"; store.Speichern(b);
            File.WriteAllText(store.Datei, "{ \"firma\": ");
            var back = store.Laden();
            Assert.Equal("Erika Muster Webdesign", back.Firma.Name); Assert.Contains("beschädigt", store.Wiederhergestellt); Assert.Single(back.Vorgaenge);
            // ohne jede Sicherung: leeres Buch, aber die kaputte Datei bleibt liegen und der Hinweis sagt es
            var root2 = Tmp(); Directory.CreateDirectory(Path.Combine(root2, "finanzen"));
            File.WriteAllText(Path.Combine(root2, "finanzen", "buchungen.json"), "[{\"Date\":");
            Assert.Empty(new Ledger(root2).All()); Assert.Contains("keine Sicherung", new Ledger(root2).RecoveryNote());
            Assert.Single(Directory.GetFiles(Path.Combine(root2, "finanzen"), "buchungen.beschaedigt-*.json"));
            Directory.Delete(root2, true);
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== T-0694: Bankimport =====================

    internal const string Camt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
          <BkToCstmrStmt>
            <GrpHdr><MsgId>MSG-2026-11-0001</MsgId><CreDtTm>2026-11-13T18:00:00</CreDtTm></GrpHdr>
            <Stmt>
              <Id>STMT-2026-011</Id><ElctrncSeqNb>11</ElctrncSeqNb><CreDtTm>2026-11-13T18:00:00</CreDtTm>
              <Acct><Id><IBAN>DE89370400440532013000</IBAN></Id><Ccy>EUR</Ccy></Acct>
              <Bal><Tp><CdOrPrtry><Cd>OPBD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">1000.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-11-01</Dt></Dt></Bal>
              <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">2176.72</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-11-13</Dt></Dt></Bal>
              <Ntry>
                <Amt Ccy="EUR">2034.22</Amt><CdtDbtInd>CRDT</CdtDbtInd><Sts>BOOK</Sts><BookgDt><Dt>2026-11-10</Dt></BookgDt><ValDt><Dt>2026-11-10</Dt></ValDt><AcctSvcrRef>2026111000001</AcctSvcrRef>
                <NtryDtls><TxDtls><Refs><EndToEndId>NOTPROVIDED</EndToEndId></Refs>
                  <RltdPties><Dbtr><Nm>Beispiel GmbH</Nm></Dbtr><DbtrAcct><Id><IBAN>DE02120300000000202051</IBAN></Id></DbtrAcct></RltdPties>
                  <RmtInf><Ustrd>Rechnung RE-2026-001</Ustrd><Ustrd>vielen Dank</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">800.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts>BOOK</Sts><BookgDt><Dt>2026-11-03</Dt></BookgDt><ValDt><Dt>2026-11-03</Dt></ValDt>
                <NtryDtls><TxDtls><Refs><EndToEndId>MIETE-2026-11</EndToEndId></Refs>
                  <RltdPties><Cdtr><Nm>Hausverwaltung Muster</Nm></Cdtr><CdtrAcct><Id><IBAN>DE02120300000000202051</IBAN></Id></CdtrAcct></RltdPties>
                  <RmtInf><Ustrd>Miete November</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">42.50</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts>BOOK</Sts><BookgDt><Dt>2026-11-05</Dt></BookgDt><ValDt><Dt>2026-11-05</Dt></ValDt><AddtlNtryInf>Sammellastschrift</AddtlNtryInf>
                <NtryDtls>
                  <TxDtls><Amt Ccy="EUR">30.00</Amt><RltdPties><Cdtr><Nm>Stadtwerke Musterstadt</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Abschlag Strom</Ustrd></RmtInf></TxDtls>
                  <TxDtls><Amt Ccy="EUR">12.50</Amt><RltdPties><Cdtr><Nm>Papierladen Beispiel</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Druckerpapier</Ustrd></RmtInf></TxDtls>
                </NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">15.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><RvslInd>true</RvslInd><Sts>BOOK</Sts><BookgDt><Dt>2026-11-12</Dt></BookgDt><ValDt><Dt>2026-11-12</Dt></ValDt>
                <NtryDtls><TxDtls><RltdPties><Cdtr><Nm>Kunde Beispiel</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Gutschrift irrtuemlich</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">99.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts>PDNG</Sts><BookgDt><Dt>2026-11-14</Dt></BookgDt>
                <NtryDtls><TxDtls><RmtInf><Ustrd>Vormerkung Kartenzahlung</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
            </Stmt>
          </BkToCstmrStmt>
        </Document>
        """;

    internal const string Mt940 = ":20:STARTUMS\r\n:25:37040044/0532013000\r\n:28C:00011/001\r\n:60F:C261101EUR1000,00\r\n" +
        ":61:2611101110C2034,22NTRFNONREF//B26111000001\r\n:86:166?00GUTSCHRIFT?109310?20EREF+NOTPROVIDED?21SVWZ+Rechnung RE-2026-00?221 vielen Dank?30COBADEFFXXX?31DE02120300000000202051?32Beispiel GmbH\r\n" +
        ":61:2611031103D800,00NDDTNONREF\r\n:86:105?00LASTSCHRIFT?20EREF+MIETE-2026-11?21SVWZ+Miete November?32Hausverwaltung\r\n?33 Muster\r\n" +
        ":61:261105D42,5NMSCNONREF\r\n:86:Kartenzahlung Papierladen Beispiel\r\n" +
        ":61:2611121112RC15,00NSTONONREF\r\n:86:159?00STORNO?20SVWZ+Gutschrift irrtuemlich?32Kunde Beispiel\r\n" +
        ":62F:C261113EUR2176,72\r\n-\r\n";

    [Fact(DisplayName = "T-0694 Bank: CAMT.053 – Anzahl und Summe stimmen mit den Salden des Auszugs, Sammelbuchung wird aufgeteilt, Storno behält die Richtung aus CdtDbtInd, Vormerkungen bleiben draußen")]
    public void Camt053()
    {
        var a = BankImport.LesenText(Camt);
        Assert.Equal("CAMT.053", a.Format); Assert.Equal("DE89370400440532013000", a.Konto);
        Assert.Equal(5, a.Zeilen.Count); Assert.Equal(1176.72m, a.Summe); Assert.Equal((1000m, 2176.72m), (a.Anfang, a.Ende)); Assert.True(a.Stimmt);
        Assert.Contains("passt zu Anfangs- und Endsaldo", a.Kontrolle);
        Assert.Equal(new BankZeile(new(2026, 11, 10), 2034.22m, "Beispiel GmbH", "Rechnung RE-2026-001 vielen Dank", "DE02120300000000202051", "2026111000001"), a.Zeilen[0]);
        Assert.Equal((-800m, "Hausverwaltung Muster", "Miete November", "MIETE-2026-11"), (a.Zeilen[1].Betrag, a.Zeilen[1].Gegenseite, a.Zeilen[1].Zweck, a.Zeilen[1].Referenz));
        Assert.Equal([(-30m, "Stadtwerke Musterstadt – Abschlag Strom"), (-12.5m, "Papierladen Beispiel – Druckerpapier")], a.Zeilen.Skip(2).Take(2).Select(z => (z.Betrag, z.Text)));
        Assert.Equal((-15m, "Storno Gutschrift irrtuemlich"), (a.Zeilen[4].Betrag, a.Zeilen[4].Zweck));
        Assert.Contains(a.Hinweise, h => h.Contains("1 vorgemerkte"));
        // andere Version des Namensraums und Name unter Pty (camt.053.001.08) werden genauso gelesen
        var v8 = Camt.Replace("camt.053.001.02", "camt.053.001.08").Replace("<Dbtr><Nm>Beispiel GmbH</Nm></Dbtr>", "<Dbtr><Pty><Nm>Beispiel GmbH</Nm></Pty></Dbtr>").Replace("<Sts>BOOK</Sts>", "<Sts><Cd>BOOK</Cd></Sts>").Replace("<Sts>PDNG</Sts>", "<Sts><Cd>PDNG</Cd></Sts>");
        var a8 = BankImport.LesenText(v8); Assert.Equal(a.Zeilen, a8.Zeilen);
        // falsche Datei: klare Sätze
        Assert.Contains("CAMT.052", Assert.Throws<InvalidDataException>(() => BankImport.LesenText("<Document><BkToCstmrAcctRpt/></Document>")).Message);
        Assert.Contains("kein CAMT.053", Assert.Throws<InvalidDataException>(() => BankImport.LesenText("<html><body>Bitte anmelden</body></html>")).Message);
        Assert.Contains("kein gültiges XML", Assert.Throws<InvalidDataException>(() => BankImport.LesenText("<Document><BkToCstmrStmt><Stmt>")).Message);
        // gefälschte Salden fallen auf
        var bad = BankImport.LesenText(Camt.Replace("2176.72", "2200.00")); Assert.False(bad.Stimmt); Assert.Contains("passt NICHT", bad.Kontrolle);
    }

    [Fact(DisplayName = "T-0694 Bank: MT940 – strukturiertes :86: (?20–?33, SVWZ+/EREF+), Folgezeilen, Betrag ohne Nachkommastellen-Ziffern, Storno RC, Buchungstag über den Jahreswechsel; Summe passt zu :60F:/:62F:")]
    public void Mt940_lesen()
    {
        var a = BankImport.LesenText(Mt940);
        Assert.Equal("MT940", a.Format); Assert.Equal("37040044/0532013000", a.Konto);
        Assert.Equal(4, a.Zeilen.Count); Assert.Equal(1176.72m, a.Summe); Assert.True(a.Stimmt);
        Assert.Equal(new BankZeile(new(2026, 11, 10), 2034.22m, "Beispiel GmbH", "Rechnung RE-2026-001 vielen Dank", "DE02120300000000202051", ""), a.Zeilen[0]);
        Assert.Equal((new DateOnly(2026, 11, 3), -800m, "Hausverwaltung Muster", "Miete November", "MIETE-2026-11"), (a.Zeilen[1].Datum, a.Zeilen[1].Betrag, a.Zeilen[1].Gegenseite, a.Zeilen[1].Zweck, a.Zeilen[1].Referenz));
        Assert.Equal((-42.5m, "", "Kartenzahlung Papierladen Beispiel"), (a.Zeilen[2].Betrag, a.Zeilen[2].Gegenseite, a.Zeilen[2].Zweck));
        Assert.Equal((-15m, "Kunde Beispiel"), (a.Zeilen[3].Betrag, a.Zeilen[3].Gegenseite)); // RC = Storno einer Gutschrift → Belastung
        // Jahreswechsel: Valuta 30.12.2026, gebucht 02.01. → 2027; und umgekehrt
        var jw = BankImport.LesenText(":20:X\r\n:25:1/2\r\n:60F:C261230EUR100,00\r\n:61:2612300102D12,50NMSCNONREF\r\n:86:Entgelt\r\n:61:2701021231C5,00NMSCNONREF\r\n:86:Zinsen\r\n:62F:C270102EUR92,50\r\n-\r\n");
        Assert.Equal([new DateOnly(2027, 1, 2), new DateOnly(2026, 12, 31)], jw.Zeilen.Select(z => z.Datum)); Assert.True(jw.Stimmt);
        // Windows-1252-Datei mit Umlauten wird richtig gelesen
        var root = Tmp();
        try
        {
            var f = Path.Combine(root, "auszug.sta");
            File.WriteAllBytes(f, Encoding.Latin1.GetBytes(Mt940.Replace("Hausverwaltung", "Bäckerei Müßig")));
            Assert.Equal("Bäckerei Müßig Muster", BankImport.Lesen(f).Zeilen[1].Gegenseite);
        }
        finally { Directory.Delete(root, true); }
        Assert.Contains("nicht lesbar", Assert.Throws<InvalidDataException>(() => BankImport.LesenText(":20:X\r\n:61:kaputt\r\n")).Message);
    }

    internal static readonly (string Bank, string Csv)[] CsvLayouts =
    [
        ("Sparkasse (CSV-CAMT)", "\"Auftragskonto\";\"Buchungstag\";\"Valutadatum\";\"Buchungstext\";\"Verwendungszweck\";\"Glaeubiger ID\";\"Mandatsreferenz\";\"Kundenreferenz (End-to-End)\";\"Sammlerreferenz\";\"Lastschrift Ursprungsbetrag\";\"Auslagenersatz Ruecklastschrift\";\"Beguenstigter/Zahlungspflichtiger\";\"Kontonummer/IBAN\";\"BIC (SWIFT-Code)\";\"Betrag\";\"Waehrung\";\"Info\"\n" +
            "\"DE89370400440532013000\";\"10.11.26\";\"10.11.26\";\"GUTSCHR. UEBERWEISUNG\";\"Rechnung RE-2026-001 vielen Dank\";\"\";\"\";\"\";\"\";\"\";\"\";\"Beispiel GmbH\";\"DE02120300000000202051\";\"BYLADEM1001\";\"2034,22\";\"EUR\";\"Umsatz gebucht\"\n" +
            "\"DE89370400440532013000\";\"03.11.26\";\"03.11.26\";\"FOLGELASTSCHRIFT\";\"Miete November\";\"DE98ZZZ09999999999\";\"M-17\";\"\";\"\";\"\";\"\";\"Hausverwaltung Muster\";\"DE02120300000000202051\";\"BYLADEM1001\";\"-800,00\";\"EUR\";\"Umsatz gebucht\"\n"),
        ("DKB (neues Format)", "\"Girokonto\";\"DE89370400440532013000\"\n\"\"\n\"Kontostand vom 13.11.2026:\";\"2.234,22 €\"\n\"\"\n\"Buchungsdatum\";\"Wertstellung\";\"Status\";\"Zahlungspflichtige*r\";\"Zahlungsempfänger*in\";\"Verwendungszweck\";\"Umsatztyp\";\"IBAN\";\"Betrag (€)\";\"Gläubiger-ID\";\"Mandatsreferenz\";\"Kundenreferenz\"\n" +
            "\"10.11.26\";\"10.11.26\";\"Gebucht\";\"Beispiel GmbH\";\"Erika Muster\";\"Rechnung RE-2026-001 vielen Dank\";\"Eingang\";\"DE02120300000000202051\";\"2.034,22\";\"\";\"\";\"\"\n" +
            "\"03.11.26\";\"03.11.26\";\"Gebucht\";\"Erika Muster\";\"Hausverwaltung Muster\";\"Miete November\";\"Ausgang\";\"DE02120300000000202051\";\"-800,00\";\"\";\"\";\"\"\n"),
        ("ING", "Umsatzanzeige;Datei erstellt am: 13.11.2026 18:00\n;Letztes Update: aktuell\n\nIBAN;DE89 3704 0044 0532 0130 00\nKontoname;Girokonto\nBank;Beispielbank\nKunde;Erika Muster\nZeitraum;01.11.2026 - 13.11.2026\nSaldo;2.234,22;EUR\n\nBuchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\n" +
            "10.11.2026;10.11.2026;Beispiel GmbH;Gutschrift;Rechnung RE-2026-001 vielen Dank;2.234,22;EUR;2.034,22;EUR\n03.11.2026;03.11.2026;Hausverwaltung Muster;Lastschrift;Miete November;200,00;EUR;-800,00;EUR\n"),
        ("Volksbank/Raiffeisenbank", "Bezeichnung Auftragskonto;IBAN Auftragskonto;BIC Auftragskonto;Bankname Auftragskonto;Buchungstag;Valutadatum;Name Zahlungsbeteiligter;IBAN Zahlungsbeteiligter;BIC (SWIFT-Code) Zahlungsbeteiligter;Buchungstext;Verwendungszweck;Betrag;Waehrung;Saldo nach Buchung;Bemerkung;Kategorie;Steuerrelevant;Glaeubiger ID;Mandatsreferenz\n" +
            "Girokonto;DE89370400440532013000;COBADEFFXXX;Beispielbank;10.11.2026;10.11.2026;Beispiel GmbH;DE02120300000000202051;BYLADEM1001;Gutschrift;Rechnung RE-2026-001 vielen Dank;2034,22;EUR;2234,22;;;;;\n" +
            "Girokonto;DE89370400440532013000;COBADEFFXXX;Beispielbank;03.11.2026;03.11.2026;Hausverwaltung Muster;DE02120300000000202051;BYLADEM1001;Basislastschrift;Miete November;-800,00;EUR;200,00;;;;;\n"),
        ("N26 (englisch, Komma, ISO-Datum)", "\"Date\",\"Payee\",\"Account number\",\"Transaction type\",\"Payment reference\",\"Amount (EUR)\",\"Amount (Foreign Currency)\",\"Type Foreign Currency\",\"Exchange Rate\"\n" +
            "\"2026-11-10\",\"Beispiel GmbH\",\"DE02120300000000202051\",\"Income\",\"Rechnung RE-2026-001 vielen Dank\",\"2034.22\",\"\",\"\",\"\"\n\"2026-11-03\",\"Hausverwaltung Muster\",\"DE02120300000000202051\",\"Direct Debit\",\"Miete November\",\"-800.0\",\"\",\"\",\"\"\n"),
        ("getrennte Spalten Soll/Haben", "Datum;Buchungstext;Empfänger;Soll;Haben\n10.11.2026;Rechnung RE-2026-001 vielen Dank;Beispiel GmbH;;2.034,22\n03.11.2026;Miete November;Hausverwaltung Muster;800,00;\nSumme;;;800,00;2.034,22\n"),
    ];

    [Fact(DisplayName = "T-0694 Bank: sechs gängige CSV-Kopfzeilen (Sparkasse, DKB, ING, Volksbank, N26, Soll/Haben) liefern dieselben zwei Buchungen – Datum, Betrag, Gegenseite, Zweck")]
    public void Csv_layouts()
    {
        foreach (var (bank, csv) in CsvLayouts)
        {
            var a = BankImport.LesenText(csv);
            Assert.True(a.Format == "CSV" && a.Zeilen.Count == 2, $"{bank}: {a.Zeilen.Count} Zeilen");
            Assert.True(a.Zeilen[0].Datum == new DateOnly(2026, 11, 10) && a.Zeilen[0].Betrag == 2034.22m && a.Zeilen[0].Gegenseite == "Beispiel GmbH" && a.Zeilen[0].Zweck == "Rechnung RE-2026-001 vielen Dank", $"{bank}: {a.Zeilen[0]}");
            Assert.True(a.Zeilen[1].Datum == new DateOnly(2026, 11, 3) && a.Zeilen[1].Betrag == -800m && a.Zeilen[1].Gegenseite == "Hausverwaltung Muster" && a.Zeilen[1].Zweck == "Miete November", $"{bank}: {a.Zeilen[1]}");
            Assert.Equal("Beispiel GmbH – Rechnung RE-2026-001 vielen Dank", a.Zeilen[0].Text);
            Assert.Null(a.Stimmt); Assert.Contains("keine Salden", a.Kontrolle);
        }
        Assert.Equal("DE02120300000000202051", BankImport.LesenText(CsvLayouts[3].Csv).Zeilen[0].Iban); // Volksbank: IBAN der Gegenseite, nicht die eigene
        Assert.Contains(BankImport.LesenText(CsvLayouts[5].Csv).Hinweise, h => h.Contains("1 Zeilen")); // Summenzeile übersprungen und gemeldet
    }

    [Fact(DisplayName = "T-0694 Bank: unbekannte CSV-Spalten – das Programm fragt nach, die Zuordnung wird gelernt und beim nächsten Auszug derselben Bank von selbst angewandt")]
    public void Csv_spalten_lernen()
    {
        var root = Tmp();
        try
        {
            const string csv = "Wann;Wieviel;Wer;Wofuer\n10.11.2026;2.034,22;Beispiel GmbH;Rechnung RE-2026-001\n03.11.2026;-800,00;Hausverwaltung Muster;Miete November\n";
            var mem = new SpaltenGedaechtnis(root);
            var ex = Assert.Throws<SpaltenUnklarException>(() => BankImport.LesenText(csv, mem));
            Assert.Equal(["Wann", "Wieviel", "Wer", "Wofuer"], ex.Kopf); Assert.Equal("2.034,22", ex.Beispiel[1]); Assert.Contains("merke es mir", ex.Message);
            var map = new SpaltenZuordnung(Datum: 0, Betrag: 1, Zweck: 3, Gegenseite: 2);
            var a = BankImport.LesenText(csv, mem, map); Assert.Equal(2, a.Zeilen.Count); Assert.Equal("Beispiel GmbH – Rechnung RE-2026-001", a.Zeilen[0].Text);
            mem.Merken(ex.Kopf, map); Assert.Equal(1, mem.Anzahl);
            var naechster = "Wann;Wieviel;Wer;Wofuer\n01.12.2026;-12,50;Papierladen Beispiel;Druckerpapier\n";
            var b = BankImport.LesenText(naechster, new SpaltenGedaechtnis(root)); // neues Objekt = nach Neustart
            Assert.Equal((new DateOnly(2026, 12, 1), -12.5m, "Papierladen Beispiel", "Druckerpapier"), (b.Zeilen[0].Datum, b.Zeilen[0].Betrag, b.Zeilen[0].Gegenseite, b.Zeilen[0].Zweck));
            Assert.Contains("kein Kontoauszug", Assert.Throws<InvalidDataException>(() => BankImport.LesenText("Hallo Welt\nnur Text\n")).Message);
            var leer = Path.Combine(root, "leer.csv"); File.WriteAllText(leer, ""); Assert.Contains("leer", Assert.Throws<InvalidDataException>(() => BankImport.Lesen(leer)).Message);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0694 Bank: Kategorie-Vorschlag (Gelerntes zuerst), Dubletten auch zwischen CSV, CAMT und MT940 derselben Buchung, offene Rechnung wird als bezahlt vorgeschlagen (Nummer + Betrag), Teilzahlung und „nur Betrag“ gekennzeichnet")]
    public void Dubletten_und_Zahlungsvorschlag()
    {
        var root = Tmp();
        try
        {
            var memory = new CategoryMemory(root); memory.Learn("Papierladen Beispiel", "Büro");
            var camt = BankImport.Buchungen(BankImport.LesenText(Camt).Zeilen, memory);
            Assert.Equal(["Einnahmen", "Wohnen", "Wohnen", "Büro", "Sonstiges"], camt.Select(e => e.Category)); // Miete, Stadtwerke über Stichwort; Papierladen gelernt
            var first = BankImport.Vorschau([], camt); Assert.All(first, x => Assert.False(x.Duplicate));
            // derselbe Auszug noch einmal als MT940 (andere Schreibweise, eine Sammelbuchung statt zwei Zeilen) und als CSV
            var mt = BankImport.Buchungen(BankImport.LesenText(Mt940).Zeilen, memory);
            var second = BankImport.Vorschau(camt, mt);
            Assert.Equal([true, true, false, true], second.Select(x => x.Duplicate)); // nur die anders geschnittene Sammelbuchung (42,50) ist neu
            var csv = BankImport.Buchungen(BankImport.LesenText(CsvLayouts[0].Csv).Zeilen, memory);
            Assert.All(BankImport.Vorschau(camt, csv), x => Assert.True(x.Duplicate));
            Assert.Equal([false, true], BankImport.Vorschau([], [camt[1], camt[1]]).Select(x => x.Duplicate)); // doppelte Zeile innerhalb der Datei
            // offene Rechnungen
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(new DateOnly(2026, 11, 1)); var re1 = b.Vorgaenge[0]; Rechnungen.Festschreiben(b, re1, kreis);
            var re2 = Rechnungen.Neu(b, "rechnung", new DateOnly(2026, 11, 2)); Rechnungen.KundeSetzen(re2, b.Kunden[1]); re2.Positionen = [new() { Text = "Beratung", Menge = 4, Einheit = "Std.", Preis = 85 }]; Rechnungen.Festschreiben(b, re2, kreis);
            b.Vorgaenge.Add(re2);
            var v1 = BankImport.Zahlungsvorschlaege(b, camt);
            var s = Assert.Single(v1); Assert.Equal((0, "RE-2026-001", 2034.22m, "sicher"), (s.Zeile, s.Nummer, s.Betrag, s.Sicherheit)); Assert.Contains("steht im Verwendungszweck", s.Grund);
            List<Entry> andere = [new(Today, "Max Mustermann – re 2026 002 Anzahlung", 100m, "Einnahmen"), new(Today, "Max Mustermann – danke", 304.60m, "Einnahmen"), new(Today, "Irgendwer", 77m, "Einnahmen"), new(Today, "Lastschrift", -404.60m)];
            var v2 = BankImport.Zahlungsvorschlaege(b, andere);
            Assert.Equal([(0, "RE-2026-002", 100m, "teilzahlung"), (1, "RE-2026-002", 304.60m, "unsicher")], v2.Select(x => (x.Zeile, x.Nummer, x.Betrag, x.Sicherheit)));
            // zuordnen: Rechnung bezahlt, Buchungen je Steuersatz
            var book = Rechnungen.ZahlungZuordnen(re1, s.Betrag, camt[0].Date, "Kontoauszug CAMT.053");
            Assert.Equal("bezahlt", Rechnungen.Status(re1, Today).Id); Assert.Equal(2034.22m, book.Sum(e => e.Amount)); Assert.Empty(BankImport.Zahlungsvorschlaege(b, camt));
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== T-0694: Kredit-Rechner =====================

    [Fact(DisplayName = "T-0694 Kredit: 10.000 € zu 5 % über 60 Monate – Rate 188,71 €, Restschuld im letzten Monat 0,00 €; Gegenrechnung: Zins je Monat = Restschuld · 5 % / 12, Barwert aller Raten = Kreditbetrag")]
    public void Kredit_annuitaet()
    {
        var p = Kredit.Plan(10000m, 5m, monate: 60, start: new DateOnly(2026, 11, 15));
        Assert.Equal(188.71m, p.Monatsrate); Assert.Equal(60, p.Monate); Assert.Equal(0m, p.Restschuld); Assert.Equal(0m, p.Raten[^1].Rest);
        Assert.Equal(new DateOnly(2026, 12, 15), p.Raten[0].Datum); Assert.Equal(new DateOnly(2031, 11, 15), p.Raten[^1].Datum);
        // Gegenrechnung 1: jede Zeile einzeln nachgerechnet, unabhängig von der Schleife im Kern
        var rest = 10000m;
        foreach (var r in p.Raten)
        {
            var zins = Math.Round(rest * 5m / 1200m, 2, MidpointRounding.AwayFromZero);
            Assert.Equal(zins, r.Zins); Assert.Equal(r.Rate, r.Zins + r.Tilgung); rest -= r.Tilgung; Assert.Equal(rest, r.Rest);
        }
        Assert.Equal(41.67m, p.Raten[0].Zins); Assert.Equal(147.04m, p.Raten[0].Tilgung);
        Assert.Equal(10000m, p.Raten.Sum(r => r.Tilgung));
        // Gegenrechnung 2: geschlossene Formel für die Zinssumme n·R − K (± Rundung der letzten Rate)
        Assert.InRange(p.ZinsenGesamt, 60 * 188.71m - 10000m - 0.60m, 60 * 188.71m - 10000m + 0.60m);
        Assert.InRange(p.ZinsenGesamt, 1322.00m, 1323.50m); // laut Formel n·R − K: 1.322,60 € bei gerundeter Rate, exakt 1.322,74 €
        // Gegenrechnung 3: Barwert aller Zahlungen zum Monatszins 5 %/12 ergibt den Kreditbetrag (auf wenige Cent genau)
        double bw = 0, q = 1; foreach (var r in p.Raten) { q *= 1 + 0.05 / 12; bw += (double)r.Rate / q; }
        Assert.InRange(bw, 9999.90, 10000.10);
        // Effektivzins ohne Gebühren = (1 + 0,05/12)^12 − 1 = 5,12 %
        Assert.Equal(5.12m, p.EffektivPa);
        Assert.Equal(Math.Round((decimal)((Math.Pow(1 + 0.05 / 12, 12) - 1) * 100), 2), p.EffektivPa);
        // mit 2 % Bearbeitungsgebühr steigt der Effektivzins spürbar; Gegenrechnung über den Barwert zum gefundenen Zins
        var g = Kredit.Plan(10000m, 5m, monate: 60, gebuehr: 200m);
        Assert.InRange(g.EffektivPa, 5.90m, 6.10m);
        var im = Math.Pow(1 + (double)g.EffektivPa / 100, 1.0 / 12) - 1; bw = 0; q = 1; foreach (var r in g.Raten) { q *= 1 + im; bw += (double)r.Rate / q; }
        Assert.InRange(bw, 9800 - 3, 9800 + 3);
        // zinslos: gleichmäßige Raten
        var z = Kredit.Plan(1200m, 0m, monate: 12); Assert.Equal(100m, z.Monatsrate); Assert.Equal(0m, z.ZinsenGesamt); Assert.Equal(0m, z.EffektivPa);
    }

    [Fact(DisplayName = "T-0694 Kredit: Sondertilgung verkürzt die Laufzeit und spart Zinsen, Rate aus anfänglicher Tilgung, Restschuld am Ende der Zinsbindung, Rate unter den Zinsen wird abgewiesen; CSV und PDF")]
    public void Kredit_sonder_und_zinsbindung()
    {
        var ohne = Kredit.Plan(10000m, 5m, monate: 60);
        var mit = Kredit.Plan(10000m, 5m, rate: ohne.Monatsrate, sonderJaehrlich: 1000m);
        Assert.True(mit.Monate < ohne.Monate); Assert.True(mit.ZinsenGesamt < ohne.ZinsenGesamt); Assert.Equal(0m, mit.Restschuld);
        Assert.Equal(1000m, mit.Raten[11].Sonder); Assert.Equal(mit.Raten[10].Rest - mit.Raten[11].Tilgung - 1000m, mit.Raten[11].Rest);
        Assert.Equal(10000m, mit.Raten.Sum(r => r.Tilgung + r.Sonder)); // getilgt wird genau der Kreditbetrag, nie mehr
        var einzel = Kredit.Plan(10000m, 5m, rate: ohne.Monatsrate, sonder: new Dictionary<int, decimal> { [3] = 500m, [200] = 9999m });
        Assert.Equal(500m, einzel.SonderGesamt); Assert.True(einzel.Monate < 60);
        // Baufinanzierung: 200.000 €, 3,5 % Zins, 2 % anfängliche Tilgung, 10 Jahre Zinsbindung
        var rate = Kredit.RateAusTilgung(200000m, 3.5m, 2m); Assert.Equal(916.67m, rate);
        var bau = Kredit.Plan(200000m, 3.5m, rate: rate, zinsbindungMonate: 120);
        Assert.Equal(120, bau.Monate); Assert.InRange(bau.Restschuld, 152000m, 153000m);
        // Gegenrechnung Restschuld nach k Raten: K·q^k − R·(q^k − 1)/(q − 1)
        var qq = 1 + 0.035 / 12; var erwartet = 200000 * Math.Pow(qq, 120) - 916.67 * (Math.Pow(qq, 120) - 1) / (qq - 1);
        Assert.InRange((double)bau.Restschuld, erwartet - 2, erwartet + 2);
        Assert.Equal(3.56m, bau.EffektivPa); // (1 + 0,035/12)^12 − 1
        Assert.InRange(bau.Jahre.Count, 10, 11); Assert.Equal(200000m - bau.Restschuld, bau.Jahre.Sum(j => j.Tilgung)); Assert.Equal(bau.Restschuld, bau.Jahre[^1].Schuld);
        Assert.Contains("deckt nicht einmal die Zinsen", Assert.Throws<ArgumentException>(() => Kredit.Plan(10000m, 12m, rate: 50m)).Message);
        Assert.Throws<ArgumentException>(() => Kredit.Plan(0m, 5m, monate: 12)); Assert.Throws<ArgumentException>(() => Kredit.Plan(1000m, 5m)); Assert.Throws<ArgumentException>(() => Kredit.Plan(1000m, 5m, monate: 12, rate: 90m));
        var root = Tmp();
        try
        {
            var csv = Kredit.Csv(ohne); var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(62, lines.Length); Assert.StartsWith("1;", lines[1]); Assert.Contains(";188,71;41,67;147,04;0,00;9852,96", lines[1]); Assert.EndsWith(";0,00", lines[^1]);
            var pdf = Path.Combine(root, "plan.pdf"); Assert.True(Kredit.Pdf(ohne, pdf) >= 2); Assert.True(PdfTools.Read(pdf).Pages >= 2);
            if (PdfToText() is { } tool) { var t = ReadPdfText(tool, pdf); Assert.Contains("188,71 €", t); Assert.Contains("Tilgungsplan", t); Assert.Contains("9.852,96 €", t); }
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== T-0694: Auswertung und DATEV =====================

    static List<Entry> Buch() =>
    [
        new(new(2026, 3, 1), "Rechnung RE-2026-001", 1190m, "Einnahmen", 19m), new(new(2026, 3, 9), "Fachbuch verkauft", 107m, "Einnahmen", 7m), new(new(2026, 3, 12), "Papier", -11.90m, "Büro", 19m),
        new(new(2026, 4, 1), "Kontoführung", -5m, "Gebühren"), new(new(2026, 4, 3), "Rewe Markt", -50m, "Lebensmittel"), new(new(2025, 12, 1), "alt", 50m, "Einnahmen"),
    ];

    [Fact(DisplayName = "T-0694 Auswertung: Einnahmen-Überschuss je Jahr/Monat/Kategorie netto mit Steuer; Export als CSV, XLSX (wieder eingelesen, Summen als Formeln) und PDF mit Diagramm")]
    public void Auswertung_export()
    {
        var a = GeldAuswertung.Rechnen(Buch(), 2026);
        Assert.Equal((1100m, 65m, 1035m), (a.EinnahmenNetto, a.AusgabenNetto, a.Ueberschuss));
        Assert.Equal((197m, 1.90m, 195.10m), (a.Umsatzsteuer, a.Vorsteuer, a.Zahllast));
        Assert.Equal(new KategorieSumme("Einnahmen", 1100m, 197m, 1297m, 2), Assert.Single(a.Einnahmen));
        Assert.Equal(["Lebensmittel", "Büro", "Gebühren"], a.Ausgaben.Select(k => k.Kategorie));
        Assert.Equal((1297m, 11.90m, 1285.10m), (a.Monate[2].Einnahmen, a.Monate[2].Ausgaben, a.Monate[2].Ergebnis));
        var m = GeldAuswertung.Rechnen(Buch(), 2026, 4); Assert.Equal("April 2026", m.Titel); Assert.Equal(-55m, m.Ueberschuss); Assert.Equal(2, m.Buchungen.Count);
        var csv = GeldAuswertung.Csv(a);
        Assert.Contains("Einnahme;\"Einnahmen\";1100,00;197,00;1297,00;2", csv); Assert.Contains("Überschuss;1035,00", csv); Assert.Contains("März;1297,00;11,90;1285,10", csv); Assert.Contains("keine Steuerberatung", csv);
        var root = Tmp();
        try
        {
            var x = Path.Combine(root, "auswertung.xlsx"); GeldAuswertung.Xlsx(a, x);
            var back = SheetFiles.Open(x);
            Assert.Equal(["Übersicht", "Monate", "Buchungen"], back.Sheets.Select(s => s.Name));
            var ueber = back["Übersicht"]; Assert.Equal("Auswertung 2026", ueber.Raw(0, 0)); Assert.Equal("Einnahmen", ueber.Raw(3, 1));
            Assert.Equal(1100m, Finance.ParseAmount(ueber.Raw(3, 2))); Assert.Equal(-50m, Finance.ParseAmount(ueber.Raw(4, 4)));
            Assert.StartsWith("=SUMME(C4:C7)", ueber.Raw(8, 2)); // Überschuss als Formel – in Excel/Calc nachrechenbar
            Assert.Equal(1035m, (decimal)new SheetCalc(back).Value(ueber, 8, 2).Number);
            Assert.Equal(5, back["Buchungen"].MaxRow); Assert.Equal("Rechnung RE-2026-001", back["Buchungen"].Raw(1, 1));
            Assert.StartsWith("=SUMME(B2:B13)", back["Monate"].Raw(13, 1)); Assert.Equal(1297m, (decimal)new SheetCalc(back).Value(back["Monate"], 13, 1).Number);
            var pdf = Path.Combine(root, "auswertung.pdf"); Assert.True(GeldAuswertung.Pdf(a, pdf, "Erika Muster Webdesign") >= 1);
            if (PdfToText() is { } tool) { var t = ReadPdfText(tool, pdf); Assert.Contains("Auswertung 2026", t); Assert.Contains("1.035,00 €", t); Assert.Contains("Lebensmittel", t); Assert.Contains("Erika Muster Webdesign", t); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0694 DATEV: Buchungsstapel-CSV – Vorlaufsatz EXTF/700/21 mit 31 Feldern, Spaltenzeile, je Buchung Umsatz ohne Vorzeichen, S/H, Konto/Gegenkonto, BU-Schlüssel nur bei Nicht-Automatikkonten, Belegdatum TTMM; Windows-1252; ehrlich gekennzeichnet")]
    public void Datev_stapel()
    {
        var k = new DatevKonten { Berater = "1001", Mandant = "456" };
        var text = Datev.Stapel(Buch(), 2026, k, new DateTime(2026, 11, 16, 9, 30, 0));
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(7, lines.Length); // Vorlauf, Spalten, 5 Buchungen aus 2026
        var head = lines[0].Split(';'); Assert.Equal(31, head.Length);
        Assert.Equal(["\"EXTF\"", "700", "21", "\"Buchungsstapel\"", "13", "20261116093000000"], head.Take(6));
        Assert.Equal(["1001", "456", "20260101", "4", "20260101", "20261231"], head.Skip(10).Take(6)); Assert.Equal("\"EUR\"", head[21]);
        Assert.Equal("Umsatz (ohne Soll/Haben-Kz);Soll/Haben-Kennzeichen;WKZ Umsatz;Kurs;Basis-Umsatz;WKZ Basis-Umsatz;Konto;Gegenkonto (ohne BU-Schlüssel);BU-Schlüssel;Belegdatum;Belegfeld 1;Belegfeld 2;Skonto;Buchungstext", lines[1]);
        Assert.Equal("1190,00;\"S\";\"EUR\";;;;1200;8400;\"\";0103;\"RE-2026-001\";\"\";;\"Rechnung RE-2026-001\"", lines[2]); // Automatikkonto 8400: kein BU-Schlüssel
        Assert.Equal("107,00;\"S\";\"EUR\";;;;1200;8300;\"\";0903;\"\";\"\";;\"Fachbuch verkauft\"", lines[3]);
        Assert.Equal("11,90;\"H\";\"EUR\";;;;1200;4930;\"9\";1203;\"\";\"\";;\"Papier\"", lines[4]); // Vorsteuer 19 % = BU 9
        Assert.Equal("5,00;\"H\";\"EUR\";;;;1200;4970;\"\";0104;\"\";\"\";;\"Kontoführung\"", lines[5]);
        Assert.Contains(";1200;1800;", lines[6]); // Haushalts-Kategorie → Privatkonto
        Assert.All(lines.Skip(2), l => Assert.Equal(14, System.Text.RegularExpressions.Regex.Split(l, ";(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)").Length));
        Assert.Contains("ungeprüft gegen DATEV-Software", Datev.Kennzeichnung);
        var root = Tmp();
        try
        {
            // Kontenzuordnung ist einstellbar und bleibt gespeichert
            k.Kategorie["Büro"] = "6815"; k.Bank = "1800"; k.Speichern(root);
            var k2 = DatevKonten.Laden(root); Assert.Equal("6815", k2.Kategorie["Büro"]);
            var f = Datev.Schreiben(Buch(), 2026, k2, Path.Combine(root, "out"));
            Assert.Equal("EXTF_Buchungsstapel_2026.csv", Path.GetFileName(f));
            var bytes = File.ReadAllBytes(f);
            Assert.Contains((byte)0xFC, bytes); // „ü“ in „Kontoführung“ als ein Byte (Windows-1252), nicht UTF-8
            Assert.DoesNotContain((byte)0xC3, bytes);
            Assert.Contains(";1800;6815;\"9\";1203;", Encoding.Latin1.GetString(bytes));
        }
        finally { Directory.Delete(root, true); }
    }
}
