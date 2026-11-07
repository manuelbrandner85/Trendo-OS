using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3, S3-09 (07.11.2026): Lippensynchron für digitale Figuren – aus Sprachaufnahme und Text (Wortzeiten von Whisper
/// oder aus den Sprechpausen) wird eine Mundbild-Spur (Viseme, Preston-Blair-Satz wie in Papagayo/Moho) mit Zeiten. Prüfung: Öffnung des Mundes
/// gegen die Lautstärke der Aufnahme (Kreuzkorrelation, 10 ms) – zu großer Versatz oder kein Zusammenhang ist ein FAIL, nie still übernommen.
/// Vergleich mit einem Video: Mundöffnung je Bild (MediaPipe) gegen die Spur.</summary>
public static class LipSync
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const int Rate = 100; // Kurven: 1 Wert je 10 ms

    /// <summary>Mundbilder (Papagayo-Namen) und wie weit der Mund offen ist (0 … 1).</summary>
    public static readonly IReadOnlyDictionary<string, double> Openness = new Dictionary<string, double>
    {
        ["AI"] = 1.0, ["O"] = 0.8, ["E"] = 0.6, ["U"] = 0.45, ["L"] = 0.45, ["WQ"] = 0.35, ["etc"] = 0.3, ["FV"] = 0.15, ["MBP"] = 0.0, ["rest"] = 0.0,
    };

    public sealed record Word(string Text, double Start, double End);
    public sealed record Key(double At, string Viseme);

    /// <summary>Deutsche Schreibung → Mundbilder (regelbasiert, ohne Wörterbuch; reicht für Mundbewegung, nicht für Aussprache-Lehre).</summary>
    public static List<string> Visemes(string word)
    {
        var w = word.ToLowerInvariant().Normalize(NormalizationForm.FormC);
        var r = new List<string>();
        void Add(string v) { if (r.Count == 0 || r[^1] != v || v is "AI" or "O" or "E" or "U") r.Add(v); }
        for (var i = 0; i < w.Length;)
        {
            bool At(string s) => string.CompareOrdinal(w, i, s, 0, s.Length) == 0;
            if (At("sch")) { Add("etc"); i += 3; continue; }
            if (At("ch")) { Add("etc"); i += 2; continue; }
            if (At("qu")) { Add("WQ"); i += 2; continue; }
            if (At("pf")) { Add("MBP"); Add("FV"); i += 2; continue; }
            if (At("ph")) { Add("FV"); i += 2; continue; }
            if (At("ei") || At("ai") || At("ay") || At("ey")) { Add("AI"); Add("E"); i += 2; continue; }
            if (At("au")) { Add("AI"); Add("U"); i += 2; continue; }
            if (At("eu") || At("äu")) { Add("O"); Add("E"); i += 2; continue; }
            if (At("ie")) { Add("E"); i += 2; continue; }
            var c = w[i++];
            switch (c)
            {
                case 'a' or 'ä' or 'á' or 'à': Add("AI"); break;
                case 'e' or 'é' or 'è' or 'i' or 'y' or 'í': Add("E"); break;
                case 'o' or 'ö' or 'ó' or 'ò': Add("O"); break;
                case 'u' or 'ü' or 'ú': Add("U"); break;
                case 'm' or 'b' or 'p': Add("MBP"); break;
                case 'f' or 'v' or 'w': Add("FV"); break;
                case 'l': Add("L"); break;
                default: if (char.IsLetter(c)) Add("etc"); break;
            }
        }
        return r;
    }

    /// <summary>Spur aus Wörtern mit Zeiten: Laute eines Wortes über seine Dauer verteilt (Vokale doppelt so lang), Pausen ab 0,12 s geschlossen („rest“).</summary>
    public static List<Key> Track(IReadOnlyList<Word> words)
    {
        var keys = new List<Key> { new(0, "rest") };
        double lastEnd = 0;
        foreach (var wd in words.Where(x => x.End > x.Start).OrderBy(x => x.Start))
        {
            if (wd.Start - lastEnd >= 0.12 && keys[^1].Viseme != "rest") keys.Add(new(lastEnd, "rest"));
            var v = Visemes(wd.Text); if (v.Count == 0) continue;
            var weights = v.Select(x => x is "AI" or "E" or "O" or "U" ? 2.0 : 1.0).ToList(); var sum = weights.Sum();
            var t = wd.Start;
            for (var i = 0; i < v.Count; i++) { keys.Add(new(Math.Round(t, 3), v[i])); t += (wd.End - wd.Start) * weights[i] / sum; }
            lastEnd = wd.End;
        }
        keys.Add(new(Math.Round(lastEnd, 3), "rest"));
        // gleiche Zeit: letzter gewinnt; gleiche Nachbarn zusammenfassen
        var r = new List<Key>();
        foreach (var k in keys) { if (r.Count > 0 && Math.Abs(r[^1].At - k.At) < 1e-6) r[^1] = k; else if (r.Count == 0 || r[^1].Viseme != k.Viseme) r.Add(k); }
        return r;
    }

    /// <summary>Whisper.cpp-JSON („-ml 1 -sow -oj“) → Wörter mit Zeiten.</summary>
    public static List<Word> ParseWhisper(string json)
    {
        var r = new List<Word>();
        var root = JsonNode.Parse(json);
        foreach (var s in root?["transcription"]?.AsArray() ?? [])
        {
            var text = ((string?)s?["text"] ?? "").Trim();
            var from = (long?)s?["offsets"]?["from"]; var to = (long?)s?["offsets"]?["to"];
            if (text.Length == 0 || from is null || to is null || text.StartsWith('[')) continue;
            r.Add(new Word(text, from.Value / 1000.0, to.Value / 1000.0));
        }
        return r;
    }

    /// <summary>Lautstärke-Hüllkurve (RMS je 10 ms, auf 0 … 1 normiert).</summary>
    public static double[] Envelope(AudioBuf a)
    {
        var step = Math.Max(1, a.Rate / Rate); var n = a.Length / step; var e = new double[n];
        for (var i = 0; i < n; i++) { double s = 0; for (var c = 0; c < a.Data.Length; c++) for (var k = 0; k < step; k++) { var v = a.Data[c][i * step + k]; s += v * v; } e[i] = Math.Sqrt(s / (step * a.Data.Length)); }
        var max = e.Length == 0 ? 0 : e.Max(); if (max > 0) for (var i = 0; i < n; i++) e[i] /= max;
        return e;
    }

    /// <summary>Wortzeiten ohne Whisper: Wörter des Textes über die gesprochenen Abschnitte verteilen (nach Buchstabenzahl). Ehrlich ungenauer.</summary>
    public static List<Word> WordsFromPauses(string text, double[] env, double threshold = 0.08)
    {
        var words = text.Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim('.', ',', '!', '?', ';', ':', '"', '„', '“', '–', '-')).Where(w => w.Length > 0).ToList();
        var segs = new List<(double S, double E)>(); int? st = null; var gap = 0;
        for (var i = 0; i <= env.Length; i++)
        {
            var on = i < env.Length && env[i] > threshold;
            if (on) { st ??= i; gap = 0; }
            else if (st is not null && ++gap >= 15) { segs.Add((st.Value / (double)Rate, (i - gap + 1) / (double)Rate)); st = null; gap = 0; }
        }
        if (st is not null) segs.Add((st.Value / (double)Rate, env.Length / (double)Rate));
        if (words.Count == 0 || segs.Count == 0) return [];
        var total = segs.Sum(s => s.E - s.S); var letters = words.Sum(w => w.Length); var r = new List<Word>();
        int si = 0; var pos = segs[0].S;
        foreach (var w in words)
        {
            var len = total * w.Length / letters;
            while (si < segs.Count - 1 && pos >= segs[si].E - 1e-6) { si++; pos = segs[si].S; }
            var end = Math.Min(segs[si].E, pos + len); r.Add(new Word(w, Math.Round(pos, 3), Math.Round(end, 3))); pos = end;
        }
        return r;
    }

    /// <summary>Gleitender Mittelwert über ±<paramref name="half"/> Werte (10 ms je Wert).</summary>
    public static double[] Smooth(double[] x, int half)
    {
        if (half <= 0) return x;
        var r = new double[x.Length]; double s = 0; int a = 0, b = -1;
        for (var i = 0; i < x.Length; i++)
        {
            while (b < Math.Min(x.Length - 1, i + half)) s += x[++b];
            while (a < i - half) s -= x[a++];
            r[i] = s / (b - a + 1);
        }
        return r;
    }

    /// <summary>Wortgrenzen am Ton ausrichten: Whisper-Wörter kleben oft aneinander und schließen Pausen ein – jedes Wort wird auf den hörbaren Teil
    /// seines Zeitfensters gekürzt (Lautstärke über <paramref name="threshold"/>). Wörter ganz ohne hörbaren Teil bleiben unverändert.</summary>
    public static List<Word> SnapToVoice(IReadOnlyList<Word> words, double[] env, double threshold = 0.06)
    {
        var r = new List<Word>();
        foreach (var w in words)
        {
            int a = Math.Max(0, (int)(w.Start * Rate)), b = Math.Min(env.Length - 1, (int)Math.Ceiling(w.End * Rate));
            int s = a; while (s <= b && env[s] < threshold) s++;
            int e = b; while (e >= s && env[e] < threshold) e--;
            r.Add(s > b || e - s < 3 ? w : new Word(w.Text, s / (double)Rate, (e + 1) / (double)Rate));
        }
        return r;
    }

    /// <summary>Mundöffnung über die Zeit (10 ms), leicht geglättet wie ein echter Mund (Übergänge ~40 ms).</summary>
    public static double[] OpennessCurve(IReadOnlyList<Key> track, double seconds)
    {
        var n = (int)Math.Ceiling(seconds * Rate); var c = new double[n]; var k = 0;
        for (var i = 0; i < n; i++) { var t = i / (double)Rate; while (k + 1 < track.Count && track[k + 1].At <= t) k++; c[i] = Openness.GetValueOrDefault(track.Count == 0 ? "rest" : track[k].Viseme); }
        var r = new double[n];
        for (var i = 0; i < n; i++) { double s = 0; var m = 0; for (var j = Math.Max(0, i - 2); j <= Math.Min(n - 1, i + 2); j++) { s += c[j]; m++; } r[i] = s / m; }
        return r;
    }

    public sealed record SyncResult(double OffsetSeconds, double Correlation, bool Ok, string Text);

    /// <summary>Versatz zwischen zwei Kurven (10 ms) – positiv: <paramref name="moving"/> kommt später als <paramref name="reference"/>. Suche ±maxSeconds.</summary>
    public static (double Offset, double Corr) Align(double[] reference, double[] moving, double maxSeconds = 0.5)
    {
        static double[] Z(double[] x) { var m = x.Length == 0 ? 0 : x.Average(); var s = Math.Sqrt(x.Sum(v => (v - m) * (v - m)) / Math.Max(1, x.Length)); return x.Select(v => s < 1e-9 ? 0 : (v - m) / s).ToArray(); }
        var a = Z(reference); var b = Z(moving); var max = (int)(maxSeconds * Rate); var best = double.MinValue; var lag0 = 0;
        for (var lag = -max; lag <= max; lag++)
        {
            double s = 0; var n = 0;
            for (var t = Math.Max(0, -lag); t < b.Length && t + lag < a.Length; t++) if (t + lag >= 0) { s += a[t + lag] * b[t]; n++; }
            if (n > Rate / 2 && s / n > best) { best = s / n; lag0 = lag; }
        }
        return (-lag0 / (double)Rate, Math.Round(best == double.MinValue ? 0 : best, 3));
    }

    /// <summary>Passt die Spur zur Aufnahme? Grenzen: |Versatz| ≤ 80 ms (ab ~45 ms hinter dem Ton wird es sichtbar, 80 ms ist die übliche Fernsehgrenze), Zusammenhang ≥ 0,3.</summary>
    public static SyncResult Check(IReadOnlyList<Key> track, double[] env, double maxOffset = 0.08, double minCorr = 0.3, int smooth = 5)
    {
        if (env.Length == 0 || env.Count(v => v > 0.08) < Rate / 4) return new(0, 0, false, "FAIL: In der Aufnahme ist keine Sprache zu hören – ohne Sprache keine Mundbilder.");
        var curve = OpennessCurve(track, env.Length / (double)Rate);
        var (off, corr) = Align(Smooth(env, smooth), Smooth(curve, smooth));
        if (corr < minCorr) return new(off, corr, false, $"FAIL: Die Mundbilder passen nicht zur Aufnahme (Zusammenhang {corr:0.00}, nötig ≥ {minCorr:0.00}).");
        if (Math.Abs(off) > maxOffset) return new(off, corr, false, $"FAIL: Mund und Ton sind um {off * 1000:+0;-0} ms versetzt (erlaubt ±{maxOffset * 1000:0} ms).");
        return new(off, corr, true, $"OK: Versatz {off * 1000:+0;-0} ms, Zusammenhang {corr:0.00}.");
    }

    /// <summary>Video-Vergleich: Mundöffnung je Bild (z. B. aus MediaPipe) gegen die Spur bzw. die Aufnahme.</summary>
    public static SyncResult CompareVideo(double[] mouthPerFrame, double fps, double[] env, double maxOffset = 0.08, double minCorr = 0.3, int smooth = 5)
    {
        if (mouthPerFrame.Length < 4 || fps <= 0) return new(0, 0, false, "FAIL: Im Video wurde kein Mund gefunden.");
        var n = (int)Math.Ceiling(mouthPerFrame.Length / fps * Rate); var m = new double[n];
        for (var i = 0; i < n; i++) { var f = i / (double)Rate * fps; var a = (int)Math.Floor(f); var b = Math.Min(mouthPerFrame.Length - 1, a + 1); var w = f - a; m[i] = mouthPerFrame[Math.Min(a, mouthPerFrame.Length - 1)] * (1 - w) + mouthPerFrame[b] * w; }
        var (off, corr) = Align(Smooth(env, smooth), Smooth(m, smooth));
        if (corr < minCorr) return new(off, corr, false, $"FAIL: Die Mundbewegung im Video folgt der Sprache nicht (Zusammenhang {corr:0.00}).");
        if (Math.Abs(off) > maxOffset) return new(off, corr, false, $"FAIL: Der Mund im Video ist um {off * 1000:+0;-0} ms gegen den Ton versetzt (erlaubt ±{maxOffset * 1000:0} ms).");
        return new(off, corr, true, $"OK: Mund im Video synchron (Versatz {off * 1000:+0;-0} ms, Zusammenhang {corr:0.00}).");
    }

    /// <summary>Papagayo/Moho-Spur („MohoSwitch1“, Bild ab 1) – lesbar in Moho, Blender (Papagayo-Import), OpenToonz, Synfig.</summary>
    public static string MohoDat(IReadOnlyList<Key> track, int fps)
    {
        var sb = new StringBuilder("MohoSwitch1\n"); var last = -1;
        foreach (var k in track) { var f = (int)Math.Round(k.At * fps) + 1; if (f == last) continue; sb.Append(f.ToString(Inv)).Append(' ').Append(k.Viseme).Append('\n'); last = f; }
        return sb.ToString();
    }

    public static string Json(IReadOnlyList<Key> track, string source, SyncResult? check) => new JsonObject
    {
        ["quelle"] = source,
        ["satz"] = "Preston Blair (Papagayo): AI, E, O, U, L, WQ, etc, FV, MBP, rest",
        ["pruefung"] = check is null ? null : new JsonObject { ["ok"] = check.Ok, ["versatz_ms"] = Math.Round(check.OffsetSeconds * 1000), ["zusammenhang"] = check.Correlation, ["text"] = check.Text },
        ["spur"] = new JsonArray(track.Select(k => (JsonNode)new JsonObject { ["zeit"] = k.At, ["mund"] = k.Viseme }).ToArray()),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    /// <summary>Mundform je Mundbild (halbe Breite, halbe Öffnung in px bei 360 × 360, Zähne sichtbar, Zunge sichtbar).</summary>
    public static readonly IReadOnlyDictionary<string, (int W, int H, bool Teeth, bool Tongue)> Shapes = new Dictionary<string, (int, int, bool, bool)>
    {
        ["AI"] = (88, 52, true, false), ["E"] = (100, 26, true, false), ["O"] = (56, 50, false, false), ["U"] = (36, 26, false, false),
        ["L"] = (78, 34, true, true), ["WQ"] = (32, 16, false, false), ["etc"] = (84, 18, true, false), ["FV"] = (86, 8, true, false),
        ["MBP"] = (80, 0, false, false), ["rest"] = (74, 0, false, false),
    };

    /// <summary>Bild eines Mundbildes (PNG 360 × 360) mit FFmpeg „geq“: Lippen, dunkle Mundhöhle, obere Zähne, Zunge; geschlossen = Lippenspalt.</summary>
    public static List<string> MouthImageArgs(string viseme, string png)
    {
        var (w, h, teeth, tongue) = Shapes.GetValueOrDefault(viseme, Shapes["rest"]);
        string N(double v) => v.ToString("0.###", Inv);
        var hh = Math.Max(0.001, h);
        var inner = $"lt(pow((X-180)/{N(w)},2)+pow((Y-180)/{N(hh)},2),1)";
        var outer = $"lt(pow((X-180)/{N(w + 16)},2)+pow((Y-180)/{N(h + 20)},2),1)";
        var teethE = teeth && h > 0 ? $"lt(Y,{N(180 - h + Math.Max(5, h * 0.42))})" : "0";
        var tongueE = tongue ? $"gt(Y,{N(180 + h * 0.35)})" : "0";
        var line = h == 0 ? $"lt(abs(Y-180),2)*lt(abs(X-180),{N(w)})" : "0";
        string C(int bg, int lip, int cave, int tooth, int tng, int seam) =>
            $"if({line},{seam},if({inner},if({teethE},{tooth},if({tongueE},{tng},{cave})),if({outer},{lip},{bg})))";
        var geq = $"geq=r='{C(29, 176, 42, 236, 190, 92)}':g='{C(29, 74, 12, 234, 92, 30)}':b='{C(36, 82, 18, 226, 104, 36)}'";
        return ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "color=c=black:s=360x360", "-frames:v", "1", "-vf", "format=rgb24," + geq, png];
    }

    /// <summary>FFmpeg-Concat-Liste: je Mundbild sein PNG für die Dauer bis zum nächsten (Vergleichsvorschau ohne Neuberechnung je Bild).</summary>
    public static string ConcatList(IReadOnlyList<Key> track, double seconds, Func<string, string> png)
    {
        var sb = new StringBuilder("ffconcat version 1.0\n"); string? last = null;
        for (var i = 0; i < track.Count; i++)
        {
            var end = i + 1 < track.Count ? track[i + 1].At : seconds; var d = end - track[i].At; if (d <= 0) continue;
            last = png(track[i].Viseme).Replace("\\", "/").Replace("'", "'\\''");
            sb.Append("file '").Append(last).Append("'\nduration ").Append(d.ToString("0.###", Inv)).Append('\n');
        }
        if (last is not null) sb.Append("file '").Append(last).Append("'\n"); // letzte Dauer wird sonst ignoriert
        return sb.ToString();
    }

    /// <summary>Vergleichsvorschau (MP4): links das Video (falls vorhanden), rechts der Mund aus der Spur, unten die Tonwelle; Ton dabei.</summary>
    public static List<string> PreviewArgs(string audio, string concatList, string output, double seconds, string? video = null)
    {
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        if (video is not null) a.AddRange(["-i", video]);
        a.AddRange(["-f", "concat", "-safe", "0", "-i", concatList, "-i", audio]);
        int mi = video is null ? 0 : 1, ai = mi + 1;
        var ww = video is null ? 360 : 720;
        var fc = $"[{mi}:v]fps=25,format=yuv420p[m];[{ai}:a]showwaves=s={ww}x90:mode=cline:draw=full:scale=sqrt:colors=0xd8dcf0:r=25[w0];color=c=0x15151b:s={ww}x90:r=25[wb];[wb][w0]overlay=shortest=1,format=yuv420p[w]";
        fc += video is null ? ";[m][w]vstack=2[v]" : ";[0:v]scale=360:360:force_original_aspect_ratio=decrease,pad=360:360:(ow-iw)/2:(oh-ih)/2,fps=25,format=yuv420p[s];[s][m]hstack=2[t];[t][w]vstack=2[v]";
        a.AddRange(["-filter_complex", fc, "-map", "[v]", "-map", $"{ai}:a", "-t", seconds.ToString("0.###", Inv), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", output]);
        return a;
    }
}
