using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Wiedergabe gesperrt – mit ehrlichem Grund und was zu tun ist (z. B. „Player einrichten“). Kein stiller Fehler (T-0731).</summary>
public sealed class AudioBlockedException(string message) : InvalidOperationException(message);

/// <summary>Wiedergabe brach mitten im Titel ab (z. B. beschädigte MP3) – verständliche Meldung statt Prozessabbruch (Gegenprüfung T-0731).
/// Getrennt von <see cref="IAudioOut"/>, damit bestehende Ersatz-Player in Tests unverändert bleiben.</summary>
public interface IAudioFailure
{
    /// <summary>Titel ist wegen eines Fehlers beendet (aus einem Hintergrund-Thread); kein <see cref="IAudioOut.Ended"/>.</summary>
    event Action<string>? Failed;
}

/// <summary>Eigene Tonausgabe ohne Zusatzprogramm (T-0731): MP3/WAV dekodiert Trendo selbst, abgespielt über die Windows-Tonausgabe
/// (waveOut aus winmm.dll – gehört zu jedem Windows, ohne Admin). Meldet Position/Länge wie der mpv-Player und das Titelende genau einmal.</summary>
public sealed class WinAudio : IAudioOut, IAudioFailure
{
    public event Action<double, double>? Progress;
    public event Action? Ended;
    public event Action<string>? Failed;

    /// <summary>Gibt es auf diesem PC überhaupt eine Tonausgabe?</summary>
    public static bool Available => OperatingSystem.IsWindows() && Native.waveOutGetNumDevs() > 0;

    const int Buffers = 4; const double BufferSeconds = 0.1;
    readonly ConcurrentQueue<Action> _cmd = new();
    readonly AutoResetEvent _wake = new(false);
    Thread? _thread; volatile bool _stop; volatile float _vol = 0.8f;
    IntPtr _h; IPcmSource? _src; double _base; long _deviceSamples;

    /// <summary>Vom Gerät tatsächlich abgespielte Werte je Kanal seit dem Laden (aus waveOutGetPosition) – Beleg für echte Ausgabe.</summary>
    public long DeviceSamplesPlayed => Interlocked.Read(ref _deviceSamples);
    public int Rate => _src?.Rate ?? 0;

    public void Load(string file)
    {
        if (!OperatingSystem.IsWindows()) throw new AudioBlockedException("Die eingebaute Tonausgabe gibt es bisher nur unter Windows – für Linux bitte „Player einrichten“ (mpv).");
        if (!PcmSource.CanPlay(file)) throw new AudioBlockedException($"„{Path.GetExtension(file)}“ spielt Trendo nicht selbst ab (eingebaut: MP3 und WAV). Für weitere Formate einmal „Player einrichten“ antippen.");
        StopThread();
        IPcmSource src;
        try { src = PcmSource.Open(file); }
        catch (Exception x) when (x is InvalidDataException or InvalidOperationException or IOException or IndexOutOfRangeException or ArgumentException)
        { throw new AudioBlockedException($"„{Path.GetFileName(file)}“ ließ sich nicht lesen ({x.Message})."); }
        var fmt = new Native.WAVEFORMATEX
        {
            wFormatTag = 1, nChannels = (ushort)src.Channels, nSamplesPerSec = (uint)src.Rate, wBitsPerSample = 16,
            nBlockAlign = (ushort)(src.Channels * 2), nAvgBytesPerSec = (uint)(src.Rate * src.Channels * 2),
        };
        var r = Native.waveOutOpen(out var h, Native.WAVE_MAPPER, ref fmt, _wake.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, Native.CALLBACK_EVENT);
        if (r != 0) { src.Dispose(); throw new AudioBlockedException(r == 2 /* MMSYSERR_BADDEVICEID */ || Native.waveOutGetNumDevs() == 0 ? "Kein Tonausgabegerät gefunden – Lautsprecher oder Kopfhörer anschließen." : $"Die Windows-Tonausgabe lehnt das Format ab (Fehler {r}, {src.Rate} Hz, {src.Channels} Kanäle)."); }
        _cmd.Clear(); _seekTo = null; // Sammelstand 0.9.104: alte Pause-/Sprungbefehle wirken nie auf den neuen Titel
        _h = h; _src = src; _base = 0; _paused = false; Interlocked.Exchange(ref _deviceSamples, 0); _stop = false;
        _thread = new Thread(Run) { IsBackground = true, Name = "trendo-ton" }; _thread.Start();
    }

    public void SetPause(bool pause) => Post(() => { _paused = pause; if (pause) Native.waveOutPause(_h); else Native.waveOutRestart(_h); });
    public void Seek(double seconds) => Post(() => _seekTo = seconds);
    public void SetVolume(int percent) => _vol = Math.Clamp(percent, 0, 130) / 100f;
    public void Stop() => StopThread();
    public void Dispose() { StopThread(); _wake.Dispose(); }

    double? _seekTo; bool _paused;
    void Post(Action a) { if (_thread is not { IsAlive: true }) return; _cmd.Enqueue(a); _wake.Set(); } // Titel beendet oder abgebrochen: Befehl verwerfen

    void StopThread()
    {
        if (_thread is null) return;
        _stop = true; _wake.Set(); if (Thread.CurrentThread != _thread) _thread.Join(3000); _thread = null;
    }

    void Run()
    {
        var src = _src!; var h = _h; var ch = src.Channels;
        var frames = Math.Max(256, (int)(src.Rate * BufferSeconds)); var samples = frames * ch;
        var hdrSize = Marshal.SizeOf<Native.WAVEHDR>(); var flagsAt = (int)Marshal.OffsetOf<Native.WAVEHDR>(nameof(Native.WAVEHDR.dwFlags));
        var data = new IntPtr[Buffers]; var hdr = new IntPtr[Buffers]; var queued = new bool[Buffers];
        var f = new float[samples]; var pcm = new short[samples]; var drained = false; var lastTick = 0L; string? failed = null;
        try
        {
            for (var i = 0; i < Buffers; i++)
            {
                data[i] = Marshal.AllocHGlobal(samples * 2); hdr[i] = Marshal.AllocHGlobal(hdrSize);
                Marshal.StructureToPtr(new Native.WAVEHDR { lpData = data[i], dwBufferLength = (uint)(samples * 2) }, hdr[i], false);
                Native.waveOutPrepareHeader(h, hdr[i], (uint)hdrSize);
            }
            Report(0, src.Seconds);
            while (!_stop)
            {
                while (_cmd.TryDequeue(out var c)) c();
                if (_seekTo is { } to)
                {
                    _seekTo = null; Native.waveOutReset(h); // alle Puffer kommen als „fertig“ zurück, Gerätezähler steht wieder auf 0
                    src.Seek(to); _base = Math.Clamp(to, 0, src.Seconds); drained = false;
                    for (var i = 0; i < Buffers; i++) queued[i] = false;
                    if (_paused) Native.waveOutPause(h); // Springen in der Pause bleibt pausiert
                }
                for (var i = 0; i < Buffers; i++)
                {
                    if (queued[i] && (Marshal.ReadInt32(hdr[i], flagsAt) & Native.WHDR_DONE) == 0) continue;
                    queued[i] = false;
                    if (drained) continue;
                    var got = 0; int n;
                    while (got < samples && (n = src.Read(f, got, samples - got)) > 0) got += n;
                    if (got < samples) drained = true;
                    got -= got % ch; if (got == 0) continue;
                    var v = _vol;
                    for (var k = 0; k < got; k++) pcm[k] = (short)Math.Round(Math.Clamp(f[k] * v, -1f, 1f) * 32767);
                    Marshal.Copy(pcm, 0, data[i], got);
                    Marshal.WriteInt32(hdr[i], (int)Marshal.OffsetOf<Native.WAVEHDR>(nameof(Native.WAVEHDR.dwBufferLength)), got * 2);
                    Marshal.WriteInt32(hdr[i], flagsAt, Marshal.ReadInt32(hdr[i], flagsAt) & ~Native.WHDR_DONE);
                    if (Native.waveOutWrite(h, hdr[i], (uint)hdrSize) == 0) queued[i] = true;
                }
                var played = DevicePosition(h);
                Interlocked.Exchange(ref _deviceSamples, played);
                var now = Environment.TickCount64;
                if (now - lastTick >= 200) { lastTick = now; Report(Math.Min(src.Seconds, _base + played / (double)src.Rate), src.Seconds); }
                if (drained && !queued.Any(q => q))
                {
                    Report(src.Seconds, src.Seconds);
                    try { Ended?.Invoke(); } catch (Exception) { /* Fehler der Oberfläche ist keine beschädigte Datei */ }
                    break;
                }
                _wake.WaitOne(50);
            }
        }
        catch (Exception x) // Gegenprüfung T-0731: Eigener Thread – eine Ausnahme hier würde den ganzen Trendo-Prozess beenden.
        {
            failed = x is InvalidDataException ? x.Message + " Der Titel ist beendet."
                : "Die Wiedergabe brach ab – die Datei ist beschädigt oder nicht lesbar. Der Titel ist beendet."; // Sammelstand 0.9.104: nur Deutsch, keine Interna
        }
        finally
        {
            Native.waveOutReset(h);
            for (var i = 0; i < Buffers; i++) if (hdr[i] != IntPtr.Zero) { Native.waveOutUnprepareHeader(h, hdr[i], (uint)hdrSize); Marshal.FreeHGlobal(hdr[i]); Marshal.FreeHGlobal(data[i]); }
            Native.waveOutClose(h);
            try { src.Dispose(); } catch (Exception) { /* Aufräumen darf den Ton-Thread nicht mehr beenden */ }
            if (ReferenceEquals(_src, src)) { _src = null; _h = IntPtr.Zero; }
        }
        if (failed is not null && !_stop) try { Failed?.Invoke(failed); } catch (Exception) { /* Meldung an die Oberfläche darf den Ton-Thread nicht beenden */ }
    }

    /// <summary>Fortschritt melden – ein Fehler im Empfänger (Oberfläche) beendet weder den Ton noch gilt er als beschädigte Datei.</summary>
    void Report(double pos, double len) { try { Progress?.Invoke(pos, len); } catch (Exception) { } }

    static long DevicePosition(IntPtr h)
    {
        var t = new Native.MMTIME { wType = Native.TIME_SAMPLES };
        return Native.waveOutGetPosition(h, ref t, (uint)Marshal.SizeOf<Native.MMTIME>()) == 0 && t.wType == Native.TIME_SAMPLES ? t.u : 0;
    }

    static class Native
    {
        public const uint WAVE_MAPPER = 0xFFFFFFFF, CALLBACK_EVENT = 0x00050000, TIME_SAMPLES = 2; public const int WHDR_DONE = 1;
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        public struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public int dwFlags; public uint dwLoops; public IntPtr lpNext, reserved; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MMTIME { public uint wType; public uint u; public uint pad; }
        [DllImport("winmm.dll")] public static extern uint waveOutGetNumDevs();
        [DllImport("winmm.dll")] public static extern int waveOutOpen(out IntPtr h, uint dev, ref WAVEFORMATEX fmt, IntPtr callback, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] public static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] public static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] public static extern int waveOutWrite(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] public static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] public static extern int waveOutPause(IntPtr h);
        [DllImport("winmm.dll")] public static extern int waveOutRestart(IntPtr h);
        [DllImport("winmm.dll")] public static extern int waveOutClose(IntPtr h);
        [DllImport("winmm.dll")] public static extern int waveOutGetPosition(IntPtr h, ref MMTIME t, uint size);
    }
}

/// <summary>Wählt je Titel den Weg (T-0731): MP3/WAV spielt Trendo selbst; andere Formate über den freien Player mpv, wenn eingerichtet;
/// sonst eine ehrliche Sperre (<see cref="AudioBlockedException"/>) statt stiller Fehlfunktion.</summary>
public sealed class AutoAudio(Func<string?> mpvPath, Func<bool>? builtinAvailable = null) : IAudioOut, IAudioFailure
{
    WinAudio? _own; MpvAudio? _mpv; IAudioOut? _cur; string? _mpvFor; int _vol = 80;
    public event Action<double, double>? Progress;
    public event Action? Ended;
    public event Action<string>? Failed;
    /// <summary>Welcher Weg zuletzt gespielt hat: „eingebaut“, „mpv“ oder null.</summary>
    public string? Route => _cur is null ? null : _cur == _own ? "eingebaut" : "mpv";

    public void Load(string file)
    {
        var own = (builtinAvailable ?? (() => WinAudio.Available))() && PcmSource.CanPlay(file);
        IAudioOut next;
        if (own) next = _own ??= Hook(new WinAudio());
        else if (mpvPath() is { } mpv)
        {
            if (_mpv is null || _mpvFor != mpv) { _mpv?.Dispose(); _mpv = Hook(new MpvAudio(mpv)); _mpvFor = mpv; }
            next = _mpv;
        }
        else throw new AudioBlockedException(PcmSource.CanPlay(file)
            ? "Auf diesem PC ist keine Tonausgabe nutzbar – Lautsprecher prüfen oder „Player einrichten“ (mpv) antippen."
            : $"„{Path.GetExtension(file)}“ spielt Trendo nicht selbst ab (eingebaut: MP3 und WAV). Für dieses Format einmal „Player einrichten“ antippen – der freie Player mpv wird nach Rückfrage geladen.");
        if (_cur is not null && _cur != next) _cur.Stop();
        _cur = next; _cur.SetVolume(_vol); _cur.Load(file);
    }

    T Hook<T>(T a) where T : IAudioOut
    {
        a.Progress += (p, d) => { if (ReferenceEquals(_cur, a)) Progress?.Invoke(p, d); };
        a.Ended += () => { if (ReferenceEquals(_cur, a)) Ended?.Invoke(); };
        if (a is IAudioFailure f) f.Failed += m => { if (ReferenceEquals(_cur, a)) Failed?.Invoke(m); };
        return a;
    }

    public void SetPause(bool pause) => _cur?.SetPause(pause);
    public void Seek(double seconds) => _cur?.Seek(seconds);
    public void SetVolume(int percent) { _vol = percent; _cur?.SetVolume(percent); }
    public void Stop() => _cur?.Stop();
    public void Dispose() { _own?.Dispose(); _mpv?.Dispose(); }
}
