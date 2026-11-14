using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-01: Großprojekte – als Kanban-Tafel (Masterprompt 131, Runde 7: Spalten Offen/Läuft/Fertig, Karten mit zuständiger KI-Rolle, Fortschritt und Belegen)
/// und Studio-Fusion (Abläufe über mehrere Studios, mit Freigabe) als zweiter Reiter.</summary>
public partial class MainWindow
{
    public string FusionFolder => WorkFolder("Produktion");
    private int _fusionRecipe;
    private readonly Dictionary<string, string> _fusionInput = new();
    private string? _fusionApproval;
    private IReadOnlyList<StepResult> _fusionResult = [];

    private void InitW5Fusion() => RegisterTab(BusinessTabs, "produktion", "Produktion", "X-01", FusionView, "ablauf", "automatisch", "fusion", "kette", "produktion");

    private FusionContext FusionCtx(Recipe r) => new() { WorkDir = Path.Combine(FusionFolder, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{r.Id}"), Input = new(_fusionInput), Ffmpeg = FfmpegPath, Blender = BlenderPath, RenderImage = (d, f) => { Directory.CreateDirectory(Path.GetDirectoryName(f)!); return ImageRender.Export(d, f); } };

    public IReadOnlyList<StepResult> FusionResult => _fusionResult;
    public void FusionSelect(string id) { _fusionRecipe = Fusion.Recipes.ToList().FindIndex(r => r.Id == id); _fusionApproval = null; _fusionResult = []; }
    public void FusionSetInput(string key, string value) => _fusionInput[key] = value;

    public void FusionRequest()
    {
        var r = Fusion.Recipes[_fusionRecipe];
        _fusionApproval = _core.Approvals.Request(Principal.LocalOwner, "fusion.run", $"Ablauf „{r.Label}“ ausführen", r.Id).Id;
        Show("produktion");
    }

    public void FusionApproveAndRun()
    {
        if (_fusionApproval is not { } id) return;
        var r = Fusion.Recipes[_fusionRecipe];
        _core.Approvals.Approve(id, Principal.LocalOwner);
        _fusionResult = _core.Approvals.Execute(id, "fusion.run", r.Id, () => Fusion.Run(r, FusionCtx(r), _core.Audit));
        _fusionApproval = null;
        Say(_fusionResult.All(x => x.Ok) ? "Fertig. Alle Schritte haben geklappt." : "Angehalten – ein Schritt hat nicht geklappt. Details unten.", "produktion");
    }

    public string ProdTab { get; set; } = "tafel";

    private Control FusionView()
    {
        var tabs = new WrapPanel { Name = "prod-tabs" };
        foreach (var (k, l) in new[] { ("tafel", "Tafel"), ("ablaeufe", "Abläufe über mehrere Studios") })
        {
            if (ProdTab == k) { tabs.Children.Add(new Border { Name = "prod-tab-" + k, Padding = new Thickness(14, 5), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(14), Background = Accent, Child = new TextBlock { Text = l, Foreground = Text } }); continue; } // aktiver Reiter ist kein Knopf
            var b = new Button { Name = "prod-tab-" + k, Content = l, Padding = new Thickness(14, 5), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(14), Foreground = Text,
                Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { ProdTab = k; Show("produktion"); };
            tabs.Children.Add(b);
        }
        return BusinessPage("Großprojekte", new StackPanel { Spacing = 8, Children = { tabs, ProdTab == "ablaeufe" ? FusionFlows() : BigBoardView() } });
    }

    private Control FusionFlows()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Ein Ablauf verbindet mehrere Studios. Du siehst zuerst den Plan und gibst ihn frei. Dann erledigt Trendo alles nacheinander.") } };
        var pick = new WrapPanel();
        for (var k = 0; k < Fusion.Recipes.Count; k++)
        {
            var idx = k; var rr = Fusion.Recipes[k];
            var b = new Button { Name = "fusion-recipe-" + rr.Id, Width = 230, Margin = new Avalonia.Thickness(0, 0, 8, 8), Background = idx == _fusionRecipe ? Accent : TileBg, Content = new StackPanel { Children = { new TextBlock { Text = rr.Label, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, P(rr.Help, null, 10.5) } } };
            b.Click += (_, _) => { _fusionRecipe = idx; _fusionApproval = null; _fusionResult = []; Say($"Ablauf „{rr.Label}“ ist ausgewählt. Trag die Angaben ein und gib den Plan frei.", "produktion"); };
            pick.Children.Add(b);
        }
        sp.Children.Add(pick);
        var r = Fusion.Recipes[_fusionRecipe];
        var inputs = new StackPanel { Spacing = 4 };
        TextBox I(string key, string ph) { var t = new TextBox { Name = "fusion-in-" + key, Text = _fusionInput.GetValueOrDefault(key, ""), PlaceholderText = ph, MinWidth = 360 }; t.TextChanged += (_, _) => _fusionInput[key] = t.Text ?? ""; inputs.Children.Add(t); return t; }
        switch (r.Id)
        {
            case "idea-site": I("name", "Name der Idee"); I("problem", "Welches Problem löst du?"); I("solution", "Wie löst du es?"); I("audience", "Für wen?"); I("price", "Preis in €"); I("email", "Deine E-Mail für Anfragen"); break;
            case "video-pack": I("file", "Pfad zur Videodatei"); break;
            case "logo-print": I("text", "Wort für den Schriftzug, z. B. Trendo"); break;
            case "figur-spiel": I("text", "Name des Spiels, z. B. Sternenspringer"); break;
        }
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("1. Angaben", 14), inputs } }));
        var ctx = FusionCtx(r); var missing = Fusion.Missing(r, ctx);
        var plan = new StackPanel { Spacing = 4, Children = { H("2. Plan", 14), P(Fusion.Plan(r), Text) } };
        foreach (var m in missing) plan.Children.Add(P("• " + m, Brush.Parse("#FACC15")));
        if (_fusionApproval is null)
        {
            var go = Act("fusion-request", "Plan freigeben …", FusionRequest, true); go.IsEnabled = missing.Count == 0;
            plan.Children.Add(go);
        }
        else plan.Children.Add(Card(Row(P("Soll Trendo diese Schritte jetzt ausführen?", Text), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            Act("fusion-approve", "Ja, ausführen", FusionApproveAndRun, true), Act("fusion-deny", "Nein", () => { _core.Approvals.Deny(_fusionApproval!, Principal.LocalOwner); _fusionApproval = null; Show("produktion"); }) } })));
        sp.Children.Add(Card(plan));
        if (_fusionResult.Count > 0)
        {
            var res = new StackPanel { Spacing = 3, Children = { H("3. Ergebnis", 14) } };
            var n = 0;
            foreach (var x in _fusionResult) res.Children.Add(new TextBlock { Name = $"fusion-result-{n++}", Text = $"{(x.Ok ? "✓" : "✗")} {x.Label}: {x.Detail}", Foreground = x.Ok ? Text : Brush.Parse("#F97316"), TextWrapping = TextWrapping.Wrap });
            res.Children.Add(Act("fusion-folder", "Ergebnisse öffnen", () => Launch(FusionFolder)));
            sp.Children.Add(Card(res));
        }
        return sp;
    }

    // ---------- Großprojekte als Kanban-Tafel ----------
    public string? BigProject { get; set; }
    public string? BigCard { get; set; }
    bool _bigNew, _bigDelArmed; string _bigMsg = "";
    public BigBoard? BigBoardNow => BigProject is { } id ? BigBoards.Load(_core.Projects, id) : null;

    void BigChange(Action<BigBoard> change)
    {
        if (BigProject is not { } id) return;
        var b = BigBoards.Load(_core.Projects, id); change(b); BigBoards.Save(_core.Projects, id, b); Show("produktion");
    }
    /// <summary>Karte in eine Spalte schieben (Ziehen auf der Tafel; öffentlich für Tests).</summary>
    public void BigMove(string cardId, int column) => BigChange(b => { if (b.Cards.FirstOrDefault(c => c.Id == cardId) is { } c) { BigBoards.Move(c, column); _core.Audit.Append(Principal.LocalOwner.Name, "grossprojekt.move", $"{BigProject} {cardId} → {BigBoards.Columns[c.Column]}"); } });

    int BigHitColumn(PointerEventArgs e)
    {
        var hit = this.InputHitTest(e.GetPosition(this)) as Visual;
        while (hit is not null && !(hit is Control { Tag: int } c && c.Name?.StartsWith("big-col-") == true)) hit = hit.GetVisualParent();
        return hit is Control { Tag: int col } ? col : -1;
    }

    private Control BigBoardView()
    {
        const string V = "produktion";
        var projects = _core.Projects.List().OrderByDescending(p => p.CreatedAt).ToList();
        if (BigProject is not null && projects.All(p => p.Id != BigProject)) BigProject = null;
        BigProject ??= projects.FirstOrDefault(p => File.Exists(_core.Projects.ResolveInProject(p.Id, BigBoards.File)))?.Id;

        // ── links: Großprojekte ──
        var left = new StackPanel { Name = "big-projects", Spacing = 4 };
        left.Children.Add(Act("big-new", "＋ Neues Großprojekt", () => { _bigNew = true; Show(V); }, true));
        if (_bigNew || projects.Count == 0)
        {
            var name = new TextBox { Name = "big-new-name", PlaceholderText = "Name, z. B. Hofladen online" };
            var goal = new TextBox { Name = "big-new-goal", PlaceholderText = "Ziel in einem Satz" };
            var tpl = new ComboBox { Name = "big-new-recipe", ItemsSource = new[] { "Leere Tafel" }.Concat(Fusion.Recipes.Select(r => "Vorlage: " + r.Label)).ToList(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            left.Children.Add(Card(new StackPanel { Spacing = 4, Children = { name, goal, tpl, Act("big-new-create", "Anlegen", () => Try(V, () =>
            {
                var (p, _) = BigBoards.Create(_core.Projects, name.Text ?? "", goal.Text ?? "", tpl.SelectedIndex > 0 ? Fusion.Recipes[tpl.SelectedIndex - 1] : null);
                BigProject = p.Id; BigCard = null; _bigNew = false; _bigMsg = $"„{p.Name}“ ist angelegt."; _core.Audit.Append(Principal.LocalOwner.Name, "grossprojekt.create", p.Id); Show(V);
            }), true) } }));
        }
        var pi = 0;
        foreach (var p in projects)
        {
            var id = p.Id; var on = id == BigProject; var b = BigBoards.Load(_core.Projects, id);
            var btn = new Button { Name = $"big-proj-{pi++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 4), Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent,
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { Ring(b.Percent, 34, $"big-ring-{id}"), Pos(new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = p.Name, Foreground = Text, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{b.Cards.Count(c => c.Column == 2)} von {b.Cards.Count} Karten fertig", Foreground = Muted, FontSize = 10.5 } } }, 1) } } };
            btn.Click += (_, _) => { BigProject = id; BigCard = null; _bigNew = false; _bigDelArmed = false; _bigMsg = ""; Show(V); };
            left.Children.Add(btn);
        }

        // ── Mitte: Tafel ──
        var center = new StackPanel { Spacing = 8 };
        if (_bigMsg.Length > 0) center.Children.Add(new TextBlock { Name = "big-msg", Text = _bigMsg, Foreground = GoldText(), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        var board = BigBoardNow;
        if (board is null) center.Children.Add(P("Leg links ein Großprojekt an – leer oder aus einer Ablauf-Vorlage. Mehrere Studios und KI-Rollen arbeiten dann Karte für Karte daran.", Muted, 12));
        else
        {
            if (board.Goal.Length > 0) center.Children.Add(new TextBlock { Text = "Ziel: " + board.Goal, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap });
            center.Children.Add(new ProgressBar { Name = "big-progress", Value = board.Percent, Maximum = 100, Height = 6 });
            var grid = new Grid { Name = "big-board", ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*") };
            for (var col = 0; col < 3; col++)
            {
                var stack = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = $"{BigBoards.Columns[col]} ({board.Cards.Count(c => c.Column == col)})", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold } } };
                foreach (var c in board.Cards.Where(c => c.Column == col))
                {
                    var cid = c.Id; var on = cid == BigCard;
                    var card = new Border { Name = $"big-card-{cid}", Background = TileBg, BorderBrush = on ? Brush.Parse("#FACC15") : Edge, BorderThickness = new Thickness(on ? 2 : 1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Cursor = new Cursor(StandardCursorType.Hand),
                        Child = new StackPanel { Spacing = 4, Children = {
                            new TextBlock { Text = c.Title, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                            new WrapPanel { Children = { new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 1), Background = Brush.Parse("#33C9A227"), Margin = new Thickness(0, 0, 6, 0), Child = new TextBlock { Text = "" + c.Role, Foreground = Text, FontSize = 10.5 } },
                                new TextBlock { Text = c.Evidence.Count > 0 ? $"{c.Evidence.Count}" : "", Foreground = Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center } } },
                            new ProgressBar { Value = c.Column == 2 ? 100 : c.Progress, Maximum = 100, Height = 4 } } } };
                    Point? from = null; var dragged = false;
                    card.PointerPressed += (_, e) => { from = e.GetPosition(this); dragged = false; e.Pointer.Capture(card); e.Handled = true; };
                    card.PointerMoved += (_, e) => { if (from is { } f && (Math.Abs(e.GetPosition(this).X - f.X) > 10 || Math.Abs(e.GetPosition(this).Y - f.Y) > 10)) { dragged = true; card.Opacity = 0.55; } };
                    card.PointerReleased += (_, e) =>
                    {
                        if (from is null) return; from = null; e.Pointer.Capture(null); card.Opacity = 1;
                        if (!dragged) { BigCard = cid; _bigDelArmed = false; Show(V); return; }
                        var to = BigHitColumn(e); if (to >= 0) { BigCard = cid; BigMove(cid, to); }
                    };
                    stack.Children.Add(card);
                }
                if (col == 0)
                {
                    var add = new TextBox { Name = "big-add-text", PlaceholderText = "Neue Karte …" };
                    void Add() { var t = (add.Text ?? "").Trim(); if (t.Length == 0) return; var nc = new BoardCard { Title = t }; BigCard = nc.Id; BigChange(b => b.Cards.Add(nc)); }
                    OnEnter(add, Add);
                    stack.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { add, Pos(Act("big-add", "＋", Add), 1) } });
                }
                var colBox = new Border { Name = $"big-col-{col}", Tag = col, Background = Brush.Parse(C("#22000000", "#0F000000")), CornerRadius = new CornerRadius(10), Padding = new Thickness(8), MinHeight = 320, Child = stack };
                Grid.SetColumn(colBox, col * 2); grid.Children.Add(colBox);
            }
            center.Children.Add(grid);
            center.Children.Add(P("Karte ziehen: in eine andere Spalte · antippen: rechts bearbeiten", Muted, 11));
            if (board.Trash.Count > 0)
            {
                var tr = new WrapPanel(); var ti = 0;
                foreach (var t in board.Trash) { var tid = t.Id; tr.Children.Add(Act($"big-trash-{ti++}", "↺ " + t.Title, () => BigChange(b => BigBoards.Restore(b, tid)))); }
                center.Children.Add(new StackPanel { Spacing = 4, Children = { P("Papierkorb der Tafel (antippen stellt wieder her):", Muted, 11), tr } });
            }
        }

        // ── rechts: Karte ──
        var right = new StackPanel { Name = "big-details", Spacing = 6 };
        var cur = board?.Cards.FirstOrDefault(c => c.Id == BigCard);
        if (board is not null && cur is not null)
        {
            var cid = cur.Id;
            void Edit(Action<BoardCard> a) => BigChange(b => { if (b.Cards.FirstOrDefault(x => x.Id == cid) is { } x) a(x); });
            var title = new TextBox { Name = "big-c-title", Text = cur.Title };
            void CommitTitle() { if ((title.Text ?? "").Trim() is { Length: > 0 } t && t != cur.Title) Edit(x => x.Title = t); }
            title.LostFocus += (_, _) => CommitTitle(); OnEnter(title, CommitTitle);
            var detail = new TextBox { Name = "big-c-detail", Text = cur.Detail, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60, PlaceholderText = "Was ist zu tun?" };
            detail.LostFocus += (_, _) => { if ((detail.Text ?? "") != cur.Detail) Edit(x => x.Detail = detail.Text ?? ""); };
            var roles = BigBoards.Roles.Concat(Fusion.Recipes.SelectMany(r => r.Steps.Select(s => s.Studio))).Append(cur.Role).Distinct().ToList();
            var role = new ComboBox { Name = "big-c-role", ItemsSource = roles, SelectedItem = cur.Role, HorizontalAlignment = HorizontalAlignment.Stretch };
            role.SelectionChanged += (_, _) => { if (role.SelectedItem is string r && r != cur.Role) Edit(x => x.Role = r); };
            var prog = new TextBox { Name = "big-c-progress", Text = (cur.Column == 2 ? 100 : cur.Progress).ToString(), Width = 60 };
            void CommitProg() { if (int.TryParse(prog.Text, out var v) && v != cur.Progress) Edit(x => BigBoards.SetProgress(x, v)); }
            prog.LostFocus += (_, _) => CommitProg(); OnEnter(prog, CommitProg);
            right.Children.Add(H("Karte", 13)); right.Children.Add(title); right.Children.Add(detail);
            right.Children.Add(P("Zuständige KI-Rolle", Muted, 11)); right.Children.Add(role);
            right.Children.Add(Row(P("Fortschritt in %", Text, 12), prog));
            right.Children.Add(new WrapPanel { Children = {
                Act("big-left", "← " + (cur.Column > 0 ? BigBoards.Columns[cur.Column - 1] : "–"), () => { if (cur.Column > 0) BigMove(cid, cur.Column - 1); }), new Border { Width = 4 },
                Act("big-right", (cur.Column < 2 ? BigBoards.Columns[cur.Column + 1] : "–") + " →", () => { if (cur.Column < 2) BigMove(cid, cur.Column + 1); }) } });
            right.Children.Add(H($"Belege ({cur.Evidence.Count})", 13));
            var ei = 0;
            foreach (var ev in cur.Evidence)
            {
                var ok = BigBoards.Intact(_core.Projects, BigProject!, ev); var full = _core.Projects.ResolveInProject(BigProject!, ev.Path);
                var eb = new Button { Name = $"big-ev-{ei++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Padding = new Thickness(4, 2),
                    Content = new StackPanel { Children = { new TextBlock { Text = (ok ? "✓ " : "⚠ ") + Path.GetFileName(ev.Path)[(cid.Length + 1)..], Foreground = ok ? Text : Brush.Parse("#F97316"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = (ev.Note.Length > 0 ? ev.Note + " · " : "") + (ok ? "Prüfsumme stimmt" : "Datei verändert oder fehlt") + $" · {ev.Added.LocalDateTime.ToString("dd.MM. HH:mm", De)}", Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap } } } };
                ToolTip.SetTip(eb, "SHA-256: " + ev.Sha256); eb.Click += (_, _) => { if (File.Exists(full)) Launch(full); };
                right.Children.Add(eb);
            }
            var evPath = new TextBox { Name = "big-ev-path", PlaceholderText = "Datei als Beleg (wird ins Projekt kopiert)" };
            var evNote = new TextBox { Name = "big-ev-note", PlaceholderText = "Notiz (optional)" };
            right.Children.Add(evPath); right.Children.Add(evNote);
            right.Children.Add(Act("big-ev-add", "Beleg hinzufügen", () => Try(V, () =>
            {
                var path = (evPath.Text ?? "").Trim().Trim('"'); var note = evNote.Text ?? "";
                BigChange(b => { if (b.Cards.FirstOrDefault(x => x.Id == cid) is { } x) BigBoards.AddEvidence(_core.Projects, BigProject!, x, path, note); });
                _core.Audit.Append(Principal.LocalOwner.Name, "grossprojekt.beleg", $"{BigProject} {cid} {Path.GetFileName(path)}");
            })));
            right.Children.Add(Act("big-c-del", _bigDelArmed ? "Wirklich in den Papierkorb?" : "In den Papierkorb", () =>
            {
                if (!_bigDelArmed) { _bigDelArmed = true; Show(V); return; }
                _bigDelArmed = false; BigCard = null; BigChange(b => BigBoards.Remove(b, cid));
            }));
        }
        else right.Children.Add(P(board is null ? "" : "Tippe eine Karte an: Titel, Aufgabe, zuständige KI-Rolle, Fortschritt und Belege.", Muted, 11.5));

        Border Col(Control c) => new() { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Top, Child = c };
        return new Grid { Name = "big-window", ColumnDefinitions = new ColumnDefinitions("240,12,*,12,280"), Children = { Col(left), Pos(center, 2), Pos(Col(right), 4) } };
    }
}
