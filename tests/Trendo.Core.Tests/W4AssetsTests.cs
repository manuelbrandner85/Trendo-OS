using Trendo.Core;
using Xunit;

public class W4_X07_Assets
{
    const string List = """{"rock_09":{"type":2,"name":"Rock 09","categories":["rocks","nature"],"tags":["stone"],"download_count":500},"chair":{"type":2,"name":"Chair","categories":["furniture"],"tags":[],"download_count":900}}""";
    const string Files = """{"gltf":{"1k":{"gltf":{"url":"https://dl.polyhaven.org/a/rock_09_1k.gltf","md5":"","size":10,"include":{"textures/rock_diff_1k.jpg":{"url":"https://dl.polyhaven.org/t.jpg","md5":"x","size":5}}}}}}""";

    [Fact(DisplayName = "X-07: Poly-Haven-Katalog suchen (Umlaute/Groß egal), Download-Plan mit Texturen")]
    public void Parse()
    {
        Assert.Equal(["chair", "rock_09"], FreeAssets.ParseList(List, "").Select(h => h.Id));
        Assert.Equal("rock_09", FreeAssets.ParseList(List, "STONE").Single().Id);
        var p = FreeAssets.Plan(Files, "models");
        Assert.Equal(["rock_09_1k.gltf", "textures/rock_diff_1k.jpg"], p.Select(f => f.RelPath));
        var hdri = """{"hdri":{"1k":{"hdr":{"url":"https://dl.polyhaven.org/x/sky_1k.hdr","md5":"","size":1}}}}""";
        Assert.Equal("sky_1k.hdr", FreeAssets.Plan(hdri, "hdris").Single().RelPath);
        Assert.All(FreeAssets.Sources, s => Assert.StartsWith("https://", s.Url));
    }

    [Fact(DisplayName = "X-07: Pfade aus dem Katalog können nicht aus dem Ordner ausbrechen, Prüfsumme wird geprüft")]
    public void Safety()
    {
        var bad = """{"gltf":{"1k":{"gltf":{"url":"https://x/a.gltf","include":{"../../evil.exe":{"url":"https://x/e"}}}}}}""";
        Assert.Throws<InvalidDataException>(() => FreeAssets.Plan(bad, "models"));
        Assert.False(FreeAssets.SafeRel("C:\\x")); Assert.False(FreeAssets.SafeRel("/etc/passwd")); Assert.True(FreeAssets.SafeRel("textures/a.jpg"));
        var f = Path.GetTempFileName(); File.WriteAllText(f, "abc");
        Assert.True(FreeAssets.Md5Ok(f, "900150983cd24fb0d6963f7d28e17f72")); Assert.False(FreeAssets.Md5Ok(f, "00"));
    }
}
