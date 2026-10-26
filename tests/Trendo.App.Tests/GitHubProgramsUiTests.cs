using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Programme von GitHub installieren und einzeln entfernen, Beschreibungen sichtbar (Vorgabe 2026-10-26).</summary>
public class GitHubProgramsUiTests
{
    [AvaloniaFact(DisplayName = "GitHub (UI): Treffer mit Beschreibung, „Zustimmen & installieren“ richtet ein, Programmseite zeigt Starten/Entfernen")]
    public async Task Install_from_search_and_remove()
    {
        var (w, _, root) = Ui.Open();
        var tools = Path.Combine(root, "werkzeuge");
        var zip = Path.Combine(root, "dl.zip");
        Directory.CreateDirectory(root);
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var s = new StreamWriter(z.CreateEntry("b/b.exe").Open())) s.Write("MZ");
        w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([new("a/b", "Ein kleines Werkzeug", "https://github.com/a/b", "https://github.com/a/b.git", 5, "MIT", "C#", DateTimeOffset.Now)]);
        w.GitHubFetchOverride = _ => Task.FromResult<(GitHubApps.Release, string?)>((new GitHubApps.Release("v2.0", "", [new("b-windows-x64.zip", "https://github.com/a/b/releases/download/v2.0/b-windows-x64.zip", new FileInfo(zip).Length, null)]), zip));
        w.Show("oss");
        await w.OssSearch("werkzeug");
        Assert.NotNull(w.Find<Expander>("oss-guide-0"));
        await w.GitHubInstall(new OssRepo("a/b", "Ein kleines Werkzeug", "https://github.com/a/b", "https://github.com/a/b.git", 5, "MIT", "C#", DateTimeOffset.Now));
        Assert.False(w.SetupOverlayOpen);
        Assert.True(File.Exists(Path.Combine(tools, "gh-a-b", "b", "b.exe")));
        w.Show("components");
        Assert.NotNull(w.Find<Button>("btn-gh-start-gh-a-b"));
        Assert.NotNull(w.Find<Expander>("guide-gh-a-b"));
        w.Find<Button>("btn-gh-remove-gh-a-b").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Wirklich entfernen?", w.Find<Button>("btn-gh-remove-gh-a-b").Content);
        w.Find<Button>("btn-gh-remove-gh-a-b").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.False(Directory.Exists(Path.Combine(tools, "gh-a-b")));
        Assert.Empty(GitHubApps.List(tools));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Programme (UI): mitgelieferte Programme haben eine Beschreibung mit Funktionsweise")]
    public void Included_have_guides()
    {
        var (w, _, _) = Ui.Open();
        w.Show("components");
        foreach (var id in new[] { "krita", "inkscape", "obs", "ffmpeg", "ollama" })
            Assert.NotNull(w.Find<Expander>("guide-" + id));
        w.Close();
    }
}
