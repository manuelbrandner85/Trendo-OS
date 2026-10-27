using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Spiele erstellen (CORE-06) als Programm – vom Retro-Spiel bis AAA+ (Wunsch 27.10.2026, Vorbild: Spielestudio-Ablauf).</summary>
public partial class MainWindow
{
    private string _gameSection = "Übersicht";
    public string GameSection { get => _gameSection; set => _gameSection = value; }
    private const string GK = "spiele-programm";

    private void InitProgGame() => RegisterProgram(KreativTabs, new ProgramSpec(GK, "CORE-06", "Spiele bauen")
    {
        Sentence = "Vom kleinen Retro-Spiel bis zum AAA+-Projekt in Unreal Engine 5 – mit Spielbibel, Figuren, Welt, Missionen und Produktion.",
        Folder = "Spiele",
        Templates = [.. GameDesign.Templates.Select(t => new ProgramTemplate(t.Id, t.Name, $"{t.Hint} · {GameDesign.ScaleOf(t.Scale).Name}", t.Bible))],
        Ribbon =
        [
            ("Spiel", [
                new ProgramCmd("check", "Spiel prüfen", d => _progMsg[GK] = GameDesign.Check(d.Data) is { Count: > 0 } w ? "Hinweise:\n• " + string.Join("\n• ", w) : "Keine Probleme gefunden ✓", true),
                new ProgramCmd("build", "Bauen", d => GameBuild(d)),
                new ProgramCmd("play", "Spielen / Öffnen", d => GamePlay(d)),
            ]),
            ("Hinzufügen", [
                new ProgramCmd("add-char", "Figur", d => { GameAdd(d, "characters", new JsonObject { ["name"] = "Neue Figur", ["role"] = "Nebenfigur", ["text"] = "", ["phases"] = 0 }); _gameSection = "Figuren"; }),
                new ProgramCmd("add-region", "Region", d => { GameAdd(d, "world", new JsonObject { ["name"] = "Neue Region", ["text"] = "" }); _gameSection = "Welt"; }),
                new ProgramCmd("add-quest", "Mission", d => { GameAdd(d, "quests", new JsonObject { ["title"] = "Neue Mission", ["kind"] = "Neben", ["goal"] = "", ["needs"] = "", ["gives"] = "", ["giver"] = "" }); _gameSection = "Missionen"; }),
            ]),
        ],
        Exports =
        [
            new("bible", "Spielbibel", ".md", (d, f) => { File.WriteAllText(f, GameDesign.BibleMarkdown(d.Data)); return f; }),
        ],
        Workspace = GameWorkspace,
        Properties = GameProperties,
        Status = d => { var s = GameDesign.ScaleOf(d.Text("scale")); var w = GameDesign.Check(d.Data).Count; return $"{s.Name} · {(s.Engine == "unreal" ? "Unreal Engine 5" : "Browser")} · {GameDesign.Overall(d.Data)} % geplant · {(w == 0 ? "keine Hinweise" : $"{w} Hinweis(e)")}"; },
        Help = "Wähle oben den Bereich: Übersicht, Spielbibel, Figuren, Welt, Missionen, Produktion oder Bauen. Retro-Spiele sind sofort im Browser spielbar, 3D bis AAA+ entsteht als Unreal-Projekt mit deiner Spielbibel.",
    }, "spiel", "spiele", "game", "retro", "unreal", "aaa", "rpg", "level", "boss", "spielbibel");

    static void GameAdd(ProgramDocument d, string key, JsonObject item) => d.Edit("Hinzufügen", x => { var l = x[key] as JsonArray ?? []; l.Add(item); x[key] = l; });

    void GameBuild(ProgramDocument d)
    {
        var scale = GameDesign.ScaleOf(d.Text("scale"));
        if (scale.Engine == "browser")
        {
            var dir = GameDesign.BuildBrowser(d.Data, WorkFolder("Spiele"));
            d.Set("built", dir, "Bauen"); _progMsg[GK] = "Spielbar gebaut: " + dir; _core.Audit.Append(Principal.LocalOwner.Name, "game.build", Path.GetFileName(dir));
            return;
        }
        var engines = _opt.Isolated ? [] : GameStudio.FindEngines();
        var version = engines.FirstOrDefault()?.Version ?? "5.4";
        var u = GameDesign.BuildUnreal(d.Data, WorkFolder("Spiele"), version);
        d.Set("built", u, "Bauen");
        _progMsg[GK] = engines.Count > 0 ? $"Unreal-Projekt angelegt (Engine {version}): {u}" : $"Unreal-Projekt angelegt: {u} – Unreal Engine 5 ist auf diesem PC nicht installiert. Sie kommt kostenlos über den Epic Games Launcher.";
        _core.Audit.Append(Principal.LocalOwner.Name, "game.build.unreal", Path.GetFileName(u));
    }

    void GamePlay(ProgramDocument d)
    {
        var built = d.Text("built");
        if (built.Length == 0 || !Directory.Exists(built)) throw new InvalidOperationException("Bitte zuerst „Bauen“.");
        if (File.Exists(Path.Combine(built, "index.html"))) { Launch(Path.Combine(built, "index.html")); return; }
        var proj = Directory.GetFiles(built, "*.uproject").FirstOrDefault() ?? throw new InvalidOperationException("Im Ordner liegt kein Spiel.");
        var engine = _opt.Isolated ? null : GameStudio.FindEngines().FirstOrDefault();
        if (engine is null) { Launch(built); _progMsg[GK] = "Unreal Engine 5 ist nicht installiert – der Projektordner wurde geöffnet."; return; }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GameStudio.EditorExe(engine)) { ArgumentList = { proj }, UseShellExecute = false });
        _core.Audit.Append(Principal.LocalOwner.Name, "game.open.unreal", Path.GetFileName(proj));
    }

    private Control GameWorkspace(ProgramDocument d)
    {
        var sections = new[] { "Übersicht", "Spielbibel", "Figuren", "Welt", "Missionen", "Produktion", "Bauen" };
        var bar = new WrapPanel { Name = "game-sections" };
        foreach (var s in sections)
        {
            var on = s == _gameSection; var ss = s;
            var b = new Button { Name = "game-sec-" + s.ToLowerInvariant().Replace("ü", "ue"), Content = s, FontSize = 12.5, Padding = new Thickness(14, 6), Margin = new Thickness(0, 0, 6, 8), CornerRadius = new CornerRadius(16),
                Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1) };
            b.Click += (_, _) => { _gameSection = ss; Show(GK); };
            bar.Children.Add(b);
        }
        Control body = _gameSection switch
        {
            "Spielbibel" => GameBible(d),
            "Figuren" => GameList(d, "characters", "Figuren und Bosse", [("name", "Name", 160), ("role", "Rolle", 0), ("text", "Beschreibung", 320), ("phases", "Phasen", 60)]),
            "Welt" => GameWorld(d),
            "Missionen" => GameList(d, "quests", "Missionen", [("title", "Titel", 170), ("kind", "Art", 0), ("goal", "Ziel", 240), ("needs", "braucht", 130), ("gives", "gibt", 130), ("giver", "Auftraggeber", 120)]),
            "Produktion" => GameProduction(d),
            "Bauen" => GameBuildView(d),
            _ => GameOverview(d),
        };
        return new StackPanel { Spacing = 4, Children = { bar, body } };
    }

    private Control GameOverview(ProgramDocument d)
    {
        var comp = GameDesign.Completeness(d.Data);
        var rings = new WrapPanel { Name = "game-rings" };
        foreach (var (k, v) in comp)
            rings.Children.Add(new Border { Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(12), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Width = 130,
                Child = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { Ring(v, 64, "game-ring-" + k), new TextBlock { Text = k, Foreground = Text, FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Center } } } });
        var scale = GameDesign.ScaleOf(d.Text("scale"));
        var warn = GameDesign.Check(d.Data);
        var w = new StackPanel { Name = "game-warnings", Spacing = 3 };
        foreach (var x in warn) w.Children.Add(P("⚠ " + x, Brush.Parse("#F5A524"), 12));
        if (warn.Count == 0) w.Children.Add(P("✓ Keine Probleme gefunden.", Brush.Parse("#2FBF71"), 12));
        var ms = d.List("milestones").OfType<JsonObject>().ToList(); var done = ms.Count(m => m["done"]?.GetValueKind() == System.Text.Json.JsonValueKind.True);
        return new StackPanel { Spacing = 10, Children = {
            new WrapPanel { Children = {
                Kpi("game-kpi-scale", "Umfang", scale.Name, scale.Hint, "#7C4DFF", "#4B2BC2"),
                Kpi("game-kpi-plan", "Geplant", GameDesign.Overall(d.Data) + " %", "über alle Bereiche", "#3D8BFF", "#1E4FD8"),
                Kpi("game-kpi-ms", "Meilensteine", $"{done} / {ms.Count}", ms.FirstOrDefault(m => m["done"]?.GetValueKind() != System.Text.Json.JsonValueKind.True)?["name"]?.GetValue<string>() ?? "alles geschafft", "#2FBF71", "#167A45") } },
            H("Bereiche", 14), rings, H("Prüfung", 14), w } };
    }

    private Control GameBible(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        void Field(string key, string label, bool multi = false)
        {
            var t = new TextBox { Name = "game-f-" + key, Text = d.Text(key), PlaceholderText = label, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 56 : 0 };
            void Commit() { if ((t.Text ?? "") != d.Text(key)) { d.Set(key, t.Text ?? "", label); Show(GK); } }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(P(label, null, 11.5)); sp.Children.Add(t);
        }
        Field("logline", "Idee in einem Satz", true); Field("genre", "Genre"); Field("loop", "Kernschleife (was macht man immer wieder?)", true);
        var mech = new TextBox { Name = "game-f-mechanics", Text = string.Join(", ", d.List("mechanics").Select(x => x?.GetValue<string>())), PlaceholderText = "Mechaniken, mit Komma getrennt" };
        void CommitMech() { var l = (mech.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); d.Set("mechanics", new JsonArray([.. l.Select(x => (JsonNode)x)]), "Mechaniken"); Show(GK); }
        mech.LostFocus += (_, _) => CommitMech(); OnEnter(mech, CommitMech);
        sp.Children.Add(P("Mechaniken", null, 11.5)); sp.Children.Add(mech);
        Field("controls", "Steuerung"); Field("style", "Stil (Grafik, Farben, Stimmung)"); Field("audience", "Zielgruppe"); Field("platforms", "Plattformen");
        Field("systems", "Systeme (Inventar, Fähigkeiten, Crafting, Speichern …)", true); Field("sound", "Ton und Musik", true); Field("menus", "Menüs und HUD", true);
        return sp;
    }

    /// <summary>Listen-Editor für Figuren und Missionen: jede Zeile eine Karte, Enter oder Verlassen übernimmt.</summary>
    private Control GameList(ProgramDocument d, string key, string title, (string Field, string Label, double Width)[] cols)
    {
        var sp = new StackPanel { Spacing = 6, Children = { H(title, 14) } };
        var items = d.List(key).OfType<JsonObject>().ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var idx = i; var row = new WrapPanel();
            foreach (var (f, label, width) in cols)
            {
                var field = f;
                Control c;
                if (f is "role" or "kind")
                {
                    var opts = f == "role" ? GameDesign.Roles : ["Haupt", "Neben"];
                    var cb = new ComboBox { Name = $"game-{key}-{i}-{f}", ItemsSource = opts, SelectedItem = items[i][f]?.GetValue<string>(), MinWidth = 110 };
                    cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is string v && v != (string?)d.List(key)[idx]?[field]) { d.Edit(label, x => ((JsonObject)((JsonArray)x[key]!)[idx]!)[field] = v); Show(GK); } };
                    c = cb;
                }
                else
                {
                    var val = items[i][f] is { } n ? (n.GetValueKind() == System.Text.Json.JsonValueKind.Number ? ProgramDocument.ToNumber(n).ToString("0") : n.ToString()) : "";
                    var t = new TextBox { Name = $"game-{key}-{i}-{f}", Text = val, PlaceholderText = label, Width = width };
                    void Commit()
                    {
                        var cur = d.List(key)[idx]?[field]; var curText = cur is null ? "" : cur.GetValueKind() == System.Text.Json.JsonValueKind.Number ? ProgramDocument.ToNumber(cur).ToString("0") : cur.ToString();
                        if ((t.Text ?? "") == curText) return;
                        d.Edit(label, x => ((JsonObject)((JsonArray)x[key]!)[idx]!)[field] = field == "phases" ? (int.TryParse(t.Text, out var p) ? Math.Clamp(p, 0, 9) : 0) : (JsonNode)(t.Text ?? ""));
                        Show(GK);
                    }
                    t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
                    c = t;
                }
                c.Margin = new Thickness(0, 0, 6, 4); ToolTip.SetTip(c, label); row.Children.Add(c);
            }
            var del = new Button { Name = $"game-{key}-{i}-del", Content = "Entfernen", FontSize = 11.5, Padding = new Thickness(8, 3) };
            del.Click += (_, _) => { d.Edit("Entfernen", x => ((JsonArray)x[key]!).RemoveAt(idx)); Show(GK); };
            row.Children.Add(del);
            sp.Children.Add(new Border { Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Background = Brush.Parse(C("#14FFFFFF", "#08000000")), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = row });
        }
        if (items.Count == 0) sp.Children.Add(P("Noch leer – oben im Menüband unter „Hinzufügen“ anlegen.", Text, 12));
        return sp;
    }

    private Control GameWorld(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 10, Children = { GameList(d, "world", "Regionen", [("name", "Name", 180), ("text", "Beschreibung", 420)]) } };
        var browser = d.Text("browser");
        if (GameDesign.ScaleOf(d.Text("scale")).Engine != "browser" || browser is not ("jump" or "breakout")) return sp;
        // Kachel-Leveleditor: Klick setzt oder entfernt einen Stein/ein Hindernis
        var rows = d.List("level").Select(x => x?.GetValue<string>() ?? "").ToList();
        if (rows.Count == 0) rows = [.. browser == "jump" ? GameStudio.DefaultJump : GameStudio.DefaultBreakout];
        var width = rows.Max(r => r.Length);
        var grid = new UniformGrid { Name = "game-level", Columns = width, Rows = rows.Count, Width = Math.Min(900, width * 22) };
        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < width; c++)
            {
                var rr = r; var cc = c; var on = c < rows[r].Length && rows[r][c] == '#';
                var cell = new Button { Name = $"game-cell-{r}-{c}", Height = 22, Padding = new Thickness(0), Margin = new Thickness(1), CornerRadius = new CornerRadius(3),
                    Background = Brush.Parse(on ? (browser == "jump" ? "#F5A524" : "#E5484D") : C("#1AFFFFFF", "#11000000")), BorderThickness = new Thickness(0) };
                cell.Click += (_, _) =>
                {
                    var l = rows.Select(x => x.PadRight(width, '.').ToCharArray()).ToList();
                    l[rr][cc] = l[rr][cc] == '#' ? '.' : '#';
                    d.Set("level", new JsonArray([.. l.Select(x => (JsonNode)new string(x))]), "Level"); Show(GK);
                };
                grid.Children.Add(cell);
            }
        var add = Act("game-level-col", browser == "jump" ? "Strecke verlängern" : "Zeile hinzufügen", () =>
        {
            var l = browser == "jump" ? rows.Select(x => x + "....").ToList() : [.. rows, new string('#', width)];
            if (l.Count > 12 || l.Max(x => x.Length) > 60) { _progMsg[GK] = "Größer geht es im Browser-Spiel nicht (12 Zeilen, 60 Spalten)."; Show(GK); return; }
            d.Set("level", new JsonArray([.. l.Select(x => (JsonNode)x)]), "Level"); Show(GK);
        });
        sp.Children.Add(new StackPanel { Spacing = 6, Children = { H(browser == "jump" ? "Level: Hindernisse setzen" : "Level: Steinmuster", 14), P("Klick auf ein Feld setzt oder entfernt es. Danach „Bauen“ und „Spielen“.", null, 12),
            new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = grid }, add } });
        return sp;
    }

    private Control GameProduction(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6, Children = { H("Meilensteine", 14), P("Wie in einem Spielestudio: Jeder Meilenstein ist ein Tor. Erst wenn er erreicht ist, geht es weiter.", null, 12) } };
        var ms = d.List("milestones").OfType<JsonObject>().ToList();
        for (var i = 0; i < ms.Count; i++)
        {
            var idx = i; var on = ms[i]["done"]?.GetValueKind() == System.Text.Json.JsonValueKind.True;
            var cb = new CheckBox { Name = $"game-ms-{i}", IsChecked = on, Content = new TextBlock { Text = $"{i + 1}. {ms[i]["name"]?.GetValue<string>()}", Foreground = Text, FontSize = 13 } };
            cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != on) { d.Edit("Meilenstein", x => ((JsonObject)((JsonArray)x["milestones"]!)[idx]!)["done"] = cb.IsChecked == true); Show(GK); } };
            sp.Children.Add(cb);
        }
        return sp;
    }

    private Control GameBuildView(ProgramDocument d)
    {
        var scale = GameDesign.ScaleOf(d.Text("scale"));
        var sp = new StackPanel { Spacing = 8, Children = { H(scale.Engine == "browser" ? "Spielbar im Browser" : "Unreal Engine 5", 14) } };
        if (scale.Engine == "browser")
        {
            sp.Children.Add(P("„Bauen“ legt ein spielbares Spiel an (HTML und JavaScript, ohne Internet). „Spielen“ öffnet es. Grafik und Farben stellst du danach im Reiter „Werkstatt“ ein.", null, 12));
        }
        else
        {
            var engines = _opt.Isolated ? [] : GameStudio.FindEngines();
            sp.Children.Add(P(engines.Count > 0 ? $"Gefunden: Unreal Engine {string.Join(", ", engines.Select(e => e.Version))}." : "Unreal Engine 5 ist auf diesem PC nicht installiert. Sie ist kostenlos und kommt über den Epic Games Launcher.", engines.Count > 0 ? Text : Brush.Parse("#F5A524"), 12));
            sp.Children.Add(P("„Bauen“ legt ein Unreal-Projekt mit deiner Spielbibel an (Ordner „Design“). " + (scale.Id is "aa" or "aaa" ? "Für AA und AAA+ sind Lumen (globale Beleuchtung), virtuelle Schatten und Distanzfelder eingeschaltet; Nanite-Modelle kommen aus dem 3D-Studio und Blender." : ""), null, 12));
            if (engines.Count == 0 && !_opt.Isolated) sp.Children.Add(Act("game-epic", "Epic Games Launcher (offizielle Seite)", () => Launch("https://www.unrealengine.com/de/download")));
        }
        var built = d.Text("built");
        sp.Children.Add(P(built.Length > 0 ? "Zuletzt gebaut: " + built : "Noch nicht gebaut.", Text, 12));
        sp.Children.Add(new WrapPanel { Children = { Act("game-build", "Bauen", () => ProgramRun(GK, () => GameBuild(d)), true), new Border { Width = 6 }, Act("game-play", scale.Engine == "browser" ? "Spielen" : "In Unreal öffnen", () => ProgramRun(GK, () => GamePlay(d))) } });
        return sp;
    }

    private Control GameProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        var title = new TextBox { Name = "game-p-title", Text = d.Text("title") };
        void CommitTitle() { if ((title.Text ?? "") != d.Text("title") && !string.IsNullOrWhiteSpace(title.Text)) { d.Set("title", title.Text.Trim(), "Spieltitel"); Show(GK); } }
        title.LostFocus += (_, _) => CommitTitle(); OnEnter(title, CommitTitle);
        sp.Children.Add(P("Spieltitel", null, 11.5)); sp.Children.Add(title);
        var scale = new ComboBox { Name = "game-p-scale", ItemsSource = GameDesign.Scales.Select(s => s.Name).ToList(), SelectedIndex = Array.FindIndex(GameDesign.Scales, s => s.Id == d.Text("scale")), HorizontalAlignment = HorizontalAlignment.Stretch };
        scale.SelectionChanged += (_, _) =>
        {
            if (scale.SelectedIndex < 0) return; var s = GameDesign.Scales[scale.SelectedIndex]; if (s.Id == d.Text("scale")) return;
            d.Edit("Umfang", x => { x["scale"] = s.Id; x["milestones"] = new JsonArray([.. GameDesign.Milestones(s.Id).Select(m => (JsonNode)new JsonObject { ["name"] = m, ["done"] = false })]); });
            Show(GK);
        };
        sp.Children.Add(P("Umfang", null, 11.5)); sp.Children.Add(scale);
        sp.Children.Add(P(GameDesign.ScaleOf(d.Text("scale")).Hint, null, 11));
        if (GameDesign.ScaleOf(d.Text("scale")).Engine == "browser")
        {
            var kinds = new (string Id, string Name)[] { ("jump", "Hüpfspiel"), ("breakout", "Mauerbrecher"), ("snake", "Schlange") };
            var kind = new ComboBox { Name = "game-p-browser", ItemsSource = kinds.Select(k => k.Name).ToList(), SelectedIndex = Array.FindIndex(kinds, k => k.Id == d.Text("browser")), HorizontalAlignment = HorizontalAlignment.Stretch };
            kind.SelectionChanged += (_, _) => { if (kind.SelectedIndex >= 0 && kinds[kind.SelectedIndex].Id != d.Text("browser")) { var id = kinds[kind.SelectedIndex].Id; d.Edit("Spielart", x => { x["browser"] = id; x["level"] = new JsonArray([.. (id == "jump" ? GameStudio.DefaultJump : id == "breakout" ? GameStudio.DefaultBreakout : []).Select(r => (JsonNode)r)]); }); Show(GK); } };
            sp.Children.Add(P("Spielart", null, 11.5)); sp.Children.Add(kind);
        }
        var comp = GameDesign.Completeness(d.Data);
        sp.Children.Add(H("Stand", 12));
        foreach (var (k, v) in comp) sp.Children.Add(new StackPanel { Spacing = 1, Children = { Row(P(k, Text, 11.5), P(v + " %", null, 11)), new ProgressBar { Value = v, Maximum = 100, Height = 5 } } });
        return sp;
    }
}
