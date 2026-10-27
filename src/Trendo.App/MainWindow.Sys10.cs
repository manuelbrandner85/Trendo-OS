using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>SYS-10 Office & Dokumente (freigegebener Katalog, 21.10.2026): PDF, Schreiben, Ordnen.</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> DokuTabs = [];
    private bool _printAsk; private string _printFile = "";
    private List<PdfForm.Field> _formFields = []; private string _formFile = "";
    public TaskList Tasks => new(_core.DataRoot);

    private void InitSys10()
    {
        RegisterTab(DokuTabs, "doku-start", "Start", "SYS-10", DokuStartView, "dokumente", "vorlagen", "zuletzt", "office", "texte und tabellen");
        RegisterTab(DokuTabs, "doku-pdf", "PDF", "SYS-10", DokuPdfView, "pdf zusammenfügen", "pdf teilen", "seiten", "scan", "ocr", "texterkennung", "metadaten", "formular", "drucken", "barrierefrei");
        RegisterTab(DokuTabs, "doku-schreiben", "Schreiben", "SYS-10", DokuWriteView, "neues dokument", "vorlage", "serienbrief", "rechtschreibung", "wörter zählen", "lesbarkeit", "vergleichen", "unterschrift", "export");
        RegisterTab(DokuTabs, "doku-ordnen", "Ordnen", "SYS-10", DokuOrderView, "tabelle", "csv", "xlsx", "protokoll", "besprechung", "aufgaben", "to-do", "suche in dokumenten", "volltext");
    }
    private Control DokuPage(string title, Control body) => W45Page(title, DokuTabs, "doku", body);
    private string? Tool(string name) => ToolPath(name) ?? Cli.Find(name);

    /// <summary>T-0211: Seitenbilder der ersten (höchstens 4) Seiten für die Druckvorschau – mit Poppler (pdftoppm), sonst ein klarer Hinweis.</summary>
    private Control PageImages(string pdf)
    {
        var row = new WrapPanel { Name = "print-pages" };
        if (Tool("pdftoppm") is not { } pp) { row.Children.Add(P("Seitenbilder brauchen Poppler (pdftoppm) – die Vorschau öffnet sich im PDF-Programm.", null, 11)); return row; }
        var dir = Path.Combine(WorkFolder("druckvorschau"), Path.GetFileNameWithoutExtension(pdf));
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "s-*.png")) File.Delete(old);
        var r = Cli.Run(pp, ["-r", "40", "-png", "-f", "1", "-l", "4", pdf, Path.Combine(dir, "s")], timeoutMs: 60000);
        var pages = Directory.GetFiles(dir, "s-*.png").OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (!r.Ok || pages.Count == 0) { row.Children.Add(P("Seitenbilder konnten nicht erzeugt werden: " + (r.Err.Length > 0 ? r.Err : "unbekannter Fehler"), null, 11)); return row; }
        var i = 0;
        foreach (var png in pages)
        {
            using var s = File.OpenRead(png);
            row.Children.Add(new Border { Name = $"print-page-{i++}", Margin = new Thickness(0, 6, 8, 0), BorderBrush = Edge, BorderThickness = new Thickness(1),
                Child = new Image { Source = new Avalonia.Media.Imaging.Bitmap(s), Height = 180 } });
        }
        return row;
    }
    private string DokuOut => WorkFolder("Dokumente");

    // ---------- PDF ----------
    private Control DokuPdfView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "doku-pdf";
        string? qpdf = Tool("qpdf"), unite = Tool("pdfunite"), sep = Tool("pdfseparate");

        var mi = new TextBox { Name = "pdf-merge-files", PlaceholderText = "PDF-Dateien, eine pro Zeile (in dieser Reihenfolge)", AcceptsReturn = true, Width = 520, MinHeight = 60 };
        var si = Field("pdf-split-file", "PDF-Datei"); var sr = Field("pdf-split-range", "Seiten, z. B. 2-5", 120);
        sp.Children.Add(Card(Section("Zusammenfügen und Teilen", qpdf is null && unite is null ? "Dafür fehlt das kostenlose Programm qpdf (Einstellungen → Zusatzprogramme → qpdf, ohne Admin). Seitenzahlen kann Trendo trotzdem anzeigen." : "Die Originale bleiben unverändert; das Ergebnis liegt im Projektordner unter „Dokumente“.",
            mi, Line(Act("pdf-merge", "Zusammenfügen", () => Try(V, () =>
            {
                var files = (mi.Text ?? "").Split('\n').Select(l => l.Trim().Trim('"')).Where(l => l.Length > 0).ToList();
                if (files.Count < 2) throw new ArgumentException("Bitte mindestens zwei PDF-Dateien.");
                var o = Path.Combine(DokuOut, $"zusammengefuegt-{DateTime.Now:yyyyMMdd-HHmmss}.pdf"); var (exe, args) = Pdf.MergeCmd(qpdf, unite, files, o);
                var r = Cli.Run(exe, args, timeoutMs: 120000); if (!File.Exists(o)) throw new InvalidOperationException("Zusammenfügen hat nicht geklappt.");
                Say($"Fertig: {Path.GetFileName(o)} mit {Pdf.Pages(o)} Seiten (Summe der Einzelteile: {files.Sum(Pdf.Pages)}).", V);
            }), true)),
            Line(si, sr, Act("pdf-split", "Seiten herausholen", () => Try(V, () =>
            {
                var (a, b) = Pdf.ParseRange(Clean(sr)); var f = Clean(si); var n = Pdf.Pages(f); if (b > n) throw new ArgumentException($"Das PDF hat nur {n} Seiten.");
                var o = Path.Combine(DokuOut, $"{Path.GetFileNameWithoutExtension(f)}-seiten-{a}-{b}.pdf"); var (exe, args) = Pdf.ExtractCmd(qpdf, sep, f, a, b, o);
                Cli.Run(exe, args, timeoutMs: 120000);
                Say(File.Exists(o) ? $"Fertig: {Path.GetFileName(o)} ({Pdf.Pages(o)} Seiten)." : $"Fertig: einzelne Seiten als {Path.GetFileNameWithoutExtension(o)}-N.pdf.", V);
            }))))));

        var tf = Field("pdf-text-file", "PDF oder Bild (Scan)"); var tout = new SelectableTextBlock { Name = "pdf-text", Foreground = Text, TextWrapping = TextWrapping.Wrap, MaxHeight = 220 };
        string? tess = Tool("tesseract"); var lang = Ocr.HasUserGerman(_core.DataRoot) ? "deu" : "deu";
        sp.Children.Add(Card(Section("Text herausholen und Scans durchsuchbar machen", "Holt den Text aus einem PDF (bei Scans per Texterkennung). Aus einem Foto oder Scan wird ein PDF, in dem man suchen kann.",
            Line(tf, Act("pdf-text-run", "Text holen", () => Try(V, () =>
            {
                var (ok, t, how) = PdfText.Extract(Clean(tf), Tool("pdftotext"), Tool("pdftoppm"), tess, lang, Ocr.HasUserGerman(_core.DataRoot) ? Ocr.UserTessdata(_core.DataRoot) : null);
                if (!ok) throw new InvalidOperationException(how);
                var o = Path.Combine(DokuOut, Path.GetFileNameWithoutExtension(Clean(tf)) + ".txt"); File.WriteAllText(o, t); tout.Text = t.Length > 3000 ? t[..3000] + " …" : t;
                Say($"{how} – gespeichert als {Path.GetFileName(o)}.", V);
            }), true), Act("pdf-scan", "Scan → durchsuchbares PDF", () => Try(V, () =>
            {
                if (tess is null) throw new InvalidOperationException("Dafür fehlt Tesseract (unter „Zusatzprogramme“ kostenlos).");
                var ob = Path.Combine(DokuOut, Path.GetFileNameWithoutExtension(Clean(tf)) + "-durchsuchbar");
                var r = Cli.Run(tess, PdfText.ScanToPdfArgs(Clean(tf), ob, lang, Ocr.HasUserGerman(_core.DataRoot) ? Ocr.UserTessdata(_core.DataRoot) : null), timeoutMs: 180000);
                if (!File.Exists(ob + ".pdf")) throw new InvalidOperationException("Texterkennung hat nicht geklappt. Ist das deutsche Sprachpaket installiert?");
                Say("Fertig: " + Path.GetFileName(ob + ".pdf"), V);
            }))), tout)));

        var mf = Field("meta-file", "PDF, DOCX, XLSX oder PPTX"); var ao = new StackPanel { Name = "a11y-result" };
        sp.Children.Add(Card(Section("Persönliche Angaben entfernen und Barrierefreiheit prüfen", "Vor dem Weitergeben: Autor, Firma und Programmname aus einer Kopie entfernen. Die Prüfung zeigt, was Bildschirmleser brauchen.",
            Line(mf, Act("meta-strip", "Angaben entfernen", () => Try(V, () =>
            {
                var f = Clean(mf); var o = Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? Pdf.StripMeta(f) : Ooxml.StripOfficeMeta(f);
                Say("Kopie ohne Angaben: " + Path.GetFileName(o), V);
            }), true), Act("a11y-check", "Barrierefreiheit prüfen", () => Try(V, () =>
            {
                ao.Children.Clear(); var l = Pdf.AccessibilityIssues(Clean(mf));
                ao.Children.Add(l.Count == 0 ? P("Keine Mängel gefunden.", Text) : P("Mängel:\n• " + string.Join("\n• ", l), Warn));
                File.WriteAllText(Path.Combine(DokuOut, Path.GetFileNameWithoutExtension(Clean(mf)) + "-barrierefreiheit.txt"), string.Join("\n", l.DefaultIfEmpty("Keine Mängel gefunden.")));
            }))), ao)));

        var ff = Field("form-file", "PDF mit Formularfeldern", 340, _formFile); var fields = new StackPanel { Spacing = 4, Name = "form-fields" }; var boxes = new Dictionary<string, TextBox>();
        foreach (var f in _formFields) { var t = new TextBox { Name = "form-field-" + boxes.Count, Text = f.Value, Width = 260, PlaceholderText = f.Type == "Btn" ? "ja / nein" : "" }; boxes[f.Name] = t; fields.Children.Add(Line(P(f.Name, Text), t)); }
        sp.Children.Add(Card(Section("PDF-Formular ausfüllen", "Liest die Felder eines PDF-Formulars. Ausgefüllt wird eine Kopie – das Original bleibt.",
            Line(ff, Act("form-read", "Felder lesen", () => Try(V, () => { _formFile = Need(ff, "die PDF-Datei mit dem Formular"); _formFields = PdfForm.Fields(_formFile); Say(_formFields.Count == 0 ? "Keine ausfüllbaren Felder gefunden." : $"{_formFields.Count} Felder gefunden.", V); })),
                Act("form-save", "Ausgefüllt speichern", () => Try(V, () => { if (string.IsNullOrEmpty(_formFile) || _formFields.Count == 0) { Say("Erst „Felder lesen“ – dann ausfüllen und speichern.", V); return; } var o = PdfForm.Fill(_formFile, boxes.ToDictionary(b => b.Key, b => b.Value.Text ?? "")); _formFields = []; Say("Gespeichert und geöffnet: " + Path.GetFileName(o), V); Launch(o); }), true)), fields)));

        var pf = Field("print-file", "PDF zum Drucken", 340, _printFile); var info = new StackPanel { Name = "print-info" };
        var sumatra = ToolPath("sumatra");
        sp.Children.Add(Card(Section("Druckvorschau und Drucken", "Zeigt Seitenzahl und Format (A4?) und öffnet die Vorschau. Gedruckt wird nur nach deinem ausdrücklichen Klick.",
            Line(pf, Act("print-preview", "Vorschau", () => Try(V, () =>
            {
                var f = Need(pf, "die PDF-Datei für die Vorschau"); if (!File.Exists(f)) { Say("Datei nicht gefunden: " + Path.GetFileName(f), V); return; } var b = Pdf.MediaBox(f); _printFile = f; info.Children.Clear();
                info.Children.Add(P($"{Pdf.Pages(f)} Seiten · {(b is { } x ? $"{x.W / 72 * 25.4:0} × {x.H / 72 * 25.4:0} mm" : "Format unbekannt")} · {(Pdf.IsA4(b) ? "A4 ✓" : "nicht A4")}", Text));
                info.Children.Add(PageImages(f));
                if (sumatra is not null) _core.Integrations.LaunchDirect(Principal.LocalOwner, sumatra, _core.Audit, f); else Launch(f);
            })), _printAsk ? Line(P("Jetzt auf dem Standarddrucker drucken?", Text), Act("print-yes", "Ja, drucken", () => Try(V, () => { PrintNow(_printFile, sumatra); _printAsk = false; Say("An den Drucker geschickt.", V); }), true), Act("print-no", "Abbrechen", () => { _printAsk = false; Show(V); }))
                            : Act("print-ask", "Drucken …", () => { _printFile = Clean(pf); if (File.Exists(_printFile)) { _printAsk = true; Show(V); } else Say("Datei nicht gefunden.", V); })), info)));
        return DokuPage("PDF", sp);
    }

    private void PrintNow(string file, string? sumatra)
    {
        _core.Audit.Append("app", "print", Path.GetFileName(file));
        if (sumatra is not null) { Cli.Run(sumatra, ["-print-to-default", "-silent", file], timeoutMs: 60000); return; }
        if (OperatingSystem.IsWindows()) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { Verb = "print", UseShellExecute = true }); return; }
        if (Cli.Find("lp") is { } lp) Cli.Run(lp, [file]); else throw new InvalidOperationException("Kein Druckprogramm gefunden.");
    }

    // ---------- Schreiben ----------
    private Control DokuWriteView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "doku-schreiben";
        var nt = Field("new-title", "Titel", 260);
        var tl = new StackPanel { Spacing = 2 }; var ti = 0;
        foreach (var (name, ver, file) in DocVersions.Templates(_core.DataRoot)) { var f = file; tl.Children.Add(Row(P($"{name} (Version {ver})", Text, 11.5), Act($"tpl-use-{ti++}", "Neu daraus", () => Try(V, () => { var o = Path.Combine(DokuOut, $"{DateTime.Now:yyyy-MM-dd-HHmm}-{Path.GetFileName(f)}"); File.Copy(f, o); Launch(o); Say("Angelegt: " + Path.GetFileName(o), V); })))); }
        var ta = Field("tpl-file", "Datei als Vorlage aufnehmen", 300); var tn = Field("tpl-name", "Name der Vorlage", 180);
        sp.Children.Add(Card(Section("Neues Dokument und Vorlagen", "Leeres Word-Dokument oder Tabelle anlegen und öffnen. Eigene Vorlagen bekommen eine Versionsnummer im Namen – alte Versionen bleiben.",
            Line(nt, Act("new-docx", "Neues Word-Dokument", () => Try(V, () => { var o = Path.Combine(DokuOut, $"{DateTime.Now:yyyy-MM-dd-HHmm}-{Web.SafeFileName(new Uri("https://x/" + Uri.EscapeDataString(nt.Text?.Trim() is { Length: > 0 } t ? t : "Dokument")))}.docx"); Ooxml.NewDocx(o, nt.Text ?? "", [""]); Launch(o); Say("Angelegt: " + Path.GetFileName(o), V); }), true),
                Act("new-xlsx", "Neue Tabelle", () => Try(V, () => { var o = Path.Combine(DokuOut, $"{DateTime.Now:yyyy-MM-dd-HHmm}-Tabelle.xlsx"); Ooxml.NewXlsx(o, [["Datum", "Beschreibung", "Betrag"]]); Launch(o); Say("Angelegt: " + Path.GetFileName(o), V); })),
                Act("new-odt", "Als ODT (LibreOffice)", async () => { var lo = ToolPath("libreoffice"); if (lo is null) { Say("Dafür fehlt LibreOffice.", V); return; } var d = Path.Combine(DokuOut, $"{DateTime.Now:yyyy-MM-dd-HHmm}-Dokument.docx"); Ooxml.NewDocx(d, nt.Text ?? "", [""]); var (ok, m, p) = await PdfExport.ConvertToAsync(new RealProcessRunner(), lo, d, "odt"); File.Delete(d); Say(ok ? "Angelegt: " + Path.GetFileName(p) : m, V); })),
            Line(ta, tn, Act("tpl-add", "Als Vorlage speichern", () => Try(V, () => { var o = DocVersions.AddTemplate(_core.DataRoot, tn.Text ?? "", Clean(ta)); Say("Vorlage gespeichert: " + Path.GetFileName(o), V); }))), tl)));

        var mt = Field("merge-template", "Word-Vorlage mit Platzhaltern wie {{Name}}", 340); var mc = Field("merge-csv", "CSV-Datei (erste Zeile = Feldnamen)", 300);
        sp.Children.Add(Card(Section("Serienbrief", "Für jede Zeile der CSV-Datei entsteht ein eigenes Dokument. Platzhalter in der Vorlage: {{Spaltenname}}.",
            Line(mt, mc, Act("merge-run", "Erstellen", () => Try(V, () =>
            {
                var rows = Csv.Records(File.ReadAllText(Clean(mc))); if (rows.Count == 0) throw new ArgumentException("Die CSV-Datei hat keine Datenzeilen.");
                var missing = Ooxml.Placeholders(Clean(mt)).Where(p => !rows[0].ContainsKey(p)).ToList();
                var files = Ooxml.MailMerge(Clean(mt), rows, Path.Combine(DokuOut, "Serienbrief-" + DateTime.Now.ToString("yyyyMMdd-HHmm")), rows[0].Keys.FirstOrDefault(k => k.Contains("name", StringComparison.OrdinalIgnoreCase)) ?? "");
                Say($"{files.Count} Dokumente erstellt." + (missing.Count > 0 ? " Achtung, diese Platzhalter gibt es nicht in der CSV: " + string.Join(", ", missing) : ""), V);
            }), true)))));

        var tx = new TextBox { Name = "text-input", PlaceholderText = "Text hier einfügen …", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Width = 620, MinHeight = 90 }; var to = new StackPanel { Name = "text-result" };
        var dic = Speller.KnownPaths.Select(Environment.ExpandEnvironmentVariables).FirstOrDefault(File.Exists);
        sp.Children.Add(Card(Section("Text prüfen", (dic is null ? "Für die Rechtschreibprüfung fehlt ein deutsches Wörterbuch (kommt mit LibreOffice). " : "") + "Zählt Wörter und Sätze und schätzt, wie leicht der Text zu lesen ist.",
            tx, Line(Act("text-stats", "Zählen und bewerten", () => { var s = Writing.Analyze(tx.Text ?? ""); to.Children.Clear(); to.Children.Add(P($"{s.Words} Wörter · {s.Sentences} Sätze · {s.Chars} Zeichen · Lesbarkeit {s.Flesch:0} von 100 ({s.Level})", Text)); }, true),
                Act("text-spell", "Rechtschreibung", () => Try(V, () =>
                {
                    if (dic is null) throw new InvalidOperationException("Kein deutsches Wörterbuch gefunden – bitte LibreOffice installieren.");
                    var m = new Speller(dic, Path.ChangeExtension(dic, ".aff")).Mistakes(tx.Text ?? ""); to.Children.Clear();
                    to.Children.Add(m.Count == 0 ? P("Keine Fehler gefunden.", Text) : P("Vielleicht falsch: " + string.Join(", ", m), Warn));
                }))), to)));

        var ca = Field("cmp-a", "Alte Fassung (TXT, DOCX, ODT, PDF)", 300); var cb = Field("cmp-b", "Neue Fassung", 300); var co = new StackPanel { Name = "cmp-result", Spacing = 1 };
        sp.Children.Add(Card(Section("Zwei Fassungen vergleichen", "Grün = neu hinzugekommen, rot = entfernt.",
            Line(ca, cb, Act("cmp-run", "Vergleichen", () => Try(V, () =>
            {
                co.Children.Clear(); var d = Writing.Diff(Writing.TextOf(Clean(ca), Tool("pdftotext")), Writing.TextOf(Clean(cb), Tool("pdftotext")));
                co.Children.Add(P($"{d.Count(x => x.Op == '+')} Zeilen neu, {d.Count(x => x.Op == '−')} entfernt.", Text));
                foreach (var (op, line) in d.Where(x => x.Op != ' ').Take(80)) co.Children.Add(P($"{op} {line}", op == '+' ? Brush.Parse("#22C55E") : Brush.Parse("#EF4444"), 11.5));
            }))), co)));

        var sd = Field("sig-doc", "Word-Dokument (DOCX)", 300); var si = Field("sig-img", "Bild der Unterschrift (PNG)", 260);
        var pm = Field("pkg-main", "Hauptdokument", 260); var pa = new TextBox { Name = "pkg-att", PlaceholderText = "Anlagen, eine pro Zeile", AcceptsReturn = true, Width = 300, MinHeight = 40 };
        sp.Children.Add(Card(Section("Unterschrift und Versand-Paket", "Die Bild-Unterschrift ist KEINE rechtsverbindliche elektronische Signatur. Das Paket ist eine ZIP mit Prüfsummenliste, damit der Empfänger Vollständigkeit prüfen kann.",
            Line(sd, si, Act("sig-run", "Unterschrift einfügen", () => Try(V, () =>
            {
                var (w, h) = ImageTools.Size(Clean(si)); var k = Math.Min(1.0, 220.0 / w);
                var o = Ooxml.InsertSignature(Clean(sd), File.ReadAllBytes(Clean(si)), (int)(w * k), (int)(h * k), "Bild-Unterschrift – nicht rechtsverbindlich im Sinne einer qualifizierten elektronischen Signatur.");
                Say("Gespeichert: " + Path.GetFileName(o), V);
            }))),
            Line(pm, pa, Act("pkg-run", "Paket erstellen", () => Try(V, () =>
            {
                var z = Path.Combine(DokuOut, Path.GetFileNameWithoutExtension(Clean(pm)) + "-paket.zip");
                DocVersions.ExportPackage(Clean(pm), (pa.Text ?? "").Split('\n').Select(x => x.Trim().Trim('"')).Where(x => x.Length > 0), z);
                Say($"Paket erstellt: {Path.GetFileName(z)} (Prüfsummen {(DocVersions.VerifyPackage(z) ? "stimmen" : "FEHLER")}).", V);
            }))))));
        return DokuPage("Schreiben", sp);
    }

    // ---------- Ordnen ----------
    private Control DokuOrderView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "doku-ordnen";
        var tf = Field("table-file", "CSV- oder XLSX-Datei"); var grid = new StackPanel { Name = "table-view", Spacing = 1 };
        sp.Children.Add(Card(Section("Tabelle ansehen", "Öffnet CSV und Excel-Dateien ohne Office (die ersten 1000 Zeilen).",
            Line(tf, Act("table-open", "Anzeigen", () => Try(V, () =>
            {
                var f = Clean(tf); var rows = Path.GetExtension(f).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? Ooxml.ReadXlsx(f) : Csv.Parse(File.ReadAllText(f), 1000);
                grid.Children.Clear(); grid.Children.Add(P($"{rows.Count} Zeilen", Text));
                foreach (var (r, i) in rows.Take(200).Select((r, i) => (r, i))) grid.Children.Add(P(string.Join("  │  ", r), i == 0 ? Text : null, 11));
            }), true)), grid)));

        var mt = Field("min-title", "Thema der Besprechung", 260); var ma = Field("min-who", "Teilnehmende", 260);
        var mn = new TextBox { Name = "min-notes", PlaceholderText = "Was wurde besprochen?", AcceptsReturn = true, Width = 520, MinHeight = 60 };
        var mtasks = new TextBox { Name = "min-tasks", PlaceholderText = "Aufgaben, je Zeile: Was | Wer | bis TT.MM.JJJJ", AcceptsReturn = true, Width = 520, MinHeight = 50 };
        sp.Children.Add(Card(Section("Besprechungsprotokoll", "Erstellt ein Word-Protokoll und eine Kalenderdatei (.ics) mit den Aufgaben – die lässt sich in Outlook oder aufs Handy übernehmen.",
            Line(mt, ma), mn, mtasks, Act("min-save", "Protokoll erstellen", () => Try(V, () =>
            {
                var tasks = (mtasks.Text ?? "").Split('\n').Where(l => l.Trim().Length > 0).Select(l => l.Split('|').Select(x => x.Trim()).ToArray())
                    .Select(p => new TaskItem(Guid.NewGuid().ToString("N")[..8], p[0], p.Length > 2 && DateTime.TryParseExact(p[2].Replace("bis", "").Trim(), "dd.MM.yyyy", De, System.Globalization.DateTimeStyles.None, out var d) ? d.AddHours(9) : null, p.Length > 1 ? p[1] : "", false)).ToList();
                var (docx, ics) = TaskList.WriteMinutes(new(mt.Text?.Trim() is { Length: > 0 } t ? t : "Besprechung", DateTime.Now, ma.Text ?? "", mn.Text ?? "", tasks), DokuOut);
                foreach (var x in tasks) Tasks.Add(x.Title, x.Due, x.Who);
                Say($"Gespeichert: {Path.GetFileName(docx)} und {Path.GetFileName(ics)}. Aufgaben stehen auch unten in der Liste.", V);
            }), true))));

        var at = Field("task-title", "Aufgabe", 260); var ad = Field("task-due", "fällig TT.MM.JJJJ (optional)", 180); var tlist = new StackPanel { Spacing = 2 }; var i = 0;
        foreach (var t in Tasks.All())
        {
            var id = t.Id; var late = !t.Done && t.Due < DateTime.Now;
            tlist.Children.Add(Row(P($"{(t.Done ? "☑" : "☐")} {t.Title}{(t.Due is { } d ? $" – bis {d:dd.MM.yyyy}" : "")}{(t.Who.Length > 0 ? " – " + t.Who : "")}{(late ? " – überfällig" : "")}", late ? Warn : Text, 11.5),
                Line(Act($"task-done-{i}", t.Done ? "Wieder offen" : "Erledigt", () => { Tasks.Toggle(id); Show(V); }), Act($"task-del-{i}", "Löschen", () => { Tasks.Delete(id); Show(V); })))); i++;
        }
        if (i == 0) tlist.Children.Add(P("Keine Aufgaben."));
        sp.Children.Add(Card(Section("Aufgaben", "Deine To-dos mit Fälligkeit. „Als Kalender speichern“ legt eine .ics-Datei mit Erinnerungen an.",
            Line(at, ad, Act("task-add", "Hinzufügen", () => Try(V, () =>
            {
                if (string.IsNullOrWhiteSpace(at.Text)) throw new ArgumentException("Bitte die Aufgabe eintragen.");
                DateTime? due = Clean(ad).Length == 0 ? null : DateTime.TryParseExact(Clean(ad), "dd.MM.yyyy", De, System.Globalization.DateTimeStyles.None, out var d) ? d.AddHours(9) : throw new ArgumentException("Datum bitte als TT.MM.JJJJ.");
                Tasks.Add(at.Text, due); Show(V);
            }), true), Act("task-ics", "Als Kalender speichern", () => Try(V, () => { var f = Path.Combine(DokuOut, "aufgaben.ics"); File.WriteAllText(f, TaskList.ToIcs(Tasks.All().Where(t => !t.Done))); Say("Gespeichert: " + f, V); }))), tlist)));

        var qf = Field("docsearch-folder", "Ordner", 300, ProjectsRoot); var qq = Field("docsearch-query", "Suchwort", 200); var qo = new StackPanel { Name = "docsearch-result", Spacing = 2 };
        sp.Children.Add(Card(Section("In Dokumenten suchen", "Sucht im Text von Word-, LibreOffice-, PDF- und Textdateien und zeigt die Fundstelle.",
            Line(qf, qq, Act("docsearch-run", "Suchen", () => Try(V, () =>
            {
                qo.Children.Clear(); var hits = Writing.Search(Clean(qf), qq.Text ?? "", Tool("pdftotext"));
                qo.Children.Add(P(hits.Count == 0 ? "Nichts gefunden." : $"{hits.Count} Fundstellen:", Text)); var k = 0;
                foreach (var h in hits.Take(40)) { var f = h.File; qo.Children.Add(Act($"docsearch-hit-{k++}", $"{Path.GetFileName(h.File)}, Zeile {h.Line}: {h.Snippet}", () => Launch(f))); }
            }), true)), qo)));
        return DokuPage("Ordnen", sp);
    }
}
