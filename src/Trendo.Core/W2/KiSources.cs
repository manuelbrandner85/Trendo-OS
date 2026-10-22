using System.Text.Json;

namespace Trendo.Core;

public enum KiProtocol { Ollama, OpenAiCompatible }

/// <summary>Bekannte, kostenlose KI-Programme, die auf diesem Gerät einen lokalen Server anbieten.</summary>
public sealed record KiSourceInfo(string Id, string Name, KiProtocol Protocol, string BaseUrl, string License, string OfficialUrl, string Hint);

public enum KiJob { Text, Code, Vision, Embed }

/// <summary>Freies Modell aus der Ollama-Bibliothek. Nur Einträge mit freier Nutzung stehen im Laden.</summary>
public sealed record FreeModel(string Tag, string Name, double SizeGb, int MinRamGb, string License, string GoodFor, KiJob Job = KiJob.Text, bool FreeToUse = true)
{
    public long Bytes => (long)(SizeGb * 1024d * 1024d * 1024d);
    public string Zweck => Job switch
    {
        KiJob.Code => "Programmieren",
        KiJob.Vision => "Bilder verstehen",
        KiJob.Embed => "Texte einordnen",
        _ => "Texte und Gespräche",
    };
}

/// <summary>X-05 Free AI Discovery & Fallback: Katalog freier lokaler Quellen und Modelle, Reihenfolge der Rückfallkette.</summary>
public static class KiCatalog
{
    public static readonly IReadOnlyList<KiSourceInfo> Sources =
    [
        new("ollama", "Ollama", KiProtocol.Ollama, "http://127.0.0.1:11434", "MIT", "https://ollama.com/download", "Standard in Trendo – wird unter „Zusatzprogramme“ mit einem Klick eingerichtet."),
        new("lmstudio", "LM Studio", KiProtocol.OpenAiCompatible, "http://127.0.0.1:1234", "kostenlos (eigene Bedingungen)", "https://lmstudio.ai", "In LM Studio unter „Developer“ den lokalen Server starten."),
        new("llamacpp", "llama.cpp-Server", KiProtocol.OpenAiCompatible, "http://127.0.0.1:8080", "MIT", "https://github.com/ggml-org/llama.cpp", "Mit „llama-server --host 127.0.0.1 --port 8080 -m modell.gguf“ starten. Nur dieser PC, kein offener Zugang."),
        new("jan", "Jan", KiProtocol.OpenAiCompatible, "http://127.0.0.1:1337", "Apache-2.0", "https://jan.ai", "In Jan unter Einstellungen den lokalen Server einschalten."),
        new("koboldcpp", "KoboldCpp", KiProtocol.OpenAiCompatible, "http://127.0.0.1:5001", "AGPL-3.0", "https://github.com/LostRuins/koboldcpp", "Startet den Server automatisch mit."),
    ];

    /// <summary>Geprüfte freie Modelle, Stand 22.10.2026. Größen sind ungefähre Angaben der Ollama-Bibliothek.</summary>
    public const string CatalogDate = "22.10.2026";

    public static readonly IReadOnlyList<FreeModel> Models =
    [
        new("qwen2.5:0.5b", "Qwen2.5 0.5B", 0.4, 4, "Apache-2.0", "sehr schnell, einfache Fragen"),
        new("qwen2.5:1.5b", "Qwen2.5 1.5B", 1.0, 6, "Apache-2.0", "alltagstauglich, sparsam"),
        new("qwen2.5:3b", "Qwen2.5 3B", 1.9, 8, "Apache-2.0", "gutes Deutsch, Alltag"),
        new("qwen2.5:7b", "Qwen2.5 7B", 4.7, 16, "Apache-2.0", "starkes Deutsch, Briefe"),
        new("qwen2.5:14b", "Qwen2.5 14B", 9.0, 24, "Apache-2.0", "sehr gutes Deutsch, braucht einen starken PC"),
        new("llama3.2:1b", "Llama 3.2 1B", 1.3, 6, "Llama 3.2 Community", "kurze Texte, sparsam"),
        new("llama3.2:3b", "Llama 3.2 3B", 2.0, 8, "Llama 3.2 Community", "Texte, Zusammenfassungen"),
        new("gemma2:2b", "Gemma 2 2B", 1.6, 8, "Gemma Terms", "kurze Antworten, sparsam"),
        new("gemma3:1b", "Gemma 3 1B", 0.8, 4, "Gemma Terms", "sehr klein, einfache Fragen"),
        new("gemma3:4b", "Gemma 3 4B", 3.3, 8, "Gemma Terms", "gutes Gleichgewicht"),
        new("mistral:7b", "Mistral 7B", 4.1, 16, "Apache-2.0", "längere Texte, mehr Wissen"),
        new("phi3:mini", "Phi-3 mini", 2.3, 8, "MIT", "knapp und schnell"),
        new("deepseek-r1:1.5b", "DeepSeek R1 1.5B", 1.1, 6, "MIT", "kurzes Nachdenken, klein"),
        new("qwen2.5-coder:0.5b", "Qwen2.5 Coder 0.5B", 0.4, 4, "Apache-2.0", "kleine Programme", KiJob.Code),
        new("qwen2.5-coder:1.5b", "Qwen2.5 Coder 1.5B", 1.0, 6, "Apache-2.0", "Skripte und Snippets", KiJob.Code),
        new("qwen2.5-coder:3b", "Qwen2.5 Coder 3B", 1.9, 8, "Apache-2.0", "Alltagsprogramme", KiJob.Code),
        new("qwen2.5-coder:7b", "Qwen2.5 Coder 7B", 4.7, 16, "Apache-2.0", "größere Programme", KiJob.Code),
        new("moondream", "Moondream", 1.7, 6, "Apache-2.0", "Bilder auf schwachen PCs", KiJob.Vision),
        new("qwen2.5vl:3b", "Qwen2.5 VL 3B", 3.2, 8, "Apache-2.0", "Bilder und Text darauf", KiJob.Vision),
        new("qwen2.5vl:7b", "Qwen2.5 VL 7B", 6.0, 16, "Apache-2.0", "genauere Bildbeschreibung", KiJob.Vision),
        new("all-minilm", "All-MiniLM", 0.05, 2, "Apache-2.0", "kleines Modell zum Einordnen", KiJob.Embed),
        new("nomic-embed-text", "Nomic Embed", 0.3, 2, "Apache-2.0", "Einordnen, etwas genauer", KiJob.Embed),
    ];

    public static bool Fits(FreeModel m, HardwareInfo h) => h.RamBytes >= m.MinRamGb * 1024L * 1024 * 1024 * 0.9;
}

/// <summary>Gespeicherte Rückfallkette: welche Quellen in welcher Reihenfolge benutzt werden.</summary>
public sealed class KiSourceSettings
{
    public List<string> Order { get; set; } = KiCatalog.Sources.Select(s => s.Id).ToList();
    public List<string> Disabled { get; set; } = [];

    static string FileOf(string root) => Path.Combine(root, "ki", "quellen.json");

    public static KiSourceSettings Load(string root)
    {
        try
        {
            var s = JsonSerializer.Deserialize<KiSourceSettings>(File.ReadAllText(FileOf(root))) ?? new();
            s.Order = s.Order.Where(id => KiCatalog.Sources.Any(x => x.Id == id)).Distinct().ToList();
            foreach (var x in KiCatalog.Sources) if (!s.Order.Contains(x.Id)) s.Order.Add(x.Id);
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

    public IReadOnlyList<KiSourceInfo> Active() => Order.Where(id => !Disabled.Contains(id)).Select(id => KiCatalog.Sources.First(s => s.Id == id)).ToList();
}
