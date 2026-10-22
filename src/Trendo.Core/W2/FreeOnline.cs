using System.Text.Json;

namespace Trendo.Core;

/// <summary>Das kostenlose Kontingent ist leer oder der Dienst verlangt Geld. Trendo springt dann weiter.</summary>
public sealed class KiLimitException(string message) : Exception(message);

/// <summary>Kostenloser Online-Dienst. Kein Schlüssel steht im Programm. Stand der Prüfung: 22.10.2026.</summary>
public sealed record FreeProvider(
    string Id, string Name, string KeyUrl, string CheckUrl, string ApiBase,
    string Note, string Limits, string[] Models, KiJob[] Jobs, int Quality);

/// <summary>Nur Dienste, die ohne Karte und ohne Kauf ein dauerhaftes oder monatliches Gratis-Kontingent haben.</summary>
public static class FreeOnline
{
    public static readonly IReadOnlyList<FreeProvider> Providers =
    [
        new("gemini", "Google Gemini", "https://aistudio.google.com/app/api-keys",
            "https://generativelanguage.googleapis.com/v1beta/openai/models",
            "https://generativelanguage.googleapis.com/v1beta/openai/",
            "Kostenloser Tarif von Google. Im Gratis-Tarif darf Google die Fragen nutzen, um seine Produkte zu verbessern.",
            "Die Grenzen stehen in deinem Google-Konto und können sich ändern. Es wird nichts gekauft.",
            ["gemini-2.5-flash", "gemini-2.5-flash-lite"], [KiJob.Text, KiJob.Code, KiJob.Vision], 70),
        new("groq", "Groq", "https://console.groq.com/keys",
            "https://api.groq.com/openai/v1/models", "https://api.groq.com/openai/v1/",
            "Schnelle Gratis-Modelle. Groq nutzt die Fragen nach eigenen Angaben nicht zum Trainieren.",
            "Etwa 30 Anfragen pro Minute und 1.000 am Tag, je Modell. Stand 22.10.2026, kann sich ändern.",
            ["openai/gpt-oss-120b", "qwen/qwen3.8-27b", "openai/gpt-oss-20b"], [KiJob.Text, KiJob.Code], 80),
        new("openrouter", "OpenRouter", "https://openrouter.ai/keys",
            "https://openrouter.ai/api/v1/models", "https://openrouter.ai/api/v1/",
            "Nur Modelle mit dem Zusatz „free“. Welcher Anbieter dahinter steht, kann wechseln.",
            "Ohne Guthaben etwa 20 Anfragen pro Minute und 50 am Tag. Ein leeres Konto kann trotzdem ablehnen.",
            ["openrouter/free"], [KiJob.Text, KiJob.Code], 50),
        new("mistral", "Mistral", "https://console.mistral.ai/api-keys",
            "https://api.mistral.ai/v1/models", "https://api.mistral.ai/v1/",
            "Kostenloser Tarif ohne Zahlungskarte. Wie viel Nutzung drin ist, nennt Mistral gerade nicht.",
            "Wenn das Guthaben leer ist, lehnt der Dienst ab. Trendo springt dann weiter. Es wird nichts gekauft.",
            ["mistral-small-latest"], [KiJob.Text, KiJob.Code], 60),
    ];

    /// <summary>Angefragt, aber nicht angeboten, weil es nicht wirklich kostenlos ist.</summary>
    public static readonly IReadOnlyList<(string Name, string Why)> LeftOut =
    [
        ("GitHub Models", "Der Dienst ist seit Juli 2026 abgeschaltet."),
        ("Cerebras", "Es gibt kein dauerhaft kostenloses Angebot. Neue Konten brauchen eine Zahlungskarte."),
        ("Hugging Face Inference", "Kostenlose Konten bekommen kein verlässliches Guthaben mehr."),
    ];

    public static string SecretKey(string id) => "TrendoOS:key-" + id;
}

/// <summary>Ausdrückliche Erlaubnis. Ohne Häkchen geht keine Frage ins Internet.</summary>
public sealed class OnlineOptIn
{
    public bool Allow { get; set; }
    public List<string> On { get; set; } = [];

    public bool Uses(string id) => Allow && On.Contains(id);

    static string FileOf(string root) => Path.Combine(root, "ki", "online.json");

    public static OnlineOptIn Load(string root)
    {
        try { return JsonSerializer.Deserialize<OnlineOptIn>(File.ReadAllText(FileOf(root))) ?? new(); }
        catch { return new(); }
    }

    public void Save(string root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf(root))!);
        File.WriteAllText(FileOf(root), JsonSerializer.Serialize(this));
    }

    public void Set(string id, bool on)
    {
        if (on) { if (!On.Contains(id)) On.Add(id); }
        else On.Remove(id);
    }
}

/// <summary>Lokale Quellen zuerst, danach freigeschaltete Online-Dienste nach Güte. Gleiche Güte behält die alte Reihenfolge.</summary>
public static class FreeAiRouter
{
    public static IReadOnlyList<IKiBackend> Order(IReadOnlyList<IKiBackend> backends)
        => backends.Select((b, i) => (b, i))
            .OrderByDescending(x => x.b.Local)
            .ThenByDescending(x => x.b.Quality)
            .ThenBy(x => x.i)
            .Select(x => x.b)
            .ToList();
}
