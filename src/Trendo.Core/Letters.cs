using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>STUDIO-LETTER-01: Ein Brief – Felder nach DIN 5008 (Form B).</summary>
public sealed class LetterDraft
{
    public string Category { get; set; } = "frei";
    public string SenderName { get; set; } = "";
    public string SenderStreet { get; set; } = "";
    public string SenderCity { get; set; } = "";
    public string SenderContact { get; set; } = "";
    public string Recipient { get; set; } = "";          // mehrzeilig: Firma, Person, Straße, PLZ Ort
    public string Place { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public string Reference { get; set; } = "";          // z. B. Kundennummer, Aktenzeichen
    public string Subject { get; set; } = "";
    public string Salutation { get; set; } = "Sehr geehrte Damen und Herren,";
    public string Body { get; set; } = "";
    public string Closing { get; set; } = "Mit freundlichen Grüßen";
    public string Attachments { get; set; } = "";
}

public sealed record LetterCategory(string Id, string Label, string Subject, string Body, bool Legal = false);
public sealed record LetterIssue(string Field, string Message, bool Blocking);

public static class Letters
{
    /// <summary>LETTER-002/004: Briefarten mit Vorlagen. Rechtlich heikle Arten tragen einen Hinweis – Trendo ersetzt keine Rechtsberatung.</summary>
    public static readonly LetterCategory[] Categories =
    [
        new("frei", "Freier Brief", "", ""),
        new("kuendigung", "Kündigung", "Kündigung meines Vertrags [Vertragsnummer]",
            "hiermit kündige ich den oben genannten Vertrag fristgerecht zum nächstmöglichen Zeitpunkt.\n\nBitte bestätigen Sie mir die Kündigung und das Vertragsende schriftlich.", Legal: true),
        new("widerspruch", "Widerspruch", "Widerspruch gegen Ihren Bescheid vom [Datum], Aktenzeichen [Az.]",
            "gegen den oben genannten Bescheid lege ich hiermit fristgerecht Widerspruch ein.\n\nBegründung:\n[Warum ist der Bescheid falsch?]\n\nBitte bestätigen Sie mir den Eingang dieses Widerspruchs.", Legal: true),
        new("mahnung", "Zahlungserinnerung", "Zahlungserinnerung zur Rechnung [Nummer] vom [Datum]",
            "sicher haben Sie übersehen, dass die oben genannte Rechnung über [Betrag] € noch offen ist.\n\nBitte überweisen Sie den Betrag bis zum [Datum] auf das bekannte Konto."),
        new("anfrage", "Anfrage / Angebot", "Anfrage: [Thema]",
            "ich interessiere mich für [Produkt/Leistung] und bitte Sie um ein unverbindliches Angebot.\n\nFolgende Punkte sind mir wichtig:\n[…]"),
        new("beschwerde", "Beschwerde / Reklamation", "Reklamation: [Produkt], Bestellung [Nummer]",
            "am [Datum] habe ich bei Ihnen [Produkt] gekauft. Leider [Mangel beschreiben].\n\nIch bitte Sie, den Mangel bis zum [Datum] zu beheben oder mir Ersatz zu liefern.", Legal: true),
        new("bewerbung", "Bewerbung", "Bewerbung als [Stelle]",
            "mit großem Interesse habe ich Ihre Stellenanzeige gelesen und bewerbe mich hiermit als [Stelle].\n\n[Warum passe ich gut?]\n\nÜber eine Einladung zu einem persönlichen Gespräch freue ich mich."),
    ];

    public static LetterCategory Category(string id) => Categories.FirstOrDefault(c => c.Id == id) ?? Categories[0];

    public static void ApplyTemplate(LetterDraft d, string categoryId)
    {
        var c = Category(categoryId); d.Category = c.Id;
        if (c.Subject.Length > 0) d.Subject = c.Subject;
        if (c.Body.Length > 0) d.Body = c.Body;
    }

    private static readonly Regex Placeholder = new(@"\[[^\]\n]{1,40}\]");
    private static readonly Regex Plz = new(@"(^|\n)\s*(D-)?\d{5}\s+\S");

    /// <summary>LETTER-018: Fehlende Angaben und Unstimmigkeiten. Blockierend = so sollte der Brief nicht raus.</summary>
    public static IReadOnlyList<LetterIssue> Validate(LetterDraft d)
    {
        var l = new List<LetterIssue>();
        void Need(string v, string f, string m) { if (string.IsNullOrWhiteSpace(v)) l.Add(new(f, m, true)); }
        Need(d.SenderName, "Absender", "Dein Name fehlt.");
        Need(d.SenderStreet, "Absender", "Deine Straße fehlt.");
        Need(d.SenderCity, "Absender", "Deine PLZ und dein Ort fehlen.");
        Need(d.Recipient, "Empfänger", "Der Empfänger fehlt.");
        Need(d.Subject, "Betreff", "Der Betreff fehlt.");
        Need(d.Body, "Text", "Der Brieftext fehlt.");
        var rec = d.Recipient.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (rec.Length is > 0 and < 3) l.Add(new("Empfänger", "Die Anschrift wirkt unvollständig (Name, Straße, PLZ Ort).", true));
        if (rec.Length > 6) l.Add(new("Empfänger", "Das Anschriftfeld hat nach DIN 5008 höchstens 6 Zeilen.", true));
        if (rec.Length >= 3 && !Plz.IsMatch(d.Recipient)) l.Add(new("Empfänger", "In der Anschrift fehlt eine fünfstellige Postleitzahl (bei Auslandsbriefen ignorieren).", false));
        if (d.SenderCity.Length > 0 && !Regex.IsMatch(d.SenderCity, @"\d{4,5}")) l.Add(new("Absender", "Bei deinem Ort fehlt die Postleitzahl.", false));
        foreach (var (f, v) in new[] { ("Betreff", d.Subject), ("Text", d.Body), ("Empfänger", d.Recipient) })
            foreach (Match m in Placeholder.Matches(v)) l.Add(new(f, $"Platzhalter {m.Value} noch ausfüllen.", true));
        if (d.Subject.Length > 120) l.Add(new("Betreff", "Der Betreff ist sehr lang – kürzer ist besser lesbar.", false));
        if (d.Date.Date > DateTime.Today.AddDays(60) || d.Date.Date < DateTime.Today.AddYears(-1)) l.Add(new("Datum", "Das Datum wirkt ungewöhnlich.", false));
        var sal = d.Salutation.Trim();
        if (sal.Length == 0) l.Add(new("Anrede", "Die Anrede fehlt.", false));
        else if (sal.EndsWith(',') && d.Body.TrimStart().Length > 0 && char.IsUpper(d.Body.TrimStart()[0]) && !d.Body.TrimStart().StartsWith("Ich") )
            l.Add(new("Text", "Nach der Anrede mit Komma beginnt der Text klein (außer Namen und Nomen).", false));
        if (d.Closing.TrimEnd().EndsWith(',')) l.Add(new("Gruß", "Nach dem Gruß steht kein Komma (DIN 5008).", false));
        return l;
    }

    public static string FileStem(LetterDraft d)
    {
        var s = Regex.Replace(Placeholder.Replace(d.Subject, ""), @"[^\p{L}\p{N}]+", "-").Trim('-');
        if (s.Length > 50) s = s[..50].TrimEnd('-');
        return $"{d.Date:yyyy-MM-dd}-{(s.Length == 0 ? "brief" : s)}";
    }

    // ---------- LETTER-003/016: DOCX nach DIN 5008 Form B (Ränder 25/20 mm, Anschriftfeld ab 45 mm, Arial 11) ----------
    public static void WriteDocx(LetterDraft d, string path)
    {
        var body = new StringBuilder();
        string Run(string t, bool bold = false, int size = 22) =>
            $"<w:r><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\" w:cs=\"Arial\"/>{(bold ? "<w:b/>" : "")}<w:sz w:val=\"{size}\"/></w:rPr><w:t xml:space=\"preserve\">{SecurityElement.Escape(t)}</w:t></w:r>";
        void Para(string t, bool bold = false, int size = 22, int before = 0, int after = 0, string align = "left") =>
            body.Append($"<w:p><w:pPr><w:spacing w:before=\"{before}\" w:after=\"{after}\" w:line=\"264\" w:lineRule=\"auto\"/><w:jc w:val=\"{align}\"/></w:pPr>{Run(t, bold, size)}</w:p>");
        void Lines(string t, int after = 0) { foreach (var line in t.Replace("\r", "").Split('\n')) Para(line, after: after); }

        // Rücksendeangabe (klein) im Anschriftfeld; Seite beginnt bei 45 mm → 1 Zeile darüber
        Para($"{d.SenderName} · {d.SenderStreet} · {d.SenderCity}", size: 16, before: 1134);
        Lines(d.Recipient);
        // Informationsblock rechtsbündig: Datum (und Zeichen)
        var info = (d.Reference.Length > 0 ? $"Ihr Zeichen/Nummer: {d.Reference}\n" : "") + $"{(d.Place.Length > 0 ? d.Place + ", " : "")}{d.Date.ToString("d. MMMM yyyy", new System.Globalization.CultureInfo("de-DE"))}";
        var first = true;
        foreach (var line in info.Split('\n')) { Para(line, before: first ? 480 : 0, align: "right"); first = false; }
        Para(d.Subject, bold: true, before: 480, after: 240);
        Para(d.Salutation, after: 240);
        foreach (var block in d.Body.Replace("\r", "").Split("\n\n")) { Lines(block); Para("", after: 0); }
        Para(d.Closing, before: 0);
        Para(d.SenderName, before: 720);
        if (d.Attachments.Trim().Length > 0) { Para("Anlagen", bold: true, before: 360); Lines(d.Attachments.Trim()); }
        if (d.SenderContact.Trim().Length > 0) Para(d.SenderContact.Trim(), size: 16, before: 480);

        var doc = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" + body +
                  "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1134\" w:right=\"1134\" w:bottom=\"1134\" w:left=\"1418\" w:header=\"709\" w:footer=\"709\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            void Add(string n, string c) { using var w = new StreamWriter(zip.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(c); }
            Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Add("word/document.xml", doc);
        }
        File.Move(tmp, path, true);
    }

    /// <summary>Reiner Text der DOCX (für Vorschau/Tests).</summary>
    public static string ReadDocxText(string path)
    {
        using var z = ZipFile.OpenRead(path);
        using var r = new StreamReader(z.GetEntry("word/document.xml")!.Open());
        var x = r.ReadToEnd();
        return System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(x, "</w:p>", "\n"), "<[^>]+>", ""));
    }
}

/// <summary>LETTER-022: Briefarchiv im Projektordner\Briefe – jede Speicherung ist eine neue Version, nichts wird überschrieben.</summary>
public sealed class LetterArchive(string projectsFolder)
{
    public string Dir => Path.Combine(projectsFolder, "Briefe");
    private static readonly JsonSerializerOptions J = new() { WriteIndented = true };

    public (string Docx, string Json, int Version) Save(LetterDraft d)
    {
        Directory.CreateDirectory(Dir);
        var stem = Letters.FileStem(d); var v = 1;
        while (File.Exists(Path.Combine(Dir, Name(stem, v) + ".docx"))) v++;
        var docx = Path.Combine(Dir, Name(stem, v) + ".docx"); var json = Path.ChangeExtension(docx, ".trendo-brief.json");
        Letters.WriteDocx(d, docx);
        File.WriteAllText(json, JsonSerializer.Serialize(d, J));
        return (docx, json, v);
    }
    private static string Name(string stem, int v) => v == 1 ? stem : $"{stem}-v{v}";

    public IReadOnlyList<string> List() => Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.docx").OrderByDescending(File.GetLastWriteTimeUtc).ToList() : [];
    public static LetterDraft? Load(string docx)
    {
        var j = Path.ChangeExtension(docx, ".trendo-brief.json");
        try { return File.Exists(j) ? JsonSerializer.Deserialize<LetterDraft>(File.ReadAllText(j)) : null; } catch (JsonException) { return null; }
    }
}

/// <summary>SYS-10/LETTER-016: PDF über LibreOffice im Hintergrund (ohne Fenster), mit Zeitlimit.</summary>
public static class PdfExport
{
    public static Task<(bool Ok, string Message, string? Pdf)> ConvertAsync(IProcessRunner run, string soffice, string input, CancellationToken ct = default) => ConvertToAsync(run, soffice, input, "pdf", ct);

    /// <summary>LETTER-016: pdf oder odt. Ergebnis mit SHA-256 im Protokoll.</summary>
    public static async Task<(bool Ok, string Message, string? Pdf)> ConvertToAsync(IProcessRunner run, string soffice, string input, string format, CancellationToken ct = default)
    {
        if (format is not ("pdf" or "odt" or "docx")) throw new ArgumentException("Format");
        var outDir = Path.GetDirectoryName(Path.GetFullPath(input))!;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            var (exit, output) = await run.RunAsync(soffice, $"--headless --norestore --convert-to {format} --outdir \"{outDir}\" \"{input}\"", null, cts.Token);
            var pdf = Path.ChangeExtension(input, "." + format);
            return exit == 0 && File.Exists(pdf) ? (true, $"{format.ToUpperInvariant()} erstellt (SHA-256 {LetterKit.Sha(pdf)[..16]}…)", pdf) : (false, $"LibreOffice meldet Fehler (Exit {exit})", null);
        }
        catch (OperationCanceledException) { return (false, "LibreOffice hat zu lange gebraucht und wurde beendet", null); }
    }
}

/// <summary>LETTER-012: Tesseract (deu) mit TSV-Ausgabe – Unsicherheiten werden markiert.</summary>
public static class Ocr
{
    public static readonly string[] ImageExt = [".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".webp"];
    /// <summary>Deutsches Sprachpaket (tessdata_fast, Apache-2.0) – pro Benutzer, ohne Admin, mit festem SHA-256.</summary>
    public const string GermanUrl = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/deu.traineddata";
    public const string GermanSha256 = "19D219BBB6672C869D20A9636C6816A81EB9A71796CB93EBE0CB1530E2CDB22D";
    public static string UserTessdata(string dataRoot) => Path.Combine(dataRoot, "tessdata");
    public static bool HasUserGerman(string dataRoot) => File.Exists(Path.Combine(UserTessdata(dataRoot), "deu.traineddata"));

    public static async Task<IReadOnlyList<string>> LanguagesAsync(IProcessRunner run, string tesseract, string? tessdata = null)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var (_, o) = await run.RunAsync(tesseract, (tessdata is null ? "" : $"--tessdata-dir \"{tessdata}\" ") + "--list-langs", null, cts.Token);
            return o.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(l => !l.Contains(' ') && !l.Contains(':')).ToList();
        }
        catch (OperationCanceledException) { return []; }
    }

    /// <summary>Lädt deu.traineddata in den Benutzerordner; falscher Hash → Datei wird verworfen.</summary>
    public static async Task<(bool Ok, string Message)> DownloadGermanAsync(Func<string, Task<Stream>> fetch, string dataRoot, AuditLog audit, Principal by, string url = GermanUrl, string sha = GermanSha256)
    {
        var dir = UserTessdata(dataRoot); Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, "deu.traineddata.part");
        try
        {
            await using (var s = await fetch(url)) await using (var f = File.Create(tmp)) await s.CopyToAsync(f);
            var got = LetterKit.Sha(tmp);
            if (got != sha) { File.Delete(tmp); audit.Append(by.Name, "ocr.lang.rejected", got); return (false, "Download verworfen: Prüfsumme stimmt nicht."); }
            File.Move(tmp, Path.Combine(dir, "deu.traineddata"), true);
            audit.Append(by.Name, "ocr.lang.installed", $"deu sha={got[..16]}");
            return (true, "Deutsches Sprachpaket installiert (1,5 MB, Apache-2.0).");
        }
        catch (Exception e) when (e is not OutOfMemoryException) { try { File.Delete(tmp); } catch { } return (false, "Download fehlgeschlagen: " + e.Message); }
    }

    public static async Task<(bool Ok, string Text, double Confidence, List<string> Unreadable, string Message)> RunAsync(IProcessRunner run, string tesseract, string image, CancellationToken ct = default, string? tessdata = null, string lang = "deu")
    {
        if (!ImageExt.Contains(Path.GetExtension(image).ToLowerInvariant())) return (false, "", 0, [], "Bitte ein Foto oder einen Scan als Bild (PNG/JPG/TIFF) wählen. PDF-Scans: Seite als Bild speichern.");
        var outBase = Path.Combine(Path.GetTempPath(), "trendo-ocr-" + Guid.NewGuid().ToString("N"));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(120));
        try
        {
            var (exit, output) = await run.RunAsync(tesseract, (tessdata is null ? "" : $"--tessdata-dir \"{tessdata}\" ") + $"\"{image}\" \"{outBase}\" -l {lang} -c tessedit_create_tsv=1 -c tessedit_create_txt=0", null, cts.Token);
            if (exit != 0 || !File.Exists(outBase + ".tsv")) return (false, "", 0, [], $"Texterkennung fehlgeschlagen (Exit {exit}). Ist das deutsche Sprachpaket installiert?");
            var (text, conf, bad) = LetterKit.ParseTesseractTsv(File.ReadAllText(outBase + ".tsv"));
            return (true, text, conf, bad, $"Erkannt ({lang}) mit durchschnittlich {conf:0} % Sicherheit, {bad.Count} unsichere Wörter markiert.");
        }
        catch (OperationCanceledException) { return (false, "", 0, [], "Texterkennung hat zu lange gebraucht."); }
        finally { foreach (var f in new[] { outBase + ".tsv" }) try { File.Delete(f); } catch { } }
    }
}

/// <summary>LETTER-020: Korrekturlesen – sichtbare Prüfhinweise, nichts wird still geändert.</summary>
public static class Proofread
{
    public static IReadOnlyList<string> Check(LetterDraft d)
    {
        var l = new List<string>(); var t = d.Body;
        foreach (Match m in Regex.Matches(t, @"\b(\w+)\s+\1\b", RegexOptions.IgnoreCase)) l.Add($"Doppeltes Wort: „{m.Value}“");
        if (Regex.IsMatch(t, @"[^\S\n]{2,}")) l.Add("Doppelte Leerzeichen im Text.");
        foreach (var s in Regex.Split(t, @"(?<=[.!?])\s+")) if (s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 35) l.Add($"Sehr langer Satz ({s.Split(' ').Length} Wörter): „{s[..Math.Min(50, s.Length)]}…“");
        if (Regex.IsMatch(t, @" [,.;:!?]")) l.Add("Leerzeichen vor Satzzeichen.");
        if (Regex.IsMatch(t, @"!{2,}|\?{2,}")) l.Add("Mehrfache Ausrufe-/Fragezeichen wirken unsachlich.");
        if (Regex.IsMatch(t, @"\b[A-ZÄÖÜ]{6,}\b")) l.Add("Wörter in GROSSBUCHSTABEN wirken wie Schreien.");
        foreach (var (wrong, right) in new[] { ("seid langem", "seit langem"), ("wiederrum", "wiederum"), ("Standart", "Standard"), ("vorraus", "voraus"), ("nähmlich", "nämlich"), ("Addresse", "Adresse"), ("tod langweilig", "todlangweilig"), ("Rechtsanwält ", "Rechtsanwalt "), ("bezüglich Ihres", "bezüglich Ihres") }.Where(x => x.Item1 != x.Item2))
            if (t.Contains(wrong, StringComparison.OrdinalIgnoreCase)) l.Add($"Rechtschreibung: „{wrong}“ → „{right}“?");
        if (d.SenderName.Trim().Length == 0) l.Add("Unterschriftsbereich: Name fehlt.");
        return l;
    }
}
