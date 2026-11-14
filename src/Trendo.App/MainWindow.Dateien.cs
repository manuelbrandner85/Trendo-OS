using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using Trendo.Core.Shell;

namespace Trendo.App;

/// <summary>Dateien (04.11.2026): Dateimanager im Stil des gewählten Systems – Datei-Explorer mit „Dieser PC“ (Windows 11), Finder (macOS),
/// „Dateien“ (GNOME). Gleiche Funktionen überall: Ordner, Laufwerke, Öffnen, Neuer Ordner, Umbenennen, Ausschneiden/Kopieren/Einfügen,
/// in den Papierkorb, Sortieren, Ansicht Liste/Symbole, Suchen im Ordner. Nachgebaut nach Bedienkonzept, ohne fremde Symbole.</summary>
public partial class MainWindow
{
    const string FK = "dateien";
    public const string PcPlace = "::pc";
    FileBrowser? _fb;
    public FileBrowser Files => _fb ??= new FileBrowser { Root = _opt.Isolated ? Path.Combine(_core.DataRoot, "dateien-test") : null };
    public string FilesDir { get; private set; } = PcPlace;
    readonly List<string> _fbSel = [];
    string? _fbAnchor;
    List<string> _fbShown = [];
    /// <summary>Zuletzt gewähltes Element (Einzelauswahl); Setzen ersetzt die ganze Auswahl.</summary>
    public string? FilesSelected { get => _fbSel.Count > 0 ? _fbSel[^1] : null; set { _fbSel.Clear(); if (value is not null) _fbSel.Add(value); _fbAnchor = value; } }
    /// <summary>Alle gewählten Elemente (Mehrfachauswahl).</summary>
    public IReadOnlyList<string> FilesSelection => _fbSel;
    bool IsSel(string path) => _fbSel.Any(x => SamePath(x, path));

    /// <summary>Klick auf ein Element wie im System: allein wählt nur dieses, Strg (Mac: ⌘) schaltet dazu/weg, Umschalt wählt den Bereich ab dem Anker.</summary>
    public void FilesClick(string path, KeyModifiers mods)
    {
        var toggle = _style == "mac" ? OsBehavior.Cmd(ModsOf(mods), OperatingSystem.IsMacOS()) : mods.HasFlag(KeyModifiers.Control);
        if (mods.HasFlag(KeyModifiers.Shift) && _fbAnchor is { } a && _fbShown.FindIndex(x => SamePath(x, a)) is var i0 && i0 >= 0 && _fbShown.FindIndex(x => SamePath(x, path)) is var i1 && i1 >= 0)
        {
            if (!toggle) _fbSel.Clear();
            for (var i = Math.Min(i0, i1); i <= Math.Max(i0, i1); i++) if (!IsSel(_fbShown[i])) _fbSel.Add(_fbShown[i]);
        }
        else if (toggle) { var k = _fbSel.FindIndex(x => SamePath(x, path)); if (k >= 0) _fbSel.RemoveAt(k); else _fbSel.Add(path); _fbAnchor = path; }
        else FilesSelected = path;
        _fbRenaming = false; Show(FK);
    }

    /// <summary>Alles auswählen (Strg+A, Mac ⌘A).</summary>
    public void FilesSelectAll() { _fbSel.Clear(); _fbSel.AddRange(_fbShown); _fbAnchor = _fbShown.FirstOrDefault(); Show(FK); }

    /// <summary>Auswahlrahmen aufziehen: alles, was den Rahmen berührt; mit Strg/⌘ zur bestehenden Auswahl dazu.</summary>
    public void FilesBand(IEnumerable<string> hit, bool add)
    {
        if (!add) _fbSel.Clear();
        foreach (var h in hit) if (!IsSel(h)) _fbSel.Add(h);
        _fbAnchor = _fbSel.FirstOrDefault(); Show(FK);
    }
    readonly List<string> _fbBack = [], _fbFwd = [];

    // ---------- Registerkarten (Explorer seit Windows 11, Finder-Tabs, GNOME „Dateien“) ----------
    sealed class FbTab { public string Dir = PcPlace; public List<string> Back = [], Fwd = []; }
    readonly List<FbTab> _fbTabs = [new()];
    int _fbTabIx;
    public int FilesTabIndex => _fbTabIx;
    public int FilesTabCount => _fbTabs.Count;
    public IReadOnlyList<string> FilesTabTitles => [.. _fbTabs.Select((t, i) => PlaceName(i == _fbTabIx ? FilesDir : t.Dir))];
    void SaveTab() { var t = _fbTabs[_fbTabIx]; t.Dir = FilesDir; t.Back = [.. _fbBack]; t.Fwd = [.. _fbFwd]; }
    void LoadTab(int i)
    {
        _fbTabIx = Math.Clamp(i, 0, _fbTabs.Count - 1); var t = _fbTabs[_fbTabIx];
        FilesDir = t.Dir; _fbBack.Clear(); _fbBack.AddRange(t.Back); _fbFwd.Clear(); _fbFwd.AddRange(t.Fwd);
        FilesSelected = null; _fbQuery = ""; _fbRenaming = false; FilesPreview = null; FilesMessage = "";
    }
    /// <summary>Neue Registerkarte rechts neben der aktiven – leer im persönlichen Ordner („Start“), oder mit einem Ordner (Mittelklick, Kontextmenü).</summary>
    public void FilesNewTab(string? dir = null)
    {
        SaveTab(); _fbTabs.Insert(_fbTabIx + 1, new FbTab { Dir = dir ?? Files.Home }); LoadTab(_fbTabIx + 1); Show(FK);
    }
    /// <summary>Registerkarte schließen; die letzte bleibt (dann schließt das Fenster wie im Original).</summary>
    public bool FilesCloseTab(int? index = null)
    {
        if (_fbTabs.Count <= 1) return false;
        SaveTab(); var k = index ?? _fbTabIx; _fbTabs.RemoveAt(k);
        LoadTab(k < _fbTabIx ? _fbTabIx - 1 : k == _fbTabIx ? Math.Min(k, _fbTabs.Count - 1) : _fbTabIx); Show(FK); return true;
    }
    public void FilesSelectTab(int index) { if (index == _fbTabIx || index < 0 || index >= _fbTabs.Count) return; SaveTab(); LoadTab(index); Show(FK); }
    (List<string> Paths, bool Move)? _fbClip;
    public string FilesView { get; set; } = "";
    string _fbSort = "name"; bool _fbDesc; string _fbQuery = ""; bool _fbRenaming;
    /// <summary>GNOME: Suche ist hinter der Lupe versteckt und geht beim Lostippen auf.</summary>
    public bool FilesSearchOpen { get; private set; }
    string _fbTypeAhead = ""; DateTime _fbTypeAt;
    public string FilesMessage { get; private set; } = "";
    /// <summary>Für Tests: statt das Programm des Systems zu starten.</summary>
    public Action<string>? FilesOpenOverride { get; set; }

    private void InitDateien()
    {
        _showMap[FK] = DateienView; PageModules[FK] = "CORE-01"; // Dateimanager gehört zur Desktop-Oberfläche
        if (!ExtraPages.Any(p => p.Key == FK)) ExtraPages.Add(("Dateien", FK, ["dateien", "explorer", "finder", "ordner", "dieser pc", "arbeitsplatz", "laufwerk", "dokumente", "downloads", "nautilus"]));
        if (_opt.Isolated) { Directory.CreateDirectory(Files.Root!); foreach (var k in Files.KnownFolders()) Directory.CreateDirectory(k.Path); }
    }

    public static string FilesTitle => _style switch { "mac" => "Finder", "linux" => "Dateien", "windows" => "Datei-Explorer", _ => "Dateien" };
    string PlaceName(string dir) => dir == PcPlace ? (_style switch { "mac" => "Orte", "linux" => "Andere Orte", _ => "Dieser PC" }) : Files.KnownFolders().FirstOrDefault(k => SamePath(k.Path, dir)).Label ?? (SamePath(dir, Files.Home) ? (_style == "linux" ? "Persönlicher Ordner" : Path.GetFileName(dir.TrimEnd('\\', '/'))) : Path.GetFileName(dir.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : dir);
    static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    // ---------- Bedienung (öffentlich für Tests und die KI) ----------
    public void FilesGo(string dir, bool history = true)
    {
        if (dir != PcPlace && !Directory.Exists(dir)) throw new DirectoryNotFoundException("Ordner nicht gefunden: " + dir);
        if (history && dir != FilesDir) { _fbBack.Add(FilesDir); _fbFwd.Clear(); }
        FilesDir = dir; FilesSelected = null; _fbQuery = ""; _fbRenaming = false; FilesMessage = ""; FilesSearchOpen = false;
        Show(FK);
    }
    public void FilesBack() { if (_fbBack.Count == 0) return; _fbFwd.Add(FilesDir); var d = _fbBack[^1]; _fbBack.RemoveAt(_fbBack.Count - 1); FilesGo(d, false); }
    public void FilesForward() { if (_fbFwd.Count == 0) return; _fbBack.Add(FilesDir); var d = _fbFwd[^1]; _fbFwd.RemoveAt(_fbFwd.Count - 1); FilesGo(d, false); }
    public void FilesUp() { if (FilesDir == PcPlace) return; var p = Directory.GetParent(FilesDir.TrimEnd('\\', '/'))?.FullName; FilesGo(p is null || (Files.Root is not null && !p.StartsWith(Path.GetFullPath(Files.Root), StringComparison.OrdinalIgnoreCase)) ? PcPlace : p); }

    public void FilesOpen(string path)
    {
        if (Directory.Exists(path)) { FilesGo(path); return; }
        if (FilesOpenOverride is { } o) { o(path); return; }
        if (Path.GetExtension(path).Equals(".tdoc", StringComparison.OrdinalIgnoreCase) || _opt.Isolated) { FilesMessage = "Öffnen: " + Path.GetFileName(path); Show(FK); return; }
        _core.Integrations.LaunchDirect(Principal.LocalOwner, path, _core.Audit);
    }

    public string? FilesPreview { get; private set; }
    static OsBehavior.Mods ModsOf(KeyModifiers k) => (k.HasFlag(KeyModifiers.Control) ? OsBehavior.Mods.Ctrl : 0) | (k.HasFlag(KeyModifiers.Shift) ? OsBehavior.Mods.Shift : 0) | (k.HasFlag(KeyModifiers.Alt) ? OsBehavior.Mods.Alt : 0) | (k.HasFlag(KeyModifiers.Meta) ? OsBehavior.Mods.Meta : 0);

    /// <summary>Ablegen eines gezogenen Elements in einem Ordner – verschieben oder kopieren wie im gewählten System (Laufwerk und Zusatztasten).</summary>
    public string? FilesDrop(string source, string targetDir, OsBehavior.Mods mods)
    {
        if (SamePath(Path.GetDirectoryName(Path.GetFullPath(source))!, targetDir) && mods == OsBehavior.Mods.None) return null; // im selben Ordner losgelassen: nichts tun
        var same = string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), Path.GetPathRoot(Path.GetFullPath(targetDir)), StringComparison.OrdinalIgnoreCase);
        var copy = OsBehavior.DropCopies(_style, same, mods, OperatingSystem.IsMacOS());
        string? r = null;
        FilesDo(() => { r = Files.Paste(source, targetDir, !copy); FilesSelected = r; FilesMessage = (copy ? "Kopiert nach „" : "Verschoben nach „") + PlaceName(targetDir) + "“"; });
        return r;
    }

    /// <summary>Taste im Dateimanager – Belegung wie im gewählten System (Finder: Zeilenschalter benennt um, ⌘⌫ Papierkorb; Explorer: F2, Entf …).</summary>
    public bool FilesKey(Key key, KeyModifiers mods)
    {
        if (_fbRenaming) return false;
        if (mods is KeyModifiers.None or KeyModifiers.Shift && key is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9)
        {
            var ch = key >= Key.D0 && key <= Key.D9 ? ((char)('0' + (key - Key.D0))).ToString() : key.ToString().ToLowerInvariant();
            if (_style == "linux") { FilesSearchOpen = true; _fbQuery += ch; Show(FK); FocusSearch(); return true; }
            _fbTypeAhead = (DateTime.Now - _fbTypeAt).TotalSeconds < 1 ? _fbTypeAhead + ch : ch; _fbTypeAt = DateTime.Now;
            if (_fbShown.FirstOrDefault(x => Path.GetFileName(x).StartsWith(_fbTypeAhead, StringComparison.CurrentCultureIgnoreCase)) is { } hit) { FilesSelected = hit; Show(FK); }
            return true;
        }
        var cmd = OsBehavior.FileKey(_style, key.ToString(), ModsOf(mods), OperatingSystem.IsMacOS());
        switch (cmd)
        {
            case "open": foreach (var o in _fbSel.ToList()) { FilesOpen(o); if (Directory.Exists(o)) break; } break;
            case "rename": if (_fbSel.Count != 1) return false; _fbRenaming = true; Show(FK); break;
            case "trash": FilesTrash(); break;
            case "copy": FilesCopy(false); break;
            case "cut": FilesCopy(true); break;
            case "paste": FilesPaste(); break;
            case "move-here": if (_fbClip is { } c) { _fbClip = (c.Paths, true); FilesPaste(); } break;
            case "duplicate": FilesDo(() => { var r = _fbSel.Select(d => Files.Paste(d, CurDir, false)).ToList(); _fbSel.Clear(); _fbSel.AddRange(r); }); break;
            case "selectall": FilesSelectAll(); break;
            case "newtab": FilesNewTab(); break;
            case "closetab": if (!FilesCloseTab() && Windows.Active is { } aw && aw.Page == FK) CloseWindow(aw.Id); break; // letzte Registerkarte: Fenster zu
            case "nexttab": FilesSelectTab((_fbTabIx + 1) % _fbTabs.Count); break;
            case "prevtab": FilesSelectTab((_fbTabIx - 1 + _fbTabs.Count) % _fbTabs.Count); break;
            case "newfolder": FilesNewFolder(); break;
            case "up": FilesUp(); break;
            case "back": FilesBack(); break;
            case "forward": FilesForward(); break;
            case "refresh": Show(FK); break;
            case "hidden": Files.ShowHidden = !Files.ShowHidden; Show(FK); break;
            case "preview": FilesPreview = FilesPreview is null ? FilesSelected : null; Show(FK); break;
            case "info": if (FilesSelected is { } i) { var fi = new FileInfo(i); FilesMessage = Directory.Exists(i) ? $"Ordner · geändert {Directory.GetLastWriteTime(i):dd.MM.yyyy HH:mm}" : $"{FileBrowser.Kind(fi.Name, false)} · {FileBrowser.SizeText(fi.Length)} · geändert {fi.LastWriteTime:dd.MM.yyyy HH:mm}"; Show(FK); } break;
            case "search" or "location": if (_style == "linux" && !FilesSearchOpen) { FilesSearchOpen = true; Show(FK); } FocusSearch(); break;
            default: return false;
        }
        return true;
    }

    void FocusSearch() { if (ActivePageRoot?.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "fb-search") is { } tb) { tb.Focus(); tb.CaretIndex = (tb.Text ?? "").Length; } }

    void FilesDo(Action a) { try { a(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { FilesMessage = e.Message; } Show(FK); }
    string CurDir => FilesDir == PcPlace ? Files.Home : FilesDir;
    public string FilesNewFolder() { string p = ""; FilesDo(() => { p = Files.NewFolder(CurDir); FilesSelected = p; _fbRenaming = true; }); return p; }
    public void FilesRename(string newName) { if (_fbSel.Count != 1 || FilesSelected is not { } s) return; FilesDo(() => { FilesSelected = Files.Rename(s, newName); _fbRenaming = false; }); }
    static string Count(int n) => n == 1 ? "1 Element" : $"{n} Elemente";
    public void FilesCopy(bool cut) { if (_fbSel.Count == 0) return; _fbClip = ([.. _fbSel], cut); FilesMessage = (cut ? "Ausgeschnitten: " : "Kopiert: ") + (_fbSel.Count == 1 ? Path.GetFileName(_fbSel[0]) : Count(_fbSel.Count)); Show(FK); }
    public void FilesPaste()
    {
        if (_fbClip is not { } c) return;
        FilesDo(() => { var r = new List<string>(); foreach (var p in c.Paths) r.Add(Files.Paste(p, CurDir, c.Move)); _fbSel.Clear(); _fbSel.AddRange(r); if (c.Move) _fbClip = null; FilesMessage = Count(r.Count) + " eingefügt"; });
    }
    public void FilesTrash()
    {
        if (_fbSel.Count == 0) return;
        var all = _fbSel.ToList();
        FilesDo(() =>
        {
            foreach (var s in all) Files.Trash(s, OperatingSystem.IsWindows() ? p =>
            {
                if (Directory.Exists(p)) Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(p, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                else Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(p, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            } : null);
            FilesMessage = all.Count == 1 ? $"„{Path.GetFileName(all[0])}“ liegt im Papierkorb." : $"{all.Count} Elemente liegen im Papierkorb."; FilesSelected = null;
        });
    }

    // ---------- Symbole (eigene Zeichnungen) ----------
    const string FolderPath = "M2 6 C2 4.9 2.9 4 4 4 H9.5 L11.5 6 H20 C21.1 6 22 6.9 22 8 V18 C22 19.1 21.1 20 20 20 H4 C2.9 20 2 19.1 2 18 Z";
    const string FilePath = "M6 2 H14 L20 8 V20 C20 21.1 19.1 22 18 22 H6 C4.9 22 4 21.1 4 20 V4 C4 2.9 4.9 2 6 2 Z";
    const string DrivePath = "M4 7 H20 C21.1 7 22 7.9 22 9 V15 C22 16.1 21.1 17 20 17 H4 C2.9 17 2 16.1 2 15 V9 C2 7.9 2.9 7 4 7 Z";
    static Control FolderIcon(double s) => Ico(FolderPath, s, _style switch { "mac" => OsGrad("#8FD3FF", "#3A9BE8"), "linux" => OsGrad("#99C1F1", "#3584E4"), _ => Grad("#FFE38A", "#F2B33D") });
    static Control FileIcon(string name, double s)
    {
        var k = FileBrowser.Kind(name, false);
        var tint = k.StartsWith("Bild", StringComparison.Ordinal) ? "#4CAF50" : k.StartsWith("PDF", StringComparison.Ordinal) ? "#E53935" : k.StartsWith("Audio", StringComparison.Ordinal) ? "#AB47BC" : k.StartsWith("Video", StringComparison.Ordinal) ? "#FB8C00" : k == "Tabelle" ? "#2E7D32" : k == "Archiv" ? "#8D6E63" : "#1E88E5";
        return new Grid { Width = s, Height = s, Children = {
            new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(FilePath), Fill = Brush.Parse("#FFF7F7F7"), Stroke = Brush.Parse("#33000000"), StrokeThickness = 0.6, Stretch = Stretch.Uniform, Width = s, Height = s },
            new Border { Height = s * 0.16, Width = s * 0.5, CornerRadius = new CornerRadius(s * 0.03), Background = Brush.Parse(tint), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, s * 0.2) } } };
    }
    static Control ItemIcon(FileBrowser.Item i, double s) => i.IsDir ? FolderIcon(s) : FileIcon(i.Name, s);
    static Control DriveIcon(double s) => new Grid { Width = s, Height = s, Children = { Ico(DrivePath, s, Grad("#CFD8DC", "#78909C")), new Border { Width = s * 0.1, Height = s * 0.1, CornerRadius = new CornerRadius(s), Background = Brush.Parse("#4CAF50"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, s * 0.16, 0) } } };

    // ---------- Ansicht ----------
    const string DragPrefix = "trendo-datei:";
    const char NL = (char)10;
    /// <summary>Ablageziel: Elemente aus diesem Dateimanager (verschieben/kopieren je System) und Dateien von außen.</summary>
    void DropTarget(Control c, string dir)
    {
        DragDrop.SetAllowDrop(c, true);
        c.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            if (e.DataTransfer.TryGetText() is { } t && t.StartsWith(DragPrefix, StringComparison.Ordinal))
            {
                var src = t[DragPrefix.Length..].Split(NL)[0]; var same = string.Equals(Path.GetPathRoot(src), Path.GetPathRoot(dir), StringComparison.OrdinalIgnoreCase);
                e.DragEffects = OsBehavior.DropCopies(_style, same, ModsOf(e.KeyModifiers), OperatingSystem.IsMacOS()) ? DragDropEffects.Copy : DragDropEffects.Move; // Zeiger zeigt „+“ beim Kopieren
            }
            else e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        });
        c.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.DataTransfer.TryGetText() is { } t && t.StartsWith(DragPrefix, StringComparison.Ordinal))
            {
                var res = new List<string>();
                foreach (var src in t[DragPrefix.Length..].Split(NL, StringSplitOptions.RemoveEmptyEntries)) if (!SamePath(src, dir) && FilesDrop(src, dir, ModsOf(e.KeyModifiers)) is { } r) res.Add(r);
                if (res.Count > 1) { _fbSel.Clear(); _fbSel.AddRange(res); Show(FK); }
                e.Handled = true; return;
            }
            foreach (var f in e.DataTransfer.TryGetFiles()?.Select(x => x.TryGetLocalPath()).OfType<string>() ?? []) FilesDrop(f, dir, ModsOf(e.KeyModifiers));
            e.Handled = true;
        });
    }

    readonly Dictionary<string, Button> _fbButtons = [];
    private Control DateienView()
    {
        _fbButtons.Clear();
        // GNOME/Finder: beim allerersten Aufbau gibt es das Fenster noch nicht – danach einmal neu zeichnen, damit die Fensterknöpfe in die Kopfleiste kommen
        if (_style is "mac" or "linux" or "windows" && Windows.Active is not { Page: FK }) _ui.Post(() => { if (Windows.Active is { Page: FK } fa && _titleBars.TryGetValue(fa.Id, out var tb) && !tb.IsVisible) Show(FK); }, Avalonia.Threading.DispatcherPriority.Background);
        Files.Style = _style;
        var view = FilesView.Length > 0 ? FilesView : _style is "mac" or "linux" ? "symbole" : "liste";
        var light = _theme == "hell";
        IReadOnlyList<FileBrowser.Item> items = [];
        if (FilesDir != PcPlace)
        {
            try { items = Files.List(FilesDir, _fbSort, _fbDesc); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { FilesMessage = e.Message; }
            if (_fbQuery.Length > 0) items = [.. items.Where(i => i.Name.Contains(_fbQuery, StringComparison.CurrentCultureIgnoreCase))];
        }
        Button Tb(string name, Control c, string tip, Action a, bool enabled = true, double w = 32)
        {
            var b = new Button { Name = name, Content = c, Width = w, Height = 32, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(_style == "linux" ? 8 : _style == "mac" ? 6 : 4),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsEnabled = enabled, Foreground = Text };
            foreach (var k in new[] { "ButtonBackgroundDisabled", "ButtonBorderBrushDisabled" }) b.Resources[k] = Brushes.Transparent; // deaktiviert: nur blasses Zeichen wie im Original
            ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, tip); b.Click += (_, _) => a(); return b;
        }
        TextBlock G(string s, double size = 15) => new() { Text = s, FontSize = size, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var back = Tb("fb-back", G("←"), "Zurück", FilesBack, _fbBack.Count > 0);
        var fwd = Tb("fb-forward", G("→"), "Vor", FilesForward, _fbFwd.Count > 0);
        var up = Tb("fb-up", G("↑"), "Übergeordneter Ordner", FilesUp, FilesDir != PcPlace);
        var refresh = Tb("fb-refresh", G("⟳"), "Aktualisieren", () => Show(FK));
        var search = new TextBox { Name = "fb-search", Text = _fbQuery, PlaceholderText = (_style == "mac" ? "Suchen" : _style == "linux" ? "Dateien suchen" : $"{PlaceName(FilesDir)} durchsuchen"), Width = 220, Height = 32, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 12.5 };
        search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _fbQuery = search.Text ?? ""; Show(FK); FocusSearch(); } else if (e.Key == Key.Escape && _style == "linux") { FilesSearchOpen = false; _fbQuery = ""; Show(FK); } };
        if (_style == "linux") search.TextChanged += (_, _) => { if ((search.Text ?? "") != _fbQuery) { _fbQuery = search.Text ?? ""; Show(FK); FocusSearch(); } };

        // Adresse: Brotkrumen (Windows), Pfadknöpfe in einer Pille (GNOME), Ordnername als Titel (Mac)
        var crumbs = new StackPanel { Name = "fb-crumbs", Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        void Crumb(string label, string target, bool last)
        {
            var b = new Button { Content = label, FontSize = 12.5, Padding = new Thickness(6, 2), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Text, FontWeight = last && _style == "linux" ? FontWeight.Bold : FontWeight.Normal };
            b.Click += (_, _) => FilesGo(target); crumbs.Children.Add(b);
            if (!last) crumbs.Children.Add(new TextBlock { Text = _style == "linux" ? "/" : "›", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0) });
        }
        Crumb(PlaceName(PcPlace), PcPlace, FilesDir == PcPlace);
        if (FilesDir != PcPlace) { var cs = Files.Crumbs(FilesDir); for (var i = 0; i < cs.Count; i++) Crumb(i == 0 && cs.Count > 1 && Files.Root is not null ? "Lokaler Datenträger" : cs[i].Label, cs[i].Path, i == cs.Count - 1); }

        // Befehle (Windows-Befehlsleiste; bei Mac/GNOME im Menü „⋯“ bzw. „☰“ und per Rechtsklick)
        var sel = _fbSel.Count > 0;
        _fbShown = [.. items.Select(i => i.Path)];
        var newBtn = Tb("fb-new-folder", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { G("＋", 14), new TextBlock { Text = "Neu", FontSize = 12.5, Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } }, "Neuer Ordner", () => FilesNewFolder(), FilesDir != PcPlace, 70);
        var cut = Tb("fb-cut", G("✂", 14), "Ausschneiden", () => FilesCopy(true), sel);
        var copy = Tb("fb-copy", G("⧉", 14), "Kopieren", () => FilesCopy(false), sel);
        var paste = Tb("fb-paste", G("📋", 13), "Einfügen", FilesPaste, _fbClip is not null && FilesDir != PcPlace);
        var ren = Tb("fb-rename", G("✎", 14), "Umbenennen", () => { _fbRenaming = true; Show(FK); }, _fbSel.Count == 1);
        var del = Tb("fb-delete", G("🗑", 13), "Löschen (in den Papierkorb)", FilesTrash, sel);
        var sortBox = new ComboBox { Name = "fb-sort", ItemsSource = new[] { "Name", "Änderungsdatum", "Typ", "Größe" }, SelectedIndex = Array.IndexOf(new[] { "name", "datum", "typ", "groesse" }, _fbSort), Height = 32, FontSize = 12 };
        sortBox.SelectionChanged += (_, _) => { var v = new[] { "name", "datum", "typ", "groesse" }[Math.Max(0, sortBox.SelectedIndex)]; if (v != _fbSort) { _fbSort = v; Show(FK); } };
        var viewBtn = Tb("fb-view", G(view == "liste" ? "▦" : "☰", 14), view == "liste" ? "Ansicht: Symbole" : "Ansicht: Liste", () => { FilesView = view == "liste" ? "symbole" : "liste"; Show(FK); });
        var hidden = new CheckBox { Name = "fb-hidden", Content = "Ausgeblendete Elemente", IsChecked = Files.ShowHidden, FontSize = 12, Foreground = Text, VerticalAlignment = VerticalAlignment.Center };
        hidden.IsCheckedChanged += (_, _) => { if (Files.ShowHidden != (hidden.IsChecked == true)) { Files.ShowHidden = hidden.IsChecked == true; Show(FK); } };
        var sysOpen = Tb("fb-system", G("⇱", 14), "Im Dateimanager des Systems öffnen", () => { if (!_opt.Isolated) _core.Integrations.LaunchDirect(Principal.LocalOwner, CurDir, _core.Audit); }, true);

        // Seitenleiste
        var side = new StackPanel { Name = "fb-side", Spacing = 1 };
        Button Side(string name, Control icon, string label, string target)
        {
            var on = target == FilesDir || (target != PcPlace && FilesDir != PcPlace && SamePath(target, FilesDir));
            var b = new Button { Name = "fb-side-" + name, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5), CornerRadius = new CornerRadius(_style == "windows" ? 4 : 6), BorderThickness = new Thickness(0),
                Background = on ? Brush.Parse(_style == "mac" ? (light ? "#1A000000" : "#26FFFFFF") : _style == "linux" ? (light ? "#1A000000" : "#1AFFFFFF") : (light ? "#0F000000" : "#0FFFFFFF")) : Brushes.Transparent,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { icon, new TextBlock { Text = label, FontSize = 13, Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } } };
            b.Click += (_, _) => FilesGo(target); if (target != PcPlace) DropTarget(b, target); return b;
        }
        TextBlock Head(string s) => new() { Text = s, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Muted, Margin = new Thickness(8, 10, 0, 4) };
        Control Sep() => new Border { Height = 1, Background = Edge, Margin = new Thickness(8, 6) };
        Control SmallFolder() => _style == "mac" ? Ico(FolderPath, 15, Brush.Parse("#0A84FF")) : _style == "linux" ? Ico(FolderPath, 15, Text) : FolderIcon(16);
        var known = Files.KnownFolders();
        switch (_style)
        {
            case "mac":
                side.Children.Add(Head("Favoriten"));
                side.Children.Add(Side("home", Ico(Mdi.Home, 15, Brush.Parse("#0A84FF")), Path.GetFileName(Files.Home.TrimEnd('\\', '/')) is { Length: > 0 } hn ? hn : "Benutzer", Files.Home));
                foreach (var k in known) side.Children.Add(Side(k.Id, SmallFolder(), k.Id == "desktop" ? "Schreibtisch" : k.Label, k.Path));
                side.Children.Add(Head("Orte"));
                foreach (var dr in Files.Drives()) side.Children.Add(Side("drive-" + side.Children.Count, Ico(DrivePath, 15, Brush.Parse("#8E8E93")), dr.Label, dr.Path));
                side.Children.Add(Side("pc", Ico(Mdi.Monitor, 15, Brush.Parse("#8E8E93")), "Orte", PcPlace));
                break;
            case "linux":
                side.Children.Add(Side("home", Ico(Mdi.Home, 15, Text), "Persönlicher Ordner", Files.Home));
                foreach (var k in known.Where(k => k.Id != "desktop")) side.Children.Add(Side(k.Id, SmallFolder(), k.Label, k.Path));
                side.Children.Add(Sep());
                side.Children.Add(Side("pc", Ico("M5 5 H19 V19 H5 Z M8 8 V16 H16 V8 Z", 15, Text), "Andere Orte", PcPlace));
                break;
            default:
                side.Children.Add(Side("home", Ico(Mdi.Home, 16, Brush.Parse("#4CC2FF")), "Start", Files.Home));
                side.Children.Add(Sep());
                foreach (var k in known) side.Children.Add(Side(k.Id, FolderIcon(16), k.Label, k.Path));
                side.Children.Add(Sep());
                side.Children.Add(Side("pc", Ico(Mdi.Monitor, 16, Brush.Parse("#4CC2FF")), "Dieser PC", PcPlace));
                foreach (var dr in Files.Drives()) side.Children.Add(Side("drive-" + side.Children.Count, DriveIcon(16), $"{dr.Label} ({dr.Path.TrimEnd('\\', '/')})", dr.Path));
                break;
        }

        // Inhalt
        Control content;
        if (FilesDir == PcPlace)
        {
            var wrap = new WrapPanel { Name = "fb-drives" };
            foreach (var dr in Files.Drives())
            {
                var used = dr.Total <= 0 ? 0 : 1 - dr.Free / (double)dr.Total;
                var tile = new Button { Name = "fb-drive-" + wrap.Children.Count, Width = 260, Height = 70, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(10, 6), Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(4), HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { DriveIcon(40), new StackPanel { Width = 190, Spacing = 3, Children = {
                        new TextBlock { Text = _style == "windows" || _style == "trendo" ? $"{dr.Label} ({dr.Path.TrimEnd('\\', '/')})" : dr.Label, FontSize = 12.5, Foreground = Text, TextTrimming = TextTrimming.CharacterEllipsis },
                        new ProgressBar { Value = used * 100, Height = 12, MinHeight = 12, Foreground = used > 0.9 ? Brush.Parse("#C42B1C") : Accent, Background = Brush.Parse(light ? "#E6E6E6" : "#3A3A3A") },
                        new TextBlock { Text = $"{FileBrowser.SizeText(dr.Free)} frei von {FileBrowser.SizeText(dr.Total)}", FontSize = 11, Foreground = Muted } } } } } };
                var p = dr.Path; tile.DoubleTapped += (_, _) => FilesGo(p); tile.Click += (_, _) => { if (_style is "mac" or "linux") FilesGo(p); };
                wrap.Children.Add(tile);
            }
            var fold = new WrapPanel { Name = "fb-pc-folders" };
            foreach (var k in known)
            {
                var kp = k.Path; var b = new Button { Name = "fb-pc-" + k.Id, Width = 260, Height = 56, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(10, 4), Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { FolderIcon(36), new TextBlock { Text = k.Label, FontSize = 12.5, Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } } };
                b.DoubleTapped += (_, _) => FilesGo(kp); b.Click += (_, _) => { if (_style is "mac" or "linux") FilesGo(kp); };
                fold.Children.Add(b);
            }
            TextBlock Sec(string t) => new() { Text = t, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 8, 0, 8) };
            content = _style switch
            {
                // GNOME „Andere Orte“: Auf diesem Rechner, Netzwerke, unten „Mit Server verbinden“
                "linux" => new StackPanel { Margin = new Thickness(12), Children = { Sec("Auf diesem Rechner"), wrap, Sec("Netzwerke"),
                    new TextBlock { Name = "fb-no-network", Text = "Keine Netzwerkorte gefunden", Foreground = Muted, FontSize = 12.5, Margin = new Thickness(4, 0, 0, 12) },
                    new Border { BorderBrush = Edge, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 0), Child = new TextBlock { Text = "Mit Server verbinden: smb://, sftp://, ftp:// – Adresse in der Pfadleiste", Foreground = Muted, FontSize = 12 } } } },
                // Finder „Orte“ (Computer): nur Volumes
                "mac" => new StackPanel { Margin = new Thickness(12), Children = { wrap } },
                // Windows „Dieser PC“
                _ => new StackPanel { Margin = new Thickness(12), Children = { Sec("Ordner"), fold, Sec("Geräte und Laufwerke"), wrap } },
            };
        }
        else if (view == "liste")
        {
            var list = new StackPanel { Name = "fb-list" };
            var cols = new ColumnDefinitions("*,160,150,90");
            Grid Row(params Control[] cells) { var g = new Grid { ColumnDefinitions = new ColumnDefinitions(cols.ToString()) }; for (var i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); g.Children.Add(cells[i]); } return g; }
            TextBlock C(string s, bool muted = false) => new() { Text = s, FontSize = 12.5, Foreground = muted ? Muted : Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(6, 0) };
            Button HeadCol(string label, string key) { var b = new Button { Name = "fb-col-" + key, Content = label + (_fbSort == key ? (_fbDesc ? " ˅" : " ˄") : ""), FontSize = 12, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                b.Click += (_, _) => { if (_fbSort == key) _fbDesc = !_fbDesc; else { _fbSort = key; _fbDesc = false; } Show(FK); }; return b; }
            list.Children.Add(new Border { BorderBrush = Edge, BorderThickness = new Thickness(0, 0, 0, 1), Child = Row(HeadCol("Name", "name"), HeadCol(_style == "mac" ? "Änderungsdatum" : "Änderungsdatum", "datum"), HeadCol(_style == "mac" ? "Art" : "Typ", "typ"), HeadCol("Größe", "groesse")) });
            var even = false;
            foreach (var it in items)
            {
                var on = IsSel(it.Path);
                Control name = on && _fbRenaming && _fbSel.Count == 1 ? RenameBox(it) : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(6, 0), Children = { ItemIcon(it, 18), new TextBlock { Text = it.Name, FontSize = 12.5, Foreground = on ? Brushes.White : Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis } } };
                var row = Row(name, C(it.Modified.ToString("dd.MM.yyyy HH:mm", OsDe), true), C(it.Kind, true), C(it.IsDir ? (_style == "mac" ? "--" : "") : FileBrowser.SizeText(it.Size), true));
                var bg = on ? Accent : _style == "mac" && even ? Brush.Parse(light ? "#0A000000" : "#0DFFFFFF") : Brushes.Transparent; even = !even;
                list.Children.Add(ItemButton(it, row, bg, 30, double.NaN));
            }
            content = new ScrollViewer { Content = list, Margin = new Thickness(6, 4) };
        }
        else
        {
            var wrap = new WrapPanel { Name = "fb-icons", Margin = new Thickness(10) };
            foreach (var it in items)
            {
                var on = IsSel(it.Path);
                var label = on && _fbRenaming && _fbSel.Count == 1 ? (Control)RenameBox(it) : new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 1), Background = on && _style == "mac" ? Accent : Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new TextBlock { Text = it.Name, FontSize = 12, Foreground = on && _style == "mac" ? Brushes.White : Text, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 100, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis } };
                var face = new StackPanel { Spacing = 4, Children = { new Border { HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(4), CornerRadius = new CornerRadius(6), Background = on && _style != "mac" ? Brush.Parse(light ? "#330067C0" : "#4D4CC2FF") : on ? Brush.Parse(light ? "#1A000000" : "#26FFFFFF") : Brushes.Transparent, Child = ItemIcon(it, 56) }, label } };
                wrap.Children.Add(ItemButton(it, face, Brushes.Transparent, 104, 112));
            }
            content = new ScrollViewer { Content = wrap };
        }
        if (FilesDir != PcPlace && items.Count == 0)
            content = new TextBlock { Name = "fb-empty", Text = _fbQuery.Length > 0 ? "Keine Treffer." : _style == "linux" ? "Ordner ist leer" : "Dieser Ordner ist leer.", Foreground = Muted, FontSize = _style == "linux" ? 18 : 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var areaDir = CurDir;
        var band = new Border { Name = "fb-band", IsVisible = false, IsHitTestVisible = false, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(_style == "windows" ? 0 : 2),
            BorderBrush = Accent, Background = new SolidColorBrush(Accent is ISolidColorBrush sb ? sb.Color : Colors.DodgerBlue, 0.25) };
        var bandHost = new Canvas { IsHitTestVisible = false, Children = { band } };
        var layer = new Grid { Children = { content, bandHost } };
        content = layer;
        Point? bandFrom = null; var bandAdd = false;
        layer.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Source is Visual v && v.FindAncestorOfType<Button>(true) is not null) return; // auf einem Element: Klick/Ziehen des Elements
            if (!e.GetCurrentPoint(layer).Properties.IsLeftButtonPressed || FilesDir == PcPlace) return;
            bandFrom = e.GetPosition(layer); bandAdd = _style == "mac" ? OsBehavior.Cmd(ModsOf(e.KeyModifiers), OperatingSystem.IsMacOS()) : e.KeyModifiers.HasFlag(KeyModifiers.Control);
            e.Pointer.Capture(layer); e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        layer.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (bandFrom is not { } f) return;
            var r = new Rect(f, e.GetPosition(layer)).Normalize();
            Canvas.SetLeft(band, r.X); Canvas.SetTop(band, r.Y); band.Width = r.Width; band.Height = r.Height; band.IsVisible = r.Width > 3 || r.Height > 3;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        layer.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (bandFrom is not { } f) return;
            bandFrom = null; e.Pointer.Capture(null);
            var r = new Rect(f, e.GetPosition(layer)).Normalize(); band.IsVisible = false;
            if (r.Width < 4 && r.Height < 4) { if (!bandAdd && _fbSel.Count > 0) { FilesSelected = null; Show(FK); } return; } // Klick ins Leere hebt die Auswahl auf
            var hit = new List<string>();
            foreach (var (path, btn) in _fbButtons)
                if (btn.TranslatePoint(new Point(0, 0), layer) is { } tl && new Rect(tl, btn.Bounds.Size).Intersects(r)) hit.Add(path);
            FilesBand(hit, bandAdd);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var area = new Border { Name = "fb-area", Background = Brush.Parse(_style switch { "mac" => light ? "#FFFFFFFF" : "#FF1E1E1E", "linux" => light ? "#FFFFFFFF" : "#FF1D1D20", "windows" => light ? "#FFF9F9F9" : "#FF191919", _ => "#00000000" }), Child = content };
        if (FilesDir != PcPlace) DropTarget(area, areaDir);
        area.ContextMenu = new ContextMenu { Items = { Mi("Neuer Ordner", () => FilesNewFolder()), Mi("Einfügen", FilesPaste), Mi("Aktualisieren", () => Show(FK)), Mi(view == "liste" ? "Als Symbole" : "Als Liste", () => { FilesView = view == "liste" ? "symbole" : "liste"; Show(FK); }) } };

        var count = FilesDir == PcPlace ? $"{Files.Drives().Count} Laufwerke" : $"{items.Count} {(items.Count == 1 ? "Element" : "Elemente")}" + (_fbSel.Count == 1 ? " · 1 Element ausgewählt" : _fbSel.Count > 1 ? $" · {_fbSel.Count} Elemente ausgewählt" : "");
        var status = new TextBlock { Name = "fb-status", Text = FilesMessage.Length > 0 ? count + "   ·   " + FilesMessage : count, FontSize = 11.5, Foreground = Muted, Margin = new Thickness(12, 4), VerticalAlignment = VerticalAlignment.Center };

        // Anordnung je System
        var sideBox = new Border { Name = "fb-sidebar", Width = _style == "linux" ? 220 : 200, Padding = new Thickness(6), Child = new ScrollViewer { Content = side },
            Background = Brush.Parse(_style switch { "mac" => light ? "#E6E8E8EA" : "#E62A2A2C", "linux" => light ? "#FFEBEBED" : "#FF2E2E32", "windows" => light ? "#FFF3F3F3" : "#FF202020", _ => "#00000000" }) };
        Control top;
        switch (_style)
        {
            case "mac":
            {
                var title = new TextBlock { Text = PlaceName(FilesDir), FontWeight = FontWeight.Bold, FontSize = 14, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
                var more = Tb("fb-more", G("⋯", 15), "Aktionen", () => { });
                more.Flyout = new MenuFlyout { Items = { Mi("Neuer Ordner", () => FilesNewFolder()), Mi("Umbenennen", () => { _fbRenaming = true; Show(FK); }), Mi("Kopieren", () => FilesCopy(false)), Mi("Ausschneiden", () => FilesCopy(true)), Mi("Einfügen", FilesPaste), Mi("In den Papierkorb legen", FilesTrash), Mi("Im System-Finder zeigen", () => { if (!_opt.Isolated) _core.Integrations.LaunchDirect(Principal.LocalOwner, CurDir, _core.Audit); }) } };
                var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { back, fwd, title } };
                if (WinCaps() is { } mcaps) { var sc = sideBox.Child; sideBox.Child = null; sideBox.Child = new DockPanel { Children = { DockTop(new Border { Height = 38, Child = mcaps }), sc! } }; Draggable(sideBox); } // Finder: Ampel oben in der Seitenleiste
                var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Children = { viewBtn, sortBox, more, del, search } };
                top = new Border { Name = "fb-headerbar", Height = 52, Padding = new Thickness(10, 0), Background = area.Background, BorderBrush = Edge, BorderThickness = new Thickness(0, 0, 0, 1), Child = new Grid { Children = { left, right } } };
                Draggable(top);
                // Pfadleiste unten wie in Finder
                var path = new Border { BorderBrush = Edge, BorderThickness = new Thickness(0, 1, 0, 0), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { crumbs, status } } };
                var mainCol = new DockPanel { Children = { DockTop(top), DockTop(TabBar()), DockBottom(path), area } };
                return Frame(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { sideBox, Pos(mainCol, 1) } });
            }
            case "linux":
            {
                var searchToggle = Tb("fb-search-toggle", Ico(Mdi.Magnify, 15, Text), "Suchen (Strg+F)", () => { FilesSearchOpen = !FilesSearchOpen; if (!FilesSearchOpen) _fbQuery = ""; Show(FK); if (FilesSearchOpen) FocusSearch(); });
                if (FilesSearchOpen || _fbQuery.Length > 0) searchToggle.Background = Brush.Parse(light ? "#1F000000" : "#26FFFFFF");
                Control SearchPill() { search.Width = double.NaN; search.Height = 34; search.CornerRadius = new CornerRadius(8); return search; }
                var pathPill = new Border { CornerRadius = new CornerRadius(8), Background = Brush.Parse(light ? "#14000000" : "#1AFFFFFF"), Padding = new Thickness(4, 0), Height = 34, Child = crumbs, VerticalAlignment = VerticalAlignment.Center };
                var menu = Tb("fb-more", G("☰", 15), "Hauptmenü", () => { });
                menu.Flyout = new MenuFlyout { Items = { Mi("Neuer Ordner", () => FilesNewFolder()), Mi("Umbenennen …", () => { _fbRenaming = true; Show(FK); }), Mi("Kopieren", () => FilesCopy(false)), Mi("Ausschneiden", () => FilesCopy(true)), Mi("Einfügen", FilesPaste), Mi("In den Papierkorb verschieben", FilesTrash), Mi("Verborgene Dateien anzeigen", () => { Files.ShowHidden = !Files.ShowHidden; Show(FK); }) } };
                var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { back, fwd } },
                    Pos(new Border { Margin = new Thickness(8, 0), Child = FilesSearchOpen || _fbQuery.Length > 0 ? SearchPill() : pathPill, HorizontalAlignment = HorizontalAlignment.Stretch }, 1),
                    Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { searchToggle, viewBtn, sortBox, menu } }, 2) } };
                if (WinCaps() is { } gcaps) { Grid.SetColumn(gcaps, 3); head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); gcaps.Margin = new Thickness(8, 0, 4, 0); head.Children.Add(gcaps); }
                top = new Border { Name = "fb-headerbar", Height = 47, Padding = new Thickness(6, 0), Background = Brush.Parse(light ? "#FFFFFFFF" : "#FF222226"), Child = head };
                Draggable(top); Draggable(sideBox);
                var mainCol = new DockPanel { Children = { DockTop(top), DockTop(TabBar()), DockBottom(status), area } };
                return Frame(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { sideBox, Pos(mainCol, 1) } });
            }
            default: // Windows 11 Datei-Explorer (auch für den Trendo-Stil)
            {
                var addr = new Border { Height = 32, CornerRadius = new CornerRadius(4), Background = Brush.Parse(light ? "#FFFFFFFF" : "#FF2D2D2D"), BorderBrush = Edge, BorderThickness = new Thickness(1), Padding = new Thickness(6, 0), Margin = new Thickness(6, 0), Child = crumbs };
                var addrRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(6, 6, 6, 4), Children = {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { back, fwd, up, refresh } }, Pos(addr, 1), Pos(search, 2) } };
                var cmd = new StackPanel { Name = "fb-commands", Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(10, 2, 10, 6), Children = {
                    newBtn, new Border { Width = 1, Height = 22, Background = Edge, Margin = new Thickness(6, 0) }, cut, copy, paste, ren, del,
                    new Border { Width = 1, Height = 22, Background = Edge, Margin = new Thickness(6, 0) }, sortBox, viewBtn, hidden, sysOpen } };
                top = new StackPanel { Background = Brush.Parse(_style == "windows" ? (light ? "#FFF3F3F3" : "#FF202020") : "#00000000"), Children = { addrRow, new Border { BorderBrush = Edge, BorderThickness = new Thickness(0, 1, 0, 1), Child = cmd } } };
                var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { sideBox, Pos(area, 1) } };
                return Frame(new DockPanel { Children = { DockTop(TabBar()), DockTop(top), DockBottom(status), body } });
            }
        }

        Control RenameBox(FileBrowser.Item it)
        {
            var tb = new TextBox { Name = "fb-rename-box", Text = it.Name, FontSize = 12.5, MinWidth = 160 };
            tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; FilesRename(tb.Text ?? ""); } else if (e.Key == Key.Escape) { _fbRenaming = false; Show(FK); } };
            tb.AttachedToVisualTree += (_, _) => { tb.Focus(); tb.SelectAll(); };
            return tb;
        }
        Button ItemButton(FileBrowser.Item it, Control content2, IBrush bg, double h, double w)
        {
            var b = new Button { Name = "fb-item-" + it.Name, Content = content2, Height = h, Width = w, Padding = new Thickness(0), Margin = new Thickness(w > 0 ? 2 : 0, 1), Background = bg, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(4),
                HorizontalAlignment = double.IsNaN(w) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = double.IsNaN(w) ? VerticalAlignment.Center : VerticalAlignment.Top };
            Avalonia.Automation.AutomationProperties.SetName(b, it.Name);
            var p = it.Path;
            _fbButtons[p] = b;
            var clickMods = KeyModifiers.None;
            b.Click += (_, _) => { if (clickMods == KeyModifiers.None && _fbSel.Count == 1 && IsSel(p)) return; FilesClick(p, clickMods); };
            b.DoubleTapped += (_, _) => FilesOpen(p);
            // Ziehen startet nach ein paar Bildpunkten Bewegung (wie überall) – Ordner nehmen Abgelegtes an
            PointerPressedEventArgs? pressed = null; Point start = default;
            if (it.IsDir) b.AddHandler(PointerPressedEvent, (_, e) => { if (e.GetCurrentPoint(b).Properties.IsMiddleButtonPressed) { e.Handled = true; FilesNewTab(p); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel); // Mittelklick: in neuer Registerkarte
            b.AddHandler(PointerPressedEvent, (_, e) => { clickMods = e.KeyModifiers; if (e.GetCurrentPoint(b).Properties.IsLeftButtonPressed) { pressed = e; start = e.GetPosition(b); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.AddHandler(PointerReleasedEvent, (_, _) => pressed = null, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.AddHandler(PointerMovedEvent, async (_, e) =>
            {
                if (pressed is not { } pe || (Math.Abs(e.GetPosition(b).X - start.X) < 5 && Math.Abs(e.GetPosition(b).Y - start.Y) < 5)) return;
                pressed = null; var dt = new DataTransfer();
                var drag = IsSel(p) ? _fbSel.ToList() : [p]; // gezogen wird die ganze Auswahl, wenn das Element dazugehört
                dt.Add(DataTransferItem.CreateText(DragPrefix + string.Join(NL, drag)));
                await DragDrop.DoDragDropAsync(pe, dt, DragDropEffects.Copy | DragDropEffects.Move);
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            if (it.IsDir) DropTarget(b, p);
            var tabItem = Mi(_style == "mac" ? "In neuem Tab öffnen" : _style == "linux" ? "In neuem Reiter öffnen" : "In neuer Registerkarte öffnen", () => FilesNewTab(p)); tabItem.IsVisible = it.IsDir;
            b.ContextMenu = new ContextMenu { Items = { Mi("Öffnen", () => FilesOpen(p)), tabItem, Mi("Ausschneiden", () => { if (!IsSel(p)) FilesSelected = p; FilesCopy(true); }), Mi("Kopieren", () => { if (!IsSel(p)) FilesSelected = p; FilesCopy(false); }),
                Mi("Umbenennen", () => { FilesSelected = p; _fbRenaming = true; Show(FK); }), Mi(_style == "mac" ? "In den Papierkorb legen" : _style == "linux" ? "In den Papierkorb verschieben" : "Löschen", () => { if (!IsSel(p)) FilesSelected = p; FilesTrash(); }) } };
            return b;
        }
        static MenuItem Mi(string header, Action a) { var m = new MenuItem { Header = header }; m.Click += (_, _) => a(); return m; }

        // Registerkartenleiste: Windows immer im Kopf (Karte mit runden oberen Ecken, „+“); Finder und GNOME erst ab zwei Tabs, gleich breit unter der Werkzeugleiste
        Control TabBar()
        {
            var win = _style is "windows" or "trendo";
            var bar = new StackPanel { Name = "fb-tabs", Orientation = Orientation.Horizontal, Spacing = win ? 2 : 4 };
            var titles = FilesTabTitles;
            for (var i = 0; i < titles.Count; i++)
            {
                var ix = i; var on = i == _fbTabIx;
                var close = new Button { Name = "fb-tab-close-" + i, Content = "✕", FontSize = 10, Width = 20, Height = 20, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(win ? 4 : 10), Foreground = Muted,
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsVisible = titles.Count > 1 || win };
                ToolTip.SetTip(close, "Schließen"); close.Click += (_, _) => { if (!FilesCloseTab(ix) && Windows.Active is { } aw) CloseWindow(aw.Id); };
                var label = new TextBlock { Text = titles[i], FontSize = 12, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = win ? 170 : 220 };
                Control inner = _style == "mac"
                    ? new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { close, Pos(new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = label }, 1) } }
                    : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { win ? FolderIcon(14) : new Border(), label, close } };
                var tab = new Button { Name = "fb-tab-" + i, Content = inner, Height = win ? 34 : 30, Padding = new Thickness(win ? 10 : 8, 0), BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = win ? new CornerRadius(8, 8, 0, 0) : new CornerRadius(_style == "linux" ? 8 : 6), MinWidth = win ? 180 : 160,
                    Background = on ? Brush.Parse(win ? (light ? "#FFF9F9F9" : "#FF2C2C2C") : (light ? "#1A000000" : "#1FFFFFFF")) : Brushes.Transparent };
                tab.Click += (_, _) => FilesSelectTab(ix);
                tab.AddHandler(PointerPressedEvent, (_, e) => { if (e.GetCurrentPoint(tab).Properties.IsMiddleButtonPressed) { e.Handled = true; FilesCloseTab(ix); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel); // Mittelklick schließt
                Avalonia.Automation.AutomationProperties.SetName(tab, titles[i]);
                bar.Children.Add(tab);
            }
            var plus = new Button { Name = "fb-tab-new", Content = "＋", FontSize = 13, Width = 30, Height = 28, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(4), Foreground = Text,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(plus, _style == "mac" ? "Neuer Tab (⌘T)" : "Neue Registerkarte (Strg+T)"); plus.Click += (_, _) => FilesNewTab();
            bar.Children.Add(plus);
            Control tabContent = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = bar };
            var inTitle = _style == "windows" && Windows.Active is { } tw && UnifiedTitle(tw);
            if (inTitle) tabContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { tabContent, Pos(OsHeaderCaps(FK), 1) } }; // Karten links, Fensterknöpfe rechts – wie im Windows-11-Explorer
            var tabBorder = new Border { Name = "fb-tabbar", IsVisible = win || titles.Count > 1, Padding = inTitle ? new Thickness(8, 6, 0, 0) : win ? new Thickness(8, 6, 8, 0) : new Thickness(6, 4),
                Background = Brush.Parse(win ? (light ? "#FFE8E8E8" : "#FF1A1A1A") : _style == "mac" ? (light ? "#FFE2E2E4" : "#FF252527") : (light ? "#FFEBEBED" : "#FF2A2A2E")),
                BorderBrush = Edge, BorderThickness = win ? default : new Thickness(0, 0, 0, 1), Child = tabContent };
            if (inTitle) OsHeaderDrag(tabBorder, FK); // ziehen verschiebt, Doppelklick maximiert
            return tabBorder;

        }
        static Control DockTop(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Top); return c; }
        static Control DockBottom(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Bottom); return c; }
        // Eine Leiste wie GNOME/Finder: Fensterknöpfe in der Kopfleiste, Ziehen verschiebt, Doppelklick maximiert
        Control? WinCaps()
        {
            if (Windows.Active is not { } aw || !UnifiedTitle(aw)) return null;
            var wid = aw.Id;
            Button Cap(string name, string tip, string glyph, string col, Action a)
            {
                var b = new Button { Name = "fb-" + name, Width = _style == "mac" ? 12 : 24, Height = _style == "mac" ? 12 : 24, Padding = new Thickness(0), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(0),
                    Background = Brush.Parse(col), Foreground = _style == "mac" ? Brush.Parse("#80000000") : Text, FontSize = _style == "mac" ? 8 : 10, Content = _style == "mac" ? "" : glyph,
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(_style == "mac" ? 0 : 4, 0, _style == "mac" ? 8 : 0, 0), VerticalAlignment = VerticalAlignment.Center };
                if (_style == "mac") { b.PointerEntered += (_, _) => b.Content = glyph; b.PointerExited += (_, _) => b.Content = ""; }
                ToolTip.SetTip(b, tip); b.Click += (_, _) => a(); return b;
            }
            if (_style == "mac")
                return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                    Cap("close", "Schließen", "✕", "#FF5F57", () => CloseWindow(wid)), Cap("min", "Im Dock ablegen", "—", "#FEBC2E", () => { Windows.Minimize(wid); AfterWindowChange(); }),
                    Cap("max", "Zoomen", "+", "#28C840", () => { Windows.ToggleMaximize(wid); AfterWindowChange(); }) } };
            return Cap("close", "Schließen", "✕", light ? "#14000000" : "#1AFFFFFF", () => CloseWindow(wid));
        }
        void Draggable(Control bar)
        {
            if (Windows.Active is not { } aw || !UnifiedTitle(aw)) return;
            var wid = aw.Id; Point? from = null;
            bar.PointerPressed += (_, e) =>
            {
                if (e.Source is Visual v && (v.FindAncestorOfType<Button>(true) is not null || v.FindAncestorOfType<TextBox>(true) is not null || v.FindAncestorOfType<ComboBox>(true) is not null)) return;
                if (e.ClickCount == 2) { Windows.ToggleMaximize(wid); AfterWindowChange(); return; }
                from = e.GetPosition(_winCanvas); e.Pointer.Capture(bar);
            };
            bar.PointerMoved += (_, e) => { if (from is not { } f) return; var p = e.GetPosition(_winCanvas); if (Windows.Get(wid) is { Maximized: true } mx) { mx.Maximized = false; mx.X = p.X - mx.W / 2; mx.Y = 0; } Windows.Move(wid, p.X - f.X, p.Y - f.Y); from = p; LayoutWindows(); };
            bar.PointerReleased += (_, e) => { from = null; e.Pointer.Capture(null); };
        }

        Control Frame(Control c)
        {
            var main = c;
            if (FilesPreview is { } pv && File.Exists(pv)) // Übersicht/Vorschau (Leertaste) rechts
            {
                Control body;
                var ext = Path.GetExtension(pv).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif") { try { body = new Image { Source = new Avalonia.Media.Imaging.Bitmap(pv), Stretch = Stretch.Uniform }; } catch (Exception) { body = new TextBlock { Text = "Keine Vorschau", Foreground = Muted }; } }
                else if (new FileInfo(pv).Length < 200_000 && ext is ".txt" or ".md" or ".csv" or ".json" or ".log" or ".xml" or ".cs" or ".py") body = new TextBlock { Text = string.Join(NL, File.ReadLines(pv).Take(60)), FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 11.5, Foreground = Text, TextWrapping = TextWrapping.Wrap };
                else body = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Children = { FileIcon(pv, 96), new TextBlock { Text = FileBrowser.Kind(pv, false) + " · " + FileBrowser.SizeText(new FileInfo(pv).Length), Foreground = Muted } } };
                var pane = new Border { Name = "fb-preview", Width = 300, Padding = new Thickness(12), BorderBrush = Edge, BorderThickness = new Thickness(1, 0, 0, 0), Background = GlassStrong,
                    Child = new DockPanel { Children = { DockTop(new TextBlock { Text = Path.GetFileName(pv), FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 0, 0, 8), TextTrimming = TextTrimming.CharacterEllipsis }), new ScrollViewer { Content = body } } } };
                main = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { c, Pos(pane, 1) } };
            }
            var root = new Border { Name = "fb-root", ClipToBounds = true, CornerRadius = new CornerRadius(_style == "trendo" ? 12 : 0), Child = main, MinHeight = 420, Focusable = true };
            root.AddHandler(KeyDownEvent, (_, e) => { if (e.Source is TextBox) return; if (FilesKey(e.Key, e.KeyModifiers)) e.Handled = true; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            return root;
        }
    }
}
