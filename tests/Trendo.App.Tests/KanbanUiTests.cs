using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// Runde 7: Großprojekte als Kanban-Tafel – Projekt aus Vorlage, Karte mit der Maus in „Läuft“ ziehen, KI-Rolle, Fortschritt, Beleg mit Prüfsumme, Papierkorb.
public class Kanban_Ui
{
    static Point Center(MainWindow w, Control c) { c.BringIntoView(); Settle(); return c.TranslatePoint(new Point(c.Bounds.Width / 2, Math.Min(c.Bounds.Height / 2, 30)), w)!.Value; }
    static void Enter(TextBox t, string v) { t.Text = v; t.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Settle(); }

    [AvaloniaFact(DisplayName = "Kanban: Großprojekt aus Ablauf-Vorlage, Karte in „Läuft“ ziehen, Rolle und Fortschritt (100 % = Fertig), Beleg mit Prüfsumme, neue Karte, Papierkorb und zurück; Abläufe bleiben als Reiter")]
    public void Kanban()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1700; w.Height = 1200;
        w.Show("produktion"); Settle();
        Assert.NotNull(w.TryFind<Grid>("big-window"));
        w.Find<TextBox>("big-new-name").Text = "Hofladen online"; w.Find<TextBox>("big-new-goal").Text = "Bestellungen über die Webseite";
        w.Find<ComboBox>("big-new-recipe").SelectedIndex = 1; w.Find<Button>("big-new-create").Press(); Settle();
        var board = w.BigBoardNow!; Assert.Equal(Fusion.Recipes[0].Steps.Count, board.Cards.Count);
        Assert.Contains(w.Core.Projects.List(), p => p.Id == w.BigProject);
        var first = board.Cards[0];
        // Karte mit der Maus in die Spalte „Läuft“ ziehen
        var a = Center(w, w.Find<Border>($"big-card-{first.Id}")); var b = Center(w, w.Find<Border>("big-col-1"));
        w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(a + (b - a) * (i / 6.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle();
        var c = w.BigBoardNow!.Cards.Single(x => x.Id == first.Id); Assert.Equal(1, c.Column); Assert.Equal(10, c.Progress);
        Assert.Equal(first.Id, w.BigCard);
        // Rolle, Fortschritt
        w.Find<ComboBox>("big-c-role").SelectedItem = "Prüfung (QA)"; Settle();
        Assert.Equal("Prüfung (QA)", w.BigBoardNow!.Cards.Single(x => x.Id == first.Id).Role);
        Enter(w.Find<TextBox>("big-c-progress"), "100");
        Assert.Equal(2, w.BigBoardNow!.Cards.Single(x => x.Id == first.Id).Column);
        // Beleg
        var src = Path.Combine(root, "freigabe.txt"); File.WriteAllText(src, "ok");
        w.Find<TextBox>("big-ev-path").Text = src; w.Find<TextBox>("big-ev-note").Text = "Kunde hat zugestimmt"; w.Find<Button>("big-ev-add").Press(); Settle();
        var ev = w.BigBoardNow!.Cards.Single(x => x.Id == first.Id).Evidence.Single();
        Assert.Equal(64, ev.Sha256.Length); Assert.StartsWith("✓", ((TextBlock)((StackPanel)w.Find<Button>("big-ev-0").Content!).Children[0]).Text);
        // neue Karte
        Enter(w.Find<TextBox>("big-add-text"), "Fotos der Produkte"); Assert.Contains(w.BigBoardNow!.Cards, x => x.Title == "Fotos der Produkte" && x.Column == 0);
        // Papierkorb
        var nid = w.BigCard!; w.Find<Button>("big-c-del").Press(); w.Find<Button>("big-c-del").Press(); Settle();
        Assert.DoesNotContain(w.BigBoardNow!.Cards, x => x.Id == nid); Assert.Single(w.BigBoardNow!.Trash);
        w.Find<Button>("big-trash-0").Press(); Assert.Contains(w.BigBoardNow!.Cards, x => x.Id == nid);
        Assert.True(w.Find<ProgressBar>("big-progress").Value > 0);
        Assert.Contains(w.Core.Audit.ReadAll(), x => x.Action == "grossprojekt.move");
        // Abläufe über mehrere Studios bleiben erreichbar
        w.Find<Button>("prod-tab-ablaeufe").Press(); Assert.NotNull(w.TryFind<Button>("fusion-request"));
        w.Close();
    }
}
