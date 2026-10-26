using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.App.Tests;

public class T0223_TrendoTextUi
{
    static void Key(Control c, Key k, KeyModifiers m = KeyModifiers.None) => c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = m });

    [AvaloniaFact(DisplayName = "T-0223 T1–T4 UI: tippen, Enter teilt, Rücktaste verbindet, Fett per Knopf und Strg+B, Absatzart, Liste, Ausrichtung, Rückgängig")]
    public void Write_and_format()
    {
        var (w, _, root) = Ui.Open();
        w.TextNew(); w.Show("text");
        var box = w.Find<TextBox>("text-edit");
        box.Text = "Hallo Welt"; Dispatcher.UIThread.RunJobs();
        Assert.Equal("Hallo Welt", ((Para)w.TextDocument.Blocks[0]).Text);
        Assert.True(w.TextDirty);
        box.CaretIndex = 5; Key(box, Avalonia.Input.Key.Enter); // teilen
        Assert.Equal(["Hallo", " Welt"], w.TextDocument.Paras.Select(p => p.Text));
        Assert.Equal(1, w.TextFocusBlock);
        var second = w.Find<TextBox>("text-edit"); second.CaretIndex = 0; second.SelectionStart = second.SelectionEnd = 0;
        Key(second, Avalonia.Input.Key.Back); // wieder verbinden
        Assert.Equal(["Hallo Welt"], w.TextDocument.Paras.Select(p => p.Text));
        w.TextSelect(0, 6, 4);
        w.Find<Button>("text-bold").Press();
        Assert.True(TextDoc.Has((Para)w.TextDocument.Blocks[0], 6, 4, Fmt.Bold));
        Assert.NotNull(w.TryFind<TextBlock>("text-para-0") ?? (object?)w.TryFind<TextBox>("text-edit"));
        w.TextSelect(0, 0, 5);
        Key(w.Find<TextBox>("text-edit"), Avalonia.Input.Key.I, KeyModifiers.Control);
        Assert.True(TextDoc.Has((Para)w.TextDocument.Blocks[0], 0, 5, Fmt.Italic));
        w.Find<ComboBox>("text-style").SelectedIndex = (int)ParaStyle.Heading1;
        Assert.Equal(ParaStyle.Heading1, ((Para)w.TextDocument.Blocks[0]).Style);
        w.Find<Button>("text-bullets").Press(); Assert.Equal(ListKind.Bullet, ((Para)w.TextDocument.Blocks[0]).List);
        w.Find<Button>("text-align-center").Press(); Assert.Equal(Align.Center, ((Para)w.TextDocument.Blocks[0]).Align);
        w.Find<Button>("text-undo").Press(); Assert.Equal(Align.Left, ((Para)w.TextDocument.Blocks[0]).Align);
        w.Find<Button>("text-redo").Press(); Assert.Equal(Align.Center, ((Para)w.TextDocument.Blocks[0]).Align);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0223 T5/T6/T7/T9/T10 UI: Tabelle und Seitenumbruch einfügen, Suchen & Ersetzen, Speichern und Öffnen, Als PDF, Vorlage")]
    public void Tables_find_save_pdf()
    {
        var (w, _, root) = Ui.Open();
        w.TextNew("rechnung"); w.Show("text");
        Assert.Contains(w.TextDocument.Blocks, b => b is Table);
        w.Find<Button>("text-more").Press();
        var before = w.TextDocument.Blocks.Count;
        w.TextSelect(0, 0); w.Show("text");
        w.Find<Button>("text-table").Press();
        Assert.IsType<Table>(w.TextDocument.Blocks[1]);
        Assert.True(w.TextDocument.Blocks.Count >= before + 1);
        var cell = w.Find<TextBox>("text-cell-1-0-0"); cell.Text = "Kosten"; Dispatcher.UIThread.RunJobs();
        Assert.Equal("Kosten", ((Table)w.TextDocument.Blocks[1]).Rows[0][0]);
        w.Find<Button>("text-row-add-1").Press(); Assert.Equal(4, ((Table)w.TextDocument.Blocks[1]).Rows.Count);
        w.Find<Button>("text-pagebreak").Press();
        Assert.Contains(w.TextDocument.Blocks, b => b is PageBreak);

        w.Find<Button>("text-find").Press();
        w.Find<TextBox>("text-find-q").Text = "Leistung"; w.Find<TextBox>("text-find-r").Text = "Arbeit";
        w.Find<Button>("text-replace-all").Press();
        Assert.Contains("ersetzt", w.Find<TextBlock>("text-status").Text);
        Assert.DoesNotContain("[Leistung]", w.TextDocument.PlainText());
        Assert.Contains("Arbeit", w.TextDocument.PlainText());

        var f = Path.Combine(root, "rechnung.odt");
        w.TextSaveAs(f); Assert.False(w.TextDirty);
        w.TextNew(); w.TextOpen(f); w.Show("text");
        Assert.Contains("Arbeit", w.TextDocument.PlainText());
        Assert.Equal(f, w.TextPath);
        w.Find<Button>("text-pdf").Press();
        Assert.Contains("PDF gespeichert", w.Find<TextBlock>("text-status").Text);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(w.ProjectsRootForTests, "Dokumente"), "*.pdf"));
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "text.save");
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0223 T8 UI: Rechtschreibung ohne Wörterbuch sagt, woher es kommt; Seite gehört zu „Texte & Tabellen“")]
    public void Spelling_without_dictionary()
    {
        var (w, _, _) = Ui.Open();
        w.TextNew(); w.Show("text");
        w.Find<Button>("text-spell").Press();
        Assert.Contains(w.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("Wörterbuch fehlt"));
        w.Show("office"); Assert.NotNull(w.TryFind<Button>("office-text"));
        w.Close();
    }
}
