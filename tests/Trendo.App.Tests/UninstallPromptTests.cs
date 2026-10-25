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

    [Fact(DisplayName = "Deinstallieren: zweite Frage nennt die Zusatzprogramme, ihre Größe, schützt selbst installierte Programme, empfiehlt Behalten")]
    public void ToolsQuestion()
    {
        var q = UninstallPrompt.ToolsQuestion(@"C:\Werkzeuge\TrendoWerkzeuge", ["colmap", "qpdf"], 424L << 20);
        Assert.Contains("colmap, qpdf", q);
        Assert.Contains("424 MB", q);
        Assert.Contains("Blender, Unreal", q);
        Assert.Contains("behalten (empfohlen)", q);
    }
}
