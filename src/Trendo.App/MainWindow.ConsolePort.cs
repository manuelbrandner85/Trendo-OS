using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.ConsolePort;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Studio „Konsolenspiele portieren“ (STUDIO-CONSOLE-01). Führt durch console-skill: Spiel wählen, Werkzeuge einrichten, portieren lassen, für Geräte bauen. Die Arbeit macht Claude Code über die Skills.</summary>
public partial class MainWindow
{
    const string ConsoleKey = "konsole-programm";
    string _conStep = "spiel";
    /// <summary>Für Tests: Auftrag an Claude nachbilden.</summary>
    public Func<string, bool>? ConsoleSendOverride { get; set; }
    /// <summary>Für Tests: Git-Aufruf nachbilden (Argumente, Ordner).</summary>
    public Func<IReadOnlyList<string>, string, bool>? ConsoleGitOverride { get; set; }
    public string? LastConsolePrompt { get; private set; }

    string ConsoleProject(ProgramDocument d) => Path.Combine(_core.DataRoot, "konsolenspiele", d.Id);
    string ConsoleSkillsDir => Path.Combine(_core.DataRoot, "konsolenspiele", "console-skill");

    private void InitConsolePort() => RegisterProgram(FachTabs, new ProgramSpec(ConsoleKey, "STUDIO-CONSOLE-01", "Konsolenspiele portieren")
    {
        Sentence = "Dein eigenes PS2-Spiel als eigenständiges Programm für PC, Steam Deck oder Handy. Experimentell und oft langwierig.",
        Folder = "Konsolenspiele",
        FillHeight = true,
        Templates = [new("neu", "Neues Spiel portieren", "Mit dem Abbild deiner eigenen Disc", () => new JsonObject { ["game"] = "", ["image"] = "", ["owned"] = false })],
        Workspace = ConsoleWorkspace,
        Status = d => ConsolePortKit.ReadyToStart(d.Text("game"), d.Text("image"), d.Flag("owned")) ?? "Bereit zum Portieren",
        Help = "Trendo liefert keine Spiele und kein BIOS und sucht auch keine. Spieldaten bleiben auf deinem PC. Viele Spiele laufen noch nicht vollständig.",
    }, "konsolenspiele", "konsole", "ps2", "playstation 2", "portieren", "recompiler", "steam deck");

    Control ConsoleWorkspace(ProgramDocument d)
    {
        var steps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(4, 4, 4, 10), [DockPanel.DockProperty] = Avalonia.Controls.Dock.Top };
        foreach (var (id, label) in new[] { ("spiel", "1 Spiel"), ("werkzeuge", "2 Werkzeuge"), ("portieren", "3 Portieren"), ("geraete", "4 Geräte") })
        {
            var b = new Button { Name = "con-step-" + id, Content = label, Padding = new Thickness(14, 6) };
            if (_conStep == id) b.Classes.Add("accent");
            b.Click += (_, _) => { _conStep = id; Show(ConsoleKey); };
            steps.Children.Add(b);
        }
        Control body = _conStep switch { "werkzeuge" => ConsoleTools(d), "portieren" => ConsoleRun(d), "geraete" => ConsoleDevices(d), _ => ConsoleGame(d) };
        return new DockPanel { Name = "con-workspace", Children = { steps, new ScrollViewer { Content = body } } };
    }

    Control ConsoleGame(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "con-game", Spacing = 8, Margin = new Thickness(4, 0, 14, 4), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(P("Damit läuft dein eigenes Spiel als Programm auf anderen Geräten, ohne Emulator. Das ist experimentell: Viele Spiele starten noch nicht, und ein Port kann Wochen dauern.", Muted, 12));
        sp.Children.Add(H("Spiel", 13));
        var name = new TextBox { Name = "con-name", Text = d.Text("game"), PlaceholderText = "Name des Spiels" };
        name.LostFocus += (_, _) => { if ((name.Text ?? "") != d.Text("game")) ProgramRun(ConsoleKey, () => d.Set("game", name.Text ?? "", "Name")); };
        sp.Children.Add(name);
        var pick = new Button { Name = "con-pick", Content = "Abbild wählen (.iso)", Padding = new Thickness(12, 5) };
        pick.Click += async (_, _) => { if (await PickMediaFile("con-iso", "Abbild deiner Disc (.iso)", ["*.iso"]) is { } f) ProgramRun(ConsoleKey, () => d.Set("image", f, "Abbild")); };
        sp.Children.Add(pick);
        if (d.Text("image").Length > 0)
        {
            var problem = ConsolePortKit.CheckImage(d.Text("image"));
            sp.Children.Add(new TextBlock { Name = "con-image-state", Text = problem is null ? "✓ " + Path.GetFileName(d.Text("image")) : "✕ " + problem, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brush.Parse(problem is null ? "#3DDC84" : "#FF6B6B") });
        }
        var owned = new CheckBox { Name = "con-owned", IsChecked = d.Flag("owned"), Content = new TextBlock { Text = "Ich besitze dieses Spiel und habe das Abbild selbst von meiner eigenen Disc erstellt.", TextWrapping = TextWrapping.Wrap, Foreground = Text, FontSize = 12 } };
        owned.IsCheckedChanged += (_, _) => { if (owned.IsChecked != d.Flag("owned")) ProgramRun(ConsoleKey, () => d.Set("owned", owned.IsChecked == true, "Besitz bestätigt")); };
        sp.Children.Add(owned);
        sp.Children.Add(H("Was du wissen solltest", 13));
        foreach (var t in new[] { "Trendo liefert und sucht keine Spiele, ISOs oder BIOS-Dateien.", "Abbild, Spieldaten und erzeugter Spielcode bleiben auf deinem PC. Fertige Pakete enthalten keine Spieldaten.", "Ob das Portieren bei dir erlaubt ist, hängt vom Recht deines Landes ab. Trendo kann das nicht für dich klären.", "Nur für PlayStation-2-Spiele. Das Projekt gehört nicht zu Sony." })
            sp.Children.Add(P("• " + t, Muted, 12));
        return sp;
    }

    bool ConsoleSkillsReady => File.Exists(Path.Combine(ConsoleSkillsDir, "console-reverse", "SKILL.md"));

    Control ConsoleTools(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "con-tools", Spacing = 6, Margin = new Thickness(4, 0, 14, 4), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(H("Skills", 13));
        sp.Children.Add(new TextBlock { Name = "con-skills-state", Text = ConsoleSkillsReady ? "✓ Eingerichtet" : "Noch nicht eingerichtet", Foreground = Text, FontSize = 12 });
        var install = new Button { Name = "con-install", Content = ConsoleSkillsReady ? "Erneut einrichten" : "Skills einrichten (console-skill, MIT)", Padding = new Thickness(12, 5) };
        install.Click += async (_, _) => await ConsoleInstall(d);
        sp.Children.Add(install);
        sp.Children.Add(H("Programme", 13));
        foreach (var t in ConsolePortKit.Tools)
        {
            var found = !_opt.Isolated && Cli.Find(t.Id) is not null;
            sp.Children.Add(P($"{(found ? "✓" : t.Required ? "✕" : "○")} {t.Label}: {t.Why}{(t.Required ? "" : " (freiwillig)")}", found ? Text : Muted, 12));
        }
        sp.Children.Add(P("Außerdem nötig: ein C++20-Compiler und PS2Recomp (GPL-3.0). Empfohlen: Ghidra. Der Schritt „Spiel untersuchen“ prüft das und nennt, was fehlt. Trendo liefert diese Programme nicht mit.", Muted, 12));
        return sp;
    }

    /// <summary>Lädt die Skills in fester Fassung und legt sie in den Projektordner, wo Claude Code sie findet.</summary>
    public async Task<bool> ConsoleInstall(ProgramDocument d)
    {
        var dir = ConsoleSkillsDir;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        foreach (var args in ConsolePortKit.InstallSteps())
        {
            var ok = ConsoleGitOverride is { } fake ? fake(args, dir) : await Task.Run(() => Cli.Find("git") is { } git && Cli.Run(git, args, dir, 120000).Ok);
            if (ok) continue;
            _progMsg[ConsoleKey] = "Die Skills ließen sich nicht laden. Ist Git installiert und das Internet erreichbar?"; Show(ConsoleKey); return false;
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.git", "console-skill " + ConsolePortKit.SkillCommit[..8]);
        ConsoleCopySkills(d);
        Show(ConsoleKey);
        return true;
    }

    void ConsoleCopySkills(ProgramDocument d)
    {
        if (!Directory.Exists(ConsoleSkillsDir)) return;
        var target = Path.Combine(ConsoleProject(d), ".claude", "skills");
        foreach (var src in Directory.GetDirectories(ConsoleSkillsDir, "console-*"))
        {
            var to = Path.Combine(target, Path.GetFileName(src));
            foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var dest = Path.Combine(to, Path.GetRelativePath(src, f));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(f, dest, true);
            }
        }
    }

    /// <summary>Auftrag an Claude im Studio geben. false: geht gerade nicht, der Grund steht im Hinweis.</summary>
    public async Task<bool> ConsoleSend(ProgramDocument d, string prompt)
    {
        var problem = ConsolePortKit.ReadyToStart(d.Text("game"), d.Text("image"), d.Flag("owned"));
        if (problem is null && !ConsoleSkillsReady && ConsoleSendOverride is null) problem = "Richte zuerst unter „Werkzeuge“ die Skills ein.";
        if (problem is not null) { _progMsg[ConsoleKey] = problem; Show(ConsoleKey); return false; }
        LastConsolePrompt = prompt;
        Directory.CreateDirectory(ConsoleProject(d));
        ConsoleCopySkills(d);
        _core.Audit.Append(Principal.LocalOwner.Name, "konsole.claude", prompt.Split('\n')[0]);
        if (ConsoleSendOverride is { } fake) return fake(prompt);
        if (await ClaudeFromStudioBar("STUDIO-CONSOLE-01", prompt, t => _progMsg[ConsoleKey] = t)) return true;
        _progMsg[ConsoleKey] = "Dafür muss Claude verbunden sein. Das richtest du unter „Mit Claude verbinden“ ein."; Show(ConsoleKey);
        return false;
    }

    Control ConsoleRun(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "con-run", Spacing = 6, Margin = new Thickness(4, 0, 14, 4), MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        var problem = ConsolePortKit.ReadyToStart(d.Text("game"), d.Text("image"), d.Flag("owned"));
        if (problem is not null) sp.Children.Add(new TextBlock { Name = "con-blocked", Text = problem, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#F5A524"), FontSize = 12 });
        var project = ConsoleProject(d);
        foreach (var stage in new[] { "Grundlage", "Ausbau" })
        {
            sp.Children.Add(H(stage, 13));
            foreach (var s in ConsolePortKit.Skills.Where(x => x.Stage == stage))
            {
                var go = new Button { Name = "con-go-" + s.Id, Content = "Starten", Padding = new Thickness(12, 3), IsEnabled = problem is null };
                go.Click += async (_, _) => await ConsoleSend(d, ConsolePortKit.Prompt(s.Id, d.Text("game"), d.Text("image"), project));
                sp.Children.Add(new DockPanel { Children = { Dk(go, Avalonia.Controls.Dock.Right), new StackPanel { Children = { new TextBlock { Text = s.Title, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold }, P(s.What, Muted, 11) } } } });
            }
        }
        sp.Children.Add(H("Fortschritt", 13));
        sp.Children.Add(new TextBlock { Name = "con-state", Text = ConsolePortKit.State(project) ?? "Noch nicht begonnen.", TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 12, FontFamily = new FontFamily("Consolas, Courier New, monospace") });
        return sp;
    }

    Control ConsoleDevices(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "con-devices", Spacing = 6, Margin = new Thickness(4, 0, 14, 4), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(P("Baut ein Paket für das gewählte Gerät, sobald der Port auf diesem PC läuft. Pakete enthalten keine Spieldaten: Auf dem Gerät legst du dein eigenes Spiel selbst dazu.", Muted, 12));
        var problem = ConsolePortKit.ReadyToStart(d.Text("game"), d.Text("image"), d.Flag("owned"));
        foreach (var t in ConsolePortKit.Targets)
        {
            var go = new Button { Name = "con-target-" + t.Id, Content = "Für " + t.Label + " bauen", HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 6), IsEnabled = problem is null };
            go.Click += async (_, _) => await ConsoleSend(d, ConsolePortKit.PackagePrompt(t.Id, d.Text("game"), ConsoleProject(d)));
            sp.Children.Add(go);
        }
        return sp;
    }
}
