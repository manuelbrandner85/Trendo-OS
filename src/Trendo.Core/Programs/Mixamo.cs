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
    public static bool Inside(Uri u) => u.Scheme == "https" && (Host(u, "mixamo.com") || Host(u, "adobe.com") || Host(u, "adobelogin.com") || Host(u, "adobe.io")
        || u.Host is "accounts.google.com" or "appleid.apple.com" or "www.facebook.com" or "login.microsoftonline.com"); // Anmeldung bei Adobe über Google, Apple, Facebook, Microsoft
    static bool Host(Uri u, string d) => u.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase);

    /// <summary>Suche auf Mixamo (Bewegungen) für einen Begriff.</summary>
    public static string SearchUrl(string query) => $"https://www.mixamo.com/#/?page=1&query={Uri.EscapeDataString(query.Trim())}&type=Motion%2CMotionPack";

    // ───────────── Erkennen ─────────────

    /// <summary>HasAnim: Animationskurven vorhanden (Mixamo-Animationen „With Skin“ haben Mesh UND Kurven).</summary>
    public sealed record Info(bool IsMixamo, bool HasMesh, bool HasAnim = false, long Bytes = 0);

    static readonly byte[] Rig = Encoding.ASCII.GetBytes("mixamorig");
    static readonly byte[] BinMesh = [0, 1, (byte)'G', (byte)'e', (byte)'o', (byte)'m', (byte)'e', (byte)'t', (byte)'r', (byte)'y'];
    static readonly byte[] AsciiMesh = Encoding.ASCII.GetBytes("Geometry::");
    static readonly byte[] BinCurve = [0, 1, (byte)'A', (byte)'n', (byte)'i', (byte)'m', (byte)'C', (byte)'u', (byte)'r', (byte)'v', (byte)'e'];
    static readonly byte[] AsciiCurve = Encoding.ASCII.GetBytes("AnimCurve::");

    /// <summary>Beginnt der Inhalt wie ein 3D-Format mit Skelett (FBX binär/Text, glTF binär/JSON, BVH, Collada)? Unabhängig von der Dateiendung.</summary>
    public static bool LooksLikeModel(ReadOnlySpan<byte> head)
    {
        var t = Encoding.ASCII.GetString(head.Length > 64 ? head[..64] : head).TrimStart('﻿', ' ', '\r', '\n', '\t');
        return t.StartsWith("Kaydara FBX Binary", StringComparison.Ordinal) || t.StartsWith("; FBX", StringComparison.Ordinal) || t.StartsWith("glTF", StringComparison.Ordinal)
            || t.StartsWith("HIERARCHY", StringComparison.Ordinal) || t.StartsWith("<?xml", StringComparison.Ordinal) || t.StartsWith("<COLLADA", StringComparison.Ordinal) || (t.StartsWith('{') && t.Contains("\"asset\"", StringComparison.Ordinal));
    }

    /// <summary>Datei enthält ein Mixamo-Skelett (Knochen „mixamorig…“)? Liest in Stücken. Nur 3D-Endungen, außer <paramref name="anyName"/> (Lizenzwächter).</summary>
    public static Info Detect(string file, bool anyName = false)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (!anyName && ext is not (".fbx" or ".glb" or ".gltf" or ".dae" or ".bvh")) return new(false, false);
        try { using var f = File.OpenRead(file); return Detect(f, ext, long.MaxValue); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(false, false); }
    }

    /// <summary>Wie oben, auf einem Datenstrom (z. B. ZIP-Eintrag), höchstens <paramref name="max"/> Bytes. Wirft InvalidDataException, wenn mehr kommt.</summary>
    public static Info Detect(Stream s, string ext, long max)
    {
        bool rig = false, mesh = false, anim = false; long total = 0;
        var buf = new byte[1 << 20]; const int keep = 16; var carry = 0; int n;
        while ((n = s.Read(buf, carry, buf.Length - carry)) > 0)
        {
            total += n; if (total > max) throw new InvalidDataException("Eintrag zu groß zum Prüfen");
            var span = buf.AsSpan(0, carry + n);
            rig |= span.IndexOf(Rig) >= 0;
            mesh |= span.IndexOf(BinMesh) >= 0 || span.IndexOf(AsciiMesh) >= 0 || (ext is ".glb" or ".gltf" && span.IndexOf("\"meshes\""u8) >= 0);
            anim |= span.IndexOf(BinCurve) >= 0 || span.IndexOf(AsciiCurve) >= 0 || (ext is ".glb" or ".gltf" && span.IndexOf("\"animations\""u8) >= 0) || ext == ".bvh";
            if (rig && mesh && anim) break;
            carry = Math.Min(keep, span.Length); span[^carry..].CopyTo(buf);
        }
        return new(rig, rig && mesh, rig && anim);
    }

    /// <summary>Figur oder Animation? Mit Kurven ist es eine Animation, wenn der Name eine Aktion verrät oder kein Mesh dabei ist („With Skin“);
    /// eine Figur ohne Aktionsnamen bleibt Figur (Mixamo legt auch Figuren-Downloads eine T-Haltung als Kurve bei). Änderbar in der Oberfläche.</summary>
    public static string KindOf(string file, Info i) => !i.HasMesh || (i.HasAnim && Guess(file) is not null) ? "animation" : "figur";

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
        // auch unter anderem Namen übernommen (Gleichnamiges bekommt „-2“): dann zählt der Ursprungsname aus dem Download-Ordner
        return Assets(d).Any(a => (Path.GetFileName(S(a["file"])).Equals(name, StringComparison.OrdinalIgnoreCase) && a["bytes"]?.GetValue<long>() == len)
            || (S(a["origin"]).Equals(name, StringComparison.OrdinalIgnoreCase) && a["originBytes"]?.GetValue<long>() == len));
    }

    /// <summary>Mixamo-Datei ins Spiel übernehmen: Kopie in den Asset-Ordner, Eintrag mit Quelle Mixamo und Lizenz, Art (Figur oder Animation) und erkannter Aktion.
    /// Eine Animation, deren Aktion noch frei ist, wird ihr gleich zugeordnet. Liefert null, wenn die Datei kein Mixamo-Skelett hat oder schon übernommen ist.</summary>
    public static JsonObject? Take(JsonObject d, string assetDir, string file, Info? known = null)
    {
        var info = known ?? Detect(file);
        if (!info.IsMixamo) return null;
        if (Known(d, file)) return null;
        var len = new FileInfo(file).Length;
        var e = GameAssets.Add(d, assetDir, file, Source, License, "Mixamo (Adobe)", Url, "datei");
        e["mixamo"] = true; e["bytes"] = len;
        Classify(d, e, KindOf(file, info));
        return e;
    }

    static void Classify(JsonObject d, JsonObject e, string kind)
    {
        var file = S(e["file"]); e["kind"] = kind;
        if (kind == "animation")
        {
            if (S(d["mixamoFigure"]) == file) d.Remove("mixamoFigure");
            if (Guess(file) is { } act) { e["action"] = act; if (!Assignments(d).ContainsKey(act)) Assign(d, act, file); }
        }
        else
        {
            e.Remove("action");
            if (d["mixamoActions"] is JsonObject o) foreach (var k in o.Where(kv => S(kv.Value) == file).Select(kv => kv.Key).ToList()) o.Remove(k);
            if (S(d["mixamoFigure"]).Length == 0) d["mixamoFigure"] = file;
        }
    }

    /// <summary>Art eines übernommenen Eintrags ändern (Figur ↔ Animation); Zuordnungen und gewählte Figur werden angepasst.</summary>
    public static void SetKind(JsonObject d, string file, string kind)
    {
        if (kind is not ("figur" or "animation")) throw new ArgumentException("Unbekannte Art.");
        var e = Assets(d).FirstOrDefault(a => S(a["file"]) == file) ?? throw new ArgumentException("Nicht in der Asset-Liste.");
        Classify(d, e, kind);
    }

    /// <summary>Mixamo-Paket (ZIP, z. B. „Motion Pack“): enthaltene Mixamo-FBX in <paramref name="dir"/> entpacken, je Eintrag höchstens <paramref name="maxEntry"/> Bytes,
    /// ohne Pfad-Ausbruch. Liefert die entpackten Dateien.</summary>
    public static List<string> FromZip(string zip, string dir, long maxEntry = 512L << 20)
    {
        var res = new List<string>();
        using var z = System.IO.Compression.ZipFile.OpenRead(zip);
        foreach (var e in z.Entries.Where(e => Path.GetExtension(e.Name).Equals(".fbx", StringComparison.OrdinalIgnoreCase) && e.Length is > 0))
        {
            if (e.Length > maxEntry) continue;
            Info i;
            using (var s = e.Open()) i = Detect(s, ".fbx", maxEntry);
            if (!i.IsMixamo) continue;
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, Path.GetFileName(e.Name));
            using (var s = e.Open()) using (var o = File.Create(target)) { CopyLimited(s, o, maxEntry); }
            res.Add(target);
        }
        return res;
    }

    static void CopyLimited(Stream from, Stream to, long max)
    {
        var buf = new byte[1 << 16]; long total = 0; int n;
        while ((n = from.Read(buf, 0, buf.Length)) > 0) { total += n; if (total > max) throw new InvalidDataException("Eintrag zu groß."); to.Write(buf, 0, n); }
    }

    /// <summary>Kurzer Stand für die Oberfläche.</summary>
    public static string Summary(JsonObject d)
    {
        var a = Assets(d); var figs = a.Count(x => S(x["kind"]) == "figur"); var anims = a.Count - figs; var done = Assignments(d).Count;
        return string.Create(CultureInfo.GetCultureInfo("de-DE"), $"{figs} Figur(en), {anims} Animation(en), {done} von {Actions.Length} Aktionen belegt");
    }
}
