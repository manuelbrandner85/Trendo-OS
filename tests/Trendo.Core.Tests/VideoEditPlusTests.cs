using Trendo.Core.Media;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>Videoschnitt-Ausbau: Überblenden, Texteinblendungen, Export im Hintergrund mit Fortschritt und Abbrechen, Abspielbilder.</summary>
public class VideoEditPlusTests
{
    [Fact(DisplayName = "Überblenden verkürzt die Länge und verschiebt die Startzeiten; Aufruf nutzt xfade/acrossfade und drawtext mit maskiertem Text")]
    public void Fade_and_title_args()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-ve2-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            foreach (var n in new[] { "a.mp4", "b.mp4", "c.png" }) File.WriteAllText(Path.Combine(dir, n), "x");
            var p = VideoEdit.NewProject();
            VideoEdit.Add(p, new EditClip("a", Path.Combine(dir, "a.mp4"), 0, 5, 5)); VideoEdit.Add(p, new EditClip("b", Path.Combine(dir, "b.mp4"), 0, 4, 4)); VideoEdit.Add(p, new EditClip("c", Path.Combine(dir, "c.png"), 0, 3, 3600, false));
            VideoEdit.SetClip(p, 0, 1, "Urlaub: 100% schön"); VideoEdit.SetClip(p, 2, 9, ""); // letzte Überblendung wirkt nicht
            Assert.Equal(11, VideoEdit.Duration(p), 3); Assert.Equal([0, 4, 8], VideoEdit.Starts(VideoEdit.Clips(p)));
            var s = string.Join(" ", VideoEdit.ExportArgs(p, Path.Combine(dir, "o.mp4")));
            Assert.Contains("xfade=transition=fade:duration=1:offset=4", s); Assert.Contains("acrossfade=d=1", s); Assert.Contains("concat=n=2:v=1:a=1[vbase][abase]", s); Assert.Contains("[vbase]null[vout]", s); // seit Profi 23: zweite Bildspur liegt zwischen vbase und vout
            if (VideoEdit.FontFile() is not null) Assert.Contains(@"text='Urlaub\: 100\% schön'", s);
            Assert.Contains("-t 11", s);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Mit echtem FFmpeg: Überblenden + Titel im Hintergrund exportiert (Fortschritt steigt bis 100 %), Abbrechen beendet FFmpeg und löscht die halbe Datei")]
    public async Task Export_async_real()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return; // ohne FFmpeg nur der Aufruf-Test oben
        var dir = Path.Combine(Path.GetTempPath(), "trendo-ve2-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string P(string n) => Path.Combine(dir, n);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=25:duration=3", "-f", "lavfi", "-i", "sine=frequency=440:duration=3", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", P("a.mp4")], dir, 60000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "smptebars=size=640x360:rate=25:duration=3", "-f", "lavfi", "-i", "sine=frequency=660:duration=3", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", P("b.mp4")], dir, 60000).Ok);
            var p = VideoEdit.NewProject(640, 360);
            VideoEdit.Add(p, VideoEdit.NewClip(P("a.mp4"), 3, true)); VideoEdit.Add(p, VideoEdit.NewClip(P("b.mp4"), 3, true));
            VideoEdit.SetClip(p, 0, 1, "Hallo Welt");
            var seen = new List<double>(); var prog = new SyncProgress(seen.Add);
            var o = await VideoEdit.ExportAsync(ff, p, P("film.mp4"), prog);
            Assert.InRange(MediaStudio.Duration(ff, o)!.Value, 4.8, 5.3); Assert.True(VideoEdit.HasAudioStream(ff, o));
            Assert.Equal(1, seen[^1]); Assert.True(seen.Count >= 2);
            // Abspielbilder: ein Lauf je Stück
            var frames = VideoEdit.PlayFrames(ff, VideoEdit.Clips(p)[0], P("play-a"), 4);
            Assert.InRange(frames.Count, 10, 14);
            // Abbrechen: langes Stück, sofort abbrechen
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=1280x720:rate=30:duration=120", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", P("lang.mp4")], dir, 300000).Ok);
            var q = VideoEdit.NewProject(1920, 1080); VideoEdit.Add(q, VideoEdit.NewClip(P("lang.mp4"), 120, false));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VideoEdit.ExportAsync(ff, q, P("abbruch.mp4"), null, cts.Token));
            Assert.False(File.Exists(P("abbruch.mp4")));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    sealed class SyncProgress(Action<double> a) : IProgress<double> { public void Report(double v) => a(v); }
}
