using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-10: Wissen, das Trendo kennen soll – als Notiz-Wiki (Masterprompt 131, Runde 7): Seiten links mit Suche, in der Mitte lesen mit
/// anklickbaren [[Verweisen]] oder bearbeiten, rechts „Verweist hierher“, fehlende Seiten und „Frag dein Wissen“. Löschen geht in den Papierkorb des Wissens.</summary>
public partial class MainWindow
{
    private KnowledgeStore? _wissen;
    public KnowledgeStore Wissen => _wissen ??= new KnowledgeStore(_core.DataRoot, _core.Audit);
    private string _wissenQuery = "", _wissenMsg = "";
    string? _wikiId; bool _wikiEdit, _wikiTrashView, _wikiDelArmed; string _wikiNewTitle = "";
    string _wikiAsk = "", _wikiAnswer = ""; bool _wikiAsking;
    public string? WikiPage => _wikiId;
    public bool WikiEditing => _wikiEdit;
    public string WikiAnswer => _wikiAnswer;

    private void InitCore10()
    {
        _showMap["ki-wissen"] = KnowledgeView; KiTabs.Add(("ki-wissen", "Wissen"));
        StudioPages["CORE-10"] = "ki-wissen";
        ExtraPages.Add(("Wissen für die KI", "ki-wissen", ["wissen", "gedaechtnis", "merken", "notiz", "lernen", "wiki", "verweise"]));
    }

    private IReadOnlyList<(string Title, string Text)> LookupKnowledge(string q, int n) => Wissen.Search(q, n).Where(h => h.Score > 0.5).Select(h => (h.Item.Title, h.Item.Text)).ToList();

    /// <summary>Seite öffnen (öffentlich für Tests); gibt es sie nicht, öffnet sich eine neue Seite mit diesem Titel.</summary>
    public void WikiOpen(string title)
    {
        if (Wissen.ByTitle(title) is { } it) { _wikiId = it.Id; _wikiEdit = false; }
        else { _wikiId = null; _wikiEdit = true; _wikiNewTitle = title; _wissenMsg = $"Die Seite „{title}“ gibt es noch nicht – schreib los."; }
        _wikiDelArmed = false; _wikiTrashView = false; Show("ki-wissen");
    }

    /// <summary>„Frag dein Wissen“: die KI antwortet nur mit dem, was im Wissen steht, und nennt die Seiten.</summary>
    public async Task WikiAskAsync(string q)
    {
        q = q.Trim(); _wikiAsk = q;
        if (q.Length == 0) { _wikiAnswer = "Bitte zuerst eine Frage eintippen."; Show("ki-wissen"); return; }
        var hits = Wissen.Search(q, 5).Where(h => h.Score > 0.3).ToList();
        if (hits.Count == 0) { _wikiAnswer = "Dazu steht noch nichts in deinem Wissen."; Show("ki-wissen"); return; }
        _wikiAsking = true; _wikiAnswer = "Trendo sucht in deinem Wissen …"; Show("ki-wissen");
        try
        {
            var ctx = string.Join("\n\n", hits.Select(h => $"## {h.Item.Title}\n{(h.Item.Text.Length > 2000 ? h.Item.Text[..2000] : h.Item.Text)}"));
            var a = await Intel.AskAsync(new KiRequest($"Beantworte die Frage nur mit diesen Notizen. Steht die Antwort nicht darin, sag das.\n\n{ctx}\n\nFrage: {q}", PreferredModel: KiModelName, UseKnowledge: false));
            _wikiAnswer = a.Ok ? a.Text + "\n\nQuellen: " + string.Join(", ", hits.Select(h => h.Item.Title)) : a.Text;
        }
        catch (OperationCanceledException) { _wikiAnswer = "Abgebrochen."; }
        finally { _wikiAsking = false; Show("ki-wissen"); }
    }

    private Control KnowledgeView()
    {
        const string V = "ki-wissen";
        var page = _wikiId is { } wid ? Wissen.Get(wid) : null;
        if (page is null && _wikiId is not null) { _wikiId = null; _wikiEdit = false; }

        // ── links: Seiten ──
        var left = new StackPanel { Name = "wissen-pages", Spacing = 4 };
        left.Children.Add(Act("wissen-new", "＋ Neue Seite", () => { _wikiId = null; _wikiEdit = true; _wikiNewTitle = ""; _wikiTrashView = false; _wissenMsg = ""; Show(V); }, true));
        var search = new TextBox { Name = "wissen-search", PlaceholderText = "Im Wissen suchen …", Text = _wissenQuery };
        search.KeyUp += (_, e) => { if (e.Key == Key.Enter || (search.Text ?? "").Length == 0) { _wissenQuery = search.Text ?? ""; Show(V); } };
        var go = new Button { Name = "wissen-go", Content = "Suchen" }; go.Click += (_, _) => { _wissenQuery = search.Text ?? ""; Show(V); };
        left.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { search, Pos(go, 1) } });
        var list = string.IsNullOrWhiteSpace(_wissenQuery) ? Wissen.All().OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase).Take(200).ToList() : Wissen.Search(_wissenQuery, 40).Select(h => h.Item).ToList();
        if (!string.IsNullOrWhiteSpace(_wissenQuery) && list.Count == 0) left.Children.Add(P($"Nichts gefunden für „{_wissenQuery}“.", Muted, 11.5));
        var i = 0;
        foreach (var it in list)
        {
            var id = it.Id; var on = id == _wikiId;
            var b = new Button { Name = $"wissen-page-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 4),
                Background = on ? Brush.Parse("#332F86F6") : Brushes.Transparent,
                Content = new StackPanel { Children = { new TextBlock { Text = it.Title, Foreground = Text, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{it.Source} · {KnowledgeStore.Links(it.Text).Count} Verweise", Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            b.Click += (_, _) => { _wikiId = id; _wikiEdit = false; _wikiDelArmed = false; _wikiTrashView = false; _wissenMsg = ""; Show(V); };
            left.Children.Add(b);
        }
        if (Wissen.All().Count == 0) left.Children.Add(P("Noch leer. Leg eine Seite an oder lies Dateien ein.", Muted, 11.5));
        var folder = new TextBox { Name = "wissen-folder", PlaceholderText = "Ordner oder Datei (.txt, .md, .csv)" };
        var imp = new Button { Name = "wissen-import", Content = "Einlesen" };
        imp.Click += (_, _) =>
        {
            var p = folder.Text?.Trim().Trim('"') ?? "";
            if (!File.Exists(p) && !Directory.Exists(p)) _wissenMsg = "Diesen Ordner gibt es nicht.";
            else { var (a, s) = Wissen.Import(p); _wissenMsg = $"{a} Datei(en) eingelesen{(s > 0 ? $", {s} übersprungen (kein Text, zu groß oder schon da)" : "")}."; }
            Show(V);
        };
        var (good, bad) = Wissen.FeedbackStats();
        left.Children.Add(new Border { Height = 8 });
        left.Children.Add(P("Dateien einlesen", Muted, 11)); left.Children.Add(folder); left.Children.Add(imp);
        left.Children.Add(P($"{Wissen.All().Count} Seiten · Rückmeldungen: {good} gut, {bad} verbesserungswürdig", null, 10.5));
        var trashed = Wissen.Trashed().Count;
        left.Children.Add(Act("wissen-trash", $"🗑 Papierkorb ({trashed})", () => { _wikiTrashView = true; _wikiId = null; _wikiEdit = false; Show(V); }));

        // ── Mitte: lesen, bearbeiten, Papierkorb ──
        var center = new StackPanel { Name = "wissen-center", Spacing = 8 };
        center.Children.Add(new TextBlock { Name = "wissen-msg", Text = _wissenMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (_wikiTrashView)
        {
            center.Children.Add(H("Papierkorb des Wissens", 15));
            var t = Wissen.Trashed(); if (t.Count == 0) center.Children.Add(P("Leer.", Muted, 12));
            var k = 0;
            foreach (var it in t)
            {
                var id = it.Id;
                center.Children.Add(Row(new StackPanel { Children = { new TextBlock { Text = it.Title, Foreground = Text }, P(it.Text.Length > 120 ? it.Text[..120] + "…" : it.Text, Muted, 11) } },
                    Act($"wissen-restore-{k++}", "Wiederherstellen", () => { Wissen.Restore(id); _wikiTrashView = false; _wikiId = id; _wissenMsg = $"„{it.Title}“ ist wieder da."; Show(V); })));
            }
        }
        else if (_wikiEdit || page is null)
        {
            var title = new TextBox { Name = "wissen-title", PlaceholderText = "Titel der Seite", Text = page?.Title ?? _wikiNewTitle, FontSize = 15 };
            var text = new TextBox { Name = "wissen-text", PlaceholderText = "Was soll Trendo wissen? z. B. „Meine Kundennummer bei den Stadtwerken ist …“ – mit [[Titel]] verweist du auf eine andere Seite.",
                Text = page?.Text ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 260 };
            Button save;
            if (page is null)
            {
                save = new Button { Name = "wissen-add", Content = "Merken", Background = Accent };
                save.Click += (_, _) =>
                {
                    if (string.IsNullOrWhiteSpace(text.Text)) { _wissenMsg = "Bitte zuerst etwas eintragen."; Show(V); return; }
                    if (!string.IsNullOrWhiteSpace(title.Text) && Wissen.ByTitle(title.Text) is not null) { _wissenMsg = $"Eine Seite „{title.Text.Trim()}“ gibt es schon."; Show(V); return; }
                    var it = Wissen.Add(title.Text ?? "", text.Text); _wikiId = it.Id; _wikiEdit = false; _wissenMsg = "Gemerkt."; Show(V);
                };
            }
            else
            {
                save = new Button { Name = "wissen-save", Content = "Speichern", Background = Accent };
                save.Click += (_, _) => Try(V, () => { Wissen.Update(page.Id, title.Text ?? "", text.Text ?? ""); _wikiEdit = false; _wissenMsg = "Gespeichert."; Show(V); });
            }
            var cancel = Act("wissen-cancel", "Abbrechen", () => { _wikiEdit = false; _wissenMsg = ""; Show(V); });
            if (page is null) center.Children.Add(P("Neue Seite. Was hier steht, liest die KI bei passenden Fragen mit. Alles bleibt auf diesem PC.", Muted, 12));
            center.Children.Add(title); center.Children.Add(text);
            center.Children.Add(new WrapPanel { Children = { save, new Border { Width = 6 }, cancel } });
            center.Children.Add(P("Tipp: [[Seitentitel]] verlinkt eine Seite, [[Seitentitel|anderer Text]] zeigt einen anderen Text an.", Muted, 11));
        }
        else if (page is not null)
        {
            center.Children.Add(Row(H(page.Title, 18), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                Act("wissen-edit", "✎ Bearbeiten", () => { _wikiEdit = true; _wissenMsg = ""; Show(V); }),
                Act("wissen-del", _wikiDelArmed ? "Wirklich in den Papierkorb?" : "In den Papierkorb", () =>
                {
                    if (!_wikiDelArmed) { _wikiDelArmed = true; Show(V); return; }
                    Wissen.Trash(page.Id); _wikiId = null; _wikiDelArmed = false; _wissenMsg = $"„{page.Title}“ liegt im Papierkorb des Wissens."; Show(V);
                }) } }));
            center.Children.Add(P($"{page.Source} · {page.Created.LocalDateTime.ToString("dd.MM.yyyy", De)}", Muted, 11));
            // Lesen mit anklickbaren Verweisen
            var body = new StackPanel { Name = "wissen-read", Spacing = 6 };
            var li = 0;
            foreach (var para in page.Text.Replace("\r", "").Split("\n"))
            {
                var line = new WrapPanel();
                foreach (var (seg, link) in KnowledgeStore.Segments(para))
                {
                    if (link is null) { foreach (var word in seg.Split(' ')) if (word.Length > 0) line.Children.Add(new TextBlock { Text = word + " ", Foreground = Text, FontSize = 13.5 }); continue; }
                    var exists = Wissen.ByTitle(link) is not null; var target = link;
                    var lb = new Button { Name = $"wissen-link-{li++}", Content = (exists ? "" : "＋ ") + seg, Padding = new Thickness(3, 0), Margin = new Thickness(0, 0, 4, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 1),
                        BorderBrush = Brush.Parse(exists ? "#2F86F6" : "#F5A524"), Foreground = Brush.Parse(exists ? "#5BA8FF" : "#F5A524"), FontSize = 13.5, Cursor = new Cursor(StandardCursorType.Hand) };
                    ToolTip.SetTip(lb, exists ? $"Zur Seite „{link}“" : $"Seite „{link}“ anlegen");
                    lb.Click += (_, _) => WikiOpen(target);
                    line.Children.Add(lb);
                }
                body.Children.Add(line.Children.Count == 0 ? new Border { Height = 6 } : line);
            }
            center.Children.Add(Card(body));
        }

        // ── rechts: Verweise und Frag dein Wissen ──
        var right = new StackPanel { Name = "wissen-side", Spacing = 6 };
        if (page is not null && !_wikiEdit)
        {
            right.Children.Add(H("Verweist hierher", 13));
            var back = Wissen.Backlinks(page.Title); var bi = 0;
            if (back.Count == 0) right.Children.Add(P("Noch keine Seite verweist hierher.", Muted, 11.5));
            foreach (var bp in back) { var t = bp.Title; right.Children.Add(Act($"wissen-back-{bi++}", "← " + t, () => WikiOpen(t))); }
            var missing = Wissen.Missing(page);
            if (missing.Count > 0)
            {
                right.Children.Add(H("Fehlende Seiten", 13)); var mi = 0;
                foreach (var m in missing) { var t = m; right.Children.Add(Act($"wissen-missing-{mi++}", "＋ " + t, () => WikiOpen(t))); }
            }
        }
        right.Children.Add(H("Frag dein Wissen", 13));
        var ask = new TextBox { Name = "wissen-ask", Text = _wikiAsk, PlaceholderText = "z. B. Wann wird der Müll abgeholt?", AcceptsReturn = false };
        ask.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _ = WikiAskAsync(ask.Text ?? ""); e.Handled = true; } };
        var askGo = Act("wissen-ask-go", _wikiAsking ? "Sucht …" : "Fragen", () => _ = WikiAskAsync(ask.Text ?? ""), true); askGo.IsEnabled = !_wikiAsking;
        right.Children.Add(ask); right.Children.Add(askGo);
        if (_wikiAnswer.Length > 0) right.Children.Add(new SelectableTextBlock { Name = "wissen-answer", Text = _wikiAnswer, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });

        Border Col(Control c, bool scroll = false) => new() { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Top,
            Child = scroll ? new ScrollViewer { Content = c, MaxHeight = 720 } : c };
        var grid = new Grid { Name = "wissen-wiki", ColumnDefinitions = new ColumnDefinitions("250,12,*,12,280"), Children = { Col(left, true), Pos(center, 2), Pos(Col(right), 4) } };
        return KiPage("Wissen", grid);
    }
}
