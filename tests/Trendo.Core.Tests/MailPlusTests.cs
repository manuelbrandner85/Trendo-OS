using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Posteingang als Mailprogramm: MIME mit verschachtelten Teilen und Anhängen, HTML-Mails, Unterhaltungen, Allen antworten, Zitat, Entwürfe und Gesendet.
public class MailPlusTests
{
    const string Nested = "From: Erika Muster <erika@example.org>\r\nTo: ich@example.org, max@example.org\r\nCc: team@example.org\r\nSubject: =?utf-8?B?UmU6IEFuZ2Vib3Q=?=\r\nDate: Sun, 1 Nov 2026 10:00:00 +0100\r\n" +
        "Content-Type: multipart/mixed; boundary=\"aussen\"\r\n\r\n--aussen\r\nContent-Type: multipart/alternative; boundary=\"innen\"\r\n\r\n--innen\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nHallo, anbei das Angebot f=C3=BCr dich.\r\n--innen\r\nContent-Type: text/html\r\n\r\n<p>Hallo</p>\r\n--innen--\r\n" +
        "--aussen\r\nContent-Type: application/pdf; name=\"angebot.pdf\"\r\nContent-Disposition: attachment; filename=\"angebot.pdf\"\r\nContent-Transfer-Encoding: base64\r\n\r\nJVBERi0xLjQK\r\n--aussen--\r\n";

    [Fact(DisplayName = "Mailprogramm: verschachtelte MIME-Mail – Text aus multipart/alternative, Anhang mit Name/Art/Inhalt, Cc und kodierter Betreff")]
    public void Nested_mime()
    {
        var m = MailCenter.ParseEml(Nested);
        Assert.Equal("Re: Angebot", m.Subject); Assert.Equal("Hallo, anbei das Angebot für dich.", m.Body); Assert.Equal("team@example.org", m.Cc);
        var a = Assert.Single(m.Attachments);
        Assert.Equal(("angebot.pdf", "application/pdf", "%PDF-1.4\n"), (a.Name, a.ContentType, System.Text.Encoding.ASCII.GetString(a.Data)));
        var html = MailCenter.ParseEml("From: a@b.de\nSubject: Nur HTML\nContent-Type: text/html\n\n<html><style>p{}</style><body><p>Erste &amp; Zeile</p><p>Zweite</p><script>x()</script></body></html>");
        Assert.Equal("Erste & Zeile\nZweite", html.Body.Replace("\n\n", "\n"));
        Assert.Empty(html.Attachments);
        Assert.Equal("Einfach", MailCenter.ParseEml("From: a@b.de\nSubject: x\n\nEinfach").Body); // ohne Content-Type: Text
    }

    [Fact(DisplayName = "Mailprogramm: Unterhaltungen ohne Re:/AW:/WG:-Präfixe; Allen antworten ohne eigene Adresse; Zitat und Betreff ohne gestapelte Präfixe")]
    public void Threads_reply_all_quote()
    {
        Assert.Equal("angebot", MailFolders.ThreadKey("AW: Re: WG: Angebot"));
        var mails = new[] { new MailHeader(1, "a", "Angebot", new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero), true), new MailHeader(2, "b", "Termin", new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero), true),
            new MailHeader(3, "c", "AW: Angebot", new DateTimeOffset(2026, 11, 1, 11, 0, 0, TimeSpan.Zero), false) };
        var t = MailFolders.Threads(mails);
        Assert.Equal(["angebot", "termin"], t.Select(x => x.Key)); Assert.Equal([3L, 1L], t[0].Mails.Select(m => m.Uid));
        var m = MailCenter.ParseEml(Nested);
        Assert.Equal(("erika@example.org", "max@example.org, team@example.org"), MailFolders.ReplyAll(m, "Ich@Example.org"));
        Assert.Equal("AW: Angebot", MailFolders.ReplySubject("Re: Angebot", false)); Assert.Equal("WG: Angebot", MailFolders.ReplySubject("AW: Angebot", true));
        Assert.Contains("schrieb Erika Muster <erika@example.org>:\n> Hallo, anbei", MailFolders.Quote(m));
    }

    [Fact(DisplayName = "Mailprogramm: Entwürfe speichern/ersetzen/löschen (leere nicht), Gesendet ablegen entfernt den Entwurf; alles bleibt nach Neustart")]
    public void Drafts_and_sent()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-mailf-" + Guid.NewGuid().ToString("N"));
        try
        {
            var f = new MailFolders(root);
            Assert.Null(f.SaveDraft(new MailDraft(" ", "", "")));
            var d = f.SaveDraft(new MailDraft("max@example.org", "Treffen", "Hallo Max"))!;
            f.SaveDraft(new MailDraft("max@example.org", "Treffen am Freitag", "Hallo Max"), d.Id);
            Assert.Equal("Treffen am Freitag", Assert.Single(f.Drafts).Subject);
            f.AddSent(new MailDraft("max@example.org", "Treffen am Freitag", "Hallo Max"), d.Id);
            var again = new MailFolders(root);
            Assert.Empty(again.Drafts); Assert.Equal("Treffen am Freitag", Assert.Single(again.Sent).Subject);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
