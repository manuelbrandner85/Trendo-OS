using System.Diagnostics;

namespace Trendo.Core.Media;

/// <summary>T-0732 (Audit-Restliste Rang 2): Aufnahme, die läuft, bis „Stopp“ gedrückt wird – mit Pegel während der Aufnahme.
/// Ein FFmpeg-Lauf schreibt zwei Ausgaben: die Aufnahme als WAV (44,1 kHz, mono, 16 Bit) und daneben einen kleinen Rohdatenstrom (8 kHz) über die
/// Standardausgabe, aus dem Trendo Pegel und Dauer liest. „Stopp“ schickt FFmpeg ein „q“ – dann schließt FFmpeg die WAV-Datei sauber ab.
/// Die Eingabe ist frei wählbar (<see cref="DshowInput"/> für ein Windows-Mikrofon; Tests nehmen eine FFmpeg-Tonquelle, das Werkzeug bleibt echt).</summary>
public sealed class MicRecorder : IDisposable
{
    /// <summary>Abtastrate des Pegel-Stroms.</summary>
    public const int MeterRate = 8000;

    readonly Process _p;
    readonly Task _pump;
    readonly System.Text.StringBuilder _err = new();
    long _samples;
    int _levelBits;      // letzter Block, Spitze 0…1 als float-Bits
    int _maxBits;        // höchste Spitze der ganzen Aufnahme
    bool _stopped;

    /// <summary>Zieldatei (WAV).</summary>
    public string Wav { get; }

    MicRecorder(Process p, string wav)
    {
        _p = p; Wav = wav;
        p.ErrorDataReceived += (_, e) => { if (e.Data is { } l) lock (_err) { if (_err.Length < 8000) _err.AppendLine(l); } };
        p.BeginErrorReadLine();
        _pump = Task.Run(Pump);
    }

    /// <summary>Windows-Mikrofon (DirectShow) als Eingabe.</summary>
    public static string[] DshowInput(string micId) => ["-f", "dshow", "-i", "audio=" + micId];

    /// <summary>FFmpeg-Argumente: <paramref name="input"/> → WAV-Datei und Pegel-Strom auf die Standardausgabe.</summary>
    public static string[] Args(IReadOnlyList<string> input, string wav) =>
    [
        "-hide_banner", "-nostats", "-loglevel", "error", "-y", .. input,
        "-map", "0:a", "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", wav,
        "-map", "0:a", "-ac", "1", "-ar", MeterRate.ToString(System.Globalization.CultureInfo.InvariantCulture), "-c:a", "pcm_s16le", "-f", "s16le", "pipe:1",
    ];

    /// <summary>Aufnahme starten. Wirft mit deutscher Meldung, wenn FFmpeg nicht startet.</summary>
    public static MicRecorder Start(string ffmpeg, IReadOnlyList<string> input, string wav)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(wav))!);
        var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in Args(input, wav)) psi.ArgumentList.Add(a);
        Process p;
        try { p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg startet nicht."); }
        catch (System.ComponentModel.Win32Exception ex) { throw new InvalidOperationException("FFmpeg startet nicht: " + ex.Message, ex); }
        return new MicRecorder(p, wav);
    }

    void Pump()
    {
        var s = _p.StandardOutput.BaseStream; var buf = new byte[1024]; var carry = -1;
        try
        {
            int n;
            while ((n = s.Read(buf, 0, buf.Length)) > 0)
            {
                float peak = 0; var count = 0; var i = 0;
                if (carry >= 0) { peak = Math.Abs((short)(carry | (buf[0] << 8)) / 32768f); count = 1; i = 1; carry = -1; }
                for (; i + 1 < n; i += 2) { var v = Math.Abs((short)(buf[i] | (buf[i + 1] << 8)) / 32768f); if (v > peak) peak = v; count++; }
                if (i < n) carry = buf[i];
                Interlocked.Add(ref _samples, count);
                Interlocked.Exchange(ref _levelBits, BitConverter.SingleToInt32Bits(peak));
                if (peak > BitConverter.Int32BitsToSingle(Volatile.Read(ref _maxBits))) Interlocked.Exchange(ref _maxBits, BitConverter.SingleToInt32Bits(peak));
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    /// <summary>Bisher aufgenommene Zeit in Sekunden (aus dem Pegel-Strom gezählt, nicht aus der Uhr).</summary>
    public double Seconds => Interlocked.Read(ref _samples) / (double)MeterRate;
    /// <summary>Spitze des letzten Blocks (rund 60 ms), 0…1.</summary>
    public double Level => BitConverter.Int32BitsToSingle(Volatile.Read(ref _levelBits));
    /// <summary>Höchste Spitze seit dem Start, 0…1 (≥ 0,99: übersteuert).</summary>
    public double MaxLevel => BitConverter.Int32BitsToSingle(Volatile.Read(ref _maxBits));
    /// <summary>Pegel in dBFS (Stille: −∞).</summary>
    public static double Db(double level) => level <= 1e-6 ? double.NegativeInfinity : 20 * Math.Log10(level);
    /// <summary>Läuft FFmpeg noch?</summary>
    public bool Running { get { try { return !_p.HasExited; } catch (InvalidOperationException) { return false; } } }
    /// <summary>Fehlermeldungen von FFmpeg (z. B. Mikrofon belegt).</summary>
    public string Errors { get { lock (_err) return _err.ToString().Trim(); } }

    /// <summary>Aufnahme beenden: FFmpeg schließt die WAV-Datei ab. Liefert die Datei oder null (dann steht der Grund in <see cref="Errors"/>).</summary>
    public string? Stop(int waitMs = 8000)
    {
        if (!_stopped)
        {
            _stopped = true;
            try { if (!_p.HasExited) { _p.StandardInput.Write('q'); _p.StandardInput.Flush(); _p.StandardInput.Close(); } }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException) { }
            if (!_p.WaitForExit(waitMs)) { try { _p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } _p.WaitForExit(3000); }
            _pump.Wait(3000);
        }
        return File.Exists(Wav) && new FileInfo(Wav).Length > 44 ? Wav : null;
    }

    public void Dispose()
    {
        if (!_stopped) Stop(3000);
        _p.Dispose();
    }
}
