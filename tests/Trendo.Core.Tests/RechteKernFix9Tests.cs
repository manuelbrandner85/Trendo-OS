using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 9. Prüfrunde: keine System-Dialoge außerhalb des Helfers (M), mpv nur über MpvSafe – auch über LaunchAs/Process.Start (N1),
/// „--“ vor Adressen und Dateien (N2), „--factory-startup“ bei jedem Blender-Skriptlauf (N3), erweiterte sichere Tasten (N4), gesperrte Tastendatei (N5).
/// Startet nichts echt.</summary>
public class RechteKernFix9
{
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }
    static IEnumerable<(string File, string Line, int No)> Lines() =>
        Directory.EnumerateFiles(Repo("src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, l, i + 1)))
            .Where(x => !x.Item2.TrimStart().StartsWith("//", StringComparison.Ordinal));

    [Fact(DisplayName = "T-0764 M: kein System-Dialog und kein System-Launcher außerhalb von MainWindow.KidPicker.cs – über welche Variable auch immer")]
    public void Dialoge_nur_im_Helfer()
    {
        var rx = new Regex(@"\.\s*(OpenFilePickerAsync|SaveFilePickerAsync|OpenFolderPickerAsync|TryGetFolderFromPathAsync|TryGetFileFromPathAsync|LaunchFileAsync|LaunchFileInfoAsync|LaunchDirectoryInfoAsync|LaunchUriAsync)\s*\(|\bLauncher\s*\.\s*Launch");
        var bad = Lines().Where(x => rx.IsMatch(x.Line) && !x.File.EndsWith("MainWindow.KidPicker.cs", StringComparison.Ordinal)).Select(x => $"{Path.GetFileName(x.File)}:{x.No}").ToList();
        Assert.True(bad.Count == 0, string.Join(", ", bad));
    }

    [Fact(DisplayName = "T-0764 N1: mpv nie über LaunchAs oder ein eigenes Process.Start mit einem mpv-Pfad; jede Startstelle sperrt die Tastendatei bis zum Ende (HoldUntilExit)")]
    public void Mpv_Startstellen()
    {
        var bad = new List<string>();
        foreach (var (file, line, no) in Lines())
        {
            if (Regex.IsMatch(line, @"\bLaunch(As|ArgsAs)?\(\s*(player|MpvExe|MpvPath|mpvPath|mpv)\b")) bad.Add($"{Path.GetFileName(file)}:{no} LaunchAs mit mpv");
            if (Regex.IsMatch(line, @"ProcessStartInfo\(\s*(MpvPath|mpvPath|mpv|MpvExe)\b") && !File.ReadAllText(file).Contains("MpvSafe.HoldUntilExit", StringComparison.Ordinal)) bad.Add($"{Path.GetFileName(file)}:{no} ohne HoldUntilExit");
        }
        Assert.True(bad.Count == 0, string.Join(", ", bad));
    }

    [Fact(DisplayName = "T-0764 N2: eigenes mpv-/VLC-Fenster – Datei immer hinter „--“ (nie als Option lesbar)")]
    public void Player_mit_Trenner()
    {
        var (_, m) = MediaLibrary.Player("-–x.mp3", "mpv", null)!.Value;
        Assert.Equal(["--", "-–x.mp3"], m[^2..]);
        var (_, v) = MediaLibrary.Player("a.mp3", null, "vlc")!.Value;
        Assert.Equal(["--", "a.mp3"], v[^2..]);
    }

    [Fact(DisplayName = "T-0764 N3: jeder Blender-Lauf mit Trendo-Skript (--python-expr) startet mit --factory-startup (Quelltext durchsucht)")]
    public void Blender_factory_startup()
    {
        var bad = new List<string>(); var exceptions = new List<string>(); var checkedSites = 0;
        foreach (var f in Directory.EnumerateFiles(Repo("src"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            if (f.EndsWith("BlenderScript.cs", StringComparison.Ordinal) || f.EndsWith("ExternalApps.cs", StringComparison.Ordinal)) continue; // Hilfsfunktion selbst; Erwachsenen-Oberfläche (eigene Einstellungen, siehe Bericht)
            var lines = File.ReadAllLines(f);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(lines[i], @"""--python-expr""|BlenderScript\.Args\(") || lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                checkedSites++;
                var near = string.Join("\n", lines[Math.Max(0, i - 3)..Math.Min(lines.Length, i + 2)]);
                // T-0785: genau EINE begründete Ausnahme – der sichtbare Start des Blender der Person mit MCP-Brücke (BlenderLink.StartLockedAsync). Mit
                // --factory-startup fehlten dort ihr MCP-Add-on, ihre Add-ons und Einstellungen; das Skript ist trotzdem per SHA-256 geprüft (BlenderScript).
                // Gilt nur in BlenderLink.cs, nur mit Markierung UND nur an der Zeile, die das Brücken-Skript („bruecke-…“) startet.
                if (f.EndsWith($"{Path.DirectorySeparatorChar}BlenderLink.cs", StringComparison.Ordinal) && near.Contains("T-0785-AUSNAHME-N3", StringComparison.Ordinal) && lines[i].Contains("bruecke-", StringComparison.Ordinal))
                { exceptions.Add($"{Path.GetFileName(f)}:{i + 1}"); continue; }
                if (!near.Contains("--factory-startup", StringComparison.Ordinal)) bad.Add($"{Path.GetFileName(f)}:{i + 1}");
            }
        }
        Assert.True(bad.Count == 0, string.Join(", ", bad));
        Assert.Single(exceptions); // Gegenprobe: genau diese eine Stelle ist ausgenommen …
        Assert.True(checkedSites - exceptions.Count >= 3, $"nur {checkedSites} Stellen gefunden"); // … alle übrigen Blender-Skriptstarts werden weiter geprüft
    }

    [Fact(DisplayName = "T-0785: Gegenprobe zur N3-Ausnahme – die Markierung allein befreit keine andere Stelle")]
    public void Blender_factory_startup_exception_is_narrow()
    {
        // dieselbe Regel wie oben auf nachgebildete Zeilen: Markierung in einer anderen Datei bzw. an einer anderen Zeile zählt nicht
        static bool Exempt(string file, string near, string line) => file.EndsWith($"{Path.DirectorySeparatorChar}BlenderLink.cs", StringComparison.Ordinal) && near.Contains("T-0785-AUSNAHME-N3", StringComparison.Ordinal) && line.Contains("bruecke-", StringComparison.Ordinal);
        var sep = Path.DirectorySeparatorChar;
        Assert.True(Exempt($"src{sep}BlenderLink.cs", "// T-0785-AUSNAHME-N3", "args.AddRange(BlenderScript.Args(Path.Combine(StateDir, $\"bruecke-{run}.py\"), x));"));
        Assert.False(Exempt($"src{sep}Kreatur.cs", "// T-0785-AUSNAHME-N3", "BlenderScript.Args(\"bruecke-x.py\", s)")); // andere Datei
        Assert.False(Exempt($"src{sep}BlenderLink.cs", "// T-0785-AUSNAHME-N3", "BlenderScript.Args(\"render.py\", s)")); // andere Zeile in BlenderLink.cs
        Assert.False(Exempt($"src{sep}BlenderLink.cs", "// ohne Markierung", "BlenderScript.Args(\"bruecke-x.py\", s)"));
    }

    [Fact(DisplayName = "T-0764 N4: sichere Tasten erweitert (Untertitel, Tonspur, Tempo, Bildschirmfoto, Kapitel, 60 s, Einzelbild); weiter ohne run, subprocess, script-*, Pfad-Optionen, Ketten")]
    public void Tasten_erweitert()
    {
        foreach (var c in new[] { "cycle sub", "cycle audio", "add speed 0.1", "add speed -0.1", "set speed 1", "screenshot", "add chapter 1", "add chapter -1", "seek 60", "seek -60", "frame-step", "frame-back-step" })
            Assert.Contains(c, MpvSafe.SafeKeys);
        Assert.True(MpvSafe.IsSafeInputConf(MpvSafe.SafeKeys));
        foreach (var bad in new[] { "x set screenshot-directory C:/x", "y screenshot-to-file C:/x.png", "z set speed 1; run calc", "w script-message x", "k add speed 5", "u set input-conf x", "t cycle-values sub \"a\"" })
            Assert.False(MpvSafe.IsSafeInputConf(bad), bad);
    }

    [Fact(DisplayName = "T-0764 N5: Tastendatei gesperrt (nur lesen) bis zur Freigabe – Ändern und Löschen schlagen fehl; danach weg")]
    public void Tastendatei_gesperrt()
    {
        var args = MpvSafe.Args(keys: true);
        var conf = args.Single(a => a.StartsWith("--input-conf=", StringComparison.Ordinal))["--input-conf=".Length..];
        Assert.Equal(MpvSafe.SafeKeys, File.ReadAllText(conf)); // lesen geht (mpv)
        Assert.ThrowsAny<IOException>(() => File.WriteAllText(conf, "x run calc"));
        Assert.ThrowsAny<IOException>(() => File.Delete(conf));
        Assert.Equal(MpvSafe.SafeKeys, File.ReadAllText(conf));
        MpvSafe.Release(args);
        Assert.False(File.Exists(conf));
        Assert.NotEqual(conf, MpvSafe.Args().Single(a => a.StartsWith("--input-conf=", StringComparison.Ordinal))["--input-conf=".Length..]); // je Start eine eigene Datei
    }

    [Theory(DisplayName = "T-0764 N2: Adressen und Dateien für mpv – nur http, https, rtmp(s), rtsp oder eine vorhandene eigene Datei")]
    [InlineData("https://example.org/a.m3u8", true)] [InlineData("http://x/y", true)] [InlineData("rtmp://x/live", true)] [InlineData("rtmps://x/live", true)] [InlineData("rtsp://cam/1", true)]
    [InlineData("file:///C:/Windows/win.ini", false)] [InlineData("memory://run", false)] [InlineData("av://lavfi:sine", false)] [InlineData("lavfi:sine", false)]
    [InlineData("--script=C:/x.lua", false)] [InlineData("ftp://x/y", false)] [InlineData("", false)] [InlineData("relativ.mp4", false)]
    public void Ziele(string target, bool ok) => Assert.Equal(ok, MpvSafe.TargetAllowed(target));
}
