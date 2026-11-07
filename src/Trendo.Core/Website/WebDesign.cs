using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Web Design Studio (Freigabe 07.11.2026): visueller Seiten-Editor im Studio „Webseite bauen“ (CORE-07).
/// Der Editor (Puck, MIT; 3D mit React Three Fiber, MIT) liegt als fertiges Bündel im Programm (<c>Assets/webdesign</c>) und läuft im eingebetteten WebView.
/// Hier: eine Datenquelle je Projekt (<c>seite.trendo.json</c>), sicheres Speichern mit Sicherung, Wiederherstellen und Export als fertige, offline lauffähige Webseite.</summary>
public static partial class WebDesign
{
    public const string PageFile = "seite.trendo.json";
    public const string BackupFile = "seite.trendo.json.bak";
    public const string ExportDir = "veroeffentlichen";
    /// <summary>Größte erlaubte Seite (Schutz vor versehentlich riesigen Daten im Editor).</summary>
    public const int MaxBytes = 8 * 1024 * 1024;

    public static string Empty(string title = "Neue Seite") =>
        new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject { ["title"] = title, ["description"] = "" } }, ["content"] = new JsonArray(), ["zones"] = new JsonObject() }.ToJsonString();

    /// <summary>Prüft Editor-Daten: JSON-Objekt mit „root“ und Liste „content“, jeder Baustein mit „type“; liefert die bereinigte Fassung oder einen Fehlertext.</summary>
    public static (string? Json, string? Error) Validate(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, "Keine Seitendaten.");
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) return (null, "Seite ist zu groß (über 8 MB).");
        JsonNode? n; try { n = JsonNode.Parse(json); } catch (JsonException) { return (null, "Seitendaten sind kein gültiges JSON."); }
        if (n is not JsonObject o || o["root"] is not JsonObject || o["content"] is not JsonArray content) return (null, "Seitendaten unvollständig (root/content fehlt).");
        foreach (var c in content) if (c is not JsonObject co || string.IsNullOrWhiteSpace(co["type"]?.ToString())) return (null, "Ein Baustein hat keinen Typ.");
        Sanitize(o);
        SanitizeTokens(o["root"]?["props"] as JsonObject);
        return (o.ToJsonString(), null);
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")] private static partial Regex HexColor();
    public static readonly string[] Fonts = ["klar", "serif", "rund", "mono"];

    /// <summary>Modul 05 Design-Tokens: nur #RRGGBB-Farben, bekannte Schriften, Rundung 0–40 und Abstand 4–48 px – sonst Standardwert. So kann kein fremder CSS-Code über die Seiteneinstellungen in die Seite gelangen.</summary>
    static void SanitizeTokens(JsonObject? p)
    {
        if (p is null) return;
        foreach (var (k, d) in new[] { ("accent", "#1d1d1f"), ("ink", "#1d1d1f"), ("paper", "#ffffff") })
            if (p[k] is not null && !(p[k] is JsonValue v && v.TryGetValue(out string? s) && HexColor().IsMatch(s))) p[k] = d;
        if (p["font"] is not null && !(p["font"] is JsonValue f && f.TryGetValue(out string? fs) && Fonts.Contains(fs))) p["font"] = "klar";
        static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue(out double d) ? d : n is JsonValue s && s.TryGetValue(out string? t) && double.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var x) ? x : null;
        if (p["radius"] is not null) p["radius"] = Math.Clamp(Num(p["radius"]) ?? 12, 0, 40);
        if (p["space"] is not null) p["space"] = Math.Clamp(Num(p["space"]) ?? 24, 4, 48);
    }

    [GeneratedRegex(@"^\s*(javascript|vbscript|data)\s*:", RegexOptions.IgnoreCase)] private static partial Regex DangerousUrl();

    /// <summary>Gefährliche Links (javascript:, vbscript:, data:) aus Link-Feldern entfernen – die fertige Seite soll niemanden angreifen können.</summary>
    static void Sanitize(JsonNode? n)
    {
        switch (n)
        {
            case JsonObject o:
                foreach (var key in o.Select(kv => kv.Key).ToList())
                    if (key is "href" or "src" && o[key] is JsonValue v && v.TryGetValue(out string? s) && DangerousUrl().IsMatch(s)) o[key] = "#";
                    else Sanitize(o[key]);
                break;
            case JsonArray a: foreach (var x in a) Sanitize(x); break;
        }
    }

    public static string Load(string projectDir)
    {
        var f = Path.Combine(projectDir, PageFile);
        if (File.Exists(f) && Validate(File.ReadAllText(f)) is { Json: { } ok }) return ok;
        var b = Path.Combine(projectDir, BackupFile); // Wiederherstellung: beschädigte Datei → letzte gute Fassung
        if (File.Exists(b) && Validate(File.ReadAllText(b)) is { Json: { } back }) return back;
        return Empty(Path.GetFileName(projectDir));
    }

    /// <summary>Speichert atomar (erst Zwischendatei, dann ersetzen) und hält die vorige Fassung als Sicherung.</summary>
    public static string? Save(string projectDir, string json)
    {
        var (ok, err) = Validate(json); if (ok is null) return err;
        Directory.CreateDirectory(projectDir);
        var f = Path.Combine(projectDir, PageFile); var tmp = f + ".neu";
        File.WriteAllText(tmp, ok);
        if (File.Exists(f)) File.Copy(f, Path.Combine(projectDir, BackupFile), true);
        File.Move(tmp, f, true);
        return null;
    }

    /// <summary>Exportiert die gespeicherte Seite als fertige Webseite: index.html, seite.js (Daten) und das Editor-Bündel im Anzeigemodus – läuft ohne Netz direkt aus dem Ordner.</summary>
    public static (string? Dir, string? Error) Export(string projectDir, string editorDir)
    {
        var js = Path.Combine(editorDir, "assets", "editor.js"); var css = Path.Combine(editorDir, "assets", "style.css");
        if (!File.Exists(js) || !File.Exists(css)) return (null, "Editor-Bündel fehlt im Programm (Assets/webdesign).");
        var (data, err) = Validate(Load(projectDir)); if (data is null) return (null, err);
        var outDir = Path.Combine(projectDir, ExportDir); var assets = Path.Combine(outDir, "assets"); Directory.CreateDirectory(assets);
        File.Copy(js, Path.Combine(assets, "editor.js"), true); File.Copy(css, Path.Combine(assets, "style.css"), true);
        var lic = Path.Combine(editorDir, "LIZENZEN.txt"); // MIT & Co. verlangen den Urheberhinweis auch in jeder weitergegebenen Seite
        if (File.Exists(lic)) File.Copy(lic, Path.Combine(assets, "LIZENZEN.txt"), true);
        var title = (JsonNode.Parse(data)?["root"]?["props"]?["title"]?.ToString() ?? "Webseite") // nur HTML-Sonderzeichen maskieren, Umlaute bleiben lesbar (UTF-8)
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        // Daten als Skript (fetch ist über file:// gesperrt); „</“ entschärfen, damit kein Text das Skript beenden kann
        File.WriteAllText(Path.Combine(outDir, "seite.js"), "window.TRENDO_SEITE = " + data.Replace("</", "<\\/") + ";\n");
        File.WriteAllText(Path.Combine(outDir, "index.html"),
            "<!doctype html>\n<html lang=\"de\">\n<head>\n<meta charset=\"UTF-8\" />\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n" +
            $"<title>{title}</title>\n<link rel=\"stylesheet\" href=\"assets/style.css\">\n<script src=\"seite.js\"></script>\n<script defer src=\"assets/editor.js\"></script>\n</head>\n<body>\n<div id=\"root\"></div>\n" +
            "<noscript>Diese Seite braucht JavaScript.</noscript>\n</body>\n</html>\n");
        return (outDir, null);
    }

    /// <summary>Nachricht vom Editor („bereit“, „geaendert“, „speichern“ mit Daten).</summary>
    public sealed record Message(string Kind, string? Data);
    public static Message? ParseMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            var n = JsonNode.Parse(body.Length > 1 && body[0] == '"' ? JsonNode.Parse(body)!.ToString() : body) as JsonObject; // manche WebViews liefern die Nachricht als JSON-String
            var kind = n?["art"]?.ToString(); if (string.IsNullOrEmpty(kind)) return null;
            return new(kind, n!["daten"]?.ToJsonString());
        }
        catch (JsonException) { return null; }
    }
}
