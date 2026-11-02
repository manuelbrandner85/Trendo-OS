using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Claude-Projekte je Studio (Wunsch 01.11.2026: „wie in Claude“ – Projekte erscheinen im Studio, lassen sich immer wieder fortsetzen, bis man sie löscht).
/// Ein Projekt ist ein Gespräch mit Claude Code samt eigenem Arbeitsordner im Studio: Verlauf (Nachrichten und Arbeitsschritte) liegt im Datenordner der Person,
/// die Sitzung von Claude Code wird mit <c>--resume</c> fortgesetzt. Die erste Nachricht in einem Studio legt das Projekt automatisch an.</summary>
public sealed partial class ClaudeChats(string dataRoot)
{
    public sealed record Step(string Text, bool Changes, bool Error = false);
    /// <summary><paramref name="Ki"/>: welche KI die Antwort geschrieben hat (Claude oder eine Ersatz-KI).</summary>
    public sealed record Msg(string Role, string Text, List<Step> Steps, DateTimeOffset At, List<string>? Changed = null, int Tokens = 0, List<string>? Attachments = null, string? Context = null, string? Ki = null, List<string>? Used = null);
    /// <summary><paramref name="Full"/>: volle Claude-Funktionen für dieses Projekt freigegeben (Befehle, Internet, eigene Skills, Agenten, Plugins/Mods, MCP-Werkzeuge);
    /// <paramref name="Safe"/>: dieses Projekt ausdrücklich auf „sicher“ gestellt (gilt auch, wenn volle Funktionen sonst immer an sind).</summary>
    public sealed record Chat(string Id, string Studio, string Title, string Folder, string? Session, DateTimeOffset Created, DateTimeOffset Last, List<Msg> Messages, bool Full = false, bool Safe = false)
    {
        /// <summary>Volle Funktionen in diesem Projekt wirksam? <paramref name="always"/>: Vorgabe „immer bei Bedarf Internet und Befehle“.</summary>
        public bool FullWith(bool always) => !Safe && (Full || always);
    }

    string Dir => Path.Combine(dataRoot, "claude-chats");
    string FileOf(string studio) => Path.Combine(Dir, Regex.Replace(studio.ToLowerInvariant(), "[^a-z0-9-]", "-") + ".json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    List<Chat> Load(string studio)
    {
        try { return File.Exists(FileOf(studio)) ? JsonSerializer.Deserialize<List<Chat>>(File.ReadAllText(FileOf(studio)), Json) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    void Save(string studio, List<Chat> all)
    {
        Directory.CreateDirectory(Dir);
        var tmp = FileOf(studio) + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(all, Json)); File.Move(tmp, FileOf(studio), true);
    }

    /// <summary>Projekte des Studios, zuletzt benutztes zuerst.</summary>
    public IReadOnlyList<Chat> All(string studio) => Load(studio).OrderByDescending(c => c.Last).ToList();
    public Chat? Get(string studio, string id) => Load(studio).FirstOrDefault(c => c.Id == id);

    /// <summary>Titel aus der ersten Nachricht (wie Claude ihn für einen Chat wählt): erste Zeile, höchstens 48 Zeichen.</summary>
    public static string TitleOf(string text)
    {
        var t = Regex.Replace(text.Trim().Split('\n')[0], @"\s+", " ").Trim().TrimEnd('.', '!', '?');
        return t.Length == 0 ? "Neues Projekt" : t.Length > 48 ? t[..47].TrimEnd() + "…" : t;
    }

    /// <summary>Ordnername für ein Projekt: aus dem Titel, eindeutig im Studio-Ordner.</summary>
    public static string FolderFor(string studioRoot, string title)
    {
        var slug = Regex.Replace(title.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss"), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "projekt"; if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
        var p = Path.Combine(studioRoot, slug);
        for (var i = 2; Directory.Exists(p); i++) p = Path.Combine(studioRoot, $"{slug}-{i}");
        return p;
    }

    /// <summary>Neues Projekt im Studio anlegen (Ordner wird erstellt). <paramref name="folder"/>: vorhandener Projektordner des Studios (z. B. die offene Webseite), sonst ein neuer.</summary>
    public Chat Create(string studio, string title, string studioRoot, string? folder = null)
    {
        var all = Load(studio);
        folder ??= FolderFor(studioRoot, title); Directory.CreateDirectory(folder);
        var c = new Chat(Guid.NewGuid().ToString("N")[..12], studio, title, folder, null, DateTimeOffset.Now, DateTimeOffset.Now, []);
        all.Add(c); Save(studio, all); return c;
    }

    Chat Update(string studio, string id, Func<Chat, Chat> f)
    {
        var all = Load(studio); var i = all.FindIndex(c => c.Id == id);
        if (i < 0) throw new InvalidOperationException("Dieses Projekt gibt es nicht mehr.");
        all[i] = f(all[i]); Save(studio, all); return all[i];
    }

    public Chat Add(string studio, string id, Msg m) => Update(studio, id, c => c with { Messages = [.. c.Messages, m], Last = DateTimeOffset.Now });
    public Chat SetSession(string studio, string id, string session) => Update(studio, id, c => c with { Session = session });
    public Chat SetFull(string studio, string id, bool full) => Update(studio, id, c => c with { Full = full, Safe = !full });
    public Chat Rename(string studio, string id, string title) => Update(studio, id, c => c with { Title = string.IsNullOrWhiteSpace(title) ? c.Title : title.Trim() });

    /// <summary>Projekt aus der Liste löschen (Verlauf weg). Den Ordner entfernt der Aufrufer (in den Papierkorb). Gibt das gelöschte Projekt zurück.</summary>
    public Chat? Delete(string studio, string id)
    {
        var all = Load(studio); var c = all.FirstOrDefault(x => x.Id == id);
        if (c is null) return null;
        all.Remove(c); Save(studio, all); return c;
    }

    /// <summary>Anweisungen für Claude in diesem Studio: Rolle, Fachgebiet, Fachregeln, Lieferformat, Arbeitsweise im Projektordner.</summary>
    public static string System(string studioName, string role, IReadOnlyList<Skill> skills, StudioFormat? format = null, string? workFile = null) =>
        ClaudeWeb.Brief(studioName, role, skills) +
        "\n\nArbeite wie in Claude Code direkt im Projektordner dieses Projekts: Dateien anlegen und ändern ist erlaubt, alles bleibt im Ordner. " +
        (format is not null ? "\nLieferformat, damit Trendo es sofort live zeigt: " + format.Rule : "") +
        (workFile is not null ? $"\nDas offene Studio-Dokument liegt als „{workFile}“ im Projektordner. Bearbeite genau diese Datei (gültiges JSON, Aufbau und Felder beibehalten, nur Inhalte unter \"data\" ändern) – Trendo übernimmt jede Änderung sofort ins Studio." : "") +
        "\nErkläre kurz, was du tust, und fasse am Ende in ein bis zwei Sätzen zusammen, was jetzt da ist." +
        "\nMusst du vor dem Weiterarbeiten etwas wissen, beende deine Antwort mit genau einer Zeile in dieser Form: " + QuestionMark + " <deine Frage> [Antwort 1 | Antwort 2 | Antwort 3]" +
        "\nAngehängte Dateien liegen im Ordner „anhang“ des Projekts; Bilder kannst du ansehen." + ClaudeTools.Rule;

    public const string QuestionMark = "Frage:";

    /// <summary>Rückfrage am Ende einer Antwort („Frage: … [A | B]“) abtrennen – die Antworten werden zu Knöpfen.</summary>
    public static (string Text, string? Question, IReadOnlyList<string> Options) SplitQuestion(string answer)
    {
        var m = Regex.Match(answer, @"(?:^|\n)[ \t>*_]*" + QuestionMark + @"\s*(?<q>[^\n\[]+?)\s*\[(?<o>[^\]\n]+)\]\s*$");
        if (!m.Success) return (answer, null, []);
        var opts = m.Groups["o"].Value.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).Take(5).ToList();
        return opts.Count == 0 ? (answer, null, []) : (answer[..m.Index].TrimEnd(), m.Groups["q"].Value.Trim(), opts);
    }

    /// <summary>Startideen im leeren Chat – passend zum Studio (anklicken und loslegen).</summary>
    public static IReadOnlyList<string> Ideas(string? studio, string? page) => (studio, page) switch
    {
        (_, "text") => ["Schreib eine Einladung zum Sommerfest", "Fasse ein Thema in einer Seite zusammen", "Schreib ein freundliches Bewerbungsanschreiben", "Erstelle eine Checkliste für den Umzug"],
        (_, "tabelle") => ["Haushaltsbuch mit Einnahmen und Ausgaben", "Trainingsplan für 8 Wochen", "Preisliste mit Mehrwertsteuer", "Stundenzettel für einen Monat"],
        ("CORE-06", _) => ["Ein kleines Jump-and-Run", "Ein Quiz mit 10 Fragen", "Ein Snake-Spiel mit Highscore", "Ein Memory mit Tierbildern"],
        ("CORE-07" or "X-03" or "X-04", _) => ["Eine Visitenkarten-Seite", "Eine Seite für ein Café mit Speisekarte", "Ein kleiner Online-Shop", "Eine Landingpage für ein Produkt"],
        ("CORE-08", _) => ["Ein Logo als SVG", "Ein Plakat für ein Konzert", "Eine Geburtstagskarte", "Icons für eine App"],
        ("CORE-05" or "SPEC-09" or "SPEC-04", _) => ["Ein einfacher Holztisch", "Eine Vase", "Ein kleines Haus", "Ein Regal für die Wand"],
        ("SPEC-01", _) => ["Richte das Wohnzimmer gemütlich ein", "Plane eine Küche", "Mach den Grundriss barrierefrei", "Schlage Farben für die Wände vor"],
        _ => ["Erklär mir, was ich hier machen kann", "Mach einen Vorschlag für den Anfang", "Hilf mir, eine Idee zu planen", "Prüfe, was schon da ist"],
    };
}

/// <summary>Lieferformat je Studio (Wunsch 01.11.2026: was Claude baut, erscheint live im Programm): Anweisung an Claude, Hauptdatei für die Vorschau,
/// und welche Dateien Trendo im passenden Programm öffnet. Nur Textformate – Claude arbeitet ohne Befehle und kann keine Word- oder Excel-Dateien schreiben.</summary>
public sealed record StudioFormat(string Rule, string? Main, string[] Opens)
{
    static readonly StudioFormat Web = new("Webseite bzw. App aus index.html mit CSS und JavaScript (alles relativ verlinkt, ohne Server und ohne externe Dienste lauffähig). Startseite ist immer index.html.", "index.html", []);
    static readonly StudioFormat Game = new("Spiel als HTML5: index.html mit JavaScript und Canvas, ohne Server spielbar, Steuerung mit Tastatur und Maus, Grafiken als SVG oder Canvas-Zeichnung. Startseite ist immer index.html.", "index.html", []);
    static readonly StudioFormat Graphic = new("Grafiken und Illustrationen als SVG-Dateien (.svg); bei mehreren Bildern eine index.html, die alle zeigt.", null, []);
    static readonly StudioFormat Model3D = new("3D-Modelle als glTF 2.0 (.gltf, JSON mit eingebetteten Daten als data:-URI) oder als OBJ (.obj mit .mtl); Maße in Metern, Y nach oben.", null, [".gltf", ".obj"]);
    static readonly StudioFormat Text = new("Texte als Markdown (.md): Überschriften mit #, Absätze, Listen. Trendo öffnet sie in Trendo Text.", null, [".md", ".txt"]);
    static readonly StudioFormat Sheet = new("Tabellen als CSV (.csv, Semikolon als Trenner, erste Zeile Spaltennamen). Trendo öffnet sie in Trendo Tabelle.", null, [".csv"]);
    static readonly StudioFormat Program = new("Arbeite im offenen Studio-Dokument (.tdoc, JSON).", null, [".tdoc"]);

    /// <summary>Format für Studio und Seite (die Seite unterscheidet z. B. Text und Tabelle im selben Studio; <paramref name="shell"/>: Seite ist ein Fachprogramm mit .tdoc).</summary>
    public static StudioFormat? For(string? studio, string? page, bool shell = false) => (studio, page) switch
    {
        (_, _) when shell => Program,
        (_, "text") => Text,
        (_, "tabelle") => Sheet,
        ("CORE-07" or "X-03" or "X-04" or "STUDIO-ONLINE-BUSINESS-01", _) => Web,
        ("CORE-06", _) => Game,
        ("CORE-08", _) => Graphic,
        ("CORE-05" or "SPEC-09" or "SPEC-04", _) => Model3D,
        ("SYS-10", _) => Text,
        _ => null,
    };

    /// <summary>Kann die Vorschau diese Datei zeigen (Browser-Ansicht) oder als Text?</summary>
    public static string PreviewKind(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" or ".htm" or ".svg" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => "web",
        ".md" or ".txt" or ".csv" or ".json" or ".js" or ".css" or ".ts" or ".py" or ".cs" or ".xml" or ".obj" or ".mtl" or ".gltf" or ".tdoc" or ".gd" => "text",
        _ => "none",
    };

    /// <summary>Welche geänderte Datei die Vorschau zeigt: zuerst die Hauptdatei des Formats, sonst die erste zeigbare (Webseiten vor Text).</summary>
    public static string? PickPreview(StudioFormat? f, IEnumerable<string> changed)
    {
        var list = changed.ToList();
        if (f?.Main is { } main && list.FirstOrDefault(c => Path.GetFileName(c).Equals(main, StringComparison.OrdinalIgnoreCase)) is { } m) return m;
        return list.FirstOrDefault(c => PreviewKind(c) == "web") ?? list.FirstOrDefault(c => PreviewKind(c) == "text");
    }
}
