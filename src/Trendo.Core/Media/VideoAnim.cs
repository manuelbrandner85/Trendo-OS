using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Pilot 4.0, V5: Keyframes für Lage, Größe, Deckkraft und Tonpegel. V6: Effektstapel je Stück. Die Werte liegen unter „anim“ und „fxstack“ (nach Stück-Kennung); alte Projekte haben beides nicht und exportieren unverändert.</summary>
public static class VideoAnim
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.####", Inv);

    public sealed record Param(string Id, string Label, double Min, double Max, double Default);
    public static readonly Param[] Params =
    [
        new("x", "Lage waagerecht", -1, 1, 0), new("y", "Lage senkrecht", -1, 1, 0), new("scale", "Größe", 0.1, 4, 1), new("opacity", "Deckkraft", 0, 1, 1), new("vol", "Tonpegel", 0, 2, 1),
    ];
    public static Param ParamOf(string id) => Params.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("Unbekannter Wert: " + id);

    public sealed record Key(double Time, double Value);

    static JsonObject? Node(JsonObject p, string id) => (p["anim"] as JsonObject)?[id] as JsonObject;

    public static List<Key> Keys(JsonObject p, string id, string param) =>
        ((Node(p, id)?[param] as JsonArray) ?? []).OfType<JsonArray>().Where(a => a.Count == 2).Select(a => new Key(Num(a[0]), Num(a[1]))).OrderBy(k => k.Time).ToList();
    static double Num(JsonNode? n) => n is not null && double.TryParse(n.ToJsonString(), NumberStyles.Float, Inv, out var v) ? v : 0;

    /// <summary>Keyframe setzen; liegt an dieser Stelle schon einer, wird er ersetzt. Werte außerhalb des erlaubten Bereichs werden begrenzt.</summary>
    public static void SetKey(JsonObject p, string id, string param, double time, double value)
    {
        var def = ParamOf(param);
        if (!double.IsFinite(time) || !double.IsFinite(value)) throw new ArgumentException("Ungültiger Wert.");
        var keys = Keys(p, id, param).Where(k => Math.Abs(k.Time - time) > 0.005).Append(new Key(Math.Max(0, Math.Round(time, 3)), Math.Clamp(value, def.Min, def.Max))).OrderBy(k => k.Time).ToList();
        var all = p["anim"] as JsonObject ?? (JsonObject)(p["anim"] = new JsonObject());
        var node = all[id] as JsonObject ?? (JsonObject)(all[id] = new JsonObject());
        node[param] = new JsonArray([.. keys.Select(k => (JsonNode)new JsonArray(k.Time, Math.Round(k.Value, 4)))]);
    }

    public static void RemoveKey(JsonObject p, string id, string param, int index)
    {
        var keys = Keys(p, id, param);
        if (index < 0 || index >= keys.Count) return;
        keys.RemoveAt(index);
        var all = (JsonObject)p["anim"]!; var node = (JsonObject)all[id]!;
        if (keys.Count == 0) { node.Remove(param); if (node.Count == 0) all.Remove(id); if (all.Count == 0) p.Remove("anim"); return; }
        node[param] = new JsonArray([.. keys.Select(k => (JsonNode)new JsonArray(k.Time, k.Value))]);
    }

    /// <summary>Wert zur Zeit t, linear zwischen den Keyframes. Vor dem ersten und nach dem letzten gilt dessen Wert.</summary>
    public static double ValueAt(IReadOnlyList<Key> keys, double t, double fallback)
    {
        if (keys.Count == 0) return fallback;
        if (t <= keys[0].Time) return keys[0].Value;
        for (var i = 1; i < keys.Count; i++)
            if (t <= keys[i].Time) return keys[i - 1].Value + (keys[i].Value - keys[i - 1].Value) * (t - keys[i - 1].Time) / (keys[i].Time - keys[i - 1].Time);
        return keys[^1].Value;
    }

    /// <summary>FFmpeg-Ausdruck für den Verlauf; <paramref name="t"/> ist die Zeitvariable, auch als Ausdruck wie „(t-2)“.</summary>
    public static string Expr(IReadOnlyList<Key> keys, double fallback, string t = "t")
    {
        if (keys.Count == 0) return F(fallback);
        var e = F(keys[^1].Value);
        for (var i = keys.Count - 1; i >= 1; i--)
        {
            var (a, b) = (keys[i - 1], keys[i]);
            e = $"if(lte({t}\\,{F(b.Time)})\\,{F(a.Value)}+({F(b.Value - a.Value)})*({t}-{F(a.Time)})/{F(b.Time - a.Time)}\\,{e})";
        }
        return $"if(lte({t}\\,{F(keys[0].Time)})\\,{F(keys[0].Value)}\\,{e})";
    }

    /// <summary>Tonpegel-Verlauf als Filterstück mit führendem Komma, leer ohne Keyframes.</summary>
    public static string VolumeFilter(JsonObject p, string id) => Keys(p, id, "vol") is { Count: > 0 } k ? $",volume='{Expr(k, 1)}':eval=frame" : "";

    static string OpacityFilter(List<Key> k, string t) => k.Count == 0 ? "" : $",format=yuva420p,geq=lum='p(X\\,Y)':cb='p(X\\,Y)':cr='p(X\\,Y)':a='255*({Expr(k, 1, t)})'";

    /// <summary>Hat das Stück Keyframes für Lage, Größe oder Deckkraft?</summary>
    public static bool HasTransform(JsonObject p, string id) => new[] { "x", "y", "scale", "opacity" }.Any(x => Keys(p, id, x).Count > 0);

    /// <summary>Bewegt ein Stück der Bildspur auf schwarzem Grund: Filterzeilen von <paramref name="input"/> nach <paramref name="output"/>.</summary>
    public static IEnumerable<string> ClipTransform(JsonObject p, string id, string input, string output, int w, int h, int fps, double length)
    {
        var s = Expr(Keys(p, id, "scale"), 1); var x = Expr(Keys(p, id, "x"), 0); var y = Expr(Keys(p, id, "y"), 0);
        yield return $"color=c=black:s={w}x{h}:r={fps}:d={F(length)},format=yuv420p[{output}bg]";
        yield return $"[{input}]scale=w='max(2\\,trunc({w}*({s})/2)*2)':h='max(2\\,trunc({h}*({s})/2)*2)':eval=frame{OpacityFilter(Keys(p, id, "opacity"), "T")}[{output}fg]";
        yield return $"[{output}bg][{output}fg]overlay=x='(W-w)/2+({x})*W':y='(H-h)/2+({y})*H':eval=frame:shortest=1,format=yuv420p,setsar=1[{output}]";
    }

    /// <summary>Einblendung: Größe und Deckkraft als Filterstück, Lage als Ausdruck. <paramref name="t"/> ist die Zeit ab dem Start der Einblendung.</summary>
    public static string OverlaySize(JsonObject p, string id, string t) => Keys(p, id, "scale") is { Count: > 0 } k ? $",scale=w='max(2\\,trunc(iw*({Expr(k, 1, t)})/2)*2)':h=-2:eval=frame" : "";
    public static string OverlayOpacity(JsonObject p, string id, string t) => OpacityFilter(Keys(p, id, "opacity"), t);
    public static string? OverlayPos(JsonObject p, string id, string param, string t) => Keys(p, id, param) is { Count: > 0 } k ? Expr(k, 0, t) : null;

    // ---------- V6: Effektstapel ----------
    public sealed record EffectType(string Id, string Label, Func<double, string> Filter);
    public static readonly EffectType[] Effects =
    [
        new("blur", "Weichzeichnen", a => $"gblur=sigma={F(0.5 + a * 20)}"),
        new("sharp", "Schärfen", a => $"unsharp=5:5:{F(a * 3)}"),
        new("vignette", "Vignette", a => $"vignette=angle={F(0.1 + a * 1.2)}"),
        new("noise", "Körnung", a => $"noise=alls={F(a * 60)}:allf=t"),
        new("mirror", "Spiegeln", _ => "hflip"),
        new("gray", "Schwarzweiß", _ => "hue=s=0"),
        new("invert", "Negativ", _ => "negate"),
    ];

    public sealed record StackItem(string Id, string Type, bool On, double Amount);

    public static List<StackItem> Stack(JsonObject p, string clip) => ((p["fxstack"] as JsonObject)?[clip] as JsonArray ?? []).OfType<JsonObject>()
        .Select(o => new StackItem((string?)o["id"] ?? "", (string?)o["type"] ?? "", (bool?)o["on"] ?? true, Num(o["amount"]))).Where(i => Effects.Any(e => e.Id == i.Type)).ToList();

    static void Store(JsonObject p, string clip, List<StackItem> l)
    {
        var all = p["fxstack"] as JsonObject ?? (JsonObject)(p["fxstack"] = new JsonObject());
        if (l.Count == 0) { all.Remove(clip); if (all.Count == 0) p.Remove("fxstack"); return; }
        all[clip] = new JsonArray([.. l.Select(i => (JsonNode)new JsonObject { ["id"] = i.Id, ["type"] = i.Type, ["on"] = i.On, ["amount"] = Math.Round(i.Amount, 3) })]);
    }

    public static void AddEffect(JsonObject p, string clip, string type)
    {
        if (!Effects.Any(e => e.Id == type)) throw new ArgumentException("Unbekannter Effekt: " + type);
        Store(p, clip, [.. Stack(p, clip), new StackItem(Guid.NewGuid().ToString("N")[..6], type, true, 0.3)]);
    }

    static void Change(JsonObject p, string clip, string id, Func<StackItem, StackItem?> f) => Store(p, clip, Stack(p, clip).Select(i => i.Id == id ? f(i) : i).OfType<StackItem>().ToList());
    public static void RemoveEffect(JsonObject p, string clip, string id) => Change(p, clip, id, _ => null);
    public static void ToggleEffect(JsonObject p, string clip, string id) => Change(p, clip, id, i => i with { On = !i.On });
    public static void SetAmount(JsonObject p, string clip, string id, double amount) => Change(p, clip, id, i => i with { Amount = Math.Clamp(amount, 0, 1) });

    /// <summary>Effekt in der Reihenfolge verschieben (die Reihenfolge wirkt auf das Ergebnis).</summary>
    public static void MoveEffect(JsonObject p, string clip, string id, int by)
    {
        var l = Stack(p, clip); var i = l.FindIndex(x => x.Id == id); var to = i + by;
        if (i < 0 || to < 0 || to >= l.Count) return;
        (l[i], l[to]) = (l[to], l[i]); Store(p, clip, l);
    }

    /// <summary>Filterkette des Stapels mit führendem Komma, leer ohne aktive Effekte.</summary>
    public static string StackFilter(JsonObject p, string clip)
    {
        var sb = new StringBuilder();
        foreach (var i in Stack(p, clip).Where(x => x.On)) sb.Append(',').Append(Effects.First(e => e.Id == i.Type).Filter(i.Amount));
        return sb.ToString();
    }
}
