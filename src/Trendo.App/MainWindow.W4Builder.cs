using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-03: Baukasten ohne Programmieren.</summary>
public partial class MainWindow
{
    public string BuilderFolder => WorkFolder("Baukasten");
    public string BuilderFile => Path.Combine(BuilderFolder, "seite.json");
    private SitePage? _page;
    public SitePage BuilderPage => _page ??= File.Exists(BuilderFile) ? SitePage.Load(BuilderFile) : new SitePage { Blocks = [new("h1", "Willkommen"), new("p", "Schreib hier etwas über dich.")] };

    private void InitW4Builder() => RegisterTab(KreativTabs, "baukasten", "Baukasten", "X-03", BuilderView, "no-code", "webseite", "homepage", "baukasten", "ohne programmieren");

    private void BuilderSave() { BuilderPage.Save(BuilderFile); Show("baukasten"); }

    public string BuilderPublish()
    {
        var p = BuilderPage.Publish(Path.Combine(BuilderFolder, "fertig"), BuilderFolder);
        _core.Audit.Append(Principal.LocalOwner.Name, "builder.publish", $"{BuilderPage.Blocks.Count} Bausteine");
        return p;
    }

    private Control BuilderView()
    {
        var pg = BuilderPage;
        var sp = new StackPanel { Spacing = 8, Children = { P("Bau deine Webseite aus Bausteinen. Kein Programmieren nötig.") } };
        var title = new TextBox { Name = "builder-title", Text = pg.Title, Width = 260 };
        title.LostFocus += (_, _) => { pg.Title = title.Text ?? ""; BuilderSave(); };
        sp.Children.Add(new WrapPanel { Children = { P("Titel der Seite  "), title } });
        var add = new WrapPanel();
        foreach (var (k, l) in SitePage.Kinds) { var kind = k; var b = Act("builder-add-" + k, "+ " + l, () => { pg.Blocks.Add(new(kind, kind switch { "h1" => "Überschrift", "p" => "Neuer Text", "button" => "Mehr erfahren", "list" => "Punkt 1\nPunkt 2", "contact" => "Schreib mir", _ => "" }, kind is "button" ? "https://" : "")); BuilderSave(); }); b.Margin = new Avalonia.Thickness(0, 0, 6, 6); add.Children.Add(b); }
        sp.Children.Add(Card(new StackPanel { Children = { H("Baustein hinzufügen", 13), add } }));
        for (var i = 0; i < pg.Blocks.Count; i++)
        {
            var idx = i; var b = pg.Blocks[i];
            var label = SitePage.Kinds.FirstOrDefault(x => x.Kind == b.Kind).Label ?? b.Kind;
            var body = new StackPanel { Spacing = 4, Children = { P(label, Text, 11) } };
            if (b.Kind != "hr")
            {
                var t = new TextBox { Name = $"builder-text-{i}", Text = b.Text, AcceptsReturn = b.Kind is "p" or "list", TextWrapping = TextWrapping.Wrap, PlaceholderText = b.Kind == "img" ? "Beschreibung des Bildes" : "Text" };
                t.LostFocus += (_, _) => { pg.Blocks[idx] = pg.Blocks[idx] with { Text = t.Text ?? "" }; pg.Save(BuilderFile); };
                body.Children.Add(t);
            }
            if (b.Kind is "img" or "button" or "contact")
            {
                var u = new TextBox { Name = $"builder-url-{i}", Text = b.Url, PlaceholderText = b.Kind == "contact" ? "deine E-Mail-Adresse" : b.Kind == "img" ? "Bilddatei im Baukasten-Ordner oder https-Adresse" : "https://…" };
                u.LostFocus += (_, _) => { pg.Blocks[idx] = pg.Blocks[idx] with { Url = u.Text ?? "" }; pg.Save(BuilderFile); };
                body.Children.Add(u);
            }
            sp.Children.Add(Card(Row(body, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top, Children = {
                Act($"builder-up-{i}", "▲", () => { pg.Move(idx, -1); BuilderSave(); }), Act($"builder-down-{i}", "▼", () => { pg.Move(idx, 1); BuilderSave(); }),
                Act($"builder-del-{i}", "Entfernen", () => { pg.Blocks.RemoveAt(idx); BuilderSave(); }) } })));
        }
        var live = new StackPanel { Name = "builder-live", Spacing = 6 };
        live.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(pg.Title) ? "Ohne Titel" : pg.Title, Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold });
        foreach (var block in pg.Blocks)
        {
            live.Children.Add(block.Kind switch
            {
                "h1" => (Control)new TextBlock { Text = block.Text, Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                "button" => new Button { Content = string.IsNullOrWhiteSpace(block.Text) ? "Knopf" : block.Text, IsEnabled = false },
                "hr" => new Border { Height = 1, Background = Edge },
                "img" => (Control)P(string.IsNullOrWhiteSpace(block.Text) ? "Bild" : "Bild: " + block.Text),
                "list" => (Control)P(block.Text, Text),
                _ => (Control)P(block.Text, Text),
            });
        }
        sp.Children.Add(Card(new StackPanel { Spacing = 4, Children = { H("Vorschau im Studio", 13), P("So sieht die Seite aus, nachdem du ein Feld verlassen hast.", null, 11), live } }));
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
            Act("builder-preview", "Ansehen im Browser", () => { pg.Save(BuilderFile); Launch(BuilderPublish()); }, true),
            Act("builder-folder", "Ordner mit fertiger Seite", () => { BuilderPublish(); Launch(Path.Combine(BuilderFolder, "fertig")); }) } });
        return KreativPage("Baukasten", sp);
    }
}
