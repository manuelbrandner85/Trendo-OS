using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Compat;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Commercial Desktop Edition: Hardware-Kompatibilität, Leistungsprofile, getrennte Linux-Abbilder.</summary>
public class HardwareCompatTests
{
    const long G = 1024L * 1024 * 1024;

    static HardwareReport Pc(int cores, double ramGb, double vramGb, double freeGb = 500, string? gpu = "Testkarte", bool win11 = true) =>
        new("Windows 11", win11 ? 26100 : 19045, win11, "X64", "Test-CPU", cores, (long)(ramGb * G), (long)(freeGb * G), 1000 * G, gpu, (long)(vramGb * G), gpu is null ? null : "1.0", 1920, 1080);

    [Theory]
    [InlineData(2, 4, 0, PerfProfile.Unter)]
    [InlineData(4, 8, 0, PerfProfile.Basic)]
    [InlineData(8, 15.6, 6, PerfProfile.Standard)] // 16-GB-Geräte melden etwas weniger
    [InlineData(16, 32, 12, PerfProfile.Professional)]
    [InlineData(16, 64, 2, PerfProfile.Basic)] // schwache Grafik begrenzt das Profil
    public void Profil_nach_Prozessor_RAM_und_Grafik(int cores, double ram, double vram, PerfProfile expected) =>
        Assert.Equal(expected, PerformanceProfiles.Classify(Pc(cores, ram, vram)));

    [Fact]
    public void Voller_Datentraeger_macht_den_PC_nicht_schwaecher_wird_aber_gewarnt()
    {
        var r = Pc(16, 32, 12, freeGb: 10);
        Assert.Equal(PerfProfile.Professional, PerformanceProfiles.Classify(r));
        Assert.Equal(CompatState.Fail, CompatibilityCheck.Items(r, _ => false, null).Single(i => i.Id == "disk").State);
    }

    [Fact]
    public void Profile_sind_als_vorlaeufig_gekennzeichnet() => Assert.True(PerformanceProfiles.Provisional);

    [Fact]
    public void Studio_Eignung_entfernt_nichts_und_nennt_Alternativen()
    {
        var weak = StudioNeeds.Verdict("CORE-06", PerfProfile.Basic, _ => true);
        Assert.Equal(StudioFit.ZuSchwach, weak.Fit);
        Assert.Contains("startet", weak.Text); // nicht gesperrt, nur gewarnt
        Assert.Contains("Tipp", weak.Text);
        Assert.Equal(StudioFit.BrauchtProgramm, StudioNeeds.Verdict("CORE-05", PerfProfile.Professional, _ => false, id => id == "blender" ? "Blender" : id).Fit);
        Assert.Equal(StudioFit.Eingeschraenkt, StudioNeeds.Verdict("CORE-05", PerfProfile.Standard, _ => true).Fit);
        Assert.Equal(StudioFit.ZuSchwach, StudioNeeds.Verdict("SYS-10", PerfProfile.Unter, _ => false).Fit);
        Assert.Equal(StudioFit.Laeuft, StudioNeeds.Verdict("SYS-10", PerfProfile.Basic, _ => false).Fit);
    }

    [Fact]
    public void Zusatzprogramme_der_Studios_gibt_es_im_Katalog()
    {
        foreach (var n in StudioNeeds.ById.Values)
            foreach (var p in n.Programs)
                Assert.NotNull(DependencyCatalog.Get(p));
    }

    [Fact]
    public void Vorschlaege_passen_zur_Hardware()
    {
        var weak = PerformanceAdvisor.Suggest(Pc(4, 8, 0, gpu: null), PerfProfile.Basic).Select(a => a.Action).ToList();
        Assert.Contains(AdviceAction.SmallKiModel, weak);
        Assert.Contains(AdviceAction.ReducedPreview, weak);
        Assert.Contains(AdviceAction.LimitParallel, weak);
        Assert.Contains(AdviceAction.CpuOnly, weak);
        Assert.Empty(PerformanceAdvisor.Suggest(Pc(16, 32, 12), PerfProfile.Professional));
        Assert.Equal(2, PerformanceAdvisor.ParallelLimit(Pc(4, 8, 0)));
    }

    [Fact]
    public void Systemcheck_meldet_aeltere_Windows_Versionen_und_prueft_Internet_nur_auf_Wunsch()
    {
        var items = CompatibilityCheck.Items(Pc(8, 16, 4, win11: false), _ => false, null);
        Assert.Equal(CompatState.Warn, items.Single(i => i.Id == "os").State);
        Assert.Equal("nicht geprüft", items.Single(i => i.Id == "net").Value);
        Assert.Contains(items, i => i.Id == "driver");
        Assert.Contains(items, i => i.Id == "screen");
        Assert.Contains(items, i => i.Id == "runtime");
    }

    [Fact]
    public void Echte_Messung_auf_diesem_PC_liefert_plausible_Werte()
    {
        var r = HardwareScan.Read(Path.GetTempPath(), 0, 0);
        Assert.True(r.Cores >= 1);
        Assert.True(r.RamBytes > 0);
        Assert.False(string.IsNullOrWhiteSpace(r.Arch));
    }

    [Fact]
    public void Vorab_Skript_nutzt_dieselben_Profilgrenzen_und_aendert_nichts()
    {
        var ps = File.ReadAllText(Path.Combine(Repo(), "installer", "systemcheck.ps1"));
        foreach (var s in PerformanceProfiles.Specs)
            Assert.Matches(new Regex($@"Name = '{s.Name}';\s+Cores = {s.MinCores}; RamGb = {s.MinRamGb};\s+DiskGb = {s.MinDiskFreeGb};\s+VramGb = {s.MinVramGb}"), ps);
        foreach (var bad in new[] { "Set-ItemProperty", "New-Item", "Remove-Item", "Invoke-WebRequest", "Invoke-RestMethod", "Start-Process", "SerialNumber", "$env:USERNAME" })
            Assert.DoesNotContain(bad, ps);
    }

    [Fact]
    public void Linux_Abbild_ist_vom_Windows_Kundenkanal_getrennt()
    {
        Assert.DoesNotContain("/releases/latest/", TrendoImage.LatestBase);
        Assert.Contains("/releases/download/" + TrendoImage.ImageTag + "/", TrendoImage.LatestBase);
        var wf = File.ReadAllText(Path.Combine(Repo(), ".github", "workflows", "trendo-os-abbild.yml"));
        Assert.DoesNotContain("types: [published]", wf);
        Assert.Contains("workflow_dispatch", wf);
        Assert.Contains("--prerelease", wf);
        Assert.Contains("--latest=false", wf);
        Assert.True(Directory.Exists(Path.Combine(Repo(), "linux-image"))); // nichts gelöscht
        Assert.True(Directory.Exists(Path.Combine(Repo(), "installer", "companion")));
    }

    static string Repo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
