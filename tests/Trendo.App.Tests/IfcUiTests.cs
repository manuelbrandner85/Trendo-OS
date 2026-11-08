using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 3 – Wohnung planen: Export „IFC (BIM)“ über das Menüband (IfcOpenShell nachgebildet; echter Lauf in IfcExchangeTests/ifc-e2e.md).</summary>
public class IfcUi
{
    [AvaloniaFact(DisplayName = "IFC-Oberfläche: ohne Einrichtung klarer Hinweis; mit IfcOpenShell → .ifc im Ordner Grundrisse, Kontrollbericht, Hilfsdateien aufgeräumt")]
    public void Export_ifc()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(MainWindow.StudioPages["SPEC-01"]); w.ProgramNew("architektur", "wohnung"); w.Show("architektur");
            Assert.NotNull(w.Find<Button>("prog-cmd-arch-ifc-setup"));
            w.Find<Button>("prog-export-ifc").Press();
            Assert.Contains("IFC einrichten", w.ProgramMessage("architektur"));

            string? plan = null;
            MainWindow.IfcRunOverride = (mode, input, output) =>
            {
                if (mode == "export") { plan = File.ReadAllText(input); File.WriteAllText(output, "ISO-10303-21;\nHEADER;\nENDSEC;\nEND-ISO-10303-21;\n"); }
                else File.WriteAllText(output, "{\"schema\":\"IFC4\",\"projekt\":\"x\",\"anzahl\":{\"IfcWall\":9,\"IfcDoor\":5,\"IfcWindow\":4,\"IfcSpace\":5,\"IfcBuildingStorey\":1},\"raeume\":[]}");
                return new Cli.Result(0, "", "");
            };
            w.Show("architektur"); w.Find<Button>("prog-export-ifc").Press();
            var file = w.ProgramMessage("architektur")["Exportiert: ".Length..].Trim();
            Assert.EndsWith(".ifc", file); Assert.True(File.Exists(file)); Assert.Contains("Grundrisse", file);
            Assert.Contains("\"waende\"", plan); Assert.Contains("\"oeffnungen\"", plan);
            Assert.Equal("IFC4: 9 Wände, 5 Türen, 4 Fenster, 5 Räume, 1 Geschoss", w.ArchIfcLastInfo);
            Assert.False(File.Exists(Path.ChangeExtension(file, ".plan.json"))); Assert.False(File.Exists(Path.ChangeExtension(file, ".info.json")));
        }
        finally { MainWindow.IfcRunOverride = null; w.Close(); }
    }
}
