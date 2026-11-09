using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Arch;

/// <summary>T-0280: Foto-Ansicht des Grundrisses – Szene für Blender (Meter, Z oben, wie Blender) und Aufruf von Cycles im Hintergrund.
/// Wände mit echten Aussparungen kommen aus <see cref="Plan3D"/>, Böden je Belag, Fenster mit Glas, Möbel in Render-Qualität (trendo_moebel.py)
/// oder als Modell aus der 3D-Bibliothek.</summary>
public static class PlanPhoto
{
    /// <summary>„vogel“ = Blick schräg von oben ohne Decke, „raum“ = Augenhöhe in einem Raum mit Decke und Leuchte.</summary>
    /// <summary>Sonnenstand aus Monat (Mitte), Sonnenzeit und Breitengrad. Azimut in Grad von Nord im Uhrzeigersinn, Elevation in Grad.</summary>
    public static (double Elevation, double Azimuth) Sun(int month, double hour, double latitude)
    {
        const double Rad = Math.PI / 180;
        var day = 15 + (Math.Clamp(month, 1, 12) - 1) * 30.4;
        var declination = 23.44 * Math.Sin(2 * Math.PI * (day - 81) / 365) * Rad;
        var lat = latitude * Rad; var angle = (hour - 13) * 15 * Rad; // 13 Uhr Ortszeit entspricht in Mitteleuropa etwa dem Sonnenhöchststand
        var sinEl = Math.Sin(lat) * Math.Sin(declination) + Math.Cos(lat) * Math.Cos(declination) * Math.Cos(angle);
        var el = Math.Asin(sinEl);
        var az = Math.Atan2(Math.Sin(angle), Math.Cos(angle) * Math.Sin(lat) - Math.Tan(declination) * Math.Cos(lat)) + Math.PI;
        return (el / Rad, (az / Rad + 360) % 360);
    }

    public static JsonObject Scene(IReadOnlyList<FloorPlan> levels, Func<string, string?> modelFile, string mode = "vogel", string? roomId = null, int width = 1600, int height = 1000, int samples = 256, int? sunMonth = null, double? sunHour = null, double latitude = 51)
    {
        static JsonArray P(params double[] v) => new(v.Select(x => (JsonNode)Math.Round(x, 4)).ToArray());
        var walls = new JsonArray(); var rooms = new JsonArray(); var openings = new JsonArray(); var items = new JsonArray();
        foreach (var p in levels)
        {
            var z = p.Elevation / 100; var wallIds = p.Walls.Select(w => w.Id).ToHashSet();
            foreach (var (_, solid, id) in Plan3D.Build(p))
                if (id is not null && wallIds.Contains(id))
                    walls.Add(new JsonArray(solid.Polys.Select(poly => (JsonNode)new JsonArray(poly.Verts.Select(v => (JsonNode)P(v.X / 100, v.Y / 100, v.Z / 100)).ToArray())).ToArray())); // je Wandkörper geschlossen
            foreach (var r in p.Rooms.Where(r => r.Points.Count >= 3))
                rooms.Add(new JsonObject { ["id"] = r.Id, ["name"] = r.Name, ["kind"] = r.Kind, ["floor"] = r.Floor, ["wall"] = r.WallFinish, ["z"] = z, ["height"] = r.Height / 100,
                    ["points"] = new JsonArray(r.Points.Select(q => (JsonNode)P(q.X / 100, -q.Y / 100)).ToArray()) });
            foreach (var o in p.Openings)
            {
                if (p.Walls.FirstOrDefault(w => w.Id == o.WallId) is not { } w) continue;
                var (cx, cy) = w.PointAt(Math.Clamp(o.At, 0, w.Length)); var (ux, uy) = w.Dir;
                openings.Add(new JsonObject { ["kind"] = o.Kind, ["x"] = cx / 100, ["y"] = -cy / 100, ["z"] = z + o.Sill / 100, ["width"] = o.Width / 100, ["height"] = Math.Min(o.Height, w.Height - o.Sill) / 100,
                    ["thickness"] = w.Thickness / 100, ["angle"] = Math.Atan2(-uy, ux) });
            }
            foreach (var i in p.Items)
            {
                var f = FurnitureCatalog.Get(i.Type);
                var o = new JsonObject { ["type"] = i.Type, ["name"] = i.Name, ["x"] = i.X / 100, ["y"] = -i.Y / 100, ["z"] = z, ["w"] = i.W / 100, ["d"] = i.D / 100,
                    ["h"] = (f?.H ?? 60) / 100, ["rot"] = -i.Rot, ["mirror"] = i.Mirror };
                if (!FurnitureCatalog.All.Any(c => c.Type == i.Type)) { if (modelFile(i.Type) is { } file) o["file"] = file; else continue; } // Bibliotheksmodell ohne Datei: weglassen
                items.Add(o);
            }
        }
        var top = levels.OrderBy(l => l.Elevation).LastOrDefault();
        return new JsonObject
        {
            ["mode"] = mode, ["room"] = roomId, ["width"] = width, ["height"] = height, ["samples"] = samples,
            ["sun"] = sunMonth is { } mon && sunHour is { } hr ? new JsonObject { ["elevation"] = Math.Round(Sun(mon, hr, latitude).Elevation, 2), ["azimuth"] = Math.Round(Sun(mon, hr, latitude).Azimuth, 2) } : null,
            ["north"] = top?.North ?? 0, ["walls"] = walls, ["rooms"] = rooms, ["openings"] = openings, ["items"] = items,
        };
    }

    /// <summary>Szene schreiben und mit Blender rendern; meldet den Fortschritt (0–1) aus den Cycles-Zeilen. Gibt den Pfad des PNG zurück.</summary>
    public static async Task<string> Render(JsonObject scene, string scriptDir, string output, string blender, IProgress<double>? progress = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        scene["out"] = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var json = Path.Combine(Path.GetTempPath(), $"trendo-foto-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(json, scene.ToJsonString(), ct);
        try
        {
            var psi = new ProcessStartInfo(blender) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "-b", "--factory-startup", "--python", Path.Combine(scriptDir, "trendo_foto.py"), "--", json }) psi.ArgumentList.Add(a);
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Blender ließ sich nicht starten.");
            string? error = null; var final = false; var errTask = proc.StandardError.ReadToEndAsync(ct);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout ?? TimeSpan.FromMinutes(15));
            try
            {
                while (await proc.StandardOutput.ReadLineAsync(cts.Token) is { } line)
                {
                    if (line.StartsWith("TRENDO_FEHLER")) error = line["TRENDO_FEHLER".Length..].Trim();
                    else if (line.StartsWith("TRENDO_FORTSCHRITT") && double.TryParse(line["TRENDO_FORTSCHRITT".Length..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) progress?.Report(Math.Clamp(v, 0, 1));
                    else if (line.StartsWith("TRENDO_PHASE final")) final = true;
                    else if (final && Progress(line) is { } pr) progress?.Report(0.45 + 0.55 * pr); // Vorbereitung bis 0,42, dann die Samples der Endaufnahme
                }
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) { try { proc.Kill(true); } catch (InvalidOperationException) { } throw new TimeoutException(ct.IsCancellationRequested ? "Foto abgebrochen." : "Das Foto dauert zu lange und wurde abgebrochen."); }
            await errTask;
            if (error is not null) throw new InvalidDataException("Blender: " + error);
            if (!File.Exists(output)) throw new InvalidDataException("Blender hat kein Bild geschrieben.");
            return output;
        }
        finally { try { File.Delete(json); } catch (IOException) { } }
    }

    /// <summary>Fortschritt aus einer Cycles-Zeile („… | Sample 64/256“). Zählt erst ab „TRENDO_PHASE final“ – die Belichtungsmessung davor nicht.</summary>
    public static double? Progress(string line)
    {
        var k = line.IndexOf("Sample ", StringComparison.Ordinal); if (k < 0) return null;
        var part = line[(k + 7)..].Split(' ', '|')[0].Split('/');
        return part.Length == 2 && int.TryParse(part[0], out var a) && int.TryParse(part[1], out var b) && b > 0 ? (double)a / b : null;
    }
}
