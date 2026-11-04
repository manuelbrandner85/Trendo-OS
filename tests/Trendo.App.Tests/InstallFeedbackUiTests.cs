using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Befunde 27.10.2026: Einrichtungsbildschirm zu kurz, Programme „fehlt“, KI-Lade-Knöpfe grau ohne Erklärung.</summary>
public class InstallFeedbackUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Einrichtung: Ergebnis bleibt stehen (mit fehlgeschlagenen Paketen), schließt erst nach „Los geht's“")]
    public async Task Finish_screen_stays()
    {
        var (w, _, root) = Ui.Open();
        // Vollpaket mit einem kaputten Paket
        var prog = Path.Combine(root, "vp", "Programme"); Directory.CreateDirectory(prog);
        File.WriteAllText(Path.Combine(prog, "x.zip"), "kein zip");
        File.WriteAllText(Path.Combine(prog, "manifest.json"), "{\"schema\":1,\"programs\":[{\"id\":\"qpdf\",\"name\":\"qpdf\",\"version\":\"1\",\"license\":\"Apache-2.0\",\"file\":\"x.zip\",\"sha256\":\"" + new string('0', 64) + "\",\"bytes\":8,\"exe\":\"bin/qpdf.exe\",\"source_url\":\"https://github.com/qpdf/qpdf\"}]}");
        var r = await w.InstallVollpaket(prog); Pump();
        Assert.NotNull(r); Assert.Single(r!.Failed);
        Assert.True(w.SetupOverlayOpen);
        Assert.True(w.SetupFinishShown);
        Assert.Contains("qpdf", w.Find<TextBlock>("setup-overlay-problems").Text);
        Assert.True(w.MayClose(false)); // am Ergebnis darf man schließen
        w.Find<Button>("setup-overlay-done").Press(); Pump();
        Assert.False(w.SetupOverlayOpen);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Einrichtung: „Abbrechen“ beendet die Installation und schließt den Bildschirm; danach kein Ergebnisbildschirm")]
    public async Task Cancel_closes()
    {
        var (w, _, root) = Ui.Open();
        w.ShowSetupOverlay("Trendo OS richtet deine Programme ein", 3, 20, "Krita wird eingerichtet …"); Pump();
        Assert.True(w.SetupOverlayOpen);
        Assert.NotNull(w.TryFind<Avalonia.Controls.Viewbox>("trendo-logo"));
        w.Find<Button>("setup-overlay-cancel").Press(); Pump();
        Assert.False(w.SetupOverlayOpen); Assert.True(w.SetupCancelRequested);
        Assert.NotNull(w.TryFind<Border>("toast"));
        // echte Einrichtung: Abbruch vor dem ersten Paket – nichts wird eingerichtet, kein Ergebnisbildschirm
        var prog = Path.Combine(root, "vp", "Programme"); Directory.CreateDirectory(prog);
        File.WriteAllText(Path.Combine(prog, "manifest.json"), "{\"schema\":1,\"programs\":[{\"id\":\"qpdf\",\"name\":\"qpdf\",\"version\":\"1\",\"license\":\"Apache-2.0\",\"file\":\"x.zip\",\"sha256\":\"" + new string('0', 64) + "\",\"bytes\":8,\"exe\":\"bin/qpdf.exe\",\"source_url\":\"https://github.com/qpdf/qpdf\"}]}");
        var task = w.InstallVollpaket(prog);
        w.CancelSetup(); Pump();
        var r = await task; Pump();
        Assert.True(r!.Canceled || r.Failed.Count == 1); // je nach Zeitpunkt: vor dem Paket abgebrochen oder schon versucht
        Assert.False(w.SetupFinishShown);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "KI-Fortschritt: laufende KI-Aufgabe erscheint oben mit Bezeichnung, Phase und Abbrechen; danach verschwindet sie")]
    public void Ki_progress_panel()
    {
        var (w, _, _) = Ui.Open();
        Trendo.Core.KiActivity act;
        using (Trendo.Core.KiActivityHub.Label("Folien entwerfen")) act = Trendo.Core.KiActivityHub.Begin();
        Trendo.Core.KiActivityHub.Update(act, delta: "Erste Folie ");
        Pump();
        Assert.Equal(1, w.KiProgressRows);
        Assert.Contains("Folien entwerfen", w.Find<TextBlock>("ki-progress-label").Text);
        Assert.Contains("schreibt", w.Find<TextBlock>("ki-progress-label").Text);
        w.Find<Button>("ki-progress-cancel-" + act.Id).Press(); Pump();
        Assert.True(act.Token.IsCancellationRequested);
        Trendo.Core.KiActivityHub.End(act, false); Pump();
        Assert.Equal(0, w.KiProgressRows);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Zusatzprogramme: keine Ordnerwahl mehr – „von GitHub laden“; ohne Internet bleibt ein Merker zum Nachladen beim nächsten Start")]
    public async Task Vollpaket_card()
    {
        var (w, _, root) = Ui.Open(offline: true);
        w.Show("components"); Pump();
        Assert.NotNull(w.TryFind<Border>("vollpaket-card"));
        Assert.Null(w.TryFind<TextBox>("vollpaket-dir"));
        Assert.Null(await w.InstallVollpaketOnline());
        Assert.True(File.Exists(Path.Combine(w.ToolsRootForTests, Trendo.Core.Vollpaket.PendingMarker)));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Programme von GitHub: Programmliste vom neuesten Release, kleine zuerst, mit Prüfsumme – Selbstgebautes (qpdf) und Bei-Bedarf-Programme (Blender) lädt das Setup nicht")]
    public async Task Vollpaket_online()
    {
        var (w, _, root) = Ui.Open();
        byte[] Zip(string exe) { using var ms = new MemoryStream(); using (var z = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true)) using (var sw = new StreamWriter(z.CreateEntry(exe).Open())) sw.Write("x"); return ms.ToArray(); }
        var big = Zip("program/soffice.exe"); var small = Zip("mpv.exe"); var q = Zip("bin/qpdf.exe"); var bl = Zip("blender.exe");
        string Sha(byte[] b) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(b));
        var manifest = System.Text.Json.JsonSerializer.Serialize(new { schema = 1, programs = new object[] {
            new { id = "libreoffice", name = "LibreOffice", version = "25", license = "MPL-2.0", file = "libreoffice.zip", sha256 = Sha(big), bytes = (long)big.Length + 100000, exe = "program/soffice.exe", source_url = "https://www.libreoffice.org" },
            new { id = "mpv", name = "mpv", version = "0.40", license = "GPL", file = "mpv.zip", sha256 = Sha(small), bytes = (long)small.Length, exe = "mpv.exe", source_url = "https://mpv.io" },
            new { id = "qpdf", name = "qpdf", version = "12", license = "Apache-2.0", file = "qpdf.zip", sha256 = Sha(q), bytes = (long)q.Length, exe = "bin/qpdf.exe", source_url = "https://github.com/qpdf/qpdf" },
            new { id = "blender", name = "Blender", version = "4", license = "GPL", file = "blender.zip", sha256 = Sha(bl), bytes = (long)bl.Length, exe = "blender.exe", source_url = "https://www.blender.org" } } });
        var asked = new List<string>();
        w.VollpaketOnlineOverride = u => { lock (asked) asked.Add(u); return Task.FromResult(u.EndsWith("manifest.json") ? System.Text.Encoding.UTF8.GetBytes(manifest) : u.EndsWith("mpv.zip") ? small : u.EndsWith("qpdf.zip") ? q : u.EndsWith("blender.zip") ? bl : big); };
        Directory.CreateDirectory(w.ToolsRootForTests); File.WriteAllText(Path.Combine(w.ToolsRootForTests, Trendo.Core.Vollpaket.PendingMarker), "offline");
        var r = await w.InstallVollpaketOnline(); Pump();
        Assert.NotNull(r);
        Assert.Empty(r!.Failed);
        Assert.StartsWith("https://github.com/manuelbrandner85/Trendo-OS/releases/latest/download/", asked[0]);
        Assert.True(asked.IndexOf(asked.First(a => a.EndsWith("mpv.zip"))) < asked.IndexOf(asked.First(a => a.EndsWith("libreoffice.zip")))); // klein zuerst
        Assert.True(File.Exists(Path.Combine(w.ToolsRootForTests, "mpv", "mpv.exe")));
        Assert.DoesNotContain(asked, a => a.EndsWith("qpdf.zip")); // kann Trendo selbst
        Assert.DoesNotContain(asked, a => a.EndsWith("blender.zip")); // erst, wenn man das 3D-Studio benutzt
        w.Close();
    }

    [AvaloniaFact(DisplayName = "KI-Quellen und KI-Bibliothek: ohne laufendes Ollama ein Hinweis mit nächstem Schritt, gesperrte Knöpfe sagen warum")]
    public void Ollama_hint()
    {
        var (w, _, _) = Ui.Open();
        w.Show("ki-quellen"); Pump();
        Assert.NotNull(w.TryFind<Border>("ollama-hint"));
        var pull = w.TryFind<Button>("model-pull-");
        Assert.NotNull(pull);
        if (!pull!.IsEnabled) Assert.NotNull(ToolTip.GetTip(pull));
        w.Find<Button>("ollama-setup").Press(); Pump();
        Assert.Equal("components", w.CurrentView);
        w.Close();
    }
}
