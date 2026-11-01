using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Claude-Projekte je Studio (Wunsch 01.11.2026: „wie in Claude“ – Projekte erscheinen im Studio, lassen sich immer wieder fortsetzen, bis man sie löscht).
/// Ein Projekt ist ein Gespräch mit Claude Code samt eigenem Arbeitsordner im Studio: Verlauf (Nachrichten und Arbeitsschritte) liegt im Datenordner der Person,
/// die Sitzung von Claude Code wird mit <c>--resume</c> fortgesetzt. Die erste Nachricht in einem Studio legt das Projekt automatisch an.</summary>
public sealed partial class ClaudeChats(string dataRoot)
{
    public sealed record Step(string Text, bool Changes, bool Error = false);
    public sealed record Msg(string Role, string Text, List<Step> Steps, DateTimeOffset At, List<string>? Changed = null);
    public sealed record Chat(string Id, string Studio, string Title, string Folder, string? Session, DateTimeOffset Created, DateTimeOffset Last, List<Msg> Messages);

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

    /// <summary>Neues Projekt im Studio anlegen (Ordner wird erstellt).</summary>
    public Chat Create(string studio, string title, string studioRoot)
    {
        var all = Load(studio);
        var folder = FolderFor(studioRoot, title); Directory.CreateDirectory(folder);
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
    public Chat Rename(string studio, string id, string title) => Update(studio, id, c => c with { Title = string.IsNullOrWhiteSpace(title) ? c.Title : title.Trim() });

    /// <summary>Projekt aus der Liste löschen (Verlauf weg). Den Ordner entfernt der Aufrufer (in den Papierkorb). Gibt das gelöschte Projekt zurück.</summary>
    public Chat? Delete(string studio, string id)
    {
        var all = Load(studio); var c = all.FirstOrDefault(x => x.Id == id);
        if (c is null) return null;
        all.Remove(c); Save(studio, all); return c;
    }

    /// <summary>Anweisungen für Claude in diesem Studio: Rolle, Fachgebiet, Fachregeln, Arbeitsweise im Projektordner.</summary>
    public static string System(string studioName, string role, IReadOnlyList<Skill> skills) =>
        ClaudeWeb.Brief(studioName, role, skills) +
        "\n\nArbeite wie in Claude Code direkt im Projektordner dieses Projekts: Dateien anlegen und ändern ist erlaubt, alles bleibt im Ordner. " +
        "Erkläre kurz, was du tust, und fasse am Ende in ein bis zwei Sätzen zusammen, was jetzt da ist.";
}
