using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Tonspur im Speicher: je Kanal ein Feld mit Werten von −1 bis 1.</summary>
public sealed class AudioBuf(int rate, float[][] data)
{
    public int Rate { get; } = rate;
    public float[][] Data { get; private set; } = data;
    public int Channels => Data.Length;
    public int Length => Data.Length == 0 ? 0 : Data[0].Length;
    public double Seconds => Rate == 0 ? 0 : Length / (double)Rate;
    public static AudioBuf Silence(double seconds, int rate = 44100, int channels = 2) =>
        new(rate, Enumerable.Range(0, channels).Select(_ => new float[Math.Max(0, (int)Math.Round(seconds * rate))]).ToArray());
    public void Replace(float[][] d) => Data = d;
    public AudioBuf Slice(int a, int b) { a = Math.Clamp(a, 0, Length); b = Math.Clamp(b, a, Length); return new(Rate, Data.Select(c => c[a..b]).ToArray()); }
}

/// <summary>Tondateien lesen und schreiben – selbst gebaut statt Audacity (Nachbau-Plan 04.11.2026).
/// WAV (8/16/24/32 Bit, Gleitkomma) und MP3 (NLayer, MIT) liest Trendo selbst; gespeichert wird WAV. MP3/FLAC/OGG schreibt FFmpeg, falls vorhanden.</summary>
public static class AudioFile
{
    public static readonly string[] Readable = [".wav", ".mp3"];

    public static AudioBuf Read(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Tondatei nicht gefunden: " + Path.GetFileName(path));
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".wav" => ReadWav(File.ReadAllBytes(path)),
            ".mp3" => ReadMp3(path),
            var e => throw new InvalidOperationException($"„{e}“ kann Trendo noch nicht selbst öffnen – bitte als WAV oder MP3 umwandeln (Programm „Umwandeln“)."),
        };
    }

    public static AudioBuf ReadWav(byte[] b)
    {
        if (b.Length < 12 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WAVE") throw new InvalidOperationException("Das ist keine gültige WAV-Datei.");
        int fmt = 0, ch = 0, rate = 0, bits = 0, pos = 12; int dataAt = -1, dataLen = 0;
        while (pos + 8 <= b.Length)
        {
            var id = Encoding.ASCII.GetString(b, pos, 4); var len = BitConverter.ToInt32(b, pos + 4); var body = pos + 8;
            if (len < 0) break;
            if (id == "fmt ")
            {
                fmt = BitConverter.ToUInt16(b, body); ch = BitConverter.ToUInt16(b, body + 2); rate = BitConverter.ToInt32(b, body + 4); bits = BitConverter.ToUInt16(b, body + 14);
                if (fmt == 0xFFFE && len >= 26) fmt = BitConverter.ToUInt16(b, body + 24); // WAVE_FORMAT_EXTENSIBLE: Untertyp
            }
            else if (id == "data") { dataAt = body; dataLen = Math.Min(len, b.Length - body); break; }
            pos = body + len + (len & 1);
        }
        if (dataAt < 0 || ch < 1 || rate < 1) throw new InvalidOperationException("Die WAV-Datei hat keine lesbaren Tondaten.");
        if (!(fmt == 1 && bits is 8 or 16 or 24 or 32) && !(fmt == 3 && bits == 32)) throw new InvalidOperationException($"WAV-Format {fmt}/{bits} Bit wird nicht unterstützt.");
        var step = bits / 8; var n = dataLen / (step * ch);
        var data = Enumerable.Range(0, ch).Select(_ => new float[n]).ToArray();
        for (var i = 0; i < n; i++)
            for (var c = 0; c < ch; c++)
            {
                var o = dataAt + (i * ch + c) * step;
                data[c][i] = (fmt, bits) switch
                {
                    (3, _) => BitConverter.ToSingle(b, o),
                    (_, 8) => (b[o] - 128) / 128f,
                    (_, 16) => BitConverter.ToInt16(b, o) / 32768f,
                    (_, 24) => ((b[o] | b[o + 1] << 8 | (sbyte)b[o + 2] << 16)) / 8388608f,
                    _ => BitConverter.ToInt32(b, o) / 2147483648f,
                };
            }
        return new AudioBuf(rate, data);
    }

    static AudioBuf ReadMp3(string path)
    {
        using var f = new NLayer.MpegFile(path);
        var ch = f.Channels; var all = new List<float>(); var buf = new float[ch * 4096]; int got;
        while ((got = f.ReadSamples(buf, 0, buf.Length)) > 0) for (var i = 0; i < got; i++) all.Add(buf[i]);
        var n = all.Count / ch; var data = Enumerable.Range(0, ch).Select(_ => new float[n]).ToArray();
        for (var i = 0; i < n; i++) for (var c = 0; c < ch; c++) data[c][i] = all[i * ch + c];
        return new AudioBuf(f.SampleRate, data);
    }

    public static void WriteWav(AudioBuf a, string path, int bits = 16)
    {
        if (bits is not (16 or 24)) throw new ArgumentException("16 oder 24 Bit.");
        var step = bits / 8; var len = a.Length * a.Channels * step;
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8); w.Write(36 + len); w.Write("WAVE"u8); w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)a.Channels); w.Write(a.Rate);
        w.Write(a.Rate * a.Channels * step); w.Write((short)(a.Channels * step)); w.Write((short)bits); w.Write("data"u8); w.Write(len);
        for (var i = 0; i < a.Length; i++)
            for (var c = 0; c < a.Channels; c++)
            {
                var v = Math.Clamp(a.Data[c][i], -1f, 1f);
                if (bits == 16) w.Write((short)Math.Round(v * 32767));
                else { var x = (int)Math.Round(v * 8388607); w.Write((byte)x); w.Write((byte)(x >> 8)); w.Write((byte)(x >> 16)); }
            }
    }
}

/// <summary>Nicht-zerstörende Bearbeitung: Das Dokument merkt sich die Quelle und eine Liste von Schritten (Zeiten in Sekunden).
/// Rückgängig/Wiederholen kommt damit aus der Programm-Hülle; die Originaldatei bleibt unangetastet.</summary>
public static class AudioEdit
{
    /// <summary>Alle Schritte mit deutschem Namen – Kennung, Anzeige, braucht eine Auswahl.</summary>
    public static readonly (string Op, string Label, bool NeedsSel)[] Ops =
    [
        ("cut", "Löschen", true), ("keep", "Zuschneiden auf Auswahl", true), ("silence", "Stille", true), ("duplicate", "Duplizieren", true),
        ("gain", "Lautstärke", false), ("fade-in", "Einblenden", false), ("fade-out", "Ausblenden", false), ("normalize", "Normalisieren", false),
        ("reverse", "Umkehren", false), ("gate", "Rauschen unterdrücken", false), ("echo", "Echo", false), ("highpass", "Tiefen weg (Trittschall)", false),
        ("lowpass", "Höhen weg (Zischen)", false), ("mono", "Zu Mono", false), ("speed", "Tempo und Tonhöhe", false), ("insert", "Datei einfügen", false),
        ("insert-silence", "Stille einfügen", false),
        // Vorbild-Funktionen (Audacity, Audition, iZotope RX – 04.11.2026)
        ("denoise", "Rauschen entfernen (Profil)", false), ("dehum", "Brummen entfernen", false), ("truncate", "Stille kürzen", false),
        ("loudness", "Lautheit angleichen (LUFS)", false), ("compress", "Kompressor", false),
        // Profi 25 (05.11.2026): spektral retuschieren wie iZotope RX
        ("spectral", "Spektral absenken", true),
        // Paket 01 S4-06 (07.11.2026): Ergebnis eines Plugins (abgeschottet berechnet) ersetzt den Bereich
        ("plugin", "Plugin angewendet", false),
    ];

    public static string Label(string op) => Ops.FirstOrDefault(o => o.Op == op).Label ?? op;

    public static JsonObject New(string? source, double silenceSeconds = 0) =>
        new() { ["source"] = source ?? "", ["length"] = silenceSeconds, ["ops"] = new JsonArray() };

    public static JsonObject Op(string op, double a = 0, double b = 0, double v = 0, string? file = null, double pa = 0, double pb = 0)
    {
        if (!Ops.Any(o => o.Op == op)) throw new ArgumentException("Unbekannter Schritt: " + op);
        var o = new JsonObject { ["op"] = op, ["a"] = Math.Round(a, 4), ["b"] = Math.Round(b, 4), ["v"] = v };
        if (file is not null) o["file"] = file;
        if (pb > pa) { o["pa"] = Math.Round(pa, 4); o["pb"] = Math.Round(pb, 4); } // Rauschprofil (Zeiten in der Spur zum Zeitpunkt des Schritts)
        return o;
    }

    /// <summary>Spektral retuschieren (RX „Attenuate“): im Zeitbereich a–b nur die Frequenzen fLo–fHi um <paramref name="gainDb"/> absenken – z. B. Husten, Handyklingeln, Pfeifton.</summary>
    public static JsonObject SpectralOp(double a, double b, double fLo, double fHi, double gainDb)
    {
        if (b <= a) throw new InvalidOperationException("Erst im Spektrogramm einen Bereich aufziehen (Zeit und Frequenz).");
        if (fHi < fLo) (fLo, fHi) = (fHi, fLo);
        if (fHi - fLo < 10) throw new InvalidOperationException("Der Frequenzbereich ist zu schmal – im Spektrogramm auch nach oben oder unten ziehen.");
        var o = Op("spectral", a, b, Math.Clamp(gainDb, -80, 0)); o["fl"] = Math.Round(Math.Max(0, fLo)); o["fh"] = Math.Round(fHi); return o;
    }

    /// <summary>Schritt anhängen. Ohne Auswahl (a == b) wirken Effekte auf die ganze Spur.</summary>
    public static void Add(JsonObject doc, JsonObject op) => ((doc["ops"] as JsonArray) ?? (JsonArray)(doc["ops"] = new JsonArray())).Add(op);

    static readonly Lock CacheLock = new(); static string? _key; static AudioBuf? _cached;

    public static string Key(JsonObject doc) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(doc["source"]?.ToJsonString() + doc["length"]?.ToJsonString() + doc["tracks"]?.ToJsonString() + doc["ops"]?.ToJsonString())))[..16];

    /// <summary>Ergebnis berechnen: Quelle lesen, Schritte der Reihe nach anwenden (das letzte Ergebnis wird zwischengespeichert).</summary>
    public static AudioBuf Render(JsonObject doc)
    {
        var key = Key(doc);
        lock (CacheLock) if (_key == key && _cached is not null) return Copy(_cached);
        var src = doc["source"]?.GetValue<string>() ?? "";
        var a = src.Length > 0 ? AudioFile.Read(src) : AudioMulti.IsMulti(doc) ? AudioMulti.Mix(doc) : AudioBuf.Silence(ProgramNum(doc["length"], 10));
        foreach (var o in (doc["ops"] as JsonArray ?? []).OfType<JsonObject>()) Apply(a, o);
        lock (CacheLock) { _key = key; _cached = Copy(a); }
        return a;
    }

    static double ProgramNum(JsonNode? n, double f) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : f;
    static AudioBuf Copy(AudioBuf a) => new(a.Rate, a.Data.Select(c => (float[])c.Clone()).ToArray());

    /// <summary>Bereich in Abtastwerten; ohne Auswahl die ganze Spur.</summary>
    static (int A, int B) Range(AudioBuf x, double a, double b)
    {
        if (b <= a) return (0, x.Length);
        return (Math.Clamp((int)Math.Round(a * x.Rate), 0, x.Length), Math.Clamp((int)Math.Round(b * x.Rate), 0, x.Length));
    }

    public static void Apply(AudioBuf x, JsonObject o)
    {
        var op = o["op"]?.GetValue<string>() ?? ""; double a = ProgramNum(o["a"], 0), b = ProgramNum(o["b"], 0), v = ProgramNum(o["v"], 0);
        var (s, e) = Range(x, a, b);
        switch (op)
        {
            case "cut": x.Replace(x.Data.Select(c => c[..s].Concat(c[e..]).ToArray()).ToArray()); break;
            case "keep": x.Replace(x.Data.Select(c => c[s..e]).ToArray()); break;
            case "silence": foreach (var c in x.Data) Array.Clear(c, s, e - s); break;
            case "duplicate": x.Replace(x.Data.Select(c => c[..e].Concat(c[s..e]).Concat(c[e..]).ToArray()).ToArray()); break;
            case "gain": { var g = (float)Math.Pow(10, v / 20); foreach (var c in x.Data) for (var i = s; i < e; i++) c[i] *= g; break; }
            case "fade-in": foreach (var c in x.Data) for (var i = s; i < e; i++) c[i] *= Curve((i - s) / (float)Math.Max(1, e - s)); break;
            case "fade-out": foreach (var c in x.Data) for (var i = s; i < e; i++) c[i] *= Curve(1 - (i - s) / (float)Math.Max(1, e - s)); break;
            case "normalize":
            {
                var peak = 0f; foreach (var c in x.Data) for (var i = s; i < e; i++) peak = Math.Max(peak, Math.Abs(c[i]));
                if (peak < 1e-6f) break;
                var g = (float)(Math.Pow(10, (v == 0 ? -1 : v) / 20) / peak); foreach (var c in x.Data) for (var i = s; i < e; i++) c[i] *= g; break;
            }
            case "reverse": foreach (var c in x.Data) Array.Reverse(c, s, e - s); break;
            case "gate": Gate(x, s, e, v == 0 ? -45 : v); break;
            case "echo":
            {
                var d = (int)(x.Rate * (v <= 0 ? 0.3 : v)); foreach (var c in x.Data) for (var i = e - 1; i >= s + d; i--) c[i] += c[i - d] * 0.4f;
                break;
            }
            case "highpass": Biquad(x, s, e, v <= 0 ? 80 : v, high: true); break;
            case "lowpass": Biquad(x, s, e, v <= 0 ? 8000 : v, high: false); break;
            case "mono":
            {
                if (x.Channels < 2) break;
                var m = new float[x.Length]; for (var i = 0; i < m.Length; i++) { var sum = 0f; foreach (var c in x.Data) sum += c[i]; m[i] = sum / x.Channels; }
                x.Replace([m, (float[])m.Clone()]); break;
            }
            case "speed":
            {
                var f = v <= 0 ? 1 : Math.Clamp(v, 0.25, 4); if (Math.Abs(f - 1) < 1e-6) break;
                x.Replace(x.Data.Select(c => c[..s].Concat(Resample(c[s..e], f)).Concat(c[e..]).ToArray()).ToArray()); break;
            }
            case "insert":
            {
                var f = o["file"]?.GetValue<string>() ?? throw new InvalidOperationException("Keine Datei zum Einfügen.");
                var ins = Match(AudioFile.Read(f), x.Rate, x.Channels); var at = Math.Clamp((int)Math.Round(a * x.Rate), 0, x.Length);
                x.Replace(x.Data.Select((c, k) => c[..at].Concat(ins.Data[k]).Concat(c[at..]).ToArray()).ToArray()); break;
            }
            case "plugin":
            {
                var f = o["file"]?.GetValue<string>() ?? throw new InvalidOperationException("Keine Plugin-Ausgabe.");
                if (!File.Exists(f)) throw new InvalidOperationException("Die Plugin-Ausgabe fehlt: " + Path.GetFileName(f));
                var res = Match(AudioFile.Read(f), x.Rate, x.Channels);
                x.Replace(x.Data.Select((c, k) => c[..s].Concat(res.Data[k]).Concat(c[e..]).ToArray()).ToArray()); break;
            }
            case "insert-silence":
            {
                var at = Math.Clamp((int)Math.Round(a * x.Rate), 0, x.Length); var n = (int)(x.Rate * Math.Max(0, v == 0 ? 1 : v));
                x.Replace(x.Data.Select(c => c[..at].Concat(new float[n]).Concat(c[at..]).ToArray()).ToArray()); break;
            }
            case "denoise":
            {
                double pa = ProgramNum(o["pa"], 0), pb = ProgramNum(o["pb"], 0);
                if (pb <= pa) throw new InvalidOperationException("Für „Rauschen entfernen“ zuerst ein Stück mit reinem Rauschen als Profil merken.");
                var (ps, pe) = Range(x, pa, pb);
                foreach (var c in x.Data) AudioDsp.Denoise(c, s, e, AudioDsp.NoiseProfile(c, ps, pe), v <= 0 ? 18 : v);
                break;
            }
            case "dehum": AudioDsp.Dehum(x, s, e, v is 60 ? 60 : 50); break;
            case "truncate": { var (d, _) = AudioDsp.TruncateSilence(x, v == 0 ? -45 : v); x.Replace(d); break; }
            case "loudness":
            {
                var now = AudioDsp.Lufs(x); if (double.IsInfinity(now)) break;
                var gain = (float)Math.Pow(10, ((v == 0 ? -16 : v) - now) / 20);
                var peak = x.Data.Max(c => c.Max(Math.Abs)) * gain;
                if (peak > 0.89f) gain *= 0.89f / peak; // Spitzen unter −1 dBFS halten
                foreach (var c in x.Data) for (var i = 0; i < c.Length; i++) c[i] *= gain;
                break;
            }
            case "compress": AudioDsp.Compress(x, s, e, v == 0 ? -20 : v); break;
            case "spectral": foreach (var c in x.Data) AudioDsp.SpectralGain(c, x.Rate, s, e, ProgramNum(o["fl"], 0), ProgramNum(o["fh"], x.Rate / 2.0), v == 0 ? -30 : v); break;
            default: throw new InvalidOperationException("Unbekannter Schritt: " + op);
        }
    }

    // gleichmäßig hörbare Blende (Viertelsinus statt geradlinig)
    static float Curve(float t) => (float)Math.Sin(Math.Clamp(t, 0, 1) * Math.PI / 2);

    /// <summary>Rauschtor mit weicher Hüllkurve: leise Stellen (Rauschen zwischen Wörtern) werden abgesenkt, Sprache bleibt.</summary>
    static void Gate(AudioBuf x, int s, int e, double thresholdDb)
    {
        var th = (float)Math.Pow(10, thresholdDb / 20); var win = Math.Max(1, x.Rate / 100); // 10 ms
        float env = 0, gain = 1; var att = 1 - (float)Math.Exp(-1.0 / (x.Rate * 0.005)); var rel = 1 - (float)Math.Exp(-1.0 / (x.Rate * 0.08));
        for (var i = s; i < e; i++)
        {
            var lvl = 0f; foreach (var c in x.Data) lvl = Math.Max(lvl, Math.Abs(c[i]));
            env += (lvl - env) * (lvl > env ? att : 1f / win);
            var target = env >= th ? 1f : 0.06f; // −24 dB statt völliger Stille: klingt natürlicher
            gain += (target - gain) * (target > gain ? att : rel);
            foreach (var c in x.Data) c[i] *= gain;
        }
    }

    /// <summary>Filter zweiter Ordnung (RBJ-Kochbuch, Q = 0,707).</summary>
    static void Biquad(AudioBuf x, int s, int e, double hz, bool high)
    {
        hz = Math.Clamp(hz, 10, x.Rate / 2.0 - 100); var w = 2 * Math.PI * hz / x.Rate; var al = Math.Sin(w) / (2 * 0.7071); var cw = Math.Cos(w);
        double b0 = high ? (1 + cw) / 2 : (1 - cw) / 2, b1 = high ? -(1 + cw) : 1 - cw, b2 = b0, a0 = 1 + al, a1 = -2 * cw, a2 = 1 - al;
        b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
        foreach (var c in x.Data)
        {
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (var i = s; i < e; i++) { double x0 = c[i], y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2; x2 = x1; x1 = x0; y2 = y1; y1 = y0; c[i] = (float)y0; }
        }
    }

    static float[] Resample(float[] c, double factor)
    {
        var n = (int)(c.Length / factor); var r = new float[n];
        for (var i = 0; i < n; i++) { var p = i * factor; var k = (int)p; var t = (float)(p - k); r[i] = k + 1 < c.Length ? c[k] * (1 - t) + c[k + 1] * t : c[Math.Min(k, c.Length - 1)]; }
        return r;
    }

    /// <summary>Eingefügte Datei an Abtastrate und Kanalzahl der Spur anpassen.</summary>
    public static AudioBuf Match(AudioBuf a, int rate, int channels)
    {
        var d = a.Rate == rate ? a.Data : a.Data.Select(c => Resample(c, a.Rate / (double)rate)).ToArray();
        var outD = Enumerable.Range(0, channels).Select(k => (float[])d[Math.Min(k, d.Length - 1)].Clone()).ToArray();
        if (d.Length > channels && channels == 1) { var m = outD[0]; for (var i = 0; i < m.Length; i++) { var s = 0f; foreach (var c in d) s += c[i]; m[i] = s / d.Length; } }
        return new AudioBuf(rate, outD);
    }

    /// <summary>Wellenform: je Spalte kleinster und größter Wert (alle Kanäle).</summary>
    public static (float Min, float Max)[] Peaks(AudioBuf a, int columns)
    {
        columns = Math.Max(1, columns); var r = new (float, float)[columns];
        if (a.Length == 0) return r;
        for (var k = 0; k < columns; k++)
        {
            int s = (int)((long)k * a.Length / columns), e = Math.Max(s + 1, (int)((long)(k + 1) * a.Length / columns)); float lo = 0, hi = 0;
            var stride = Math.Max(1, (e - s) / 256); // große Spuren: Stichproben statt jedes Werts
            foreach (var c in a.Data) for (var i = s; i < e && i < c.Length; i += stride) { lo = Math.Min(lo, c[i]); hi = Math.Max(hi, c[i]); }
            r[k] = (lo, hi);
        }
        return r;
    }

    /// <summary>Lautester Wert in dBFS (Anzeige und Prüfung).</summary>
    public static double PeakDb(AudioBuf a, double from = 0, double to = 0)
    {
        var (s, e) = Range(a, from, to); var p = 0f; foreach (var c in a.Data) for (var i = s; i < e; i++) p = Math.Max(p, Math.Abs(c[i]));
        return p <= 0 ? double.NegativeInfinity : 20 * Math.Log10(p);
    }

    public static string Time(double s) => $"{(int)(s / 60)}:{s % 60:00.00}".Replace('.', ',');
}
