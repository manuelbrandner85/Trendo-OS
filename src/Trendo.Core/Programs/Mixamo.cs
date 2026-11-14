using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0660 (Freigabe 14.11.2026, M1–M6): Mixamo im Spiele-Studio. Es gibt keine offizielle Schnittstelle: Trendo öffnet mixamo.com im eingebauten Browser,
/// die Anmeldung mit dem eigenen kostenlosen Adobe-Konto macht die Person selbst, Trendo speichert keine Anmeldedaten. Heruntergeladene Dateien kommen aus dem
/// Download-Ordner ins Spiel (nichts wird abgegriffen). Mixamo erlaubt Figuren und Animationen in eigenen Spielen, auch verkauft; die rohen Dateien dürfen
/// aber nicht weitergegeben werden – das überwacht <see cref="MixamoGuard"/>. Trendo liefert keine Mixamo-Dateien mit.</summary>
public static class Mixamo
{
    public const string Url = "https://www.mixamo.com/";
    public const string Source = "Mixamo";
    public const string License = "Mixamo-Bedingungen, nur im fertigen Spiel";

    /// <summary>Seiten, die im eingebauten Browser bleiben dürfen: Mixamo selbst und die Adobe-Anmeldung.</summary>
    public static bool Inside(Uri u) => u.Scheme == "https" && (Host(u, "mixamo.com") || Host(u, "adobe.com") || Host(u, "adobelogin.com") || Host(u, "adobe.io"));
    static bool Host(Uri u, string d) => u.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase);

    /// <summary>Suche auf Mixamo (Bewegungen) für einen Begriff.</summary>
    public static string SearchUrl(string query) => $"https://www.mixamo.com/#/?page=1&query={Uri.EscapeDataString(query.Trim())}&type=Motion%2CMotionPack";

    // ───────────── Erkennen ─────────────

    public sealed record Info(bool IsMixamo, bool HasMesh);

    static readonly byte[] Rig = Encoding.ASCII.GetBytes("mixamorig");
    static readonly byte[] BinMesh = [0, 1, (byte)'G', (byte)'e', (byte)'o', (byte)'m', (byte)'e', (byte)'t', (byte)'r', (byte)'y'];
    static readonly byte[] AsciiMesh = Encoding.ASCII.GetBytes("Geometry::");

    /// <summary>Datei enthält ein Mixamo-Skelett (Knochen „mixamorig…“)? Mit Mesh = Figur, ohne = reine Animation. Liest die Datei in Stücken.</summary>
    public static Info Detect(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is not (".fbx" or ".glb" or ".gltf" or ".dae" or ".bvh")) return new(false, false);
        bool rig = false, mesh = false;
        try
        {
            using var f = File.OpenRead(file);
            var buf = new byte[1 << 20]; var keep = 16; var carry = 0;
            int n;
            while ((n = f.Read(buf, carry, buf.Length - carry)) > 0)
            {
                var span = buf.AsSpan(0, carry + n);
                rig |= span.IndexOf(Rig) >= 0;
                mesh |= span.IndexOf(BinMesh) >= 0 || span.IndexOf(AsciiMesh) >= 0 || (ext is ".glb" or ".gltf" && span.IndexOf("\"meshes\""u8) >= 0);
                if (rig && mesh) break;
                carry = Math.Min(keep, span.Length); span[^carry..].CopyTo(buf);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(false, false); }
        return new(rig, rig && mesh);
    }

    // ───────────── Spielaktionen (M5) ─────────────

    public sealed record Action(string Id, string Label, string[] Search, string[] Words);

    /// <summary>Aktionen des Spiels mit vorgeschlagenen Suchbegriffen für Mixamo und Wörtern, an denen Trendo eine Datei erkennt.</summary>
    public static readonly Action[] Actions =
    [
        new("stehen", "Stehen", ["Idle", "Breathing Idle", "Standing Idle"], ["idle", "stand", "breath"]),
        new("gehen", "Gehen", ["Walking", "Walk In Place", "Casual Walk"], ["walk"]),
        new("laufen", "Laufen", ["Running", "Jog Forward", "Fast Run"], ["run", "jog", "sprint"]),
        new("springen", "Springen", ["Jump", "Jumping Up", "Running Jump"], ["jump"]),
        new("fallen", "Fallen", ["Falling Idle", "Falling", "Fall A Loop"], ["fall"]),
        new("getroffen", "Getroffen", ["Hit Reaction", "Receive Hit", "Big Hit"], ["hit", "impact", "react"]),
    ];

    /// <summary>Aktion aus dem Dateinamen („Running Jump.fbx“ → springen, „Walking.fbx“ → gehen); null, wenn nichts passt.</summary>
    public static string? Guess(string file)
    {
        var n = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
        foreach (var id in new[] { "springen", "fallen", "getroffen", "laufen", "gehen", "stehen" }) // Sprung vor Laufen: „Running Jump“ ist ein Sprung
            if (Actions.First(a => a.Id == id).Words.Any(n.Contains)) return id;
        return null;
    }

    static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    /// <summary>Mixamo-Einträge der Asset-Liste.</summary>
    public static List<JsonObject> Assets(JsonObject d) => [.. (d["assets"] as JsonArray ?? []).OfType<JsonObject>().Where(a => a["mixamo"]?.GetValueKind() == System.Text.Json.JsonValueKind.True)];

    /// <summary>Zuordnung Aktion → Datei (nur Mixamo-Animationen der Asset-Liste).</summary>
    public static Dictionary<string, string> Assignments(JsonObject d)
    {
        var map = new Dictionary<string, string>();
        if (d["mixamoActions"] is JsonObject o)
            foreach (var (k, v) in o)
                if (Actions.Any(a => a.Id == k) && S(v) is { Length: > 0 } f) map[k] = f;
        return map;
    }

    public static void Assign(JsonObject d, string action, string? file)
    {
        if (!Actions.Any(a => a.Id == action)) throw new ArgumentException("Unbekannte Aktion.");
        var o = d["mixamoActions"] as JsonObject ?? []; d["mixamoActions"] = o;
        if (file is null) { o.Remove(action); return; }
        if (!Assets(d).Any(a => S(a["file"]) == file && S(a["kind"]) == "animation")) throw new ArgumentException("Diese Datei ist keine Mixamo-Animation in der Asset-Liste.");
        o[action] = file;
    }

    // ───────────── Übernehmen (M2) ─────────────

    /// <summary>Gleiche Datei (Name und Größe) schon in der Asset-Liste?</summary>
    public static bool Known(JsonObject d, string file)
    {
        var name = Path.GetFileName(file); long len;
        try { len = new FileInfo(file).Length; } catch (IOException) { return false; }
        return Assets(d).Any(a => Path.GetFileName(S(a["file"])).Equals(name, StringComparison.OrdinalIgnoreCase) && a["bytes"]?.GetValue<long>() == len);
    }

    /// <summary>Mixamo-Datei ins Spiel übernehmen: Kopie in den Asset-Ordner, Eintrag mit Quelle Mixamo und Lizenz, Art (Figur oder Animation) und erkannter Aktion.
    /// Eine Animation, deren Aktion noch frei ist, wird ihr gleich zugeordnet. Liefert null, wenn die Datei kein Mixamo-Skelett hat oder schon übernommen ist.</summary>
    public static JsonObject? Take(JsonObject d, string assetDir, string file)
    {
        var info = Detect(file);
        if (!info.IsMixamo) return null;
        if (Known(d, file)) return null;
        var len = new FileInfo(file).Length;
        var e = GameAssets.Add(d, assetDir, file, Source, License, "Mixamo (Adobe)", Url, "datei");
        e["mixamo"] = true; e["bytes"] = len;
        e["kind"] = info.HasMesh ? "figur" : "animation";
        if (!info.HasMesh && Guess(file) is { } act)
        {
            e["action"] = act;
            if (!Assignments(d).ContainsKey(act)) Assign(d, act, S(e["file"]));
        }
        if (info.HasMesh && S(d["mixamoFigure"]).Length == 0) d["mixamoFigure"] = S(e["file"]);
        return e;
    }

    /// <summary>Kurzer Stand für die Oberfläche.</summary>
    public static string Summary(JsonObject d)
    {
        var a = Assets(d); var figs = a.Count(x => S(x["kind"]) == "figur"); var anims = a.Count - figs; var done = Assignments(d).Count;
        return string.Create(CultureInfo.GetCultureInfo("de-DE"), $"{figs} Figur(en), {anims} Animation(en), {done} von {Actions.Length} Aktionen belegt");
    }
}
