using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0736 (V-10, Entscheidung des Nutzers 17.11.2026 „Studio-Name überall“): Eine Seite heißt überall gleich. Ist eine Seite der
/// Einstellungen oder der KI-Zentrale die Startseite eines Studios, steht in der linken Leiste der Studio-Name – derselbe wie im Fenstertitel,
/// im Seitenkopf und in der Studio-Übersicht. Vorher hießen 18 Seiten in der Leiste anders (z. B. „Leistung messen“ statt „Qualität prüfen“).</summary>
public class T0736Seitennamen_Ui
{
    static MainWindow Open()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Settings.SetupDone = true;
        w.ProgramSetLevel(UserLevel.Profi); // alle Seiten der Leiste sichtbar
        return w;
    }

    static string NavText(MainWindow w, string shell, string key)
    {
        var item = w.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == shell + "-nav-" + key);
        Assert.True(item is not null, $"{key}: kein Eintrag in der linken Leiste");
        return item!.GetLogicalDescendants().OfType<TextBlock>().Single().Text ?? "";
    }

    [AvaloniaFact(DisplayName = "T-0736: Jede Seite heißt in der linken Leiste wie im Fenstertitel; Studio-Startseiten tragen den Studio-Namen")]
    public void Left_bar_uses_the_one_name()
    {
        var w = Open();
        try
        {
            var studioPages = 0;
            foreach (var (shell, groups) in new[] { ("settings", MainWindow.SettingsGroups), ("ki", MainWindow.KiGroups) })
                foreach (var (key, label) in groups.SelectMany(g => g.Pages))
                {
                    if (!w.PageKeys.Contains(key)) continue;
                    w.Show(key);
                    var expected = key is "settings" or "ki" ? label : w.PageTitle(key); // Einstiegsseiten: Fenster heißt wie die App, Leiste wie die Seite
                    Assert.True(NavText(w, shell, key) == expected, $"{key}: Leiste „{NavText(w, shell, key)}“, Fenster „{expected}“");
                    if (w.ModuleOf(key) is { } m && StudioCatalog.For(m) is { } info && w.PageForStudio(m) == key)
                    {
                        studioPages++;
                        Assert.Equal(info.Name, NavText(w, shell, key));
                    }
                }
            Assert.True(studioPages >= 15, $"nur {studioPages} Studio-Startseiten in Einstellungen und KI-Zentrale");
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0736: Die Suche zeigt den Studio-Namen und findet die Seite auch unter dem früheren Leistennamen")]
    public void Search_finds_old_and_new_name()
    {
        var w = Open();
        try
        {
            // T-0763: Startseite des Studios „Qualität prüfen“ ist jetzt „Alles prüfen“ (vorher „Leistung messen“) – gleiche Prüfung an der neuen Startseite
            var page = MainWindow.SettingsGroups.SelectMany(g => g.Pages).First(p => p.Key == "qual-check");
            var studio = w.ShellLabel(page.Key, page.Label);
            Assert.NotEqual(page.Label, studio); // die Seite ist Startseite eines Studios mit anderem Namen
            Assert.Contains(w.SettingsSearch(page.Label), h => h.Key == page.Key && h.Label == studio); // alter Name findet, gezeigt wird der neue
            Assert.Contains(w.SettingsSearch(studio), h => h.Key == page.Key);
            var plain = MainWindow.SettingsGroups.SelectMany(g => g.Pages).First(p => p.Key == "lizenz");
            Assert.Equal(plain.Label, w.ShellLabel(plain.Key, plain.Label)); // keine Studio-Startseite: Leistenname bleibt
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }
}
