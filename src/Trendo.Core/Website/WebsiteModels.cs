using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trendo.Core;

/// <summary>CORE-07: Beschreibung einer Webseite, bevor Dateien entstehen.</summary>
public sealed record WebsiteBrief(
    string Prompt,
    string Name,
    string? Kind = null,
    string? Technique = null,
    string? StyleId = null,
    WebsiteStyle? Style = null,
    bool Vite = false,
    string? ModelPath = null,
    string? ModelMotion = null,
    string? MediaPath = null,
    bool PixelStreaming = false,
    bool ExampleCube = false);

/// <summary>Optionen für den Bau. Die lokale KI ist freiwillig. Unreal-Liste null heißt: auf diesem PC nachsehen.</summary>
public sealed record WebsiteBuildContext(
    string? AiAnswer = null,
    string? Blender = null,
    IReadOnlyList<GameStudio.Engine>? Engines = null);

public sealed record WebsiteBuildResult(
    string Directory,
    WebsiteContent Content,
    WebsiteQualityReport Quality,
    string Message);

public sealed record WebsiteStyle(
    string Id,
    string Label,
    string Font,
    string Background,
    string Surface,
    string Text,
    string Muted,
    string Accent,
    string AccentText,
    int Radius)
{
    public static WebsiteStyle Klar { get; } = new("klar", "Klar", "\"Segoe UI\", \"Helvetica Neue\", Arial, sans-serif", "#f5f5f7", "#ffffff", "#1d1d1f", "#3a3a3c", "#0b4f9c", "#ffffff", 20);
    public static WebsiteStyle Verspielt { get; } = new("verspielt", "Verspielt", "\"Trebuchet MS\", \"Segoe UI\", sans-serif", "#fff6ee", "#ffffff", "#241c18", "#3f342c", "#5b21b6", "#ffffff", 28);
    public static WebsiteStyle Luxus { get; } = new("luxus", "Luxuriös", "\"Palatino Linotype\", Palatino, Georgia, serif", "#14110e", "#1e1a16", "#f6f1e7", "#e4d8c4", "#e6d3a1", "#1a140c", 2);
    public static WebsiteStyle Technik { get; } = new("technik", "Technik", "\"Segoe UI\", Roboto, Arial, sans-serif", "#0b1220", "#121a2b", "#e7eefc", "#c9d6ee", "#7dd3fc", "#082033", 10);

    public static IReadOnlyList<WebsiteStyle> Presets { get; } = [Klar, Verspielt, Luxus, Technik];

    public static WebsiteStyle? Find(string? id) => Presets.FirstOrDefault(p => p.Id.Equals(id ?? "", StringComparison.OrdinalIgnoreCase));
}

public sealed class WebsiteSection
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public List<WebsiteItem> Items { get; set; } = [];
}

public sealed class WebsiteItem
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Price { get; set; }
    public string? Href { get; set; }
}

/// <summary>Gespeicherter Inhalt (inhalt.json). Daraus werden HTML und CSS neu geschrieben.</summary>
public sealed class WebsiteContent
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Kind { get; set; } = "landing";
    public string Technique { get; set; } = "mikro";
    public string TechniqueReason { get; set; } = "";
    public bool Vite { get; set; }
    public bool CopyFromAi { get; set; }
    public string Headline { get; set; } = "";
    public string Subline { get; set; } = "";
    public string Description { get; set; } = "";
    public List<WebsiteSection> Sections { get; set; } = [];
    public WebsiteStyle Style { get; set; } = WebsiteStyle.Klar;
    public string? ModelFile { get; set; }
    public string ModelMotion { get; set; } = "drehen";
    public string ModelNote { get; set; } = "";
    public bool ExampleModel { get; set; }
    public string? VideoFile { get; set; }
    public string? PosterFile { get; set; }
    public List<string> Frames { get; set; } = [];
    public string MediaNote { get; set; } = "";
    public bool PixelStreaming { get; set; }
    public string UnrealNote { get; set; } = "";
}

public static class WebsiteKinds
{
    public static readonly (string Id, string Label)[] All =
    [
        ("landing", "Landingpage"),
        ("shop", "Shop"),
        ("portfolio", "Portfolio"),
        ("blog", "Blog"),
        ("firma", "Firmenwebseite"),
    ];

    public static bool Known(string? id) => All.Any(x => x.Id == id);
    public static string Label(string id) => All.FirstOrDefault(x => x.Id == id).Label ?? id;
}

public static class WebsiteTechniques
{
    public static readonly (string Id, string Label, string Help)[] All =
    [
        ("geschichte", "Story-Scrolling", "Die Seite führt beim Scrollen durch Kapitel."),
        ("parallax", "Parallax", "Ebenen verschieben sich unterschiedlich schnell."),
        ("drei", "3D im Browser", "Ein Objekt mit Three.js, drehbar oder beim Scrollen."),
        ("partikel", "Partikel und Shader", "Bewegter Hintergrund aus Punkten und einer Farbfläche."),
        ("video", "Video-Hintergrund", "Ein Video oder eine Bildfolge liegt hinter dem Text."),
        ("mikro", "Mikro-Animationen", "Feine Bewegungen an Karten und Schaltflächen."),
    ];

    public static bool Known(string? id) => All.Any(x => x.Id == id);
    public static string Label(string id) => All.FirstOrDefault(x => x.Id == id).Label ?? id;
}

public static class WebsiteJson
{
    public static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Write<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, Opt) + "\n");

    public static T Read<T>(string path) where T : class
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opt)
            ?? throw new InvalidDataException(Path.GetFileName(path) + " ist leer oder ungültig.");
    }
}
