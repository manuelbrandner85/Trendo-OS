using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.App.Tests;

/// Pilot 0.9.47 (Vorbild Word/Writer): Schriftart, Größe, Farbe, Hervorhebung, Zeilen- und Absatzabstand im Menüband „Start“, Rechtsklick-Menü im Text.
public class Pilot_TextLookUi
{
    static void Click(MenuItem mi) => mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    static MenuItem Item(Control owner, string name) => owner.ContextMenu!.Items.OfType<MenuItem>().SelectMany(m => new[] { m }.Concat(m.Items.OfType<MenuItem>())).First(m => m.Name == name);

    [AvaloniaFact(DisplayName = "Pilot Text UI: Schriftart, Größe, Farbe und Hervorhebung über das Menüband, Zeilenabstand und Erstzeileneinzug, alles rückgängig machbar")]
    public void Ribbon_look()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.TextNew(); w.Show("text");
            w.Find<TextBox>("text-edit").Text = "Gold glänzt im Dunkeln"; Dispatcher.UIThread.RunJobs();
            Para P() => (Para)w.TextDocument.Blocks[0];
            w.TextSelect(0, 0, 4); w.Show("text");
            var font = w.Find<ComboBox>("text-font");
            font.SelectedIndex = font.Items.OfType<ComboBoxItem>().ToList().FindIndex(i => (string?)i.Content == "Georgia");
            Assert.Equal("Georgia", TextDoc.InkAt(P(), 1, MarkKind.Font));
            w.TextSelect(0, 0, 4); w.Show("text");
            w.Find<ComboBox>("text-size").SelectedIndex = Array.IndexOf(MainWindow.TextSizes, 24.0);
            Assert.Equal(24, P().At(1).Size);
            w.TextSelect(0, 0, 4); w.Show("text");
            Click(Item(w.Find<Button>("text-color"), "text-color-2"));
            Assert.Equal("#C00000", TextDoc.InkAt(P(), 0, MarkKind.Color));
            w.TextSelect(0, 5, 6); w.Show("text");
            Click(Item(w.Find<Button>("text-highlight"), "text-highlight-0"));
            Assert.Equal("#FFF59D", TextDoc.InkAt(P(), 6, MarkKind.Highlight));
            // ohne Markierung: das Wort am Cursor (wie Word)
            w.TextSelect(0, 14, 0); w.Show("text");
            Click(Item(w.Find<Button>("text-color"), "text-color-6"));
            Assert.Equal("#1971C2", TextDoc.InkAt(P(), 12, MarkKind.Color)); Assert.Equal("#1971C2", TextDoc.InkAt(P(), 13, MarkKind.Color)); Assert.Null(TextDoc.InkAt(P(), 11, MarkKind.Color)); // „im“
            Click(Item(w.Find<Button>("text-linespacing"), "text-linespacing-2"));
            Assert.Equal(1.5, P().LineSpacing);
            Click(Item(w.Find<Button>("text-paraspace"), "text-paraspace-7"));
            Assert.Equal(1.25, P().FirstLineCm);
            // in der Anzeige (Absatz ohne Fokus) erscheinen Farbe und Schrift
            w.TextSelect(0, 0, 0); w.TextDocument.Blocks.Add(new Para("zweiter")); w.TextSelect(1, 0, 0); w.Show("text");
            var view = w.Find<TextBlock>("text-para-0");
            Assert.Contains(view.Inlines!.OfType<Avalonia.Controls.Documents.Run>(), r => r.Text == "Gold" && r.FontFamily.Name == "Georgia" && r.Foreground is Avalonia.Media.ISolidColorBrush { Color: var c } && c == Avalonia.Media.Color.Parse("#C00000"));
            w.Find<Button>("text-undo").Press(); // Erstzeileneinzug weg
            Assert.Equal(0, ((Para)w.TextDocument.Blocks[0]).FirstLineCm);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Pilot Text Leistung: 300 formatierte Absätze (ca. 15 Seiten) – Seite aufbauen unter 3 s, Formatwechsel unter 1,5 s")]
    public void Performance()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.TextNew(); w.Show("text");
            var doc = w.TextDocument; doc.Blocks.Clear();
            for (var i = 0; i < 300; i++)
            {
                var p = new Para($"Absatz {i}: Trendo Text setzt Schrift, Farbe und Abstände wie ein Profi-Schreibprogramm.") { LineSpacing = 1.15 };
                TextDoc.SetInk(p, 0, 6, MarkKind.Color, "#B8860B"); TextDoc.Format(p, 0, 6, Fmt.Bold, true);
                doc.Blocks.Add(p);
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            w.Show("text"); Dispatcher.UIThread.RunJobs();
            var build = sw.ElapsedMilliseconds;
            w.TextSelect(150, 0, 6); sw.Restart();
            w.TextSetInk(MarkKind.Font, "Georgia"); w.Show("text"); Dispatcher.UIThread.RunJobs();
            var change = sw.ElapsedMilliseconds;
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "trendo-leistung-text.txt"), $"{DateTime.Now:s} aufbau={build}ms formatwechsel={change}ms" + Environment.NewLine);
            Assert.True(build < 3000, $"Seite aufbauen dauerte {build} ms");
            Assert.True(change < 1500, $"Formatwechsel dauerte {change} ms");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Pilot Text UI: Rechtsklick im Text zeigt Ausschneiden/Kopieren/Einfügen, Formate, Farbe, Absatzart, Link, Kommentar und „✳ Claude fragen“")]
    public void Context_menu()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.TextNew(); w.Show("text");
            var box = w.Find<TextBox>("text-edit"); box.Text = "Wichtiger Satz"; Dispatcher.UIThread.RunJobs();
            w.TextSelect(0, 0, 9); w.Show("text");
            var menu = w.Find<TextBox>("text-edit").ContextMenu!;
            var names = menu.Items.OfType<MenuItem>().Select(m => m.Name).ToList();
            foreach (var n in new[] { "text-context-cut", "text-context-copy", "text-context-paste", "text-context-bold", "text-context-color", "text-context-mark", "text-context-style", "text-context-link", "text-context-comment" })
                Assert.Contains(n, names);
            Assert.Contains("text-context-claude", names); // Claude gehört in jedes Programm
            Click(Item(w.Find<TextBox>("text-edit"), "text-context-bold"));
            Assert.True(TextDoc.Has((Para)w.TextDocument.Blocks[0], 0, 9, Fmt.Bold));
            w.TextSelect(0, 0, 9); w.Show("text");
            Click(Item(w.Find<TextBox>("text-edit"), "text-context-style-heading2"));
            Assert.Equal(ParaStyle.Heading2, ((Para)w.TextDocument.Blocks[0]).Style);
        }
        finally { w.Close(); }
    }
}
