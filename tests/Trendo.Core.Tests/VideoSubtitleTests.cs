using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Videoschnitt X10 (04.11.2026): SRT lesen, Pfad für FFmpeg maskieren, Untertitel einbrennen.</summary>
public class VideoSubtitleTests
{
    const string Srt = "1\r\n00:00:00,500 --> 00:00:02,250\r\nHallo und willkommen\r\n\r\n2\r\n00:00:02,300 --> 00:00:04,000\r\nzweite\r\nZeile\r\n\r\nkaputt\r\n";

    [Fact(DisplayName = "SRT: Zeiten und Text je Untertitel, mehrzeiliger Text zusammengefasst, kaputte Blöcke übersprungen")]
    public void Read_srt()
    {
        var c = VideoExport.ReadSrt(Srt);
        Assert.Equal(2, c.Count);
        Assert.Equal((0.5, 2.25, "Hallo und willkommen"), c[0]);
        Assert.Equal("zweite Zeile", c[1].Text);
    }

    [Fact(DisplayName = "Filterpfad: Windows-Laufwerk und Hochkomma maskiert; Einbrennen nach der Größenänderung, Hochformat mit kleinerer Schrift")]
    public void Filter()
    {
        Assert.Equal("C\\:/Filme/Peter\\'s Film.srt", VideoExport.SubPath(@"C:\Filme\Peter's Film.srt"));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-sub-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            var srt = Path.Combine(dir, "u.srt"); File.WriteAllText(srt, Srt);
            var f = new List<string>();
            var (v, _) = VideoExport.Tail(f, "vout", "aout", 1920, 1080, new VideoExport.Options(VideoExport.Get("hoch"), Subtitles: srt));
            Assert.Equal("vsub", v);
            var i = f.FindIndex(x => x.Contains("subtitles=")); Assert.True(i > f.FindIndex(x => x.Contains("overlay")));
            Assert.Contains("FontSize=13", f[i]);
            f.Clear(); VideoExport.Tail(f, "vout", "aout", 1920, 1080, new VideoExport.Options(VideoExport.Get("projekt"), Subtitles: srt));
            Assert.Contains("FontSize=20", f.Single());
            f.Clear(); VideoExport.Tail(f, "vout", "aout", 1920, 1080, new VideoExport.Options(VideoExport.Get("projekt"), Subtitles: Path.Combine(dir, "fehlt.srt")));
            Assert.Empty(f);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Echter Export mit eingebrannten Untertiteln (FFmpeg libass)")]
    public void Burn_real()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-sub-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a.mp4");
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=black:size=640x360:rate=25:duration=3", "-f", "lavfi", "-i", "sine=frequency=440:duration=3", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", a], dir, 60000).Ok);
            var srt = Path.Combine(dir, "u.srt"); File.WriteAllText(srt, Srt);
            var p = VideoEdit.NewProject(640, 360); VideoEdit.Add(p, VideoEdit.NewClip(a, 3, true));
            var o = VideoEdit.Export(ff, p, Path.Combine(dir, "o.mp4"), o: new VideoExport.Options(VideoExport.Get("projekt"), Subtitles: srt));
            // Bild bei 1 s als Graustufen (PGM): weißer Text auf Schwarz muss helle Pixel haben
            var pgm = Path.Combine(dir, "f.pgm");
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-ss", "1", "-i", o, "-frames:v", "1", "-pix_fmt", "gray", pgm], dir, 60000).Ok);
            var px = File.ReadAllBytes(pgm); var max = px.Skip(px.Length - 640 * 360).Max();
            Assert.True(max > 200, "kein heller Text im Bild: max=" + max);
        }
        finally { Directory.Delete(dir, true); }
    }
}
