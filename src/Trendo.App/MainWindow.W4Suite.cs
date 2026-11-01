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

    // ---------- Zeitplan (Gantt, Masterprompt 131) ----------
    double _planDayW = 28; int _planSel = -1; readonly Stack<(string File, string Text, string Label)> _planUndo = new();
    public GanttCanvas? GanttForTests { get; private set; }
    public string PlanUndoLabel => _planUndo.TryPeek(out var u) ? u.Label : "";
    /// <summary>Für Vorschaubilder: Vorhaben öffnen, Ansicht und gewählten Schritt setzen.</summary>
    public void SuiteOpenForPreview(string dir, string mode, int sel = -1) { _suiteOpen = dir; SuiteMode = mode; _planSel = sel; }

    void PlanChange(string open, string label, Func<int> act)
    {
        const string V = "vorhaben";
        var f = Path.Combine(open, CreationSuite.ListFile); var before = File.ReadAllText(f);
        try { var moved = act(); _planUndo.Push((f, before, label)); if (moved > 0) Say($"{moved} abhängige Schritte sind nachgerückt.", V); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException) { Say(ex.Message, V); }
        Show(V);
    }

    /// <summary>F1 + Masterprompt 131: Zeitplan als Gantt – Balken ziehen, Kanten ziehen, Abhängigkeiten, kritischer Pfad, Wochenenden, heute.</summary>
    private Control SuiteTimeline(string open, IReadOnlyList<Step> steps)
    {
        const string V = "vorhaben";
        var sp = new StackPanel { Name = "suite-timeline", Spacing = 8 };
        var bars = CreationSuite.Bars(steps);
        if (bars.Count == 0) sp.Children.Add(P("Noch keine Termine. Trag unten bei einem Schritt ein Datum ein – oder lege das Vorhaben mit Startdatum an.", Text));
        else
        {
            var from = bars.Min(b => b.Start).AddDays(-2); if (DateTime.Today < from && (from - DateTime.Today).TotalDays < 14) from = DateTime.Today.AddDays(-1);
            var to = new[] { bars.Max(b => b.End), DateTime.Today }.Max().AddDays(8);
            var canvas = new GanttCanvas { Name = "suite-gantt", Bars = bars, From = from, DaysShown = Math.Max(21, (to - from).Days), DayW = _planDayW, Selected = _planSel < bars.Count ? _planSel : -1 };
            GanttForTests = canvas;
            canvas.Moved = (no, d) => PlanChange(open, "Verschieben", () => CreationSuite.MoveBy(open, no, d));
            canvas.Resized = (no, d, atStart) => PlanChange(open, atStart ? "Beginn ändern" : "Dauer ändern", () => CreationSuite.ResizeBy(open, no, d, atStart));
            canvas.Linked = (fromNo, toNo) => PlanChange(open, "Abhängigkeit", () => { CreationSuite.AddAfter(open, toNo, fromNo); return CreationSuite.Reschedule(open); });
            canvas.Clicked = row => { _planSel = row; Show(V); };
            var crit = bars.Count(b => b.Critical); var end = bars.Max(b => b.End);
            var undo = Act("suite-plan-undo", _planUndo.TryPeek(out var u) ? "↶ " + u.Label : "↶ Rückgängig", () => { if (_planUndo.TryPop(out var x)) { File.WriteAllText(x.File, x.Text); Show(V); } });
            undo.IsEnabled = _planUndo.Count > 0;
            sp.Children.Add(new WrapPanel { Children = {
                new TextBlock { Name = "suite-plan-info", Text = $"Fertig am {end:ddd dd.MM.yyyy} · {(end - bars.Min(b => b.Start)).Days + 1} Tage · {crit} Schritte auf dem kritischen Pfad (rot)", Foreground = GoldText(), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) },
                Act("suite-zoom-days", "Tage", () => { _planDayW = 28; Show(V); }, _planDayW >= 20), Act("suite-zoom-weeks", "Wochen", () => { _planDayW = 12; Show(V); }, _planDayW < 20), new Border { Width = 8 }, undo } });
            var labels = new StackPanel { Width = 230, Children = { new Border { Height = GanttCanvas.Head } } };
            for (var r = 0; r < bars.Count; r++)
            {
                var b = bars[r]; var row = r;
                var lb = new Button { Name = $"suite-plan-row-{r}", Height = GanttCanvas.RowH, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = row == _planSel ? Brush.Parse("#332F86F6") : Brushes.Transparent, Padding = new Thickness(6, 0), CornerRadius = new CornerRadius(4),
                    Content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                        new TextBlock { Text = $"{b.No}. " + (b.Step.Milestone ? "◆ " : "") + b.Step.Text, Foreground = b.Critical ? Brush.Parse("#FCA5A5") : Text, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = $"{b.Start:dd.MM.}–{b.End:dd.MM.} · {b.Days} T" + (b.Slack > 0 ? $" · {b.Slack} T Puffer" : ""), Foreground = Muted, FontSize = 10 } } } };
                lb.Click += (_, _) => { _planSel = row; Show(V); };
                labels.Children.Add(lb);
            }
            var scroll = new ScrollViewer { Content = canvas, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1), Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { labels, Pos(scroll, 1) } } });
            sp.Children.Add(P("Balken ziehen: verschieben · Kante ziehen: Beginn/Ende · gelben Punkt am Balkenende auf einen anderen Balken ziehen: „kommt danach“ · Nachfolger rücken automatisch nach", Muted, 11));
            if (_planSel >= 0 && _planSel < bars.Count)
            {
                var b = bars[_planSel];
                var st = new TextBox { Name = "suite-plan-start", Text = b.Start.ToString("dd.MM.yyyy"), Width = 110 };
                var en = new TextBox { Name = "suite-plan-end", Text = b.End.ToString("dd.MM.yyyy"), Width = 110 };
                var deps = new WrapPanel();
                for (var k = 0; k < steps.Count; k++)
                {
                    if (k + 1 == b.No) continue; var no = k + 1;
                    deps.Children.Add(new CheckBox { Name = $"suite-plan-after-{no}", IsChecked = (b.Step.After ?? []).Contains(no), Content = new TextBlock { Text = $"{no}. {steps[k].Text}", Foreground = Text, FontSize = 11.5 }, Margin = new Thickness(0, 0, 10, 0) });
                }
                var apply = Act("suite-plan-apply", "Übernehmen", () => PlanChange(open, "Schritt ändern", () =>
                {
                    DateTime Date(TextBox t) => DateTime.TryParseExact((t.Text ?? "").Trim(), "d.M.yyyy", De, System.Globalization.DateTimeStyles.None, out var x) ? x : throw new ArgumentException("Datum bitte als TT.MM.JJJJ.");
                    CreationSuite.SetSpan(open, b.Step.Line, Date(st), Date(en));
                    CreationSuite.SetAfter(open, b.No, deps.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => int.Parse(c.Name!["suite-plan-after-".Length..])));
                    return CreationSuite.Reschedule(open);
                }), true);
                sp.Children.Add(Card(new StackPanel { Name = "suite-plan-step", Spacing = 6, Children = { H($"{b.No}. {b.Step.Text}", 14),
                    new WrapPanel { Children = { new TextBlock { Text = "Beginn", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, st, new TextBlock { Text = "Ende", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) }, en } },
                    new TextBlock { Text = "Kommt nach:", Foreground = Muted, FontSize = 12 }, deps, apply } }));
            }
        }
        // Ende oder Meilenstein für einen Schritt setzen (auch für Schritte ohne Termin)
        var pick = new ComboBox { Name = "suite-due-step", ItemsSource = steps.Select(s => s.Text).ToList(), SelectedIndex = steps.Count > 0 ? 0 : -1, MinWidth = 220 };
        var date = new TextBox { Name = "suite-due-date", PlaceholderText = "TT.MM.JJJJ", Width = 110 };
        var ms = new CheckBox { Name = "suite-due-milestone", Content = "Meilenstein" };
        var undated = steps.Where(s => s.Due is null).ToList();
        if (undated.Count > 0) sp.Children.Add(P("Ohne Termin: " + string.Join(" · ", undated.Select(s => s.Text)), Muted, 11.5));
        sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { new TextBlock { Text = "Ende/Meilenstein setzen:", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, pick, new Border { Width = 6 }, date, new Border { Width = 6 }, ms, new Border { Width = 6 },
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
            var f = Clean(src); if (!File.Exists(f)) throw new ArgumentException("Diese Datei gibt es nicht.");
            File.Copy(f, Path.Combine(open, Path.GetFileName(f)), false); Say("Hinzugefügt: " + Path.GetFileName(f), V);
        })) } });
        return sp;
    }
}
