using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0630: Engine und Werkzeuge wählt das Team selbst, niemand muss etwas auswählen. Claude oder die lokale KI begründen die Wahl in der Spielbibel
/// (engine, engineReason, blender); ohne brauchbare Angabe entscheiden feste Regeln nach Umfang, Stil und Plattform. Gewählt wird nur, was auf diesem PC installiert ist.
/// Route ist das, was Trendos eigener Bau daraus machen kann; Claude arbeitet direkt in der gewählten Engine.</summary>
public static class GameTools
{
    public sealed record Available(bool Unreal, bool Unity, bool Godot, bool Blender, bool Claude = false);
    public sealed record Choice(string Engine, string Route, bool Blender, string Reason, string Note);

    public static readonly string[] Engines = ["browser", "godot", "unity", "unreal"];

    public static string NameOf(string engine) => engine switch
    {
        "unreal" => "Unreal Engine 5", "unity" => "Unity", "godot" => "Godot", _ => "Browser",
    };

    static string S(JsonNode? n) => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>().Trim() : "";

    static bool Usable(string engine, Available a) => engine switch
    {
        "unreal" => a.Unreal, "unity" => a.Unity, "godot" => a.Godot, _ => true,
    };

    /// <summary>Reihenfolge nach Bedarf des Spiels: die erste installierte Engine gewinnt.</summary>
    static (string[] Order, string Reason) Rule(JsonObject d)
    {
        var scale = S(d["scale"]);
        var text = string.Join(" ", S(d["genre"]), S(d["style"]), S(d["logline"])).ToLowerInvariant();
        var platforms = S(d["platforms"]).ToLowerInvariant();
        var photoreal = text.Contains("fotoreal") || (text.Contains("realistisch") && !text.Contains("unrealistisch"));
        if (platforms.Contains("smartphone") || platforms.Contains("handy") || platforms.Contains("mobil") || platforms.Contains("android") || platforms.Contains("ios"))
            return (["unity", "godot", "unreal"], "läuft gut auf Smartphones");
        if (scale is "aa" or "aaa" || photoreal || text.Contains("open world") || text.Contains("open-world"))
            return (["unreal", "unity", "godot"], "große oder fotorealistische 3D-Welt");
        if (scale == "indie3d") return (["godot", "unity", "unreal"], "kleines 3D-Spiel mit eigenem Stil");
        if (platforms.Contains("browser")) return (["browser", "godot", "unity"], "sofort im Browser spielbar");
        return (["godot", "browser", "unity"], "2D-Spiel für den PC");
    }

    /// <summary>Entscheidet Engine und Blender für das Spiel. Eine Wahl der KI zählt, wenn sie gültig und installiert ist.</summary>
    public static Choice Decide(JsonObject d, Available a)
    {
        var (order, ruleReason) = Rule(d);
        var wanted = Engines.Contains(S(d["engine"])) ? S(d["engine"]) : "";
        var candidates = wanted.Length > 0 ? new[] { wanted }.Concat(order.Where(e => e != wanted)).ToArray() : order;
        var engine = candidates.FirstOrDefault(e => Usable(e, a)) ?? candidates[0];
        var notes = new List<string>();
        if (!Usable(engine, a)) notes.Add($"{NameOf(engine)} ist auf diesem PC nicht installiert. Installiere sie von der offiziellen Seite, Trendo lädt keine Engine selbst.");
        else if (wanted.Length > 0 && wanted != engine) notes.Add($"{NameOf(wanted)} ist nicht installiert, darum {NameOf(engine)}.");
        var reason = wanted == engine && S(d["engineReason"]).Length > 0 ? S(d["engineReason"]) : ruleReason;

        var scaleEngine = GameDesign.ScaleOf(S(d["scale"])).Engine;
        var route = engine is "godot" or "unity" && S(d["browser"]) == "plattform" && scaleEngine == "browser" ? engine : scaleEngine;
        if (route != engine)
            notes.Add(a.Claude ? $"Claude baut es direkt in {NameOf(engine)}, der Knopf „Bauen“ erzeugt daraus {NameOf(route)}." : $"Ohne Claude entsteht es beim Bauen als {NameOf(route)}, in {NameOf(engine)} baut es erst Claude.");

        var needsModels = engine is "unreal" or "unity" || (engine == "godot" && scaleEngine != "browser");
        var blender = needsModels && a.Blender && d["blender"]?.GetValueKind() != JsonValueKind.False;
        if (needsModels && !a.Blender) notes.Add("Blender ist nicht installiert, 3D-Modelle kommen dann aus freien Quellen.");
        return new Choice(engine, route, blender, reason, string.Join(" ", notes));
    }

    /// <summary>Hält die Entscheidung im Dokument fest, damit Fachleute und spätere Läufe dieselbe Engine sehen.</summary>
    public static Choice Apply(JsonObject d, Available a)
    {
        var c = Decide(d, a);
        d["engine"] = c.Engine;
        d["engineReason"] = c.Reason;
        d["blender"] = c.Blender;
        return c;
    }

    /// <summary>Eine Zeile für die Oberfläche.</summary>
    public static string Line(Choice c) =>
        $"Automatisch gewählt: {NameOf(c.Engine)}{(c.Blender ? ", 3D-Modelle mit Blender" : "")}, weil: {c.Reason}.{(c.Note.Length > 0 ? " " + c.Note : "")}";

    /// <summary>Anweisung an Claude oder die lokale KI: selbst entscheiden, nicht nachfragen, nur Installiertes wählen.</summary>
    public static string Brief(Available a)
    {
        var here = new[] { (a.Unreal, "Unreal Engine 5"), (a.Unity, "Unity"), (a.Godot, "Godot"), (a.Blender, "Blender") }.Where(x => x.Item1).Select(x => x.Item2).ToList();
        return "\n\nEngine und Werkzeuge entscheidest du selbst und fragst dafür nicht nach. Auf diesem PC vorhanden: " + (here.Count > 0 ? string.Join(", ", here) : "keine Engine") + ", dazu immer der Browser. " +
               "Liefere in der Spielbibel engine (browser, godot, unity oder unreal), engineReason (ein kurzer Satz) und blender (true oder false). " +
               "Richtwerte: große oder fotorealistische 3D-Welten Unreal, Smartphone Unity, kleines 3D oder 2D für den PC Godot, sofort spielbar im Browser. Blender, wenn eigene 3D-Modelle nötig sind.";
    }
}
