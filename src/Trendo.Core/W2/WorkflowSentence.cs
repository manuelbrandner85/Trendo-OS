using System.Text.Json;
using System.Text.Json.Nodes;
using IOPath = System.IO.Path;

namespace Trendo.Core;

/// <summary>T-0742 (SYS-05): Aus einem Satz wird ein echter Ablauf der <see cref="WorkflowEngine"/> – kein zweiter Workflow-Kern.
/// Die KI beschreibt Auslöser, Bedingung (Wochentage, Dateimuster) und Schritte als JSON; dieser Baustein prüft jede Angabe und baut daraus
/// einen <see cref="Workflow"/>. Der Ablauf ist danach AUS (<see cref="Workflow.Enabled"/> = false) und startet erst, wenn die Person ihn einschaltet.
/// Pfade müssen im erlaubten Benutzerordner liegen, Agent-Schritte legt die KI nicht an (die wählst du selbst).</summary>
public static class WorkflowSentence
{
    public const int MaxSteps = 20;

    /// <summary>Anweisung an die KI. Sie antwortet NUR mit JSON in genau diesem Aufbau.</summary>
    public const string System =
        "Du baust aus einem deutschen Satz einen Ablauf für die Ablauf-Engine von Trendo OS. Antworte ausschließlich mit einem JSON-Objekt, ohne Erklärung.\n" +
        "Aufbau: {\"name\": \"kurzer Name\", \"ausloeser\": {\"art\": \"manuell|taeglich|intervall|neue_datei\", \"uhrzeit\": \"HH:MM\", \"minuten\": 60, " +
        "\"wochentage\": [\"mo\",\"di\",\"mi\",\"do\",\"fr\",\"sa\",\"so\"], \"ordner\": \"~/Downloads\", \"muster\": \"*.pdf\"}, " +
        "\"schritte\": [{\"art\": \"ordner_anlegen|kopieren|verschieben|datum_voranstellen|text_schreiben|zippen|ki_zusammenfassen\", \"von\": \"~/Ordner\", \"nach\": \"~/Ordner oder ~/Ordner/datei.txt\", \"muster\": \"*.pdf\", \"text\": \"Inhalt\"}]}\n" +
        "Regeln: Pfade beginnen mit ~/ (Benutzerordner, z. B. ~/Documents, ~/Downloads, ~/Pictures). Platzhalter {datum} (JJJJ-MM-TT) und {zeit} sind in nach und text erlaubt. " +
        "\"wochentage\" nur bei taeglich und nur, wenn der Satz bestimmte Tage nennt. \"uhrzeit\" bei taeglich, \"minuten\" bei intervall, \"ordner\"/\"muster\" bei neue_datei. " +
        "Höchstens 20 Schritte. Keine anderen Schrittarten. Nichts löschen.";

    /// <summary>Die Frage an die KI.</summary>
    public static string Prompt(string sentence) => "Satz: " + sentence.Trim() + "\n\nAntworte nur mit dem JSON-Objekt.";

    public sealed record Result(Workflow? Workflow, IReadOnlyList<string> Problems);

    static readonly Dictionary<string, TriggerKind> Triggers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["manuell"] = TriggerKind.Manuell, ["taeglich"] = TriggerKind.Taeglich, ["täglich"] = TriggerKind.Taeglich,
        ["intervall"] = TriggerKind.Intervall, ["neue_datei"] = TriggerKind.NeueDatei,
    };

    static readonly Dictionary<string, StepKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ordner_anlegen"] = StepKind.OrdnerAnlegen, ["kopieren"] = StepKind.Kopieren, ["verschieben"] = StepKind.Verschieben,
        ["datum_voranstellen"] = StepKind.DatumVoranstellen, ["text_schreiben"] = StepKind.TextSchreiben, ["zippen"] = StepKind.Zippen,
        ["ki_zusammenfassen"] = StepKind.KiZusammenfassen,
    };

    static readonly Dictionary<string, DayOfWeek> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mo"] = DayOfWeek.Monday, ["di"] = DayOfWeek.Tuesday, ["mi"] = DayOfWeek.Wednesday, ["do"] = DayOfWeek.Thursday,
        ["fr"] = DayOfWeek.Friday, ["sa"] = DayOfWeek.Saturday, ["so"] = DayOfWeek.Sunday,
    };

    /// <summary>Antwort der KI prüfen und in einen Ablauf übersetzen. Bei Problemen gibt es keinen Ablauf (nichts wird halb angelegt).</summary>
    /// <param name="home">Benutzerordner, unter dem alle Pfade liegen müssen (wie bei der Engine).</param>
    public static Result FromAnswer(string? answer, string home, string sentence)
    {
        var problems = new List<string>();
        if (Extract(answer) is not { } o) return new(null, ["Die KI hat keinen Ablauf beschrieben (keine lesbare Antwort)."]);
        var w = new Workflow { Enabled = false, Origin = sentence.Trim() };
        w.Name = Str(o, "name") is { Length: > 0 } n ? (n.Length > 80 ? n[..80] : n) : "Ablauf aus einem Satz";

        var trig = o["ausloeser"] as JsonObject ?? new JsonObject();
        var art = Str(trig, "art") ?? "manuell";
        if (!Triggers.TryGetValue(art, out var tk)) { problems.Add($"Unbekannter Auslöser „{art}“."); tk = TriggerKind.Manuell; }
        w.Trigger = tk;
        switch (tk)
        {
            case TriggerKind.Taeglich:
                if (TimeOnly.TryParse(Str(trig, "uhrzeit") ?? "", out var at)) w.At = at.ToString("HH:mm"); else problems.Add("Für „jeden Tag“ fehlt eine gültige Uhrzeit (HH:MM).");
                if (trig["wochentage"] is JsonArray days)
                    foreach (var d in days.Select(x => x?.ToString().Trim() ?? ""))
                        if (DayNames.TryGetValue(d.Length >= 2 ? d[..2] : d, out var dw)) { if (!w.Days.Contains(dw)) w.Days.Add(dw); }
                        else problems.Add($"Unbekannter Wochentag „{d}“.");
                if (w.Days.Count == 7) w.Days.Clear(); // alle Tage = keine Bedingung
                break;
            case TriggerKind.Intervall:
                if (Int(trig, "minuten") is > 0 and <= 60 * 24 * 31 and var m) w.EveryMinutes = m; else problems.Add("Für „alle X Minuten“ fehlt eine gültige Minutenzahl.");
                break;
            case TriggerKind.NeueDatei:
                if (FullPath(Str(trig, "ordner"), home, problems, "Beobachteter Ordner") is { } f) w.WatchFolder = f; else if (problems.Count == 0) problems.Add("Für „neue Datei“ fehlt der Ordner.");
                w.WatchPattern = Pattern(Str(trig, "muster"));
                break;
        }

        if (o["schritte"] is not JsonArray steps || steps.Count == 0) problems.Add("Die KI hat keine Schritte genannt.");
        else if (steps.Count > MaxSteps) problems.Add($"Höchstens {MaxSteps} Schritte.");
        else
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i] is not JsonObject s) { problems.Add($"Schritt {i + 1} ist unlesbar."); continue; }
                var k = Str(s, "art") ?? "";
                if (k.Contains("agent", StringComparison.OrdinalIgnoreCase)) { problems.Add($"Schritt {i + 1}: Einen Agenten wählst du selbst im Ablauf – die KI legt keinen an."); continue; }
                if (!Kinds.TryGetValue(k, out var kind)) { problems.Add($"Schritt {i + 1}: unbekannte Art „{k}“."); continue; }
                var step = new WfStep { Kind = kind, Pattern = Pattern(Str(s, "muster")), Text = Str(s, "text") ?? "" };
                var from = FullPath(Str(s, "von"), home, problems, $"Schritt {i + 1} (von)"); var to = FullPath(Str(s, "nach"), home, problems, $"Schritt {i + 1} (nach)");
                step.From = from ?? ""; step.To = to ?? "";
                var needFrom = kind is StepKind.Kopieren or StepKind.Verschieben or StepKind.DatumVoranstellen or StepKind.Zippen or StepKind.KiZusammenfassen;
                var needTo = kind is StepKind.OrdnerAnlegen or StepKind.Kopieren or StepKind.Verschieben or StepKind.TextSchreiben or StepKind.Zippen;
                if (needFrom && step.From.Length == 0 && from is null && !problems.Any(p => p.StartsWith($"Schritt {i + 1} (von)"))) problems.Add($"Schritt {i + 1} „{WorkflowEngine.StepLabel(kind)}“: Ordner „von“ fehlt.");
                if (needTo && step.To.Length == 0 && to is null && !problems.Any(p => p.StartsWith($"Schritt {i + 1} (nach)"))) problems.Add($"Schritt {i + 1} „{WorkflowEngine.StepLabel(kind)}“: Ziel „nach“ fehlt.");
                w.Steps.Add(step);
            }
        return problems.Count > 0 ? new(null, problems) : new(w, []);
    }

    /// <summary>Wochentage als kurzer Text („Mo, Fr“; leer = jeden Tag).</summary>
    public static string DaysText(IEnumerable<DayOfWeek> days) =>
        string.Join(", ", days.Distinct().OrderBy(d => ((int)d + 6) % 7).Select(d => DayNames.First(x => x.Value == d).Key switch { var k => char.ToUpper(k[0]) + k[1..] }));

    /// <summary>„Mo, Fr“ / „montags und freitags“ → Wochentage; leer → keine Bedingung; unbekannt → null.</summary>
    public static List<DayOfWeek>? ParseDays(string? text)
    {
        var list = new List<DayOfWeek>();
        foreach (var part in (text ?? "").Split([',', ' ', ';', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals("und", StringComparison.OrdinalIgnoreCase)) continue;
            if (part.Length < 2 || !DayNames.TryGetValue(part[..2], out var d)) return null;
            if (!list.Contains(d)) list.Add(d);
        }
        return list.Count == 7 ? [] : list;
    }

    /// <summary>JSON-Objekt aus der Antwort holen (auch in ```json … ``` oder mit Text davor/danach).</summary>
    static JsonObject? Extract(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return null;
        var a = answer.IndexOf('{'); var b = answer.LastIndexOf('}');
        if (a < 0 || b <= a) return null;
        try { return JsonNode.Parse(answer[a..(b + 1)]) as JsonObject; } catch (JsonException) { return null; }
    }

    static string? Str(JsonObject o, string k) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : o[k] is JsonValue n ? n.ToString().Trim() : null;
    static int? Int(JsonObject o, string k) => o[k] is JsonValue v && (v.TryGetValue<int>(out var i) || int.TryParse(v.ToString(), out i)) ? i : null;
    static string Pattern(string? p) => string.IsNullOrWhiteSpace(p) || p.IndexOfAny(['/', '\\', ':']) >= 0 ? "*" : p.Trim();

    /// <summary>„~/Ordner“ oder relativer Pfad → unter dem Benutzerordner; absolute Pfade nur, wenn sie darin liegen. Leer → null ohne Problem.</summary>
    static string? FullPath(string? p, string home, List<string> problems, string what)
    {
        if (string.IsNullOrWhiteSpace(p)) return null;
        p = p.Trim().Replace('/', IOPath.DirectorySeparatorChar).Replace('\\', IOPath.DirectorySeparatorChar);
        if (p.Contains(".." + IOPath.DirectorySeparatorChar) || p.EndsWith("..")) { problems.Add($"{what}: „..“ ist nicht erlaubt."); return null; }
        if (p == "~") p = ""; else if (p.StartsWith("~" + IOPath.DirectorySeparatorChar)) p = p[2..];
        var full = IOPath.GetFullPath(IOPath.IsPathRooted(p) ? p : IOPath.Combine(home, p));
        var root = IOPath.GetFullPath(home).TrimEnd(IOPath.DirectorySeparatorChar) + IOPath.DirectorySeparatorChar;
        if (!(full + IOPath.DirectorySeparatorChar).StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        { problems.Add($"{what}: Ordner außerhalb deines Benutzerordners ist nicht erlaubt."); return null; }
        return full;
    }
}
