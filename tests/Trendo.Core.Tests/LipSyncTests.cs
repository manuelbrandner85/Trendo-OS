using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-09 (07.11.2026): Lippensynchron – Mundbilder, Spur, Prüfung, Vorschau.</summary>
public class LipSyncTests
{
    /// <summary>Sprachähnlicher Ton: Lautstärke folgt der Mundöffnung der Spur (verschoben um <paramref name="shift"/> s).</summary>
    static AudioBuf Speech(IReadOnlyList<LipSync.Key> track, double seconds, double shift = 0, int rate = 16000)
    {
        var curve = LipSync.OpennessCurve(track, seconds); var x = new float[(int)(seconds * rate)]; var rng = new Random(5);
        for (var i = 0; i < x.Length; i++)
        {
            var t = i / (double)rate - shift; var k = (int)(t * LipSync.Rate);
            var amp = k >= 0 && k < curve.Length ? curve[k] : 0;
            x[i] = (float)(amp * (0.5 * Math.Sin(2 * Math.PI * 140 * i / rate) + 0.25 * Math.Sin(2 * Math.PI * 280 * i / rate) + 0.05 * (rng.NextDouble() - 0.5)));
        }
        return new AudioBuf(rate, [x]);
    }

    static readonly LipSync.Word[] Words = [new("Heute", 0.3, 0.7), new("Abend", 0.75, 1.15), new("tanzen", 1.4, 1.85), new("wir", 1.9, 2.1), new("am", 2.15, 2.3), new("Fluss", 2.35, 2.9)];

    [Fact(DisplayName = "S3-09-P: deutsche Wörter → Mundbilder (Papagayo-Satz), Spur mit Zeiten und Pausen, Moho-Datei und Prüfung OK bei passender Aufnahme")]
    public void Track_and_check()
    {
        Assert.Equal(["MBP", "AI", "MBP", "AI"], LipSync.Visemes("Mama"));
        Assert.Equal(["etc", "U", "L", "E"], LipSync.Visemes("Schule"));
        Assert.Equal(["etc", "AI", "U", "FV", "E"], LipSync.Visemes("Haufwe")[..5]);
        Assert.Equal(["MBP", "FV", "E", "etc", "E"], LipSync.Visemes("Pferde")[..5]);
        var tr = LipSync.Track(Words);
        Assert.Equal("rest", tr[0].Viseme); Assert.Equal("rest", tr[^1].Viseme); Assert.Equal(2.9, tr[^1].At);
        Assert.Contains(tr, k => k.Viseme == "rest" && Math.Abs(k.At - 1.15) < 1e-6); // Pause 1,15–1,40 s
        Assert.True(tr.Zip(tr.Skip(1)).All(p => p.First.At < p.Second.At));
        var dat = LipSync.MohoDat(tr, 24); Assert.StartsWith("MohoSwitch1\n1 rest\n", dat);
        var env = LipSync.Envelope(Speech(tr, 3.2));
        var ok = LipSync.Check(tr, env); Assert.True(ok.Ok, ok.Text); Assert.InRange(ok.OffsetSeconds, -0.02, 0.02);
        Assert.Contains("\"mund\": \"MBP\"", LipSync.Json(LipSync.Track([new("Mama", 0, 1)]), "test", ok));
    }

    [Fact(DisplayName = "S3-09-N: Versatz von 200 ms, unpassende Aufnahme und Stille werden als FAIL gemeldet")]
    public void Check_fails()
    {
        var tr = LipSync.Track(Words);
        var late = LipSync.Check(tr, LipSync.Envelope(Speech(tr, 3.4, 0.2)));
        Assert.False(late.Ok); Assert.Contains("versetzt", late.Text); Assert.InRange(late.OffsetSeconds, -0.23, -0.17); // Spur kommt 200 ms vor dem Ton
        var other = LipSync.Track([new("Ooo", 0.1, 0.5), new("Mmm", 1.6, 2.0), new("Aaa", 2.6, 3.1)]);
        Assert.Contains("passen nicht", LipSync.Check(other, LipSync.Envelope(Speech(tr, 3.2))).Text);
        Assert.Contains("keine Sprache", LipSync.Check(tr, LipSync.Envelope(new AudioBuf(16000, [new float[48000]]))).Text);
        Assert.Contains("kein Mund", LipSync.CompareVideo([0.1, 0.2], 25, new double[300]).Text);
    }

    [Fact(DisplayName = "S3-09: Wortzeiten ohne Whisper aus den Sprechpausen; Whisper-JSON wird gelesen; Video-Mund gegen Ton verglichen")]
    public void Pauses_whisper_video()
    {
        var tr = LipSync.Track(Words); var env = LipSync.Envelope(Speech(tr, 3.2));
        var w = LipSync.WordsFromPauses("Heute Abend, tanzen wir am Fluss!", env);
        Assert.Equal(6, w.Count); Assert.InRange(w[0].Start, 0.25, 0.45); Assert.InRange(w[^1].End, 2.7, 3.0);
        var json = """{"transcription":[{"offsets":{"from":300,"to":700},"text":" Heute"},{"offsets":{"from":700,"to":700},"text":"[_BEG_]"},{"offsets":{"from":750,"to":1150},"text":" Abend"}]}""";
        Assert.Equal([new LipSync.Word("Heute", 0.3, 0.7), new LipSync.Word("Abend", 0.75, 1.15)], LipSync.ParseWhisper(json));
        // Mund im Video (25 Bilder/s) = Öffnung der Spur, 120 ms zu spät
        var curve = LipSync.OpennessCurve(tr, 3.2); var frames = Enumerable.Range(0, 80).Select(f => { var k = (int)((f / 25.0 - 0.12) * 100); return k < 0 ? 0 : curve[Math.Min(curve.Length - 1, k)]; }).ToArray();
        var v = LipSync.CompareVideo(frames, 25, env); Assert.False(v.Ok); Assert.InRange(v.OffsetSeconds, 0.09, 0.15);
        var v2 = LipSync.CompareVideo(Enumerable.Range(0, 80).Select(f => curve[Math.Min(curve.Length - 1, f * 4)]).ToArray(), 25, env); Assert.True(v2.Ok, v2.Text);
    }

    /// <summary>Echter Lauf nur mit TRENDO_LIP_WHISPER (whisper-cli.exe), TRENDO_LIP_MODEL (ggml-base.bin) und TRENDO_LIP_WAV (Sprachaufnahme).</summary>
    [Fact(DisplayName = "S3-09 echt: Sprachaufnahme → Whisper-Wortzeiten → Mundbilder; Prüfung OK; dieselbe Spur auf 200 ms später gesetzten Ton → FAIL; Vorschau")]
    public void Real_whisper()
    {
        string? cli = Environment.GetEnvironmentVariable("TRENDO_LIP_WHISPER"), model = Environment.GetEnvironmentVariable("TRENDO_LIP_MODEL"), src = Environment.GetEnvironmentVariable("TRENDO_LIP_WAV");
        var ff = Cli.Find("ffmpeg"); if (cli is null || model is null || src is null || ff is null) return;
        var keep = Environment.GetEnvironmentVariable("TRENDO_LIP_BELEG");
        var dir = keep ?? Path.Combine(Path.GetTempPath(), "trendo-lipecht-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        var log = new List<string>();
        var wav = Path.Combine(dir, "sprache-16k.wav"); Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-i", src, "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav], dir, 60000).Ok);
        var b = Path.Combine(dir, "woerter"); var r = Cli.Run(cli, ["-m", model, "-f", wav, "-l", "de", "-ml", "1", "-sow", "-oj", "-of", b], dir, 600000);
        log.Add($"whisper-cli Exitcode {r.Exit}"); Assert.True(r.Ok && File.Exists(b + ".json"), r.Err);
        var words = LipSync.ParseWhisper(File.ReadAllText(b + ".json"));
        log.Add($"Wörter ({words.Count}): " + string.Join(" ", words.Select(w => $"{w.Text}[{w.Start:0.00}-{w.End:0.00}]")));
        Assert.True(words.Count >= 10, string.Join("\n", log));
        var buf = AudioFile.Read(wav); var env = LipSync.Envelope(buf);
        var raw = LipSync.Check(LipSync.Track(words), env); log.Add("Whisper-Zeiten roh (ohne Ausrichten): " + raw.Text);
        var tr = LipSync.Track(LipSync.SnapToVoice(words, env));
        var ok = LipSync.Check(tr, env); log.Add("Prüfung (Wortgrenzen am Ton ausgerichtet): " + ok.Text);
        // Ton 200 ms später (Stille vorn), Spur bleibt → muss FAIL sein
        var late = new float[buf.Length + buf.Rate / 5]; Array.Copy(buf.Data[0], 0, late, buf.Rate / 5, buf.Length);
        var bad = LipSync.Check(tr, LipSync.Envelope(new AudioBuf(buf.Rate, [late]))); log.Add("Ton 200 ms später: " + bad.Text);
        // ohne Whisper: Text über Sprechpausen
        var pw = LipSync.WordsFromPauses(string.Join(" ", words.Select(w => w.Text)), env); var pc = LipSync.Check(LipSync.Track(pw), env); log.Add("Nur Text + Pausen: " + pc.Text);
        File.WriteAllText(Path.Combine(dir, "mundbilder.json"), LipSync.Json(tr, "Whisper base (whisper.cpp b5454)", ok));
        File.WriteAllText(Path.Combine(dir, "mundbilder-25fps.dat"), LipSync.MohoDat(tr, 25));
        foreach (var v in LipSync.Shapes.Keys) Cli.Run(ff, LipSync.MouthImageArgs(v, Path.Combine(dir, "mund-" + v + ".png")), dir, 60000);
        var list = Path.Combine(dir, "mund.ffconcat"); File.WriteAllText(list, LipSync.ConcatList(tr, buf.Length / (double)buf.Rate, v => Path.Combine(dir, "mund-" + v + ".png")));
        var pr = Cli.Run(ff, LipSync.PreviewArgs(src, list, Path.Combine(dir, "vergleich-echt.mp4"), buf.Length / (double)buf.Rate), dir, 120000); log.Add($"Vorschau Exitcode {pr.Exit}");
        File.WriteAllLines(Path.Combine(dir, "messung.txt"), log);
        Assert.True(ok.Ok, string.Join("\n", log)); Assert.False(bad.Ok, string.Join("\n", log)); Assert.Contains("versetzt", bad.Text); Assert.True(pr.Ok, pr.Err);
        if (keep is null) Directory.Delete(dir, true);
    }

    /// <summary>Echter Lauf nur mit TRENDO_LIP_MOUTH (JSON aus „trendo_pruefung.py --mund“) und TRENDO_LIP_MOUTH_WAV (Ton des Videos).</summary>
    [Fact(DisplayName = "S3-09 echt: Mund im KI-Video (MediaPipe je Bild) gegen den Gesang verglichen; derselbe Mund 200 ms verschoben wird als versetzt erkannt")]
    public void Real_video_compare()
    {
        string? mj = Environment.GetEnvironmentVariable("TRENDO_LIP_MOUTH"), mw = Environment.GetEnvironmentVariable("TRENDO_LIP_MOUTH_WAV");
        var ff = Cli.Find("ffmpeg"); if (mj is null || mw is null || ff is null) return;
        var dir = Path.GetDirectoryName(mj)!;
        var j = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(mj))!; var fps = (double)j["fps"]!;
        var mouth = j["mund"]!.AsArray().Select(v => v is null ? 0 : (double)v).ToArray();
        var wav = Path.Combine(dir, "ton-16k.wav"); Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-i", mw, "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav], dir, 60000).Ok);
        var env = LipSync.Envelope(AudioFile.Read(wav));
        var r = LipSync.CompareVideo(mouth, fps, env);
        var shift = (int)Math.Round(0.2 * fps); var late = Enumerable.Repeat(mouth[0], shift).Concat(mouth).Take(mouth.Length).ToArray();
        var r2 = LipSync.CompareVideo(late, fps, env);
        File.WriteAllLines(Path.Combine(dir, "vergleich-video.txt"), [$"Bilder {mouth.Length}, {fps} Bilder/s", "Video wie erzeugt: " + r.Text, $"Mund {shift} Bilder (~200 ms) später: " + r2.Text]);
        Assert.True(r2.OffsetSeconds - r.OffsetSeconds is > 0.12 and < 0.28, $"{r.Text} | {r2.Text}");
        Assert.False(r2.Ok && Math.Abs(r2.OffsetSeconds) > 0.08);
    }

    [Fact(DisplayName = "S3-09 echt mit FFmpeg: Vergleichsvorschau (Mund mit Namen, Tonwelle, Ton) als MP4 in Länge der Aufnahme")]
    public void Real_preview()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-lip-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var tr = LipSync.Track(Words); var wav = Path.Combine(dir, "stimme.wav"); AudioFile.WriteWav(Speech(tr, 3.2), wav);
            foreach (var v in LipSync.Shapes.Keys) { var r0 = Cli.Run(ff, LipSync.MouthImageArgs(v, Path.Combine(dir, v + ".png")), dir, 60000); Assert.True(r0.Ok, r0.Err); }
            var list = Path.Combine(dir, "mund.ffconcat"); File.WriteAllText(list, LipSync.ConcatList(tr, 3.2, v => Path.Combine(dir, v + ".png")));
            var mp4 = Path.Combine(dir, "vergleich.mp4");
            var r = Cli.Run(ff, LipSync.PreviewArgs(wav, list, mp4, 3.2), dir, 120000); Assert.True(r.Ok && File.Exists(mp4), r.Err);
            var probe = Cli.Run(ff, ["-hide_banner", "-i", mp4], dir, 30000).Err;
            Assert.Contains("Duration: 00:00:03.2", probe); Assert.Contains("360x450", probe); Assert.Contains("Audio: aac", probe);
            if (Environment.GetEnvironmentVariable("TRENDO_LIP_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep); File.Copy(mp4, Path.Combine(keep, "vergleich-synthetisch.mp4"), true);
                Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", "0.5", "-i", mp4, "-frames:v", "1", Path.Combine(keep, "vergleich-synthetisch.png")], dir, 30000);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
