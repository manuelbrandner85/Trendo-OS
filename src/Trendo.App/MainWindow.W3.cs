using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 3: System, Sicherheit & Kommunikation – eigene Reiterleiste „Schutz & Geräte“ (max. 2 Ebenen).</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label)> SysTabs = [];
    private string _secMsg = ""; private string _scanFolder = "";
    public IReadOnlyList<PrivacyFinding> LastPrivacy { get; private set; } = [];

    partial void InitWave3()
    {
        _showMap["schutz"] = SecurityView; SysTabs.Add(("schutz", "Schutz"));
        StudioPages["CORE-11"] = "schutz";
        InitX02(); InitSys04(); InitSys07(); InitSys06(); InitSys09(); InitSpec12(); InitSys08();
        ExtraPages.Add(("Schutz & Datenschutz", "schutz", ["sicherheit", "datenschutz", "passwort", "privat", "wiederherstellen", "schutz"]));
    }

    private Control SysNav()
    {
        var row = new WrapPanel();
        foreach (var (k, l) in SysTabs.Where(t => _showMap.ContainsKey(t.Key)))
        {
            var b = new Button { Name = "sys-tab-" + k, Content = l, Margin = new Thickness(0, 0, 6, 6), Background = CurrentView == k ? Accent : null };
            b.Click += (_, _) => Show(k); row.Children.Add(b);
        }
        return row;
    }

    private Control SysPage(string title, Control body) => Page(title, new StackPanel { Spacing = 8, Children = { SysNav(), body } });

    private Control Row2(Control left, Control? right) => WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { left, right is null ? new Panel() : Pos(right, 1) } });

    // ---------- CORE-11 ----------
    private Control SecurityView()
    {
        var checks = SecurityCenter.Checks(_core, Backups, DateTime.Now);
        var score = SecurityCenter.Score(checks);
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Name = "sec-score", Text = $"Schutz: {score} von 100", FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = score >= 80 ? Brush.Parse("#4ADE80") : score >= 50 ? Brush.Parse("#FACC15") : Brush.Parse("#F87171") });
        foreach (var c in checks)
        {
            Button? fix = null;
            if (c.FixPage is { } fp && c.Level != CheckLevel.Gut) { fix = new Button { Name = "sec-fix-" + c.Id, Content = c.Id == "backup" ? "Jetzt sichern" : "Ansehen", Padding = new Thickness(8, 2) }; fix.Click += (_, _) => { if (c.Id == "backup") { Backups.Create(DateTime.Now); _secMsg = "Sicherung erstellt."; Show("schutz"); } else Show(fp); }; }
            sp.Children.Add(Row2(new StackPanel { Children = { new TextBlock { Text = (c.Level == CheckLevel.Gut ? "✓ " : c.Level == CheckLevel.Hinweis ? "• " : "⚠ ") + c.Title, Foreground = Text, FontSize = 13 }, P(c.Detail, null, 11) } }, fix));
        }
        sp.Children.Add(H("Liegen Passwörter offen herum?", 14));
        sp.Children.Add(P("Trendo liest Textdateien in einem Ordner und zeigt offen gespeicherte Passwörter, Schlüssel, IBANs und Kartennummern – nur maskiert. Nichts wird verändert oder verschickt."));
        var folder = new TextBox { Name = "sec-folder", Text = _scanFolder == "" ? (string.IsNullOrEmpty(_settings.ProjectsFolder) ? UserSettings.DefaultProjectsFolder() : _settings.ProjectsFolder) : _scanFolder, Width = 360 };
        var scan = new Button { Name = "sec-scan", Content = "Ordner prüfen", Background = Accent };
        scan.Click += async (_, _) =>
        {
            _scanFolder = folder.Text?.Trim().Trim('"') ?? "";
            if (!Directory.Exists(_scanFolder)) { _secMsg = "Diesen Ordner gibt es nicht."; Show("schutz"); return; }
            _secMsg = "Prüfung läuft …"; Show("schutz");
            scan.IsEnabled = false; var f = _scanFolder;
            LastPrivacy = await Task.Run(() => SecurityCenter.ScanFolder(f));
            _core.Audit.Append(Principal.LocalOwner.Name, "privacy.scan", $"treffer={LastPrivacy.Count}");
            _secMsg = LastPrivacy.Count == 0 ? "Nichts gefunden – gut so." : $"{LastPrivacy.Count} Fundstelle(n). Tipp: Passwörter gehören in einen Passwort-Manager, nicht in Textdateien.";
            Show("schutz");
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { folder, scan } });
        sp.Children.Add(new TextBlock { Name = "sec-msg", Text = _secMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        var i = 0;
        foreach (var f in LastPrivacy.Take(50))
        {
            var open = new Button { Name = $"sec-open-{i++}", Content = "Ordner öffnen", Padding = new Thickness(8, 2) };
            open.Click += (_, _) => Launch(Path.GetDirectoryName(f.File) ?? "");
            sp.Children.Add(Row2(new StackPanel { Children = { new TextBlock { Text = $"{f.Kind}: {f.Masked}", Foreground = Text, FontSize = 12.5 }, P($"{Path.GetFileName(f.File)}, Zeile {f.Line}", null, 10.5) } }, open));
        }
        sp.Children.Add(H("Wiederherstellen", 14));
        sp.Children.Add(P($"{Backups.List().Count} Sicherung(en) vorhanden. Damit holst du Einstellungen, Projekte und Protokoll zurück."));
        sp.Children.Add(Nav2("Zur Sicherung", "sicherung"));
        return SysPage("Schutz", sp);
    }
}
