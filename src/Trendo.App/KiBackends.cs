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

public sealed class OllamaBackend(string baseUrl = "http://127.0.0.1:11434", HttpMessageHandler? handler = null) : IStreamingKiBackend
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

    /// <summary>Modell aus Ollama entfernen (DELETE /api/delete) – gibt den Speicherplatz frei.</summary>
    public async Task<bool> DeleteAsync(string model, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, "/api/delete") { Content = JsonContent.Create(new { model }) };
            using var r = await _http.SendAsync(req, ct); return r.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return false; }
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

    /// <summary>Antwort Stück für Stück (NDJSON von /api/chat mit stream=true) – für die Fortschrittsanzeige.</summary>
    public async Task<string> ChatStreamAsync(string model, IReadOnlyList<KiMessage> messages, Action<string> onDelta, CancellationToken ct = default)
    {
        var body = new { model, stream = true, messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray() };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat") { Content = JsonContent.Create(body) };
        using var r = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        r.EnsureSuccessStatusCode();
        return await KiStream.ReadNdjson(await r.Content.ReadAsStreamAsync(ct), onDelta, ct);
    }
}

/// <summary>Gestreamte Antworten lesen: Ollama (NDJSON) und OpenAI-Format (Server-Sent Events).</summary>
public static class KiStream
{
    public static async Task<string> ReadNdjson(Stream s, Action<string> onDelta, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder(); using var rd = new StreamReader(s);
        while (await rd.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("error", out var err)) throw new InvalidOperationException(err.GetString());
            if (doc.RootElement.TryGetProperty("message", out var m) && m.TryGetProperty("content", out var c) && c.GetString() is { Length: > 0 } t) { sb.Append(t); onDelta(t); }
            if (doc.RootElement.TryGetProperty("done", out var done) && done.GetBoolean()) break;
        }
        return sb.ToString().Trim();
    }

    public static async Task<string> ReadSse(Stream s, Action<string> onDelta, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder(); using var rd = new StreamReader(s);
        while (await rd.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim(); if (data == "[DONE]") break;
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("choices", out var ch) && ch.GetArrayLength() > 0 && ch[0].TryGetProperty("delta", out var d) && d.TryGetProperty("content", out var c) && c.GetString() is { Length: > 0 } t) { sb.Append(t); onDelta(t); }
        }
        return sb.ToString().Trim();
    }
}

/// <summary>LM Studio, llama.cpp-Server, Jan, KoboldCpp … sprechen das OpenAI-Format unter /v1 – hier nur lokal.</summary>
public sealed class OpenAiCompatBackend(string name, string baseUrl, HttpMessageHandler? handler = null) : IStreamingKiBackend
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

    /// <summary>Antwort Stück für Stück (Server-Sent Events) – für die Fortschrittsanzeige.</summary>
    public async Task<string> ChatStreamAsync(string model, IReadOnlyList<KiMessage> messages, Action<string> onDelta, CancellationToken ct = default)
    {
        var body = new { model, stream = true, messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray() };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions") { Content = JsonContent.Create(body) };
        using var r = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        r.EnsureSuccessStatusCode();
        return await KiStream.ReadSse(await r.Content.ReadAsStreamAsync(ct), onDelta, ct);
    }
}


/// <summary>KI-Dienst mit API-Schlüssel (OpenAI-Format). Bewusste Ausnahme von <see cref="LocalOnly"/>: nur HTTPS, nur genau die Adresse
/// aus <see cref="KiCatalog.Cloud"/>, nur wenn der Besitzer den Dienst eingeschaltet und einen Schlüssel gespeichert hat. Der Text verlässt dann den PC.</summary>
public sealed class CloudKiBackend : IRemoteKiBackend
{
    private readonly HttpClient _http;
    private readonly KiCloudService _svc;
    private readonly string _prefix;

    public CloudKiBackend(KiCloudService svc, string key, HttpMessageHandler? handler = null)
    {
        var u = new Uri(svc.BaseUrl.TrimEnd('/') + "/");
        if (u.Scheme != Uri.UriSchemeHttps || u.Port != 443 || KiCatalog.Cloud.All(c => c.Host != u.Host) || u.AbsolutePath.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidOperationException($"Nur die bekannten KI-Dienste über HTTPS, nicht {u.Host}.");
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Schlüssel fehlt.");
        _svc = svc; _prefix = u.AbsolutePath;
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(u.GetLeftPart(UriPartial.Authority)), Timeout = TimeSpan.FromSeconds(120) };
        _http.MaxResponseContentBufferSize = 8 * 1024 * 1024;
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        if (u.Host == "api.anthropic.com") { _http.DefaultRequestHeaders.Add("x-api-key", key); _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01"); }
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TrendoOS/1.0");
    }

    private static readonly Dictionary<(string, string), CloudKiBackend> Cache = [];
    /// <summary>Eine Verbindung je Dienst und Schlüssel (nicht bei jeder Frage neu).</summary>
    public static CloudKiBackend For(KiCloudService svc, string key) { lock (Cache) return Cache.TryGetValue((svc.Id, key), out var b) ? b : Cache[(svc.Id, key)] = new CloudKiBackend(svc, key); }

    public string Name => _svc.Name;
    public string BaseUrl => _svc.BaseUrl;
    public async Task<bool> IsUpAsync(CancellationToken ct = default) => (await ModelsAsync(ct)).Count > 0;

    /// <summary>Prüft einen Schlüssel über dieselben Grenzen (HTTPS, fester Anbieter, keine Weiterleitung): gültig, wenn der Dienst Modelle liefert.</summary>
    /// <summary>Vom Nutzer gewähltes Modell je Dienst (z. B. OpenRouter-Textmodell aus „Kostenlose Internet-KIs“).</summary>
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Preferred = new();

    /// <summary>C3: Frage mit Datei (Bild, Audio, Video) an ein Modell, das sie versteht. Die Datei geht dabei ins Internet.</summary>
    public async Task<string> AskWithFileAsync(string model, string question, string file, CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(file, ct);
        if (bytes.Length > 20 * 1024 * 1024) throw new InvalidOperationException("Die Datei ist zu groß (höchstens 20 MB).");
        var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
        var b64 = Convert.ToBase64String(bytes);
        object part = ext switch
        {
            "png" or "jpg" or "jpeg" or "webp" or "gif" => new { type = "image_url", image_url = new { url = $"data:image/{(ext == "jpg" ? "jpeg" : ext)};base64,{b64}" } },
            "wav" or "mp3" or "ogg" or "flac" or "m4a" => new { type = "input_audio", input_audio = new { data = b64, format = ext } },
            "mp4" or "webm" or "mov" => new { type = "video_url", video_url = new { url = $"data:video/{(ext == "mov" ? "quicktime" : ext)};base64,{b64}" } },
            _ => throw new InvalidOperationException("Dieses Dateiformat versteht die KI nicht (Bild, Audio oder Video wählen)."),
        };
        var body = new { model, stream = false, messages = new object[] { new { role = "user", content = new object[] { new { type = "text", text = question }, part } } } };
        using var r = await _http.PostAsJsonAsync(_prefix + "chat/completions", body, ct);
        r.EnsureSuccessStatusCode();
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
    }

    public static async Task<bool> ValidateAsync(KiCloudService svc, string key) { try { return (await new CloudKiBackend(svc, key).ModelsAsync()).Count > 0; } catch (ArgumentException) { return false; } }

    /// <summary>Modelle des Dienstes; das bevorzugte steht vorn (bei OpenRouter das erste kostenlose „:free“).
    /// Kostenpflichtige Dienste nur mit ihrem festen, günstigen Modell – fehlt es, wird nicht ausgewichen.</summary>
    public async Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(8000);
            var doc = await _http.GetFromJsonAsync<JsonElement>(_prefix + "models", cts.Token);
            var ids = doc.GetProperty("data").EnumerateArray().Select(m => m.GetProperty("id").GetString() ?? "").Where(n => n != "").ToList();
            // OpenRouter: ausschließlich kostenlose Modelle – nie versehentlich ein kostenpflichtiges
            if (_svc.Id == "openrouter") ids = ids.Where(i => i.EndsWith(":free", StringComparison.Ordinal)).ToList();
            if (Preferred.TryGetValue(_svc.Id, out var pref) && ids.Contains(pref)) return [pref, .. ids.Where(i => i != pref)];
            var pick = _svc.DefaultModel.StartsWith(':') ? ids.FirstOrDefault(i => i.EndsWith(_svc.DefaultModel, StringComparison.Ordinal))
                : ids.FirstOrDefault(i => i == _svc.DefaultModel || i == "models/" + _svc.DefaultModel);
            if (pick is null) return _svc.FreeTier ? ids : [];
            return [pick, .. ids.Where(i => i != pick)];
        }
        catch { return []; }
    }

    public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
    {
        var body = new { model, stream = false, messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray() };
        using var r = await _http.PostAsJsonAsync(_prefix + "chat/completions", body, ct);
        r.EnsureSuccessStatusCode();
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
    }
}
