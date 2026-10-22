using System.Text.Json;

namespace Trendo.Core;

/// <summary>Adressen nur auf diesem PC. Ollama und llama.cpp dürfen nicht ins Netz hören.</summary>
public static class LoopbackAi
{
    public const string OllamaHost = "127.0.0.1:11434";
    public const string LlamaHost = "127.0.0.1";
    public const int LlamaPort = 8080;

    public static string LlamaArgs(string modelFile) => $"--host {LlamaHost} --port {LlamaPort} -m \"{modelFile}\"";

    /// <summary>Dienst nur auf diesem PC, ohne Ollama-Cloud. Gilt für den Prozess, den Trendo selbst startet.</summary>
    public static void BindLocal(IDictionary<string, string?> env)
    {
        env["OLLAMA_HOST"] = OllamaHost;
        env["OLLAMA_NO_CLOUD"] = "1";
    }

    public static bool IsLoopback(string host)
    {
        var h = host.Trim();
        return h.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || h.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
            || h.StartsWith("[::1]", StringComparison.OrdinalIgnoreCase)
            || h == "::1";
    }
}

/// <summary>Liest die Linux-Datei aus einer GitHub-Release-Antwort. Kein Download.</summary>
public static class OllamaRelease
{
    /// <summary>Aktuelle Veröffentlichung ist .tar.zst (Stand 22.10.2026, v0.40.8). Ältere .tgz bleibt gültig.</summary>
    public static string? LinuxTar(string json, System.Runtime.InteropServices.Architecture arch)
    {
        var names = arch == System.Runtime.InteropServices.Architecture.Arm64
            ? new[] { "ollama-linux-arm64.tar.zst", "ollama-linux-arm64.tgz" }
            : new[] { "ollama-linux-amd64.tar.zst", "ollama-linux-amd64.tgz" };
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("assets", out var assets)) return null;
            foreach (var want in names)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!string.Equals(name, want, StringComparison.OrdinalIgnoreCase)) continue;
                    var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (url is not null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return url;
                }
            }
        }
        catch (JsonException) { }
        return null;
    }

    public static bool IsZst(string url)
        => url.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".zst", StringComparison.OrdinalIgnoreCase);
}

public readonly record struct DiskVerdict(bool Ok, string Message, long NeedBytes, long FreeBytes);

/// <summary>Welche freien Modelle zu diesem PC passen. Schwache PCs bleiben bei kleinen Modellen.</summary>
public static class LocalAiPlan
{
    const long G = 1024L * 1024 * 1024;

    public static bool FitsPc(FreeModel m, HardwareInfo h)
    {
        if (!m.FreeToUse) return false;
        if (KiCatalog.Fits(m, h)) return true;
        return h.VramBytes > 0 && h.RamBytes >= 4 * G && h.VramBytes >= (long)(m.SizeGb * 1.3 * G);
    }

    public static (string Tier, string Why) TierOf(HardwareInfo h)
    {
        var ram = h.RamBytes / (double)G;
        var vram = h.VramBytes / (double)G;
        var cpu = h.CpuThreads > 0 ? $", {h.CpuThreads} Kerne" : "";
        var gpu = string.IsNullOrWhiteSpace(h.Gpu) ? "keine eigene Grafikkarte" : $"{h.Gpu}{(vram >= 0.5 ? $" ({vram:0} GB)" : "")}";
        if (ram >= 24 || vram >= 8) return ("groß", $"{ram:0} GB Arbeitsspeicher{cpu}, {gpu}. Ein größeres Modell passt.");
        if (ram >= 12 || vram >= 4) return ("mittel", $"{ram:0} GB Arbeitsspeicher{cpu}, {gpu}. Ein mittleres Modell passt.");
        return ("klein", $"{ram:0} GB Arbeitsspeicher{cpu}, {gpu}. Es bleiben die kleinen Modelle.");
    }

    public static IReadOnlyList<FreeModel> Recommend(HardwareInfo h)
    {
        var (tier, _) = TierOf(h);
        return new[] { KiJob.Text, KiJob.Code, KiJob.Vision, KiJob.Embed }
            .Select(job => Pick(h, job, tier)).OfType<FreeModel>().ToList();
    }

    public static FreeModel? Pick(HardwareInfo h, KiJob job, string? tier = null)
    {
        tier ??= TierOf(h).Tier;
        var fit = KiCatalog.Models.Where(m => m.Job == job && m.FreeToUse && FitsPc(m, h)).ToList();
        if (fit.Count == 0) return null;
        if (tier == "groß") return fit.OrderByDescending(m => m.SizeGb).First();
        if (tier == "klein")
        {
            var band = fit.Where(m => m.SizeGb is >= 0.8 and <= 4).ToList();
            return (band.Count > 0 ? band : fit).OrderByDescending(m => m.SizeGb).First();
        }
        return fit.OrderBy(m => Math.Abs(m.SizeGb - 3)).First();
    }

    public static string Sentence(HardwareInfo h)
    {
        var rec = Recommend(h);
        string One(KiJob job)
        {
            var m = rec.FirstOrDefault(x => x.Job == job);
            return m is null ? "keins, zu wenig Speicher" : $"{m.Name} (ca. {m.SizeGb:0.#} GB)";
        }
        return $"Für diesen PC: Texte {One(KiJob.Text)}. Programmieren {One(KiJob.Code)}. Bilder {One(KiJob.Vision)}. Einordnen {One(KiJob.Embed)}.";
    }

    public static string FitText(FreeModel m, HardwareInfo h)
    {
        if (!m.FreeToUse) return "Diese Lizenz laden wir nicht.";
        return FitsPc(m, h) ? "passt zu diesem PC" : $"zu groß für diesen PC (braucht {m.MinRamGb} GB Arbeitsspeicher)";
    }

    public static DiskVerdict CheckDisk(HardwareInfo h, long freeBytes, Func<string, bool> present)
        => CheckList(Recommend(h), freeBytes, present);

    public static DiskVerdict CheckList(IReadOnlyList<FreeModel> models, long freeBytes, Func<string, bool> present)
    {
        var need = models.Where(m => !present(m.Tag)).Sum(m => m.Bytes);
        var reserve = G;
        if (need == 0) return new(true, "Die empfohlenen Modelle sind schon da.", 0, freeBytes);
        if (freeBytes < need + reserve)
        {
            var miss = (need + reserve - freeBytes) / (double)G;
            return new(false, $"Zu wenig Platz. Es fehlen etwa {miss:0.0} GB. Es wird nichts geladen.", need, freeBytes);
        }
        return new(true, $"Platz reicht. Es werden etwa {need / (double)G:0.0} GB geladen. Du kannst abbrechen und später weitermachen.", need, freeBytes);
    }

    /// <summary>Reihenfolge der Modelle, die für diese Aufgabe schon da sind. Größeres zuerst, das Wunschmodell bei Text ganz vorn.</summary>
    public static List<string> Candidates(IReadOnlyList<string> have, KiRequest req)
    {
        var list = new List<string>();
        void Add(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || !have.Contains(tag) || list.Contains(tag)) return;
            list.Add(tag);
        }
        if (req.Task == KiJob.Text) Add(req.PreferredModel);
        foreach (var m in KiCatalog.Models.Where(m => m.Job == req.Task && m.FreeToUse).OrderByDescending(m => m.SizeGb)) Add(m.Tag);
        if (req.Task != KiJob.Text) Add(req.PreferredModel);
        foreach (var m in have) Add(m);
        return list;
    }

    public static string SafeId(string tag) => string.Concat(tag.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-'));

    public static string ActiveLine(string? backend, string? model, bool onThisPc, KiJob job)
    {
        var what = job switch { KiJob.Code => "Programmieren", KiJob.Vision => "Bilder", KiJob.Embed => "Einordnen", _ => "Texte" };
        if (string.IsNullOrWhiteSpace(backend)) return "Gerade aktiv: keine KI";
        return onThisPc
            ? $"Gerade aktiv: {model} auf diesem PC ({what})"
            : $"Gerade aktiv: {backend} ({what}) – kostenlos, die Frage ging ins Internet";
    }
}

/// <summary>Merkt sich halbfertige Downloads, damit Abbrechen und Weitermachen über einen Neustart gehen.</summary>
public sealed class DownloadBook
{
    public List<Item> Items { get; set; } = [];

    public sealed class Item
    {
        public string Tag { get; set; } = "";
        public string Phase { get; set; } = "geplant";
        public double Fraction { get; set; }
        public string Message { get; set; } = "";
    }

    public static string PathOf(string root) => Path.Combine(root, "ki", "downloads.json");

    public static DownloadBook Load(string root)
    {
        try { return JsonSerializer.Deserialize<DownloadBook>(File.ReadAllText(PathOf(root))) ?? new(); }
        catch { return new(); }
    }

    public void Save(string root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(root))!);
        File.WriteAllText(PathOf(root), JsonSerializer.Serialize(this));
    }

    public Item Get(string tag)
    {
        var i = Items.FirstOrDefault(x => x.Tag == tag);
        if (i is not null) return i;
        Items.Add(i = new Item { Tag = tag });
        return i;
    }

    public void Plan(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            var i = Get(tag);
            if (i.Phase is "fertig") continue;
            i.Phase = "geplant";
        }
    }

    public void Start(string tag) { var i = Get(tag); i.Phase = "läuft"; i.Fraction = 0; i.Message = "Lädt …"; }
    public void Tick(string tag, double fraction, string message) { var i = Get(tag); i.Phase = "läuft"; i.Fraction = Math.Clamp(fraction, 0, 1); i.Message = message; }
    public void Done(string tag, string message) { var i = Get(tag); i.Phase = "fertig"; i.Fraction = 1; i.Message = message; }
    public void Pause(string tag, string message) { var i = Get(tag); if (i.Phase != "fertig") { i.Phase = "pause"; i.Message = message; } }
    public void Fail(string tag, string message) { var i = Get(tag); i.Phase = "fehler"; i.Message = message; }
    public bool IsDone(string tag) => Items.Any(i => i.Tag == tag && i.Phase == "fertig");

    public string Summary()
    {
        var ok = Items.Count(i => i.Phase == "fertig");
        var open = Items.Where(i => i.Phase is "fehler" or "pause" or "geplant").Select(i => i.Tag).ToList();
        return open.Count == 0 ? $"{ok} Modelle sind bereit." : $"{ok} bereit. Noch offen: {string.Join(", ", open)}";
    }
}

public readonly record struct FetchTick(double Fraction, string Status);

/// <summary>Meldet den Fortschritt sofort, ohne ihn auf einen späteren Zeitpunkt zu verschieben.</summary>
public sealed class InlineProgress(Action<FetchTick> on) : IProgress<FetchTick>
{
    public void Report(FetchTick value) => on(value);
}

/// <summary>Lädt die empfohlenen Modelle der Reihe nach. Bricht sauber ab und macht später weiter.</summary>
public static class ModelFetch
{
    public static Task<string> Run(string root, HardwareInfo hw, long freeBytes, Func<string, bool> present, Func<string, IProgress<FetchTick>, CancellationToken, Task<bool>> pull, CancellationToken ct)
        => Run(root, hw, freeBytes, present, pull, ct, null);

    public static async Task<string> Run(string root, HardwareInfo hw, long freeBytes, Func<string, bool> present, Func<string, IProgress<FetchTick>, CancellationToken, Task<bool>> pull, CancellationToken ct, IReadOnlyList<string>? only)
    {
        var models = only is null
            ? LocalAiPlan.Recommend(hw).ToList()
            : only.Select(t => KiCatalog.Models.FirstOrDefault(m => m.Tag == t)).OfType<FreeModel>().Where(m => m.FreeToUse).ToList();
        var disk = LocalAiPlan.CheckList(models, freeBytes, present);
        var book = DownloadBook.Load(root);
        if (!disk.Ok) { book.Save(root); return disk.Message; }
        var tags = models.Select(m => m.Tag).ToList();
        book.Plan(tags);
        book.Save(root);
        foreach (var tag in tags)
        {
            if (present(tag)) { book.Done(tag, "schon da"); book.Save(root); continue; }
            if (ct.IsCancellationRequested)
            {
                foreach (var rest in tags.Where(t => !book.IsDone(t))) book.Pause(rest, "Angehalten. Du kannst später weitermachen.");
                book.Save(root);
                return "Angehalten. Du kannst später weitermachen.";
            }
            book.Start(tag); book.Save(root);
            var saved = DateTime.UtcNow;
            try
            {
                var ok = await pull(tag, new InlineProgress(t =>
                {
                    book.Tick(tag, t.Fraction, t.Status);
                    var now = DateTime.UtcNow;
                    if ((now - saved).TotalMilliseconds > 400 || t.Fraction >= 1) { saved = now; book.Save(root); }
                }), ct);
                if (ok) book.Done(tag, "fertig");
                else book.Fail(tag, "Hat nicht geklappt. Du kannst es erneut versuchen.");
            }
            catch (OperationCanceledException)
            {
                book.Pause(tag, "Angehalten. Du kannst später weitermachen.");
                foreach (var rest in tags.Where(t => !book.IsDone(t) && t != tag)) book.Pause(rest, "Angehalten. Du kannst später weitermachen.");
                book.Save(root);
                return "Angehalten. Du kannst später weitermachen.";
            }
            book.Save(root);
        }
        return book.Summary();
    }
}
