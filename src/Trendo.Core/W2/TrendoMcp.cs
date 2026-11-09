using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Ergebnis eines Trendo-Werkzeugs für Claude: Text, optional ein Bild (PNG).</summary>
public sealed record McpResult(string Text, byte[]? Png = null, bool Error = false)
{
    public static McpResult Fail(string text) => new(text, null, true);
}

public sealed record McpTool(string Name, string Description, JsonObject Schema, Func<JsonObject, Task<McpResult>> Run);

/// <summary>Protokoll zwischen Claude Code und Trendo (MCP über JSON-RPC). Hier steht nur das Protokoll; die Werkzeuge kommen aus der App, das Netz aus Trendo.Api.</summary>
public sealed class TrendoMcp(IReadOnlyList<McpTool> tools)
{
    public const string ServerName = "trendo";
    /// <summary>Freigabe für Claude Code: alle Werkzeuge dieses Servers.</summary>
    public const string AllowedTools = "mcp__trendo";
    const string DefaultProtocol = "2025-06-18";

    public IReadOnlyList<McpTool> Tools => tools;

    /// <summary>Eine Anfrage beantworten. null = Benachrichtigung, darauf gibt es keine Antwort.</summary>
    public async Task<JsonObject?> HandleAsync(JsonNode? request)
    {
        if (request is not JsonObject r || r["method"]?.GetValueKind() != JsonValueKind.String) return Error(null, -32600, "Ungültige Anfrage");
        var method = r["method"]!.GetValue<string>();
        var id = r["id"]?.DeepClone();
        if (id is null) return null;
        switch (method)
        {
            case "initialize":
                var version = r["params"]?["protocolVersion"]?.GetValueKind() == JsonValueKind.String ? r["params"]!["protocolVersion"]!.GetValue<string>() : DefaultProtocol;
                return Ok(id, new JsonObject { ["protocolVersion"] = version, ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() }, ["serverInfo"] = new JsonObject { ["name"] = ServerName, ["version"] = "1.0" } });
            case "ping":
                return Ok(id, new JsonObject());
            case "tools/list":
                return Ok(id, new JsonObject { ["tools"] = new JsonArray(tools.Select(t => (JsonNode)new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["inputSchema"] = t.Schema.DeepClone() }).ToArray()) });
            case "tools/call":
                var name = r["params"]?["name"]?.GetValueKind() == JsonValueKind.String ? r["params"]!["name"]!.GetValue<string>() : "";
                if (tools.FirstOrDefault(t => t.Name == name) is not { } tool) return Error(id, -32602, "Unbekanntes Werkzeug: " + name);
                var args = r["params"]?["arguments"] as JsonObject ?? [];
                McpResult result;
                try { result = await tool.Run(args); }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or FormatException or KeyNotFoundException or JsonException) { result = McpResult.Fail(e.Message); }
                return Ok(id, Content(result));
            default:
                return Error(id, -32601, "Unbekannte Methode: " + method);
        }
    }

    static JsonObject Content(McpResult r)
    {
        var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = r.Text } };
        if (r.Png is { Length: > 0 } png) content.Add(new JsonObject { ["type"] = "image", ["data"] = Convert.ToBase64String(png), ["mimeType"] = "image/png" });
        return new JsonObject { ["content"] = content, ["isError"] = r.Error };
    }

    static JsonObject Ok(JsonNode id, JsonObject result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
    static JsonObject Error(JsonNode? id, int code, string message) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

    /// <summary>Schema für Werkzeug-Argumente: (Name, Typ, Beschreibung, Pflicht). Typ "any" lässt jeden JSON-Wert zu.</summary>
    public static JsonObject Schema(params (string Name, string Type, string Text, bool Required)[] props)
    {
        var p = new JsonObject();
        foreach (var x in props)
        {
            var one = new JsonObject { ["description"] = x.Text };
            if (x.Type != "any") one["type"] = x.Type;
            p[x.Name] = one;
        }
        return new JsonObject { ["type"] = "object", ["properties"] = p, ["required"] = new JsonArray(props.Where(x => x.Required).Select(x => (JsonNode)x.Name).ToArray()) };
    }

    /// <summary>Teil der Anweisung an Claude, wenn es Programme selbst bedient.</summary>
    public const string FinishMark = "TRENDO_FERTIG";
    public const string HandsFreeRule =
        "\n\nDU ARBEITEST SELBSTÄNDIG. Der Nutzer klickt nichts und bestätigt nichts. Führe den Auftrag vollständig aus, so wie Claude Code es täte, und liefere ein fertiges, geprüftes Ergebnis statt eines Vorschlags.\n" +
        "- Du bedienst Trendo mit den Werkzeugen des Servers „trendo“: `programme`, `programm_oeffnen`, `seite_lesen`, `befehle`, `befehl`, `werkzeug`, `feld_setzen`, `auswaehlen`, `klicken`, `dokument_lesen`, `dokument_setzen`, `exportieren`, `speichern`, `rueckgaengig`, `bild_ansehen`, `studio_wissen`, `programme_pruefen`, `projekt_kontext`, `projekt_merken`, `uebergeben`, `uebergaben`, `uebernehmen`. Schreibe keine Zeilen der Form [[trendo:…]].\n" +
        "- Lies zu Beginn `projekt_kontext` und `studio_wissen` und berücksichtige frühere Entscheidungen, statt von vorn zu beginnen. Halte wichtige Entscheidungen, Probleme und Prüfergebnisse mit `projekt_merken` fest.\n" +
        "- Prüfe mit `programme_pruefen`, bevor du ein Fachprogramm brauchst. Fehlt es oder startet es nicht, sage das offen und behaupte keinen Erfolg.\n" +
        "- Braucht der Auftrag mehrere Studios, arbeite sie nacheinander ab. Gib Ergebnisse als Datei im Projektordner mit `uebergeben` weiter und nimm sie im nächsten Studio mit `uebernehmen` an.\n" +
        "- Vorgehen: Aufgabenliste anlegen, Programm öffnen, `befehle` und `dokument_lesen` ansehen, Schritt für Schritt arbeiten, nach jedem größeren Schritt mit `seite_lesen` oder `bild_ansehen` prüfen, Fehler selbst beheben, zum Schluss speichern.\n" +
        "- Befehle stehen dir auf allen Bedienstufen zur Verfügung, auch die im Menü versteckten. Das Dokument kannst du bis in jedes Feld lesen und ändern.\n" +
        "- Keine Platzhalter und keine halben Sachen. Triff vernünftige Entscheidungen selbst. Frage nur, wenn der Auftrag ohne die Antwort gar nicht umsetzbar ist.\n" +
        "- Antwortet ein Werkzeug „Nicht bestätigt“, hat der Nutzer das abgelehnt. Versuche es dann nicht auf anderem Weg.\n" +
        "- Schreibe als allerletzte Zeile genau " + FinishMark + ", aber nur wenn wirklich alles fertig und geprüft ist. Fehlt noch etwas, lass die Zeile weg, dann bekommst du einen weiteren Durchgang.";

    public static bool IsFinished(string answer) => answer.Contains(FinishMark, StringComparison.Ordinal);
    public static string StripFinish(string answer) => answer.Replace(FinishMark, "", StringComparison.Ordinal).TrimEnd();
    public const string ContinuePrompt = "Der Auftrag ist noch nicht als fertig gemeldet. Prüfe, was fehlt, und arbeite ohne Rückfrage weiter, bis alles fertig und geprüft ist.";
}

/// <summary>Zugriff auf einzelne Stellen eines Dokuments mit Pfaden wie „levels/0/name“.</summary>
public static class DocPath
{
    static string[] Parts(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static JsonNode? Get(JsonObject root, string? path)
    {
        JsonNode? n = root;
        foreach (var p in Parts(path ?? ""))
            n = n switch
            {
                JsonObject o => o[p],
                JsonArray a when int.TryParse(p, out var i) && i >= 0 && i < a.Count => a[i],
                _ => null,
            };
        return n;
    }

    /// <summary>Wert setzen. Fehlende Objekte entstehen unterwegs. Ein Array-Index gleich der Länge hängt an.</summary>
    public static void Set(JsonObject root, string path, JsonNode? value)
    {
        var parts = Parts(path);
        if (parts.Length == 0) throw new ArgumentException("Der Pfad darf nicht leer sein.");
        JsonNode node = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o[parts[i]] is not { } next)
                    {
                        if (int.TryParse(parts[i + 1], out _)) throw new ArgumentException($"Die Stelle „{parts[i]}“ gibt es nicht."); // eine Liste entsteht nicht nebenbei
                        o[parts[i]] = next = new JsonObject();
                    }
                    node = next; break;
                case JsonArray a when int.TryParse(parts[i], out var k) && k >= 0 && k < a.Count && a[k] is { } item:
                    node = item; break;
                default: throw new ArgumentException($"Die Stelle „{parts[i]}“ gibt es nicht.");
            }
        }
        var last = parts[^1];
        switch (node)
        {
            case JsonObject o: o[last] = value; break;
            case JsonArray a when int.TryParse(last, out var k) && k >= 0 && k < a.Count: a[k] = value; break;
            case JsonArray a when int.TryParse(last, out var k) && k == a.Count: a.Add(value); break;
            default: throw new ArgumentException($"Die Stelle „{last}“ gibt es nicht.");
        }
    }

    /// <summary>Werte kommen von Claude manchmal als Text mit JSON darin an („\"#FF0000\"“ oder „[1,2]“). Nur vollständig gültiges JSON wird ausgepackt, sonst bleibt der Text, wie er ist.</summary>
    public static JsonNode? Unwrap(JsonNode? value)
    {
        if (value is not JsonValue v || v.GetValueKind() != JsonValueKind.String) return value;
        var text = v.GetValue<string>().Trim();
        if (text.Length < 2 || !((text[0] == '"' && text[^1] == '"') || (text[0] == '[' && text[^1] == ']') || (text[0] == '{' && text[^1] == '}'))) return value;
        try { return JsonNode.Parse(text); }
        catch (JsonException) { return value; }
    }

    public static bool Remove(JsonObject root, string path)
    {
        var parts = Parts(path);
        if (parts.Length == 0) throw new ArgumentException("Der Pfad darf nicht leer sein.");
        var parent = Get(root, string.Join('/', parts[..^1]));
        switch (parent)
        {
            case JsonObject o: return o.Remove(parts[^1]);
            case JsonArray a when int.TryParse(parts[^1], out var k) && k >= 0 && k < a.Count: a.RemoveAt(k); return true;
            default: return false;
        }
    }
}

public static partial class ClaudeActions
{
    [GeneratedRegex(@"senden|verschick|kauf|bezahl|bestell|zahlung|überweis|veröffentlich|hochlad|posten|\bteilen\b|installier|formatier|lösch|entfern|leeren|zurücksetz|überschreib|abschick|ausführen|einricht|aktualisier|update|einwillig|papierkorb|drucken|druck starten|maschine|abmeld|freigeb|freigabe|genehmig|einverstanden|zustimm|vertrauen|approve|mikrofon|not-aus|notaus|verbinden mit", RegexOptions.IgnoreCase)]
    private static partial Regex ConsentRx();
    [GeneratedRegex(@"lösch|entfern|leeren", RegexOptions.IgnoreCase)]
    private static partial Regex DeleteRx();
    [GeneratedRegex(@"datei|projekt|dokument|programm|papierkorb|\balle|endgültig|konto", RegexOptions.IgnoreCase)]
    private static partial Regex WholeThingRx();

    /// <summary>Muss der Nutzer im Chat zustimmen? Nur bei Wirkung nach außen oder wenn sich etwas nicht zurückholen lässt.
    /// Etwas innerhalb eines Dokuments zu löschen (Ebene, Stab, Zeile) lässt sich rückgängig machen und läuft deshalb ohne Nachfrage.</summary>
    public static bool NeedsConsent(string label, string name, bool undoableDocument)
    {
        var text = label + " " + name;
        if (!ConsentRx().IsMatch(text)) return false;
        var onlyDelete = DeleteRx().IsMatch(text) && !ConsentRx().IsMatch(DeleteRx().Replace(text, " "));
        return !(undoableDocument && onlyDelete && !WholeThingRx().IsMatch(text));
    }
}
