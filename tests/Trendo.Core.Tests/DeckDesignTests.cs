using Trendo.Core.Office;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Präsentationen gestalten (STUDIO-DECKDESIGN-01): Prüfungen aus docs/nachbau/praesentation-gestalten/anforderungen.md.</summary>
public class DeckDesignTests
{
    const string Talk = "Energie sparen\n  - Kleine Schritte\nWarum\n  - Strom wird teurer\n  - Gut fürs Klima\nZahlen\n  - 30 % weniger Heizkosten\n  - 10 % weniger Strom\n"
        + "Plan\n  - 2025: Zähler ablesen\n  - 2026: Thermostate\n  - 2027: Solaranlage\nVorher und nachher\n  - Stand-by | Steckerleiste aus\n  - Fenster gekippt | Stoßlüften\n"
        + "Zum Schluss\n  - „Die billigste Energie ist die, die wir nicht verbrauchen.“ – Energieberatung\nTeil 2\nBild\n  - Unser Haus\nBild: gibtsnicht.png";

    [Fact(DisplayName = "DD-01: Gliederung lesen – Folien, Punkte, Notizen, Bild, feste Folienart")]
    public void Parse_outline()
    {
        var s = DeckDesign.Parse("# Start\n  - Untertitel\n> Begrüßen\nIdee [Zitat]\n- „Satz“\nNotiz: frei sprechen\nFoto\nBild: C:/x.png\n```\n");
        Assert.Equal(["Start", "Idee", "Foto"], s.Select(x => x.Title));
        Assert.Equal(["Untertitel"], s[0].Points); Assert.Equal(["Begrüßen"], s[0].Notes);
        Assert.Equal(DeckLayout.Zitat, s[1].Forced); Assert.Equal(["frei sprechen"], s[1].Notes);
        Assert.Equal("C:/x.png", s[2].Image);
    }

    [Fact(DisplayName = "DD-02: Folienart je Inhalt – Titel, Kennzahl, Zeitleiste, Zwei Spalten, Zitat, Abschnitt, Bild+Text, Stichpunkte")]
    public void Layout_by_content()
    {
        var r = DeckDesign.Build(Talk, DeckDesign.Theme("gold"));
        Assert.Equal([DeckLayout.Titel, DeckLayout.Stichpunkte, DeckLayout.Kennzahl, DeckLayout.Zeitleiste, DeckLayout.ZweiSpalten, DeckLayout.Zitat, DeckLayout.Abschnitt, DeckLayout.BildText], r.Layouts);
        Assert.False(DeckDesign.IsFigure("1. Schritt")); Assert.False(DeckDesign.IsFigure("2025: Start")); Assert.True(DeckDesign.IsFigure("1,2 Mio. Besucher"));
        // Kennzahl: Zahl groß in Akzentfarbe, Beschriftung darunter
        var k = r.Doc.Slides[2]; Assert.Contains(k.Shapes, s => s.Text == "30 %" && s.Color == r.Doc.Theme.Accent && s.FontSize >= 60);
        Assert.Contains(k.Shapes, s => s.Text == "weniger Heizkosten");
        // Zeitleiste: drei Punkte auf der Linie
        Assert.Equal(3, r.Doc.Slides[3].Shapes.Count(s => s.Kind == ShapeKind.Ellipse));
        // Zitat mit Quelle
        Assert.Contains(r.Doc.Slides[5].Shapes, s => s.Role == "sub" && s.Text.Contains("Energieberatung"));
        // Zwei Spalten aus „links | rechts“
        Assert.Equal("Steckerleiste aus\nStoßlüften", r.Doc.Slides[4].ByRole("body2")!.Text);
        // Bild fehlt → Platzhalter + Hinweis
        Assert.NotNull(r.Doc.Slides[7].ByRole("pic")); Assert.Contains(r.Findings, f => f.Kind == "bild");
    }

    [Fact(DisplayName = "DD-03: Regel höchstens 6 Punkte – 9 Punkte werden auf 2 Folien verteilt; zu lange Punkte geteilt, gekürzter Satz in die Notizen")]
    public void Rules_split()
    {
        var outline = "Titel\nViele Punkte\n" + string.Join("\n", Enumerable.Range(1, 9).Select(i => $"  - Punkt {i}"));
        var r = DeckDesign.Build(outline, DeckDesign.Theme("klar"));
        Assert.Equal(3, r.Doc.Slides.Count); Assert.Equal("Viele Punkte (Forts.)", r.Doc.Slides[2].Title);
        Assert.Equal(5, r.Doc.Slides[1].ByRole("body")!.Text.Split('\n').Length); Assert.Equal(4, r.Doc.Slides[2].ByRole("body")!.Text.Split('\n').Length);
        Assert.Contains(r.Findings, f => f.Kind == "aufgeteilt" && f.Slide == 2);
        var longSentence = string.Join(' ', Enumerable.Repeat("Wort", 40));
        var parts = DeckDesign.SplitPoint("Erster Satz ist kurz. " + longSentence, 90, out var sh);
        Assert.True(sh); Assert.All(parts, p => Assert.True(p.Length <= 92)); Assert.Equal("Erster Satz ist kurz.", parts[0]);
        var r2 = DeckDesign.Build("T\nFolie\n  - " + longSentence, DeckDesign.Theme("klar"));
        Assert.Contains(longSentence, r2.Doc.Slides[1].Notes); Assert.Contains(r2.Findings, f => f.Kind == "gekürzt");
        // eigene Grenze: 3 Punkte
        Assert.Equal(4, DeckDesign.Build(outline, DeckDesign.Theme("klar"), new DeckOptions(MaxPoints: 3)).Doc.Slides.Count);
    }

    [Fact(DisplayName = "DD-04: Kontrast-Prüfung nach WCAG – Weiß/Schwarz 21:1, alle Design-Themen lesbar, schlechtes Design wird repariert")]
    public void Contrast()
    {
        Assert.Equal(21, DeckDesign.Contrast("#FFFFFF", "#000000"), 1);
        Assert.Equal(1, DeckDesign.Contrast("#777777", "#777777"), 3);
        Assert.All(DeckDesign.Themes, t => Assert.Empty(DeckDesign.CheckTheme(t)));
        var bad = new SlideTheme("x", "Grau", "#FFFFFF", "#DDDDDD", "#CCCCCC", "#EEEEEE");
        Assert.Equal(3, DeckDesign.CheckTheme(bad).Count);
        var fixedT = DeckDesign.FixContrast(bad);
        Assert.Empty(DeckDesign.CheckTheme(fixedT)); Assert.Equal("#FFFFFF", fixedT.Background);
        Assert.True(DeckDesign.Contrast(fixedT.Text, "#FFFFFF") >= 4.5);
        var r = DeckDesign.Build("A\nB\n  - x", bad); Assert.Contains(r.Findings, f => f.Kind == "kontrast");
        Assert.Throws<FormatException>(() => DeckDesign.Luminance("blau"));
    }

    [Fact(DisplayName = "DD-05: Schriftgröße passt sich an – viel Text kleiner, aber nie unter 14 pt; zu viel Text wird gemeldet")]
    public void Fit_size()
    {
        var (big, ok1) = DeckDesign.FitSize("Kurz", 29.2, 12.8, 28);
        Assert.Equal(28, big); Assert.True(ok1);
        var (small, ok2) = DeckDesign.FitSize(string.Join('\n', Enumerable.Repeat(new string('x', 80), 6)), 29.2, 12.8, 28, bullets: true);
        Assert.True(small < 28 && small >= 14); Assert.True(ok2);
        var (_, ok3) = DeckDesign.FitSize(new string('x', 5000), 29.2, 12.8, 28);
        Assert.False(ok3);
    }

    [Fact(DisplayName = "DD-06: Sprechernotizen (eigene bleiben, fehlende vorgeschlagen) und Zeitplan auf die Redezeit verteilt")]
    public void Notes_and_schedule()
    {
        var r = DeckDesign.Build("Start\nPunkte\n  - a\n  - b\n> Eigene Notiz\nMehr\n  - c", DeckDesign.Theme("gold"), new DeckOptions(Minutes: 10));
        Assert.Equal("Eigene Notiz", r.Doc.Slides[1].Notes);
        Assert.StartsWith("Mehr:", r.Doc.Slides[2].Notes);
        Assert.Equal(10, r.Minutes.Sum(), 0);
        Assert.True(r.Minutes[1] > r.Minutes[0]);
        Assert.Equal("0:00", DeckDesign.Clock(r.Minutes)[0]);
        var off = DeckDesign.Build("Start\nPunkte\n  - a", DeckDesign.Theme("gold"), new DeckOptions(AutoNotes: false));
        Assert.Equal("", off.Doc.Slides[1].Notes);
        Assert.Contains("Folienplan", DeckDesign.PlanText(r)); Assert.Contains("[0:00", DeckDesign.PlanText(r));
    }

    [Fact(DisplayName = "DD-07: Export PPTX über den vorhandenen Kern – Folien, Notizen und Titel kommen zurück; PDF mit einer Seite je Folie")]
    public void Export_pptx_pdf()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-dd-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            var r = DeckDesign.Build(Talk, DeckDesign.Theme("papier"));
            var pptx = Path.Combine(dir, "a.pptx"); SlideFiles.Save(r.Doc, pptx);
            var back = SlideFiles.Open(pptx);
            Assert.Equal(r.Doc.Slides.Count, back.Slides.Count);
            Assert.Equal(r.Doc.Slides.Select(s => s.Title), back.Slides.Select(s => s.Title));
            Assert.Equal(r.Doc.Slides[1].Notes, back.Slides[1].Notes);
            var pdf = Path.Combine(dir, "a.pdf"); Assert.Equal(r.Doc.Slides.Count, SlidePdf.Export(r.Doc, pdf, true));
            Assert.True(new FileInfo(pdf).Length > 1000);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "DD-08: Von Hand gewählte Folienart gilt, Gliederung aus vorhandener Präsentation, leere Gliederung meldet sich")]
    public void Overrides_and_roundtrip()
    {
        var r = DeckDesign.Build(Talk, DeckDesign.Theme("gold"), null, new Dictionary<int, DeckLayout> { [1] = DeckLayout.ZweiSpalten });
        Assert.Equal(DeckLayout.ZweiSpalten, r.Layouts[1]); Assert.NotNull(r.Doc.Slides[1].ByRole("body2"));
        var outline = DeckDesign.FromDeck(SlideOutline.FromOutline("Titel\n  - Sub\nFolie\n  - a\n  - b\n> Notiz"));
        var again = DeckDesign.Parse(outline);
        Assert.Equal(["Titel", "Folie"], again.Select(s => s.Title)); Assert.Equal(["a", "b"], again[1].Points); Assert.Equal(["Notiz"], again[1].Notes);
        Assert.Throws<InvalidOperationException>(() => DeckDesign.Build("  \n", DeckDesign.Theme("gold")));
        Assert.Contains("Zeitleiste", DeckDesign.Prompt("Thema", 6, 10)); Assert.Contains("10 Minuten", DeckDesign.Prompt("Thema", 6, 10));
    }

    [Fact(DisplayName = "STUDIO-DECKDESIGN-01 Adapter: PPTX und Folienplan im Projekt – ohne Nebenwirkung, Pflichtfeld Gliederung")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-DECKDESIGN-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-DECKDESIGN-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["gliederung"] = "x", ["design"] = "bunt" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["gliederung"] = "x", ["minuten"] = "0" }));
        var root = Path.Combine(Path.GetTempPath(), "trendo-dd-ad-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(root);
        try
        {
            var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
            var project = core.Projects.Create("probe", "Probe").Id;
            var wf = new StudioWorkflow(core, all);
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("dd-1", project, "STUDIO-DECKDESIGN-01", "Design: minimal\nMinuten: 5\nGliederung:\nUnser Fest\n- Sommer im Hof\nZahlen\n- 120 Gäste\n- 40 Kuchen\nAblauf\n- 2026 – Probe\n- 2027 – Start\n- 2028 – Ausbau", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var plan = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("folienplan.txt")).RelativePath));
            Assert.Contains("3 Folien", plan); Assert.Contains("Kennzahlen: Zahlen", plan); Assert.Contains("Zeitleiste: Ablauf", plan);Assert.Contains("Minimal", plan);
            var pptx = core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith(".pptx")).RelativePath);
            Assert.Equal(3, SlideFiles.Open(pptx).Slides.Count);
        }
        finally { Directory.Delete(root, true); }
    }
}
