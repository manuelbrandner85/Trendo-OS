using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core.Sys;

namespace Trendo.Core;

/// <summary>T-0734: eine Datei, die an eine Frage in „KI fragen“ gehängt wird. Text-Dateien (TXT, MD, PDF, DOCX …) werden als Text gelesen,
/// Bilder bleiben Bilddaten und gehen nur an ein Bildmodell auf diesem PC.</summary>
public sealed record KiAttachment(string Name, bool IsImage, string Text, byte[]? Image = null);

/// <summary>T-0734: Dateien für eine KI-Frage lesen und in die Frage einbauen. Kein eigener KI-Weg – das Ergebnis geht über <see cref="IntelligenceCore.AskAsync"/>.</summary>
public static class KiAttachments
{
    /// <summary>So viele Zeichen je Datei gehen höchstens mit (sonst sprengt eine große Datei das Gedächtnis kleiner Modelle).</summary>
    public const int MaxTextChars = 40_000;
    public const long MaxImageBytes = 15 * 1024 * 1024;
    public const int MaxFiles = 5;

    public static readonly string[] TextExtensions = [".txt", ".md", ".markdown", ".csv", ".json", ".log", ".xml", ".yaml", ".yml", ".ini", ".html", ".htm"];
    public static readonly string[] DocExtensions = [".pdf", ".docx", ".odt"];
    public static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];
    public static string[] Patterns => [.. TextExtensions.Concat(DocExtensions).Concat(ImageExtensions).Select(e => "*" + e)];

    static string Ext(string path) => Path.GetExtension(path).ToLowerInvariant();
    public static bool IsImage(string path) => ImageExtensions.Contains(Ext(path));
    public static bool IsSupported(string path) => IsImage(path) || TextExtensions.Contains(Ext(path)) || DocExtensions.Contains(Ext(path));

    /// <summary>Gegenprüfung T-0734: Word-, ODT- und PDF-Dateien darüber werden gar nicht erst geöffnet (Lesen dauert sonst Minuten und füllt den Speicher).</summary>
    public const long MaxDocBytes = 200L * 1024 * 1024;
    /// <summary>Von Textdateien wird höchstens so viel gelesen (UTF-8 braucht bis zu 4 Byte je Zeichen) – der Rest bleibt auf der Platte, egal wie groß die Datei ist.</summary>
    const int MaxTextBytes = MaxTextChars * 4 + 4;

    /// <summary>Datei lesen. <paramref name="pdfToText"/> ist der Pfad zu pdftotext (Poppler) oder null. Wirft <see cref="InvalidOperationException"/> mit einer deutschen Meldung, wenn es nicht geht.</summary>
    /// <param name="toPng">Wandelt ein Bild (GIF, BMP, WEBP …) in PNG um oder liefert null; ohne Umwandlung gehen nur PNG und JPG mit (das verstehen die Bildmodelle auf dem PC sicher).</param>
    /// <param name="ct">„Stopp“: bricht auch das Lesen ab (bei pdftotext wird das Programm beendet).</param>
    public static KiAttachment Load(string path, string? pdfToText = null, Func<byte[], byte[]?>? toPng = null, CancellationToken ct = default)
    {
        var name = Path.GetFileName(path);
        if (!File.Exists(path)) throw new InvalidOperationException($"„{name}“ gibt es nicht mehr.");
        ct.ThrowIfCancellationRequested();
        var ext = Ext(path);
        var len = new FileInfo(path).Length;
        if (ImageExtensions.Contains(ext))
        {
            if (len > MaxImageBytes) throw new InvalidOperationException($"„{name}“ ist zu groß für die KI (höchstens {MaxImageBytes / 1024 / 1024} MB).");
            var bytes = File.ReadAllBytes(path);
            if (ImageMime(bytes) is "image/png" or "image/jpeg") return new(name, true, "", bytes);
            // Gegenprüfung T-0734: GIF, BMP, WEBP kennt nicht jedes Bildmodell – vorher nach PNG umwandeln oder ehrlich ablehnen
            var png = toPng?.Invoke(bytes);
            if (png is { Length: > 0 } && ImageMime(png) == "image/png") return new(name, true, "", png);
            throw new InvalidOperationException($"„{name}“ kann die KI auf diesem PC so nicht ansehen. Speichere das Bild bitte als PNG oder JPG.");
        }
        string text; var cut = false;
        if (TextExtensions.Contains(ext)) (text, cut) = ReadTextHead(path, ct);
        else if (DocExtensions.Contains(ext) && len > MaxDocBytes) throw new InvalidOperationException($"„{name}“ ist zu groß für die KI (höchstens {MaxDocBytes / 1024 / 1024} MB).");
        else if (ext is ".docx" or ".odt")
        {
            try { text = ext == ".docx" ? Ooxml.DocxText(path) : Ooxml.OdtText(path); }
            catch (Exception e) when (e is not InvalidOperationException and not OperationCanceledException) { throw new InvalidOperationException($"„{name}“ lässt sich nicht lesen: {e.Message}"); }
        }
        else if (ext == ".pdf")
        {
            if (pdfToText is null) throw new InvalidOperationException($"„{name}“: Dafür fehlt Poppler (pdftotext).");
            // Gegenprüfung T-0734: abbrechbar (Stopp beendet pdftotext), statt bis zu 60 s fest zu warten
            var r = Cli.RunStreamAsync(pdfToText, ["-layout", "-enc", "UTF-8", path, "-"], null, null, ct, 60_000, encoding: new UTF8Encoding(false)).GetAwaiter().GetResult();
            ct.ThrowIfCancellationRequested();
            if (!r.Ok || r.Out.Trim().Length == 0) throw new InvalidOperationException($"„{name}“: Das PDF enthält keinen Text (Scan). Für Texterkennung fehlen Poppler oder Tesseract.");
            text = r.Out;
        }
        else throw new InvalidOperationException($"„{name}“ kann die KI nicht lesen. Möglich sind Text (TXT, MD, CSV), PDF, Word (DOCX, ODT) und Bilder.");
        text = text.Replace("\r\n", "\n").Trim();
        if (text.Length == 0) throw new InvalidOperationException($"In „{name}“ steht kein lesbarer Text.");
        if (text.Length > MaxTextChars) { text = text[..MaxTextChars]; cut = true; }
        if (cut) text += $"\n[… gekürzt, die Datei ist länger als {MaxTextChars} Zeichen]";
        return new(name, false, text);
    }

    /// <summary>Gegenprüfung T-0734: alle Anhänge neben der Oberfläche lesen. „Stopp“ (<paramref name="ct"/>) wirkt sofort, auch während eine Datei noch gelesen wird.</summary>
    public static Task<List<KiAttachment>> LoadAllAsync(IReadOnlyList<string> paths, string? pdfToText, Func<byte[], byte[]?>? toPng, CancellationToken ct) =>
        Task.Run(() => paths.Select(p => Load(p, pdfToText, toPng, ct)).ToList(), ct).WaitAsync(ct);

    /// <summary>Nur den Anfang einer Textdatei lesen und die Kodierung erkennen (UTF-8 mit/ohne BOM, UTF-16 mit BOM, sonst Windows-1252).</summary>
    static (string Text, bool Cut) ReadTextHead(string path, CancellationToken ct)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
        var buf = new byte[(int)Math.Min(MaxTextBytes, Math.Max(0, fs.Length))];
        var n = 0;
        while (n < buf.Length)
        {
            ct.ThrowIfCancellationRequested();
            var got = fs.Read(buf, n, Math.Min(1 << 16, buf.Length - n));
            if (got == 0) break;
            n += got;
        }
        var cut = fs.Length > n;
        if (cut) n = WholeChars(buf, n);
        var (text, _) = TextFileCodec.Decode(n == buf.Length ? buf : buf[..n]);
        return (text, cut);
    }

    /// <summary>Beim Kürzen nicht mitten in einem Zeichen aufhören (sonst hielte die Erkennung UTF-8 für Windows-1252).</summary>
    internal static int WholeChars(byte[] b, int n)
    {
        if (n >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF))) return n - (n % 2); // UTF-16
        var i = n - 1; var back = 0;
        while (i >= 0 && back < 3 && (b[i] & 0xC0) == 0x80) { i--; back++; }
        if (i < 0) return n;
        var lead = b[i]; var need = lead >= 0xF0 ? 4 : lead >= 0xE0 ? 3 : lead >= 0xC0 ? 2 : 1;
        return n - i < need ? i : n;
    }

    /// <summary>Bildtyp an den ersten Bytes erkennen (nicht an der Dateiendung).</summary>
    public static string? ImageMime(ReadOnlySpan<byte> b) =>
        b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 ? "image/png"
        : b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? "image/jpeg"
        : b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' ? "image/gif"
        : b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P' ? "image/webp"
        : b.Length >= 2 && b[0] == 'B' && b[1] == 'M' ? "image/bmp"
        : null;

    /// <summary>Frage mit dem Text der angehängten Dateien. Bilder stehen nur mit Namen drin (die Bilddaten gehen getrennt mit).</summary>
    public static string Compose(string question, IReadOnlyList<KiAttachment>? files)
    {
        if (files is not { Count: > 0 }) return question;
        var sb = new StringBuilder(question.Trim());
        foreach (var f in files)
        {
            if (f.IsImage) { sb.Append($"\n\n[Angehängtes Bild: {f.Name}]"); continue; }
            sb.Append($"\n\n--- Angehängte Datei: {f.Name} ---\n{f.Text}\n--- Ende {f.Name} ---");
        }
        return sb.ToString();
    }

    static readonly Regex Vision = new(@"(llava|bakllava|vision|moondream|minicpm-v|qwen[\d.]*-?vl|-vl\b|gemma3(?!:1b)|llama4|mistral-small3\.[12]|granite3\.2-vision|internvl|pixtral)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    /// <summary>Kann das Modell (nach seinem Namen) Bilder ansehen? Ollama-Modelle mit Bildverstehen tragen das im Namen (llava, qwen2.5vl, llama3.2-vision …).</summary>
    public static bool IsVisionModel(string model) => Vision.IsMatch(model);

    public const string NoVisionModel = "Auf diesem PC ist kein Modell, das Bilder ansehen kann. Lade unter „KI-Quellen“ ein Bildmodell (z. B. qwen2.5vl oder llava) – ohne Bildmodell beschreibt Trendo kein Bild, statt etwas zu erfinden.";
}
