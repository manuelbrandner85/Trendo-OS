using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Automation;

/// <summary>n8n in Trendo (Freigabe 06.11.2026, N1–N7): n8n wird nicht mitgeliefert (Sustainable Use License), sondern auf Wunsch aus der offiziellen npm-Quelle
/// in den Trendo-Werkzeugordner geladen und nur auf diesem PC (127.0.0.1) betrieben. Trendo stellt dazu eine OpenAI-kompatible Adresse „Trendo KI“ für alle lokalen
/// KIs bereit, Trendo-Aktionen und -Ereignisse als Bausteine, Vorlagen je Studio, Umwandlung der Trendo-Abläufe und die Prüfung von Abläufen, die Claude baut.</summary>
public static class N8n
{
    public const int Port = 5678, NodeRedPort = 1880, TrendoApiPort = 47800;
    public static string EditorUrl => $"http://127.0.0.1:{Port}/";
    public static string HealthUrl => $"http://127.0.0.1:{Port}/healthz";
    public static string TrendoKiBase => $"http://127.0.0.1:{TrendoApiPort}/v1";
    public static string TrendoApiBase => $"http://127.0.0.1:{TrendoApiPort}/api";

    /// <summary>Einrichten (N1): npm in einen eigenen Ordner, ohne Admin-Rechte, ohne globale Installation.</summary>
    /// <remarks>Node-RED ohne Installationsskripte (--ignore-scripts): echter Lauf 08.11.2026 – Node-RED 5.0.9 läuft ohne sie, und beim Einrichten wird kein fremder Code ausgeführt.
    /// n8n behält die Skripte (seine Datenbank-Bausteine brauchen sie; ohne nicht geprüft).</remarks>
    public static List<string> InstallArgs(string dir, string package = "n8n") =>
        ["install", package + "@latest", "--prefix", dir, "--no-fund", "--no-audit", "--omit=dev", .. package == "node-red" ? new[] { "--ignore-scripts" } : []];
    public static string Exe(string dir, string name = "n8n") => Path.Combine(dir, "node_modules", ".bin", OperatingSystem.IsWindows() ? name + ".cmd" : name);

    /// <summary>Umgebung für den Start: nur 127.0.0.1, eigener Datenordner, keine Telemetrie, keine Werbe-Hinweise.</summary>
    public static Dictionary<string, string> StartEnv(string dataDir) => new()
    {
        ["N8N_PORT"] = Port.ToString(), ["N8N_LISTEN_ADDRESS"] = "127.0.0.1", ["N8N_HOST"] = "127.0.0.1", ["N8N_PROTOCOL"] = "http",
        ["N8N_USER_FOLDER"] = dataDir, ["N8N_DIAGNOSTICS_ENABLED"] = "false", ["N8N_VERSION_NOTIFICATIONS_ENABLED"] = "false",
        ["N8N_PERSONALIZATION_ENABLED"] = "false", ["N8N_HIRING_BANNER_ENABLED"] = "false", ["N8N_SECURE_COOKIE"] = "false",
    };

    // ───────────── Trendo KI (N2) ─────────────
    public sealed record KiBackend(string Id, string Label, string BaseUrl);
    /// <summary>Lokale KIs mit OpenAI-kompatibler Schnittstelle – in dieser Reihenfolge versucht.</summary>
    public static readonly KiBackend[] Backends =
    [
        new("ollama", "Ollama", "http://127.0.0.1:11434/v1"),
        new("lmstudio", "LM Studio", "http://127.0.0.1:1234/v1"),
        new("llamacpp", "llama.cpp-Server", "http://127.0.0.1:8080/v1"),
        new("localai", "LocalAI", "http://127.0.0.1:8081/v1"),
    ];
    /// <summary>Modellname „anbieter/modell“ wählt den Anbieter; ohne Vorsatz der erste erreichbare.</summary>
    public static (KiBackend Backend, string Model)? Route(string model, IReadOnlyCollection<string> available)
    {
        var slash = model.IndexOf('/');
        if (slash > 0 && Backends.FirstOrDefault(b => b.Id == model[..slash]) is { } b0) return available.Contains(b0.Id) ? (b0, model[(slash + 1)..]) : null;
        var first = Backends.FirstOrDefault(b => available.Contains(b.Id));
        return first is null ? null : (first, model);
    }

    /// <summary>Zugang „Trendo KI“ für n8n (Typ openAiApi, Adresse und Schlüssel) – über die n8n-Schnittstelle anlegen.</summary>
    public static JsonObject KiCredential(string token) => new() { ["name"] = "Trendo KI (lokal)", ["type"] = "openAiApi", ["data"] = new JsonObject { ["apiKey"] = token, ["url"] = TrendoKiBase } };

    // ───────────── Trendo-Bausteine und Ereignisse (N3) ─────────────
    public sealed record Action(string Path, string Label, string Body);
    public static readonly Action[] Actions =
    [
        new("/api/actions/notify", "Meldung in Trendo anzeigen", """{"text":"…"}"""),
        new("/api/actions/calendar", "Termin in den Trendo-Kalender", """{"title":"…","start":"2026-11-10T09:00","minutes":60,"remindMin":30}"""),
        new("/api/actions/note", "Notiz in „Wissen“ ablegen (Markdown)", """{"title":"…","text":"…"}"""),
        new("/api/actions/letter-draft", "Brief-Entwurf anlegen (wird nicht verschickt)", """{"recipient":"…","subject":"…","body":"…"}"""),
        new("/api/workflows", "Trendo-Abläufe auflisten (GET)", ""),
        new("/api/workflows/{id}/run", "Trendo-Ablauf starten (nur freigegebene)", """{"dryRun":false}"""),
    ];
    public sealed record Event(string Id, string Label);
    public static readonly Event[] Events =
    [
        new("druck-fertig", "3D-Druck fertig oder fehlgeschlagen"), new("frist-bald", "Frist oder Wiedervorlage in den nächsten 3 Tagen"),
        new("ablauf-fertig", "Ein Trendo-Ablauf ist gelaufen"), new("test", "Probe-Ereignis (Knopf in Trendo)"),
    ];

    // ───────────── Vorlagen je Studio (N4) ─────────────
    public sealed record Template(string Id, string Studio, string Name, string Hint, bool External, Func<string, JsonObject> Build);

    static JsonObject Node(string name, string type, double version, JsonObject parameters, int x) =>
        new() { ["parameters"] = parameters, ["name"] = name, ["type"] = type, ["typeVersion"] = version, ["position"] = new JsonArray(x, 300) };
    static JsonObject Http(string name, string method, string url, string? token, string jsonBodyExpr, int x)
    {
        var p = new JsonObject { ["method"] = method, ["url"] = url };
        if (token is not null) { p["sendHeaders"] = true; p["headerParameters"] = new JsonObject { ["parameters"] = new JsonArray(new JsonObject { ["name"] = "Authorization", ["value"] = "Bearer " + token }) }; }
        if (jsonBodyExpr.Length > 0) { p["sendBody"] = true; p["specifyBody"] = "json"; p["jsonBody"] = jsonBodyExpr; }
        return Node(name, "n8n-nodes-base.httpRequest", 4.2, p, x);
    }
    static JsonObject Chain(string name, params JsonObject[] nodes)
    {
        var con = new JsonObject();
        for (var i = 0; i + 1 < nodes.Length; i++)
            con[nodes[i]["name"]!.GetValue<string>()] = new JsonObject { ["main"] = new JsonArray(new JsonArray(new JsonObject { ["node"] = nodes[i + 1]["name"]!.GetValue<string>(), ["type"] = "main", ["index"] = 0 })) };
        return new JsonObject { ["name"] = name, ["nodes"] = new JsonArray([.. nodes]), ["connections"] = con, ["settings"] = new JsonObject(), ["active"] = false };
    }
    static string Ki(string prompt) => "={{ JSON.stringify({ model: 'ollama/llama3.2', messages: [{ role: 'user', content: " + prompt + " }] }) }}";

    public static readonly Template[] Templates =
    [
        new("mail-sortieren", "Büro", "Posteingang mit lokaler KI sortieren", "Neue Mails (dein Postfach in n8n eintragen) → lokale KI bewertet Wichtigkeit und schreibt einen Antwortentwurf → Meldung in Trendo. Verschickt wird nichts.", false,
            t => Chain("Posteingang sortieren (Trendo)",
                Node("Neue Mail", "n8n-nodes-base.emailReadImap", 2, new JsonObject { ["mailbox"] = "INBOX" }, 0),
                Http("Trendo KI bewertet", "POST", TrendoKiBase + "/chat/completions", t, Ki("'Wie wichtig ist diese Mail (hoch/mittel/niedrig) und ein kurzer, höflicher Antwortentwurf auf Deutsch:\\n' + $json.subject + '\\n' + ($json.textPlain || '').slice(0, 4000)"), 260),
                Http("Meldung in Trendo", "POST", TrendoApiBase + "/actions/notify", t, "={{ JSON.stringify({ text: 'Mail bewertet: ' + $json.choices[0].message.content.slice(0, 300) }) }}", 520))),
        new("rechnung-termin", "Business", "Rechnungen aus Mails als Zahltermin", "Mails mit „Rechnung“ → lokale KI liest Betrag und Fälligkeit → Termin im Trendo-Kalender.", false,
            t => Chain("Rechnung → Zahltermin (Trendo)",
                Node("Neue Mail", "n8n-nodes-base.emailReadImap", 2, new JsonObject { ["mailbox"] = "INBOX", ["options"] = new JsonObject { ["customEmailConfig"] = "[\"UNSEEN\", [\"SUBJECT\", \"Rechnung\"]]" } }, 0),
                Http("Trendo KI liest", "POST", TrendoKiBase + "/chat/completions", t, Ki("'Antworte nur mit JSON {\"betrag\":\"…\",\"faellig\":\"JJJJ-MM-TT\"} zu dieser Rechnung:\\n' + ($json.textPlain || '').slice(0, 4000)"), 260),
                Http("Zahltermin", "POST", TrendoApiBase + "/actions/calendar", t, "={{ (() => { const r = JSON.parse($json.choices[0].message.content); return JSON.stringify({ title: 'Rechnung zahlen: ' + r.betrag, start: r.faellig + 'T09:00', minutes: 15, remindMin: 1440 }); })() }}", 520))),
        new("druck-handy", "Technik", "3D-Druck fertig → Nachricht aufs Handy", "Trendo meldet „Druck fertig“ → n8n schickt eine Nachricht über ntfy (eigenes Thema eintragen). Wirkt nach außen.", true,
            t => Chain("Druck fertig → Handy (Trendo)",
                Node("Trendo: Druck fertig", "n8n-nodes-base.webhook", 2, new JsonObject { ["httpMethod"] = "POST", ["path"] = "trendo-druck-fertig" }, 0),
                Http("Nachricht (ntfy)", "POST", "https://ntfy.sh/DEIN-THEMA", null, "={{ JSON.stringify({ message: $json.body.text }) }}", 260))),
        new("feed-zusammenfassen", "Wissen", "Nachrichten-Feed täglich zusammenfassen", "Jeden Morgen einen RSS-Feed lesen → lokale KI fasst zusammen → Notiz in „Wissen“.", false,
            t => Chain("Feed-Zusammenfassung (Trendo)",
                Node("Jeden Morgen", "n8n-nodes-base.scheduleTrigger", 1.2, new JsonObject { ["rule"] = new JsonObject { ["interval"] = new JsonArray(new JsonObject { ["field"] = "hours", ["hoursInterval"] = 24 }) } }, 0),
                Node("Feed lesen", "n8n-nodes-base.rssFeedRead", 1.1, new JsonObject { ["url"] = "https://www.tagesschau.de/xml/rss2/" }, 200),
                Http("Trendo KI fasst zusammen", "POST", TrendoKiBase + "/chat/completions", t, Ki("'Fasse in 5 Stichpunkten auf Deutsch zusammen:\\n' + $input.all().slice(0, 15).map(i => i.json.title).join('\\n')"), 400),
                Http("Notiz in Wissen", "POST", TrendoApiBase + "/actions/note", t, "={{ JSON.stringify({ title: 'Nachrichten ' + $now.toFormat('dd.MM.yyyy'), text: $json.choices[0].message.content }) }}", 600))),
        new("frist-erinnerung", "Büro", "Frist bald → Erinnerung in Trendo", "Trendo meldet Fristen und Wiedervorlagen der nächsten 3 Tage → Meldung (hier kannst du weitere Wege anhängen).", false,
            t => Chain("Frist-Erinnerung (Trendo)",
                Node("Trendo: Frist bald", "n8n-nodes-base.webhook", 2, new JsonObject { ["httpMethod"] = "POST", ["path"] = "trendo-frist-bald" }, 0),
                Http("Meldung in Trendo", "POST", TrendoApiBase + "/actions/notify", t, "={{ JSON.stringify({ text: 'Bald fällig: ' + $json.body.text }) }}", 260))),
        new("fotos-ordnen", "Kreativ", "Neue Fotos → Trendo-Ablauf „nach Datum ordnen“", "Neue Datei im Foto-Ordner → startet deinen freigegebenen Trendo-Ablauf.", false,
            t => Chain("Neue Fotos ordnen (Trendo)",
                Node("Neue Datei", "n8n-nodes-base.localFileTrigger", 1, new JsonObject { ["triggerOn"] = "folder", ["path"] = "C:/Bilder/Eingang", ["events"] = new JsonArray("add") }, 0),
                Http("Trendo-Ablauf starten", "POST", TrendoApiBase + "/workflows/ABLAUF-ID/run", t, "={{ JSON.stringify({ dryRun: false }) }}", 260))),
    ];

    // ───────────── Austausch mit Trendo-Abläufen (N6) ─────────────
    /// <summary>Trendo-Ablauf als n8n-Ablauf: gleicher Auslöser, ruft den (freigegebenen) Trendo-Ablauf über die lokale Schnittstelle auf.</summary>
    public static JsonObject FromTrendo(Workflow w, string token)
    {
        JsonObject trigger = w.Trigger switch
        {
            TriggerKind.Taeglich => Node("Täglich " + w.At, "n8n-nodes-base.scheduleTrigger", 1.2, new JsonObject { ["rule"] = new JsonObject { ["interval"] = new JsonArray(new JsonObject { ["field"] = "days", ["triggerAtHour"] = int.TryParse(w.At.Split(':')[0], out var h) ? h : 8 }) } }, 0),
            TriggerKind.Intervall => Node($"Alle {w.EveryMinutes} Minuten", "n8n-nodes-base.scheduleTrigger", 1.2, new JsonObject { ["rule"] = new JsonObject { ["interval"] = new JsonArray(new JsonObject { ["field"] = "minutes", ["minutesInterval"] = w.EveryMinutes }) } }, 0),
            TriggerKind.NeueDatei => Node("Neue Datei", "n8n-nodes-base.localFileTrigger", 1, new JsonObject { ["triggerOn"] = "folder", ["path"] = w.WatchFolder, ["events"] = new JsonArray("add") }, 0),
            _ => Node("Von Hand starten", "n8n-nodes-base.manualTrigger", 1, [], 0),
        };
        return Chain(w.Name + " (aus Trendo)", trigger, Http("Trendo-Ablauf „" + w.Name + "“", "POST", $"{TrendoApiBase}/workflows/{w.Id}/run", token, "={{ JSON.stringify({ dryRun: false }) }}", 260));
    }

    // ───────────── Abläufe prüfen (N5, auch für Claude) ─────────────
    public sealed record Review(string Name, IReadOnlyList<(string Name, string Type)> Nodes, IReadOnlyList<string> External, IReadOnlyList<string> Problems);
    static readonly string[] ExternalTypes = ["email", "gmail", "telegram", "slack", "discord", "twitter", "whatsapp", "ntfy", "pushover", "sms", "twilio", "ftp", "ssh", "googledrive", "dropbox", "notion", "airtable", "hubspot", "stripe", "paypal", "openai", "anthropic"];

    /// <summary>n8n-Ablauf lesen: Bausteine, was nach außen wirkt (Versand, fremde Dienste, Adressen außerhalb dieses PCs), Fehler im Aufbau.</summary>
    public static Review Check(string json)
    {
        JsonNode? root; try { root = JsonNode.Parse(json); } catch (JsonException e) { return new("", [], [], ["Kein gültiger Ablauf (JSON): " + e.Message]); }
        if (root is not JsonObject o || o["nodes"] is not JsonArray nodes) return new("", [], [], ["Im Ablauf fehlen die Bausteine („nodes“)."]);
        var list = nodes.OfType<JsonObject>().Select(n => (Name: n["name"]?.GetValue<string>() ?? "", Type: n["type"]?.GetValue<string>() ?? "")).ToList();
        var ext = new List<string>(); var probs = new List<string>();
        foreach (var n in nodes.OfType<JsonObject>())
        {
            var type = (n["type"]?.GetValue<string>() ?? "").ToLowerInvariant(); var name = n["name"]?.GetValue<string>() ?? "";
            if (ExternalTypes.Any(e => type.Contains(e)) && !type.Contains("trigger") && !type.Contains("read")) ext.Add($"„{name}“ ({type.Split('.').Last()}) sendet an einen fremden Dienst");
            if (type.EndsWith("httprequest") && n["parameters"]?["url"]?.ToString() is { } url && !url.Contains("127.0.0.1") && !url.Contains("localhost")) ext.Add($"„{name}“ ruft eine Adresse außerhalb dieses PCs auf: {url}");
            if (type.Contains("executecommand")) probs.Add($"„{name}“ führt Befehle auf dem PC aus – in Trendo-Vorlagen nicht erlaubt, bitte genau prüfen");
        }
        if (list.Count == 0) probs.Add("Der Ablauf hat keine Bausteine.");
        if (list.Select(l => l.Name).Distinct().Count() != list.Count) probs.Add("Zwei Bausteine heißen gleich – n8n braucht eindeutige Namen.");
        return new(o["name"]?.GetValue<string>() ?? "Ablauf", list, ext, probs);
    }

    // ───────────── Ereignisse an n8n (N3) ─────────────
    public sealed record Subscription(string Event, string Url);
    /// <summary>Nur Webhooks auf diesem PC (n8n oder Node-RED) – Ereignisse verlassen den PC nie direkt.</summary>
    public static bool LocalWebhook(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "http" && u.Host is "127.0.0.1" or "localhost";

    public sealed class Hooks(string dataRoot)
    {
        string FileOf => Path.Combine(dataRoot, "automation", "ereignisse.json");
        public List<Subscription> List() { try { return File.Exists(FileOf) ? JsonSerializer.Deserialize<List<Subscription>>(File.ReadAllText(FileOf)) ?? [] : []; } catch (JsonException) { return []; } }
        public void Add(string ev, string url)
        {
            if (!Events.Any(e => e.Id == ev)) throw new ArgumentException("Unbekanntes Ereignis.");
            if (!LocalWebhook(url)) throw new ArgumentException("Nur Webhooks auf diesem PC (http://127.0.0.1:…) – Ereignisse gehen nie direkt ins Internet.");
            var l = List().Where(s => !(s.Event == ev && s.Url == url)).Append(new Subscription(ev, url)).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(FileOf)!); File.WriteAllText(FileOf, JsonSerializer.Serialize(l));
        }
        public void Remove(string ev, string url) { var l = List().Where(s => !(s.Event == ev && s.Url == url)).ToList(); Directory.CreateDirectory(Path.GetDirectoryName(FileOf)!); File.WriteAllText(FileOf, JsonSerializer.Serialize(l)); }
        public IEnumerable<string> For(string ev) => List().Where(s => s.Event == ev).Select(s => s.Url);
    }

    /// <summary>Webhook-Adresse einer Vorlage mit Webhook-Auslöser (für das Abonnieren nach dem Import).</summary>
    public static string WebhookUrl(string path) => $"http://127.0.0.1:{Port}/webhook/{path}";

    // ───────────── Node-RED (N7, frei, darf mitgeliefert werden) ─────────────
    public const string NodeRedLicense = "Apache-2.0";
    public static Dictionary<string, string> NodeRedEnv(string dataDir) => new() { ["NODE_RED_ENABLE_PROJECTS"] = "false" };
    public static List<string> NodeRedArgs(string dataDir) => ["--userDir", dataDir, "--port", NodeRedPort.ToString(), "--settings", Path.Combine(dataDir, "settings.js")];
    /// <summary>Node-RED nur auf 127.0.0.1 (eigene Einstellungsdatei). Dazu eine Herkunftsprüfung für Verwaltung und eigene Ablauf-Adressen (08.11.2026):
    /// nur Anfragen an 127.0.0.1/localhost und – falls der Browser eine Herkunft mitschickt – nur von dort. So können fremde Webseiten (auch per DNS-Rebinding)
    /// keine Abläufe mit eigenem Code einschleusen; der Editor in Trendo bleibt ohne Anmeldung bedienbar.</summary>
    public const string NodeRedSettings = """
        const lokal = /^(127\.0\.0\.1|localhost)(:\d+)?$/i;
        const lokaleHerkunft = /^https?:\/\/(127\.0\.0\.1|localhost)(:\d+)?$/i;
        function nurVonDiesemPc(req, res, next) {
            if (!lokal.test(req.headers.host || "")) { res.status(403).send("Node-RED in Trendo: nur von diesem PC."); return; }
            const herkunft = req.headers.origin;
            if (herkunft && herkunft !== "null" && !lokaleHerkunft.test(herkunft)) { res.status(403).send("Node-RED in Trendo: fremde Webseite abgewiesen."); return; }
            if (herkunft === "null") { res.status(403).send("Node-RED in Trendo: unbekannte Herkunft abgewiesen."); return; }
            next();
        }
        module.exports = { uiHost: "127.0.0.1", uiPort: 1880, diagnostics: { enabled: false }, telemetry: { enabled: false },
            httpAdminMiddleware: nurVonDiesemPc, httpNodeMiddleware: nurVonDiesemPc };

        """;
}
