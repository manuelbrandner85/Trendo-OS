using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SkiaSharp;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>STUDIO-PDFTOOLS-01 PDF-Werkstatt (T-0672, Freigabe 09.11./15.11.2026): Stapel-Arbeit an PDF-Dateien wie in PDF24 oder PDFsam – links das Werkzeug,
/// in der Mitte Dateien, Seiten mit Vorschaubildern und Ergebnisse. Nutzt den Kern von „Trendo PDF“ (PdfTools) und dessen Seitenbilder (PdfView, PDFium).
/// Jede Aktion schreibt neue Dateien in „Ergebnisse“ – das Original bleibt. Anforderungen: docs/nachbau/pdf-werkstatt/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string PwKey = "pdf-werkstatt-programm";
    const string PwStudio = "STUDIO-PDFTOOLS-01";

    /// <summary>Werkzeuge: Id, Zeichen, Name, was es tut.</summary>
    static readonly (string Id, string Icon, string Name, string What)[] PwTools =
    [
        ("zusammenfuegen", "⧉", "Zusammenfügen", "Alle PDFs der Liste in dieser Reihenfolge zu einer Datei."),
        ("teilen", "✂", "Teilen", "Jede PDF nach Bereichen („1-3; 4-z“) oder alle n Seiten in mehrere Dateien."),
        ("ordnen", "▦", "Seiten ordnen", "Seiten in der Übersicht verschieben, drehen oder löschen – auch über mehrere Dateien hinweg."),
        ("bilder", "🖼", "Bilder zu PDF", "PNG- und JPG-Bilder der Liste werden Seiten einer PDF."),
        ("komprimieren", "⇲", "Komprimieren", "Bilder verkleinern und neu packen – die Größe vorher/nachher wird gemessen."),
        ("wasserzeichen", "▨", "Wasserzeichen", "Text schräg und halbdurchsichtig über die Seiten."),
        ("stempel", "▣", "Stempel", "Stempel wie „GEPRÜFT“ oder „KOPIE“ mit Datum in eine Ecke."),
        ("seitenzahlen", "#", "Seitenzahlen", "„Seite 1 von 5“ unten, oben, links oder rechts – Deckblatt auf Wunsch ohne."),
        ("metadaten", "ⓘ", "Metadaten", "Titel, Verfasser und Thema setzen – oder alle Angaben entfernen."),
        ("passwort", "🔒", "Passwort", "Mit Passwort schützen – oder mit dem bekannten Passwort entsperren."),
        ("schwaerzen", "▬", "Schwärzen", "Balken über Stellen ziehen; die Seite wird zum Bild, der Inhalt darunter ist danach wirklich weg."),
        ("vergleichen", "⇄", "Vergleichen", "Text zweier PDFs Zeile für Zeile vergleichen – mit Seitenangabe."),
    ];

    /// <summary>Werkzeuge, die je Datei der Liste laufen (Stapel).</summary>
    static readonly string[] PwPerFile = ["teilen", "komprimieren", "wasserzeichen", "stempel", "seitenzahlen", "metadaten", "passwort"];

    readonly HashSet<int> _pwSel = [];
    /// <summary>Passwort für „Passwort“ – nur im Speicher, nie im Dokument oder Protokoll.</summary>
    public string PwPassword { get; set; } = "";
    public PdfCompare? PwLastCompare { get; private set; }
    public IReadOnlyCollection<int> PwSelection => _pwSel;

    static JsonObject PwNew(string tool) => new()
    {
        ["tool"] = tool, ["files"] = new JsonArray(), ["pages"] = new JsonArray(), ["results"] = new JsonArray(), ["boxes"] = new JsonArray(),
        ["ranges"] = "1-z", ["splitMode"] = "bereiche", ["every"] = "1", ["level"] = "mittel", ["wmText"] = "VERTRAULICH", ["wmOpacity"] = "18", ["wmPages"] = "",
        ["stampText"] = "GEPRÜFT", ["stampPos"] = "oben-rechts", ["stampDate"] = true, ["numFormat"] = "Seite {n} von {N}", ["numPos"] = "unten-mitte", ["numStart"] = "1",
        ["numSkipFirst"] = false, ["metaMode"] = "setzen", ["pwMode"] = "setzen", ["imgSize"] = "a4", ["redactPage"] = 0,
    };

    private void InitPdfWerkstatt() => RegisterProgram(FachTabs, new ProgramSpec(PwKey, PwStudio, "PDF-Werkstatt")
    {
        Sentence = "Stapel-Arbeit an PDFs: zusammenfügen, teilen, ordnen, verkleinern, stempeln, schwärzen und vergleichen – jede Aktion ergibt eine neue Datei.",
        Folder = "PDF-Werkstatt",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Dateien"] = 0, ["Seiten"] = 0, ["Ergebnis"] = 0 },
        Templates =
        [
            new("zusammenfuegen", "PDFs zusammenfügen", "Mehrere Dateien zu einer", () => PwNew("zusammenfuegen")),
            new("teilen", "PDF teilen", "Nach Bereichen oder alle n Seiten", () => PwNew("teilen")),
            new("ordnen", "Seiten ordnen", "Verschieben, drehen, löschen", () => PwNew("ordnen")),
            new("komprimieren", "PDF verkleinern", "Für E-Mail und Upload", () => PwNew("komprimieren")),
            new("bilder", "Bilder zu PDF", "Fotos und Scans als PDF", () => PwNew("bilder")),
            new("schwaerzen", "Schwärzen", "Stellen wirklich entfernen", () => PwNew("schwaerzen")),
            new("vergleichen", "Zwei PDFs vergleichen", "Was hat sich geändert?", () => PwNew("vergleichen")),
        ],
        Ribbon =
        [
            ("Dateien", [
                new ProgramCmd("pw-add", "Dateien hinzufügen …", d => _ = PwPick(d), Primary: true, Tip: "PDFs (bei „Bilder zu PDF“ auch PNG und JPG) zur Liste hinzufügen."),
                new ProgramCmd("pw-run", "Ausführen", d => PwRun(d), Primary: true, Tip: "Das gewählte Werkzeug auf die Liste anwenden – das Ergebnis ist eine neue Datei."),
                new ProgramCmd("pw-clear", "Liste leeren", d => PwSetFiles(d, [], "Liste leeren")),
            ]),
            ("Seiten", [
                new ProgramCmd("pw-rot-left", "⟲ Drehen", d => PwRotate(d, -90), Tip: "Gewählte Seiten nach links drehen"),
                new ProgramCmd("pw-rot-right", "⟳ Drehen", d => PwRotate(d, 90), Tip: "Gewählte Seiten nach rechts drehen"),
                new ProgramCmd("pw-del", "Seiten löschen", d => PwDeletePages(d)),
                new ProgramCmd("pw-earlier", "Nach vorn", d => PwMove(d, -1)),
                new ProgramCmd("pw-later", "Nach hinten", d => PwMove(d, 1)),
                new ProgramCmd("pw-reset", "Alle Seiten zurück", d => PwResetPages(d), Level: 1),
            ]),
            ("Ergebnis", [
                new ProgramCmd("pw-folder", "Ergebnis-Ordner öffnen", d => { var dir = PwOutDir(); var last = d.List("results").OfType<JsonObject>().LastOrDefault()?["file"].AsText(); if (last is not null && File.Exists(last)) RevealInFolder(last); else Launch(dir); }),
                new ProgramCmd("pw-results-clear", "Ergebnisliste leeren", d => d.Set("results", new JsonArray(), "Ergebnisliste leeren"), Level: 1),
            ]),
        ],
        Exports =
        [
            new("pdf", "Ergebnis als PDF", ".pdf", (d, f) => PwRunTo(d, f)),
            new("vergleich", "Vergleich als Text (.txt)", ".txt", (d, f) => { var c = PwCompare(d); File.WriteAllText(f, PdfTools.CompareText(c.Result, c.A, c.B)); return f; }),
        ],
        // T-0774 (Q1): die KI arbeitet mit denselben Fachaktionen wie die Knöpfe – „Seiten 1–3 heraus“ ergibt eine neue PDF im offenen Programm, ein Rückgängig-Schritt
        KiActions = [PwKiPages, PwKiRun],
        KiHint = "Eine neue PDF entsteht nur mit „pdf.seiten_heraus“ (Seiten wie „1-3“ oder „2,5-z“) oder „pdf.ausfuehren“ (Werkzeug auf die Dateiliste anwenden); "
               + "„dokument.setzen“ ändert nur Einstellungen wie Wasserzeichen-Text oder Stempel und erzeugt keine Datei. Die Originale bleiben immer unverändert.",
        Tools = [.. PwTools.Select(t => new ProgramTool("pw-tool-" + t.Id, t.Icon, t.Name, t.What, d => PwSetTool(d, t.Id), () => ProgramActive(PwKey)?.Text("tool") == t.Id))],
        // T-0702 (V-02): leere Liste – erst Dateien holen
        FirstStep = new("Dateien hinzufügen …", "Wähl die PDFs (oder Bilder) oder zieh sie aus dem Explorer hierher – die Originale bleiben unverändert, jedes Ergebnis wird eine neue Datei.",
            d => _ = PwPick(d), d => PwFiles(d).Count == 0 && d.List("results").Count == 0),
        Workspace = PwWorkspace,
        Properties = PwProperties,
        Status = d => { var files = PwFiles(d); var pages = d.List("pages").Count; return $"{PwToolName(d)} · {files.Count} Datei(en)" + (pages > 0 ? $" · {pages} Seiten" : "") + $" · {d.List("results").Count} Ergebnis(se)"; },
        Help = "Links das Werkzeug wählen, oben „Dateien hinzufügen“, Einstellungen im Kasten „Werkzeug“, dann „Ausführen“. Alles landet als neue Datei unter „Ergebnisse“ – "
             + "die Originale bleiben unverändert. Seiten ordnen: Seiten anklicken (Strg für mehrere), dann drehen, löschen oder verschieben. Schwärzen: Balken mit der Maus über die Stelle ziehen.",
    }, "pdf-werkstatt", "pdf zusammenfügen", "pdf teilen", "pdf komprimieren", "pdf verkleinern", "schwärzen", "wasserzeichen", "seitenzahlen", "pdf vergleichen", "bilder zu pdf", "pdf24", "pdfsam", "metadaten");

    // ---------- T-0774: Fachaktionen für die KI ----------

    /// <summary>„Seiten herausziehen“: Bereich aus jeder PDF der Liste als neue Datei (Werkzeug „Teilen“ mit genau einem Bereich).</summary>
    ProgramKiAction PwKiPages => new(new AssistAction("pdf.seiten_heraus", "Seiten als neue PDF herausziehen", ["seiten", "heraus", "herausziehen", "herausnehmen", "extrahieren", "auszug", "neue", "pdf"],
        [new AssistParam("seiten", "Seiten", Words: ["seiten", "seite"])]),
        (d, a) =>
        {
            var spec = PwPageSpec(a.GetValueOrDefault("seiten") ?? "");
            var pdfs = PwFiles(d).Where(f => !PwIsImage(f)).ToList();
            if (pdfs.Count == 0) throw new InvalidOperationException("Es ist noch keine PDF in der Liste – erst „Dateien hinzufügen …“.");
            foreach (var f in pdfs) { if (!File.Exists(f)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(f)); PdfTools.ParsePages(spec, PdfView.PageCount(f)); } // prüft schon in der Vorschau
            d.Edit("Seiten herausziehen", x => { x["tool"] = "teilen"; x["splitMode"] = "bereiche"; x["ranges"] = spec; });
        },
        PwKiProduce, _ => PwOutDir());

    /// <summary>„Werkzeug ausführen“: das gewählte (oder genannte) Werkzeug auf die Liste – wie der Knopf „Ausführen“.</summary>
    ProgramKiAction PwKiRun => new(new AssistAction("pdf.ausfuehren", "Werkzeug ausführen (neue Datei)", ["ausfuehren", "anwenden", "zusammenfuegen", "komprimieren", "verkleinern", "wasserzeichen", "stempel", "seitenzahlen", "metadaten", "vergleichen", "bilder"],
        [new AssistParam("werkzeug", "Werkzeug", AssistKind.Choice, [.. PwTools.Select(t => t.Id)], Required: false)]),
        (d, a) =>
        {
            if (a.GetValueOrDefault("werkzeug") is { Length: > 0 } t && PwTools.Any(x => x.Id == t)) d.Edit("Werkzeug: " + PwTools.First(x => x.Id == t).Name, x => x["tool"] = t);
            if (PwFiles(d).Count == 0) throw new InvalidOperationException("Es ist noch keine Datei in der Liste – erst „Dateien hinzufügen …“.");
            PwKiAllowed(d);
        },
        PwKiProduce, _ => PwOutDir());

    /// <summary>T-0774 (Gegenprüfung 2): KI-Pläne führen keine Passwort-Werkzeuge aus (weder schützen noch entsperren) – geprüft am Stand, der wirklich läuft.</summary>
    static void PwKiAllowed(ProgramDocument d)
    {
        if (d.Text("tool") == "passwort") throw new InvalidOperationException("Passwörter setzt oder entfernt nur die Person selbst (Werkzeug „Passwort“) – die KI führt das nicht aus.");
    }

    /// <summary>Erzeugen bei „Ja, behalten“: dieselbe Prüfung noch einmal am echten Dokument, dann dieselbe Funktion wie der Knopf „Ausführen“.</summary>
    List<string> PwKiProduce(ProgramDocument d) { PwKiAllowed(d); return PwRun(d); }

    /// <summary>Füllwörter rund um eine Seitenangabe („Seiten 1 bis 3 als neue PDF heraus“) – alles andere ist eine unbekannte Angabe und wird gemeldet.</summary>
    static readonly HashSet<string> PwSpecFill = ["seite", "seiten", "s", "und", "als", "neue", "neuen", "pdf", "datei", "heraus", "herausziehen", "herausnehmen", "raus", "aus", "der", "die", "das", "dem", "den", "von",
        "nur", "bitte", "zieh", "ziehe", "nimm", "nehmen", "extrahieren", "mach", "mache", "daraus", "eine", "einer", "ein"];

    /// <summary>Seitenangabe aus KI oder Alltagssprache: „1–3“, „1 bis 3“, „Seite 2 und 5“, „3 bis Ende“, „3-“ → „1-3“, „2,5“, „3-z“.
    /// Wirft, wenn keine Seite darin steht oder ein Teil keine Seitenangabe ist (nichts wird still verworfen).</summary>
    public static string PwPageSpec(string raw)
    {
        var t = raw.ToLowerInvariant().Replace('–', '-').Replace('—', '-');
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\b(?:zum\s+|bis\s+zum\s+|bis\s+)?(?:ende|schluss|letzten?\s+seite|letzte)\b", m => m.Value.StartsWith("bis") || m.Value.StartsWith("zum") ? "-z" : "z");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s*\bbis\b\s*", "-");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s*-\s*", "-").Replace("--", "-");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"(\d)-(?=$|[\s,;]|und\b)", "$1-z"); // „3-“ = bis zum Ende
        var parts = new List<string>(); var unknown = new List<string>();
        foreach (var tok in System.Text.RegularExpressions.Regex.Split(t, @"[\s,;:.]+").Where(x => x.Length > 0))
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(tok, @"^(\d{1,6}|z)(-(\d{1,6}|z))?$")) parts.Add(tok);
            else if (!PwSpecFill.Contains(tok)) unknown.Add(tok);
        }
        if (unknown.Count > 0) throw new ArgumentException($"Diese Seitenangabe verstehe ich nicht: „{string.Join(" ", unknown)}“. Beispiel: „1-3“, „2,5“ oder „3 bis Ende“.");
        if (parts.Count == 0) throw new ArgumentException("Welche Seiten? Zum Beispiel „1-3“ oder „2,5-z“.");
        return string.Join(",", parts);
    }

    // ---------- Modell ----------

    static List<string> PwFiles(ProgramDocument d) => [.. d.List("files").Select(n => n.AsText()).OfType<string>()];
    static string PwToolName(ProgramDocument d) => PwTools.FirstOrDefault(t => t.Id == d.Text("tool")).Name ?? "Zusammenfügen";
    static bool PwIsImage(string f) => Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg";
    string PwOutDir() { var d = Path.Combine(WorkFolder("PDF-Werkstatt"), "Ergebnisse"); Directory.CreateDirectory(d); return d; }
    string PwOut(string stem, string suffix, string ext = ".pdf") => PdfTools.Free(Path.Combine(PwOutDir(), DevStudio.SafeName(stem) + suffix + ext));

    void PwSetTool(ProgramDocument d, string id)
    {
        if (d.Text("tool") == id) return;
        d.Set("tool", id, "Werkzeug: " + PwTools.First(t => t.Id == id).Name);
        _progMsg[PwKey] = PwTools.First(t => t.Id == id).What;
    }

    /// <summary>Dateiliste setzen; die Seitenübersicht beginnt wieder mit allen Seiten aller PDFs.</summary>
    void PwSetFiles(ProgramDocument d, List<string> files, string label)
    {
        var pages = new JsonArray();
        for (var i = 0; i < files.Count; i++)
        {
            if (PwIsImage(files[i])) continue;
            int n; try { n = PdfView.PageCount(files[i]); } catch (Exception) { n = 0; } // geschützt oder beschädigt: keine Seiten in der Übersicht
            for (var p = 0; p < n; p++) pages.Add(new JsonObject { ["f"] = i, ["p"] = p, ["r"] = 0 });
        }
        d.Edit(label, x => { x["files"] = new JsonArray([.. files.Select(f => (JsonNode)f)]); x["pages"] = pages; x["boxes"] = new JsonArray(); x["redactPage"] = 0; });
        _pwSel.Clear();
    }

    /// <summary>Dateien hinzufügen (öffentlich für Tests und die KI). Unbekannte Arten werden abgelehnt.</summary>
    public void PwAddFiles(ProgramDocument d, IEnumerable<string> add)
    {
        var list = PwFiles(d); var n = 0;
        foreach (var f in add)
        {
            if (!File.Exists(f)) throw new ArgumentException("Datei nicht gefunden: " + Path.GetFileName(f));
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext != ".pdf" && !PwIsImage(f)) throw new ArgumentException($"„{Path.GetFileName(f)}“ ist keine PDF und kein Bild (PNG, JPG).");
            list.Add(Path.GetFullPath(f)); n++;
        }
        PwSetFiles(d, list, "Dateien hinzufügen");
        _progMsg[PwKey] = $"{n} Datei(en) hinzugefügt – jetzt „Ausführen“.";
        _core.Audit.Append(Principal.LocalOwner.Name, "pdfw.add", $"{n} Dateien");
    }

    async Task PwPick(ProgramDocument d)
    {
        var img = d.Text("tool") == "bilder";
        var req = new PickRequest("pw-add", false, true, img ? ["png", "jpg", "jpeg"] : ["pdf"]);
        string[]? got;
        if (PickOverride is { } fake) got = await fake(req);
        else got = (await PickOpenFiles(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = img ? "Bilder wählen" : "PDF-Dateien wählen", AllowMultiple = true,
            FileTypeFilter = [img ? new Avalonia.Platform.Storage.FilePickerFileType("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg"] } : new Avalonia.Platform.Storage.FilePickerFileType("PDF") { Patterns = ["*.pdf"] }] }))
            .Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is { Length: > 0 }) ProgramRun(PwKey, () => PwAddFiles(d, got));
    }

    public void PwMoveFile(ProgramDocument d, int index, int by)
    {
        var l = PwFiles(d); var j = index + by; if (index < 0 || index >= l.Count || j < 0 || j >= l.Count) return;
        (l[index], l[j]) = (l[j], l[index]); PwSetFiles(d, l, "Reihenfolge der Dateien");
    }

    record struct PwPage(int F, int P, int R);
    static List<PwPage> PwPages(ProgramDocument d) => [.. d.List("pages").OfType<JsonObject>().Select(o => new PwPage((int)ProgramDocument.ToNumber(o["f"]), (int)ProgramDocument.ToNumber(o["p"]), (int)ProgramDocument.ToNumber(o["r"])))];
    static void PwSavePages(ProgramDocument d, IEnumerable<PwPage> pages, string label) =>
        d.Edit(label, x => x["pages"] = new JsonArray([.. pages.Select(p => (JsonNode)new JsonObject { ["f"] = p.F, ["p"] = p.P, ["r"] = p.R })]));

    public void PwSelect(params int[] pages) { _pwSel.Clear(); foreach (var p in pages) _pwSel.Add(p); }
    void PwNeedSel() { if (_pwSel.Count == 0) throw new InvalidOperationException("Bitte zuerst Seiten in der Übersicht anklicken."); }
    public void PwRotate(ProgramDocument d, int deg) { PwNeedSel(); PwSavePages(d, PwPages(d).Select((p, i) => _pwSel.Contains(i) ? p with { R = (((p.R + deg) % 360) + 360) % 360 } : p), "Seiten drehen"); }
    public void PwDeletePages(ProgramDocument d)
    {
        PwNeedSel(); var rest = PwPages(d).Where((_, i) => !_pwSel.Contains(i)).ToList();
        if (rest.Count == 0) throw new InvalidOperationException("Es muss mindestens eine Seite übrig bleiben.");
        PwSavePages(d, rest, "Seiten löschen"); _pwSel.Clear();
    }
    public void PwMove(ProgramDocument d, int by)
    {
        PwNeedSel(); var l = PwPages(d); var sel = _pwSel.Order().ToList();
        if (by < 0 && sel[0] == 0 || by > 0 && sel[^1] == l.Count - 1) return;
        var moving = sel.Select(i => l[i]).ToList(); var rest = l.Where((_, i) => !_pwSel.Contains(i)).ToList();
        var at = Math.Clamp(sel[0] + by, 0, rest.Count); rest.InsertRange(at, moving);
        PwSavePages(d, rest, by < 0 ? "Nach vorn" : "Nach hinten");
        _pwSel.Clear(); for (var k = 0; k < moving.Count; k++) _pwSel.Add(at + k);
    }
    void PwResetPages(ProgramDocument d) => PwSetFiles(d, PwFiles(d), "Alle Seiten zurück");

    /// <summary>T-0705: Seite <paramref name="from"/> an die Stelle von Seite <paramref name="to"/> ziehen (wie in PDF24/PDFsam). Ist die gezogene Seite gewählt,
    /// wandern alle gewählten Seiten gemeinsam. Nach hinten gezogen landet sie hinter dem Ziel, nach vorn davor. Rückgängig machbar.</summary>
    public void PwMoveTo(ProgramDocument d, int from, int to)
    {
        var l = PwPages(d);
        if (from < 0 || from >= l.Count || to < 0 || to >= l.Count || from == to) return;
        var moving = (_pwSel.Contains(from) ? [.. _pwSel.Order()] : new List<int> { from }).Where(i => i < l.Count).ToList();
        if (moving.Contains(to)) return;
        var rest = Enumerable.Range(0, l.Count).Where(i => !moving.Contains(i)).ToList();
        var at = rest.IndexOf(to) + (to > from ? 1 : 0);
        rest.InsertRange(at, moving);
        PwSavePages(d, rest.Select(i => l[i]), moving.Count > 1 ? $"{moving.Count} Seiten verschieben" : "Seite verschieben");
        _pwSel.Clear(); for (var k = 0; k < moving.Count; k++) _pwSel.Add(at + k);
        _progMsg[PwKey] = (moving.Count > 1 ? $"{moving.Count} Seiten" : "Seite") + $" jetzt an Stelle {at + 1} – „Ausführen“ speichert die neue Reihenfolge als neue Datei.";
    }

    /// <summary>Schwärz-Balken auf einer Seite der ersten PDF (Punkte von links/oben).</summary>
    public void PwAddBox(ProgramDocument d, int page, double x, double y, double w, double h)
    {
        if (w < 2 || h < 2) return;
        d.Edit("Balken setzen", j => { if (j["boxes"] is not JsonArray a) j["boxes"] = a = []; a.Add(new JsonObject { ["p"] = page, ["x"] = Math.Round(x, 1), ["y"] = Math.Round(y, 1), ["w"] = Math.Round(w, 1), ["h"] = Math.Round(h, 1) }); });
        _progMsg[PwKey] = $"{d.List("boxes").Count} Balken gesetzt – „Ausführen“ entfernt den Inhalt darunter.";
    }
    static List<RedactBox> PwBoxes(ProgramDocument d) => [.. d.List("boxes").OfType<JsonObject>().Select(o => new RedactBox((int)ProgramDocument.ToNumber(o["p"]), ProgramDocument.ToNumber(o["x"]), ProgramDocument.ToNumber(o["y"]), ProgramDocument.ToNumber(o["w"]), ProgramDocument.ToNumber(o["h"])))];

    // ---------- Ausführen ----------

    static int PwInt(ProgramDocument d, string key, int def, int min, int max) =>
        int.TryParse(d.Text(key).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? Math.Clamp(v, min, max) : d.Data[key] is JsonValue jv && jv.TryGetValue<double>(out var dv) ? Math.Clamp((int)dv, min, max) : def;
    static bool PwBool(ProgramDocument d, string key) => d.Data[key].AsBool();

    void PwResult(ProgramDocument d, IEnumerable<(string File, string Note)> items)
    {
        var list = items.ToList();
        d.Edit("Ergebnis", x => { if (x["results"] is not JsonArray a) x["results"] = a = []; foreach (var (f, n) in list) a.Add(new JsonObject { ["file"] = f, ["note"] = n, ["tool"] = x["tool"]?.DeepClone() }); });
    }

    static string Kb(long b) => b >= 1 << 20 ? $"{b / 1048576.0:0.0} MB" : $"{Math.Max(1, b / 1024)} KB";

    /// <summary>Werkzeug auf die Liste anwenden. Ergebnis: neue Dateien (auch im Dokument unter „results“).</summary>
    public List<string> PwRun(ProgramDocument d)
    {
        try { return PwRunCore(d); }
        catch (PdfSharp.Pdf.IO.PdfReaderException e) when (e.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException(PwPassword.Length > 0 ? "Das Passwort stimmt nicht – bitte noch einmal eingeben." : "Diese PDF ist mit Passwort geschützt – erst mit „Passwort“ entsperren.", e); }
        catch (PdfSharp.Pdf.IO.PdfReaderException e) { throw new InvalidDataException("Die PDF lässt sich nicht lesen – ist sie beschädigt?", e); }
    }

    List<string> PwRunCore(ProgramDocument d)
    {
        var tool = d.Text("tool", "zusammenfuegen"); var files = PwFiles(d);
        var pdfs = files.Where(f => !PwIsImage(f)).ToList(); var images = files.Where(PwIsImage).ToList();
        var outs = new List<(string File, string Note)>();
        if (tool == "bilder") { if (images.Count == 0) throw new InvalidOperationException("Bitte erst Bilder (PNG oder JPG) hinzufügen."); }
        else if (pdfs.Count == 0) throw new InvalidOperationException("Bitte erst PDF-Dateien hinzufügen.");
        var stem = Path.GetFileNameWithoutExtension(pdfs.FirstOrDefault() ?? images[0]);
        switch (tool)
        {
            case "zusammenfuegen":
                if (pdfs.Count < 2) throw new InvalidOperationException("Zum Zusammenfügen bitte mindestens zwei PDFs hinzufügen.");
                { var o = PwOut(stem, "-zusammengefuegt"); var n = PdfTools.Merge(pdfs, o, _core.Audit); outs.Add((o, $"{pdfs.Count} Dateien, {n} Seiten")); }
                break;
            case "ordnen":
                {
                    var ops = PwPages(d).Select(p => new PageOp(files[p.F], p.P, p.R)).ToList();
                    var o = PwOut(stem, "-geordnet"); var n = PdfTools.Rearrange(ops, o, audit: _core.Audit); outs.Add((o, $"{n} Seiten in neuer Reihenfolge"));
                }
                break;
            case "bilder":
                { var o = PwOut(Path.GetFileNameWithoutExtension(images[0]), "-bilder"); var n = PdfTools.ImagesToPdf(images, o, d.Text("imgSize", "a4"), audit: _core.Audit); outs.Add((o, $"{n} Seiten aus Bildern")); }
                break;
            case "schwaerzen":
                {
                    var boxes = PwBoxes(d); var src = pdfs[0];
                    var o = PwOut(Path.GetFileNameWithoutExtension(src), "-geschwaerzt");
                    var n = PdfTools.Redact(src, o, boxes, (page, dpi) => { using var bmp = PdfView.RenderSk(src, page, dpi); using var data = bmp.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray(); }, 150, audit: _core.Audit);
                    outs.Add((o, $"{n} Seite(n) geschwärzt – dort ist der Inhalt jetzt nur noch Bild, der Text darunter ist entfernt"));
                }
                break;
            case "vergleichen":
                {
                    var c = PwCompare(d); var o = PwOut(Path.GetFileNameWithoutExtension(c.A), "-vergleich", ".txt");
                    File.WriteAllText(o, PdfTools.CompareText(c.Result, c.A, c.B));
                    outs.Add((o, c.Result.Identical ? "Der Text ist gleich." : $"{c.Result.Removed} Zeilen nur in A, {c.Result.Added} nur in B"));
                }
                break;
            default:
                foreach (var f in pdfs) outs.AddRange(PwOne(d, tool, f));
                break;
        }
        PwResult(d, outs);
        _progMsg[PwKey] = outs.Count == 1 ? $"Fertig: {Path.GetFileName(outs[0].File)} – {outs[0].Note}." : $"Fertig: {outs.Count} neue Dateien unter „Ergebnisse“. Die Originale sind unverändert.";
        NotifyFileResult(_progMsg[PwKey]);
        return [.. outs.Select(o => o.File)];
    }

    /// <summary>Ein Werkzeug, das je Datei läuft.</summary>
    List<(string, string)> PwOne(ProgramDocument d, string tool, string f)
    {
        var stem = Path.GetFileNameWithoutExtension(f); var res = new List<(string, string)>();
        switch (tool)
        {
            case "teilen":
                var parts = d.Text("splitMode") == "alle" ? PdfTools.Split(f, PwSplitDir(stem), PwInt(d, "every", 1, 1, 10000), _core.Audit) : PdfTools.SplitRanges(f, PwSplitDir(stem), d.Text("ranges", "1-z"), _core.Audit);
                res.AddRange(parts.Select(p => (p, $"Teil von {Path.GetFileName(f)}")));
                break;
            case "komprimieren":
                {
                    var o = PwOut(stem, "-klein"); var lv = CompressLevel.Get(d.Text("level", "mittel"));
                    var r = PdfTools.Compress(f, o, lv, PwJpeg, PwJpegDecode, _core.Audit);
                    res.Add((o, r.KeptOriginal ? r.Note : $"{Kb(r.Before)} → {Kb(r.After)} ({r.Saved:P0} kleiner) · {r.Note}"));
                }
                break;
            case "wasserzeichen":
                { var o = PwOut(stem, "-wasserzeichen"); PdfTools.Watermark(f, o, d.Text("wmText", "VERTRAULICH"), PwInt(d, "wmOpacity", 18, 3, 100) / 100.0, spec: d.Text("wmPages").Trim().Length > 0 ? d.Text("wmPages") : null, audit: _core.Audit); res.Add((o, "Wasserzeichen „" + d.Text("wmText") + "“")); }
                break;
            case "stempel":
                { var o = PwOut(stem, "-stempel"); PdfTools.Stamp(f, o, d.Text("stampText", "GEPRÜFT"), d.Text("stampPos", "oben-rechts"), withDate: PwBool(d, "stampDate"), spec: "1", audit: _core.Audit); res.Add((o, "Stempel auf Seite 1")); }
                break;
            case "seitenzahlen":
                { var o = PwOut(stem, "-seitenzahlen"); var n = PdfTools.PageNumbers(f, o, d.Text("numFormat", "Seite {n} von {N}"), d.Text("numPos", "unten-mitte"), PwInt(d, "numStart", 1, 0, 100000), PwBool(d, "numSkipFirst"), audit: _core.Audit); res.Add((o, $"{n} Seiten nummeriert")); }
                break;
            case "metadaten":
                {
                    var o = PwOut(stem, d.Text("metaMode") == "entfernen" ? "-ohne-metadaten" : "-metadaten");
                    if (d.Text("metaMode") == "entfernen") { PdfTools.RemoveMeta(f, o, _core.Audit); res.Add((o, "Alle Metadaten entfernt")); }
                    else
                    {
                        var vals = new Dictionary<string, string>();
                        foreach (var (k, _) in PdfTools.MetaNames.Where(m => m.Key is "Title" or "Author" or "Subject" or "Keywords")) if (d.Data["meta" + k] is not null) vals[k] = d.Text("meta" + k);
                        PdfTools.WriteMeta(f, o, vals, _core.Audit); res.Add((o, "Metadaten gesetzt"));
                    }
                }
                break;
            case "passwort":
                {
                    if (string.IsNullOrEmpty(PwPassword)) throw new InvalidOperationException("Bitte im Kasten „Werkzeug“ ein Passwort eingeben.");
                    if (d.Text("pwMode") == "entfernen") { var o = PwOut(stem, "-entsperrt"); PdfTools.Decrypt(f, o, PwPassword, _core.Audit); res.Add((o, "Passwortschutz entfernt")); }
                    else { var o = PwOut(stem, "-geschuetzt"); PdfTools.Encrypt(f, o, PwPassword, audit: _core.Audit); res.Add((o, "Mit Passwort geschützt (AES-256)")); }
                }
                break;
            default: throw new InvalidOperationException("Unbekanntes Werkzeug.");
        }
        return res;
    }

    string PwSplitDir(string stem) { var dir = Path.Combine(PwOutDir(), DevStudio.SafeName(stem) + "-teile"); Directory.CreateDirectory(dir); return dir; }

    /// <summary>Export „Ergebnis als PDF“: Werkzeug ausführen und das (eine) Ergebnis an den gewählten Ort kopieren.</summary>
    string PwRunTo(ProgramDocument d, string target)
    {
        var outs = PwRun(d).Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (outs.Count != 1) throw new InvalidOperationException(outs.Count == 0 ? "Dieses Werkzeug ergibt keine PDF." : "Dieses Werkzeug ergibt mehrere Dateien – sie liegen unter „Ergebnisse“.");
        File.Copy(outs[0], target, true); return target;
    }

    (PdfCompare Result, string A, string B) PwCompare(ProgramDocument d)
    {
        var pdfs = PwFiles(d).Where(f => !PwIsImage(f)).ToList();
        if (pdfs.Count < 2) throw new InvalidOperationException("Zum Vergleichen bitte zwei PDFs hinzufügen (A zuerst, dann B).");
        static List<string> Texts(string f) { var n = PdfView.PageCount(f); return [.. Enumerable.Range(0, n).Select(i => PdfView.Text(f, i))]; }
        var c = PdfTools.Compare(Texts(pdfs[0]), Texts(pdfs[1]));
        PwLastCompare = c;
        return (c, Path.GetFileName(pdfs[0]), Path.GetFileName(pdfs[1]));
    }

    /// <summary>JPEG kodieren mit SkiaSharp (für „Komprimieren“).</summary>
    static byte[]? PwJpeg(byte[] px, int w, int h, int c, int q)
    {
        try
        {
            var rgba = c == 1 ? px : new byte[w * h * 4];
            if (c == 3) for (int i = 0, k = 0; i < w * h; i++, k += 3) { rgba[i * 4] = px[k]; rgba[i * 4 + 1] = px[k + 1]; rgba[i * 4 + 2] = px[k + 2]; rgba[i * 4 + 3] = 255; }
            using var bmp = new SKBitmap(new SKImageInfo(w, h, c == 1 ? SKColorType.Gray8 : SKColorType.Rgba8888, SKAlphaType.Opaque));
            if (bmp.RowBytes != w * (c == 1 ? 1 : 4)) return null;
            Marshal.Copy(rgba, 0, bmp.GetPixels(), rgba.Length);
            using var data = bmp.Encode(SKEncodedImageFormat.Jpeg, q);
            return data?.ToArray();
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return null; }
    }

    /// <summary>JPEG lesen mit SkiaSharp – Pixel als RGB (3) oder Grau (1).</summary>
    static (byte[] Pixels, int Width, int Height, int Channels)? PwJpegDecode(byte[] jpeg)
    {
        using var bmp = SKBitmap.Decode(jpeg);
        if (bmp is null) return null;
        if (bmp.ColorType == SKColorType.Gray8) { var g = new byte[bmp.Width * bmp.Height]; for (var y = 0; y < bmp.Height; y++) Marshal.Copy(bmp.GetPixels() + y * bmp.RowBytes, g, y * bmp.Width, bmp.Width); return (g, bmp.Width, bmp.Height, 1); }
        using var rgba = bmp.Copy(SKColorType.Rgba8888);
        if (rgba is null) return null;
        var all = new byte[rgba.RowBytes * rgba.Height]; Marshal.Copy(rgba.GetPixels(), all, 0, all.Length);
        var rgb = new byte[rgba.Width * rgba.Height * 3];
        for (var y = 0; y < rgba.Height; y++) for (var x = 0; x < rgba.Width; x++) { var s = y * rgba.RowBytes + x * 4; var t = (y * rgba.Width + x) * 3; rgb[t] = all[s]; rgb[t + 1] = all[s + 1]; rgb[t + 2] = all[s + 2]; }
        return (rgb, rgba.Width, rgba.Height, 3);
    }

    // ---------- Oberfläche ----------

    ComboBox PwCombo(ProgramDocument d, string key, string name, IReadOnlyList<(string Id, string Name)> items, string label)
    {
        var cur = d.Text(key); var idx = Math.Max(0, items.ToList().FindIndex(i => i.Id == cur));
        var c = new ComboBox { Name = name, ItemsSource = items.Select(i => i.Name).ToList(), SelectedIndex = idx, MinWidth = 180, FontSize = 12 };
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0 && items[c.SelectedIndex].Id != d.Text(key)) ProgramRun(PwKey, () => d.Set(key, items[c.SelectedIndex].Id, label)); };
        return c;
    }
    CheckBox PwCheck(ProgramDocument d, string key, string name, string label)
    {
        var c = new CheckBox { Name = name, Content = label, IsChecked = PwBool(d, key), FontSize = 12 };
        c.IsCheckedChanged += (_, _) => { if ((c.IsChecked == true) != PwBool(d, key)) ProgramRun(PwKey, () => d.Set(key, c.IsChecked == true, label)); };
        return c;
    }
    static Control PwRow(string label, Control c) => new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 14, 6), Children = { P(label, null, 11), c } };

    /// <summary>Einstellungen des gewählten Werkzeugs (stehen in der Arbeitsfläche, damit sie auch in „Einfach“ sichtbar sind).</summary>
    Control PwOptions(ProgramDocument d)
    {
        var w = new WrapPanel { Name = "pw-options" };
        switch (d.Text("tool"))
        {
            case "teilen":
                w.Children.Add(PwRow("Teilen", PwCombo(d, "splitMode", "pw-split-mode", [("bereiche", "Nach Bereichen"), ("alle", "Alle n Seiten")], "Teilen")));
                w.Children.Add(d.Text("splitMode") == "alle" ? PwRow("Seiten je Datei", KrBox(d, "every", "pw-every", "1")) : PwRow("Bereiche (je Datei durch ; getrennt)", KrBox(d, "ranges", "pw-ranges", "1-3; 4-z")));
                break;
            case "komprimieren":
                w.Children.Add(PwRow("Stufe", PwCombo(d, "level", "pw-level", [.. CompressLevel.All.Select(l => (l.Id, l.Name))], "Stufe")));
                break;
            case "wasserzeichen":
                w.Children.Add(PwRow("Text", KrBox(d, "wmText", "pw-wm-text", "VERTRAULICH")));
                w.Children.Add(PwRow("Deckkraft in %", KrBox(d, "wmOpacity", "pw-wm-opacity", "18")));
                w.Children.Add(PwRow("Seiten (leer = alle)", KrBox(d, "wmPages", "pw-wm-pages", "z. B. 1-3,5")));
                break;
            case "stempel":
                w.Children.Add(PwRow("Stempeltext", KrBox(d, "stampText", "pw-stamp-text", "GEPRÜFT")));
                w.Children.Add(PwRow("Ecke", PwCombo(d, "stampPos", "pw-stamp-pos", PdfTools.Positions, "Ecke")));
                w.Children.Add(PwRow(" ", PwCheck(d, "stampDate", "pw-stamp-date", "Mit Datum")));
                break;
            case "seitenzahlen":
                w.Children.Add(PwRow("Format ({n} = Seite, {N} = alle)", KrBox(d, "numFormat", "pw-num-format", "Seite {n} von {N}")));
                w.Children.Add(PwRow("Lage", PwCombo(d, "numPos", "pw-num-pos", PdfTools.Positions, "Lage")));
                w.Children.Add(PwRow("Erste Zahl", KrBox(d, "numStart", "pw-num-start", "1")));
                w.Children.Add(PwRow(" ", PwCheck(d, "numSkipFirst", "pw-num-skip", "Deckblatt ohne Zahl")));
                break;
            case "metadaten":
                w.Children.Add(PwRow("Was tun?", PwCombo(d, "metaMode", "pw-meta-mode", [("setzen", "Angaben setzen"), ("entfernen", "Alle Angaben entfernen")], "Metadaten")));
                if (d.Text("metaMode") != "entfernen")
                    foreach (var (k, n) in PdfTools.MetaNames.Where(m => m.Key is "Title" or "Author" or "Subject" or "Keywords")) w.Children.Add(PwRow(n, KrBox(d, "meta" + k, "pw-meta-" + k.ToLowerInvariant(), n)));
                break;
            case "passwort":
                w.Children.Add(PwRow("Was tun?", PwCombo(d, "pwMode", "pw-pw-mode", [("setzen", "Passwort setzen"), ("entfernen", "Passwort entfernen (bekanntes Passwort)")], "Passwort")));
                var pw = new TextBox { Name = "pw-password", PasswordChar = '•', Text = PwPassword, Width = 220, PlaceholderText = "Passwort" };
                pw.TextChanged += (_, _) => PwPassword = pw.Text ?? ""; // nur im Speicher
                w.Children.Add(PwRow("Passwort (wird nicht gespeichert)", pw));
                break;
            case "bilder":
                w.Children.Add(PwRow("Seitengröße", PwCombo(d, "imgSize", "pw-img-size", [("a4", "A4 (Bild eingepasst)"), ("bild", "So groß wie das Bild")], "Seitengröße")));
                break;
        }
        return w;
    }

    Control PwWorkspace(ProgramDocument d)
    {
        var tool = d.Text("tool", "zusammenfuegen"); var t = PwTools.FirstOrDefault(x => x.Id == tool); var files = PwFiles(d);
        var root = new StackPanel { Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };

        // Werkzeug: Name, was es tut, Einstellungen, Ausführen
        var run = new Button { Name = "pw-run-now", Content = "Ausführen", Padding = new Thickness(18, 7), FontWeight = FontWeight.SemiBold }; run.Classes.Add("accent");
        run.Click += (_, _) => ProgramRun(PwKey, () => PwRun(d));
        var add = new Button { Name = "pw-add-now", Content = tool == "bilder" ? "Bilder hinzufügen …" : "Dateien hinzufügen …", Padding = new Thickness(12, 7) };
        add.Click += (_, _) => _ = PwPick(d);
        root.Children.Add(KrCard(new StackPanel { Spacing = 6, Children =
        {
            new TextBlock { Name = "pw-tool-title", Text = $"{t.Icon}  {t.Name}", FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Text },
            P(t.What, Muted, 12), PwOptions(d),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, run } },
        } }, "pw-tool-card"));

        // Dateien
        var fl = new StackPanel { Name = "pw-files", Spacing = 4 };
        fl.Children.Add(H($"Dateien ({files.Count})", 14));
        if (files.Count == 0) fl.Children.Add(P(tool == "bilder" ? "Noch keine Bilder – oben „Bilder hinzufügen“." : "Noch keine Dateien – oben „Dateien hinzufügen“. Die Reihenfolge der Liste gilt beim Zusammenfügen.", Muted, 12));
        for (var i = 0; i < files.Count; i++)
        {
            var idx = i; var f = files[i]; string info;
            try
            {
                if (PwIsImage(f)) { var (iw, ih) = XrPlus.ImageSize(File.ReadAllBytes(f)); info = $"Bild {iw} × {ih} px"; }
                else { var r = PdfTools.Read(f); info = r.NeedsPassword ? "geschützt (Passwort nötig)" : $"{r.Pages} Seiten"; }
                info += " · " + Kb(new FileInfo(f).Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PdfSharp.Pdf.IO.PdfReaderException or InvalidOperationException) { info = "nicht lesbar"; }
            Button Mini(string name, string label, string tip, Action a) { var b = new Button { Name = name, Content = label, Padding = new Thickness(7, 1), FontSize = 11 }; ToolTip.SetTip(b, tip); b.Click += (_, _) => ProgramRun(PwKey, a); return b; }
            var letter = tool == "vergleichen" && i < 2 ? (i == 0 ? "A  " : "B  ") : $"{i + 1}.  ";
            fl.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = letter + Path.GetFileName(f), Foreground = Text, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = info, Foreground = Muted, FontSize = 11 } } },
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Children = {
                    Mini($"pw-file-up-{i}", "▲", "Nach oben", () => PwMoveFile(d, idx, -1)), Mini($"pw-file-down-{i}", "▼", "Nach unten", () => PwMoveFile(d, idx, 1)),
                    Mini($"pw-file-del-{i}", "✕", "Aus der Liste nehmen (die Datei bleibt)", () => PwSetFiles(d, [.. PwFiles(d).Where((_, k) => k != idx)], "Datei entfernen")) } }, 1) } });
        }
        root.Children.Add(KrCard(fl, "pw-files-card"));

        // Seiten / Schwärzen / Vergleich
        if (tool == "schwaerzen" && files.Any(f => !PwIsImage(f))) root.Children.Add(KrCard(PwRedactView(d, files.First(f => !PwIsImage(f))), "pw-redact-card"));
        else if (tool == "vergleichen" && PwLastCompare is { } cmp) root.Children.Add(KrCard(PwCompareView(cmp), "pw-compare-card"));
        else if (tool is "ordnen" or "zusammenfuegen" && d.List("pages").Count > 0) root.Children.Add(KrCard(PwPageGrid(d, files), "pw-pages-card"));

        // Ergebnisse
        var results = d.List("results").OfType<JsonObject>().ToList();
        var rl = new StackPanel { Name = "pw-results", Spacing = 4 };
        rl.Children.Add(H($"Ergebnisse ({results.Count})", 14));
        if (results.Count == 0) rl.Children.Add(P("Hier erscheinen die neuen Dateien. Die Originale bleiben unverändert.", Muted, 12));
        var ri = 0;
        foreach (var r in Enumerable.Reverse(results).Take(30))
        {
            var f = r["file"].AsText() ?? ""; var exists = File.Exists(f); var n = ri++;
            var open = new Button { Name = $"pw-res-open-{n}", Content = f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "In Trendo PDF öffnen" : "Öffnen", Padding = new Thickness(8, 2), FontSize = 11, IsEnabled = exists };
            open.Click += (_, _) => { if (f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { OpenPdf(f); Show("pdf"); } else Launch(f); };
            var folder = new Button { Name = $"pw-res-folder-{n}", Content = "Ordner", Padding = new Thickness(8, 2), FontSize = 11, IsEnabled = exists };
            folder.Click += (_, _) => RevealInFolder(f);
            rl.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = "✓ " + Path.GetFileName(f), Foreground = exists ? Text : Muted, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = r["note"].AsText() ?? "", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } },
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { open, folder } }, 1) } });
        }
        root.Children.Add(KrCard(rl, "pw-results-card"));
        // T-0705: PDFs und Bilder aus dem Explorer auf die Arbeitsfläche ziehen
        BueroFileDrop(root, PwKey, f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || PwIsImage(f), "PDFs oder Bilder (PNG, JPG)", (dd, add) => PwAddFiles(dd, add));
        return root;
    }

    /// <summary>Seitenübersicht mit Vorschaubildern: Klick wählt (Strg: mehrere, Umschalt: Bereich).</summary>
    Control PwPageGrid(ProgramDocument d, List<string> files)
    {
        var pages = PwPages(d);
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H($"Seiten ({pages.Count})" + (_pwSel.Count > 0 ? $" · {_pwSel.Count} gewählt" : ""), 14));
        sp.Children.Add(P("Anklicken wählt eine Seite (Strg: mehrere, Umschalt: Bereich). Seite mit der Maus an eine andere Stelle ziehen, Rechtsklick oder oben „Seiten“: drehen, löschen, verschieben. „Ausführen“ speichert die neue Reihenfolge.", Muted, 11));
        var wrap = new WrapPanel { Name = "pw-page-grid" };
        for (var i = 0; i < Math.Min(pages.Count, 400); i++)
        {
            var idx = i; var pg = pages[i]; var on = _pwSel.Contains(i);
            if (pg.F >= files.Count) continue;
            var img = new Image { Width = 92, Height = 120, Stretch = Stretch.Uniform };
            PdfThumb(img, files[pg.F], pg.P, pg.R, 16);
            var b = new Button { Name = $"pw-page-{i}", Padding = new Thickness(4), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(6),
                Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(on ? 2 : 1),
                Content = new StackPanel { Spacing = 2, Children = { new Border { Background = Brushes.White, Child = img },
                    new TextBlock { Text = (files.Count > 1 ? $"{pg.F + 1}·" : "") + (pg.P + 1) + (pg.R != 0 ? $" ⟳{pg.R}°" : ""), Foreground = on ? GoldText() : Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
            ToolTip.SetTip(b, $"{Path.GetFileName(files[pg.F])}, Seite {pg.P + 1}");
            b.Click += (_, _) =>
            {
                var mods = _pwLastMods; _pwLastMods = KeyModifiers.None;
                if (mods.HasFlag(KeyModifiers.Control)) { if (!_pwSel.Remove(idx)) _pwSel.Add(idx); }
                else if (mods.HasFlag(KeyModifiers.Shift) && _pwSel.Count > 0) { var a = _pwSel.Min(); for (var k = Math.Min(a, idx); k <= Math.Max(a, idx); k++) _pwSel.Add(k); }
                else { var only = _pwSel.Count == 1 && _pwSel.Contains(idx); _pwSel.Clear(); if (!only) _pwSel.Add(idx); }
                Show(PwKey);
            };
            b.AddHandler(PointerPressedEvent, (_, e) => _pwLastMods = e.KeyModifiers, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            // T-0705: Seite mit der Maus an eine andere Stelle ziehen (gewählte Seiten wandern gemeinsam); Rechtsklick für Drehen, Löschen, Anfang/Ende
            BueroReorder(wrap, b, i, PwKey, (from, to) => PwMoveTo(d, from, to));
            var last = pages.Count - 1;
            b.ContextMenu = BueroMenu(PwKey, "pw-page-menu-" + i, () => { if (!_pwSel.Contains(idx)) PwSelect(idx); },
                ("pw-ctx-rot-left", "⟲ Nach links drehen", () => PwRotate(d, -90), true),
                ("pw-ctx-rot-right", "⟳ Nach rechts drehen", () => PwRotate(d, 90), true),
                ("-", "", () => { }, true),
                ("pw-ctx-first", "An den Anfang", () => PwMoveTo(d, idx, 0), idx > 0),
                ("pw-ctx-last", "Ans Ende", () => PwMoveTo(d, idx, last), idx < last),
                ("-", "", () => { }, true),
                ("pw-ctx-del", "Seite löschen", () => PwDeletePages(d), pages.Count > 1));
            wrap.Children.Add(b);
        }
        sp.Children.Add(wrap);
        return sp;
    }
    KeyModifiers _pwLastMods;

    /// <summary>Schwärzen: Seite groß, Balken mit der Maus ziehen; gesetzte Balken schwarz, mit ✕ entfernbar.</summary>
    Control PwRedactView(ProgramDocument d, string file)
    {
        int count; try { count = PdfView.PageCount(file); } catch (Exception) { return P("Diese PDF lässt sich nicht anzeigen (geschützt?). Erst mit „Passwort“ entsperren.", Muted, 12); }
        var page = Math.Clamp((int)d.Number("redactPage"), 0, Math.Max(0, count - 1));
        var sizes = PdfView.Sizes(file); var (pw, ph) = sizes[page];
        const double scale = 1.1; // Bildschirm-Pixel je Punkt
        var sp = new StackPanel { Spacing = 6 };
        var prev = new Button { Name = "pw-redact-prev", Content = "◀", IsEnabled = page > 0, Padding = new Thickness(8, 2) }; prev.Click += (_, _) => ProgramRun(PwKey, () => d.Set("redactPage", page - 1, "Seite"));
        var next = new Button { Name = "pw-redact-next", Content = "▶", IsEnabled = page < count - 1, Padding = new Thickness(8, 2) }; next.Click += (_, _) => ProgramRun(PwKey, () => d.Set("redactPage", page + 1, "Seite"));
        var boxes = PwBoxes(d);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { H("Schwärzen", 14), prev, new TextBlock { Text = $"Seite {page + 1} von {count}", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, next,
            new TextBlock { Text = $"{boxes.Count} Balken", Foreground = GoldText(), VerticalAlignment = VerticalAlignment.Center } } });
        sp.Children.Add(P("Mit gedrückter Maustaste einen Rahmen über die Stelle ziehen. Beim Ausführen wird jede Seite mit Balken zum Bild – der Text darunter ist danach wirklich entfernt (nicht nur überdeckt).", Muted, 11));
        Bitmap? bmp = null; try { bmp = PdfView.Render(file, page, (int)(72 * scale * 1.5)); } catch (Exception) { }
        var canvas = new Canvas { Name = "pw-redact-canvas", Width = pw * scale, Height = ph * scale, Background = Brushes.White, Cursor = new Cursor(StandardCursorType.Cross), ClipToBounds = true };
        if (bmp is not null) canvas.Children.Add(new Image { Source = bmp, Width = pw * scale, Height = ph * scale, IsHitTestVisible = false });
        var bi = 0;
        foreach (var b in boxes)
        {
            if (b.Page != page) { bi++; continue; }
            var r = new Border { Name = $"pw-box-{bi}", Width = b.W * scale, Height = b.H * scale, Background = Brush.Parse("#E6000000"), BorderBrush = Brush.Parse("#D4A84B"), BorderThickness = new Thickness(1) };
            var k = bi; var del = new Button { Name = $"pw-box-del-{bi}", Content = "✕", FontSize = 10, Padding = new Thickness(4, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            del.Click += (_, _) => ProgramRun(PwKey, () => d.Edit("Balken entfernen", x => { if (x["boxes"] is JsonArray a && k < a.Count) a.RemoveAt(k); }));
            r.Child = del; Canvas.SetLeft(r, b.X * scale); Canvas.SetTop(r, b.YTop * scale); canvas.Children.Add(r); bi++;
        }
        Point? start = null; Rectangle? rubber = null;
        canvas.PointerPressed += (_, e) =>
        {
            if (e.Source is Button) return;
            start = e.GetPosition(canvas); rubber = new Rectangle { Stroke = Brush.Parse("#D4A84B"), StrokeThickness = 1.5, Fill = Brush.Parse("#66000000"), IsHitTestVisible = false };
            Canvas.SetLeft(rubber, start.Value.X); Canvas.SetTop(rubber, start.Value.Y); canvas.Children.Add(rubber); e.Pointer.Capture(canvas); e.Handled = true;
        };
        canvas.PointerMoved += (_, e) =>
        {
            if (start is not { } s || rubber is null) return;
            var p = e.GetPosition(canvas);
            Canvas.SetLeft(rubber, Math.Min(s.X, p.X)); Canvas.SetTop(rubber, Math.Min(s.Y, p.Y)); rubber.Width = Math.Abs(p.X - s.X); rubber.Height = Math.Abs(p.Y - s.Y);
        };
        canvas.PointerReleased += (_, e) =>
        {
            if (start is not { } s) return;
            var p = e.GetPosition(canvas); start = null; rubber = null; e.Pointer.Capture(null);
            double x0 = Math.Clamp(Math.Min(s.X, p.X), 0, canvas.Width), y0 = Math.Clamp(Math.Min(s.Y, p.Y), 0, canvas.Height), x1 = Math.Clamp(Math.Max(s.X, p.X), 0, canvas.Width), y1 = Math.Clamp(Math.Max(s.Y, p.Y), 0, canvas.Height);
            ProgramRun(PwKey, () => PwAddBox(d, page, x0 / scale, y0 / scale, (x1 - x0) / scale, (y1 - y0) / scale));
        };
        sp.Children.Add(new Border { BoxShadow = BoxShadows.Parse("0 4 14 0 #66000000"), HorizontalAlignment = HorizontalAlignment.Left, Child = canvas });
        return sp;
    }

    Control PwCompareView(PdfCompare c)
    {
        var sp = new StackPanel { Name = "pw-compare", Spacing = 2 };
        sp.Children.Add(H(c.Identical ? "Vergleich: Der Text ist gleich" : $"Vergleich: {c.Removed} Zeilen nur in A, {c.Added} nur in B, {c.Same} gleich", 14));
        foreach (var l in c.Lines.Take(600))
        {
            var (bg, fg, mark) = l.Op switch { '+' => ("#2216A34A", "#86EFAC", $"+ B S.{l.PageB}"), '−' => ("#22DC2626", "#FCA5A5", $"− A S.{l.PageA}"), _ => ("#00000000", "#9AA6B2", $"  S.{l.PageA}/{l.PageB}") };
            sp.Children.Add(new Border { Background = Brush.Parse(bg), Padding = new Thickness(6, 1), Child = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*"), Children = {
                new TextBlock { Text = mark, Foreground = Brush.Parse(fg), FontSize = 11, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace") },
                Pos(new TextBlock { Text = l.Text, Foreground = l.Op == ' ' ? Muted : Brush.Parse(fg), FontSize = 12, TextWrapping = TextWrapping.Wrap }, 1) } } });
        }
        return sp;
    }

    Control PwProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "pw-props", Spacing = 6 };
        var files = PwFiles(d).Where(f => !PwIsImage(f)).ToList();
        sp.Children.Add(H("Datei", 13));
        if (files.Count == 0) { sp.Children.Add(P("Noch keine PDF in der Liste.", Muted, 11)); }
        else
        {
            var f = files[0];
            try
            {
                var info = PdfTools.Read(f);
                sp.Children.Add(P($"{Path.GetFileName(f)}\n{(info.NeedsPassword ? "Geschützt – Passwort nötig" : $"{info.Pages} Seiten")} · {Kb(new FileInfo(f).Length)}" + (info.Encrypted && !info.NeedsPassword ? " · verschlüsselt" : ""), null, 11));
                if (!info.NeedsPassword)
                {
                    var (meta, xmp) = PdfTools.ReadMeta(f);
                    sp.Children.Add(H("Metadaten", 12));
                    if (meta.Count == 0 && !xmp) sp.Children.Add(P("Keine Angaben.", Muted, 11));
                    foreach (var (k, n) in PdfTools.MetaNames) if (meta.TryGetValue(k, out var v)) sp.Children.Add(P($"{n}: {v}", null, 11));
                    if (xmp) sp.Children.Add(P("Dazu XMP-Angaben (unsichtbar im Dokument).", Muted, 11));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PdfSharp.Pdf.IO.PdfReaderException or InvalidOperationException) { sp.Children.Add(P("Die Datei lässt sich nicht lesen: " + FriendlyError.Say(ex), Muted, 11)); }
        }
        sp.Children.Add(H("Gut zu wissen", 13));
        sp.Children.Add(P("• Jede Aktion schreibt eine neue Datei – das Original bleibt.\n• Schwärzen entfernt den Inhalt wirklich: die Seite wird dabei zum Bild (nicht mehr durchsuchbar).\n• Komprimieren misst ehrlich: wird es nicht kleiner, bekommst du eine Kopie und einen Hinweis.\n• Passwörter werden nirgends gespeichert.", Muted, 11));
        sp.Children.Add(H("Ergebnis-Ordner", 13));
        sp.Children.Add(P(PwOutDir(), Muted, 11));
        return sp;
    }
}
