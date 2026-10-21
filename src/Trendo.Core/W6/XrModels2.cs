using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record XrTexture(string Name, string Mime, int Width, int Height, long VramBytes);
public sealed record XrDetail(XrModelInfo Info, double SizeX, double SizeY, double SizeZ, IReadOnlyList<string> Nodes, IReadOnlyList<string> MeshNames, IReadOnlyList<string> Animations, IReadOnlyList<XrTexture> Textures, IReadOnlyDictionary<string, int> DrawCallsPerMaterial, IReadOnlyList<string> Extensions, IReadOnlyList<string> Required);

/// <summary>SPEC-04 Ausbau: Details aus glTF/GLB (Maße, Namen, Texturen, Erweiterungen), Komfort, LOD, Texeldichte, Bericht.</summary>
public static class XrPlus
{
    public static XrDetail Detail(string path)
    {
        var info = XrInspector.Inspect(path);
        var bytes = File.ReadAllBytes(path); string json; byte[]? bin = null;
        if (Encoding.ASCII.GetString(bytes, 0, Math.Min(4, bytes.Length)) == "glTF")
        {
            var len = (int)BitConverter.ToUInt32(bytes, 12); json = Encoding.UTF8.GetString(bytes, 20, len);
            var o = 20 + len; if (o + 8 <= bytes.Length && BitConverter.ToUInt32(bytes, o + 4) == 0x004E4942) { var bl = (int)BitConverter.ToUInt32(bytes, o); bin = bytes[(o + 8)..Math.Min(bytes.Length, o + 8 + bl)]; }
        }
        else json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json); var r = doc.RootElement;
        IEnumerable<JsonElement> Arr(string k) => r.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray() : [];
        string Nm(JsonElement e, string fb) => e.TryGetProperty("name", out var n) ? n.GetString() ?? fb : fb;
        var acc = Arr("accessors").ToList();
        double[] mn = [double.MaxValue, double.MaxValue, double.MaxValue], mx = [double.MinValue, double.MinValue, double.MinValue];
        var draws = new Dictionary<string, int>(); var mats = Arr("materials").Select((m, i) => Nm(m, $"Material {i + 1}")).ToList();
        foreach (var m in Arr("meshes")) if (m.TryGetProperty("primitives", out var ps)) foreach (var p in ps.EnumerateArray())
        {
            var key = p.TryGetProperty("material", out var mi) && mi.GetInt32() < mats.Count ? mats[mi.GetInt32()] : "(ohne Material)"; draws[key] = draws.GetValueOrDefault(key) + 1;
            if (p.TryGetProperty("attributes", out var at) && at.TryGetProperty("POSITION", out var pos) && pos.GetInt32() < acc.Count && acc[pos.GetInt32()].TryGetProperty("min", out var a0) && acc[pos.GetInt32()].TryGetProperty("max", out var a1))
                for (var k = 0; k < 3; k++) { mn[k] = Math.Min(mn[k], a0[k].GetDouble()); mx[k] = Math.Max(mx[k], a1[k].GetDouble()); }
        }
        var views = Arr("bufferViews").ToList(); var tex = new List<XrTexture>(); var imgI = 0;
        foreach (var im in Arr("images"))
        {
            imgI++; var mime = im.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "" : ""; byte[]? data = null;
            if (im.TryGetProperty("bufferView", out var bv) && bin is not null && bv.GetInt32() < views.Count)
            { var v = views[bv.GetInt32()]; var off = v.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0; var l = v.GetProperty("byteLength").GetInt32(); if (off + l <= bin.Length) data = bin[off..(off + l)]; }
            else if (im.TryGetProperty("uri", out var u) && u.GetString() is { } uri)
            {
                if (uri.StartsWith("data:")) data = Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..]);
                else { var f = Path.Combine(Path.GetDirectoryName(path) ?? ".", Uri.UnescapeDataString(uri)); if (File.Exists(f)) data = File.ReadAllBytes(f); }
            }
            var (w, h) = data is null ? (0, 0) : ImageSize(data);
            tex.Add(new(Nm(im, $"Bild {imgI}"), mime == "" && data is not null ? (data[0] == 0x89 ? "image/png" : "image/jpeg") : mime, w, h, (long)(w * (long)h * 4 * 4 / 3.0)));
        }
        List<string> Strs(string k) => r.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
        var has = mn[0] != double.MaxValue;
        return new(info, has ? mx[0] - mn[0] : 0, has ? mx[1] - mn[1] : 0, has ? mx[2] - mn[2] : 0, Arr("nodes").Select((n, i) => Nm(n, $"Knoten {i + 1}")).ToList(), Arr("meshes").Select((n, i) => Nm(n, $"Mesh {i + 1}")).ToList(),
            Arr("animations").Select((n, i) => Nm(n, $"Animation {i + 1}")).ToList(), tex, draws, Strs("extensionsUsed"), Strs("extensionsRequired"));
    }

    /// <summary>Breite/Höhe aus PNG- oder JPEG-Kopf.</summary>
    public static (int W, int H) ImageSize(byte[] d)
    {
        if (d.Length > 24 && d[0] == 0x89 && d[1] == 'P') return (d[16] << 24 | d[17] << 16 | d[18] << 8 | d[19], d[20] << 24 | d[21] << 16 | d[22] << 8 | d[23]);
        if (d.Length > 4 && d[0] == 0xFF && d[1] == 0xD8)
            for (var i = 2; i + 9 < d.Length;)
            {
                if (d[i] != 0xFF) { i++; continue; }
                var m = d[i + 1]; var len = d[i + 2] << 8 | d[i + 3];
                if (m is >= 0xC0 and <= 0xCF && m is not (0xC4 or 0xC8 or 0xCC)) return (d[i + 7] << 8 | d[i + 8], d[i + 5] << 8 | d[i + 6]);
                i += 2 + len;
            }
        return (0, 0);
    }

    /// <summary>F08: Maßstab – glTF ist in Metern. Warnung bei unter 1 cm oder über 500 m.</summary>
    public static string ScaleVerdict(XrDetail d)
    {
        var m = Math.Max(d.SizeX, Math.Max(d.SizeY, d.SizeZ));
        return m == 0 ? "Keine Maße im Modell (min/max fehlt)." : m < 0.01 ? $"⚠ Nur {m * 1000:0.#} mm groß – wahrscheinlich falsche Einheit (cm/mm statt m). Beim Export „Maßstab 100“ bzw. „Meter“ wählen." : m > 500 ? $"⚠ {m:0} m groß – wahrscheinlich in mm/cm exportiert. Auf Meter umstellen." : $"✓ Größe {Fach.F(d.SizeX)} × {Fach.F(d.SizeY)} × {Fach.F(d.SizeZ)} m – Maßstab plausibel.";
    }

    /// <summary>F09: Texel-Dichte = Texturpixel / größte Objektkante (px/m). Richtwert VR: ≥ 512 px/m für Nahbereich.</summary>
    public static (double PxPerM, string Verdict) TexelDensity(int texPx, double objectM, double viewDistM = 1)
    {
        if (texPx <= 0 || objectM <= 0) throw new ArgumentException("Texturgröße und Objektgröße müssen größer als 0 sein.");
        var d = texPx / objectM; var need = 1024 / Math.Max(0.25, viewDistM);
        return (Math.Round(d), d >= need ? "✓ scharf genug" : d >= need / 2 ? "geht, wirkt aus der Nähe leicht weich" : "⚠ zu unscharf – größere Textur oder Atlas");
    }

    /// <summary>F10: Komfort: Geradeaus > 3 m/s, Drehen > 90°/s (glatt), Beschleunigung, Höhenänderung → Übelkeitsrisiko.</summary>
    public static (int Score, IReadOnlyList<string> Tips) Comfort(double moveMs, double turnDegS, bool snapTurn, bool vignette, double accelMs2 = 0)
    {
        var tips = new List<string>(); var s = 100;
        if (moveMs > 3) { s -= 30; tips.Add("Fortbewegung über 3 m/s – langsamer oder Teleport anbieten."); } else if (moveMs > 1.5) { s -= 10; tips.Add("Gehtempo ist bequemer (ca. 1,4 m/s)."); }
        if (!snapTurn && turnDegS > 90) { s -= 25; tips.Add("Glattes Drehen über 90°/s – Schritt-Drehen (30–45°) anbieten."); }
        if (accelMs2 > 0) { s -= Math.Min(25, (int)(accelMs2 * 5)); tips.Add("Beschleunigung vermeiden: sofort auf Endgeschwindigkeit."); }
        if (!vignette && moveMs > 0) { s -= 10; tips.Add("Tunnelblick (Vignette) beim Bewegen einschalten."); }
        return (Math.Max(0, s), tips);
    }

    /// <summary>F14: LOD-Stufen 100/50/25/12,5 % mit Umschaltentfernungen (Bildschirmanteil-Faustregel).</summary>
    public static IReadOnlyList<(int Level, long Triangles, double FromM)> LodPlan(long triangles, double objectM)
    {
        if (triangles <= 0 || objectM <= 0) throw new ArgumentException("Dreiecke und Größe müssen größer als 0 sein.");
        return Enumerable.Range(0, 4).Select(i => (i, (long)Math.Ceiling(triangles / Math.Pow(2, i)), i == 0 ? 0 : Math.Round(objectM * 5 * Math.Pow(2, i - 1), 1))).ToList();
    }

    private static readonly Dictionary<string, string> ExtInfo = new()
    {
        ["KHR_draco_mesh_compression"] = "Draco-Kompression: Quest/WebXR ja (Decoder nötig), Apple Quick Look nein",
        ["KHR_texture_basisu"] = "KTX2/Basis-Texturen: Quest/WebXR ja, Apple Quick Look nein",
        ["EXT_meshopt_compression"] = "Meshopt-Kompression: WebXR/three.js ja, viele Engines nur mit Plugin",
        ["KHR_materials_unlit"] = "Unbeleuchtete Materialien: überall unterstützt, spart Rechenzeit",
        ["KHR_texture_transform"] = "Textur-Verschiebung: weit unterstützt",
        ["KHR_mesh_quantization"] = "Gepackte Koordinaten: spart Speicher, weit unterstützt",
        ["KHR_materials_transmission"] = "Glas/Durchsicht: teuer auf Brillen, sparsam nutzen",
    };
    public static IReadOnlyList<string> ExtensionNotes(XrDetail d) => d.Extensions.Select(e => (d.Required.Contains(e) ? "Pflicht: " : "Genutzt: ") + e + " – " + (ExtInfo.TryGetValue(e, out var i) ? i : "unbekannt, auf dem Zielgerät testen")).ToList();

    /// <summary>F13: Prüfbericht als Markdown.</summary>
    public static string Report(XrDetail d)
    {
        var sb = new StringBuilder($"# VR/AR-Prüfbericht: {d.Info.File}\n\nErstellt: {DateTime.Now:dd.MM.yyyy HH:mm}\n\n- Dreiecke: {Fach.F(d.Info.Triangles, 0)}\n- Zeichenaufrufe: {d.Info.Primitives}\n- Größe: {ScaleVerdict(d)}\n- Datei: {d.Info.Bytes / 1048576.0:0.##} MB\n\n## Geräte\n\n");
        foreach (var b in XrInspector.Budgets) { var c = XrInspector.Check(d.Info, b); sb.Append($"- {(c.Ok ? "✓" : "⚠")} {b.Device}{(c.Ok ? "" : ": " + string.Join(" ", c.Problems))}\n"); }
        sb.Append("\n## Texturen\n\n"); foreach (var t in d.Textures) sb.Append($"- {t.Name}: {t.Width} × {t.Height} ({t.Mime}), Grafikspeicher ca. {t.VramBytes / 1048576.0:0.#} MB\n");
        sb.Append("\n## Zeichenaufrufe je Material\n\n"); foreach (var (k, v) in d.DrawCallsPerMaterial) sb.Append($"- {k}: {v}\n");
        if (d.Extensions.Count > 0) { sb.Append("\n## Erweiterungen\n\n"); foreach (var e in ExtensionNotes(d)) sb.Append($"- {e}\n"); }
        if (d.Animations.Count > 0) sb.Append($"\n## Animationen\n\n{string.Join(", ", d.Animations)}\n");
        return sb.ToString();
    }
}
