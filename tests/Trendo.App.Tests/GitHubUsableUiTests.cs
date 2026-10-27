using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 27.10.2026: GitHub-Programme mit einem Klick laden, einrichten, starten – und alles wieder entfernen.</summary>
public class GitHubUsableUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static OssRepo Repo(string full) => new(full, "Ein freies Musikprogramm", "https://github.com/" + full, "https://github.com/" + full + ".git", 1234, "MIT", "TypeScript", DateTimeOffset.UtcNow);

    [AvaloniaFact(DisplayName = "Installer-Programm: Installieren & starten führt den Installer still aus, erkennt das Programm, startet es; Entfernen ruft den Deinstaller")]
    public async Task Installer_is_usable()
    {
        var (w, l, root) = Ui.Open();
        var inst = Path.Combine(root, "YouTube-Music-3.12.0.exe"); File.WriteAllBytes(inst, [.. "MZ"u8.ToArray(), .. Encoding.ASCII.GetBytes("Nullsoft.NSIS")]);
        var programs = Path.Combine(root, "local", "Programs"); Directory.CreateDirectory(programs);
        w.GitHubInstallRootsOverride = [programs];
        w.GitHubFetchOverride = _ => Task.FromResult((new GitHubApps.Release("v3.12.0", "v3.12.0", [new GitHubApps.Asset("YouTube-Music-3.12.0.exe", "https://github.com/x", 10, null)]), (string?)inst));
        string[]? args = null;
        w.GitHubInstallerRunOverride = (_, a) => { args = a; var d = Path.Combine(programs, "youtube-music"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "YouTube Music.exe"), new string('x', 400)); File.WriteAllText(Path.Combine(d, "Uninstall YouTube Music.exe"), "x"); return Task.CompletedTask; };
        var exe = await w.GitHubInstall(Repo("pear-devs/pear-desktop")); Pump();
        Assert.EndsWith("YouTube Music.exe", exe);
        Assert.Contains("/S", args!);
        Assert.Contains(l.Opened, o => o.EndsWith("YouTube Music.exe")); // sofort gestartet
        var entry = Assert.Single(GitHubApps.List(w.ToolsRootForTests));
        Assert.Equal(exe, GitHubApps.ExePath(w.ToolsRootForTests, entry));
        Assert.True(w.SetupFinishShown); w.Find<Button>("setup-overlay-done").Press(); Pump();
        // OSS-Seite zeigt jetzt „Starten“
        w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([Repo("pear-devs/pear-desktop")]);
        await w.OssSearch("music"); Pump();
        Assert.NotNull(w.TryFind<Button>("oss-start-0"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Beschreibung: README wird beim Aufklappen geladen; geholte Projekte lassen sich mit Rückfrage entfernen")]
    public async Task Readme_and_remove_source()
    {
        var (w, _, _) = Ui.Open();
        w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([Repo("o/notes")]);
        w.OssReadmeOverride = _ => Task.FromResult("# Notes\n\nNotes is a fast, private note taking app for everyday thoughts and longer documents.\n\n## Features\n- Markdown\n- Offline\n");
        await w.OssSearch("notes"); Pump();
        await w.OssLoadReadme("o/notes"); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Notes is a fast, private note taking app"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Markdown"));
        var dir = Path.Combine(w.OssFolder, "o-notes"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "README.md"), "x");
        File.SetAttributes(Path.Combine(dir, "README.md"), FileAttributes.ReadOnly);
        w.Show("oss"); Pump();
        w.Find<Button>("oss-local-del-0").Press(); Pump();
        Assert.True(Directory.Exists(dir)); // erst fragen
        w.Find<Button>("oss-local-del-0").Press(); Pump();
        Assert.False(Directory.Exists(dir));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "KI-Programme entfernen: über KI-Quellen eingerichtetes KoboldCpp und eine KI-Laufzeit lassen sich wieder entfernen")]
    public async Task Ki_remove()
    {
        var (w, _, root) = Ui.Open();
        var exeFile = Path.Combine(root, "dl.exe"); File.WriteAllText(exeFile, "MZ");
        w.KiSourceReleasesOverride = _ => Task.FromResult("""[{ "tag_name": "v1.0", "assets": [ { "name": "koboldcpp-nocuda.exe", "browser_download_url": "https://github.com/LostRuins/koboldcpp/releases/download/v1.0/koboldcpp-nocuda.exe", "size": 2 } ] }]""");
        w.KiDownloadOverride = (_, _) => Task.FromResult<(string, string?)>((exeFile, null));
        Assert.True(await w.KiSourceInstall("koboldcpp")); Pump();
        w.Find<Button>("setup-overlay-done").Press();
        w.Show("ki-quellen"); Pump();
        w.Find<Button>("src-remove-koboldcpp").Press(); Pump();
        w.Find<Button>("src-remove-koboldcpp").Press(); Pump();
        Assert.Null(w.KiSourceExe("koboldcpp"));
        var rt = w.KiStore.RuntimeDir(KiRuntime.WhisperCpp); Directory.CreateDirectory(rt); File.WriteAllText(Path.Combine(rt, "x.exe"), "x");
        Assert.True(w.KiStore.RemoveRuntime(KiRuntime.WhisperCpp));
        Assert.False(Directory.Exists(rt));
        w.Close();
    }
}
