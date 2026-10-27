using System.Text.Json;

namespace Trendo.Core;

/// <summary>Persönliche Einstellungen aus der Ersteinrichtung (settings.json im Benutzerdatenordner).</summary>
public sealed class UserSettings
{
    /// <summary>SYS-13-P20: Dateiformat-Version; ältere Dateien werden beim Laden angehoben.</summary>
    public int FormatVersion { get; set; } = Sys.Quality.SettingsFormat;
    public string Name { get; set; } = "";
    /// <summary>B1: Profilbild – „emoji:🦊“ oder „bild“ (liegt als profil/bild.png im Datenordner).</summary>
    public string Avatar { get; set; } = "emoji:🙂";
    /// <summary>B3: gewählte Interessen (siehe <see cref="Interests"/>).</summary>
    public List<string> Interests { get; set; } = [];
    /// <summary>B7: Kinderprofil – einfache Bedienung, Installieren nur durch Erwachsene.</summary>
    public bool Child { get; set; }
    /// <summary>B5: Handy-Zugang beim Start automatisch einschalten (nur wenn ein Handy verbunden ist).</summary>
    public bool PhoneAutoStart { get; set; } = true;
    /// <summary>B8: Dokumente und Bilder aus Windows in der Trendo-Suche finden (nichts wird kopiert).</summary>
    public bool SearchWindowsFolders { get; set; }
    public string Language { get; set; } = "de-DE";
    public string ProjectsFolder { get; set; } = "";
    public string Theme { get; set; } = "dunkel";          // dunkel | hell | kontrast
    public string Accent { get; set; } = "#2F86F6";
    public string Wallpaper { get; set; } = "orbit";
    public bool WallpaperByTime { get; set; }
    /// <summary>Akzentfarbe folgt dem Hintergrund (Orbit, Abendrot und Bergsee → Blau, Morgen → Bernstein, Polarlicht → Türkis, Wellen → Violett).</summary>
    public bool AccentFromWallpaper { get; set; }
    public bool ReduceMotion { get; set; }
    /// <summary>T-0107 Leistungsprofil: auto (Raspberry Pi/ARM → sparsam), hoch (alle Effekte), sparsam (ohne Unschärfe, Schatten und Animationen).</summary>
    public string PowerProfile { get; set; } = "auto";
    public static readonly string[] PowerProfiles = ["auto", "hoch", "sparsam"];
    public bool IsLowPower(bool arm) => PowerProfile == "sparsam" || (PowerProfile is not "hoch" && arm);
    /// <summary>SYS-12: Mindestschriftgröße in pt (0 = wie gestaltet), Leichte Sprache, Farbblind-Symbole, leise Meldungen, einfacher Modus, Fensterplatz.</summary>
    public int FontMin { get; set; }
    public bool EasyLanguage { get; set; }
    public bool ColorBlind { get; set; }
    public string NotifyStyle { get; set; } = "normal";
    public bool SimpleMode { get; set; }
    public int[]? WindowRect { get; set; }
    /// <summary>Startmenü: angeheftete Studios (IDs) und zuletzt benutzte Seiten (neueste zuerst).</summary>
    public List<string> PinnedStudios { get; set; } = [];
    /// <summary>Wunsch 27.10.2026: Symbole auf dem Desktop (Studio-IDs oder „page:Seite“). Standard: leer – der Desktop ist aufgeräumt.</summary>
    public List<string> DesktopIcons { get; set; } = [];
    public List<string> RecentPages { get; set; } = [];
    /// <summary>T-0201: zuletzt gewählte Dateien je Seite (neueste zuerst).</summary>
    public Dictionary<string, List<string>> RecentFiles { get; set; } = [];
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
    /// <summary>SYS-12: Anzeigegröße (1,0 = normal; 1,15 = groß; 1,3 = sehr groß).</summary>
    /// <summary>STUDIO-LETTER-01: Absender wird für den nächsten Brief gemerkt.</summary>
    public string SenderStreet { get; set; } = "";
    public string SenderCity { get; set; } = "";
    public string SenderContact { get; set; } = "";
    /// <summary>Briefe F4: Bild der eigenen Unterschrift (PNG/JPEG), wird unter den Gruß gesetzt.</summary>
    public string LetterSignature { get; set; } = "";
    /// <summary>Geld F3: eigenes Logo (PNG/JPG/SVG) oben rechts auf Rechnungen.</summary>
    public string InvoiceLogo { get; set; } = "";
    /// <summary>Kleinunternehmer nach § 19 UStG (Rechnungen ohne MwSt, Shop-Preise ohne MwSt).</summary>
    public bool SmallBusiness { get; set; } = true;
    public double UiScale { get; set; } = 1.0;
    public static readonly double[] UiScales = [1.0, 1.15, 1.3];
    public static double ClampScale(double v) => UiScales.OrderBy(x => Math.Abs(x - v)).First();

    public static readonly string[] Themes = ["dunkel", "hell", "kontrast"];
    public static readonly string[] Wallpapers = ["orbit", "abendrot", "bergsee-nacht", "polarlicht", "wellen", "morgen"];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string PathIn(string dataRoot) => Path.Combine(dataRoot, "settings.json");

    public static UserSettings Load(string dataRoot)
    {
        try { if (File.Exists(PathIn(dataRoot)) && System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(PathIn(dataRoot))) is System.Text.Json.Nodes.JsonObject o) return Sys.Quality.MigrateSettings(o).Deserialize<UserSettings>() ?? new(); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { /* defekte Datei: Standardwerte, Datei bleibt zur Prüfung liegen */ }
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

    public static string AccentFor(string wallpaper) => wallpaper switch
    {
        "morgen" => "#F59E0B", "polarlicht" => "#22C3A6", "wellen" => "#8B5CF6", _ => "#2F86F6",
    };

    /// <summary>Farbe, die gerade gilt: passend zum Hintergrund oder selbst gewählt.</summary>
    public string EffectiveAccent(DateTime now) => AccentFromWallpaper ? AccentFor(CurrentWallpaper(now)) : Accent;

    public string CurrentWallpaper(DateTime now) => WallpaperByTime ? WallpaperFor(now) : (Wallpapers.Contains(Wallpaper) ? Wallpaper : "orbit");
}
