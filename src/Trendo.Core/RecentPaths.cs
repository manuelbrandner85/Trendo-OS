using System.Text.Json;

namespace Trendo.Core;

/// <summary>Zuletzt benutzte Dateien, getrennt nach Studio. Liegt lokal neben den übrigen Daten.</summary>
public sealed class RecentPaths
{
    private readonly string _file;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public RecentPaths(string file) => _file = file;

    public IReadOnlyList<string> List(string studio, int max = 8)
    {
        var all = Load();
        return all.TryGetValue(Key(studio), out var list) ? list.Take(max).ToList() : [];
    }

    public void Remember(string studio, string path)
    {
        path = path.Trim().Trim('"');
        if (path.Length == 0) return;
        var all = Load();
        var key = Key(studio);
        var list = all.TryGetValue(key, out var prev) ? prev.ToList() : [];
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > 8) list.RemoveRange(8, list.Count - 8);
        all[key] = list;
        var dir = Path.GetDirectoryName(_file);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_file, JsonSerializer.Serialize(all, Json));
    }

    private Dictionary<string, List<string>> Load()
    {
        try
        {
            if (!File.Exists(_file)) return new(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(_file)) ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { return new(StringComparer.OrdinalIgnoreCase); }
        catch (IOException) { return new(StringComparer.OrdinalIgnoreCase); }
    }

    private static string Key(string studio) => string.IsNullOrWhiteSpace(studio) ? "allgemein" : studio.Trim();
}
