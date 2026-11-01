using Trendo.Core;

namespace Trendo.App;

/// <summary>Netzabruf für den Kern über HttpClient (der Kern selbst bleibt ohne Netz-Abhängigkeit, QA-019).</summary>
public sealed class HttpFetch(HttpClient http) : IWebFetch
{
    public async Task<WebReply> GetAsync(string url, CancellationToken ct = default)
    {
        using var r = await http.GetAsync(url, ct);
        return new((int)r.StatusCode, await r.Content.ReadAsByteArrayAsync(ct));
    }
}
