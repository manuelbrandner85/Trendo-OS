using System.Text;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-20 (06.11.2026): Audio-Prüfung vor der Freigabe – Lautheit (BS.1770), True Peak (4× überabgetastet),
/// Übersteuerung, Format, Kanäle, Phase, Stille, Herkunft/Lizenz und Einwilligung bei KI-Stimmen. Übersteuerung, defekte Datei oder
/// Rechteverstoß sperren die Freigabe mit Grund.</summary>
public static class AudioQa
{
    public sealed record Target(string Id, string Label, double Lufs, double Tolerance, double MaxTruePeak);

    public static readonly Target[] Targets =
    [
        new("streaming", "Musik / Streaming (−14 LUFS)", -14, 1, -1),
        new("podcast", "Sprache / Podcast (−16 LUFS)", -16, 1, -1),
        new("film", "Film / Fernsehen (EBU R128, −23 LUFS)", -23, 1, -1),
    ];

    public static Target TargetOf(string id) => Targets.FirstOrDefault(t => t.Id == id) ?? Targets[0];

    /// <summary>Herkunft einer Tonquelle. KI-Stimmen brauchen eine Einwilligung der Person.</summary>
    public sealed record Source(string Name, string License, bool VoiceModel = false, bool Consent = false);

    public sealed record Measure(int Rate, int Channels, int Bits, double Seconds, double SamplePeakDb, double TruePeakDb, int ClipEvents, double DcOffset,
        double Lufs, double Correlation, double LeadSilence, double TailSilence);

    static double Db(double v) => v <= 1e-9 ? -180 : 20 * Math.Log10(v);

    /// <summary>True Peak nach BS.1770 (Näherung): 4× Überabtastung mit gefenstertem Sinc (16 Stützstellen je Phase).</summary>
    public static double TruePeak(AudioBuf a)
    {
        const int taps = 16, half = taps / 2;
        var h = new double[3][];
        for (var k = 1; k <= 3; k++)
        {
            h[k - 1] = new double[taps]; var frac = k / 4.0;
            for (var n = 0; n < taps; n++)
            {
                var t = n - half + 1 - frac; // Abstand der Stützstelle zum Zwischenpunkt
                var sinc = Math.Abs(t) < 1e-12 ? 1 : Math.Sin(Math.PI * t) / (Math.PI * t);
                var w = 0.5 * (1 + Math.Cos(Math.PI * t / (half + 0.5))); // Hann
                h[k - 1][n] = sinc * w;
            }
        }
        double peak = 0;
        foreach (var x in a.Data)
            for (var i = 0; i < x.Length; i++)
            {
                peak = Math.Max(peak, Math.Abs(x[i]));
                if (i < half - 1 || i + half >= x.Length) continue;
                for (var k = 0; k < 3; k++)
                {
                    double s = 0; var c = h[k];
                    for (var n = 0; n < taps; n++) s += x[i - half + 1 + n] * c[n];
                    peak = Math.Max(peak, Math.Abs(s));
                }
            }
        return Math.Round(Db(peak), 2);
    }

    public static Measure Analyze(AudioBuf a, int bits)
    {
        double sp = 0, dc = 0; var clips = 0;
        foreach (var x in a.Data)
        {
            double sum = 0; var run = 0;
            for (var i = 0; i < x.Length; i++)
            {
                var v = Math.Abs(x[i]); sp = Math.Max(sp, v); sum += x[i];
                if (v >= 0.999) { run++; if (run == 3) clips++; } else run = 0; // Übersteuerung: drei volle Werte hintereinander
            }
            dc = Math.Max(dc, Math.Abs(sum / Math.Max(1, x.Length)));
        }
        double corr = 1;
        if (a.Channels >= 2 && a.Length > 0)
        {
            double sl = 0, sr = 0, slr = 0; var l = a.Data[0]; var r = a.Data[1];
            for (var i = 0; i < a.Length; i++) { sl += l[i] * (double)l[i]; sr += r[i] * (double)r[i]; slr += l[i] * (double)r[i]; }
            corr = sl < 1e-9 || sr < 1e-9 ? 1 : slr / Math.Sqrt(sl * sr);
        }
        double Silence(bool fromEnd)
        {
            var n = 0; var th = 0.001; // −60 dBFS
            for (var i = 0; i < a.Length; i++) { var j = fromEnd ? a.Length - 1 - i : i; if (a.Data.Any(c => Math.Abs(c[j]) > th)) break; n++; }
            return n / (double)Math.Max(1, a.Rate);
        }
        return new Measure(a.Rate, a.Channels, bits, Math.Round(a.Seconds, 3), Math.Round(Db(sp), 2), TruePeak(a), clips, Math.Round(dc, 5),
            AudioDsp.Lufs(a), Math.Round(corr, 3), Math.Round(Silence(false), 2), Math.Round(Silence(true), 2));
    }

    /// <summary>Bittiefe aus dem WAV-Kopf (0 = unbekannt/kein WAV).</summary>
    public static int WavBits(byte[] b)
    {
        if (b.Length < 36 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF") return 0;
        for (var pos = 12; pos + 8 <= b.Length;)
        {
            var id = Encoding.ASCII.GetString(b, pos, 4); var len = BitConverter.ToInt32(b, pos + 4);
            if (id == "fmt " && pos + 8 + 16 <= b.Length) return BitConverter.ToUInt16(b, pos + 8 + 14);
            if (len < 0) break; pos += 8 + len + (len & 1);
        }
        return 0;
    }

    /// <summary>Prüfpunkte mit Ampel. Rot sperrt die Freigabe.</summary>
    public static List<FilmDelivery.Check> Check(Measure m, Target t, IReadOnlyList<Source>? sources, int expectRate = 0, int expectChannels = 0)
    {
        var r = new List<FilmDelivery.Check>();
        r.Add(m.ClipEvents > 0 ? new("rot", $"Übersteuert: {m.ClipEvents}× volle Aussteuerung (Clipping) – erst leiser mischen oder begrenzen.") : new("gruen", "Keine Übersteuerung."));
        r.Add(m.TruePeakDb > t.MaxTruePeak ? new("gelb", $"True Peak {m.TruePeakDb:0.0} dBTP über {t.MaxTruePeak:0} – beim Umwandeln (MP3/AAC) kann es verzerren.") : new("gruen", $"True Peak {m.TruePeakDb:0.0} dBTP."));
        if (double.IsNegativeInfinity(m.Lufs)) r.Add(new("rot", "Keine hörbaren Töne (stumm)."));
        else r.Add(Math.Abs(m.Lufs - t.Lufs) > t.Tolerance ? new("gelb", $"Lautheit {m.Lufs:0.0} LUFS – Ziel {t.Lufs:0} LUFS ({t.Label}).") : new("gruen", $"Lautheit {m.Lufs:0.0} LUFS passt zu „{t.Label}“."));
        if (expectRate > 0 && m.Rate != expectRate) r.Add(new("rot", $"Abtastrate {m.Rate} Hz – verlangt sind {expectRate} Hz."));
        if (expectChannels > 0 && m.Channels != expectChannels) r.Add(new("rot", $"{m.Channels} Kanäle – verlangt sind {expectChannels}."));
        r.Add(new("gruen", $"Format: {m.Rate} Hz, {m.Channels} Kanal{(m.Channels == 1 ? "" : "e")}, {(m.Bits > 0 ? m.Bits + " Bit" : "Bittiefe unbekannt")}, {m.Seconds:0.0} s."));
        if (m.Channels >= 2 && m.Correlation < 0) r.Add(new("gelb", $"Phase: Korrelation {m.Correlation:0.00} – in Mono wird es leiser oder hohl."));
        if (m.DcOffset > 0.01) r.Add(new("gelb", $"Gleichspannungsversatz {m.DcOffset:0.000} – Tiefen-weg-Filter hilft."));
        if (m.LeadSilence > 2 || m.TailSilence > 4) r.Add(new("gelb", $"Lange Stille am Anfang ({m.LeadSilence:0.0} s) oder Ende ({m.TailSilence:0.0} s)."));
        if (sources is null || sources.Count == 0) r.Add(new("rot", "Herkunft und Lizenz fehlen – ohne Angabe keine Freigabe."));
        else
            foreach (var s in sources)
            {
                if (string.IsNullOrWhiteSpace(s.License) || s.License.Trim().Equals("unbekannt", StringComparison.OrdinalIgnoreCase))
                    r.Add(new("rot", $"„{s.Name}“: Lizenz unbekannt – nicht veröffentlichen."));
                else if (s.VoiceModel && !s.Consent) r.Add(new("rot", $"„{s.Name}“: KI-Stimme ohne Einwilligung der Person – gesperrt."));
                else r.Add(new("gruen", $"„{s.Name}“: {s.License}{(s.VoiceModel ? ", Einwilligung liegt vor" : "")}."));
            }
        return r;
    }

    public sealed record Result(Measure? Measure, IReadOnlyList<FilmDelivery.Check> Checks)
    {
        public bool Blocked => Checks.Any(c => c.Light == "rot");
        public string Reason => string.Join(" ", Checks.Where(c => c.Light == "rot").Select(c => c.Text));
    }

    /// <summary>Datei prüfen; eine defekte Datei ergibt ein rotes Ergebnis statt einer Ausnahme.</summary>
    public static Result Run(string file, Target t, IReadOnlyList<Source>? sources, int expectRate = 0, int expectChannels = 0)
    {
        if (!File.Exists(file)) return new(null, [new("rot", "Datei fehlt: " + Path.GetFileName(file))]);
        AudioBuf a; var bits = 0;
        try
        {
            if (file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) { var b = File.ReadAllBytes(file); bits = WavBits(b); a = AudioFile.ReadWav(b); }
            else a = AudioFile.Read(file);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or IndexOutOfRangeException or EndOfStreamException or NotSupportedException)
        { return new(null, [new("rot", "Datei defekt oder nicht lesbar: " + ex.Message)]); }
        if (a.Length == 0) return new(null, [new("rot", "Datei enthält keine Tondaten.")]);
        return Run(a, bits, t, sources, expectRate, expectChannels);
    }

    public static Result Run(AudioBuf a, int bits, Target t, IReadOnlyList<Source>? sources, int expectRate = 0, int expectChannels = 0)
    {
        var m = Analyze(a, bits); return new(m, Check(m, t, sources, expectRate, expectChannels));
    }

    /// <summary>Messprotokoll als Markdown mit Dateien und SHA-256.</summary>
    public static string Report(string title, Target t, Result r, IEnumerable<string> files)
    {
        var sb = new StringBuilder($"# Audio-Prüfung „{title}“\n\nErstellt: {DateTime.Now:dd.MM.yyyy HH:mm} · Ziel: {t.Label}\n\n");
        sb.Append(r.Blocked ? $"**Freigabe gesperrt:** {r.Reason}\n\n" : "**Freigabe möglich.**\n\n");
        if (r.Measure is { } m)
            sb.Append($"## Messwerte\n| Wert | Ergebnis |\n|---|---|\n| Lautheit | {m.Lufs:0.0} LUFS |\n| True Peak | {m.TruePeakDb:0.0} dBTP |\n| Sample Peak | {m.SamplePeakDb:0.0} dBFS |\n| Übersteuerungen | {m.ClipEvents} |\n| Format | {m.Rate} Hz · {m.Channels} Kanäle · {m.Bits} Bit · {m.Seconds:0.00} s |\n| Phase (Korrelation) | {m.Correlation:0.00} |\n| Gleichspannung | {m.DcOffset:0.0000} |\n| Stille Anfang/Ende | {m.LeadSilence:0.0} s / {m.TailSilence:0.0} s |\n\n");
        sb.Append("## Prüfung\n");
        foreach (var c in r.Checks) sb.Append($"- {(c.Light switch { "rot" => "🔴", "gelb" => "🟡", _ => "🟢" })} {c.Text}\n");
        var fl = files.Where(File.Exists).ToList();
        if (fl.Count > 0)
        {
            sb.Append("\n## Dateien\n| Datei | Größe | SHA-256 |\n|---|---|---|\n");
            foreach (var f in fl) sb.Append($"| {Path.GetFileName(f)} | {new FileInfo(f).Length:N0} Bytes | `{FilmDelivery.Sha256(f)}` |\n");
        }
        return sb.ToString();
    }
}
