using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using SkiaSharp;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0731 SYS-02: Musik ohne Zusatzprogramm (eigene Tonausgabe über die Windows-Tonausgabe) und Bildschirmfoto mit Bereichswahl ohne ffmpeg.
/// Die Echtläufe nutzen das echte Tongerät bzw. den echten Desktop – nichts ersetzt. Mit TRENDO_BELEG_T0731=&lt;Ordner&gt; schreiben sie ihre Belege dorthin.</summary>
public class T0731HelferUiTests
{
    /// <summary>Echte MP3 aus einem fremden Kodierer (ffmpeg/LAME, 2 s, 440 Hz, Stereo, 44,1 kHz) – liegt im Repo.</summary>
    static string Mp3()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "tests", "Trendo.App.Tests", "Daten", "ton-440hz-2s.mp3");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("ton-440hz-2s.mp3 fehlt");
    }

    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0731-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_BELEG_T0731") is { Length: > 0 } b ? Directory.CreateDirectory(b).FullName : null;

    [Fact(DisplayName = "T-0731: MP3 dekodiert Trendo selbst (NLayer) – Rate, Kanäle, Länge, Springen")]
    public void Decode()
    {
        using var s = PcmSource.Open(Mp3());
        Assert.Equal(44100, s.Rate); Assert.Equal(2, s.Channels); Assert.InRange(s.Seconds, 1.9, 2.15);
        var buf = new float[8820]; var n = s.Read(buf, 0, buf.Length); Assert.Equal(buf.Length, n);
        Assert.True(buf.Skip(4410).Max(Math.Abs) > 0.08, "Ton erwartet, nicht Stille"); // ffmpeg-Sinus hat Amplitude 1/8
        s.Seek(1.9); var rest = 0; int r; while ((r = s.Read(buf, 0, buf.Length)) > 0) rest += r;
        Assert.InRange(rest / 2 / 44100.0, 0.02, 0.25); // nach dem Sprung bleibt nur der Rest
        Assert.False(PcmSource.CanPlay("lied.flac")); Assert.True(PcmSource.CanPlay("LIED.MP3"));
    }

    [Fact(DisplayName = "T-0731: eingebaute Tonausgabe (echt, Windows-Tonausgabe) – MP3 spielt in Echtzeit, Gerät meldet die Werte, Pause hält, Titelende genau einmal")]
    public async Task Real_builtin_playback()
    {
        if (!WinAudio.Available) { Assert.Skip("Übersprungen – auf diesem Rechner gibt es keine Windows-Tonausgabe."); return; }
        var log = new StringBuilder(); var sw = Stopwatch.StartNew();
        using var a = new WinAudio();
        double dur = 0, pos = 0; var ended = 0; long endedAt = 0;
        a.Progress += (p, d) => { dur = d; pos = Math.Max(pos, p); };
        a.Ended += () => { if (Interlocked.Increment(ref ended) == 1) endedAt = sw.ElapsedMilliseconds; };
        a.SetVolume(15); a.Load(Mp3());
        log.AppendLine($"geladen: ton-440hz-2s.mp3, Rate {a.Rate} Hz");
        await Task.Delay(600);
        a.SetPause(true); await Task.Delay(200); var held = a.DeviceSamplesPlayed; await Task.Delay(500); var afterPause = a.DeviceSamplesPlayed;
        log.AppendLine($"Pause: Gerätezähler {held} → {afterPause} in 0,5 s");
        Assert.InRange(afterPause - held, 0, 44100 / 20); // in der Pause steht das Gerät (ohne Pause wären es ~22 050 Werte)
        a.SetPause(false);
        for (var i = 0; i < 200 && ended == 0; i++) await Task.Delay(25);
        var device = a.DeviceSamplesPlayed;
        log.AppendLine($"Länge {dur:0.000} s, letzte Position {pos:0.000} s, Titelende nach {endedAt} ms (davon 700 ms Pause), Gerät spielte {device} Werte je Kanal");
        Assert.Equal(1, ended);
        Assert.InRange(dur, 1.9, 2.15);
        Assert.True(endedAt >= 2300, $"Ende nach {endedAt} ms – zu früh für 2 s Ton plus 0,7 s Pause, das Gerät hat also nicht in Echtzeit gespielt");
        Assert.True(device >= 44100 * 1.8, $"Gerät meldet nur {device} gespielte Werte");
        await Task.Delay(300); Assert.Equal(1, ended);
        if (Beleg() is { } b) await File.WriteAllTextAsync(Path.Combine(b, "wiedergabe-echt.log"), log.ToString());
    }

    // ---------- Gegenprüfung: beschädigte MP3 darf Trendo nicht beenden ----------

    /// <summary>Beschädigte MP3 wie im Fuzz-Lauf der Gegenprüfung: 40 zufällige Bytes in die echte Datei geschrieben (festes Muster je <paramref name="seed"/>).</summary>
    static string CorruptMp3(int seed, string dir)
    {
        var b = File.ReadAllBytes(Mp3()); var rng = new Random(seed);
        for (var i = 0; i < 40; i++) b[rng.Next(b.Length)] = (byte)rng.Next(256);
        var f = Path.Combine(dir, $"kaputt-{seed}.mp3"); File.WriteAllBytes(f, b); return f;
    }

    /// <summary>Liest die Datei ganz; liefert die Ausnahme beim Lesen (null = lesbar). Öffnen muss gehen, sonst „nicht geeignet“.</summary>
    static (bool Opened, Exception? ReadError) ReadAll(string f)
    {
        IPcmSource s;
        try { s = PcmSource.Open(f); } catch (Exception) { return (false, null); }
        using (s)
        {
            var buf = new float[4410];
            try { while (s.Read(buf, 0, buf.Length) > 0) { } s.Seek(0.5); s.Read(buf, 0, buf.Length); return (true, null); }
            catch (Exception x) { return (true, x); }
        }
    }

    /// <summary>Erste Datei aus dem festen Muster 0…299, die sich öffnen lässt, beim Dekodieren aber scheitert.</summary>
    static string FirstBrokenMp3(string dir)
    {
        for (var seed = 0; seed < 300; seed++) { var f = CorruptMp3(seed, dir); if (ReadAll(f) is (true, not null)) return f; File.Delete(f); }
        throw new Xunit.Sdk.XunitException("Im festen Muster 0…299 fand sich keine beschädigte MP3, an der NLayer scheitert");
    }

    [Fact(DisplayName = "T-0731 Gegenprüfung: beschädigte MP3 (Fuzz, festes Muster 0…299) – PcmSource meldet „beschädigt“ statt NLayer-Interna (IndexOutOfRange)")]
    public void Corrupt_mp3_pcm_source()
    {
        var dir = Temp(); var opened = 0; var broken = new List<string>();
        for (var seed = 0; seed < 300; seed++)
        {
            var f = CorruptMp3(seed, dir); var (ok, err) = ReadAll(f);
            if (ok) opened++;
            if (err is not null) broken.Add($"{seed}: {err.GetType().Name} – {err.Message}");
        }
        Assert.True(opened > 200, $"nur {opened} von 300 ließen sich öffnen");
        Assert.NotEmpty(broken); // NLayer scheitert an einem Teil der Dateien – genau der Fall aus der Gegenprüfung
        Assert.All(broken, b => Assert.Contains("InvalidDataException – ", b));
        Assert.All(broken, b => Assert.Contains("beschädigt", b));
        if (Beleg() is { } bl) File.WriteAllText(Path.Combine(bl, "gegenpruefung-fuzz.log"),
            $"Fuzz wie in der Gegenprüfung: ton-440hz-2s.mp3, je Datei 40 zufällige Bytes (new Random(seed), seed 0…299)\ngeöffnet: {opened} von 300\nbeim Dekodieren gescheitert: {broken.Count} – alle als InvalidDataException mit „beschädigt“\n" + string.Join("\n", broken) + "\n");
    }

    [Fact(DisplayName = "T-0731 Gegenprüfung: Ton-Thread (WinAudio.Run) wirft bei beschädigter MP3 nicht – er räumt auf und meldet den Fehler")]
    public void Corrupt_mp3_run_thread_does_not_throw()
    {
        var f = FirstBrokenMp3(Temp());
        using var a = new WinAudio();
        var t = typeof(WinAudio); const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        // Run direkt auf dem Testthread – ohne Gerät (Griff 0), damit ein Fehler hier sichtbar wird statt den Testprozess zu beenden.
        t.GetField("_src", F)!.SetValue(a, PcmSource.Open(f)); t.GetField("_h", F)!.SetValue(a, IntPtr.Zero);
        var messages = new List<string>(); var ended = 0;
        a.Ended += () => ended++;
        if (t.GetEvent("Failed") is { } ev) ev.AddEventHandler(a, new Action<string>(messages.Add));
        Exception? thrown = null;
        try { t.GetMethod("Run", F)!.Invoke(a, null); }
        catch (System.Reflection.TargetInvocationException x) { thrown = x.InnerException; }
        Assert.True(thrown is null, $"Ton-Thread warf {thrown?.GetType().Name}: {thrown?.Message} – im echten Hintergrund-Thread beendet das den ganzen Trendo-Prozess");
        Assert.Null(t.GetField("_src", F)!.GetValue(a)); // aufgeräumt: Quelle geschlossen und freigegeben
        var m = Assert.Single(messages); Assert.Contains("beschädigt", m); Assert.Contains(Path.GetFileName(f), m);
        Assert.Equal(0, ended); // Fehler ist kein normales Titelende
        Assert.DoesNotContain("Exception", m); // Sammelstand 0.9.104: nur Deutsch, keine englischen Interna

        // Sammelstand 0.9.104: Ist der Ton-Thread beendet, werden Pause/Springen verworfen – sie wirken nie auf den nächsten Titel
        var done = new Thread(() => { }); done.Start(); done.Join();
        t.GetField("_thread", F)!.SetValue(a, done);
        a.SetPause(true); a.Seek(1.0);
        var queue = (System.Collections.Concurrent.ConcurrentQueue<Action>)t.GetField("_cmd", F)!.GetValue(a)!;
        Assert.Empty(queue);
        t.GetField("_thread", F)!.SetValue(a, null);
    }

    [AvaloniaFact(DisplayName = "Sammelstand 0.9.104: Bildschirmfoto im Testmodus nimmt nie den echten Bildschirm auf; „Bereich wählen“ zeigt sofort einen Hinweis im Hauptfenster")]
    public void Test_mode_never_grabs_real_screen()
    {
        var (w, _, root) = Ui.Open();
        w.ShotHideDelayMs = 0; // kein ScreenGrabOverride: der eingebaute Weg des Testmodus
        w.Show("alltag-bilder"); Dispatcher.UIThread.RunJobs();
        var task = w.ShotAsync("alltag-bilder", pick: false); Pump(200);
        var png = task.GetAwaiter().GetResult() ?? throw new Xunit.Sdk.XunitException("Kein Bildschirmfoto gespeichert");
        Assert.StartsWith(Path.Combine(root), png);
        using (var bmp = SKBitmap.Decode(png))
        {
            Assert.Equal((640, 400), (bmp.Width, bmp.Height)); // neutrales Testbild, nicht der Desktop
            Assert.All(new[] { bmp.GetPixel(0, 0), bmp.GetPixel(320, 200), bmp.GetPixel(639, 399) }, c => Assert.Equal((0x40, 0x40, 0x40), ((int)c.Red, (int)c.Green, (int)c.Blue)));
        }
        w.Find<Button>("shot-area").Press(); Pump(100);
        Assert.NotNull(w.ScreenPickerOpen);
        Assert.Contains(w.GetLogicalDescendants().OfType<TextBlock>(), x => (x.Text ?? "").Contains("Bereich mit der Maus aufziehen"));
        w.ScreenPickerOpen!.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); Pump(100);
        Assert.Null(w.ScreenPickerOpen);
    }

    [Fact(DisplayName = "T-0731 Gegenprüfung (echt, Windows-Tonausgabe): beschädigte MP3 über die Wiedergabe – Ton-Thread endet sauber mit Meldung, Prozess lebt, Gerät danach wieder nutzbar")]
    public async Task Real_corrupt_playback()
    {
        if (!WinAudio.Available) { Assert.Skip("Übersprungen – auf diesem Rechner gibt es keine Windows-Tonausgabe."); return; }
        var f = FirstBrokenMp3(Temp());
        var crashed = 0; UnhandledExceptionEventHandler watch = (_, _) => Interlocked.Increment(ref crashed);
        AppDomain.CurrentDomain.UnhandledException += watch;
        try
        {
            using var a = new WinAudio();
            string? failed = null; var ended = 0;
            a.Failed += m => failed = m; a.Ended += () => Interlocked.Increment(ref ended);
            a.SetVolume(5); a.Load(f);
            for (var i = 0; i < 240 && failed is null; i++) await Task.Delay(25);
            Assert.NotNull(failed); Assert.Contains("beschädigt", failed); Assert.Contains("Titel ist beendet", failed);
            Assert.Equal(0, ended); Assert.Equal(0, crashed);
            Assert.Equal(0, a.Rate); // Quelle geschlossen, Gerät freigegeben
            // Danach spielt dasselbe Objekt eine heile Datei wieder normal (Gerät wurde sauber geschlossen)
            a.Load(Mp3()); await Task.Delay(400);
            var after = a.DeviceSamplesPlayed;
            Assert.True(after > 0, "Nach dem Fehler spielt das Gerät nicht mehr");
            a.Stop();
            if (Beleg() is { } bl) File.WriteAllText(Path.Combine(bl, "gegenpruefung-kaputte-mp3-echt.log"),
                $"Datei: {Path.GetFileName(f)} (erste beschädigte aus dem festen Muster)\nMeldung: {failed}\nTitelende-Ereignisse: {ended}, unbehandelte Ausnahmen: {crashed}\nDanach heile MP3 auf demselben Gerät: {after} Werte in 0,4 s gespielt\n");
        }
        finally { AppDomain.CurrentDomain.UnhandledException -= watch; }
    }

    [AvaloniaFact(DisplayName = "T-0731 Gegenprüfung (UI): beschädigte MP3 im Musik-Player – Meldung „beschädigt … Titel ist beendet“ in der Leiste, Wiedergabe steht, Trendo läuft weiter")]
    public void Player_corrupt_mp3()
    {
        if (!WinAudio.Available) { Assert.Skip("Übersprungen – auf diesem Rechner gibt es keine Windows-Tonausgabe."); return; }
        var (w, _, root) = Ui.Open();
        var dir = Path.Combine(root, "musik"); Directory.CreateDirectory(dir);
        var bad = FirstBrokenMp3(dir);
        w.MusicFoldersOverride = [dir];
        w.Show("medien"); Dispatcher.UIThread.RunJobs(); w.Find<Button>("media-tab-musik").Press();
        for (var i = 0; i < 200 && w.MusicTracks.Count < 1; i++) Pump(25);
        w.MusicPlay([bad]); Dispatcher.UIThread.RunJobs();
        Assert.Null(w.MusicBlocked); // öffnen ging – der Fehler kommt erst beim Dekodieren
        for (var i = 0; i < 160 && w.MusicFailed is null; i++) Pump(25);
        Assert.NotNull(w.MusicFailed); Assert.Contains("beschädigt", w.MusicFailed); Assert.Contains(Path.GetFileName(bad), w.MusicFailed);
        Assert.True(w.MusicPaused);
        Assert.StartsWith("⚠", w.Find<TextBlock>("music-failed").Text);
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "musik.fehler");
        // Nächster Titel startet normal und räumt die Meldung weg
        var good = Path.Combine(dir, "heil.mp3"); File.Copy(Mp3(), good);
        w.MusicPlay([good]); Dispatcher.UIThread.RunJobs();
        Assert.Null(w.MusicFailed); Assert.False(w.MusicPaused); Assert.Null(w.TryFind<TextBlock>("music-failed"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0731 Gegenprüfung (UI): Mediathek-Karte spielt MP3 eingebaut statt mpv/VLC, „Musik anhalten“ stoppt; Text nennt Video ehrlich als fremdes Programm")]
    public void Library_card_plays_builtin()
    {
        if (!WinAudio.Available) { Assert.Skip("Übersprungen – auf diesem Rechner gibt es keine Windows-Tonausgabe."); return; }
        var (w, _, root) = Ui.Open();
        var mp3 = Path.Combine(root, "lied.mp3"); File.Copy(Mp3(), mp3);
        w.Show("alltag-bilder"); Dispatcher.UIThread.RunJobs();
        var texts = string.Join("|", w.GetLogicalTexts());
        Assert.Contains("MP3 und WAV eingebaut", texts); Assert.Contains("Videos und übrige Musik öffnen im Player mpv oder VLC", texts);
        Assert.DoesNotContain("Klick spielt mit mpv oder VLC ab", texts);
        w.LibraryPlay(mp3); Pump(300);
        Assert.Equal(mp3, w.SidePlaying); Assert.Equal("eingebaut", w.SideAudioRoute);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Spielt in Trendo: lied.mp3"));
        Assert.Empty(Process.GetProcessesByName("mpv")); Assert.Empty(Process.GetProcessesByName("vlc"));
        w.Find<Button>("lib-stop").Press(); Pump(50);
        Assert.Null(w.SidePlaying);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0731 Gegenprüfung (UI): Wecker klingelt mit dem eingebauten Weg (Weckton-WAV über die Windows-Tonausgabe), ohne mpv")]
    public void Alarm_rings_builtin()
    {
        if (!WinAudio.Available) { Assert.Skip("Übersprungen – auf diesem Rechner gibt es keine Windows-Tonausgabe."); return; }
        var (w, _, _) = Ui.Open();
        w.AlarmVolume = 5;
        w.Wecker.Add(6, 30, "Aufstehen", 0);
        var due = w.CheckAlarms(DateTime.Today.AddHours(6).AddMinutes(30).AddSeconds(3)); Pump(200);
        Assert.Single(due);
        Assert.Equal("eingebaut", w.SideAudioRoute);
        Assert.EndsWith("weckton.wav", w.SidePlaying);
        Assert.Empty(Process.GetProcessesByName("mpv"));
        w.Close();
    }

    static void Pump(int ms)
    {
        var until = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact(DisplayName = "T-0731 (UI): Musik ohne mpv – MP3 spielt eingebaut bis zum Titelende; FLAC ohne Player meldet BLOCKIERT mit „Player einrichten“")]
    public void Player_without_mpv()
    {
        var (w, _, root) = Ui.Open();
        var dir = Path.Combine(root, "musik"); Directory.CreateDirectory(dir);
        var mp3 = Path.Combine(dir, "ton.mp3"); File.Copy(Mp3(), mp3); var flac = Path.Combine(dir, "lied.flac"); File.WriteAllBytes(flac, [0x66, 0x4C, 0x61, 0x43]);
        w.MusicFoldersOverride = [dir];
        w.Show("medien"); Dispatcher.UIThread.RunJobs(); w.Find<Button>("media-tab-musik").Press();
        for (var i = 0; i < 200 && w.MusicTracks.Count < 2; i++) Pump(25); // Bibliothek liest im Hintergrund ein
        Assert.Equal(2, w.MusicTracks.Count);
        var noMpv = w.TryFind<Button>("music-setup") is not null; // ohne mpv zeigt die Leiste „Mehr Formate (Player)“
        if (WinAudio.Available)
        {
            w.MusicPlay([mp3]); Dispatcher.UIThread.RunJobs();
            Assert.Null(w.MusicBlocked); Assert.False(w.MusicPaused);
            Pump(800);
            Assert.False(w.MusicPaused);
            var t = w.Find<TextBlock>("music-duration").Text; Assert.Equal("0:02", t);
            Pump(2400); // Titelende → Wiedergabe steht, Leiste zeigt wieder „▶“
            Assert.True(w.MusicPaused, "Nach dem Titelende muss die Wiedergabe stehen (Ende vom Gerät gemeldet)");
        }
        if (noMpv)
        {
            w.MusicPlay([flac]); Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.MusicBlocked); Assert.Contains("Player einrichten", w.MusicBlocked);
            Assert.StartsWith("BLOCKIERT", w.Find<TextBlock>("music-blocked").Text);
            Assert.Equal("Player einrichten", (w.Find<Button>("music-setup").Content as string) ?? FirstText(w.Find<Button>("music-setup")));
        }
        w.Close();
    }

    static string FirstText(Button b) => b.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";

    [AvaloniaFact(DisplayName = "T-0731 (UI): „Bereich wählen“ – Maus zieht einen Bereich auf, die PNG enthält genau diesen Ausschnitt; Esc bricht ab")]
    public void Area_pick_with_mouse()
    {
        var (w, _, root) = Ui.Open();
        // Desktop-Ersatz NUR für den Inhalt (der echte Desktop folgt im Echtlauf unten); Auswahl, Umrechnung, Speichern sind echt.
        const int W = 400, H = 300; var px = new byte[W * H * 4];
        for (var y = 0; y < H; y++) for (var x = 0; x < W; x++)
        {
            var o = (y * W + x) * 4; var red = x >= 100 && x < 200 && y >= 80 && y < 160;
            px[o] = (byte)(red ? 30 : 200); px[o + 1] = (byte)(red ? 40 : 200); px[o + 2] = (byte)(red ? 220 : 200); px[o + 3] = 255;
        }
        w.ScreenGrabOverride = () => new ScreenGrab.Shot(px, W, H, 0, 0); w.ShotHideDelayMs = 0;
        w.Show("alltag-bilder"); Dispatcher.UIThread.RunJobs();
        w.Find<Button>("shot-area").Press(); Pump(100);
        var picker = w.ScreenPickerOpen ?? throw new Xunit.Sdk.XunitException("Bereichsauswahl hat sich nicht geöffnet");
        Pump(50);
        picker.MouseDown(new Point(100, 80), MouseButton.Left);
        for (var i = 1; i <= 5; i++) { picker.MouseMove(new Point(100 + 20 * i, 80 + 16 * i)); Dispatcher.UIThread.RunJobs(); }
        Assert.Equal("100 × 80 Pixel", picker.Find<TextBlock>("pick-size").Text);
        picker.MouseUp(new Point(200, 160), MouseButton.Left); Pump(100);
        var png = w.LastScreenshot ?? throw new Xunit.Sdk.XunitException("Keine Datei gespeichert");
        Assert.StartsWith(Path.Combine(root), png);
        using (var bmp = SKBitmap.Decode(png))
        {
            Assert.Equal((100, 80), (bmp.Width, bmp.Height));
            var c = bmp.GetPixel(50, 40); Assert.Equal((220, 40, 30), (c.Red, c.Green, c.Blue));
        }
        // Esc bricht ab: keine neue Datei
        w.Find<Button>("shot-area").Press(); Pump(100);
        var p2 = w.ScreenPickerOpen!; p2.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); Pump(100);
        Assert.Null(w.ScreenPickerOpen); Assert.Equal(png, w.LastScreenshot);
        Assert.Equal(new PixelRect(10, 20, 50, 60), ScreenAreaPicker.Map(new Point(30, 40), new Point(5, 10), new Size(200, 150), new PixelSize(400, 300)));
        Assert.Null(ScreenAreaPicker.Map(new Point(5, 5), new Point(6, 6), new Size(200, 150), new PixelSize(400, 300)));
        w.Close();
    }

    [Fact(DisplayName = "T-0731: Bildschirmfoto ohne ffmpeg (echt, GDI) – nimmt ein fremdes Fenster eines anderen Prozesses auf, PNG enthält dessen Pixel")]
    public void Real_screen_grab_foreign_window()
    {
        if (!ScreenGrab.Available) { Assert.Skip("Übersprungen – das eingebaute Bildschirmfoto gibt es nur unter Windows."); return; }
        // Fremdes Fenster: eigener PowerShell-Prozess mit Windows Forms, randlos, immer oben, Gold (212,168,75) mit Aufschrift.
        const int X = 240, Y = 240, W = 420, H = 200;
        var script = "Add-Type -AssemblyName System.Windows.Forms; Add-Type -AssemblyName System.Drawing; " +
            "$f = New-Object System.Windows.Forms.Form; $f.FormBorderStyle = 'None'; $f.StartPosition = 'Manual'; $f.TopMost = $true; " +
            $"$f.Location = New-Object System.Drawing.Point({X},{Y}); $f.Size = New-Object System.Drawing.Size({W},{H}); $f.BackColor = [System.Drawing.Color]::FromArgb(212,168,75); " +
            "$l = New-Object System.Windows.Forms.Label; $l.Text = 'Fremdes Testfenster T-0731'; $l.Font = New-Object System.Drawing.Font('Segoe UI', 16); $l.ForeColor = [System.Drawing.Color]::FromArgb(20,20,20); $l.AutoSize = $true; $l.Location = New-Object System.Drawing.Point(20,20); $f.Controls.Add($l); " +
            "[System.Windows.Forms.Application]::Run($f)";
        var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        try
        {
            ScreenGrab.Shot? shot = null; double gold = 0; var rect = default(PixelRect);
            for (var i = 0; i < 120; i++) // unter Last braucht PowerShell länger zum Zeichnen
            {
                Thread.Sleep(250); p.Refresh();
                if (p.MainWindowHandle == IntPtr.Zero) continue;
                rect = ScreenGrab.WindowRect(p.MainWindowHandle); // echte Bildschirmpixel (Anzeigeskalierung egal)
                if (rect.Width < 50) continue;
                shot = ScreenGrab.Area(rect);
                var n = 0; var hit = 0;
                for (var y = shot.Height / 2; y < shot.Height - 10; y += 4) for (var x = 10; x < shot.Width - 10; x += 4) { n++; var (r, g, b) = shot.At(x, y); if (Math.Abs(r - 212) < 8 && Math.Abs(g - 168) < 8 && Math.Abs(b - 75) < 8) hit++; }
                gold = hit / (double)n; if (gold > 0.95) break;
            }
            if (gold <= 0.95 && rect.Width > 0 && TopWindowAt(rect.X + rect.Width / 2, rect.Y + rect.Height * 3 / 4) is var top && top != p.MainWindowHandle)
            {   // Ehrlich statt rot: Ein anderes Fenster (z. B. ein Systemdialog mit Abdunklung) liegt über dem Testfenster – die Aufnahme zeigt dann zu Recht dieses
                Assert.Skip($"Übersprungen – das Testfenster ist von einem anderen Fenster verdeckt (oben liegt 0x{top:X}); der Bildschirm zeigt gerade etwas anderes.");
                return;
            }
            Assert.True(gold > 0.95, $"Nur {gold:P0} der Pixel zeigen das fremde Fenster – Bildschirm gesperrt? Fenster gefunden: {rect}");
            // Schrift des fremden Fensters ist dunkel und liegt oben links
            var dark = 0; for (var y = 0; y < shot!.Height / 2; y++) for (var x = 0; x < shot.Width * 3 / 4; x++) if (shot.At(x, y).R < 100) dark++;
            Assert.True(dark > 200, $"Aufschrift fehlt ({dark} dunkle Pixel)");
            var out1 = Path.Combine(Beleg() ?? Temp(), "bildschirmfoto-fremdes-fenster.png");
            ScreenGrab.SavePng(shot!, out1);
            using var bmp = SKBitmap.Decode(out1); Assert.Equal((rect.Width, rect.Height), (bmp.Width, bmp.Height));
            var c = bmp.GetPixel(bmp.Width / 2, bmp.Height - 10); Assert.Equal((212, 168, 75), (c.Red, c.Green, c.Blue));
            var desk = ScreenGrab.Desktop(); Assert.True(desk.Width >= rect.Width && desk.Height >= rect.Height);
            if (Beleg() is { } bd) File.WriteAllText(Path.Combine(bd, "bildschirmfoto-echt.log"),
                $"fremdes Fenster (PowerShell/Windows Forms, PID {p.Id}) angefordert bei {X},{Y} {W}×{H}, in Bildschirmpixeln {rect}\nAnteil Gold-Pixel: {gold:P1}\ndunkle Schriftpixel: {dark}\nDesktop gesamt: {desk.Width}×{desk.Height} ab {desk.Left},{desk.Top}\nPNG: {Path.GetFileName(out1)} {bmp.Width}×{bmp.Height}\n");
        }
        finally { try { p.Kill(true); } catch (InvalidOperationException) { } }
    }

    /// <summary>Oberstes Fenster an einem Bildschirmpunkt (echte Pixel).</summary>
    static IntPtr TopWindowAt(int x, int y)
    {
        var old = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try { var h = WindowFromPoint(new POINT { X = x, Y = y }); return h == IntPtr.Zero ? h : GetAncestor(h, 2 /* GA_ROOT */); }
        finally { if (old != IntPtr.Zero) SetThreadDpiAwarenessContext(old); }
    }
    struct POINT { public int X, Y; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
}
