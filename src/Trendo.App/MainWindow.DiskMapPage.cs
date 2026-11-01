using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Speicherplatz-Karte auf „Speicher & Leistung“ (Masterprompt 131): Ordner einlesen (Hintergrund, abbrechbar), Kachelkarte mit Zoom,
/// große Dateien, Duplikate, „In den Papierkorb“ nur nach Ja (rückholbar, nie endgültig gelöscht).</summary>
public partial class MainWindow
{
    private DiskMap.Node? _diskRoot, _diskAt, _diskSel; private CancellationTokenSource? _diskScan; private int _diskCount; private string _diskMsg = "", _diskFolder = "";
    private bool _diskAskTrash; private HashSet<string> _diskDupes = [];
    public DiskMap.Node? DiskAt => _diskAt;
    public DiskMap.Node? DiskSelected => _diskSel;
    public bool DiskScanning => _diskScan is not null;
    public DiskMapView? DiskViewForTests { get; private set; }
    /// <summary>Für Tests: statt des echten Papierkorbs.</summary>
    public Action<string>? TrashOverride { get; set; }

    public async Task DiskScanAsync(string folder)
    {
        _tmTab = "speicher"; // Task-Manager: die Platzkarte liegt im Reiter „Speicher“
        if (!Directory.Exists(folder)) { _diskMsg = "Diesen Ordner gibt es nicht."; Show("ressourcen"); return; }
        _diskScan?.Cancel(); _diskScan = new CancellationTokenSource(); _diskFolder = folder; _diskCount = 0; _diskMsg = "Wird eingelesen …"; _diskDupes = []; Show("ressourcen");
        var prog = new Progress<int>(n => { _diskCount = n; });
        try
        {
            var ct = _diskScan.Token;
            var root = await Task.Run(() => DiskMap.Scan(folder, 8, prog, ct), ct);
            _diskRoot = _diskAt = root; _diskSel = null;
            _diskMsg = $"{DiskMapView.Size(root.Bytes)} in {root.Files:N0} Dateien.";
            _core.Audit.Append(Principal.LocalOwner.Name, "disk.scan", Path.GetFileName(folder));
        }
        catch (OperationCanceledException) { _diskMsg = "Einlesen abgebrochen."; }
        finally { _diskScan?.Dispose(); _diskScan = null; }
        Show("ressourcen");
    }

    public void DiskSelect(DiskMap.Node n) { _diskSel = n; _diskAskTrash = false; Show("ressourcen"); }
    public void DiskOpen(DiskMap.Node n) { if (!n.IsDir) return; _diskAt = n; _diskSel = null; _diskAskTrash = false; Show("ressourcen"); }

    /// <summary>Gewählte Datei/Ordner in den Papierkorb – nur nach Bestätigung (zweiter Schritt „Ja“).</summary>
    public void DiskTrash(bool confirmed)
    {
        if (_diskSel is not { } n) return;
        if (!confirmed) { _diskAskTrash = true; Show("ressourcen"); return; }
        try
        {
            if (TrashOverride is { } fake) fake(n.Path); else RecycleBin.Move(n.Path);
            DiskMap.Remove(n); _core.Audit.Append(Principal.LocalOwner.Name, "disk.trash", n.Name);
            _diskMsg = $"„{n.Name}“ ({DiskMapView.Size(n.Bytes)}) liegt im Papierkorb – dort kannst du es zurückholen."; _diskSel = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException) { _diskMsg = ex.Message; }
        _diskAskTrash = false; Show("ressourcen");
    }

    public async Task DiskFindDuplicates()
    {
        if (_diskAt is not { } at) return;
        _diskMsg = "Suche Duplikate …"; Show("ressourcen");
        var groups = await Task.Run(() => FileAnalysis.Duplicates(at.Path, 1 << 20));
        _diskDupes = groups.SelectMany(g => g.Skip(1)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var waste = groups.Sum(g => g.Skip(1).Sum(f => { try { return new FileInfo(f).Length; } catch (IOException) { return 0L; } }));
        _diskMsg = groups.Count == 0 ? "Keine doppelten Dateien (ab 1 MB) gefunden." : $"{groups.Count} Gruppen doppelter Dateien – {DiskMapView.Size(waste)} könnten frei werden. Rot gestrichelt markiert.";
        Show("ressourcen");
    }

    Control DiskMapCard()
    {
        var sp = new StackPanel { Name = "disk-card", Spacing = 8 };
        sp.Children.Add(H("Speicherplatz als Karte", 15));
        var folder = new TextBox { Name = "disk-folder", Text = _diskFolder.Length > 0 ? _diskFolder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), PlaceholderText = "Ordner", Width = 360 };
        var go = new Button { Name = "disk-scan", Content = DiskScanning ? "Abbrechen" : "Karte erstellen", Background = DiskScanning ? null : Accent, Foreground = DiskScanning ? Text : Brushes.White };
        if (DiskScanning) go.Background = Brushes.Transparent;
        go.Click += (_, _) => { if (DiskScanning) _diskScan?.Cancel(); else _ = DiskScanAsync((folder.Text ?? "").Trim().Trim('"')); };
        sp.Children.Add(new WrapPanel { Children = { folder, new Border { Width = 6 }, go } });
        if (_diskMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "disk-msg", Text = DiskScanning ? $"Wird eingelesen … {_diskCount:N0} Dateien" : _diskMsg, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (_diskAt is not { } at) return sp;
        // Pfad zum Zurückgehen
        var crumbs = new WrapPanel { Name = "disk-path" }; var chain = new List<DiskMap.Node>(); for (var n = at; n is not null; n = n.Parent) chain.Insert(0, n);
        for (var i = 0; i < chain.Count; i++)
        {
            var c = chain[i]; var b = new Button { Name = $"disk-up-{i}", Content = (i == 0 ? "⌂ " : "› ") + c.Name, FontSize = 12, Padding = new Thickness(6, 2), Background = Brushes.Transparent, Margin = new Thickness(0, 0, 2, 4) };
            b.Click += (_, _) => DiskOpen(c); crumbs.Children.Add(b);
        }
        sp.Children.Add(crumbs);
        var view = new DiskMapView { Name = "disk-map", Root = at, Selected = _diskSel, Marked = _diskDupes, Height = 420, Open = DiskOpen, Select = DiskSelect };
        DiskViewForTests = view;
        // rechts: Auswahl, Legende, große Dateien
        var side = new StackPanel { Spacing = 6, Width = 300 };
        if (_diskSel is { } s)
        {
            side.Children.Add(new TextBlock { Name = "disk-sel", Text = (s.IsDir ? "📁 " : "") + s.Name, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            side.Children.Add(P($"{DiskMapView.Size(s.Bytes)}{(s.IsDir ? $" · {s.Files:N0} Dateien" : " · " + DiskMap.Kind(s.Name))} · {s.Bytes * 100.0 / Math.Max(1, at.Bytes):0.#} % hiervon", Muted, 12));
            var row = new WrapPanel();
            if (s.IsDir && s.Children.Count > 0) { var o = new Button { Name = "disk-open", Content = "Hinein", Margin = new Thickness(0, 0, 4, 4) }; o.Click += (_, _) => DiskOpen(s); row.Children.Add(o); }
            var show = new Button { Name = "disk-show", Content = "Im Ordner zeigen", Margin = new Thickness(0, 0, 4, 4) }; show.Click += (_, _) => Launch(s.IsDir ? s.Path : Path.GetDirectoryName(s.Path)!); row.Children.Add(show);
            var trash = new Button { Name = "disk-trash", Content = "In den Papierkorb …", Margin = new Thickness(0, 0, 4, 4) }; trash.Click += (_, _) => DiskTrash(false); row.Children.Add(trash);
            side.Children.Add(row);
            if (_diskAskTrash)
            {
                var yes = new Button { Name = "disk-trash-yes", Content = "Ja, in den Papierkorb", Background = Accent, Foreground = Brushes.White }; yes.Click += (_, _) => DiskTrash(true);
                var no = new Button { Name = "disk-trash-no", Content = "Nein" }; no.Click += (_, _) => { _diskAskTrash = false; Show("ressourcen"); };
                side.Children.Add(new Border { Name = "disk-confirm", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = new StackPanel { Spacing = 6, Children = {
                    new TextBlock { Text = $"„{s.Name}“ ({DiskMapView.Size(s.Bytes)}) in den Papierkorb verschieben? Du kannst es dort zurückholen.", Foreground = Text, TextWrapping = TextWrapping.Wrap }, new WrapPanel { Children = { yes, new Border { Width = 6 }, no } } } } });
            }
        }
        else side.Children.Add(P("Klick wählt aus, zweiter Klick auf einen Ordner geht hinein.", Muted, 12));
        var dup = new Button { Name = "disk-dupes", Content = "Duplikate suchen (ab 1 MB)" }; dup.Click += (_, _) => _ = DiskFindDuplicates(); side.Children.Add(dup);
        var legend = new WrapPanel();
        foreach (var (k, c) in DiskMapView.KindColor) legend.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 4), Children = { new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2), Background = Brush.Parse(c), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = k, Foreground = Muted, FontSize = 11 } } });
        side.Children.Add(legend);
        side.Children.Add(new TextBlock { Text = "Größte Dateien hier", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5 });
        var big = DiskMap.Largest(at, 8);
        for (var i = 0; i < big.Count; i++) { var f = big[i]; var b = new Button { Name = $"disk-big-{i}", Content = $"{DiskMapView.Size(f.Bytes),9}  {f.Name}", FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(6, 2), Background = Brushes.Transparent }; ToolTip.SetTip(b, f.Path); b.Click += (_, _) => { if (f.Parent is { } p0) _diskAt = p0; DiskSelect(f); }; side.Children.Add(b); }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto") };
        grid.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = view }); grid.Children.Add(Pos(side, 2));
        sp.Children.Add(grid);
        sp.Children.Add(P("Trendo löscht nichts endgültig: „In den Papierkorb“ verschiebt erst nach deinem Ja, und du kannst es dort zurückholen.", Muted, 11));
        return sp;
    }
}
