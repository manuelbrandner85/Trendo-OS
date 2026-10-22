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

    private sealed class PathInfo { public string Page = ""; public PathFields.Kind Kind; public bool Multi; public IReadOnlyList<string> Ext = []; public Button? Recent; }
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
            AddHandler(Button.ClickEvent, (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => EnhancePathFields(ActivePageRoot, CurrentView), Avalonia.Threading.DispatcherPriority.Background), RoutingStrategies.Bubble, true);
        }
        if (root is null) return;
        foreach (var tb in root.GetLogicalDescendants().OfType<TextBox>().ToList())
        {
            if (_pathFields.TryGetValue(tb, out _)) continue;
            var hint = tb.PlaceholderText ?? tb.Watermark;
            var kind = PathFields.Classify(tb.Name, hint);
            if (kind == PathFields.Kind.None) continue;
            var info = new PathInfo { Page = page, Kind = kind, Multi = PathFields.Multiple(tb.Name, hint), Ext = PathFields.Extensions(hint) };
            _pathFields.Add(tb, info);
            DragDrop.SetAllowDrop(tb, true);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Name = (tb.Name ?? "pfad") + "-row" };
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
        }
    }

    /// <summary>Setzt die Zeile [Feld | Knöpfe] an die Stelle des Feldes, Layout-Angaben gehen mit.</summary>
    private static bool Wrap(TextBox tb, StackPanel row)
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
            var sp = StorageProvider;
            if (folder) got = (await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Ordner wählen", AllowMultiple = false })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
            else
            {
                var filters = new List<FilePickerFileType>();
                if (info.Ext.Count > 0) filters.Add(new FilePickerFileType(string.Join(", ", info.Ext.Select(e => e.ToUpperInvariant()))) { Patterns = info.Ext.Select(e => "*." + e).ToList() });
                filters.Add(new FilePickerFileType("Alle Dateien") { Patterns = ["*"] });
                got = (await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Datei wählen", AllowMultiple = req.Multiple, FileTypeFilter = filters })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
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
