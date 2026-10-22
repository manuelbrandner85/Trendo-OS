using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-07: Software-Studio.</summary>
public partial class MainWindow
{
    private string? _devSelected;
    private string? _devFile;
    private string? _devKiDatei;
    private string? _devKiNachher;
    private string _devKiSatz = "";
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
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("Neues Projekt", 14), new WrapPanel { Children = { name, tpl } }, create } }));

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
                Act($"dev-open-{i}", "Ordner", () => Launch(dir)),
                OpenInButton("vscode", () => dir, "dev", $"-{i}"),
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
        if (_devSelected is { } sel && Directory.Exists(sel)) sp.Children.Add(DevEditor(sel));
        return KreativPage("Software-Studio", sp);
    }

    private Control DevEditor(string dir)
    {
        var dateien = WebArbeit.Dateien(dir);
        var liste = new WrapPanel();
        for (var n = 0; n < dateien.Count && n < 40; n++)
        {
            var pfad = dateien[n];
            var rel = Path.GetRelativePath(dir, pfad);
            liste.Children.Add(Act($"dev-file-{n}", rel, () => { _devFile = pfad; Say(rel + " geöffnet.", "dev"); }));
        }
        var text = _devFile is { } f && File.Exists(f) ? WebArbeit.Lesen(f) : "";
        var zeilen = new TextBlock { Name = "dev-lines", FontFamily = new FontFamily("monospace"), Foreground = Muted, FontSize = 13, Text = Zeilen(text) };
        var editor = new TextBox { Name = "dev-editor", Text = text, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("monospace"), MinHeight = 180, Width = 520 };
        editor.TextChanged += (_, _) => zeilen.Text = Zeilen(editor.Text ?? "");
        var blick = WebArbeit.Blick(dir);
        IBrush hintergrund;
        try { hintergrund = Brush.Parse(string.IsNullOrWhiteSpace(blick.Hintergrund) ? "#10203f" : blick.Hintergrund); }
        catch (Exception) { hintergrund = Brush.Parse("#10203f"); }
        var vor = new Border
        {
            Name = "dev-preview",
            Background = hintergrund,
            Padding = new Avalonia.Thickness(12),
            Child = new StackPanel { Spacing = 4, Children = {
                new TextBlock { Text = string.IsNullOrWhiteSpace(blick.Ueberschrift) ? blick.Titel : blick.Ueberschrift, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = blick.Absatz, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = blick.Knopf, Foreground = Brushes.White },
            } },
        };
        var ki = new TextBox { Name = "dev-ki", Watermark = "Ändere die Überschrift in …", Width = 360 };
        var kiOut = new TextBlock { Name = "dev-ki-out", Foreground = Text, TextWrapping = TextWrapping.Wrap, Text = _devKiSatz };
        return Card(new StackPanel { Spacing = 6, Children = {
            H("Dateien von " + Path.GetFileName(dir), 14),
            P("Liste, Editor mit Zeilennummern und Vorschau. Die Vorschau zeigt die Seite, wie sie nach dem Speichern aussieht."),
            liste,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { zeilen, editor } },
            new WrapPanel { Children = {
                Act("dev-save", "Speichern", () =>
                {
                    if (_devFile is null) { Say("Bitte zuerst eine Datei in der Liste wählen.", "dev"); return; }
                    WebArbeit.Speichern(_devFile, editor.Text ?? "");
                    Say("Gespeichert: " + Path.GetFileName(_devFile), "dev");
                }, true),
                Act("dev-browser", "Im Browser öffnen", () =>
                {
                    var html = Path.Combine(dir, "index.html");
                    if (!File.Exists(html)) { Say("Bitte zuerst eine Webseite anlegen. In diesem Ordner liegt keine index.html.", "dev"); return; }
                    Launch(html); Say("Seite im Browser geöffnet.", "dev");
                }),
            } },
            H("Vorschau im Studio", 13),
            vor,
            H("Hilfe beim Text", 13),
            P("Schreib zum Beispiel: Ändere die Überschrift in Willkommen. Du siehst den Unterschied, bevor etwas gespeichert wird."),
            new WrapPanel { Children = { ki, Act("dev-ki-preview", "Vorschau", () =>
            {
                var hit = WebArbeit.Ueberschrift(dir, ki.Text ?? "");
                if (hit is null) { _devKiNachher = null; Say("Bitte zuerst einen Satz wie „Ändere die Überschrift in Willkommen“ eingeben.", "dev"); return; }
                _devKiDatei = hit.Value.Datei; _devKiNachher = hit.Value.Nachher;
                _devKiSatz = $"Bisher: {hit.Value.Alt} → Neu: {hit.Value.Neu}";
                kiOut.Text = _devKiSatz;
                Say("Vorschau steht bereit. Noch nichts gespeichert.", "dev");
            }), Act("dev-ki-apply", "Übernehmen", () =>
            {
                if (_devKiDatei is null || _devKiNachher is null) { Say("Bitte zuerst die Vorschau ansehen.", "dev"); return; }
                WebArbeit.Speichern(_devKiDatei, _devKiNachher);
                _devFile = _devKiDatei;
                Say("Überschrift übernommen.", "dev");
            }) } },
            kiOut,
        } });
    }

    static string Zeilen(string text)
    {
        var n = Math.Max(1, text.Replace("\r\n", "\n").Split('\n').Length);
        return string.Join("\n", Enumerable.Range(1, n));
    }

    private static string Short(string s) { s = s.Trim().Replace('\n', ' '); return s.Length > 160 ? s[..160] + " …" : s; }
}
