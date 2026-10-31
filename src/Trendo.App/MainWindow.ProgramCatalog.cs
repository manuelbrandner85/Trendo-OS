using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.LogicalTree;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Masterprompt 131 / 19: zentraler Programmkatalog. Die Programme kommen aus den angemeldeten Seiten (ein Programm je Fenster,
/// Reiter desselben Fensters gehören dazu); Fachangaben (Engine, Formate, Tests, Vorbild) aus dem mitgelieferten, aus dem Repository erzeugten Katalog.
/// „Öffnen“ startet das echte Programm.</summary>
public partial class MainWindow
{
    private string _catQuery = "", _catArea = "";

    private void InitProgramCatalog()
    {
        _showMap["programmkatalog"] = ProgramCatalogView; SysTabs.Add(("programmkatalog", "Programmkatalog"));
        PageModules["programmkatalog"] = "SYS-13";
        if (!ExtraPages.Any(p => p.Key == "programmkatalog")) ExtraPages.Add(("Programmkatalog", "programmkatalog", ["programme", "katalog", "alle programme", "übersicht", "funktionen", "dateiformate", "tests"]));
    }

    /// <summary>Programme laut Laufzeit: Seite, Name, Studio, Bereich, Reiter, Werkzeuge und Exportformate der Programm-Hülle.</summary>
    public List<ProgramEntry> ProgramCatalogRuntime()
    {
        string? Listed(string key) => Pages.Concat(ExtraPages).FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Title;
        string Label(string key) => Programs.TryGetValue(key, out var s) ? s.Title : Listed(key) ?? PageTitle(key);
        // nur Seiten, die man öffnen kann: im Startmenü/in der Suche, Studio-Hauptseite oder Reiter eines Studios (keine Einrichtungsschritte)
        var keys = _showMap.Keys.Where(k => ModuleOf(k) is { } m && StudioCatalog.For(m) is not null && k != "programmkatalog"
            && (Listed(k) is not null || StudioPages.Values.Contains(k, StringComparer.OrdinalIgnoreCase) || TabbedPages.Contains(k))).ToList();
        var groups = keys.GroupBy(k => Programs.ContainsKey(k) ? "prog:" + k : WindowGroupOf(k), StringComparer.OrdinalIgnoreCase); // Programme der Programm-Hülle sind eigene Einträge, auch als Reiter eines Studios
        var list = new List<ProgramEntry>();
        foreach (var g in groups)
        {
            var module = ModuleOf(g.First())!; var info = StudioCatalog.For(module)!;
            var main = g.FirstOrDefault(k => string.Equals(StudioPages.GetValueOrDefault(module), k, StringComparison.OrdinalIgnoreCase)) ?? g.First();
            var spec = Programs.GetValueOrDefault(main);
            list.Add(new ProgramEntry
            {
                Id = main, Name = Label(main), Studio = module, StudioName = info.Name, Area = info.Area, Shell = spec is not null,
                Tabs = g.Select(Label).Distinct().ToList(),
                Tools = spec is null ? [] : spec.Ribbon.SelectMany(r => r.Cmds.Select(c => $"{c.Label} ({r.Group})")).Concat(spec.Exports.Select(x => $"{x.Label} (Exportieren)")).ToList(),
                Formats = spec is null ? [] : spec.Exports.Select(x => x.Extension).Append(Trendo.Core.Programs.ProgramDocument.Extension).Distinct().ToList(),
                Version = Trendo.Core.CrashLog.AppVersion(typeof(MainWindow).Assembly),
            });
        }
        return list.OrderBy(e => Array.IndexOf(StudioCatalog.Areas, e.Area)).ThenBy(e => e.StudioName, StringComparer.CurrentCulture).ThenBy(e => e.Name, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>Arbeitsfläche und Werkzeuge einer geöffneten Seite aus den tatsächlich angezeigten Bedienelementen (für den Katalog-Generator).</summary>
    public (List<string> Ui, List<string> Tools) ProgramCatalogInspect(string key)
    {
        var spec = Programs.GetValueOrDefault(key); var made = false;
        if (spec is not null && ProgramActive(key) is null) { ProgramNew(key, (spec.Templates.FirstOrDefault(t => t.Id.Contains("beispiel")) ?? spec.Templates.FirstOrDefault(t => t.Id != "leer"))?.Id); made = true; } // Arbeitsfläche mit Inhalt zeigen
        switch (key) { case "text": TextNew(); break; case "tabelle": SheetNew(); break; case "praesentation": DeckNew(); break; } // Office: mit leerem Dokument statt Startseite
        Show(key);
        // Seiten mit „Beispiel laden“: einmal laden, damit die Arbeitsfläche mit Inhalt erscheint (nur im Katalog-Generator, eigener Datenordner)
        if ((ActivePageRoot ?? this).GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.IsVisible && b.Name is { } n && (n.EndsWith("-demo") || n.EndsWith("-sample"))) is { } demo)
        { demo.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Show(key); }
        var page = ActivePageRoot ?? this;
        var all = page.GetLogicalDescendants().OfType<Control>().Where(c => c.IsVisible).ToList();
        var kinds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var c in all)
            if (c switch
            {
                CadView => "CAD-/3D-Zeichenfläche", MeshView => "3D-Ansicht", FloorPlanView => "Grundriss-Zeichenfläche", CircuitCanvas => "Schaltplan-Zeichenfläche", PcbCanvas => "Platinen-Zeichenfläche",
                FrameCanvas => "Tragwerks-Zeichenfläche", MapCanvas => "Karte", PointCloudView => "Punktwolken-Ansicht", CncPathView => "Werkzeugbahn-Ansicht", ArmView => "Roboter-Ansicht",
                SheetGridView or MeasureGrid => "Tabellenraster", CalTimeGrid => "Kalender-Zeitachse", EditTimeline => "Video-Zeitleiste", ShapeView => "Diagramm/Zeichnung", Border { Name: "learn-card" } => "Lernkarte", StackPanel { Name: "wf-flow" } => "Ablaufplan", LevelCanvas => "Level-Editor", Control { Name: "text-paper" } => "Dokumentseite (Textverarbeitung)", Control { Name: "pdf-page" } => "PDF-Seite", Grid { Name: "cal-month" } => "Monatskalender", Grid { Name: "todo-board" } => "Aufgabentafel", SlideView => "Folienansicht", GraphView or BarChart => "Diagramm", Avalonia.Controls.Image => "Bildansicht",
                TextBox { AcceptsReturn: true } t when Math.Max(t.MinHeight, double.IsNaN(t.Height) ? 0 : t.Height) >= 100 => "Texteditor", _ => null,
            } is { } k) kinds.Add(k);
        var tools = all.OfType<Button>().Where(b => b.Name is { Length: > 0 } n && !n.StartsWith("prog-tab") && !n.StartsWith("dock") && !n.StartsWith("win-") && !n.StartsWith("toast"))
            .Select(LabelOf).Where(l => l is { Length: > 1 } && l.Any(char.IsLetter)).Select(l => l!.Trim()).Distinct().Take(60).ToList();
        if (made) ProgramClose(key, _progActive.GetValueOrDefault(key));
        return (kinds.ToList(), tools);
    }

    private Control ProgramCatalogView()
    {
        var facts = ProgramCatalog.Load().ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
        var all = ProgramCatalogRuntime().Select(e => facts.TryGetValue(e.Id, out var f) ? f with { Name = e.Name, Tabs = e.Tabs, Shell = e.Shell, Version = e.Version } : e).ToList();
        var q = new TextBox { Name = "cat-q", PlaceholderText = "Programm, Funktion oder Format suchen …", Width = 320, Text = _catQuery };
        q.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _catQuery = q.Text ?? ""; Show("programmkatalog"); } };
        var bar = new WrapPanel { Name = "cat-bar", Children = { q, Act("cat-search", "Suchen", () => { _catQuery = q.Text ?? ""; Show("programmkatalog"); }) } };
        foreach (var a in StudioCatalog.Areas.Where(a => all.Any(e => e.Area == a)).Prepend(""))
        {
            var area = a; var on = _catArea == a;
            bar.Children.Add(Act("cat-area-" + (a.Length == 0 ? "alle" : Array.IndexOf(StudioCatalog.Areas, a).ToString()), a.Length == 0 ? "Alle" : a, () => { _catArea = area; Show("programmkatalog"); }, on));
        }
        bool Hit(ProgramEntry e)
        {
            if (_catArea.Length > 0 && e.Area != _catArea) return false;
            var t = _catQuery.Trim(); if (t.Length == 0) return true;
            return new[] { e.Name, e.StudioName, e.Area }.Concat(e.Formats).Concat(e.Tools).Concat(e.Functions.Select(f => f.Name)).Concat(e.Type).Any(x => x.Contains(t, StringComparison.CurrentCultureIgnoreCase));
        }
        var shown = all.Where(Hit).ToList();
        var list = new WrapPanel { Name = "cat-list" };
        var i = 0;
        foreach (var e in shown)
        {
            var key = e.Id; var icon = StudioIcons.For(e.Studio);
            string Chips(IEnumerable<string> xs, int n) { var l = xs.ToList(); return l.Count == 0 ? "–" : string.Join(" · ", l.Take(n)) + (l.Count > n ? $" (+{l.Count - n})" : ""); }
            var open = Act($"cat-open-{i}", "Öffnen", () => Show(key), true);
            var card = new Border
            {
                Name = $"cat-item-{i++}", Width = 372, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow,
                Child = new StackPanel { Spacing = 5, Children = {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                        new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = icon is null ? Accent : new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(icon.C2), 0), new GradientStop(Color.Parse(icon.C1), 1) } },
                            Child = icon is null ? null : Ico(icon.Path, 18, Brushes.White) },
                        Pos(new StackPanel { Margin = new Thickness(10, 0, 0, 0), Children = {
                            new TextBlock { Text = e.Name, Foreground = Text, FontSize = 14.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = $"{e.StudioName} · {e.Area}", Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1),
                        Pos(open, 2) } },
                    Fact("Arbeitsfläche", e.Type.Count == 0 ? "keine eigene Arbeitsfläche erkannt" : string.Join(" · ", e.Type)),
                    Fact("Funktionen", e.Functions.Count > 0 ? $"{e.Functions.Count} im Funktionskatalog" : e.Tools.Count > 0 ? $"{e.Tools.Count} Werkzeuge" : "–"),
                    Fact("Fach-Engine", Chips(e.Engine, 3)),
                    Fact("Formate", Chips(e.Formats, 6)),
                    Fact("Braucht", Chips(e.Dependencies, 2)),
                    Fact("Tests", e.Tests.Count > 0 ? $"{e.Tests.Count} Tests" : "–"),
                    Fact("Vorbild", e.ReaSummary ?? "–"),
                    Fact("Stand", (e.Shell ? "Programm-Hülle · " : "") + (e.Status.Length > 0 ? e.Status : "–")) } },
            };
            list.Children.Add(card);
        }
        if (shown.Count == 0) list.Children.Add(P("Kein Programm passt zur Suche.", Muted, 13));
        var head = P($"{shown.Count} von {all.Count} Programmen · {all.Count(e => e.Shell)} mit Programm-Hülle · Angaben aus Code, Funktionskatalogen und Tests" + (facts.Count == 0 ? " (Katalogdaten fehlen in diesem Build)" : ""), Muted, 12);
        head.Name = "cat-summary";
        return Page("Programmkatalog", new StackPanel { Spacing = 10, Children = { bar, head, list } });

        Control Fact(string k, string v) => new Grid { ColumnDefinitions = new ColumnDefinitions("92,*"), Children = {
            new TextBlock { Text = k, Foreground = Muted, FontSize = 11.5 },
            Pos(new TextBlock { Text = v, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 34 }, 1) } };
    }
}
