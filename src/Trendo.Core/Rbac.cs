namespace Trendo.Core;

/// <summary>Rollen laut Spezifikation (Benutzer-/Projektisolation, rollenbasierte Erlaubnisse).</summary>
public enum Role { Guest = 0, User = 1, Owner = 2 }

public enum Permission
{
    ViewStudios, ViewProjects, ManageProjects, RequestAction, ApproveAction, ViewAudit,
    /// <summary>T-0764: Programme installieren, aktualisieren und entfernen (alle Wege: Zusatzprogramme, winget, Portable, Offline-Installer,
    /// Vollpaket, GitHub, Quellcode-Programme, KI-Laufzeiten, Apps). Nur Erwachsene (Rolle Besitzer) – ein Kinderprofil nie.</summary>
    ManageSoftware,
}

public sealed record Principal(string Name, Role Role)
{
    /// <summary>Der am Desktop angemeldete Windows-Benutzer, der die App selbst bedient.</summary>
    public static Principal LocalOwner { get; } = new("lokaler-besitzer", Role.Owner);

    /// <summary>B4 (T-0706): Kinderprofil – darf Wirkungen anfragen, aber nicht freigeben (Rolle Benutzer).</summary>
    public static Principal ChildProfile { get; } = new("kinderprofil", Role.User);

    /// <summary>B4 (T-0706): Rolle der angemeldeten Person aus ihrem Profil. Erwachsene = Besitzer, Kinderprofil = Benutzer.
    /// Der Kern (<see cref="Rbac"/>, <see cref="ApprovalGate"/>) lehnt Freigaben eines Kinderprofils ab.</summary>
    public static Principal ForProfile(bool child) => child ? ChildProfile : LocalOwner;
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

    /// <summary>T-0764: Hinweis, wenn ein Kinderprofil Programme installieren oder entfernen möchte.</summary>
    public const string NurErwachseneSoftware = "Programme installieren und entfernen geht nur in einem Erwachsenen-Profil. Es wurde nichts geändert.";

    /// <summary>T-0764: Prüfung an jedem Installations- und Entfernen-Weg im Kern. Ohne Recht: Protokolleintrag „rbac.denied“ mit dem Vorgang
    /// und <see cref="UnauthorizedAccessException"/> mit einem verständlichen Satz – es wird nichts geändert.</summary>
    public static void DemandSoftware(Principal p, string what, AuditLog? audit = null, string? message = null)
    {
        if (Has(p, Permission.ManageSoftware)) return;
        audit?.Append(p.Name, "rbac.denied", $"{p.Role} fehlt {Permission.ManageSoftware}: {what}");
        throw new UnauthorizedAccessException(message ?? NurErwachseneSoftware);
    }

    /// <summary>T-0764 (2. Prüfrunde N-D): Hinweis, wenn ein Kinderprofil ein Programm oder einen Installer starten möchte.</summary>
    public const string NurErwachseneStart = "Programme starten und einrichten darf nur ein Erwachsenen-Profil. Dokumente, Bilder, Musik und Videos kannst du weiter öffnen.";
}
