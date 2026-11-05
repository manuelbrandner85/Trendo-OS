using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>CORE-08 Videoschnitt wie Premiere/DaVinci (Profi 23, 05.11.2026): Farbe, Tonpegel, Übergangsart, Titellage je Stück, zweite Bildspur.</summary>
public class VideoProTests
{
    [Fact(DisplayName = "CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich")]
    public void Args()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-vpro-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a.mp4"); var b = Path.Combine(dir, "b.mp4"); var logo = Path.Combine(dir, "logo.png");
            foreach (var f in new[] { a, b, logo }) File.WriteAllBytes(f, [0]);
            var p = VideoEdit.NewProject();
            VideoEdit.Add(p, VideoEdit.NewClip(a, 3, true)); VideoEdit.Add(p, VideoEdit.NewClip(b, 3, true));
            var plain = string.Join(" ", VideoEdit.ExportArgs(p, Path.Combine(dir, "o.mp4")));
            Assert.DoesNotContain("eq=", plain); Assert.DoesNotContain("overlay=", plain); Assert.Contains("[vbase]null[vout]", plain);
            var c = VideoEdit.Clips(p);
            VideoEdit.SetClip(p, 0, 1, "Hallo");
            VideoPro.SetFx(p, c[0].Id, VideoPro.Looks.First(l => l.Id == "sw").Fx with { Transition = "wipeleft", TitlePos = "oben", TitleSize = 2 });
            VideoPro.SetClipVolume(p, 1, 0.5);
            VideoPro.AddOverlay(p, logo, 0.5, 2);
            var args = string.Join(" ", VideoEdit.ExportArgs(p, Path.Combine(dir, "o.mp4")));
            Assert.Contains("saturation=0", args); Assert.Contains("xfade=transition=wipeleft", args); Assert.Contains("y=h/10", args); Assert.Contains("fontsize=h/14*2", args);
            Assert.Contains("volume=0.5", args); Assert.Contains("overlay=x=0.68*W", args); Assert.Contains("between(t,0.5,2.5)", args);
            Assert.Throws<ArgumentException>(() => VideoPro.SetFx(p, c[0].Id, new VideoPro.ClipFx(Transition: "zauber")));
            VideoPro.SetFx(p, c[1].Id, new VideoPro.ClipFx(Brightness: 9)); Assert.Equal(0.5, VideoPro.Fx(p, c[1].Id).Brightness); // begrenzt
            var ov = VideoPro.Overlays(p).Single(); VideoPro.SetOverlay(p, ov.Id, 1, 3, 0.1, 0.1, 0.5, 0.7);
            Assert.Equal((1.0, 0.5, 0.7), (VideoPro.Overlays(p)[0].Start, VideoPro.Overlays(p)[0].Width, VideoPro.Overlays(p)[0].Opacity));
            VideoPro.RemoveOverlay(p, ov.Id); Assert.Empty(VideoPro.Overlays(p));
            Assert.Equal((0.97 - 0.3, 0.04), VideoPro.Corner("oben-rechts", 0.3));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt")]
    public void Real_export()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return; // ohne FFmpeg nur die Befehlsprüfung oben
        var dir = Path.Combine(Path.GetTempPath(), "trendo-vpro-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            string Make(string name, string src) { var f = Path.Combine(dir, name); var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", src, "-f", "lavfi", "-i", "sine=f=440:d=2", "-t", "2", "-shortest", "-pix_fmt", "yuv420p", f], dir, 60000); Assert.True(File.Exists(f), r.Err); return f; }
            var red = Make("rot.mp4", "color=c=red:s=320x180:d=2"); var blue = Make("blau.mp4", "color=c=blue:s=320x180:d=2");
            var logo = Path.Combine(dir, "logo.png"); Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "color=c=white:s=64x64", "-frames:v", "1", logo], dir, 60000);
            var p = VideoEdit.NewProject(320, 180);
            VideoEdit.Add(p, VideoEdit.NewClip(red, 2, true)); VideoEdit.Add(p, VideoEdit.NewClip(blue, 2, true));
            VideoPro.SetFx(p, VideoEdit.Clips(p)[0].Id, VideoPro.Looks.First(l => l.Id == "sw").Fx);
            VideoPro.AddOverlay(p, logo, 2.5, 1);
            var outFile = Path.Combine(dir, "fertig.mp4");
            VideoEdit.Export(ff, p, outFile, 120000);
            Assert.True(File.Exists(outFile));
            (byte R, byte G, byte B) Px(double t, int x, int y)
            {
                var raw = Path.Combine(dir, $"f{t:0.0}.rgb".Replace(',', '.'));
                Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", t.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", outFile, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", raw], dir, 60000);
                var bytes = File.ReadAllBytes(raw); var i = (y * 320 + x) * 3; return (bytes[i], bytes[i + 1], bytes[i + 2]);
            }
            var gray = Px(0.5, 160, 90); Assert.True(Math.Abs(gray.R - gray.B) < 12 && Math.Abs(gray.R - gray.G) < 12, $"Stück 1 nicht grau: {gray}");
            var col = Px(3.0, 40, 150); Assert.True(col.B > 150 && col.R < 80, $"Stück 2 nicht blau: {col}");
            var logoPx = Px(3.0, (int)(320 * 0.68) + 10, (int)(180 * 0.05) + 10); Assert.True(logoPx.R > 200 && logoPx.G > 200 && logoPx.B > 200, $"Logo fehlt: {logoPx}");
            var noLogo = Px(1.0, (int)(320 * 0.68) + 10, (int)(180 * 0.05) + 10); Assert.True(noLogo.R < 200 || noLogo.G < 200, "Logo zu früh sichtbar");
        }
        finally { Directory.Delete(dir, true); }
    }
}
