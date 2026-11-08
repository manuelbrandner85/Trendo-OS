using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Arch;

/// <summary>Alle Studios, Runde 3 – SPEC-01 „Wohnung planen“: Grundriss als IFC4 (BIM) über IfcOpenShell (LGPL-3.0, eigener Prozess, Skript trendo_ifc.py).
/// Trendo übergibt ein neutrales JSON: Meter statt Zentimeter, y nach oben statt nach unten, Wandachse so verschoben, dass der IFC-Wandkörper
/// (IfcOpenShell legt ihn links der Achse an) wie in Trendo mittig auf der Achse sitzt. Öffnungen: Anfang statt Mitte.</summary>
public static class IfcExchange
{
    public const double MinWallCm = 1, MaxWallCm = 100_000;

    public static string ToJson(FloorPlan plan, string? projectName = null)
    {
        var walls = new JsonArray(); var index = new Dictionary<string, int>(); var axes = new List<(double X1, double Y1, double Ux, double Uy, double Len, double T)>();
        foreach (var w in plan.Walls)
        {
            if (!(w.Length >= MinWallCm && w.Length <= MaxWallCm) || w.Thickness <= 0 || w.Height <= 0) continue; // Nullwände und Unsinn auslassen
            double x1 = w.X1 / 100, y1 = -w.Y1 / 100, x2 = w.X2 / 100, y2 = -w.Y2 / 100, t = w.Thickness / 100;
            var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)); double ux = (x2 - x1) / len, uy = (y2 - y1) / len;
            double sx = uy * t / 2, sy = -ux * t / 2; // um die halbe Dicke nach rechts: Körper links davon = mittig auf Trendos Achse
            index[w.Id] = walls.Count; axes.Add((x1 + sx, y1 + sy, ux, uy, len, t));
            walls.Add(new JsonObject { ["x1"] = R(x1 + sx), ["y1"] = R(y1 + sy), ["x2"] = R(x2 + sx), ["y2"] = R(y2 + sy), ["staerke"] = R(t), ["hoehe"] = R(w.Height / 100) });
        }
        var openings = new JsonArray();
        foreach (var o in plan.Openings)
        {
            if (!index.TryGetValue(o.WallId, out var wi)) continue;
            var len = axes[wi].Len; var start = (o.At - o.Width / 2) / 100;
            if (o.Width <= 0 || o.Height <= 0 || start < 0 || start + o.Width / 100 > len + 1e-6) continue; // ragt aus der Wand – auslassen
            openings.Add(new JsonObject { ["wand"] = wi, ["art"] = o.Kind == "fenster" ? "fenster" : "tuer", ["bei"] = R(start), ["breite"] = R(o.Width / 100), ["hoehe"] = R(o.Height / 100), ["bruestung"] = R(Math.Max(0, o.Sill) / 100) });
        }
        var rooms = new JsonArray();
        foreach (var r in plan.Rooms.Where(r => r.Points.Count >= 3))
            rooms.Add(new JsonObject { ["name"] = r.Name, ["hoehe"] = R(r.Height / 100), ["umriss"] = new JsonArray([.. r.Points.Select(p => (JsonNode)new JsonArray(R(p.X / 100), R(-p.Y / 100)))]) });
        return new JsonObject
        {
            ["projekt"] = string.IsNullOrWhiteSpace(projectName) ? plan.Name : projectName.Trim(),
            ["geschoss"] = plan.LevelName, ["hoehenlage"] = R(plan.Elevation / 100),
            ["waende"] = walls, ["oeffnungen"] = openings, ["raeume"] = rooms,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    static double R(double v) => Math.Round(v, 4);

    /// <summary>Kurzbericht aus <c>trendo_ifc.py info</c>.</summary>
    public sealed record Info(string Schema, string? Project, int Walls, int Doors, int Windows, int Spaces, int Storeys, IReadOnlyList<string> Rooms)
    {
        public string Text => $"{Schema}: {Walls} Wände, {Doors} Türen, {Windows} Fenster, {Spaces} Räume, {Storeys} Geschoss{(Storeys == 1 ? "" : "e")}";
    }

    public static Info ParseInfo(string json)
    {
        using var d = JsonDocument.Parse(json); var r = d.RootElement;
        var schema = r.GetProperty("schema").GetString() ?? "";
        if (!schema.StartsWith("IFC", StringComparison.Ordinal)) throw new FormatException("Keine IFC-Datei.");
        var a = r.GetProperty("anzahl"); int N(string k) => a.TryGetProperty(k, out var v) ? v.GetInt32() : 0;
        var rooms = r.TryGetProperty("raeume", out var rr) ? rr.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
        return new Info(schema, r.TryGetProperty("projekt", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null,
            N("IfcWall"), N("IfcDoor"), N("IfcWindow"), N("IfcSpace"), N("IfcBuildingStorey"), rooms);
    }

    /// <summary>Dateiname für die IFC-Ausgabe aus dem Plannamen (nur sichere Zeichen).</summary>
    public static string FileName(string name)
    {
        var s = string.Concat((name ?? "").Trim().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        while (s.Contains("--", StringComparison.Ordinal)) s = s.Replace("--", "-", StringComparison.Ordinal);
        return (s.Length == 0 ? "grundriss" : s[..Math.Min(60, s.Length)]) + ".ifc";
    }
}
