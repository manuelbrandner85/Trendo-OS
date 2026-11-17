using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <param name="Images">T-0734: Bilder (Base64) zur Nachricht – nur für ein Bildmodell auf diesem PC.</param>
public sealed record KiMessage(string Role, string Content, IReadOnlyList<string>? Images = null);

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

/// <summary>Alle Studios, Runde 12 – X-05: Quelle, die ausweichen kann. Nur wenn <see cref="CanSwitchModel"/> gilt (alle Modelle kostenlos), darf Trendo bei einem ausgelasteten Modell ein anderes derselben Quelle versuchen.</summary>
public interface IKiFailoverBackend : IRemoteKiBackend { bool CanSwitchModel { get; } }

/// <summary>Das gewählte Modell kann gerade nicht antworten. <paramref name="AccountLimit"/>: Die Grenze gilt für das ganze Konto, ein anderes Modell hilft dann nicht.</summary>
public sealed class KiModelBusyException(string message, bool accountLimit) : Exception(message) { public bool AccountLimit { get; } = accountLimit; }

/// <summary>T-0316: vom Nutzer bewusst gewählte Haupt-KI außerhalb des PCs (Claude über das eigene Abo). Darf überall antworten, nicht nur im freien Chat –
/// bekommt aber wie jede Internet-KI keine Ausschnitte aus den eigenen Dokumenten.</summary>
public interface IMainKiBackend : IRemoteKiBackend { }

/// <param name="AllowRemote">Nur der freie Chat setzt das: dann darf ein eingeschalteter KI-Dienst außerhalb des PCs einspringen – ohne Verlauf und ohne eigene Dokumente. Abläufe, E-Mail, Agenten und Aufträge bleiben immer lokal.</param>
/// <param name="Attachments">T-0734: angehängte Dateien. Mit Anhang antworten nur die KI auf diesem PC oder die bewusst gewählte Haupt-KI (Claude über das Abo) – nie ein anderer KI-Dienst;
/// mit Bild nur ein Bildmodell auf diesem PC.</param>
/// <param name="OnText">T-0734: neue Stücke der Antwort, während sie entsteht; ein leerer Text heißt „neuer Versuch, Bisheriges verwerfen“.
/// Nur die Stücke, nicht der ganze Text je Stück (Gegenprüfung: sonst wächst die Arbeit quadratisch mit der Länge).</param>
public sealed record KiRequest(string Question, string? SystemPrompt = null, string? PreferredModel = null, IReadOnlyList<KiMessage>? History = null, bool UseKnowledge = true, bool AllowRemote = false, bool Personal = false,
    IReadOnlyList<KiAttachment>? Attachments = null, Action<string>? OnText = null);
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
    /// <summary>C3: Internet-KIs in allen Bereichen erlaubt (nicht nur im freien Chat)? Eigene Dokumente gehen trotzdem nie hinaus.</summary>
    public Func<bool>? RemoteEverywhere { get; set; }
    /// <summary>Gedächtnis der Person; nur bei Anfragen mit <see cref="KiRequest.Personal"/> und nur für die KI auf diesem PC.</summary>
    public KiMemoryStore? Memory { get; set; }
    /// <summary>C4: eine Antwort kam aus dem Internet (Name der Quelle) – für Hinweis und Tageszähler.</summary>
    public Action<string>? RemoteUsed { get; set; }
    /// <summary>T-0703: kleinere Modelle auf diesem PC, auf die Trendo ausweicht, wenn das gewählte nicht lädt oder bis zur Zeitgrenze nichts sagt (größtes zuerst).
    /// Null = kein Ausweichen (wie bisher).</summary>
    public Func<string, IReadOnlyList<string>>? LocalFallbacks { get; set; }
    /// <summary>Zeitgrenze bis zum ersten Lebenszeichen eines lokalen Modells (Laden + erstes Wort oder Nachdenken), nur wenn es ein Ausweichmodell gibt.</summary>
    public TimeSpan LocalFirstAnswerTimeout { get; set; } = KiModel.FirstAnswerTimeout;
    /// <summary>Ein lokales Modell hat nicht geladen oder war zu langsam (Modell, Grund) – die App merkt es sich (<see cref="KiModel.Demote"/>).</summary>
    public Action<string, string>? LocalModelFailed { get; set; }

    public IntelligenceCore(string dataRoot, AuditLog audit, Func<IReadOnlyList<IKiBackend>> backends, Func<string, int, IReadOnlyList<(string Title, string Text)>>? knowledge = null)
    {
        _audit = audit; _backends = backends; _knowledge = knowledge;
        _historyFile = Path.Combine(dataRoot, "ki", "verlauf.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(_historyFile)!);
    }

    /// <summary>Höchstens so viele kostenlose Modelle je Frage, damit eine Frage das Tageskontingent nicht aufbraucht.</summary>
    public const int MaxFreeModelTries = 3;

    /// <summary>Gegenprüfung T-0734: so viele Zeichen gehen höchstens an eine KI auf diesem PC – (16384 Token Kontext − 4096 für die Antwort) × 3 Zeichen je Token,
    /// siehe <c>KiBackendsShared.OllamaContext</c>. Darüber schneidet Ollama den Anfang still ab, auch die Systemanweisung.</summary>
    public const int LocalPromptChars = 36_000;
    /// <summary>So viel zählt ein Bild (grob 1500 Token).</summary>
    public const int ImageChars = 4_500;

    static string Cut(string s, int max, string why)
    {
        var tail = $"\n[… gekürzt, {why}]";
        return s.Length <= max ? s : s[..Math.Max(0, max - tail.Length)] + tail;
    }

    /// <summary>Sammelstand 0.9.104 (Restpunkt der Gegenprüfung T-0734): eine zu lange Frage in der Mitte kürzen – Anfang und Ende bleiben.
    /// Steht der eigentliche Auftrag hinter einem langen Text („… fasse das zusammen“), geht er so nicht verloren.</summary>
    public static string CutMiddle(string s, int max)
    {
        if (s.Length <= max) return s;
        const string mark = "\n[… Mitte gekürzt, damit Anfang, Auftrag und Anweisung vollständig bleiben …]\n";
        var keep = Math.Max(0, max - mark.Length);
        var head = keep * 2 / 3;
        return s[..head] + mark + s[^(keep - head)..];
    }

    /// <summary>Verlauf in den Platz <paramref name="budget"/> einpassen: Passt alles, bleibt alles unverändert. Sonst zählen die neuesten Nachrichten zuerst;
    /// die erste, die nicht mehr ganz passt, wird vorn behalten und gekürzt (z. B. ein großer Anhang), ältere fallen weg.</summary>
    public static List<KiMessage> FitHistory(IEnumerable<KiMessage> history, int budget)
    {
        var all = history.ToList();
        if (all.Sum(x => x.Content.Length) <= budget) return all;
        var keep = new List<KiMessage>(); var used = 0;
        for (var i = all.Count - 1; i >= 0; i--)
        {
            var x = all[i];
            if (used + x.Content.Length <= budget) { keep.Add(x); used += x.Content.Length; continue; }
            if (budget - used >= 500) keep.Add(x with { Content = Cut(x.Content, budget - used, "im Verlauf, damit die Anweisung vollständig bleibt") });
            break;
        }
        keep.Reverse();
        if (keep.Count > 0 && keep[0].Role == "assistant") keep.RemoveAt(0); // keine Antwort ohne ihre Frage
        return keep;
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
        if (req.Personal && Memory?.PromptBlock() is { Length: > 0 } remembered) sys.Append(remembered);
        // T-0734: angehängte Dateien – Text wird Teil der Frage, Bilder gehen als Bilddaten nur an ein Bildmodell auf diesem PC
        var files = req.Attachments ?? [];
        var images = files.Where(f => f.IsImage && f.Image is not null).Select(f => Convert.ToBase64String(f.Image!)).ToList();
        var question = KiAttachments.Compose(req.Question, files);
        List<KiMessage> Messages(string system)
        {
            // Gegenprüfung T-0734: für die KI auf dem PC begrenzen – sonst schneidet Ollama still vorn ab, auch die Systemanweisung
            var room = LocalPromptChars - system.Length - images.Count * ImageChars;
            var q = CutMiddle(question, Math.Max(2000, room - 1000));
            var m = new List<KiMessage> { new("system", system) };
            if (req.History is not null) m.AddRange(FitHistory(req.History.TakeLast(10), room - q.Length));
            m.Add(new("user", q, images.Count > 0 ? images : null));
            return m;
        }
        var msgs = Messages(sys.ToString());
        List<KiMessage> remoteMsgs = [new("system", baseSystem), new("user", question)]; // ohne Verlauf und ohne eigene Dokumente (angehängte Dateien hat die Person selbst mitgegeben)
        var tried = new List<string>();
        var noVision = false;
        KiActivityHub.Update(act, phase: "KI wird gesucht …");
        foreach (var b in _backends())
        {
            if (b is IRemoteKiBackend and not IMainKiBackend && !req.AllowRemote && RemoteEverywhere?.Invoke() != true) continue; // nur der freie Chat – oder überall, wenn eingestellt
            if (files.Count > 0 && b is IRemoteKiBackend and not IMainKiBackend) continue; // T-0734: Anhänge nie an fremde KI-Dienste
            if (images.Count > 0 && b is IRemoteKiBackend) continue; // T-0734: Bilder bleiben auf diesem PC
            if (!await b.IsUpAsync(ct)) { tried.Add($"{b.Name}: nicht erreichbar"); continue; }
            var models = await b.ModelsAsync(ct);
            if (images.Count > 0) { models = models.Where(KiAttachments.IsVisionModel).ToList(); if (models.Count == 0) { tried.Add($"{b.Name}: kein Bildmodell"); noVision = true; continue; } }
            var first = req.PreferredModel is { } p && models.Contains(p) ? p : models.FirstOrDefault();
            if (first is null) { tried.Add($"{b.Name}: kein Modell"); continue; }
            var local = b is not IRemoteKiBackend;
            // T-0703: auf diesem PC darf Trendo auf ein kleineres Modell ausweichen, wenn das große nicht lädt oder zu lange schweigt (bei Bildern nicht: kleinere Modelle sehen keine Bilder)
            List<string> fallbacks = local && images.Count == 0 && LocalFallbacks?.Invoke(first) is { Count: > 0 } fb ? fb.Where(m => m != first && models.Contains(m)).ToList() : [];
            IEnumerable<string> candidates = b is IKiFailoverBackend { CanSwitchModel: true } ? new[] { first }.Concat(models.Where(m => m != first)).Take(MaxFreeModelTries) : [first, .. fallbacks];
            foreach (var model in candidates)
            try
            {
                var remote = b is IRemoteKiBackend;
                KiActivityHub.Update(act, phase: remote ? $"🌐 Geht ins Internet zu {b.Name} ({model}) …" : $"{model} wird gestartet …", backend: remote ? "🌐 " + b.Name : b.Name, model: model);
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var guarded = local && fallbacks.Count > 0 && model != fallbacks[^1]; // das letzte Ausweichmodell darf so lange brauchen, wie es braucht
                if (guarded) limit.CancelAfter(LocalFirstAnswerTimeout);
                string text;
                req.OnText?.Invoke(""); // neuer Versuch: Mitschrift beginnt neu
                try
                {
                    text = b is IStreamingKiBackend sb
                        ? await sb.ChatStreamAsync(model, remote ? remoteMsgs : msgs, d =>
                        {
                            if (guarded) limit.CancelAfter(Timeout.InfiniteTimeSpan); // erstes Lebenszeichen: keine Zeitgrenze mehr
                            if (d.Length > 0) { KiActivityHub.Update(act, delta: d); req.OnText?.Invoke(d); }
                            else KiActivityHub.Update(act, phase: $"{model} denkt nach …");
                        }, limit.Token)
                        : await b.ChatAsync(model, remote ? remoteMsgs : msgs, limit.Token);
                }
                catch (OperationCanceledException) when (guarded && limit.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    throw new TimeoutException($"keine Antwort nach {LocalFirstAnswerTimeout.TotalSeconds:0} s");
                }
                _audit.Append("ki", "ki.ask", $"quelle={b.Name} modell={model} frage={req.Question.Length}Z antwort={text.Length}Z wissen={(remote ? 0 : sources.Count)}{(files.Count > 0 ? $" anhang={files.Count} bild={images.Count}" : "")}{(remote ? " extern" : "")}");
                Save(req.Question, text, b.Name, model);
                if (remote) RemoteUsed?.Invoke(b.Name);
                return new(true, text, b.Name, model, remote ? [] : sources, sw.Elapsed);
            }
            catch (KiModelBusyException ex) { tried.Add($"{b.Name} ({model}): {ex.Message}"); if (ex.AccountLimit) break; }
            catch (Exception ex) when (ex is not OperationCanceledException && local && fallbacks.Count > 0 && model != fallbacks[^1])
            {
                tried.Add($"{b.Name} ({model}): {ex.Message}");
                _audit.Append("ki", "ki.model.ausweichen", $"quelle={b.Name} modell={model} grund={ex.GetType().Name}");
                LocalModelFailed?.Invoke(model, ex.Message);
                KiActivityHub.Update(act, phase: $"{model} antwortet nicht – Trendo nimmt ein kleineres Modell …");
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { tried.Add($"{b.Name}: {ex.Message}"); break; }
        }
        _audit.Append("ki", "ki.unavailable", string.Join("; ", tried));
        if (noVision) return new(false, KiAttachments.NoVisionModel, null, null, sources, sw.Elapsed);
        return new(false,"Gerade ist keine KI auf diesem PC erreichbar. Unter „KI-Quellen“ siehst du, was fehlt, und kannst es einrichten.", null, null, sources, sw.Elapsed);
    }

    private void Save(string q, string a, string backend, string model) =>
        File.AppendAllText(_historyFile, JsonSerializer.Serialize(new HistoryItem(DateTimeOffset.Now, q, a, backend, model)) + "\n");

    public sealed record HistoryItem(DateTimeOffset At, string Question, string Answer, string Backend, string Model);

    public IReadOnlyList<HistoryItem> History(int max = 50) => !File.Exists(_historyFile) ? [] :
        File.ReadAllLines(_historyFile).Where(l => l.Length > 0).Select(l => { try { return JsonSerializer.Deserialize<HistoryItem>(l); } catch { return null; } })
            .OfType<HistoryItem>().Reverse().Take(max).ToList();

    public void ClearHistory() { if (File.Exists(_historyFile)) File.Delete(_historyFile); _audit.Append("ki", "ki.history.cleared", ""); }
}
