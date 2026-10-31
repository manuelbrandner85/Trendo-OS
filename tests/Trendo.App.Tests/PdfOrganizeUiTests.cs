using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0303: Trendo PDF – Miniaturleiste und „Seiten ordnen“ mit echter Maus (ziehen, auswählen), Drehen, Löschen, leere Seite, PDF einfügen, Rückgängig, Speichern als Kopie.
public class PdfOrganize_Ui
{
    static void Make(string file, params double[] widths)
    {
        using var d = new PdfSharp.Pdf.PdfDocument();
        foreach (var x in widths) { var p = d.AddPage(); p.Width = PdfSharp.Drawing.XUnit.FromPoint(x); p.Height = PdfSharp.Drawing.XUnit.FromPoint(600); }
        d.Save(file);
    }

    [AvaloniaFact(DisplayName = "T-0303: Miniatur springt zur Seite; Seiten ordnen: Ziehen verschiebt, Drehen, Löschen, leere Seite, PDF einfügen, Rückgängig, Kopie speichern – Original bleibt")]
    public void Ui_organize()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
        Point Mid(Control c, double fx = 0.5) => c.TranslatePoint(new Point(c.Bounds.Width * fx, c.Bounds.Height / 2), w)!.Value;
        var a = Path.Combine(root, "Vertrag.pdf"); var b = Path.Combine(root, "Anhang.pdf");
        Make(a, 200, 300, 400); Make(b, 500);
        var original = File.ReadAllBytes(a);
        w.OpenPdf(a); w.Show("pdf"); Settle();
        // Miniaturleiste
        Assert.NotNull(w.Find<Button>("pdf-thumb-2"));
        Click("pdf-thumb-2"); Assert.Equal(2, w.PdfPage);
        // Seiten ordnen: Seite 3 per Maus vor Seite 1 ziehen
        Click("pdf-organize-start"); Assert.True(w.PdfOrganizing);
        var t2 = V<Control>(w, "pdf-org-2"); t2.BringIntoView(); Settle();
        var from = Mid(t2); var to = Mid(V<Control>(w, "pdf-org-0"), 0.15);
        w.MouseDown(from, MouseButton.Left); for (var i = 1; i <= 5; i++) { w.MouseMove(from + (to - from) * (i / 5.0)); Settle(); } w.MouseUp(to, MouseButton.Left); Settle();
        Assert.Equal([2, 0, 1], w.PdfOrgPages.Select(p => p.Page));
        // Seite 2 (ursprünglich 1) anklicken und rechts drehen
        Click("pdf-org-1"); Assert.Equal([1], w.PdfOrgSelection);
        Click("pdf-org-right"); Assert.Equal(90, w.PdfOrgPages[1].Rotate);
        // letzte Seite löschen, leere Seite anhängen
        Click("pdf-org-2"); Click("pdf-org-delete"); Assert.Equal([2, 0], w.PdfOrgPages.Select(p => p.Page));
        Click("pdf-org-blank"); Assert.Equal(-1, w.PdfOrgPages[^1].Page);
        // andere PDF einfügen (Dateiwahl), dann Rückgängig
        w.PickOverride = _ => Task.FromResult<string[]?>([b]);
        Click("pdf-org-insert"); for (var i = 0; i < 50 && w.PdfOrgPages.Count < 4; i++) { Thread.Sleep(10); Settle(); }
        Assert.Equal(4, w.PdfOrgPages.Count); Assert.Equal(b, w.PdfOrgPages[3].File);
        Click("pdf-org-undo"); Assert.Equal(3, w.PdfOrgPages.Count);
        // speichern als Kopie
        Click("pdf-org-save");
        Assert.False(w.PdfOrganizing); Assert.EndsWith("(Trendo).pdf", w.PdfPath); Assert.Equal(3, w.PdfPages);
        Assert.Equal(90, PdfTools.PageRotation(w.PdfPath!, 1));
        Assert.Equal([400.0, 200, 200], PdfTools.Read(w.PdfPath!).PageSizes.Select(s => Math.Round(Math.Min(s.Width, s.Height))));
        Assert.Equal(original, File.ReadAllBytes(a));
    }
}
