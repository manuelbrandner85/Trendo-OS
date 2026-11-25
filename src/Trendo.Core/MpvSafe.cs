using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Trendo.Core;

/// <summary>T-0764 (8./9. Prüfrunde): Jeder mpv-Start von Trendo – für alle Profile – lädt nichts aus %APPDATA%\mpv bzw. „portable_config“: keine
/// Einstellungen (mpv.conf, input.conf), keine Skripte (Lua/JavaScript, auch nicht aus dem Benutzerordner) und keine Standard-Tastenbelegung (darüber
/// wäre die eingebaute Konsole mit „run“/„subprocess“ erreichbar). Die Tastenbelegung kommt aus einer Trendo-Datei, die für jeden Start neu (mit eigenem
/// Namen) geschrieben und bis zum Ende des Players gesperrt wird (FileShare.Read: niemand kann sie ändern oder löschen): leer, oder – wo eine Tastatur
/// gebraucht wird (eigenes Player-Fenster) – nur sichere Befehle (<see cref="SafeKeys"/>). Nie: run, subprocess, load-script, script-*, keybind,
/// change-list, load-config-file, set für Optionen mit Dateipfaden, Befehlsketten.
/// „--input-conf=memory://…“ nutzt Trendo nicht: ob die mitgelieferte mpv-Fassung (shinchiro-Build 20261021) das für input.conf sicher kann, lässt
/// sich ohne einen echten mpv-Lauf nicht belegen.</summary>
public static class MpvSafe
{
    /// <summary>Feste, sichere Tastenbelegung für ein eigenes Player-Fenster.</summary>
    public const string SafeKeys = """
SPACE cycle pause
p cycle pause
LEFT seek -5
RIGHT seek 5
Shift+LEFT seek -60
Shift+RIGHT seek 60
UP add volume 5
DOWN add volume -5
m cycle mute
f cycle fullscreen
ESC set fullscreen no
v cycle sub
a cycle audio
[ add speed -0.1
] add speed 0.1
BS set speed 1
s screenshot
PGUP add chapter 1
PGDWN add chapter -1
. frame-step
, frame-back-step
q quit
""";

    /// <summary>Erlaubte Befehle einer Trendo-Tastenbelegung (Befehl + Ziel), genau diese – sonst nichts.</summary>
    static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "cycle pause", "cycle mute", "cycle fullscreen", "seek -5", "seek 5", "seek -60", "seek 60", "add volume 5", "add volume -5", "set fullscreen no", "quit",
        "cycle sub", "cycle audio", "add speed 0.1", "add speed -0.1", "set speed 1", "screenshot", "add chapter 1", "add chapter -1", "frame-step", "frame-back-step",
    };

    /// <summary>Gesperrte Tastendateien je Pfad (bis der Player endet).</summary>
    static readonly ConcurrentDictionary<string, FileStream> Locks = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, byte> Held = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Die festen Argumente für jeden mpv-Start. <paramref name="keys"/>: sichere Tastenbelegung statt leerer Datei. Die Tastendatei bleibt
    /// gesperrt, bis <see cref="HoldUntilExit"/> (nach dem Start) das Ende des Players meldet oder <see cref="Release"/> gerufen wird; ohne Start
    /// spätestens nach 10 Minuten.</summary>
    public static List<string> Args(bool keys = false, string? confDir = null, string? screenshotDir = null)
    {
        var dir = confDir ?? OwnDir;
        Directory.CreateDirectory(dir);
        var conf = Path.Combine(dir, (keys ? "trendo-tasten-" : "trendo-leer-") + Guid.NewGuid().ToString("N") + ".conf");
        var bytes = new UTF8Encoding(false).GetBytes(keys ? SafeKeys : "");
        using (var w = new FileStream(conf, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { w.Write(bytes); w.Flush(true); }
        // Sperre: eigener Lese-Zugriff, andere dürfen nur lesen (mpv) – Ändern, Umbenennen und Löschen schlagen fehl. Danach den Inhalt über genau
        // diesen gesperrten Zugriff prüfen: wurde die Datei zwischen Schreiben und Sperren ausgetauscht, startet nichts.
        var fs = new FileStream(conf, FileMode.Open, FileAccess.Read, FileShare.Read);
        var now = new byte[bytes.Length + 1]; var n = 0; int r; while ((r = fs.Read(now, n, now.Length - n)) > 0) n += r;
        if (n != bytes.Length || !now.AsSpan(0, n).SequenceEqual(bytes)) { fs.Dispose(); throw new IOException("Die Tastendatei für den Player wurde verändert – es wird nichts gestartet."); }
        Locks[conf] = fs;
        _ = Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(_ => { if (!Held.ContainsKey(conf)) ReleaseConf(conf); }, TaskScheduler.Default);
        // 10. Prüfrunde: „--ytdl=no“ – der eingebaute yt-dlp-Hook läuft sonst trotz „--load-scripts=no“ und startet yt-dlp.exe (mpv-Ordner, Arbeitsordner)
        List<string> a = ["--no-config", "--load-scripts=no", "--ytdl=no", "--input-default-bindings=no", "--input-conf=" + conf];
        if (screenshotDir is not null) a.Add("--screenshot-directory=" + screenshotDir);
        return a;
    }

    /// <summary>Trendos eigener mpv-Ordner dieses Laufs (%TEMP%	rendo-mpv-&lt;PID&gt;) – geschützt (<see cref="Sys.ProtectedFolders"/>).</summary>
    public static string OwnDir => Path.Combine(Path.GetTempPath(), "trendo-mpv-" + Environment.ProcessId);

    /// <summary>T-0764 (10. Prüfrunde): leerer, Trendo-eigener Arbeitsordner für jeden mpv-/VLC-Start – dort liegt nichts, was der Player nachladen
    /// könnte (z. B. yt-dlp.exe). Vorhandener Inhalt wird vor jedem Start entfernt.</summary>
    public static string WorkDir()
    {
        var d = Path.Combine(OwnDir, "leer");
        Directory.CreateDirectory(d);
        foreach (var f in Directory.EnumerateFileSystemEntries(d))
            try { if (Directory.Exists(f)) Directory.Delete(f, true); else File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (Directory.EnumerateFileSystemEntries(d).Any()) throw new IOException("Der Arbeitsordner des Players ist nicht leer – es wird nichts gestartet.");
        return d;
    }

    /// <summary>T-0764 (10. Prüfrunde): Ordner „trendo-mpv-&lt;PID&gt;“ früherer Trendo-Läufe aufräumen – nur, wenn der Name genau passt, kein Prozess mit
    /// dieser PID mehr läuft und der Ordner nur Trendos eigene Dateien enthält (trendo-leer-/trendo-tasten-*.conf, Ordner „leer“). Nie fremde Ordner.</summary>
    public static int CleanupStale()
    {
        var n = 0;
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(Path.GetTempPath(), "trendo-mpv-*").ToList(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
        foreach (var d in dirs)
        {
            var name = Path.GetFileName(d);
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^trendo-mpv-[0-9]{1,10}$") || !int.TryParse(name["trendo-mpv-".Length..], out var pid) || pid == Environment.ProcessId) continue;
            if (Running(pid)) continue;
            try
            {
                var own = Directory.EnumerateFileSystemEntries(d).All(e => Directory.Exists(e) ? Path.GetFileName(e) == "leer" : System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(e), @"^trendo-(leer|tasten)(-[0-9a-f]{32})?\.conf$"));
                if (!own) continue;
                Directory.Delete(d, true); n++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return n;
    }

    static bool Running(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; } // kein Prozess mit dieser PID
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return true; } // unklar: nicht anfassen
    }

    static string? ConfOf(IEnumerable<string> args) => args.FirstOrDefault(a => a.StartsWith("--input-conf=", StringComparison.Ordinal))?["--input-conf=".Length..];

    /// <summary>Sperre halten, bis der gestartete Player endet (danach Datei freigeben und löschen).</summary>
    public static void HoldUntilExit(IEnumerable<string> args, Process? p)
    {
        if (ConfOf(args) is not { } conf) return;
        if (p is null) { ReleaseConf(conf); return; }
        Held[conf] = 0;
        try
        {
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) => ReleaseConf(conf);
            if (p.HasExited) ReleaseConf(conf);
        }
        catch (InvalidOperationException) { ReleaseConf(conf); }
    }

    /// <summary>Sperre sofort freigeben (Start fehlgeschlagen, Tests).</summary>
    public static void Release(IEnumerable<string> args) { if (ConfOf(args) is { } c) ReleaseConf(c); }

    /// <summary>Ist die Tastendatei gesperrt (Tests)?</summary>
    public static bool IsLocked(IEnumerable<string> args) => ConfOf(args) is { } c && Locks.ContainsKey(c);

    static void ReleaseConf(string conf)
    {
        Held.TryRemove(conf, out _);
        if (Locks.TryRemove(conf, out var fs)) fs.Dispose();
        try { File.Delete(conf); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Ist eine Tastenbelegung sicher? Jede Zeile: Taste + genau ein erlaubter Befehl (keine Befehlsketten mit „;“, keine Präfixe, keine Anführungszeichen).</summary>
    public static bool IsSafeInputConf(string text) =>
        text.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))
            .All(l => l.IndexOfAny([';', '$', '`', '"', '\'']) < 0 && l.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) is [_, var cmd] && Allowed.Contains(string.Join(' ', cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries))));

    /// <summary>T-0764 (9. Prüfrunde): Ziel, das Trendo an mpv übergibt (Kommandozeile hinter „--“ oder per „loadfile“): nur http, https, rtmp, rtmps,
    /// rtsp oder eine vorhandene lokale Datei mit vollem Pfad (eigene Aufnahmen, Mediathek). Keine anderen Schemata (file:, memory:, av:, lavfi: …),
    /// nichts, was mit „-“ beginnt, keine Steuerzeichen.</summary>
    public static bool TargetAllowed(string? target)
    {
        var t = (target ?? "").Trim();
        if (t.Length == 0 || t.StartsWith('-') || t.Any(char.IsControl)) return false;
        if (t.Contains("://", StringComparison.Ordinal))
            return Uri.TryCreate(t, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "rtmp" or "rtmps" or "rtsp" or "udp" or "rtp"; // udp/rtp: IPTV-Multicast (10. Prüfrunde)
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[A-Za-z][A-Za-z0-9+.\-]+:")) return false; // anderes Schema (auch memory:, av:)
        return Path.IsPathFullyQualified(t) && File.Exists(t);
    }
}
