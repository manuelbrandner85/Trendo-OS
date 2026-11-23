using Trendo.Core;
using Trendo.Core.ConsolePort;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0752: Ablauf „Freies Spiel portieren“ (STUDIO-CONSOLE-01).</summary>
public class FreePortTests(ITestOutputHelper output)
{
    /// <summary>Vollständiger MIT-Text (ohne Personennamen).</summary>
    public const string Mit = "MIT License\n\nCopyright (c) 2026 Erika Muster\n\nPermission is hereby granted, free of charge, to any person obtaining a copy\nof this software and associated documentation files (the \"Software\"), to deal\nin the Software without restriction, including without limitation the rights\nto use, copy, modify, merge, publish, distribute, sublicense, and/or sell\ncopies of the Software, subject to the following conditions: ...\n";

    /// <summary>LICENSE.txt aus glampert/ps2-homebrew (Stand 602441a6), Zeilenenden LF.</summary>
    const string GlampertLicense = "\nThe MIT License <from http://opensource.org/licenses/MIT> :\n\nPermission is hereby granted, free of charge, to any person obtaining a copy\nof this software and associated documentation files (the \"Software\"), to deal\nin the Software without restriction, including without limitation the rights\nto use, copy, modify, merge, publish, distribute, sublicense, and/or sell\ncopies of the Software, and to permit persons to whom the Software is\nfurnished to do so, subject to the following conditions:\n\nThe above copyright notice and this permission notice shall be included in\nall copies or substantial portions of the Software.\n\nTHE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\nIMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,\nFITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\nAUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER\nLIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,\nOUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN\nTHE SOFTWARE.\n\n";

    static string Root() { var d = Path.Combine(Path.GetTempPath(), "trendo-fp-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static FreePort.Title Own => FreePort.Find(FreePort.OwnId)!;
    static FreePort.Title Cubes => FreePort.Find("cubes-demo")!;

    /// <summary>Kleinste gültige PS2-ELF-Kopfzeile: 32 Bit, Little Endian, MIPS.</summary>
    public static string Elf(string dir, string name = "spiel.elf", byte machine = 8)
    {
        var b = new byte[64]; b[0] = 0x7F; b[1] = (byte)'E'; b[2] = (byte)'L'; b[3] = (byte)'F'; b[4] = 1; b[5] = 1; b[6] = 1; b[16] = 2; b[18] = machine;
        Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, name); File.WriteAllBytes(f, b); return f;
    }

    static string License(string dir, string text = Mit) { Directory.CreateDirectory(dir); var f = Path.Combine(dir, "LICENSE"); File.WriteAllText(f, text); return f; }

    public static string Recomp(string dir) { var r = Path.Combine(dir, "ps2recomp"); Directory.CreateDirectory(Path.Combine(r, "ps2xRuntime")); File.WriteAllText(Path.Combine(r, "CMakeLists.txt"), "project(x)"); return r; }

    public static FreePort.Probe AllThere => new(n => n == "cmake" ? "cmake.exe" : null, () => "cl.exe", 4096);

    [Fact(DisplayName = "Freies Spiel: ELF-Prüfung erkennt PS2-Programme und weist Disc-Startprogramme, SYSTEM.CNF und Fremdformate ab")]
    public void Elf_check()
    {
        var r = Root();
        Assert.Null(FreePort.CheckElf(Elf(r)));
        Assert.Contains("MIPS", FreePort.CheckElf(Elf(r, "x86.elf", 3)));
        foreach (var n in new[] { "SLUS_123.45", "SLUS-203.12", "PBPX_952.01", "SCES_501.23", "TLES_123.45" })
            Assert.Contains("Spiel-Disc", FreePort.CheckElf(Elf(r, n)));
        File.WriteAllText(Path.Combine(r, "text.elf"), new string('x', 80));
        Assert.Contains("keine ELF", FreePort.CheckElf(Path.Combine(r, "text.elf")));
        Assert.Contains("Internet", FreePort.CheckElf("https://example.org/a.elf"));
        // M2: Kopie eines Disc-Programms unter neuem Namen, daneben die SYSTEM.CNF der Disc
        var disc = Path.Combine(r, "disc"); var copy = Elf(disc, "spiel.elf");
        File.WriteAllText(Path.Combine(disc, "SYSTEM.CNF"), "BOOT2 = cdrom0:\\SLUS_203.12;1\nVER = 1.00\nVMODE = NTSC\n");
        Assert.Contains("SYSTEM.CNF", FreePort.CheckElf(copy));
        Assert.Contains("SYSTEM.CNF", FreePort.CheckElf(Elf(Path.Combine(disc, "DATA"), "x.elf")));
    }

    [Fact(DisplayName = "Freies Spiel M2: Katalogtitel ist an die bekannte cubes_demo.elf gebunden (SHA-256)")]
    public void Catalog_title_bound_to_elf()
    {
        var r = Root();
        Assert.Equal("5af35384a6ed7ea880a0370a6490bd178a7e53a9b1a6179c75638c406613a8e8", Cubes.ElfSha256);
        var other = Elf(r, "cubes_demo.elf");
        Assert.Contains("Prüfsumme weicht ab", FreePort.CheckElf(other, Cubes));
        Assert.Null(FreePort.CheckElf(other, Own));
        var lic = License(r, GlampertLicense);
        Assert.False(FreePort.Prerequisites(Cubes, lic, other, Recomp(r), AllThere).Single(c => c.Id == "elf").Ok);
    }

    [Fact(DisplayName = "Freies Spiel M1: Lizenzprüfung lässt Umgehungen nicht durch")]
    public void License_bypass_rejected()
    {
        var r = Root(); var elf = Elf(r);
        string Lic(string text) => License(r, text);
        Assert.False(FreePort.CheckLicense(Lic("Dieses Spiel steht NICHT unter CC0. Alle Rechte vorbehalten."), Own, elf).Ok);
        Assert.False(FreePort.CheckLicense(Lic("ACC0UNT data. All rights reserved."), Own, elf).Ok);
        Assert.False(FreePort.CheckLicense(Lic("Proprietary EULA. Parts use the Apache License Version 2.0, January 2004, see NOTICE. TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION"), Own, elf).Ok);
        Assert.False(FreePort.CheckLicense(Lic("GNU GENERAL PUBLIC LICENSE Version 3\n" + Mit), Own, elf).Ok);
        Assert.False(FreePort.CheckLicense(Lic("Permission is hereby granted, free of charge"), Own, elf).Ok); // nur der Satzanfang
        Assert.Equal((true, "MIT"), FreePort.CheckLicense(Lic(Mit), Own, elf));
    }

    [Fact(DisplayName = "Freies Spiel M1: Lizenz muss im selben Projekt wie die ELF liegen; beim Katalogtitel genau die bekannte LICENSE")]
    public void License_same_project_and_hash()
    {
        var r = Root();
        var proj = Path.Combine(r, "projekt"); var elf = Elf(Path.Combine(proj, "source", "demos", "bin"), "cubes_demo.elf");
        var lic = License(proj, GlampertLicense);
        Assert.Equal((true, "MIT"), FreePort.CheckLicense(lic, Cubes, elf));
        File.WriteAllText(lic, GlampertLicense.Replace("\n", "\r\n")); // Git unter Windows: CRLF
        Assert.True(FreePort.CheckLicense(lic, Cubes, elf).Ok);
        var foreign = License(Path.Combine(r, "anderes"), GlampertLicense);
        Assert.Contains("selben Projekt", FreePort.CheckLicense(foreign, Cubes, elf).Text);
        File.WriteAllText(lic, Mit);
        Assert.Contains("Prüfsumme", FreePort.CheckLicense(lic, Cubes, elf).Text);
        Assert.False(FreePort.CheckLicense(lic, Cubes with { LicenseSha256 = null, License = "BSD" }, elf).Ok);
        Assert.False(FreePort.CheckLicense("", Own, elf).Ok);
    }

    [Fact(DisplayName = "Freies Spiel: config.toml enthält Eingabe, Ausgabe und die Bindungen; falsche Bindungen werden benannt")]
    public void Config_and_stubs()
    {
        Assert.Equal("MIT", Cubes.License);
        Assert.Contains("sceSifInitRpc@0x00122648", Cubes.Stubs);
        var toml = FreePort.Config(@"C:\spiel\cubes_demo.elf", @"C:\work\ausgabe", Cubes.Stubs);
        Assert.Contains("input = \"C:/spiel/cubes_demo.elf\"", toml);
        Assert.Contains("output = \"C:/work/ausgabe/\"", toml);
        Assert.Contains("\"ret0@0x001246C8\"", toml);
        Assert.Contains("[general]", toml);
        Assert.Null(FreePort.CheckStubs(Cubes.Stubs));
        Assert.Contains("rm -rf", FreePort.CheckStubs(["rm -rf"]));
    }

    [Fact(DisplayName = "Freies Spiel: Hänger heißt [run:tick] mit stehender pc")]
    public void Hang()
    {
        Assert.True(FreePort.IsHang(["[run:tick] pc=0x00100008", "[run:tick] pc=0x0010000C", "[run:tick] pc=0x0010000C", "[run:tick] pc=0x0010000C"]));
        Assert.False(FreePort.IsHang(["[run:tick] pc=0x00100008", "[run:tick] pc=0x0010000C", "[run:tick] pc=0x00100010"]));
        Assert.False(FreePort.IsHang(["nichts"]));
    }

    [Fact(DisplayName = "Freies Spiel: Voraussetzungen nennen fehlendes PS2Recomp, CMake, Compiler und zu lange Pfade")]
    public void Prerequisites()
    {
        var r = Root(); var lic = License(r); var elf = Elf(r);
        var none = FreePort.Prerequisites(Own, lic, elf, "", new FreePort.Probe(_ => null, () => null));
        Assert.True(none.Single(c => c.Id == "lizenz").Ok);
        Assert.True(none.Single(c => c.Id == "elf").Ok);
        Assert.Contains("github.com/ran-j/PS2Recomp", none.Single(c => c.Id == "recomp").Detail);
        Assert.False(none.Single(c => c.Id == "cmake").Ok);
        Assert.False(none.Single(c => c.Id == "compiler").Ok);
        var longPath = FreePort.Prerequisites(Own, lic, elf, Recomp(r), new FreePort.Probe(_ => "x", () => "y"));
        Assert.Contains("zu lang", longPath.Single(c => c.Id == "recomp").Detail);
        Assert.All(FreePort.Prerequisites(Own, lic, elf, Recomp(r), AllThere), c => Assert.True(c.Ok, c.Detail));
    }

    [Fact(DisplayName = "Freies Spiel N1: gesperrte ELF-Datei wird als ✕-Zeile gemeldet statt zu werfen")]
    public void Locked_elf_does_not_throw()
    {
        if (!OperatingSystem.IsWindows()) return; // Dateisperren gibt es so nur unter Windows
        var r = Root(); var lic = License(r); var elf = Elf(r);
        using var lockIt = new FileStream(elf, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var e = FreePort.Prerequisites(Own, lic, elf, Recomp(r), AllThere).Single(c => c.Id == "elf");
        Assert.False(e.Ok);
        Assert.Contains("nicht lesen", e.Detail);
    }

    [Fact(DisplayName = "Freies Spiel N4: CMake nur als .exe, eine .cmd im Suchpfad zählt nicht")]
    public void FindExe_only_exe()
    {
        if (!OperatingSystem.IsWindows()) return;
        var r = Root(); File.WriteAllText(Path.Combine(r, "trendofp-werkzeug.cmd"), "@echo x");
        var old = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", r + Path.PathSeparator + old);
            Assert.NotNull(Cli.Find("trendofp-werkzeug"));
            Assert.Null(FreePort.FindExe("trendofp-werkzeug"));
            File.WriteAllBytes(Path.Combine(r, "trendofp-werkzeug.exe"), [0x4D, 0x5A]);
            Assert.EndsWith(".exe", FreePort.FindExe("trendofp-werkzeug"));
        }
        finally { Environment.SetEnvironmentVariable("PATH", old); }
    }

    /// <summary>Nachbildung von CMake und PS2Recomp: legt die Dateien an, die die echten Werkzeuge erzeugen würden.</summary>
    public static FreePort.Runner FakeTools(List<string> calls, string gameFile = "game") => (exe, args, cwd, onLine, ct, timeout) =>
    {
        lock (calls) calls.Add(Path.GetFileName(exe) + " " + string.Join(' ', args));
        var a = args.ToList();
        if (Path.GetFileName(exe) == "cmake.exe" && a[0] == "--build")
        {
            var rel = Path.Combine(a[1], "Release"); Directory.CreateDirectory(rel);
            File.WriteAllText(Path.Combine(rel, a.Contains("--target") ? "ps2EntryRunner.exe" : "ps2recomp.exe"), "MZ " + string.Join(' ', a));
            if (a.Contains("--target")) { var obj = Path.Combine(a[1], "ps2EntryRunner.dir", "Release"); Directory.CreateDirectory(obj); File.WriteAllText(Path.Combine(obj, gameFile + ".obj"), "o"); }
        }
        if (Path.GetFileName(exe) == "ps2recomp.exe")
        {
            var cfg = File.ReadAllText(a[0]);
            var outDir = cfg.Split('\n').First(l => l.StartsWith("output")).Split('"')[1];
            File.WriteAllText(Path.Combine(outDir, gameFile + ".cpp"), "// x"); File.WriteAllText(Path.Combine(outDir, gameFile + ".h"), "// x");
        }
        onLine("ok");
        return Task.FromResult(new Cli.Result(0, "ok", ""));
    };

    static FreePort.Job Job(string r, string recomp, string work = "arbeit") { var proj = Path.Combine(r, "projekt"); return new(Own, Elf(proj), License(proj), recomp, [], Path.Combine(r, work)); }

    [Fact(DisplayName = "Freies Spiel: Ablauf mit nachgebildeten Werkzeugen bis zur Ergebnisdatei mit SHA-256, Liste der Dateien und Protokoll")]
    public async Task Run_to_result()
    {
        var r = Root(); var recomp = Recomp(r);
        var calls = new List<string>(); var steps = new List<string>();
        var o = await FreePort.RunAsync(Job(r, recomp), AllThere, FakeTools(calls), (i, n, s) => steps.Add(s), null, CancellationToken.None);
        Assert.Equal("fertig", o.Status);
        Assert.Equal(FreePort.Plan(recomp).Count, steps.Count);
        Assert.Equal(FreePort.Sha256(o.Program!), o.ProgramSha256);
        Assert.True(File.Exists(Path.Combine(recomp, "ps2xRuntime", "src", "runner", "game.cpp")));
        Assert.True(File.Exists(Path.Combine(recomp, "ps2xRuntime", "include", "game.h")));
        Assert.Contains(calls, c => c.EndsWith("--target ps2EntryRunner"));
        Assert.Equal(2, calls.Count(c => c.StartsWith("cmake.exe -S"))); // nach dem Einsortieren neu eingerichtet
        var json = File.ReadAllText(o.ResultPath!);
        Assert.Contains("programm_sha256", json); Assert.Contains("\"lizenz\": \"MIT\"", json);
        Assert.Contains("src/runner/game.cpp", json); Assert.Contains("elf_pfad", json);
        var log = File.ReadAllText(o.LogPath);
        Assert.Contains("Schritt 8/8", log); Assert.Contains("== FERTIG", log);
    }

    [Fact(DisplayName = "Freies Spiel M4: zweiter Lauf entfernt die Code-Dateien des ersten, löscht .obj und lässt Dateien von PS2Recomp in Ruhe")]
    public async Task Second_run_does_not_mix()
    {
        var r = Root(); var recomp = Recomp(r);
        var runner = Path.Combine(recomp, "ps2xRuntime", "src", "runner"); Directory.CreateDirectory(runner);
        File.WriteAllText(Path.Combine(runner, "main.cpp"), "// gehört PS2Recomp");
        var calls = new List<string>();
        Assert.Equal("fertig", (await FreePort.RunAsync(Job(r, recomp, "a"), AllThere, FakeTools(calls, "alt_von_spiel_A"), null, null, CancellationToken.None)).Status);
        var objA = Path.Combine(recomp, "build", "ps2EntryRunner.dir", "Release", "alt_von_spiel_A.obj");
        Assert.True(File.Exists(objA));
        var o = await FreePort.RunAsync(Job(r, recomp, "b"), AllThere, FakeTools(calls, "spiel_B"), null, null, CancellationToken.None);
        Assert.Equal("fertig", o.Status);
        Assert.False(File.Exists(Path.Combine(runner, "alt_von_spiel_A.cpp")));
        Assert.False(File.Exists(Path.Combine(recomp, "ps2xRuntime", "include", "alt_von_spiel_A.h")));
        Assert.False(File.Exists(objA));
        Assert.True(File.Exists(Path.Combine(runner, "spiel_B.cpp")));
        Assert.True(File.Exists(Path.Combine(runner, "main.cpp")));
        Assert.DoesNotContain("alt_von_spiel_A", File.ReadAllText(o.ResultPath!));
        // Eine Datei von PS2Recomp würde überschrieben: Fehler statt still ersetzen
        var c = await FreePort.RunAsync(Job(r, recomp, "c"), AllThere, FakeTools(calls, "main"), null, null, CancellationToken.None);
        Assert.Equal("fehler", c.Status);
        Assert.Contains("gehört zu PS2Recomp", c.Message);
        Assert.Equal("// gehört PS2Recomp", File.ReadAllText(Path.Combine(runner, "main.cpp")));
    }

    [Fact(DisplayName = "Freies Spiel N2: Protokoll wird während eines langen Schritts gesichert")]
    public async Task Log_flushed_during_step()
    {
        var r = Root(); var job = Job(r, Recomp(r));
        var log = Path.Combine(job.WorkDir, "protokoll.txt");
        var seen = false;
        var old = FreePort.LogFlushEvery;
        FreePort.LogFlushEvery = TimeSpan.Zero;
        try
        {
            FreePort.Runner slow = async (exe, args, cwd, onLine, ct, timeout) =>
            {
                onLine("Zwischenstand 42 %");
                seen = File.ReadAllText(log).Contains("Zwischenstand 42 %");
                await Task.Yield();
                return new(-1, "", "kaputt");
            };
            await FreePort.RunAsync(job, AllThere, slow, null, null, CancellationToken.None);
        }
        finally { FreePort.LogFlushEvery = old; }
        Assert.True(seen);
    }

    [Fact(DisplayName = "Freies Spiel: fehlt ein Werkzeug, startet nichts und das Protokoll sagt BLOCKIERT")]
    public async Task Blocked_runs_nothing()
    {
        var r = Root();
        var calls = new List<string>();
        var o = await FreePort.RunAsync(Job(r, Recomp(r)), new FreePort.Probe(_ => null, () => "cl", 4096), FakeTools(calls), null, null, CancellationToken.None);
        Assert.Equal("blockiert", o.Status);
        Assert.Contains("CMake", o.Message);
        Assert.Empty(calls);
        Assert.Contains("== BLOCKIERT", File.ReadAllText(o.LogPath));
    }

    [Fact(DisplayName = "Freies Spiel: Abbrechen während eines Schritts beendet den Lauf und vermerkt es")]
    public async Task Cancel()
    {
        var r = Root();
        using var cts = new CancellationTokenSource();
        FreePort.Runner slow = async (exe, args, cwd, onLine, ct, timeout) =>
        {
            cts.Cancel();
            try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { return new(Cli.Cancelled, "", "Abgebrochen"); }
            return new(0, "", "");
        };
        var job = Job(r, Recomp(r));
        var o = await FreePort.RunAsync(job, AllThere, slow, null, null, cts.Token);
        Assert.Equal("abgebrochen", o.Status);
        Assert.Contains("Recompiler einrichten", o.Message);
        Assert.False(File.Exists(Path.Combine(job.WorkDir, "ergebnis.json")));
    }

    [Fact(DisplayName = "Freies Spiel M3: Probelauf startet nur das gebaute, unveränderte Programm aus dem Build-Ordner mit der ELF aus dem Lauf")]
    public async Task Trial_target_checked()
    {
        var r = Root(); var recomp = Recomp(r); var job = Job(r, recomp);
        var o = await FreePort.RunAsync(job, AllThere, FakeTools([]), null, null, CancellationToken.None);
        var t = FreePort.ResolveTrial(o.ResultPath!, recomp);
        Assert.Null(t.Error);
        Assert.Equal(Path.GetFullPath(o.Program!), t.Program);
        Assert.Equal(Path.GetFullPath(job.Elf), t.Elf);
        var json = File.ReadAllText(o.ResultPath!);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node["programm"] = Path.Combine(Environment.SystemDirectory, "cmd.exe"); // fremdes Programm eingetragen
        File.WriteAllText(o.ResultPath!, node.ToJsonString());
        Assert.NotNull(FreePort.ResolveTrial(o.ResultPath!, recomp).Error);
        File.WriteAllText(o.ResultPath!, json);
        File.AppendAllText(o.Program!, "verändert"); // Programm nach dem Bauen verändert
        Assert.Contains("verändert", FreePort.ResolveTrial(o.ResultPath!, recomp).Error);
        File.WriteAllText(o.ResultPath!, "{ kaputt"); // kaputte Ergebnisdatei wirft nicht
        Assert.Contains("beschädigt", FreePort.ResolveTrial(o.ResultPath!, recomp).Error);
    }

    [Fact(DisplayName = "Freies Spiel: Probelauf startet einen echten Prozess, beendet ihn nach dem Zeitlimit und erkennt den Hänger")]
    public async Task Trial_real_process()
    {
        var py = Cli.Find("python") ?? Cli.Find("python3");
        Assert.NotNull(py); // Python ist Projektvoraussetzung (CLAUDE.md)
        var r = Root();
        var script = Path.Combine(r, "tick.py");
        File.WriteAllText(script, "import time,sys\nwhile True:\n    print('[run:tick] pc=0x0010000C', flush=True)\n    time.sleep(0.2)\n");
        // Probelauf ruft „Programm ELF“ auf – hier Python mit dem Skript an Stelle der ELF-Datei.
        var t = await FreePort.TrialAsync(py!, script, r, FreePort.RealRunner, 3, CancellationToken.None);
        Assert.Equal("hängt", t.Status);
        Assert.True(t.Ticks >= 3, t.Ticks.ToString());
        Assert.Contains("== hängt", File.ReadAllText(t.LogPath));
    }

    [Fact(DisplayName = "Freies Spiel N3: Probelauf ohne [run:tick] gilt nicht als Erfolg")]
    public async Task Trial_without_ticks_is_unclear()
    {
        var r = Root();
        FreePort.Runner quiet = (exe, args, cwd, onLine, ct, timeout) => Task.FromResult(new Cli.Result(-2, "", "Zeitüberschreitung"));
        var t = await FreePort.TrialAsync("x.exe", "y.elf", r, quiet, 1, CancellationToken.None);
        Assert.Equal("unklar", t.Status);
    }

    [Fact(DisplayName = "Freies Spiel: echte Prüfung auf diesem PC stimmt mit dem Suchpfad überein")]
    public void Real_probe_on_this_pc()
    {
        var r = Root(); var lic = License(r); var elf = Elf(r, "cubes_demo.elf");
        var checks = FreePort.Prerequisites(Own, lic, elf, Path.Combine("C:\\", "ps2recomp"), FreePort.SystemProbe);
        foreach (var c in checks) output.WriteLine($"[prüfung] {(c.Ok ? "OK " : "FEHLT")} {c.Label}: {c.Detail}");
        Assert.Equal(FreePort.FindExe("cmake") is not null, checks.Single(c => c.Id == "cmake").Ok);
        Assert.Equal(FreePort.FindCompiler() is not null, checks.Single(c => c.Id == "compiler").Ok);
        Assert.Equal(File.Exists(@"C:\ps2recomp\CMakeLists.txt") && Directory.Exists(@"C:\ps2recomp\ps2xRuntime"), checks.Single(c => c.Id == "recomp").Ok);
    }
}
