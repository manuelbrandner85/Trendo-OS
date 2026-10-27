using System.Net;
using System.Net.Sockets;
using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>O1–O6: Posteingang (IMAP, nur lesen), Senden (SMTP), Kalender mit ICS und Erinnerungen, Aufgaben, Regelvorschläge.</summary>
public class PimTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-pim-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "O1: IMAP nur lesend – EXAMINE und BODY.PEEK, Kopfzeilen und Nachricht werden gelesen, Passwort nicht im Protokoll")]
    public async Task Imap_readonly()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var ct = cts.Token;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var commands = new List<string>();
        var server = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync(ct); var s = c.GetStream();
            var r = new StreamReader(s, Encoding.UTF8); var w = new StreamWriter(s, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await w.WriteLineAsync("* OK IMAP bereit");
            while (await r.ReadLineAsync(ct) is { } line)
            {
                commands.Add(line); var tag = line.Split(' ')[0]; var cmd = line[(tag.Length + 1)..];
                if (cmd.StartsWith("LOGIN")) await w.WriteLineAsync($"{tag} OK angemeldet");
                else if (cmd.StartsWith("EXAMINE")) { await w.WriteLineAsync("* 2 EXISTS"); await w.WriteLineAsync($"{tag} OK [READ-ONLY] EXAMINE"); }
                else if (cmd.StartsWith("FETCH"))
                {
                    var h1 = "From: Erika Muster <erika@example.org>\r\nSubject: =?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Grüße")) + "?=\r\nDate: Mon, 26 Oct 2026 10:00:00 +0100\r\n\r\n";
                    var h2 = "From: shop@example.com\r\nSubject: Rechnung\r\nDate: Tue, 27 Oct 2026 09:00:00 +0100\r\n\r\n";
                    await w.WriteAsync($"* 1 FETCH (UID 11 FLAGS (\\Seen) BODY[HEADER.FIELDS (FROM SUBJECT DATE)] {{{Encoding.UTF8.GetByteCount(h1)}}}\r\n{h1})\r\n");
                    await w.WriteAsync($"* 2 FETCH (UID 12 FLAGS () BODY[HEADER.FIELDS (FROM SUBJECT DATE)] {{{Encoding.UTF8.GetByteCount(h2)}}}\r\n{h2})\r\n");
                    await w.WriteLineAsync($"{tag} OK FETCH");
                }
                else if (cmd.StartsWith("UID FETCH"))
                {
                    var msg = "From: shop@example.com\r\nTo: erika@example.org\r\nSubject: Rechnung\r\nDate: Tue, 27 Oct 2026 09:00:00 +0100\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nIhre Rechnung über 12,50 €.\r\n";
                    await w.WriteAsync($"* 2 FETCH (UID 12 BODY[] {{{Encoding.UTF8.GetByteCount(msg)}}}\r\n{msg})\r\n");
                    await w.WriteLineAsync($"{tag} OK FETCH");
                }
                else if (cmd.StartsWith("LOGOUT")) { await w.WriteLineAsync("* BYE"); await w.WriteLineAsync($"{tag} OK"); break; }
                else await w.WriteLineAsync($"{tag} BAD");
            }
        }, ct);
        var tcp = new TcpClient(); await tcp.ConnectAsync(IPAddress.Loopback, port, ct);
        var imap = new ImapSession(tcp.GetStream());
        await imap.GreetingAsync(ct);
        await imap.LoginAsync("erika@example.org", "geheim\"pw", ct);
        var n = await imap.ExamineAsync(ct: ct);
        var heads = await imap.HeadersAsync(n, 50, ct);
        Assert.Equal(2, n);
        Assert.Equal(["Rechnung", "Grüße"], heads.Select(h => h.Subject));
        Assert.True(heads[1].Seen); Assert.False(heads[0].Seen); Assert.Equal(12, heads[0].Uid);
        var msg = await imap.MessageAsync(12, ct);
        Assert.Contains("12,50 €", msg.Body);
        await imap.LogoutAsync(ct);
        await server.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.Contains(commands, c => c.Contains("EXAMINE \"INBOX\""));
        Assert.All(commands.Where(c => c.Contains("FETCH")), c => Assert.Contains("PEEK", c));
        Assert.DoesNotContain(commands, c => c.Contains("SELECT") || c.Contains("STORE"));
        Assert.DoesNotContain("geheim", imap.Log);
        listener.Stop();
    }

    [Fact(DisplayName = "O2: SMTP – Anmeldung, Absender, Empfänger, Inhalt mit Umlauten (Base64), Punkt-Verdopplung")]
    public async Task Smtp_send()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var ct = cts.Token;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var log = new List<string>(); var data = new StringBuilder();
        var server = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync(ct); var s = c.GetStream();
            var r = new StreamReader(s, Encoding.ASCII); var w = new StreamWriter(s, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            await w.WriteLineAsync("220 bereit");
            var inData = false;
            while (await r.ReadLineAsync(ct) is { } line)
            {
                if (inData) { if (line == ".") { inData = false; await w.WriteLineAsync("250 angenommen"); } else data.AppendLine(line); continue; }
                log.Add(line);
                if (line.StartsWith("EHLO")) { await w.WriteLineAsync("250-hallo"); await w.WriteLineAsync("250 AUTH PLAIN"); }
                else if (line.StartsWith("AUTH PLAIN")) await w.WriteLineAsync("235 ok");
                else if (line.StartsWith("MAIL FROM") || line.StartsWith("RCPT TO")) await w.WriteLineAsync("250 ok");
                else if (line == "DATA") { inData = true; await w.WriteLineAsync("354 los"); }
                else if (line == "QUIT") { await w.WriteLineAsync("221 tschüss"); break; }
            }
        }, ct);
        var acc = MailAccount.Guess("erika@example.org", "Erika Muster");
        var draft = new MailDraft("Max <max@example.com>", "Grüße aus Berlin", "Hallo Max,\n.\nviele Grüße");
        var eml = SmtpSession.Compose(acc, draft, new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.FromHours(1)));
        var tcp = new TcpClient(); await tcp.ConnectAsync(IPAddress.Loopback, port, ct);
        await new SmtpSession(tcp.GetStream()).SendAsync(acc.User, "pw", acc.Email, SmtpSession.Recipients(draft), eml, ct);
        await server.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.Contains("MAIL FROM:<erika@example.org>", log); Assert.Contains("RCPT TO:<max@example.com>", log);
        var sent = data.ToString();
        Assert.Contains("Subject: =?UTF-8?B?", sent); Assert.Contains("Date: Tue, 27 Oct 2026 10:00:00 +0100", sent);
        var body = Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(sent[(sent.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..].Split("\r\n").Where(l => l.Length > 0))));
        Assert.Equal("Hallo Max,\r\n.\r\nviele Grüße", body);
        Assert.Equal(("imap.gmx.net", "mail.gmx.net", 587), (MailAccount.Guess("a@gmx.de").ImapHost, MailAccount.Guess("a@gmx.de").SmtpHost, MailAccount.Guess("a@gmx.de").SmtpPort));
        listener.Stop();
    }

    [Fact(DisplayName = "O3/O5: Kalender – Wiederholungen, Erinnerungen, ICS hin und zurück (auch Fremd-ICS mit Faltung, UTC, Alarm); Aufgaben als VTODO")]
    public void Calendar()
    {
        var dir = Temp();
        try
        {
            var cal = new Trendo.Core.Sys.Calendar(dir);
            cal.Add(new Trendo.Core.Sys.CalEvent("a1", new DateTime(2026, 10, 26, 18, 0, 0), new DateTime(2026, 10, 26, 19, 0, 0), "Sport", RemindMin: 30, Repeat: Trendo.Core.Sys.Repeat.Woechentlich));
            cal.Add(new Trendo.Core.Sys.CalEvent("a2", new DateTime(2026, 11, 3), new DateTime(2026, 11, 4), "Geburtstag Erika", AllDay: true, RemindMin: 1440, Repeat: Trendo.Core.Sys.Repeat.Jaehrlich));
            cal.AddTodo(new Trendo.Core.Sys.TodoItem("t1", "Steuer erledigen", new DateTime(2026, 11, 30), Priority: 1, List: "Haushalt"));
            var nov = cal.Occurrences(new DateTime(2026, 11, 1), new DateTime(2026, 12, 1));
            Assert.Equal(5, nov.Count(o => o.Event.Uid == "a1")); Assert.Single(nov, o => o.Event.Uid == "a2");
            var due = cal.DueReminders(new DateTime(2026, 11, 2, 17, 0, 0), new DateTime(2026, 11, 2, 17, 31, 0));
            Assert.Contains(due, d => d.Event.Uid == "a1"); Assert.DoesNotContain(due, d => d.Event.Uid == "a2");
            Assert.Contains(cal.DueReminders(new DateTime(2026, 11, 1, 23, 55, 0), new DateTime(2026, 11, 2, 0, 5, 0)), d => d.Event.Uid == "a2"); // ganztägig, 1 Tag vorher
            var ics = Trendo.Core.Sys.Ics.Export(cal.All(), cal.Todos());
            Assert.Contains("RRULE:FREQ=WEEKLY", ics); Assert.Contains("TRIGGER:-PT30M", ics); Assert.Contains("BEGIN:VTODO", ics); Assert.DoesNotContain("Trendo", ics);
            var back = new Trendo.Core.Sys.Calendar(Temp());
            Assert.Equal((2, 1), back.ImportText(ics));
            Assert.Equal(cal.All(), back.All());
            Assert.Equal(1, back.Todos().Single().Priority);
            var foreign = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:x@google.com\r\nDTSTART:20261105T090000Z\r\nDTEND:20261105T100000Z\r\nSUMMARY:Zahnarzt mit sehr langem Titel der\r\n  umbricht\r\nLOCATION:Praxis\\, Hauptstraße 1\r\nBEGIN:VALARM\r\nTRIGGER:-PT1H\r\nEND:VALARM\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
            Assert.Equal((1, 0), back.ImportText(foreign));
            var z = back.All().Single(e => e.Uid == "x@google.com");
            Assert.Equal("Zahnarzt mit sehr langem Titel der umbricht", z.Title); Assert.Equal("Praxis, Hauptstraße 1", z.Location); Assert.Equal(60, z.RemindMin);
            cal.SetDone("t1", true);
            Assert.True(new Trendo.Core.Sys.Calendar(dir).Todos().Single().Done);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "O6: Regelvorschläge – häufige Absender, Treffer nur als Vorschau, inaktiv bis zur Freigabe")]
    public void Rules()
    {
        var inbox = Enumerable.Range(0, 4).Select(i => new MailHeader(i, "Shop <info@laden.de>", "Angebot " + i, null, false))
            .Append(new MailHeader(9, "Erika <erika@example.org>", "Hallo", null, true)).ToList();
        var rules = MailRule.Suggest(inbox);
        var r = Assert.Single(rules);
        Assert.False(r.Active); Assert.Equal("laden", r.Category);
        Assert.Equal(4, inbox.Count(r.Matches));
        Assert.False(new MailRule("x", "leer", "", "", "x").Matches(inbox[0]));
    }
}
