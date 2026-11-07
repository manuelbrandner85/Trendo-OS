using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3, S3-02 (07.11.2026): Proxy-Dateien wie im Profi-Schnitt – leichte Kopien (540p, H.264, kurze Bildgruppen) für flüssige
/// Vorschau bei großen Kameradateien. Geschnitten wird mit dem Proxy, ausgegeben immer das Original; fehlt das Original, wird nie der Proxy ausgeliefert.</summary>
public static class VideoProxy
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Eindeutiger Proxy-Name je Original (Pfad, Größe, Änderungszeit) – ändert sich das Original, entsteht ein neuer Proxy.</summary>
    public static string PathFor(string original, string proxyDir)
    {
        var fi = new FileInfo(original);
        var key = $"{Path.GetFullPath(original)}|{(fi.Exists ? fi.Length : 0)}|{(fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0)}";
        var h = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)))[..12].ToLowerInvariant();
        return Path.Combine(proxyDir, $"{Path.GetFileNameWithoutExtension(original)}-{h}.proxy.mp4");
    }

    /// <summary>FFmpeg-Aufruf: 540 Pixel hoch, schnelle Kodierung, Schlüsselbild alle 12 Bilder (schnelles Springen), Ton bleibt.</summary>
    public static List<string> Args(string original, string proxy) =>
        ["-hide_banner", "-loglevel", "error", "-y", "-i", original, "-map", "0:v:0", "-map", "0:a?", "-vf", "scale=-2:'min(540,ih)'", "-c:v", "libx264", "-preset", "veryfast", "-crf", "28", "-g", "12",
         "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart", proxy];

    public static Dictionary<string, string> Map(JsonObject p) =>
        (p["proxy"] as JsonObject ?? []).Where(x => x.Value is JsonValue).ToDictionary(x => x.Key, x => x.Value!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);

    public static void Set(JsonObject p, string original, string proxy) { var o = p["proxy"] as JsonObject ?? (JsonObject)(p["proxy"] = new JsonObject()); o[original] = proxy; }

    public static void Clear(JsonObject p) => p.Remove("proxy");

    /// <summary>Datei für die Vorschau: Proxy, wenn eingeschaltet und vorhanden – sonst das Original.</summary>
    public static string ForPreview(JsonObject p, string original) =>
        p["useProxy"] is JsonValue v && v.TryGetValue<bool>(out var on) && on && Map(p).TryGetValue(original, out var px) && File.Exists(px) ? px : original;

    /// <summary>Proxy prüfen: gleiche Länge wie das Original (±0,1 s). Sonst würde der Schnitt beim Export verrutschen.</summary>
    public static bool SameLength(double originalSeconds, double proxySeconds) => Math.Abs(originalSeconds - proxySeconds) <= 0.1;

    public static double? ParseDuration(string ffmpegInfo)
    {
        var m = System.Text.RegularExpressions.Regex.Match(ffmpegInfo, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
        return m.Success ? int.Parse(m.Groups[1].Value, Inv) * 3600 + int.Parse(m.Groups[2].Value, Inv) * 60 + double.Parse(m.Groups[3].Value, Inv) : null;
    }

    public sealed record Made(string Original, string? Proxy, string Note);

    /// <summary>Proxys für alle Videos der Bildspur erzeugen (vorhandene werden wiederverwendet). <paramref name="run"/> führt FFmpeg aus.</summary>
    public static List<Made> MakeAll(Func<IReadOnlyList<string>, Cli.Result> run, JsonObject p, string proxyDir)
    {
        Directory.CreateDirectory(proxyDir); var r = new List<Made>();
        foreach (var file in VideoEdit.Clips(p).Where(c => !c.IsImage).Select(c => c.File).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(file)) { r.Add(new(file, null, "Original fehlt")); continue; }
            var px = PathFor(file, proxyDir);
            if (File.Exists(px)) { Set(p, file, px); r.Add(new(file, px, "schon da")); continue; }
            var res = run(Args(file, px));
            var lo = ParseDuration(run(["-hide_banner", "-i", file]).Err); var lp = File.Exists(px) ? ParseDuration(run(["-hide_banner", "-i", px]).Err) : null;
            if (!res.Ok || !File.Exists(px) || lo is null || lp is null || !SameLength(lo.Value, lp.Value))
            {
                try { if (File.Exists(px)) File.Delete(px); } catch (IOException) { }
                r.Add(new(file, null, lo is not null && lp is not null ? $"Länge weicht ab ({lo:0.00} s / {lp:0.00} s) – verworfen" : "Proxy ging nicht"));
                continue;
            }
            Set(p, file, px); r.Add(new(file, px, "neu"));
        }
        return r;
    }
}
