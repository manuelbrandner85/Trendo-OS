using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0771 (Etappe 0.9.111): Restpunkte aus 0.9.110 – protokollrelative Links im eingebetteten App-JSON (N-a), Zähler der Test-Freigaben
/// beim erneuten Öffnen eines Profils (N2) und Ersteinrichtung nur in einem wirklich neuen Datenordner (Prüfer-N1).</summary>
[Collection("ChildWrite")]
public class T0771Restpunkte
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0771-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static void Weg(string d) { try { Directory.Delete(d, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }

    static string Repo() { var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!; return d; }

    [Fact(DisplayName = "T-0771 N-b: Katalog-Erzeuger meldet veraltete Programmnamen der Vorbild-Analysen früh und klar (Datei und Name); aktueller Stand ohne Meldung")]
    public void Veraltete_vorbild_namen()
    {
        var repo = Tmp();
        try
        {
            var dir = Directory.CreateDirectory(Path.Combine(repo, "docs", "claude", "vorbilder")).FullName;
            File.WriteAllText(Path.Combine(dir, "netz.md"), "# Vorbild-Analyse Netz (Clean-Room)\n\n- Programme: „Downloads“, „Task-Manager“\n- Vorbilder: x\n");
            File.WriteAllText(Path.Combine(dir, "system.md"), "# System (Clean-Room)\n\n- Programme: „Einstellungen“, „Leistung“\n");
            var stale = Trendo.Core.Programs.ProgramCatalog.StaleAnalysisNames(repo, ["Downloads & Links", "Task-Manager", "Einstellungen", "Alles prüfen"]);
            Assert.Equal([("netz.md", "Downloads"), ("system.md", "Leistung")], stale);
            var msg = Trendo.Core.Programs.ProgramCatalog.StaleAnalysisMessage(stale);
            Assert.Contains("netz.md: „Downloads“", msg); Assert.Contains("system.md: „Leistung“", msg); Assert.Contains("docs/claude/vorbilder", msg);
            Assert.Equal("", Trendo.Core.Programs.ProgramCatalog.StaleAnalysisMessage([]));
        }
        finally { Weg(repo); }
        // Stand dieses Zweigs: Katalog und Vorbild-Analysen passen zusammen
        var known = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Repo(), "docs", "programme", "katalog.json")))!.AsArray().Select(x => (string)x!["Name"]!);
        Assert.Empty(Trendo.Core.Programs.ProgramCatalog.StaleAnalysisNames(Repo(), known));
    }

    public static TheoryData<string> FremdeZiele() =>
    [
        "//fremd.example/x", "\\\\fremd.example\\x", "\\fremd.example", "/\\fremd.example", "\\/fremd.example", "   //fremd.example", "/pfad/auf/der/wurzel",
        "%2F%2Ffremd.example", "%2f%5cfremd.example", "%5C%5Cfremd.example", "%252F%252Ffremd.example", "&#47;&#47;fremd.example", "&#x2F;&#x2F;fremd.example",
        "%20%2F%2Ffremd.example", "%09//fremd.example", "%00//fremd.example", "javascript%3Aalert(1)", "javascript&#58;alert(1)", "%6A%61vascript%3Aalert(1)",
    ];

    [Theory(DisplayName = "T-0771 N-a: eingebettetes App-JSON – protokollrelative, Wurzel- und kodierte Ziele werden zu „#“")]
    [MemberData(nameof(FremdeZiele))]
    public void Fremde_ziele_werden_raute(string ziel)
    {
        var p = SitePage.Template("einkaufsliste")!;
        p.Pages[0].Blocks.Add(new Block("button", "Fremd", ziel));
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "index.html"); File.WriteAllText(f, p.ToHtml());
            var q = SitePage.FromExport(f);
            Assert.Equal("#", q.Pages[0].Blocks[^1].Url);
            Assert.DoesNotContain("fremd.example", File.ReadAllText(f).Split("id=\"trendo-app\">")[1].Split("</script>")[0]);
        }
        finally { Weg(d); }
    }

    [Fact(DisplayName = "T-0771 N-a: Gegenprobe – Datei, E-Mail, seite:, summe:, https und mailto bleiben im eingebetteten JSON erhalten")]
    public void Erlaubte_ziele_bleiben()
    {
        var p = SitePage.Template("einkaufsliste")!;
        string[] gut = ["bild.png", "ordner/bild.png", "info@beispiel.de", "https://example.org/a?b=%2F%2F", "mailto:info@beispiel.de", "anzahl"];
        foreach (var g in gut) p.Pages[0].Blocks.Add(new Block("button", "Gut", g));
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "index.html"); File.WriteAllText(f, p.ToHtml());
            var q = SitePage.FromExport(f);
            Assert.Equal(gut, q.Pages[0].Blocks.TakeLast(gut.Length).Select(b => b.Url));
            Assert.Contains(q.Pages.SelectMany(s => s.Blocks), b => b.Url.StartsWith("seite:", StringComparison.Ordinal));
        }
        finally { Weg(d); }
    }

    [Fact(DisplayName = "T-0771 N2: Profil zweimal offen – Schließen des ersten Fensters nimmt dem zweiten die Freigabe nicht; erst der letzte Bereich beendet sie")]
    public void Zaehler_bei_wieder_geoeffnetem_profil()
    {
        var root = Tmp();
        try
        {
            var datei = Path.Combine(root, "bild.png");
            Assert.False(ChildWrite.InAllowedRoot(datei));
            var erstes = ChildWrite.TestScope(root);
            var zweites = ChildWrite.TestScope(root); // dasselbe Profil (derselbe Datenordner) noch einmal geöffnet
            Assert.Equal(2, ChildWrite.OpenScopes(root));
            erstes.Dispose();
            erstes.Dispose(); // doppeltes Schließen zählt nicht doppelt
            Assert.True(ChildWrite.InAllowedRoot(datei)); // das zweite Fenster ist noch offen
            Assert.Equal(1, ChildWrite.OpenScopes(root));
            zweites.Dispose();
            Assert.False(ChildWrite.InAllowedRoot(datei));
            Assert.Equal(0, ChildWrite.OpenScopes(root));
        }
        finally { Weg(root); }
    }

    [Fact(DisplayName = "T-0771 N2: ein Test-Bereich über einem dauerhaft freigegebenen Ort entfernt die dauerhafte Freigabe nicht")]
    public void Bereich_entfernt_dauerhafte_freigabe_nicht()
    {
        var root = Tmp();
        try
        {
            ChildWrite.AllowInside(root);
            using (ChildWrite.TestScope(root)) { }
            Assert.True(ChildWrite.InAllowedRoot(Path.Combine(root, "x.txt")));
        }
        finally { Weg(root); }
    }

    static void Altern(string root)
    {
        foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.SetCreationTimeUtc(f, DateTime.UtcNow.AddDays(-3));
    }

    [Fact(DisplayName = "T-0771 Prüfer-N1: Kind-Hauptprofil löscht rolle.json, settings.json und personen.json – nächster Start ist KEINE Ersteinrichtung, bleibt Kind")]
    public void Kind_loescht_rollendateien_bleibt_kind()
    {
        var root = Tmp();
        try
        {
            new UserSettings { Child = true, Name = "Kind" }.Save(root);
            Assert.True(ProfileRole.IsChild(root));
            Directory.CreateDirectory(Path.Combine(root, "audit")); File.WriteAllText(Path.Combine(root, "audit", "audit.jsonl"), "{}\n");
            Directory.CreateDirectory(Path.Combine(root, "projekte", "Mein Bild")); File.WriteAllText(Path.Combine(root, "projekte", "Mein Bild", "bild.txt"), "x");
            Altern(root); // die Dateien stammen aus früheren Läufen
            File.Delete(ProfileRole.FileOf(root)); File.Delete(UserSettings.PathIn(root));
            Assert.True(ProfileRole.IsChild(root));
            Assert.True(ProfileRole.IsChild(root)); // und bleibt es (rolle.json neu geschrieben)
            // Schon eine einzige alte Projektdatei reicht als Spur
            File.Delete(ProfileRole.FileOf(root)); File.Delete(Path.Combine(root, "audit", "audit.jsonl"));
            Assert.True(ProfileRole.IsChild(root));
        }
        finally { Weg(root); }
    }

    [Fact(DisplayName = "T-0771 Prüfer-N1: im selben Trendo-Lauf gelöschte Rollendatei macht ebenfalls keinen Erwachsenen")]
    public void Im_selben_lauf_geloescht_bleibt_kind()
    {
        var root = Tmp();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "audit")); File.WriteAllText(Path.Combine(root, "audit", "audit.jsonl"), "{}\n"); // wie der Kern beim Start
            new UserSettings { Child = true, Name = "Kind" }.Save(root);
            Assert.True(ProfileRole.IsChild(root));
            Thread.Sleep(20);
            File.Delete(ProfileRole.FileOf(root)); File.Delete(UserSettings.PathIn(root));
            Assert.True(ProfileRole.IsChild(root));
        }
        finally { Weg(root); }
    }

    [Fact(DisplayName = "T-0771 Prüfer-N1: echte Ersteinrichtung bleibt Besitzer – leerer Ordner, Dateien dieses Laufs, alte leere Unterordner")]
    public void Echte_ersteinrichtung_bleibt_besitzer()
    {
        var leer = Tmp(); var mitStart = Tmp(); var alteOrdner = Tmp(); var fehlt = Path.Combine(Path.GetTempPath(), "trendo-t0771-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.False(ProfileRole.IsChild(leer)); Assert.True(File.Exists(ProfileRole.FileOf(leer)));
            Assert.False(ProfileRole.IsChild(leer)); // bleibt Besitzer (rolle.json)
            // der Kern legt beim ersten Start schon Protokoll und Sicherheitsordner an, bevor die Rolle bestimmt wird
            Directory.CreateDirectory(Path.Combine(mitStart, "audit")); File.WriteAllText(Path.Combine(mitStart, "audit", "audit.jsonl"), "{}\n");
            Directory.CreateDirectory(Path.Combine(mitStart, "sicherheit")); File.WriteAllText(Path.Combine(mitStart, "sicherheit", "vertrauen.json"), "{}");
            Assert.False(ProfileRole.IsChild(mitStart));
            // früher angelegte, aber leere Ordner verhindern keine Ersteinrichtung
            var u = Path.Combine(alteOrdner, "werkzeuge", "leer"); Directory.CreateDirectory(u);
            Directory.SetCreationTimeUtc(u, DateTime.UtcNow.AddDays(-30)); Directory.SetCreationTimeUtc(alteOrdner, DateTime.UtcNow.AddDays(-30));
            Assert.False(ProfileRole.IsChild(alteOrdner));
            Assert.False(ProfileRole.IsChild(fehlt)); // Ordner gibt es noch gar nicht
            // Erwachsenen-Stand vor dem Umbau (settings.json alt) wird weiter einmal übernommen
            var vorher = Tmp(); new UserSettings { Name = "Erika Muster" }.Save(vorher); Altern(vorher);
            Assert.False(ProfileRole.IsChild(vorher)); Weg(vorher);
        }
        finally { Weg(leer); Weg(mitStart); Weg(alteOrdner); Weg(fehlt); }
    }
}

/// <summary>T-0771: Quelltext-Wächter – in der Oberfläche stellt Hintergrund-Arbeit nie über <c>Dispatcher.UIThread.Post</c> zu, sondern über
/// <c>UiThread.Post</c> (Grund siehe src/Trendo.App/UiThread.cs).</summary>
public class T0771DispatcherWaechter
{
    static string Repo() { var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!; return d; }

    [Fact(DisplayName = "T-0771: kein „Dispatcher.UIThread.Post(“ in src/Trendo.App – Hintergrund-Ergebnisse gehen über UiThread.Post")]
    public void Kein_direktes_post()
    {
        var hits = Directory.EnumerateFiles(Path.Combine(Repo(), "src", "Trendo.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && Path.GetFileName(f) != "UiThread.cs")
            .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, i, l)).Where(x => x.l.Contains("Dispatcher.UIThread.Post(", StringComparison.Ordinal)).Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}"))
            .ToList();
        Assert.True(hits.Count == 0, "Dispatcher.UIThread.Post statt UiThread.Post: " + string.Join(", ", hits));
    }
}
