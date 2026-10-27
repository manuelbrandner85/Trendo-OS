namespace Trendo.Core;

/// <summary>Wunsch 27.10.2026: „Egal welche KI genutzt wird, soll der Fortschritt angezeigt werden.“ Eine laufende KI-Bearbeitung:
/// Bezeichnung, Phase, bisher geschriebene Zeichen, geschätzte Länge, Quelle und Modell, Abbruch.</summary>
public sealed class KiActivity
{
    internal readonly CancellationTokenSource Cts;
    internal KiActivity(string label, int expected, CancellationToken outer)
    {
        Label = label; ExpectedChars = expected; Cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
    }
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; }
    public string Phase { get; internal set; } = "Wird vorbereitet …";
    public string? Backend { get; internal set; }
    public string? Model { get; internal set; }
    public int Chars { get; internal set; }
    public int ExpectedChars { get; }
    public DateTime Started { get; } = DateTime.Now;
    public bool Streaming { get; internal set; }
    public CancellationToken Token => Cts.Token;
    public TimeSpan Elapsed => DateTime.Now - Started;
    public int Words { get; internal set; }

    /// <summary>Geschätzter Anteil (0…0,97), nur wenn die Antwort Stück für Stück kommt; sonst null (Balken ohne Prozent).</summary>
    public double? Percent => Streaming && ExpectedChars > 0 ? Math.Min(0.97, Chars / (double)ExpectedChars) : null;
    public void Cancel() { if (!Cts.IsCancellationRequested) Cts.Cancel(); }
}

/// <summary>Sammelstelle aller laufenden KI-Bearbeitungen. Die Oberfläche hört auf <see cref="Changed"/> und zeigt Phase, Wörter, Zeit und Schätzung.</summary>
public static class KiActivityHub
{
    static readonly object Gate = new();
    static readonly List<KiActivity> Live = [];
    static readonly Dictionary<string, (double Avg, int N)> Lengths = new(StringComparer.OrdinalIgnoreCase);
    static readonly AsyncLocal<string?> CurrentLabel = new();
    public static event Action? Changed;

    public static IReadOnlyList<KiActivity> Running { get { lock (Gate) return Live.ToList(); } }

    /// <summary>Bezeichnung für alle KI-Aufrufe in diesem Ablauf, z. B. „Brief verbessern“ (gilt auch über await hinweg).</summary>
    public static IDisposable Label(string label) { var before = CurrentLabel.Value; CurrentLabel.Value = label; return new Restore(before); }
    sealed class Restore(string? before) : IDisposable { public void Dispose() => CurrentLabel.Value = before; }

    /// <summary>Erwartete Antwortlänge aus den letzten Antworten derselben Art (sonst 900 Zeichen).</summary>
    public static int Estimate(string label) { lock (Gate) return Lengths.TryGetValue(label, out var l) && l.N > 0 ? (int)Math.Max(120, l.Avg) : 900; }

    public static KiActivity Begin(string? label = null, CancellationToken outer = default)
    {
        var l = label ?? CurrentLabel.Value ?? "KI-Antwort";
        var a = new KiActivity(l, Estimate(l), outer);
        lock (Gate) Live.Add(a);
        Changed?.Invoke(); return a;
    }

    public static void Update(KiActivity a, string? phase = null, string? delta = null, string? backend = null, string? model = null)
    {
        if (phase is not null) a.Phase = phase;
        if (backend is not null) a.Backend = backend;
        if (model is not null) a.Model = model;
        if (delta is not null)
        {
            a.Streaming = true; a.Chars += delta.Length;
            a.Words += delta.Count(char.IsWhiteSpace);
            a.Phase = $"schreibt … {Math.Max(1, a.Words)} Wörter";
        }
        Changed?.Invoke();
    }

    public static void End(KiActivity a, bool ok, int answerChars = 0)
    {
        lock (Gate)
        {
            Live.Remove(a);
            if (ok && answerChars > 0)
            {
                var (avg, n) = Lengths.TryGetValue(a.Label, out var x) ? x : (0, 0);
                n = Math.Min(n + 1, 20); avg += (answerChars - avg) / n; Lengths[a.Label] = (avg, n);
            }
        }
        a.Cts.Dispose();
        Changed?.Invoke();
    }

    /// <summary>Nur für Tests: alles zurücksetzen.</summary>
    public static void Reset() { lock (Gate) { Live.Clear(); Lengths.Clear(); } }
}

/// <summary>KI-Quelle, die die Antwort Stück für Stück liefert (für die Fortschrittsanzeige).</summary>
public interface IStreamingKiBackend : IKiBackend
{
    Task<string> ChatStreamAsync(string model, IReadOnlyList<KiMessage> messages, Action<string> onDelta, CancellationToken ct = default);
}
