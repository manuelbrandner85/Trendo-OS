using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SkiaSharp;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Media;
using Xunit;
using Path = System.IO.Path;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0699: „Bilder erkennen“ erkennt wirklich, „Videos mit KI“ erzeugt Bewegtbild mit Ton. Je Programm ein Ablauf mit nachgebildeten Werkzeugen
/// (schnell, in jeder Testumgebung) und ein Echtlauf mit den echten Werkzeugen dieses PCs (YOLOX-S, Tesseract, Bild-KI, Piper, FFmpeg, optional Wan 2.2) –
/// ersetzt ist dort nur die Werkzeugsuche (der Testmodus sucht keine Werkzeuge), nie das Werkzeug.</summary>
public class VisionVideoT0699_Ui
{
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.Press(); Settle(); }
    static void WaitFor(Func<bool> done, int max = 600) { for (var i = 0; i < max && !done(); i++) { Thread.Sleep(15); Settle(); } }
    static List<string> Texts(Control c) => c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Length > 0).ToList();
    static int Gold(Control c) => c.GetLogicalDescendants().OfType<Button>().Count(b => MainWindow.IsGold(b) && b.IsVisible);

    static string Export(MainWindow w, string key, string name, string root)
    {
        var e = MainWindow.Programs[key].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(key)!, Path.Combine(root, "export-" + key + "-" + name + e.Extension));
    }

    static void Png(string file, Action<SKCanvas> paint, int w = 320, int h = 200)
    {
        using var bmp = new SKBitmap(w, h);
        using (var c = new SKCanvas(bmp)) { c.Clear(SKColors.White); paint(c); }
        using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(file, data.ToArray());
    }

    static void Wav(string file, double seconds)
    {
        int rate = 22050, n = (int)(rate * seconds);
        using var bw = new BinaryWriter(File.Create(file));
        bw.Write("RIFF"u8); bw.Write(36 + n * 2); bw.Write("WAVEfmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write("data"u8); bw.Write(n * 2);
        for (var i = 0; i < n; i++) bw.Write((short)(Math.Sin(i * 2 * Math.PI * 220 / rate) * 3000));
    }

    const string K = "bilder-erkennen-programm", V = "video-ki-programm";

    [AvaloniaFact(DisplayName = "T-0699 Bilder erkennen: Leerzustand mit erstem Schritt, Ordner erkennen (Objekte mit Rahmen, Beschreibung, Schlagworte), Suche „zeig alle Fotos mit Hund“, Seitendatei, CSV, nach Neustart noch da")]
    public void Vision_recognize()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var dir = Path.Combine(root, "fotos"); Directory.CreateDirectory(dir);
        Png(Path.Combine(dir, "hund.png"), c => c.DrawCircle(160, 100, 60, new SKPaint { Color = SKColors.SaddleBrown }));
        Png(Path.Combine(dir, "katze.png"), c => c.DrawRect(40, 40, 200, 100, new SKPaint { Color = SKColors.Gray }));
        File.Copy(Path.Combine(dir, "hund.png"), Path.Combine(dir, "hund-kopie.png"));
        var before = File.ReadAllBytes(Path.Combine(dir, "hund.png"));
        MainWindow.FolderPickOverride = _ => dir;
        MainWindow.VisionTesseractOverride = () => null;
        var runs = new List<IReadOnlyList<string>>();
        MainWindow.ObjectsRunOverride = a =>
        {
            runs.Add(a);
            var dog = a[1].Contains("hund", StringComparison.OrdinalIgnoreCase);
            return new Cli.Result(0, dog ? """{"breite":320,"hoehe":200,"ms":5,"objekte":[{"klasse":"dog","sicherheit":0.93,"rahmen":[100,40,220,160]}]}""" : """{"breite":320,"hoehe":200,"ms":5,"objekte":[{"klasse":"cat","sicherheit":0.81,"rahmen":[40,40,240,140]}]}""", "");
        };
        MainWindow.VisionDescribeOverride = f => Task.FromResult<(string, string)?>(f.Contains("hund") ? ("Ein brauner Hund auf weißem Grund.\nSchlagworte: Hund, Haustier, braun", "Test-Modell") : ("Eine graue Katze.\nSchlagworte: Katze, Tier", "Test-Modell"));
        try
        {
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var ws = w.Find<StackPanel>("vi-workspace");
            Assert.Equal("Ordner erkennen …", w.Find<Button>("prog-first-step").Content); // erster Schritt im Leerzustand, golden
            Assert.True(MainWindow.IsGold(w.Find<Button>("prog-first-step"))); Assert.Equal(0, Gold(ws)); Assert.False(MainWindow.IsGold(w.Find<Button>("prog-cmd-vi-recognize-folder")));
            Assert.Contains(Texts(w.Find<Border>("vi-empty")), t => t.StartsWith("Erster Schritt: „Ordner erkennen …“"));
            Assert.Contains(Texts(w.Find<StackPanel>("vi-tools")), t => t.Contains("Bildbeschreibung und Schlagworte") || t.Contains("Bildbeschreibung:"));

            Tap(w, "prog-first-step"); WaitFor(() => w.ProgramMessage(K).StartsWith("✓"));
            Assert.Equal("✓ 3 Bilder erkannt. Suche oben, z. B. „zeig alle Fotos mit Hund“.", w.ProgramMessage(K));
            Assert.Equal(3, runs.Count); Assert.Equal("-", runs[0][2]); // Fotos werden nicht beschriftet: keine markierte Ausgabe
            var d = w.ProgramActive(K)!;
            var res = w.VisionResults(d);
            Assert.Equal(["Hund", "Haustier", "Braun"], res.Single(r => r.File.EndsWith("hund.png")).Tags);
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(dir, "hund.png"))); // Foto unverändert
            Assert.Contains("<rdf:li>Hund</rdf:li>", File.ReadAllText(Path.Combine(dir, "hund.png.xmp")));
            Assert.Equal(3, w.Find<WrapPanel>("vi-gallery").Children.Count);
            Assert.Contains(Texts(w.Find<Border>("vi-gallery-card")), t => t.StartsWith("Doppelte Fotos: 1 Gruppe"));

            var search = w.Find<TextBox>("vi-search"); search.Text = "zeig alle Fotos mit Hund";
            search.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
            Assert.Equal("zeig alle Fotos mit Hund", d.Text("query"));
            Assert.Equal(2, w.Find<WrapPanel>("vi-gallery").Children.Count);
            Assert.Contains(Texts(w.Find<Border>("vi-gallery-card")), t => t == "2 von 3 Bildern passen zu „zeig alle Fotos mit Hund“");

            // Treffer öffnen: Bild mit goldenen Rahmen und Erkennung daneben
            Tap(w, "vi-hit-0"); WaitFor(() => w.ProgramMessage(K).StartsWith("hund"));
            Assert.Single(w.Find<Canvas>("vi-boxes").Children.OfType<Rectangle>());
            Assert.Contains("Hund", Texts(w.Find<WrapPanel>("vi-tags")));
            Assert.Equal("Ein brauner Hund auf weißem Grund.", w.Find<TextBlock>("vi-desc").Text);
            Assert.True(MainWindow.IsGold(w.Find<Button>("prog-cmd-vi-recognize-folder"))); Assert.Equal(0, Gold(w.Find<StackPanel>("vi-workspace"))); // genau ein goldener Knopf

            var csv = File.ReadAllLines(Export(w, K, "csv", root));
            Assert.Equal(3, csv.Length); // Kopf + zwei Treffer der Suche
            Assert.StartsWith("Datei;Ordner;Schlagworte", csv[0]); Assert.Contains("Hund, Haustier, Braun", csv[1]);

            // gespeichert und neu geöffnet: Erkennung bleibt, ohne neu zu rechnen
            var tdoc = w.ProgramSave(K, Path.Combine(root, "fotos.tdoc"));
            var again = Trendo.Core.Programs.ProgramDocument.Load(tdoc, "STUDIO-VISION-01");
            Assert.Equal(3, w.VisionResults(again).Count);
            runs.Clear();
            var t = w.VisionRecognizeFolder(d, dir); WaitFor(() => t.IsCompleted);
            Assert.Empty(runs); Assert.Contains("3 unverändert übernommen", w.ProgramMessage(K));
        }
        finally { MainWindow.FolderPickOverride = null; MainWindow.VisionTesseractOverride = null; MainWindow.ObjectsRunOverride = null; MainWindow.VisionDescribeOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0699 Bilder erkennen: ohne jedes Werkzeug ehrlicher Hinweis mit Einrichten-Weg, nichts erfunden")]
    public void Vision_nothing_ready()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var img = Path.Combine(root, "bild.png"); Png(img, c => c.DrawCircle(50, 50, 30, new SKPaint { Color = SKColors.Red }));
        w.PickOverride = _ => Task.FromResult<string[]?>([img]);
        MainWindow.VisionTesseractOverride = () => null;
        try
        {
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            RealClick(w, w.Find<Button>("prog-cmd-vi-open")); WaitFor(() => w.ProgramMessage(K).StartsWith("bild.png"));
            Assert.True(MainWindow.IsGold(w.Find<Button>("prog-cmd-vi-recognize-folder"))); Assert.Equal(0, Gold(w.Find<StackPanel>("vi-workspace")));
            Assert.NotNull(w.Find<Button>("vi-objects-setup")); Assert.NotNull(w.Find<Button>("vi-describe-setup")); Assert.NotNull(w.Find<Button>("vi-tess-setup"));
            Tap(w, "vi-recognize-now"); WaitFor(() => w.ProgramMessage(K).StartsWith("⚠"));
            Assert.StartsWith("⚠ Für die Erkennung ist noch nichts eingerichtet", w.ProgramMessage(K));
            Assert.Empty(w.VisionResults(w.ProgramActive(K)!));
            Assert.False(File.Exists(img + ".xmp"));
        }
        finally { w.PickOverride = null; MainWindow.VisionTesseractOverride = null; w.Close(); }
    }

    // ---------- Echtlauf Bilder erkennen ----------

    static string YoloxDir => Path.Combine(global::Trendo.Core.TempDir.SystemTemp, "trendo-tools-b", "yolox");
    static string YoloxPy => Path.Combine(YoloxDir, "venv", "Scripts", "python.exe");
    static string Repo() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent; return d!.FullName; }
    static string? Tesseract() => Cli.Find("tesseract") ?? new DependencyManager(new AuditLog(Path.Combine(Path.GetTempPath(), "trendo-t0699-needs.jsonl")), new RealProcessRunner()).ResolvePath(DependencyCatalog.Get("tesseract"));

    static string? VisionRealMissing() =>
        !File.Exists(YoloxPy) || !File.Exists(Path.Combine(YoloxDir, ObjectDetect.ModelFile)) || !File.Exists(Path.Combine(YoloxDir, "dog.jpg")) ? "Übersprungen – YOLOX-Prüfumgebung fehlt (%TEMP%/trendo-tools-b/yolox)."
        : Tesseract() is null ? "Übersprungen – Tesseract fehlt auf diesem PC." : null;

    [AvaloniaFact(DisplayName = "T-0699 Echtlauf Bilder erkennen: echtes YOLOX-S erkennt Hund und Fahrrad, echtes Tesseract liest gedruckten Text; Suche findet beide Fotos, Seitendatei geschrieben")]
    public void Vision_real_tools()
    {
        if (VisionRealMissing() is { } why) { Assert.Skip(why); return; }
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var dir = Path.Combine(root, "echt"); Directory.CreateDirectory(dir);
        File.Copy(Path.Combine(YoloxDir, "dog.jpg"), Path.Combine(dir, "hund-und-rad.jpg"));
        var letter = Path.Combine(dir, "brief.png");
        Png(letter, c =>
        {
            using var f = new SKFont(SKTypeface.FromFamilyName("Arial"), 46); using var p = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            c.DrawText("Rechnung Nummer 4711", 40, 110, f, p); c.DrawText("Betrag: 128 Euro", 40, 190, f, p);
        }, 900, 260);
        var log = new List<string>();
        MainWindow.FolderPickOverride = _ => dir;
        MainWindow.VisionTesseractOverride = Tesseract; // echtes Tesseract – nur die Suche nach dem Programm ist hier fest
        MainWindow.ObjectsRunOverride = a =>
        {
            // echtes YOLOX-S mit onnxruntime: gleiches Startskript wie in Trendo, Modell aus der Prüfumgebung (Prüfsumme wie im Produkt)
            var args = a.ToList(); args[0] = Path.Combine(YoloxDir, ObjectDetect.ModelFile);
            var r = Cli.Run(YoloxPy, ["-I", Path.Combine(Repo(), "src", "Trendo.App", "Assets", "ki", "trendo_objekte.py"), "-", .. args], timeoutMs: 180_000);
            log.Add($"YOLOX {Path.GetFileName(args[1])}: Code {r.Exit} {r.Out.Trim().Split('\n')[^1]}");
            return r;
        };
        try
        {
            Assert.Equal(ObjectDetect.ModelSha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(YoloxDir, ObjectDetect.ModelFile)))));
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            Tap(w, "vi-recognize-folder-big"); WaitFor(() => w.ProgramMessage(K).StartsWith("✓") || w.ProgramMessage(K).StartsWith("⚠"), 6000);
            log.Add("Meldung: " + w.ProgramMessage(K));
            Assert.Equal("✓ 2 Bilder erkannt. Suche oben, z. B. „zeig alle Fotos mit Hund“.", w.ProgramMessage(K));
            var d = w.ProgramActive(K)!; var res = w.VisionResults(d);
            var dog = res.Single(r => r.File.EndsWith("hund-und-rad.jpg")); var bill = res.Single(r => r.File.EndsWith("brief.png"));
            foreach (var r in res) log.Add($"{Path.GetFileName(r.File)}: Schlagworte [{string.Join(", ", r.Tags)}] · Objekte [{string.Join(", ", r.Objects.Select(o => $"{o.Label} {o.Score:0.00} ({o.X1},{o.Y1},{o.X2},{o.Y2})"))}] · Text „{r.Text.Replace('\n', ' ')}“ · Quellen {string.Join(", ", r.Sources)}");
            Assert.Contains(dog.Objects, o => o.Label == "Hund" && o.Score > 0.85); Assert.Contains(dog.Objects, o => o.Label == "Fahrrad" && o.Score > 0.9);
            Assert.Contains("Rechnung", bill.Text); Assert.Contains("4711", bill.Text);
            Assert.Equal([dog], VisionTags.Search(res, "zeig alle Fotos mit Hund"));
            Assert.Equal([bill], VisionTags.Search(res, "Fotos mit Rechnung"));
            Assert.Contains("<rdf:li>Hund</rdf:li>", File.ReadAllText(dog.File + ".xmp"));
            w.Show(K); Settle(); Tap(w, "vi-hit-1"); WaitFor(() => w.ProgramMessage(K).StartsWith("hund"));
            Assert.True(w.Find<Canvas>("vi-boxes").Children.OfType<Rectangle>().Count() >= 3); // Fahrrad, Hund, Fahrzeug
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG") is { Length: > 0 } beleg)
            {
                Directory.CreateDirectory(beleg);
                File.WriteAllLines(Path.Combine(beleg, "bilder-erkennen-echt.log"), log);
                File.Copy(dog.File + ".xmp", Path.Combine(beleg, "hund-und-rad.jpg.xmp"), true);
                File.Copy(Export(w, K, "csv", root), Path.Combine(beleg, "bilder-erkennen-liste.csv"), true);
                File.Copy(letter, Path.Combine(beleg, "brief-fuer-tesseract.png"), true);
                w.Width = 1600; w.Height = 1000; Settle(); w.CaptureRenderedFrame()?.Save(Path.Combine(beleg, "bilder-erkennen-echt-bildschirm.png"));
            }
        }
        finally { MainWindow.FolderPickOverride = null; MainWindow.VisionTesseractOverride = null; MainWindow.ObjectsRunOverride = null; w.Close(); }
    }

    // ---------- Videos mit KI ----------

    [AvaloniaFact(DisplayName = "T-0699 Videos mit KI: eine goldene Hauptaktion, Sprecher je Szene, Musik, Untertitelspur, Szene mit Video-KI bewegt → Clip statt Kamerafahrt; ohne Stimme ehrlicher Hinweis")]
    public void Video_sound_and_clip()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var png = Path.Combine(root, "p.png"); Png(png, c => c.DrawCircle(160, 100, 50, new SKPaint { Color = SKColors.Gold }));
        var models = Path.Combine(root, "modell"); Directory.CreateDirectory(models);
        foreach (var f in new[] { "wan.gguf", "t5.gguf", "vae.safetensors" }) File.WriteAllBytes(Path.Combine(models, f), [1]);
        var tts = new List<string>(); List<string>? args = null; var genCalls = new List<IReadOnlyList<string>>();
        MainWindow.ImageAiOverride = (p, seed, file, wi, he) => { File.Copy(png, file, true); return Task.FromResult(true); };
        MainWindow.VideoAiFfmpegOverride = (a, outFile) => { if (a.Contains("-filter_complex")) args = [.. a]; File.WriteAllBytes(outFile, [0, 0, 0, 1]); return true; };
        w.EditGenModelOverride = new GenVideo.Model("wan2.2-szenen", "Wan 2.2", "Apache-2.0", Path.Combine(models, "wan.gguf"), Path.Combine(models, "t5.gguf"), Path.Combine(models, "vae.safetensors"));
        w.VideoKiOverride = (exe, a) => { genCalls.Add(a); File.WriteAllBytes(a[a.ToList().IndexOf("-o") + 1], [1, 2, 3]); return Task.FromResult(true); };
        try
        {
            w.Show(V); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel-ton")); Settle();
            var d = w.ProgramActive(V)!;
            Assert.True(MainWindow.IsGold(w.Find<Button>("prog-cmd-video-plan"))); Assert.Equal(0, Gold(w.Find<StackPanel>("video-workspace")));
            RealClick(w, w.Find<Button>("prog-cmd-video-plan")); Settle();
            Assert.Equal(3, d.List("scenes").Count);
            Assert.Equal("Am Meer beginnt der Tag ganz leise.", w.Find<TextBox>("video-s0-speech").Text);
            Assert.Equal(0, Gold(w.Find<StackPanel>("video-workspace"))); Assert.Equal("Szenen neu planen", w.Find<Button>("video-go-plan").Content);
            var t1 = w.VideoImages(d); WaitFor(() => t1.IsCompleted); Assert.Equal(3, t1.Result);
            w.Show(V); Settle();
            Assert.Equal("Szene 1 · Bild mit Kamerafahrt (KI-Bild)", w.Find<TextBlock>("video-s0-kind").Text);
            Assert.Equal(3, w.Find<StackPanel>("video-s0-motion-preview").Children.OfType<Border>().Count());

            // ohne Stimme: Video entsteht, Untertitel ja, Sprecher nein – und das steht in der Meldung
            var t2 = w.VideoBuild(d, Path.Combine(root, "ohne-stimme.mp4")); WaitFor(() => t2.IsCompleted);
            Assert.NotNull(t2.Result); Assert.Contains("Ohne Sprecher gebaut", w.ProgramMessage(V));
            Assert.Contains("mov_text", args!); Assert.DoesNotContain("[aout]", args!);
            Assert.True(File.Exists(Path.Combine(root, "ohne-stimme.srt")));

            // Szene 1 mit der Video-KI bewegen: echter Aufruf von sd-cli (vid_gen) mit Startbild in Clipgröße, Clip ersetzt die Kamerafahrt
            w.Show(V); Settle(); Tap(w, "video-s0-move"); WaitFor(() => w.ProgramMessage(V).StartsWith("✓") || w.ProgramMessage(V).Contains("nicht"));
            Assert.StartsWith("✓ Szene 1 ist jetzt ein KI-Video", w.ProgramMessage(V));
            var g = genCalls.Single(); Assert.Equal("vid_gen", g[1]); Assert.Contains("-i", g);
            Assert.Equal("Szene 1 · KI-Video (bewegt)", w.Find<TextBlock>("video-s0-kind").Text);
            var clip = d.List("scenes")[0]!["clip"]!.ToString(); Assert.True(File.Exists(clip)); Assert.True(File.Exists(GenVideo.ProvenancePath(clip)));

            // mit Stimme und Musik
            MainWindow.VideoAiTtsOverride = (text, wav) => { tts.Add(text); Wav(wav, 1.5); return true; };
            var song = Path.Combine(root, "lied.wav"); Wav(song, 3);
            w.PickOverride = _ => Task.FromResult<string[]?>([song]);
            w.Show(V); Settle(); Tap(w, "video-go-music"); WaitFor(() => d.Text("music").Length > 0);
            Assert.NotEqual(song, d.Text("music")); Assert.True(File.Exists(d.Text("music")));
            var t3 = w.VideoBuild(d, Path.Combine(root, "mit-ton.mp4")); WaitFor(() => t3.IsCompleted);
            Assert.NotNull(t3.Result); Assert.DoesNotContain("Ohne Sprecher", w.ProgramMessage(V));
            Assert.Equal(3, tts.Count);
            var inputs = args!.Select((x, i) => (x, i)).Where(p => p.x == "-i").Select(p => args![p.i + 1]).ToList();
            Assert.Equal(clip, inputs[0]); // Szene 1 = KI-Clip
            Assert.Equal(3, inputs.Count(i => i.EndsWith(".wav") && Path.GetFileName(i).StartsWith("sprecher-")));
            Assert.Contains(inputs, i => i == d.Text("music")); Assert.EndsWith("untertitel.srt", inputs[^1]);
            Assert.Contains("[aout]", args!); Assert.Contains("aac", args!);
            var prov = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mit-ton.mp4.json"))).RootElement;
            Assert.Equal("ki-video", prov.GetProperty("Scenes")[0].GetProperty("Source").GetString());
            Assert.Contains("Sprecher Piper", prov.GetProperty("Generator").GetString());
            // gleicher Text: kein zweites Einsprechen
            var t4 = w.VideoBuild(d, Path.Combine(root, "nochmal.mp4")); WaitFor(() => t4.IsCompleted);
            Assert.Equal(3, tts.Count);
        }
        finally { MainWindow.ImageAiOverride = null; MainWindow.VideoAiFfmpegOverride = null; MainWindow.VideoAiTtsOverride = null; w.PickOverride = null; w.Close(); }
    }

    // ---------- Echtlauf Videos mit KI ----------

    static string Tools => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge", "ki");
    static string SdCli => Path.Combine(Tools, "laufzeit", "stablediffusioncpp", "sd-cli.exe");
    static string Sdxl => Path.Combine(Tools, "modelle", "sdxl-turbo", "sd_xl_turbo_1.0_fp16.safetensors");
    static string Piper => Path.Combine(Tools, "laufzeit", "piper", "piper", "piper.exe");
    static string Voice => Path.Combine(Tools, "modelle", "piper-thorsten", "de_DE-thorsten-high.onnx");
    static string WanDir => Path.Combine(Tools, "modelle", "wan2.2-szenen");

    /// <summary>Braucht Rechenzeit auf der Grafikkarte: nur mit TRENDO_ECHT_VIDEO=1 (Wan 2.2 zusätzlich mit TRENDO_ECHT_WAN=1).</summary>
    static string? VideoRealMissing() =>
        Environment.GetEnvironmentVariable("TRENDO_ECHT_VIDEO") != "1" ? "Übersprungen – Echtlauf mit Bild-KI nur mit TRENDO_ECHT_VIDEO=1."
        : !File.Exists(SdCli) || !File.Exists(Sdxl) ? "Übersprungen – Bild-KI (sd-cli, SDXL Turbo) fehlt."
        : !File.Exists(Piper) || !File.Exists(Voice) ? "Übersprungen – Stimme (Piper, Thorsten) fehlt."
        : Cli.Find("ffmpeg") is null || Cli.Find("ffprobe") is null ? "Übersprungen – FFmpeg/ffprobe fehlt." : null;

    static Cli.Result WithInput(string exe, IReadOnlyList<string> args, string stdin)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(exe)!, StandardInputEncoding = new System.Text.UTF8Encoding(false) };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
        p.StandardInput.Write(stdin); p.StandardInput.Close();
        p.WaitForExit(120_000);
        return new(p.ExitCode, o.Result, e.Result);
    }

    static double MeanDiff(string a, string b)
    {
        using var x = SKBitmap.Decode(a); using var y = SKBitmap.Decode(b);
        double sum = 0; var n = 0;
        for (var j = 0; j < x.Height; j += 4) for (var i = 0; i < x.Width; i += 4) { var p = x.GetPixel(i, j); var q = y.GetPixel(i, j); sum += Math.Abs(p.Red - q.Red) + Math.Abs(p.Green - q.Green) + Math.Abs(p.Blue - q.Blue); n += 3; }
        return sum / n;
    }

    [AvaloniaFact(DisplayName = "T-0699 Echtlauf Videos mit KI: echte Bild-KI malt 2 Schlüsselbilder, echte Stimme spricht, FFmpeg rendert MP4 mit Ton und Untertitel; ffprobe prüft, Standbilder einer Szene unterscheiden sich")]
    public void Video_real_tools()
    {
        if (VideoRealMissing() is { } why) { Assert.Skip(why); return; }
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var ff = Cli.Find("ffmpeg")!; var probe = Cli.Find("ffprobe")!;
        var log = new List<string>(); var wan = Environment.GetEnvironmentVariable("TRENDO_ECHT_WAN") == "1" && Directory.Exists(WanDir);
        MainWindow.ImageAiOverride = (prompt, seed, png, wi, he) =>
        {
            // echte Bild-KI (SDXL Turbo); kleine Auflösung, damit der Test kurz bleibt
            var r = Cli.Run(SdCli, ["-m", Sdxl, "-p", prompt, "-n", ImageAiKit.Negative, "-o", png, "--steps", "4", "--cfg-scale", "1.0", "-W", "768", "-H", "448", "--seed", seed.ToString(), "--vae-tiling"], timeoutMs: 600_000);
            log.Add($"sd-cli Seed {seed}: Code {r.Exit}, {(File.Exists(png) ? new FileInfo(png).Length : 0)} Bytes");
            return Task.FromResult(r.Ok && File.Exists(png));
        };
        MainWindow.VideoAiTtsOverride = (text, wav) => { var r = WithInput(Piper, ["--model", Voice, "--output_file", wav], text); log.Add($"Piper „{text}“: Code {r.Exit}"); return r.Ok && File.Exists(wav); };
        MainWindow.VideoAiFfmpegOverride = (a, outFile) => { var r = Cli.Run(ff, a, timeoutMs: 900_000); log.Add($"ffmpeg → {Path.GetFileName(outFile)}: Code {r.Exit} {r.Err.Trim().Split('\n').LastOrDefault()}"); return r.Ok; };
        if (wan)
        {
            w.EditGenModelOverride = new GenVideo.Model("wan2.2-szenen", "Wan 2.2 – Filmszenen (5B)", "Apache-2.0", Path.Combine(WanDir, "Wan2.2-TI2V-5B-Q6_K.gguf"), Path.Combine(WanDir, "umt5-xxl-encoder-Q8_0.gguf"), Path.Combine(WanDir, "wan2.2_vae.safetensors"));
            w.VideoKiOverride = (exe, a) => { var sw = System.Diagnostics.Stopwatch.StartNew(); var r = Cli.Run(SdCli, a, timeoutMs: 3_600_000); log.Add($"Wan 2.2 (sd-cli vid_gen): Code {r.Exit} in {sw.Elapsed.TotalSeconds:0} s"); if (!r.Ok) log.Add("sd-cli: " + string.Join(" | ", (r.Err + r.Out).Trim().Split((char)10).TakeLast(8))); return Task.FromResult(r.Ok); };
        }
        var song = Path.Combine(root, "musik.wav");
        Assert.True(Cli.Run(ff, ["-hide_banner", "-y", "-f", "lavfi", "-i", "sine=frequency=330:duration=8", "-ac", "2", song]).Ok); // Platzhalter für „eigene Musik“
        w.PickOverride = _ => Task.FromResult<string[]?>([song]);
        try
        {
            w.Show(V); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(V)!;
            d.Set("idea", "Ein alter Leuchtturm auf einer Klippe im Morgenlicht, Zoom rein, 4 s, Sprecher: Am Meer beginnt der Tag.\nSonnenstrahlen auf ruhigen Wellen, Schwenk nach rechts, 4 s, Text: Guten Morgen, Sprecher: Guten Morgen.", "Idee");
            w.Show(V); Settle(); RealClick(w, w.Find<Button>("prog-cmd-video-plan")); Settle();
            var t1 = w.VideoImages(d); WaitFor(() => t1.IsCompleted, 40000); Assert.Equal(2, t1.Result);
            if (wan) { w.Show(V); Settle(); var tw = w.VideoMoveScene(d, 0); WaitFor(() => tw.IsCompleted, 400000); log.Add("Video-KI: " + w.ProgramMessage(V)); Assert.NotNull(tw.Result); }
            else if (Environment.GetEnvironmentVariable("TRENDO_ECHT_WAN_CLIP") is { Length: > 0 } earlier && File.Exists(earlier))
            {
                // Wan rechnet über 30 Minuten: einen in einem früheren Echtlauf über „Mit Video-KI bewegen“ erzeugten Clip wiederverwenden – nur mit passendem Herkunftsnachweis
                Assert.Null(GenVideo.VerifyProvenance(earlier));
                var own = Path.Combine(Path.GetDirectoryName(d.List("scenes")[0]!["image"]!.ToString())!, Path.GetFileName(earlier));
                File.Copy(earlier, own); File.Copy(GenVideo.ProvenancePath(earlier), GenVideo.ProvenancePath(own));
                d.Edit("KI-Video", x => x["scenes"]![0]!["clip"] = own);
                log.Add($"Video-KI: Clip aus früherem Echtlauf übernommen ({Path.GetFileName(earlier)}, Herkunftsnachweis passt, SHA-256 {GenVideo.Sha256(own)})");
            }
            w.Show(V); Settle(); Tap(w, "video-go-music"); WaitFor(() => d.Text("music").Length > 0);
            var mp4 = Path.Combine(root, "echt.mp4");
            var t2 = w.VideoBuild(d, mp4); WaitFor(() => t2.IsCompleted, 40000);
            log.Add("Meldung: " + w.ProgramMessage(V));
            Assert.Equal(mp4, t2.Result);
            var pr = Cli.Run(probe, ["-v", "error", "-show_entries", "format=duration:stream=codec_type,codec_name,width,height", "-of", "json", mp4]);
            log.Add("ffprobe: " + pr.Out.Replace("\r", "").Replace("\n", " "));
            var j = JsonDocument.Parse(pr.Out).RootElement;
            var dur = double.Parse(j.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(dur, 7.3, 7.8); // 4 + 4 s − 0,5 s Überblendung
            var streams = j.GetProperty("streams").EnumerateArray().Select(s => (s.GetProperty("codec_type").GetString(), s.GetProperty("codec_name").GetString())).ToList();
            Assert.Contains(("video", "h264"), streams); Assert.Contains(("audio", "aac"), streams); Assert.Contains(("subtitle", "mov_text"), streams);
            // Ton ist nicht still
            var vol = Cli.Run(ff, ["-hide_banner", "-i", mp4, "-map", "0:a", "-af", "volumedetect", "-f", "null", "-"]);
            var mean = System.Text.RegularExpressions.Regex.Match(vol.Err, @"mean_volume: (-?[\d.]+) dB"); log.Add("Lautstärke: " + mean.Value);
            Assert.True(mean.Success && double.Parse(mean.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) > -45);
            // Bewegung: zwei Standbilder derselben Szene unterscheiden sich (Szene 2 = Kamerafahrt; Szene 1 = KI-Clip, falls Wan lief)
            var frames = new List<(string Name, string File)>();
            foreach (var (name, t) in new[] { ("szene1-anfang", 0.3), ("szene1-ende", 3.2), ("szene2-anfang", 4.1), ("szene2-ende", 7.2) })
            {
                var f = Path.Combine(root, name + ".png");
                Assert.True(Cli.Run(ff, ["-hide_banner", "-y", "-ss", t.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", mp4, "-frames:v", "1", f]).Ok);
                frames.Add((name, f));
            }
            var d1 = MeanDiff(frames[0].File, frames[1].File); var d2 = MeanDiff(frames[2].File, frames[3].File);
            log.Add($"Bildunterschied Szene 1: {d1:0.0} · Szene 2: {d2:0.0} (0 = gleiches Bild)");
            Assert.True(d1 > 3, $"Szene 1 bewegt sich nicht ({d1:0.0})"); Assert.True(d2 > 3, $"Szene 2 bewegt sich nicht ({d2:0.0})");
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG") is { Length: > 0 } beleg)
            {
                Directory.CreateDirectory(beleg);
                foreach (var (name, f) in frames) File.Copy(f, Path.Combine(beleg, "video-" + name + ".png"), true);
                w.Width = 1600; w.Height = 1000; w.Show(V); Settle(); w.CaptureRenderedFrame()?.Save(Path.Combine(beleg, "videos-mit-ki-echt-bildschirm.png"));
                File.Copy(Path.ChangeExtension(mp4, ".srt"), Path.Combine(beleg, "video-untertitel.srt"), true);
                File.Copy(mp4 + ".json", Path.Combine(beleg, "video-herkunft.json"), true);
                log.Add("MP4 SHA-256: " + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(mp4))) + $" ({new FileInfo(mp4).Length} Bytes)");
                if (Environment.GetEnvironmentVariable("TRENDO_BELEG_MP4") == "1") File.Copy(mp4, Path.Combine(beleg, "videos-mit-ki-echt.mp4"), true);
            }
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG") is { Length: > 0 } b) { Directory.CreateDirectory(b); File.WriteAllLines(Path.Combine(b, "videos-mit-ki-echt.log"), log); }
            MainWindow.ImageAiOverride = null; MainWindow.VideoAiTtsOverride = null; MainWindow.VideoAiFfmpegOverride = null; w.PickOverride = null; w.Close();
        }
    }
}
