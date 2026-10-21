using System.Net.Http.Json;
using System.Text.Json;
using Trendo.Core;

namespace Trendo.App;

public static class LocalOnly
{
    /// <summary>Wirft, wenn die Adresse nicht auf diesem Gerät liegt (CORE-02: keine Cloud-Exfiltration).</summary>
    public static Uri Require(string url)
    {
        var u = new Uri(url);
        if (!(u.IsLoopback || u.Host is "localhost" or "127.0.0.1" or "[::1]"))
            throw new InvalidOperationException($"Nur Quellen auf diesem Gerät erlaubt, nicht {u.Host}.");
        return u;
    }
}

public sealed class OllamaBackend(string baseUrl = "http://127.0.0.1:11434", HttpMessageHandler? handler = null) : IKiBackend
{
    private readonly HttpClient _http = new(handler ?? new HttpClientHandler()) { BaseAddress = LocalOnly.Require(baseUrl), Timeout = TimeSpan.FromMinutes(30) };
    public string Name => "Ollama";
    public string BaseUrl => baseUrl;

    public async Task<bool> IsUpAsync(CancellationToken ct = default)
    {
        try { using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(1200); using var r = await _http.GetAsync("/api/version", cts.Token); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(2000);
            var doc = await _http.GetFromJsonAsync<JsonElement>("/api/tags", cts.Token);
            return doc.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("name").GetString() ?? "").Where(n => n != "").ToList();
        }
        catch { return []; }
    }

    /// <summary>X-05: freies Modell über Ollama laden (wartet bis fertig).</summary>
    public async Task<bool> PullAsync(string model, CancellationToken ct = default)
    {
        try { using var r = await _http.PostAsJsonAsync("/api/pull", new { model, stream = false }, ct); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
    {
        var body = new { model, stream = false, messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray() };
        using var r = await _http.PostAsJsonAsync("/api/chat", body, ct);
        r.EnsureSuccessStatusCode();
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
    }
}

/// <summary>LM Studio, llama.cpp-Server, Jan, KoboldCpp … sprechen das OpenAI-Format unter /v1 – hier nur lokal.</summary>
public sealed class OpenAiCompatBackend(string name, string baseUrl, HttpMessageHandler? handler = null) : IKiBackend
{
    private readonly HttpClient _http = new(handler ?? new HttpClientHandler()) { BaseAddress = LocalOnly.Require(baseUrl), Timeout = TimeSpan.FromSeconds(180) };
    public string Name => name;
    public string BaseUrl => baseUrl;
    public async Task<bool> IsUpAsync(CancellationToken ct = default) => (await ModelsAsync(ct)).Count > 0;

    public async Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(1200);
            var doc = await _http.GetFromJsonAsync<JsonElement>("/v1/models", cts.Token);
            return doc.GetProperty("data").EnumerateArray().Select(m => m.GetProperty("id").GetString() ?? "").Where(n => n != "").ToList();
        }
        catch { return []; }
    }

    public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
    {
        var body = new { model, stream = false, messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray() };
        using var r = await _http.PostAsJsonAsync("/v1/chat/completions", body, ct);
        r.EnsureSuccessStatusCode();
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
    }
}

