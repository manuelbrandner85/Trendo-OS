using System.Text.Json;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.Core.Tests;

public class Sys13_Qualitaet
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-s13-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static string Repo([System.Runtime.CompilerServices.CallerFilePath] string f = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));

    [Fact(DisplayName = "SYS-13-P09: Hash-Kette erkennt Ändern, Löschen und Vertauschen samt Stelle")]
    public void Audit()
    {
        var d = Tmp(); var p = Path.Combine(d, "a.jsonl"); var a = new AuditLog(p);
        for (var i = 0; i < 5; i++) a.Append("t", "x" + i, "d" + i);
        Assert.Null(a.FirstBroken());
        var lines = File.ReadAllLines(p);
        File.WriteAllLines(p, lines.Select((l, i) => i == 2 ? l.Replace("\"d2\"", "\"anders\"") : l)); Assert.Equal(3, a.FirstBroken()); Assert.False(a.Verify());
        File.WriteAllLines(p, lines.Where((_, i) => i != 1)); Assert.Equal(2, a.FirstBroken());
        File.WriteAllLines(p, new[] { lines[0], lines[2], lines[1], lines[3], lines[4] }); Assert.Equal(2, a.FirstBroken());
        File.WriteAllLines(p, lines); Assert.Null(a.FirstBroken());
    }

    [Fact(DisplayName = "SYS-13-P15/P16: Median-Budget und Leck-Grenze 50 MB")]
    public void Budgets()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(20), Quality.Median([TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(900), TimeSpan.FromMilliseconds(20)]));
        Assert.False(Quality.Leaks(100L << 20, 140L << 20)); Assert.True(Quality.Leaks(100L << 20, 151L << 20));
        Assert.True(new Quality.Measure("x", TimeSpan.FromMilliseconds(299), Quality.PageBudget).Ok);
    }

    [Fact(DisplayName = "SYS-13-P20: Alte Einstellungsdatei (v1) wird gelesen und angehoben")]
    public void Migration()
    {
        var d = Tmp(); File.WriteAllText(UserSettings.PathIn(d), "{\"Name\":\"Ana\",\"Theme\":\"Light\",\"UiScale\":115,\"Wallpaper\":\"default\",\"Altfeld\":1}");
        var s = UserSettings.Load(d);
        Assert.Equal(("Ana", "hell", 1.15, "bergsee-nacht", 3), (s.Name, s.Theme, s.UiScale, s.Wallpaper, s.FormatVersion)); // Format 3 seit T-0224
        s.Save(d); Assert.Contains("\"FormatVersion\": 3", File.ReadAllText(UserSettings.PathIn(d)));
        File.WriteAllText(UserSettings.PathIn(d), "{\"Theme\":5}"); Assert.Equal("dunkel", UserSettings.Load(d).Theme);
    }

    [Fact(DisplayName = "SYS-13-P21: Keine Telemetrie-Bibliothek oder -Adresse im Quellcode")]
    public void NoTelemetry()
    {
        Assert.Empty(Quality.TelemetryHits(Path.Combine(Repo(), "src")));
        var d = Tmp(); File.WriteAllText(Path.Combine(d, "x.cs"), "new TelemetryClient();"); Assert.Single(Quality.TelemetryHits(d));
    }

    [Fact(DisplayName = "SYS-13-P22: NuGet-Lizenzliste ist vollständig (alle Pakete aus src/)")]
    public void Licenses()
    {
        var assets = Directory.GetFiles(Path.Combine(Repo(), "src"), "project.assets.json", SearchOption.AllDirectories);
        Assert.NotEmpty(assets);
        var nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var pk = Quality.Packages(assets, nuget); Assert.Contains(pk, p => p.Id == "Avalonia");
        var md = File.ReadAllText(Path.Combine(Repo(), "THIRD_PARTY_NUGET.md"));
        Assert.Empty(Quality.MissingInList(pk, md));
        Assert.Contains("MIT", pk.First(p => p.Id == "Avalonia").License);
    }

    [Fact(DisplayName = "SYS-13-P23: Ausgabe von dotnet list package --vulnerable wird erkannt")]
    public void Vulnerable()
    {
        var o = "Project `Trendo.App` has the following vulnerable packages\n   [net10.0]: \n   Top-level Package      Requested   Resolved   Severity   Advisory URL\n   > Foo.Bar              1.0.0       1.0.0      High       https://github.com/advisories/GHSA-x\n\n   Transitive Package     Resolved   Severity   Advisory URL\n   > Baz                  2.1.0      Moderate   https://github.com/advisories/GHSA-y\n";
        var v = Quality.ParseVulnerable(o);
        Assert.Equal(2, v.Count); Assert.Equal(("Trendo.App", "Foo.Bar", "1.0.0", "High"), (v[0].Project, v[0].Package, v[0].Version, v[0].Severity)); Assert.Equal("2.1.0", v[1].Version);
        Assert.Empty(Quality.ParseVulnerable("The given project `Trendo.Core` has no vulnerable packages given the current sources."));
        Assert.Contains("vuln_scan.sh", File.ReadAllText(Path.Combine(Repo(), "installer", "release.sh")));
    }

    [Fact(DisplayName = "SYS-13-P25: Rückfall – vorige Version, Sicherung, Anleitung vorhanden")]
    public void Downgrade()
    {
        Assert.Equal("0.9.1", Quality.PreviousVersion(["0.9.0", "0.9.1", "0.10.0-beta", "0.10.0", "0.10.1"], "0.10.0"));
        Assert.Equal("v0.8.0", Quality.PreviousVersion(["v0.8.0", "0.9.0-rc1"], "0.9.0"));
        Assert.Null(Quality.PreviousVersion(["1.0.0"], "1.0.0"));
        var d = Tmp(); new UserSettings { Name = "x" }.Save(d); new AuditLog(Path.Combine(d, "audit", "audit.jsonl")).Append("a", "b", "c");
        var b = Quality.PrepareDowngrade(d, "0.10.0"); Assert.True(File.Exists(Path.Combine(b, "settings.json"))); Assert.True(File.Exists(Path.Combine(b, "audit.jsonl")));
        var neu = "{\"Name\":\"n\",\"Theme\":\"hell\",\"KommtErstSpaeter\":{\"a\":1}}"; Assert.Equal("hell", JsonSerializer.Deserialize<UserSettings>(neu)!.Theme);
        var doc = File.ReadAllText(Path.Combine(Repo(), "docs", "RUECKFALL.md")); Assert.Contains("SHA256SUMS", doc); Assert.Contains("Sicherung für Rückfall anlegen", doc);
        Assert.Equal("https://github.com/o/r/releases/tag/v0.9.1", Quality.DowngradeUrl("o/r", "0.9.1"));
    }
}
