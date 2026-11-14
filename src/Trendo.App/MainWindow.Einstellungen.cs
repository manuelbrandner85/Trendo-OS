using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Systemumbau 2, E1 (freigegeben 03.11.2026): die System-Programme als eine Einstellungen-App wie in Windows 11 – links Bereiche mit
/// Suche über alle Einstellungen, rechts die jeweilige Seite. Gilt für alle Einstellungs-Seiten auf einmal (vorher: lose Reiter-Seiten).</summary>
public partial class MainWindow
{
    /// <summary>Bereiche der Einstellungen-App (Reihenfolge wie Windows: System, Geräte, Programme, Datenschutz & Sicherheit, Zustand).</summary>
    public static readonly (string Group, string Icon, (string Key, string Label)[] Pages)[] SettingsGroups =
    [
        ("System", "⚙", [("settings", "Darstellung & Sprache"), ("personen", "Personen & Konten"), ("bedien-sehen", "Sehen & Bedienhilfen"), ("updates", "Trendo-Updates"), ("lizenz", "Konto, Lizenz & Vollversion"), ("modus", "Betriebsmodus")]),
        ("Geräte", "🖥", [("geraete", "Geräte & Laufwerke"), ("handy", "Handy verbinden"), ("verbund", "Geräteverbund"), ("teilen", "Im Heimnetz teilen")]),
        ("Programme", "▦", [("apps", "Apps"), ("appcenter", "Programme verwalten"), ("hinzufuegen", "Hinzufügen"), ("prog-pflege", "Pflegen & Aktualisieren"), ("components", "Zusatzprogramme"), ("downloads", "Downloads")]),
        ("Datenschutz & Sicherheit", "🛡", [("schutz", "Schutz & Datenschutz"), ("approvals", "Freigaben"), ("sicherung", "Sicherung"), ("heilung", "Selbstheilung"), ("netz-pruefen", "Sicher prüfen"), ("forensik", "Sicherheits-Werkzeuge"), ("system", "Protokoll & Abstürze")]),
        ("Zustand & Leistung", "📈", [("hardware", "Hardware & Leistung"), ("ressourcen", "Speicher & Leistung"), ("qual-leistung", "Leistung messen"), ("systemcheck", "Systemcheck"), ("qualitaet", "Qualitäts-Ampel"), ("fortschritt", "Bau-Fortschritt")]),
    ];

    /// <summary>E2 (freigegeben 03.11.2026): KI-Zentrale wie LM Studio – alles rund um KI in einer App: fragen, Modelle, Anweisungen, Team & Abläufe, Verbindungen.</summary>
    public static readonly (string Group, string Icon, (string Key, string Label)[] Pages)[] KiGroups =
    [
        ("Fragen", "✳", [("ki-fragen", "KI fragen"), ("ki", "Trendo KI (lokal)"), ("claude-verbinden", "Mit Claude verbinden")]),
        ("Modelle", "◈", [("ki-bibliothek", "KI-Bibliothek"), ("ki-quellen", "KI-Quellen"), ("ki-internet", "Kostenlose Internet-KIs")]),
        ("Anweisungen", "✎", [("ki-vorlagen", "KI-Vorlagen"), ("ki-skills", "KI-Fähigkeiten (Skills)")]),
        ("Team & Abläufe", "⚡", [("ki-rollen", "KI-Helfer und Teams"), ("ablaeufe", "Abläufe"), ("produktion", "Produktion")]),
        ("Verbindungen", "⇄", [("verbindungen", "Verbindungen"), ("keys", "Online-Dienste")]),
    ];

    sealed record AppShellDef(string Name, string Title, string Icon, (string Group, string Icon, (string Key, string Label)[] Pages)[] Groups);
    static readonly AppShellDef[] AppShells = [new("settings", "Einstellungen", "⚙", SettingsGroups), new("ki", "KI-Zentrale", "✳", KiGroups)];
    static readonly Dictionary<string, AppShellDef> ShellOf = AppShells.SelectMany(d => d.Groups.SelectMany(g => g.Pages.Select(p => (p.Key, d)))).ToDictionary(x => x.Key, x => x.d, StringComparer.OrdinalIgnoreCase);
    public static bool IsSettingsPage(string key) => ShellOf.TryGetValue(key, out var d) && d.Name == "settings";
    /// <summary>Seite gehört zu einer App-Hülle (Einstellungen oder KI-Zentrale).</summary>
    public static bool InAppShell(string key) => ShellOf.ContainsKey(key);
    string _setQuery = "";

    /// <summary>E12 (T-0616): In „Einfach“ zeigen die Einstellungen diese Hauptseiten, alles Weitere nach einem Klick auf „Mehr Einstellungen“ (die Suche findet immer alles).</summary>
    public static readonly string[] SimpleSettingsPages = ["settings", "bedien-sehen", "personen", "geraete", "sicherung", "updates"];
    bool _setAllPages;

    /// <summary>Persönlicher Ordner (Bilder, Videos, Musik, Dokumente). Im Testmodus ein leerer eigener Ordner – Tests und Bildschirmfotos
    /// sehen nie echte Fotos oder Dateien der Person (Befund 04.11.2026: Medien zeigte im Testmodus echte Fotos).</summary>
    string UserFolder(Environment.SpecialFolder f)
    {
        if (!_opt.Isolated) return Environment.GetFolderPath(f);
        var d = Path.Combine(_core.DataRoot, "testordner", f.ToString());
        Directory.CreateDirectory(d);
        return d;
    }

    /// <summary>Suche über alle Einstellungen: Bereichsnamen, Seitennamen und die Suchwörter der Seiten.</summary>
    public List<(string Key, string Label)> SettingsSearch(string q, string shell = "settings")
    {
        q = q.Trim();
        var groups = AppShells.First(d => d.Name == shell).Groups;
        var pages = groups.SelectMany(g => g.Pages.Select(p => (p.Key, p.Label, g.Group))).Where(p => _showMap.ContainsKey(p.Key)).ToList();
        if (q.Length == 0) return pages.Select(p => (p.Key, p.Label)).ToList();
        bool Hit(string s) => s.Contains(q, StringComparison.CurrentCultureIgnoreCase);
        return pages.Where(p => Hit(p.Label) || Hit(p.Group) || ExtraPages.Any(e => string.Equals(e.Key, p.Key, StringComparison.OrdinalIgnoreCase) && (Hit(e.Title) || e.Words.Any(Hit))))
            .Select(p => (p.Key, p.Label)).ToList();
    }

    Control SettingsShell(string key, Control page)
    {
        var def = ShellOf[key]; var n = def.Name;
        var nav = new StackPanel { Name = n + "-nav", Spacing = 1 };
        var hits = _setQuery.Length > 0 ? SettingsSearch(_setQuery, n).Select(h => h.Key).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        var slim = n == "settings" && IsSimple && !_setAllPages && hits is null; // E12
        var hiddenCount = 0;
        foreach (var (group, icon, pages) in def.Groups)
        {
            var visible = pages.Where(p => _showMap.ContainsKey(p.Key) && (hits is null || hits.Contains(p.Key))).ToList();
            if (slim) { var all = visible.Count; visible = visible.Where(p => SimpleSettingsPages.Contains(p.Key) || string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).ToList(); hiddenCount += all - visible.Count; }
            if (visible.Count == 0) continue;
            nav.Children.Add(new TextBlock { Text = icon + "  " + group, Foreground = Muted, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(10, 10, 0, 3) });
            foreach (var (k, label) in visible)
            {
                var on = string.Equals(k, key, StringComparison.OrdinalIgnoreCase); var target = k;
                var b = new Button { Name = n + "-nav-" + k, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(8),
                    Background = on ? Brush.Parse("#26D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(0),
                    Content = new DockPanel { Children = {
                        Dk(new Border { Width = 3, Height = 16, CornerRadius = new CornerRadius(2), Background = on ? Accent : Brushes.Transparent, Margin = new Thickness(0, 0, 8, 0) }, Avalonia.Controls.Dock.Left),
                        new TextBlock { Text = label, Foreground = on ? Text : Brush.Parse(C("#CCFFFFFF", "#CC000000")), FontSize = 13, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal } } } };
                b.Click += (_, _) => { _setQuery = ""; Show(target); };
                // die geöffnete Seite ist eine Markierung, kein Knopf (ein Klick darauf hätte nichts bewirkt)
                if (on) { var c = (Control)b.Content!; b.Content = null; nav.Children.Add(new Border { Name = n + "-nav-" + k, Background = b.Background, CornerRadius = b.CornerRadius, Padding = b.Padding, Child = c }); }
                else nav.Children.Add(b);
            }
        }
        if (n == "settings" && IsSimple && hits is null && (hiddenCount > 0 || _setAllPages))
        {
            var more = new Button { Name = "settings-nav-more", Content = _setAllPages ? "Weniger anzeigen" : $"Mehr Einstellungen ({hiddenCount})", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), Margin = new Thickness(0, 8, 0, 0), Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            ToolTip.SetTip(more, "In der Bedienstufe „Einfach“ stehen hier nur die wichtigsten Seiten – die Suche oben findet immer alle");
            more.Click += (_, _) => { _setAllPages = !_setAllPages; Show(key); };
            nav.Children.Add(more);
        }
        if (nav.Children.Count == 0) nav.Children.Add(new TextBlock { Name = n + "-none", Text = n == "settings" ? "Keine Einstellung gefunden." : "Nichts gefunden.", Foreground = Muted, FontSize = 12, Margin = new Thickness(10) });
        var search = new TextBox { Name = n + "-search", Watermark = n == "settings" ? "Einstellung suchen" : "In der " + def.Title + " suchen", Text = _setQuery, Margin = new Thickness(6, 4, 6, 6) };
        search.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) { _setQuery = search.Text ?? ""; var found = SettingsSearch(_setQuery, n); if (found.Count == 1) { _setQuery = ""; Show(found[0].Key); } else Show(key); }
            if (e.Key == Avalonia.Input.Key.Escape) { _setQuery = ""; Show(key); }
        };
        var side = new Border { Name = n + "-side", Width = 250, Background = Brush.Parse(C("#F20E131C", "#FFF2F4F8")), BorderBrush = Edge, BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(6, 10),
            Child = new DockPanel { Children = {
                Dk(new StackPanel { Children = {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 0, 0, 8), Children = {
                        new TextBlock { Text = def.Icon, FontSize = 20, Foreground = Accent, VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = def.Title, FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } },
                    search } }, Avalonia.Controls.Dock.Top),
                new ScrollViewer { Content = nav } } } };
        var grid = new Grid { Name = n + "-shell", ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(side);
        grid.Children.Add(Pos(page, 1));
        return grid;
    }
}
