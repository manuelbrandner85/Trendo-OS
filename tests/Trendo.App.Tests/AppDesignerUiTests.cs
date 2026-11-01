using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// Runde 7: Baukasten als App-Designer – Baustein mit der Maus in die Vorschau ziehen, in der Vorschau umsortieren, Eigenschaften rechts, Akzentfarbe, Handy/Bildschirm, Rückgängig, Export als ZIP.
public class AppDesigner_Ui
{
    static Point Center(MainWindow w, Control c) { c.BringIntoView(); Settle(); return c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value; }

    [AvaloniaFact(DisplayName = "App-Designer: Knopf aus der Leiste auf den ersten Baustein ziehen fügt ihn dort ein, Ziehen in der Vorschau sortiert um, Eigenschaften und Akzentfarbe wirken in der Vorschau, Rückgängig, ZIP")]
    public void Designer()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1200;
        w.Show("baukasten"); Settle();
        Assert.NotNull(w.TryFind<Grid>("builder-designer"));
        Assert.Equal(["h1", "p"], w.BuilderPage.Blocks.Select(b => b.Kind));
        // Baustein „Knopf“ aus der Leiste auf den Text (Index 1) ziehen → davor eingefügt
        var src = Center(w, w.Find<Button>("builder-add-button")); var dst = Center(w, w.Find<Border>("builder-block-1"));
        w.MouseDown(src, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(src + (dst - src) * (i / 6.0)); Settle(); } w.MouseUp(dst, MouseButton.Left); Settle();
        Assert.Equal(["h1", "button", "p"], w.BuilderPage.Blocks.Select(b => b.Kind));
        Assert.Equal(1, w.BuilderSel);
        // in der Vorschau: Text (2) auf die Überschrift (0) ziehen
        var a = Center(w, w.Find<Border>("builder-block-2")); var b0 = Center(w, w.Find<Border>("builder-block-0"));
        w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(a + (b0 - a) * (i / 6.0)); Settle(); } w.MouseUp(b0, MouseButton.Left); Settle();
        Assert.Equal(["p", "h1", "button"], w.BuilderPage.Blocks.Select(b => b.Kind));
        // antippen wählt aus, Eigenschaften rechts
        var knopf = Center(w, w.Find<Border>("builder-block-2")); w.MouseDown(knopf, MouseButton.Left); w.MouseUp(knopf, MouseButton.Left); Settle();
        Assert.Equal(2, w.BuilderSel);
        var t = w.Find<TextBox>("builder-text-2"); t.Text = "Jetzt buchen"; t.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Settle();
        Assert.Equal("Jetzt buchen", w.BuilderPage.Blocks[2].Text);
        Assert.Contains("Jetzt buchen", w.GetLogicalTexts());
        w.Find<Button>("builder-accent-22B35E").Press(); Assert.Equal("#22B35E", w.BuilderPage.Accent);
        Assert.Equal(Color.Parse("#22B35E"), ((ISolidColorBrush)((TextBlock)w.Find<Border>("builder-block-1").Child!).Foreground!).Color);
        // Handy / Bildschirm
        Assert.Equal(390, w.Find<Border>("builder-preview-frame").Width);
        w.Find<Button>("builder-view-desk").Press(); Assert.Equal(760, w.Find<Border>("builder-preview-frame").Width);
        // Rückgängig: Farbe zurück
        w.Find<Button>("builder-undo").Press(); Assert.Equal("#2F86F6", w.BuilderPage.Accent);
        w.Find<Button>("builder-undo").Press(); Assert.Equal("Mehr erfahren", w.BuilderPage.Blocks[2].Text);
        // Export
        var zip = w.BuilderExport();
        using (var z = System.IO.Compression.ZipFile.OpenRead(zip)) Assert.Contains(z.Entries, e => e.Name == "index.html");
        Assert.Contains(w.Core.Audit.ReadAll(), x => x.Action == "builder.export");
        w.Close();
    }
}
