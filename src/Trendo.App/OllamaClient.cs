using System.Net.Http.Json;
using System.Text.Json;

namespace Trendo.App;

/// <summary>Spricht nur mit der lokalen Ollama-Laufzeit auf 127.0.0.1 – keine Cloud.</summary>
public sealed class OllamaClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:11434"), Timeout = TimeSpan.FromSeconds(120) };

    public async Task<bool> IsUpAsync()
    {
        try { using var cts = new CancellationTokenSource(1000); using var r = await _http.GetAsync("/api/version", cts.Token); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> HasModelAsync(string model)
    {
        try
        {
            using var cts = new CancellationTokenSource(1500);
            var doc = await _http.GetFromJsonAsync<JsonElement>("/api/tags", cts.Token);
            return doc.GetProperty("models").EnumerateArray().Any(m => m.GetProperty("name").GetString() == model);
        }
        catch { return false; }
    }

    /// <summary>X-09: Modelle, die gerade im Speicher liegen (/api/ps).</summary>
    public async Task<List<(string Name, long Bytes)>> LoadedAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(1500);
            var doc = await _http.GetFromJsonAsync<JsonElement>("/api/ps", cts.Token);
            return doc.GetProperty("models").EnumerateArray().Select(m => (m.GetProperty("name").GetString() ?? "", m.TryGetProperty("size", out var s) ? s.GetInt64() : 0)).ToList();
        }
        catch { return []; }
    }

    /// <summary>X-09: Modell sofort aus dem Speicher entladen (keep_alive 0). Es bleibt installiert und lädt bei der nächsten Frage neu.</summary>
    public async Task<bool> UnloadAsync(string model)
    {
        try { using var r = await _http.PostAsJsonAsync("/api/generate", new { model, keep_alive = 0 }); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<string> ChatAsync(string model, string question)
    {
        try
        {
            var body = new { model, stream = false, messages = new object[] {
                new { role = "system", content = "Du bist Trendo KI, ein hilfreicher Assistent in Trendo OS. Antworte kurz und auf Deutsch." },
                new { role = "user", content = question } } };
            using var r = await _http.PostAsJsonAsync("/api/chat", body);
            r.EnsureSuccessStatusCode();
            var doc = await r.Content.ReadFromJsonAsync<JsonElement>();
            return doc.GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "(leere Antwort)";
        }
        catch (Exception ex) { return $"Fehler bei der lokalen KI: {ex.Message}"; }
    }
}
