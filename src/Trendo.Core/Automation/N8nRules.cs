namespace Trendo.Core.Automation;

/// <summary>Regeln für Claude im Studio mit den Abläufen (Claude-Ausbau zu n8n, N5): Abläufe als Datei zur Prüfung, nie selbst einschalten.</summary>
public static class N8nRules
{
    public static readonly string Text = "\n\nN8N-ABLÄUFE (Trendo-Regel):\n"
        + "- Wünscht der Nutzer einen Ablauf, baue ihn als n8n-Ablauf (JSON mit name, nodes, connections) und lege ihn als Datei „<kurzer-name>.n8n.json“ im Projektordner ab. Trendo zeigt ihn im Bereich Abläufe → Profi als Fluss; angelegt wird er erst nach dem Ja des Nutzers, eingeschaltet nur von ihm selbst.\n"
        + "- Für KI-Schritte nutze die lokale Adresse „Trendo KI“: HTTP-Request POST " + N8n.TrendoKiBase + "/chat/completions (OpenAI-Format, Modell z. B. „ollama/llama3.2“) oder den n8n-Zugang „Trendo KI (lokal)“. Trendo-Aktionen: " + "/api/actions/notify, /api/actions/calendar, /api/actions/note, /api/actions/letter-draft, /api/workflows/{id}/run unter " + N8n.TrendoApiBase + " mit Kopfzeile „Authorization: Bearer …“ (Platzhalter TRENDO_SCHLUESSEL, Trendo setzt ihn ein).\n"
        + "- Nichts, was nach außen wirkt (Mails senden, Beiträge posten, Zahlungen, fremde Dienste), ohne dass der Nutzer es ausdrücklich will; solche Bausteine im Ablauf klar benennen. Keine Befehls-Bausteine (Execute Command).\n";
}
