using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0262: Programm „Karteikarten“ über den echten Eingabeweg – Karten eintippen, Leertaste/Zifferntasten beim Lernen, Eintippen, Quiz, Tabelle, Prüfungsplan.</summary>
public class SPEC06_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void TypeInto(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); Settle(); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    static string Front(MainWindow w) => Get<TextBlock>(w, "learn-front").Text!;

    [AvaloniaFact(DisplayName = "SPEC-06 UI: Karten eintippen, mit Leertaste aufdecken, mit Taste 3 bewerten – Abstand-Vorschau auf den Knöpfen, Statistik zählt mit")]
    public void Add_and_study_with_keyboard()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-06"]); Settle();
        Assert.Null(Try<Button>(w, "learn-gut")); // kein Formular mehr
        Press(w, "learn-mode-karten");
        var deck = Get<TextBox>(w, "learn-new-deck"); deck.Text = "";
        TypeInto(w, "learn-new-deck", "Englisch");
        foreach (var (f, b) in new[] { ("Hund", "dog"), ("Katze", "cat") })
        {
            TypeInto(w, "learn-new-front", f); TypeInto(w, "learn-new-back", b); Key1(w, Key.Enter, PhysicalKey.Enter);
        }
        Assert.Equal(2, w.Learn.Progress("Englisch").Total);
        Assert.Equal("Englisch", w.LearnDeck);
        Assert.Equal("Hund", Get<TextBox>(w, "learn-f-0").Text); Assert.Equal("cat", Get<TextBox>(w, "learn-b-1").Text);
        // Lernen: Leertaste deckt auf, Knöpfe zeigen den nächsten Abstand, Taste 3 = „Gut“
        Press(w, "learn-mode-lernen");
        var first = Front(w); Assert.Null(Try<TextBlock>(w, "learn-back"));
        Key1(w, Key.Space, PhysicalKey.Space);
        Assert.Equal(first == "Hund" ? "dog" : "cat", Get<TextBlock>(w, "learn-back").Text);
        Assert.Contains("in 4 T.", string.Join(" ", Get<Avalonia.Controls.Primitives.UniformGrid>(w, "learn-grades").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        Key1(w, Key.D3, PhysicalKey.Digit3);
        Assert.Contains("1 Tag", Get<TextBlock>(w, "learn-msg").Text);
        Assert.NotEqual(first, Front(w));
        // Klick auf die Karte deckt ebenfalls auf, Knopf „Leicht“
        RealClick(w, Get<Border>(w, "learn-card")); Assert.NotNull(Try<TextBlock>(w, "learn-back"));
        Press(w, "learn-grade-4");
        Assert.Contains("4 Tagen", Get<TextBlock>(w, "learn-msg").Text);
        Assert.Contains("Für heute alles gelernt", Front(w));
        Assert.Equal(0, w.Learn.Progress("Englisch").Due);
        Assert.Contains("✓ heute", string.Join(" ", Get<Border>(w, "learn-side").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-06 UI: Eintippen (Tippfehler toleriert), Quiz mit Zifferntaste, Tabelle bearbeiten/suchen/löschen, Liste einfügen, umkehren, CSV, Prüfungsplan")]
    public void Typed_quiz_table_tools()
    {
        var (w, _, _) = Ui.Open();
        w.Learn.Import("Englisch", "Hund;dog\nKatze;cat\nHaus;house\nBaum;tree");
        w.LearnDeck = "Englisch"; w.Show(MainWindow.StudioPages["SPEC-06"]); Settle();
        // Eintippen
        Press(w, "learn-mode-tippen");
        var front = Front(w); var answer = w.Learn.Store.Cards.First(c => c.Front == front).Back;
        TypeInto(w, "learn-typed", answer[..^1] + "x"); Key1(w, Key.Enter, PhysicalKey.Enter);
        Assert.Contains("✓ Richtig (kleiner Tippfehler)", Get<TextBlock>(w, "learn-typed-result").Text);
        Press(w, "learn-next");
        front = Front(w); TypeInto(w, "learn-typed", "falsch"); Key1(w, Key.Enter, PhysicalKey.Enter);
        Assert.Contains("✕ Leider falsch", Get<TextBlock>(w, "learn-typed-result").Text);
        // Quiz: richtige Antwort mit ihrer Zifferntaste
        Press(w, "learn-mode-quiz");
        front = Front(w); answer = w.Learn.Store.Cards.First(c => c.Front == front).Back;
        var opts = Enumerable.Range(0, 4).Select(i => Try<Button>(w, $"learn-opt-{i}")).Where(b => b is not null).Select(b => b!.Content!.ToString()!).ToList();
        var idx = opts.FindIndex(o => o.EndsWith(" " + answer));
        Get<DockPanel>(w, "learn-root").Focus(); Settle();
        Key1(w, Key.D1 + idx, PhysicalKey.Digit1 + idx);
        Assert.Equal("✓ Richtig!", Get<TextBlock>(w, "learn-msg").Text);
        // Tabelle: bearbeiten, suchen, löschen, Liste einfügen
        Press(w, "learn-mode-karten");
        var row = Enumerable.Range(0, 4).First(i => Get<TextBox>(w, $"learn-f-{i}").Text == "Haus");
        var back = Get<TextBox>(w, $"learn-b-{row}"); back.Text = ""; TypeInto(w, $"learn-b-{row}", "home"); Key1(w, Key.Enter, PhysicalKey.Enter);
        Assert.Equal("home", w.Learn.Store.Cards.First(c => c.Front == "Haus").Back);
        TypeInto(w, "learn-search", "Baum");
        Assert.Equal("Baum", Get<TextBox>(w, "learn-f-0").Text); Assert.Null(Try<TextBox>(w, "learn-f-1"));
        Press(w, "learn-del-0"); Assert.DoesNotContain(w.Learn.Store.Cards, c => c.Front == "Baum");
        Get<TextBox>(w, "learn-search").Text = ""; Settle();
        Get<Expander>(w, "learn-import-box").IsExpanded = true; Settle();
        Get<TextBox>(w, "learn-import-text").Text = "Apfel;apple\nBuch\tbook";
        Press(w, "learn-import"); Assert.Contains("2 Karte(n) übernommen", Get<TextBlock>(w, "learn-msg").Text);
        Assert.Equal(5, w.Learn.Progress("Englisch").Total);
        // Stapel-Werkzeuge rechts
        Press(w, "learn-reverse"); Assert.Equal(5, w.Learn.Progress("Englisch (umgekehrt)").Total);
        Press(w, "learn-export"); var file = Get<TextBlock>(w, "learn-msg").Text!["Gespeichert: ".Length..];
        Assert.True(File.Exists(file)); Assert.Contains("Apfel;apple", File.ReadAllText(file));
        var exam = Get<TextBox>(w, "learn-exam-d"); exam.Text = DateTime.Now.AddDays(10).ToString("dd.MM.yyyy"); Settle();
        Assert.Contains("pro Tag", Get<SelectableTextBlock>(w, "learn-exam-out").Text);
        exam.Text = "morgen"; Settle(); Assert.StartsWith("⚠", Get<SelectableTextBlock>(w, "learn-exam-out").Text);
        // Stapel umbenennen
        var ren = Get<TextBox>(w, "learn-rename"); ren.Text = "EN"; ren.Focus(); Settle(); Key1(w, Key.Enter, PhysicalKey.Enter);
        Assert.Equal("EN", w.LearnDeck); Assert.Equal(5, w.Learn.Progress("EN").Total);
        w.Close();
    }
}
