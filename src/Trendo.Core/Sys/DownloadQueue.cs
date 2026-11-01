using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trendo.Core.Sys;

/// <summary>Download-Manager (Masterprompt 131, Runde 6): Warteschlange mit mehreren gleichzeitigen Downloads, Anhalten/Fortsetzen je Eintrag
/// (über die vorhandene .part-Fortsetzung von <see cref="Web"/>), Reihenfolge, Prüfsumme, Geschwindigkeit und Restzeit. Die Liste bleibt über Neustarts erhalten;
/// beim Start wird „lädt“ zu „angehalten“ (nichts lädt ungefragt weiter). Netz läuft in der App (QA-019) – hier nur Zustand und Regeln.</summary>
public sealed partial class DownloadQueue
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum State { Wartet, Lädt, Angehalten, Fertig, Fehler }

    public sealed class Job
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public string Url { get; set; } = "";
        public string Target { get; set; } = "";
        public State Status { get; set; } = State.Wartet;
        public long Done { get; set; }
        public long Total { get; set; } = -1;
        public string? ExpectedHash { get; set; }
        public string? HashResult { get; set; }
        public string Message { get; set; } = "";
        public DateTimeOffset Added { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset? Finished { get; set; }
        [JsonIgnore] public double BytesPerSecond { get; set; }
        public string Name => Path.GetFileName(Target);
        public double Fraction => Total > 0 ? Math.Clamp((double)Done / Total, 0, 1) : 0;
        /// <summary>Restzeit in Sekunden (−1 = unbekannt).</summary>
        public double SecondsLeft => Total > 0 && BytesPerSecond > 1 ? (Total - Done) / BytesPerSecond : -1;
    }

    public const int MaxParallelLimit = 6;
    readonly string _file;
    public List<Job> Jobs { get; } = [];
    public int MaxParallel { get; set; } = 3;

    public DownloadQueue(string dataRoot)
    {
        _file = Path.Combine(dataRoot, "downloads", "warteschlange.json");
        try
        {
            if (File.Exists(_file) && JsonSerializer.Deserialize<Saved>(File.ReadAllText(_file)) is { } s)
            {
                Jobs.AddRange(s.Jobs); MaxParallel = Math.Clamp(s.MaxParallel, 1, MaxParallelLimit);
                foreach (var j in Jobs.Where(j => j.Status == State.Lädt)) { j.Status = State.Angehalten; j.Message = "Beim letzten Beenden unterbrochen – „Fortsetzen“ macht dort weiter."; }
            }
        }
        catch (Exception e) when (e is JsonException or IOException) { }
    }

    sealed record Saved(List<Job> Jobs, int MaxParallel);
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file + ".tmp", JsonSerializer.Serialize(new Saved(Jobs, MaxParallel), new JsonSerializerOptions { WriteIndented = true })); SafeFile.Move(_file + ".tmp", _file);
    }

    [GeneratedRegex(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase)] private static partial Regex Links();

    /// <summary>Adressen aus eingefügtem Text (auch mehrere, z. B. aus einer Liste oder Webseite). Nur http/https, ohne Doppelte.</summary>
    public static IReadOnlyList<Uri> ParseLinks(string text) =>
        Links().Matches(text).Select(m => m.Value.TrimEnd('.', ',', ';', ')', ']')).Select(s => Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? u : null)
            .OfType<Uri>().DistinctBy(u => u.AbsoluteUri).ToList();

    /// <summary>Eintrag anlegen. Gleicher Name im Ordner oder in der Liste → „Name (2).ext“. Prüfsumme optional (MD5/SHA-1/SHA-256/SHA-512).</summary>
    public Job Add(Uri url, string folder, string? expectedHash = null)
    {
        if (url.Scheme is not ("http" or "https")) throw new ArgumentException("Nur http- und https-Adressen.");
        if (expectedHash is { Length: > 0 } h && Web.AlgoFor(h) is null) throw new ArgumentException("Das sieht nicht wie eine Prüfsumme aus (MD5, SHA-1, SHA-256 oder SHA-512).");
        if (Jobs.FirstOrDefault(j => j.Url == url.AbsoluteUri && j.Status is not (State.Fertig or State.Fehler)) is { } same) return same;
        var name = Web.SafeFileName(url); var target = Path.Combine(folder, name);
        for (var n = 2; File.Exists(target) || File.Exists(Web.PartFile(target)) && Web.LoadState(target)?.Url != url.AbsoluteUri || Jobs.Any(j => j.Target.Equals(target, StringComparison.OrdinalIgnoreCase)); n++)
            target = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}");
        var job = new Job { Url = url.AbsoluteUri, Target = target, ExpectedHash = expectedHash is { Length: > 0 } ? Regex.Replace(expectedHash, @"\s", "") : null };
        Jobs.Add(job); Save(); return job;
    }

    public Job Get(string id) => Jobs.FirstOrDefault(j => j.Id == id) ?? throw new KeyNotFoundException("Diesen Download gibt es nicht mehr.");

    /// <summary>Welche Einträge jetzt starten dürfen (in Listenreihenfolge, höchstens <see cref="MaxParallel"/> gleichzeitig).</summary>
    public IReadOnlyList<Job> ToStart()
    {
        var free = MaxParallel - Jobs.Count(j => j.Status == State.Lädt);
        return free <= 0 ? [] : Jobs.Where(j => j.Status == State.Wartet).Take(free).ToList();
    }

    public void Started(Job j) { j.Status = State.Lädt; j.Message = ""; Save(); }
    public void Pause(string id) { var j = Get(id); if (j.Status is State.Lädt or State.Wartet) { j.Status = State.Angehalten; j.Message = "Angehalten."; Save(); } }
    /// <summary>Fortsetzen bzw. erneut versuchen: wieder in die Warteschlange (lädt ab der vorhandenen .part-Datei weiter).</summary>
    public void Resume(string id) { var j = Get(id); if (j.Status is State.Angehalten or State.Fehler) { j.Status = State.Wartet; j.Message = ""; Save(); } }
    public void Failed(Job j, string why) { j.Status = State.Fehler; j.Message = why; j.BytesPerSecond = 0; Save(); }

    /// <summary>Fertig: Prüfsumme vergleichen (wenn angegeben). Stimmt sie nicht, gilt der Download als Fehler – die Datei bleibt zur Prüfung liegen.</summary>
    public void Completed(Job j)
    {
        j.Done = File.Exists(j.Target) ? new FileInfo(j.Target).Length : j.Done; if (j.Total <= 0) j.Total = j.Done;
        j.BytesPerSecond = 0; j.Finished = DateTimeOffset.Now;
        if (j.ExpectedHash is { } h && File.Exists(j.Target))
        {
            var (ok, algo, actual) = Web.VerifyHash(j.Target, h);
            j.HashResult = $"{algo} {(ok ? "stimmt" : "stimmt NICHT")}";
            if (!ok) { j.Status = State.Fehler; j.Message = $"Prüfsumme ({algo}) stimmt nicht – die Datei ist beschädigt oder nicht die erwartete. Nicht öffnen."; Save(); return; }
        }
        j.Status = State.Fertig; j.Message = ""; Save();
    }

    /// <summary>Aus der Liste nehmen. Unfertige Teil-Dateien werden mit entfernt (sie gehören nur dem Download); fertige Dateien bleiben im Ordner.</summary>
    public void Remove(string id)
    {
        var j = Get(id);
        if (j.Status != State.Fertig) { try { File.Delete(Web.PartFile(j.Target)); File.Delete(Web.StateFile(j.Target)); } catch (IOException) { } }
        Jobs.Remove(j); Save();
    }

    /// <summary>Fertige Einträge aus der Liste nehmen (Dateien bleiben).</summary>
    public int ClearFinished() { var n = Jobs.RemoveAll(j => j.Status == State.Fertig); if (n > 0) Save(); return n; }

    public void Move(string id, int delta)
    {
        var j = Get(id); var i = Jobs.IndexOf(j); var k = Math.Clamp(i + delta, 0, Jobs.Count - 1);
        if (k == i) return; Jobs.RemoveAt(i); Jobs.Insert(k, j); Save();
    }

    /// <summary>Fortschritt melden; Geschwindigkeit geglättet (gleitender Mittelwert), damit die Restzeit nicht springt.</summary>
    public static void Progress(Job j, long done, long total, double seconds)
    {
        if (seconds > 0 && done > j.Done) { var now = (done - j.Done) / seconds; j.BytesPerSecond = j.BytesPerSecond <= 0 ? now : j.BytesPerSecond * 0.7 + now * 0.3; }
        j.Done = done; if (total > 0) j.Total = total;
    }

    public (int Running, int Waiting, int Done, int Failed) Counts => (Jobs.Count(j => j.Status == State.Lädt), Jobs.Count(j => j.Status is State.Wartet or State.Angehalten), Jobs.Count(j => j.Status == State.Fertig), Jobs.Count(j => j.Status == State.Fehler));

    public static string Duration(double s) => s < 0 ? "–" : s < 60 ? $"{Math.Ceiling(s)} s" : s < 3600 ? $"{Math.Floor(s / 60)} min {Math.Ceiling(s % 60)} s" : $"{Math.Floor(s / 3600)} h {Math.Floor(s % 3600 / 60)} min";
}
