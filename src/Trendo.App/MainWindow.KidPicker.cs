using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0764 (8. Prüfrunde M1): EINZIGER Ort für Datei- und Ordner-Dialoge. Erwachsene: wie bisher der Dialog des Systems (StorageProvider).
/// Kinderprofil: nie der Windows-Dialog (dort ließe sich jede Datei starten, umbenennen oder mit „Öffnen mit“ an ein Programm übergeben), sondern
/// Trendos eigener Dateimanager als Auswahl – nur in den eigenen Ordnern (Benutzerordner und bekannte Ordner), ohne Starten, ohne Umbenennen.
/// Ein Quelltext-Test stellt sicher, dass StorageProvider.Open…/Save…PickerAsync nur hier vorkommen.</summary>
public partial class MainWindow
{
    /// <summary>Datei(en) zum Öffnen wählen – Ersatz für <c>StorageProvider.OpenFilePickerAsync</c>.</summary>
    internal async Task<IReadOnlyList<IStorageFile>> PickOpenFiles(FilePickerOpenOptions o)
    {
        if (CanManageSoftware) return await StorageProvider.OpenFilePickerAsync(o);
        var got = await KidPick(new KidPickRequest(o.Title ?? "Datei wählen", KidPickMode.Open, o.AllowMultiple, Patterns(o.FileTypeFilter), null));
        var files = new List<IStorageFile>();
        foreach (var p in got) if (await StorageProvider.TryGetFileFromPathAsync(p) is { } f) files.Add(f); // nur der lokale Pfad, kein Dialog
        return files;
    }

    /// <summary>Speicherort wählen – Ersatz für <c>StorageProvider.SaveFilePickerAsync</c>.</summary>
    internal async Task<IStorageFile?> PickSaveFile(FilePickerSaveOptions o)
    {
        if (CanManageSoftware) return await StorageProvider.SaveFilePickerAsync(o);
        var ext = o.DefaultExtension is { Length: > 0 } d ? (d.StartsWith('.') ? d : "." + d) : Patterns(o.FileTypeChoices).Select(p => p.TrimStart('*')).FirstOrDefault(p => p.StartsWith('.') && p.Length > 1);
        var got = await KidPick(new KidPickRequest(o.Title ?? "Speichern unter", KidPickMode.Save, false, [], o.SuggestedFileName, ext));
        if (got.Count == 0) return null;
        if (!File.Exists(got[0])) await File.WriteAllBytesAsync(got[0], []); // der Aufrufer schreibt hinein; ohne Datei liefert das System kein Objekt
        return await StorageProvider.TryGetFileFromPathAsync(got[0]);
    }

    /// <summary>Ordner wählen – Ersatz für <c>StorageProvider.OpenFolderPickerAsync</c>.</summary>
    internal async Task<IReadOnlyList<IStorageFolder>> PickFolders(FolderPickerOpenOptions o)
    {
        if (CanManageSoftware) return await StorageProvider.OpenFolderPickerAsync(o);
        var got = await KidPick(new KidPickRequest(o.Title ?? "Ordner wählen", KidPickMode.Folder, false, [], null));
        var dirs = new List<IStorageFolder>();
        foreach (var p in got) if (await StorageProvider.TryGetFolderFromPathAsync(p) is { } f) dirs.Add(f);
        return dirs;
    }

    static List<string> Patterns(IReadOnlyList<FilePickerFileType>? types) =>
        types is null ? [] : types.SelectMany(t => t.Patterns ?? []).Where(p => p != "*" && p != "*.*").Select(p => p.Trim().ToLowerInvariant()).Distinct().ToList();

    internal enum KidPickMode { Open, Save, Folder }
    internal sealed record KidPickRequest(string Title, KidPickMode Mode, bool Multiple, IReadOnlyList<string> Patterns, string? SuggestedName, string? DefaultExtension = null);

    /// <summary>Für Tests: das zuletzt geöffnete Auswahlfenster des Kinderprofils und die zuletzt gewählten Pfade.</summary>
    public Window? KidPickWindow { get; private set; }
    public IReadOnlyList<string> KidPickLast { get; private set; } = [];

    /// <summary>Ordner, in denen das Kinderprofil wählen darf: der Benutzerordner und die bekannten Ordner (Dokumente, Bilder …).</summary>
    internal IReadOnlyList<string> KidPickRoots() =>
        new[] { Files.Home }.Concat(Files.KnownFolders().Select(k => k.Path)).Where(p => p.Length > 0).Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    internal bool KidPickAllowed(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        if (Trendo.Core.Sys.ProtectedFolders.IsProtected(full)) return false; // T-0764 (10. Prüfrunde): nie in Trendos eigene Ordner
        return KidPickRoots().Any(r => string.Equals(full, r, StringComparison.OrdinalIgnoreCase) || full.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Name für „Speichern unter“ im Kinderprofil: ein einfacher Dateiname, keine Pfade, Gerätenamen, Datenströme oder Programm-/Skript-Endungen.</summary>
    internal static string? KidSaveName(string? name, string? defaultExt)
    {
        var n = (name ?? "").Trim().TrimEnd(' ', '.');
        if (n.Length == 0 || n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || n.Contains(':') || n.Contains("..")) return null;
        if (Path.GetExtension(n).Length == 0 && defaultExt is { Length: > 1 }) n += defaultExt;
        if (Trendo.Core.Sys.Archives.UnsafeEntryName(n)) return null; // Code-Endungen, Installer, Gerätenamen, ohne Endung
        return n;
    }

    async Task<IReadOnlyList<string>> KidPick(KidPickRequest r)
    {
        _core.Audit.Append(CurrentPrincipal.Name, "datei.auswahl.trendo", r.Mode.ToString());
        var roots = KidPickRoots();
        var dir = roots[0]; Directory.CreateDirectory(dir);
        var selected = new List<string>();
        var win = new Window { Title = r.Title, Width = 640, Height = 520, Background = Brushes.Black, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true, Name = "kid-pick" };
        var where = new TextBlock { Name = "kp-where", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 6), TextTrimming = TextTrimming.CharacterEllipsis };
        var list = new StackPanel { Name = "kp-list", Spacing = 2 };
        var name = new TextBox { Name = "kp-name", Text = r.SuggestedName ?? "", IsVisible = r.Mode == KidPickMode.Save, Watermark = "Dateiname" };
        var msg = new TextBlock { Name = "kp-msg", Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap };
        string[]? result = null;
        bool Matches(string f) => r.Patterns.Count == 0 || r.Patterns.Any(p => p.StartsWith("*.") ? f.EndsWith(p[1..], StringComparison.OrdinalIgnoreCase) : string.Equals(Path.GetFileName(f), p, StringComparison.OrdinalIgnoreCase));
        void Fill()
        {
            list.Children.Clear(); where.Text = dir; msg.Text = "";
            IEnumerable<string> dirs = [], files = [];
            try
            {
                dirs = Directory.EnumerateDirectories(dir).Where(d => !new DirectoryInfo(d).Attributes.HasFlag(FileAttributes.Hidden) && !Path.GetFileName(d).StartsWith('.')).OrderBy(d => d, StringComparer.CurrentCultureIgnoreCase).ToList();
                if (r.Mode != KidPickMode.Folder) files = Directory.EnumerateFiles(dir).Where(Matches).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).Take(2000).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { msg.Text = "Dieser Ordner lässt sich nicht lesen."; }
            foreach (var d in dirs)
            {
                var b = new Button { Name = "kp-dir-" + Path.GetFileName(d), Content = "📁 " + Path.GetFileName(d), HorizontalAlignment = HorizontalAlignment.Stretch };
                var target = d; b.Click += (_, _) => { dir = target; selected.Clear(); Fill(); };
                list.Children.Add(b);
            }
            foreach (var f in files)
            {
                var file = f;
                var b = new Button { Name = "kp-file-" + Path.GetFileName(f), Content = "📄 " + Path.GetFileName(f), HorizontalAlignment = HorizontalAlignment.Stretch, Background = selected.Contains(f) ? Brushes.DarkGoldenrod : null };
                b.Click += (_, _) =>
                {
                    if (r.Mode == KidPickMode.Save) { name.Text = Path.GetFileName(file); return; }
                    if (!r.Multiple) selected.Clear();
                    if (!selected.Remove(file)) selected.Add(file);
                    Fill();
                };
                list.Children.Add(b);
            }
        }
        var up = new Button { Name = "kp-up", Content = "⬆ Nach oben" };
        up.Click += (_, _) =>
        {
            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(dir));
            if (parent is not null && KidPickAllowed(parent) && !roots.Contains(dir, StringComparer.OrdinalIgnoreCase)) { dir = parent; selected.Clear(); Fill(); }
            else msg.Text = "Weiter nach oben geht es hier nicht.";
        };
        var places = new WrapPanel();
        foreach (var root in roots)
        {
            var b = new Button { Name = "kp-place-" + Path.GetFileName(root), Content = Path.GetFileName(root), Margin = new Thickness(0, 0, 4, 4) };
            var target = root; b.Click += (_, _) => { dir = target; Directory.CreateDirectory(dir); selected.Clear(); Fill(); };
            places.Children.Add(b);
        }
        var ok = new Button { Name = "kp-ok", Content = r.Mode switch { KidPickMode.Save => "Speichern", KidPickMode.Folder => "Diesen Ordner wählen", _ => "Öffnen" } };
        ok.Click += (_, _) =>
        {
            if (!KidPickAllowed(dir)) { msg.Text = "Dieser Ordner ist nicht erlaubt."; return; }
            switch (r.Mode)
            {
                case KidPickMode.Folder: result = [dir]; break;
                case KidPickMode.Save:
                    if (KidSaveName(name.Text, r.DefaultExtension) is not { } n) { msg.Text = "Bitte einen einfachen Dateinamen eingeben (keine Programme oder Skripte)."; return; }
                    result = [Path.Combine(dir, n)]; break;
                default:
                    if (selected.Count == 0) { msg.Text = "Bitte zuerst eine Datei antippen."; return; }
                    result = [.. selected]; break;
            }
            win.Close();
        };
        var cancel = new Button { Name = "kp-cancel", Content = "Abbrechen" };
        cancel.Click += (_, _) => { result = null; win.Close(); };
        win.Content = new DockPanel
        {
            Margin = new Thickness(12),
            Children =
            {
                new StackPanel { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Top, Spacing = 6, Children = { places, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { up, where } } } },
                new StackPanel { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Bottom, Spacing = 6, Margin = new Thickness(0, 8, 0, 0), Children = { name, msg, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ok, cancel } } } },
                new ScrollViewer { Content = list },
            },
        };
        Fill();
        KidPickWindow = win;
        try { await win.ShowDialog(this); }
        finally { KidPickWindow = null; }
        KidPickLast = result ?? [];
        return KidPickLast;
    }
}
