using PdfSharp.Pdf.IO;
using Trendo.Core.Buero;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0671 Arbeiten &amp; Büro Teil A: Bewerbung &amp; Lebenslauf, Karten &amp; Einladungen, Verträge-Ordner – Kerne ohne Oberfläche.</summary>
public class BueroTests
{
    static readonly DateTime Today = new(2026, 11, 15);
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-buero-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static (StudioWorkflow Wf, StudioExecutionContext Ctx, TrendoCore Core, string Project) Workflow(string root)
    {
        var all = PromptActions.CreateDefault();
        var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
        var project = core.Projects.Create("probe", "Probe").Id;
        return (new StudioWorkflow(core, all), new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default), core, project);
    }

    // ===================== Bewerbung & Lebenslauf =====================

    [Fact(DisplayName = "CV-01: Datumsangaben MM.JJJJ, M/JJJJ, JJJJ-MM und JJJJ; „heute“ und leer laufen; Unsinn wird erkannt")]
    public void Cv_dates()
    {
        Assert.Equal(2019 * 12 + 2, CvDate.Parse("03.2019"));
        Assert.Equal(2019 * 12 + 2, CvDate.Parse("3/2019"));
        Assert.Equal(2019 * 12 + 2, CvDate.Parse("2019-03"));
        Assert.Equal(2019 * 12, CvDate.Parse("2019"));
        Assert.Equal(2019 * 12 + 11, CvDate.Parse("2019", end: true));
        Assert.Null(CvDate.Parse("13.2019")); Assert.Null(CvDate.Parse("März 2019")); Assert.Null(CvDate.Parse(""));
        Assert.True(CvDate.IsOpen("")); Assert.True(CvDate.IsOpen("heute")); Assert.False(CvDate.IsOpen("12.2020"));
        Assert.Equal("03.2019 – heute", CvDate.Range("3/2019", ""));
        Assert.Equal("08.2016 – 07.2019", CvDate.Range("2016-08", "07.2019"));
    }

    [Fact(DisplayName = "CV-02: Prüfung findet fehlende Kontaktdaten, falsches Datum, Ende vor Beginn, Lücken ab 3 Monaten (auch bis heute) und falsche GER-Stufe")]
    public void Cv_check()
    {
        var a = CvBeispiel.Akte();
        Assert.DoesNotContain(CvCheck.Run(a.Cv, Today), f => f.Fehler); // Beispiel ist sauber
        Assert.Empty(CvCheck.Gaps(a.Cv, Today));
        var cv = a.Cv;
        cv.Person.Email = "keine-mail"; cv.Person.Telefon = "";
        cv.Stationen[1].Von = "Mai 2019";
        cv.Ausbildung[1].Bis = "01.2009"; // vor dem Beginn 09.2010
        cv.Sprachen.Add(new CvSprache { Name = "Spanisch", Stufe = "gut" });
        var f = CvCheck.Run(cv, Today);
        Assert.Contains(f, x => x.Fehler && x.Feld == "E-Mail");
        Assert.Contains(f, x => x.Fehler && x.Feld == "Telefon");
        Assert.Contains(f, x => x.Fehler && x.Text.Contains("„Mai 2019“"));
        Assert.Contains(f, x => x.Fehler && x.Text.Contains("Ende liegt vor dem Beginn"));
        Assert.Contains(f, x => x.Fehler && x.Text.Contains("Spanisch: Stufe „gut“"));

        // Lücke zwischen zwei Stationen und nach der letzten Station
        var g = new Lebenslauf();
        g.Stationen.Add(new CvStation { Titel = "A", Firma = "x", Von = "01.2020", Bis = "06.2020" });
        g.Stationen.Add(new CvStation { Titel = "B", Firma = "y", Von = "01.2021", Bis = "03.2026" });
        var gaps = CvCheck.Gaps(g, Today);
        Assert.Equal(2, gaps.Count);
        Assert.Equal((2020 * 12 + 6, 2020 * 12 + 11, 6), gaps[0]); // 07–12.2020
        Assert.Equal(8, gaps[1].Monate); // 04.2026 bis 11.2026 (der laufende Monat zählt mit)
        Assert.Contains(CvCheck.Run(g, Today), x => x.Feld == "Lücke" && x.Text.Contains("07.2020 – 12.2020"));
        // zwei Monate Pause sind keine Lücke
        g.Stationen[1].Von = "09.2020";
        Assert.Single(CvCheck.Gaps(g, Today));
    }

    [Fact(DisplayName = "CV-03: ATS-Fassung ohne Spalten mit Standard-Überschriften, neueste Station zuerst; Stichwort-Abgleich mit der Anzeige")]
    public void Cv_ats_and_keywords()
    {
        var a = CvBeispiel.Akte();
        var t = CvText.Ats(a.Cv);
        Assert.Contains("BERUFSERFAHRUNG", t); Assert.Contains("AUSBILDUNG", t); Assert.Contains("SPRACHEN", t);
        Assert.Contains("01/2021 - heute", t);
        Assert.True(t.IndexOf("Sachbearbeiterin Buchhaltung", StringComparison.Ordinal) < t.IndexOf("Muster & Co. KG", StringComparison.Ordinal));
        Assert.Contains("- Englisch: B2 (Sehr gute Kenntnisse)", t);
        Assert.DoesNotContain("\t", t); Assert.DoesNotContain("|", t);
        var (found, missing) = CvText.Abgleich(a.Stelle.Anzeige, a.Cv);
        Assert.Contains("DATEV", found); Assert.Contains("Mahnwesen", found);
        Assert.Contains(missing, w => w.Equals("Kreditorenbuchhaltung", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "CV-04: PDF in allen drei Layouts (lesbar, A4) und DOCX über „Trendo Text“")]
    public void Cv_pdf_docx()
    {
        var dir = Tmp();
        try
        {
            var a = CvBeispiel.Akte();
            foreach (var (id, _, _) in CvPdf.Layouts)
            {
                a.Cv.Layout = id; var f = Path.Combine(dir, id + ".pdf");
                var n = CvPdf.Write(a.Cv, f);
                var info = PdfTools.Read(f);
                Assert.Equal(n, info.Pages); Assert.True(n >= 1);
                Assert.Equal(595, info.PageSizes[0].Width, 0); Assert.Equal(842, info.PageSizes[0].Height, 0);
            }
            // sehr langer Lebenslauf → Seitenumbruch
            for (var i = 0; i < 25; i++) a.Cv.Stationen.Add(new CvStation { Titel = "Station " + i, Firma = "Firma", Von = "01.2000", Bis = "12.2000", Text = "Aufgabe eins\nAufgabe zwei\nAufgabe drei" });
            a.Cv.Layout = "klassisch";
            Assert.True(CvPdf.Write(a.Cv, Path.Combine(dir, "lang.pdf")) >= 2);
            Assert.Contains(CvCheck.Run(a.Cv, Today), x => x.Feld == "Länge");
            var docx = Path.Combine(dir, "cv.docx");
            Trendo.Core.Office.DocxFormat.Write(CvText.Doc(CvBeispiel.Akte().Cv), docx);
            var back = Trendo.Core.Office.DocxFormat.Read(docx).PlainText();
            Assert.Contains("Erika Muster", back); Assert.Contains("Berufserfahrung", back); Assert.Contains("DATEV", back);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "CV-05: Anschreiben-Entwurf nur aus Stelle und Lebenslauf, Platzhalter bei Fehlendem; als DIN-Brief über den Brief-Kern; KI-Text ohne doppelte Anrede")]
    public void Cv_letter()
    {
        var a = CvBeispiel.Akte();
        var t = Anschreiben.Entwurf(a.Cv, a.Stelle);
        Assert.StartsWith("Sehr geehrte Frau Beispiel,", t);
        Assert.Contains("Sachbearbeiterin Finanzbuchhaltung (m/w/d)", t); Assert.Contains("Musterwerke AG", t);
        Assert.Contains("Seit 01.2021 arbeite ich als Sachbearbeiterin Buchhaltung bei Beispiel GmbH.", t);
        Assert.Contains("DATEV", t); Assert.Contains("Englisch (B2)", t); Assert.Contains("ab 01.02.2027", t);
        var empty = Anschreiben.Entwurf(new Lebenslauf(), new CvStelle());
        Assert.Contains("[Stelle]", empty); Assert.Contains("[Firma]", empty); Assert.Contains("[ab wann?]", empty);
        var prompt = Anschreiben.Prompt(a.Cv, a.Stelle);
        Assert.Contains("erfinde keine", prompt); Assert.Contains("Kreditorenbuchhaltung", prompt);

        var draft = Anschreiben.AlsBrief(a.Cv, a.Stelle, t, Today);
        Assert.Equal("Sehr geehrte Frau Beispiel,", draft.Salutation); Assert.DoesNotContain("Sehr geehrte", draft.Body);
        Assert.Equal("Bewerbung als Sachbearbeiterin Finanzbuchhaltung (m/w/d)", draft.Subject);
        Assert.StartsWith("Musterwerke AG\nFrau Beispiel\nIndustriestraße 5", draft.Recipient);
        Assert.Equal("Erika Muster", draft.SenderName);
        var ai = Anschreiben.Clean("Ich bringe fünf Jahre Erfahrung mit.\n\nMit freundlichen Grüßen\nErika Muster", a.Stelle);
        Assert.StartsWith("Sehr geehrte Frau Beispiel,\n\nIch bringe", ai); Assert.DoesNotContain("Mit freundlichen", ai.Split('\n')[^1]);
    }

    [Fact(DisplayName = "CV-06: Bewerbungsmappe = Deckblatt + Anschreiben + Lebenslauf + Anlagen (PDF und Bild) in einer PDF; fehlende Anlage meldet sich")]
    public void Cv_mappe()
    {
        var dir = Tmp();
        try
        {
            var a = CvBeispiel.Akte();
            var letter = Path.Combine(dir, "l.pdf"); var lp = LetterPdf.Write(Anschreiben.AlsBrief(a.Cv, a.Stelle, Anschreiben.Entwurf(a.Cv, a.Stelle), Today), letter);
            var cv = Path.Combine(dir, "c.pdf"); var cp = CvPdf.Write(a.Cv, cv);
            var zeugnis = Path.Combine(dir, "zeugnis.pdf"); PdfTools.NewBlank(zeugnis);
            var foto = Path.Combine(dir, "zertifikat.png"); File.WriteAllBytes(foto, Trendo.Core.Kreativ.PngCodec.Encode(new Trendo.Core.Kreativ.Pixmap(400, 300)));
            a.Anlagen = [zeugnis, foto];
            var outFile = Path.Combine(dir, "mappe.pdf");
            var n = Mappe.Erstellen(a, letter, cv, outFile, Path.Combine(dir, "work"));
            Assert.Equal(1 + lp + cp + 1 + 1, n);
            Assert.Equal(n, PdfTools.Read(outFile).Pages);
            a.Deckblatt = false;
            Assert.Equal(lp + cp + 2, Mappe.Erstellen(a, letter, cv, Path.Combine(dir, "ohne.pdf"), Path.Combine(dir, "work2")));
            a.Anlagen.Add(Path.Combine(dir, "weg.pdf"));
            Assert.Throws<FileNotFoundException>(() => Mappe.Erstellen(a, letter, cv, Path.Combine(dir, "x.pdf"), Path.Combine(dir, "work3")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "CV-07: Übersicht – Fristen in 7 Tagen, Nachfassen nach 14 Tagen, Zählung, CSV mit Anführungszeichen; Daten als JSON hin und zurück")]
    public void Cv_tracker()
    {
        var list = new List<BewerbungEintrag>
        {
            new() { Id = "1", Firma = "A", Stelle = "x", Status = "versendet", Beworben = "2026-10-20", Frist = "2026-11-18" },
            new() { Id = "2", Firma = "B \"Nord\"", Stelle = "y", Status = "gespraech", Frist = "2026-11-10" },
            new() { Id = "3", Firma = "C", Stelle = "z", Status = "absage", Frist = "2026-11-16" },
            new() { Id = "4", Firma = "D", Stelle = "z", Status = "idee", Frist = "2026-12-30" },
        };
        var due = BewerbungsListe.Faellig(list, Today);
        Assert.Equal(["B \"Nord\"", "A"], due.Select(x => x.E.Firma));
        Assert.Equal(-5, due[0].Tage);
        Assert.Equal("A", Assert.Single(BewerbungsListe.Nachfassen(list, Today)).Firma);
        Assert.Equal(1, BewerbungsListe.Zaehlen(list)["absage"]);
        var csv = BewerbungsListe.Csv(list);
        Assert.StartsWith("Firma;Stelle;Status;Beworben am;Frist;Notiz\r\n", csv);
        Assert.Contains("\"B \"\"Nord\"\"\";\"y\";\"Gespräch\"", csv);
        var akte = CvBeispiel.Akte(); akte.Bewerbungen = list;
        var back = BewerbungsAkte.FromJson(akte.ToJson());
        Assert.Equal(4, back.Bewerbungen.Count); Assert.Equal("Erika Muster", back.Cv.Person.Name); Assert.Equal("B2", back.Cv.Sprachen[1].Stufe);
        Assert.Empty(BewerbungsAkte.FromJson(null).Cv.Stationen);
    }

    [Fact(DisplayName = "STUDIO-CV-01 Adapter: Lebenslauf aus Stichpunkten prüfen, ATS-Fassung und Abgleich – ohne Nebenwirkung, Pflichtfeld Name")]
    public void Lebenslauf_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-CV-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-CV-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["name"] = "x", ["stationen"] = "irgendwann irgendwas" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["name"] = "x", ["sprachen"] = "Englisch gut" }));
        var root = Tmp();
        try
        {
            var (wf, ctx, core, project) = Workflow(root);
            var req = new StudioPromptRequest("cv-1", project, "STUDIO-CV-01",
                "Name: Erika Muster\nEmail: erika@example.org\nTelefon: 0171 2345678\nOrt: Musterstadt\nStationen: 04.2021-heute Sachbearbeiterin bei Beispiel GmbH; 01.2019-06.2020 Bürokraft bei Muster KG\nAusbildung: 08.2016-12.2018 Ausbildung Kauffrau an der Berufsschule\nKenntnisse: DATEV, Excel\nSprachen: Deutsch Muttersprache, Englisch B2\nAnzeige: Wir suchen Unterstützung in der Buchhaltung mit DATEV und SAP.\nStichtag: 2026-11-15",
                [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var text = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("lebenslauf.txt")).RelativePath));
            Assert.Contains("Lücke von 9 Monaten (07.2020 – 03.2021)", text);
            Assert.Contains("Schon drin: DATEV", text); Assert.Contains("SAP", text);
            Assert.Contains("BERUFSERFAHRUNG\nSachbearbeiterin, Beispiel GmbH\n04/2021 - heute", text);
            Assert.Contains("- Englisch: B2", text);
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== Karten & Einladungen =====================

    [Fact(DisplayName = "CARDS-01: Formate und Druckbögen – Klappkarte außen Rückseite|Vorderseite, innen links|rechts; Postkarte zwei Seiten; alle fünf Vorlagen")]
    public void Cards_formats()
    {
        var a6 = KartenKit.Format("a6"); Assert.True(a6.Klapp); Assert.Equal(210, a6.FlatW); Assert.Equal(148, a6.H);
        var k = KartenVorlagen.Neu("hochzeit");
        Assert.Equal(["vorne", "innen-links", "innen-rechts", "hinten"], KartenKit.Seiten(k).Select(s => s.Id));
        Assert.Equal(["hinten", "vorne"], KartenKit.Boegen(k)[0].Panels);
        Assert.Equal(["innen-links", "innen-rechts"], KartenKit.Boegen(k)[1].Panels);
        var p = KartenVorlagen.Neu("danke", "postkarte");
        Assert.Equal(2, KartenKit.Seiten(p).Count); Assert.Empty(p.InnenRechts.Texte); Assert.NotEmpty(p.Hinten.Texte); // Innentexte wandern nach hinten
        foreach (var (id, _, _) in KartenVorlagen.Alle) foreach (var f in KartenKit.Formate)
        {
            var c = KartenVorlagen.Neu(id, f.Id);
            Assert.DoesNotContain(KartenKit.Pruefen(c), x => x.Fehler && x.Feld != "Angaben");
        }
        Assert.Throws<ArgumentException>(() => KartenVorlagen.Neu("gibtsnicht"));
        Assert.Throws<ArgumentException>(() => KartenKit.Format("a3"));
    }

    [Fact(DisplayName = "CARDS-02: Gästeliste – schnell eintragen, Zählen (Personen nur bei Zusage), CSV hin und zurück mit Umlauten und Anführungszeichen")]
    public void Cards_guests()
    {
        var g = Gaeste.ParseLines("Familie Muster, 4, ja\nErika Muster; erika@example.org\nOnkel Kurt, 2, nein\nTante Ilse, vielleicht, Tisch 3\n\n");
        Assert.Equal(4, g.Count);
        Assert.Equal((4, "zugesagt"), (g[0].Personen, g[0].Status));
        Assert.Equal("erika@example.org", g[1].Email);
        Assert.Equal("abgesagt", g[2].Status); Assert.Equal(("vielleicht", "Tisch 3"), (g[3].Status, g[3].Gruppe));
        var st = Gaeste.Zaehlen(g);
        Assert.Equal(new Gaeste.Stand(4, 1, 1, 1, 1, 4, 6), st);
        g[1].Notiz = "bringt \"Kuchen\" mit; vegan";
        var csv = Gaeste.Csv(g);
        var back = Gaeste.FromCsv("﻿" + csv);
        Assert.Equal(g.Select(x => (x.Name, x.Email, x.Personen, x.Status, x.Notiz)), back.Select(x => (x.Name, x.Email, x.Personen, x.Status, x.Notiz)));
        var comma = Gaeste.FromCsv("name,anzahl,rsvp\nJörg Müller,3,Zusage\n");
        Assert.Equal(("Jörg Müller", 3, "zugesagt"), (comma[0].Name, comma[0].Personen, comma[0].Status));
        Assert.Throws<InvalidDataException>(() => Gaeste.FromCsv("Vorname;Alter\nA;3"));
    }

    [Fact(DisplayName = "CARDS-03: Platzhalter und Serienkarten – eine Karte je Gast ohne Absagen, {Name} braucht es; Prüfung (Kontrast, Rand, fehlendes Datum, Postkarte innen)")]
    public void Cards_serie_and_check()
    {
        var k = KartenVorlagen.Neu("einladung");
        k.Gaeste = Gaeste.ParseLines("Anna, ja\nBen, nein\nCarla");
        var serie = KartenKit.Serie(k);
        Assert.Equal(["Anna", "Carla"], serie.Select(s => s.Gast.Name));
        Assert.Equal("Hallo Carla,", serie[1].Karte.InnenRechts.Texte[0].Text);
        Assert.Contains("Gemeindesaal Musterstadt", serie[0].Karte.InnenRechts.Texte[1].Text);
        Assert.Contains("{Name}", k.InnenRechts.Texte[0].Text); // Vorlage bleibt unverändert
        var ohne = KartenVorlagen.Neu("einladung"); ohne.Gaeste = k.Gaeste; ohne.InnenRechts.Texte[0].Text = "Hallo,";
        Assert.Contains("{Name}", Assert.Throws<InvalidOperationException>(() => KartenKit.Serie(ohne)).Message);
        Assert.Throws<InvalidOperationException>(() => KartenKit.Serie(KartenVorlagen.Neu("danke")));

        var c = KartenVorlagen.Neu("geburtstag");
        c.Vorne.Texte[0].Farbe = "#FFF0D8"; // fast wie der Hintergrund
        c.Vorne.Texte[1].Y = 0.97; c.Vorne.Texte[1].Groesse = 5;
        var f = KartenKit.Pruefen(c);
        Assert.Contains(f, x => x.Text.Contains("hebt sich kaum"));
        Assert.Contains(f, x => x.Text.Contains("nah am Rand")); Assert.Contains(f, x => x.Text.Contains("5 pt"));
        var h = KartenVorlagen.Neu("hochzeit"); h.Datum = "";
        Assert.Contains(KartenKit.Pruefen(h), x => x.Fehler && x.Text.Contains("{Datum}"));
        var post = KartenVorlagen.Neu("geburtstag"); post.Format = "postkarte";
        Assert.Contains(KartenKit.Pruefen(post), x => x.Feld == "Format");
        Assert.True(KartenKit.Kontrast("#000000", "#FFFFFF") > 20); Assert.Equal(1, KartenKit.Kontrast("#123456", "#123456"), 3);
    }

    [Fact(DisplayName = "CARDS-04: Druck-PDF – je Bogen eine Seite, Größe = Endformat + Beschnitt + Markenrand, TrimBox/BleedBox gesetzt; Serie = Bögen je Gast; Bild bis in den Beschnitt")]
    public void Cards_pdf()
    {
        var dir = Tmp();
        try
        {
            var k = KartenVorlagen.Neu("hochzeit");
            var img = Path.Combine(dir, "foto.png"); var px = new Trendo.Core.Kreativ.Pixmap(1300, 1800); px.Fill(200, 120, 90, 255); File.WriteAllBytes(img, Trendo.Core.Kreativ.PngCodec.Encode(px));
            k.Vorne.Bild = img; k.Vorne.BildModus = "fuellen";
            var f = Path.Combine(dir, "karte.pdf");
            Assert.Equal(2, KartenPdf.Write(k, f));
            Assert.Equal((236.0, 174.0), KartenPdf.PageSize(k)); // 210 + 2·(3+10), 148 + 2·13
            using (var doc = PdfReader.Open(f, PdfDocumentOpenMode.Import))
            {
                var p = doc.Pages[0];
                Assert.Equal(236 * 72 / 25.4, p.Width.Point, 0.1); Assert.Equal(174 * 72 / 25.4, p.Height.Point, 0.1);
                Assert.Equal(210 * 72 / 25.4, p.TrimBox.Width, 0.1); Assert.Equal(13 * 72 / 25.4, p.TrimBox.X1, 0.1);
                Assert.Equal(216 * 72 / 25.4, p.BleedBox.Width, 0.1);
            }
            Assert.DoesNotContain(KartenKit.Pruefen(k), x => x.Text.Contains("dpi")); // 1300 px auf 111 mm ≈ 297 dpi
            k.Marken = false; k.Beschnitt = 0;
            Assert.Equal((210.0, 148.0), KartenPdf.PageSize(k));
            k.Gaeste = Gaeste.ParseLines("Anna, ja\nBen\nCarla, nein");
            Assert.Equal(4, KartenPdf.WriteSerie(k, Path.Combine(dir, "serie.pdf")));
            var post = KartenVorlagen.Neu("danke", "postkarte");
            Assert.Equal(2, KartenPdf.Write(post, Path.Combine(dir, "post.pdf")));
            k.Beschnitt = 15;
            Assert.Throws<ArgumentException>(() => KartenPdf.Write(k, Path.Combine(dir, "x.pdf")));
            // kleines Bild → Hinweis auf geringe Auflösung
            var small = Path.Combine(dir, "klein.png"); File.WriteAllBytes(small, Trendo.Core.Kreativ.PngCodec.Encode(new Trendo.Core.Kreativ.Pixmap(200, 200)));
            var s = KartenVorlagen.Neu("danke"); s.Vorne.Bild = small;
            Assert.Contains(KartenKit.Pruefen(s), x => x.Text.Contains("dpi"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "STUDIO-CARDS-01 Adapter: Karte aus Anlass anlegen, Platzhalter füllen, Gäste zählen – ohne Nebenwirkung, Pflichtfeld Anlass")]
    public void Karte_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-CARDS-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-CARDS-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["anlass"] = "Ostern" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["anlass"] = "Hochzeit", ["format"] = "a3" }));
        Assert.Equal("einladung", KarteAdapter.Anlass("Gartenparty"));
        var root = Tmp();
        try
        {
            var (wf, ctx, core, project) = Workflow(root);
            var req = new StudioPromptRequest("karte-1", project, "STUDIO-CARDS-01", "Anlass: Hochzeit\nFormat: a5\nDatum: 12.06.2027\nOrt: Gut Beispielhof\nAntwort: 01.05.2027\nGäste: Familie Muster, 4, ja; Onkel Kurt, nein; Tante Ilse",
                [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var text = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("karte.txt")).RelativePath));
            Assert.Contains("Karte: Hochzeit – Klappkarte A5", text);
            Assert.Contains("am 12.06.2027 Gut Beispielhof", text); Assert.Contains("bis 01.05.2027", text);
            Assert.Contains("Gäste: 3 eingeladen, 1 Zusagen (4 Personen), 1 Absagen, 1 offen", text);
            Assert.Contains("\"Familie Muster\"", text);
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== Verträge-Ordner =====================

    [Fact(DisplayName = "CONTRACTS-01: „Spätestens kündigen bis“ rückwärts vom Vertragsende – Monatsende richtig (30.06./3 M → 31.03., 28.02./1 M → 31.01., 31.12./1 M → 30.11.), Wochen und Tage")]
    public void Contracts_deadline()
    {
        Assert.Equal(new DateTime(2027, 3, 31), Vertraege.KuendigenBis(new DateTime(2027, 6, 30), 3, "monate"));
        Assert.Equal(new DateTime(2027, 1, 31), Vertraege.KuendigenBis(new DateTime(2027, 2, 28), 1, "monate"));
        Assert.Equal(new DateTime(2026, 11, 30), Vertraege.KuendigenBis(new DateTime(2026, 12, 31), 1, "monate"));
        Assert.Equal(new DateTime(2027, 4, 14), Vertraege.KuendigenBis(new DateTime(2027, 5, 14), 1, "monate"));
        Assert.Equal(new DateTime(2028, 1, 31), Vertraege.KuendigenBis(new DateTime(2028, 2, 29), 1, "monate")); // Schaltjahr
        Assert.Equal(new DateTime(2027, 6, 2), Vertraege.KuendigenBis(new DateTime(2027, 6, 30), 4, "wochen"));
        Assert.Equal(new DateTime(2027, 6, 16), Vertraege.KuendigenBis(new DateTime(2027, 6, 30), 14, "tage"));
        Assert.Throws<ArgumentException>(() => Vertraege.KuendigenBis(Today, 1, "jahre"));
    }

    [Fact(DisplayName = "CONTRACTS-02: Nächstes Ende – Mindestlaufzeit endet am Tag vor dem Jahrestag, danach je Verlängerung; Frist verpasst → nächste Periode; unbefristet zum Monatsende; endet von selbst")]
    public void Contracts_end()
    {
        var v = new Vertrag { Name = "Handy", Beginn = "15.03.2025", LaufzeitMonate = 24, VerlaengerungMonate = 1, FristWert = 1, FristEinheit = "monate" };
        Assert.Equal(new DateTime(2027, 3, 14), Vertraege.NaechstesEnde(v, Today));
        Assert.Equal(new DateTime(2027, 2, 14), Vertraege.Stand(v, Today).KuendigenBis);
        Assert.Equal(new DateTime(2027, 3, 14), Vertraege.NaechstesEnde(v, new DateTime(2027, 2, 14)));  // letzter Tag geht noch
        Assert.Equal(new DateTime(2027, 4, 14), Vertraege.NaechstesEnde(v, new DateTime(2027, 2, 15)));  // verpasst → ein Monat später
        var ins = new Vertrag { Name = "Hausrat", Beginn = "2025-01-01", LaufzeitMonate = 12, VerlaengerungMonate = 12, FristWert = 3, FristEinheit = "monate" };
        Assert.Equal(new DateTime(2026, 12, 31), Vertraege.NaechstesEnde(ins, new DateTime(2026, 9, 30)));
        Assert.Equal(new DateTime(2027, 12, 31), Vertraege.NaechstesEnde(ins, new DateTime(2026, 10, 1)));
        var abo = new Vertrag { Name = "Abo", Beginn = "2026-03-10", LaufzeitMonate = 0, FristWert = 1, FristEinheit = "monate", ZumMonatsende = true };
        Assert.Equal(new DateTime(2026, 12, 31), Vertraege.NaechstesEnde(abo, Today)); // bis 30.11. kündigen
        Assert.Equal(new DateTime(2027, 1, 31), Vertraege.NaechstesEnde(abo, new DateTime(2026, 12, 1)));
        abo.ZumMonatsende = false; abo.FristWert = 14; abo.FristEinheit = "tage";
        Assert.Equal(new DateTime(2026, 11, 29), Vertraege.NaechstesEnde(abo, Today));
        var fix = new Vertrag { Name = "Fest", Beginn = "2026-01-01", LaufzeitMonate = 12, VerlaengerungMonate = 0 };
        Assert.Equal("grau", Vertraege.Stand(fix, Today).Ampel); Assert.Contains("Endet von selbst am 31.12.2026", Vertraege.Stand(fix, Today).Text);
        Assert.Null(Vertraege.NaechstesEnde(fix, new DateTime(2027, 1, 5)));
        Assert.Contains("Beginn bitte als Datum", Vertraege.Stand(new Vertrag { Name = "X", Beginn = "irgendwann" }, Today).Text);
    }

    [Fact(DisplayName = "CONTRACTS-03: Ampel (rot ≤ 14, gelb ≤ 60 Tage, grau gekündigt), Kosten pro Monat/Jahr/Kategorie, gekündigte zählen ab Ende nicht mehr")]
    public void Contracts_ampel_costs()
    {
        var v = new Vertrag { Name = "Handy", Beginn = "2025-01-01", LaufzeitMonate = 24, VerlaengerungMonate = 1, FristWert = 1, FristEinheit = "monate" }; // Ende 31.12.2026, bis 30.11.2026
        Assert.Equal(("gelb", 15), (Vertraege.Stand(v, new DateTime(2026, 11, 15)).Ampel, Vertraege.Stand(v, new DateTime(2026, 11, 15)).TageBis!.Value));
        Assert.Equal("rot", Vertraege.Stand(v, new DateTime(2026, 11, 16)).Ampel);
        Assert.Equal("gelb", Vertraege.Stand(v, new DateTime(2026, 10, 1)).Ampel);
        Assert.Equal("gruen", Vertraege.Stand(v, new DateTime(2026, 9, 1)).Ampel);
        Assert.Contains("(morgen)", Vertraege.Stand(v, new DateTime(2026, 11, 29)).Text);
        var list = new List<Vertrag>
        {
            new() { Name = "Handy", Kategorie = "Handy & Internet", Kosten = 30m, Intervall = "monat", Beginn = "2025-01-01" },
            new() { Name = "Haftpflicht", Kategorie = "Versicherung", Kosten = 60m, Intervall = "jahr", Beginn = "2025-01-01" },
            new() { Name = "Hausrat", Kategorie = "Versicherung", Kosten = 30m, Intervall = "quartal", Beginn = "2025-01-01" },
            new() { Name = "Alt", Kategorie = "Streaming & Abos", Kosten = 10m, Intervall = "monat", Beginn = "2024-01-01", GekuendigtZum = "2026-10-31" },
            new() { Name = "Gerät", Kategorie = "Sonstiges", Kosten = 500m, Intervall = "einmalig", Beginn = "2026-01-01" },
        };
        var k = Vertraege.Kosten(list, Today);
        Assert.Equal(45m, k.Monat); Assert.Equal(540m, k.Jahr);
        Assert.Equal(("Handy & Internet", 360m), (k.Kategorien[0].Kategorie, k.Kategorien[0].Jahr));
        Assert.Equal((180m, 2), (k.Kategorien.Single(c => c.Kategorie == "Versicherung").Jahr, k.Kategorien.Single(c => c.Kategorie == "Versicherung").Anzahl));
        Assert.Equal(120m, k.Gespart);
        Assert.Equal("grau", Vertraege.Stand(list[3], Today).Ampel); Assert.Contains("Beendet am 31.10.2026", Vertraege.Stand(list[3], Today).Text);
        var csv = Vertraege.Csv(list, Today);
        Assert.Contains("\"Haftpflicht\";\"\";\"Versicherung\";\"\";\"2025-01-01\";60,00;\"jährlich\";5,00", csv);
        var back = VertragsOrdner.FromJson(new VertragsOrdner { Vertraege = list, VorlaufTage = 21 }.ToJson());
        Assert.Equal((5, 21, 60m), (back.Vertraege.Count, back.VorlaufTage, back.Vertraege[1].Kosten));
    }

    [Fact(DisplayName = "CONTRACTS-04: Erinnerung als Kalenderdatei über den Kalender-Kern – ganztägig am letzten Kündigungstag, Alarm 14 Tage vorher, von Ical.Net lesbar")]
    public void Contracts_ics()
    {
        var o = Vertraege.Beispiel(Today);
        var ics = Vertraege.Ics(o.Vertraege, Today, 14);
        Assert.Equal(4, ics.Split("\r\n").Count(l => l == "BEGIN:VEVENT"));
        Assert.Contains("TRIGGER:-PT20160M", ics);
        var cal = Ical.Net.Calendar.Load(ics)!;
        Assert.Equal(4, cal.Events.Count);
        var handy = cal.Events.Single(e => e.Summary!.Contains("Handyvertrag"));
        var s = Vertraege.Stand(o.Vertraege[0], Today);
        Assert.Equal(s.KuendigenBis!.Value, handy.DtStart!.Value.Date);
        Assert.False(handy.DtStart.HasTime);
        Assert.Contains("KD-123456", handy.Description);
        Assert.Throws<InvalidOperationException>(() => Vertraege.Ics([new Vertrag { Name = "Fest", Beginn = "2026-01-01", LaufzeitMonate = 12, VerlaengerungMonate = 0 }], Today));
        Assert.Throws<ArgumentException>(() => Vertraege.Ics(o.Vertraege, Today, 400));
    }

    [Fact(DisplayName = "CONTRACTS-05: Kündigungsschreiben über die Brief-Vorlage „Kündigung“ – zum berechneten Ende, hilfsweise nächstmöglich, mit Kundennummer; als PDF über den Brief-Kern")]
    public void Contracts_letter()
    {
        var o = Vertraege.Beispiel(Today); var v = o.Vertraege[1];
        var absender = new LetterDraft { SenderName = "Erika Muster", SenderStreet = "Musterweg 12", SenderCity = "12345 Musterstadt" };
        var d = Vertraege.Kuendigung(v, Today, absender);
        var end = Vertraege.Stand(v, Today).Ende!.Value;
        Assert.Equal("Kündigung meines Vertrags HR-778899 (Hausratversicherung)", d.Subject);
        Assert.Contains($"fristgerecht zum {end:dd.MM.yyyy}, hilfsweise zum nächstmöglichen Zeitpunkt", d.Body);
        Assert.Contains("schriftlich", d.Body);
        Assert.StartsWith("Muster Versicherung AG\nVersicherungsplatz 1", d.Recipient);
        Assert.Equal(("Erika Muster", "HR-778899"), (d.SenderName, d.Reference));
        var none = Vertraege.Kuendigung(new Vertrag { Name = "X", Beginn = "2026-01-01" }, Today);
        Assert.Contains("[Vertragsnummer]", none.Subject); Assert.Equal("[Anbieter und Anschrift]", none.Recipient);
        var dir = Tmp();
        try { var f = Path.Combine(dir, "k.pdf"); Assert.Equal(1, LetterPdf.Write(d, f)); Assert.Equal(1, PdfTools.Read(f).Pages); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "STUDIO-CONTRACTS-01 Adapter: spätestes Kündigungsdatum mit Ampel und Kosten – ohne Nebenwirkung, Pflichtfeld Beginn")]
    public void Vertrag_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-CONTRACTS-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-CONTRACTS-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["beginn"] = "gestern" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["beginn"] = "01.01.2025", ["frist"] = "bald" }));
        Assert.Equal((4, "wochen"), VertragFristAdapter.Frist("4 Wochen"));
        var root = Tmp();
        try
        {
            var (wf, ctx, core, project) = Workflow(root);
            var req = new StudioPromptRequest("vertrag-1", project, "STUDIO-CONTRACTS-01", "Name: Handyvertrag\nAnbieter: Beispiel Mobilfunk\nBeginn: 15.03.2025\nLaufzeit: 24\nVerlaengerung: 1\nFrist: 1 Monat\nKosten: 29,99\nStichtag: 15.11.2026",
                [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var text = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("vertrag.txt")).RelativePath));
            Assert.Contains("Spätestens kündigen bis 14.02.2027 (in 91 Tagen) – dann endet er am 14.03.2027", text);
            Assert.Contains("Ampel: grün", text); Assert.Contains("29,99 € im Monat, 359,88 € im Jahr", text);
        }
        finally { Directory.Delete(root, true); }
    }
}
