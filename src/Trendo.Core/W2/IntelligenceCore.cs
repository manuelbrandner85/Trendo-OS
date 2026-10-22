using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record KiMessage(string Role, string Content);

/// <summary>Eine KI-Quelle auf diesem Gerät (Ollama oder ein OpenAI-kompatibler lokaler Server). Nur Loopback-Adressen – keine Cloud, nichts kostet Geld.</summary>
public interface IKiBackend
{
    string Name { get; }
    string BaseUrl { get; }
    Task<bool> IsUpAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default);
    Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default);
    /// <summary>Wahr für Quellen auf diesem PC. Online-Dienste setzen es auf falsch.</summary>
    bool Local => true;
    /// <summary>Höher ist besser. Wirkt nur unter den Online-Diensten. Lokal bleibt immer vorn.</summary>
    int Quality => 0;
    bool Serves(KiJob job) => job is KiJob.Text or KiJob.Code;
    Task<string> SeeAsync(string model, string question, byte[] image, CancellationToken ct = default)
        => Task.FromException<string>(new NotSupportedException("kein Bild"));
    Task<float[]?> EmbedAsync(string model, string text, CancellationToken ct = default)
        => Task.FromResult<float[]?>(null);
}

public sealed record KiRequest(string Question, string? SystemPrompt = null, string? PreferredModel = null, IReadOnlyList<KiMessage>? History = null, bool UseKnowledge = true, KiJob Task = KiJob.Text, bool AllowOnline = true, byte[]? Image = null);
public sealed record KiAnswer(bool Ok, string Text, string? Backend, string? Model, IReadOnlyList<string> Sources, TimeSpan Took, bool OnThisPc = true);

/// <summary>CORE-02 Trendo Intelligence Core: ein Eingang für alle KI-Fragen in Trendo OS.
/// Wählt die erste erreichbare lokale Quelle (Reihenfolge = Rückfallkette aus X-05), ergänzt passendes Wissen (CORE-10),
/// protokolliert jede Frage (nur Länge und Quelle, nie den Inhalt) und führt einen Verlauf auf diesem Gerät.</summary>
public sealed class IntelligenceCore
{
    public const string DefaultSystem = "Du bist Trendo, die KI in Trendo OS. Antworte freundlich, kurz und auf Deutsch. Wenn du etwas nicht weißt, sag das ehrlich.";
    private readonly AuditLog _audit;
    private readonly Func<IReadOnlyList<IKiBackend>> _backends;
    private readonly Func<string, int, IReadOnlyList<(string Title, string Text)>>? _knowledge;
    private readonly string _historyFile;

    public IntelligenceCore(string dataRoot, AuditLog audit, Func<IReadOnlyList<IKiBackend>> backends, Func<string, int, IReadOnlyList<(string Title, string Text)>>? knowledge = null)
    {
        _audit = audit; _backends = backends; _knowledge = knowledge;
        _historyFile = Path.Combine(dataRoot, "ki", "verlauf.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(_historyFile)!);
    }

    public async Task<KiAnswer> AskAsync(KiRequest req, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(req.Question) && req.Image is not { Length: > 0 })
            return new(false, "Bitte schreib zuerst eine Frage.", null, null, [], sw.Elapsed);
        var question = string.IsNullOrWhiteSpace(req.Question) ? "Was siehst du?" : req.Question.Trim();
        var sources = new List<string>();
        var sys = new StringBuilder(req.SystemPrompt ?? DefaultSystem);
        if (req.UseKnowledge && _knowledge is not null && !string.IsNullOrWhiteSpace(req.Question))
        {
            var hits = _knowledge(question, 3);
            if (hits.Count > 0)
            {
                sys.Append("\n\nNutze dieses Wissen des Nutzers, wenn es passt, und nenne den Titel als Quelle:\n");
                foreach (var (t, x) in hits) { sys.Append($"### {t}\n{(x.Length > 1500 ? x[..1500] : x)}\n"); sources.Add(t); }
            }
        }
        var msgs = new List<KiMessage> { new("system", sys.ToString()) };
        if (req.History is not null) msgs.AddRange(req.History.TakeLast(10));
        msgs.Add(new("user", question));
        var tried = new List<string>();
        var limited = false;
        var ordered = FreeAiRouter.Order(_backends().Where(b => (req.AllowOnline || b.Local) && b.Serves(req.Task)).ToList());
        foreach (var b in ordered)
        {
            if (!await b.IsUpAsync(ct)) { tried.Add($"{b.Name}: nicht erreichbar"); continue; }
            var models = await b.ModelsAsync(ct);
            var picks = LocalAiPlan.Candidates(models, req);
            if (picks.Count == 0) { tried.Add($"{b.Name}: kein Modell"); continue; }
            foreach (var model in picks.Take(4))
            {
                try
                {
                    string text;
                    if (req.Task == KiJob.Embed)
                    {
                        var vec = await b.EmbedAsync(model, question, ct);
                        if (vec is not { Length: > 0 }) { tried.Add($"{b.Name}: kein Einordnen"); continue; }
                        text = $"Eingeordnet ({vec.Length} Werte).";
                    }
                    else if (req.Task == KiJob.Vision && req.Image is { Length: > 0 } img)
                        text = await b.SeeAsync(model, question, img, ct);
                    else
                        text = await b.ChatAsync(model, msgs, ct);
                    _audit.Append("ki", "ki.ask", $"quelle={b.Name} modell={model} aufgabe={req.Task} lokal={b.Local} frage={question.Length}Z antwort={text.Length}Z wissen={sources.Count}");
                    Save(question, text, b.Name, model);
                    return new(true, text, b.Name, model, sources, sw.Elapsed, b.Local);
                }
                catch (KiLimitException) { limited = true; tried.Add($"{b.Name}: Menge leer"); }
                catch (Exception ex) when (ex is not OperationCanceledException) { tried.Add($"{b.Name}: {ex.Message}"); }
            }
        }
        _audit.Append("ki", "ki.unavailable", string.Join("; ", tried));
        var hint = limited
            ? "Die kostenlose Menge ist aufgebraucht. Trendo springt beim nächsten Mal weiter. Im KI-Laden kannst du ein Modell auf diesen PC holen."
            : req.Task == KiJob.Vision
                ? "Für Bilder ist noch kein Modell da. Im KI-Laden kannst du eines holen."
                : "Gerade ist keine KI erreichbar. Im KI-Laden und unter „KI-Quellen“ siehst du, was fehlt.";
        return new(false, hint, null, null, sources, sw.Elapsed);
    }

    private void Save(string q, string a, string backend, string model) =>
        File.AppendAllText(_historyFile, JsonSerializer.Serialize(new HistoryItem(DateTimeOffset.Now, q, a, backend, model)) + "\n");

    public sealed record HistoryItem(DateTimeOffset At, string Question, string Answer, string Backend, string Model);

    public IReadOnlyList<HistoryItem> History(int max = 50) => !File.Exists(_historyFile) ? [] :
        File.ReadAllLines(_historyFile).Where(l => l.Length > 0).Select(l => { try { return JsonSerializer.Deserialize<HistoryItem>(l); } catch { return null; } })
            .OfType<HistoryItem>().Reverse().Take(max).ToList();

    public void ClearHistory() { if (File.Exists(_historyFile)) File.Delete(_historyFile); _audit.Append("ki", "ki.history.cleared", ""); }
}
