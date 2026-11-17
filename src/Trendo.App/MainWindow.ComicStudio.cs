using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0697 (D4b): „Comics &amp; Storyboards“ als echtes Programm – Seiten mit frei gesetzten und verformbaren Bildfeldern, Zeichnen direkt im Bildfeld
/// (Mal-Kern des Künstlerstudios, Ebenen je Seite), Bilder einsetzen und zuschneiden, Sprechblasen und Textkästen frei mit Dorn, mehrere Seiten,
/// Ausgabe als PDF (Druck), PNG je Seite und CBZ; Storyboard-Ansicht mit Übergabe an „Filme machen“.</summary>
public partial class MainWindow
{
    /// <summary>Werkzeug auf der Seite: auswahl, feld, pinsel, radierer, blase.</summary>
    public string ComicTool { get; set; } = "auswahl";
    public (string Kind, string Id)? ComicSel { get; set; }
    public string ComicBrush { get; set; } = "tusche";
    public string ComicColor { get; set; } = "#111111";
    public double ComicSize { get; set; } = 6;
    /// <summary>Tests: KI-Antwort ersetzen (Prompt → Antwort).</summary>
    public Func<string, Task<string?>>? ComicKiOverride { get; set; }
    public ComicPageView? ComicViewForTests { get; private set; }
    readonly Dictionary<string, (DateTime When, Bitmap Bmp)> _comicImages = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (string Sig, PaintDoc Paint, WriteableBitmap? Bmp)> _comicArt = [];
    string? _comicLayer; bool _comicFocusText;

    static readonly string[] ComicImageExt = [".png", ".jpg", ".jpeg", ".webp"];

    static JsonObject ComicLegacy(string title, string layout, params (string Speech, string Caption)[] panels)
    {
        var old = new JsonObject
        {
            ["title"] = title, ["layout"] = layout,
            ["panels"] = new JsonArray([.. panels.Select(p => (JsonNode)new JsonObject { ["speech"] = p.Speech, ["caption"] = p.Caption, ["image"] = "" })]),
        };
        var j = new JsonObject(); ComicBook.Read(old).Write(j); j["page"] = 0; return j;
    }

    public static ComicBook ComicOf(ProgramDocument d) => ComicBook.Read(d.Data);
    public static int ComicPageOf(ProgramDocument d) => Math.Clamp((int)d.Number("page"), 0, Math.Max(0, ComicOf(d).Pages.Count - 1));

    /// <summary>Ein Bearbeitungsschritt am Heft (rückgängig machbar).</summary>
    void ComicEdit(ProgramDocument d, string label, Action<ComicBook, ComicPage> change)
    {
        var i = ComicPageOf(d);
        d.Edit(label, x => { var b = ComicBook.Read(x); change(b, b.Pages[Math.Clamp(i, 0, b.Pages.Count - 1)]); b.Write(x); });
    }

    static bool ComicEmpty(ProgramDocument d) => ComicOf(d).Pages.All(p => p.IsEmpty);

    private void InitComic() => RegisterProgram(FachTabs, new ProgramSpec(ComicKey, "STUDIO-COMIC-01", "Comics & Storyboards")
    {
        Sentence = "Comicseiten und Storyboards: Bildfelder frei ziehen, direkt hineinzeichnen, Bilder einsetzen, Sprechblasen setzen – als PDF, PNG oder CBZ.",
        Folder = "Comics",
        FillHeight = true,
        FirstStep = new("Erste Sprechblase setzen", "Setz eine Sprechblase und schreib hinein. Danach zeichnest du mit dem Pinsel (B) im Bildfeld oder ziehst ein Bild auf ein Feld.",
            d => { ComicAddBalloon(d, "sprech"); ProgramShowProperties(ComicKey); _comicFocusText = true; }, d => ComicEmpty(d) && ComicTool == "auswahl" && ComicSel is null),
        Templates =
        [
            new("leer", "Leere Seite", "Vier Bildfelder als Vorschlag – frei verschiebbar", () => ComicBook.NewDoc("", "comic", "2x2")),
            new("storyboard", "Storyboard", "Sechs Einstellungen im Format 16:9 mit Beschreibung", () => ComicBook.NewDoc("", "storyboard", "sb-6")),
            new("beispiel", "Beispiel: Der Schlüssel", "Kurze Geschichte in vier Bildern", () => ComicLegacy("Der Schlüssel", "2x2",
                ("Wo ist nur mein Schlüssel?", "Montag, 7:58 Uhr."), ("Nicht in der Jacke …", ""), ("Miau!", "Der Kater weiß mehr."), ("Danke, Kater. Wieder zu spät.", "8:15 Uhr."))),
        ],
        Tools =
        [
            new ProgramTool("comic-tool-auswahl", "↖", "Auswahl (V)", "Bildfelder und Blasen verschieben, Ecken ziehen verformt das Feld, der runde Griff richtet den Dorn. Alt + Ziehen verschiebt das Bild im Feld, Alt + Mausrad zoomt.", _ => ComicTool = "auswahl", () => ComicTool == "auswahl"),
            new ProgramTool("comic-tool-feld", "▭", "Bildfeld aufziehen (F)", "Mit der Maus ein neues Bildfeld aufziehen.", _ => ComicTool = "feld", () => ComicTool == "feld"),
            new ProgramTool("comic-tool-pinsel", "🖋", "Zeichnen (B)", "Direkt ins Bildfeld zeichnen – der Strich bleibt im Feld, in dem er beginnt.", _ => ComicTool = "pinsel", () => ComicTool == "pinsel"),
            new ProgramTool("comic-tool-radierer", "◌", "Radierer (E)", "Zeichnung wegradieren.", _ => ComicTool = "radierer", () => ComicTool == "radierer"),
            new ProgramTool("comic-tool-blase", "💬", "Sprechblase (T)", "Klick auf die Seite setzt eine Sprechblase an dieser Stelle.", _ => ComicTool = "blase", () => ComicTool == "blase"),
        ],
        Ribbon =
        [
            ("Einfügen", [
                new ProgramCmd("comic-balloon", "Sprechblase", d => ComicAddBalloon(d, "sprech"), Primary: true, Tip: "Neue Sprechblase im gewählten Bildfeld (T: an der Mausposition)"),
                new ProgramCmd("comic-thought", "Gedankenblase", d => ComicAddBalloon(d, "gedanke")),
                new ProgramCmd("comic-shout", "Ausruf", d => ComicAddBalloon(d, "schrei")),
                new ProgramCmd("comic-box", "Textkasten", d => ComicAddBalloon(d, "kasten"), Tip: "Erzähltext am unteren Rand des Bildfelds"),
                new ProgramCmd("comic-image", "Bild einsetzen …", d => _ = ComicPickImage(d), Tip: "PNG, JPEG oder WEBP ins gewählte Bildfeld – oder einfach auf ein Feld ziehen"),
            ]),
            ("Seite", [
                new ProgramCmd("comic-page-add", "Neue Seite", d => ComicAddPage(d), Tip: "Bild ↓ wechselt die Seite"),
                new ProgramCmd("comic-page-prev", "◀ Vorige", d => ComicGo(d, -1)),
                new ProgramCmd("comic-page-next", "Nächste ▶", d => ComicGo(d, 1)),
                new ProgramCmd("comic-page-del", "Seite löschen", d => ComicDeletePage(d), Level: 1),
            ]),
            ("Raster-Vorschlag", ComicBook.Layouts.Select(l => new ProgramCmd("comic-layout-" + l.Id, l.Label, d => ComicEdit(d, "Raster " + l.Label, (_, p) => ComicBook.ApplyLayout(p, l.Id)), Tip: "Ordnet die Bildfelder dieser Seite neu an – Bilder, Zeichnung und Blasen bleiben")).ToList()),
            ("Zeichnen", [
                new ProgramCmd("comic-layer-add", "Neue Ebene", d => ComicAddLayer(d), Tip: "Weitere Mal-Ebene auf dieser Seite (z. B. Farbe unter der Tusche)"),
            ]),
            ("KI", [
                new ProgramCmd("comic-claude", "Geschichte mit KI", d => _ = ComicKi(d), Tip: "Die KI schreibt zu deinem Titel Sprechblasen und Erzähltexte direkt in die Bildfelder dieser Seite. Rückgängig holt den alten Stand zurück."),
            ]),
            ("Storyboard", [
                new ProgramCmd("comic-mode", "Storyboard-Ansicht", d => d.Edit("Ansicht gewechselt", x => x["mode"] = ComicOf(d).Storyboard ? "comic" : "storyboard"), Tip: "Bildfeld = Einstellung mit Beschreibung, Kamera und Dauer"),
                new ProgramCmd("comic-to-film", "An „Filme machen“ übergeben", d => _ = ComicToFilm(d), Tip: "Jedes Bildfeld wird eine Einstellung im Programm „Filme machen“ (als Bild)"),
            ]),
        ],
        GroupLevels = new() { ["Zeichnen"] = 1, ["Storyboard"] = 1 },
        Exports =
        [
            new("pdf", "Heft als PDF (Druck)", ".pdf", (d, f) => { ComicExport.Pdf(ComicOf(d), f); return f; }),
            new("png", "Seiten als PNG", ".png", ComicPngFiles),
            new("cbz", "Comic-Archiv (.cbz)", ".cbz", (d, f) => { ComicExport.Cbz(ComicPngs(d), f, ComicOf(d).Title.Length > 0 ? ComicOf(d).Title : d.Title); return f; }),
            new("frames", "Bildfelder als PNG-Folge", ".png", ComicFrameFiles),
            new("svg", "Diese Seite (.svg)", ".svg", (d, f) => { File.WriteAllText(f, ComicBook.Svg(ComicOf(d), ComicPageOf(d))); return f; }),
            new("md", "Storyboard-Liste (.md)", ".md", (d, f) => { File.WriteAllText(f, ComicShotList(d)); return f; }),
        ],
        SimpleExports = 4, // T-0704: Heft (PDF), Seiten (PNG), Comic-Archiv (CBZ) und Bildfelder gehören zum Fach – SVG und Storyboard-Liste ab „Erweitert“
        Workspace = ComicWorkspace,
        Properties = ComicProperties,
        Status = d => { var b = ComicOf(d); var p = b.Pages[ComicPageOf(d)]; return $"Seite {ComicPageOf(d) + 1} von {b.Pages.Count} · {p.Panels.Count} Bildfelder · {p.Balloons.Count} Blasen/Texte" + (p.HasArt ? " · mit Zeichnung" : ""); },
        Help = "Links das Werkzeug wählen: Auswahl (V) verschiebt Bildfelder und Blasen, an den Ecken verformst du ein Feld, am runden Griff richtest du den Dorn. Bildfeld (F) aufziehen, Zeichnen (B) und Radierer (E) arbeiten im Feld, Sprechblase (T) setzt eine Blase. Bilder ziehst du einfach auf ein Feld. Rechtsklick zeigt alles, was mit dem Feld oder der Blase geht. Entf löscht, Bild ↑/↓ wechselt die Seite.",
    }, "comic", "comics", "storyboard", "manga", "bildergeschichte", "sprechblase", "panel", "graphic novel");

    // ---------- Befehle ----------
    public void ComicAddBalloon(ProgramDocument d, string kind, double? atX = null, double? atY = null)
    {
        string id = "";
        ComicEdit(d, ComicBook.Kinds.First(k => k.Id == kind).Name + " gesetzt", (_, p) =>
        {
            var panel = (ComicSel is ("panel", var pid) ? p.Panels.FirstOrDefault(x => x.Id == pid) : null) ?? p.Panels.FirstOrDefault();
            var (x, y, w, h) = panel?.Bounds() ?? (ComicBook.Margin, ComicBook.Margin, 400, 400);
            if (atX is { } ax && atY is { } ay) { x = ax - 130; y = ay - 55; w = 300; h = 300; }
            var b = kind == "kasten" ? ComicBalloon.Make("kasten", x, y + h - 52, Math.Min(w, 600), "Erzähltext …")
                : ComicBalloon.Make(kind, x + 20, y + 20, Math.Min(w - 40, 280), kind == "schrei" ? "Wow!" : "Text …", x + Math.Min(w * 0.5, 180), y + Math.Min(h * 0.7, 240));
            b.X = Math.Clamp(b.X, 0, ComicBook.PageW - b.W); b.Y = Math.Clamp(b.Y, 0, ComicBook.PageH - b.H);
            b.Fit(); p.Balloons.Add(b); id = b.Id;
        });
        ComicSel = ("balloon", id); ComicTool = "auswahl"; _comicFocusText = true;
    }

    void ComicAddPage(ProgramDocument d)
    {
        var b = ComicOf(d);
        d.Edit("Neue Seite", x => { var bb = ComicBook.Read(x); bb.Pages.Insert(ComicPageOf(d) + 1, ComicPage.Grid(bb.Storyboard ? "sb-6" : "2x2")); bb.Write(x); x["page"] = ComicPageOf(d) + 1; });
        ComicSel = null; _progMsg[ComicKey] = $"Seite {ComicPageOf(d) + 1} von {b.Pages.Count + 1} angelegt.";
    }

    public void ComicGo(ProgramDocument d, int by)
    {
        var n = ComicOf(d).Pages.Count; var to = Math.Clamp(ComicPageOf(d) + by, 0, n - 1);
        if (to == ComicPageOf(d)) return;
        d.Edit($"Seite {to + 1}", x => x["page"] = to); ComicSel = null;
    }

    void ComicDeletePage(ProgramDocument d)
    {
        if (ComicOf(d).Pages.Count <= 1) throw new InvalidOperationException("Die letzte Seite bleibt – lösche lieber ihren Inhalt.");
        var i = ComicPageOf(d);
        d.Edit($"Seite {i + 1} gelöscht", x => { var b = ComicBook.Read(x); b.Pages.RemoveAt(i); b.Write(x); x["page"] = Math.Max(0, i - 1); });
        ComicSel = null;
    }

    public void ComicDeleteSelected(ProgramDocument d)
    {
        if (ComicSel is not { } s) return;
        ComicEdit(d, s.Kind == "panel" ? "Bildfeld gelöscht" : "Blase gelöscht", (_, p) => { if (s.Kind == "panel") p.Panels.RemoveAll(x => x.Id == s.Id); else p.Balloons.RemoveAll(x => x.Id == s.Id); });
        ComicSel = null;
    }

    void ComicDuplicate(ProgramDocument d)
    {
        if (ComicSel is not { } s) return; string id = "";
        ComicEdit(d, "Dupliziert", (_, p) =>
        {
            if (s.Kind == "panel" && p.Panels.FirstOrDefault(x => x.Id == s.Id) is { } f) { var c = ComicPanel.FromJson(f.ToJson()); c.Id = "f" + Guid.NewGuid().ToString("N")[..8]; c.Move(30, 30); p.Panels.Add(c); id = c.Id; }
            if (s.Kind == "balloon" && p.Balloons.FirstOrDefault(x => x.Id == s.Id) is { } b) { var c = ComicBalloon.FromJson(b.ToJson()); c.Id = "b" + Guid.NewGuid().ToString("N")[..8]; c.Move(30, 30); p.Balloons.Add(c); id = c.Id; }
        });
        if (id.Length > 0) ComicSel = (s.Kind, id);
    }

    void ComicNudge(ProgramDocument d, double dx, double dy)
    {
        if (ComicSel is not { } s) return;
        ComicEdit(d, "Verschoben", (_, p) => { if (s.Kind == "panel") p.Panels.FirstOrDefault(x => x.Id == s.Id)?.Move(dx, dy); else p.Balloons.FirstOrDefault(x => x.Id == s.Id)?.Move(dx, dy); });
    }

    /// <summary>Bild in ein Bildfeld setzen (Dateiauswahl, Ziehen und Ablegen, Rechtsklick). Ohne Feld: das gewählte, sonst das erste ohne Bild.</summary>
    public void ComicSetImage(ProgramDocument d, string file, string? panelId = null)
    {
        if (!ComicImageExt.Contains(Path.GetExtension(file).ToLowerInvariant())) throw new InvalidOperationException("Das ist kein Bild. Es gehen PNG, JPEG und WEBP.");
        if (!KreativSafe.LocalFile(file, KreativSafe.MaxImageBytes)) throw new InvalidOperationException("Das Bild liegt nicht auf diesem Rechner oder ist größer als 25 MB.");
        var p0 = ComicOf(d).Pages[ComicPageOf(d)];
        var target = panelId ?? (ComicSel is ("panel", var sid) ? sid : null) ?? p0.Panels.FirstOrDefault(p => p.Image.Length == 0)?.Id ?? p0.Panels.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("Auf dieser Seite gibt es noch kein Bildfeld – zieh erst eines auf (F).");
        ComicEdit(d, "Bild eingesetzt", (_, p) => { if (p.Panels.FirstOrDefault(x => x.Id == target) is { } f) { f.Image = file; f.Zoom = 1; f.Ox = f.Oy = 0; } });
        ComicSel = ("panel", target);
        _progMsg[ComicKey] = $"„{Path.GetFileName(file)}“ eingesetzt – Alt + Ziehen verschiebt den Ausschnitt, Alt + Mausrad zoomt.";
    }

    async Task ComicPickImage(ProgramDocument d, string? panelId = null)
    {
        if (await PickMediaFile("comic-bild", "Bild (PNG, JPEG, WEBP)", ["*.png", "*.jpg", "*.jpeg", "*.webp"]) is { } f)
            ProgramRun(ComicKey, () => ComicSetImage(d, f, panelId));
    }

    void ComicAddLayer(ProgramDocument d)
    {
        string id = "";
        ComicEdit(d, "Neue Ebene", (_, p) => { var paint = p.Paint(); var l = paint.Add($"Ebene {paint.Layers.Count + 1}"); id = l.Id; var j = paint.ToJson(); j["active"] = id; p.Art = j; });
        _comicLayer = id; ComicTool = "pinsel";
    }

    /// <summary>KI schreibt die Geschichte in die Bildfelder dieser Seite (ein Schritt, rückgängig machbar).</summary>
    public async Task<bool> ComicKi(ProgramDocument d)
    {
        var page = ComicOf(d).Pages[ComicPageOf(d)];
        var n = Math.Max(1, page.Panels.Count); var title = ComicOf(d).Title.Trim().Length > 0 ? ComicOf(d).Title.Trim() : d.Title;
        var prompt = $"Schreib eine kurze Comic-Geschichte auf Deutsch in genau {n} Bildern. Antworte nur mit {n} Zeilen der Form „Bild N: Sprechblase | Bildunterschrift“ (Sprechblase höchstens 12 Wörter). Thema: {title}";
        _progMsg[ComicKey] = "Die KI schreibt die Geschichte …"; Show(ComicKey);
        try
        {
            string? answer;
            if (ComicKiOverride is { } fake) answer = await fake(prompt);
            else if (_opt.Isolated && !_kiConnected) { _progMsg[ComicKey] = "Im abgeschotteten Testmodus wird keine KI gefragt."; Show(ComicKey); return false; }
            else { using var label = KiActivityHub.Label("Comic-Geschichte"); answer = await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : null; }
            if (answer is null) { _progMsg[ComicKey] = "Gerade ist keine KI erreichbar. Richte unter „KI“ ein lokales Modell ein oder verbinde Claude – an der Seite wurde nichts geändert."; Show(ComicKey); return false; }
            var got = 0;
            var probe = ComicPage.FromJson(page.ToJson()); if (ComicBook.AdoptStory(probe, answer) == 0) { _progMsg[ComicKey] = "Die Antwort der KI hatte keine Bilder („Bild 1: …“) – nichts geändert."; Show(ComicKey); return false; }
            ComicEdit(d, "Geschichte von der KI", (_, p) => got = ComicBook.AdoptStory(p, answer));
            _core.Audit.Append(Principal.LocalOwner.Name, "comic.ki", $"{got} Bildfelder");
            _progMsg[ComicKey] = $"Die KI hat {got} Bildfelder beschriftet. Blasen frei verschieben und ändern – „Rückgängig“ holt den alten Stand zurück.";
            Show(ComicKey); return true;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or TaskCanceledException) { _progMsg[ComicKey] = "Die KI hat nicht geantwortet: " + e.Message; Show(ComicKey); return false; }
    }

    // ---------- Ausgabe ----------
    Bitmap? ComicImage(string path)
    {
        if (!KreativSafe.LocalFile(path, KreativSafe.MaxImageBytes)) return null;
        var when = File.GetLastWriteTimeUtc(path);
        if (_comicImages.TryGetValue(path, out var c) && c.When == when) return c.Bmp;
        try { var b = new Bitmap(path); _comicImages[path] = (when, b); return b; }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { return null; }
    }

    /// <summary>Mal-Ebenen einer Seite (zwischengespeichert, solange sich die Ebenen nicht ändern).</summary>
    (PaintDoc Paint, WriteableBitmap? Bmp) ComicArtOf(ComicPage p)
    {
        var json = p.Art?.ToJsonString() ?? ""; var sig = json.Length + ":" + json.GetHashCode();
        if (_comicArt.TryGetValue(p.Id, out var c) && c.Sig == sig) return (c.Paint, c.Bmp);
        var paint = p.Paint(); var bmp = ComicDraw.Art(paint);
        _comicArt[p.Id] = (p.Art is null ? "" : sig, paint, bmp);
        return (paint, bmp);
    }

    /// <summary>Seiten als PNG: erst das Druck-PDF, dann jede Seite über PDFium in Bildpunkte (Standard 242 dpi = 2000 Punkte breit) – so sehen PNG, CBZ und PDF gleich aus.</summary>
    public List<byte[]> ComicPngs(ProgramDocument d, int dpi = 242)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-comic-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var n = ComicExport.Pdf(ComicOf(d), tmp); var list = new List<byte[]>();
            for (var i = 0; i < n; i++)
            {
                using var bmp = PdfView.RenderSk(tmp, i, dpi);
                using var data = bmp.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                list.Add(data.ToArray());
            }
            return list;
        }
        finally { try { File.Delete(tmp); } catch (IOException) { } }
    }

    /// <summary>PNG je Seite: name.png, name-2.png …; liefert die erste Datei.</summary>
    string ComicPngFiles(ProgramDocument d, string file)
    {
        var pngs = ComicPngs(d); var dir = Path.GetDirectoryName(Path.GetFullPath(file))!; var stem = Path.GetFileNameWithoutExtension(file);
        for (var i = 0; i < pngs.Count; i++) AtomicFile.WriteAllBytes(i == 0 ? file : Path.Combine(dir, $"{stem}-{i + 1}.png"), pngs[i]);
        return file;
    }

    /// <summary>Jedes Bildfeld als eigenes PNG (Storyboard-Einstellungen der Reihe nach): name-01.png …</summary>
    public List<string> ComicFrames(ProgramDocument d, string dir, string stem)
    {
        var b = ComicOf(d); var files = new List<string>(); Directory.CreateDirectory(dir); var n = 0; var pngs = ComicPngs(d, 180);
        for (var i = 0; i < b.Pages.Count; i++)
        {
            var page = PngCodec.Decode(pngs[i]); var scale = page.W / ComicBook.PageW;
            foreach (var p in b.Pages[i].Panels)
            {
                var f = Path.Combine(dir, $"{stem}-{++n:00}.png");
                AtomicFile.WriteAllBytes(f, PngCodec.Encode(ComicExport.Crop(page, p, scale))); files.Add(f);
            }
        }
        return files;
    }

    string ComicFrameFiles(ProgramDocument d, string file)
    {
        var files = ComicFrames(d, Path.GetDirectoryName(Path.GetFullPath(file))!, Path.GetFileNameWithoutExtension(file));
        return files.Count > 0 ? files[0] : throw new InvalidOperationException("Das Heft hat noch kein Bildfeld.");
    }

    static string ComicShotList(ProgramDocument d)
    {
        var b = ComicOf(d); var sb = new System.Text.StringBuilder($"# {(b.Title.Length > 0 ? b.Title : d.Title)}\n\n"); var n = 0;
        foreach (var (p, pi) in b.Pages.Select((p, i) => (p, i)))
        {
            sb.Append($"## Seite {pi + 1}\n\n");
            foreach (var f in p.Panels)
            {
                var (x, y, w, h) = f.Bounds();
                var texts = p.Balloons.Where(bl => bl.X + bl.W / 2 >= x && bl.X + bl.W / 2 <= x + w && bl.Y + bl.H / 2 >= y && bl.Y + bl.H / 2 <= y + h).Select(bl => bl.Text.Trim()).Where(t => t.Length > 0);
                sb.Append($"{++n}. {f.ShotHead(n - 1)}{(f.Shot.Trim().Length > 0 ? " — " + f.Shot.Trim() : "")}{(texts.Any() ? " — „" + string.Join("“ „", texts) + "“" : "")}\n");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Storyboard an „Filme machen“: jedes Bildfeld wird ein Bild und damit eine Einstellung im neuen Film-Dokument.</summary>
    public async Task<ProgramDocument?> ComicToFilm(ProgramDocument d)
    {
        try
        {
            var name = string.Join("_", (ComicOf(d).Title.Length > 0 ? ComicOf(d).Title : d.Title).Split(Path.GetInvalidFileNameChars())).Trim();
            var dir = Path.Combine(WorkFolder("Comics"), (name.Length > 0 ? name : "Storyboard") + " – Einstellungen");
            var files = ComicFrames(d, dir, "einstellung");
            if (files.Count == 0) { _progMsg[ComicKey] = "Das Storyboard hat noch kein Bildfeld."; Show(ComicKey); return null; }
            var film = ProgramNew(FilmKey, "leer");
            var got = await FilmImport(film, files);
            _progMsg[ComicKey] = got > 0 ? $"{got} Einstellungen an „Filme machen“ übergeben (Bilder in {dir})." : $"Die Bilder liegen in {dir} – „Filme machen“ hat sie nicht übernommen.";
            if (got > 0) Show(FilmKey); else Show(ComicKey);
            return film;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException or InvalidDataException) { _progMsg[ComicKey] = e.Message; Show(ComicKey); return null; }
    }

    // ---------- Arbeitsfläche ----------
    Control ComicWorkspace(ProgramDocument d)
    {
        var book = ComicOf(d); var pi = ComicPageOf(d); var page = book.Pages[pi];
        if (ComicSel is { } s && !(s.Kind == "panel" ? page.Panels.Any(p => p.Id == s.Id) : page.Balloons.Any(b => b.Id == s.Id))) ComicSel = null;
        var (paint, art) = ComicArtOf(page);
        if (_comicLayer is null || paint.Layers.All(l => l.Id != _comicLayer)) _comicLayer = (page.Art?["active"] as JsonValue)?.ToString() is { } a && paint.Layers.Any(l => l.Id == a) ? a : paint.Layers[^1].Id;
        var view = new ComicPageView
        {
            Name = "comic-view", Book = book, PageIndex = pi, Tool = ComicTool, Selected = ComicSel, Image = ComicImage, Paint = paint, ArtBmp = art, ActiveLayer = _comicLayer,
            BrushId = ComicBrush, BrushColor = ComicColor, BrushSize = ComicSize, MinHeight = 240,
            Cursor = new Cursor(ComicTool switch { "pinsel" or "radierer" or "feld" => StandardCursorType.Cross, "blase" => StandardCursorType.Ibeam, _ => StandardCursorType.Arrow }),
        };
        ComicViewForTests = view;
        view.SelectionChanged = sel => { ComicSel = sel; Dispatcher.UIThread.Post(() => Show(ComicKey), DispatcherPriority.Background); };
        view.Changed = label => ProgramRun(ComicKey, () => d.Edit(label, x => { book.Write(x); }));
        view.Stroked = layer => ProgramRun(ComicKey, () =>
        {
            d.Edit(ComicTool == "radierer" ? "Radiert" : "Gezeichnet", x =>
            {
                var b = ComicBook.Read(x); var p = b.Pages[pi];
                var j = paint.ToJson(); j["active"] = layer.Id; p.Art = j; b.Write(x);
            });
            var np = ComicOf(d).Pages[pi]; var json = np.Art?.ToJsonString() ?? "";
            _comicArt[np.Id] = (json.Length + ":" + json.GetHashCode(), paint, view.ArtBmp); // kein erneutes Entpacken
        });
        view.PanelDrawn = (x, y, w, h) => ProgramRun(ComicKey, () => { string id = ""; ComicEdit(d, "Bildfeld aufgezogen", (_, p) => { var f = ComicPanel.Rect(x, y, w, h); p.Panels.Add(f); id = f.Id; }); ComicSel = ("panel", id); });
        view.BalloonAt = (x, y) => ProgramRun(ComicKey, () => ComicAddBalloon(d, "sprech", x, y));
        view.EditText = b => { ComicSel = ("balloon", b.Id); ProgramShowProperties(ComicKey); _comicFocusText = true; Show(ComicKey); };
        view.MenuAt = (x, y) => ComicMenu(d, x, y);
        view.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        view.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];
            if (files.Count == 0) return;
            e.Handled = true;
            var (x, y) = view.ToPage(e.GetPosition(view)); var hit = ComicBook.HitPanel(page, x, y);
            ProgramRun(ComicKey, () => ComicDrop(d, files, hit >= 0 ? page.Panels[hit].Id : null));
        });
        view.KeyDown += (_, e) => { if (ComicKey_(d, e)) e.Handled = true; };

        // Seitenleiste oben: Seiten wählen, neue Seite
        var strip = new WrapPanel { Name = "comic-pages", Orientation = Orientation.Horizontal };
        for (var i = 0; i < book.Pages.Count; i++)
        {
            var k = i; var b = new Button { Name = $"comic-page-btn-{i}", Content = $"Seite {i + 1}", Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 0), FontSize = 12 };
            if (i == pi) { b.BorderBrush = Accent; b.BorderThickness = new Thickness(1.5); }
            b.Click += (_, _) => ProgramRun(ComicKey, () => ComicGo(d, k - ComicPageOf(d)));
            strip.Children.Add(b);
        }
        var add = new Button { Name = "comic-page-plus", Content = "+ Seite", Padding = new Thickness(10, 3), FontSize = 12 };
        add.Click += (_, _) => ProgramRun(ComicKey, () => ComicAddPage(d));
        strip.Children.Add(add);
        var hint = new TextBlock { Text = ComicTool switch
        {
            "pinsel" => SimpleCollapsed(ComicKey) ? "Zeichnen: Strich beginnt im Bildfeld und bleibt darin. Pinsel, Farbe und Größe oben." : "Zeichnen: Strich beginnt im Bildfeld und bleibt darin. Farbe, Größe und Ebenen rechts.",
            "radierer" => "Radierer: über die Zeichnung fahren.",
            "feld" => "Bildfeld: mit der Maus ein Rechteck aufziehen.",
            "blase" => "Sprechblase: auf die Stelle klicken.",
            _ => "Ziehen verschiebt · Ecken verformen · runder Griff richtet den Dorn · Doppelklick: Text · Rechtsklick: mehr",
        }, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 0, 0, 6), Children = { strip, Pos(hint, 1) } };
        var frame = new Border { Name = "comic-page", CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = view };
        var dock = new DockPanel { LastChildFill = true }; DockPanel.SetDock(top, Avalonia.Controls.Dock.Top); dock.Children.Add(top);
        if (SimpleCollapsed(ComicKey)) { var bar = ComicDrawBar(d, page); DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); dock.Children.Add(bar); } // T-0704: Zeichnen auch in „Einfach“
        dock.Children.Add(frame);
        Dispatcher.UIThread.Post(() => { if (view.IsAttachedToVisualTree() && !_comicFocusText) view.Focus(); }, DispatcherPriority.Background);
        return dock;
    }

    void ComicDrop(ProgramDocument d, List<string> files, string? panelId)
    {
        var imgs = files.Where(f => ComicImageExt.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
        if (imgs.Count == 0) throw new InvalidOperationException("Das ist kein Bild. Es gehen PNG, JPEG und WEBP.");
        ComicSetImage(d, imgs[0], panelId);
        // weitere Bilder füllen die nächsten leeren Felder
        foreach (var f in imgs.Skip(1)) if (ComicOf(d).Pages[ComicPageOf(d)].Panels.FirstOrDefault(p => p.Image.Length == 0) is { } free) ComicSetImage(d, f, free.Id);
    }

    /// <summary>Tastenkürzel der Seite.</summary>
    bool ComicKey_(ProgramDocument d, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control); var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        void Tool(string t) { ComicTool = t; Show(ComicKey); }
        switch (e.Key)
        {
            case Key.Delete or Key.Back when ComicSel is not null: ProgramRun(ComicKey, () => ComicDeleteSelected(d)); return true;
            case Key.D when ctrl: ProgramRun(ComicKey, () => ComicDuplicate(d)); return true;
            case Key.V when !ctrl: Tool("auswahl"); return true;
            case Key.F when !ctrl: Tool("feld"); return true;
            case Key.B when !ctrl: Tool("pinsel"); return true;
            case Key.E when !ctrl: Tool("radierer"); return true;
            case Key.T when !ctrl: Tool("blase"); return true;
            case Key.PageDown: ProgramRun(ComicKey, () => ComicGo(d, 1)); return true;
            case Key.PageUp: ProgramRun(ComicKey, () => ComicGo(d, -1)); return true;
            case Key.Escape: ComicSel = null; ComicTool = "auswahl"; Show(ComicKey); return true;
            case Key.F2 or Key.Enter when ComicSel is ("balloon", _): ProgramShowProperties(ComicKey); _comicFocusText = true; Show(ComicKey); return true;
            case Key.Left when ComicSel is not null: ProgramRun(ComicKey, () => ComicNudge(d, -step, 0)); return true;
            case Key.Right when ComicSel is not null: ProgramRun(ComicKey, () => ComicNudge(d, step, 0)); return true;
            case Key.Up when ComicSel is not null: ProgramRun(ComicKey, () => ComicNudge(d, 0, -step)); return true;
            case Key.Down when ComicSel is not null: ProgramRun(ComicKey, () => ComicNudge(d, 0, step)); return true;
        }
        return false;
    }

    /// <summary>Rechtsklick: was mit Blase, Bildfeld oder leerer Stelle geht.</summary>
    ContextMenu ComicMenu(ProgramDocument d, double x, double y)
    {
        MenuItem Item(string name, string header, Action run, bool on = true) { var m = new MenuItem { Name = name, Header = header, IsEnabled = on }; m.Click += (_, _) => ProgramRun(ComicKey, run); return m; }
        var page = ComicOf(d).Pages[ComicPageOf(d)]; var menu = new ContextMenu { Name = "comic-menu" };
        var bi = ComicBook.HitBalloon(page, x, y); var pi = ComicBook.HitPanel(page, x, y);
        if (bi >= 0)
        {
            var b = page.Balloons[bi];
            menu.Items.Add(Item("comic-menu-text", "Text bearbeiten (F2)", () => { ComicSel = ("balloon", b.Id); ProgramShowProperties(ComicKey); _comicFocusText = true; }));
            foreach (var (id, name) in ComicBook.Kinds.Where(k => k.Id != b.Kind))
                menu.Items.Add(Item("comic-menu-kind-" + id, "Als " + name, () => ComicEdit(d, "Art: " + name, (_, p) => { if (p.Balloons.FirstOrDefault(z => z.Id == b.Id) is { } t) { t.Kind = id; t.Fit(); } })));
            menu.Items.Add(Item("comic-menu-front", "Nach vorn", () => ComicEdit(d, "Nach vorn", (_, p) => { var t = p.Balloons.First(z => z.Id == b.Id); p.Balloons.Remove(t); p.Balloons.Add(t); })));
            menu.Items.Add(Item("comic-menu-dup", "Duplizieren (Strg+D)", () => { ComicSel = ("balloon", b.Id); ComicDuplicate(d); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("comic-menu-delete", "Löschen (Entf)", () => { ComicSel = ("balloon", b.Id); ComicDeleteSelected(d); }));
        }
        else if (pi >= 0)
        {
            var f = page.Panels[pi];
            menu.Items.Add(Item("comic-menu-image", "Bild einsetzen …", () => _ = ComicPickImage(d, f.Id)));
            menu.Items.Add(Item("comic-menu-image-off", "Bild entfernen", () => ComicEdit(d, "Bild entfernt", (_, p) => { if (p.Panels.FirstOrDefault(z => z.Id == f.Id) is { } t) t.Image = ""; }), f.Image.Length > 0));
            menu.Items.Add(Item("comic-menu-crop-reset", "Ausschnitt zurücksetzen", () => ComicEdit(d, "Ausschnitt zurückgesetzt", (_, p) => { if (p.Panels.FirstOrDefault(z => z.Id == f.Id) is { } t) { t.Zoom = 1; t.Ox = t.Oy = 0; } }), f.Image.Length > 0));
            menu.Items.Add(Item("comic-menu-balloon", "Sprechblase hier", () => ComicAddBalloon(d, "sprech", x, y)));
            menu.Items.Add(Item("comic-menu-draw", "Hier zeichnen (B)", () => ComicTool = "pinsel"));
            menu.Items.Add(Item("comic-menu-dup", "Duplizieren (Strg+D)", () => { ComicSel = ("panel", f.Id); ComicDuplicate(d); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("comic-menu-delete", "Bildfeld löschen (Entf)", () => { ComicSel = ("panel", f.Id); ComicDeleteSelected(d); }));
        }
        else
        {
            menu.Items.Add(Item("comic-menu-balloon", "Sprechblase hier", () => ComicAddBalloon(d, "sprech", x, y)));
            menu.Items.Add(Item("comic-menu-box", "Textkasten hier", () => ComicAddBalloon(d, "kasten", x, y)));
            menu.Items.Add(Item("comic-menu-panel", "Bildfeld hier", () => ComicEdit(d, "Bildfeld", (_, p) => p.Panels.Add(ComicPanel.Rect(Math.Clamp(x - 150, 0, ComicBook.PageW - 300), Math.Clamp(y - 100, 0, ComicBook.PageH - 200), 300, 200)))));
            menu.Items.Add(Item("comic-menu-page", "Neue Seite", () => ComicAddPage(d)));
        }
        return menu;
    }

    // ---------- Eigenschaften ----------
    Control ComicProperties(ProgramDocument d)
    {
        var book = ComicOf(d); var pi = ComicPageOf(d); var page = book.Pages[pi];
        var sp = new StackPanel { Spacing = 4 };
        TextBox Field(string name, string value, string label, Action<string> commit, bool multi = false)
        {
            var t = new TextBox { Name = name, Text = value, PlaceholderText = label, TextWrapping = TextWrapping.Wrap, AcceptsReturn = multi };
            t.LostFocus += (_, _) => { if ((t.Text ?? "") != value) ProgramRun(ComicKey, () => commit(t.Text ?? "")); };
            return t;
        }
        sp.Children.Add(P("Titel des Hefts", null, 11));
        sp.Children.Add(Field("comic-p-title", book.Title, "Titel", v => d.Edit("Titel", x => x["title"] = v)));

        TextBox? focus = null;
        if (ComicSel is ("balloon", var bid) && page.Balloons.FirstOrDefault(b => b.Id == bid) is { } sb)
        {
            sp.Children.Add(H(ComicBook.Kinds.First(k => k.Id == sb.Kind).Name, 13));
            var kinds = new WrapPanel();
            foreach (var (id, name) in ComicBook.Kinds)
            {
                var b = new Button { Name = "comic-kind-" + id, Content = name, Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 4, 4), FontSize = 11.5 };
                if (id == sb.Kind) { b.BorderBrush = Accent; b.BorderThickness = new Thickness(1.5); }
                b.Click += (_, _) => ProgramRun(ComicKey, () => ComicEdit(d, "Art: " + name, (_, p) => { var t = p.Balloons.First(z => z.Id == bid); t.Kind = id; t.Fit(); }));
                kinds.Children.Add(b);
            }
            sp.Children.Add(kinds);
            focus = Field("comic-sel-text", sb.Text, "Text der Blase", v => ComicEdit(d, "Text geändert", (_, p) => { var t = p.Balloons.First(z => z.Id == bid); t.Text = v; t.Fit(); }), true);
            focus.MinHeight = 70; sp.Children.Add(focus);
            var size = new Slider { Name = "comic-sel-size", Minimum = 10, Maximum = 60, Value = sb.Size };
            size.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && Math.Abs(size.Value - sb.Size) > 0.5) { var v = Math.Round(size.Value); ProgramRun(ComicKey, () => ComicEdit(d, "Schriftgröße", (_, p) => { var t = p.Balloons.First(z => z.Id == bid); t.Size = v; t.Fit(); })); } };
            sp.Children.Add(P($"Schriftgröße {sb.Size:0}", null, 11)); sp.Children.Add(size);
        }
        else if (ComicSel is ("panel", var fid) && page.Panels.FirstOrDefault(p => p.Id == fid) is { } sf)
        {
            sp.Children.Add(H($"Bildfeld {page.Panels.IndexOf(sf) + 1}", 13));
            var pick = new Button { Name = "comic-image-sel", Content = sf.Image.Length > 0 ? "Bild: " + Path.GetFileName(sf.Image) : "Bild wählen …", Padding = new Thickness(10, 4) };
            pick.Click += (_, _) => _ = ComicPickImage(d, fid);
            sp.Children.Add(pick);
            if (sf.Image.Length > 0)
            {
                var zoom = new Slider { Name = "comic-sel-zoom", Minimum = 1, Maximum = 4, Value = sf.Zoom };
                zoom.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && Math.Abs(zoom.Value - sf.Zoom) > 0.02) { var v = zoom.Value; ProgramRun(ComicKey, () => ComicEdit(d, "Bildausschnitt gezoomt", (_, p) => p.Panels.First(z => z.Id == fid).Zoom = v)); } };
                sp.Children.Add(P("Ausschnitt (Zoom) – Alt + Ziehen verschiebt das Bild im Feld", null, 11)); sp.Children.Add(zoom);
            }
            if (book.Storyboard)
            {
                sp.Children.Add(P("Einstellung: was passiert?", null, 11));
                sp.Children.Add(Field("comic-shot", sf.Shot, "Beschreibung", v => ComicEdit(d, "Einstellung beschrieben", (_, p) => p.Panels.First(z => z.Id == fid).Shot = v), true));
                sp.Children.Add(Field("comic-camera", sf.Camera, "Kamera (z. B. Totale, Nah, Schwenk)", v => ComicEdit(d, "Kamera", (_, p) => p.Panels.First(z => z.Id == fid).Camera = v)));
                sp.Children.Add(Field("comic-seconds", sf.Seconds.ToString("0.#", De), "Dauer in Sekunden", v => { if (double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s)) ComicEdit(d, "Dauer", (_, p) => p.Panels.First(z => z.Id == fid).Seconds = Math.Clamp(s, 0.2, 600)); }));
            }
        }

        // Zeichnen: Pinsel, Farbe, Größe, Ebenen
        sp.Children.Add(H("Zeichnen", 13));
        sp.Children.Add(ComicBrushButtons());
        foreach (var c in ComicColorControls()) sp.Children.Add(c);
        sp.Children.Add(P("Strichstärke", null, 11)); sp.Children.Add(ComicSizeSlider());
        sp.Children.Add(P($"Ebenen auf Seite {pi + 1} (oberste zuletzt)", null, 11));
        foreach (var row in ComicLayerRows(d, page)) sp.Children.Add(row);

        // alle Texte der Seite zum schnellen Schreiben
        if (page.Balloons.Count > 0)
        {
            sp.Children.Add(H("Texte auf dieser Seite", 13));
            for (var i = 0; i < page.Balloons.Count; i++)
            {
                var id = page.Balloons[i].Id;
                var t = Field($"comic-speech-{i}", page.Balloons[i].Text, ComicBook.Kinds.First(k => k.Id == page.Balloons[i].Kind).Name, v => ComicEdit(d, "Text geändert", (_, p) => { var b = p.Balloons.First(z => z.Id == id); b.Text = v; b.Fit(); }), true);
                t.GotFocus += (_, _) => { if (ComicSel is not ("balloon", var s) || s != id) { ComicSel = ("balloon", id); if (ComicViewForTests is { } v) { v.Selected = ComicSel; v.InvalidateVisual(); } } };
                sp.Children.Add(t);
                if (focus is null && ComicSel is ("balloon", var sel) && sel == id) focus = t;
            }
        }
        if (_comicFocusText && focus is not null)
        {
            _comicFocusText = false; var f = focus;
            Dispatcher.UIThread.Post(() => { if (f.IsAttachedToVisualTree()) { f.Focus(); f.SelectAll(); } }, DispatcherPriority.Background);
        }
        return sp;
    }

    // ---------- Zeichnen: Pinsel, Farbe, Strichstärke, Ebenen (Eigenschaften und Zeichenleiste in „Einfach“) ----------
    static readonly string[] ComicSwatches = ["#111111", "#FFFFFF", "#C0392B", "#E67E22", "#F1C40F", "#27AE60", "#2E86C1", "#8E44AD", "#7F5539", "#F5CBA7"];

    WrapPanel ComicBrushButtons()
    {
        var brushes = new WrapPanel();
        foreach (var b in BrushPreset.All.Where(b => !b.Erase))
        {
            var btn = new Button { Name = "comic-brush-" + b.Id, Content = b.Icon + " " + b.Name, Padding = new Thickness(7, 2), Margin = new Thickness(0, 0, 4, 4), FontSize = 11 };
            if (b.Id == ComicBrush) { btn.BorderBrush = Accent; btn.BorderThickness = new Thickness(1.5); }
            btn.Click += (_, _) => { ComicBrush = b.Id; ComicSize = b.Size; ComicTool = "pinsel"; Show(ComicKey); };
            brushes.Children.Add(btn);
        }
        return brushes;
    }

    /// <summary>Farbfelder und das Feld für eine eigene Farbe (#RRGGBB).</summary>
    List<Control> ComicColorControls()
    {
        var swatches = new WrapPanel();
        foreach (var c in ComicSwatches)
        {
            var sw = new Button { Name = "comic-color-" + c.TrimStart('#'), Width = 22, Height = 22, Padding = new Thickness(0), Margin = new Thickness(0, 0, 4, 4), Background = Brush.Parse(c), BorderBrush = c == ComicColor ? Accent : Brush.Parse("#55FFFFFF"), BorderThickness = new Thickness(c == ComicColor ? 2 : 1) };
            ToolTip.SetTip(sw, c); sw.Click += (_, _) => { ComicColor = c; ComicTool = ComicTool == "radierer" ? "pinsel" : ComicTool; Show(ComicKey); };
            swatches.Children.Add(sw);
        }
        var hex = new TextBox { Name = "comic-color", Text = ComicColor, Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
        ToolTip.SetTip(hex, "Eigene Farbe als #RRGGBB");
        hex.LostFocus += (_, _) => { if (PaintColors.IsHex(hex.Text) && hex.Text != ComicColor) { ComicColor = hex.Text!.ToUpperInvariant(); Show(ComicKey); } };
        return [swatches, hex];
    }

    Slider ComicSizeSlider(double width = double.NaN)
    {
        var bsize = new Slider { Name = "comic-brush-size", Minimum = 1, Maximum = 80, Value = ComicSize, Width = width };
        ToolTip.SetTip(bsize, "Strichstärke");
        bsize.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) { ComicSize = Math.Round(bsize.Value); if (ComicViewForTests is { } v) v.BrushSize = ComicSize; } };
        return bsize;
    }

    List<Control> ComicLayerRows(ProgramDocument d, ComicPage page)
    {
        var (paint, _) = ComicArtOf(page);
        var rows = new List<Control>();
        foreach (var l in paint.Layers)
        {
            var id = l.Id;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var vis = new CheckBox { Name = "comic-layer-vis-" + id, IsChecked = l.Visible };
            ToolTip.SetTip(vis, "Sichtbar");
            vis.IsCheckedChanged += (_, _) => ProgramRun(ComicKey, () => ComicEdit(d, "Ebene ein/aus", (_, p) => { var pd = p.Paint(); pd.Get(id).Visible = vis.IsChecked == true; var j = pd.ToJson(); j["active"] = _comicLayer; p.Art = j; }));
            var pickL = new Button { Name = "comic-layer-" + id, Content = l.Name, Padding = new Thickness(8, 2), FontSize = 11.5 };
            if (id == _comicLayer) { pickL.BorderBrush = Accent; pickL.BorderThickness = new Thickness(1.5); }
            pickL.Click += (_, _) => { _comicLayer = id; ComicTool = "pinsel"; Show(ComicKey); };
            row.Children.Add(vis); row.Children.Add(pickL);
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>T-0704: In „Einfach“ sind die Eigenschaften eingeklappt – Zeichnen gehört aber zum Kern eines Comic-Programms. Deshalb eine schlanke Leiste
    /// über der Seite: Pinsel und Farbe als Aufklappknopf mit Wort, Strichstärke direkt, Ebenen aufklappbar. Dieselben Bedienelemente wie in den Eigenschaften.</summary>
    Control ComicDrawBar(ProgramDocument d, ComicPage page)
    {
        Button Drop(string name, Control content, string tip, params Control[] parts)
        {
            var b = new Button { Name = name, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 0), FontSize = 12,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 },
                Flyout = new Flyout { Content = content } };
            foreach (var p in parts) ((StackPanel)b.Content).Children.Add(p);
            ToolTip.SetTip(b, tip);
            return b;
        }
        var brush = BrushPreset.Get(ComicBrush);
        var colors = new StackPanel { Spacing = 6, MaxWidth = 260 };
        foreach (var c in ComicColorControls()) colors.Children.Add(c);
        var layers = new StackPanel { Spacing = 4, Children = { P("Ebenen dieser Seite (oberste zuletzt)", null, 11) } };
        foreach (var r in ComicLayerRows(d, page)) layers.Children.Add(r);
        var addLayer = new Button { Name = "comic-bar-layer-add", Content = "Neue Ebene", Padding = new Thickness(8, 2), FontSize = 11.5 };
        addLayer.Click += (_, _) => ProgramRun(ComicKey, () => ComicAddLayer(d));
        layers.Children.Add(addLayer);
        var bar = new WrapPanel { Name = "comic-drawbar", Margin = new Thickness(0, 0, 0, 6) };
        bar.Children.Add(Drop("comic-bar-brush", new StackPanel { MaxWidth = 300, Children = { ComicBrushButtons() } }, "Pinsel wählen – schaltet auf Zeichnen (B)",
            new TextBlock { Text = "Pinsel: " + brush.Name + " ▾", VerticalAlignment = VerticalAlignment.Center }));
        bar.Children.Add(Drop("comic-bar-color", colors, "Farbe wählen",
            new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Brush.Parse(PaintColors.IsHex(ComicColor) ? ComicColor : "#111111"), BorderBrush = Brush.Parse("#55FFFFFF"), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = "Farbe ▾", VerticalAlignment = VerticalAlignment.Center }));
        bar.Children.Add(new TextBlock { Text = "Strichstärke", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) });
        var size = ComicSizeSlider(140); size.Margin = new Thickness(0, 0, 10, 0); bar.Children.Add(size);
        bar.Children.Add(Drop("comic-bar-layers", layers, "Ebenen zeigen, ausblenden und wählen", new TextBlock { Text = "Ebenen ▾", VerticalAlignment = VerticalAlignment.Center }));
        return bar;
    }
}
