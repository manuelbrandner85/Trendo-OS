using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Audio-Editor – selbst nachgebaut statt Audacity (Nachbau-Plan, freigegeben 04.11.2026).
/// Datei öffnen, Bereich mit der Maus wählen, schneiden/kopieren/einfügen, Effekte, anhören, als WAV (und mit FFmpeg als MP3/FLAC/OGG) speichern.
/// Jeder Schritt ist im Rückgängig der Programm-Hülle; die Originaldatei bleibt unverändert.</summary>
public partial class MainWindow
{
    const string AUK = "ton";
    readonly Dictionary<string, (double A, double B)> _auSel = [];
    string? _auClip; DispatcherTimer? _auTimer; double _auPlayFrom; WaveView? _auWave;

    /// <summary>Auswahl der aktiven Spur in Sekunden (a == b: nur Marke). Öffentlich für Tests und die KI.</summary>
    public (double A, double B) AuSel
    {
        get => ProgramActive(AUK) is { } d ? _auSel.GetValueOrDefault(d.Id) : default;
        set { if (ProgramActive(AUK) is { } d) _auSel[d.Id] = (Math.Max(0, Math.Min(value.A, value.B)), Math.Max(0, Math.Max(value.A, value.B))); }
    }
    public WaveView? AuWaveForTests => _auWave;

    static string AuTemp() { var d = Path.Combine(Path.GetTempPath(), $"trendo-audio-{Environment.ProcessId}"); Directory.CreateDirectory(d); return d; }

    private void InitAudioEditor() => RegisterProgram(KreativTabs, new ProgramSpec(AUK, "CORE-08", "Audio bearbeiten")
    {
        Sentence = "Aufnahmen und Musik schneiden, verbessern und speichern – wie ein Tonstudio, direkt in Trendo.",
        FillHeight = true,
        Folder = "Medien",
        GroupLevels = new() { ["Filter"] = 2 },
        CmdLevels = new() { ["au-spectral"] = 1, ["au-mixdown-cmd"] = 1, ["au-echo"] = 2, ["au-rev"] = 2, ["au-compress"] = 2, ["au-loud-music"] = 1, ["au-dup"] = 1, ["au-silence"] = 1 }, // Profi zeigt alles – Erweitert bleibt übersichtlich
        Templates =
        [
            new("leer", "Tondatei öffnen", "WAV oder MP3 rechts wählen", () => AudioEdit.New(null, 0)),
            new("stille", "Leere Spur (30 s)", "Zum Zusammensetzen aus mehreren Dateien", () => AudioEdit.New(null, 30)),
            new("mehrspur", "Mehrspur-Projekt", "Stimme, Musik und Geräusche auf eigenen Spuren mischen", AudioMulti.New),
        ],
        Ribbon =
        [
            ("Bearbeiten", [
                new ProgramCmd("au-cut", "✂ Ausschneiden", d => { AuCopy(d); AuOp(d, "cut", true); }, Tip: "Auswahl entfernen und merken"),
                new ProgramCmd("au-copy", "⧉ Kopieren", d => AuCopy(d), Tip: "Auswahl merken"),
                new ProgramCmd("au-paste", "📋 Einfügen", d => AuPaste(d), Tip: "Gemerkten Ton an der Marke einfügen"),
                new ProgramCmd("au-del", "Löschen", d => AuOp(d, "cut", true)),
                new ProgramCmd("au-keep", "Zuschneiden", d => AuOp(d, "keep", true), Tip: "Nur die Auswahl behalten"),
                new ProgramCmd("au-silence", "Stille", d => AuOp(d, "silence", true)),
                new ProgramCmd("au-dup", "Duplizieren", d => AuOp(d, "duplicate", true)),
            ]),
            ("Effekte", [
                new ProgramCmd("au-fadein", "◢ Einblenden", d => AuOp(d, "fade-in", fallback: (0, 2)), Tip: "Ohne Auswahl: die ersten 2 Sekunden"),
                new ProgramCmd("au-fadeout", "◣ Ausblenden", d => AuOp(d, "fade-out", fallback: (-2, 0)), Tip: "Ohne Auswahl: die letzten 2 Sekunden"),
                new ProgramCmd("au-louder", "🔊 Lauter", d => AuOp(d, "gain", v: 3), Tip: "+3 dB (Auswahl oder alles)"),
                new ProgramCmd("au-quieter", "🔉 Leiser", d => AuOp(d, "gain", v: -3), Tip: "−3 dB (Auswahl oder alles)"),
                new ProgramCmd("au-norm", "Normalisieren", d => AuOp(d, "normalize", v: -1), Primary: true, Tip: "Lautester Punkt auf −1 dB"),
                new ProgramCmd("au-gate", "Rauschen unterdrücken", d => AuOp(d, "gate", v: -45), Tip: "Leises Rauschen zwischen Wörtern absenken"),
                new ProgramCmd("au-echo", "Echo", d => AuOp(d, "echo", v: 0.3)),
                new ProgramCmd("au-rev", "Umkehren", d => AuOp(d, "reverse")),
            ]),
            ("Filter", [
                new ProgramCmd("au-hp", "Tiefen weg", d => AuOp(d, "highpass", v: 80), Tip: "Trittschall und Brummen unter 80 Hz"),
                new ProgramCmd("au-lp", "Höhen weg", d => AuOp(d, "lowpass", v: 8000), Tip: "Zischen über 8 kHz"),
                new ProgramCmd("au-mono", "Zu Mono", d => AuOp(d, "mono")),
                new ProgramCmd("au-faster", "Schneller", d => AuOp(d, "speed", v: 1.1), Tip: "+10 % Tempo (Tonhöhe steigt mit)"),
                new ProgramCmd("au-slower", "Langsamer", d => AuOp(d, "speed", v: 0.9), Tip: "−10 % Tempo"),
                new ProgramCmd("au-sil1", "1 s Stille einfügen", d => AuOp(d, "insert-silence", v: 1)),
            ]),
            ("Reparieren", [ // Vorbilder iZotope RX, Audacity, Audition (04.11.2026)
                new ProgramCmd("au-noise-profile", "Rauschprofil merken", _ => AuNoiseProfile(), Tip: "Erst ein Stück mit reinem Rauschen (ohne Stimme) wählen"),
                new ProgramCmd("au-denoise", "Rauschen entfernen", d => AuDenoise(d), Primary: true, Tip: "Mit dem gemerkten Profil – Auswahl oder alles"),
                new ProgramCmd("au-dehum", "Brummen weg (50 Hz)", d => AuOp(d, "dehum", v: 50), Tip: "Netzbrummen und Oberwellen"),
                new ProgramCmd("au-truncate", "Stille kürzen", d => AuOp(d, "truncate", v: -45), Tip: "Pausen über 0,6 s auf 0,3 s"),
                new ProgramCmd("au-loud-podcast", "Lautheit Sprache (−16 LUFS)", d => AuOp(d, "loudness", v: -16)),
                new ProgramCmd("au-loud-music", "Lautheit Musik (−14 LUFS)", d => AuOp(d, "loudness", v: -14)),
                new ProgramCmd("au-compress", "Kompressor", d => AuOp(d, "compress", v: -20), Tip: "Leise und laute Stellen näher zusammen"),
                new ProgramCmd("au-spectral", "Spektral absenken", d => AuSpectral(d, -30), Tip: "Im Spektrogramm ein Rechteck um Husten, Pfeifton oder Klingeln ziehen – nur das wird leiser"),
            ]),
            ("Abspielen", [
                new ProgramCmd("au-play", "▶ Abspielen", d => AuPlay(d), Primary: true, Tip: "Auswahl – oder ab der Marke"),
                new ProgramCmd("au-stop", "■ Stopp", _ => AuStop()),
                new ProgramCmd("au-all", "Alles wählen", d => AuSel = (0, AudioEdit.Render(d.Data).Seconds)),
                new ProgramCmd("au-none", "Auswahl aufheben", _ => AuSel = default),
                new ProgramCmd("au-spectro", "Spektrogramm", _ => AuSpectro = !AuSpectro, Tip: "Frequenzen über die Zeit – Störgeräusche sieht man sofort"),
                new ProgramCmd("au-record", "⏺ Aufnehmen (10 s)", d => _ = AuRecord(d, 10), Tip: "Mikrofon an der Marke einfügen"),
                new ProgramCmd("au-mixdown-cmd", "⤓ Abmischen", d => AuMixdown(d), Tip: "Mehrspur-Projekt zu einer Datei abmischen"),
            ]),
            ("Prüfen", [ // Paket 01 S4-20
                new ProgramCmd("au-qa-cmd", "✅ Prüfen", d => AuQa(d), Tip: "Lautheit, True Peak, Übersteuerung, Format, Phase, Herkunft – rechts das Ziel wählen"),
                new ProgramCmd("au-release-cmd", "📦 Freigeben", d => AuRelease(d), Tip: "Nur ohne Rot: WAV 24 Bit und Prüfbericht mit Prüfsumme"),
            ]),
        ],
        Exports =
        [
            new("wav", "WAV (16 Bit)", ".wav", (d, f) => { AudioFile.WriteWav(AudioEdit.Render(d.Data), f); return f; }),
            new("wav24", "WAV (24 Bit, Studio)", ".wav", (d, f) => { AudioFile.WriteWav(AudioEdit.Render(d.Data), f, 24); return f; }),
            new("mp3", "MP3", ".mp3", (d, f) => AuFfmpeg(d, f, ["-c:a", "libmp3lame", "-q:a", "2"])),
            new("flac", "FLAC (verlustfrei)", ".flac", (d, f) => AuFfmpeg(d, f, ["-c:a", "flac"])),
            new("ogg", "OGG", ".ogg", (d, f) => AuFfmpeg(d, f, ["-c:a", "libvorbis", "-q:a", "6"])),
        ],
        Workspace = AuWorkspace,
        Properties = AuProperties,
        Status = d => { var a = AuRendered(d); var (s, e) = AuSel; return a is null ? "Keine Tondatei" : $"{AudioEdit.Time(a.Seconds)} · {a.Rate} Hz · {(a.Channels == 1 ? "Mono" : "Stereo")} · " + (e > s ? $"Auswahl {AudioEdit.Time(s)}–{AudioEdit.Time(e)} ({e - s:0.00} s)" : $"Marke {AudioEdit.Time(s)}"); },
        Help = "Rechts eine WAV- oder MP3-Datei öffnen. In der Wellenform mit der Maus einen Bereich ziehen – oder klicken, um die Marke zu setzen. Oben schneiden, Effekte anwenden, ▶ zum Anhören. Ohne Auswahl wirken Effekte auf die ganze Spur. Exportieren speichert das Ergebnis; das Original bleibt unverändert.",
    }, "audio bearbeiten", "ton bearbeiten", "tonstudio", "aufnahme schneiden", "wav", "mp3 schneiden", "rauschen entfernen", "normalisieren", "podcast", "audacity");

    /// <summary>Spektrogramm statt Wellenform zeigen (Audition „Spektral-Ansicht“).</summary>
    public bool AuSpectro { get; set; }
    readonly Dictionary<string, (double A, double B)> _auNoise = [];
    float[,]? _auSpecCache; string? _auSpecKey;

    /// <summary>Gewählten Bereich als Rauschprofil merken (Audacity „Rauschprofil ermitteln“).</summary>
    public void AuNoiseProfile()
    {
        var d = ProgramActive(AUK) ?? throw new InvalidOperationException("Erst eine Tondatei öffnen.");
        var (s, e) = AuSel; if (e - s < 0.1) throw new InvalidOperationException("Bitte zuerst mindestens 0,1 s mit reinem Rauschen (ohne Stimme) wählen.");
        _auNoise[d.Id] = (s, e); _progMsg[AUK] = $"Rauschprofil gemerkt ({AudioEdit.Time(s)}–{AudioEdit.Time(e)}). Jetzt „Rauschen entfernen“ – mit Auswahl oder für alles.";
    }

    void AuDenoise(ProgramDocument d)
    {
        if (!_auNoise.TryGetValue(d.Id, out var np)) throw new InvalidOperationException("Zuerst ein Stück mit reinem Rauschen wählen und „Rauschprofil merken“.");
        var (s, e) = AuSel;
        d.Edit(AudioEdit.Label("denoise"), x => AudioEdit.Add(x, AudioEdit.Op("denoise", e > s ? s : 0, e > s ? e : 0, 18, null, np.A, np.B)));
        _progMsg[AUK] = "Rauschen entfernt (18 dB) – Strg+Z macht es rückgängig.";
    }

    /// <summary>Mikrofon aufnehmen und an der Marke einfügen.</summary>
    public async Task AuRecord(ProgramDocument d, int seconds)
    {
        _progMsg[AUK] = $"⏺ Aufnahme läuft ({seconds} s) …"; Show(AUK);
        var wav = await RecordMic(seconds);
        if (wav is null) { _progMsg[AUK] = _msMsg.Length > 0 ? _msMsg : "Aufnahme ging nicht."; Show(AUK); return; }
        var dir = Path.Combine(WorkFolder("Medien"), "Aufnahmen"); Directory.CreateDirectory(dir);
        var keep = Path.Combine(dir, $"Aufnahme {DateTime.Now:yyyy-MM-dd HH-mm-ss}.wav"); File.Move(wav, keep);
        if (AuRendered(d) is null) AuOpen(keep); else AuInsertFile(keep);
        Show(AUK);
    }

    AudioBuf? AuRendered(ProgramDocument d)
    {
        if (d.Text("source").Length == 0 && d.Number("length") <= 0 && d.List("ops").Count == 0 && (!AudioMulti.IsMulti(d.Data) || AudioMulti.Tracks(d.Data).Count == 0)) return null;
        try { return AudioEdit.Render(d.Data); } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { _progMsg[AUK] = e.Message; return null; }
    }

    /// <summary>Tondatei öffnen (öffentlich für Tests und die KI). Ist die aktive Spur leer, wird sie benutzt, sonst entsteht eine neue.</summary>
    public ProgramDocument AuOpen(string file)
    {
        file = file.Trim().Trim('"');
        if (ProgramActive(AUK) is { } mp && AudioMulti.IsMulti(mp.Data)) { AuAddTrack(file); return mp; } // im Mehrspur-Projekt wird jede Datei eine Spur
        var probe = AudioFile.Read(file); // wirft mit deutscher Meldung bei falschem Format
        var d = ProgramActive(AUK) is { } cur && AuRendered(cur) is null ? cur : ProgramNew(AUK, "leer");
        d.Edit("Datei öffnen", x => { x["source"] = Path.GetFullPath(file); x["length"] = 0; x["ops"] = new JsonArray(); });
        if (d.Title.StartsWith("Tondatei", StringComparison.Ordinal) || d.Title.Length == 0 || d.Title.StartsWith("Unbenannt", StringComparison.Ordinal)) d.Title = Path.GetFileNameWithoutExtension(file);
        _auSel[d.Id] = default;
        _progMsg[AUK] = $"„{Path.GetFileName(file)}“ geöffnet: {AudioEdit.Time(probe.Seconds)}, {probe.Rate} Hz, {(probe.Channels == 1 ? "Mono" : "Stereo")}.";
        return d;
    }

    /// <summary>Schritt auf die Auswahl anwenden. needsSel: ohne Auswahl Fehlermeldung. fallback: Bereich ohne Auswahl (negativ = vom Ende).</summary>
    void AuOp(ProgramDocument d, string op, bool needsSel = false, double v = 0, (double A, double B)? fallback = null)
    {
        var a = AuRendered(d) ?? throw new InvalidOperationException("Erst eine Tondatei öffnen.");
        var (s, e) = AuSel;
        if (e <= s)
        {
            if (needsSel) throw new InvalidOperationException("Erst in der Wellenform einen Bereich mit der Maus ziehen.");
            if (fallback is { } f) { s = f.A < 0 ? Math.Max(0, a.Seconds + f.A) : f.A; e = f.B <= 0 ? a.Seconds : Math.Min(a.Seconds, f.B); }
            if (op is "insert-silence") { e = s; }
        }
        if (op is "insert-silence") { var at = s; d.Edit(AudioEdit.Label(op), x => AudioEdit.Add(x, AudioEdit.Op(op, at, at, v))); _progMsg[AUK] = $"{v:0.#} s Stille bei {AudioEdit.Time(at)} eingefügt."; return; }
        var (ss, ee) = (s, e);
        d.Edit(AudioEdit.Label(op), x => AudioEdit.Add(x, AudioEdit.Op(op, ss, ee, v)));
        if (op is "cut" or "keep") AuSel = op == "cut" ? (ss, ss) : (0, ee - ss);
        else if (op == "duplicate") AuSel = (ee, ee + (ee - ss));
        var after = AudioEdit.Render(d.Data);
        _progMsg[AUK] = $"{AudioEdit.Label(op)}" + (ee > ss ? $" ({AudioEdit.Time(ss)}–{AudioEdit.Time(ee)})" : " (ganze Spur)") + $" · jetzt {AudioEdit.Time(after.Seconds)}, Spitze {AuDb(AudioEdit.PeakDb(after))}.";
    }

    static string AuDb(double db) => double.IsNegativeInfinity(db) ? "Stille" : $"{db:0.0} dB";

    void AuCopy(ProgramDocument d)
    {
        var a = AuRendered(d) ?? throw new InvalidOperationException("Erst eine Tondatei öffnen.");
        var (s, e) = AuSel; if (e <= s) throw new InvalidOperationException("Erst einen Bereich wählen.");
        var f = Path.Combine(AuTemp(), $"clip-{Guid.NewGuid():N}.wav");
        AudioFile.WriteWav(a.Slice((int)(s * a.Rate), (int)(e * a.Rate)), f, 24);
        _auClip = f; _progMsg[AUK] = $"{e - s:0.00} s gemerkt – mit „Einfügen“ an der Marke einsetzen.";
    }

    void AuPaste(ProgramDocument d)
    {
        if (_auClip is null || !File.Exists(_auClip)) throw new InvalidOperationException("Noch nichts kopiert.");
        var at = AuSel.A; var clip = _auClip; var len = AudioFile.Read(clip).Seconds;
        d.Edit("Einfügen", x => AudioEdit.Add(x, AudioEdit.Op("insert", at, at, 0, clip)));
        AuSel = (at, at + len); _progMsg[AUK] = $"{len:0.00} s bei {AudioEdit.Time(at)} eingefügt.";
    }

    /// <summary>Weitere Datei an der Marke einfügen (öffentlich für Tests).</summary>
    public void AuInsertFile(string file)
    {
        var d = ProgramActive(AUK) ?? throw new InvalidOperationException("Erst eine Tondatei öffnen.");
        file = Path.GetFullPath(file.Trim().Trim('"')); var len = AudioFile.Read(file).Seconds; var at = AuSel.A;
        d.Edit("Datei einfügen", x => AudioEdit.Add(x, AudioEdit.Op("insert", at, at, 0, file)));
        AuSel = (at, at + len); _progMsg[AUK] = $"„{Path.GetFileName(file)}“ ({AudioEdit.Time(len)}) bei {AudioEdit.Time(at)} eingefügt.";
    }

    void AuPlay(ProgramDocument d)
    {
        var a = AuRendered(d) ?? throw new InvalidOperationException("Erst eine Tondatei öffnen.");
        var (s, e) = AuSel; if (e <= s) e = a.Seconds; if (s >= a.Seconds) s = 0;
        var f = Path.Combine(AuTemp(), "anhoeren.wav");
        AudioFile.WriteWav(a.Slice((int)(s * a.Rate), (int)(e * a.Rate)), f);
        var o = Audio() ?? throw new InvalidOperationException("Zum Anhören fehlt der Abspieler (mpv) – unter Werkzeuge mit einem Klick laden.");
        _auPlayFrom = s; _mPos = 0; o.Load(f);
        _auTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background, (_, _) =>
        {
            if (CurrentView != AUK) return;
            foreach (var l in _auLanes) { l.Playhead = _auPlayFrom + _mPos; l.InvalidateVisual(); }
            if (_auWave is null) return;
            _auWave.Playhead = _auPlayFrom + _mPos; _auWave.InvalidateVisual();
        });
        _auTimer.Start();
        _progMsg[AUK] = $"Spielt {AudioEdit.Time(s)}–{AudioEdit.Time(e)}.";
    }

    void AuStop()
    {
        _audio?.Stop(); _auTimer?.Stop();
        if (_auWave is not null) { _auWave.Playhead = -1; _auWave.InvalidateVisual(); }
        foreach (var l in _auLanes) { l.Playhead = -1; l.InvalidateVisual(); }
    }

    string AuFfmpeg(ProgramDocument d, string target, string[] codec)
    {
        var ff = FfmpegExe ?? throw new InvalidOperationException("Für MP3, FLAC und OGG braucht Trendo FFmpeg – unter Werkzeuge mit einem Klick laden. WAV geht immer.");
        var tmp = Path.Combine(AuTemp(), "export.wav"); AudioFile.WriteWav(AudioEdit.Render(d.Data), tmp, 24);
        var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var x in (string[])["-y", "-hide_banner", "-i", tmp, .. codec, target]) psi.ArgumentList.Add(x);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg startet nicht.");
        var err = p.StandardError.ReadToEnd(); p.WaitForExit();
        if (p.ExitCode != 0 || !File.Exists(target)) throw new InvalidOperationException("FFmpeg meldet einen Fehler: " + err.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault());
        return target;
    }

    private Control AuWorkspace(ProgramDocument d)
    {
        if (AudioMulti.IsMulti(d.Data) && !AuSpectro) return AuMultiWorkspace(d);
        _auLanes.Clear();
        var a = AuRendered(d);
        var (s, e) = AuSel;
        var (flo, fhi) = AuFreq;
        var wave = new WaveView { Name = "au-wave", Peaks = a is null ? [] : AudioEdit.Peaks(a, 1400), Seconds = a?.Seconds ?? 0, SelA = s, SelB = e, Rate = a?.Rate ?? 44100, FreqLo = flo, FreqHi = fhi };
        if (AuSpectro && a is not null)
        {
            var key = AudioEdit.Key(d.Data);
            if (_auSpecKey != key) { _auSpecCache = AudioDsp.Spectrogram(a, 600, 160); _auSpecKey = key; }
            wave.Spectro = _auSpecCache;
        }
        _auWave = wave;
        wave.Selected = (x, y) => { AuSel = (x, y); AuFreq = default; Show(AUK); };
        wave.SelectedBox = (x, y, f1, f2) => { AuSel = (x, y); AuFreq = (f1, f2); _progMsg[AUK] = $"Bereich {AudioEdit.Time(x)}–{AudioEdit.Time(y)} · {AuHz(f1)}–{AuHz(f2)} – rechts „Bereich absenken“."; Show(AUK); };
        // Transportleiste wie im Videoschnitt: groß, mittig, mit Zeitanzeige
        Button T(string name, string label, string tip, Action act, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 14, MinWidth = 44, Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(8) };
            if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
            ToolTip.SetTip(b, tip); b.Click += (_, _) => ProgramRun(AUK, act); return b;
        }
        // DAW-Stil (Wunsch 06.11.2026 „Design je Programm“): Transport oben mit Anzeigefeldern wie in Audition/Bitwig
        var bar = new StackPanel { Name = "au-transport", Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        bar.Children.Add(T("au-tp-start", "⏮", "Marke an den Anfang", () => AuSel = default));
        bar.Children.Add(T("au-tp-play", "▶", "Abspielen (Auswahl oder ab Marke)", () => AuPlay(d), true));
        bar.Children.Add(T("au-tp-stop", "■", "Stopp", AuStop));
        bar.Children.Add(T("au-tp-end", "⏭", "Marke ans Ende", () => AuSel = (a?.Seconds ?? 0, a?.Seconds ?? 0)));
        Border Lcd(string name, string label, string value, double w, TextBlock? tb = null) => new() { Name = name, MinWidth = w, Height = 40, CornerRadius = new CornerRadius(6), Background = DawLcd, BorderBrush = DawEdge, BorderThickness = new Thickness(1), Padding = new Thickness(8, 2),
            Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 9.5 },
                tb ?? new TextBlock { Text = value, Foreground = DawDigits, FontSize = 14, FontFamily = new FontFamily("Consolas, monospace"), FontWeight = FontWeight.SemiBold } } } };
        var time = new TextBlock { Name = "au-time", Text = a is null ? "–" : e > s ? $"{AudioEdit.Time(s)} – {AudioEdit.Time(e)}  ·  {e - s:0.00} s" : $"{AudioEdit.Time(s)} / {AudioEdit.Time(a.Seconds)}",
            Foreground = DawDigits, FontSize = 14, FontFamily = new FontFamily("Consolas, monospace"), FontWeight = FontWeight.SemiBold };
        var lcds = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            Lcd("au-lcd-pos", e > s ? "AUSWAHL" : "MARKE / LÄNGE", "", 230, time),
            Lcd("au-lcd-format", "FORMAT", a is null ? "–" : $"{a.Rate / 1000.0:0.#} kHz · {(a.Channels == 1 ? "Mono" : "Stereo")}", 130),
            Lcd("au-lcd-peak", "SPITZE", a is null ? "–" : AuDb(AudioEdit.PeakDb(a)), 90),
            Lcd("au-lcd-steps", "SCHRITTE", d.List("ops").Count.ToString(), 70) } };
        var head = new Border { Name = "au-daw-bar", CornerRadius = new CornerRadius(10), Background = DawPanel, BorderBrush = DawEdge, BorderThickness = new Thickness(1), Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 0, 8),
            Child = new WrapPanel { Children = { bar, new Border { Width = 12 }, lcds } } };
        var frame = new Border { Child = wave, CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Brush.Parse("#33F5B83D"), BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 6 18 0 #66000000") };
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        g.Children.Add(head); Grid.SetRow(frame, 1); g.Children.Add(frame);
        return g;
    }

    private Control AuProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        Button B(string name, string label, Action act, bool primary = false) { var b = new Button { Name = name, Content = label, FontSize = 12, Margin = new Thickness(0, 0, 4, 4) }; if (primary) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => ProgramRun(AUK, act); return b; }
        var path = new TextBox { Name = "au-file", PlaceholderText = "Tondatei (WAV oder MP3)" };
        sp.Children.Add(H("Tondatei", 13)); sp.Children.Add(path);
        string Picked() => (path.Text ?? "").Trim().Trim('"') is { Length: > 0 } f ? f : throw new InvalidOperationException("Bitte eine Tondatei wählen.");
        if (AudioMulti.IsMulti(d.Data))
            sp.Children.Add(new WrapPanel { Children = { B("au-add-track", "Als neue Spur (an der Marke)", () => AuAddTrack(Picked()), true) } });
        else sp.Children.Add(new WrapPanel { Children = { B("au-open", "Öffnen", () => AuOpen(Picked()), true), B("au-insert", "An der Marke einfügen", () => AuInsertFile(Picked())) } });
        if (AuSpectralPanel(d) is { } spec) sp.Children.Add(spec);
        var a = AuRendered(d);
        if (a is not null)
        {
            var src = d.Text("source");
            sp.Children.Add(H("Spur", 13));
            sp.Children.Add(P((src.Length > 0 ? $"Quelle: {Path.GetFileName(src)}\n" : AudioMulti.IsMulti(d.Data) ? $"Summe aus {AudioMulti.Tracks(d.Data).Count} Spuren\n" : "Leere Spur\n") + $"Länge {AudioEdit.Time(a.Seconds)} · {a.Rate} Hz · {(a.Channels == 1 ? "Mono" : "Stereo")}\nLautester Punkt: {AuDb(AudioEdit.PeakDb(a))}", Muted, 12));
            // Auswahl genau eintippen
            var (s, e) = AuSel;
            sp.Children.Add(H("Auswahl (Sekunden)", 13));
            var fa = new NumericUpDown { Name = "au-sel-a", Value = (decimal)Math.Round(s, 2), Minimum = 0, Maximum = (decimal)Math.Ceiling(a.Seconds), Increment = 0.1m, FormatString = "0.00", Width = 110 };
            var fb = new NumericUpDown { Name = "au-sel-b", Value = (decimal)Math.Round(e, 2), Minimum = 0, Maximum = (decimal)Math.Ceiling(a.Seconds), Increment = 0.1m, FormatString = "0.00", Width = 110 };
            Control Row(string label, Control box) { var r = new DockPanel { Margin = new Thickness(0, 0, 0, 4) }; var t = new TextBlock { Text = label, Width = 34, VerticalAlignment = VerticalAlignment.Center, Foreground = Muted }; DockPanel.SetDock(t, Avalonia.Controls.Dock.Left); r.Children.Add(t); r.Children.Add(box); return r; }
            fa.Width = fb.Width = double.NaN; fa.HorizontalAlignment = fb.HorizontalAlignment = HorizontalAlignment.Stretch;
            sp.Children.Add(Row("von", fa)); sp.Children.Add(Row("bis", fb));
            sp.Children.Add(B("au-sel-set", "Übernehmen", () => AuSel = ((double)(fa.Value ?? 0), (double)(fb.Value ?? 0))));
            if (e > s) sp.Children.Add(P($"Spitze in der Auswahl: {AuDb(AudioEdit.PeakDb(a, s, e))}", Muted, 12));
        }
        // Verlauf der Schritte (nicht-zerstörend)
        var ops = d.List("ops").OfType<JsonObject>().ToList();
        sp.Children.Add(H($"Schritte ({ops.Count})", 13));
        if (ops.Count == 0) sp.Children.Add(P("Noch nichts bearbeitet. Strg+Z macht jeden Schritt rückgängig.", Muted, 12));
        foreach (var o in ops.TakeLast(12))
        {
            var op = o["op"].AsText() ?? ""; double oa = ProgramDocument.ToNumber(o["a"]), ob = ProgramDocument.ToNumber(o["b"]);
            sp.Children.Add(new TextBlock { Text = "• " + AudioEdit.Label(op) + (ob > oa ? $"  {AudioEdit.Time(oa)}–{AudioEdit.Time(ob)}" : ""), Foreground = Text, FontSize = 12 });
        }
        if (AuRendered(d) is not null) sp.Children.Add(AuQaPanel(d)); // Paket 01 S4-20: Prüfen und freigeben
        sp.Children.Add(AuFoleyPanel(d)); // Paket 01 S4-17: Geräusch-Werkstatt
        return sp;
    }
}
