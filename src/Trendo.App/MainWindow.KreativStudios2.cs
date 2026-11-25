using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 2 (T-0611): Bücher schreiben, Animation &amp; Motion, Fotos verwalten, Bilder mit KI.</summary>
public partial class MainWindow
{
    const string BookKey = "buch-programm", AnimKey = "animation-programm", PhotosKey = "fotos-verwalten-programm", ImgAiKey = "bild-ki-programm";

    private void InitKreativStudios2() { InitBook(); InitAnim(); InitPhotos(); InitImageAi(); }

    // ---------------- Bücher schreiben ----------------

    const string BookSample = "# Der Leuchtturmwärter\n\nJeden Abend stieg Ole die hundertzwölf Stufen hinauf. Er zählte sie nie, und doch wusste er genau, wann die Tür kam.\n\nOben roch es nach Öl und Salz. Das Licht drehte sich, als hätte es etwas zu sagen.\n\n# Der Brief\n\nAm Dienstag lag ein Umschlag auf der untersten Stufe. Kein Absender, nur sein Name in einer Schrift, die er kannte.\n\n**Komm zurück**, stand darin. Mehr nicht.\n";

    static JsonObject BookDoc(string title, string text, int goal) => new() { ["title"] = title, ["author"] = "", ["goal"] = goal.ToString(System.Globalization.CultureInfo.InvariantCulture), ["text"] = text };
    static int BookGoal(ProgramDocument d) => int.TryParse(d.Text("goal"), out var g) && g > 0 ? g : 0;

    private void InitBook() => RegisterProgram(FachTabs, new ProgramSpec(BookKey, "STUDIO-BOOK-01", "Bücher schreiben")
    {
        Sentence = "Romane, Sachbücher und Kurzgeschichten in Kapiteln schreiben – mit Wortziel und als fertiges E-Book.",
        Folder = "Bücher",
        Templates =
        [
            new("leer", "Leeres Buch", "Mit Kapitel 1 anfangen", () => BookDoc("Neues Buch", "# Kapitel 1\n\n", 50000)),
            new("beispiel", "Beispiel: Kurzgeschichte", "Zwei Kapitel zum Ausprobieren", () => BookDoc("Der Leuchtturmwärter", BookSample, 8000)),
        ],
        Ribbon =
        [
            // T-0725 (Audit D7): fremdes Manuskript (Word, OpenDocument, Text, Markdown) übernehmen – ein Rückgängig-Schritt, Original bleibt unverändert
            ("Datei", [
                new ProgramCmd("book-open", "Manuskript öffnen …", d => _ = BookPick(d), Tip: "Word (.docx), OpenDocument (.odt), Text oder Markdown als Manuskript übernehmen – Rückgängig holt den alten Stand zurück."),
            ]),
            ("Kapitel", [
                new ProgramCmd("book-chapter", "Neues Kapitel", d => d.Set("text", BookKit.NextChapter(d.Text("text")), "Neues Kapitel"), Primary: true, Tip: "Hängt „# Kapitel N“ ans Ende an."),
            ]),
            ("Claude", [
                new ProgramCmd("book-claude-next", "Weiterschreiben mit Claude", d => _ = AssistFromProgram(BookKey, "STUDIO-BOOK-01", "Schreib das letzte Kapitel in meinem Stil weiter, etwa eine Seite, nur erfundene Figuren:\n\n" + BookTail(d.Text("text"))), Tip: "Nutzt deinen Claude-Zugang. Du entscheidest, was übernommen wird."),
                new ProgramCmd("book-claude-edit", "Lektorat mit Claude", d => _ = AssistFromProgram(BookKey, "STUDIO-BOOK-01", "Lies dieses Kapitel wie eine Lektorin: Logik, Längen, Wiederholungen, Rechtschreibung. Mach Vorschläge als Liste:\n\n" + BookTail(d.Text("text")))),
            ]),
        ],
        Exports =
        [
            new("epub", "E-Book (.epub)", ".epub", (d, f) => { File.WriteAllBytes(f, BookKit.Epub(d.Text("title"), d.Text("author"), d.Text("text"), DateTimeOffset.UtcNow)); return f; }),
            // T-0725: Word, OpenDocument und Druck-PDF über die Schreiber von Trendo Text (Titelseite, je Kapitel eine neue Seite, fett/kursiv/Aufzählung)
            new("docx", "Word (.docx)", ".docx", (d, f) => { Trendo.Core.Office.DocxFormat.Write(BookKit.ToTextDoc(d.Text("title"), d.Text("author"), d.Text("text")), f); return f; }),
            new("pdf", "Druck-PDF (.pdf)", ".pdf", (d, f) => { Trendo.Core.Office.TextPdf.Export(BookKit.ToTextDoc(d.Text("title"), d.Text("author"), d.Text("text")), f); return f; }),
            new("odt", "OpenDocument (.odt)", ".odt", (d, f) => { Trendo.Core.Office.OdtFormat.Write(BookKit.ToTextDoc(d.Text("title"), d.Text("author"), d.Text("text")), f); return f; }),
            new("md", "Manuskript (.md)", ".md", (d, f) => { File.WriteAllText(f, d.Text("text")); return f; }),
            new("txt", "Nur Text (.txt)", ".txt", (d, f) => { File.WriteAllText(f, string.Join("\n\n", BookKit.Chapters(d.Text("text")).Select(c => c.Title.ToUpperInvariant() + "\n\n" + c.Body))); return f; }),
        ],
        Workspace = BookWorkspace,
        Properties = BookProperties,
        SimpleExports = 4, // T-0725: E-Book, Word, Druck-PDF und OpenDocument auch in „Einfach“ (keine Funktion nur in „Erweitert“)
        Status = d => { var s = BookKit.Of(d.Text("text"), BookGoal(d)); return $"{s.Words:N0} Wörter · {s.Chapters} Kapitel · etwa {s.Pages} Seiten" + (BookGoal(d) > 0 ? $" · Ziel {s.GoalPercent.ToString("0.#", De)} %" : ""); },
        Help = "Jedes Kapitel beginnt mit „# Titel“, Absätze trennst du mit einer Leerzeile. **fett** und *kursiv* gehen auch. Rechts siehst du Kapitel, Wörter und dein Ziel. Das E-Book öffnet jeder E-Book-Leser.",
    }, "buch", "bücher", "roman", "manuskript", "kapitel", "ebook", "e-book", "epub", "kurzgeschichte", "autorin", "schreiben");

    /// <summary>T-0725: Datei wählen (Dialog; im Test ersetzt) und als Manuskript übernehmen.</summary>
    async Task BookPick(ProgramDocument d)
    {
        var req = new PickRequest("book-open", false, false, BookKit.ImportExtensions);
        string[]? got;
        if (PickOverride is { } fake) got = await fake(req);
        else got = (await PickOpenFiles(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Manuskript öffnen",
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Manuskript") { Patterns = ["*.docx", "*.odt", "*.txt", "*.md"] }] }))
            .Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is [var f, ..]) ProgramRun(BookKey, () => BookOpenFile(d, f));
    }

    /// <summary>T-0725: Manuskript aus einer fremden Datei ins offene Buch (ein Rückgängig-Schritt). Was die Datei mehr enthält (Bilder, Kommentare …), steht in der Meldung.</summary>
    public void BookOpenFile(ProgramDocument d, string path)
    {
        var r = BookKit.Import(path);
        d.Edit("Manuskript öffnen", x => { x["text"] = r.Manuscript; if (r.Title.Length > 0) x["title"] = r.Title; });
        _progMsg[BookKey] = $"„{Path.GetFileName(path)}“ übernommen: {r.Chapters} Kapitel, {BookKit.Words(r.Manuscript).ToString("N0", De)} Wörter. Das Original bleibt unverändert."
            + (r.Report is { HasLoss: true } rep ? " Nicht übernommen: " + rep.Kurz() + "." : "");
        _core.Audit.Append(Principal.LocalOwner.Name, "buch.oeffnen", Path.GetFileName(path));
    }

    static string BookTail(string text) { var ch = BookKit.Chapters(text); return ch.Count == 0 ? "" : $"# {ch[^1].Title}\n\n{KreativSafe.Cut(ch[^1].Body, 12000)}"; }

    Control BookWorkspace(ProgramDocument d)
    {
        var right = new StackPanel { Name = "book-overview", Spacing = 6 };
        void Render(string text)
        {
            right.Children.Clear();
            var ch = BookKit.Chapters(text); var s = BookKit.Of(text, BookGoal(d));
            right.Children.Add(H("Kapitel", 15));
            var max = Math.Max(1, ch.Select(c => c.Words).DefaultIfEmpty(1).Max());
            foreach (var c in ch)
                right.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,70"), RowDefinitions = new RowDefinitions("Auto,6"),
                    Children =
                    {
                        new TextBlock { Text = c.Title, Foreground = Text, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis },
                        Pos(new TextBlock { Text = c.Words.ToString("N0", De), Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right }, 1),
                        new Border { [Grid.RowProperty] = 1, [Grid.ColumnSpanProperty] = 2, Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Max(4, 260.0 * c.Words / max), Background = Accent },
                    },
                });
            if (BookGoal(d) > 0)
            {
                right.Children.Add(P($"Ziel {BookGoal(d):N0} Wörter – {s.GoalPercent.ToString("0.#", De)} % geschafft", Muted, 12));
                right.Children.Add(new ProgressBar { Name = "book-goal", Minimum = 0, Maximum = 100, Value = s.GoalPercent, Height = 8 });
            }
            right.Children.Add(P($"{s.Words:N0} Wörter · etwa {s.Pages} Normseiten · {s.ReadMinutes} Minuten Lesezeit", Muted, 11));
        }
        var box = KrBox(d, "text", "book-text", "# Kapitel 1", true, Render);
        BueroFileDrop(box, BookKey, BueroExt(".docx", ".odt", ".txt", ".md"), "ein Manuskript (.docx, .odt, .txt, .md)", (dd, files) => BookOpenFile(dd, files[0])); // T-0725: Datei aufs Manuskript ziehen
        box.FontFamily = new FontFamily("Georgia, Cambria, serif"); box.FontSize = 15; box.MinHeight = 460;
        Render(d.Text("text"));
        // V-12 (T-0700): Kapitel, Fett, Kursiv und Liste per Knopf statt „#“ und „**“ tippen
        var write = WithWriteMarks(box, "book",
            (Trendo.Core.Text.WriteMarks.Kind.Heading, "Kapitelüberschrift", "Macht die Zeile zur Kapitelüberschrift (noch einmal: wieder normaler Text)"),
            (Trendo.Core.Text.WriteMarks.Kind.Bold, "Fett", "Markiertes fett – ohne Markierung ein Beispielwort zum Überschreiben"),
            (Trendo.Core.Text.WriteMarks.Kind.Italic, "Kursiv", "Markiertes schräg"),
            (Trendo.Core.Text.WriteMarks.Kind.List, "Aufzählung", "Zeilen als Aufzählung"));
        return new Grid { ColumnDefinitions = new ColumnDefinitions("2*,14,*"), Margin = new Thickness(4, 0, 10, 4), Children = { write, Pos(KrCard(right, "book-card"), 2) } };
    }

    Control BookProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        void Txt(string key, string label) { sp.Children.Add(P(label, null, 11)); sp.Children.Add(KrBox(d, key, "book-p-" + key, label)); }
        Txt("title", "Buchtitel");
        Txt("author", "Autorin oder Autor");
        Txt("goal", "Wortziel (z. B. 50000)");
        sp.Children.Add(P("Ein Roman hat meist 50 000 bis 90 000 Wörter, eine Kurzgeschichte 2 000 bis 8 000.", Muted, 11));
        return sp;
    }

    // ---------------- Animation & Motion ----------------
    // T-0696: Bühne mit dem gemeinsamen Vektor-Editor (Formen, Pfade, Text, Bilder/SVG anfassen), Zeitleiste mit ziehbaren Schlüsselbildern,
    // Kurven je Schlüssel. Ältere Animationen (Ebenen rechteck/kreis/text) werden beim Öffnen gelesen und beim nächsten Speichern umgestellt.

    double _animTime; int _animSel; DispatcherTimer? _animPlay; string _animEase = "ein-aus";

    static JsonObject AnimLayerJson(AnimKit.Layer l) => new()
    {
        ["id"] = l.Id, ["shape"] = l.Shape, ["text"] = l.Text, ["color"] = l.Color, ["w"] = l.W, ["h"] = l.H,
        ["tracks"] = new JsonObject(l.Tracks.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, new JsonArray(kv.Value.Select(k => (JsonNode)new JsonObject { ["t"] = k.T, ["v"] = k.V, ["e"] = k.Ease }).ToArray())))),
    };

    static double Num(JsonNode? n, double fallback) => n is JsonValue v && v.TryGetValue<double>(out var x) && double.IsFinite(x) ? x : fallback;

    /// <summary>Altes Ebenen-Format (vor T-0696).</summary>
    static AnimKit.Layer AnimLegacy(JsonObject o)
    {
        var tracks = new Dictionary<string, List<AnimKit.Key>>();
        if (o["tracks"] is JsonObject tr)
            foreach (var (prop, arr) in tr)
                if (AnimKit.Props.Contains(prop) && arr is JsonArray a)
                    tracks[prop] = a.OfType<JsonObject>().Select(k => new AnimKit.Key(Math.Max(0, Num(k["t"], 0)), Num(k["v"], AnimKit.Default(prop)), Js(k["e"]) is { Length: > 0 } e ? e : "ein-aus")).OrderBy(k => k.T).ToList();
        var shape = Js(o["shape"]); var color = Js(o["color"]);
        return new AnimKit.Layer(Js(o["id"]), AnimKit.Shapes.Contains(shape) ? shape : "rechteck", Js(o["text"]), LogoKit.IsHex(color) ? color : "#F5C542",
            Math.Clamp(Num(o["w"], 200), 4, 4000), Math.Clamp(Num(o["h"], 120), 4, 4000), tracks);
    }

    /// <summary>Die Bühne als Vektor-Dokument (Formen mit ihren Schlüsselbildern).</summary>
    public static VDoc AnimVec(ProgramDocument d)
    {
        var bg = d.Text("background");
        var v = new VDoc { W = Math.Clamp(d.Number("w", AnimKit.StageW), 64, 3840), H = Math.Clamp(d.Number("h", AnimKit.StageH), 64, 3840), Background = LogoKit.IsHex(bg) ? bg : "#101827" };
        foreach (var o in d.List("layers").OfType<JsonObject>())
        {
            var s = o["kind"] is not null ? VectorKit.ShapeFrom(o) : MotionKit.FromLayer(AnimLegacy(o));
            if (s.Id.Length == 0 || v.Find(s.Id) is not null) s.Id = VectorKit.NextId(v, s.Kind);
            v.Shapes.Add(s);
        }
        return v;
    }

    static void AnimSave(ProgramDocument d, VDoc v, string label) => d.Edit(label, x =>
    {
        x["layers"] = new JsonArray(v.Shapes.Select(s => (JsonNode)VectorKit.ShapeJson(s)).ToArray());
        x["w"] = v.W; x["h"] = v.H;
    });

    static JsonObject AnimDoc(string bg, params AnimKit.Layer[] layers) => new() { ["background"] = bg, ["w"] = AnimKit.StageW, ["h"] = AnimKit.StageH, ["layers"] = new JsonArray(layers.Select(l => (JsonNode)AnimLayerJson(l)).ToArray()) };

    static AnimKit.Layer AnimSampleBall()
    {
        var l = new AnimKit.Layer("ball", "kreis", "", "#F5C542", 120, 120, []);
        AnimKit.SetKey(l, "x", 0, 200, "linear"); AnimKit.SetKey(l, "x", 2, 1080);
        AnimKit.SetKey(l, "y", 0, 120, "federnd"); AnimKit.SetKey(l, "y", 1.2, 600);
        return l;
    }

    /// <summary>Gewählte Form: Auswahl des Editors, sonst die zuletzt gewählte Ebene.</summary>
    VShape? AnimCurrent(ProgramDocument d, VDoc v)
    {
        var st = VecState(d);
        if (v.Shapes.FirstOrDefault(s => st.Selected.Contains(s.Id)) is { } s0) { _animSel = v.Shapes.IndexOf(s0); return s0; }
        if (v.Shapes.Count == 0) return null;
        var s = v.Shapes[Math.Clamp(_animSel, 0, v.Shapes.Count - 1)];
        st.Selected.Clear(); st.Selected.Add(s.Id);
        return s;
    }

    void AnimAdd(ProgramDocument d, string shape)
    {
        var v = AnimVec(d);
        var kind = shape switch { "kreis" => "ellipse", "text" => "text", "rechteck" => "rect", _ => shape };
        var s = VectorKit.New(kind, 0, 0, kind == "ellipse" ? 140 : 220, 140, VectorKit.NextId(v, kind), kind == "ellipse" ? "#5FA8D3" : "#F5C542");
        if (kind == "rect") s.Radius = 12;
        if (kind == "text") { s.Text = "Titel"; s.FontSize = 90; s.Bold = true; s.Anchor = "middle"; VectorRender.FitText(s); }
        s.X = v.W / 2 - s.W / 2; s.Y = v.H / 2 - s.H / 2;
        v.Shapes.Add(s);
        AnimSave(d, v, VectorKit.Label(kind) + " hinzugefügt");
        var st = VecState(d); st.Selected.Clear(); st.Selected.Add(s.Id); st.Tool = "select"; _animSel = v.Shapes.Count - 1;
    }

    /// <summary>Schlüssel setzen; ist es der erste Schlüssel einer Eigenschaft nach dem Anfang, hält ein Schlüssel bei 0 s den bisherigen Wert (sonst springt die Form).</summary>
    void AnimSetKey(VShape s, string prop, double t, double value)
    {
        if ((!s.Tracks.TryGetValue(prop, out var k) || k.Count == 0) && t > 0.005) MotionKit.SetKey(s, prop, 0, MotionKit.Base(s, prop), _animEase);
        MotionKit.SetKey(s, prop, t, value, _animEase);
    }

    void AnimPreset(ProgramDocument d, string move)
    {
        var v = AnimVec(d);
        if (AnimCurrent(d, v) is not { } s) throw new InvalidOperationException("Lege zuerst eine Form an.");
        var p = Trendo.Core.PromptAction.AnimTitleAdapter.Title(s.Text, move, VectorKit.IsColor(s.Fill) ? s.Fill : "#F5C542");
        var start = _animTime;
        foreach (var prop in p.Tracks.Keys) s.Tracks.Remove(prop);
        foreach (var (prop, keys) in p.Tracks)
            foreach (var k in keys)
                MotionKit.SetKey(s, prop, start + k.T, prop == "x" ? k.V - AnimKit.StageW / 2 + s.Cx : prop == "y" ? k.V - AnimKit.StageH / 2 + s.Cy : k.V, k.Ease);
        AnimSave(d, v, "Bewegung " + move);
    }

    /// <summary>Auf der Bühne gezogen, gedreht, skaliert: mit „Schlüssel automatisch“ (oder wenn die Eigenschaft schon Schlüssel hat) entsteht ein Schlüssel zur aktuellen Zeit, sonst ändert sich die Form selbst.</summary>
    void AnimTransform(ProgramDocument d, IReadOnlyList<(VShape Before, VShape After)> changes, string label)
    {
        var v = AnimVec(d); var st = VecState(d); var t = _animTime; var keyed = false;
        foreach (var (b, a) in changes)
        {
            if (v.Find(a.Id) is not { } s) continue;
            bool Key(string prop) => st.AutoKey || s.Tracks.ContainsKey(prop);
            if (Math.Abs(a.Cx - b.Cx) + Math.Abs(a.Cy - b.Cy) > 1e-6)
            {
                if (Key("x") || Key("y")) { AnimSetKey(s, "x", t, a.Cx); AnimSetKey(s, "y", t, a.Cy); keyed = true; }
                else { s.X += a.Cx - b.Cx; s.Y += a.Cy - b.Cy; }
            }
            if (Math.Abs(a.Rot - b.Rot) > 1e-6) { if (Key("drehung")) { AnimSetKey(s, "drehung", t, a.Rot); keyed = true; } else s.Rot = a.Rot; }
            if (Math.Abs(a.Scale - b.Scale) > 1e-6) { if (Key("skalierung")) { AnimSetKey(s, "skalierung", t, a.Scale); keyed = true; } else s.Scale = a.Scale; }
        }
        AnimSave(d, v, keyed ? $"{label} – Schlüssel bei {t.ToString("0.00", De)} s" : label);
    }

    void AnimFill(ProgramDocument d, IReadOnlyList<VShape> sel, string color)
    {
        if (!VectorKit.IsColor(color)) throw new ArgumentException("Farbe als #RRGGBB angeben.");
        var v = AnimVec(d); var st = VecState(d); var keyed = false;
        foreach (var p in sel)
        {
            if (v.Find(p.Id) is not { } s) continue;
            if (st.AutoKey || s.Tracks.ContainsKey("farbe")) { AnimSetKey(s, "farbe", _animTime, VectorKit.Pack(color)); keyed = true; }
            else { s.Fill = color; s.Fill2 = ""; }
        }
        AnimSave(d, v, keyed ? $"Farbe – Schlüssel bei {_animTime.ToString("0.00", De)} s" : "Farbe");
    }

    /// <summary>Andere Änderungen auf der Bühne (Form gezeichnet, Punkte, Text, Reihenfolge, Löschen) zurück in die Formen ohne Pose übernehmen.</summary>
    static VDoc AnimMerge(VDoc baseDoc, VDoc posed, double t)
    {
        var res = new VDoc { W = baseDoc.W, H = baseDoc.H, Background = baseDoc.Background };
        foreach (var p in posed.Shapes)
        {
            if (baseDoc.Find(p.Id) is not { } b) { var n = p.Clone(); n.Tracks.Clear(); res.Shapes.Add(n); continue; }
            var was = MotionKit.Pose(b, t);
            var m = p.Clone(); m.Tracks = b.Clone().Tracks;
            double dcx = p.Cx - was.Cx, dcy = p.Cy - was.Cy;
            var cx = b.Cx + (m.Tracks.ContainsKey("x") ? 0 : dcx); var cy = b.Cy + (m.Tracks.ContainsKey("y") ? 0 : dcy);
            if (m.Tracks.TryGetValue("x", out var kx)) for (var i = 0; i < kx.Count; i++) kx[i] = kx[i] with { V = kx[i].V + dcx };
            if (m.Tracks.TryGetValue("y", out var ky)) for (var i = 0; i < ky.Count; i++) ky[i] = ky[i] with { V = ky[i].V + dcy };
            m.X = cx - m.W / 2; m.Y = cy - m.H / 2;
            m.Rot = m.Tracks.ContainsKey("drehung") ? b.Rot : p.Rot;
            m.Scale = m.Tracks.ContainsKey("skalierung") ? b.Scale : p.Scale;
            m.Opacity = m.Tracks.ContainsKey("deckkraft") ? b.Opacity : p.Opacity;
            if (m.Tracks.ContainsKey("farbe")) { m.Fill = b.Fill; m.Fill2 = b.Fill2; }
            res.Shapes.Add(m);
        }
        return res;
    }

    private void InitAnim() => RegisterProgram(FachTabs, new ProgramSpec(AnimKey, "STUDIO-ANIM-01", "Animation & Motion")
    {
        Sentence = "Formen, Pfade, Texte und Bilder auf der Bühne bewegen: anfassen, Schlüsselbilder in der Zeitleiste ziehen, Kurven wählen – als animiertes SVG, Video oder GIF.",
        Folder = "Animationen",
        FillHeight = true,
        FirstStep = new("Erste Form auf die Bühne", "Leg eine Form auf die Bühne – oder zeichne links mit Rechteck, Ellipse, Stern oder Text.", d => AnimAdd(d, "rechteck"), d => d.List("layers").Count == 0),
        Templates =
        [
            new("leer", "Leere Bühne", "1280 × 720, dunkler Hintergrund", () => AnimDoc("#101827")),
            new("ball", "Beispiel: Springender Ball", "Ein Ball fällt und federt", () => AnimDoc("#101827", AnimSampleBall())),
            new("titel", "Beispiel: Titel", "Ein Titel fliegt ein", () => AnimDoc("#101827", Trendo.Core.PromptAction.AnimTitleAdapter.Title("Willkommen", "einfliegen", "#F5C542"))),
        ],
        Tools = VecTools(AnimKey),
        Ribbon =
        [
            ("Ebene", [
                new ProgramCmd("anim-add-rechteck", "Rechteck", d => AnimAdd(d, "rechteck")),
                new ProgramCmd("anim-add-kreis", "Kreis", d => AnimAdd(d, "kreis")),
                new ProgramCmd("anim-add-text", "Text", d => AnimAdd(d, "text"), Primary: true),
            ]),
            ("Bewegung", Trendo.Core.PromptAction.AnimTitleAdapter.Moves.Select(m => new ProgramCmd("anim-move-" + m, char.ToUpper(m[0], De) + m[1..], d => AnimPreset(d, m), Tip: "Für die gewählte Form ab der aktuellen Zeit.")).ToList()),
            ("Ausgabe", [
                new ProgramCmd("anim-video", "Video (MP4)", d => _ = AnimVideo(d), Tip: "Braucht FFmpeg. 30 Bilder je Sekunde in der Größe der Bühne."),
                new ProgramCmd("anim-gif", "GIF", d => _ = AnimVideo(d, gif: true), Tip: "Braucht FFmpeg. Bewegtes Bild für Chats und Webseiten, 15 Bilder je Sekunde."),
            ]),
            ("Claude", [
                new ProgramCmd("anim-claude", "Bewegung mit Claude planen", d => _ = AssistFromProgram(AnimKey, "STUDIO-ANIM-01", "Plane für meine Animation eine Bewegung in Schritten (Zeit in Sekunden, Position, Größe, Drehung, Deckkraft, Kurve gleichmäßig/weich/federnd). Formen auf der Bühne: " + string.Join(", ", AnimVec(d).Shapes.Select(s => s.Kind == "text" ? "Text „" + s.Text + "“" : VectorKit.Label(s.Kind)))), Tip: "Nutzt deinen Claude-Zugang."),
            ]),
        ],
        GroupLevels = new() { ["Ausgabe"] = 1, ["Claude"] = 1 },
        Exports =
        [
            new("svg", "Animiertes SVG (.svg)", ".svg", (d, f) => { File.WriteAllText(f, MotionKit.Svg(AnimVec(d))); return f; }),
        ],
        Workspace = AnimWorkspace,
        Properties = AnimProperties,
        Bottom = new ProgramPanel("Zeitleiste", AnimTimeline, 176),
        Status = d => { var v = AnimVec(d); return $"{v.Shapes.Count} Ebenen · {MotionKit.Duration(v).ToString("0.0", De)} s · Zeit {_animTime.ToString("0.00", De)} s · {v.W:0} × {v.H:0}"; },
        Help = "Links ein Werkzeug wählen und auf der Bühne zeichnen. Mit „Auswahl“ eine Form ziehen, an den Griffen skalieren, am runden Griff drehen: Bei „Schlüssel automatisch“ entsteht dabei ein Schlüsselbild zur aktuellen Zeit. Unten in der Zeitleiste die Zeit setzen, Rauten ziehen, Doppelklick setzt einen Schlüssel, Rechtsklick wählt die Kurve (gleichmäßig, weich, federnd). Bilder und SVG einfach auf die Bühne ziehen.",
    }, "animation", "motion", "motion design", "keyframe", "schlüsselbild", "bewegung", "intro", "titelanimation", "animieren", "zeitleiste");


    Control AnimWorkspace(ProgramDocument d)
    {
        var v = AnimVec(d);
        AnimCurrent(d, v);
        _animTime = Math.Clamp(_animTime, 0, Math.Max(MotionKit.Duration(v) + 1, 3));
        var ed = VecEditor(AnimKey, d, MotionKit.PoseDoc(v, _animTime), (nv, label) => AnimSave(d, AnimMerge(AnimVec(d), nv, _animTime), label), scaleMode: true);
        ed.TransformCommit = (list, label) => ProgramRun(AnimKey, () => AnimTransform(d, list, label));
        ed.FillCommit = (sel, c) => ProgramRun(AnimKey, () => AnimFill(d, sel, c));
        ed.DetachedFromVisualTree += (_, _) => { if (VecEditors.GetValueOrDefault(AnimKey) == ed) { _animPlay?.Stop(); _animPlay = null; } };
        return new Border { Name = "anim-stage", Margin = new Thickness(4, 0, 6, 4), Child = ed };
    }

    Control AnimTimeline(ProgramDocument d)
    {
        var v = AnimVec(d); var st = VecState(d);
        var dur = MotionKit.Duration(v);
        var tl = new KeyTimeline(v, st, Math.Max(dur + 1, 3)) { Name = "anim-timeline", Time = _animTime };
        var time = new TextBlock { Name = "anim-time", Foreground = Text, FontFamily = new FontFamily("Consolas, monospace"), VerticalAlignment = VerticalAlignment.Center, Width = 70, Text = $"{_animTime:0.00} s", Margin = new Thickness(8, 0) };
        var slider = new Slider { Name = "anim-slider", Minimum = 0, Maximum = dur, Value = Math.Min(_animTime, dur), VerticalAlignment = VerticalAlignment.Center, MinWidth = 160 };
        ToolTip.SetTip(slider, "Zeit – auch in der Zeitleiste darunter klicken oder ziehen");
        void Draw(double t)
        {
            _animTime = t; time.Text = $"{t:0.00} s"; tl.Time = t; tl.InvalidateVisual();
            if (VecEditors.GetValueOrDefault(AnimKey) is { } ed) ed.ShowDoc(MotionKit.PoseDoc(AnimVec(d), t));
        }
        slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && Math.Abs(slider.Value - _animTime) > 0.0001 && _animTime <= dur + 1e-9) Draw(slider.Value); };
        tl.Scrub = t => { Draw(t); if (Math.Abs(slider.Value - Math.Min(t, dur)) > 0.0001) slider.Value = Math.Min(t, dur); };
        tl.SelectShape = id => { st.Selected.Clear(); st.Selected.Add(id); Show(AnimKey); };
        tl.KeysMoved = (id, props, from, to) => ProgramRun(AnimKey, () => { var x = AnimVec(d); if (x.Find(id) is { } s) { MotionKit.MoveKeys(s, props, from, to); AnimSave(d, x, $"Schlüssel nach {to.ToString("0.00", De)} s verschoben"); _animTime = to; } });
        tl.KeyCommand = (id, props, t, cmd) => ProgramRun(AnimKey, () =>
        {
            var x = AnimVec(d); if (x.Find(id) is not { } s) return;
            if (cmd == "delete") { MotionKit.DeleteKeys(s, props, t); AnimSave(d, x, "Schlüssel gelöscht"); }
            else if (cmd.StartsWith("ease:", StringComparison.Ordinal)) { MotionKit.SetEase(s, props, t, cmd[5..]); AnimSave(d, x, "Kurve " + AnimKit.Eases.First(e => e.Id == cmd[5..]).Label); }
        });
        tl.AddKey = (id, props, t) => ProgramRun(AnimKey, () =>
        {
            var x = AnimVec(d); if (x.Find(id) is not { } s) return;
            foreach (var p in props) AnimSetKey(s, p, t, MotionKit.Value(s, p, t));
            _animTime = t; AnimSave(d, x, $"Schlüssel bei {t.ToString("0.00", De)} s");
        });
        var play = new Button { Name = "anim-play", Content = _animPlay is null ? "▶ Abspielen" : "■ Stopp", Padding = new Thickness(12, 4) };
        play.Click += (_, _) =>
        {
            if (_animPlay is { } running) { running.Stop(); _animPlay = null; play.Content = "▶ Abspielen"; Draw(_animTime); return; }
            var start = DateTime.UtcNow; var from = _animTime >= dur ? 0 : _animTime; var cur = AnimVec(d);
            _animPlay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _animPlay.Tick += (_, _) =>
            {
                var t = from + (DateTime.UtcNow - start).TotalSeconds;
                if (t >= dur) { t = dur; _animPlay?.Stop(); _animPlay = null; play.Content = "▶ Abspielen"; }
                _animTime = t; time.Text = $"{t:0.00} s"; tl.Time = t; tl.InvalidateVisual(); slider.Value = t;
                if (VecEditors.GetValueOrDefault(AnimKey) is { } ed) ed.ShowDoc(MotionKit.PoseDoc(cur, t));
            };
            _animPlay.Start(); play.Content = "■ Stopp";
        };
        var auto = new CheckBox { Name = "anim-autokey", Content = "Schlüssel automatisch", IsChecked = st.AutoKey, FontSize = 12 };
        ToolTip.SetTip(auto, "An: Ziehen, Drehen, Skalieren oder Umfärben auf der Bühne setzt ein Schlüsselbild zur aktuellen Zeit. Aus: die Form selbst ändert sich.");
        auto.Click += (_, _) => st.AutoKey = auto.IsChecked == true;
        var ease = new ComboBox { Name = "anim-ease", ItemsSource = AnimKit.Eases.Select(e => "Kurve: " + e.Label).ToList(), SelectedIndex = Math.Max(0, AnimKit.Eases.ToList().FindIndex(e => e.Id == _animEase)), FontSize = 12, MinWidth = 170 };
        ToolTip.SetTip(ease, "Kurve für neue Schlüssel – bestehende per Rechtsklick auf die Raute ändern");
        ease.SelectionChanged += (_, _) => _animEase = AnimKit.Eases[Math.Max(0, ease.SelectedIndex)].Id;
        var fmt = new ComboBox { Name = "anim-p-format", ItemsSource = MotionKit.Formats.Select(f => f.Label).ToList(), FontSize = 12, MinWidth = 190, Margin = new Thickness(0, 0, 6, 0),
            SelectedIndex = MotionKit.Formats.ToList().FindIndex(f => f.W == v.W.ToString("0", System.Globalization.CultureInfo.InvariantCulture) && f.H == v.H.ToString("0", System.Globalization.CultureInfo.InvariantCulture)) };
        fmt.SelectionChanged += (_, _) =>
        {
            if (fmt.SelectedIndex < 0) return;
            var f = MotionKit.Formats[fmt.SelectedIndex]; double nw = double.Parse(f.W, System.Globalization.CultureInfo.InvariantCulture), nh = double.Parse(f.H, System.Globalization.CultureInfo.InvariantCulture);
            if (Math.Abs(nw - v.W) + Math.Abs(nh - v.H) < 0.5) return;
            ProgramRun(AnimKey, () =>
            {
                // Formen bleiben in der Mitte der Bühne
                var x = AnimVec(d); double dx = (nw - x.W) / 2, dy = (nh - x.H) / 2;
                foreach (var s in x.Shapes)
                {
                    s.X += dx; s.Y += dy;
                    if (s.Tracks.TryGetValue("x", out var kx)) for (var i = 0; i < kx.Count; i++) kx[i] = kx[i] with { V = kx[i].V + dx };
                    if (s.Tracks.TryGetValue("y", out var ky)) for (var i = 0; i < ky.Count; i++) ky[i] = ky[i] with { V = ky[i].V + dy };
                }
                x.W = nw; x.H = nh; AnimSave(d, x, f.Label);
            });
        };
        ToolTip.SetTip(fmt, "Bühnengröße – Formen bleiben mittig");
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto"), Margin = new Thickness(0, 0, 0, 6), Children = { play, Pos(time, 1), Pos(slider, 2), Pos(auto, 3), Pos(ease, 4), Pos(fmt, 5) } };
        auto.Margin = new Thickness(10, 0, 6, 0);
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        ToolTip.SetTip(tl, "Rauten ziehen verschiebt Schlüssel · Doppelklick in eine Spur setzt einen · Rechtsklick: Kurve oder Löschen · Klick auf einen Namen wählt die Form");
        return new DockPanel { Children = { bar, new ScrollViewer { Content = tl, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } } };
    }

    Control AnimProperties(ProgramDocument d)
    {
        var v = AnimVec(d);
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Hintergrund (#RRGGBB)", null, 11)); sp.Children.Add(KrBox(d, "background", "anim-p-background", "#101827"));
        sp.Children.Add(H("Ebenen", 13));
        var l = AnimCurrent(d, v);
        if (l is null) { sp.Children.Add(P("Noch keine Form. Links zeichnen oder oben „Rechteck“, „Kreis“ oder „Text“.", Muted, 12)); return sp; }
        for (var i = 0; i < v.Shapes.Count; i++)
        {
            var s = v.Shapes[i]; var id = s.Id;
            var b = new Button { Name = "anim-layer-" + i, Content = $"{VectorKit.Label(s.Kind)} · {(s.Kind == "text" ? s.Text : s.Name.Length > 0 ? s.Name : s.Id)}", HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 4) };
            if (s == l) b.Classes.Add("accent");
            b.Click += (_, _) => { var st = VecState(d); st.Selected.Clear(); st.Selected.Add(id); Show(AnimKey); };
            sp.Children.Add(b);
        }
        sp.Children.Add(H("Gewählte Ebene", 13));
        void Field(string name, string label, string value, Action<string> apply)
        {
            var t = new TextBox { Name = name, Text = value, PlaceholderText = label };
            t.LostFocus += (_, _) => { if ((t.Text ?? "") != value) ProgramRun(AnimKey, () => apply(t.Text ?? "")); };
            sp.Children.Add(P(label, null, 11)); sp.Children.Add(t);
        }
        var sid = l.Id;
        if (l.Kind == "text") Field("anim-p-text", "Text", l.Text, val => { var x = AnimVec(d); if (x.Find(sid) is { } s) { s.Text = val; VectorRender.FitText(s); AnimSave(d, x, "Text geändert"); } });
        Field("anim-p-color", "Farbe (#RRGGBB)", l.Fill, val => { if (!LogoKit.IsHex(val)) throw new ArgumentException("Farbe als #RRGGBB angeben."); var x = AnimVec(d); if (x.Find(sid) is { } s) { s.Fill = val; s.Fill2 = ""; AnimSave(d, x, "Farbe geändert"); } });
        sp.Children.Add(H($"Schlüssel bei {_animTime.ToString("0.00", De)} s", 13));
        foreach (var prop in MotionKit.Props)
        {
            var p = prop;
            var shown = p == "farbe" ? MotionKit.Color(l, _animTime) : MotionKit.Value(l, p, _animTime).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var box = new TextBox { Name = "anim-val-" + p, Text = shown, Width = 90 };
            var set = new Button { Name = "anim-key-" + p, Content = "Schlüssel", Padding = new Thickness(8, 3) };
            set.Click += (_, _) => ProgramRun(AnimKey, () =>
            {
                double val;
                if (p == "farbe") { if (!LogoKit.IsHex((box.Text ?? "").Trim())) throw new FormatException("Farbe als #RRGGBB eintragen."); val = VectorKit.Pack((box.Text ?? "").Trim()); }
                else if (!double.TryParse((box.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val) || !double.IsFinite(val)) throw new FormatException("Bitte eine Zahl eintragen.");
                var x = AnimVec(d); if (x.Find(sid) is not { } s) return;
                AnimSetKey(s, p, _animTime, val);
                AnimSave(d, x, $"Schlüssel {p}");
            });
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("90,*,Auto"), Margin = new Thickness(0, 2), Children = { new TextBlock { Text = p, Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, Pos(box, 1), Pos(set, 2) } });
        }
        var del = new Button { Name = "anim-delete", Content = "Ebene löschen", Padding = new Thickness(8, 3), Margin = new Thickness(0, 8, 0, 0) };
        del.Click += (_, _) => ProgramRun(AnimKey, () => { var x = AnimVec(d); x.Shapes.RemoveAll(s => s.Id == sid); _animSel = Math.Max(0, _animSel - 1); VecState(d).Selected.Clear(); AnimSave(d, x, "Ebene gelöscht"); });
        sp.Children.Add(del);
        return sp;
    }

    /// <summary>Tests: Bilder zu Video (Bildmuster, Bilder je Sekunde, Ziel) statt FFmpeg.</summary>
    public static Func<string, int, string, bool>? AnimVideoOverride { get; set; }
    /// <summary>T-0696: FFmpeg für die Animation fest vorgeben (Tests mit dem echten FFmpeg im abgeschotteten Modus) – kein Ersatz für das Werkzeug.</summary>
    public string? AnimFfmpegPath { get; set; }
    bool _animBusy;

    /// <summary>Jedes Bild zeichnen (in der Größe der Bühne), dann FFmpeg: H.264-MP4 oder GIF. Die Bildfolge liegt nur kurz im Temp-Ordner.</summary>
    public async Task<string?> AnimVideo(ProgramDocument d, string? output = null, bool gif = false)
    {
        if (_animBusy) { _progMsg[AnimKey] = "Das Video entsteht schon – einen Moment."; Show(AnimKey); return null; }
        var ff = AnimVideoOverride is null ? AnimFfmpegPath ?? FfmpegPath : "ffmpeg";
        if (ff is null) { _progMsg[AnimKey] = "Für das Video fehlt FFmpeg. Du richtest es im Studio „Videos schneiden“ mit einem Klick ein."; Show(AnimKey); return null; }
        var v = AnimVec(d); var dur = MotionKit.Duration(v);
        if (dur > 60) { _progMsg[AnimKey] = "Videos gehen bis 60 Sekunden."; Show(AnimKey); return null; }
        var fps = gif ? 15 : 30;
        int pw = (int)Math.Round(v.W / 2) * 2, ph = (int)Math.Round(v.H / 2) * 2; // H.264 braucht gerade Maße
        var dir = Path.Combine(Path.GetTempPath(), "trendo-animation-" + Guid.NewGuid().ToString("N")[..8]);
        output ??= Path.Combine(_core.DataRoot, "animationen", $"{AnimFileName(d.Title)}-{DateTime.Now:yyyyMMdd-HHmmss}{(gif ? ".gif" : ".mp4")}");
        _animBusy = true; _progMsg[AnimKey] = gif ? "GIF entsteht …" : "Video entsteht …"; Show(AnimKey);
        try
        {
            Directory.CreateDirectory(dir); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var n = (int)Math.Ceiling(dur * fps);
            // Bilder mit SkiaSharp im Hintergrund zeichnen – die Oberfläche bleibt bedienbar
            await Task.Run(() => Parallel.For(0, n + 1, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, i =>
                File.WriteAllBytes(Path.Combine(dir, $"f{i:00000}.png"), VectorSkia.Png(MotionKit.PoseDoc(v, Math.Min(dur, (double)i / fps)), pw, null, ph))));
            var pattern = Path.Combine(dir, "f%05d.png");
            bool ok; var why = "";
            if (AnimVideoOverride is { } o) ok = o(pattern, fps, output);
            else
            {
                var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                foreach (var a in gif ? MotionKit.GifArgs(pattern, fps, output) : AnimKit.VideoArgs(pattern, fps, output)) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
                var err = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                ok = p.ExitCode == 0;
                why = err.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
            }
            _progMsg[AnimKey] = ok && File.Exists(output) ? (gif ? "GIF fertig: " : "Video fertig: ") + output : "FFmpeg konnte das Video nicht schreiben." + (why.Length > 0 ? " (" + KreativSafe.Cut(why, 160) + ")" : "");
            if (ok) NotifyFileResult(_progMsg[AnimKey]);
            return ok && File.Exists(output) ? output : null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception) { _progMsg[AnimKey] = ex.Message; return null; }
        finally { _animBusy = false; try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } Show(AnimKey); }
    }

    static string AnimFileName(string s) { var x = string.Concat((s ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim(); return x.Length == 0 ? "animation" : KreativSafe.Cut(x, 60); }

    // ---------------- Fotos verwalten ----------------

    List<Photo>? _photosScan; List<Move>? _photosPlan; bool _photosAsk, _photosBusy;
    /// <summary>T-0725: im Raster gewähltes Foto (Pfad) – groß mit Angaben über dem Raster.</summary>
    string? _photosPick;
    /// <summary>T-0725: Doppelte (zweite und weitere Kopie gleichen Inhalts) des letzten Suchlaufs – einmal je Suche berechnet.</summary>
    HashSet<string>? _photosDups;
    /// <summary>T-0725: höchstens so viele Vorschaubilder im Raster (Bilder laden im Hintergrund, verkleinert).</summary>
    public const int PhotosGridMax = 120;

    static JsonObject PhotosDoc() => new() { ["source"] = "", ["target"] = "", ["scheme"] = "jahr-monat", ["rename"] = "nein", ["mode"] = "kopieren", ["lastLog"] = "", ["lastKind"] = "" };

    private void InitPhotos() => RegisterProgram(FachTabs, new ProgramSpec(PhotosKey, "STUDIO-PHOTOS-01", "Fotos verwalten")
    {
        Sentence = "Fotos nach Aufnahmedatum in Ordner sortieren, einheitlich benennen und Doppelte erkennen – mit Probelauf und Rückgängig.",
        Folder = "Fotos",
        ScrollWorkspace = true,
        Templates = [new("leer", "Fotos einsortieren", "Quelle und Ziel wählen", PhotosDoc)],
        Ribbon =
        [
            ("Ordnung", PhotoOrganizer.Schemes.Select((s, i) => new ProgramCmd("photos-scheme-" + s.Id, s.Label, d => { d.Set("scheme", s.Id, "Ordnung " + s.Label); _photosPlan = null; _photosAsk = false; }, Primary: i == 0)).ToList()),
            ("Fotos", [
                new ProgramCmd("photos-album", "Im Fotoalbum ansehen", d => { _mediaTab = "fotos"; Show("medien"); }, Tip: "Alle Fotos nach Monaten, Favoriten und Alben."),
            ]),
        ],
        Exports = [new("plan", "Probelauf als Liste (.txt)", ".txt", (d, f) => { File.WriteAllText(f, string.Join("\n", (_photosPlan ?? []).Select(m => m.From + " → " + m.To))); return f; })],
        Workspace = PhotosWorkspace,
        Properties = PhotosProperties,
        Status = d => _photosScan is null ? "Noch nicht durchsucht" : $"{_photosScan.Count} Fotos gefunden" + (_photosPlan is null ? "" : $" · {_photosPlan.Count} im Plan"),
        Help = "1. Ordner mit den Fotos wählen. 2. Zielordner wählen. 3. „Probelauf“ zeigt, wohin jedes Foto käme. 4. „Einsortieren“ kopiert oder verschiebt erst nach deinem Ja. Doppelte werden übersprungen, nichts wird überschrieben. „Rückgängig“ macht den letzten Lauf rückgängig.",
    }, "fotos verwalten", "fotos sortieren", "fotos ordnen", "bilder sortieren", "exif", "aufnahmedatum", "doppelte fotos", "fotos umbenennen", "fotosammlung");

    /// <summary>T-0725 (Audit D7, STUDIO-PHOTOS-01: „keine Bildansicht im Programm“): Raster mit echten Vorschaubildern der gefundenen Fotos,
    /// Doppelte markiert, Ziel aus dem Probelauf unter dem Bild. Klick zeigt das Foto groß mit Aufnahmedatum, Größe und Ordner; Doppelklick oder Rechtsklick
    /// „In Bildbearbeitung öffnen“ übergibt es an die Bildbearbeitung. Es wird nichts verändert.</summary>
    Control PhotosGrid(ProgramDocument d, List<Photo> shots)
    {
        var box = new StackPanel { Name = "photos-grid-card", Spacing = 8 };
        var dups = _photosDups ?? [];
        var target = d.Text("target");
        var dest = (_photosPlan ?? []).ToDictionary(m => m.From, m => m.To, StringComparer.OrdinalIgnoreCase);
        if (_photosPick is { } pick && shots.FirstOrDefault(x => string.Equals(x.Path, pick, StringComparison.OrdinalIgnoreCase)) is { } sel)
        {
            var big = new Image { Name = "photos-big", Width = 420, Height = 300, Stretch = Stretch.Uniform };
            Thumb(big, sel.Path, 840);
            var open = new Button { Name = "photos-edit", Content = "In Bildbearbeitung öffnen", Padding = new Thickness(12, 5) };
            open.Click += (_, _) => PhotosEdit(sel.Path);
            var close = new Button { Name = "photos-big-close", Content = "Schließen", Padding = new Thickness(12, 5), Margin = new Thickness(8, 0, 0, 0) };
            close.Click += (_, _) => { _photosPick = null; Show(PhotosKey); };
            box.Children.Add(new StackPanel { Name = "photos-detail", Orientation = Orientation.Horizontal, Spacing = 14, Children = {
                big,
                new StackPanel { Spacing = 4, Width = 320, Children = {
                    new TextBlock { Name = "photos-detail-name", Text = Path.GetFileName(sel.Path), Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    P($"{(sel.FromExif ? "Aufgenommen" : "Datei vom")} {sel.Taken:dd.MM.yyyy HH:mm}", Text, 12.5),
                    P(DiskMapView.Size(sel.Bytes) + (!dups.Contains(sel.Path) ? "" : " · doppelt – wird beim Einsortieren übersprungen"), Muted, 12),
                    P(Path.GetDirectoryName(sel.Path) ?? "", Muted, 11.5),
                    P(dest.TryGetValue(sel.Path, out var to) && target.Length > 0 ? "Käme nach: " + Path.GetRelativePath(target, to) : "", Muted, 11.5),
                    new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { open, close } } } } } });
        }
        box.Children.Add(H(shots.Count > PhotosGridMax ? $"Vorschau: die ersten {PhotosGridMax} von {shots.Count} Fotos" : $"Vorschau: {shots.Count} Fotos", 14));
        var grid = new WrapPanel { Name = "photos-grid" };
        foreach (var (ph, i) in shots.Take(PhotosGridMax).Select((x, i) => (x, i)))
        {
            var dup = dups.Contains(ph.Path);
            var img = new Image { Width = 150, Height = 112, Stretch = Stretch.UniformToFill };
            Thumb(img, ph.Path, 220);
            var tile = new Border
            {
                Name = "photos-tile-" + i, Width = 160, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(5), CornerRadius = new CornerRadius(6), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Background = string.Equals(_photosPick, ph.Path, StringComparison.OrdinalIgnoreCase) ? Brush.Parse("#4A3D1C") : Brush.Parse("#1E2229"),
                BorderBrush = dup ? Brush.Parse("#8A6D2B") : null, BorderThickness = new Thickness(dup ? 2 : 0),
                Child = new StackPanel { Spacing = 2, Children = {
                    img,
                    new TextBlock { Text = ph.Taken.ToString("dd.MM.yyyy", De) + (dup ? " · doppelt" : ""), Foreground = dup ? Accent : Text, FontSize = 11.5 },
                    new TextBlock { Text = dest.TryGetValue(ph.Path, out var t2) && target.Length > 0 ? "→ " + Path.GetDirectoryName(Path.GetRelativePath(target, t2)) : Path.GetFileName(ph.Path), Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis } } },
            };
            ToolTip.SetTip(tile, Path.GetFileName(ph.Path));
            var path = ph.Path;
            tile.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(tile).Properties.IsLeftButtonPressed) return;
                if (e.ClickCount >= 2) { PhotosEdit(path); e.Handled = true; return; }
                _photosPick = path; e.Handled = true; Show(PhotosKey);
            };
            tile.ContextMenu = BueroMenu(PhotosKey, "photos-tile-menu-" + i, () => _photosPick = path,
                ("photos-m-show", "Groß ansehen", () => { }, true),
                ("photos-m-edit", "In Bildbearbeitung öffnen", () => PhotosEdit(path), true),
                ("photos-m-folder", "Im Ordner zeigen", () => Launch(Path.GetDirectoryName(path)!), true));
            grid.Children.Add(tile);
        }
        box.Children.Add(grid);
        box.Children.Add(P("Klick: groß ansehen · Doppelklick: in der Bildbearbeitung öffnen · Gold umrandet: doppelt, wird beim Einsortieren übersprungen.", Muted, 11.5));
        return KrCard(box);
    }

    /// <summary>T-0725: Foto an die Bildbearbeitung übergeben (wie im Fotoalbum).</summary>
    void PhotosEdit(string path) { ProgramRun(PhotosKey, () => ImgAddImage(path)); if (!_progErr.ContainsKey(PhotosKey)) Show("bild"); }

    async Task<string?> PickFolderPath(string title)
    {
        if (FolderPickOverride is { } o) return o(title);
        var r = await PickFolders(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return r.Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }
    /// <summary>Tests: Ordnerwahl ohne Dialog.</summary>
    public static Func<string, string?>? FolderPickOverride { get; set; }

    public async Task PhotosScan(ProgramDocument d)
    {
        var src = d.Text("source");
        if (!KreativSafe.LocalDir(src)) { _progMsg[PhotosKey] = "Wähle zuerst den Ordner mit den Fotos (auf diesem Rechner)."; Show(PhotosKey); return; }
        if (_photosBusy) return;
        _photosBusy = true; _progMsg[PhotosKey] = "Fotos werden gesucht …"; Show(PhotosKey);
        try
        {
            _photosScan = await Task.Run(() => Trendo.Core.Sys.PhotoAlbum.Scan([src], 20000));
            var scanned = _photosScan; _photosPick = null;
            _photosDups = await Task.Run(() => Trendo.Core.Sys.PhotoAlbum.Duplicates(scanned.OrderBy(p => p.Taken).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase)).SelectMany(g => g.Skip(1)).Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase));
            var target = d.Text("target");
            _photosPlan = KreativSafe.LocalPath(target)
                ? await Task.Run(() => PhotoOrganizer.Plan(_photosScan, target, d.Text("scheme"), d.Text("rename") == "ja")) : null;
            _photosAsk = false;
            _progMsg[PhotosKey] = _photosPlan is null ? $"{_photosScan.Count} Fotos gefunden. Wähle noch den Zielordner." : $"Probelauf: {_photosPlan.Count} Fotos würden einsortiert. Es wurde noch nichts verändert.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { _progMsg[PhotosKey] = ex.Message; }
        finally { _photosBusy = false; Show(PhotosKey); }
    }

    public void PhotosApply(ProgramDocument d)
    {
        if (_photosPlan is not { Count: > 0 } plan) throw new InvalidOperationException("Starte zuerst den Probelauf.");
        var copy = d.Text("mode") != "verschieben";
        string? log = null;
        try { log = PhotoOrganizer.Run(plan, PhotosUndoDir, copy); }
        finally
        {
            // auch nach einem Abbruch: das Protokoll gibt es, „Rückgängig“ bleibt möglich
            var written = log ?? Directory.EnumerateFiles(PhotosUndoDir, "fotos-*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (written is not null) d.Edit("Fotos einsortiert", x => { x["lastLog"] = written; x["lastKind"] = copy ? "kopieren" : "verschieben"; });
            _photosPlan = null; _photosAsk = false; _photosScan = null;
        }
        var n = System.Text.Json.JsonSerializer.Deserialize<PhotoOrganizer.RunLog>(File.ReadAllText(log))?.Items.Count ?? 0;
        _core.Audit.Append(Principal.LocalOwner.Name, "fotos.einsortieren", $"{n} {(copy ? "kopiert" : "verschoben")}");
        _progMsg[PhotosKey] = $"{n} Fotos {(copy ? "kopiert" : "verschoben")}" + (n < plan.Count ? $", {plan.Count - n} lagen schon dort oder fehlten" : "") + ". „Rückgängig“ setzt es zurück.";
    }

    string PhotosUndoDir => Path.Combine(_core.DataRoot, "werkzeuge", "rueckgaengig");

    public void PhotosUndo(ProgramDocument d)
    {
        var log = d.Text("lastLog");
        // nur eigene Protokolle: eine fremde Projektdatei darf auf keine andere Datei zeigen
        var inOwn = KreativSafe.LocalFile(log) && Path.GetDirectoryName(Path.GetFullPath(log))!.Equals(Path.GetFullPath(PhotosUndoDir), StringComparison.OrdinalIgnoreCase);
        if (!inOwn) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen.");
        var (ok, skip) = PhotoOrganizer.Undo(log);
        var copy = d.Text("lastKind") == "kopieren";
        _progMsg[PhotosKey] = (copy ? $"{ok} Kopien entfernt. Die Originale sind unverändert." : $"{ok} Fotos liegen wieder am alten Platz.") + (skip > 0 ? $" {skip} wurden inzwischen geändert oder ersetzt und bleiben, wo sie sind." : "");
        d.Edit("Rückgängig", x => { x["lastLog"] = ""; x["lastKind"] = ""; });
    }

    Control PhotosWorkspace(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "photos-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        Control FolderRow(string key, string name, string label, string hint)
        {
            var path = d.Text(key);
            var b = new Button { Name = name, Content = path.Length > 0 ? "Ändern …" : "Ordner wählen …", Padding = new Thickness(12, 5) };
            b.Click += async (_, _) => { if (await PickFolderPath(label) is { } f) ProgramRun(PhotosKey, () => { d.Set(key, f, label); _photosPlan = null; _photosAsk = false; Show(PhotosKey); }); };
            return KrCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new StackPanel { Spacing = 2, Children = { H(label, 14), P(path.Length > 0 ? path : hint, path.Length > 0 ? Text : Muted, 12) } }, Pos(b, 1) } });
        }
        sp.Children.Add(FolderRow("source", "photos-source", "1 · Fotos aus", "Ordner mit deinen Fotos, auch mit Unterordnern."));
        sp.Children.Add(FolderRow("target", "photos-target", "2 · Einsortieren nach", "Zielordner, z. B. „Bilder\\Sortiert“."));
        var run = new Button { Name = "photos-scan", Content = _photosBusy ? "Suche läuft …" : "3 · Probelauf", Padding = new Thickness(14, 6), IsEnabled = !_photosBusy };
        run.Classes.Add("accent");
        run.Click += (_, _) => _ = PhotosScan(d);
        var actions = new WrapPanel { Children = { run } };
        if (_photosPlan is { Count: > 0 } plan)
        {
            var verb = d.Text("mode") == "verschieben" ? "verschieben" : "kopieren";
            var go = new Button { Name = "photos-apply", Content = _photosAsk ? $"Ja, {plan.Count} Fotos {verb}" : "4 · Einsortieren …", Padding = new Thickness(14, 6), Margin = new Thickness(8, 0, 0, 0) };
            if (_photosAsk) go.Classes.Add("accent");
            go.Click += (_, _) => ProgramRun(PhotosKey, () => { if (!_photosAsk) { _photosAsk = true; _progMsg[PhotosKey] = $"Wirklich {plan.Count} Fotos {verb}? Nochmal klicken zum Bestätigen."; } else PhotosApply(d); Show(PhotosKey); });
            actions.Children.Add(go);
        }
        if (d.Text("lastLog").Length > 0)
        {
            var undo = new Button { Name = "photos-undo", Content = "Rückgängig", Padding = new Thickness(12, 6), Margin = new Thickness(8, 0, 0, 0) };
            undo.Click += (_, _) => ProgramRun(PhotosKey, () => { PhotosUndo(d); Show(PhotosKey); });
            actions.Children.Add(undo);
        }
        sp.Children.Add(actions);
        if (_photosScan is { } scan)
        {
            var (years, noExif, dups) = PhotoOrganizer.Summary(scan);
            var sum = new StackPanel { Name = "photos-summary", Spacing = 4 };
            sum.Children.Add(H($"{scan.Count} Fotos gefunden", 14));
            var row = new WrapPanel();
            foreach (var (y, n) in years.Take(12))
                row.Children.Add(new Border { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(12), Background = Brush.Parse("#2A3A55"), Child = new TextBlock { Text = $"{y}: {n}", Foreground = Text, FontSize = 12 } });
            sum.Children.Add(row);
            sum.Children.Add(P($"{noExif} ohne Aufnahmedatum (dann zählt das Änderungsdatum) · {dups} doppelt (werden übersprungen)", Muted, 12));
            sp.Children.Add(KrCard(sum));
        }
        if (_photosScan is { Count: > 0 } shots) sp.Children.Add(PhotosGrid(d, shots));
        if (_photosPlan is { } pl)
        {
            var list = new StackPanel { Name = "photos-plan", Spacing = 2 };
            list.Children.Add(H($"Probelauf: {pl.Count} Fotos", 14));
            var target = d.Text("target");
            foreach (var m in pl.Take(200))
                list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,24,*"), Children = {
                    new TextBlock { Text = Path.GetFileName(m.From), Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                    Pos(new TextBlock { Text = "→", Foreground = Accent, FontSize = 12 }, 1),
                    Pos(new TextBlock { Text = Path.GetRelativePath(target, m.To), Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, 2) } });
            if (pl.Count > 200) list.Children.Add(P($"… und {pl.Count - 200} weitere.", Muted, 12));
            sp.Children.Add(KrCard(list));
        }
        return sp;
    }

    Control PhotosProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Einstellungen", 13));
        var rename = new CheckBox { Name = "photos-rename", Content = "Nach Datum umbenennen (2026-11-11 14-30-05.jpg)", IsChecked = d.Text("rename") == "ja" };
        rename.IsCheckedChanged += (_, _) => ProgramRun(PhotosKey, () => { d.Set("rename", rename.IsChecked == true ? "ja" : "nein", "Umbenennen"); _photosPlan = null; _photosAsk = false; Show(PhotosKey); });
        sp.Children.Add(rename);
        var copy = new RadioButton { Name = "photos-mode-kopieren", GroupName = "photos-mode", Content = "Kopieren (Originale bleiben)", IsChecked = d.Text("mode") != "verschieben" };
        var move = new RadioButton { Name = "photos-mode-verschieben", GroupName = "photos-mode", Content = "Verschieben", IsChecked = d.Text("mode") == "verschieben" };
        copy.IsCheckedChanged += (_, _) => { if (copy.IsChecked == true && d.Text("mode") != "kopieren") ProgramRun(PhotosKey, () => { d.Set("mode", "kopieren", "Kopieren"); _photosAsk = false; Show(PhotosKey); }); };
        move.IsCheckedChanged += (_, _) => { if (move.IsChecked == true && d.Text("mode") != "verschieben") ProgramRun(PhotosKey, () => { d.Set("mode", "verschieben", "Verschieben"); _photosAsk = false; Show(PhotosKey); }); };
        sp.Children.Add(copy); sp.Children.Add(move);
        sp.Children.Add(P($"Ordnung: {PhotoOrganizer.Schemes.FirstOrDefault(s => s.Id == d.Text("scheme")).Label ?? "Jahr / Monat"} (oben umstellen)", Muted, 11));
        sp.Children.Add(P("Nichts wird überschrieben. Gleiche Namen bekommen „ (2)“.", Muted, 11));
        return sp;
    }

    // ---------------- Bilder mit KI ----------------

    /// <summary>Tests: malt statt der Bild-KI (Beschreibung, Startwert, Ziel-PNG, Breite, Höhe → erfolgreich?).</summary>
    public static Func<string, int, string, int, int, Task<bool>>? ImageAiOverride { get; set; }
    bool _imgAiBusy;
    /// <summary>T-0738 Echtlauf: das isolierte Testfenster darf die echte Bild-KI aus <see cref="DependencyManager.ToolsRoot"/> starten
    /// (wie <c>CodeRealToolsInTests</c>). Der Weg ist derselbe wie in der App – nichts wird ersetzt.</summary>
    public bool KiRealToolsInTests { get; set; }
    /// <summary>Rohausgabe der letzten sd-cli-Läufe (Befehl, Ausgabe, Fehlerausgabe, Exitcode) – für Fehlermeldungen und Belege.</summary>
    public string ImgAiLastLog { get; private set; } = "";

    /// <summary>T-0738: Was genau fehlt der Bild-KI? null = alles da. Nennt Programm und Modell einzeln, damit die Meldung ehrlich sagt, was einzurichten ist.</summary>
    public (string Short, string Long)? ImgAiMissing()
    {
        if (ImageAiOverride is not null) return null;
        var store = KiStore;
        var exe = store.RuntimeReady(KiRuntime.StableDiffusionCpp);
        var model = KiLibrary.Models.Any(m => m.Id is "sdxl-turbo" or "sd-turbo" && store.ModelReady(m));
        if (exe && model) return _opt.Isolated && !KiRealToolsInTests ? ("Bild-KI im Testmodus aus", "Im Testmodus startet Trendo keine Bild-KI.") : null;
        var what = !exe && !model ? "das Programm stable-diffusion.cpp (sd-cli.exe) und ein Bildmodell (SDXL Turbo, 6,9 GB, oder SD Turbo, 5,2 GB)"
            : !exe ? "das Programm stable-diffusion.cpp (sd-cli.exe)" : "ein Bildmodell (SDXL Turbo, 6,9 GB, oder SD Turbo, 5,2 GB)";
        var shortText = !exe && !model ? "Bild-KI fehlt" : !exe ? "Bildprogramm fehlt" : "Bildmodell fehlt"; // kurz: der Hinweis steht im Menüband (V-01: Arbeitsfläche oben)
        return (shortText, $"Die Bild-KI ist noch nicht eingerichtet. Es fehlt {what}. So richtest du sie ein: oben „Bild-KI einrichten“ öffnet die KI-Bibliothek, dort unter „Bilder erzeugen“ SDXL Turbo oder SD Turbo wählen (kostenlos, nur mit deiner Zustimmung).");
    }

    static JsonObject ImgDoc(string prompt, string style) => new() { ["dir"] = Guid.NewGuid().ToString("N"), ["prompt"] = prompt, ["style"] = style, ["format"] = "quadrat", ["count"] = "2", ["seed"] = "42", ["negative"] = "", ["images"] = new JsonArray() };
    /// <summary>Bildordner je Dokument. Die Kennung steht im Dokument („dir“), damit die Bilder nach dem Wiederöffnen da sind (T-0619).</summary>
    static bool ImgDirOk(string dir) => System.Text.RegularExpressions.Regex.IsMatch(dir, "^[0-9a-f]{32}$");
    string ImgDir(ProgramDocument d) => Path.Combine(_core.DataRoot, "ki-bilder", ImgDirOk(d.Text("dir")) ? d.Text("dir") : d.Id);

    /// <summary>Nur eigene KI-Bilder: PNG im Bildordner dieses Dokuments, höchstens 25 MB. Eine fremde Projektdatei kann so keine anderen Dateien in einen Export ziehen.</summary>
    List<string> ImgList(ProgramDocument d)
    {
        var own = Path.GetFullPath(ImgDir(d)) + Path.DirectorySeparatorChar;
        return d.List("images").Select(Js).Where(p => p.Length > 0 && KreativSafe.LocalFile(p, KreativSafe.MaxImageBytes)
            && Path.GetFullPath(p).StartsWith(own, StringComparison.OrdinalIgnoreCase) && Path.GetExtension(p).Equals(".png", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void InitImageAi() => RegisterProgram(FachTabs, new ProgramSpec(ImgAiKey, "STUDIO-IMAGEAI-01", "Bilder mit KI")
    {
        Sentence = "Bilder aus einer Beschreibung malen – lokal auf deinem Rechner, mit Stil, Format und Varianten. Jedes Bild bekommt einen Herkunftsnachweis.",
        Folder = "KI-Bilder",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Neues Bild", "Beschreibung eintragen", () => ImgDoc("", "foto")),
            new("beispiel", "Beispiel: Leuchtturm", "Leuchtturm bei Sonnenuntergang", () => ImgDoc("ein alter Leuchtturm auf einer Klippe bei Sonnenuntergang, Wellen, Möwen", "foto")),
        ],
        Ribbon =
        [
            ("Malen", [
                new ProgramCmd("img-generate", "Bilder malen", d => _ = ImgGenerate(d), Primary: true, Tip: "Nutzt SDXL Turbo oder SD Turbo aus der KI-Bibliothek. Alles bleibt auf deinem Rechner."),
                new ProgramCmd("img-setup", "Bild-KI einrichten", d => Show("ki-bibliothek"), Tip: "Öffnet die KI-Bibliothek."),
            ]),
            ("Stil", ImageAiKit.Styles.Select(s => new ProgramCmd("img-style-" + s.Id, s.Label, d => d.Set("style", s.Id, "Stil " + s.Label))).ToList()),
            ("Format", ImageAiKit.Formats.Select(f => new ProgramCmd("img-format-" + f.Id, f.Label, d => d.Set("format", f.Id, "Format " + f.Label))).ToList()),
        ],
        GroupLevels = new() { ["Format"] = 1 },
        Exports =
        [
            new("png", "Neuestes Bild (.png)", ".png", (d, f) => { var l = ImgList(d).LastOrDefault(p => KreativSafe.LocalFile(p)) ?? throw new InvalidOperationException("Noch kein Bild gemalt."); File.Copy(l, f, true); if (File.Exists(l + ".json")) File.Copy(l + ".json", f + ".json", true); return f; }),
            new("zip", "Alle Bilder mit Nachweis (.zip)", ".zip", ImgZip),
        ],
        Workspace = ImgAiWorkspace,
        Properties = ImgAiProperties,
        Status = d => (ImageAiOverride is not null ? "Bild-KI: Test" : ImgAiMissing() is null && CoverModel() is { } m ? "Bild-KI: " + m.Model.Name : "Bild-KI nicht eingerichtet") + $" · {ImgList(d).Count} Bilder",
        // V-09 (T-0700): fehlt die Bild-KI, steht das direkt an „Bilder malen“ – nicht nur in der Statuszeile
        Blocker = _ => ImgAiMissing() is { } miss ? new ProgramBlocker(miss.Short, "Jetzt einrichten", () => Show("ki-bibliothek"), "img-setup") : null,
        Help = "Beschreibe, was auf dem Bild sein soll, wähle oben Stil und Format und klicke „Bilder malen“. Beim ersten Mal richtest du die Bild-KI in der KI-Bibliothek ein. Keine echten Personen, keine fremden Marken oder Figuren. Neben jedem Bild liegt eine Datei mit dem Herkunftsnachweis.",
    }, "bild ki", "bilder mit ki", "ki bild", "bild erzeugen", "bild malen", "text zu bild", "stable diffusion", "sdxl", "ki-kunst", "illustration erzeugen");

    string ImgZip(ProgramDocument d, string file)
    {
        var imgs = ImgList(d).Where(p => KreativSafe.LocalFile(p)).ToList();
        if (imgs.Count == 0) throw new InvalidOperationException("Noch kein Bild gemalt.");
        if (File.Exists(file)) File.Delete(file);
        using var z = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (var p in imgs)
        {
            z.CreateEntryFromFile(p, Path.GetFileName(p));
            if (File.Exists(p + ".json")) z.CreateEntryFromFile(p + ".json", Path.GetFileName(p) + ".json");
        }
        return file;
    }

    public async Task<int> ImgGenerate(ProgramDocument d)
    {
        if (_imgAiBusy) { _progMsg[ImgAiKey] = "Es wird schon gemalt – einen Moment."; Show(ImgAiKey); return 0; }
        var prompt = d.Text("prompt");
        if (ImageAiKit.Blocked(prompt) is { } why) { _progMsg[ImgAiKey] = why; Show(ImgAiKey); return 0; }
        var cm = ImageAiOverride is null ? CoverModel() : null;
        if (ImgAiMissing() is not null || (ImageAiOverride is null && cm is null)) { _progMsg[ImgAiKey] = ImgAiMissing()?.Long ?? "Die Bild-KI ist noch nicht eingerichtet."; Show(ImgAiKey); return 0; }
        var xl = cm?.Model.Id != "sd-turbo";
        var (w, h) = ImageAiKit.Size(d.Text("format"), xl ? 1024 : 512);
        var count = int.TryParse(d.Text("count"), out var c) ? Math.Clamp(c, 1, 4) : 2;
        var baseSeed = int.TryParse(d.Text("seed"), out var s) ? s : 42;
        if (!ImgDirOk(d.Text("dir"))) d.Set("dir", Guid.NewGuid().ToString("N"), "Bildordner"); // ältere Dokumente: Ordner einmal festlegen
        var dir = ImgDir(d);
        Directory.CreateDirectory(dir);
        _imgAiBusy = true; var made = new List<string>(); string? why2 = null; ImgAiLastLog = "";
        try
        {
            for (var i = 0; i < count; i++)
            {
                var seed = ImageAiKit.Seed(baseSeed, i); // gleicher Startwert = gleiche Bilder
                _progMsg[ImgAiKey] = $"Male Bild {i + 1} von {count} …"; Show(ImgAiKey);
                var png = Path.Combine(dir, $"bild-{DateTime.Now:yyyyMMdd-HHmmss}-{seed}.png");
                bool ok;
                using (await GpuSlot("bild", "Bild mit KI"))
                {
                    if (ImageAiOverride is { } o) ok = await o(ImageAiKit.FullPrompt(prompt, d.Text("style")), seed, png, w, h);
                    else
                    {
                        var args = ImageAiKit.Args(KiStore.ModelFile(cm!.Value.Model, cm.Value.Model.Files[0]), prompt, d.Text("style"), d.Text("negative"), png, w, h, seed, xl);
                        var r = await Task.Run(() => RunWithInput(cm.Value.Exe, args, null));
                        ImgAiLog(cm.Value.Exe, args, r);
                        if (!r.Ok || !File.Exists(png)) { r = await Task.Run(() => RunWithInput(cm.Value.Exe, [.. args, "--offload-to-cpu"], null)); ImgAiLog(cm.Value.Exe, [.. args, "--offload-to-cpu"], r); }
                        ok = r.Ok;
                        if (!ok || !File.Exists(png)) why2 = ImageAiKit.ErrorHint(r.Err + "\n" + r.Out);
                    }
                }
                if (!ok || !File.Exists(png)) { _progMsg[ImgAiKey] = "Die Bild-KI hat kein Bild geliefert. " + (why2 ?? "Ist die Grafikkarte gerade mit etwas anderem beschäftigt?"); break; }
                ImageAiKit.WriteProvenance(png, new("stable-diffusion.cpp", cm?.Model.Id ?? "test", prompt, d.Text("style"), d.Text("negative"), seed, w, h, DateTime.UtcNow, ""));
                made.Add(png);
                d.Edit("Bild gemalt", x => { if (x["images"] is not JsonArray a) x["images"] = a = []; a.Add(png); });
            }
            if (made.Count > 0) { _progMsg[ImgAiKey] = $"{made.Count} Bild(er) fertig. Neben jedem Bild liegt der Herkunftsnachweis."; _core.Audit.Append(Principal.LocalOwner.Name, "bild-ki.malen", $"{made.Count} Bilder"); }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _progMsg[ImgAiKey] = ex.Message; }
        finally { _imgAiBusy = false; Show(ImgAiKey); }
        return made.Count;
    }

    /// <summary>Rohausgabe eines sd-cli-Laufs festhalten (Befehl mit Argumenten, Exitcode, Ausgabe).</summary>
    void ImgAiLog(string exe, IReadOnlyList<string> args, Cli.Result r) =>
        ImgAiLastLog += $"$ {Path.GetFileName(exe)} {string.Join(' ', args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a))}\n[Exitcode {r.Exit}]\n{r.Out}{(r.Err.Length > 0 ? "\n[Fehlerausgabe]\n" + r.Err : "")}\n";

    Control ImgAiWorkspace(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "img-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        var box = KrBox(d, "prompt", "img-prompt", "Was soll auf dem Bild sein? z. B. „ein alter Leuchtturm bei Sonnenuntergang“", true);
        box.MinHeight = 80; box.FontSize = 15;
        var style = ImageAiKit.Styles.FirstOrDefault(x => x.Id == d.Text("style")) ?? ImageAiKit.Styles[0];
        var fmt = ImageAiKit.Formats.FirstOrDefault(x => x.Id == d.Text("format")); if (fmt.Id is null) fmt = ImageAiKit.Formats[0];
        var go = new Button { Name = "img-go", Content = _imgAiBusy ? "Malt …" : "Bilder malen", Padding = new Thickness(18, 8), IsEnabled = !_imgAiBusy, FontSize = 14 };
        go.Classes.Add("accent");
        go.Click += (_, _) => _ = ImgGenerate(d);
        sp.Children.Add(KrCard(new StackPanel { Spacing = 8, Children = { box, new WrapPanel { Children = { go, new TextBlock { Text = $"Stil: {style.Label} · Format: {fmt.Label}", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) } } } } }, "img-card"));
        if (ImageAiKit.Blocked(d.Text("prompt")) is { } why && d.Text("prompt").Length > 0) sp.Children.Add(P("⚠ " + why, Brush.Parse("#F5A524"), 12));
        var imgs = ImgList(d).Where(p => KreativSafe.LocalFile(p, KreativSafe.MaxImageBytes)).Reverse().ToList();
        var gallery = new WrapPanel { Name = "img-gallery" };
        foreach (var p in imgs.Take(48))
        {
            var img = new Image { Stretch = Stretch.Uniform, Height = 240 };
            try { using var fs = File.OpenRead(p); img.Source = Bitmap.DecodeToHeight(fs, 480); }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { continue; }
            var open = new Button { Content = "Groß ansehen", Padding = new Thickness(8, 3), FontSize = 11 };
            var path = p; open.Click += (_, _) => { PhotoOpenForPreview(path); _mediaTab = "fotos"; Show("medien"); };
            gallery.Children.Add(new Border { Margin = new Thickness(0, 0, 12, 12), CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Brush.Parse("#0A0F1A"), BoxShadow = BoxShadows.Parse("0 6 18 0 #66000000"),
                Child = new StackPanel { Children = { img, new DockPanel { Margin = new Thickness(8, 6), Children = { new TextBlock { Text = "KI-Bild", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, new Border { HorizontalAlignment = HorizontalAlignment.Right, Child = open } } } } } });
        }
        sp.Children.Add(imgs.Count == 0 ? P("Noch keine Bilder. Beschreibung eintragen und „Bilder malen“.", Muted, 12) : gallery);
        return sp;
    }

    Control ImgAiProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        void Txt(string key, string label, bool multi = false) { sp.Children.Add(P(label, null, 11)); sp.Children.Add(KrBox(d, key, "img-p-" + key, label, multi)); }
        Txt("count", "Anzahl Varianten (1–4)");
        Txt("seed", "Startwert (gleich = gleiches Bild)");
        Txt("negative", "Nicht im Bild (optional)", true);
        sp.Children.Add(H("Regeln", 13));
        sp.Children.Add(P("Keine echten Personen, keine fremden Marken, Logos oder bekannte Figuren. KI-Bilder als solche kennzeichnen, wenn du sie veröffentlichst.", Muted, 11));
        return sp;
    }
}
