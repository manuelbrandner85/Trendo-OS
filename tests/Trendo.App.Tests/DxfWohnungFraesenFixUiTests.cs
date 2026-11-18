using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Arch;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0747-fix (Gegenprüfung) über die Oberfläche: Bahnen aus der Zeichnung rechnen bei geänderten Werten neu bzw. sperren den Export;
/// große DXF-Dateien werden im Hintergrund gelesen, die Oberfläche bleibt bedienbar, „Abbrechen“ lässt das Dokument unverändert.</summary>
public class T0747_FixUi
{
    const string AK = "architektur", CK = "cnc";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); if (w.DrawingImportHold is null) WaitImport(w); } // T-0747-fix2: Hintergrund abwarten
    static void Type(MainWindow w, string box, string text)
    {
        var t = Get<TextBox>(w, box); t.BringIntoView(); t.Focus(); t.Text = text; Settle();
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle(); WaitImport(w);
    }
    static string Fixture(string name)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return Path.Combine(d!.FullName, "tests", "fixtures", "t0747", name);
    }
    static FloorPlan Plan(MainWindow w) => FloorPlan.From(w.ProgramActive(AK)!.Data);
    static CncJob Job(MainWindow w) => CncJob.From(w.ProgramActive(CK)!.Data);

    static void WaitImport(MainWindow w)
    {
        var sw = Stopwatch.StartNew();
        while (w.DrawingImportBusy && sw.Elapsed < TimeSpan.FromSeconds(180)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        Assert.False(w.DrawingImportBusy, "Einlesen im Hintergrund wurde nicht fertig");
        Settle();
    }

    static string BigDxf(int lines)
    {
        var sb = new StringBuilder("0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n"); var n = 0;
        void L(double x1, double y1, double x2, double y2) { if (n++ < lines) sb.Append(FormattableString.Invariant($"0\nLINE\n8\nWAND\n10\n{x1}\n20\n{y1}\n11\n{x2}\n21\n{y2}\n")); }
        for (var row = 0; n < lines; row++)
            for (var col = 0; col < 50 && n < lines; col++)
            {
                double x = col * 4000, y = row * 3000;
                L(x, y, x + 1500, y); L(x + 2400, y, x + 4000, y); L(x, y + 240, x + 1500, y + 240); L(x + 2400, y + 240, x + 4000, y + 240);
                L(x, y, x, y + 3000); L(x + 240, y + 240, x + 240, y + 3000);
            }
        return sb.Append("0\nENDSEC\n0\nEOF\n").ToString();
    }

    [AvaloniaFact(DisplayName = "T-0747-fix UI: Fräser-Ø/Vorschub geändert → Bahnen aus der Zeichnung neu (ein Schritt); geht es nicht, ist der Export gesperrt")]
    public void Cnc_values_recompute_or_lock_export()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var svg = Path.Combine(root, "teil.svg"); File.Copy(Fixture("teil-fremd.svg"), svg);
        w.Show(CK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Type(w, "cnc-p-tool", "4"); Type(w, "cnc-p-depth", "6"); Type(w, "cnc-p-step", "2");
        w.PickOverride = _ => Task.FromResult<string[]?>([svg]);
        Press(w, "prog-cmd-cnc-g-import");
        var g0 = Job(w).Gcode; Assert.True(Job(w).IsCamGcode);
        // Fräser-Ø ändern → Bahnen sofort neu, nicht veraltet
        Type(w, "cnc-p-tool", "3");
        var j = Job(w); Assert.Equal(3, j.ToolD); Assert.NotEqual(g0, j.Gcode); Assert.False(j.CamStale); Assert.Contains("Fräser Ø 3", w.ProgramMessage(CK));
        Assert.Contains("(Fräser Ø 3 mm", j.Gcode);
        Press(w, "prog-undo"); Assert.Equal(4, Job(w).ToolD); Assert.Equal(g0, Job(w).Gcode); // Wert und Bahnen in einem Schritt zurück
        Type(w, "cnc-p-feed", "900"); Assert.Contains(" F900\n", Job(w).Gcode); Assert.False(Job(w).CamStale);
        // nur innen fräsen und Fräser größer als jedes Loch → keine Bahn möglich: Wert bleibt, G-Code veraltet, Export gesperrt
        Get<ComboBox>(w, "cnc-p-cammode").SelectedIndex = 2; Settle(); WaitImport(w); Assert.Equal("innen", Job(w).CamMode);
        var before = Job(w).Gcode;
        Type(w, "cnc-p-tool", "70");
        Assert.Equal(70, Job(w).ToolD); Assert.Equal(before, Job(w).Gcode); Assert.True(Job(w).CamStale);
        Assert.Contains("gesperrt", w.ProgramMessage(CK));
        Assert.Contains("Bahnen veraltet", Get<TextBlock>(w, "cnc-cam-stale").Text);
        Press(w, "prog-export-nc");
        Assert.Contains("Bahnen neu", w.ProgramMessage(CK)); Assert.DoesNotContain("Exportiert", w.ProgramMessage(CK));
        // zurück auf einen passenden Fräser → wieder neu gerechnet, Export geht
        Type(w, "cnc-p-tool", "4");
        Assert.False(Job(w).CamStale);
        Press(w, "prog-export-nc");
        var file = w.ProgramMessage(CK)["Exportiert: ".Length..].Trim();
        Assert.Equal(Job(w).Gcode.Replace("\r", ""), File.ReadAllText(file).Replace("\r", ""));
        // von Hand geänderter G-Code ist kein Zeichnungs-G-Code mehr: Werte ändern rechnet nichts um
        var hand = Job(w).Gcode + "(von Hand)\n"; w.ProgramActive(CK)!.Edit("Hand", x => { var jj = CncJob.From(x); jj.Gcode = hand; jj.WriteTo(x); }); Settle();
        Type(w, "cnc-p-tool", "5"); Assert.Equal(hand, Job(w).Gcode); Assert.False(Job(w).CamStale);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0747-fix2 UI: Trendo CAD zeigt, dass eine Zoll-DXF in mm umgerechnet wurde")]
    public void Cad_import_shows_unit_conversion()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "zoll.dxf");
        File.WriteAllText(f, "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n1\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n0\nLINE\n8\n0\n10\n0\n20\n0\n11\n2\n21\n0\n0\nENDSEC\n0\nEOF\n");
        var msg = w.CadImport(f);
        Assert.Contains("Maße in mm umgerechnet aus Zoll (×25,4)", msg);
        var mm = Path.Combine(root, "mm.dxf"); File.WriteAllText(mm, File.ReadAllText(f).Replace("70\n1\n", "70\n4\n"));
        Assert.DoesNotContain("umgerechnet", w.CadImport(mm));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0747-fix UI: große DXF (12 000 Linien) im Hintergrund – Karte mit Abbrechen, Ergebnis als ein Schritt; Abbrechen ändert nichts")]
    public void Big_dxf_background_and_cancel()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var big = Path.Combine(root, "gross.dxf"); File.WriteAllText(big, BigDxf(12_000));
        w.Show(AK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        w.PickOverride = _ => Task.FromResult<string[]?>([big]);
        var sw = Stopwatch.StartNew();
        RealClick(w, Get<Button>(w, "prog-cmd-arch-dxf-import")); // ohne zu warten: die Oberfläche muss sofort frei sein
        var uiMs = sw.ElapsedMilliseconds;
        Assert.True(w.DrawingImportBusy);
        Assert.Empty(Plan(w).Walls); // noch nichts übernommen
        Get<Button>(w, "draw-import-cancel"); Assert.Contains("wird eingelesen", Get<Border>(w, "draw-import-card").Child is DockPanel dp ? string.Join(" ", dp.Children.OfType<TextBlock>().Select(t => t.Text)) : "");
        WaitImport(w);
        var total = sw.ElapsedMilliseconds;
        Assert.True(Plan(w).Walls.Count > 100, w.ProgramMessage(AK));
        Assert.Contains("DXF „gross.dxf“ eingelesen", w.ProgramMessage(AK));
        Console.WriteLine($"T-0747-fix: Klick bis Oberfläche wieder frei {uiMs} ms, fertig nach {total} ms, {Plan(w).Walls.Count} Wände");
        Press(w, "prog-undo"); Assert.Empty(Plan(w).Walls); // ein Schritt
        // Abbrechen: Dokument bleibt leer (das Einlesen wartet, bis abgebrochen ist – sonst wäre es je nach PC schon fertig)
        using var hold = new ManualResetEventSlim(false); w.DrawingImportHold = hold;
        Press(w, "prog-cmd-arch-dxf-import"); Assert.True(w.DrawingImportBusy);
        Press(w, "draw-import-cancel");
        WaitImport(w);
        Assert.Empty(Plan(w).Walls); Assert.Contains("abgebrochen", w.ProgramMessage(AK));
        // CNC: dieselbe Datei im Hintergrund, Abbrechen
        w.Show(CK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        var g = Job(w).Gcode;
        Press(w, "prog-cmd-cnc-g-import"); Assert.True(w.DrawingImportBusy);
        Press(w, "draw-import-cancel"); WaitImport(w);
        Assert.Equal(g, Job(w).Gcode); Assert.Empty(Job(w).Contours); Assert.Contains("abgebrochen", w.ProgramMessage(CK));
        w.Close();
    }
}
