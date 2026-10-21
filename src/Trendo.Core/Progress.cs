using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Trendo.Core;

public sealed record ProgressItem(string Id, string Name, string Impl, string Test);
public sealed record ProgressModule(string Wave, string WaveName, int Order, ProgressItem Module, IReadOnlyList<ProgressItem> Subs)
{
    public bool Done => Module.Impl is "IMPLEMENTED" or "TESTED" or "ACCEPTED" or "RELEASED" && Module.Test == "PASS";
}

/// <summary>Bau-Fortschritt: Reihenfolge aus docs/buildplan.json, Status IMMER live aus requirements.yaml.</summary>
public static class Progress
{
    private sealed class Doc
    {
        public List<Dictionary<string, object>> Modules { get; set; } = [];
        public List<Dictionary<string, object>> BusinessRequirements { get; set; } = [];
        public List<Dictionary<string, object>> LetterRequirements { get; set; } = [];
    }
    private sealed record PlanSub(string id);
    private sealed record PlanEntry(string wave, string waveName, int order, string id, string name, string impl, string test, bool local, List<PlanSub> subs);

    public static IReadOnlyList<ProgressModule> Load(string requirementsYaml, string planJson)
    {
        var doc = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<Doc>(File.ReadAllText(requirementsYaml));
        static string S(Dictionary<string, object> m, string k) => m.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";
        var all = doc.Modules.Concat(doc.BusinessRequirements).Concat(doc.LetterRequirements)
            .ToDictionary(m => S(m, "id"), m => new ProgressItem(S(m, "id"), S(m, "name") is { Length: > 0 } n ? n : S(m, "title"), S(m, "implementation_status"), S(m, "test_status")));
        var plan = JsonSerializer.Deserialize<List<PlanEntry>>(File.ReadAllText(planJson)) ?? [];
        // Welle 0 (Setup) steht nicht in requirements.yaml: Status aus docs/wave0.yaml (über buildplan.json)
        foreach (var p in plan.Where(p => p.local && !all.ContainsKey(p.id))) all[p.id] = new ProgressItem(p.id, p.name, p.impl, p.test);
        return plan.Where(p => all.ContainsKey(p.id))
            .Select(p => new ProgressModule(p.wave, p.waveName, p.order, all[p.id], p.subs.Where(s => all.ContainsKey(s.id)).Select(s => all[s.id]).ToList()))
            .ToList();
    }

    public static string GermanStatus(ProgressItem i) => (i.Impl, i.Test) switch
    {
        (_, "PASS") when i.Impl is "IMPLEMENTED" or "TESTED" or "ACCEPTED" or "RELEASED" => "Fertig & getestet",
        ("IMPLEMENTED", _) => "Gebaut, Test offen",
        ("IN_PROGRESS", _) => "In Arbeit",
        ("PLANNED", _) => "Geplant",
        (_, "FAIL") => "Test fehlgeschlagen",
        _ => "Noch nicht gebaut",
    };
}
