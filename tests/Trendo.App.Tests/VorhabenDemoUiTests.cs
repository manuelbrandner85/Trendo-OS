using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Vorhaben (Umbau 04.11.2026): ohne Projekte zeigt die Seite „Beispiel ansehen“, danach den Projektplaner.</summary>
public class VorhabenDemo_Ui
{
    [AvaloniaFact(DisplayName = "Vorhaben: „Beispiel ansehen“ legt ein Beispielprojekt an und öffnet den Planer mit Übersicht, Liste, Tafel, Zeitplan, Dateien")]
    public void Demo()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.Show("vorhaben"); Settle();
            RealClick(w, V<Button>(w, "suite-demo")); Settle();
            Assert.NotNull(w.TryFind<WrapPanel>("suite-modes"));
            Assert.Null(w.TryFind<Button>("suite-demo"));
            RealClick(w, V<Button>(w, "suite-mode-zeitplan")); Settle();
            Assert.NotNull(w.GanttForTests);
        }
        finally { w.Close(); }
    }
}
