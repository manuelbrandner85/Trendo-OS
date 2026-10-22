using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>Vorschaubild aus einem Film und mehrere Ausschnitte aneinanderhängen. Nutzt das vorhandene FFmpeg, installiert nichts.</summary>
public static class MediaCut
{
    public static string[] FrameArgs(string input, string output, double seconds) =>
    [
        "-hide_banner", "-loglevel", "error", "-y",
        "-ss", seconds.ToString("0.###", CultureInfo.InvariantCulture),
        "-i", input, "-frames:v", "1", output,
    ];

    public static (Cli.Result Result, string Output) Aneinander(string ffmpeg, IReadOnlyList<(string Datei, double Start, double Ende)> clips, string zielOrdner)
    {
        if (clips.Count < 2) throw new ArgumentException("Bitte mindestens zwei Ausschnitte.");
        Directory.CreateDirectory(zielOrdner);
        var work = Path.Combine(zielOrdner, "schnitt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(work);
        var parts = new List<string>();
        for (var i = 0; i < clips.Count; i++)
        {
            var (datei, start, ende) = clips[i];
            if (!File.Exists(datei)) return (new(-1, "", "Datei nicht gefunden: " + Path.GetFileName(datei)), "");
            if (ende <= start) throw new ArgumentException("Das Ende muss nach dem Start liegen.");
            var part = Path.Combine(work, $"teil-{i}.mp4");
            var r = Cli.Run(ffmpeg, MediaStudio.Args(datei, part, "trim", start, ende - start), null, 600000);
            if (!r.Ok) return (r, part);
            parts.Add(part);
        }
        var list = Path.Combine(work, "liste.txt");
        var sb = new StringBuilder();
        foreach (var p in parts) sb.Append("file '").Append(p.Replace("'", "'\\''")).Append('\'').Append('\n');
        File.WriteAllText(list, sb.ToString());
        var outp = Path.Combine(work, "gesamt.mp4");
        var c = Cli.Run(ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", list, "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-movflags", "+faststart", outp], null, 600000);
        return (c, outp);
    }
}
