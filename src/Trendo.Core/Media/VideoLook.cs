using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>T-0695 (16.11.2026): Vorschau = Export. Das Bild am Abspielkopf und die Wiedergabe mit Effekten entstehen aus demselben Filtergraphen wie der Export
/// (<see cref="VideoEdit.PictureGraph"/>, <see cref="VideoPro.OverlayGraph"/>) – nur auf den Ausschnitt um die Stelle begrenzt: Farbe, Looks, Effektstapel, Titel,
/// Keyframes, Übergänge, Einblendungen und eingebrannte Untertitel sehen in der Vorschau aus wie in der fertigen Datei. Vorher zeigte die Vorschau nur das rohe Stück.</summary>
public static class VideoLook
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F6(double v) => v.ToString("0.######", Inv);

    public static int Fps(JsonObject p) => p["fps"] is JsonValue v && v.TryGetValue<double>(out var f) && f >= 1 ? (int)f : p["fps"] is JsonValue v2 && v2.TryGetValue<int>(out var i) && i >= 1 ? i : 30;

    /// <summary>Zeit auf das nächste Einzelbild des Projekts gerundet – genau dieses Bild steht im Export an dieser Stelle.</summary>
    public static double Snap(JsonObject p, double t) { var fps = Fps(p); return Math.Max(0, Math.Round(t * fps) / fps); }

    /// <summary>Gemeinsamer Bildteil für den Ausschnitt ab <paramref name="from"/>; liefert Argumente, Graph und das Etikett des fertigen Bildes.</summary>
    static (List<string> Args, List<string> Graph, string V, int W, int H, int Fps) Build(JsonObject p, double from, double to, VideoExport.Options? o, Func<string, string>? source, bool still = false)
    {
        var (a, f, input, w, h, fps, _, vb) = VideoEdit.PictureGraph(p, new VideoEdit.Window(from, to, still), source);
        a.AddRange(VideoPro.OverlayGraph(p, f, vb, "vout", ref input, w, h, fps, from, to, source));
        var v = "vout";
        if (o?.Subtitles is { Length: > 0 } srt && File.Exists(srt))
        {
            // eingebrannte Untertitel wie im Export, ohne Bereich und Zielgröße (die Vorschau zeigt das Projektformat)
            (v, var ao) = VideoExport.Tail(f, v, "abase", w, h, new VideoExport.Options(VideoExport.Get("projekt"), Subtitles: srt));
            f.Add($"[{ao}]anullsink");
        }
        else f.Add("[abase]anullsink"); // Ton spielt die Vorschau getrennt (fertige Mischung über mpv)
        return (a, f, v, w, h, fps);
    }

    /// <summary>FFmpeg-Aufruf für ein Standbild (PNG) zur Zeit <paramref name="t"/>. <paramref name="maxWidth"/> &gt; 0 verkleinert erst am Ende – das Bild entsteht in Projektgröße.</summary>
    public static List<string> FrameArgs(JsonObject p, double t, string output, int maxWidth = 0, VideoExport.Options? o = null, Func<string, string>? source = null)
    {
        var tq = Snap(p, Math.Min(t, Math.Max(0, VideoEdit.VideoLength(VideoEdit.Clips(p)) - 1.0 / Fps(p))));
        // Viertelbild Abstand: trim rundet den Start auf die Zeitbasis der Verbindung (oft 1/Bildrate) – ein halbes Bild würde auf das Bild davor fallen
        var half = 0.5 / Fps(p); var quarter = 0.25 / Fps(p);
        var (a, f, v, w, _, _) = Build(p, tq, tq + half, o, source, still: true);
        f.Add($"[{v}]format=yuv420p,trim=start={F6(Math.Max(0, tq - quarter))},setpts=PTS-STARTPTS{(maxWidth > 0 && maxWidth < w ? $",scale={maxWidth}:-2" : "")}[vlook]");
        a.AddRange(["-filter_complex", string.Join(";", f), "-map", "[vlook]", "-frames:v", "1", "-update", "1", output]);
        return a;
    }

    /// <summary>FFmpeg-Aufruf für die Wiedergabe ab <paramref name="from"/>: fertige Bilder als BGRA in <paramref name="width"/>×<paramref name="height"/> auf die Standardausgabe.</summary>
    public static List<string> StreamArgs(JsonObject p, double from, int width, int height, VideoExport.Options? o = null, Func<string, string>? source = null)
    {
        var quarter = 0.25 / Fps(p);
        var (a, f, v, _, _, _) = Build(p, from, 0, o, source);
        f.Add($"[{v}]format=yuv420p,trim=start={F6(Math.Max(0, from - quarter))},setpts=PTS-STARTPTS,scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,format=bgra[vlook]");
        a.Insert(1, "-nostdin");
        a.AddRange(["-filter_complex", string.Join(";", f), "-map", "[vlook]", "-f", "rawvideo", "-pix_fmt", "bgra", "pipe:1"]);
        return a;
    }

    /// <summary>Schlüssel für den Zwischenspeicher: alles, was das Bild ändert (Projekt ohne Abspielkopf, Stand der Quelldateien, Stelle, Größe).</summary>
    public static string Key(JsonObject p, double t, int maxWidth, VideoExport.Options? o = null)
    {
        var q = (JsonObject)p.DeepClone(); q.Remove("playhead");
        var sb = new StringBuilder(q.ToJsonString()).Append('|').Append(F6(Snap(p, t))).Append('|').Append(maxWidth).Append('|').Append(o?.Subtitles);
        foreach (var file in VideoEdit.Clips(p).Select(c => c.File).Concat(VideoPro.Overlays(p).Select(x => x.File)).Append(o?.Subtitles ?? "").Distinct())
            if (File.Exists(file)) sb.Append('|').Append(File.GetLastWriteTimeUtc(file).Ticks);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..24];
    }

    /// <summary>Standbild rendern (oder aus dem Zwischenspeicher <paramref name="dir"/> nehmen). Abbrechen beendet nur den eigenen FFmpeg-Prozess.
    /// Liefert die PNG-Datei oder null, wenn FFmpeg kein Bild geliefert hat.</summary>
    public static async Task<string?> RenderFrameAsync(string ffmpeg, JsonObject p, double t, string dir, int maxWidth = 0, VideoExport.Options? o = null, Func<string, string>? source = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, Key(p, t, maxWidth, o) + ".png");
        if (File.Exists(png)) return png;
        var tmp = Path.Combine(dir, Guid.NewGuid().ToString("N")[..12] + ".tmp.png");
        var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = true };
        foreach (var a in FrameArgs(p, t, tmp, maxWidth, o, source)) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
        proc.StandardInput.Close();
        var err = proc.StandardError.ReadToEndAsync(CancellationToken.None);
        _ = proc.StandardOutput.ReadToEndAsync(CancellationToken.None);
        try { await proc.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(true); } catch (InvalidOperationException) { }
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
            throw;
        }
        var e = await err;
        if (proc.ExitCode != 0 || !File.Exists(tmp) || new FileInfo(tmp).Length == 0)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
            LastError = e.Trim().Length > 0 ? e.Trim().Split('\n')[^1] : $"FFmpeg-Code {proc.ExitCode}";
            return null;
        }
        File.Move(tmp, png, true);
        return png;
    }

    /// <summary>Letzte Fehlermeldung von FFmpeg (für die Anzeige „Vorschau nicht möglich“).</summary>
    public static string LastError { get; private set; } = "";

    /// <summary>Zwischenspeicher klein halten: älteste Bilder löschen, wenn mehr als <paramref name="keep"/> darin liegen.</summary>
    public static void Trim(string dir, int keep = 400)
    {
        try
        {
            var files = new DirectoryInfo(dir).GetFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc).Skip(keep).ToList();
            foreach (var f in files) f.Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { }
    }
}
