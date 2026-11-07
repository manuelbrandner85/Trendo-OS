using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – S3-16 und S4-18 (07.11.2026): SRT, WebVTT, Sprachfassungen, LRC.</summary>
public class TimedTextTests
{
    static readonly TimedText.Cue[] De = [new(0.5, 2.25, "Guten Morgen, Erika."), new(2.5, 4.0, "Wir fahren ans Meer."), new(4.2, 6.0, "Kommst du mit?")];

    [Fact(DisplayName = "S3-16-P: Untertitel als SRT und WebVTT, drei Sprachfassungen mit exakt gleichen Zeiten; SRT → lesen → schreiben unverändert")]
    public void Srt_vtt_languages()
    {
        var srt = TimedText.ToSrt(De);
        Assert.StartsWith("1\n00:00:00,500 --> 00:00:02,250\nGuten Morgen, Erika.", srt);
        Assert.Equal(srt, TimedText.ToSrt(TimedText.FromSrt(srt)));
        var vtt = TimedText.ToVtt(De, "de");
        Assert.StartsWith("WEBVTT\nLanguage: de\n\n1\n00:00:00.500 --> 00:00:02.250\n", vtt);
        var it = TimedText.Translate(De, ["Buongiorno, Erika.", "Andiamo al mare.", "Vieni con noi?"]);
        var en = TimedText.Translate(De, ["Good morning, Erika.", "We're going to the sea.", "Are you coming?"]);
        Assert.True(TimedText.InSync(De, it)); Assert.True(TimedText.InSync(De, en));
        Assert.True(TimedText.InSync(TimedText.FromSrt(TimedText.ToSrt(it)), TimedText.FromSrt(TimedText.ToSrt(en))));
        Assert.Contains("Andiamo al mare.", TimedText.ToVtt(it, "it"));
    }

    [Fact(DisplayName = "S3-16/S4-18-N: Übersetzung mit anderer Zeilenzahl, überlappende, rückwärts laufende oder zu lange Zeiten werden gemeldet, nichts wird geschrieben")]
    public void Rejects_bad_timing()
    {
        Assert.Contains("nicht synchron", Assert.Throws<ArgumentException>(() => TimedText.Translate(De, ["eins", "zwei"])).Message);
        Assert.Contains("beginnt, bevor", string.Join(" ", TimedText.Problems([new(0, 2, "a"), new(1.5, 3, "b")])));
        Assert.Contains("nicht nach dem Beginn", string.Join(" ", TimedText.Problems([new(3, 2, "a")])));
        Assert.Contains("nach dem Ende", string.Join(" ", TimedText.Problems(De, length: 5)));
        Assert.Throws<ArgumentException>(() => TimedText.ToVtt([new(0, 2, "a"), new(1, 3, "b")]));
        Assert.Throws<ArgumentException>(() => TimedText.ToLrc(De, "Lied", length: 5));
        Assert.False(TimedText.InSync(De, [.. De.Select(c => c with { Start = c.Start + 0.1 })]));
    }

    [Fact(DisplayName = "S4-18-P: Liedtext als LRC (Karaoke) – Zeiten auf die Hundertstel, Kopf mit Titel und Sprache; Sprachfassungen mit gleichen Zeiten")]
    public void Lrc()
    {
        var lrc = TimedText.ToLrc(De, "Sommerlied", "de", 6.5);
        Assert.StartsWith("[ti:Sommerlied]\n[la:de]\n[length:00:06]\n[00:00.50]Guten Morgen, Erika.\n", lrc);
        var back = TimedText.ReadLrc(lrc);
        Assert.Equal([0.5, 2.5, 4.2, 6.0], back.Select(x => x.Time));
        var it = TimedText.ReadLrc(TimedText.ToLrc(TimedText.Translate(De, ["Buongiorno, Erika.", "Andiamo al mare.", "Vieni con noi?"]), "Canzone d'estate", "it", 6.5));
        Assert.Equal(back.Select(x => x.Time), it.Select(x => x.Time));
    }

    [Fact(DisplayName = "S3-16 echt mit FFmpeg: WebVTT und SRT werden von FFmpeg gelesen und ins Video eingebettet (drei Untertitelspuren de/it/en)")]
    public void Real_ffmpeg()
    {
        var ff = Cli.Find("ffmpeg"); var fp = Cli.Find("ffprobe"); if (ff is null || fp is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-subs-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var v = Path.Combine(dir, "film.mp4");
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=25:d=7", "-pix_fmt", "yuv420p", v], dir, 60000).Ok);
            var de = Path.Combine(dir, "film.de.vtt"); File.WriteAllText(de, TimedText.ToVtt(De, "de", 7));
            var it = Path.Combine(dir, "film.it.vtt"); File.WriteAllText(it, TimedText.ToVtt(TimedText.Translate(De, ["Buongiorno, Erika.", "Andiamo al mare.", "Vieni con noi?"]), "it", 7));
            var en = Path.Combine(dir, "film.en.srt"); File.WriteAllText(en, TimedText.ToSrt(TimedText.Translate(De, ["Good morning, Erika.", "We're going to the sea.", "Are you coming?"]), 7));
            var mkv = Path.Combine(dir, "film-untertitel.mkv");
            var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-i", v, "-i", de, "-i", it, "-i", en, "-map", "0:v", "-map", "1", "-map", "2", "-map", "3", "-c:v", "copy", "-c:s", "srt",
                "-metadata:s:s:0", "language=deu", "-metadata:s:s:1", "language=ita", "-metadata:s:s:2", "language=eng", mkv], dir, 60000);
            Assert.True(r.Ok && File.Exists(mkv), r.Err);
            var probe = Cli.Run(fp, ["-v", "error", "-select_streams", "s", "-show_entries", "stream=codec_name:stream_tags=language", "-of", "compact=p=0:nk=1", mkv], dir, 30000).Out.Trim();
            Assert.Equal("subrip|deu\nsubrip|ita\nsubrip|eng", probe.Replace("\r", ""));
            var timesFromMkv = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-i", mkv, "-map", "0:s:1", "-f", "srt", "-"], dir, 30000).Out;
            Assert.True(TimedText.InSync(De, TimedText.FromSrt(timesFromMkv))); // italienische Spur hat die Zeiten des Originals
            if (Environment.GetEnvironmentVariable("TRENDO_SUBS_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                foreach (var f in new[] { de, it, en, mkv }) File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true);
                File.WriteAllText(Path.Combine(keep, "lied.de.lrc"), TimedText.ToLrc(De, "Sommerlied", "de", 6.5));
                File.WriteAllText(Path.Combine(keep, "probe.txt"), "ffprobe Untertitelspuren:\n" + probe.Replace("\r", "") + "\n\nitalienische Spur aus dem MKV zurückgelesen:\n" + timesFromMkv.Replace("\r", ""));
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
