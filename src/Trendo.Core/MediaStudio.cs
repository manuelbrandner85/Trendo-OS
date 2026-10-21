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
        var r = Cli.Run(ffmpeg, Args(input, outp, jobId, start, length), Path.GetDirectoryName(Path.GetFullPath(input)), 600000);
        if (!r.Ok && File.Exists(outp) && new FileInfo(outp).Length == 0) File.Delete(outp);
        return (r, outp);
    }

    /// <summary>Dauer in Sekunden über ffprobe (neben ffmpeg).</summary>
    public static double? Duration(string ffmpeg, string file)
    {
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", Path.GetFileName(ffmpeg).Replace("ffmpeg", "ffprobe"));
        var r = Cli.Run(File.Exists(probe) ? probe : "ffprobe", ["-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", file], null, 30000);
        return r.Ok && double.TryParse(r.Out.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
