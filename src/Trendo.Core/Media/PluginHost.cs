using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-06 (07.11.2026): Plugin-Host. Ein Plugin wird nur geladen, wenn (1) die statische Prüfung es für ladbar hält,
/// (2) der Nutzer genau diese Datei (SHA-256) freigegeben hat und (3) es in der abgeschotteten Laufumgebung (<see cref="Sys.Sandbox"/>) läuft.
/// Der Host selbst ist Pedalboard (Spotify, GPL-3.0) als eigenes Programm (trendo_plugin.py) – er lädt VST3, CLAP noch nicht.</summary>
public static class PluginHost
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public sealed record Approval(string Sha256, string Name, string File, string At, string Note);

    /// <summary>Freigaben je Programmdatei (Prüfsumme) unter &lt;Daten&gt;/plugins/freigaben.json – ändert sich die Datei, gilt die Freigabe nicht mehr.</summary>
    public sealed class Approvals(string dataRoot)
    {
        string FilePath => Path.Combine(dataRoot, "plugins", "freigaben.json");
        public List<Approval> All()
        {
            try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<Approval>>(File.ReadAllText(FilePath)) ?? [] : []; }
            catch (JsonException) { return []; }
        }
        void Save(List<Approval> l) { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath, JsonSerializer.Serialize(l, Json)); }
        public bool IsApproved(PluginInspect.Result r) => r.Sha256.Length == 64 && All().Any(a => a.Sha256 == r.Sha256);
        public Approval Approve(PluginInspect.Result r, string note)
        {
            if (!r.Loadable) throw new InvalidOperationException("Nicht ladbar – Freigabe nicht möglich: " + (r.Problem ?? "unbekannt"));
            if (note.Trim().Length < 5) throw new InvalidOperationException("Bitte kurz angeben, woher das Plugin stammt (z. B. „offizielle Seite des Herstellers“).");
            var a = new Approval(r.Sha256, Path.GetFileNameWithoutExtension(r.Path.TrimEnd('\\', '/')), Path.GetFileName(r.Binary), DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv), note.Trim());
            var l = All().Where(x => x.Sha256 != r.Sha256).ToList(); l.Add(a); Save(l); return a;
        }
        public bool Revoke(string sha) { var l = All(); var n = l.RemoveAll(x => x.Sha256 == sha); if (n > 0) Save(l); return n > 0; }
    }

    /// <summary>Darf geladen werden? null = ja, sonst der Grund.</summary>
    public static string? Refuse(PluginInspect.Result r, Approvals ap)
    {
        if (r.Problem is not null) return r.Problem;
        if (r.Format == "CLAP") return "CLAP-Plugins lädt der Host noch nicht (Pedalboard kennt nur VST3) – die Prüfung oben gilt trotzdem.";
        if (!ap.IsApproved(r)) return "Noch nicht freigegeben – erst prüfen, Herkunft angeben und „Freigeben“.";
        return null;
    }

    /// <summary>Grund-Interpreter einer uv-Umgebung („home“ in pyvenv.cfg). Ohne Starter-Programm läuft im Job genau ein Prozess.</summary>
    public static string? BasePython(string venvDir)
    {
        var cfg = Path.Combine(venvDir, "pyvenv.cfg"); if (!File.Exists(cfg)) return null;
        var home = File.ReadAllLines(cfg).Select(l => l.Split('=', 2)).Where(p => p.Length == 2 && p[0].Trim().Equals("home", StringComparison.OrdinalIgnoreCase)).Select(p => p[1].Trim()).FirstOrDefault();
        var exe = home is null ? null : Path.Combine(home, OperatingSystem.IsWindows() ? "python.exe" : "python3");
        return exe is not null && File.Exists(exe) ? exe : null;
    }

    public static string SitePackages(string venvDir) => Path.Combine(venvDir, "Lib", "site-packages");

    /// <summary>„name=wert, name2=wert2“ → Paare (Komma, Strichpunkt oder Zeilenumbruch trennen).</summary>
    public static List<(string Name, string Value)> ParseParams(string text)
    {
        var r = new List<(string, string)>();
        foreach (var part in (text ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2 || kv[0].Trim().Length == 0) throw new ArgumentException($"„{part}“ verstehe ich nicht – Schreibweise name=wert.");
            r.Add((kv[0].Trim(), kv[1].Trim()));
        }
        return r;
    }

    /// <summary>Aufruf des Host-Skripts (läuft mit -I: keine fremden Umgebungsvariablen, kein Benutzer-Site).</summary>
    public static List<string> Args(string script, string sitePackages, string cmd, string plugin, string result, string? input = null, string? output = null,
        string? preset = null, IEnumerable<(string Name, string Value)>? parameters = null, double tail = 0)
    {
        var a = new List<string> { "-I", "-X", "utf8", "-B", script, "--site", sitePackages, cmd, plugin };
        if (cmd == "render") { a.Add(input!); a.Add(output!); }
        a.Add(result);
        if (preset is not null) { a.Add("--preset"); a.Add(preset); }
        foreach (var (n, v) in parameters ?? []) { a.Add("--param"); a.Add(n + "=" + v); }
        if (tail > 0) { a.Add("--nachlauf"); a.Add(tail.ToString("0.###", Inv)); }
        return a;
    }

    public sealed record Param(string Name, string Value, string Label);
    public sealed record Outcome(bool Ok, string Name, string Kind, IReadOnlyList<Param> Params, string? Error, double InPeakDb, double OutPeakDb, double Seconds, double Changed);

    /// <summary>Ergebnis-JSON des Host-Skripts lesen.</summary>
    public static Outcome Parse(string json)
    {
        var j = JsonNode.Parse(json) ?? throw new JsonException("leer");
        double D(string k) => j[k] is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.NaN;
        var ps = (j["parameter"] as JsonArray ?? []).Select(p => new Param((string?)p?["name"] ?? "", (string?)p?["wert"] ?? "", (string?)p?["anzeige"] ?? "")).ToList();
        return new((bool?)j["ok"] ?? false, (string?)j["name"] ?? "", (string?)j["art"] ?? "", ps, (string?)j["fehler"], D("spitze_ein_db"), D("spitze_aus_db"), D("sekunden"), D("veraenderung"));
    }

    /// <summary>Absturzbericht für den Nutzer (Plugin, Prüfsumme, Code, letzte Fehlerzeilen) – die Sitzung in Trendo bleibt unberührt.</summary>
    public static string CrashReport(PluginInspect.Result r, Sys.Sandbox.Result s, string step) =>
        $"# Plugin-Absturzbericht\n\nZeit: {DateTime.Now:dd.MM.yyyy HH:mm:ss}  \nPlugin: `{Path.GetFileName(r.Path.TrimEnd('\\', '/'))}` · SHA-256 `{r.Sha256}`  \nSchritt: {step}  \n" +
        $"Ergebnis: {s.Text} (Code {s.Exit}, {s.Seconds:0.0} s, höchster Speicher {s.PeakMemory >> 20} MB)\n\n" +
        "Das Plugin lief abgeschottet in einem eigenen Prozess. Trendo, dein Projekt und deine Dateien sind nicht betroffen; nichts wurde übernommen.\n\n" +
        "## Letzte Fehlerausgabe\n```\n" + string.Join('\n', s.Err.Split('\n').TakeLast(30)) + "\n```\n";
}
