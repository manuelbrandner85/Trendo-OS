using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 10 – SYS-12: gut lesbare Schrift für die ganze Oberfläche.</summary>
public class ReadFont_Ui
{
    [AvaloniaFact(DisplayName = "Lesbare Schrift: Knöpfe in „Sehen“; Atkinson wird gespeichert und aufs Fenster angewandt; zurück zum Standard; unbekannte Wahl ignoriert; Schriftdateien und Lizenz liegen bei")]
    public void Choose()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("bedien-sehen");
            Assert.NotNull(w.Find<Button>("readfont-standard")); var b = w.Find<Button>("readfont-atkinson"); Assert.Null(w.TryFind<Button>("readfont-opendyslexic"));
            b.Press();
            Assert.Equal("atkinson", w.Settings.ReadFont); Assert.Contains("Atkinson Hyperlegible", w.FontFamily.Name);
            w.SetReadFont("gibtsnicht"); Assert.Equal("atkinson", w.Settings.ReadFont);
            w.Show("bedien-sehen"); w.Find<Button>("readfont-standard").Press();
            Assert.Equal("", w.Settings.ReadFont); Assert.DoesNotContain("Atkinson", w.FontFamily.Name);
            var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
            var dir = Path.Combine(root!.FullName, "src", "Trendo.App", "Assets", "schriften");
            Assert.Equal(["AtkinsonHyperlegible-Bold.ttf", "AtkinsonHyperlegible-Regular.ttf", "OFL-Atkinson.txt"], Directory.GetFiles(dir).Select(Path.GetFileName).Order());
        }
        finally { w.SetReadFont(""); w.Close(); }
    }
}
