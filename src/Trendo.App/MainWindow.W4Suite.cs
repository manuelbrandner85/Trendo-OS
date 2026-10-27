using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>X-11: Vorhaben – Ziel wählen, Trendo richtet alles ein. Teil 4: F1 Zeitplan mit Meilensteinen, F2 alle Dateien des Vorhabens,
/// U1 Kanban (Offen/In Arbeit/Fertig) neben der Liste, U2 Fortschrittsring je Vorhaben.</summary>
public partial class MainWindow
{
    public string SuiteFolder => WorkFolder("Vorhaben");
    private string? _suiteOpen;
    public string SuiteMode { get; set; } = "liste";

    private void InitW4Suite() => RegisterTab(KreativTabs, "vorhaben", "Vorhaben", "X-11", SuiteView, "vorhaben", "plan", "checkliste", "schritte", "ziel", "kanban", "zeitplan", "meilenstein", "projekt");

    /// <summary>U2: Fortschritt als Ring mit Prozentzahl.</summary>
    private Control Ring(int percent, double size, string name)
    {
        var bg = new Ellipse { Width = size, Height = size, Stroke = Edge, StrokeThickness = size / 10 };
        var arc = new Arc { Name = name, Width = size, Height = size, StrokeThickness = size / 10, StartAngle = -90, SweepAngle = 3.6 * percent, StrokeLineCap = PenLineCap.Round,
            Stroke = Brush.Parse(percent >= 100 ? "#22B35E" : "#2F86F6") };
        return new Grid { Width = size, Height = size, Children = { bg, arc, new TextBlock { Text = $"{percent} %", Foreground = Text, FontSize = size / 4.2, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
    }

    private Control SuiteView()
    {
        const string V = "vorhaben";
        var sp = new StackPanel { Spacing = 8, Children = { P("Was willst du machen? Wähle ein Ziel. Trendo legt Ordner, Schritte und einen Zeitplan an.") } };
        var name = new TextBox { Name = "suite-name", PlaceholderText = "Name, z. B. Urlaubsvideo", Width = 240 };
        var start = new TextBox { Name = "suite-start", Text = DateTime.Today.ToString("dd.MM.yyyy", De), Width = 110 };
        ToolTip.SetTip(start, "Startdatum für den Zeitplan");
        var wrap = new WrapPanel();
        foreach (var k in CreationSuite.Kits)
        {
            var kit = k;
            var b = new Button { Name = "suite-kit-" + k.Id, Margin = new Thickness(0, 0, 8, 8), Width = 200, Background = TileBg, Content = new StackPanel { Children = {
                new TextBlock { Text = k.Label, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold }, P(k.Goal, null, 10.5), P($"{k.Steps.Length} Schritte · etwa {CreationSuite.Days(k.Id)} Tage", null, 10) } } };
            b.Click += (_, _) =>
            {
                try
                {
                    var s = DateTime.TryParseExact(start.Text?.Trim(), "d.M.yyyy", De, System.Globalization.DateTimeStyles.None, out var d) ? d : DateTime.Today;
                    _suiteOpen = CreationSuite.Create(SuiteFolder, kit.Id, string.IsNullOrWhiteSpace(name.Text) ? kit.Label : name.Text, s);
                    _core.Audit.Append(Principal.LocalOwner.Name, "suite.create", kit.Id); Say($"„{Path.GetFileName(_suiteOpen)}“ ist eingerichtet. Los geht's mit Schritt 1.", V);
                }
                catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, V); }
            };
            wrap.Children.Add(b);
        }
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { new WrapPanel { Children = { name, new Border { Width = 8 }, P("Start", null, 11.5), new Border { Width = 4 }, start } }, wrap } }));

        // U2: Vorhaben als Karten mit Ring
        var dirs = Directory.GetDirectories(SuiteFolder).OrderByDescending(Directory.GetLastWriteTime).ToList();
        _suiteOpen ??= dirs.FirstOrDefault();
        if (dirs.Count > 0)
        {
            sp.Children.Add(H("Deine Vorhaben", 14));
            var cards = new WrapPanel { Name = "suite-cards" };
            var i = 0;
            foreach (var d in dirs.Take(12))
            {
                var dir = d; var steps = CreationSuite.Steps(dir); var pct = CreationSuite.Percent(dir);
                var next = steps.FirstOrDefault(s => !s.Done);
                var b = new Button { Name = $"suite-open-{i}", Width = 250, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10), CornerRadius = new CornerRadius(12),
                    Background = dir == _suiteOpen ? Accent : TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { Ring(pct, 52, $"suite-ring-{i}"),
                        Pos(new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                            new TextBlock { Text = Path.GetFileName(dir), Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = next is null ? "Alles erledigt" : "Als Nächstes: " + next.Text, Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = steps.LastOrDefault(s => s.Milestone)?.Due is { } due ? $"Ziel: {due:dd.MM.yyyy}" : $"{steps.Count(s => s.Done)} von {steps.Count} Schritten", Foreground = Muted, FontSize = 10.5 } } }, 1) } } };
                b.Click += (_, _) => { _suiteOpen = dir; Show(V); };
                cards.Children.Add(b); i++;
            }
            sp.Children.Add(cards);
        }

        if (_suiteOpen is { } open && Directory.Exists(open))
        {
            var steps = CreationSuite.Steps(open);
            var modes = new WrapPanel { Name = "suite-modes" };
            foreach (var (m, l) in new[] { ("liste", "Liste"), ("kanban", "Kanban"), ("zeitplan", "Zeitplan"), ("dateien", "Dateien") })
            {
                var b = new Button { Name = "suite-mode-" + m, Content = l, FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(14),
                    Background = SuiteMode == m ? Accent : Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
                b.Click += (_, _) => { SuiteMode = m; Show(V); };
                modes.Children.Add(b);
            }
            var head = new StackPanel { Spacing = 6, Children = {
                Row(H(Path.GetFileName(open), 15), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { modes, Act("suite-folder", "Ordner", () => Launch(open)) } }),
                new ProgressBar { Name = "suite-progress", Value = CreationSuite.Percent(open), Maximum = 100, Height = 6 } } };
            Control body = SuiteMode switch { "kanban" => SuiteKanban(open, steps), "zeitplan" => SuiteTimeline(open, steps), "dateien" => SuiteFiles(open), _ => SuiteList(open, steps) };
            var jump = new WrapPanel();
            foreach (var (k, l) in new[] { ("kreativ-medien", "Medien-Studio"), ("baukasten", "Baukasten"), ("dev", "Software"), ("spiele", "Spiele"), ("3d", "3D"), ("assets", "Freie Assets") })
                if (_showMap.ContainsKey(k)) { var b = Nav2(l, k); b.Margin = new Thickness(0, 4, 6, 0); jump.Children.Add(b); }
            sp.Children.Add(Card(new StackPanel { Spacing = 8, Children = { head, body, jump } }));
        }
        return KreativPage("Vorhaben", sp);
    }

    private Control SuiteList(string open, IReadOnlyList<Step> steps)
    {
        const string V = "vorhaben";
        var list = new StackPanel { Spacing = 3 };
        var n = 0;
        foreach (var s in steps)
        {
            var st = s;
            var cb = new CheckBox { Name = $"suite-step-{n++}", IsChecked = s.Done, Content = new TextBlock { Text = (s.Milestone ? "◆ " : "") + s.Text + (s.State == 1 ? "  (in Arbeit)" : ""), Foreground = Text, TextWrapping = TextWrapping.Wrap } };
            cb.IsCheckedChanged += (_, _) => { CreationSuite.Toggle(open, st.Line); Show(V); };
            list.Children.Add(Row(cb, P(s.Due is { } d ? d.ToString("ddd dd.MM.", De) + (d < DateTime.Today && !s.Done ? " · überfällig" : "") : "", s.Due < DateTime.Today && !s.Done ? Brush.Parse("#F97316") : null, 11)));
        }
        var add = new TextBox { Name = "suite-add-text", PlaceholderText = "Eigener Schritt …", Width = 260 };
        list.Children.Add(new WrapPanel { Children = { add, Act("suite-add", "Hinzufügen", () => { CreationSuite.AddStep(open, add.Text ?? ""); Show(V); }) } });
        return list;
    }

    /// <summary>U1: Kanban mit drei Spalten; Karten mit ← / → verschieben.</summary>
    private Control SuiteKanban(string open, IReadOnlyList<Step> steps)
    {
        const string V = "vorhaben";
        var grid = new Grid { Name = "suite-kanban", ColumnDefinitions = new ColumnDefinitions("*,8,*,8,*") };
        string[] titles = ["Offen", "In Arbeit", "Fertig"];
        for (var col = 0; col < 3; col++)
        {
            var c = col;
            var stack = new StackPanel { Name = $"suite-col-{col}", Spacing = 6, Children = { new TextBlock { Text = $"{titles[col]} ({steps.Count(s => s.State == c)})", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold } } };
            foreach (var s in steps.Where(s => s.State == c))
            {
                var st = s;
                var moves = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
                if (c > 0) { var l = new Button { Name = $"suite-left-{s.Line}", Content = "←", Padding = new Thickness(8, 1) }; ToolTip.SetTip(l, titles[c - 1]); l.Click += (_, _) => { CreationSuite.SetState(open, st.Line, c - 1); Show(V); }; moves.Children.Add(l); }
                if (c < 2) { var r = new Button { Name = $"suite-right-{s.Line}", Content = "→", Padding = new Thickness(8, 1) }; ToolTip.SetTip(r, titles[c + 1]); r.Click += (_, _) => { CreationSuite.SetState(open, st.Line, c + 1); Show(V); }; moves.Children.Add(r); }
                stack.Children.Add(new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6),
                    Child = new StackPanel { Spacing = 4, Children = {
                        new TextBlock { Text = (s.Milestone ? "◆ " : "") + s.Text, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap },
                        Row(P(s.Due is { } d ? d.ToString("dd.MM.", De) : "", s.Due < DateTime.Today && c < 2 ? Brush.Parse("#F97316") : null, 10.5), moves) } } });
            }
            Grid.SetColumn(stack, col * 2);
            grid.Children.Add(new Border { Background = Brush.Parse(C("#22000000", "#0F000000")), CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Child = stack });
            Grid.SetColumn(grid.Children[^1], col * 2);
        }
        return grid;
    }

    /// <summary>F1: Zeitplan als einfache Balken (Gantt light) mit Meilensteinen und heutiger Linie.</summary>
    private Control SuiteTimeline(string open, IReadOnlyList<Step> steps)
    {
        const string V = "vorhaben";
        var dated = steps.Where(s => s.Due is not null).ToList();
        var sp = new StackPanel { Name = "suite-timeline", Spacing = 4 };
        if (dated.Count == 0) sp.Children.Add(P("Noch keine Termine. Trag bei einem Schritt ein Datum ein:", Text));
        else
        {
            var first = Directory.GetCreationTime(open).Date; var from = new[] { first, dated.Min(s => s.Due!.Value) }.Min(); var to = dated.Max(s => s.Due!.Value).AddDays(1);
            var span = Math.Max(1, (to - from).TotalDays); const double W = 520;
            var prev = from;
            foreach (var s in steps)
            {
                var bar = new Canvas { Width = W, Height = 18 };
                if (s.Due is { } due)
                {
                    var x0 = (prev - from).TotalDays / span * W; var x1 = (due.AddDays(1) - from).TotalDays / span * W;
                    var color = s.Done ? "#22B35E" : s.State == 1 ? "#F08A3C" : due < DateTime.Today ? "#E5484D" : "#2F86F6";
                    if (s.Milestone)
                    {
                        var diamond = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M7,0 L14,7 L7,14 L0,7 Z"), Fill = Brush.Parse(color) };
                        Canvas.SetLeft(diamond, x1 - 7); Canvas.SetTop(diamond, 2); bar.Children.Add(diamond);
                    }
                    else { var r = new Rectangle { Width = Math.Max(4, x1 - x0), Height = 12, RadiusX = 4, RadiusY = 4, Fill = Brush.Parse(color) }; Canvas.SetLeft(r, x0); Canvas.SetTop(r, 3); bar.Children.Add(r); }
                    prev = due.AddDays(1) > prev ? due.AddDays(1) : prev;
                }
                var today = (DateTime.Today - from).TotalDays / span * W;
                if (today is >= 0 and <= W) { var l = new Line { StartPoint = new Point(today, 0), EndPoint = new Point(today, 18), Stroke = Brush.Parse("#E5484D"), StrokeThickness = 1 }; bar.Children.Add(l); }
                sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("230,90,*"), Children = {
                    new TextBlock { Text = (s.Milestone ? "◆ " : "") + s.Text, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center },
                    Pos(P(s.Due is { } d ? d.ToString("ddd dd.MM.", De) : "–", null, 11), 1), Pos(bar, 2) } });
            }
            sp.Children.Add(P($"{from:dd.MM.yyyy} bis {to.AddDays(-1):dd.MM.yyyy} · rote Linie = heute · ◆ = Meilenstein", null, 10.5));
        }
        // Datum oder Meilenstein für einen Schritt setzen
        var pick = new ComboBox { Name = "suite-due-step", ItemsSource = steps.Select(s => s.Text).ToList(), SelectedIndex = steps.Count > 0 ? 0 : -1, MinWidth = 220 };
        var date = new TextBox { Name = "suite-due-date", PlaceholderText = "TT.MM.JJJJ", Width = 110 };
        var ms = new CheckBox { Name = "suite-due-milestone", Content = "Meilenstein" };
        sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { pick, new Border { Width = 6 }, date, new Border { Width = 6 }, ms, new Border { Width = 6 },
            Act("suite-due-set", "Übernehmen", () => Try(V, () =>
            {
                if (pick.SelectedIndex < 0) return;
                var st = steps[pick.SelectedIndex];
                DateTime? d = string.IsNullOrWhiteSpace(date.Text) ? null : DateTime.TryParseExact(date.Text.Trim(), "d.M.yyyy", De, System.Globalization.DateTimeStyles.None, out var x) ? x : throw new ArgumentException("Datum bitte als TT.MM.JJJJ.");
                CreationSuite.SetDue(open, st.Line, d); CreationSuite.SetMilestone(open, st.Line, ms.IsChecked == true); Show(V);
            }), true) } });
        return sp;
    }

    /// <summary>F2: alle Dateien des Vorhabens an einem Ort; Office-Dateien öffnen in Trendos Programmen.</summary>
    private Control SuiteFiles(string open)
    {
        const string V = "vorhaben";
        var files = CreationSuite.Files(open);
        var sp = new StackPanel { Name = "suite-files", Spacing = 3 };
        if (files.Count == 0) sp.Children.Add(P("Noch keine Dateien. Alles, was du in den Studios für dieses Vorhaben speicherst, erscheint hier.", Text));
        var i = 0;
        foreach (var f in files)
        {
            var path = f.FullName;
            var b = new Button { Name = $"suite-file-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(6, 3),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,110,120"), Children = {
                    new TextBlock { Text = Path.GetRelativePath(open, path), Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                    Pos(P(FileShelf.Kind(path), null, 11), 1), Pos(P(f.LastWriteTime.ToString("dd.MM.yyyy HH:mm", De), null, 11), 2) } } };
            b.Click += (_, _) => Try(V, () => OpenOfficeDoc(path));
            sp.Children.Add(b);
        }
        var src = new TextBox { Name = "suite-file-add", PlaceholderText = "Datei zum Vorhaben hinzufügen (wird kopiert)", Width = 320 };
        sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { src, new Border { Width = 6 }, Act("suite-file-copy", "Hinzufügen", () => Try(V, () =>
        {
            var f = src.Text?.Trim().Trim('"') ?? ""; if (!File.Exists(f)) throw new ArgumentException("Diese Datei gibt es nicht.");
            File.Copy(f, Path.Combine(open, Path.GetFileName(f)), false); Say("Hinzugefügt: " + Path.GetFileName(f), V);
        })) } });
        return sp;
    }
}
