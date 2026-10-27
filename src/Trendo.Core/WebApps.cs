using System.Text.Json;

namespace Trendo.Core;

/// <summary>Wunsch 27.10.2026: WhatsApp, Telegram, TikTok, Instagram und Facebook wie Apps in Trendo – installieren, öffnen, wieder entfernen.
/// Jede App ist die offizielle Webversion im eigenen App-Fenster (Edge/Chrome/Brave mit „--app“) und mit eigenem Anmeldespeicher je Person.</summary>
public sealed record WebApp(string Id, string Name, string Url, string What, string Emoji, string C1, string C2, string? Hint = null);

public static class WebApps
{
    public static readonly WebApp[] Catalog =
    [
        new("whatsapp", "WhatsApp", "https://web.whatsapp.com/", "Chats und Anrufe – mit dem Handy per QR-Code koppeln", "💬", "#5BE584", "#128C4A"),
        new("telegram", "Telegram", "https://web.telegram.org/a/", "Chats, Gruppen und Kanäle – Anmeldung mit Telefonnummer oder QR-Code", "✈️", "#7FD3FF", "#1C8ADB",
            "Es gibt auch das offizielle Telegram-Programm für Windows (unter „Hinzufügen“ → Apps)."),
        new("tiktok", "TikTok", "https://www.tiktok.com/", "Kurzvideos ansehen und folgen", "🎵", "#FF5C8A", "#25F4EE", "Hochladen geht im Web nur eingeschränkt."),
        new("instagram", "Instagram", "https://www.instagram.com/", "Fotos, Reels und Nachrichten", "📸", "#FFB35C", "#C13584", "Hochladen geht im Web nur eingeschränkt."),
        new("facebook", "Facebook", "https://www.facebook.com/", "Freunde, Gruppen und Messenger", "👥", "#8FB8FF", "#1877F2"),
    ];

    public static WebApp? Get(string id) => Catalog.FirstOrDefault(a => a.Id == id);

    static string Dir(string dataRoot) => Path.Combine(dataRoot, "apps");
    static string ListFile(string dataRoot) => Path.Combine(Dir(dataRoot), "installiert.json");

    /// <summary>Eigener Anmeldespeicher der App (bleibt angemeldet, getrennt vom normalen Browser und von anderen Personen).</summary>
    public static string ProfileDir(string dataRoot, string id) => Path.Combine(Dir(dataRoot), id);

    public static IReadOnlyList<string> Installed(string dataRoot)
    {
        try { return File.Exists(ListFile(dataRoot)) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ListFile(dataRoot))) ?? [] : []; }
        catch (Exception e) when (e is IOException or JsonException) { return []; }
    }

    public static bool IsInstalled(string dataRoot, string id) => Installed(dataRoot).Contains(id);

    static void Save(string dataRoot, IEnumerable<string> ids)
    {
        Directory.CreateDirectory(Dir(dataRoot));
        File.WriteAllText(ListFile(dataRoot), JsonSerializer.Serialize(ids.Distinct().ToList()));
    }

    public static void Install(string dataRoot, string id, AuditLog audit, string actor)
    {
        if (Get(id) is null) throw new ArgumentException("Unbekannte App: " + id);
        Save(dataRoot, Installed(dataRoot).Append(id));
        Directory.CreateDirectory(ProfileDir(dataRoot, id));
        audit.Append(actor, "app.install", id);
    }

    /// <summary>Entfernen: aus der Liste nehmen und den eigenen Anmeldespeicher löschen (abgemeldet, keine Reste).</summary>
    public static void Uninstall(string dataRoot, string id, AuditLog audit, string actor)
    {
        Save(dataRoot, Installed(dataRoot).Where(x => x != id));
        var dir = ProfileDir(dataRoot, id);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { audit.Append(actor, "app.uninstall.rest", id + ": " + e.Message); }
        audit.Append(actor, "app.uninstall", id);
    }

    /// <summary>Browser für App-Fenster: nur Chromium-Browser können „--app“ (Edge ist bei Windows dabei).</summary>
    public static BrowserInfo? Browser(IReadOnlyList<BrowserInfo> found) =>
        found.FirstOrDefault(b => b.Id == "edge") ?? found.FirstOrDefault(b => b.Id == "chrome") ?? found.FirstOrDefault(b => b.Id == "brave");

    /// <summary>Startargumente: eigenes Fenster ohne Adressleiste, eigener Speicher, keine Willkommensseiten.
    /// Benachrichtigungen erlaubt die Website selbst (einmalige Frage im App-Fenster).</summary>
    public static List<string> LaunchArgs(string dataRoot, WebApp app) =>
    [
        "--app=" + app.Url,
        "--user-data-dir=" + ProfileDir(dataRoot, app.Id),
        "--no-first-run",
        "--no-default-browser-check",
        "--window-size=1200,860",
    ];

    // Telegram als Windows-Programm: offizielles Setup von telegram.org (installiert ohne Admin ins Benutzerprofil)
    public const string TelegramSetupUrl = "https://telegram.org/dl/desktop/win64";
    public static readonly string[] TelegramHosts = ["telegram.org", "updates.tdesktop.com"];
    public static string TelegramExe(Func<string, string>? expand = null) =>
        (expand ?? Environment.ExpandEnvironmentVariables)(@"%APPDATA%\Telegram Desktop\Telegram.exe");
}
