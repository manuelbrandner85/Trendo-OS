using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0680: Sperre gegen ein zweites Fenster oder einen zweiten Prozess. Je geöffneter Datei liegt eine kleine Sperrdatei im Datenordner
/// (<c>programme/sperren</c>) mit Prozesskennung, Startzeit des Prozesses, Kennung des Fensters und Zeitpunkt. Läuft der Prozess nicht mehr (Absturz),
/// gilt die Sperre als verwaist und wird übernommen. Die Sperrdateien liegen bewusst nicht neben den Dokumenten: kein Zusatz in Projektordnern.
/// Grenze: schützt Fenster und Prozesse, die denselben Datenordner benutzen – für alles andere fragt die Hülle vor dem Überschreiben nach
/// (<see cref="ProgramDocument.ChangedOnDisk"/>).</summary>
public sealed class ProgramLocks : IDisposable
{
    /// <summary>Wer eine Datei gerade geöffnet hat.</summary>
    public sealed record Holder(int Pid, DateTime Started, string Token, DateTime Since, string Path);

    static readonly HashSet<string> Live = []; // Fenster-Kennungen, die in diesem Prozess noch leben
    static readonly DateTime OwnStart = StartOf(Process.GetCurrentProcess());

    readonly string _dir;
    readonly string _token = Guid.NewGuid().ToString("N");
    readonly HashSet<string> _held = new(StringComparer.OrdinalIgnoreCase);

    public ProgramLocks(string dir) { _dir = dir; lock (Live) Live.Add(_token); }

    static DateTime StartOf(Process p) { try { return p.StartTime.ToUniversalTime(); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { return default; } }

    /// <summary>Sperrdatei zu einer Datei (Name aus dem vollständigen Pfad, Groß-/Kleinschreibung egal).</summary>
    public string FileFor(string path) =>
        System.IO.Path.Combine(_dir, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(path).ToUpperInvariant())))[..32].ToLowerInvariant() + ".lock");

    /// <summary>Datei für dieses Fenster sperren. null = gesperrt (oder war es schon); sonst, wer sie offen hat. Verwaiste Sperren werden übernommen.</summary>
    public Holder? Acquire(string path)
    {
        var f = FileFor(path);
        Directory.CreateDirectory(_dir);
        for (var i = 0; i < 6; i++)
        {
            try
            {
                var json = new JsonObject
                {
                    ["pid"] = Environment.ProcessId, ["start"] = OwnStart.ToString("O"), ["fenster"] = _token,
                    ["seit"] = DateTime.Now.ToString("O"), ["datei"] = System.IO.Path.GetFullPath(path),
                }.ToJsonString();
                using (var fs = new FileStream(f, FileMode.CreateNew, FileAccess.Write, FileShare.None)) // CreateNew: nur einer gewinnt
                    fs.Write(Encoding.UTF8.GetBytes(json));
                _held.Add(f); return null;
            }
            catch (IOException) when (File.Exists(f)) { }
            var h = Read(f);
            if (h is null)
            {
                // unlesbar: entsteht gerade (anderes Fenster schreibt) oder ist der Rest eines Absturzes
                if (Age(f) < TimeSpan.FromSeconds(2)) { Thread.Sleep(40); continue; }
                TryDelete(f); continue;
            }
            if (h.Token == _token) { _held.Add(f); return null; }
            if (Alive(h)) return h;
            TryDelete(f); // verwaist – der Prozess läuft nicht mehr
        }
        return Read(f) ?? new Holder(0, default, "", DateTime.Now, path);
    }

    /// <summary>Hat ein anderes, noch laufendes Fenster die Datei gesperrt? (ohne selbst zu sperren)</summary>
    public Holder? HeldByOther(string path) => Read(FileFor(path)) is { } h && h.Token != _token && Alive(h) ? h : null;

    /// <summary>Sperre freigeben – nur wenn sie diesem Fenster gehört.</summary>
    public void Release(string path)
    {
        var f = FileFor(path);
        _held.Remove(f);
        if (Read(f)?.Token == _token) TryDelete(f);
    }

    /// <summary>Wie ein Absturz dieses Fensters: Die Sperrdateien bleiben liegen, die Kennung gilt als nicht mehr lebend.
    /// Für Tests, die im selben Prozess ein abgestürztes Fenster nachstellen – ein echter Absturz beendet den Prozess, dann greift die Prozessprüfung.</summary>
    public void Abandon() { lock (Live) Live.Remove(_token); _held.Clear(); }

    public void Dispose()
    {
        foreach (var f in _held.ToList()) if (Read(f)?.Token == _token) TryDelete(f);
        _held.Clear();
        lock (Live) Live.Remove(_token);
    }

    static bool Alive(Holder h)
    {
        if (h.Pid == Environment.ProcessId) { lock (Live) return Live.Contains(h.Token); }
        try
        {
            using var p = Process.GetProcessById(h.Pid);
            if (p.HasExited) return false;
            var start = StartOf(p);
            // gleiche Prozessnummer, aber anderer Start = ein neuer, fremder Prozess hat die Nummer bekommen
            return start == default || h.Started == default || Math.Abs((start - h.Started).TotalSeconds) < 3;
        }
        catch (ArgumentException) { return false; } // läuft nicht mehr
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; } // läuft, gehört aber jemand anderem – im Zweifel nicht überschreiben
    }

    static Holder? Read(string f)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(f)) is not JsonObject o) return null;
            return new Holder((int?)o["pid"] ?? 0,
                DateTime.TryParse((string?)o["start"], null, System.Globalization.DateTimeStyles.RoundtripKind, out var s) ? s.ToUniversalTime() : default,
                (string?)o["fenster"] ?? "",
                DateTime.TryParse((string?)o["seit"], null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t : File.GetLastWriteTime(f),
                (string?)o["datei"] ?? "");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException) { return null; }
    }

    static TimeSpan Age(string f) { try { return DateTime.UtcNow - File.GetLastWriteTimeUtc(f); } catch (IOException) { return TimeSpan.Zero; } }
    static void TryDelete(string f) { try { File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
}
