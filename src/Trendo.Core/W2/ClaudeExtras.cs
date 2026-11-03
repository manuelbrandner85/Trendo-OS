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
        var tmp = FileOf(chatId) + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(items)); SafeFile.Move(tmp, FileOf(chatId)); // Virenscanner/Indexdienst kann die Datei kurz halten
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

/// <summary>Wiederholungen automatisieren (Wunsch 02.11.2026): ein Auftrag an Claude wird zum Ablauf, der sich mit einem Klick in jedem Projekt des Studios wiederholen lässt.
/// Abläufe sind Vorlagen in der vorhandenen Prompt-Bibliothek (Kategorie je Studio) – dort lassen sie sich auch bearbeiten und versionieren.</summary>
public static class ClaudeFlows
{
    public static string Category(string studioName) => "Claude-Abläufe · " + studioName;
    public static IReadOnlyList<PromptTemplate> For(PromptLibrary lib, string studioName) => lib.All().Where(p => p.Category == Category(studioName)).ToList();

    /// <summary>Auftrag als Ablauf merken (gleicher Text nur einmal). Gibt die Vorlage zurück.</summary>
    public static PromptTemplate Save(PromptLibrary lib, string studioName, string text)
    {
        var t = text.Trim();
        return For(lib, studioName).FirstOrDefault(p => p.Text.Equals(t, StringComparison.OrdinalIgnoreCase)) ?? lib.Save(null, ClaudeChats.TitleOf(t), Category(studioName), t, "aus dem Claude-Chat");
    }

    /// <summary>Wie oft dieser Auftrag in den Projekten des Studios schon kam – ab 2 schlägt Trendo vor, ihn als Ablauf zu merken.</summary>
    public static int Repeats(IEnumerable<ClaudeChats.Chat> chats, string text) =>
        chats.SelectMany(c => c.Messages).Count(m => m.Role == "user" && m.Text.Trim().Equals(text.Trim(), StringComparison.OrdinalIgnoreCase));
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
        "10 heißt: professionell, fehlerfrei, nichts mehr zu verbessern. Sei ehrlich – keine Gefälligkeitsnoten." +
        "\nIst eine Schwäche ein allgemeines Muster, das künftig in allen Projekten dieses Studios vermieden werden sollte, schreib am Ende eine Zeile „Lernregel: <Regel>“.";

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
            return "Trendo hat dein Ergebnis echt gestartet und geprüft und das gefunden – bitte behebe es (ist ein Fehler ein allgemeines Muster, ergänze eine Zeile „Lernregel: …“, damit er künftig nicht mehr passiert):\n" + string.Join("\n", errors.Take(25).Select(e => "- " + e)) +
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

/// <summary>Claude bedient das offene Programm (Wunsch 02.11.2026): Trendo beschreibt Claude die Seite (Felder, Auswahllisten, Knöpfe), Claude antwortet mit
/// Zeilen „[[trendo:feld | name | wert]]“, „[[trendo:wahl | name | eintrag]]“, „[[trendo:klick | name]]“. Trendo zeigt das als Plan und führt es erst nach „Ausführen“ aus;
/// Knöpfe mit Wirkung nach außen (senden, löschen, kaufen …) sind im Plan deutlich markiert.</summary>
public static partial class ClaudeActions
{
    public sealed record Step(string Kind, string Target, string? Value);
    public sealed record Field(string Name, string Label, string Value);
    public sealed record Choice(string Name, string Label, string? Selected, IReadOnlyList<string> Options);
    public sealed record Button(string Name, string Label);
    /// <summary>Was auf der offenen Seite zu sehen ist – Grundlage für „Claude kennt jedes Programm“.</summary>
    public sealed record Page(string Program, string Title, string? Selected, IReadOnlyList<string> Texts, IReadOnlyList<Field> Fields, IReadOnlyList<Choice> Choices, IReadOnlyList<Button> Buttons);

    public const int MaxSteps = 12;

    [GeneratedRegex(@"senden|verschick|lösch|entfern|leeren|papierkorb|kauf|bezahl|bestell|zahlen|überweis|drucken|veröffentlich|hochlad|teilen|posten|abmeld|deinstall|formatier|überschreib|zurücksetz|endgültig|ausführen|starten|aufnahme", RegexOptions.IgnoreCase)]
    private static partial Regex RiskyRx();
    [GeneratedRegex(@"pass|kenn?wort|token|secret|geheim|schlüssel|api-?key|pin\b|iban|kreditkarte|cvc", RegexOptions.IgnoreCase)]
    private static partial Regex SecretRx();

    /// <summary>Wirkt dieser Knopf nach außen oder unumkehrbar? Dann markiert der Plan ihn besonders.</summary>
    public static bool Risky(string label, string name) => RiskyRx().IsMatch(label + " " + name);
    /// <summary>Feld mit Geheimnis (Passwort, Schlüssel, Bankdaten) – geht nie an Claude und wird nie von Claude ausgefüllt.</summary>
    public static bool Secret(string label, string name) => SecretRx().IsMatch(label + " " + name);

    [GeneratedRegex(@"^[ \t]*\[\[trendo:(?<k>klick|feld|wahl)\s*\|\s*(?<t>[^|\]\n]+?)\s*(?:\|\s*(?<v>[^\]\n]*?)\s*)?\]\][ \t\r]*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex Marker();

    /// <summary>Bedien-Schritte aus Claudes Antwort – nur Ziele, die es auf der Seite wirklich gibt; Geheimnisfelder nie.</summary>
    public static IReadOnlyList<Step> Parse(string answer, Page page)
    {
        var list = new List<Step>();
        foreach (Match m in Marker().Matches(answer))
        {
            var k = m.Groups["k"].Value.ToLowerInvariant(); var t = m.Groups["t"].Value.Trim(); var v = m.Groups["v"].Success ? m.Groups["v"].Value : null;
            var ok = k switch
            {
                "klick" => page.Buttons.Any(b => b.Name == t),
                "feld" => v is not null && page.Fields.FirstOrDefault(f => f.Name == t) is { } f && !Secret(f.Label, f.Name),
                "wahl" => v is not null && page.Choices.FirstOrDefault(c => c.Name == t) is { } c && c.Options.Contains(v.Trim(), StringComparer.OrdinalIgnoreCase),
                _ => false,
            };
            if (ok) list.Add(new(k, t, k == "wahl" ? v!.Trim() : v));
            if (list.Count == MaxSteps) break;
        }
        return list;
    }

    /// <summary>Verständliche Beschreibung eines Schritts für den Plan.</summary>
    public static string Describe(Step s, Page page) => s.Kind switch
    {
        "klick" => $"„{page.Buttons.First(b => b.Name == s.Target).Label}“ klicken",
        "feld" => $"„{page.Fields.First(f => f.Name == s.Target).Label}“ = „{s.Value}“",
        _ => $"„{page.Choices.First(c => c.Name == s.Target).Label}“ auf „{s.Value}“ stellen",
    };

    public static bool StepRisky(Step s, Page page) => s.Kind == "klick" && page.Buttons.First(b => b.Name == s.Target) is var b && Risky(b.Label, b.Name);

    /// <summary>Beschreibung der Seite für Claude (gekürzt) und wie Claude sie bedienen kann.</summary>
    public static string Prompt(Page p)
    {
        static string Cut(string s, int n) { s = s.Replace('\n', ' ').Trim(); return s.Length > n ? s[..n] + "…" : s; }
        var sb = new System.Text.StringBuilder();
        sb.Append($"\n\n[Offenes Programm: {p.Program}" + (p.Title.Length > 0 ? $" – {p.Title}" : "") + "]");
        if (p.Selected is { Length: > 0 } sel) sb.Append($"\nMarkiert: „{Cut(sel, 600)}“");
        if (p.Texts.Count > 0) sb.Append("\nZu sehen: " + Cut(string.Join(" · ", p.Texts), 900));
        if (p.Fields.Count > 0) sb.Append("\nFelder: " + string.Join("; ", p.Fields.Take(30).Select(f => $"{f.Name} „{Cut(f.Label, 40)}“" + (f.Value.Length > 0 ? $" = „{Cut(f.Value, 80)}“" : ""))));
        if (p.Choices.Count > 0) sb.Append("\nAuswahl: " + string.Join("; ", p.Choices.Take(15).Select(c => $"{c.Name} „{Cut(c.Label, 40)}“ = „{c.Selected}“ [{string.Join(" | ", c.Options.Take(12))}]")));
        if (p.Buttons.Count > 0) sb.Append("\nKnöpfe: " + string.Join("; ", p.Buttons.Take(60).Select(b => $"{b.Name} „{Cut(b.Label, 30)}“")));
        sb.Append("\nWillst du im Programm selbst etwas tun, schreib je Schritt eine eigene Zeile: [[trendo:feld | NAME | WERT]], [[trendo:wahl | NAME | EINTRAG]] oder [[trendo:klick | NAME]] (Namen aus der Liste oben). " +
                  "Trendo zeigt dem Nutzer die Schritte und führt sie erst nach seiner Freigabe aus. Passwörter und Bankdaten füllst du nie aus.");
        return sb.ToString();
    }
}

/// <summary>Deine Unterlagen nutzen (Wunsch 02.11.2026): Einträge aus „Wissen“ gehen nur an Claude, wenn du sie für ein Projekt freigibst. Je Projekt gemerkt.</summary>
public sealed class ClaudeDocs(string dataRoot)
{
    string FileOf(string chatId) => Path.Combine(dataRoot, "claude-chats", "unterlagen", Regex.Replace(chatId, "[^A-Za-z0-9-]", "-") + ".json");
    sealed record State(List<string> Shared, List<string> Declined);
    State Load(string chatId)
    {
        try { return File.Exists(FileOf(chatId)) ? JsonSerializer.Deserialize<State>(File.ReadAllText(FileOf(chatId))) ?? new([], []) : new([], []); }
        catch (JsonException) { return new([], []); }
    }
    void Save(string chatId, State s) { Directory.CreateDirectory(Path.GetDirectoryName(FileOf(chatId))!); File.WriteAllText(FileOf(chatId), JsonSerializer.Serialize(s)); }
    public IReadOnlyList<string> Shared(string chatId) => Load(chatId).Shared;
    public bool Declined(string chatId, string id) => Load(chatId).Declined.Contains(id);
    public void Share(string chatId, string id) { var s = Load(chatId); if (!s.Shared.Contains(id)) s.Shared.Add(id); s.Declined.Remove(id); Save(chatId, s); }
    public void Unshare(string chatId, string id) { var s = Load(chatId); s.Shared.Remove(id); Save(chatId, s); }
    public void Decline(string chatId, string id) { var s = Load(chatId); if (!s.Declined.Contains(id)) s.Declined.Add(id); Save(chatId, s); }
    public void Delete(string chatId) { try { if (File.Exists(FileOf(chatId))) File.Delete(FileOf(chatId)); } catch (IOException) { } }

    /// <summary>Freigegebene Unterlagen als Dateien in den Projektordner (.trendo/unterlagen) legen und den Hinweis für Claude liefern.</summary>
    public static string Write(string folder, IEnumerable<KnowledgeItem> items)
    {
        var dir = Path.Combine(folder, ".trendo", "unterlagen");
        if (Directory.Exists(dir)) foreach (var f in Directory.EnumerateFiles(dir, "*.md")) File.Delete(f); // nur, was gerade freigegeben ist
        var names = new List<string>();
        foreach (var it in items)
        {
            Directory.CreateDirectory(dir);
            var name = Regex.Replace(it.Title, @"[\\/:*?""<>|]", "-").Trim(); if (name.Length == 0) name = it.Id; if (name.Length > 60) name = name[..60];
            File.WriteAllText(Path.Combine(dir, name + ".md"), "# " + it.Title + "\n\n" + it.Text);
            names.Add(name + ".md");
        }
        return names.Count == 0 ? "" : "\nDer Nutzer hat dir für dieses Projekt Unterlagen freigegeben (Ordner .trendo/unterlagen): " + string.Join(", ", names) +
            ". Nutze sie, wo sie passen, und gib nichts davon nach außen weiter.";
    }
}

/// <summary>Große Aufträge in Etappen (Wunsch 02.11.2026): ist das Claude-Abo mitten in der Arbeit aufgebraucht, merkt sich Trendo das Projekt;
/// sobald Claude wieder frei ist, macht Claude dort weiter – und prüft, was die Ersatz-KI in der Zwischenzeit gemacht hat.</summary>
public sealed class ClaudeStages(string dataRoot)
{
    public sealed record Stage(string ChatId, string Studio, string Request, DateTimeOffset Since);
    public const string Resume = "Dein Claude-Abo ist wieder frei. Mach mit dem Auftrag weiter, wo du aufgehört hast. Prüfe zuerst, was die Ersatz-KI in der Zwischenzeit geändert hat, und verbessere es, wo nötig. Ursprünglicher Auftrag: ";
    string FileOf => Path.Combine(dataRoot, "claude-chats", "etappen.json");
    public IReadOnlyList<Stage> Pending()
    {
        try { return File.Exists(FileOf) ? JsonSerializer.Deserialize<List<Stage>>(File.ReadAllText(FileOf)) ?? [] : []; } catch (JsonException) { return []; }
    }
    void Save(List<Stage> l) { Directory.CreateDirectory(Path.GetDirectoryName(FileOf)!); File.WriteAllText(FileOf, JsonSerializer.Serialize(l)); }
    /// <summary>Projekt merken (je Projekt nur einmal – der erste unterbrochene Auftrag zählt).</summary>
    public void Add(string chatId, string studio, string request) { var l = Pending().ToList(); if (l.Any(s => s.ChatId == chatId)) return; l.Add(new(chatId, studio, request.Length > 400 ? request[..400] + " …" : request, DateTimeOffset.Now)); Save(l); }
    public void Done(string chatId) { var l = Pending().Where(s => s.ChatId != chatId).ToList(); Save(l); }
}

/// <summary>Veröffentlichen mit einem Klick (Wunsch 02.11.2026): das Projekt fertig verpacken – als ZIP im Ordner „Veröffentlicht“, ohne Hilfsdateien von Trendo und Claude.
/// Hochgeladen wird dabei nichts.</summary>
public static class ClaudePublish
{
    static readonly string[] Skip = [".trendo", ".claude", ".git", "node_modules", "anhang", "__pycache__", "bin", "obj"];

    public static string Pack(string folder, string outDir, string title)
    {
        Directory.CreateDirectory(outDir);
        var name = Regex.Replace(title, @"[\\/:*?""<>|]", "-").Trim(); if (name.Length == 0) name = "Projekt"; if (name.Length > 50) name = name[..50].Trim();
        var zip = Path.Combine(outDir, $"{name} {DateTime.Now:yyyy-MM-dd HH-mm}.zip");
        for (var i = 2; File.Exists(zip); i++) zip = Path.Combine(outDir, $"{name} {DateTime.Now:yyyy-MM-dd HH-mm} ({i}).zip");
        using var a = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create);
        var root = Path.GetFullPath(folder);
        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, f);
            if (rel.Split(Path.DirectorySeparatorChar).Any(p => Skip.Contains(p, StringComparer.OrdinalIgnoreCase))) continue;
            System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(a, f, rel.Replace(Path.DirectorySeparatorChar, '/'));
        }
        return zip;
    }
}
