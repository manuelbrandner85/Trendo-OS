using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-07: Software-Studio.</summary>
public partial class MainWindow
{
    private string? _devSelected;
    public string DevFolder => WorkFolder("Software");

    private void InitW4Dev() => RegisterTab(KreativTabs, "dev", "Software", "CORE-07", DevView, "programmieren", "code", "git", "app", "software");

    private Control DevView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Lege ein neues Programm aus einer Vorlage an. Trendo legt alles an und speichert jeden Stand mit Git."));
        var name = new TextBox { Name = "dev-name", PlaceholderText = "Name, z. B. Meine Webseite", Width = 260 };
        var tpl = new ComboBox { Name = "dev-template", ItemsSource = DevStudio.Templates.Select(t => t.Name).ToList(), SelectedIndex = 0, Width = 260 };
        var create = Act("dev-create", "Anlegen", () =>
        {
            var t = DevStudio.Templates[Math.Max(0, tpl.SelectedIndex)];
            try
            {
                var dir = DevStudio.Create(DevFolder, t.Id, string.IsNullOrWhiteSpace(name.Text) ? "Mein Projekt" : name.Text);
                var g = Cli.Find("git") is null ? "Git fehlt – ohne Versionsstand angelegt." : DevStudio.GitInit(dir).Ok ? "Erster Stand gespeichert." : "Git hat nicht geklappt.";
                _devSelected = dir; _core.Audit.Append(Principal.LocalOwner.Name, "dev.create", t.Id);
                Say($"„{Path.GetFileName(dir)}“ angelegt. {g}", "dev");
            }
            catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "dev"); }
        }, true);
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("Neues Projekt", 14), ProgrammLeiste("vscode", "VS Code", AktuellesDevProjekt), new WrapPanel { Children = { name, tpl } }, create } }));

        var tools = string.Join(" · ", new[] { ("git", "Git"), ("dotnet", ".NET"), (OperatingSystem.IsWindows() ? "python" : "python3", "Python"), ("node", "Node") }
            .Select(x => (Cli.Find(x.Item1) is null ? "✗ " : "✓ ") + x.Item2));
        sp.Children.Add(P("Werkzeuge auf diesem PC: " + tools, null, 11));

        sp.Children.Add(H("Deine Projekte", 14));
        var dirs = Directory.GetDirectories(DevFolder).OrderByDescending(Directory.GetLastWriteTime).ToList();
        if (dirs.Count == 0) sp.Children.Add(P("Noch keine Projekte. Lege oben eins an."));
        var i = 0;
        foreach (var d in dirs.Take(20))
        {
            var dir = d; var g = DevStudio.Git(dir);
            var info = g.IsRepo ? $"Zweig {g.Branch} · {(g.Changed == 0 ? "alles gespeichert" : g.Changed + " geänderte Dateien")} · {g.LastCommit}" : "ohne Versionsstand";
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = {
                ProgrammKnopf($"dev-code-{i}", "vscode", dir),
                Act($"dev-open-{i}", "Ordner", () => Launch(dir)),
                Act($"dev-test-{i}", "Testen", () => { var r = DevStudio.RunTests(dir); _core.Audit.Append(Principal.LocalOwner.Name, "dev.test", $"{Path.GetFileName(dir)} exit={r.Exit}"); Say(r.Ok ? $"„{Path.GetFileName(dir)}“: Test bestanden." : $"„{Path.GetFileName(dir)}“: Test nicht bestanden. {Short(r.Err + r.Out)}", "dev"); }),
                Act($"dev-select-{i}", "Stand speichern …", () => { _devSelected = dir; Show("dev"); }) } };
            sp.Children.Add(Card(Row(new StackPanel { Children = { new TextBlock { Text = Path.GetFileName(dir), Foreground = Text, FontSize = 12.5 }, P(info, null, 10.5) } }, buttons)));
            if (_devSelected == dir && g.IsRepo)
            {
                var msg = new TextBox { Name = "dev-commit-msg", PlaceholderText = "Was hast du geändert?", Width = 300 };
                sp.Children.Add(Card(new WrapPanel { Children = { msg, Act("dev-commit", "Speichern", () => { var r = DevStudio.Commit(dir, msg.Text ?? ""); Say(r.Ok ? "Stand gespeichert." : "Nichts gespeichert. " + Short(r.Err + r.Out), "dev"); }, true) } }));
            }
            i++;
        }
        return KreativPage("Software-Studio", sp);
    }

    private static string Short(string s) { s = s.Trim().Replace('\n', ' '); return s.Length > 160 ? s[..160] + " …" : s; }
}
