using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Trendo.Core;

public enum Capability { Ready, NotConnected, Blocked, SpecOnly, Rejected, InProgress }

/// <summary>Eine Regel für „fertig“: gebaut UND mit Beleg getestet (alte PASS-Werte zählen weiter).</summary>
public static class StatusRules
{
    public static bool Built(string impl) => impl is "IMPLEMENTED" or "TESTED" or "ACCEPTED" or "RELEASED";
    public static bool Tested(string test) => test is "PASS" or "TESTED" or "TESTED_BOX" or "PC_VERIFIED";
    public static bool Done(string impl, string test) => Built(impl) && Tested(test);
    public static bool Started(string impl) => impl is "IN_PROGRESS" or "PC_VERIFIED_PARTIAL" || Built(impl);
}

public sealed record StudioEntry(string Id, string Name, string ApprovalStatus, string ImplementationStatus,
    string TestStatus, Capability Capability, string Reason);

/// <summary>Liest requirements.yaml und zeigt den ehrlichen Status. Nichts wird als bereit angezeigt, was nicht implementiert UND geprüft ist.</summary>
public sealed class StudioRegistry
{
    private sealed class Doc { public List<Dictionary<string, object>> Modules { get; set; } = []; }

    /// <summary>Zuordnung Modul → benötigtes externes Programm (für BLOCKED-Erkennung).</summary>
    public static readonly IReadOnlyDictionary<string, string> RequiredTools = new Dictionary<string, string>
    {
        ["CORE-05"] = "blender", ["CORE-06"] = "unreal", ["SYS-10"] = "libreoffice", ["SYS-03"] = "browser",
    };

    public IReadOnlyList<StudioEntry> Load(string requirementsYamlPath, IToolProbe probe)
    {
        var de = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
        var doc = de.Deserialize<Doc>(File.ReadAllText(requirementsYamlPath));
        return doc.Modules.Select(m =>
        {
            string S(string k) => m.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";
            var id = S("id"); var appr = S("approval_status"); var impl = S("implementation_status"); var test = S("test_status");
            Capability cap; string why;
            if (appr.StartsWith("REJECTED")) { cap = Capability.Rejected; why = "Erweiterung abgelehnt"; }
            else if (RequiredTools.TryGetValue(id, out var tool) && !probe.IsAvailable(tool))
            { cap = Capability.Blocked; why = $"Benötigtes Programm nicht gefunden: {tool}"; }
            else if (StatusRules.Done(impl, test))
            { cap = Capability.Ready; why = test == "PC_VERIFIED" ? "Gebaut, getestet und auf dem PC geprüft" : "Gebaut und automatisch getestet"; }
            else if (StatusRules.Started(impl)) { cap = Capability.InProgress; why = "Teilweise gebaut – einige Funktionen fehlen noch"; }
            else if (RequiredTools.ContainsKey(id)) { cap = Capability.NotConnected; why = "Programm ist da, die Verbindung zu Trendo fehlt noch"; }
            else { cap = Capability.SpecOnly; why = "Geplant – noch nicht gebaut"; }
            return new StudioEntry(id, S("name"), appr, impl, test, cap, why);
        }).ToList();
    }

    /// <summary>Freundlicher Status für die Oberfläche – nur wo er hilft (null = nichts anzeigen, z. B. bei teilweise gebauten Studios).</summary>
    public static string? FriendlyLabel(Capability c) => c switch
    {
        Capability.Ready => "Bereit",
        Capability.Blocked => "Braucht ein Zusatzprogramm",
        Capability.InProgress => null,
        _ => "Noch nicht verfügbar",
    };

    public static string GermanLabel(Capability c) => c switch
    {
        Capability.Ready => "Bereit",
        Capability.NotConnected => "Nicht verbunden",
        Capability.Blocked => "Blockiert",
        Capability.Rejected => "Abgelehnt",
        Capability.InProgress => "In Arbeit",
        _ => "Noch nicht gebaut",
    };
}

public interface IToolProbe { bool IsAvailable(string tool); }

/// <summary>Prüft nur Präsenz typischer Programme; startet nichts.</summary>
public sealed class DefaultToolProbe : IToolProbe
{
    private static readonly DependencyManager _dm = new(new AuditLog(Path.Combine(Path.GetTempPath(), "trendo-probe-noop.jsonl")), new RealProcessRunner());
    public bool IsAvailable(string tool)
    {
        if (!OperatingSystem.IsWindows()) return tool == "browser" && OnPath("xdg-open");
        if (tool == "browser") return true; // Windows hat immer einen Standardbrowser (Edge)
        var c = DependencyCatalog.All.FirstOrDefault(x => x.Tools?.Contains(tool) == true);
        return c is not null && _dm.IsInstalled(c);
    }
    private static bool OnPath(string exe) => (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator).Any(d => File.Exists(Path.Combine(d, exe)));
}
