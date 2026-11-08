using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 8 – CORE-10: „Word, PDF, PowerPoint, Excel einlesen“ (markitdown nachgebildet; Echtlauf erst nach Freigabe).</summary>
public class WissenDocs_Ui
{
    [AvaloniaFact(DisplayName = "Dokumente einlesen: ohne Einrichtung Einrichten-Knopf; 2 Dokumente → 2 Wissensseiten mit Titel und Quelle; zweites Mal übersprungen; kaputte Datei gemeldet")]
    public async Task Import()
    {
        var (w, _, root) = Ui.Open();
        var dir = Path.Combine(root, "unterlagen"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "vertrag.pdf"), "x"); File.WriteAllText(Path.Combine(dir, "plan.docx"), "x");
        try
        {
            MainWindow.DocsRunOverride = null;
            w.Show("ki-wissen"); Assert.NotNull(w.Find<Button>("wissen-docs-setup"));
            MainWindow.DocsRunOverride = (inp, outMd) =>
            {
                var name = Path.GetFileNameWithoutExtension(inp);
                File.WriteAllText(outMd, $"# {name}\n\nInhalt von {name}: Kündigungsfrist drei Monate.");
                return new Cli.Result(0, "{\"zeichen\": 50, \"titel\": \"" + (name == "vertrag" ? "Mietvertrag 2026" : "") + "\"}", "");
            };
            w.Show("ki-wissen"); Assert.NotNull(w.Find<Button>("wissen-docs"));
            Assert.Equal((2, 0, 0), await w.WissenImportDocs("\"" + dir + "\""));
            var items = w.Wissen.All();
            Assert.Contains(items, k => k.Title == "Mietvertrag 2026" && k.Source == "Datei: vertrag.pdf" && k.Text.Contains("Kündigungsfrist"));
            Assert.Contains(items, k => k.Title == "plan" && k.Source == "Datei: plan.docx");
            Assert.Equal((0, 2, 0), await w.WissenImportDocs(dir));
            File.WriteAllText(Path.Combine(dir, "kaputt.xlsx"), "x");
            var good = MainWindow.DocsRunOverride;
            MainWindow.DocsRunOverride = (inp, outMd) => inp.EndsWith(".xlsx") ? new Cli.Result(3, "", "Nicht lesbar: BadZipFile") : good!(inp, outMd);
            var (a, s, f) = await w.WissenImportDocs(dir);
            Assert.Equal((0, 2, 1), (a, s, f)); Assert.Contains("kaputt.xlsx", w.Find<TextBlock>("wissen-msg").Text);
            Assert.Equal((0, 0, 0), await w.WissenImportDocs(Path.Combine(root, "leer-gibts-nicht")));
        }
        finally { MainWindow.DocsRunOverride = null; w.Close(); }
    }
}
