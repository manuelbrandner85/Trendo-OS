using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// T-0707 (Paket B5 „Ein Assistenzweg“): ein Weg vom Auftrag bis zur Änderung im offenen Dokument und zur echten Datei –
/// Auftragszeile und KI-Befehle im Menüband gehen über dieselbe Weiche und dieselbe Kette (Plan → Vorschau Ja/Nein → ein Rückgängig-Schritt → Datei).
/// Programme außerhalb der Programm-Hülle sagen „Noch nicht im Dokument möglich“. Die KI ist hier ersetzt (feste Antwort);
/// der Lauf mit dem echten Ollama-Modell steht in <see cref="Real_local_ki"/> (nur mit TRENDO_ASSIST_ECHT=&lt;Belegordner&gt;).
[Collection("Claude")]
public class AssistChain_Ui
{
    const string Book = "buch-programm", Day = "tagesplaner-programm", Measure = "wissenschaft";

    /// <summary>Ersatz-KI auf diesem PC: antwortet je Frage mit dem, was <paramref name="reply"/> aus dem Auftrag macht.</summary>
    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static string Edit(string path, JsonNode value) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "dokument.setzen", ["args"] = new JsonObject { ["pfad"] = path, ["wert"] = value } }) }.ToJsonString();
    static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0707-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    /// <summary>Programm mit Vorlage öffnen und unter einem Dateinamen speichern (wie eine schon vorhandene Datei der Person).</summary>
    static (ProgramDocument Doc, string File) OpenSaved(MainWindow w, string key, string template, string dir)
    {
        w.ProgramNew(key, template);
        var file = w.ProgramSave(key, Path.Combine(dir, key + ".tdoc"));
        w.Show(key); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (w.ProgramActive(key)!, file);
    }

    /// <summary>Ein Auftrag über die Auftragszeile bis zur Datei; danach Rückgängig. Gibt (vorher, nachher-Datei, Plan-Quelle) zurück.</summary>
    static async Task<(string Before, JsonObject Saved, string Source, IReadOnlyList<string> Steps)> Order(MainWindow w, string key, string prompt, string file, int ms = 10000)
    {
        var doc = w.ProgramActive(key)!;
        var before = doc.Data.ToJsonString(); var history = doc.History.Count;
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        w.Find<Button>("auftrag-pruefen").Press();
        var audits = w.Core.Audit.ReadAll().Count;
        bool NoPlan() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.ki.ohneplan" or "assist.ki.fehler" && a.Detail.StartsWith(key));
        await Until(() => NoPlan() || w.AssistLastKey == key && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen", ms);
        Assert.False(NoPlan(), $"{key}: kein Plan aus der KI-Antwort: {w.AssistLastKiAnswer}");
        Assert.True(w.AssistLast!.Value.Phase == "vorschau", $"{key}: {w.AssistLast.Value.Phase} – {w.AssistLast.Value.Message} | {string.Join(" | ", w.AssistLog)}");
        Assert.Equal(before, doc.Data.ToJsonString()); // Vorschau ändert das Original nicht
        Assert.Equal(history, doc.History.Count);
        var source = w.Find<TextBlock>("assist-source").Text ?? "";
        var steps = w.AssistLast!.Value.Steps;
        w.Find<Button>("assist-apply").Press();
        await Until(() => w.AssistLast?.Phase == "fertig");
        Assert.NotEqual(before, doc.Data.ToJsonString());
        Assert.Equal(history + 1, doc.History.Count); // EIN Rückgängig-Schritt
        Assert.StartsWith(AssistChain.UndoPrefix, doc.UndoLabel);
        Assert.Contains("gespeichert", w.AssistLast!.Value.Message);
        var saved = ProgramDocument.Load(file).Data; // echte Datei auf der Platte
        Assert.Equal(doc.Data.ToJsonString(), saved.ToJsonString());
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "assist.apply" && a.Detail.StartsWith(key));
        w.Find<Button>("assist-undo").Press();
        Assert.Equal(before, w.ProgramActive(key)!.Data.ToJsonString()); // Rückgängig nach der KI-Änderung
        return (before, saved, source, steps);
    }

    [AvaloniaFact(DisplayName = "T-0707: Drei Programme aus drei Fächern – Auftrag → KI-Plan → Vorschau → Ja → ein Rückgängig-Schritt im offenen Dokument → echte Datei → Rückgängig (KI ersetzt)")]
    public async Task Three_programs_into_document()
    {
        var (w, _, _) = Ui.Open(); var dir = TempDir();
        MainWindow.AssistStepDelayMs = 0;
        var ki = new FixedKi(q => q.Contains("Leuchtturm") ? Edit("text", "# Kapitel 1\n\nDer Leuchtturm stand allein auf dem Felsen.")
            : q.Contains("Wocheneinkauf") ? Edit("tasks/0", new JsonObject { ["id"] = "k1", ["title"] = "Wocheneinkauf", ["min"] = "60", ["imp"] = "2", ["due"] = "", ["done"] = "" })
            : Edit("sheet/name", "Temperatur im Keller"));
        try
        {
            w.KiBackendsOverride = () => [ki];
            // Schreiben (Bücher)
            OpenSaved(w, Book, "leer", dir);
            var (_, b, src, _) = await Order(w, Book, "Schreib den Anfang von Kapitel 1 über einen Leuchtturm", Path.Combine(dir, Book + ".tdoc"));
            Assert.Contains("Leuchtturm stand allein", b["text"]!.GetValue<string>());
            Assert.Contains("KI (Ersatz-KI", src);
            Assert.Contains("dokument.setzen", ki.Systems[^1]); Assert.Contains("# Kapitel 1", ki.Systems[^1]); // die KI sieht das offene Dokument
            // Alltag (Tagesplaner)
            OpenSaved(w, Day, "leer", dir);
            var (_, d, _, _) = await Order(w, Day, "Nimm den Wocheneinkauf mit einer Stunde in meine Aufgaben auf", Path.Combine(dir, Day + ".tdoc"));
            Assert.Equal("Wocheneinkauf", d["tasks"]![0]!["title"]!.GetValue<string>());
            // Wissenschaft/Technik (Messdaten)
            OpenSaved(w, Measure, null!, dir);
            var (_, m, _, _) = await Order(w, Measure, "Gib der Messreihe einen sprechenden Namen für den Keller", Path.Combine(dir, Measure + ".tdoc"));
            Assert.Equal("Temperatur im Keller", m["sheet"]!["name"]!.GetValue<string>());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0707: KI-Befehl im Menüband („Weiterschreiben mit Claude“) ohne Claude – vorher still nichts, jetzt dieselbe Kette ins offene Dokument")]
    public async Task Ribbon_command_uses_chain()
    {
        var (w, _, _) = Ui.Open(); var dir = TempDir();
        MainWindow.AssistStepDelayMs = 0;
        try
        {
            w.KiBackendsOverride = () => [new FixedKi(_ => Edit("text", "# Kapitel 1\n\nWeitergeschrieben."))];
            OpenSaved(w, Book, "leer", dir);
            Assert.False(w.ClaudeTakesStudioPrompts);
            Assert.Equal(AssistRoute.Dokument, w.AssistRouteFor(Book));
            Assert.True(await w.AssistFromProgram(Book, "STUDIO-BOOK-01", "Schreib das letzte Kapitel in meinem Stil weiter"));
            await Until(() => w.AssistLast?.Phase == "vorschau");
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "assist.plan" && a.Detail.Contains("einstieg=Programmbefehl"));
            w.Find<Button>("assist-apply").Press();
            await Until(() => w.AssistLast?.Phase == "fertig");
            Assert.Contains("Weitergeschrieben.", w.ProgramActive(Book)!.Text("text"));
            // ohne jede KI: klare Rückmeldung statt Stille
            w.KiBackendsOverride = () => [];
            Assert.False(await w.AssistFromProgram(Book, "STUDIO-BOOK-01", "Lies dieses Kapitel wie eine Lektorin"));
            Assert.Contains("Keine KI hat geantwortet", w.AssistProgramNote);
            Assert.Equal(w.AssistProgramNote, w.ProgramMessage(Book));
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0707: KI liefert nicht reparierbares JSON – die Kette fragt einmal nach und arbeitet mit der Korrektur weiter")]
    public async Task Broken_json_is_asked_again()
    {
        var (w, _, _) = Ui.Open(); var dir = TempDir();
        MainWindow.AssistStepDelayMs = 0;
        var n = 0;
        var good = Edit("tasks/0", new JsonObject { ["id"] = "k1", ["title"] = "Wocheneinkauf", ["min"] = "60", ["imp"] = "2", ["due"] = "", ["done"] = "" });
        var ki = new FixedKi(_ => n++ == 0 ? good[..(good.IndexOf("\"wert\"", StringComparison.Ordinal) + 7)] : good); // erste Antwort bricht mitten im Plan ab – nicht zu reparieren, also nachfragen
        try
        {
            w.KiBackendsOverride = () => [ki];
            var (_, file) = OpenSaved(w, Day, "leer", dir);
            var (_, saved, _, _) = await Order(w, Day, "Nimm den Wocheneinkauf mit einer Stunde in meine Aufgaben auf", file);
            Assert.Equal(2, n);
            Assert.Equal("Wocheneinkauf", saved["tasks"]![0]!["title"]!.GetValue<string>());
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "assist.ki.nachfrage");
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0707: Programme außerhalb der Hülle (Text, Tabelle, Geld …) – einheitlich „Noch nicht im Dokument möglich“, Liste aus dem Katalog")]
    public async Task Outside_shell_says_so()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.KiBackendsOverride = () => [];
            var outside = w.AssistOutsideShell();
            foreach (var k in new[] { "text", "tabelle", "finanzen" }) Assert.Contains(outside, o => o.Key == k);
            Assert.DoesNotContain(outside, o => o.Key == Book);
            Assert.Equal(AssistRoute.Projekt, w.AssistRouteFor("text"));
            Assert.StartsWith(AssistChain.NotInDocumentLead, w.AssistNotInDocument("text"));
            w.Show("text"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            w.Find<TextBox>("auftrag-prompt").Text = "Schreib einen Gruß an das Team";
            w.Find<Button>("auftrag-pruefen").Press();
            await Until(() => w.Find<TextBlock>("auftrag-nicht-im-dokument").IsVisible);
            Assert.StartsWith(AssistChain.NotInDocumentLead, w.Find<TextBlock>("auftrag-nicht-im-dokument").Text);
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_ECHT") is { Length: > 0 } beleg)
            {
                var sb = new StringBuilder("# T-0707: Programme außerhalb der Programm-Hülle (Stand dieses Zweigs, aus ProgramCatalog minus angemeldete Hüllen-Programme)\n\n");
                sb.AppendLine($"Anzahl: {outside.Count} · in der Hülle: {MainWindow.Programs.Count}").AppendLine().AppendLine("| Seite | Programm | Studio |").AppendLine("|---|---|---|");
                foreach (var o in outside) sb.AppendLine($"| {o.Key} | {o.Name} | {o.Studio} |");
                Directory.CreateDirectory(beleg); File.WriteAllText(Path.Combine(beleg, "ausserhalb-der-huelle.md"), sb.ToString());
            }
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0707: Rechte in der Kette – beim Not-Stopp ändert die KI nichts; Claude-Brücke prüft mit derselben Regel (Text bleibt Text)")]
    public async Task Rights_and_bridge_same_rule()
    {
        var (w, _, _) = Ui.Open(); var dir = TempDir();
        MainWindow.AssistStepDelayMs = 0;
        try
        {
            var ki = new FixedKi(_ => Edit("text", "Nie"));
            w.KiBackendsOverride = () => [ki];
            OpenSaved(w, Book, "leer", dir);
            w.Core.Approvals.Halt(Principal.LocalOwner, "Test");
            w.Find<TextBox>("auftrag-prompt").Text = "Schreib eine Widmung für meine Oma";
            w.Find<Button>("auftrag-pruefen").Press();
            await Until(() => w.TryFind<TextBlock>("assist-msg")?.Text?.Contains("Not-Stopp") == true);
            Assert.Empty(ki.Systems); // die KI wurde gar nicht erst gefragt
            w.Core.Approvals.Resume(Principal.LocalOwner);
            var m = w.ClaudeBridgeForTest(dir, Book);
            var r = await m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = "dokument_setzen", ["arguments"] = new JsonObject { ["programm"] = Book, ["pfad"] = "text", ["wert"] = new JsonArray(1, 2) } } });
            Assert.True((bool)r!["result"]!["isError"]!);
            Assert.Contains("passt nicht zum Dokument", (string)r["result"]!["content"]![0]!["text"]!);
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    // ───────────── Echtlauf mit der KI auf diesem PC ─────────────

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_ASSIST_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0707 ECHT: lokale KI (Ollama, bestes Modell nach T-0703) ändert in drei Programmen das offene Dokument, Datei auf der Platte, Rückgängig (nur mit TRENDO_ASSIST_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) return; // ohne Belegordner kein Echtlauf (normale Läufe haben kein Modell)
        var (w, _, _) = Ui.Open(); var dir = TempDir();
        MainWindow.AssistStepDelayMs = 0;
        var log = new StringBuilder();
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w); // T-0727: Modelle über den Weg der App (Ollama /api/tags) und Grafikspeicher dieses PCs – daraus wählt die Regel aus T-0703
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source); // läuft Ollama nicht, ist das kein Echtlauf
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            var choice = w.KiModelChoice;
            log.AppendLine("# T-0707 ECHT: eine Kette, lokale KI im offenen Dokument").AppendLine();
            log.AppendLine($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            log.AppendLine("Bedienung über die Oberfläche (Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.").AppendLine();
            var cases = new (string Key, string Template, string Prompt, Func<JsonObject, bool> Ok)[]
            {
                (Book, "leer", "Schreib für Kapitel 1 einen kurzen Absatz (drei Sätze) über einen alten Leuchtturmwärter. Ersetze den Text des Buchs dafür durch die Überschrift „# Kapitel 1“ und diesen Absatz.",
                    d => d["text"]?.GetValue<string>() is { } t && t.Contains("Kapitel 1") && t.Contains("Leuchtturm", StringComparison.OrdinalIgnoreCase) && !t.Contains("Kapitel 2")),
                (Day, "leer", "Trag in meine Aufgaben „Wocheneinkauf“ mit 60 Minuten ein (als neue Aufgabe in der Liste tasks, Felder wie im Dokument: id, title, min, imp, due, done).",
                    d => d["tasks"] is JsonArray a && a.Any(x => x?["title"]?.ToString().Contains("Wocheneinkauf", StringComparison.OrdinalIgnoreCase) == true)),
                (Measure, null!, "Benenne die Messreihe (sheet/name) in „Kellertemperatur Januar“ um.",
                    d => d["sheet"]?["name"]?.ToString().Contains("Kellertemperatur", StringComparison.OrdinalIgnoreCase) == true),
            };
            foreach (var (key, template, prompt, ok) in cases)
            {
                var (doc, file) = OpenSaved(w, key, template, dir);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var (before, saved, source, steps) = await Order(w, key, prompt, file, 600_000);
                sw.Stop();
                Assert.True(ok(saved), $"{key}: Ergebnis passt nicht zum Auftrag: {saved.ToJsonString()}");
                var copy = Path.Combine(beleg, key + "-nach-ki.tdoc"); File.Copy(file, copy, true);
                log.AppendLine($"## {MainWindow.Programs[key].Title} ({key})").AppendLine();
                log.AppendLine($"- Auftrag: {prompt}").AppendLine($"- Plan ({source}, {sw.Elapsed.TotalSeconds:F0} s): {string.Join(" → ", steps)}");
                log.AppendLine($"- Datei nach „Ja, behalten“: {Path.GetFileName(copy)} · SHA-256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copy))).ToLowerInvariant()}");
                log.AppendLine($"- Rückgängig: Dokument wieder wie vorher = {before == w.ProgramActive(key)!.Data.ToJsonString()}");
                var txt = saved.ToJsonString(); log.AppendLine($"- Daten danach (Ausschnitt): `{(txt.Length > 600 ? txt[..600] + " …" : txt).Replace("`", "'")}`").AppendLine();
            }
            log.AppendLine("Ergebnis: alle drei Programme bestanden.");
        }
        catch (Exception e) { log.AppendLine().AppendLine("FEHLER: " + e.Message); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-lokale-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"));
            MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.VramOverride = null; w.InstalledModelsOverride = null; w.Close();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
