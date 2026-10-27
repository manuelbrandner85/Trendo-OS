using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Wunsch 27.10.2026: KI-Programme, die es nur auf GitHub oder beim Hersteller gibt, lädt und richtet Trendo selbst ein.
/// Portable: offizielle Windows-Datei von GitHub, geprüft und entpackt in den Werkzeugordner. Installer: offizieller Installer
/// (GitHub oder Herstellerseite) wird geladen und gestartet, danach erkennt Trendo das Programm.</summary>
public sealed record KiSourceSetup(string Id, string? Repo, string AssetPattern, bool Portable, string? VendorUrl, string Exe, string ServerArgs, string[] InstalledPaths)
{
    public Regex Pattern => new(AssetPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    public bool NeedsModel => ServerArgs.Contains("{model}", StringComparison.Ordinal);
    public string Host => VendorUrl is { } v ? new Uri(v).Host : "github.com";
}

public static class KiSourceSetups
{
    /// <summary>Herstellerseiten, von denen Trendo Installer laden darf (zusätzlich zu GitHub). Nur HTTPS.</summary>
    public static readonly string[] VendorHosts = ["lmstudio.ai", "installers.lmstudio.ai"];

    public static readonly IReadOnlyList<KiSourceSetup> All =
    [
        new("ollama", "ollama/ollama", @"^ollama-windows-amd64\.zip$", true, null, "ollama.exe", "serve", []),
        new("llamacpp", "ggml-org/llama.cpp", @"^llama-b\d+-bin-win-cpu-x64\.zip$", true, null, "llama-server.exe", "-m \"{model}\" --host 127.0.0.1 --port 8080", []),
        new("koboldcpp", "LostRuins/koboldcpp", @"^koboldcpp-nocuda\.exe$", true, null, "koboldcpp-nocuda.exe", "--model \"{model}\" --host 127.0.0.1 --port 5001 --skiplauncher", []),
        new("jan", "janhq/jan", @"^Jan_[\d.]+_x64-setup\.exe$", false, null, "Jan.exe", "", [@"Programs\Jan\Jan.exe", @"Programs\jan\Jan.exe"]),
        new("lmstudio", null, "", false, "https://lmstudio.ai/download/latest/win32/x64", "LM Studio.exe", "", [@"Programs\LM Studio\LM Studio.exe", @"Programs\lm-studio\LM Studio.exe"]),
    ];

    public static KiSourceSetup? For(string id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>Alle Versionen aus /releases (auch Vorabversionen, z. B. llama.cpp).</summary>
    public static List<GitHubApps.Release> ParseReleases(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().Select(e => GitHubApps.ParseRelease(e.GetRawText())).ToList() : [];
    }

    public static string ReleasesUrl(string repo) => $"https://api.github.com/repos/{repo}/releases?per_page=15";

    /// <summary>Neueste Version, die die passende Windows-Datei enthält; nur Dateien von github.com.</summary>
    public static (GitHubApps.Release Release, GitHubApps.Asset Asset)? Pick(KiSourceSetup s, IEnumerable<GitHubApps.Release> releases)
    {
        foreach (var r in releases)
            if (r.Assets.FirstOrDefault(a => s.Pattern.IsMatch(a.Name) && a.Size > 0 && a.Url.StartsWith("https://github.com/", StringComparison.Ordinal)) is { } a)
                return (r, a);
        return null;
    }

    /// <summary>Startbefehl des Servers; {model} wird durch das Sprachmodell (GGUF) ersetzt.</summary>
    public static string Args(KiSourceSetup s, string? model)
    {
        if (s.NeedsModel && string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("Dafür braucht es ein Sprachmodell (GGUF). Lade zuerst eines in der KI-Bibliothek.");
        if (model is not null && model.Contains('"')) throw new ArgumentException("Ungültiger Dateiname.");
        return s.ServerArgs.Replace("{model}", model ?? "");
    }

    /// <summary>Installiertes Programm eines Installers suchen (unter %LOCALAPPDATA%).</summary>
    public static string? FindInstalled(KiSourceSetup s, string localAppData) =>
        s.InstalledPaths.Select(p => Path.Combine(localAppData, p)).FirstOrDefault(File.Exists);

    /// <summary>Größtes Sprachmodell (GGUF) im KI-Ordner – das nimmt der Server, wenn keines gewählt ist.</summary>
    public static string? BestModel(string kiRoot) =>
        Directory.Exists(kiRoot) ? new DirectoryInfo(kiRoot).EnumerateFiles("*.gguf", SearchOption.AllDirectories).Where(f => !f.Name.Contains("mmproj", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Length).Select(f => f.FullName).FirstOrDefault() : null;
}
