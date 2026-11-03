using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// Paket F2 (freigegeben 02.11.2026, A2 Vorbild-Recherche): jedes Arbeitsprogramm hat eine Vorbild-Analyse (Clean-Room) mit ehrlichem Stand.
public class VorbildAnalysenTests
{
    static string Repo() { var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!; return d; }

    /// <summary>Arbeitsprogramme (keine Einstellungs- oder Systemseiten) – für jedes muss eine Analyse da sein.</summary>
    static readonly string[] WorkPrograms =
    [
        "Briefe schreiben", "Trendo Text", "Trendo Tabelle", "Trendo Präsentation", "Trendo PDF", "E-Mail & Kontakte", "Posteingang", "Kalender", "Aufgaben", "Adressbuch",
        "Medien", "Geld", "Verkaufen", "Bildbearbeitung", "Medien bearbeiten", "Videoschnitt", "Live streamen", "TV & Live", "Kleidung & Produkte", "KI-Vorlagen",
        "3D", "Trendo CAD", "Trendo 3D-Druck", "Aus Fotos", "Punktwolke", "Spiele bauen", "Werkstatt", "VR & AR gestalten", "Grundriss & Wohnen", "Schaltung & Simulation",
        "Tragwerk & Mechanik", "Geräte überwachen", "Karten & Touren", "CNC-Werkstatt", "Auto & Fahrten", "Kameras & Roboter steuern", "Messdaten & Auswertung",
        "Lernen mit Karteikarten", "Wissen für die KI", "Recherche im Lesemodus", "KI fragen", "Abläufe (Automatisch erledigen)", "Produktion", "Software", "Baukasten",
    ];

    [Fact(DisplayName = "Vorbild-Analysen: jedes Arbeitsprogramm hat eine Analyse (Clean-Room) mit Funktionsliste ✓/◐/✗, Bedienkonzept und nächsten Schritten; nur echte Programmnamen")]
    public void Every_work_program_has_analysis()
    {
        var dir = Path.Combine(Repo(), "docs", "claude", "vorbilder");
        var covered = new HashSet<string>(StringComparer.Ordinal);
        var known = JsonNode.Parse(File.ReadAllText(Path.Combine(Repo(), "docs", "programme", "katalog.json")))!.AsArray().Select(x => (string)x!["Name"]!).ToHashSet(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(dir, "*.md"))
        {
            var t = File.ReadAllText(f); var names = ProgramCatalog.AnalysisPrograms(t);
            Assert.True(names.Count > 0, Path.GetFileName(f) + ": Zeile „- Programme:“ fehlt");
            Assert.Contains("Clean-Room", t);
            foreach (var n in names) Assert.True(known.Contains(n), $"{Path.GetFileName(f)}: unbekanntes Programm „{n}“");
            covered.UnionWith(names);
        }
        var missing = WorkPrograms.Where(p => !covered.Contains(p)).ToList();
        Assert.True(missing.Count == 0, "Ohne Vorbild-Analyse: " + string.Join(", ", missing));
        foreach (var f in Directory.GetFiles(dir, "*.md").Where(f => File.ReadAllText(f).Contains("## Funktionen eines Profi-Programms")))
        {
            var t = File.ReadAllText(f);
            Assert.True(t.Split('\n').Count(l => l.StartsWith("- ✓") || l.StartsWith("- ◐") || l.StartsWith("- ✗")) >= 3, Path.GetFileName(f));
            Assert.Contains("## Nächste Schritte", t); Assert.Contains("## Bedienkonzept", t);
        }
    }

    [Fact(DisplayName = "Vorbild-Analyse gilt nur für die genannten Programme – nicht mehr für das ganze Studio")]
    public void Analysis_matches_by_program_line()
    {
        var names = ProgramCatalog.AnalysisPrograms("# X\n\n- Programme: „Trendo Text“, „Briefe schreiben“\n- Vorbilder: Word\nauch „Texte & Tabellen“ erwähnt");
        Assert.Equal(["Briefe schreiben", "Trendo Text"], names.Order());
        Assert.Empty(ProgramCatalog.AnalysisPrograms("# ohne Zeile „Trendo Text“"));
    }
}
