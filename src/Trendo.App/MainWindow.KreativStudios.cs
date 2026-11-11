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
        void Commit() { timer.Stop(); if ((t.Text ?? "") != d.Text(key)) d.Set(key, t.Text ?? "", label); }
        timer.Tick += (_, _) => Commit();
        t.TextChanged += (_, _) => { live?.Invoke(t.Text ?? ""); timer.Stop(); if ((t.Text ?? "") != d.Text(key)) timer.Start(); };
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
            ("Länge", [
                new ProgramCmd("sum-kurz", "Kurz (3 Sätze)", d => d.Set("length", "kurz", "Länge kurz")),
                new ProgramCmd("sum-mittel", "Mittel (5 Sätze)", d => d.Set("length", "mittel", "Länge mittel"), Primary: true),
                new ProgramCmd("sum-lang", "Lang (8 Sätze)", d => d.Set("length", "lang", "Länge lang")),
            ]),
            ("Claude", [
                new ProgramCmd("sum-claude", "Mit Claude ausführlicher", d => _ = ClaudeFromStudioBar("STUDIO-SUMMARY-01", "Fasse diesen Text auf Deutsch in einfachen Worten zusammen, mit den wichtigsten Punkten als Liste:\n\n" + d.Text("text"), null), Tip: "Nutzt deinen Claude-Zugang. Ohne Anmeldung bleibt es bei der eingebauten Zusammenfassung."),
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
        box.MinHeight = 380;
        Render(d.Text("text"));
        var left = new StackPanel { Spacing = 8, Children = { title, box } };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Margin = new Thickness(4, 0, 10, 4), Children = { left, Pos(KrCard(result, "sum-card"), 2) } };
    }

    // ---------------- Logos & Marken ----------------

    static JsonObject LogoDoc(string name, string tag, string primary, string accent, string layout) => new() { ["name"] = name, ["tagline"] = tag, ["primary"] = primary, ["accent"] = accent, ["layout"] = layout, ["font"] = LogoKit.Fonts[0] };
    static LogoKit.Spec LogoSpec(ProgramDocument d) => new(d.Text("name"), d.Text("tagline"), LogoKit.IsHex(d.Text("primary")) ? d.Text("primary") : "#1E3A8A", LogoKit.IsHex(d.Text("accent")) ? d.Text("accent") : "#D4A017", d.Text("layout"), d.Text("font"));

    private void InitLogo() => RegisterProgram(FachTabs, new ProgramSpec(LogoKey, "STUDIO-LOGO-01", "Logos & Marken")
    {
        Sentence = "Logo, Farben und Schrift für deine Marke – als SVG für Druck und Web, mit Kontrastprüfung.",
        Folder = "Logos",
        Templates =
        [
            new("leer", "Neues Logo", "Name eintragen, Form und Farben wählen", () => LogoDoc("Meine Marke", "", "#1E3A8A", "#D4A017", "kreis")),
            new("cafe", "Beispiel: Café", "Warme Farben, Schild-Form", () => LogoDoc("Café Morgenrot", "Röstet seit heute", "#7A2E1F", "#E0A458", "schild")),
            new("tech", "Beispiel: Technik", "Kühle Farben, Wortmarke", () => LogoDoc("Nordlicht Labs", "Software mit Ruhe", "#0F4C5C", "#5FA8D3", "wort")),
        ],
        Ribbon =
        [
            ("Form", LogoKit.Layouts.Select((l, i) => new ProgramCmd("logo-form-" + l.Id, l.Label, d => d.Set("layout", l.Id, "Form " + l.Label), Primary: i == 0)).ToList()),
            ("Schrift", LogoKit.Fonts.Select(f => new ProgramCmd("logo-font-" + f.Replace(' ', '-').ToLowerInvariant(), f, d => d.Set("font", f, "Schrift " + f))).ToList()),
        ],
        GroupLevels = new() { ["Schrift"] = 1 },
        Exports =
        [
            new("svg", "Logo (.svg)", ".svg", (d, f) => { File.WriteAllText(f, LogoKit.Svg(LogoSpec(d))); return f; }),
            new("md", "Markenpaket (.md)", ".md", (d, f) => { File.WriteAllText(f, LogoKit.BrandMarkdown(LogoSpec(d))); return f; }),
        ],
        Workspace = LogoWorkspace,
        Properties = LogoProperties,
        Status = d => { var s = LogoSpec(d); return $"Kontrast Schrift auf Hauptfarbe {LogoKit.Contrast(s.Primary, LogoKit.TextOn(s.Primary)).ToString("0.0", De)}:1"; },
        Help = "Rechts Namen, Untertitel und Farben eintragen, oben die Form wählen. Die Vorschau zeigt das Logo auf hellem und dunklem Grund. Vor einer Markenanmeldung prüfen, ob es den Namen schon gibt.",
    }, "logo", "logos", "marke", "branding", "markenzeichen", "wortmarke", "firmenlogo", "corporate design", "markenfarben");

    /// <summary>Zeichnet das Logo nativ, gleiche Maße wie das SVG (640 × 180).</summary>
    static Control LogoDraw(LogoKit.Spec s, IBrush bg)
    {
        var c = new Canvas { Width = 640, Height = 180, Background = bg };
        IBrush P1 = Brush.Parse(s.Primary), A1 = Brush.Parse(s.Accent), On = Brush.Parse(LogoKit.TextOn(s.Primary));
        var font = new FontFamily(LogoKit.Fonts.Contains(s.Font) ? s.Font : LogoKit.Fonts[0]);
        void Add(Control x, double l, double t) { Canvas.SetLeft(x, l); Canvas.SetTop(x, t); c.Children.Add(x); }
        TextBlock T(string text, double size, IBrush fg, bool bold = true) => new() { Text = text, FontSize = size, Foreground = fg, FontFamily = font, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal };
        void Ini(IBrush fg, double size, double cy)
        {
            var t = T(LogoKit.Initials(s.Name), size, fg); t.Width = 180; t.TextAlignment = TextAlignment.Center; Add(t, 0, cy - size * 0.68);
        }
        switch (s.Layout)
        {
            case "schild": Add(new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M40,20 H140 V90 Q140,140 90,160 Q40,140 40,90 Z"), Fill = P1 }, 0, 0); Ini(On, 52, 84); break;
            case "quadrat": Add(new Rectangle { Width = 120, Height = 120, RadiusX = 26, RadiusY = 26, Fill = P1 }, 30, 30); Ini(On, 56, 88); break;
            case "wort": break;
            case "linie": Add(new Rectangle { Width = 120, Height = 8, Fill = A1 }, 30, 118); Ini(P1, 60, 84); break;
            default: Add(new Ellipse { Width = 124, Height = 124, Fill = P1, Stroke = A1, StrokeThickness = 6 }, 28, 28); Ini(On, 52, 90); break;
        }
        var tx = s.Layout == "wort" ? 30 : 180;
        Add(T(s.Name.Trim(), 44, P1), tx, 40);
        if (s.Tagline.Trim().Length > 0) Add(T(s.Tagline.Trim(), 20, A1, false), tx, 104);
        return new Viewbox { Stretch = Stretch.Uniform, Child = c };
    }

    Control LogoWorkspace(ProgramDocument d)
    {
        var s = LogoSpec(d);
        Border Stage(string label, string bg, string name) => new()
        {
            Name = name, CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Background = Brush.Parse(bg), BoxShadow = BoxShadows.Parse("0 6 20 0 #55000000"),
            Child = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = label, FontSize = 11, Foreground = Brush.Parse(bg == "#FFFFFF" ? "#6B7280" : "#9CA3AF") }, new Border { MaxHeight = 170, HorizontalAlignment = HorizontalAlignment.Left, Child = LogoDraw(s, Brush.Parse(bg)) } } },
        };
        var sp = new StackPanel { Name = "logo-workspace", Spacing = 14, Margin = new Thickness(4, 0, 10, 4) };
        if (string.IsNullOrWhiteSpace(s.Name)) { sp.Children.Add(P("Trag rechts einen Namen ein.", Muted, 13)); return sp; }
        sp.Children.Add(Stage("Auf hellem Grund", "#FFFFFF", "logo-light"));
        sp.Children.Add(Stage("Auf dunklem Grund", "#111827", "logo-dark"));
        var row = new WrapPanel();
        foreach (var size in new[] { 48.0, 96, 160 })
            row.Children.Add(new Border { Margin = new Thickness(0, 0, 12, 0), Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(6), Child = new Viewbox { Height = size * 0.6, Child = LogoDraw(s, Brushes.White) } });
        sp.Children.Add(P("Kleine Größen (Briefkopf, App-Symbol, Stempel)", Muted, 11));
        sp.Children.Add(row);
        return sp;
    }

    Control LogoProperties(ProgramDocument d)
    {
        var s = LogoSpec(d);
        var sp = new StackPanel { Spacing = 4 };
        void Txt(string key, string label) { sp.Children.Add(P(label, null, 11)); sp.Children.Add(KrBox(d, key, "logo-p-" + key, label)); }
        Txt("name", "Name der Marke");
        Txt("tagline", "Untertitel (optional)");
        Txt("primary", "Hauptfarbe (#RRGGBB)");
        Txt("accent", "Akzentfarbe (#RRGGBB)");
        var cw = LogoKit.Contrast(s.Primary, "#FFFFFF"); var cb = LogoKit.Contrast(s.Primary, "#000000");
        sp.Children.Add(H("Kontrast", 13));
        sp.Children.Add(P($"Hauptfarbe zu Weiß {cw.ToString("0.0", De)}:1 · zu Schwarz {cb.ToString("0.0", De)}:1", Text, 12));
        sp.Children.Add(P(Math.Max(cw, cb) >= 4.5 ? "✓ Gut lesbar auch in kleiner Schrift." : "⚠ Unter 4,5:1 – für kleine Schrift eine kräftigere Farbe wählen.", Math.Max(cw, cb) >= 4.5 ? Brush.Parse("#4ADE80") : Brush.Parse("#F5A524"), 12));
        if (!LogoKit.IsHex(d.Text("primary")) || !LogoKit.IsHex(d.Text("accent"))) sp.Children.Add(P("Farben bitte als #RRGGBB eintragen, z. B. #1E3A8A.", Brush.Parse("#F5A524"), 12));
        return sp;
    }

    // ---------------- Podcast aufnehmen ----------------

    static JsonObject PodcDoc(string title, string desc, string chapters) => new() { ["title"] = title, ["description"] = desc, ["chapters"] = chapters, ["audio"] = "", ["show"] = "", ["author"] = "", ["baseUrl"] = "" };

    private void InitPodcast() => RegisterProgram(FachTabs, new ProgramSpec(PodcKey, "STUDIO-PODCAST-01", "Podcast aufnehmen")
    {
        Sentence = "Folge aufnehmen oder laden, Lautheit angleichen, Kapitel setzen, Shownotes und Feed schreiben.",
        Folder = "Podcast",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Neue Folge", "Titel und Kapitel eintragen", () => PodcDoc("Folge 1", "", "00:00 Begrüßung\n")),
            new("interview", "Beispiel: Interview", "Vier Kapitel zum Ausprobieren", () => PodcDoc("Folge 7: Gärtnern auf dem Balkon", "Erika Muster erzählt, was auf zwei Quadratmetern wächst.", "00:00 Begrüßung\n02:15 Wie alles anfing\n14:40 Die besten Pflanzen\n31:05 Fragen der Hörer")),
        ],
        Ribbon =
        [
            ("Ton", [
                new ProgramCmd("podc-pick", "Aufnahme laden …", d => _ = PodcPick(d), Primary: true, Tip: "MP3, WAV, M4A oder OGG von deinem Rechner."),
                new ProgramCmd("podc-loud", "Lautheit angleichen", d => _ = PodcLoudness(d), Tip: "-16 LUFS, wie bei den großen Podcast-Plattformen üblich. Schreibt eine neue MP3 mit Kapiteln, das Original bleibt."),
                new ProgramCmd("podc-record", "Aufnehmen", d => Show("sendung"), Tip: "Öffnet die Aufnahme mit Mikrofon im Studio Live & Sendung."),
            ]),
        ],
        Exports =
        [
            new("notes", "Shownotes (.md)", ".md", (d, f) => { File.WriteAllText(f, PodcastKit.ShowNotes(d.Text("title"), d.Text("description"), PodcastKit.ParseChapters(d.Text("chapters")).Chapters)); return f; }),
            new("meta", "Kapiteldatei für FFmpeg (.txt)", ".txt", (d, f) => { File.WriteAllText(f, PodcastKit.FfMetadata(d.Text("title"), PodcastKit.ParseChapters(d.Text("chapters")).Chapters, PodcLength(d))); return f; }),
            new("rss", "Podcast-Feed (.xml)", ".xml", PodcRss),
        ],
        SimpleExports = 2,
        Workspace = PodcWorkspace,
        Properties = PodcProperties,
        Status = d => { var (c, p) = PodcastKit.ParseChapters(d.Text("chapters")); return $"{c.Count} Kapitel" + (p.Count > 0 ? $" · {p.Count} Hinweis(e)" : "") + (KreativSafe.LocalFile(d.Text("audio")) ? " · Aufnahme: " + Path.GetFileName(d.Text("audio")) : " · keine Aufnahme"); },
        Help = "Kapitel schreibst du als „Minuten:Sekunden Titel“, eine Zeile je Kapitel. „Lautheit angleichen“ braucht FFmpeg und legt eine neue MP3 neben das Original. Den Feed lädst du selbst zu deinem Webspace hoch – Trendo veröffentlicht nichts.",
    }, "podcast", "podcasts", "folge", "shownotes", "kapitelmarken", "lautheit", "rss", "hörsendung", "interview aufnehmen");

    static TimeSpan PodcLength(ProgramDocument d)
    {
        var c = PodcastKit.ParseChapters(d.Text("chapters")).Chapters;
        return double.TryParse(d.Text("seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0
            ? TimeSpan.FromSeconds(s) : (c.Count > 0 ? c[^1].Start + TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(5));
    }

    async Task PodcPick(ProgramDocument d)
    {
        if (await PickMediaFile("podc-audio", "Aufnahme", ["*.mp3", "*.wav", "*.m4a", "*.ogg", "*.flac"]) is { } f)
            ProgramRun(PodcKey, () => { d.Set("audio", f, "Aufnahme"); Show(PodcKey); });
    }

    bool _podcBusy;

    async Task PodcLoudness(ProgramDocument d)
    {
        var src = d.Text("audio");
        if (!KreativSafe.LocalFile(src)) { _progMsg[PodcKey] = "Lade zuerst eine Aufnahme von diesem Rechner."; Show(PodcKey); return; }
        if (_podcBusy) { _progMsg[PodcKey] = "Die Lautheit wird schon angeglichen – einen Moment."; Show(PodcKey); return; }
        _podcBusy = true;
        if (FfmpegPath is not { } ff) { _progMsg[PodcKey] = "FFmpeg fehlt. Du richtest es im Studio „Videos schneiden“ mit einem Klick ein."; Show(PodcKey); return; }
        var dir = Path.GetDirectoryName(src)!;
        var meta = Path.Combine(Path.GetTempPath(), "trendo-podcast-" + Guid.NewGuid().ToString("N") + ".txt");
        var output = Path.Combine(dir, Path.GetFileNameWithoutExtension(src) + "-fertig.mp3");
        _progMsg[PodcKey] = "Lautheit wird angeglichen …"; Show(PodcKey);
        try
        {
            File.WriteAllText(meta, PodcastKit.FfMetadata(d.Text("title"), PodcastKit.ParseChapters(d.Text("chapters")).Chapters, PodcLength(d)));
            var args = PodcastKit.LoudnessArgs(src, output).ToList();
            args.InsertRange(3, ["-i", meta, "-map_metadata", "1", "-map_chapters", "1", "-map", "0:a"]);
            var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
            var err = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            _progMsg[PodcKey] = p.ExitCode == 0 && File.Exists(output) ? "Fertig: " + output : "FFmpeg meldet einen Fehler: " + string.Concat(err.Split('\n').LastOrDefault(l => l.Trim().Length > 0) ?? "");
            if (p.ExitCode == 0) NotifyFileResult(_progMsg[PodcKey]);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception) { _progMsg[PodcKey] = ex.Message; }
        finally { _podcBusy = false; try { File.Delete(meta); } catch (IOException) { } }
        Show(PodcKey);
    }

    string PodcRss(ProgramDocument d, string file)
    {
        var audio = d.Text("audio");
        var fin = KreativSafe.LocalFile(audio) ? Path.Combine(Path.GetDirectoryName(audio)!, Path.GetFileNameWithoutExtension(audio) + "-fertig.mp3") : "";
        var use = File.Exists(fin) ? fin : audio;
        if (!KreativSafe.LocalFile(use)) throw new InvalidOperationException("Für den Feed braucht es eine Aufnahme auf diesem Rechner.");
        var ep = new PodcastKit.Episode(d.Text("title"), d.Text("description"), Path.GetFileName(use), new FileInfo(use).Length, PodcLength(d), DateTimeOffset.Now, d.Id);
        File.WriteAllText(file, PodcastKit.Rss(d.Text("show").Length > 0 ? d.Text("show") : d.Text("title"), d.Text("author"), d.Text("description"), d.Text("baseUrl"), [ep]));
        return file;
    }

    Control PodcWorkspace(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "podc-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        var audio = d.Text("audio");
        var info = new StackPanel { Spacing = 4 };
        info.Children.Add(H("Aufnahme", 14));
        var hasAudio = KreativSafe.LocalFile(audio);
        info.Children.Add(P(hasAudio ? $"{Path.GetFileName(audio)} · {new FileInfo(audio).Length / 1024.0 / 1024.0:0.0} MB" : "Noch keine Aufnahme. Oben „Aufnahme laden …“ oder „Aufnehmen“.", hasAudio ? Text : Muted, 12));
        info.Children.Add(P("Ziel-Lautheit -16 LUFS, Spitze -1,5 dBTP, MP3 128 kbit/s. Das Original bleibt unverändert.", Muted, 11));
        sp.Children.Add(KrCard(info, "podc-audio-card"));
        var notes = new StackPanel { Name = "podc-chapters-view", Spacing = 3 };
        void Render(string text)
        {
            notes.Children.Clear();
            var (ch, problems) = PodcastKit.ParseChapters(text);
            foreach (var c in ch)
                notes.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("70,*"), Children = { new TextBlock { Text = c.Start.TotalHours >= 1 ? c.Start.ToString(@"h\:mm\:ss") : c.Start.ToString(@"mm\:ss"), Foreground = Accent, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 13 }, Pos(new TextBlock { Text = c.Title, Foreground = Text, FontSize = 13 }, 1) } });
            foreach (var p in problems) notes.Children.Add(P("⚠ " + p, Brush.Parse("#F5A524"), 12));
        }
        var box = KrBox(d, "chapters", "podc-chapters", "00:00 Begrüßung", true, Render);
        box.FontFamily = new FontFamily("Consolas, Courier New, monospace"); box.MinHeight = 200;
        Render(d.Text("chapters"));
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Children = { new StackPanel { Spacing = 4, Children = { H("Kapitel", 14), box } }, Pos(KrCard(new StackPanel { Spacing = 6, Children = { H("So sieht es im Player aus", 13), notes } }), 2) } });
        return sp;
    }

    Control PodcProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        void Txt(string key, string label, bool multi = false) { sp.Children.Add(P(label, null, 11)); sp.Children.Add(KrBox(d, key, "podc-p-" + key, label, multi)); }
        Txt("title", "Titel der Folge");
        Txt("description", "Beschreibung", true);
        sp.Children.Add(H("Für den Feed", 13));
        Txt("show", "Name des Podcasts");
        Txt("author", "Autorin oder Autor");
        Txt("baseUrl", "Adresse deines Webspace (https://…)");
        Txt("seconds", "Länge in Sekunden (optional)");
        return sp;
    }

    // ---------------- Comics & Storyboards ----------------

    static JsonObject ComicDoc(string title, string layout, params (string Speech, string Caption)[] panels) => new()
    {
        ["title"] = title, ["layout"] = layout,
        ["panels"] = new JsonArray(panels.Select(p => (JsonNode)new JsonObject { ["speech"] = p.Speech, ["caption"] = p.Caption, ["image"] = "" }).ToArray()),
    };
    static List<ComicKit.Panel> ComicPanels(ProgramDocument d) =>
        d.List("panels").OfType<JsonObject>().Select(o => new ComicKit.Panel(Js(o["speech"]), Js(o["caption"]), Js(o["image"]))).ToList();

    static void ComicFit(JsonObject x)
    {
        var need = ComicKit.Boxes(Js(x["layout"]) is { Length: > 0 } l ? l : "2x2").Count;
        if (x["panels"] is not JsonArray a) x["panels"] = a = [];
        while (a.Count < need) a.Add(new JsonObject { ["speech"] = "", ["caption"] = "", ["image"] = "" });
    }

    private void InitComic() => RegisterProgram(FachTabs, new ProgramSpec(ComicKey, "STUDIO-COMIC-01", "Comics & Storyboards")
    {
        Sentence = "Comicseiten und Storyboards: Bildaufteilung wählen, Bilder einsetzen, Sprechblasen und Texte schreiben.",
        Folder = "Comics",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leere Seite", "Vier Bilder", () => ComicDoc("", "2x2", ("", ""), ("", ""), ("", ""), ("", ""))),
            new("beispiel", "Beispiel: Der Schlüssel", "Kurze Geschichte in vier Bildern", () => ComicDoc("Der Schlüssel", "2x2",
                ("Wo ist nur mein Schlüssel?", "Montag, 7:58 Uhr."), ("Nicht in der Jacke …", ""), ("Miau!", "Der Kater weiß mehr."), ("Danke, Kater. Wieder zu spät.", "8:15 Uhr."))),
        ],
        Ribbon =
        [
            ("Aufteilung", ComicKit.Layouts.Select((l, i) => new ProgramCmd("comic-layout-" + l.Id, l.Label, d => d.Edit("Aufteilung " + l.Label, x => { x["layout"] = l.Id; ComicFit(x); }), Primary: i == 0)).ToList()),
            ("Claude", [
                new ProgramCmd("comic-claude", "Geschichte mit Claude", d => _ = ClaudeFromStudioBar("STUDIO-COMIC-01", $"Schreib eine kurze Comic-Geschichte in {ComicKit.Boxes(d.Text("layout")).Count} Bildern, je Bild: „Bild N: Sprechblase | Bildunterschrift“. Thema: {d.Text("title")}", null), Tip: "Nutzt deinen Claude-Zugang."),
            ]),
        ],
        Exports =
        [
            new("svg", "Seite (.svg)", ".svg", (d, f) => { File.WriteAllText(f, ComicKit.Svg(d.Text("layout"), ComicPanels(d), d.Text("title"))); return f; }),
            new("md", "Storyboard-Liste (.md)", ".md", (d, f) => { File.WriteAllText(f, $"# {d.Title}\n\n" + string.Join("\n", ComicPanels(d).Select((p, i) => $"{i + 1}. {p.Speech}{(p.Caption.Length > 0 ? " — " + p.Caption : "")}"))); return f; }),
        ],
        Workspace = ComicWorkspace,
        Properties = ComicProperties,
        Status = d => $"{ComicKit.Boxes(d.Text("layout")).Count} Bilder · {ComicPanels(d).Count(p => KreativSafe.LocalFile(p.ImagePath))} mit Bild",
        Help = "Oben die Aufteilung wählen. Rechts für jedes Bild Sprechblase, Bildunterschrift und ein Bild (PNG/JPEG) eintragen. Bilder machst du z. B. im Studio „Bilder mit KI“ oder mit einer Zeichnung.",
    }, "comic", "comics", "storyboard", "manga", "bildergeschichte", "sprechblase", "panel", "graphic novel");

    Control ComicWorkspace(ProgramDocument d)
    {
        var boxes = ComicKit.Boxes(d.Text("layout")); var panels = ComicPanels(d);
        var c = new Canvas { Width = ComicKit.PageW, Height = ComicKit.PageH, Background = Brushes.White };
        void Add(Control x, double l, double t) { Canvas.SetLeft(x, l); Canvas.SetTop(x, t); c.Children.Add(x); }
        if (d.Text("title").Trim().Length > 0) Add(new TextBlock { Text = d.Text("title").Trim(), FontSize = 24, FontWeight = FontWeight.Bold, Foreground = Brushes.Black }, ComicKit.Margin, 4);
        for (var i = 0; i < boxes.Count; i++)
        {
            var b = boxes[i]; var p = i < panels.Count ? panels[i] : new ComicKit.Panel("", "", "");
            var frame = new Border { Width = b.W, Height = b.H, BorderBrush = Brushes.Black, BorderThickness = new Thickness(4), ClipToBounds = true, Background = Brush.Parse("#F3F4F6") };
            if (KreativSafe.LocalFile(p.ImagePath, KreativSafe.MaxImageBytes))
                try { frame.Child = new Image { Source = new Bitmap(p.ImagePath), Stretch = Stretch.UniformToFill }; }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException) { }
            else frame.Child = new TextBlock { Text = $"Bild {i + 1}", Foreground = Brush.Parse("#9CA3AF"), FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Add(frame, b.X, b.Y);
            if (p.Speech.Trim().Length > 0)
            {
                Add(new Border { MaxWidth = Math.Min(b.W - 40, 370), Background = Brushes.White, BorderBrush = Brushes.Black, BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(18), Padding = new Thickness(14, 10),
                    Child = new TextBlock { Text = p.Speech.Trim(), FontSize = 20, FontFamily = new FontFamily("Comic Sans MS, Segoe UI"), Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap } }, b.X + 20, b.Y + 20);
            }
            if (p.Caption.Trim().Length > 0)
                Add(new Border { Width = b.W, Height = 44, Background = Brush.Parse("#FFF3B0"), BorderBrush = Brushes.Black, BorderThickness = new Thickness(3), Padding = new Thickness(12, 8),
                    Child = new TextBlock { Text = p.Caption.Trim(), FontSize = 18, Foreground = Brushes.Black, TextTrimming = TextTrimming.CharacterEllipsis } }, b.X, b.Y + b.H - 44);
        }
        var page = new Border { Name = "comic-page", Background = Brushes.White, BoxShadow = BoxShadows.Parse("0 10 30 0 #88000000"), Child = new Viewbox { Stretch = Stretch.Uniform, Child = c }, HorizontalAlignment = HorizontalAlignment.Center };
        return new Border { Padding = new Thickness(20), Background = Brush.Parse("#0E1420"), CornerRadius = new CornerRadius(8), Child = page };
    }

    Control ComicProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Titel der Seite", null, 11)); sp.Children.Add(KrBox(d, "title", "comic-p-title", "Titel"));
        var list = d.List("panels").OfType<JsonObject>().ToList();
        var count = ComicKit.Boxes(d.Text("layout")).Count;
        for (var i = 0; i < Math.Min(count, list.Count); i++)
        {
            var index = i;
            sp.Children.Add(H($"Bild {i + 1}", 13));
            TextBox Field(string field, string label)
            {
                var t = new TextBox { Name = $"comic-{field}-{i}", Text = Js(list[i][field]), PlaceholderText = label, TextWrapping = TextWrapping.Wrap, AcceptsReturn = field == "speech" };
                t.LostFocus += (_, _) => { if ((t.Text ?? "") != Js(list[index][field])) d.Edit(label + " geändert", x => { if (x["panels"] is JsonArray a && a[index] is JsonObject o) o[field] = t.Text ?? ""; }); };
                return t;
            }
            sp.Children.Add(Field("speech", "Sprechblase"));
            sp.Children.Add(Field("caption", "Bildunterschrift"));
            var pick = new Button { Name = $"comic-image-{i}", Content = KreativSafe.LocalFile(Js(list[i]["image"])) ? "Bild: " + Path.GetFileName(Js(list[i]["image"])) : "Bild wählen …", Padding = new Thickness(10, 4) };
            pick.Click += async (_, _) =>
            {
                if (await PickMediaFile("comic-bild", "Bild (PNG oder JPEG)", ["*.png", "*.jpg", "*.jpeg"]) is { } f)
                    ProgramRun(ComicKey, () => { d.Edit("Bild eingesetzt", x => { if (x["panels"] is JsonArray a && a[index] is JsonObject o) o["image"] = f; }); Show(ComicKey); });
            };
            sp.Children.Add(pick);
        }
        return sp;
    }
}
