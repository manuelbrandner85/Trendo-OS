using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Runde 7: Neues Projekt als Projektmappe – Projekte und Mappen-Baum links, Übersicht mit Kennzahlen, Aufgaben-Tafel, Zeitplan-Leiste und letzten Dateien in einem Fenster; Umbenennen.
public class Projektmappe_Ui
{
    [AvaloniaFact(DisplayName = "Projektmappe: ohne Projekt Auswahl der Ziele; danach Übersicht mit Tafel, Zeitplan-Leiste, Dateien und Baum; zweites Projekt, wechseln, umbenennen")]
    public void Projektmappe()
    {
        var (w, launcher, _) = Ui.Open();
        w.Show("vorhaben");
        Assert.NotNull(w.TryFind<StackPanel>("suite-newcard")); Assert.Null(w.TryFind<Button>("suite-new-cancel")); // erstes Projekt: nichts abzubrechen
        w.Find<TextBox>("suite-name").Text = "Gartenfest"; w.Find<TextBox>("suite-start").Text = DateTime.Today.AddDays(-10).ToString("dd.MM.yyyy");
        w.Find<Button>("suite-kit-event").Press();
        var dir = Path.Combine(w.SuiteFolder, "Gartenfest");
        // Übersicht: alles in einem Fenster
        Assert.Null(w.TryFind<StackPanel>("suite-newcard"));
        Assert.NotNull(w.TryFind<StackPanel>("suite-overview"));
        Assert.NotNull(w.TryFind<Grid>("suite-kanban"));
        Assert.Equal(6, w.Find<Canvas>("suite-mini-plan").Children.Count - 1); // 6 Balken + Heute-Linie
        Assert.Contains("Datum und Ort", ((TextBlock)((StackPanel)w.Find<Border>("suite-kpi-next").Child!).Children[1]).Text);
        Assert.Contains("1 Schritt", ((TextBlock)((StackPanel)w.Find<Border>("suite-kpi-overdue").Child!).Children[1]).Text); // Start vor 10 Tagen: Schritt 1 überfällig
        // Tafel in der Übersicht verschiebt wirklich
        var first = CreationSuite.Steps(dir)[0];
        w.Find<Button>($"suite-right-{first.Line}").Press();
        Assert.Equal(1, CreationSuite.Steps(dir)[0].State);
        // Dateien: Baum links und letzte Dateien
        File.WriteAllText(Path.Combine(dir, "Einladungen", "brief.txt"), "Hallo");
        w.Show("vorhaben");
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("▸ Einladungen  (1)"));
        Assert.NotNull(w.TryFind<Button>("suite-recent-0"));
        w.Find<Button>("suite-tree-0").Press(); // Ordner „Einladungen“ öffnen
        Assert.Contains(launcher.Opened, l => l.EndsWith("Einladungen"));
        w.Find<Button>("suite-mini-open").Press(); Assert.Equal("zeitplan", w.SuiteMode); Assert.NotNull(w.TryFind<StackPanel>("suite-timeline"));
        // zweites Projekt, Abbrechen und wechseln
        w.Find<Button>("suite-new").Press(); Assert.NotNull(w.TryFind<StackPanel>("suite-newcard"));
        w.Find<Button>("suite-new-cancel").Press(); Assert.Null(w.TryFind<StackPanel>("suite-newcard"));
        w.Find<Button>("suite-new").Press();
        w.Find<TextBox>("suite-name").Text = "Spiel"; w.Find<Button>("suite-kit-game").Press();
        Assert.Equal("Spiel", Path.GetFileName(w.SuiteOpenDir)); Assert.Equal("uebersicht", w.SuiteMode);
        Assert.NotNull(w.TryFind<Button>("suite-open-1"));
        var other = Enumerable.Range(0, 2).Select(i => w.Find<Button>($"suite-open-{i}")).First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Gartenfest"));
        other.Press(); Assert.Equal(dir, w.SuiteOpenDir);
        // umbenennen
        w.Find<Button>("suite-rename").Press();
        w.Find<TextBox>("suite-rename-text").Text = "Spiel"; w.Find<Button>("suite-rename-ok").Press(); // gibt es schon
        Assert.True(Directory.Exists(dir)); Assert.NotNull(w.TryFind<TextBox>("suite-rename-text"));
        w.Find<TextBox>("suite-rename-text").Text = "Sommerfest"; w.Find<Button>("suite-rename-ok").Press();
        Assert.Equal("Sommerfest", Path.GetFileName(w.SuiteOpenDir)); Assert.False(Directory.Exists(dir));
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "suite.rename");
        w.Close();
    }
}
