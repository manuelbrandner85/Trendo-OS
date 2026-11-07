using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-09 (07.11.2026): Mischpult mit Bussen wie in einer DAW – Spuren gehen auf die Summe oder einen Bus,
/// Sends schicken einen Anteil zusätzlich auf einen Bus (z. B. Hall), jeder Bus hat 3-Band-EQ, Kompressor (auf Wunsch mit Sidechain einer Spur,
/// „Ducking“) und Pegel. Busse können in andere Busse gehen; ein Routing-Kreis wird erkannt und abgewiesen.</summary>
public static class MixRouting
{
    public const string Master = "master";

    public sealed record Bus(string Id, string Name, string Out, double GainDb, double LowDb, double MidDb, double HighDb, double CompThresholdDb, double CompRatio, string Sidechain, double ReverbMix);
    public sealed record Send(string TrackId, string BusId, double Level);

    static string S(JsonNode? n, string f = "") => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : f;
    static double D(JsonNode? n, double f = 0) => n is JsonValue v && double.TryParse(v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : f;

    public static bool HasRouting(JsonObject doc) => doc["buses"] is JsonArray { Count: > 0 };

    public static List<Bus> Buses(JsonObject doc) => [.. (doc["buses"] as JsonArray ?? []).OfType<JsonObject>().Select(b => new Bus(S(b["id"]), S(b["name"]), S(b["out"], Master), D(b["gain"]),
        D(b["low"]), D(b["mid"]), D(b["high"]), D(b["thr"], 0), D(b["ratio"], 1), S(b["sc"]), D(b["rev"]))) ];

    public static List<Send> Sends(JsonObject doc) => [.. (doc["sends"] as JsonArray ?? []).OfType<JsonObject>().Select(s => new Send(S(s["track"]), S(s["bus"]), D(s["level"])))];

    /// <summary>Ziel einer Spur: „master“ oder Bus-ID.</summary>
    public static string TrackOut(JsonObject doc, string trackId) =>
        S((doc["tracks"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(t => S(t["id"]) == trackId)?["out"], Master);

    public static Bus AddBus(JsonObject doc, string name)
    {
        var arr = doc["buses"] as JsonArray ?? (JsonArray)(doc["buses"] = new JsonArray());
        if (arr.Count >= 8) throw new InvalidOperationException("Höchstens 8 Busse.");
        var id = "b" + Guid.NewGuid().ToString("N")[..6];
        arr.Add(new JsonObject { ["id"] = id, ["name"] = string.IsNullOrWhiteSpace(name) ? $"Bus {arr.Count + 1}" : name.Trim(), ["out"] = Master, ["gain"] = 0.0, ["low"] = 0.0, ["mid"] = 0.0, ["high"] = 0.0, ["thr"] = 0.0, ["ratio"] = 1.0, ["sc"] = "", ["rev"] = 0.0 });
        return Buses(doc).Last();
    }

    static JsonObject BusObj(JsonObject doc, string id) => (doc["buses"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(b => S(b["id"]) == id) ?? throw new InvalidOperationException("Diesen Bus gibt es nicht.");

    /// <summary>EQ (je Band −15 … +15 dB), Kompressor (Schwelle −60 … 0 dB, Verhältnis 1 … 20), Pegel, Hall-Anteil (0 … 1), Sidechain-Spur.</summary>
    public static void SetBus(JsonObject doc, string id, double? gainDb = null, double? low = null, double? mid = null, double? high = null, double? threshold = null, double? ratio = null, string? sidechain = null, double? reverb = null)
    {
        var b = BusObj(doc, id);
        if (gainDb is { } g) b["gain"] = Math.Clamp(g, -60, 12);
        if (low is { } l) b["low"] = Math.Clamp(l, -15, 15);
        if (mid is { } m) b["mid"] = Math.Clamp(m, -15, 15);
        if (high is { } h) b["high"] = Math.Clamp(h, -15, 15);
        if (threshold is { } t) b["thr"] = Math.Clamp(t, -60, 0);
        if (ratio is { } r) b["ratio"] = Math.Clamp(r, 1, 20);
        if (reverb is { } rv) b["rev"] = Math.Clamp(rv, 0, 1);
        if (sidechain is not null)
        {
            if (sidechain.Length > 0 && AudioMulti.Tracks(doc).All(x => x.Id != sidechain)) throw new ArgumentException("Sidechain: diese Spur gibt es nicht.");
            b["sc"] = sidechain;
        }
    }

    /// <summary>Ausgang setzen (Spur oder Bus). Ein Kreis (Bus A → B → A) wird abgewiesen; das Projekt bleibt unverändert.</summary>
    public static void Route(JsonObject doc, string sourceId, string target)
    {
        if (target != Master && Buses(doc).All(b => b.Id != target)) throw new ArgumentException("Ziel-Bus gibt es nicht.");
        var isBus = Buses(doc).Any(b => b.Id == sourceId);
        if (isBus)
        {
            if (target == sourceId || Reaches(doc, target, sourceId)) throw new InvalidOperationException($"Routing-Kreis: „{Name(doc, sourceId)}“ würde über „{Name(doc, target)}“ wieder bei sich selbst ankommen – abgewiesen.");
            BusObj(doc, sourceId)["out"] = target;
        }
        else
        {
            var t = (doc["tracks"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => S(x["id"]) == sourceId) ?? throw new InvalidOperationException("Diese Spur gibt es nicht.");
            t["out"] = target;
        }
    }

    /// <summary>Send einer Spur auf einen Bus (0 = aus).</summary>
    public static void SetSend(JsonObject doc, string trackId, string busId, double level)
    {
        if (Buses(doc).All(b => b.Id != busId)) throw new ArgumentException("Bus gibt es nicht.");
        if (TrackOut(doc, trackId) == busId && level > 0) throw new InvalidOperationException("Die Spur geht schon direkt auf diesen Bus – ein Send zusätzlich würde sie doppelt schicken.");
        var arr = doc["sends"] as JsonArray ?? (JsonArray)(doc["sends"] = new JsonArray());
        var ex = arr.OfType<JsonObject>().FirstOrDefault(s => S(s["track"]) == trackId && S(s["bus"]) == busId);
        if (level <= 0) { if (ex is not null) arr.Remove(ex); return; }
        if (ex is null) arr.Add(new JsonObject { ["track"] = trackId, ["bus"] = busId, ["level"] = Math.Clamp(level, 0, 1) });
        else ex["level"] = Math.Clamp(level, 0, 1);
    }

    static string Name(JsonObject doc, string id) => id == Master ? "Summe" : Buses(doc).FirstOrDefault(b => b.Id == id)?.Name ?? id;

    static bool Reaches(JsonObject doc, string from, string target)
    {
        var buses = Buses(doc).ToDictionary(b => b.Id); var seen = new HashSet<string>(); var cur = from;
        while (cur != Master && buses.TryGetValue(cur, out var b) && seen.Add(cur)) { if (b.Out == target) return true; cur = b.Out; }
        return false;
    }

    /// <summary>Reihenfolge der Busse: zuerst die, in die nichts mehr hineinfließt, was noch nicht berechnet ist (Ziel zuletzt).</summary>
    public static List<Bus> Order(JsonObject doc)
    {
        var buses = Buses(doc); var done = new List<Bus>(); var left = buses.ToList();
        while (left.Count > 0)
        {
            var ready = left.Where(b => !left.Any(o => o.Out == b.Id)).ToList();
            if (ready.Count == 0) throw new InvalidOperationException("Routing-Kreis im Projekt – bitte Bus-Ausgänge prüfen.");
            done.AddRange(ready); left.RemoveAll(ready.Contains);
        }
        return done;
    }

    // ───────────── Bearbeitung ─────────────

    /// <summary>Biquad-Filter (RBJ): Tiefen-Kuhschwanz 200 Hz, Glocke 1 kHz, Höhen-Kuhschwanz 5 kHz.</summary>
    static void Biquad(float[] x, int rate, string kind, double f0, double gainDb, double q = 0.7)
    {
        if (Math.Abs(gainDb) < 0.01) return;
        var A = Math.Pow(10, gainDb / 40); var w = 2 * Math.PI * f0 / rate; var cs = Math.Cos(w); var sn = Math.Sin(w); var alpha = sn / (2 * q);
        double b0, b1, b2, a0, a1, a2;
        switch (kind)
        {
            case "low": { var s = 2 * Math.Sqrt(A) * alpha; b0 = A * ((A + 1) - (A - 1) * cs + s); b1 = 2 * A * ((A - 1) - (A + 1) * cs); b2 = A * ((A + 1) - (A - 1) * cs - s); a0 = (A + 1) + (A - 1) * cs + s; a1 = -2 * ((A - 1) + (A + 1) * cs); a2 = (A + 1) + (A - 1) * cs - s; break; }
            case "high": { var s = 2 * Math.Sqrt(A) * alpha; b0 = A * ((A + 1) + (A - 1) * cs + s); b1 = -2 * A * ((A - 1) + (A + 1) * cs); b2 = A * ((A + 1) + (A - 1) * cs - s); a0 = (A + 1) - (A - 1) * cs + s; a1 = 2 * ((A - 1) - (A + 1) * cs); a2 = (A + 1) - (A - 1) * cs - s; break; }
            default: b0 = 1 + alpha * A; b1 = -2 * cs; b2 = 1 - alpha * A; a0 = 1 + alpha / A; a1 = -2 * cs; a2 = 1 - alpha / A; break;
        }
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (var i = 0; i < x.Length; i++) { double x0 = x[i]; var y = (b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0; x2 = x1; x1 = x0; y2 = y1; y1 = y; x[i] = (float)y; }
    }

    /// <summary>Kompressor; mit <paramref name="key"/> regelt ein anderes Signal (Sidechain), z. B. Stimme drückt Musik.</summary>
    static void Compress(float[][] ch, int rate, double thresholdDb, double ratio, float[]? key)
    {
        if (ratio <= 1.001) return;
        double at = Math.Exp(-1.0 / (rate * 0.01)), rt = Math.Exp(-1.0 / (rate * 0.2)), env = 0; var n = ch[0].Length;
        for (var i = 0; i < n; i++)
        {
            double p = key is not null ? (i < key.Length ? Math.Abs(key[i]) : 0) : ch.Max(c => Math.Abs(c[i]));
            env = p > env ? at * env + (1 - at) * p : rt * env + (1 - rt) * p;
            var over = 20 * Math.Log10(env + 1e-9) - thresholdDb;
            if (over <= 0) continue;
            var g = (float)Math.Pow(10, -over * (1 - 1 / ratio) / 20);
            foreach (var c in ch) c[i] *= g;
        }
    }

    /// <summary>Einfacher Hall (vier Kammfilter, Schroeder) – Anteil 0 … 1.</summary>
    static void Reverb(float[][] ch, int rate, double mix)
    {
        if (mix <= 0.001) return;
        int[] delays = [(int)(0.0297 * rate), (int)(0.0371 * rate), (int)(0.0411 * rate), (int)(0.0437 * rate)];
        foreach (var c in ch)
        {
            var wet = new float[c.Length];
            foreach (var d in delays) { var buf = new float[c.Length]; for (var i = 0; i < c.Length; i++) { buf[i] = c[i] + (i >= d ? buf[i - d] * 0.78f : 0); wet[i] += buf[i] * 0.25f; } }
            for (var i = 0; i < c.Length; i++) c[i] = (float)(c[i] * (1 - mix * 0.5) + wet[i] * mix);
        }
    }

    /// <summary>Abmischen mit Bussen, Sends und Sidechain (Stereo). Ohne Busse wie das normale Abmischen.</summary>
    public static AudioBuf Mix(JsonObject doc, int rate = 44100)
    {
        var all = AudioMulti.Tracks(doc); var buses = Order(doc);
        var parts = all.Select(t => (T: t, A: AudioEdit.Match(AudioFile.Read(t.File), rate, 2))).ToList();
        var len = parts.Count == 0 ? 0 : parts.Max(p => (int)Math.Round(p.T.Start * rate) + p.A.Length);
        float[][] New() => [new float[len], new float[len]];
        var master = New(); var busBuf = buses.ToDictionary(b => b.Id, _ => New()); var trackOut = new Dictionary<string, float[][]>();
        var sends = Sends(doc);
        foreach (var (t, a) in parts)
        {
            var tb = New(); var g = Math.Pow(10, t.GainDb / 20); var ang = (t.Pan + 1) * Math.PI / 4;
            float gl = (float)(g * Math.Cos(ang) * Math.Sqrt(2)), gr = (float)(g * Math.Sin(ang) * Math.Sqrt(2)); var off = (int)Math.Round(t.Start * rate);
            var auto = AudioMulti.Automation(doc, t.Id); // S4-01: Automation gilt auch mit Bussen
            for (var i = 0; i < a.Length; i++) { var k = auto.Count == 0 ? 1f : AudioMulti.AutoGain(auto, (off + i) / (double)rate); tb[0][off + i] = a.Data[0][i] * gl * k; tb[1][off + i] = a.Data[1][i] * gr * k; }
            trackOut[t.Id] = tb; // auch für Sidechain (unabhängig von Stumm)
            if (!AudioMulti.Audible(all, t)) continue;
            var dest = TrackOut(doc, t.Id); var target = dest != Master && busBuf.TryGetValue(dest, out var bb) ? bb : master;
            for (var c = 0; c < 2; c++) for (var i = 0; i < len; i++) target[c][i] += tb[c][i];
            foreach (var s in sends.Where(s => s.TrackId == t.Id && busBuf.ContainsKey(s.BusId)))
                for (var c = 0; c < 2; c++) for (var i = 0; i < len; i++) busBuf[s.BusId][c][i] += tb[c][i] * (float)s.Level;
        }
        foreach (var b in buses)
        {
            var x = busBuf[b.Id];
            foreach (var c in x) { Biquad(c, rate, "low", 200, b.LowDb); Biquad(c, rate, "mid", 1000, b.MidDb, 1.0); Biquad(c, rate, "high", 5000, b.HighDb); }
            float[]? key = b.Sidechain.Length > 0 && trackOut.TryGetValue(b.Sidechain, out var k) ? [.. k[0].Zip(k[1], (l, r) => Math.Max(Math.Abs(l), Math.Abs(r)))] : null;
            Compress(x, rate, b.CompThresholdDb, b.CompRatio, key);
            Reverb(x, rate, b.ReverbMix);
            var gain = (float)Math.Pow(10, b.GainDb / 20);
            var target = b.Out != Master && busBuf.TryGetValue(b.Out, out var ob) ? ob : master;
            for (var c = 0; c < 2; c++) for (var i = 0; i < len; i++) target[c][i] += x[c][i] * gain;
        }
        static float Soft(float v) => Math.Abs(v) <= 0.95f ? v : Math.Sign(v) * (0.95f + 0.05f * (float)Math.Tanh((Math.Abs(v) - 0.95f) / 0.05f));
        for (var c = 0; c < 2; c++) for (var i = 0; i < len; i++) master[c][i] = Soft(master[c][i]);
        return new AudioBuf(rate, master);
    }
}
