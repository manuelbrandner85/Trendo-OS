using Xunit;

namespace Trendo.App.Tests;

/// T-0306: echter mpv im Hintergrund – Steuerkanal liefert Position und Länge, Pause, Springen und das Titelende.
public class MpvAudioTests
{
    static string? Mpv() => new[] { Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\MPV Player\mpv.exe"), Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\mpv\mpv.exe"), Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\mpv\mpv.exe") }.FirstOrDefault(File.Exists);

    /// <summary>Stille als WAV (8 kHz, mono, 16 Bit).</summary>
    static void Wav(string file, double seconds)
    {
        var n = (int)(8000 * seconds); using var w = new BinaryWriter(File.Create(file));
        w.Write("RIFF"u8); w.Write(36 + n * 2); w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(8000); w.Write(16000); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(n * 2); w.Write(new byte[n * 2]);
    }

    [Fact(DisplayName = "T-0306: mpv (echt) – Abspielen meldet Position und Länge, Springen, Pause, Titelende kommt genau einmal")]
    public async Task Real_mpv()
    {
        if (Mpv() is not { } mpv) { Assert.Skip("Übersprungen – mpv ist auf diesem PC nicht installiert."); return; }
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mpv-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "stille.wav"); Wav(f, 1.5);
        using var a = new MpvAudio(mpv);
        double dur = 0, pos = 0; var ended = 0;
        a.Progress += (p, d) => { if (!double.IsNaN(d)) dur = d; if (!double.IsNaN(p)) pos = Math.Max(pos, p); };
        a.Ended += () => Interlocked.Increment(ref ended);
        a.SetVolume(0); a.Load(f);
        for (var i = 0; i < 100 && dur == 0; i++) await Task.Delay(50);
        Assert.InRange(dur, 1.4, 1.6);
        a.SetPause(true); await Task.Delay(300); var held = pos; await Task.Delay(400); Assert.InRange(pos - held, 0, 0.15); // in der Pause steht die Position (ohne Pause wären es ~0,4 s); eine letzte Meldung kurz nach dem Pausieren ist erlaubt
        a.Seek(1.2); a.SetPause(false);
        for (var i = 0; i < 100 && ended == 0; i++) await Task.Delay(50);
        Assert.Equal(1, ended); Assert.True(pos >= 1.2, $"pos={pos}");
        a.Dispose();
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }
}
