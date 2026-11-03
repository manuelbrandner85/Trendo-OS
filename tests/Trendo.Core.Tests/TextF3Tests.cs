using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>F3 Text (Vorbild-Analyse „Nächste Lücken“ 3): mehrstufige Listen und Formatierung löschen.</summary>
public class TextF3Tests
{
    static Para L(string t, ListKind k, int lvl = 0) => new(t) { List = k, ListLevel = lvl };

    static TextDoc Sample() => new()
    {
        Blocks =
        [
            new Para("Einkauf", ParaStyle.Heading1),
            L("Obst", ListKind.Number), L("Äpfel", ListKind.Number, 1), L("Birnen", ListKind.Number, 1), L("rot", ListKind.Bullet, 2),
            L("Gemüse", ListKind.Number), L("Möhren", ListKind.Number, 1), L("Brot", ListKind.Number),
            new Para("Ende"),
            L("neu", ListKind.Number),
        ],
    };

    [Fact(DisplayName = "Text F3: Listen-Nummern je Ebene – 1. a) i. bzw. • ◦ ▪, tiefere Ebenen zählen neu, Absatz ohne Liste beendet die Liste")]
    public void List_labels()
    {
        var l = TextTools.ListLabels(Sample());
        Assert.Equal([null, "1.", "a)", "b)", "▪", "2.", "a)", "3.", null, "1."], l);
        var d = new TextDoc { Blocks = [L("x", ListKind.Number, 2), L("y", ListKind.Number, 2), L("z", ListKind.Bullet, 1)] };
        Assert.Equal(["i.", "ii.", "◦"], TextTools.ListLabels(d));
        var big = new TextDoc { Blocks = [.. Enumerable.Range(0, 28).Select(_ => (Trendo.Core.Office.Block)L("p", ListKind.Number, 1))] };
        Assert.Equal("ab)", TextTools.ListLabels(big)[27]);
        var p = L("q", ListKind.Number);
        Assert.False(TextTools.ChangeListLevel(p, -1)); Assert.True(TextTools.ChangeListLevel(p, 1)); Assert.Equal(1, p.ListLevel);
        Assert.False(TextTools.ChangeListLevel(new Para("kein Punkt"), 1));
        var split = new TextDoc { Blocks = [L("Punkt", ListKind.Bullet, 2)] }; split.SplitAt(0, 5);
        Assert.Equal(2, ((Para)split.Blocks[1]).ListLevel); // Enter bleibt auf der Ebene
    }

    [Fact(DisplayName = "Text F3: Listenebenen überstehen DOCX und ODT (Speichern und wieder Öffnen), auch gemischt 1. mit • darunter")]
    public void List_levels_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-text-f3-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var doc = Sample(); foreach (var i in new[] { 2, 3 }) ((Para)doc.Blocks[i]).List = ListKind.Bullet; // Punkte unter „1. Obst“, a) unter „2. Gemüse“
            foreach (var ext in new[] { "docx", "odt" })
            {
                var f = Path.Combine(dir, "liste." + ext); TextFiles.Save(doc, f);
                var back = TextFiles.Open(f);
                var paras = back.Blocks.OfType<Para>().Where(x => x.Text.Length > 0).Select(x => (x.Text, x.List, x.ListLevel)).ToList();
                Assert.Equal(("Obst", ListKind.Number, 0), paras.Single(x => x.Text == "Obst"));
                Assert.Equal(("Äpfel", ListKind.Bullet, 1), paras.Single(x => x.Text == "Äpfel"));
                Assert.Equal(("Möhren", ListKind.Number, 1), paras.Single(x => x.Text == "Möhren"));
                Assert.Equal(("Birnen", ListKind.Bullet, 1), paras.Single(x => x.Text == "Birnen"));
                Assert.Equal(("rot", ListKind.Bullet, 2), paras.Single(x => x.Text == "rot"));
                Assert.Equal(("Brot", ListKind.Number, 0), paras.Single(x => x.Text == "Brot"));
                Assert.Equal(["1.", "◦", "◦", "▪", "2.", "a)", "3."], TextTools.ListLabels(back).Where(x => x is not null).Take(7)); // Nummern laufen über die Unterpunkte weiter
                Assert.Equal(("Ende", ListKind.None, 0), paras.Single(x => x.Text == "Ende"));
            }
            var pdf = Path.Combine(dir, "liste.pdf"); TextPdf.Export(doc, pdf);
            Assert.True(new FileInfo(pdf).Length > 500);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Text F3: Formatierung löschen – im markierten Bereich Fett, Größe, Farbe weg; ohne Markierung ganzer Absatz samt Absatzart und Einzug; Links und Liste bleiben")]
    public void Clear_format()
    {
        var p = new Para("Hallo schöne Welt") { Style = ParaStyle.Heading2, Align = Align.Center, IndentCm = 2, List = ListKind.Bullet, LineSpacing = 2 };
        TextDoc.Format(p, 0, 17, Fmt.Bold, true); TextDoc.Size(p, 0, 17, 20);
        p.Marks = [new Mark(0, 17, MarkKind.Color, "#FF0000"), new Mark(6, 6, MarkKind.Link, "https://example.org")];
        TextTools.ClearFormat(p, 6, 6); // nur „schöne“
        Assert.True(TextDoc.Has(p, 0, 5, Fmt.Bold)); Assert.False(TextDoc.Has(p, 6, 6, Fmt.Bold)); Assert.True(TextDoc.Has(p, 12, 5, Fmt.Bold));
        Assert.Equal("Hallo schöne Welt", p.Text);
        Assert.Contains(p.Marks, m => m.Kind == MarkKind.Color && m.Start == 0 && m.Length == 6);
        Assert.Contains(p.Marks, m => m.Kind == MarkKind.Color && m.Start == 12 && m.Length == 5);
        Assert.Contains(p.Marks, m => m.Kind == MarkKind.Link); // Link bleibt
        Assert.Equal(ParaStyle.Heading2, p.Style); // Absatz bleibt bei Markierung
        TextTools.ClearFormat(p); // ganzer Absatz
        Assert.False(TextDoc.Has(p, 0, 17, Fmt.Bold)); Assert.All(p.Runs(), r => Assert.Null(r.Size));
        Assert.Equal((ParaStyle.Text, Align.Left, 0.0, (double?)null), (p.Style, p.Align, p.IndentCm, p.LineSpacing));
        Assert.DoesNotContain(p.Marks, m => m.Kind == MarkKind.Color);
        Assert.Equal(ListKind.Bullet, p.List);
    }
}
