using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// T-0723 (Audit D6 „Code mit KI“ echt machen): Auftrag → Plan → Diff je Datei → Ja/Nein je Änderung → EIN Rückgängig-Schritt (Dokument und Dateien) →
/// Tests nach Freigabe mit echtem Python → bei Rot „zurücknehmen?“. In den ersten Tests ist nur die KI ersetzt (feste Antwort), der Testlauf ist echtes
/// <c>python -m unittest</c>. Der Lauf mit der echten KI auf diesem PC steht in <see cref="Real_local_ki"/> (nur mit TRENDO_CODEKI_ECHT=&lt;Belegordner&gt;).
[Collection("Claude")]
public class CodeAiKi_Ui
{
    const string K = "code-ki-programm";

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [], Questions = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
        { Systems.Add(messages[0].Content); Questions.Add(messages[^1].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static void Settle() { for (var i = 0; i < 8; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok, int ms = 120000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Settle(); await Task.Delay(20); } Settle(); Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); Assert.True(b.IsEnabled, name + " ist gesperrt"); b.Press(); Settle(); }

    const string Rechner = "\"\"\"Kleiner Rechner zum Ausprobieren.\"\"\"\n\n\ndef addiere(a, b):\n    return a + b\n\n\ndef teile(a, b):\n    if b == 0:\n        raise ValueError(\"Durch null kann man nicht teilen.\")\n    return a / b\n";
    static string Answer(int expect, bool readme = false) =>
        "PLAN:\n- Funktion multipliziere in rechner.py ergänzen\n- Test dafür in test_rechner.py schreiben\n\n"
        + "DATEI: rechner.py\n```python\n" + Rechner + "\n\ndef multipliziere(a, b):\n    return a * b\n```\n\n"
        + "DATEI: test_rechner.py\n```python\nimport unittest\n\nfrom rechner import addiere, multipliziere, teile\n\n\nclass RechnerTest(unittest.TestCase):\n    def test_addiere(self):\n        self.assertEqual(addiere(2, 3), 5)\n\n    def test_teile(self):\n        self.assertEqual(teile(9, 3), 3)\n        with self.assertRaises(ValueError):\n            teile(1, 0)\n\n"
        + $"    def test_multipliziere(self):\n        self.assertEqual(multipliziere(2, 3), {expect})\n\n\nif __name__ == \"__main__\":\n    unittest.main()\n```\n"
        + (readme ? "\nDATEI: README.md\n```\n# Ganz anderer Titel\n```\n" : "");

    /// <summary>Beispielprojekt (Python-Vorlage des Studios) über die Vorlage öffnen, Wunsch ins Feld der KI-Karte schreiben.</summary>
    static (ProgramDocument D, string Root) OpenSample(MainWindow w, string wish)
    {
        w.Show(K); Settle();
        w.Find<Button>("prog-tpl-beispiel").Press(); Settle();
        var d = w.ProgramActive(K)!; var root = d.Text("root");
        Assert.True(File.Exists(Path.Combine(root, "rechner.py")));
        w.Find<TextBox>("ca-ki-wish").Text = wish;
        return (d, root);
    }

    static string Read(string root, string f) => File.ReadAllText(Path.Combine(root, f));

    [AvaloniaFact(DisplayName = "T-0723: Auftrag → Plan → Diff je Datei → Ja → ein Rückgängig-Schritt → echtes Python rot → „zurücknehmen?“ → alter Stand, Wiederholen (KI ersetzt)")]
    public async Task Red_tests_ask_and_undo()
    {
        var (w, _, _) = Ui.Open();
        var ki = new FixedKi(_ => Answer(expect: 7)); // falscher Test: 2·3 ≠ 7
        try
        {
            w.KiBackendsOverride = () => [ki]; w.CodeRealToolsInTests = true;
            var (d, root) = OpenSample(w, "Ergänze eine Funktion multipliziere(a, b) und schreib einen Test dafür");
            var r0 = Read(root, "rechner.py"); var t0 = Read(root, "test_rechner.py");
            Tap(w, "ca-ki-propose");
            await Until(() => w.CodeAiKiPhase is "vorschau" or "fehler");
            var hist = d.History.Count; // der Wunsch selbst ist schon ein Schritt („Wunsch an die KI“)
            Assert.Equal("vorschau", w.CodeAiKiPhase);
            Assert.Contains("DATEI:", ki.Systems[^1]); Assert.Contains("def teile", ki.Questions[^1]); // die KI sieht die Projektdateien
            Assert.Equal(r0, Read(root, "rechner.py")); // Vorschau ändert nichts
            Assert.Equal(2, w.CodeAiKiPreview!.Files.Count);
            Assert.Contains(w.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "1. Funktion multipliziere in rechner.py ergänzen");
            Assert.Contains(w.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("def multipliziere(a, b):") == true && t.Text.Contains('+'));
            Tap(w, "ca-ki-apply");
            await Until(() => w.CodeAiKiPhase is "rot" or "gruen" or "fehler" or "angewendet");
            Assert.Equal("rot", w.CodeAiKiPhase);
            Assert.Contains("multipliziere", Read(root, "rechner.py"));
            Assert.Equal(hist + 1, d.History.Count); // EIN Rückgängig-Schritt
            Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Assert.Equal(1, w.CodeAiKiTests!.Failed); Assert.Equal(2, w.CodeAiKiTests.Passed); // echter Python-Lauf
            Assert.Equal("code-ki.test", w.LastFreigabe!.Action); // Start nur nach Freigabe
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "execution.done" && a.Detail == w.LastFreigabe.Id);
            Assert.Contains("zurücknehmen", w.CodeAiKiMessage);
            Tap(w, "ca-ki-undo");
            Assert.Equal(r0, Read(root, "rechner.py")); Assert.Equal(t0, Read(root, "test_rechner.py"));
            Assert.Equal(hist, d.History.Count);
            Assert.Empty(CodeKiChange.AppliedIds(d.Data));
            // Wiederholen bringt die Änderung zurück, Rückgängig (Knopf der Hülle) nimmt sie wieder weg
            d.Redo(); Assert.Contains("multipliziere", Read(root, "rechner.py"));
            w.Show(K); Settle(); Tap(w, "prog-undo");
            Assert.Equal(r0, Read(root, "rechner.py"));
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0723: Nein zu einer Datei – nur Angenommenes wird geschrieben, Tests grün; Rückgängig der Hülle stellt alles her (KI ersetzt, Python echt)")]
    public async Task Partial_accept_green()
    {
        var (w, _, _) = Ui.Open();
        var ki = new FixedKi(_ => Answer(expect: 6, readme: true));
        try
        {
            w.KiBackendsOverride = () => [ki]; w.CodeRealToolsInTests = true;
            var (d, root) = OpenSample(w, "Ergänze multipliziere(a, b) mit Test");
            var readme = Read(root, "README.md"); var r0 = Read(root, "rechner.py");
            Tap(w, "ca-ki-propose");
            await Until(() => w.CodeAiKiPhase is "vorschau" or "fehler");
            var idx = w.CodeAiKiPreview!.Files.ToList().FindIndex(f => f.Path == "README.md");
            Assert.True(idx >= 0);
            var total = w.CodeAiKiPreview.Files.Sum(f => f.Hunks.Count);
            w.Find<CheckBox>($"ca-ki-hunk-{idx}-0").IsChecked = false; Settle(); // Nein zu dieser Änderung
            Assert.Contains($"{total - 1} Änderung", w.Find<Button>("ca-ki-apply").Content?.ToString());
            Tap(w, "ca-ki-apply");
            await Until(() => w.CodeAiKiPhase is "rot" or "gruen" or "fehler" or "angewendet");
            Assert.Equal("gruen", w.CodeAiKiPhase);
            Assert.Equal(3, w.CodeAiKiTests!.Passed);
            Assert.Equal(readme, Read(root, "README.md")); // abgelehnt = unverändert
            Assert.Contains("multipliziere", Read(root, "rechner.py"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "code-ki.ki.apply" && !a.Detail.Contains("README"));
            Tap(w, "prog-undo");
            Assert.Equal(r0, Read(root, "rechner.py"));
            Assert.DoesNotContain("multipliziere", Read(root, "test_rechner.py"));
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0723: Projektgrenzen, Not-Stopp und keine Freigabe – nichts außerhalb des Projekts, KI nicht gefragt, kein Testlauf ohne Ja (KI ersetzt)")]
    public async Task Boundaries_and_approval()
    {
        var (w, _, _) = Ui.Open();
        var outside = new FixedKi(_ => "PLAN:\n- etwas außerhalb\nDATEI: ../draussen.py\n```\nprint('x')\n```\nDATEI: .git/config\n```\n[core]\n```\n");
        try
        {
            w.KiBackendsOverride = () => [outside]; w.CodeRealToolsInTests = true;
            var (d, root) = OpenSample(w, "Schreib etwas neben das Projekt");
            Tap(w, "ca-ki-propose");
            await Until(() => w.CodeAiKiPhase is "vorschau" or "fehler");
            Assert.Equal("fehler", w.CodeAiKiPhase);
            Assert.Contains("nicht ändern darf", w.CodeAiKiMessage);
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "draussen.py")));
            Assert.Null(w.CodeAiKiPreview);
            // Not-Stopp: die KI wird gar nicht gefragt
            var asked = outside.Questions.Count;
            w.Core.Approvals.Halt(Principal.LocalOwner, "Test");
            await w.CodeAiKiPropose(d);
            Assert.Equal(asked, outside.Questions.Count); Assert.Contains("Not-Stopp", w.CodeAiKiMessage);
            w.Core.Approvals.Resume(Principal.LocalOwner);
            // Freigabe abgelehnt: Änderung bleibt (rückgängig machbar), aber kein Testlauf
            var good = new FixedKi(_ => Answer(expect: 6));
            w.KiBackendsOverride = () => [good]; w.FreigabeConfirm = _ => Task.FromResult(false);
            await w.CodeAiKiPropose(d); Settle();
            Assert.Equal("vorschau", w.CodeAiKiPhase);
            await w.CodeAiKiApply(d); Settle();
            Assert.Equal("angewendet", w.CodeAiKiPhase);
            Assert.Null(w.CodeAiKiTests);
            Assert.Contains("keine Freigabe", w.CodeAiKiMessage);
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Action == "code-ki.ki.test");
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    // ───────────── Echtlauf mit der KI auf diesem PC ─────────────

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_CODEKI_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0723 ECHT: lokale KI (Ollama, bestes Modell nach T-0703) ergänzt im Python-Beispielprojekt eine Funktion mit Test → Diff → Ja → echtes unittest → Rückgängig (nur mit TRENDO_CODEKI_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) { Assert.Skip("Übersprungen – Echtlauf nur mit TRENDO_CODEKI_ECHT=<Belegordner> und laufendem Ollama."); return; } // ohne Belegordner kein Echtlauf (normale Läufe haben kein Modell)
        var (w, _, _) = Ui.Open();
        var log = new StringBuilder();
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w); // T-0727: Modelle über den Weg der App (Ollama /api/tags) und Grafikspeicher dieses PCs – daraus wählt die Regel aus T-0703
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source); // läuft Ollama nicht, ist das kein Echtlauf
            if (Environment.GetEnvironmentVariable("TRENDO_CODEKI_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            w.CodeRealToolsInTests = true;
            var choice = w.KiModelChoice;
            const string wish = "Ergänze in rechner.py eine Funktion multipliziere(a, b), die das Produkt von a und b zurückgibt, und schreib in test_rechner.py einen Test test_multipliziere dafür.";
            var (d, root) = OpenSample(w, wish);
            var files = new[] { "rechner.py", "test_rechner.py", "README.md" };
            var before = files.ToDictionary(f => f, f => Read(root, f));
            log.AppendLine("# T-0723 ECHT: Code mit KI – lokale KI ändert Code im offenen Projekt").AppendLine();
            log.AppendLine($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            log.AppendLine("Bedienung über die Oberfläche: Vorlage „Beispiel: kleines Python-Projekt“, Wunsch in „Mit KI ändern“, „Plan und Vorschau holen“, „Ja – … übernehmen und testen“, Freigabe „Tests starten“ (Rückfrage mit Ja beantwortet), danach „Rückgängig“ der Programm-Hülle. Fenster headless, Claude nicht beteiligt.").AppendLine();
            log.AppendLine($"Auftrag: {wish}").AppendLine();
            foreach (var f in files) log.AppendLine($"- vorher {f}: SHA-256 {Sha(Path.Combine(root, f))}");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Tap(w, "ca-ki-propose");
            await Until(() => w.CodeAiKiPhase is "vorschau" or "fehler", 900_000);
            log.AppendLine().AppendLine($"Antwortzeit KI: {sw.Elapsed.TotalSeconds:F0} s · Phase: {w.CodeAiKiPhase} · {w.CodeAiKiMessage}");
            File.WriteAllText(Path.Combine(beleg, "ki-antwort-roh.txt"), w.CodeAiKiLastAnswer ?? "");
            Assert.True(w.CodeAiKiPhase == "vorschau", w.CodeAiKiMessage + "\n" + w.CodeAiKiLastAnswer);
            var pv = w.CodeAiKiPreview!;
            File.WriteAllText(Path.Combine(beleg, "vorschau.diff"), CodeKiChange.UnifiedAll(pv));
            log.AppendLine("Plan der KI:"); foreach (var s in pv.Plan) log.AppendLine("- " + s);
            log.AppendLine($"Vorschau: {string.Join(", ", pv.Files.Select(f => $"{f.Path} ({(f.IsNew ? "neu" : "geändert")}, {f.Hunks.Count} Änderung(en), +{f.Added} −{f.Removed})"))}; abgelehnt: {pv.Rejected.Count}");
            foreach (var f in files) Assert.Equal(before[f], Read(root, f)); // Vorschau ändert nichts
            var hist = d.History.Count;
            Tap(w, "ca-ki-apply");
            await Until(() => w.CodeAiKiPhase is "rot" or "gruen" or "fehler" or "angewendet", 600_000);
            log.AppendLine().AppendLine($"Nach „Ja“: Phase {w.CodeAiKiPhase} · {w.CodeAiKiMessage}");
            Assert.Equal(hist + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            foreach (var f in pv.Files) { File.Copy(Path.Combine(root, f.Path), Path.Combine(beleg, "nach-ki-" + f.Path.Replace('/', '_')), true); log.AppendLine($"- nach Ja {f.Path}: SHA-256 {Sha(Path.Combine(root, f.Path))}"); }
            Assert.Contains("multipliziere", Read(root, "rechner.py"));
            log.AppendLine().AppendLine("Testlauf (echt, nach Freigabe):").AppendLine("```").AppendLine(w.CodeAiKiOutput).AppendLine("```");
            File.WriteAllText(Path.Combine(beleg, "testlauf-roh.txt"), w.CodeAiKiOutput);
            Assert.True(w.CodeAiKiPhase is "gruen" or "rot", w.CodeAiKiMessage);
            if (w.CodeAiKiPhase == "rot") { log.AppendLine("Tests ROT – Rückfrage „KI-Änderung zurücknehmen?“ sichtbar: " + (w.TryFind<Button>("ca-ki-undo") is not null)); Tap(w, "ca-ki-undo"); }
            else { w.Show(K); Settle(); Tap(w, "prog-undo"); }
            var back = files.All(f => before[f] == Read(root, f)) && pv.Files.Where(f => f.IsNew).All(f => !File.Exists(Path.Combine(root, f.Path)));
            log.AppendLine().AppendLine($"Rückgängig: alle Dateien wieder wie vorher = {back}");
            foreach (var f in files) log.AppendLine($"- nach Rückgängig {f}: SHA-256 {Sha(Path.Combine(root, f))}");
            Assert.True(back);
            log.AppendLine().AppendLine("Ergebnis: Kette vollständig durchlaufen.");
        }
        catch (Exception e) { log.AppendLine().AppendLine("FEHLER: " + e.Message); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-code-mit-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"));
            w.KiBackendsOverride = null; w.VramOverride = null; w.InstalledModelsOverride = null; w.Close();
        }
    }

    static string Sha(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();
}
