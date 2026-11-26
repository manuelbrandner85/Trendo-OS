using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0734 „KI fragen“: Datei anhängen (Knopf, Ablegen), Hinweis wohin die Datei geht, Antwort wächst sichtbar beim Entstehen, Stopp behält das Geschriebene.
/// Die ersten Tests ersetzen die KI (Stream nachgebildet); der Echtlauf mit Ollama läuft nur mit TRENDO_ECHT_KIFRAGEN=&lt;Belegordner&gt;.</summary>
public class ChatAttachT0734_Ui
{
    static async Task Until(Func<bool> ok, int ms = 8000) { var sw = Stopwatch.StartNew(); while (!ok() && sw.ElapsedMilliseconds < ms) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Assert.True(ok()); }

    /// <summary>Nachgebildete KI, die Stück für Stück schreibt (bis zum Abbruch, wenn <paramref name="endless"/>).</summary>
    sealed class SlowKi(string[] parts, bool endless = false, string[]? models = null) : IStreamingKiBackend
    {
        public string Name => "Test-KI"; public string BaseUrl => "http://127.0.0.1:1";
        public List<IReadOnlyList<KiMessage>> Calls { get; } = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(models ?? ["test"]);
        public Task<string> ChatAsync(string m, IReadOnlyList<KiMessage> x, CancellationToken ct = default) => ChatStreamAsync(m, x, _ => { }, ct);
        public async Task<string> ChatStreamAsync(string m, IReadOnlyList<KiMessage> x, Action<string> onDelta, CancellationToken ct = default)
        {
            Calls.Add(x); var sb = new StringBuilder(); var i = 0;
            do foreach (var p in parts) { ct.ThrowIfCancellationRequested(); await Task.Delay(60, ct); var piece = endless ? $"{p}{i++} " : p; sb.Append(piece); onDelta(piece); }
            while (endless);
            return sb.ToString();
        }
    }

    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0734-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [AvaloniaFact(DisplayName = "T-0734: „Datei anhängen“ (Auswahl) → Karte mit Name und Hinweis „bleibt auf diesem PC“; Frage enthält den Dateitext, Gespräch zeigt 📎; falsche Art wird abgelehnt")]
    public async Task Attach_by_button_and_send()
    {
        var (w, _, _) = Ui.Open(); var d = Tmp();
        try
        {
            var note = Path.Combine(d, "notiz.txt"); File.WriteAllText(note, "Der Zahnarzttermin ist am Dienstag um 9 Uhr.");
            var exe = Path.Combine(d, "werkzeug.exe"); File.WriteAllText(exe, "MZ");
            var ki = new SlowKi(["Am ", "Dienstag ", "um 9 Uhr."]);
            w.KiBackendsOverride = () => [ki];
            w.PickOverride = req => Task.FromResult<string[]?>(req.Field == "ask-attach" ? [note, exe] : null);
            w.Show("ki-fragen");
            Assert.False(w.Find<TextBlock>("ask-attach-hint").IsVisible);
            RealClick(w, w.Find<Button>("ask-attach"));
            await Until(() => w.AskFiles.Count == 1);
            Assert.Contains("notiz.txt", string.Join(" ", w.GetLogicalTexts()));
            Assert.Contains("werkzeug.exe", w.Find<TextBlock>("ask-state").Text); // abgelehnt mit Grund
            var hint = w.Find<TextBlock>("ask-attach-hint");
            Assert.True(hint.IsVisible); Assert.Contains("bleibt auf diesem PC", hint.Text); Assert.Contains("An andere KI-Dienste im Internet geht kein Anhang", hint.Text);

            var input = w.Find<TextBox>("ask-input"); input.Text = "Wann ist der Termin?";
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.CurrentChatId is not null && w.TryFind<Border>("ask-msg-1") is not null);
            Assert.Contains("--- Angehängte Datei: notiz.txt ---", ki.Calls[0][^1].Content);
            Assert.Contains("Zahnarzttermin", ki.Calls[0][^1].Content);
            var chat = w.Chats.Get(w.CurrentChatId!)!;
            Assert.Equal("Wann ist der Termin?\n\n📎 notiz.txt", chat.Messages[0].Content);
            Assert.Equal("Am Dienstag um 9 Uhr.", chat.Messages[1].Content);
            Assert.Empty(w.AskFiles); Assert.Null(w.TryFind<Border>("ask-file-0"));

            // Anhang wieder entfernen über ✕
            w.AskAttach([note]); Assert.NotNull(w.TryFind<Border>("ask-file-0"));
            RealClick(w, w.Find<Button>("ask-file-del-0")); Assert.Empty(w.AskFiles);
        }
        finally { w.Close(); Directory.Delete(d, true); }
    }

    [AvaloniaFact(DisplayName = "T-0734: Antwort wächst sichtbar in der Sprechblase; Stopp beendet in unter 2 s und das Geschriebene bleibt mit „(abgebrochen)“ im Gespräch")]
    public async Task Stream_visible_and_stop()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.KiBackendsOverride = () => [new SlowKi(["Wort"], endless: true)];
            w.Show("ki-fragen");
            var input = w.Find<TextBox>("ask-input"); input.Text = "Erzähl eine lange Geschichte";
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.TryFind<Border>("ask-stream") is not null);
            var lengths = new List<int>();
            await Until(() => { if (w.AskStreamShown is { } s && s.StartsWith("Wort")) { if (lengths.Count == 0 || lengths[^1] != s.Length) lengths.Add(s.Length); } return lengths.Count >= 3; });
            Assert.True(lengths.Zip(lengths.Skip(1)).All(p => p.Second > p.First)); // wächst
            Assert.Null(w.TryFind<Border>("ask-msg-1")); // noch nicht fertig
            var sw = Stopwatch.StartNew();
            RealClick(w, w.Find<Button>("ask-stop"));
            await Until(() => w.Find<Button>("ask-send").IsEnabled && w.TryFind<Border>("ask-stream") is null, 2000);
            sw.Stop(); Assert.True(sw.ElapsedMilliseconds < 2000, $"Stopp dauerte {sw.ElapsedMilliseconds} ms");
            Assert.Equal("Abgebrochen.", w.Find<TextBlock>("ask-state").Text);
            var chat = w.Chats.Get(w.CurrentChatId!)!;
            Assert.StartsWith("Wort0 Wort1 Wort2", chat.Messages[1].Content);
            Assert.EndsWith("(abgebrochen)", chat.Messages[1].Content);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0734: Bild ohne Bildmodell → klare Meldung statt erfundener Antwort, Anhang bleibt; Hinweis sagt, dass Bilder auf dem PC bleiben")]
    public async Task Image_without_vision_model()
    {
        var (w, _, _) = Ui.Open(); var d = Tmp();
        try
        {
            var png = Path.Combine(d, "foto.png"); File.WriteAllBytes(png, Png.RedSquare());
            var ki = new SlowKi(["Erfunden"], models: ["qwen3:14b"]);
            w.KiBackendsOverride = () => [ki];
            w.Show("ki-fragen");
            w.AskAttach([png]);
            Assert.Contains("Bilder bleiben auf diesem PC", w.Find<TextBlock>("ask-attach-hint").Text);
            RealClick(w, w.Find<Button>("ask-send")); // ohne Text: Standardfrage zum Bild
            await Until(() => w.Find<TextBlock>("ask-state").Text == KiAttachments.NoVisionModel);
            Assert.Empty(ki.Calls); Assert.Single(w.AskFiles); Assert.Null(w.CurrentChatId);
        }
        finally { w.Close(); Directory.Delete(d, true); }
    }

    // ── Gegenprüfung T-0734 ──

    [AvaloniaFact(DisplayName = "T-0734 Gegenprüfung: Anhang beim Senden nicht lesbar (leer, gelöscht) oder Bild ohne Bildmodell → Frage bleibt im Eingabefeld, Anhang bleibt, Meldung nennt die Datei")]
    public async Task Unreadable_attachment_keeps_question()
    {
        var (w, _, _) = Ui.Open(); var d = Tmp();
        try
        {
            var ki = new SlowKi(["Erfunden"], models: ["qwen3:14b"]);
            w.KiBackendsOverride = () => [ki];
            w.Show("ki-fragen");
            // 1) leere Textdatei
            var leer = Path.Combine(d, "leer.txt"); File.WriteAllText(leer, "   ");
            w.AskAttach([leer]);
            w.Find<TextBox>("ask-input").Text = "Was steht in der Notiz?";
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.Find<TextBlock>("ask-state").Text?.Contains("leer.txt") == true && w.Find<Button>("ask-send").IsEnabled);
            Assert.Contains("kein lesbarer Text", w.Find<TextBlock>("ask-state").Text);
            Assert.Equal("Was steht in der Notiz?", w.Find<TextBox>("ask-input").Text);
            Assert.Single(w.AskFiles); Assert.Empty(ki.Calls); Assert.Null(w.CurrentChatId);

            // 2) Datei seit dem Anhängen gelöscht
            var weg = Path.Combine(d, "weg.txt"); File.WriteAllText(weg, "Inhalt");
            w.Find<Button>("ask-file-del-0").Press();
            w.AskAttach([weg]); File.Delete(weg);
            w.Show("ki-fragen");
            Assert.Equal("Was steht in der Notiz?", w.Find<TextBox>("ask-input").Text); // Entwurf übersteht das Neuzeichnen
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.Find<TextBlock>("ask-state").Text?.Contains("weg.txt") == true && w.Find<Button>("ask-send").IsEnabled);
            Assert.Equal("Was steht in der Notiz?", w.Find<TextBox>("ask-input").Text);
            Assert.Single(w.AskFiles);

            // 3) Bild ohne Bildmodell: Antwort kommt erst von der KI-Suche zurück – auch dann bleibt die Frage stehen
            w.Find<Button>("ask-file-del-0").Press();
            var png = Path.Combine(d, "foto.png"); File.WriteAllBytes(png, Png.RedSquare());
            w.AskAttach([png]);
            w.Find<TextBox>("ask-input").Text = "Was ist auf dem Foto?";
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.Find<TextBlock>("ask-state").Text == KiAttachments.NoVisionModel && w.Find<Button>("ask-send").IsEnabled);
            Assert.Equal("Was ist auf dem Foto?", w.Find<TextBox>("ask-input").Text);
            Assert.Single(w.AskFiles); Assert.Empty(ki.Calls);
        }
        finally { w.Close(); Directory.Delete(d, true); }
    }

    [AvaloniaFact(DisplayName = "T-0734 Gegenprüfung: Datei lesen (hier ein langsames pdftotext) läuft neben der Oberfläche; „Stopp“ bricht das Lesen in unter 3 s ab, Frage und Anhang bleiben")]
    public async Task Reading_off_ui_and_stoppable()
    {
        var (w, _, _) = Ui.Open(); var d = Tmp();
        try
        {
            var slow = Path.Combine(d, "pdftotext-langsam.cmd");
            File.WriteAllText(slow, "@ping -n 12 127.0.0.1 >nul\r\n@echo Text aus dem PDF\r\n");
            w.AskPdfToTextOverride = slow;
            var ki = new SlowKi(["ok"]);
            w.KiBackendsOverride = () => [ki];
            var pdf = Path.Combine(d, "bericht.pdf"); File.WriteAllText(pdf, "%PDF-1.4");
            w.Show("ki-fragen");
            w.AskAttach([pdf]);
            w.Find<TextBox>("ask-input").Text = "Fasse den Bericht zusammen";
            var sw = Stopwatch.StartNew();
            RealClick(w, w.Find<Button>("ask-send"));
            var clickMs = sw.ElapsedMilliseconds;
            Assert.True(clickMs < 3000, $"Oberfläche stand {clickMs} ms still, während die Datei gelesen wurde");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(w.Find<Button>("ask-stop").IsVisible);
            Assert.Contains("liest", w.Find<TextBlock>("ask-state").Text);
            sw.Restart();
            RealClick(w, w.Find<Button>("ask-stop"));
            await Until(() => w.Find<Button>("ask-send").IsEnabled, 3000);
            Assert.True(sw.ElapsedMilliseconds < 3000, $"Stopp beim Lesen dauerte {sw.ElapsedMilliseconds} ms");
            Assert.Contains("Abgebrochen", w.Find<TextBlock>("ask-state").Text);
            Assert.Equal("Fasse den Bericht zusammen", w.Find<TextBox>("ask-input").Text);
            Assert.Single(w.AskFiles); Assert.Empty(ki.Calls);
        }
        finally { w.Close(); Directory.Delete(d, true); }
    }

    [AvaloniaFact(DisplayName = "T-0734 Gegenprüfung: während einer laufenden Antwort abgelegte Datei bleibt für die nächste Frage; nur die gesendeten Anhänge verschwinden")]
    public async Task Drop_during_answer_is_kept()
    {
        var (w, _, _) = Ui.Open(); var d = Tmp();
        try
        {
            var a = Path.Combine(d, "erste.txt"); File.WriteAllText(a, "Erste Datei");
            var b = Path.Combine(d, "zweite.txt"); File.WriteAllText(b, "Zweite Datei");
            var ki = new SlowKi(["Eins ", "zwei ", "drei ", "vier ", "fünf."]);
            w.KiBackendsOverride = () => [ki];
            w.Show("ki-fragen");
            w.AskAttach([a]);
            w.Find<TextBox>("ask-input").Text = "Was steht drin?";
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.TryFind<Border>("ask-stream") is not null);
            w.AskAttach([b]); // wie Ablegen auf dem Chat während die Antwort entsteht
            await Until(() => w.CurrentChatId is not null && w.Find<Button>("ask-send").IsEnabled);
            Assert.Equal([b], w.AskFiles);
            Assert.DoesNotContain("Zweite Datei", ki.Calls[0][^1].Content);
        }
        finally { w.Close(); Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0734 Gegenprüfung: OpenAI-kompatibler Server bekommt den richtigen Bildtyp (JPG als image/jpeg, PNG als image/png)")]
    public void OpenAi_image_mime()
    {
        string Json(byte[] img) => System.Text.Json.JsonSerializer.Serialize(KiBackendsShared.OpenAiMessages([new KiMessage("user", "Was ist das?", [Convert.ToBase64String(img)])]));
        Assert.Contains("data:image/jpeg;base64,", Json([0xFF, 0xD8, 0xFF, 0xE0, 0, 16]));
        Assert.Contains("data:image/png;base64,", Json(Png.RedSquare(8)));
    }

    [AvaloniaFact(DisplayName = "T-0734 Gegenprüfung: BMP wird vor dem Senden mit SkiaSharp in PNG umgewandelt – das Bildmodell bekommt ein echtes PNG")]
    public async Task Bmp_converted_to_png()
    {
        var (w, _, _) = Ui.Open(); var d = Tmp();
        try
        {
            var bmp = Path.Combine(d, "skizze.bmp"); File.WriteAllBytes(bmp, Bmp24(4, 4));
            var ki = new SlowKi(["Ein rotes Quadrat."], models: ["qwen2.5vl:7b"]);
            w.KiBackendsOverride = () => [ki];
            w.Show("ki-fragen");
            w.AskAttach([bmp]);
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.CurrentChatId is not null && w.Find<Button>("ask-send").IsEnabled);
            var sent = Convert.FromBase64String(Assert.Single(ki.Calls[0][^1].Images!));
            Assert.Equal("image/png", KiAttachments.ImageMime(sent));
            Assert.Contains("data:image/png;base64,", System.Text.Json.JsonSerializer.Serialize(KiBackendsShared.OpenAiMessages(ki.Calls[0])));
        }
        finally { w.Close(); Directory.Delete(d, true); }
    }

    /// <summary>Kleines echtes 24-Bit-BMP (rot).</summary>
    static byte[] Bmp24(int wd, int ht)
    {
        var row = (wd * 3 + 3) / 4 * 4; var size = 54 + row * ht;
        var b = new byte[size];
        b[0] = (byte)'B'; b[1] = (byte)'M'; BitConverter.GetBytes(size).CopyTo(b, 2); BitConverter.GetBytes(54).CopyTo(b, 10);
        BitConverter.GetBytes(40).CopyTo(b, 14); BitConverter.GetBytes(wd).CopyTo(b, 18); BitConverter.GetBytes(ht).CopyTo(b, 22);
        BitConverter.GetBytes((short)1).CopyTo(b, 26); BitConverter.GetBytes((short)24).CopyTo(b, 28); BitConverter.GetBytes(row * ht).CopyTo(b, 34);
        for (var y = 0; y < ht; y++) for (var x = 0; x < wd; x++) { var o = 54 + y * row + x * 3; b[o] = 20; b[o + 1] = 20; b[o + 2] = 220; }
        return b;
    }

    [AvaloniaFact(DisplayName = "T-0734 ECHT: Ollama liest eine angehängte Datei, Antwort wächst sichtbar, Folgefrage kennt die Datei; Bildmodell erkennt die Farbe eines Bildes (nur mit TRENDO_ECHT_KIFRAGEN)")]
    public async Task Real_ollama_attachment_and_stream()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_ECHT_KIFRAGEN") is not { Length: > 0 } dirEnv) { Assert.Skip("Übersprungen – Echtlauf nur mit TRENDO_ECHT_KIFRAGEN=<Belegordner> und laufendem Ollama."); return; }
        var dir = Directory.CreateDirectory(dirEnv).FullName;
        var tags = await new OllamaBackend().ModelsAsync();
        if (tags.Count == 0) { Assert.Skip("BLOCKIERT – Ollama läuft nicht auf 127.0.0.1:11434."); return; }
        var textModel = new[] { "qwen2.5:7b", "qwen2.5:3b", "mistral:7b" }.FirstOrDefault(tags.Contains);
        if (textModel is null) { Assert.Skip("BLOCKIERT – kein Textmodell (qwen2.5:7b/3b, mistral:7b) in Ollama."); return; }
        var (w, _, _) = Ui.Open(); var d = Tmp();
        var log = new StringBuilder();
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            w.InstalledModelsOverride = () => [new InstalledModel(textModel, 1)];
            var note = Path.Combine(d, "lieferschein.txt");
            File.WriteAllText(note, "Lieferschein Nr. 4711\nKunde: Erika Muster\nWare: 12 Kartons Druckerpapier A4\nLiefertag: Donnerstag, 20. November\nFahrer: Tour 3");
            w.Show("ki-fragen");
            w.AskAttach([note]);
            log.AppendLine($"Modell: {textModel} (Ollama 127.0.0.1:11434)").AppendLine("Hinweis: " + w.Find<TextBlock>("ask-attach-hint").Text);
            var input = w.Find<TextBox>("ask-input"); input.Text = "Wie viele Kartons werden geliefert und an welchem Tag? Antworte in einem Satz.";
            var sw = Stopwatch.StartNew();
            RealClick(w, w.Find<Button>("ask-send"));
            var samples = new List<(long Ms, int Len)>();
            await Until(() => { if (w.AskStreamShown is { } s && !s.StartsWith("Trendo denkt") && (samples.Count == 0 || samples[^1].Len != s.Length)) samples.Add((sw.ElapsedMilliseconds, s.Length)); return w.TryFind<Border>("ask-msg-1") is not null; }, 240_000);
            var chat = w.Chats.Get(w.CurrentChatId!)!;
            var answer = chat.Messages[1].Content;
            log.AppendLine($"Frage 1 fertig nach {sw.ElapsedMilliseconds} ms, Mitschrift-Stände: {samples.Count} ({string.Join(", ", samples.Take(12).Select(s => $"{s.Ms} ms:{s.Len} Z"))} …)").AppendLine("Antwort 1: " + answer);
            Assert.True(samples.Count >= 3, "Antwort ist nicht sichtbar mitgewachsen");
            Assert.Contains("12", answer);
            Assert.Contains("Donnerstag", answer, StringComparison.OrdinalIgnoreCase);

            // Folgefrage ohne neuen Anhang: der Dateitext ist im Verlauf
            input = w.Find<TextBox>("ask-input"); input.Text = "Welche Lieferschein-Nummer steht in der Datei? Nur die Zahl.";
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.Chats.Get(w.CurrentChatId!)!.Messages.Count == 4, 240_000);
            var answer2 = w.Chats.Get(w.CurrentChatId!)!.Messages[3].Content;
            log.AppendLine("Antwort 2 (Folgefrage): " + answer2);
            Assert.Contains("4711", answer2);

            // Stopp mitten in einer echten Antwort: unter 2 s, das Geschriebene bleibt
            input = w.Find<TextBox>("ask-input"); input.Text = "Schreib eine ausführliche Geschichte über einen Leuchtturm, mindestens 600 Wörter.";
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => (w.AskStreamShown?.Length ?? 0) > 80 && !w.AskStreamShown!.StartsWith("Trendo denkt"), 120_000);
            var stopWatch = Stopwatch.StartNew();
            RealClick(w, w.Find<Button>("ask-stop"));
            await Until(() => w.Find<Button>("ask-send").IsEnabled, 2000);
            stopWatch.Stop();
            var partial = w.Chats.Get(w.CurrentChatId!)!.Messages[^1].Content;
            log.AppendLine($"Stopp nach {stopWatch.ElapsedMilliseconds} ms, behalten: {partial.Length} Zeichen, Ende: …{partial[^Math.Min(40, partial.Length)..].Replace("\n", " ")}");
            Assert.True(stopWatch.ElapsedMilliseconds < 2000); Assert.EndsWith("(abgebrochen)", partial);

            // Bild: nur wenn ein Bildmodell da ist
            var vision = tags.FirstOrDefault(KiAttachments.IsVisionModel);
            if (vision is null) log.AppendLine("Bild: BLOCKIERT – kein Bildmodell in Ollama.");
            else
            {
                var png = Path.Combine(d, "flaeche.png"); File.WriteAllBytes(png, Png.RedSquare());
                File.Copy(png, Path.Combine(dir, "t0734-testbild.png"), true);
                w.Find<Button>("ask-chat-new").Press();
                w.AskAttach([png]);
                input = w.Find<TextBox>("ask-input"); input.Text = "Welche Farbe hat die große Fläche in der Mitte des Bildes? Antworte mit einem Wort.";

                RealClick(w, w.Find<Button>("ask-send"));
                Assert.False(w.Find<Button>("ask-send").IsEnabled);
                await Until(() => w.Find<Button>("ask-send").IsEnabled, 300_000);
                var state = w.Find<TextBlock>("ask-state").Text;
                var img = w.CurrentChatId is { } id ? w.Chats.Get(id)!.Messages[1] : null;
                log.AppendLine($"Bild an {vision}: {state}").AppendLine("Antwort Bild: " + img?.Content + " · " + img?.Model);
                Assert.NotNull(img);
                Assert.Contains("rot", img!.Content, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(vision, img.Model);
            }
            var audit = w.Core.Audit.ReadAll().Where(e => e.Action == "ki.ask").Select(e => e.Detail).ToList();
            log.AppendLine("Audit ki.ask:").AppendLine(string.Join("\n", audit));
            Assert.Contains(audit, a => a.Contains("anhang=1 bild=0"));
            Assert.DoesNotContain(audit, a => a.Contains("4711"));
        }
        finally
        {
            var f = Path.Combine(dir, "t0734-echtlauf.log");
            // Gegenprüfung T-0734: einheitlich \n (vorher gemischt CRLF/LF – der Hash war nach dem Auschecken nicht nachprüfbar)
            File.WriteAllText(f, log.ToString().Replace(Environment.UserName, "<user>").Replace(Environment.MachineName, "<pc>").ReplaceLineEndings("\n"));
            var sums = new StringBuilder();
            foreach (var x in Directory.GetFiles(dir, "t0734-*").Where(x => !x.EndsWith("SHA256.txt")).Order()) sums.Append($"{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(x)))}  {Path.GetFileName(x)}\n");
            File.WriteAllText(Path.Combine(dir, "t0734-SHA256.txt"), sums.ToString());
            w.Close(); Directory.Delete(d, true);
        }
    }
}

/// <summary>Kleines echtes PNG ohne Bildbibliothek: weißer Grund, große rote Fläche in der Mitte.</summary>
static class Png
{
    public static byte[] RedSquare(int size = 256)
    {
        var raw = new MemoryStream();
        for (var y = 0; y < size; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < size; x++)
            {
                var inside = x > size / 5 && x < size * 4 / 5 && y > size / 5 && y < size * 4 / 5;
                raw.Write(inside ? [220, 20, 20] : [255, 255, 255]);
            }
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) raw.WriteTo(zs);
        var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13]; Be(ihdr, 0, size); Be(ihdr, 4, size); ihdr[8] = 8; ihdr[9] = 2;
        Chunk(png, "IHDR", ihdr); Chunk(png, "IDAT", z.ToArray()); Chunk(png, "IEND", []);
        return png.ToArray();
    }
    static void Be(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; Be(len, 0, data.Length); s.Write(len);
        var td = Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); s.Write(td);
        var crc = new byte[4]; Be(crc, 0, (int)Crc(td)); s.Write(crc);
    }
    static uint Crc(byte[] d)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in d) { c ^= b; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; }
        return c ^ 0xFFFFFFFFu;
    }
}
