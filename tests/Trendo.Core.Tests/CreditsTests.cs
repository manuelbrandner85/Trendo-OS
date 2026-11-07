using System.Text;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-17 (07.11.2026): Abspann mit Keyframes, Schrift-Lizenzprüfung.</summary>
public class CreditsTests
{
    static readonly Credits.Section[] Crew = [new("Regie", ["Erika Muster"]), new("Musik", ["Max Beispiel", "Trendo Geräusch-Werkstatt"]), new("Schnitt", ["Erika Muster"])];

    /// <summary>Kleinste Schriftdatei (nur OS/2 und name) mit Lizenztext und Einbettungsrecht.</summary>
    static string FakeFont(string dir, string name, string license, int fsType)
    {
        var lic = Encoding.BigEndianUnicode.GetBytes(license);
        var os2 = new byte[10]; os2[8] = (byte)(fsType >> 8); os2[9] = (byte)fsType;
        var nm = new List<byte> { 0, 0, 0, 1, 0, 18, 0, 3, 0, 1, 0x04, 0x09, 0, 13, (byte)(lic.Length >> 8), (byte)lic.Length, 0, 0 }; nm.AddRange(lic);
        var head = new List<byte> { 0, 1, 0, 0, 0, 2, 0, 32, 0, 1, 0, 0 };
        void Rec(string tag, int off, int len) { head.AddRange(Encoding.ASCII.GetBytes(tag)); head.AddRange([0, 0, 0, 0]); head.AddRange([(byte)(off >> 24), (byte)(off >> 16), (byte)(off >> 8), (byte)off]); head.AddRange([(byte)(len >> 24), (byte)(len >> 16), (byte)(len >> 8), (byte)len]); }
        Rec("OS/2", 44, os2.Length); Rec("name", 44 + os2.Length, nm.Count);
        var f = Path.Combine(dir, name); File.WriteAllBytes(f, [.. head, .. os2, .. nm]); return f;
    }

    [Fact(DisplayName = "S3-17: Keyframes – Lage und Deckkraft linear dazwischen, FFmpeg-Ausdruck stückweise; ungültige Keyframes und leerer Abspann abgelehnt")]
    public void Keyframes()
    {
        var t = Credits.Rolling("Unser Film", Crew, 10, "arial.ttf");
        Assert.Equal((1.0, 0.0), Credits.At(t.Keys, 0));
        Assert.Equal(0.9, Credits.At(t.Keys, 0.5).Y, 6); Assert.Equal(1, Credits.At(t.Keys, 5).Opacity, 6);
        Assert.Equal(0.5, Credits.At(t.Keys, 9.75).Opacity, 6);
        var e = Credits.Expr(t.Keys, k => k.Y);
        Assert.StartsWith("if(lt(t\\,0)\\,1\\,if(lt(t\\,0.5)\\,1+(0.9-1)*(t-0)/0.5", e);
        Assert.Contains("REGIE\nErika Muster\n\nMUSIK\nMax Beispiel", Credits.Text(t));
        Assert.Throws<ArgumentException>(() => Credits.Validate(t with { Keys = [new(0, 1), new(0, 0)] }));
        Assert.Throws<ArgumentException>(() => Credits.Validate(t with { Keys = [new(0, 1), new(12, 0)] }));
        Assert.Throws<ArgumentException>(() => Credits.Validate(t with { Keys = [new(0, 1, 2), new(5, 0)] }));
        Assert.Throws<ArgumentException>(() => Credits.Validate(t with { Sections = [] }));
    }

    [Fact(DisplayName = "S3-17-N: Schrift-Lizenz vor der Freigabe – OFL grün, Einbetten verboten rot, ohne Lizenzangabe gelb, keine Schrift rot")]
    public void Font_license()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-font-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal("gruen", Credits.CheckFont(FakeFont(dir, "frei.ttf", "This Font Software is licensed under the SIL Open Font License, Version 1.1.", 0)).Light);
            var r = Credits.CheckFont(FakeFont(dir, "gesperrt.ttf", "Alle Rechte vorbehalten", 0x0002));
            Assert.Equal("rot", r.Light); Assert.Contains("verbietet", r.Text);
            Assert.Equal("gelb", Credits.CheckFont(FakeFont(dir, "unbekannt.ttf", "", 0)).Light);
            var junk = Path.Combine(dir, "kaputt.ttf"); File.WriteAllText(junk, "keine Schrift");
            Assert.Equal("rot", Credits.CheckFont(junk).Light);
            if (OperatingSystem.IsWindows() && File.Exists(@"C:\Windows\Fonts\arial.ttf")) Assert.Equal("gruen", Credits.CheckFont(@"C:\Windows\Fonts\arial.ttf").Light);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "S3-17-P echt mit FFmpeg: Abspann-Clip 6 s – am Anfang unten, am Ende oben (gemessen), gültiges MP4")]
    public void Real_render()
    {
        var ff = Cli.Find("ffmpeg"); var font = VideoEdit.FontFile(); if (ff is null || font is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-credits-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var t = Credits.Rolling("Unser Film", Crew, 6, font, 640, 360);
            var txt = Path.Combine(dir, "abspann.txt"); File.WriteAllText(txt, Credits.Text(t));
            var mp4 = Path.Combine(dir, "abspann.mp4");
            var r = Cli.Run(ff, Credits.Args(t, txt, mp4), dir, 120000); Assert.True(File.Exists(mp4), r.Err);
            double Bright(double sec, string crop) => ColorScopes.ParseStats(Cli.Run(ff, ["-hide_banner", "-ss", sec.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", mp4, "-frames:v", "1", "-vf", $"crop={crop},signalstats,metadata=mode=print", "-f", "null", "-"], dir, 30000).Err) is { } s ? s.YMax : -1;
            var bottomEarly = Bright(0.8, "640:120:0:240"); var topEarly = Bright(0.8, "640:120:0:0");
            var topLate = Bright(4.8, "640:120:0:0"); var bottomLate = Bright(4.8, "640:120:0:240");
            Assert.True(bottomEarly > 150 && topEarly < 40, $"Anfang: unten {bottomEarly}, oben {topEarly}");
            Assert.True(topLate > 150 && bottomLate < 40, $"Ende: oben {topLate}, unten {bottomLate}");
            if (Environment.GetEnvironmentVariable("TRENDO_CREDITS_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep); File.Copy(mp4, Path.Combine(keep, "abspann.mp4"), true); File.Copy(txt, Path.Combine(keep, "abspann.txt"), true);
                foreach (var (sec, n) in new[] { (0.8, "bild-anfang.png"), (2.5, "bild-mitte.png"), (4.8, "bild-ende.png") })
                    Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", sec.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", mp4, "-frames:v", "1", Path.Combine(keep, n)], dir, 30000);
                File.WriteAllText(Path.Combine(keep, "messung.txt"), $"Helligkeit (YMAX) bei 0,8 s: unten {bottomEarly}, oben {topEarly}\nHelligkeit bei 4,8 s: oben {topLate}, unten {bottomLate}\nSchrift: {Path.GetFileName(font)} → {Credits.CheckFont(font).Text}\n");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
