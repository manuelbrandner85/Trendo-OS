using Trendo.Core.Business;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0675 Business &amp; Geld Teil A: Rechnungen &amp; Angebote, Mahnungen, Belege &amp; Spesen, Preise kalkulieren – Kerne ohne Oberfläche.</summary>
public class BusinessGeldTests
{
    static readonly DateOnly Today = new(2026, 11, 15);
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-biz-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static (StudioWorkflow Wf, StudioExecutionContext Ctx, TrendoCore Core, string Project) Workflow(string root)
    {
        var all = PromptActions.CreateDefault();
        var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
        var project = core.Projects.Create("probe", "Probe").Id;
        return (new StudioWorkflow(core, all), new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default), core, project);
    }

    static string Run(string root, string studio, string prompt, string file)
    {
        var (wf, ctx, core, project) = Workflow(Path.Combine(root, Guid.NewGuid().ToString("N")[..6]));
        var req = new StudioPromptRequest("biz-1", project, studio, prompt, [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
        var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
        Assert.True(done.Status == RunStatus.Verified, string.Join(" | ", done.StepEvidence.Select(s => s.ToString())));
        return File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith(file)).RelativePath));
    }

    // ===================== Rechnungen & Angebote =====================

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

    [Fact(DisplayName = "INV-02: Pflichtangaben nach § 14 Abs. 4 UStG – Steuernummer, Anschrift, Leistungsdatum, 0 % braucht Befreiungsgrund, Kleinbetragsrechnung bis 250 €")]
    public void Rechnung_pflichtangaben()
    {
        var b = Rechnungen.Beispiel(Today); var v = b.Vorgaenge[0];
        Assert.True(Rechnungen.Vollstaendig(b, v));
        b.Firma.SteuerNr = ""; v.Leistung = "";
        var miss = Rechnungen.Pruefen(b, v).Where(p => !p.Ok).Select(p => p.Id).ToList();
        Assert.Equal(["steuernr", "leistung"], miss);
        b.Firma.SteuerNr = "12/345/67890"; v.Leistung = "November 2026";
        v.Positionen[2].Ust = 0; Assert.Contains(Rechnungen.Pruefen(b, v), p => p.Id == "befreiung" && !p.Ok);
        v.Befreiung = "Steuerfreie Leistung nach § 4 Nr. 21 UStG"; Assert.True(Rechnungen.Vollstaendig(b, v));
        v.Positionen[2].Ust = 16; Assert.Contains(Rechnungen.Pruefen(b, v), p => p.Id == "saetze" && !p.Ok);
        // Kleinbetrag: ohne Steuernummer und Kundenanschrift zulässig
        var k = Rechnungen.Neu(b, "rechnung", Today); k.Positionen = [new() { Text = "Kleinteil", Menge = 1, Preis = 100 }]; b.Firma.SteuerNr = "";
        Assert.True(Rechnungen.Vollstaendig(b, k)); Assert.Contains(Rechnungen.Pruefen(b, k), p => p.Id == "klein");
        k.Positionen[0].Preis = 300; Assert.False(Rechnungen.Vollstaendig(b, k));
    }

    [Fact(DisplayName = "INV-03: Nummernkreis lückenlos je Art und Jahr, gemeinsam mit „Geld & Rechnungen“; Archiv; Lückenprüfung")]
    public void Rechnung_nummern()
    {
        var root = Tmp();
        try
        {
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(Today);
            Assert.Equal("RE-2026-001", kreis.Vorschau("rechnung", Today));
            // eine Rechnung aus SPEC-11 (HTML) belegt schon die 5
            var led = new Ledger(root); Directory.CreateDirectory(led.InvoiceDir); File.WriteAllText(Path.Combine(led.InvoiceDir, "RE-2026-005.html"), "<html></html>");
            Assert.Equal("RE-2026-006", Rechnungen.Festschreiben(b, b.Vorgaenge[0], kreis));
            Assert.True(File.Exists(Path.Combine(kreis.Archiv, "RE-2026-006.json")));
            Assert.Contains("RE-2026-006", led.InvoiceNumbers()); // SPEC-11 sieht die Nummer – nie doppelt
            Assert.Equal("RE-2026-007", Finance.NextInvoiceNumber(led.InvoiceNumbers(), Today));
            Assert.Throws<InvalidOperationException>(() => Rechnungen.Festschreiben(b, b.Vorgaenge[0], kreis));
            var a = Rechnungen.Neu(b, "angebot", Today); Rechnungen.KundeSetzen(a, b.Kunden[1]); a.Positionen = [new() { Text = "Beratung", Menge = 2, Preis = 85, Einheit = "Std." }];
            Assert.Equal("AN-2026-001", Rechnungen.Festschreiben(b, a, kreis));
            Assert.Equal("RE-2027-001", kreis.Vorschau("rechnung", new DateOnly(2027, 1, 2)));
            Assert.Single(kreis.Abgelegt().Where(x => x.Art == "rechnung"));
            Assert.Equal(["RE-2026-002"], Rechnungen.Luecken(["RE-2026-001", "RE-2026-003", "AN-2026-001"], "RE"));
            Assert.Throws<InvalidOperationException>(() => Rechnungen.KundeSetzen(a, b.Kunden[0]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "INV-04: Angebot → Auftragsbestätigung → Rechnung, Storno mit eigener Nummer, Status offen/überfällig/bezahlt, Buchungen je Satz")]
    public void Rechnung_kette()
    {
        var root = Tmp();
        try
        {
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(Today);
            var an = Rechnungen.Neu(b, "angebot", new DateOnly(2026, 9, 20)); Rechnungen.KundeSetzen(an, b.Kunden[0]); an.Positionen = [.. b.Vorgaenge[0].Positionen.Select(Trendo.Core.Business.BizJson.Clone)];
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
            Rechnungen.Zahlung(re, 1000m, Today);
            Assert.Equal("ueberfaellig", Rechnungen.Status(re, Today).Id); Assert.Equal(1034.22m, Rechnungen.Offen(re));
            Rechnungen.Zahlung(re, 1034.22m, Today);
            Assert.Equal("Bezahlt am 15.11.2026", Rechnungen.Status(re, Today).Text);
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
            // Kleinunternehmer: Kategorie E mit § 19-Hinweis
            b.Firma.Kleinunternehmer = true; var k = Rechnungen.Neu(b, "rechnung", Today); Rechnungen.KundeSetzen(k, b.Kunden[0]); k.Positionen = [new() { Text = "Beratung", Menge = 2, Einheit = "Std.", Preis = 85 }];
            Rechnungen.Festschreiben(b, k, kreis);
            var kx = ERechnung.Utf8(Rechnungen.XRechnung(b, k)); Assert.Contains("§ 19 UStG", kx); Assert.Equal(170m, ERechnung.Read(Rechnungen.XRechnung(b, k)).Gross);
            b.Firma.Iban = "DE00"; Assert.Throws<ArgumentException>(() => Rechnungen.XRechnung(b, k));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "STUDIO-INVOICE-01 Adapter: Entwurf mit Summen je Satz und Pflichtangaben – ohne Nummer, ohne Nebenwirkung")]
    public void Rechnung_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-INVOICE-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-INVOICE-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["position"] = "Beratung | 0 | 85" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["position"] = "Beratung | 1 | Std. | 85 | 16" }));
        Assert.Equal(("Std.", 85m, 19m, 10m), RechnungEntwurfAdapter.Positionen("Beratung | 6 | Std. | 85 | 19 | 10").Select(p => (p.Einheit, p.Preis, p.Ust, p.Rabatt)).Single());
        var root = Tmp();
        try
        {
            var text = Run(root, "STUDIO-INVOICE-01", "Verkaeufer: Erika Muster Webdesign, Musterstraße 1, 12345 Musterstadt\nSteuernummer: 12/345/67890\nKunde: Beispiel GmbH, Hauptstraße 5, 54321 Beispielhausen\n" +
                "Position: Beratung | 6 | Std. | 85 | 19 | 10\nPosition: Fachbuch | 2 | 28,04 | 7\nDatum: 15.11.2026", "entwurf.txt");
            Assert.Contains("1. Beratung: 6 Std. × 85,00 € − 10 % (19 % USt) = 459,00 €", text);
            Assert.Contains("USt 7 % auf 56,08 €: 3,93 €", text); Assert.Contains("Gesamtbetrag 606,22 €", text);
            Assert.Contains("✓ Steuernummer oder USt-IdNr.", text); Assert.Contains("ohne Nummer", text);
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== Mahnungen =====================

    [Fact(DisplayName = "DUN-01: Verzugszinsen tagesgenau – Unternehmer +9, Verbraucher +5 Prozentpunkte, Abschnitte beim Basiszins-Wechsel, fehlender Basiszins meldet sich")]
    public void Mahnung_zinsen()
    {
        var e = new MahnEinstellungen();
        Assert.Equal(1.27m, Mahnwesen.Basis(e, new DateOnly(2026, 6, 30))); Assert.Equal(1.52m, Mahnwesen.Basis(e, new DateOnly(2026, 7, 1)));
        Assert.Equal(10.27m, Mahnwesen.Zinssatz(e, new DateOnly(2026, 1, 15), true));
        Assert.Equal(8.72m, Mahnwesen.Verzugszinsen(e, 1000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), true).Zinsen); // 1000 × 10,27 % × 31/365
        var (z, parts) = Mahnwesen.Verzugszinsen(e, 1000m, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 31), false);
        Assert.Equal(10.69m, z);
        Assert.Equal([(30, 6.27m, 5.15m), (31, 6.52m, 5.54m)], parts.Select(p => (p.Tage, p.Satz, p.Zinsen)));
        Assert.Equal(0m, Mahnwesen.Verzugszinsen(e, 1000m, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31), true).Zinsen);
        Assert.Throws<InvalidOperationException>(() => Mahnwesen.Verzugszinsen(e, 100m, new DateOnly(2021, 12, 1), new DateOnly(2022, 1, 5), true));
        e.Basiszinsen.Add(new BasisZins { Ab = "2027-01-01", Satz = 2m });
        Assert.Equal(11m, Mahnwesen.Zinssatz(e, new DateOnly(2027, 3, 1), true));
    }

    [Fact(DisplayName = "DUN-02: Erinnerung → 1./2. Mahnung mit Fristen, Pauschale 40 € nur einmal und nur bei Unternehmern, Gebühren bei Verbrauchern, Brief mit Anrede und PDF")]
    public void Mahnung_stufen()
    {
        var a = Mahnwesen.Beispiel(Today); var f = a.Forderungen[0];
        Assert.Equal("bereit", Mahnwesen.Stand(a, f, Today).Id);
        Assert.Equal("nicht-faellig", Mahnwesen.Stand(a, a.Forderungen[2], Today).Id);
        Assert.Equal(2, Mahnwesen.Ueberfaellig(a, Today).Count);
        var m0 = Mahnwesen.Mahnen(a, f, Today);
        Assert.Equal(("Zahlungserinnerung", 0m, 0m, 1190m, "2026-11-22"), (m0.Name, m0.Zinsen, m0.Pauschale, m0.Summe, m0.FristBis));
        Assert.Equal("frist", Mahnwesen.Stand(a, f, Today).Id);
        Assert.Throws<InvalidOperationException>(() => Mahnwesen.Mahnen(a, f, Today));
        var m1 = Mahnwesen.Mahnen(a, f, new DateOnly(2026, 11, 23));
        Assert.Equal(("1. Mahnung", 13.38m, 40m, 0m, 1243.38m), (m1.Name, m1.Zinsen, m1.Pauschale, m1.Gebuehr, m1.Summe)); // 1190 × 10,52 % × 39/365
        var m2 = Mahnwesen.Mahnen(a, f, new DateOnly(2026, 12, 4));
        Assert.Equal((17.15m, 0m, 1247.15m), (m2.Zinsen, m2.Pauschale, m2.Summe));
        var brief = Mahnwesen.Brief(a, f, m1);
        Assert.Equal("1. Mahnung – Rechnung RE-2026-041 vom 01.10.2026", brief.Subject);
        Assert.Equal("Sehr geehrte Frau Beispiel,", brief.Salutation);
        Assert.Contains("Verzugspauschale nach § 288 Abs. 5 BGB: 40,00 €", brief.Body); Assert.Contains("Zu zahlen: 1.243,38 €", brief.Body);
        Assert.Contains("IBAN DE89370400440532013000", brief.Body);
        Assert.Contains("letzte Mahnung", Mahnwesen.Brief(a, f, Mahnwesen.Mahnen(a, f, new DateOnly(2026, 12, 20))).Body);
        Assert.Equal("ende", Mahnwesen.Stand(a, f, new DateOnly(2027, 1, 30)).Id);
        // Verbraucher: keine Pauschale, Gebühr ab der 2. Mahnung
        var c = a.Forderungen[1];
        Mahnwesen.Mahnen(a, c, Today); var c1 = Mahnwesen.Mahnen(a, c, Today.AddDays(8)); var c2 = Mahnwesen.Mahnen(a, c, Today.AddDays(19));
        Assert.Equal((0m, 0m, 2.50m), (c1.Pauschale, c1.Gebuehr, c2.Gebuehr));
        Assert.Equal("Sehr geehrte Damen und Herren,", Mahnwesen.Brief(a, c, c2).Salutation);
        var root = Tmp();
        try { var p = Path.Combine(root, "m.pdf"); Assert.Equal(1, LetterPdf.Write(brief, p)); Assert.Equal(1, PdfTools.Read(p).Pages); }
        finally { Directory.Delete(root, true); }
        Assert.Contains("\"RE-2026-041\";\"Beispiel GmbH\";1190,00", Mahnwesen.Csv(a, Today));
    }

    [Fact(DisplayName = "DUN-03: Offene Rechnungen aus „Rechnungen & Angebote“ übernehmen, Zahlung und Storno werden nachgezogen")]
    public void Mahnung_aus_rechnungen()
    {
        var root = Tmp();
        try
        {
            var kreis = new Nummernkreis(root); var b = Rechnungen.Beispiel(Today); var v = b.Vorgaenge[0]; v.Datum = "2026-10-01"; v.Leistung = "September 2026";
            var draft = Rechnungen.Neu(b, "rechnung", Today); b.Vorgaenge.Add(draft);
            Rechnungen.Festschreiben(b, v, kreis);
            var a = new MahnAkte();
            Assert.Equal(1, Mahnwesen.AusRechnungen(a, b.Vorgaenge, b.Firma));
            var f = a.Forderungen.Single();
            Assert.Equal((v.Nummer, 2034.22m, "2026-10-15", true, "Erika Muster Webdesign"), (f.Nummer, f.Betrag, f.Faellig, f.Unternehmer, a.Absender.Name));
            Rechnungen.Zahlung(v, 34.22m, Today); Assert.Equal(0, Mahnwesen.AusRechnungen(a, b.Vorgaenge)); Assert.Equal(2000m, f.Betrag);
            Rechnungen.Zahlung(v, 2000m, Today); Mahnwesen.AusRechnungen(a, b.Vorgaenge);
            Assert.Equal("bezahlt", Mahnwesen.Stand(a, f, Today).Id);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "STUDIO-DUNNING-01 Adapter: Zinsen mit Abschnitten und Pauschale – ohne Nebenwirkung, Pflichtfelder Betrag und Fälligkeit")]
    public void Mahnung_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-DUNNING-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(2, ad.Missing(new Dictionary<string, string>()).Count);
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["betrag"] = "viel" }));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-DUNNING-01", all));
        var root = Tmp();
        try
        {
            var text = Run(root, "STUDIO-DUNNING-01", "Betrag: 1000\nFaellig: 31.05.2026\nStichtag: 31.07.2026\nKunde: Verbraucher", "zinsen.txt");
            Assert.Contains("01.06.2026–30.06.2026: 30 Tage × 6,27 % = 5,15 €", text);
            Assert.Contains("Verzugszinsen: 10,69 €", text); Assert.DoesNotContain("Pauschale § 288 Abs. 5 BGB: ", text);
            var b2b = Run(root, "STUDIO-DUNNING-01", "Betrag: 1000\nFaellig: 31.12.2025\nStichtag: 31.01.2026\nKunde: Unternehmer", "zinsen.txt");
            Assert.Contains("Verzugszinsen: 8,72 €", b2b); Assert.Contains("Gesamt: 1.048,72 €", b2b);
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== Belege & Spesen =====================

    const string Bon = "Bürobedarf Muster GmbH\nMusterstraße 1\n12345 Musterstadt\nKassenbon\nKopierpapier A4      4,99\nToner schwarz       18,81\nSUMME EUR           23,80\nMwSt 19% 3,80\nNetto 20,00\nDatum: 03.11.2026 14:22\nVielen Dank für Ihren Einkauf";

    [Fact(DisplayName = "REC-01: Kassenbon auslesen – Datum, Händler, Summe, USt-Satz und -Betrag, Kategorie; Unsicheres als Hinweis")]
    public void Beleg_erkennen()
    {
        var e = BelegKit.Erkennen(Bon, null, Today);
        Assert.Equal((new DateOnly(2026, 11, 3), 23.80m, "Bürobedarf Muster GmbH", 19m, 3.80m, "Bürobedarf"), (e.Datum, e.Betrag, e.Haendler, e.UstSatz, e.Ust, e.Kategorie));
        Assert.Empty(e.Hinweise);
        var hotel = BelegKit.Erkennen("Hotel Musterhof\nRechnung Nr. 4711\n1 Übernachtung 100,00\nGesamtbetrag 107,00 EUR\nenthaltene MwSt 7 %: 7,00\n12.11.2026", null, Today);
        Assert.Equal((107.00m, 7m, 7.00m, "Reisekosten", new DateOnly(2026, 11, 12)), (hotel.Betrag, hotel.UstSatz, hotel.Ust, hotel.Kategorie, hotel.Datum));
        var bad = BelegKit.Erkennen("irgendwas 12,00 und 4,50", null, Today);
        Assert.Equal(12m, bad.Betrag); Assert.Contains(bad.Hinweise, h => h.Contains("größte Betrag")); Assert.Contains(bad.Hinweise, h => h.Contains("Kein Datum"));
        var calc = BelegKit.Erkennen("Bäckerei\n05.11.2026\nSumme 10,70\n7 %", null, Today);
        Assert.Equal(0.70m, calc.Ust);
        Assert.Null(BelegKit.Erkennen("Zukunft 01.01.2030 Summe 5,00", null, Today).Datum);
    }

    [Fact(DisplayName = "REC-02: Ablage – Dateiname „Datum Händler Betrag“, Monatsordner, Kopie ohne Überschreiben, Original bleibt")]
    public void Beleg_ablage()
    {
        var root = Tmp();
        try
        {
            var src = Path.Combine(root, "scan 1.pdf"); PdfTools.NewBlank(src);
            var b = BelegKit.Uebernehmen(new Beleg { Datei = src }, BelegKit.Erkennen(Bon, null, Today), "Text aus dem PDF");
            Assert.Equal("2026-11-03 Bürobedarf Muster GmbH 23,80 EUR.pdf", BelegKit.Ablagename(b, ".PDF"));
            var t1 = BelegKit.Ablegen(b, Path.Combine(root, "ablage")); var t2 = BelegKit.Ablegen(b, Path.Combine(root, "ablage"));
            Assert.Equal(Path.Combine(root, "ablage", "2026-11", "2026-11-03 Bürobedarf Muster GmbH 23,80 EUR.pdf"), t1);
            Assert.EndsWith("(2).pdf", t2); Assert.True(File.Exists(src));
            Assert.Equal("2026-11-03 Bad Name 1,00 EUR.jpg", BelegKit.Ablagename(new Beleg { Datum = "2026-11-03", Haendler = "Bad/Name:", Brutto = 1 }, ".jpg"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "REC-03: Reisekosten – An-/Abreisetag 14 €, voller Tag 28 €, eintägig über 8 h, Mahlzeiten kürzen (20/40 %), Kilometer 0,30 €")]
    public void Reise_pauschalen()
    {
        var p = new Pauschalen();
        var r = BelegKit.Abrechnen(new Reise { Beginn = "2026-11-09 07:30", Ende = "2026-11-10 18:00", Fruehstueck = 1, Km = 240, Fahrzeug = "pkw" }, p);
        Assert.Equal([(14m, 0m), (14m, 5.60m)], r.Tage.Select(t => (t.Pauschale, t.Kuerzung)));
        Assert.Equal((22.40m, 72.00m, 94.40m), (r.Verpflegung, r.Fahrt, r.Summe));
        var r3 = BelegKit.Abrechnen(new Reise { Beginn = "2026-11-02 08:00", Ende = "2026-11-04 17:00", Abend = 2, Km = 100, Fahrzeug = "motor", Uebernachtung = 180 }, p);
        Assert.Equal([2.80m, 16.80m, 14m], r3.Tage.Select(t => t.Betrag)); Assert.Equal("voller Tag (24 Stunden)", r3.Tage[1].Art);
        Assert.Equal((33.60m, 20.00m, 233.60m), (r3.Verpflegung, r3.Fahrt, r3.Summe));
        Assert.Equal(0m, BelegKit.Abrechnen(new Reise { Beginn = "2026-11-02 08:00", Ende = "2026-11-02 15:00" }, p).Verpflegung);
        Assert.Equal(14m, BelegKit.Abrechnen(new Reise { Beginn = "2026-11-02 08:00", Ende = "2026-11-02 17:00" }, p).Verpflegung);
        Assert.Equal(0m, BelegKit.Abrechnen(new Reise { Beginn = "2026-11-02 08:00", Ende = "2026-11-02 17:00", Mittag = 1, Abend = 1 }, p).Verpflegung); // nie unter 0
        Assert.Throws<ArgumentException>(() => BelegKit.Abrechnen(new Reise { Beginn = "2026-11-02 08:00", Ende = "2026-11-01 17:00" }, p));
        p.Kurz = 16; Assert.Equal(16m, BelegKit.Abrechnen(new Reise { Beginn = "2026-11-02 08:00", Ende = "2026-11-02 17:00" }, p).Verpflegung); // Tabelle einstellbar
    }

    [Fact(DisplayName = "REC-04: Monatsbericht – Summe, Vorsteuer, Kategorien mit Konto, Erstattung (privat + Reisen), CSV und PDF")]
    public void Beleg_bericht()
    {
        var s = BelegKit.Beispiel(Today); var m = BelegKit.Bericht(s, 2026, 11);
        Assert.Equal((3, 162.90m, 12.90m, 233.50m), (m.Belege.Count, m.Brutto, m.Ust, m.Erstattung));
        Assert.Equal(("Reisekosten", "4670"), (m.JeKategorie[0].Kategorie, m.JeKategorie[0].Konto));
        Assert.Single(m.Reisen);
        var csv = BelegKit.Csv(s, 2026, 11);
        Assert.Contains("03.11.2026;\"Bürobedarf Muster\";\"Bürobedarf\";\"4930\";23,80;19;3,80;20,00", csv); Assert.Contains("Reise: Beispielhausen", csv);
        var root = Tmp();
        try { var f = Path.Combine(root, "spesen.pdf"); Assert.Equal(1, BelegKit.Pdf(s, 2026, 11, f)); Assert.Equal(1, PdfTools.Read(f).Pages); }
        finally { Directory.Delete(root, true); }
        Assert.Empty(BelegKit.Bericht(s, 2026, 10).Belege);
    }

    [Fact(DisplayName = "STUDIO-RECEIPTS-01 Adapter: Belegtext auslesen mit Kategorie und Ablagename – ohne Nebenwirkung")]
    public void Beleg_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-RECEIPTS-01");
        Assert.False(ad.CanPerformSideEffect); Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-RECEIPTS-01", all));
        var root = Tmp();
        try
        {
            var text = Run(root, "STUDIO-RECEIPTS-01", "Stichtag: 15.11.2026\nText: " + Bon.Replace("Datum: 03.11.2026 14:22", "03.11.2026").Replace("\n", "\n  "), "beleg.txt");
            Assert.Contains("Betrag: 23,80 €", text); Assert.Contains("USt: 19 % = 3,80 €", text);
            Assert.Contains("Kategorie: Bürobedarf (Konto-Vorschlag SKR03 4930)", text); Assert.Contains("Ablagename: 2026-11-03 Bürobedarf Muster GmbH 23,80 EUR.pdf", text);
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== Preise kalkulieren =====================

    [Fact(DisplayName = "PRC-01: Zuschlagskalkulation bis zum Bruttopreis, Aufschlag und Marge, Rückwärtsrechnung vom Wunschpreis, Fehler bei negativen Werten")]
    public void Preis_kalkulation()
    {
        var b = PreisKit.Beispiel(); var s = PreisKit.Rechnen(b.Kalkulation);
        Assert.Equal((1.20m, 67.50m, 13.50m, 94.20m, 14.13m, 108.33m, 21.67m, 130.00m, 24.70m, 154.70m), (s.MaterialGk, s.Lohn, s.FertigungGk, s.Herstellkosten, s.VerwaltungGk, s.Selbstkosten, s.Gewinn, s.Netto, s.Ust, s.Brutto));
        Assert.Equal((63.52m, 16.67m), (s.Aufschlag, s.Marge));
        var r = PreisKit.Rueckwaerts(b.Kalkulation, 129m);
        Assert.Equal((108.40m, 0.07m, 90.33m, 0m, true), (r.Netto, r.Gewinn, r.MaxSelbstkosten, r.MaxMaterial, r.Lohnt));
        Assert.False(PreisKit.Rueckwaerts(b.Kalkulation, 100m).Lohnt);
        var k2 = Trendo.Core.Business.BizJson.Clone(b.Kalkulation); k2.Stunden = 0;
        Assert.Equal(71.41m, PreisKit.Rueckwaerts(k2, 129m).MaxMaterial); // 108,40/1,2 = 90,33 → /1,15/1,1
        b.Kalkulation.Kleinunternehmer = true; Assert.Equal(130m, PreisKit.Rechnen(b.Kalkulation).Brutto);
        b.Kalkulation.Material = -1; Assert.Throws<ArgumentException>(() => PreisKit.Rechnen(b.Kalkulation));
        Assert.Equal((20m, 25m), (PreisKit.MargeAusAufschlag(25), PreisKit.AufschlagAusMarge(20)));
    }

    [Fact(DisplayName = "PRC-02: Stundensatz für Selbständige, Break-Even mit Diagrammpunkten, Plattformgebühren, Preisstaffeln, Übergabe als Artikel")]
    public void Preis_weitere()
    {
        var st = PreisKit.Stundensatz(new StundensatzEingabe());
        Assert.Equal((205, 1640m, 984.0m, 62400m, 69600m, 70.73m, 339.50m), (st.Arbeitstage, st.Jahresstunden, st.Fakturierbar, st.GewinnVorSteuerJahr, st.UmsatzJahr, st.Satz, st.Tagessatz));
        Assert.Throws<ArgumentException>(() => PreisKit.Stundensatz(new StundensatzEingabe { Auslastung = 0 }));
        var be = PreisKit.BreakEven(1500m, 130m, 108.33m);
        Assert.Equal((21.67m, 70), (be.Deckungsbeitrag, be.Menge)); Assert.Equal(11, be.Punkte.Count); Assert.Equal((140, 18200m), (be.Punkte[^1].Menge, be.Punkte[^1].Umsatz));
        Assert.Throws<InvalidOperationException>(() => PreisKit.BreakEven(1500m, 100m, 120m));
        var b = PreisKit.Beispiel(); var sk = PreisKit.Rechnen(b.Kalkulation).Selbstkosten;
        var etsy = PreisKit.AufPlattform(b.Plattformen.Single(p => p.Id == "etsy"), 154.70m, 4.90m, sk, 19, false, 30);
        Assert.Equal((159.60m, 17.24m, 25.48m, 142.36m, 3.65m), (etsy.Brutto, etsy.Gebuehren, etsy.UstAbfuehren, etsy.Auszahlung, etsy.GewinnJeStueck));
        Assert.Equal(25.24m, PreisKit.AufPlattform(b.Plattformen.Single(p => p.Id == "amazon"), 154.70m, 4.90m, sk, 19, false, 30).Gebuehren);
        Assert.All(PreisKit.StandardPlattformen, p => Assert.False(string.IsNullOrWhiteSpace(p.Stand)));
        var tiers = PreisKit.Staffeln(b.Kalkulation, b.Staffeln);
        Assert.Equal([(1, 130.00m, 21.67m), (10, 123.50m, 15.17m), (50, 117.00m, 8.67m)], tiers.Select(t => (t.AbMenge, t.Netto, t.GewinnJeStueck)));
        Assert.True(PreisKit.Staffeln(b.Kalkulation, [new Staffel { AbMenge = 100, Rabatt = 20 }])[0].UnterSelbstkosten);
        var art = PreisKit.AlsArtikel(b.Kalkulation, "A-10");
        Assert.Equal(("Holzschneidebrett Eiche", 130m, 19m, "Stück"), (art.Name, art.Preis, art.Ust, art.Einheit));
        Assert.Contains("\"Selbstkosten\";108,33", PreisKit.Csv(b));
    }

    [Fact(DisplayName = "STUDIO-PRICING-01 Adapter: Kalkulationsschema mit Rückwärtsrechnung – ohne Nebenwirkung, Pflichtfeld Material")]
    public void Preis_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-PRICING-01");
        Assert.False(ad.CanPerformSideEffect); Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["material"] = "viel" }));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-PRICING-01", all));
        var root = Tmp();
        try
        {
            var text = Run(root, "STUDIO-PRICING-01", "Name: Holzschneidebrett\nMaterial: 12\nMaterialGK: 10\nStunden: 1,5\nStundensatz: 45\nFertigungsGK: 20\nGemeinkosten: 15\nGewinn: 20\nWunschpreis: 129", "kalkulation.txt");
            Assert.Contains("= Selbstkosten 108,33 €", text); Assert.Contains("= Bruttopreis 154,70 €", text);
            Assert.Contains("Rückwärts vom Wunschpreis 129,00 € brutto: netto 108,40 €, Gewinn 0,07 €", text);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Business & Geld A: Aktionen eindeutig, Studios in Profilen, Teams und Wissen")]
    public void Registrierung()
    {
        var all = PromptActions.CreateDefault();
        Assert.Equal(all.Count, all.Select(a => a.ActionId).Distinct().Count());
        foreach (var id in new[] { "STUDIO-INVOICE-01", "STUDIO-DUNNING-01", "STUDIO-RECEIPTS-01", "STUDIO-PRICING-01" })
        {
            Assert.NotNull(CapabilityCatalog.Find(id)); Assert.True(StudioKnowledgeBase.For(id)!.Detailed); Assert.NotNull(StudioTeams.For(id));
            Assert.Single(all.Where(a => a.StudioId == id));
        }
    }
}
