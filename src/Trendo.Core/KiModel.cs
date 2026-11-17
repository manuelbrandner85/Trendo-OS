using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Ein in Ollama vorhandenes Modell (aus dem Manifest auf der Platte): Name mit Größe der Gewichte.</summary>
public sealed record InstalledModel(string Tag, long Bytes);

/// <summary>Gewähltes Standard-Modell für Chat und Agenten und der Grund in einem Satz (für die Oberfläche).</summary>
public sealed record ModelChoice(string Model, string Reason, bool ByUser);

/// <summary>Auswahl des lokalen KI-Modells. Gespeichert in &lt;DataRoot&gt;/ki-model.txt (Wahl der Person, hat immer Vorrang).
/// T-0703 (Freigabe 17.11.2026 „Bestes vorhandenes Modell als Standard“) – Regel für die automatische Wahl (<see cref="Best"/>):
/// <list type="number">
/// <item>Nur allgemeine Sprachmodelle: Spezialmodelle (Programmieren, Bilder verstehen, Einbettungen/Suche, Übersetzen) sind für den freien Chat
/// ausgeschlossen – erkannt über die Art in der KI-Bibliothek und über den Namen (<see cref="IsGeneralChat"/>).</item>
/// <item>Muss zum PC passen: mit Grafikkarte (ab 4 GB) höchstens 85 % des Grafikspeichers, sonst höchstens ein Viertel des Arbeitsspeichers (max. 5 GB,
/// der Prozessor rechnet große Modelle zu langsam); nie mehr als die Hälfte des Arbeitsspeichers.</item>
/// <item>Unter den passenden gewinnt das größte (mehr Parameter = bessere Antworten bei gleicher Quantisierung).</item>
/// <item>Schwacher PC (unter 15 GB Arbeitsspeicher und keine Grafikkarte ab 4 GB): das kleinste Modell bleibt Standard.</item>
/// <item>Hat ein Modell nicht geladen oder zu langsam geantwortet (<see cref="Demote"/>), überspringt die automatische Wahl es 7 Tage lang
/// und nimmt das nächstkleinere.</item>
/// </list></summary>
public static class KiModel
{
    public const long RecommendLargeFromBytes = 15L * 1024 * 1024 * 1024; // 16-GB-Geräte melden etwas weniger
    const double G = 1024.0 * 1024 * 1024;
    /// <summary>So lange überspringt die automatische Wahl ein Modell, das nicht geladen hat oder zu langsam war.</summary>
    public static readonly TimeSpan DemoteFor = TimeSpan.FromDays(7);
    /// <summary>Zeitgrenze bis zum ersten Lebenszeichen (Laden + erstes Wort oder Nachdenken); danach weicht Trendo auf ein kleineres Modell aus.</summary>
    public static readonly TimeSpan FirstAnswerTimeout = TimeSpan.FromSeconds(90);

    public static bool RecommendLarge(long totalRamBytes) => totalRamBytes >= RecommendLargeFromBytes;

    public static string Current(string dataRoot, long totalRamBytes, Func<string, bool> present)
    {
        var f = Path.Combine(dataRoot, "ki-model.txt");
        if (File.Exists(f))
        {
            var m = File.ReadAllText(f).Trim();
            if ((m == DependencyCatalog.LocalModel || m == DependencyCatalog.LargeModel) && present(m)) return m;
        }
        return RecommendLarge(totalRamBytes) && present(DependencyCatalog.LargeModel) ? DependencyCatalog.LargeModel : DependencyCatalog.LocalModel;
    }

    public static void Select(string dataRoot, string model, AuditLog audit)
    {
        if (model != DependencyCatalog.LocalModel && model != DependencyCatalog.LargeModel) throw new ArgumentException("unbekanntes Modell");
        File.WriteAllText(Path.Combine(dataRoot, "ki-model.txt"), model);
        audit.Append("owner", "ki.model_selected", model);
    }

    static readonly Regex TagRx = new(@"^[a-z0-9][a-z0-9._-]*(:[a-z0-9._-]+)?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Die Person wählt ein vorhandenes Modell selbst – es gilt dann überall, bis sie „Automatisch“ wählt.</summary>
    public static void SelectInstalled(string dataRoot, string model, IReadOnlyCollection<InstalledModel> have, AuditLog audit)
    {
        if (!TagRx.IsMatch(model) || have.All(h => h.Tag != model)) throw new ArgumentException("Dieses Modell ist auf dem PC nicht vorhanden.");
        File.WriteAllText(Path.Combine(dataRoot, "ki-model.txt"), model);
        audit.Append("owner", "ki.model_selected", model);
    }

    /// <summary>Zurück zur automatischen Wahl (eigene Wahl löschen).</summary>
    public static void SelectAuto(string dataRoot, AuditLog audit)
    {
        var f = Path.Combine(dataRoot, "ki-model.txt");
        if (File.Exists(f)) File.Delete(f);
        audit.Append("owner", "ki.model_selected", "automatisch");
    }

    /// <summary>Eigene Wahl der Person (oder null = automatisch).</summary>
    public static string? UserChoice(string dataRoot)
    {
        var f = Path.Combine(dataRoot, "ki-model.txt");
        try { return File.Exists(f) && File.ReadAllText(f).Trim() is { Length: > 0 } m && TagRx.IsMatch(m) ? m : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    static readonly Regex SpecialRx = new(@"(coder|codellama|codegemma|starcoder|embed|bge|minilm|vl$|-vl|vision|llava|moondream|minicpm-v|translate|guard|ocr|whisper)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Allgemeines Sprachmodell für Chat und Agenten? Nein bei Spezialmodellen (Programmieren, Bilder verstehen, Suche, Übersetzen).</summary>
    public static bool IsGeneralChat(string tag)
    {
        var name = tag.Split(':')[0];
        if (SpecialRx.IsMatch(name)) return false;
        var kinds = KiLibrary.Models.Where(m => m.Runtime == KiRuntime.Ollama && (m.Tag == tag || m.Tag == name)).Select(m => m.Kind).ToList();
        return kinds.Count == 0 || kinds.Any(k => k is KiKind.Chat or KiKind.Denken);
    }

    /// <summary>Größte Modelldatei, die auf diesem PC flüssig läuft (Regel siehe Klasse), ob es ein schwacher PC ist, und der Grund als Satzteil.</summary>
    public static (long Budget, bool Weak, string Why) Budget(long ramBytes, double vramGb)
    {
        var ram = ramBytes / G;
        if (vramGb >= 4) return ((long)(Math.Min(vramGb * 0.85, ram * 0.5) * G), false, $"in deinen Grafikspeicher ({vramGb:0} GB) passt");
        if (ramBytes < RecommendLargeFromBytes) return (0, true, $"dein PC wenig Arbeitsspeicher ({ram:0} GB) und keine starke Grafikkarte hat");
        return ((long)(Math.Min(ram * 0.25, 5) * G), false, $"zu deinem Arbeitsspeicher ({ram:0} GB, ohne starke Grafikkarte) passt");
    }

    /// <summary>Automatische Wahl aus den vorhandenen Modellen. <paramref name="skip"/>: Modelle, die zuletzt nicht geladen haben oder zu langsam waren.</summary>
    public static ModelChoice Best(IReadOnlyCollection<InstalledModel> have, long ramBytes, double vramGb, IReadOnlyCollection<string>? skip = null)
    {
        var chat = have.Where(h => IsGeneralChat(h.Tag)).OrderBy(h => h.Bytes).ToList();
        if (chat.Count == 0) return new(DependencyCatalog.LocalModel, "Es ist noch kein allgemeines Sprachmodell auf dem PC – Trendo nimmt das kleine Standardmodell, sobald es geladen ist.", false);
        var (budget, weak, why) = Budget(ramBytes, vramGb);
        var usable = chat.Where(h => skip?.Contains(h.Tag) != true).ToList();
        if (usable.Count == 0) usable = chat;
        if (weak)
        {
            var small = usable.FirstOrDefault(h => h.Tag == DependencyCatalog.LocalModel) ?? usable[0];
            return new(small.Tag, $"Das kleine Modell ist Standard, weil {why} – es antwortet hier am schnellsten.", false);
        }
        var best = usable.Where(h => h.Bytes <= budget).OrderByDescending(h => h.Bytes).FirstOrDefault();
        if (best is null) return new(usable[0].Tag, $"Kein größeres Modell passt zu diesem PC – darum das kleinste ({Size(usable[0].Bytes)}).", false);
        var skipped = chat.Where(h => skip?.Contains(h.Tag) == true && h.Bytes > best.Bytes).Select(h => h.Tag).ToList();
        return new(best.Tag, $"Das größte vorhandene allgemeine Sprachmodell ({Size(best.Bytes)}), das {why}." +
            (skipped.Count > 0 ? $" {string.Join(", ", skipped)} wird übersprungen, weil es zuletzt nicht geladen oder zu langsam geantwortet hat." : ""), false);
    }

    /// <summary>Standard für Chat und Agenten: eigene Wahl der Person, wenn das Modell vorhanden ist – sonst <see cref="Best"/>.</summary>
    public static ModelChoice Choose(string dataRoot, long ramBytes, double vramGb, IReadOnlyCollection<InstalledModel> have)
    {
        if (UserChoice(dataRoot) is { } own && have.Any(h => h.Tag == own))
            return new(own, "Von dir gewählt. „Automatisch“ nimmt wieder das beste passende Modell.", true);
        return Best(have, ramBytes, vramGb, Demoted(dataRoot));
    }

    /// <summary>Kleinere allgemeine Modelle zum Ausweichen, größtes zuerst (höchstens zwei; das kleinste ist immer dabei).</summary>
    public static IReadOnlyList<string> Fallbacks(string model, IReadOnlyCollection<InstalledModel> have)
    {
        var size = have.FirstOrDefault(h => h.Tag == model)?.Bytes ?? long.MaxValue;
        var smaller = have.Where(h => h.Tag != model && h.Bytes < size && IsGeneralChat(h.Tag)).OrderByDescending(h => h.Bytes).ToList();
        return smaller.Count <= 2 ? smaller.Select(h => h.Tag).ToList() : [smaller[0].Tag, smaller[^1].Tag];
    }

    static string DemoteFile(string dataRoot) => Path.Combine(dataRoot, "ki", "modell-ausweichen.json");

    /// <summary>Modell hat nicht geladen oder zu langsam geantwortet: die automatische Wahl überspringt es <see cref="DemoteFor"/> lang.</summary>
    public static void Demote(string dataRoot, string model, string reason, AuditLog audit)
    {
        var f = DemoteFile(dataRoot);
        var map = ReadDemotes(f);
        map[model] = DateTimeOffset.Now;
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        AtomicFile.WriteAllText(f, JsonSerializer.Serialize(map));
        audit.Append("ki", "ki.model.fallback", $"{model}: {reason}");
    }

    /// <summary>Modelle, die die automatische Wahl gerade überspringt.</summary>
    public static IReadOnlyCollection<string> Demoted(string dataRoot) =>
        ReadDemotes(DemoteFile(dataRoot)).Where(kv => DateTimeOffset.Now - kv.Value < DemoteFor).Select(kv => kv.Key).ToList();

    static Dictionary<string, DateTimeOffset> ReadDemotes(string f)
    {
        try { return File.Exists(f) ? JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(f)) ?? [] : []; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>Ablage der Manifeste je Ollama-Version: älter „manifests/registry.ollama.ai/library“, neuer zusätzlich „manifests-v2/ollama.com/library“.</summary>
    static readonly string[][] ManifestLibs = [["manifests", "registry.ollama.ai", "library"], ["manifests-v2", "ollama.com", "library"]];

    /// <summary>Vorhandene Ollama-Modelle aus den Manifesten (&lt;root&gt;/manifests/registry.ollama.ai/library/&lt;name&gt;/&lt;tag&gt;, neuere Ollama-Versionen
    /// auch manifests-v2/ollama.com/library) – ohne laufendes Ollama. Größe = Summe der Gewichte-Schichten (application/vnd.ollama.image.model und .projector).</summary>
    public static IReadOnlyList<InstalledModel> ScanManifests(IEnumerable<string> modelRoots)
    {
        var res = new Dictionary<string, InstalledModel>();
        foreach (var root in modelRoots)
            foreach (var parts in ManifestLibs)
            {
                var lib = Path.Combine([root, .. parts]);
                if (!Directory.Exists(lib)) continue;
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(lib))
                        foreach (var file in Directory.EnumerateFiles(dir))
                        {
                            var tag = Path.GetFileName(dir) + ":" + Path.GetFileName(file);
                            if (res.ContainsKey(tag)) continue;
                            try
                            {
                                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                                long bytes = 0;
                                if (doc.RootElement.TryGetProperty("layers", out var layers))
                                    foreach (var l in layers.EnumerateArray())
                                        if (l.TryGetProperty("mediaType", out var mt) && mt.GetString() is "application/vnd.ollama.image.model" or "application/vnd.ollama.image.projector" && l.TryGetProperty("size", out var s))
                                            bytes += s.GetInt64();
                                if (bytes > 0) res[tag] = new(tag, bytes);
                            }
                            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException or FormatException) { }
                        }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        return res.Values.OrderBy(m => m.Tag, StringComparer.Ordinal).ToList();
    }

    /// <summary>T-0712: Ordner, in denen Ollama Modelle ablegen kann, in dieser Reihenfolge und ohne Doppelte:
    /// OLLAMA_MODELS aus der Prozessumgebung, OLLAMA_MODELS aus der Benutzerumgebung (nur gelesen – gilt für ein Ollama, das nach dem Setzen gestartet wurde),
    /// der Standardordner ~/.ollama/models und Trendos eigener Ordner &lt;Werkzeuge&gt;/model (dorthin zeigt Trendo sein Ollama, <see cref="Vollpaket.ConfigureOllama"/>).
    /// <paramref name="env"/> ersetzt das Lesen der Umgebung (Tests).</summary>
    public static IReadOnlyList<string> ModelRoots(string toolsRoot, Func<string, EnvironmentVariableTarget, string?>? env = null)
    {
        env ??= ReadEnv;
        var list = new List<string>();
        void Add(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            try { p = Path.GetFullPath(Environment.ExpandEnvironmentVariables(p.Trim().Trim('"'))); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return; }
            if (!list.Any(x => string.Equals(x.TrimEnd('\\', '/'), p.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))) list.Add(p);
        }
        Add(env("OLLAMA_MODELS", EnvironmentVariableTarget.Process));
        if (OperatingSystem.IsWindows()) Add(env("OLLAMA_MODELS", EnvironmentVariableTarget.User));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama", "models"));
        Add(Path.Combine(toolsRoot, "model"));
        return list;
    }

    static string? ReadEnv(string name, EnvironmentVariableTarget target)
    {
        try { return Environment.GetEnvironmentVariable(name, target); }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return null; }
    }

    /// <summary>Antwort von GET /api/tags (lokales Ollama) → Modelle mit Größe. Null bei unlesbarer Antwort.</summary>
    public static IReadOnlyList<InstalledModel>? ParseTags(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array) return null;
            var res = new Dictionary<string, InstalledModel>();
            foreach (var m in models.EnumerateArray())
            {
                var name = m.TryGetProperty("name", out var n) ? n.GetString() : m.TryGetProperty("model", out var n2) ? n2.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!name.Contains(':')) name += ":latest";
                var size = m.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
                if (size > 0) res[name] = new(name, size);
            }
            return res.Values.OrderBy(m => m.Tag, StringComparer.Ordinal).ToList();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    /// <summary>Woher die Modellliste stammt und was dabei versucht wurde (für Beleg und Protokoll).</summary>
    public sealed record ModelScan(IReadOnlyList<InstalledModel> Models, string Source, string Detail);

    /// <summary>T-0712: vorhandene Modelle ermitteln. Reihenfolge: 1. das laufende Ollama selbst (<paramref name="api"/>, GET /api/tags) –
    /// es kennt genau die Modelle, die es auch laden kann, egal in welchem Ordner sie liegen; 2. ist Ollama nicht erreichbar (null) oder meldet es
    /// keine Modelle, die Manifeste in <paramref name="roots"/> (erster Ordner mit Modellen zuerst, Doppelte zählen einmal).</summary>
    public static ModelScan Discover(Func<IReadOnlyList<InstalledModel>?>? api, IReadOnlyList<string> roots)
    {
        var apiNote = "Ollama-API nicht gefragt";
        if (api is not null)
        {
            IReadOnlyList<InstalledModel>? fromApi = null;
            try { fromApi = api(); } catch (Exception e) when (e is TaskCanceledException or OperationCanceledException or IOException or AggregateException || e.GetType().Name == "HttpRequestException") { } // QA-019: Typ nur über den Namen
            if (fromApi is { Count: > 0 }) return new(fromApi.OrderBy(m => m.Tag, StringComparer.Ordinal).ToList(), "ollama-api", $"{fromApi.Count} Modelle von Ollama (GET /api/tags)");
            apiNote = fromApi is null ? "Ollama-API nicht erreichbar" : "Ollama-API meldet keine Modelle";
        }
        var found = ScanManifests(roots);
        var withModels = roots.Where(r => ScanManifests([r]).Count > 0).ToList();
        return new(found, found.Count > 0 ? "manifeste" : "keine",
            $"{apiNote}; Manifeste: {found.Count} Modelle" + (withModels.Count > 0 ? " in " + string.Join(", ", withModels) : $" (gesucht in {roots.Count} Ordnern)"));
    }

    static string Size(long b) => b >= G ? (b / G).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " GB" : $"{b / (1024 * 1024)} MB";
}
