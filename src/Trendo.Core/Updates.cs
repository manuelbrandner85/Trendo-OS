using System.Text.Json;

namespace Trendo.Core;

/// <summary>Wunsch 27.10.2026 (U1–U6): Updates wie bei Windows. Hier die Regeln ohne Netz: wann installiert wird (U3),
/// Startwächter für die automatische Rückkehr zur alten Version (U5) und „Was ist neu?“ (U4).</summary>
public static class UpdateSchedule
{
    public static readonly (string Id, string Label)[] Modes = [("sofort", "Sofort, sobald es geladen ist – mit 30 Sekunden Hinweis, alles wird vorher gesichert"), ("start", "Beim nächsten Start von Trendo"), ("nacht", "Nachts (wenn du nicht am PC bist)")];

    /// <summary>Jetzt installieren? „sofort“: ja. „nacht“: nur in der gewählten Stunde (bis 2 Stunden danach). „start“: nie während Trendo läuft.</summary>
    public static bool ApplyNow(string mode, int hour, DateTime nowLocal) => mode switch
    {
        "sofort" => true,
        "nacht" => nowLocal.Hour >= hour && nowLocal.Hour < hour + 2,
        _ => false,
    };

    /// <summary>Wie oft wird nach Updates gesucht.</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(30);

    /// <summary>Countdown vor dem automatischen Neustart (Sekunden).</summary>
    public const int Countdown = 30;
}

/// <summary>U5: Startwächter. Jeder Start einer Version zählt; läuft sie 30 Sekunden stabil, gilt sie als gut. Scheitert eine neue Version
/// dreimal hintereinander, kehrt Trendo zur letzten guten Version zurück (Sicherung vom Update).</summary>
public sealed class BootGuard(string dataRoot)
{
    sealed record State(string Version, int Attempts, bool Ok, string? LastGood);
    string FileOf => Path.Combine(dataRoot, "updates", "start.json");
    public const int MaxAttempts = 3;

    State? Load() { try { return File.Exists(FileOf) ? JsonSerializer.Deserialize<State>(File.ReadAllText(FileOf)) : null; } catch (Exception e) when (e is IOException or JsonException) { return null; } }
    void Save(State s) { Directory.CreateDirectory(Path.GetDirectoryName(FileOf)!); File.WriteAllText(FileOf, JsonSerializer.Serialize(s)); }

    /// <summary>Start beginnt. Liefert die Version, zu der zurückgekehrt werden soll – oder null, wenn alles in Ordnung ist.</summary>
    public string? Begin(string version)
    {
        var s = Load();
        if (s is null || s.Version != version) { Save(new State(version, 1, false, s is { Ok: true } ? s.Version : s?.LastGood)); return null; }
        if (s.Ok) return null;
        var next = s with { Attempts = s.Attempts + 1 };
        Save(next);
        return next.Attempts >= MaxAttempts && next.LastGood is { } good && good != version ? good : null;
    }

    public void MarkOk(string version) { var s = Load(); if (s is not null && s.Version == version && !s.Ok) Save(s with { Ok = true, Attempts = 0 }); }
    public string? LastGood => Load() is { } s ? (s.Ok ? s.Version : s.LastGood) : null;
    public int Attempts => Load()?.Attempts ?? 0;

    /// <summary>Ordner für die Sicherung der laufenden Version vor einem Update.</summary>
    public string BackupDir => Path.Combine(dataRoot, "updates", "vorherige-version");
}

/// <summary>U4: Neuerungen je Version (Markdown mit Abschnitten „## 0.9.12“). Nach einem Update zeigt Trendo einmal, was neu ist.</summary>
public static class ReleaseNotes
{
    public static string NotesFile(string dataRoot, string version) => Path.Combine(dataRoot, "updates", $"neu-{version}.md");

    public static void Save(string dataRoot, string version, string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return;
        Directory.CreateDirectory(Path.Combine(dataRoot, "updates"));
        File.WriteAllText(NotesFile(dataRoot, version), markdown);
    }

    public static string? For(string dataRoot, string version) => File.Exists(NotesFile(dataRoot, version)) ? File.ReadAllText(NotesFile(dataRoot, version)) : null;

    /// <summary>Punkte einer Version aus einer Notizen-Datei („## 0.9.12“ bis zur nächsten Überschrift), ohne Markdown-Zeichen.</summary>
    public static List<string> Points(string markdown, string? version = null)
    {
        var lines = markdown.Replace("\r", "").Split('\n');
        var start = version is null ? 0 : Array.FindIndex(lines, l => l.TrimStart('#', ' ').StartsWith(version, StringComparison.Ordinal));
        if (start < 0) start = 0;
        var list = new List<string>();
        for (var i = start + (version is null ? 0 : 1); i < lines.Length; i++)
        {
            var l = lines[i].Trim();
            if (version is not null && l.StartsWith("## ", StringComparison.Ordinal)) break;
            if (l.StartsWith("- ") || l.StartsWith("* ")) list.Add(l[2..].Replace("**", "").Trim());
        }
        return list;
    }

    /// <summary>Muss „Was ist neu?“ gezeigt werden? Ja, wenn die Version neu ist und es Notizen gibt.</summary>
    public static bool ShowFor(string? lastSeen, string current, string? notes) => !string.IsNullOrEmpty(notes) && !string.Equals(lastSeen, current, StringComparison.Ordinal) && !string.IsNullOrEmpty(lastSeen);
}

/// <summary>Eine Folie der Vorstellung „Das ist neu“: Titel, Text, Unterpunkte und ein Symbol passend zum Thema.</summary>
public sealed record NewsSlide(string Title, string Text, IReadOnlyList<string> Points, string Icon)
{
    /// <summary>Anzeigedauer: genug Zeit zum Lesen (4 s + Text), höchstens 12 s.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(Math.Clamp(4 + (Text.Length + Points.Sum(p => p.Length)) / 28.0, 5, 12));
}

/// <summary>Vorstellung nach einem Update (Wunsch 01.11.2026): jede Neuerung als eigene Folie.</summary>
public static class NewsShow
{
    /// <summary>Folien aus den Notizen einer Version: ein Punkt der obersten Ebene = eine Folie; „**Titel:** Text“ wird zu Titel und Text,
    /// eingerückte Punkte werden Unterpunkte. Ohne fetten Titel gilt der Teil bis zum ersten Doppelpunkt (sonst die ersten Wörter) als Titel.</summary>
    public static List<NewsSlide> Slides(string markdown, string? version = null)
    {
        var lines = markdown.Replace("\r", "").Split('\n');
        var start = version is null ? -1 : Array.FindIndex(lines, l => l.StartsWith("## ") && l.TrimStart('#', ' ').StartsWith(version, StringComparison.Ordinal));
        var res = new List<NewsSlide>(); string? title = null, text = null; List<string> pts = [];
        void Flush() { if (title is not null) res.Add(new(title, text ?? "", pts, Icon(title + " " + text))); title = null; text = null; pts = []; }
        for (var i = start + 1; i < lines.Length; i++)
        {
            var raw = lines[i]; var l = raw.Trim();
            if (l.StartsWith("## ", StringComparison.Ordinal)) { if (version is not null || res.Count > 0 || title is not null) break; continue; }
            if (!(l.StartsWith("- ") || l.StartsWith("* "))) continue;
            var body = l[2..].Trim();
            if (raw.Length - raw.TrimStart().Length >= 2 && title is not null) { pts.Add(body.Replace("**", "")); continue; }
            Flush();
            if (body.StartsWith("**") && body.IndexOf("**", 2, StringComparison.Ordinal) is var e and > 2)
            {
                title = body[2..e].Trim().TrimEnd(':'); text = body[(e + 2)..].Replace("**", "").Trim().TrimStart(':').Trim();
            }
            else
            {
                body = body.Replace("**", "");
                var c = body.IndexOf(':');
                if (c is > 3 and < 60) { title = body[..c].Trim(); text = body[(c + 1)..].Trim(); }
                else if (body.Length <= 60) { title = body; text = ""; }
                else { var cut = body.LastIndexOf(' ', 50); title = body[..(cut > 20 ? cut : 50)].TrimEnd(',', ' ') + " …"; text = body; }
            }
            if (text.Length > 0) text = char.ToUpper(text[0]) + text[1..];
        }
        Flush();
        return res;
    }

    static readonly (string[] Words, string Icon)[] Icons =
    [
        (["mail", "posteingang"], "✉️"), (["karte", "tour", "gpx"], "🗺️"), (["spiel", "level"], "🎮"),
        (["3d", "scan", "punktwolke", "modell"], "🧊"), (["video", "film"], "🎬"), (["musik", "klang", "audio"], "🎵"), (["foto", "bild"], "🖼️"),
        (["geld", "kasse", "haushalt"], "💶"), (["kalender", "termin", "zeitplan"], "📅"), (["aufgabe", "kanban", "tafel", "projekt"], "🗂️"), (["sicherung", "backup"], "🛟"),
        (["leistung", "task-manager", "speicher"], "📈"), (["download", "github"], "⬇️"), (["kleidung", "schnitt"], "🧵"), (["wissen", "wiki", "notiz"], "📚"),
        (["ablauf", "automatisch"], "⚙️"), (["brief", "text", "pdf", "dokument"], "📄"), (["tv", "sendung"], "📺"), (["auto", "fahrzeug", "fahrtenbuch"], "🚗"),
        (["grundriss", "wohnen", "möbel"], "🏠"), (["schaltung", "platine"], "🔌"), (["tragwerk", "cnc", "roboter"], "🛠️"), (["baukasten", "app", "webseite"], "🧩"),
        (["chat", "ki ", "ki-", "claude", "assistent", "skill"], "✨"), // zuletzt: „KI-Rolle“ steht in vielen Texten
    ];

    public static string Icon(string s) { s = " " + s.ToLowerInvariant(); foreach (var (w, i) in Icons) if (w.Any(s.Contains)) return i; return "✦"; }
}
