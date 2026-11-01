using System.Diagnostics;

namespace Trendo.Core.Sys;

/// <summary>Leistung als Task-Manager (Masterprompt 131, Runde 7): Prozessliste mit Prozessorlast je Programm (aus zwei Messungen),
/// Speicher, sortierbar und durchsuchbar; Verlauf der letzten 60 Messungen für die Kurven. Beenden nur für Programme aus Trendos eigenen Ordnern
/// (Werkzeugordner, Datenordner) – fremde Programme beendet Trendo nie.</summary>
public static class TaskManager
{
    public sealed record Proc(int Pid, string Name, double CpuPercent, long Bytes, string? Path, bool Own);
    public readonly record struct Sample(int Pid, string Name, TimeSpan Cpu, long Bytes, string? Path);

    /// <summary>Prozessorlast je Prozess aus zwei Messungen: verbrauchte Prozessorzeit / (vergangene Zeit × Kerne). Neue Prozesse zählen mit 0 %.</summary>
    public static IReadOnlyList<Proc> Rows(IReadOnlyList<Sample> before, IReadOnlyList<Sample> now, double seconds, int cores, IReadOnlyList<string> ownRoots)
    {
        var prev = before.GroupBy(s => s.Pid).ToDictionary(g => g.Key, g => g.First());
        var span = Math.Max(0.001, seconds) * Math.Max(1, cores);
        return now.Select(s =>
        {
            var cpu = prev.TryGetValue(s.Pid, out var p) && p.Name == s.Name ? Math.Clamp((s.Cpu - p.Cpu).TotalSeconds / span * 100, 0, 100) : 0;
            return new Proc(s.Pid, s.Name, Math.Round(cpu, 1), s.Bytes, s.Path, IsOwn(s.Path, ownRoots));
        }).ToList();
    }

    /// <summary>Gehört das Programm zu Trendo? Nur wenn die Programmdatei in einem der eigenen Ordner liegt (Groß-/Kleinschreibung egal, nur ganze Ordner).</summary>
    public static bool IsOwn(string? path, IReadOnlyList<string> ownRoots)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string Full(string p) { try { return System.IO.Path.GetFullPath(p); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return ""; } }
        var f = Full(path);
        return f.Length > 0 && ownRoots.Where(r => r.Length > 0).Select(r => Full(r).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar)
            .Any(r => r.Length > 1 && f.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Sortieren nach „name“, „cpu“, „ram“ oder „pid“; Suche in Name und Kennung.</summary>
    public static IReadOnlyList<Proc> Sort(IEnumerable<Proc> rows, string by, bool descending, string? search = null)
    {
        var q = search?.Trim() ?? "";
        var f = rows.Where(r => q.Length == 0 || r.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || r.Pid.ToString() == q);
        IOrderedEnumerable<Proc> o = by switch
        {
            "name" => descending ? f.OrderByDescending(r => r.Name, StringComparer.CurrentCultureIgnoreCase) : f.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            "pid" => descending ? f.OrderByDescending(r => r.Pid) : f.OrderBy(r => r.Pid),
            "ram" => descending ? f.OrderByDescending(r => r.Bytes) : f.OrderBy(r => r.Bytes),
            _ => descending ? f.OrderByDescending(r => r.CpuPercent) : f.OrderBy(r => r.CpuPercent),
        };
        return o.ThenBy(r => r.Pid).ToList();
    }

    /// <summary>Alle Prozesse dieses PCs messen (Namen, Prozessorzeit, Arbeitsspeicher, Programmpfad – wenn lesbar).</summary>
    public static IReadOnlyList<Sample> Measure()
    {
        var list = new List<Sample>();
        foreach (var p in Process.GetProcesses())
            using (p)
            {
                try
                {
                    string? path = null; try { path = p.MainModule?.FileName; } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
                    list.Add(new Sample(p.Id, p.ProcessName, p.TotalProcessorTime, p.WorkingSet64, path));
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        return list;
    }
}

/// <summary>Verlauf fester Länge (älteste Werte fallen heraus) – für die Kurven im Task-Manager.</summary>
public sealed class History(int size = 60)
{
    readonly Queue<double> _q = new();
    public int Size { get; } = Math.Max(2, size);
    public void Add(double? v) { _q.Enqueue(v ?? 0); while (_q.Count > Size) _q.Dequeue(); }
    public IReadOnlyList<double> Values => _q.ToList();
    public double Last => _q.Count == 0 ? 0 : _q.Last();
    public double Average => _q.Count == 0 ? 0 : _q.Average();
    public double Max => _q.Count == 0 ? 0 : _q.Max();
}
