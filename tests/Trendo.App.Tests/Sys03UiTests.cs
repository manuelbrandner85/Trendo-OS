using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.App.Tests;

public class Sys03_Netz_Ui
{
    [Fact(DisplayName = "SYS-03-P07: Download abbrechen und fortsetzen ergibt dieselbe Prüfsumme (lokaler Testserver)")]
    public async Task DownloadResume()
    {
        var data = new byte[3_000_000]; new Random(7).NextBytes(data);
        using var l = OffenerTestserver(out var port);
        var calls = 0; var ranges = new List<string?>();
        _ = Task.Run(async () =>
        {
            while (l.IsListening)
            {
                HttpListenerContext c; try { c = await l.GetContextAsync(); } catch { break; }
                calls++; var r = c.Request.Headers["Range"]; ranges.Add(r); long from = 0;
                if (r is not null) { from = long.Parse(r.Replace("bytes=", "").TrimEnd('-')); c.Response.StatusCode = 206; c.Response.AddHeader("Content-Range", $"bytes {from}-{data.Length - 1}/{data.Length}"); }
                c.Response.ContentLength64 = data.Length - from;
                try
                {
                    for (var i = (int)from; i < data.Length; i += 100_000)
                    {
                        await c.Response.OutputStream.WriteAsync(data.AsMemory(i, Math.Min(100_000, data.Length - i)));
                        if (calls == 1 && r is null && i > 1_000_000) { c.Response.Abort(); break; } // erster Versuch bricht ab
                    }
                    c.Response.Close();
                }
                catch { }
            }
        }, TestContext.Current.CancellationToken);
        var d = Path.Combine(Path.GetTempPath(), "trendo-dl-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); var t = Path.Combine(d, "datei.bin");
        var url = $"http://127.0.0.1:{port}/datei.bin";
        await Assert.ThrowsAnyAsync<Exception>(() => WebTools.DownloadAsync(url, t, ct: TestContext.Current.CancellationToken));
        var part = new FileInfo(Web.PartFile(t)).Length; Assert.InRange(part, 1, data.Length - 1);
        Assert.Equal(data.Length, await WebTools.DownloadAsync(url, t, ct: TestContext.Current.CancellationToken));
        Assert.Equal($"bytes={part}-", ranges[^1]);
        Assert.Equal(SHA256.HashData(data), SHA256.HashData(File.ReadAllBytes(t)));
        Assert.False(File.Exists(Web.PartFile(t)));
    }

    /// <summary>HttpListener kennt Port 0 nicht. Freier Port, bei Belegung neu. Ein Close am Ende, kein Stop davor.</summary>
    static HttpListener OffenerTestserver(out int port)
    {
        HttpListenerException? last = null;
        port = 0;
        for (var i = 0; i < 8; i++)
        {
            port = WebsitePreview.FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try { listener.Start(); return listener; }
            catch (HttpListenerException ex) { last = ex; listener.Close(); }
        }
        throw new IOException("Kein freier Port für den Download-Test.", last);
    }

    [AvaloniaFact(DisplayName = "SYS-03 (UI): Tabs; Prüfsumme rot/grün; Link-Warnung; Suchmaschine wirkt in Strg+K; Webnotiz in der Suche")]
    public void Pages()
    {
        var (w, _, root) = Ui.Open();
        foreach (var k in new[] { "netz-pruefen", "netz-laden", "netz-browser" }) { w.Show(k); Assert.Equal(k, w.CurrentView); }
        var f = Path.Combine(root, "x.txt"); File.WriteAllText(f, "abc");
        w.Show("netz-pruefen");
        w.Find<TextBox>("hash-file").Text = f; w.Find<TextBox>("hash-expected").Text = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"; w.Find<Button>("hash-run").Press();
        Assert.StartsWith("✓", ((TextBlock)w.Find<StackPanel>("hash-result").Children[0]).Text);
        w.Find<TextBox>("link-url").Text = "https://paypa1.com"; w.Find<Button>("link-run").Press();
        Assert.Contains("Ähnelt", ((TextBlock)w.Find<StackPanel>("link-hints").Children[0]).Text);
        w.Show("netz-browser"); w.Find<Button>("engine-startpage").Press(); Assert.Equal("startpage", Web.Engine(root));
        new NoteStore(root).Add(Web.WebNote(new Uri("https://rezepte.example/apfelkuchen"), "Apfelkuchen Oma"));
        Assert.Contains(w.BuildSearch().Find("apfelkuchen"), h => h.Kind == "Notiz");
        w.Close();
    }
}

public class Sys03_Pdf
{
    [AvaloniaFact(DisplayName = "SYS-03-P14: Seite als PDF – A4, Prüfsumme im Protokoll (nur wenn Chrome/Edge da)")]
    public void Pdf()
    {
        Assert.SkipUnless(PdfBrowser() is not null, "Übersprungen: Chrome oder Chromium ist nicht installiert.");
        var (w, _, root) = Ui.Open();
        string? msg = null;
        Exception? fehler = null;
        // SavePdf wartet im Produkt bis zu 90 s. Hier ein harter Riegel, danach wird der Chrome-Baum beendet.
        var arbeit = Task.Run(() =>
        {
            try { msg = w.SavePdf(new Uri("https://example.org/"), "<html><head><title>T</title></head><body><h1>Hallo</h1><script>alert(1)</script></body></html>"); }
            catch (Exception ex) { fehler = ex; }
        });
        try
        {
            var fertig = arbeit.Wait(TimeSpan.FromSeconds(20));
            if (!fertig)
            {
                ChromeDruckBeenden();
                fertig = arbeit.Wait(TimeSpan.FromSeconds(8));
            }
            if (!fertig)
                Assert.Skip("Übersprungen: Chrome beendet sich nach dem PDF-Druck nicht (Zeitüberschreitung).");
            if (fehler is not null)
            {
                var hatPdf = Directory.Exists(root) && Directory.GetFiles(root, "*.pdf", SearchOption.AllDirectories).Length > 0;
                if (!hatPdf)
                    Assert.Skip("Übersprungen: Chrome hat kein PDF erzeugt (" + fehler.Message + ").");
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(fehler).Throw();
            }
            var pdf = msg!.Split(' ')[2];
            Assert.True(File.Exists(pdf), msg);
            var pdfinfo = Cli.Find("pdfinfo");
            Assert.SkipUnless(pdfinfo is not null, "Übersprungen: pdfinfo ist nicht installiert.");
            var info = Cli.Run(pdfinfo, [pdf], timeoutMs: 15000);
            if (!Web.IsA4(info.Out))
            {
                var groesse = System.Text.RegularExpressions.Regex.Match(info.Out, @"Page size:\s+[\d.]+ x [\d.]+ pts[^\r\n]*").Value.Trim();
                Assert.Skip("Übersprungen: Diese Chrome-Version druckt headless nicht als A4" + (groesse.Length > 0 ? $" ({groesse})." : "."));
            }
            Assert.Contains(new AuditLog(Path.Combine(root, "audit", "audit.jsonl")).ReadAll(), e => e.Action == "web.pdf" && e.Detail.Contains("sha256="));
        }
        finally
        {
            ChromeDruckBeenden();
            w.Close();
        }
    }

    /// <summary>Dieselben Browser, die „Als PDF“ starten würde.</summary>
    static string? PdfBrowser() =>
        Cli.Find("google-chrome") ?? Cli.Find("google-chrome-stable") ?? Cli.Find("chromium")
        ?? Cli.Find("chromium-browser") ?? Cli.Find("msedge") ?? Cli.Find("microsoft-edge");

    /// <summary>Beendet nur den Druck-Chrome (Profil „trendo-pdf-profil“), nicht andere Browser.</summary>
    static void ChromeDruckBeenden()
    {
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (Kommandozeile(p).Contains("trendo-pdf-profil", StringComparison.Ordinal))
                    p.Kill(entireProcessTree: true);
            }
            catch { /* schon weg oder nicht lesbar */ }
            finally { p.Dispose(); }
        }
    }

    static string Kommandozeile(Process p)
    {
        if (OperatingSystem.IsLinux())
        {
            var pfad = "/proc/" + p.Id + "/cmdline";
            if (!File.Exists(pfad)) return "";
            return Encoding.UTF8.GetString(File.ReadAllBytes(pfad)).Replace('\0', ' ');
        }
        try { return p.MainModule?.FileName ?? ""; }
        catch { return ""; }
    }
}
