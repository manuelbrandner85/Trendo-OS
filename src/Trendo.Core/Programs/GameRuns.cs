using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>T-0650, E6: wiederholbare Testläufe. Jeder Lauf hält Startwert, Engine, Eingaben (Startargumente ohne Pfade), die Prüfpunkte des
/// Testspieler-Bots, alle Messwerte der TRENDO-Zeilen und Kopien seiner Bilder fest. Zwei Läufe mit gleichem Startwert und gleichen Eingaben lassen sich
/// vergleichen; Verschlechterungen werden gemeldet. Nichts wird geschätzt: fehlt eine Messzeile, fehlt der Wert.</summary>
public static partial class GameRuns
{
    public sealed record Run(string Id, DateTime At, int Seed, string Engine, IReadOnlyList<string> Inputs,
        IReadOnlyDictionary<string, bool> Checks, IReadOnlyDictionary<string, double> Values, IReadOnlyDictionary<string, string> Shots);

    public sealed record Change(string What, string Before, string After, bool Worse);
    public sealed record Comparison(bool Comparable, string Note, IReadOnlyList<Change> Changes)
    {
        public int Worse => Changes.Count(c => c.Worse);
    }

    /// <summary>Startwert für einen neuen Lauf (positiv, damit er als Kommandozeilen-Zahl taugt).</summary>
    public static int NewSeed(Random? r = null) => (r ?? Random.Shared).Next(1, int.MaxValue);

    /// <summary>Startargument für Unreal: der Spielcode setzt damit FMath::RandInit und SRandInit.</summary>
    public static string SeedArg(int seed) => "-trendoseed=" + seed.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"TRENDO-([A-ZÄÖÜ]+)\b(.*)$")] private static partial Regex Tag();
    [GeneratedRegex(@"([A-Za-zÄÖÜäöüß]+)=(-?\d+(?:\.\d+)?)")] private static partial Regex Pair();

    /// <summary>Messwerte aus dem Protokoll: jedes „Name=Zahl“ jeder TRENDO-Zeile als „ART.Name“; kommt ein Name öfter vor, gilt der letzte Wert.
    /// Alle Zeilen einer Art zählen, denn manche Arten schreiben mehrere Zeilen (z. B. TRENDO-WAGEN „gesteuert=…“ und danach „ausgestiegen“).
    /// Sonderwert −1 bei BODEN.Boden heißt „kein Boden getroffen“ und fehlt darum.</summary>
    public static SortedDictionary<string, double> Values(string log)
    {
        var v = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var raw in (log ?? "").Split('\n'))
        {
            if (Tag().Match(raw.Trim()) is not { Success: true } m) continue;
            var tag = m.Groups[1].Value;
            foreach (Match p in Pair().Matches(m.Groups[2].Value))
                if (double.TryParse(p.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    var key = $"{tag}.{p.Groups[1].Value}";
                    if (key == "BODEN.Boden" && d < 0) v.Remove(key); else v[key] = d;
                }
        }
        return v;
    }

    /// <summary>Eingaben ohne Ordnerpfade, damit Läufe auf verschiedenen Rechnern oder in kopierten Projekten vergleichbar bleiben.</summary>
    public static List<string> Inputs(IEnumerable<string> args) =>
        [.. args.Where(a => !a.EndsWith(".uproject", StringComparison.OrdinalIgnoreCase) && !a.StartsWith("-abslog=", StringComparison.OrdinalIgnoreCase) && !a.StartsWith("-trendoseed=", StringComparison.OrdinalIgnoreCase))];

    public static Run From(string log, int seed, string engine, IEnumerable<string> args, IEnumerable<(string Path, string Label)> shots, DateTime? at = null)
    {
        var when = at ?? DateTime.Now;
        var checks = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        foreach (var f in GamePlaytest.Read(log)) checks[f.Area] = f.Ok;
        return new(when.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture), when, seed, engine, Inputs(args), checks, Values(log),
            shots.GroupBy(s => s.Label).ToDictionary(g => g.Key, g => g.First().Path));
    }

    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    /// <summary>Lauf speichern: lauf.json und Kopien der Bilder in einem eigenen Ordner (der nächste Lauf überschreibt die Bilder im Spiel).</summary>
    public static string Save(string root, Run r)
    {
        var id = r.Id; var dir = Path.Combine(root, id);
        for (var i = 2; Directory.Exists(dir); i++) dir = Path.Combine(root, id = $"{r.Id}-{i}");
        Directory.CreateDirectory(dir);
        var shots = new JsonObject();
        foreach (var (label, path) in r.Shots)
        {
            if (!File.Exists(path)) continue;
            var name = Path.GetFileName(path); File.Copy(path, Path.Combine(dir, name), true); shots[label] = name;
        }
        var o = new JsonObject
        {
            ["id"] = id, ["at"] = r.At.ToString("o", CultureInfo.InvariantCulture), ["seed"] = r.Seed, ["engine"] = r.Engine,
            ["inputs"] = new JsonArray([.. r.Inputs.Select(x => (JsonNode)x)]),
            ["checks"] = new JsonObject(r.Checks.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
            ["values"] = new JsonObject(r.Values.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
            ["shots"] = shots,
        };
        File.WriteAllText(Path.Combine(dir, "lauf.json"), o.ToJsonString(Opt));
        Prune(root, Keep);
        return dir;
    }

    /// <summary>So viele Läufe bleiben liegen; ältere werden mit ihren Bildern entfernt.</summary>
    public const int Keep = 20;

    static void Prune(string root, int keep)
    {
        foreach (var r in List(root).Skip(keep))
            try { var dir = Path.Combine(root, r.Id); if (File.Exists(Path.Combine(dir, "lauf.json"))) Directory.Delete(dir, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Gespeicherte Läufe, neueste zuerst; beschädigte werden übersprungen. Bildpfade zeigen auf die Kopien im Laufordner.</summary>
    public static List<Run> List(string root)
    {
        var list = new List<Run>();
        if (!Directory.Exists(root)) return list;
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var f = Path.Combine(dir, "lauf.json"); if (!File.Exists(f)) continue;
            try
            {
                var o = JsonNode.Parse(File.ReadAllText(f))!.AsObject();
                list.Add(new((string?)o["id"] ?? Path.GetFileName(dir), DateTime.Parse((string?)o["at"] ?? "", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    (int?)o["seed"] ?? 0, (string?)o["engine"] ?? "",
                    [.. (o["inputs"] as JsonArray ?? []).Select(x => (string?)x ?? "")],
                    (o["checks"] as JsonObject ?? []).ToDictionary(kv => kv.Key, kv => (bool?)kv.Value ?? false),
                    (o["values"] as JsonObject ?? []).ToDictionary(kv => kv.Key, kv => (double?)kv.Value ?? double.NaN),
                    (o["shots"] as JsonObject ?? []).ToDictionary(kv => kv.Key, kv => Path.Combine(dir, Path.GetFileName((string?)kv.Value ?? "")))));
            }
            catch (Exception e) when (e is JsonException or IOException or FormatException or InvalidOperationException or NullReferenceException) { }
        }
        return [.. list.OrderByDescending(r => r.At).ThenByDescending(r => r.Id, StringComparer.Ordinal)];
    }

    /// <summary>Messwerte mit bekannter Richtung: true = größer ist besser, false = näher an 0 ist besser, dazu die kleinste Änderung, die zählt
    /// (Messrauschen bei Werten nahe 0). Andere Werte werden nur als „verändert“ gezeigt.</summary>
    static readonly Dictionary<string, (bool Up, double Min)> Direction = new(StringComparer.Ordinal)
    {
        ["WAGEN.Strecke"] = (true, 1.0), ["WAGEN.Tempo"] = (true, 2.0), ["BODEN.Boden"] = (false, 1.0),
    };

    static string Num(double v) => double.IsNaN(v) ? "–" : v.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>Vergleich „vorher“ gegen „jetzt“. Vergleichbar nur bei gleichem Startwert, gleicher Engine und gleichen Eingaben – sonst steht das in der Notiz,
    /// und die Unterschiede gelten nicht als Verschlechterung.</summary>
    public static Comparison Compare(Run before, Run now, double tolerance = 0.05)
    {
        var why = new List<string>();
        if (before.Seed != now.Seed) why.Add($"anderer Startwert ({before.Seed} → {now.Seed})");
        if (before.Engine != now.Engine) why.Add($"andere Engine ({before.Engine} → {now.Engine})");
        if (!before.Inputs.SequenceEqual(now.Inputs)) why.Add("andere Eingaben");
        var comparable = why.Count == 0;
        var ch = new List<Change>();
        foreach (var k in before.Checks.Keys.Union(now.Checks.Keys).Order(StringComparer.Ordinal))
        {
            var had = before.Checks.TryGetValue(k, out var a); var has = now.Checks.TryGetValue(k, out var b);
            if (had && !has) ch.Add(new(k, a ? "✓" : "⚠", "fehlt jetzt", comparable));
            else if (!had && has) ch.Add(new(k, "–", b ? "✓" : "⚠", comparable && !b));
            else if (a != b) ch.Add(new(k, a ? "✓" : "⚠", b ? "✓" : "⚠", comparable && a && !b));
        }
        foreach (var k in before.Values.Keys.Union(now.Values.Keys).Order(StringComparer.Ordinal))
        {
            var x = before.Values.GetValueOrDefault(k, double.NaN); var y = now.Values.GetValueOrDefault(k, double.NaN);
            if (double.IsNaN(x) && double.IsNaN(y)) continue;
            if (double.IsNaN(x) != double.IsNaN(y)) { ch.Add(new(k, Num(x), Num(y), comparable && double.IsNaN(y) && Direction.ContainsKey(k))); continue; }
            var known = Direction.TryGetValue(k, out var dir);
            if (Math.Abs(x - y) <= Math.Max(known ? dir.Min : 1e-9, Math.Abs(x) * tolerance)) continue;
            var worse = comparable && known && (dir.Up ? y < x : Math.Abs(y) > Math.Abs(x));
            ch.Add(new(k, Num(x), Num(y), worse));
        }
        return new(comparable, comparable ? "gleicher Startwert und gleiche Eingaben" : "nicht vergleichbar: " + string.Join(", ", why), ch);
    }
}
