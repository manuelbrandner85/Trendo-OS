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
    /// <summary>T-0748-fix: Gegenprobe und KiCad-Import laufen im Hintergrund – warten, bis das Ergebnis auf der Oberfläche angekommen ist.</summary>
    static void Until(Func<bool> done, string what)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!done()) { if (sw.Elapsed.TotalSeconds > 20) Assert.Fail("Zeitüberschreitung: " + what); Settle(); }
    }
    static string Check(MainWindow w) { Until(() => w.FrameCheckRunning is null, "Gegenprobe"); Settle(); return Get<TextBlock>(w, "tk-crosscheck").Text ?? ""; }

    [AvaloniaFact(DisplayName = "T-0748 UI: Tragwerk – Vorlage Einfeldträger zeigt „Gegenprobe ✓ Abweichung“; Rahmen ebenso; Export „Gegenprobe (Bericht)“ mit Tabellenformel")]
    public void Statik_Gegenprobe()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-03"]); Settle();
        var tpl = Get<Button>(w, "prog-tpl-einfeld"); tpl.BringIntoView(); Settle(); RealClick(w, tpl);
        Assert.StartsWith("Gegenprobe ✓ Abweichung", Check(w));
        // Last verdoppeln: Gegenprobe rechnet neu und bleibt ✓
        w.FrameEdit("Test", f => f.Loads.Single(l => l.Kind == "strecke").Value = 20); Settle();
        Assert.StartsWith("Gegenprobe ✓", Check(w));
        Press(w, "prog-export-gegenprobe");
        var file = w.ProgramMessage("technik")["Exportiert: ".Length..].Trim();
        var md = File.ReadAllText(file);
        Assert.Contains("Tabelle: M = qL²/8", md); Assert.Contains("| 62,5 |", md); Assert.Contains("ersetzt keine Prüfstatik", md);
        w.ProgramNew("technik", "rahmen"); w.Show("technik"); Settle();
        Assert.StartsWith("Gegenprobe ✓ Abweichung", Check(w));
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
            Until(() => w.FrameCheckRunning is null, "Gegenprobe"); Settle();
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
            Until(() => w.ProgramMessage("elektronik").Contains("aus KiCad übernommen"), "KiCad-Import im Hintergrund");
            Assert.Contains("3 Bauteile und 3 Netze aus KiCad übernommen", w.ProgramMessage("elektronik"));
            var c = Circuit.From(w.ProgramActive("elektronik")!.Data);
            Assert.Equal("led", c.Name); Assert.Equal(2, w.ProgramDocs("elektronik").Count);
            w.CircuitAdvance(0.01);
            var led = c.Parts.Single(p => p.Kind == "led"); Assert.InRange(w.CircuitSim()!.Last!.Parts[led.Id].I, 0.015, 0.025);
            Press(w, "prog-cmd-ek-pcb"); Press(w, "prog-cmd-ek-route");
            Assert.Contains("✓ Prüfung: keine Konflikte", Get<TextBlock>(w, "ek-pcb-info").Text);
            Press(w, "prog-cmd-ek-fabcheck");
            Assert.StartsWith("✓ Fertigungsdaten gelesen: 5 Dateien", w.ProgramMessage("elektronik"));
            Assert.Contains("nicht die Netze", w.ProgramMessage("elektronik"));
            Press(w, "prog-export-fab");
            var zip = w.ProgramMessage("elektronik")["Exportiert: ".Length..].Trim();
            Assert.True(GerberCheck.ReadZip(zip).Ok);
            // Rückgängig nach dem Import: das Dokument hat noch keinen Schritt – die Leere Schaltung davor bleibt unberührt
            Assert.Empty(Circuit.From(w.ProgramDocs("elektronik")[0].Data).Parts);
        }
        finally { w.PickOverride = null; w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
    [AvaloniaFact(DisplayName = "T-0748-fix UI: großes Tragwerk – „Gegenprobe läuft …“, Rechnung im Hintergrund, neue Änderung bricht ab; danach ✓ in Inspektor und Statuszeile")]
    public void Statik_Hintergrund()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.FrameCheckGrace = TimeSpan.Zero; // nicht warten: immer erst „läuft …“ zeigen
            w.ProgramNew("technik", "leer"); w.Show("technik"); Settle();
            w.FrameEdit("Rahmen", f =>
            {   // 10 Geschosse × 10 Felder = 210 Stäbe
                for (var st = 0; st < 10; st++) for (var b = 0; b <= 10; b++) f.AddMember(b * 6, st * 3.5, b * 6, (st + 1) * 3.5, "HEA 200");
                for (var st = 1; st <= 10; st++) for (var b = 0; b < 10; b++) f.AddLineLoad(f.AddMember(b * 6, st * 3.5, (b + 1) * 6, st * 3.5, "IPE 300").Id, 12);
                foreach (var n in f.Nodes.Where(n => Math.Abs(n.Y) < 1e-9)) n.Support = "fest";
            }); // ohne Settle: der Dispatcher läuft nicht, das Ergebnis kann die Anzeige noch nicht erreicht haben
            var first = w.FrameCheckRunning;
            Assert.NotNull(first);
            TextBlock Named(string n) => w.GetLogicalDescendants().OfType<TextBlock>().Last(t => t.Name == n);
            Assert.Equal("Gegenprobe läuft …", Named("tk-crosscheck").Text);
            Assert.Contains("Gegenprobe läuft …", Named("prog-status-text").Text);
            // neue Änderung, solange die erste Rechnung läuft: die alte wird abgebrochen bzw. verworfen, die neue zählt
            w.FrameEdit("Last", f => f.Loads.First(l => l.Kind == "strecke").Value = 15);
            Assert.NotNull(w.FrameCheckRunning); Assert.NotSame(first, w.FrameCheckRunning);
            try { Assert.True(first!.Wait(20000)); } catch (AggregateException e) when (e.InnerException is OperationCanceledException) { } // alte Rechnung: abgebrochen oder fertig – verworfen
            var text = Check(w);
            Assert.StartsWith("Gegenprobe ✓ Abweichung", text);
            Assert.Contains("Gegenprobe ✓ Abweichung", Get<TextBlock>(w, "prog-status-text").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0748-fix UI: Temperaturlast – Statuszeile sagt „nur Gleichgewicht geprüft“, nicht „✓ < 0,01 %“")]
    public void Statik_nur_Gleichgewicht()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.ProgramSetLevel(UserLevel.Einfach);
            w.ProgramNew("technik", "einfeld"); w.Show("technik"); Settle();
            w.FrameEdit("Temperatur", f => f.AddTemperature(f.Members[0].Id, 20)); Settle();
            Until(() => w.FrameCheckRunning is null, "Gegenprobe"); Settle();
            var status = Get<TextBlock>(w, "prog-status-text").Text ?? "";
            Assert.Contains("Gegenprobe: nur Gleichgewicht geprüft", status); Assert.DoesNotContain("< 0,01", status);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0748-fix UI: Export Fertigungsdaten mit erzwungenem Prüffehler – Meldung, vorige ZIP bleibt unverändert, keine Zwischendatei")]
    public void Fab_export_atomar()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.ProgramSetLevel(UserLevel.Erweitert);
            w.ProgramNew("elektronik", "led"); w.Show("elektronik"); Settle();
            Press(w, "prog-export-fab");
            var zip = w.ProgramMessage("elektronik")["Exportiert: ".Length..].Trim();
            var good = File.ReadAllBytes(zip);
            w.FabVerifyOverride = _ => "erzwungener Prüffehler";
            w.CircuitEdit("Bauteil verschieben", c => c.Parts.First(p => p.Kind == "widerstand").Value = 470); Settle();
            Press(w, "prog-export-fab");
            Assert.Contains("erzwungener Prüffehler", w.ProgramMessage("elektronik") + " " + string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
            Assert.Equal(good, File.ReadAllBytes(zip));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(zip)!, Path.GetFileName(zip) + ".*.tmp"));
        }
        finally { w.FabVerifyOverride = null; w.Close(); }
    }
}
