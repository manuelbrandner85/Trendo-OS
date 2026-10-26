using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Deinstallieren: Frage nach den eigenen Daten, Standard behalten; still = nicht fragen.</summary>
public class UninstallPromptTests
{
    [Theory(DisplayName = "Deinstallieren: stille Deinstallation wird erkannt (dann keine Frage, Daten bleiben)")]
    [InlineData(@"""C:\Users\x\AppData\Local\TrendoOS\Update.exe"" uninstall --silent", true)]
    [InlineData("Update.exe uninstall -s", true)]
    [InlineData(@"""C:\Users\x\AppData\Local\TrendoOS\Update.exe"" --uninstall", false)]
    [InlineData(null, false)]
    public void Silent(string? cmd, bool expected) => Assert.Equal(expected, UninstallPrompt.IsSilent(cmd));

    [Fact(DisplayName = "Deinstallieren: Frage nennt den Datenordner, sagt dass Projekte bleiben, empfiehlt Behalten")]
    public void Question()
    {
        var q = UninstallPrompt.Question(@"C:\Daten\TrendoOS", @"C:\Dokumente\Trendo OS");
        Assert.Contains(@"C:\Daten\TrendoOS", q);
        Assert.Contains("Projekte und Dokumente bleiben", q);
        Assert.Contains("behalten (empfohlen)", q);
        Assert.Contains("projects, projekte", q);
        // Frage (höchstens TimeoutMs ab Hook-Beginn) + Prüfung der stillen Deinstallation (2 s) bleiben unter den 30 s von Velopack
        Assert.True(UninstallPrompt.TimeoutMs + 2_000 + 3_000 < 30_000, "Velopack beendet den Hook nach 30 s");
    }

    [Fact(DisplayName = "Deinstallieren: mitinstallierte Programme werden immer entfernt, selbst installierte bleiben")]
    public void Tools_always_removed()
    {
        var q = UninstallPrompt.Question(@"C:\Daten\TrendoOS", null);
        Assert.Contains("mit Trendo installierten Programme werden in jedem Fall mit entfernt", q);
        Assert.Contains("Blender, Unreal", q);
        var args = UninstallPrompt.ToolsCleanupArguments(@"C:\Users\x\AppData\Local\TrendoWerkzeuge");
        Assert.EndsWith(@"rd /s /q ""C:\Users\x\AppData\Local\TrendoWerkzeuge""", args);
        Assert.Throws<ArgumentException>(() => UninstallPrompt.ToolsCleanupArguments(@"C:\a"" & del x"));
    }

    [Fact(DisplayName = "Deinstallieren: Werkzeugordner nur mit genau diesem Namen und nie eine Laufwerkswurzel")]
    public void Tools_guard()
    {
        Assert.Null(Trendo.Core.DataLocation.RemovableTools(@"C:\"));
        Assert.Null(Trendo.Core.DataLocation.RemovableTools(Path.GetTempPath()));
        var d = Path.Combine(Path.GetTempPath(), "trendo-deinst-" + Guid.NewGuid().ToString("N"), "TrendoWerkzeuge");
        Directory.CreateDirectory(d);
        try { Assert.Equal(Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar), Trendo.Core.DataLocation.RemovableTools(d)!.TrimEnd(Path.DirectorySeparatorChar)); }
        finally { Directory.Delete(Path.GetDirectoryName(d)!, true); }
    }
}
