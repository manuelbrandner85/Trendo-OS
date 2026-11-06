using System.Text;
using System.Text.Json;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-17 (06.11.2026): Geräusch-Werkstatt – Regen, Wind, Schritte (Kies/Gras), Maschine, Feuer, Klopfen
/// selbst erzeugt (keine fremden Samples, Rechte eindeutig) – und eine Geräusch-Bibliothek mit Ereignis-Tags für Spiele und Filme.
/// Beim Paket-Export werden Geräusche ohne gültige Lizenz ausgeschlossen und mit Grund aufgelistet.</summary>
public static class Foley
{
    public const string OwnLicense = "selbst erzeugt (Trendo) – frei verwendbar";

    public static readonly (string Id, string Label, string Event)[] Kinds =
    [
        ("regen", "Regen", "Wetter.Regen"), ("wind", "Wind", "Wetter.Wind"), ("schritte-kies", "Schritte auf Kies", "Schritt.Kies"),
        ("schritte-gras", "Schritte auf Gras", "Schritt.Gras"), ("maschine", "Maschine / Motor", "Maschine.Lauf"), ("feuer", "Feuer (Knistern)", "Umgebung.Feuer"),
        ("klopfen", "An die Tür klopfen", "Tuer.Klopfen"),
    ];

    /// <summary>Geräusch erzeugen: Länge in Sekunden (0,2–120), Stärke 0–1, Zufallsstart für Varianten.</summary>
    public static AudioBuf Make(string kind, double seconds, double intensity = 0.6, int seed = 1, int rate = 48000)
    {
        if (!Kinds.Any(k => k.Id == kind)) throw new ArgumentException("Unbekanntes Geräusch: " + kind);
        if (!(seconds is >= 0.2 and <= 120)) throw new ArgumentException("Länge 0,2 bis 120 Sekunden.");
        intensity = Math.Clamp(intensity, 0, 1);
        var n = (int)(seconds * rate); var l = new double[n]; var r = new double[n]; var rng = new Random(seed);
        double W() => rng.NextDouble() * 2 - 1;
        // einfache Filter
        double lpL = 0, lpR = 0, lp2 = 0, hpPrev = 0, hp = 0, l2 = 0, l3 = 0, r2 = 0, r3 = 0, m2 = 0;
        void Burst(int at, double len, double amp, double cutoff, double pan)
        {
            var m = (int)(len * rate); double y = 0; var a = 1 - Math.Exp(-2 * Math.PI * cutoff / rate);
            for (var i = 0; i < m && at + i < n; i++) { y += a * (W() - y); var env = Math.Exp(-i / (double)m * 5) * Math.Min(1, i / (0.002 * rate)); var v = y * env * amp; l[at + i] += v * (1 - pan) ; r[at + i] += v * (1 + pan); }
        }
        switch (kind)
        {
            case "regen":
                for (var i = 0; i < n; i++)
                {
                    var w = W(); hp = 0.95 * (hp + w - hpPrev); hpPrev = w; // Rauschen ohne Tiefen …
                    lpL += 0.45 * (hp - lpL); l2 += 0.45 * (lpL - l2); // … und ohne Zischen (≈ 5 kHz, 12 dB/Oktave)
                    lpR += 0.45 * (0.95 * hp + 0.05 * W() - lpR); r2 += 0.45 * (lpR - r2);
                    l[i] = l2 * 0.35 * (0.5 + intensity); r[i] = r2 * 0.35 * (0.5 + intensity);
                }
                var drops = (int)(seconds * (40 + 160 * intensity));
                for (var k = 0; k < drops; k++) Burst(rng.Next(n), 0.01 + rng.NextDouble() * 0.02, 0.2 + rng.NextDouble() * 0.3, 1200 + rng.NextDouble() * 2500, W() * 0.8);
                break;
            case "wind":
                for (var i = 0; i < n; i++)
                {
                    var t = i / (double)rate; var gust = 0.55 + 0.45 * Math.Sin(2 * Math.PI * 0.13 * t + Math.Sin(2 * Math.PI * 0.031 * t) * 2);
                    var cut = 1 - Math.Exp(-2 * Math.PI * (250 + 900 * gust * intensity) / rate);
                    lpL += cut * (W() - lpL); lpR += cut * (W() - lpR); // drei Stufen: 18 dB/Oktave – Wind ist dunkel
                    l2 += cut * (lpL - l2); r2 += cut * (lpR - r2); l3 += cut * (l2 - l3); r3 += cut * (r2 - r3);
                    l[i] = l3 * gust; r[i] = r3 * gust;
                }
                break;
            case "schritte-kies" or "schritte-gras":
                {
                    var gravel = kind == "schritte-kies"; var step = 0.55 - 0.15 * intensity;
                    for (double t = 0.1; t < seconds - 0.1; t += step * (0.92 + rng.NextDouble() * 0.16))
                    {
                        var at = (int)(t * rate); var pan = (Math.Round(t / step) % 2 == 0 ? -0.15 : 0.15);
                        if (gravel) for (var g = 0; g < 14; g++) Burst(at + (int)(rng.NextDouble() * 0.09 * rate), 0.004 + rng.NextDouble() * 0.01, 0.5 + rng.NextDouble() * 0.5, 2500 + rng.NextDouble() * 4000, pan);
                        else Burst(at, 0.18, 0.7, 900, pan);
                        Burst(at, 0.05, 0.6, 180, pan); // Auftritt (dumpf)
                    }
                    break;
                }
            case "maschine":
                for (var i = 0; i < n; i++)
                {
                    var t = i / (double)rate; var f = 38 + 25 * intensity;
                    var v = 0.5 * Math.Sin(2 * Math.PI * f * t) + 0.3 * Math.Sin(2 * Math.PI * 2 * f * t) + 0.15 * Math.Sin(2 * Math.PI * 3 * f * t + 0.5);
                    lp2 += 0.05 * (W() - lp2); m2 += 0.05 * (lp2 - m2); v = v * (0.8 + 0.2 * Math.Sin(2 * Math.PI * f / 4 * t)) + m2 * 1.2;
                    l[i] = v * 0.6; r[i] = v * 0.6;
                }
                break;
            case "feuer":
                for (var i = 0; i < n; i++) { lpL += 0.02 * (W() - lpL); l[i] = lpL * 1.2; r[i] = lpL * 1.1; }
                var cracks = (int)(seconds * (6 + 25 * intensity));
                for (var k = 0; k < cracks; k++) Burst(rng.Next(n), 0.002 + rng.NextDouble() * 0.006, 0.6 + rng.NextDouble() * 0.4, 4000, W() * 0.6);
                break;
            case "klopfen":
                for (var k = 0; k < 3; k++) { var at = (int)((0.2 + k * 0.22) * rate); Burst(at, 0.09, 1.0, 500, 0); Burst(at, 0.02, 0.5, 2500, 0); }
                break;
        }
        // Ein- und Ausblenden, Pegel auf −3 dBFS Spitze
        var fade = Math.Min(n / 4, (int)(0.05 * rate));
        for (var i = 0; i < fade; i++) { var g = i / (double)fade; l[i] *= g; r[i] *= g; l[n - 1 - i] *= g; r[n - 1 - i] *= g; }
        var peak = 1e-9; for (var i = 0; i < n; i++) peak = Math.Max(peak, Math.Max(Math.Abs(l[i]), Math.Abs(r[i])));
        var gain = 0.708 / peak;
        return new AudioBuf(rate, [l.Select(v => (float)(v * gain)).ToArray(), r.Select(v => (float)(v * gain)).ToArray()]);
    }

    // ───────────── Bibliothek ─────────────

    public sealed record Entry(string Id, string Name, string File, IReadOnlyList<string> Tags, string License, string Source, double Seconds, string Sha256, DateTimeOffset Added)
    {
        /// <summary>Veröffentlichen nur mit bekannter Lizenz.</summary>
        public bool Publishable => !string.IsNullOrWhiteSpace(License) && !License.Trim().Equals("unbekannt", StringComparison.OrdinalIgnoreCase) && !License.Contains("nicht kommerziell ohne Erlaubnis", StringComparison.OrdinalIgnoreCase);
    }

    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    static string Index(string folder) => Path.Combine(folder, "geraeusche.json");

    public static List<Entry> Load(string folder)
    {
        try { return File.Exists(Index(folder)) ? JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(Index(folder)), Opt) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    static void Save(string folder, List<Entry> l) { Directory.CreateDirectory(folder); var tmp = Index(folder) + ".neu"; File.WriteAllText(tmp, JsonSerializer.Serialize(l, Opt)); SafeFile.Move(tmp, Index(folder)); }

    /// <summary>Ton in die Bibliothek legen (WAV 24 Bit). Tags werden bereinigt (Ereignis-Schreibweise „Bereich.Name“ bleibt).</summary>
    public static Entry Add(string folder, AudioBuf a, string name, IEnumerable<string> tags, string license, string source)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name fehlt.");
        Directory.CreateDirectory(folder);
        var safe = string.Concat(name.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ' ? c : '_')).Trim();
        var file = Path.Combine(folder, safe + ".wav");
        for (var i = 2; File.Exists(file); i++) file = Path.Combine(folder, $"{safe} {i}.wav");
        AudioFile.WriteWav(a, file, 24);
        var e = new Entry(Guid.NewGuid().ToString("N")[..8], name.Trim(), file, tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            (license ?? "").Trim(), (source ?? "").Trim(), Math.Round(a.Seconds, 2), FilmDelivery.Sha256(file), DateTimeOffset.Now);
        var l = Load(folder); l.Add(e); Save(folder, l); return e;
    }

    public static List<Entry> Find(string folder, string? tag) => Load(folder).Where(e => File.Exists(e.File) && (string.IsNullOrWhiteSpace(tag) || e.Tags.Any(t => t.Contains(tag, StringComparison.OrdinalIgnoreCase)) || e.Name.Contains(tag, StringComparison.OrdinalIgnoreCase))).ToList();

    public sealed record PackResult(string Folder, IReadOnlyList<Entry> Included, IReadOnlyList<(Entry Entry, string Reason)> Excluded);

    /// <summary>Paket für Veröffentlichung (Spiel, Film): nur Geräusche mit gültiger Lizenz und unveränderter Datei; dazu Lizenzen.csv und Ereignisse.json.</summary>
    public static PackResult Pack(string libraryFolder, string target)
    {
        Directory.CreateDirectory(target);
        var inc = new List<Entry>(); var exc = new List<(Entry, string)>();
        foreach (var e in Load(libraryFolder))
        {
            if (!File.Exists(e.File)) { exc.Add((e, "Datei fehlt")); continue; }
            if (!e.Publishable) { exc.Add((e, $"Lizenz „{(e.License.Length == 0 ? "leer" : e.License)}“ erlaubt keine Veröffentlichung")); continue; }
            if (FilmDelivery.Sha256(e.File) != e.Sha256) { exc.Add((e, "Datei wurde seit der Aufnahme verändert (Prüfsumme)")); continue; }
            File.Copy(e.File, Path.Combine(target, Path.GetFileName(e.File)), true); inc.Add(e);
        }
        var csv = new StringBuilder("Datei;Name;Lizenz;Quelle;SHA-256\n");
        foreach (var e in inc) csv.Append($"{Path.GetFileName(e.File)};{e.Name.Replace(';', ',')};{e.License.Replace(';', ',')};{e.Source.Replace(';', ',')};{e.Sha256}\n");
        File.WriteAllText(Path.Combine(target, "Lizenzen.csv"), csv.ToString(), new UTF8Encoding(true));
        var events = inc.SelectMany(e => e.Tags.Where(t => t.Contains('.')).Select(t => (Event: t, File: Path.GetFileName(e.File)))).GroupBy(x => x.Event)
            .ToDictionary(g => g.Key, g => g.Select(x => x.File).ToList());
        File.WriteAllText(Path.Combine(target, "Ereignisse.json"), JsonSerializer.Serialize(events, Opt));
        if (exc.Count > 0) File.WriteAllText(Path.Combine(target, "Ausgeschlossen.txt"), string.Join("\n", exc.Select(x => $"{x.Item1.Name}: {x.Item2}")) + "\n");
        return new PackResult(target, inc, exc);
    }
}
