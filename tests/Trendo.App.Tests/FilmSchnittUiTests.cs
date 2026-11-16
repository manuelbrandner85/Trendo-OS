using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Film;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0693: „Filme machen“ erzeugt einen Film – Material je Einstellung, „Film rendern“ vom Klick bis zur echten Datei (mit echtem FFmpeg),
/// „Im Videoschnitt weiterbearbeiten“, Abbrechen, KI schreibt ins offene Dokument, Leerzustand, Tastatur und Rechtsklick.</summary>
public class FilmSchnitt_Ui
{
    const string K = "film-programm";

    static async Task Until(Func<bool> cond, int seconds = 30)
    {
        var sw = Stopwatch.StartNew();
        while (!cond()) { Dispatcher.UIThread.RunJobs(); if (sw.Elapsed > TimeSpan.FromSeconds(seconds)) break; await Task.Delay(30); }
        Dispatcher.UIThread.RunJobs();
    }

    static Trendo.Core.Programs.ProgramDocument Sample(MainWindow w)
    {
        w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-kurzfilm")); Settle();
        RealClick(w, w.Find<Button>("prog-cmd-film-shots")); Settle();
        return w.ProgramActive(K)!;
    }

    [AvaloniaFact(DisplayName = "Film mit echtem FFmpeg, vom Klick bis zur Datei: Beispiel-Kurzfilm, Clips über die Dateiauswahl, Foto auf eine Einstellung gelegt, Musik, Klick auf „Film rendern“ → MP4 und SRT; ffprobe prüft Dauer, Spuren, Auflösung; danach „Im Videoschnitt weiterbearbeiten“")]
    public async Task Render_click_to_file_real()
    {
        var ff = Cli.Find("ffmpeg"); var probe = Cli.Find("ffprobe");
        if (ff is null || probe is null) { Assert.Skip("Übersprungen – FFmpeg oder ffprobe fehlt auf diesem PC."); return; }
        var (w, _, root) = Ui.Open();
        var log = new StringBuilder();
        try
        {
            var media = Path.Combine(root, "eigene-clips"); Directory.CreateDirectory(media);
            string P(string n) => Path.Combine(media, n);
            foreach (var (name, color, hz) in new[] { ("rot", "0xFF0000", 440), ("gruen", "0x00FF00", 660), ("blau", "0x0000FF", 880) })
                Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"color=c={color}:size=640x360:rate=25:duration=6", "-f", "lavfi", "-i", $"sine=frequency={hz}:duration=6", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", P(name + ".mp4")], media, 60000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "color=c=0xFFFF00:size=800x600", "-frames:v", "1", P("foto.png")], media, 60000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=220:duration=40", P("musik.mp3")], media, 60000).Ok);

            var d = Sample(w);
            Assert.Equal(5, FilmCut.Shots(d.Data).Count);
            Assert.NotNull(w.TryFind<Grid>("film-production"));
            w.FilmFfmpegPath = ff; // echter FFmpeg statt der Sperre des abgeschotteten Testmodus – kein Ersatz für das Werkzeug

            // Clips über die Dateiauswahl: füllen der Reihe nach die Einstellungen ohne Material
            w.PickOverride = _ => Task.FromResult<string[]?>([P("rot.mp4"), P("gruen.mp4"), P("blau.mp4")]);
            RealClick(w, w.Find<Button>("prog-cmd-film-import"));
            await Until(() => FilmCut.Shots(d.Data).Count(s => s.Kind == "video") == 3);
            var shots = FilmCut.Shots(d.Data);
            Assert.Equal(["video", "video", "video", "karte", "karte"], shots.Select(s => s.Kind));
            Assert.Equal(4, shots[0].Seconds, 3); // geplante 4 s aus einem 6-s-Clip
            // Foto gezielt auf eine Einstellung gelegt (derselbe Weg wie Ziehen-und-Ablegen auf die Zeile)
            Assert.Equal(1, await w.FilmImport(d, [P("foto.png")], "SZ02-E01"));
            Assert.Equal("bild", FilmCut.Shots(d.Data)[3].Kind);
            // Musik über die Dateiauswahl
            w.PickOverride = _ => Task.FromResult<string[]?>([P("musik.mp3")]);
            RealClick(w, w.Find<Button>("prog-cmd-film-import-music"));
            await Until(() => FilmCutOptions.Of(d.Data).Music.Length > 0);
            Assert.InRange(FilmCutOptions.Of(d.Data).MusicLength, 39, 41);
            Settle();
            Assert.Contains("4 von 5 Einstellungen mit Material", w.Find<TextBlock>("film-plan-info").Text);

            var plan = FilmCut.Plan(d.Data, d.Title, Path.Combine(root, "plan"));
            // Klick auf die goldene Hauptaktion
            var render = w.Find<Button>("prog-cmd-film-render");
            RealClick(w, render);
            Assert.True(w.FilmRendering);
            await Until(() => !w.FilmRendering, 180);
            Assert.True(w.FilmLastOutput is not null, w.ProgramMessage(K));
            var film = w.FilmLastOutput!;
            Assert.True(File.Exists(film)); Assert.EndsWith("Der-letzte-Zug.mp4", film);
            Assert.Contains("Film fertig", w.ProgramMessage(K)); Assert.Contains("1 Einstellung(en) ohne Material", w.ProgramMessage(K));
            Assert.Contains("Der-letzte-Zug.mp4", w.Find<TextBlock>("film-result").Text);

            string Probe(params string[] a) { var r = Cli.Run(probe, ["-v", "error", .. a], root, 60000); Assert.True(r.Ok, r.Err); return r.Out.Trim(); }
            var dur = double.Parse(Probe("-show_entries", "format=duration", "-of", "csv=p=0", film), CultureInfo.InvariantCulture);
            var streams = Probe("-show_entries", "stream=codec_type,codec_name,width,height", "-of", "csv=p=0", film).Replace("\r", "").Replace("\n", " | ");
            log.AppendLine($"Datei: {Path.GetFileName(film)}, {new FileInfo(film).Length} Bytes");
            log.AppendLine($"Dauer {dur.ToString("0.000", CultureInfo.InvariantCulture)} s, geplant {plan.Seconds.ToString("0.000", CultureInfo.InvariantCulture)} s ({plan.Pieces.Count} Stücke: {string.Join(", ", plan.Pieces.Select(p => $"{p.Kind} {p.Seconds.ToString("0.#", CultureInfo.InvariantCulture)}"))})");
            log.AppendLine("Spuren: " + streams);
            Assert.Equal(7, plan.Pieces.Count); // Titel, 5 Einstellungen, Abspann
            Assert.InRange(dur, plan.Seconds - 0.3, plan.Seconds + 0.3);
            Assert.Contains("video", streams); Assert.Contains("audio", streams); Assert.Contains("1280,720", streams);
            var srt = Path.ChangeExtension(film, ".srt");
            Assert.True(File.Exists(srt));
            var cues = TimedText.FromSrt(File.ReadAllText(srt));
            Assert.Equal(3, cues.Count); Assert.Contains("Ist er schon weg?", cues[0].Text);
            log.AppendLine("Untertitel: " + string.Join(" | ", cues.Select(c => $"{c.Start.ToString("0.00", CultureInfo.InvariantCulture)}–{c.End.ToString("0.00", CultureInfo.InvariantCulture)} {c.Text.Replace('\n', ' ')}")));
            // Standbild aus der Mitte der zweiten Einstellung: der grüne Clip
            var starts = VideoEdit.Starts(FilmCut.Clips(plan)); var t = starts[2] + plan.Pieces[2].Seconds / 2;
            var raw = Path.Combine(root, "pixel.rgb");
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", t.ToString("0.###", CultureInfo.InvariantCulture), "-i", film, "-frames:v", "1", "-vf", "scale=1:1", "-f", "rawvideo", "-pix_fmt", "rgb24", raw], root, 60000).Ok);
            var px = File.ReadAllBytes(raw);
            log.AppendLine($"Standbild bei {t.ToString("0.00", CultureInfo.InvariantCulture)} s (SZ01-E02, erwartet grün): R={px[0]} G={px[1]} B={px[2]}");
            Assert.True(px[1] > 150 && px[0] < 90 && px[2] < 90, $"{px[0]},{px[1]},{px[2]}");
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "film.render");

            // dieselbe Zeitleiste im Videoschnitt
            w.Show(K); Settle();
            RealClick(w, w.Find<Button>("prog-cmd-film-to-editor"));
            await Until(() => w.ProgramActive("schnitt") is not null && w.CurrentView == "schnitt");
            var v = w.ProgramActive("schnitt")!;
            Assert.Equal("Der letzte Zug", v.Title);
            Assert.Equal(7, VideoEdit.Clips(v.Data).Count); Assert.Single(VideoEdit.Clips(v.Data, "music"));
            Assert.Equal(5, VideoEdit.Markers(v.Data).Count(m => m.Kind == "shot"));
            Assert.Equal(plan.Seconds, VideoEdit.Duration(v.Data), 2);
            Assert.True(v.CanUndo);
            log.AppendLine($"Videoschnitt: {VideoEdit.Clips(v.Data).Count} Stücke, {VideoEdit.Markers(v.Data).Count} Marken, Länge {VideoEdit.Duration(v.Data).ToString("0.00", CultureInfo.InvariantCulture)} s");

            log.AppendLine($"SHA-256 {Path.GetFileName(film)}: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(film))).ToLowerInvariant()}");
            log.AppendLine($"SHA-256 {Path.GetFileName(srt)}: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(srt))).ToLowerInvariant()}");
            if (Environment.GetEnvironmentVariable("TRENDO_T0693_BELEG") is { Length: > 0 } beleg)
            {
                Directory.CreateDirectory(beleg);
                File.WriteAllText(Path.Combine(beleg, "ui-rohlog.txt"), log.ToString());
                File.Copy(srt, Path.Combine(beleg, "Der-letzte-Zug.srt"), true);
                Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", "1", "-i", film, "-frames:v", "1", "-vf", "scale=640:-2", Path.Combine(beleg, "ui-standbild-titel.png")], root, 60000);
                Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", (starts[5] + 1).ToString("0.###", CultureInfo.InvariantCulture), "-i", film, "-frames:v", "1", "-vf", "scale=640:-2", Path.Combine(beleg, "ui-standbild-texttafel.png")], root, 60000);
                Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-sseof", "-2", "-i", film, "-frames:v", "1", "-vf", "scale=640:-2", Path.Combine(beleg, "ui-standbild-abspann.png")], root, 60000);
            }
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Film rendern: Fortschritt und Abbrechen (Export und Tafeln im Test ersetzt) – der Balken erscheint, „Abbrechen“ beendet den Lauf, es bleibt keine Datei")]
    public async Task Render_cancel_replaced()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var d = Sample(w);
            w.FfmpegRunOverride = a => { File.WriteAllBytes(a[^1], [1, 2, 3]); return new Cli.Result(0, "", ""); }; // ersetzt: Texttafeln
            var started = new TaskCompletionSource();
            w.ExportAsyncOverride = async (_, file, progress, ct) => { progress.Report(0.4); started.SetResult(); await Task.Delay(Timeout.Infinite, ct); return file; }; // ersetzt: FFmpeg-Export
            RealClick(w, w.Find<Button>("prog-cmd-film-render"));
            await Until(() => started.Task.IsCompleted);
            Settle();
            Assert.True(w.FilmRendering);
            await Until(() => w.TryFind<ProgressBar>("film-render-bar") is { Value: > 30 });
            RealClick(w, w.Find<Button>("film-render-cancel"));
            await Until(() => !w.FilmRendering);
            Assert.Null(w.FilmLastOutput);
            Assert.Contains("abgebrochen", w.ProgramMessage(K));
            Assert.Null(w.TryFind<ProgressBar>("film-render-bar"));
            Assert.Empty(Directory.GetFiles(root, "*.srt", SearchOption.AllDirectories));

            // ohne FFmpeg im abgeschotteten Modus: klare Meldung, nichts wird vorgetäuscht
            w.ExportAsyncOverride = null;
            RealClick(w, w.Find<Button>("prog-cmd-film-render"));
            await Until(() => !w.FilmRendering && w.ProgramMessage(K).Contains("FFmpeg"));
            Assert.Null(w.FilmLastOutput);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Film: Leerzustand zeigt den ersten Schritt; Fotos ohne Drehbuch werden zu Einstellungen; Auswahl, Tastatur (Pfeile, Alt+Pfeil, Entf), Rechtsklick-Menü, Dauer ändern, Rückgängig")]
    public async Task Empty_state_keyboard_context()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show(K); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            RealClick(w, w.Find<Button>("film-step-shots")); Settle();
            Assert.NotNull(w.TryFind<StackPanel>("film-empty"));
            Assert.NotNull(w.TryFind<Button>("film-empty-write")); Assert.NotNull(w.TryFind<Button>("film-empty-clips"));
            // genau eine goldene Hauptaktion im Menüband des Programms: „Film rendern“
            Assert.Equal(["film-render"], MainWindow.Programs[K].Ribbon.SelectMany(g => g.Cmds).Where(c => c.Primary).Select(c => c.Name));

            // zwei Fotos (echte PNG-Dateien) über „Clips hinzufügen“ – ohne FFmpeg möglich
            var a = Path.Combine(root, "strand.png"); var b = Path.Combine(root, "berg.png");
            foreach (var f in new[] { a, b }) new Avalonia.Media.Imaging.WriteableBitmap(new Avalonia.PixelSize(32, 18), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque).Save(f);
            w.PickOverride = _ => Task.FromResult<string[]?>([a, b]);
            RealClick(w, w.Find<Button>("film-empty-clips"));
            await Until(() => FilmCut.Shots(d.Data).Count == 2);
            Settle();
            var shots = FilmCut.Shots(d.Data);
            Assert.All(shots, s => Assert.Equal("bild", s.Kind));
            Assert.Contains("- Totale: strand (4 s)", d.Text("script"));
            Assert.Null(w.TryFind<StackPanel>("film-empty"));
            Assert.Equal(shots[0].Id, w.FilmSelected);

            // Auswahl per Klick und Tastatur
            RealClick(w, w.Find<Button>("film-shot-" + shots[0].Id)); Settle();
            var prod = w.Find<Grid>("film-production"); prod.Focus(); Settle();
            w.Key(PhysicalKey.ArrowDown); Settle();
            Assert.Equal(shots[1].Id, w.FilmSelected);
            // Alt+↑: die zweite Einstellung wandert nach vorn, das Foto wandert mit
            w.Find<Grid>("film-production").Focus(); Settle();
            w.Key(PhysicalKey.ArrowUp, RawInputModifiers.Alt); Settle();
            var moved = FilmCut.Shots(d.Data);
            Assert.Contains("berg", moved[0].Shot.Description); Assert.Equal(b, moved[0].Material!.File);
            Assert.Equal(moved[0].Id, w.FilmSelected);
            // Entf: Material weg → Texttafel
            w.Find<Grid>("film-production").Focus(); Settle();
            w.Key(PhysicalKey.Delete); Settle();
            Assert.Equal("karte", FilmCut.Shots(d.Data)[0].Kind);
            Assert.Contains("Texttafel", w.ProgramMessage(K));
            Assert.True(d.Undo()); w.Show(K); Settle();
            Assert.Equal("bild", FilmCut.Shots(d.Data)[0].Kind);

            // Rechtsklick-Menü an der Einstellung
            var menu = w.Find<Button>("film-shot-" + moved[1].Id).ContextMenu!;
            var items = menu.Items.OfType<MenuItem>().ToList();
            Assert.Contains(items, i => i.Name == "film-menu-material"); Assert.Contains(items, i => i.Name == "film-menu-hinter");
            items.Single(i => i.Name == "film-menu-entfernen").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Equal("karte", FilmCut.Shots(d.Data)[1].Kind);

            // Dauer in den Eigenschaften (Stufe Erweitert zeigt sie rechts)
            RealClick(w, w.Find<Button>("film-shot-" + moved[0].Id)); Settle();
            var sec = w.Find<TextBox>("film-p-seconds"); sec.Focus(); sec.Text = "6,5"; w.Find<TextBox>("film-o-fade").Focus(); Settle();
            Assert.Equal(6.5, FilmCut.Shots(d.Data)[0].Seconds, 3); Assert.Contains("(6,5 s)", d.Text("script"));
            // Rohschnitt-Schalter: ohne Titel wird der Film kürzer, rückgängig machbar
            var before = FilmCut.Plan(d.Data, d.Title, root).Seconds;
            w.Find<CheckBox>("film-o-title").IsChecked = false; Settle();
            Assert.True(FilmCut.Plan(d.Data, d.Title, root).Seconds < before);
            Assert.True(d.Undo());
            Assert.Equal(before, FilmCut.Plan(d.Data, d.Title, root).Seconds, 3);
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Film-KI: „Drehbuch mit KI schreiben“ schreibt Drehbuch und Shotliste ins offene Dokument (KI im Test ersetzt), ein Schritt, rückgängig; unbrauchbare Antwort ändert nichts; der KI-Auftrag „Drehbuch-Gerüst“ landet im Dokument; Rendern startet dabei nie")]
    public async Task Ki_writes_into_document()
    {
        var (w, _, _) = Ui.Open(offline: true); // offline: der Assistent fragt keine echte KI, der Auftrag geht an den Studio-Ablauf
        try
        {
            w.Show(K); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!; var empty = d.Text("script");

            // ohne Satz: Hinweis, keine Anfrage
            var asked = 0;
            w.FilmKiOverride = p => { asked++; Assert.Contains("Idee: Ein Bäcker findet nachts einen Brief", p); return Task.FromResult<string?>("Klar!\n```\nTitel: Der Brief\n\nINNEN. BACKSTUBE – NACHT\n\nMehl liegt in der Luft.\n\n- Totale: Backstube im Halbdunkel (4 s)\n\nERIKA\nWer schreibt mir um diese Zeit?\n\nAUSSEN. GASSE – MORGEN\n\nDer Brief liegt im Rinnstein.\n```"); };
            RealClick(w, w.Find<Button>("prog-cmd-film-ki-start")); Settle();
            Assert.Equal(0, asked); Assert.Contains("Worum geht es", w.ProgramMessage(K));

            var log = w.Find<TextBox>("film-p-logline"); log.Focus(); log.Text = "Ein Bäcker findet nachts einen Brief"; w.Find<TextBox>("film-p-genre").Focus(); Settle();
            var undoBefore = d.History.Count;
            RealClick(w, w.Find<Button>("prog-cmd-film-ki-start"));
            await Until(() => d.Text("script").Contains("BACKSTUBE"));
            Settle();
            Assert.Equal(1, asked);
            Assert.DoesNotContain("```", d.Text("script"));
            var shots = FilmCut.Shots(d.Data);
            Assert.Equal(2, shots.Count); // eine von der KI, eine für die Szene ohne Einstellungen ergänzt
            Assert.Equal(undoBefore + 1, d.History.Count); Assert.Equal("Drehbuch von der KI", d.UndoLabel);
            Assert.Contains("BACKSTUBE", w.Find<TextBox>("film-script").Text); // steht bearbeitbar im Drehbuch-Feld
            Assert.False(w.FilmRendering); Assert.Null(w.FilmLastOutput);
            Assert.True(d.Undo());
            Assert.Equal(empty, d.Text("script"));
            Assert.True(d.Redo());

            // Antwort ohne Szenenkopf: nichts ändert sich
            var good = d.Text("script");
            w.FilmKiOverride = _ => Task.FromResult<string?>("Das kann ich leider nicht.");
            RealClick(w, w.Find<Button>("prog-cmd-film-ki-start"));
            await Until(() => w.ProgramMessage(K).Contains("kein Drehbuch"));
            Assert.Equal(good, d.Text("script"));
            // keine KI erreichbar (abgeschotteter Testmodus): klare Meldung statt Gerüst
            w.FilmKiOverride = null;
            RealClick(w, w.Find<Button>("prog-cmd-film-ki-start"));
            await Until(() => w.ProgramMessage(K).Contains("keine KI gefragt"));
            Assert.Equal(good, d.Text("script"));

            // KI-Auftrag über die Auftragszeile: das Gerüst mit Shotliste landet im offenen Dokument, rückgängig machbar
            w.Show(K); Settle();
            var box = w.Find<TextBox>("auftrag-prompt"); box.Text = "Idee: Eine Frau verpasst den letzten Zug";
            w.Core.Projects.Create("filmprobe", "Filmprobe"); // der Studio-Ablauf legt sein Ergebnis in ein Projekt
            w.Find<Button>("auftrag-pruefen").Press();
            await Until(() => w.Find<Button>("auftrag-los").IsVisible || d.Text("script").Contains("# Anfang"), 60);
            if (!d.Text("script").Contains("# Anfang")) { w.Find<Button>("auftrag-los").Press(); await Until(() => d.Text("script").Contains("# Anfang"), 20); }
            Assert.Contains("# Anfang", d.Text("script")); Assert.Contains("- Totale:", d.Text("script"));
            Assert.Equal("Drehbuch aus dem KI-Auftrag", d.UndoLabel);
            Assert.Equal(3, FilmCut.Shots(d.Data).Count);
            Assert.True(d.Undo()); Assert.Equal(good, d.Text("script"));
            Assert.False(w.FilmRendering);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Film: Drehbuch aus einer fremden Fountain-Datei öffnen; ein Dokument aus Teil 1 (ohne Material und Schnitt-Einstellungen) bleibt lesbar")]
    public async Task Fountain_import_and_old_document()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show(K); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            // Fountain, wie es andere Drehbuchprogramme schreiben (englische Szenenköpfe, Title-Block)
            var file = Path.Combine(root, "big-fish-auszug.fountain");
            File.WriteAllText(file, "Title: The Pier\nAuthor: Erika Muster\n\nEXT. PIER - DAY\n\nWaves hit the wood.\n\nANNA\nYou came back.\n\nINT. BOAT HOUSE - NIGHT\n\nA lamp flickers.\n");
            w.PickOverride = _ => Task.FromResult<string[]?>([file]);
            RealClick(w, w.Find<Button>("prog-cmd-film-open-fountain"));
            await Until(() => d.Text("script").Contains("PIER"));
            Assert.Contains("2 Szenen", w.ProgramMessage(K)); Assert.Equal("The Pier", d.Title);
            Assert.True(d.Undo());

            // altes Dokument: nur die Felder aus Teil 1
            var old = w.ProgramNew(K, "kurzfilm"); Assert.Null(old.Data["material"]); Assert.Null(old.Data["cut"]);
            old.Set("script", Fountain.InsertSuggestedShots(old.Text("script")));
            w.FilmStep = "shots"; w.Show(K); Settle();
            Assert.Equal(5, FilmCut.Shots(old.Data).Count);
            Assert.NotNull(w.TryFind<StackPanel>("film-strip"));
            Assert.Contains("0 von 5 Einstellungen mit Material", w.Find<TextBlock>("film-plan-info").Text);
        }
        finally { w.PickOverride = null; w.Close(); }
    }
}
