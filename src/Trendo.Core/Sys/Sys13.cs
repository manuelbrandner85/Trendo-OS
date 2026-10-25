using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Trendo.Core.Sys;

/// <summary>SYS-13 (Katalog freigegeben 21.10.2026): Qualität, Betrieb, Ehrlichkeit.</summary>
public static class Quality
{
    // P15 Leistungsbudget
    public static readonly TimeSpan StartBudget = TimeSpan.FromSeconds(3), PageBudget = TimeSpan.FromMilliseconds(300);
    public sealed record Measure(string What, TimeSpan Took, TimeSpan Budget) { public bool Ok => Took <= Budget; }
    /// <summary>Median, damit ein einzelner Ausreißer (JIT beim ersten Öffnen) nicht entscheidet.</summary>
    public static TimeSpan Median(IEnumerable<TimeSpan> t) { var l = t.OrderBy(x => x).ToList(); return l.Count == 0 ? TimeSpan.Zero : l[l.Count / 2]; }

    // P16 Speicherleck
    public const long LeakLimitBytes = 50L << 20;
    public static bool Leaks(long before, long after) => after - before > LeakLimitBytes;

    // P21 Keine Telemetrie
    static readonly string[] TelemetryMarks = ["ApplicationInsights", "Sentry", "Segment.Analytics", "Mixpanel", "GoogleAnalytics", "google-analytics.com", "TelemetryClient", "AppCenter.Analytics", "Bugsnag", "Raygun", "posthog", "amplitude.com", "datadoghq"];
    /// <summary>Durchsucht Quellcode und Projektdateien nach Telemetrie-Bibliotheken oder -Adressen.</summary>
    public static List<string> TelemetryHits(string srcRoot) =>
        Directory.EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".csproj") || f.EndsWith(".json")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.EndsWith("Sys13.cs"))
            .SelectMany(f => TelemetryMarks.Where(m => File.ReadAllText(f).Contains(m, StringComparison.OrdinalIgnoreCase)).Select(m => $"{Path.GetFileName(f)}: {m}")).ToList();

    // P22 Lizenzliste
    public sealed record Package(string Id, string Version, string License);
    /// <summary>Alle Pakete (auch indirekte) aus project.assets.json; Lizenz aus der .nuspec im NuGet-Ordner.</summary>
    public static List<Package> Packages(IEnumerable<string> assetsFiles, string nugetRoot)
    {
        var set = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in assetsFiles)
        {
            using var d = JsonDocument.Parse(File.ReadAllText(a));
            foreach (var lib in d.RootElement.GetProperty("libraries").EnumerateObject())
                if (lib.Value.GetProperty("type").GetString() == "package") { var p = lib.Name.Split('/'); set[p[0]] = p[1]; }
        }
        return set.Select(kv => new Package(kv.Key, kv.Value, LicenseOf(nugetRoot, kv.Key, kv.Value))).ToList();
    }
    public static string LicenseOf(string nugetRoot, string id, string ver)
    {
        var spec = Path.Combine(nugetRoot, id.ToLowerInvariant(), ver.ToLowerInvariant(), id.ToLowerInvariant() + ".nuspec");
        if (!File.Exists(spec)) return "unbekannt (Paket nicht im Cache)";
        var x = XDocument.Load(spec); var md = x.Root!.Elements().First(e => e.Name.LocalName == "metadata");
        var lic = md.Elements().FirstOrDefault(e => e.Name.LocalName == "license"); var url = md.Elements().FirstOrDefault(e => e.Name.LocalName == "licenseUrl")?.Value;
        if (lic is not null) return lic.Attribute("type")?.Value == "file" ? "Lizenzdatei im Paket: " + lic.Value : lic.Value;
        return url is { Length: > 0 } ? KnownUrl(url) : "unbekannt";
    }
    static string KnownUrl(string u) => u switch
    {
        _ when u.Contains("MIT", StringComparison.OrdinalIgnoreCase) => "MIT (" + u + ")",
        _ when u.Contains("apache", StringComparison.OrdinalIgnoreCase) => "Apache-2.0 (" + u + ")",
        _ => "siehe " + u,
    };
    public static string LicenseMarkdown(IEnumerable<Package> p) => "# NuGet-Bibliotheken und Lizenzen\n\nAutomatisch erzeugt aus project.assets.json (alle Projekte, auch indirekte Pakete).\n\n| Paket | Version | Lizenz |\n|---|---|---|\n" + string.Concat(p.Select(x => $"| {x.Id} | {x.Version} | {x.License.Replace("|", "/")} |\n"));
    public static List<string> MissingInList(IEnumerable<Package> p, string markdown) => p.Where(x => !markdown.Contains($"| {x.Id} | {x.Version} |")).Select(x => x.Id).ToList();

    // P23 Sicherheits-Scan
    public sealed record Vulnerability(string Project, string Package, string Version, string Severity, string Advisory);
    /// <summary>Liest die Ausgabe von „dotnet list package --vulnerable --include-transitive“.</summary>
    public static List<Vulnerability> ParseVulnerable(string output)
    {
        var res = new List<Vulnerability>(); var proj = "";
        foreach (var raw in output.Split('\n'))
        {
            var l = raw.TrimEnd();
            if (Regex.Match(l, @"^Project [`'""]?([^`'""]+)[`'""]?") is { Success: true } pm) proj = pm.Groups[1].Value;
            else if (Regex.Match(l, @"^\s+>\s+(\S+)\s+(\S+)\s+(?:(\S+)\s+)?(Low|Moderate|High|Critical)\s+(\S+)") is { Success: true } m)
                res.Add(new(proj, m.Groups[1].Value, m.Groups[3].Success ? m.Groups[3].Value : m.Groups[2].Value, m.Groups[4].Value, m.Groups[5].Value));
        }
        return res;
    }

    // P20 Migrationen
    public const int SettingsFormat = 3;
    /// <summary>Hebt alte settings.json auf das aktuelle Format: v1 (ohne Nummer) kannte englische Designnamen und Prozent-Skalierung.</summary>
    public static JsonObject MigrateSettings(JsonObject o)
    {
        var v = o["FormatVersion"]?.GetValue<int>() ?? 1;
        if (v < 2)
        {
            if (o["Theme"]?.GetValue<string>() is { } t) o["Theme"] = t.ToLowerInvariant() switch { "dark" => "dunkel", "light" => "hell", "contrast" or "highcontrast" => "kontrast", var x => x };
            if (o["UiScale"] is JsonValue s && s.TryGetValue<double>(out var d) && d > 10) o["UiScale"] = d / 100.0;
            if (o["Wallpaper"]?.GetValue<string>() is "default" or "") o["Wallpaper"] = "bergsee-nacht";
        }
        // T-0224: neuer Desktop – der alte Standard „Abendrot“ wird einmalig zu „Orbit“; eine andere eigene Wahl bleibt
        if (v < 3 && o["Wallpaper"]?.GetValue<string>() is "abendrot") o["Wallpaper"] = "orbit";
        o["FormatVersion"] = SettingsFormat; return o;
    }

    // P25 Rückfall-Version
    /// <summary>Vorige veröffentlichte Version (ohne Vorabversionen, außer es gibt nur solche).</summary>
    public static string? PreviousVersion(IEnumerable<string> released, string current)
    {
        static Version V(string s) => Version.TryParse(Regex.Match(s, @"\d+(\.\d+){1,3}").Value, out var v) ? v : new Version(0, 0);
        var cur = V(current); var older = released.Where(r => V(r) < cur).OrderByDescending(V).ToList();
        return older.FirstOrDefault(r => !r.Contains('-')) ?? older.FirstOrDefault();
    }
    /// <summary>Vor dem Zurückgehen: Einstellungen und Protokoll sichern (ältere Versionen lesen neuere Dateien, unbekannte Felder werden ignoriert).</summary>
    public static string PrepareDowngrade(string dataRoot, string current)
    {
        var d = Path.Combine(dataRoot, "sicherungen", $"vor-rueckfall-{current}-{DateTime.Now:yyyyMMdd-HHmmss}"); Directory.CreateDirectory(d);
        foreach (var f in new[] { UserSettings.PathIn(dataRoot), Path.Combine(dataRoot, "audit", "audit.jsonl") }.Where(File.Exists)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)));
        return d;
    }
    public static string DowngradeUrl(string repo, string version) => $"https://github.com/{repo}/releases/tag/v{version.TrimStart('v')}";
}
