using System.Globalization;
using System.Text.RegularExpressions;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3, S3-12 (06.11.2026): Bild verbessern mit FFmpeg – Stabilisieren (deshake), Entrauschen (hqdn3d), Schärfen (unsharp),
/// Hochskalieren (Lanczos) – und messbar prüfen (SSIM, PSNR). Eine Verbesserung gilt nur mit Messung gegen eine Referenz als belegt;
/// beim Hochskalieren wird zurückgerechnet und bei Artefakten abgeraten.</summary>
public static class VideoEnhance
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="Denoise">0 = aus, 1 = stark.</param><param name="Sharpen">0 = aus, 1 = stark.</param><param name="Upscale">1 oder 2.</param>
    public sealed record Options(bool Stabilize = false, double Denoise = 0, double Sharpen = 0, int Upscale = 1)
    {
        public bool Any => Stabilize || Denoise > 0.01 || Sharpen > 0.01 || Upscale > 1;
    }

    public sealed record Quality(double Ssim, double Psnr);

    public sealed record Verdict(string Light, string Text, bool MayApply);

    static string F(double v) => v.ToString("0.###", Inv);

    /// <summary>FFmpeg-Filterkette in fester Reihenfolge: erst ruhig, dann sauber, dann scharf, dann groß.</summary>
    public static string Filter(Options o)
    {
        var f = new List<string>();
        if (o.Stabilize) f.Add("deshake");
        if (o.Denoise > 0.01) { var d = Math.Clamp(o.Denoise, 0, 1) * 2; f.Add($"hqdn3d={F(4 * d)}:{F(3 * d)}:{F(6 * d)}:{F(4.5 * d)}"); }
        if (o.Sharpen > 0.01) f.Add($"unsharp=5:5:{F(Math.Clamp(o.Sharpen, 0, 1) * 1.5)}:5:5:0");
        if (o.Upscale > 1) f.Add($"scale=iw*{Math.Clamp(o.Upscale, 1, 4)}:ih*{Math.Clamp(o.Upscale, 1, 4)}:flags=lanczos");
        f.Add("format=yuv420p");
        return string.Join(",", f);
    }

    /// <summary>Aufruf zum Verbessern: Ton wird unverändert übernommen (falls vorhanden).</summary>
    public static List<string> Args(string input, string output, Options o)
    {
        if (!o.Any) throw new ArgumentException("Nichts zu verbessern – mindestens eine Wahl treffen.");
        return ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-map", "0:v:0", "-map", "0:a?", "-vf", Filter(o), "-c:v", "libx264", "-crf", "16", "-preset", "medium", "-c:a", "copy", output];
    }

    /// <summary>SSIM und PSNR von <paramref name="test"/> gegen <paramref name="reference"/>; das Testbild wird auf die Referenzgröße gebracht
    /// (beim Hochskalieren = Rückrechnung).</summary>
    public static List<string> CompareArgs(string test, string reference, int width, int height) =>
        ["-hide_banner", "-nostats", "-i", test, "-i", reference, "-lavfi", $"[0:v]scale={width}:{height}:flags=bicubic,format=yuv420p,split[a][b];[1:v]format=yuv420p,split[c][d];[a][c]ssim;[b][d]psnr", "-f", "null", "-"];

    /// <summary>Liest „SSIM … All:0.987“ und „PSNR … average:34.5“ (oder „inf“ bei gleichen Bildern) aus der FFmpeg-Ausgabe.</summary>
    public static Quality? ParseQuality(string log)
    {
        var s = Regex.Match(log, @"SSIM .*?All:([0-9.]+)"); var p = Regex.Match(log, @"PSNR .*?average:(inf|[0-9.]+)");
        if (!s.Success || !p.Success) return null;
        return new Quality(double.Parse(s.Groups[1].Value, Inv), p.Groups[1].Value == "inf" ? 100 : double.Parse(p.Groups[1].Value, Inv));
    }

    /// <summary>Mit Referenz (sauberes Original): verbessert nur, wenn SSIM spürbar steigt und PSNR nicht fällt.</summary>
    public static Verdict Judge(Quality before, Quality after)
    {
        var dS = after.Ssim - before.Ssim; var dP = after.Psnr - before.Psnr;
        var vals = $"SSIM {before.Ssim:0.000} → {after.Ssim:0.000}, PSNR {before.Psnr:0.0} → {after.Psnr:0.0} dB";
        if (dS >= 0.005 && dP >= -0.2) return new("gruen", "Messbar besser: " + vals, true);
        if (dS <= -0.005 || dP <= -0.5) return new("rot", "Schlechter als vorher – nicht übernehmen: " + vals, false);
        return new("gelb", "Kaum Unterschied: " + vals, true);
    }

    /// <summary>Hochskalieren: zurückgerechnet auf die alte Größe muss das Bild dem Original gleichen (SSIM ≥ 0,97), sonst sind Artefakte entstanden.
    /// Mehr Pixel allein gelten nie als Verbesserung.</summary>
    public static Verdict JudgeUpscale(Quality roundTrip)
    {
        var vals = $"Rückrechnung SSIM {roundTrip.Ssim:0.000}, PSNR {roundTrip.Psnr:0.0} dB";
        return roundTrip.Ssim >= 0.97 && roundTrip.Psnr >= 30
            ? new("gelb", "Hochskaliert ohne erkennbare Artefakte (" + vals + ") – mehr Pixel heißt nicht mehr Details, bitte selbst vergleichen.", true)
            : new("rot", "Artefakte beim Hochskalieren (" + vals + ") – nicht übernommen.", false);
    }

    /// <summary>Ohne Referenz: zeigt nur, wie stark das Bild verändert wurde – keine Behauptung „besser“.</summary>
    /// <param name="stabilized">Stabilisieren verschiebt das Bild gewollt – dann darf es stärker abweichen.</param>
    public static Verdict JudgeChange(Quality vsOriginal, bool stabilized = false) =>
        vsOriginal.Ssim < (stabilized ? 0.5 : 0.80)
            ? new("rot", $"Bild stark verändert (SSIM {vsOriginal.Ssim:0.000} zum Original) – Einstellung zu stark, nicht übernommen.", false)
            : new("gelb", $"Verändert: SSIM {vsOriginal.Ssim:0.000}, PSNR {vsOriginal.Psnr:0.0} dB zum Original – Vorher/Nachher selbst vergleichen.", true);

    public sealed record Result(string Output, Verdict Verdict, Quality? VsOriginal);

    /// <summary>Bildgröße aus „ffmpeg -i“ (Zeile „Video: …, 1920x1080“).</summary>
    public static (int W, int H)? ParseSize(string log)
    {
        var m = Regex.Match(log, @"Video:.*?\b(\d{2,5})x(\d{2,5})\b");
        return m.Success ? (int.Parse(m.Groups[1].Value, Inv), int.Parse(m.Groups[2].Value, Inv)) : null;
    }

    /// <summary>Verbessern und prüfen. <paramref name="run"/> führt FFmpeg aus (Argumente → Ergebnis), damit Tests ohne FFmpeg laufen.
    /// Bei rotem Urteil wird die Ausgabedatei wieder gelöscht.</summary>
    public static Result Enhance(Func<IReadOnlyList<string>, Cli.Result> run, string input, string output, Options o, int width = 0, int height = 0)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("Quelle fehlt: " + Path.GetFileName(input));
        if (width < 16 || height < 16) (width, height) = ParseSize(run(["-hide_banner", "-i", input]).Err) ?? (0, 0);
        if (width < 16 || height < 16) throw new InvalidOperationException("Bildgröße der Quelle unbekannt – ist das ein Video?");
        var r = run(Args(input, output, o));
        if (!r.Ok || !File.Exists(output)) throw new InvalidOperationException("Verbessern ging nicht: " + (r.Err.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? ""));
        var q = ParseQuality(run(CompareArgs(output, input, width, height)).Err);
        if (q is null) { TryDelete(output); throw new InvalidOperationException("Messung (SSIM/PSNR) ging nicht – Ergebnis nicht übernommen."); }
        var v = o.Upscale > 1 ? JudgeUpscale(q) : JudgeChange(q, o.Stabilize);
        if (o.Upscale > 1 && v.MayApply && (o.Denoise > 0.01 || o.Sharpen > 0.01 || o.Stabilize)) v = v with { Text = v.Text + " Weitere Schritte verändern das Bild zusätzlich." };
        if (!v.MayApply) TryDelete(output);
        return new Result(output, v, q);
        static void TryDelete(string f) { try { File.Delete(f); } catch (IOException) { } }
    }
}
