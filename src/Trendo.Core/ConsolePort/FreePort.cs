using System.Diagnostics;
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
    /// <summary>Ein freier Titel mit belegter Lizenz. <paramref name="Stubs"/> sind die Bindungen für <c>general.stubs</c> – sie gelten nur für genau diese Datei.
    /// Bei Katalogtiteln sind ELF-Datei und Lizenzdatei über SHA-256 festgelegt (Lizenz mit Zeilenenden LF).</summary>
    public sealed record Title(string Id, string Name, string Source, string License, string ElfName, IReadOnlyList<string> Stubs, string Note, string? ElfSha256 = null, string? LicenseSha256 = null);

    public const string OwnId = "eigen";

    public static readonly IReadOnlyList<Title> Titles =
    [
        // Prüfsummen aus https://github.com/glampert/ps2-homebrew, Stand 602441a6877a3136709d6320664340d52e3027a1: source/demos/bin/cubes_demo.elf und LICENSE.txt
        new("cubes-demo", "Würfel-Demo (cubes_demo)", "https://github.com/glampert/ps2-homebrew", "MIT", "cubes_demo.elf",
            ["sceSifInitRpc@0x00122648", "sceSifBindRpc@0x00121F70", "sceSifCallRpc@0x00122098", "sceSifGetOtherData@0x00122250", "ret0@0x001228C8", "ret0@0x00124470", "ret0@0x001246C8"],
            "Drehende Würfel, Homebrew-Demo. Datei source/demos/bin/cubes_demo.elf, Lizenz LICENSE.txt im selben Projekt. Die Bindungen gelten nur für genau diese ELF-Datei.",
            "5af35384a6ed7ea880a0370a6490bd178a7e53a9b1a6179c75638c406613a8e8", "45b4891f1852bf3d3f4425fa1ff60398afd703675841d902ee7f8aaf4ba950d3"),
        new(OwnId, "Eigenes Homebrew-Programm", "", "", "", [],
            "Dein eigenes oder ein frei lizenziertes PS2-Programm. Die Lizenzdatei muss im selben Projekt liegen wie das Programm; Bindungen trägst du selbst ein."),
    ];

    public static Title? Find(string id) => Titles.FirstOrDefault(t => t.Id == id);

    /// <summary>PS2Recomp holt beim Einrichten Bausteine per FetchContent; bei langem Pfad bricht das unter Windows ab. Deshalb ein kurzer Ordner, z. B. C:\ps2recomp.</summary>
    public const int MaxRecompPath = 40;

    /// <summary>Freie Lizenzen: alle Kennzeichen müssen im Text stehen (Leerraum zusammengefasst, ohne Groß-/Kleinschreibung).</summary>
    public static readonly IReadOnlyList<(string Id, string[] Markers)> FreeLicenses =
    [
        ("MIT", ["Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files", "without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell"]),
        ("BSD", ["Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met"]),
        ("zlib", ["This software is provided 'as-is', without any express or implied warranty. In no event will the authors be held liable for any damages arising from the use of this software.", "Permission is granted to anyone to use this software for any purpose, including commercial applications"]),
        ("Apache-2.0", ["Apache License Version 2.0, January 2004", "TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION"]),
        ("Unlicense", ["This is free and unencumbered software released into the public domain.", "Anyone is free to copy, modify, publish, use, compile, sell, or distribute this software"]),
        ("CC0", ["CC0 1.0 Universal", "Statement of Purpose"]),
    ];

    /// <summary>Hinweise auf unfreie oder fremde Lizenzbedingungen: dann gilt die Datei nicht als Beleg.</summary>
    /// T-0757 R1: auch Weitergabe- und Kopierverbote und Vertraulichkeit – sonst gälte ein angehängter MIT-Satz in einer sonst unfreien Datei als MIT.
    static readonly string[] NotFree = ["eula", "proprietary", "proprietär", "gpl", "general public license",
        "nicht weitergegeben", "darf nicht weitergegeben", "confidential", "vertraulich", "all copying prohibited", "kopieren verboten"];

    /// <summary>Stehen oft in der Copyright-Zeile echter MIT- und BSD-Lizenzen. Sie zählen nur, wenn keine vollständige freie Lizenz im Text steht.</summary>
    static readonly string[] Reserved = ["all rights reserved", "alle rechte vorbehalten"];

    /// <summary>So weit darf die Lizenzdatei über dem Ordner des Programms liegen (z. B. LICENSE.txt im Projekt, Programm in source/demos/bin).</summary>
    public const int MaxLicenseUp = 3;

    /// <summary>Kommentarzeichen am Zeilenanfang (// # * ;) entfernen – Lizenzen wie die von ANGLE stehen als Quelltext-Kommentar – und Leerraum zusammenfassen.</summary>
    static string Norm(string s) => Regex.Replace(Regex.Replace(s, @"(?m)^[ \t]*(//+|#+|\*+|;+)[ \t]?", ""), @"\s+", " ").Trim();

    /// <summary>SHA-256 einer Textdatei mit Zeilenenden LF (unabhängig davon, ob Git sie mit CRLF ausgecheckt hat).</summary>
    public static string TextSha256(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path).Where(b => b != (byte)'\r').ToArray()));

    static StringComparison PathCmp => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    static string Dir(string p) => Path.GetFullPath(p).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;

    /// <summary>Liegt die Lizenzdatei im selben Projekt wie das Programm? Ihr Ordner ist der des Programms oder höchstens <see cref="MaxLicenseUp"/> Ebenen
    /// darüber – nie der Laufwerksstamm, der Benutzerordner oder ein Ordner darüber.</summary>
    public static bool SameProject(string license, string elf)
    {
        var l = Dir(Path.GetDirectoryName(Path.GetFullPath(license))!);
        var e = Dir(Path.GetDirectoryName(Path.GetFullPath(elf))!);
        if (!e.StartsWith(l, PathCmp)) return false;
        if (string.Equals(l, Dir(Path.GetPathRoot(l)!), PathCmp)) return false;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0 && Dir(home).StartsWith(l, PathCmp)) return false;
        var up = e[l.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;
        return up <= MaxLicenseUp;
    }

    /// <summary>Lizenzdatei lesen und die freie Lizenz erkennen. Ok=false: Grund in Klartext.</summary>
    public static (bool Ok, string Text) CheckLicense(string path, Title t, string elf)
    {
        if (string.IsNullOrWhiteSpace(path)) return (false, "Wähle die Lizenzdatei des Projekts (LICENSE).");
        if (path.Contains("://", StringComparison.Ordinal)) return (false, "Nur eine Datei auf diesem PC.");
        if (!File.Exists(path)) return (false, "Die Lizenzdatei wurde nicht gefunden.");
        if (new FileInfo(path).Length > 256 * 1024) return (false, "Das ist keine Lizenzdatei (zu groß).");
        if (!string.IsNullOrWhiteSpace(elf) && !SameProject(path, elf)) return (false, $"Die Lizenzdatei muss im selben Projekt liegen wie das Programm: im Ordner des Programms oder höchstens {MaxLicenseUp} Ebenen darüber, nicht im Laufwerksstamm oder Benutzerordner.");
        var text = Norm(File.ReadAllText(path));
        if (NotFree.FirstOrDefault(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)) is { } bad)
            return (false, $"In der Lizenzdatei steht „{bad}“. Das ist keine freie Lizenz für dieses Vorhaben – Trendo portiert nur MIT, BSD, zlib, Apache 2.0, Unlicense oder CC0.");
        var hit = FreeLicenses.FirstOrDefault(l => l.Markers.All(m => text.Contains(Norm(m), StringComparison.OrdinalIgnoreCase)));
        if (hit.Id is null && Reserved.FirstOrDefault(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)) is { } res)
            return (false, $"In der Lizenzdatei steht „{res}“, aber keine vollständige freie Lizenz. Trendo portiert nur MIT, BSD, zlib, Apache 2.0, Unlicense oder CC0.");
        if (hit.Id is null) return (false, "In der Datei steht keine vollständige freie Lizenz (MIT, BSD, zlib, Apache 2.0, Unlicense, CC0). Ohne belegte Lizenz portiert Trendo nichts.");
        if (t.License.Length > 0 && !string.Equals(hit.Id, t.License, StringComparison.OrdinalIgnoreCase)) return (false, $"Erwartet war {t.License}, die Datei enthält {hit.Id}.");
        if (t.LicenseSha256 is { } want && TextSha256(path) != want) return (false, $"Das ist nicht die Lizenzdatei von {t.Source} (Prüfsumme weicht ab).");
        return (true, hit.Id);
    }

    /// <summary>Kennungen von PS2-Discs: S[CL] + Region + Art (SLUS, SCES, SLPM, SCAJ …) sowie Demo- und Testdiscs (PBPX, PAPX, PCPX, TCES, TLES, TCPS …).</summary>
    static readonly Regex Retail = new(@"^(S[CL][AEKPU][ASMJDH]|P[ABC]PX|T[CL][EPU]S)[_-]\d{3}\.?\d{2}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    /// <summary>T-0757 R2: Nur eine SYSTEM.CNF, deren BOOT2 auf eine Disc-Kennung zeigt, gilt als Spiel-Disc. Ein eigenes Homebrew-Projekt hat oft eine
    /// SYSTEM.CNF mit <c>BOOT2 = cdrom0:\MEINSPIEL.ELF;1</c> – die ist erlaubt.</summary>
    static readonly Regex Boot2 = new(@"BOOT2\s*=\s*cdrom0:\\?(S[CL][AEKPU][ASMJDH]|P[ABC]PX|T[CL][EPU]S)[_-]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Prüft, ob die Datei ein PS2-Programm (ELF, 32 Bit, MIPS, Little Endian) ist und nicht von einer Spiel-Disc stammt. null = passt.
    /// Bei Katalogtiteln muss die Datei genau die bekannte sein (SHA-256).</summary>
    public static string? CheckElf(string path, Title? t = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Wähle das Programm des Spiels (.elf).";
        if (path.Contains("://", StringComparison.Ordinal)) return "Nur eine Datei auf diesem PC. Trendo lädt keine Spiele aus dem Internet.";
        if (Retail.IsMatch(Path.GetFileName(path))) return "Das ist das Startprogramm einer Spiel-Disc. Hier gehen nur eigene oder frei lizenzierte Programme.";
        if (!File.Exists(path)) return "Die Datei wurde nicht gefunden.";
        var full = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows() && Path.GetPathRoot(full) is { Length: > 0 } root)
        {
            try { if (new DriveInfo(root).DriveType == DriveType.CDRom) return "Die Datei liegt auf einer Disc oder einem eingebundenen Abbild. Hier gehen nur eigene oder frei lizenzierte Programme."; }
            catch (ArgumentException) { }
        }
        // SYSTEM.CNF liegt im Stamm der Disc: im Ordner der Datei und in allen Ordnern darüber suchen (bis zum Laufwerksstamm, höchstens 16 Ebenen)
        var level = 0;
        for (var dir = Path.GetDirectoryName(full); dir is not null && level < 16; dir = Path.GetDirectoryName(dir), level++)
        {
            var cnf = Path.Combine(dir, "SYSTEM.CNF");
            if (File.Exists(cnf) && new FileInfo(cnf).Length < 64 * 1024 && Boot2.IsMatch(File.ReadAllText(cnf))) return "Neben der Datei liegt eine SYSTEM.CNF, deren Startprogramm (BOOT2) die Kennung einer Spiel-Disc trägt. Hier gehen nur eigene oder frei lizenzierte Programme.";
        }
        Span<byte> h = stackalloc byte[20];
        using (var f = File.OpenRead(path))
        {
            if (f.Length < 52) return "Die Datei ist zu klein für ein Programm.";
            f.ReadExactly(h);
        }
        if (h[0] != 0x7F || h[1] != (byte)'E' || h[2] != (byte)'L' || h[3] != (byte)'F') return "Das ist keine ELF-Programmdatei.";
        if (h[4] != 1 || h[5] != 1) return "Das ist kein 32-Bit-Programm im PS2-Format.";
        if ((h[18] | h[19] << 8) != 8) return "Das Programm ist nicht für den PS2-Prozessor (MIPS) gebaut.";
        if (t?.ElfSha256 is { } want && Sha256(path) != want) return $"Das ist nicht die bekannte {t.ElfName} aus {t.Source} (Prüfsumme weicht ab). Für andere Programme wähle „Eigenes Homebrew-Programm“.";
        return null;
    }

    public static string Sha256(string path) { using var s = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(s)); }

    /// <summary>Programm im Suchpfad finden, unter Windows nur als .exe (eine .cmd/.bat würde die Argumente durch cmd.exe deuten lassen).</summary>
    public static string? FindExe(string name)
    {
        var file = OperatingSystem.IsWindows() ? name + ".exe" : name;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try { var p = Path.Combine(dir.Trim('"'), file); if (File.Exists(p)) return p; }
            catch (ArgumentException) { }
        }
        return null;
    }

    /// <summary>Wie Trendo Programme auf dem PC findet. In Tests ersetzbar.</summary>
    public sealed record Probe(Func<string, string?> Find, Func<string?> Compiler, int MaxPath = MaxRecompPath);
    public static Probe SystemProbe => new(FindExe, FindCompiler);

    /// <summary>C++-Compiler: im Suchpfad oder eine Visual-Studio-Installation mit MSVC (CMake findet sie selbst).</summary>
    public static string? FindCompiler()
    {
        foreach (var n in new[] { "cl", "clang-cl", "clang++", "g++" }) if (FindExe(n) is { } p) return p;
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

    static Check Safe(string id, string label, Func<Check> f)
    {
        try { return f(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(id, label, false, "Datei lässt sich gerade nicht lesen: " + FriendlyError.Say(e)); }
    }

    /// <summary>Alle Voraussetzungen vor dem ersten Schritt. Was fehlt, steht im Klartext mit Einrichten-Hinweis. Wirft nicht.</summary>
    public static IReadOnlyList<Check> Prerequisites(Title t, string license, string elf, string recompDir, Probe probe)
    {
        var list = new List<Check>
        {
            Safe("lizenz", "Lizenz belegt", () => { var lic = CheckLicense(license, t, elf); return new("lizenz", "Lizenz belegt", lic.Ok, lic.Ok ? lic.Text + " – " + Path.GetFileName(license) : lic.Text); }),
            Safe("elf", "Programm des Spiels", () => { var e = CheckElf(elf, t); return new("elf", "Programm des Spiels", e is null, e ?? Path.GetFileName(elf) + " · SHA-256 " + Sha256(elf)[..16] + "…"); }),
            Safe("recomp", "PS2Recomp eingerichtet", () => RecompCheck(recompDir, probe.MaxPath)),
        };
        var cmake = probe.Find("cmake");
        list.Add(new("cmake", "CMake 3.20 oder neuer", cmake is not null, cmake is not null ? "gefunden" : "fehlt – CMake von cmake.org einrichten (portable ZIP genügt) und cmake.exe in den Suchpfad legen."));
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

    /// <summary>Liste der zuletzt von Trendo in PS2Recomp einsortierten Dateien (relativ zu ps2xRuntime).</summary>
    public const string Manifest = "trendo-einsortiert.txt";

    static readonly Regex ManifestEntry = new(@"^(src/runner/[A-Za-z0-9_][A-Za-z0-9_.\-]*\.cpp|include/[A-Za-z0-9_][A-Za-z0-9_.\-]*\.(h|hpp))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
            new("neu-einrichten", "Neue Code-Dateien anmelden", Kind.Run, ["-S", recompDir, "-B", build], 30),
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

    /// <summary>Erzeugte .cpp nach ps2xRuntime/src/runner, .h nach ps2xRuntime/include. Vorher werden die beim letzten Mal von Trendo einsortierten Dateien
    /// entfernt (sonst mischt ein zweiter Lauf alten und neuen Code), Dateien von PS2Recomp selbst werden nie überschrieben. Danach werden die
    /// Zwischendateien (.obj) von ps2EntryRunner gelöscht. Liefert die einsortierten Dateien (relativ zu ps2xRuntime).
    /// T-0757 R3: Die Liste steht zusätzlich als Kopie in <paramref name="workDir"/> (Arbeitsordner des Projekts, nur Trendo schreibt dort).
    /// Als von Trendo einsortiert gilt nur, was in beiden Listen mit derselben Prüfsumme steht – eine im PS2Recomp-Ordner untergeschobene Liste
    /// löscht damit nichts. Steht eine Datei nur in der Liste im PS2Recomp-Ordner und liegt noch dort (Lauf aus einem anderen Arbeitsordner),
    /// wird nichts gelöscht und nichts gemischt: Fehler mit Hinweis.</summary>
    public static IReadOnlyList<string> Distribute(string outDir, string recompDir, string workDir)
    {
        var rt = Path.Combine(recompDir, "ps2xRuntime");
        var root = Dir(rt);
        var manifest = Path.Combine(rt, Manifest);
        var copy = Path.Combine(workDir, Manifest);
        // Nur gültige Einträge (src/runner/<name>.cpp, include/<name>.h|.hpp, ohne weitere Trenner) mit Prüfsumme gelten als von Trendo einsortiert;
        // gelöscht wird nur, was noch genau so aussieht, wie Trendo es hingelegt hat.
        static Dictionary<string, string> ReadList(string file)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(file))
                foreach (var line in File.ReadAllLines(file))
                    if (line.Split('\t') is [var rel, var sha] && ManifestEntry.IsMatch(rel) && Regex.IsMatch(sha, "^[0-9a-f]{64}$")) d[rel] = sha;
            return d;
        }
        var old = ReadList(manifest);
        var mine = ReadList(copy);
        string Full(string rel) => Path.GetFullPath(Path.Combine(rt, rel));
        bool Unchanged(KeyValuePair<string, string> kv) => Full(kv.Key).StartsWith(root, PathCmp) && (!File.Exists(Full(kv.Key)) || Sha256(Full(kv.Key)) == kv.Value);
        var owned = old.Where(kv => mine.TryGetValue(kv.Key, out var s2) && s2 == kv.Value && Unchanged(kv)).Select(kv => kv.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var foreign = old.Where(kv => !owned.Contains(kv.Key) && File.Exists(Full(kv.Key)) && Unchanged(kv)).Select(kv => kv.Key).ToList();
        if (foreign.Count > 0)
            throw new IOException($"Im PS2Recomp-Ordner liegen noch {foreign.Count} Code-Dateien, die dieser Arbeitsordner nicht selbst einsortiert hat (z. B. {foreign[0]}). Trendo löscht sie nicht und mischt keinen Code. Bitte PS2Recomp frisch einrichten oder im ursprünglichen Projekt portieren.");
        var plan = new List<(string From, string Rel)>();
        foreach (var f in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            var rel = ext == ".cpp" ? "src/runner/" + Path.GetFileName(f) : ext is ".h" or ".hpp" ? "include/" + Path.GetFileName(f) : null;
            if (rel is null) continue;
            if (!ManifestEntry.IsMatch(rel)) throw new IOException($"Der Recompiler hat eine Datei mit ungewöhnlichem Namen erzeugt: {Path.GetFileName(f)}");
            plan.Add((f, rel));
        }
        foreach (var (_, rel) in plan)
            if (!owned.Contains(rel) && File.Exists(Full(rel))) throw new IOException($"{rel} gehört zu PS2Recomp (nicht von Trendo einsortiert) und würde überschrieben. Bitte PS2Recomp frisch einrichten.");
        foreach (var rel in owned)
            if (File.Exists(Full(rel))) File.Delete(Full(rel));
        Directory.CreateDirectory(Path.Combine(rt, "src", "runner")); Directory.CreateDirectory(Path.Combine(rt, "include"));
        foreach (var (from, rel) in plan) File.Copy(from, Path.Combine(rt, rel), true);
        var placed = plan.Select(p => p.Rel).ToList();
        var listText = string.Join("\n", placed.Select(r => r + "\t" + Sha256(Full(r)))) + "\n";
        Directory.CreateDirectory(workDir);
        AtomicFile.WriteAllText(copy, listText);
        AtomicFile.WriteAllText(manifest, listText);
        var build = Path.Combine(recompDir, "build");
        if (Directory.Exists(build))
            foreach (var d in Directory.EnumerateDirectories(build, "*" + RunnerTarget + "*.dir", SearchOption.AllDirectories).ToList())
                foreach (var o in Directory.EnumerateFiles(d, "*.obj", SearchOption.AllDirectories).ToList()) File.Delete(o);
        return placed;
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

    /// <summary><paramref name="RightsConfirmed"/>: T-0757 R1 – bei „Eigenes Homebrew“ hat die Person ausdrücklich bestätigt: <see cref="RightsText"/>.</summary>
    public sealed record Job(Title Title, string Elf, string License, string RecompDir, IReadOnlyList<string> Stubs, string WorkDir, bool RightsConfirmed = false);

    /// <summary>T-0757 R1: Bestätigung vor dem Start bei „Eigenes Homebrew“; sie steht in ergebnis.json.</summary>
    public const string RightsText = "Ich habe die Rechte an diesem Programm";

    /// <summary>Braucht dieser Titel die ausdrückliche Bestätigung <see cref="RightsText"/>? (Katalogtitel sind über Prüfsummen belegt.)</summary>
    public static bool NeedsRights(Title t) => t.Id == OwnId;

    /// <summary>Ergebnis eines Laufs. Status: fertig, fehler, abgebrochen, blockiert.</summary>
    public sealed record Outcome(string Status, string Message, string? Program, string? ProgramSha256, string LogPath, string? ResultPath);

    /// <summary>Wie oft das Protokoll während eines Schritts gesichert wird.</summary>
    public static TimeSpan LogFlushEvery { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Führt den ganzen Ablauf aus. Jeder Schritt landet mit Befehl, Ausgabe und Exitcode in protokoll.txt (alle paar Sekunden gesichert);
    /// das Ergebnis in ergebnis.json.</summary>
    public static async Task<Outcome> RunAsync(Job job, Probe probe, Runner runner, Action<int, int, string>? progress, Action<string>? onLine, CancellationToken ct)
    {
        Directory.CreateDirectory(job.WorkDir);
        var log = Path.Combine(job.WorkDir, "protokoll.txt");
        var started = DateTime.UtcNow;
        var lines = new List<string>();
        var flushed = Stopwatch.StartNew();
        var flushLock = new object();
        void Flush()
        {
            lock (flushLock)
            {
                string text; lock (lines) text = string.Join("\n", lines) + "\n";
                try { AtomicFile.WriteAllText(log, text); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                flushed.Restart();
            }
        }
        void Log(string l) { lock (lines) lines.Add(l); onLine?.Invoke(l); if (flushed.Elapsed >= LogFlushEvery) Flush(); }
        Outcome Done(string status, string msg, string? exe = null, string? sha = null, string? result = null) { Log($"== {status.ToUpperInvariant()}: {msg}"); Flush(); return new(status, msg, exe, sha, log, result); }

        Log($"Trendo OS · Freies Spiel portieren · {job.Title.Name} · Beginn {started:yyyy-MM-dd HH:mm:ss} UTC");
        var checks = Prerequisites(job.Title, job.License, job.Elf, job.RecompDir, probe);
        foreach (var c in checks) Log($"[prüfung] {(c.Ok ? "OK " : "FEHLT")} {c.Label}: {c.Detail}");
        if (CheckStubs(job.Stubs) is { } bad) return Done("blockiert", bad);
        if (checks.FirstOrDefault(c => !c.Ok) is { } miss) return Done("blockiert", miss.Label + ": " + miss.Detail);
        if (NeedsRights(job.Title) && !job.RightsConfirmed) return Done("blockiert", $"Bestätige zuerst „{RightsText}“. Ohne diese Bestätigung portiert Trendo kein eigenes Programm.");
        if (NeedsRights(job.Title)) Log($"[prüfung] OK  Bestätigt: „{RightsText}“");
        var cmake = probe.Find("cmake")!;
        var steps = Plan(job.RecompDir);
        var build = Path.Combine(job.RecompDir, "build");
        var outDir = Path.Combine(job.WorkDir, "ausgabe");
        var cfg = Path.Combine(job.WorkDir, "config.toml");
        var stepLog = new JsonArray();
        IReadOnlyList<string> placed = [];
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
                        placed = Distribute(outDir, job.RecompDir, job.WorkDir);
                        Log($"  {placed.Count(p => p.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase))} .cpp nach ps2xRuntime/src/runner, {placed.Count(p => !p.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase))} .h nach ps2xRuntime/include; alte Dateien früherer Läufe und Zwischendateien von {RunnerTarget} entfernt");
                        break;
                    case Kind.Result:
                        var prog = FindBuilt(build, RunnerTarget);
                        if (prog is null) return Done("fehler", "Das Windows-Programm (" + RunnerTarget + ".exe) wurde nicht gefunden.");
                        var sha = Sha256(prog);
                        var result = Path.Combine(job.WorkDir, "ergebnis.json");
                        stepLog.Add(new JsonObject { ["schritt"] = s.Id, ["exit"] = 0 });
                        var json = new JsonObject
                        {
                            ["titel"] = job.Title.Name, ["quelle"] = job.Title.Source, ["lizenz"] = CheckLicense(job.License, job.Title, job.Elf).Text,
                            ["elf"] = Path.GetFileName(job.Elf), ["elf_pfad"] = Path.GetFullPath(job.Elf), ["elf_sha256"] = Sha256(job.Elf),
                            ["programm"] = Path.GetFullPath(prog), ["programm_sha256"] = sha, ["programm_bytes"] = new FileInfo(prog).Length,
                            ["einsortiert"] = new JsonArray(placed.Select(p => (JsonNode)p).ToArray()),
                            ["rechte_bestaetigt"] = NeedsRights(job.Title) ? RightsText : null,
                            ["beginn_utc"] = started.ToString("o"), ["ende_utc"] = DateTime.UtcNow.ToString("o"), ["schritte"] = stepLog,
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

    /// <summary>Was der Probelauf starten darf: nur das im Build-Ordner von PS2Recomp gebaute ps2EntryRunner, unverändert seit dem Lauf (SHA-256),
    /// mit der ELF-Datei aus dem Lauf (ebenfalls per SHA-256 geprüft). Error != null: Grund in Klartext.</summary>
    public sealed record TrialTarget(string? Program, string? Elf, string? Error);

    public static TrialTarget ResolveTrial(string resultJson, string recompDir)
    {
        try
        {
            if (!File.Exists(resultJson)) return new(null, null, "Es gibt noch kein Ergebnis.");
            var info = JsonNode.Parse(File.ReadAllText(resultJson)) as JsonObject;
            string? S(string k) => info?[k] is JsonValue v && v.TryGetValue<string>(out var x) ? x : null;
            var build = Path.GetFullPath(Path.Combine(recompDir, "build")).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            var prog = FindBuilt(build, RunnerTarget);
            if (prog is null) return new(null, null, "Im Build-Ordner von PS2Recomp liegt kein " + RunnerTarget + ".exe. Starte das Portieren neu.");
            prog = Path.GetFullPath(prog);
            var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!prog.StartsWith(build, cmp) || !string.Equals(S("programm"), prog, cmp)) return new(null, null, "Die Ergebnisdatei passt nicht zum gebauten Programm. Starte das Portieren neu.");
            if (S("programm_sha256") != Sha256(prog)) return new(null, null, "Das Programm wurde nach dem Bauen verändert (Prüfsumme weicht ab). Starte das Portieren neu.");
            var elf = S("elf_pfad");
            if (elf is null || !File.Exists(elf) || S("elf_sha256") != Sha256(elf)) return new(null, null, "Die ELF-Datei aus dem Lauf fehlt oder wurde verändert.");
            return new(prog, elf, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return new(null, null, "Die Ergebnisdatei ist beschädigt: " + FriendlyError.Say(e));
        }
    }

    /// <summary>Probelauf des fertigen Programms. Status: läuft (bis zum Zeitlimit mit [run:tick], danach beendet), unklar (keine Meldung),
    /// hängt, beendet, absturz, abgebrochen.</summary>
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
            -2 when ticks == 0 => ("unklar", $"Das Programm lief {seconds} s, meldete aber kein [run:tick]. Ob das Spiel läuft, ist offen."),
            -2 => ("läuft", $"Das Programm lief {seconds} s ohne Absturz ({ticks} Meldungen [run:tick]) und wurde danach beendet."),
            0 => ("beendet", "Das Programm hat sich selbst ordentlich beendet."),
            _ => ("absturz", $"Das Programm endete mit Exitcode {r.Exit}."),
        };
        AtomicFile.WriteAllText(log, $"Probelauf {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC · {Path.GetFileName(program)} {Path.GetFileName(elf)} · {seconds} s\n" + string.Join("\n", all) + (r.Err.Length > 0 ? "\n[stderr]\n" + r.Err : "") + $"\n== {status}: {msg}\n");
        return new(status, msg, r.Exit, ticks, log);
    }
}
