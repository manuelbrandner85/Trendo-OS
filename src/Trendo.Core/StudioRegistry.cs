using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Trendo.Core;

public enum Capability { Ready, NotConnected, Blocked, SpecOnly, Rejected }

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
            else if (impl is "IMPLEMENTED" or "TESTED" or "ACCEPTED" or "RELEASED" && test == "PASS")
            { cap = Capability.Ready; why = "Implementiert und getestet"; }
            else if (RequiredTools.ContainsKey(id)) { cap = Capability.NotConnected; why = "Programm vorhanden, Adapter noch nicht gebaut"; }
            else { cap = Capability.SpecOnly; why = "Nur genehmigte Spezifikation – noch nicht gebaut"; }
            return new StudioEntry(id, S("name"), appr, impl, test, cap, why);
        }).ToList();
    }

    public static string GermanLabel(Capability c) => c switch
    {
        Capability.Ready => "Bereit",
        Capability.NotConnected => "Nicht verbunden",
        Capability.Blocked => "Blockiert",
        Capability.Rejected => "Abgelehnt",
        _ => "Noch nicht gebaut",
    };
}

public interface IToolProbe { bool IsAvailable(string tool); }

/// <summary>Prüft nur Präsenz typischer Programme; startet nichts.</summary>
public sealed class DefaultToolProbe : IToolProbe
{
    public bool IsAvailable(string tool)
    {
        if (!OperatingSystem.IsWindows()) return tool == "browser" && OnPath("xdg-open");
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return tool switch
        {
            "blender" => Directory.Exists(Path.Combine(pf, "Blender Foundation")),
            "unreal" => Directory.Exists(Path.Combine(pf, "Epic Games")) && Directory.EnumerateDirectories(Path.Combine(pf, "Epic Games"), "UE_*").Any(),
            "libreoffice" => Directory.Exists(Path.Combine(pf, "LibreOffice")),
            "browser" => true, // Windows hat immer einen Standardbrowser (Edge)
            _ => false,
        };
    }
    private static bool OnPath(string exe) => (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator).Any(d => File.Exists(Path.Combine(d, exe)));
}
