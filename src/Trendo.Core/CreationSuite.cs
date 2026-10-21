using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>X-11: Vorhaben-Baukasten – ein Ziel wählen, Trendo legt Ordner, Startdateien und eine Schritt-für-Schritt-Liste an.</summary>
public sealed record Kit(string Id, string Label, string Goal, string[] Folders, string[] Steps, string? DevTemplate = null, string? GameTemplate = null, bool Page = false);
public sealed record Step(int Line, bool Done, string Text);

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

    public static string Create(string parent, string kitId, string name)
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
        foreach (var s in k.Steps) sb.Append($"- [ ] {s}\n");
        File.WriteAllText(Path.Combine(dir, ListFile), sb.ToString(), new UTF8Encoding(false));
        return dir;
    }

    [GeneratedRegex(@"^\s*- \[( |x|X)\] (.*)$")] private static partial Regex StepRx();

    public static IReadOnlyList<Step> Steps(string dir)
    {
        var f = Path.Combine(dir, ListFile); if (!File.Exists(f)) return [];
        var lines = File.ReadAllLines(f); var list = new List<Step>();
        for (var i = 0; i < lines.Length; i++) if (StepRx().Match(lines[i]) is { Success: true } m) list.Add(new(i, m.Groups[1].Value != " ", m.Groups[2].Value));
        return list;
    }

    public static void Toggle(string dir, int line)
    {
        var f = Path.Combine(dir, ListFile); var lines = File.ReadAllLines(f);
        if (line < 0 || line >= lines.Length || StepRx().Match(lines[line]) is not { Success: true } m) return;
        lines[line] = (m.Groups[1].Value == " " ? "- [x] " : "- [ ] ") + m.Groups[2].Value;
        File.WriteAllLines(f, lines);
    }

    public static void AddStep(string dir, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        File.AppendAllText(Path.Combine(dir, ListFile), $"- [ ] {text.Trim().Replace('\n', ' ')}\n");
    }

    public static int Percent(string dir) { var s = Steps(dir); return s.Count == 0 ? 0 : s.Count(x => x.Done) * 100 / s.Count; }
}
