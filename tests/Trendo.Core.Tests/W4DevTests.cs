using Trendo.Core;
using Xunit;

public class W4_Core07_DevStudio
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-dev-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "CORE-07: Vorlagen legen echte Projekte an, vorhandener Ordner wird nie überschrieben")]
    public void Create_templates()
    {
        var root = Tmp();
        foreach (var t in DevStudio.Templates) Assert.True(Directory.Exists(DevStudio.Create(root, t.Id, "Test " + t.Id)));
        Assert.True(File.Exists(Path.Combine(root, "Test-web", "index.html")));
        Assert.Contains("&lt;b&gt;", File.ReadAllText(Path.Combine(DevStudio.Create(root, "web", "<b>"), "index.html")));
        Assert.Throws<IOException>(() => DevStudio.Create(root, "web", "Test web"));
        Assert.Equal("a-b", DevStudio.SafeName("../a b"[3..]));
        Assert.DoesNotContain("..", DevStudio.SafeName("../../etc"));
    }

    [NeedsFact("git", DisplayName = "CORE-07: Git-Stand anlegen, Änderung erkennen, speichern (echtes git)")]
    public void Git_roundtrip()
    {
        if (Cli.Find("git") is null) return;
        var dir = DevStudio.Create(Tmp(), "web", "g");
        Assert.False(DevStudio.Git(dir).IsRepo);
        Assert.True(DevStudio.GitInit(dir).Ok);
        var g = DevStudio.Git(dir);
        Assert.True(g.IsRepo); Assert.Equal("main", g.Branch); Assert.Equal(0, g.Changed);
        File.AppendAllText(Path.Combine(dir, "app.js"), "// neu\n");
        Assert.Equal(1, DevStudio.Git(dir).Changed);
        Assert.False(DevStudio.Commit(dir, " ").Ok);
        Assert.True(DevStudio.Commit(dir, "Neu").Ok);
        Assert.Equal(0, DevStudio.Git(dir).Changed);
    }

    [Fact(DisplayName = "CORE-07: Python- und .NET-Vorlage bestehen ihren eigenen Test (echte Werkzeuge)")]
    public void Templates_build()
    {
        var root = Tmp();
        if (Cli.Find(OperatingSystem.IsWindows() ? "python" : "python3") is not null)
            Assert.True(DevStudio.RunTests(DevStudio.Create(root, "python", "py")).Ok);
        if (Cli.Find("node") is not null)
        {
            var r = DevStudio.RunTests(DevStudio.Create(root, "node", "nd"));
            Assert.True(r.Ok, r.Err + r.Out);
        }
        if (Cli.Find("dotnet") is not null)
        {
            var r = DevStudio.RunTests(DevStudio.Create(root, "dotnet-console", "cs"));
            Assert.True(r.Ok, r.Err + r.Out);
        }
        Assert.False(DevStudio.RunTests(DevStudio.Create(root, "web", "w")).Ok);
    }
}
