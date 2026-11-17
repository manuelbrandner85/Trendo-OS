using System.Globalization;

namespace Trendo.Core;

/// <summary>CORE-08: Kreativ- & Medien-Studio – häufige Aufgaben mit FFmpeg, in einfachen Worten.</summary>
public sealed record MediaJob(string Id, string Label, string Ext, string Help, bool NeedsVideo);

public static class MediaStudio
{
    public static readonly IReadOnlyList<MediaJob> Jobs =
    [
        new("mp4", "In MP4 umwandeln", ".mp4", "Läuft danach auf fast jedem Gerät.", true),
        new("mp3", "Ton herausholen (MP3)", ".mp3", "Nur die Tonspur als MP3.", false),
        new("small", "Kleiner machen", ".mp4", "Höchstens 1280 Pixel breit, deutlich kleinere Datei.", true),
        new("thumb", "Vorschaubild", ".jpg", "Ein Bild aus der ersten Sekunde.", true),
        new("gif", "Kurzes GIF (5 s)", ".gif", "Die ersten 5 Sekunden als GIF.", true),
        new("trim", "Ausschnitt", ".mp4", "Schneidet ab Start die gewünschte Länge heraus.", true),
        new("image", "Bild verkleinern", ".jpg", "Höchstens 1920 Pixel breit.", false),
    ];

    public static readonly string[] Inputs = [".mp4", ".mkv", ".mov", ".avi", ".webm", ".mp3", ".wav", ".flac", ".m4a", ".ogg", ".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    public static string OutputPath(string input, MediaJob job)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(input))!; var b = Path.GetFileNameWithoutExtension(input) + "-" + job.Id;
        var p = Path.Combine(dir, b + job.Ext);
        for (var i = 2; File.Exists(p); i++) p = Path.Combine(dir, $"{b}-{i}{job.Ext}");
        return p;
    }

    public static string[] Args(string input, string output, string jobId, double start = 0, double length = 10)
    {
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var head = new List<string> { "-hide_banner", "-loglevel", "error", "-n" };
        if (jobId == "trim") head.AddRange(["-ss", F(Math.Max(0, start))]);
        head.AddRange(["-i", input]);
        head.AddRange(jobId switch
        {
            "mp4" => ["-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-movflags", "+faststart"],
            "mp3" => ["-vn", "-c:a", "libmp3lame", "-q:a", "2"],
            "small" => ["-vf", "scale='min(1280,iw)':-2", "-c:v", "libx264", "-preset", "veryfast", "-crf", "28", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k"],
            "thumb" => ["-ss", "1", "-frames:v", "1", "-q:v", "3"],
            "gif" => ["-t", "5", "-vf", "fps=12,scale='min(480,iw)':-2:flags=lanczos"],
            "trim" => ["-t", F(Math.Max(0.1, length)), "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-c:a", "aac"],
            "image" => ["-vf", "scale='min(1920,iw)':-2", "-q:v", "3"],
            _ => throw new ArgumentException("Unbekannte Aufgabe " + jobId),
        });
        head.Add(output);
        return head.ToArray();
    }

    public static (Cli.Result Result, string Output) Run(string ffmpeg, string input, string jobId, double start = 0, double length = 10)
    {
        var job = Jobs.First(j => j.Id == jobId);
        if (!File.Exists(input)) return (new(-1, "", "Datei nicht gefunden."), "");
        var outp = OutputPath(input, job);
        var r = RunInto(ffmpeg, Args(input, outp, jobId, start, length), outp, Path.GetDirectoryName(Path.GetFullPath(input))!);
        return (r, outp);
    }

    /// <summary>T-0715 (Programm-Hülle E3): Ergebnis atomar – FFmpeg schreibt in eine Zwischendatei neben dem Ziel (gleiche Endung, damit FFmpeg das Format erkennt),
    /// erst nach Erfolg wird sie unter den richtigen Namen geschoben. Ein Abbruch mitten im Umwandeln hinterlässt nie eine halbe Datei unter dem Ergebnisnamen.
    /// <paramref name="args"/> endet mit <paramref name="output"/> (wie <see cref="Args"/> und <see cref="ConcatArgs"/> sie bauen).</summary>
    public static Cli.Result RunInto(string ffmpeg, string[] args, string output, string workDir, int timeoutMs = 600000)
    {
        var full = Path.GetFullPath(output);
        var tmp = TempOf(full);
        var a = (string[])args.Clone(); a[^1] = tmp;
        var moved = false;
        try
        {
            var r = Cli.Run(ffmpeg, a, workDir, timeoutMs);
            if (r.Ok && File.Exists(tmp) && new FileInfo(tmp).Length > 0) { SafeFile.Move(tmp, full); moved = true; }
            return r;
        }
        finally
        {
            // Ohne Umbenennen (Fehler, Zeitüberschreitung, Ausnahme) darf die „.~xxxx.teil.<Endung>“ nicht im Ordner des Nutzers liegen bleiben.
            // Nach einer Zeitüberschreitung wird FFmpeg nur beendet, nicht abgewartet – die Datei ist dann oft noch kurz gesperrt, deshalb mit Wiederholungen.
            if (!moved) TempDir.Delete(tmp, attempts: 25, delayMs: 200);
        }
    }

    /// <summary>Name der Zwischendatei: „&lt;Name&gt;.~&lt;zufällig&gt;.teil&lt;Endung&gt;“ im Zielordner.</summary>
    public static string TempOf(string output) =>
        Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + ".~" + Guid.NewGuid().ToString("N")[..8] + ".teil" + Path.GetExtension(output));

    static string Inv(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>T-0205: ein Vorschaubild bei Sekunde <paramref name="at"/>, 160 Pixel breit.</summary>
    public static string[] FrameArgs(string input, double at, string output) =>
        ["-hide_banner", "-loglevel", "error", "-y", "-ss", Inv(Math.Max(0, at)), "-i", input, "-frames:v", "1", "-vf", "scale=160:-2", output];

    /// <summary>Zeitleiste: <paramref name="count"/> Vorschaubilder gleichmäßig verteilt; vorhandene Bilder werden wiederverwendet.
    /// T-0726: jedes Bild atomar (<see cref="RunInto"/>) – unter dem Bildnamen liegt nur ein vollständiges Bild.</summary>
    public static IReadOnlyList<string> Frames(string ffmpeg, string input, double duration, int count, string dir)
    {
        Directory.CreateDirectory(dir);
        var list = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var at = duration <= 0 ? 0 : duration * (i + 0.5) / count;
            var png = Path.Combine(dir, $"bild-{i:00}.png");
            // T-0726: atomar wie die Ergebnisse – ein abgebrochenes FFmpeg hinterließ sonst ein halbes Bild unter dem richtigen Namen, das danach für immer wiederverwendet wurde
            if (!File.Exists(png)) RunInto(ffmpeg, FrameArgs(input, at, png), png, dir, 30000);
            if (File.Exists(png)) list.Add(png);
        }
        return list;
    }

    /// <summary>Ausschnitt von Start bis Ende (Sekunden) als neue MP4-Datei.</summary>
    public static (Cli.Result Result, string Output) Cut(string ffmpeg, string input, double start, double end)
    {
        if (end <= start) return (new(-1, "", "Das Ende muss nach dem Anfang liegen."), "");
        return Run(ffmpeg, input, "trim", start, end - start);
    }

    /// <summary>Liste für den concat-Demuxer; einfache Anführungszeichen im Pfad werden maskiert.</summary>
    public static string ConcatList(IEnumerable<string> files) =>
        string.Concat(files.Select(f => "file '" + Path.GetFullPath(f).Replace("\\", "/").Replace("'", "'\\''") + "'\n"));

    public static string[] ConcatArgs(string listFile, string output) =>
        ["-hide_banner", "-loglevel", "error", "-n", "-f", "concat", "-safe", "0", "-i", listFile,
         "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-c:a", "aac", output];

    /// <summary>Hängt Clips aneinander (neu kodiert, damit unterschiedliche Quellen zusammenpassen).</summary>
    public static (Cli.Result Result, string Output) Concat(string ffmpeg, IReadOnlyList<string> files, string folder)
    {
        if (files.Count < 2) return (new(-1, "", "Bitte mindestens zwei Clips in die Liste legen."), "");
        if (files.FirstOrDefault(f => !File.Exists(f)) is { } missing) return (new(-1, "", "Datei nicht gefunden: " + Path.GetFileName(missing)), "");
        Directory.CreateDirectory(folder);
        var list = Path.Combine(folder, "clipliste.txt"); AtomicFile.WriteAllText(list, ConcatList(files)); // T-0715: atomar
        var o = Path.Combine(folder, "zusammen.mp4");
        for (var i = 2; File.Exists(o); i++) o = Path.Combine(folder, $"zusammen-{i}.mp4");
        var r = RunInto(ffmpeg, ConcatArgs(list, o), o, folder); // T-0715: atomar
        return (r, o);
    }

    public static bool IsImage(string file) => Path.GetExtension(file).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp";
    public static bool IsAudio(string file) => Path.GetExtension(file).ToLowerInvariant() is ".mp3" or ".wav" or ".flac" or ".m4a" or ".ogg";

    /// <summary>Dauer in Sekunden über ffprobe (neben ffmpeg).</summary>
    public static double? Duration(string ffmpeg, string file)
    {
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", Path.GetFileName(ffmpeg).Replace("ffmpeg", "ffprobe"));
        var r = Cli.Run(File.Exists(probe) ? probe : "ffprobe", ["-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", file], null, 30000);
        return r.Ok && double.TryParse(r.Out.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
