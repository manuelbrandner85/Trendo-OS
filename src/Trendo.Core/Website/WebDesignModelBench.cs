using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Z6: lokales Modell für Webdesign. Ein Prüflauf mit festen Web-Aufgaben vergleicht die Modelle auf diesem PC; empfohlen wird nur, was alle Aufgaben besteht
/// und zu Arbeitsspeicher und Grafikkarte passt. Das Ergebnis liegt in <c>ki/web-pruefung.json</c> im Datenordner.</summary>
public static class WebDesignModelBench
{
    public const string FileName = "web-pruefung.json";
    const string Base = """{"root":{"props":{"title":"Prüfseite","description":"Seite für den Prüflauf"}},"content":[{"type":"Kopfbereich","props":{"id":"held","title":"Willkommen","sub":"Text","cta":"Mehr","href":"#","motion":"keine","seconds":1}},{"type":"Text","props":{"id":"t1","text":"Wir haben jeden Tag geöffnet.","align":"left"}},{"type":"Bild","props":{"id":"foto","src":"","alt":""}}],"zones":{}}""";

    public sealed record BenchTask(string Name, string Prompt, string Page, WebPlanScope? Scope, Func<string, string?> Judge);
    public sealed record TaskResult(string Task, bool Passed, string Note, double Seconds);
    public sealed record ModelResult(string Model, IReadOnlyList<TaskResult> Tasks, DateTimeOffset At)
    {
        public bool Passed => Tasks.Count > 0 && Tasks.All(t => t.Passed);
        public double Seconds => Tasks.Sum(t => t.Seconds);
    }

    /// <summary>Feste Web-Aufgaben: neue Seite aus einem Satz, Änderung nur an der Auswahl, Nachbessern eines gefundenen Fehlers.</summary>
    public static readonly IReadOnlyList<BenchTask> Tasks =
    [
        new("Seite aus einem Satz", WebDesignKi.NewPagePrompt("Eine Seite für eine kleine Bäckerei mit Angebot, Öffnungszeiten und Kontakt."), WebDesign.Empty(), null, json =>
        {
            var doc = JsonNode.Parse(json)!; var blocks = WebDesignComponents.Expand(doc.AsObject())["content"]!.AsArray();
            if (blocks.Count < 3) return "weniger als drei Bausteine";
            return WebDesignCheck.CheckData(json, Path.GetTempPath()).Any(f => f.Code == "h1") ? "keine oder mehrere Hauptüberschriften" : null;
        }),
        new("Nur die Auswahl ändern", WebDesignKi.SelectionPrompt("Formuliere den Text freundlicher und einladender.", Base, ["t1"]), Base, new WebPlanScope(new HashSet<string> { "t1" }), json =>
            JsonNode.Parse(json)!["content"]![1]!["props"]!["text"]!.ToString() == "Wir haben jeden Tag geöffnet." ? "Text unverändert" : null),
        new("Gefundenen Fehler beheben", WebDesignKi.RepairPrompt(Base.Replace("\"href\":\"#\"", "\"href\":\"#weg\""), [new("Links", "Fehler", "Baustein „held“ springt zu „#weg“, dieses Ziel gibt es auf der Seite nicht.", "anchor")]),
            Base.Replace("\"href\":\"#\"", "\"href\":\"#weg\""), null, json => json.Contains("\"#weg\"") ? "Fehler nicht behoben" : null),
    ];

    /// <summary>Antwort bewerten: Nur ein gültiger Plan zählt; ungültige Editor-Daten oder roher Code fallen durch.</summary>
    public static (bool Passed, string Note) Score(BenchTask task, string answer)
    {
        var r = WebDesignActions.Apply(task.Page, answer, task.Scope);
        if (r.Json is null) return (false, "ungültig: " + r.Error);
        return task.Judge(r.Json) is { } why ? (false, why) : (true, "bestanden");
    }

    /// <summary>Alle Aufgaben mit einem Modell durchlaufen.</summary>
    public static async Task<ModelResult> RunAsync(IKiBackend backend, string model, string? skills = null, CancellationToken ct = default)
    {
        var results = new List<TaskResult>();
        foreach (var t in Tasks)
        {
            var sw = Stopwatch.StartNew();
            string answer;
            try { answer = await backend.ChatAsync(model, [new("system", WebDesignKi.Instructions(skills, t.Scope)), new("user", t.Prompt)], ct); }
            catch (Exception e) when (e is not OperationCanceledException) { results.Add(new(t.Name, false, "keine Antwort: " + e.Message, sw.Elapsed.TotalSeconds)); continue; }
            var (ok, note) = Score(t, answer);
            results.Add(new(t.Name, ok, note, Math.Round(sw.Elapsed.TotalSeconds, 1)));
        }
        return new(model, results, DateTimeOffset.Now);
    }

    static string File(string dataRoot) => Path.Combine(dataRoot, "ki", FileName);

    public static void Save(string dataRoot, IEnumerable<ModelResult> results)
    {
        var merged = Load(dataRoot).ToDictionary(r => r.Model);
        foreach (var r in results) merged[r.Model] = r;
        Directory.CreateDirectory(Path.GetDirectoryName(File(dataRoot))!);
        System.IO.File.WriteAllText(File(dataRoot), JsonSerializer.Serialize(merged.Values.OrderBy(r => r.Model).ToList(), new JsonSerializerOptions { WriteIndented = true }));
    }

    public static List<ModelResult> Load(string dataRoot)
    {
        if (!System.IO.File.Exists(File(dataRoot))) return [];
        try { return JsonSerializer.Deserialize<List<ModelResult>>(System.IO.File.ReadAllText(File(dataRoot))) ?? []; }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException) { return []; }
    }

    public sealed record Recommendation(string? Model, string Note, IReadOnlyList<KiLibraryModel> Candidates);

    /// <summary>Modelle für Webdesign, die auf diesen PC passen (Arbeitsspeicher; mit Grafikkarte zählt deren Speicher für die Größe).</summary>
    public static IReadOnlyList<KiLibraryModel> Candidates(HardwareInfo hw) =>
        KiLibrary.Models.Where(m => m.Kind is KiKind.Chat or KiKind.Code && m.Runtime == KiRuntime.Ollama)
            .Where(m => hw.RamBytes >= m.MinRamGb * 1024L * 1024 * 1024 * 0.9 && (hw.VramBytes == 0 || m.SizeGb * 1024L * 1024 * 1024 <= Math.Max(hw.VramBytes, hw.RamBytes / 2)))
            .OrderByDescending(m => m.SizeGb).ToList();

    /// <summary>Empfehlung: das schnellste Modell, das alle Aufgaben bestanden hat und zum PC passt. Sonst sagt Trendo klar, was fehlt.</summary>
    public static Recommendation Recommend(HardwareInfo hw, IReadOnlyList<ModelResult> results)
    {
        var fits = Candidates(hw);
        if (fits.Count == 0) return new(null, "Für diesen PC passt kein Modell für Webdesign: Es braucht mindestens 8 GB Arbeitsspeicher. Mit Claude über dein Abo geht es trotzdem.", fits);
        var tags = fits.Select(m => m.Tag).ToHashSet();
        var best = results.Where(r => r.Passed && tags.Contains(r.Model)).OrderBy(r => r.Seconds).FirstOrDefault();
        if (best is not null) return new(best.Model, $"Empfohlen für Webdesign: {best.Model} – hat alle {best.Tasks.Count} Web-Aufgaben bestanden ({best.Seconds:0} s).", fits);
        return results.Count == 0
            ? new(null, $"Noch kein Prüflauf. Passend für diesen PC: {string.Join(", ", fits.Take(4).Select(m => m.Tag))}. Starte den Prüflauf, dann empfiehlt Trendo nur, was besteht.", fits)
            : new(null, "Kein geprüftes Modell hat alle Web-Aufgaben bestanden. Lade ein größeres Modell aus der Liste oder nutze Claude über dein Abo.", fits);
    }
}
