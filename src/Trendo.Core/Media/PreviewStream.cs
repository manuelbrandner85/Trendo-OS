using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;

namespace Trendo.Core.Media;

/// <summary>Quelle für Vorschaubilder eines Stücks (austauschbar für Tests). Sekunden zählen ab dem Start der Quelle.</summary>
public interface IPreviewSource : IDisposable
{
    /// <summary>Kopiert das Bild zur Zeit <paramref name="seconds"/> als BGRA nach <paramref name="target"/>. false: gerade kein neues Bild.</summary>
    bool TryCopy(double seconds, Span<byte> target, int stride);
    /// <summary>ffmpeg ließ sich nicht starten oder ist mit Fehler beendet, bevor ein Bild kam.</summary>
    bool Failed { get; }
}

/// <summary>Pilot 4.0, V1 (09.11.2026): liest ein Stück über ffmpeg fortlaufend als Rohbilder. Vorher wurden alle Bilder vorab als JPG entpackt, mit 6 Bildern pro Sekunde
/// und blockierter Oberfläche. Ein Hintergrund-Thread dekodiert und hält nur wenige Bilder bereit; die Wiedergabe nimmt immer das zur Uhr passende Bild und verwirft zu alte.</summary>
public sealed class PreviewStream : IPreviewSource
{
    const int Buffered = 6;
    readonly int _w, _h, _fps, _size;
    readonly Channel<(byte[] Data, int Index)> _frames = Channel.CreateBounded<(byte[], int)>(new BoundedChannelOptions(Buffered) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    readonly CancellationTokenSource _cts = new();
    readonly Thread? _thread;
    Process? _proc;
    (byte[] Data, int Index)? _held;
    volatile bool _anyFrame, _failed;

    public bool Failed => _failed;

    public static IReadOnlyList<string> Args(string file, double start, double length, int width, int height, int fps)
    {
        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        return ["-hide_banner", "-loglevel", "error", "-nostdin", "-ss", F(start), "-t", F(length), "-i", file, "-an", "-sn",
            "-vf", $"fps={fps},scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2",
            "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1"];
    }

    public PreviewStream(string ffmpeg, string file, double start, double length, int width, int height, int fps)
    {
        (_w, _h, _fps) = (width, height, fps); _size = width * height * 4;
        var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = false };
        foreach (var a in Args(file, start, length, width, height, fps)) psi.ArgumentList.Add(a);
        try { _proc = Process.Start(psi); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { _failed = true; return; }
        if (_proc is null) { _failed = true; return; }
        _thread = new Thread(Pump) { IsBackground = true, Name = "vorschau-bilder" };
        _thread.Start();
    }

    void Pump()
    {
        var input = _proc!.StandardOutput.BaseStream;
        try
        {
            for (var index = 0; !_cts.IsCancellationRequested; index++)
            {
                var buf = ArrayPool<byte>.Shared.Rent(_size);
                var have = 0;
                while (have < _size)
                {
                    var n = input.Read(buf, have, _size - have);
                    if (n == 0) { ArrayPool<byte>.Shared.Return(buf); if (!_anyFrame) _failed = true; return; }
                    have += n;
                }
                _anyFrame = true;
                if (!_frames.Writer.WaitToWriteAsync(_cts.Token).AsTask().GetAwaiter().GetResult()) { ArrayPool<byte>.Shared.Return(buf); return; }
                _frames.Writer.TryWrite((buf, index));
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException) { }
        finally { _frames.Writer.TryComplete(); }
    }

    public bool TryCopy(double seconds, Span<byte> target, int stride)
    {
        var want = (int)Math.Floor(Math.Max(0, seconds) * _fps);
        (byte[] Data, int Index)? best = null;
        if (_held is { } held)
        {
            if (held.Index > want) return false; // das nächste Bild ist noch nicht dran
            best = held; _held = null;
        }
        while ((best is null || best.Value.Index < want) && _frames.Reader.TryRead(out var f))
        {
            if (f.Index > want) { _held = f; break; }
            if (best is { } old) ArrayPool<byte>.Shared.Return(old.Data);
            best = f;
        }
        if (best is not { } frame) return false;
        var row = _w * 4;
        if (stride == row) frame.Data.AsSpan(0, _size).CopyTo(target);
        else for (var y = 0; y < _h; y++) frame.Data.AsSpan(y * row, row).CopyTo(target[(y * stride)..]);
        ArrayPool<byte>.Shared.Return(frame.Data);
        return true;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { if (_proc is { HasExited: false }) _proc.Kill(true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        _thread?.Join(1000);
        _proc?.Dispose();
        while (_frames.Reader.TryRead(out var f)) ArrayPool<byte>.Shared.Return(f.Data);
        if (_held is { } h) { ArrayPool<byte>.Shared.Return(h.Data); _held = null; }
        _cts.Dispose();
    }
}
