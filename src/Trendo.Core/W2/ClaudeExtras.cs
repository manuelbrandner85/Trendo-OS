using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Projekt-Gedächtnis (Wunsch 02.11.2026): was sich Claude für ein Projekt dauerhaft merken soll – Wünsche, Stil, Entscheidungen. Liegt im Datenordner der Person,
/// geht bei jeder Nachricht mit (auch an die Ersatz-KI) und lässt sich im Chat ansehen, ergänzen und löschen. Claude schreibt neue Punkte mit einer Zeile „Merken: …“.</summary>
public sealed partial class ClaudeMemory(string dataRoot)
{
    public const string Mark = "Merken:";
    public const int Max = 40;
    string FileOf(string chatId) => Path.Combine(dataRoot, "claude-chats", "gedaechtnis", Regex.Replace(chatId, "[^A-Za-z0-9-]", "-") + ".json");

    public IReadOnlyList<string> Items(string chatId)
    {
        try { return File.Exists(FileOf(chatId)) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FileOf(chatId))) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    void Save(string chatId, List<string> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf(chatId))!);
        var tmp = FileOf(chatId) + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(items)); File.Move(tmp, FileOf(chatId), true);
    }

    /// <summary>Punkt merken (doppelte und leere übergehen; höchstens <see cref="Max"/> – der älteste fällt dann weg). Gibt true zurück, wenn er neu ist.</summary>
    public bool Add(string chatId, string text)
    {
        var t = Regex.Replace(text.Trim(), @"\s+", " ");
        if (t.Length == 0) return false;
        if (t.Length > 300) t = t[..299] + "…";
        var items = Items(chatId).ToList();
        if (items.Any(x => x.Equals(t, StringComparison.OrdinalIgnoreCase))) return false;
        items.Add(t); while (items.Count > Max) items.RemoveAt(0);
        Save(chatId, items); return true;
    }

    public void Remove(string chatId, int index) { var items = Items(chatId).ToList(); if (index >= 0 && index < items.Count) { items.RemoveAt(index); Save(chatId, items); } }
    public void Delete(string chatId) { try { if (File.Exists(FileOf(chatId))) File.Delete(FileOf(chatId)); } catch (IOException) { } }

    /// <summary>Teil der Anweisungen: was gemerkt ist, und wie Claude Neues merkt.</summary>
    public string Prompt(string chatId)
    {
        var items = Items(chatId);
        return "\nProjekt-Gedächtnis: Soll etwas für dieses Projekt dauerhaft gelten (Wunsch, Stil, Entscheidung, wichtige Angabe), schreibe dafür eine eigene Zeile „" + Mark + " <kurz und klar>“." +
            (items.Count > 0 ? "\nDas gilt in diesem Projekt (vom Nutzer bestätigt oder früher gemerkt):\n" + string.Join("\n", items.Select(i => "- " + i)) : "");
    }

    [GeneratedRegex(@"^[ \t>*_-]*Merken:\s*(?<t>.+?)\s*$", RegexOptions.Multiline)] private static partial Regex MarkLine();

    /// <summary>„Merken: …“-Zeilen aus einer Antwort.</summary>
    public static IReadOnlyList<string> Extract(string answer) => MarkLine().Matches(answer).Select(m => m.Groups["t"].Value.Trim('*', '_', ' ')).Where(t => t.Length > 0).ToList();
    /// <summary>Antwort ohne „Merken: …“-Zeilen (die zeigt Trendo als Hinweis „gemerkt“).</summary>
    public static string Strip(string answer) => Regex.Replace(MarkLine().Replace(answer, ""), @"\n{3,}", "\n\n").Trim();
}

/// <summary>Zeitreise (Wunsch 02.11.2026): vor jeder Antwort mit Änderungen sichert Trendo den Projektordner als Versionspunkt. Man sieht alle Punkte mit Zeit und Auftrag
/// und springt zu jedem zurück – vorher sichert Trendo den aktuellen Stand, die Zeitreise ist also selbst wieder rückgängig zu machen.</summary>
public sealed class ClaudeTimeline(string dataRoot)
{
    public const int Max = 30;
    public sealed record Point(string Id, DateTimeOffset At, string Label, int Changes);
    string Dir(string chatId) => Path.Combine(dataRoot, "claude-chats", "sicherung", chatId);
    static string Meta(string store) => Path.Combine(store + ".json");

    /// <summary>Neuer Speicherort für einen Punkt (Sicherung entsteht mit <see cref="FolderSnapshot.Take"/>).</summary>
    public string NewStore(string chatId) => Path.Combine(Dir(chatId), DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff"));

    /// <summary>Punkt behalten (mit Beschriftung) – oder wegwerfen, wenn sich nichts geändert hat. Älteste Punkte über <see cref="Max"/> fallen weg.</summary>
    public void Keep(string chatId, string store, string label, int changes)
    {
        if (changes == 0) { Drop(store); return; }
        var t = Regex.Replace(label.Trim(), @"\s+", " "); if (t.Length > 80) t = t[..79] + "…";
        File.WriteAllText(Meta(store), JsonSerializer.Serialize(new Point(Path.GetFileName(store), DateTimeOffset.Now, t, changes)));
        foreach (var old in Points(chatId).Skip(Max)) Drop(Path.Combine(Dir(chatId), old.Id));
    }

    static void Drop(string store)
    {
        try { if (Directory.Exists(store)) Directory.Delete(store, true); if (File.Exists(Meta(store))) File.Delete(Meta(store)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Alle Punkte, neuester zuerst.</summary>
    public IReadOnlyList<Point> Points(string chatId)
    {
        if (!Directory.Exists(Dir(chatId))) return [];
        var list = new List<Point>();
        foreach (var f in Directory.EnumerateFiles(Dir(chatId), "*.json"))
            try { if (JsonSerializer.Deserialize<Point>(File.ReadAllText(f)) is { } p && Directory.Exists(Path.Combine(Dir(chatId), p.Id))) list.Add(p); } catch (JsonException) { }
        return list.OrderByDescending(p => p.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>Projektordner auf den Stand vor diesem Punkt bringen. Der aktuelle Stand wird vorher selbst ein Punkt („Vor der Zeitreise“).</summary>
    public void Travel(string chatId, string pointId, string folder)
    {
        var store = Path.Combine(Dir(chatId), pointId);
        if (!Directory.Exists(store)) throw new InvalidOperationException("Diesen Versionspunkt gibt es nicht mehr.");
        var target = FolderSnapshot.Open(folder, store);
        var now = NewStore(chatId); FolderSnapshot.Take(folder, now);
        Keep(chatId, now, "Vor der Zeitreise", target.Changes().Count);
        target.Restore();
    }
}

/// <summary>Gutachter (Wunsch 02.11.2026): zweite Meinung zum Ergebnis mit Note 1–10, Stärken und Schwächen. Läuft in einer eigenen Claude-Sitzung, die nichts ändern darf.</summary>
public static partial class ClaudeReview
{
    public const string Rule = "Du bist ein strenger, fairer Gutachter. Sieh dir das Ergebnis im Projektordner genau an (Dateien lesen, Bilder ansehen) und ändere NICHTS. " +
        "Antworte auf Deutsch in diesem Aufbau:\nNote: <1 bis 10>/10\nStärken:\n- …\nSchwächen:\n- …\nVerbesserungen:\n- … (konkret, umsetzbar)\n" +
        "10 heißt: professionell, fehlerfrei, nichts mehr zu verbessern. Sei ehrlich – keine Gefälligkeitsnoten.";

    public static string Prompt(string studioName, string request, IEnumerable<string> files) =>
        $"Begutachte das Ergebnis im Studio „{studioName}“. Auftrag des Nutzers war: „{request.Trim()}“." +
        (files.Any() ? "\nZuletzt geändert: " + string.Join(", ", files.Take(20)) : "") + "\nBei Webseiten oder Spielen liegt ein Bildschirmfoto unter .trendo/ansicht.png, wenn es eins gibt.";

    [GeneratedRegex(@"Note\s*:?\s*\**\s*(?<n>10|[1-9])(?:[.,]\d)?\s*(?:/\s*10|von\s*10)?", RegexOptions.IgnoreCase)] private static partial Regex GradeRx();

    /// <summary>Note aus dem Gutachten (1–10) oder null.</summary>
    public static int? Grade(string text) => GradeRx().Match(text) is { Success: true } m ? int.Parse(m.Groups["n"].Value) : null;

    /// <summary>Nachricht an Claude: die Schwächen und Vorschläge des Gutachtens umsetzen.</summary>
    public static string Improve(string review) => "Ein Gutachter hat dein Ergebnis bewertet. Setze seine Verbesserungen um und behebe die Schwächen:\n" + review.Trim();
}

/// <summary>„Echt testen, bis es läuft“ und „Claude schaut hin“ (Wunsch 02.11.2026): Befunde aus dem echten Start (Konsolenfehler, Syntaxfehler) und das Bildschirmfoto
/// als Nachricht an Claude – höchstens <see cref="Rounds"/> Runden, damit es nicht endlos läuft.</summary>
public static class ClaudeProbe
{
    public const int Rounds = 3;
    public const string Shot = ".trendo/ansicht.png";
    public sealed record Result(IReadOnlyList<string> Errors, string? Screenshot);

    /// <summary>Nachricht für die nächste Runde – oder null, wenn nichts zu tun ist. <paramref name="look"/>: Claude soll sich das Bildschirmfoto ansehen.</summary>
    public static string? Feedback(IReadOnlyList<string> agentFindings, Result probe, bool look)
    {
        var errors = agentFindings.Concat(probe.Errors).Distinct().ToList();
        if (errors.Count > 0)
            return "Trendo hat dein Ergebnis echt gestartet und geprüft und das gefunden – bitte behebe es:\n" + string.Join("\n", errors.Take(25).Select(e => "- " + e)) +
                (probe.Screenshot is not null ? $"\nSo sieht es gerade aus: {Shot} (ansehen)." : "");
        if (look && probe.Screenshot is not null)
            return $"Trendo hat dein Ergebnis gestartet – ohne Fehler. Sieh dir das Bildschirmfoto {Shot} an, so wie ein Nutzer es sieht. Stimmt alles (Aufbau, Lesbarkeit, Abstände, nichts abgeschnitten, passt zum Auftrag)? " +
                "Wenn ja, antworte nur „Passt.“ und ändere nichts. Wenn nicht, verbessere es.";
        return null;
    }

    /// <summary>JSON-Dateien prüfen (ohne fremdes Programm).</summary>
    public static IEnumerable<string> JsonErrors(string folder, IEnumerable<string> files)
    {
        foreach (var rel in files.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)))
        {
            string? err = null;
            try { using var _ = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, rel.Replace('/', Path.DirectorySeparatorChar)))); }
            catch (JsonException e) { err = $"{rel}: ungültiges JSON – {e.Message}"; }
            catch (IOException) { }
            if (err is not null) yield return err;
        }
    }
}
