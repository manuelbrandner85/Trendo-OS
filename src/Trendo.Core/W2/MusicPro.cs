using System.Globalization;
using System.Text;
using Trendo.Core.Media;

namespace Trendo.Core;

/// <summary>Trendo Musik – Funktionen der Vorbilder nachgebaut (Recherche 04.11.2026, docs/programme/MUSIK-KI-RECHERCHE.md; nur Funktionen, Clean-Room):
/// Mastering-Stile und Ziel je Plattform, Mastern nach Referenz (LANDR) · Üben: Tempo/Tonart, Klick, Kapodaster (Moises) · MIDI-Export (AIVA) ·
/// Ablaufplan je Abschnitt (ElevenLabs „composition plan“, Soundraw) · nahtlose Loops (Stable Audio) · Kurzclip aus dem Refrain (Suno Hooks).</summary>
public static class MusicPro
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.###", Inv);
    const char NL = (char)10;

    // ───────────── Mastering wie LANDR ─────────────
    public sealed record MasterStyle(string Id, string Label, string Hint, string Filter);
    public static readonly MasterStyle[] Styles =
    [
        new("ausgewogen", "Ausgewogen", "neutral, für fast alles", "acompressor=threshold=-18dB:ratio=2:attack=20:release=200"),
        new("warm", "Warm", "weicher, mehr Tiefe, sanfte Höhen", "bass=g=2:f=110,treble=g=-1.5:f=9000,acompressor=threshold=-18dB:ratio=2.2:attack=25:release=250,asoftclip=type=atan"),
        new("offen", "Offen", "brillant, luftig, klare Stimme", "highpass=f=30,equalizer=f=3500:t=o:w=1.2:g=1.5,treble=g=2:f=11000,acompressor=threshold=-16dB:ratio=2:attack=10:release=150"),
    ];
    public sealed record Target(string Id, string Label, double Lufs, double TruePeak);
    public static readonly Target[] Targets =
    [
        new("streaming", "Spotify, YouTube, Amazon", -14, -1), new("apple", "Apple Music", -16, -1), new("podcast", "Podcast / Sprache", -16, -1.5),
        new("cd", "CD / laut", -9, -0.3), new("club", "Club / DJ", -8, -0.3),
    ];
    public static MasterStyle Style(string id) => Styles.FirstOrDefault(s => s.Id == id) ?? Styles[0];
    public static Target TargetOf(string id) => Targets.FirstOrDefault(t => t.Id == id) ?? Targets[0];

    /// <summary>FFmpeg-Kette: Stil (+ optional Angleichung an eine Referenz), dann Lautheit auf das Ziel der Plattform.</summary>
    public static string[] MasterArgs(string input, string output, string style, string target, string? referenceEq = null)
    {
        var t = TargetOf(target);
        var chain = string.Join(",", new[] { Style(style).Filter, referenceEq ?? "", $"loudnorm=I={F(t.Lufs)}:TP={F(t.TruePeak)}:LRA=11" }.Where(x => x.Length > 0));
        var codec = output.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? new[] { "-c:a", "libmp3lame", "-q:a", "1" } : output.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ? ["-c:a", "flac"] : [];
        return ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-af", chain, "-ar", "44100", .. codec, output];
    }

    /// <summary>Bänder für „Mastern nach Referenz“ (Mittenfrequenz in Hz).</summary>
    public static readonly double[] Bands = [60, 150, 400, 1000, 2500, 6000, 12000];

    /// <summary>Energie je Band in dB (Bandpass zweiter Ordnung, Mono, höchstens 90 s).</summary>
    public static double[] BandLevels(AudioBuf a)
    {
        var n = (int)Math.Min(a.Length, a.Rate * 90L);
        var mono = new float[n];
        for (var i = 0; i < n; i++) { float s = 0; foreach (var c in a.Data) s += c[i]; mono[i] = s / a.Channels; }
        var res = new double[Bands.Length];
        for (var b = 0; b < Bands.Length; b++)
        {
            var f0 = Math.Min(Bands[b], a.Rate / 2.0 - 500); var w = 2 * Math.PI * f0 / a.Rate; var al = Math.Sin(w) / (2 * 1.0); var cw = Math.Cos(w);
            double b0 = al, b1 = 0, b2 = -al, a0 = 1 + al, a1 = -2 * cw, a2 = 1 - al; b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0, sum = 0;
            for (var i = 0; i < n; i++) { double x0 = mono[i], y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2; x2 = x1; x1 = x0; y2 = y1; y1 = y0; sum += y0 * y0; }
            res[b] = 10 * Math.Log10(sum / Math.Max(1, n) + 1e-12);
        }
        return res;
    }

    /// <summary>Klangbild an die Referenz angleichen: Unterschied je Band (Gesamtpegel abgezogen), höchstens ±6 dB, als FFmpeg-Equalizer.</summary>
    public static (string Filter, double[] Gains) MatchEq(double[] song, double[] reference)
    {
        var d = song.Zip(reference, (s, r) => r - s).ToArray(); var mean = d.Average();
        var g = d.Select(x => Math.Round(Math.Clamp(x - mean, -6, 6), 1)).ToArray();
        var parts = Bands.Select((f, i) => Math.Abs(g[i]) < 0.5 ? null : $"equalizer=f={F(f)}:t=o:w=1.2:g={F(g[i])}").Where(p => p is not null);
        return (string.Join(",", parts), g);
    }

    // ───────────── Üben wie Moises ─────────────
    /// <summary>Tempo (0,5–1,5) ohne Tonhöhe zu ändern und Tonart um Halbtöne (−6 bis +6) – Rubber Band.</summary>
    public static string[] PracticeArgs(string input, string output, double tempo, int semitones)
    {
        tempo = Math.Clamp(tempo, 0.5, 1.5); semitones = Math.Clamp(semitones, -6, 6);
        var pitch = Math.Pow(2, semitones / 12.0);
        return ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-af", $"rubberband=tempo={F(tempo)}:pitch={F(pitch)}:formant=preserved", output];
    }

    /// <summary>Klickspur zu den Schlägen: jeder <paramref name="perBar"/>. Schlag betont (höher, lauter).</summary>
    public static AudioBuf ClickTrack(IReadOnlyList<double> beats, double seconds, int perBar = 4, int rate = 44100)
    {
        var a = AudioBuf.Silence(seconds, rate, 2); var len = rate / 25;
        for (var k = 0; k < beats.Count; k++)
        {
            var s = (int)(beats[k] * rate); if (s < 0 || s >= a.Length) continue;
            var accent = perBar > 0 && k % perBar == 0; var f = accent ? 1600.0 : 1000.0; var amp = accent ? 0.8f : 0.5f;
            for (var i = 0; i < len && s + i < a.Length; i++)
            {
                var v = (float)(Math.Sin(2 * Math.PI * f * i / rate) * Math.Exp(-i / (rate * 0.006))) * amp;
                a.Data[0][s + i] += v; a.Data[1][s + i] += v;
            }
        }
        return a;
    }

    static readonly string[] Names = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    static readonly string[] EasyMajor = ["C", "G", "D", "A", "E"], EasyMinor = ["A", "E", "D"];

    /// <summary>Kapodaster-Vorschlag: Bund (0–7), mit dem die Tonart mit einfachen Griffen gespielt wird, und die Griff-Tonart.</summary>
    public static (int Capo, string Shape) Capo(string key)
    {
        var p = key.Split(' '); var i = Array.IndexOf(Names, p[0]); var minor = p.Length > 1 && p[1].StartsWith("min", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return (0, key);
        var easy = minor ? EasyMinor : EasyMajor;
        for (var capo = 0; capo <= 7; capo++) { var shape = Names[((i - capo) % 12 + 12) % 12]; if (easy.Contains(shape)) return (capo, shape + (minor ? "m" : "")); }
        return (0, p[0] + (minor ? "m" : ""));
    }

    // ───────────── MIDI-Export wie AIVA ─────────────
    public sealed record Note(int Pitch, double Start, double Length);

    /// <summary>Melodie (MIDI-Werte je Zeitschritt) zu Noten: gleiche Tonhöhe hintereinander wird eine Note, kürzer als 2 Schritte fällt weg.</summary>
    public static List<Note> NotesFromMelody(IReadOnlyList<double?> midi, double hop)
    {
        var notes = new List<Note>(); int? cur = null; var start = 0;
        void Close(int end) { if (cur is { } c && end - start >= 2) notes.Add(new(c, start * hop, (end - start) * hop)); }
        for (var i = 0; i <= midi.Count; i++)
        {
            int? p = i < midi.Count && midi[i] is { } m ? (int)Math.Round(m) : null;
            if (p == cur) continue;
            Close(i); cur = p; start = i;
        }
        return notes;
    }

    static readonly Dictionary<string, int> Root = new() { ["C"] = 0, ["C#"] = 1, ["Db"] = 1, ["D"] = 2, ["D#"] = 3, ["Eb"] = 3, ["E"] = 4, ["F"] = 5, ["F#"] = 6, ["Gb"] = 6, ["G"] = 7, ["G#"] = 8, ["Ab"] = 8, ["A"] = 9, ["A#"] = 10, ["Bb"] = 10, ["B"] = 11 };

    /// <summary>Akkord-Name („Am“, „F#“, „Cmaj7“) → drei MIDI-Töne um C3; unbekannt → leer.</summary>
    public static int[] ChordNotes(string chord)
    {
        var root = chord.Length > 1 && chord[1] is '#' or 'b' ? chord[..2] : chord[..Math.Min(1, chord.Length)];
        if (!Root.TryGetValue(root, out var r)) return [];
        var rest = chord[root.Length..]; var minor = rest.StartsWith('m') && !rest.StartsWith("maj");
        return [48 + r, 48 + r + (minor ? 3 : 4), 48 + r + 7];
    }

    /// <summary>Standard-MIDI-Datei (Format 1): Spur 1 Melodie, Spur 2 Akkorde (ein Akkord je Takt, der Reihe nach wiederholt).</summary>
    public static byte[] Midi(IReadOnlyList<Note> melody, IReadOnlyList<string> chords, double bpm, int beatsPerBar, double seconds)
    {
        const int Ppq = 480; bpm = bpm <= 0 ? 120 : bpm;
        int Tick(double sec) => (int)Math.Round(sec * bpm / 60 * Ppq);
        static void Var(List<byte> b, int v) { var st = new Stack<byte>(); st.Push((byte)(v & 0x7F)); while ((v >>= 7) > 0) st.Push((byte)((v & 0x7F) | 0x80)); b.AddRange(st); }
        byte[] Track(IEnumerable<(int Tick, byte[] Msg)> evs, string name, int? tempoUs)
        {
            var b = new List<byte>();
            var nm = Encoding.UTF8.GetBytes(name); b.AddRange([0, 0xFF, 0x03]); Var(b, nm.Length); b.AddRange(nm);
            if (tempoUs is { } us) b.AddRange([0, 0xFF, 0x51, 3, (byte)(us >> 16), (byte)(us >> 8), (byte)us, 0, 0xFF, 0x58, 4, (byte)beatsPerBar, 2, 24, 8]);
            var last = 0;
            foreach (var (t, m) in evs.OrderBy(e => e.Tick).ThenBy(e => e.Msg[0] & 0xF0) /* Note-aus vor Note-an */) { Var(b, Math.Max(0, t - last)); b.AddRange(m); last = Math.Max(last, t); }
            b.AddRange([0, 0xFF, 0x2F, 0]);
            return [.. "MTrk"u8.ToArray(), (byte)(b.Count >> 24), (byte)(b.Count >> 16), (byte)(b.Count >> 8), (byte)b.Count, .. b];
        }
        var mel = melody.SelectMany(n => new[] { (Tick(n.Start), new byte[] { 0x90, (byte)Math.Clamp(n.Pitch, 0, 127), 96 }), (Tick(n.Start + n.Length), new byte[] { 0x80, (byte)Math.Clamp(n.Pitch, 0, 127), 0 }) });
        var bar = 60.0 / bpm * Math.Max(1, beatsPerBar); var chordEv = new List<(int, byte[])>();
        if (chords.Count > 0)
            for (var k = 0; k * bar < seconds; k++)
                foreach (var p in ChordNotes(chords[k % chords.Count])) { chordEv.Add((Tick(k * bar), [0x91, (byte)p, 72])); chordEv.Add((Tick((k + 1) * bar) - 1, [0x81, (byte)p, 0])); }
        var head = new byte[] { (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6, 0, 1, 0, 2, Ppq >> 8, Ppq & 0xFF };
        return [.. head, .. Track(mel, "Melodie", (int)(60_000_000 / bpm)), .. Track(chordEv, "Akkorde", null)];
    }

    // ───────────── Ablaufplan wie ElevenLabs „composition plan“ ─────────────
    public sealed record PlanPart(string Name, int Bars, string Mood, bool Vocals);
    public static readonly PlanPart[] DefaultPlan = [new("Intro", 4, "ruhig", false), new("Strophe", 8, "erzählend", true), new("Refrain", 8, "kraftvoll", true), new("Strophe", 8, "erzählend", true), new("Refrain", 8, "kraftvoll", true), new("Bridge", 4, "überraschend", true), new("Refrain", 8, "groß", true), new("Outro", 4, "ausklingend", false)];

    /// <summary>Länge des Plans in Sekunden.</summary>
    public static double PlanSeconds(IReadOnlyList<PlanPart> plan, double bpm, int beatsPerBar) => plan.Sum(p => p.Bars) * Math.Max(1, beatsPerBar) * 60.0 / (bpm <= 0 ? 120 : bpm);

    /// <summary>Gerüst für den Liedtext: Abschnitts-Marken mit Stimmung (so versteht ACE-Step den Aufbau), Text der Abschnitte bleibt erhalten.</summary>
    public static string PlanLyrics(IReadOnlyList<PlanPart> plan, IReadOnlyDictionary<string, string>? texts = null)
    {
        var sb = new StringBuilder(); var count = new Dictionary<string, int>();
        foreach (var p in plan)
        {
            count[p.Name] = count.GetValueOrDefault(p.Name) + 1;
            sb.Append($"[{p.Name}{(p.Mood.Length > 0 ? " - " + p.Mood : "")}]").Append(NL);
            var key = p.Name + " " + count[p.Name];
            if (!p.Vocals) sb.Append("[Instrumental]").Append(NL);
            else if ((texts?.GetValueOrDefault(key) ?? texts?.GetValueOrDefault(p.Name)) is { Length: > 0 } t) sb.Append(t.Trim()).Append(NL);
            sb.Append(NL);
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>Vorhandene Texte je Abschnitt aus einem Liedtext („[Strophe]“ … bis zur nächsten Marke) – Schlüssel „Name N“ und „Name“ (erstes Vorkommen).</summary>
    public static Dictionary<string, string> SectionTexts(string lyrics)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); var count = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? cur = null; var buf = new List<string>();
        void Flush()
        {
            if (cur is null) return;
            var t = string.Join(NL, buf).Trim(); buf.Clear();
            if (t.Length == 0 || t == "[Instrumental]") return;
            count[cur] = count.GetValueOrDefault(cur) + 1;
            d[cur + " " + count[cur]] = t; d.TryAdd(cur, t);
        }
        foreach (var raw in lyrics.Replace(((char)13).ToString(), "").Split(NL))
        {
            var l = raw.Trim();
            if (l.StartsWith('[') && l.EndsWith(']') && l != "[Instrumental]") { Flush(); cur = l[1..^1].Split(" - ")[0].Trim(); continue; }
            buf.Add(raw);
        }
        Flush();
        return d;
    }

    // ───────────── Nahtlose Loops wie Stable Audio ─────────────
    /// <summary>Loop aus dem Anfang: genau <paramref name="bars"/> Takte; das Stück danach wird gleichmäßig (gleiche Leistung) in den Anfang überblendet – kein Knacken am Übergang.</summary>
    public static AudioBuf MakeLoop(AudioBuf a, double bpm, int beatsPerBar, int bars, double fade = 0.25)
    {
        var len = (int)Math.Round(bars * Math.Max(1, beatsPerBar) * 60.0 / (bpm <= 0 ? 120 : bpm) * a.Rate);
        var fl = (int)(fade * a.Rate);
        if (len + fl > a.Length) throw new InvalidOperationException($"Für {bars} Takte ist das Stück zu kurz.");
        var outD = a.Data.Select(c =>
        {
            var o = c[..len];
            for (var i = 0; i < fl; i++) { var t = i / (float)fl; o[i] = (float)(c[i] * Math.Sin(t * Math.PI / 2) + c[len + i] * Math.Cos(t * Math.PI / 2)); }
            return o;
        }).ToArray();
        return new AudioBuf(a.Rate, outD);
    }

    // ───────────── Kurzclip wie Suno Hooks ─────────────
    /// <summary>Stärkste Stelle (meist der Refrain): Abschnitt mit der höchsten Energie, Länge <paramref name="seconds"/>.</summary>
    public static double HookStart(IReadOnlyList<CoverStudio.Section> sections, double duration, double seconds)
    {
        if (sections.Count == 0) return Math.Max(0, Math.Min(duration - seconds, duration * 0.3));
        var best = sections.MaxBy(s => s.Energy)!;
        return Math.Max(0, Math.Min(best.Start, duration - seconds));
    }

    /// <summary>Hochkant-Video 1080 × 1920: Titelbild, goldene Wellenform, Titel; Ton mit Ein- und Ausblenden.</summary>
    public static string[] HookArgs(string audio, string? cover, string output, double start, double seconds, string title)
    {
        var fade = Math.Min(1.5, seconds / 6);
        var font = VideoEdit.FontFile() is { } ff ? $"fontfile='{ff.Replace("\\", "/").Replace(":", "\\:")}':" : ""; // Windows: Schrift ausdrücklich (sonst Fontconfig-Fehler)
        var esc = title.Replace("\\", "\\\\").Replace("'", "’").Replace(":", "\\:").Replace("%", "\\%");
        var bg = cover is not null && File.Exists(cover)
            ? "[1:v]scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,boxblur=18:2,eq=brightness=-0.15[bg];[1:v]scale=900:900:force_original_aspect_ratio=decrease[fg];[bg][fg]overlay=(W-w)/2:360[base]"
            : "color=c=0x101826:s=1080x1920:d=" + F(seconds) + "[base]";
        var f = $"{bg};[0:a]showwaves=s=1000x220:mode=cline:colors=0xF5B83D:rate=30,format=rgba[wv];[base][wv]overlay=40:1380:shortest=1,drawtext={font}text='{esc}':fontcolor=white:fontsize=64:x=(w-text_w)/2:y=1660[v];[0:a]afade=t=in:d={F(fade)},afade=t=out:st={F(seconds - fade)}:d={F(fade)}[a]";
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y", "-ss", F(start), "-t", F(seconds), "-i", audio };
        if (cover is not null && File.Exists(cover)) a.AddRange(["-loop", "1", "-i", cover]);
        a.AddRange(["-filter_complex", f, "-map", "[v]", "-map", "[a]", "-t", F(seconds), "-r", "30", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", output]);
        return [.. a];
    }
}
