using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Befund 27.10.2026: GitHub-Programme wirklich nutzbar machen (Installer erkennen, Programmpaket bevorzugen), Beschreibung aus der README.</summary>
public class GitHubInstallerTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-ghi-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static GitHubApps.Asset A(string n, long size = 100) => new(n, "https://github.com/o/r/releases/download/v1/" + n, size, null);

    [Fact(DisplayName = "GitHub: Programmpaket (*.nsis.7z) wird dem Installer vorgezogen, wenn 7-Zip da ist; ohne 7-Zip die .exe")]
    public void Prefers_payload()
    {
        GitHubApps.Asset[] assets = [A("youtube-music-3.12.0-arm64.nsis.7z"), A("youtube-music-3.12.0-x64.nsis.7z"), A("YouTube-Music-3.12.0.exe"), A("YouTube-Music-Web-Setup-3.12.0.exe")];
        Assert.Equal("youtube-music-3.12.0-x64.nsis.7z", GitHubApps.PickWindows(assets, false, allow7z: true)!.Name);
        Assert.Equal("youtube-music-3.12.0-arm64.nsis.7z", GitHubApps.PickWindows(assets, true, allow7z: true)!.Name);
        Assert.Equal("YouTube-Music-3.12.0.exe", GitHubApps.PickWindows(assets)!.Name);
    }

    [Fact(DisplayName = "GitHub: NSIS-, Inno- und Squirrel-Installer werden erkannt; normale Programme nicht; stille Benutzer-Installation")]
    public void Detects_installers()
    {
        var d = Tmp();
        try
        {
            string F(string name, string marker) { var f = Path.Combine(d, name); File.WriteAllBytes(f, [.. "MZ"u8.ToArray(), .. new byte[100], .. Encoding.ASCII.GetBytes(marker), .. new byte[100]]); return f; }
            Assert.Equal(GitHubApps.InstallerKind.Nsis, GitHubApps.DetectInstaller(F("a.exe", "Nullsoft.NSIS.exehead")));
            Assert.Equal(GitHubApps.InstallerKind.Inno, GitHubApps.DetectInstaller(F("b.exe", "Inno Setup Setup Data")));
            Assert.Equal(GitHubApps.InstallerKind.Squirrel, GitHubApps.DetectInstaller(F("c.exe", "SquirrelSetup")));
            Assert.Equal(GitHubApps.InstallerKind.None, GitHubApps.DetectInstaller(F("d.exe", "ganz normales Programm")));
            Assert.Contains("/currentuser", GitHubApps.SilentArgs(GitHubApps.InstallerKind.Nsis));
            Assert.Contains("/CURRENTUSER", GitHubApps.SilentArgs(GitHubApps.InstallerKind.Inno));
            // Installer ohne 7-Zip: Install meldet „nur Installer“, damit die App ihn still ausführt
            var inst = F("YouTube-Music-3.12.0.exe", "Nullsoft.NSIS.exehead");
            var ex = Assert.Throws<GitHubApps.InstallerOnlyException>(() => GitHubApps.Install(Path.Combine(d, "tools"), "pear-devs/pear-desktop", new("v3", "v3", []), A("YouTube-Music-3.12.0.exe"), inst, "MIT", ""));
            Assert.Equal(GitHubApps.InstallerKind.Nsis, ex.Kind);
            Assert.Empty(GitHubApps.List(Path.Combine(d, "tools")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "GitHub: NSIS-Installer mit 7-Zip auspacken – Programm aus app-64.7z liegt im Werkzeugordner und ist startbar")]
    public void Extracts_nsis()
    {
        var d = Tmp();
        try
        {
            var inst = Path.Combine(d, "App-1.0.exe"); File.WriteAllBytes(inst, [.. "MZ"u8.ToArray(), .. Encoding.ASCII.GetBytes("Nullsoft.NSIS")]);
            // nachgestelltes 7-Zip: Installer → $PLUGINSDIR/app-64.7z, Paket → Programmordner
            void Fake7z(string archive, string target)
            {
                if (archive.EndsWith(".exe")) { Directory.CreateDirectory(Path.Combine(target, "$PLUGINSDIR")); File.WriteAllText(Path.Combine(target, "$PLUGINSDIR", "app-64.7z"), "paket"); File.WriteAllText(Path.Combine(target, "Uninstall App.exe"), "x"); }
                else { File.WriteAllText(Path.Combine(target, "Mein Programm.exe"), new string('x', 500)); File.WriteAllText(Path.Combine(target, "elevate.exe"), "x"); }
            }
            var e = GitHubApps.Install(Path.Combine(d, "tools"), "o/app", new("v1", "v1", []), A("App-1.0.exe"), inst, "MIT", "", null, Fake7z);
            Assert.Equal("Mein Programm.exe", Path.GetFileName(GitHubApps.ExePath(Path.Combine(d, "tools"), e)));
            Assert.True(File.Exists(GitHubApps.ExePath(Path.Combine(d, "tools"), e)));
            // per Installer eingerichtet: absoluter Pfad, entfernbar
            var outside = Path.Combine(d, "Programs", "Tool", "Tool.exe"); Directory.CreateDirectory(Path.GetDirectoryName(outside)!); File.WriteAllText(outside, "x");
            var r = GitHubApps.Register(Path.Combine(d, "tools"), "o/tool", new("v2", "v2", []), A("Tool-Setup.exe"), outside, "ab", "MIT", "");
            Assert.Equal(outside, GitHubApps.ExePath(Path.Combine(d, "tools"), r));
            Assert.True(GitHubApps.Uninstall(Path.Combine(d, "tools"), r.Id));
            Assert.Single(GitHubApps.List(Path.Combine(d, "tools")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "GitHub: README – Einleitung ohne Abzeichen/HTML/Überschriften, Funktionen aus „Features“; Beschreibung nutzt beides")]
    public void Readme()
    {
        const string md = """
        # YouTube Music
        [![Build](https://img.shields.io/x.svg)](https://ci) <img src="logo.png">

        **YouTube Music Desktop App** bundled with custom plugins. Read the [docs](https://x) to learn more about how it works and what you can do with it.

        ## Features
        - **Ad blocker**: block all ads and tracking
        - Downloader for songs and playlists
        * Lyrics in the app

        ## Install
        - npm install
        """;
        var intro = GitHubApps.ReadmeIntro(md);
        Assert.StartsWith("YouTube Music Desktop App bundled", intro);
        Assert.DoesNotContain("shields", intro); Assert.DoesNotContain("<img", intro); Assert.Contains("Read the docs", intro);
        Assert.Equal(["Ad blocker: block all ads and tracking", "Downloader for songs and playlists", "Lyrics in the app"], GitHubApps.ReadmeFeatures(md));
        var g = ProgramGuide.ForGitHub("pear-devs/pear-desktop", "Music app", "MIT", "TypeScript", 30000, "v3.12.0", "app.exe", 100, intro, GitHubApps.ReadmeFeatures(md));
        Assert.Contains("README", g.WhatIs); Assert.Contains("Ad blocker: block all ads and tracking", g.Features);
        Assert.Contains("startet das Programm sofort", string.Join(" ", g.HowItWorks));
        var de = ProgramGuide.ForGitHub("pear-devs/pear-desktop", "Music app", "MIT", null, 1, null, "app.exe", null, intro, null, "Ein Musikprogramm für YouTube Music.");
        Assert.Equal("Ein Musikprogramm für YouTube Music.", de.WhatIs);
    }
}
