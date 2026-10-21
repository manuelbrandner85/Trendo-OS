using System.Diagnostics;

namespace Trendo.App;

/// <summary>Startzeit-Messung (SYS-13-P15): Zeitpunkte relativ zum Prozessstart, auf Wunsch als Datei (--startup-log).</summary>
public static class StartupTrace
{
    static readonly Stopwatch Sw = Stopwatch.StartNew();
    static readonly double Offset = Math.Max(0, (DateTime.Now - SafeStart()).TotalMilliseconds - 0);
    static readonly List<(string, double)> Marks = [];
    public static string? LogPath { get; set; }
    static DateTime SafeStart() { try { return Process.GetCurrentProcess().StartTime; } catch { return DateTime.Now; } }

    /// <summary>Millisekunden seit Prozessstart.</summary>
    public static double Now => Offset + Sw.Elapsed.TotalMilliseconds;
    public static void Mark(string name) { lock (Marks) Marks.Add((name, Now)); }
    /// <summary>Dauer einer Teilaufgabe (z. B. Seitenaufbau) festhalten.</summary>
    public static void Took(string name, double ms) { lock (Marks) Marks.Add((name, ms)); }
    public static IReadOnlyList<(string Name, double Ms)> All { get { lock (Marks) return [.. Marks]; } }
    public static double? Of(string name) { lock (Marks) return Marks.LastOrDefault(m => m.Item1 == name) is var m && m.Item1 is not null ? m.Item2 : null; }
    public static void Flush()
    {
        if (LogPath is null) return;
        var lines = All.Select(m => FormattableString.Invariant($"{m.Name,-22} {m.Ms,8:0} ms"));
        try { File.WriteAllLines(LogPath, lines); } catch (IOException) { }
    }
}
