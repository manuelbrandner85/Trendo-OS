using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Recherche mit Markierungen (Masterprompt 131): Textmarker in vier Farben mit Notiz, Randliste der Markierungen, Zitat kopieren,
/// Zitatsammlung über alle Quellen der Sitzung und Literaturliste (einfach/APA). Flüchtig wie die Sitzung – „Als Bericht speichern“ nimmt alles mit.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Ordner für den Bericht statt „Dokumente\Trendo Recherche“.</summary>
    public string? ResearchReportFolder { get; set; }
    public string? LastClipboardText { get; private set; }

    static string MarkColor(string c, int alpha = 0xFF) => $"#{alpha:X2}" + c switch { "grün" => "22B35E", "blau" => "2F86F6", "rot" => "E5484D", _ => "FACC15" };

    /// <summary>Lesetext als Abschnitte: Markierungen farbig hinterlegt, „## “-Zeilen als Überschrift (das Kürzel bleibt unsichtbar im Text, damit die Zeichenpositionen der Markierungen stimmen).</summary>
    static Avalonia.Controls.Documents.InlineCollection ReaderInlines(string text, IReadOnlyList<Highlight> marks)
    {
        var heading = new bool[text.Length]; var prefix = new bool[text.Length]; var lineStart = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != '\n') continue;
            if (text.AsSpan(lineStart, i - lineStart).StartsWith("## ")) { for (var k = lineStart; k < i; k++) heading[k] = true; for (var k = lineStart; k < lineStart + 3; k++) prefix[k] = true; }
            lineStart = i + 1;
        }
        var owner = new Highlight?[text.Length];
        foreach (var (seg, m, at) in ResearchDesk.Segments(text, marks).Aggregate((List: new List<(string, Highlight?, int)>(), Pos: 0), (acc, s) => { acc.List.Add((s.Text, s.Mark, acc.Pos)); return (acc.List, acc.Pos + s.Text.Length); }).List)
            for (var k = at; k < at + seg.Length; k++) owner[k] = m;
        var inl = new Avalonia.Controls.Documents.InlineCollection(); var from = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && heading[i] == heading[from] && prefix[i] == prefix[from] && ReferenceEquals(owner[i], owner[from])) continue;
            var run = new Avalonia.Controls.Documents.Run(text[from..i]);
            if (prefix[from]) { run.FontSize = 0.1; run.Foreground = Brushes.Transparent; }
            else if (heading[from]) { run.FontSize = 18; run.FontWeight = FontWeight.Bold; run.Foreground = Brush.Parse("#F6D98C"); }
            if (owner[from] is { } m2 && !prefix[from]) { run.Background = Brush.Parse(MarkColor(m2.Color, 0x66)); run.Foreground = Brushes.White; }
            inl.Add(run); from = i;
        }
        return inl;
    }

    void ResearchCopy(string text) { LastClipboardText = text; if (Clipboard is { } cb) _ = cb.SetTextAsync(text); }

    /// <summary>Gewählte Stelle im Lesetext markieren (öffentlich für Tests und die KI).</summary>
    public Highlight? ResearchMark(string color, string note = "")
    {
        if (ReaderShown is not { } p || _rsTextBlock is null) return null;
        var a = Math.Min(_rsTextBlock.SelectionStart, _rsTextBlock.SelectionEnd); var b = Math.Max(_rsTextBlock.SelectionStart, _rsTextBlock.SelectionEnd);
        try { var h = Desk.AddHighlight(_rsShownUrl, p.Title, p.Text, a, b - a, color, note); _rsMsg = $"Markiert ({color}): „{(h.Text.Length > 60 ? h.Text[..60] + " …" : h.Text)}“"; return h; }
        catch (ArgumentException ex) { _rsMsg = ex.Message; return null; }
        finally { Show("recherche"); }
    }

    Control ResearchMarkBar(ReaderPage p)
    {
        var note = new TextBox { Name = "rs-mark-note", PlaceholderText = "Notiz zur Markierung (freiwillig)", Width = 260 };
        var bar = new WrapPanel { Name = "rs-markbar", Children = { new TextBlock { Text = "Text auswählen, dann markieren:", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) } } };
        foreach (var c in ResearchDesk.Colors)
        {
            var cc = c;
            var b = new Button { Name = "rs-mark-" + (c == "grün" ? "gruen" : c), Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(14),
                Background = Brush.Parse(MarkColor(c, 0x55)), BorderBrush = Brush.Parse(MarkColor(c)), BorderThickness = new Thickness(1.5),
                Content = new TextBlock { Text = char.ToUpperInvariant(c[0]) + c[1..], Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeight.SemiBold } };
            ToolTip.SetTip(b, $"Auswahl {c} markieren");
            b.Click += (_, _) => ResearchMark(cc, note.Text ?? "");
            bar.Children.Add(b);
        }
        bar.Children.Add(note);
        return bar;
    }

    Control ResearchMarksSide()
    {
        var marks = Desk.Highlights(_rsShownUrl);
        var sp = new StackPanel { Name = "rs-marks", Spacing = 8, Children = { H($"Markierungen ({marks.Count})", 14) } };
        if (marks.Count == 0) sp.Children.Add(P("Noch keine. Wähle im Text eine Stelle aus und tippe oben eine Farbe an.", Muted, 12));
        for (var i = 0; i < marks.Count; i++)
        {
            var h = marks[i]; var id = h.Id;
            var note = new TextBox { Name = $"rs-hl-note-{i}", Text = h.Note, PlaceholderText = "Notiz …", FontSize = 12, AcceptsReturn = false };
            note.LostFocus += (_, _) => { if ((note.Text ?? "") != h.Note) Desk.SetHighlight(id, note: note.Text ?? ""); };
            note.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { Desk.SetHighlight(id, note: note.Text ?? ""); _rsMsg = "Notiz gespeichert."; Show("recherche"); } };
            var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            foreach (var c in ResearchDesk.Colors)
            {
                var cc = c; var dot = new Button { Name = $"rs-hl-color-{i}-{(c == "grün" ? "gruen" : c)}", Width = 18, Height = 18, Padding = new Thickness(0), CornerRadius = new CornerRadius(9), Background = Brush.Parse(MarkColor(c)), BorderBrush = Brushes.White, BorderThickness = new Thickness(h.Color == c ? 2 : 0) };
                dot.Click += (_, _) => { Desk.SetHighlight(id, color: cc); Show("recherche"); }; colors.Children.Add(dot);
            }
            sp.Children.Add(new Border { Name = $"rs-hl-{i}", Background = Brush.Parse("#10FFFFFF"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8), BorderBrush = Brush.Parse(MarkColor(h.Color)), BorderThickness = new Thickness(4, 0, 0, 0),
                Child = new StackPanel { Spacing = 6, Children = {
                    new TextBlock { Text = "„" + (h.Text.Length > 220 ? h.Text[..220] + " …" : h.Text) + "“", Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, FontStyle = FontStyle.Italic },
                    note,
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { colors, Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                        Act($"rs-hl-copy-{i}", "Zitat kopieren", () => { ResearchCopy(Desk.Quote(Desk.Highlights().First(x => x.Id == id), _rsStyle)); _rsMsg = "Zitat mit Quelle kopiert."; Show("recherche"); }),
                        Act($"rs-hl-del-{i}", "✕", () => { Desk.RemoveHighlight(id); Show("recherche"); }) } }, 1) } } } } });
        }
        return new ScrollViewer { Content = sp, MaxHeight = 600 };
    }

    Control ResearchQuotes()
    {
        var all = Desk.Highlights(); var srcs = Desk.SessionSources();
        var sp = new StackPanel { Name = "rs-quotes", Spacing = 6 };
        var style = new ComboBox { Name = "rs-cite-style", ItemsSource = new[] { "einfach (DIN-ähnlich)", "APA" }, SelectedIndex = _rsStyle == "apa" ? 1 : 0, Width = 200 };
        style.SelectionChanged += (_, _) => { var s = style.SelectedIndex == 1 ? "apa" : "einfach"; if (s != _rsStyle) { _rsStyle = s; Show("recherche"); } };
        // untereinander statt nebeneinander: passt auch in die schmale rechte Spalte des Lese-Browsers
        sp.Children.Add(H($"Zitatsammlung · {all.Count} Markierungen aus {all.Select(h => h.Url).Distinct().Count()} Quellen", 14));
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Zitierstil", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, style } });
        foreach (var g in all.GroupBy(h => h.Url))
        {
            var block = new StackPanel { Spacing = 3, Children = { new TextBlock { Text = g.First().Title, Foreground = GoldText(), FontWeight = FontWeight.SemiBold, FontSize = 13 } } };
            foreach (var h in g) block.Children.Add(new Border { BorderBrush = Brush.Parse(MarkColor(h.Color)), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(8, 2), Child = new StackPanel { Children = {
                new TextBlock { Text = "„" + (h.Text.Length > 300 ? h.Text[..300] + " …" : h.Text) + "“", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = h.Note, Foreground = Muted, FontSize = 11.5, IsVisible = h.Note.Length > 0, TextWrapping = TextWrapping.Wrap } } } });
            sp.Children.Add(block);
        }
        var bib = Desk.Bibliography(_rsStyle);
        var list = new StackPanel { Name = "rs-bib", Spacing = 2 };
        foreach (var b in bib) list.Children.Add(new SelectableTextBlock { Text = "• " + b, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (bib.Count == 0) list.Children.Add(P("Noch keine Quellen gelesen.", Muted, 12));
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 6, 0, 0), Children = { H("Literaturliste", 13),
            Pos(Act("rs-bib-copy", "Literaturliste kopieren", () => { ResearchCopy(string.Join("\n", Desk.Bibliography(_rsStyle))); _rsMsg = "Literaturliste kopiert."; Show("recherche"); }), 1) } });
        sp.Children.Add(list);
        sp.Children.Add(P("Markierungen gehören zu dieser Sitzung und werden beim Beenden gelöscht – „Als Bericht speichern“ nimmt Zitate und Literaturliste mit.", Muted, 11));
        return WCard(sp);
    }
}
