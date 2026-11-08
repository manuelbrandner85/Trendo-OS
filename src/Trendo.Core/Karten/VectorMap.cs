using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Karten;

/// <summary>Alle Studios, Runde 1 – SPEC-13: Vektorkarten (PMTiles mit MVT, z. B. Protomaps-Basiskarte) im Karten-Studio.
/// Die Kartenseite (MapLibre GL JS, BSD-3, gebündelt unter Assets/karten) läuft unter file:// ohne Netz und fragt jede Kachel über die WebView-Brücke an;
/// Trendo liest sie mit dem eigenen <see cref="PmTiles"/>-Leser (bereits entpackt) und schickt sie als Base64 zurück.</summary>
public sealed class VectorMap : IDisposable
{
    readonly PmTiles _pm;
    public PmTiles.Header Info => _pm.Info;
    public string Name { get; }

    public VectorMap(string path)
    {
        if (!path.EndsWith(".pmtiles", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) throw new ArgumentException("Bitte eine Vektorkarte als .pmtiles-Datei wählen.");
        _pm = new PmTiles(path);
        if (_pm.Info.Type != PmTiles.TileType.Mvt) { _pm.Dispose(); throw new InvalidDataException("Das ist keine Vektorkarte (MVT) – Rasterkarten zeigt das Karten-Studio direkt."); }
        Name = Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>Aufruf, der die Karte in der Seite startet (Zoomstufen und Gebiet aus dem PMTiles-Kopf).</summary>
    public string InitScript()
    {
        var i = _pm.Info; var ci = CultureInfo.InvariantCulture;
        var o = new JsonObject { ["name"] = Name, ["minzoom"] = i.MinZoom, ["maxzoom"] = i.MaxZoom,
            ["bounds"] = new JsonArray(Math.Round(i.MinLon, 6), Math.Round(i.MinLat, 6), Math.Round(i.MaxLon, 6), Math.Round(i.MaxLat, 6)) };
        return "window.trendoKarte(" + o.ToJsonString() + ")";
    }

    public sealed record Message(string Kind, int Id, int Z, int X, int Y, string Text);

    /// <summary>Nachricht der Kartenseite lesen; unbekannt oder kaputt → null (keine Ausnahme aus der Seite heraus).</summary>
    public static Message? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 10_000) return null;
        try
        {
            using var d = JsonDocument.Parse(body); var r = d.RootElement; if (r.ValueKind != JsonValueKind.Object) return null;
            var kind = r.TryGetProperty("art", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString()! : "";
            int N(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : -1;
            var text = r.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
            return kind is "bereit" or "kachel" or "geladen" or "fehler" ? new Message(kind, N("id"), N("z"), N("x"), N("y"), text.Length > 300 ? text[..300] : text) : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Antwort auf eine Kachelanfrage als Skriptaufruf; leere Kachel = "" (die Karte zeichnet dort nichts).</summary>
    public string? Answer(Message m)
    {
        if (m.Kind != "kachel" || m.Id < 0 || m.Z < 0 || m.Z > 30 || m.X < 0 || m.Y < 0) return null;
        byte[]? tile;
        try { tile = _pm.Get(m.Z, m.X, m.Y); }
        catch (InvalidDataException) { tile = null; }
        return $"window.trendoKachel({m.Id},\"{(tile is null ? "" : Convert.ToBase64String(tile))}\")";
    }

    public void Dispose() => _pm.Dispose();
}
