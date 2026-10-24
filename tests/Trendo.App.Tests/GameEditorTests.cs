using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0204: eigenes Spiel statt fester Vorlage – Regeln, Aussehen, Bilder, Level, Export, Unreal öffnen.</summary>
public class GameEditorTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-game-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0204: Einstellungen landen in config.js, Bild wird kopiert, Export enthält alles")]
    public void Settings_image_export()
    {
        var parent = Temp();
        var dir = GameStudio.CreateBrowserGame(parent, "breakout", "Mein Spiel");
        Assert.Contains("\"level\":[\"########\"", File.ReadAllText(Path.Combine(dir, "config.js")));
        var bild = Path.Combine(parent, "held.png"); File.WriteAllBytes(bild, [0x89, 0x50, 0x4E, 0x47]);
        var s = GameStudio.Save(dir, new GameStudio.GameSettings { Speed = 9, Lives = 5, WinScore = 7, Player = "#FF0000", Level = ["#.#.", ".#.#"] }, bild);
        Assert.Equal("spielfigur.png", s.PlayerImage);
        var cfg = File.ReadAllText(Path.Combine(dir, "config.js"));
        Assert.Contains("\"speed\":9", cfg); Assert.Contains("\"lives\":5", cfg); Assert.Contains("\"win\":7", cfg); Assert.Contains("\"playerImage\":\"spielfigur.png\"", cfg);
        Assert.Equal(9, GameStudio.ReadSettings(dir).Speed);
        Assert.Contains("config.js", File.ReadAllText(Path.Combine(dir, "index.html")));
        var zip = GameStudio.Export(dir, Path.Combine(parent, "Export"));
        using var z = ZipFile.OpenRead(zip);
        Assert.Superset(new HashSet<string> { "index.html", "game.js", "config.js", "spielfigur.png" }, z.Entries.Select(e => e.FullName).ToHashSet());
        Assert.Throws<ArgumentException>(() => GameStudio.Save(dir, new GameStudio.GameSettings { Player = "rot" }));
        Assert.Throws<ArgumentException>(() => GameStudio.Save(dir, new GameStudio.GameSettings { Level = ["#x#"] }));
    }

    [AvaloniaFact(DisplayName = "T-0204 (UI): Anlegen → Editor; Tempo und Level ändern, speichern, als ZIP exportieren")]
    public async Task Editor_ui()
    {
        var (w, _, _) = Ui.Open();
        w.Show("spiele");
        w.Find<TextBox>("game-name").Text = "Editor Test";
        w.Find<ComboBox>("game-template").SelectedIndex = 1; // Mauerbrecher
        w.Find<Button>("game-create").Press();
        Assert.NotNull(w.TryFind<StackPanel>("game-editor"));
        w.Find<NumericUpDown>("game-speed").Value = 8;
        w.Find<Button>("game-cell-0-0").Press();
        w.Find<Button>("game-save").Press();
        var dir = w.GameEditing!;
        for (var i = 0; i < 400 && GameStudio.ReadSettings(dir).Speed != 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(25); }
        var s = GameStudio.ReadSettings(dir);
        Assert.Equal(8, s.Speed);
        Assert.Equal('.', s.Level[0][0]);
        w.Find<Button>("game-export").Press();
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(w.GameFolder, "Export"), "*.zip"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0204 (UI): Unreal – vorhandenes Projekt öffnen; ohne Unreal klarer Hinweis")]
    public void Unreal_open()
    {
        var (w, l, root) = Ui.Open();
        w.EngineFinder = () => [];
        var up = Path.Combine(root, "Alt.uproject"); File.WriteAllText(up, "{}");
        w.Show("spiele");
        w.Find<TextBox>("game-ue-file").Text = up;
        w.Find<Button>("game-ue-open").Press();
        Assert.Contains("nicht installiert", ((TextBlock)w.Find<Border>("w45-msg").Child!).Text);
        var eng = Path.Combine(root, "UE_5.6"); w.EngineFinder = () => [new GameStudio.Engine("5.6", eng)];
        w.Show("spiele");
        w.Find<TextBox>("game-ue-file").Text = up;
        w.Find<Button>("game-ue-open").Press();
        Assert.Contains(l.Opened, o => o.Contains("Alt.uproject"));
        w.Close();
    }
}
