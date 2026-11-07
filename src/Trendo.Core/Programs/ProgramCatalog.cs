using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>Masterprompt 131 / 19: Eintrag im zentralen Programmkatalog. Alles hier ist aus dem Code abgeleitet (angemeldete Seiten, Menübänder,
/// Quelltext, Funktionskataloge, Vorbild-Analysen, Tests) – nichts wird von Hand behauptet.</summary>
public sealed record ProgramEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Studio { get; init; } = "";
    public string StudioName { get; init; } = "";
    public string Area { get; init; } = "";
    /// <summary>Art der Hauptarbeitsfläche (z. B. „CAD-Zeichenfläche“, „Tabellenraster“) – aus den tatsächlich verwendeten Bedienelementen.</summary>
    public List<string> Type { get; init; } = [];
    /// <summary>Funktionen aus dem freigegebenen Funktionskatalog (Nr, Name, Stand, Test).</summary>
    public List<CatalogFunction> Functions { get; init; } = [];
    public string? FunctionCatalog { get; init; }
    /// <summary>Fach-Engine: Kern-Typen aus Trendo.Core, die das Programm verwendet.</summary>
    public List<string> Engine { get; init; } = [];
    public List<string> Tools { get; init; } = [];
    public List<string> Formats { get; init; } = [];
    public List<string> Dependencies { get; init; } = [];
    public string? Rea { get; init; }
    public string? ReaSummary { get; init; }
    public List<string> Ui { get; init; } = [];
    public List<string> Tabs { get; init; } = [];
    public List<string> Tests { get; init; } = [];
    public List<string> SourceFiles { get; init; } = [];
    public string Status { get; init; } = "";
    public string Version { get; init; } = "";
    /// <summary>Läuft über die gemeinsame Programm-Hülle (Dokumente, Rückgängig, Autosave, Bedienstufen).</summary>
    public bool Shell { get; init; }
    /// <summary>A1: gemessene Öffnungszeit (warm, ms) beim Katalog-Lauf; null = nicht gemessen.</summary>
    public double? WarmMs { get; init; }
    /// <summary>A1: Windows-gebundene Stellen im Quelltext (siehe <see cref="QualityLight.WindowsOnly"/>).</summary>
    public List<string> WindowsOnly { get; init; } = [];
}

public sealed record CatalogFunction(string Nr, string Name, string State, string? Test);

/// <summary>Testergebnis aus einem echten Lauf (TRX), Name → bestanden/fehlgeschlagen/übersprungen.</summary>
public sealed record TestOutcome(string Name, string Outcome);

public static class ProgramCatalog
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Der im Programm mitgelieferte Katalog (erzeugt aus dem Repository, siehe docs/programme).</summary>
    public static List<ProgramEntry> Load()
    {
        using var s = typeof(ProgramCatalog).Assembly.GetManifestResourceStream("Trendo.Core.Programme.katalog.json");
        return s is null ? [] : JsonSerializer.Deserialize<List<ProgramEntry>>(s, Json) ?? [];
    }
    public static string ToJson(List<ProgramEntry> all) => JsonSerializer.Serialize(all, Json);
    public static List<ProgramEntry> FromJson(string json) => JsonSerializer.Deserialize<List<ProgramEntry>>(json, Json) ?? [];

    // ---------- Aus dem Repository ableiten ----------
    static readonly string[] FormatExt = ["pdf", "svg", "dxf", "png", "jpg", "gltf", "glb", "obj", "stl", "fbx", "ply", "gpx", "kml", "geojson", "csv", "xlsx", "docx", "odt", "ods", "pptx", "txt", "md", "json", "xml", "html", "gcode", "nc", "ngc", "ifc", "spice", "cir", "gbr", "drl", "mp4", "mp3", "wav", "ogg", "webm", "m3u", "m3u8", "xmltv", "ics", "vcf", "eml", "zip", "tdoc", "apkg", "srt", "e57", "las", "xyz", "pts"];
    /// <summary>Externe Programme und Dienste: Suchmuster (Regex, ohne Groß/Klein) → Anzeigename.</summary>
    static readonly (string Pattern, string Name)[] Deps =
    [
        (@"\bblender\b", "Blender (optional, 3D-Fotos/Render)"), (@"\bunreal\b", "Unreal Engine (optional)"), (@"\bollama\b", "Ollama – lokale KI (optional)"), (@"\bffmpeg\b", "FFmpeg"),
        (@"\blibreoffice\b|\bsoffice\b", "LibreOffice (optional)"), (@"\bqpdf\b", "qpdf"), (@"\bkrita\b", "Krita (optional)"), (@"\binkscape\b", "Inkscape (optional)"),
        (@"\bgimp\b", "GIMP (optional)"), (@"\bfreecad\b", "FreeCAD (optional)"), (@"\bkicad\b", "KiCad (optional)"), (@"\btesseract\b", "Tesseract OCR"), (@"\bpiper\b", "Piper (Vorlesen)"),
        (@"\bwhisper\b", "whisper (Spracherkennung)"), (@"openstreetmap", "OpenStreetMap-Kacheln (Internet, Namensnennung)"), (@"polyhaven", "Poly Haven (freie 3D-Modelle)"),
        (@"ambientcg", "ambientCG (freie Materialien)"), (@"\bPdfSharp\b", "PDFsharp (MIT)"), (@"\bHunspell\b", "Hunspell-Wörterbücher"), (@"""git""|\bgit\.exe\b|\bgit (clone|pull|init)\b", "Git"),
        (@"\bOBS\b(?! ?=)|obs-websocket|obs64", "OBS Studio (optional)"), (@"\bvlc\b", "VLC (optional)"), (@"\bmpv\b", "mpv (optional)"), (@"""python3?""|\bpython(3)?\.exe\b", "Python (optional)"),
        (@"\bnode\.exe\b|""node""|\bnpm\b", "Node.js (optional)"), (@"\bdocker\b", "Docker (optional)"),
    ];
    /// <summary>Allerweltsnamen, die als Kern-Typ nichts über die Fach-Engine sagen.</summary>
    static readonly HashSet<string> NotEngine = ["Vec3", "Mesh", "Fach", "Cli", "Principal", "AuditLog", "DevStudio", "UserSettings", "ProgramDocument", "Level", "Action", "Color", "Point", "Shape", "State", "Entry", "Result", "Options", "Item", "Material", "Language", "Fact", "Kind", "Num", "Field", "Info", "Note", "Step", "Line", "Text"];

    /// <summary>Ergänzt die Laufzeitangaben (Name, Studio, Werkzeuge …) um alles, was sich aus dem Quelltext belegen lässt.</summary>
    public static ProgramEntry Enrich(ProgramEntry e, string repo, IReadOnlyList<string>? requirementsLines = null, IReadOnlyDictionary<string, string>? coreTypes = null)
    {
        var app = Path.Combine(repo, "src", "Trendo.App");
        var files = Directory.EnumerateFiles(app, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => { var t = File.ReadAllText(f); return Regex.IsMatch(t, $"(RegisterTab|RegisterProgram)\\([^;]*\"{Regex.Escape(e.Studio)}\"") || t.Contains($"new ProgramSpec({Regex.Escape(e.Id)}") || Regex.IsMatch(t, $"PageModules\\[\"{Regex.Escape(e.Id)}\"\\]\\s*=") || Regex.IsMatch(t, $"new ProgramSpec\\(\\w+, \"{Regex.Escape(e.Studio)}\""); })
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        var text = string.Join("\n", files.Select(File.ReadAllText));
        coreTypes ??= CoreTypes(repo);
        // nur echte Verwendungen: Erzeugung, statischer Aufruf, Typargument
        int Uses(string t) { var q = Regex.Escape(t); return Regex.Matches(text, $@"(?<![\w.])(new {q}\(|{q}\.[A-Z]|{q}<|<{q}>)").Count; }
        var engine = coreTypes.Keys.Where(t => t.Length >= 4 && !NotEngine.Contains(t)).Select(t => (t, n: Uses(t))).Where(x => x.n > 0)
            .OrderByDescending(x => x.n).ThenBy(x => x.t, StringComparer.Ordinal).Select(x => x.t).Take(10).ToList();
        var engineText = text + string.Join("\n", engine.Select(t => coreTypes[t]).Distinct().Select(f => File.ReadAllText(Path.Combine(repo, f))));
        var formats = FormatExt.Where(x => Regex.IsMatch(engineText, $"\"[^\"\\n]{{0,40}}\\.{x}\"", RegexOptions.IgnoreCase)).Select(x => "." + x).Union(e.Formats).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var deps = Deps.Where(d => Regex.IsMatch(engineText, d.Pattern, RegexOptions.IgnoreCase)).Select(d => d.Name).Distinct().ToList();
        var (fc, functions) = Functions(repo, e.Studio);
        var (rea, reaSum) = Vorbild(repo, e);
        return e with
        {
            Engine = engine, Formats = formats, Dependencies = deps, Functions = functions, FunctionCatalog = fc, Rea = rea, ReaSummary = reaSum,
            Tests = Tests(repo, e), SourceFiles = files.Select(f => Rel(repo, f)).ToList(), Status = Status(requirementsLines ?? ReadLines(Path.Combine(repo, "requirements.yaml")), e.Studio),
            WindowsOnly = QualityLight.WindowsOnly(text),
        };
    }

    static string[] ReadLines(string f) => File.Exists(f) ? File.ReadAllLines(f) : [];
    static string Rel(string repo, string f) => Path.GetRelativePath(repo, f).Replace('\\', '/');

    /// <summary>Öffentliche Typen in Trendo.Core → Datei.</summary>
    public static Dictionary<string, string> CoreTypes(string repo)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in Directory.EnumerateFiles(Path.Combine(repo, "src", "Trendo.Core"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")).OrderBy(f => f, StringComparer.Ordinal))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"public (?:sealed |static |abstract |partial |readonly )*(?:class|record|struct|enum) (\w+)"))
                d.TryAdd(m.Groups[1].Value, Rel(repo, f));
        return d;
    }

    /// <summary>Freigegebener Funktionskatalog des Studios (docs/w6-katalog oder docs/spec/kataloge).</summary>
    public static (string? File, List<CatalogFunction> Rows) Functions(string repo, string studio)
    {
        var f = new[] { Path.Combine(repo, "docs", "w6-katalog", studio + ".md"), Path.Combine(repo, "docs", "spec", "kataloge", studio + "_KATALOG.md") }.FirstOrDefault(File.Exists);
        if (f is null) return (null, []);
        var rows = new List<CatalogFunction>(); string[]? head = null;
        foreach (var line in File.ReadLines(f))
        {
            if (!line.StartsWith('|')) { head = line.Length == 0 ? head : null; continue; }
            var c = line.Trim('|').Split('|').Select(x => x.Trim()).ToArray();
            if (head is null) { head = c; continue; }
            if (c.All(x => x.All(ch => ch is '-' or ':' or ' '))) continue;
            int Col(params string[] names) => Array.FindIndex(head, h => names.Any(n => h.Contains(n, StringComparison.OrdinalIgnoreCase)));
            var (nr, name, state, test) = (Col("Nr", "ID"), Col("Funktion"), Col("Stand"), Col("Test"));
            if (nr < 0 || name < 0) continue;
            string? Get(int i) => i >= 0 && i < c.Length ? c[i] : null;
            rows.Add(new(Get(nr)!, Get(name)!, Get(state) ?? "", Get(test)?.Trim('`') is { Length: > 0 } t ? t : null));
        }
        return (Rel(repo, f), rows);
    }

    /// <summary>Programme, für die eine Vorbild-Analyse gilt (Zeile „- Programme: „A“, „B““).</summary>
    public static HashSet<string> AnalysisPrograms(string text) =>
        text.Split('\n').FirstOrDefault(l => l.StartsWith("- Programme:")) is { } line
            ? System.Text.RegularExpressions.Regex.Matches(line, "„([^“]+)“").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal)
            : [];

    /// <summary>Vorbild-Analyse (docs/claude/vorbilder), wenn sie das Programm in ihrer Zeile „Programme:“ nennt.</summary>
    public static (string? File, string? Summary) Vorbild(string repo, ProgramEntry e)
    {
        var dir = Path.Combine(repo, "docs", "claude", "vorbilder"); if (!Directory.Exists(dir)) return (null, null);
        foreach (var f in Directory.EnumerateFiles(dir, "*.md").OrderBy(f => f, StringComparer.Ordinal))
        {
            var t = File.ReadAllText(f);
            // Paket F (03.11.2026): eindeutig über die Zeile „- Programme: „A“, „B““ – früher genügte der Studioname, dann galt z. B. die
            // Text-Analyse fälschlich auch für Tabelle, Präsentation und PDF
            if (!AnalysisPrograms(t).Contains(e.Name)) continue;
            var title = t.Split('\n').FirstOrDefault(l => l.StartsWith("# "))?[2..].Trim();
            return (Rel(repo, f), title);
        }
        return (null, null);
    }

    /// <summary>Tests, die das Programm ansprechen: Name nennt das Studio, oder die Testdatei öffnet seine Seite.</summary>
    public static List<string> Tests(string repo, ProgramEntry e)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var f in Directory.EnumerateFiles(Path.Combine(repo, "tests"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var t = File.ReadAllText(f);
            var whole = t.Contains($"StudioPages[\"{e.Studio}\"]") || Regex.IsMatch(t, $"Show\\(\"{Regex.Escape(e.Id)}\"\\)");
            foreach (Match m in Regex.Matches(t, "DisplayName = \"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                var n = Regex.Unescape(m.Groups[1].Value);
                if (whole || Regex.IsMatch(n, $"(^|[^A-Z0-9-]){Regex.Escape(e.Studio)}([^0-9]|$)")) names.Add(n);
            }
        }
        return names.ToList();
    }

    static string Status(IReadOnlyList<string> req, string studio)
    {
        var i = req.ToList().FindIndex(l => l.Trim() == $"- id: {studio}"); if (i < 0) return "nicht im Modulregister";
        string? Val(string k) => req.Skip(i + 1).TakeWhile(l => !l.StartsWith("- id:")).FirstOrDefault(l => l.Trim().StartsWith(k + ":"))?.Split(':', 2)[1].Trim();
        return $"{Val("implementation_status") ?? "?"} / Test {Val("test_status") ?? "?"}";
    }

    // ---------- Ausgabe ----------
    public static string Markdown(List<ProgramEntry> all)
    {
        var sb = new StringBuilder("# Trendo OS – Programmkatalog\n\n");
        sb.Append("Erzeugt aus dem Repository (angemeldete Programme, Menübänder, Quelltext, Funktionskataloge, Vorbild-Analysen, Tests). ");
        sb.Append("Nicht von Hand pflegen – neu erzeugen mit `TRENDO_KATALOG=. dotnet run --project tools/Trendo.Render -c Release`.\n\n");
        sb.Append($"{all.Count} Programme in {all.Select(x => x.Studio).Distinct().Count()} Studios.\n\n");
        sb.Append("| Ampel | Programm | Studio | Bereich | Arbeitsfläche | Funktionen | Tests | Vorbild-Analyse | Öffnen (warm) | Trendo OS (Linux) | Modulstatus | Doku |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (var e in all)
        {
            var q = QualityLight.Rate(e);
            sb.Append($"| {(q.Light == "grün" ? "🟢" : q.Light == "gelb" ? "🟡" : "🔴")} {q.Score} | {e.Name} | {e.StudioName} ({e.Studio}) | {e.Area} | {(e.Type.Count == 0 ? "keine eigene erkannt" : string.Join(", ", e.Type))} | {(e.Functions.Count > 0 ? e.Functions.Count.ToString() : "–")} | {e.Tests.Count} | {(e.Rea is null ? "–" : "ja")} | {(e.WarmMs is { } ms ? $"{ms:0} ms" : "–")} | {(e.WindowsOnly.Count == 0 ? "ja" : "eingeschränkt (" + string.Join(", ", e.WindowsOnly) + ")")} | {e.Status} | [Paket]({e.Id}/README.md) |\n");
        }
        return sb.ToString();
    }

    public static readonly string[] DocFiles = ["README.md", "ARCHITECTURE.md", "REA_REFERENCE_ANALYSIS.md", "FEATURE_MATRIX.md", "IMPLEMENTATION_STATUS.md", "USER_WORKFLOWS.md", "TEST_PLAN.md", "TEST_RESULTS.md", "PERFORMANCE_REPORT.md", "LICENSE_REPORT.md", "INTEGRATION_REPORT.md"];

    /// <summary>Masterprompt 131 / 17: Produktionspaket je Programm. Testergebnisse nur aus einem echten Lauf (TRX); ohne Lauf steht dort „nicht ausgeführt“.</summary>
    public static Dictionary<string, string> Docs(ProgramEntry e, IReadOnlyDictionary<string, string>? outcomes = null, string? runSource = null)
    {
        string L(IEnumerable<string> xs, string none = "– (nicht erfasst)") { var l = xs.ToList(); return l.Count == 0 ? none + "\n" : string.Concat(l.Select(x => $"- {x}\n")); }
        string? Outcome(string? test) => test is null || outcomes is null ? null : outcomes.TryGetValue(test, out var o) ? o : null;
        var head = $"# {e.Name} – {{0}}\n\nStudio: {e.StudioName} ({e.Studio}) · Bereich: {e.Area} · Seite: `{e.Id}` · Stand: {e.Version}\n\n";
        string H(string t) => string.Format(head, t);
        var d = new Dictionary<string, string>();
        d["README.md"] = H("Produktionspaket") +
            $"Arbeitsfläche: {(e.Type.Count == 0 ? "keine eigene Arbeitsfläche erkannt (Seite aus Listen, Feldern und Knöpfen)" : string.Join(", ", e.Type))}  \nModulstatus (requirements.yaml): {e.Status}  \n" +
            $"Programm-Hülle (Dokumente, Rückgängig, Autosave, Bedienstufen): {(e.Shell ? "ja" : "nein – eigene Arbeitsseite")}\n\n## Inhalt\n" + L(DocFiles.Skip(1).Select(f => $"[{f}]({f})")) +
            "\nAlle Angaben sind aus Code, Katalogen und Tests abgeleitet. Was nicht belegt ist, steht als „nicht erfasst“ da.\n";
        d["ARCHITECTURE.md"] = H("Architektur") +
            "## Präsentation (Trendo.App)\n" + L(e.SourceFiles.Select(f => $"`{f}`")) + "\n## Bedienelemente der Arbeitsfläche\n" + L(e.Ui) +
            (e.Tabs.Count > 1 ? "\n## Reiter\n" + L(e.Tabs) : "") +
            "\n## Fach-Engine (Trendo.Core)\n" + L(e.Engine.Select(t => $"`{t}`")) + "\n## Dateiformate (im Code belegt)\n" + L(e.Formats) +
            "\n## Externe Programme und Dienste\n" + L(e.Dependencies, "– keine") +
            "\n## Bausteine (Masterprompt 131 / 5)\n" + (e.Shell
                ? "A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen\n"
                : "Eigene Arbeitsseite ohne die gemeinsame Programm-Hülle – Dokumentmodell, Rückgängig und Bedienstufen sind hier nicht einheitlich.\n");
        d["REA_REFERENCE_ANALYSIS.md"] = H("Vorbild-Analyse (REA, Clean-Room)") + (e.Rea is null
            ? "Für dieses Programm ist noch keine Vorbild-Analyse dokumentiert.\n"
            : $"Analyse: [{e.ReaSummary}](../../../{e.Rea})\n\nKein fremder Code und keine fremden Grafiken übernommen – nur öffentlich beschriebener Funktionsumfang.\n");
        var rows = e.Functions;
        string Matrix(CatalogFunction f) => f.State.ToLowerInvariant() switch
        {
            var s when s.Contains("teil") => "Teilweise vorhanden",
            var s when s.Contains("blockiert") || s.Contains("eingeschränkt") => "Technisch eingeschränkt",
            var s when s.Contains("gebaut") || s.Contains("vorhanden") || s.Contains("umgesetzt") || s.Contains("fertig") => "Vorhanden",
            var s when s.Contains("extern") || s.Contains("engine") => "Über externe Engine",
            _ => "Noch zu entwickeln",
        };
        d["FEATURE_MATRIX.md"] = H("Funktionsmatrix") + (rows.Count == 0
            ? "Kein freigegebener Funktionskatalog für dieses Studio.\n\n## Werkzeuge der Oberfläche\n" + L(e.Tools)
            : $"Quelle: `{e.FunctionCatalog}`\n\n| Nr | Funktion | Einordnung | Stand laut Katalog |\n|---|---|---|---|\n" + string.Concat(rows.Select(f => $"| {f.Nr} | {f.Name} | {Matrix(f)} | {f.State} |\n")) + "\n## Werkzeuge der Oberfläche\n" + L(e.Tools));
        string Impl(CatalogFunction f)
        {
            var m = Matrix(f);
            if (m == "Technisch eingeschränkt") return "Blockiert";
            if (m == "Noch zu entwickeln") return "Geplant";
            if (m == "Teilweise vorhanden") return "In Arbeit";
            return Outcome(f.Test) == "Passed" ? "Getestet" : "Implementiert";
        }
        d["IMPLEMENTATION_STATUS.md"] = H("Umsetzungsstand") +
            "Stufen: Geplant · In Arbeit · Implementiert · Getestet · Blockiert. „Getestet“ nur, wenn der zugehörige Test im letzten echten Lauf bestanden hat.\n\n" +
            (rows.Count == 0 ? "Kein Funktionskatalog – Stand je Funktion nicht erfasst.\n" :
            "| Nr | Funktion | Stand | Test |\n|---|---|---|---|\n" + string.Concat(rows.Select(f => $"| {f.Nr} | {f.Name} | {Impl(f)} | {(f.Test is null ? "–" : $"`{f.Test}`")} |\n")) +
            $"\n{rows.Count(f => Impl(f) == "Getestet")} von {rows.Count} Funktionen getestet{(outcomes is null ? " (noch kein Testlauf eingelesen)" : "")}.\n");
        d["USER_WORKFLOWS.md"] = H("Arbeitsabläufe") +
            "Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).\n\n" +
            L(e.Tests.Where(t => t.Contains(" UI") || t.Contains("UI:") || t.Contains("Ui")).Select(t => t), "– kein Oberflächentest gefunden");
        d["TEST_PLAN.md"] = H("Testplan") + $"{e.Tests.Count} Tests sprechen dieses Programm an.\n\n" + L(e.Tests, "– keine Tests gefunden");
        var res = e.Tests.Select(t => (t, o: Outcome(t))).ToList();
        d["TEST_RESULTS.md"] = H("Testergebnisse") + (outcomes is null
            ? "Noch kein Testlauf eingelesen. Ergebnisse stehen hier erst nach einem echten Lauf (TRX-Datei) – nicht vorher.\n"
            : $"Lauf: `{runSource}`\n\nBestanden {res.Count(x => x.o == "Passed")} · Fehlgeschlagen {res.Count(x => x.o == "Failed")} · Übersprungen {res.Count(x => x.o is "NotExecuted" or "Skipped")} · nicht im Lauf {res.Count(x => x.o is null)}\n\n| Test | Ergebnis |\n|---|---|\n" +
              string.Concat(res.Select(x => $"| {x.t.Replace("|", "\\|")} | {x.o switch { "Passed" => "✓ bestanden", "Failed" => "✕ fehlgeschlagen", null => "nicht im Lauf", _ => "übersprungen" }} |\n")));
        d["PERFORMANCE_REPORT.md"] = H("Leistung") + "Kein eigener Leistungsmesslauf für dieses Programm. Zielhardware und Budgets sind noch nicht festgelegt – Messung offen.\n" +
            (e.Tests.Any(t => t.Contains("ms") || t.Contains("schnell") || t.Contains("Dreiecke")) ? "\nTests mit Leistungsbezug:\n" + L(e.Tests.Where(t => t.Contains("ms") || t.Contains("schnell") || t.Contains("Dreiecke"))) : "");
        d["LICENSE_REPORT.md"] = H("Lizenzen") + "Eigener Code (Trendo OS). Eingebundene Komponenten und Dienste, die der Code nennt:\n\n" + L(e.Dependencies, "– keine externen Komponenten") +
            "\nLizenztexte der mitgelieferten Bibliotheken: `src/Trendo.Core/WebAssets/LIZENZEN.txt`. Optionale Programme werden nur nach Zustimmung aus offiziellen Quellen eingerichtet (GRUNDSATZ-01).\n";
        d["INTEGRATION_REPORT.md"] = H("Einbindung in Trendo OS") +
            $"- Seite `{e.Id}` im Studio „{e.StudioName}“ ({e.Area}) – erreichbar über Startmenü, Suche und Programmkatalog\n" +
            $"- Programm-Hülle: {(e.Shell ? "ja (Dokumente .tdoc, Zuletzt, Wiederherstellen, Bedienstufen)" : "nein")}\n" +
            (e.Tabs.Count > 1 ? $"- Reiter: {string.Join(", ", e.Tabs)}\n" : "") + "- Quelltext: " + string.Join(", ", e.SourceFiles.Select(f => $"`{f}`")) + "\n";
        foreach (var k in d.Keys.ToList()) d[k] = NoPersonalPaths(d[k]);
        return d;
    }

    /// <summary>Der Katalog geht ins öffentliche Repo: Pfade im Benutzerordner (z. B. Testordner aus der Oberfläche) werden zu C:\Users\&lt;user&gt;.</summary>
    public static string NoPersonalPaths(string text)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 3) text = text.Replace(home, Path.Combine(Path.GetDirectoryName(home) ?? "", "<user>"), StringComparison.OrdinalIgnoreCase);
        return System.Text.RegularExpressions.Regex.Replace(text, @"([A-Za-z]:\\Users\\)(?!<user>)[^\\\s]+", "$1<user>");
    }

    /// <summary>Testergebnisse aus TRX-Dateien (Name = DisplayName).</summary>
    public static Dictionary<string, string> ReadTrx(IEnumerable<string> files)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            var x = System.Xml.Linq.XDocument.Load(f);
            foreach (var r in x.Descendants().Where(n => n.Name.LocalName == "UnitTestResult"))
                if (r.Attribute("testName")?.Value is { } n) d[n] = r.Attribute("outcome")?.Value ?? "";
        }
        return d;
    }
}
