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

    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    static string MipsElf(string dir) { var b = new byte[64]; b[0] = 0x7F; b[1] = (byte)'E'; b[2] = (byte)'L'; b[3] = (byte)'F'; b[4] = 1; b[5] = 1; b[18] = 8; var f = Path.Combine(dir, "cubes_demo.elf"); File.WriteAllBytes(f, b); return f; }

    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, w.Find<Button>(name)); Settle(); }

    [AvaloniaFact(DisplayName = "Konsole T-0752: Freies Spiel – ohne PS2Recomp und CMake BLOCKIERT vor dem ersten Schritt, Start gesperrt, nichts wird aufgerufen")]
    public async Task Free_port_blocked()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1500;
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-neu")); Settle();
        var d = w.ProgramActive(K)!;
        var calls = 0; w.FreePortRunnerOverride = (_, _, _, _, _, _) => { calls++; return Task.FromResult(new Trendo.Core.Cli.Result(0, "", "")); };
        Tap(w, "con-step-frei");
        Assert.Contains("github.com/glampert/ps2-homebrew", w.Find<TextBlock>("fp-source").Text);
        Assert.StartsWith("BLOCKIERT", w.Find<TextBlock>("fp-blocked").Text);
        Assert.StartsWith("✕", w.Find<TextBlock>("fp-check-recomp").Text);
        Assert.StartsWith("✕", w.Find<TextBlock>("fp-check-cmake").Text);
        Assert.False(w.Find<Button>("fp-start").IsEnabled);
        Assert.True(w.Find<Button>("fp-start").Classes.Contains("haupt"));
        var o = await w.FreePortStart(d);
        Assert.Equal("blockiert", o!.Status);
        Assert.Equal(0, calls);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Konsole T-0752: Freies Spiel – Klick auf Lizenz, Programm, Ordner und „Portieren starten“ bis zur Ergebnisdatei, dann Probelauf")]
    public async Task Free_port_click_to_result()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1500;
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-neu")); Settle();
        var d = w.ProgramActive(K)!;
        var lic = Path.Combine(root, "LICENSE"); File.WriteAllText(lic, "MIT License\n\nPermission is hereby granted, free of charge, to any person obtaining a copy\nof this software and associated documentation files (the \"Software\"), to deal\nin the Software without restriction, including without limitation the rights\nto use, copy, modify, merge, publish, distribute, sublicense, and/or sell\ncopies of the Software ...\n");
        var elf = MipsElf(root);
        var recomp = Path.Combine(root, "ps2recomp"); Directory.CreateDirectory(Path.Combine(recomp, "ps2xRuntime")); File.WriteAllText(Path.Combine(recomp, "CMakeLists.txt"), "project(x)");
        w.PickOverride = r => Task.FromResult<string[]?>(r.Field == "fp-license" ? [lic] : [elf]);
        MainWindow.FolderPickOverride = _ => recomp;
        w.FreePortProbeOverride = new(n => n == "cmake" ? "cmake.exe" : null, () => "cl.exe", 4096);
        var calls = new List<string>();
        w.FreePortRunnerOverride = (exe, args, cwd, onLine, ct, timeout) =>
        {
            lock (calls) calls.Add(Path.GetFileName(exe) + " " + string.Join(' ', args));
            var a = args.ToList();
            if (exe == "cmake.exe" && a[0] == "--build") { var rel = Path.Combine(a[1], "Release"); Directory.CreateDirectory(rel); File.WriteAllText(Path.Combine(rel, a.Contains("--target") ? "ps2EntryRunner.exe" : "ps2recomp.exe"), "MZ"); }
            if (Path.GetFileName(exe) == "ps2recomp.exe") { var outDir = Path.Combine(cwd, "ausgabe"); File.WriteAllText(Path.Combine(outDir, "game.cpp"), "//"); File.WriteAllText(Path.Combine(outDir, "game.h"), "//"); }
            if (Path.GetFileName(exe) == "ps2EntryRunner.exe") { for (var i = 0; i < 5; i++) onLine($"[run:tick] pc=0x0010{i:X4}"); return Task.FromResult(new Trendo.Core.Cli.Result(-2, "", "Zeitüberschreitung")); }
            onLine("fertig: " + a[0]);
            return Task.FromResult(new Trendo.Core.Cli.Result(0, "", ""));
        };
        try
        {
            Tap(w, "con-step-frei");
            Assert.Contains("BLOCKIERT", w.Find<TextBlock>("prog-status-text").Text);
            Tap(w, "fp-title-eigen"); await Until(() => d.Text("fp_title") == "eigen");
            Tap(w, "fp-pick-license"); await Until(() => d.Text("fp_license") == lic);
            Tap(w, "fp-pick-elf"); await Until(() => d.Text("fp_elf") == elf);
            Tap(w, "fp-pick-recomp"); await Until(() => d.Text("fp_recomp") == recomp);
            Settle();
            // T-0757 R1: „Eigenes Homebrew“ startet erst nach der ausdrücklichen Bestätigung
            Assert.False(w.Find<Button>("fp-start").IsEnabled);
            Assert.Contains("Rechte am Programm: noch nicht bestätigt", w.Find<TextBlock>("fp-blocked").Text);
            Assert.Equal("blockiert", (await w.FreePortStart(d))!.Status); // auch direkt aufgerufen: nichts startet
            Assert.Empty(calls);
            w.Show(K); Settle();
            w.Find<CheckBox>("fp-rights").IsChecked = true; await Until(() => d.Text("fp_rights") == Trendo.Core.ConsolePort.FreePort.Sha256(elf)); Settle(); // Fix1: an die SHA-256 der ELF gebunden
            Assert.Null(w.TryFind<TextBlock>("fp-blocked"));
            Assert.True(w.Find<Button>("fp-start").IsEnabled);
            Tap(w, "fp-start");
            await Until(() => w.FreePortLast is not null && !w.FreePortBusy);
            Assert.Equal("fertig", w.FreePortLast!.Status);
            Settle();
            Assert.StartsWith("✓ Fertig", w.Find<TextBlock>("fp-outcome").Text);
            Assert.Contains("ps2EntryRunner.exe · SHA-256 " + w.FreePortLast.ProgramSha256, w.Find<TextBlock>("fp-result").Text);
            Assert.Contains("Schritt 8/8", w.Find<TextBox>("fp-log").Text);
            Assert.Equal("Portieren fertig", w.Find<TextBlock>("prog-status-text").Text);
            Assert.Contains(calls, c => c.EndsWith("--target ps2EntryRunner"));
            Assert.True(File.Exists(Path.Combine(recomp, "ps2xRuntime", "src", "runner", "game.cpp")));
            Assert.Contains("\"rechte_bestaetigt\": \"Ich habe die Rechte an diesem Programm\"", File.ReadAllText(Path.Combine(Path.GetDirectoryName(w.FreePortLast.LogPath)!, "ergebnis.json")));
            Tap(w, "fp-trial");
            await Until(() => w.FreePortLastTrial is not null && !w.FreePortBusy);
            Settle();
            Assert.Equal("läuft", w.FreePortLastTrial!.Status);
            Assert.Contains("ohne Absturz", w.Find<TextBlock>("fp-trial-state").Text);
            Assert.Contains(calls, c => c.StartsWith("ps2EntryRunner.exe ") && c.EndsWith("cubes_demo.elf"));
            Assert.True(d.CanUndo); // Auswahl von Lizenz, Programm und Ordner ist rückgängig machbar
            // M3: fremdes Programm in der Ergebnisdatei – Probelauf gesperrt, nichts wird gestartet
            var result = Path.Combine(Path.GetDirectoryName(w.FreePortLast.LogPath)!, "ergebnis.json");
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(result))!;
            node["programm"] = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            File.WriteAllText(result, node.ToJsonString());
            w.Show(K); Settle();
            Assert.False(w.Find<Button>("fp-trial").IsEnabled);
            Assert.StartsWith("✕", w.Find<TextBlock>("fp-trial-blocked").Text);
            var before = calls.Count;
            Assert.Null(await w.FreePortTrial(d));
            Assert.Equal(before, calls.Count);
            // N1: kaputte Ergebnisdatei – Ansicht baut trotzdem auf
            File.WriteAllText(result, "{ kaputt");
            w.Show(K); Settle();
            Assert.Contains("beschädigt", w.Find<TextBlock>("fp-result").Text);
            // T-0757 Fix1: „Alte Trendo-Dateien aus PS2Recomp entfernen“ fragt erst nach und entfernt nur Trendos eigene Dateien
            var own = Path.Combine(recomp, "ps2xRuntime", "src", "runner", "game.cpp");
            var foreign = Path.Combine(recomp, "ps2xRuntime", "src", "runner", "main.cpp"); File.WriteAllText(foreign, "// PS2Recomp");
            Tap(w, "fp-clean"); Settle();
            Assert.Contains("Wirklich entfernen?", w.Find<TextBlock>("fp-clean-ask").Text);
            Assert.True(File.Exists(own)); // erst nach „Ja“
            Tap(w, "fp-clean-no"); Settle();
            Assert.Null(w.TryFind<TextBlock>("fp-clean-ask"));
            Assert.True(File.Exists(own));
            Tap(w, "fp-clean"); Settle(); Tap(w, "fp-clean-yes"); Settle();
            Assert.False(File.Exists(own));
            Assert.True(File.Exists(foreign));
            // Fix1 NIEDRIG 5: geänderte ELF braucht eine neue Bestätigung
            File.AppendAllText(elf, "x");
            w.Show(K); Settle();
            Assert.Contains("geändert", w.Find<TextBlock>("fp-blocked").Text);
            Assert.False(w.Find<Button>("fp-start").IsEnabled);
        }
        finally { MainWindow.FolderPickOverride = null; w.Close(); }
    }
}
