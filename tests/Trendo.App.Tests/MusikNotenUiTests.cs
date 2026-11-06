using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 4, S4-04 (06.11.2026): Modus „Noten“ (Piano-Roll) in Trendo Musik.</summary>
public class MusikNoten_Ui
{
    [AvaloniaFact(DisplayName = "S4-04 Trendo Musik: Noten per Mausklick in die Piano-Roll, quantisieren, transponieren, als MIDI speichern und identisch öffnen; kaputte Datei → Warnung, Stück bleibt")]
    public void Piano_roll()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show("musik-studio"); Settle(); if (w.Windows.Active is { Maximized: false } pw) w.Windows.ToggleMaximize(pw.Id); Settle();
            w.Find<Button>("ms-mode-noten").Press(); Settle();
            PianoRollView View() => w.GetVisualDescendants().OfType<PianoRollView>().Single(v => v.IsEffectivelyVisible);
            var view = View(); Settle();
            Point At(int pitch, int tick) { Settle(); view = View(); return view.PointOf(pitch, tick).Transform(view.TransformToVisual(w) ?? throw new Xunit.Sdk.XunitException($"Piano-Roll nicht im Fenster ({w.MsRoll.Notes.Count} Noten)")); }
            foreach (var (p, t) in new[] { (60, 0), (64, 480), (67, 960) }) { w.MouseDown(At(p, t), MouseButton.Left); w.MouseUp(At(p, t), MouseButton.Left); Settle(); view = View(); }
            Assert.Equal([(60, 0), (64, 480), (67, 960)], w.MsRoll.Notes.Select(n => (n.Pitch, n.Start)));
            w.Find<Button>("ms-roll-up").Press(); Settle();
            Assert.Equal([61, 65, 68], w.MsRoll.Notes.Select(n => n.Pitch));
            w.Find<Button>("ms-roll-undo").Press(); Settle();
            Assert.Equal([60, 64, 67], w.MsRoll.Notes.Select(n => n.Pitch));
            w.MsRoll = w.MsRoll with { Notes = [.. w.MsRoll.Notes, new PianoRoll.Note(72, 1450, 200)] }; w.Show("musik-studio"); Settle();
            w.Find<Button>("ms-roll-quant").Press(); Settle();
            Assert.Equal(1440, w.MsRoll.Notes.Single(n => n.Pitch == 72).Start);
            w.Find<TextBox>("ms-roll-name").Text = "Dreiklang";
            w.Find<Button>("ms-roll-save").Press(); Settle();
            var mid = Directory.GetFiles(root, "Dreiklang.mid", SearchOption.AllDirectories).Single();
            var saved = w.MsRoll;
            // kaputte Datei: Warnung, Stück bleibt
            var junk = Path.Combine(root, "kaputt.mid"); File.WriteAllText(junk, "keine MIDI");
            w.Find<TextBox>("ms-roll-file").Text = junk; w.Find<Button>("ms-roll-open").Press(); Settle();
            Assert.Contains("Nicht geöffnet", w.MsRollMessage); Assert.Equal(saved.Notes, w.MsRoll.Notes);
            // leeren und die gespeicherte Datei wieder öffnen → identisch
            w.Find<Button>("ms-roll-clear").Press(); Settle(); Assert.Empty(w.MsRoll.Notes);
            w.Find<TextBox>("ms-roll-file").Text = mid; w.Find<Button>("ms-roll-open").Press(); Settle();
            Assert.Equal(saved.Notes, w.MsRoll.Notes); Assert.Equal("Dreiklang", w.MsRoll.Name);
        }
        finally { w.Close(); }
    }
}
