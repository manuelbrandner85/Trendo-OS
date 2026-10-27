using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>Texte & Tabellen (SYS-10), Vorschläge Teil 4: F1 Startseite mit Programmkacheln und Vorlagen-Galerie,
/// U1 Dokumente als Kacheln mit Filter nach Art und Datum, U2 Öffnen im passenden Trendo-Programm.</summary>
public partial class MainWindow
{
    private string _dsType = "Alle";
    private int _dsDays;

    /// <summary>U2: Dokument im passenden Trendo-Programm öffnen; unbekannte Arten mit dem Standardprogramm.</summary>
    public void OpenOfficeDoc(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".docx" or ".odt" or ".txt" or ".md" or ".rtf": TextOpen(path); Show("text"); break;
            case ".xlsx" or ".ods" or ".csv": SheetOpen(path); Show("tabelle"); break;
            case ".pptx" or ".odp": DeckOpen(path); Show("praesentation"); break;
            case ".pdf": OpenPdf(path); Show("pdf"); break;
            default: Launch(path); break;
        }
    }

    private static (string Icon, string C1, string C2) KindLook(string kind) => kind switch
    {
        "Text" => (Mdi.FileDocument, "#9FD3FF", "#2F86F6"),
        "Tabelle" => (Mdi.ViewGrid, "#9CF5A8", "#22B35E"),
        "Präsentation" => (Mdi.Palette, "#FFD39A", "#F08A3C"),
        "PDF" => (Mdi.FileDocumentOutline, "#FFB3B3", "#E5484D"),
        _ => (Mdi.Folder, "#ECEFF1", "#90A4AE"),
    };

    private Control DokuStartView()
    {
        const string V = "doku-start";
        // Programme
        var apps = new UniformGrid { Name = "doku-apps", Columns = 5 };
        foreach (var (key, name, sub, kind) in new[] {
                     ("text", "Trendo Text", "Briefe, Berichte, Lebenslauf", "Text"),
                     ("tabelle", "Trendo Tabelle", "Rechnen, Listen, Diagramme", "Tabelle"),
                     ("praesentation", "Trendo Präsentation", "Folien und Vorträge", "Präsentation"),
                     ("pdf", "Trendo PDF", "Ansehen, ausfüllen, zusammenfügen", "PDF"),
                     ("brief", "Briefe schreiben", "DIN 5008, Fristen, Serienbrief", "Brief") })
        {
            if (!_showMap.ContainsKey(key)) continue;
            var (icon, c1, c2) = kind == "Brief" ? (Mdi.EmailEdit, "#C7A6FF", "#7C4DFF") : KindLook(kind);
            var chip = Chip(icon, c1, c2, 46, 0.56); chip.HorizontalAlignment = HorizontalAlignment.Left;
            var k = key;
            var b = new Button { Name = "doku-app-" + key, Height = 118, Margin = new Thickness(5), Padding = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 6, Children = { chip, new TextBlock { Text = name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = sub, Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            b.Click += (_, _) => { _w45Msg = ""; Show(k); };
            apps.Children.Add(b);
        }

        // Vorlagen-Galerie über alle Programme
        var tpl = new WrapPanel { Name = "doku-templates" };
        void Tpl(string id, string name, string hint, string kind, Action go)
        {
            var (icon, c1, c2) = KindLook(kind);
            var b = new Button { Name = "doku-tpl-" + id, Width = 200, Height = 86, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top,
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { Chip(icon, c1, c2, 28, 0.6),
                    Pos(new StackPanel { Margin = new Thickness(8, 0, 0, 0), Children = { new TextBlock { Text = name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = kind, Foreground = Muted, FontSize = 10 }, new TextBlock { Text = hint, Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 28 } } }, 1) } } };
            ToolTip.SetTip(b, hint);
            b.Click += (_, _) => go();
            tpl.Children.Add(b);
        }
        foreach (var t in TextTemplates.Builtin.Where(t => t.Id != "leer")) { var id = t.Id; Tpl("text-" + id, t.Name, t.Hint, "Text", () => { TextNew(id); Show("text"); }); }
        foreach (var t in SheetTemplates.Builtin.Where(t => t.Id != "leer")) { var id = t.Id; Tpl("tabelle-" + id, t.Name, t.Hint, "Tabelle", () => { SheetNew(id); Show("tabelle"); }); }
        foreach (var (id, name, hint) in new[] { ("vortrag", "Vortrag", "Titel, Ablauf, Kernpunkte, Fragen"), ("projekt", "Projektbericht", "Ziele, Stand, Zeitplan, Risiken"), ("fotoalbum", "Fotoalbum", "Bilder mit Bildunterschrift") })
        { var x = id; Tpl("praesentation-" + id, name, hint, "Präsentation", () => { DeckNew(x); Show("praesentation"); }); }

        // Zuletzt benutzte Dokumente als Kacheln mit Filter
        var filters = new WrapPanel { Name = "doku-filter" };
        Button F(string name, string label, bool on, Action go)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(14),
                Background = on ? Accent : Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { go(); Show(V); }; return b;
        }
        foreach (var t in new[] { "Alle", "Text", "Tabelle", "Präsentation", "PDF" }) filters.Children.Add(F("doku-type-" + t.ToLowerInvariant(), t, _dsType == t, () => _dsType = t));
        filters.Children.Add(new Border { Width = 14 });
        foreach (var (d, l) in new[] { (0, "Jederzeit"), (1, "Heute"), (7, "7 Tage"), (30, "30 Tage") }) filters.Children.Add(F("doku-age-" + d, l, _dsDays == d, () => _dsDays = d));

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var since = _dsDays == 0 ? DateTime.MinValue : DateTime.Today.AddDays(1 - _dsDays);
        var files = FileShelf.Recent(_opt.Isolated ? [ProjectsRoot] : [ProjectsRoot, docs], FileShelf.Office, 200)
            .Where(f => (_dsType == "Alle" || FileShelf.Kind(f.FullName) == _dsType) && f.LastWriteTime >= since).Take(40).ToList();
        var grid = new WrapPanel { Name = "doku-files" };
        var i = 0;
        foreach (var f in files)
        {
            var kind = FileShelf.Kind(f.FullName); var (icon, c1, c2) = KindLook(kind);
            var path = f.FullName;
            var b = new Button { Name = $"doku-file-{i++}", Width = 172, Height = 132, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(0), CornerRadius = new CornerRadius(10), Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = {
                    new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(c1), 0), new GradientStop(Color.Parse(c2), 1) } },
                        CornerRadius = new CornerRadius(10, 10, 0, 0), Opacity = 0.9, Child = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(icon), Fill = Brushes.White, Stretch = Stretch.Uniform, Width = 34, Height = 34, Opacity = 0.95 } },
                    Row1(new StackPanel { Margin = new Thickness(8, 5, 8, 6), Children = {
                        new TextBlock { Text = f.Name, Foreground = Text, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = $"{kind} · {f.LastWriteTime:dd.MM.yyyy HH:mm}", Foreground = Muted, FontSize = 10 } } }) } } };
            ToolTip.SetTip(b, path);
            b.Click += (_, _) => Try(V, () => OpenOfficeDoc(path));
            grid.Children.Add(b);
        }
        if (i == 0) grid.Children.Add(P(_dsType == "Alle" && _dsDays == 0 ? "Noch keine Dokumente – leg oben eins an." : "Keine Dokumente für diesen Filter.", Text));

        return DokuPage("Texte & Tabellen", new StackPanel { Spacing = 10, Children = {
            apps,
            H("Vorlagen", 15), tpl,
            H("Zuletzt benutzt", 15), filters, grid } });

        static Control Row1(Control c) { Grid.SetRow(c, 1); return c; }
    }
}
