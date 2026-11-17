using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Buero;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Etappe „Arbeiten &amp; Büro“ Teil A (T-0671): Bewerbung &amp; Lebenslauf, Karten &amp; Einladungen, Verträge-Ordner.
/// Kerne in <c>Trendo.Core.Buero</c>; PDF über PDFsharp und den Brief-Kern, DOCX über „Trendo Text“, Kalenderdatei über den Kalender-Kern.
/// Anforderungen: docs/nachbau/bewerbung|karten|vertraege/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string CvKey = "bewerbung-programm", CardKey = "karten-programm", ConKey = "vertraege-programm";

    private void InitBueroStudios() { InitBewerbung(); InitKarten(); InitVertraege(); }

    /// <summary>Feld, das Getipptes nach einer Sekunde Pause, beim Verlassen und beim Neuzeichnen übernimmt – ohne die Seite neu zu zeichnen (der Cursor bleibt).</summary>
    static TextBox BueroBox(string name, string value, string hint, Action<string> commit, bool multi = false, double width = double.NaN)
    {
        var t = new TextBox { Name = name, Text = value, PlaceholderText = hint, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 64 : 0, FontSize = 12.5, Width = width,
            VerticalContentAlignment = multi ? VerticalAlignment.Top : VerticalAlignment.Center };
        ToolTip.SetTip(t, hint);
        var shown = value; var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        void Commit() { timer.Stop(); var now = t.Text ?? ""; if (now != shown) { shown = now; commit(now); } }
        timer.Tick += (_, _) => Commit();
        t.TextChanged += (_, _) => { timer.Stop(); if ((t.Text ?? "") != shown) timer.Start(); };
        t.LostFocus += (_, _) => Commit();
        t.DetachedFromVisualTree += (_, _) => Commit();
        return t;
    }

    ComboBox BueroCombo(string key, string name, IEnumerable<(string Id, string Name)> items, string current, Action<string> set, double minWidth = 140)
    {
        var list = items.ToList();
        var c = new ComboBox { Name = name, ItemsSource = list.Select(i => i.Name).ToList(), SelectedIndex = Math.Max(0, list.FindIndex(i => i.Id == current)), MinWidth = minWidth, FontSize = 12 };
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0 && list[c.SelectedIndex].Id != current) { var v = list[c.SelectedIndex].Id; ProgramRun(key, () => set(v)); } };
        return c;
    }

    static StackPanel BueroLabeled(string label, Control c) => new() { Spacing = 2, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11 }, c } };

    static Border BueroChip(string text, IBrush bg, IBrush? fg = null) => new()
    {
        Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(9, 3), CornerRadius = new CornerRadius(12), Background = bg,
        Child = new TextBlock { Text = text, Foreground = fg ?? Text, FontSize = 12 },
    };

    static Control BueroFindings(string name, IReadOnlyList<CvFinding> list, string ok)
    {
        var sp = new StackPanel { Name = name, Spacing = 3 };
        if (list.Count == 0) sp.Children.Add(new TextBlock { Text = "✓ " + ok, Foreground = K4Good, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        foreach (var f in list.OrderByDescending(f => f.Fehler))
            sp.Children.Add(new TextBlock { Text = (f.Fehler ? "✗ " : "• ") + f.Feld + ": " + f.Text, Foreground = f.Fehler ? K4Bad : K4Warn, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    // ======================================================================
    // Bewerbung & Lebenslauf
    // ======================================================================

    /// <summary>Tests: Antwort der KI für das Anschreiben (Prompt → Text) statt des echten KI-Kerns.</summary>
    public static Func<string, Task<string?>>? CvAiOverride { get; set; }
    /// <summary>Tests: „heute“ für Prüfung und Fristen.</summary>
    public static Func<DateTime>? CvNowOverride { get; set; }
    static DateTime CvNow => CvNowOverride?.Invoke() ?? DateTime.Now;
    bool _cvAiBusy;

    public static BewerbungsAkte CvOf(ProgramDocument d) => BewerbungsAkte.FromJson(d.Data["akte"]);
    public void CvChange(ProgramDocument d, string label, Action<BewerbungsAkte> change)
    {
        var a = CvOf(d); change(a); var j = a.ToJson();
        d.Edit(label, x => x["akte"] = j);
    }
    static JsonObject CvDoc(BewerbungsAkte a) => new() { ["akte"] = a.ToJson(), ["view"] = "lebenslauf" };

    static BewerbungsAkte CvAusbildungVorlage() => new()
    {
        Cv = new Lebenslauf
        {
            Person = new() { Name = "", Titel = "Schülerin / Schüler", Profil = "Ich suche einen Ausbildungsplatz und arbeite gern mit Menschen. In Praktika habe ich gelernt, zuverlässig und sorgfältig zu sein." },
            Stationen = [new() { Id = "s1", Art = "praktikum", Titel = "Schülerpraktikum", Firma = "", Von = "", Bis = "", Text = "" }],
            Ausbildung = [new() { Id = "a1", Abschluss = "Mittlerer Schulabschluss (voraussichtlich)", Einrichtung = "", Von = "", Bis = "" }],
            Sprachen = [new() { Name = "Deutsch", Stufe = "Muttersprache" }, new() { Name = "Englisch", Stufe = "B1" }],
            Layout = "modern",
        },
    };

    private void InitBewerbung() => RegisterProgram(FachTabs, new ProgramSpec(CvKey, "STUDIO-CV-01", "Bewerbung & Lebenslauf")
    {
        Sentence = "Lebenslauf eintragen, in drei Layouts als PDF oder Word, auf Lücken prüfen, Anschreiben zur Stelle und die ganze Bewerbungsmappe als eine PDF.",
        Folder = "Bewerbungen",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Lebenslauf"] = 0, ["Anschreiben"] = 0, ["Mappe"] = 0, ["Übersicht"] = 0 },
        Templates =
        [
            new("leer", "Leerer Lebenslauf", "Alles selbst eintragen", () => CvDoc(new BewerbungsAkte())),
            new("beispiel", "Beispiel: Bürokauffrau", "Fertiger Lebenslauf mit Stelle zum Ausprobieren", () => CvDoc(CvBeispiel.Akte())),
            new("ausbildung", "Erste Bewerbung (Ausbildung)", "Schule, Praktikum, Sprachen – modernes Layout", () => CvDoc(CvAusbildungVorlage())),
        ],
        Ribbon =
        [
            ("Lebenslauf", [
                new ProgramCmd("cv-check", "Prüfen", d => CvCheckNow(d), Primary: true, Tip: "Kontaktdaten, Datumsformat, Lücken im Zeitverlauf, Länge, Sprachstufen."),
                new ProgramCmd("cv-add-station", "＋ Station", d => { CvChange(d, "Station", a => a.Cv.Stationen.Insert(0, new CvStation { Id = NewId("s") })); d.Data["view"] = "lebenslauf"; }),
                new ProgramCmd("cv-add-edu", "＋ Ausbildung", d => { CvChange(d, "Ausbildung", a => a.Cv.Ausbildung.Insert(0, new CvAusbildung { Id = NewId("a") })); d.Data["view"] = "lebenslauf"; }),
                new ProgramCmd("cv-add-lang", "＋ Sprache", d => CvChange(d, "Sprache", a => a.Cv.Sprachen.Add(new CvSprache { Name = "", Stufe = "B1" }))),
                new ProgramCmd("cv-photo", "Foto …", d => _ = CvPickPhoto(d), Tip: "Bewerbungsfoto (freiwillig)."),
            ]),
            ("Anschreiben", [
                new ProgramCmd("cv-letter-draft", "Entwurf schreiben", d => CvLetterDraft(d), Tip: "Aus Stelle und Lebenslauf – ohne KI, fehlende Angaben stehen in [Klammern]."),
                new ProgramCmd("cv-letter-ai", "Mit KI schreiben", d => _ = CvLetterAi(d), Tip: "Nutzt den eingestellten KI-Zugang. Erfindet nichts – nur Angaben aus Lebenslauf und Anzeige."),
                new ProgramCmd("cv-letter-open", "In „Briefe schreiben“ öffnen", d => CvToLetter(d), Tip: "Feinschliff, Designs und Unterschrift im Briefprogramm."),
            ]),
            ("Mappe", [
                new ProgramCmd("cv-attach", "Anlage …", d => _ = CvPickAttachment(d), Tip: "Zeugnis oder Zertifikat als PDF oder Bild."),
                new ProgramCmd("cv-mappe", "Mappe als PDF", d => CvMappeNow(d), Tip: "Deckblatt, Anschreiben, Lebenslauf und Anlagen in einer PDF."),
            ]),
            ("Übersicht", [
                new ProgramCmd("cv-track", "Bewerbung eintragen", d => CvTrackCurrent(d), Tip: "Die aktuelle Stelle in die Übersicht übernehmen (Status „Versendet“, heute)."),
            ]),
        ],
        Exports =
        [
            new("pdf", "Lebenslauf (PDF)", ".pdf", (d, f) => { CvPdf.Write(CvOf(d).Cv, f); return f; }),
            new("docx", "Lebenslauf (Word)", ".docx", (d, f) => { Trendo.Core.Office.DocxFormat.Write(CvText.Doc(CvOf(d).Cv), f); return f; }),
            new("ats", "ATS-Textfassung (.txt)", ".txt", (d, f) => { File.WriteAllText(f, CvText.Ats(CvOf(d).Cv), new UTF8Encoding(false)); return f; }),
            new("anschreiben", "Anschreiben (PDF)", ".pdf", (d, f) => { LetterPdf.Write(CvLetterDraftOf(CvOf(d)), f); return f; }),
            new("mappe", "Bewerbungsmappe (PDF)", ".pdf", (d, f) => CvMappe(CvOf(d), f)),
            new("csv", "Bewerbungen (CSV)", ".csv", (d, f) => { File.WriteAllText(f, BewerbungsListe.Csv(CvOf(d).Bewerbungen), new UTF8Encoding(true)); return f; }),
        ],
        SimpleExports = 2,
        // T-0702 (V-02): leerer Lebenslauf – der erste Schritt holt das Namensfeld
        FirstStep = new("Namen eintragen", "Fang mit deinem Namen an – danach Stationen, Ausbildung und Sprachen. Rechts siehst du sofort, wie der Lebenslauf aussieht.",
            d => { d.Data["view"] = "lebenslauf"; ProgramFirstFocus(d, "cv-p-name"); },
            d => !ProgramFirstDone(d) && CvOf(d).Cv is { Stationen.Count: 0, Ausbildung.Count: 0 } cv && cv.Person.Name.Trim().Length == 0),
        Workspace = CvWorkspace,
        Properties = CvProperties,
        Status = d =>
        {
            var a = CvOf(d); var f = CvCheck.Run(a.Cv, CvNow);
            return $"{a.Cv.Stationen.Count} Stationen · {a.Cv.Ausbildung.Count} Ausbildungen · etwa {CvCheck.Pages(a.Cv):0.#} Seiten · {f.Count(x => x.Fehler)} Pflichtpunkte · {a.Bewerbungen.Count(BewerbungsListe.Offen)} offene Bewerbungen";
        },
        Help = "Oben zwischen Lebenslauf, Anschreiben, Mappe und Übersicht wechseln. Datum als MM.JJJJ (z. B. 03.2019), Ende leer = bis heute. Rechts siehst du die Prüfung und wählst das Layout. "
             + "Für das Anschreiben die Stellenanzeige einfügen, dann „Entwurf schreiben“ oder „Mit KI schreiben“. „Mappe als PDF“ fügt Deckblatt, Anschreiben, Lebenslauf und Anlagen zusammen. Trendo versendet nichts.",
    }, "bewerbung", "lebenslauf", "cv", "anschreiben", "bewerbungsmappe", "resume", "europass", "ats", "jobsuche", "stellenanzeige", "zeugnisse");

    // ---------- Befehle ----------

    public List<CvFinding> CvCheckNow(ProgramDocument d)
    {
        var f = CvCheck.Run(CvOf(d).Cv, CvNow);
        var err = f.Count(x => x.Fehler);
        _progMsg[CvKey] = f.Count == 0 ? "✓ Der Lebenslauf ist vollständig – keine Lücken, Daten im richtigen Format." : $"Prüfung: {err} Pflichtpunkte, {f.Count - err} Hinweise – rechts unter „Prüfung“.";
        return f;
    }

    public void CvLetterDraft(ProgramDocument d)
    {
        var a = CvOf(d);
        if (a.Stelle.Position.Trim().Length == 0 && a.Stelle.Anzeige.Trim().Length == 0) throw new InvalidOperationException("Erst unter „Anschreiben“ die Stelle eintragen (Bezeichnung oder Anzeige).");
        var text = Anschreiben.Entwurf(a.Cv, a.Stelle);
        CvChange(d, "Anschreiben-Entwurf", x => x.Anschreiben = text); d.Data["view"] = "anschreiben";
        _progMsg[CvKey] = "Entwurf steht – bitte lesen und anpassen. Fehlende Angaben stehen in [Klammern].";
    }

    /// <summary>Anschreiben mit dem vorhandenen KI-Kern; ohne KI bleibt es beim eigenen Entwurf.</summary>
    public async Task CvLetterAi(ProgramDocument d)
    {
        if (_cvAiBusy) return;
        var a = CvOf(d);
        if (a.Stelle.Position.Trim().Length == 0 && a.Stelle.Anzeige.Trim().Length == 0) { _progMsg[CvKey] = "Erst unter „Anschreiben“ die Stelle eintragen (Bezeichnung oder Anzeige)."; Show(CvKey); return; }
        _cvAiBusy = true; _progMsg[CvKey] = "Die KI schreibt das Anschreiben …"; Show(CvKey);
        string? text = null;
        try
        {
            var prompt = Anschreiben.Prompt(a.Cv, a.Stelle);
            text = CvAiOverride is { } o ? await o(prompt)
                : _kiConnected || !_opt.Isolated ? (await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } r ? r.Text : null) : null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or TaskCanceledException) { text = null; }
        finally { _cvAiBusy = false; }
        if (string.IsNullOrWhiteSpace(text))
        {
            CvChange(d, "Anschreiben-Entwurf", x => x.Anschreiben = Anschreiben.Entwurf(x.Cv, x.Stelle));
            _progMsg[CvKey] = "Keine KI erreichbar – stattdessen steht der eigene Entwurf da. KI einrichten unter „KI fragen“.";
        }
        else
        {
            var clean = Anschreiben.Clean(text, a.Stelle);
            CvChange(d, "Anschreiben (KI)", x => x.Anschreiben = clean);
            _progMsg[CvKey] = "Die KI hat das Anschreiben geschrieben – bitte prüfen: stimmt jede Angabe?";
        }
        d.Data["view"] = "anschreiben"; Show(CvKey);
    }

    static LetterDraft CvLetterDraftOf(BewerbungsAkte a) =>
        Anschreiben.AlsBrief(a.Cv, a.Stelle, a.Anschreiben.Trim().Length > 0 ? a.Anschreiben : Anschreiben.Entwurf(a.Cv, a.Stelle), CvNow.Date);

    /// <summary>Anschreiben an „Briefe schreiben“ übergeben (Brief-Kern, gleiches Format wie dort).</summary>
    public void CvToLetter(ProgramDocument d)
    {
        var draft = CvLetterDraftOf(CvOf(d));
        _letter = draft; _letterGallery = false; _letterSaved = false; _letterStep = 3;
        _letterMsg = "Anschreiben aus „Bewerbung & Lebenslauf“ übernommen – hier Design, Unterschrift und Feinschliff. Versendet wird nichts.";
        _progMsg[CvKey] = "Anschreiben an „Briefe schreiben“ übergeben.";
        Dispatcher.UIThread.Post(() => Show("brief")); // nach dem Neuzeichnen des Programms (ProgramRun) wechseln
    }

    async Task CvPickPhoto(ProgramDocument d)
    {
        var f = await PickMediaFile("cv-photo", "Bewerbungsfoto", ["*.jpg", "*.jpeg", "*.png"]);
        if (f is not null) ProgramRun(CvKey, () => { CvChange(d, "Foto", a => a.Cv.Person.Foto = f); _progMsg[CvKey] = "Foto eingefügt: " + Path.GetFileName(f); });
    }

    async Task CvPickAttachment(ProgramDocument d)
    {
        var f = await PickMediaFile("cv-attach", "Anlage (Zeugnis, Zertifikat)", ["*.pdf", "*.png", "*.jpg", "*.jpeg"]);
        if (f is not null) ProgramRun(CvKey, () => CvAddAttachment(d, f));
    }

    public void CvAddAttachment(ProgramDocument d, string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Die Datei gibt es nicht.", file);
        if (!Mappe.AnlageEndungen.Contains(Path.GetExtension(file).ToLowerInvariant())) throw new InvalidDataException("Anlagen bitte als PDF, PNG oder JPG.");
        CvChange(d, "Anlage", a => { if (!a.Anlagen.Contains(file)) a.Anlagen.Add(file); }); d.Data["view"] = "mappe";
        _progMsg[CvKey] = "Anlage hinzugefügt: " + Path.GetFileName(file);
    }

    /// <summary>Mappe in einen eigenen Arbeitsordner unter %TEMP%\trendo-…, der danach wieder gelöscht wird.</summary>
    static string CvMappe(BewerbungsAkte a, string output)
    {
        var work = Path.Combine(Path.GetTempPath(), "trendo-mappe-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            var letter = Path.Combine(work, "01-anschreiben.pdf"); LetterPdf.Write(CvLetterDraftOf(a), letter);
            var cv = Path.Combine(work, "02-lebenslauf.pdf"); CvPdf.Write(a.Cv, cv);
            Mappe.Erstellen(a, letter, cv, output, work);
            return output;
        }
        finally { try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    public string CvMappeNow(ProgramDocument d)
    {
        var a = CvOf(d);
        var stem = DevStudio.SafeName("Bewerbung " + (a.Stelle.Firma.Trim().Length > 0 ? a.Stelle.Firma.Trim() : a.Cv.Person.Name.Trim()));
        var dir = WorkFolder("Bewerbungen"); var f = Path.Combine(dir, stem + ".pdf");
        for (var n = 2; File.Exists(f); n++) f = Path.Combine(dir, $"{stem} ({n}).pdf");
        CvMappe(a, f);
        var pages = Trendo.Core.Sys.PdfTools.Read(f).Pages;
        _progMsg[CvKey] = $"Bewerbungsmappe fertig ({pages} Seiten): {f}";
        NotifyFileResult(_progMsg[CvKey]);
        return f;
    }

    public void CvTrackCurrent(ProgramDocument d)
    {
        var a = CvOf(d);
        if (a.Stelle.Firma.Trim().Length == 0 && a.Stelle.Position.Trim().Length == 0) throw new InvalidOperationException("Erst unter „Anschreiben“ Firma und Stelle eintragen – oder in der Übersicht eine Zeile anlegen.");
        CvChange(d, "Bewerbung eingetragen", x => x.Bewerbungen.Insert(0, new BewerbungEintrag { Id = NewId("b"), Firma = x.Stelle.Firma.Trim(), Stelle = x.Stelle.Position.Trim(), Status = "versendet",
            Beworben = CvNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Frist = CvNow.AddDays(14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Notiz = "Nachfassen, wenn keine Antwort" }));
        d.Data["view"] = "uebersicht";
        _progMsg[CvKey] = "In die Übersicht eingetragen – Frist zum Nachfassen in 14 Tagen.";
    }

    // ---------- Oberfläche ----------

    static readonly (string Id, string Name)[] CvViews = [("lebenslauf", "Lebenslauf"), ("anschreiben", "Anschreiben"), ("mappe", "Mappe"), ("uebersicht", "Übersicht")];
    Border? _cvPreviewHost, _cvFindingsHost;
    const string CvAllGood = "Alles drin: Kontakt, Daten, keine Lücken.";

    Control CvWorkspace(ProgramDocument d)
    {
        var view = d.Text("view", "lebenslauf"); var a = CvOf(d);
        var tabs = new WrapPanel { Name = "cv-tabs", Margin = new Thickness(0, 0, 0, 10) };
        foreach (var (id, name) in CvViews)
        {
            var b = new Button { Name = "cv-tab-" + id, Content = name, Padding = new Thickness(14, 6), Margin = new Thickness(0, 0, 6, 0) };
            if (id == view) b.Classes.Add("accent");
            b.Click += (_, _) => { d.Data["view"] = id; Show(CvKey); };
            tabs.Children.Add(b);
        }
        Control left = view switch { "anschreiben" => CvLetterView(d, a), "mappe" => CvMappeView(d, a), "uebersicht" => CvListView(d, a), _ => CvEditor(d, a) };
        _cvPreviewHost = new Border { Name = "cv-preview-host", Child = view == "anschreiben" ? CvLetterPreview(a) : CvPreview(a.Cv) };
        Control grid = view == "uebersicht" ? left : new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,Auto"), Children = { left, Pos(_cvPreviewHost, 2) } };
        var root = new StackPanel { Margin = new Thickness(4, 0, 10, 4), Children = { tabs, grid } };
        // T-0705: Zeugnisse und Zertifikate (PDF, Bild) auf die Arbeitsfläche ziehen = Anlage der Mappe; im Lebenslauf ein Foto = Bewerbungsfoto
        BueroFileDrop(root, CvKey, BueroExt([.. Mappe.AnlageEndungen]), "Anlagen als PDF, PNG oder JPG", (dd, files) =>
        {
            if (dd.Text("view", "lebenslauf") == "lebenslauf" && files is [var one] && !one.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            { CvChange(dd, "Foto", x => x.Cv.Person.Foto = one); _progMsg[CvKey] = "Foto eingefügt: " + Path.GetFileName(one); return; }
            foreach (var f in files) CvAddAttachment(dd, f);
            _progMsg[CvKey] = $"{files.Count} Anlage(n) zur Mappe hinzugefügt – „Mappe als PDF“ fügt sie hinten an.";
        });
        return root;
    }

    /// <summary>Nach dem Tippen nur die Vorschau neu zeichnen (das Eingabefeld behält den Cursor).</summary>
    void CvTouch(ProgramDocument d, string label, Action<BewerbungsAkte> change)
    {
        CvChange(d, label, change);
        var a = CvOf(d);
        if (_cvPreviewHost is { } host) host.Child = d.Text("view", "lebenslauf") == "anschreiben" ? CvLetterPreview(a) : CvPreview(a.Cv);
        if (_cvFindingsHost is { } fh) fh.Child = BueroFindings("cv-findings", CvCheck.Run(a.Cv, CvNow), CvAllGood);
    }

    Control CvEditor(ProgramDocument d, BewerbungsAkte a)
    {
        var sp = new StackPanel { Name = "cv-editor", Spacing = 12 };
        var p = a.Cv.Person;
        TextBox F(string key, string label, string value, Action<CvPerson, string> set, bool multi = false) => BueroBox("cv-p-" + key, value, label, v => CvTouch(d, label, x => set(x.Cv.Person, v)), multi);
        var person = new StackPanel { Spacing = 6, Children = { H("Persönliche Daten", 14),
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { BueroLabeled("Name", F("name", "Vor- und Nachname", p.Name, (x, v) => x.Name = v)), Pos(BueroLabeled("Berufsbezeichnung", F("titel", "z. B. Kauffrau für Büromanagement", p.Titel, (x, v) => x.Titel = v)), 2) } },
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { BueroLabeled("E-Mail", F("email", "name@beispiel.de", p.Email, (x, v) => x.Email = v)), Pos(BueroLabeled("Telefon", F("telefon", "0171 2345678", p.Telefon, (x, v) => x.Telefon = v)), 2) } },
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { BueroLabeled("Straße", F("strasse", "Musterweg 12", p.Strasse, (x, v) => x.Strasse = v)), Pos(BueroLabeled("PLZ und Ort", F("ort", "12345 Musterstadt", p.Ort, (x, v) => x.Ort = v)), 2) } },
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { BueroLabeled("Webseite oder Profil (freiwillig)", F("web", "z. B. Profil-Link", p.Web, (x, v) => x.Web = v)), Pos(BueroLabeled("Geburtsdatum (freiwillig)", F("geb", "TT.MM.JJJJ", p.Geburtsdatum, (x, v) => x.Geburtsdatum = v)), 2) } },
            BueroLabeled("Kurzprofil – zwei bis vier Sätze", F("profil", "Wer bist du, was kannst du, was suchst du?", p.Profil, (x, v) => x.Profil = v, true)),
            p.Foto.Length > 0 ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Foto: " + Path.GetFileName(p.Foto), Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center },
                KrBtn("cv-photo-del", "Entfernen", () => CvChange(d, "Foto entfernt", x => x.Cv.Person.Foto = ""), key: CvKey) } } : new Border(),
        } };
        sp.Children.Add(KrCard(person, "cv-person-card"));

        // Stationen
        var st = new StackPanel { Name = "cv-stations", Spacing = 8 };
        st.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("Berufserfahrung", 14), Pos(KrBtn("cv-station-add", "＋ Station", () => CvChange(d, "Station", x => x.Cv.Stationen.Insert(0, new CvStation { Id = NewId("s") })), key: CvKey), 1) } });
        if (a.Cv.Stationen.Count == 0) st.Children.Add(P("Noch keine Station. Auch Praktika, Nebenjobs und Ehrenamt zählen.", Muted, 12));
        var i = 0;
        foreach (var s in a.Cv.Stationen)
        {
            var id = s.Id; var n = i++;
            TextBox SF(string key, string label, string value, Action<CvStation, string> set, bool multi = false, double w = double.NaN) =>
                BueroBox($"cv-st-{n}-{key}", value, label, v => CvTouch(d, label, x => { if (x.Cv.Stationen.FirstOrDefault(y => y.Id == id) is { } y) set(y, v); }), multi, w);
            st.Children.Add(new Border { BorderBrush = Brush.Parse("#33FFFFFF"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 0), Child = new StackPanel { Spacing = 5, Children = {
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*,8,Auto"), Children = { SF("titel", "Bezeichnung, z. B. Verkäuferin", s.Titel, (y, v) => y.Titel = v), Pos(SF("firma", "Firma", s.Firma, (y, v) => y.Firma = v), 2),
                    Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = {
                        KrBtn($"cv-st-{n}-up", "▲", () => CvChange(d, "Station nach oben", x => CvMove(x.Cv.Stationen, id, -1)), key: CvKey),
                        KrBtn($"cv-st-{n}-down", "▼", () => CvChange(d, "Station nach unten", x => CvMove(x.Cv.Stationen, id, 1)), key: CvKey),
                        KrBtn($"cv-st-{n}-del", "✕", () => CvChange(d, "Station gelöscht", x => x.Cv.Stationen.RemoveAll(y => y.Id == id)), key: CvKey) } }, 4) } },
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,100,8,100,8,Auto"), Children = { SF("ort", "Ort", s.Ort, (y, v) => y.Ort = v), Pos(SF("von", "von MM.JJJJ", s.Von, (y, v) => y.Von = v), 2), Pos(SF("bis", "bis (leer = heute)", s.Bis, (y, v) => y.Bis = v), 4),
                    Pos(BueroCombo(CvKey, $"cv-st-{n}-art", [("beruf", "Beruf"), ("praktikum", "Praktikum"), ("ehrenamt", "Ehrenamt"), ("sonstiges", "Sonstiges")], s.Art, v => CvChange(d, "Art", x => { if (x.Cv.Stationen.FirstOrDefault(y => y.Id == id) is { } y) y.Art = v; }), 110), 6) } },
                SF("text", "Aufgaben und Erfolge – eine je Zeile", s.Text, (y, v) => y.Text = v, true),
            } } });
        }
        sp.Children.Add(KrCard(st, "cv-stations-card"));

        // Ausbildung
        var ed = new StackPanel { Name = "cv-edu", Spacing = 8 };
        ed.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("Ausbildung und Schule", 14), Pos(KrBtn("cv-edu-add", "＋ Ausbildung", () => CvChange(d, "Ausbildung", x => x.Cv.Ausbildung.Insert(0, new CvAusbildung { Id = NewId("a") })), key: CvKey), 1) } });
        i = 0;
        foreach (var e in a.Cv.Ausbildung)
        {
            var id = e.Id; var n = i++;
            TextBox EF(string key, string label, string value, Action<CvAusbildung, string> set, bool multi = false) =>
                BueroBox($"cv-ed-{n}-{key}", value, label, v => CvTouch(d, label, x => { if (x.Cv.Ausbildung.FirstOrDefault(y => y.Id == id) is { } y) set(y, v); }), multi);
            ed.Children.Add(new Border { BorderBrush = Brush.Parse("#33FFFFFF"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 0), Child = new StackPanel { Spacing = 5, Children = {
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*,8,Auto"), Children = { EF("abschluss", "Abschluss, z. B. Mittlere Reife", e.Abschluss, (y, v) => y.Abschluss = v), Pos(EF("einrichtung", "Schule, Hochschule, Betrieb", e.Einrichtung, (y, v) => y.Einrichtung = v), 2),
                    Pos(KrBtn($"cv-ed-{n}-del", "✕", () => CvChange(d, "Ausbildung gelöscht", x => x.Cv.Ausbildung.RemoveAll(y => y.Id == id)), key: CvKey), 4) } },
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,100,8,100,8,80"), Children = { EF("ort", "Ort", e.Ort, (y, v) => y.Ort = v), Pos(EF("von", "von MM.JJJJ", e.Von, (y, v) => y.Von = v), 2), Pos(EF("bis", "bis MM.JJJJ", e.Bis, (y, v) => y.Bis = v), 4), Pos(EF("note", "Note", e.Note, (y, v) => y.Note = v), 6) } },
            } } });
        }
        sp.Children.Add(KrCard(ed, "cv-edu-card"));

        // Kenntnisse, Sprachen, Interessen
        var more = new StackPanel { Spacing = 6, Children = { H("Kenntnisse und Sprachen", 14),
            BueroLabeled("Kenntnisse – durch Komma getrennt", BueroBox("cv-skills", string.Join(", ", a.Cv.Kenntnisse), "z. B. Excel, DATEV, Führerschein Klasse B", v => CvTouch(d, "Kenntnisse", x => x.Cv.Kenntnisse = [.. v.Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]))),
            BueroLabeled("Interessen – durch Komma getrennt (freiwillig)", BueroBox("cv-hobbies", string.Join(", ", a.Cv.Interessen), "z. B. Laufen, Kochen", v => CvTouch(d, "Interessen", x => x.Cv.Interessen = [.. v.Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]))) } };
        for (var k = 0; k < a.Cv.Sprachen.Count; k++)
        {
            var kk = k; var l = a.Cv.Sprachen[k];
            more.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto,8,Auto"), Children = {
                BueroBox($"cv-lang-{k}-name", l.Name, "Sprache", v => CvTouch(d, "Sprache", x => { if (kk < x.Cv.Sprachen.Count) x.Cv.Sprachen[kk].Name = v; })),
                Pos(BueroCombo(CvKey, $"cv-lang-{k}-level", CvCheck.Stufen.Select(s => (s, s == "Muttersprache" ? s : $"{s} – {CvCheck.StufeText(s)}")), l.Stufe, v => CvChange(d, "Sprachstufe", x => { if (kk < x.Cv.Sprachen.Count) x.Cv.Sprachen[kk].Stufe = v; }), 200), 2),
                Pos(KrBtn($"cv-lang-{k}-del", "✕", () => CvChange(d, "Sprache gelöscht", x => { if (kk < x.Cv.Sprachen.Count) x.Cv.Sprachen.RemoveAt(kk); }), key: CvKey), 4) } });
        }
        more.Children.Add(KrBtn("cv-lang-add", "＋ Sprache", () => CvChange(d, "Sprache", x => x.Cv.Sprachen.Add(new CvSprache { Name = "", Stufe = "B1" })), key: CvKey));
        more.Children.Add(BueroLabeled("Ort, Datum unter dem Lebenslauf (freiwillig)", BueroBox("cv-sign", a.Cv.Unterschrift, "z. B. Musterstadt, 15.11.2026", v => CvTouch(d, "Unterschrift", x => x.Cv.Unterschrift = v))));
        sp.Children.Add(KrCard(more, "cv-more-card"));
        return sp;
    }

    static void CvMove<T>(List<T> list, string id, int dir) where T : class
    {
        var i = list.FindIndex(x => (x as CvStation)?.Id == id); var j = i + dir;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
    }

    /// <summary>Vorschau wie das PDF: weißes A4-Blatt, Layout und Akzentfarbe.</summary>
    static Control CvPreview(Lebenslauf cv)
    {
        var acc = Brush.Parse(KartenKit.IsHex(cv.Akzent) ? cv.Akzent : "#1F5FA8"); var dark = Brush.Parse("#222222"); var grey = Brush.Parse("#5C6B7A");
        var modern = cv.Layout == "modern"; var compact = cv.Layout == "kompakt"; var fs = compact ? 8.5 : 9.5;
        TextBlock T(string t, double size, IBrush b, bool bold = false, bool italic = false) => new() { Text = t, FontSize = size, Foreground = b, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, FontStyle = italic ? FontStyle.Italic : FontStyle.Normal, TextWrapping = TextWrapping.Wrap };
        var main = new StackPanel { Spacing = 3 };
        void Head(string t) { main.Children.Add(new Border { Margin = new Thickness(0, 8, 0, 2), BorderBrush = acc, BorderThickness = new Thickness(0, 0, 0, 1), Child = T(t.ToUpperInvariant(), fs + 1.5, acc, true) }); }
        var p = cv.Person;
        main.Children.Add(T(p.Name.Trim().Length > 0 ? p.Name : "Dein Name", compact ? 18 : 21, modern ? dark : acc, true));
        if (p.Titel.Trim().Length > 0) main.Children.Add(T(p.Titel, fs + 2.5, grey));
        var contact = string.Join("  ·  ", new[] { p.Strasse, p.Ort, p.Telefon, p.Email, p.Web }.Select(s => s.Trim()).Where(s => s.Length > 0));
        if (!modern && contact.Length > 0) main.Children.Add(T(contact, fs - 0.5, grey));
        if (p.Profil.Trim().Length > 0) { Head("Profil"); main.Children.Add(T(p.Profil, fs, dark)); }
        void Entry(string range, string head, string sub, string text)
        {
            var body = new StackPanel { Spacing = 1, Children = { T(head, fs, dark, true) } };
            if (sub.Length > 0) body.Children.Add(T(sub, fs - 0.5, grey, italic: true));
            foreach (var l in text.Replace("\r", "").Split('\n').Select(x => x.Trim().TrimStart('-', '•', '*').Trim()).Where(x => x.Length > 0)) body.Children.Add(T("• " + l, fs - 0.5, dark));
            if (modern) { body.Children.Insert(0, T(range, fs - 1, grey)); main.Children.Add(body); }
            else main.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions(compact ? "78,*" : "92,*"), Margin = new Thickness(0, 2, 0, 3), Children = { T(range, fs - 0.5, grey), Pos(body, 1) } });
        }
        if (cv.Stationen.Count > 0) { Head("Berufserfahrung"); foreach (var s in cv.Stationen.OrderByDescending(s => CvDate.SortKey(s.Von, s.Bis))) Entry(CvDate.Range(s.Von, s.Bis), s.Titel, string.Join(", ", new[] { s.Firma, s.Ort }.Select(x => x.Trim()).Where(x => x.Length > 0)), s.Text); }
        if (cv.Ausbildung.Count > 0) { Head("Ausbildung"); foreach (var e in cv.Ausbildung.OrderByDescending(e => CvDate.SortKey(e.Von, e.Bis))) Entry(CvDate.Range(e.Von, e.Bis), e.Abschluss + (e.Note.Trim().Length > 0 ? $" (Note {e.Note.Trim()})" : ""), string.Join(", ", new[] { e.Einrichtung, e.Ort }.Select(x => x.Trim()).Where(x => x.Length > 0)), e.Text); }
        var langs = cv.Sprachen.Select(s => $"{s.Name} – {CvCheck.StufeText(s.Stufe)}" + (s.Stufe == "Muttersprache" ? "" : $" ({s.Stufe})")).ToList();
        if (!modern)
        {
            if (cv.Kenntnisse.Count > 0) { Head("Kenntnisse"); main.Children.Add(T(string.Join("  ·  ", cv.Kenntnisse), fs, dark)); }
            if (langs.Count > 0) { Head("Sprachen"); foreach (var l in langs) main.Children.Add(T(l, fs, dark)); }
            if (cv.Interessen.Count > 0) { Head("Interessen"); main.Children.Add(T(string.Join(", ", cv.Interessen), fs, dark)); }
        }
        if (cv.Unterschrift.Trim().Length > 0) { main.Children.Add(new Border { Height = 10 }); main.Children.Add(T(cv.Unterschrift, fs, dark)); }
        Control page = main;
        if (modern)
        {
            var side = new StackPanel { Spacing = 3 };
            void S(string title, IEnumerable<string> rows) { var r = rows.Where(x => x.Trim().Length > 0).ToList(); if (r.Count == 0) return; side.Children.Add(T(title.ToUpperInvariant(), fs, Brushes.White, true)); foreach (var x in r) side.Children.Add(T(x, fs - 1, Brush.Parse("#E6FFFFFF"))); side.Children.Add(new Border { Height = 8 }); }
            S("Kontakt", new[] { p.Strasse, p.Ort, p.Telefon, p.Email, p.Web });
            S("Kenntnisse", cv.Kenntnisse); S("Sprachen", langs); S("Interessen", cv.Interessen);
            page = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*"), Children = { new Border { Background = acc, Padding = new Thickness(12, 16), Child = side }, Pos(new Border { Padding = new Thickness(16, 16, 20, 16), Child = main }, 1) } };
        }
        else page = new Border { Padding = new Thickness(26, 22), Child = main };
        var pages = CvCheck.Pages(cv);
        return new StackPanel { Name = "cv-preview", Spacing = 4, Width = 420, Children = {
            new Border { Background = Brushes.White, MinHeight = 594, CornerRadius = new CornerRadius(3), BoxShadow = BoxShadows.Parse("0 6 18 0 #66000000"), ClipToBounds = true, Child = page },
            new TextBlock { Name = "cv-preview-pages", Text = $"Vorschau · {CvPdf.Layouts.First(l => l.Id == (CvPdf.Layouts.Any(x => x.Id == cv.Layout) ? cv.Layout : "klassisch")).Name} · etwa {pages:0.#} Seiten", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } };
    }

    static Control CvLetterPreview(BewerbungsAkte a)
    {
        var dark = Brush.Parse("#222222"); var grey = Brush.Parse("#5C6B7A");
        var draft = CvLetterDraftOf(a);
        TextBlock T(string t, double size, IBrush b, bool bold = false) => new() { Text = t, FontSize = size, Foreground = b, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap };
        var sp = new StackPanel { Spacing = 6, Children = {
            T(string.Join(" · ", new[] { draft.SenderName, draft.SenderStreet, draft.SenderCity }.Where(x => x.Trim().Length > 0)), 7.5, grey),
            T(draft.Recipient.Trim().Length > 0 ? draft.Recipient : "[Firma und Anschrift]", 9.5, dark),
            new TextBlock { Text = draft.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), FontSize = 9, Foreground = grey, HorizontalAlignment = HorizontalAlignment.Right },
            T(draft.Subject, 10, dark, true), T(draft.Salutation, 9.5, dark), T(draft.Body, 9.5, dark), T(draft.Closing, 9.5, dark), T(draft.SenderName, 9.5, dark), T("Anlagen: " + draft.Attachments, 8.5, grey) } };
        return new StackPanel { Name = "cv-letter-preview", Width = 420, Spacing = 4, Children = {
            new Border { Background = Brushes.White, MinHeight = 594, Padding = new Thickness(30, 26), CornerRadius = new CornerRadius(3), BoxShadow = BoxShadows.Parse("0 6 18 0 #66000000"), Child = sp },
            new TextBlock { Text = $"Anschreiben · {a.Anschreiben.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Length} Wörter (gut: 150–300)", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } };
    }

    Control CvLetterView(ProgramDocument d, BewerbungsAkte a)
    {
        var s = a.Stelle;
        TextBox F(string key, string label, string value, Action<CvStelle, string> set, bool multi = false) => BueroBox("cv-job-" + key, value, label, v => CvTouch(d, label, x => set(x.Stelle, v)), multi);
        var job = new StackPanel { Spacing = 6, Children = { H("Die Stelle", 14),
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { BueroLabeled("Firma", F("firma", "Firma", s.Firma, (x, v) => x.Firma = v)), Pos(BueroLabeled("Stelle", F("position", "Bezeichnung der Stelle", s.Position, (x, v) => x.Position = v)), 2) } },
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { BueroLabeled("Ansprechperson", F("kontakt", "z. B. Frau Beispiel", s.Ansprechpartner, (x, v) => x.Ansprechpartner = v)), Pos(BueroLabeled("Eintritt möglich ab", F("eintritt", "z. B. 01.02.2027", s.Eintritt, (x, v) => x.Eintritt = v)), 2) } },
            BueroLabeled("Anschrift der Firma", F("anschrift", "Straße\nPLZ Ort", s.Anschrift, (x, v) => x.Anschrift = v, true)),
            BueroLabeled("Stellenanzeige (einfügen)", F("anzeige", "Text der Anzeige – für den Abgleich der Stichwörter", s.Anzeige, (x, v) => x.Anzeige = v, true)),
        } };
        var sp = new StackPanel { Name = "cv-letter", Spacing = 12, Children = { KrCard(job, "cv-job-card") } };
        if (s.Anzeige.Trim().Length > 0)
        {
            var (found, missing) = CvText.Abgleich(s.Anzeige, a.Cv);
            var chips = new WrapPanel { Name = "cv-keywords" };
            foreach (var w in found) chips.Children.Add(BueroChip("✓ " + w, Brush.Parse("#1F4D35")));
            foreach (var w in missing) chips.Children.Add(BueroChip("+ " + w, Brush.Parse("#4D3A1F")));
            sp.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = { H("Stichwörter aus der Anzeige", 14), chips,
                P("Grün steht schon im Lebenslauf. Gelb fehlt noch – nur aufnehmen, was wirklich stimmt. Bewerbungsportale suchen oft nach genau diesen Wörtern.", Muted, 11) } }, "cv-keywords-card"));
        }
        var letter = new StackPanel { Spacing = 6, Children = {
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,6,Auto"), Children = { H("Anschreiben", 14),
                Pos(KrBtn("cv-draft", "Entwurf schreiben", () => CvLetterDraft(d), key: CvKey), 1),
                Pos(new Button { Name = "cv-ai", Content = _cvAiBusy ? "KI schreibt …" : "Mit KI schreiben", IsEnabled = !_cvAiBusy, Padding = new Thickness(12, 6) }.Also(b => b.Click += (_, _) => _ = CvLetterAi(d)), 3) } },
            BueroBox("cv-letter-text", a.Anschreiben, "Anrede, drei bis vier Absätze – der Gruß kommt automatisch", v => CvTouch(d, "Anschreiben", x => x.Anschreiben = v), true),
            P("Der Brief wird nach DIN 5008 gesetzt (wie in „Briefe schreiben“). Absender kommt aus dem Lebenslauf, Empfänger aus der Stelle.", Muted, 11),
        } };
        sp.Children.Add(KrCard(letter, "cv-letter-card"));
        return sp;
    }

    Control CvMappeView(ProgramDocument d, BewerbungsAkte a)
    {
        var sp = new StackPanel { Name = "cv-mappe", Spacing = 6 };
        sp.Children.Add(H("Bewerbungsmappe", 14));
        sp.Children.Add(P("Reihenfolge: Deckblatt · Anschreiben · Lebenslauf · Anlagen. Alles in einer PDF – so wollen es die meisten Firmen.", Muted, 12));
        var cover = new CheckBox { Name = "cv-cover", Content = "Mit Deckblatt", IsChecked = a.Deckblatt, Foreground = Text, FontSize = 12.5 };
        cover.IsCheckedChanged += (_, _) => { if ((cover.IsChecked == true) != a.Deckblatt) ProgramRun(CvKey, () => CvChange(d, "Deckblatt", x => x.Deckblatt = cover.IsChecked == true)); };
        sp.Children.Add(cover);
        var list = new StackPanel { Name = "cv-attachments", Spacing = 4 };
        var n = 0;
        foreach (var f in a.Anlagen)
        {
            var ff = f; var k = n++;
            list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,4,Auto,4,Auto"), Children = {
                new TextBlock { Text = (File.Exists(f) ? "📄 " : "⚠ fehlt: ") + Path.GetFileName(f), Foreground = File.Exists(f) ? Text : K4Warn, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                Pos(KrBtn($"cv-att-{k}-up", "▲", () => CvChange(d, "Anlage nach oben", x => { var i = x.Anlagen.IndexOf(ff); if (i > 0) (x.Anlagen[i - 1], x.Anlagen[i]) = (x.Anlagen[i], x.Anlagen[i - 1]); }), key: CvKey), 1),
                Pos(KrBtn($"cv-att-{k}-down", "▼", () => CvChange(d, "Anlage nach unten", x => { var i = x.Anlagen.IndexOf(ff); if (i >= 0 && i < x.Anlagen.Count - 1) (x.Anlagen[i + 1], x.Anlagen[i]) = (x.Anlagen[i], x.Anlagen[i + 1]); }), key: CvKey), 3),
                Pos(KrBtn($"cv-att-{k}-del", "✕", () => CvChange(d, "Anlage entfernt", x => x.Anlagen.Remove(ff)), key: CvKey), 5) } });
        }
        if (a.Anlagen.Count == 0) list.Children.Add(P("Noch keine Anlagen. Zeugnisse und Zertifikate als PDF oder Foto hinzufügen.", Muted, 12));
        sp.Children.Add(list);
        sp.Children.Add(new WrapPanel { Children = {
            KrBtn("cv-attach-add", "Anlage hinzufügen …", () => _ = CvPickAttachment(d), key: CvKey).Also(b => b.Margin = new Thickness(0, 0, 6, 0)),
            KrBtn("cv-mappe-go", "Mappe als PDF erstellen", () => CvMappeNow(d), true, CvKey) } });
        return KrCard(sp, "cv-mappe-card");
    }

    Control CvListView(ProgramDocument d, BewerbungsAkte a)
    {
        var sp = new StackPanel { Name = "cv-list", Spacing = 6 };
        var count = BewerbungsListe.Zaehlen(a.Bewerbungen);
        var tiles = new WrapPanel();
        foreach (var (id, name) in BewerbungsListe.Status) tiles.Children.Add(BueroChip($"{name}: {count[id]}", Brush.Parse(id switch { "zusage" => "#1F4D35", "absage" => "#4D1F1F", "gespraech" => "#1F3A4D", _ => "#2A3A55" })));
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H($"Meine Bewerbungen ({a.Bewerbungen.Count})", 14),
            Pos(KrBtn("cv-list-add", "＋ Zeile", () => CvChange(d, "Bewerbung", x => x.Bewerbungen.Insert(0, new BewerbungEintrag { Id = NewId("b"), Beworben = CvNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) })), key: CvKey), 1) } });
        sp.Children.Add(tiles);
        foreach (var (e, days) in BewerbungsListe.Faellig(a.Bewerbungen, CvNow))
            sp.Children.Add(new TextBlock { Text = $"⚠ {e.Firma} – {e.Stelle}: Frist {(days < 0 ? $"seit {-days} Tagen vorbei" : days == 0 ? "heute" : $"in {days} Tagen")}" + (e.Notiz.Length > 0 ? $" ({e.Notiz})" : ""), Foreground = days < 0 ? K4Bad : K4Warn, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*,8,150,8,110,8,110,8,*,8,Auto"), Margin = new Thickness(0, 6, 0, 0), Children = {
            P("Firma", Muted, 11), Pos(P("Stelle", Muted, 11), 2), Pos(P("Status", Muted, 11), 4), Pos(P("Beworben am", Muted, 11), 6), Pos(P("Frist", Muted, 11), 8), Pos(P("Notiz", Muted, 11), 10) } });
        var i = 0;
        foreach (var e in a.Bewerbungen)
        {
            var id = e.Id; var n = i++;
            TextBox F(string key, string label, string value, Action<BewerbungEintrag, string> set) => BueroBox($"cv-b-{n}-{key}", value, label, v => CvChange(d, label, x => { if (x.Bewerbungen.FirstOrDefault(y => y.Id == id) is { } y) set(y, v); }));
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*,8,150,8,110,8,110,8,*,8,Auto"), Children = {
                F("firma", "Firma", e.Firma, (y, v) => y.Firma = v), Pos(F("stelle", "Stelle", e.Stelle, (y, v) => y.Stelle = v), 2),
                Pos(BueroCombo(CvKey, $"cv-b-{n}-status", BewerbungsListe.Status, e.Status, v => CvChange(d, "Status", x => { if (x.Bewerbungen.FirstOrDefault(y => y.Id == id) is { } y) y.Status = v; }), 140), 4),
                Pos(F("beworben", "JJJJ-MM-TT", e.Beworben, (y, v) => y.Beworben = v), 6), Pos(F("frist", "JJJJ-MM-TT", e.Frist, (y, v) => y.Frist = v), 8),
                Pos(F("notiz", "Notiz", e.Notiz, (y, v) => y.Notiz = v), 10),
                Pos(KrBtn($"cv-b-{n}-del", "✕", () => CvChange(d, "Bewerbung gelöscht", x => x.Bewerbungen.RemoveAll(y => y.Id == id)), key: CvKey), 12) } });
        }
        if (a.Bewerbungen.Count == 0) sp.Children.Add(P("Noch keine Bewerbung. Unter „Anschreiben“ die Stelle eintragen und oben „Bewerbung eintragen“ – oder hier „＋ Zeile“.", Muted, 12));
        var follow = BewerbungsListe.Nachfassen(a.Bewerbungen, CvNow);
        if (follow.Count > 0) sp.Children.Add(P("Nachfassen? Seit zwei Wochen keine Antwort: " + string.Join(", ", follow.Select(f => f.Firma)), K4Warn, 12));
        return KrCard(sp, "cv-list-card");
    }

    static readonly string[] CvAccents = ["#1F5FA8", "#0F766E", "#7C3AED", "#B45309", "#BE123C", "#334155"];

    Control CvProperties(ProgramDocument d)
    {
        var a = CvOf(d); var sp = new StackPanel { Name = "cv-props", Spacing = 6 };
        sp.Children.Add(H("Layout", 13));
        sp.Children.Add(BueroCombo(CvKey, "cv-layout", CvPdf.Layouts.Select(l => (l.Id, l.Name)), a.Cv.Layout, v => CvChange(d, "Layout", x => x.Cv.Layout = v), 180));
        sp.Children.Add(P(CvPdf.Layouts.FirstOrDefault(l => l.Id == a.Cv.Layout).Hint ?? "", Muted, 11));
        var sw = new WrapPanel();
        for (var i = 0; i < CvAccents.Length; i++)
        {
            var c = CvAccents[i];
            var b = new Button { Name = "cv-accent-" + i, Width = 26, Height = 26, Margin = new Thickness(0, 0, 4, 4), Background = Brush.Parse(c), BorderBrush = a.Cv.Akzent == c ? Brushes.White : Brushes.Transparent, BorderThickness = new Thickness(2) };
            ToolTip.SetTip(b, "Akzentfarbe " + c);
            b.Click += (_, _) => ProgramRun(CvKey, () => CvChange(d, "Akzentfarbe", x => x.Cv.Akzent = c));
            sw.Children.Add(b);
        }
        sp.Children.Add(new TextBlock { Text = "Akzentfarbe", Foreground = Muted, FontSize = 11 }); sp.Children.Add(sw);

        sp.Children.Add(H("Prüfung", 13));
        sp.Children.Add(_cvFindingsHost = new Border { Child = BueroFindings("cv-findings", CvCheck.Run(a.Cv, CvNow), CvAllGood) });
        var gaps = CvCheck.Gaps(a.Cv, CvNow);
        sp.Children.Add(P($"Etwa {CvCheck.Pages(a.Cv):0.#} Seiten · {gaps.Count} Lücken ab 3 Monaten", Muted, 11));

        sp.Children.Add(H("Fristen", 13));
        var due = BewerbungsListe.Faellig(a.Bewerbungen, CvNow);
        if (due.Count == 0) sp.Children.Add(P("Keine Frist in den nächsten 7 Tagen.", Muted, 11));
        foreach (var (e, days) in due) sp.Children.Add(P($"{e.Firma}: {(days < 0 ? "überfällig" : days == 0 ? "heute" : $"in {days} Tagen")}", days <= 0 ? K4Bad : K4Warn, 11.5));
        sp.Children.Add(P("Trendo versendet keine Bewerbung – du schickst die Mappe selbst ab (E-Mail oder Portal).", Muted, 10.5));
        return sp;
    }
}
