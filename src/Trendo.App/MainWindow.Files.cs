using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Trendo.Core.Shell;

namespace Trendo.App;

/// <summary>T-0201: jedes Datei-/Ordnerfeld bekommt „Datei wählen …“ (echter Dialog), nimmt gezogene Dateien an und bietet die zuletzt benutzten Dateien der Seite an. Eintippen bleibt möglich.</summary>
public partial class MainWindow
{
    public sealed record PickRequest(string Field, bool Folder, bool Multiple, IReadOnlyList<string> Extensions);
    /// <summary>Für Tests: ersetzt den System-Dialog.</summary>
    public Func<PickRequest, Task<string[]?>>? PickOverride { get; set; }

    private sealed class PathInfo { public string Page = ""; public PathFields.Kind Kind; public bool Multi; public IReadOnlyList<string> Ext = []; public Button? Recent; public Button? Then; public TextBlock? What; }

    /// <summary>V-07 (T-0700): Ist das Pfadfeld selbst zu sehen? Nur in „Professionell“ – sonst trägt der Knopf „Datei wählen …“ die Auswahl, das Kärtchen zeigt die Datei.</summary>
    public bool PathTextVisible => ProgramLevel == Trendo.Core.Programs.UserLevel.Profi;

    /// <summary>V-07: Knopf direkt hinter dem Feld, der die gewählte Datei nur einliest, öffnet oder übernimmt – er läuft außerhalb von „Professionell“ gleich nach der Auswahl.
    /// Prüfen, Entpacken, Löschen, Senden oder Installieren laufen nie von selbst.</summary>
    public static bool IsReadStep(string? label)
    {
        var l = (label ?? "").Trim().TrimEnd('…', '.', ' ').ToLowerInvariant();
        if (l.Length == 0 || l.Length > 40) return false;
        if (new[] { "entpack", "lösch", "send", "install", "entfern", "überschreib", "zurücksetz", "start", "drucken", "hochlad" }.Any(l.Contains)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(l, @"(^|\s)(einlesen|öffnen|laden|übernehmen|importieren|aufnehmen|hinzufügen|anzeigen)( und prüfen)?$");
    }

    /// <summary>V-07: Knopf, der nach dem Feld folgt (gleiche Zeile), wenn er nur einliest – sonst null. Zwischen Feld und Knopf darf kein weiteres Eingabefeld stehen.</summary>
    static Button? ReadStepAfter(Control row)
    {
        if (row.Parent is not Panel p) return null;
        foreach (var c in p.Children.Skip(p.Children.IndexOf(row) + 1))
        {
            if (c is Button b) return IsReadStep(SimpleWords.Label(b)) ? b : null;
            if (c is TextBox or ComboBox) return null;
            if (c is Panel inner && inner.Children.OfType<Button>().FirstOrDefault() is { } ib && !inner.Children.OfType<TextBox>().Any()) return IsReadStep(SimpleWords.Label(ib)) ? ib : null;
        }
        return null;
    }

    /// <summary>V-07: Feld und Folgeknopf je nach Stufe zeigen. Das Feld bleibt im Baum (Tests, Ziehen und Ablegen, Inhalt für den Folgeknopf).</summary>
    void PathFieldLook(TextBox tb, PathInfo info)
    {
        var pro = PathTextVisible;
        tb.IsVisible = pro;
        if (info.Then is { } then) then.IsVisible = pro;
        if (info.What is { } what) what.IsVisible = !pro && string.IsNullOrWhiteSpace(tb.Text) && what.Text is { Length: > 0 };
    }
    private readonly ConditionalWeakTable<TextBox, PathInfo> _pathFields = new();
    private bool _dropWired;

    /// <summary>Alle erkannten Pfadfelder unter <paramref name="root"/> (für Tests und Drop aufs Studio).</summary>
    public List<TextBox> PathFieldsIn(Control root) =>
        root.GetLogicalDescendants().OfType<TextBox>().Where(t => _pathFields.TryGetValue(t, out _)).ToList();

    private void EnhancePathFields(Control? root, string page)
    {
        if (!_dropWired)
        {
            _dropWired = true;
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; }, RoutingStrategies.Bubble, true);
            AddHandler(DragDrop.DropEvent, (_, e) =>
            {
                var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray() ?? [];
                if (paths.Length > 0 && e.Source is Control s) { DropPaths(s, paths); e.Handled = true; }
            }, RoutingStrategies.Bubble, true);
            // Felder, die erst nach einem Klick entstehen (z. B. Rezept-Eingaben), bekommen die Knöpfe gleich danach
            AddHandler(Button.ClickEvent, (_, _) => _ui.Post(() => EnhancePathFields(ActivePageRoot, CurrentView), Avalonia.Threading.DispatcherPriority.Background), RoutingStrategies.Bubble, true);
        }
        if (root is null) return;
        foreach (var tb in root.GetLogicalDescendants().OfType<TextBox>().ToList())
        {
            if (_pathFields.TryGetValue(tb, out var known)) { PathFieldLook(tb, known); continue; }
            var hint = tb.PlaceholderText;
            var kind = PathFields.Classify(tb.Name, hint);
            if (kind == PathFields.Kind.None) continue;
            var info = new PathInfo { Page = page, Kind = kind, Multi = PathFields.Multiple(tb.Name, hint), Ext = PathFields.Extensions(hint) };
            _pathFields.Add(tb, info);
            DragDrop.SetAllowDrop(tb, true);
            // T-0261: WrapPanel – in schmalen Spalten (Eigenschaften) rutschen die Knöpfe unter das Feld statt aus dem Fenster
            var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 4, LineSpacing = 4, Name = (tb.Name ?? "pfad") + "-row" };
            if (!Wrap(tb, row)) continue;
            var id = tb.Name ?? "pfad";
            if (kind is PathFields.Kind.File or PathFields.Kind.FileOrFolder)
                row.Children.Add(PickButton($"{id}-pick", "Datei wählen …", info.Multi ? "Eine oder mehrere Dateien auswählen – oder hierher ziehen" : "Datei auswählen – oder hierher ziehen", tb, false));
            if (kind is PathFields.Kind.Folder or PathFields.Kind.FileOrFolder)
                row.Children.Add(PickButton($"{id}-pickdir", "Ordner wählen …", "Ordner auswählen – oder hierher ziehen", tb, true));
            var recent = new Button { Name = $"{id}-recent", Content = "🕘", VerticalAlignment = VerticalAlignment.Bottom };
            ToolTip.SetTip(recent, "Zuletzt benutzte Dateien");
            recent.Click += (_, _) => ShowRecent(tb, recent);
            info.Recent = recent; row.Children.Add(recent); UpdateRecent(tb);
            // G6: Vorschau der gewählten Datei (Symbol, Name, Größe; bei Bildern ein Miniaturbild) statt nur eines langen Pfads
            var chip = new Border { Name = $"{id}-preview", IsVisible = false, VerticalAlignment = VerticalAlignment.Bottom, Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 3), Margin = new Thickness(4, 0, 0, 0) };
            row.Children.Add(chip);
            // V-07: ohne sichtbares Feld sagt ein kurzer Text, welche Datei gemeint ist (aus dem Platzhalter)
            var what0 = (hint ?? "").Trim();
            if (what0.StartsWith("Klicken", StringComparison.Ordinal)) what0 = what0.Contains(" – ") ? what0[(what0.IndexOf(" – ", StringComparison.Ordinal) + 3)..] : "";
            info.What = new TextBlock { Name = $"{id}-what", Text = what0, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 320, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 0, 0, 0) };
            if (what0.Length > 0) ToolTip.SetTip(info.What, what0);
            row.Children.Add(info.What);
            tb.TextChanged += (_, _) => { FilePreview(tb.Text, chip); if (_pathFields.TryGetValue(tb, out var pi)) PathFieldLook(tb, pi); };
            FilePreview(tb.Text, chip);
            info.Then = ReadStepAfter(row);
            PathFieldLook(tb, info);
            // Wunsch 27.10.2026: nie einen Pfad eintippen müssen – ein Klick ins Feld öffnet sofort die Auswahl (Ordner bzw. Datei)
            tb.IsReadOnly = true; tb.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            var pickFolder = kind == PathFields.Kind.Folder;
            tb.AddHandler(PointerReleasedEvent, async (_, e) => { if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left) await PickInto(tb, pickFolder); }, RoutingStrategies.Tunnel, handledEventsToo: true);
            tb.AddHandler(KeyDownEvent, async (_, e) => { if (e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Space) { e.Handled = true; await PickInto(tb, pickFolder); } }, RoutingStrategies.Tunnel);
            if (string.IsNullOrEmpty(tb.Text) && !(tb.PlaceholderText ?? "").StartsWith("Klicken", StringComparison.Ordinal))
                tb.PlaceholderText = (pickFolder ? "Klicken, um einen Ordner zu wählen" : "Klicken, um eine Datei zu wählen") + (string.IsNullOrEmpty(tb.PlaceholderText) ? "" : " – " + tb.PlaceholderText);
            if (ToolTip.GetTip(tb) is null) ToolTip.SetTip(tb, pickFolder ? "Klicken öffnet die Ordnerauswahl – oder einen Ordner hierher ziehen" : "Klicken öffnet die Dateiauswahl – oder Dateien hierher ziehen");
        }
    }

    /// <summary>G6: kleine Karte zur gewählten Datei. Mehrere Dateien: Anzahl. Nicht vorhanden: ausgeblendet.</summary>
    private static void FilePreview(string? text, Border chip)
    {
        var paths = (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => p.Trim('"')).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (paths.Count == 0) { chip.IsVisible = false; chip.Child = null; return; }
        var first = paths[0]; var isDir = Directory.Exists(first);
        var ext = Path.GetExtension(first).ToLowerInvariant();
        var icon = isDir ? "📁" : ext switch
        {
            ".pdf" => "📕", ".docx" or ".odt" or ".txt" or ".md" => "📄", ".xlsx" or ".ods" or ".csv" => "📊", ".pptx" or ".odp" => "📽",
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp" => "🖼", ".mp4" or ".mkv" or ".mov" or ".webm" or ".avi" => "🎬",
            ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" => "🎵", ".zip" or ".7z" or ".rar" => "🗜", ".stl" or ".obj" or ".glb" or ".gltf" or ".fbx" or ".blend" or ".3mf" => "🧊", _ => "📎",
        };
        var size = isDir ? "Ordner" : FileSize(new FileInfo(first).Length);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (!isDir && ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" && new FileInfo(first).Length < 30_000_000)
            try { using var s = File.OpenRead(first); line.Children.Add(new Image { Source = Avalonia.Media.Imaging.Bitmap.DecodeToHeight(s, 28), Height = 28, MaxWidth = 48 }); }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { line.Children.Add(new TextBlock { Text = icon, FontSize = 16 }); }
        else line.Children.Add(new TextBlock { Text = icon, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        var name = Path.GetFileName(first.TrimEnd(Path.DirectorySeparatorChar));
        line.Children.Add(new TextBlock { Text = paths.Count > 1 ? $"{name} und {paths.Count - 1} weitere" : name, Foreground = Text, FontSize = 12, MaxWidth = 220, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        line.Children.Add(new TextBlock { Text = size, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        chip.Child = line; chip.IsVisible = true;
        ToolTip.SetTip(chip, string.Join("\n", paths));
    }

    static string FileSize(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" : b >= 1L << 20 ? $"{b / (double)(1L << 20):0.0} MB" : $"{Math.Max(1, b / 1024)} KB";

    /// <summary>Setzt die Zeile [Feld | Knöpfe] an die Stelle des Feldes, Layout-Angaben gehen mit.</summary>
    private static bool Wrap(TextBox tb, Panel row)
    {
        var parent = tb.Parent;
        row.HorizontalAlignment = tb.HorizontalAlignment; row.VerticalAlignment = tb.VerticalAlignment; row.Margin = tb.Margin; tb.Margin = default;
        Grid.SetRow(row, Grid.GetRow(tb)); Grid.SetColumn(row, Grid.GetColumn(tb)); Grid.SetColumnSpan(row, Grid.GetColumnSpan(tb)); DockPanel.SetDock(row, DockPanel.GetDock(tb));
        switch (parent)
        {
            case Panel p: { var i = p.Children.IndexOf(tb); p.Children.RemoveAt(i); row.Children.Add(tb); p.Children.Insert(i, row); return true; }
            case Decorator d: d.Child = null; row.Children.Add(tb); d.Child = row; return true;
            case ContentControl c: c.Content = null; row.Children.Add(tb); c.Content = row; return true;
            default: return false;
        }
    }

    private Button PickButton(string name, string label, string tip, TextBox tb, bool folder)
    {
        var b = new Button { Name = name, Content = label, VerticalAlignment = VerticalAlignment.Bottom };
        ToolTip.SetTip(b, tip);
        b.Click += async (_, _) => await PickInto(tb, folder);
        return b;
    }

    private async Task PickInto(TextBox tb, bool folder)
    {
        if (!_pathFields.TryGetValue(tb, out var info)) return;
        var req = new PickRequest(tb.Name ?? "", folder, info.Multi && !folder, info.Ext);
        string[]? got;
        if (PickOverride is { } fake) got = await fake(req);
        else
        {
            // T-0764 (9. Prüfrunde): über den zentralen Helfer – Kinder bekommen Trendos eigene Auswahl, nie den Windows-Dialog
            if (folder) got = (await PickFolders(new FolderPickerOpenOptions { Title = "Ordner wählen", AllowMultiple = false })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
            else
            {
                var filters = new List<FilePickerFileType>();
                if (info.Ext.Count > 0) filters.Add(new FilePickerFileType(string.Join(", ", info.Ext.Select(e => e.ToUpperInvariant()))) { Patterns = info.Ext.Select(e => "*." + e).ToList() });
                filters.Add(new FilePickerFileType("Alle Dateien") { Patterns = ["*"] });
                got = (await PickOpenFiles(new FilePickerOpenOptions { Title = "Datei wählen", AllowMultiple = req.Multiple, FileTypeFilter = filters })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
            }
        }
        if (got is { Length: > 0 }) Fill(tb, got);
    }

    /// <summary>Gezogene Dateien: aufs Feld → dieses Feld; aufs Studio → erstes leeres passendes Feld der Seite.</summary>
    public bool DropPaths(Control target, string[] paths)
    {
        var tb = target as TextBox ?? (target as Visual)?.FindAncestorOfType<TextBox>();
        if (tb is null || !_pathFields.TryGetValue(tb, out _))
        {
            var frame = (target as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => _frames.Values.Any(f => ReferenceEquals(f.Frame, c)));
            var root = (frame ?? ActivePageRoot) ?? this;
            var fields = PathFieldsIn(root);
            bool isDir = Directory.Exists(paths[0]);
            bool Fits(TextBox t) => _pathFields.TryGetValue(t, out var i) && (isDir ? i.Kind != PathFields.Kind.File : i.Kind != PathFields.Kind.Folder);
            tb = fields.FirstOrDefault(t => Fits(t) && string.IsNullOrWhiteSpace(t.Text)) ?? fields.FirstOrDefault(Fits) ?? fields.FirstOrDefault();
        }
        if (tb is null) return false;
        Fill(tb, paths);
        return true;
    }

    private void Fill(TextBox tb, string[] paths)
    {
        if (!_pathFields.TryGetValue(tb, out var info)) { tb.Text = paths[0]; return; }
        if (info.Kind == PathFields.Kind.Folder && File.Exists(paths[0])) paths = [Path.GetDirectoryName(paths[0]) ?? paths[0]];
        tb.Text = info.Multi
            ? string.Join("\n", (tb.Text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Concat(paths))
            : paths[0];
        var list = _settings.RecentFiles.GetValueOrDefault(info.Page) ?? [];
        foreach (var p in paths) list = PathFields.Remember(list, p);
        _settings.RecentFiles[info.Page] = list; SaveSettings();
        foreach (var t in _pathFields) if (t.Value.Page == info.Page) UpdateRecent(t.Key);
        // V-07: ein Klick genügt – außerhalb von „Professionell“ liest der Folgeknopf („Einlesen“, „Öffnen“ …) die Datei sofort ein
        if (!PathTextVisible && info.Then is { } then && then.IsEnabled) { then.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return; }
        tb.Focus();
    }

    private List<string> RecentFor(TextBox tb) =>
        _pathFields.TryGetValue(tb, out var i)
            ? (_settings.RecentFiles.GetValueOrDefault(i.Page) ?? []).Where(p => i.Kind switch { PathFields.Kind.Folder => Directory.Exists(p), PathFields.Kind.File => File.Exists(p), _ => File.Exists(p) || Directory.Exists(p) })
                .Where(p => i.Ext.Count == 0 || Directory.Exists(p) || i.Ext.Contains(Path.GetExtension(p).TrimStart('.').ToLowerInvariant())).ToList()
            : [];

    private void UpdateRecent(TextBox tb) { if (_pathFields.TryGetValue(tb, out var i) && i.Recent is { } r) r.IsVisible = RecentFor(tb).Count > 0; }

    private void ShowRecent(TextBox tb, Button anchor)
    {
        var menu = new MenuFlyout();
        foreach (var p in RecentFor(tb))
        {
            var mi = new MenuItem { Header = Path.GetFileName(p.TrimEnd('/', '\\')) + "  –  " + Path.GetDirectoryName(p), Name = "recent-item" };
            mi.Click += (_, _) => Fill(tb, [p]);
            menu.Items.Add(mi);
        }
        menu.ShowAt(anchor);
    }
}
