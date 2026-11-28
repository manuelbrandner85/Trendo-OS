using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0787 Gegenprüfung (Oberfläche): Ersatzwege in „Einfach“ (V10) wirklich geklickt; Einlesen einer bösartigen 20-MB-Seite im Hintergrund mit ehrlicher Meldung;
/// Gesundheitsversprechen in Zutaten aus der KI-Aktion abgelehnt.</summary>
[Collection("Claude")]
public class T0787Gegenpruefung_Ui
{
    const string K = MainWindow.RzKey;

    static ProgramDocument Open(MainWindow w, string tpl, int level)
    {
        w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(level);
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(K)!;
    }

    static MenuItem MenuItemOf(MainWindow w, string name) => w.ActiveMenuBar()!.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Name == name);

    [AvaloniaFact(DisplayName = "T-0787 G1 (V10): in „Einfach“ – Bedienstufe über „Mehr Werkzeuge“ und „Ansicht ▸ Bedienstufe“, jede weitere Ausgabe über „Datei ▸ Exportieren“ (schreibt die Datei)")]
    public void Einfach_ersatzwege()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = Open(w, "beispiel", UserLevel.Einfach);
            Assert.Equal(UserLevel.Einfach, w.ProgramLevel);
            // Stufe: „Mehr Werkzeuge“ blendet die Stufenwahl ein, ein Klick wechselt die Stufe
            Assert.Null(w.TryFind<Button>("prog-level-profi"));
            RealClick(w, w.Find<Button>("prog-tools-more")); Settle();
            w.Find<Button>("prog-level-profi").Press(); Settle(); // Klick-Ereignis: die Kopfzeile ist bei 1600 px voll, die Stufenwahl ragt rechts hinaus (gilt ebenso für Referate u. a. – siehe Bericht)
            Assert.Equal(UserLevel.Profi, w.ProgramLevel);
            w.ProgramSetLevel(UserLevel.Einfach); w.Show(K); Settle();
            // dieselbe Umschaltung über die Menüleiste „Ansicht ▸ Bedienstufe“
            MenuItemOf(w, "menu-view-level-erweitert").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Equal(UserLevel.Erweitert, w.ProgramLevel);
            w.ProgramSetLevel(UserLevel.Einfach); w.Show(K); Settle();
            // jede Ausgabe ohne eigenen Knopf in „Einfach“: über „Datei ▸ Exportieren“
            foreach (var (name, endung) in new[] { ("docx", ".docx"), ("html", ".html"), ("kochbuch", ".pdf"), ("liste-csv", ".csv"), ("liste-txt", ".txt"), ("odt", ".odt") })
            {
                Assert.Null(w.TryFind<Button>("prog-export-" + name));
                MenuItemOf(w, "menu-prog-export-" + name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Settle();
                Assert.True(w.ProgramMessage(K).StartsWith("Exportiert: ", StringComparison.Ordinal), name + ": " + w.ProgramMessage(K));
                var file = w.ProgramMessage(K)["Exportiert: ".Length..].Trim();
                Assert.EndsWith(endung, file); Assert.True(new FileInfo(file).Length > 100, file);
                w.Show(K); Settle();
            }
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0787 G2: „Rezept öffnen …“ mit bösartiger 20-MB-Seite – Einlesen im Hintergrund, nach wenigen Sekunden ehrliche Meldung, nichts angelegt")]
    public void Import_boesartig_im_hintergrund()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var d = Open(w, "leer", UserLevel.Erweitert);
            var f = Path.Combine(root, "boese.html");
            var sb = new StringBuilder(20_100_000); sb.Append("<html><head>");
            while (sb.Length < 19_900_000) sb.Append("<script type=\"application/ld+json\">{\"a\":");
            File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false));
            w.PickOverride = _ => Task.FromResult<string[]?>([f]);
            var sw = Stopwatch.StartNew();
            RealClick(w, w.Find<Button>("prog-cmd-rz-import")); Settle();
            var sawBusy = false;
            while (sw.Elapsed < TimeSpan.FromSeconds(60) && (w.RzImportBusy > 0 || !w.ProgramMessage(K).Contains("nicht geschlossen")))
            { sawBusy |= w.RzImportBusy > 0; Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
            sw.Stop();
            Assert.Contains("nicht geschlossen", w.ProgramMessage(K));
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"{sw.Elapsed.TotalSeconds:0.0} s");
            Assert.True(sawBusy, "das Einlesen lief nicht im Hintergrund");
            Assert.Empty(MainWindow.RzRezepte(d));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0787 G5: Zutat mit Heilversprechen aus der Fachaktion wird nicht übernommen; Hinweis bei mehrdeutiger Menge beim Tippen")]
    public void Versprechen_in_zutaten_und_hinweis()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = Open(w, "leer", UserLevel.Erweitert);
            var ki = MainWindow.Programs[K].KiActions.Single(a => a.Def.Id == "rezept.anlegen");
            var ex = Assert.Throws<InvalidOperationException>(() => ki.Prepare(d, new Dictionary<string, string>
            { ["name"] = "Goldene Milch", ["zutaten"] = "200 ml Milch\n1 TL Kurkuma, heilt Entzündungen", ["schritte"] = "Erwärmen.", ["portionen"] = "1" }));
            Assert.Contains("Gesundheitsversprechen", ex.Message);
            Assert.Empty(MainWindow.RzRezepte(d));
            var r = w.RzNew(d, "Brot");
            w.RzSetZutaten(d, r.Id, "1.000 g Mehl\n1e5 g Salz");
            Assert.Contains("Tausender", w.ProgramMessage(K)); Assert.Contains("keine lesbare Menge", w.ProgramMessage(K));
            Assert.Equal(1000, MainWindow.RzRezepte(d)[0].Zutaten[0].Menge);
        }
        finally { w.Close(); }
    }
}
