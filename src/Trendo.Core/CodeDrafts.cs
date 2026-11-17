using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trendo.Core.Programs;

namespace Trendo.Core;

/// <summary>T-0688: Entwürfe des Code-Editors. Ungespeicherter Text jeder offenen Datei wird kurz nach dem Tippen in den Datenordner von Trendo
/// geschrieben (atomar, <see cref="AtomicFile"/>) – nie in die Quelldatei selbst. Nach einem Absturz bietet der Editor diese Entwürfe zum
/// Wiederherstellen an. Beim Speichern oder Verwerfen wird der Entwurf gelöscht.
/// T-0726: Sperre gegen ein zweites Fenster wie bei der Programm-Hülle (<see cref="ProgramLocks"/>): Wer einen Entwurf schreibt, sperrt ihn für sein Fenster.
/// Ein anderes, noch laufendes Fenster überschreibt oder löscht ihn nicht und bietet ihn nicht als „nach einem Absturz“ an; die Sperre eines abgestürzten
/// Prozesses gilt als verwaist – dann wird der Entwurf wie bisher angeboten. Ohne <c>locks</c> verhält sich alles wie vorher.</summary>
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

    /// <summary>Entwurf schreiben. Mit <paramref name="locks"/>: nur wenn kein anderes laufendes Fenster ihn hält – dann liefert es dessen Halter und schreibt nichts.</summary>
    public static ProgramLocks.Holder? Save(string dataRoot, string root, string file, string text, string diskSha, DateTime? nowUtc = null, ProgramLocks? locks = null)
    {
        var p = PathOf(dataRoot, file);
        if (locks?.Acquire(p) is { } other) return other;
        AtomicFile.WriteAllText(p, JsonSerializer.Serialize(new Draft(Path.GetFullPath(root), Path.GetFullPath(file), text, diskSha, nowUtc ?? DateTime.UtcNow)));
        return null;
    }

    /// <summary>Entwurf löschen (gespeichert, verworfen, nicht mehr geändert). Mit <paramref name="locks"/>: der Entwurf eines anderen laufenden Fensters bleibt.</summary>
    public static void Delete(string dataRoot, string file, ProgramLocks? locks = null)
    {
        var p = PathOf(dataRoot, file);
        if (locks?.HeldByOther(p) is not null) return;
        try { locks?.Release(p); System.IO.File.Delete(p); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static Draft? Load(string dataRoot, string file)
    {
        var p = PathOf(dataRoot, file);
        try { return System.IO.File.Exists(p) ? JsonSerializer.Deserialize<Draft>(System.IO.File.ReadAllText(p)) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>Alle Entwürfe, deren Text sich wirklich von der Datei unterscheidet (neueste zuerst). Kaputte oder überholte Entwürfe werden aufgeräumt.
    /// Mit <paramref name="locks"/>: Entwürfe, die ein anderes noch laufendes Fenster hält, werden weder angeboten noch angefasst.</summary>
    public static List<Draft> All(string dataRoot, ProgramLocks? locks = null)
    {
        var list = new List<Draft>();
        if (!Directory.Exists(Dir(dataRoot))) return list;
        foreach (var f in Directory.GetFiles(Dir(dataRoot), "*.json"))
        {
            if (locks?.HeldByOther(f) is not null) continue;
            Draft? d = null;
            string raw;
            // T-0730: Ein vorübergehender Lesefehler (Datei gerade gesperrt, wird geschrieben, Virenscanner) ist KEIN kaputter Entwurf – er bleibt liegen
            // und wird beim nächsten Mal wieder geprüft. Gelöscht wird nur, was sich wirklich nicht als Entwurf lesen lässt.
            try { raw = System.IO.File.ReadAllText(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            try { d = JsonSerializer.Deserialize<Draft>(raw); } catch (JsonException) { }
            if (d is not null && (d.File is null || d.Text is null)) d = null; // Pflichtfelder fehlen → unbrauchbar
            string? disk = null;
            try { if (d is not null && System.IO.File.Exists(d.File)) disk = CodeProject.ReadText(d.File); } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { }
            if (d is null || disk is not null && disk.Replace("\r\n", "\n") == d.Text.Replace("\r\n", "\n")) { try { System.IO.File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } continue; }
            list.Add(d);
        }
        return list.OrderByDescending(d => d.SavedUtc).ToList();
    }
}
