using System.Globalization;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;

namespace Trendo.Core.Sys;

/// <summary>T-0223 Trendo PDF (Stufe 1, P3/P4/P5/P6): Zusammenfügen, Seiten auswählen, Teilen, Drehen, Passwort, Infos, Anmerkungen und Formulare –
/// eigener Nachbau mit PDFsharp 6.2.4 (MIT), Vorbild qpdf 12.4.2. Die Eingabe wird nie überschrieben; jedes Ergebnis ist eine neue Datei.</summary>
public static partial class PdfTools
{
    static PdfTools() => Fonts.Ensure();

    public sealed record Info(int Pages, bool Encrypted, bool NeedsPassword, string? Title, IReadOnlyList<(double Width, double Height)> PageSizes);
    public sealed record Field(string Name, string Kind, string Value);
    public enum Mark { Highlight, Underline, StrikeOut, Squiggly }

    /// <summary>Seitenangabe wie bei qpdf: „1-3,5,8-z“, „z“ = letzte Seite, „r1“ = letzte, „r2“ = vorletzte. Ergebnis 0-basiert, Reihenfolge bleibt.</summary>
    public static List<int> ParsePages(string spec, int count)
    {
        if (string.IsNullOrWhiteSpace(spec)) return Enumerable.Range(0, count).ToList();
        int Num(string s)
        {
            s = s.Trim().ToLowerInvariant();
            var n = s == "z" ? count : s.StartsWith('r') ? count + 1 - int.Parse(s[1..], CultureInfo.InvariantCulture) : int.Parse(s, CultureInfo.InvariantCulture);
            if (n < 1 || n > count) throw new ArgumentException($"Seite {s} gibt es nicht (Dokument hat {count} Seiten).");
            return n;
        }
        var list = new List<int>();
        foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!SpecPart().IsMatch(part)) throw new ArgumentException($"Seitenangabe „{part}“ verstehe ich nicht. Beispiel: 1-3,5,8-z");
            var dash = part.IndexOf('-', part.StartsWith('r') ? 1 : 0);
            if (dash < 0) { list.Add(Num(part) - 1); continue; }
            var a = Num(part[..dash]); var b = part[(dash + 1)..].Length == 0 ? count : Num(part[(dash + 1)..]);
            if (a <= b) for (var i = a; i <= b; i++) list.Add(i - 1); else for (var i = a; i >= b; i--) list.Add(i - 1);
        }
        return list;
    }

    [GeneratedRegex(@"^(z|r?\d+)(-(z|r?\d+)?)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SpecPart();

    static PdfDocument OpenImport(string path, string? password = null) =>
        password is null ? PdfReader.Open(path, PdfDocumentOpenMode.Import) : PdfReader.Open(path, password, PdfDocumentOpenMode.Import);

    static PdfDocument OpenModify(string path, string? password = null) =>
        password is null ? PdfReader.Open(path, PdfDocumentOpenMode.Modify) : PdfReader.Open(path, password, PdfDocumentOpenMode.Modify);

    /// <summary>Vorlage nach <paramref name="output"/> kopieren und dort bearbeiten; bricht die Bearbeitung ab, bleibt keine halbe Datei liegen.</summary>
    static void EditCopy(string input, string output, Action<PdfDocument> edit)
    {
        Guard(output, input);
        File.Copy(input, output, true);
        try
        {
            using var d = OpenModify(output);
            edit(d);
            Save(d, output);
        }
        catch { try { File.Delete(output); } catch (IOException) { } throw; }
    }

    /// <summary>Jede Seite braucht ein /Resources-Wörterbuch (PDF 1.7, 7.7.3.3) – leere Seiten von PDFsharp haben keins, qpdf --check warnt sonst.</summary>
    static void Save(PdfDocument d, string output)
    {
        foreach (var p in d.Pages.Cast<PdfPage>())
            if (p.Elements.GetObject("/Resources") is null && (p.Elements.GetObject("/Parent") as PdfDictionary)?.Elements.GetObject("/Resources") is null) p.Elements["/Resources"] = new PdfDictionary(d);
        d.Save(output);
    }

    static void Guard(string output, params string[] inputs)
    {
        var o = Path.GetFullPath(output);
        if (inputs.Any(i => string.Equals(Path.GetFullPath(i), o, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Das Ergebnis braucht einen neuen Dateinamen – die Vorlage wird nie überschrieben.");
        Directory.CreateDirectory(Path.GetDirectoryName(o)!);
    }

    /// <summary>Seitenzahl, Verschlüsselung, Titel und Seitengrößen (Punkte). Ohne passendes Passwort: NeedsPassword, keine Seiten.</summary>
    public static Info Read(string path, string? password = null)
    {
        try
        {
            using var d = OpenImport(path, password);
            var enc = HasEncryptDictionary(path);
            return new(d.PageCount, enc, false, string.IsNullOrWhiteSpace(d.Info.Title) ? null : d.Info.Title,
                d.Pages.Cast<PdfPage>().Select(p => (p.Width.Point, p.Height.Point)).ToList());
        }
        catch (PdfReaderException e) when (e.Message.Contains("password", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("Passwort", StringComparison.OrdinalIgnoreCase))
        { return new(0, true, true, null, []); }
    }

    /// <summary>Verschlüsselt ist eine PDF, wenn ihr Trailer bzw. Querverweis-Stream auf ein /Encrypt-Wörterbuch zeigt (beide liegen unkomprimiert vor).</summary>
    static bool HasEncryptDictionary(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return System.Text.Encoding.Latin1.GetString(bytes).Contains("/Encrypt", StringComparison.Ordinal);
    }

    /// <summary>Dateien in Reihenfolge zusammenfügen (qpdf --empty --pages a.pdf b.pdf --).</summary>
    public static int Merge(IReadOnlyList<string> inputs, string output, AuditLog? audit = null)
    {
        if (inputs.Count < 2) throw new ArgumentException("Bitte mindestens zwei PDF-Dateien wählen.");
        Guard(output, [.. inputs]);
        using var target = new PdfDocument();
        foreach (var f in inputs)
        {
            using var src = OpenImport(f);
            foreach (var p in src.Pages) target.AddPage(p);
        }
        var count = target.PageCount;
        Save(target, output);
        audit?.Append("app", "pdf.merge", $"{inputs.Count} Dateien → {Path.GetFileName(output)} ({count} Seiten)");
        return count;
    }

    /// <summary>Nur bestimmte Seiten in eine neue Datei (qpdf in.pdf --pages . 1-3,5 -- out.pdf).</summary>
    public static int SelectPages(string input, string spec, string output, AuditLog? audit = null)
    {
        Guard(output, input);
        using var src = OpenImport(input);
        var pages = ParsePages(spec, src.PageCount);
        using var target = new PdfDocument();
        foreach (var i in pages) target.AddPage(src.Pages[i]);
        var count = target.PageCount;
        Save(target, output);
        audit?.Append("app", "pdf.pages", $"{Path.GetFileName(input)} [{spec}] → {Path.GetFileName(output)}");
        return count;
    }

    /// <summary>In Dateien zu je <paramref name="every"/> Seiten teilen (qpdf --split-pages=n). Ergebnis: name-01.pdf, name-02.pdf …</summary>
    public static List<string> Split(string input, string outDir, int every = 1, AuditLog? audit = null)
    {
        if (every < 1) throw new ArgumentException("Mindestens eine Seite je Datei.");
        Directory.CreateDirectory(outDir);
        using var src = OpenImport(input);
        var stem = Path.GetFileNameWithoutExtension(input);
        var parts = (src.PageCount + every - 1) / every;
        var digits = Math.Max(2, parts.ToString(CultureInfo.InvariantCulture).Length);
        var files = new List<string>();
        for (var k = 0; k < parts; k++)
        {
            using var t = new PdfDocument();
            for (var i = k * every; i < Math.Min(src.PageCount, (k + 1) * every); i++) t.AddPage(src.Pages[i]);
            var f = Path.Combine(outDir, $"{stem}-{(k + 1).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')}.pdf");
            Guard(f, input); Save(t, f); files.Add(f);
        }
        audit?.Append("app", "pdf.split", $"{Path.GetFileName(input)} → {files.Count} Dateien");
        return files;
    }

    /// <summary>Seiten drehen (qpdf --rotate=+90:1-3). Gradzahl muss ein Vielfaches von 90 sein.</summary>
    public static void Rotate(string input, string output, int degrees, string? spec = null, AuditLog? audit = null)
    {
        if (degrees % 90 != 0) throw new ArgumentException("Drehen geht nur in Vierteln: 90, 180 oder 270 Grad.");
        EditCopy(input, output, d =>
        {
            foreach (var i in ParsePages(spec ?? "", d.PageCount)) d.Pages[i].Rotate = (((d.Pages[i].Rotate + degrees) % 360) + 360) % 360;
        });
        audit?.Append("app", "pdf.rotate", $"{Path.GetFileName(input)} {degrees}° [{spec ?? "alle"}]");
    }

    /// <summary>Mit Passwort schützen (AES-256 wie qpdf --encrypt … 256). Öffnen braucht das Nutzer-Passwort.</summary>
    public static void Encrypt(string input, string output, string userPassword, string? ownerPassword = null, string? currentPassword = null, AuditLog? audit = null)
    {
        if (string.IsNullOrEmpty(userPassword)) throw new ArgumentException("Bitte ein Passwort eingeben.");
        Guard(output, input);
        using var d = OpenModify(input, currentPassword);
        d.SecurityHandler.SetEncryptionToV5();
        d.SecuritySettings.UserPassword = userPassword;
        d.SecuritySettings.OwnerPassword = string.IsNullOrEmpty(ownerPassword) ? userPassword : ownerPassword;
        Save(d, output);
        audit?.Append("app", "pdf.encrypt", Path.GetFileName(output)); // Passwort nie ins Protokoll
    }

    /// <summary>Passwortschutz entfernen (qpdf --decrypt). Geht nur mit dem richtigen Passwort.</summary>
    public static void Decrypt(string input, string output, string password, AuditLog? audit = null)
    {
        Guard(output, input);
        using var d = OpenModify(input, password);
        d.SecurityHandler.SetEncryption(PdfDefaultEncryption.None);
        Save(d, output);
        audit?.Append("app", "pdf.decrypt", Path.GetFileName(output));
    }

    /// <summary>Leere PDF mit einer A4-Seite (Vorbild qpdf --empty).</summary>
    public static void NewBlank(string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        using var d = new PdfDocument();
        d.AddPage().Size = PdfSharp.PageSize.A4;
        Save(d, output);
    }

    // ---------- P5 Anmerkungen ----------
    static PdfArray Annots(PdfPage page)
    {
        if (page.Elements.GetObject("/Annots") is PdfArray a) return a;
        var arr = new PdfArray(page.Owner); page.Elements["/Annots"] = arr; return arr;
    }

    static PdfArray Nums(PdfDocument d, params double[] v) { var a = new PdfArray(d); foreach (var x in v) a.Elements.Add(new PdfReal(x)); return a; }

    /// <summary>Text markieren, unterstreichen, durchstreichen oder wellig unterstreichen. Rechteck in PDF-Punkten (Ursprung unten links).</summary>
    public static void AddMark(string input, string output, int pageIndex, Mark kind, double x, double y, double w, double h, string? note = null, AuditLog? audit = null)
    {
        EditCopy(input, output, d =>
        {
            var page = d.Pages[pageIndex];
            var a = new PdfDictionary(d);
            a.Elements["/Type"] = new PdfName("/Annot");
            a.Elements["/Subtype"] = new PdfName("/" + kind);
            a.Elements["/Rect"] = Nums(d, x, y, x + w, y + h);
            a.Elements["/QuadPoints"] = Nums(d, x, y + h, x + w, y + h, x, y, x + w, y);
            a.Elements["/C"] = kind == Mark.Highlight ? Nums(d, 1, 0.92, 0.23) : Nums(d, 0.86, 0.15, 0.15);
            a.Elements["/CA"] = new PdfReal(kind == Mark.Highlight ? 0.5 : 1);
            a.Elements["/F"] = new PdfInteger(4); // drucken
            a.Elements["/T"] = new PdfString("Trendo");
            a.Elements["/M"] = new PdfString(DateTime.Now.ToString("'D:'yyyyMMddHHmmss", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(note)) a.Elements["/Contents"] = new PdfString(note, PdfStringEncoding.Unicode);
            d.Internals.AddObject(a);
            Annots(page).Elements.Add(a.Reference!);
        });
        audit?.Append("app", "pdf.annot", $"{kind} S.{pageIndex + 1} {Path.GetFileName(output)}");
    }

    /// <summary>Notiz als Freitext auf die Seite (Vorbild SumatraPDF „Freitext“).</summary>
    public static void AddNote(string input, string output, int pageIndex, double x, double y, string text, AuditLog? audit = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Bitte einen Text für die Notiz eingeben.");
        EditCopy(input, output, d =>
        {
            var page = d.Pages[pageIndex];
            var lines = text.Split('\n').Length; var width = Math.Min(260, 12 + text.Split('\n').Max(l => l.Length) * 6.2);
            var a = new PdfDictionary(d);
            a.Elements["/Type"] = new PdfName("/Annot");
            a.Elements["/Subtype"] = new PdfName("/FreeText");
            a.Elements["/Rect"] = Nums(d, x, y - lines * 14 - 6, x + width, y);
            a.Elements["/Contents"] = new PdfString(text, PdfStringEncoding.Unicode);
            a.Elements["/DA"] = new PdfString("/Helv 11 Tf 0 0 0.55 rg");
            a.Elements["/C"] = Nums(d, 1, 0.98, 0.8);
            a.Elements["/F"] = new PdfInteger(4);
            a.Elements["/T"] = new PdfString("Trendo");
            d.Internals.AddObject(a);
            Annots(page).Elements.Add(a.Reference!);
        });
        audit?.Append("app", "pdf.note", $"S.{pageIndex + 1} {Path.GetFileName(output)}");
    }

    /// <summary>Anmerkungen einer Seite: Art und Text (zum Prüfen und Anzeigen).</summary>
    public static List<(string Kind, string? Text)> Annotations(string path, int pageIndex)
    {
        using var d = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        if (d.Pages[pageIndex].Elements.GetObject("/Annots") is not PdfArray arr) return [];
        return arr.Elements.Select(e => (e as PdfReference)?.Value as PdfDictionary ?? e as PdfDictionary).Where(x => x is not null)
            .Select(x => (x!.Elements.GetName("/Subtype").TrimStart('/'), x.Elements.GetString("/Contents") is { Length: > 0 } s ? s : null)).ToList();
    }

    // ---------- P6 Formulare ----------
    /// <summary>Formularfelder: Name, Art (Text, Ankreuzfeld, Auswahl) und aktueller Wert.</summary>
    public static List<Field> Fields(string path)
    {
        using var d = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        var list = new List<Field>();
        if (d.AcroForm is null) return list;
        void Walk(PdfAcroField.PdfAcroFieldCollection fields)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                if (f.HasKids) { Walk(f.Fields); continue; }
                list.Add(f switch
                {
                    PdfTextField t => new Field(f.Name, "Text", t.Text ?? ""),
                    PdfCheckBoxField c => new Field(f.Name, "Ankreuzfeld", c.Checked ? "ja" : "nein"),
                    PdfComboBoxField or PdfListBoxField => new Field(f.Name, "Auswahl", f.Value?.ToString()?.Trim('(', ')') ?? ""),
                    _ => new Field(f.Name, f.GetType().Name.Replace("Pdf", "").Replace("Field", ""), f.Value?.ToString() ?? ""),
                });
            }
        }
        Walk(d.AcroForm.Fields);
        return list;
    }

    /// <summary>Formular ausfüllen und als neue Datei speichern. Ankreuzfelder: „ja“/„nein“. Unbekannte Feldnamen werden gemeldet.</summary>
    public static List<string> Fill(string input, string output, IReadOnlyDictionary<string, string> values, AuditLog? audit = null)
    {
        var unknown = new List<string>(values.Keys);
        EditCopy(input, output, d =>
        {
            if (d.AcroForm is null) throw new InvalidOperationException("Diese PDF hat kein Formular.");
            void Walk(PdfAcroField.PdfAcroFieldCollection fields)
            {
                for (var i = 0; i < fields.Count; i++)
                {
                    var f = fields[i];
                    if (f.HasKids) { Walk(f.Fields); continue; }
                    if (!values.TryGetValue(f.Name, out var v)) continue;
                    unknown.Remove(f.Name);
                    if (f.ReadOnly) continue;
                    switch (f)
                    {
                        case PdfTextField t: t.Text = v; break;
                        case PdfCheckBoxField c: c.Checked = v.Trim().ToLowerInvariant() is "ja" or "x" or "1" or "true"; break;
                        default: f.Value = new PdfString(v); break;
                    }
                }
            }
            Walk(d.AcroForm.Fields);
            d.AcroForm.Elements["/NeedAppearances"] = new PdfBoolean(true); // Anzeigeprogramme zeichnen die neuen Werte
        });
        audit?.Append("app", "pdf.fill", $"{values.Count - unknown.Count} Felder → {Path.GetFileName(output)}");
        return unknown;
    }
}
