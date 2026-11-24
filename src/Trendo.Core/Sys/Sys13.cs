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

    /// <summary>T-0763: dieselbe Suche für die installierte Version (ohne Quelltext) – in den Paketlisten (*.deps.json) und Dateinamen der Programmbibliotheken.</summary>
    public static List<string> TelemetryHitsInstalled(string appDir) =>
        Directory.Exists(appDir)
            ? Directory.EnumerateFiles(appDir, "*.deps.json", SearchOption.TopDirectoryOnly).SelectMany(f => TelemetryMarks.Where(m => File.ReadAllText(f).Contains(m, StringComparison.OrdinalIgnoreCase)).Select(m => $"{Path.GetFileName(f)}: {m}"))
                .Concat(Directory.EnumerateFiles(appDir, "*.dll", SearchOption.TopDirectoryOnly).SelectMany(f => TelemetryMarks.Where(m => Path.GetFileName(f).Contains(m, StringComparison.OrdinalIgnoreCase)).Select(m => $"{Path.GetFileName(f)}: {m}"))).Distinct().ToList()
            : [];
    /// <summary>Wie viele Paketlisten die Suche lesen konnte (0 = nichts zu prüfen – dann zeigt die Karte kein Ergebnis vor).</summary>
    public static int DepsFiles(string appDir) => Directory.Exists(appDir) ? Directory.EnumerateFiles(appDir, "*.deps.json", SearchOption.TopDirectoryOnly).Count() : 0;

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
    public const int SettingsFormat = 4;
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
        // E9 (T-0616): „Einfacher Modus“ und „Bedienstufe“ sind eine Wahl – wer den einfachen Modus an hatte, bekommt die Stufe „Einfach“; die größeren Knöpfe bleiben
        // E9 (T-0616/T-0620): aus zwei Schaltern wird einer. „Einfacher Modus“ an → Stufe „Einfach“ – außer es war ausdrücklich „Erweitert“ oder „Profi“ gewählt:
        // dann bleibt diese Wahl (keine Funktion geht verloren) und der alte Schalter geht aus, damit beide Angaben zusammenpassen.
        if (v < 4 && o["SimpleMode"] is JsonValue sm && sm.TryGetValue<bool>(out var simple) && simple)
        {
            if (o["Level"] is JsonValue lv && lv.TryGetValue<string>(out var level) && level is "erweitert" or "profi") o["SimpleMode"] = false;
            else o["Level"] = "einfach";
        }
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

/// <summary>T-0763 (SYS-13): „Alles prüfen“ für Endnutzer – je Bereich eine Ampel, ein verständlicher Befund und genau ein nächster Schritt
/// (mit der Seite, auf der er geht). Die Bewertung ist hier rein und prüfbar; die Messwerte liefert die App.</summary>
public static class Checkup
{
    public enum Light { Gruen, Gelb, Rot }
    public sealed record Area(string Id, string Title, Light Light, string Finding, string Next, string? Page);

    public static Light Overall(IEnumerable<Area> areas) { var l = areas.ToList(); return l.Any(a => a.Light == Light.Rot) ? Light.Rot : l.Any(a => a.Light == Light.Gelb) ? Light.Gelb : Light.Gruen; }

    public static string Sentence(IReadOnlyList<Area> areas)
    {
        int rot = areas.Count(a => a.Light == Light.Rot), gelb = areas.Count(a => a.Light == Light.Gelb);
        return rot > 0 ? $"{rot} {(rot == 1 ? "Bereich braucht" : "Bereiche brauchen")} jetzt deine Hilfe" + (gelb > 0 ? $", {gelb} weitere{(gelb == 1 ? "r" : "")} einen Blick." : ".")
            : gelb > 0 ? $"Fast alles in Ordnung – {gelb} {(gelb == 1 ? "Bereich" : "Bereiche")} mit Hinweis."
            : $"Alles in Ordnung: {areas.Count} von {areas.Count} Bereichen grün.";
    }

    /// <summary>Programmdateien gegen das Installationspaket.</summary>
    public static Area Installation(InstallIntegrity.Report r) =>
        !r.Installed ? new("installation", "Programmdateien", Light.Gruen, "Nur für Entwickler: Trendo läuft aus dem Quelltext, es gibt kein Installationspaket zum Vergleichen.", "Nichts zu tun.", null)
        : r.Package is null ? new("installation", "Programmdateien", Light.Gelb, r.Note, "Setup erneut ausführen (Trendo-Setup von GitHub).", "heilung")
        : r.Findings.Count == 0 ? new("installation", "Programmdateien", Light.Gruen, r.Note, "Nichts zu tun.", null)
        : new("installation", "Programmdateien", Light.Rot, $"{r.Findings.Count} Programmdatei(en) fehlen oder sind verändert: {string.Join(", ", r.Findings.Take(3).Select(f => $"{f.File} ({f.Problem})"))}{(r.Findings.Count > 3 ? " …" : "")}",
            "Unter „Einrichten & Reparieren“ aus dem Installationspaket ersetzen.", "heilung");

    /// <summary>Zusatzprogramme der gewählten Pakete.</summary>
    public static Area Programs(int all, IReadOnlyList<string> missing, IReadOnlyList<string> broken) =>
        missing.Count + broken.Count == 0 ? new("programme", "Zusatzprogramme", Light.Gruen, all == 0 ? "Für deine Auswahl braucht Trendo keine Zusatzprogramme." : $"Alle {all} bereit.", "Nichts zu tun.", null)
        : new("programme", "Zusatzprogramme", Light.Gelb, string.Join(" · ", new[] { missing.Count > 0 ? "Fehlt: " + string.Join(", ", missing) : null, broken.Count > 0 ? "Startet nicht: " + string.Join(", ", broken) : null }.Where(x => x is not null)),
            "Unter „Einrichten & Reparieren“ mit einem Klick nachholen (Trendo fragt vorher).", "heilung");

    /// <summary>Datenordner (Ergebnis der Selbstprüfung ohne Reparatur, ohne Protokoll und Speicherplatz – die haben eigene Bereiche).</summary>
    public static Area Data(IReadOnlyList<HealResult> heal)
    {
        var h = heal.Where(x => x.Id is not ("audit" or "disk")).ToList();
        var bad = h.Where(x => x.State is HealState.NeedsYou or HealState.Failed).ToList();
        return bad.Count == 0 ? new("daten", "Deine Daten und Einstellungen", Light.Gruen, "Ordner vorhanden, alle Einstellungsdateien lesbar.", "Nichts zu tun.", null)
            : new("daten", "Deine Daten und Einstellungen", bad.Any(x => x.State == HealState.Failed) ? Light.Rot : Light.Gelb, string.Join(" · ", bad.Select(x => $"{x.Title}: {x.Detail}")),
                "„Jetzt prüfen und reparieren“ unter „Einrichten & Reparieren“.", "heilung");
    }

    /// <summary>Protokoll-Kette: eine veränderte oder gelöschte Zeile wird mit ihrer Nummer genannt.</summary>
    public static Area Audit(long? broken, int entries) =>
        broken is null ? new("protokoll", "Protokoll", Light.Gruen, $"Alle {entries} Einträge unverändert.", "Nichts zu tun.", null)
        : new("protokoll", "Protokoll", Light.Rot, $"Eintrag {broken} wurde verändert oder gelöscht – ab dort ist die Kette unterbrochen.",
            "Nichts löschen: Sicherung des Protokolls anlegen und die Person fragen, die den PC mitbenutzt. Trendo repariert das bewusst nicht.", "qual-leistung");

    public static Area Backup(DateTime? latest, bool complete, DateTime now) =>
        latest is null ? new("sicherung", "Sicherung", Light.Gelb, "Es gibt noch keine Sicherung.", "Unter „Sicherung“ jetzt eine anlegen.", "sicherung")
        : (now - latest.Value).TotalDays > 7 ? new("sicherung", "Sicherung", Light.Gelb, $"Letzte Sicherung am {latest:dd.MM.yyyy} – älter als eine Woche.", "Unter „Sicherung“ jetzt eine anlegen.", "sicherung")
        : !complete ? new("sicherung", "Sicherung", Light.Gelb, $"Letzte Sicherung am {latest:dd.MM.yyyy HH:mm} ist unvollständig.", "Unter „Sicherung“ nachsehen, was fehlt, und neu sichern.", "sicherung")
        : new("sicherung", "Sicherung", Light.Gruen, $"Zuletzt am {latest:dd.MM.yyyy HH:mm}.", "Nichts zu tun.", null);

    public static Area Disk(long freeBytes) =>
        freeBytes < 0 ? new("speicher", "Speicherplatz", Light.Gelb, "Konnte nicht gemessen werden.", "Unter „Speicher & Leistung“ nachsehen.", "ressourcen")
        : freeBytes < 2L << 30 ? new("speicher", "Speicherplatz", Light.Rot, $"Nur noch {freeBytes >> 20:N0} MB frei – Speichern kann fehlschlagen.", "Unter „Speicher & Leistung“ sehen, was viel Platz belegt, und aufräumen.", "ressourcen")
        : freeBytes < 10L << 30 ? new("speicher", "Speicherplatz", Light.Gelb, $"{freeBytes >> 30} GB frei – wird knapp.", "Unter „Speicher & Leistung“ sehen, was viel Platz belegt.", "ressourcen")
        : new("speicher", "Speicherplatz", Light.Gruen, $"{freeBytes >> 30} GB frei.", "Nichts zu tun.", null);

    public static Area Crashes(IReadOnlyList<DateTime> times, DateTime now)
    {
        var recent = times.Count(t => (now - t).TotalDays <= 7);
        return recent == 0 ? new("abstuerze", "Stabilität", Light.Gruen, "Keine Abstürze in den letzten 7 Tagen.", "Nichts zu tun.", null)
            : new("abstuerze", "Stabilität", Light.Gelb, $"{recent} Fehlerbericht(e) in den letzten 7 Tagen (ohne persönliche Angaben gespeichert).", "Trendo aktualisieren; bleibt es, die Fehlerberichte unter „System & Sicherheit“ ansehen.", "updates");
    }

    /// <summary>Für die Zwischenablage oder einen Bericht: eine Zeile je Bereich.</summary>
    public static string Text(IReadOnlyList<Area> areas, DateTime at) =>
        $"Trendo – Alles prüfen ({at:dd.MM.yyyy HH:mm}): {Sentence(areas)}\n" + string.Concat(areas.Select(a => $"[{a.Light switch { Light.Gruen => "grün", Light.Gelb => "gelb", _ => "rot" }}] {a.Title}: {a.Finding} → {a.Next}\n"));
}
