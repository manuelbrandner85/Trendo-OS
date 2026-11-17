using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Elektro;
using Trendo.Core.Programs;
using Trendo.Core.Statik;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0748: Gegenprobe im Programm „Tragwerk & Mechanik“, KiCad-Import und Gerber-Prüfung im Programm „Schaltung & Simulation“ – vom Klick bis zur Datei.</summary>
public class T0748_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }

    [AvaloniaFact(DisplayName = "T-0748 UI: Tragwerk – Vorlage Einfeldträger zeigt „Gegenprobe ✓ Abweichung“; Rahmen ebenso; Export „Gegenprobe (Bericht)“ mit Tabellenformel")]
    public void Statik_Gegenprobe()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-03"]); Settle();
        var tpl = Get<Button>(w, "prog-tpl-einfeld"); tpl.BringIntoView(); Settle(); RealClick(w, tpl);
        Assert.StartsWith("Gegenprobe ✓ Abweichung", Get<TextBlock>(w, "tk-crosscheck").Text);
        // Last verdoppeln: Gegenprobe rechnet neu und bleibt ✓
        w.FrameEdit("Test", f => f.Loads.Single(l => l.Kind == "strecke").Value = 20); Settle();
        Assert.StartsWith("Gegenprobe ✓", Get<TextBlock>(w, "tk-crosscheck").Text);
        Press(w, "prog-export-gegenprobe");
        var file = w.ProgramMessage("technik")["Exportiert: ".Length..].Trim();
        var md = File.ReadAllText(file);
        Assert.Contains("Tabelle: M = qL²/8", md); Assert.Contains("| 62,5 |", md); Assert.Contains("ersetzt keine Prüfstatik", md);
        w.ProgramNew("technik", "rahmen"); w.Show("technik"); Settle();
        Assert.StartsWith("Gegenprobe ✓ Abweichung", Get<TextBlock>(w, "tk-crosscheck").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0748 UI: Stufe Einfach – Gegenprobe steht im Inspektor, Bericht über „Datei ▸ Exportieren“ in der Menüleiste")]
    public void Statik_Einfach_Menue()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.ProgramSetLevel(UserLevel.Einfach);
            w.ProgramNew("technik", "durchlauf"); w.Show("technik"); Settle();
            Assert.Contains("Gegenprobe ✓ Abweichung", Get<TextBlock>(w, "prog-status-text").Text); // Inspektor ist in „Einfach“ zu – die Statuszeile zeigt sie
            var mi = w.ActiveMenuBar()!.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Name == "menu-prog-export-gegenprobe");
            mi.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            var file = w.ProgramMessage("technik")["Exportiert: ".Length..].Trim();
            Assert.Contains("Formel Zweifeldträger", File.ReadAllText(file));
        }
        finally { w.Close(); }
    }

    const string Net = """
        (export (version "E")
          (components
            (comp (ref "BT1") (value "9V") (libsource (lib "Device") (part "Battery")))
            (comp (ref "R1") (value "330") (libsource (lib "Device") (part "R")))
            (comp (ref "D1") (value "LED red") (libsource (lib "Device") (part "LED"))))
          (nets
            (net (code "1") (name "GND") (node (ref "BT1") (pin "2") (pinfunction "-")) (node (ref "D1") (pin "1") (pinfunction "K")))
            (net (code "2") (name "Net-(BT1-+)") (node (ref "BT1") (pin "1") (pinfunction "+")) (node (ref "R1") (pin "1")))
            (net (code "3") (name "Net-(D1-A)") (node (ref "D1") (pin "2") (pinfunction "A")) (node (ref "R1") (pin "2")))))
        """;

    [AvaloniaFact(DisplayName = "T-0748 UI: Schaltung – KiCad-Netzliste über den Knopf öffnen, simulieren, Platine verlegen, Gerber-ZIP exportieren und prüfen")]
    public void KiCad_import_bis_Gerber()
    {
        var (w, _, _) = Ui.Open();
        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0748-ui-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        var net = Path.Combine(dir, "led.net"); File.WriteAllText(net, Net);
        try
        {
            w.ProgramSetLevel(UserLevel.Erweitert);
            w.Show(MainWindow.StudioPages["SPEC-02"]); Settle();
            var tpl = Get<Button>(w, "prog-tpl-leer"); tpl.BringIntoView(); Settle(); RealClick(w, tpl);
            w.PickOverride = _ => Task.FromResult<string[]?>([net]);
            Press(w, "prog-cmd-ek-kicad"); Settle();
            Assert.Contains("3 Bauteile und 3 Netze aus KiCad übernommen", w.ProgramMessage("elektronik"));
            var c = Circuit.From(w.ProgramActive("elektronik")!.Data);
            Assert.Equal("led", c.Name); Assert.Equal(2, w.ProgramDocs("elektronik").Count);
            w.CircuitAdvance(0.01);
            var led = c.Parts.Single(p => p.Kind == "led"); Assert.InRange(w.CircuitSim()!.Last!.Parts[led.Id].I, 0.015, 0.025);
            Press(w, "prog-cmd-ek-pcb"); Press(w, "prog-cmd-ek-route");
            Assert.Contains("✓ Prüfung: keine Konflikte", Get<TextBlock>(w, "ek-pcb-info").Text);
            Press(w, "prog-cmd-ek-fabcheck");
            Assert.StartsWith("✓ Fertigungsdaten gelesen: 5 Dateien", w.ProgramMessage("elektronik"));
            Press(w, "prog-export-fab");
            var zip = w.ProgramMessage("elektronik")["Exportiert: ".Length..].Trim();
            Assert.True(GerberCheck.ReadZip(zip).Ok);
            // Rückgängig nach dem Import: das Dokument hat noch keinen Schritt – die Leere Schaltung davor bleibt unberührt
            Assert.Empty(Circuit.From(w.ProgramDocs("elektronik")[0].Data).Parts);
        }
        finally { w.PickOverride = null; w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
