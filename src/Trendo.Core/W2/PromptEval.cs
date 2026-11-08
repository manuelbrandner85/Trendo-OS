using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record PromptCheck(string Kind, string Value, string Label);
public sealed record PromptCheckResult(PromptCheck Check, bool Ok);
public sealed record PromptRun(string Answer, bool Answered, IReadOnlyList<PromptCheckResult> Checks) { public bool Ok => Answered && Checks.All(c => c.Ok); }
public sealed record PromptVariantResult(string Label, IReadOnlyList<PromptRun> Runs) { public int Passed => Runs.Count(r => r.Ok); }

/// <summary>Alle Studios, Runde 11 – CORE-12: KI-Anweisungen messbar vergleichen. Prüfregeln in Alltagssprache, jede Fassung läuft mehrmals gegen dieselbe KI,
/// gezählt wird, wie oft alle Regeln erfüllt sind. Eigene Umsetzung der Idee von promptfoo (MIT), ohne Node und ohne Zusatzpaket.</summary>
public static partial class PromptEval
{
    public const int MaxRuns = 5;
    private static readonly TimeSpan RegexLimit = TimeSpan.FromMilliseconds(250);

    [GeneratedRegex(@"^(höchstens|mindestens)\s+(\d{1,6})\s+w(ö|oe)rter?$", RegexOptions.IgnoreCase)] private static partial Regex Words();

    /// <summary>Regeln, eine pro Zeile: „enthält: Preis“, „nicht: leider“, „Muster: \d+ Euro“, „höchstens 50 Wörter“, „mindestens 3 Wörter“, „ist JSON“.</summary>
    public static (IReadOnlyList<PromptCheck> Checks, IReadOnlyList<string> Errors) Parse(string text)
    {
        var checks = new List<PromptCheck>(); var errors = new List<string>();
        foreach (var raw in (text ?? "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var words = Words().Match(line);
            if (words.Success) { checks.Add(new(words.Groups[1].Value.ToLowerInvariant() == "höchstens" ? "max" : "min", words.Groups[2].Value, line)); continue; }
            if (line.Equals("ist JSON", StringComparison.OrdinalIgnoreCase)) { checks.Add(new("json", "", line)); continue; }
            var colon = line.IndexOf(':');
            var key = colon > 0 ? line[..colon].Trim().ToLowerInvariant() : "";
            var value = colon > 0 ? line[(colon + 1)..].Trim() : "";
            if (value.Length == 0 || key is not ("enthält" or "enthaelt" or "nicht" or "muster")) { errors.Add($"Nicht verstanden: „{line}“"); continue; }
            if (key == "muster")
            {
                try { _ = new Regex(value, RegexOptions.None, RegexLimit); }
                catch (ArgumentException) { errors.Add($"Muster ist ungültig: „{value}“"); continue; }
                checks.Add(new("regex", value, line));
            }
            else checks.Add(new(key == "nicht" ? "not" : "has", value, line));
        }
        return (checks, errors);
    }

    public static IReadOnlyList<PromptCheckResult> Evaluate(string answer, IReadOnlyList<PromptCheck> checks) =>
        checks.Select(c => new PromptCheckResult(c, Holds(answer, c))).ToList();

    private static bool Holds(string answer, PromptCheck c)
    {
        switch (c.Kind)
        {
            case "has": return answer.Contains(c.Value, StringComparison.CurrentCultureIgnoreCase);
            case "not": return !answer.Contains(c.Value, StringComparison.CurrentCultureIgnoreCase);
            case "max": return CountWords(answer) <= int.Parse(c.Value);
            case "min": return CountWords(answer) >= int.Parse(c.Value);
            case "json":
                try { using var _ = JsonDocument.Parse(answer.Trim()); return true; } catch (JsonException) { return false; }
            case "regex":
                try { return Regex.IsMatch(answer, c.Value, RegexOptions.None, RegexLimit); } catch (RegexMatchTimeoutException) { return false; }
            default: return false;
        }
    }

    private static int CountWords(string s) => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Führt jede Fassung <paramref name="runs"/>-mal aus (1 bis <see cref="MaxRuns"/>). Bleibt die KI die Antwort schuldig, zählt der Lauf als nicht bestanden.</summary>
    public static async Task<IReadOnlyList<PromptVariantResult>> RunAsync(IReadOnlyList<(string Label, string Prompt)> variants, IReadOnlyList<PromptCheck> checks, int runs,
        Func<string, CancellationToken, Task<(bool Ok, string Text)>> ask, CancellationToken ct = default)
    {
        runs = Math.Clamp(runs, 1, MaxRuns);
        var results = new List<PromptVariantResult>();
        foreach (var (label, prompt) in variants)
        {
            var list = new List<PromptRun>();
            for (var i = 0; i < runs; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (ok, text) = await ask(prompt, ct);
                list.Add(ok ? new PromptRun(text, true, Evaluate(text, checks)) : new PromptRun(text, false, []));
            }
            results.Add(new PromptVariantResult(label, list));
        }
        return results;
    }
}

public sealed partial class PromptLibrary
{
    private string ChecksFile => Path.Combine(Path.GetDirectoryName(_file)!, "pruefregeln.json");

    private Dictionary<string, string> LoadChecks()
    {
        try { return File.Exists(ChecksFile) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ChecksFile)) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    /// <summary>Gespeicherte Prüfregeln einer Vorlage (Text, eine Regel pro Zeile).</summary>
    public string ChecksFor(string id) => LoadChecks().GetValueOrDefault(id, "");

    public void SaveChecks(string id, string text)
    {
        var all = LoadChecks(); all[id] = text ?? "";
        File.WriteAllText(ChecksFile, JsonSerializer.Serialize(all));
    }
}
