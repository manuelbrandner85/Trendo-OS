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
