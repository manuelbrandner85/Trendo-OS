using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.App;

/// <summary>Spricht mit dem lokalen ACE-Step-1.5-Server (REST-API, nur 127.0.0.1): Aufgabe abgeben (/release_task), Stand abfragen (/query_result),
/// fertiges Lied holen (/v1/audio). Beschreibung der API: github.com/ace-step/ACE-Step-1.5/blob/main/docs/en/API.md.</summary>
public sealed class AceStepClient(HttpClient http, int port = AceStepClient.DefaultPort)
{
    public const int DefaultPort = 8001;
    public Uri Base { get; } = new($"http://127.0.0.1:{port}/");

    /// <summary>Startbefehl im offiziellen Komplettpaket, wie start_api_server.bat, aber direkt: keine Git-Updateprüfung, keine Rückfragen,
    /// nur 127.0.0.1 und fester Port.</summary>
    public static (string File, string[] Args) ServerCommand(string runtimeDir, int port = DefaultPort) =>
        (Path.Combine(runtimeDir, "python_embeded", "python.exe"), ["acestep/api_server.py", "--host", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture)]);

    public sealed record Song(string Prompt, string Lyrics, int Seconds, string Language);

    /// <summary>Anfrage wie in der API-Doku: Stil, Liedtext, Länge, Sprache; ein Lied pro Auftrag, MP3; das Sprachmodell plant das Stück („thinking“).</summary>
    public static JsonObject Request(Song s) => new()
    {
        ["prompt"] = s.Prompt.Trim(),
        ["lyrics"] = string.IsNullOrWhiteSpace(s.Lyrics) ? "[Instrumental]" : s.Lyrics.Trim(),
        ["audio_duration"] = Math.Clamp(s.Seconds, 10, 600),
        ["vocal_language"] = s.Language,
        ["batch_size"] = 1,
        ["audio_format"] = "mp3",
        ["thinking"] = true,
    };

    /// <summary>Startumgebung: kleines Sprachmodell (0.6B) mit PyTorch-Backend – laut ACE-Step-Tabelle passend ab 6 GB Grafikspeicher; keine Telemetrie.
    /// Diese Werte haben Vorrang vor der .env im Paket (dort override=False).</summary>
    public static IReadOnlyDictionary<string, string> ServerEnvironment() => new Dictionary<string, string>
    {
        ["ACESTEP_LM_MODEL_PATH"] = "acestep-5Hz-lm-0.6B",
        ["ACESTEP_LM_BACKEND"] = "pt",
        ["ACESTEP_INIT_LLM"] = "auto",
        ["HF_HUB_DISABLE_TELEMETRY"] = "1",
        ["PYTHONUTF8"] = "1",
    };

    public async Task<bool> HealthyAsync(CancellationToken ct = default)
    {
        try
        {
            using var r = await http.GetAsync(new Uri(Base, "health"), ct);
            return r.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return false; }
    }

    static JsonNode Data(string body)
    {
        var root = JsonNode.Parse(body) ?? throw new InvalidOperationException("Leere Antwort von ACE-Step.");
        if (root["code"]?.GetValue<int>() is int code && code != 200) throw new InvalidOperationException("ACE-Step meldet: " + (root["error"]?.ToString() ?? code.ToString()));
        return root["data"] ?? throw new InvalidOperationException("Antwort ohne Daten.");
    }

    public async Task<string> SubmitAsync(Song s, CancellationToken ct = default)
    {
        using var r = await http.PostAsJsonAsync(new Uri(Base, "release_task"), Request(s), ct);
        r.EnsureSuccessStatusCode();
        return Data(await r.Content.ReadAsStringAsync(ct))["task_id"]?.ToString() ?? throw new InvalidOperationException("Keine Auftragsnummer erhalten.");
    }

    /// <summary>Stand: 0 = läuft, 1 = fertig (mit Pfad für /v1/audio), 2 = fehlgeschlagen.</summary>
    public sealed record State(int Status, string? AudioUrl, string? Error);

    public static State ParseState(string body, string taskId)
    {
        var data = Data(body);
        var item = (data as JsonArray)?.FirstOrDefault(x => x?["task_id"]?.ToString() == taskId) ?? (data is JsonObject o && o["task_id"]?.ToString() == taskId ? o : null);
        if (item is null) return new(0, null, null);
        var status = item["status"]?.GetValue<int>() ?? 0;
        string? url = null;
        // „result“ ist ein JSON-Text mit einer Liste der erzeugten Stücke
        if (item["result"] is JsonNode res)
        {
            var list = res is JsonValue v && v.TryGetValue<string>(out var txt) ? JsonNode.Parse(txt) : res;
            url = (list as JsonArray)?.Select(x => x?["file"]?.ToString()).FirstOrDefault(f => !string.IsNullOrEmpty(f));
        }
        return new(status, url, status == 2 ? item["error"]?.ToString() ?? "Erzeugen fehlgeschlagen." : null);
    }

    public async Task<State> QueryAsync(string taskId, CancellationToken ct = default)
    {
        using var r = await http.PostAsJsonAsync(new Uri(Base, "query_result"), new JsonObject { ["task_id_list"] = new JsonArray(taskId) }, ct);
        r.EnsureSuccessStatusCode();
        return ParseState(await r.Content.ReadAsStringAsync(ct), taskId);
    }

    /// <summary>Lädt das fertige Lied – nur vom lokalen Server, nur Pfade unter /v1/audio.</summary>
    public async Task DownloadAsync(string audioUrl, string target, CancellationToken ct = default)
    {
        var u = new Uri(Base, audioUrl);
        if (u.Host != "127.0.0.1" || !u.AbsolutePath.StartsWith("/v1/audio", StringComparison.Ordinal)) throw new InvalidOperationException("Unerwartete Adresse für das Lied.");
        using var r = await http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, ct);
        r.EnsureSuccessStatusCode();
        await using var s = await r.Content.ReadAsStreamAsync(ct);
        await using var f = File.Create(target);
        await s.CopyToAsync(f, ct);
    }
}
