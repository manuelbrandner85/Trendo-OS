using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764 (12. Prüfrunde): Tests, die die Freigabeliste (globaler Zustand) im Bereich ändern, laufen nicht parallel.</summary>
[CollectionDefinition("ChildWrite", DisableParallelization = true)]
public sealed class ChildWriteCollection;

/// <summary>T-0764, 12. Prüfrunde: Schreibziele mit Ort oder Namen vom Nutzer laufen für Kinder über ChildWrite (M) – Quelltext-Wächter;
/// Freigaben nur im Bereich (N1). Schreibt nie in echte Systemorte.</summary>
[Collection("ChildWrite")]
public class RechteKernFix12
{
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    /// <summary>Durchgesehen und bewusst ohne ChildWrite – jeweils mit Grund (Ziel und Name legt Trendo fest, oder die Methode liest nur).</summary>
    static readonly Dictionary<string, string> Intern = new()
    {
        ["AceStepClient.DownloadAsync"] = "Ziel in Trendos KI-Ordner, Name von Trendo",
        ["DiskMapView.Move"] = "Kartenansicht, kein Dateizugriff",
        ["ImageRender.Export"] = "Aufrufer (Export-Liste) mit festem Ordner/fester Endung oder geprüfter Auswahl",
        ["ImageTools.Apply"] = "schreibt neben die gewählte Mediendatei, feste Endung",
        ["MainWindow.AudioPluginHost.AuPluginInspect"] = "liest nur",
        ["MainWindow.Claude.ClaudeOpen"] = "nur Erwachsene (DarfAusfuehren), Protokoll in .trendo",
        ["MainWindow.Claude.ClaudeRunProcess"] = "nur Erwachsene (DarfAusfuehren)",
        ["MainWindow.ComicStudio.ComicFrames"] = "Trendos eigener Ordner, Namen von Trendo",
        ["MainWindow.GameLibrary.LibSetGodot"] = "Einstellung in Trendos Datenordner",
        ["MainWindow.MusikTools.MsImport"] = "liest die gewählte Datei, schreibt in Trendos Musikordner",
        ["MainWindow.Spec05.SheetChartPng"] = "Aufrufer (Export-Liste) mit festem Ordner/fester Endung",
        ["MainWindow.Spec10Alarm.TwinImportRules"] = "liest nur",
        ["MainWindow.Spec10Alarm.TwinAddFileSource"] = "liest nur",
        ["MainWindow.Studio3DEngines.ExportForEngine"] = "„target“ ist das Ziel-Engine-Kürzel, Ausgabe in Trendos 3D-Ordner",
        ["MainWindow.Sys02.Play"] = "startet den Player, schreibt nichts (Arbeitsordner von Trendo)",
        ["MainWindow.Tabelle.RememberSheetRecent"] = "Liste „zuletzt“ in Trendos Datenordner",
        ["MainWindow.Text.RememberRecent"] = "Liste „zuletzt“ in Trendos Datenordner",
        ["MainWindow.W4Game.GameEditor"] = "nur Erwachsene (Konsolen-Programm)",
        ["MainWindow.W4Builder.BuilderOpenExport"] = "T-0768 (aus T-0765): liest die gewählte ZIP/index.html; schreibt nur Bilder mit Bild-Endung (ImportImageTypes) in Trendos Baukasten-Ordner (WorkFolder über ProjectsBase), Unterordner von Trendo",
        ["MainWindow.WebDesign3D.WebDesignModelImport"] = "liest, kopiert in Trendos Webdesign-Ordner",
        ["MainWindow.WebDesignAssets.WebShrinkImage"] = "Aufrufer übergibt Trendos eigene Kopie",
        ["MainWindow.axaml.SaveScreenshot"] = "Startoption --screenshot (Kommandozeile, nicht Kinderprofil)",
        ["MapCanvas.TileCache"] = "Kartenkacheln in Trendos Zwischenspeicher",
        ["PdfView.SavePng"] = "Aufrufer: Seitenbild für die Texterkennung in Trendos Ordner",
        ["ScreenGrab.SavePng"] = "Aufrufer: Bildschirmfoto mit festem Namen im Bilder-Ordner",
        ["TrendoUpdater.BackupCurrent"] = "Update (nur Erwachsene)",
        ["TrendoUpdater.RollbackTo"] = "Update (nur Erwachsene)",
        ["VollpaketStart.RememberFromInstaller"] = "Einrichtung (nur Erwachsene)",
    };

    [Fact(DisplayName = "T-0764 M (Wächter, Heuristik): jede App-Methode mit Pfad-/Datei-/Ordner-Parameter, die schreibt, ruft UserFile/UserDir/ChildWrite – oder ist begründet ausgenommen")]
    public void Schreibziele_geprueft()
    {
        var hdr = new Regex(@"^\s*(public|internal|private|protected)[^=;(]*?\s(\w+)\s*\(([^)]*)\)\s*(=>.*|\{?\s*)$");
        var par = new Regex(@"\bstring\??\s+(path|file|target|folder|dir|relative|outDir|outFile|dest|ziel)\b");
        var wr = new Regex(@"File\.(WriteAll|AppendAll|Copy|Move|Create|Replace)|new FileStream\(|Directory\.(CreateDirectory|Move)|WriteText\(|ExtractToFile|AtomicFile\.|\.Save\(|SaveAs\(|Export[A-Za-z]*\(|Unpack");
        var ok = new Regex(@"UserFile\(|UserDir\w*\(|ChildWrite\.|ProtectedFolders\.");
        var bad = new List<string>(); var seen = new HashSet<string>();
        foreach (var f in Directory.EnumerateFiles(Repo("src/Trendo.App"), "*.cs"))
        {
            var lines = File.ReadAllLines(f); var cls = Path.GetFileNameWithoutExtension(f);
            for (var i = 0; i < lines.Length; i++)
            {
                var m = hdr.Match(lines[i]);
                if (!m.Success || !par.IsMatch(m.Groups[3].Value) || m.Groups[2].Value is "if" or "for" or "foreach" or "while" or "switch" or "catch" or "using") continue;
                var body = lines[i]; var j = i;
                if (m.Groups[4].Value.StartsWith("=>", StringComparison.Ordinal)) { while (!body.TrimEnd().EndsWith(';') && j + 1 < lines.Length) body += "\n" + lines[++j]; }
                else { var depth = 0; var started = false; body = ""; for (; j < lines.Length; j++) { body += lines[j] + "\n"; depth += lines[j].Count(c => c == '{') - lines[j].Count(c => c == '}'); if (lines[j].Contains('{')) started = true; if (started && depth <= 0) break; } }
                var name = cls + "." + m.Groups[2].Value; seen.Add(name);
                if (wr.IsMatch(body) && !ok.IsMatch(body) && !Intern.ContainsKey(name)) bad.Add($"{name} ({Path.GetFileName(f)}:{i + 1})");
                i = j;
            }
        }
        Assert.True(bad.Count == 0, "ohne ChildWrite: " + string.Join(", ", bad));
        Assert.All(Intern.Keys, k => Assert.Contains(k, seen)); // Ausnahmeliste bleibt aktuell (keine verwaisten Einträge)
        // die Stellen aus dem Befund
        var code = File.ReadAllText(Repo("src/Trendo.App/MainWindow.CodeEditor.cs"));
        foreach (var m in new[] { "public void CodeNewFile", "public void CodeSave", "public void CodeOpenFolder" })
        {
            var at = code.IndexOf(m, StringComparison.Ordinal); Assert.True(at >= 0, m);
            var end = code.IndexOf("\n    }", at, StringComparison.Ordinal);
            Assert.Matches(new Regex(@"UserFile\(|UserDir\w*\("), code[at..end]);
        }
    }

    [Fact(DisplayName = "T-0764 N1: TestScope – Freigabe nur im Bereich, danach wieder entfernt")]
    public void Bereich()
    {
        using var f = new Fixture();
        var root = Path.Combine(f.Root, "eigen"); Directory.CreateDirectory(root);
        var file = Path.Combine(root, "bild.png");
        Assert.NotNull(ChildWrite.Problem(Principal.ChildProfile, file));
        using (ChildWrite.TestScope(root)) Assert.Null(ChildWrite.Problem(Principal.ChildProfile, file));
        Assert.NotNull(ChildWrite.Problem(Principal.ChildProfile, file));
        Assert.DoesNotContain(ChildWrite.Roots(), r => r.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }
}
