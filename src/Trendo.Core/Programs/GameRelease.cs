using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>Spiel veröffentlichen (Wunsch 02.11.2026): Ziele (Browser, itch.io, Windows, Mac, Steam, Handy), Checkliste je Plattform wie in echten Studios,
/// fertige Pakete je Ziel und die Befehle zum Hochladen mit dem eigenen Konto. Hochgeladen wird nur nach Ja des Nutzers, und die Anmeldung macht er selbst
/// (Trendo sieht kein Passwort). Die Angaben liegen im Spiele-Dokument unter „release“.</summary>
public static partial class GameRelease
{
    public sealed record Target(string Id, string Label, string Hint);
    public static readonly Target[] Targets =
    [
        new("browser", "Browser-Spiel", "Spielbar im Browser – auf deiner Webseite oder zum Weitergeben"),
        new("itchio", "itch.io", "Die große Plattform für Indie-Spiele – kostenloses Konto, Browser- und Download-Spiele"),
        new("windows", "Windows", "Zum Herunterladen und Spielen unter Windows"),
        new("mac", "Mac", "Zum Herunterladen und Spielen auf dem Mac"),
        new("steam", "Steam", "Die größte PC-Plattform – braucht ein Steamworks-Konto (einmalige Gebühr je Spiel)"),
        new("handy", "Handy", "Im Handy-Browser spielbar, auf dem Startbildschirm installierbar (Web-App)"),
    ];

    public sealed record Item(string Id, string Label, string Hint, string[] For);
    /// <summary>Checkliste wie in Spielestudios – je Eintrag, für welche Ziele er gilt (leer = alle).</summary>
    public static readonly Item[] Checklist =
    [
        new("test", "Komplett durchgespielt und getestet", "Vom Start bis zum Ende, ohne Absturz – am besten von jemand anderem", []),
        new("texte", "Store-Texte fertig", "Titel, Kurzbeschreibung (1 Satz) und Beschreibung mit den besten Seiten des Spiels", []),
        new("bilder", "Bilder fertig", "Titelbild und mindestens 5 Screenshots (1920 × 1080)", []),
        new("trailer", "Trailer", "30–90 Sekunden mit den besten Szenen (empfohlen)", ["steam", "itchio"]),
        new("alter", "Altersfreigabe geklärt", "USK/PEGI – für Stores über den kostenlosen IARC-Fragebogen", ["steam", "itchio", "handy"]),
        new("datenschutz", "Datenschutz geklärt", "Sammelt das Spiel keine Daten, steht das so in der Beschreibung; sonst eine Datenschutzerklärung", ["steam", "itchio", "handy", "browser"]),
        new("preis", "Preis festgelegt", "Kostenlos, fester Preis oder „Zahl, was du willst“ (itch.io)", ["steam", "itchio"]),
        new("anforderungen", "Systemanforderungen", "Betriebssystem, Prozessor, Speicher, Grafikkarte", ["windows", "mac", "steam"]),
        new("konto-steam", "Steamworks-Konto und App-ID", "partner.steamgames.com – Konto anlegen machst du selbst (einmalige Gebühr je Spiel)", ["steam"]),
        new("konto-itch", "itch.io-Konto und Spielseite", "itch.io – Konto und Spielseite legst du selbst an", ["itchio"]),
    ];

    static JsonObject Rel(JsonObject d) { if (d["release"] is not JsonObject r) { r = []; d["release"] = r; } return r; }

    /// <summary>Gewählte Ziele – aus „release.targets“, sonst aus dem Feld „platforms“ (z. B. „Windows und Mac“) erkannt.</summary>
    public static IReadOnlyList<string> TargetsOf(JsonObject d)
    {
        if (d["release"]?["targets"] is JsonArray a) // ausdrücklich gewählt (auch „keins“) – sonst aus „platforms“ erkannt
            return a.Select(x => x.AsText() ?? "").Where(t => Targets.Any(x => x.Id == t)).Distinct().ToList();
        var p = (d["platforms"].AsText() ?? "").ToLowerInvariant();
        var list = new List<string>();
        if (p.Contains("browser") || p.Contains("web")) list.Add("browser");
        if (p.Contains("itch")) list.Add("itchio");
        if (p.Contains("windows") || p.Contains("pc") || p.Contains("setup")) list.Add("windows");
        if (p.Contains("mac")) list.Add("mac");
        if (p.Contains("steam")) list.Add("steam");
        if (p.Contains("handy") || p.Contains("mobil") || p.Contains("android") || p.Contains("ios")) list.Add("handy");
        return list;
    }

    public static void SetTargets(JsonObject d, IEnumerable<string> targets) => Rel(d)["targets"] = new JsonArray(targets.Distinct().Select(t => (JsonNode?)t).ToArray());

    public static IReadOnlyList<Item> ChecklistFor(IEnumerable<string> targets)
    {
        var t = targets.ToList();
        return Checklist.Where(i => i.For.Length == 0 || i.For.Any(t.Contains)).ToList();
    }
    public static bool Done(JsonObject d, string item) => d["release"]?["done"]?[item].AsBool() == true;
    public static void SetDone(JsonObject d, string item, bool on) { var r = Rel(d); if (r["done"] is not JsonObject dn) { dn = []; r["done"] = dn; } dn[item] = on; }
    public static string Get(JsonObject d, string key) => d["release"]?[key].AsText() ?? "";
    public static void Set(JsonObject d, string key, string value) => Rel(d)[key] = value;

    [GeneratedRegex(@"[^\p{L}\p{N}]+")] private static partial Regex NonWord();
    public static string Slug(string title) { var s = NonWord().Replace(title.Trim(), "-").Trim('-'); return s.Length == 0 ? "Spiel" : s.Length > 40 ? s[..40].Trim('-') : s; }

    /// <summary>Pakete je Ziel aus dem gebauten Browser-Spiel (<paramref name="built"/> mit index.html). Liefert die Dateien und Hinweise.</summary>
    public static (List<string> Files, List<string> Notes) Pack(JsonObject d, string built, string outDir, IEnumerable<string> targets)
    {
        var files = new List<string>(); var notes = new List<string>();
        var name = Slug(d["title"].AsText() ?? "Spiel");
        Directory.CreateDirectory(outDir);
        var web = File.Exists(Path.Combine(built, "index.html"));
        if (web) MixamoGuard.Ensure(MixamoGuard.Export, [built]); // T-0660 M6: lose Mixamo-Rohdateien nie in ein Paket zum Weitergeben
        foreach (var t in targets.Distinct())
        {
            if (!web) { notes.Add($"{Label(t)}: Dieses Spiel ist ein Unreal-Projekt – fertige Pakete baut Unreal selbst (Projekt öffnen → Plattformen → Windows → „Projekt packen“). Für Mac braucht Unreal einen Mac."); continue; }
            switch (t)
            {
                case "browser" or "itchio":
                    files.Add(Zip(built, Path.Combine(outDir, $"{name}-web.zip"))); // itch.io: HTML5-Spiel, index.html liegt oben
                    break;
                case "windows":
                    files.Add(Zip(built, Path.Combine(outDir, $"{name}-windows.zip"), ("Spielen.cmd", "@echo off\r\nstart \"\" msedge --app=\"%~dp0index.html\" 2>nul || start \"\" \"%~dp0index.html\"\r\n")));
                    notes.Add("Windows: ZIP entpacken, „Spielen.cmd“ starten – das Spiel öffnet im eigenen Fenster. Ein klassisches Setup mit Installer gibt es bei Unreal- und Godot-Spielen.");
                    break;
                case "mac":
                    files.Add(Zip(built, Path.Combine(outDir, $"{name}-mac.zip"), ("Spielen.command", "#!/bin/sh\ncd \"$(dirname \"$0\")\"\nopen index.html\n")));
                    notes.Add("Mac: ZIP entpacken, „Spielen.command“ öffnen (beim ersten Mal mit Rechtsklick → Öffnen, weil es nicht signiert ist).");
                    break;
                case "handy":
                    files.Add(Zip(built, Path.Combine(outDir, $"{name}-handy.zip"), ("manifest.webmanifest", $"{{\"name\":\"{Escape(d["title"].AsText() ?? name)}\",\"start_url\":\"index.html\",\"display\":\"fullscreen\",\"orientation\":\"landscape\",\"background_color\":\"#000000\"}}")));
                    notes.Add("Handy: Als Web-App veröffentlichen (z. B. auf itch.io oder deiner Seite) – im Handy-Browser „Zum Startbildschirm“ hinzufügen. Für App-Stores braucht es Godot oder Unreal.");
                    break;
                case "steam":
                    var dir = Path.Combine(outDir, $"{name}-steam"); var content = Path.Combine(dir, "content");
                    CopyDir(built, content);
                    File.WriteAllText(Path.Combine(content, "Spielen.cmd"), "@echo off\r\nstart \"\" msedge --app=\"%~dp0index.html\" 2>nul || start \"\" \"%~dp0index.html\"\r\n");
                    var app = Get(d, "steamApp"); var appId = Regex.IsMatch(app, @"^\d+$") ? app : "DEINE_APP_ID"; var depot = Regex.IsMatch(app, @"^\d+$") ? (long.Parse(app) + 1).ToString() : "DEINE_DEPOT_ID";
                    Directory.CreateDirectory(Path.Combine(dir, "scripts"));
                    File.WriteAllText(Path.Combine(dir, "scripts", "app_build.vdf"), $"\"AppBuild\"\n{{\n  \"AppID\" \"{appId}\"\n  \"Desc\" \"{Escape(d["title"].AsText() ?? name)}\"\n  \"ContentRoot\" \"..\\content\\\"\n  \"BuildOutput\" \"..\\output\\\"\n  \"Depots\"\n  {{\n    \"{depot}\"\n    {{\n      \"FileMapping\" {{ \"LocalPath\" \"*\" \"DepotPath\" \".\" \"recursive\" \"1\" }}\n    }}\n  }}\n}}\n");
                    File.WriteAllText(Path.Combine(dir, "LIESMICH.txt"), "Steam-Upload vorbereitet.\r\n1. Steamworks-Konto und App-ID anlegen (partner.steamgames.com) – machst du selbst.\r\n2. App-ID im Spiele-Programm unter „Veröffentlichen“ eintragen und Pakete neu bauen.\r\n3. „Hochladen“ öffnet SteamCMD – dort meldest du dich selbst an.\r\n");
                    files.Add(dir);
                    notes.Add("Steam: Inhalt und Upload-Skript liegen bereit. Hochladen geht erst mit deiner App-ID und deiner Anmeldung in SteamCMD.");
                    break;
            }
        }
        return (files, notes);
    }

    public static string Label(string id) => Targets.FirstOrDefault(t => t.Id == id)?.Label ?? id;
    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static string Zip(string dir, string zip, params (string Name, string Content)[] extra)
    {
        if (File.Exists(zip)) File.Delete(zip);
        using var a = ZipFile.Open(zip, ZipArchiveMode.Create);
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir, f);
            if (rel.Split(Path.DirectorySeparatorChar).Any(p => p is ".trendo" or ".claude" or ".vorschau")) continue;
            a.CreateEntryFromFile(f, rel.Replace(Path.DirectorySeparatorChar, '/'));
        }
        foreach (var (n, c) in extra) { using var w = new StreamWriter(a.CreateEntry(n).Open()); w.Write(c); }
        return zip;
    }

    static void CopyDir(string src, string dst)
    {
        foreach (var f in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, f);
            if (rel.Split(Path.DirectorySeparatorChar).Any(p => p is ".trendo" or ".claude" or ".vorschau")) continue;
            var t = Path.Combine(dst, rel); Directory.CreateDirectory(Path.GetDirectoryName(t)!); File.Copy(f, t, true);
        }
    }

    /// <summary>Befehl zum Hochladen mit dem eigenen Konto (der Nutzer meldet sich im geöffneten Fenster selbst an).</summary>
    public static string? Upload(JsonObject d, string target, string package)
    {
        switch (target)
        {
            case "itchio":
                var page = Get(d, "itch").Trim();
                if (!Regex.IsMatch(page, @"^[A-Za-z0-9_-]+/[A-Za-z0-9_-]+$")) return null;
                return $"butler push \"{package}\" {page}:html5";
            case "steam":
                var script = Path.Combine(package, "scripts", "app_build.vdf");
                return Regex.IsMatch(Get(d, "steamApp"), @"^\d+$") ? $"steamcmd +login {Get(d, "steamUser").Trim()} +run_app_build \"{script}\" +quit" : null;
        }
        return null;
    }

    /// <summary>Teil der Spielregeln: was Claude für die Veröffentlichung im Dokument ausfüllt.</summary>
    public const string Rule = "\n- Veröffentlichen: Wünscht der Nutzer eine Veröffentlichung, fülle im Spiele-Dokument unter „release“ aus: targets (Liste aus browser, itchio, windows, mac, steam, handy), " +
        "short (ein Satz), long (Beschreibung), price, age (z. B. USK 12), requirements – Schritt für Schritt gespeichert. Hochladen startet nur der Nutzer selbst (Knopf „Hochladen“, eigene Anmeldung).";
}
