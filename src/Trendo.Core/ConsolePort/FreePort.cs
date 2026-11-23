using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.ConsolePort;

/// <summary>T-0752: fester Ablauf „Freies Spiel portieren“ im Studio Konsolenspiele. Ein frei lizenziertes PS2-Homebrew-Programm (ELF) wird mit dem
/// Recompiler PS2Recomp zu einem Windows-Programm übersetzt. PS2Recomp (GPL-3.0) ist ein externes Werkzeug: Trendo liefert es nicht mit, übernimmt
/// keinen Code daraus und ruft es nur auf, wenn die Person es selbst eingerichtet hat. Kaufspiele und BIOS-Dateien sind ausgeschlossen.
/// Die Befehle folgen dem Lauf vom 09.11.2026 (cubes_demo.elf aus glampert/ps2-homebrew, MIT).</summary>
public static class FreePort
{
    /// <summary>Ein freier Titel mit belegter Lizenz. <paramref name="Stubs"/> sind die Bindungen für <c>general.stubs</c> – sie gelten nur für genau diese Datei.</summary>
    public sealed record Title(string Id, string Name, string Source, string License, string ElfName, IReadOnlyList<string> Stubs, string Note);

    public const string OwnId = "eigen";

    public static readonly IReadOnlyList<Title> Titles =
    [
        new("cubes-demo", "Würfel-Demo (cubes_demo)", "https://github.com/glampert/ps2-homebrew", "MIT", "cubes_demo.elf",
            ["sceSifInitRpc@0x00122648", "sceSifBindRpc@0x00121F70", "sceSifCallRpc@0x00122098", "sceSifGetOtherData@0x00122250", "ret0@0x001228C8", "ret0@0x00124470", "ret0@0x001246C8"],
            "Drehende Würfel, Homebrew-Demo. Die Bindungen gelten nur für genau diese ELF-Datei; eine anders gebaute Fassung braucht eigene Adressen."),
        new(OwnId, "Eigenes Homebrew-Programm", "", "", "", [],
            "Dein eigenes oder ein frei lizenziertes PS2-Programm. Lizenzdatei und Bindungen trägst du selbst ein."),
    ];

    public static Title? Find(string id) => Titles.FirstOrDefault(t => t.Id == id);

    /// <summary>PS2Recomp holt beim Einrichten Bausteine per FetchContent; bei langem Pfad bricht das unter Windows ab. Deshalb ein kurzer Ordner, z. B. C:\ps2recomp.</summary>
    public const int MaxRecompPath = 40;

    /// <summary>Freie Lizenzen, die als Beleg zählen: Kennzeichen im Lizenztext.</summary>
    public static readonly IReadOnlyList<(string Id, string Marker)> FreeLicenses =
    [
        ("MIT", "Permission is hereby granted, free of charge"),
        ("BSD", "Redistribution and use in source and binary forms"),
        ("zlib", "This software is provided 'as-is', without any express or implied"),
        ("Apache-2.0", "Apache License"),
        ("Unlicense", "This is free and unencumbered software released into the public domain"),
        ("CC0", "CC0"),
    ];

    /// <summary>Lizenzdatei lesen und die freie Lizenz erkennen. Ok=false: Grund in Klartext.</summary>
    public static (bool Ok, string Text) CheckLicense(string path, string expected)
    {
        if (string.IsNullOrWhiteSpace(path)) return (false, "Wähle die Lizenzdatei des Projekts (LICENSE).");
        if (path.Contains("://", StringComparison.Ordinal)) return (false, "Nur eine Datei auf diesem PC.");
        if (!File.Exists(path)) return (false, "Die Lizenzdatei wurde nicht gefunden.");
        if (new FileInfo(path).Length > 256 * 1024) return (false, "Das ist keine Lizenzdatei (zu groß).");
        var text = File.ReadAllText(path);
        foreach (var (id, marker) in FreeLicenses)
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                if (expected.Length > 0 && !string.Equals(id, expected, StringComparison.OrdinalIgnoreCase)) return (false, $"Erwartet war {expected}, die Datei enthält {id}.");
                return (true, id);
            }
        return (false, "In der Datei steht keine freie Lizenz (MIT, BSD, zlib, Apache, Unlicense, CC0). Ohne belegte Lizenz portiert Trendo nichts.");
    }

    static readonly Regex Retail = new(@"^S[CL][A-Z]{2}_\d{3}\.\d{2}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Prüft, ob die Datei ein PS2-Programm (ELF, 32 Bit, MIPS, Little Endian) ist und nicht das Startprogramm eines Kaufspiels. null = passt.</summary>
    public static string? CheckElf(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Wähle das Programm des Spiels (.elf).";
        if (path.Contains("://", StringComparison.Ordinal)) return "Nur eine Datei auf diesem PC. Trendo lädt keine Spiele aus dem Internet.";
        if (Retail.IsMatch(Path.GetFileName(path))) return "Das ist das Startprogramm eines Kaufspiels. Hier gehen nur eigene oder frei lizenzierte Programme.";
        if (!File.Exists(path)) return "Die Datei wurde nicht gefunden.";
        Span<byte> h = stackalloc byte[20];
        using var f = File.OpenRead(path);
        if (f.Length < 52) return "Die Datei ist zu klein für ein Programm.";
        f.ReadExactly(h);
        if (h[0] != 0x7F || h[1] != (byte)'E' || h[2] != (byte)'L' || h[3] != (byte)'F') return "Das ist keine ELF-Programmdatei.";
        if (h[4] != 1 || h[5] != 1) return "Das ist kein 32-Bit-Programm im PS2-Format.";
        if ((h[18] | h[19] << 8) != 8) return "Das Programm ist nicht für den PS2-Prozessor (MIPS) gebaut.";
        return null;
    }

    public static string Sha256(string path) { using var s = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(s)); }

    /// <summary>Wie Trendo Programme auf dem PC findet. In Tests ersetzbar.</summary>
    public sealed record Probe(Func<string, string?> Find, Func<string?> Compiler, int MaxPath = MaxRecompPath);
    public static Probe SystemProbe => new(Cli.Find, FindCompiler);

    /// <summary>C++-Compiler: im Suchpfad oder eine Visual-Studio-Installation mit MSVC (CMake findet sie selbst).</summary>
    public static string? FindCompiler()
    {
        foreach (var n in new[] { "cl", "clang-cl", "clang++", "g++" }) if (Cli.Find(n) is { } p) return p;
        if (!OperatingSystem.IsWindows()) return null;
        foreach (var pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            var vs = Path.Combine(pf, "Microsoft Visual Studio");
            if (!Directory.Exists(vs)) continue;
            try
            {
                foreach (var ver in Directory.GetDirectories(vs))
                    foreach (var ed in Directory.GetDirectories(ver))
                    {
                        var msvc = Path.Combine(ed, "VC", "Tools", "MSVC");
                        if (!Directory.Exists(msvc)) continue;
                        foreach (var t in Directory.GetDirectories(msvc).OrderDescending())
                            if (File.Exists(Path.Combine(t, "bin", "Hostx64", "x64", "cl.exe"))) return Path.Combine(t, "bin", "Hostx64", "x64", "cl.exe");
                    }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return null;
    }

    public sealed record Check(string Id, string Label, bool Ok, string Detail);

    /// <summary>Alle Voraussetzungen vor dem ersten Schritt. Was fehlt, steht im Klartext mit Einrichten-Hinweis.</summary>
    public static IReadOnlyList<Check> Prerequisites(Title t, string license, string elf, string recompDir, Probe probe)
    {
        var list = new List<Check>();
        var lic = CheckLicense(license, t.License);
        list.Add(new("lizenz", "Lizenz belegt", lic.Ok, lic.Ok ? lic.Text + " – " + Path.GetFileName(license) : lic.Text));
        var e = CheckElf(elf);
        list.Add(new("elf", "Programm des Spiels", e is null, e ?? Path.GetFileName(elf) + " · SHA-256 " + Sha256(elf)[..16] + "…"));
        list.Add(RecompCheck(recompDir, probe.MaxPath));
        var cmake = probe.Find("cmake");
        list.Add(new("cmake", "CMake 3.20 oder neuer", cmake is not null, cmake is not null ? "gefunden" : "fehlt – CMake von cmake.org einrichten (portable ZIP genügt) und in den Suchpfad legen."));
        var cc = probe.Compiler();
        list.Add(new("compiler", "C++20-Compiler", cc is not null, cc is not null ? "gefunden" : "fehlt – Visual Studio mit „Desktopentwicklung mit C++“ oder Clang einrichten."));
        return list;
    }

    static Check RecompCheck(string dir, int maxPath)
    {
        const string Hint = " PS2Recomp (GPL-3.0) selbst von github.com/ran-j/PS2Recomp holen und den Ordner hier wählen. Trendo liefert es nicht mit.";
        if (string.IsNullOrWhiteSpace(dir)) return new("recomp", "PS2Recomp eingerichtet", false, "Noch kein Ordner gewählt." + Hint);
        if (!Directory.Exists(dir)) return new("recomp", "PS2Recomp eingerichtet", false, "Der Ordner fehlt." + Hint);
        if (!File.Exists(Path.Combine(dir, "CMakeLists.txt")) || !Directory.Exists(Path.Combine(dir, "ps2xRuntime"))) return new("recomp", "PS2Recomp eingerichtet", false, "Im Ordner liegt kein PS2Recomp (CMakeLists.txt und ps2xRuntime fehlen)." + Hint);
        if (Path.GetFullPath(dir).Length > maxPath) return new("recomp", "PS2Recomp eingerichtet", false, $"Der Pfad ist zu lang ({Path.GetFullPath(dir).Length} Zeichen, höchstens {maxPath}). Lege PS2Recomp in einen kurzen Ordner wie C:\\ps2recomp – sonst bricht das Einrichten ab.");
        return new("recomp", "PS2Recomp eingerichtet", true, "gefunden");
    }

    static string Toml(string s) => "\"" + s.Replace('\\', '/').Replace("\"", "\\\"") + "\"";

    /// <summary>Einstellungen für den Recompiler (config.toml).</summary>
    public static string Config(string elf, string outDir, IEnumerable<string> stubs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Erstellt von Trendo OS – Ablauf „Freies Spiel portieren“");
        sb.AppendLine("[general]");
        sb.AppendLine("input = " + Toml(elf));
        sb.AppendLine("output = " + Toml(outDir.TrimEnd('\\', '/') + "/"));
        sb.AppendLine("single_file_output = false");
        sb.AppendLine("stubs = [" + string.Join(", ", stubs.Select(s => s.Trim()).Where(s => s.Length > 0).Select(Toml)) + "]");
        return sb.ToString();
    }

    /// <summary>Bindungen aus dem Eingabefeld: eine je Zeile, Form <c>name@0xADRESSE</c> oder nur <c>name</c>. Liefert Fehlerzeile oder null.</summary>
    public static string? CheckStubs(IEnumerable<string> stubs)
    {
        var ok = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(@0x[0-9A-Fa-f]{1,8})?$", RegexOptions.CultureInvariant);
        foreach (var s in stubs.Select(x => x.Trim()).Where(x => x.Length > 0))
            if (!ok.IsMatch(s)) return $"Die Bindung „{s}“ hat nicht die Form name@0xADRESSE.";
        return null;
    }

    public enum Kind { Run, Config, Translate, Sort, Result }

    /// <summary>Ein Schritt des Ablaufs. Bei <see cref="Kind.Run"/> ist <paramref name="Args"/> der Aufruf von CMake.</summary>
    public sealed record Step(string Id, string Label, Kind Kind, IReadOnlyList<string> Args, int TimeoutMin);

    public const string RunnerTarget = "ps2EntryRunner";

    public static IReadOnlyList<Step> Plan(string recompDir)
    {
        var build = Path.Combine(recompDir, "build");
        return
        [
            new("einrichten", "Recompiler einrichten", Kind.Run, ["-S", recompDir, "-B", build], 30),
            new("werkzeug", "Recompiler bauen", Kind.Run, ["--build", build, "--config", "Release"], 60),
            new("einstellungen", "Einstellungen schreiben", Kind.Config, [], 1),
            new("uebersetzen", "Spiel übersetzen", Kind.Translate, [], 30),
            new("einsortieren", "Erzeugten Code einsortieren", Kind.Sort, [], 1),
            new("programm", "Windows-Programm bauen", Kind.Run, ["--build", build, "--config", "Release", "--target", RunnerTarget], 60),
            new("ergebnis", "Ergebnis festhalten", Kind.Result, [], 1),
        ];
    }

    /// <summary>Datei im Build-Ordner suchen (ohne Groß-/Kleinschreibung). Neueste zuerst.</summary>
    public static string? FindBuilt(string buildDir, params string[] names)
    {
        if (!Directory.Exists(buildDir)) return null;
        return Directory.EnumerateFiles(buildDir, "*.exe", SearchOption.AllDirectories)
            .Where(f => names.Any(n => string.Equals(Path.GetFileNameWithoutExtension(f), n, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>Erzeugte .cpp nach ps2xRuntime/src/runner, .h nach ps2xRuntime/include. Liefert die Anzahl.</summary>
    public static (int Cpp, int H) Distribute(string outDir, string recompDir)
    {
        var src = Path.Combine(recompDir, "ps2xRuntime", "src", "runner");
        var inc = Path.Combine(recompDir, "ps2xRuntime", "include");
        Directory.CreateDirectory(src); Directory.CreateDirectory(inc);
        int c = 0, h = 0;
        foreach (var f in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext == ".cpp") { File.Copy(f, Path.Combine(src, Path.GetFileName(f)), true); c++; }
            else if (ext is ".h" or ".hpp") { File.Copy(f, Path.Combine(inc, Path.GetFileName(f)), true); h++; }
        }
        return (c, h);
    }

    static readonly Regex Tick = new(@"\[run:tick\].*?pc\s*[=:]\s*(0x[0-9A-Fa-f]+)", RegexOptions.CultureInvariant);

    /// <summary>Hänger erkennen: die letzten <paramref name="same"/> Meldungen <c>[run:tick]</c> zeigen dieselbe pc.</summary>
    public static bool IsHang(IEnumerable<string> lines, int same = 3)
    {
        var pcs = lines.Select(l => Tick.Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value.ToLowerInvariant()).TakeLast(same).ToList();
        return pcs.Count == same && pcs.Distinct().Count() == 1;
    }

    public static int Ticks(IEnumerable<string> lines) => lines.Count(l => Tick.IsMatch(l));

    /// <summary>Aufruf eines Programms mit Zeilenausgabe und Abbruch (Standard: <see cref="Cli.RunStreamAsync"/>).</summary>
    public delegate Task<Cli.Result> Runner(string exe, IReadOnlyList<string> args, string cwd, Action<string> onLine, CancellationToken ct, int timeoutMs);

    public static readonly Runner RealRunner = (exe, args, cwd, onLine, ct, timeout) => Cli.RunStreamAsync(exe, args, cwd, onLine, ct, timeout);

    public sealed record Job(Title Title, string Elf, string License, string RecompDir, IReadOnlyList<string> Stubs, string WorkDir);

    /// <summary>Ergebnis eines Laufs. Status: fertig, fehler, abgebrochen, blockiert.</summary>
    public sealed record Outcome(string Status, string Message, string? Program, string? ProgramSha256, string LogPath, string? ResultPath);

    /// <summary>Führt den ganzen Ablauf aus. Jeder Schritt landet mit Befehl, Ausgabe und Exitcode in protokoll.txt; das Ergebnis in ergebnis.json.</summary>
    public static async Task<Outcome> RunAsync(Job job, Probe probe, Runner runner, Action<int, int, string>? progress, Action<string>? onLine, CancellationToken ct)
    {
        Directory.CreateDirectory(job.WorkDir);
        var log = Path.Combine(job.WorkDir, "protokoll.txt");
        var started = DateTime.UtcNow;
        var lines = new List<string>();
        void Log(string l) { lock (lines) lines.Add(l); onLine?.Invoke(l); }
        void Flush() { string text; lock (lines) text = string.Join("\n", lines) + "\n"; AtomicFile.WriteAllText(log, text); }
        Outcome Done(string status, string msg, string? exe = null, string? sha = null, string? result = null) { Log($"== {status.ToUpperInvariant()}: {msg}"); Flush(); return new(status, msg, exe, sha, log, result); }

        Log($"Trendo OS · Freies Spiel portieren · {job.Title.Name} · Beginn {started:yyyy-MM-dd HH:mm:ss} UTC");
        var checks = Prerequisites(job.Title, job.License, job.Elf, job.RecompDir, probe);
        foreach (var c in checks) Log($"[prüfung] {(c.Ok ? "OK " : "FEHLT")} {c.Label}: {c.Detail}");
        if (CheckStubs(job.Stubs) is { } bad) return Done("blockiert", bad);
        if (checks.FirstOrDefault(c => !c.Ok) is { } miss) return Done("blockiert", miss.Label + ": " + miss.Detail);
        var cmake = probe.Find("cmake")!;
        var steps = Plan(job.RecompDir);
        var build = Path.Combine(job.RecompDir, "build");
        var outDir = Path.Combine(job.WorkDir, "ausgabe");
        var cfg = Path.Combine(job.WorkDir, "config.toml");
        var stepLog = new JsonArray();
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            if (ct.IsCancellationRequested) return Done("abgebrochen", "Vor „" + s.Label + "“ abgebrochen.");
            progress?.Invoke(i, steps.Count, s.Label);
            Log($"-- Schritt {i + 1}/{steps.Count}: {s.Label}");
            Flush();
            var exit = 0;
            try
            {
                switch (s.Kind)
                {
                    case Kind.Run:
                    case Kind.Translate:
                        string exe; IReadOnlyList<string> args;
                        if (s.Kind == Kind.Run) { exe = cmake; args = s.Args; }
                        else
                        {
                            exe = FindBuilt(build, "ps2recomp", "ps2xRecomp", "ps2_recomp") ?? "";
                            if (exe.Length == 0) return Done("fehler", "Der Recompiler (ps2recomp.exe) wurde nach dem Bauen nicht gefunden.");
                            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                            Directory.CreateDirectory(outDir);
                            args = [cfg];
                        }
                        Log("$ " + Path.GetFileName(exe) + " " + string.Join(' ', args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a)));
                        var r = await runner(exe, args, s.Kind == Kind.Run ? job.RecompDir : job.WorkDir, l => Log("  " + l), ct, s.TimeoutMin * 60_000).ConfigureAwait(false);
                        exit = r.Exit;
                        if (r.Exit == Cli.Cancelled) { stepLog.Add(new JsonObject { ["schritt"] = s.Id, ["exit"] = r.Exit }); return Done("abgebrochen", "Während „" + s.Label + "“ abgebrochen, das Programm wurde beendet."); }
                        if (!r.Ok)
                        {
                            stepLog.Add(new JsonObject { ["schritt"] = s.Id, ["exit"] = r.Exit });
                            var tail = (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n').TakeLast(3);
                            return Done("fehler", $"„{s.Label}“ ging nicht (Exitcode {r.Exit}): " + string.Join(" ", tail).Trim());
                        }
                        if (s.Kind == Kind.Translate && !Directory.EnumerateFiles(outDir, "*.cpp", SearchOption.AllDirectories).Any()) return Done("fehler", "Der Recompiler hat keinen Code erzeugt.");
                        break;
                    case Kind.Config:
                        AtomicFile.WriteAllText(cfg, Config(job.Elf, outDir, job.Stubs));
                        Log("  config.toml geschrieben, " + job.Stubs.Count(x => x.Trim().Length > 0) + " Bindungen");
                        break;
                    case Kind.Sort:
                        var (cpp, h) = Distribute(outDir, job.RecompDir);
                        Log($"  {cpp} .cpp nach ps2xRuntime/src/runner, {h} .h nach ps2xRuntime/include");
                        break;
                    case Kind.Result:
                        var prog = FindBuilt(build, RunnerTarget);
                        if (prog is null) return Done("fehler", "Das Windows-Programm (" + RunnerTarget + ".exe) wurde nicht gefunden.");
                        var sha = Sha256(prog);
                        var result = Path.Combine(job.WorkDir, "ergebnis.json");
                        stepLog.Add(new JsonObject { ["schritt"] = s.Id, ["exit"] = 0 });
                        var json = new JsonObject
                        {
                            ["titel"] = job.Title.Name, ["quelle"] = job.Title.Source, ["lizenz"] = CheckLicense(job.License, job.Title.License).Text,
                            ["elf"] = Path.GetFileName(job.Elf), ["elf_sha256"] = Sha256(job.Elf),
                            ["programm"] = prog, ["programm_sha256"] = sha, ["programm_bytes"] = new FileInfo(prog).Length,
                            ["start_argument"] = job.Elf, ["beginn_utc"] = started.ToString("o"), ["ende_utc"] = DateTime.UtcNow.ToString("o"), ["schritte"] = stepLog,
                        };
                        AtomicFile.WriteAllText(result, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                        return Done("fertig", $"{Path.GetFileName(prog)} gebaut, SHA-256 {sha}", prog, sha, result);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Done("fehler", $"„{s.Label}“: " + FriendlyError.Say(ex));
            }
            stepLog.Add(new JsonObject { ["schritt"] = s.Id, ["exit"] = exit });
        }
        return Done("fehler", "Der Ablauf endete ohne Ergebnis.");
    }

    /// <summary>Probelauf des fertigen Programms. Status: läuft (bis zum Zeitlimit ohne Absturz, danach beendet), hängt, beendet, absturz, abgebrochen.</summary>
    public sealed record Trial(string Status, string Message, int Exit, int Ticks, string LogPath);

    /// <summary>Startet nur das eigene, eben gebaute Programm kurz mit der ELF-Datei, beendet es nach <paramref name="seconds"/> s und schreibt probelauf.txt.</summary>
    public static async Task<Trial> TrialAsync(string program, string elf, string workDir, Runner runner, int seconds, CancellationToken ct)
    {
        Directory.CreateDirectory(workDir);
        var log = Path.Combine(workDir, "probelauf.txt");
        var lines = new List<string>();
        var r = await runner(program, [elf], Path.GetDirectoryName(program) ?? workDir, l => { lock (lines) lines.Add(l); }, ct, seconds * 1000).ConfigureAwait(false);
        List<string> all; lock (lines) all = [.. lines];
        var ticks = Ticks(all);
        var (status, msg) = r.Exit switch
        {
            Cli.Cancelled => ("abgebrochen", "Probelauf abgebrochen, das Programm wurde beendet."),
            -2 when IsHang(all) => ("hängt", $"Das Programm hängt: {ticks} Meldungen [run:tick] mit stehender pc."),
            -2 => ("läuft", $"Das Programm lief {seconds} s ohne Absturz ({ticks} Meldungen [run:tick]) und wurde danach beendet."),
            0 => ("beendet", "Das Programm hat sich selbst ordentlich beendet."),
            _ => ("absturz", $"Das Programm endete mit Exitcode {r.Exit}."),
        };
        AtomicFile.WriteAllText(log, $"Probelauf {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC · {Path.GetFileName(program)} {Path.GetFileName(elf)} · {seconds} s\n" + string.Join("\n", all) + (r.Err.Length > 0 ? "\n[stderr]\n" + r.Err : "") + $"\n== {status}: {msg}\n");
        return new(status, msg, r.Exit, ticks, log);
    }
}
