using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
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

    bool EditSmoothPossible => EditRate == 1 && PlayFramesOverride is null && (PreviewSourceOverride is not null || (FfmpegPath is not null && !_opt.Isolated));

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
        if (c.IsImage) { if (_veSourceKey != c.Id) { _veSource?.Dispose(); _veSource = null; _veSourceKey = c.Id; EditShowFrame(d, t); } return; }
        var key = c.Id + "|" + c.In + "|" + c.Out;
        if (_veSourceKey != key || _veSource is null)
        {
            _veSource?.Dispose();
            _veSourceKey = key; _veSourceStart = at.SourceTime;
            var rest = Math.Max(0.1, c.In + c.Length - at.SourceTime);
            _veSource = PreviewSourceOverride is { } o ? o(c, at.SourceTime, rest, fps) : FfmpegPath is { } ff ? new PreviewStream(ff, VideoProxy.ForPreview(d.Data, c.File), at.SourceTime, rest, PreviewW, PreviewH, fps) : null;
        }
        if (_veSource is null) return;
        _veBitmap ??= new WriteableBitmap(new PixelSize(PreviewW, PreviewH), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        _veBuffer ??= new byte[PreviewW * PreviewH * 4];
        if (!_veSource.TryCopy(at.SourceTime - _veSourceStart, _veBuffer, PreviewW * 4)) return;
        using (var fb = _veBitmap.Lock())
            for (var y = 0; y < PreviewH; y++) System.Runtime.InteropServices.Marshal.Copy(_veBuffer, y * PreviewW * 4, fb.Address + y * fb.RowBytes, PreviewW * 4);
        PreviewFramesShown++;
        // Avalonia zeichnet ein WriteableBitmap nur neu, wenn die Quelle neu gesetzt oder das Bild ungültig gemacht wird
        if (!ReferenceEquals(_vePreview.Source, _veBitmap)) _vePreview.Source = _veBitmap;
        _vePreview.IsVisible = true; _vePreview.InvalidateVisual();
    }

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
}
