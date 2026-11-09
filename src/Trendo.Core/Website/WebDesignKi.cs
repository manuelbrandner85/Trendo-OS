using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>A7 und K4: welche KI eine Aufgabe übernimmt und was sie als Auftrag bekommt. Kein eigener KI-Kern: Gefragt wird über <see cref="IntelligenceCore"/>.</summary>
public static class WebDesignKi
{
    /// <summary>Aufgaben der KI mit ihrem Bedarf.</summary>
    public static readonly IReadOnlyDictionary<string, KiBedarf> Tasks = new Dictionary<string, KiBedarf>
    {
        ["seite_aus_text"] = KiBedarf.Text, ["seite_aus_bild"] = KiBedarf.Bild, ["auswahl_aendern"] = KiBedarf.Text, ["nachbessern"] = KiBedarf.Text, ["bild_bewerten"] = KiBedarf.Bild,
    };

    /// <summary>Lokale Modelle (Ollama), die Bilder sehen können, beste zuerst.</summary>
    public static readonly string[] VisionModels = ["qwen2.5vl:7b", "llama3.2-vision", "gemma3:4b"];

    public sealed record Choice(bool Ok, string Who, string Note);

    public const string ClaudeLabel = "Claude über dein Abo", LocalLabel = "KI auf diesem PC";

    /// <summary>Erlaubte KIs: Claude über das Abo im installierten Claude Code und KIs auf diesem PC. KI-Dienste mit API-Schlüssel sind ausgeschlossen.</summary>
    public static IReadOnlyList<IKiBackend> Allowed(IEnumerable<IKiBackend> all) => all.Where(b => b is IMainKiBackend || b is not IRemoteKiBackend).ToList();

    /// <summary>Wählt nach Bedarf: Claude über das Abo, sonst lokal, und sagt klar, was gerade nicht geht.</summary>
    public static Choice Choose(KiBedarf need, bool claude, bool local, bool localVision) => need switch
    {
        KiBedarf.Bild when claude => new(true, ClaudeLabel, "Claude sieht dein Bild über dein Abo."),
        KiBedarf.Bild when localVision => new(true, LocalLabel, "Eine KI auf diesem PC sieht dein Bild."),
        KiBedarf.Bild => new(false, "", "Für diese Aufgabe muss eine KI Bilder sehen können. Melde Claude an oder lade auf der KI-Seite eine KI, die Bilder versteht. Mit einem Satz statt Bild geht es sofort."),
        _ when claude => new(true, ClaudeLabel, ""),
        _ when local => new(true, LocalLabel, need == KiBedarf.Code ? "Für Code nimmt Trendo das passende Modell auf diesem PC." : ""),
        _ => new(false, "", "Gerade ist keine KI verfügbar. Melde Claude an oder lade auf der KI-Seite eine KI für diesen PC."),
    };

    static string ActionsText(WebPlanScope? scope) => string.Join("\n", WebDesignActions.Catalog.Where(a => scope is null || a.FuerAuswahl).Select(a =>
        $"- {a.Name} (braucht {a.Bedarf}): {a.Beschreibung} Pflicht: {(a.Pflicht.Count == 0 ? "keine" : string.Join(", ", a.Pflicht))}. Optional: {(a.Optional.Count == 0 ? "keine" : string.Join(", ", a.Optional))}."));

    /// <summary>Anweisungen an die KI: nur ein Plan aus benannten Aktionen, kein Code.</summary>
    public static string Instructions(string? skills, WebPlanScope? scope = null)
    {
        var sb = new StringBuilder();
        sb.Append("Du bedienst den visuellen Webseiten-Editor von Trendo. Du schreibst keinen Code und keine Dateien.\n");
        sb.Append("Antworte ausschließlich mit einem JSON-Objekt dieser Form: {\"aktionen\":[{\"aktion\":\"name\", ...Angaben}]}. Kein Text davor oder danach.\n");
        sb.Append("Alle Texte auf Deutsch. Schreibe echte, konkrete Inhalte, keine Platzhalter. Links: \"#\", vollständige https-Adressen oder mailto. Keine erfundenen Sprungziele und Bilddateien.\n");
        sb.Append("Wenn du neue Inhalte brauchst, gib dem Baustein eine eigene id, damit spätere Aktionen ihn nennen können.\n\nAktionen:\n").Append(ActionsText(scope));
        sb.Append("\n\nBausteine und ihre Felder (Angaben in props, nur diese Felder):\n").Append(WebDesignActions.ComponentsText());
        if (scope is not null) sb.Append("\n\nÄndere ausschließlich die genannten Bausteine. Andere ids werden abgelehnt.");
        if (skills is { Length: > 0 }) sb.Append("\n\nFachregeln:\n").Append(skills);
        return sb.ToString();
    }

    /// <summary>Auftrag für eine neue Seite (K4). <paramref name="imageNote"/>: Beschreibung oder Hinweis zu einem eigenen Bild.</summary>
    public static string NewPagePrompt(string wish, string? imageNote = null) =>
        "Baue eine vollständige, bearbeitbare Seite für diesen Wunsch. Beginne mit seite_anlegen, dann baustein_einfuegen. Genau ein Kopfbereich.\nWunsch: " + wish.Trim()
        + (imageNote is { Length: > 0 } ? "\nOrientiere dich an diesem Bild:\n" + imageNote : "");

    /// <summary>Auftrag für eine Änderung an ausgewählten Bausteinen (K1). Die KI sieht nur die Auswahl.</summary>
    public static string SelectionPrompt(string wish, string pageJson, IReadOnlyCollection<string> ids)
    {
        var blocks = new JsonArray();
        var doc = JsonNode.Parse(pageJson)!.AsObject();
        void Walk(JsonNode? n)
        {
            if (n is JsonArray a) foreach (var x in a) Walk(x);
            else if (n is JsonObject o)
            {
                if (o["type"] is not null && o["props"]?["id"]?.ToString() is { } id && ids.Contains(id)) blocks.Add(o.DeepClone());
                else foreach (var kv in o) Walk(kv.Value);
            }
        }
        Walk(doc["content"]); Walk(doc["zones"]);
        return "Ändere nur diese Bausteine:\n" + blocks.ToJsonString() + "\n\nÄnderungswunsch: " + wish.Trim();
    }

    /// <summary>Auftrag zum Nachbessern (A6): nur die gefundenen Fehler, gezielt.</summary>
    public static string RepairPrompt(string pageJson, IEnumerable<WebsiteFinding> findings) =>
        "Die Prüfung der fertigen Seite hat diese Fehler gefunden. Behebe gezielt nur sie, ändere sonst nichts.\n"
        + string.Join("\n", findings.Select(f => $"- {f.Area}: {f.Text}")) + "\n\nSeite:\n" + pageJson;
}
