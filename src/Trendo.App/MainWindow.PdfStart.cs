using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo PDF – Startseite wie in einem Profi-PDF-Programm (Clean-Room): links „Zuletzt geöffnet“ und PDFs auf diesem PC,
/// Mitte großer Öffnen-Bereich, rechts die Werkzeuge im Überblick (was nach dem Öffnen geht).</summary>
public partial class MainWindow
{
    Control PdfStartView()
    {
        async void Open(string f)
        {
            try { OpenPdf(f); }
            catch (Exception e) when (e is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException) { PdfPath = null; PdfPages = 0; PdfSay($"„{Path.GetFileName(f)}“ ist keine lesbare PDF-Datei."); }
            await Task.CompletedTask; Show("pdf");
        }
        Control FileRow(string name, FileInfo f)
        {
            var b = new Button { Name = name, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5), Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(8),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,8,*"), Children = {
                    new Border { Width = 30, Height = 38, CornerRadius = new CornerRadius(4), Background = Brush.Parse("#33EF4444"), Child = new TextBlock { Text = "PDF", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#FCA5A5"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                    Pos(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = f.Name, Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = $"{f.LastWriteTime:dd.MM.yyyy HH:mm} · {f.Length / 1024.0:0} KB", Foreground = Muted, FontSize = 11 } } }, 2) } } };
            ToolTip.SetTip(b, f.FullName); var path = f.FullName; b.Click += (_, _) => Open(path); Hover(b);
            return b;
        }
        var recent = PdfRecent().Select(p => new FileInfo(p)).ToList();
        var found = FileShelf.Recent([UserFolder(Environment.SpecialFolder.MyDocuments), Path.Combine(UserFolder(Environment.SpecialFolder.UserProfile), "Downloads"), ProjectsRoot], [".pdf"], 12)
            .Where(f => !recent.Any(r => string.Equals(r.FullName, f.FullName, StringComparison.OrdinalIgnoreCase))).ToList();
        var left = new StackPanel { Name = "pdf-start-files", Spacing = 2 };
        left.Children.Add(H("Zuletzt geöffnet", 14));
        if (recent.Count == 0) left.Children.Add(P("Noch keine – geöffnete PDFs erscheinen hier.", Muted, 11.5));
        for (var i = 0; i < recent.Count; i++) left.Children.Add(FileRow($"pdf-recent-{i}", recent[i]));
        left.Children.Add(new Border { Height = 10 });
        left.Children.Add(H("PDFs auf diesem PC", 14));
        if (found.Count == 0) left.Children.Add(P("Keine in Dokumente, Downloads oder Projekte gefunden.", Muted, 11.5));
        for (var i = 0; i < found.Count; i++) left.Children.Add(FileRow($"pdf-found-{i}", found[i]));

        var open = new Button { Name = "pdf-start-open", Content = "PDF öffnen …", Background = Accent, Foreground = Brushes.White, FontSize = 15, Padding = new Thickness(22, 9), HorizontalAlignment = HorizontalAlignment.Center };
        open.Click += async (_, _) => { if (await PickPdf("pdf-open", false) is [var f, ..]) Open(f); };
        var center = new Border { Name = "pdf-start-drop", CornerRadius = new CornerRadius(16), BorderBrush = Brush.Parse("#55D4A84B"), BorderThickness = new Thickness(2), Background = Brush.Parse("#0FD4A84B"), Padding = new Thickness(30), MinHeight = 340,
            Child = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "📄", FontSize = 56, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "PDF lesen, suchen, markieren, ordnen", Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                P("Ohne Zusatzprogramm – alles bleibt auf deinem PC.", Muted, 12.5), open } } };

        var tools = new StackPanel { Name = "pdf-start-tools", Spacing = 6, Children = { H("Werkzeuge", 14) } };
        foreach (var (icon, name, what) in new[] { ("🔍", "Suchen", "Text in allen Seiten finden"), ("🖍", "Markieren", "Stellen hervorheben, Notizen anheften"), ("🗂", "Seiten ordnen", "drehen, löschen, verschieben, leere Seite einfügen"),
            ("📋", "Text kopieren", "den Text einer Seite übernehmen"), ("🔒", "Geschützte PDFs", "mit Passwort öffnen"), ("➕", "Neue leere PDF", "zum Ausfüllen und Ergänzen") })
            tools.Children.Add(new Border { Background = Brush.Parse("#10FFFFFF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 7), Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                new TextBlock { Text = icon, FontSize = 18, VerticalAlignment = VerticalAlignment.Center }, new StackPanel { MaxWidth = 205, Children = { new TextBlock { Text = name, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = what, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } } } } });
        return new Grid { Name = "pdf-start", ColumnDefinitions = new ColumnDefinitions("300,14,*,14,280"), Margin = new Thickness(0, 6, 0, 0), Children = {
            Card(new ScrollViewer { Content = left }), Pos(center, 2), Pos(Card(tools), 4) } };
    }
}
