using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 8 – SPEC-12: Karte „Auf Schadsoftware-Merkmale prüfen“ (yara-x nachgebildet; Echtlauf erst nach Freigabe).</summary>
public class ForensikYara_Ui
{
    [AvaloniaFact(DisplayName = "YARA-Karte: ohne Einrichtung Einrichten-Knopf; mit yara-x Treffer als Warnung mit deutscher Art; nichts wird verändert; fehlender Ordner verständlich")]
    public async Task Scan()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            MainWindow.YaraDirOverride = null; MainWindow.YaraRunOverride = null;
            w.Show("forensik"); Assert.NotNull(w.Find<Button>("for-yara-setup"));
            var dir = Path.Combine(root, "verdacht"); Directory.CreateDirectory(dir); var f = Path.Combine(dir, "rechnung.exe"); File.WriteAllText(f, "MZ");
            MainWindow.YaraDirOverride = Path.Combine(root, "yx"); IReadOnlyList<string>? seen = null;
            MainWindow.YaraRunOverride = (_, a, _) => { seen = a; return new Cli.Result(0, "{\"path\":\"" + f.Replace("\\", "\\\\") + "\",\"rules\":[{\"identifier\":\"Win32_Ransomware_WannaCry\",\"meta\":{\"tc_detection_type\":\"Ransomware\",\"tc_detection_name\":\"WannaCry\",\"tc_detection_factor\":5}}]}\n", ""); };
            w.Show("forensik"); Assert.NotNull(w.Find<Button>("for-yara-run"));
            await w.ForensicsYara("\"" + dir + "\"");
            Assert.StartsWith("1 Datei(en) mit 1 Treffer(n): rechnung.exe → Erpressersoftware WannaCry", w.YaraMessage);
            Assert.Equal(dir, seen![^1]); Assert.True(File.Exists(f)); Assert.Equal(5, w.YaraLast!.Single().Severity);
            Assert.Contains("Erpressersoftware", w.Find<SelectableTextBlock>("for-yara-msg").Text);
            MainWindow.YaraRunOverride = (_, _, _) => new Cli.Result(0, "", "");
            await w.ForensicsYara(dir); Assert.StartsWith("Keine bekannten Schadsoftware-Merkmale", w.YaraMessage);
            await w.ForensicsYara(Path.Combine(root, "gibtsnicht")); Assert.Equal("Diese Datei oder diesen Ordner gibt es nicht.", w.YaraMessage);
        }
        finally { MainWindow.YaraDirOverride = null; MainWindow.YaraRunOverride = null; w.Close(); }
    }
}
