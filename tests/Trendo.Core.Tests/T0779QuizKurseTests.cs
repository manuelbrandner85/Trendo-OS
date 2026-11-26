using System.IO.Compression;
using System.Xml.Linq;
using PdfSharp.Pdf.IO;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0779: Kerne von „Quiz erstellen“ und „Kurse erstellen“ ohne Oberfläche – Bewertung, Textformat, GIFT hin und zurück, HTML, PDF, Kurs-Fortschritt, SCORM-Paket, Adapter.</summary>
public class T0779QuizKurseTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0779-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static Quiz Europa() => new()
    {
        Titel = "Europa", Bestehen = 60, FragenMischen = false,
        Fragen =
        [
            new() { Typ = "auswahl", Text = "Hauptstadt von Frankreich?", Antworten = [new() { Text = "Paris", Richtig = true }, new() { Text = "Lyon" }, new() { Text = "Nizza" }] },
            new() { Typ = "mehrfach", Text = "Nachbarn von Deutschland?", Antworten = [new() { Text = "Polen", Richtig = true }, new() { Text = "Österreich", Richtig = true }, new() { Text = "Spanien" }, new() { Text = "Italien" }], Punkte = 2 },
            new() { Typ = "wahrfalsch", Text = "Die Donau mündet ins Schwarze Meer.", Antworten = QuizKit.WahrFalsch(true) },
            new() { Typ = "luecke", Text = "Der höchste Berg Deutschlands ist die [Zugspitze]; Hauptstadt ist [Berlin|berlin]." },
            new() { Typ = "zuordnen", Text = "Hauptstädte", Paare = [new() { Links = "Italien", Rechts = "Rom" }, new() { Links = "Spanien", Rechts = "Madrid" }, new() { Links = "Polen", Rechts = "Warschau" }], Punkte = 3, Erklaerung = "Alle drei sind EU-Hauptstädte." },
        ],
    };

    [Fact(DisplayName = "T-0779 Quiz: Bewertung je Fragetyp – Auswahl, Mehrfach mit Abzug, Wahr/Falsch, Lücken mit Tippfehler-Toleranz, Zuordnen anteilig")]
    public void Scoring()
    {
        var q = Europa();
        Assert.Equal(1, QuizKit.Anteil(q.Fragen[0], QuizEingabe.Auswahl(0)));
        Assert.Equal(0, QuizKit.Anteil(q.Fragen[0], QuizEingabe.Auswahl(1)));
        Assert.Equal(1, QuizKit.Anteil(q.Fragen[1], QuizEingabe.Auswahl(0, 1)));
        Assert.Equal(0.5, QuizKit.Anteil(q.Fragen[1], QuizEingabe.Auswahl(0)));
        Assert.Equal(0.5, QuizKit.Anteil(q.Fragen[1], QuizEingabe.Auswahl(0, 1, 2)));   // 2/2 richtig − 1/2 falsch
        Assert.Equal(0, QuizKit.Anteil(q.Fragen[1], QuizEingabe.Auswahl(2, 3)));
        Assert.Equal(1, QuizKit.Anteil(q.Fragen[2], QuizEingabe.Auswahl(0)));
        Assert.Equal(1, QuizKit.Anteil(q.Fragen[3], QuizEingabe.Text("zugspitz", "BERLIN")));  // Tippfehler, Groß/klein
        Assert.Equal(0.5, QuizKit.Anteil(q.Fragen[3], QuizEingabe.Text("Watzmann", "Berlin")));
        Assert.Equal(1, QuizKit.Anteil(q.Fragen[4], QuizEingabe.Paare(0, 1, 2)));
        Assert.Equal(1 / 3.0, QuizKit.Anteil(q.Fragen[4], QuizEingabe.Paare(0, 2, 1)), 3);
        Assert.Equal(["Zugspitze", "Berlin"], QuizKit.Luecken(q.Fragen[3].Text).Select(g => g[0]));
        Assert.Equal("Der höchste Berg Deutschlands ist die ___; Hauptstadt ist ___.", QuizKit.LueckenText(q.Fragen[3].Text, "___"));
        Assert.Empty(QuizKit.Pruefen(q).Where(f => f.Fehler));
        // Tempo-Bonus nur mit Zeit und nur bei voll richtig; Zeit überschritten = 0
        q.Sekunden = 20; q.TempoBonus = true;
        Assert.Equal(1.5, QuizKit.Punkte(q, q.Fragen[0], QuizEingabe.Auswahl(0), 0));
        Assert.Equal(1.25, QuizKit.Punkte(q, q.Fragen[0], QuizEingabe.Auswahl(0), 10));
        Assert.Equal(0, QuizKit.Punkte(q, q.Fragen[0], QuizEingabe.Auswahl(0), 21));
        Assert.Equal(0.5, QuizKit.Punkte(q, q.Fragen[3], QuizEingabe.Text("x", "Berlin"), 1));
    }

    [Fact(DisplayName = "T-0779 Quiz: Prüfung meldet fehlende richtige Antwort, Lückentext ohne Lücke, doppelte Zuordnung, ungültige Zeit")]
    public void Checks()
    {
        var q = new Quiz { Titel = "", Sekunden = 5000, Fragen = [new() { Typ = "auswahl", Text = "A?", Antworten = [new() { Text = "x" }, new() { Text = "y" }] }, new() { Typ = "luecke", Text = "ohne Lücke" },
            new() { Typ = "zuordnen", Text = "Z", Paare = [new() { Links = "a", Rechts = "1" }, new() { Links = "b", Rechts = "1" }] }] };
        var f = QuizKit.Pruefen(q);
        Assert.Contains(f, x => x.Fehler && x.Text.Contains("keinen Titel"));
        Assert.Contains(f, x => x.Fehler && x.Feld == "Frage 1" && x.Text.Contains("genau eine"));
        Assert.Contains(f, x => x.Fehler && x.Feld == "Frage 2" && x.Text.Contains("keine Lücke"));
        Assert.Contains(f, x => x.Fehler && x.Feld == "Frage 3" && x.Text.Contains("doppelt"));
        Assert.Contains(f, x => x.Fehler && x.Text.Contains("3600"));
        Assert.Throws<InvalidOperationException>(() => new QuizSpiel(q, 1));
    }

    [Fact(DisplayName = "T-0779 Quiz: Spielmodus – Reihenfolge mit Startwert, Punkte, Zeit abgelaufen, Prozent ohne Tempo-Bonus, bestanden")]
    public void Game()
    {
        var q = Europa(); q.FragenMischen = true; q.Sekunden = 30; q.TempoBonus = true;
        var a = new QuizSpiel(q, 42); var b = new QuizSpiel(q, 42);
        Assert.Equal(a.Reihenfolge, b.Reihenfolge); Assert.Equal(5, a.Reihenfolge.Distinct().Count());
        var first = true;
        while (!a.Fertig)
        {
            var f = a.Aktuell!;
            if (first) { a.ZeitAbgelaufen(); first = false; continue; }
            a.Antworten(f.Typ switch { "luecke" => QuizEingabe.Text("Zugspitze", "Berlin"), "zuordnen" => QuizEingabe.Paare(0, 1, 2), "mehrfach" => QuizEingabe.Auswahl(0, 1), _ => QuizEingabe.Auswahl(0) }, 15);
        }
        var lost = q.Fragen[a.Reihenfolge[0]].Punkte;
        Assert.True(a.Ergebnisse[0].ZeitUm); Assert.Equal(0, a.Ergebnisse[0].Punkte);
        Assert.Equal((int)Math.Round(100 * (q.MaxPunkte - lost) / q.MaxPunkte), a.Prozent);
        Assert.Equal(Math.Round((q.MaxPunkte - lost) * 1.25, 2), a.Punkte); // halbe Zeit → +25 %
        Assert.Equal(a.Prozent >= 60, a.Bestanden);
        Assert.Throws<InvalidOperationException>(() => a.Antworten(QuizEingabe.Auswahl(0), 1));
    }

    [Fact(DisplayName = "T-0779 Quiz: Fragen-Textformat (KI und Einfügen) – alle fünf Typen, Punkte, Erklärung, Unlesbares wird gemeldet; Schreiben und Lesen ergibt dasselbe")]
    public void Text_format()
    {
        const string t = "1. ? Welche Farbe hat der Himmel?\n+ Blau\n- Grün\n- Rot\nE: Wegen der Streuung.\n\n? Welche sind Primzahlen?\n+ 2\n+ 3\n- 4\nP: 2\n\nW: Wasser kocht bei 100 °C auf Meereshöhe.\n+ Wahr\n\n"
                       + "L: Die Hauptstadt von Italien ist [Rom|Roma].\n\nZ: Ordne zu\nHund = bellt\nKatze -> miaut\nKuh → muht\n\n? Ohne richtige\n- a\n- b\n";
        var (f, skipped) = QuizText.Parse(t);
        Assert.Equal(["auswahl", "mehrfach", "wahrfalsch", "luecke", "zuordnen"], f.Select(x => x.Typ));
        Assert.Equal("Wegen der Streuung.", f[0].Erklaerung); Assert.Equal(2, f[1].Punkte);
        Assert.True(f[2].Antworten.Single(a => a.Text == "Wahr").Richtig);
        Assert.Equal(["Rom", "Roma"], QuizKit.Luecken(f[3].Text)[0]);
        Assert.Equal(["bellt", "miaut", "muht"], f[4].Paare.Select(p => p.Rechts));
        Assert.Single(skipped); Assert.Contains("Ohne richtige", skipped[0]);
        var (again, s2) = QuizText.Parse(QuizText.Write(f));
        Assert.Empty(s2);
        Assert.Equal(f.Select(x => (x.Typ, x.Text, x.Punkte, QuizKit.Loesung(x))), again.Select(x => (x.Typ, x.Text, x.Punkte, QuizKit.Loesung(x))));
    }

    [Fact(DisplayName = "T-0779 Quiz: GIFT (Moodle) – Export aller Typen mit Escapes, Import zurück; fremde GIFT-Datei mit Prozenten, {T}, Kurzantwort, Zahlenfrage (ausgelassen)")]
    public void Gift_roundtrip()
    {
        var q = Europa(); q.Fragen[0].Text = "Was kostet 1 {Brot} = 2:3?";
        var gift = QuizGift.Write(q);
        Assert.Contains(@"Was kostet 1 \{Brot\} \= 2\:3? { =Paris ~Lyon ~Nizza }", gift);
        Assert.Contains("~%50%Polen ~%50%Österreich ~%-50%Spanien ~%-50%Italien", gift);
        Assert.Contains("{TRUE}", gift);
        Assert.Contains("::Frage 4.1::Der höchste Berg Deutschlands ist die { =Zugspitze }; Hauptstadt ist Berlin.", gift);
        Assert.Contains("::Frage 4.2::Der höchste Berg Deutschlands ist die Zugspitze; Hauptstadt ist { =Berlin =berlin }.", gift);
        Assert.Contains("{ =Italien -> Rom =Spanien -> Madrid =Polen -> Warschau ####Alle drei sind EU-Hauptstädte. }", gift);
        var (back, skipped) = QuizGift.Read(gift);
        Assert.Empty(skipped);
        Assert.Equal(["auswahl", "mehrfach", "wahrfalsch", "luecke", "luecke", "zuordnen"], back.Fragen.Select(f => f.Typ));
        Assert.Equal("Was kostet 1 {Brot} = 2:3?", back.Fragen[0].Text);
        Assert.Equal(["Polen", "Österreich"], back.Fragen[1].Antworten.Where(a => a.Richtig).Select(a => a.Text));
        Assert.Equal("Alle drei sind EU-Hauptstädte.", back.Fragen[5].Erklaerung);
        Assert.Equal("Europa", back.Titel);
        // fremde Datei, wie Moodle sie dokumentiert
        const string foreign = "// Beispiel\n::Q1:: Wer liegt in Grants Grab begraben?{~niemand =Grant ~Napoleon}\n\n::Q2:: 1+1=2 {T}\n\n::Q3:: Zwei plus {=zwei =2} ist vier.\n\n"
                             + "::Q4:: Welche zwei? {~%50%a ~%50%b ~%-100%c}\n\n::Q5:: Wie viel ist 2+2? {#4}\n\n::Q6:: Paare {=Katze -> Miau =Hund -> Wuff =Kuh -> Muh}\n";
        var (fq, fs) = QuizGift.Read(foreign);
        Assert.Equal(["auswahl", "wahrfalsch", "luecke", "mehrfach", "zuordnen"], fq.Fragen.Select(f => f.Typ));
        Assert.Equal("Zwei plus [zwei|2] ist vier.", fq.Fragen[2].Text);
        Assert.Single(fs); Assert.Contains("Zahlenfragen", fs[0]);
    }

    [Fact(DisplayName = "T-0779 Quiz: HTML ist eine Datei ohne fremde Quellen, Daten sicher eingebettet; PDF hat mindestens zwei Seiten (Fragen, Lösungsblatt; Text prüft der App-Test mit PDFium)")]
    public void Html_and_pdf()
    {
        var root = Tmp();
        try
        {
            var q = Europa(); q.Beschreibung = "Achtung </script><b>";
            var html = QuizHtml.Html(q);
            Assert.DoesNotContain("src=\"http", html); Assert.DoesNotContain("href=\"http", html);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "</script>").Count); // nur die eigenen zwei Skript-Enden
            Assert.Contains("function TrendoQuiz", html); Assert.Contains("#selbsttest", html);
            var pdf = Path.Combine(root, "quiz.pdf");
            var pages = QuizPdf.Write(q, pdf);
            using var d = PdfReader.Open(pdf, PdfDocumentOpenMode.Import);
            Assert.Equal(pages, d.PageCount); Assert.True(pages >= 2);
        }
        finally { Directory.Delete(root, true); }
    }

    static Kurs Course()
    {
        var q = Europa();
        return new Kurs
        {
            Titel = "Erdkunde", Bestehen = 60, Reihenfolge = true,
            Kapitel =
            [
                new() { Titel = "Start", Lektionen = [new() { Id = "l1", Titel = "Einführung", Bloecke = [new() { Art = "text", Text = "Hallo" }] }] },
                new() { Titel = "Prüfung", Lektionen = [new() { Id = "l2", Titel = "Quiz", Bloecke = [new() { Id = "b-quiz", Art = "quiz", Quiz = q }] }, new() { Id = "l3", Titel = "Ende", Bloecke = [new() { Art = "hinweis", Text = "Fertig" }] }] },
            ],
            Lernende = [new() { Id = "p1", Name = "Erika Muster" }],
        };
    }

    [Fact(DisplayName = "T-0779 Kurs: der Reihe nach, Lektion mit Quiz erst nach bestandenem Quiz, Fortschritt, CSV, Gliederung aus Text, Malkurs-Vorlage")]
    public void Course_progress()
    {
        var k = Course(); var p = k.Lernende[0];
        Assert.False(KursKit.Offen(k, p, "l2"));
        Assert.Throws<InvalidOperationException>(() => KursKit.Abschliessen(k, p, "l2"));
        KursKit.Abschliessen(k, p, "l1");
        Assert.Contains("braucht mindestens 60 %", KursKit.Fehlt(k, p, KursKit.Lektion(k, "l2")!));
        KursKit.QuizErgebnis(p, "b-quiz", 40); Assert.Throws<InvalidOperationException>(() => KursKit.Abschliessen(k, p, "l2"));
        KursKit.QuizErgebnis(p, "b-quiz", 80); KursKit.QuizErgebnis(p, "b-quiz", 50); Assert.Equal(80, p.Quiz["b-quiz"]); // das beste zählt
        KursKit.Abschliessen(k, p, "l2");
        var st = KursKit.Fortschritt(k, p);
        Assert.Equal((2, 3, 67, (int?)80, false), (st.Erledigt, st.Gesamt, st.Prozent, st.QuizSchnitt, st.Fertig));
        KursKit.Abschliessen(k, p, "l3"); Assert.True(KursKit.Fortschritt(k, p).Fertig);
        var csv = KursKit.Csv(k);
        Assert.StartsWith("Name;Erledigt;Lektionen;Fortschritt %;Quiz-Schnitt %;Abgeschlossen;Zuletzt;Datum\n\"Erika Muster\";3;3;100;80;ja;\"Ende\";", csv);
        var g = KursKit.AusGliederung("Hunde", "# Grundlagen\n## Füttern\nZweimal am Tag.\n- Wasser\n- Futter\n! Nie Schokolade.\n## Gassi\nMorgens und abends.\n# Pflege\n## Fell\nBürsten.");
        Assert.Equal(["Grundlagen", "Pflege"], g.Kapitel.Select(c => c.Titel));
        Assert.Equal(["Füttern", "Gassi", "Fell"], KursKit.Lektionen(g).Select(x => x.Lektion.Titel));
        Assert.Equal(["text", "liste", "hinweis"], g.Kapitel[0].Lektionen[0].Bloecke.Select(b => b.Art));
        Assert.Empty(KursKit.Pruefen(g).Where(f => f.Fehler));
        var mal = KursKit.AusMalkurs("erste-schritte");
        Assert.Equal(Trendo.Core.Kreativ.Courses.All[0].Steps.Count, KursKit.Lektionen(mal).Count);
        Assert.Empty(KursKit.Pruefen(mal).Where(f => f.Fehler));
    }

    [Fact(DisplayName = "T-0779 Kurs: SCORM-1.2-Paket – Manifest mit Namensräumen, ein SCO auf index.html, Bestehensgrenze; Seite spricht die SCORM-API an; keine lokalen Pfade, Bilder eingebettet")]
    public void Scorm_package()
    {
        var root = Tmp();
        try
        {
            var k = Course();
            var img = Path.Combine(root, "bild.png"); File.WriteAllBytes(img, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="));
            k.Kapitel[0].Lektionen[0].Bloecke.Add(new KursBlock { Art = "bild", Bild = img, Text = "Punkt" });
            var zip = Path.Combine(root, "kurs.zip");
            KursHtml.WriteScorm(k, zip);
            using var z = ZipFile.OpenRead(zip);
            Assert.Equal(["imsmanifest.xml", "index.html"], z.Entries.Select(e => e.FullName).Order());
            XNamespace cp = "http://www.imsproject.org/xsd/imscp_rootv1p1p2", adl = "http://www.adlnet.org/xsd/adlcp_rootv1p2";
            using var ms = z.GetEntry("imsmanifest.xml")!.Open();
            var x = XDocument.Load(ms);
            Assert.Equal("1.2", x.Root!.Element(cp + "metadata")!.Element(cp + "schemaversion")!.Value);
            var res = x.Descendants(cp + "resource").Single();
            Assert.Equal("sco", res.Attribute(adl + "scormtype")!.Value); Assert.Equal("index.html", res.Attribute("href")!.Value);
            Assert.Equal("60", x.Descendants(adl + "masteryscore").Single().Value);
            using var r = new StreamReader(z.GetEntry("index.html")!.Open());
            var html = r.ReadToEnd();
            Assert.Contains("LMSInitialize", html); Assert.Contains("cmi.core.lesson_status", html); Assert.Contains("\"scorm\":true", html);
            Assert.Contains("data:image/png;base64,", html);
            Assert.DoesNotContain(root.Replace("\\", "\\\\"), html); Assert.DoesNotContain("Erika Muster", html); // keine Pfade und keine Lernenden im Paket
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0779 Adapter Quiz: Fragen aus dem Auftrag → HTML-Quiz im Projekt, ohne Nebenwirkung, Pflichtfeld Fragen")]
    public void Quiz_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-QUIZ-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-QUIZ-01", all));
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["fragen"] = "kein Format" }));
        var root = Tmp();
        try
        {
            var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
            var project = core.Projects.Create("probe", "Probe").Id; var wf = new StudioWorkflow(core, all);
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("quiz-1", project, "STUDIO-QUIZ-01", "Titel: Tiere\n? Was bellt?\n+ Hund\n- Katze\n\nW: Kühe geben Milch.\n+ Wahr", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var html = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("quiz.html")).RelativePath));
            Assert.Contains("<title>Tiere</title>", html); Assert.Contains("Was bellt?", html); Assert.Contains("Kühe geben Milch.", html);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0779 Adapter Kurs: Gliederung aus dem Auftrag → Kurs-Webseite im Projekt, ohne Nebenwirkung")]
    public void Kurs_Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-COURSE-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(2, ad.Missing(new Dictionary<string, string>()).Count);
        var root = Tmp();
        try
        {
            var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
            var project = core.Projects.Create("probe", "Probe").Id; var wf = new StudioWorkflow(core, all);
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("kurs-1", project, "STUDIO-COURSE-01", "Titel: Hundepflege\n# Grundlagen\n## Füttern\nZweimal am Tag füttern.\n## Gassi\nMorgens und abends.", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var html = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("kurs.html")).RelativePath));
            Assert.Contains("<title>Hundepflege</title>", html); Assert.Contains("Zweimal am Tag füttern.", html); Assert.Contains("\"titel\":\"Gassi\"", html);
        }
        finally { Directory.Delete(root, true); }
    }

    // ===================== Gegenprüfung =====================

    [Fact(DisplayName = "T-0779 H1: Lücken mit Zahlen und kurzen Lösungen ohne Toleranz – „1988, 4, Ast, B“ ist bei „1989, 3, Ost, A“ 0 %; Karteikarten „Eintippen“ unverändert; längere Wörter behalten einen Tippfehler")]
    public void H1_gaps_strict()
    {
        var f = new QuizFrage { Typ = "luecke", Text = "Mauerfall [1989], Seiten [3], Himmelsrichtung [Ost], Buchstabe [A]." };
        Assert.Equal(0, QuizKit.Anteil(f, QuizEingabe.Text("1988", "4", "Ast", "B")));
        Assert.Equal(1, QuizKit.Anteil(f, QuizEingabe.Text("1989", "3", "ost", "a")));
        Assert.False(QuizKit.LueckeOk("1989", "1988")); Assert.False(QuizKit.LueckeOk("Baujahr 1989", "Baujahr 1988")); Assert.True(QuizKit.LueckeOk("Zugspitze", "zugspitz"));
        Assert.True(LearnBox.CheckTyped("dog", "dox").Ok); // M-A: Karteikarten unverändert (Tippfehler bei kurzen Wörtern wie bisher)
        Assert.Contains(@"if(e.length<=4||/\p{Nd}/u.test(e))return false;", QuizHtml.PlayerJs);
    }

    [Fact(DisplayName = "T-0779 M3: Fragetext mit „<!--“ – im HTML als gültiges JSON-Escape, JSON lässt sich lesen und ergibt denselben Text")]
    public void M3_comment_in_json()
    {
        var q = new Quiz { Titel = "Kommentar <!-- und </script>", Fragen = [new() { Typ = "auswahl", Text = "Was beginnt mit <!-- in HTML?", Antworten = [new() { Text = "Kommentar", Richtig = true }, new() { Text = "Tabelle" }] }] };
        var html = QuizHtml.Html(q);
        Assert.DoesNotContain(@"<\!--", html);
        var start = html.IndexOf("<script id=\"tq-data\" type=\"application/json\">", StringComparison.Ordinal); start = html.IndexOf('>', start) + 1;
        var json = html[start..html.IndexOf("</script>", start, StringComparison.Ordinal)];
        Assert.DoesNotContain("<!--", json);
        var back = Quiz.FromJson(System.Text.Json.Nodes.JsonNode.Parse(json));
        Assert.Equal(q.Fragen[0].Text, back.Fragen[0].Text); Assert.Equal(q.Titel, back.Titel);
    }

    [Fact(DisplayName = "T-0779 M2: Kurs bettet nur Bilder (PNG/JPG/GIF/WEBP/SVG, höchstens 15 MB) ein – andere Dateien sind ein Fehler in der Prüfung und fehlen im Export")]
    public void M2_only_images_embedded()
    {
        var root = Tmp();
        try
        {
            var exe = Path.Combine(root, "geheim.txt"); File.WriteAllText(exe, "Kontonummer 123");
            var big = Path.Combine(root, "riesig.png"); using (var fs = File.Create(big)) fs.SetLength(15L * 1024 * 1024 + 1);
            var k = Course(); k.Kapitel[0].Lektionen[0].Bloecke.Add(new KursBlock { Art = "bild", Bild = exe }); k.Kapitel[0].Lektionen[0].Bloecke.Add(new KursBlock { Art = "bild", Bild = big });
            var f = KursKit.Pruefen(k);
            Assert.Contains(f, x => x.Fehler && x.Text.Contains("geheim.txt")); Assert.Contains(f, x => x.Fehler && x.Text.Contains("riesig.png"));
            var html = KursHtml.Html(k);
            Assert.DoesNotContain("Kontonummer", html); Assert.DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Kontonummer 123")), html);
            Assert.DoesNotContain("data:application/octet-stream", html); Assert.DoesNotContain("data:image/png", html);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0779 Niedrig: CSV entschärft =,+,-,@; Manifest ohne Steuerzeichen; Quiz im Kurs mit der Bestehensgrenze des Kurses; suspend_data bleibt gültiges JSON")]
    public void Low_fixes()
    {
        var k = Course(); k.Titel = "Kurs\u0001mit\u0007Steuerzeichen"; k.Lernende[0].Name = "=HYPERLINK(\"x\")"; k.Bestehen = 75;
        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\";", KursKit.Csv(k));
        var m = KursHtml.Manifest(k, "id-1");
        Assert.DoesNotContain('\u0001', m); Assert.DoesNotContain('\u0007', m); System.Xml.Linq.XDocument.Parse(m);
        var html = KursHtml.Html(k);
        Assert.Contains("\"bestehen\":75", html); Assert.DoesNotContain("\"bestehen\":60", html);
        Assert.Contains("function kSuspend(s)", html); Assert.DoesNotContain("slice(0,4000)", html);
    }
}
