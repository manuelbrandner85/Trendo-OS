namespace Trendo.Core.Media;

/// <summary>Ton als fortlaufender Strom (verschachtelte Werte −1 … 1) – für die eigene Wiedergabe ohne Zusatzprogramm (T-0731).
/// MP3 dekodiert NLayer (MIT, schon im Projekt), WAV liest <see cref="AudioFile"/>.</summary>
public interface IPcmSource : IDisposable
{
    int Rate { get; }
    int Channels { get; }
    /// <summary>Länge in Sekunden (bei MP3 aus dem Datenstrom geschätzt).</summary>
    double Seconds { get; }
    /// <summary>Liest bis zu <paramref name="count"/> Werte (verschachtelt, alle Kanäle); 0 = Ende.</summary>
    int Read(float[] buffer, int offset, int count);
    /// <summary>Springt an eine Stelle (Sekunden, wird auf 0 … Länge begrenzt).</summary>
    void Seek(double seconds);
}

public static class PcmSource
{
    /// <summary>Formate, die Trendo selbst abspielt. Alles andere braucht den freien Player mpv.</summary>
    public static readonly string[] Playable = [".mp3", ".wav"];

    public static bool CanPlay(string path) => Playable.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static IPcmSource Open(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Tondatei nicht gefunden: " + Path.GetFileName(path));
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => new Mp3Source(path),
            ".wav" => new BufferSource(AudioFile.ReadWav(File.ReadAllBytes(path))),
            var e => throw new NotSupportedException($"„{e}“ spielt Trendo nicht selbst ab."),
        };
    }

    sealed class Mp3Source : IPcmSource
    {
        readonly NLayer.MpegFile _f;
        public Mp3Source(string path)
        {
            _f = new NLayer.MpegFile(path);
            if (_f.SampleRate <= 0 || _f.Channels <= 0) { _f.Dispose(); throw new InvalidDataException("Die MP3-Datei hat keine lesbaren Tondaten."); }
        }
        public int Rate => _f.SampleRate;
        public int Channels => _f.Channels;
        public double Seconds => _f.Duration.TotalSeconds;
        public int Read(float[] buffer, int offset, int count) => _f.ReadSamples(buffer, offset, count);
        public void Seek(double seconds) { if (_f.CanSeek) _f.Time = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, Math.Max(0, Seconds))); }
        public void Dispose() => _f.Dispose();
    }

    /// <summary>Ganze Tonspur im Speicher (WAV) als Strom.</summary>
    sealed class BufferSource(AudioBuf a) : IPcmSource
    {
        long _frame;
        public int Rate => a.Rate;
        public int Channels => a.Channels;
        public double Seconds => a.Seconds;
        public int Read(float[] buffer, int offset, int count)
        {
            var ch = a.Channels; var frames = Math.Min(count / ch, a.Length - _frame); if (frames <= 0) return 0;
            for (long i = 0; i < frames; i++) for (var c = 0; c < ch; c++) buffer[offset + i * ch + c] = a.Data[c][_frame + i];
            _frame += frames; return (int)(frames * ch);
        }
        public void Seek(double seconds) => _frame = Math.Clamp((long)Math.Round(seconds * a.Rate), 0, a.Length);
        public void Dispose() { }
    }
}
