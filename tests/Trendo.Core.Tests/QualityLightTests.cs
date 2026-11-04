using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>A1 Qualitäts-Ampel: aus dem Katalog belegt, schwächstes zuerst; Windows-gebundene Stellen werden erkannt.</summary>
public class QualityLightTests
{
    static ProgramEntry E(string name, int tests, bool surface, bool rea, double? ms = 80, params string[] win) => new()
    { Id = name, Name = name, Type = surface ? ["Zeichenfläche"] : [], Tests = Enumerable.Range(0, tests).Select(i => "t" + i).ToList(), Rea = rea ? "docs/rea.md" : null, WarmMs = ms, WindowsOnly = [.. win],
      Functions = [new("1", "a", "fertig", null), new("2", "b", "offen", null)] };

    [Fact(DisplayName = "Ampel: ohne Arbeitsfläche oder mit < 4 Tests rot; Gründe werden genannt; Reihenfolge schwächstes zuerst")]
    public void Rate_and_rank()
    {
        var form = QualityLight.Rate(E("Formular", 10, false, true));
        Assert.Equal("rot", form.Light); Assert.Contains("keine eigene Arbeitsfläche (noch Formular)", form.Missing);
        var good = QualityLight.Rate(E("Gut", 12, true, true) with { Functions = [new("1", "a", "fertig", null)] });
        Assert.Equal("grün", good.Light); Assert.Empty(good.Missing); Assert.Equal("ja", good.Linux);
        var slow = QualityLight.Rate(E("Langsam", 12, true, true, 400, "Registry"));
        Assert.Equal("gelb", slow.Light); Assert.Equal("eingeschränkt", slow.Linux);
        Assert.Contains(slow.Missing, m => m.Contains("400 ms")); Assert.Contains(slow.Missing, m => m.Contains("Registry"));
        Assert.Contains("1 von 2 Vorbild-Funktionen offen", QualityLight.Rate(E("Halb", 9, true, true)).Missing);
        var ranked = QualityLight.Ranked([E("Gut", 12, true, true), E("Formular", 2, false, false), E("Mittel", 5, true, false)]);
        Assert.Equal(["Formular", "Mittel", "Gut"], ranked.Select(r => r.Entry.Name));
    }

    [Fact(DisplayName = "Trendo OS: Explorer, Registry, PowerShell, Laufwerke werden erkannt; mit eigenem Linux-Weg (xdg-open) kein Mangel")]
    public void Windows_only()
    {
        Assert.Equal(["Windows-Explorer"], QualityLight.WindowsOnly("Process.Start(\"explorer.exe\", dir)"));
        Assert.Contains("Registry", QualityLight.WindowsOnly("Microsoft.Win32.Registry.CurrentUser.OpenSubKey(x)"));
        Assert.Empty(QualityLight.WindowsOnly("Process.Start(\"explorer.exe\", d); if (OperatingSystem.IsLinux()) Process.Start(\"xdg-open\", d);"));
        Assert.Empty(QualityLight.WindowsOnly("var x = 1;"));
    }

    [Fact(DisplayName = "Mitgelieferter Katalog enthält Ampel-Daten (Öffnungszeit gemessen)")]
    public void Catalog_has_data()
    {
        var all = ProgramCatalog.Load();
        Assert.True(all.Count > 40);
        Assert.Contains(all, e => e.WarmMs is > 0);
    }
}
