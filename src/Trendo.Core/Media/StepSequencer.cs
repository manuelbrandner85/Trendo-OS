using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-13 (06.11.2026): Step-Sequencer wie ein Drumcomputer – Kick, Snare, Clap, Hi-Hat, offene Hi-Hat und Bass
/// im 16tel-Raster, Swing, Lautstärke-Automation je Spur und Arpeggiator. Alle Klänge werden selbst erzeugt (keine Samples, keine Lizenzfragen).
/// Ungültige Muster, Automation oder eine zu lange Schleife werden mit Grund abgelehnt.</summary>
public static class StepSequencer
{
    public static readonly (string Id, string Label)[] Voices = [("kick", "Kick"), ("snare", "Snare"), ("clap", "Clap"), ("hat", "Hi-Hat"), ("ohat", "Offene Hi-Hat"), ("bass", "Bass")];
    public static readonly string[] ArpModes = ["up", "down", "updown"];
    public const double MaxSeconds = 600;

    public sealed record Track(string Id, string Steps, double Volume = 0.8, IReadOnlyList<(int Step, double Value)>? Automation = null);
    public sealed record Arp(bool On, string Chord = "Am", string Mode = "up", int Octaves = 2, double Volume = 0.5);
    public sealed record Pattern(double Bpm, int Steps, int Bars, double Swing, IReadOnlyList<Track> Tracks, Arp Arp, string Chord = "Am");

    /// <summary>Vorlage „EDM“: Four-on-the-floor, Clap auf 2 und 4, Offbeat-Hats, Bass auf den Offbeats, Arpeggio in Am.</summary>
    public static Pattern Edm() => new(128, 16, 4, 0,
    [
        new("kick", "x...x...x...x..."), new("snare", "................", 0.6), new("clap", "....x.......x...", 0.7),
        new("hat", "..x...x...x...x.", 0.45), new("ohat", "..............x.", 0.35), new("bass", "..x...x...x...x.", 0.7, [(0, 0.4), (63, 0.9)]),
    ], new Arp(true, "Am", "up", 2, 0.4));

    public static Pattern Validate(Pattern p)
    {
        if (!(p.Bpm is >= 40 and <= 300)) throw new ArgumentException($"Tempo {p.Bpm} BPM – erlaubt sind 40 bis 300.");
        if (p.Steps is not (8 or 16 or 32)) throw new ArgumentException("Schritte je Takt: 8, 16 oder 32.");
        if (p.Bars is < 1 or > 512) throw new ArgumentException("Takte: 1 bis 512.");
        if (!(p.Swing is >= 0 and <= 0.75)) throw new ArgumentException("Swing: 0 bis 75 %.");
        var total = p.Steps * p.Bars; var sec = Seconds(p);
        if (sec > MaxSeconds) throw new ArgumentException($"Das Muster wäre {sec / 60:0} Minuten lang – das sieht nach einer Endlosschleife aus (höchstens {MaxSeconds / 60:0} Minuten).");
        foreach (var t in p.Tracks)
        {
            if (!Voices.Any(v => v.Id == t.Id)) throw new ArgumentException("Unbekannte Spur: " + t.Id);
            if (t.Steps.Length != p.Steps || t.Steps.Any(c => c is not ('x' or 'X' or '.'))) throw new ArgumentException($"Spur „{t.Id}“: genau {p.Steps} Zeichen aus x, X (betont) und Punkt.");
            if (!(t.Volume is >= 0 and <= 1.5)) throw new ArgumentException($"Spur „{t.Id}“: Lautstärke 0 bis 150 %.");
            if (t.Automation is { Count: > 0 } au)
            {
                for (var i = 0; i < au.Count; i++)
                {
                    var (s, v) = au[i];
                    if (s < 0 || s >= total) throw new ArgumentException($"Automation „{t.Id}“: Schritt {s} liegt außerhalb des Musters (0–{total - 1}).");
                    if (!double.IsFinite(v) || v < 0 || v > 1.5) throw new ArgumentException($"Automation „{t.Id}“: Wert {v} ungültig (0 bis 1,5).");
                    if (i > 0 && s <= au[i - 1].Step) throw new ArgumentException($"Automation „{t.Id}“: Punkte müssen der Reihe nach kommen (Schritt {s} nach {au[i - 1].Step}).");
                }
            }
        }
        if (p.Arp.On)
        {
            if (MusicPro.ChordNotes(p.Arp.Chord).Length == 0) throw new ArgumentException("Arpeggiator: Akkord unbekannt – z. B. Am, C, F#m.");
            if (!ArpModes.Contains(p.Arp.Mode)) throw new ArgumentException("Arpeggiator: auf, ab oder auf und ab.");
            if (p.Arp.Octaves is < 1 or > 3) throw new ArgumentException("Arpeggiator: 1 bis 3 Oktaven.");
        }
        if (MusicPro.ChordNotes(p.Chord).Length == 0) throw new ArgumentException("Grundakkord unbekannt.");
        return p;
    }

    public static double StepSeconds(Pattern p) => 60.0 / p.Bpm * 4 / p.Steps;
    public static double Seconds(Pattern p) => p.Steps * p.Bars * StepSeconds(p);

    /// <summary>Automation als Faktor an einem Schritt (linear zwischen den Punkten, davor/danach konstant).</summary>
    public static double AutoAt(Track t, int step)
    {
        if (t.Automation is not { Count: > 0 } a) return 1;
        if (step <= a[0].Step) return a[0].Value;
        for (var i = 1; i < a.Count; i++)
            if (step <= a[i].Step) { var (s0, v0) = a[i - 1]; var (s1, v1) = a[i]; return v0 + (v1 - v0) * (step - s0) / (double)(s1 - s0); }
        return a[^1].Value;
    }

    /// <summary>Tonfolge des Arpeggiators über alle Schritte (MIDI-Noten).</summary>
    public static List<int> ArpNotes(Arp a, int count)
    {
        var chord = MusicPro.ChordNotes(a.Chord).Select(n => n + 12).ToList(); // eine Oktave über C3
        var up = Enumerable.Range(0, a.Octaves).SelectMany(o => chord.Select(n => n + 12 * o)).ToList();
        var seq = a.Mode switch { "down" => Enumerable.Reverse(up).ToList(), "updown" => up.Concat(up.Skip(1).Reverse().Skip(1)).ToList(), _ => up };
        if (seq.Count == 0) seq = up;
        return Enumerable.Range(0, count).Select(i => seq[i % seq.Count]).ToList();
    }

    static double Hz(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);

    /// <summary>Muster zu Stereo-Ton rendern (48 kHz). Spitze wird auf −1 dBFS begrenzt – ohne Übersteuerung.</summary>
    public static AudioBuf Render(Pattern p, int rate = 48000)
    {
        Validate(p);
        var stepS = StepSeconds(p); var total = p.Steps * p.Bars; var n = (int)Math.Ceiling((Seconds(p) + 1.0) * rate);
        var l = new double[n]; var r = new double[n]; var rng = new Random(7);
        void Add(int at, double[] x, double gain, double pan)
        {
            var gl = gain * Math.Sqrt(0.5 * (1 - pan)); var gr = gain * Math.Sqrt(0.5 * (1 + pan));
            for (var i = 0; i < x.Length && at + i < n; i++) { if (at + i < 0) continue; l[at + i] += x[i] * gl; r[at + i] += x[i] * gr; }
        }
        double[] Kick() { var len = (int)(0.45 * rate); var x = new double[len]; double ph = 0; for (var i = 0; i < len; i++) { var t = i / (double)rate; var f = 45 + 110 * Math.Exp(-t * 28); ph += 2 * Math.PI * f / rate; x[i] = Math.Sin(ph) * Math.Exp(-t * 7); } return x; }
        double[] Noise(double sec, double decay, double hp)
        {
            var len = (int)(sec * rate); var x = new double[len]; double prev = 0, y = 0; var a = Math.Exp(-2 * Math.PI * hp / rate);
            for (var i = 0; i < len; i++) { var w = rng.NextDouble() * 2 - 1; y = a * (y + w - prev); prev = w; x[i] = y * Math.Exp(-i / (double)rate * decay); }
            return x;
        }
        double[] Snare() { var x = Noise(0.25, 18, 1200); for (var i = 0; i < x.Length; i++) { var t = i / (double)rate; x[i] = 0.7 * x[i] + 0.5 * Math.Sin(2 * Math.PI * 185 * t) * Math.Exp(-t * 25); } return x; }
        double[] Clap() { var x = Noise(0.3, 14, 900); for (var i = 0; i < x.Length; i++) { var t = i / (double)rate; var burst = t < 0.03 ? (Math.Sin(t / 0.01 * Math.PI) > 0 ? 1 : 0.3) : 1; x[i] *= burst; } return x; }
        double[] Tone(int midi, double sec, double decay, bool saw, double cutoff)
        {
            var len = (int)(sec * rate); var x = new double[len]; var f = Hz(midi); double ph = 0, y = 0; var a = 1 - Math.Exp(-2 * Math.PI * cutoff / rate);
            for (var i = 0; i < len; i++)
            {
                ph += f / rate; ph -= Math.Floor(ph); var v = saw ? 2 * ph - 1 : (ph < 0.5 ? 1 : -1);
                y += a * (v - y); var t = i / (double)rate; var env = Math.Min(1, t / 0.004) * Math.Exp(-t * decay);
                x[i] = y * env;
            }
            return x;
        }
        var kick = Kick(); var snare = Snare(); var clap = Clap(); var hat = Noise(0.05, 60, 7000); var ohat = Noise(0.3, 9, 6000);
        var bassNote = MusicPro.ChordNotes(p.Chord)[0] - 12; // eine Oktave unter C3
        var bass = Tone(bassNote, stepS * 1.8, 6, true, 600);
        int At(int step) => (int)Math.Round((step * stepS + (step % 2 == 1 ? p.Swing * stepS * 0.5 : 0)) * rate);
        foreach (var t in p.Tracks)
        {
            var (snd, pan) = t.Id switch { "kick" => (kick, 0.0), "snare" => (snare, 0.0), "clap" => (clap, 0.1), "hat" => (hat, 0.3), "ohat" => (ohat, -0.3), _ => (bass, 0.0) };
            for (var s = 0; s < total; s++)
            {
                var c = t.Steps[s % p.Steps]; if (c == '.') continue;
                Add(At(s), snd, t.Volume * AutoAt(t, s) * (c == 'X' ? 1.25 : 1), pan);
            }
        }
        if (p.Arp.On)
        {
            var notes = ArpNotes(p.Arp, total);
            for (var s = 0; s < total; s++) Add(At(s), Tone(notes[s], stepS * 0.9, 10, false, 2500), p.Arp.Volume * 0.5, s % 2 == 0 ? -0.25 : 0.25);
        }
        var peak = 1e-9; for (var i = 0; i < n; i++) peak = Math.Max(peak, Math.Max(Math.Abs(l[i]), Math.Abs(r[i])));
        var g = Math.Min(1, 0.891 / peak); // −1 dBFS
        return new AudioBuf(rate, [l.Select(v => (float)(v * g)).ToArray(), r.Select(v => (float)(v * g)).ToArray()]);
    }

    // ───────────── Speichern ─────────────

    public static JsonObject ToJson(Pattern p) => new()
    {
        ["bpm"] = p.Bpm, ["steps"] = p.Steps, ["bars"] = p.Bars, ["swing"] = p.Swing, ["chord"] = p.Chord,
        ["tracks"] = new JsonArray([.. p.Tracks.Select(t => (JsonNode)new JsonObject
        {
            ["id"] = t.Id, ["steps"] = t.Steps, ["vol"] = t.Volume,
            ["auto"] = t.Automation is { Count: > 0 } a ? new JsonArray([.. a.Select(x => (JsonNode)new JsonArray(x.Step, x.Value))]) : null,
        })]),
        ["arp"] = new JsonObject { ["on"] = p.Arp.On, ["chord"] = p.Arp.Chord, ["mode"] = p.Arp.Mode, ["oct"] = p.Arp.Octaves, ["vol"] = p.Arp.Volume },
    };

    /// <summary>Muster aus JSON lesen und prüfen; fehlerhafte Daten → ArgumentException mit Grund.</summary>
    public static Pattern FromJson(JsonObject o)
    {
        double D(JsonNode? n, double f) => n is JsonValue v && double.TryParse(v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : f;
        try
        {
            var tracks = (o["tracks"] as JsonArray ?? []).OfType<JsonObject>().Select(t => new Track((string?)t["id"] ?? "", (string?)t["steps"] ?? "", D(t["vol"], 0.8),
                (t["auto"] as JsonArray)?.OfType<JsonArray>().Select(x => ((int)D(x[0], -1), D(x[1], double.NaN))).ToList())).ToList();
            var a = o["arp"] as JsonObject ?? [];
            return Validate(new Pattern(D(o["bpm"], 128), (int)D(o["steps"], 16), (int)D(o["bars"], 4), D(o["swing"], 0), tracks,
                new Arp(a["on"] is JsonValue on && on.TryGetValue<bool>(out var b) && b, (string?)a["chord"] ?? "Am", (string?)a["mode"] ?? "up", (int)D(a["oct"], 2), D(a["vol"], 0.5)), (string?)o["chord"] ?? "Am"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException) { throw new ArgumentException("Muster beschädigt: " + ex.Message); }
    }
}
