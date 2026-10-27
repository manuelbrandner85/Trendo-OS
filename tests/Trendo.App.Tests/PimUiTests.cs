using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

/// <summary>O1–O6 Trendo Post & Kalender – Oberfläche.</summary>
public class O1O6_PimUi
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    /// <summary>Kleiner IMAP-Server für zwei Anfragen (Abruf, Öffnen).</summary>
    static Task FakeImap(TcpListener l, List<string> log, CancellationToken ct) => Task.Run(async () =>
    {
        for (var conn = 0; conn < 2; conn++)
        {
            using var c = await l.AcceptTcpClientAsync(ct); var s = c.GetStream();
            var r = new StreamReader(s, Encoding.UTF8); var w = new StreamWriter(s, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await w.WriteLineAsync("* OK");
            while (await r.ReadLineAsync(ct) is { } line)
            {
                log.Add(line); var tag = line.Split(' ')[0]; var cmd = line[(tag.Length + 1)..];
                if (cmd.StartsWith("EXAMINE")) { await w.WriteLineAsync("* 1 EXISTS"); await w.WriteLineAsync($"{tag} OK"); }
                else if (cmd.StartsWith("FETCH")) { var h = "From: Erika Muster <erika@example.org>\r\nSubject: Termin am Freitag\r\n\r\n"; await w.WriteAsync($"* 1 FETCH (UID 7 FLAGS () BODY[HEADER.FIELDS (FROM SUBJECT DATE)] {{{Encoding.UTF8.GetByteCount(h)}}}\r\n{h})\r\n"); await w.WriteLineAsync($"{tag} OK"); }
                else if (cmd.StartsWith("UID FETCH")) { var m = "From: Erika Muster <erika@example.org>\r\nSubject: Termin am Freitag\r\n\r\nPasst 10 Uhr?\r\n"; await w.WriteAsync($"* 1 FETCH (UID 7 BODY[] {{{Encoding.UTF8.GetByteCount(m)}}}\r\n{m})\r\n"); await w.WriteLineAsync($"{tag} OK"); }
                else if (cmd.StartsWith("LOGOUT")) { await w.WriteLineAsync($"{tag} OK"); break; }
                else await w.WriteLineAsync($"{tag} OK");
            }
        }
    }, ct);

    [AvaloniaFact(DisplayName = "O1/O2/O6: Konto (Passwort geschützt), Posteingang nur lesend, Nachricht öffnen, Antworten nur nach Bestätigung senden, Regel erst nach Freigabe")]
    public async Task Inbox_and_send()
    {
        var (w, _, _) = Ui.Open(); var ct = TestContext.Current.CancellationToken;
        w.SetMailAccount("erika@example.org", "Erika Muster", "geheim");
        Assert.DoesNotContain("geheim", File.ReadAllText(Path.Combine(w.Core.DataRoot, "post", "konto.json")));
        var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var port = ((IPEndPoint)l.LocalEndpoint).Port; var log = new List<string>();
        var server = FakeImap(l, log, ct);
        w.ImapStreamOverride = async () => { var t = new TcpClient(); await t.ConnectAsync(IPAddress.Loopback, port, ct); return t.GetStream(); };
        w.Show("posteingang"); Pump();
        Assert.True(await w.FetchInbox());
        w.Show("posteingang"); Pump();
        Assert.Equal("Termin am Freitag", Assert.Single(w.Inbox).Subject);
        await w.OpenInboxMail(7); w.Show("posteingang"); Pump();
        Assert.Contains("Passt 10 Uhr?", w.Find<SelectableTextBlock>("inbox-body").Text);
        await server.WaitAsync(TimeSpan.FromSeconds(10), ct); l.Stop();
        Assert.DoesNotContain(log, x => x.Contains("SELECT") || x.Contains("STORE"));
        // Antworten: Senden erst nach Bestätigung
        MailDraft? sent = null; w.SendOverride = d => { sent = d; return Task.CompletedTask; };
        w.Find<Button>("inbox-reply").Press(); Pump();
        Assert.Equal("erika@example.org", w.Find<TextBox>("send-to").Text);
        w.Find<TextBox>("send-body").Text = "Ja, passt.";
        w.Find<Button>("send-ask").Press(); Pump();
        Assert.True(w.SendConfirmOpen); Assert.Null(sent);
        w.Find<Button>("send-confirm-yes").Press();
        for (var i = 0; i < 50 && sent is null; i++) { Pump(); await Task.Delay(20, ct); }
        Assert.Equal("Re: Termin am Freitag", sent!.Subject); Assert.Equal("Ja, passt.", sent.Body);
        // Regeln: nur Vorschlag, wirksam erst nach Freigabe
        w.MailRules.Add(new MailRule("r1", "Post von Erika", "erika@", "", "Familie"));
        w.Show("posteingang"); Pump();
        Assert.DoesNotContain("[Familie]", ((TextBlock)((StackPanel)w.Find<Button>("inbox-item-0").Content!).Children[1]).Text);
        w.Find<Button>("inbox-rule-0").Press(); Pump();
        Assert.Contains("[Familie]", ((TextBlock)((StackPanel)w.Find<Button>("inbox-item-0").Content!).Children[1]).Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "O3/O5: Termin eintragen (wöchentlich), Monats-/Wochen-/Tagesansicht, Erinnerung als Benachrichtigung; Aufgabe anlegen und abhaken")]
    public void Calendar_and_todos()
    {
        var (w, _, _) = Ui.Open();
        w.CalDay = new DateTime(2026, 11, 2); w.Show("kalender"); Pump();
        w.Find<TextBox>("cal-new-title").Text = "Chorprobe";
        w.Find<TextBox>("cal-new-date").Text = "02.11.2026"; w.Find<TextBox>("cal-new-time").Text = "19:00";
        w.Find<ComboBox>("cal-new-repeat").SelectedIndex = 2;
        w.Find<Button>("cal-add").Press(); Pump();
        var ev = Assert.Single(w.Cal.All()); Assert.Equal(Trendo.Core.Sys.Repeat.Woechentlich, ev.Repeat);
        Assert.Equal(5, w.Cal.Occurrences(new DateTime(2026, 11, 1), new DateTime(2026, 12, 1)).Count);
        Assert.NotNull(w.TryFind<Grid>("cal-month")); Assert.NotNull(w.TryFind<Button>("cal-ev-0"));
        w.Find<Button>("cal-mode-woche").Press(); Pump(); Assert.NotNull(w.TryFind<Grid>("cal-week"));
        w.Find<Button>("cal-mode-tag").Press(); Pump(); Assert.NotNull(w.TryFind<StackPanel>("cal-day"));
        var due = w.CheckCalendarReminders(new DateTime(2026, 11, 2, 18, 50, 0));
        Assert.Single(due);
        w.Show("aufgaben"); Pump();
        w.Find<TextBox>("todo-title").Text = "Noten kopieren"; w.Find<TextBox>("todo-due").Text = "01.11.2026";
        w.Find<Button>("todo-add").Press(); Pump();
        var t = Assert.Single(w.Cal.Todos()); Assert.False(t.Done);
        w.Find<CheckBox>("todo-done-0").IsChecked = true; Pump();
        Assert.True(w.Cal.Todos().Single().Done);
        w.Close();
    }
}
