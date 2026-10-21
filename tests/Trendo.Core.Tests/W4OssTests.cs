using Trendo.Core;
using Xunit;

public class W4_X06_OpenSource
{
    const string Json = """{"items":[{"full_name":"a/b","description":"Ein Editor","html_url":"https://github.com/a/b","clone_url":"https://github.com/a/b.git","stargazers_count":42,"license":{"spdx_id":"MIT"},"language":"C#","pushed_at":"2026-10-01T10:00:00Z"},{"full_name":"c/d","description":null,"html_url":"x","clone_url":"y","stargazers_count":1,"license":null,"language":null}]}""";

    [Fact(DisplayName = "X-06: GitHub-Antwort wird gelesen, Lizenz verständlich erklärt")]
    public void Parse_and_license()
    {
        var r = OpenSource.Parse(Json);
        Assert.Equal(2, r.Count); Assert.Equal("MIT", r[0].LicenseId); Assert.Equal(42, r[0].Stars); Assert.Equal("", r[1].LicenseId);
        Assert.Equal(OpenSource.LicenseKind.Frei, OpenSource.Classify("Apache-2.0"));
        Assert.Equal(OpenSource.LicenseKind.FreiMitWeitergabe, OpenSource.Classify("GPL-3.0"));
        Assert.Equal(OpenSource.LicenseKind.Unklar, OpenSource.Classify(""));
        Assert.Contains("archived%3Afalse", OpenSource.SearchUrl("editor", "C#"));
        Assert.Contains("language%3AC%23", OpenSource.SearchUrl("editor", "C#"));
    }

    [Fact(DisplayName = "X-06: Kopie holen (echtes git, lokales Quell-Repo) mit Herkunftsdatei; unsichere Adressen abgewiesen")]
    public void Fetch_local()
    {
        if (Cli.Find("git") is null) return;
        var t = Path.Combine(Path.GetTempPath(), "trendo-oss-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(t);
        var src = DevStudio.Create(t, "web", "quelle"); Assert.True(DevStudio.GitInit(src).Ok);
        var dst = Path.Combine(t, "ziel"); Directory.CreateDirectory(dst);
        var r = OpenSource.Fetch(new Uri(src).AbsoluteUri, dst, "a/quelle", "MIT");
        Assert.True(r.Ok, r.Err);
        Assert.True(File.Exists(Path.Combine(dst, "a_quelle", "index.html")));
        Assert.Contains("Lizenz: MIT", File.ReadAllText(Path.Combine(dst, "a_quelle.herkunft.txt")));
        Assert.False(OpenSource.Fetch("http://x/y.git", dst, "x/y", "MIT").Ok);
        Assert.False(OpenSource.Fetch("--upload-pack=evil", dst, "x/z", "MIT").Ok);
    }
}
