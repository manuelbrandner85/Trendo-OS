using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Pilot 4.0, V1 (09.11.2026): flüssige Vorschau mit Ton im Videoschnitt. Bild aus <see cref="PreviewStream"/>, Ton als fertige Mischung (Pegel, Musik, Ducking) über mpv im Hintergrund.
/// Vor- und Rücklauf mit J/K/L in anderem Tempo nutzen weiter die Einzelbilder.</summary>
public partial class MainWindow
{
    const int PreviewW = 640, PreviewH = 360;
    /// <summary>Für Tests: Bildquelle ersetzen (Stück, Start in der Quelldatei, Länge, Bildrate).</summary>
    public Func<EditClip, double, double, int, IPreviewSource?>? PreviewSourceOverride { get; set; }
    /// <summary>Für Tests: Tonausgabe ersetzen.</summary>
    public Func<IAudioOut>? PreviewAudioOverride { get; set; }
    /// <summary>Für Tests: Vorschau-Ton fertig bereitstellen (Projekt → Datei oder null).</summary>
    public Func<System.Text.Json.Nodes.JsonObject, string?>? PreviewMixOverride { get; set; }

    IPreviewSource? _veSource; string _veSourceKey = ""; double _veSourceStart;
    WriteableBitmap? _veBitmap; byte[]? _veBuffer;
    readonly Stopwatch _veClock = new(); double _veClockStart;
    IAudioOut? _veAudio; bool _veAudioSeek; int _vePlayRun;
    public int PreviewFramesShown { get; private set; }

    bool EditSmoothPossible => EditRate == 1 && PlayFramesOverride is null && (PreviewSourceOverride is not null || EditFf is not null);

    static int EditPreviewFps(ProgramDocument d) => Math.Clamp((int)d.Number("fps", 30), 10, 30);

    /// <summary>Startet die flüssige Wiedergabe ab <paramref name="from"/>.</summary>
    void EditSmoothStart(ProgramDocument d, double from)
    {
        var fps = EditPreviewFps(d);
        _veClockStart = from; _veClock.Restart(); PreviewFramesShown = 0;
        var run = ++_vePlayRun;
        _vePlay = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / fps), Avalonia.Threading.DispatcherPriority.Render, (_, _) => EditSmoothTick(d, fps));
        _vePlay.Start();
        _progMsg[VK] = "Spielt … (Leertaste hält an)";
        _ = EditPreviewAudioAsync(d, run);
    }

    /// <summary>Für Tests: ein Schritt der Wiedergabe (der Taktgeber läuft ohne Fenster nicht).</summary>
    public void EditSmoothTickForTests(ProgramDocument d) => EditSmoothTick(d, EditPreviewFps(d));

    void EditSmoothTick(ProgramDocument d, int fps)
    {
        var t = _veClockStart + _veClock.Elapsed.TotalSeconds;
        _vePos = t;
        if (EditRate != 1) { EditSmoothStop(); _vePlay?.Stop(); _vePlay = null; d.Set("playhead", Math.Round(t, 3), "Abspielkopf"); EditTogglePlay(d); return; } // J/L: anderes Tempo über die Einzelbilder
        var clips = VideoEdit.Clips(d.Data); var len = VideoEdit.VideoLength(clips);
        if (t >= len) { EditSmoothStop(); _vePlay?.Stop(); _vePlay = null; d.Set("playhead", Math.Round(len, 2), "Abspielkopf"); _progMsg[VK] = "Ende erreicht."; Show(VK); return; }
        if (VideoEdit.At(d.Data, t) is not { } at) return;
        var c = clips[at.Index];
        EditShowChrome(d, t, c);
        EditUpdateMeters(d, t); // V8: Pegel laufen mit
        if (_vePreview is null) return;
        double local;
        if (PreviewSourceOverride is null && EditFf is { } fl)
        {
            // T-0695: fertiges Bild aus demselben Filtergraphen wie der Export – Farbe, Titel, Übergänge, Keyframes, Effekte, Einblendungen – ab der Startstelle bis zum Ende
            if (_veSourceKey != "look" || _veSource is null)
            {
                _veSource?.Dispose();
                _veSourceKey = "look"; _veSourceStart = t;
                var snap = (System.Text.Json.Nodes.JsonObject)d.Data.DeepClone();
                try { _veSource = new PreviewStream(fl, VideoLook.StreamArgs(snap, t, PreviewW, PreviewH, EditExportOptions(d), f => VideoProxy.ForPreview(snap, f)), PreviewW, PreviewH, VideoLook.Fps(snap)); }
                catch (Exception e) when (e is IOException or InvalidOperationException) { _progMsg[VK] = "Vorschau nicht möglich: " + e.Message; _veSource = null; }
            }
            local = t - _veSourceStart;
        }
        else
        {
            if (c.IsImage) { if (_veSourceKey != c.Id) { _veSource?.Dispose(); _veSource = null; _veSourceKey = c.Id; EditShowFrame(d, t); } return; }
            var key = c.Id + "|" + c.In + "|" + c.Out;
            if (_veSourceKey != key || _veSource is null)
            {
                _veSource?.Dispose();
                _veSourceKey = key; _veSourceStart = at.SourceTime;
                var rest = Math.Max(0.1, c.In + c.Length - at.SourceTime);
                _veSource = PreviewSourceOverride is { } o ? o(c, at.SourceTime, rest, fps) : FfmpegPath is { } ff ? new PreviewStream(ff, VideoProxy.ForPreview(d.Data, c.File), at.SourceTime, rest, PreviewW, PreviewH, fps) : null;
            }
            local = at.SourceTime - _veSourceStart;
        }
        if (_veSource is null) return;
        _veBitmap ??= new WriteableBitmap(new PixelSize(PreviewW, PreviewH), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        _veBuffer ??= new byte[PreviewW * PreviewH * 4];
        if (!_veSource.TryCopy(local, _veBuffer, PreviewW * 4))
        {
            if (++_veMissed == 15 && _veLookBusy is { } busy) { busy.Text = "Vorschau wird berechnet …"; busy.IsVisible = true; } // ehrlich zeigen, wenn die Bilder nicht nachkommen
            return;
        }
        _veMissed = 0; if (_veLookBusy is { } b0) b0.IsVisible = false;
        using (var fb = _veBitmap.Lock())
            for (var y = 0; y < PreviewH; y++) System.Runtime.InteropServices.Marshal.Copy(_veBuffer, y * PreviewW * 4, fb.Address + y * fb.RowBytes, PreviewW * 4);
        PreviewFramesShown++;
        // Avalonia zeichnet ein WriteableBitmap nur neu, wenn die Quelle neu gesetzt oder das Bild ungültig gemacht wird
        if (!ReferenceEquals(_vePreview.Source, _veBitmap)) _vePreview.Source = _veBitmap;
        _vePreview.IsVisible = true; _vePreview.InvalidateVisual();
        EditFullSync();
    }
    int _veMissed;

    /// <summary>Beendet Bildquelle und Ton der flüssigen Wiedergabe.</summary>
    void EditSmoothStop()
    {
        _vePlayRun++;
        _veClock.Stop();
        _veSource?.Dispose(); _veSource = null; _veSourceKey = "";
        _veAudio?.Stop();
    }

    /// <summary>Ton-Mischung im Hintergrund bauen (oder aus dem Zwischenspeicher nehmen), dann ab der aktuellen Stelle abspielen. Bis dahin läuft das Bild ohne Ton.</summary>
    async Task EditPreviewAudioAsync(ProgramDocument d, int run)
    {
        if (PreviewAudioOverride is null && (MpvExe is null || _opt.Isolated)) return;
        var snapshot = (System.Text.Json.Nodes.JsonObject)d.Data.DeepClone();
        string? mix;
        try { mix = await Task.Run(() => EditPreviewMix(snapshot)); }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException) { return; }
        if (mix is null || run != _vePlayRun || _vePlay is null) return;
        if (_veAudio is null)
        {
            _veAudio = PreviewAudioOverride is { } o ? o() : new MpvAudio(MpvExe!);
            _veAudio.Progress += (_, _) => _ui.Post(() => { if (!_veAudioSeek) return; _veAudioSeek = false; if (_vePlay is not null) _veAudio?.Seek(_veClockStart + _veClock.Elapsed.TotalSeconds); });
            Closed += (_, _) => _veAudio?.Dispose();
        }
        _veAudioSeek = true; // mpv springt erst, wenn die Datei geladen ist: die erste Positionsmeldung abwarten
        _veAudio.Load(mix);
        _veAudio.SetPause(false);
    }

    string? EditPreviewMix(System.Text.Json.Nodes.JsonObject project)
    {
        if (PreviewMixOverride is { } o) return o(project);
        if (FfmpegPath is not { } ff) return null;
        project.Remove("playhead");
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(project.ToJsonString())))[..24];
        var dir = Path.Combine(MediaFolder, ".vorschau", "ton"); Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, hash + ".m4a");
        if (File.Exists(file)) return file;
        var tmp = Path.Combine(dir, hash + ".tmp.m4a");
        var r = Cli.Run(ff, VideoEdit.ExportArgs(project, tmp, new VideoExport.Options(VideoExport.Presets.First(p => p.AudioOnly))), dir, 300000);
        if (!r.Ok || !File.Exists(tmp)) { if (File.Exists(tmp)) File.Delete(tmp); return null; }
        File.Move(tmp, file, true);
        return file;
    }

    // ---------- T-0695: Vorschau = Export (Standbild am Abspielkopf) ----------
    /// <summary>Für Tests: echtes FFmpeg auch im abgeschotteten Testmodus (sonst das eingerichtete, außer im Testmodus).</summary>
    public string? EditFfmpegPath { get; set; }
    string? EditFf => EditFfmpegPath ?? (_opt.Isolated ? null : FfmpegPath);
    /// <summary>Breite des Vorschaubilds: gerechnet wird in Projektgröße, verkleinert erst am Ende.</summary>
    const int LookWidth = 960;
    readonly Dictionary<string, Bitmap> _veLooks = [];
    CancellationTokenSource? _veLookCts; string _veLookKey = ""; Bitmap? _veLookShown; TextBlock? _veLookBusy;
    /// <summary>Für Tests und Messung: laufende Berechnung, Dauer der letzten (ms, vom Wunsch bis zum Bild), letzter Fehler.</summary>
    public Task? EditLookTask { get; private set; }
    public long EditLookMs { get; private set; } = -1;
    public string EditLookError { get; private set; } = "";
    /// <summary>Bild, das die Vorschau gerade zeigt (fertiges Bild mit Effekten oder Wiedergabe).</summary>
    public IImage? EditPreviewImage => _vePreview?.Source;

    /// <summary>Fertiges Bild am Abspielkopf aus dem Zwischenspeicher – sonst Berechnung im Hintergrund starten und bis dahin das letzte fertige Bild zeigen.
    /// Jede neue Stelle bricht die vorige Berechnung ab (Scrubben), das Bild kommt nach dem Loslassen.</summary>
    Bitmap? EditLook(ProgramDocument d, out bool pending)
    {
        pending = false;
        if (EditFf is not { } ff || VideoEdit.At(d.Data, d.Number("playhead")) is null) return null;
        var t = d.Number("playhead"); var o = EditExportOptions(d);
        string key;
        try { key = VideoLook.Key(d.Data, t, LookWidth, o); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        if (_veLooks.TryGetValue(key, out var have)) { _veLookShown = have; return have; }
        pending = true;
        if (key != _veLookKey) EditLookStart(ff, d, key, t, o); // dieselbe Stelle wird nicht doppelt gerechnet (auch nicht nach einem Fehler)
        return _veLookShown;
    }

    void EditLookStart(string ff, ProgramDocument d, string key, double t, VideoExport.Options o)
    {
        _veLookCts?.Cancel();
        var cts = _veLookCts = new CancellationTokenSource();
        _veLookKey = key; EditLookError = "";
        var snap = (System.Text.Json.Nodes.JsonObject)d.Data.DeepClone();
        var dir = Path.Combine(MediaFolder, ".vorschau", "bild");
        var clock = Stopwatch.StartNew();
        EditLookTask = Run();
        async Task Run()
        {
            string? png = null; string err = "";
            try
            {
                await Task.Delay(30, cts.Token); // beim Ziehen des Abspielkopfs nicht jede Zwischenstelle rechnen
                png = await Task.Run(() => VideoLook.RenderFrameAsync(ff, snap, t, dir, LookWidth, o, f => VideoProxy.ForPreview(snap, f), cts.Token), cts.Token);
                if (png is null) err = VideoLook.LastError;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { err = e.Message; }
            if (cts.IsCancellationRequested) return;
            if (png is null)
            {
                EditLookError = err.Length > 0 ? err : "FFmpeg hat kein Bild geliefert.";
                if (_veLookBusy is { } nb) { nb.Text = "Vorschau nicht möglich: " + EditLookError; nb.IsVisible = true; }
                return;
            }
            Bitmap bmp;
            try { using var s = File.OpenRead(png); bmp = new Bitmap(s); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { EditLookError = e.Message; return; }
            if (_veLooks.Count > 120) _veLooks.Clear();
            _veLooks[key] = bmp; _veLookShown = bmp;
            EditLookMs = clock.ElapsedMilliseconds;
            if (_veLookKey == key && _vePlay is null) EditLookShow(bmp);
            if (_veLooks.Count % 40 == 0) _ = Task.Run(() => VideoLook.Trim(dir));
        }
    }

    void EditLookShow(Bitmap b)
    {
        if (_vePreview is null) return;
        _vePreview.Source = b; _vePreview.IsVisible = true;
        if (_veOverlay is not null) _veOverlay.IsVisible = false; // der Titel steckt schon im fertigen Bild
        if (_veLookBusy is not null) _veLookBusy.IsVisible = false;
        EditFullSync();
    }

    // ---------- T-0695: Vollbild-Vorschau (F) ----------
    Window? _veFull; Image? _veFullImg;
    public bool EditFullscreenOpen => _veFull is not null;
    public Window? EditFullscreenWindow => _veFull;

    /// <summary>Vorschau als Vollbild (F); F oder Esc schließt, Leertaste spielt ab, Pfeile gehen bildweise.</summary>
    public void EditToggleFullscreen(ProgramDocument d)
    {
        if (_veFull is { } open) { open.Close(); return; }
        var img = new Image { Name = "ve-full-image", Stretch = Avalonia.Media.Stretch.Uniform, Source = _vePreview?.Source };
        var hint = new TextBlock { Text = "F oder Esc: zurück · Leertaste: Abspielen · ←/→: Bild für Bild", Foreground = Avalonia.Media.Brushes.Gray, FontSize = 12, Margin = new Thickness(14), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom };
        var w = new Window { Title = "Vorschau – Vollbild", Background = Avalonia.Media.Brushes.Black, WindowState = WindowState.FullScreen, Content = new Grid { Children = { img, hint } } };
        w.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Avalonia.Input.Key.F or Avalonia.Input.Key.Escape: w.Close(); break;
                case Avalonia.Input.Key.Space: EditTogglePlay(d); break;
                case Avalonia.Input.Key.Left or Avalonia.Input.Key.Right:
                    var step = (e.Key == Avalonia.Input.Key.Left ? -1 : 1) * (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift) ? 1 : 1.0 / VideoTools.Fps(d.Data));
                    ProgramRun(VK, () => d.Set("playhead", Math.Clamp(Math.Round(d.Number("playhead") + step, 3), 0, VideoEdit.Duration(d.Data)), "Abspielkopf")); break;
                default: return;
            }
            e.Handled = true;
        };
        w.Closed += (_, _) => { _veFull = null; _veFullImg = null; };
        _veFull = w; _veFullImg = img;
        w.Show(this);
    }

    void EditFullSync()
    {
        if (_veFullImg is null || _vePreview is null) return;
        if (!ReferenceEquals(_veFullImg.Source, _vePreview.Source)) _veFullImg.Source = _vePreview.Source;
        _veFullImg.InvalidateVisual();
    }

    // ---------- T-0695: Dateien auf die Zeitleiste ziehen ----------
    /// <summary>Abgelegte Dateien an der Abwurfstelle einfügen: Videos und Fotos in die Bildspur vor das Stück unter der Stelle (oder ans Ende),
    /// Ton auf die Musikspur ab der Stelle. Ein Schritt für Rückgängig. Öffentlich für Tests.</summary>
    public int EditDropFiles(IReadOnlyList<string> files, double at, bool onMusic, double? lengthForTests = null, bool? audioForTests = null)
    {
        var d = ProgramActive(VK) ?? ProgramNew(VK);
        var ok = files.Where(f => File.Exists(f) && MediaStudio.Inputs.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
        if (ok.Count == 0) throw new InvalidOperationException("Das sind keine Videos, Fotos oder Töne.");
        var made = new List<(EditClip Clip, bool Music)>();
        foreach (var f in ok)
        {
            var music = MediaStudio.IsAudio(f) || (onMusic && !MediaStudio.IsImage(f));
            double len; bool hasAudio;
            if (MediaStudio.IsImage(f)) { len = VideoEdit.ImageSeconds; hasAudio = false; }
            else
            {
                var ff = FfmpegPath;
                if (lengthForTests is null && ff is null) throw new InvalidOperationException("Für Videos und Ton fehlt FFmpeg – unter Zusatzprogramme einrichten.");
                len = lengthForTests ?? MediaStudio.Duration(ff!, f) ?? throw new InvalidOperationException("Die Länge von „" + Path.GetFileName(f) + "“ lässt sich nicht lesen.");
                hasAudio = audioForTests ?? (ff is not null && VideoEdit.HasAudioStream(ff, f));
            }
            made.Add((VideoEdit.NewClip(f, len, hasAudio), music));
        }
        var clips0 = VideoEdit.Clips(d.Data); var starts = VideoEdit.Starts(clips0);
        var index = Enumerable.Range(0, clips0.Count).FirstOrDefault(i => starts[i] + clips0[i].Length / 2 > at, clips0.Count); // vor das Stück, dessen Mitte hinter der Stelle liegt
        d.Edit(ok.Count == 1 ? "Datei abgelegt" : $"{ok.Count} Dateien abgelegt", x =>
        {
            var clips = VideoEdit.Clips(x); var pos = index; var t = Math.Max(0, at);
            foreach (var (c, music) in made)
            {
                if (music)
                {
                    VideoEdit.Add(x, c, "music"); var mi = VideoEdit.Clips(x, "music").Count - 1;
                    VideoEdit.SetMusic(x, mi, t, 1); t += c.Length;
                }
                else { clips.Insert(Math.Min(pos++, clips.Count), c); VideoEdit.Store(x, clips); }
            }
        });
        var last = made[^1];
        EditSelected = last.Music ? ("music", VideoEdit.Clips(d.Data, "music").Count - 1) : ("clips", index + made.Count(m => !m.Music) - 1);
        _progMsg[VK] = ok.Count == 1 ? $"„{Path.GetFileName(ok[0])}“ liegt bei {ClockMs(at)} auf der {(last.Music ? "Musikspur" : "Bildspur")}." : $"{ok.Count} Dateien abgelegt.";
        return ok.Count;
    }
}
