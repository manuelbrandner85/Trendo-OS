using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-05 (06.11.2026): Greenscreen und Maske auf der zweiten Bildspur.</summary>
public class GreenscreenTests
{
    [Fact(DisplayName = "S3-05-N: Maske außerhalb des Bildes und falsche Farbe werden abgelehnt; Greenscreen und Maske landen vor dem Skalieren im Filter; alte Einblendungen bleiben unverändert")]
    public void Key_and_mask_args()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-key-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a.mp4"); var g = Path.Combine(dir, "gruen.mp4"); File.WriteAllBytes(a, [0]); File.WriteAllBytes(g, [0]);
            var p = VideoEdit.NewProject(320, 180); VideoEdit.Add(p, VideoEdit.NewClip(a, 2, true));
            var o = VideoPro.AddOverlay(p, g, 0, 2);
            Assert.Equal("", VideoPro.KeyAndMask(VideoPro.Overlays(p)[0])); // ohne Greenscreen wie bisher
            Assert.Throws<ArgumentOutOfRangeException>(() => VideoPro.SetMask(p, o.Id, (0.5, 0, 0.6, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => VideoPro.SetMask(p, o.Id, (-0.1, 0, 0.5, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => VideoPro.SetMask(p, o.Id, (0, 0, 0, 1)));
            Assert.Throws<ArgumentException>(() => VideoPro.SetKey(p, o.Id, "grün"));
            Assert.Null(VideoPro.Overlays(p)[0].Mask); Assert.Equal("", VideoPro.Overlays(p)[0].Key); // nichts verändert
            VideoPro.SetKey(p, o.Id, "#00ff00", 0.2, 0.1); VideoPro.SetMask(p, o.Id, (0, 0, 0.5, 1));
            VideoPro.SetOverlay(p, o.Id, 0, 2, 0, 0, 1, 1); // Lage ändern behält Greenscreen und Maske
            var ov = VideoPro.Overlays(p)[0];
            Assert.Equal(("#00FF00", 0.2, 0.1, (0.0, 0.0, 0.5, 1.0)), (ov.Key, ov.KeySimilarity, ov.KeyBlend, ov.Mask!.Value));
            var args = string.Join(" ", VideoEdit.ExportArgs(p, Path.Combine(dir, "o.mp4")));
            Assert.Contains("format=yuva444p,chromakey=0x00FF00:0.2:0.1,crop=iw*0.5:ih*1:iw*0:ih*0,pad=iw/0.5:ih/1:iw/0.5*0:ih/1*0:color=black@0,scale=", args);
            VideoPro.SetMask(p, o.Id, null); Assert.Null(VideoPro.Overlays(p)[0].Mask);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "S3-05-P echt mit FFmpeg: rotes Hintergrundvideo, darüber Greenscreen-Aufnahme mit blauer Figur – Grün wird durchsichtig, Figur bleibt; mit Maske links verschwindet die Figur rechts")]
    public void Real_key_render()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-key-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            Cli.Result Run(IReadOnlyList<string> a) => Cli.Run(ff, a, dir, 120000);
            string Make(string name, params string[] args) { var f = Path.Combine(dir, name); var r = Run(["-hide_banner", "-loglevel", "error", "-y", .. args, f]); Assert.True(File.Exists(f), r.Err); return f; }
            var bg = Make("hintergrund.mp4", "-f", "lavfi", "-i", "color=c=red:s=320x180:r=25:d=2", "-pix_fmt", "yuv420p");
            // Greenscreen-Aufnahme: grüner Grund, blaue „Figur“ rechts der Mitte
            var gs = Make("greenscreen.mp4", "-f", "lavfi", "-i", "color=c=0x00FF00:s=320x180:r=25:d=2", "-vf", "drawbox=x=200:y=60:w=60:h=60:color=blue:t=fill", "-pix_fmt", "yuv420p", "-crf", "10");
            byte[] Pixel(string video, int x, int y)
            {
                var raw = Path.Combine(dir, $"px-{Guid.NewGuid():N}.rgb");
                var r = Run(["-hide_banner", "-loglevel", "error", "-y", "-ss", "1", "-i", video, "-frames:v", "1", "-vf", $"crop=2:2:{x}:{y},format=rgb24", "-f", "rawvideo", raw]);
                Assert.True(File.Exists(raw) && new FileInfo(raw).Length >= 3, "kein Bild: " + r.Err); return File.ReadAllBytes(raw)[..3];
            }
            string Render(string name, (double, double, double, double)? mask)
            {
                var p = VideoEdit.NewProject(320, 180); p["fps"] = 25; VideoEdit.Add(p, VideoEdit.NewClip(bg, 2, false));
                var o = VideoPro.AddOverlay(p, gs, 0, 2); VideoPro.SetOverlay(p, o.Id, 0, 2, 0, 0, 1, 1);
                VideoPro.SetKey(p, o.Id, "#00FF00", 0.3, 0.05); VideoPro.SetMask(p, o.Id, mask);
                var outp = Path.Combine(dir, name); var r = Run(VideoEdit.ExportArgs(p, outp)); Assert.True(File.Exists(outp), r.Err); return outp;
            }
            bool IsRed(byte[] c) => c[0] > 180 && c[1] < 70 && c[2] < 70; bool IsBlue(byte[] c) => c[2] > 180 && c[0] < 70 && c[1] < 70;
            var full = Render("ergebnis.mp4", null);
            Assert.True(IsRed(Pixel(full, 20, 20)), "Grün nicht durchsichtig: " + string.Join(",", Pixel(full, 20, 20)));
            Assert.True(IsBlue(Pixel(full, 230, 90)), "Figur fehlt: " + string.Join(",", Pixel(full, 230, 90)));
            var masked = Render("ergebnis-maske.mp4", (0, 0, 0.5, 1)); // nur linke Hälfte der Einblendung
            Assert.True(IsRed(Pixel(masked, 230, 90)), "Maske wirkt nicht: " + string.Join(",", Pixel(masked, 230, 90)));
            if (Environment.GetEnvironmentVariable("TRENDO_KEY_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                foreach (var f in Directory.GetFiles(dir, "*.mp4")) File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true);
                foreach (var (v, png) in new[] { (full, "vergleich-ergebnis.png"), (masked, "vergleich-maske.png"), (gs, "vergleich-quelle.png") })
                    Run(["-hide_banner", "-loglevel", "error", "-y", "-ss", "1", "-i", v, "-frames:v", "1", Path.Combine(keep, png)]);
                File.WriteAllText(Path.Combine(keep, "pixel.txt"), $"ergebnis (20,20) = {string.Join(",", Pixel(full, 20, 20))}\nergebnis (230,90) = {string.Join(",", Pixel(full, 230, 90))}\nmaske (230,90) = {string.Join(",", Pixel(masked, 230, 90))}\n");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
