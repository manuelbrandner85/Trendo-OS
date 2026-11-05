namespace Trendo.Core;

/// <summary>Trendo Musik Profi 26 (05.11.2026): mitlaufender Liedtext beim Abspielen, Tonart- und Tempo-Raster, Fassungen nebeneinander vergleichen
/// (Vorbilder: Spotify/Apple-Music-Liedtext, Ableton/Logic-Taktraster, A/B-Vergleich beim Mastering).</summary>
public static class SongFollow
{
    /// <summary>Eine Liedzeile mit Zeit. <paramref name="Timed"/> = aus „Lied verstehen“ gemessen; sonst geschätzt.</summary>
    public sealed record FollowLine(double Start, double End, string Text, string Part, bool Timed);

    /// <summary>Zeitplan des Liedtexts: gemessene Zeilen aus „Lied verstehen“, wenn vorhanden; sonst Schätzung –
    /// Vorspiel 8 %, Nachspiel 6 %, dazwischen nach Silbenmenge verteilt, Abschnittswechsel bekommen eine kleine Pause.</summary>
    public static List<FollowLine> Plan(string lyrics, double seconds, SongUnderstanding.Result? understanding = null)
    {
        if (understanding is { Lines.Count: > 0 } u && u.Lines.Any(l => l.Timed))
            return [.. u.Lines.Where(l => l.Timed).OrderBy(l => l.Start).Select(l => new FollowLine(l.Start, l.End, l.Text, l.Part, true))];
        var lines = new List<(string Text, string Part, bool NewPart)>(); var part = ""; var fresh = false;
        foreach (var raw in (lyrics ?? "").Replace("\r", "").Split('\n'))
        {
            var l = raw.Trim(); if (l.Length == 0) continue;
            if (l.StartsWith('[') && l.EndsWith(']')) { part = l.Trim('[', ']').Trim(); fresh = true; continue; }
            lines.Add((l, part, fresh)); fresh = false;
        }
        if (lines.Count == 0 || seconds <= 0) return [];
        double a = seconds * 0.08, b = seconds * 0.94;
        var weights = lines.Select(l => Math.Max(3, SongTools.Syllables(l.Text)) + (l.NewPart ? 3 : 0)).ToList();
        var per = (b - a) / weights.Sum(); var t = a; var res = new List<FollowLine>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].NewPart && i > 0) t += 3 * per; // kurze Pause vor dem neuen Abschnitt
            var len = (weights[i] - (lines[i].NewPart && i > 0 ? 3 : 0)) * per;
            res.Add(new FollowLine(Math.Round(t, 2), Math.Round(t + len * 0.92, 2), lines[i].Text, lines[i].Part, false)); t += len;
        }
        return res;
    }

    /// <summary>Zeile, die zur Zeit <paramref name="t"/> läuft (bzw. die zuletzt begonnene); −1 vor der ersten.</summary>
    public static int At(IReadOnlyList<FollowLine> plan, double t)
    {
        var idx = -1;
        for (var i = 0; i < plan.Count; i++) if (plan[i].Start <= t + 0.05) idx = i; else break;
        return idx;
    }

    // ───────────── Raster ─────────────
    public sealed record Bar(int Number, double Start, double End);

    /// <summary>Takte: aus gemessenen Schlägen (Gruppen zu je Zähler der Taktart), sonst gleichmäßig aus dem Tempo.</summary>
    public static List<Bar> Bars(double bpm, string? meter, double seconds, IReadOnlyList<double>? beats = null)
    {
        var per = int.TryParse((meter ?? "4/4").Split('/')[0], out var m) && m is > 0 and <= 12 ? m : 4;
        var res = new List<Bar>();
        if (beats is { Count: > 4 })
        {
            for (var i = 0; i + per <= beats.Count; i += per) res.Add(new Bar(res.Count + 1, beats[i], i + per < beats.Count ? beats[i + per] : Math.Min(seconds, beats[^1] + (beats[^1] - beats[^2]))));
            return res;
        }
        if (bpm <= 0 || seconds <= 0) return res;
        var len = per * 60 / bpm;
        for (var t = 0.0; t < seconds - 0.01; t += len) res.Add(new Bar(res.Count + 1, Math.Round(t, 3), Math.Round(Math.Min(seconds, t + len), 3)));
        return res;
    }

    static readonly string[] PitchEn = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    static readonly string[] PitchDe = ["C", "Cis", "D", "Es", "E", "F", "Fis", "G", "As", "A", "B", "H"];

    /// <summary>Tonleiter einer Tonart („A minor“ → A H C D E F G; „Eb major“ → Es F G As B C D) in deutscher Schreibweise; leer, wenn unbekannt.</summary>
    public static List<string> Scale(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var p = key.Trim().Split(' '); var i = Array.IndexOf(PitchEn, p[0]); if (i < 0) return [];
        var minor = p.Length > 1 && p[1].StartsWith("min", StringComparison.OrdinalIgnoreCase);
        int[] steps = minor ? [0, 2, 3, 5, 7, 8, 10] : [0, 2, 4, 5, 7, 9, 11];
        return [.. steps.Select(s => PitchDe[(i + s) % 12])];
    }

    /// <summary>Paralleltonart (A-Moll ↔ C-Dur) – zum Wechseln in Brücke oder Refrain.</summary>
    public static string? Relative(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var p = key.Trim().Split(' '); var i = Array.IndexOf(PitchEn, p[0]); if (i < 0) return null;
        var minor = p.Length > 1 && p[1].StartsWith("min", StringComparison.OrdinalIgnoreCase);
        return minor ? PitchEn[(i + 3) % 12] + " major" : PitchEn[(i + 9) % 12] + " minor";
    }

    // ───────────── Fassungen ─────────────
    /// <summary>Alle Fassungen eines Lieds: das Ausgangslied und alles, was davon abstammt (Cover, Abschnitt neu, verlängert, mit eigener Stimme) – ohne getrennte Spuren.
    /// Reihenfolge: das Ausgangslied zuerst, dann nach Erstellung.</summary>
    public static List<MusicSong> Versions(IReadOnlyList<MusicSong> all, MusicSong s)
    {
        var byId = all.ToDictionary(x => x.Id);
        MusicSong Root(MusicSong x) { var seen = new HashSet<string>(); while (x.ParentId is { } pid && byId.TryGetValue(pid, out var p) && seen.Add(pid)) x = p; return x; }
        var root = Root(s);
        return [.. all.Where(x => x.Kind is not ("begleitung" or "gesang") && Root(x).Id == root.Id).OrderBy(x => x.Id == root.Id ? 0 : 1).ThenBy(x => x.Created)];
    }

    public static string KindLabel(string kind) => kind switch
    {
        "neu" => "Original", "eigen" => "Eigene Datei", "cover" => "Cover", "abschnitt" => "Abschnitt neu", "laenger" => "Verlängert", "aufnahme" => "Mit meiner Stimme", _ => kind,
    };
}
