namespace Trendo.Core;

/// <summary>Rollen laut Spezifikation (Benutzer-/Projektisolation, rollenbasierte Erlaubnisse).</summary>
public enum Role { Guest = 0, User = 1, Owner = 2 }

public enum Permission { ViewStudios, ViewProjects, ManageProjects, RequestAction, ApproveAction, ViewAudit }

public sealed record Principal(string Name, Role Role)
{
    /// <summary>Der am Desktop angemeldete Windows-Benutzer, der die App selbst bedient.</summary>
    public static Principal LocalOwner { get; } = new("lokaler-besitzer", Role.Owner);
}

public static class Rbac
{
    private static readonly Dictionary<Role, HashSet<Permission>> Map = new()
    {
        [Role.Guest] = [Permission.ViewStudios],
        [Role.User] = [Permission.ViewStudios, Permission.ViewProjects, Permission.ManageProjects, Permission.RequestAction],
        [Role.Owner] = [.. Enum.GetValues<Permission>()],
    };

    public static bool Has(Principal p, Permission perm) => Map[p.Role].Contains(perm);

    public static void Demand(Principal p, Permission perm, AuditLog? audit = null)
    {
        if (Has(p, perm)) return;
        audit?.Append(p.Name, "rbac.denied", $"{p.Role} fehlt {perm}");
        throw new UnauthorizedAccessException($"Rolle {p.Role} hat keine Berechtigung {perm}.");
    }
}
