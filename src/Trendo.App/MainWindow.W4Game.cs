using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-06: Spiele-Studio.</summary>
public partial class MainWindow
{
    public string GameFolder => WorkFolder("Spiele");
    public Func<IReadOnlyList<GameStudio.Engine>> EngineFinder { get; set; } = GameStudio.FindEngines;

    private void InitW4Game() => RegisterTab(KreativTabs, "spiele", "Spiele", "CORE-06", GameView, "spiel", "game", "unreal", "snake");

    private Control GameView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Starte mit einem fertigen kleinen Spiel und verändere es. Es läuft direkt im Browser.") } };
        var name = new TextBox { Name = "game-name", Text = "Mein Spiel", Width = 200 };
        var tpl = new ComboBox { Name = "game-template", ItemsSource = GameStudio.Templates.Select(t => t.Label).ToList(), SelectedIndex = 0, Width = 170 };
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("Browser-Spiel", 14), new WrapPanel { Children = { name, tpl } },
            Act("game-create", "Anlegen und spielen", () =>
            {
                try
                {
                    var dir = GameStudio.CreateBrowserGame(GameFolder, GameStudio.Templates[Math.Max(0, tpl.SelectedIndex)].Id, name.Text ?? "Spiel");
                    _core.Audit.Append(Principal.LocalOwner.Name, "game.create", Path.GetFileName(dir));
                    Launch(Path.Combine(dir, "index.html")); Say($"„{Path.GetFileName(dir)}“ angelegt und im Browser geöffnet.", "spiele");
                }
                catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "spiele"); }
            }, true) } }));

        var engines = EngineFinder();
        var ue = new StackPanel { Spacing = 6, Children = { H("Unreal Engine", 14) } };
        if (engines.Count == 0)
            ue.Children.Add(Row(P("Unter Programme\\Epic Games und in der Epic-Launcher-Liste wurde keine Unreal Engine gefunden. Sie kommt über den Epic Games Launcher (kostenloses Konto nötig)."), Act("game-get-unreal", "Epic Launcher holen", () => Launch("https://store.epicgames.com/download"))));
        else
        {
            var uname = new TextBox { Name = "game-ue-name", Text = "MeinUnrealSpiel", Width = 200 };
            ue.Children.Add(P($"Gefunden: Unreal {string.Join(", ", engines.Select(e => e.Version))}"));
            ue.Children.Add(new WrapPanel { Children = { uname, Act("game-ue-create", "Unreal-Projekt anlegen", () =>
            {
                try
                {
                    var e = engines[0]; var dir = GameStudio.CreateUnrealProject(GameFolder, uname.Text ?? "Spiel", e.Version);
                    var up = Directory.GetFiles(dir, "*.uproject")[0];
                    _core.Audit.Append(Principal.LocalOwner.Name, "game.unreal_project", e.Version);
                    var exe = GameStudio.EditorExe(e);
                    if (File.Exists(exe)) _core.Integrations.LaunchDirect(Principal.LocalOwner, exe, _core.Audit, up);
                    Say($"Projekt „{Path.GetFileName(dir)}“ für Unreal {e.Version} angelegt." + (File.Exists(exe) ? " Der Editor startet (das dauert)." : ""), "spiele");
                }
                catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "spiele"); }
            }) } });
        }
        sp.Children.Add(Card(ue));

        sp.Children.Add(H("Deine Spiele", 14));
        var i = 0;
        foreach (var d in Directory.GetDirectories(GameFolder).OrderByDescending(Directory.GetLastWriteTime).Take(15))
        {
            var dir = d; var html = Path.Combine(dir, "index.html"); var up = Directory.GetFiles(dir, "*.uproject").FirstOrDefault();
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                Act($"game-play-{i}", up is not null ? "Öffnen" : "Spielen", () => Launch(up ?? html)), Act($"game-folder-{i}", "Ordner", () => Launch(dir)) } };
            if (up is not null)
            {
                var project = up;
                actions.Children.Add(OpenInButton("unreal", () => project, "spiele", $"-{i}"));
                actions.Children.Add(Act($"game-ue-check-{i}", "Projekt prüfen", () =>
                {
                    var e = EngineFinder().FirstOrDefault();
                    if (e is null) { Say("Unter Programme\\Epic Games und in der Epic-Launcher-Liste wurde keine Unreal Engine gefunden.", "spiele"); return; }
                    var cmd = GameStudio.EditorCmdExe(e);
                    if (!File.Exists(cmd)) { Say("UnrealEditor-Cmd.exe liegt im Ordner der gefundenen Engine nicht.", "spiele"); return; }
                    Say("Der Editor prüft das Projekt. Das kann dauern.", "spiele");
                    _ = Task.Run(() =>
                    {
                        var c = GameStudio.CheckProject(cmd, project);
                        Dispatcher.UIThread.Post(() => Say(c.Message, "spiele"));
                    });
                }));
            }
            sp.Children.Add(Card(Row(P(Path.GetFileName(dir) + (up is not null ? " · Unreal" : " · Browser"), Text), actions)));
            i++;
        }
        return KreativPage("Spiele-Studio", sp);
    }
}
