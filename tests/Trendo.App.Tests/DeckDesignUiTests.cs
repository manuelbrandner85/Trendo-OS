using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Präsentationen gestalten (STUDIO-DECKDESIGN-01): echte Klicks – Vorlage, Gliederung, Folienleiste, Design und Kontrast, Folienart, Export, Übergabe, KI-Entwurf.</summary>
public class DeckDesign_Ui
{
    const string K = MainWindow.DeckDesignKey;
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static ProgramDocument Open(MainWindow w, string tpl = "vortrag")
    {
        w.ProgramSetLevel(UserLevel.Profi); w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(K)!;
    }
    static string Export(MainWindow w, string name, string root)
    {
        var e = MainWindow.Programs[K].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(K)!, Path.Combine(root, "export-" + name + e.Extension));
    }

    [AvaloniaFact(DisplayName = "Präsentationen gestalten: eigene Seite, Vorlage zeigt gestaltete Folien mit Folienleiste; Klick wählt die Folie")]
    public void Page_and_strip()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1500; w.Height = 950;
        try
        {
            Assert.Equal(K, MainWindow.StudioPages["STUDIO-DECKDESIGN-01"]);
            var d = Open(w);
            var r = w.DdBuild(d);
            Assert.Contains(DeckLayout.Kennzahl, r.Layouts); Assert.Contains(DeckLayout.Zeitleiste, r.Layouts); Assert.Contains(DeckLayout.Zitat, r.Layouts); Assert.Contains(DeckLayout.ZweiSpalten, r.Layouts);
            Assert.NotNull(w.Find<SlideView>("dd-preview"));
            for (var i = 0; i < r.Doc.Slides.Count; i++) Assert.NotNull(w.Find<Button>("dd-thumb-" + i));
            Tap(w, "dd-thumb-2");
            Assert.Equal(2, w.DdSelected(d));
            Assert.StartsWith("Folie 3 von", w.Find<TextBlock>("dd-slide-info").Text);
            Assert.Equal(r.Doc.Slides[2], w.Find<SlideView>("dd-preview").Slide);
            Tap(w, "prog-cmd-dd-next"); Assert.Equal(3, w.DdSelected(d));
            Assert.Equal(10, r.Minutes.Sum(), 0); // Vorlage: 10 Minuten Redezeit
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "DD-03/07/09: Gliederung tippen, „Gestalten“ – Regel teilt 8 Punkte; Export PPTX/PDF/Handzettel/Plan; Übergabe an Trendo Präsentation")]
    public void Build_export_handover()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1500; w.Height = 950; Directory.CreateDirectory(root);
        try
        {
            var d = Open(w, "leer");
            var box = w.Find<TextBox>("dd-outline");
            box.Text = "Mein Vortrag\n  - Heute\nAcht Punkte\n" + string.Join("\n", Enumerable.Range(1, 8).Select(i => $"  - Punkt {i}")) + "\n> Langsam sprechen";
            Tap(w, "dd-build-now");
            var r = w.DdBuild(d);
            Assert.Equal(3, r.Doc.Slides.Count); Assert.Equal("Acht Punkte (Forts.)", r.Doc.Slides[2].Title);
            Assert.Equal("Langsam sprechen", r.Doc.Slides[1].Notes);
            Assert.Contains("3 Folien gestaltet", w.ProgramMessage(K));
            Assert.NotNull(w.Find<StackPanel>("dd-findings"));
            var pptx = Export(w, "pptx", root); Assert.Equal(3, SlideFiles.Open(pptx).Slides.Count);
            var pdf = Export(w, "pdf", root); Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(pdf), 0, 4));
            Assert.True(new FileInfo(Export(w, "handzettel", root)).Length > new FileInfo(pdf).Length / 2);
            Assert.Contains("Folienplan", File.ReadAllText(Export(w, "plan", root)));
            Tap(w, "prog-cmd-dd-handover");
            Assert.Equal(3, w.Deck.Slides.Count); Assert.Equal("Mein Vortrag", w.Deck.Slides[0].Title); Assert.Equal("praesentation", w.CurrentView);
            Assert.Equal(r.Doc.Theme.Id, w.Deck.Theme.Id);
            // zurückholen: Gliederung aus der offenen Präsentation
            w.Show(K); Settle(); Tap(w, "prog-cmd-dd-import");
            Assert.Contains("Acht Punkte", d.Text("outline"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "DD-04: Design wechseln, eigene Farbe mit schlechtem Kontrast wird gemeldet und per Klick repariert")]
    public void Theme_and_contrast()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1500; w.Height = 950;
        try
        {
            var d = Open(w, "pitch");
            Assert.Equal("minimal", d.Text("theme"));
            var combo = w.Find<ComboBox>("dd-theme"); combo.SelectedIndex = DeckDesign.Themes.ToList().FindIndex(t => t.Id == "papier"); Settle();
            Assert.Equal("papier", d.Text("theme")); Assert.Equal("Georgia", w.DdBuild(d).Doc.Theme.Font);
            var text = w.Find<TextBox>("dd-color-text"); text.Text = "#E5E0D8"; text.Focus(); w.KeyPressQwerty(Avalonia.Input.PhysicalKey.Enter, Avalonia.Input.RawInputModifiers.None); Settle();
            Assert.Equal("#E5E0D8", d.Text("text"));
            Assert.Contains(w.DdBuild(d).Findings, f => f.Kind == "kontrast");
            Assert.Contains(w.Find<StackPanel>("dd-contrast").Children.OfType<TextBlock>(), t => (t.Text ?? "").StartsWith('⚠'));
            Tap(w, "prog-cmd-dd-fix-contrast");
            Assert.Empty(DeckDesign.CheckTheme(w.DdTheme(d)));
            Assert.DoesNotContain(w.DdBuild(d).Findings, f => f.Kind == "kontrast");
            Assert.True(d.Undo()); Assert.Equal("#E5E0D8", d.Text("text"));
            Tap(w, "prog-cmd-dd-next-theme"); Assert.NotEqual("papier", d.Text("theme"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "DD-08: Folienart der gewählten Folie von Hand wählen und wieder automatisch")]
    public void Layout_override()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1500; w.Height = 950;
        try
        {
            var d = Open(w);
            Tap(w, "dd-thumb-1");
            var auto = w.DdBuild(d).Layouts[1];
            var combo = w.Find<ComboBox>("dd-slide-layout");
            combo.SelectedIndex = 1 + DeckDesign.LayoutNames.ToList().FindIndex(l => l.Layout == DeckLayout.ZweiSpalten); Settle();
            Assert.Equal(DeckLayout.ZweiSpalten, w.DdBuild(d).Layouts[1]);
            Assert.Contains("von Hand", w.Find<TextBlock>("dd-slide-info").Text);
            Tap(w, "prog-cmd-dd-auto-layout");
            Assert.Equal(auto, w.DdBuild(d).Layouts[1]);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "DD-10: KI-Entwurf – Thema rein, Gliederung der (ersetzten) KI wird gestaltet; ohne Thema ein Hinweis")]
    public void Ai_draft()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1500; w.Height = 950;
        string? asked = null;
        MainWindow.DeckAiOverride = p => { asked = p; return Task.FromResult("```\nGarten im Herbst\n  - Was jetzt zu tun ist\nAufgaben\n  - Laub sammeln\n  - Beete mulchen\nZeitplan\n  - Oktober: Zwiebeln setzen\n  - November: Rosen schützen\n  - Dezember: Werkzeug pflegen\n```"); };
        try
        {
            var d = Open(w, "leer");
            Tap(w, "dd-ai-go");
            Assert.Contains("Thema eintragen", w.ProgramMessage(K));
            d.Set("topic", "Garten im Herbst", "Thema"); w.Show(K); Settle();
            Tap(w, "dd-ai-go"); Settle();
            Assert.Contains("Garten im Herbst", asked); Assert.Contains("Zeitleiste", asked);
            var r = w.DdBuild(d);
            Assert.Equal([DeckLayout.Titel, DeckLayout.Stichpunkte, DeckLayout.Zeitleiste], r.Layouts);
            Assert.Contains("prüfen", w.ProgramMessage(K));
            Assert.True(d.Undo()); Assert.DoesNotContain("Garten", d.Text("outline"));
        }
        finally { MainWindow.DeckAiOverride = null; w.Close(); }
    }
}
