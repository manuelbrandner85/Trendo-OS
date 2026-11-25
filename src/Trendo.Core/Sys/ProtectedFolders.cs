using System.Collections.Concurrent;

namespace Trendo.Core.Sys;

/// <summary>T-0764 (10. Prüfrunde): Trendos eigene Ordner, in die ein Kinderprofil NIE schreiben darf – kein Anlegen, Kopieren, Verschieben, Umbenennen,
/// Löschen, Entpacken oder Speichern. Das sind der Datenordner (Einstellungen, Profile, Protokolle, Vertrauensliste, Zwischenspeicher, Werkzeuge,
/// Programme), der Werkzeugordner, das Vollpaket, der Installationsordner und Trendos mpv-Ordner im Temp-Verzeichnis. Ausnahmen (z. B. „projekte“ im
/// Datenordner) werden ausdrücklich freigegeben; der längste passende Eintrag entscheidet. Geprüft wird der Pfad wie angegeben UND aufgelöst
/// (Junctions, symbolische Verknüpfungen, subst – <c>GetFinalPathNameByHandle</c>), auch für noch nicht vorhandene Ziele (nächster vorhandener Ordner).
/// Lesen bleibt erlaubt. Erwachsene: unverändert.</summary>
public static class ProtectedFolders
{
    static readonly ConcurrentDictionary<string, bool> Entries = new(StringComparer.OrdinalIgnoreCase); // Pfad → geschützt (true) / freigegeben (false)

    static string Norm(string p) => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(p));
    static IEnumerable<string> Forms(string p)
    {
        var full = Norm(p);
        yield return full;
        if (OperatingSystem.IsWindows())
        {
            string? fin = null;
            try { fin = Norm(new Trendo.Core.WindowsDiskResolver().FinalPath(full)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
            if (fin is not null && !string.Equals(fin, full, StringComparison.OrdinalIgnoreCase)) yield return fin;
        }
    }

    /// <summary>Ordner schützen (alle aufgelösten Formen).</summary>
    public static void Protect(string? dir) { if (!string.IsNullOrWhiteSpace(dir)) foreach (var f in Forms(dir)) Entries[f] = true; }
    /// <summary>Ausnahme innerhalb eines geschützten Ordners (z. B. eigene Projekte).</summary>
    public static void Allow(string? dir) { if (!string.IsNullOrWhiteSpace(dir)) foreach (var f in Forms(dir)) Entries.TryAdd(f, false); }

    static bool Under(string path, string root) => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>Liegt <paramref name="path"/> (oder sein aufgelöstes Ziel) in einem geschützten Trendo-Ordner?</summary>
    public static bool IsProtected(string path)
    {
        IEnumerable<string> forms;
        try { forms = Forms(path).ToList(); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return true; } // unklarer Pfad: fail-closed
        foreach (var f in forms)
        {
            if (TrendoTemp(f)) return true;
            var hit = Entries.Where(e => Under(f, e.Key)).OrderByDescending(e => e.Key.Length).FirstOrDefault();
            if (hit.Key is not null && hit.Value) return true;
        }
        return false;
    }

    /// <summary>Trendos mpv-Ordner im Temp-Verzeichnis (trendo-mpv-&lt;PID&gt;): Tastendateien und leerer Arbeitsordner.</summary>
    static bool TrendoTemp(string full)
    {
        var temp = Norm(System.IO.Path.GetTempPath());
        if (!Under(full, temp) || full.Length <= temp.Length) return false;
        var first = full[(temp.Length + 1)..].Split(System.IO.Path.DirectorySeparatorChar)[0];
        return first.StartsWith("trendo-mpv-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Schreiben im Kinderprofil verweigern, wenn das Ziel geschützt ist („rbac.denied“ und Hinweis); Erwachsene: wie bisher.</summary>
    public static void DemandWrite(Principal by, string path, AuditLog audit)
    {
        if (Rbac.Has(by, Permission.ManageSoftware)) return;
        if (IsProtected(path)) Rbac.DemandSoftware(by, "schreiben " + System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path)), audit, Rbac.NurErwachseneTrendoOrdner);
    }

    /// <summary>Fehlermeldung für einen Schreibversuch als <paramref name="by"/> oder null (erlaubt) – für Rückruf-Prüfungen (z. B. Dateimanager).</summary>
    public static string? WriteProblem(Principal by, string path, AuditLog audit)
    {
        try { DemandWrite(by, path, audit); return null; }
        catch (UnauthorizedAccessException e) { return e.Message; }
    }
}
