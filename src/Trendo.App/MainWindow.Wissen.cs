using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-10: Wissen, das Trendo kennen soll – eigene Notizen, eingelesene Dateien, Korrekturen.</summary>
public partial class MainWindow
{
    private KnowledgeStore? _wissen;
    public KnowledgeStore Wissen => _wissen ??= new KnowledgeStore(_core.DataRoot, _core.Audit);
    private string _wissenQuery = "", _wissenMsg = "";

    private void InitCore10()
    {
        _showMap["ki-wissen"] = KnowledgeView; KiTabs.Add(("ki-wissen", "Wissen"));
        StudioPages["CORE-10"] = "ki-wissen";
        ExtraPages.Add(("Wissen für die KI", "ki-wissen", ["wissen", "gedaechtnis", "merken", "notiz", "lernen"]));
    }

    private IReadOnlyList<(string Title, string Text)> LookupKnowledge(string q, int n) => Wissen.Search(q, n).Where(h => h.Score > 0.5).Select(h => (h.Item.Title, h.Item.Text)).ToList();

    private Control KnowledgeView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Was hier steht, liest die KI bei passenden Fragen mit. Alles bleibt auf diesem PC.") } };
        var title = new TextBox { Name = "wissen-title", PlaceholderText = "Titel (optional)" };
        var text = new TextBox { Name = "wissen-text", PlaceholderText = "Was soll Trendo wissen? z. B. „Meine Kundennummer bei den Stadtwerken ist …“", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
        var add = new Button { Name = "wissen-add", Content = "Merken", Background = Accent };
        add.Click += (_, _) => { if (string.IsNullOrWhiteSpace(text.Text)) { _wissenMsg = "Bitte zuerst etwas eintragen."; } else { Wissen.Add(title.Text ?? "", text.Text); _wissenMsg = "Gemerkt."; } Show("ki-wissen"); };
        var folder = new TextBox { Name = "wissen-folder", PlaceholderText = "Ordner oder Datei (.txt, .md, .csv)", Width = 340 };
        var imp = new Button { Name = "wissen-import", Content = "Einlesen" };
        imp.Click += (_, _) =>
        {
            var p = folder.Text?.Trim().Trim('"') ?? "";
            if (!File.Exists(p) && !Directory.Exists(p)) _wissenMsg = "Diesen Ordner gibt es nicht.";
            else { var (a, s) = Wissen.Import(p); _wissenMsg = $"{a} Datei(en) eingelesen{(s > 0 ? $", {s} übersprungen (kein Text, zu groß oder schon da)" : "")}."; }
            Show("ki-wissen");
        };
        sp.Children.Add(title); sp.Children.Add(text);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, folder, imp } });
        sp.Children.Add(new TextBlock { Name = "wissen-msg", Text = _wissenMsg, Foreground = Text, FontSize = 12 });
        var (g, b) = Wissen.FeedbackStats();
        sp.Children.Add(P($"{Wissen.All().Count} Einträge · Rückmeldungen: {g} gut, {b} verbesserungswürdig", null, 11));

        var search = new TextBox { Name = "wissen-search", PlaceholderText = "Im Wissen suchen …", Text = _wissenQuery, Width = 340 };
        var go = new Button { Name = "wissen-go", Content = "Suchen" };
        go.Click += (_, _) =>
        {
            _wissenQuery = search.Text ?? "";
            if (string.IsNullOrWhiteSpace(_wissenQuery)) { _wissenMsg = "Bitte zuerst ein Suchwort eingeben."; Show("ki-wissen"); return; }
            Show("ki-wissen");
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { search, go } });
        var list = string.IsNullOrWhiteSpace(_wissenQuery) ? Wissen.All().Take(30).ToList() : Wissen.Search(_wissenQuery, 30).Select(h => h.Item).ToList();
        if (!string.IsNullOrWhiteSpace(_wissenQuery) && list.Count == 0) sp.Children.Add(P($"Nichts gefunden für „{_wissenQuery}“."));
        var i = 0;
        foreach (var it in list)
        {
            var del = new Button { Name = $"wissen-del-{i++}", Content = "Löschen", Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Top };
            del.Click += (_, _) => { Wissen.Remove(it.Id); Show("ki-wissen"); };
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = it.Title, Foreground = Text, FontSize = 13 }, P(it.Text.Length > 180 ? it.Text[..180] + "…" : it.Text, null, 11), P($"{it.Source} · {it.Created:dd.MM.yyyy}", null, 10) } },
                Pos(del, 1) } }));
        }
        return KiPage("Wissen", sp);
    }
}
