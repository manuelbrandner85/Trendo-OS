using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Business;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Etappe „Business &amp; Geld“ Teil B (T-0676): Flyer &amp; Visitenkarten und Verkaufsseiten.</summary>
public partial class MainWindow
{
    // ======================================================================
    // Flyer & Visitenkarten (STUDIO-FLYER-01)
    // ======================================================================

    static JsonObject FlyItem(int side, string kind, double x, double y, double w, double h, string text = "", double size = 12, string color = "#111111", string fill = "", bool bold = false, string align = "links") => new()
    {
        ["id"] = NewId("e"), ["side"] = side, ["kind"] = kind, ["x"] = x, ["y"] = y, ["w"] = w, ["h"] = h, ["text"] = text, ["size"] = size, ["color"] = color, ["fill"] = fill, ["bold"] = bold, ["align"] = align, ["src"] = "",
    };

    static JsonObject FlyDoc(string format, string bg, IEnumerable<JsonObject> items, string bg2 = "#FFFFFF") => new()
    {
        ["format"] = format, ["bleed"] = 3, ["bg"] = bg, ["bg2"] = bg2, ["items"] = new JsonArray([.. items.Select(i => (JsonNode)i)]), ["side"] = 0, ["sel"] = "", ["marks"] = true, ["guides"] = true,
    };

    static FlyerItem FlyOf(JsonObject o) => new(Js(o["id"]), (int)ProgramDocument.ToNumber(o["side"]), Js(o["kind"]), ProgramDocument.ToNumber(o["x"]), ProgramDocument.ToNumber(o["y"]),
        ProgramDocument.ToNumber(o["w"], 10), ProgramDocument.ToNumber(o["h"], 10), Js(o["text"]), ProgramDocument.ToNumber(o["size"], 12), Js(o["color"]) is { Length: > 0 } c ? c : "#111111",
        Js(o["fill"]), o["bold"].AsBool(), Js(o["align"]) is { Length: > 0 } a ? a : "links", Js(o["src"]));

    public FlyerDoc FlyerModel(ProgramDocument d) => new(FlyerKit.Formats.Any(f => f.Id == d.Text("format")) ? d.Text("format") : "a5", d.Text("bg", "#FFFFFF"), d.Text("bg2", "#FFFFFF"),
        [.. BzList(d, "items").Select(FlyOf)], d.Number("bleed", 3));
    public List<FlyerFinding> FlyerCheck(ProgramDocument d) => FlyerKit.Check(FlyerModel(d), BizImageSize);

    /// <summary>Element einfügen (Text, Fläche, Kreis, Linie, QR-Code, Bild) – in die Mitte der aktuellen Seite, innerhalb des Sicherheitsabstands.</summary>
    public string FlyerAdd(ProgramDocument d, string kind, string text = "")
    {
        var f = FlyerKit.Format(FlyerModel(d).Format); var side = (int)d.Number("side");
        var s = f.SafeMm; var w = Math.Min(f.WMm - 2 * s, kind is "qr" ? 22 : kind == "text" ? 60 : 40); var h = kind switch { "qr" => 22, "line" => 0.5, "text" => 14, _ => Math.Min(30, f.HMm - 2 * s) };
        var o = FlyItem(side, kind, Math.Round((f.WMm - w) / 2, 1), Math.Round((f.HMm - h) / 2, 1), w, h,
            text.Length > 0 ? text : kind switch { "text" => "Neuer Text", "qr" => "https://example.org", _ => "" }, kind == "text" ? 14 : 12, kind is "rect" or "ellipse" ? "#B45309" : "#111111");
        d.Edit(kind switch { "text" => "Text", "qr" => "QR-Code", "image" => "Bild", "line" => "Linie", "ellipse" => "Kreis", _ => "Fläche" } + " eingefügt", x =>
        {
            if (x["items"] is not JsonArray a) x["items"] = a = [];
            a.Add(o); x["sel"] = Js(o["id"]);
        });
        return Js(o["id"]);
    }

    /// <summary>Element verschieben/skalieren (mm) – ein Rückgängig-Schritt.</summary>
    public void FlyerPlace(ProgramDocument d, string id, double x, double y, double? w = null, double? h = null) =>
        d.Edit("Verschieben", j =>
        {
            var o = (j["items"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(o => Js(o["id"]) == id) ?? throw new InvalidOperationException("Dieses Element gibt es nicht mehr.");
            o["x"] = Math.Round(x, 1); o["y"] = Math.Round(y, 1);
            if (w is { } ww) o["w"] = Math.Round(Math.Max(1, ww), 1);
            if (h is { } hh) o["h"] = Math.Round(Math.Max(0.2, hh), 1);
        });

    /// <summary>Element in den Sicherheitsabstand ziehen (Text, QR) bzw. randabfallend bis in den Beschnitt (Fläche, Bild).</summary>
    public void FlyerFix(ProgramDocument d, string id)
    {
        var m = FlyerModel(d); var f = FlyerKit.Format(m.Format); var it = m.Items.First(i => i.Id == id);
        if (it.Kind is "text" or "qr")
        {
            var s = f.SafeMm; var w = Math.Min(it.W, f.WMm - 2 * s); var h = Math.Min(it.H, f.HMm - 2 * s);
            FlyerPlace(d, id, Math.Clamp(it.X, s, f.WMm - s - w), Math.Clamp(it.Y, s, f.HMm - s - h), w, h);
        }
        else
        {
            double x = it.X, y = it.Y, r = it.X + it.W, b = it.Y + it.H, bl = m.BleedMm;
            if (x < 1.5) x = -bl; if (y < 1.5) y = -bl; if (r > f.WMm - 1.5) r = f.WMm + bl; if (b > f.HMm - 1.5) b = f.HMm + bl;
            FlyerPlace(d, id, x, y, r - x, b - y);
        }
    }

    async Task FlyerPickImage(ProgramDocument d, string? id)
    {
        var file = await PickMediaFile("flyer-image", "Bild für den Flyer", ["*.jpg", "*.jpeg", "*.png"]);
        if (file is null) return;
        ProgramRun(FlyerKey, () => FlyerSetImage(d, id, file));
    }

    public string FlyerSetImage(ProgramDocument d, string? id, string file)
    {
        if (BizImageSize(file) is not { } px) throw new InvalidDataException("Das ist kein Bild, das Trendo lesen kann.");
        if (id is null || BzList(d, "items").All(o => Js(o["id"]) != id || Js(o["kind"]) != "image")) id = FlyerAdd(d, "image");
        var it = FlyOf(BzItem(d, "items", id));
        var h = Math.Round(it.W * px.H / px.W, 1);
        d.Edit("Bild", j => { var o = (j["items"] as JsonArray)!.OfType<JsonObject>().First(o => Js(o["id"]) == id); o["src"] = file; o["h"] = h; });
        var dpi = FlyerKit.EffectiveDpi(px.W, it.W);
        _progMsg[FlyerKey] = $"Bild gesetzt: {px.W} × {px.H} px ergeben bei {it.W:0} mm Breite {dpi:0} dpi" + (dpi < 300 ? " – für den Druck kleiner ziehen oder ein größeres Bild nehmen." : ".");
        return id;
    }

    string FlyerPdf(ProgramDocument d, string f, bool marks)
    {
        var errs = FlyerCheck(d).Where(x => x.Error).ToList();
        var n = FlyerKit.Pdf(FlyerModel(d), f, marks, BizRead);
        var (w, h) = FlyerKit.DataSize(FlyerModel(d));
        _progMsg[FlyerKey] = $"Druck-PDF mit {n} Seite(n), Datenformat {w:0.#} × {h:0.#} mm" + (marks ? " plus Schnittmarken" : "") + "." + (errs.Count > 0 ? $" Achtung: {errs.Count} Fehler aus der Prüfung sind noch offen." : "");
        return f;
    }

    static readonly Dictionary<string, Bitmap> _qrBmp = [];
    static Bitmap? FlyQrBitmap(string text, string color)
    {
        var key = color + "|" + text; if (_qrBmp.TryGetValue(key, out var b)) return b;
        bool[,] q; try { q = QrMatrix.Encode(text); } catch (InvalidOperationException) { return null; }
        var n = q.GetLength(0); const int cell = 6; var size = (n + 8) * cell; var px = new Pixmap(size, size); px.Fill(255, 255, 255, 255);
        var (r, g, bl) = PaintColors.IsHex(color) ? PaintColors.Rgb(color) : ((byte)0, (byte)0, (byte)0);
        for (var y = 0; y < n; y++) for (var x = 0; x < n; x++) if (q[y, x])
            for (var yy = 0; yy < cell; yy++) for (var xx = 0; xx < cell; xx++) { var i = (((y + 4) * cell + yy) * size + (x + 4) * cell + xx) * 4; px.Px[i] = r; px.Px[i + 1] = g; px.Px[i + 2] = bl; }
        if (_qrBmp.Count > 40) _qrBmp.Clear();
        return _qrBmp[key] = new Bitmap(new MemoryStream(PngCodec.Encode(px)));
    }

    private void InitFlyer() => RegisterProgram(FachTabs, new ProgramSpec(FlyerKey, "STUDIO-FLYER-01", "Flyer & Visitenkarten")
    {
        Sentence = "Flyer, Faltflyer und Visitenkarten druckfertig gestalten – mit 3 mm Beschnitt, Sicherheitsabstand, Schnittmarken und QR-Code.",
        Folder = "Flyer",
        GroupLevels = new() { ["Einfügen"] = 0, ["Seite"] = 0, ["Prüfen"] = 0, ["Anordnen"] = 1 },
        Templates =
        [
            new("visitenkarte", "Visitenkarte", "85 × 55 mm, vorne Name, hinten QR-Code", () => FlyDoc("visitenkarte", "#FFFFFF", [
                FlyItem(0, "rect", -3, -3, 28, 61, fill: "#B45309"),
                FlyItem(0, "text", 30, 12, 50, 9, "Erika Muster", 13, "#111111", bold: true),
                FlyItem(0, "text", 30, 21, 50, 6, "Konditormeisterin", 8.5, "#6B7280"),
                FlyItem(0, "text", 30, 33, 50, 16, "Telefon 01234 56789\nkontakt@example.org\nexample.org", 7.5, "#111111"),
                FlyItem(1, "text", 6, 20, 46, 14, "Bäckerei Muster", 12, "#FFFFFF", bold: true),
                FlyItem(1, "qr", 58, 16, 22, 22, "https://example.org", 12, "#111111"),
            ], bg2: "#1F2937")),
            new("flyer-a5", "Flyer A5: Veranstaltung", "148 × 210 mm, Bildfläche, Titel, Termin, QR-Code", () => FlyDoc("a5", "#FFFFFF", [
                FlyItem(0, "rect", -3, -3, 154, 98, fill: "#1F2937"),
                FlyItem(0, "text", 10, 30, 128, 22, "Sommerfest", 36, "#FDE68A", bold: true, align: "mitte"),
                FlyItem(0, "text", 10, 58, 128, 10, "Samstag, 20. Juni · ab 14 Uhr", 14, "#FFFFFF", align: "mitte"),
                FlyItem(0, "text", 12, 108, 124, 46, "Musik, Kuchen und Spiele für die ganze Familie.\nEintritt frei.\n\nGemeindehaus, Hauptstraße 1, 12345 Musterstadt", 12, "#111111"),
                FlyItem(0, "qr", 112, 170, 26, 26, "https://example.org/sommerfest"),
                FlyItem(0, "text", 12, 176, 96, 14, "Alle Infos: example.org/sommerfest", 10, "#6B7280"),
            ])),
            new("dinlang", "DIN lang: Angebot", "99 × 210 mm, schmal für Briefumschläge", () => FlyDoc("dinlang", "#FFFBEB", [
                FlyItem(0, "text", 8, 14, 83, 30, "Frühstück\nzum Mitnehmen", 22, "#7C2D12", bold: true),
                FlyItem(0, "line", 8, 48, 40, 0.8, color: "#B45309"),
                FlyItem(0, "text", 8, 56, 83, 80, "Jeden Morgen ab 6 Uhr:\n\n• Kaffee und Brötchen 3,50 €\n• Müsli-Becher 4,20 €\n• Zimtschnecke 2,40 €", 12, "#1F2937"),
                FlyItem(0, "qr", 8, 170, 24, 24, "https://example.org"),
                FlyItem(0, "text", 36, 176, 55, 14, "Bäckerei Muster\nHauptstraße 1", 9, "#6B7280"),
            ])),
            new("falt", "Faltflyer A4 (6 Seiten)", "Wickelfalz auf DIN lang, innen und außen", () => FlyDoc("falt-a4", "#FFFFFF", [
                FlyItem(0, "text", 8, 16, 83, 20, "Über uns", 18, "#111111", bold: true),
                FlyItem(0, "text", 8, 40, 83, 120, "Seit 1990 backen wir mit regionalem Mehl.", 11, "#1F2937"),
                FlyItem(0, "rect", 198, -3, 102, 216, fill: "#B45309"),
                FlyItem(0, "text", 206, 80, 83, 30, "Bäckerei Muster", 22, "#FFFFFF", bold: true, align: "mitte"),
                FlyItem(1, "text", 8, 16, 280, 20, "Unser Sortiment", 18, "#111111", bold: true),
            ])),
            new("a6", "Postkarte A6", "105 × 148 mm", () => FlyDoc("a6", "#FFFFFF", [FlyItem(0, "text", 8, 60, 89, 20, "Danke!", 28, "#B45309", bold: true, align: "mitte")])),
            new("a4", "Leeres Blatt A4", "210 × 297 mm", () => FlyDoc("a4", "#FFFFFF", [])),
        ],
        Ribbon =
        [
            ("Einfügen", [
                new ProgramCmd("fly-text", "＋ Text", d => FlyerAdd(d, "text"), Primary: true),
                new ProgramCmd("fly-image", "Bild …", d => _ = FlyerPickImage(d, null)),
                new ProgramCmd("fly-rect", "Fläche", d => FlyerAdd(d, "rect")),
                new ProgramCmd("fly-ellipse", "Kreis", d => FlyerAdd(d, "ellipse")),
                new ProgramCmd("fly-line", "Linie", d => FlyerAdd(d, "line"), Level: 1),
                new ProgramCmd("fly-qr", "QR-Code", d => FlyerAdd(d, "qr")),
            ]),
            ("Seite", [
                new ProgramCmd("fly-front", "Vorderseite", d => { d.Data["side"] = 0; d.Data["sel"] = ""; }),
                new ProgramCmd("fly-back", "Rückseite", d => { d.Data["side"] = 1; d.Data["sel"] = ""; }),
                new ProgramCmd("fly-guides", "Hilfslinien", d => d.Data["guides"] = !d.Data["guides"].AsBool(true), Tip: "Beschnitt, Sicherheitsabstand und Falz ein/aus"),
            ]),
            ("Anordnen", [
                new ProgramCmd("fly-front-most", "Nach vorn", d => BzMove(d, "items", d.Text("sel"), 1)),
                new ProgramCmd("fly-back-most", "Nach hinten", d => BzMove(d, "items", d.Text("sel"), -1)),
                new ProgramCmd("fly-dup", "Kopieren", d => { var o = BzItem(d, "items", d.Text("sel")).DeepClone().AsObject(); var nid = NewId("e"); o["id"] = nid; o["x"] = ProgramDocument.ToNumber(o["x"]) + 3; o["y"] = ProgramDocument.ToNumber(o["y"]) + 3; d.Edit("Kopie", x => { (x["items"] as JsonArray)!.Add(o); x["sel"] = nid; }); }),
                new ProgramCmd("fly-del", "Löschen", d => { var id = d.Text("sel"); if (id.Length == 0) throw new InvalidOperationException("Erst ein Element anklicken."); BzRemove(d, "items", id, "Element gelöscht"); d.Data["sel"] = ""; }),
            ]),
            ("Prüfen", [
                new ProgramCmd("fly-check", "Druck prüfen", d => { var f = FlyerCheck(d); _progMsg[FlyerKey] = f.Count == 0 ? "✓ Druckfertig nach den geprüften Punkten." : $"{f.Count(x => x.Error)} Fehler, {f.Count(x => !x.Error)} Hinweise – rechts unter „Prüfung“."; }),
                new ProgramCmd("fly-fix-all", "Alles in den Rahmen", d => { var ids = FlyerCheck(d).Where(x => x.ItemId is not null && (x.Text.Contains("Randbereich") || x.Text.Contains("Beschnitt ziehen"))).Select(x => x.ItemId!).Distinct().ToList(); foreach (var id in ids) FlyerFix(d, id); _progMsg[FlyerKey] = ids.Count == 0 ? "Nichts zu verschieben." : $"{ids.Count} Element(e) in den Sicherheitsabstand bzw. in den Beschnitt gezogen."; }, Tip: "Text in den Sicherheitsabstand, Flächen randabfallend bis in den Beschnitt"),
            ]),
        ],
        Exports =
        [
            new("pdf", "Druck-PDF mit 3 mm Beschnitt (.pdf)", ".pdf", (d, f) => FlyerPdf(d, f, false)),
            new("pdf-marken", "Druck-PDF mit Schnittmarken (.pdf)", ".pdf", (d, f) => FlyerPdf(d, f, true)),
        ],
        SimpleExports = 2,
        Workspace = FlyerWorkspace,
        Properties = FlyerProperties,
        Status = d => { var m = FlyerModel(d); var f = FlyerKit.Format(m.Format); var (w, h) = FlyerKit.DataSize(m); var c = FlyerCheck(d); return $"{f.Name} · Datenformat {w:0.#} × {h:0.#} mm · {(d.Number("side") == 0 ? "Vorderseite" : "Rückseite")} · {m.Items.Count} Elemente · " + (c.Any(x => x.Error) ? $"{c.Count(x => x.Error)} Fehler" : "druckfertig"); },
        Help = "Format rechts wählen, oben Text, Bild, Flächen oder QR-Code einfügen und auf der Seite verschieben. Rot = Beschnitt (wird abgeschnitten), gestrichelt grün = Sicherheitsabstand: Text gehört innerhalb. "
             + "Hintergründe bis in den roten Rand ziehen. „Druck prüfen“ meldet Bilder unter 300 dpi und Text im Randbereich. Das Druck-PDF hat Endformat- und Beschnittrahmen; mit Schnittmarken für den eigenen Zuschnitt.",
    }, "flyer", "visitenkarte", "faltflyer", "handzettel", "din lang", "druckdaten", "beschnitt", "schnittmarken", "qr-code", "canva", "vistaprint", "druckerei");

    Control FlyerWorkspace(ProgramDocument d)
    {
        var m = FlyerModel(d); var f = FlyerKit.Format(m.Format); var side = (int)d.Number("side"); var b = m.BleedMm; var sel = d.Text("sel"); var guides = d.Data["guides"].AsBool(true);
        double dw = f.WMm + 2 * b, dh = f.HMm + 2 * b;
        var k = Math.Min(Math.Min(760 / dw, 560 / dh), 9); // Pixel je mm
        var canvas = new Canvas { Name = "fly-canvas", Width = dw * k, Height = dh * k, Background = Brush.Parse(NlCol(side == 0 ? m.Background : m.BackBackground)), ClipToBounds = true };
        double X(double mm) => (mm + b) * k;
        foreach (var it in m.Items.Where(i => i.Side == side))
        {
            Control c;
            switch (it.Kind)
            {
                case "rect": c = new Border { Background = Brush.Parse(NlCol(it.Fill.Length > 0 ? it.Fill : it.Color)) }; break;
                case "ellipse": c = new Avalonia.Controls.Shapes.Ellipse { Fill = Brush.Parse(NlCol(it.Fill.Length > 0 ? it.Fill : it.Color)) }; break;
                case "line": c = new Border { Background = Brush.Parse(NlCol(it.Color)), MinHeight = 1 }; break;
                case "image": c = BizThumb(it.Src, it.W * k, it.H * k); break;
                case "qr": c = FlyQrBitmap(it.Text, it.Color) is { } q ? new Image { Source = q, Stretch = Stretch.Uniform } : new Border { Background = Brush.Parse("#FECACA"), Child = new TextBlock { Text = "QR zu lang", FontSize = 9, Foreground = Brushes.Black } }; break;
                default:
                    c = new Border { Background = it.Fill.Length > 0 ? Brush.Parse(NlCol(it.Fill)) : Brushes.Transparent, Child = new TextBlock { Text = it.Text, Foreground = Brush.Parse(NlCol(it.Color)), FontSize = Math.Max(4, it.SizePt / FlyerKit.MmPt * k),
                        FontWeight = it.Bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Arial"),
                        TextAlignment = it.Align switch { "mitte" => TextAlignment.Center, "rechts" => TextAlignment.Right, _ => TextAlignment.Left } } };
                    break;
            }
            c.Width = Math.Max(1, it.W * k); c.Height = Math.Max(1, it.H * k);
            var holder = new Border { Name = "fly-item-" + it.Id, Child = c, Width = c.Width + 2, Height = c.Height + 2, BorderThickness = new Thickness(1), Cursor = new Cursor(StandardCursorType.SizeAll),
                BorderBrush = it.Id == sel ? Brush.Parse("#2F86F6") : Brushes.Transparent, Background = Brushes.Transparent };
            Canvas.SetLeft(holder, X(it.X) - 1); Canvas.SetTop(holder, X(it.Y) - 1);
            var id = it.Id; Point? start = null; var moved = false;
            holder.PointerPressed += (_, e) => { start = e.GetPosition(canvas); moved = false; e.Pointer.Capture(holder); e.Handled = true; };
            holder.PointerMoved += (_, e) =>
            {
                if (start is not { } s0) return; var p = e.GetPosition(canvas); var dx = p.X - s0.X; var dy = p.Y - s0.Y;
                if (!moved && Math.Abs(dx) + Math.Abs(dy) < 3) return; moved = true;
                Canvas.SetLeft(holder, X(it.X) - 1 + dx); Canvas.SetTop(holder, X(it.Y) - 1 + dy);
            };
            holder.PointerReleased += (_, e) =>
            {
                if (start is not { } s0) return; var p = e.GetPosition(canvas); start = null; e.Pointer.Capture(null);
                if (moved) ProgramRun(FlyerKey, () => { FlyerPlace(d, id, it.X + (p.X - s0.X) / k, it.Y + (p.Y - s0.Y) / k); d.Data["sel"] = id; });
                else { d.Data["sel"] = id; Show(FlyerKey); }
            };
            canvas.Children.Add(holder);
        }
        if (guides)
        {
            var red = Brush.Parse("#55EF4444");
            foreach (var (x, y, w, h) in new[] { (0.0, 0.0, dw * k, b * k), (0.0, (dh - b) * k, dw * k, b * k), (0.0, 0.0, b * k, dh * k), ((dw - b) * k, 0.0, b * k, dh * k) })
            { var r = new Border { Width = w, Height = h, Background = red, IsHitTestVisible = false }; Canvas.SetLeft(r, x); Canvas.SetTop(r, y); canvas.Children.Add(r); }
            var trim = new Avalonia.Controls.Shapes.Rectangle { Width = f.WMm * k, Height = f.HMm * k, Stroke = Brush.Parse("#EF4444"), StrokeThickness = 1, IsHitTestVisible = false };
            Canvas.SetLeft(trim, X(0)); Canvas.SetTop(trim, X(0)); canvas.Children.Add(trim);
            var safe = new Avalonia.Controls.Shapes.Rectangle { Width = (f.WMm - 2 * f.SafeMm) * k, Height = (f.HMm - 2 * f.SafeMm) * k, Stroke = Brush.Parse("#22C55E"), StrokeThickness = 1, StrokeDashArray = [4, 3], IsHitTestVisible = false };
            Canvas.SetLeft(safe, X(f.SafeMm)); Canvas.SetTop(safe, X(f.SafeMm)); canvas.Children.Add(safe);
            foreach (var fx in FlyerKit.Folds(f))
            { var l = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(X(fx), 0), EndPoint = new Point(X(fx), dh * k), Stroke = Brush.Parse("#3B82F6"), StrokeThickness = 1, StrokeDashArray = [6, 4], IsHitTestVisible = false }; canvas.Children.Add(l); }
        }
        canvas.PointerPressed += (_, _) => { if (d.Text("sel").Length > 0) { d.Data["sel"] = ""; Show(FlyerKey); } };
        var info = $"{f.Name} · {(side == 0 ? "Vorderseite" : "Rückseite")} · Datenformat {dw:0.#} × {dh:0.#} mm";
        return new StackPanel { Spacing = 6, Margin = new Thickness(4, 0, 10, 4), Children = {
            new TextBlock { Text = info, Foreground = Muted, FontSize = 12 },
            new Border { Child = canvas, HorizontalAlignment = HorizontalAlignment.Center, BoxShadow = BoxShadows.Parse("0 6 24 0 #66000000") },
            P(guides ? "Rot: Beschnitt (wird abgeschnitten) · Grün gestrichelt: Sicherheitsabstand · Blau: Falz. Elemente mit der Maus verschieben." : "Hilfslinien aus – so ungefähr sieht das geschnittene Ergebnis aus (Ränder fallen weg).", Muted, 11) } };
    }

    TextBox FlyNum(ProgramDocument d, string id, string field, string name, string hint, double value)
    {
        var t = new TextBox { Name = name, Text = value.ToString("0.#", De), Width = 64, FontSize = 12 }; ToolTip.SetTip(t, hint);
        var shown = t.Text;
        void Commit()
        {
            if ((t.Text ?? "") == shown) return;
            var s = (t.Text ?? "").Trim().Replace(',', '.');
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { _progMsg[FlyerKey] = $"„{t.Text}“ ist keine Zahl."; Show(FlyerKey); return; }
            shown = t.Text; ProgramRun(FlyerKey, () => BzField(d, "items", id, field, Math.Round(v, 1), hint));
        }
        OnEnter(t, Commit); t.LostFocus += (_, _) => Commit();
        return t;
    }

    Control FlyerProperties(ProgramDocument d)
    {
        var m = FlyerModel(d); var sp = new StackPanel { Name = "fly-props", Spacing = 5 };
        sp.Children.Add(H("Format", 13));
        var fmt = new ComboBox { Name = "fly-format", ItemsSource = FlyerKit.Formats.Select(f => f.Name).ToList(), SelectedIndex = FlyerKit.Formats.ToList().FindIndex(f => f.Id == m.Format), FontSize = 12, MinWidth = 220 };
        fmt.SelectionChanged += (_, _) => { if (fmt.SelectedIndex >= 0 && FlyerKit.Formats[fmt.SelectedIndex].Id != m.Format) ProgramRun(FlyerKey, () => d.Set("format", FlyerKit.Formats[fmt.SelectedIndex].Id, "Format")); };
        sp.Children.Add(fmt);
        sp.Children.Add(KrBox(d, "bg", "fly-bg", "Hintergrund vorne, z. B. #FFFFFF"));
        sp.Children.Add(KrBox(d, "bg2", "fly-bg2", "Hintergrund hinten"));

        var selO = BzList(d, "items").FirstOrDefault(o => Js(o["id"]) == d.Text("sel"));
        if (selO is not null)
        {
            var it = FlyOf(selO); var id = it.Id;
            sp.Children.Add(H(it.Kind switch { "text" => "Text", "qr" => "QR-Code", "image" => "Bild", "rect" => "Fläche", "ellipse" => "Kreis", _ => "Linie" }, 13));
            sp.Children.Add(new WrapPanel { Children = {
                new TextBlock { Text = "X", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) }, FlyNum(d, id, "x", "fly-x", "Abstand links (mm)", it.X),
                new TextBlock { Text = " Y", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) }, FlyNum(d, id, "y", "fly-y", "Abstand oben (mm)", it.Y),
                new TextBlock { Text = " B", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) }, FlyNum(d, id, "w", "fly-w", "Breite (mm)", it.W),
                new TextBlock { Text = " H", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) }, FlyNum(d, id, "h", "fly-h", "Höhe (mm)", it.H) } });
            sp.Children.Add(P("Maße in Millimetern ab der Ecke des Endformats (negativ = in den Beschnitt).", Muted, 10.5));
            if (it.Kind is "text" or "qr") sp.Children.Add(BzBox(d, "items", id, "text", "fly-text-input", it.Kind == "qr" ? "Inhalt des QR-Codes (Webadresse)" : "Text", it.Kind == "text"));
            if (it.Kind == "text")
            {
                sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Größe (pt) ", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, FlyNum(d, id, "size", "fly-size", "Schriftgröße in Punkt", it.SizePt) } });
                var bold = new CheckBox { Name = "fly-bold", Content = "Fett", IsChecked = it.Bold, Foreground = Text, FontSize = 12 };
                bold.IsCheckedChanged += (_, _) => { if ((bold.IsChecked == true) != it.Bold) ProgramRun(FlyerKey, () => BzField(d, "items", id, "bold", bold.IsChecked == true, "Fett")); };
                var align = new ComboBox { Name = "fly-align", ItemsSource = new[] { "links", "mitte", "rechts" }, SelectedItem = it.Align, FontSize = 12 };
                align.SelectionChanged += (_, _) => { if (align.SelectedItem is string a && a != it.Align) ProgramRun(FlyerKey, () => BzField(d, "items", id, "align", a, "Ausrichtung")); };
                sp.Children.Add(new WrapPanel { Children = { MSp(bold), MSp(align) } });
            }
            if (it.Kind is "text" or "qr" or "line") sp.Children.Add(BzBox(d, "items", id, "color", "fly-color", "Farbe, z. B. #111111"));
            if (it.Kind is "rect" or "ellipse" or "text") sp.Children.Add(BzBox(d, "items", id, "fill", "fly-fill", it.Kind == "text" ? "Hintergrund des Textfelds (leer = durchsichtig)" : "Füllfarbe, z. B. #B45309"));
            if (it.Kind == "image")
            {
                sp.Children.Add(BzBtn(FlyerKey, "fly-image-pick", it.Src.Length == 0 ? "Bild wählen …" : "Anderes Bild …", () => _ = FlyerPickImage(d, id)));
                if (BizImageSize(it.Src) is { } px) sp.Children.Add(P($"{px.W} × {px.H} px → {Math.Min(FlyerKit.EffectiveDpi(px.W, it.W), FlyerKit.EffectiveDpi(px.H, it.H)):0} dpi im Druck", Muted, 11));
            }
            if (it.Kind == "qr")
            {
                sp.Children.Add(P("Oder Kontakt als QR-Code (das Handy speichert ihn als Kontakt):", Muted, 11));
                var cn = new TextBox { Name = "fly-qr-name", PlaceholderText = "Name", FontSize = 12 }; var ct = new TextBox { Name = "fly-qr-tel", PlaceholderText = "Telefon", FontSize = 12 };
                var ce = new TextBox { Name = "fly-qr-mail", PlaceholderText = "E-Mail", FontSize = 12 }; var cw = new TextBox { Name = "fly-qr-web", PlaceholderText = "Webseite", FontSize = 12 };
                sp.Children.Add(cn); sp.Children.Add(ct); sp.Children.Add(ce); sp.Children.Add(cw);
                sp.Children.Add(BzBtn(FlyerKey, "fly-qr-contact", "Als Kontakt-QR", () =>
                {
                    if (string.IsNullOrWhiteSpace(cn.Text)) throw new ArgumentException("Bitte einen Namen eintragen.");
                    var v = FlyerKit.ContactQr(cn.Text ?? "", ct.Text ?? "", ce.Text ?? "", cw.Text ?? "");
                    try { QrMatrix.Encode(v); } catch (InvalidOperationException) { throw new InvalidOperationException("Zu viele Angaben für einen kleinen QR-Code – lass z. B. die Webseite weg."); }
                    BzField(d, "items", id, "text", v, "Kontakt-QR");
                }));
            }
            sp.Children.Add(new WrapPanel { Children = { BzBtn(FlyerKey, "fly-fix", it.Kind is "text" or "qr" ? "In den Sicherheitsabstand" : "Bis in den Beschnitt", () => FlyerFix(d, id)),
                BzBtn(FlyerKey, "fly-remove", "Löschen", () => { BzRemove(d, "items", id, "Element gelöscht"); d.Data["sel"] = ""; }) } });
        }
        else sp.Children.Add(P("Ein Element auf der Seite anklicken, um es zu bearbeiten.", Muted, 11));
        sp.Children.Add(H("Prüfung", 13));
        sp.Children.Add(BzFindings("fly-check-list", FlyerCheck(d), x => (x.Text, x.Error)));
        return sp;
    }

    // ======================================================================
    // Verkaufsseiten (STUDIO-LANDING-01)
    // ======================================================================

    static JsonObject LpJson(string kind, params (string K, string V)[] f)
    {
        var o = new JsonObject { ["id"] = NewId("s"), ["kind"] = kind };
        foreach (var (k, v) in f) o[k] = v;
        return o;
    }

    const string LpImpressumSample = "Erika Muster\nSeifenmanufaktur Muster\nHauptstraße 1\n12345 Musterstadt\nE-Mail: kontakt@example.org\nTelefon: 01234 56789";
    const string LpPrivacySample = "Diese Seite speichert keine Cookies und lädt keine fremden Inhalte. Beim Klick auf „Jetzt kaufen“ wechselst du zum Bezahldienst; dort gilt dessen Datenschutzerklärung. Verantwortlich: siehe Impressum. Du hast das Recht auf Auskunft, Berichtigung und Löschung deiner Daten.";

    static JsonObject LandingDoc(string name, string desc, IEnumerable<JsonObject> blocks, string impressum = "", string privacy = "") => new()
    {
        ["name"] = name, ["desc"] = desc, ["accent"] = "#B45309", ["buy"] = "", ["impressum"] = impressum, ["privacy"] = privacy, ["small"] = false,
        ["blocks"] = new JsonArray([.. blocks.Select(b => (JsonNode)b)]), ["sel"] = "", ["view"] = "desktop",
    };

    static LpBlock LpOf(JsonObject o) => new(Js(o["id"]), Js(o["kind"]), Js(o["title"]), Js(o["text"]), Js(o["src"]), Js(o["alt"]), Js(o["label"]), Js(o["price"]), Js(o["old"]), Js(o["unit"]), Js(o["items"]));

    public LpDoc LandingModel(ProgramDocument d) => new(d.Text("name"), d.Text("desc"), d.Text("accent", "#B45309"), d.Text("buy"), d.Text("impressum"), d.Text("privacy"), [.. BzList(d, "blocks").Select(LpOf)], d.Data["small"].AsBool());
    public List<LpFinding> LandingCheck(ProgramDocument d) => LandingKit.Check(LandingModel(d), BizBytes);

    public string LandingAdd(ProgramDocument d, string kind)
    {
        var o = kind switch
        {
            "held" => LpJson("held", ("title", d.Text("name", "Dein Produkt")), ("text", "Ein Satz, warum es sich lohnt."), ("label", "Jetzt kaufen")),
            "nutzen" => LpJson("nutzen", ("title", "Darum lohnt es sich"), ("items", "Erster Vorteil | Ein Satz dazu\nZweiter Vorteil | Ein Satz dazu\nDritter Vorteil | Ein Satz dazu")),
            "preis" => LpJson("preis", ("title", "Preis"), ("price", "19,90 €"), ("unit", ""), ("items", "Was enthalten ist\nNoch etwas"), ("label", "Jetzt kaufen")),
            "bewertungen" => LpJson("bewertungen", ("title", "Das sagen Kundinnen und Kunden"), ("items", "")),
            "faq" => LpJson("faq", ("title", "Häufige Fragen"), ("items", "Wie lange dauert der Versand? | 2 bis 4 Werktage.")),
            "kaufen" => LpJson("kaufen", ("title", "Bereit?"), ("label", "Jetzt kaufen")),
            "text" => LpJson("text", ("title", "Überschrift"), ("text", "Dein Text.")),
            "bild" => LpJson("bild", ("src", ""), ("alt", "")),
            _ => throw new ArgumentException("Unbekannter Baustein: " + kind),
        };
        var list = BzList(d, "blocks"); var i = list.FindIndex(b => Js(b["id"]) == d.Text("sel"));
        list.Insert(i >= 0 ? i + 1 : list.Count, o);
        d.Edit("Baustein " + LandingKit.Kinds.First(k => k.Id == kind).Name, x => { x["blocks"] = new JsonArray([.. list.Select(b => (JsonNode)b.DeepClone())]); x["sel"] = Js(o["id"]); });
        return Js(o["id"]);
    }

    async Task LandingPickImage(ProgramDocument d, string id)
    {
        var f = await PickMediaFile("landing-image", "Bild für die Seite", ["*.jpg", "*.jpeg", "*.png", "*.webp"]);
        if (f is not null) ProgramRun(LandingKey, () => BzField(d, "blocks", id, "src", f, "Bild"));
    }

    /// <summary>Seite als Ordner schreiben (index.html, impressum.html, datenschutz.html, style.css, bilder/).</summary>
    public string LandingWrite(ProgramDocument d, string dir)
    {
        if (Directory.Exists(dir)) foreach (var old in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.Delete(old);
        return LandingKit.Write(LandingModel(d), dir, BizRead);
    }

    /// <summary>An „Webseite bauen“ übergeben: Ordner schreiben und dort öffnen – veröffentlicht wird dort (mit deiner Bestätigung).</summary>
    public string LandingToWeb(ProgramDocument d)
    {
        var errs = LandingCheck(d).Where(f => f.Error).ToList();
        if (errs.Count > 0) throw new InvalidOperationException($"Erst {errs.Count} Pflichtpunkt(e) beheben: " + string.Join(" ", errs.Select(e => e.Text)));
        var dir = Path.Combine(BizDir("verkaufsseiten"), Portfolio.Slug(d.Text("name", d.Title)));
        var index = LandingWrite(d, dir);
        WebDir = dir;
        _core.Audit.Append(Principal.LocalOwner.Name, "verkaufsseite.uebergabe", Path.GetFileName(dir));
        _progMsg[LandingKey] = $"Seite liegt in {dir} und ist in „Webseite bauen“ geöffnet – dort prüfen und veröffentlichen.";
        _w45Msg = "Verkaufsseite übernommen – „Qualität prüfen“ und dann veröffentlichen.";
        return index;
    }

    /// <summary>Bezahllinks aus „Online verkaufen“ (eigenes Stripe-Konto), falls dort schon angelegt.</summary>
    public List<(string Name, string Url)> LandingPayLinks()
    {
        try { return [.. PodShopItems.All().SelectMany(i => i.Variants.Where(v => LandingKit.SafeUrl(v.PayUrl ?? "")).Select(v => ($"{i.Name} {v.Name}".Trim() + $" ({i.Price:0.00} €)", v.PayUrl!)))]; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { return []; }
    }

    private void InitLanding() => RegisterProgram(FachTabs, new ProgramSpec(LandingKey, "STUDIO-LANDING-01", "Verkaufsseiten")
    {
        Sentence = "Eine schnelle Seite für ein Produkt oder Angebot – Held, Nutzen, Preis, Bewertungen, Fragen und ein Kaufen-Knopf zu deinem Bezahllink.",
        Folder = "Verkaufsseiten",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Ansicht"] = 0, ["Prüfen"] = 0, ["Veröffentlichen"] = 0 },
        CmdLevels = new() { ["lp-add-text"] = 1, ["lp-add-bild"] = 1 },
        Templates =
        [
            new("produkt", "Produktseite", "Held, Nutzen, Preis, FAQ, Kaufen", () => LandingDoc("Handgemachte Seife", "Milde Seife aus Olivenöl, von Hand gesiedet – ohne Palmöl, plastikfrei verpackt.", [
                LpJson("held", ("title", "Handgemachte Seife"), ("text", "Mild zur Haut, gut für die Umwelt – von Hand gesiedet aus Olivenöl."), ("label", "Jetzt kaufen")),
                LpJson("nutzen", ("title", "Darum lohnt es sich"), ("items", "Ohne Palmöl | Nur Oliven-, Kokos- und Rapsöl\nPlastikfrei | Verpackt in Papier\nLange haltbar | Ein Stück reicht etwa sechs Wochen")),
                LpJson("preis", ("title", "Preis"), ("price", "8,90 €"), ("unit", "je Stück (100 g)"), ("items", "Handgesiedet\nVersand in 2–4 Werktagen\n14 Tage Rückgaberecht"), ("label", "Jetzt kaufen")),
                LpJson("faq", ("title", "Häufige Fragen"), ("items", "Ist die Seife vegan? | Ja, ohne tierische Zutaten.\nWie lange dauert der Versand? | 2 bis 4 Werktage.")),
                LpJson("kaufen", ("title", "Bereit für weiche Haut?"), ("label", "Jetzt kaufen")),
            ], LpImpressumSample, LpPrivacySample)),
            new("angebot", "Angebot / Kurs", "Held, Text, Preis, Kaufen", () => LandingDoc("Brotbackkurs", "Sauerteigbrot an einem Samstag lernen – kleine Gruppe, alles inklusive.", [
                LpJson("held", ("title", "Brotbacken mit Sauerteig"), ("text", "Ein Samstag, sechs Plätze, dein eigenes Brot zum Mitnehmen."), ("label", "Platz buchen")),
                LpJson("text", ("title", "Was dich erwartet"), ("text", "Wir setzen gemeinsam Sauerteig an, kneten, formen und backen.\n\nDauer: 10 bis 15 Uhr, mit Mittagessen.")),
                LpJson("preis", ("title", "Teilnahme"), ("price", "89 €"), ("unit", "pro Person"), ("items", "Alle Zutaten\nRezeptheft\nSauerteig zum Mitnehmen"), ("label", "Platz buchen")),
            ], LpImpressumSample, LpPrivacySample)),
            new("leer", "Leere Seite", "Nur ein Held-Baustein", () => LandingDoc("", "", [LpJson("held", ("title", ""), ("text", ""), ("label", "Jetzt kaufen"))])),
        ],
        Ribbon =
        [
            ("Bausteine", [.. LandingKit.Kinds.Select(k => new ProgramCmd("lp-add-" + k.Id, "＋ " + k.Name, d => LandingAdd(d, k.Id), Primary: k.Id == "nutzen")),
                new ProgramCmd("lp-up", "▲", d => BzMove(d, "blocks", d.Text("sel"), -1), Tip: "Baustein nach oben"),
                new ProgramCmd("lp-down", "▼", d => BzMove(d, "blocks", d.Text("sel"), 1), Tip: "Baustein nach unten"),
                new ProgramCmd("lp-del", "Baustein löschen", d => { var id = d.Text("sel"); if (id.Length == 0) throw new InvalidOperationException("Erst einen Baustein wählen."); BzRemove(d, "blocks", id, "Baustein gelöscht"); d.Data["sel"] = ""; })]),
            ("Ansicht", [
                new ProgramCmd("lp-desktop", "Desktop", d => d.Data["view"] = "desktop"),
                new ProgramCmd("lp-phone", "Handy", d => d.Data["view"] = "handy"),
            ]),
            ("Prüfen", [
                new ProgramCmd("lp-check", "Pflicht prüfen", d => { var f = LandingCheck(d); _progMsg[LandingKey] = f.Count == 0 ? "✓ Alles in Ordnung." : $"{f.Count(x => x.Error)} Pflichtpunkte, {f.Count(x => !x.Error)} Hinweise – rechts unter „Prüfung“."; }),
            ]),
            ("Veröffentlichen", [
                new ProgramCmd("lp-web", "An „Webseite bauen“ übergeben", d => { LandingToWeb(d); Avalonia.Threading.Dispatcher.UIThread.Post(() => Show("dev")); }, Tip: "Dort prüfen und veröffentlichen."),
            ]),
        ],
        Exports =
        [
            new("ordner", "Webseiten-Ordner (index.html)", ".html", (d, f) =>
            {
                var dir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(f))!, Path.GetFileNameWithoutExtension(f));
                var index = LandingWrite(d, dir); _progMsg[LandingKey] = $"Ordner geschrieben: {dir}"; return index;
            }),
        ],
        Workspace = LandingWorkspace,
        Properties = LandingProperties,
        Status = d => { var m = LandingModel(d); var f = LandingCheck(d); return $"{m.Blocks.Count} Bausteine · {LandingKit.Weight(m, BizBytes) / 1024.0:0} KB · " + (f.Any(x => x.Error) ? $"{f.Count(x => x.Error)} Pflichtpunkte offen" : "Pflichtangaben vollständig") + (LandingKit.SafeUrl(m.BuyUrl) ? " · Kauf-Link gesetzt" : " · ohne Kauf-Link"); },
        Help = "Bausteine oben einfügen, in der Vorschau anklicken und rechts ausfüllen. Listen schreibst du zeilenweise mit „|“ (z. B. „Frage | Antwort“). "
             + "Der Kaufen-Knopf führt zu deinem Bezahllink (z. B. aus „Online verkaufen“) – in Trendo wird nicht bezahlt. Impressum und Datenschutz sind Pflicht. „An Webseite bauen übergeben“ öffnet die Seite dort zum Veröffentlichen.",
    }, "verkaufsseite", "landingpage", "landing page", "produktseite", "angebotsseite", "carrd", "leadpages", "kaufen-knopf", "bezahllink");

    Control LandingWorkspace(ProgramDocument d)
    {
        var m = LandingModel(d); var handy = d.Text("view") == "handy"; var sel = d.Text("sel"); var width = handy ? 375.0 : 860.0;
        var acc = Brush.Parse(NlCol(m.Accent)); var dark = Brush.Parse("#1F2937"); var grey = Brush.Parse("#6B7280");
        var page = new StackPanel { Name = "lp-preview", Width = width, Background = Brushes.White };
        Control Btn(string label, bool inverse = false) => new Border { Background = inverse ? Brushes.White : acc, CornerRadius = new CornerRadius(999), Padding = new Thickness(26, 10), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10),
            Child = new TextBlock { Text = (label.Length > 0 ? label : "Jetzt kaufen") + (LandingKit.SafeUrl(m.BuyUrl) ? "" : "  (ohne Link)"), Foreground = inverse ? acc : Brushes.White, FontWeight = FontWeight.Bold, FontSize = 14 } };
        TextBlock T(string t, double size, IBrush c, bool bold = false, bool center = false) => new() { Text = t, FontSize = size, Foreground = c, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap, TextAlignment = center ? TextAlignment.Center : TextAlignment.Left };
        Control Cards(IEnumerable<Control> cards)
        {
            var list = cards.ToList();
            if (handy) { var s = new StackPanel { Spacing = 10 }; foreach (var c in list) s.Children.Add(c); return s; }
            var g = new UniformGrid { Columns = Math.Clamp(list.Count, 1, 3) }; foreach (var c in list) { c.Margin = new Thickness(0, 0, 12, 12); g.Children.Add(c); } return g;
        }
        Border Card(Control c) => new() { BorderBrush = Brush.Parse("#E5E7EB"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Child = c };
        foreach (var b in m.Blocks)
        {
            var pad = new Thickness(handy ? 18 : 40, handy ? 28 : 40);
            Control c = b.Kind switch
            {
                "held" => new Border { Background = acc, Padding = pad, Child = new StackPanel { Spacing = 8, Children = {
                    T(b.Title.Length > 0 ? b.Title : m.Name, handy ? 26 : 36, Brushes.White, true, true), T(b.Text, handy ? 14 : 17, Brushes.White, false, true), Btn(b.Label, true),
                    b.Src.Length > 0 ? BizThumb(b.Src, width - pad.Left * 2, (width - pad.Left * 2) * 0.5) : new Border() } } },
                "nutzen" => new StackPanel { Margin = pad, Spacing = 12, Children = { T(b.Title, 22, dark, true),
                    Cards(LandingKit.Rows(b.Items).Select(r => (Control)Card(new StackPanel { Spacing = 4, Children = { T(r[0], 15, dark, true), T(r.Length > 1 ? r[1] : "", 13, grey) } }))) } },
                "preis" => new StackPanel { Margin = pad, Spacing = 6, Children = { T(b.Title, 22, dark, true, true),
                    T((b.OldPrice.Length > 0 ? b.OldPrice + " → " : "") + b.Price + (b.Unit.Length > 0 ? " " + b.Unit : ""), 30, acc, true, true), T(LandingKit.PriceNote(m), 11, grey, false, true),
                    T(string.Join("\n", LandingKit.Rows(b.Items).Select(r => "✓ " + r[0])), 14, dark, false, true), Btn(b.Label) } },
                "bewertungen" => new StackPanel { Margin = pad, Spacing = 12, Children = { T(b.Title, 22, dark, true),
                    LandingKit.Rows(b.Items).Count == 0 ? T("Noch keine Bewertungen – nur echte, freigegebene Stimmen eintragen.", 13, grey) :
                    Cards(LandingKit.Rows(b.Items).Select(r => (Control)Card(new StackPanel { Spacing = 4, Children = { T(new string('★', r.Length > 2 && int.TryParse(r[2], out var s) ? Math.Clamp(s, 1, 5) : 5), 14, Brush.Parse("#F59E0B")), T(r.Length > 1 ? "„" + r[1] + "“" : "", 13, dark), T("– " + r[0], 12, grey) } }))) } },
                "faq" => new StackPanel { Margin = pad, Spacing = 8, Children = { T(b.Title, 22, dark, true),
                    new StackPanel { Spacing = 8, Children = { } }.Also(s => { foreach (var r in LandingKit.Rows(b.Items)) s.Children.Add(new StackPanel { Children = { T("▸ " + r[0], 14, dark, true), T(r.Length > 1 ? r[1] : "", 13, grey) } }); }) } },
                "kaufen" => new Border { Background = Brush.Parse("#F9FAFB"), Padding = pad, Child = new StackPanel { Children = { T(b.Title, 22, dark, true, true), Btn(b.Label) } } },
                "text" => new StackPanel { Margin = pad, Spacing = 8, Children = { T(b.Title, 22, dark, true), T(b.Text, 14, dark) } },
                "bild" => new Border { Margin = pad, Child = b.Src.Length > 0 ? BizThumb(b.Src, width - pad.Left * 2, (width - pad.Left * 2) * 0.55) : new Border { Height = 120, Background = Brush.Parse("#E5E7EB"), Child = T("Bild wählen", 13, grey, false, true) } },
                _ => new Border(),
            };
            var id = b.Id;
            var hit = new Button { Name = "lp-block-" + id, Content = c, Padding = new Thickness(0), Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                BorderBrush = id == sel ? Brush.Parse("#2F86F6") : Brushes.Transparent, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(0) };
            ToolTip.SetTip(hit, LandingKit.Kinds.First(k => k.Id == b.Kind).Name + " – anklicken zum Bearbeiten");
            hit.Click += (_, _) => { d.Data["sel"] = id; Show(LandingKey); };
            page.Children.Add(hit);
        }
        page.Children.Add(new Border { BorderBrush = Brush.Parse("#E5E7EB"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(handy ? 18 : 40, 16),
            Child = T($"Impressum · Datenschutz · © {DateTime.Now.Year} {m.Name}", 12, grey) });
        return new StackPanel { Spacing = 8, Margin = new Thickness(4, 0, 10, 4), Children = {
            new TextBlock { Text = handy ? "Handy (375 px)" : "Desktop", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center },
            new Border { Child = page, HorizontalAlignment = HorizontalAlignment.Center, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(handy ? 20 : 6), ClipToBounds = true, BoxShadow = BoxShadows.Parse("0 6 24 0 #66000000") },
            P("Vorschau des Aufbaus – die echte Seite (HTML ohne Skripte) entsteht beim Export oder bei der Übergabe an „Webseite bauen“.", Muted, 11) } };
    }

    Control LandingProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "lp-props", Spacing = 5 };
        sp.Children.Add(H("Seite", 13));
        sp.Children.Add(KrBox(d, "name", "lp-name", "Name des Produkts oder Angebots"));
        sp.Children.Add(KrBox(d, "desc", "lp-desc", "Beschreibung für Suchmaschinen (bis 155 Zeichen)", true));
        sp.Children.Add(KrBox(d, "accent", "lp-accent", "Akzentfarbe, z. B. #B45309"));
        sp.Children.Add(H("Kaufen-Knopf", 13));
        sp.Children.Add(KrBox(d, "buy", "lp-buy", "Kauf-Link (https://…, z. B. Stripe-Bezahllink)"));
        var links = LandingPayLinks();
        if (links.Count > 0)
        {
            var c = new ComboBox { Name = "lp-paylinks", PlaceholderText = "Bezahllink aus „Online verkaufen“", ItemsSource = links.Select(l => l.Name).ToList(), MinWidth = 220, FontSize = 12 };
            c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) ProgramRun(LandingKey, () => d.Set("buy", links[c.SelectedIndex].Url, "Kauf-Link")); };
            sp.Children.Add(c);
        }
        else sp.Children.Add(P("Bezahllinks legst du in „Online verkaufen“ mit deinem Stripe-Konto an – oder trag einen Link deines Shops ein.", Muted, 11));
        var small = new CheckBox { Name = "lp-small", Content = "Kleinunternehmer (§ 19 UStG, keine Umsatzsteuer)", IsChecked = d.Data["small"].AsBool(), Foreground = Text, FontSize = 12 };
        small.IsCheckedChanged += (_, _) => { if ((small.IsChecked == true) != d.Data["small"].AsBool()) ProgramRun(LandingKey, () => d.Set("small", small.IsChecked == true, "Kleinunternehmer")); };
        sp.Children.Add(small);

        var sel = BzList(d, "blocks").FirstOrDefault(o => Js(o["id"]) == d.Text("sel"));
        if (sel is not null)
        {
            var b = LpOf(sel); var id = b.Id;
            sp.Children.Add(H("Baustein: " + LandingKit.Kinds.First(k => k.Id == b.Kind).Name, 13));
            TextBox F(string field, string hint, bool multi = false) => BzBox(d, "blocks", id, field, "lp-f-" + field, hint, multi);
            switch (b.Kind)
            {
                case "held": sp.Children.Add(F("title", "Überschrift")); sp.Children.Add(F("text", "Ein Satz zum Nutzen", true)); sp.Children.Add(F("label", "Knopf-Text")); break;
                case "nutzen": sp.Children.Add(F("title", "Überschrift")); sp.Children.Add(F("items", "Je Zeile: Vorteil | Erklärung", true)); break;
                case "preis":
                    sp.Children.Add(F("title", "Überschrift")); sp.Children.Add(F("price", "Preis, z. B. 8,90 €")); sp.Children.Add(F("unit", "Einheit, z. B. je Stück"));
                    if (ProgramLevel >= UserLevel.Erweitert) sp.Children.Add(F("old", "Streichpreis (nur wenn erlaubt)"));
                    sp.Children.Add(F("items", "Je Zeile eine Leistung", true)); sp.Children.Add(F("label", "Knopf-Text")); break;
                case "bewertungen": sp.Children.Add(F("title", "Überschrift")); sp.Children.Add(F("items", "Je Zeile: Name | Text | Sterne (1–5) – nur echte Bewertungen", true)); break;
                case "faq": sp.Children.Add(F("title", "Überschrift")); sp.Children.Add(F("items", "Je Zeile: Frage | Antwort", true)); break;
                case "kaufen": sp.Children.Add(F("title", "Überschrift")); sp.Children.Add(F("label", "Knopf-Text")); break;
                case "text": sp.Children.Add(F("title", "Überschrift")); sp.Children.Add(F("text", "Text (Leerzeile = neuer Absatz)", true)); break;
            }
            if (b.Kind is "held" or "bild")
            {
                sp.Children.Add(BzBtn(LandingKey, "lp-image", b.Src.Length == 0 ? "Bild wählen …" : "Anderes Bild …", () => _ = LandingPickImage(d, id)));
                sp.Children.Add(F("alt", "Alt-Text: was ist zu sehen?"));
            }
        }
        sp.Children.Add(H("Pflichtseiten", 13));
        sp.Children.Add(KrBox(d, "impressum", "lp-impressum", "Impressum: Name, Anschrift, E-Mail (§ 5 DDG)", true));
        sp.Children.Add(KrBox(d, "privacy", "lp-privacy", "Datenschutzerklärung", true));
        sp.Children.Add(P("Muster sind nur ein Anfang – rechtlich prüfen lassen.", Muted, 10.5));
        sp.Children.Add(H("Prüfung", 13));
        sp.Children.Add(BzFindings("lp-check-list", LandingCheck(d), f => (f.Text, f.Error)));
        return sp;
    }
}

