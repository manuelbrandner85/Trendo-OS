using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-06: Spiele-Studio.</summary>
public partial class MainWindow
{
    public string GameFolder => WorkFolder("Spiele");
    public Func<IReadOnlyList<GameStudio.Engine>> EngineFinder { get; set; } = GameStudio.FindEngines;
    private string? _gameEdit;
    private string? _gamePreview;
    private string _gamePreviewNote = "";
    public string? GameEditing { get => _gameEdit; set => _gameEdit = value; }

    private void InitW4Game() { InitProgGame(); RegisterTab(KreativTabs, "spiele", "Werkstatt", "CORE-06", GameView, "spiel", "game", "unreal", "snake"); }

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
                    _gameEdit = dir; _gamePreview = null;
                    Launch(Path.Combine(dir, "index.html")); Say($"„{Path.GetFileName(dir)}“ angelegt und im Browser geöffnet. Unten kannst du Regeln, Aussehen und Level einstellen.", "spiele");
                }
                catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "spiele"); }
            }, true) } }));

        if (_gameEdit is { } ge && File.Exists(Path.Combine(ge, "config.js"))) sp.Children.Add(GameEditor(ge));

        var engines = EngineFinder();
        var ue = new StackPanel { Spacing = 6, Children = { H("Unreal Engine", 14) } };
        if (engines.Count == 0)
            ue.Children.Add(Row(P("Unreal Engine ist nicht installiert. Sie kommt über den Epic Games Launcher (kostenloses Konto nötig)."), Act("game-get-unreal", "Epic Launcher holen", () => Launch("https://store.epicgames.com/download"))));
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
        var uo = new TextBox { Name = "game-ue-file", PlaceholderText = "Vorhandenes Unreal-Projekt (.uproject) – Datei wählen", Width = 380 };
        ue.Children.Add(new WrapPanel { Children = { uo, Act("game-ue-open", "Projekt öffnen", () =>
        {
            var f = (uo.Text ?? "").Trim('"', ' ');
            if (f.Length == 0) { Say("Bitte zuerst eine .uproject-Datei wählen.", "spiele"); return; }
            if (!File.Exists(f) || !f.EndsWith(".uproject", StringComparison.OrdinalIgnoreCase)) { Say("Das ist keine vorhandene .uproject-Datei.", "spiele"); return; }
            if (engines.Count == 0) { Say("Unreal Engine ist nicht installiert – das Projekt kann erst nach der Installation geöffnet werden.", "spiele"); return; }
            var exe = GameStudio.EditorExe(engines[0]);
            if (File.Exists(exe)) { _core.Integrations.LaunchDirect(Principal.LocalOwner, exe, _core.Audit, f); Say($"Unreal öffnet „{Path.GetFileNameWithoutExtension(f)}“ (das dauert).", "spiele"); }
            else { Launch(f); Say("Unreal-Editor nicht gefunden – das Projekt wird mit der zugeordneten App geöffnet.", "spiele"); }
        }) } });
        sp.Children.Add(Card(ue));

        sp.Children.Add(H("Deine Spiele", 14));
        var i = 0;
        foreach (var d in Directory.GetDirectories(GameFolder).OrderByDescending(Directory.GetLastWriteTime).Take(15))
        {
            var dir = d; var html = Path.Combine(dir, "index.html"); var up = Directory.GetFiles(dir, "*.uproject").FirstOrDefault();
            sp.Children.Add(Card(Row(P(Path.GetFileName(dir) + (up is not null ? " · Unreal" : " · Browser"), Text), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                Act($"game-play-{i}", up is not null ? "Öffnen" : "Spielen", () => Launch(up ?? html)), Act($"game-folder-{i}", "Ordner", () => Launch(dir)),
                up is null && File.Exists(Path.Combine(dir, "config.js")) ? Act($"game-edit-{i}", "Bearbeiten", () => { _gameEdit = dir; _gamePreview = null; Show("spiele"); }) : new Panel(), up is not null ? OpenInButton("unreal", () => up, "spiele", $"-{i}") : new Panel() } })));
            i++;
        }
        return KreativPage("Spiele-Studio", sp);
    }

    /// <summary>T-0204: Regeln, Aussehen, Spielfigur/Hintergrund aus Datei, Ton und Level-Raster; Vorschau im Studio, Export als ZIP.</summary>
    private Control GameEditor(string dir)
    {
        var s = GameStudio.ReadSettings(dir); var tpl = GameStudio.TemplateOf(dir);
        var box = new StackPanel { Spacing = 6, Name = "game-editor" };
        box.Children.Add(H($"„{Path.GetFileName(dir)}“ einstellen", 14));
        NumericUpDown Num(string n, int v, int min, int max) => new() { Name = n, Minimum = min, Maximum = max, Value = v, Increment = 1, Width = 110, FormatString = "0" };
        var speed = Num("game-speed", s.Speed, 1, 10); var lives = Num("game-lives", s.Lives, 1, 9); var win = Num("game-win", s.WinScore, 0, 9999);
        TextBox Col(string n, string v) => new() { Name = n, Text = v, Width = 90 };
        var player = Col("game-color-player", s.Player); var accent = Col("game-color-accent", s.Accent); var back = Col("game-color-bg", s.Background);
        var pimg = new TextBox { Name = "game-player-img", PlaceholderText = "Bild-Datei für die Spielfigur (optional)", Width = 320 };
        var bimg = new TextBox { Name = "game-bg-img", PlaceholderText = "Bild-Datei für den Hintergrund (optional)", Width = 320 };
        var sound = new CheckBox { Name = "game-sound", Content = "Ton an", IsChecked = s.Sound, Foreground = Text };
        box.Children.Add(Row(P("Tempo (1–10)"), speed, P("Leben"), lives, P("Punkte zum Sieg (0 = endlos)"), win));
        box.Children.Add(Row(P("Farben: Spielfigur"), player, P("Ziel/Ball"), accent, P("Hintergrund"), back, sound));
        box.Children.Add(Row(pimg)); box.Children.Add(Row(bimg));
        if (s.PlayerImage is not null || s.BackgroundImage is not null) box.Children.Add(P($"Im Spiel: {string.Join(", ", new[] { s.PlayerImage, s.BackgroundImage }.Where(x => x is not null))}", null, 11));
        // Level-Raster: Klick schaltet zwischen frei und Stein/Hindernis
        var level = (s.Level.Length > 0 ? s.Level : tpl == "breakout" ? GameStudio.DefaultBreakout : tpl == "jump" ? GameStudio.DefaultJump : []).Select(r => r.ToCharArray()).ToArray();
        if (level.Length > 0)
        {
            box.Children.Add(P(tpl == "jump" ? "Level: Hindernisse der Reihe nach (█ = Hindernis)" : "Level: Steine (█ = Stein)", Text, 12));
            var grid = new StackPanel { Name = "game-level", Spacing = 1 };
            for (var r = 0; r < level.Length; r++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
                for (var col = 0; col < level[r].Length; col++)
                {
                    var (rr, cc) = (r, col);
                    var cell = new Button { Name = $"game-cell-{r}-{col}", Width = 22, Height = 22, Padding = new Avalonia.Thickness(0), Content = level[r][col] == '#' ? "█" : "·", FontSize = 11 };
                    cell.Click += (_, _) => { level[rr][cc] = level[rr][cc] == '#' ? '.' : '#'; cell.Content = level[rr][cc] == '#' ? "█" : "·"; };
                    row.Children.Add(cell);
                }
                grid.Children.Add(row);
            }
            box.Children.Add(grid);
        }
        var img = new Image { Name = "game-preview", MaxWidth = 480, MaxHeight = 420, Stretch = Stretch.Uniform };
        if (_gamePreview is { } gp && File.Exists(gp)) { using var st = File.OpenRead(gp); img.Source = new Avalonia.Media.Imaging.Bitmap(st); }
        async Task SaveAndPreview()
        {
            try
            {
                GameStudio.Save(dir, s with { Speed = (int)(speed.Value ?? 5), Lives = (int)(lives.Value ?? 3), WinScore = (int)(win.Value ?? 20),
                    Player = player.Text?.Trim() ?? "", Accent = accent.Text?.Trim() ?? "", Background = back.Text?.Trim() ?? "", Sound = sound.IsChecked == true,
                    Level = level.Select(r => new string(r)).ToArray() }, pimg.Text?.Trim('"', ' '), bimg.Text?.Trim('"', ' '));
                _core.Audit.Append(Principal.LocalOwner.Name, "game.settings", Path.GetFileName(dir));
                var shot = await WebsitePreview.CaptureAsync(dir, 520, 520, Path.Combine(dir, ".vorschau", "spiel.png"));
                _gamePreview = shot.Ok ? shot.Path : null; _gamePreviewNote = shot.Ok ? "Vorschau aus dem Browser." : shot.Note;
                Say("Gespeichert." + (shot.Ok ? " Die Vorschau zeigt den neuen Stand." : " " + shot.Note), "spiele");
            }
            catch (Exception ex) when (ex is ArgumentException or IOException) { Say("Hat nicht geklappt: " + ex.Message, "spiele"); }
        }
        box.Children.Add(new WrapPanel { Children = {
            Act("game-save", "Speichern und Vorschau", SaveAndPreview, true),
            Act("game-play", "Spielen", () => Launch(Path.Combine(dir, "index.html"))),
            Act("game-export", "Als ZIP weitergeben", () => { try { var z = GameStudio.Export(dir, Path.Combine(GameFolder, "Export")); Say("Exportiert: " + Path.GetFileName(z) + " – index.html im ZIP öffnen genügt.", "spiele"); Launch(Path.GetDirectoryName(z)!); } catch (IOException ex) { Say("Export hat nicht geklappt: " + ex.Message, "spiele"); } }),
            Act("game-edit-close", "Fertig", () => { _gameEdit = null; Show("spiele"); }) } });
        box.Children.Add(img);
        if (_gamePreviewNote.Length > 0) box.Children.Add(P(_gamePreviewNote, null, 11));
        return Card(box);
    }
}
