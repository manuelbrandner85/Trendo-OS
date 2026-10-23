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

    [Fact(DisplayName = "CORE-07: Git-Stand anlegen, Änderung erkennen, speichern (echtes git)")]
    public void Git_roundtrip()
    {
        Assert.SkipUnless(Cli.Find("git") is not null, "Übersprungen: Git ist nicht installiert.");
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

    [Fact(DisplayName = "CORE-07: Web-Vorlage hat keinen eigenen Testlauf")]
    public void Templates_build()
    {
        var root = Tmp();
        Assert.False(DevStudio.RunTests(DevStudio.Create(root, "web", "w")).Ok);
    }

    [Fact(DisplayName = "CORE-07: Python-Vorlage besteht ihren eigenen Test")]
    public void Python_template()
    {
        var py = OperatingSystem.IsWindows() ? "python" : "python3";
        Assert.SkipUnless(Cli.Find(py) is not null, "Übersprungen: Python ist nicht installiert.");
        Assert.True(DevStudio.RunTests(DevStudio.Create(Tmp(), "python", "py")).Ok);
    }

    [Fact(DisplayName = "CORE-07: Node-Vorlage besteht ihren eigenen Test")]
    public void Node_template()
    {
        Assert.SkipUnless(Cli.Find("node") is not null, "Übersprungen: Node.js ist nicht installiert.");
        var r = DevStudio.RunTests(DevStudio.Create(Tmp(), "node", "nd"));
        Assert.True(r.Ok, r.Err + r.Out);
    }

    [Fact(DisplayName = "CORE-07: .NET-Vorlage besteht ihren eigenen Test")]
    public void Dotnet_template()
    {
        Assert.SkipUnless(Cli.Find("dotnet") is not null, "Übersprungen: das .NET SDK ist nicht installiert.");
        var r = DevStudio.RunTests(DevStudio.Create(Tmp(), "dotnet-console", "cs"));
        Assert.True(r.Ok, r.Err + r.Out);
    }
}
