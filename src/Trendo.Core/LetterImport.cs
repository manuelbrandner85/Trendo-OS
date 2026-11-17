using System.Globalization;
using System.Text.RegularExpressions;
using Trendo.Core.Office;

namespace Trendo.Core;

/// <summary>T-0725 (Audit D7, STUDIO-LETTER-01: „eine fremde DOCX/ODT lässt sich nicht als Brief öffnen“): einen fremden Brief (z. B. in Word geschrieben)
/// über die Leser von Trendo Text einlesen und in die Brief-Felder aufteilen – Anschrift, Datum/Ort, Betreff, Anrede, Text, Gruß, Anlagen.
/// Was nicht sicher erkannt wird, landet im Brieftext statt verloren zu gehen. Das Original wird nie verändert; gespeichert wird ein neuer Trendo-Brief.</summary>
public static partial class LetterImport
{
    public sealed record Result(LetterDraft Draft, ImportReport? Report, IReadOnlyList<string> Found);

    public static readonly string[] Extensions = ["docx", "odt", "txt"];

    [GeneratedRegex(@"^(Sehr geehrte|Guten Tag|Hallo|Liebe |Lieber |Liebe,|Moin|Servus)", RegexOptions.IgnoreCase)]
    private static partial Regex SalutationRx();
    [GeneratedRegex(@"^(Mit freundlichen Grüßen|Mit freundlichem Gruß|Freundliche Grüße|Viele Grüße|Beste Grüße|Herzliche Grüße|Liebe Grüße|Hochachtungsvoll|Mit besten Grüßen)", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingRx();
    [GeneratedRegex(@"^(Betreff|Betr\.)\s*:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectRx();
    [GeneratedRegex(@"(\d{1,2})\.\s?(\d{1,2})\.\s?(\d{4})")]
    private static partial Regex DateRx();
    [GeneratedRegex(@"^\d{5}\s+\S")]
    private static partial Regex ZipRx();
    [GeneratedRegex(@"^Anlage(n)?\s*:?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex AttachRx();

    /// <summary>Datei lesen und in <paramref name="baseDraft"/> (Absender aus den Einstellungen) einsetzen.</summary>
    public static Result Read(string path, LetterDraft baseDraft)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant().TrimStart('.');
        if (!Extensions.Contains(ext)) throw new ArgumentException($"Das Format .{ext} lässt sich nicht als Brief öffnen – bitte .docx, .odt oder .txt.");
        var doc = TextFiles.Open(path);
        var lines = doc.PlainText().Replace("\r", "").Replace("\f", "\n").Split('\n').Select(l => l.Trim()).ToList();
        var (d, found) = Split(lines, baseDraft);
        return new(d, doc.Import, found);
    }

    /// <summary>Zeilen in Brief-Felder aufteilen (öffentlich für Tests).</summary>
    public static (LetterDraft Draft, List<string> Found) Split(IReadOnlyList<string> lines, LetterDraft baseDraft)
    {
        var d = Clone(baseDraft); var found = new List<string>();
        d.Subject = ""; d.Body = ""; d.Recipient = ""; d.Reference = ""; d.Attachments = ""; d.Category = "frei";
        var sal = Index(lines, l => SalutationRx().IsMatch(l));
        var subj = Index(lines, l => SubjectRx().IsMatch(l));
        var head = sal >= 0 ? sal : (subj >= 0 ? subj + 1 : 0);
        // Betreff: „Betreff: …“ oder die letzte Textzeile vor der Anrede (kein Datum, nicht zu lang)
        if (subj >= 0 && subj < head) { d.Subject = SubjectRx().Replace(lines[subj], "").Trim(); found.Add("Betreff"); }
        else if (sal > 0 && Prev(lines, sal) is var ps and >= 0 && lines[ps].Length <= 120 && !DateRx().IsMatch(lines[ps]) && !ZipRx().IsMatch(lines[ps]))
        { d.Subject = lines[ps]; subj = ps; found.Add("Betreff"); }
        var headEnd = subj >= 0 ? subj : head;
        // Datum (und Ort davor, „Musterstadt, 12.03.2026“) im Kopf
        for (var i = 0; i < headEnd; i++)
            if (DateRx().Match(lines[i]) is { Success: true } m && DateTime.TryParseExact($"{m.Groups[1].Value.PadLeft(2, '0')}.{m.Groups[2].Value.PadLeft(2, '0')}.{m.Groups[3].Value}", "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                d.Date = dt; found.Add("Datum");
                var place = lines[i][..m.Index].Replace(" den", " ").Trim().TrimEnd(',').Trim();
                if (place.Length is > 1 and < 40 && !place.Any(char.IsDigit)) { d.Place = place; found.Add("Ort"); }
                headEnd = Math.Min(headEnd, i);
                break;
            }
        // Anschrift: der zusammenhängende Block vor Datum/Betreff, der eine Zeile „PLZ Ort“ enthält (höchstens 6 Zeilen)
        var zip = -1; for (var i = headEnd - 1; i >= 0; i--) if (ZipRx().IsMatch(lines[i])) { zip = i; break; }
        if (zip >= 0)
        {
            var start = zip; while (start > 0 && lines[start - 1].Length > 0 && zip - start < 5) start--;
            // Absenderzeile (eigene Anschrift in der ersten Zeile, „Name · Straße · PLZ Ort“) nicht mitnehmen
            var block = lines.Skip(start).Take(zip - start + 1).Where(l => !l.Contains(" · ") && !l.Contains(" | ")).ToList();
            if (block.Count > 0) { d.Recipient = string.Join("\n", block); found.Add("Anschrift"); }
        }
        // Anrede, Text, Gruß, Anlagen
        var bodyStart = sal >= 0 ? sal + 1 : (subj >= 0 ? subj + 1 : 0);
        if (sal >= 0) { d.Salutation = lines[sal]; found.Add("Anrede"); }
        var clo = Index(lines, l => ClosingRx().IsMatch(l), bodyStart);
        var bodyEnd = clo >= 0 ? clo : lines.Count;
        if (clo >= 0) { d.Closing = lines[clo].TrimEnd(','); found.Add("Gruß"); }
        var att = Index(lines, l => AttachRx().IsMatch(l) && l.Length < 200, Math.Max(bodyStart, clo >= 0 ? clo : bodyStart));
        if (att >= 0 && clo >= 0)
        {
            var rest = AttachRx().Replace(lines[att], "").Trim();
            var items = (rest.Length > 0 ? [rest] : new List<string>()).Concat(lines.Skip(att + 1).TakeWhile(l => l.Length > 0)).Select(l => l.TrimStart('-', '•', ' ')).Where(l => l.Length > 0);
            d.Attachments = string.Join("\n", items); if (d.Attachments.Length > 0) found.Add("Anlagen");
        }
        // Text: Absätze durch eine Leerzeile getrennt; ohne erkannte Anrede bleibt der ganze Text erhalten
        var paras = lines.Skip(bodyStart).Take(Math.Max(0, bodyEnd - bodyStart)).Where(l => l.Length > 0)
            .Select(l => l.StartsWith('•') ? "– " + l.TrimStart('•', ' ') : l).ToList(); // ein Absatz je Zeile der Vorlage, Aufzählung als Strich
        d.Body = string.Join("\n\n", paras).Trim();
        if (d.Body.Length > 0) found.Add("Text");
        if (d.Subject.Length == 0 && d.Body.Length == 0) throw new InvalidDataException("In der Datei steht kein Text.");
        return (d, found);
    }

    static int Index(IReadOnlyList<string> l, Func<string, bool> f, int from = 0) { for (var i = Math.Max(0, from); i < l.Count; i++) if (l[i].Length > 0 && f(l[i])) return i; return -1; }
    static int Prev(IReadOnlyList<string> l, int at) { for (var i = at - 1; i >= 0; i--) if (l[i].Length > 0) return i; return -1; }

    static LetterDraft Clone(LetterDraft s) => System.Text.Json.JsonSerializer.Deserialize<LetterDraft>(System.Text.Json.JsonSerializer.Serialize(s))!;
}
