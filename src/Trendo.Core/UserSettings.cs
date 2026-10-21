using System.Text.Json;

namespace Trendo.Core;

/// <summary>Persönliche Einstellungen aus der Ersteinrichtung (settings.json im Benutzerdatenordner).</summary>
public sealed class UserSettings
{
    public string Name { get; set; } = "";
    public string Language { get; set; } = "de-DE";
    public string ProjectsFolder { get; set; } = "";
    public string Theme { get; set; } = "dunkel";          // dunkel | hell | kontrast
    public string Accent { get; set; } = "#2F86F6";
    public string Wallpaper { get; set; } = "bergsee-nacht";
    public bool WallpaperByTime { get; set; }
    public bool ReduceMotion { get; set; }
    public List<string> StudioPacks { get; set; } = [];
    public List<string> HiddenTiles { get; set; } = [];
    public List<string> DockOrder { get; set; } = [];
    public List<string> TileOrder { get; set; } = [];
    public List<string> WidgetOrder { get; set; } = [];

    /// <summary>Sortiert nach gespeicherter Reihenfolge; Unbekanntes behält die Standardposition am Ende.</summary>
    public static List<T> Ordered<T>(IEnumerable<T> items, Func<T, string> id, IList<string> order) =>
        items.Select((x, i) => (x, i)).OrderBy(t => order.IndexOf(id(t.x)) is var k && k >= 0 ? k : order.Count + t.i).Select(t => t.x).ToList();

    /// <summary>Verschiebt ein Element um eine Position (−1 = nach vorn, +1 = nach hinten) und speichert die vollständige Reihenfolge.</summary>
    public static List<string> Move(IList<string> current, string item, int delta)
    {
        var l = current.ToList(); var i = l.IndexOf(item); var j = i + delta;
        if (i < 0 || j < 0 || j >= l.Count) return l;
        (l[i], l[j]) = (l[j], l[i]); return l;
    }
    public List<string> SeenHints { get; set; } = [];
    public bool AutoUpdate { get; set; } = true;
    public int BackupEveryHours { get; set; } = 24;
    public bool SetupDone { get; set; }

    public static readonly string[] Themes = ["dunkel", "hell", "kontrast"];
    public static readonly string[] Wallpapers = ["bergsee-nacht", "polarlicht", "wellen", "morgen"];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string PathIn(string dataRoot) => Path.Combine(dataRoot, "settings.json");

    public static UserSettings Load(string dataRoot)
    {
        try { if (File.Exists(PathIn(dataRoot))) return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(PathIn(dataRoot))) ?? new(); }
        catch (JsonException) { /* defekte Datei: Standardwerte, Datei bleibt zur Prüfung liegen */ }
        return new();
    }

    public void Save(string dataRoot)
    {
        if (!Themes.Contains(Theme)) throw new ArgumentException("unbekanntes Design");
        var tmp = PathIn(dataRoot) + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, PathIn(dataRoot), true);
    }

    public static string DefaultProjectsFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trendo OS");

    /// <summary>Tageszeit-Wechsel: Morgen 6–10, Tag 10–17, Abend 17–21, Nacht 21–6.</summary>
    public static string WallpaperFor(DateTime t) => t.Hour switch
    {
        >= 6 and < 10 => "morgen", >= 10 and < 17 => "wellen", >= 17 and < 21 => "bergsee-nacht", _ => "polarlicht",
    };

    public string CurrentWallpaper(DateTime now) => WallpaperByTime ? WallpaperFor(now) : (Wallpapers.Contains(Wallpaper) ? Wallpaper : "bergsee-nacht");
}
