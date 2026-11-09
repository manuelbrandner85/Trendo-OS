using System.Security.Cryptography;

namespace Trendo.Core.ConsolePort;

/// <summary>Spiele-Bibliothek im Konsolen-Studio: eigene Spiele nach Spielprinzip, frei lizenzierte Godot-Projekte und frei verteilte Homebrew-Spiele für den Emulator.
/// Bestehende kommerzielle Spiele stehen nicht im Katalog und werden nicht nachgebaut, auch nicht umbenannt. Trendo lädt nie ROMs oder BIOS von Kaufspielen.</summary>
public static class GameLibrary
{
    public enum Kind { Eigen, Frei, Homebrew }

    public sealed record Entry(string Id, string Title, Kind Kind, string Genre, string Style, string Description, IReadOnlyList<string> Features, string License, string? Author = null, string? Folder = null, string? Url = null, string? Sha256 = null);

    public const string WishId = "wunsch";
    public const int MaxWish = 2000;

    public const string DemoRepo = "https://github.com/godotengine/godot-demo-projects";
    public const string DemoCommit = "6ad6167e0577fe3622c18546138f456b107ce93c";

    public const string MgbaUrl = "https://github.com/mgba-emu/mgba/releases/download/0.10.5/mGBA-0.10.5-win64.7z";
    public const string MgbaSha256 = "b497a57c7d9093834dadc64f33a90f7c411439c21fdb8a0143255a45ea37563a";
    public const string MgbaExe = "mGBA-0.10.5-win64/mGBA.exe";

    const string Demo = "MIT (Godot-Demoprojekte)";
    const string Yours = "Dein Projekt";

    public static readonly IReadOnlyList<Entry> Entries =
    [
        new("morvane", "Hafenstadt Morvane", Kind.Eigen, "Open World, Krimi", "Moderne Optik", "Eine erfundene Hafenstadt, frei befahrbar. Du steigst vom Kurierfahrer zur festen Größe der Unterwelt auf.", ["Stadt frei erkunden", "Fahrzeuge fahren und wechseln", "Aufträge und Nebenjobs", "Fahndungsstufe", "Radio mit eigener Musik"], Yours),
        new("moosgrund", "Moosgrund", Kind.Eigen, "3D-Hüpfspiel", "2000er-3D", "Ein kleiner Waldgeist sammelt das Licht seines Tals wieder ein, von Baumkrone zu Baumkrone.", ["Dreifachsprung und Gleiten", "Offene Level", "Sammelobjekte", "Bosse am Ende jedes Tals"], Yours),
        new("ascheturm", "Ascheturm", Kind.Eigen, "Action-Rollenspiel, schwer", "Moderne Optik", "Ein Turm aus Asche, der sich jede Nacht neu ordnet. Jeder Fehler kostet, jeder Sieg zählt.", ["Ausdauerbasierter Nahkampf", "Ausweichen und Parieren", "Bosse mit Phasen", "Rastplätze"], Yours),
        new("kuestenrallye", "Küstenrallye", Kind.Eigen, "Arcade-Rennen", "2000er-3D", "Sonnige Küstenstraßen, driftende Wagen und knappe Zieleinläufe.", ["Driften mit Boost", "Sechs Strecken", "Geteilter Bildschirm", "Fahrzeuge verbessern"], Yours),
        new("eisfront", "Eisfront-Station", Kind.Eigen, "Survival-Horror", "HD-Ära", "Eine Forschungsstation im Eis antwortet nicht mehr. Munition und Licht sind knapp.", ["Knappe Vorräte", "Rätsel", "Enge Gänge", "Mehrere Enden"], Yours),
        new("klingenbund", "Klingenbund", Kind.Eigen, "Rollenspiel, rundenbasiert", "2000er-3D", "Vier Fremde, ein Schwur und ein Königreich, das langsam vergisst.", ["Rundenkämpfe", "Gruppe aus vier Figuren", "Weltkarte", "Berufe wechseln"], Yours),
        new("funkstille", "Funkstille", Kind.Eigen, "Schleichspiel", "HD-Ära", "Ein Agent ohne Verbindung zur Zentrale. Gesehen werden heißt von vorn beginnen.", ["Sichtkegel und Geräusche", "Gadgets", "Mehrere Wege je Einsatz", "Bewertung am Ende"], Yours),
        new("sternenwerft", "Sternenwerft", Kind.Eigen, "Weltraum-Action", "Moderne Optik", "Du baust dein Schiff Teil für Teil und verteidigst die letzte freie Werft.", ["Schiff zusammenbauen", "Raumkämpfe", "Handel", "Fraktionen"], Yours),

        new("demo-platformer-3d", "Platformer 3D", Kind.Frei, "3D-Hüpfspiel", "Offenes Projekt", "Offizielles Godot-Beispiel mit Figur, Gegnern und Münzen. Ideal zum Umbauen.", ["Sofort spielbar", "Komplett änderbar"], Demo, "Godot-Mitwirkende", "3d/platformer"),
        new("demo-squash", "Squash the Creeps", Kind.Frei, "3D-Arcade", "Offenes Projekt", "Springe auf Gegner, bevor sie dich erwischen.", ["Kurze Runden", "Komplett änderbar"], Demo, "Godot-Mitwirkende", "3d/squash_the_creeps"),
        new("demo-truck", "Truck Town", Kind.Frei, "Fahrspiel", "Offenes Projekt", "Fahrzeuge mit echter Fahrphysik in einer kleinen Stadt, ein guter Start für eigene Fahrspiele.", ["Fahrphysik", "Mehrere Fahrzeuge", "Komplett änderbar"], Demo, "Godot-Mitwirkende", "3d/truck_town"),
        new("demo-platformer-2d", "Platformer 2D", Kind.Frei, "2D-Hüpfspiel", "Offenes Projekt", "Pixel-Art-Hüpfspiel mit Grafik und Ton.", ["Pixel-Art", "Komplett änderbar"], Demo, "Godot-Mitwirkende", "2d/platformer"),
        new("demo-dodge", "Dodge the Creeps", Kind.Frei, "2D-Arcade", "Offenes Projekt", "Weiche so lange wie möglich aus.", ["Kurze Runden", "Komplett änderbar"], Demo, "Godot-Mitwirkende", "2d/dodge_the_creeps"),

        new("hb-libbet", "Libbet and the Magic Floor", Kind.Homebrew, "Rätselspiel", "Game Boy", "Rolle den Boden aus, ohne dich festzufahren. Ein frei verteiltes Game-Boy-Spiel.", ["Läuft im Emulator", "Frei verteilt"], "Zlib", "Damian Yerrick", null, "https://github.com/pinobatch/libbet/releases/download/v0.08/libbet.gb", "3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9"),
        new("hb-ucity", "µCity", Kind.Homebrew, "Städtebau", "Game Boy Color", "Baue und verwalte eine Stadt auf dem Game Boy Color.", ["Läuft im Emulator", "Frei verteilt"], "GPL-3.0 oder neuer, Grafik und Musik CC BY-SA 4.0", "Antonio Niño Díaz", null, "https://github.com/AntonioND/ucity/releases/download/v1.3/ucity.gbc", "9422ee2ca7b7ea1d46b58b2a429fff3f354dfd3e732dee1e7ae6220f148ce6e0"),
    ];

    public sealed record Target(string Id, string Label);
    public static readonly IReadOnlyList<Target> Targets = [new("windows", "Windows"), new("linux", "Linux"), new("macos", "Mac"), new("android", "Android"), new("web", "Browser")];

    public static Entry? Find(string? id) => Entries.FirstOrDefault(e => e.Id == id);

    public static string KindLabel(Kind k) => k switch { Kind.Eigen => "Eigenes Spiel", Kind.Frei => "Freies Spiel", _ => "Homebrew" };

    /// <summary>Eintrag für ein Spiel, das aus einem frei formulierten Wunsch entsteht.</summary>
    public static Entry Wish(string text) => new(WishId, "Dein Wunsch-Spiel", Kind.Eigen, "Nach deinem Wunsch", "Frei", text.Trim(), [], Yours);

    /// <summary>Darf der Wunsch losgeschickt werden? Liefert null, sonst den Grund.</summary>
    public static string? CheckWish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Beschreibe zuerst, was du spielen möchtest.";
        return text.Length > MaxWish ? $"Bitte höchstens {MaxWish} Zeichen." : null;
    }

    /// <summary>Gilt für jeden Auftrag an Claude: Spielprinzip ja, Nachbau bestehender Spiele nein.</summary>
    public const string OriginalRule = "Regeln: Erfinde alles neu: Name, Welt, Karten, Figuren, Story, Fahrzeuge, Logos, Oberfläche, Grafik, Musik und Texte. Nennt der Wunsch ein bestehendes Spiel, übernimm daraus nur Genre und allgemeine Spielmechaniken. Baue kein bestehendes Spiel nach, auch nicht umbenannt oder leicht verändert, und sag das offen, wenn der Wunsch darauf hinausläuft. Suche, lade oder empfiehl keine ROMs, ISOs oder BIOS-Dateien. Verwende nur selbst erzeugte oder frei lizenzierte Inhalte und notiere deren Herkunft und Lizenz in CREDITS.md. Antworte auf Deutsch.";

    const string BuildSteps = "Beginne mit einem kleinen, vollständig spielbaren Ausschnitt und baue ihn dann aus. Prüfe mit Godot ohne Fenster, dass das Projekt fehlerfrei lädt, und sag ehrlich, was noch fehlt.";

    public static string BuildPrompt(Entry e, string projectDir)
    {
        if (e.Kind != Kind.Eigen) throw new ArgumentException("Nur eigene Spiele werden neu gebaut.");
        if (e.Id == WishId) return $"Baue mit Godot 4 ein neues, eigenes Spiel nach diesem Wunsch:\n{e.Description}\nArbeitsordner: {projectDir}\n{BuildSteps}\n{OriginalRule}";
        return $"Baue mit Godot 4 das eigene Spiel „{e.Title}“.\nGenre: {e.Genre}\nStil: {e.Style}\nIdee: {e.Description}\nMerkmale: {string.Join(", ", e.Features)}\nArbeitsordner: {projectDir}\n{BuildSteps}\n{OriginalRule}";
    }

    public static string ChangePrompt(Entry e, string wish, string projectDir)
    {
        var keep = e.Kind == Kind.Frei ? $" Das Projekt steht unter {e.License}: Lizenz- und Urheberhinweise bleiben erhalten." : "";
        return $"Ändere das Spiel „{e.Title}“ in diesem Ordner: {projectDir}\nWunsch: {wish.Trim()}\nPrüfe danach mit Godot ohne Fenster, dass das Projekt fehlerfrei lädt.{keep}\n{OriginalRule}";
    }

    public static string ExportPrompt(Entry e, string target, string projectDir)
    {
        if (Targets.All(t => t.Id != target)) throw new ArgumentException("Unbekanntes Gerät: " + target);
        return $"Exportiere das Godot-Spiel „{e.Title}“ aus {projectDir} für {Targets.First(t => t.Id == target).Label}. Lege eine passende Exportvorlage an und nenne genau, was auf diesem PC dafür noch fehlt, statt einen Erfolg zu behaupten.\n{OriginalRule}";
    }

    /// <summary>Ordner des Godot-Projekts unterhalb des Dokument-Ordners.</summary>
    public static string ProjectDir(string baseDir, Entry e) =>
        e.Folder is null ? Path.Combine(baseDir, e.Id) : Path.Combine([baseDir, e.Id, .. e.Folder.Split('/')]);

    public static bool IsBuilt(string projectDir) => File.Exists(Path.Combine(projectDir, "project.godot"));

    public static IReadOnlyList<string> PlayArgs(string projectDir) => ["--path", projectDir];

    /// <summary>Git-Aufrufe für ein freies Spiel in fester Fassung, nur der Ordner dieses Spiels. Laufen im leeren Zielordner.</summary>
    public static IReadOnlyList<string[]> FetchSteps(Entry e)
    {
        if (e.Kind != Kind.Frei || e.Folder is null) throw new ArgumentException("Nur freie Spiele werden geholt.");
        return
        [
            ["init", "-q"],
            ["remote", "add", "origin", DemoRepo],
            ["sparse-checkout", "set", e.Folder],
            ["fetch", "-q", "--depth", "1", "--filter=blob:none", "origin", DemoCommit],
            ["checkout", "-q", "FETCH_HEAD"],
        ];
    }

    public static string RomFile(Entry e) => e.Url is null ? throw new ArgumentException("Kein Homebrew-Spiel.") : e.Url[(e.Url.LastIndexOf('/') + 1)..];

    /// <summary>Lädt eine Datei und behält sie nur, wenn die Prüfsumme stimmt. Liefert null, sonst den Grund.</summary>
    public static async Task<string?> DownloadAsync(HttpClient http, string url, string sha256, string dest, CancellationToken ct = default)
    {
        var part = dest + ".part";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                if (!resp.IsSuccessStatusCode) return $"Der Download ist fehlgeschlagen ({(int)resp.StatusCode}).";
                await using var from = await resp.Content.ReadAsStreamAsync(ct);
                await using var to = File.Create(part);
                await from.CopyToAsync(to, ct);
            }
            string actual;
            await using (var check = File.OpenRead(part)) actual = Convert.ToHexString(await SHA256.HashDataAsync(check, ct));
            if (!actual.Equals(sha256, StringComparison.OrdinalIgnoreCase)) { File.Delete(part); return "Die Prüfsumme stimmt nicht. Die Datei wurde verworfen."; }
            File.Move(part, dest, true);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException)
        {
            try { if (File.Exists(part)) File.Delete(part); } catch (IOException) { }
            return "Der Download ist fehlgeschlagen. Ist das Internet erreichbar?";
        }
    }
}
