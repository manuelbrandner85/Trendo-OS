using System.Security.Cryptography;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Eine Datei, die ein Studio im gemeinsamen Projekt an ein anderes weitergibt.</summary>
public sealed record HandoffItem(string Id, string From, string To, string File, string Kind, string Note, string Sha256, DateTimeOffset At, DateTimeOffset? Received = null);

/// <summary>Übergaben zwischen Studios. Sie liegen im Projektordner neben den Dateien, gelten nur für dieses Projekt und werden nicht zwischen Projekten geteilt.</summary>
public sealed class ProjectHandoff(string projectFolder)
{
    public const string FileName = "uebergaben.json";
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    string Store => Path.Combine(projectFolder, ".trendo", FileName);

    public IReadOnlyList<HandoffItem> All()
    {
        if (!File.Exists(Store)) return [];
        try { return JsonSerializer.Deserialize<List<HandoffItem>>(File.ReadAllText(Store)) ?? []; }
        catch (JsonException) { return []; }
    }

    public IReadOnlyList<HandoffItem> OpenFor(string? studio) => All().Where(h => h.Received is null && (studio is null || h.To.Equals(studio, StringComparison.OrdinalIgnoreCase))).ToList();

    /// <summary>Datei übergeben. Sie muss im Projektordner liegen und existieren.</summary>
    public HandoffItem Add(string from, string to, string relativeFile, string kind, string note = "")
    {
        var full = Resolve(relativeFile);
        if (!File.Exists(full)) throw new ArgumentException("Diese Datei gibt es im Projekt nicht: " + relativeFile);
        using var stream = File.OpenRead(full);
        var item = new HandoffItem(Guid.NewGuid().ToString("N")[..8], from, to, Path.GetRelativePath(projectFolder, full).Replace('\\', '/'), kind, note, Convert.ToHexString(SHA256.HashData(stream)), DateTimeOffset.Now);
        Save([.. All(), item]);
        return item;
    }

    /// <summary>Annehmen. Wurde die Datei seit der Übergabe verändert, gilt die Übergabe nicht mehr.</summary>
    public string Take(string id, string studio)
    {
        var items = All().ToList();
        var i = items.FindIndex(h => h.Id == id);
        if (i < 0) throw new ArgumentException("Diese Übergabe gibt es nicht.");
        var h = items[i];
        if (!h.To.Equals(studio, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Diese Übergabe ist für {h.To} bestimmt, nicht für {studio}.");
        if (h.Received is not null) throw new ArgumentException("Diese Übergabe wurde schon angenommen.");
        var full = Resolve(h.File);
        if (!File.Exists(full)) throw new ArgumentException("Die übergebene Datei fehlt inzwischen.");
        using (var stream = File.OpenRead(full))
            if (Convert.ToHexString(SHA256.HashData(stream)) != h.Sha256) throw new ArgumentException("Die Datei wurde nach der Übergabe verändert. Bitte neu übergeben.");
        items[i] = h with { Received = DateTimeOffset.Now };
        Save(items);
        return full;
    }

    string Resolve(string relative)
    {
        var root = Path.GetFullPath(projectFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : throw new ArgumentException("Übergaben gibt es nur innerhalb des Projekts.");
    }

    void Save(List<HandoffItem> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Store)!);
        File.WriteAllText(Store, JsonSerializer.Serialize(items, Options));
    }
}
