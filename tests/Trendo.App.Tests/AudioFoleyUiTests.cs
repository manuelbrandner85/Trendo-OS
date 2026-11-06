using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 4, S4-17 (06.11.2026): Geräusch-Werkstatt im Audioeditor.</summary>
public class AudioFoley_Ui
{
    [AvaloniaFact(DisplayName = "S4-17 Audioeditor: Schritte erzeugen und öffnen, mit Ereignis-Tag in die Bibliothek; Fremdgeräusch ohne Lizenz wird im Paket ausgeschlossen und genannt")]
    public void Make_add_pack()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.Find<ComboBox>("au-foley-kind").SelectedIndex = Array.FindIndex(Foley.Kinds, k => k.Id == "schritte-kies");
            w.Find<NumericUpDown>("au-foley-len").Value = 4;
            var make = w.Find<Button>("au-foley-make"); make.BringIntoView(); Settle(); make.Press(); Settle();
            var d = w.ProgramActive("ton")!;
            Assert.Equal(Foley.OwnLicense, d.Text("license")); Assert.InRange(AudioFile.Read(d.Text("source")).Seconds, 3.9, 4.1);
            Assert.Equal("Schritt.Kies", w.Find<TextBox>("au-foley-tags").Text);
            w.Find<TextBox>("au-foley-name").Text = "Schritte Kies";
            var add = w.Find<Button>("au-foley-add"); add.BringIntoView(); Settle(); add.Press(); Settle();
            // fremde Datei ohne Lizenz
            var foreign = Path.Combine(root, "fremd.wav"); AudioFile.WriteWav(Foley.Make("feuer", 1), foreign, 16);
            w.AuOpen(foreign); w.Show("ton"); Settle();
            w.Find<TextBox>("au-foley-name").Text = "Feuer fremd"; w.Find<TextBox>("au-foley-license").Text = "unbekannt";
            add = w.Find<Button>("au-foley-add"); add.BringIntoView(); Settle(); add.Press(); Settle();
            Assert.Contains("ausgeschlossen", w.ProgramMessage("ton"));
            var pack = w.Find<Button>("au-foley-pack"); pack.BringIntoView(); Settle(); pack.Press(); Settle();
            var r = w.AuFoleyLastPack!;
            Assert.Equal(["Schritte Kies"], r.Included.Select(e => e.Name));
            Assert.Contains(r.Excluded, x => x.Entry.Name == "Feuer fremd");
            Assert.Contains("Feuer fremd", w.ProgramMessage("ton"));
            Assert.True(File.Exists(Path.Combine(r.Folder, "Ereignisse.json")));
        }
        finally { w.Close(); }
    }
}
