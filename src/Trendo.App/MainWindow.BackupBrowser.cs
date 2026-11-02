using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Sicherung mit Zeitleiste (Masterprompt 131): Sicherungen als Punkte auf einer Zeitachse, Inhalt durchblättern, Vergleich mit heute,
/// Vorschau (Text, Bild), einzelne Datei zurückholen – nach „Ja“, die heutige Fassung wird vorher aufgehoben.</summary>
public partial class MainWindow
{
    public Backup BackupStore => Backups;
    public string? BackupSelected { get; set; }
    public string? BackupFile { get; set; }
    public string BackupFilter { get; set; } = "Geändert";
    private string _backupQuery = ""; private bool _backupOneAsk;

    Control BackupTimeline(IReadOnlyList<FileInfo> list)
    {
        var strip = new Grid { Name = "backup-timeline", Height = 64, Margin = new Thickness(0, 4, 0, 4) };
        if (list.Count == 0) return strip;
        var first = list.Min(f => f.LastWriteTime); var last = list.Max(f => f.LastWriteTime); var span = Math.Max(1, (last - first).TotalMinutes);
        var line = new Border { Height = 3, Background = Brush.Parse("#334155"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
        var canvas = new Canvas { Name = "backup-timeline-canvas" };
        strip.Children.Add(line); strip.Children.Add(canvas);
        strip.SizeChanged += (_, _) =>
        {
            canvas.Children.Clear(); var w = strip.Bounds.Width - 40;
            for (var i = 0; i < list.Count; i++)
            {
                var f = list[i]; var x = 10 + (list.Count == 1 ? w / 2 : (f.LastWriteTime - first).TotalMinutes / span * w); var on = f.FullName == BackupSelected;
                var dot = new Button { Name = $"backup-pick-{i}", Width = on ? 22 : 16, Height = on ? 22 : 16, CornerRadius = new CornerRadius(11), Padding = new Thickness(0), Background = Brush.Parse(on ? "#FACC15" : "#2F86F6"), BorderThickness = new Thickness(0) };
                ToolTip.SetTip(dot, $"{f.LastWriteTime:dddd, dd.MM.yyyy HH:mm} · {f.Length / 1024} KB");
                var full = f.FullName; dot.Click += (_, _) => { BackupSelected = full; BackupFile = null; _backupOneAsk = false; Show("sicherung"); };
                Canvas.SetLeft(dot, x - dot.Width / 2); Canvas.SetTop(dot, 32 - dot.Height / 2); canvas.Children.Add(dot);
                if (on || i == 0 || i == list.Count - 1)
                {
                    var t = new TextBlock { Text = f.LastWriteTime.ToString("dd.MM. HH:mm", De), Foreground = on ? Text : Muted, FontSize = 10.5 };
                    Canvas.SetLeft(t, Math.Max(0, x - 28)); Canvas.SetTop(t, on ? 2 : 46); canvas.Children.Add(t);
                }
            }
        };
        return strip;
    }

    Control BackupBrowser()
    {
        var sp = new StackPanel { Name = "backup-browser", Spacing = 6 };
        if (BackupSelected is not { } zip || !File.Exists(zip)) { sp.Children.Add(P("Einen Punkt auf der Zeitleiste anklicken, um hineinzusehen.", Muted, 12)); return sp; }
        IReadOnlyList<Backup.Diff> diff;
        try { diff = Backups.CompareWithNow(zip); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { sp.Children.Add(P("Sicherung nicht lesbar: " + ex.Message, Brush.Parse("#F87171"), 12)); return sp; }
        int Cnt(Backup.Change c) => diff.Count(d => d.Kind == c);
        sp.Children.Add(new TextBlock { Name = "backup-summary", Text = $"Sicherung vom {File.GetLastWriteTime(zip):dd.MM.yyyy HH:mm}: {diff.Count(d => d.Kind != Backup.Change.Neu)} Dateien · seither {Cnt(Backup.Change.Geaendert)} geändert, {Cnt(Backup.Change.Geloescht)} gelöscht, {Cnt(Backup.Change.Neu)} neu", Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        var chips = new WrapPanel();
        foreach (var (k, label) in new[] { ("Geändert", "Geändert"), ("Gelöscht", "Seither gelöscht"), ("Neu", "Seither neu"), ("Alle", "Alle") })
        { var kk = k; var b = Act("backup-filter-" + k.ToLowerInvariant(), label, () => { BackupFilter = kk; Show("sicherung"); }, BackupFilter == k); b.Margin = new Thickness(0, 0, 4, 4); chips.Children.Add(b); }
        var q = new TextBox { Name = "backup-search", PlaceholderText = "🔎  Datei suchen …", Text = _backupQuery, Width = 220, CornerRadius = new CornerRadius(8), Margin = new Thickness(4, 0, 0, 4) }; q.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _backupQuery = q.Text ?? ""; Show("sicherung"); } };
        chips.Children.Add(q); sp.Children.Add(chips);
        var shown = diff.Where(d => BackupFilter switch { "Geändert" => d.Kind == Backup.Change.Geaendert, "Gelöscht" => d.Kind == Backup.Change.Geloescht, "Neu" => d.Kind == Backup.Change.Neu, _ => d.Kind != Backup.Change.Neu })
            .Where(d => _backupQuery.Length == 0 || d.Path.Contains(_backupQuery, StringComparison.OrdinalIgnoreCase)).Take(200).ToList();
        var list = new StackPanel { Name = "backup-files", Spacing = 1 };
        for (var i = 0; i < shown.Count; i++)
        {
            var d = shown[i]; var path = d.Path;
            var (tag, col) = d.Kind switch { Backup.Change.Geaendert => ("geändert", "#F59E0B"), Backup.Change.Geloescht => ("gelöscht", "#F87171"), Backup.Change.Neu => ("neu", "#60A5FA"), _ => ("gleich", "#64748B") };
            var b = new Button { Name = $"backup-file-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 3), Background = path == BackupFile ? Brush.Parse("#33C9A227") : Brushes.Transparent,
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = { new TextBlock { Text = path, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                    Pos(new TextBlock { Text = DiskMapView.Size(d.Bytes), Foreground = Muted, FontSize = 11, Margin = new Thickness(8, 0) }, 1), Pos(new TextBlock { Text = tag, Foreground = Brush.Parse(col), FontSize = 11, FontWeight = FontWeight.SemiBold }, 2) } } };
            b.Click += (_, _) => { BackupFile = path; _backupOneAsk = false; Show("sicherung"); };
            list.Children.Add(b);
        }
        if (shown.Count == 0) list.Children.Add(P(BackupFilter == "Geändert" ? "Seit dieser Sicherung hat sich nichts geändert." : "Keine Dateien.", Muted, 12));
        // Vorschau und Zurückholen
        var preview = new StackPanel { Name = "backup-preview", Spacing = 6, Width = 380 };
        if (BackupFile is { } bf && diff.FirstOrDefault(x => x.Path == bf) is { } sel)
        {
            preview.Children.Add(new TextBlock { Text = bf, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (sel.Kind != Backup.Change.Neu)
            {
                try
                {
                    var bytes = Backups.Read(zip, bf, 400_000); var ext = Path.GetExtension(bf).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp") preview.Children.Add(new Image { Name = "backup-preview-image", Source = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(bytes)), MaxHeight = 260, Stretch = Stretch.Uniform });
                    else if (bytes.Take(4000).All(c => c >= 9 && c != 0)) preview.Children.Add(new Border { Background = Brush.Parse("#0B1424"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), MaxHeight = 260,
                        Child = new ScrollViewer { Content = new SelectableTextBlock { Name = "backup-preview-text", Text = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 8000)), FontFamily = new FontFamily("Consolas, Cascadia Mono, monospace"), FontSize = 11.5, Foreground = Text, TextWrapping = TextWrapping.Wrap } } });
                    else preview.Children.Add(P($"Keine Vorschau ({DiskMapView.Size(sel.Bytes)}).", Muted, 12));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FileNotFoundException) { preview.Children.Add(P("Vorschau nicht möglich: " + ex.Message, Muted, 12)); }
                if (sel.Kind == Backup.Change.Gleich) preview.Children.Add(P("Gleich wie heute – nichts zurückzuholen.", Muted, 12));
                else if (!_backupOneAsk) preview.Children.Add(Act("backup-one", sel.Kind == Backup.Change.Geloescht ? "Diese Datei zurückholen" : "Diese Fassung zurückholen …", () => { _backupOneAsk = true; Show("sicherung"); }, true));
                else
                {
                    var yes = Act("backup-one-yes", "Ja, zurückholen", () =>
                    {
                        try { var kept = Backups.RestoreOne(zip, bf, DateTime.Now); _backupMsg = $"„{Path.GetFileName(bf)}“ ist zurückgeholt." + (kept is null ? "" : " Die heutige Fassung liegt aufgehoben unter Sicherungen › einzeln."); }
                        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or FileNotFoundException) { _backupMsg = "Zurückholen hat nicht geklappt: " + ex.Message; }
                        _backupOneAsk = false; Show("sicherung");
                    }, true);
                    var no = Act("backup-one-no", "Nein", () => { _backupOneAsk = false; Show("sicherung"); });
                    preview.Children.Add(new Border { Name = "backup-one-confirm", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = new StackPanel { Spacing = 6, Children = {
                        new TextBlock { Text = sel.Kind == Backup.Change.Geloescht ? "Die Datei wird wiederhergestellt." : "Die heutige Fassung wird ersetzt – sie wird vorher aufgehoben.", Foreground = Text, TextWrapping = TextWrapping.Wrap }, new WrapPanel { Children = { yes, new Border { Width = 6 }, no } } } } });
                }
            }
            else preview.Children.Add(P("Diese Datei ist nach der Sicherung entstanden – sie ist nicht darin.", Muted, 12));
        }
        else preview.Children.Add(P("Datei anklicken für Vorschau und Zurückholen.", Muted, 12));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto") };
        grid.Children.Add(new ScrollViewer { Content = list, MaxHeight = 360 }); grid.Children.Add(Pos(preview, 2));
        sp.Children.Add(grid);
        return sp;
    }
}
