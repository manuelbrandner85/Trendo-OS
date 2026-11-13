using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
using S = Trendo.Core.Sys.DownloadQueue.State;

namespace Trendo.App.Tests;

/// Runde 6: Lernkarten mit Lernplan, Download-Manager, Fahrtenbuch (Korrigieren, Stornieren, Festschreiben) – echte Klicks auf der Oberfläche.
public class RoundSix_Ui
{
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    [AvaloniaFact(DisplayName = "Lernplan: Plan mit Prüfungstermin anlegen → Tagesplan mit Ziel, Kalender mit Prüfungstag; „Jetzt lernen“ lernt in Plan-Reihenfolge und hält das Tempo")]
    public void Learn_plan()
    {
        var (w, _, _) = Ui.Open();
        for (var i = 0; i < 20; i++) w.Learn.Add("Französisch", "mot " + i, "Wort " + i);
        w.LearnDeck = "Französisch"; w.LearnMode = "plan"; w.Show("lernen");
        Assert.Contains("Heute: 0 Wiederholungen und 20 neue Karten", w.Find<TextBlock>("learn-today-text").Text); // ohne Plan: alles
        var exam = DateTime.Today.AddDays(13);
        w.Find<TextBox>("learn-plan-exam-0").Text = exam.ToString("dd.MM.yyyy");
        w.Find<Button>("learn-plan-save-0").Press();
        Assert.Contains("2 neue Karten pro Tag", w.Find<TextBlock>("learn-plan-msg").Text); // 20 neue / 10 Lerntage
        Assert.Contains("Heute: 0 Wiederholungen und 2 neue Karten", w.Find<TextBlock>("learn-today-text").Text);
        Assert.True(w.Find<Border>("learn-cal-13").BorderThickness.Left >= 2); // Prüfungstag golden umrandet
        w.Find<Button>("learn-plan-go").Press();
        Assert.Equal("lernen", w.LearnMode);
        w.LearnReveal(); w.LearnGrade(Grade.Gut); w.LearnReveal(); w.LearnGrade(Grade.Gut);
        w.LearnMode = "plan"; w.Show("lernen");
        Assert.StartsWith("Tagesziel geschafft – 2 Karten gelernt", w.Find<TextBlock>("learn-today-text").Text);
        Assert.False(w.Find<Button>("learn-plan-go").IsEnabled);
        w.Find<TextBox>("learn-plan-exam-0").Text = "31.02."; w.Find<Button>("learn-plan-save-0").Press();
        Assert.Contains("TT.MM.JJJJ", w.Find<TextBlock>("learn-plan-msg").Text);
        w.Find<Button>("learn-plan-remove-0").Press(); Assert.Null(w.Learn.Plan("Französisch"));
    }

    [AvaloniaFact(DisplayName = "Download-Manager: mehrere Links auf einmal, höchstens N gleichzeitig, Fortschritt, Anhalten/Fortsetzen, Prüfsumme falsch → Fehler; ohne Netz (nachgebildet)")]
    public async Task Download_manager()
    {
        var (w, _, root) = Ui.Open();
        var gates = new Dictionary<string, TaskCompletionSource>(); var runs = new Dictionary<string, int>();
        MainWindow.DownloadRunOverride = async (j, p, ct) =>
        {
            var gate = new TaskCompletionSource(); lock (gates) { gates[j.Name] = gate; runs[j.Name] = runs.GetValueOrDefault(j.Name) + 1; }
            p.Report((50, 100));
            using (ct.Register(() => gate.TrySetCanceled())) await gate.Task;
            await File.WriteAllTextAsync(j.Target, j.Name.StartsWith("b") ? "anders" : "Trendo", ct); p.Report((100, 100));
        };
        try
        {
            w.Downloads.MaxParallel = 2; w.Show("downloads");
            w.Find<TextBox>("dlm-url").Text = "https://example.org/a.txt\nhttps://example.org/b.txt https://example.org/c.txt";
            w.Find<Button>("dlm-add").Press();
            Assert.Contains("3 Downloads", w.Find<TextBlock>("dlm-msg").Text);
            Assert.All(w.Downloads.Jobs, j => Assert.StartsWith(root, j.Target)); // Testmodus: nie in den echten Download-Ordner
            await Until(() => gates.Count == 2);
            Assert.Equal([S.Lädt, S.Lädt, S.Wartet], w.Downloads.Jobs.Select(j => j.Status)); // höchstens 2 gleichzeitig
            w.DownloadPause(w.Downloads.Jobs[0].Id);
            for (var t = 0; t < 200 && !(w.Downloads.Jobs[0].Status == S.Angehalten && gates.ContainsKey("c.txt")); t++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
            Assert.True(w.Downloads.Jobs[0].Status == S.Angehalten && gates.ContainsKey("c.txt"), string.Join(", ", w.Downloads.Jobs.Select(j => $"{j.Name}={j.Status}:{j.Message}")) + " | Tore: " + string.Join(",", gates.Keys)); // freier Platz → c startet
            gates["c.txt"].SetResult(); await Until(() => w.Downloads.Jobs[2].Status == S.Fertig);
            w.DownloadResume(w.Downloads.Jobs[0].Id);
            await Until(() => runs["a.txt"] == 2 && w.Downloads.Jobs[0].Status == S.Lädt); // zweiter Lauf (macht dort weiter)
            lock (gates) foreach (var g in gates.Values) g.TrySetResult();
            await Until(() => w.Downloads.Jobs.All(j => j.Status != S.Lädt));
            w.Show("downloads");
            Assert.NotNull(w.TryFind<Button>("dlm-open-")); Assert.NotNull(w.TryFind<ProgressBar>("dlm-bar-0"));
            // falsche Prüfsumme
            w.Find<TextBox>("dlm-url").Text = "https://example.org/b2.txt"; w.Find<TextBox>("dlm-hash").Text = new string('0', 64);
            w.Find<Button>("dlm-add").Press();
            await Until(() => gates.ContainsKey("b2.txt")); gates["b2.txt"].SetResult();
            await Until(() => w.Downloads.Jobs.Last().Status == S.Fehler);
            Assert.Contains("Prüfsumme", w.Downloads.Jobs.Last().Message);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "download.hash_fail");
        }
        finally { MainWindow.DownloadRunOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Fahrtenbuch (T-0311): Fahrt korrigieren mit Grund, Fahrt stornieren mit Grund, Monat festschreiben – danach keine Änderung mehr möglich")]
    public void Logbook_fix_void_lock()
    {
        var (w, _, _) = Ui.Open();
        var y = DateTime.Today.Year; var m = DateTime.Today.Month == 1 ? 1 : DateTime.Today.Month - 1;
        w.Cars.Add("Kombi", "B-EM 123");
        w.Cars.Log("Kombi", new DateTime(y, m, 3), 1000, 1050, "dienstlich", "Kunde Max Beispiel", "Beratung", "Beispiel GmbH");
        w.Cars.Log("Kombi", new DateTime(y, m, 4), 1050, 1070, "privat", "Supermarkt");
        w.CarSel = "Kombi"; w.CarTab = "fahrten"; w.Show("fahrzeug");
        // neueste zuerst: Zeile 0 = 1050→1070, Zeile 1 = 1000→1050
        w.Find<Button>("car-t-fix-1").Press();
        w.Find<TextBox>("car-t-fix-dest").Text = "Kunde Erika Muster"; w.Find<TextBox>("car-t-fix-reason").Text = "Ziel falsch eingetragen";
        w.Find<Button>("car-t-fix-save").Press();
        var trip = w.Cars.Get("Kombi").Trips.Single(t => t.StartKm == 1000);
        Assert.Equal("Kunde Erika Muster", trip.Destination); Assert.Single(trip.Corrections);
        w.Find<Button>("car-t-void-0").Press();
        w.Find<TextBox>("car-t-void-reason").Text = "doppelt eingetragen";
        w.Find<Button>("car-t-void-yes").Press();
        Assert.True(w.Cars.Get("Kombi").Trips.Single(t => t.StartKm == 1050).Voided);
        w.Find<Button>($"car-t-lock-{m}").Press();
        Assert.NotNull(w.TryFind<TextBlock>($"car-t-locked-{m}"));
        Assert.Null(w.TryFind<Button>("car-t-fix-1")); // festgeschrieben: nicht mehr änderbar
        Assert.Throws<InvalidOperationException>(() => w.Cars.Correct("Kombi", trip, "nachträglich", end: 1060));
    }
}
