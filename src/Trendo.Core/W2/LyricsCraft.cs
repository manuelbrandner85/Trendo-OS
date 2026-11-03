using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Trendo Musik – Liedtexte wie von Profis (Wunsch 03.11.2026: „keine KI-Phrasen“): feste Songwriter-Regeln für die Text-KI
/// und eine Prüfung auf typische KI-Floskeln, die Text-KIs in Liedtexten immer wieder benutzen.</summary>
public static partial class LyricsCraft
{
    /// <summary>Regeln für jeden Liedtext-Auftrag an die Text-KI.</summary>
    public const string Rules =
        "Schreibe wie ein erfahrener Songwriter, nicht wie eine KI: konkrete Bilder aus dem Alltag (Orte, Gegenstände, Handlungen, Tageszeiten) statt großer Gefühlswörter; " +
        "zeigen statt behaupten; eine klare Geschichte oder Situation mit einer Person, die etwas erlebt; natürliche Umgangssprache, so wie Menschen wirklich reden; " +
        "in jedem Abschnitt etwa gleich viele Silben pro Zeile; Reime gern unrein und überraschend, nie erzwungen; ein kurzer, wiederholter Refrain-Haken, der hängen bleibt; " +
        "die Strophen erzählen weiter statt dasselbe zu wiederholen. Verboten sind abgegriffene KI-Floskeln wie " +
        "„Neonlicht“, „Schatten der Nacht“, „Herz in Flammen“, „Sternenstaub“, „Echo“, „unendlich“, „Seele brennt“, „tanzen im Regen“, „frei wie der Wind“, " +
        "„neon lights“, „shadows“, „whispers“, „embers“, „ignite“, „tapestry“, „symphony“, „chasing dreams“, „heart on fire“, „we are infinite“.";

    /// <summary>Typische KI-Floskeln (Kleinschreibung, Wortanfang genügt bei Wortstämmen mit *).</summary>
    public static readonly string[] Phrases =
    [
        // Deutsch
        "neonlicht", "neonlichter", "schatten der nacht", "in der dunkelheit", "herz in flammen", "herz aus gold", "sternenstaub", "sternenmeer", "echo der", "echos",
        "unendlich*", "ewigkeit", "seele brennt", "tanzen im regen", "tanz im regen", "frei wie der wind", "frei wie ein vogel", "flügel aus", "zwischen den sternen",
        "melodie des lebens", "symphonie", "flüstern der", "flüsternd", "glut", "funkeln", "funkelnd", "leuchtfeuer", "phönix", "aus der asche", "stärker als je zuvor",
        "wir sind eins", "gemeinsam sind wir", "grenzenlos", "zeitlos", "kaleidoskop", "labyrinth", "mosaik", "puzzle meines",
        // Englisch
        "neon light*", "neon glow", "neon dream*", "shadows", "in the shadows", "whisper*", "ember*", "ignite*", "tapestry", "symphony", "kaleidoscope", "labyrinth",
        "chasing dreams", "chase the dream*", "heart on fire", "heart of gold", "we are infinite", "infinite", "stardust", "echoes", "echoing", "soar*", "rise above",
        "set me free", "break the chains", "electric", "electrifying", "dancing in the rain", "into the night", "through the night", "midnight sky", "cosmic", "ethereal",
        "phoenix", "from the ashes", "unbreakable", "unstoppable", "against all odds", "journey", "destiny",
    ];

    /// <summary>Gefundene KI-Floskeln im Text (ohne Abschnitts-Marken), jede höchstens einmal, in der Reihenfolge der Liste.</summary>
    public static List<string> FindPhrases(string lyrics)
    {
        var t = Brackets().Replace(lyrics, " ").ToLowerInvariant();
        t = " " + NonWord().Replace(t, " ") + " ";
        var hits = new List<string>();
        foreach (var p in Phrases)
        {
            var stem = p.TrimEnd('*').Replace("*", " ");
            var pattern = @"(?<![\p{L}])" + Regex.Escape(stem).Replace(@"\ ", @"\s+") + (p.EndsWith('*') ? @"\p{L}*" : @"(?![\p{L}])");
            if (Regex.IsMatch(t, pattern) && !hits.Contains(stem)) hits.Add(stem);
        }
        // Kürzere Treffer weglassen, die schon in einem längeren stecken („shadows“ in „in the shadows“)
        return hits.Where(h => !hits.Any(o => o != h && o.Contains(h))).ToList();
    }

    /// <summary>Auftrag an die Text-KI, nur die gefundenen Floskeln zu ersetzen – Aufbau, Reime und alles andere bleiben.</summary>
    public static string RewritePrompt(string lyrics, IReadOnlyList<string> hits) =>
        "Überarbeite diesen Liedtext wie ein erfahrener Songwriter. Ersetze diese abgegriffenen Floskeln durch eigene, konkrete Bilder aus dem Alltag: " +
        string.Join(", ", hits.Select(h => "„" + h + "“")) + ". Behalte Abschnitts-Marken, Aufbau, Silbenzahl und Reimschema bei; ändere nur die betroffenen Zeilen. " +
        "Gib nur den Liedtext aus.\n\n" + lyrics;

    [GeneratedRegex(@"\[[^\]]*\]")] private static partial Regex Brackets();
    [GeneratedRegex(@"[^\p{L}\s]")] private static partial Regex NonWord();
}
