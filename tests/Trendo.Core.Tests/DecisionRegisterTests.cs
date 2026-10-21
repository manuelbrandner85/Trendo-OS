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
        for (var n = 13; n <= 40; n++) Assert.True(d.ContainsKey($"IMP-{n}"), $"IMP-{n} fehlt");
        Assert.All(d.Values, v => Assert.Contains(v, new[] { "APPROVED", "REJECTED", "UNDECIDED" }));
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
