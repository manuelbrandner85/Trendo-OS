using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0738 „Echtläufe: Bilder mit KI und Trendo Musik“.
/// Ohne Umgebungsvariable: die ehrliche Meldung, wenn die Bild-KI fehlt (nennt Programm und Modell einzeln, Knopf führt zur KI-Bibliothek).
/// Mit <c>TRENDO_ECHT_KI=&lt;Belegordner&gt;</c>: echter Lauf vom Klick bis zur Datei mit den Werkzeugen dieses PCs (%LOCALAPPDATA%\TrendoWerkzeuge) –
/// nichts ist ersetzt (kein <c>ImageAiOverride</c>, kein <c>AceClientOverride</c>). Rohlog, Ergebnisdatei, ffprobe-Ausgabe und SHA256SUMS landen im Belegordner.
/// Fehlt dort ein Werkzeug, schreibt der Test BLOCKIERT mit der genauen Voraussetzung in den Beleg und schlägt nicht still durch.</summary>
public class T0738_KiEchtlaeufe_Ui
{
    const string Img = "bild-ki-programm", Ms = "musik-studio";
    const string SkipText = "Übersprungen – Echtlauf nur mit TRENDO_ECHT_KI=<Belegordner>.";
    static string? BelegDir => Environment.GetEnvironmentVariable("TRENDO_ECHT_KI") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    /// <summary>Pfade und Namen dieses PCs aus Belegen entfernen: Benutzerordner (mit „\“ oder „/“ und URL-kodiert wie
    /// <c>C%3A%5CUsers%5C…</c> aus Server-Zeilen), den bloßen Benutzernamen und den Rechnernamen.</summary>
    internal static string Anon(string s) =>
        Anon(s, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.MachineName);

    internal static string Anon(string s, string profile, string machine)
    {
        s = s.Replace("\r\n", "\n");
        var user = Path.GetFileName(profile.TrimEnd('\\', '/'));
        if (profile.Length > 0)
        {
            s = Repl(s, profile, @"C:\Users\<user>");
            s = Repl(s, profile.Replace('\\', '/'), "C:/Users/<user>");
            // URL-kodiert: „:“ → %3A, „\“ → %5C, „/“ → %2F (auch gemischt groß/klein, z. B. %5c)
            var enc = Uri.EscapeDataString(profile);
            s = Repl(s, enc, "C%3A%5CUsers%5C%3Cuser%3E");
            s = Repl(s, enc.Replace("%5C", "%2F", StringComparison.OrdinalIgnoreCase), "C%3A%2FUsers%2F%3Cuser%3E");
        }
        if (machine.Length > 0) s = Repl(s, machine, "<pc>"); // vor dem Benutzernamen: der Rechnername kann ihn enthalten
        if (user.Length > 0)
            s = System.Text.RegularExpressions.Regex.Replace(s, "(?:(?<![A-Za-z0-9])|(?<=%5C)|(?<=%2F))" + System.Text.RegularExpressions.Regex.Escape(user) + "(?![A-Za-z0-9])",
                "<user>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return s;
    }

    static string Repl(string s, string from, string to) => from.Length == 0 ? s : s.Replace(from, to, StringComparison.OrdinalIgnoreCase);

    /// <summary>Steuerfolgen (alle ANSI-CSI-Folgen, z. B. Farben „ESC[…m“ und Zeile löschen „ESC[K“) aus Werkzeugausgaben entfernen,
    /// damit der Rohlog lesbar bleibt; der Text selbst bleibt unverändert.</summary>
    internal static string NoAnsi(string s) => System.Text.RegularExpressions.Regex.Replace(s, "\u001b\\[[0-?]*[ -/]*[@-~]", "");

    static string Sha(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    /// <summary>Beleg mit einheitlichen Zeilenenden (LF, UTF-8 ohne BOM) schreiben und in SHA256SUMS eintragen.</summary>
    static void Beleg(string dir, string name, string text)
    {
        var f = Path.Combine(dir, name);
        File.WriteAllText(f, Anon(NoAnsi(text)), new UTF8Encoding(false));
        Sum(dir, name);
    }
    static void Sum(string dir, string name)
    {
        var sums = Path.Combine(dir, "SHA256SUMS");
        var lines = File.Exists(sums) ? File.ReadAllLines(sums).Where(l => !l.EndsWith("  " + name, StringComparison.Ordinal)).ToList() : [];
        lines.Add(Sha(Path.Combine(dir, name)) + "  " + name);
        File.WriteAllText(sums, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }

    static string? Ffprobe()
    {
        foreach (var p in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (p.Length > 0 && File.Exists(Path.Combine(p, "ffprobe.exe"))) return Path.Combine(p, "ffprobe.exe");
        var own = Path.Combine(DataLocation.ToolsRoot(), "ffmpeg", "bin", "ffprobe.exe");
        return File.Exists(own) ? own : null;
    }

    static string Probe(string ffprobe, string file)
    {
        var r = Cli.Run(ffprobe, ["-v", "error", "-show_entries", "format=format_name,duration,size:stream=codec_type,codec_name,width,height,sample_rate,channels", "-of", "default=noprint_wrappers=0", file], timeoutMs: 60_000);
        Assert.True(r.Ok, "ffprobe: " + r.Err);
        return r.Out;
    }

    static void TypeInto(Window w, string name, string text)
    {
        var t = w.Find<TextBox>(name);
        t.BringIntoView(); Settle();
        t.Focus(); t.Text = text; Settle();
    }

    static void Click(Window w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, w.Find<Button>(name)); Settle(); }

    static void WaitUntil(Func<bool> done, TimeSpan max, string what)
    {
        var sw = Stopwatch.StartNew();
        while (!done())
        {
            Assert.True(sw.Elapsed < max, what + " dauert länger als " + max);
            Thread.Sleep(250); Settle();
        }
    }

    [Fact(DisplayName = "T-0738 Belege: Benutzerordner (auch URL-kodiert), bloßer Benutzername, Rechnername und ANSI-Steuerfolgen werden entfernt")]
    public void Beleg_anonymisiert_auch_url_kodiert()
    {
        const string raw = "INFO: \"GET /v1/audio?path=C%3A%5CUsers%5CErika%5CAppData%5Cx.mp3 HTTP/1.1\" 200\r\n"
            + "q=C%3a%5cusers%5cerika%5cTemp und C%3A%2FUsers%2FErika%2FTemp\n"
            + @"C:\Users\Erika\AppData und C:/Users/erika/AppData, Konto erika, PC ERIKA-PC" + "\n"
            + "teilkodiert C:%5CUsers%5CErika%5Cx und %2FUsers%2Ferika%2Fy\n" // Sammelstand 0.9.105: Lücke aus der zweiten Prüfung T-0738
            + "erikas Datei bleibt, \u001b[32mgrün\u001b[0m \u001b[Kfortschritt\u001b[2K\u001b[1;5H.";
        var s = Anon(NoAnsi(raw), @"C:\Users\Erika", "ERIKA-PC");
        Assert.False(s.Contains('\u001b'), "ESC-Steuerzeichen übrig: " + s); // Zeichenvergleich: kulturabhängig würde „\u001b“ ignoriert
        Assert.DoesNotMatch("(?i)(?<![a-z0-9])erika(?![a-z0-9])", s);
        Assert.False(s.Contains('\r'), "CR übrig");
        Assert.Contains("path=C%3A%5CUsers%5C%3Cuser%3E%5CAppData", s);
        Assert.Contains("C%3A%2FUsers%2F%3Cuser%3E%2FTemp", s);
        Assert.Contains(@"C:\Users\<user>\AppData", s);
        Assert.Contains("C:/Users/<user>/AppData", s);
        Assert.Contains("PC <pc>", s);
        Assert.Contains("erikas Datei bleibt, grün fortschritt.", s); // Wortteile und Text bleiben unverändert
    }

    [AvaloniaFact(DisplayName = "T-0738 Bilder mit KI: fehlt die Bild-KI, nennt die App Programm und Modell einzeln und führt zur KI-Bibliothek")]
    public void ImageAi_blocked_names_prerequisite()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(Img); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var d = w.ProgramActive(Img)!;
            Assert.Equal("Bild-KI fehlt", w.Find<TextBlock>("prog-blocker-text").Text);
            RealClick(w, w.Find<Button>("img-go")); Settle();
            WaitUntil(() => w.ProgramMessage(Img).Length > 0, TimeSpan.FromSeconds(10), "Meldung");
            var msg = w.ProgramMessage(Img);
            Assert.Contains("sd-cli.exe", msg); Assert.Contains("SDXL Turbo", msg); Assert.Contains("KI-Bibliothek", msg);
            Assert.Empty(d.List("images"));

            // nur das Programm da (leere Datei reicht für die Erkennung): jetzt fehlt nur noch das Modell
            var exe = w.KiStore.RuntimeExe(KiRuntime.StableDiffusionCpp);
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!); File.WriteAllBytes(exe, []);
            w.Show(Img); Settle();
            Assert.Equal("Bildmodell fehlt", w.Find<TextBlock>("prog-blocker-text").Text);
            var t = w.ImgGenerate(d); WaitUntil(() => t.IsCompleted, TimeSpan.FromSeconds(10), "Meldung");
            Assert.DoesNotContain("sd-cli.exe", w.ProgramMessage(Img)); Assert.Contains("Bildmodell", w.ProgramMessage(Img));

            RealClick(w, w.Find<Button>("prog-blocker-setup")); Settle();
            Assert.Equal("ki-bibliothek", w.CurrentView);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0738 Trendo Musik: der Hinweis unter „Erstellen“ nennt die gemessene Dauer statt „unter einer Minute“")]
    public void Musik_hint_matches_measurement()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(Ms); Settle();
            var texts = w.GetLogicalTexts();
            Assert.DoesNotContain(texts, t => t.Contains("unter einer Minute"));
            Assert.Contains(texts, t => t.Contains("Beim ersten Lied lädt die Musik-KI erst ihre Modelle")); // Echtlauf 17.11.2026: Start 115 s, 30-s-Lied 142 s
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0738 ECHT Bilder mit KI: Klick auf „Bilder malen“ → sd-cli dieses PCs → PNG im Projekt (nur mit TRENDO_ECHT_KI)")]
    public void ImageAi_echt()
    {
        if (BelegDir is not { } beleg) { Assert.Skip(SkipText); return; }
        var (w, _, root) = Ui.Open();
        var log = new StringBuilder();
        try
        {
            w.Deps.ToolsRoot = DataLocation.ToolsRoot(); // die echten Werkzeuge dieses PCs – nur lesen und ausführen, nichts einrichten
            w.KiRealToolsInTests = true;
            log.Append($"T-0738 Echtlauf „Bilder mit KI“ {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\n");
            log.Append($"Werkzeugordner: {w.Deps.ToolsRoot}\n");
            var exe = w.KiStore.RuntimeExe(KiRuntime.StableDiffusionCpp);
            log.Append($"sd-cli: {exe} vorhanden={File.Exists(exe)}\n");
            foreach (var id in new[] { "sdxl-turbo", "sd-turbo" })
            {
                var m = KiLibrary.Models.First(x => x.Id == id);
                log.Append($"Modell {id}: {w.KiStore.ModelFile(m, m.Files[0])} bereit={w.KiStore.ModelReady(m)}\n");
            }
            if (w.ImgAiMissing() is { } miss)
            {
                Beleg(beleg, "bild-ki-lauf.log", log + "BLOCKIERT: " + miss.Long + "\n");
                Assert.Fail("BLOCKIERT: " + miss.Long);
            }

            w.Show(Img); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(Img)!;
            Assert.Null(w.TryFind<Border>("prog-blocker"));
            const string prompt = "ein alter Leuchtturm auf einer Klippe bei Sonnenuntergang, Wellen, Möwen";
            TypeInto(w, "img-prompt", prompt);
            if (w.TryFind<TextBox>("img-p-count") is not null) TypeInto(w, "img-p-count", "1"); else d.Set("count", "1", "Varianten");
            var go = w.Find<Button>("img-go"); go.Focus(); Settle(); // Feld verlässt den Fokus → Eingabe steht im Dokument
            Assert.Equal(prompt, d.Text("prompt")); Assert.Equal("1", d.Text("count"));
            var sw = Stopwatch.StartNew();
            RealClick(w, go);
            WaitUntil(() => !w.ProgramMessage(Img).StartsWith("Male Bild", StringComparison.Ordinal) && w.ProgramMessage(Img).Length > 0, TimeSpan.FromMinutes(20), "Malen");
            sw.Stop();
            log.Append($"Meldung der App: {w.ProgramMessage(Img)}\nLaufzeit vom Klick bis zur Meldung: {sw.Elapsed.TotalSeconds:0.0} s\n\n--- Rohausgabe sd-cli ---\n{w.ImgAiLastLog}\n");
            var imgs = d.List("images").Select(n => (string)n!).ToList();
            if (imgs.Count == 0) { Beleg(beleg, "bild-ki-lauf.log", log.ToString()); Assert.Fail("kein Bild: " + w.ProgramMessage(Img)); }
            var png = imgs[0];
            Assert.StartsWith(Path.Combine(root, "ki-bilder", d.Text("dir")), png); // das Bild liegt im Bildordner des Dokuments
            Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], File.ReadAllBytes(png)[..4]);
            var copy = Path.Combine(beleg, "bild-ki-leuchtturm.png");
            File.Copy(png, copy, true);
            File.Copy(png + ".json", Path.Combine(beleg, "bild-ki-leuchtturm.png.json"), true);
            File.WriteAllText(Path.Combine(beleg, "bild-ki-leuchtturm.png.json"), Anon(File.ReadAllText(png + ".json")), new UTF8Encoding(false));
            log.Append($"Bild im Projekt: {png}\nGröße: {new FileInfo(png).Length} Bytes\nSHA-256: {Sha(png)}\n");
            var fp = Ffprobe();
            Assert.NotNull(fp); // ohne Fremdwerkzeug kein Nachweis, dass sich das Bild außerhalb von Trendo öffnet
            var pr = Probe(fp!, copy);
            log.Append($"\n--- ffprobe (Fremdwerkzeug) ---\n{pr}\n");
            Assert.Contains("codec_name=png", pr); Assert.Contains("width=1024", pr); Assert.Contains("height=1024", pr);
            w.Show(Img); Settle();
            Assert.Single(w.Find<WrapPanel>("img-gallery").Children);
            Sum(beleg, "bild-ki-leuchtturm.png"); Sum(beleg, "bild-ki-leuchtturm.png.json");
            Beleg(beleg, "bild-ki-lauf.log", log.ToString());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0738 ECHT Trendo Musik: Klick auf „Erstellen“ → ACE-Step dieses PCs → 30-s-Lied in der Bibliothek (nur mit TRENDO_ECHT_KI)")]
    public void Musik_echt()
    {
        if (BelegDir is not { } beleg) { Assert.Skip(SkipText); return; }
        var (w, _, root) = Ui.Open();
        var log = new StringBuilder();
        var serverLog = "";
        long serverLogStart = 0;
        try
        {
            w.Deps.ToolsRoot = DataLocation.ToolsRoot(); // die echten Werkzeuge dieses PCs – Trendo startet seinen Musik-Server wie in der App
            log.Append($"T-0738 Echtlauf „Trendo Musik“ {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\nWerkzeugordner: {w.Deps.ToolsRoot}\n");
            var dir = w.KiStore.RuntimeDir(KiRuntime.AceStep);
            var (py, _) = AceStepClient.ServerCommand(dir);
            log.Append($"ACE-Step: {dir}\npython: {py} vorhanden={File.Exists(py)}\nModell schnell: {Directory.Exists(Path.Combine(dir, "checkpoints", AceStepClient.TurboModel))}\n");
            if (!File.Exists(py) || !Directory.Exists(Path.Combine(dir, "checkpoints", AceStepClient.TurboModel)))
            {
                Beleg(beleg, "musik-lauf.log", log + "BLOCKIERT: ACE-Step 1.5 ist nicht eingerichtet (python_embeded\\python.exe und checkpoints\\" + AceStepClient.TurboModel + "). Einrichten: KI-Bibliothek → Musik.\n");
                Assert.Fail("BLOCKIERT: ACE-Step fehlt");
            }
            serverLog = Path.Combine(w.KiStore.Root, "acestep-server.log");
            serverLogStart = File.Exists(serverLog) ? new FileInfo(serverLog).Length : 0;

            w.Show(Ms); Settle();
            // Umschalter über das Klick-Ereignis des Knopfs: im kleinen Testfenster ragt „Mit eigenem Songtext“ mit der Kopfzeilen-Schrift
            // des Testsystems über die Spalte hinaus (die Trefferprüfung von RealClick landet dann in „Meine Lieder“); im Bild der App (1100 × 800) passt er.
            w.Find<Button>("ms-mode-profi").Press(); Settle();
            TypeInto(w, "ms-title", "Probe T-0738");
            TypeInto(w, "ms-style", "fröhlicher Pop, Klavier, Akustikgitarre, warme Frauenstimme");
            TypeInto(w, "ms-lyrics", "[Strophe]\nHeute scheint die Sonne über unsrer Stadt\n[Refrain]\nWir singen laut, weil jeder Tag ein Anfang hat");
            w.Find<ComboBox>("ms-length").SelectedIndex = 0; // 30 Sekunden
            w.Find<ComboBox>("ms-variants").SelectedIndex = 0; // 1 Fassung
            w.Find<ComboBox>("ms-language").SelectedIndex = 0; // Deutsch
            Settle();
            var sw = Stopwatch.StartNew();
            Click(w, "ms-create");
            WaitUntil(() => w.Songs.All().Count > 0 || (w.TryFind<TextBlock>("ms-msg") is { Text: { } t } && (t.StartsWith("Hat nicht", StringComparison.Ordinal) || t.Contains("nicht eingerichtet") || t.Contains("abgebrochen") || t.Contains("beendet"))),
                TimeSpan.FromMinutes(60), "Lied erzeugen");
            sw.Stop();
            Settle();
            log.Append($"Meldung der App: {w.TryFind<TextBlock>("ms-msg")?.Text}\nLaufzeit vom Klick bis zum Lied in der Bibliothek: {sw.Elapsed.TotalSeconds:0.0} s\n");
            var songs = w.Songs.All();
            if (songs.Count == 0) { Beleg(beleg, "musik-lauf.log", log.ToString()); Assert.Fail("kein Lied: " + w.TryFind<TextBlock>("ms-msg")?.Text); }
            var s = songs[0];
            Assert.StartsWith(Path.Combine(root, "Projekte", "Musik"), s.File, StringComparison.OrdinalIgnoreCase); // das Lied liegt im Projektordner „Musik“
            var ext = Path.GetExtension(s.File);
            var copy = Path.Combine(beleg, "trendo-musik-probe" + ext);
            File.Copy(s.File, copy, true);
            log.Append($"Lied in der Bibliothek: „{s.Title}“, {s.Seconds} s, Sprache {s.Language}, gemastert={s.Mastered}\nDatei im Projekt: {s.File}\nGröße: {new FileInfo(s.File).Length} Bytes\nSHA-256: {Sha(s.File)}\n");
            var fp = Ffprobe();
            Assert.NotNull(fp);
            var pr = Probe(fp!, copy);
            log.Append($"\n--- ffprobe (Fremdwerkzeug) ---\n{pr}\n");
            Assert.Contains("codec_type=audio", pr);
            var dur = double.Parse(pr.Split('\n').First(l => l.StartsWith("duration=", StringComparison.Ordinal))["duration=".Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(dur, 20, 40);
            Sum(beleg, Path.GetFileName(copy));
        }
        finally
        {
            w.Close(); // beendet Trendos eigenen Musik-Server
            if (serverLog.Length > 0 && File.Exists(serverLog))
            {
                using var fs = new FileStream(serverLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(Math.Min(serverLogStart, fs.Length), SeekOrigin.Begin);
                log.Append("\n--- Rohlog ACE-Step-Server (nur dieser Lauf, aus ki\\acestep-server.log) ---\n").Append(NoAnsi(new StreamReader(fs).ReadToEnd()));
            }
            Beleg(beleg, "musik-lauf.log", log.ToString());
        }
    }
}
