using System.Globalization;

namespace Trendo.Core;

/// <summary>T-0732 (Audit-Restliste Rang 7): Stapel für „Medien bearbeiten“ – eine Aufgabe auf viele Dateien, eine nach der anderen,
/// Fortschritt je Datei (aus FFmpegs „-progress“), Abbruch jederzeit. Jedes Ergebnis wird erst in eine Zwischendatei im Ausgabeordner
/// geschrieben und nach Erfolg umbenannt – ein Abbruch hinterlässt keine halbe Datei. Originale werden nie verändert.</summary>
public enum MediaBatchState { Wartet, Laeuft, Fertig, Fehler, Uebersprungen, Abgebrochen }

/// <summary>Stand einer Datei im Stapel.</summary>
public sealed class MediaBatchItem(string input)
{
    public string Input { get; } = input;
    public MediaBatchState State { get; internal set; } = MediaBatchState.Wartet;
    /// <summary>Fortschritt 0…1.</summary>
    public double Fraction { get; internal set; }
    public string? Output { get; internal set; }
    public string? Message { get; internal set; }

    /// <summary>Für einen neuen Lauf zurücksetzen.</summary>
    public void Reset() { State = MediaBatchState.Wartet; Fraction = 0; Output = null; Message = null; }

    /// <summary>Kurzer deutscher Zustand für die Liste.</summary>
    public string StateText => State switch
    {
        MediaBatchState.Wartet => "wartet",
        MediaBatchState.Laeuft => $"läuft … {Fraction * 100:0} %",
        MediaBatchState.Fertig => "fertig",
        MediaBatchState.Fehler => "Fehler" + (Message is { Length: > 0 } m ? ": " + m : ""),
        MediaBatchState.Uebersprungen => "übersprungen" + (Message is { Length: > 0 } m2 ? " – " + m2 : ""),
        _ => "abgebrochen",
    };
}

public static class MediaBatch
{
    /// <summary>Passt die Aufgabe zur Datei? Sonst der Grund (die Datei wird dann übersprungen).</summary>
    public static string? Unsuitable(string file, MediaJob job)
    {
        if (!File.Exists(file)) return "Datei nicht gefunden";
        var img = MediaStudio.IsImage(file); var audio = MediaStudio.IsAudio(file);
        if (job.Id == "image") return img ? null : "nur für Bilder";
        if (img) return "Bild – dafür „Bild verkleinern“ wählen";
        if (job.NeedsVideo && audio) return "Tondatei – diese Aufgabe braucht ein Video";
        return null;
    }

    /// <summary>Wie lange die Ausgabe wird (für den Fortschritt); null = unbekannt.</summary>
    public static double? OutputSeconds(string jobId, double? duration, double start, double length) => jobId switch
    {
        "thumb" or "image" => null,
        "gif" => duration is { } d ? Math.Min(5, d) : 5,
        "trim" => duration is { } d2 ? Math.Max(0.1, Math.Min(length, d2 - start)) : length,
        _ => duration,
    };

    /// <summary>Zeile aus „-progress“ lesen: „out_time_us=“ bzw. „out_time_ms=“ (beide in Mikrosekunden) → Sekunden.</summary>
    public static double? ProgressSeconds(string line)
    {
        var i = line.IndexOf('=');
        if (i < 0) return null;
        var k = line[..i];
        if (k is not ("out_time_us" or "out_time_ms")) return null;
        return long.TryParse(line[(i + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var us) && us >= 0 ? us / 1e6 : null;
    }

    /// <summary>Alle Dateien nacheinander umwandeln. <paramref name="changed"/> meldet jede Änderung (aus einem Hintergrund-Thread).</summary>
    public static async Task RunAsync(string ffmpeg, IReadOnlyList<MediaBatchItem> items, string jobId, string outDir, Action<MediaBatchItem>? changed, CancellationToken ct, double start = 0, double length = 10)
    {
        var job = MediaStudio.Jobs.FirstOrDefault(j => j.Id == jobId) ?? throw new ArgumentException("Unbekannte Aufgabe " + jobId);
        Directory.CreateDirectory(outDir);
        foreach (var it in items)
        {
            if (ct.IsCancellationRequested) { if (it.State == MediaBatchState.Wartet) { it.State = MediaBatchState.Abgebrochen; changed?.Invoke(it); } continue; }
            if (Unsuitable(it.Input, job) is { } why) { it.State = MediaBatchState.Uebersprungen; it.Message = why; changed?.Invoke(it); continue; }
            it.State = MediaBatchState.Laeuft; it.Fraction = 0; changed?.Invoke(it);
            var total = OutputSeconds(jobId, MediaStudio.IsImage(it.Input) ? null : MediaStudio.Duration(ffmpeg, it.Input), start, length);
            var outp = MediaStudio.OutputPath(it.Input, job, outDir);
            var tmp = MediaStudio.TempOf(outp);
            var args = MediaStudio.Args(it.Input, tmp, jobId, start, length);
            string[] full = ["-progress", "pipe:1", "-nostats", .. args];
            var moved = false;
            try
            {
                var r = await Cli.RunStreamAsync(ffmpeg, full, outDir, line =>
                {
                    if (total is > 0 && ProgressSeconds(line) is { } sec) { var f = Math.Clamp(sec / total.Value, 0, 0.99); if (f > it.Fraction + 0.01) { it.Fraction = f; changed?.Invoke(it); } }
                }, ct, timeoutMs: 3600_000).ConfigureAwait(false);
                if (r.Exit == Cli.Cancelled || ct.IsCancellationRequested) { it.State = MediaBatchState.Abgebrochen; it.Message = null; }
                else if (r.Ok && File.Exists(tmp) && new FileInfo(tmp).Length > 0)
                {
                    SafeFile.Move(tmp, outp); moved = true;
                    it.State = MediaBatchState.Fertig; it.Fraction = 1; it.Output = outp;
                }
                else { it.State = MediaBatchState.Fehler; it.Message = (r.Err.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "unbekannt").Trim(); }
            }
            finally
            {
                if (!moved) TempDir.Delete(tmp, attempts: 25, delayMs: 200); // T-0715-Muster: nie eine halbe Datei liegen lassen
            }
            changed?.Invoke(it);
        }
    }
}
