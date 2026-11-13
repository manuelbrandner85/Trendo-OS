namespace Trendo.Core.Programs;

/// <summary>T-0640 E2: Das Spiele-Team als Hierarchie statt flacher Reihe – Leitung (Kreativ, Technik, Produktion), darunter Abteilungen mit je einer Leitung,
/// darunter die Fachleute. Aufgaben laufen von oben nach unten, Streit zwischen zwei Rollen entscheidet ihre nächste gemeinsame Leitung,
/// zwischen den drei Direktoren der Game Director. Nutzt die vorhandenen Fachrollen aus <see cref="GameExperts"/>, kein neuer KI-Kern.</summary>
public static class GameTeam
{
    /// <summary>Kennung des Game Directors aus <see cref="GameDirector.Team"/>: er steht über der Leitung.</summary>
    public const string Head = "director";
    public const string HeadName = "Game Director";

    public sealed record Member(string Id, string Name, int Level, string Lead, string Department);

    /// <summary>Abteilung → (Abteilungsleitung, zuständige Direktion).</summary>
    static readonly Dictionary<string, (string Lead, string Director)> Departments = new()
    {
        ["Design"] = ("game-designer", "creative-director"),
        ["Grafik"] = ("art-director", "creative-director"),
        ["Ton"] = ("audio-director", "creative-director"),
        ["Erzählung"] = ("narrative-director", "creative-director"),
        ["Programmierung"] = ("lead-programmer", "technical-director"),
        ["Engine"] = ("lead-programmer", "technical-director"),
        ["Qualität"] = ("qa-lead", "producer"),
        ["Produktion"] = ("release-manager", "producer"),
    };

    static readonly string[] Directors = ["creative-director", "technical-director", "producer"];

    /// <summary>Alle Rollen mit Ebene (0 = Leitung, 1 = Abteilungsleitung, 2 = Fachleute) und direkter Leitung.</summary>
    public static IReadOnlyList<Member> Tree()
    {
        var leads = Departments.Values.Select(v => v.Lead).ToHashSet();
        var list = new List<Member>();
        foreach (var e in GameExperts.All)
        {
            if (Directors.Contains(e.Id)) { list.Add(new(e.Id, e.Name, 0, Head, "Leitung")); continue; }
            if (leads.Contains(e.Id))
            {
                var dep = Departments.First(kv => kv.Value.Lead == e.Id);
                list.Add(new(e.Id, e.Name, 1, dep.Value.Director, dep.Key == "Engine" ? "Programmierung" : dep.Key));
                continue;
            }
            var d = Departments.TryGetValue(e.Group, out var x) ? x : ("game-designer", "creative-director");
            list.Add(new(e.Id, e.Name, 2, d.Item1, e.Group));
        }
        return list;
    }

    public static Member? Find(string id) => Tree().FirstOrDefault(m => m.Id == id);

    /// <summary>Kette von der Rolle bis zum Game Director (die Rolle selbst zuerst).</summary>
    public static List<string> Chain(string id)
    {
        var all = Tree().ToDictionary(m => m.Id);
        var chain = new List<string>();
        for (var cur = id; cur != Head && all.TryGetValue(cur, out var m) && !chain.Contains(cur); cur = m.Lead) chain.Add(cur);
        chain.Add(Head);
        return chain;
    }

    /// <summary>Wer entscheidet, wenn zwei Rollen sich widersprechen: die nächste gemeinsame Leitung (nie eine der beiden Streitenden selbst, außer sie leitet die andere).</summary>
    public static string Escalate(string a, string b)
    {
        if (a == b) return Find(a)?.Lead ?? Head;
        var ca = Chain(a); var cb = Chain(b);
        return ca.FirstOrDefault(cb.Contains) ?? Head;
    }

    public static string NameOf(string id) => id == Head ? HeadName : Find(id)?.Name ?? id;

    /// <summary>Aufbau als kurzer Text für die KI-Anweisungen.</summary>
    public static string Describe()
    {
        var tree = Tree();
        var lines = new List<string> { $"Aufbau des Teams: {HeadName} → Leitung → Abteilungen → Fachleute. Aufgaben gehen von oben nach unten." };
        foreach (var dir in tree.Where(m => m.Level == 0))
        {
            var deps = tree.Where(m => m.Level == 1 && m.Lead == dir.Id).Select(l => $"{l.Department} ({l.Name})").Distinct();
            lines.Add($"- {dir.Name}: {string.Join(", ", deps)}");
        }
        lines.Add($"Widersprechen sich zwei Rollen, entscheidet ihre nächste gemeinsame Leitung; zwischen den Direktoren entscheidet der {HeadName}.");
        lines.Add("Bei einer Grundsatzentscheidung zwischen mehreren Wegen stelle sie als Frage mit 2 bis 4 Antworten; geändert wird erst, wenn gewählt ist.");
        return string.Join("\n", lines);
    }
}
