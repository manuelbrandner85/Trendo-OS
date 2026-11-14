using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Kie;

/// <summary>kie.ai (Freigabe 13.11.2026, K1–K4): ein Schlüssel je Person, viele Modelle für Bilder, Video, Musik und Sprache.
/// Kostenpflichtig, darum immer optional und standardmäßig aus. Antworten kommen bei kie.ai immer mit HTTP 200; der eigentliche Status steht im Feld <c>code</c>.
/// Der Schlüssel geht nur an kie.ai (api.kie.ai und den Upload-Dienst), nie an die Ergebnis-Server.</summary>
public sealed class KieException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

public sealed record KieTask(string Id, string State, IReadOnlyList<string> Urls, string? FailMessage, double? Credits, long? CostMs, string? ResultJson = null)
{
    public bool Done => State is "success" or "fail";
    public bool Ok => State == "success";
}

public sealed class KieClient(IWebSend web, string key)
{
    public const string Api = "https://api.kie.ai";
    public const string UploadHost = "https://kieai.redpandaai.co";
    public const string KeyPage = "https://kie.ai/api-key";
    public const string LogsPage = "https://kie.ai/logs";
    /// <summary>Laut kie.ai/pricing: 1 Credit ≈ 0,005 US-Dollar.</summary>
    public const double UsdPerCredit = 0.005;
    public const int MaxBase64Bytes = 8 * 1024 * 1024;

    Dictionary<string, string> Head(bool json = true)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new KieException(401, "Es ist kein kie.ai-Schlüssel eingetragen.");
        var h = new Dictionary<string, string> { ["Authorization"] = "Bearer " + key.Trim() };
        if (json) h["Accept"] = "application/json";
        return h;
    }

    /// <summary>Verständliche Meldung zu den Fehlercodes von kie.ai.</summary>
    public static string Explain(int code, string? msg) => code switch
    {
        401 => "kie.ai kennt diesen Schlüssel nicht. Bitte den Schlüssel prüfen.",
        402 => "Dein kie.ai-Guthaben reicht nicht. Aufladen kannst du auf kie.ai.",
        404 => "kie.ai findet das nicht (Modell oder Auftrag unbekannt).",
        408 => "kie.ai hat in der erlaubten Zeit kein Ergebnis geliefert. Der Auftrag kann dort noch fertig werden – „Ergebnis holen“ versucht es erneut.",
        422 => "kie.ai lehnt die Eingaben ab" + (string.IsNullOrWhiteSpace(msg) ? "." : ": " + msg),
        429 => "Zu viele Aufträge auf einmal – bitte kurz warten.",
        455 => "kie.ai wird gerade gewartet. Später nochmal versuchen.",
        501 => "Die Erzeugung ist bei kie.ai fehlgeschlagen" + (string.IsNullOrWhiteSpace(msg) ? "." : ": " + msg),
        505 => "Diese Funktion ist bei kie.ai gerade abgeschaltet.",
        _ => $"kie.ai meldet Fehler {code}" + (string.IsNullOrWhiteSpace(msg) ? "." : ": " + msg),
    };

    static JsonNode? Data(WebReply r)
    {
        if (!r.Ok) throw new KieException(r.Status, Explain(r.Status, null));
        JsonObject o;
        try { o = JsonNode.Parse(r.Text) as JsonObject ?? throw new KieException(500, "kie.ai hat eine unlesbare Antwort geschickt."); }
        catch (JsonException) { throw new KieException(500, "kie.ai hat eine unlesbare Antwort geschickt."); }
        var code = o["code"] is JsonValue c && c.TryGetValue<int>(out var ci) ? ci : 200;
        var msg = o["msg"] is JsonValue m && m.TryGetValue<string>(out var ms) ? ms : null;
        if (code != 200) throw new KieException(code, Explain(code, msg));
        return o["data"];
    }

    /// <summary>Restguthaben in Credits.</summary>
    public async Task<double> CreditsAsync(CancellationToken ct = default)
    {
        var d = Data(await web.SendAsync("GET", Api + "/api/v1/chat/credit", Head(), null, null, ct));
        return d is JsonValue v && v.TryGetValue<double>(out var x) ? x : throw new KieException(500, "kie.ai hat keinen Kontostand geliefert.");
    }

    /// <summary>Auftrag anlegen, liefert die Auftragsnummer.</summary>
    public async Task<string> CreateAsync(string model, JsonObject input, CancellationToken ct = default)
    {
        var body = new JsonObject { ["model"] = model, ["input"] = input.DeepClone() };
        var d = Data(await web.SendAsync("POST", Api + "/api/v1/jobs/createTask", Head(), Encoding.UTF8.GetBytes(body.ToJsonString()), "application/json", ct));
        return d?["taskId"] is JsonValue t && t.TryGetValue<string>(out var id) && id.Length > 0 ? id : throw new KieException(500, "kie.ai hat keine Auftragsnummer geliefert.");
    }

    public async Task<KieTask> GetAsync(string taskId, CancellationToken ct = default)
    {
        var d = Data(await web.SendAsync("GET", Api + "/api/v1/jobs/recordInfo?taskId=" + Uri.EscapeDataString(taskId), Head(), null, null, ct)) as JsonObject
                ?? throw new KieException(500, "kie.ai hat keinen Auftragsstand geliefert.");
        static string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        static double? D(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var x) ? x : null;
        var state = S(d["state"]) ?? "waiting";
        return new(taskId, state, ResultUrls(S(d["resultJson"])), S(d["failMsg"]) ?? S(d["failCode"]), D(d["creditsConsumed"]), d["costTime"] is JsonValue ct2 && ct2.TryGetValue<long>(out var ms) ? ms : null, S(d["resultJson"]));
    }

    /// <summary>Wartet auf das Ergebnis: erst alle 3 s, dann langsamer bis 15 s. Videos können über 10 Minuten brauchen.</summary>
    public async Task<KieTask> WaitAsync(string taskId, Func<TimeSpan, CancellationToken, Task> delay, TimeSpan max, Action<KieTask>? progress = null, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow; var wait = TimeSpan.FromSeconds(3);
        while (true)
        {
            KieTask? t = null;
            try { t = await GetAsync(taskId, ct); }
            // kurze Netz- oder Dienststörungen nicht als Fehlschlag werten: der bezahlte Auftrag läuft bei kie.ai weiter
            catch (Exception e) when (!ct.IsCancellationRequested && Transient(e)) { }
            if (t is not null) { progress?.Invoke(t); if (t.Done) return t; }
            if (DateTime.UtcNow - start > max) throw new KieException(408, Explain(408, null));
            await delay(wait, ct);
            wait = TimeSpan.FromSeconds(Math.Min(15, wait.TotalSeconds * 1.5));
        }
    }

    /// <summary>Vorübergehende Störung (Netz weg, Zeitüberschreitung, Wartung, Überlast)? Der Netzfehler der App wird nur am Namen erkannt –
    /// der Kern kennt kein Netzwerk-Paket (QA-019).</summary>
    public static bool Transient(Exception e) =>
        e is TaskCanceledException or TimeoutException or IOException
        || e.GetType().FullName == "System.Net.Http.HttpRequestException"
        || e is KieException { Code: 429 or 455 or 500 or 502 or 503 or 504 };

    /// <summary>Datei zu kie.ai hochladen (bleibt dort etwa 24 Stunden), liefert die Adresse für den Auftrag.</summary>
    public async Task<string> UploadAsync(byte[] bytes, string fileName, CancellationToken ct = default)
    {
        var name = Path.GetFileName(fileName);
        WebReply r;
        if (bytes.Length <= MaxBase64Bytes)
        {
            var body = new JsonObject { ["base64Data"] = Convert.ToBase64String(bytes), ["uploadPath"] = "trendo", ["fileName"] = name };
            r = await web.SendAsync("POST", UploadHost + "/api/file-base64-upload", Head(), Encoding.UTF8.GetBytes(body.ToJsonString()), "application/json", ct);
        }
        else
        {
            var boundary = "trendo" + Guid.NewGuid().ToString("N");
            using var ms = new MemoryStream();
            void W(string s) { var b = Encoding.UTF8.GetBytes(s); ms.Write(b); }
            W($"--{boundary}\r\nContent-Disposition: form-data; name=\"uploadPath\"\r\n\r\ntrendo\r\n");
            W($"--{boundary}\r\nContent-Disposition: form-data; name=\"fileName\"\r\n\r\n{name}\r\n");
            W($"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{name.Replace("\"", "")}\"\r\nContent-Type: application/octet-stream\r\n\r\n");
            ms.Write(bytes); W($"\r\n--{boundary}--\r\n");
            r = await web.SendAsync("POST", UploadHost + "/api/file-stream-upload", Head(), ms.ToArray(), "multipart/form-data; boundary=" + boundary, ct);
        }
        var d = Data(r);
        return d?["downloadUrl"] is JsonValue u && u.TryGetValue<string>(out var url) && url.StartsWith("https://", StringComparison.Ordinal) ? url
            : d?["fileUrl"] is JsonValue f && f.TryGetValue<string>(out var furl) && furl.StartsWith("https://", StringComparison.Ordinal) ? furl
            : throw new KieException(500, "kie.ai hat keine Adresse für die hochgeladene Datei geliefert.");
    }

    /// <summary>Alle https-Adressen aus dem Ergebnis (resultUrls, audio_url, video_url …), ohne Doppelte, in Reihenfolge.</summary>
    public static IReadOnlyList<string> ResultUrls(string? resultJson)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(resultJson)) return list;
        JsonNode? root;
        try { root = JsonNode.Parse(resultJson); } catch (JsonException) { return list; }
        void Walk(JsonNode? n, string key)
        {
            switch (n)
            {
                case JsonObject o: foreach (var (k, v) in o) Walk(v, k); break;
                case JsonArray a: foreach (var v in a) Walk(v, key); break;
                case JsonValue v when v.TryGetValue<string>(out var s) && s.StartsWith("https://", StringComparison.Ordinal)
                                       && (key.Contains("url", StringComparison.OrdinalIgnoreCase) || key.Contains("Url", StringComparison.Ordinal)) && !key.Contains("stream", StringComparison.OrdinalIgnoreCase):
                    if (!list.Contains(s)) list.Add(s); break;
            }
        }
        Walk(root, "");
        return list;
    }
}

/// <summary>Was ein Auftrag braucht. Nicht jedes Modell nutzt jedes Feld.</summary>
public sealed record KieInput(
    string Prompt = "", string Aspect = "1:1", IReadOnlyList<string>? ImageUrls = null, string? AudioUrl = null, int Seconds = 5,
    string Title = "", string Style = "", string Lyrics = "", bool Instrumental = false, string Voice = "", string Upscale = "2",
    bool Loop = false, int Tempo = 0, string PersonaId = "");

public enum KieKind { Bild, BildBearbeiten, Freistellen, Hochskalieren, Video, Avatar, Musik, Sprache, Geraeusch }

/// <summary>Ein Modell bei kie.ai. <paramref name="Credits"/>: belegter Preis je Auftrag (null = Preis steht erst nach dem Auftrag fest).
/// <paramref name="Verified"/>: Anfrageform aus der offiziellen Beschreibung belegt; sonst als „ungeprüft“ gekennzeichnet.</summary>
public sealed record KieModel(string Id, string Label, KieKind Kind, string Hint, double? Credits, bool Verified, bool NeedsImage, bool NeedsAudio, Func<KieInput, JsonObject> Input);

public static partial class KieModels
{
    static JsonArray Arr(IEnumerable<string>? xs) => new((xs ?? []).Select(x => (JsonNode)x).ToArray());

    /// <summary>Kuratierte Liste, nur Modelle mit belegter Anfrageform. Midjourney bewusst nicht (keine offizielle API, Automatisierung untersagt).
    /// Chat-Modelle bewusst nicht: Claude nur über die Anmeldung der Person.</summary>
    public static readonly IReadOnlyList<KieModel> All =
    [
        new("google/nano-banana", "Nano Banana (Google)", KieKind.Bild, "Schnell, gute Qualität, viele Seitenverhältnisse", 4, true, false, false,
            i => new JsonObject { ["prompt"] = i.Prompt, ["output_format"] = "png", ["image_size"] = i.Aspect }),
        new("google/nano-banana-edit", "Nano Banana – Bild bearbeiten", KieKind.BildBearbeiten, "Vorhandenes Bild nach Beschreibung ändern", 4, true, true, false,
            i => new JsonObject { ["prompt"] = i.Prompt, ["image_urls"] = Arr(i.ImageUrls), ["output_format"] = "png", ["image_size"] = i.Aspect }),
        new("recraft/remove-background", "Hintergrund entfernen (Recraft)", KieKind.Freistellen, "Motiv freistellen, PNG mit Transparenz", 1, true, true, false,
            i => new JsonObject { ["image"] = i.ImageUrls?.FirstOrDefault() ?? "" }),
        new("topaz/image-upscale", "Hochskalieren (Topaz)", KieKind.Hochskalieren, "Bild 2-, 4- oder 8-fach vergrößern", null, true, true, false,
            i => new JsonObject { ["image_url"] = i.ImageUrls?.FirstOrDefault() ?? "", ["upscale_factor"] = i.Upscale is "1" or "2" or "4" or "8" ? i.Upscale : "2" }),
        new("kling-3.0/video", "Kling 3.0 Video", KieKind.Video, "Text oder Bild zu Video, 3–15 Sekunden", null, true, false, false,
            i => { var o = new JsonObject { ["prompt"] = i.Prompt, ["duration"] = Math.Clamp(i.Seconds, 3, 15).ToString(CultureInfo.InvariantCulture), ["aspect_ratio"] = i.Aspect is "16:9" or "9:16" or "1:1" ? i.Aspect : "16:9", ["sound"] = false };
                   if (i.ImageUrls is { Count: > 0 }) o["image_urls"] = Arr(i.ImageUrls.Take(2)); return o; }),
        // Veo 3.1 (docs.kie.ai, Stand 14.11.2026): Modell „veo-3-1“, Stufe in input.model; Ton immer dabei; 1080p/4K kosten extra (hier 720p)
        new("veo-3-1", "Veo 3.1 Fast (Google)", KieKind.Video, "Text oder Startbild zu Video, 8 Sekunden, mit Ton", null, true, false, false,
            i => { var o = new JsonObject { ["prompt"] = i.Prompt, ["model"] = "veo3_fast", ["aspect_ratio"] = i.Aspect is "9:16" ? "9:16" : "16:9" };
                   if (i.ImageUrls is { Count: > 0 }) { o["image_urls"] = Arr(i.ImageUrls.Take(2)); o["generation_type"] = "FIRST_AND_LAST_FRAMES_2_VIDEO"; }
                   else o["generation_type"] = "TEXT_2_VIDEO";
                   return o; }),
        new("kling/ai-avatar-standard", "Kling Avatar (sprechende Figur)", KieKind.Avatar, "Porträt plus Sprachaufnahme wird zum sprechenden Video", null, true, true, true,
            i => new JsonObject { ["image_url"] = i.ImageUrls?.FirstOrDefault() ?? "", ["audio_url"] = i.AudioUrl ?? "", ["prompt"] = string.IsNullOrWhiteSpace(i.Prompt) ? "natural talking, friendly" : i.Prompt }),
        new("ai-music-api/generate", "Suno V6 – Lied", KieKind.Musik, "Lied mit Gesang oder Instrumental, 10–360 Sekunden", null, true, false, false,
            i => { var o = new JsonObject { ["model"] = "V6", ["custom_mode"] = true, ["instrumental"] = i.Instrumental, ["title"] = KCut(i.Title, 80), ["style"] = KCut(i.Style, 1000), ["duration"] = Math.Clamp(i.Seconds, 10, 360) };
                   if (!i.Instrumental && !string.IsNullOrWhiteSpace(i.Lyrics)) o["lyrics"] = KCut(i.Lyrics, 5000);
                   // S2: eigene Stimme (Voice-ID aus dem Prüfsatz-Ablauf) – nur mit Gesang
                   if (!i.Instrumental && !string.IsNullOrWhiteSpace(i.PersonaId)) { o["persona_id"] = i.PersonaId.Trim(); o["persona_model"] = "voice_persona"; }
                   return o; }),
        new("ai-music-api/sounds", "Suno – Geräusche & Klänge", KieKind.Geraeusch, "Geräusch, Klangteppich oder Loop aus einer Beschreibung", null, true, false, false,
            i => { var o = new JsonObject { ["prompt"] = KCut(i.Prompt, 500), ["model"] = "V6", ["sound_loop"] = i.Loop };
                   if (i.Tempo is >= 1 and <= 300) o["sound_tempo"] = i.Tempo; return o; }),
        new("elevenlabs/text-to-speech-multilingual-v2", "ElevenLabs – Sprachausgabe", KieKind.Sprache, "Text vorlesen lassen, viele Sprachen", null, false, false, false,
            i => new JsonObject { ["text"] = KCut(i.Text(), 5000), ["voice"] = string.IsNullOrWhiteSpace(i.Voice) ? DefaultVoice : i.Voice.Trim() }),
    ];

    /// <summary>Standardstimme laut kie.ai-Beschreibung („James“). Die Zuordnung weiterer Stimmen ist bei kie.ai nicht sauber belegt.</summary>
    public const string DefaultVoice = "EkK5I93UQWFDigLMpZcX";

    static string Text(this KieInput i) => string.IsNullOrWhiteSpace(i.Lyrics) ? i.Prompt : i.Lyrics;
    static string KCut(string s, int max) => Kreativ.KreativSafe.Cut((s ?? "").Trim(), max);

    public static IEnumerable<KieModel> Of(KieKind k) => All.Where(m => m.Kind == k);
    public static KieModel? Find(string id) => All.FirstOrDefault(m => m.Id == id);

    public static string KindLabel(KieKind k) => k switch
    {
        KieKind.Bild => "Bild erzeugen", KieKind.BildBearbeiten => "Bild bearbeiten", KieKind.Freistellen => "Hintergrund entfernen", KieKind.Hochskalieren => "Hochskalieren",
        KieKind.Video => "Video", KieKind.Avatar => "Sprechende Figur", KieKind.Musik => "Musik (Suno)", KieKind.Geraeusch => "Geräusche & Klänge", _ => "Sprachausgabe",
    };

    /// <summary>Welche Arten in welchem Studio zuerst stehen. Jedes Studio bekommt mindestens Bilder und Sprache.</summary>
    public static IReadOnlyList<KieKind> ForStudio(string? studio) => studio switch
    {
        "STUDIO-IMAGEAI-01" or "STUDIO-LOGO-01" or "STUDIO-COMIC-01" or "STUDIO-POD-01" or "CORE-07" or "X-07"
            => [KieKind.Bild, KieKind.BildBearbeiten, KieKind.Freistellen, KieKind.Hochskalieren],
        "STUDIO-FILM-01" or "CORE-08" => [KieKind.Video, KieKind.Bild, KieKind.Avatar, KieKind.Sprache, KieKind.Musik, KieKind.Geraeusch, KieKind.BildBearbeiten],
        "STUDIO-ANIM-01" => [KieKind.Video, KieKind.Bild, KieKind.Freistellen],
        "STUDIO-VIDEOAI-01" => [KieKind.Video, KieKind.Bild, KieKind.Avatar, KieKind.Geraeusch],
        "STUDIO-MUSIK-01" => [KieKind.Musik, KieKind.Geraeusch, KieKind.Video, KieKind.Bild],
        "STUDIO-PODCAST-01" => [KieKind.Sprache, KieKind.Musik, KieKind.Geraeusch, KieKind.Avatar, KieKind.Bild],
        "STUDIO-BOOK-01" => [KieKind.Bild, KieKind.Sprache],
        "SPEC-15" or "STUDIO-SOCIAL-01" => [KieKind.Video, KieKind.Bild, KieKind.Avatar, KieKind.Sprache],
        _ => [KieKind.Bild, KieKind.Sprache, KieKind.Video, KieKind.Musik, KieKind.Geraeusch],
    };

    // Ausdrücklich eine fremde Stimme: immer gesperrt
    [GeneratedRegex(@"\b(stimme von|stimme wie|voice of|klingt wie|sounds like|sing(t|en)? wie|sung by|gesungen von)\b", RegexOptions.IgnoreCase)]
    private static partial Regex VoiceRx();
    // Stil eines namentlich genannten Künstlers: zwei großgeschriebene Wörter nach „im Stil von“, „like“, „à la“ … (ohne IgnoreCase, damit „wie ein See“ frei bleibt)
    [GeneratedRegex(@"\b(im Stil von|Im Stil von|in the style of|In the style of|like|Like|à la|a la|wie|Wie)\s+\p{Lu}[\p{Ll}'’-]+(\s+\p{Lu}[\p{Ll}'’-]+)+")]
    private static partial Regex ArtistRx();

    /// <summary>Höchstlängen je Feld (kie.ai): Beschreibung 5000, Stil 1000, Liedtext 5000, Titel 80.</summary>
    static string? TooLong(KieInput i) =>
        i.Prompt.Length > 5000 ? "Die Beschreibung ist zu lang (höchstens 5000 Zeichen)." :
        i.Style.Length > 1000 ? "Der Stil ist zu lang (höchstens 1000 Zeichen)." :
        i.Lyrics.Length > 5000 ? "Der Liedtext ist zu lang (höchstens 5000 Zeichen)." : null;

    const int SoundMax = 500;

    /// <summary>Grund, warum ein Auftrag nicht losgeht, sonst null. Regeln von Trendo, weil kie.ai selbst kaum prüft.</summary>
    public static string? Blocked(KieModel m, KieInput i, bool consent)
    {
        var text = string.Join(" ", i.Prompt, i.Style, i.Title, i.Lyrics);
        if (Kreativ.ImageAiKit.Offensive(text)) return "Solche Inhalte erzeugt Trendo nicht.";
        if (TooLong(i) is { } tl) return tl;
        if (m.Kind == KieKind.Geraeusch && i.Prompt.Length > SoundMax) return "Die Beschreibung eines Geräuschs darf höchstens 500 Zeichen haben.";
        if (m.Kind is KieKind.Musik or KieKind.Sprache or KieKind.Avatar or KieKind.Geraeusch && (VoiceRx().IsMatch(text) || ArtistRx().IsMatch(text)))
            return "Stimmen oder Stil echter Personen werden nicht nachgeahmt. Beschreibe Genre, Stimmung und Instrumente.";
        if (m.Kind == KieKind.Avatar && !consent) return "Für eine sprechende Figur bestätige, dass das Bild dich selbst oder eine erfundene Figur zeigt und die Stimme dir gehört oder du die Einwilligung hast.";
        if (m.NeedsImage && i.ImageUrls is not { Count: > 0 }) return "Wähle ein Bild aus.";
        if (m.NeedsAudio && string.IsNullOrWhiteSpace(i.AudioUrl)) return "Wähle eine Sprachaufnahme aus.";
        return m.Kind switch
        {
            KieKind.Bild or KieKind.BildBearbeiten or KieKind.Video or KieKind.Geraeusch when string.IsNullOrWhiteSpace(i.Prompt) => "Beschreibe, was entstehen soll.",
            KieKind.Musik when string.IsNullOrWhiteSpace(i.Title) || string.IsNullOrWhiteSpace(i.Style) => "Für ein Lied braucht es Titel und Stil (Genre, Stimmung, Instrumente).",
            KieKind.Sprache when string.IsNullOrWhiteSpace(i.Prompt) && string.IsNullOrWhiteSpace(i.Lyrics) => "Gib den Text ein, der vorgelesen werden soll.",
            _ => null,
        };
    }

    /// <summary>Richtpreis in Credits vor dem Auftrag, wo kie.ai ihn veröffentlicht (kie.ai/pricing, Stand 13.11.2026); null = unbekannt.
    /// Kling 3.0: 0,0755 $ je Sekunde; ElevenLabs: 6 Credits je 1000 Zeichen; Nano Banana 4, Recraft 1.</summary>
    public static double? Estimate(KieModel m, KieInput i) => m.Id switch
    {
        "kling-3.0/video" => Math.Round(Math.Clamp(i.Seconds, 3, 15) * 0.0755 / KieClient.UsdPerCredit, 1),
        "elevenlabs/text-to-speech-multilingual-v2" => Math.Max(6, Math.Ceiling((string.IsNullOrWhiteSpace(i.Lyrics) ? i.Prompt : i.Lyrics).Length / 1000.0) * 6),
        _ => m.Credits,
    };

    public static bool PriceKnown(KieModel m, KieInput i) => Estimate(m, i) is not null;

    static readonly CultureInfo DeC = CultureInfo.GetCultureInfo("de-DE");

    public static string CostText(KieModel m, KieInput? i = null) => Estimate(m, i ?? new KieInput()) is { } c
        ? $"etwa {c.ToString("0.#", DeC)} Credits (≈ {(c * KieClient.UsdPerCredit).ToString("0.00", DeC)} $)"
        : "einen Preis, den kie.ai vorab nicht veröffentlicht (unbekannt) – die echten Kosten zeigt Trendo danach";

    /// <summary>Passt der Inhalt zur Art? Verhindert, dass eine Fehlerseite als Bild oder Video gespeichert wird.</summary>
    public static bool LooksLike(byte[] b, KieKind kind)
    {
        bool At(int o, params byte[] sig) => b.Length >= o + sig.Length && sig.Select((x, k) => b[o + k] == x).All(v => v);
        var png = At(0, 0x89, 0x50, 0x4E, 0x47); var jpg = At(0, 0xFF, 0xD8, 0xFF); var webp = At(0, 0x52, 0x49, 0x46, 0x46) && At(8, 0x57, 0x45, 0x42, 0x50);
        var mp4 = At(4, 0x66, 0x74, 0x79, 0x70); var webm = At(0, 0x1A, 0x45, 0xDF, 0xA3);
        var mp3 = At(0, 0x49, 0x44, 0x33) || (b.Length > 1 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0); var wav = At(0, 0x52, 0x49, 0x46, 0x46) && At(8, 0x57, 0x41, 0x56, 0x45); var ogg = At(0, 0x4F, 0x67, 0x67, 0x53);
        return kind switch
        {
            KieKind.Video or KieKind.Avatar => mp4 || webm,
            KieKind.Musik or KieKind.Sprache or KieKind.Geraeusch => mp3 || wav || ogg || mp4,
            _ => png || jpg || webp,
        };
    }

    /// <summary>Ergebnisse nur von öffentlichen https-Adressen laden, nie aus dem eigenen Netz.</summary>
    public static bool PublicHttps(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps || u.IsLoopback || !u.Host.Contains('.')) return false;
        if (System.Net.IPAddress.TryParse(u.Host.Trim('[', ']'), out var ip))
        {
            var a = ip.GetAddressBytes();
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || System.Net.IPAddress.IsLoopback(ip)) return false;
            if (a.Length == 4 && (a[0] == 10 || a[0] == 127 || (a[0] == 172 && a[1] >= 16 && a[1] <= 31) || (a[0] == 192 && a[1] == 168) || (a[0] == 169 && a[1] == 254) || a[0] == 0)) return false;
        }
        return !u.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) && !u.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    public static readonly IReadOnlyList<string> ImageExt = [".png", ".jpg", ".jpeg", ".webp"];
    public static readonly IReadOnlyList<string> AudioExt = [".mp3", ".wav", ".m4a", ".ogg"];

    /// <summary>K4: Welche Modelle die offizielle Übersicht (llms.txt) noch nennt. Fehlt eines, wird es gekennzeichnet statt still zu scheitern.</summary>
    public static IReadOnlyList<string> Missing(string overview) =>
        string.IsNullOrWhiteSpace(overview) ? [] : All.Where(m => !overview.Contains(DocSlug(m.Id), StringComparison.OrdinalIgnoreCase)).Select(m => m.Id).ToList();

    /// <summary>Wie die Übersicht ein Modell nennt (Adresse der Beschreibung), wo die Kennung allein zu allgemein wäre.</summary>
    static string DocSlug(string id) => id switch
    {
        "kling-3.0/video" => "kling-3-0",
        "ai-music-api/generate" => "suno",
        "kling/ai-avatar-standard" => "ai-avatar",
        "elevenlabs/text-to-speech-multilingual-v2" => "multilingual-v2",
        "veo-3-1" => "veo3-api",
        "ai-music-api/sounds" => "generate-sounds",
        _ => id,
    };

    /// <summary>Dateiendung aus Adresse oder Art.</summary>
    public static string Extension(string url, KieKind kind)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.AbsolutePath : url;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".mp4" or ".mov" or ".webm" or ".mp3" or ".wav" or ".m4a" or ".ogg") return ext;
        return kind switch { KieKind.Video or KieKind.Avatar => ".mp4", KieKind.Musik or KieKind.Sprache or KieKind.Geraeusch => ".mp3", _ => ".png" };
    }
}

/// <summary>Herkunftsnachweis je Ergebnis (Datei.json): kie.ai, Modell, Eingaben ohne Schlüssel, Auftrag, Kosten, Prüfsumme.</summary>
public sealed record KieProvenance(string Service, string Model, string Kind, string Prompt, string Title, string Style, string TaskId, double? Credits, DateTime CreatedUtc, string SourceUrl, string Sha256, bool AiGenerated = true)
{
    public static string Write(string file, KieProvenance p)
    {
        using var s = File.OpenRead(file);
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)).ToLowerInvariant();
        var json = file + ".json";
        File.WriteAllText(json, JsonSerializer.Serialize(p with { Sha256 = sha }, new JsonSerializerOptions { WriteIndented = true }));
        return json;
    }
}

/// <summary>S2 (Freigabe 13.11.2026, Anfrageform belegt in docs.kie.ai am 14.11.2026): eigene Stimme für Suno.
/// Ablauf: 1. Aufnahme der eigenen Stimme hochladen → „validation-phrase“ (kostenlos) liefert einen Prüfsatz; der Auftrag bleibt „generating“,
/// bis der Satz da ist. 2. Den Prüfsatz selbst einsprechen oder singen, hochladen → „create-voice“ (kostenlos). 3. Die Stimme ist fertig, wenn der
/// Prüfsatz-Auftrag „success“ meldet (bei „fail“ hat die Aufnahme die Prüfung nicht bestanden). Die Auftragsnummer des Prüfsatzes ist die Stimm-ID
/// (persona_id mit persona_model „voice_persona“ beim Lied). Nur die eigene Stimme – die Einwilligung bestätigt die Person vorher.</summary>
public static class KieVoice
{
    public const string PhraseModel = "ai-music-api/validation-phrase", CreateModel = "ai-music-api/create-voice";
    public static readonly string[] Languages = ["de", "en", "es", "fr", "pt", "ja", "ko", "zh", "hi", "ru"];

    /// <summary>Eingabe für den Prüfsatz. Der Abschnitt mit Gesang muss mindestens 5 und höchstens 60 Sekunden lang sein.</summary>
    public static JsonObject PhraseInput(string voiceUrl, int startS, int endS, string language)
    {
        if (!voiceUrl.StartsWith("https://", StringComparison.Ordinal)) throw new ArgumentException("Die Aufnahme muss zuerst hochgeladen werden.");
        if (startS < 0 || endS <= startS) throw new ArgumentException("Das Ende des Abschnitts muss nach dem Anfang liegen.");
        if (endS - startS < 5) throw new ArgumentException("Der Abschnitt ist zu kurz – bitte mindestens 5 Sekunden.");
        if (endS - startS > 60) throw new ArgumentException("Der Abschnitt ist zu lang – bitte höchstens 60 Sekunden.");
        return new JsonObject { ["voice_url"] = voiceUrl, ["vocal_start_s"] = startS, ["vocal_end_s"] = endS, ["language"] = Languages.Contains(language) ? language : "de" };
    }

    public static JsonObject CreateInput(string phraseTaskId, string verifyUrl, string name, string style)
    {
        if (string.IsNullOrWhiteSpace(phraseTaskId)) throw new ArgumentException("Es fehlt der Prüfsatz-Auftrag.");
        if (!verifyUrl.StartsWith("https://", StringComparison.Ordinal)) throw new ArgumentException("Die Aufnahme des Prüfsatzes muss zuerst hochgeladen werden.");
        return new JsonObject
        {
            ["task_id"] = phraseTaskId, ["verify_url"] = verifyUrl,
            ["voice_name"] = Kreativ.KreativSafe.Cut(string.IsNullOrWhiteSpace(name) ? "Meine Stimme" : name.Trim(), 60),
            ["description"] = "Eigene Stimme, in Trendo OS angelegt", ["style"] = Kreativ.KreativSafe.Cut((style ?? "").Trim(), 200), ["singer_skill_level"] = "beginner",
        };
    }

    /// <summary>Prüfsatz aus dem Auftragsstand (resultJson.data.verify_phrase_text), sonst null.</summary>
    public static string? Phrase(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return null;
        try
        {
            var n = JsonNode.Parse(resultJson);
            var t = n?["data"]?["verify_phrase_text"] ?? n?["verify_phrase_text"];
            return t is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;
        }
        catch (JsonException) { return null; }
    }
}
