using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik „hochprofessionell“: Mastering-Argumente (zweistufig, −14 LUFS) und Prüfung auf KI-Floskeln.</summary>
public class MusicProfiTests
{
    const string Ffmpeg = """
        [Parsed_loudnorm_2 @ 000001]
        {
        	"input_i" : "-17.28",
        	"input_tp" : "-3.73",
        	"input_lra" : "12.20",
        	"input_thresh" : "-28.00",
        	"output_i" : "-12.45",
        	"normalization_type" : "dynamic",
        	"target_offset" : "-1.55"
        }
        """;

    [Fact(DisplayName = "Mastern: Messwerte aus FFmpeg lesen, zweiter Lauf linear mit den Messwerten auf −14 LUFS/−1,2 dBTP, Format nach Endung; kaputte Ausgabe = null")]
    public void Master_args()
    {
        var m = SongMaster.ParseMeasure("Stream #0 …\n" + Ffmpeg)!;
        Assert.Equal(("-17.28", "-3.73", "12.20", "-28.00", "-1.55"), (m.I, m.Tp, m.Lra, m.Thresh, m.Offset));
        Assert.Null(SongMaster.ParseMeasure("kein json"));
        Assert.Null(SongMaster.ParseMeasure("""{"input_i" : "-inf", "input_tp" : "-3", "input_lra" : "1", "input_thresh" : "-2", "target_offset" : "0"}"""));
        var measure = SongMaster.MeasureArgs("in.mp3");
        Assert.Contains("loudnorm=I=-14:TP=-1.2:LRA=11:print_format=json", string.Join(" ", measure));
        var mp3 = SongMaster.ApplyArgs("in.mp3", "out.mp3", m); var af = mp3[Array.IndexOf(mp3, "-af") + 1];
        Assert.StartsWith(SongMaster.Pre, af);
        Assert.Contains("measured_I=-17.28:measured_TP=-3.73:measured_LRA=12.20:measured_thresh=-28.00:offset=-1.55:linear=true", af);
        Assert.Equal(["-c:a", "libmp3lame", "-b:a", "320k", "out.mp3"], mp3[^5..]);
        Assert.Contains("pcm_s24le", SongMaster.ApplyArgs("in.wav", "out.wav", m));
        Assert.Contains("flac", SongMaster.ApplyArgs("in.flac", "out.flac", m));
    }

    [Fact(DisplayName = "Y1 YouTube-Link: watch/youtu.be/shorts/music erkannt, andere Seiten nicht; oEmbed lesen; Interpret und Titel ohne „(Official Video)“, sonst Kanal als Interpret")]
    public void YouTube_link()
    {
        Assert.Equal("https://www.youtube.com/watch?v=abcdefghijk", YouTubeLink.Normalize("https://www.youtube.com/watch?v=abcdefghijk&t=42s")!.AbsoluteUri);
        Assert.Equal("https://www.youtube.com/watch?v=abcdefghijk", YouTubeLink.Normalize("youtu.be/abcdefghijk?si=x")!.AbsoluteUri);
        Assert.Equal("https://www.youtube.com/watch?v=abc_def-123", YouTubeLink.Normalize("https://www.youtube.com/shorts/abc_def-123")!.AbsoluteUri);
        Assert.NotNull(YouTubeLink.Normalize("https://music.youtube.com/watch?v=abcdefghijk"));
        Assert.Null(YouTubeLink.Normalize("https://evil.example/watch?v=abcdefghijk"));
        Assert.Null(YouTubeLink.Normalize("https://www.youtube.com/watch?v=kurz"));
        Assert.StartsWith("https://www.youtube.com/oembed?format=json&url=https%3A%2F%2Fwww.youtube.com", YouTubeLink.OEmbedUri(new Uri("https://www.youtube.com/watch?v=abcdefghijk")).AbsoluteUri);
        Assert.Equal(("Erika Muster - Sommerwind (Official Video) (4K Remaster)", "Erika Muster"), YouTubeLink.ParseOEmbed("""{"title":"Erika Muster - Sommerwind (Official Video) (4K Remaster)","author_name":"Erika Muster"}"""));
        Assert.Null(YouTubeLink.ParseOEmbed("Not Found"));
        Assert.Equal(("Erika Muster", "Sommerwind"), YouTubeLink.Split("Erika Muster - Sommerwind (Official Video) (4K Remaster)", "x"));
        Assert.Equal(("Die Musterband", "Abendrot"), YouTubeLink.Split("Die Musterband – Abendrot [Official Music Video]", "x"));
        Assert.Equal(("Die Musterband", "Abendrot"), YouTubeLink.Split("Abendrot (Lyrics)", "Die Musterband - Topic"));
        Assert.Equal(("Die Musterband", "Abendrot (Live in Berlin)"), YouTubeLink.Split("Abendrot (Live in Berlin)", "Die Musterband Official"));
    }

    [Fact(DisplayName = "KI-Floskeln: findet deutsche und englische Phrasen (auch mit Endungen, über Zeilen), keine Treffer in Abschnitts-Marken oder in normalen Wörtern")]
    public void Floskeln()
    {
        var t = "[Strophe]\nIm Neonlicht der Stadt\nmein Herz in\nFlammen, unendliche Nacht\n[Refrain]\nWe were whispering in the shadows";
        Assert.Equal(["neonlicht", "herz in flammen", "unendlich", "in the shadows", "whisper"], LyricsCraft.FindPhrases(t));
        Assert.Empty(LyricsCraft.FindPhrases("[Echo]\nDer Bus kommt um halb acht,\ndie Bäckerin lacht,\nich hol mir zwei Brötchen und den Schlüssel vom Haken"));
        Assert.Empty(LyricsCraft.FindPhrases("Schattenmorellen im Glas, Glutamat im Essen")); // „Schatten“ und „Glut“ nur als ganze Wörter
        Assert.Contains("Neonlicht", LyricsCraft.Rules);
        var p = LyricsCraft.RewritePrompt("[Refrain]\nHerz in Flammen", ["herz in flammen"]);
        Assert.Contains("„herz in flammen“", p); Assert.EndsWith("[Refrain]\nHerz in Flammen", p);
    }
}
