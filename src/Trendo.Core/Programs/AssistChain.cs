using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>Woher ein Auftrag an die KI kommt. Alle Einstiege laufen über dieselbe Kette (<see cref="AssistChain"/>).</summary>
public enum AssistEntry
{
    /// <summary>Auftragszeile über dem Programm („Los“ oder Enter).</summary>
    Auftragszeile,
    /// <summary>KI-Befehl im Menüband eines Programms („Weiterschreiben mit Claude“ …).</summary>
    Programmbefehl,
    /// <summary>Claude über die Brücke (<c>dokument_setzen</c>) während eines Claude-Laufs.</summary>
    ClaudeBruecke,
    /// <summary>Vorschlag „Als Nächstes“ aus einem fertigen Auftrag.</summary>
    Vorschlag,
}

/// <summary>Geprüfter Auftrag: Programm (Seite), Text, Einstieg.</summary>
public sealed record AssistOrder(string Program, string Prompt, AssistEntry Entry);

/// <summary>Wohin ein Auftrag wirken kann. <see cref="InDocument"/>: das offene Dokument der Programm-Hülle (ein Rückgängig-Schritt, echte Datei).
/// <see cref="NotInDocument"/>: Programm läuft noch außerhalb der Hülle – es gibt eine klare Rückmeldung statt einer stillen Ausgabe.</summary>
public enum AssistReach { InDocument, NotInDocument, NoProgram }

/// <summary>T-0707 (Paket B5 „Ein Assistenzweg“): die eine Kette für KI-Aufträge in Programmen –
/// Auftrag prüfen → Fähigkeit (Dokument der Hülle?) → Rechte (Rbac, Not-Stopp/offene Freigaben über die vorhandene Freigabe-API) → Plan (Regeln, sonst KI:
/// Claude über das eigene Abo, sonst die KI auf diesem PC – beides über <c>IntelligenceCore</c>) → Vorschau mit Ja/Nein → Änderung im offenen Dokument als EIN
/// Rückgängig-Schritt → echte Datei. Kein eigener KI-Kern: dieselben Bausteine wie bisher (<see cref="AssistPlanner"/>, <see cref="ProgramDocument"/>, <see cref="DocPath"/>).</summary>
public static partial class AssistChain
{
    public const int MaxPrompt = 4000;
    /// <summary>Ab so vielen Wörtern ist ein Auftrag eine Fachaufgabe: zuerst die KI, der Regel-Planer nur als Rückfall.</summary>
    public const int KiFirstWords = 8;
    /// <summary>Höchstlänge der Dokumentdaten, die die KI zum Planen sieht.</summary>
    public const int MaxDocJson = 8000;
    /// <summary>Größter Wert, den die KI an eine Stelle schreiben darf.</summary>
    public const int MaxValue = 100_000;
    /// <summary>Beschriftung jedes KI-Schritts in der Rückgängig-Liste („KI: …“).</summary>
    public const string UndoPrefix = "KI: ";
    /// <summary>Feste Rückmeldung für Programme außerhalb der Programm-Hülle.</summary>
    public const string NotInDocumentLead = "Noch nicht im Dokument möglich";

    /// <summary>Die Schritte der Kette in Alltagswörtern (für Protokoll und Profi-Ansicht).</summary>
    public static readonly IReadOnlyList<string> Steps = ["Auftrag geprüft", "Programm erkannt", "Rechte geprüft", "Plan", "Vorschau – Ja oder Nein", "Im Dokument geändert", "Datei gespeichert"];

    /// <summary>Allgemeine Aktion der KI: eine Stelle des Dokuments mit neuem Inhalt füllen. Steht nur im KI-Plan, nicht im Regel-Planer
    /// (der Regel-Planer kennt nur die Befehle des Menübands).</summary>
    public const string EditAction = "dokument.setzen";
    public static AssistAction EditActionDef { get; } = new(EditAction, "Inhalt im Dokument ändern", ["dokument", "inhalt"],
        [new AssistParam("pfad", "Stelle"), new AssistParam("wert", "Neuer Inhalt")]);

    [GeneratedRegex("consent|einwillig|freigabe|approved", RegexOptions.IgnoreCase)] private static partial Regex ConsentPath();
    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex Thinking();

    /// <summary>Schritt 1: Auftrag prüfen. Wirft <see cref="ArgumentException"/> mit einem Satz für die Person.</summary>
    public static AssistOrder Validate(string? program, string? prompt, AssistEntry entry)
    {
        var p = (prompt ?? "").Trim();
        if (string.IsNullOrWhiteSpace(program)) throw new ArgumentException("Es ist kein Programm offen.");
        if (p.Length == 0) throw new ArgumentException("Schreib zuerst, was im Dokument entstehen oder sich ändern soll.");
        if (p.Length > MaxPrompt) throw new ArgumentException($"Der Auftrag ist zu lang (höchstens {MaxPrompt} Zeichen).");
        return new AssistOrder(program.Trim(), p, entry);
    }

    /// <summary>Schritt 2: Fähigkeit – liegt ein offenes Dokument der Programm-Hülle vor?</summary>
    public static AssistReach Reach(bool isShellProgram, bool hasOpenDocument, bool isProgramPage) =>
        isShellProgram && hasOpenDocument ? AssistReach.InDocument : isProgramPage || isShellProgram ? AssistReach.NotInDocument : AssistReach.NoProgram;

    /// <summary>Einheitliche Rückmeldung für Programme außerhalb der Hülle.</summary>
    public static string NotInDocumentText(string title, bool fallsBackToProject) =>
        $"{NotInDocumentLead}: „{title}“ arbeitet noch nicht mit der Programm-Hülle, darum kann die KI hier nichts direkt im offenen Dokument ändern."
        + (fallsBackToProject ? " Das Ergebnis kommt stattdessen als Datei ins Projekt." : "");

    /// <summary>Schritt 3: Rechte. null = erlaubt, sonst der Grund. Nutzt nur vorhandene Bausteine: <see cref="Rbac"/> und den Not-Stopp des <see cref="ApprovalGate"/>.</summary>
    public static string? RightsBlock(Principal who, ApprovalGate gate)
    {
        if (!Rbac.Has(who, Permission.RequestAction)) return "Dieses Konto darf keine Aufträge an die KI geben.";
        if (gate.Halted) return "Der Not-Stopp ist an – die KI ändert gerade nichts. Erst auf der Seite „Freigaben“ wieder einschalten.";
        return null;
    }

    /// <summary>Anweisung an die KI: Rolle, Fachregeln, die erlaubten Befehle, dazu die Daten des offenen Dokuments und die Aktion <see cref="EditAction"/>.</summary>
    public static string KiPrompt(string title, IReadOnlyList<AssistAction> actions, string? role, string? skills, JsonObject document)
    {
        var all = actions.Any(a => a.Id == EditAction) ? actions : [.. actions, EditActionDef];
        var sb = new StringBuilder(AssistPlanner.KiPrompt(title, all, role, skills));
        var json = document.ToJsonString();
        sb.AppendLine($"Das offene Dokument „{title}“ hat diese Daten (JSON{(json.Length > MaxDocJson ? ", gekürzt" : "")}):");
        sb.AppendLine(json.Length > MaxDocJson ? json[..MaxDocJson] + " …" : json);
        sb.AppendLine($"Mit „{EditAction}“ schreibst du Inhalt direkt ins Dokument: \"pfad\" ist die Stelle wie „titel“ oder „abschnitte/0/text“ (nur Stellen, die es gibt, neue Felder nur in vorhandenen Objekten, an Listen hinten anhängen mit dem Index gleich der Länge),");
        sb.AppendLine("\"wert\" ist der fertige neue Inhalt – Text als Text, Listen und Objekte als JSON in derselben Form wie im Dokument. Schreib den Inhalt selbst aus, keine Platzhalter, und ändere nur, was der Auftrag verlangt.");
        sb.AppendLine("Antworte auf Deutsch nur mit dem JSON, ohne Erklärung davor oder danach.");
        return sb.ToString();
    }

    /// <summary>KI-Antwort für den Planer aufbereiten: Denkteil entfernen.</summary>
    public static string CleanAnswer(string answer) => Thinking().Replace(answer ?? "", "").Trim();

    /// <summary>Sieht die Antwort nach einem Plan aus, der sich nicht lesen ließ (kaputtes JSON)? Dann fragt die Kette die KI genau einmal nach.</summary>
    public static bool LooksLikePlan(string answer) => CleanAnswer(answer).Contains("\"steps\"", StringComparison.Ordinal);

    /// <summary>Nachfrage an die KI, wenn ihr Plan kein gültiges JSON war.</summary>
    public const string RepairPrompt = "Deine Antwort war kein gültiges JSON (eine Klammer fehlt oder ist zu viel). Antworte noch einmal mit genau demselben Plan als gültigem JSON, ohne Erklärung.";

    /// <summary>Plan aus der KI-Antwort: dieselbe strenge Prüfung wie <see cref="AssistPlanner.FromKi"/>, dazu <see cref="EditAction"/> mit beliebigem JSON-Wert.</summary>
    public static AssistPlan FromKi(string prompt, string answer, IReadOnlyList<AssistAction> actions)
    {
        var all = actions.Any(a => a.Id == EditAction) ? actions : [.. actions, EditActionDef];
        var clean = CleanAnswer(answer);
        var plan = AssistPlanner.FromKi(prompt, clean, all);
        // Echter Fall (qwen3:14b, Tagesplaner): eine schließende Klammer fehlt vor „]“. Fehlende Klammern ergänzen – nur wenn danach gültiges JSON entsteht;
        // der Plan läuft trotzdem durch dieselbe strenge Prüfung, Vorschau und Ja/Nein.
        // Zweiter echter Fall: der Plan ist vollständig, danach folgt noch ein zweites Objekt („…]},{"frage":""}“) – dann gilt das erste vollständige Objekt.
        if (!plan.Understood && LooksLikePlan(clean))
            foreach (var candidate in new[] { FirstObject(clean), CloseBrackets(clean) })
                if (candidate is { } c && c != clean && AssistPlanner.FromKi(prompt, c, all) is { Understood: true } repaired) return repaired;
        return plan;
    }

    /// <summary>Das erste vollständige JSON-Objekt im Text (Klammern in Texten zählen nicht); null, wenn keins vollständig ist.</summary>
    public static string? FirstObject(string text)
    {
        var start = text.IndexOf('{'); if (start < 0) return null;
        int depth = 0; bool inText = false, escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inText) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') inText = false; continue; }
            if (c == '"') inText = true;
            else if (c is '{' or '[') depth++;
            else if (c is '}' or ']' && --depth == 0) return c == '}' ? text[start..(i + 1)] : null;
        }
        return null;
    }

    /// <summary>Fehlende schließende Klammern ergänzen (auch mitten im Text, wenn eine andere Klammer zu früh schließt). Zeichen in Texten bleiben unberührt.
    /// null, wenn der Text überzählige Klammern hat – dann wird nichts erraten.</summary>
    public static string? CloseBrackets(string json)
    {
        var sb = new StringBuilder(json.Length + 8); var open = new Stack<char>(); bool inText = false, escaped = false;
        foreach (var c in json)
        {
            if (inText)
            {
                sb.Append(c);
                if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') inText = false;
                continue;
            }
            switch (c)
            {
                case '"': inText = true; break;
                case '{': open.Push('}'); break;
                case '[': open.Push(']'); break;
                case '}' or ']':
                    while (open.Count > 0 && open.Peek() != c) sb.Append(open.Pop());
                    if (open.Count == 0) return null;
                    open.Pop(); break;
            }
            sb.Append(c);
        }
        while (open.Count > 0) sb.Append(open.Pop());
        return sb.ToString();
    }

    /// <summary>Wert aus dem Plan in einen JSON-Wert für die Stelle verwandeln: steht dort Text, bleibt es Text; sonst gilt gültiges JSON.</summary>
    public static JsonNode? ValueFor(JsonObject document, string path, string raw)
    {
        var existing = DocPath.Get(document, path);
        if (existing is JsonValue ev && ev.GetValueKind() == JsonValueKind.String)
            return JsonValue.Create(DocPath.Unwrap(JsonValue.Create(raw)) is JsonValue uv && uv.GetValueKind() == JsonValueKind.String ? uv.GetValue<string>() : raw);
        try { return JsonNode.Parse(raw); }
        catch (JsonException) { return JsonValue.Create(raw); }
    }

    /// <summary>Prüfung vor jeder Änderung durch eine KI (Assistent und Claude-Brücke): keine Freigabe-/Einwilligungsfelder, die Stelle muss zum Dokument passen,
    /// eine vorhandene Stelle behält ihre Art (Text bleibt Text, Liste bleibt Liste). Wirft <see cref="ArgumentException"/> mit einem Satz für die Person.</summary>
    public static void CheckEdit(JsonObject document, string path, JsonNode? value)
    {
        var parts = PathParts(path);
        if (parts.Length == 0) throw new ArgumentException("Die KI hat keine Stelle im Dokument genannt.");
        if (ConsentPath().IsMatch(path!)) throw new ArgumentException("Einwilligungen und Freigaben trägt nur der Nutzer selbst ein.");
        if (value is not null && value.ToJsonString().Length > MaxValue) throw new ArgumentException("Der neue Inhalt ist zu groß für eine Stelle im Dokument.");
        var parent = DocPath.Get(document, string.Join('/', parts[..^1]));
        // neue Felder nur mit einfachen Namen (kein „tasks[0]“ als Feldname)
        if (DocPath.Get(document, path!) is null && parts.Any(p => !FieldName().IsMatch(p))) throw new ArgumentException($"„{path}“ ist keine gültige Stelle im Dokument.");
        if (parent is null) // wie DocPath.Set: fehlende Gruppen entstehen unterwegs – aber nur unter einer vorhandenen Gruppe und nie als Liste
        {
            var depth = parts.Length - 1;
            while (depth > 0 && DocPath.Get(document, string.Join('/', parts[..depth])) is null) depth--;
            if (DocPath.Get(document, string.Join('/', parts[..depth])) is JsonObject && parts[depth..].All(p => !int.TryParse(p, out _))) return;
        }
        if (parent is not (JsonObject or JsonArray)) throw new ArgumentException($"Die Stelle „{path}“ gibt es in diesem Dokument nicht.");
        if (parent is JsonArray arr && (!int.TryParse(parts[^1], out var i) || i < 0 || i > arr.Count)) throw new ArgumentException($"Die Stelle „{path}“ gibt es in dieser Liste nicht.");
        var existing = DocPath.Get(document, path);
        if (existing is null || value is null) return;
        if (Kind(existing) != Kind(value)) throw new ArgumentException($"Der neue Inhalt für „{path}“ passt nicht zum Dokument (dort steht {KindWord(existing)}, die KI wollte {KindWord(value)} eintragen).");
    }

    static string Kind(JsonNode n) => n switch
    {
        JsonObject => "objekt",
        JsonArray => "liste",
        JsonValue v => v.GetValueKind() switch { JsonValueKind.String => "text", JsonValueKind.Number => "zahl", JsonValueKind.True or JsonValueKind.False => "jn", _ => "leer" },
        _ => "leer",
    };
    static string KindWord(JsonNode n) => Kind(n) switch { "objekt" => "eine Gruppe", "liste" => "eine Liste", "text" => "Text", "zahl" => "eine Zahl", "jn" => "Ja/Nein", _ => "nichts" };

    [GeneratedRegex(@"\[(\d+)\]")] private static partial Regex IndexBrackets();
    [GeneratedRegex(@"^[\p{L}\p{N}_-]+$")] private static partial Regex FieldName();

    /// <summary>T-0774 (2. Gegenprüfung H1): die Teile einer Stelle genau so, wie <see cref="CheckEdit"/> und <see cref="DocPath"/> sie lesen
    /// (Leerzeichen und Tabs am Rand weg, leere Teile weg) – für jede Prüfung, die eine Stelle mit einem Feldnamen vergleicht.</summary>
    public static string[] PathParts(string? path) => (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Pfad vereinheitlichen: „tasks[0]/title“ und „/tasks/0“ werden zu „tasks/0/title“ (so schreiben KIs Pfade oft).</summary>
    public static string NormalizePath(string path) => IndexBrackets().Replace(path ?? "", "/$1").Replace('.', '/').Trim('/');

    /// <summary>Eine geprüfte Änderung an den Daten ausführen (ohne Rückgängig – das übernimmt der Aufrufer mit <see cref="ProgramDocument.Edit"/>).</summary>
    public static void ApplyEdit(JsonObject document, string path, string raw)
    {
        path = NormalizePath(path);
        var value = ValueFor(document, path, raw);
        CheckEdit(document, path, value);
        DocPath.Set(document, path, value);
    }
}
