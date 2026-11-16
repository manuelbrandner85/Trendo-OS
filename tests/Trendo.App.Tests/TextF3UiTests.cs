using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>F3 Text: mehrstufige Listen (Tab, Umschalt+Tab, Knöpfe) und „Formatierung löschen“ in der Oberfläche.</summary>
public class TextF3UiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static void Press(Control c, Avalonia.Input.Key k, KeyModifiers m = KeyModifiers.None) => c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = m });
    static Para P(MainWindow w, int i) => (Para)w.TextDocument.Blocks[i];

    [AvaloniaFact(DisplayName = "Text F3: 1.-Liste, Enter und Tab rückt ein (a), Tab noch einmal (i.), Umschalt+Tab zurück; ⇤/⇥-Knöpfe; Rückgängig; Tab ohne Liste ändert nichts")]
    public void Multilevel_list()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.TextNew(); w.Show("text"); Pump();
            var box = w.Find<TextPageView>("text-edit"); box.Type("Obst"); Pump();
            Press(box, Avalonia.Input.Key.Tab); Assert.Equal(0, P(w, 0).ListLevel); // kein Listenpunkt
            w.Find<Button>("text-numbers").Press(); Pump();
            Assert.False(w.Find<Button>("text-list-out").IsEnabled);
            w.TextSelect(0, 4); Press(box, Avalonia.Input.Key.Enter); Pump();
            box.Type("Äpfel"); Pump();
            Press(box, Avalonia.Input.Key.Tab); Pump();
            Assert.Equal(1, P(w, 1).ListLevel);
            Assert.Equal("a)", w.TextView.ListLabel(1));
            Assert.Equal("1.", w.TextView.ListLabel(0));
            Press(w.Find<TextPageView>("text-edit"), Avalonia.Input.Key.Tab); Pump();
            Assert.Equal("i.", w.TextView.ListLabel(1));
            Press(w.Find<TextPageView>("text-edit"), Avalonia.Input.Key.Tab, KeyModifiers.Shift); Pump();
            Assert.Equal(1, P(w, 1).ListLevel);
            w.Find<Button>("text-list-out").Press(); Pump();
            Assert.Equal(0, P(w, 1).ListLevel); Assert.Equal("2.", w.TextView.ListLabel(1));
            w.Find<Button>("text-list-in").Press(); Pump();
            Assert.Equal(1, P(w, 1).ListLevel);
            w.Find<Button>("text-undo").Press(); Pump();
            Assert.Equal(0, P(w, 1).ListLevel);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Text F3: „Formatierung löschen“ – erst nur das Markierte, ohne Markierung der ganze Absatz (Absatzart, Ausrichtung zurück)")]
    public void Clear_format()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.TextNew(); w.Show("text"); Pump();
            w.TextView.Type("Hallo schöne Welt"); Pump();
            w.TextSelect(0, 0, 17); w.Find<Button>("text-bold").Press(); Pump();
            w.Find<Button>("text-align-center").Press(); Pump();
            P(w, 0).Style = ParaStyle.Heading2; w.Show("text"); Pump();
            w.TextSelect(0, 6, 6); w.Find<Button>("text-clear-format").Press(); Pump();
            Assert.False(TextDoc.Has(P(w, 0), 6, 6, Fmt.Bold)); Assert.True(TextDoc.Has(P(w, 0), 0, 5, Fmt.Bold));
            Assert.Equal(Align.Center, P(w, 0).Align);
            w.TextSelect(0, 3, 0); w.Find<Button>("text-clear-format").Press(); Pump();
            Assert.False(TextDoc.Has(P(w, 0), 0, 17, Fmt.Bold));
            Assert.Equal((ParaStyle.Text, Align.Left), (P(w, 0).Style, P(w, 0).Align));
            Assert.Equal("Hallo schöne Welt", P(w, 0).Text);
        }
        finally { w.Close(); }
    }
}
