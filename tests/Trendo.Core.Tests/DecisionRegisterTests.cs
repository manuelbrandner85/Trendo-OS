using Xunit;

namespace Trendo.Core.Tests;

/// Masterprompt MP-1.0: abgelehnte/unentschiedene Punkte dürfen nie genehmigt werden.
public class DecisionRegisterTests
{
    // Absichtlich hart kodiert – Änderung nur mit ausdrücklicher Entscheidung von Manuel.
    private static readonly Dictionary<string, string> Locked = new()
    {
        ["IMP-23"] = "REJECTED", ["IMP-25"] = "REJECTED", ["IMP-34"] = "REJECTED",
        ["SPEC-07"] = "REJECTED", ["IMP-35"] = "UNDECIDED", ["LANG-2.0"] = "REJECTED",
        // Freigabe-Delta 2026-10-23 (docs/claude/TRENDO_GODMODE_ENTSCHEIDUNGEN_2026-10-23.md)
        ["IMP-86"] = "REJECTED", ["IMP-97"] = "UNDECIDED",
    };

    private static Dictionary<string, string> Load(string file)
    {
        var res = new Dictionary<string, string>(); string? id = null;
        foreach (var raw in File.ReadAllLines(file))
        {
            var l = raw.Trim();
            if (l.StartsWith("- id:")) id = l[5..].Trim();
            else if (id != null && l.StartsWith("decision:")) res[id] = l[9..].Trim();
            else if (id != null && l.StartsWith("approval_status:")) res[id] = l[16..].Trim();
        }
        return res;
    }

    [Fact(DisplayName = "Entscheidungsregister: REJECTED/UNDECIDED bleiben gesperrt")]
    public void Locked_items_never_approved()
    {
        var d = Load(Path.Combine(AppContext.BaseDirectory, "decisions.yaml"));
        foreach (var (k, v) in Locked) { Assert.True(d.ContainsKey(k), k + " fehlt"); Assert.Equal(v, d[k]); }
        for (var n = 13; n <= 113; n++) Assert.True(d.ContainsKey($"IMP-{n}"), $"IMP-{n} fehlt");
        Assert.All(d.Values, v => Assert.Contains(v, new[] { "APPROVED", "REJECTED", "UNDECIDED" }));
    }

    [Fact(DisplayName = "Register: Verbesserungen 13–113 in requirements.yaml, Abgelehntes/Offenes nie begonnen")]
    public void Improvements_follow_decisions()
    {
        var dec = Load(Path.Combine(AppContext.BaseDirectory, "decisions.yaml"));
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "requirements.yaml"));
        for (var n = 13; n <= 113; n++)
        {
            var i = Array.FindIndex(lines, l => l.Trim() == $"- id: IMP-{n}");
            Assert.True(i >= 0, $"IMP-{n} fehlt in requirements.yaml");
            var block = string.Join("\n", lines.Skip(i + 1).TakeWhile(l => !l.TrimStart().StartsWith("- id:")));
            Assert.Contains($"decision: {dec[$"IMP-{n}"]}", block);
            if (dec[$"IMP-{n}"] != "APPROVED") Assert.Contains("implementation_status: NOT_STARTED", block);
        }
    }

    [Fact(DisplayName = "Entscheidungsregister: SPEC-07 in requirements.yaml nicht gebaut")]
    public void Spec07_not_built()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "requirements.yaml"));
        var i = Array.FindIndex(lines, l => l.Trim() == "- id: SPEC-07");
        Assert.True(i >= 0);
        var block = string.Join("\n", lines.Skip(i + 1).TakeWhile(l => !l.TrimStart().StartsWith("- id:")));
        Assert.Contains("implementation_status: NOT_STARTED", block);
    }
}
