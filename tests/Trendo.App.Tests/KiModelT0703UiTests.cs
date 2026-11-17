using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0703: bestes vorhandenes Modell als Standard (sichtbar in KI-Quellen, KI-Bibliothek und „Modell“), eigene Wahl, Laden mit Fortschritt.
/// Echtläufe nur mit Umgebungsvariablen (sonst übersprungen): TRENDO_ECHT_KI=&lt;Belegordner&gt; (17 + 25 an das automatisch gewählte Modell),
/// TRENDO_ECHT_VLM_PULL=&lt;Belegordner&gt; (Bildverstehens-Modell über die KI-Bibliothek wirklich laden), TRENDO_ECHT_VLM=&lt;Belegordner&gt; (Bilder erkennen beschreibt ein Foto lokal).</summary>
public class KiModelT0703_Ui
{
    const long G = 1024L * 1024 * 1024;
    static readonly InstalledModel[] PcList =
    [
        new("qwen3:14b", 9_276_184_896), new("qwen2.5-coder:14b", 8_988_110_656), new("qwen2.5:7b", 4_683_073_184), new("mistral:7b", 4_372_812_000),
        new("qwen2.5:0.5b", 397_807_936), new("qwen2.5vl:7b", 5_969_233_408),
    ];

    static List<string> Texts(Control c) => c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Length > 0).ToList();
    static void WaitFor(Func<bool> done, int ms) { var sw = Stopwatch.StartNew(); while (!done() && sw.ElapsedMilliseconds < ms) { Thread.Sleep(50); Settle(); } Settle(); }
    static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent; return d!.FullName; }
    static string Anon(string s) => s.Replace(Environment.UserName, "<user>").Replace(Environment.MachineName, "<pc>");

    static void Sums(string dir, string prefix)
    {
        var sb = new StringBuilder();
        foreach (var f in Directory.GetFiles(dir, prefix + "*").Where(f => !f.EndsWith("SHA256.txt")).Order())
            sb.AppendLine($"{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f)))}  {Path.GetFileName(f)}");
        File.WriteAllText(Path.Combine(dir, prefix + "SHA256.txt"), sb.ToString());
    }

    [AvaloniaFact(DisplayName = "T-0703: KI-Quellen, KI-Bibliothek und „Modell“ zeigen den Standard und den Grund; eigene Wahl und „Automatisch“ über Knöpfe")]
    public void Standard_visible_and_choosable()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.InstalledModelsOverride = () => PcList; w.VramOverride = () => 12;
            Assert.Equal("qwen3:14b", w.KiModelChoice.Model);
            w.Show("ki-quellen"); Settle();
            var line = w.Find<TextBlock>("src-standard").Text!;
            Assert.StartsWith("Standard für Chat und Agenten: qwen3:14b.", line);
            Assert.Contains("Grafikspeicher (12 GB)", line);
            w.KiLibraryShow(KiKind.Chat); Settle();
            Assert.Contains("qwen3:14b", w.Find<TextBlock>("kilib-standard").Text);
            w.KiLibraryShow(KiKind.BildVerstehen); Settle();
            Assert.Null(w.TryFind<TextBlock>("kilib-standard")); // nur bei Chat und Nachdenken

            w.Show("ki"); Settle();
            Assert.False(w.Find<Button>("btn-model-auto").IsEnabled); // automatisch ist schon aktiv
            Assert.Null(w.TryFind<Button>("btn-model-use-qwen2.5-coder-14b")); // Spezialmodelle stehen nicht zur Wahl für den Chat
            Assert.Null(w.TryFind<Button>("btn-model-use-qwen2.5vl-7b"));
            RealClick(w, w.Find<Button>("btn-model-use-mistral-7b")); WaitFor(() => w.KiModelChoice.ByUser, 3000);
            Assert.Equal("mistral:7b", w.KiModelChoice.Model);
            w.Show("ki-quellen"); Settle();
            Assert.Contains("Von dir gewählt", w.Find<TextBlock>("src-standard").Text);
            w.Show("ki"); Settle();
            RealClick(w, w.Find<Button>("btn-model-auto")); WaitFor(() => !w.KiModelChoice.ByUser, 3000);
            Assert.Equal("qwen3:14b", w.KiModelChoice.Model);
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "ki.model_selected" && e.Detail == "mistral:7b");

            // Schwacher PC: das kleine Modell bleibt
            w.VramOverride = () => 0; w.InstalledModelsOverride = () => PcList;
            Assert.Equal("qwen2.5:0.5b", KiModel.Best(PcList, 8 * G, 0).Model);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0703: KI-Bibliothek lädt ein Ollama-Modell mit Fortschritt, Protokoll und Audit; zu wenig Speicherplatz → nichts wird geladen")]
    public async Task Library_pull_with_progress()
    {
        var (w, _, _) = Ui.Open();
        var m = KiLibrary.Models.Single(x => x.Id == "qwen2.5vl-7b");
        try
        {
            var asked = new List<string>();
            w.OllamaPullOverride = (tag, step) =>
            {
                asked.Add(tag);
                step(new("pulling manifest", null, 0, 0));
                foreach (var p in new[] { 0L, 2_000_000_000, 5_969_233_408 }) step(new("pulling a99b7f834d75", "sha256:a99b7f834d75", p, 5_969_233_408));
                step(new("verifying sha256 digest", null, 0, 0)); step(new("writing manifest", null, 0, 0)); step(new("success", null, 0, 0));
                return Task.FromResult((true, "success"));
            };
            w.FreeBytesForModels = () => 8L * G; // 6 GB + 5 GB Reserve passt nicht
            await w.KiLibraryInstall(m); Settle();
            Assert.Empty(asked);
            Assert.Contains("Zu wenig Speicherplatz", w.Find<TextBlock>("kilib-msg").Text);
            w.FreeBytesForModels = () => 40L * G;
            await w.KiLibraryInstall(m); Settle();
            Assert.Equal(["qwen2.5vl:7b"], asked);
            var log = File.ReadAllText(Path.Combine(w.Core.DataRoot, "ki", "laden", "qwen2.5vl_7b.log"));
            Assert.Contains("100 %", log); Assert.Contains("verifying sha256 digest", log); Assert.Contains("Ende: success", log);
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "ki.model.pull.ok" && e.Detail.Contains("qwen2.5vl:7b Apache-2.0") && e.Detail.Contains("gewichte=sha256:a99b7f834d75"));
            Assert.Contains("Prüfsumme von Ollama kontrolliert", w.Find<TextBlock>("kilib-msg").Text);
        }
        finally { w.OllamaPullOverride = null; w.FreeBytesForModels = null; w.Close(); }
    }

    // ---------- Echtläufe ----------

    static string? Dir(string env) => Environment.GetEnvironmentVariable(env) is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    static async Task<List<string>> OllamaTags()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:11434"), Timeout = TimeSpan.FromSeconds(5) };
        try { var doc = await http.GetFromJsonAsync<JsonElement>("/api/tags"); return doc.GetProperty("models").EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList(); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return []; }
    }

    [AvaloniaFact(DisplayName = "T-0703 ECHT: „Was ist 17 + 25?“ an das automatisch gewählte Modell über das echte Ollama ergibt 42 (nur mit TRENDO_ECHT_KI)")]
    public async Task Real_auto_model_answers_42()
    {
        if (Dir("TRENDO_ECHT_KI") is not { } dir) return;
        if ((await OllamaTags()).Count == 0) { Assert.Skip("BLOCKIERT – Ollama läuft nicht auf 127.0.0.1:11434."); return; }
        var (w, _, _) = Ui.Open();
        try
        {
            // Echte Modellliste aus den Ollama-Manifesten und echter Grafikspeicher (im Testfenster ist die Hardware-Abfrage sonst aus)
            var have = KiModel.ScanManifests(KiModel.ModelRoots(DataLocation.ToolsRoot()));
            var vram = SystemMetrics.GpuMemory()?.TotalGb ?? 0;
            w.InstalledModelsOverride = () => have; w.VramOverride = () => vram;
            w.KiBackendsOverride = () => [new OllamaBackend()];
            var choice = w.KiModelChoice;
            var sw = Stopwatch.StartNew();
            var a = await w.Intel.AskAsync(new KiRequest("Was ist 17 + 25?", PreferredModel: choice.Model, UseKnowledge: false));
            sw.Stop();
            var sb = new StringBuilder();
            sb.AppendLine("# T-0703 Echtlauf: 17 + 25 an das automatisch gewählte Modell").AppendLine();
            sb.AppendLine($"Datum: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            sb.AppendLine($"Weg: MainWindow.KiModelChoice → Intel.AskAsync (IntelligenceCore, Ausweichen aktiv) → echtes Ollama 127.0.0.1:11434 (/api/chat, Streaming)");
            sb.AppendLine($"Arbeitsspeicher: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (double)G:0.0} GB · Grafikspeicher (nvidia-smi): {vram:0.0} GB").AppendLine();
            sb.AppendLine("Vorhandene Modelle (Manifeste):");
            foreach (var h in have.OrderByDescending(h => h.Bytes)) sb.AppendLine($"- {h.Tag} · {h.Bytes / (double)G:0.00} GB · allgemein: {(KiModel.IsGeneralChat(h.Tag) ? "ja" : "nein (Spezialmodell)")}");
            sb.AppendLine().AppendLine($"Automatisch gewählt: {choice.Model} – {choice.Reason}");
            sb.AppendLine($"Ausweichmodelle: {string.Join(", ", KiModel.Fallbacks(choice.Model, have))}").AppendLine();
            sb.AppendLine($"Frage: Was ist 17 + 25?").AppendLine($"Antwort ({a.Backend}, {a.Model}, {sw.Elapsed.TotalSeconds:0.0} s, ok={a.Ok}): {a.Text}");
            File.WriteAllText(Path.Combine(dir, "ki-17plus25.md"), Anon(sb.ToString()));
            Sums(dir, "ki-");
            Assert.True(a.Ok, a.Text);
            Assert.Equal(choice.Model, a.Model);
            Assert.NotEqual(DependencyCatalog.LocalModel, choice.Model);
            Assert.Contains("42", a.Text);
            Assert.DoesNotContain("32", a.Text);
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0703 ECHT: Bildverstehens-Modell über die KI-Bibliothek wirklich laden (nur mit TRENDO_ECHT_VLM_PULL)")]
    public async Task Real_pull_vision_model()
    {
        if (Dir("TRENDO_ECHT_VLM_PULL") is not { } dir) return;
        if ((await OllamaTags()).Count == 0) { Assert.Skip("BLOCKIERT – Ollama läuft nicht auf 127.0.0.1:11434."); return; }
        var (w, _, _) = Ui.Open();
        var m = KiLibrary.Models.Single(x => x.Id == "qwen2.5vl-7b");
        try
        {
            var before = await OllamaTags();
            var sw = Stopwatch.StartNew();
            await w.KiLibraryInstall(m); // derselbe Weg wie der Knopf „Laden“
            sw.Stop(); Settle();
            var after = await OllamaTags();
            var log = Path.Combine(w.Core.DataRoot, "ki", "laden", "qwen2.5vl_7b.log");
            File.Copy(log, Path.Combine(dir, "vlm-laden.log"), true);
            var audit = w.Core.Audit.ReadAll().Where(e => e.Action.StartsWith("ki.")).Select(e => $"{e.At:HH:mm:ss} {e.Action} {e.Detail}");
            var man = KiModel.ScanManifests(KiModel.ModelRoots(DataLocation.ToolsRoot())).FirstOrDefault(h => h.Tag == "qwen2.5vl:7b");
            File.WriteAllText(Path.Combine(dir, "vlm-laden.md"), Anon(new StringBuilder()
                .AppendLine("# T-0703 Echtlauf: Qwen 2.5 VL 7B über die KI-Bibliothek laden").AppendLine()
                .AppendLine($"Datum: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} · Dauer: {sw.Elapsed.TotalMinutes:0.0} min")
                .AppendLine($"Weg: MainWindow.KiLibraryInstall (Knopf „Laden“) → Freigabe ki.pull → OllamaBackend.PullStreamAsync → Ollama /api/pull (registry.ollama.ai)")
                .AppendLine($"Lizenz laut Katalog: {m.License} (Lizenzschicht im Ollama-Manifest: Apache License 2.0)")
                .AppendLine($"Vorher vorhanden: {before.Contains("qwen2.5vl:7b")} · nachher vorhanden: {after.Contains("qwen2.5vl:7b")} · Manifestgröße: {man?.Bytes ?? 0} Bytes")
                .AppendLine($"Meldung: {w.Find<TextBlock>("kilib-msg").Text}").AppendLine()
                .AppendLine("## Audit").AppendLine().AppendLine(string.Join("\n", audit)).ToString()));
            Sums(dir, "vlm-laden");
            Assert.Contains("qwen2.5vl:7b", after);
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "ki.model.pull.ok" && e.Detail.StartsWith("qwen2.5vl:7b Apache-2.0"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0703 ECHT: „Bilder erkennen“ beschreibt ein Foto lokal mit dem Bildverstehens-Modell (nur mit TRENDO_ECHT_VLM)")]
    public async Task Real_vision_describes_photo()
    {
        if (Dir("TRENDO_ECHT_VLM") is not { } dir) return;
        if (!(await OllamaTags()).Contains("qwen2.5vl:7b")) { Assert.Skip("BLOCKIERT – qwen2.5vl:7b ist in Ollama nicht vorhanden."); return; }
        var photo = Path.Combine(global::Trendo.Core.TempDir.SystemTemp, "trendo-tools-b", "yolox", "dog.jpg");
        if (!File.Exists(photo)) { Assert.Skip("Testfoto fehlt (%TEMP%/trendo-tools-b/yolox/dog.jpg)."); return; }
        const string K = "bilder-erkennen-programm";
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var pics = Path.Combine(root, "fotos"); Directory.CreateDirectory(pics);
        File.Copy(photo, Path.Combine(pics, "foto.jpg"));
        // Im Testfenster ist die Modellsuche aus (isoliert) – hier nur der Name des wirklich vorhandenen Modells; die Beschreibung läuft echt über Ollama
        MainWindow.VisionLocalModelOverride = () => Task.FromResult<string?>("qwen2.5vl:7b");
        MainWindow.FolderPickOverride = _ => pics;
        try
        {
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var sw = Stopwatch.StartNew();
            var b = w.Find<Button>("vi-recognize-folder-big"); b.Press(); Settle();
            WaitFor(() => w.ProgramMessage(K).StartsWith("✓") || w.ProgramMessage(K).StartsWith("⚠"), 600_000);
            sw.Stop();
            var res = w.VisionResults(w.ProgramActive(K)!).Single();
            File.Copy(photo, Path.Combine(dir, "vlm-testfoto.jpg"), true);
            File.WriteAllText(Path.Combine(dir, "vlm-beschreibung.md"), Anon(new StringBuilder()
                .AppendLine("# T-0703 Echtlauf: „Bilder erkennen“ beschreibt ein Foto lokal").AppendLine()
                .AppendLine($"Datum: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} · Dauer: {sw.Elapsed.TotalSeconds:0.0} s")
                .AppendLine("Weg: Programm „Bilder erkennen“ → Knopf „Ordner erkennen …“ → VisionDescribe → OllamaClient.AskImagesAsync (qwen2.5vl:7b, /api/generate, 127.0.0.1)")
                .AppendLine($"Testfoto: vlm-testfoto.jpg (YOLOX-Beispielbild dog.jpg, SHA-256 {Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(photo)))})")
                .AppendLine($"Meldung: {w.ProgramMessage(K)}").AppendLine()
                .AppendLine($"Quellen: {string.Join(", ", res.Sources)}")
                .AppendLine($"Beschreibung: {res.Description}")
                .AppendLine($"Schlagworte: {string.Join(", ", res.Tags)}").ToString()));
            Sums(dir, "vlm-b");
            Assert.Contains(res.Sources, s => s.Contains("qwen2.5vl:7b"));
            Assert.True(res.Description.Length > 20, res.Description);
            Assert.Contains("Hund", res.Tags); // Schlagworte aus der Beschreibung (auch wenn das Modell sie hinter den letzten Satz schreibt)
        }
        finally { MainWindow.VisionLocalModelOverride = null; MainWindow.FolderPickOverride = null; w.Close(); }
    }
}
