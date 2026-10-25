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
        Assert.True(UninstallPrompt.TimeoutMs < 30_000, "Velopack beendet den Hook nach 30 s");
    }
}
