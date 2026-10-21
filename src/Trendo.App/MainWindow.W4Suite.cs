using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-11: Vorhaben – Ziel wählen, Trendo richtet alles ein.</summary>
public partial class MainWindow
{
    public string SuiteFolder => WorkFolder("Vorhaben");
    private string? _suiteOpen;

    private void InitW4Suite() => RegisterTab(KreativTabs, "vorhaben", "Vorhaben", "X-11", SuiteView, "vorhaben", "plan", "checkliste", "schritte", "ziel");

    private Control SuiteView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Was willst du machen? Wähle ein Ziel. Trendo legt alles an und zeigt dir jeden Schritt.") } };
        var name = new TextBox { Name = "suite-name", PlaceholderText = "Name, z. B. Urlaubsvideo", Width = 240 };
        var wrap = new WrapPanel();
        foreach (var k in CreationSuite.Kits)
        {
            var kit = k;
            var b = new Button { Name = "suite-kit-" + k.Id, Margin = new Avalonia.Thickness(0, 0, 8, 8), Width = 200, Background = TileBg, Content = new StackPanel { Children = { new TextBlock { Text = k.Label, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold }, P(k.Goal, null, 10.5) } } };
            b.Click += (_, _) =>
            {
                try { _suiteOpen = CreationSuite.Create(SuiteFolder, kit.Id, string.IsNullOrWhiteSpace(name.Text) ? kit.Label : name.Text); _core.Audit.Append(Principal.LocalOwner.Name, "suite.create", kit.Id); Say($"„{Path.GetFileName(_suiteOpen)}“ ist eingerichtet. Los geht's mit Schritt 1.", "vorhaben"); }
                catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "vorhaben"); }
            };
            wrap.Children.Add(b);
        }
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { name, wrap } }));

        var dirs = Directory.GetDirectories(SuiteFolder).OrderByDescending(Directory.GetLastWriteTime).ToList();
        _suiteOpen ??= dirs.FirstOrDefault();
        if (dirs.Count > 0)
        {
            sp.Children.Add(H("Deine Vorhaben", 14));
            var i = 0;
            foreach (var d in dirs.Take(12)) { var dir = d; sp.Children.Add(Act($"suite-open-{i++}", $"{(dir == _suiteOpen ? "● " : "")}{Path.GetFileName(dir)} – {CreationSuite.Percent(dir)} % erledigt", () => { _suiteOpen = dir; Show("vorhaben"); })); }
        }
        if (_suiteOpen is { } open && Directory.Exists(open))
        {
            var steps = CreationSuite.Steps(open);
            var list = new StackPanel { Spacing = 3, Children = { Row(H(Path.GetFileName(open), 14), Act("suite-folder", "Ordner", () => Launch(open))), new ProgressBar { Name = "suite-progress", Value = CreationSuite.Percent(open), Maximum = 100, Height = 6 } } };
            var n = 0;
            foreach (var s in steps)
            {
                var st = s;
                var cb = new CheckBox { Name = $"suite-step-{n++}", IsChecked = s.Done, Content = new TextBlock { Text = s.Text, Foreground = Text, TextWrapping = TextWrapping.Wrap } };
                cb.IsCheckedChanged += (_, _) => { CreationSuite.Toggle(open, st.Line); Show("vorhaben"); };
                list.Children.Add(cb);
            }
            var add = new TextBox { Name = "suite-add-text", PlaceholderText = "Eigener Schritt …", Width = 260 };
            list.Children.Add(new WrapPanel { Children = { add, Act("suite-add", "Hinzufügen", () => { CreationSuite.AddStep(open, add.Text ?? ""); Show("vorhaben"); }) } });
            var jump = new WrapPanel();
            foreach (var (k, l) in new[] { ("kreativ-medien", "Medien-Studio"), ("baukasten", "Baukasten"), ("dev", "Software"), ("spiele", "Spiele"), ("3d", "3D"), ("assets", "Freie Assets") })
                if (_showMap.ContainsKey(k)) { var b = Nav2(l, k); b.Margin = new Avalonia.Thickness(0, 4, 6, 0); jump.Children.Add(b); }
            list.Children.Add(jump);
            sp.Children.Add(Card(list));
        }
        return KreativPage("Vorhaben", sp);
    }
}
