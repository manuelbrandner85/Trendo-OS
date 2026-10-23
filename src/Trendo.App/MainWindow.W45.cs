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
        // Ein Fenster je Studio, nicht je Welle: Reiter desselben Moduls teilen sich das Fenster, andere Studios nicht.
        string group = key;
        foreach (var (k, m) in PageModules)
            if (string.Equals(m, module, StringComparison.OrdinalIgnoreCase) && _windowGroups.TryGetValue(k, out var existing)) { group = existing; break; }
        _windowGroups[key] = group;
        _showMap[key] = view; TabbedPages.Add(key); PageModules[key] = module;
        StudioPages.TryAdd(module, key);
        if (!ExtraPages.Any(p => p.Key == key)) ExtraPages.Add((label, key, words));
    }

    private Control TabNav(List<(string Key, string Label, string Module)> tabs, string prefix, bool compact = false)
    {
        var row = new WrapPanel();
        foreach (var (k, l, m) in tabs)
        {
            object content = l;
            if (StudioIcons.For(m) is { } si)
            {
                var chip = Chip(si.Path, si.C1, si.C2, compact ? 16 : 20, 0.64); chip.Name = prefix + "-tabicon-" + k;
                content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { chip, new TextBlock { Text = l, VerticalAlignment = VerticalAlignment.Center } } };
            }
            var b = new Button { Name = prefix + "-tab-" + k, Content = content, Margin = compact ? new Thickness(0, 0, 4, 4) : new Thickness(0, 0, 6, 6), Padding = compact ? new Thickness(7, 3) : new Thickness(10, 6), CornerRadius = new CornerRadius(10), Foreground = Text, Background = CurrentView == k ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(CurrentView == k ? 0 : 1) };
            if (compact) b.FontSize = 12;
            b.Click += (_, _) => { _w45Msg = ""; Show(k); }; row.Children.Add(b);
        }
        return row;
    }

    private Control KreativPage(string title, Control body) => W45Page(title, KreativTabs, "kreativ", body);

    /// <summary>Wie KreativPage, aber der untere Teil füllt das Fenster. Vorschau und Knöpfe bleiben ohne Scrollen sichtbar.</summary>
    private Control FitKreativPage(string title, Control header, Control fill)
    {
        var head = new StackPanel { Spacing = 6, Children = { TabNav(KreativTabs, "kreativ", compact: true) } };
        if (_w45Msg.Length > 0) head.Children.Add(new Border { Name = "w45-msg", Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = P(_w45Msg, Text) });
        head.Children.Add(header);
        return FitPage(title, head, fill);
    }
    private Control BusinessPage(string title, Control body) => W45Page(title, BusinessTabs, "business", body);

    private Control W45Page(string title, List<(string, string, string)> tabs, string prefix, Control body)
    {
        var sp = new StackPanel { Spacing = 8, Children = { TabNav(tabs, prefix) } };
        if (_w45Msg.Length > 0) sp.Children.Add(new Border { Name = "w45-msg", Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = P(_w45Msg, Text) });
        sp.Children.Add(body);
        return Page(title, sp);
    }

    private Border Card(Control child) => new() { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8), Child = child };

    private Grid Row(Control left, Control right) => new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { left, Pos(right, 1) } };

    private Button Act(string name, string label, Action click, bool primary = false)
    {
        var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Foreground = Text, Background = primary ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(primary ? 0 : 1) };
        b.Click += (_, _) => click();
        return b;
    }

    private string WorkFolder(string sub)
    {
        var root = string.IsNullOrEmpty(_settings.ProjectsFolder) || !Path.IsPathRooted(_settings.ProjectsFolder) ? Path.Combine(_core.DataRoot, "projekte") : _settings.ProjectsFolder;
        var d = Path.Combine(root, sub); Directory.CreateDirectory(d); return d;
    }

    private void Say(string msg, string view) { _w45Msg = msg; Show(view); }

    /// <summary>Baut die Seite neu, solange sie noch offen ist. Ein fertiger Hintergrundjob wechselt nicht von allein auf eine andere Seite zurück.</summary>
    private void RefreshHere(string view)
    {
        if (CurrentView == view) Show(view);
    }

    /// <summary>Für den Knopf-Test: Hinweise leeren, damit jeder Klick von einer ruhigen Seite aus bewertet wird.</summary>
    public void QuietNotices()
    {
        _w45Msg = ""; _caseMsg = ""; _a11yOut = ""; _devKiSatz = ""; _wfMsg = ""; _srcMsg = "";
        _promptMsg = ""; _rsMsg = ""; _repairMsg = ""; _backupMsg = ""; _shareMsg = ""; _scanStatus = "";
        _progOut = ""; _qualOut = ""; _letterMsg = ""; _secMsg = ""; _devMsg = ""; _forMsg = ""; _hashOut = "";
        _wizTap = ""; _mailMsg = ""; _wissenMsg = ""; _askState = ""; _agentMsg = ""; _netStatus = "";
        _noteMsg = ""; _healNote = ""; W7Msg = ""; _depAsk = "";
        _settingsNote = ""; _studioNote = "";
        _kiText.Text = "Hallo! Ich bin noch nicht verbunden. Mit „KI einrichten“ installierst du die kostenlose lokale KI – danach kannst du mir Aufgaben geben, Studios öffnen oder Fragen stellen.";
        if (_depLog.Text?.Length > 0) _depLog.Text = "";
        Bench.CamStatus = "Noch kein Bild. Kamera suchen oder eine rtsp-Adresse eintragen.";
        Bench.DriveStatus = "Die Steuerung steht auf Simulation. Es bewegt sich nichts Echtes.";
        if (Bench.Live) { Bench.Live = false; _liveCts?.Cancel(); }
    }
}
