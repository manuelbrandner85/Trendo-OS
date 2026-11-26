using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0774 (Q1 „KI schreibt ins offene Dokument statt in TXT-Dateien“): PDF-Werkstatt und Dokumente scannen hängen an der einen Kette (<see cref="AssistChain"/>).
/// Vorher schrieb die KI nur <c>pdf-auftrag.txt</c> bzw. <c>scan-auftrag.txt</c>, und die Kette kannte nur Einstellungen („dokument.setzen“) – keine Fachaktion, keine PDF.
/// Jetzt: Klick in der Auftragszeile → KI-Plan → Vorschau (noch keine Datei) → „Ja, behalten“ → echte PDF + EIN Rückgängig-Schritt → Rückgängig legt die PDF beiseite.
/// In den ersten Tests ist die KI ersetzt (feste Antwort); <see cref="Real_local_ki"/> läuft mit dem Ollama-Modell dieses PCs (nur mit TRENDO_T0774_ECHT=&lt;Belegordner&gt;).
[Collection("Claude")]
public class T0774KiDokument_Ui
{
    const string Pw = MainWindow.PwKey, Scan = MainWindow.ScanKey;
    static readonly (double X, double Y)[] Quad = [(130, 45), (465, 75), (505, 410), (85, 380)];

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static string Plan(string action, JsonObject args) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = action, ["args"] = args }) }.ToJsonString();
    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();
    static int Pages(string pdf) { using var d = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import); return d.PageCount; }
    static List<string> Results(ProgramDocument d) => [.. d.List("results").Select(r => r!["file"].AsText()!)];

    /// <summary>PDF-Werkstatt über die Oberfläche öffnen (Vorlage „Seiten ordnen“) und eine fünfseitige PDF mit „Dateien hinzufügen …“ in die Liste holen.</summary>
    static (ProgramDocument Doc, string Pdf) OpenPw(MainWindow w, string root)
    {
        w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Profi);
        w.Show(Pw); Settle(); RealClick(w, w.Find<Button>("prog-tpl-ordnen")); Settle();
        var d = w.ProgramActive(Pw)!;
        Directory.CreateDirectory(Path.Combine(root, "eingang"));
        var pdf = PdfWerkstatt_Ui.TextPdf(Path.Combine(root, "eingang", "vertrag.pdf"), "Seite eins", "Seite zwei", "Seite drei", "Seite vier", "Seite fuenf");
        w.PickOverride = _ => Task.FromResult<string[]?>([pdf]);
        Tap(w, "prog-cmd-pw-add");
        Assert.Single(d.List("files"));
        return (d, pdf);
    }

    /// <summary>„Dokumente scannen“ über die Oberfläche öffnen und zwei Fotos (schräges Blatt auf dunklem Tisch) mit „Fotos hinzufügen …“ holen.</summary>
    static (ProgramDocument Doc, string[] Photos) OpenScan(MainWindow w, string root)
    {
        w.Width = 1500; w.Height = 950; w.ProgramSetLevel(UserLevel.Profi);
        w.Show(Scan); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        var d = w.ProgramActive(Scan)!;
        var dir = Path.Combine(root, "fotos"); Directory.CreateDirectory(dir);
        var photos = new[] { "seite1.png", "seite2.png" }.Select(n => { var f = Path.Combine(dir, n); File.WriteAllBytes(f, PngCodec.Encode(DocScan.TestPhoto(600, 450, Quad))); return f; }).ToArray();
        w.PickOverride = _ => Task.FromResult<string[]?>(photos);
        Tap(w, "prog-cmd-scan-add");
        Assert.Equal(2, w.ScanPages(d).Count);
        return (d, photos);
    }

    /// <summary>Auftrag in die Zeile, „Los“, warten bis zur Vorschau. Gibt die Plan-Quelle zurück.</summary>
    static async Task<string> Ask(MainWindow w, string key, string prompt, int ms = 10000)
    {
        var audits = w.Core.Audit.ReadAll().Count;
        bool NoPlan() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.ki.ohneplan" or "assist.ki.fehler" && a.Detail.StartsWith(key));
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        Tap(w, "auftrag-pruefen");
        await Until(() => NoPlan() || w.AssistLastKey == key && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen", ms);
        Assert.False(NoPlan(), $"{key}: kein Plan aus der KI-Antwort: {w.AssistLastKiAnswer}");
        Assert.True(w.AssistLast!.Value.Phase == "vorschau", $"{key}: {w.AssistLast.Value.Phase} – {w.AssistLast.Value.Message} | {string.Join(" | ", w.AssistLog)} | KI: {w.AssistLastKiAnswer}");
        return w.Find<TextBlock>("assist-source").Text ?? "";
    }

    [AvaloniaFact(DisplayName = "T-0774 PDF-Werkstatt: „Seiten 1–3 heraus“ – Klick → KI-Plan → Vorschau ohne Datei → Ja → neue PDF mit 3 Seiten, EIN Rückgängig-Schritt, Rückgängig/Wiederholen bringen die Datei mit (KI ersetzt)")]
    public async Task Pdf_pages_out_into_open_program()
    {
        var (w, _, root) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var ki = new FixedKi(_ => Plan("pdf.seiten_heraus", new JsonObject { ["seiten"] = "1–3" }));
        try
        {
            w.KiBackendsOverride = () => [ki];
            var (d, pdf) = OpenPw(w, root);
            var original = Hash(pdf); var before = d.Data.ToJsonString(); var history = d.History.Count;
            Assert.Equal(AssistRoute.Dokument, w.AssistRouteFor(Pw));
            await Ask(w, Pw, "Zieh aus der PDF die Seiten 1 bis 3 als neue Datei heraus");
            Assert.Contains("pdf.seiten_heraus", ki.Systems[^1]); // die KI kennt die Fachaktion
            Assert.Contains("vertrag.pdf", ki.Systems[^1]); // und sieht das offene Dokument
            Assert.Equal(before, d.Data.ToJsonString()); Assert.Equal(history, d.History.Count); // Vorschau ändert nichts
            Assert.Empty(Results(d));
            Assert.Contains("erst bei „Ja, behalten“", w.AssistLast!.Value.Message);
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var res = Results(d); Assert.Single(res);
            Assert.True(File.Exists(res[0]), res[0]);
            Assert.Equal(3, Pages(res[0]));
            Assert.Contains("Seite eins", PdfView.Text(res[0], 0)); Assert.Contains("Seite drei", PdfView.Text(res[0], 2));
            Assert.Equal(original, Hash(pdf)); // Original unverändert
            Assert.Equal(history + 1, d.History.Count); // genau EIN Rückgängig-Schritt
            Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Assert.Equal("teilen", d.Text("tool")); Assert.Equal("1-3", d.Text("ranges"));
            Tap(w, "assist-undo");
            var doc = w.ProgramActive(Pw)!;
            Assert.Equal(before, doc.Data.ToJsonString());
            Assert.False(File.Exists(res[0])); // die neue PDF ist beiseitegelegt, nicht gelöscht
            Assert.True(w.KiUndoPlace(doc, res[0]) is { } away && File.Exists(away) && away.Contains(".rueckgaengig"));
            Assert.True(doc.CanRedo, "Wiederholen fehlt: " + string.Join(" | ", doc.History) + " / gleich=" + ReferenceEquals(doc, d));
            Assert.True(doc.Redo());
            Assert.True(File.Exists(res[0])); Assert.Equal(3, Pages(res[0]));
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0774 PDF-Werkstatt: falsche Seiten („Seite 9“ bei 5 Seiten) – Fehler schon in der Vorschau, keine Datei, Dokument unverändert (KI ersetzt)")]
    public async Task Pdf_bad_pages_stop_in_preview()
    {
        var (w, _, root) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        try
        {
            w.KiBackendsOverride = () => [new FixedKi(_ => Plan("pdf.seiten_heraus", new JsonObject { ["seiten"] = "9" }))];
            var (d, _) = OpenPw(w, root);
            var before = d.Data.ToJsonString();
            w.Find<TextBox>("auftrag-prompt").Text = "Zieh aus der PDF die Seite 9 als neue Datei heraus bitte";
            Tap(w, "auftrag-pruefen");
            await Until(() => w.AssistLast?.Phase is "fehler" or "vorschau");
            Assert.Equal("fehler", w.AssistLast!.Value.Phase);
            Assert.Contains("Seite 9 gibt es nicht", w.AssistLast.Value.Message);
            Assert.Equal(before, d.Data.ToJsonString()); Assert.Empty(Results(d));
            Assert.Equal("1-3", MainWindow.PwPageSpec("Seiten 1 bis 3"));
            Assert.Equal("2,5-z", MainWindow.PwPageSpec("Seite 2 und 5–z"));
            Assert.Throws<ArgumentException>(() => MainWindow.PwPageSpec("alle wichtigen"));
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0774 Dokumente scannen: KI-Aktion „Fotos → PDF“ – Klick → Vorschau → Ja → PDF „Datum Titel.pdf“ mit 2 Seiten, EIN Rückgängig-Schritt, Rückgängig legt die PDF beiseite (KI ersetzt)")]
    public async Task Scan_photos_to_pdf_into_open_document()
    {
        var (w, _, root) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var ki = new FixedKi(_ => Plan("scan.pdf", new JsonObject { ["titel"] = "Rechnung Stadtwerke", ["modus"] = "dokument" }));
        try
        {
            w.KiBackendsOverride = () => [ki];
            var (d, photos) = OpenScan(w, root);
            d.Set("date", "2026-11-26", "Datum");
            var before = d.Data.ToJsonString(); var history = d.History.Count; var photoHash = Hash(photos[0]);
            await Ask(w, Scan, "Mach aus den beiden Fotos eine PDF mit dem Titel Rechnung Stadtwerke");
            Assert.Contains("scan.pdf", ki.Systems[^1]);
            var scans = Path.GetDirectoryName(w.ScanFolder(d))!;
            Assert.Empty(Directory.Exists(scans) ? Directory.GetFiles(scans, "*.pdf", SearchOption.AllDirectories) : []); Assert.Equal(before, d.Data.ToJsonString()); // Vorschau: noch keine Datei
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var pdf = Path.Combine(w.ScanFolder(d), "2026-11-26 Rechnung Stadtwerke.pdf");
            Assert.EndsWith(Path.Combine("Scans", "Rechnungen"), w.ScanFolder(d));
            Assert.True(File.Exists(pdf), pdf);
            Assert.Equal(2, Pages(pdf));
            Assert.Equal(pdf, d.Text("last")); Assert.Equal("Rechnung Stadtwerke", d.Text("title"));
            Assert.Equal(history + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Assert.Equal(photoHash, Hash(photos[0])); // Fotos unverändert
            Tap(w, "assist-undo");
            var doc = w.ProgramActive(Scan)!;
            Assert.Equal(before, doc.Data.ToJsonString());
            Assert.False(File.Exists(pdf)); Assert.True(w.KiUndoPlace(doc, pdf) is { } away && File.Exists(away));
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0774 Dokumente scannen: der Knopf heißt ehrlich „Hinweis: Papier-Scanner“ (kein Scanner angeschlossen) und gibt weiter den Weg über Fotos an")]
    public void Scanner_button_is_honest()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.ProgramSetLevel(UserLevel.Profi); w.Show(Scan); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var b = w.Find<Button>("prog-cmd-scan-scanner");
            var label = (b.Content as string ?? "") + " " + string.Join(" ", b.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains("Hinweis", label);
            Assert.DoesNotContain("Scanner …", label);
            Tap(w, "prog-cmd-scan-scanner");
            Assert.Contains("WIA/TWAIN", w.ProgramMessage(Scan));
        }
        finally { w.Close(); }
    }

    // ───────────── Echtlauf mit der KI auf diesem PC ─────────────

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0774_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0774 ECHT: lokale KI (Ollama) – PDF-Werkstatt „Seiten 1–3 heraus“ und Scannen „Fotos → PDF“ im offenen Programm, echte PDFs, Rückgängig (nur mit TRENDO_T0774_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) return; // ohne Belegordner kein Echtlauf (normale Läufe haben kein Modell)
        var (w, _, root) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var log = new StringBuilder();
        void L(string s) => log.AppendLine(s);
        var seen = 0;
        void KiAudit() { var all = w.Core.Audit.ReadAll(); foreach (var a in all.Skip(seen).Where(a => a.Action.StartsWith("ki.", StringComparison.Ordinal) || a.Action.StartsWith("assist.", StringComparison.Ordinal))) L($"  - Protokoll: {a.Action} {a.Detail}"); seen = all.Count; }
        string Keep(string file, string name) { var to = Path.Combine(beleg, name); File.Copy(file, to, true); L($"- Datei {name}: {new FileInfo(to).Length} Bytes, SHA-256 {Hash(to)}"); return to; }
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w);
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source); // läuft Ollama nicht, ist das kein Echtlauf
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            var choice = w.KiModelChoice;
            L("# T-0774 ECHT: KI schreibt ins offene Dokument (PDF-Werkstatt, Dokumente scannen)"); L("");
            L($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            L("Bedienung über die Oberfläche (Vorlage, „Dateien/Fotos hinzufügen …“, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt."); L("");

            // PDF-Werkstatt
            var (d, pdf) = OpenPw(w, root); seen = w.Core.Audit.ReadAll().Count;
            var original = Hash(pdf); var before = d.Data.ToJsonString(); var history = d.History.Count;
            const string p1 = "Zieh aus der PDF die Seiten 1 bis 3 heraus und mach daraus eine neue PDF.";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var src = await Ask(w, Pw, p1, 600_000); sw.Stop();
            L("## PDF-Werkstatt (pdf-werkstatt-programm)"); L("");
            L($"- Eingang: vertrag.pdf, 5 Seiten, SHA-256 {original}"); L($"- Auftrag: {p1}");
            L($"- Plan ({src.Trim()}, {sw.Elapsed.TotalSeconds:F0} s): {string.Join(" → ", w.AssistLast!.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            Assert.Empty(Results(d));
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var res = Results(d); Assert.Single(res); Assert.Equal(3, Pages(res[0]));
            var t1 = PdfView.Text(res[0], 0); var t3 = PdfView.Text(res[0], 2);
            Assert.Contains("Seite eins", t1); Assert.Contains("Seite drei", t3);
            Assert.Equal(original, Hash(pdf)); Assert.Equal(history + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            L($"- Nach „Ja, behalten“: neue PDF {Path.GetFileName(res[0])}, {Pages(res[0])} Seiten (PDFsharp), Text Seite 1 „{t1.Trim()}“, Seite 3 „{t3.Trim()}“ (PDFium)");
            L($"- Rückgängig-Liste: {history} → {d.History.Count} Schritte, oben „{d.UndoLabel}“; Original unverändert = {original == Hash(pdf)}");
            Keep(res[0], "pdf-werkstatt-seiten-1-3.pdf");
            Keep(pdf, "pdf-werkstatt-eingang.pdf");
            Tap(w, "assist-undo");
            var pd = w.ProgramActive(Pw)!;
            L($"- Rückgängig: Dokument wieder wie vorher = {before == pd.Data.ToJsonString()}, neue PDF beiseitegelegt = {!File.Exists(res[0]) && w.KiUndoPlace(pd, res[0]) is { } a1 && File.Exists(a1)}");
            Assert.Equal(before, pd.Data.ToJsonString()); Assert.False(File.Exists(res[0]));
            L("");

            // Dokumente scannen
            var (s, photos) = OpenScan(w, root);
            s.Set("date", "2026-11-26", "Datum");
            var sBefore = s.Data.ToJsonString(); var sHistory = s.History.Count;
            const string p2 = "Mach aus den beiden Fotos eine PDF mit dem Titel „Rechnung Stadtwerke“ im Modus Dokument.";
            sw.Restart();
            var src2 = await Ask(w, Scan, p2, 600_000); sw.Stop();
            L("## Dokumente scannen (scannen-programm)"); L("");
            L($"- Eingang: 2 Fotos (schräges Blatt auf dunklem Tisch, 600×450), SHA-256 {Hash(photos[0])} / {Hash(photos[1])}"); L($"- Auftrag: {p2}");
            L($"- Plan ({src2.Trim()}, {sw.Elapsed.TotalSeconds:F0} s): {string.Join(" → ", w.AssistLast!.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var scanPdf = s.Text("last");
            Assert.True(File.Exists(scanPdf), scanPdf); Assert.Equal(2, Pages(scanPdf));
            Assert.Contains("Stadtwerke", Path.GetFileName(scanPdf));
            Assert.Equal(sHistory + 1, s.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, s.UndoLabel);
            L($"- Nach „Ja, behalten“: {Path.GetFileName(scanPdf)} im Ordner Scans/{Path.GetFileName(Path.GetDirectoryName(scanPdf))}, {Pages(scanPdf)} Seiten; Titel im Dokument „{s.Text("title")}“");
            L($"- Rückgängig-Liste: {sHistory} → {s.History.Count} Schritte, oben „{s.UndoLabel}“");
            Keep(scanPdf, "scannen-fotos-zu-pdf.pdf");
            Tap(w, "assist-undo");
            var sd = w.ProgramActive(Scan)!;
            L($"- Rückgängig: Dokument wieder wie vorher = {sBefore == sd.Data.ToJsonString()}, PDF beiseitegelegt = {!File.Exists(scanPdf)}");
            Assert.Equal(sBefore, sd.Data.ToJsonString()); Assert.False(File.Exists(scanPdf));
            L(""); L("Ergebnis: beide Programme bestanden.");
        }
        catch (Exception e) { L(""); L("FEHLER: " + e.Message); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-lokale-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"), new UTF8Encoding(false));
            MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.VramOverride = null; w.InstalledModelsOverride = null; w.PickOverride = null; w.Close();
        }
    }
}
