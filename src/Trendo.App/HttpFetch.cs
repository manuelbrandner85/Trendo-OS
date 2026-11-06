using Trendo.Core;

namespace Trendo.App;

/// <summary>Netzabruf für den Kern über HttpClient (der Kern selbst bleibt ohne Netz-Abhängigkeit, QA-019).</summary>
public sealed class HttpFetch(HttpClient http) : Trendo.Core.Bibliothek.IWebFetchAuth
{
    /// <summary>Abruf mit Kopfzeilen (z. B. eigener Schlüssel des Nutzers) – die Kopfzeilen gehen nur an diese eine Anfrage.</summary>
    public async Task<WebReply> GetAsync(string url, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
        using var r = await http.SendAsync(req, ct);
        return new((int)r.StatusCode, await r.Content.ReadAsByteArrayAsync(ct));
    }

    public async Task<WebReply> GetAsync(string url, CancellationToken ct = default)
    {
        using var r = await http.GetAsync(url, ct);
        return new((int)r.StatusCode, await r.Content.ReadAsByteArrayAsync(ct));
    }
}
