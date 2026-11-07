using System.IO.Compression;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Vorschlag 12: Ein-Klick-Veröffentlichung – Unreals Paketierer (BuildCookRun) und ZIP für Windows.</summary>
public class UnrealPackageTests
{
    [Fact(DisplayName = "Unreal-Paket: offizieller Paketierer mit Shipping für Windows, Erfolg/Grund aus dem Protokoll, ZIP aus dem Windows-Ordner")]
    public void Package_steps()
    {
        Assert.EndsWith(Path.Combine("Engine", "Build", "BatchFiles", "RunUAT.bat"), UnrealPackage.RunUat("C:/UE"));
        var a = UnrealPackage.Args("C:/P/Spiel.uproject", "C:/Out");
        Assert.Equal("BuildCookRun", a[0]);
        Assert.Contains("-project=C:/P/Spiel.uproject", a); Assert.Contains("-platform=Win64", a); Assert.Contains("-clientconfig=Shipping", a);
        Assert.Contains("-cook", a); Assert.Contains("-pak", a); Assert.Contains("-archivedirectory=C:/Out", a); Assert.Contains("-unattended", a);
        Assert.True(UnrealPackage.Ok("…\nBUILD SUCCESSFUL\nAutomationTool exiting with ExitCode=0 (Success)"));
        Assert.False(UnrealPackage.Ok("BUILD FAILED"));
        Assert.Contains("Missing asset", UnrealPackage.Reason("x\nError: Missing asset /Game/X\nBUILD FAILED"));
        Assert.Contains("paket.txt", UnrealPackage.Reason(""));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-paket-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(UnrealPackage.Exe(dir));
            Assert.Throws<InvalidOperationException>(() => UnrealPackage.Zip(dir, Path.Combine(dir, "x.zip")));
            var win = Path.Combine(dir, "Windows"); Directory.CreateDirectory(Path.Combine(win, "Spiel", "Content", "Paks"));
            File.WriteAllBytes(Path.Combine(win, "Spiel.exe"), [1, 2]); File.WriteAllBytes(Path.Combine(win, "Spiel", "Content", "Paks", "Spiel-Windows.pak"), [3]);
            Assert.EndsWith("Spiel.exe", UnrealPackage.Exe(dir));
            var zip = UnrealPackage.Zip(dir, Path.Combine(dir, "aus", "spiel-windows.zip"));
            using var z = ZipFile.OpenRead(zip);
            Assert.Contains(z.Entries, e => e.FullName == "Spiel.exe"); Assert.Contains(z.Entries, e => e.FullName.EndsWith("Spiel-Windows.pak"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
