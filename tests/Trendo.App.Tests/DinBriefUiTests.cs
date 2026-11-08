using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 6 – Dokumente: „Geschäftsbrief nach DIN 5008“ in der typst-Karte (typst nachgebildet; echte Läufe in DinLetterTests).</summary>
public class DinBriefUi
{
    [AvaloniaFact(DisplayName = "DIN-5008-Oberfläche: fehlender Empfänger → Hinweis; vollständig → PDF „brief-…“ mit DIN-Quelle (Form B bzw. A), Datum und Zeichen")]
    public void Din_letter()
    {
        var exe = Path.Combine(Path.GetTempPath(), "trendo-din-fake-" + Guid.NewGuid().ToString("N") + ".exe"); File.WriteAllText(exe, "x");
        try
        {
            MainWindow.ToolExeOverride["typst"] = exe; IReadOnlyList<string>? seen = null;
            MainWindow.ToolRunOverride = (_, a, _) => { seen = a; File.WriteAllText(a[4], "%PDF-1.7"); return new Cli.Result(0, "", ""); };
            var (w, _, _) = Ui.Open(); w.Show("doku-schreiben");
            Assert.NotNull(w.Find<Expander>("din-box"));
            Assert.Null(w.TypstDinLetter("Erika Muster\nMusterweg 1\n12345 Musterstadt", "", "Angebot", "Sehr geehrte Damen und Herren,", false));
            Assert.Contains("Empfänger fehlt.", w.TypstMessage);
            w.Show("doku-schreiben");
            w.Find<TextBox>("typst-title").Text = "Ihr Angebot vom 3. November"; w.Find<TextBox>("typst-body").Text = "Sehr geehrte Frau Beispiel,\n\nvielen Dank.";
            w.Find<TextBox>("din-sender").Text = "Erika Muster\nMusterweg 1\n12345 Musterstadt";
            w.Find<TextBox>("din-recipient").Text = "Max Beispiel GmbH\nHauptstraße 5\n54321 Beispielort";
            w.Find<TextBox>("din-our").Text = "EM-41";
            w.Find<Button>("din-run").Press();
            Assert.StartsWith("✓ Brief nach DIN 5008 (Form B) gespeichert: brief-Ihr-Angebot-vom-3--November.pdf", w.TypstMessage);
            var src = File.ReadAllText(seen![3]);
            Assert.Contains("DIN 5008 Form B", src); Assert.Contains("[Unser Zeichen:], [EM\\-41]", src); Assert.Contains("Max Beispiel GmbH", src); Assert.Contains("<din-anschrift>", src);
            Assert.NotNull(w.TypstDinLetter("Erika Muster\nMusterweg 1\n12345 Musterstadt", "Max Beispiel GmbH\n54321 Beispielort", "Kurz", "Text", true));
            Assert.Contains("DIN 5008 Form A", File.ReadAllText(seen![3]));
        }
        finally { MainWindow.ToolExeOverride.Clear(); MainWindow.ToolRunOverride = null; File.Delete(exe); }
    }
}
