using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>T-0688: Entwürfe des Code-Editors. Ungespeicherter Text jeder offenen Datei wird kurz nach dem Tippen in den Datenordner von Trendo
/// geschrieben (atomar, <see cref="AtomicFile"/>) – nie in die Quelldatei selbst. Nach einem Absturz bietet der Editor diese Entwürfe zum
/// Wiederherstellen an. Beim Speichern oder Verwerfen wird der Entwurf gelöscht.</summary>
public static class CodeDrafts
{
    /// <param name="DiskSha">Prüfsumme der Datei auf der Platte, als der Entwurf entstand – so ist erkennbar, ob die Datei inzwischen anders geändert wurde.</param>
    public sealed record Draft(string Root, string File, string Text, string DiskSha, DateTime SavedUtc);

    static string Dir(string dataRoot) => Path.Combine(dataRoot, "code-editor", "entwuerfe");
    static string Key(string file) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(file).ToLowerInvariant())))[..24];
    static string PathOf(string dataRoot, string file) => Path.Combine(Dir(dataRoot), Key(file) + ".json");

    public static string ShaOf(string file)
    {
        try { using var s = System.IO.File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(s)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    public static void Save(string dataRoot, string root, string file, string text, string diskSha, DateTime? nowUtc = null) =>
        AtomicFile.WriteAllText(PathOf(dataRoot, file), JsonSerializer.Serialize(new Draft(Path.GetFullPath(root), Path.GetFullPath(file), text, diskSha, nowUtc ?? DateTime.UtcNow)));

    public static void Delete(string dataRoot, string file)
    {
        try { System.IO.File.Delete(PathOf(dataRoot, file)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static Draft? Load(string dataRoot, string file)
    {
        var p = PathOf(dataRoot, file);
        try { return System.IO.File.Exists(p) ? JsonSerializer.Deserialize<Draft>(System.IO.File.ReadAllText(p)) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>Alle Entwürfe, deren Text sich wirklich von der Datei unterscheidet (neueste zuerst). Kaputte oder überholte Entwürfe werden aufgeräumt.</summary>
    public static List<Draft> All(string dataRoot)
    {
        var list = new List<Draft>();
        if (!Directory.Exists(Dir(dataRoot))) return list;
        foreach (var f in Directory.GetFiles(Dir(dataRoot), "*.json"))
        {
            Draft? d = null;
            try { d = JsonSerializer.Deserialize<Draft>(System.IO.File.ReadAllText(f)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
            string? disk = null;
            try { if (d is not null && System.IO.File.Exists(d.File)) disk = CodeProject.ReadText(d.File); } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { }
            if (d is null || disk is not null && disk.Replace("\r\n", "\n") == d.Text.Replace("\r\n", "\n")) { try { System.IO.File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } continue; }
            list.Add(d);
        }
        return list.OrderByDescending(d => d.SavedUtc).ToList();
    }
}
