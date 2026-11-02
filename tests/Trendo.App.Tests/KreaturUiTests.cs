using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

/// 0.9.47: Kreatur-Baukasten im 3D-Studio – Art und Farbe wählen, Blender sichtbar oder im Hintergrund, Ergebnis mit Vorschau und „In Blender öffnen“.
public class Kreatur_Ui
{
    [AvaloniaFact(DisplayName = "3D-Studio: Kreatur-Karte – Drache in Bronze/Grün, „Vorschaubild rendern“ startet Blender im Hintergrund, „sichtbar“ ohne -b; Ergebnis mit Vorschau")]
    public async Task Build_dragon()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<(IReadOnlyList<string> Args, bool Visible)>();
        MainWindow.KreaturRunOverride = (exe, args, visible) =>
        {
            calls.Add((args, visible));
            var json = System.Text.Json.JsonDocument.Parse(args[^1]).RootElement;
            var basis = json.GetProperty("ausgabe").GetString()!;
            File.WriteAllBytes(basis + ".glb", [0x67, 0x6C, 0x54, 0x46]); File.WriteAllText(basis + ".blend", "x");
            if (json.GetProperty("bild").GetBoolean()) File.WriteAllBytes(basis + ".png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==")); // 1×1-PNG als Vorschau
            return Task.FromResult("TRENDO_KREATUR {}");
        };
        try
        {
            w.Show("3d"); Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.TryFind<StackPanel>("3d-kreatur"));
            w.Find<ComboBox>("3d-kreatur-farbe").SelectedIndex = 1; // Grün
            await w.KreaturBuild(false); Dispatcher.UIThread.RunJobs();
            var (args, visible) = Assert.Single(calls);
            Assert.False(visible); Assert.Contains("-b", args); Assert.Contains("--factory-startup", args);
            var opt = System.Text.Json.JsonDocument.Parse(args[^1]).RootElement;
            Assert.Equal("drache", opt.GetProperty("art").GetString()); Assert.Equal("gruen", opt.GetProperty("farbe").GetString());
            Assert.True(opt.GetProperty("bild").GetBoolean());
            Assert.True(File.Exists(w.KreaturLast + ".glb"));
            Assert.NotNull(w.TryFind<Image>("3d-kreatur-vorschau"));
            Assert.True(w.Find<Button>("3d-kreatur-oeffnen").IsEnabled);
            await w.KreaturBuild(true);
            Assert.True(calls[1].Visible); Assert.DoesNotContain("-b", calls[1].Args); // sichtbar: Blender-Fenster öffnet sich
            Assert.False(System.Text.Json.JsonDocument.Parse(calls[1].Args[^1]).RootElement.GetProperty("bild").GetBoolean());
        }
        finally { MainWindow.KreaturRunOverride = null; w.Close(); }
    }
}
