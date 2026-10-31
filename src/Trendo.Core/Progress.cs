using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Trendo.Core;

/// <summary>T-0245 (V111): Beleg eines Status – Pfad im Projekt, Prüfsumme und optional der geprüfte Stand.</summary>
public sealed record EvidenceInfo(string? Path, string? Sha256, string? Commit);

/// <summary>Wie belastbar ein Beleg ist: geprüft (Datei da, Prüfsumme stimmt), erfasst (Prüfsumme bekannt, Datei nicht auf diesem PC),
/// nur Pfad (ohne Prüfsumme), verändert (Prüfsumme stimmt nicht) oder keiner.</summary>
public enum EvidenceState { None, PathOnly, Recorded, Verified, Invalid }

public static class Evidence
{
    public static EvidenceState Check(EvidenceInfo? e, string? repoRoot)
    {
        if (e?.Path is not { Length: > 0 } rel) return EvidenceState.None;
        var file = repoRoot is null ? null : System.IO.Path.Combine(repoRoot, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var exists = file is not null && File.Exists(file);
        if (e.Sha256 is not { Length: 64 } sha) return exists || repoRoot is null ? EvidenceState.PathOnly : EvidenceState.Invalid;
        if (!exists) return EvidenceState.Recorded;
        using var s = File.OpenRead(file!);
        // Zeilenenden wie in Git (LF) vergleichen, damit ein Auschecken unter Windows (CRLF) keinen Fehlalarm auslöst
        var bytes = new MemoryStream(); s.CopyTo(bytes); var raw = bytes.ToArray();
        var lf = raw.Length > 0 && !raw.Contains((byte)0) ? System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(raw).Replace("\r\n", "\n")) : raw;
        var h = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(lf));
        return string.Equals(h, sha, StringComparison.OrdinalIgnoreCase) ? EvidenceState.Verified : EvidenceState.Invalid;
    }

    public static string Label(EvidenceState s) => s switch
    {
        EvidenceState.Verified => "Beleg geprüft",
        EvidenceState.Recorded => "Beleg erfasst (nicht auf diesem PC)",
        EvidenceState.PathOnly => "Beleg ohne Prüfsumme",
        EvidenceState.Invalid => "Beleg fehlt oder verändert",
        _ => "kein Beleg",
    };

    /// <summary>Projektordner (mit Trendo.sln) ab einem Startordner – null im installierten Trendo.</summary>
    public static string? RepoRoot(string start)
    {
        for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            if (File.Exists(System.IO.Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        return null;
    }
}

public sealed record ProgressItem(string Id, string Name, string Impl, string Test, EvidenceInfo? Evidence = null)
{
    /// <summary>„Getestet“ zählt als belegt nur mit geprüftem oder erfasstem Beleg.</summary>
    public bool TestedWithEvidence(string? repoRoot) => StatusRules.Tested(Test) && Trendo.Core.Evidence.Check(Evidence, repoRoot) is EvidenceState.Verified or EvidenceState.Recorded;
}
public sealed record ProgressModule(string Wave, string WaveName, int Order, ProgressItem Module, IReadOnlyList<ProgressItem> Subs)
{
    public bool Done => StatusRules.Done(Module.Impl, Module.Test);
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
            .ToDictionary(m => S(m, "id"), m => new ProgressItem(S(m, "id"), S(m, "name") is { Length: > 0 } n ? n : S(m, "title"), S(m, "implementation_status"), S(m, "test_status"),
                S(m, "evidence") is { Length: > 0 } ev ? new EvidenceInfo(ev, S(m, "evidence_sha256") is { Length: > 0 } sh ? sh : null, S(m, "evidence_commit") is { Length: > 0 } co ? co : null) : null));
        var plan = JsonSerializer.Deserialize<List<PlanEntry>>(File.ReadAllText(planJson)) ?? [];
        // Welle 0 (Setup) steht nicht in requirements.yaml: Status aus docs/wave0.yaml (über buildplan.json)
        foreach (var p in plan.Where(p => p.local && !all.ContainsKey(p.id))) all[p.id] = new ProgressItem(p.id, p.name, p.impl, p.test);
        return plan.Where(p => all.ContainsKey(p.id))
            .Select(p => new ProgressModule(p.wave, p.waveName, p.order, all[p.id], p.subs.Where(s => all.ContainsKey(s.id)).Select(s => all[s.id]).ToList()))
            .ToList();
    }

    public static string GermanStatus(ProgressItem i) => (i.Impl, i.Test) switch
    {
        (_, "PC_VERIFIED") when StatusRules.Built(i.Impl) => "Fertig & auf dem PC geprüft",
        (_, _) when StatusRules.Done(i.Impl, i.Test) => "Fertig & getestet",
        ("IMPLEMENTED", _) => "Gebaut, Test offen",
        ("IN_PROGRESS", _) => "In Arbeit",
        ("PLANNED", _) => "Geplant",
        (_, "FAIL") => "Test fehlgeschlagen",
        _ => "Noch nicht gebaut",
    };
}
