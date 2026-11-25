using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Trendo.Core.Sys;

/// <summary>Leistung als Task-Manager (Masterprompt 131, Runde 7): Prozessliste mit Prozessorlast je Programm (aus zwei Messungen),
/// Speicher, sortierbar und durchsuchbar; Verlauf der letzten 60 Messungen für die Kurven. Beenden nur für Programme aus Trendos eigenen Ordnern
/// (Werkzeugordner, Datenordner) – fremde Programme beendet Trendo nie.</summary>
public static class TaskManager
{
    public sealed record Proc(int Pid, string Name, double CpuPercent, long Bytes, string? Path, bool Own, DateTime? Start = null);
    public readonly record struct Sample(int Pid, string Name, TimeSpan Cpu, long Bytes, string? Path, DateTime? Start = null);

    /// <summary>T-0763 Fix N5: Läuft unter dieser Kennung noch genau das Programm aus der Zeile? Gleicher Pfad, eigen, gleiche Startzeit (Kennungen werden
    /// von Windows wiederverwendet). Ohne bekannte Startzeit in der Zeile zählt nur der Pfad.</summary>
    public static bool SameProcess(Proc row, string? path, DateTime? start, IReadOnlyList<string> ownRoots) =>
        row.Own && IsOwn(path, ownRoots) && string.Equals(path, row.Path, StringComparison.OrdinalIgnoreCase) && (row.Start is null || row.Start == start);

    /// <summary>Prozessorlast je Prozess aus zwei Messungen: verbrauchte Prozessorzeit / (vergangene Zeit × Kerne). Neue Prozesse zählen mit 0 %.</summary>
    public static IReadOnlyList<Proc> Rows(IReadOnlyList<Sample> before, IReadOnlyList<Sample> now, double seconds, int cores, IReadOnlyList<string> ownRoots)
    {
        var prev = before.GroupBy(s => s.Pid).ToDictionary(g => g.Key, g => g.First());
        var span = Math.Max(0.001, seconds) * Math.Max(1, cores);
        return now.Select(s =>
        {
            var cpu = prev.TryGetValue(s.Pid, out var p) && p.Name == s.Name ? Math.Clamp((s.Cpu - p.Cpu).TotalSeconds / span * 100, 0, 100) : 0;
            return new Proc(s.Pid, s.Name, Math.Round(cpu, 1), s.Bytes, s.Path, IsOwn(s.Path, ownRoots), s.Start);
        }).ToList();
    }

    /// <summary>Gehört das Programm zu Trendo? Nur wenn die Programmdatei in einem der eigenen Ordner liegt (Groß-/Kleinschreibung egal, nur ganze Ordner).</summary>
    public static bool IsOwn(string? path, IReadOnlyList<string> ownRoots)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string Full(string p) { try { return System.IO.Path.GetFullPath(p); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return ""; } }
        var f = Full(path);
        // T-0763 Fix N5: Liegt auf dem Weg eine Verknüpfung (z. B. Junction aus dem Werkzeugordner auf ein fremdes Programm), ist es nicht „eigen“
        return f.Length > 0 && ownRoots.Where(r => r.Length > 0).Select(r => Full(r).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar)
            .Any(r => r.Length > 1 && f.StartsWith(r, StringComparison.OrdinalIgnoreCase) && PathGuard.LinkOnWay(r, f) is null);
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
                    DateTime? start = null; try { start = p.StartTime; } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
                    list.Add(new Sample(p.Id, p.ProcessName, p.TotalProcessorTime, p.WorkingSet64, path, start));
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

/// <summary>T-0763 (X-09): Leistungsindikatoren mit Platzhalter-Instanz (z. B. „\GPU Engine(*)\Utilization Percentage“) über pdh.dll – Windows-Bordmittel,
/// ohne Administratorrechte. Erste Abfrage = Bezug; ab der zweiten liefert <see cref="Read"/> je Instanz einen Wert. Außerhalb von Windows: nichts.</summary>
public sealed class PdhArray : IDisposable
{
    readonly object _lock = new();
    IntPtr _q, _c, _c2; bool _primed;
    public bool Available { get; }
    /// <param name="secondPath">Optional ein zweiter Zähler derselben Instanzen (z. B. „\Process(*)\ID Process“) – beide aus derselben Abfrage, siehe <see cref="ReadPair"/>.</param>
    public PdhArray(string englishPath, string? secondPath = null)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            if (PdhOpenQueryW(null, IntPtr.Zero, out _q) != 0) { _q = IntPtr.Zero; return; }
            if (PdhAddEnglishCounterW(_q, englishPath, IntPtr.Zero, out _c) != 0) { PdhCloseQuery(_q); _q = IntPtr.Zero; return; }
            if (secondPath is not null && PdhAddEnglishCounterW(_q, secondPath, IntPtr.Zero, out _c2) != 0) { PdhCloseQuery(_q); _q = IntPtr.Zero; return; }
            Available = true;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { _q = IntPtr.Zero; }
    }

    /// <summary>Neue Messung; null beim ersten Aufruf (Bezug) oder wenn Windows den Zähler nicht liefert.</summary>
    public IReadOnlyList<(string Instance, double Value)>? Read()
    {
        lock (_lock)
        {
            if (!Available || _q == IntPtr.Zero) return null;
            if (PdhCollectQueryData(_q) != 0) return null;
            if (!_primed) { _primed = true; return null; }
            uint size = 0;
            var st = PdhGetFormattedCounterArrayW(_c, Fmt, ref size, out _, IntPtr.Zero);
            if (st != MoreData || size == 0) return st == 0 ? [] : null;
            var buf = Marshal.AllocHGlobal((int)size);
            try
            {
                if (PdhGetFormattedCounterArrayW(_c, Fmt, ref size, out var count, buf) != 0) return null;
                var item = Marshal.SizeOf<Item>(); var res = new List<(string, double)>((int)count);
                for (var i = 0; i < count; i++)
                {
                    var it = Marshal.PtrToStructure<Item>(buf + i * item);
                    if (it.Status == 0 && double.IsFinite(it.Value)) res.Add((Marshal.PtrToStringUni(it.Name) ?? "", it.Value));
                }
                return res;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }

    /// <summary>T-0763: beide Zähler aus <b>einer</b> Messung, je Instanz in derselben Reihenfolge – so lassen sich gleichnamige Instanzen
    /// (mehrere „powershell“, die PDH ohne Nummer liefert) sicher über den zweiten Zähler (z. B. die Prozesskennung) zuordnen.</summary>
    public IReadOnlyList<(string Instance, double Value, double Second)>? ReadPair()
    {
        lock (_lock)
        {
            if (!Available || _q == IntPtr.Zero || _c2 == IntPtr.Zero) return null;
            if (PdhCollectQueryData(_q) != 0) return null;
            if (!_primed) { _primed = true; return null; }
            var a = Raw(_c); var b = Raw(_c2);
            if (a is null || b is null || a.Count != b.Count) return null;
            var res = new List<(string, double, double)>(a.Count);
            for (var i = 0; i < a.Count; i++)
                if (a[i].Name == b[i].Name && a[i].Status == 0 && b[i].Status == 0 && double.IsFinite(a[i].Value) && double.IsFinite(b[i].Value)) res.Add((a[i].Name, a[i].Value, b[i].Value));
            return res;
        }
    }

    List<(string Name, uint Status, double Value)>? Raw(IntPtr c)
    {
        uint size = 0;
        var st = PdhGetFormattedCounterArrayW(c, Fmt, ref size, out _, IntPtr.Zero);
        if (st != MoreData || size == 0) return st == 0 ? [] : null;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(c, Fmt, ref size, out var count, buf) != 0) return null;
            var item = Marshal.SizeOf<Item>(); var res = new List<(string, uint, double)>((int)count);
            for (var i = 0; i < count; i++) { var it = Marshal.PtrToStructure<Item>(buf + i * item); res.Add((Marshal.PtrToStringUni(it.Name) ?? "", it.Status, it.Value)); }
            return res;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public void Dispose() { lock (_lock) { if (_q != IntPtr.Zero) { PdhCloseQuery(_q); _q = IntPtr.Zero; } } }

    const uint Fmt = 0x00000200 /* PDH_FMT_DOUBLE */ | 0x00008000 /* PDH_FMT_NOCAP100 */, MoreData = 0x800007D2;
    [StructLayout(LayoutKind.Sequential)] struct Item { public IntPtr Name; public uint Status; public uint Pad; public double Value; }
#pragma warning disable SYSLIB1054 // klassisches DllImport: Trendo.Core erlaubt keinen unsafe-Code
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhOpenQueryW(string? source, IntPtr user, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr user, out IntPtr counter);
    [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint size, out uint count, IntPtr buffer);
    [DllImport("pdh.dll")] static extern uint PdhCloseQuery(IntPtr query);
#pragma warning restore SYSLIB1054
}

/// <summary>T-0763 (X-09): Grafiklast je Programm wie im Windows-Task-Manager – Leistungsindikator „GPU Engine“ (Instanz „pid_1234_luid_…_engtype_3D“),
/// je Prozess der höchste Wert über alle Grafik-Einheiten. Netzlast je Programm liefert Windows ohne Administratorrechte nicht (nur je Netzwerkkarte) –
/// Trendo zeigt sie deshalb bewusst nicht an.</summary>
public static class GpuLoad
{
    public const string CounterPath = @"\GPU Engine(*)\Utilization Percentage";
    public const string NetNote = "Netzlast je Programm: Windows liefert sie ohne Administratorrechte nicht – Trendo zeigt sie deshalb nicht an.";

    /// <summary>Instanzen zu „Prozess → höchste Auslastung einer Grafik-Einheit“ (mehrere Instanzen derselben Einheit werden addiert).</summary>
    public static Dictionary<int, double> PerProcess(IEnumerable<(string Instance, double Value)> items)
    {
        var perEngine = new Dictionary<(int Pid, string Engine), double>();
        foreach (var (inst, v) in items)
        {
            var m = System.Text.RegularExpressions.Regex.Match(inst, @"^pid_(\d+)_(luid_.+?_eng_\d+)");
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out var pid)) continue;
            var key = (pid, m.Groups[2].Value);
            perEngine[key] = perEngine.GetValueOrDefault(key) + Math.Max(0, v);
        }
        return perEngine.GroupBy(kv => kv.Key.Pid).ToDictionary(g => g.Key, g => Math.Round(Math.Clamp(g.Max(kv => kv.Value), 0, 100), 1));
    }
}
