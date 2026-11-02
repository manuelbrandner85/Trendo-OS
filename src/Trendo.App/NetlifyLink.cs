using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core;

namespace Trendo.App;

// Liegt in der App, nicht im Kern: der Kern bleibt ohne Netzwerkzugriff (QA-019).
/// <summary>Paket D: Webseiten auf Netlify veröffentlichen – über die offizielle Netlify-Schnittstelle (ZIP-Deploy), erst nach Ja.
/// Der persönliche Zugangsschlüssel liegt im geschützten Speicher (Windows-Datenschutz) und lässt sich jederzeit entfernen.</summary>
public sealed class NetlifyLink(ISecretStore secrets, HttpMessageHandler? handler = null)
{
    public const string SecretKey = "netlify.token";
    public const string TokenPage = "https://app.netlify.com/user/applications#personal-access-tokens";
    const string Api = "https://api.netlify.com/api/v1/";
    public bool Connected => !string.IsNullOrEmpty(secrets.Get(SecretKey));
    public void Connect(string token)
    {
        var t = token.Trim();
        if (t.Length < 20 || t.Any(char.IsWhiteSpace)) throw new ArgumentException("Das sieht nicht wie ein Netlify-Zugangsschlüssel aus.");
        secrets.Set(SecretKey, t);
    }
    public void Disconnect() => secrets.Delete(SecretKey);

    /// <summary>Gemerkte Netlify-Seite eines Projekts (im versteckten .trendo-Ordner).</summary>
    public static (string? SiteId, string? Url) SiteOf(string folder)
    {
        var f = Path.Combine(folder, ".trendo", "netlify.json");
        if (!File.Exists(f)) return (null, null);
        try { var j = JsonNode.Parse(File.ReadAllText(f)); return ((string?)j?["siteId"], (string?)j?["url"]); } catch (JsonException) { return (null, null); }
    }

    /// <summary>Ordner als ZIP (ohne .trendo, .git, node_modules) – genau das, was hochgeladen wird.</summary>
    public static byte[] Zip(string folder)
    {
        if (!File.Exists(Path.Combine(folder, "index.html"))) throw new InvalidOperationException("Im Ordner fehlt index.html – das ist keine Webseite zum Veröffentlichen.");
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(folder, f).Replace('\\', '/');
                if (rel.StartsWith(".trendo/") || rel.StartsWith(".git/") || rel.StartsWith("node_modules/") || rel == ".env") continue;
                z.CreateEntryFromFile(f, rel, CompressionLevel.Optimal);
            }
        return ms.ToArray();
    }

    /// <summary>Veröffentlichen: neue Seite anlegen (beim ersten Mal) und den Ordner hochladen. Liefert die öffentliche Adresse.</summary>
    public async Task<string> DeployAsync(string folder, string? siteName = null, CancellationToken ct = default)
    {
        var token = secrets.Get(SecretKey) ?? throw new InvalidOperationException("Erst mit Netlify verbinden.");
        var bytes = Zip(folder); // zuerst prüfen und packen – ohne index.html wird bei Netlify gar nichts angelegt
        using var http = handler is null ? new HttpClient() : new HttpClient(handler, false);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TrendoOS");
        var (site, _) = SiteOf(folder);
        if (site is null)
        {
            var body = new JsonObject(); if (!string.IsNullOrWhiteSpace(siteName)) body["name"] = GitHubLink.SafeRepoName(siteName);
            using var r = await http.PostAsync(Api + "sites", new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"), ct);
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException(Explain(r.StatusCode, "Seite anlegen"));
            site = (string?)JsonNode.Parse(await r.Content.ReadAsStringAsync(ct))?["id"] ?? throw new InvalidOperationException("Netlify hat keine Seiten-ID geliefert.");
        }
        var zip = new ByteArrayContent(bytes); zip.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var d = await http.PostAsync(Api + $"sites/{Uri.EscapeDataString(site)}/deploys", zip, ct);
        if (!d.IsSuccessStatusCode) throw new InvalidOperationException(Explain(d.StatusCode, "Hochladen"));
        var dj = JsonNode.Parse(await d.Content.ReadAsStringAsync(ct));
        var url = (string?)dj?["ssl_url"] ?? (string?)dj?["url"] ?? (string?)dj?["deploy_ssl_url"] ?? "";
        Directory.CreateDirectory(Path.Combine(folder, ".trendo"));
        File.WriteAllText(Path.Combine(folder, ".trendo", "netlify.json"), new JsonObject { ["siteId"] = site, ["url"] = url, ["at"] = DateTime.UtcNow.ToString("o") }.ToJsonString());
        return url;
    }

    static string Explain(System.Net.HttpStatusCode s, string what) => (int)s switch
    {
        401 or 403 => $"{what}: Netlify hat den Zugangsschlüssel abgelehnt – bitte neu verbinden.",
        422 => $"{what}: Der Name ist schon vergeben – wähle einen anderen.",
        _ => $"{what}: Netlify meldet Fehler {(int)s}.",
    };
}
