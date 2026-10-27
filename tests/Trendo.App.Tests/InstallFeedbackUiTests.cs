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

    [AvaloniaFact(DisplayName = "Zusatzprogramme: Vollpaket-Karte mit Ordnerwahl; falscher Ordner wird erklärt")]
    public void Vollpaket_card()
    {
        var (w, _, root) = Ui.Open();
        w.Show("components"); Pump();
        Assert.NotNull(w.TryFind<Border>("vollpaket-card"));
        w.Find<TextBox>("vollpaket-dir").Text = root;
        w.Find<Button>("vollpaket-install").Press(); Pump();
        Assert.Contains("keinen gültigen Ordner", w.Find<TextBlock>("vollpaket-msg").Text);
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
