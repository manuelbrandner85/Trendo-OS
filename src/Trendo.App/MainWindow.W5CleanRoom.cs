using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-10: Sauberer Nachbau.</summary>
public partial class MainWindow
{
    public string CleanFolder => WorkFolder("Sauberer Nachbau");
    private string? _cleanOpen;
    private IReadOnlyList<Similar>? _cleanSims;

    private void InitW5CleanRoom() => RegisterTab(BusinessTabs, "nachbau", "Sauberer Nachbau", "X-10", CleanView, "clean room", "nachbau", "nachbauen", "urheberrecht", "aehnlichkeit", "plagiat");

    public void CleanCheck(string original)
    {
        if (_cleanOpen is null || !Directory.Exists(original)) { Say("Wähle einen Original-Ordner, der existiert.", "nachbau"); return; }
        _cleanSims = CleanRoom.Compare(Path.Combine(_cleanOpen, "2-Umsetzung"), original);
        var rep = CleanRoom.Report(_cleanOpen, _cleanSims, original);
        CleanRoom.Log(_cleanOpen, "Prüfen", $"Ähnlichkeitsprüfung: {_cleanSims.Count} auffällige Dateien");
        Say(_cleanSims.Count == 0 ? "Keine auffällige Ähnlichkeit gefunden. Bericht gespeichert." : $"{_cleanSims.Count} Dateien sind dem Original sehr ähnlich. Bitte prüfen.", "nachbau");
    }

    /// <summary>Sauberer Nachbau als Projekt-Arbeitsfläche: links Projekte und Regeln, Mitte die drei Schritte (Beschreibung → Umsetzung → Prüfung)
    /// mit Beschreibung direkt bearbeitbar, rechts das Tagebuch mit Rollen und Zeit. Keine Rechtsberatung.</summary>
    private Control CleanView()
    {
        var left = new StackPanel { Spacing = 8 };
        var name = new TextBox { Name = "clean-name", PlaceholderText = "Name deines Projekts" }; var orig = new TextBox { Name = "clean-original", PlaceholderText = "Vorbild, z. B. ein bekannter Notizblock" };
        left.Children.Add(H("Neues Projekt", 13));
        left.Children.Add(name); left.Children.Add(orig);
        left.Children.Add(Act("clean-create", "＋ Anlegen", () =>
        {
            try { _cleanOpen = CleanRoom.Create(CleanFolder, string.IsNullOrWhiteSpace(name.Text) ? "Nachbau" : name.Text, orig.Text ?? ""); _cleanSims = null; Say("Angelegt. Schritt 1: Beschreibung schreiben.", "nachbau"); }
            catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "nachbau"); }
        }, true));
        var dirs = Directory.GetDirectories(CleanFolder).OrderByDescending(Directory.GetLastWriteTime).ToList();
        _cleanOpen ??= dirs.FirstOrDefault();
        left.Children.Add(H($"Projekte ({dirs.Count})", 13));
        var n = 0;
        foreach (var d in dirs.Take(20))
        {
            var dir = d; var on = dir == _cleanOpen;
            var b = new Button { Name = $"clean-open-{n++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Avalonia.Thickness(8, 5), Background = on ? Brush.Parse("#26D4A84B") : Brushes.Transparent, BorderThickness = new Avalonia.Thickness(0),
                Content = new StackPanel { Children = { new TextBlock { Text = Path.GetFileName(dir), Foreground = Text, FontSize = 13, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal }, new TextBlock { Text = Directory.GetLastWriteTime(dir).ToString("dd.MM.yyyy HH:mm"), Foreground = Muted, FontSize = 11 } } } };
            b.Click += (_, _) => { _cleanOpen = dir; _cleanSims = null; Show("nachbau"); }; Hover(b);
            left.Children.Add(b);
        }
        if (dirs.Count == 0) left.Children.Add(P("Noch keine – oben anlegen.", Muted, 11.5));
        var rules = new StackPanel { Spacing = 2 };
        foreach (var r in CleanRoom.Rules) rules.Children.Add(P("• " + r, null, 11));
        left.Children.Add(new Expander { Name = "clean-rules", Header = new TextBlock { Text = "Die Regeln", Foreground = Text, FontSize = 12.5 }, Content = rules, HorizontalAlignment = HorizontalAlignment.Stretch });

        Control center; Control right;
        if (_cleanOpen is { } open && Directory.Exists(open))
        {
            var specFile = Path.Combine(open, "1-Beschreibung", "BESCHREIBUNG.md");
            var spec = new TextBox { Name = "clean-spec-text", Text = File.Exists(specFile) ? File.ReadAllText(specFile) : "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 220, FontSize = 13 };
            var specSave = Act("clean-spec-save", "Beschreibung speichern", () => { Directory.CreateDirectory(Path.GetDirectoryName(specFile)!); File.WriteAllText(specFile, spec.Text ?? ""); CleanRoom.Log(open, CleanRoom.Roles[0], "Beschreibung geändert"); Say("Beschreibung gespeichert.", "nachbau"); }, true);
            var impl = Path.Combine(open, "2-Umsetzung");
            var implFiles = Directory.Exists(impl) ? Directory.GetFiles(impl, "*", SearchOption.AllDirectories).Length : 0;
            Border Step(string nr, string title, string who, bool done, Control body) => new() { Background = TileBg, BorderBrush = done ? Brush.Parse("#664ADE80") : EdgeLit, BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(14), Padding = new Avalonia.Thickness(14, 10),
                Child = new StackPanel { Spacing = 6, Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                    new Border { Width = 28, Height = 28, CornerRadius = new Avalonia.CornerRadius(14), Background = done ? Brush.Parse("#4ADE80") : Accent, Child = new TextBlock { Text = done ? "✓" : nr, Foreground = Brushes.White, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                    new TextBlock { Text = title, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "· " + who, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } }, body } } };
            var theirs = new TextBox { Name = "clean-theirs", PlaceholderText = "Ordner mit dem Original-Code (nur zum Vergleich)" };
            var check = new StackPanel { Spacing = 6, Children = { P("Vergleicht deinen Code in „2-Umsetzung“ mit dem Original. Umbenennen von Namen hilft dabei nicht.", null, 11.5),
                new DockPanel { Children = { Dk(Act("clean-check", "Prüfen", () => CleanCheck(theirs.Text?.Trim('"', ' ') ?? ""), true), Avalonia.Controls.Dock.Right), theirs } } } };
            if (_cleanSims is { } sims)
            {
                if (sims.Count == 0) check.Children.Add(P("✓ Keine auffällige Ähnlichkeit gefunden.", Brush.Parse("#4ADE80"), 12));
                foreach (var sm in sims.Take(10)) check.Children.Add(P($"⚠ {sm.Ours} ↔ {sm.Theirs}: {sm.Score:P0} gleich", Brush.Parse("#F97316"), 11.5));
            }
            center = new ScrollViewer { Content = new StackPanel { Spacing = 10, Children = {
                new DockPanel { Children = { Dk(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Act("clean-spec", "Beschreibung als Datei", () => Launch(specFile)), Act("clean-folder", "Ordner", () => Launch(open)) } }, Avalonia.Controls.Dock.Right),
                    new TextBlock { Text = Path.GetFileName(open), Foreground = Text, FontSize = 22, FontWeight = FontWeight.SemiBold } } },
                Step("1", "Beschreibung", "wer das Original kennt – schreibt nur WAS es tut, kein Code", spec.Text?.Trim().Length > 40, new StackPanel { Spacing = 6, Children = { spec, specSave } }),
                Step("2", "Umsetzung", "wer das Original NICHT gesehen hat – baut nur nach der Beschreibung", implFiles > 0, new StackPanel { Spacing = 4, Children = {
                    P(implFiles == 0 ? "Noch keine Dateien in „2-Umsetzung“. Dort entsteht der eigene Code." : $"{implFiles} Dateien in „2-Umsetzung“.", Text, 12.5), Act("clean-impl-folder", "Umsetzungs-Ordner öffnen", () => { Directory.CreateDirectory(impl); Launch(impl); }) } }),
                Step("3", "Prüfung", "Ähnlichkeit mit dem Original messen, vor jeder Veröffentlichung", _cleanSims is { Count: 0 }, check) } } };
            var role = new ComboBox { Name = "clean-role", ItemsSource = CleanRoom.Roles, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            var note = new TextBox { Name = "clean-note", PlaceholderText = "Was hast du gemacht?" };
            var journal = new StackPanel { Name = "clean-journal", Spacing = 6 };
            foreach (var e in CleanRoom.Journal(open).TakeLast(30).Reverse())
                journal.Children.Add(new Border { Background = Brush.Parse("#10FFFFFF"), CornerRadius = new Avalonia.CornerRadius(8), Padding = new Avalonia.Thickness(8, 5), Child = new StackPanel { Children = {
                    new TextBlock { Text = $"{e.At:dd.MM. HH:mm} · {e.Role}", Foreground = GoldText(), FontSize = 11 }, new TextBlock { Text = e.Text, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } });
            right = new ScrollViewer { Content = new StackPanel { Spacing = 8, Children = { H("Tagebuch", 14), role, note, Act("clean-log", "Ins Tagebuch", () => { CleanRoom.Log(open, CleanRoom.Roles[Math.Max(0, role.SelectedIndex)], note.Text ?? ""); Show("nachbau"); }), journal } } };
        }
        else
        {
            center = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "🧪", FontSize = 52, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Sauber nachbauen", Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                P("Baue eine Idee neu, ohne fremden Code zu kopieren: Beschreibung, Umsetzung und Prüfung in getrennten Rollen, mit Tagebuch. Links ein Projekt anlegen.", Muted, 12.5) } };
            right = P("Das Tagebuch erscheint, sobald ein Projekt offen ist.", Muted, 12);
        }
        var grid = new Grid { Name = "clean-workspace", ColumnDefinitions = new ColumnDefinitions("280,14,*,14,300"), Children = { Card(new ScrollViewer { Content = left }), Pos(center, 2), Pos(Card(right), 4) } };
        return BusinessPage("Sauberer Nachbau", grid);
    }
}
