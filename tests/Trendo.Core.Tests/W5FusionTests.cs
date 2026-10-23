using Trendo.Core;
using Xunit;

public class W5_X01_Fusion
{
    static string Tmp() => Path.Combine(Path.GetTempPath(), "trendo-fu-" + Guid.NewGuid().ToString("N"));
    static Recipe R(string id) => Fusion.Recipes.First(r => r.Id == id);

    [Fact(DisplayName = "X-01: Idee → Bauplan → Webseite → Schrittliste läuft durch, Protokoll geschrieben")]
    public void Idea_site()
    {
        var c = new FusionContext { WorkDir = Tmp(), Input = new() { ["name"] = "Radkurier", ["problem"] = "Pakete in der Innenstadt kommen zu spät an.", ["solution"] = "Lieferung per Lastenrad in 1 Stunde.", ["audience"] = "Läden in der Stadt", ["price"] = "9,90", ["email"] = "a@b.de" } };
        Assert.Empty(Fusion.Missing(R("idea-site"), c));
        var res = Fusion.Run(R("idea-site"), c);
        Assert.All(res, x => Assert.True(x.Ok, x.Detail)); Assert.Equal(3, res.Count);
        Assert.Contains("Radkurier", File.ReadAllText(c.Out["site"])); Assert.Contains("mailto:a@b.de", File.ReadAllText(c.Out["site"]));
        Assert.Contains("Preis: 9,90", File.ReadAllText(c.Out["blueprint"]) + File.ReadAllText(c.Out["site"]));
        Assert.Contains("OK", File.ReadAllText(Path.Combine(c.WorkDir, "PROTOKOLL.txt")));
    }

    [Fact(DisplayName = "X-01: Fehlende Angaben werden genannt; Fehler hält die Kette an und steht im Protokoll")]
    public void Missing_and_stop()
    {
        var c = new FusionContext { WorkDir = Tmp(), Input = new() { ["file"] = "/nicht/da.mp4" }, Ffmpeg = null };
        Assert.Equal(2, Fusion.Missing(R("video-pack"), c).Count);
        var c2 = new FusionContext { WorkDir = Tmp(), Input = new() { ["file"] = "/nicht/da.mp4" }, Ffmpeg = "ffmpeg" };
        var res = Fusion.Run(R("video-pack"), c2);
        Assert.Single(res); Assert.False(res[0].Ok);
        Assert.Contains("3 Schritte nicht ausgeführt", File.ReadAllText(Path.Combine(c2.WorkDir, "PROTOKOLL.txt")));
        Assert.Contains("1. Medien: In MP4 umwandeln", Fusion.Plan(R("video-pack")));
    }

    [Fact(DisplayName = "X-01: Video-Paket mit echtem FFmpeg")]
    public void Video_ffmpeg()
    {
        var ff = Cli.Find("ffmpeg");
        Assert.SkipUnless(ff is not null, "Übersprungen: FFmpeg ist nicht installiert.");
        var clip = W4_Core08_Media.Sample(out _);
        Assert.NotNull(clip);
        var c = new FusionContext { WorkDir = Tmp(), Input = new() { ["file"] = clip }, Ffmpeg = ff };
        Assert.All(Fusion.Run(R("video-pack"), c), x => Assert.True(x.Ok, x.Detail));
        Assert.Equal(4, c.Out.Count);
    }

    [Fact(DisplayName = "X-01: 3D-Schriftzug mit echtem Blender")]
    public void Logo_blender()
    {
        var bl = Cli.Find("blender");
        Assert.SkipUnless(bl is not null, "Übersprungen: Blender ist nicht installiert.");
        var c = new FusionContext { WorkDir = Tmp(), Input = new() { ["text"] = "Hi" }, Blender = bl };
        Assert.All(Fusion.Run(R("logo-print"), c), x => Assert.True(x.Ok, x.Detail));
        Assert.True(new FileInfo(c.Out["stl"]).Length > 1000);
    }
}
