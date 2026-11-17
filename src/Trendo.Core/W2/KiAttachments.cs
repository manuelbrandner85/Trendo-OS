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

    /// <summary>Datei lesen. <paramref name="pdfToText"/> ist der Pfad zu pdftotext (Poppler) oder null. Wirft <see cref="InvalidOperationException"/> mit einer deutschen Meldung, wenn es nicht geht.</summary>
    public static KiAttachment Load(string path, string? pdfToText = null)
    {
        var name = Path.GetFileName(path);
        if (!File.Exists(path)) throw new InvalidOperationException($"„{name}“ gibt es nicht mehr.");
        var ext = Ext(path);
        if (ImageExtensions.Contains(ext))
        {
            var len = new FileInfo(path).Length;
            if (len > MaxImageBytes) throw new InvalidOperationException($"„{name}“ ist zu groß für die KI (höchstens {MaxImageBytes / 1024 / 1024} MB).");
            return new(name, true, "", File.ReadAllBytes(path));
        }
        string text;
        if (TextExtensions.Contains(ext)) text = File.ReadAllText(path);
        else if (ext is ".docx" or ".odt")
        {
            try { text = ext == ".docx" ? Ooxml.DocxText(path) : Ooxml.OdtText(path); }
            catch (Exception e) when (e is not InvalidOperationException) { throw new InvalidOperationException($"„{name}“ lässt sich nicht lesen: {e.Message}"); }
        }
        else if (ext == ".pdf")
        {
            var (ok, t, how) = PdfText.Extract(path, pdfToText, null, null);
            if (!ok) throw new InvalidOperationException($"„{name}“: {how}");
            text = t;
        }
        else throw new InvalidOperationException($"„{name}“ kann die KI nicht lesen. Möglich sind Text (TXT, MD, CSV), PDF, Word (DOCX, ODT) und Bilder.");
        text = text.Replace("\r\n", "\n").Trim();
        if (text.Length == 0) throw new InvalidOperationException($"In „{name}“ steht kein lesbarer Text.");
        if (text.Length > MaxTextChars) text = text[..MaxTextChars] + $"\n[… gekürzt, die Datei ist länger als {MaxTextChars} Zeichen]";
        return new(name, false, text);
    }

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
