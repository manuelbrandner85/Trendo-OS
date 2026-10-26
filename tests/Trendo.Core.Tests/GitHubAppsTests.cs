using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Programme von GitHub installieren/entfernen, mitgelieferte einzeln entfernen, Beschreibungen (Vorgabe 2026-10-26).</summary>
public class GitHubAppsTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-gh-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    const string ReleaseJson = """
    {"tag_name":"v1.2.0","name":"Version 1.2","assets":[
      {"name":"tool-1.2.0-linux-x64.tar.gz","browser_download_url":"https://github.com/o/tool/releases/download/v1.2.0/a","size":100},
      {"name":"tool-1.2.0-setup.exe","browser_download_url":"https://github.com/o/tool/releases/download/v1.2.0/b","size":100},
      {"name":"tool-1.2.0.msi","browser_download_url":"https://github.com/o/tool/releases/download/v1.2.0/c","size":100},
      {"name":"tool-1.2.0-windows-arm64.zip","browser_download_url":"https://github.com/o/tool/releases/download/v1.2.0/d","size":100},
      {"name":"tool-1.2.0-windows-x64.zip","browser_download_url":"https://github.com/o/tool/releases/download/v1.2.0/e","size":120,"digest":"sha256:ABCDEF"},
      {"name":"tool-1.2.0-src.zip","browser_download_url":"https://github.com/o/tool/releases/download/v1.2.0/f","size":50}]}
    """;

    [Fact(DisplayName = "GitHub: wählt das Windows-ZIP passend zum Prozessor, nie Installer, Quellcode oder Linux")]
    public void Picks_windows_zip()
    {
        var r = GitHubApps.ParseRelease(ReleaseJson);
        Assert.Equal("v1.2.0", r.Tag);
        var x64 = GitHubApps.PickWindows(r.Assets)!;
        Assert.Equal("tool-1.2.0-windows-x64.zip", x64.Name);
        Assert.Equal("abcdef", x64.Sha256);
        Assert.Equal("tool-1.2.0-windows-arm64.zip", GitHubApps.PickWindows(r.Assets, arm64: true)!.Name);
        Assert.Null(GitHubApps.PickWindows([new("x.msi", "https://github.com/a", 1, null), new("x-setup.exe", "https://github.com/b", 1, null), new("x-linux.zip", "https://github.com/c", 1, null)]));
        Assert.Null(GitHubApps.PickWindows([new("x-windows.zip", "https://evil.example/x", 1, null)])); // nur Downloads von github.com
    }

    [Theory(DisplayName = "GitHub: nur gültige Projektnamen")]
    [InlineData("owner/repo", true)]
    [InlineData("a-b/c.d_e", true)]
    [InlineData("owner/../x", false)]
    [InlineData("owner", false)]
    [InlineData("o/r/x", false)]
    [InlineData("o w/r", false)]
    public void Valid_names(string name, bool ok) => Assert.Equal(ok, GitHubApps.ValidRepo(name));

    static string MakeZip(string dir, params (string Name, string Content)[] files)
    {
        var zip = Path.Combine(dir, "paket.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var (n, c) in files) using (var w = new StreamWriter(z.CreateEntry(n).Open())) w.Write(c);
        return zip;
    }

    [Fact(DisplayName = "GitHub: installieren in eigenen Ordner, Liste führen, einzeln wieder entfernen")]
    public void Install_and_uninstall()
    {
        var d = Temp(); var tools = Path.Combine(d, "TrendoWerkzeuge");
        try
        {
            var zip = MakeZip(d, ("tool/tool.exe", "MZ-haupt"), ("tool/unins000.exe", "MZ-weg"), ("tool/readme.txt", "hallo"));
            var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
            var rel = new GitHubApps.Release("v1.2.0", "", []);
            var asset = new GitHubApps.Asset("tool-windows-x64.zip", "https://github.com/o/tool/x", new FileInfo(zip).Length, sha);
            var audit = new AuditLog(Path.Combine(d, "audit.jsonl"));
            var e = GitHubApps.Install(tools, "o/tool", rel, asset, zip, "MIT", "Ein Werkzeug", audit);
            Assert.Equal("gh-o-tool", e.Id);
            Assert.Equal("tool/tool.exe", e.Exe);
            Assert.True(File.Exists(Path.Combine(tools, "gh-o-tool", "tool", "tool.exe")));
            Assert.Single(GitHubApps.List(tools));
            Assert.Contains("github.install", File.ReadAllText(Path.Combine(d, "audit.jsonl")));
            Assert.True(GitHubApps.Uninstall(tools, "gh-o-tool", audit));
            Assert.False(Directory.Exists(Path.Combine(tools, "gh-o-tool")));
            Assert.Empty(GitHubApps.List(tools));
            Assert.False(GitHubApps.Uninstall(tools, "../x"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "GitHub: falsche Prüfsumme und Zip-Slip werden abgelehnt, nichts bleibt liegen")]
    public void Rejects_bad_files()
    {
        var d = Temp(); var tools = Path.Combine(d, "TrendoWerkzeuge");
        try
        {
            var zip = MakeZip(d, ("tool.exe", "MZ"));
            var bad = new GitHubApps.Asset("tool-windows.zip", "https://github.com/o/t/x", 1, new string('0', 64));
            Assert.Throws<InvalidOperationException>(() => GitHubApps.Install(tools, "o/tool", new("v1", "", []), bad, zip, "MIT", ""));
            Assert.False(Directory.Exists(Path.Combine(tools, "gh-o-tool")));
            var slip = Path.Combine(d, "slip.zip");
            using (var z = ZipFile.Open(slip, ZipArchiveMode.Create)) using (var w = new StreamWriter(z.CreateEntry("../../boese.exe").Open())) w.Write("MZ");
            var ok = new GitHubApps.Asset("tool-windows.zip", "https://github.com/o/t/x", 1, null);
            Assert.Throws<InvalidOperationException>(() => GitHubApps.Install(tools, "o/tool", new("v1", "", []), ok, slip, "MIT", ""));
            Assert.False(File.Exists(Path.Combine(d, "boese.exe")));
            Assert.Empty(GitHubApps.List(tools));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Vollpaket: mitgeliefertes Programm einzeln entfernen, unbekannte Namen abgelehnt")]
    public void Vollpaket_uninstall_single()
    {
        var d = Temp();
        try
        {
            Directory.CreateDirectory(Path.Combine(d, "krita", "bin")); File.WriteAllText(Path.Combine(d, "krita", "bin", "krita.exe"), "MZ");
            File.SetAttributes(Path.Combine(d, "krita", "bin", "krita.exe"), FileAttributes.ReadOnly);
            Directory.CreateDirectory(Path.Combine(d, "fremd"));
            Assert.True(Vollpaket.Uninstall(d, "krita"));
            Assert.False(Directory.Exists(Path.Combine(d, "krita")));
            Assert.False(Vollpaket.Uninstall(d, "fremd"));
            Assert.False(Vollpaket.Uninstall(d, ".."));
            Assert.True(Directory.Exists(Path.Combine(d, "fremd")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Beschreibungen: jedes mitgelieferte Programm hat Was/Wofür/So geht's/Funktionen/Herkunft/Entfernen")]
    public void Every_program_has_guide()
    {
        foreach (var pkg in Vollpaket.Included.Values.Select(i => i.Package).Distinct())
        {
            Assert.True(ProgramGuide.Included.TryGetValue(pkg, out var g), "Beschreibung fehlt: " + pkg);
            Assert.True(g!.WhatIs.Length > 20 && g.UseFor.Length > 20 && g.HowItWorks.Count >= 2 && g.Features.Count >= 2, pkg);
            Assert.Contains("Offizielle Quelle", g.Origin);
            Assert.Contains("Entfernen", g.Remove);
            Assert.Contains("So funktioniert es:", g.ToText());
        }
        var gh = ProgramGuide.ForGitHub("o/tool", "Ein Werkzeug zum Testen", "MIT", "C#", 1234, "v1.2.0", "tool-windows-x64.zip", 5L << 20);
        Assert.Contains("tool-windows-x64.zip", string.Join(" ", gh.HowItWorks));
        Assert.Contains("5 MB", gh.Origin);
        var none = ProgramGuide.ForGitHub("o/lib", null, "NOASSERTION", null, 3, null, null, null);
        Assert.Contains("kein fertiges Windows-Programm", string.Join(" ", none.HowItWorks));
    }
}
