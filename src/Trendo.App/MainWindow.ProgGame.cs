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
        StartTop = () => GamePromptBar(null), // Spiele-Prompt: Spiel aus einem Satz
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
        var sections = new List<string> { "Übersicht", "Spielbibel", "Figuren", "Welt", "Missionen", "Produktion", "Bauen", "Veröffentlichen" };
        if (d.Text("browser") is "plattform" or "jump" or "breakout") sections.Insert(4, "Level");
        if (d.Text("browser") == "plattform") sections.Insert(5, "Test"); // Testmodus mit Spielbarkeits-Prüfung, Fehlern und Notizen
        if (d.Text("browser") is "plattform" or "jump" or "breakout") sections.Insert(sections.IndexOf("Level") + 1, "Assets"); // freie Assets (G1–G3)
        if ((_gameSection is "Level" or "Test" or "Assets") && !sections.Contains(_gameSection)) _gameSection = "Übersicht";
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
            "Level" => GameLevel(d),
            "Test" => GameTestView(d),
            "Assets" => GameAssetsView(d),
            "Bauen" => GameBuildView(d),
            "Veröffentlichen" => GameReleaseView(d), // Ziele, Checkliste, Pakete, Hochladen mit eigenem Konto
            _ => GameOverview(d),
        };
        return new StackPanel { Spacing = 4, Children = { GamePromptBar(d), bar, body } };
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
                Kpi("game-kpi-ms", "Meilensteine", $"{done} / {ms.Count}", ms.FirstOrDefault(m => m["done"]?.GetValueKind() != System.Text.Json.JsonValueKind.True)?["name"].AsText() ?? "alles geschafft", "#2FBF71", "#167A45") } },
            H("Phase", 14), GamePhaseBar(d, false), H("Bereiche", 14), rings, H("Prüfung", 14), w } };
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
        var mech = new TextBox { Name = "game-f-mechanics", Text = string.Join(", ", d.List("mechanics").Select(x => x.AsText())), PlaceholderText = "Mechaniken, mit Komma getrennt" };
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
                    var cb = new ComboBox { Name = $"game-{key}-{i}-{f}", ItemsSource = opts, SelectedItem = items[i][f].AsText(), MinWidth = 110 };
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
        var rows = d.List("level").Select(x => x.AsText() ?? "").ToList();
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
        var sp = new StackPanel { Spacing = 6, Children = { H("Phasen mit Abnahme", 14), GamePhaseBar(d, true), new Border { Height = 6 }, GameRolesPanel(d), H("Meilensteine", 14), P("Wie in einem Spielestudio: Jeder Meilenstein ist ein Tor. Erst wenn er erreicht ist, geht es weiter.", null, 12) } };
        var ms = d.List("milestones").OfType<JsonObject>().ToList();
        for (var i = 0; i < ms.Count; i++)
        {
            var idx = i; var on = ms[i]["done"]?.GetValueKind() == System.Text.Json.JsonValueKind.True;
            var cb = new CheckBox { Name = $"game-ms-{i}", IsChecked = on, Content = new TextBlock { Text = $"{i + 1}. {ms[i]["name"].AsText()}", Foreground = Text, FontSize = 13 } };
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
            sp.Children.Add(P("„Bauen“ legt ein spielbares Unreal-Spiel an: C++-Code mit deinen Leveln und Regeln plus Spielbibel (Ordner „Design“). " + (scale.Id is "aa" or "aaa" ? "Für AA und AAA+ sind Lumen (globale Beleuchtung), virtuelle Schatten und Distanzfelder eingeschaltet; Nanite-Modelle kommen aus dem 3D-Studio und Blender." : ""), null, 12));
            if (engines.Count == 0 && !_opt.Isolated) sp.Children.Add(Act("game-epic", "Epic Games Launcher (offizielle Seite)", () => Launch("https://www.unrealengine.com/de/download")));
        }
        var built = d.Text("built");
        sp.Children.Add(P(built.Length > 0 ? "Zuletzt gebaut: " + built : "Noch nicht gebaut.", Text, 12));
        sp.Children.Add(new WrapPanel { Children = { Act("game-build", "Bauen", () => ProgramRun(GK, () => GameBuild(d)), true), new Border { Width = 6 }, Act("game-play", scale.Engine == "browser" ? "Spielen" : "In Unreal öffnen", () => ProgramRun(GK, () => GamePlay(d))) } });
        if (scale.Engine == "unreal") sp.Children.Add(GameUnrealView(d)); // 3D-Spiel in Unreal: kompilieren, prüfen, spielen
        sp.Children.Add(GameEnginesView(d)); // Paket 01 S1-01: Unreal, Unity, Godot, Blender
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
            var kinds = new (string Id, string Name)[] { ("jump", "Hüpfspiel"), ("breakout", "Mauerbrecher"), ("snake", "Schlange"), ("plattform", "Plattformer") };
            var kind = new ComboBox { Name = "game-p-browser", ItemsSource = kinds.Select(k => k.Name).ToList(), SelectedIndex = Array.FindIndex(kinds, k => k.Id == d.Text("browser")), HorizontalAlignment = HorizontalAlignment.Stretch };
            kind.SelectionChanged += (_, _) => { if (kind.SelectedIndex >= 0 && kinds[kind.SelectedIndex].Id != d.Text("browser")) { var id = kinds[kind.SelectedIndex].Id; d.Edit("Spielart", x => { x["browser"] = id; x["level"] = new JsonArray([.. (id == "jump" ? GameStudio.DefaultJump : id == "breakout" ? GameStudio.DefaultBreakout : id == "plattform" ? GameStudio.DefaultPlatform : []).Select(r => (JsonNode)r)]); }); Show(GK); } };
            sp.Children.Add(P("Spielart", null, 11.5)); sp.Children.Add(kind);
        }
        var comp = GameDesign.Completeness(d.Data);
        sp.Children.Add(H("Stand", 12));
        foreach (var (k, v) in comp) sp.Children.Add(new StackPanel { Spacing = 1, Children = { Row(P(k, Text, 11.5), P(v + " %", null, 11)), new ProgressBar { Value = v, Maximum = 100, Height = 5 } } });
        return sp;
    }

    // ---------- Level-Editor (Masterprompt 131: Spiele-Werkstatt mit Arbeitsfläche) ----------
    public char GameTile { get; set; } = '#';
    public LevelCanvas? GameLevelForTests { get; private set; }

    // ---------- Spiel-Editor (Masterprompt 131, Runde 7): Objekte auswählen, Eigenschaften, Probespielen im Fenster ----------
    public (int X, int Y)? GameObjSel { get; set; }
    PlatformSim? _gamePlay;
    public PlatformSim? GamePlaytest => _gamePlay;
    public PlaytestCanvas? GamePlaytestCanvas { get; private set; }
    static List<string> GameRows(ProgramDocument d)
    {
        var rows = d.List("level").Select(x => x.AsText() ?? "").Where(x => x.Length > 0).ToList();
        var kind = d.Text("browser");
        return rows.Count > 0 ? rows : [.. kind == "plattform" ? GameStudio.DefaultPlatform : kind == "breakout" ? GameStudio.DefaultBreakout : GameStudio.DefaultJump];
    }

    // ---------- Animationen je Zustand (05.11.2026, Vorbild: AnimatedSprite in Godot, Objekt-Animationen in GDevelop) ----------
    public static readonly (string Id, string Label)[] GameAnimStates = [.. PlatformSim.States.Select(s => (s.Id, s.Label))];
    /// <summary>Animation eines Zustands setzen: Bildstreifen (nebeneinander), Anzahl Bilder, Bilder je Sekunde. Leerer Pfad entfernt sie.</summary>
    public void GameSetAnim(ProgramDocument d, string state, string? file, int frames, int fps)
    {
        if (!GameAnimStates.Any(s => s.Id == state)) throw new ArgumentException($"Unbekannter Zustand „{state}“.");
        if (!string.IsNullOrWhiteSpace(file))
        {
            if (!File.Exists(file)) throw new FileNotFoundException("Bild nicht gefunden.", file);
            if (Path.GetExtension(file).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif")) throw new ArgumentException("Bitte ein Bild (PNG, JPG, WebP oder GIF).");
        }
        frames = Math.Clamp(frames, 1, 32); fps = Math.Clamp(fps, 1, 30);
        d.Edit("Animation " + state, x =>
        {
            var a = x["anims"] as JsonObject ?? [];
            var old = a[state] as JsonObject;
            var f = file ?? old?["file"].AsText() ?? "";
            if (f.Length == 0 || file == "") a.Remove(state); else a[state] = new JsonObject { ["file"] = f, ["frames"] = frames, ["fps"] = fps };
            x["anims"] = a;
        });
    }
    static Dictionary<string, (Avalonia.Media.Imaging.Bitmap, int, int)> GameAnimBitmaps(ProgramDocument d)
    {
        var r = new Dictionary<string, (Avalonia.Media.Imaging.Bitmap, int, int)>();
        foreach (var (k, v) in d.Data["anims"] as JsonObject ?? [])
            if (v is JsonObject o && o["file"].AsText() is { Length: > 0 } f && File.Exists(f))
                try { r[k] = (new Avalonia.Media.Imaging.Bitmap(f), (int)ProgramDocument.ToNumber(o["frames"]), (int)ProgramDocument.ToNumber(o["fps"])); } catch { /* kaputtes Bild: Figur bleibt ein Rechteck */ }
        return r;
    }

    private Control GameAnimPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-anim-panel", Spacing = 4 };
        sp.Children.Add(H("Figur: Animationen", 13));
        sp.Children.Add(P("Je Zustand ein Bildstreifen (Bilder nebeneinander). Fehlt einer, zeigt das Spiel „stehen“.", Muted, 11));
        var anims = d.Data["anims"] as JsonObject ?? [];
        foreach (var (id, label) in GameAnimStates)
        {
            var o = anims[id] as JsonObject; var st = id;
            var frames = new TextBox { Name = $"game-anim-frames-{id}", Text = ((int)(o is null ? 1 : ProgramDocument.ToNumber(o["frames"]))).ToString(), Width = 42 };
            var fps = new TextBox { Name = $"game-anim-fps-{id}", Text = ((int)(o is null ? 8 : ProgramDocument.ToNumber(o["fps"]))).ToString(), Width = 42 };
            void Commit()
            {
                if (o is null) return;
                if (!int.TryParse(frames.Text, out var fr) || !int.TryParse(fps.Text, out var fp)) { _progMsg[GK] = "Bitte ganze Zahlen."; return; }
                if (fr != (int)ProgramDocument.ToNumber(o["frames"]) || fp != (int)ProgramDocument.ToNumber(o["fps"])) ProgramRun(GK, () => GameSetAnim(d, st, null, fr, fp));
            }
            frames.LostFocus += (_, _) => Commit(); fps.LostFocus += (_, _) => Commit(); OnEnter(frames, Commit); OnEnter(fps, Commit);
            var pick = Act($"game-anim-pick-{id}", o is null ? "Bild…" : "✓ " + Path.GetFileName(o["file"].AsText() ?? ""), async () =>
            {
                var got = PickOverride is { } fake ? await fake(new PickRequest($"game-anim-{st}", false, false, ["png", "jpg", "webp", "gif"]))
                    : (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Bildstreifen für „" + label + "“", FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"] }] }))
                        .Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToArray();
                if (got is { Length: > 0 }) ProgramRun(GK, () => GameSetAnim(d, st, got[0], int.TryParse(frames.Text, out var a) ? a : 1, int.TryParse(fps.Text, out var b) ? b : 8));
            });
            var row = new WrapPanel { Children = { new TextBlock { Text = label, Width = 64, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, pick } };
            if (o is not null)
            {
                row.Children.Add(new Border { Width = 4 }); row.Children.Add(frames); row.Children.Add(P(" Bilder ", Muted, 11)); row.Children.Add(fps); row.Children.Add(P(" /s ", Muted, 11));
                row.Children.Add(Act($"game-anim-del-{id}", "✕", () => ProgramRun(GK, () => GameSetAnim(d, st, "", 1, 8))));
            }
            sp.Children.Add(row);
        }
        return sp;
    }

    // ---------- Mehrere Level und Ebenen (05.11.2026, Vorbild: Szenen-/Levelliste in GDevelop und Godot, TileMap-Ebenen) ----------
    // Level 1 liegt wie bisher in „level“/„decor“, weitere Level in „levels“: [{name, rows, decor}].
    int _gameLevelIdx;
    /// <summary>Aktives Level im Editor (0 = erstes).</summary>
    public int GameLevelIndex { get => _gameLevelIdx; set => _gameLevelIdx = Math.Max(0, value); }
    /// <summary>Aktive Ebene: „spiel“ (Boden, Gegner …) oder „hintergrund“ (nur Deko).</summary>
    public string GameLayer { get; set; } = "spiel";
    static JsonArray GameMore(ProgramDocument d) => d.Data["levels"] as JsonArray ?? [];
    public static int GameLevelCount(ProgramDocument d) => 1 + GameMore(d).Count;
    static List<string> Lines(JsonNode? n) => (n as JsonArray ?? []).Select(x => x.AsText() ?? "").ToList();
    public static List<string> GameLevelRows(ProgramDocument d, int i) => i == 0 ? GameRows(d) : Lines((GameMore(d).ElementAtOrDefault(i - 1) as JsonObject)?["rows"]) is { Count: > 0 } r ? r : ["S......Z", "########"];
    public static List<string> GameDecorRows(ProgramDocument d, int i)
    {
        var rows = GameLevelRows(d, i); var cols = rows.Max(x => x.Length);
        var dec = i == 0 ? Lines(d.Data["decor"]) : Lines((GameMore(d).ElementAtOrDefault(i - 1) as JsonObject)?["decor"]);
        return [.. Enumerable.Range(0, rows.Count).Select(y => (y < dec.Count ? dec[y] : "").PadRight(cols, '.')[..cols])];
    }
    public static string GameLevelName(ProgramDocument d, int i) => (i == 0 ? d.Text("levelName") : (GameMore(d).ElementAtOrDefault(i - 1) as JsonObject)?["name"].AsText() ?? "") is { Length: > 0 } n ? n : $"Level {i + 1}";
    static JsonArray Arr(IEnumerable<string> l) => new([.. l.Select(x => (JsonNode)x)]);
    void GameSetRows(ProgramDocument d, int i, List<string> rows, string label, string key = "rows") => d.Edit(label, x =>
    {
        if (i == 0) { x[key == "rows" ? "level" : "decor"] = Arr(rows); return; }
        if ((x["levels"] as JsonArray)?.ElementAtOrDefault(i - 1) is JsonObject o) o[key] = Arr(rows);
    });
    /// <summary>Schlüssel-Vorsatz für Objekt-Eigenschaften (Level 2 und später „2:x,y:…“).</summary>
    static string GameKeyPrefix(int i) => i == 0 ? "" : $"{i + 1}:";
    public void GameAddLevel(ProgramDocument d) => d.Edit("Level hinzufügen", x =>
    {
        var l = x["levels"] as JsonArray ?? [];
        if (l.Count >= 19) throw new ArgumentException("Höchstens 20 Level.");
        l.Add(new JsonObject { ["name"] = $"Level {l.Count + 2}", ["rows"] = Arr(["............................", "............................", "..........o.o...............", "S..................E.......Z", "############################"]) });
        x["levels"] = l; _gameLevelIdx = l.Count;
    });
    public void GameDeleteLevel(ProgramDocument d, int i)
    {
        if (i == 0) throw new ArgumentException("Das erste Level bleibt – lösche stattdessen ein späteres.");
        d.Edit("Level löschen", x =>
        {
            (x["levels"] as JsonArray)?.RemoveAt(i - 1);
            if (x["objects"] is JsonObject o) // Eigenschaften der späteren Level rücken nach
            {
                var all = o.Select(kv => (kv.Key, V: kv.Value?.DeepClone())).ToList(); o.Clear();
                foreach (var (k, v) in all)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(k, @"^(\d+):(\d+,\d+:.*)$");
                    if (!m.Success) { o[k] = v; continue; }
                    var n = int.Parse(m.Groups[1].Value); if (n == i + 1) continue;
                    o[(n > i + 1 ? $"{n - 1}:" : $"{n}:") + m.Groups[2].Value] = v;
                }
            }
        });
        _gameLevelIdx = Math.Min(_gameLevelIdx, GameLevelCount(d) - 1);
    }
    /// <summary>Level mit dem davor tauschen (Reihenfolge im Spiel). Das erste Level bleibt vorn.</summary>
    public void GameMoveLevelUp(ProgramDocument d, int i)
    {
        if (i < 2) throw new ArgumentException("Das erste Level bleibt vorn.");
        d.Edit("Level verschieben", x => { if (x["levels"] is JsonArray l) { var a = l[i - 1]!.DeepClone(); var b = l[i - 2]!.DeepClone(); l[i - 2] = a; l[i - 1] = b; } });
        _gameLevelIdx = i - 1;
    }
    public void GameRenameLevel(ProgramDocument d, int i, string name)
    {
        name = name.Trim(); if (name.Length == 0) throw new ArgumentException("Bitte einen Namen eingeben.");
        if (name.Length > 40) name = name[..40];
        d.Edit("Level umbenennen", x => { if (i == 0) x["levelName"] = name; else if ((x["levels"] as JsonArray)?.ElementAtOrDefault(i - 1) is JsonObject o) o["name"] = name; });
    }
    static Dictionary<string, double> GameProps(ProgramDocument d) =>
        (d.Data["objects"] as JsonObject ?? []).Where(kv => kv.Value is JsonValue).ToDictionary(kv => kv.Key, kv => ProgramDocument.ToNumber(kv.Value));
    /// <summary>Probespiel starten (öffentlich für Tests und die KI).</summary>
    public void GamePlayHere(ProgramDocument d)
    {
        var rows = GameRows(d);
        if (GameStudio.CheckPlatform(rows) is { Count: > 0 } w && w.Any(x => x.StartsWith("Kein"))) throw new InvalidOperationException(w.First(x => x.StartsWith("Kein")));
        var more = Enumerable.Range(1, GameLevelCount(d) - 1).Select(i => (IReadOnlyList<string>)GameLevelRows(d, i)).ToList();
        _gamePlay = new PlatformSim(rows, (int)d.Number("speed", 5), (int)d.Number("lives", 3), GameProps(d), (int)d.Number("timeLimit", 0), GameDesign.Rules(d.Data), more, GameDesign.Abilities(d.Data), GameDesign.Texts(d.Data), GameDesign.Weapon(d.Data));
        _core.Audit.Append(Principal.LocalOwner.Name, "game.playtest", d.Text("title"));
    }

    private Control GameLevel(ProgramDocument d)
    {
        var kind = d.Text("browser");
        if (kind == "plattform" && _gamePlay is not null) return GamePlayView(d);
        if (kind != "plattform") { _gameLevelIdx = 0; GameLayer = "spiel"; }
        _gameLevelIdx = Math.Min(_gameLevelIdx, GameLevelCount(d) - 1);
        var li = _gameLevelIdx; var deco = kind == "plattform" && GameLayer == "hintergrund";
        var rows = GameLevelRows(d, li);
        var tiles = deco
            ? new (char Ch, string Id, string Label)[] { ('w', "wolke", "Wolke"), ('m', "berg", "Berg"), ('t', "baum", "Baum"), ('b', "busch", "Busch"), ('g', "gras", "Gras"), ('.', "radierer", "Radierer") }
            : kind == "plattform"
            ? new (char Ch, string Id, string Label)[] { (LevelCanvas.PickTool, "auswahl", "⬚ Auswählen"), ('#', "boden", "Boden"), ('o', "muenze", "Münze"), ('E', "gegner", "Gegner"), ('^', "stacheln", "Stacheln"), ('S', "start", "Start"), ('F', "feder", "Sprungfeder"), ('C', "speicher", "Speicherpunkt"), ('K', "schluessel", "Schlüssel"), ('D', "tuer", "Tür"), ('G', "gegenstand", "Gegenstand"), ('N', "figur", "Sprechende Figur"), ('V', "fahrzeug", "Fahrzeug"), ('Z', "ziel", "Ziel"), ('.', "radierer", "Radierer") }
            : [('#', "boden", kind == "breakout" ? "Stein" : "Hindernis"), ('.', "radierer", "Radierer")];
        if (!tiles.Any(t => t.Ch == GameTile)) GameTile = tiles[deco ? 0 : kind == "plattform" ? 1 : 0].Ch;
        var canvas = new LevelCanvas { Name = "game-level", Rows = deco ? GameDecorRows(d, li) : rows, Tile = GameTile, Cell = kind == "jump" ? 18 : 22, Selected = deco ? null : GameObjSel,
            Ghost = kind != "plattform" ? null : deco ? rows : GameDecorRows(d, li) };
        GameLevelForTests = canvas;
        canvas.LevelChanged = l => ProgramRun(GK, () => GameSetRows(d, li, l, deco ? "Hintergrund malen" : "Level malen", deco ? "decor" : "rows"));
        canvas.Picked = (x, y) => { GameObjSel = (x, y); Show(GK); };
        var palette = new WrapPanel { Name = "game-tiles" };
        foreach (var (ch, id, label) in tiles)
        {
            var c = ch; var on = GameTile == ch;
            var b = new Button { Name = "game-tile-" + id, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) };
            if (on) { b.Background = Accent; b.Foreground = Brushes.White; }
            b.Click += (_, _) => { GameTile = c; Show(GK); };
            palette.Children.Add(b);
        }
        void Resize(int by) => ProgramRun(GK, () =>
        {
            var cols = Math.Clamp(rows.Max(x => x.Length) + by, 8, 200);
            GameSetRows(d, li, [.. rows.Select(x => x.Length >= cols ? x[..cols] : x.PadRight(cols, '.'))], by > 0 ? "Level verlängern" : "Level kürzen");
        });
        var more = new Button { Name = "game-level-wider", Content = "＋ 10 Spalten" }; more.Click += (_, _) => Resize(10);
        var less = new Button { Name = "game-level-narrower", Content = "－ 10 Spalten" }; less.Click += (_, _) => Resize(-10);
        var test = new Button { Name = "game-level-test", Content = kind == "plattform" ? "Im Browser testen" : "▶ Testen" };
        if (kind != "plattform") { test.Background = Accent; test.Foreground = Brushes.White; }
        test.Click += (_, _) => ProgramRun(GK, () => { if (d.List("level").Count == 0) d.Set("level", new JsonArray([.. rows.Select(x => (JsonNode)x)]), "Level"); GameBuild(d); GamePlay(d); });
        var tools = new WrapPanel { Children = { palette, new Border { Width = 12 }, more, new Border { Width = 4 }, less, new Border { Width = 12 } } };
        if (kind == "plattform")
        {
            var play = new Button { Name = "game-level-play", Content = "▶ Im Fenster spielen", Background = Accent, Foreground = Brushes.White, Margin = new Thickness(0, 0, 4, 0) };
            play.Click += (_, _) => ProgramRun(GK, () => GamePlayHere(d));
            tools.Children.Add(play);
        }
        tools.Children.Add(test);
        Control? levelBar = kind == "plattform" ? GameLevelBar(d, li) : null;
        var hints = new StackPanel { Name = "game-level-hints", Spacing = 2 };
        foreach (var h in kind == "plattform" ? GameStudio.CheckPlatform(rows) : []) hints.Children.Add(P("⚠ " + h, Brush.Parse("#F5A524"), 12));
        if (hints.Children.Count == 0 && kind == "plattform") hints.Children.Add(P("✓ Start, Ziel und Boden sind da – „Im Fenster spielen“ startet sofort, „Im Browser testen“ baut das fertige Spiel.", Brush.Parse("#2FBF71"), 12));
        var stats = string.Concat(rows);
        var info = P(kind == "plattform" ? $"{rows.Max(x => x.Length)} × {rows.Count} Kacheln · {stats.Count(ch => ch == 'o')} Münzen · {stats.Count(ch => ch == 'E')} Gegner · {stats.Count(ch => ch == '^')} Stacheln" : $"{rows.Max(x => x.Length)} × {rows.Count} Kacheln", Muted, 11.5);
        Control scene = new ScrollViewer { Content = canvas, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 16 * 22 + 20, VerticalAlignment = VerticalAlignment.Top };
        if (kind == "plattform")
            scene = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,260"), Children = { scene, Pos(GameObjPanel(d, rows), 2) } };
        var outer = new StackPanel { Spacing = 8 };
        if (levelBar is not null) outer.Children.Add(levelBar);
        outer.Children.Add(new StackPanel { Spacing = 8, Children = {
            tools,
            new TextBlock { Text = deco ? "Hintergrund: Wolken, Berge, Bäume, Büsche und Gras sind nur Deko – die Figur läuft hindurch. Die Spiel-Ebene ist blass zu sehen." : kind == "plattform" ? "Linke Maustaste malt (auch ziehen), rechte radiert · „Auswählen“ zeigt rechts die Eigenschaften eines Objekts" : "Linke Maustaste malt (auch ziehen), rechte radiert. Jeder Strich ist ein Schritt im Rückgängig.", Foreground = Muted, FontSize = 11.5 },
            scene, info, hints } });
        return outer;
    }

    /// <summary>Leiste über dem Level: Level-Reiter (wie die Szenenliste in GDevelop), Hinzufügen/Umbenennen/Verschieben/Löschen und Ebenen-Schalter.</summary>
    private Control GameLevelBar(ProgramDocument d, int li)
    {
        var tabs = new WrapPanel { Name = "game-levels" };
        for (var i = 0; i < GameLevelCount(d); i++)
        {
            var ii = i; var on = i == li;
            var b = new Button { Name = $"game-lv-{i + 1}", Content = GameLevelName(d, i), FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(8),
                Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1) };
            b.Click += (_, _) => { _gameLevelIdx = ii; GameObjSel = null; Show(GK); };
            tabs.Children.Add(b);
        }
        tabs.Children.Add(Act("game-lv-add", "＋ Level", () => ProgramRun(GK, () => GameAddLevel(d))));
        var name = new TextBox { Name = "game-lv-name", Text = GameLevelName(d, li), Width = 150 };
        void Rename() { if (name.Text?.Trim() is { Length: > 0 } t && t != GameLevelName(d, li)) ProgramRun(GK, () => GameRenameLevel(d, li, t)); }
        name.LostFocus += (_, _) => Rename(); OnEnter(name, Rename);
        TextBlock Lbl(string t) => new() { Text = t, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center };
        var manage = new WrapPanel { Children = { Lbl("Name"), new Border { Width = 6 }, name, new Border { Width = 6 } } };
        if (li >= 2) manage.Children.Add(Act("game-lv-up", "◀ früher", () => ProgramRun(GK, () => GameMoveLevelUp(d, li))));
        if (li >= 1) manage.Children.Add(Act("game-lv-del", "Level löschen", () => ProgramRun(GK, () => GameDeleteLevel(d, li))));
        var layers = new WrapPanel { Name = "game-layers", VerticalAlignment = VerticalAlignment.Center, Children = { Lbl("Ebene"), new Border { Width = 6 } } };
        foreach (var (id, label) in new[] { ("spiel", "Spiel (Boden, Gegner …)"), ("hintergrund", "Hintergrund (nur Deko)") })
        {
            var on = GameLayer == id; var ll = id;
            var b = new Button { Name = "game-layer-" + id, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 0), Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text };
            b.Click += (_, _) => { GameLayer = ll; GameObjSel = null; Show(GK); };
            layers.Children.Add(b);
        }
        return new StackPanel { Spacing = 4, Children = { tabs, new WrapPanel { Children = { manage, new Border { Width = 16 }, layers } } } };
    }

    /// <summary>Eigenschaften-Leiste: Spielregeln (Tempo, Leben) und das ausgewählte Objekt (Gegner-Tempo, Münzwert, Entfernen).</summary>
    private Control GameObjPanel(ProgramDocument d, List<string> rows)
    {
        var sp = new StackPanel { Name = "game-obj-panel", Spacing = 6 };
        void Num(string name, string label, double value, double min, double max, Action<double> set)
        {
            var t = new TextBox { Name = name, Text = value.ToString("0.##", De), Width = 70 };
            void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && Math.Abs(Math.Clamp(v, min, max) - value) > 1e-9) ProgramRun(GK, () => set(Math.Clamp(v, min, max))); }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(Row(P(label, Text, 12), t));
        }
        sp.Children.Add(H("Spielregeln", 13));
        Num("game-set-speed", "Tempo (1–10)", d.Number("speed", 5), 1, 10, v => d.Set("speed", Math.Round(v), "Tempo"));
        Num("game-set-lives", "Leben (1–9)", d.Number("lives", 3), 1, 9, v => d.Set("lives", Math.Round(v), "Leben"));
        Num("game-set-time", "Zeitlimit s (0 = ohne)", d.Number("timeLimit", 0), 0, 3600, v => d.Set("timeLimit", Math.Round(v), "Zeitlimit"));
        var menu = new CheckBox { Name = "game-set-menu", IsChecked = d.Data["menu"]?.GetValueKind() != System.Text.Json.JsonValueKind.False, Content = new TextBlock { Text = "Startmenü, Pause, Einstellungen und Karte", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } };
        ToolTip.SetTip(menu, "Im gebauten Spiel: Startmenü (Spielen, Fortsetzen), Pause mit Esc, Einstellungen mit Lautstärke, hohem Kontrast, großer Schrift und ruhigen Effekten, Karte und Controller.");
        menu.IsCheckedChanged += (_, _) => ProgramRun(GK, () => d.Set("menu", menu.IsChecked == true, menu.IsChecked == true ? "Spielmenü an" : "Spielmenü aus"));
        sp.Children.Add(menu);
        // Ereignis-Regeln wie die Ereignisblätter in GDevelop/Construct: Wenn … dann …
        sp.Children.Add(H("Regeln (Wenn … dann …)", 13));
        var rules = GameDesign.Rules(d.Data);
        for (var i = 0; i < rules.Count; i++)
        {
            var idx = i;
            sp.Children.Add(new DockPanel { Children = { Act($"game-rule-del-{i}", "✕", () => ProgramRun(GK, () => d.Edit("Regel löschen", x => (x["rules"] as JsonArray)?.RemoveAt(idx)))), P((rules[i].Title is { } qt ? $"Aufgabe „{qt}“: " : "") + GameStudio.RuleText(rules[i]), Text, 11.5) } });
        }
        var when = new ComboBox { Name = "game-rule-when", ItemsSource = GameStudio.RuleWhens.Select(w => w.Label).ToList(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var n = new TextBox { Name = "game-rule-n", Text = "5", Width = 60 };
        var act = new ComboBox { Name = "game-rule-do", ItemsSource = GameStudio.RuleDos.Select(w => w.Label).ToList(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var title = new TextBox { Name = "game-rule-title", PlaceholderText = "z. B. Sammler", Width = 140 };
        ToolTip.SetTip(title, "Optional: Mit Titel erscheint die Regel als Aufgabe mit Fortschritt im Spiel.");
        sp.Children.Add(Row(P("Wenn", Muted, 11.5), when)); sp.Children.Add(Row(P("mindestens", Muted, 11.5), n)); sp.Children.Add(Row(P("dann", Muted, 11.5), act)); sp.Children.Add(Row(P("als Aufgabe", Muted, 11.5), title));
        sp.Children.Add(Act("game-rule-add", "＋ Regel", () => ProgramRun(GK, () =>
        {
            if (!int.TryParse(n.Text, out var nn) || nn < 0) throw new ArgumentException("Bitte eine Zahl ab 0.");
            if (rules.Count >= 20) throw new ArgumentException("Höchstens 20 Regeln.");
            var r = new JsonObject { ["when"] = GameStudio.RuleWhens[Math.Max(0, when.SelectedIndex)].Id, ["n"] = nn, ["do"] = GameStudio.RuleDos[Math.Max(0, act.SelectedIndex)].Id };
            if (!string.IsNullOrWhiteSpace(title.Text)) r["title"] = title.Text.Trim().Length > 40 ? title.Text.Trim()[..40] : title.Text.Trim();
            d.Edit(r.ContainsKey("title") ? "Aufgabe" : "Regel", x => { var a = x["rules"] as JsonArray ?? []; a.Add(r); x["rules"] = a; });
        })));
        // Fähigkeiten der Spielfigur (Paket 01 S1-05)
        sp.Children.Add(H("Fähigkeiten", 13));
        var have = GameDesign.Abilities(d.Data);
        foreach (var (abId, abLabel) in GameStudio.Abilities)
        {
            var id = abId;
            var cb = new CheckBox { Name = "game-ability-" + abId, IsChecked = have.Contains(abId), Content = new TextBlock { Text = abLabel, Foreground = Text, FontSize = 12 } };
            cb.IsCheckedChanged += (_, _) => ProgramRun(GK, () => d.Edit(cb.IsChecked == true ? "Fähigkeit an" : "Fähigkeit aus", x =>
            {
                var l = GameDesign.Abilities(x); l.Remove(id); if (cb.IsChecked == true) l.Add(id);
                x["abilities"] = new JsonArray([.. l.Select(a => (JsonNode)a)]);
            }));
            sp.Children.Add(cb);
        }
        var weapon = new ComboBox { Name = "game-weapon", ItemsSource = GameStudio.Weapons.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(GameStudio.Weapons, x => x.Id == GameDesign.Weapon(d.Data))), HorizontalAlignment = HorizontalAlignment.Stretch };
        ToolTip.SetTip(weapon, "Angriff mit X oder J. Gegner mit mehr Trefferpunkten (rechts beim Gegner) werden zum Boss.");
        weapon.SelectionChanged += (_, _) => { var v = GameStudio.Weapons[Math.Max(0, weapon.SelectedIndex)].Id; if (v != GameDesign.Weapon(d.Data)) ProgramRun(GK, () => d.Set("weapon", v, "Waffe")); };
        sp.Children.Add(Row(P("Waffe", Muted, 11.5), weapon));
        sp.Children.Add(GameAnimPanel(d));
        sp.Children.Add(GameSoundPanel(d));
        sp.Children.Add(H("Objekt", 13));
        if (GameObjSel is not { } sel || sel.Y >= rows.Count || sel.X >= rows[sel.Y].Length)
        {
            sp.Children.Add(P("Werkzeug „Auswählen“ nehmen und ein Objekt im Level antippen.", Muted, 11.5));
            return new Border { CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Top, Child = sp };
        }
        var ch = rows[sel.Y][sel.X]; var key = GameKeyPrefix(_gameLevelIdx) + $"{sel.X},{sel.Y}"; var lvi = _gameLevelIdx;
        var (name, what) = ch switch
        {
            'E' => ("Gegner", "Läuft hin und her und kehrt an Wand oder Kante um. Draufspringen besiegt ihn."),
            'o' => ("Münze", "Bringt Punkte."), '#' => ("Boden", "Fester Boden oder Wand."), '^' => ("Stacheln", "Kostet ein Leben."),
            'S' => ("Start", "Hier beginnt die Figur."), 'Z' => ("Ziel", "Erreichen = geschafft (+10 Punkte)."),
            'F' => ("Sprungfeder", "Draufstehen schleudert die Figur hoch."), 'C' => ("Speicherpunkt", "Berühren: nach einem Treffer geht es hier weiter."),
            'K' => ("Schlüssel", "Alle Schlüssel gesammelt öffnen die Türen."), 'D' => ("Tür", "Fest, bis alle Schlüssel gesammelt sind (oder eine Regel sie öffnet)."),
            'G' => ("Gegenstand", "Kommt beim Berühren ins Inventar. Sprechende Figuren können ihn sich wünschen."),
            'N' => ("Sprechende Figur", "Sagt ihren Text. Wünscht sie sich einen Gegenstand, gibt sie dafür eine Belohnung."),
            'V' => ("Fahrzeug", "Berühren = einsteigen, E = aussteigen. Wagen: schnell und fängt einen Treffer ab. Ufo: Sprungtaste halten zum Fliegen (10 s Treibstoff)."), _ => ("Leer", "Hier ist nichts.")
        };
        sp.Children.Add(new TextBlock { Name = "game-obj-name", Text = $"{name} · Spalte {sel.X + 1}, Zeile {sel.Y + 1}", Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
        sp.Children.Add(P(what, Muted, 11.5));
        var props = GameProps(d);
        void SetProp(string k, double v, string label) => d.Edit(label, x => { var o = x["objects"] as JsonObject ?? []; o[$"{key}:{k}"] = v; x["objects"] = o; });
        if (ch == 'E') Num("game-obj-speed", "Tempo (× 0,25–4)", props.GetValueOrDefault(key + ":speed", 1), 0.25, 4, v => SetProp("speed", Math.Round(v, 2), "Gegner-Tempo"));
        if (ch == 'E') Num("game-obj-hp", "Treffer bis besiegt (1–9, ab 2 Boss)", props.GetValueOrDefault(key + ":hp", 1), 1, 9, v => SetProp("hp", Math.Round(v), "Gegner-Trefferpunkte"));
        if (ch == 'o') Num("game-obj-value", "Wert (Punkte)", props.GetValueOrDefault(key + ":value", 1), 1, 100, v => SetProp("value", Math.Round(v), "Münzwert"));
        if (ch is 'G' or 'N') GameObjTexts(d, sp, ch, key);
        if (ch == 'V')
        {
            var cur = GameDesign.Texts(d.Data).GetValueOrDefault(key + ":kind", "wagen");
            var kind = new ComboBox { Name = "game-obj-vehicle", ItemsSource = GameStudio.Vehicles.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(GameStudio.Vehicles, x => x.Id == cur)), HorizontalAlignment = HorizontalAlignment.Stretch };
            kind.SelectionChanged += (_, _) => { var v = GameStudio.Vehicles[Math.Max(0, kind.SelectedIndex)].Id; if (v != cur) ProgramRun(GK, () => d.Edit("Fahrzeug-Art", x => { var o = x["texts"] as JsonObject ?? []; o[key + ":kind"] = v; x["texts"] = o; })); };
            sp.Children.Add(Row(P("Art", Muted, 11.5), kind));
        }
        if (ch != '.')
        {
            var del = Act("game-obj-del", "Entfernen", () => ProgramRun(GK, () =>
            {
                var l = rows.ToList(); var r = l[sel.Y].ToCharArray(); r[sel.X] = '.'; l[sel.Y] = new string(r);
                GameSetRows(d, lvi, l, "Objekt entfernen");
                d.Edit("Eigenschaften entfernen", x => { foreach (var bag in new[] { x["objects"], x["texts"] }.OfType<JsonObject>()) foreach (var k in bag.Select(kv => kv.Key).Where(k => k.StartsWith(key + ":")).ToList()) bag.Remove(k); });
            }));
            sp.Children.Add(del);
        }
        return new Border { CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Top, Child = sp };
    }

    /// <summary>Texte für Gegenstand (Name) und sprechende Figur (Text, Wunsch, Dank, Belohnung) – Paket 01 S1-05. Jede Änderung ist ein Rückgängig-Schritt.</summary>
    void GameObjTexts(ProgramDocument d, StackPanel sp, char ch, string key)
    {
        var texts = GameDesign.Texts(d.Data);
        string Get(string k) => texts.GetValueOrDefault($"{key}:{k}", "");
        void Set(string k, string v, string label) => ProgramRun(GK, () => d.Edit(label, x => { var o = x["texts"] as JsonObject ?? []; var t = v.Trim(); if (t.Length == 0) o.Remove($"{key}:{k}"); else o[$"{key}:{k}"] = t.Length > 200 ? t[..200] : t; x["texts"] = o; }));
        void Field(string k, string name, string label, string hint)
        {
            var tb = new TextBox { Name = name, Text = Get(k), PlaceholderText = hint, TextWrapping = TextWrapping.Wrap };
            void Commit() { if ((tb.Text ?? "").Trim() != Get(k)) Set(k, tb.Text ?? "", label); }
            tb.LostFocus += (_, _) => Commit(); OnEnter(tb, Commit);
            sp.Children.Add(P(label, Muted, 11.5)); sp.Children.Add(tb);
        }
        if (ch == 'G') { Field("name", "game-obj-item", "Name des Gegenstands", "z. B. Kristall"); return; }
        Field("say", "game-obj-say", "Sagt", "z. B. Hallo! Bring mir den Kristall.");
        // Wunsch: ein Gegenstand, der im Spiel vorkommt
        var items = texts.Where(kv => kv.Key.EndsWith(":name")).Select(kv => kv.Value).Append("Kristall").Distinct().ToList();
        var need = new ComboBox { Name = "game-obj-need", ItemsSource = new[] { "(nichts)" }.Concat(items).ToList(), SelectedIndex = Get("need") is { Length: > 0 } nd ? Math.Max(0, items.IndexOf(nd) + 1) : 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        need.SelectionChanged += (_, _) => { var v = need.SelectedIndex <= 0 ? "" : items[need.SelectedIndex - 1]; if (v != Get("need")) Set("need", v, "Wunsch der Figur"); };
        sp.Children.Add(P("Wünscht sich", Muted, 11.5)); sp.Children.Add(need);
        if (Get("need").Length == 0) return;
        Field("thanks", "game-obj-thanks", "Sagt danach", "z. B. Danke! Die Tür ist offen.");
        var give = new ComboBox { Name = "game-obj-give", ItemsSource = GameStudio.RuleDos.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(GameStudio.RuleDos, x => x.Id == (Get("give") is { Length: > 0 } g ? g : "punkte"))), HorizontalAlignment = HorizontalAlignment.Stretch };
        give.SelectionChanged += (_, _) => { var v = GameStudio.RuleDos[Math.Max(0, give.SelectedIndex)].Id; if (v != (Get("give") is { Length: > 0 } g ? g : "punkte")) Set("give", v, "Belohnung der Figur"); };
        sp.Children.Add(P("Belohnung", Muted, 11.5)); sp.Children.Add(give);
    }

    /// <summary>Spielstand des Probespiels (Paket 01 S1-15): eine Datei je Spiel im Ordner „Spiele/Spielstände“, gleiches Format wie im gebauten Spiel.</summary>
    public string GameSavePath(ProgramDocument d) => Path.Combine(WorkFolder("Spiele"), "Spielstände", DevStudio.SafeName(d.Text("title", "Spiel")) + ".json");
    public void GameSaveHere(ProgramDocument d)
    {
        if (_gamePlay is not { } sim) throw new InvalidOperationException("Erst „Im Fenster spielen“.");
        var f = GameSavePath(d); Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        var s = sim.Snapshot(); File.WriteAllText(f, GameSaves.ToJson(s));
        _progMsg[GK] = "Gespeichert: " + GameSaves.Describe(s, sim.LevelCount);
    }
    public void GameLoadHere(ProgramDocument d)
    {
        var f = GameSavePath(d);
        if (!File.Exists(f)) throw new InvalidOperationException("Für dieses Spiel gibt es noch keinen Spielstand.");
        if (_gamePlay is null) GamePlayHere(d);
        var sim = _gamePlay!;
        var s = GameSaves.Parse(File.ReadAllText(f), sim.LevelCount); // wirft verständlich bei kaputt/neuer/unpassend
        sim.Restore(s);
        _progMsg[GK] = "Fortgesetzt: " + GameSaves.Describe(s, sim.LevelCount);
    }

    /// <summary>Probespiel im Fenster: Figur steuern, Punkte und Leben oben, „Nochmal“ und „Zurück zum Editor“.</summary>
    private Control GamePlayView(ProgramDocument d)
    {
        var sim = _gamePlay!;
        var status = new TextBlock { Name = "game-play-status", Foreground = GoldText(), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        string Status() => sim.Won ? $"Geschafft! {sim.Score} Punkte" : sim.Lost ? (sim.TimeUp ? $"Zeit abgelaufen – {sim.Score} Punkte" : $"Verloren – {sim.Score} Punkte")
            : $"Punkte {sim.Score} · Leben {sim.Lives} · {sim.Coins.Count(c => c.Taken)}/{sim.Coins.Count} Münzen" + (sim.KeysTotal > 0 ? $" · Schlüssel {sim.Keys}/{sim.KeysTotal}" : "")
              + (sim.SecondsLeft >= 0 ? $" · noch {Math.Ceiling(sim.SecondsLeft):0} s" : "") + (sim.Vehicle is { } veh ? $" · {(veh == "ufo" ? $"Ufo ({sim.Fuel / 60} s Treibstoff)" : "Wagen")}" : "") + (sim.Inventory.Count > 0 ? " · Inventar: " + string.Join(", ", sim.Inventory) : "") + string.Concat(sim.Quests.Select(q => " · " + GameStudio.QuestText(q.Rule, q.Value, q.Done))) + (sim.Note.Length > 0 ? " · " + sim.Note : "");
        status.Text = Status();
        var canvas = new PlaytestCanvas { Name = "game-playtest", Sim = sim, Height = 360, HorizontalAlignment = HorizontalAlignment.Stretch,
            Decor = [.. Enumerable.Range(0, GameLevelCount(d)).Select(i => (IReadOnlyList<string>)GameDecorRows(d, i))], Anims = GameAnimBitmaps(d) };
        GamePlaytestCanvas = canvas;
        canvas.Ticked = () => status.Text = Status();
        canvas.Stop = () => { _gamePlay = null; GamePlaytestCanvas = null; Show(GK); };
        var again = Act("game-play-again", "↻ Nochmal", () => { _gamePlay = sim.Restart(); Show(GK); }, true);
        var stop = Act("game-play-stop", "■ Zurück zum Editor", () => canvas.Stop());
        var save = Act("game-play-save", "💾 Speichern", () => ProgramRun(GK, () => GameSaveHere(d)));
        var load = Act("game-play-load", "⤴ Fortsetzen", () => ProgramRun(GK, () => GameLoadHere(d)));
        load.IsEnabled = File.Exists(GameSavePath(d));
        return new StackPanel { Spacing = 8, Children = {
            new WrapPanel { Children = { status, again, new Border { Width = 6 }, save, new Border { Width = 4 }, load, new Border { Width = 6 }, stop } },
            GamePlayNoteBar(d),
            new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = canvas },
            P("← → oder A/D laufen · Leertaste, ↑ oder W springt · Esc zurück zum Editor · gleiche Regeln wie im gebauten Spiel", Muted, 11.5) } };
    }
}
