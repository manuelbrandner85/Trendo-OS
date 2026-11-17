using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Trendo.Core;

namespace Trendo.App;

// Liegt in der App, nicht im Kern: der Kern bleibt ohne Netzwerkzugriff (QA-019). Adresse, Ordnerliste und Meldungen kommen aus WebPublishing (Kern).
/// <summary>T-0718: Website auf den eigenen Server hochladen – über WebDAV (Standard bei NAS im Heimnetz, Nextcloud und vielen Webhostern),
/// ohne neues Konto bei einem Dritten. Ablauf: Ordner anlegen (MKCOL), jede Datei hochladen (PUT), danach jede Datei zurücklesen (GET) und mit dem
/// freigegebenen Inhalt vergleichen. Benutzername und Passwort liegen nur im geschützten Speicher der Person (<see cref="ISecretStore"/>), nie im Klartext.</summary>
public sealed class WebDavLink(ISecretStore secrets, HttpMessageHandler? handler = null)
{
    static string Key(Uri u, string part) => "webdav." + u.GetLeftPart(UriPartial.Authority).ToLowerInvariant() + "." + part;

    /// <summary>Gemerkte Anmeldung für diesen Server (nur Host und Port zählen).</summary>
    public (string? User, string? Password) Saved(Uri server) => (secrets.Get(Key(server, "benutzer")), secrets.Get(Key(server, "passwort")));
    public void Remember(Uri server, string user, string password) { secrets.Set(Key(server, "benutzer"), user); secrets.Set(Key(server, "passwort"), password); }
    public void Forget(Uri server) { secrets.Delete(Key(server, "benutzer")); secrets.Delete(Key(server, "passwort")); }

    /// <summary>Fehler mit Antwort des Servers.</summary>
    public sealed class ServerException(int status) : Exception($"Server-Fehler {status}") { public int Status { get; } = status; }

    /// <summary>Lädt genau diese Dateien hoch und liest sie zurück. Liefert, wie viele Dateien nach dem Hochladen gleich zurückkamen.</summary>
    public async Task<int> UploadAsync(Uri baseUrl, string user, string password, IReadOnlyList<(string Rel, byte[] Bytes)> files, CancellationToken ct = default)
    {
        using var http = handler is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(60) } : new HttpClient(handler, false);
        if (user.Length > 0) http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TrendoOS");
        foreach (var dir in WebPublishing.ServerFolders(files.Select(f => f.Rel)))
        {
            using var r = await http.SendAsync(new HttpRequestMessage(new HttpMethod("MKCOL"), WebPublishing.ServerUrl(baseUrl, dir)), ct);
            // 201 angelegt, 405 gibt es schon – beides gut
            if (!r.IsSuccessStatusCode && r.StatusCode != HttpStatusCode.MethodNotAllowed) throw new ServerException((int)r.StatusCode);
        }
        foreach (var (rel, bytes) in files)
        {
            var body = new ByteArrayContent(bytes); body.Headers.ContentType = MediaTypeHeaderValue.Parse(WebsiteHostMime(rel));
            using var r = await http.PutAsync(WebPublishing.ServerUrl(baseUrl, rel), body, ct);
            if (!r.IsSuccessStatusCode) throw new ServerException((int)r.StatusCode);
        }
        var same = 0;
        foreach (var (rel, bytes) in files)
        {
            using var r = await http.GetAsync(WebPublishing.ServerUrl(baseUrl, rel), ct);
            if (!r.IsSuccessStatusCode) continue;
            var back = await r.Content.ReadAsByteArrayAsync(ct);
            if (SHA256.HashData(back).AsSpan().SequenceEqual(SHA256.HashData(bytes))) same++;
        }
        return same;
    }

    static string WebsiteHostMime(string rel) => Path.GetExtension(rel).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8", ".css" => "text/css; charset=utf-8", ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".json" => "application/json", ".svg" => "image/svg+xml", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp",
        ".xml" => "application/xml", ".txt" => "text/plain; charset=utf-8", _ => "application/octet-stream",
    };
}
