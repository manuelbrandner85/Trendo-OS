using System.IO.Compression;
using System.Text;
using PdfSharp.Pdf.IO;
using Trendo.Core.Business;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Etappe „Business &amp; Geld“ Teil B: Prüfungen aus docs/nachbau/{social-media,newsletter,flyer,verkaufsseiten}/anforderungen.md.</summary>
public class BusinessStudiosTests
{
    static string Tmp(string tag) { var d = Path.Combine(Path.GetTempPath(), "trendo-biz-" + tag + "-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static SocialPost Post(string text, string[] nets, string tags = "", string image = "", DateTime? when = null) =>
        new("p1", when ?? new DateTime(2026, 11, 16, 9, 0, 0), text, SocialPlan.Tags(tags), nets, image);

    // ---------- Social ----------

    [Fact(DisplayName = "SO-01: Netzwerke-Tabelle mit Zeichengrenzen und Bildformaten für alle sieben Netzwerke")]
    public void Social_networks()
    {
        Assert.Equal(["instagram", "facebook", "linkedin", "x", "tiktok", "pinterest", "mastodon"], SocialPlan.Networks.Select(n => n.Id));
        Assert.Equal(280, SocialPlan.Network("x").MaxChars); Assert.Equal(2200, SocialPlan.Network("instagram").MaxChars); Assert.Equal(3000, SocialPlan.Network("linkedin").MaxChars);
        Assert.Equal(500, SocialPlan.Network("mastodon").MaxChars); Assert.Equal(500, SocialPlan.Network("pinterest").MaxChars);
        Assert.Equal((1080, 1350), (SocialPlan.Network("instagram").Formats[0].W, SocialPlan.Network("instagram").Formats[0].H));
        Assert.Equal((1000, 1500), (SocialPlan.Network("pinterest").Formats[0].W, SocialPlan.Network("pinterest").Formats[0].H));
        Assert.All(SocialPlan.Networks, n => Assert.NotEmpty(n.Formats));
    }

    [Fact(DisplayName = "SO-02: Hashtags säubern, Text je Netzwerk mit höchstens erlaubten Hashtags, Zählung wie das Netzwerk (Link = 23, Emoji = 1)")]
    public void Social_compose_and_count()
    {
        Assert.Equal(["#Bäckerei", "#frischesBrot", "#Sonntag"], SocialPlan.Tags("#Bäckerei, frisches-Brot  #bäckerei Sonntag #123"));
        var p = Post("Neu bei uns", ["instagram", "x"], "#a #b #c #d #e #f #g");
        Assert.Equal("Neu bei uns\n\n#a #b #c #d #e", SocialPlan.Compose(p, "instagram"));
        Assert.Equal("Neu bei uns\n\n#a #b #c", SocialPlan.Compose(p, "x"));
        var own = p with { PerNetwork = new Dictionary<string, string> { ["x"] = "Kurz #a" } };
        Assert.Equal("Kurz #a\n\n#b #c", SocialPlan.Compose(own, "x"));
        Assert.Equal(23 + 6, SocialPlan.Length("Hallo https://example.org/ein/sehr/langer/pfad/der/viel/laenger/ist", "x"));
        Assert.Equal(2, SocialPlan.Length("👍🏽!", "instagram"));
    }

    [Fact(DisplayName = "SO-03: Prüfung – zu lang für X ist Fehler, Instagram ohne Bild ist Fehler, Hashtags über Grenze ist Hinweis")]
    public void Social_check()
    {
        var p = Post(new string('a', 300), ["x", "instagram", "linkedin"], "#a #b #c #d #e #f");
        var f = SocialPlan.Check(p);
        Assert.Contains(f, x => x.Network == "X" && x.Error && x.Text.StartsWith("300 von 280"));
        Assert.Contains(f, x => x.Network == "Instagram" && x.Error && x.Text.Contains("Bild"));
        Assert.Contains(f, x => x.Network == "X" && !x.Error && x.Text.Contains("Hashtags"));
        Assert.DoesNotContain(f, x => x.Network == "LinkedIn" && x.Error);
        Assert.Contains(SocialPlan.Check(Post("", [])), x => x.Error && x.Text.Contains("Netzwerk"));
    }

    [Fact(DisplayName = "SO-04: Woche ab Montag, Monat mit 42 Tagen, Warteschlange nimmt die nächste freie feste Zeit")]
    public void Social_calendar_and_queue()
    {
        Assert.Equal(new DateTime(2026, 11, 16), SocialPlan.WeekStart(new DateTime(2026, 11, 22, 15, 0, 0)));
        var g = SocialPlan.MonthGrid(new DateTime(2026, 11, 10));
        Assert.Equal(42, g.Count); Assert.Equal(new DateTime(2026, 10, 26), g[0]); Assert.Equal(DayOfWeek.Monday, g[0].DayOfWeek);
        var slots = SocialPlan.ParseSlots("Mo 09:00, Mi 18:30, Sa 11:00");
        Assert.Equal(3, slots.Count);
        var now = new DateTime(2026, 11, 16, 10, 0, 0); // Montag nach 9 Uhr
        Assert.Equal(new DateTime(2026, 11, 18, 18, 30, 0), SocialPlan.NextSlot([], slots, now));
        var taken = Post("x", ["x"], when: new DateTime(2026, 11, 18, 18, 30, 0));
        Assert.Equal(new DateTime(2026, 11, 21, 11, 0, 0), SocialPlan.NextSlot([taken], slots, now));
        Assert.Throws<FormatException>(() => SocialPlan.ParseSlots("Montag früh"));
    }

    [Fact(DisplayName = "SO-05: CSV je Beitrag und Netzwerk mit Anführungszeichen, ICS mit Erinnerung, gepostete fallen aus dem Kalender")]
    public void Social_csv_ics()
    {
        var a = Post("Hallo, \"Welt\"\nZeile 2", ["instagram", "facebook"], "#a", "bild.jpg");
        var b = Post("Schon da", ["x"]) with { Id = "p2", Status = SocialPlan.Posted, When = new DateTime(2026, 11, 17, 9, 0, 0) };
        var csv = SocialPlan.ToCsv([a, b]);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Datum,Uhrzeit,Netzwerk,Text", lines[0]);
        Assert.Contains("2026-11-16,09:00,Instagram,\"Hallo, \"\"Welt\"\"\nZeile 2\n\n#a\",", csv);
        Assert.Contains("2026-11-17,09:00,X,Schon da,8,,gepostet", csv);
        var ics = SocialPlan.ToIcs([a, b]);
        Assert.Equal(1, ics.Split("\r\n").Count(l => l == "BEGIN:VEVENT"));
        Assert.Contains("TRIGGER:-PT10M", ics); Assert.Contains("SUMMARY:Beitrag posten: Instagram\\, Facebook", ics);
        Assert.Single(Ical.Net.Calendar.Load(ics)!.Events);
    }

    [Fact(DisplayName = "SO-06: Bild füllend zuschneiden (Mitte), Verlust in Prozent, Bildpaket als ZIP mit Bildern je Netzwerk und Texten")]
    public void Social_crop_and_package()
    {
        var src = new Pixmap(400, 200);
        for (var y = 0; y < 200; y++) for (var x = 0; x < 400; x++) { var i = (y * 400 + x) * 4; src.Px[i] = (byte)(x < 200 ? 255 : 0); src.Px[i + 2] = (byte)(x < 200 ? 0 : 255); src.Px[i + 3] = 255; }
        var c = SocialPlan.Crop(src, 100, 100);
        Assert.Equal((100, 100), (c.W, c.H));
        Assert.Equal(255, c.At(10, 50).R); Assert.Equal(255, c.At(90, 50).B); // Mitte des Bildes: links rot, rechts blau
        var left = SocialPlan.Crop(src, 100, 100, fx: 0); Assert.Equal(255, left.At(90, 50).R);
        Assert.Equal(50, SocialPlan.CropLoss(400, 200, 100, 100));
        var dir = Tmp("zip");
        try
        {
            var zip = Path.Combine(dir, "paket.zip");
            var n = SocialPlan.Package([Post("Hallo", ["instagram", "pinterest"], image: "a.png"), Post("Ohne Bild", ["x"]) with { Id = "p2" }], f => f == "a.png" ? src : null, zip);
            Assert.Equal(2, n);
            using var z = ZipFile.OpenRead(zip);
            Assert.Contains(z.Entries, e => e.Name == "2026-11-16_0900_instagram_hoch.png");
            using (var s = z.Entries.First(e => e.Name.EndsWith("_pinterest_pin.png")).Open()) { var ms = new MemoryStream(); s.CopyTo(ms); var px = PngCodec.Decode(ms.ToArray()); Assert.Equal((1000, 1500), (px.W, px.H)); }
            using var t = new StreamReader(z.GetEntry("texte.txt")!.Open());
            var txt = t.ReadToEnd(); Assert.Contains("Ohne Bild", txt); Assert.Contains("· X ==", txt);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "SO-07: KI-Ideen – Anfrage nennt Netzwerke und verbietet Erfundenes, Antwortzeilen werden zu Ideen")]
    public void Social_ideas()
    {
        var q = SocialPlan.IdeaPrompt("Herbst", ["instagram", "linkedin"], 3);
        Assert.Contains("Instagram, LinkedIn", q); Assert.Contains("ohne erfundene", q);
        Assert.Equal(["Kürbisbrot vorstellen", "Blick in die Backstube", "Rezept zum Nachbacken"], SocialPlan.ParseIdeas("Hier sind Ideen:\n1. Kürbisbrot vorstellen\n2) „Blick in die Backstube“\n- Rezept zum Nachbacken\n"));
        Assert.Contains("höchstens 280 Zeichen", SocialPlan.VariantPrompt("Text", "x"));
    }

    // ---------- Newsletter ----------

    static NlDoc News(params NlBlock[] blocks) => new("Neuigkeiten im November", "Kurz vorab", "newsletter@example.org", "#B45309",
        blocks.Length > 0 ? blocks : [new("k", "kopf", Title: "Hallo"), new("t", "text", Text: "Ein **fetter** Satz mit [Link](https://example.org) und <script>."), new("b", "knopf", Label: "Los", Url: "https://example.org"),
            new("f", "fuss", Text: "Erika Muster\nHauptstraße 1, 12345 Musterstadt\nkontakt@example.org")]);

    [Fact(DisplayName = "NL-01: E-Mail-HTML mit Tabellen, Inline-CSS und Breite 600, ohne externe Stylesheets; Text maskiert, javascript-Links fallen weg")]
    public void News_html()
    {
        var h = NewsletterKit.Html(News());
        Assert.Contains("<table role=\"presentation\" class=\"nl-wrap\" width=\"600\"", h);
        Assert.DoesNotContain("<link", h); Assert.DoesNotContain("<script>", h); Assert.Contains("&lt;script&gt;", h);
        Assert.Contains("<strong>fetter</strong>", h); Assert.Contains("<a href=\"https://example.org\" style=\"color:#B45309", h);
        Assert.Contains("Newsletter abbestellen", h); Assert.Contains(NewsletterKit.Unsub, h);
        Assert.Contains("display:none", h); // Vorschautext
        Assert.Contains("max-width:620px", h); // Handy: volle Breite
        Assert.DoesNotContain("href=\"javascript", NewsletterKit.Html(News(new NlBlock("t", "text", Text: "[x](javascript:alert(1))"), new NlBlock("f", "fuss"))));
        var t = NewsletterKit.Text(News());
        Assert.Contains("Ein fetter Satz mit Link (https://example.org)", t); Assert.Contains("Los: https://example.org", t); Assert.Contains("Abbestellen: {{abmelden}}", t);
    }

    [Fact(DisplayName = "NL-02: Prüfung – Fußzeile, Impressum, Alt-Text, Knopf-Link, Spam-Wörter, Großbuchstaben, Bildgröße, 102 KB")]
    public void News_check()
    {
        Assert.DoesNotContain(NewsletterKit.Check(News()), f => f.Error);
        var bad = new NlDoc("JETZT KAUFEN!!! 100% KOSTENLOS", "", "keine-mail", "#000000", [new("b", "bild", Src: "a.jpg", Width: 600), new("k", "knopf", Label: "Los", Url: "http://example.org")]);
        var f = NewsletterKit.Check(bad, _ => (2_000_000, 400, 300));
        Assert.Contains(f, x => x.Error && x.Text.Contains("Fußzeile"));
        Assert.Contains(f, x => x.Error && x.Text.Contains("Alt-Text"));
        Assert.Contains(f, x => x.Error && x.Text.Contains("https://"));
        Assert.Contains(f, x => x.Error && x.Text.Contains("Absender"));
        Assert.Contains(f, x => !x.Error && x.Text.Contains("„100% kostenlos“"));
        Assert.Contains(f, x => x.Text.Contains("Großbuchstaben")); Assert.Contains(f, x => x.Text.Contains("Ausrufe"));
        Assert.Contains(f, x => x.Text.Contains("1953 KB")); Assert.Contains(f, x => x.Text.Contains("nur 400 px"));
        var imp = NewsletterKit.Check(News(new NlBlock("f", "fuss", Text: "Erika Muster")));
        Assert.Contains(imp, x => x.Error && x.Text.Contains("Impressum"));
        var big = NewsletterKit.Check(News(new NlBlock("t", "text", Text: new string('x', 110_000)), new NlBlock("f", "fuss", Text: "Erika Muster, Hauptstraße 1, 12345 Musterstadt, a@example.org")));
        Assert.Contains(big, x => x.Error && x.Text.Contains("Gmail"));
    }

    [Fact(DisplayName = "NL-03: Empfänger mit Double-Opt-in – neu unbestätigt, doppelt/ungültig abgelehnt, abgemeldet nicht zurück auf bestätigt, CSV ein/aus")]
    public void News_recipients()
    {
        var now = new DateTime(2026, 11, 16);
        var l = NewsletterKit.Add([], "Erika@Example.org", "Erika", now);
        Assert.Equal(NewsletterKit.Pending, l[0].Status); Assert.Equal("erika@example.org", l[0].Email);
        Assert.Throws<ArgumentException>(() => NewsletterKit.Add(l, "erika@example.org", "", now));
        Assert.Throws<ArgumentException>(() => NewsletterKit.Add(l, "kein-at", "", now));
        l = NewsletterKit.SetStatus(l, "erika@example.org", NewsletterKit.Confirmed, now);
        Assert.Equal(1, NewsletterKit.Sendable(l));
        l = NewsletterKit.SetStatus(l, "erika@example.org", NewsletterKit.Unsubscribed, now);
        Assert.Throws<InvalidOperationException>(() => NewsletterKit.SetStatus(l, "erika@example.org", NewsletterKit.Confirmed, now));
        var (l2, add, skip) = NewsletterKit.ImportCsv(l, "E-Mail;Name\nmax@example.org;Max\nerika@example.org;Erika\nkaputt@\n", now);
        Assert.Equal((1, 2), (add, skip)); Assert.Equal(2, l2.Count); Assert.Equal(0, NewsletterKit.Sendable(l2));
        Assert.Contains("max@example.org;Max;unbestätigt;2026-11-16", NewsletterKit.ExportCsv(l2));
        Assert.Contains("{{bestaetigen}}", NewsletterKit.OptInText("Bäckerei Muster"));
    }

    [Fact(DisplayName = "NL-04: EML-Entwurf mit Text- und HTML-Teil und eingebettetem Bild, als Entwurf markiert; nur eine Adresse erlaubt")]
    public void News_eml()
    {
        var dir = Tmp("eml");
        try
        {
            var img = Path.Combine(dir, "foto.png"); File.WriteAllBytes(img, PngCodec.Encode(new Pixmap(4, 4)));
            var doc = News(new NlBlock("k", "kopf", Title: "Hallo"), new NlBlock("b", "bild", Src: img, Alt: "Foto"), new NlBlock("t", "text", Text: "Grüße aus der Backstube"), new NlBlock("f", "fuss", Text: "x"));
            var eml = NewsletterKit.Eml(doc, "ich@example.org", new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero), File.ReadAllBytes);
            Assert.StartsWith("X-Unsent: 1\r\n", eml); Assert.Contains("To: ich@example.org", eml);
            Assert.Contains("multipart/alternative", eml); Assert.Contains("Content-ID: <bild1@newsletter>", eml);
            var parsed = Trendo.Core.MailCenter.ParseEml(eml);
            Assert.Equal("Neuigkeiten im November", parsed.Subject); Assert.Contains("Grüße aus der Backstube", parsed.Text);
            var htmlPart = Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(eml.Split("Content-Type: text/html; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n")[1].Split("--")[0].Split("\r\n"))));
            Assert.Contains("src=\"cid:bild1@newsletter\"", htmlPart);
            Assert.Throws<ArgumentException>(() => NewsletterKit.Eml(doc, "a@example.org, b@example.org", DateTimeOffset.Now));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---------- Flyer ----------

    [Fact(DisplayName = "FL-01: Formate mit Datenformat (Endformat + 2 × 3 mm), Faltflyer mit zwei Falzen, wirksame Auflösung")]
    public void Flyer_formats()
    {
        Assert.Equal((105, 216), FlyerKit.DataSize(new FlyerDoc("dinlang", "", "", [])));
        Assert.Equal((91, 61), FlyerKit.DataSize(new FlyerDoc("visitenkarte", "", "", [])));
        Assert.Equal((154, 216), FlyerKit.DataSize(new FlyerDoc("a5", "", "", [])));
        Assert.Equal([99, 198], FlyerKit.Folds(FlyerKit.Format("falt-a4")));
        Assert.Equal(300, FlyerKit.EffectiveDpi(1181, 100));
        Assert.Throws<ArgumentException>(() => FlyerKit.Format("a2"));
    }

    [Fact(DisplayName = "FL-02: Prüfung – Text im Randbereich, Bild unter 150 dpi, Fläche endet am Schnitt, QR zu lang/zu klein, Text über Falz")]
    public void Flyer_check()
    {
        var items = new List<FlyerItem>
        {
            new("t1", 0, "text", 1, 10, 40, 10, "Zu nah am Rand"),
            new("t2", 0, "text", 10, 30, 40, 10, "Gut"),
            new("i1", 0, "image", 10, 50, 100, 60, Src: "klein.jpg"),
            new("r1", 0, "rect", 0, 0, 148, 20, Fill: "#000000"),
            new("r2", 0, "rect", -3, 150, 154, 63, Fill: "#000000"),
            new("q1", 0, "qr", 20, 120, 10, 10, new string('x', 300)),
        };
        var f = FlyerKit.Check(new FlyerDoc("a5", "#FFFFFF", "#FFFFFF", items), s => s == "klein.jpg" ? (500, 300) : null);
        Assert.Contains(f, x => x.ItemId == "t1" && x.Error && x.Text.Contains("Randbereich"));
        Assert.DoesNotContain(f, x => x.ItemId == "t2");
        Assert.Contains(f, x => x.ItemId == "i1" && x.Error && x.Text.Contains("127 dpi"));
        Assert.Contains(f, x => x.ItemId == "r1" && x.Text.Contains("Beschnitt"));
        Assert.DoesNotContain(f, x => x.ItemId == "r2");
        Assert.Contains(f, x => x.ItemId == "q1" && x.Error && x.Text.Contains("zu lang"));
        Assert.Contains(f, x => x.ItemId == "q1" && x.Text.Contains("15 mm"));
        var fold = FlyerKit.Check(new FlyerDoc("falt-a4", "", "", [new("t", 0, "text", 80, 20, 40, 10, "Über die Falz")]));
        Assert.Contains(fold, x => x.Text.Contains("Falz bei 99"));
        Assert.Contains(FlyerKit.Check(new FlyerDoc("a5", "", "", [], 1)), x => x.Error && x.Text.Contains("3 mm"));
        Assert.Contains(FlyerKit.Check(new FlyerDoc("a5", "", "", [new("t", 0, "text", 10, 10, 20, 5, "Ein sehr langer Text, der nie und nimmer in dieses kleine Feld passt", 12)])), x => x.Text.Contains("passt vermutlich nicht"));
    }

    [Fact(DisplayName = "FL-03: Druck-PDF – Seite = Datenformat, TrimBox = Endformat, Rückseite als zweite Seite, mit Schnittmarken 10 mm größer")]
    public void Flyer_pdf()
    {
        var dir = Tmp("pdf");
        try
        {
            var img = Path.Combine(dir, "bild.png"); File.WriteAllBytes(img, PngCodec.Encode(new Pixmap(50, 30)));
            var doc = new FlyerDoc("visitenkarte", "#FFFFFF", "#1F2937", [
                new("a", 0, "rect", -3, -3, 30, 61, Fill: "#B45309"), new("b", 0, "text", 30, 10, 50, 10, "Erika Muster", 12, Bold: true),
                new("c", 0, "image", 30, 25, 20, 12, Src: img), new("d", 1, "qr", 58, 16, 22, 22, "https://example.org")]);
            var f = Path.Combine(dir, "karte.pdf");
            Assert.Equal(2, FlyerKit.Pdf(doc, f, false, File.ReadAllBytes));
            using (var pdf = PdfReader.Open(f, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(2, pdf.PageCount);
                Assert.Equal(91, Math.Round(pdf.Pages[0].Width.Millimeter, 1)); Assert.Equal(61, Math.Round(pdf.Pages[0].Height.Millimeter, 1));
                Assert.Equal(85, Math.Round(pdf.Pages[0].TrimBox.Width / FlyerKit.MmPt, 1));
            }
            var m = Path.Combine(dir, "marken.pdf");
            Assert.Equal(1, FlyerKit.Pdf(doc with { Items = [.. doc.Items.Where(i => i.Side == 0)], BackBackground = "#FFFFFF" }, m, true, File.ReadAllBytes));
            using (var pdf = PdfReader.Open(m, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(111, Math.Round(pdf.Pages[0].Width.Millimeter, 1));
                Assert.Equal(91, Math.Round(pdf.Pages[0].BleedBox.Width / FlyerKit.MmPt, 1));
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "FL-04: QR-Code aus dem vorhandenen Kodierer – Kontakt-QR (MECARD) mit Sonderzeichen passt in einen kleinen Code")]
    public void Flyer_qr()
    {
        var v = FlyerKit.ContactQr("Erika Muster", "+49 1234 56789", "kontakt@example.org", "https://example.org");
        Assert.Equal("MECARD:N:Erika Muster;TEL:+49 1234 56789;EMAIL:kontakt@example.org;URL:https\\://example.org;;", v);
        var m = QrMatrix.Encode(v); Assert.True(m.GetLength(0) >= 21);
        Assert.Contains("N:A\\;B", FlyerKit.ContactQr("A;B", "", "", ""));
    }

    // ---------- Verkaufsseiten ----------

    static LpDoc Page(string buy = "https://buy.example.org/x", string imp = "Erika Muster\nHauptstraße 1\n12345 Musterstadt\nkontakt@example.org") => new("Seife <Test>", "Milde Seife", "#B45309", buy, imp,
        "Diese Seite speichert keine Cookies und lädt keine fremden Inhalte. Verantwortlich: siehe Impressum.",
        [new("h", "held", "Handgemachte Seife", "Mild", "foto.jpg", "Seifenstück", "Jetzt kaufen"), new("n", "nutzen", "Darum", Items: "Ohne Palmöl | Nur Olivenöl\nPlastikfrei | Papier"),
         new("p", "preis", "Preis", Price: "8,90 €", Unit: "je Stück", Items: "Handgesiedet"), new("r", "bewertungen", Items: "Max | Toll | 4"), new("f", "faq", Items: "Vegan? | Ja.")]);

    [Fact(DisplayName = "VS-01: Statische Seite ohne Skripte, mit viewport, Beschreibung, Impressum-/Datenschutz-Link, maskiertem Text und Kauf-Link")]
    public void Landing_html()
    {
        var h = LandingKit.Index(Page());
        Assert.DoesNotContain("<script", h); Assert.Contains("name=\"viewport\"", h); Assert.Contains("content=\"Milde Seife\"", h);
        Assert.Contains("href=\"impressum.html\"", h); Assert.Contains("href=\"datenschutz.html\"", h);
        Assert.Contains("Seife &lt;Test&gt;", h); Assert.Contains("href=\"https://buy.example.org/x\" rel=\"noopener\"", h);
        Assert.Contains("<span class=\"now\">8,90 €</span>", h); Assert.Contains("inkl. MwSt.", h);
        Assert.Contains("★★★★☆", h); Assert.Contains("<details><summary>Vegan?</summary>", h);
        Assert.Contains("src=\"bilder/foto.jpg\"", h);
        var unsafeLink = LandingKit.Index(Page("javascript:alert(1)"));
        Assert.DoesNotContain("javascript:", unsafeLink); Assert.Contains("aria-disabled", unsafeLink);
        Assert.Contains("§ 19 UStG", LandingKit.Index(Page() with { SmallBusiness = true }));
    }

    [Fact(DisplayName = "VS-02: Pflichtprüfung – Impressum mit Anschrift/E-Mail, Datenschutz, Kauf-Link mit https, Streichpreis-Hinweis, echte Bewertungen, Bild fehlt")]
    public void Landing_check()
    {
        Assert.DoesNotContain(LandingKit.Check(Page()), f => f.Error);
        var f = LandingKit.Check(Page("http://x.example.org", "Erika Muster") with { Privacy = "" }, _ => null);
        Assert.Contains(f, x => x.Error && x.Text.Contains("Impressum fehlen"));
        Assert.Contains(f, x => x.Error && x.Text.Contains("Datenschutz"));
        Assert.Contains(f, x => x.Error && x.Text.Contains("https://"));
        Assert.Contains(f, x => x.Error && x.Text.Contains("nicht gefunden"));
        Assert.Contains(f, x => !x.Error && x.Text.Contains("UWG"));
        Assert.Contains(LandingKit.Check(Page() with { Blocks = [new("p", "preis", Price: "9 €", OldPrice: "12 €")] }), x => x.Text.Contains("30 Tage"));
        Assert.Contains(LandingKit.Check(Page("")), x => x.Error && x.Text.Contains("Kauf-Link"));
    }

    [Fact(DisplayName = "VS-03: Export als Ordner – index, impressum, datenschutz, style.css, Bild kopiert; Seitengewicht")]
    public void Landing_write()
    {
        var dir = Tmp("lp");
        try
        {
            var img = Path.Combine(dir, "foto.jpg"); File.WriteAllBytes(img, new byte[2048]);
            var doc = Page() with { Blocks = [new("h", "held", "Seife", Src: img, Alt: "Seife")] };
            var out1 = Path.Combine(dir, "seite");
            var index = LandingKit.Write(doc, out1, File.ReadAllBytes);
            Assert.True(File.Exists(index));
            foreach (var n in new[] { "impressum.html", "datenschutz.html", "style.css", Path.Combine("bilder", "foto.jpg") }) Assert.True(File.Exists(Path.Combine(out1, n)), n);
            Assert.Contains("12345 Musterstadt", File.ReadAllText(Path.Combine(out1, "impressum.html")));
            Assert.Contains("@media(min-width:720px)", File.ReadAllText(Path.Combine(out1, "style.css")));
            Assert.True(LandingKit.Weight(doc, f => new FileInfo(f).Length) > 2048);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---------- Adapter ----------

    static string RunAdapter(string studio, string prompt, string file)
    {
        var root = Tmp("ad");
        try
        {
            var all = PromptActions.CreateDefault();
            var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
            var project = core.Projects.Create("probe", "Probe").Id;
            var wf = new StudioWorkflow(core, all);
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("biz-1", project, studio, prompt, [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            return File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith(file)).RelativePath));
        }
        finally { Directory.Delete(root, true); }
    }

    static IStudioActionAdapter Ad(string studio)
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == studio);
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level(studio, all));
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Single(all, a => a.ActionId == ad.ActionId);
        return ad;
    }

    [Fact(DisplayName = "STUDIO-SOCIAL-01 Adapter: Plan als CSV mit Zeichen je Netzwerk, ohne Nebenwirkung, Pflichtfeld Text")]
    public void Social_adapter()
    {
        var ad = Ad("STUDIO-SOCIAL-01");
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["text"] = "x", ["netzwerke"] = "myspace" }));
        var csv = RunAdapter("STUDIO-SOCIAL-01", "Text: Frische Zimtschnecken ab 7 Uhr\nNetzwerke: facebook, twitter\nHashtags: #Frühstück\nZeit: 21.11.2026 08:00", "redaktionsplan.csv");
        Assert.Contains("2026-11-21,08:00,Facebook,\"Frische Zimtschnecken ab 7 Uhr\n\n#Frühstück\",47", csv);
        Assert.Contains("2026-11-21,08:00,X,", csv);
    }

    [Fact(DisplayName = "STUDIO-NEWSLETTER-01 Adapter: E-Mail-HTML mit Knopf und Fußzeile, ohne Versand")]
    public void Newsletter_adapter()
    {
        var ad = Ad("STUDIO-NEWSLETTER-01");
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["betreff"] = "x", ["text"] = "y", ["link"] = "http://x" }));
        var html = RunAdapter("STUDIO-NEWSLETTER-01", "Betreff: Neue Öffnungszeiten\nText: Ab Montag bis 19 Uhr.\nLink: https://example.org\nImpressum: Erika Muster, Hauptstraße 1, 12345 Musterstadt, kontakt@example.org", "newsletter.html");
        Assert.Contains("width=\"600\"", html); Assert.Contains("Ab Montag bis 19 Uhr.", html); Assert.Contains("href=\"https://example.org\"", html); Assert.Contains("12345 Musterstadt", html);
    }

    [Fact(DisplayName = "STUDIO-FLYER-01 Adapter: Druckvorgabe mit Datenformat und Falz, ohne Bestellung")]
    public void Flyer_adapter()
    {
        var ad = Ad("STUDIO-FLYER-01");
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["titel"] = "x", ["format"] = "a2" }));
        var t = RunAdapter("STUDIO-FLYER-01", "Titel: Sommerfest\nFormat: falt-a4\nQR: https://example.org", "druckvorgabe.txt");
        Assert.Contains("Datenformat mit 3 mm Beschnitt: 303 × 216 mm", t); Assert.Contains("Falzlinien bei 99 mm und 198 mm", t); Assert.Contains("QR-Code: https://example.org", t);
    }

    [Fact(DisplayName = "STUDIO-LANDING-01 Adapter: Verkaufsseite als HTML ohne Skripte, Pflichtfelder Produkt und Preis")]
    public void Landing_adapter()
    {
        var ad = Ad("STUDIO-LANDING-01");
        Assert.Equal(2, ad.Missing(new Dictionary<string, string>()).Count);
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["produkt"] = "x", ["preis"] = "1", ["link"] = "ftp://x" }));
        var h = RunAdapter("STUDIO-LANDING-01", "Produkt: Handgemachte Seife\nPreis: 8,90 €\nNutzen: Ohne Palmöl; Plastikfrei\nLink: https://buy.example.org/seife", "index.html");
        Assert.Contains("<h1>Handgemachte Seife</h1>", h); Assert.Contains("<h3>Ohne Palmöl</h3>", h); Assert.Contains("8,90 €", h); Assert.DoesNotContain("<script", h);
    }
}
