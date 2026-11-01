using Trendo.Core;

namespace Trendo.Core.Tests;

/// <summary>Testhilfe: HttpClient (mit Attrappen-Handler) als <see cref="IWebFetch"/> – der Kern selbst kennt kein HttpClient (QA-019).</summary>
public sealed class TestFetch(HttpClient http) : IWebFetch
{
    public async Task<WebReply> GetAsync(string url, CancellationToken ct = default)
    {
        using var r = await http.GetAsync(url, ct);
        return new((int)r.StatusCode, await r.Content.ReadAsByteArrayAsync(ct));
    }
}
