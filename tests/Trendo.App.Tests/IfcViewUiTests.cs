using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 3 – Wohnung planen: „IFC ansehen“ (web-ifc/ThatOpen) füllt die Arbeitsfläche; Brücke bereit → Datei, geladen → Bauteile (ohne echtes WebView).</summary>
public class IfcViewUi
{
    [AvaloniaFact(DisplayName = "IFC-Ansicht: Menüband „IFC ansehen“ exportiert und öffnet die 3D-Ansicht; bereit → Datei als Base64; geladen → Bauteile; nochmal klicken schließt")]
    public void View_ifc()
    {
        var (w, _, _) = Ui.Open(); var sent = new List<string>();
        try
        {
            MainWindow.IfcRunOverride = (mode, input, output) =>
            {
                File.WriteAllText(output, mode == "export" ? "ISO-10303-21;\nEND-ISO-10303-21;\n" : "{\"schema\":\"IFC4\",\"anzahl\":{\"IfcWall\":14},\"raeume\":[]}");
                return new Cli.Result(0, "", "");
            };
            w.IfcViewScriptOverride = sent.Add;
            w.Show(MainWindow.StudioPages["SPEC-01"]); w.ProgramNew("architektur", "wohnung"); w.Show("architektur");
            w.Find<Button>("prog-cmd-arch-ifc-view").Press();
            Assert.NotNull(w.IfcViewFile); Assert.EndsWith(".ifc", w.IfcViewFile); Assert.NotNull(w.Find<Border>("arch-ifc-area"));
            w.IfcViewMessage("{\"art\":\"bereit\"}");
            Assert.StartsWith("window.trendoIfc(\"" + Convert.ToBase64String(File.ReadAllBytes(w.IfcViewFile!)) + "\",", sent[^1]);
            w.IfcViewMessage("{\"art\":\"geladen\",\"waende\":14,\"tueren\":6,\"fenster\":3,\"raeume\":5}");
            Assert.Equal("14 Wände, 6 Türen, 3 Fenster, 5 Räume", w.IfcViewNote);
            w.IfcViewMessage("kaputt"); w.IfcViewMessage("{\"art\":\"loeschen\"}"); Assert.Single(sent);
            w.Find<Button>("prog-cmd-arch-ifc-view").Press(); Assert.Null(w.IfcViewFile);
            w.ArchIfcViewOpen(Path.Combine(Path.GetTempPath(), "gibtsnicht.ifc")); Assert.Null(w.IfcViewFile); Assert.Contains("IFC-Datei", w.ProgramMessage("architektur"));
            var fremd = Path.Combine(Path.GetTempPath(), "trendo-architekt-" + Guid.NewGuid().ToString("N") + ".ifc"); File.WriteAllText(fremd, "ISO-10303-21;");
            try { w.PickOverride = _ => Task.FromResult<string[]?>([fremd]); w.ArchIfcPick().GetAwaiter().GetResult(); Assert.Equal(fremd, w.IfcViewFile); }
            finally { File.Delete(fremd); }
        }
        finally { MainWindow.IfcRunOverride = null; w.Close(); }
    }
}
