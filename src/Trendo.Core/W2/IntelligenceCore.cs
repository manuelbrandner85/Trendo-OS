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
}

/// <summary>Quelle außerhalb dieses PCs (KI-Dienst mit Schlüssel). Bekommt nie Ausschnitte aus den eigenen Dokumenten („Wissen“) – nur Frage und Verlauf.</summary>
public interface IRemoteKiBackend : IKiBackend { }

/// <param name="AllowRemote">Nur der freie Chat setzt das: dann darf ein eingeschalteter KI-Dienst außerhalb des PCs einspringen – ohne Verlauf und ohne eigene Dokumente. Abläufe, E-Mail, Agenten und Aufträge bleiben immer lokal.</param>
public sealed record KiRequest(string Question, string? SystemPrompt = null, string? PreferredModel = null, IReadOnlyList<KiMessage>? History = null, bool UseKnowledge = true, bool AllowRemote = false);
public sealed record KiAnswer(bool Ok, string Text, string? Backend, string? Model, IReadOnlyList<string> Sources, TimeSpan Took);

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
        if (string.IsNullOrWhiteSpace(req.Question)) return new(false, "Bitte schreib zuerst eine Frage.", null, null, [], TimeSpan.Zero);
        // Fortschritt für die Oberfläche (Wunsch 27.10.2026): Phase, Wörter, Zeit, Abbruch
        var act = KiActivityHub.Begin(outer: ct);
        KiAnswer? res = null;
        try { res = await AskCoreAsync(req, act); return res; }
        catch (OperationCanceledException) when (act.Token.IsCancellationRequested && !ct.IsCancellationRequested) { res = new(false, "Abgebrochen.", null, null, [], act.Elapsed); return res; }
        finally { KiActivityHub.End(act, res?.Ok == true, res?.Text.Length ?? 0); }
    }

    private async Task<KiAnswer> AskCoreAsync(KiRequest req, KiActivity act)
    {
        var ct = act.Token;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        KiActivityHub.Update(act, phase: "Wissen wird durchsucht …");
        var sources = new List<string>();
        var baseSystem = req.SystemPrompt ?? DefaultSystem;
        var sys = new StringBuilder(baseSystem);
        if (req.UseKnowledge && _knowledge is not null)
        {
            var hits = _knowledge(req.Question, 3);
            if (hits.Count > 0)
            {
                sys.Append("\n\nNutze dieses Wissen des Nutzers, wenn es passt, und nenne den Titel als Quelle:\n");
                foreach (var (t, x) in hits) { sys.Append($"### {t}\n{(x.Length > 1500 ? x[..1500] : x)}\n"); sources.Add(t); }
            }
        }
        List<KiMessage> Messages(string system)
        {
            var m = new List<KiMessage> { new("system", system) };
            if (req.History is not null) m.AddRange(req.History.TakeLast(10));
            m.Add(new("user", req.Question));
            return m;
        }
        var msgs = Messages(sys.ToString());
        List<KiMessage> remoteMsgs = [new("system", baseSystem), new("user", req.Question)]; // ohne Verlauf und ohne eigene Dokumente
        var tried = new List<string>();
        KiActivityHub.Update(act, phase: "KI wird gesucht …");
        foreach (var b in _backends())
        {
            if (b is IRemoteKiBackend && !req.AllowRemote) continue; // nur der freie Chat darf nach außen
            if (!await b.IsUpAsync(ct)) { tried.Add($"{b.Name}: nicht erreichbar"); continue; }
            var models = await b.ModelsAsync(ct);
            var model = req.PreferredModel is { } p && models.Contains(p) ? p : models.FirstOrDefault();
            if (model is null) { tried.Add($"{b.Name}: kein Modell"); continue; }
            try
            {
                var remote = b is IRemoteKiBackend;
                KiActivityHub.Update(act, phase: $"{model} wird gestartet …", backend: b.Name, model: model);
                var text = b is IStreamingKiBackend sb
                    ? await sb.ChatStreamAsync(model, remote ? remoteMsgs : msgs, d => KiActivityHub.Update(act, delta: d), ct)
                    : await b.ChatAsync(model, remote ? remoteMsgs : msgs, ct);
                _audit.Append("ki", "ki.ask", $"quelle={b.Name} modell={model} frage={req.Question.Length}Z antwort={text.Length}Z wissen={(remote ? 0 : sources.Count)}{(remote ? " extern" : "")}");
                Save(req.Question, text, b.Name, model);
                return new(true, text, b.Name, model, remote ? [] : sources, sw.Elapsed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { tried.Add($"{b.Name}: {ex.Message}"); }
        }
        _audit.Append("ki", "ki.unavailable", string.Join("; ", tried));
        return new(false, "Gerade ist keine KI auf diesem PC erreichbar. Unter „KI-Quellen“ siehst du, was fehlt, und kannst es einrichten.", null, null, sources, sw.Elapsed);
    }

    private void Save(string q, string a, string backend, string model) =>
        File.AppendAllText(_historyFile, JsonSerializer.Serialize(new HistoryItem(DateTimeOffset.Now, q, a, backend, model)) + "\n");

    public sealed record HistoryItem(DateTimeOffset At, string Question, string Answer, string Backend, string Model);

    public IReadOnlyList<HistoryItem> History(int max = 50) => !File.Exists(_historyFile) ? [] :
        File.ReadAllLines(_historyFile).Where(l => l.Length > 0).Select(l => { try { return JsonSerializer.Deserialize<HistoryItem>(l); } catch { return null; } })
            .OfType<HistoryItem>().Reverse().Take(max).ToList();

    public void ClearHistory() { if (File.Exists(_historyFile)) File.Delete(_historyFile); _audit.Append("ki", "ki.history.cleared", ""); }
}
