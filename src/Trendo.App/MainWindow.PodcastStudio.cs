using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Media;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0697 (D4b): „Podcast aufnehmen“ nimmt selbst auf (Mikrofon über FFmpeg wie die übrige App), mehrere Sprecherspuren mit Wellenform,
/// Stellen löschen (Kapitel rücken mit), Pausen kürzen, Intro/Outro, Kapitelmarken, Lautheit −16 LUFS (zwei Durchgänge) und MP3 mit ID3-Tags und Kapiteln.
/// Der Feed (RSS) wird nur als Datei geschrieben – Trendo lädt nichts hoch.</summary>
public partial class MainWindow
{
    static JsonObject PodcDoc(string title, string desc, string chapters) => new() { ["title"] = title, ["description"] = desc, ["chapters"] = chapters, ["audio"] = "", ["show"] = "", ["author"] = "", ["baseUrl"] = "" };

    bool _podcBusy;
    /// <summary>Tests: Pfad zu FFmpeg für Aufnahme und „Folge fertig machen“ ersetzen; leer = fehlt (wie <see cref="LiveFfmpegPath"/>).</summary>
    public string? PodcFfmpegPath { get; set; }
    /// <summary>Tests: ersetzt das Mikrofon – liefert beim „Stopp“ die Aufnahme (WAV). Kein Ende-zu-Ende-Nachweis des Mikrofons.</summary>
    public Func<string?>? PodcMicOverride { get; set; }
    /// <summary>Markierte Stelle an der Welle (Sekunden); a == b ist nur die Marke.</summary>
    public (double A, double B) PodcSel { get; set; }
    public string? PodcLastOutput { get; private set; }
    public bool PodcRecording => _podcRec is not null || _podcFakeRec;
    Process? _podcRec; string? _podcRecFile; DateTime _podcRecStart; bool _podcFakeRec; DispatcherTimer? _podcRecTimer; readonly StringBuilder _podcRecErr = new(); TextBlock? _podcRecLabel;
    readonly Dictionary<string, (AudioBuf Buf, (float Min, float Max)[] Peaks)> _podcCache = [];

    string? PodcFf => PodcFfmpegPath is { } own ? (own.Length > 0 ? own : null) : FfmpegPath;

    private void InitPodcast() => RegisterProgram(FachTabs, new ProgramSpec(PodcKey, "STUDIO-PODCAST-01", "Podcast aufnehmen")
    {
        Sentence = "Folge aufnehmen, Versprecher und Pausen herausschneiden, Kapitel setzen, Lautheit angleichen – fertig als MP3 mit Kapiteln, dazu Shownotes und Feed.",
        Folder = "Podcast",
        ScrollWorkspace = true,
        FirstStep = new("Aufnahme starten", "Sprich ins Mikrofon – jede Aufnahme wird eine eigene Spur (Sprecher 1, Sprecher 2 …). Eine fertige Datei kannst du auch hineinziehen.",
            d => _ = PodcRecordStart(d), d => PodcastEdit.Tracks(d.Data).Count == 0 && !PodcRecording),
        Templates =
        [
            new("leer", "Neue Folge", "Aufnehmen, schneiden, fertig machen", () => PodcDoc("Folge 1", "", "00:00 Begrüßung\n")),
            new("interview", "Beispiel: Interview", "Vier Kapitel zum Ausprobieren", () => PodcDoc("Folge 7: Gärtnern auf dem Balkon", "Erika Muster erzählt, was auf zwei Quadratmetern wächst.", "00:00 Begrüßung\n02:15 Wie alles anfing\n14:40 Die besten Pflanzen\n31:05 Fragen der Hörer")),
        ],
        Ribbon =
        [
            ("Aufnahme", [
                new ProgramCmd("podc-record", "● Aufnehmen", d => _ = PodcRecordStart(d), Primary: true, Tip: "Neue Sprecherspur über das Mikrofon (R)"),
                new ProgramCmd("podc-stop", "■ Stopp", d => _ = PodcRecordStop(d), Tip: "Aufnahme beenden – sie wird als Spur angelegt (R)"),
                new ProgramCmd("podc-pick", "Aufnahme laden …", d => _ = PodcPick(d), Tip: "WAV oder MP3 als weitere Spur (M4A, OGG, FLAC mit FFmpeg). Du kannst Dateien auch hineinziehen."),
                new ProgramCmd("podc-play", "▶ Anhören", d => PodcPlay(d), Tip: "Markierte Stelle oder ab der Marke anhören (Leertaste)"),
            ]),
            ("Schnitt", [
                new ProgramCmd("podc-cut", "Stelle löschen", d => PodcCut(d), Tip: "Markierte Stelle aus allen Spuren entfernen – Kapitel dahinter rücken mit (Entf)"),
                new ProgramCmd("podc-pauses", "Pausen kürzen", d => PodcTrimPauses(d), Tip: "Pausen über 1 s auf 0,5 s kürzen"),
                new ProgramCmd("podc-chapter", "Kapitel an der Marke", d => PodcChapterHere(d), Tip: "Neues Kapitel an der Marke (K)"),
            ]),
            ("Musik", [
                new ProgramCmd("podc-intro", "Intro wählen …", d => _ = PodcPickMusic(d, "intro")),
                new ProgramCmd("podc-outro", "Outro wählen …", d => _ = PodcPickMusic(d, "outro")),
            ]),
            ("Fertig", [
                new ProgramCmd("podc-loud", "Folge fertig machen (MP3)", d => _ = PodcLoudness(d), Tip: "Mischt die Spuren, wendet Schnitte an, Intro/Outro, Lautheit −16 LUFS wie bei den großen Podcast-Plattformen, MP3 mit Kapiteln und Tags. Die Aufnahmen bleiben unverändert."),
            ]),
        ],
        GroupLevels = new() { ["Musik"] = 1 },
        Exports =
        [
            new("notes", "Shownotes (.md)", ".md", (d, f) => { File.WriteAllText(f, PodcastKit.ShowNotes(d.Text("title"), d.Text("description"), PodcastKit.ParseChapters(d.Text("chapters")).Chapters)); return f; }),
            new("rss", "Podcast-Feed (.xml)", ".xml", PodcRss),
            new("meta", "Kapiteldatei für FFmpeg (.txt)", ".txt", (d, f) => { File.WriteAllText(f, PodcastKit.FfMetadata(d.Text("title"), PodcastKit.ParseChapters(d.Text("chapters")).Chapters, PodcLength(d))); return f; }),
            new("wav", "Schnitt als WAV (ohne Lautheit)", ".wav", (d, f) => { AudioFile.WriteWav(PodcastEdit.Render(d.Data), f); return f; }),
        ],
        SimpleExports = 2,
        Workspace = PodcWorkspace,
        Properties = PodcProperties,
        Status = d =>
        {
            var (c, p) = PodcastKit.ParseChapters(d.Text("chapters")); var t = PodcastEdit.Tracks(d.Data);
            return $"{c.Count} Kapitel" + (p.Count > 0 ? $" · {p.Count} Hinweis(e)" : "") + (t.Count == 0 ? " · keine Aufnahme" : t.Count == 1 ? " · Aufnahme: " + Path.GetFileName(t[0].File) : $" · {t.Count} Spuren")
                + (PodcastEdit.Cuts(d.Data).Count is > 0 and var n ? $" · {n} Schnitte" : "");
        },
        Help = "„● Aufnehmen“ (R) nimmt eine Spur auf, „■ Stopp“ legt sie an – für jede Person eine Spur. An der Welle ziehst du eine Stelle auf: Entf löscht sie aus allen Spuren, Kapitel dahinter rücken mit. K setzt ein Kapitel an der Marke, Leertaste spielt ab. „Folge fertig machen“ braucht FFmpeg und schreibt eine MP3 mit −16 LUFS, Kapiteln und Tags. Den Feed lädst du selbst zu deinem Webspace hoch – Trendo veröffentlicht nichts.",
    }, "podcast", "podcasts", "folge", "shownotes", "kapitelmarken", "lautheit", "rss", "hörsendung", "interview aufnehmen");

    TimeSpan PodcLength(ProgramDocument d)
    {
        if (double.TryParse(d.Text("seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0) return TimeSpan.FromSeconds(s);
        if (PodcMix(d) is { } a && a.Seconds > 0) return TimeSpan.FromSeconds(a.Seconds);
        var c = PodcastKit.ParseChapters(d.Text("chapters")).Chapters;
        return c.Count > 0 ? c[^1].Start + TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(5);
    }

    static string PodcSig(ProgramDocument d, string? track)
    {
        var t = PodcastEdit.Tracks(d.Data);
        return string.Join("|", t.Select(x => x.Id + x.File + x.GainDb + x.Muted + (File.Exists(x.File) ? File.GetLastWriteTimeUtc(x.File).Ticks : 0))) + d.Data["cuts"]?.ToJsonString() + "#" + track;
    }

    /// <summary>Gemischte, geschnittene Folge (zwischengespeichert); null, wenn eine Spur fehlt oder nicht lesbar ist.</summary>
    AudioBuf? PodcMix(ProgramDocument d, string? track = null)
    {
        var tracks = PodcastEdit.Tracks(d.Data);
        if (tracks.Count == 0 || tracks.Any(t => !KreativSafe.LocalFile(t.File) || !AudioFile.Readable.Contains(Path.GetExtension(t.File).ToLowerInvariant()))) return null;
        var key = PodcSig(d, track);
        if (_podcCache.TryGetValue(key, out var c)) return c.Buf;
        try
        {
            var a = PodcastEdit.Render(d.Data, null, track);
            if (_podcCache.Count > 24) _podcCache.Clear();
            _podcCache[key] = (a, AudioEdit.Peaks(a, 1400));
            return a;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return null; }
    }

    (float Min, float Max)[] PodcPeaks(ProgramDocument d, string track) => PodcMix(d, track) is not null && _podcCache.TryGetValue(PodcSig(d, track), out var c) ? c.Peaks : [];

    // ---------- Aufnahme ----------
    /// <summary>Aufnahme einer neuen Spur starten. Ohne FFmpeg oder Mikrofon: ehrlicher Hinweis, nichts wird vorgetäuscht.</summary>
    public async Task PodcRecordStart(ProgramDocument d)
    {
        if (PodcRecording) { _progMsg[PodcKey] = "Die Aufnahme läuft schon – „■ Stopp“ (R) beendet sie."; Show(PodcKey); return; }
        if (PodcMicOverride is not null) { _podcFakeRec = true; _podcRecStart = DateTime.Now; _progMsg[PodcKey] = "Aufnahme läuft …"; PodcTick(); Show(PodcKey); return; }
        if (_opt.Isolated) { _progMsg[PodcKey] = "Testmodus: es wird nichts aufgenommen."; Show(PodcKey); return; }
        if (PodcFf is not { } ff) { _progMsg[PodcKey] = "Aufnehmen geht nicht: FFmpeg fehlt. So richtest du es ein: Einstellungen › Programme › Zusatzprogramme, dort bei FFmpeg auf „Einrichten“. Eine fertige Aufnahme kannst du trotzdem laden."; Show(PodcKey); return; }
        _progMsg[PodcKey] = "Mikrofon wird gesucht …"; Show(PodcKey);
        string[] input;
        if (OperatingSystem.IsWindows())
        {
            var mic = (await Task.Run(() => DesktopCapture.Devices(ff))).Microphones.FirstOrDefault();
            if (mic is null) { _progMsg[PodcKey] = "Aufnehmen geht nicht: kein Mikrofon gefunden. Steck eines an und erlaube den Zugriff (Windows-Einstellungen › Datenschutz › Mikrofon). Eine fertige Aufnahme kannst du trotzdem laden."; Show(PodcKey); return; }
            input = ["-f", "dshow", "-i", "audio=" + mic.Id];
        }
        else input = ["-f", "pulse", "-i", "default"];
        var dir = Path.Combine(WorkFolder("Podcast"), "Aufnahmen"); Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{PodcastEdit.NextSpeaker(d.Data)} {DateTime.Now:yyyy-MM-dd HH-mm-ss}.wav");
        var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
        foreach (var a in (string[])["-hide_banner", "-nostats", "-y", .. input, "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", file]) psi.ArgumentList.Add(a);
        try
        {
            _podcRecErr.Clear();
            var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
            p.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 } l) lock (_podcRecErr) { _podcRecErr.AppendLine(l); if (_podcRecErr.Length > 8000) _podcRecErr.Remove(0, 4000); } };
            p.BeginErrorReadLine();
            _podcRec = p; _podcRecFile = file; _podcRecStart = DateTime.Now;
            await Task.Delay(1200);
            if (p.HasExited)
            {
                _podcRec = null; string why; lock (_podcRecErr) why = _podcRecErr.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "";
                _progMsg[PodcKey] = "Die Aufnahme ging nicht – vermutlich ist das Mikrofon belegt oder Windows erlaubt den Zugriff nicht. FFmpeg meldet: " + why; Show(PodcKey); return;
            }
            _progMsg[PodcKey] = "Aufnahme läuft … „■ Stopp“ (R) beendet sie."; PodcTick(); Show(PodcKey);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { _podcRec = null; _progMsg[PodcKey] = "Aufnahme ging nicht: " + e.Message; Show(PodcKey); }
    }

    void PodcTick()
    {
        _podcRecTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) =>
        {
            if (!PodcRecording) { _podcRecTimer?.Stop(); return; }
            if (_podcRecLabel is { } t) t.Text = "● " + PodcastEdit.Clock(DateTime.Now - _podcRecStart);
        });
        _podcRecTimer.Start();
    }

    /// <summary>Aufnahme beenden und als neue Sprecherspur anlegen (ein Schritt, rückgängig machbar).</summary>
    public async Task PodcRecordStop(ProgramDocument d)
    {
        if (!PodcRecording) { _progMsg[PodcKey] = "Es läuft keine Aufnahme – „● Aufnehmen“ (R) startet eine."; Show(PodcKey); return; }
        string? file;
        if (_podcFakeRec) { _podcFakeRec = false; file = PodcMicOverride?.Invoke(); }
        else
        {
            var p = _podcRec!; file = _podcRecFile; _podcRec = null;
            try { await p.StandardInput.WriteAsync("q"); await p.StandardInput.FlushAsync(); } catch (IOException) { }
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                try { await p.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { try { p.Kill(true); } catch (InvalidOperationException) { } }
            p.Dispose();
        }
        _podcRecTimer?.Stop();
        if (file is null || !File.Exists(file) || new FileInfo(file).Length <= 44) { _progMsg[PodcKey] = "Die Aufnahme ist leer – nichts angelegt."; Show(PodcKey); return; }
        var name = PodcastEdit.NextSpeaker(d.Data);
        d.Edit("Aufnahme: " + name, x => PodcastEdit.AddTrack(x, name, file));
        var len = AudioFile.Read(file).Seconds;
        _progMsg[PodcKey] = $"„{name}“ aufgenommen ({PodcastEdit.Clock(TimeSpan.FromSeconds(len))}). Für die nächste Person einfach wieder „● Aufnehmen“.";
        Show(PodcKey);
    }

    async Task PodcPick(ProgramDocument d)
    {
        if (await PickMediaFile("podc-audio", "Aufnahme", ["*.mp3", "*.wav", "*.m4a", "*.ogg", "*.flac"]) is { } f)
            await PodcAddFiles(d, [f]);
    }

    /// <summary>Tondateien als Spuren übernehmen (Dateiauswahl, Ziehen und Ablegen). M4A/OGG/FLAC wandelt FFmpeg in WAV um; ohne FFmpeg bleibt die Datei nur für „Folge fertig machen“.</summary>
    public async Task PodcAddFiles(ProgramDocument d, IReadOnlyList<string> files)
    {
        var added = 0; var notes = new List<string>();
        foreach (var f0 in files)
        {
            var f = f0; var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext is not (".mp3" or ".wav" or ".m4a" or ".ogg" or ".flac" or ".opus" or ".aac")) { notes.Add($"„{Path.GetFileName(f)}“ ist keine Tondatei."); continue; }
            if (!AudioFile.Readable.Contains(ext))
            {
                if (PodcFf is not { } ff)
                {
                    if (PodcastEdit.Tracks(d.Data).Count == 0) { d.Set("audio", f, "Aufnahme"); added++; notes.Add("Ohne FFmpeg lässt sich diese Datei nicht schneiden – nur fertig machen."); }
                    else notes.Add($"„{Path.GetFileName(f)}“ braucht FFmpeg zum Umwandeln.");
                    continue;
                }
                var dir = Path.Combine(WorkFolder("Podcast"), "Spuren"); Directory.CreateDirectory(dir);
                var wav = Path.Combine(dir, Path.GetFileNameWithoutExtension(f) + ".wav");
                var (code, err) = await PodcRunFf(ff, ["-y", "-hide_banner", "-nostats", "-i", f, "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", wav]);
                if (code != 0 || !File.Exists(wav)) { notes.Add($"„{Path.GetFileName(f)}“ ließ sich nicht umwandeln: {PodcLast(err)}"); continue; }
                f = wav;
            }
            var name = PodcastEdit.NextSpeaker(d.Data);
            d.Edit("Spur: " + Path.GetFileName(f), x => PodcastEdit.AddTrack(x, name, f)); added++;
        }
        _progMsg[PodcKey] = (added > 0 ? $"{added} Spur(en) übernommen. " : "") + string.Join(" ", notes);
        Show(PodcKey);
    }

    async Task PodcPickMusic(ProgramDocument d, string which)
    {
        if (await PickMediaFile("podc-" + which, which == "intro" ? "Intro-Musik" : "Outro-Musik", ["*.mp3", "*.wav"]) is { } f)
            ProgramRun(PodcKey, () => { AudioFile.Read(f); d.Set(which, f, which == "intro" ? "Intro" : "Outro"); _progMsg[PodcKey] = $"{(which == "intro" ? "Intro" : "Outro")}: {Path.GetFileName(f)}"; });
    }

    // ---------- Schnitt ----------
    public void PodcCut(ProgramDocument d)
    {
        var (a, b) = PodcSel;
        if (PodcMix(d) is null) throw new InvalidOperationException("Erst eine Aufnahme machen oder laden (WAV oder MP3).");
        d.Edit($"Stelle {AudioEdit.Time(a)}–{AudioEdit.Time(b)} gelöscht", x => PodcastEdit.ApplyCut(x, a, b));
        PodcSel = (a, a); _progMsg[PodcKey] = $"{(b - a).ToString("0.0", De)} s entfernt – Kapitel dahinter sind mitgerückt.";
    }

    public void PodcTrimPauses(ProgramDocument d)
    {
        var mix = PodcMix(d) ?? throw new InvalidOperationException("Erst eine Aufnahme machen oder laden (WAV oder MP3).");
        var found = PodcastEdit.FindPauses(mix);
        if (found.Count == 0) { _progMsg[PodcKey] = "Keine langen Pausen gefunden."; return; }
        (int Count, double Seconds) r = default;
        d.Edit($"{found.Count} Pausen gekürzt", x => r = PodcastEdit.TrimPauses(x, mix));
        _progMsg[PodcKey] = $"{r.Count} Pausen gekürzt, {r.Seconds.ToString("0.0", De)} s gespart – Kapitel sind mitgerückt.";
    }

    public void PodcChapterHere(ProgramDocument d)
    {
        var at = TimeSpan.FromSeconds(Math.Round(PodcSel.A));
        var lines = d.Text("chapters").Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        lines.Add(PodcastEdit.Clock(at) + " Neues Kapitel");
        var sorted = lines.OrderBy(l => PodcastKit.ParseChapters(l).Chapters.FirstOrDefault()?.Start ?? TimeSpan.MaxValue).ToList();
        d.Set("chapters", string.Join("\n", sorted), "Kapitel bei " + PodcastEdit.Clock(at));
        _progMsg[PodcKey] = $"Kapitel bei {PodcastEdit.Clock(at)} – den Namen änderst du unten in der Kapitelliste.";
    }

    /// <summary>V-12 (T-0709): „Kapitel hinzufügen“ – an der Marke in der Aufnahme, ohne Marke eine Minute nach dem letzten Kapitel (ein leeres Dokument beginnt bei 00:00).</summary>
    public void PodcChapterAdd(ProgramDocument d)
    {
        if (PodcSel.A > 0.5) { PodcChapterHere(d); return; }
        var ch = PodcastKit.ParseChapters(d.Text("chapters")).Chapters;
        var at = ch.Count == 0 ? TimeSpan.Zero : ch.Max(c => c.Start) + TimeSpan.FromMinutes(1);
        var lines = PodcChapterLines(d); lines.Add(PodcastEdit.Clock(at) + " Neues Kapitel");
        d.Set("chapters", string.Join("\n", lines), "Kapitel bei " + PodcastEdit.Clock(at));
        _progMsg[PodcKey] = $"Kapitel bei {PodcastEdit.Clock(at)} – Zeit und Namen änderst du direkt in der Liste.";
    }

    /// <summary>Zeilen der Kapitelliste, wie sie im Dokument stehen (auch fehlerhafte – nichts geht beim Bearbeiten in der Liste verloren).</summary>
    static List<string> PodcChapterLines(ProgramDocument d) => d.Text("chapters").Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();

    /// <summary>„02:15 Wie alles anfing“ → („02:15“, „Wie alles anfing“); ohne Zeit vorn bleibt die Zeit leer.</summary>
    public static (string Time, string Title) PodcSplit(string line)
    {
        var l = line.Trim(); var sp = l.IndexOf(' ');
        var first = sp < 0 ? l : l[..sp];
        return first.Length > 0 && first.All(c => char.IsDigit(c) || c is ':' or '.') && first.Any(char.IsDigit) ? (first, sp < 0 ? "" : l[(sp + 1)..].Trim()) : ("", l);
    }

    /// <summary>V-12 (T-0709): Kapitel als Liste – je Kapitel Zeit und Name in eigenen Feldern, „Entfernen“ nimmt es heraus, „+ Kapitel hinzufügen“ legt eins an.
    /// Gespeichert wird weiter das bekannte Textformat („00:00 Titel“ je Zeile), Shownotes, Feed und MP3-Kapitel lesen es unverändert.</summary>
    Control PodcChapterList(ProgramDocument d)
    {
        var lines = PodcChapterLines(d);
        var list = new StackPanel { Name = "podc-ch-list", Spacing = 4 };
        void Save(int i, string time, string title)
        {
            var now = PodcChapterLines(d);
            var line = (time.Trim() + " " + title.Trim()).Trim();
            if (i >= now.Count || now[i] == line) return;
            if (line.Length == 0) now.RemoveAt(i); else now[i] = line;
            d.Set("chapters", string.Join("\n", now), "Kapitel geändert");
        }
        for (var i = 0; i < lines.Count; i++)
        {
            var idx = i; var (time, title) = PodcSplit(lines[i]);
            var t = new TextBox { Name = $"podc-ch-time-{i}", Text = time, Width = 78, PlaceholderText = "00:00", FontFamily = new FontFamily("Consolas, Courier New, monospace") };
            ToolTip.SetTip(t, "Beginn des Kapitels (Minuten:Sekunden)");
            var n = new TextBox { Name = $"podc-ch-title-{i}", Text = title, PlaceholderText = "Name des Kapitels" };
            void Commit() => Save(idx, t.Text ?? "", n.Text ?? "");
            foreach (var tb in new[] { t, n })
            {
                tb.LostFocus += (_, _) => Commit();
                tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); Show(PodcKey); e.Handled = true; } };
            }
            var del = new Button { Name = $"podc-ch-del-{i}", Content = "Entfernen", Background = Brushes.Transparent, Foreground = Muted, FontSize = 12, Padding = new Thickness(8, 4), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(del, "Kapitel entfernen"); Avalonia.Automation.AutomationProperties.SetName(del, "Kapitel entfernen");
            del.Click += (_, _) => ProgramRun(PodcKey, () => { var now = PodcChapterLines(d); if (idx < now.Count) { now.RemoveAt(idx); d.Set("chapters", string.Join("\n", now), "Kapitel entfernt"); } });
            list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,6,*,4,Auto"), Children = { t, Pos(n, 2), Pos(del, 4) } });
        }
        var add = new Button { Name = "podc-ch-add", Content = "+ Kapitel hinzufügen", Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), Foreground = Text };
        ToolTip.SetTip(add, "Neues Kapitel – an der Marke in der Aufnahme, sonst eine Minute nach dem letzten (Taste K)");
        add.Click += (_, _) => ProgramRun(PodcKey, () => PodcChapterAdd(d));
        list.Children.Add(add);
        return list;
    }

    void PodcPlay(ProgramDocument d)
    {
        var a = PodcMix(d) ?? throw new InvalidOperationException("Erst eine Aufnahme machen oder laden (WAV oder MP3).");
        var (s, e) = PodcSel; if (e <= s) e = a.Seconds; if (s >= a.Seconds) s = 0;
        var f = Path.Combine(Path.GetTempPath(), "trendo-podcast-anhoeren.wav");
        AudioFile.WriteWav(a.Slice((int)(s * a.Rate), (int)(e * a.Rate)), f);
        var o = Audio(); // WAV spielt Trendo selbst (T-0731); ohne Tonausgabe meldet Load eine ehrliche Sperre
        o.Load(f); _progMsg[PodcKey] = $"Spielt {AudioEdit.Time(s)}–{AudioEdit.Time(e)}.";
    }

    static string PodcLast(string err) => err.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";

    static async Task<(int Code, string Err)> PodcRunFf(string ff, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, err);
    }

    /// <summary>„Folge fertig machen“: Spuren mischen, Schnitte, Intro/Outro, Lautheit −16 LUFS (Messung + lineares Angleichen), MP3 mit ID3-Tags und Kapiteln.
    /// Das Original bleibt; die MP3 liegt als „…-fertig.mp3“ neben der ersten Aufnahme.</summary>
    public async Task PodcLoudness(ProgramDocument d)
    {
        var tracks = PodcastEdit.Tracks(d.Data);
        if (tracks.Count == 0 || tracks.Any(t => !KreativSafe.LocalFile(t.File))) { _progMsg[PodcKey] = "Nimm zuerst auf oder lade eine Aufnahme von diesem Rechner."; Show(PodcKey); return; }
        if (_podcBusy) { _progMsg[PodcKey] = "Die Folge wird schon fertig gemacht – einen Moment."; Show(PodcKey); return; }
        // T-0681: erst prüfen, dann „beschäftigt“ – früher blieb der Merker ohne FFmpeg gesetzt und der Knopf war bis zum Neustart tot
        if (PodcFf is not { } ff) { _progMsg[PodcKey] = "FFmpeg fehlt – ohne geht „Folge fertig machen“ (Lautheit angleichen, MP3) nicht. So richtest du es ein: Einstellungen › Programme › Zusatzprogramme, dort bei FFmpeg auf „Einrichten“. Danach hier noch einmal klicken."; Show(PodcKey); return; }
        var src = tracks[0].File;
        var meta = Path.Combine(Path.GetTempPath(), "trendo-podcast-" + Guid.NewGuid().ToString("N") + ".txt");
        var mix = Path.Combine(Path.GetTempPath(), "trendo-podcast-" + Guid.NewGuid().ToString("N") + ".wav");
        var output = Path.Combine(Path.GetDirectoryName(src)!, Path.GetFileNameWithoutExtension(src) + "-fertig.mp3");
        _podcBusy = true; // ab hier setzt das finally unten den Merker in jedem Fall zurück
        try
        {
            _progMsg[PodcKey] = "Folge wird fertig gemacht – Lautheit wird angeglichen …"; Show(PodcKey);
            var direct = tracks.Count == 1 && PodcastEdit.Cuts(d.Data).Count == 0 && d.Text("intro").Length == 0 && d.Text("outro").Length == 0;
            string input = src; double intro = 0; var total = PodcLength(d);
            if (!direct)
            {
                var data = (JsonObject)d.Data.DeepClone(); string introF = d.Text("intro"), outroF = d.Text("outro");
                (intro, var secs) = await Task.Run(() =>
                {
                    var speech = PodcastEdit.Render(data);
                    var (all, i) = PodcastEdit.WithMusic(speech, introF.Length > 0 ? AudioFile.Read(introF) : null, outroF.Length > 0 ? AudioFile.Read(outroF) : null);
                    AudioFile.WriteWav(all, mix, 24); return (i, all.Seconds);
                });
                input = mix; total = TimeSpan.FromSeconds(secs);
            }
            File.WriteAllText(meta, PodcastEdit.Metadata(d.Text("title"), PodcastKit.ParseChapters(d.Text("chapters")).Chapters, total, intro));
            var (c1, e1) = await PodcRunFf(ff, PodcastEdit.MeasureArgs(input));
            var measured = c1 == 0 ? PodcastEdit.ParseMeasure(e1) : null;
            if (c1 != 0) { _progMsg[PodcKey] = "FFmpeg meldet einen Fehler: " + PodcLast(e1); return; }
            var tags = new PodcastEdit.Tags(d.Text("title"), d.Text("author"), d.Text("show"), d.Text("year").Length > 0 ? d.Text("year") : DateTime.Now.Year.ToString(System.Globalization.CultureInfo.InvariantCulture), d.Text("description"), d.Text("episode"));
            var (c2, e2) = await PodcRunFf(ff, PodcastEdit.ExportArgs(input, meta, output, measured, tags));
            if (c2 == 0 && File.Exists(output))
            {
                PodcLastOutput = output; d.Set("master", output, "Folge fertig");
                _progMsg[PodcKey] = "Fertig: " + output + (measured is { } m ? $" (vorher {m.I.ToString("0.0", De)} LUFS, jetzt −16 LUFS)" : "");
                NotifyFileResult(_progMsg[PodcKey]);
            }
            else _progMsg[PodcKey] = "FFmpeg meldet einen Fehler: " + PodcLast(e2);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or InvalidDataException or System.ComponentModel.Win32Exception) { _progMsg[PodcKey] = "Fertig machen ging nicht: " + ex.Message; }
        finally { _podcBusy = false; try { File.Delete(meta); File.Delete(mix); } catch (IOException) { } }
        Show(PodcKey);
    }

    string PodcRss(ProgramDocument d, string file)
    {
        var audio = PodcastEdit.Tracks(d.Data).FirstOrDefault()?.File ?? d.Text("audio");
        var fin = d.Text("master") is { Length: > 0 } m && File.Exists(m) ? m : KreativSafe.LocalFile(audio) ? Path.Combine(Path.GetDirectoryName(audio)!, Path.GetFileNameWithoutExtension(audio) + "-fertig.mp3") : "";
        var use = File.Exists(fin) ? fin : audio;
        if (!KreativSafe.LocalFile(use)) throw new InvalidOperationException("Für den Feed braucht es eine Aufnahme auf diesem Rechner.");
        var ep = new PodcastKit.Episode(d.Text("title"), d.Text("description"), Path.GetFileName(use), new FileInfo(use).Length, PodcLength(d), DateTimeOffset.Now, d.Id);
        File.WriteAllText(file, PodcastKit.Rss(d.Text("show").Length > 0 ? d.Text("show") : d.Text("title"), d.Text("author"), d.Text("description"), d.Text("baseUrl"), [ep]));
        return file;
    }

    // ---------- Arbeitsfläche ----------
    Control PodcWorkspace(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "podc-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4), Focusable = true };
        var tracks = PodcastEdit.Tracks(d.Data);
        var mix = PodcMix(d); var total = mix?.Seconds ?? 0;
        if (PodcSel.B > total || PodcSel.A > total) PodcSel = (0, 0);

        // Aufnahme-Leiste
        var rec = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        rec.Children.Add(_podcRecLabel = new TextBlock { Name = "podc-rec-time", Text = PodcRecording ? "● " + PodcastEdit.Clock(DateTime.Now - _podcRecStart) : "Bereit", Foreground = PodcRecording ? Brush.Parse("#F87171") : Muted, FontSize = 18, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        rec.Children.Add(P(PodcRecording ? "Aufnahme läuft – „■ Stopp“ oder R beendet sie." : tracks.Count == 0 ? "Noch keine Aufnahme. „● Aufnehmen“ (R) oder eine Datei hineinziehen." : $"{tracks.Count} Spur(en) · Länge {AudioEdit.Time(total)}" + (PodcSel.B > PodcSel.A ? $" · markiert {AudioEdit.Time(PodcSel.A)}–{AudioEdit.Time(PodcSel.B)} (Entf löscht)" : $" · Marke {AudioEdit.Time(PodcSel.A)}"), Muted, 12));
        rec.Children[^1].VerticalAlignment = VerticalAlignment.Center;
        var info = new StackPanel { Spacing = 8, Children = { rec } };

        // Spuren mit Wellenform
        var lanes = new List<WaveView>();
        foreach (var (t, i) in tracks.Select((t, i) => (t, i)))
        {
            var readable = AudioFile.Readable.Contains(Path.GetExtension(t.File).ToLowerInvariant()) && File.Exists(t.File);
            var wave = new WaveView { Name = $"podc-wave-{i}", Peaks = readable ? PodcPeaks(d, t.Id) : [], Seconds = total, SelA = PodcSel.A, SelB = PodcSel.B, MinHeight = 84, Height = 84, Opacity = t.Muted ? 0.45 : 1 };
            lanes.Add(wave);
            wave.Selected = (a, b) => { PodcSel = (a, b); foreach (var l in lanes) { l.SelA = a; l.SelB = b; l.InvalidateVisual(); } sp.Focus(); };
            var tid = t.Id;
            wave.ContextMenu = PodcMenu(d, tid, t.Muted);
            var head = new StackPanel { Width = 130, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Text = t.Name, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = (t.Muted ? "stumm · " : "") + (t.GainDb != 0 ? $"{t.GainDb:+0;-0} dB · " : "") + Path.GetFileName(t.File), Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } };
            ToolTip.SetTip(head, t.File);
            info.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { head, Pos(new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = wave }, 1) } });
            if (!readable) info.Children.Add(P("Diese Spur kann Trendo nicht selbst lesen – schneiden geht nach dem Umwandeln in WAV (FFmpeg).", Brush.Parse("#F5A524"), 11));
        }
        var music = new List<string>();
        if (d.Text("intro").Length > 0) music.Add("Intro: " + Path.GetFileName(d.Text("intro")));
        if (d.Text("outro").Length > 0) music.Add("Outro: " + Path.GetFileName(d.Text("outro")));
        info.Children.Add(P((music.Count > 0 ? string.Join(" · ", music) + " · " : "") + (IsSimple // V-11 (T-0709): in „Einfach“ ohne LUFS/dBTP/kbit/s – die Werte stehen ab „Erweitert“
            ? "Zum Schluss gleicht Trendo die Lautstärke aus und speichert eine MP3 mit Kapiteln. Die Aufnahmen bleiben unverändert."
            : "Ziel −16 LUFS, Spitze −1,5 dBTP, MP3 128 kbit/s mit Kapiteln und Tags. Die Aufnahmen bleiben unverändert."), Muted, 11));
        if (d.Text("master") is { Length: > 0 } fin && File.Exists(fin)) info.Children.Add(P("Fertige Folge: " + fin, Text, 11.5));
        var card = KrCard(info, "podc-audio-card");
        DragDrop.SetAllowDrop(card, true);
        card.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        card.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];
            if (files.Count == 0) return; e.Handled = true; _ = PodcAddFiles(d, files);
        });
        sp.Children.Add(card);

        // Kapitel
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
        box.FontFamily = new FontFamily("Consolas, Courier New, monospace"); box.MinHeight = 160;
        Render(d.Text("chapters"));
        // V-12 (T-0709): in „Einfach“ Kapitel als Liste mit Knöpfen statt Textformat („00:00 Titel“ je Zeile) – das Textfeld bleibt im Baum und ab „Erweitert“ sichtbar
        var chapterEdit = new StackPanel { Spacing = 4, Children = { H("Kapitel", 14) } };
        if (IsSimple) { box.IsVisible = false; chapterEdit.Children.Add(PodcChapterList(d)); }
        chapterEdit.Children.Add(box);
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Children = { chapterEdit, Pos(KrCard(new StackPanel { Spacing = 6, Children = { H("So sieht es im Player aus", 13), notes } }), 2) } });

        sp.KeyDown += (_, e) =>
        {
            if (e.Source is TextBox) return;
            switch (e.Key)
            {
                case Key.Delete or Key.Back: ProgramRun(PodcKey, () => PodcCut(d)); break;
                case Key.Space: ProgramRun(PodcKey, () => PodcPlay(d)); break;
                case Key.K: ProgramRun(PodcKey, () => PodcChapterHere(d)); break;
                case Key.R: _ = PodcRecording ? PodcRecordStop(d) : PodcRecordStart(d); break;
                default: return;
            }
            e.Handled = true;
        };
        return sp;
    }

    ContextMenu PodcMenu(ProgramDocument d, string trackId, bool muted)
    {
        MenuItem Item(string name, string header, Action run) { var m = new MenuItem { Name = name, Header = header }; m.Click += (_, _) => ProgramRun(PodcKey, run); return m; }
        return new ContextMenu { Name = "podc-menu", Items = {
            Item("podc-menu-cut", "Markierte Stelle löschen (Entf)", () => PodcCut(d)),
            Item("podc-menu-chapter", "Kapitel an der Marke (K)", () => PodcChapterHere(d)),
            Item("podc-menu-play", "Anhören (Leertaste)", () => PodcPlay(d)),
            new Separator(),
            Item("podc-menu-louder", "Spur lauter (+3 dB)", () => d.Edit("Spur lauter", x => PodcastEdit.SetTrack(x, trackId, o => o["gain"] = Math.Min(24, ProgramDocument.ToNumber(o["gain"], 0) + 3)))),
            Item("podc-menu-quieter", "Spur leiser (−3 dB)", () => d.Edit("Spur leiser", x => PodcastEdit.SetTrack(x, trackId, o => o["gain"] = Math.Max(-24, ProgramDocument.ToNumber(o["gain"], 0) - 3)))),
            Item("podc-menu-mute", muted ? "Spur wieder hörbar" : "Spur stumm", () => d.Edit(muted ? "Spur hörbar" : "Spur stumm", x => PodcastEdit.SetTrack(x, trackId, o => o["muted"] = !muted))),
            Item("podc-menu-remove", "Spur entfernen", () => d.Edit("Spur entfernt", x => PodcastEdit.RemoveTrack(x, trackId))),
        } };
    }

    Control PodcProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        void Txt(string key, string label, bool multi = false) { sp.Children.Add(P(label, null, 11)); sp.Children.Add(KrBox(d, key, "podc-p-" + key, label, multi)); }
        Txt("title", "Titel der Folge");
        Txt("description", "Beschreibung", true);
        sp.Children.Add(H("Tags in der MP3 und Feed", 13));
        Txt("show", "Name des Podcasts");
        Txt("author", "Autorin oder Autor");
        Txt("episode", "Folgennummer");
        Txt("year", "Jahr");
        Txt("baseUrl", "Adresse deines Webspace (https://…)");
        Txt("seconds", "Länge in Sekunden (optional)");
        return sp;
    }
}
