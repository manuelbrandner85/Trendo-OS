using System.Text.Json;

namespace Trendo.Core;

public enum KiProtocol { Ollama, OpenAiCompatible }

/// <summary>Bekannte, kostenlose KI-Programme, die auf diesem Gerät einen lokalen Server anbieten.</summary>
public sealed record KiSourceInfo(string Id, string Name, KiProtocol Protocol, string BaseUrl, string License, string OfficialUrl, string Hint);

public sealed record FreeModel(string Tag, string Name, double SizeGb, int MinRamGb, string License, string GoodFor);

/// <summary>KI-Dienst im Internet, der einen eigenen API-Schlüssel verlangt (OpenAI-kompatible Schnittstelle).
/// Trendo leitet zum Anbieter, der Schlüssel wird in Trendo eingetragen. Freiwillig – Trendo funktioniert immer ohne.</summary>
public sealed record KiCloudService(string Id, string Name, string BaseUrl, string KeyUrl, string DefaultModel, bool FreeTier, string Hint)
{
    public string SecretName => "TrendoOS:ki-" + Id;
    public string Host => new Uri(BaseUrl).Host;
}

/// <summary>X-05 Free AI Discovery & Fallback: Katalog freier lokaler Quellen und Modelle, Reihenfolge der Rückfallkette.</summary>
public static class KiCatalog
{
    public static readonly IReadOnlyList<KiSourceInfo> Sources =
    [
        new("ollama", "Ollama", KiProtocol.Ollama, "http://127.0.0.1:11434", "MIT", "https://ollama.com/download", "Standard in Trendo – wird unter „Zusatzprogramme“ mit einem Klick eingerichtet."),
        new("lmstudio", "LM Studio", KiProtocol.OpenAiCompatible, "http://127.0.0.1:1234", "kostenlos (eigene Bedingungen)", "https://lmstudio.ai", "In LM Studio unter „Developer“ den lokalen Server starten."),
        new("llamacpp", "llama.cpp-Server", KiProtocol.OpenAiCompatible, "http://127.0.0.1:8080", "MIT", "https://github.com/ggml-org/llama.cpp", "Mit „llama-server -m modell.gguf“ starten."),
        new("jan", "Jan", KiProtocol.OpenAiCompatible, "http://127.0.0.1:1337", "Apache-2.0", "https://jan.ai", "In Jan unter Einstellungen den lokalen Server einschalten."),
        new("koboldcpp", "KoboldCpp", KiProtocol.OpenAiCompatible, "http://127.0.0.1:5001", "AGPL-3.0", "https://github.com/LostRuins/koboldcpp", "Startet den Server automatisch mit."),
    ];

    public static readonly IReadOnlyList<FreeModel> Models =
    [
        new("qwen2.5:0.5b", "Qwen2.5 0.5B", 0.4, 4, "Apache-2.0", "sehr schnell, einfache Fragen"),
        new("qwen2.5:3b", "Qwen2.5 3B", 1.9, 8, "Qwen Research", "gutes Deutsch, Alltag"),
        new("llama3.2:3b", "Llama 3.2 3B", 2.0, 8, "Llama 3.2 Community", "Texte, Zusammenfassungen"),
        new("gemma2:2b", "Gemma 2 2B", 1.6, 8, "Gemma Terms", "kurze Antworten, sparsam"),
        new("mistral:7b", "Mistral 7B", 4.1, 16, "Apache-2.0", "längere Texte, mehr Wissen"),
        new("qwen2.5:7b", "Qwen2.5 7B", 4.7, 16, "Apache-2.0", "starkes Deutsch, Briefe"),
        new("qwen2.5-coder:7b", "Qwen2.5 Coder 7B", 4.7, 16, "Apache-2.0", "Programmieren"),
    ];

    /// <summary>KI-Dienste mit Schlüssel (Stand 2026-10-25, offizielle Adressen der Anbieter).</summary>
    public static readonly IReadOnlyList<KiCloudService> Cloud =
    [
        new("openrouter", "OpenRouter", "https://openrouter.ai/api/v1", "https://openrouter.ai/settings/keys", ":free", true, "Viele Modelle, darunter kostenlose (Endung „:free“). Kostenloses Konto genügt."),
        new("gemini", "Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", "https://aistudio.google.com/apikey", "gemini-2.5-flash", true, "Kostenlose Stufe mit Google-Konto; Google darf Eingaben der kostenlosen Stufe auswerten."),
        new("mistral", "Mistral AI", "https://api.mistral.ai/v1", "https://console.mistral.ai/api-keys", "mistral-small-latest", true, "Europäischer Anbieter; kostenlose Stufe „Experiment“ nach Anmeldung."),
        new("huggingface", "Hugging Face", "https://router.huggingface.co/v1", "https://huggingface.co/settings/tokens", "meta-llama/Llama-3.1-8B-Instruct", true, "Kostenloses Konto mit monatlichem Guthaben; Token mit Recht „Inference“ anlegen."),
        new("anthropic", "Claude (Anthropic)", "https://api.anthropic.com/v1", "https://console.anthropic.com/settings/keys", "claude-haiku-4-5", false, "Kostenpflichtig je Nutzung – nur, wenn du das willst."),
        new("openai", "OpenAI", "https://api.openai.com/v1", "https://platform.openai.com/api-keys", "gpt-4.1-mini", false, "Kostenpflichtig je Nutzung – nur, wenn du das willst."),
    ];

    /// <summary>Freie Lizenz ohne eigene Zusatzbedingungen: darf Trendo ohne Rückfrage laden (Apache-2.0, MIT).</summary>
    public static bool AutoLicense(FreeModel m) => m.License is "Apache-2.0" or "MIT";

    /// <summary>Modelle, die Trendo nach dem Setup automatisch lädt: freie Lizenz, passt zum Arbeitsspeicher, noch nicht vorhanden. Kleinste zuerst.</summary>
    public static List<FreeModel> AutoModels(HardwareInfo h, IReadOnlyCollection<string> have) =>
        Models.Where(m => AutoLicense(m) && Fits(m, h) && !have.Contains(m.Tag)).OrderBy(m => m.SizeGb).ToList();

    public static bool Fits(FreeModel m, HardwareInfo h) => h.RamBytes >= m.MinRamGb * 1024L * 1024 * 1024 * 0.9;
}

/// <summary>Gespeicherte Rückfallkette: welche Quellen in welcher Reihenfolge benutzt werden.</summary>
public sealed class KiSourceSettings
{
    public List<string> Order { get; set; } = KiCatalog.Sources.Select(s => s.Id).ToList();
    public List<string> Disabled { get; set; } = [];
    /// <summary>KI-Dienste mit Schlüssel, die als Ersatz genutzt werden dürfen (nach den lokalen Quellen, in dieser Reihenfolge).</summary>
    public List<string> Cloud { get; set; } = [];
    /// <summary>Freie Modelle nach dem Setup automatisch laden (Vorgabe Manuel 2026-10-25).</summary>
    public bool AutoModels { get; set; } = true;
    /// <summary>Fehlversuche je Modell beim automatischen Laden; nach 3 Versuchen nur noch per Knopf.</summary>
    public Dictionary<string, int> AutoFailed { get; set; } = [];

    static string FileOf(string root) => Path.Combine(root, "ki", "quellen.json");

    public static KiSourceSettings Load(string root)
    {
        try
        {
            var s = JsonSerializer.Deserialize<KiSourceSettings>(File.ReadAllText(FileOf(root))) ?? new();
            s.Order = s.Order.Where(id => KiCatalog.Sources.Any(x => x.Id == id)).Distinct().ToList();
            foreach (var x in KiCatalog.Sources) if (!s.Order.Contains(x.Id)) s.Order.Add(x.Id);
            s.Cloud = s.Cloud.Where(id => KiCatalog.Cloud.Any(c => c.Id == id)).Distinct().ToList();
            return s;
        }
        catch { return new(); }
    }

    public void Save(string root) { Directory.CreateDirectory(Path.GetDirectoryName(FileOf(root))!); File.WriteAllText(FileOf(root), JsonSerializer.Serialize(this)); }

    public void Move(string id, int delta)
    {
        var i = Order.IndexOf(id); var j = i + delta;
        if (i < 0 || j < 0 || j >= Order.Count) return;
        (Order[i], Order[j]) = (Order[j], Order[i]);
    }

    public void Toggle(string id) { if (!Disabled.Remove(id)) Disabled.Add(id); }

    public void ToggleCloud(string id) { if (KiCatalog.Cloud.All(c => c.Id != id)) return; if (!Cloud.Remove(id)) Cloud.Add(id); }

    public IReadOnlyList<KiSourceInfo> Active() => Order.Where(id => !Disabled.Contains(id)).Select(id => KiCatalog.Sources.First(s => s.Id == id)).ToList();
}
