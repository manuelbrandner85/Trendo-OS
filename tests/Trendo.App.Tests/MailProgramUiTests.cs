using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Runde 7: Posteingang als Mailprogramm – Anhänge ansehen/speichern, Allen antworten mit Kopie, Entwürfe, Gesendet, Unterhaltungen, Tastenkürzel. Mailserver nachgebildet.
public class MailProgram_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    const string Msg = "From: Erika Muster <erika@example.org>\r\nTo: ich@example.org, max@example.org\r\nCc: team@example.org\r\nSubject: Angebot\r\nDate: Sun, 1 Nov 2026 10:00:00 +0100\r\n" +
        "Content-Type: multipart/mixed; boundary=\"b1\"\r\n\r\n--b1\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nAnbei das Angebot.\r\n--b1\r\nContent-Type: application/pdf; name=\"angebot.pdf\"\r\nContent-Disposition: attachment; filename=\"angebot.pdf\"\r\nContent-Transfer-Encoding: base64\r\n\r\nJVBERi0xLjQK\r\n--b1--\r\n";

    static Task FakeImap(TcpListener l, CancellationToken ct) => Task.Run(async () =>
    {
        for (var conn = 0; conn < 2; conn++)
        {
            using var c = await l.AcceptTcpClientAsync(ct); var s = c.GetStream();
            var r = new StreamReader(s, Encoding.UTF8); var w = new StreamWriter(s, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await w.WriteLineAsync("* OK");
            while (await r.ReadLineAsync(ct) is { } line)
            {
                var tag = line.Split(' ')[0]; var cmd = line[(tag.Length + 1)..];
                if (cmd.StartsWith("EXAMINE")) { await w.WriteLineAsync("* 2 EXISTS"); await w.WriteLineAsync($"{tag} OK"); }
                else if (cmd.StartsWith("FETCH"))
                {
                    var h1 = "From: Erika Muster <erika@example.org>\r\nSubject: Angebot\r\nDate: Sun, 1 Nov 2026 10:00:00 +0100\r\n\r\n";
                    var h2 = "From: Max Beispiel <max@example.org>\r\nSubject: AW: Angebot\r\nDate: Sun, 1 Nov 2026 11:00:00 +0100\r\n\r\n";
                    await w.WriteAsync($"* 1 FETCH (UID 7 FLAGS () BODY[HEADER.FIELDS (FROM SUBJECT DATE)] {{{Encoding.UTF8.GetByteCount(h1)}}}\r\n{h1})\r\n");
                    await w.WriteAsync($"* 2 FETCH (UID 8 FLAGS () BODY[HEADER.FIELDS (FROM SUBJECT DATE)] {{{Encoding.UTF8.GetByteCount(h2)}}}\r\n{h2})\r\n");
                    await w.WriteLineAsync($"{tag} OK");
                }
                else if (cmd.StartsWith("UID FETCH")) { await w.WriteAsync($"* 1 FETCH (UID 7 BODY[] {{{Encoding.UTF8.GetByteCount(Msg)}}}\r\n{Msg})\r\n"); await w.WriteLineAsync($"{tag} OK"); }
                else if (cmd.StartsWith("LOGOUT")) { await w.WriteLineAsync($"{tag} OK"); break; }
                else await w.WriteLineAsync($"{tag} OK");
            }
        }
    }, ct);

    [AvaloniaFact(DisplayName = "Mailprogramm: Anhang ansehen und speichern, Allen antworten (Kopie ohne eigene Adresse), Entwurf speichern/öffnen, Senden → Gesendet, Unterhaltungen, Tasten N/R")]
    public async Task Mail_program()
    {
        var (w, _, root) = Ui.Open(); var ct = TestContext.Current.CancellationToken;
        w.SetMailAccount("ich@example.org", "Ich", "geheim");
        var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var port = ((IPEndPoint)l.LocalEndpoint).Port;
        var server = FakeImap(l, ct);
        w.ImapStreamOverride = async () => { var t = new TcpClient(); await t.ConnectAsync(IPAddress.Loopback, port, ct); return t.GetStream(); };
        Assert.True(await w.FetchInbox());
        await w.OpenInboxMail(7); w.Show("posteingang"); Pump();
        await server.WaitAsync(TimeSpan.FromSeconds(10), ct); l.Stop();
        // Anhang
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("angebot.pdf"));
        w.Find<Button>("inbox-att-0").Press();
        var saved = Directory.GetFiles(Path.Combine(root, "test-anhaenge"));
        Assert.Equal("%PDF-1.4\n", File.ReadAllText(Assert.Single(saved)));
        // Allen antworten
        w.Find<Button>("inbox-reply-all").Press(); Pump();
        Assert.Equal(("erika@example.org", "max@example.org, team@example.org", "AW: Angebot"), (w.Find<TextBox>("send-to").Text, w.Find<TextBox>("send-cc").Text, w.Find<TextBox>("send-subject").Text));
        Assert.Contains("> Anbei das Angebot.", w.Find<TextBox>("send-body").Text);
        // Entwurf speichern und wieder öffnen
        w.Find<TextBox>("send-body").Text = "Danke, ich melde mich.";
        w.Find<Button>("send-draft").Press();
        Assert.Single(w.MailBoxes.Drafts);
        w.Find<Button>("inbox-folder-entwuerfe").Press(); w.Find<Button>("inbox-local-0").Press();
        Assert.Equal("Danke, ich melde mich.", w.Find<TextBox>("send-body").Text);
        Assert.Equal("max@example.org, team@example.org", w.Find<TextBox>("send-cc").Text);
        // Senden nur nach Bestätigung → Gesendet, Entwurf weg
        MailDraft? sent = null; w.SendOverride = d => { sent = d; return Task.CompletedTask; };
        w.Find<Button>("send-ask").Press(); Pump(); Assert.Null(sent);
        w.Find<Button>("send-confirm-yes").Press();
        for (var i = 0; i < 50 && sent is null; i++) { Pump(); await Task.Delay(20, ct); }
        Assert.Equal("max@example.org, team@example.org", sent!.Cc);
        Assert.Empty(w.MailBoxes.Drafts); Assert.Equal("AW: Angebot", Assert.Single(w.MailBoxes.Sent).Subject);
        w.Find<Button>("inbox-folder-gesendet").Press(); w.Find<Button>("inbox-local-0").Press();
        Assert.Contains("Gesendet an erika@example.org", w.GetLogicalTexts().First(t => t.StartsWith("Gesendet an")));
        // Unterhaltungen: „Angebot“ und „AW: Angebot“ werden eine Zeile mit (2)
        w.Find<Button>("inbox-folder-alle").Press();
        w.Find<CheckBox>("inbox-threads").IsChecked = true; Pump();
        Assert.Null(w.TryFind<Button>("inbox-item-1"));
        Assert.Contains(w.GetLogicalTexts(), t => t.EndsWith("(2)"));
        // Tasten
        Assert.True(w.MailKey(Avalonia.Input.Key.N, Avalonia.Input.KeyModifiers.None));
        Assert.Equal("", w.Find<TextBox>("send-to").Text);
        Assert.False(w.MailKey(Avalonia.Input.Key.Q, Avalonia.Input.KeyModifiers.None));
        w.Close();
    }
}
