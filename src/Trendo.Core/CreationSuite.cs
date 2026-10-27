using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>X-11: Vorhaben-Baukasten – ein Ziel wählen, Trendo legt Ordner, Startdateien und eine Schritt-für-Schritt-Liste an.</summary>
public sealed record Kit(string Id, string Label, string Goal, string[] Folders, string[] Steps, string? DevTemplate = null, string? GameTemplate = null, bool Page = false);
/// <summary>Ein Schritt. State: 0 offen, 1 in Arbeit, 2 fertig. Due und Milestone für den Zeitplan (Teil 4, F1).</summary>
public sealed record Step(int Line, bool Done, string Text, int State = 0, DateTime? Due = null, bool Milestone = false);

public static partial class CreationSuite
{
    public const string ListFile = "SCHRITTE.md";

    public static readonly IReadOnlyList<Kit> Kits =
    [
        new("video", "Video für YouTube", "Ein fertiges Video mit Vorschaubild", ["Aufnahmen", "Ton", "Bilder", "Fertig"],
            ["Idee in einem Satz aufschreiben", "Drehbuch: Anfang, Mitte, Schluss", "Aufnahmen in den Ordner „Aufnahmen“ legen", "Im Medien-Studio in MP4 umwandeln und kürzen", "Vorschaubild erstellen", "Titel und Beschreibung schreiben", "Hochladen"]),
        new("web", "Eigene Webseite", "Eine Seite, die du online stellen kannst", ["Bilder", "Texte"],
            ["Ziel der Seite festlegen", "Texte schreiben", "Bilder sammeln (freie Assets)", "Im Baukasten zusammenbauen", "Im Browser ansehen und prüfen", "Online stellen"], Page: true),
        new("app", "Kleines Programm", "Ein Programm, das eine Aufgabe für dich erledigt", ["Notizen"],
            ["Aufgabe beschreiben", "Vorlage im Software-Studio öffnen", "Erste Funktion bauen", "Test laufen lassen", "Stand speichern"], DevTemplate: "python"),
        new("game", "Eigenes Spiel", "Ein spielbares Browser-Spiel", ["Grafiken", "Töne"],
            ["Spielidee in einem Satz", "Vorlage spielen", "Eine Regel ändern (game.js)", "Grafik oder Farbe anpassen", "Freunde testen lassen"], GameTemplate: "jump"),
        new("print3d", "3D-Druck", "Ein druckbares Modell als STL", ["Modelle", "Druck"],
            ["Maße festlegen", "Modell im 3D-Studio anlegen", "Vorschau prüfen", "Als STL umwandeln", "Drucken"]),
        new("event", "Feier oder Treffen planen", "Alles für einen gelungenen Termin", ["Einladungen", "Fotos"],
            ["Datum und Ort festlegen", "Gästeliste schreiben", "Einladung als Brief erstellen", "Einkaufsliste", "Ablauf planen", "Fotos sammeln"]),
    ];

    /// <summary>Übliche Dauer je Vorlage in Tagen – daraus werden die Fälligkeiten verteilt.</summary>
    public static int Days(string kitId) => kitId switch { "video" => 14, "web" => 21, "app" => 14, "game" => 10, "print3d" => 7, "event" => 30, _ => 14 };

    public static string Create(string parent, string kitId, string name) => Create(parent, kitId, name, null);

    /// <summary>Mit Startdatum: jeder Schritt bekommt eine Fälligkeit, der letzte ist der Meilenstein „fertig“.</summary>
    public static string Create(string parent, string kitId, string name, DateTime? start)
    {
        var k = Kits.FirstOrDefault(x => x.Id == kitId) ?? throw new ArgumentException("Unbekanntes Vorhaben");
        var dir = Path.Combine(parent, DevStudio.SafeName(name));
        if (Directory.Exists(dir)) throw new IOException($"Das Vorhaben „{Path.GetFileName(dir)}“ gibt es schon.");
        Directory.CreateDirectory(dir);
        foreach (var f in k.Folders) Directory.CreateDirectory(Path.Combine(dir, f));
        if (k.DevTemplate is { } t) DevStudio.Create(dir, t, "Programm");
        if (k.GameTemplate is { } g) GameStudio.CreateBrowserGame(dir, g, "Spiel");
        if (k.Page) new SitePage { Title = name, Blocks = [new("h1", name), new("p", "Worum geht es?")] }.Save(Path.Combine(dir, "seite.json"));
        var sb = new StringBuilder($"# {name}\n\nZiel: {k.Goal}\nVorhaben: {k.Label}\n\n");
        for (var i = 0; i < k.Steps.Length; i++)
            sb.Append(Format(new Step(0, false, k.Steps[i], 0, start is { } st ? st.Date.AddDays(Math.Max(1, (int)Math.Round(Days(kitId) * (i + 1.0) / k.Steps.Length))) : null, start is not null && i == k.Steps.Length - 1))).Append('\n');
        File.WriteAllText(Path.Combine(dir, ListFile), sb.ToString(), new UTF8Encoding(false));
        return dir;
    }

    [GeneratedRegex(@"^\s*- \[( |x|X|~)\] (◆ )?(.*?)(?: @(\d{4}-\d{2}-\d{2}))?\s*$")] private static partial Regex StepRx();

    static Step Parse(int line, Match m)
    {
        var state = m.Groups[1].Value switch { " " => 0, "~" => 1, _ => 2 };
        DateTime? due = m.Groups[4].Success ? DateTime.ParseExact(m.Groups[4].Value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : null;
        return new(line, state == 2, m.Groups[3].Value, state, due, m.Groups[2].Success);
    }

    static string Format(Step s) => $"- [{(s.State switch { 1 => "~", 2 => "x", _ => " " })}] {(s.Milestone ? "◆ " : "")}{s.Text}{(s.Due is { } d ? " @" + d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "")}";

    static void Update(string dir, int line, Func<Step, Step> change)
    {
        var f = Path.Combine(dir, ListFile); var lines = File.ReadAllLines(f);
        if (line < 0 || line >= lines.Length || StepRx().Match(lines[line]) is not { Success: true } m) return;
        lines[line] = Format(change(Parse(line, m)));
        File.WriteAllLines(f, lines);
    }

    /// <summary>Kanban: Schritt nach Offen (0), In Arbeit (1) oder Fertig (2) schieben.</summary>
    public static void SetState(string dir, int line, int state) => Update(dir, line, s => s with { State = Math.Clamp(state, 0, 2), Done = state >= 2 });
    public static void SetDue(string dir, int line, DateTime? due) => Update(dir, line, s => s with { Due = due?.Date });
    public static void SetMilestone(string dir, int line, bool on) => Update(dir, line, s => s with { Milestone = on });

    /// <summary>F2: alle Dateien des Vorhabens (aus allen Studios), neueste zuerst; die Schrittliste selbst zählt nicht.</summary>
    public static List<FileInfo> Files(string dir) =>
        Directory.Exists(dir) ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f => f.Name != ListFile && !f.FullName.Contains(Path.DirectorySeparatorChar + ".", StringComparison.Ordinal) && !f.FullName.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(f => f.LastWriteTimeUtc).Take(200).ToList() : [];

    public static IReadOnlyList<Step> Steps(string dir)
    {
        var f = Path.Combine(dir, ListFile); if (!File.Exists(f)) return [];
        var lines = File.ReadAllLines(f); var list = new List<Step>();
        for (var i = 0; i < lines.Length; i++) if (StepRx().Match(lines[i]) is { Success: true } m) list.Add(Parse(i, m));
        return list;
    }

    public static void Toggle(string dir, int line) => Update(dir, line, s => s with { State = s.Done ? 0 : 2, Done = !s.Done });

    public static void AddStep(string dir, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        File.AppendAllText(Path.Combine(dir, ListFile), $"- [ ] {text.Trim().Replace('\n', ' ')}\n");
    }

    public static int Percent(string dir) { var s = Steps(dir); return s.Count == 0 ? 0 : s.Count(x => x.Done) * 100 / s.Count; }
}
