using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0732: Aufnahme bis Stopp mit Pegel (echtes FFmpeg, Tonquelle statt Mikrofon) und Stapel für „Medien bearbeiten“ (echtes FFmpeg).</summary>
public class TonMedienT0732Tests
{
    static string Dir() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0732-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static string Video(string dir, string name, double seconds, string size = "320x240")
    {
        var p = Path.Combine(dir, name);
        var r = Cli.Run("ffmpeg", ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", $"testsrc=duration={seconds}:size={size}:rate=25", "-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}",
            "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-shortest", p], dir, 120000);
        Assert.True(r.Ok, r.Err); return p;
    }

    [NeedsFact("ffmpeg", DisplayName = "T-0732 Aufnahme: läuft bis Stopp (nicht fest 10 s), Pegel sichtbar, WAV sauber abgeschlossen und so lang wie aufgenommen")]
    public async Task Record_until_stop_with_level()
    {
        var dir = Dir(); var wav = Path.Combine(dir, "aufnahme.wav");
        // Tonquelle in Echtzeit (-re) statt Mikrofon – FFmpeg, WAV-Schreiben, Pegelstrom und Stopp sind echt
        using var rec = MicRecorder.Start("ffmpeg", ["-re", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=600"], wav);
        var t = System.Diagnostics.Stopwatch.StartNew();
        while (rec.Seconds < 2.5 && t.Elapsed < TimeSpan.FromSeconds(20)) await Task.Delay(50);
        Assert.True(rec.Running, "Aufnahme muss weiterlaufen, bis Stopp kommt: " + rec.Errors);
        Assert.InRange(rec.Level, 0.05, 1.0); // Sinus aus lavfi hat Amplitude 1/8
        Assert.InRange(MicRecorder.Db(rec.Level), -30, 0);
        var secs = rec.Seconds;
        var got = rec.Stop();
        Assert.Equal(wav, got);
        var a = AudioFile.Read(wav); // wirft bei kaputtem Kopf
        Assert.Equal(44100, a.Rate); Assert.Equal(1, a.Channels);
        Assert.InRange(a.Seconds, secs - 0.5, secs + 1.0);
        Assert.True(a.Seconds is > 2.0 and < 15, $"Länge {a.Seconds}");
        Assert.InRange(AudioEdit.PeakDb(a), -24, 0);
        Assert.False(rec.Running);
    }

    [Fact(DisplayName = "T-0732 Aufnahme: Argumente ohne Shell, zwei Ausgaben (WAV und Pegelstrom), Mikrofon über DirectShow")]
    public void Record_args()
    {
        var a = MicRecorder.Args(MicRecorder.DshowInput("Mikrofon (USB; \"x\")"), "C:/a b/x.wav");
        Assert.Contains("audio=Mikrofon (USB; \"x\")", a);
        Assert.Contains("C:/a b/x.wav", a); Assert.Equal("pipe:1", a[^1]);
        Assert.Equal(2, a.Count(x => x == "-map"));
        Assert.Equal(double.NegativeInfinity, MicRecorder.Db(0)); Assert.Equal(0, MicRecorder.Db(1), 6);
    }

    [Fact(DisplayName = "T-0732 Stapel: Fortschrittszeilen, Eignung je Datei, Ausgabedauer")]
    public void Batch_rules()
    {
        Assert.Equal(1.5, MediaBatch.ProgressSeconds("out_time_us=1500000"));
        Assert.Equal(2.0, MediaBatch.ProgressSeconds("out_time_ms=2000000"));
        Assert.Null(MediaBatch.ProgressSeconds("out_time=00:00:01.5"));
        Assert.Null(MediaBatch.ProgressSeconds("out_time_us=N/A"));
        var dir = Dir(); var png = Path.Combine(dir, "a.png"); File.WriteAllBytes(png, [1]); var mp3 = Path.Combine(dir, "b.mp3"); File.WriteAllBytes(mp3, [1]);
        MediaJob J(string id) => MediaStudio.Jobs.First(j => j.Id == id);
        Assert.NotNull(MediaBatch.Unsuitable(png, J("mp4")));
        Assert.Null(MediaBatch.Unsuitable(png, J("image")));
        Assert.NotNull(MediaBatch.Unsuitable(mp3, J("gif")));
        Assert.Null(MediaBatch.Unsuitable(mp3, J("mp3")));
        Assert.NotNull(MediaBatch.Unsuitable(Path.Combine(dir, "weg.mp4"), J("mp4")));
        Assert.Equal(5, MediaBatch.OutputSeconds("gif", 30, 0, 10));
        Assert.Equal(2, MediaBatch.OutputSeconds("trim", 30, 1, 2));
        Assert.Null(MediaBatch.OutputSeconds("thumb", 30, 0, 10));
        var o = MediaStudio.OutputPath(mp3, J("mp3"), Path.Combine(dir, "aus"));
        Assert.Equal(Path.Combine(dir, "aus", "b-mp3.mp3"), o);
    }

    [NeedsFact("ffmpeg", DisplayName = "T-0732 Stapel: zehn Dateien in einem Lauf – Fortschritt je Datei, Ergebnisse im Ausgabeordner, Unpassendes übersprungen, Originale unverändert")]
    public async Task Batch_ten_files()
    {
        var dir = Dir(); var outDir = Path.Combine(dir, "ergebnisse");
        var files = Enumerable.Range(1, 9).Select(i => Video(dir, $"clip{i}.mp4", 2)).ToList();
        var img = Path.Combine(dir, "bild.png"); Assert.True(Cli.Run("ffmpeg", ["-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=64x64", "-frames:v", "1", img], dir).Ok);
        files.Add(img);
        var hashes = files.ToDictionary(f => f, f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
        var items = files.Select(f => new MediaBatchItem(f)).ToList();
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, List<double>>();
        await MediaBatch.RunAsync("ffmpeg", items, "mp3", outDir, it => seen.GetOrAdd(it.Input, _ => []).Add(it.Fraction), CancellationToken.None);
        Assert.Equal(9, items.Count(i => i.State == MediaBatchState.Fertig));
        Assert.Equal(MediaBatchState.Uebersprungen, items[^1].State);
        foreach (var it in items.Take(9))
        {
            Assert.StartsWith(outDir, it.Output!);
            Assert.InRange(MediaStudio.Duration("ffmpeg", it.Output!) ?? 0, 1.8, 2.3);
            Assert.Equal(1, it.Fraction);
            Assert.True(seen[it.Input].Count >= 2, "Fortschritt je Datei gemeldet");
        }
        Assert.Empty(Directory.GetFiles(outDir, "*.teil*"));
        foreach (var f in files) Assert.Equal(hashes[f], Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
    }

    [NeedsFact("ffmpeg", DisplayName = "T-0732 Stapel: Abbruch hält mitten in einer Datei an, löscht die halbe Datei, Rest bleibt „abgebrochen“")]
    public async Task Batch_cancel()
    {
        var dir = Dir(); var outDir = Path.Combine(dir, "ergebnisse");
        var files = Enumerable.Range(1, 3).Select(i => Video(dir, $"lang{i}.mp4", 40, "1280x720")).ToList();
        var items = files.Select(f => new MediaBatchItem(f)).ToList();
        using var cts = new CancellationTokenSource();
        var run = MediaBatch.RunAsync("ffmpeg", items, "small", outDir, it => { if (it.State == MediaBatchState.Laeuft && it.Fraction > 0.02) cts.Cancel(); }, cts.Token);
        await run.WaitAsync(TimeSpan.FromMinutes(3));
        Assert.Equal(MediaBatchState.Abgebrochen, items[0].State);
        Assert.All(items.Skip(1), i => Assert.Equal(MediaBatchState.Abgebrochen, i.State));
        Assert.Null(items[0].Output);
        await Task.Delay(300);
        Assert.Empty(Directory.Exists(outDir) ? Directory.GetFiles(outDir) : []); // keine halbe Datei, kein Ergebnis
    }

    /// <summary>Beleg (reports/T-0732): echte Fremddateien (Windows-Systemklänge) im Stapel → MP3, Ton-Kette mit einstellbarem Wert → FLAC, Aufnahme bis Stopp;
    /// jedes Ergebnis wird mit ffprobe (fremdes Programm) geöffnet. Ablage: Umgebungsvariable TRENDO_T0732_BELEG, sonst ein Temp-Ordner.</summary>
    [NeedsFact("ffmpeg", @"C:\Windows\Media\Alarm01.wav",DisplayName = "T-0732 Beleg: Windows-Klänge im Stapel, Ton-Kette nach FLAC, Aufnahme – geöffnet mit ffprobe")]
    public async Task Beleg_echte_dateien()
    {
        var root = Environment.GetEnvironmentVariable("TRENDO_T0732_BELEG") is { Length: > 0 } b ? b : Dir();
        Directory.CreateDirectory(root);
        var log = new System.Text.StringBuilder();
        var probe = Path.Combine(Path.GetDirectoryName(Cli.Find("ffmpeg")!)!, "ffprobe.exe");
        string Probe(string f) { var r = Cli.Run(File.Exists(probe) ? probe : "ffprobe", ["-v", "error", "-show_entries", "format=format_name,duration:stream=codec_name,sample_rate,channels", "-of", "compact", f], null, 30000); Assert.True(r.Ok, r.Err); return r.Out.Trim().Replace("\r", "").Replace("\n", " | "); }
        log.AppendLine("== ffmpeg: " + Cli.Run("ffmpeg", ["-version"]).Out.Split('\n')[0].Trim());
        // 1) Stapel: zehn echte Fremddateien in einem Lauf
        var src = Directory.GetFiles(@"C:\Windows\Media", "*.wav").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        var items = src.Select(f => new MediaBatchItem(f)).ToList(); var ticks = 0;
        var outDir = Path.Combine(root, "stapel");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await MediaBatch.RunAsync("ffmpeg", items, "mp3", outDir, _ => Interlocked.Increment(ref ticks), CancellationToken.None);
        log.AppendLine($"== Stapel „Ton herausholen (MP3)“: {items.Count} Dateien, {sw.Elapsed.TotalSeconds:0.0} s, {ticks} Fortschrittsmeldungen");
        foreach (var it in items)
        {
            Assert.Equal(MediaBatchState.Fertig, it.State);
            log.AppendLine($"{Path.GetFileName(it.Input)} -> {Path.GetFileName(it.Output)} | {it.StateText} | sha256 {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(it.Output!)))[..16]}… | ffprobe: {Probe(it.Output!)}");
        }
        // 2) Abbruch mitten in einer Datei
        var longSrc = Path.Combine(root, "lang.wav");
        Assert.True(Cli.Run("ffmpeg", ["-v", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=330:duration=1200", "-ac", "2", longSrc], root, 120000).Ok);
        using (var cts = new CancellationTokenSource())
        {
            var it = new MediaBatchItem(longSrc); var cancelDir = Path.Combine(root, "abbruch");
            await MediaBatch.RunAsync("ffmpeg", [it], "mp3", cancelDir, x => { if (x.Fraction > 0.05) cts.Cancel(); }, cts.Token);
            await Task.Delay(300);
            var left = Directory.Exists(cancelDir) ? Directory.GetFiles(cancelDir).Length : 0;
            log.AppendLine($"== Abbruch bei {it.Fraction * 100:0} %: Zustand {it.StateText}, Dateien im Ausgabeordner: {left}");
            Assert.Equal(MediaBatchState.Abgebrochen, it.State); Assert.Equal(0, left);
        }
        File.Delete(longSrc);
        // 3) Ton-Kette: Windows-Klang öffnen, „Lauter“ mit eingestelltem Wert 6 dB und „Echo“ mit 0,5 s, als FLAC speichern, mit ffprobe öffnen, wieder einlesen
        var doc = AudioEdit.New(src[0]);
        var before = AudioEdit.Render(doc);
        AudioEdit.Add(doc, AudioEdit.Op("gain", 0, 0, -6)); AudioEdit.Add(doc, AudioEdit.Op("gain", 0, 0, 6)); // Leiser/Lauter je 6 dB
        AudioEdit.Add(doc, AudioEdit.Op("echo", 0, 0, 0.5));
        var after = AudioEdit.Render(doc);
        var wav = Path.Combine(root, "kette.wav"); AudioFile.WriteWav(after, wav, 24);
        var flac = Path.Combine(root, "ton-kette.flac");
        Assert.True(Cli.Run("ffmpeg", ["-v", "error", "-y", "-i", wav, "-c:a", "flac", flac], root).Ok);
        File.Delete(wav);
        var back = Path.Combine(root, "zurueck.wav"); Assert.True(Cli.Run("ffmpeg", ["-v", "error", "-y", "-i", flac, back], root).Ok);
        var re = AudioFile.Read(back); File.Delete(back);
        log.AppendLine($"== Ton-Kette: {Path.GetFileName(src[0])} {before.Seconds:0.000} s → FLAC {re.Seconds:0.000} s, Abweichung {Math.Abs(re.Length - after.Length)} Samples | ffprobe: {Probe(flac)}");
        Assert.True(Math.Abs(re.Length - after.Length) <= 1);
        // 4) Aufnahme bis Stopp (Mikrofon fehlt auf diesem PC: Tonquelle in Echtzeit)
        var micList = Cli.Run("ffmpeg", ["-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"]).Err;
        log.AppendLine("== Mikrofon (DirectShow): " + (micList.Contains("(audio)") ? "vorhanden" : "keines gefunden – Aufnahme mit Tonquelle statt Mikrofon"));
        var recWav = Path.Combine(root, "aufnahme.wav");
        using (var rec = MicRecorder.Start("ffmpeg", ["-re", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=600"], recWav))
        {
            var t = System.Diagnostics.Stopwatch.StartNew(); var levels = new List<double>();
            while (t.Elapsed < TimeSpan.FromSeconds(4)) { await Task.Delay(500); levels.Add(rec.Level); }
            var secs = rec.Seconds; Assert.NotNull(rec.Stop());
            log.AppendLine($"== Aufnahme bis Stopp nach {t.Elapsed.TotalSeconds:0.0} s: gezählt {secs:0.00} s, Pegel {string.Join(", ", levels.Select(l => $"{MicRecorder.Db(l):0.0} dB"))} | ffprobe: {Probe(recWav)}");
        }
        File.WriteAllText(Path.Combine(root, "beleg.log"), log.ToString());
    }
}
