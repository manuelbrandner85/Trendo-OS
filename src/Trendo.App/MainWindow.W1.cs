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
        StudioPages.TryAdd("X-09", "ressourcen"); // Speicher & Leistung
        InitPdf(); InitText(); InitTabelle(); InitPraesentation();
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
        ("Briefe schreiben", "brief", ["brief", "kuendigung", "widerspruch", "din"]), ("Akten", "akten", ["akte", "vorgang", "frist", "bescheid", "gericht", "versicherung", "anwalt"]), ("Briefköpfe", "briefkoepfe", ["absender", "briefkopf"]), ("Downloads", "downloads", ["download", "browser", "internet"]),
    ];
    public static readonly List<(string Title, string Key, string[] Words)> ExtraPages = [];

    /// <summary>Suchwörter eines Studios: Name, Satz, Bereich und die Schlagwörter seiner Seiten.</summary>
    private string[] StudioWords(string id, string name)
    {
        var words = new List<string> { id, name };
        void AddText(string? t)
        {
            if (string.IsNullOrWhiteSpace(t)) return;
            foreach (var part in t.Split([' ', ',', '.', ';', ':', '/', '–', '—', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
                if (part.Length > 1) words.Add(part);
        }
        if (StudioCatalog.For(id) is { } info) { AddText(info.Name); AddText(info.Sentence); AddText(info.Area); }
        foreach (var (title, key, extra) in Pages.Concat(ExtraPages))
        {
            if (key is "start" or "studios") continue;
            var mod = PageModules.TryGetValue(key, out var m) ? m : null;
            if (mod is null && string.Equals(PageForStudio(id), key, StringComparison.OrdinalIgnoreCase)) mod = id;
            if (mod is null && StudioPages.TryGetValue(id, out var sp) && string.Equals(sp, key, StringComparison.OrdinalIgnoreCase)) mod = id;
            if (!string.Equals(mod, id, StringComparison.OrdinalIgnoreCase)) continue;
            words.AddRange(extra); AddText(title);
        }
        return words.ToArray();
    }

    public GlobalSearch BuildSearch()
    {
        var gs = new GlobalSearch();
        foreach (var (t, k, w) in Pages.Concat(ExtraPages)) if (_showMap.ContainsKey(k)) gs.Add("Seite", t, "page:" + k, w);
        foreach (var c in Categories) gs.Add("Seite", c.Label, "filter:" + c.Filter, c.Label.Split(' ', '&'));
        foreach (var s in _studios) gs.Add("Studio", StudioCatalog.For(s.Id)?.Name ?? s.Name, "studio:" + s.Id, StudioWords(s.Id, s.Name));
        foreach (var (id, info) in StudioCatalog.ById)
            if (!_studios.Any(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)))
                gs.Add("Studio", info.Name, "studio:" + id, StudioWords(id, info.Name));
        foreach (var p in _core.Projects.List()) gs.Add("Projekt", p.Name, "project:" + p.Id, p.Id);
        foreach (var n in new NoteStore(_core.DataRoot).All()) gs.Add("Notiz", n.Text.Split('\n')[0], "page:werkzeuge", n.Text.Split([' ', '\n', '/', '.', '-'], StringSplitOptions.RemoveEmptyEntries)); // SYS-03-P21
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
            sp.Children.Add(H(g.Key switch { "Seite" => "Seiten", "Studio" => "Studios", "Projekt" => "Projekte", "Notiz" => "Notizen", _ => "Dateien" }, 14));
            var i = 0;
            foreach (var h in g)
            {
                var b = new Button { Name = $"hit-{g.Key.ToLowerInvariant()}-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = TileBg,
                    Content = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = h.Title, Foreground = Text, FontSize = 13 }, P(h.Kind == "Datei" ? Path.GetDirectoryName(h.Target) ?? "" : h.Kind, null, 10.5) } } };
                b.Click += async (_, _) => await OpenHit(h);
                sp.Children.Add(b);
            }
        }
        if (!_core.Offline && _query.Trim().Length > 0 && Trendo.Core.Sys.Web.Engines.First(e => e.Id == Trendo.Core.Sys.Web.Engine(_core.DataRoot)) is var eng) // SYS-03-P13
        {
            var web = new Button { Name = "hit-web", Content = $"Im Internet suchen mit {eng.Name}", Margin = new Thickness(0, 8, 0, 0) };
            web.Click += (_, _) => Launch(eng.Url + Uri.EscapeDataString(_query.Trim())); sp.Children.Add(web);
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
