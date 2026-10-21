using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wellen 4 und 5: Kreativ & Entwicklung, Business. Je ein Bereich mit einer Unterebene.</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> KreativTabs = [];
    public static readonly List<(string Key, string Label, string Module)> BusinessTabs = [];
    /// <summary>Studio-ID → Seite, die beim Klick auf die Studio-Kachel öffnet.</summary>
    public static readonly Dictionary<string, string> StudioPages = new(StringComparer.OrdinalIgnoreCase);
    private string _w45Msg = "";

    private void InitWave45()
    {
        InitW4Dev();
        InitW4Oss();
        InitW4Media();
        InitW4Studio3D();
        InitW4Game();
        InitW4Assets();
        InitW4Builder();
        InitW4Suite();
        InitW5Business();
        InitW5Finance();
        InitW5Fusion();
        InitW5Ext();
        InitW5CleanRoom();
        InitW5Market();
        InitW5Brand();
        InitW5Ops();
        InitW5Launch();
        InitSys02();
        InitSys03();
        InitSys10();
        InitSys11();
        InitSys12();
        InitSys13();
        InitStartMenu();
    }

    private void RegisterTab(List<(string, string, string)> tabs, string key, string label, string module, Func<Control> view, params string[] words)
    {
        if (ReferenceEquals(tabs, FachTabs) && StudioCatalog.For(module) is { } info) label = info.Name; // ein Reiter je Studio: alltagsnaher Name
        if (!tabs.Any(t => t.Item1 == key)) tabs.Add((key, label, module));
        _windowGroups[key] = tabs[0].Item1;
        _showMap[key] = view; TabbedPages.Add(key); PageModules[key] = module;
        StudioPages.TryAdd(module, key);
        if (!ExtraPages.Any(p => p.Key == key)) ExtraPages.Add((label, key, words));
    }

    private Control TabNav(List<(string Key, string Label, string Module)> tabs, string prefix)
    {
        var row = new WrapPanel();
        foreach (var (k, l, m) in tabs)
        {
            object content = l;
            if (StudioIcons.For(m) is { } si)
            {
                var chip = Chip(si.Path, si.C1, si.C2, 20, 0.64); chip.Name = prefix + "-tabicon-" + k;
                content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { chip, new TextBlock { Text = l, VerticalAlignment = VerticalAlignment.Center } } };
            }
            var b = new Button { Name = prefix + "-tab-" + k, Content = content, Margin = new Thickness(0, 0, 6, 6), Background = CurrentView == k ? Accent : null };
            b.Click += (_, _) => { _w45Msg = ""; Show(k); }; row.Children.Add(b);
        }
        return row;
    }

    private Control KreativPage(string title, Control body) => W45Page(title, KreativTabs, "kreativ", body);
    private Control BusinessPage(string title, Control body) => W45Page(title, BusinessTabs, "business", body);

    private Control W45Page(string title, List<(string, string, string)> tabs, string prefix, Control body)
    {
        var sp = new StackPanel { Spacing = 8, Children = { TabNav(tabs, prefix) } };
        if (_w45Msg.Length > 0) sp.Children.Add(new Border { Name = "w45-msg", Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = P(_w45Msg, Text) });
        sp.Children.Add(body);
        return Page(title, sp);
    }

    private Border Card(Control child) => new() { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = child };

    private Grid Row(Control left, Control right) => new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { left, Pos(right, 1) } };

    private Button Act(string name, string label, Action click, bool primary = false)
    {
        var b = new Button { Name = name, Content = label, FontSize = 11.5, Padding = new Thickness(10, 3), Background = primary ? Accent : null };
        b.Click += (_, _) => click();
        return b;
    }

    private string WorkFolder(string sub)
    {
        var root = string.IsNullOrEmpty(_settings.ProjectsFolder) || !Path.IsPathRooted(_settings.ProjectsFolder) ? Path.Combine(_core.DataRoot, "projekte") : _settings.ProjectsFolder;
        var d = Path.Combine(root, sub); Directory.CreateDirectory(d); return d;
    }

    private void Say(string msg, string view) { _w45Msg = msg; Show(view); }
}
