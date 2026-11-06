using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Live streamen wie OBS (Profi 27, 05.11.2026): Szenen mit Quellen (Hauptbild plus Einblendungen: Bild/Logo, Bauchbinde, zweite Kamera),
/// Tonmischer mit Mikrofon und Musik (Lautstärke, stumm, Pegel in LUFS), Sendeplan und Probelauf vor dem Senden.
/// Eine Szene ist ein fertiges Bild; Umschalten startet die Aufnahme kurz neu (Trendo nutzt FFmpeg, kein eigenes Mischpult im Speicher).</summary>
public sealed record LiveOverlay(string Id, string Kind, string Value, double X, double Y, double Width, bool Visible = true)
{
    public static readonly string[] Kinds = ["bild", "text", "kamera"];
    public string Label => Kind switch { "bild" => "Bild „" + Path.GetFileName(Value) + "“", "text" => "Bauchbinde „" + Value + "“", _ => "Kamera „" + Value + "“" };
}

public sealed record LiveScene(string Id, string Name, VideoKind Main, List<LiveOverlay> Overlays);

/// <summary>Tonmischer: Mikrofon und Musik je mit Lautstärke (dB) und Stumm.</summary>
public sealed record LiveMix(double MicDb = 0, bool MicMute = false, string? MusicFile = null, double MusicDb = -14, bool MusicMute = false);

/// <summary>Ein Punkt im Sendeplan: ab Minute, Szene, Notiz für den Moderator.</summary>
public sealed record LiveCue(TimeSpan At, string Scene, string Note);

public sealed class LiveShow
{
    public List<LiveScene> Scenes { get; set; } = [];
    public string? Active { get; set; }
    public LiveMix Mix { get; set; } = new();
    public List<LiveCue> Plan { get; set; } = [];

    public LiveScene? Current => Scenes.FirstOrDefault(s => s.Id == Active) ?? Scenes.FirstOrDefault();

    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    public static LiveShow Load(string dir)
    {
        var f = Path.Combine(dir, "szenen.json");
        try { if (File.Exists(f) && JsonSerializer.Deserialize<LiveShow>(File.ReadAllText(f), Opt) is { } s) return s.Normal(); } catch (JsonException) { }
        return Default();
    }
    public void Save(string dir) { Directory.CreateDirectory(dir); var f = Path.Combine(dir, "szenen.json"); var tmp = f + ".neu"; File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opt)); SafeFile.Move(tmp, f); }
    LiveShow Normal() { if (Scenes.Count == 0) Scenes = Default().Scenes; Active ??= Scenes[0].Id; return this; }

    /// <summary>Startbesetzung wie bei Profis: Start-Bild, Kamera, Bildschirm, Pause.</summary>
    public static LiveShow Default() => new()
    {
        Scenes =
        [
            new("start", "Gleich geht's los", VideoKind.Screen, [new("t1", "text", "Gleich geht's los", 0.05, 0.78, 0.9)]),
            new("kamera", "Kamera", VideoKind.Camera, []),
            new("bildschirm", "Bildschirm", VideoKind.Screen, []),
            new("pause", "Pause", VideoKind.Screen, [new("t2", "text", "Kurze Pause – gleich weiter", 0.05, 0.78, 0.9)]),
        ],
        Active = "bildschirm",
    };

    public LiveScene Add(string name, VideoKind main)
    {
        var n = name.Trim(); if (n.Length == 0) throw new ArgumentException("Bitte einen Namen für die Szene.");
        if (Scenes.Count >= 12) throw new InvalidOperationException("Höchstens 12 Szenen.");
        var s = new LiveScene("s" + Guid.NewGuid().ToString("N")[..6], n.Length > 40 ? n[..40] : n, main, []); Scenes.Add(s); return s;
    }

    public LiveOverlay AddOverlay(string sceneId, string kind, string value)
    {
        var s = Scenes.FirstOrDefault(x => x.Id == sceneId) ?? throw new InvalidOperationException("Diese Szene gibt es nicht.");
        if (!LiveOverlay.Kinds.Contains(kind)) throw new ArgumentException("Unbekannte Quelle: " + kind);
        var v = value.Trim(); if (v.Length == 0) throw new ArgumentException(kind == "text" ? "Bitte den Text der Bauchbinde eingeben." : kind == "bild" ? "Bitte ein Bild wählen." : "Bitte eine Kamera wählen.");
        if (kind == "bild" && !File.Exists(v)) throw new FileNotFoundException("Bild nicht gefunden: " + Path.GetFileName(v));
        if (s.Overlays.Count >= 6) throw new InvalidOperationException("Höchstens 6 Einblendungen je Szene.");
        var o = kind switch
        {
            "text" => new LiveOverlay("o" + Guid.NewGuid().ToString("N")[..6], kind, v.Length > 80 ? v[..80] : v, 0.05, 0.8, 0.9),
            "bild" => new LiveOverlay("o" + Guid.NewGuid().ToString("N")[..6], kind, v, 0.78, 0.04, 0.18), // Logo oben rechts
            _ => new LiveOverlay("o" + Guid.NewGuid().ToString("N")[..6], kind, v, 0.68, 0.64, 0.28), // Bild im Bild unten rechts
        };
        s.Overlays.Add(o); return o;
    }
}

/// <summary>FFmpeg-Graph für Szene und Tonmischer, dazu Pegelmessung (EBU R128, momentan) je Kanal in eine Textdatei.</summary>
public static class LiveCompose
{
    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Pegel-Datei eines Kanals (relativ zum Arbeitsordner des FFmpeg-Prozesses, damit kein Laufwerks-Doppelpunkt in den Filter muss).</summary>
    public static string MeterFile(string meterDir, string channel) => meterDir.TrimEnd('/', '\\') + "/" + channel + ".txt";

    /// <summary>Text für drawtext maskieren.</summary>
    public static string DrawText(string t) => t.Replace("\\", "\\\\").Replace("'", "’").Replace(":", "\\:"); // % bleibt wörtlich (expansion=none)

    /// <summary>Video: Hauptbild [0:v] auf die Ausgabegröße, darüber die sichtbaren Einblendungen; Ergebnis [vmain]. Gibt die zusätzlichen Eingänge zurück (ab <paramref name="nextInput"/>).</summary>
    public static (List<string> Inputs, string Graph, int Next) Video(CaptureChoice c, LiveScene? scene, int nextInput, string? font)
    {
        var scale = $"{c.Width}:{c.Height}";
        var g = $"[0:v]scale={scale}:force_original_aspect_ratio=decrease,pad={scale}:(ow-iw)/2:(oh-ih)/2,setsar=1[b0]";
        var inputs = new List<string>(); var cur = "b0"; var n = 0;
        foreach (var o in scene?.Overlays.Where(x => x.Visible) ?? [])
        {
            var next = "b" + (++n);
            int x = (int)Math.Round(Math.Clamp(o.X, 0, 1) * c.Width), y = (int)Math.Round(Math.Clamp(o.Y, 0, 1) * c.Height), w = Math.Max(16, (int)Math.Round(Math.Clamp(o.Width, 0.05, 1) * c.Width) / 2 * 2);
            switch (o.Kind)
            {
                case "text":
                    if (font is null) continue;
                    g += $";[{cur}]drawtext=fontfile='{font.Replace("\\", "/").Replace(":", "\\:")}':expansion=none:text='{DrawText(o.Value)}':fontcolor=white:fontsize=h/16:box=1:boxcolor=black@0.55:boxborderw=14:x={x}:y={y}[{next}]";
                    break;
                case "bild":
                    inputs.AddRange(["-loop", "1", "-framerate", c.Fps.ToString(CultureInfo.InvariantCulture), "-i", o.Value]);
                    g += $";[{nextInput}:v]scale={w}:-2,format=rgba[o{n}];[{cur}][o{n}]overlay=x={x}:y={y}:shortest=0[{next}]"; nextInput++;
                    break;
                case "kamera":
                    if (c.Os == CaptureOs.Windows) inputs.AddRange(["-thread_queue_size", "1024", "-f", "dshow", "-framerate", c.Fps.ToString(CultureInfo.InvariantCulture), "-i", "video=" + o.Value]);
                    else if (c.Os == CaptureOs.Mac) inputs.AddRange(["-f", "avfoundation", "-framerate", c.Fps.ToString(CultureInfo.InvariantCulture), "-i", o.Value + ":none"]);
                    else inputs.AddRange(["-f", "v4l2", "-framerate", c.Fps.ToString(CultureInfo.InvariantCulture), "-i", o.Value]);
                    g += $";[{nextInput}:v]scale={w}:-2[o{n}];[{cur}][o{n}]overlay=x={x}:y={y}[{next}]"; nextInput++;
                    break;
                default: continue;
            }
            cur = next;
        }
        g += $";[{cur}]null[vmain]";
        return (inputs, g, nextInput);
    }

    /// <summary>Ton: Mikrofon (Eingang <paramref name="micInput"/>) und Musik (neuer Eingang, endlos) mit Lautstärke und Stumm, je Kanal Pegel-Datei, gemischt zu [aout].
    /// null = kein Ton aus dem Mischer (dann bleibt es beim bisherigen Weg).</summary>
    public static (List<string> Inputs, string? Graph, int Next) Audio(LiveMix? mix, int? micInput, int nextInput, string? meterDir)
    {
        var inputs = new List<string>(); var chains = new List<string>(); var labels = new List<string>();
        string Meter(string ch) => meterDir is null ? "" : $",ebur128=metadata=1,ametadata=mode=print:key=lavfi.r128.M:file={MeterFile(meterDir, ch)}";
        mix ??= new LiveMix();
        if (micInput is int mi)
        {
            chains.Add($"[{mi}:a]aresample=44100,volume={(mix.MicMute ? "0" : F(Math.Pow(10, Math.Clamp(mix.MicDb, -60, 20) / 20)))}{Meter("mikrofon")}[amic]"); labels.Add("[amic]");
        }
        if (mix.MusicFile is { Length: > 0 } mf && File.Exists(mf))
        {
            inputs.AddRange(["-stream_loop", "-1", "-i", mf]);
            chains.Add($"[{nextInput}:a]aresample=44100,aformat=channel_layouts=stereo,volume={(mix.MusicMute ? "0" : F(Math.Pow(10, Math.Clamp(mix.MusicDb, -60, 20) / 20)))}{Meter("musik")}[amus]"); labels.Add("[amus]"); nextInput++;
        }
        if (labels.Count == 0) return (inputs, null, nextInput);
        var mixG = labels.Count == 1 ? $"{labels[0]}anull[aout]" : $"{string.Concat(labels)}amix=inputs={labels.Count}:normalize=0:duration=first[aout]";
        return (inputs, string.Join(";", chains) + ";" + mixG, nextInput);
    }
}

/// <summary>Pegel lesen und Ton-Wache: Aussetzer (Mikrofon offen, aber über 5 s still) und Übersteuern melden.</summary>
public static class LiveMeter
{
    static readonly Regex Line = new(@"pts_time:(?<t>[0-9.]+)[\s\S]*?lavfi\.r128\.M=(?<m>-?[0-9.]+|-inf)", RegexOptions.Compiled);

    /// <summary>Messwerte (Zeit in s, momentane Lautheit in LUFS) aus der Pegel-Datei; leer, wenn es sie nicht gibt.</summary>
    public static List<(double T, double Lufs)> Read(string text)
    {
        var r = new List<(double, double)>();
        foreach (Match m in Line.Matches(text))
            r.Add((double.Parse(m.Groups["t"].Value, CultureInfo.InvariantCulture), m.Groups["m"].Value == "-inf" ? -120 : double.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture)));
        return r;
    }

    /// <summary>Anzeige 0–1 für den Pegelbalken (−60 LUFS = leer, 0 = voll).</summary>
    public static double Bar(double lufs) => Math.Clamp((lufs + 60) / 60, 0, 1);

    /// <summary>Warnung aus den letzten Messwerten eines Kanals oder null.</summary>
    public static string? Warn(IReadOnlyList<(double T, double Lufs)> v, string channel, bool muted)
    {
        if (v.Count == 0 || muted) return null;
        var end = v[^1].T;
        var last = v.Where(x => x.T >= end - 5).ToList();
        if (end >= 5 && last.Count >= 5 && last.All(x => x.Lufs < -55)) return $"{channel}: seit über 5 Sekunden kein Ton – Mikrofon prüfen (stumm geschaltet oder falsches Gerät?).";
        if (last.Count(x => x.Lufs > -6) >= 3) return $"{channel}: zu laut, klingt verzerrt – Regler etwas herunter.";
        return null;
    }
}

/// <summary>Probelauf: kurze Probeaufnahme mit allen Quellen, danach prüfen wie ein Regisseur vor der Sendung.</summary>
public static class LiveProbe
{
    public sealed record Check(string Light, string Text);

    /// <summary>FFmpeg-Befehl zum Auswerten der Probeaufnahme (Lautstärke, schwarze Stellen).</summary>
    public static List<string> AnalyzeArgs(string file) => ["-hide_banner", "-nostats", "-i", file, "-vf", "blackdetect=d=1:pix_th=0.08", "-af", "volumedetect", "-f", "null", "-"];

    /// <summary>Ergebnis lesen: Bild da (nicht schwarz), Ton da und nicht zu leise/zu laut, Datenrate passt zur Plattform.</summary>
    public static List<Check> Evaluate(string stderr, double seconds, long bytes, int targetKbps, bool expectAudio)
    {
        var res = new List<Check>();
        double D(string key) { var m = Regex.Match(stderr, key + @":\s*(-?[0-9.]+|-inf)"); return !m.Success ? double.NaN : m.Groups[1].Value == "-inf" ? -120 : double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); }
        var black = Regex.Matches(stderr, @"black_duration:\s*([0-9.]+)").Sum(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        if (seconds <= 0 || bytes < 1000) { res.Add(new("rot", "Die Probeaufnahme ist leer – Quelle oder Gerät prüfen.")); return res; }
        res.Add(black >= seconds * 0.8 ? new("rot", "Das Bild ist schwarz – Quelle gewählt? Kamera von einem anderen Programm belegt?")
            : black > 1 ? new("gelb", $"{black:0} s schwarzes Bild – kurz verdeckt oder umgeschaltet?") : new("gruen", "Bild ist da."));
        var mean = D("mean_volume"); var max = D("max_volume");
        if (!expectAudio) res.Add(new("gelb", "Ohne Ton – Mikrofon oder Musik im Tonmischer einschalten, falls gewünscht."));
        else if (double.IsNaN(mean) || mean < -50) res.Add(new("rot", "Kein Ton zu hören – Mikrofon prüfen (Gerät, stumm, Regler)."));
        else if (max >= -0.5) res.Add(new("gelb", $"Ton übersteuert (Spitze {max:0.0} dB) – Mikrofon-Regler etwas herunter."));
        else if (mean < -35) res.Add(new("gelb", $"Ton sehr leise (Mittel {mean:0} dB) – Mikrofon-Regler hoch."));
        else res.Add(new("gruen", $"Ton gut (Mittel {mean:0} dB, Spitze {max:0.0} dB)."));
        var kbps = bytes * 8 / 1000.0 / seconds;
        res.Add(kbps > targetKbps * 1.6 ? new("gelb", $"Datenrate {kbps:0} kbit/s – höher als geplant ({targetKbps}); bei schwachem Internet Qualität senken.")
            : new("gruen", $"Datenrate {kbps:0} kbit/s – für die Sendung brauchst du mindestens {targetKbps * 1.5 / 1000:0.0} Mbit/s Upload.".Replace('.', ',')));
        return res;
    }

    // ───────────── Sendeplan ─────────────
    /// <summary>Sendeplan lesen: Zeilen „mm:ss Szene – Notiz“ (auch mit „ / “ getrennt). Unbekannte Szenen werden mit Grund abgelehnt.</summary>
    public static (List<LiveCue>? Plan, string? Error) ParsePlan(string text, IReadOnlyList<LiveScene> scenes)
    {
        var plan = new List<LiveCue>();
        foreach (var raw in Regex.Split(text.Replace("\r", ""), @"\n|\s+/\s+"))
        {
            var l = raw.Trim(); if (l.Length == 0) continue;
            var m = Regex.Match(l, @"^(?<m>\d{1,3}):(?<s>\d{2})\s+(?<scene>[^–\-]+?)\s*(?:[–-]\s*(?<note>.*))?$");
            if (!m.Success) return (null, $"Zeile „{l}“ – Form „mm:ss Szene – Notiz“");
            var name = m.Groups["scene"].Value.Trim();
            var sc = scenes.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || s.Id.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (sc is null) return (null, $"Szene „{name}“ gibt es nicht ({string.Join(", ", scenes.Select(s => s.Name))})");
            plan.Add(new LiveCue(TimeSpan.FromMinutes(int.Parse(m.Groups["m"].Value)) + TimeSpan.FromSeconds(int.Parse(m.Groups["s"].Value)), sc.Id, m.Groups["note"].Value.Trim()));
        }
        if (plan.Count == 0) return (null, "kein Sendeplan");
        return ([.. plan.OrderBy(c => c.At)], null);
    }

    public static string PlanText(IEnumerable<LiveCue> plan, IReadOnlyList<LiveScene> scenes) =>
        string.Join(" / ", plan.Select(c => $"{(int)c.At.TotalMinutes:00}:{c.At.Seconds:00} {scenes.FirstOrDefault(s => s.Id == c.Scene)?.Name ?? c.Scene}" + (c.Note.Length > 0 ? " – " + c.Note : "")));

    /// <summary>Laufender und nächster Punkt des Sendeplans bei <paramref name="elapsed"/>.</summary>
    public static (LiveCue? Now, LiveCue? Next) At(IReadOnlyList<LiveCue> plan, TimeSpan elapsed) =>
        (plan.LastOrDefault(c => c.At <= elapsed), plan.FirstOrDefault(c => c.At > elapsed));
}
