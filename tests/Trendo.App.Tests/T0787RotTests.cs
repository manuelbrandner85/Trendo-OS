using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0787 Rot-Grün: nur Namen und Kennungen, keine neuen Typen – übersetzt auch auf dem alten Stand (main 9bc135aaa) und ist dort rot.</summary>
public class T0787_Rot
{
    [AvaloniaFact(DisplayName = "T-0787 Rot-Grün: „Rezepte & Ernährung“ ist ein Programm der Hülle in „Wissen & Lernen“ mit Vorlagen, JSON-LD- und PDF-Ausgabe und KI-Fachaktion")]
    public void Studio_exists()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            Assert.True(MainWindow.Programs.TryGetValue("rezepte-programm", out var spec), "rezepte-programm fehlt");
            Assert.Equal("STUDIO-RECIPE-01", spec.Studio);
            Assert.Equal("Wissen & Lernen", StudioCatalog.For("STUDIO-RECIPE-01")?.Area);
            Assert.True(spec.Templates.Count >= 2);
            Assert.Contains(spec.Exports, x => x.Extension == ".json"); Assert.Contains(spec.Exports, x => x.Extension == ".pdf");
            Assert.Contains(spec.KiActions, a => a.Def.Id == "rezept.anlegen");
            Assert.Equal("rezepte-programm", w.PageForStudio("STUDIO-RECIPE-01"));
        }
        finally { w.Close(); }
    }
}
