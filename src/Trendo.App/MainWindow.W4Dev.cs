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

    private void InitW4Dev() => RegisterTab(KreativTabs, "dev", "Webseite bauen", "CORE-07", DevView, "programmieren", "code", "git", "app", "software", "webseite", "landingpage", "homepage");

    private Control DevView()
    {
        var all = new StackPanel { Spacing = 8 };
        all.Children.Add(WebsitePanel());
        var sp = new StackPanel { Spacing = 8 };
        // V1 (T-0663): Programme aus Vorlagen und eigene Projekte in „Einfach“ unter einem Klappbereich
        all.Children.Add(SimpleFold("dev-more-projects", "Programme aus einer Vorlage und deine Projekte", sp));
        sp.Children.Add(P("Darunter kannst du auch ein Programm aus einer Vorlage anlegen. Trendo speichert jeden Stand mit Git."));
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
            catch (Exception ex) { Say("Hat nicht geklappt: " + Trendo.Core.FriendlyError.Say(ex), "dev"); }
        }, true);
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("Neues Projekt", 14), new WrapPanel { Children = { name, tpl } }, create } }));

        var tools = string.Join(" · ", new[] { ("git", "Git"), ("dotnet", ".NET SDK"), (OperatingSystem.IsWindows() ? "python" : "python3", "Python"), ("node", "Node") }
            .Select(x => ((x.Item1 == "dotnet" ? DevBuild.Missing("dotnet") is null : Cli.Find(x.Item1) is not null) ? "✓ " : "✗ ") + x.Item2));
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
                Act($"dev-edit-{i}", "Bearbeiten", () => { CodeOpenFolder(dir); Show("code"); }, true), // eigener Code-Editor statt VS Code
                Act($"dev-open-{i}", "Ordner", () => Launch(dir)),
                OpenInButton("vscode", () => dir, "dev", $"-{i}"),
                // T-0688: Testen und Paket bauen laufen im Code-Editor im Hintergrund (Ausgabe laufend, Ergebnis je Test, Abbrechen) – die Seite blockiert nicht mehr
                Act($"dev-test-{i}", "Testen", () => Try("dev", () => { _core.Audit.Append(Principal.LocalOwner.Name, "dev.test", Path.GetFileName(dir)); DevInEditor(dir); CodeTest(); Show("code"); })),
                Act($"dev-publish-{i}", "Paket bauen", () => Try("dev", () => { DevInEditor(dir); CodePublish(); Show("code"); })),
                Act($"dev-select-{i}", "Stand speichern …", () => { _devSelected = dir; Show("dev"); }) } };
            sp.Children.Add(Card(Row(new StackPanel { Children = { new TextBlock { Text = Path.GetFileName(dir), Foreground = Text, FontSize = 12.5 }, P(info, null, 10.5) } }, buttons)));
            if (_devSelected == dir && g.IsRepo)
            {
                var msg = new TextBox { Name = "dev-commit-msg", PlaceholderText = "Was hast du geändert?", Width = 300 };
                sp.Children.Add(Card(new WrapPanel { Children = { msg, Act("dev-commit", "Speichern", () => { var r = DevStudio.Commit(dir, msg.Text ?? ""); Say(r.Ok ? "Stand gespeichert." : "Nichts gespeichert. " + Short(r.Err + r.Out), "dev"); }, true) } }));
            }
            i++;
        }
        // V-03/V-22 (T-0709): genau eine goldene Hauptaktion – „Seite bauen“. Alle anderen Hauptknöpfe der Abschnitte (Editor, KI, Seiten, Gestaltung, Medien, Verbindungen …)
        // bleiben ruhig hervorgehoben (goldener Rand), Klick und Name unverändert.
        foreach (var b in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(all).OfType<Button>().Where(b => b.Name != "web-build" && IsGold(b)).ToList()) QuietGold(b);
        return KreativPage("Webseite bauen", all); // T-0663: ein Name für Studio, linke Leiste und Fenster
    }

    /// <summary>Projekt im Code-Editor öffnen (falls dort ein anderes offen ist und gerade nichts läuft).</summary>
    void DevInEditor(string dir)
    {
        if (CodeRoot is { } r && string.Equals(Path.GetFullPath(r), Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase)) return;
        if (CodeRunning) throw new InvalidOperationException("Im Code-Editor läuft gerade noch etwas – erst abwarten oder abbrechen.");
        CodeOpenFolder(dir);
    }

    private static string Short(string s) { s = s.Trim().Replace('\n', ' '); return s.Length > 160 ? s[..160] + " …" : s; }
}
