using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Trendo.App;

/// <summary>Tonausgabe für den Musikplayer (austauschbar für Tests).</summary>
public interface IAudioOut : IDisposable
{
    void Load(string file);
    void SetPause(bool pause);
    void Seek(double seconds);
    void SetVolume(int percent);
    void Stop();
    /// <summary>Position und Länge in Sekunden (aus einem Hintergrund-Thread).</summary>
    event Action<double, double>? Progress;
    /// <summary>Titel ist zu Ende gespielt (nicht bei Wechsel oder Stopp).</summary>
    event Action? Ended;
}

/// <summary>mpv ohne Bild im Hintergrund, gesteuert über JSON-IPC (Named Pipe). Meldet Position/Länge und das Ende eines Titels.</summary>
public sealed class MpvAudio(string mpvPath) : IAudioOut
{
    Process? _proc; NamedPipeClientStream? _pipe; StreamWriter? _w; Thread? _reader;
    readonly object _lock = new(); double _pos, _dur;
    readonly string _name = $"trendo-musik-{Environment.ProcessId}-{Guid.NewGuid():N}";
    public event Action<double, double>? Progress;
    public event Action? Ended;

    void Ensure()
    {
        if (_proc is { HasExited: false } && _pipe is { IsConnected: true }) return;
        Close();
        var psi = new ProcessStartInfo(mpvPath) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "--no-video", "--idle=yes", "--no-terminal", "--really-quiet", "--audio-display=no", $"--input-ipc-server=\\\\.\\pipe\\{_name}" }) psi.ArgumentList.Add(a);
        _proc = Process.Start(psi) ?? throw new InvalidOperationException("Der Player ließ sich nicht starten.");
        var pipe = new NamedPipeClientStream(".", _name, PipeDirection.InOut, PipeOptions.Asynchronous);
        for (var i = 0; ; i++) { try { pipe.Connect(250); break; } catch (TimeoutException) when (i < 20) { } }
        _pipe = pipe; _w = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        _reader = new Thread(Read) { IsBackground = true, Name = "mpv-musik" }; _reader.Start();
        Send("observe_property", 1, "time-pos"); Send("observe_property", 2, "duration");
    }

    void Read()
    {
        try
        {
            using var r = new StreamReader(_pipe!, Encoding.UTF8);
            while (r.ReadLine() is { } line)
            {
                using var d = JsonDocument.Parse(line); var e = d.RootElement;
                if (!e.TryGetProperty("event", out var ev)) continue;
                switch (ev.GetString())
                {
                    case "property-change" when e.TryGetProperty("name", out var n):
                        var v = e.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Number ? data.GetDouble() : double.NaN;
                        if (n.GetString() == "time-pos") _pos = v; else if (n.GetString() == "duration") _dur = v;
                        Progress?.Invoke(_pos, _dur); break;
                    case "end-file" when e.TryGetProperty("reason", out var why) && why.GetString() == "eof":
                        Ended?.Invoke(); break;
                }
            }
        }
        catch (Exception x) when (x is IOException or ObjectDisposedException or JsonException or InvalidOperationException) { }
    }

    void Send(params object[] command)
    {
        lock (_lock) { try { _w?.WriteLine(JsonSerializer.Serialize(new { command })); } catch (Exception x) when (x is IOException or ObjectDisposedException) { } }
    }

    public void Load(string file) { Ensure(); Send("loadfile", file, "replace"); Send("set_property", "pause", false); }
    public void SetPause(bool pause) => Send("set_property", "pause", pause);
    public void Seek(double seconds) => Send("seek", seconds, "absolute");
    public void SetVolume(int percent) => Send("set_property", "volume", Math.Clamp(percent, 0, 130));
    public void Stop() => Send("stop");

    void Close()
    {
        try { _w?.Dispose(); } catch (IOException) { }
        try { _pipe?.Dispose(); } catch (IOException) { }
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch (Exception x) when (x is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        _proc?.Dispose(); _proc = null; _pipe = null; _w = null;
    }
    public void Dispose() => Close();
}
