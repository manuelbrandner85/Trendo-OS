using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 1: Suche (CORE-01), Bedienhilfen (SYS-12) und Alltagsseiten.</summary>
public partial class MainWindow
{
    private Action? Rescale;
    private string _query = "";
    public string LastQuery => _query;

    private void InitWave1()
    {
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.K, KeyModifiers.Control), Command = new RelayCommand(() => { _search.Focus(); _search.SelectAll(); }) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F1), Command = new RelayCommand(() => ShowHelp(CurrentView, force: true)) });
    }

    private static readonly (string Title, string Key, string[] Words)[] Pages =
    [
        ("Startbildschirm", "start", ["desktop", "start", "home"]), ("Alle Studios", "studios", ["studio"]), ("Projekte", "projects", ["ordner", "projekt"]),
        ("Freigaben", "approvals", ["erlaubnis", "genehmigen"]), ("System & Sicherheit", "system", ["protokoll", "version", "absturz"]), ("Apps", "apps", ["programme", "anwendungen"]),
        ("KI", "ki", ["assistent", "modell", "qwen"]), ("Einstellungen", "settings", ["design", "farbe", "hintergrund", "groesse", "schrift", "dunkel", "hell", "kontrast"]),
        ("Zusatzprogramme", "components", ["installieren", "download"]), ("Systemcheck", "systemcheck", ["reparieren", "pruefen"]), ("Sicherung", "sicherung", ["backup", "wiederherstellen"]),
        ("Fortschritt", "fortschritt", ["bauplan"]), ("Online-Dienste", "keys", ["schluessel", "api"]), ("Ersteinrichtung", "einrichtung", ["assistent", "setup"]),
        ("Werkzeuge: Notizen, Rechner, Wecker", "werkzeuge", ["notiz", "rechner", "taschenrechner", "timer", "wecker"]), ("Medien", "medien", ["musik", "video", "mpv"]),
        ("Speicher & Leistung", "ressourcen", ["ram", "arbeitsspeicher", "leistung", "langsam"]), ("Dokumente", "office", ["office", "word", "excel", "pdf"]),
        ("Briefstudio", "brief", ["brief", "kuendigung", "widerspruch", "din"]), ("Briefstudio: Akten", "akten", ["akte", "vorgang", "frist", "bescheid", "gericht", "versicherung", "anwalt"]), ("Briefköpfe", "briefkoepfe", ["absender", "briefkopf"]), ("Downloads", "downloads", ["download", "browser", "internet"]),
    ];
    public static readonly List<(string Title, string Key, string[] Words)> ExtraPages = [];

    public GlobalSearch BuildSearch()
    {
        var gs = new GlobalSearch();
        foreach (var (t, k, w) in Pages.Concat(ExtraPages)) if (_showMap.ContainsKey(k)) gs.Add("Seite", t, "page:" + k, w);
        foreach (var c in Categories) gs.Add("Seite", c.Label, "filter:" + c.Filter, c.Label.Split(' ', '&'));
        foreach (var s in _studios) gs.Add("Studio", s.Name, "studio:" + s.Id, s.Id);
        foreach (var p in _core.Projects.List()) gs.Add("Projekt", p.Name, "project:" + p.Id, p.Id);
        gs.AddFiles(string.IsNullOrEmpty(_settings.ProjectsFolder) ? UserSettings.DefaultProjectsFolder() : _settings.ProjectsFolder);
        return gs;
    }

    private Control SearchView()
    {
        var sp = new StackPanel { Spacing = 4 };
        var hits = BuildSearch().Find(_query);
        sp.Children.Add(P(hits.Count == 0 ? $"Nichts gefunden für „{_query}“. Tipp: kürzere Wörter versuchen, z. B. „brief“ statt „briefvorlage“." : $"{hits.Count} Treffer für „{_query}“", Text));
        foreach (var g in hits.GroupBy(h => h.Kind))
        {
            sp.Children.Add(H(g.Key switch { "Seite" => "Seiten", "Studio" => "Studios", "Projekt" => "Projekte", _ => "Dateien" }, 14));
            var i = 0;
            foreach (var h in g)
            {
                var b = new Button { Name = $"hit-{g.Key.ToLowerInvariant()}-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = TileBg,
                    Content = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = h.Title, Foreground = Text, FontSize = 13 }, P(h.Kind == "Datei" ? Path.GetDirectoryName(h.Target) ?? "" : h.Kind, null, 10.5) } } };
                b.Click += async (_, _) => await OpenHit(h);
                sp.Children.Add(b);
            }
        }
        return Page("Suche", sp);
    }

    public async Task OpenHit(SearchHit h)
    {
        var (kind, arg) = (h.Target[..h.Target.IndexOf(':')], h.Target[(h.Target.IndexOf(':') + 1)..]);
        switch (kind)
        {
            case "page": Show(arg); break;
            case "studio": await OpenStudio(arg, h.Title); break;
            case "filter": _studioFilter = arg; Show("studios"); break;
            case "project": Show("projects"); break;
            default: _core.Integrations.LaunchDirect(Principal.LocalOwner, h.Target, _core.Audit); break;
        }
    }
}
