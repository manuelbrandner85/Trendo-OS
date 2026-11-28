using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0787 zweite Prüfrunde (Oberfläche): Einlesen im Hintergrund mit Sperre und Prüfung, ob das Buch noch offen ist; mehrdeutige Mengen aus der KI-Aktion markiert.</summary>
[Collection("Claude")]
public class T0787Runde2_Ui
{
    const string K = MainWindow.RzKey;

    static ProgramDocument Open(MainWindow w)
    {
        w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Erweitert);
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        return w.ProgramActive(K)!;
    }

    static void Bis(Task t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); } Assert.True(t.IsCompleted, "Einlesen endet nicht"); Settle(); }

    static string Datei(string root, string name, int rezepte)
    {
        var f = Path.Combine(root, name);
        var sb = new StringBuilder("<html><head><script type=\"application/ld+json\">[");
        for (var i = 0; i < rezepte; i++) sb.Append(i > 0 ? "," : "").Append($"{{\"@type\":\"Recipe\",\"name\":\"Rezept {i}\",\"recipeIngredient\":[\"200 g Mehl\",\"2 Eier\"],\"recipeInstructions\":\"Backen.\"}}");
        sb.Append("]</script></head></html>");
        File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false));
        return f;
    }

    [AvaloniaFact(DisplayName = "T-0787 R2-2: Buch geschlossen, während die Datei gelesen wird – nichts übernommen, ehrliche Meldung")]
    public void Geschlossen_waehrend_lesen()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var d = Open(w); var vorher = d.Data.ToJsonString();
            var t = w.RzImportFilesAsync(d, [Datei(root, "viele.html", 1500)]);
            Assert.Equal(1, w.RzImportBusy);
            w.ProgramClose(K, 0); Settle();
            Bis(t);
            Assert.Contains("geschlossen", w.ProgramMessage(K)); Assert.Contains("nichts übernommen", w.ProgramMessage(K));
            Assert.Equal(vorher, d.Data.ToJsonString());
            Assert.Equal(0, w.RzImportBusy);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0787 R2-2/R2-6: Sperre – solange ein Einlesen läuft, startet kein zweites (Doppelklick, Ablegen); danach geht es wieder")]
    public void Sperre_gegen_doppelt()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var d = Open(w);
            var f1 = Datei(root, "eins.html", 1500); var f2 = Datei(root, "zwei.html", 1);
            var t1 = w.RzImportFilesAsync(d, [f1]);
            var t2 = w.RzImportFilesAsync(d, [f2]); // zweiter Versuch während des ersten
            Assert.True(t2.IsCompleted);
            Assert.Contains("schon eine Datei", w.ProgramMessage(K));
            Bis(t1);
            Assert.Equal(1500, MainWindow.RzRezepte(d).Count);
            Assert.Single(d.History.Where(h => h.Contains("eingelesen")));
            Bis(w.RzImportFilesAsync(d, [f2]));
            Assert.Equal(1501, MainWindow.RzRezepte(d).Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0787 R2-1: KI-Aktion mit „1.500 kg Mehl“ – Zutat mit „Menge prüfen“ markiert und Hinweis in der Meldung")]
    public void Ki_mehrdeutig_markiert()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = Open(w);
            var ki = MainWindow.Programs[K].KiActions.Single(a => a.Def.Id == "rezept.anlegen");
            ki.Prepare(d, new Dictionary<string, string> { ["name"] = "Brot", ["zutaten"] = "1.500 kg Mehl\n2 TL Salz", ["schritte"] = "Kneten.\nBacken.", ["portionen"] = "2" });
            var r = Assert.Single(MainWindow.RzRezepte(d));
            Assert.Contains("Menge prüfen", r.Zutaten[0].Notiz); Assert.Equal(1.5, r.Zutaten[0].Menge);
            Assert.Contains("mehrdeutig", w.ProgramMessage(K));
        }
        finally { w.Close(); }
    }
}
