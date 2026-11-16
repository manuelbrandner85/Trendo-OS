using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.App.Tests;

/// T-0692 D1a: Trendo Text als durchgehender Texteditor – echte Tastendrücke und Mausklicks im Headless-Fenster.
public class T0692_TextEditorUi
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static void Keys(MainWindow w, PhysicalKey k, RawInputModifiers m = RawInputModifiers.None) { w.KeyPressQwerty(k, m); w.KeyReleaseQwerty(k, m); Pump(); }
    static void Type(MainWindow w, string text) { w.KeyTextInput(text); Pump(); }
    static Point At(MainWindow w, TextPos p) => w.TextView.TranslatePoint(w.TextView.PointOf(p), w)!.Value;
    static void Click(MainWindow w, Point pt, RawInputModifiers m = RawInputModifiers.None, int clicks = 1)
    {
        for (var i = 0; i < clicks; i++) { w.MouseDown(pt, MouseButton.Left, m); w.MouseUp(pt, MouseButton.Left, m); }
        Pump();
    }
    static Para P(MainWindow w, int i) => (Para)w.TextDocument.Blocks[i];

    [AvaloniaFact(DisplayName = "T-0692 UI: leere Seite zeigt „Hier schreiben …“, Tippen über die Tastatur, Enter teilt, Umschalt+Pfeil markiert über drei Absätze, Strg+B setzt alle fett, Weitertippen bleibt fett, Strg+Z")]
    public void Keyboard_across_paragraphs()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.TextNew(); w.Show("text"); Pump();
            var v = w.Find<TextPageView>("text-edit");
            Assert.True(v.IsEmptyDocument); Assert.True(v.IsFocused); // der erste Schritt ist sichtbar: Cursor steht auf der leeren Seite
            Type(w, "Erster Absatz"); Keys(w, PhysicalKey.Enter); Type(w, "Zweiter Absatz"); Keys(w, PhysicalKey.Enter); Type(w, "Dritter Absatz");
            Assert.Equal(["Erster Absatz", "Zweiter Absatz", "Dritter Absatz"], w.TextDocument.Paras.Select(p => p.Text));
            Assert.False(v.IsEmptyDocument); Assert.True(w.TextDirty);
            // Strg+Pos1, dann Umschalt+Pfeil runter zweimal und Umschalt+Ende: Auswahl über drei Absätze
            Keys(w, PhysicalKey.Home, RawInputModifiers.Control); Assert.Equal(new TextPos(0, 0), v.Caret);
            Keys(w, PhysicalKey.ArrowRight, RawInputModifiers.Control); Assert.Equal(new TextPos(0, 7), v.Caret); // wortweise
            Keys(w, PhysicalKey.ArrowDown, RawInputModifiers.Shift); Keys(w, PhysicalKey.ArrowDown, RawInputModifiers.Shift); Keys(w, PhysicalKey.End, RawInputModifiers.Shift);
            Assert.Equal((new TextPos(0, 7), new TextPos(2, 14)), v.Range);
            Assert.Equal("Absatz\nZweiter Absatz\nDritter Absatz", v.SelectedText());
            Keys(w, PhysicalKey.B, RawInputModifiers.Control);
            Assert.True(TextDoc.Has(P(w, 0), 7, 6, Fmt.Bold)); Assert.False(TextDoc.Has(P(w, 0), 0, 6, Fmt.Bold)); Assert.True(TextDoc.Has(P(w, 1), 0, 14, Fmt.Bold)); Assert.True(TextDoc.Has(P(w, 2), 0, 14, Fmt.Bold));
            Assert.Contains(w.TextView.Flow.Pages[0].Items.OfType<FlowText>(), t => t.Text == "Zweiter" && t.Font.Bold); // auf der Seite sichtbar
            Keys(w, PhysicalKey.End); Type(w, " und mehr");
            Assert.Equal("Dritter Absatz und mehr", P(w, 2).Text); Assert.True(TextDoc.Has(P(w, 2), 14, 9, Fmt.Bold)); // Weitertippen bleibt fett
            Keys(w, PhysicalKey.Z, RawInputModifiers.Control); Assert.Equal("Dritter Absatz", P(w, 2).Text); // ein Schritt für das Getippte
            Keys(w, PhysicalKey.Z, RawInputModifiers.Control); Assert.False(TextDoc.Has(P(w, 1), 0, 14, Fmt.Bold));
            Keys(w, PhysicalKey.Y, RawInputModifiers.Control); Assert.True(TextDoc.Has(P(w, 1), 0, 14, Fmt.Bold));
            // Pos1/Ende, Rücktaste am Absatzanfang verbindet, Entf löscht vorwärts
            v.Select(new TextPos(1, 0), new TextPos(1, 0)); Keys(w, PhysicalKey.Backspace);
            Assert.Equal(["Erster AbsatzZweiter Absatz", "Dritter Absatz"], w.TextDocument.Paras.Select(p => p.Text)); Assert.Equal(new TextPos(0, 13), v.Caret);
            Keys(w, PhysicalKey.Delete); Assert.Equal("Erster Absatzweiter Absatz", P(w, 0).Text);
            // Strg+A, Strg+X, Strg+V: alles weg und mit Format wieder da
            Keys(w, PhysicalKey.A, RawInputModifiers.Control); Assert.Equal((new TextPos(0, 0), new TextPos(1, 14)), v.Range);
            Keys(w, PhysicalKey.X, RawInputModifiers.Control); Assert.Single(w.TextDocument.Blocks); Assert.Equal("", P(w, 0).Text);
            v.PasteText(null); Pump(); // Zwischenablage des Systems gibt es im Testfenster nicht: Trendos eigener Zwischenspeicher
            Assert.Equal(["Erster Absatzweiter Absatz", "Dritter Absatz"], w.TextDocument.Paras.Select(p => p.Text)); Assert.True(TextDoc.Has(P(w, 1), 0, 14, Fmt.Bold));
            v.PasteText("Von außen\r\nzwei Zeilen"); Pump(); // reiner Text von außen: ohne Format, Zeilen werden Absätze
            Assert.Equal("Dritter AbsatzVon außen", P(w, 1).Text); Assert.Equal("zwei Zeilen", P(w, 2).Text); Assert.False(TextDoc.Has(P(w, 2), 0, 4, Fmt.Bold));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0692 UI: Maus – Klick setzt den Cursor, Umschalt+Klick markiert über Absatzgrenzen, Doppelklick markiert ein Wort, Dreifachklick den Absatz, Ziehen markiert")]
    public void Mouse_selection()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.TextNew(); w.TextDocument.Blocks.Clear();
            w.TextDocument.Blocks.AddRange([new Para("Die Sonne scheint hell"), new Para("Der Wind weht kalt"), new Para("Es regnet nicht")]);
            w.Show("text"); Pump();
            var v = w.TextView;
            Click(w, At(w, new TextPos(0, 4))); Assert.Equal(new TextPos(0, 4), v.Caret); Assert.False(v.HasSelection);
            Click(w, At(w, new TextPos(2, 2)), RawInputModifiers.Shift);
            Assert.Equal((new TextPos(0, 4), new TextPos(2, 2)), v.Range); Assert.Equal("Sonne scheint hell\nDer Wind weht kalt\nEs", v.SelectedText());
            Click(w, At(w, new TextPos(1, 6)), clicks: 2); Assert.Equal((new TextPos(1, 4), new TextPos(1, 8)), v.Range); // „Wind“
            Click(w, At(w, new TextPos(2, 5)), clicks: 3); Assert.Equal((new TextPos(2, 0), new TextPos(2, 15)), v.Range);
            // Ziehen von „Sonne“ bis in den zweiten Absatz
            var a = At(w, new TextPos(0, 4)); var b = At(w, new TextPos(1, 3));
            w.MouseDown(a, MouseButton.Left); w.MouseMove(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), RawInputModifiers.LeftMouseButton); w.MouseMove(b, RawInputModifiers.LeftMouseButton); w.MouseUp(b, MouseButton.Left); Pump();
            Assert.Equal((new TextPos(0, 4), new TextPos(1, 3)), v.Range);
            w.Find<Button>("text-italic").Press(); Pump(); // Knopf wirkt auf die ganze Mausauswahl
            Assert.True(TextDoc.Has(P(w, 0), 4, 18, Fmt.Italic)); Assert.True(TextDoc.Has(P(w, 1), 0, 3, Fmt.Italic)); Assert.False(TextDoc.Has(P(w, 1), 3, 1, Fmt.Italic));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0692 UI: echte Seiten – 3 Seiten am Bildschirm wie im PDF, Status „Seite x von y“ folgt dem Cursor, Strg+Ende springt auf die letzte Seite, Strg+Enter macht einen Seitenumbruch")]
    public void Pages_on_screen()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.TextNew(); w.TextDocument.Blocks.Clear();
            for (var i = 0; i < 70; i++) w.TextDocument.Blocks.Add(new Para($"Absatz {i}: Ein Satz, der lang genug ist, damit er auf der Seite eine ganze Zeile füllt und umbricht."));
            w.TextDocument.Page.PageNumbers = true;
            w.Show("text"); Pump();
            var v = w.TextView;
            Assert.True(v.PageCount >= 3, $"{v.PageCount} Seiten");
            var pdf = Path.Combine(root, "seiten.pdf");
            Assert.Equal(v.PageCount, TextPdf.Export(w.TextDocument, pdf)); // derselbe Umbruch wie im PDF
            Assert.Contains($"Seite 1 von {v.PageCount}", w.Find<TextBlock>("text-status").Text);
            Keys(w, PhysicalKey.End, RawInputModifiers.Control);
            Assert.Contains($"Seite {v.PageCount} von {v.PageCount}", w.Find<TextBlock>("text-status").Text);
            var before = v.PageCount;
            v.Select(new TextPos(10, 0), new TextPos(10, 0)); Keys(w, PhysicalKey.Enter, RawInputModifiers.Control);
            Assert.IsType<PageBreak>(w.TextDocument.Blocks[11]); Assert.Equal(new TextPos(12, 0), v.Caret);
            Assert.Equal(2, v.CaretPage); Assert.True(v.PageCount >= before);
            Assert.Contains(v.Flow.Pages[^1].Items.OfType<FlowAligned>(), a => a.Text == $"Seite {v.PageCount} von {v.PageCount}");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0692 UI: Tabelle im Text – in die Zelle klicken und tippen, Tab springt zur nächsten Zelle, fett in der Zelle, „Verbinden →“ verbindet, Speichern als DOCX und ODT behält Format und Verbindung")]
    public void Table_cells()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.TextNew(); w.Show("text"); Pump();
            Type(w, "Vor der Tabelle");
            w.Find<Button>("text-more").Press(); Pump();
            w.Find<Button>("text-table").Press(); Pump();
            var t = Assert.IsType<Table>(w.TextDocument.Blocks[1]);
            Click(w, At(w, new TextPos(1, 0, 0, 0))); Assert.Equal(new TextPos(1, 0, 0, 0), w.TextView.Caret);
            Type(w, "Kopf"); Keys(w, PhysicalKey.Tab); Type(w, "B"); Keys(w, PhysicalKey.Tab); Type(w, "C");
            Assert.Equal(["Kopf", "B", "C"], t.Rows[0]);
            Click(w, At(w, new TextPos(1, 2, 0, 0)), clicks: 2); Keys(w, PhysicalKey.B, RawInputModifiers.Control); // Doppelklick auf „Kopf“, fett
            Assert.True(TextDoc.Has(t.CellPara(0, 0), 0, 4, Fmt.Bold));
            w.Find<Button>("text-cell-merge-right-1").Press(); Pump();
            Assert.Equal((2, 1), t.SpanOf(0, 0)); Assert.Equal("Kopf\nB", t.Rows[0][0]);
            Assert.Equal(2 + 3 + 3, w.TextView.Flow.Boxes.Count(b => b.Kind == FlowBoxKind.Cell)); // die verbundene Zelle ist auf der Seite eine
            foreach (var ext in new[] { "docx", "odt" })
            {
                var f = Path.Combine(root, "tabelle." + ext); Assert.True(w.TextSaveAs(f));
                var back = TextFiles.Open(f).Blocks.OfType<Table>().Single();
                Assert.Equal((2, 1), back.SpanOf(0, 0)); Assert.True(TextDoc.Has(back.CellPara(0, 0), 0, 4, Fmt.Bold)); Assert.Equal("C", back.Rows[0][2]);
            }
            w.Find<Button>("text-undo").Press(); Pump(); Assert.Equal((1, 1), ((Table)w.TextDocument.Blocks[1]).SpanOf(0, 0)); // Rückgängig löst die Verbindung
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0692 V-20: „Einfach“ zeigt eine Werkzeugzeile mit den wichtigsten Formaten und genau einer goldenen Hauptaktion; alles Weitere liegt unter „Mehr …“ und geht nicht verloren")]
    public void Simple_one_row()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Einfach);
            w.TextNew(); w.Show("text"); Pump();
            var bar = w.Find<WrapPanel>("text-toolbar");
            var names = bar.Children.Select(c => c.Name).Where(n => n is not null).ToList();
            foreach (var n in new[] { "text-save", "text-undo", "text-style", "text-size", "text-color", "text-bold", "text-italic", "text-underline", "text-bullets", "text-numbers", "text-align-left", "text-align-center", "text-more" }) Assert.Contains(n, names);
            foreach (var n in new[] { "text-pdf", "text-font", "text-find", "text-spell", "text-list-in", "text-align-justify" }) Assert.DoesNotContain(n, names);
            Assert.Null(w.TryFind<StackPanel>("text-style-gallery"));
            var gold = (Avalonia.Media.ISolidColorBrush)w.Find<Button>("text-save").Background!;
            Assert.Single(bar.Children.OfType<Button>(), b => b.Background is Avalonia.Media.ISolidColorBrush s && s.Color == gold.Color); // genau eine goldene Hauptaktion
            var more = w.Find<WrapPanel>("text-more-bar"); Assert.False(more.IsVisible);
            Assert.Contains(more.Children, c => c.Name == "text-find"); Assert.Contains(more.Children, c => c.Name == "text-pdf"); // nichts geht verloren
            w.Find<Button>("text-more").Press(); Pump();
            Assert.True(w.Find<WrapPanel>("text-more-bar").IsVisible); Assert.Contains(w.Find<WrapPanel>("text-toolbar").Children, c => c.Name == "text-find");
        }
        finally { w.Close(); }
    }
}
