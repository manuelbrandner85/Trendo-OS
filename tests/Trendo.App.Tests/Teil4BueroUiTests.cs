using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Teil 4, Arbeiten & Büro: Texte & Tabellen (SYS-10), E-Mails & Termine (SYS-09), Vorhaben (X-11).</summary>
public class Teil4BueroUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "SYS-10 F1/U1/U2: Start mit Programmkacheln und Vorlagen; Dokumente als Kacheln mit Filter; Öffnen im passenden Programm")]
    public void Doku_start()
    {
        var (w, _, root) = Ui.Open();
        w.Show("doku-start"); Pump();
        Assert.Equal("doku-start", MainWindow.StudioPages["SYS-10"]);
        Assert.True(w.Find<UniformGrid>("doku-apps").Children.Count >= 4);
        w.Find<Button>("doku-tpl-tabelle-haushalt").Press(); Pump();
        Assert.Equal("tabelle", w.CurrentView);
        var docs = Path.Combine(w.ProjectsRootForTests, "Dokumente"); Directory.CreateDirectory(docs);
        var csv = Path.Combine(docs, "liste.csv"); File.WriteAllText(csv, "A;B\n1;2\n");
        File.WriteAllText(Path.Combine(docs, "notiz.txt"), "Hallo");
        w.Show("doku-start"); Pump();
        w.Find<Button>("doku-type-tabelle").Press(); Pump();
        var tiles = w.Find<WrapPanel>("doku-files").Children.OfType<Button>().ToList();
        Assert.Single(tiles);
        tiles[0].Press(); Pump();
        Assert.Equal("tabelle", w.CurrentView);
        w.Show("doku-start"); Pump();
        w.Find<Button>("doku-type-alle").Press(); Pump();
        Assert.Equal(2, w.Find<WrapPanel>("doku-files").Children.OfType<Button>().Count());
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen")]
    public async Task Inbox_columns_and_dates()
    {
        var (w, _, _) = Ui.Open(); var ct = TestContext.Current.CancellationToken;
        w.SetMailAccount("erika@example.org", "Erika Muster", "geheim");
        var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var port = ((IPEndPoint)l.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var c = await l.AcceptTcpClientAsync(ct); var s = c.GetStream();
            var r = new StreamReader(s, Encoding.UTF8); var wr = new StreamWriter(s, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await wr.WriteLineAsync("* OK");
            while (await r.ReadLineAsync(ct) is { } line)
            {
                var tag = line.Split(' ')[0]; var cmd = line[(tag.Length + 1)..];
                if (cmd.StartsWith("EXAMINE")) { await wr.WriteLineAsync("* 1 EXISTS"); await wr.WriteLineAsync($"{tag} OK"); }
                else if (cmd.StartsWith("UID FETCH")) { var m = "From: Erika Muster <erika@example.org>\r\nSubject: Chorprobe\r\nDate: Tue, 27 Oct 2026 09:00:00 +0100\r\n\r\nWir proben am 05.11. um 19:30 Uhr.\r\n"; await wr.WriteAsync($"* 1 FETCH (UID 7 BODY[] {{{Encoding.UTF8.GetByteCount(m)}}}\r\n{m})\r\n"); await wr.WriteLineAsync($"{tag} OK"); }
                else if (cmd.StartsWith("LOGOUT")) { await wr.WriteLineAsync($"{tag} OK"); break; }
                else await wr.WriteLineAsync($"{tag} OK");
            }
        }, ct);
        w.ImapStreamOverride = async () => { var t = new TcpClient(); await t.ConnectAsync(IPAddress.Loopback, port, ct); return t.GetStream(); };
        w.Show("posteingang"); Pump();
        Assert.NotNull(w.TryFind<Grid>("inbox-columns"));
        Assert.NotNull(w.TryFind<Button>("inbox-folder-alle"));
        Assert.NotNull(w.TryFind<Avalonia.Controls.Border>("inbox-read-empty"));
        await w.OpenInboxMail(7); await server.WaitAsync(TimeSpan.FromSeconds(10), ct); l.Stop();
        w.Show("posteingang"); Pump();
        w.Find<Button>("inbox-cal-0").Press(); Pump();
        var ev = Assert.Single(w.Cal.All());
        Assert.Equal(new DateTime(2026, 11, 5, 19, 30, 0), ev.Start);
        Assert.Equal("Chorprobe", ev.Title);
        w.Find<Button>("inbox-new").Press(); Pump();
        Assert.NotNull(w.TryFind<TextBox>("send-to"));
        w.Find<Button>("send-discard").Press(); Pump();
        Assert.Null(w.TryFind<TextBox>("send-to"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-11 F1/F2/U1/U2: Vorhaben mit Ring, Kanban verschieben, Zeitplan mit Meilenstein, Dateien des Vorhabens")]
    public void Suite_views()
    {
        var (w, _, _) = Ui.Open();
        w.Show("vorhaben"); Pump();
        w.Find<TextBox>("suite-name").Text = "Sommerfest"; w.Find<TextBox>("suite-start").Text = "01.11.2026";
        w.Find<Button>("suite-kit-event").Press(); Pump();
        Assert.NotNull(w.TryFind<Avalonia.Controls.Shapes.Arc>("suite-ring-0"));
        w.Find<Button>("suite-mode-kanban").Press(); Pump();
        var dir = Path.Combine(w.SuiteFolder, "Sommerfest"); var first = CreationSuite.Steps(dir)[0];
        w.Find<Button>($"suite-right-{first.Line}").Press(); Pump();
        Assert.Equal(1, CreationSuite.Steps(dir)[0].State);
        Assert.Equal(2, w.Find<StackPanel>("suite-col-1").Children.Count); // Überschrift + Karte
        w.Find<Button>("suite-mode-zeitplan").Press(); Pump();
        Assert.Contains("◆", string.Join("|", w.GetLogicalTexts()));
        w.Find<TextBox>("suite-due-date").Text = "20.11.2026"; w.Find<Button>("suite-due-set").Press(); Pump();
        Assert.Equal(new DateTime(2026, 11, 20), CreationSuite.Steps(dir)[0].Due);
        File.WriteAllText(Path.Combine(dir, "Einladungen", "gaeste.csv"), "Name\nAnna\n");
        w.Find<Button>("suite-mode-dateien").Press(); Pump();
        w.Find<Button>("suite-file-0").Press(); Pump();
        Assert.Equal("tabelle", w.CurrentView);
        w.Close();
    }
}
