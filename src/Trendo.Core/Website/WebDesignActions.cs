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
        new("ansicht_anpassen", "Legt fest, in welcher Geräte-Ansicht der Editor die Seite zeigt: desktop oder der Name einer Ansicht.", KiBedarf.Text, ["ansicht"], []),
        new("ansicht_setzen", "Legt eine Geräte-Ansicht an oder ändert ihre Breite: name und breite (240 bis 1920 Pixel). Sie gilt bis zu dieser Breite.", KiBedarf.Text, ["name", "breite"], []),
        new("ansicht_entfernen", "Entfernt eine Geräte-Ansicht samt ihrer Anpassungen.", KiBedarf.Text, ["name"], []),
        new("anpassung_setzen", "Passt einen Baustein für eine Ansicht und alle kleineren an: werte mit ausblenden (wahr/falsch), ausrichtung (left, center, right), schrift (50 bis 200 Prozent), spalten (1 oder 2, nur Spalten).", KiBedarf.Text, ["id", "ansicht", "werte"], [], true),
        new("anpassung_zuruecksetzen", "Nimmt Anpassungen eines Bausteins in einer Ansicht zurück, optional nur die genannten felder.", KiBedarf.Text, ["id", "ansicht"], ["felder"], true),
        new("gestaltungswert_setzen", "Setzt einen Gestaltungswert für das ganze Projekt: accent, ink, paper, accentDunkel, inkDunkel, paperDunkel (#RRGGBB), dunkel (wahr/falsch), font (klar, serif, rund, mono), radius (0 bis 40), space (4 bis 48).", KiBedarf.Text, ["name", "wert"], []),
        new("animation_setzen", "Setzt die Bewegung eines Bausteins, der sie kann: bewegung (keine, einblenden, schweben), dauer (0,2 bis 5 Sekunden).", KiBedarf.Text, ["id", "bewegung"], ["dauer"], true),
        new("bild_setzen", "Setzt die Bilddatei eines Bild-Bausteins samt Alternativtext. Die KI muss das Bild sehen können.", KiBedarf.Bild, ["id", "src", "alt"], [], true),
        new("stand_speichern", "Speichert das Ergebnis des Plans als benannten Stand.", KiBedarf.Text, ["name"], []),
        new("bewegung_setzen", "Setzt die Bewegung eines Bausteins: erscheinen (keine, einblenden, hochgleiten, zoomen), darueber (keine, anheben, vergroessern, aufhellen), dauer (0,2 bis 3 s), verzoegerung (0 bis 2 s). Bei „weniger Bewegung“ läuft nichts.", KiBedarf.Text, ["id"], ["erscheinen", "darueber", "dauer", "verzoegerung"], true),
        new("zeitleiste_setzen", "Stellt die Zeitleiste eines Bausteins ein: dauer (0,2 bis 30 s), ausloeser (laden, scroll), wiederholen (nein, ja).", KiBedarf.Text, ["id"], ["dauer", "ausloeser", "wiederholen"], true),
        new("schluesselbild_setzen", "Setzt ein Schlüsselbild: eigenschaft (x, y in px; skalierung 0 bis 5; drehung in Grad; deckkraft 0 bis 1), zeit in Sekunden, wert.", KiBedarf.Text, ["id", "eigenschaft", "zeit", "wert"], [], true),
        new("schluesselbild_entfernen", "Entfernt das Schlüsselbild einer Eigenschaft zu einer Zeit.", KiBedarf.Text, ["id", "eigenschaft", "zeit"], [], true),
        new("seitenuebergang_setzen", "Übergang beim Öffnen der Seite: art (keiner, einblenden).", KiBedarf.Text, ["art"], []),
        new("komponente_erstellen", "Macht Bausteine (ids, aus derselben Liste) zu einer Komponente mit name; an ihrer Stelle steht danach eine Verwendung.", KiBedarf.Text, ["name", "ids"], ["id"]),
        new("komponente_definieren", "Legt eine Komponente aus neuen Bausteinen an: name und bausteine (Liste aus typ, id, props).", KiBedarf.Text, ["name", "bausteine"], ["komponente"]),
        new("komponente_einfuegen", "Fügt eine Verwendung einer Komponente ein: komponente (id), optional id, in, slot, position.", KiBedarf.Text, ["komponente"], ["id", "in", "slot", "position"]),
        new("komponente_aendern", "Ändert das Original einer Komponente (wirkt auf alle Verwendungen): komponente, element (id im Original), props.", KiBedarf.Text, ["komponente", "element", "props"], []),
        new("komponente_ueberschreiben", "Überschreibt Felder nur in einer Verwendung: id der Verwendung, element (id im Original), props.", KiBedarf.Text, ["id", "element", "props"], [], true),
        new("komponente_zuruecksetzen", "Nimmt Überschreibungen einer Verwendung zurück, optional nur für element oder felder.", KiBedarf.Text, ["id"], ["element", "felder"], true),
    ];

    enum Art { Text, Link, Bild, Farbe, Wahl, Zahl, Slot, Stufe, Liste, Email, Objekt }
    sealed record Feld(Art Art, string[]? Werte = null, double Min = 0, double Max = 0, Dictionary<string, Feld>? Sub = null);
    static Feld Liste(int max, params (string K, Feld F)[] sub) => new(Art.Liste, Max: max, Sub: sub.ToDictionary(x => x.K, x => x.F));
    static Feld Objekt(params (string K, Feld F)[] sub) => new(Art.Objekt, Sub: sub.ToDictionary(x => x.K, x => x.F));

    /// <summary>W4, Z2: Felder, die jeder Baustein hat.</summary>
    static readonly Dictionary<string, Feld> Allgemein = new()
    {
        ["bewegung"] = Objekt(("erscheinen", new(Art.Wahl, ["keine", "einblenden", "hochgleiten", "zoomen"])), ("darueber", new(Art.Wahl, ["keine", "anheben", "vergroessern", "aufhellen"])),
            ("dauer", new(Art.Zahl, Min: 0.2, Max: 3)), ("verzoegerung", new(Art.Zahl, Min: 0, Max: 2))),
        ["zeitleiste"] = Objekt(("dauer", new(Art.Zahl, Min: 0.2, Max: 30)), ("ausloeser", new(Art.Wahl, ["laden", "scroll"])), ("wiederholen", new(Art.Wahl, ["nein", "ja"])),
            ("punkte", Liste(60, ("zeit", new(Art.Zahl, Min: 0, Max: 30)), ("eigenschaft", new(Art.Wahl, [.. WebDesignMotion.Properties.Keys])), ("wert", new(Art.Zahl, Min: -2000, Max: 2000))))),
    };

    static JsonArray Items(params (string K, JsonNode? V)[][] items) => new(items.Select(i => (JsonNode)new JsonObject(i.Select(x => KeyValuePair.Create(x.K, x.V)))).ToArray());
    sealed record Baustein(Dictionary<string, Feld> Felder, Dictionary<string, JsonNode?> Standard);

    static Dictionary<string, JsonNode?> D(params (string K, JsonNode? V)[] v) => v.ToDictionary(x => x.K, x => x.V);
    static readonly Feld T = new(Art.Text), L = new(Art.Link), Motion = new(Art.Wahl, ["keine", "einblenden", "schweben"]), Seconds = new(Art.Zahl, Min: 0.2, Max: 5);

    /// <summary>Die Bausteine des Editors mit ihren Feldern und Standardwerten (wie in <c>tools/webdesign-editor/src/main.jsx</c>).</summary>
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
        // A5: Stapel mit Abstand als Stufe des Gestaltungswerts statt fester Pixel
        // Z1: Bausteine in Profi-Qualität
        ["Navigation"] = new(new() { ["marke"] = T, ["links"] = Liste(12, ("label", T), ("href", L)), ["knopf"] = T, ["knopfZiel"] = L },
            D(("marke", "Mein Laden"), ("links", Items([("label", "Angebot"), ("href", "#")], [("label", "Über uns"), ("href", "#")], [("label", "Kontakt"), ("href", "#")])), ("knopf", "Termin buchen"), ("knopfZiel", "#"))),
        ["Hero"] = new(new() { ["kicker"] = T, ["titel"] = T, ["text"] = T, ["knopf"] = T, ["knopfZiel"] = L, ["knopf2"] = T, ["knopf2Ziel"] = L, ["bild"] = new(Art.Bild), ["bildAlt"] = T },
            D(("kicker", "Neu"), ("titel", "Ein klares Versprechen in einem Satz"), ("text", "Zwei Sätze, die erklären, was Besucher hier bekommen und warum es ihnen hilft."), ("knopf", "Jetzt starten"), ("knopfZiel", "#"), ("knopf2", "Mehr erfahren"), ("knopf2Ziel", "#"), ("bild", ""), ("bildAlt", ""))),
        ["Preise"] = new(new() { ["titel"] = T, ["tarife"] = Liste(6, ("name", T), ("preis", T), ("zeitraum", T), ("merkmale", T), ("knopf", T), ("ziel", L), ("hervorgehoben", new(Art.Wahl, ["nein", "ja"]))) },
            D(("titel", "Preise"), ("tarife", Items(
                [("name", "Basis"), ("preis", "9 €"), ("zeitraum", "im Monat"), ("merkmale", "Ein Projekt\nE-Mail-Hilfe"), ("knopf", "Basis wählen"), ("ziel", "#"), ("hervorgehoben", "nein")],
                [("name", "Profi"), ("preis", "29 €"), ("zeitraum", "im Monat"), ("merkmale", "Zehn Projekte\nTelefon-Hilfe\nEigene Adresse"), ("knopf", "Profi wählen"), ("ziel", "#"), ("hervorgehoben", "ja")],
                [("name", "Team"), ("preis", "79 €"), ("zeitraum", "im Monat"), ("merkmale", "Unbegrenzte Projekte\nPersönliche Betreuung"), ("knopf", "Team wählen"), ("ziel", "#"), ("hervorgehoben", "nein")])))),
        ["Fragen"] = new(new() { ["titel"] = T, ["fragen"] = Liste(30, ("frage", T), ("antwort", T)) },
            D(("titel", "Häufige Fragen"), ("fragen", Items([("frage", "Wie lange dauert die Lieferung?"), ("antwort", "In der Regel zwei bis drei Werktage.")], [("frage", "Kann ich zurückgeben?"), ("antwort", "Ja, innerhalb von 30 Tagen ohne Angabe von Gründen.")])))),
        ["Galerie"] = new(new() { ["titel"] = T, ["bilder"] = Liste(40, ("src", new(Art.Bild)), ("alt", T)), ["spalten"] = new(Art.Stufe, Min: 2, Max: 4) }, D(("titel", ""), ("bilder", new JsonArray()), ("spalten", 3))),
        ["Formular"] = new(new() { ["titel"] = T, ["empfaenger"] = new(Art.Email), ["knopf"] = T, ["hinweis"] = T },
            D(("titel", "Schreib uns"), ("empfaenger", "kontakt@example.org"), ("knopf", "Nachricht senden"), ("hinweis", "Senden öffnet dein E-Mail-Programm. Es wird nichts im Internet gespeichert."))),
        ["Fusszeile"] = new(new() { ["text"] = T, ["links"] = Liste(12, ("label", T), ("href", L)), ["impressum"] = L, ["datenschutz"] = L },
            D(("text", "© Mein Laden"), ("links", new JsonArray()), ("impressum", "impressum.html"), ("datenschutz", "datenschutz.html"))),
        // W5: Verwendung einer Komponente; Inhalt kommt aus dem Original, Änderungen nur über die Komponenten-Aktionen
        ["Komponente"] = new(new(), D()),
        ["Stapel"] = new(new() { ["inhalt"] = new(Art.Slot), ["richtung"] = new(Art.Wahl, ["senkrecht", "waagerecht"]), ["abstand"] = new(Art.Stufe, Min: 0, Max: 4), ["breite"] = new(Art.Wahl, ["passt", "fuellt"]), ["ausrichtung"] = new(Art.Wahl, ["start", "mitte", "ende"]) },
            D(("richtung", "senkrecht"), ("abstand", 2), ("breite", "passt"), ("ausrichtung", "start"))),
    };

    public static IReadOnlyCollection<string> ComponentNames => Bausteine.Keys;
    /// <summary>Feldnamen eines Bausteins (für den Abgleich mit dem Editor).</summary>
    public static IReadOnlyCollection<string> ComponentFields(string name) => Bausteine[name].Felder.Keys;

    /// <summary>Bausteine mit ihren Feldern als Text für die KI.</summary>
    public static string ComponentsText() => string.Join("\n", Bausteine.Select(b =>
        $"- {b.Key}: " + string.Join(", ", b.Value.Felder.Select(f => f.Key + f.Value.Art switch
        {
            Art.Wahl => " (" + string.Join("|", f.Value.Werte!) + ")",
            Art.Zahl => $" ({f.Value.Min.ToString(CultureInfo.InvariantCulture)} bis {f.Value.Max.ToString(CultureInfo.InvariantCulture)})",
            Art.Slot => " (Slot)",
            Art.Stufe => $" (Stufe {f.Value.Min.ToString(CultureInfo.InvariantCulture)} bis {f.Value.Max.ToString(CultureInfo.InvariantCulture)} des Abstand-Grundwerts, keine Pixel)",
            Art.Farbe => " (#RRGGBB)",
            Art.Link => " (Ziel)",
            Art.Bild => " (Datei im Projekt)",
            Art.Email => " (E-Mail-Adresse)",
            Art.Liste => " (Liste aus " + string.Join(", ", f.Value.Sub!.Keys) + ")",
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
        catch (ArgumentException) { return (null, "Der Aktionsplan ist fehlerhaft (zum Beispiel doppelte Angaben)."); }
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
        try { return ApplyPlan(pageJson, plan, scope); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or JsonException or FormatException) { return WebPlanResult.Fail("Der Aktionsplan ist fehlerhaft (zum Beispiel doppelte Angaben)."); }
    }

    static WebPlanResult ApplyPlan(string pageJson, JsonObject plan, WebPlanScope? scope)
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
                if (WebDesign.Number(value) is { } d && d >= f.Min && d <= f.Max) return JsonValue.Create(d);
                throw new PlanError($"„{feld}“ muss eine Zahl von {f.Min.ToString(CultureInfo.InvariantCulture)} bis {f.Max.ToString(CultureInfo.InvariantCulture)} sein.");
            case Art.Stufe:
                if (WebDesign.Number(value) is { } sd && sd == Math.Floor(sd) && sd >= f.Min && sd <= f.Max) return JsonValue.Create((int)sd);
                throw new PlanError($"„{feld}“ muss eine Stufe von {f.Min.ToString(CultureInfo.InvariantCulture)} bis {f.Max.ToString(CultureInfo.InvariantCulture)} sein, keine Pixel.");
            case Art.Email:
                return S(value) is { } mail && System.Text.RegularExpressions.Regex.IsMatch(mail, @"^[^@\s<>""'?&#]+@[^@\s<>""'?&#]+\.[^@\s<>""'?&#]+$") ? JsonValue.Create(mail) : throw new PlanError($"„{feld}“ muss eine E-Mail-Adresse sein.");
            case Art.Liste:
            {
                if (value is not JsonArray items) throw new PlanError($"„{feld}“ muss eine Liste sein.");
                if (items.Count > f.Max) throw new PlanError($"„{feld}“ hat höchstens {f.Max.ToString(CultureInfo.InvariantCulture)} Einträge.");
                var outList = new JsonArray();
                foreach (var it in items)
                {
                    if (it is not JsonObject io) throw new PlanError($"Jeder Eintrag in „{feld}“ muss ein Objekt sein.");
                    var o = new JsonObject();
                    foreach (var kv in io)
                    {
                        if (!f.Sub!.TryGetValue(kv.Key, out var sf)) throw new PlanError($"„{feld}“ kennt kein Feld „{kv.Key}“.");
                        o[kv.Key] = Check(feld + "." + kv.Key, sf, kv.Value);
                    }
                    outList.Add(o);
                }
                return outList;
            }
            case Art.Objekt:
            {
                if (value is not JsonObject obj) throw new PlanError($"„{feld}“ muss ein Objekt sein.");
                var o = new JsonObject();
                foreach (var kv in obj)
                {
                    if (!f.Sub!.TryGetValue(kv.Key, out var sf)) throw new PlanError($"„{feld}“ kennt kein Feld „{kv.Key}“.");
                    o[kv.Key] = Check(feld + "." + kv.Key, sf, kv.Value);
                }
                if (feld == "zeitleiste" && WebDesignMotion.CheckTimeline(o) is { } zerr) throw new PlanError(zerr);
                return o;
            }
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
        if (s.Any(char.IsControl) || !WebDesign.IsAllowedUrl(s)) throw new PlanError($"„{feld}“: diese Linkart ist nicht erlaubt.");
        return s;
    }

    /// <summary>Bilder müssen als Bilddatei im Projekt liegen: keine Adressen, kein Weg nach oben, keine anderen Dateiarten, sonst läuft die Seite nicht offline oder fremde Dateien gelangen in den Export. Leer heißt: noch kein Bild.</summary>
    static string Image(string feld, string v)
    {
        var s = v.Trim().Replace('\\', '/');
        if (s.Length > 0 && !WebDesign.IsSafeImagePath(s)) throw new PlanError($"„{feld}“: das Bild muss als Bilddatei (PNG, JPG, WebP, GIF, AVIF) im Projekt liegen.");
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
            if (!def.Felder.TryGetValue(kv.Key, out var f) && !Allgemein.TryGetValue(kv.Key, out f)) throw new PlanError($"Baustein {type} hat kein Feld „{kv.Key}“.");
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
                if (type == "Komponente") throw new PlanError("Komponenten bitte mit „komponente_einfuegen“ einfügen.");
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
                var v = S(a["ansicht"])?.ToLowerInvariant();
                var names = new[] { WebDesignViews.Base }.Concat(WebDesignViews.Of(rootProps).Select(x => x.Name)).Select(x => x.ToLowerInvariant()).ToList();
                if (v is null || !names.Contains(v)) throw new PlanError($"„ansicht“ muss eine dieser Ansichten sein: {string.Join(", ", names)}.");
                rootProps["ansicht"] = v; changes.Add($"Ansicht: {v}");
                return doc;
            }
            case "ansicht_setzen":
            {
                var viewName = NeedS(a["name"], "name").Trim();
                if (WebDesign.Number(a["breite"]) is not { } breite || breite != Math.Floor(breite)) throw new PlanError("„breite“ muss eine ganze Zahl sein.");
                var views = WebDesignViews.Of(rootProps).ToList();
                if (WebDesignViews.CheckView(viewName, (int)breite, views) is { } err) throw new PlanError(err);
                var existing = views.FirstOrDefault(x => x.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));
                if (existing is not null) views.Remove(existing);
                views.Add(new(existing?.Name ?? viewName, (int)breite));
                rootProps["ansichten"] = WebDesignViews.ToJson(views);
                changes.Add(existing is null ? $"Ansicht „{viewName}“ bis {breite} px angelegt" : $"Ansicht „{existing.Name}“ gilt jetzt bis {breite} px");
                return doc;
            }
            case "ansicht_entfernen":
            {
                var viewName = NeedS(a["name"], "name");
                var views = WebDesignViews.Of(rootProps).ToList();
                var existing = views.FirstOrDefault(x => x.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase)) ?? throw new PlanError($"Ansicht „{viewName}“ gibt es nicht.");
                views.Remove(existing);
                rootProps["ansichten"] = WebDesignViews.ToJson(views);
                foreach (var blk in Lists(doc).SelectMany(l => l.OfType<JsonObject>())) if (blk["props"]?["anpassungen"] is JsonObject ov) { ov.Remove(existing.Name); if (ov.Count == 0) blk["props"]!.AsObject().Remove("anpassungen"); }
                if (S(rootProps["ansicht"]) == existing.Name.ToLowerInvariant()) rootProps.Remove("ansicht");
                changes.Add($"Ansicht „{existing.Name}“ entfernt");
                return doc;
            }
            case "anpassung_setzen":
            case "anpassung_zuruecksetzen":
            {
                var id = NeedS(a["id"], "id"); var block = Find(doc, id); var type = TypeOf(block);
                var view = NeedS(a["ansicht"], "ansicht");
                var known = WebDesignViews.Of(rootProps).FirstOrDefault(x => x.Name.Equals(view, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PlanError(view.Equals(WebDesignViews.Base, StringComparison.OrdinalIgnoreCase) ? $"{WebDesignViews.Base} ist die Grundlage; dort ändert man den Baustein selbst." : $"Ansicht „{view}“ gibt es nicht.");
                var props = block["props"]!.AsObject();
                if (props["anpassungen"] is not JsonObject all) props["anpassungen"] = all = [];
                if (all[known.Name] is not JsonObject vals) all[known.Name] = vals = [];
                if (name == "anpassung_setzen")
                {
                    if (a["werte"] is not JsonObject werte || werte.Count == 0) throw new PlanError("„werte“ muss ein nicht leeres Objekt sein.");
                    foreach (var kv in werte)
                    {
                        var (val, err) = WebDesignViews.CheckOverride(type, kv.Key, kv.Value);
                        if (err is not null) throw new PlanError(err);
                        vals[kv.Key] = val;
                    }
                    changes.Add($"{type} {id}: in {known.Name} angepasst ({string.Join(", ", werte.Select(x => x.Key))})");
                }
                else
                {
                    var fields = a["felder"] is JsonArray fa ? fa.Select(x => S(x) ?? throw new PlanError("„felder“ muss eine Liste von Namen sein.")).ToList() : vals.Select(x => x.Key).ToList();
                    foreach (var f in fields) vals.Remove(f);
                    changes.Add($"{type} {id}: Anpassung in {known.Name} zurückgesetzt");
                }
                if (vals.Count == 0) all.Remove(known.Name);
                if (all.Count == 0) props.Remove("anpassungen");
                return doc;
            }
            case "gestaltungswert_setzen":
            {
                var n = NeedS(a["name"], "name");
                JsonNode value = n switch
                {
                    "accent" or "ink" or "paper" or "accentDunkel" or "inkDunkel" or "paperDunkel" => Check("wert", new(Art.Farbe), a["wert"]),
                    "dunkel" => a["wert"] is JsonValue dv && dv.TryGetValue(out bool dark) ? JsonValue.Create(dark) : throw new PlanError("„wert“ muss wahr oder falsch sein."),
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
            case "komponente_erstellen":
            case "komponente_definieren":
            {
                var cname = NeedS(a["name"], "name").Trim();
                if (!WebDesignComponents.ValidName(cname)) throw new PlanError("Der Name der Komponente darf 1 bis 60 Buchstaben, Ziffern, Leerzeichen, Punkt, Strich oder Unterstrich haben.");
                var defs = WebDesignComponents.Read(rootProps["komponenten"]);
                if (defs.Any(c => c.Name.Equals(cname, StringComparison.OrdinalIgnoreCase))) throw new PlanError($"Eine Komponente „{cname}“ gibt es schon.");
                if (defs.Count >= WebDesignComponents.MaxComponents) throw new PlanError("Zu viele Komponenten.");
                var kid = name == "komponente_definieren" && a["komponente"] is { } kn ? NeedS(kn, "komponente") : null;
                if (kid is null) for (var n = 1; ; n++) if (!defs.Any(c => c.Id == $"K-{n}")) { kid = $"K-{n}"; break; }
                if (!IdPattern().IsMatch(kid) || defs.Any(c => c.Id == kid)) throw new PlanError($"Komponenten-id „{kid}“ ist ungültig oder vergeben.");
                var blocks = new JsonArray();
                if (name == "komponente_erstellen")
                {
                    if (a["ids"] is not JsonArray ids || ids.Count == 0) throw new PlanError("„ids“ muss eine Liste von Bausteinen sein.");
                    var found = ids.Select(x => Find(doc, NeedS(x, "ids"))).Distinct().ToList();
                    var list = Lists(doc).First(l => l.Contains(found[0]));
                    if (found.Any(b => !list.Contains(b))) throw new PlanError("Alle Bausteine einer Komponente müssen nebeneinander in derselben Liste liegen.");
                    if (found.Any(b => TypeOf(b) == "Komponente")) throw new PlanError("Eine Komponente kann keine Verwendung einer Komponente enthalten.");
                    var ordered = found.OrderBy(b => list.IndexOf(b)).ToList();
                    var at = list.IndexOf(ordered[0]);
                    foreach (var blk in ordered) { list.Remove(blk); blocks.Add(blk); }
                    var uid = a["id"] is { } ui ? NeedS(ui, "id") : NewId(doc, "Komponente");
                    if (!IdPattern().IsMatch(uid) || Lists(doc).SelectMany(l => l.OfType<JsonObject>()).Any(x => IdOf(x) == uid)) throw new PlanError($"id „{uid}“ ist ungültig oder vergeben.");
                    list.Insert(at, new JsonObject { ["type"] = "Komponente", ["props"] = new JsonObject { ["id"] = uid, ["komponente"] = kid } });
                }
                else
                {
                    if (a["bausteine"] is not JsonArray items || items.Count == 0 || items.Count > WebDesignComponents.MaxBlocks) throw new PlanError($"„bausteine“ muss 1 bis {WebDesignComponents.MaxBlocks} Bausteine enthalten.");
                    var used = new HashSet<string>();
                    foreach (var item in items)
                    {
                        if (item is not JsonObject io) throw new PlanError("Jeder Baustein braucht typ und props.");
                        var type = NeedS(io["typ"], "typ"); var bdef = Def(type);
                        if (type == "Komponente") throw new PlanError("Eine Komponente kann keine Verwendung einer Komponente enthalten.");
                        foreach (var kv in io) if (kv.Key is not ("typ" or "id" or "props")) throw new PlanError($"unbekannte Angabe „{kv.Key}“ in bausteine.");
                        var bid = io["id"] is { } bi ? NeedS(bi, "id") : $"{type}-{used.Count + 1}";
                        if (!IdPattern().IsMatch(bid) || !used.Add(bid)) throw new PlanError($"id „{bid}“ ist ungültig oder doppelt.");
                        var props = new JsonObject();
                        foreach (var kv in bdef.Standard) props[kv.Key] = kv.Value?.DeepClone();
                        foreach (var kv in bdef.Felder.Where(f => f.Value.Art == Art.Slot)) props[kv.Key] = new JsonArray();
                        foreach (var kv in Props(type, io["props"], true)) props[kv.Key] = kv.Value?.DeepClone();
                        props["id"] = bid;
                        blocks.Add(new JsonObject { ["type"] = type, ["props"] = props });
                    }
                }
                defs.Add(new(kid, cname, blocks));
                rootProps["komponenten"] = WebDesignComponents.ToJson(defs);
                changes.Add($"Komponente „{cname}“ ({kid}) angelegt");
                return doc;
            }
            case "komponente_einfuegen":
            {
                var kid = NeedS(a["komponente"], "komponente");
                if (!WebDesignComponents.Read(rootProps["komponenten"]).Any(c => c.Id == kid)) throw new PlanError($"Komponente „{kid}“ gibt es nicht.");
                var uid = a["id"] is { } ui ? NeedS(ui, "id") : NewId(doc, "Komponente");
                if (!IdPattern().IsMatch(uid) || Lists(doc).SelectMany(l => l.OfType<JsonObject>()).Any(x => IdOf(x) == uid)) throw new PlanError($"id „{uid}“ ist ungültig oder vergeben.");
                var list = Target(doc, a["in"], a["slot"]);
                list.Insert(Position(a["position"], list.Count), new JsonObject { ["type"] = "Komponente", ["props"] = new JsonObject { ["id"] = uid, ["komponente"] = kid } });
                changes.Add($"Komponente {kid} eingefügt ({uid})");
                return doc;
            }
            case "komponente_aendern":
            {
                var kid = NeedS(a["komponente"], "komponente"); var element = NeedS(a["element"], "element");
                var defs = WebDesignComponents.Read(rootProps["komponenten"]);
                var def = defs.FirstOrDefault(c => c.Id == kid) ?? throw new PlanError($"Komponente „{kid}“ gibt es nicht.");
                var block = def.Blocks.OfType<JsonObject>().FirstOrDefault(b => IdOf(b) == element) ?? throw new PlanError($"„{element}“ gibt es in der Komponente nicht.");
                var props = block["props"]!.AsObject();
                foreach (var kv in Props(TypeOf(block), a["props"], false)) { props[kv.Key] = kv.Value?.DeepClone(); changes.Add($"Komponente {kid}, {element}: {kv.Key} im Original geändert"); }
                rootProps["komponenten"] = WebDesignComponents.ToJson(defs);
                return doc;
            }
            case "komponente_ueberschreiben":
            case "komponente_zuruecksetzen":
            {
                var id = NeedS(a["id"], "id"); var usage = Find(doc, id);
                if (TypeOf(usage) != "Komponente") throw new PlanError($"„{id}“ ist keine Verwendung einer Komponente.");
                var up = usage["props"]!.AsObject();
                var def = WebDesignComponents.Read(rootProps["komponenten"]).FirstOrDefault(c => c.Id == S(up["komponente"])) ?? throw new PlanError("Das Original dieser Komponente fehlt.");
                if (up["ueberschreibungen"] is not JsonObject over) up["ueberschreibungen"] = over = [];
                if (name == "komponente_ueberschreiben")
                {
                    var element = NeedS(a["element"], "element");
                    var block = def.Blocks.OfType<JsonObject>().FirstOrDefault(b => IdOf(b) == element) ?? throw new PlanError($"„{element}“ gibt es in der Komponente nicht.");
                    if (over[element] is not JsonObject eo) over[element] = eo = [];
                    foreach (var kv in Props(TypeOf(block), a["props"], false))
                    {
                        if (Def(TypeOf(block)).Felder[kv.Key].Art == Art.Slot) throw new PlanError("Bereiche lassen sich nicht überschreiben.");
                        eo[kv.Key] = kv.Value?.DeepClone(); changes.Add($"Komponente {id}, {element}: {kv.Key} nur hier überschrieben");
                    }
                }
                else
                {
                    var targets = a["element"] is { } en ? [NeedS(en, "element")] : over.Select(kv => kv.Key).ToList();
                    foreach (var el in targets)
                    {
                        if (over[el] is not JsonObject eo) continue;
                        var fields = a["felder"] is JsonArray fa ? fa.Select(x => S(x) ?? throw new PlanError("„felder“ muss eine Liste von Namen sein.")).ToList() : eo.Select(kv => kv.Key).ToList();
                        foreach (var f in fields) eo.Remove(f);
                        if (eo.Count == 0) over.Remove(el);
                    }
                    changes.Add($"Komponente {id}: Überschreibungen zurückgesetzt");
                }
                if (over.Count == 0) up.Remove("ueberschreibungen");
                return doc;
            }
            case "bewegung_setzen":
            case "zeitleiste_setzen":
            case "schluesselbild_setzen":
            case "schluesselbild_entfernen":
            {
                var id = NeedS(a["id"], "id"); var block = Find(doc, id); var props = block["props"]!.AsObject();
                var key = name == "bewegung_setzen" ? "bewegung" : "zeitleiste";
                var current = props[key] as JsonObject ?? [];
                var next = (JsonObject)current.DeepClone();
                if (name is "bewegung_setzen" or "zeitleiste_setzen")
                {
                    var given = 0;
                    foreach (var kv in a) if (kv.Key is not ("aktion" or "id")) { next[kv.Key] = kv.Value?.DeepClone(); given++; }
                    if (given == 0) throw new PlanError("Keine Einstellung angegeben.");
                }
                else
                {
                    var prop = NeedS(a["eigenschaft"], "eigenschaft");
                    var zeit = WebDesign.Number(a["zeit"]) ?? throw new PlanError("„zeit“ muss eine Zahl sein.");
                    if (next["punkte"] is not JsonArray pts) next["punkte"] = pts = [];
                    var same = pts.OfType<JsonObject>().Where(p => p["eigenschaft"]?.ToString() == prop && WebDesign.Number(p["zeit"]) == zeit).ToList();
                    if (name == "schluesselbild_entfernen" && same.Count == 0) throw new PlanError($"Bei {zeit.ToString(CultureInfo.InvariantCulture)} s gibt es kein Schlüsselbild für „{prop}“.");
                    foreach (var p in same) pts.Remove(p);
                    if (name == "schluesselbild_setzen") pts.Add(new JsonObject { ["zeit"] = zeit, ["eigenschaft"] = prop, ["wert"] = a["wert"]?.DeepClone() });
                    next["dauer"] ??= 2;
                }
                props[key] = Check(key, Allgemein[key], next);
                changes.Add($"{TypeOf(block)} {id}: {(key == "bewegung" ? "Bewegung" : "Zeitleiste")} geändert");
                return doc;
            }
            case "seitenuebergang_setzen":
            {
                var art = S(a["art"]);
                if (art is not ("keiner" or "einblenden")) throw new PlanError("„art“ muss keiner oder einblenden sein.");
                rootProps["uebergang"] = art; changes.Add($"Seitenübergang: {art}");
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
