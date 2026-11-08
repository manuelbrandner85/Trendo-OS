using System.IO.Compression;
using System.Text.Json.Nodes;

namespace Trendo.Core.Sys;

/// <summary>Alle Studios, Runde 8 – SPEC-12 „System auf Gefahren prüfen“: Dateien und Ordner mit YARA-Regeln auf Merkmale bekannter Schadsoftware prüfen.
/// yara-x (VirusTotal, BSD-3, Rust-Einzelprogramm „yr“) und die geprüften Regeln von ReversingLabs (MIT) – beide aus offiziellen Quellen,
/// fest gewählte Stände mit SHA-256. Es wird nur gelesen: nichts gelöscht, nichts verschoben, nichts hochgeladen.</summary>
public static class YaraScan
{
    public const string Version = "1.22.0";
    public const string ZipUrl = "https://github.com/VirusTotal/yara-x/releases/download/v1.22.0/yara-x-v1.22.0-x86_64-pc-windows-msvc.zip";
    /// <summary>Prüfsumme laut GitHub-Release (Feld „digest“ des Assets).</summary>
    public const string ZipSha256 = "eac36a58699b94d3732a7fe1d8453fe89e0077bb18ac2c99b4c2ee102a431f2e";
    public const string RulesCommit = "e0a0be54aa1e11ccfd6854e4f19e9476f328fd84";
    public const string RulesUrl = "https://codeload.github.com/reversinglabs/reversinglabs-yara-rules/zip/" + RulesCommit;
    /// <summary>Prüfsumme des Regel-Archivs, ermittelt beim echten Download am 08.11.2026 (310 Regeln).</summary>
    public const string RulesSha256 = "8572f9efc8f5b196e8343a9bb74a5da2c403665670a891f1b0ac7d3fa54eefd0";
    public const long MaxFileBytes = 200L * 1024 * 1024;

    public static string Exe(string dir) => Path.Combine(dir, OperatingSystem.IsWindows() ? "yr.exe" : "yr");
    public static string RulesDir(string dir) => Path.Combine(dir, "regeln");
    public static bool Ready(string dir) => File.Exists(Exe(dir)) && Directory.Exists(RulesDir(dir)) && Directory.EnumerateFiles(RulesDir(dir), "*.yara").Any();

    /// <summary>Argumente: NDJSON (eine Zeile je Datei mit Treffern), mit Meta und Tags, Unterordner, Dateien über 200 MB überspringen, 30 s je Datei.</summary>
    public static IReadOnlyList<string> Args(string rulesDir, string target) =>
        ["scan", "-o", "ndjson", "-m", "-g", "--recursive", "--disable-console-logs", "-z", MaxFileBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), "-a", "30", rulesDir, target];

    public sealed record Finding(string File, string Rule, string Kind, string Name, string Description, int Severity);

    static readonly Dictionary<string, string> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ransomware"] = "Erpressersoftware", ["Trojan"] = "Trojaner", ["Backdoor"] = "Hintertür", ["Downloader"] = "Nachlader", ["Infostealer"] = "Datendieb",
        ["Rootkit"] = "Rootkit", ["Virus"] = "Virus", ["Exploit"] = "Angriffswerkzeug (Exploit)", ["Worm"] = "Wurm", ["Hacktool"] = "Hackerwerkzeug", ["Keylogger"] = "Tastaturspion",
        ["Adware"] = "Werbesoftware", ["PUA"] = "möglicherweise unerwünschtes Programm", ["Certificate"] = "verdächtiges Zertifikat", ["Coinminer"] = "Krypto-Schürfer",
    };
    public static string Kind(string type) => Kinds.TryGetValue(type.Trim(), out var de) ? de : type.Trim().Length > 0 ? type.Trim() : "Auffälligkeit";

    /// <summary>NDJSON-Ausgabe von „yr scan“ lesen (Zeilen ohne JSON werden übersprungen).</summary>
    public static List<Finding> Parse(string ndjson)
    {
        var list = new List<Finding>();
        foreach (var line in ndjson.Replace("\r", "").Split('\n'))
        {
            if (!line.TrimStart().StartsWith('{')) continue;
            var j = JsonNode.Parse(line)!; var file = j["path"]?.GetValue<string>() ?? "";
            foreach (var r in j["rules"]?.AsArray() ?? [])
            {
                // yr 1.22 liefert meta als Liste von Paaren [["schlüssel", wert], …] (echter Lauf 08.11.2026); ein Objekt wird ebenso verstanden
                var meta = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
                if (r!["meta"] is JsonObject mo) foreach (var (k, v) in mo) meta[k] = v;
                else if (r["meta"] is JsonArray ma) foreach (var p in ma.OfType<JsonArray>()) if (p.Count == 2 && p[0] is JsonValue kv && kv.TryGetValue<string>(out var key)) meta[key] = p[1];
                string M(string k) => meta.GetValueOrDefault(k) is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
                int Sev() => meta.GetValueOrDefault("tc_detection_factor") is JsonValue v && v.TryGetValue<int>(out var i) ? Math.Clamp(i, 1, 5) : 3;
                list.Add(new(file, r["identifier"]?.GetValue<string>() ?? "?", Kind(M("tc_detection_type").Length > 0 ? M("tc_detection_type") : M("category")),
                    M("tc_detection_name").Length > 0 ? M("tc_detection_name") : M("malware"), M("description"), Sev()));
            }
        }
        return [.. list.OrderByDescending(f => f.Severity).ThenBy(f => f.File, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Verständliche Zusammenfassung.</summary>
    public static string Summary(IReadOnlyList<Finding> f, int seconds) => f.Count == 0
        ? $"Keine bekannten Schadsoftware-Merkmale gefunden ({seconds} s). Das ist kein Freibrief – nur bekannte Muster werden erkannt."
        : $"{f.Select(x => x.File).Distinct(StringComparer.OrdinalIgnoreCase).Count()} Datei(en) mit {f.Count} Treffer(n): " + string.Join("; ", f.Take(5).Select(x => $"{Path.GetFileName(x.File)} → {x.Kind}{(x.Name.Length > 0 ? " " + x.Name : "")}"))
          + (f.Count > 5 ? " …" : "") + ". Nichts wurde verändert – erst ansehen, dann entscheiden (z. B. Quarantäne unter „Sicher prüfen“).";

    /// <summary>Prüfen. Ziel muss existieren; Regeln und Programm müssen eingerichtet sein.</summary>
    public static (List<Finding>? Findings, string Message) Scan(string toolDir, string target, int timeoutMs = 600_000, Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (!File.Exists(target) && !Directory.Exists(target)) return (null, "Diese Datei oder diesen Ordner gibt es nicht.");
        if (run is null && !Ready(toolDir)) return (null, "Die Regel-Prüfung (yara-x) ist noch nicht eingerichtet.");
        var t0 = DateTime.UtcNow;
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(Exe(toolDir), Args(RulesDir(toolDir), target), timeoutMs);
        if (!r.Ok && r.Out.Trim().Length == 0) return (null, "yara-x meldet: " + (r.Err.Length > 0 ? r.Err : "unbekannter Fehler").Trim().Split('\n')[0]);
        try { var f = Parse(r.Out); return (f, Summary(f, (int)(DateTime.UtcNow - t0).TotalSeconds)); }
        catch (System.Text.Json.JsonException) { return (null, "yara-x hat keine lesbare Antwort geliefert."); }
    }

    /// <summary>Regeln aus dem Archiv flach in den Regelordner übernehmen – nur *.yara unter „yara/“, Namen „kategorie-datei.yara“, ohne Pfade aus dem Archiv.</summary>
    public static int ExtractRules(Stream zip, string toolDir)
    {
        var dest = RulesDir(toolDir); Directory.CreateDirectory(dest); var n = 0;
        using var a = new ZipArchive(zip, ZipArchiveMode.Read);
        foreach (var e in a.Entries)
        {
            var parts = e.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || parts[1] != "yara" || !parts[3].EndsWith(".yara", StringComparison.OrdinalIgnoreCase) || e.Length > 2_000_000) continue;
            var safe = string.Concat($"{parts[2]}-{parts[3]}".Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_'));
            e.ExtractToFile(Path.Combine(dest, safe), overwrite: true); n++;
        }
        return n;
    }

    /// <summary>Herunterladen mit Prüfsumme (Programm-Archiv oder Regel-Archiv). Falsche Prüfsumme → verworfen.</summary>
    public static async Task<(byte[]? Data, string Message)> FetchAsync(Func<string, Task<Stream>> fetch, string url, string sha)
    {
        try
        {
            await using var s = await fetch(url); using var ms = new MemoryStream(); await s.CopyToAsync(ms); var data = ms.ToArray();
            var got = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));
            return got == sha ? (data, "ok") : (null, "Download verworfen: Prüfsumme stimmt nicht.");
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return (null, "Download fehlgeschlagen: " + e.Message); } // kein HttpRequestException: der Kern bleibt ohne Netz-Abhängigkeit (QA-019)
    }

    /// <summary>Programm aus dem geprüften Archiv übernehmen (nur yr.exe, keine weiteren Dateien).</summary>
    public static bool ExtractExe(byte[] zip, string toolDir)
    {
        using var a = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var e = a.Entries.FirstOrDefault(x => Path.GetFileName(x.FullName).Equals(Path.GetFileName(Exe(toolDir)), StringComparison.OrdinalIgnoreCase));
        if (e is null) return false;
        Directory.CreateDirectory(toolDir); e.ExtractToFile(Exe(toolDir), overwrite: true); return true;
    }
}
