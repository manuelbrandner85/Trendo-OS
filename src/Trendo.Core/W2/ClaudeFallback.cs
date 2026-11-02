using System.Text.Json;
using System.Text.RegularExpressions;
using Trendo.Core.Programs;

namespace Trendo.Core;

/// <summary>Claude-Abo aufgebraucht (Wunsch 02.11.2026): Trendo merkt sich, bis wann – bis dahin arbeitet eine kostenlose Ersatz-KI, danach geht die nächste
/// Nachricht automatisch wieder an Claude. Gespeichert im Datenordner der Person.</summary>
public sealed partial class ClaudeLimit(string dataRoot)
{
    string FileOf => Path.Combine(dataRoot, "claude-chats", "kontingent.json");
    /// <summary>Wie lange Trendo wartet, wenn Claude keine Uhrzeit nennt – danach wird Claude einfach wieder versucht.</summary>
    public static readonly TimeSpan Retry = TimeSpan.FromMinutes(30);

    public DateTimeOffset? Until
    {
        get { try { return File.Exists(FileOf) && JsonSerializer.Deserialize<DateTimeOffset?>(File.ReadAllText(FileOf)) is { } u ? u : null; } catch (Exception e) when (e is JsonException or IOException) { return null; } }
    }
    public bool Active(DateTimeOffset now) => Until is { } u && u > now;
    public void Set(DateTimeOffset until) { Directory.CreateDirectory(Path.GetDirectoryName(FileOf)!); File.WriteAllText(FileOf, JsonSerializer.Serialize(until)); }
    public void Clear() { if (File.Exists(FileOf)) File.Delete(FileOf); }

    [GeneratedRegex(@"\|(\d{10})\b")] private static partial Regex Epoch();
    [GeneratedRegex(@"resets?\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)?", RegexOptions.IgnoreCase)] private static partial Regex ResetAt();

    /// <summary>Wann das Kontingent laut Claudes Meldung wieder frei ist („…limit reached|1730000000“ oder „resets 3pm“); sonst jetzt + <see cref="Retry"/>.</summary>
    public static DateTimeOffset ResetOf(string? message, DateTimeOffset now)
    {
        if (message is not null && Epoch().Match(message) is { Success: true } e && long.TryParse(e.Groups[1].Value, out var s))
        { var t = DateTimeOffset.FromUnixTimeSeconds(s).ToLocalTime(); if (t > now && t < now.AddDays(8)) return t; }
        if (message is not null && ResetAt().Match(message) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var h))
        {
            var min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            if (m.Groups[3].Value.Equals("pm", StringComparison.OrdinalIgnoreCase) && h < 12) h += 12;
            if (m.Groups[3].Value.Equals("am", StringComparison.OrdinalIgnoreCase) && h == 12) h = 0;
            if (h is >= 0 and < 24 && min is >= 0 and < 60)
            {
                var t = new DateTimeOffset(now.Year, now.Month, now.Day, h, min, 0, now.Offset);
                return t > now ? t : t.AddDays(1);
            }
        }
        return now + Retry;
    }
}

/// <summary>Kostenlose KI-Werkzeuge im Claude-Chat (Bilder, Video, Bilder verbessern, Musik, Vorlesen, Mitschreiben): Claude oder die Ersatz-KI bestellt sie mit einer Zeile
/// „[[trendo:bild | Beschreibung | datei.png]]“, Trendo erzeugt die Datei mit der KI-Bibliothek auf dem PC und legt sie ins Projekt.</summary>
public static partial class ClaudeTools
{
    public sealed record Call(string Kind, string Input, string File);

    public static readonly IReadOnlyDictionary<string, (KiKind Kind, string Ext, string Label)> Kinds = new Dictionary<string, (KiKind, string, string)>
    {
        ["bild"] = (KiKind.BildErzeugen, ".png", "Bild erzeugen"),
        ["video"] = (KiKind.Video, ".webm", "Video erzeugen"),
        ["verbessern"] = (KiKind.BildVerbessern, ".png", "Bild verbessern"),
        ["musik"] = (KiKind.Musik, ".mp3", "Musik erzeugen"),
        ["vorlesen"] = (KiKind.TextZuSprache, ".wav", "Vorlesen"),
        ["mitschreiben"] = (KiKind.SpracheZuText, ".txt", "Mitschreiben"),
    };

    public const string Rule =
        "\nKostenlose KI-Werkzeuge von Trendo (auf dem PC, ohne dein Kontingent): Brauchst du ein Bild, Video, Musik, eine Sprachausgabe, eine Mitschrift oder ein vergrößertes Bild, " +
        "schreib dafür jeweils eine eigene Zeile: [[trendo:ART | BESCHREIBUNG ODER EINGABE | ZIELDATEI]] – ART ist bild, video, verbessern, musik, vorlesen oder mitschreiben; " +
        "bei verbessern und mitschreiben ist die Eingabe eine Datei im Projekt; die Zieldatei liegt relativ im Projektordner (z. B. bilder/held.png). Trendo erzeugt sie danach und legt sie dorthin.";

    [GeneratedRegex(@"^[ \t]*\[\[trendo:(?<k>[a-zäöü]+)\s*\|\s*(?<i>[^|\]\n]+?)\s*(?:\|\s*(?<f>[^|\]\n]+?)\s*)?\]\][ \t\r]*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex Marker();

    /// <summary>Werkzeug-Bestellungen aus einer Antwort (höchstens 6; Zieldatei sicher im Projekt, sonst ein Name aus der Art).</summary>
    public static IReadOnlyList<Call> Parse(string answer)
    {
        var list = new List<Call>();
        foreach (Match m in Marker().Matches(answer))
        {
            var k = m.Groups["k"].Value.ToLowerInvariant();
            if (!Kinds.TryGetValue(k, out var info)) continue;
            var f = m.Groups["f"].Success ? m.Groups["f"].Value.Trim().Replace('\\', '/') : "";
            if (f.Length == 0 || f.Contains("..") || Path.IsPathRooted(f) || f.Contains(':')) f = $"medien/{k}-{list.Count + 1}{info.Ext}";
            if (!Path.HasExtension(f)) f += info.Ext;
            list.Add(new(k, m.Groups["i"].Value.Trim(), f));
            if (list.Count == 6) break;
        }
        return list;
    }

    /// <summary>Antwort ohne die Werkzeug-Zeilen (für die Anzeige).</summary>
    public static string Strip(string answer) => Regex.Replace(Marker().Replace(answer, ""), @"\n{3,}", "\n\n").Trim();
}

/// <summary>Ersatz-KI für den Claude-Chat: derselbe Auftrag im Format des Trendo-Agenten (Zusammenfassung + vollständige Dateien), damit auch freie KIs
/// Dateien im Projekt anlegen und ändern können.</summary>
public static class ClaudeFallback
{
    public static string Prompt(string text, string system, string root) =>
        TrendoAgent.ChangePrompt(text, new AgentPlan([text], [], text), new Dictionary<string, string>(), system + ClaudeTools.Rule +
            "\nIst keine Datei zu ändern (z. B. eine Frage), gib \"dateien\": [] zurück und antworte in \"zusammenfassung\".", root);

    /// <summary>Zusammenfassung aus der JSON-Antwort, sonst der Text selbst (freie KIs antworten manchmal ohne JSON).</summary>
    public static string Summary(string answer)
    {
        var m = Regex.Match(answer, "\"zusammenfassung\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        if (m.Success) try { return JsonSerializer.Deserialize<string>("\"" + m.Groups[1].Value + "\"") ?? answer; } catch (JsonException) { return m.Groups[1].Value; }
        return Regex.IsMatch(answer.Trim(), @"^\{[\s\S]*\}$") ? "Erledigt." : answer.Trim();
    }

    /// <summary>Werkzeug-Zeilen können in der Zusammenfassung oder im Rohtext stehen.</summary>
    public static string ToolText(string answer) => answer.Replace("\\n", "\n");
}
