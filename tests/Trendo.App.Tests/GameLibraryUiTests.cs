using System.Net;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Trendo.App;
using Trendo.Core.ConsolePort;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Spiele-Bibliothek im Konsolen-Studio.</summary>
public class GameLibrary_Ui
{
    const string K = "konsole-programm";

    sealed class Fake(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }

    static (MainWindow w, string root) Open()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1500;
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-neu")); Settle();
        return (w, root);
    }

    [AvaloniaFact(DisplayName = "Bibliothek: Startet mit allen Karten, der Filter zeigt nur die gewählte Art, die Disc-Schritte bleiben erreichbar")]
    public void Grid_and_filter()
    {
        var (w, _) = Open();
        Assert.Equal(GameLibrary.Entries.Count, w.Find<WrapPanel>("lib-grid").Children.Count);
        RealClick(w, w.Find<Button>("lib-kind-Homebrew")); Settle();
        Assert.Equal(GameLibrary.Entries.Count(e => e.Kind == GameLibrary.Kind.Homebrew), w.Find<WrapPanel>("lib-grid").Children.Count);
        RealClick(w, w.Find<Button>("con-step-spiel")); Settle();
        Assert.NotNull(w.TryFind<TextBox>("con-name"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Bibliothek: Jeder Eintrag hat ein Cover in den Assets")]
    public void Covers_exist() => Assert.All(GameLibrary.Entries, e => Assert.True(AssetLoader.Exists(new Uri($"avares://TrendoOS/Assets/bibliothek/{e.Id}.jpg")), e.Id));

    [AvaloniaFact(DisplayName = "Bibliothek: Klick auf ein eigenes Spiel zeigt Beschreibung und schickt den Bau-Auftrag mit Nachbau-Verbot an Claude")]
    public void Own_game_build()
    {
        var (w, _) = Open();
        string? sent = null; w.ConsoleSendOverride = p => { sent = p; return true; };
        RealClick(w, w.Find<Button>("lib-card-morvane")); Settle();
        Assert.Equal("Hafenstadt Morvane", w.Find<TextBlock>("lib-title").Text);
        Assert.Null(w.TryFind<Button>("lib-play"));
        RealClick(w, w.Find<Button>("lib-build")); Settle();
        Assert.Contains("Hafenstadt Morvane", sent);
        Assert.Contains("Baue kein bestehendes Spiel nach", sent);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Bibliothek: Wunsch wird geprüft und als eigenes Spiel beauftragt; leerer Wunsch bleibt in der Bibliothek")]
    public void Wish_flow()
    {
        var (w, _) = Open();
        string? sent = null; w.ConsoleSendOverride = p => { sent = p; return true; };
        RealClick(w, w.Find<Button>("lib-wish-go")); Settle();
        Assert.NotNull(w.TryFind<WrapPanel>("lib-grid"));
        w.Find<TextBox>("lib-wish").Text = "so etwas wie GTA";
        RealClick(w, w.Find<Button>("lib-wish-go")); Settle();
        Assert.Equal("so etwas wie GTA", w.Find<TextBlock>("lib-description").Text);
        RealClick(w, w.Find<Button>("lib-build")); Settle();
        Assert.Contains("so etwas wie GTA", sent);
        Assert.Contains("nur Genre und allgemeine Spielmechaniken", sent);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Bibliothek: Freies Spiel wird geholt, danach Spielen, Ändern und Export; Godot wird mit dem Projekt gestartet")]
    public async Task Free_game()
    {
        var (w, _) = Open();
        var d = w.ProgramActive(K)!;
        var e = GameLibrary.Find("demo-truck")!;
        var git = new List<string>();
        w.ConsoleGitOverride = (a, dir) =>
        {
            git.Add(string.Join(' ', a));
            var p = Path.Combine(dir, "3d", "truck_town"); Directory.CreateDirectory(p); File.WriteAllText(Path.Combine(p, "project.godot"), "x");
            return true;
        };
        (string exe, IReadOnlyList<string> args)? started = null;
        w.LibGodotOverride = "godot.exe"; w.LibStartOverride = (x, a) => { started = (x, a); return true; };
        string? sent = null; w.ConsoleSendOverride = p => { sent = p; return true; };

        RealClick(w, w.Find<Button>("lib-card-demo-truck")); Settle();
        Assert.Null(w.TryFind<Button>("lib-play"));
        Assert.True(await w.LibFetch(d, e)); Settle();
        Assert.Contains(git, s => s.Contains(GameLibrary.DemoCommit));

        RealClick(w, w.Find<Button>("lib-play")); Settle();
        Assert.Equal("--path", started!.Value.args[0]);
        Assert.EndsWith("truck_town", started.Value.args[1]);

        w.Find<TextBox>("lib-change").Text = "Regen in der Nacht";
        RealClick(w, w.Find<Button>("lib-change-go")); Settle();
        Assert.Contains("Regen in der Nacht", sent);
        Assert.Contains("Lizenz- und Urheberhinweise", sent);

        RealClick(w, w.Find<Button>("lib-export-android")); Settle();
        Assert.Contains("Android", sent);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Bibliothek: Homebrew mit falscher Prüfsumme startet nicht und meldet den Grund")]
    public async Task Homebrew_bad_hash()
    {
        var (w, root) = Open();
        var started = false;
        w.LibHttpOverride = new HttpClient(new Fake([9, 9])); w.LibStartOverride = (_, _) => { started = true; return true; };
        Assert.False(await w.LibPlayHomebrew(GameLibrary.Find("hb-libbet")!));
        Assert.False(started);
        Assert.Empty(Directory.Exists(Path.Combine(root, "konsolenspiele", "homebrew")) ? Directory.GetFiles(Path.Combine(root, "konsolenspiele", "homebrew")) : []);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Bibliothek: Ohne Godot erklärt Spielen, was fehlt, und startet nichts")]
    public void No_godot()
    {
        var (w, _) = Open();
        var started = false; w.LibStartOverride = (_, _) => { started = true; return true; };
        Assert.False(w.LibPlayGodot(Path.GetTempPath()));
        Assert.False(started);
        w.Close();
    }
}
