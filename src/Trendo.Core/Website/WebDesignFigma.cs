using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Z8 mit Figma: Inhalte einer eigenen Figma-Datei als Bausteine übernehmen. Nur mit dem eigenen Konto und Schlüssel des Nutzers; der Schlüssel wird nicht gespeichert
/// (die App fragt ihn je Import ab und vergisst ihn danach). Hier stehen nur Prüfung des Links und die Abbildung der Datei-Struktur; der Abruf liegt in der App.
/// Abgebildet werden Rahmen (Frames) als Bereiche, Texte als Überschrift oder Absatz (nach Schriftgröße) und Flächen mit Bildfüllung als Bilder.</summary>
public static partial class WebDesignFigma
{
    public const string ApiHost = "api.figma.com";
    public const int MaxNodes = 5000, MaxImages = 40;

    [GeneratedRegex(@"^https://(www\.)?figma\.com/(file|design)/([A-Za-z0-9]{10,64})(/|\?|$)")] private static partial Regex LinkPattern();
    [GeneratedRegex("^[0-9]+[:-][0-9]+(;[0-9]+[:-][0-9]+)*$")] private static partial Regex NodeIdPattern();

    /// <summary>Datei-Schlüssel aus einem Figma-Link (nur figma.com, nur https), sonst Fehler.</summary>
    public static (string? Key, string? Error) KeyOf(string? link)
    {
        var l = (link ?? "").Trim();
        if (LinkPattern().Match(l) is { Success: true } m) return (m.Groups[3].Value, null);
        return (null, "Bitte den Link einer eigenen Figma-Datei, z. B. https://www.figma.com/design/…/Name.");
    }

    /// <summary>Schlüssel nur grob prüfen (Form), nie speichern oder ausgeben.</summary>
    public static string? CheckToken(string? token) =>
        string.IsNullOrWhiteSpace(token) || token.Trim().Length < 20 || token.Trim().Length > 200 || token.Any(char.IsWhiteSpace) || token.Any(char.IsControl)
            ? "Bitte deinen persönlichen Zugriffsschlüssel aus Figma (Einstellungen › Sicherheit) einfügen. Trendo speichert ihn nicht." : null;

    public sealed record Mapping(JsonArray Content, IReadOnlyList<string> ImageNodes, IReadOnlyList<string> Report, string Title);

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";
    static double Num(JsonNode? n) => WebDesign.Number(n) ?? 0;

    /// <summary>Antwort von GET /v1/files/:key auf Bausteine abbilden. Bilder bleiben zunächst leer; ihre Knoten-ids holt die App über /v1/images und setzt sie mit <see cref="SetImages"/>.</summary>
    public static Mapping Map(JsonObject file)
    {
        var report = new List<string>(); var images = new List<string>();
        var title = Str(file["name"]) is { Length: > 0 } n ? n[..Math.Min(120, n.Length)] : "Figma";
        var content = new JsonArray(); var ids = 0; var count = 0;
        var page = (file["document"]?["children"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(c => Str(c["type"]) == "CANVAS");
        if (page is null) { report.Add("Die Datei hat keine Seite mit Inhalt."); return new(content, images, report, title); }
        var frames = (page["children"] as JsonArray ?? []).OfType<JsonObject>().Where(c => Str(c["type"]) is "FRAME" or "SECTION" or "COMPONENT").OrderBy(Y).ToList();
        if ((page["children"] as JsonArray ?? []).Count > 1 && frames.Count > 0) report.Add($"Erste Seite „{Str(page["name"])}“: {frames.Count} Rahmen übernommen.");
        string Id(string t) => $"{t.ToLowerInvariant()}-f{++ids}";
        foreach (var frame in frames)
        {
            var inner = new JsonArray();
            var leaves = new List<JsonObject>();
            Collect(frame, leaves, ref count, 0);
            foreach (var leaf in leaves.OrderBy(Y).ThenBy(X))
            {
                var type = Str(leaf["type"]);
                if (type == "TEXT")
                {
                    var text = Str(leaf["characters"]).Trim(); if (text.Length == 0) continue;
                    var size = Num(leaf["style"]?["fontSize"]);
                    inner.Add(size >= 28 ? Block(Id("Ueberschrift"), "Ueberschrift", new JsonObject { ["text"] = text, ["level"] = "h2" })
                        : size >= 20 ? Block(Id("Ueberschrift"), "Ueberschrift", new JsonObject { ["text"] = text, ["level"] = "h3" })
                        : Block(Id("Text"), "Text", new JsonObject { ["text"] = text, ["align"] = Str(leaf["style"]?["textAlignHorizontal"]) == "CENTER" ? "center" : "left" }));
                }
                else if (HasImageFill(leaf) && NodeIdPattern().IsMatch(Str(leaf["id"])))
                {
                    if (images.Count >= MaxImages) { report.Add($"Mehr als {MaxImages} Bilder: der Rest wurde nicht geholt."); continue; }
                    images.Add(Str(leaf["id"]));
                    inner.Add(Block(Id("Bild"), "Bild", new JsonObject { ["src"] = "", ["alt"] = Str(leaf["name"]), ["figma"] = Str(leaf["id"]) }));
                }
            }
            if (inner.Count == 0) continue;
            content.Add(Block(Id("Bereich"), "Bereich", new JsonObject { ["content"] = inner, ["tone"] = "hell" }));
        }
        if (count > MaxNodes) report.Add($"Sehr große Datei: nach {MaxNodes} Elementen abgeschnitten.");
        report.Add("Farben, Schriften und Abstände aus Figma werden nicht übernommen; sie kommen aus der Gestaltung des Projekts. Formen ohne Bild und Vektoren fehlen.");
        return new(content, images, report, title);
    }

    static double Y(JsonObject n) => Num(n["absoluteBoundingBox"]?["y"]);
    static double X(JsonObject n) => Num(n["absoluteBoundingBox"]?["x"]);

    static bool HasImageFill(JsonObject n) => (n["fills"] as JsonArray ?? []).OfType<JsonObject>().Any(f => Str(f["type"]) == "IMAGE" && !(f["visible"] is JsonValue v && (!v.TryGetValue(out bool b) || !b)));

    /// <summary>Dateiname für ein Figma-Bild: umkehrbar aus der Knoten-id und dem Datei-Schlüssel, damit sich Bilder nicht gegenseitig überschreiben.</summary>
    public static string ImageName(string fileKey, string nodeId) => $"figma-{fileKey[..Math.Min(8, fileKey.Length)]}-{nodeId.Replace(':', '-').Replace(';', '_')}.png";

    static void Collect(JsonObject n, List<JsonObject> leaves, ref int count, int depth)
    {
        if (depth > 40) return;
        foreach (var c in (n["children"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (++count > MaxNodes) return;
            if (c["visible"] is JsonValue v && (!v.TryGetValue(out bool vis) || !vis)) continue; // falscher Typ: wie unsichtbar behandeln
            var type = Str(c["type"]);
            if (type == "TEXT" || HasImageFill(c)) leaves.Add(c);
            else Collect(c, leaves, ref count, depth + 1);
        }
    }

    static JsonObject Block(string id, string type, JsonObject props) { props["id"] = id; return new JsonObject { ["type"] = type, ["props"] = props }; }

    /// <summary>Geholte Bilder einsetzen (Figma-Knoten → Datei im Projekt); Bilder ohne Datei fallen weg.</summary>
    public static void SetImages(JsonArray content, IReadOnlyDictionary<string, string> files)
    {
        void Walk(JsonNode? n)
        {
            switch (n)
            {
                case JsonArray a:
                    foreach (var x in a.ToList())
                    {
                        if (x is JsonObject o && Str(o["type"]) == "Bild" && o["props"] is JsonObject p && Str(p["figma"]) is { Length: > 0 } fid)
                        {
                            p.Remove("figma");
                            if (files.TryGetValue(fid, out var f)) p["src"] = f; else a.Remove(x);
                            continue;
                        }
                        Walk(x);
                    }
                    break;
                case JsonObject o: foreach (var kv in o.ToList()) Walk(kv.Value); break;
            }
        }
        Walk(content);
    }

    /// <summary>Nur Bild-Adressen, die Figma für Exporte nutzt (https, bekannte Speicher-Hosts).</summary>
    public static bool IsImageUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.UserInfo.Length == 0 &&
        (u.Host.StartsWith("figma-alpha-api.s3.", StringComparison.OrdinalIgnoreCase) && u.Host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".figma.com", StringComparison.OrdinalIgnoreCase)) && u.IsDefaultPort;

    /// <summary>Knoten-ids für die Bild-Abfrage (nur gültige Form).</summary>
    public static string IdsParam(IEnumerable<string> ids) => string.Join(",", ids.Where(i => NodeIdPattern().IsMatch(i)).Select(Uri.EscapeDataString));
}
