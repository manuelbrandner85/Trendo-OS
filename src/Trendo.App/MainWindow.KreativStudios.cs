using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 1 (T-0610, freigegeben 09.11.2026): Texte zusammenfassen, Logos &amp; Marken, Podcast aufnehmen, Comics &amp; Storyboards.
/// Alles läuft ohne Internet; Claude kann über die Studio-Leiste ausführlicher helfen.</summary>
public partial class MainWindow
{
    const string SumKey = "zusammenfassen-programm", LogoKey = "logo-programm", PodcKey = "podcast-programm", ComicKey = "comic-programm";

    const string SumSample = "Der Gemeinderat hat am Montag den neuen Radweg beschlossen. Er verbindet den Bahnhof mit dem Schulzentrum und ist vier Kilometer lang. " +
        "Die Kosten liegen bei 1,2 Millionen Euro, das Land übernimmt die Hälfte. Bauen will die Gemeinde ab dem Frühjahr. Während der Bauzeit wird die Hauptstraße abschnittsweise gesperrt. " +
        "Anwohner können ihre Fragen bei einer Bürgerversammlung im März stellen. Der Radweg soll vor allem Schülern einen sicheren Weg ermöglichen. " +
        "Bisher fahren viele Kinder auf der stark befahrenen Hauptstraße. Die Fertigstellung ist für den Herbst geplant.";

    /// <summary>Text aus Dokumentdaten, auch wenn eine fremde Datei dort eine Zahl oder ein Objekt hat.</summary>
    static string Js(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    private void InitKreativStudios() { InitSummary(); InitLogo(); InitPodcast(); InitComic(); }

    /// <summary>Gemeinsames Textfeld: übernimmt Getipptes nach einer Sekunde Pause, beim Verlassen und beim Schließen.</summary>
    TextBox KrBox(ProgramDocument d, string key, string name, string label, bool multi = false, Action<string>? live = null)
    {
        var t = new TextBox { Name = name, Text = d.Text(key), PlaceholderText = label, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 90 : 0, VerticalContentAlignment = multi ? VerticalAlignment.Top : VerticalAlignment.Center };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        // nur Getipptes übernehmen: Nach einem Befehl (z. B. „Neues Kapitel“) darf das alte Feld beim Neuzeichnen den neuen Stand nicht überschreiben
        var shown = d.Text(key); // was das Feld beim Aufbau zeigte
        void Commit()
        {
            timer.Stop();
            var now = t.Text ?? "";
            if (now != shown && now != d.Text(key)) { d.Set(key, now, label); shown = now; }
        }
        timer.Tick += (_, _) => Commit();
        t.TextChanged += (_, _) => { live?.Invoke(t.Text ?? ""); timer.Stop(); if ((t.Text ?? "") != shown) timer.Start(); };
        t.LostFocus += (_, _) => Commit();
        t.DetachedFromVisualTree += (_, _) => Commit();
        return t;
    }

    static Border KrCard(Control child, string? name = null) => new()
    {
        Name = name, Padding = new Thickness(16, 14), CornerRadius = new CornerRadius(10), Child = child,
        Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#1F2A3D"), 0), new GradientStop(Color.Parse("#151D2B"), 1) } },
        BorderBrush = Brush.Parse("#33D4A017"), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 6 18 0 #55000000"),
    };

    Button KrBtn(string name, string label, Action click, bool primary = false, string key = "")
    {
        var b = new Button { Name = name, Content = label, Padding = new Thickness(12, 6) };
        if (primary) b.Classes.Add("accent");
        b.Click += (_, _) => ProgramRun(key, click);
        return b;
    }

    // ---------------- Texte zusammenfassen ----------------

    static JsonObject SumDoc(string title, string text) => new() { ["title"] = title, ["text"] = text, ["length"] = "mittel" };
    static SummaryKit.Summary SumOf(ProgramDocument d) => SummaryKit.Summarize(d.Text("text"), SummaryKit.CountFor(d.Text("length")));

    private void InitSummary() => RegisterProgram(FachTabs, new ProgramSpec(SumKey, "STUDIO-SUMMARY-01", "Texte zusammenfassen")
    {
        Sentence = "Lange Texte, Protokolle und Artikel auf das Wichtigste bringen – ohne Internet, mit Stichwörtern und Lesezeit.",
        Folder = "Zusammenfassungen",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Leerer Text", "Text einfügen und zusammenfassen", () => SumDoc("", "")),
            new("beispiel", "Beispiel: Zeitungsmeldung", "Eine kurze Meldung zum Ausprobieren", () => SumDoc("Neuer Radweg", SumSample)),
        ],
        Ribbon =
        [
            // T-0725 (Audit D7, STUDIO-SUMMARY-01: „kein Dateiimport“): PDF, Word, OpenDocument, Text und Markdown einlesen
            ("Datei", [
                new ProgramCmd("sum-open", "Datei öffnen …", d => _ = SumPick(d), Tip: "PDF, Word (.docx), OpenDocument (.odt), Text oder Markdown – der Text kommt links hinein, die Datei bleibt unverändert."),
            ]),
            ("Länge", [
                new ProgramCmd("sum-kurz", "Kurz (3 Sätze)", d => d.Set("length", "kurz", "Länge kurz")),
                new ProgramCmd("sum-mittel", "Mittel (5 Sätze)", d => d.Set("length", "mittel", "Länge mittel"), Primary: true),
                new ProgramCmd("sum-lang", "Lang (8 Sätze)", d => d.Set("length", "lang", "Länge lang")),
            ]),
            ("Claude", [
                new ProgramCmd("sum-claude", "Mit Claude ausführlicher", d => _ = AssistFromProgram(SumKey, "STUDIO-SUMMARY-01", "Fasse diesen Text auf Deutsch in einfachen Worten zusammen, mit den wichtigsten Punkten als Liste:\n\n" + d.Text("text")), Tip: "Nutzt deinen Claude-Zugang. Ohne Anmeldung bleibt es bei der eingebauten Zusammenfassung."),
            ]),
        ],
        Exports =
        [
            new("md", "Zusammenfassung (.md)", ".md", (d, f) => { File.WriteAllText(f, SummaryKit.Markdown(d.Text("title"), SumOf(d))); return f; }),
            new("txt", "Nur Kernsätze (.txt)", ".txt", (d, f) => { File.WriteAllText(f, string.Join("\n", SumOf(d).Sentences)); return f; }),
        ],
        Workspace = SumWorkspace,
        Status = d => { var s = SumOf(d); return $"{s.Words} Wörter · etwa {s.ReadMinutes} min Lesezeit · {s.Sentences.Count} Kernsätze"; },
        Help = "Text links einfügen. Rechts stehen sofort die wichtigsten Sätze in der ursprünglichen Reihenfolge. Oben wählst du die Länge. Für eine frei formulierte Fassung „Mit Claude ausführlicher“ nehmen.",
    }, "zusammenfassen", "zusammenfassung", "kurzfassung", "tldr", "protokoll kürzen", "text kürzen", "kernaussagen", "stichwörter");

    public static readonly string[] SumExtensions = ["pdf", "docx", "odt", "txt", "md"];

    /// <summary>T-0725: Text aus einer Datei – PDF über PDFium (Seite für Seite, höchstens 500 Seiten), DOCX/ODT über die Leser von Trendo Text, TXT/MD direkt.
    /// Ein PDF ohne Textebene (Scan) wird ehrlich gemeldet statt still leer übernommen.</summary>
    public static string SumReadFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        string text = ext switch
        {
            ".pdf" => string.Join("\n\n", Enumerable.Range(0, Math.Min(500, PdfView.PageCount(path))).Select(i => PdfView.Text(path, i))),
            ".md" => File.ReadAllText(path),
            ".docx" or ".odt" or ".txt" => Trendo.Core.Office.TextFiles.Open(path).PlainText(),
            _ => throw new ArgumentException($"Das Format {ext} lässt sich hier nicht öffnen – bitte PDF, .docx, .odt, .txt oder .md."),
        };
        text = text.Replace("\r", "").Replace("\f", "\n\n").Trim();
        if (text.Length == 0) throw new InvalidOperationException(ext == ".pdf" ? "Dieses PDF enthält keinen Text (vermutlich ein Scan). Mach es zuerst in „Dokumente scannen“ durchsuchbar." : "Die Datei enthält keinen Text.");
        return text;
    }

    async Task SumPick(ProgramDocument d)
    {
        var req = new PickRequest("sum-open", false, false, SumExtensions);
        string[]? got;
        if (PickOverride is { } fake) got = await fake(req);
        else got = (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Text zum Zusammenfassen öffnen",
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("PDF, Word, Text") { Patterns = SumExtensions.Select(e => "*." + e).ToList() }] }))
            .Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToArray();
        if (got is [var f, ..]) ProgramRun(SumKey, () => SumOpenFile(d, f));
    }

    /// <summary>T-0725: Datei einlesen → Text links (ein Rückgängig-Schritt), Überschrift aus dem Dateinamen, falls leer.</summary>
    public void SumOpenFile(ProgramDocument d, string path)
    {
        var text = SumReadFile(path);
        d.Edit("Datei öffnen", x => { x["text"] = text; if (string.IsNullOrWhiteSpace(Js(x["title"]))) x["title"] = Path.GetFileNameWithoutExtension(path); });
        var s = SumOf(d);
        _progMsg[SumKey] = $"„{Path.GetFileName(path)}“ eingelesen: {s.Words:N0} Wörter → {s.Sentences.Count} Kernsätze. Die Datei bleibt unverändert.";
    }

    Control SumWorkspace(ProgramDocument d)
    {
        var result = new StackPanel { Name = "sum-result", Spacing = 8 };
        void Render(string text)
        {
            result.Children.Clear();
            var s = SummaryKit.Summarize(text, SummaryKit.CountFor(d.Text("length")));
            result.Children.Add(H("Das Wichtigste", 15));
            if (s.Sentences.Count == 0) { result.Children.Add(P("Noch kein Text. Füge links einen Text ein.", Muted, 12)); return; }
            foreach (var x in s.Sentences)
                result.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("18,*"), Children = { new TextBlock { Text = "•", Foreground = Accent, FontSize = 14 }, Pos(P(x, Text, 13), 1) } });
            var chips = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var k in s.Keywords)
                chips.Children.Add(new Border { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(9, 3), CornerRadius = new CornerRadius(12), Background = Brush.Parse("#2A3A55"), Child = new TextBlock { Text = k, Foreground = Text, FontSize = 12 } });
            result.Children.Add(P("Stichwörter", Muted, 11)); result.Children.Add(chips);
            result.Children.Add(P($"{s.Words} Wörter · etwa {s.ReadMinutes} Minuten Lesezeit", Muted, 11));
        }
        var title = KrBox(d, "title", "sum-title", "Überschrift (optional)");
        var box = KrBox(d, "text", "sum-text", "Text hier einfügen …", true, Render);
        BueroFileDrop(box, SumKey, BueroExt(".pdf", ".docx", ".odt", ".txt", ".md"), "ein PDF, eine Word-, OpenDocument- oder Textdatei", (dd, files) => SumOpenFile(dd, files[0])); // T-0725
        box.MinHeight = 380;
        Render(d.Text("text"));
        var left = new StackPanel { Spacing = 8, Children = { title, box } };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Margin = new Thickness(4, 0, 10, 4), Children = { left, Pos(KrCard(result, "sum-card"), 2) } };
    }

    // ---------------- Logos & Marken ----------------
    // T-0696: Das Formular (Name, Untertitel, Farben, Form, Schrift) ist nur noch die Hilfe „Vorschlag erzeugen“ –
    // das Ergebnis ist ein bearbeitbares Vektor-Dokument („vector“), das im gemeinsamen Vektor-Editor frei weitergestaltet wird.

    static JsonObject LogoDoc(string name, string tag, string primary, string accent, string layout)
    {
        var o = new JsonObject { ["name"] = name, ["tagline"] = tag, ["primary"] = primary, ["accent"] = accent, ["layout"] = layout, ["font"] = LogoKit.Fonts[0] };
        o["vector"] = VectorKit.ToJson(VectorKit.FromLogo(new LogoKit.Spec(name, tag, primary, accent, layout, LogoKit.Fonts[0])));
        return o;
    }
    static LogoKit.Spec LogoSpec(ProgramDocument d) => new(d.Text("name"), d.Text("tagline"), LogoKit.IsHex(d.Text("primary")) ? d.Text("primary") : "#1E3A8A", LogoKit.IsHex(d.Text("accent")) ? d.Text("accent") : "#D4A017", d.Text("layout"), d.Text("font"));

    /// <summary>Das bearbeitbare Logo. Ältere Dokumente (nur Formularfelder) bekommen es aus dem Vorschlag.</summary>
    public static VDoc LogoVec(ProgramDocument d) => d.Data["vector"] is JsonObject v ? VectorKit.FromJson(v, 640, 180) : VectorKit.FromLogo(LogoSpec(d));
    static void LogoSave(ProgramDocument d, VDoc v, string label) => d.Edit(label, x => x["vector"] = VectorKit.ToJson(v));

    /// <summary>„Vorschlag erzeugen“: Logo aus dem Formular neu aufbauen (ersetzt die Zeichnung – Strg+Z holt sie zurück).</summary>
    static void LogoSuggest(ProgramDocument d, string? layout = null)
    {
        if (string.IsNullOrWhiteSpace(d.Text("name"))) throw new ArgumentException("Gib zuerst einen Namen ein.");
        d.Edit(layout is null ? "Vorschlag erzeugt" : "Vorschlag " + (LogoKit.Layouts.FirstOrDefault(l => l.Id == layout).Label ?? layout), x =>
        {
            if (layout is not null) x["layout"] = layout;
            var s = new LogoKit.Spec(Js(x["name"]), Js(x["tagline"]), LogoKit.IsHex(Js(x["primary"])) ? Js(x["primary"]) : "#1E3A8A", LogoKit.IsHex(Js(x["accent"])) ? Js(x["accent"]) : "#D4A017", Js(x["layout"]), Js(x["font"]));
            x["vector"] = VectorKit.ToJson(VectorKit.FromLogo(s));
        });
    }

    /// <summary>Formularfeld ändern und die Zeichnung mitziehen, ohne Handarbeit zu verlieren: Name/Untertitel/Anfangsbuchstaben ersetzen den Text,
    /// eine neue Haupt- oder Akzentfarbe färbt alles um, was die alte Farbe hatte. Ein Schritt für Rückgängig.</summary>
    static void LogoField(ProgramDocument d, string key, string value, string label) => d.Edit(label, x =>
    {
        var old = Js(x[key]); x[key] = value;
        var v = x["vector"] is JsonObject vo ? VectorKit.FromJson(vo, 640, 180) : null;
        if (v is null) return;
        switch (key)
        {
            case "name":
                foreach (var s in v.Shapes.Where(s => s.Kind == "text" && s.Id == "name")) s.Text = value.Trim();
                foreach (var s in v.Shapes.Where(s => s.Kind == "text" && s.Id == "zeichen-text")) s.Text = LogoKit.Initials(value);
                break;
            case "tagline": foreach (var s in v.Shapes.Where(s => s.Kind == "text" && s.Id == "untertitel")) s.Text = value.Trim(); break;
            case "primary" or "accent" when LogoKit.IsHex(value) && LogoKit.IsHex(old):
                foreach (var s in v.Shapes)
                {
                    if (string.Equals(s.Fill, old, StringComparison.OrdinalIgnoreCase)) s.Fill = value.ToUpperInvariant();
                    if (string.Equals(s.Fill2, old, StringComparison.OrdinalIgnoreCase)) s.Fill2 = value.ToUpperInvariant();
                    if (string.Equals(s.Stroke, old, StringComparison.OrdinalIgnoreCase)) s.Stroke = value.ToUpperInvariant();
                }
                break;
        }
        x["vector"] = VectorKit.ToJson(v);
    });

    private void InitLogo() => RegisterProgram(FachTabs, new ProgramSpec(LogoKey, "STUDIO-LOGO-01", "Logos & Marken")
    {
        Sentence = "Logo frei gestalten: Formen, Pfade und Schrift auf der Fläche – mit Vorschlag zum Start, Kontrastprüfung und fertigem Marken-Paket.",
        Folder = "Logos",
        FillHeight = true,
        // T-0686 (V-02): Name und Farben stehen in den Eigenschaften, die „Einfach“ einklappt – der erste Schritt holt sie
        FirstStep = new("Namen eingeben", "Trag den Namen deiner Marke ein – dann gestaltest du das Logo direkt auf der Fläche.", _ => ProgramShowProperties(LogoKey),
            d => SimpleCollapsed(LogoKey) && d.Text("name") is "Meine Marke" or ""),
        Templates =
        [
            new("leer", "Neues Logo", "Name eintragen, Vorschlag wählen, frei weiterzeichnen", () => LogoDoc("Meine Marke", "", "#1E3A8A", "#D4A017", "kreis")),
            new("cafe", "Beispiel: Café", "Warme Farben, Schild-Form", () => LogoDoc("Café Morgenrot", "Röstet seit heute", "#7A2E1F", "#E0A458", "schild")),
            new("tech", "Beispiel: Technik", "Kühle Farben, Wortmarke", () => LogoDoc("Nordlicht Labs", "Software mit Ruhe", "#0F4C5C", "#5FA8D3", "wort")),
        ],
        Tools = VecTools(LogoKey),
        Ribbon =
        [
            ("Vorschlag", [
                new ProgramCmd("logo-suggest", "Vorschlag erzeugen", d => LogoSuggest(d), Primary: true, Tip: "Baut das Logo aus Name, Farben, Form und Schrift neu auf. Deine Zeichnung holst du mit Strg+Z zurück."),
                .. LogoKit.Layouts.Select(l => new ProgramCmd("logo-form-" + l.Id, l.Label, d => LogoSuggest(d, l.Id), Tip: "Vorschlag in dieser Form")),
            ]),
            ("Schrift", LogoKit.Fonts.Select(f => new ProgramCmd("logo-font-" + f.Replace(' ', '-').ToLowerInvariant(), f, d => d.Edit("Schrift " + f, x =>
            {
                x["font"] = f;
                if (x["vector"] is JsonObject vo) { var v = VectorKit.FromJson(vo, 640, 180); foreach (var s in v.Shapes.Where(s => s.Kind == "text")) s.Font = f; x["vector"] = VectorKit.ToJson(v); }
            }), Tip: "Alle Texte im Logo bekommen diese Schrift")).ToList()),
            ("Claude", [
                new ProgramCmd("logo-claude", "Ideen mit Claude", d => _ = AssistFromProgram(LogoKey, "STUDIO-LOGO-01", $"Mach mir drei Ideen für ein Logo der Marke „{d.Text("name")}“ ({d.Text("tagline")}): Bildzeichen, Farben (#RRGGBB) und Schrift, jeweils mit einem Satz Begründung. Das Logo liegt als bearbeitbare Vektor-Zeichnung im Programm „Logos & Marken“."), Tip: "Nutzt deinen Claude-Zugang."),
            ]),
        ],
        GroupLevels = new() { ["Schrift"] = 1, ["Claude"] = 1 },
        SimpleExports = 4, // Einfach: Paket, Bild, Druck, Favicon – SVG und Markenpaket stecken im Paket
        Exports =
        [
            new("paket", "Logo-Paket (.zip)", ".zip", (d, f) => { LogoPackage(LogoVec(d), d.Text("name"), f); return f; }),
            new("png", "Bild (.png, 1024 px)", ".png", (d, f) => { File.WriteAllBytes(f, VectorRender.Png(LogoVec(d), 1024)); return f; }),
            new("pdf", "Druck (.pdf)", ".pdf", (d, f) => { File.WriteAllBytes(f, VectorKit.Pdf(LogoVec(d))); return f; }),
            new("ico", "Favicon (.ico)", ".ico", (d, f) => { File.WriteAllBytes(f, LogoIco(LogoVec(d))); return f; }),
            new("svg", "Logo (.svg)", ".svg", (d, f) => { File.WriteAllText(f, VectorKit.ToSvg(LogoVec(d))); return f; }),
            new("md", "Markenpaket (.md)", ".md", (d, f) => { File.WriteAllText(f, VectorKit.BrandMarkdown(LogoVec(d), d.Text("name"))); return f; }),
        ],
        Workspace = LogoWorkspace,
        Properties = LogoProperties,
        Status = d => { var v = LogoVec(d); var pal = VectorKit.Palette(v); var c = pal.Count > 0 ? Math.Max(LogoKit.Contrast(pal[0], "#FFFFFF"), LogoKit.Contrast(pal[0], "#000000")) : 0; return $"{v.Shapes.Count} Formen · {v.W:0} × {v.H:0} · Hauptfarbe {(pal.Count > 0 ? pal[0] : "–")}, Kontrast bis {c.ToString("0.0", De)}:1"; },
        Help = "Links ein Werkzeug wählen und auf der Fläche ziehen: Rechteck, Ellipse, Vieleck, Stern, Linie, Zeichenstift (Bézier-Pfade) oder Text. Mit „Auswahl“ verschieben, an den Griffen Größe ändern, am runden Griff drehen; Doppelklick auf eine Form zeigt ihre Punkte. Rechtsklick: Anordnen, Gruppieren, Ausrichten. Ein eigenes SVG oder Bild ziehst du einfach auf die Fläche. „Vorschlag erzeugen“ baut aus Name und Farben neu auf. Das Logo-Paket enthält SVG, PNG in drei Größen, PDF, Favicon und hell/dunkel/einfarbig.",
    }, "logo", "logos", "marke", "branding", "markenzeichen", "wortmarke", "firmenlogo", "corporate design", "markenfarben", "vektor", "svg");

    /// <summary>Vorschau des echten Logos (aus dem Vektor-Dokument) – gleiche Darstellung wie der PNG-Export.</summary>
    static Control LogoDraw(VDoc v, string bg) => new Viewbox { Stretch = Stretch.Uniform, Child = VectorRender.Stage(VectorKit.ForBackground(v, bg), bg) };

    Control LogoWorkspace(ProgramDocument d)
    {
        var v = LogoVec(d);
        var ed = VecEditor(LogoKey, d, v, (nv, label) => LogoSave(d, nv, label));
        Border Stage(string label, string bg, string name) => new()
        {
            Name = name, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Background = Brush.Parse(bg), Margin = new Thickness(0, 0, 10, 0),
            Child = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 11, Foreground = Brush.Parse(bg == "#FFFFFF" ? "#6B7280" : "#9CA3AF") }, new Border { Height = 80, HorizontalAlignment = HorizontalAlignment.Left, Child = LogoDraw(v, bg) } } },
        };
        var strip = new Grid { Name = "logo-workspace", ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        strip.Children.Add(Stage("Auf hellem Grund", "#FFFFFF", "logo-light"));
        strip.Children.Add(Pos(Stage("Auf dunklem Grund", "#111827", "logo-dark"), 1));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
        foreach (var size in new[] { 24.0, 40, 64 })
            row.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(4), VerticalAlignment = VerticalAlignment.Bottom, Child = new Viewbox { Height = size, Child = VectorRender.Stage(v, "#FFFFFF") } });
        strip.Children.Add(Pos(new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Bottom, Children = { P("Klein (Briefkopf, Symbol)", Muted, 11), row } }, 2));
        DockPanel.SetDock(strip, Avalonia.Controls.Dock.Bottom);
        var root = new DockPanel { Margin = new Thickness(4, 0, 6, 4), Children = { strip, ed } };
        // kleines Fenster: die Fläche hat Vorrang, die Vorschau-Leiste kommt erst ab genug Höhe dazu
        root.SizeChanged += (_, e) => strip.IsVisible = e.NewSize.Height >= 470;
        return root;
    }

    Control LogoProperties(ProgramDocument d)
    {
        var s = LogoSpec(d);
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Für den Vorschlag – Name und Farben wirken auch auf die Zeichnung.", Muted, 11));
        void Txt(string key, string label)
        {
            sp.Children.Add(P(label, null, 11));
            var t = new TextBox { Name = "logo-p-" + key, Text = d.Text(key), PlaceholderText = label };
            var shown = d.Text(key);
            bool Commit() { var now = t.Text ?? ""; if (now == shown || now == d.Text(key)) return false; shown = now; LogoField(d, key, now, label); return true; }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => { timer.Stop(); Commit(); };
            t.TextChanged += (_, _) => { timer.Stop(); if ((t.Text ?? "") != shown) timer.Start(); };
            t.LostFocus += (_, _) => { timer.Stop(); if (Commit()) Avalonia.Threading.Dispatcher.UIThread.Post(() => Show(LogoKey)); };
            t.DetachedFromVisualTree += (_, _) => { timer.Stop(); Commit(); };
            sp.Children.Add(t);
        }
        Txt("name", "Name der Marke");
        Txt("tagline", "Untertitel (optional)");
        Txt("primary", "Hauptfarbe (#RRGGBB)");
        Txt("accent", "Akzentfarbe (#RRGGBB)");
        var cw = LogoKit.Contrast(s.Primary, "#FFFFFF"); var cb = LogoKit.Contrast(s.Primary, "#000000");
        sp.Children.Add(H("Kontrast", 13));
        sp.Children.Add(P($"Hauptfarbe zu Weiß {cw.ToString("0.0", De)}:1 · zu Schwarz {cb.ToString("0.0", De)}:1", Text, 12));
        sp.Children.Add(P(Math.Max(cw, cb) >= 4.5 ? "✓ Gut lesbar auch in kleiner Schrift." : "⚠ Unter 4,5:1 – für kleine Schrift eine kräftigere Farbe wählen.", Math.Max(cw, cb) >= 4.5 ? Brush.Parse("#4ADE80") : Brush.Parse("#F5A524"), 12));
        if (!LogoKit.IsHex(d.Text("primary")) || !LogoKit.IsHex(d.Text("accent"))) sp.Children.Add(P("Farben bitte als #RRGGBB eintragen, z. B. #1E3A8A.", Brush.Parse("#F5A524"), 12));
        var pal = VectorKit.Palette(LogoVec(d));
        if (pal.Count > 0)
        {
            sp.Children.Add(H("Farben im Logo", 13));
            var wp = new WrapPanel { Name = "logo-palette" };
            foreach (var c in pal.Take(8)) wp.Children.Add(new Border { Width = 26, Height = 26, Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(5), Background = Brush.Parse(c), [ToolTip.TipProperty] = c });
            sp.Children.Add(wp);
        }
        return sp;
    }

    // Podcast aufnehmen: MainWindow.PodcastStudio.cs (T-0697)
    // Comics & Storyboards: MainWindow.ComicStudio.cs (T-0697)
}
