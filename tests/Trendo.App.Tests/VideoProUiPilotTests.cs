using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Pilot 4.0: V5 bis V9 über die Oberfläche.</summary>
public class VideoPilotPanels_Ui
{
    static (MainWindow W, Trendo.Core.Programs.ProgramDocument D, string Root) Open()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        var d = w.ProgramActive("schnitt")!;
        w.EditAdd(F("a.mp4"), length: 10, audio: true); w.EditAdd(F("b.mp4"), length: 10, audio: true); w.EditAdd(F("m.mp3"), music: true, length: 4, audio: true); Settle();
        w.EditSelected = ("clips", 0); w.EditTab = "auswahl"; w.Show("schnitt"); Settle();
        return (w, d, root);
    }
    static void Press(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle(); }

    [AvaloniaFact(DisplayName = "V5: Keyframe über die Oberfläche setzen und entfernen; Abspielkopf bestimmt die Stelle im Stück")]
    public void Keyframe_panel()
    {
        var (w, d, _) = Open();
        var c = VideoEdit.Clips(d.Data)[0]; d.Set("playhead", 2.5); w.Show("schnitt"); Settle();
        w.Find<ComboBox>("ve-key-param-" + c.Id).SelectedIndex = 3; Settle(); // Deckkraft
        w.Find<Slider>("ve-key-value-" + c.Id).Value = 0.4;
        Press(w, "ve-key-set-" + c.Id);
        var k = VideoAnim.Keys(d.Data, c.Id, "opacity");
        // Auswahl des Werts springt nach dem Neuaufbau zurück, darum gezielt prüfen, was gespeichert wurde
        Assert.Single(k); Assert.Equal(2.5, k[0].Time, 3); Assert.Equal(0.4, k[0].Value, 3);
        Assert.True(d.Undo()); Assert.Empty(VideoAnim.Keys(d.Data, c.Id, "opacity"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "V6: Effekt hinzufügen, abschalten, entfernen")]
    public void Stack_panel()
    {
        var (w, d, _) = Open();
        var c = VideoEdit.Clips(d.Data)[0];
        w.Find<ComboBox>("ve-fx-pick").SelectedIndex = 5; Press(w, "ve-fx-add");
        Assert.Equal(",hue=s=0", VideoAnim.StackFilter(d.Data, c.Id));
        w.Find<CheckBox>("ve-fx-on-0").IsChecked = false; Settle();
        Assert.Equal("", VideoAnim.StackFilter(d.Data, c.Id));
        Press(w, "ve-fx-del-0");
        Assert.Empty(VideoAnim.Stack(d.Data, c.Id));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "V7: Farbrad wirkt auf den Export und lässt sich zurücksetzen")]
    public void Wheels()
    {
        var (w, d, _) = Open();
        var c = VideoEdit.Clips(d.Data)[0];
        w.PreviewWheelsForTests[1].Changed!(0.8, 0); Settle();
        Assert.Equal(",colorbalance=rm=0.4:gm=-0.2:bm=-0.2", VideoGrade.Filter(d.Data, c.Id));
        Press(w, "ve-grade-reset");
        Assert.Equal("", VideoGrade.Filter(d.Data, c.Id));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "V8: Mischpult zeigt alle Tonspuren; Pegel, Stumm und Solo wirken aufs Dokument")]
    public void Mixer()
    {
        var (w, d, _) = Open();
        w.EditTab = "mischpult"; w.Show("schnitt"); Settle();
        Assert.NotNull(w.TryFind<Slider>("ve-mix-gain--1")); Assert.NotNull(w.TryFind<Slider>("ve-mix-gain-0"));
        w.Find<Slider>("ve-mix-gain-0").Value = 0.5; Press(w, "ve-mix-set-0");
        Assert.Equal(0.5, VideoLanes.Gain(d.Data, 0), 2);
        Press(w, "ve-mix-m-0"); Assert.True(VideoLanes.IsMuted(d.Data, "audio", 0));
        Press(w, "ve-mix-s--1"); Assert.True(VideoLanes.IsSolo(d.Data, "audio", -1));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "V9: Rechtsklick-Menü passt zum Stück; Aktionen wirken; Zeitleiste hat einen Höhen-Griff")]
    public void Context_menu()
    {
        var (w, d, _) = Open();
        var m = w.EditContextMenu(d, "music", 0);
        Assert.Contains(m.Items.OfType<MenuItem>(), i => (string?)i.Header == "Löschen und aufrücken");
        var c = w.EditContextMenu(d, "clips", 1);
        var del = c.Items.OfType<MenuItem>().First(i => i.Name == "ve-ctx-loeschen");
        del.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
        Assert.Single(VideoEdit.Clips(d.Data));
        Assert.NotNull(w.TryFind<GridSplitter>("ve-timeline-grip"));
        var t = w.EditTimelineForTests!;
        Assert.Equal(("music", 0), t.ItemAt(t.BoxOf("music", 0).Center));
        Assert.Null(t.ItemAt(new Point(1, 2)));
        w.Close();
    }
}
