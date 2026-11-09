using System.Text;

namespace Trendo.Core.ConsolePort;

/// <summary>Studio „Konsolenspiele portieren“: Selbst besessene PS2-Spiele als eigenes Programm auf andere Geräte bringen, mit den Claude-Skills aus console-skill (MIT) und dem Recompiler PS2Recomp.
/// Trendo liefert weder Spiele noch BIOS und sucht sie nicht. Nur ein Abbild von der eigenen Disc, nur lokal; Spieldaten bleiben auf dem PC der Person.</summary>
public static class ConsolePortKit
{
    public const string SkillRepo = "https://github.com/Jakeschincariol/console-skill";
    public const string SkillCommit = "a508929f1e8f5dd6f54afe489fd74340df3b6f74";

    public sealed record Skill(string Id, string Title, string What, string Stage);
    public static readonly IReadOnlyList<Skill> Skills =
    [
        new("console-reverse", "Spiel untersuchen", "Liest das Abbild, findet das Startprogramm und richtet den Recompiler ein.", "Grundlage"),
        new("console-backend", "Laufzeit ergänzen", "Baut fehlende Systemaufrufe, Dateizugriffe und Bausteine nach.", "Grundlage"),
        new("console-tester", "Starten und prüfen", "Startet den Port, wertet Protokolle aus, ordnet Abstürze.", "Grundlage"),
        new("console-graphics", "Grafik", "Texturen auslesen und ersetzen.", "Ausbau"),
        new("console-sound", "Ton", "Musik und Geräusche auslesen und ersetzen.", "Ausbau"),
        new("console-levels", "Level", "Leveldaten auslesen und ändern.", "Ausbau"),
        new("console-controller", "Controller", "Belegung für Xbox, DualSense, Switch Pro und andere.", "Ausbau"),
        new("console-physics", "Spielwerte", "Werte wie Tempo oder Schwerkraft finden.", "Ausbau"),
        new("console-mod", "Ändern", "Wünsche in Klartext werden zu Patches, das Original bleibt unberührt.", "Ausbau"),
        new("console-saves", "Spielstände", "Speicherstände übernehmen und abgleichen.", "Ausbau"),
    ];

    public sealed record Target(string Id, string Label);
    public static readonly IReadOnlyList<Target> Targets =
    [
        new("windows", "Windows"), new("linux", "Linux"), new("macos", "Mac"), new("steamdeck", "Steam Deck"), new("android", "Android"), new("vita", "PS Vita"),
    ];

    public sealed record Tool(string Id, string Label, string Why, bool Required);
    public static readonly IReadOnlyList<Tool> Tools =
    [
        new("git", "Git", "lädt die Skills", true),
        new("python", "Python 3.8 oder neuer", "für die Werkzeuge der Skills", true),
        new("cmake", "CMake 3.20 oder neuer", "baut den Recompiler und das Spiel", true),
        new("ninja", "Ninja", "beschleunigt das Bauen", false),
        new("pcsx2", "PCSX2", "nur zum Vergleichen mit dem Original", false),
    ];

    /// <summary>Prüft, ob die Datei ein lesbares Disc-Abbild ist. Liefert null, wenn alles passt, sonst den Grund in Klartext.</summary>
    public static string? CheckImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Wähle das Abbild deiner eigenen Disc aus.";
        if (path.Contains("://", StringComparison.Ordinal)) return "Nur eine Datei auf diesem PC. Trendo lädt keine Spiele aus dem Internet.";
        if (!path.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) return "Bitte ein Abbild im Format .iso wählen.";
        if (!File.Exists(path)) return "Die Datei wurde nicht gefunden.";
        const int Signature = 0x8001;
        Span<byte> id = stackalloc byte[5];
        using var f = File.OpenRead(path);
        if (f.Length < Signature + 5) return "Das ist kein vollständiges Disc-Abbild.";
        f.Position = Signature; f.ReadExactly(id);
        return Encoding.ASCII.GetString(id) == "CD001" ? null : "Das ist kein gültiges ISO-Abbild.";
    }

    /// <summary>Darf die Portierung starten? Liefert null, sonst was noch fehlt.</summary>
    public static string? ReadyToStart(string game, string image, bool owned)
    {
        if (string.IsNullOrWhiteSpace(game)) return "Trag zuerst den Namen des Spiels ein.";
        if (!owned) return "Bestätige zuerst, dass du das Spiel besitzt und das Abbild von deiner eigenen Disc stammt.";
        return CheckImage(image);
    }

    /// <summary>Git-Aufrufe für die Skills in fester Fassung. Laufen im leeren Zielordner.</summary>
    public static IReadOnlyList<string[]> InstallSteps() =>
    [
        ["init", "-q"],
        ["fetch", "-q", "--depth", "1", SkillRepo, SkillCommit],
        ["checkout", "-q", "FETCH_HEAD"],
    ];

    const string Rules = "Regeln: Arbeite nur mit dem angegebenen Abbild. Suche, lade oder empfiehl keine Spiele, ROMs, ISOs oder BIOS-Dateien. Spieldaten, das Abbild und erzeugter Spielcode bleiben auf diesem PC und werden nirgends hochgeladen. Antworte auf Deutsch.";

    public static string Prompt(string skill, string game, string image, string projectDir)
    {
        if (Skills.All(s => s.Id != skill)) throw new ArgumentException("Unbekannter Schritt: " + skill);
        return $"/{skill}\nSpiel: {game}\nAbbild: {image}\nArbeitsordner: {projectDir}\n{Rules}";
    }

    public static string PackagePrompt(string target, string game, string projectDir)
    {
        if (Targets.All(t => t.Id != target)) throw new ArgumentException("Unbekanntes Gerät: " + target);
        return $"/console-anywhere package --target {target}\nSpiel: {game}\nArbeitsordner: {projectDir}\nDas Paket darf keine Spieldaten enthalten, lass die eingebaute Prüfung „guard“ laufen.\n{Rules}";
    }

    /// <summary>Fortschritt aus console/STATE.md, höchstens die ersten Zeilen.</summary>
    public static string? State(string projectDir, int maxChars = 4000)
    {
        var f = Path.Combine(projectDir, "console", "STATE.md");
        if (!File.Exists(f)) return null;
        var t = File.ReadAllText(f);
        return t.Length <= maxChars ? t : t[..maxChars] + "\n…";
    }
}
