using System.Text;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-04 (06.11.2026): Piano-Roll – Noten mit Tonhöhe, Beginn, Länge und Anschlag in Ticks (480 je Viertel),
/// Quantisieren, Transponieren, Standard-MIDI-Datei schreiben und lesen. Beim Lesen werden kaputte Dateien gemeldet (Warnungen) statt
/// das offene Stück zu zerstören.</summary>
public static class PianoRoll
{
    public const int Ppq = 480;

    public sealed record Note(int Pitch, int Start, int Length, int Velocity = 96);

    public sealed record Song(double Bpm, int Numerator, IReadOnlyList<Note> Notes, string Name = "");

    public sealed record ImportResult(Song? Song, IReadOnlyList<string> Warnings)
    {
        public bool Ok => Song is not null;
    }

    static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "H"];
    public static string NoteName(int pitch) => $"{Names[((pitch % 12) + 12) % 12]}{pitch / 12 - 1}";

    public static Song Validate(Song s)
    {
        if (!(s.Bpm is >= 20 and <= 400)) throw new ArgumentException("Tempo 20 bis 400 BPM.");
        if (s.Numerator is < 1 or > 16) throw new ArgumentException("Taktart: 1 bis 16 Schläge.");
        foreach (var n in s.Notes)
            if (n.Pitch is < 0 or > 127 || n.Start < 0 || n.Length < 1 || n.Velocity is < 1 or > 127) throw new ArgumentException($"Ungültige Note: {n}.");
        return s;
    }

    /// <summary>Note an dieser Stelle umschalten: liegt dort eine Note dieser Tonhöhe, wird sie entfernt, sonst wird eine neue gesetzt.</summary>
    public static List<Note> Toggle(IReadOnlyList<Note> notes, int pitch, int tick, int length)
    {
        var hit = notes.FirstOrDefault(n => n.Pitch == pitch && tick >= n.Start && tick < n.Start + n.Length);
        return hit is not null ? [.. notes.Where(n => n != hit)] : [.. notes, new Note(pitch, tick, length)];
    }

    /// <summary>Quantisieren: Beginn (und Länge) auf das Raster ziehen. Stärke 1 = ganz, 0,5 = halb (natürlicher). Raster in Ticks (120 = 1/16).</summary>
    public static List<Note> Quantize(IReadOnlyList<Note> notes, int grid, double strength = 1, bool lengths = false)
    {
        if (grid < 1) throw new ArgumentException("Raster muss größer als 0 sein.");
        strength = Math.Clamp(strength, 0, 1);
        return [.. notes.Select(n =>
        {
            var target = (int)Math.Round(n.Start / (double)grid) * grid;
            var start = (int)Math.Round(n.Start + (target - n.Start) * strength);
            var len = lengths ? Math.Max(grid, (int)Math.Round(n.Length / (double)grid) * grid) : n.Length;
            return n with { Start = Math.Max(0, start), Length = len };
        }).OrderBy(n => n.Start).ThenBy(n => n.Pitch)];
    }

    public static List<Note> Transpose(IReadOnlyList<Note> notes, int semitones) =>
        [.. notes.Select(n => n with { Pitch = Math.Clamp(n.Pitch + semitones, 0, 127) })];

    // ───────────── MIDI schreiben ─────────────

    static void VarLen(List<byte> o, int v)
    {
        var buf = new Stack<byte>(); buf.Push((byte)(v & 0x7F)); v >>= 7;
        while (v > 0) { buf.Push((byte)((v & 0x7F) | 0x80)); v >>= 7; }
        o.AddRange(buf);
    }

    static byte[] Chunk(string id, List<byte> body)
    {
        var o = new List<byte>(Encoding.ASCII.GetBytes(id)); var n = body.Count;
        o.AddRange([(byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n]); o.AddRange(body); return [.. o];
    }

    /// <summary>Standard-MIDI-Datei Format 1: Spur 1 Tempo/Taktart/Name, Spur 2 die Noten (Kanal 1).</summary>
    public static byte[] ToMidi(Song s)
    {
        Validate(s);
        var t0 = new List<byte>();
        if (s.Name.Length > 0) { var nb = Encoding.UTF8.GetBytes(s.Name); VarLen(t0, 0); t0.AddRange([0xFF, 0x03]); VarLen(t0, nb.Length); t0.AddRange(nb); }
        var us = (int)Math.Round(60_000_000 / s.Bpm);
        VarLen(t0, 0); t0.AddRange([0xFF, 0x51, 0x03, (byte)(us >> 16), (byte)(us >> 8), (byte)us]);
        VarLen(t0, 0); t0.AddRange([0xFF, 0x58, 0x04, (byte)s.Numerator, 2, 24, 8]);
        VarLen(t0, 0); t0.AddRange([0xFF, 0x2F, 0x00]);
        var ev = s.Notes.SelectMany(n => new[] { (Tick: n.Start, On: true, n.Pitch, n.Velocity), (Tick: n.Start + n.Length, On: false, n.Pitch, Velocity: 0) })
            .OrderBy(e => e.Tick).ThenBy(e => e.On ? 1 : 0).ThenBy(e => e.Pitch).ToList(); // Note-aus vor Note-an am gleichen Tick
        var t1 = new List<byte>(); var last = 0;
        foreach (var e in ev) { VarLen(t1, e.Tick - last); last = e.Tick; t1.AddRange(e.On ? [0x90, (byte)e.Pitch, (byte)e.Velocity] : [0x80, (byte)e.Pitch, 0x40]); }
        VarLen(t1, 0); t1.AddRange([0xFF, 0x2F, 0x00]);
        var head = new List<byte> { 0, 1, 0, 2, (byte)(Ppq >> 8), unchecked((byte)(Ppq & 0xFF)) };
        return [.. Chunk("MThd", head), .. Chunk("MTrk", t0), .. Chunk("MTrk", t1)];
    }

    // ───────────── MIDI lesen ─────────────

    /// <summary>Standard-MIDI-Datei lesen (Format 0 und 1, alle Kanäle). Fehler werden als Warnungen gesammelt;
    /// ohne lesbaren Kopf gibt es kein Ergebnis (das offene Stück bleibt unangetastet).</summary>
    public static ImportResult FromMidi(byte[] b)
    {
        var warn = new List<string>();
        if (b.Length < 14 || Encoding.ASCII.GetString(b, 0, 4) != "MThd") return new(null, ["Keine MIDI-Datei (Kopf „MThd“ fehlt)."]);
        int U16(int p) => b[p] << 8 | b[p + 1]; int U32(int p) => b[p] << 24 | b[p + 1] << 16 | b[p + 2] << 8 | b[p + 3];
        var hlen = U32(4); var format = U16(8); var tracks = U16(10); var div = U16(12);
        if (format > 2) return new(null, [$"MIDI-Format {format} unbekannt."]);
        if ((div & 0x8000) != 0) return new(null, ["SMPTE-Zeitbasis wird nicht unterstützt."]);
        if (div == 0) return new(null, ["Zeitbasis 0 – Datei beschädigt."]);
        double bpm = 120; var num = 4; var name = ""; var notes = new List<Note>();
        var pos = 8 + hlen;
        for (var t = 0; t < tracks; t++)
        {
            if (pos + 8 > b.Length) { warn.Add($"Spur {t + 1} fehlt – Datei vorzeitig zu Ende."); break; }
            if (Encoding.ASCII.GetString(b, pos, 4) != "MTrk") { warn.Add($"Spur {t + 1}: Kennung „MTrk“ fehlt – Rest übersprungen."); break; }
            var len = U32(pos + 4); var p = pos + 8; var end = p + len;
            if (end > b.Length || len < 0) { warn.Add($"Spur {t + 1} ist abgeschnitten."); end = b.Length; }
            var tick = 0; byte status = 0; var open = new Dictionary<(int Ch, int Pitch), (int Tick, int Vel)>();
            try
            {
                while (p < end)
                {
                    int v = 0, guard = 0; byte c; do { c = b[p++]; v = v << 7 | (c & 0x7F); } while ((c & 0x80) != 0 && ++guard < 4); tick += v;
                    var s = b[p];
                    if (s >= 0x80) { status = s; p++; } else if (status < 0x80) { warn.Add($"Spur {t + 1}: Ereignis ohne Status – Spur abgebrochen."); break; }
                    if (status == 0xFF)
                    {
                        var type = b[p++]; int l = 0; do { c = b[p++]; l = l << 7 | (c & 0x7F); } while ((c & 0x80) != 0);
                        if (type == 0x51 && l == 3) bpm = Math.Round(60_000_000.0 / (b[p] << 16 | b[p + 1] << 8 | b[p + 2]), 3);
                        else if (type == 0x58 && l >= 1) num = b[p];
                        else if (type == 0x03 && name.Length == 0) name = Encoding.UTF8.GetString(b, p, l);
                        p += l; if (type == 0x2F) break; continue;
                    }
                    if (status is 0xF0 or 0xF7) { int l = 0; do { c = b[p++]; l = l << 7 | (c & 0x7F); } while ((c & 0x80) != 0); p += l; continue; }
                    var kind = status & 0xF0; var ch = status & 0x0F;
                    if (kind is 0xC0 or 0xD0) { p += 1; continue; }
                    var d1 = b[p++]; var d2 = b[p++];
                    if (d1 > 127 || d2 > 127) { warn.Add($"Spur {t + 1}: ungültige Datenbytes bei Tick {tick}."); continue; }
                    if (kind == 0x90 && d2 > 0) { if (open.Remove((ch, d1), out var prev)) notes.Add(new Note(d1, prev.Tick, Math.Max(1, tick - prev.Tick), prev.Vel)); open[(ch, d1)] = (tick, d2); }
                    else if (kind == 0x80 || (kind == 0x90 && d2 == 0))
                    {
                        if (open.Remove((ch, d1), out var on)) notes.Add(new Note(d1, on.Tick, Math.Max(1, tick - on.Tick), on.Vel));
                        else warn.Add($"Note aus ohne Note an ({NoteName(d1)}, Tick {tick}) – übersprungen.");
                    }
                }
            }
            catch (IndexOutOfRangeException) { warn.Add($"Spur {t + 1} endet mitten in einem Ereignis – gelesen bis Tick {tick}."); }
            foreach (var (k, o) in open) { warn.Add($"{NoteName(k.Pitch)} bei Tick {o.Tick} ohne Ende – bis Spurende verlängert."); notes.Add(new Note(k.Pitch, o.Tick, Math.Max(1, tick - o.Tick), o.Vel)); }
            pos = Math.Min(b.Length, pos + 8 + Math.Max(0, len));
        }
        // auf 480 Ticks je Viertel umrechnen
        if (div != Ppq) notes = [.. notes.Select(n => n with { Start = (int)Math.Round(n.Start * (double)Ppq / div), Length = Math.Max(1, (int)Math.Round(n.Length * (double)Ppq / div)) })];
        var song = new Song(Math.Clamp(bpm, 20, 400), Math.Clamp(num, 1, 16), [.. notes.OrderBy(n => n.Start).ThenBy(n => n.Pitch)], name);
        return new(song, warn);
    }
}
