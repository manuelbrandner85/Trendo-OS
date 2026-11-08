using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Masterprompt 131 / 7: drei Bedienstufen in allen Programmen – gleiche Engine, gleiche Daten, nur andere Werkzeugauswahl.</summary>
public class ProgramLevel_Ui
{
    const string TK = "statik-programm";
    static T In<T>(Button host, string name) where T : Control => ((Control)((Flyout)host.Flyout!).Content!).GetLogicalDescendants().Prepend((Control)((Flyout)host.Flyout!).Content!).OfType<T>().First(c => c.Name == name);

    [AvaloniaFact(DisplayName = "Bedienstufen: Einfach blendet Fachwerkzeuge aus, Erweitert zeigt alle, Wechsel lässt das Dokument unverändert; Einstellung bleibt gespeichert")]
    public void Levels_filter_tools_not_data()
    {
        var (w, _, root) = Ui.Open();
        var key = MainWindow.StudioPages["SPEC-03"]; w.Show(key); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-einfeld"));
        var before = w.ProgramActive(key)!.Data.ToJsonString();
        Assert.Equal(UserLevel.Erweitert, w.ProgramLevel);
        Assert.NotNull(w.TryFind<Button>("prog-cmd-tk-ec")); Assert.NotNull(w.TryFind<Button>("prog-cmd-tk-m"));
        Assert.Null(w.TryFind<Button>("prog-palette"));
        RealClick(w, w.Find<Button>("prog-level-einfach")); Settle();
        Assert.Equal(UserLevel.Einfach, w.ProgramLevel);
        Assert.Null(w.TryFind<Button>("prog-cmd-tk-ec")); Assert.Null(w.TryFind<Button>("prog-cmd-tk-react"));
        Assert.NotNull(w.TryFind<Button>("prog-cmd-tk-m")); // Kernwerkzeug bleibt
        Assert.Equal(before, w.ProgramActive(key)!.Data.ToJsonString());
        Assert.Equal("einfach", UserSettings.Load(root).Level);
        RealClick(w, w.Find<Button>("prog-level-erweitert")); Settle();
        Assert.NotNull(w.TryFind<Button>("prog-cmd-tk-ec"));
        Assert.Equal(before, w.ProgramActive(key)!.Data.ToJsonString());
        // Andere Programme folgen derselben Stufe
        w.ProgramSetLevel(UserLevel.Einfach); w.Show(MainWindow.StudioPages["SPEC-05"]); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-" + MainWindow.Programs[MainWindow.StudioPages["SPEC-05"]].Templates[1].Id)); Settle();
        Assert.Null(w.TryFind<Button>("prog-cmd-ms-f-linear")); Assert.NotNull(w.TryFind<Button>("prog-cmd-ms-k-linie"));
        // Einstellungen: gleiche Stufe, gleiche Wirkung
        w.Show("bedien-sehen"); Settle();
        w.Find<Button>("level-erweitert").BringIntoView(); Settle(); // liegt auf der längeren Seite unterhalb des Sichtbereichs – wie ein Mensch erst hinscrollen
        RealClick(w, w.Find<Button>("level-erweitert")); Settle();
        Assert.Equal(UserLevel.Erweitert, w.ProgramLevel);
    }

    [AvaloniaFact(DisplayName = "Profi-Stufe: Befehlssuche führt Befehle aus, Verlauf springt zurück, Projektdaten – falsches JSON wird abgelehnt, richtiges übernommen und rückgängig machbar")]
    public void Profi_tools_work()
    {
        var (w, _, _) = Ui.Open();
        var key = MainWindow.StudioPages["SPEC-08"]; w.Show(key); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-tshirt"));
        w.ProgramSetLevel(UserLevel.Profi, key); Settle();
        var d = w.ProgramActive(key)!;
        // Befehlssuche: „Varianten“ finden und ausführen
        var pal = w.Find<Button>("prog-palette");
        var q = In<TextBox>(pal, "prog-palette-q"); q.Text = "variant";
        In<Button>(pal, "prog-palette-0").Press(); Settle();
        Assert.Equal(12, w.ProgramActive(key)!.List("variants").Count);
        // zweite Änderung, dann im Verlauf zwei Schritte zurück
        w.ProgramActive(key)!.Set("name", "Shirt Pro"); w.Show(key); Settle();
        var hist = w.Find<Button>("prog-history"); Assert.Contains("(2)", hist.Content as string);
        In<Button>(hist, "prog-history-1").Press(); Settle();
        Assert.Empty(w.ProgramActive(key)!.List("variants")); Assert.Equal("T-Shirt Basic", w.ProgramActive(key)!.Text("name"));
        Assert.True(w.ProgramActive(key)!.CanRedo);
        // Projektdaten
        var data = w.Find<Button>("prog-data"); var text = In<TextBox>(data, "prog-data-text");
        Assert.Contains("\"name\"", text.Text);
        text.Text = "[1,2]"; In<Button>(data, "prog-data-apply").Press(); Settle();
        Assert.Contains("JSON-Objekt", w.ProgramMessage(key)); Assert.Equal("T-Shirt Basic", w.ProgramActive(key)!.Text("name"));
        data = w.Find<Button>("prog-data"); text = In<TextBox>(data, "prog-data-text");
        text.Text = text.Text!.Replace("T-Shirt Basic", "T-Shirt Bio"); In<Button>(data, "prog-data-apply").Press(); Settle();
        Assert.Equal("T-Shirt Bio", w.ProgramActive(key)!.Text("name"));
        Assert.Equal("Projektdaten bearbeitet", w.ProgramActive(key)!.UndoLabel);
        RealClick(w, w.Find<Button>("prog-undo")); Settle();
        Assert.Equal("T-Shirt Basic", w.ProgramActive(key)!.Text("name"));
        w.ProgramSetLevel(UserLevel.Erweitert);
    }
}
