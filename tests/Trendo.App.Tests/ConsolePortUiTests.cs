using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.ConsolePort;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Studio „Konsolenspiele portieren“.</summary>
public class ConsolePort_Ui
{
    const string K = "konsole-programm";

    static string Iso(string root) { var b = new byte[0x8010]; "CD001"u8.CopyTo(b.AsSpan(0x8001)); var f = Path.Combine(root, "spiel.iso"); File.WriteAllBytes(f, b); return f; }

    [AvaloniaFact(DisplayName = "Konsole: Ohne Besitz-Bestätigung bleiben die Schritte gesperrt; danach geht der richtige Auftrag an Claude")]
    public async Task Gate_and_send()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1500;
        Assert.Equal(K, MainWindow.StudioPages["STUDIO-CONSOLE-01"]);
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-neu")); Settle();
        var d = w.ProgramActive(K)!;
        string? sent = null; w.ConsoleSendOverride = p => { sent = p; return true; };

        RealClick(w, w.Find<Button>("con-step-portieren")); Settle();
        Assert.Contains("Namen", w.Find<TextBlock>("con-blocked").Text);
        Assert.False(w.Find<Button>("con-go-console-reverse").IsEnabled);
        Assert.False(await w.ConsoleSend(d, "/console-reverse")); Assert.Null(sent);

        d.Set("game", "Mein Spiel"); d.Set("image", Iso(root));
        RealClick(w, w.Find<Button>("con-step-spiel")); Settle();
        Assert.StartsWith("✓", w.Find<TextBlock>("con-image-state").Text);
        w.Find<CheckBox>("con-owned").IsChecked = true; Settle();
        Assert.True(d.Flag("owned"));

        RealClick(w, w.Find<Button>("con-step-portieren")); Settle();
        Assert.Null(w.TryFind<TextBlock>("con-blocked"));
        RealClick(w, w.Find<Button>("con-go-console-reverse")); Settle();
        Assert.StartsWith("/console-reverse", sent);
        Assert.Contains("Mein Spiel", sent);

        RealClick(w, w.Find<Button>("con-step-geraete")); Settle();
        RealClick(w, w.Find<Button>("con-target-steamdeck")); Settle();
        Assert.Contains("--target steamdeck", sent);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Konsole: Einrichten holt die feste Fassung, legt die Skills ins Projekt und meldet Fehler verständlich")]
    public async Task Install()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1500;
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-neu")); Settle();
        var d = w.ProgramActive(K)!;
        var seen = new List<string>();
        w.ConsoleGitOverride = (a, dir) =>
        {
            seen.Add(string.Join(' ', a));
            Directory.CreateDirectory(Path.Combine(dir, "console-reverse")); File.WriteAllText(Path.Combine(dir, "console-reverse", "SKILL.md"), "x");
            return true;
        };
        Assert.True(await w.ConsoleInstall(d));
        Assert.Contains(seen, s => s.Contains(ConsolePortKit.SkillCommit));
        RealClick(w, w.Find<Button>("con-step-werkzeuge")); Settle();
        Assert.StartsWith("✓", w.Find<TextBlock>("con-skills-state").Text);
        w.ConsoleGitOverride = (_, _) => false;
        Assert.False(await w.ConsoleInstall(d));
        Assert.Equal("Noch nicht eingerichtet", w.Find<TextBlock>("con-skills-state").Text);
        w.Close();
        _ = root;
    }
}
