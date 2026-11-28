using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0787 dritte Prüfrunde (Oberfläche): mehrere Dateien mit gesammelten Fehlern; Sperre schon vor dem Dateidialog.</summary>
[Collection("Claude")]
public class T0787Runde3_Ui
{
    const string K = MainWindow.RzKey;

    static ProgramDocument Open(MainWindow w)
    {
        w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Erweitert);
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        return w.ProgramActive(K)!;
    }

    static void Bis(Func<bool> fertig) { for (var i = 0; i < 3000 && !fertig(); i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); } Assert.True(fertig(), "wird nicht fertig"); Settle(); }

    [AvaloniaFact(DisplayName = "T-0787 R3-5: drei Dateien ablegen/einlesen, zwei davon fehlerhaft – „1 von 3 Dateien eingelesen“ mit Name und Grund je Fehler, die gute Datei ist übernommen")]
    public void Mehrere_dateien_fehler_gesammelt()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var d = Open(w);
            var gut = Path.Combine(root, "gut.html");
            File.WriteAllText(gut, "<html><script type=\"application/ld+json\">{\"@type\":\"Recipe\",\"name\":\"Suppe\",\"recipeIngredient\":[\"1 Zwiebel\"]}</script></html>", new UTF8Encoding(false));
            var kaputt = Path.Combine(root, "kaputt.json"); File.WriteAllText(kaputt, "{ kein json");
            var leer = Path.Combine(root, "ohne.html"); File.WriteAllText(leer, "<html><body>nur Text</body></html>");
            var t = w.RzImportFilesAsync(d, [kaputt, gut, leer]);
            Bis(() => t.IsCompleted);
            var msg = w.ProgramMessage(K);
            Assert.StartsWith("1 von 3 Dateien eingelesen", msg);
            Assert.Contains("„kaputt.json“", msg); Assert.Contains("JSON", msg);
            Assert.Contains("„ohne.html“", msg); Assert.Contains("Kein schema.org-Rezept", msg);
            Assert.Single(MainWindow.RzRezepte(d)); Assert.Equal(0, w.RzImportBusy);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0787 R3-6: Sperre schon vor dem Dateidialog – ein zweiter Klick auf „Rezept öffnen …“ öffnet keinen zweiten Dialog")]
    public void Sperre_vor_dialog()
    {
        var (w, _, _) = Ui.Open();
        var dialog = new TaskCompletionSource<string[]?>(); var aufrufe = 0;
        try
        {
            var d = Open(w);
            w.PickOverride = _ => { aufrufe++; return dialog.Task; };
            RealClick(w, w.Find<Button>("prog-cmd-rz-import")); Settle();
            Assert.Equal(1, aufrufe); Assert.Equal(1, w.RzImportBusy);
            RealClick(w, w.Find<Button>("prog-cmd-rz-import")); Settle();
            Assert.Equal(1, aufrufe); // kein zweiter Dialog
            Assert.Contains("schon eine Datei", w.ProgramMessage(K));
            dialog.SetResult(null); // Dialog abgebrochen
            Bis(() => w.RzImportBusy == 0);
            Assert.Empty(MainWindow.RzRezepte(d));
        }
        finally { w.PickOverride = null; w.Close(); }
    }
}
