using System.Text;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0734: Dateien an eine KI-Frage hängen (Text, PDF, Word, Bild) und Antwort beim Entstehen mitlesen – über den vorhandenen <see cref="IntelligenceCore"/>.</summary>
public class KiAttachmentsT0734
{
    sealed class StreamKi(string name, string[] parts, string[]? models = null, bool remote = false) : IStreamingKiBackend
    {
        public string Name => name; public string BaseUrl => "http://127.0.0.1:1";
        public List<IReadOnlyList<KiMessage>> Calls { get; } = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(models ?? ["test-modell"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> m, CancellationToken ct = default) => ChatStreamAsync(model, m, _ => { }, ct);
        public Task<string> ChatStreamAsync(string model, IReadOnlyList<KiMessage> m, Action<string> onDelta, CancellationToken ct = default)
        { Calls.Add(m); foreach (var p in parts) onDelta(p); return Task.FromResult(string.Concat(parts)); }
    }
    sealed class Remote(string name) : IRemoteKiBackend
    {
        public string Name => name; public string BaseUrl => "https://example.invalid";
        public int Calls;
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["fern"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> m, CancellationToken ct = default) { Calls++; return Task.FromResult("aus dem Internet"); }
    }
    sealed class Main(string name) : IMainKiBackend
    {
        public string Name => name; public string BaseUrl => "claude-code";
        public List<IReadOnlyList<KiMessage>> Calls { get; } = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["claude"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> m, CancellationToken ct = default) { Calls.Add(m); return Task.FromResult("von Claude"); }
    }

    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0734-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0734: TXT, MD, DOCX und Bild werden gelesen; unbekannte Art und leere Datei melden verständlich, warum nicht")]
    public void Load_files()
    {
        var d = Tmp();
        try
        {
            File.WriteAllText(Path.Combine(d, "notiz.txt"), "Termin am Dienstag um 9 Uhr.");
            File.WriteAllText(Path.Combine(d, "liste.md"), "# Einkauf\n- Mehl\n- Zucker");
            Ooxml.NewDocx(Path.Combine(d, "brief.docx"), "Brief", ["Sehr geehrte Frau Muster,", "die Lieferung kommt am Freitag."]);
            File.WriteAllBytes(Path.Combine(d, "foto.png"), [137, 80, 78, 71, 13, 10, 26, 10, 0, 0]);
            File.WriteAllText(Path.Combine(d, "leer.txt"), "  ");
            File.WriteAllText(Path.Combine(d, "programm.exe"), "MZ");

            var t = KiAttachments.Load(Path.Combine(d, "notiz.txt"));
            Assert.False(t.IsImage); Assert.Equal("notiz.txt", t.Name); Assert.Contains("Dienstag", t.Text);
            Assert.Contains("Zucker", KiAttachments.Load(Path.Combine(d, "liste.md")).Text);
            Assert.Contains("Lieferung kommt am Freitag", KiAttachments.Load(Path.Combine(d, "brief.docx")).Text);
            var img = KiAttachments.Load(Path.Combine(d, "foto.png"));
            Assert.True(img.IsImage); Assert.Equal(10, img.Image!.Length);

            Assert.Contains("kein lesbarer Text", Assert.Throws<InvalidOperationException>(() => KiAttachments.Load(Path.Combine(d, "leer.txt"))).Message);
            Assert.Contains("kann die KI nicht lesen", Assert.Throws<InvalidOperationException>(() => KiAttachments.Load(Path.Combine(d, "programm.exe"))).Message);
            var pdf = Path.Combine(d, "leer.pdf"); PdfTools.NewBlank(pdf);
            Assert.Contains("Poppler", Assert.Throws<InvalidOperationException>(() => KiAttachments.Load(pdf, null)).Message); // ohne pdftotext: ehrliche Meldung
            Assert.False(KiAttachments.IsSupported("a.exe")); Assert.True(KiAttachments.IsSupported("A.PDF")); Assert.True(KiAttachments.IsImage("b.JPG"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0734 ECHT: PDF mit Text wird über das echte pdftotext gelesen (übersprungen, wenn Poppler fehlt)")]
    public void Load_pdf_real()
    {
        var tool = Cli.Find("pdftotext");
        if (tool is null) Ueberspringen.Weil("BLOCKIERT – pdftotext (Poppler) fehlt auf diesem PC.");
        var d = Tmp();
        try
        {
            var doc = new Trendo.Core.Office.TextDoc { Title = "Rechnung", Blocks = [new Trendo.Core.Office.Para("Rechnungsbetrag 128,50 Euro, fällig am 30. November.")] };
            var pdf = Path.Combine(d, "rechnung.pdf");
            Trendo.Core.Office.TextPdf.Export(doc, pdf);
            var a = KiAttachments.Load(pdf, tool);
            Assert.Contains("128,50", a.Text);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0734: Dateitext steht in der Frage an die KI auf dem PC, die Antwort wächst Stück für Stück mit, Protokoll ohne Inhalt")]
    public async Task Text_attachment_and_stream()
    {
        using var f = new Fixture();
        var ki = new StreamKi("Lokal", ["Der ", "Termin ", "ist ", "Dienstag."]);
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [ki]);
        var seen = new List<string>();
        var a = await ic.AskAsync(new KiRequest("Wann ist der Termin?", UseKnowledge: false, Attachments: [new KiAttachment("notiz.txt", false, "Termin am Dienstag um 9 Uhr.")], OnText: seen.Add));
        Assert.True(a.Ok); Assert.Equal("Der Termin ist Dienstag.", a.Text);
        var user = ki.Calls[0][^1];
        Assert.Equal("user", user.Role);
        Assert.StartsWith("Wann ist der Termin?", user.Content);
        Assert.Contains("--- Angehängte Datei: notiz.txt ---\nTermin am Dienstag um 9 Uhr.", user.Content);
        Assert.Null(user.Images);
        Assert.Equal(["", "Der ", "Termin ", "ist ", "Dienstag."], seen); // Gegenprüfung: nur neue Stücke (kein O(n²) durch den ganzen Text je Stück)
        var log = string.Join("\n", f.Core.Audit.ReadAll().Select(e => e.Detail));
        Assert.Contains("anhang=1 bild=0", log); Assert.DoesNotContain("Dienstag", log);
    }

    [Fact(DisplayName = "T-0734: mit Anhang antwortet nie ein fremder KI-Dienst; Claude als Haupt-KI (Abo) bekommt den Dateitext, Bilder nie")]
    public async Task Attachments_never_to_other_services()
    {
        using var f = new Fixture();
        var cloud = new Remote("Dienst"); var main = new Main("Claude (dein Abo)");
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [cloud]);
        var a = await ic.AskAsync(new KiRequest("Fasse zusammen", AllowRemote: true, UseKnowledge: false, Attachments: [new KiAttachment("a.txt", false, "Inhalt")]));
        Assert.False(a.Ok); Assert.Equal(0, cloud.Calls);
        // ohne Anhang darf der eingeschaltete Dienst im freien Chat wie bisher einspringen
        Assert.True((await ic.AskAsync(new KiRequest("Hallo", AllowRemote: true, UseKnowledge: false))).Ok); Assert.Equal(1, cloud.Calls);

        var ic2 = new IntelligenceCore(f.Root, f.Core.Audit, () => [main]);
        var b = await ic2.AskAsync(new KiRequest("Fasse zusammen", UseKnowledge: false, Attachments: [new KiAttachment("a.txt", false, "Geheimer Inhalt")]));
        Assert.True(b.Ok); Assert.Contains("Geheimer Inhalt", main.Calls[0][^1].Content);
        var c = await ic2.AskAsync(new KiRequest("Was zeigt das Bild?", UseKnowledge: false, Attachments: [new KiAttachment("foto.png", true, "", [1, 2, 3])]));
        Assert.False(c.Ok); Assert.Single(main.Calls);
    }

    [Fact(DisplayName = "T-0734: Bild geht nur an ein Bildmodell auf dem PC (Base64 an der Nachricht); ohne Bildmodell klare Meldung statt erfundener Antwort")]
    public async Task Image_only_to_vision_model()
    {
        using var f = new Fixture();
        var ki = new StreamKi("Ollama", ["Eine Katze."], models: ["qwen3:14b", "qwen2.5vl:7b"]);
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [ki]) { LocalFallbacks = _ => ["qwen2.5:0.5b"] };
        var a = await ic.AskAsync(new KiRequest("Was ist zu sehen?", PreferredModel: "qwen3:14b", UseKnowledge: false, Attachments: [new KiAttachment("katze.png", true, "", [1, 2, 3])]));
        Assert.True(a.Ok); Assert.Equal("qwen2.5vl:7b", a.Model);
        Assert.Equal([Convert.ToBase64String([1, 2, 3])], ki.Calls[0][^1].Images);
        Assert.Contains("[Angehängtes Bild: katze.png]", ki.Calls[0][^1].Content);

        var textOnly = new StreamKi("Ollama", ["Erfunden"], models: ["qwen3:14b", "mistral:7b"]);
        var ic2 = new IntelligenceCore(f.Root, f.Core.Audit, () => [textOnly]);
        var b = await ic2.AskAsync(new KiRequest("Was ist zu sehen?", UseKnowledge: false, Attachments: [new KiAttachment("katze.png", true, "", [1, 2, 3])]));
        Assert.False(b.Ok); Assert.Equal(KiAttachments.NoVisionModel, b.Text); Assert.Empty(textOnly.Calls);
    }

    [Fact(DisplayName = "T-0734: Bildmodelle am Namen erkannt (llava, qwen2.5vl, llama3.2-vision, gemma3 außer 1b), reine Textmodelle nicht")]
    public void Vision_names()
    {
        foreach (var m in new[] { "llava:7b", "qwen2.5vl:7b", "qwen2-vl:2b", "llama3.2-vision:11b", "gemma3:4b", "moondream:latest", "minicpm-v:8b" }) Assert.True(KiAttachments.IsVisionModel(m), m);
        foreach (var m in new[] { "qwen3:14b", "qwen2.5:7b", "mistral:7b", "gemma3:1b", "qwen2.5-coder:14b" }) Assert.False(KiAttachments.IsVisionModel(m), m);
    }

    // ── Gegenprüfung T-0734 ──

    [Fact(DisplayName = "T-0734 Gegenprüfung: große Textdatei wird nur am Anfang gelesen (kein Einfrieren, kein Speicherüberlauf) und als gekürzt markiert")]
    public void Big_text_reads_only_head()
    {
        var d = Tmp();
        try
        {
            var big = Path.Combine(d, "gross.log");
            var line = Encoding.ASCII.GetBytes("Zeile mit Messwert 42\n");
            using (var fs = new FileStream(big, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20)) for (var i = 0; i < 40 * 1024 * 1024 / line.Length; i++) fs.Write(line);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var a = KiAttachments.Load(big);
            var used = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(used < 8L * 1024 * 1024, $"für 40 MB Datei {used / 1024 / 1024} MB Speicher belegt – es wird mehr gelesen als nötig");
            Assert.StartsWith("Zeile mit Messwert 42", a.Text);
            Assert.Contains("gekürzt", a.Text);
            Assert.True(a.Text.Length <= KiAttachments.MaxTextChars + 200);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0734 Gegenprüfung: Kodierung erkannt – ANSI-CSV (Windows-1252) mit richtigen Umlauten, UTF-8 mit BOM ohne BOM-Zeichen, UTF-8 beim Kürzen nicht zerbrochen")]
    public void Encoding_detected()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var d = Tmp();
        try
        {
            var ansi = Path.Combine(d, "preise.csv");
            File.WriteAllBytes(ansi, Encoding.GetEncoding(1252).GetBytes("Größe;Maß;Preis €\nÄpfel;5;3,20"));
            Assert.Contains("Größe;Maß;Preis €", KiAttachments.Load(ansi).Text);
            var bom = Path.Combine(d, "gruss.txt");
            File.WriteAllText(bom, "Grüße aus Graz", new UTF8Encoding(true));
            Assert.Equal("Grüße aus Graz", KiAttachments.Load(bom).Text);
            var lang = Path.Combine(d, "umlaute.txt"); // länger als die Grenze, damit mitten in einem Zeichen gekürzt werden kann
            File.WriteAllText(lang, "x" + new string('ä', KiAttachments.MaxTextChars * 3), new UTF8Encoding(false));
            var t = KiAttachments.Load(lang).Text;
            Assert.StartsWith("xäää", t); Assert.DoesNotContain("Ã", t);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0734 Gegenprüfung: PNG und JPG gehen unverändert mit; GIF/BMP/WEBP ohne Umwandlung ehrlich abgelehnt statt falsch beschriftet")]
    public void Image_formats_honest()
    {
        var d = Tmp();
        try
        {
            var jpg = Path.Combine(d, "foto.jpg"); File.WriteAllBytes(jpg, [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 74, 70, 73, 70]);
            Assert.Equal(10, KiAttachments.Load(jpg).Image!.Length);
            var gif = Path.Combine(d, "bild.gif"); File.WriteAllBytes(gif, [.. Encoding.ASCII.GetBytes("GIF89a"), 1, 0, 1, 0, 0, 0, 0]);
            Assert.Contains("PNG oder JPG", Assert.Throws<InvalidOperationException>(() => KiAttachments.Load(gif)).Message);
            byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 7];
            Assert.Equal(png, KiAttachments.Load(gif, toPng: _ => png).Image); // mit Umwandlung geht das PNG mit
            Assert.Contains("PNG oder JPG", Assert.Throws<InvalidOperationException>(() => KiAttachments.Load(gif, toPng: _ => null)).Message); // nicht umwandelbar: ehrlich ablehnen
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0734 Gegenprüfung: großer Anhangstext im Verlauf wird für die KI auf dem PC begrenzt – die Systemanweisung bleibt vollständig vorn")]
    public async Task History_capped_system_intact()
    {
        using var f = new Fixture();
        var ki = new StreamKi("Lokal", ["ok"]);
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [ki]);
        var big = "Fasse zusammen\n\n--- Angehängte Datei: a.txt ---\n" + new string('x', 200_000) + "\n--- Ende a.txt ---";
        List<KiMessage> hist = [new("user", big), new("assistant", "Zusammenfassung"), new("user", "Frage 2"), new("assistant", "Antwort 2")];
        var a = await ic.AskAsync(new KiRequest("Und jetzt?", SystemPrompt: "SYSTEMREGEL-T0734", History: hist, UseKnowledge: false));
        Assert.True(a.Ok);
        var m = ki.Calls[0];
        Assert.Equal("system", m[0].Role); Assert.Contains("SYSTEMREGEL-T0734", m[0].Content);
        Assert.True(m.Sum(x => x.Content.Length) <= 36_000, $"{m.Sum(x => x.Content.Length)} Zeichen gehen an die KI – Ollama schneidet dann vorn ab (auch die Systemanweisung)");
        Assert.Equal("Und jetzt?", m[^1].Content);
        Assert.Contains(m, x => x.Content == "Antwort 2");
        Assert.Contains(m, x => x.Content.StartsWith("Fasse zusammen", StringComparison.Ordinal) && x.Content.Contains("gekürzt"));
    }
}
