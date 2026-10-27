using System.Diagnostics;
using System.IO.Compression;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0229: Trendo Präsentation – Modell, Gliederung, PPTX/ODP, PDF.</summary>
public class SlideTests
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-slide-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    const string Outline = """
        Unser Sommerfest
          - Planung für alle Nachbarn
        Ablauf
          - 14 Uhr Begrüßung
          - 15 Uhr Kuchenbuffet
            - selbst gebacken
          - 18 Uhr Grillen
        > Kurz die Helfer nennen
        Kosten
          - Getränke 120 €
          - Deko 40 €
        Danke!
        """;

    static SlideDoc Sample()
    {
        var d = SlideOutline.FromOutline(Outline, SlideThemes.Get("ozean"));
        var s = d.AddSlide(SlideLayout.NurTitel); s.ByRole("title")!.Text = "Zahlen";
        s.Shapes.Add(new SlideShape { Kind = ShapeKind.Tabelle, X = 2, Y = 5, W = 14, H = 6, FontSize = 16, Table = [["Posten", "Betrag"], ["Getränke", "120"], ["Deko", "40"]] });
        s.Shapes.Add(new SlideShape { Kind = ShapeKind.Diagramm, X = 17, Y = 5, W = 14, H = 10, Text = "Kosten", ChartKind = ChartKind.Kreis, ChartLabels = ["Getränke", "Deko"], ChartValues = [120, 40] });
        s.Shapes.Add(new SlideShape { Kind = ShapeKind.Bild, X = 2, Y = 12, W = 4, H = 4, Image = Png, Animation = Animation.Einblenden });
        s.Shapes.Add(new SlideShape { Kind = ShapeKind.Ellipse, X = 8, Y = 12, W = 4, H = 4, Fill = "#F59E0B", Text = "Neu!", Align = Align.Center, Animation = Animation.Hereinfliegen });
        s.Transition = Transition.Ueberblenden;
        d.Slides[1].Transition = Transition.Schieben;
        return d;
    }

    [Fact(DisplayName = "T-0229 P7/P1: Gliederung → Titelfolie, Inhaltsfolien mit Punkten und Unterpunkten, Notizen, Folie nur mit Titel")]
    public void Outline_to_slides()
    {
        var d = SlideOutline.FromOutline(Outline);
        Assert.Equal(4, d.Slides.Count);
        Assert.Equal(SlideLayout.Titel, d.Slides[0].Layout);
        Assert.Equal("Unser Sommerfest", d.Slides[0].ByRole("title")!.Text);
        Assert.Equal("Planung für alle Nachbarn", d.Slides[0].ByRole("sub")!.Text);
        Assert.Equal("14 Uhr Begrüßung\n15 Uhr Kuchenbuffet\n\tselbst gebacken\n18 Uhr Grillen", d.Slides[1].ByRole("body")!.Text);
        Assert.Equal("Kurz die Helfer nennen", d.Slides[1].Notes);
        Assert.Equal(SlideLayout.NurTitel, d.Slides[3].Layout);
        Assert.Contains("Folien", SlideOutline.Prompt("Sommerfest", 6));
        d.Move(3, 0); Assert.Equal("Danke!", d.Slides[0].Title);
        foreach (var (l, _) in SlideLayouts.Names) Assert.NotNull(SlideLayouts.Create(l));
    }

    [Fact(DisplayName = "T-0229 P6: PPTX speichern und wieder öffnen – Texte, Punkte, Tabelle, Diagramm, Bild, Form, Notizen, Übergänge, Animationen; kein Programmname")]
    public void Pptx_roundtrip()
    {
        var dir = Temp();
        try
        {
            var f = Path.Combine(dir, "fest.pptx"); SlideFiles.Save(Sample(), f);
            using (var z = ZipFile.OpenRead(f))
                foreach (var e in z.Entries.Where(e => e.Name.EndsWith(".xml", StringComparison.Ordinal)))
                    Assert.DoesNotContain("Trendo", new StreamReader(e.Open()).ReadToEnd());
            Check(SlideFiles.Open(f), full: true);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static void Check(SlideDoc b, bool full)
    {
        Assert.Equal(5, b.Slides.Count);
        Assert.Equal("Unser Sommerfest", b.Slides[0].Title);
        Assert.Equal(SlideLayout.Titel, b.Slides[0].Layout);
        var body = b.Slides[1].ByRole("body") ?? b.Slides[1].Shapes.First(s => s.Text.Contains("Begrüßung"));
        Assert.Contains("15 Uhr Kuchenbuffet", body.Text); Assert.Contains("\tselbst gebacken", body.Text); Assert.True(body.Bullets);
        Assert.Equal("Kurz die Helfer nennen", b.Slides[1].Notes);
        var z = b.Slides[4];
        var t = z.Shapes.Single(s => s.Kind == ShapeKind.Tabelle); Assert.Equal("Getränke", t.Table![1][0]);
        var img = z.Shapes.Single(s => s.Kind == ShapeKind.Bild); Assert.True(img.Image!.Length > 50);
        Assert.InRange(img.X, 1.9, 2.1); Assert.InRange(img.W, 3.9, 4.1);
        if (!full) return;
        var ch = z.Shapes.Single(s => s.Kind == ShapeKind.Diagramm); Assert.Equal(ChartKind.Kreis, ch.ChartKind); Assert.Equal([120.0, 40.0], ch.ChartValues);
        var el = z.Shapes.Single(s => s.Kind == ShapeKind.Ellipse); Assert.Equal("Neu!", el.Text); Assert.Equal("#F59E0B", el.Fill);
        Assert.Equal(Animation.Hereinfliegen, el.Animation); Assert.Equal(Animation.Einblenden, img.Animation);
        Assert.Equal(Transition.Ueberblenden, z.Transition); Assert.Equal(Transition.Schieben, b.Slides[1].Transition);
        Assert.Equal("ozean", b.Theme.Id);
    }

    [Fact(DisplayName = "T-0229 P6: PDF – eine Seite je Folie, Handzettel mit Notizen")]
    public void Pdf()
    {
        var dir = Temp();
        try
        {
            var f = Path.Combine(dir, "fest.pdf");
            Assert.Equal(5, SlidePdf.Export(Sample(), f));
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(f), 0, 4));
            Assert.Equal(5, SlidePdf.Export(Sample(), Path.Combine(dir, "notizen.pdf"), withNotes: true));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static string? Soffice()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge") })
            if (root is not null && Path.Combine(root, "libreoffice", "App", "libreoffice", "program", "soffice.exe") is var p && File.Exists(p)) return p;
        return null;
    }

    [Fact(DisplayName = "T-0229 P6: Abgleich mit LibreOffice Impress – Impress öffnet Trendos PPTX, speichert PPTX und ODP, Trendo liest beides (nur mit LibreOffice)")]
    public void Impress_roundtrip()
    {
        if (Soffice() is not { } soffice) return;
        var dir = Temp();
        try
        {
            var src = Path.Combine(dir, "fest.pptx"); SlideFiles.Save(Sample(), src);
            foreach (var (ext, filter) in new[] { ("pptx", "Impress MS PowerPoint 2007 XML"), ("odp", "impress8") })
            {
                var outDir = Path.Combine(dir, ext); Directory.CreateDirectory(outDir);
                var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var x in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(dir, "profil-" + ext)).AbsoluteUri, "--headless", "--norestore", "--convert-to", $"{ext}:{filter}", "--outdir", outDir, src }) psi.ArgumentList.Add(x);
                using (var p = Process.Start(psi)!) Assert.True(p.WaitForExit(180_000), "LibreOffice antwortet nicht");
                Check(SlideFiles.Open(Path.Combine(outDir, "fest." + ext)), full: false);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
