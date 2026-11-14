using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Projekte als echte Projektmappe (Umbau 04.11.2026, vorher nur ein Eingabefeld): links die Projekte mit Suche, in der Mitte die Dateien
/// des Projekts (hinzufügen, Ordner anlegen), rechts Details, Notiz und Aktionen. Alles innerhalb der Projektgrenze (ProjectStore prüft jeden Pfad).</summary>
public partial class MainWindow
{
    string? _projSel; string _projQuery = "", _projMsg = "";
    public string? ProjectSelected { get => _projSel; set => _projSel = value; }
    public string ProjectMessage => _projMsg;

    static string ProjIcon(ProjectStore.ProjectFile f) => f.IsDir ? "📁" : Path.GetExtension(f.Name).ToLowerInvariant() switch
    {
        ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" => "🖼", ".mp4" or ".mov" or ".mkv" or ".webm" => "🎬", ".mp3" or ".wav" or ".flac" => "🎵",
        ".pdf" => "📕", ".docx" or ".odt" or ".txt" or ".md" => "📄", ".xlsx" or ".ods" or ".csv" => "📊", ".json" => "{ }", ".stl" or ".obj" or ".glb" => "🧊", _ => "📎",
    };

    /// <summary>Datei ins gewählte Projekt holen (öffentlich für Tests und die KI).</summary>
    public string ProjectAddFile(string file)
    {
        var id = _projSel ?? throw new InvalidOperationException("Erst links ein Projekt wählen.");
        var rel = _core.Projects.AddFile(id, file);
        _projMsg = $"„{Path.GetFileName(rel)}“ ist jetzt im Projekt."; return rel;
    }

    private Control ProjectsView()
    {
        const string V = "projects";
        var all = _core.Projects.List().OrderByDescending(p => p.CreatedAt).ToList();
        if (_projSel is null || all.All(p => p.Id != _projSel)) _projSel = all.FirstOrDefault()?.Id;
        var gold = Brush.Parse("#F5B83D");

        // ── links: Projekte ──
        var left = new StackPanel { Name = "proj-list", Spacing = 4 };
        var id = new TextBox { Name = "txt-project-id", PlaceholderText = "Name, z. B. Urlaub 2026", FontSize = 12.5 };
        var create = new Button { Name = "btn-create-project", Content = "＋ Projekt anlegen", Background = Accent, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        var msg = P(_projMsg); msg.Name = "txt-project-msg";
        create.Click += (_, _) =>
        {
            try
            {
                var t = (id.Text ?? "").Trim();
                var p = System.Text.RegularExpressions.Regex.IsMatch(t, "^[a-z0-9][a-z0-9-]{1,47}$") ? _core.Projects.Create(t, t) : _core.Projects.CreateNamed(t);
                _projSel = p.Id; _projMsg = $"Projekt „{p.Name}“ angelegt."; Show(V);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException) { msg.Text = ex.Message; }
        };
        left.Children.Add(id); left.Children.Add(create); left.Children.Add(msg);
        var search = new TextBox { Name = "proj-search", PlaceholderText = "Projekte suchen …", Text = _projQuery, FontSize = 12, Margin = new Thickness(0, 6, 0, 2) };
        search.KeyUp += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter || (search.Text ?? "").Length == 0) { _projQuery = search.Text ?? ""; Show(V); } };
        left.Children.Add(search);
        var shown = all.Where(p => _projQuery.Length == 0 || p.Name.Contains(_projQuery, StringComparison.CurrentCultureIgnoreCase) || p.Id.Contains(_projQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        if (all.Count == 0) left.Children.Add(P("Noch keine Projekte. Gib oben einen Namen ein.", Muted, 11.5));
        foreach (var p in shown)
        {
            var on = p.Id == _projSel; var pid = p.Id;
            var b = new Button { Name = "proj-item-" + p.Id, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5),
                Background = on ? Brush.Parse("#2A2410") : Brushes.Transparent, BorderBrush = on ? gold : Brushes.Transparent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Content = new StackPanel { Children = { new TextBlock { Text = "" + p.Name, Foreground = on ? gold : Text, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"angelegt {p.CreatedAt:dd.MM.yyyy}", Foreground = Muted, FontSize = 10.5 } } } };
            b.Click += (_, _) => { _projSel = pid; _projMsg = ""; Show(V); };
            left.Children.Add(b);
        }
        var url = new Button { Name = "btn-open-url", Content = "Trendo im Browser öffnen", IsEnabled = !_core.Offline, FontSize = 11, Margin = new Thickness(0, 12, 0, 0) }; ToolTip.SetTip(url, "Trendo-Repository im Browser öffnen (Freigabe nötig)");
        if (_core.Offline) ToolTip.SetTip(url, "Offline-Modus: deaktiviert");
        url.Click += (_, _) => { try { _core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://github.com/manuelbrandner85/Trendo-OS"); Show("approvals"); } catch (OfflineException ex) { msg.Text = ex.Message; } };
        left.Children.Add(url);

        // ── Mitte: Dateien ──
        var sel = all.FirstOrDefault(p => p.Id == _projSel);
        var center = new StackPanel { Name = "proj-files", Spacing = 2 };
        var right = new StackPanel { Name = "proj-details", Spacing = 8 };
        if (sel is null)
        {
            center.Children.Add(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 0), Spacing = 6, Children = {
                new TextBlock { Text = "📁", FontSize = 46, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Ein Projekt hält alles zu einer Sache zusammen – Dateien, Notizen, Ergebnisse aus den Studios.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, MaxWidth = 380, TextAlignment = TextAlignment.Center } } });
        }
        else
        {
            var pid = sel.Id;
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            var add = new Button { Name = "proj-add-file", Content = "＋ Dateien hinzufügen", FontSize = 12, Margin = new Thickness(0, 0, 6, 0) };
            add.Click += async (_, _) =>
            {
                string[]? got;
                if (PickOverride is { } fake) got = await fake(new PickRequest("proj-add", false, true, []));
                else got = (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Dateien ins Projekt holen", AllowMultiple = true })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
                if (got is null || got.Length == 0) return;
                Try(V, () => { foreach (var f in got) ProjectAddFile(f); _projMsg = got.Length == 1 ? _projMsg : $"{got.Length} Dateien sind jetzt im Projekt."; Show(V); });
            };
            var folderName = new TextBox { Name = "proj-newdir-name", PlaceholderText = "neuer Ordner", Width = 150, FontSize = 12 };
            var folder = new Button { Name = "proj-new-folder", Content = "Ordner anlegen", FontSize = 12 };
            folder.Click += (_, _) => Try(V, () => { _core.Projects.NewFolder(pid, (folderName.Text ?? "").Trim() is { Length: > 0 } n ? n : throw new InvalidOperationException("Bitte einen Ordnernamen eingeben.")); _projMsg = "Ordner angelegt."; Show(V); });
            bar.Children.Add(add); bar.Children.Add(folderName); bar.Children.Add(new Border { Width = 4 }); bar.Children.Add(folder);
            center.Children.Add(new TextBlock { Text = sel.Name, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 0, 0, 4) });
            center.Children.Add(bar);
            var files = _core.Projects.Files(pid);
            // Kopfzeile
            center.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,90,130"), Margin = new Thickness(6, 2), Children = {
                new TextBlock { Text = "Name", Foreground = Muted, FontSize = 11 }, Pos(new TextBlock { Text = "Größe", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right }, 1), Pos(new TextBlock { Text = "Geändert", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right }, 2) } });
            if (files.Count == 0) center.Children.Add(P("Noch leer – oben Dateien hinzufügen oder in einem Studio „im Projekt speichern“.", Muted, 12));
            var i = 0;
            foreach (var f in files.Take(400))
            {
                var row = new Grid { Name = $"proj-file-{i++}", ColumnDefinitions = new ColumnDefinitions("*,90,130"), Margin = new Thickness(6 + f.Depth * 16, 1, 6, 1) };
                row.Children.Add(new TextBlock { Text = $"{ProjIcon(f)}  {f.Name}", Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis });
                row.Children.Add(Pos(new TextBlock { Text = f.IsDir ? "" : FileSize(f.Bytes), Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right }, 1));
                row.Children.Add(Pos(new TextBlock { Text = f.Changed.ToString("dd.MM.yyyy HH:mm"), Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right }, 2));
                center.Children.Add(new Border { Child = row, CornerRadius = new CornerRadius(5), Padding = new Thickness(0, 2), Background = i % 2 == 0 ? Brush.Parse("#0DFFFFFF") : Brushes.Transparent });
            }

            // ── rechts: Details ──
            var info = _core.Projects.Info(pid);
            right.Children.Add(H("Details", 13));
            foreach (var (k, v) in new[] { ("Dateien", info.Files.ToString()), ("Größe", FileSize(Math.Max(1, info.Bytes))), ("Zuletzt geändert", info.LastChange?.ToString("dd.MM.yyyy HH:mm") ?? "–"), ("Angelegt", sel.CreatedAt.ToString("dd.MM.yyyy")), ("Arten", info.Kinds.Count == 0 ? "–" : string.Join(" ", info.Kinds)) })
                right.Children.Add(new DockPanel { Children = { Dk(new TextBlock { Text = v, Foreground = Text, FontSize = 12 }, Avalonia.Controls.Dock.Right), new TextBlock { Text = k, Foreground = Muted, FontSize = 12 } } });
            var note = new TextBox { Name = "proj-note", Text = _core.Projects.Note(pid), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, PlaceholderText = "Notiz zum Projekt – Ziel, Stand, Ideen …", FontSize = 12 };
            note.LostFocus += (_, _) => { if ((note.Text ?? "") != _core.Projects.Note(pid)) _core.Projects.SetNote(pid, note.Text ?? ""); };
            right.Children.Add(H("Notiz", 13)); right.Children.Add(note);
            right.Children.Add(H("Aktionen", 13));
            var open = new Button { Name = "btn-open-folder-" + pid, Content = "Ordner im Explorer öffnen (Freigabe nötig)", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
            open.Click += (_, _) => { _core.Integrations.PlanOpenProjectFolder(Principal.LocalOwner, pid); Show("approvals"); };
            right.Children.Add(open);
            if (File.Exists(Path.Combine(_core.Projects.ProjectFilesRoot(pid), "3d", "szene.json")))
            {
                var open3 = new Button { Name = "btn-open-3d-" + pid, Content = "Im 3D-Studio öffnen", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
                open3.Click += (_, _) => Open3DProject(pid);
                right.Children.Add(open3);
            }
            var newName = new TextBox { Name = "proj-rename", Text = sel.Name, FontSize = 12 };
            var ren = new Button { Name = "proj-rename-go", Content = "Umbenennen", FontSize = 12 };
            ren.Click += (_, _) => Try(V, () => { _core.Projects.Rename(pid, newName.Text ?? ""); _projMsg = "Umbenannt."; Show(V); });
            right.Children.Add(new DockPanel { Children = { Dk(ren, Avalonia.Controls.Dock.Right), newName } });
        }

        Border Panel(Control c, string name, double? w = null) => new() { Name = name, Child = new ScrollViewer { Content = c }, Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(10), BoxShadow = CardShadow, Width = w ?? double.NaN };
        var g = new Grid { Name = "proj-workspace", ColumnDefinitions = new ColumnDefinitions("240,*,290"), ColumnSpacing = 10 };
        g.Children.Add(Panel(left, "proj-left")); g.Children.Add(Pos(Panel(center, "proj-center"), 1)); g.Children.Add(Pos(Panel(right, "proj-right"), 2));
        return FitPage("Projekte", new Panel(), g);
    }
}
