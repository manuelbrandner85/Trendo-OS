using System.Text;
using Trendo.Core;
using Trendo.TestMail;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0689: Mailprogramm gegen einen echten lokalen Mailserver (<see cref="MailTestServer"/>, IMAP + SMTP über Loopback, wahlweise TLS).
/// Die Protokollschicht von Trendo (<see cref="ImapSession"/>, <see cref="SmtpSession"/>, <see cref="MailMime"/>) läuft unverändert – nichts ist ersetzt.</summary>
public class MailRoundtripTests
{
    static CancellationToken Ct => new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

    static async Task<ImapSession> Login(MailTestServer srv)
    {
        var imap = new ImapSession(await srv.ConnectImap(Ct));
        await imap.GreetingAsync(Ct); await imap.LoginAsync(srv.User, srv.Password, Ct); await imap.CapabilityAsync(Ct);
        return imap;
    }

    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-mailrt-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0689 Rundreise über TLS: senden (STARTTLS, Anhänge, Umlaute, Blindkopie) → abrufen → Anhang bytegleich; gelesen, Stern, verschieben, suchen, Papierkorb, endgültig löschen, ablegen")]
    public async Task Send_fetch_roundtrip_tls()
    {
        using var srv = new MailTestServer(tls: true);
        var dir = Temp();
        try
        {
            var bin = new byte[150_000]; new Random(4711).NextBytes(bin);
            var f1 = Path.Combine(dir, "Übersicht März – Größe.bin"); File.WriteAllBytes(f1, bin);
            var f2 = Path.Combine(dir, "angebot.pdf"); File.WriteAllBytes(f2, Encoding.ASCII.GetBytes("%PDF-1.4\n.\n..zwei Punkte\n%%EOF\n"));
            var acc = new MailAccount(srv.User, "Erika Müßig", "localhost", srv.ImapPort, "localhost", srv.SmtpPort, false, srv.User);
            var subject = "Größere Übersicht für März: Zähler, Maße und Öffnungszeiten – bitte bis Freitag prüfen und zurückschicken 😀";
            var body = "Hallo Jörg,\n.\n..anbei die Übersicht – „mit Anführungszeichen“ und 12,50 €.\nFrom hier geht es weiter.\n\nViele Grüße\nErika";
            var draft = new MailDraft($"Jörg Größe <{srv.User}>", subject, body, "kopie@example.com", [f1, f2]) { Bcc = "geheim@example.net", Html = MailMime.TextToHtml(body) };
            var raw = MailMime.Build(acc, draft, new DateTimeOffset(2026, 11, 16, 9, 30, 0, TimeSpan.FromHours(1)));
            Assert.All(Encoding.ASCII.GetString(raw).Split("\r\n"), l => Assert.True(l.Length <= 78, "Zeile zu lang: " + l));
            Assert.DoesNotContain("geheim@example.net", Encoding.ASCII.GetString(raw)); // Blindkopie steht in keiner Kopfzeile

            // Senden: STARTTLS wird wirklich ausgehandelt, erst danach geht das Passwort über die Leitung
            var smtp = new SmtpSession(await srv.ConnectSmtp(Ct), srv.ClientTls);
            await smtp.SendRawAsync(acc.User, srv.Password, acc.Email, SmtpSession.Recipients(draft), raw, Ct);
            var got = Assert.Single(srv.Accepted);
            Assert.Equal([srv.User, "kopie@example.com", "geheim@example.net"], got.To);
            Assert.Equal(raw, got.Raw); // bytegleich angekommen (Punkt-Verdopplung hin und zurück)
            Assert.Equal("STARTTLS", srv.SmtpLog[1]);
            Assert.DoesNotContain(srv.SmtpLog, l => l.Contains(srv.Password));

            // Abrufen über TLS
            var imap = await Login(srv);
            var folders = await imap.FoldersAsync(Ct);
            Assert.Equal(["Posteingang", "Entwürfe", "Gesendet", "Spam", "Papierkorb", "Rechnungen"], folders.Select(f => f.Label));
            Assert.Equal("Entwürfe", folders.Single(f => f.Role == "drafts").Path);
            var n = await imap.ExamineAsync("INBOX", Ct);
            Assert.Equal(1, n);
            var h = Assert.Single(await imap.HeadersPageAsync(n, 0, 50, Ct));
            Assert.Equal((subject, false, true), (h.Subject, h.Seen, h.HasAttachment));
            Assert.Equal("Erika Müßig <erika@example.org>", h.From);
            Assert.Equal(new DateTimeOffset(2026, 11, 16, 9, 30, 0, TimeSpan.FromHours(1)), h.Date);
            var back = await imap.RawAsync(h.Uid, Ct);
            Assert.Equal(raw, back);
            var mail = MailMime.Parse(back);
            Assert.Equal(body, mail.Body);
            Assert.Contains("<p>Hallo Jörg,", mail.Html);
            Assert.Equal(["Übersicht März – Größe.bin", "angebot.pdf"], mail.Attachments.Select(a => a.Name));
            Assert.Equal(bin, mail.Attachments[0].Data);
            Assert.Equal(File.ReadAllBytes(f2), mail.Attachments[1].Data);
            Assert.Equal("application/pdf", mail.Attachments[1].ContentType);
            Assert.Contains("kopie@example.com", mail.Cc);
            Assert.False(srv.Get("INBOX").Msgs[0].Flags.Contains("\\Seen")); // Lesen allein ändert nichts
            // nur lesend geöffnet: Ändern wird vom Server abgelehnt
            await Assert.ThrowsAsync<InvalidOperationException>(() => imap.StoreAsync([h.Uid], "\\Seen", true, Ct));

            // gelesen + Stern
            await imap.SelectAsync("INBOX", Ct);
            await imap.StoreAsync([h.Uid], "\\Seen", true, Ct); await imap.StoreAsync([h.Uid], "\\Flagged", true, Ct);
            Assert.Subset(srv.Get("INBOX").Msgs[0].Flags, new HashSet<string> { "\\Seen", "\\Flagged" });
            await imap.StoreAsync([h.Uid], "\\Seen", false, Ct);
            Assert.DoesNotContain("\\Seen", srv.Get("INBOX").Msgs[0].Flags);
            // suchen (Umlaut als Literal, Text steht Base64-kodiert in der Nachricht)
            Assert.Equal([h.Uid], await imap.SearchAsync("Öffnungszeiten", Ct));
            Assert.Equal([h.Uid], await imap.SearchAsync("Anführungszeichen", Ct));
            Assert.Empty(await imap.SearchAsync("gibt es nicht", Ct));
            Assert.Equal([h.Uid], await imap.SearchMessageIdAsync(mail.MessageId, Ct));
            // verschieben
            await imap.MoveAsync([h.Uid], "Rechnungen", Ct);
            Assert.Empty(srv.Get("INBOX").Msgs); Assert.Single(srv.Get("Rechnungen").Msgs);
            // löschen: erst Papierkorb, dann endgültig
            Assert.Equal(1, await imap.SelectAsync("Rechnungen", Ct));
            var moved = Assert.Single(await imap.HeadersPageAsync(1, 0, 50, Ct));
            Assert.True(moved.Flagged);
            await imap.MoveAsync([moved.Uid], "Papierkorb", Ct);
            Assert.Empty(srv.Get("Rechnungen").Msgs); Assert.Single(srv.Get("Papierkorb").Msgs);
            await imap.SelectAsync("Papierkorb", Ct);
            await imap.DeleteForeverAsync([srv.Get("Papierkorb").Msgs[0].Uid], Ct);
            Assert.Empty(srv.Get("Papierkorb").Msgs);
            // Kopie in „Gesendet“ und Entwurf in „Entwürfe“ ablegen (Ordnername mit Umlaut geht kodiert über die Leitung)
            await imap.AppendAsync("Gesendet", raw, "\\Seen", Ct);
            await imap.AppendAsync("Entwürfe", raw, "\\Draft \\Seen", Ct);
            Assert.Equal(raw, Assert.Single(srv.Get("Gesendet").Msgs).Raw);
            Assert.Contains("\\Draft", Assert.Single(srv.Get("Entw&APw-rfe").Msgs).Flags);
            await imap.LogoutAsync(Ct);
            Assert.DoesNotContain(srv.Password, imap.Log);
            Assert.Contains(srv.ImapLog, l => l.Contains("APPEND Entw&APw-rfe"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0689 Seitenweise: 120 Nachrichten in Seiten zu 50 (neueste zuerst), Nachladen ohne Lücke und ohne Doppelte")]
    public async Task Paging()
    {
        using var srv = new MailTestServer();
        for (var i = 1; i <= 120; i++) srv.Deliver("INBOX", $"From: a@example.com\r\nTo: {srv.User}\r\nSubject: Nachricht {i}\r\nDate: Mon, 16 Nov 2026 10:00:00 +0100\r\n\r\nText {i}\r\n");
        var imap = await Login(srv);
        var n = await imap.ExamineAsync("INBOX", Ct);
        Assert.Equal(120, n);
        var p1 = await imap.HeadersPageAsync(n, 0, 50, Ct); var p2 = await imap.HeadersPageAsync(n, 50, 50, Ct); var p3 = await imap.HeadersPageAsync(n, 100, 50, Ct);
        Assert.Equal((50, 50, 20), (p1.Count, p2.Count, p3.Count));
        Assert.Equal(("Nachricht 120", "Nachricht 71", "Nachricht 70", "Nachricht 1"), (p1[0].Subject, p1[^1].Subject, p2[0].Subject, p3[^1].Subject));
        Assert.Equal(120, p1.Concat(p2).Concat(p3).Select(h => h.Uid).Distinct().Count());
        Assert.Empty(await imap.HeadersPageAsync(n, 120, 50, Ct));
        var some = await imap.HeadersByUidAsync([3, 77, 119], Ct);
        Assert.Equal(["Nachricht 119", "Nachricht 77", "Nachricht 3"], some.Select(h => h.Subject));
        await imap.LogoutAsync(Ct);
    }

    [Fact(DisplayName = "T-0689 Ohne MOVE/UIDPLUS: Verschieben über Kopieren + Löschen; falsches Passwort wird abgelehnt und steht in keinem Protokoll")]
    public async Task Fallback_and_wrong_password()
    {
        using var srv = new MailTestServer { SupportsMove = false, SupportsUidPlus = false };
        var uid = srv.Deliver("INBOX", "From: a@example.com\r\nSubject: Eins\r\n\r\nx\r\n"); srv.Deliver("INBOX", "From: a@example.com\r\nSubject: Zwei\r\n\r\ny\r\n");
        var imap = await Login(srv);
        Assert.DoesNotContain("MOVE", imap.Caps);
        await imap.SelectAsync("INBOX", Ct);
        await imap.MoveAsync([uid], "Papierkorb", Ct);
        Assert.Equal("Zwei", MailMime.Parse(Assert.Single(srv.Get("INBOX").Msgs).Raw).Subject);
        Assert.Single(srv.Get("Papierkorb").Msgs);
        Assert.Contains(srv.ImapLog, l => l.StartsWith("UID COPY")); Assert.Contains(srv.ImapLog, l => l.StartsWith("EXPUNGE"));
        await imap.LogoutAsync(Ct);

        var bad = new ImapSession(await srv.ConnectImap(Ct)); await bad.GreetingAsync(Ct);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bad.LoginAsync(srv.User, "falsch-Geheim9", Ct));
        Assert.Contains("Anmeldung abgelehnt", ex.Message); Assert.DoesNotContain("falsch-Geheim9", ex.Message + bad.Log);
        var smtp = new SmtpSession(await srv.ConnectSmtp(Ct));
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => smtp.SendRawAsync(srv.User, "falsch-Geheim9", srv.User, [srv.User], "Subject: x\r\n\r\ny"u8.ToArray(), Ct));
        Assert.Contains("Anmeldung abgelehnt", ex2.Message); Assert.Empty(srv.Accepted);
    }

    [Fact(DisplayName = "T-0689 TLS-Pflicht beim Senden: bietet der Server kein STARTTLS, bricht Trendo ab, bevor das Passwort gesendet wird")]
    public async Task Smtp_requires_tls()
    {
        using var srv = new MailTestServer(); // ohne Zertifikat: kein STARTTLS im Angebot
        var smtp = new SmtpSession(await srv.ConnectSmtp(Ct), s => Task.FromResult(s));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => smtp.SendRawAsync(srv.User, srv.Password, srv.User, [srv.User], "Subject: x\r\n\r\ny"u8.ToArray(), Ct));
        Assert.Contains("keine Verschlüsselung", ex.Message);
        Assert.DoesNotContain(srv.SmtpLog, l => l.StartsWith("AUTH"));
        Assert.Empty(srv.Accepted);
    }

    [Fact(DisplayName = "T-0689 MIME schreiben: lange Betreffzeile mit Umlauten gefaltet (≤ 76 je Zeile) und verlustfrei; Namen in Adressen kodiert; Ordnernamen (UTF-7) hin und zurück")]
    public void Mime_write()
    {
        var subject = string.Concat(Enumerable.Repeat("Äpfel, Öl und Übergrößen – ", 9)) + "Ende 🍎";
        var enc = MailMime.EncodeWord(subject, 9);
        Assert.All(enc.Split("\r\n"), l => Assert.True(l.Length <= 76));
        Assert.Equal(subject, MailMime.DecodeWords(enc.Replace("\r\n ", " ")));
        Assert.Equal("Kurz und ASCII", MailMime.EncodeWord("Kurz und ASCII"));
        var list = MailMime.AddressList("Jörg Müller <j@example.com>; \"Muster, Erika\" <e@example.org>, nur@example.net");
        Assert.DoesNotContain("ö", list); Assert.Contains("<j@example.com>", list); Assert.Contains("\"Muster, Erika\" <e@example.org>", list); Assert.EndsWith("nur@example.net", list);
        var d = new MailDraft("\"Muster, Erika\" <e@example.org>; j@example.com", "x", "y") { Bcc = "b@example.net, e@example.org" };
        Assert.Equal(["e@example.org", "j@example.com", "b@example.net"], SmtpSession.Recipients(d));
        foreach (var name in new[] { "INBOX", "Entwürfe", "Gelöschte Elemente", "A & B", "日本語/下書き" }) Assert.Equal(name, ImapUtf7.Decode(ImapUtf7.Encode(name)));
        Assert.Equal("Entw&APw-rfe", ImapUtf7.Encode("Entwürfe")); Assert.Equal("A &- B", ImapUtf7.Encode("A & B"));
        // Antwort: Bezug auf die Ursprungsnachricht steht in den Kopfzeilen
        var acc = MailAccount.Guess("erika@example.org", "Erika Muster");
        var reply = Encoding.ASCII.GetString(MailMime.Build(acc, new MailDraft("a@example.com", "AW: Hallo", "ok") { InReplyTo = "<1@example.com>", References = "<0@example.com> <1@example.com>" }, DateTimeOffset.Now));
        Assert.Contains("In-Reply-To: <1@example.com>\r\n", reply); Assert.Contains("References: <0@example.com>\r\n <1@example.com>\r\n", reply);
        Assert.Contains("Content-Type: text/plain; charset=utf-8", reply); Assert.DoesNotContain("multipart", reply);
        Assert.Throws<InvalidOperationException>(() => MailMime.Build(acc, new MailDraft("a@example.com", "x", "y", "", [Path.Combine(Path.GetTempPath(), "gibt-es-nicht-" + Guid.NewGuid().ToString("N") + ".pdf")]), DateTimeOffset.Now));
    }

    [Fact(DisplayName = "T-0689 MIME lesen: ISO-8859-1/Windows-1252 (Quoted-Printable und 8bit), RFC-2231-Dateiname, geteilte kodierte Wörter, Pfad im Anhangsnamen entschärft, eingebettetes Bild")]
    public void Mime_read_foreign()
    {
        var latin = Encoding.Latin1;
        var raw = latin.GetBytes("From: =?ISO-8859-1?Q?J=F6rg_M=FCller?= <j@example.com>\r\nTo: e@example.org\r\nSubject: =?ISO-8859-1?Q?Gr=FC=DFe?=\r\n =?ISO-8859-1?Q?_aus_K=F6ln?=\r\nDate: Mon, 16 Nov 2026 08:00:00 +0100 (CET)\r\nMessage-ID: <abc@example.com>\r\n" +
            "Content-Type: multipart/mixed;\r\n boundary=\"=_g1\"\r\n\r\nVorspann\r\n--=_g1\r\nContent-Type: text/plain; charset=ISO-8859-1\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nSch=F6ne Gr=FC=DFe, das kostet 5 =80 =\r\nmehr.\r\n" +
            "--=_g1\r\nContent-Type: application/octet-stream\r\nContent-Disposition: attachment;\r\n filename*0*=UTF-8''%C3%9Cbersicht%20;\r\n filename*1*=M%C3%A4rz.txt\r\nContent-Transfer-Encoding: 8bit\r\n\r\nInhalt mit \u00e4 roh\r\n" +
            "--=_g1\r\nContent-Type: text/plain; name=\"..\\..\\Autostart\\b\u00f6se.cmd\"\r\nContent-Disposition: attachment; filename=\"../../Autostart/boese.cmd\"\r\n\r\necho\r\n" +
            "--=_g1\r\nContent-Type: image/png\r\nContent-ID: <bild1@x>\r\nContent-Disposition: inline\r\nContent-Transfer-Encoding: base64\r\n\r\niVBORw0KGgo=\r\n--=_g1--\r\nNachspann\r\n");
        var m = MailMime.Parse(raw);
        Assert.Equal(("Jörg Müller <j@example.com>", "Grüße aus Köln", "Schöne Grüße, das kostet 5 € mehr."), (m.From, m.Subject, m.Body));
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 8, 0, 0, TimeSpan.FromHours(1)), m.When);
        Assert.Equal("<abc@example.com>", m.MessageId);
        Assert.Equal(["Übersicht März.txt", "Autostartboese.cmd", "anhang3"], m.Attachments.Select(a => a.Name));
        Assert.All(m.Attachments, a => Assert.Equal(a.Name, Path.GetFileName(a.Name))); // nie ein Pfad
        Assert.Equal(latin.GetBytes("Inhalt mit \u00e4 roh"), m.Attachments[0].Data); // 8bit bytegleich
        Assert.True(m.Attachments[2].Inline); Assert.Equal("bild1@x", m.Attachments[2].ContentId);
        // ein Zeichen über zwei kodierte Wörter verteilt (kommt bei fremden Programmen vor)
        var eur = Encoding.UTF8.GetBytes("€");
        Assert.Equal("€", MailMime.DecodeWords($"=?UTF-8?B?{Convert.ToBase64String(eur[..1])}?= =?UTF-8?B?{Convert.ToBase64String(eur[1..])}?="));
        // rohes UTF-8 und rohes Windows-1252 ohne Angabe
        Assert.Equal("Grüße", MailMime.Parse(Encoding.UTF8.GetBytes("Subject: Grüße\r\n\r\nx")).Subject);
        Assert.Equal("Grüße", MailMime.Parse(latin.GetBytes("Subject: Gr\u00fc\u00dfe\r\n\r\nx")).Subject);
        Assert.Equal("Zeile 1\nZeile 2", MailMime.Parse(Encoding.UTF8.GetBytes("Subject: x\n\nZeile 1\nZeile 2\n")).Body);
    }

    [Fact(DisplayName = "T-0689 HTML sicher: Skripte, Ereignis-Attribute, javascript:-Links, Rahmen, Formulare und Stilangaben fallen weg; Bilder aus dem Internet erst nach Freigabe")]
    public void Html_sanitize()
    {
        const string evil = "<html><head><title>t</title><style>p{background:url(https://spion.example/x)}</style><meta http-equiv=refresh content='0;url=https://boese.example'></head><body onload=\"alert(1)\">" +
            "<p onclick=\"steal()\" style=\"background:url(https://spion.example/s.png)\">Hallo <b>Welt</b> &amp; mehr<script>alert('x')</script><scr<script>ipt>alert(2)</script></p>" +
            "<a href=\"javascript:alert(1)\">A</a><a href=\" jav&#x09;ascript:alert(1)\">B</a><a href=\"https://example.org/seite?a=1&amp;b=2\">C</a><a href=\"data:text/html,<script>x</script>\">D</a><a href=\"mailto:e@example.org\">E</a>" +
            "<img src=\"https://spion.example/zaehlpixel.gif\" width=1 height=1 onerror=\"alert(3)\"><img src=\"cid:bild1@x\" alt=\"Logo\"><img src=\"data:image/png;base64,iVBORw0KGgo=\"><img src=\"data:image/svg+xml;base64,PHN2Zz4=\"><img src=\"file:///c:/geheim.txt\">" +
            "<iframe src=\"https://boese.example\"></iframe><form action=\"https://boese.example\"><input name=pw><button>Senden</button></form><object data=x></object><svg><script>alert(4)</script></svg>" +
            "<table><tr><td>Zelle 1<td>Zelle 2</tr></table><ul><li>eins<li>zwei</ul><!-- <script>alert(5)</script> --></body></html>";
        var safe = MailHtml.Sanitize(evil);
        var all = MailHtml.All(safe.Root).ToList();
        var tags = all.OfType<MailHtmlElement>().Select(e => e.Tag).ToHashSet();
        Assert.Empty(tags.Intersect(["script", "style", "iframe", "form", "input", "button", "object", "svg", "meta", "head", "title", "body", "html"]));
        Assert.All(all.OfType<MailHtmlElement>(), e => Assert.All(e.Attr.Keys, k => Assert.Contains(k, new[] { "href", "src", "alt", "title", "width", "height", "colspan", "blocked" })));
        var text = MailHtml.TextOf(safe.Root);
        Assert.DoesNotContain("alert", text); Assert.DoesNotContain("steal", text); Assert.Contains("Hallo Welt & mehr", text);
        var links = all.OfType<MailHtmlElement>().Where(e => e.Tag == "a").Select(e => e.Attr.GetValueOrDefault("href")).ToList();
        Assert.Equal([null, null, "https://example.org/seite?a=1&b=2", null, "mailto:e@example.org"], links);
        var imgs = all.OfType<MailHtmlElement>().Where(e => e.Tag == "img").ToList();
        Assert.Equal(1, safe.BlockedImages);
        Assert.Equal([null, "cid:bild1@x", "data:image/png;base64,iVBORw0KGgo=", null, null], imgs.Select(i => i.Attr.GetValueOrDefault("src")));
        Assert.True(imgs[0].Attr.ContainsKey("blocked"));
        Assert.Equal(["Zelle 1", "Zelle 2"], all.OfType<MailHtmlElement>().Where(e => e.Tag == "td").Select(MailHtml.TextOf));
        Assert.Equal(["eins", "zwei"], all.OfType<MailHtmlElement>().Where(e => e.Tag == "li").Select(MailHtml.TextOf));
        // erst nach ausdrücklicher Freigabe steht die Internet-Adresse des Bildes im Ergebnis
        var open = MailHtml.Sanitize(evil, allowRemoteImages: true);
        Assert.Equal(0, open.BlockedImages);
        Assert.Contains(MailHtml.All(open.Root).OfType<MailHtmlElement>(), e => e.Attr.GetValueOrDefault("src") == "https://spion.example/zaehlpixel.gif");
        // Seite für den Browser: zusätzliche Sperre im Dokument, kein Skript, kein fremdes Ziel ohne Freigabe
        var doc = MailHtml.ToDocument(safe, "Test <x>", id => id == "bild1@x" ? new MailAttachment("b.png", "image/png", [1, 2, 3]) : null);
        Assert.Contains("Content-Security-Policy\" content=\"default-src 'none'; img-src data:;", doc);
        Assert.DoesNotContain("<script", doc, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("spion.example", doc); Assert.DoesNotContain("onerror", doc); Assert.DoesNotContain("javascript", doc);
        Assert.Contains("src=\"data:image/png;base64,AQID\"", doc); Assert.Contains("<title>Test &lt;x&gt;</title>", doc);
        // tief verschachtelt und riesig: kein Absturz
        var deep = MailHtml.Sanitize(string.Concat(Enumerable.Repeat("<div>", 5000)) + "unten" + string.Concat(Enumerable.Repeat("<b>x", 30000)));
        Assert.Contains("unten", MailHtml.TextOf(deep.Root));
    }

    [Fact(DisplayName = "T-0689 Zwischenspeicher: Kopfzeilen und geöffnete Nachrichten bleiben ohne Netz lesbar; auf dem Server Gelöschtes verschwindet; neue Ordner-Kennung baut neu auf; Suche im Text")]
    public void Store_offline()
    {
        var root = Temp();
        try
        {
            var st = new MailStore(root, "Erika@Example.org");
            MailHeader H(long uid, string s, bool seen = false) => new(uid, "Max <max@example.com>", s, new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero), seen) { To = "erika@example.org", HasAttachment = uid == 3 };
            st.SaveFolders([new MailFolderInfo("INBOX", "INBOX", "inbox"), new MailFolderInfo("Entwürfe", "Entwürfe", "drafts")]);
            st.Merge("INBOX", 77, 3, [H(3, "Drei"), H(2, "Zwei"), H(1, "Eins")], firstPage: true);
            st.SaveRaw("INBOX", 2, Encoding.UTF8.GetBytes("Subject: Zwei\r\n\r\nGeheimer Kuchen am Sonntag\r\n"));
            // „Neustart“: neues Objekt liest alles von der Platte
            var again = new MailStore(root, "erika@example.org");
            Assert.Equal(["Posteingang", "Entwürfe"], again.Folders().Select(f => f.Label));
            var s1 = again.Load("INBOX");
            Assert.Equal((77L, 3, 3), (s1.UidValidity, s1.Exists, s1.Headers.Count));
            Assert.True(s1.Headers[0].HasAttachment); Assert.Equal("erika@example.org", s1.Headers[0].To);
            Assert.Equal("Geheimer Kuchen am Sonntag", MailMime.Parse(again.Raw("INBOX", 2)!).Body);
            Assert.Null(again.Raw("INBOX", 1));
            Assert.Equal([2L], again.Search("INBOX", "kuchen").Select(h => h.Uid)); Assert.Equal([3L], again.Search("INBOX", "drei").Select(h => h.Uid));
            // neuer Abruf: 2 ist auf dem Server weg, 4 ist neu
            var s2 = again.Merge("INBOX", 77, 3, [H(4, "Vier"), H(3, "Drei", seen: true), H(1, "Eins")], firstPage: true);
            Assert.Equal([4L, 3L, 1L], s2.Headers.Select(h => h.Uid)); Assert.True(s2.Headers[1].Seen);
            Assert.Null(again.Raw("INBOX", 2));
            // ältere Seite nachladen entfernt nichts
            Assert.Equal(4, again.Merge("INBOX", 77, 4, [H(0, "Null")], firstPage: false).Headers.Count);
            Assert.Equal([4L, 3L, 0L], again.Update("INBOX", h => h.Uid == 1 ? null : h with { Seen = true }).Headers.Select(h => h.Uid));
            // Server hat die Ordner-Kennung geändert: alte Nummern gelten nicht mehr
            again.SaveRaw("INBOX", 4, [1]);
            var s3 = again.Merge("INBOX", 78, 1, [H(1, "Neu")], firstPage: true);
            Assert.Equal("Neu", Assert.Single(s3.Headers).Subject); Assert.Null(again.Raw("INBOX", 4));
            Assert.DoesNotContain("Erika", Path.GetFileName(again.Root)); // Ordnername klein und ohne Sonderzeichen
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0689 Anbieter-Tabelle: bekannte Server, App-Passwort-Hinweis, Outlook.com ehrlich blockiert; unbekannte Adresse → imap./smtp. + Domain")]
    public void Providers()
    {
        Assert.Equal(("imap.gmail.com", 993, "smtp.gmail.com", 465, true), (MailAccount.Guess("a@gmail.com").ImapHost, MailAccount.Guess("a@gmail.com").ImapPort, MailAccount.Guess("a@gmail.com").SmtpHost, MailAccount.Guess("a@gmail.com").SmtpPort, MailAccount.Guess("a@gmail.com").SmtpImplicitTls));
        Assert.Equal(MailAuth.AppPassword, MailProviders.For("A@GMAIL.com")!.Auth);
        Assert.Equal(MailAuth.OAuthOnly, MailProviders.For("a@hotmail.de")!.Auth); Assert.Contains("blockiert", MailProviders.For("a@outlook.com")!.State);
        Assert.Null(MailProviders.For("a@meine-firma.example"));
        Assert.Equal(("imap.meine-firma.example", "smtp.meine-firma.example", 587, false), (MailAccount.Guess("a@meine-firma.example").ImapHost, MailAccount.Guess("a@meine-firma.example").SmtpHost, MailAccount.Guess("a@meine-firma.example").SmtpPort, MailAccount.Guess("a@meine-firma.example").SmtpImplicitTls));
        Assert.All(MailProviders.All, p => { Assert.Equal(993, p.ImapPort); Assert.Contains(p.SmtpPort, new[] { 465, 587 }); Assert.Equal(p.SmtpPort == 465, p.SmtpImplicitTls); Assert.NotEmpty(p.Hint); });
        Assert.Equal(MailProviders.All.SelectMany(p => p.Domains).Count(), MailProviders.All.SelectMany(p => p.Domains).Distinct().Count());
    }
}
