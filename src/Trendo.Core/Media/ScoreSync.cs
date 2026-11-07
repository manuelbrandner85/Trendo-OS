using System.Text;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-14 (07.11.2026): Filmmusik auf den Schnitt legen – Tempo-Map (Tempowechsel je Takt), Leitmotiv beginnt genau am
/// Timecode eines Schnitts, Treffpunkte („Hits“) werden in Bildern geprüft. Mehr als ein Bild Abweichung ist ein Fehler. Ausgabe: MIDI mit Tempowechseln und Cue-Sheet.</summary>
public static class ScoreSync
{
    /// <summary>Ab Takt <paramref name="Bar"/> (1-basiert) gilt dieses Tempo und diese Taktart.</summary>
    public sealed record TempoChange(int Bar, double Bpm, int Numerator = 4);

    public sealed record Hit(string Name, double Seconds, int Bar, double Beat = 1);

    public static IReadOnlyList<TempoChange> Validate(IReadOnlyList<TempoChange> map)
    {
        if (map.Count == 0 || map[0].Bar != 1) throw new ArgumentException("Die Tempo-Map muss bei Takt 1 beginnen.");
        for (var i = 0; i < map.Count; i++)
        {
            if (!(map[i].Bpm is >= 20 and <= 400)) throw new ArgumentException($"Tempo {map[i].Bpm} BPM ungültig (20–400).");
            if (map[i].Numerator is < 1 or > 16) throw new ArgumentException("Taktart 1 bis 16.");
            if (i > 0 && map[i].Bar <= map[i - 1].Bar) throw new ArgumentException("Tempowechsel müssen nach Takten geordnet sein.");
        }
        return map;
    }

    /// <summary>Zeit in Sekunden an Takt/Schlag (beides 1-basiert, Schlag darf Bruchteile haben).</summary>
    public static double Seconds(IReadOnlyList<TempoChange> map, int bar, double beat = 1)
    {
        Validate(map); if (bar < 1 || beat < 1) throw new ArgumentException("Takt und Schlag beginnen bei 1.");
        double t = 0; var cur = 1;
        for (var i = 0; i < map.Count; i++)
        {
            var seg = map[i]; var next = i + 1 < map.Count ? map[i + 1].Bar : int.MaxValue; var beatSec = 60 / seg.Bpm;
            if (bar < next) return t + ((bar - cur) * seg.Numerator + (beat - 1)) * beatSec;
            t += (next - cur) * seg.Numerator * beatSec; cur = next;
        }
        return t;
    }

    /// <summary>Tempo so wählen, dass Takt <paramref name="bar"/> genau bei <paramref name="seconds"/> beginnt (ab Takt <paramref name="fromBar"/> der Map).</summary>
    public static double FitBpm(IReadOnlyList<TempoChange> before, int fromBar, int bar, double seconds, int numerator = 4)
    {
        var start = fromBar == 1 ? 0 : Seconds(before, fromBar);
        var beats = (bar - fromBar) * numerator; var span = seconds - start;
        if (beats <= 0 || span <= 0) throw new ArgumentException("Der Treffpunkt muss nach dem Tempowechsel liegen.");
        var bpm = beats * 60 / span;
        if (!(bpm is >= 20 and <= 400)) throw new ArgumentException($"Dafür bräuchte es {bpm:0.#} BPM – außerhalb von 20 bis 400. Anderen Takt wählen.");
        return Math.Round(bpm, 3);
    }

    public sealed record HitCheck(string Name, double Target, double Actual, double OffFrames, bool Ok);

    /// <summary>Treffpunkte prüfen: Abweichung in Bildern (bei <paramref name="fps"/>); mehr als <paramref name="toleranceFrames"/> = FAIL.</summary>
    public static List<HitCheck> CheckHits(IReadOnlyList<TempoChange> map, IEnumerable<Hit> hits, int fps = 25, double toleranceFrames = 1)
        => [.. hits.Select(h => { var a = Seconds(map, h.Bar, h.Beat); var off = (a - h.Seconds) * fps; return new HitCheck(h.Name, h.Seconds, Math.Round(a, 4), Math.Round(off, 2), Math.Abs(off) <= toleranceFrames + 1e-9); })];

    static void VarLen(List<byte> o, int v) { var s = new Stack<byte>(); s.Push((byte)(v & 0x7F)); v >>= 7; while (v > 0) { s.Push((byte)((v & 0x7F) | 0x80)); v >>= 7; } o.AddRange(s); }

    /// <summary>MIDI (Format 1) mit allen Tempowechseln; das Leitmotiv (Noten ab Tick 0) wird an Takt <paramref name="atBar"/> gesetzt.</summary>
    public static byte[] ToMidi(IReadOnlyList<TempoChange> map, IReadOnlyList<PianoRoll.Note> motif, int atBar, string name)
    {
        Validate(map); var ppq = PianoRoll.Ppq;
        int TickOfBar(int bar) { var t = 0; var cur = 1; for (var i = 0; i < map.Count; i++) { var next = i + 1 < map.Count ? map[i + 1].Bar : int.MaxValue; if (bar < next) return t + (bar - cur) * map[i].Numerator * ppq; t += (next - cur) * map[i].Numerator * ppq; cur = next; } return t; }
        var t0 = new List<byte>(); var last = 0;
        var nb = Encoding.UTF8.GetBytes(name); VarLen(t0, 0); t0.AddRange([0xFF, 0x03]); VarLen(t0, nb.Length); t0.AddRange(nb);
        foreach (var c in map)
        {
            var tick = TickOfBar(c.Bar); VarLen(t0, tick - last); last = tick;
            var us = (int)Math.Round(60_000_000 / c.Bpm);
            t0.AddRange([0xFF, 0x51, 0x03, (byte)(us >> 16), (byte)(us >> 8), (byte)us]);
            VarLen(t0, 0); t0.AddRange([0xFF, 0x58, 0x04, (byte)c.Numerator, 2, 24, 8]);
        }
        VarLen(t0, 0); t0.AddRange([0xFF, 0x2F, 0x00]);
        var off = TickOfBar(atBar);
        var notes = new PianoRoll.Song(map[0].Bpm, map[0].Numerator, [.. motif.Select(n => n with { Start = n.Start + off })]);
        var bytes = PianoRoll.ToMidi(notes); // Spur 2 übernehmen
        var trackStart = 14 + 8 + (bytes[18] << 24 | bytes[19] << 16 | bytes[20] << 8 | bytes[21]); // hinter MThd und erster MTrk
        static byte[] Chunk(List<byte> body) { var n = body.Count; return [.. "MTrk"u8.ToArray(), (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n, .. body]; }
        byte[] head = [.. "MThd"u8.ToArray(), 0, 0, 0, 6, 0, 1, 0, 2, (byte)(ppq >> 8), unchecked((byte)(ppq & 0xFF))];
        return [.. head, .. Chunk(t0), .. bytes[trackStart..]];
    }

    /// <summary>Tempowechsel aus einer MIDI-Datei lesen: (Tick, BPM).</summary>
    public static List<(int Tick, double Bpm)> TempoEvents(byte[] b)
    {
        var r = new List<(int, double)>(); if (b.Length < 22) return r;
        var len = b[18] << 24 | b[19] << 16 | b[20] << 8 | b[21]; var p = 22; var end = Math.Min(b.Length, p + len); var tick = 0;
        while (p < end)
        {
            int v = 0; byte c; do { c = b[p++]; v = v << 7 | (c & 0x7F); } while ((c & 0x80) != 0); tick += v;
            if (b[p] != 0xFF) break;
            var type = b[p + 1]; var l = b[p + 2];
            if (type == 0x51) r.Add((tick, Math.Round(60_000_000.0 / (b[p + 3] << 16 | b[p + 4] << 8 | b[p + 5]), 2)));
            p += 3 + l; if (type == 0x2F) break;
        }
        return r;
    }

    /// <summary>Timecode „HH:MM:SS:FF“ (oder Sekunden mit Komma/Punkt) in Sekunden.</summary>
    public static double ParseTimecode(string tc, int fps)
    {
        tc = (tc ?? "").Trim();
        var p = tc.Split(':');
        if (p.Length == 4 && p.All(x => int.TryParse(x, out _)))
        {
            var f = int.Parse(p[3]); if (f >= fps) throw new ArgumentException($"Bild {f} gibt es bei {fps} fps nicht.");
            return int.Parse(p[0]) * 3600 + int.Parse(p[1]) * 60 + int.Parse(p[2]) + f / (double)fps;
        }
        if (double.TryParse(tc.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && s >= 0) return s;
        throw new ArgumentException("Timecode als HH:MM:SS:BB angeben, z. B. 00:00:12:12.");
    }

    /// <summary>Cue-Sheet-Zeile des Leitmotivs mit Timecode (gleiches Format wie die Cue-Liste der Film-Auslieferung).</summary>
    public static string CueLine(string id, string title, double start, double end, int fps) =>
        $"{id};{title.Replace(';', ',')};{FilmDelivery.Timecode(start, fps)};{FilmDelivery.Timecode(end, fps)};{(end - start).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";
}
