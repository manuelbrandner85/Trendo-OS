using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 7 – CORE-07 „Webseite bauen“: Barrierefreiheit genau prüfen mit axe-core (Deque, MPL-2.0, unverändert)
/// in jsdom (MIT) – getrennter Node-Prozess, eingerichtet per npm ohne Installationsskripte. Die Seiten-Skripte laufen dabei NICHT,
/// es wird nichts nachgeladen und nichts ins Netz geschickt. Farbkontrast prüft jsdom nicht (kein Layout) – das übernimmt Trendos eigene Prüfung.</summary>
public static class AxeCheck
{
    public const string AxeVersion = "4.14.0", JsdomVersion = "30.1.3", RunnerName = "trendo-axe.js";

    /// <summary>npm-Argumente: feste Fassungen, eigener Ordner, ohne Installationsskripte.</summary>
    public static IReadOnlyList<string> InstallArgs(string dir) =>
        ["install", "axe-core@" + AxeVersion, "jsdom@" + JsdomVersion, "--prefix", dir, "--no-fund", "--no-audit", "--omit=dev", "--ignore-scripts"];

    public static bool Ready(string dir) =>
        File.Exists(Path.Combine(dir, "node_modules", "axe-core", "axe.min.js")) && File.Exists(Path.Combine(dir, "node_modules", "jsdom", "package.json"));

    /// <summary>Startskript (eigener Code). jsdom mit runScripts „outside-only“: nur axe läuft, Skripte der Seite nicht; ohne Adresse lädt jsdom nichts nach.</summary>
    public const string Runner = """
        // Trendo – Barrierefreiheit mit axe-core in jsdom. Seiten-Skripte laufen nicht, nichts wird nachgeladen.
        'use strict';
        const fs = require('fs');
        const path = require('path');
        const { JSDOM, VirtualConsole } = require('jsdom');
        const axeSource = fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');
        const files = process.argv.slice(2);
        (async () => {
          const results = [];
          for (const f of files) {
            const dom = new JSDOM(fs.readFileSync(f, 'utf8'), { runScripts: 'outside-only', pretendToBeVisual: true, virtualConsole: new VirtualConsole() });
            dom.window.eval(axeSource);
            const r = await dom.window.axe.run(dom.window.document, { resultTypes: ['violations'], rules: { 'color-contrast': { enabled: false } } });
            results.push({ file: path.basename(f), passes: r.passes.length, violations: r.violations.map(v => ({ id: v.id, impact: v.impact, help: v.help, nodes: v.nodes.length, target: ((v.nodes[0] || {}).target || []).join(' ') })) });
            dom.window.close();
          }
          process.stdout.write(JSON.stringify({ axe: require('axe-core/package.json').version, results }));
        })().catch(e => { process.stderr.write(String((e && e.message) || e)); process.exit(1); });
        """;

    /// <summary>Häufige Regeln verständlich auf Deutsch; alle anderen behalten den axe-Hinweis.</summary>
    public static readonly IReadOnlyDictionary<string, string> German = new Dictionary<string, string>
    {
        ["image-alt"] = "Bild ohne Alternativtext – Screenreader können es nicht beschreiben.",
        ["input-image-alt"] = "Bild-Knopf ohne Alternativtext.",
        ["svg-img-alt"] = "Grafik (SVG) mit Bildrolle ohne Beschreibung.",
        ["role-img-alt"] = "Element mit Bildrolle ohne Beschreibung.",
        ["html-has-lang"] = "Der Seite fehlt die Sprachangabe (lang).",
        ["html-lang-valid"] = "Die Sprachangabe (lang) ist ungültig.",
        ["document-title"] = "Die Seite hat keinen Titel.",
        ["label"] = "Eingabefeld ohne Beschriftung.",
        ["select-name"] = "Auswahlliste ohne Beschriftung.",
        ["button-name"] = "Knopf ohne erkennbaren Text.",
        ["link-name"] = "Link ohne erkennbaren Text.",
        ["empty-heading"] = "Leere Überschrift.",
        ["heading-order"] = "Überschriften springen eine Ebene über (z. B. h1 → h3).",
        ["page-has-heading-one"] = "Die Seite hat keine Hauptüberschrift (h1).",
        ["landmark-one-main"] = "Die Seite hat keinen Hauptbereich (main).",
        ["region"] = "Inhalt steht außerhalb von Bereichen wie header, main, nav oder footer.",
        ["list"] = "Liste enthält falsche Elemente (nur li direkt in ul/ol).",
        ["listitem"] = "Listenpunkt (li) steht nicht in einer Liste.",
        ["duplicate-id-aria"] = "Doppelte ID, auf die sich eine Beschriftung bezieht.",
        ["meta-viewport"] = "Zoomen ist gesperrt – Menschen mit Sehschwäche können nicht vergrößern.",
        ["frame-title"] = "Eingebetteter Rahmen (iframe) ohne Titel.",
        ["aria-hidden-focus"] = "Versteckter Bereich enthält bedienbare Elemente.",
        ["nested-interactive"] = "Bedienelemente sind ineinander verschachtelt.",
        ["video-caption"] = "Video ohne Untertitel.",
        ["tabindex"] = "Positive Tab-Reihenfolge (tabindex > 0) verwirrt die Tastaturbedienung.",
        ["aria-allowed-attr"] = "ARIA-Eigenschaft passt nicht zur Rolle des Elements.",
        ["aria-required-attr"] = "Pflicht-ARIA-Eigenschaft fehlt.",
        ["aria-valid-attr-value"] = "ARIA-Eigenschaft hat einen ungültigen Wert.",
    };

    public sealed record Result(string Version, int Files, int Passes, IReadOnlyList<WebsiteFinding> Findings);

    /// <summary>Ausgabe des Startskripts lesen.</summary>
    public static Result Parse(string json)
    {
        var j = JsonNode.Parse(json) ?? throw new FormatException("axe-core hat nichts geliefert.");
        var findings = new List<WebsiteFinding>(); var passes = 0; var files = 0;
        foreach (var f in j["results"]?.AsArray() ?? [])
        {
            files++; passes += f!["passes"]?.GetValue<int>() ?? 0; var file = f["file"]?.GetValue<string>() ?? "";
            foreach (var v in f["violations"]?.AsArray() ?? [])
            {
                var id = v!["id"]!.GetValue<string>(); var impact = v["impact"]?.GetValue<string>() ?? "minor"; var n = v["nodes"]?.GetValue<int>() ?? 1;
                var text = German.TryGetValue(id, out var de) ? de : (v["help"]?.GetValue<string>() ?? id) + " (axe: " + id + ")";
                var where = v["target"]?.GetValue<string>() is { Length: > 0 } t ? $" – z. B. „{(t.Length > 60 ? t[..60] + "…" : t)}“" : "";
                findings.Add(new("Barrierefreiheit", impact is "critical" or "serious" ? "Fehler" : "Hinweis", $"{file}: {text}{(n > 1 ? $" ({n}×)" : "")}{where}", "axe-" + id));
            }
        }
        return new(j["axe"]?.GetValue<string>() ?? "?", files, passes, findings);
    }

    /// <summary>Alle HTML-Seiten im Ordner prüfen (ohne Vorschau-Kopien). Fehlt die Einrichtung, kommt eine klare Meldung statt eines Laufs.</summary>
    public static (Result? Result, string Message) Run(string node, string toolDir, string siteDir, int timeoutMs = 120_000, Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (!Directory.Exists(siteDir)) return (null, "Der Ordner der Seite fehlt.");
        var html = Directory.EnumerateFiles(siteDir, "*.html", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + ".vorschau" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).Take(200).ToList();
        if (html.Count == 0) return (null, "Es liegt keine HTML-Datei im Ordner.");
        if (run is null && !Ready(toolDir)) return (null, "axe-core ist noch nicht eingerichtet.");
        var runner = Path.Combine(toolDir, RunnerName);
        if (!File.Exists(runner) || File.ReadAllText(runner) != Runner) { Directory.CreateDirectory(toolDir); File.WriteAllText(runner, Runner); }
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(node, [runner, .. html], timeoutMs);
        if (!r.Ok) return (null, "axe-core meldet: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0]);
        try { var res = Parse(r.Out); return (res, $"axe-core {res.Version}: {res.Files} Seite(n), {res.Passes} Regeln bestanden, {res.Findings.Count} Befund(e)."); }
        catch (System.Text.Json.JsonException) { return (null, "axe-core hat keine lesbare Antwort geliefert."); }
    }
}
