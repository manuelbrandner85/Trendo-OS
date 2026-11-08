using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 7 – SPEC-08 „Kleidung &amp; Produkte entwerfen“: echte Schnittmuster nach Körpermaßen mit FreeSewing 4 (MIT,
/// aktiv auf Codeberg). Pakete nach Klick per npm (feste Fassung, ohne Installationsskripte), Lauf als eigener Node-Prozess mit Trendos Startskript.
/// Fehlende Maße kommen aus der FreeSewing-Standardgröße mit dem nächstliegenden Brustumfang – Trendo sagt genau, welche.</summary>
public static class Sewing
{
    public const string Version = "4.10.2", RunnerName = "trendo-schnitt.mjs";

    /// <summary>Angebotene Schnitte: FreeSewing-Paket, deutscher Name.</summary>
    public static readonly (string Id, string Label)[] Designs =
    [
        ("teagan", "T-Shirt"), ("aaron", "Tanktop"), ("simon", "Hemd mit Knopfleiste"), ("sven", "Pullover"), ("hugo", "Kapuzenpulli (Raglan)"),
        ("titan", "Hose – Grundschnitt"), ("charlie", "Chino-Hose"), ("waralee", "Wickelhose"), ("penelope", "Bleistiftrock"), ("sandy", "Tellerrock"), ("bella", "Oberteil – Grundschnitt (Damen)"),
    ];

    /// <summary>FreeSewing-Maße auf Deutsch.</summary>
    public static readonly IReadOnlyDictionary<string, string> German = new Dictionary<string, string>
    {
        ["ankle"] = "Knöchelumfang", ["biceps"] = "Oberarmumfang", ["bustFront"] = "Brust vorn", ["bustPointToUnderbust"] = "Brustpunkt bis Unterbrust", ["bustSpan"] = "Brustpunktabstand",
        ["chest"] = "Brustumfang", ["crossSeam"] = "Schrittnaht", ["crossSeamFront"] = "Schrittnaht vorn", ["crotchDepth"] = "Schritttiefe", ["heel"] = "Fersenumfang", ["head"] = "Kopfumfang",
        ["highBust"] = "obere Brustweite", ["highBustFront"] = "obere Brustweite vorn", ["hips"] = "Hüftumfang", ["hpsToBust"] = "Schulterpunkt bis Brustpunkt", ["hpsToWaistBack"] = "Rückenlänge",
        ["hpsToWaistFront"] = "Vorderlänge", ["inseam"] = "Innenbeinlänge", ["knee"] = "Knieumfang", ["neck"] = "Halsumfang", ["seat"] = "Gesäßumfang", ["seatBack"] = "Gesäß hinten",
        ["shoulderSlope"] = "Schulterneigung (Grad)", ["shoulderToElbow"] = "Schulter bis Ellbogen", ["shoulderToShoulder"] = "Schulterbreite", ["shoulderToWrist"] = "Armlänge", ["underbust"] = "Unterbrustumfang",
        ["upperLeg"] = "Oberschenkelumfang", ["waist"] = "Taillenumfang", ["waistBack"] = "Taille hinten", ["waistToArmpit"] = "Taille bis Achsel", ["waistToFloor"] = "Taille bis Boden",
        ["waistToHips"] = "Taille bis Hüfte", ["waistToKnee"] = "Taille bis Knie", ["waistToSeat"] = "Taille bis Gesäß", ["waistToUnderbust"] = "Taille bis Unterbrust", ["waistToUpperLeg"] = "Taille bis Oberschenkel",
        ["wrist"] = "Handgelenkumfang",
    };
    public static string Name(string m) => German.TryGetValue(m, out var n) ? n : m;

    public static IReadOnlyList<string> InstallArgs(string dir) =>
        // config und plugin-transform werden von models/core-plugins importiert, aber dort nicht als Abhängigkeit geführt (im echten Lauf 08.11.2026 gefunden)
        ["install", "@freesewing/core@" + Version, "@freesewing/models@" + Version, "@freesewing/plugin-theme@" + Version, "@freesewing/config@" + Version, "@freesewing/plugin-transform@" + Version,
         .. Designs.Select(d => $"@freesewing/{d.Id}@{Version}"),
         "--prefix", dir, "--no-fund", "--no-audit", "--omit=dev", "--ignore-scripts"];

    public static bool Ready(string dir) => File.Exists(Path.Combine(dir, "node_modules", "@freesewing", "core", "package.json")) && File.Exists(Path.Combine(dir, "node_modules", "@freesewing", "models", "package.json"));

    /// <summary>Startskript (eigener Code, ES-Modul). Maße in Millimetern (Schulterneigung in Grad).</summary>
    public const string Runner = """
        // Trendo – Schnittmuster mit FreeSewing 4 (MIT). Aufruf: node trendo-schnitt.mjs <schnitt> <massen.json> <frau|mann> <naht-mm> <aus.svg>
        import fs from 'node:fs';
        const [design, measuresFile, body, saText, out] = process.argv.slice(2);
        if (!/^[a-z]+$/.test(design || '') || !out) { process.stderr.write('Aufruf: schnitt massen.json frau|mann naht-mm aus.svg'); process.exit(2); }
        const user = JSON.parse(fs.readFileSync(measuresFile, 'utf8'));
        const mod = await import('@freesewing/' + design);
        const Design = mod[design[0].toUpperCase() + design.slice(1)];
        const models = await import('@freesewing/models');
        const { pluginTheme } = await import('@freesewing/plugin-theme');
        const group = body === 'mann' ? 'cisMaleAdult' : 'cisFemaleAdult';
        let best = null;
        for (const size of models.sizes[group]) {
          const m = models[group + size];
          if (!best || Math.abs(m.chest - (user.chest || m.chest)) < Math.abs(best.m.chest - (user.chest || best.m.chest))) best = { size, m };
        }
        const cfg = Design.patternConfig;
        const needed = [...(cfg.measurements || []), ...(cfg.optionalMeasurements || [])];
        const measurements = {};
        const fromPreset = [];
        for (const k of needed) {
          if (typeof user[k] === 'number' && user[k] > 0) measurements[k] = user[k];
          else if (typeof best.m[k] === 'number') { measurements[k] = best.m[k]; if ((cfg.measurements || []).includes(k)) fromPreset.push(k); }
        }
        const pattern = new Design({ measurements, units: 'metric', sa: Math.max(0, Math.min(30, Number(saText) || 0)) }).use(pluginTheme);
        pattern.draft();
        const svg = pattern.render();
        const logs = (pattern.store && pattern.store.logs) || {};
        const errors = (logs.error || []).map(String).slice(0, 5);
        if (!svg || svg.length < 200) { process.stderr.write('Kein Schnitt erzeugt. ' + errors.join(' ')); process.exit(3); }
        fs.writeFileSync(out, svg);
        const w = /width="([0-9.]+)mm"/.exec(svg), h = /height="([0-9.]+)mm"/.exec(svg);
        process.stdout.write(JSON.stringify({ schnitt: design, groesse: group + best.size, ergaenzt: fromPreset, pflicht: cfg.measurements || [], fehler: errors,
          breite_mm: w ? Number(w[1]) : 0, hoehe_mm: h ? Number(h[1]) : 0 }));
        """;

    /// <summary>Trendo-Maße (cm, Schlüssel wie im Programm) in FreeSewing-Maße (mm).</summary>
    public static Dictionary<string, double> ToFreeSewing(IReadOnlyDictionary<string, double> cm)
    {
        var map = new Dictionary<string, string> { ["chest"] = "chest", ["waist"] = "waist", ["hip"] = "hips", ["hips"] = "hips", ["neck"] = "neck", ["biceps"] = "biceps", ["inseam"] = "inseam" };
        var r = new Dictionary<string, double>();
        foreach (var (k, v) in cm)
        {
            if (v <= 0) continue;
            var fs = map.TryGetValue(k, out var m) ? m : German.ContainsKey(k) ? k : null; if (fs is null) continue;
            r[fs] = fs == "shoulderSlope" ? v : Math.Round(v * 10, 1);
        }
        return r;
    }

    public sealed record Result(string Design, string Size, IReadOnlyList<string> Supplemented, IReadOnlyList<string> Required, IReadOnlyList<string> Errors, double WidthMm, double HeightMm)
    {
        /// <summary>Kurztext für die Oberfläche.</summary>
        public string Summary => $"Schnittbogen {WidthMm / 10:0} × {HeightMm / 10:0} cm"
            + (Supplemented.Count == 0 ? " – alle Pflichtmaße von dir." : $" – aus Standardgröße ergänzt: {string.Join(", ", Supplemented.Select(Name))}.");
    }

    public static Result Parse(string json)
    {
        var j = JsonNode.Parse(json) ?? throw new FormatException("Keine Antwort.");
        static List<string> L(JsonNode? n) => n?.AsArray().Select(x => x!.GetValue<string>()).ToList() ?? [];
        return new(j["schnitt"]?.GetValue<string>() ?? "", j["groesse"]?.GetValue<string>() ?? "", L(j["ergaenzt"]), L(j["pflicht"]), L(j["fehler"]),
            j["breite_mm"]?.GetValue<double>() ?? 0, j["hoehe_mm"]?.GetValue<double>() ?? 0);
    }

    /// <summary>Schnitt erzeugen. Prüft Schnittname, Maße (plausibel 1–300 cm), Nahtzugabe 0–30 mm. Rückgabe: Ergebnis oder Meldung.</summary>
    public static (Result? Result, string Message) Draft(string node, string toolDir, string design, IReadOnlyDictionary<string, double> cm, bool man, int seamMm, string outSvg,
        int timeoutMs = 120_000, Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (!Designs.Any(d => d.Id == design)) return (null, "Unbekannter Schnitt.");
        if (cm.TryGetValue("chest", out var chest) is false || chest is < 40 or > 200) return (null, "Bitte den Brustumfang in cm angeben (40–200).");
        if (cm.Values.Any(v => v is < 0 or > 300)) return (null, "Ein Maß ist nicht plausibel (0–300 cm).");
        if (run is null && !Ready(toolDir)) return (null, "FreeSewing ist noch nicht eingerichtet.");
        Directory.CreateDirectory(toolDir);
        var runner = Path.Combine(toolDir, RunnerName);
        if (!File.Exists(runner) || File.ReadAllText(runner) != Runner) File.WriteAllText(runner, Runner);
        var measures = Path.Combine(Path.GetTempPath(), "trendo-masse-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(measures, new JsonObject(ToFreeSewing(cm).Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))).ToJsonString());
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outSvg))!);
            var r = (run ?? ((e, a, t) => Cli.Run(e, a, toolDir, t)))(node, [runner, design, measures, man ? "mann" : "frau", Math.Clamp(seamMm, 0, 30).ToString(CultureInfo.InvariantCulture), outSvg], timeoutMs);
            if (!r.Ok) return (null, "FreeSewing meldet: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0]);
            var res = Parse(r.Out.Trim().Split('\n')[^1]);
            return File.Exists(outSvg) ? (res, res.Summary) : (null, "FreeSewing hat keine Datei geschrieben.");
        }
        catch (System.Text.Json.JsonException) { return (null, "FreeSewing hat keine lesbare Antwort geliefert."); }
        finally { try { File.Delete(measures); } catch (IOException) { } }
    }
}
