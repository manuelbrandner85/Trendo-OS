using System.IO.Compression;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0697 (D4b): Comics &amp; Storyboards und Podcast aufnehmen als echte Programme – über Maus, Tasten und Knöpfe bis zur fertigen Datei.</summary>
public class T0697ComicPodcast_Ui
{
    const string CK = "comic-programm", PK = "podcast-programm";
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static string Export(MainWindow w, string key, string name, string file) => MainWindow.Programs[key].Exports.Single(x => x.Name == name).Write(w.ProgramActive(key)!, file);
    /// <summary>Optionaler Belegordner (reports/T-0697): Tests legen dort ihre Ausgaben ab, wenn TRENDO_BELEG gesetzt ist.</summary>
    static string? Beleg => Environment.GetEnvironmentVariable("TRENDO_BELEG") is { Length: > 0 } b ? Directory.CreateDirectory(b).FullName : null;

    /// <summary>T-0730: Rohtext für Belege anonymisieren – Testordner → &lt;testordner&gt;, Benutzerordner → C:\Users\&lt;user&gt;, Benutzername → &lt;user&gt;.
    /// Wirkt auf rohe und auf JSON-maskierte Pfade (doppelte Backslashes), damit auch fertige JSON-Ausgaben (ffprobe) sauber werden.</summary>
    internal static string AnonBeleg(string s, string? root)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        var pairs = new List<(string From, string To)>();
        if (root is { Length: > 3 }) pairs.Add((root.TrimEnd('\\', '/'), "<testordner>"));
        if (home.Length > 3 && Path.GetDirectoryName(home) is { Length: > 0 } parent) pairs.Add((home, Path.Combine(parent, "<user>")));
        foreach (var (from, to) in pairs.OrderByDescending(p => p.From.Length))
            s = s.Replace(from, to, StringComparison.OrdinalIgnoreCase)
                 .Replace(from.Replace("\\", "\\\\"), to.Replace("\\", "\\\\"), StringComparison.OrdinalIgnoreCase)
                 .Replace(from.Replace('\\', '/'), to.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        foreach (var name in new[] { Path.GetFileName(home), Environment.UserName }.Distinct())
            if (name is { Length: >= 3 })
                s = System.Text.RegularExpressions.Regex.Replace(s, @"(?:(?<![A-Za-z0-9])|(?<=%5[Cc])|(?<=%2[Ff]))" + System.Text.RegularExpressions.Regex.Escape(name) + "(?![A-Za-z0-9])", "<user>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return s;
    }

    /// <summary>T-0730: alle Zeichenketten eines JSON-Baums vor dem Serialisieren anonymisieren (Kopie, das Original bleibt unverändert).</summary>
    internal static JsonNode? AnonJson(JsonNode? n, string? root) => n switch
    {
        JsonObject o => new JsonObject(o.Select(kv => KeyValuePair.Create(kv.Key, AnonJson(kv.Value, root)))),
        JsonArray a => new JsonArray(a.Select(x => AnonJson(x, root)).ToArray()),
        JsonValue v when v.TryGetValue<string>(out var s) => JsonValue.Create(AnonBeleg(s, root)),
        _ => n?.DeepClone(),
    };

    /// <summary>T-0730: Beleg-JSON wie bisher (Standard-Maskierung, eingerückt), nur die Platzhalter &lt;…&gt; bleiben lesbar.</summary>
    internal static string BelegJson(JsonNode n, string? root) =>
        AnonJson(n, root)!.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace("\\u003C", "<").Replace("\\u003E", ">");

    [Fact(DisplayName = "T-0730 Podcast-Beleg: Pfade mit Testordner, Benutzerordner und Benutzername werden vor dem Serialisieren ersetzt – auch JSON-maskiert")]
    public void Beleg_anonymisiert()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(home, "AppData", "Local", "Temp", "trendo-ui-test");
        var data = new JsonObject
        {
            ["tracks"] = new JsonArray(new JsonObject { ["file"] = Path.Combine(root, "ersatz-mikrofon-1.wav") }),
            ["master"] = Path.Combine(home, "AppData", "Local", "Temp", "anders", "fertig.mp3"),
            ["n"] = 3,
        };
        var json = BelegJson(data, root);
        var probe = new JsonObject { ["format"] = new JsonObject { ["filename"] = Path.Combine(home, "x.mp3") } }.ToJsonString(); // wie ffprobe: maskierte Backslashes
        var probeAnon = AnonBeleg(probe, root);
        foreach (var t in new[] { json, probeAnon })
        {
            Assert.DoesNotContain(home, t, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(home.Replace("\\", "\\\\"), t, StringComparison.OrdinalIgnoreCase);
            if (Environment.UserName.Length >= 3) Assert.DoesNotMatch(@"(?i)(?<![A-Za-z0-9])" + System.Text.RegularExpressions.Regex.Escape(Environment.UserName) + "(?![A-Za-z0-9])", t);
        }
        Assert.Contains("<testordner>", json);
        Assert.Contains("<user>", json);
        Assert.Contains("<user>", probeAnon);
        Assert.Contains("\"n\": 3", json);
        Assert.Equal(Path.Combine(root, "ersatz-mikrofon-1.wav"), (string?)data["tracks"]![0]!["file"]); // Original unverändert
    }

    static Point Screen(MainWindow w, ComicPageView v, double x, double y)
    {
        var o = new Point((v.Bounds.Width - ComicBook.PageW * v.Scale) / 2, (v.Bounds.Height - ComicBook.PageH * v.Scale) / 2);
        return v.TranslatePoint(new Point(o.X + x * v.Scale, o.Y + y * v.Scale), w)!.Value;
    }
    static void Drag(MainWindow w, Point a, Point b, RawInputModifiers mods = RawInputModifiers.None)
    {
        Thread.Sleep(600); // kein Doppelklick mit dem vorigen Klick
        w.MouseDown(a, MouseButton.Left, mods); for (var i = 1; i <= 12; i++) w.MouseMove(a + (b - a) * (i / 12.0), mods); w.MouseUp(b, MouseButton.Left, mods); Settle();
    }
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); }
    static void Click(MainWindow w, Point p) { Thread.Sleep(600); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph, RawInputModifiers m = RawInputModifiers.None) { w.KeyPress(k, m, ph, null); w.KeyRelease(k, m, ph, null); Settle(); }

    [AvaloniaFact(DisplayName = "T-0697 Comic: Bildfeld aufziehen, im Feld zeichnen (Strich bleibt im Feld), Sprechblase setzen, Dorn richten, Feld verformen, Entf, Rückgängig – drei Seiten als PDF, PNG und CBZ")]
    public void Comic_von_der_leeren_Seite_bis_PDF()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root); w.Width = 1800; w.Height = 1400;
        try
        {
            w.Show(CK); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(CK)!;
            Assert.NotNull(w.Find<Button>("prog-first-step")); // erster Schritt im Leerzustand
            var v = w.Find<ComicPageView>("comic-view"); Settle();

            // Bildfeld aufziehen (F)
            RealClick(w, w.Find<Button>("prog-cmd-comic-tool-feld")); Assert.Equal("feld", w.ComicTool);
            v = w.Find<ComicPageView>("comic-view");
            Drag(w, Screen(w, v, 100, 1150), Screen(w, v, 600, 1350));
            var page = MainWindow.ComicOf(d).Pages[0];
            Assert.True(page.Panels.Count == 5, $"Felder {page.Panels.Count}, Meldung: {w.ProgramMessage(CK)}, Werkzeug {w.ComicTool}, Ansicht {v.Bounds} neu {w.ComicViewForTests?.Bounds}");
            var (nx, ny, nw, nh) = page.Panels[4].Bounds(); Assert.InRange(nx, 95, 105); Assert.InRange(nw, 490, 510);

            // Zeichnen im ersten Bildfeld – über den rechten Rand hinaus: außerhalb bleibt nichts
            RealClick(w, w.Find<Button>("prog-cmd-comic-tool-pinsel")); w.ComicSize = 12; w.ComicColor = "#C0392B"; w.Show(CK); Settle();
            v = w.Find<ComicPageView>("comic-view");
            var f0 = page.Panels[0].Bounds();
            Drag(w, Screen(w, v, f0.X + 100, f0.Y + 200), Screen(w, v, f0.X + f0.W + 120, f0.Y + 200));
            var art = MainWindow.ComicOf(d).Pages[0].Paint().Composite(false);
            var inside = art.At((int)(f0.X + 200), (int)(f0.Y + 200)); Assert.True(inside.A > 200 && inside.R > 150, $"im Feld gezeichnet: {inside} letzter Schritt {d.UndoLabel} Meldung {w.ProgramMessage(CK)} Werkzeug {w.ComicTool} hasArt {MainWindow.ComicOf(d).Pages[0].HasArt} max {Enumerable.Range(0, art.Px.Length / 4).Max(i => art.Px[i * 4 + 3])}");
            Assert.Equal(0, art.At((int)(f0.X + f0.W + 60), (int)(f0.Y + 200)).A); // Strich blieb im Feld
            Assert.True(MainWindow.ComicOf(d).Pages[0].HasArt);

            // Sprechblase setzen (T) an der Mausposition
            RealClick(w, w.Find<Button>("prog-cmd-comic-tool-blase")); v = w.Find<ComicPageView>("comic-view");
            Click(w, Screen(w, v, 300, 300));
            var b = Assert.Single(MainWindow.ComicOf(d).Pages[0].Balloons);
            Assert.Equal(("balloon", b.Id), w.ComicSel);
            // Text schreiben: Zeilenumbruch lässt die Blase wachsen
            var tb = w.Find<TextBox>("comic-sel-text"); tb.Focus(); tb.Text = "Das ist ein ziemlich langer Satz, der in einer kleinen Blase umbrechen muss, damit nichts abgeschnitten wird."; w.Find<TextBox>("comic-p-title").Focus(); Settle();
            var b2 = MainWindow.ComicOf(d).Pages[0].Balloons[0];
            Assert.StartsWith("Das ist", b2.Text); Assert.True(b2.H > b.H, $"Blase gewachsen: {b.H} → {b2.H}");
            Assert.True(ComicBook.Wrap(b2.Text, b2.W - 2 * b2.Pad, b2.Size).Count > 1);

            // Auswahl: Dorn auf eine Figur richten, Blase verschieben
            RealClick(w, w.Find<Button>("prog-cmd-comic-tool-auswahl")); v = w.Find<ComicPageView>("comic-view");
            Drag(w, Screen(w, v, b2.TailX, b2.TailY), Screen(w, v, 420, 640));
            var b3 = MainWindow.ComicOf(d).Pages[0].Balloons[0];
            Assert.InRange(b3.TailX, 410, 430); Assert.InRange(b3.TailY, 630, 650); Assert.Equal(b2.X, b3.X);
            v = w.Find<ComicPageView>("comic-view");
            Drag(w, Screen(w, v, b3.X + b3.W / 2, b3.Y + b3.H / 2), Screen(w, v, b3.X + b3.W / 2 + 50, b3.Y + b3.H / 2 + 20));
            var b4 = MainWindow.ComicOf(d).Pages[0].Balloons[0]; Assert.True(Math.Abs(b4.X - b3.X - 50) < 6, $"verschoben {b4.X - b3.X}, Auswahl {w.ComicSel}, Schritt {d.UndoLabel}, b3 {b3.X},{b3.Y},{b3.W},{b3.H} tail {b3.TailX},{b3.TailY}");

            // Bildfeld wählen und eine Ecke ziehen: freies Viereck
            v = w.Find<ComicPageView>("comic-view"); var p4 = MainWindow.ComicOf(d).Pages[0].Panels[4];
            Click(w, Screen(w, v, p4.P[0] + 250, p4.P[1] + 100)); Assert.Equal(("panel", p4.Id), w.ComicSel);
            v = w.Find<ComicPageView>("comic-view");
            Drag(w, Screen(w, v, p4.P[2], p4.P[3]), Screen(w, v, p4.P[2] + 80, p4.P[3] - 40)); // obere rechte Ecke
            var q = MainWindow.ComicOf(d).Pages[0].Panels[4].P;
            Assert.InRange(q[2], p4.P[2] + 70, p4.P[2] + 90); Assert.Equal(p4.P[4], q[4]); // nur diese Ecke

            // Entf löscht das gewählte Feld, Rückgängig holt es zurück
            w.ComicViewForTests!.Focus(); Key1(w, Key.Delete, PhysicalKey.Delete);
            Assert.Equal(4, MainWindow.ComicOf(d).Pages[0].Panels.Count);
            Assert.True(d.Undo()); Assert.Equal(5, MainWindow.ComicOf(d).Pages[0].Panels.Count);

            // Rechtsklick-Menü einer Blase: „Als Gedankenblase“
            var menu = w.ComicViewForTests!.MenuAt!(b4.X + b4.W / 2, b4.Y + b4.H / 2)!;
            var asThought = menu.Items.OfType<MenuItem>().Single(m => m.Name == "comic-menu-kind-gedanke");
            asThought.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Equal("gedanke", MainWindow.ComicOf(d).Pages[0].Balloons[0].Kind);

            // drei Seiten
            RealClick(w, w.Find<Button>("prog-cmd-comic-page-add")); RealClick(w, w.Find<Button>("prog-cmd-comic-page-add"));
            Assert.Equal(3, MainWindow.ComicOf(d).Pages.Count); Assert.Equal(2, MainWindow.ComicPageOf(d));
            w.ComicAddBalloon(d, "kasten"); w.Show(CK); Settle();
            w.ComicViewForTests!.Focus(); Key1(w, Key.PageUp, PhysicalKey.PageUp); Assert.Equal(1, MainWindow.ComicPageOf(d));

            // Ausgabe
            var dir = Beleg ?? root;
            var pdf = Export(w, CK, "pdf", Path.Combine(dir, "comic-3-seiten.pdf"));
            using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
            {
                Assert.Equal(3, doc.PageCount);
                Assert.InRange(doc.Pages[0].Width.Millimeter, 209, 211); Assert.InRange(doc.Pages[0].Height.Millimeter, 296, 298); // DIN A4
            }
            var cbz = Export(w, CK, "cbz", Path.Combine(dir, "comic-3-seiten.cbz"));
            using (var zip = ZipFile.OpenRead(cbz))
            {
                Assert.Equal(["001.png", "002.png", "003.png", "ComicInfo.xml"], zip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
                using var s = zip.GetEntry("001.png")!.Open(); using var ms = new MemoryStream(); s.CopyTo(ms);
                var px = PngCodec.Decode(ms.ToArray()); Assert.InRange(px.W, 1990, 2010); Assert.InRange(px.H / (double)px.W, 1.41, 1.42); var k = px.W / ComicBook.PageW;
                // Zeichnung und Rahmen sind im Bild: rote Tusche im ersten Feld (Seitenpunkt × 2)
                var red = px.At((int)((f0.X + 200) * k), (int)((f0.Y + 200) * k)); Assert.True(red.R > 150 && red.G < 120, $"Tusche im Export: {red}");
                var ink = px.At((int)(f0.X * k), (int)((f0.Y + f0.H / 2) * k)); Assert.True(ink.R < 60, $"Rahmen im Export: {ink}");
            }
            var png = Export(w, CK, "png", Path.Combine(dir, "comic-seite.png"));
            Assert.True(File.Exists(png) && File.Exists(Path.Combine(dir, "comic-seite-2.png")) && File.Exists(Path.Combine(dir, "comic-seite-3.png")));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0697 Comic: KI schreibt die Geschichte in die Bildfelder des offenen Hefts – ein Schritt, Rückgängig holt den alten Stand")]
    public async Task Comic_KI_ins_offene_Dokument()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(CK); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(CK)!; string? asked = null;
            w.ComicKiOverride = p => { asked = p; return Task.FromResult<string?>("Bild 1: Guten Morgen! | Montag.\nBild 2: Wo ist der Kaffee?\nBild 3: Miau.\nBild 4: Ach so. | Ende."); };
            RealClick(w, w.Find<Button>("prog-cmd-comic-claude"));
            await Until(() => MainWindow.ComicOf(d).Pages[0].Balloons.Count > 0);
            Assert.Contains("genau 4 Bildern", asked);
            var page = MainWindow.ComicOf(d).Pages[0];
            Assert.Equal(6, page.Balloons.Count); // 4 Sprechblasen + 2 Textkästen
            Assert.Contains(page.Balloons, b => b.Text == "Wo ist der Kaffee?" && b.Kind == "sprech");
            Assert.Contains(page.Balloons, b => b.Text == "Ende." && b.Kind == "kasten");
            Assert.Equal("Geschichte von der KI", d.UndoLabel);
            Assert.True(d.Undo()); Assert.Empty(MainWindow.ComicOf(d).Pages[0].Balloons);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0697 Comic: altes Dokument (feste Aufteilung, Texte je Bild) wird gelesen und als freie Blasen gezeigt")]
    public void Comic_altes_Format_bleibt_lesbar()
    {
        var old = new JsonObject { ["title"] = "Alt", ["layout"] = "3-reihen", ["panels"] = new JsonArray(
            new JsonObject { ["speech"] = "Hallo", ["caption"] = "", ["image"] = "" }, new JsonObject { ["speech"] = "", ["caption"] = "Später.", ["image"] = "" }, new JsonObject { ["speech"] = "", ["caption"] = "", ["image"] = "" }) };
        var b = ComicBook.Read(old);
        Assert.Single(b.Pages); Assert.Equal(3, b.Pages[0].Panels.Count);
        Assert.Contains(b.Pages[0].Balloons, x => x.Text == "Hallo" && x.Kind == "sprech");
        Assert.Contains(b.Pages[0].Balloons, x => x.Text == "Später." && x.Kind == "kasten");
    }

    [AvaloniaFact(DisplayName = "T-0697 Storyboard: Einstellung beschreiben, Bildfelder werden Einstellungen in „Filme machen“")]
    public async Task Storyboard_an_Filme_machen()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show(CK); Settle(); RealClick(w, w.Find<Button>("prog-tpl-storyboard")); Settle();
            var d = w.ProgramActive(CK)!;
            Assert.True(MainWindow.ComicOf(d).Storyboard); Assert.Equal(6, MainWindow.ComicOf(d).Pages[0].Panels.Count);
            w.ComicSel = ("panel", MainWindow.ComicOf(d).Pages[0].Panels[0].Id); w.Show(CK); Settle();
            var shot = w.Find<TextBox>("comic-shot"); shot.Focus(); shot.Text = "Totale: Hof im Morgenlicht."; w.Find<TextBox>("comic-p-title").Focus(); Settle();
            Assert.Equal("Totale: Hof im Morgenlicht.", MainWindow.ComicOf(d).Pages[0].Panels[0].Shot);
            Assert.Contains("Totale: Hof im Morgenlicht.", File.ReadAllText(Export(w, CK, "md", Path.Combine(root, "liste.md"))));
            var film = await w.ComicToFilm(d);
            Assert.True(film is not null, w.ProgramMessage(CK));
            Assert.Equal(6, Trendo.Core.Film.FilmCut.Shots(film!.Data).Count);
            Assert.All(Trendo.Core.Film.FilmCut.Shots(film.Data), s => Assert.True(File.Exists(s.Material!.File)));
        }
        finally { w.Close(); }
    }

    // ---------- Podcast ----------
    /// <summary>Ersatz für das Mikrofon (so gekennzeichnet): im Test erzeugte Sprache-ähnliche Töne mit Pause.</summary>
    static string TestWav(string file, double seconds, Func<double, double> amp, double hz)
    {
        var n = (int)(seconds * 44100); var c = new float[n]; var rnd = new Random(7);
        for (var i = 0; i < n; i++) { var t = i / 44100.0; c[i] = (float)(amp(t) * (Math.Sin(2 * Math.PI * hz * t) * 0.7 + Math.Sin(2 * Math.PI * hz * 2.5 * t) * 0.3) * (0.75 + 0.25 * Math.Sin(2 * Math.PI * 3 * t)) + (rnd.NextDouble() - 0.5) * 0.0004); }
        AudioFile.WriteWav(new AudioBuf(44100, [c]), file); return file;
    }

    [AvaloniaFact(DisplayName = "T-0697 Podcast echt mit FFmpeg: zwei Spuren aufnehmen (Mikrofon durch Test-WAV ersetzt), Stelle an der Welle löschen (Kapitel rückt), Pausen kürzen, fertig machen → ffprobe: Dauer, −16 LUFS ±1, Kapitel, Tags")]
    public async Task Podcast_aufnehmen_schneiden_fertig()
    {
        var ff = Cli.Find("ffmpeg"); var probe = Cli.Find("ffprobe");
        if (ff is null || probe is null) { Assert.Skip("Übersprungen – FFmpeg/ffprobe fehlt auf diesem PC."); return; }
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root); w.Width = 1500; w.Height = 1000;
        try
        {
            var dir = Beleg ?? root;
            // Sprecher 1: 0–8 s Sprache, 8–11 s Pause, 11–20 s Sprache; Sprecher 2: nur 3–6 s (gleichzeitig aufgenommen)
            var s1 = TestWav(Path.Combine(root, "ersatz-mikrofon-1.wav"), 20, t => t is >= 8 and < 11 ? 0 : 0.12, 220);
            var s2 = TestWav(Path.Combine(root, "ersatz-mikrofon-2.wav"), 20, t => t is >= 3 and < 6 ? 0.08 : 0, 330);
            var takes = new Queue<string>([s1, s2]);
            w.PodcMicOverride = () => takes.Dequeue(); w.PodcFfmpegPath = ff;
            w.Show(PK); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(PK)!;
            d.Set("title", "Testfolge 1", "Titel"); d.Set("author", "Testsender", "Autor"); d.Set("show", "Trendo Testpodcast", "Podcast"); d.Set("episode", "1", "Nr.");
            d.Set("chapters", "00:00 Begrüßung\n00:15 Teil zwei", "Kapitel");
            Assert.NotNull(w.Find<Button>("prog-first-step"));
            for (var k = 0; k < 2; k++)
            {
                w.Show(PK); Settle(); RealClick(w, w.Find<Button>("prog-cmd-podc-record")); Assert.True(w.PodcRecording);
                RealClick(w, w.Find<Button>("prog-cmd-podc-stop"));
                await Until(() => PodcastEdit.Tracks(d.Data).Count == k + 1);
            }
            Assert.Equal(["Sprecher 1", "Sprecher 2"], PodcastEdit.Tracks(d.Data).Select(t => t.Name).ToArray());
            w.Show(PK); Settle();

            // Versprecher 2–3 s an der Welle markieren und mit Entf löschen
            var wave = w.Find<WaveView>("podc-wave-0"); Settle();
            Point At(double sec) => wave.TranslatePoint(new Point(wave.ToX(sec), wave.Bounds.Height / 2), w)!.Value;
            Drag(w, At(2), At(3));
            Assert.InRange(w.PodcSel.A, 1.9, 2.1); Assert.InRange(w.PodcSel.B, 2.9, 3.1);
            Key1(w, Key.Delete, PhysicalKey.Delete);
            Assert.Single(PodcastEdit.Cuts(d.Data));
            Assert.Contains("00:14 Teil zwei", d.Text("chapters")); // Kapitel rückt mit

            Tap(w, "prog-cmd-podc-pauses");
            Assert.Equal(2, PodcastEdit.Cuts(d.Data).Count);
            var expected = PodcastEdit.Render(d.Data).Seconds;
            Assert.InRange(expected, 16.2, 16.8); // 20 − 1 − 2,5
            var ch2 = PodcastKit.ParseChapters(d.Text("chapters")).Chapters[1].Start.TotalSeconds; Assert.InRange(ch2, 11, 12);

            Tap(w, "prog-cmd-podc-loud");
            await Until(() => w.PodcLastOutput is not null || w.ProgramMessage(PK).StartsWith("FFmpeg meldet") || w.ProgramMessage(PK).StartsWith("Fertig machen ging"), 60000);
            var mp3 = w.PodcLastOutput ?? throw new Xunit.Sdk.XunitException(w.ProgramMessage(PK));
            var copy = Path.Combine(dir, "podcast-fertig.mp3"); if (!string.Equals(copy, mp3, StringComparison.OrdinalIgnoreCase)) File.Copy(mp3, copy, true);

            var pr = Cli.Run(probe, ["-v", "error", "-show_format", "-show_chapters", "-of", "json", mp3], null, 30000);
            Assert.True(pr.Ok, pr.Err);
            var j = JsonNode.Parse(pr.Out)!;
            var dur = double.Parse((string)j["format"]!["duration"]!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(dur, expected - 0.3, expected + 0.3);
            var chapters = j["chapters"]!.AsArray();
            Assert.Equal(2, chapters.Count);
            Assert.Equal("Teil zwei", (string?)chapters[1]!["tags"]!["title"]);
            var tags = j["format"]!["tags"]!;
            Assert.Equal("Testfolge 1", (string?)tags["title"]); Assert.Equal("Testsender", (string?)tags["artist"]); Assert.Equal("Trendo Testpodcast", (string?)tags["album"]); Assert.Equal("Podcast", (string?)tags["genre"]);

            var lr = Cli.Run(ff, ["-hide_banner", "-nostats", "-i", mp3, "-af", "ebur128", "-f", "null", "-"], null, 60000);
            var summary = lr.Err[lr.Err.LastIndexOf("Summary:", StringComparison.Ordinal)..];
            var m = System.Text.RegularExpressions.Regex.Match(summary, @"I:\s+(-?[0-9.]+) LUFS");
            Assert.True(m.Success, summary);
            var lufs = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(lufs, -17, -15);
            if (Beleg is { } b)
            {
                // T-0730: vor dem Serialisieren anonymisieren (sonst trifft Replace die JSON-maskierten Backslashes nicht)
                File.WriteAllText(Path.Combine(b, "podcast-ffprobe.json"), AnonBeleg(pr.Out, root));
                File.WriteAllText(Path.Combine(b, "podcast-ebur128.txt"), AnonBeleg(summary, root));
                File.WriteAllText(Path.Combine(b, "podcast-schnitt.json"), BelegJson(d.Data, root));
            }
            // RSS-Feed nur als Datei, verweist auf die fertige MP3
            d.Set("baseUrl", "https://example.org/pod", "Adresse");
            var rss = System.Xml.Linq.XDocument.Load(Export(w, PK, "rss", Path.Combine(dir, "podcast-feed.xml")));
            Assert.EndsWith(Uri.EscapeDataString(Path.GetFileName(mp3)), (string?)rss.Descendants("enclosure").Single().Attribute("url"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0697 Podcast: ohne Mikrofon-Ersatz im Testmodus wird nichts vorgetäuscht – ehrlicher Hinweis, keine Spur")]
    public void Podcast_ohne_Mikrofon_ehrlich()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(PK); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            RealClick(w, w.Find<Button>("prog-cmd-podc-record")); Settle();
            Assert.False(w.PodcRecording); Assert.Contains("nichts aufgenommen", w.ProgramMessage(PK));
            Assert.Empty(PodcastEdit.Tracks(w.ProgramActive(PK)!.Data));
            w.PodcFfmpegPath = ""; RealClick(w, w.Find<Button>("prog-cmd-podc-stop"));
            Assert.Contains("keine Aufnahme", w.ProgramMessage(PK));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0704 Comic in „Einfach“: Pinsel, Farbe, Strichstärke und Ebenen über die Zeichenleiste über der Seite – ohne die Eigenschaften aufzuklappen; der Strich hat die gewählte Farbe")]
    public void Comic_Einfach_Zeichenleiste()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root); w.Width = 1800; w.Height = 1400;
        try
        {
            w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Einfach);
            w.Show(CK); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(CK)!;
            Assert.Null(w.TryFind<Border>("prog-properties")); // Eigenschaften in „Einfach“ eingeklappt
            Assert.NotNull(w.Find<WrapPanel>("comic-drawbar"));
            Button InFly(string bar, string name) => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((Control)((Flyout)w.Find<Button>(bar).Flyout!).Content!).OfType<Button>().Single(b => b.Name == name);
            InFly("comic-bar-brush", "comic-brush-bleistift").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
            Assert.Equal("bleistift", w.ComicBrush); Assert.Equal("pinsel", w.ComicTool);
            Assert.Contains("Bleistift", string.Join(" ", w.Find<Button>("comic-bar-brush").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
            InFly("comic-bar-color", "comic-color-C0392B").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
            Assert.Equal("#C0392B", w.ComicColor);
            w.Find<Slider>("comic-brush-size").Value = 14; Settle(); Assert.Equal(14, w.ComicSize);
            Assert.NotNull(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((Control)((Flyout)w.Find<Button>("comic-bar-layers").Flyout!).Content!).OfType<CheckBox>().FirstOrDefault(c => c.Name!.StartsWith("comic-layer-vis-", StringComparison.Ordinal)));
            // zeichnen mit der Wahl aus der Leiste
            var v = w.Find<ComicPageView>("comic-view"); Settle();
            var f0 = MainWindow.ComicOf(d).Pages[0].Panels[0].Bounds();
            Drag(w, Screen(w, v, f0.X + 100, f0.Y + 200), Screen(w, v, f0.X + 300, f0.Y + 200));
            var px = MainWindow.ComicOf(d).Pages[0].Paint().Composite(false).At((int)(f0.X + 200), (int)(f0.Y + 200));
            Assert.True(px.A > 100 && px.R > 120 && px.G < 120, $"Strich in Rot erwartet: {px}");
            // CBZ gehört in „Einfach“ dazu (im Menü „Mehr“)
            Assert.NotNull(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((Control)((Flyout)w.Find<Button>("prog-more").Flyout!).Content!).OfType<Button>().FirstOrDefault(b => b.Name == "prog-export-cbz"));
        }
        finally { w.Close(); }
    }
}
