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
            ("Kapitel", [
                new ProgramCmd("book-chapter", "Neues Kapitel", d => d.Set("text", BookKit.NextChapter(d.Text("text")), "Neues Kapitel"), Primary: true, Tip: "Hängt „# Kapitel N“ ans Ende an."),
            ]),
            ("Claude", [
                new ProgramCmd("book-claude-next", "Weiterschreiben mit Claude", d => _ = ClaudeFromStudioBar("STUDIO-BOOK-01", "Schreib das letzte Kapitel in meinem Stil weiter, etwa eine Seite, nur erfundene Figuren:\n\n" + BookTail(d.Text("text"))), Tip: "Nutzt deinen Claude-Zugang. Du entscheidest, was übernommen wird."),
                new ProgramCmd("book-claude-edit", "Lektorat mit Claude", d => _ = ClaudeFromStudioBar("STUDIO-BOOK-01", "Lies dieses Kapitel wie eine Lektorin: Logik, Längen, Wiederholungen, Rechtschreibung. Mach Vorschläge als Liste:\n\n" + BookTail(d.Text("text")))),
            ]),
        ],
        Exports =
        [
            new("epub", "E-Book (.epub)", ".epub", (d, f) => { File.WriteAllBytes(f, BookKit.Epub(d.Text("title"), d.Text("author"), d.Text("text"), DateTimeOffset.UtcNow)); return f; }),
            new("md", "Manuskript (.md)", ".md", (d, f) => { File.WriteAllText(f, d.Text("text")); return f; }),
            new("txt", "Nur Text (.txt)", ".txt", (d, f) => { File.WriteAllText(f, string.Join("\n\n", BookKit.Chapters(d.Text("text")).Select(c => c.Title.ToUpperInvariant() + "\n\n" + c.Body))); return f; }),
        ],
        Workspace = BookWorkspace,
        Properties = BookProperties,
        Status = d => { var s = BookKit.Of(d.Text("text"), BookGoal(d)); return $"{s.Words:N0} Wörter · {s.Chapters} Kapitel · etwa {s.Pages} Seiten" + (BookGoal(d) > 0 ? $" · Ziel {s.GoalPercent.ToString("0.#", De)} %" : ""); },
        Help = "Jedes Kapitel beginnt mit „# Titel“, Absätze trennst du mit einer Leerzeile. **fett** und *kursiv* gehen auch. Rechts siehst du Kapitel, Wörter und dein Ziel. Das E-Book öffnet jeder E-Book-Leser.",
    }, "buch", "bücher", "roman", "manuskript", "kapitel", "ebook", "e-book", "epub", "kurzgeschichte", "autorin", "schreiben");

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
        box.FontFamily = new FontFamily("Georgia, Cambria, serif"); box.FontSize = 15; box.MinHeight = 460;
        Render(d.Text("text"));
        return new Grid { ColumnDefinitions = new ColumnDefinitions("2*,14,*"), Margin = new Thickness(4, 0, 10, 4), Children = { box, Pos(KrCard(right, "book-card"), 2) } };
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

    double _animTime; int _animSel; DispatcherTimer? _animPlay;

    static JsonObject AnimLayerJson(AnimKit.Layer l) => new()
    {
        ["id"] = l.Id, ["shape"] = l.Shape, ["text"] = l.Text, ["color"] = l.Color, ["w"] = l.W, ["h"] = l.H,
        ["tracks"] = new JsonObject(l.Tracks.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, new JsonArray(kv.Value.Select(k => (JsonNode)new JsonObject { ["t"] = k.T, ["v"] = k.V, ["e"] = k.Ease }).ToArray())))),
    };

    static double Num(JsonNode? n, double fallback) => n is JsonValue v && v.TryGetValue<double>(out var x) && double.IsFinite(x) ? x : fallback;

    static List<AnimKit.Layer> AnimLayers(ProgramDocument d) => d.List("layers").OfType<JsonObject>().Select(o =>
    {
        var tracks = new Dictionary<string, List<AnimKit.Key>>();
        if (o["tracks"] is JsonObject tr)
            foreach (var (prop, arr) in tr)
                if (AnimKit.Props.Contains(prop) && arr is JsonArray a)
                    tracks[prop] = a.OfType<JsonObject>().Select(k => new AnimKit.Key(Math.Max(0, Num(k["t"], 0)), Num(k["v"], AnimKit.Default(prop)), Js(k["e"]) is { Length: > 0 } e ? e : "ein-aus")).OrderBy(k => k.T).ToList();
        var shape = Js(o["shape"]); var color = Js(o["color"]);
        return new AnimKit.Layer(Js(o["id"]), AnimKit.Shapes.Contains(shape) ? shape : "rechteck", Js(o["text"]), LogoKit.IsHex(color) ? color : "#F5C542",
            Math.Clamp(Num(o["w"], 200), 4, 4000), Math.Clamp(Num(o["h"], 120), 4, 4000), tracks);
    }).ToList();

    static void AnimSave(ProgramDocument d, List<AnimKit.Layer> layers, string label) =>
        d.Edit(label, x => x["layers"] = new JsonArray(layers.Select(l => (JsonNode)AnimLayerJson(l)).ToArray()));

    static JsonObject AnimDoc(string bg, params AnimKit.Layer[] layers) => new() { ["background"] = bg, ["layers"] = new JsonArray(layers.Select(l => (JsonNode)AnimLayerJson(l)).ToArray()) };

    static AnimKit.Layer AnimSampleBall()
    {
        var l = new AnimKit.Layer("ball", "kreis", "", "#F5C542", 120, 120, []);
        AnimKit.SetKey(l, "x", 0, 200, "linear"); AnimKit.SetKey(l, "x", 2, 1080);
        AnimKit.SetKey(l, "y", 0, 120, "federnd"); AnimKit.SetKey(l, "y", 1.2, 600);
        return l;
    }

    void AnimAdd(ProgramDocument d, string shape)
    {
        var layers = AnimLayers(d);
        var id = shape + "-" + (layers.Count + 1);
        layers.Add(new AnimKit.Layer(id, shape, shape == "text" ? "Titel" : "", shape == "kreis" ? "#5FA8D3" : "#F5C542", shape == "text" ? 0 : 220, shape == "text" ? 90 : 140, []));
        AnimSave(d, layers, "Ebene hinzugefügt"); _animSel = layers.Count - 1;
    }

    void AnimPreset(ProgramDocument d, string move)
    {
        var layers = AnimLayers(d);
        if (layers.Count == 0) throw new InvalidOperationException("Lege zuerst eine Ebene an.");
        var i = Math.Clamp(_animSel, 0, layers.Count - 1); var l = layers[i];
        var p = Trendo.Core.PromptAction.AnimTitleAdapter.Title(l.Text, move, l.Color);
        var start = _animTime;
        l.Tracks.Clear();
        foreach (var (prop, keys) in p.Tracks) foreach (var k in keys) AnimKit.SetKey(l, prop, start + k.T, prop == "x" && k.V == AnimKit.StageW / 2 ? AnimKit.Default("x") : k.V, k.Ease);
        AnimSave(d, layers, "Bewegung " + move);
    }

    private void InitAnim() => RegisterProgram(FachTabs, new ProgramSpec(AnimKey, "STUDIO-ANIM-01", "Animation & Motion")
    {
        Sentence = "Formen und Titel bewegen: Schlüsselbilder setzen, Übergänge wählen, als animiertes SVG oder Video ausgeben.",
        Folder = "Animationen",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leere Bühne", "1280 × 720, dunkler Hintergrund", () => AnimDoc("#101827")),
            new("ball", "Beispiel: Springender Ball", "Ein Ball fällt und federt", () => AnimDoc("#101827", AnimSampleBall())),
            new("titel", "Beispiel: Titel", "Ein Titel fliegt ein", () => AnimDoc("#101827", Trendo.Core.PromptAction.AnimTitleAdapter.Title("Willkommen", "einfliegen", "#F5C542"))),
        ],
        Ribbon =
        [
            ("Ebene", [
                new ProgramCmd("anim-add-rechteck", "Rechteck", d => AnimAdd(d, "rechteck")),
                new ProgramCmd("anim-add-kreis", "Kreis", d => AnimAdd(d, "kreis")),
                new ProgramCmd("anim-add-text", "Text", d => AnimAdd(d, "text"), Primary: true),
            ]),
            ("Bewegung", Trendo.Core.PromptAction.AnimTitleAdapter.Moves.Select(m => new ProgramCmd("anim-move-" + m, char.ToUpper(m[0], De) + m[1..], d => AnimPreset(d, m), Tip: "Für die gewählte Ebene ab der aktuellen Zeit.")).ToList()),
            ("Ausgabe", [
                new ProgramCmd("anim-video", "Video (MP4)", d => _ = AnimVideo(d), Tip: "Braucht FFmpeg. 30 Bilder je Sekunde, 1280 × 720."),
            ]),
        ],
        GroupLevels = new() { ["Ausgabe"] = 1 },
        Exports =
        [
            new("svg", "Animiertes SVG (.svg)", ".svg", (d, f) => { File.WriteAllText(f, AnimKit.Svg(AnimLayers(d), d.Text("background"))); return f; }),
        ],
        Workspace = AnimWorkspace,
        Properties = AnimProperties,
        Status = d => { var l = AnimLayers(d); return $"{l.Count} Ebenen · {AnimKit.Duration(l).ToString("0.0", De)} s · Zeit {_animTime.ToString("0.00", De)} s"; },
        Help = "Ebene wählen, Zeit mit dem Regler einstellen, rechts einen Wert eintragen und „Schlüssel“ klicken. Zwischen zwei Schlüsseln bewegt sich die Ebene mit dem gewählten Übergang. Oben gibt es fertige Bewegungen.",
    }, "animation", "motion", "motion design", "keyframe", "schlüsselbild", "bewegung", "intro", "titelanimation", "animieren");

    Canvas AnimStage(ProgramDocument d, IReadOnlyList<AnimKit.Layer> layers, double t, int? selected)
    {
        var bg = d.Text("background");
        var c = new Canvas { Width = AnimKit.StageW, Height = AnimKit.StageH, Background = Brush.Parse(LogoKit.IsHex(bg) ? bg : "#101827"), ClipToBounds = true };
        for (var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            Control shape = l.Shape switch
            {
                "kreis" => new Ellipse { Width = l.W, Height = l.H, Fill = Brush.Parse(l.Color) },
                "text" => new TextBlock { Text = l.Text, FontSize = l.H, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(l.Color) },
                _ => new Rectangle { Width = l.W, Height = l.H, RadiusX = 12, RadiusY = 12, Fill = Brush.Parse(l.Color) },
            };
            shape.Measure(Size.Infinity);
            var w = shape.DesiredSize.Width; var h = shape.DesiredSize.Height;
            var x = AnimKit.ValueOf(l, "x", t); var y = AnimKit.ValueOf(l, "y", t);
            shape.Opacity = Math.Clamp(AnimKit.ValueOf(l, "deckkraft", t), 0, 1);
            shape.RenderTransformOrigin = RelativePoint.Center;
            var s = AnimKit.ValueOf(l, "skalierung", t);
            shape.RenderTransform = new TransformGroup { Children = { new ScaleTransform(s, s), new RotateTransform(AnimKit.ValueOf(l, "drehung", t)) } };
            Canvas.SetLeft(shape, x - w / 2); Canvas.SetTop(shape, y - h / 2);
            c.Children.Add(shape);
            if (selected == i)
            {
                var mark = new Rectangle { Width = w * s + 12, Height = h * s + 12, Stroke = Brush.Parse("#FFD54F"), StrokeThickness = 2, StrokeDashArray = [4, 3] };
                Canvas.SetLeft(mark, x - (w * s + 12) / 2); Canvas.SetTop(mark, y - (h * s + 12) / 2); c.Children.Add(mark);
            }
        }
        return c;
    }

    Control AnimWorkspace(ProgramDocument d)
    {
        var layers = AnimLayers(d);
        _animSel = Math.Clamp(_animSel, 0, Math.Max(0, layers.Count - 1));
        var dur = AnimKit.Duration(layers);
        _animTime = Math.Clamp(_animTime, 0, dur);
        var view = new Viewbox { Name = "anim-stage", Stretch = Stretch.Uniform, Child = AnimStage(d, layers, _animTime, layers.Count > 0 ? _animSel : null) };
        var time = new TextBlock { Name = "anim-time", Foreground = Text, FontFamily = new FontFamily("Consolas, monospace"), VerticalAlignment = VerticalAlignment.Center, Width = 90, Text = $"{_animTime:0.00} s" };
        var slider = new Slider { Name = "anim-slider", Minimum = 0, Maximum = dur, Value = _animTime, VerticalAlignment = VerticalAlignment.Center };
        void Draw(double t) { _animTime = t; view.Child = AnimStage(d, layers, t, _animPlay is null && layers.Count > 0 ? _animSel : null); time.Text = $"{t:0.00} s"; }
        slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && Math.Abs(slider.Value - _animTime) > 0.0001) Draw(slider.Value); };
        var play = new Button { Name = "anim-play", Content = _animPlay is null ? "Abspielen" : "Stopp", Padding = new Thickness(12, 5) };
        play.Click += (_, _) =>
        {
            if (_animPlay is { } running) { running.Stop(); _animPlay = null; play.Content = "Abspielen"; Draw(_animTime); return; }
            var start = DateTime.UtcNow; var from = _animTime >= dur ? 0 : _animTime;
            _animPlay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _animPlay.Tick += (_, _) => { var t = from + (DateTime.UtcNow - start).TotalSeconds; if (t >= dur) { t = dur; _animPlay?.Stop(); _animPlay = null; play.Content = "Abspielen"; } slider.Value = t; Draw(t); };
            _animPlay.Start(); play.Content = "Stopp";
        };
        view.DetachedFromVisualTree += (_, _) => { _animPlay?.Stop(); _animPlay = null; };
        // Zeitleiste: Schlüssel der gewählten Ebene als Punkte
        var keys = new Canvas { Name = "anim-keys", Height = 16 };
        keys.SizeChanged += (_, e) =>
        {
            keys.Children.Clear();
            if (layers.Count == 0) return;
            foreach (var k in layers[_animSel].Tracks.Values.SelectMany(v => v))
            {
                var dot = new Ellipse { Width = 9, Height = 9, Fill = Accent };
                Canvas.SetLeft(dot, e.NewSize.Width * k.T / dur - 4.5); Canvas.SetTop(dot, 3); keys.Children.Add(dot);
            }
        };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(0, 8, 0, 0),
            Children = { play, Pos(time, 1), Pos(slider, 2), new Border { [Grid.RowProperty] = 1, [Grid.ColumnProperty] = 2, Child = keys } } };
        Grid.SetColumn(time, 1); time.Margin = new Thickness(10, 0);
        var stage = new Border { Background = Brush.Parse("#0A0F1A"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), BoxShadow = BoxShadows.Parse("0 8 24 0 #66000000"), Child = view };
        return new DockPanel { Margin = new Thickness(4, 0, 10, 4), Children = { new Border { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Bottom, Child = bar }, stage } };
    }

    Control AnimProperties(ProgramDocument d)
    {
        var layers = AnimLayers(d);
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Hintergrund (#RRGGBB)", null, 11)); sp.Children.Add(KrBox(d, "background", "anim-p-background", "#101827"));
        sp.Children.Add(H("Ebenen", 13));
        if (layers.Count == 0) { sp.Children.Add(P("Noch keine Ebene. Oben „Rechteck“, „Kreis“ oder „Text“.", Muted, 12)); return sp; }
        for (var i = 0; i < layers.Count; i++)
        {
            var idx = i;
            var b = new Button { Name = "anim-layer-" + i, Content = $"{layers[i].Shape} · {(layers[i].Text.Length > 0 ? layers[i].Text : layers[i].Id)}", HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 4) };
            if (i == _animSel) b.Classes.Add("accent");
            b.Click += (_, _) => { _animSel = idx; Show(AnimKey); };
            sp.Children.Add(b);
        }
        var l = layers[_animSel];
        sp.Children.Add(H("Gewählte Ebene", 13));
        TextBox Field(string name, string label, string value, Action<string> apply)
        {
            var t = new TextBox { Name = name, Text = value, PlaceholderText = label };
            t.LostFocus += (_, _) => { if ((t.Text ?? "") != value) ProgramRun(AnimKey, () => { apply(t.Text ?? ""); Show(AnimKey); }); };
            sp.Children.Add(P(label, null, 11)); sp.Children.Add(t);
            return t;
        }
        if (l.Shape == "text") Field("anim-p-text", "Text", l.Text, v => { var ls = AnimLayers(d); ls[_animSel] = ls[_animSel] with { Text = v }; AnimSave(d, ls, "Text geändert"); });
        Field("anim-p-color", "Farbe (#RRGGBB)", l.Color, v => { if (!LogoKit.IsHex(v)) throw new ArgumentException("Farbe als #RRGGBB angeben."); var ls = AnimLayers(d); ls[_animSel] = ls[_animSel] with { Color = v }; AnimSave(d, ls, "Farbe geändert"); });
        sp.Children.Add(H($"Schlüssel bei {_animTime.ToString("0.00", De)} s", 13));
        var ease = new ComboBox { Name = "anim-ease", ItemsSource = AnimKit.Eases.Select(e => e.Label).ToList(), SelectedIndex = 3, HorizontalAlignment = HorizontalAlignment.Stretch };
        sp.Children.Add(P("Übergang zum nächsten Schlüssel", null, 11)); sp.Children.Add(ease);
        foreach (var prop in AnimKit.Props)
        {
            var p = prop;
            var box = new TextBox { Name = "anim-val-" + p, Text = AnimKit.ValueOf(l, p, _animTime).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), Width = 90 };
            var set = new Button { Name = "anim-key-" + p, Content = "Schlüssel", Padding = new Thickness(8, 3) };
            set.Click += (_, _) => ProgramRun(AnimKey, () =>
            {
                if (!double.TryParse((box.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v)) throw new FormatException("Bitte eine Zahl eintragen.");
                var ls = AnimLayers(d); AnimKit.SetKey(ls[_animSel], p, _animTime, v, AnimKit.Eases[Math.Max(0, ease.SelectedIndex)].Id);
                AnimSave(d, ls, $"Schlüssel {p}"); Show(AnimKey);
            });
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("90,*,Auto"), Margin = new Thickness(0, 2), Children = { new TextBlock { Text = p, Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, Pos(box, 1), Pos(set, 2) } });
        }
        var del = new Button { Name = "anim-delete", Content = "Ebene löschen", Padding = new Thickness(8, 3), Margin = new Thickness(0, 8, 0, 0) };
        del.Click += (_, _) => ProgramRun(AnimKey, () => { var ls = AnimLayers(d); ls.RemoveAt(_animSel); _animSel = Math.Max(0, _animSel - 1); AnimSave(d, ls, "Ebene gelöscht"); Show(AnimKey); });
        sp.Children.Add(del);
        return sp;
    }

    /// <summary>Tests: Bilder zu Video (Bildmuster, Bilder je Sekunde, Ziel) statt FFmpeg.</summary>
    public static Func<string, int, string, bool>? AnimVideoOverride { get; set; }
    bool _animBusy;

    /// <summary>Jedes Bild zeichnen, dann FFmpeg (H.264). Die Bildfolge liegt nur kurz im Temp-Ordner.</summary>
    public async Task<string?> AnimVideo(ProgramDocument d, string? output = null)
    {
        if (_animBusy) { _progMsg[AnimKey] = "Das Video entsteht schon – einen Moment."; Show(AnimKey); return null; }
        var ff = AnimVideoOverride is null ? FfmpegPath : "ffmpeg";
        if (ff is null) { _progMsg[AnimKey] = "Für das Video fehlt FFmpeg. Du richtest es im Studio „Videos schneiden“ mit einem Klick ein."; Show(AnimKey); return null; }
        var layers = AnimLayers(d); var dur = AnimKit.Duration(layers);
        if (dur > 60) { _progMsg[AnimKey] = "Videos gehen bis 60 Sekunden."; Show(AnimKey); return null; }
        const int fps = 30;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-animation-" + Guid.NewGuid().ToString("N")[..8]);
        output ??= Path.Combine(_core.DataRoot, "animationen", $"{AnimFileName(d.Title)}-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
        _animBusy = true; _progMsg[AnimKey] = "Video entsteht …"; Show(AnimKey);
        try
        {
            Directory.CreateDirectory(dir); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var n = (int)Math.Ceiling(dur * fps);
            for (var i = 0; i <= n; i++)
            {
                var stage = AnimStage(d, layers, Math.Min(dur, (double)i / fps), null);
                stage.Measure(new Size(AnimKit.StageW, AnimKit.StageH)); stage.Arrange(new Rect(0, 0, AnimKit.StageW, AnimKit.StageH));
                using var bmp = new RenderTargetBitmap(new PixelSize((int)AnimKit.StageW, (int)AnimKit.StageH));
                bmp.Render(stage);
                bmp.Save(Path.Combine(dir, $"f{i:00000}.png"));
                if (i % 3 == 0) await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); // Oberfläche bleibt bedienbar
            }
            var pattern = Path.Combine(dir, "f%05d.png");
            bool ok;
            if (AnimVideoOverride is { } o) ok = o(pattern, fps, output);
            else
            {
                var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                foreach (var a in AnimKit.VideoArgs(pattern, fps, output)) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
                _ = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                ok = p.ExitCode == 0;
            }
            _progMsg[AnimKey] = ok && File.Exists(output) ? "Video fertig: " + output : "FFmpeg konnte das Video nicht schreiben.";
            if (ok) NotifyFileResult(_progMsg[AnimKey]);
            return ok && File.Exists(output) ? output : null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception) { _progMsg[AnimKey] = ex.Message; return null; }
        finally { _animBusy = false; try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } Show(AnimKey); }
    }

    static string AnimFileName(string s) { var x = string.Concat((s ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim(); return x.Length == 0 ? "animation" : KreativSafe.Cut(x, 60); }

    // ---------------- Fotos verwalten ----------------

    List<Photo>? _photosScan; List<Move>? _photosPlan; bool _photosAsk, _photosBusy;

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

    async Task<string?> PickFolderPath(string title)
    {
        if (FolderPickOverride is { } o) return o(title);
        var r = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
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
        Status = d => (CoverModel() is { } m ? "Bild-KI: " + m.Model.Name : ImageAiOverride is not null ? "Bild-KI: Test" : "Bild-KI nicht eingerichtet") + $" · {ImgList(d).Count} Bilder",
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
        if (ImageAiOverride is null && (cm is null || _opt.Isolated)) { _progMsg[ImgAiKey] = "Die Bild-KI ist noch nicht eingerichtet. Oben „Bild-KI einrichten“ öffnet die KI-Bibliothek (SD Turbo oder SDXL Turbo, kostenlos)."; Show(ImgAiKey); return 0; }
        var xl = cm?.Model.Id != "sd-turbo";
        var (w, h) = ImageAiKit.Size(d.Text("format"), xl ? 1024 : 512);
        var count = int.TryParse(d.Text("count"), out var c) ? Math.Clamp(c, 1, 4) : 2;
        var baseSeed = int.TryParse(d.Text("seed"), out var s) ? s : 42;
        if (!ImgDirOk(d.Text("dir"))) d.Set("dir", Guid.NewGuid().ToString("N"), "Bildordner"); // ältere Dokumente: Ordner einmal festlegen
        var dir = ImgDir(d);
        Directory.CreateDirectory(dir);
        _imgAiBusy = true; var made = new List<string>();
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
                        if (!r.Ok || !File.Exists(png)) r = await Task.Run(() => RunWithInput(cm.Value.Exe, [.. args, "--offload-to-cpu"], null));
                        ok = r.Ok;
                    }
                }
                if (!ok || !File.Exists(png)) { _progMsg[ImgAiKey] = "Die Bild-KI hat kein Bild geliefert. Ist die Grafikkarte gerade mit etwas anderem beschäftigt?"; break; }
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
