using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Was eine Aktion von der KI braucht (A7).</summary>
public enum KiBedarf { Text, Bild, Code }

/// <summary>Eine benannte Editor-Aktion: Name, was sie tut, was sie von der KI braucht, ihre erlaubten Angaben, ob sie auf eine Auswahl beschränkt werden kann.</summary>
public sealed record WebAktion(string Name, string Beschreibung, KiBedarf Bedarf, IReadOnlyList<string> Pflicht, IReadOnlyList<string> Optional, bool FuerAuswahl = false);

/// <summary>Beschränkung eines Plans auf ausgewählte Bausteine (K1).</summary>
public sealed record WebPlanScope(IReadOnlySet<string> Ids);

/// <summary>Ergebnis eines Plans. <see cref="Error"/> gesetzt: der Plan wurde ganz abgelehnt, <see cref="Json"/> ist null und nichts wurde geschrieben.</summary>
public sealed record WebPlanResult(string? Json, string? Error, IReadOnlyList<string> Changes, IReadOnlyList<string> States, IReadOnlyList<KiBedarf> Needs)
{
    public static WebPlanResult Fail(string error) => new(null, error, [], [], []);
}

/// <summary>A1: Jede Editor-Funktion als benannte, geprüfte Aktion. Die KI liefert einen JSON-Plan aus Aktionen, der Mensch nutzt dieselben Aktionen (Text direkt ändern, Stand speichern).
/// Ein Plan wird ganz oder gar nicht ausgeführt; das Ergebnis hängt nur von Plan und Seite ab, nicht davon, wer den Plan geliefert hat.</summary>
public static partial class WebDesignActions
{
    public const int MaxActions = 200, MaxTextChars = 5000;

    public static readonly IReadOnlyList<WebAktion> Catalog =
    [
        new("seite_anlegen", "Beginnt eine neue, leere Seite. Nur als erste Aktion erlaubt.", KiBedarf.Text, ["titel"], ["beschreibung"]),
        new("seite_beschreiben", "Ändert Titel oder Beschreibung für Suchmaschinen.", KiBedarf.Text, [], ["titel", "beschreibung"]),
        new("baustein_einfuegen", "Fügt einen Baustein ein: typ, optional eigene id, props, in (id eines Bereichs oder Spalten-Bausteins), slot und position.", KiBedarf.Text, ["typ"], ["id", "props", "in", "slot", "position"]),
        new("element_aendern", "Ändert Felder eines Bausteins.", KiBedarf.Text, ["id", "props"], [], true),
        new("element_entfernen", "Entfernt einen Baustein samt Inhalt.", KiBedarf.Text, ["id"], []),
        new("element_verschieben", "Verschiebt einen Baustein an eine andere Stelle.", KiBedarf.Text, ["id", "position"], ["in", "slot"]),
        new("ansicht_anpassen", "Legt fest, in welcher Geräte-Ansicht der Editor die Seite zeigt: handy, tablet oder desktop.", KiBedarf.Text, ["ansicht"], []),
        new("gestaltungswert_setzen", "Setzt einen zentralen Wert: accent, ink, paper (#RRGGBB), font (klar, serif, rund, mono), radius (0 bis 40), space (4 bis 48).", KiBedarf.Text, ["name", "wert"], []),
        new("animation_setzen", "Setzt die Bewegung eines Bausteins, der sie kann: bewegung (keine, einblenden, schweben), dauer (0,2 bis 5 Sekunden).", KiBedarf.Text, ["id", "bewegung"], ["dauer"], true),
        new("bild_setzen", "Setzt die Bilddatei eines Bild-Bausteins samt Alternativtext. Die KI muss das Bild sehen können.", KiBedarf.Bild, ["id", "src", "alt"], [], true),
        new("stand_speichern", "Speichert das Ergebnis des Plans als benannten Stand.", KiBedarf.Text, ["name"], []),
    ];

    enum Art { Text, Link, Bild, Farbe, Wahl, Zahl, Slot }
    sealed record Feld(Art Art, string[]? Werte = null, double Min = 0, double Max = 0);
    sealed record Baustein(Dictionary<string, Feld> Felder, Dictionary<string, JsonNode?> Standard);

    static Dictionary<string, JsonNode?> D(params (string K, JsonNode? V)[] v) => v.ToDictionary(x => x.K, x => x.V);
    static readonly Feld T = new(Art.Text), L = new(Art.Link), Motion = new(Art.Wahl, ["keine", "einblenden", "schweben"]), Seconds = new(Art.Zahl, Min: 0.2, Max: 5);

    /// <summary>Die acht Bausteine des Editors mit ihren Feldern und Standardwerten (wie in <c>tools/webdesign-editor/src/main.jsx</c>).</summary>
    static readonly Dictionary<string, Baustein> Bausteine = new()
    {
        ["Kopfbereich"] = new(new() { ["title"] = T, ["sub"] = T, ["cta"] = T, ["href"] = L, ["motion"] = Motion, ["seconds"] = Seconds },
            D(("title", "Willkommen"), ("sub", "Eine Seite, gebaut mit Trendo."), ("cta", "Mehr erfahren"), ("href", "#"), ("motion", "einblenden"), ("seconds", 0.9))),
        ["Ueberschrift"] = new(new() { ["text"] = T, ["level"] = new(Art.Wahl, ["h2", "h3"]) }, D(("text", "Überschrift"), ("level", "h2"))),
        ["Text"] = new(new() { ["text"] = T, ["align"] = new(Art.Wahl, ["left", "center"]) }, D(("text", "Hier steht dein Text."), ("align", "left"))),
        ["Bild"] = new(new() { ["src"] = new(Art.Bild), ["alt"] = T }, D(("src", ""), ("alt", ""))),
        ["Knopf"] = new(new() { ["label"] = T, ["href"] = L }, D(("label", "Kontakt"), ("href", "#"))),
        ["Bereich"] = new(new() { ["content"] = new(Art.Slot), ["tone"] = new(Art.Wahl, ["hell", "dunkel"]) }, D(("tone", "hell"))),
        ["Spalten"] = new(new() { ["left"] = new(Art.Slot), ["right"] = new(Art.Slot) }, D()),
        ["Szene3D"] = new(new() { ["form"] = new(Art.Wahl, ["wuerfel", "kugel", "ring"]), ["color"] = new(Art.Farbe), ["spin"] = new(Art.Zahl, Min: 0, Max: 3), ["height"] = new(Art.Zahl, Min: 160, Max: 900) },
            D(("form", "ring"), ("color", "#d4a73a"), ("spin", 0.6), ("height", 380))),
    };

    public static IReadOnlyCollection<string> ComponentNames => Bausteine.Keys;

    /// <summary>Bausteine mit ihren Feldern als Text für die KI.</summary>
    public static string ComponentsText() => string.Join("\n", Bausteine.Select(b =>
        $"- {b.Key}: " + string.Join(", ", b.Value.Felder.Select(f => f.Key + f.Value.Art switch
        {
            Art.Wahl => " (" + string.Join("|", f.Value.Werte!) + ")",
            Art.Zahl => $" ({f.Value.Min.ToString(CultureInfo.InvariantCulture)} bis {f.Value.Max.ToString(CultureInfo.InvariantCulture)})",
            Art.Slot => " (Slot)",
            Art.Farbe => " (#RRGGBB)",
            Art.Link => " (Ziel)",
            Art.Bild => " (Datei im Projekt)",
            _ => "",
        }))));

    /// <summary>Liest den Plan aus einer KI-Antwort. Roher Code oder Text ohne Plan wird abgelehnt.</summary>
    public static (JsonObject? Plan, string? Error) Parse(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return (null, "Die KI hat nichts geliefert.");
        var fence = Regex.Match(answer, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        var a = answer.IndexOf('{'); var b = answer.LastIndexOf('}');
        var text = fence.Success ? fence.Groups[1].Value : a >= 0 && b > a ? answer[a..(b + 1)] : null;
        if (text is null) return (null, "Die KI hat keinen Aktionsplan geliefert.");
        try { return JsonNode.Parse(text) is JsonObject o && o["aktionen"] is JsonArray ? (o, null) : (null, "Die KI hat keinen Aktionsplan geliefert."); }
        catch (JsonException) { return (null, "Der Aktionsplan der KI ist kein gültiges JSON."); }
    }

    /// <summary>Plan aus einer KI-Antwort oder als JSON-Text prüfen und auf eine Kopie der Seite anwenden.</summary>
    public static WebPlanResult Apply(string pageJson, string planAnswer, WebPlanScope? scope = null)
    {
        var (plan, err) = Parse(planAnswer);
        return plan is null ? WebPlanResult.Fail(err!) : Apply(pageJson, plan, scope);
    }

    /// <summary>Führt alle Aktionen auf einer Kopie aus. Die erste ungültige Aktion verwirft den ganzen Plan.</summary>
    public static WebPlanResult Apply(string pageJson, JsonObject plan, WebPlanScope? scope = null)
    {
        foreach (var kv in plan) if (kv.Key != "aktionen") return WebPlanResult.Fail($"Unbekannte Angabe im Plan: „{kv.Key}“.");
        var (valid, verr) = WebDesign.Validate(pageJson); if (valid is null) return WebPlanResult.Fail(verr!);
        if (plan["aktionen"] is not JsonArray actions || actions.Count == 0) return WebPlanResult.Fail("Der Plan enthält keine Aktionen.");
        if (actions.Count > MaxActions) return WebPlanResult.Fail($"Der Plan ist zu lang (höchstens {MaxActions} Aktionen).");
        var doc = JsonNode.Parse(valid)!.AsObject();
        var changes = new List<string>(); var states = new List<string>(); var needs = new List<KiBedarf>();
        for (var i = 0; i < actions.Count; i++)
        {
            var nr = i + 1;
            if (actions[i] is not JsonObject a) return WebPlanResult.Fail($"Aktion {nr} ist kein Objekt.");
            var name = S(a["aktion"]);
            var def = Catalog.FirstOrDefault(x => x.Name == name);
            if (def is null) return WebPlanResult.Fail($"Aktion {nr}: unbekannte Aktion „{name ?? "(ohne Namen)"}“.");
            foreach (var kv in a) if (kv.Key != "aktion" && !def.Pflicht.Contains(kv.Key) && !def.Optional.Contains(kv.Key)) return WebPlanResult.Fail($"Aktion {nr} ({name}): unbekannte Angabe „{kv.Key}“.");
            foreach (var p in def.Pflicht) if (a[p] is null) return WebPlanResult.Fail($"Aktion {nr} ({name}): „{p}“ fehlt.");
            if (scope is not null && !def.FuerAuswahl) return WebPlanResult.Fail($"Aktion {nr}: „{name}“ würde mehr als die Auswahl ändern.");
            try
            {
                if (scope is not null)
                {
                    var target = S(a["id"]) ?? throw new PlanError("„id“ fehlt.");
                    if (!InScope(doc, scope, target)) throw new PlanError($"Baustein „{target}“ gehört nicht zur Auswahl.");
                }
                if (name == "seite_anlegen" && i != 0) throw new PlanError("nur als erste Aktion erlaubt.");
                doc = Run(name!, a, doc, changes, states);
            }
            catch (PlanError e) { return WebPlanResult.Fail($"Aktion {nr} ({name}): {e.Message}"); }
            if (!needs.Contains(def.Bedarf)) needs.Add(def.Bedarf);
        }
        var (final, ferr) = WebDesign.Validate(doc.ToJsonString());
        return final is null ? WebPlanResult.Fail(ferr!) : new(final, null, changes, states, needs);
    }

    /// <summary>Übernimmt ein Ergebnis als einen Schritt: erst ein automatischer Stand der bisherigen Fassung, dann Schreiben, dann die im Plan benannten Stände. Liefert die Nummer des automatischen Stands.</summary>
    public static (string? AutoId, string? Error) Commit(string projectDir, WebPlanResult result, string baseJson, string label, string? variant = null)
    {
        if (result.Json is null) return (null, result.Error ?? "Nichts zu übernehmen.");
        var (auto, err) = WebDesignStates.SaveContent(projectDir, "Vor KI: " + label, baseJson, true, variant);
        if (auto is null) return (null, err);
        var write = WebDesignStates.Write(projectDir, result.Json, variant);
        if (write is not null) return (null, write);
        foreach (var n in result.States) WebDesignStates.SaveContent(projectDir, n, result.Json, false, variant);
        return (auto.Id, null);
    }

    sealed class PlanError(string message) : Exception(message);

    static string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : null;
    static string NeedS(JsonNode? n, string what) => S(n) is { Length: > 0 } s ? (s.Length > MaxTextChars ? throw new PlanError($"„{what}“ ist zu lang.") : s) : throw new PlanError($"„{what}“ muss ein nicht leerer Text sein.");
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")] private static partial Regex IdPattern();
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")] private static partial Regex HexPattern();
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*:")] private static partial Regex HasScheme();

    /// <summary>Alle Baustein-Listen der Seite: Hauptliste, Slots und ältere Puck-Bereiche.</summary>
    static IEnumerable<JsonArray> Lists(JsonObject doc)
    {
        IEnumerable<JsonArray> Deep(JsonArray list)
        {
            yield return list;
            foreach (var item in list.OfType<JsonObject>())
                if (item["props"] is JsonObject p)
                    foreach (var kv in p)
                        if (kv.Value is JsonArray a && IsBlocks(a)) foreach (var x in Deep(a)) yield return x;
        }
        if (doc["content"] is JsonArray c) foreach (var x in Deep(c)) yield return x;
        if (doc["zones"] is JsonObject z) foreach (var kv in z) if (kv.Value is JsonArray a) foreach (var x in Deep(a)) yield return x;
    }
    static bool IsBlocks(JsonArray a) => a.All(i => i is JsonObject o && o["type"] is not null);
    static string? IdOf(JsonObject block) => S(block["props"]?["id"]);

    static JsonObject Find(JsonObject doc, string id) => Lists(doc).SelectMany(l => l.OfType<JsonObject>()).FirstOrDefault(b => IdOf(b) == id) ?? throw new PlanError($"Baustein „{id}“ gibt es nicht.");

    /// <summary>In der Auswahl sind die gewählten Bausteine und alles, was in ihnen liegt.</summary>
    static bool InScope(JsonObject doc, WebPlanScope scope, string id)
    {
        if (scope.Ids.Contains(id)) return true;
        foreach (var chosen in Lists(doc).SelectMany(l => l.OfType<JsonObject>()).Where(b => IdOf(b) is { } x && scope.Ids.Contains(x)))
            if (chosen["props"] is JsonObject p)
                foreach (var kv in p)
                    if (kv.Value is JsonArray a && IsBlocks(a) && Lists(new JsonObject { ["content"] = a.DeepClone() }).SelectMany(l => l.OfType<JsonObject>()).Any(b => IdOf(b) == id)) return true;
        return false;
    }

    static string TypeOf(JsonObject block) => S(block["type"]) ?? "";
    static Baustein Def(string type) => Bausteine.TryGetValue(type, out var d) ? d : throw new PlanError($"unbekannter Baustein „{type}“.");

    static JsonNode Check(string feld, Feld f, JsonNode? value)
    {
        switch (f.Art)
        {
            case Art.Text:
                if (value is JsonValue v && v.TryGetValue(out string? t)) return t.Length <= MaxTextChars ? JsonValue.Create(t) : throw new PlanError($"„{feld}“ ist zu lang.");
                throw new PlanError($"„{feld}“ muss Text sein.");
            case Art.Wahl:
                return S(value) is { } w && f.Werte!.Contains(w) ? JsonValue.Create(w) : throw new PlanError($"„{feld}“ muss einer von {string.Join(", ", f.Werte!)} sein.");
            case Art.Zahl:
                if (value is JsonValue n && n.TryGetValue(out double d) && d >= f.Min && d <= f.Max) return JsonValue.Create(d);
                throw new PlanError($"„{feld}“ muss eine Zahl von {f.Min.ToString(CultureInfo.InvariantCulture)} bis {f.Max.ToString(CultureInfo.InvariantCulture)} sein.");
            case Art.Farbe:
                return S(value) is { } c && HexPattern().IsMatch(c) ? JsonValue.Create(c) : throw new PlanError($"„{feld}“ muss eine Farbe #RRGGBB sein.");
            case Art.Link:
                return JsonValue.Create(Link(feld, S(value) ?? throw new PlanError($"„{feld}“ muss Text sein.")));
            case Art.Bild:
                return JsonValue.Create(Image(feld, S(value) ?? throw new PlanError($"„{feld}“ muss Text sein.")));
            default: throw new PlanError($"„{feld}“ ist ein Bereich für Bausteine und lässt sich so nicht ändern.");
        }
    }

    /// <summary>Erlaubt sind Sprungziele, Dateien im Projekt, https, http und mailto. Alles andere (javascript:, data: …) wird abgelehnt.</summary>
    static string Link(string feld, string v)
    {
        var s = v.Trim();
        if (HasScheme().IsMatch(s) && !s.StartsWith("https:", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("http:", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            throw new PlanError($"„{feld}“: diese Linkart ist nicht erlaubt.");
        return s;
    }

    /// <summary>Bilder müssen im Projekt liegen: keine Adressen und kein Weg nach oben, sonst läuft die Seite nicht offline.</summary>
    static string Image(string feld, string v)
    {
        var s = v.Trim().Replace('\\', '/');
        if (HasScheme().IsMatch(s) || s.StartsWith("//", StringComparison.Ordinal) || s.StartsWith('/') || s.Split('/').Contains("..")) throw new PlanError($"„{feld}“: das Bild muss als Datei im Projekt liegen.");
        return s;
    }

    static JsonObject Props(string type, JsonNode? input, bool allowEmpty)
    {
        var def = Def(type); var o = new JsonObject();
        if (input is null) return o;
        if (input is not JsonObject p) throw new PlanError("„props“ muss ein Objekt sein.");
        if (p.Count == 0 && !allowEmpty) throw new PlanError("„props“ ist leer.");
        foreach (var kv in p)
        {
            if (!def.Felder.TryGetValue(kv.Key, out var f)) throw new PlanError($"Baustein {type} hat kein Feld „{kv.Key}“.");
            o[kv.Key] = Check(kv.Key, f, kv.Value);
        }
        return o;
    }

    static JsonArray Target(JsonObject doc, JsonNode? parent, JsonNode? slot)
    {
        var id = parent is null ? null : NeedS(parent, "in");
        if (id is null) return slot is null ? doc["content"]!.AsArray() : throw new PlanError("„slot“ braucht „in“.");
        var host = Find(doc, id); var def = Def(TypeOf(host));
        var slots = def.Felder.Where(f => f.Value.Art == Art.Slot).Select(f => f.Key).ToList();
        if (slots.Count == 0) throw new PlanError($"in „{id}“ kann nichts liegen.");
        var name = slot is null ? slots[0] : NeedS(slot, "slot");
        if (!slots.Contains(name)) throw new PlanError($"Baustein „{id}“ hat keinen Bereich „{name}“.");
        var props = host["props"]!.AsObject();
        if (props[name] is not JsonArray list) props[name] = list = [];
        return list;
    }

    static int Position(JsonNode? n, int count)
    {
        if (n is null) return count;
        if (n is JsonValue v && v.TryGetValue(out int p) && p >= 0 && p <= count) return p;
        throw new PlanError($"„position“ muss eine ganze Zahl von 0 bis {count} sein.");
    }

    static string NewId(JsonObject doc, string type)
    {
        var used = Lists(doc).SelectMany(l => l.OfType<JsonObject>()).Select(IdOf).OfType<string>().ToHashSet();
        for (var n = 1; ; n++) if (!used.Contains($"{type}-{n}")) return $"{type}-{n}";
    }

    static JsonObject Run(string name, JsonObject a, JsonObject doc, List<string> changes, List<string> states)
    {
        var root = doc["root"]!.AsObject();
        if (root["props"] is not JsonObject rootProps) root["props"] = rootProps = [];
        switch (name)
        {
            case "seite_anlegen":
            {
                var titel = NeedS(a["titel"], "titel");
                var fresh = JsonNode.Parse(WebDesign.Empty(titel))!.AsObject();
                fresh["root"]!["props"]!["description"] = S(a["beschreibung"]) ?? "";
                changes.Add($"Neue Seite „{titel}“");
                return fresh;
            }
            case "seite_beschreiben":
                if (a["titel"] is null && a["beschreibung"] is null) throw new PlanError("„titel“ oder „beschreibung“ fehlt.");
                if (a["titel"] is { } t) rootProps["title"] = NeedS(t, "titel");
                if (a["beschreibung"] is { } b) rootProps["description"] = S(b) ?? throw new PlanError("„beschreibung“ muss Text sein.");
                changes.Add("Titel oder Beschreibung geändert");
                return doc;
            case "baustein_einfuegen":
            {
                var type = NeedS(a["typ"], "typ"); var def = Def(type);
                var given = Props(type, a["props"], true);
                var id = a["id"] is { } idn ? NeedS(idn, "id") : NewId(doc, type);
                if (!IdPattern().IsMatch(id)) throw new PlanError($"„{id}“ ist keine gültige id.");
                if (Lists(doc).SelectMany(l => l.OfType<JsonObject>()).Any(x => IdOf(x) == id)) throw new PlanError($"id „{id}“ gibt es schon.");
                var props = new JsonObject();
                foreach (var kv in def.Standard) props[kv.Key] = kv.Value?.DeepClone();
                foreach (var kv in def.Felder.Where(f => f.Value.Art == Art.Slot)) props[kv.Key] = new JsonArray();
                foreach (var kv in given) props[kv.Key] = kv.Value?.DeepClone();
                props["id"] = id;
                var list = Target(doc, a["in"], a["slot"]);
                list.Insert(Position(a["position"], list.Count), new JsonObject { ["type"] = type, ["props"] = props });
                changes.Add($"{type} eingefügt ({id})");
                return doc;
            }
            case "element_aendern":
            {
                var id = NeedS(a["id"], "id"); var block = Find(doc, id); var props = block["props"]!.AsObject();
                foreach (var kv in Props(TypeOf(block), a["props"], false)) { props[kv.Key] = kv.Value?.DeepClone(); changes.Add($"{TypeOf(block)} {id}: {kv.Key} geändert"); }
                return doc;
            }
            case "element_entfernen":
            {
                var id = NeedS(a["id"], "id"); var block = Find(doc, id);
                Lists(doc).First(l => l.Contains(block)).Remove(block);
                changes.Add($"{TypeOf(block)} {id} entfernt");
                return doc;
            }
            case "element_verschieben":
            {
                var id = NeedS(a["id"], "id"); var block = Find(doc, id);
                if (a["in"] is { } dest && (S(dest) == id || Lists(new JsonObject { ["content"] = new JsonArray(block.DeepClone()) }).SelectMany(l => l.OfType<JsonObject>()).Any(b => IdOf(b) == S(dest)))) throw new PlanError("ein Baustein kann nicht in sich selbst liegen.");
                var from = Lists(doc).First(l => l.Contains(block));
                from.Remove(block);
                var to = Target(doc, a["in"], a["slot"]);
                to.Insert(Position(a["position"], to.Count), block);
                changes.Add($"{TypeOf(block)} {id} verschoben");
                return doc;
            }
            case "ansicht_anpassen":
            {
                var v = S(a["ansicht"]);
                if (v is not ("handy" or "tablet" or "desktop")) throw new PlanError("„ansicht“ muss handy, tablet oder desktop sein.");
                rootProps["ansicht"] = v; changes.Add($"Ansicht: {v}");
                return doc;
            }
            case "gestaltungswert_setzen":
            {
                var n = NeedS(a["name"], "name");
                JsonNode value = n switch
                {
                    "accent" or "ink" or "paper" => Check("wert", new(Art.Farbe), a["wert"]),
                    "font" => Check("wert", new(Art.Wahl, WebDesign.Fonts), a["wert"]),
                    "radius" => Check("wert", new(Art.Zahl, Min: 0, Max: 40), a["wert"]),
                    "space" => Check("wert", new(Art.Zahl, Min: 4, Max: 48), a["wert"]),
                    _ => throw new PlanError($"Gestaltungswert „{n}“ gibt es nicht."),
                };
                rootProps[n] = value; changes.Add($"Gestaltungswert {n} gesetzt");
                return doc;
            }
            case "animation_setzen":
            {
                var id = NeedS(a["id"], "id"); var block = Find(doc, id); var def = Def(TypeOf(block));
                if (!def.Felder.ContainsKey("motion")) throw new PlanError($"Baustein {TypeOf(block)} kann keine Bewegung.");
                var props = block["props"]!.AsObject();
                props["motion"] = Check("bewegung", def.Felder["motion"], a["bewegung"]);
                if (a["dauer"] is { } d) props["seconds"] = Check("dauer", def.Felder["seconds"], d);
                changes.Add($"{TypeOf(block)} {id}: Bewegung gesetzt");
                return doc;
            }
            case "bild_setzen":
            {
                var id = NeedS(a["id"], "id"); var block = Find(doc, id);
                if (TypeOf(block) != "Bild") throw new PlanError($"„{id}“ ist kein Bild.");
                var src = NeedS(a["src"], "src"); var alt = NeedS(a["alt"], "alt");
                var props = block["props"]!.AsObject();
                props["src"] = Image("src", src); props["alt"] = alt;
                changes.Add($"Bild {id} gesetzt");
                return doc;
            }
            case "stand_speichern":
            {
                var n = NeedS(a["name"], "name"); states.Add(n); changes.Add($"Stand „{n}“ wird gespeichert");
                return doc;
            }
            default: throw new PlanError("nicht umgesetzt.");
        }
    }
}
