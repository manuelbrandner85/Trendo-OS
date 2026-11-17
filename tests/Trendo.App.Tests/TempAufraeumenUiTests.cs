using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0729: Nach einem typischen Testablauf (Fenster öffnen und schließen, echte Chrome-Vorschau, LibreOffice-Export wie in den Echtläufen)
/// bleibt im echten %TEMP% kein neuer trendo-*-Ordner zurück. Geprüft wird in einem eigenen Kindprozess dieses Testprogramms,
/// dessen „echtes Temp“ ein privater Prüfordner ist – so stört kein anderer Lauf auf dem PC und es wird nichts Fremdes gelöscht.</summary>
public class TempAufraeumen_Ui
{
    const string ChildVar = "TRENDO_T0729_KIND";

    [Fact(DisplayName = "T-0729: Typischer Ablauf im Kindprozess – danach liegt im Temp-Ordner nichts mehr (echtes Chrome/LibreOffice, falls vorhanden)")]
    public async Task Typical_run_leaves_nothing()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Trendo.App.Tests.exe");
        if (!OperatingSystem.IsWindows() || !File.Exists(exe)) { Assert.Skip("Übersprungen – Testprogramm als .exe nur unter Windows."); return; }
        // kurzer Pfad direkt im echten Temp (eigener Prüfordner dieses Tests): lange Temp-Pfade lassen LibreOffice still scheitern
        var probe = Path.Combine(TempDir.SystemTemp, "t0729-" + Guid.NewGuid().ToString("N")[..8]);
        var fake = Directory.CreateDirectory(probe).FullName; // das „echte Temp“ des Kindprozesses
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = AppContext.BaseDirectory };
            foreach (var a in new[] { "-method", typeof(TempAufraeumen_Ui).FullName + "." + nameof(Child_flow), "-reporter", "verbose" }) psi.ArgumentList.Add(a);
            psi.Environment[TempDir.OriginalVar] = fake;
            foreach (var v in new[] { "TMP", "TEMP" }) psi.Environment[v] = fake;
            psi.Environment[TempDir.SweepVar] = "0"; // der Kindprozess räumt nichts Fremdes – er soll nur hinter sich selbst aufräumen
            psi.Environment[ChildVar] = "1";
            using var p = Process.Start(psi)!;
            var outTask = p.StandardOutput.ReadToEndAsync(); var errTask = p.StandardError.ReadToEndAsync();
            Assert.True(p.WaitForExit(240_000), "Kindprozess hängt");
            var output = await outTask + await errTask;
            Assert.True(p.ExitCode == 0, "Kindprozess fehlgeschlagen:\n" + output);
            Assert.Contains("Total: 1, Errors: 0, Failed: 0, Skipped: 0", output); // der Ablauf lief wirklich (nicht übersprungen)
            var left = Directory.EnumerateFileSystemEntries(fake).Select(Path.GetFileName).ToList();
            Assert.True(left.Count == 0, "im Temp-Ordner liegt noch: " + string.Join(", ", left));
        }
        finally { TempDir.Delete(fake); }
    }

    /// <summary>Nur im Kindprozess: der typische Ablauf. Alles landet im Laufordner; Chrome-Profil und LibreOffice-Profil sind danach schon weg.</summary>
    [AvaloniaFact(DisplayName = "T-0729: Ablauf im Kindprozess (Fenster, Chrome-Vorschau, LibreOffice-Export)")]
    public async Task Child_flow()
    {
        if (Environment.GetEnvironmentVariable(ChildVar) != "1") { Assert.Skip("Läuft nur als Kindprozess von „Typischer Ablauf im Kindprozess“."); return; }
        var run = TempDir.TestRun!;
        Assert.Equal(Path.GetFullPath(Environment.GetEnvironmentVariable(TempDir.OriginalVar)!), Path.GetDirectoryName(run));

        var (w, _, root) = Ui.Open();
        Assert.StartsWith(run, root);
        w.Close();

        if (WebsitePreview.FindBrowser() is not null)
        {
            var site = Path.Combine(Path.GetTempPath(), "seite");
            var built = WebsiteStudio.Build(site, WebsiteStudio.Example("mikro"));
            var shot = await WebsitePreview.CaptureAsync(built.Directory, 800, 600, Path.Combine(site, "bild.png"), ct: TestContext.Current.CancellationToken);
            Assert.True(shot.Ok, shot.Note);
            Assert.Empty(Directory.GetDirectories(run, "trendo-chrome-*")); // Profil gleich nach dem Lauf gelöscht, nicht erst am Prozessende
        }

        var lo = Cli.Find("libreoffice") ?? new DependencyManager(new AuditLog(Path.Combine(Path.GetTempPath(), "t0729-needs.jsonl")), new RealProcessRunner()).ResolvePath(DependencyCatalog.Get("libreoffice"));
        if (lo is not null)
        {
            var docx = Path.Combine(Path.GetTempPath(), "brief.docx"); Ooxml.NewDocx(docx, "Brief", ["Hallo Welt"]);
            var (ok, msg, _) = await PdfExport.ConvertAsync(new RealProcessRunner(), lo, docx, TestContext.Current.CancellationToken);
            Assert.True(ok, msg);
            Assert.Empty(Directory.GetDirectories(run, "trendo-lo-*"));
        }
    }
}
