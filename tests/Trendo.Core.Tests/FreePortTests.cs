using Trendo.Core;
using Trendo.Core.ConsolePort;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0752: Ablauf „Freies Spiel portieren“ (STUDIO-CONSOLE-01).</summary>
public class FreePortTests(ITestOutputHelper output)
{
    public const string Mit = "MIT License\n\nCopyright (c) Erika Muster\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software ...";

    static string Root() { var d = Path.Combine(Path.GetTempPath(), "trendo-fp-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    /// <summary>Kleinste gültige PS2-ELF-Kopfzeile: 32 Bit, Little Endian, MIPS.</summary>
    public static string Elf(string dir, string name = "spiel.elf", byte machine = 8)
    {
        var b = new byte[64]; b[0] = 0x7F; b[1] = (byte)'E'; b[2] = (byte)'L'; b[3] = (byte)'F'; b[4] = 1; b[5] = 1; b[6] = 1; b[16] = 2; b[18] = machine;
        var f = Path.Combine(dir, name); File.WriteAllBytes(f, b); return f;
    }

    public static string Recomp(string dir) { var r = Path.Combine(dir, "ps2recomp"); Directory.CreateDirectory(Path.Combine(r, "ps2xRuntime")); File.WriteAllText(Path.Combine(r, "CMakeLists.txt"), "project(x)"); return r; }

    public static FreePort.Probe AllThere => new(n => n == "cmake" ? "cmake.exe" : null, () => "cl.exe", 4096);

    [Fact(DisplayName = "Freies Spiel: ELF-Prüfung erkennt PS2-Programme und weist Kaufspiele und Fremdformate ab")]
    public void Elf_check()
    {
        var r = Root();
        Assert.Null(FreePort.CheckElf(Elf(r)));
        Assert.Contains("MIPS", FreePort.CheckElf(Elf(r, "x86.elf", 3)));
        Assert.Contains("Kaufspiel", FreePort.CheckElf(Elf(r, "SLUS_123.45")));
        File.WriteAllText(Path.Combine(r, "text.elf"), new string('x', 80));
        Assert.Contains("keine ELF", FreePort.CheckElf(Path.Combine(r, "text.elf")));
        Assert.Contains("Internet", FreePort.CheckElf("https://example.org/a.elf"));
    }

    [Fact(DisplayName = "Freies Spiel: Lizenzdatei muss eine freie Lizenz belegen, beim Katalogtitel genau die erwartete")]
    public void License_check()
    {
        var r = Root();
        var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, Mit);
        Assert.Equal((true, "MIT"), FreePort.CheckLicense(lic, "MIT"));
        Assert.False(FreePort.CheckLicense(lic, "BSD").Ok);
        File.WriteAllText(lic, "Alle Rechte vorbehalten.");
        Assert.False(FreePort.CheckLicense(lic, "").Ok);
        Assert.False(FreePort.CheckLicense("", "MIT").Ok);
    }

    [Fact(DisplayName = "Freies Spiel: config.toml enthält Eingabe, Ausgabe und die Bindungen; falsche Bindungen werden benannt")]
    public void Config_and_stubs()
    {
        var t = FreePort.Find("cubes-demo")!;
        Assert.Equal("MIT", t.License);
        Assert.Contains("sceSifInitRpc@0x00122648", t.Stubs);
        var toml = FreePort.Config(@"C:\spiel\cubes_demo.elf", @"C:\work\ausgabe", t.Stubs);
        Assert.Contains("input = \"C:/spiel/cubes_demo.elf\"", toml);
        Assert.Contains("output = \"C:/work/ausgabe/\"", toml);
        Assert.Contains("\"ret0@0x001246C8\"", toml);
        Assert.Contains("[general]", toml);
        Assert.Null(FreePort.CheckStubs(t.Stubs));
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
        var r = Root(); var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, Mit);
        var t = FreePort.Titles[0];
        var none = FreePort.Prerequisites(t, lic, Elf(r), "", new FreePort.Probe(_ => null, () => null));
        Assert.True(none.Single(c => c.Id == "lizenz").Ok);
        Assert.True(none.Single(c => c.Id == "elf").Ok);
        Assert.Contains("github.com/ran-j/PS2Recomp", none.Single(c => c.Id == "recomp").Detail);
        Assert.False(none.Single(c => c.Id == "cmake").Ok);
        Assert.False(none.Single(c => c.Id == "compiler").Ok);
        var longPath = FreePort.Prerequisites(t, lic, Elf(r), Recomp(r), new FreePort.Probe(_ => "x", () => "y"));
        Assert.Contains("zu lang", longPath.Single(c => c.Id == "recomp").Detail);
        Assert.All(FreePort.Prerequisites(t, lic, Elf(r), Recomp(r), AllThere), c => Assert.True(c.Ok, c.Detail));
    }

    /// <summary>Nachbildung von CMake und PS2Recomp: legt die Dateien an, die die echten Werkzeuge erzeugen würden.</summary>
    public static FreePort.Runner FakeTools(List<string> calls) => (exe, args, cwd, onLine, ct, timeout) =>
    {
        calls.Add(Path.GetFileName(exe) + " " + string.Join(' ', args));
        var a = args.ToList();
        if (Path.GetFileName(exe) == "cmake.exe" && a[0] == "--build")
        {
            var rel = Path.Combine(a[1], "Release"); Directory.CreateDirectory(rel);
            File.WriteAllText(Path.Combine(rel, a.Contains("--target") ? "ps2EntryRunner.exe" : "ps2recomp.exe"), "MZ " + string.Join(' ', a));
        }
        if (Path.GetFileName(exe) == "ps2recomp.exe")
        {
            var cfg = File.ReadAllText(a[0]);
            var outDir = cfg.Split('\n').First(l => l.StartsWith("output")).Split('"')[1];
            File.WriteAllText(Path.Combine(outDir, "game.cpp"), "// x"); File.WriteAllText(Path.Combine(outDir, "game.h"), "// x");
        }
        onLine("ok");
        return Task.FromResult(new Cli.Result(0, "ok", ""));
    };

    [Fact(DisplayName = "Freies Spiel: Ablauf mit nachgebildeten Werkzeugen bis zur Ergebnisdatei mit SHA-256 und Protokoll")]
    public async Task Run_to_result()
    {
        var r = Root(); var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, Mit);
        var recomp = Recomp(r);
        var t = FreePort.Titles[0];
        var calls = new List<string>(); var steps = new List<string>();
        var o = await FreePort.RunAsync(new(t, Elf(r), lic, recomp, t.Stubs, Path.Combine(r, "arbeit")), AllThere, FakeTools(calls), (i, n, s) => steps.Add(s), null, CancellationToken.None);
        Assert.Equal("fertig", o.Status);
        Assert.Equal(FreePort.Plan(recomp).Count, steps.Count);
        Assert.Equal(FreePort.Sha256(o.Program!), o.ProgramSha256);
        Assert.True(File.Exists(Path.Combine(recomp, "ps2xRuntime", "src", "runner", "game.cpp")));
        Assert.True(File.Exists(Path.Combine(recomp, "ps2xRuntime", "include", "game.h")));
        Assert.Contains(calls, c => c.EndsWith("--target ps2EntryRunner"));
        var json = File.ReadAllText(o.ResultPath!);
        Assert.Contains("programm_sha256", json); Assert.Contains("\"lizenz\": \"MIT\"", json);
        var log = File.ReadAllText(o.LogPath);
        Assert.Contains("Schritt 7/7", log); Assert.Contains("== FERTIG", log);
    }

    [Fact(DisplayName = "Freies Spiel: fehlt ein Werkzeug, startet nichts und das Protokoll sagt BLOCKIERT")]
    public async Task Blocked_runs_nothing()
    {
        var r = Root(); var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, Mit);
        var calls = new List<string>();
        var o = await FreePort.RunAsync(new(FreePort.Titles[0], Elf(r), lic, Recomp(r), [], Path.Combine(r, "arbeit")), new FreePort.Probe(_ => null, () => "cl", 4096), FakeTools(calls), null, null, CancellationToken.None);
        Assert.Equal("blockiert", o.Status);
        Assert.Contains("CMake", o.Message);
        Assert.Empty(calls);
        Assert.Contains("== BLOCKIERT", File.ReadAllText(o.LogPath));
    }

    [Fact(DisplayName = "Freies Spiel: Abbrechen während eines Schritts beendet den Lauf und vermerkt es")]
    public async Task Cancel()
    {
        var r = Root(); var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, Mit);
        using var cts = new CancellationTokenSource();
        FreePort.Runner slow = async (exe, args, cwd, onLine, ct, timeout) =>
        {
            cts.Cancel();
            try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { return new(Cli.Cancelled, "", "Abgebrochen"); }
            return new(0, "", "");
        };
        var o = await FreePort.RunAsync(new(FreePort.Titles[0], Elf(r), lic, Recomp(r), [], Path.Combine(r, "arbeit")), AllThere, slow, null, null, cts.Token);
        Assert.Equal("abgebrochen", o.Status);
        Assert.Contains("Recompiler einrichten", o.Message);
        Assert.False(File.Exists(Path.Combine(r, "arbeit", "ergebnis.json")));
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

    [Fact(DisplayName = "Freies Spiel: echte Prüfung auf diesem PC stimmt mit dem Suchpfad überein")]
    public void Real_probe_on_this_pc()
    {
        var r = Root(); var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, Mit);
        var checks = FreePort.Prerequisites(FreePort.Titles[0], lic, Elf(r, "cubes_demo.elf"), Path.Combine("C:\\", "ps2recomp"), FreePort.SystemProbe);
        foreach (var c in checks) output.WriteLine($"[prüfung] {(c.Ok ? "OK " : "FEHLT")} {c.Label}: {c.Detail}");
        Assert.Equal(Cli.Find("cmake") is not null, checks.Single(c => c.Id == "cmake").Ok);
        Assert.Equal(FreePort.FindCompiler() is not null, checks.Single(c => c.Id == "compiler").Ok);
        Assert.Equal(File.Exists(@"C:\ps2recomp\CMakeLists.txt") && Directory.Exists(@"C:\ps2recomp\ps2xRuntime"), checks.Single(c => c.Id == "recomp").Ok);
    }
}
