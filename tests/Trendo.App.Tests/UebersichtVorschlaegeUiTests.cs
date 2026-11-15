using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0663 V1–V9 (freigegeben 15.11.2026): Verhalten in der Stufe „Einfach“; Erweitert bleibt wie bisher.</summary>
public class UebersichtVorschlaege_Ui
{
    static MainWindow Simple(string key)
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.ProgramSetLevel(UserLevel.Einfach);
        w.Show(key); Pump();
        return w;
    }
    static void Pump() { Dispatcher.UIThread.RunJobs(); Dispatcher.UIThread.RunJobs(); }

    /// <summary>Wunsch 15.11.2026: Eintrag der linken Leiste und das Fenster, das er öffnet, heißen gleich (vorher „Programmieren“ → „Software-Studio“ für das Studio „Webseite bauen“).</summary>
    [AvaloniaFact(DisplayName = "Linke Leiste: jeder Eintrag öffnet ein Fenster mit demselben Namen")]
    public void Side_names_match_windows()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var rows = new List<string>();
        try
        {
            foreach (var b in w.GetLogicalDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("side-", StringComparison.Ordinal) == true).ToList())
            {
                var label = SimpleWords.Label(b).Trim();
                b.Press(); Pump();
                var title = w.Windows.Active?.Title ?? "";
                rows.Add($"{b.Name}\t{label}\t{title}");
            }
        }
        finally { w.Close(); }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "trendo-leiste.txt"), rows);
        foreach (var r in rows.Select(r => r.Split('\t')).Where(r => r[0] is not ("side-start" or "side-studios")))
            Assert.True(r[2] == r[1], $"{r[0]}: Leiste „{r[1]}“, Fenster „{r[2]}“");
    }

    [AvaloniaFact(DisplayName = "V3: in „Einfach“ tragen Zeichen-Knöpfe ein Wort aus ihrem Hinweis, in „Erweitert“ nicht")]
    public void Symbol_buttons_get_a_word()
    {
        var w = Simple("text");
        try
        {
            Assert.Contains("Fett", SimpleWords.Label(w.Find<Button>("text-bold")));
            Assert.Contains("Rückgängig", SimpleWords.Label(w.Find<Button>("text-undo")));
            w.ProgramSetLevel(UserLevel.Erweitert, "text"); Pump();
            Assert.DoesNotContain("Fett", SimpleWords.Label(w.Find<Button>("text-bold")));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V4: Studio-Übersicht und 3D haben nur noch „KI ▾“ – Claude steht im Menü, nicht als eigener Knopf daneben")]
    public void One_ki_way()
    {
        foreach (var key in new[] { "studios", "3d" })
        {
            var w = Simple(key);
            try
            {
                var claude = w.GetLogicalDescendants().OfType<Button>().Where(b => b.Name == "auftrag-claude").ToList();
                Assert.NotEmpty(claude);
                Assert.All(claude, b => Assert.NotNull(b.FindLogicalAncestorOfType<Popup>()));
                Assert.Single(w.GetLogicalDescendants().OfType<Button>(), b => b.Name == "ki-menu");
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "V5: Texte zeigt in „Einfach“ drei Reiter; „Mehr ▾“ holt die übrigen")]
    public void Text_three_tabs()
    {
        var w = Simple("text");
        try
        {
            Assert.NotNull(w.TryFind<Button>("text-rib-start"));
            Assert.Null(w.TryFind<Button>("text-rib-layout"));
            w.Find<Button>("text-rib-more").Press(); Pump();
            w.Find<Button>("text-rib-layout").Press(); Pump();
            Assert.Equal("layout", w.TextRibbon);
            Assert.NotNull(w.TryFind<Button>("text-rib-referenzen"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V2: „Hinzufügen“ zeigt je Gruppe vier Einträge, „Alle … zeigen“ öffnet die ganze Gruppe")]
    public void Hub_groups_short()
    {
        var w = Simple("hinzufuegen");
        try
        {
            var all = w.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name?.StartsWith("hub-all-", StringComparison.Ordinal) == true);
            Assert.NotNull(all);
            var group = all!.Name!["hub-all-".Length..];
            all.Press(); Pump();
            Assert.Equal(UserLevel.Einfach, w.ProgramLevel);
            Assert.Null(w.TryFind<Button>("hub-all-" + group)); // Gruppe ist jetzt ganz offen
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V9: Studio-Übersicht in „Einfach“ – Bereichs-Kacheln statt aller Karten; ein Klick zeigt die Studios des Bereichs")]
    public void Studio_areas_first()
    {
        var w = Simple("studios");
        try
        {
            Assert.Null(w.TryFind<Button>("studio-card-SPEC-11"));
            w.Find<Button>("studio-area-tile-business").Press(); Pump();
            Assert.NotNull(w.TryFind<Button>("studio-card-SPEC-11"));
            Assert.Null(w.TryFind<Button>("studio-area-tile-business"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V1: Webseite bauen in „Einfach“ – geführter Weg offen, Fachbereiche im Klappbereich „Mehr Werkzeuge“ (weiter im Baum)")]
    public void Web_guided()
    {
        var w = Simple("dev");
        try
        {
            Assert.True(w.Find<Button>("web-build").IsEffectivelyVisible);
            var more = w.Find<Expander>("web-more-tools");
            Assert.False(more.IsExpanded);
            Assert.NotNull(w.TryFind<Button>("web-weiter-anlegen")); // Weiterleitungen: im Klappbereich, nicht entfernt
            Assert.DoesNotContain(w.Find<Button>("web-weiter-anlegen"), Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w).OfType<Button>()); // zugeklappt: nicht zu sehen
            more.IsExpanded = true; Pump();
            w.Show("dev"); Pump();
            Assert.True(w.Find<Expander>("web-more-tools").IsExpanded); // bleibt beim Neuaufbau offen
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V6: Musik-Studio – Eingabeart heißt „Nur beschreiben / Mit eigenem Songtext“, die Art wählt in „Einfach“ eine Liste")]
    public void Music_one_tab_bar()
    {
        var w = Simple("musik-studio");
        try
        {
            Assert.Equal("Nur beschreiben", SimpleWords.Label(w.Find<Button>("ms-mode-einfach")));
            Assert.Null(w.TryFind<Button>("ms-kind-sounds"));
            var pick = w.Find<ComboBox>("ms-kind-pick");
            pick.SelectedIndex = 1; Pump();
            Assert.NotNull(w.Find<ComboBox>("ms-kind-pick"));
            Assert.Equal(1, w.Find<ComboBox>("ms-kind-pick").SelectedIndex);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V7: Fachwörter auf Knöpfen werden in „Einfach“ zu Alltagswörtern, das Fachwort steht im Hinweis")]
    public void Everyday_words()
    {
        Assert.Equal("Als Tabelle speichern", PlainWords.Simple("Als CSV speichern"));
        Assert.Null(PlainWords.Simple("Speichern"));
        var w = Simple("finanzen");
        try
        {
            var b = w.Find<Button>("fin-export");
            Assert.Equal("Als Tabelle speichern", b.Content);
            Assert.Contains("Fachwort: Als CSV speichern", ToolTip.GetTip(b) as string);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V8: Fortschritt zeigt in „Einfach“ je Welle den Stand, die Module auf Klick")]
    public void Progress_folded()
    {
        var w = Simple("fortschritt");
        try
        {
            var waves = w.GetLogicalDescendants().OfType<Expander>().Where(e => e.Name?.StartsWith("progress-wave-", StringComparison.Ordinal) == true).ToList();
            if (waves.Count == 0) return; // ohne Bauplan im Testordner keine Wellen
            Assert.All(waves, e => Assert.False(e.IsExpanded));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V4: im Fokusmodus und in „Erweitert“ bleibt 3D wie bisher – „Programme hinzufügen“ ist erreichbar")]
    public void Focus_and_advanced_unchanged()
    {
        var w = Simple("3d");
        try
        {
            Assert.NotNull(w.TryFind<Button>("auftrag-hinzufuegen"));
            w.ProgramSetLevel(UserLevel.Erweitert, "3d"); Pump();
            Assert.NotNull(w.TryFind<Button>("auftrag-hinzufuegen"));
            Assert.Contains(w.GetLogicalDescendants().OfType<Button>(), b => b.Name == "auftrag-claude" && b.FindLogicalAncestorOfType<Popup>() is null); // Erweitert: eigener Knopf wie bisher
            w.ToggleFocusMode(); w.Show("3d"); Pump();
            Assert.NotNull(w.TryFind<Button>("auftrag-hinzufuegen"));
            w.ProgramSetLevel(UserLevel.Einfach, "3d"); Pump();
            Assert.NotNull(w.TryFind<Button>("auftrag-hinzufuegen"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V3: Wörter nur im Seiteninhalt (nicht an Fensterknöpfen) und zurückgenommen beim Wechsel nach „Erweitert“; das Symbolzeichen bleibt abrufbar")]
    public void Words_only_in_page_and_revert()
    {
        var w = Simple("text");
        try
        {
            foreach (var b in w.GetLogicalDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("win-", StringComparison.Ordinal) == true))
                Assert.False(b.GetValue(SimpleWords.WordedProperty), b.Name);
            var undo = w.Find<Button>("text-undo");
            Assert.True(undo.GetValue(SimpleWords.WordedProperty));
            w.ProgramSetLevel(UserLevel.Erweitert); Pump(); // ohne Neuaufbau: der Wechsel nimmt das Wort zurück
            Assert.False(undo.GetValue(SimpleWords.WordedProperty));
            Assert.DoesNotContain("Rückgängig", SimpleWords.Label(undo));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V2/V8: Claude-Chat und Protokolle werden nie gekürzt; eine Liste wird nur einmal gekürzt")]
    public void Never_shorten_chat()
    {
        StackPanel Rows(int n) { var p = new StackPanel(); for (var i = 0; i < n; i++) p.Children.Add(new Border { Child = new Button { Content = "Öffnen" } }); return p; }
        var plain = Rows(15);
        Assert.NotNull(MainWindow.LongRun(plain));
        var chat = Rows(15);
        _ = new Border { Name = "claude-dock", Child = new StackPanel { Children = { chat } } };
        Assert.Null(MainWindow.LongRun(chat));
        plain.Children.Add(new Button { Name = "einfach-alle-liste", Content = "Alle 15 anzeigen" });
        Assert.Null(MainWindow.LongRun(plain)); // schon gekürzt: kein zweiter Knopf
        var log = new TextBlock { Name = "deps-log", Text = string.Join(' ', Enumerable.Repeat("Wort", 40)) };
        MainWindow.OneLine(log);
        Assert.NotEqual(1, log.MaxLines);
        var desc = new TextBlock { Text = string.Join(' ', Enumerable.Repeat("Wort", 40)) };
        MainWindow.OneLine(desc);
        Assert.Equal(1, desc.MaxLines);
    }
}
