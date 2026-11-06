using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-16 (06.11.2026): Raumklang – Spuren im Kreis platzieren (Winkel wie ITU-R BS.775), Abmischung als 5.1
/// (L R C LFE Ls Rs, WAV mit Kanalmaske „5.1(side)“), Kanalplan und Kopfhörer-Vorschau (binaural: Laufzeit- und Pegelunterschied zwischen den Ohren).
/// Was kein echtes 5.1 ist (falsche Kanalzahl), wird nicht still als Surround geschrieben.</summary>
public static class SpatialAudio
{
    /// <summary>Lautsprecher in Kanalreihenfolge der WAV-Datei (Microsoft-Kanalmaske 0x60F).</summary>
    public static readonly (string Id, string Label, double Angle)[] Speakers51 =
        [("L", "Links vorn", -30), ("R", "Rechts vorn", 30), ("C", "Mitte", 0), ("LFE", "Tiefton (LFE)", double.NaN), ("Ls", "Links Raum", -110), ("Rs", "Rechts Raum", 110)];
    public const int Mask51Side = 0x60F;

    /// <summary>Winkel einer Spur: eigener Wert („az“) – sonst aus dem Stereo-Panorama (−1…1 → −30°…30°).</summary>
    public static double AzimuthOf(JsonObject track) =>
        track["az"] is JsonValue v && v.TryGetValue<double>(out var a) ? a : (track["pan"] is JsonValue p && p.TryGetValue<double>(out var pan) ? pan * 30 : 0);

    public static void SetAzimuth(JsonObject doc, string trackId, double degrees)
    {
        if (!double.IsFinite(degrees)) throw new ArgumentException("Winkel ungültig.");
        var t = (doc["tracks"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => (string?)x["id"] == trackId) ?? throw new InvalidOperationException("Diese Spur gibt es nicht mehr.");
        t["az"] = Math.Round(((degrees + 180) % 360 + 360) % 360 - 180, 1);
    }

    /// <summary>Verstärkung je Lautsprecher (ohne LFE): paarweises Panorama mit gleicher Leistung zwischen den beiden nächsten Lautsprechern.</summary>
    public static double[] Gains(double azimuth)
    {
        var ring = new[] { (Idx: 4, A: -110.0), (Idx: 0, A: -30.0), (Idx: 2, A: 0.0), (Idx: 1, A: 30.0), (Idx: 5, A: 110.0) };
        var g = new double[6]; var az = ((azimuth + 180) % 360 + 360) % 360 - 180;
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Length];
            var span = (b.A - a.A + 360) % 360; var off = (az - a.A + 360) % 360;
            if (off <= span + 1e-9)
            {
                var x = span < 1e-9 ? 0 : off / span; // 0 … 1 zwischen a und b
                g[a.Idx] = Math.Cos(x * Math.PI / 2); g[b.Idx] = Math.Sin(x * Math.PI / 2);
                return g;
            }
        }
        g[2] = 1; return g;
    }

    /// <summary>Mehrspur-Projekt als 5.1 abmischen (48 kHz). LFE = Tiefen unter 120 Hz aus der Summe (−6 dB).</summary>
    public static AudioBuf Mix51(JsonObject doc, int rate = 48000)
    {
        var all = AudioMulti.Tracks(doc); var objs = (doc["tracks"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (all.Count == 0) throw new InvalidOperationException("Keine Spuren zum Abmischen.");
        var parts = all.Select((t, i) => (T: t, Az: AzimuthOf(objs[i]), A: AudioEdit.Match(AudioFile.Read(t.File), rate, 2))).ToList();
        var len = parts.Max(p => (int)Math.Round(p.T.Start * rate) + p.A.Length);
        var ch = Enumerable.Range(0, 6).Select(_ => new double[len]).ToArray();
        foreach (var (t, az, a) in parts)
        {
            if (!AudioMulti.Audible(all, t)) continue;
            var g = Math.Pow(10, t.GainDb / 20); var sp = Gains(az); var off = (int)Math.Round(t.Start * rate);
            for (var i = 0; i < a.Length; i++)
            {
                var mono = (a.Data[0][i] + a.Data[1][i]) * 0.5 * g;
                for (var k = 0; k < 6; k++) if (sp[k] != 0) ch[k][off + i] += mono * sp[k];
            }
        }
        // LFE: Tiefpass 120 Hz (zweistufig) aus der Summe der fünf Hauptkanäle
        var c = 1 - Math.Exp(-2 * Math.PI * 120 / rate); double y1 = 0, y2 = 0;
        for (var i = 0; i < len; i++) { var sum = ch[0][i] + ch[1][i] + ch[2][i] + ch[4][i] + ch[5][i]; y1 += c * (sum - y1); y2 += c * (y1 - y2); ch[3][i] = y2 * 0.5; }
        var peak = 1e-9; foreach (var x in ch) foreach (var v in x) peak = Math.Max(peak, Math.Abs(v));
        var gain = peak > 0.891 ? 0.891 / peak : 1;
        return new AudioBuf(rate, ch.Select(x => x.Select(v => (float)(v * gain)).ToArray()).ToArray());
    }

    /// <summary>5.1 als WAV (24 Bit, WAVE_FORMAT_EXTENSIBLE mit Kanalmaske). Nur genau 6 Kanäle – sonst Fehler statt falsch beschriftetem Surround.</summary>
    public static void WriteWav51(AudioBuf a, string path)
    {
        if (a.Channels != 6) throw new ArgumentException($"Kein 5.1: {a.Channels} Kanal{(a.Channels == 1 ? "" : "e")}. Erst „Als 5.1 abmischen“ – Stereo wird nicht still zu Surround umbeschriftet.");
        const int bits = 24, step = 3; var len = a.Length * 6 * step;
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8); w.Write(4 + 8 + 40 + 8 + len); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(40); w.Write((ushort)0xFFFE); w.Write((short)6); w.Write(a.Rate); w.Write(a.Rate * 6 * step); w.Write((short)(6 * step)); w.Write((short)bits);
        w.Write((short)22); w.Write((short)bits); w.Write(Mask51Side);
        w.Write(new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray()); // KSDATAFORMAT_SUBTYPE_PCM
        w.Write("data"u8); w.Write(len);
        for (var i = 0; i < a.Length; i++)
            for (var c = 0; c < 6; c++) { var x = (int)Math.Round(Math.Clamp(a.Data[c][i], -1f, 1f) * 8388607); w.Write((byte)x); w.Write((byte)(x >> 8)); w.Write((byte)(x >> 16)); }
    }

    /// <summary>Kanalmaske aus einem WAV-Kopf (0 = keine/kein WAVE_FORMAT_EXTENSIBLE).</summary>
    public static int WavChannelMask(byte[] b)
    {
        for (var pos = 12; pos + 8 <= b.Length;)
        {
            var id = Encoding.ASCII.GetString(b, pos, 4); var n = BitConverter.ToInt32(b, pos + 4);
            if (id == "fmt ") return n >= 40 && BitConverter.ToUInt16(b, pos + 8) == 0xFFFE ? BitConverter.ToInt32(b, pos + 8 + 20) : 0;
            if (n < 0) break; pos += 8 + n + (n & 1);
        }
        return 0;
    }

    /// <summary>Kopfhörer-Vorschau: jeder Lautsprecher als virtuelle Quelle – Laufzeitunterschied (Woodworth, Kopfradius 8,75 cm)
    /// und Kopfschatten (Tiefpass am abgewandten Ohr). LFE mittig.</summary>
    public static AudioBuf Binaural(AudioBuf a51)
    {
        if (a51.Channels != 6) throw new ArgumentException("Kopfhörer-Vorschau braucht 5.1 (6 Kanäle).");
        var rate = a51.Rate; var n = a51.Length; var l = new double[n + rate / 100]; var r = new double[n + rate / 100];
        for (var c = 0; c < 6; c++)
        {
            var x = a51.Data[c];
            if (c == 3) { for (var i = 0; i < n; i++) { l[i] += x[i] * 0.5; r[i] += x[i] * 0.5; } continue; }
            var th = Speakers51[c].Angle * Math.PI / 180; var lat = Math.Asin(Math.Sin(th)); // seitlicher Anteil (vorn/hinten gespiegelt)
            var itd = 0.0875 / 343 * (Math.Abs(lat) + Math.Sin(Math.Abs(lat))); var delay = (int)Math.Round(itd * rate);
            var far = th > 0 ? l : r; var near = th > 0 ? r : l;
            var shadow = Math.Abs(Math.Sin(th)); var cut = 1 - Math.Exp(-2 * Math.PI * (18000 - 15000 * shadow) / rate); double y = 0;
            var rear = Math.Abs(Speakers51[c].Angle) > 90 ? 0.85 : 1.0; // hinten etwas dumpfer und leiser
            for (var i = 0; i < n; i++)
            {
                near[i] += x[i] * (1 + 0.25 * shadow) * 0.6 * rear;
                y += cut * (x[i] - y); far[i + delay] += y * (1 - 0.45 * shadow) * 0.6 * rear;
            }
        }
        var peak = 1e-9; for (var i = 0; i < l.Length; i++) peak = Math.Max(peak, Math.Max(Math.Abs(l[i]), Math.Abs(r[i])));
        var g = peak > 0.891 ? 0.891 / peak : 1;
        return new AudioBuf(rate, [l.Select(v => (float)(v * g)).ToArray(), r.Select(v => (float)(v * g)).ToArray()]);
    }

    /// <summary>Kanalplan als Text: Kanal, Lautsprecher, Winkel und Pegel je Kanal (RMS in dBFS).</summary>
    public static string ChannelPlan(AudioBuf a51)
    {
        var sb = new StringBuilder("Kanal | Lautsprecher | Winkel | Pegel (RMS)\n---|---|---|---\n");
        for (var c = 0; c < Math.Min(6, a51.Channels); c++)
        {
            double s = 0; foreach (var v in a51.Data[c]) s += v * (double)v; var rms = Math.Sqrt(s / Math.Max(1, a51.Length));
            var (id, label, ang) = Speakers51[c];
            sb.Append($"{c + 1} {id} | {label} | {(double.IsNaN(ang) ? "–" : $"{ang:+0;-0;0}°")} | {(rms < 1e-6 ? "stumm" : $"{20 * Math.Log10(rms):0.0} dBFS")}\n");
        }
        return sb.ToString();
    }
}
