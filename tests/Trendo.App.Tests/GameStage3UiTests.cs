using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0650: Spiele-Studio Stufe 3 in der Oberfläche – Engine einrichten, Bildprüfung und Laufvergleich, Dienste mit Lizenz, ähnliche Spiele mit Beleg.</summary>
public class GameStage3_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static void BlackPng(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, VisionPlus.EncodePng(new RgbImage(64, 36, new byte[64 * 36 * 3])));
    }

    [AvaloniaFact(DisplayName = "E5 + E6: Testlauf mit Startwert, schwarzes Bild wird gemeldet, zweiter Lauf meldet die Verschlechterung")]
    public async Task Runs_and_shots()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-openworld").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        w.Find<ComboBox>("game-world").SelectedIndex = 0; Pump(); // einfaches Level, ohne Welt-Einrichtung
        w.Find<Button>("game-build").Press(); Pump();
        var built = d.Text("built");
        var strecke = "31.5"; var startArgs = new List<IReadOnlyList<string>>();
        w.UnrealEngineOverride = "C:/UE";
        w.UnrealRunOverride = (exe, args) =>
        {
            if (exe.EndsWith("Build.bat")) return (0, "Result: Succeeded");
            if (args.Any(a => a.Contains("Automation RunTests"))) return (0, "Test Completed. Result={Success} Name={Daten} Path={Trendo.Daten}");
            startArgs.Add(args);
            BlackPng(Path.Combine(built, "Saved", "Screenshots", "trendo-kampf.png")); // das Spiel schreibt sein Bild in diesem Lauf
            return (0, $"TRENDO-START Startwert=1\nTRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=40 km/h Strecke={strecke} m Raddrehung=336° (Reifen drehen sich) Gang=2\nTRENDO-WAGEN ausgestiegen, Figur=sichtbar\nTRENDO-OK Spiel läuft");
        };
        try
        {
            var old = Path.Combine(built, "Saved", "Screenshots", "trendo-wagen.png"); BlackPng(old); File.SetLastWriteTime(old, DateTime.Now.AddDays(-1)); // altes Bild eines früheren Laufs
            await w.GameUnrealCheck(d, screenshot: true); Pump();
            Assert.DoesNotContain("trendo-wagen.png", w.LastUnrealReport); // alte Bilder zählen nicht
            Assert.StartsWith("Nicht alles ging", w.LastUnrealReport); // schwarzes Bild: Lauf nicht bestanden
            var seedArg = Assert.Single(startArgs[0], a => a.StartsWith("-trendoseed=", StringComparison.Ordinal));
            Assert.Contains("Bildprüfung: 1 Befund", w.LastUnrealReport);
            Assert.Contains("Kampf (trendo-kampf.png): Bild ist schwarz", w.LastUnrealReport);
            Assert.Contains("Erster Lauf", w.LastUnrealReport);
            Assert.Contains(GameBoard.Tasks(d.Data), t => ((string?)t["title"] ?? "").StartsWith("Bild Kampf", StringComparison.Ordinal));
            // zweiter Lauf, gleicher Startwert, Wagen kommt kaum noch weit
            strecke = "4";
            await w.GameUnrealCheck(d, screenshot: true); Pump();
            Assert.Equal(seedArg, Assert.Single(startArgs[1], a => a.StartsWith("-trendoseed=", StringComparison.Ordinal)));
            Assert.NotNull(w.LastRunComparison); Assert.True(w.LastRunComparison!.Comparable);
            Assert.True(w.LastRunComparison.Worse >= 2); // Fahrzeug ✓ → ⚠ und Strecke
            Assert.Contains("⚠ WAGEN.Strecke: 31,5 → 4", w.LastUnrealReport);
            Assert.Equal(2, GameRuns.List(MainWindow.GameRunsDir(built)).Count);
            Assert.Single(GameBoard.Tasks(d.Data), t => (string?)t["title"] == "Bild Kampf: schwarzes Bild"); // gleicher Befund legt keine zweite Aufgabe an
            w.Show(GK); Pump();
            Assert.Contains(seedArg["-trendoseed=".Length..], w.Find<TextBlock>("game-run-seed").Text);
            w.Find<Button>("game-run-newseed").Press(); Pump();
            Assert.DoesNotContain(seedArg["-trendoseed=".Length..] + " ", w.Find<TextBlock>("game-run-seed").Text);
        }
        finally { w.UnrealRunOverride = null; w.UnrealEngineOverride = null; }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "E1: Ohne Engine genau ein Schritt zur offiziellen Seite; mit Engine und bestandenem Bericht „Alles geprüft“")]
    public void Engine_setup_card()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-openworld").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        Assert.Contains("offiziellen Seite", w.Find<TextBlock>("game-engine-next").Text);
        Assert.StartsWith("○ Engine erkannt", w.Find<TextBlock>("game-engine-step-erkannt").Text);
        Assert.Equal("Offizielle Seite öffnen", w.Find<Button>("game-engine-next-go").Content);
        w.EngineProbeOverride = () => [new("unreal", "Unreal Engine", "5.8", "C:/UE/Engine/Binaries/Win64/UnrealEditor.exe", true, "bereit", "")];
        try
        {
            w.Show(GK); Pump();
            Assert.Equal("Bauen", w.Find<Button>("game-engine-next-go").Content);
            w.Find<Button>("game-build").Press(); Pump();
            Assert.Equal("Öffnen und bauen prüfen", w.Find<Button>("game-engine-next-go").Content);
            var report = Path.Combine(d.Text("built"), "Trendo-Pruefung", "bericht.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report, "2026-11-14 10:00 level\nNicht alles ging – Details unten.\n✗ Kompilieren fehlgeschlagen (Exitcode 6)\n");
            w.Show(GK); Pump();
            Assert.StartsWith("✗ Öffnen und Bauen: fehlgeschlagen", w.Find<TextBlock>("game-engine-step-pruefung").Text);
            File.WriteAllText(report, "2026-11-14 10:05 level\nAlles bereit – „Spielen“ startet dein 3D-Spiel.\n✓ Kompiliert (Exitcode 0)\n");
            w.Show(GK); Pump();
            Assert.Equal("Alles geprüft.", w.Find<TextBlock>("game-engine-next").Text);
            Assert.Null(w.TryFind<Button>("game-engine-next-go"));
            // erneut gebaut: der alte Bericht gilt nicht mehr
            File.SetLastWriteTimeUtc(report, DateTime.UtcNow.AddMinutes(-5));
            w.Find<Button>("game-build").Press(); Pump();
            Assert.Contains("älter als der letzte Bau", w.Find<TextBlock>("game-engine-step-pruefung").Text);
        }
        finally { w.EngineProbeOverride = null; }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "E7: Dienste-Auswahl je Art; Erzeugtes landet mit Herkunft und Lizenz in der Asset-Liste")]
    public async Task Services_with_license()
    {
        var (w, _, _) = Ui.Open();
        var d = w.ProgramNew(GK, "topdown");
        w.GameServiceReadyOverride = id => id == "ki-bild";
        MainWindow.KiToolOverride = (kind, input, target) => { BlackPng(target); return Task.FromResult(kind == "bild"); };
        try
        {
            w.GameSection = "Produktion"; w.Show(GK); Pump(); // Top-down hat keinen Assets-Bereich, die Dienste stehen unter Produktion
            Assert.StartsWith("Bilder: Bild-KI auf dem PC", ((TextBlock)w.Find<StackPanel>("game-svc-bild").Children[0]).Text);
            Assert.StartsWith("3D-Modelle: Poly Haven", ((TextBlock)w.Find<StackPanel>("game-svc-3d").Children[0]).Text);
            Assert.Contains("in der KI-Bibliothek", w.Find<TextBlock>("game-svc-sprache-hint").Text);
            await w.GameServiceMake(d, GameServices.All.First(p => p.Id == "ki-bild"), "Heldin mit Laterne"); Pump();
            var a = Assert.Single(d.List("assets").OfType<JsonObject>());
            Assert.StartsWith("Bild-KI auf dem PC", (string?)a["source"]);
            Assert.Contains("Lizenz", (string?)a["license"]);
            Assert.Empty(GameServices.MissingLicense(d.Data));
            Assert.StartsWith("✓", w.Find<TextBlock>("game-svc-msg").Text);
        }
        finally { MainWindow.KiToolOverride = null; w.GameServiceReadyOverride = null; }
        w.Close();
    }

    sealed class Wiki(bool known) : IWebFetch
    {
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default)
        {
            if (known && url == GameReferences.SummaryUrl("de", "Stardew Valley"))
                return Task.FromResult(new WebReply(200, System.Text.Encoding.UTF8.GetBytes("""{"type":"standard","title":"Stardew Valley","description":"Computerspiel von 2016","extract":"","content_urls":{"desktop":{"page":"https://de.wikipedia.org/wiki/Stardew_Valley"}}}""")));
            return Task.FromResult(new WebReply(404, []));
        }
    }

    sealed class Down : IWebFetch { public Task<WebReply> GetAsync(string url, CancellationToken ct = default) => Task.FromResult(new WebReply(503, [])); }

    [AvaloniaFact(DisplayName = "D5: Ähnliche Spiele zeigt nur belegte Titel mit Quelle; ohne Beleg „keine Referenz gefunden“")]
    public async Task References_card()
    {
        var (w, _, _) = Ui.Open();
        var d = w.ProgramNew(GK, "topdown");
        MainWindow.GamePromptOverride = (_, _) => Task.FromResult("Stardew Valley\nGarten der Zeit (erfunden)");
        try
        {
            w.GameRefFetchOverride = new Wiki(true);
            await w.GameReferencesRun(d);
            w.GameSection = "Spielbibel"; w.Show(GK); Pump();
            Assert.NotNull(w.Find<DockPanel>("game-ref-0"));
            Assert.Null(w.TryFind<DockPanel>("game-ref-1"));
            Assert.Equal("Stardew Valley", Assert.Single(GameReferences.Read(d.Data)).Title);
            w.GameRefFetchOverride = new Down(); // Wikipedia nicht erreichbar: nichts überschreiben
            await w.GameReferencesRun(d); w.Show(GK); Pump();
            Assert.Single(GameReferences.Read(d.Data));
            Assert.Null(w.TryFind<TextBlock>("game-refs-none"));
            w.GameRefFetchOverride = new Wiki(false);
            await w.GameReferencesRun(d); w.Show(GK); Pump();
            Assert.Equal(GameReferences.None, w.Find<TextBlock>("game-refs-none").Text);
            Assert.Null(w.TryFind<DockPanel>("game-ref-0"));
        }
        finally { MainWindow.GamePromptOverride = null; w.GameRefFetchOverride = null; }
        w.Close();
    }
}
