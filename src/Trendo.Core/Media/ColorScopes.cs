using System.Globalization;
using System.Text.RegularExpressions;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3, S3-11 (06.11.2026): Farbkorrektur wie im Profi-Schnitt – 3D-LUT (.cube) je Stück, Scopes (Waveform, Vektorskop)
/// und Messwerte (Helligkeit, Sättigung, Farbton) zum Vergleichen zweier Stücke. Eine LUT gilt nur als passend, wenn die Messung das zeigt.</summary>
public static class ColorScopes
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public sealed record Cube(int Size, string Title);

    /// <summary>Prüft eine .cube-Datei (Adobe/Resolve-Format): LUT_3D_SIZE 2–256 und genau Größe³ Zeilen mit drei Zahlen im Wertebereich.</summary>
    public static Cube ParseCube(string text)
    {
        int size = 0; var title = ""; var rows = 0; double min = 0, max = 1;
        foreach (var raw in text.Split('\n'))
        {
            var l = raw.Trim(); if (l.Length == 0 || l.StartsWith('#')) continue;
            if (l.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase)) { title = l[5..].Trim().Trim('"'); continue; }
            if (l.StartsWith("LUT_1D_SIZE", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Das ist eine 1D-LUT – für Farbe je Stück wird eine 3D-LUT (.cube mit LUT_3D_SIZE) gebraucht.");
            if (l.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase)) { if (!int.TryParse(l[11..].Trim(), NumberStyles.Integer, Inv, out size) || size is < 2 or > 256) throw new ArgumentException("LUT_3D_SIZE ungültig."); continue; }
            if (l.StartsWith("DOMAIN_MIN", StringComparison.OrdinalIgnoreCase)) { min = Num(l[10..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]); continue; }
            if (l.StartsWith("DOMAIN_MAX", StringComparison.OrdinalIgnoreCase)) { max = Num(l[10..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]); continue; }
            if (char.IsLetter(l[0])) continue; // weitere Schlüsselwörter (LUT_3D_INPUT_RANGE …)
            var parts = l.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) throw new ArgumentException($"LUT beschädigt: Zeile „{l}“ hat nicht drei Werte.");
            foreach (var x in parts) { var v = Num(x); if (v < min - 0.5 || v > max + 0.5) throw new ArgumentException($"LUT beschädigt: Wert {x} außerhalb des Bereichs."); }
            rows++;
        }
        if (size == 0) throw new ArgumentException("Keine 3D-LUT: LUT_3D_SIZE fehlt.");
        if (rows != size * size * size) throw new ArgumentException($"LUT unvollständig: {rows} statt {size * size * size} Farbwerte.");
        return new Cube(size, title);
        static double Num(string s) => double.TryParse(s, NumberStyles.Float, Inv, out var v) ? v : throw new ArgumentException($"LUT beschädigt: „{s}“ ist keine Zahl.");
    }

    /// <summary>Prüft eine LUT-Datei vor dem Setzen; nur .cube.</summary>
    public static Cube ValidateCubeFile(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("LUT nicht gefunden: " + Path.GetFileName(file));
        if (!file.EndsWith(".cube", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Nur .cube-LUTs (Resolve/Adobe). ICC-Profile sind keine LUT.");
        if (new FileInfo(file).Length > 64L * 1024 * 1024) throw new ArgumentException("LUT-Datei zu groß.");
        return ParseCube(File.ReadAllText(file));
    }

    /// <summary>Pfad für FFmpeg-Filter (lut3d=file='…').</summary>
    public static string FilterPath(string path) => path.Replace('\\', '/').Replace(":", "\\:").Replace("'", "\\'");

    public static string LutFilter(string lut) => lut.Length == 0 ? "" : $",lut3d=file='{FilterPath(lut)}'";

    /// <summary>Scopes als ein Bild: Bild · Waveform (Luma) · Vektorskop, je 480 × 270.</summary>
    public static List<string> ScopeArgs(string input, double seconds, string pngOut, string extraFilter = "") =>
        ["-hide_banner", "-loglevel", "error", "-y", "-ss", seconds.ToString("0.###", Inv), "-i", input, "-frames:v", "1", "-filter_complex",
         $"[0:v]format=yuv444p{extraFilter},split=3[o][w][v];[o]scale=480:270,setsar=1,format=rgb24[o2];[w]format=yuv444p,waveform=g=green:fl=numbers,scale=480:270,setsar=1,format=rgb24[wf];[v]format=yuv444p,vectorscope=m=color3:g=green,scale=270:270,pad=480:270:105:0,setsar=1,format=rgb24[vs];[o2][wf][vs]hstack=inputs=3",
         pngOut];

    public sealed record Stats(double YAvg, double YMin, double YMax, double SatAvg, double HueAvg);

    /// <summary>Messwerte eines Bildes (signalstats): Helligkeit 0–255, Sättigung, mittlerer Farbton in Grad.</summary>
    public static List<string> StatsArgs(string input, double seconds, string extraFilter = "") =>
        ["-hide_banner", "-ss", seconds.ToString("0.###", Inv), "-i", input, "-frames:v", "1", "-vf", $"format=yuv444p{extraFilter},signalstats,metadata=mode=print", "-f", "null", "-"];

    public static Stats? ParseStats(string log)
    {
        double? V(string k) { var m = Regex.Match(log, $@"lavfi\.signalstats\.{k}=([-0-9.]+)"); return m.Success ? double.Parse(m.Groups[1].Value, Inv) : null; }
        return V("YAVG") is { } y && V("SATAVG") is { } s ? new Stats(y, V("YMIN") ?? 0, V("YMAX") ?? 255, s, V("HUEAVG") ?? 0) : null;
    }

    public sealed record Match(bool Equivalent, string Text);

    /// <summary>Bildgenauer Vergleich desselben Bildes mit und ohne Farbfilter/LUT (SSIM, PSNR) – erkennt auch vertauschte Farbkanäle,
    /// die im Mittelwert gleich aussehen.</summary>
    public static List<string> EquivalenceArgs(string input, double seconds, string extraFilter) =>
        ["-hide_banner", "-nostats", "-ss", seconds.ToString("0.###", Inv), "-i", input, "-frames:v", "1", "-lavfi",
         $"[0:v]split[a][b];[a]format=yuv444p{extraFilter},format=yuv420p,split[x1][x2];[b]format=yuv420p,split[y1][y2];[x1][y1]ssim;[x2][y2]psnr", "-f", "null", "-"];

    /// <summary>Gleichwertig nur bei SSIM ≥ 0,98 und PSNR ≥ 35 dB.</summary>
    public static Match Equivalence(VideoEnhance.Quality q) => q.Ssim >= 0.98 && q.Psnr >= 35
        ? new(true, $"Bildgleich (SSIM {q.Ssim:0.000}, PSNR {q.Psnr:0.0} dB).")
        : new(false, $"Nicht gleichwertig: SSIM {q.Ssim:0.000}, PSNR {q.Psnr:0.0} dB – die LUT verändert das Bild deutlich.");

    /// <summary>Zwei Bilder farblich vergleichen (z. B. Stück mit LUT gegen das Referenzstück). Gleichwertig nur innerhalb enger Grenzen.</summary>
    public static Match Compare(Stats a, Stats reference)
    {
        var dy = a.YAvg - reference.YAvg; var ds = a.SatAvg - reference.SatAvg;
        var dh = Math.Abs(a.HueAvg - reference.HueAvg) % 360; if (dh > 180) dh = 360 - dh;
        var hueMatters = a.SatAvg > 8 && reference.SatAvg > 8;
        var problems = new List<string>();
        if (Math.Abs(dy) > 6) problems.Add($"Helligkeit {(dy > 0 ? "+" : "")}{dy:0.0}");
        if (Math.Abs(ds) > 5) problems.Add($"Sättigung {(ds > 0 ? "+" : "")}{ds:0.0}");
        if (hueMatters && dh > 10) problems.Add($"Farbton {dh:0}° verschoben");
        var vals = $"Helligkeit {a.YAvg:0.0}/{reference.YAvg:0.0}, Sättigung {a.SatAvg:0.0}/{reference.SatAvg:0.0}, Farbton {a.HueAvg:0}°/{reference.HueAvg:0}°";
        return problems.Count == 0 ? new(true, "Farblich gleichwertig – " + vals) : new(false, "Nicht gleichwertig: " + string.Join(", ", problems) + " – " + vals);
    }
}
