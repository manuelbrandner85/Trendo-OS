using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.App;

/// <summary>Spricht mit dem lokalen ACE-Step-1.5-Server (REST-API, nur 127.0.0.1): Aufgabe abgeben (/release_task), Stand abfragen (/query_result),
/// fertiges Lied holen (/v1/audio). Beschreibung der API: github.com/ace-step/ACE-Step-1.5/blob/main/docs/en/API.md.</summary>
public sealed class AceStepClient(HttpClient http, int port = AceStepClient.DefaultPort)
{
    /// <summary>Gesangssprachen von ACE-Step 1.5 (acestep/constants.py VALID_LANGUAGES, geprüft 03.11.2026) – Deutsch und Englisch zuerst.</summary>
    public static readonly (string Code, string Name)[] Languages =
    [
        ("de", "Deutsch"), ("en", "Englisch"), ("ar", "Arabisch"), ("az", "Aserbaidschanisch"), ("bg", "Bulgarisch"), ("bn", "Bengalisch"), ("ca", "Katalanisch"),
        ("cs", "Tschechisch"), ("da", "Dänisch"), ("el", "Griechisch"), ("es", "Spanisch"), ("fa", "Persisch"), ("fi", "Finnisch"), ("fr", "Französisch"),
        ("he", "Hebräisch"), ("hi", "Hindi"), ("hr", "Kroatisch"), ("ht", "Haitianisch"), ("hu", "Ungarisch"), ("id", "Indonesisch"), ("is", "Isländisch"),
        ("it", "Italienisch"), ("ja", "Japanisch"), ("ko", "Koreanisch"), ("la", "Latein"), ("lt", "Litauisch"), ("ms", "Malaiisch"), ("ne", "Nepalesisch"),
        ("nl", "Niederländisch"), ("no", "Norwegisch"), ("pa", "Panjabi"), ("pl", "Polnisch"), ("pt", "Portugiesisch"), ("ro", "Rumänisch"), ("ru", "Russisch"),
        ("sa", "Sanskrit"), ("sk", "Slowakisch"), ("sr", "Serbisch"), ("sv", "Schwedisch"), ("sw", "Suaheli"), ("ta", "Tamil"), ("te", "Telugu"), ("th", "Thailändisch"),
        ("tl", "Tagalog"), ("tr", "Türkisch"), ("uk", "Ukrainisch"), ("ur", "Urdu"), ("vi", "Vietnamesisch"), ("yue", "Kantonesisch"), ("zh", "Chinesisch"),
    ];

    public const int DefaultPort = 8001;
    /// <summary>So viel freier Grafikspeicher reicht, damit das Lied auf der Grafikkarte fertig wird (Probelauf 27.10.2026: bei 0 GB frei lief die Fertigstellung auf dem Prozessor über eine Stunde).</summary>
    public const double MinFreeVramGb = 4;

    /// <summary>Verständlicher Hinweis, wenn die Grafikkarte zu voll ist; null, wenn genug frei ist oder nicht messbar.</summary>
    public static string? VramAdvice((double FreeGb, double TotalGb)? mem) =>
        mem is { } m && m.FreeGb < MinFreeVramGb
            ? string.Create(System.Globalization.CultureInfo.GetCultureInfo("de-DE"), $"Deine Grafikkarte ist fast voll ({m.FreeGb:0.#} von {m.TotalGb:0.#} GB frei). Andere Programme wie Unreal, Blender oder Spiele belegen sie gerade. " +
              $"Schließe sie am besten vorher – sonst rechnet der Prozessor, und ein Lied kann über eine Stunde dauern. Erneut klicken = trotzdem starten.")
            : null;
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
        // Das planende Sprachmodell kürzt lange Lieder (bis 12 GB Grafikspeicher auf 8 Minuten, gemessen 03.11.2026: 600 s → 8:02).
        // Über 8 Minuten ohne Sprachmodell – dann entstehen echte 10:00 Minuten.
        ["thinking"] = s.Seconds <= ThinkingMaxSeconds,
    };
    /// <summary>Bis zu dieser Länge plant das Sprachmodell das Stück mit; darüber würde es gekürzt.</summary>
    public const int ThinkingMaxSeconds = 480;

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

    /// <summary>Studio „Trendo Musik“: Auftrag mit allen geprüften Möglichkeiten (03.11.2026 auf einer RTX 5070 echt erprobt:
    /// Varianten, Sprachen, Abschnitt neu erzeugen und Cover gehen; Verlängern, Spuren trennen und Begleitung liefern mit dem Turbo-Modell kein brauchbares Ergebnis).</summary>
    public sealed record Job(string Prompt, string Lyrics, int Seconds, string Language, int Variants = 1, int? Bpm = null, string? Key = null, string? TimeSignature = null,
        string Task = "text2music", double RepaintStart = 0, double RepaintEnd = -1, double CoverStrength = 0.6, string Format = "mp3", string? SampleQuery = null);
    // SampleQuery: Einfach-Modus von ACE-Step (am 03.11.2026 echt erprobt) – die Sprach-KI schreibt Liedtext, Stil, Tempo und Tonart selbst und singt;
    // die Länge legt sie dabei selbst fest (gemessen: 100–120 s statt der gewünschten 60 s).

    public static readonly string[] Tasks = ["text2music", "repaint", "cover"];
    public static readonly string[] Keys = [.. new[] { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" }.SelectMany(n => new[] { n + " major", n + " minor" })];
    public static readonly (string Label, string Value)[] TimeSignatures = [("4/4", "4"), ("3/4", "3"), ("6/8", "6"), ("2/4", "2")];

    /// <summary>Felder des Auftrags (Werte wie in der API-Doku; Grenzen geprüft).</summary>
    public static Dictionary<string, string> Fields(Job j)
    {
        if (!Tasks.Contains(j.Task)) throw new ArgumentException("Unbekannte Aufgabe: " + j.Task);
        var secs = Math.Clamp(j.Seconds, 10, 600);
        var f = new Dictionary<string, string>
        {
            ["prompt"] = j.Prompt.Trim(), ["lyrics"] = string.IsNullOrWhiteSpace(j.Lyrics) ? "[Instrumental]" : j.Lyrics.Trim(),
            ["audio_duration"] = secs.ToString(System.Globalization.CultureInfo.InvariantCulture), ["vocal_language"] = j.Language,
            ["batch_size"] = Math.Clamp(j.Variants, 1, 2).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["audio_format"] = j.Format is "mp3" or "wav" or "flac" ? j.Format : "mp3", ["task_type"] = j.Task,
            ["thinking"] = secs <= ThinkingMaxSeconds && j.Task == "text2music" ? "true" : "false",
        };
        if (j.Bpm is int b) f["bpm"] = Math.Clamp(b, 30, 300).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (j.Key is { Length: > 0 } k) f["key_scale"] = Keys.Contains(k) ? k : throw new ArgumentException("Unbekannte Tonart: " + k);
        if (j.TimeSignature is { Length: > 0 } ts) f["time_signature"] = TimeSignatures.Any(x => x.Value == ts) ? ts : throw new ArgumentException("Unbekannter Takt: " + ts);
        if (j.Task == "repaint")
        {
            if (j.RepaintEnd >= 0 && j.RepaintEnd - j.RepaintStart < 1) throw new ArgumentException("Der Abschnitt muss mindestens 1 Sekunde lang sein.");
            f["repainting_start"] = j.RepaintStart.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            f["repainting_end"] = j.RepaintEnd.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (j.SampleQuery is { Length: > 0 } q && j.Task == "text2music")
        {
            f["sample_query"] = q.Trim(); f.Remove("lyrics"); f["prompt"] = ""; f["thinking"] = "true"; // Text schreibt ACE-Step – nicht „[Instrumental]“ schicken
        }
        if (j.Task == "cover") f["audio_cover_strength"] = Math.Clamp(j.CoverStrength, 0.1, 1.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return f;
    }

    /// <summary>Auftrag abschicken; bei Abschnitt/Cover wird das Ausgangslied hochgeladen.</summary>
    public async Task<string> SubmitAsync(Job j, string? sourceAudio = null, CancellationToken ct = default)
    {
        var fields = Fields(j);
        HttpResponseMessage r;
        if (j.Task == "text2music") r = await http.PostAsJsonAsync(new Uri(Base, "release_task"), new JsonObject(fields.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)(kv.Key is "thinking" ? JsonValue.Create(kv.Value == "true") : kv.Key is "audio_duration" or "batch_size" or "bpm" ? JsonValue.Create(int.Parse(kv.Value, System.Globalization.CultureInfo.InvariantCulture)) : JsonValue.Create(kv.Value))))), ct);
        else
        {
            if (sourceAudio is null || !File.Exists(sourceAudio)) throw new InvalidOperationException("Das Ausgangslied fehlt.");
            using var form = new MultipartFormDataContent();
            foreach (var (k, v) in fields) form.Add(new StringContent(v), k);
            var audio = new ByteArrayContent(await File.ReadAllBytesAsync(sourceAudio, ct)); audio.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
            form.Add(audio, "src_audio", Path.GetFileName(sourceAudio));
            r = await http.PostAsync(new Uri(Base, "release_task"), form, ct);
        }
        using (r)
        {
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException("ACE-Step lehnt den Auftrag ab: " + await r.Content.ReadAsStringAsync(ct));
            return Data(await r.Content.ReadAsStringAsync(ct))["task_id"]?.ToString() ?? throw new InvalidOperationException("Keine Auftragsnummer erhalten.");
        }
    }

    public async Task<string> SubmitAsync(Song s, CancellationToken ct = default)
    {
        using var r = await http.PostAsJsonAsync(new Uri(Base, "release_task"), Request(s), ct);
        r.EnsureSuccessStatusCode();
        return Data(await r.Content.ReadAsStringAsync(ct))["task_id"]?.ToString() ?? throw new InvalidOperationException("Keine Auftragsnummer erhalten.");
    }

    /// <summary>Stand: 0 = läuft, 1 = fertig (mit Pfad für /v1/audio), 2 = fehlgeschlagen.</summary>
    public sealed record State(int Status, string? AudioUrl, string? Error)
    {
        /// <summary>Alle erzeugten Stücke (Varianten) – das erste ist <see cref="AudioUrl"/>.</summary>
        public IReadOnlyList<string> AudioUrls { get; init; } = AudioUrl is null ? [] : [AudioUrl];
        /// <summary>Was ACE-Step tatsächlich gesungen hat (im Einfach-Modus selbst geschrieben), Beschreibung und Länge.</summary>
        public string? Lyrics { get; init; }
        public string? Caption { get; init; }
        public double? Duration { get; init; }
    }

    public static State ParseState(string body, string taskId)
    {
        var data = Data(body);
        var item = (data as JsonArray)?.FirstOrDefault(x => x?["task_id"]?.ToString() == taskId) ?? (data is JsonObject o && o["task_id"]?.ToString() == taskId ? o : null);
        if (item is null) return new(0, null, null);
        var status = item["status"]?.GetValue<int>() ?? 0;
        string? url = null; List<string> urls = []; string? lyrics = null, caption = null; double? duration = null;
        // „result“ ist ein JSON-Text mit einer Liste der erzeugten Stücke
        if (item["result"] is JsonNode res)
        {
            var list = res is JsonValue v && v.TryGetValue<string>(out var txt) ? JsonNode.Parse(txt) : res;
            urls = (list as JsonArray)?.Select(x => x?["file"]?.ToString()).Where(f => !string.IsNullOrEmpty(f)).Cast<string>().ToList() ?? [];
            url = urls.FirstOrDefault();
            if ((list as JsonArray)?.FirstOrDefault() is JsonObject first)
            {
                lyrics = first["lyrics"]?.ToString(); caption = first["prompt"]?.ToString();
                if (first["metas"]?["duration"] is JsonValue dv && dv.TryGetValue<double>(out var d)) duration = d;
            }
        }
        return new(status, url, status == 2 ? item["error"]?.ToString() ?? "Erzeugen fehlgeschlagen." : null) { AudioUrls = urls, Lyrics = lyrics, Caption = caption, Duration = duration };
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
