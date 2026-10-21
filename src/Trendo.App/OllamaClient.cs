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
