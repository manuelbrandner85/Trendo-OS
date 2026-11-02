using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Spiele-Regeln für Claude im Spiele-Studio (Wunsch 02.11.2026): eingebaute, neutrale Regeln nach dem Vorbild eines Spielestudio-Ablaufs – für alle Nutzer,
/// ohne persönliche Projekte oder Namen. Ist das Claude-Plugin GAMEFORGE auf dem PC, arbeitet Claude zusätzlich mit dessen Skills und Agenten.</summary>
public static class GameRules
{
    public const string Studio = "CORE-06";

    public const string Text =
        "\nRegeln für Spieleentwicklung in Trendo:" +
        "\n- Status ehrlich führen: Idee → geplant → umgesetzt (Datei existiert) → getestet → freigegeben. Nur was als Datei nachgewiesen ist, gilt als gebaut; nie Erfolg melden ohne Beweis (Datei, Prüfung, Bildschirmfoto)." +
        "\n- Entscheidungen als Ja/Nein-Vorschlag mit Empfehlung zuerst; keine Fachwörter ohne kurze Erklärung." +
        "\n- Die Größe bestimmt das Werkzeug: kleine 2D-/Retro-/Web-Spiele im Browser (HTML5) oder mit Godot 4; 3D, AA und AAA+ mit Unreal Engine 5. Wechsel der Engine nie heimlich." +
        "\n- Erst blocken, dann verschönern: Level mit Platzhaltern (BLOCK_) aufbauen und spielbar machen, danach fertige Bausteine nutzen, eigene 3D-Modelle (Blender) nur für das, was dann noch fehlt; Licht und Look zum Schluss." +
        "\n- Maße und Namen: Unreal 1 Einheit = 1 cm, Z oben, X vorwärts; Godot/glTF 1 Einheit = 1 m; Vorsilben SM_ SK_ M_ MI_ T_ BP_ NS_ LS_ UCX_ BLOCK_." +
        "\n- Jedes Spiel ist getrennt; die Spielbibel (Story, Figuren, Welt, Missionen) ist die Quelle der Wahrheit. Den festgelegten Story-Kanon nur nach ausdrücklicher Zustimmung ändern, vorher Vorher/Nachher zeigen." +
        "\n- Vor Löschen oder großen Umbauten sichern; Assets nie ohne Zustimmung importieren, löschen oder überschreiben; nichts „fertig“ ohne echten Test im Editor bzw. Spiel." +
        "\n- Keine kostenpflichtigen Dienste, keine Käufe, keine Veröffentlichung ohne Zustimmung. Der Stil des Spiels bestimmt den Look – Qualität im Stil des Spiels, so hoch wie sinnvoll." +
        "\n- Grafikkarte schonen: immer nur ein schwerer Grafikjob gleichzeitig (Bild-KI, Rendern); fertige Endqualität nie selbst senken." +
        "\n- Wie ein professionelles Spielestudio arbeiten – aber für den Nutzer einfach: Konzept → Vorproduktion (Spielbibel, Stil, spielbarer Prototyp) → Produktion in Meilensteinen → Test und Qualität → Veröffentlichung. Die Meilensteine stehen im Feld „milestones“." +
        "\n- Ziel klären: Sagt der Nutzer bei einem neuen Spiel nicht, wo es erscheinen soll, stelle genau diese eine Frage: " + ClaudeChats.QuestionMark + " Wie soll dein Spiel erscheinen? [Browser-Spiel | Windows-Setup | Windows und Mac | Steam | itch.io | Handy] – und trage die Antwort im Feld „platforms“ ein." +
        "\n- Alle Formulare ausfüllen: Fülle im Spiele-Dokument jedes Feld (title, scale, genre, logline, loop, controls, style, audience, platforms, sound, menus, systems, mechanics, characters, world, quests, milestones) – Feld für Feld und jeweils gespeichert, damit der Nutzer live im Programm zusieht. Listen im vorhandenen Aufbau (z. B. mechanics als Liste kurzer Texte, characters/world/quests als Einträge mit den vorhandenen Feldern).";
}

/// <summary>Claude-Plugin GAMEFORGE auf dem PC: wird erkannt (neueste Version im Plugin-Speicher von Claude Code) und im Spiele-Studio genutzt –
/// im vollen Modus lädt Claude es selbst, im sicheren Modus legt Trendo seine Skills als Projekt-Skills dazu. Trendo verändert das Plugin nie.</summary>
public static class GameForge
{
    public sealed record Install(string Version, string Root, IReadOnlyList<string> Skills, IReadOnlyList<string> Agents);

    /// <summary>Neueste GAMEFORGE-Version in den Claude-Ordnern (<c>plugins/cache/&lt;quelle&gt;/gameforge/&lt;version&gt;</c>) – oder null.</summary>
    public static Install? Find(IEnumerable<string?> claudeDirs)
    {
        Install? best = null; Version? bestV = null;
        foreach (var dir in claudeDirs.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var cache = Path.Combine(dir, "plugins", "cache");
            if (!Directory.Exists(cache)) continue;
            IEnumerable<string> roots;
            try { roots = Directory.EnumerateDirectories(cache).Select(src => Path.Combine(src, "gameforge")).Where(Directory.Exists).SelectMany(Directory.EnumerateDirectories).ToList(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var r in roots)
            {
                if (!Version.TryParse(Path.GetFileName(r), out var v) || !Directory.Exists(Path.Combine(r, "skills"))) continue;
                if (bestV is not null && v <= bestV) continue;
                var skills = Directory.EnumerateDirectories(Path.Combine(r, "skills")).Where(d => File.Exists(Path.Combine(d, "SKILL.md"))).Select(Path.GetFileName).OfType<string>().OrderBy(x => x).ToList();
                var agents = Directory.Exists(Path.Combine(r, "agents")) ? Directory.EnumerateFiles(Path.Combine(r, "agents"), "*.md").Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(x => x).ToList() : [];
                (best, bestV) = (new Install(v.ToString(), r, skills, agents), v);
            }
        }
        return best;
    }

    /// <summary>Anweisung an Claude: GAMEFORGE nutzen – Trendos Programm-Dokument bleibt das Projekt.</summary>
    public static string Rule(Install i) =>
        $"\nGAMEFORGE {i.Version} ist auf diesem PC eingerichtet. Arbeite im Spiele-Studio nach seinen Regeln: lade zuerst den Skill „gameforge-studio“ und nutze passend " +
        string.Join(", ", i.Skills.Where(s => s != "gameforge-studio").Take(14).Select(s => "„" + s + "“")) +
        (i.Agents.Count > 0 ? $"; für große Aufgaben verteilt der Agent „gameforge:studio-director“ an die Spezialisten ({i.Agents.Count} Agenten)." : ".") +
        " Wichtig: In Trendo ist das Projekt das Dokument im Programm „Spiele bauen“ im Projektordner – trage alles zuerst dort ein; GAMEFORGE-Dateien (z. B. Spielbibel als Markdown) nur zusätzlich in diesem Projektordner, keine Projektordner woanders anlegen.";

    /// <summary>Für den sicheren Modus: Skill-Texte (nur .md) als Projekt-Skills „gameforge-…“ in den Projektordner legen. Liefert die Anzahl.</summary>
    public static int ExportSkills(Install i, string projectDir)
    {
        var n = 0;
        foreach (var s in i.Skills)
        {
            var src = Path.Combine(i.Root, "skills", s); var dst = Path.Combine(projectDir, ".claude", "skills", "gameforge-" + s);
            try
            {
                Directory.CreateDirectory(dst);
                foreach (var f in Directory.EnumerateFiles(src, "*.md")) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
                n++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return n;
    }
}

/// <summary>Selbstlernende Studio-Regeln (Wunsch 02.11.2026, alle Studios): aus jedem Projekt lernt Trendo, was künftig besser laufen soll – aus Claudes Erkenntnissen,
/// Gutachten, behobenen Prüffehlern und deinen Korrekturen (Zeilen „Lernregel: …“). Eine neue Regel ist zuerst nur ein Vorschlag und gilt erst nach deinem Ja –
/// dann für alle künftigen Projekte des Studios. Jede Regel hat Herkunft und Verlauf und lässt sich zurücknehmen.</summary>
public sealed partial class StudioLearning(string dataRoot)
{
    public const string Mark = "Lernregel:";
    public const int MaxActive = 40;
    public enum State { Vorschlag, Aktiv, Abgelehnt, Zurueckgenommen }
    public sealed record Rule(string Id, string Text, string Source, State Status, DateTimeOffset Created, DateTimeOffset? Decided = null, string? Project = null);

    string FileOf(string studio) => Path.Combine(dataRoot, "claude-chats", "lernen", Regex.Replace(studio.ToLowerInvariant(), "[^a-z0-9-]", "-") + ".json");

    public IReadOnlyList<Rule> All(string studio)
    {
        try { return File.Exists(FileOf(studio)) ? JsonSerializer.Deserialize<List<Rule>>(File.ReadAllText(FileOf(studio))) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    void Save(string studio, List<Rule> rules)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf(studio))!);
        var tmp = FileOf(studio) + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(rules)); File.Move(tmp, FileOf(studio), true);
    }
    public IReadOnlyList<Rule> Active(string studio) => All(studio).Where(r => r.Status == State.Aktiv).ToList();
    public IReadOnlyList<Rule> Pending(string studio) => All(studio).Where(r => r.Status == State.Vorschlag).ToList();

    static string Norm(string t) => Regex.Replace(t.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    /// <summary>Neue Regel vorschlagen. Gleiche Regel (auch abgelehnt oder schon aktiv) wird nicht noch einmal vorgeschlagen. Gibt den Vorschlag oder null zurück.</summary>
    public Rule? Propose(string studio, string text, string source, string? project = null)
    {
        var t = Regex.Replace(text.Trim().Trim('*', '_', '„', '“', '"'), @"\s+", " ");
        if (t.Length < 8) return null;
        if (t.Length > 280) t = t[..279] + "…";
        var rules = All(studio).ToList();
        if (rules.Any(r => Norm(r.Text) == Norm(t) && r.Status != State.Zurueckgenommen)) return null;
        var r = new Rule(Guid.NewGuid().ToString("N")[..10], t, source, State.Vorschlag, DateTimeOffset.Now, null, project);
        rules.Add(r); Save(studio, rules); return r;
    }

    Rule? Set(string studio, string id, State s)
    {
        var rules = All(studio).ToList(); var i = rules.FindIndex(r => r.Id == id);
        if (i < 0) return null;
        rules[i] = rules[i] with { Status = s, Decided = DateTimeOffset.Now };
        if (s == State.Aktiv) // höchstens MaxActive aktive Regeln – die älteste aktive wird zurückgenommen
            while (rules.Count(r => r.Status == State.Aktiv) > MaxActive) { var old = rules.FindIndex(r => r.Status == State.Aktiv); rules[old] = rules[old] with { Status = State.Zurueckgenommen, Decided = DateTimeOffset.Now }; }
        Save(studio, rules); return rules[i];
    }
    public Rule? Approve(string studio, string id) => Set(studio, id, State.Aktiv);
    public Rule? Reject(string studio, string id) => Set(studio, id, State.Abgelehnt);
    public Rule? Retract(string studio, string id) => Set(studio, id, State.Zurueckgenommen);

    /// <summary>Teil der Anweisungen: geltende Studio-Regeln und wie Claude neue vorschlägt.</summary>
    public string Prompt(string studio)
    {
        var active = Active(studio);
        return (active.Count > 0 ? "\nGelernte Regeln dieses Studios (vom Nutzer bestätigt, gelten für alle Projekte):\n" + string.Join("\n", active.Select(r => "- " + r.Text)) : "") +
            "\nLernst du in dieser Arbeit etwas, das künftig für ALLE Projekte dieses Studios gelten sollte (ein allgemeines Muster, nicht nur dieses Projekt), schreib es in eine eigene Zeile „" + Mark + " <kurze, klare Regel>“. Der Nutzer entscheidet, ob sie gilt.";
    }

    [GeneratedRegex(@"^[ \t>*_-]*Lernregel:\**\s*(?<t>.+?)\s*$", RegexOptions.Multiline)] private static partial Regex MarkLine();
    public static IReadOnlyList<string> Extract(string answer) => MarkLine().Matches(answer).Select(m => m.Groups["t"].Value.Trim('*', '_', ' ')).Where(t => t.Length > 0).ToList();
    public static string Strip(string answer) => Regex.Replace(MarkLine().Replace(answer, ""), @"\n{3,}", "\n\n").Trim();

    [GeneratedRegex(@"\b(nein|falsch|nicht so|immer|nie|niemals|bitte nicht|lieber|stattdessen|grundsätzlich)\b", RegexOptions.IgnoreCase)] private static partial Regex CorrectionRx();
    /// <summary>Klingt die Nachricht nach einer grundsätzlichen Korrektur durch den Nutzer?</summary>
    public static bool IsCorrection(string userText) => CorrectionRx().IsMatch(userText);

    /// <summary>Herkunft einer Lernregel aus dem Zusammenhang: Gutachten, Prüfung (Nachbesserung), deine Korrektur oder Claudes Erkenntnis.</summary>
    public static string SourceOf(bool review, bool check, string? userText) =>
        review ? "Gutachter" : check ? "Prüfung (behobener Fehler)" : userText is not null && IsCorrection(userText) ? "Deine Korrektur" : "Claudes Erkenntnis";
}
