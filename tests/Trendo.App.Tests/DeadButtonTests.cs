using System.Collections;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>
/// Sichtbare, aktivierte Schaltflächen ohne Click-Handler und ohne Command.
/// Ausnahmen, jeweils mit Begründung:
/// 1. ToggleButton (CheckBox, RadioButton) – der Klick schaltet IsChecked um. Das ist die Wirkung, auch ohne Click-Handler.
///    Der erste Lauf hat genau diese Kästchen gemeldet: easy-lang, a11y-motion, color-blind, simple-mode, ln-consumers, ln-small, inv-small.
/// 2. TemplatedParent gesetzt – Knopf aus einer Avalonia-Vorlage (Bildlauf, Klappfeld). Die Wirkung sitzt im umgebenden Steuerelement.
/// SPEC-07 hat keine Seite (abgelehnt, nicht bauen). CORE-01 ist der Startbildschirm und hat keine Überschrift aus Page().
/// </summary>
public class DeadButtonTests
{
    static readonly string[] Exceptions =
    [
        "ToggleButton: Klick schaltet IsChecked um (CheckBox, RadioButton). Das ist die Wirkung, auch ohne Click-Handler.",
        "TemplatedParent: Avalonia-Vorlage (Bildlauf, Klappfeld), Wirkung im umgebenden Steuerelement.",
    ];

    static readonly FieldInfo Handlers = typeof(Interactive).GetField("_eventHandlers", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Avalonia-Feld _eventHandlers fehlt");

    public static IEnumerable<object[]> KatalogStudios() =>
        StudioCatalog.ById.Keys.OrderBy(id => id, StringComparer.Ordinal).Select(id => new object[] { id });

    [AvaloniaFact(DisplayName = "Startseite, Alle Studios und jede Studio-Seite: keine sichtbare Taste ohne Wirkung")]
    public void Keine_tote_Taste()
    {
        var (w, _, _) = Ui.Open();
        w.Show("start");
        Pump();
        // Die linke Navigation gibt es seit Design 41 nicht mehr; der Startknopf ist der feste Bezugsknopf.
        var nav = w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "start-button");
        Assert.True(HasEffect(nav), "Reflexion erkennt den Click von start-button nicht.");

        var dead = new List<string>();
        foreach (var page in Pages(w))
        {
            w.Show(page);
            Pump();
            Assert.True(w.GetVisualDescendants().OfType<Control>().All(c => c.Name != "error-page"), page + " ist mit der Fehlerseite geöffnet");
            foreach (var ex in Scope(w, page).GetVisualDescendants().OfType<Expander>().ToList()) ex.IsExpanded = true;
            Pump();
            foreach (var b in Candidates(w, page))
                if (!HasEffect(b)) dead.Add($"{page}: {Describe(b)}");
        }

        Assert.True(dead.Count == 0, "Tote Tasten (" + dead.Count + "):\n" + string.Join("\n", dead.OrderBy(x => x, StringComparer.Ordinal)));
        w.Close();
    }

    [AvaloniaTheory(DisplayName = "Studio-Abnahme: Seite öffnet, Überschrift ist der Katalogname, keine tote Taste, eine Hauptaktion ist bedienbar")]
    [MemberData(nameof(KatalogStudios))]
    public void Studio_Abnahme(string id)
    {
        var (w, _, _) = Ui.Open();
        // SPEC-07 ist abgelehnt. Keine Seite anlegen.
        if (id == "SPEC-07")
        {
            Assert.Null(w.PageForStudio(id));
            Assert.Equal("Texte übersetzen", StudioCatalog.ById[id].Name);
            w.Close();
            return;
        }

        var page = w.PageForStudio(id);
        Assert.False(string.IsNullOrEmpty(page), id + " hat keine Seite");
        w.Show(page!);
        Pump();
        Assert.Equal(page, w.CurrentView);
        Assert.True(w.GetVisualDescendants().OfType<Control>().All(c => c.Name != "error-page"), id + " öffnet die Fehlerseite");

        // CORE-01 öffnet den Startbildschirm. Der wird nicht über Page() gebaut und hat deshalb kein page-title.
        if (id == "CORE-01")
        {
            Assert.Equal("start", page);
            var startDead = Candidates(w, "start").Where(b => !HasEffect(b)).Select(Describe).ToList();
            Assert.True(startDead.Count == 0, string.Join("\n", startDead));
            Assert.True(MainActions(w, "start").Any(), "Startbildschirm ohne bedienbare Hauptaktion");
            w.Close();
            return;
        }

        var title = w.ActivePageRoot!.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "page-title").Text;
        Assert.True(title == StudioCatalog.ById[id].Name, $"{id}: „{title}“ statt „{StudioCatalog.ById[id].Name}“");
        foreach (var ex in Scope(w, page!).GetVisualDescendants().OfType<Expander>().ToList()) ex.IsExpanded = true;
        Pump();
        var dead = Candidates(w, page!).Where(b => !HasEffect(b)).Select(Describe).ToList();
        Assert.True(dead.Count == 0, id + " tote Tasten: " + string.Join(" | ", dead));
        Assert.True(MainActions(w, page!).Any(), id + ": keine bedienbare Hauptaktion");
        w.Close();
    }

    [Fact(DisplayName = "Ausnahmen bleiben eine kleine begründete Liste")]
    public void Ausnahmen_sind_begründet()
    {
        Assert.Equal(2, Exceptions.Length);
        Assert.All(Exceptions, r => Assert.True(r.Length > 20, r));
    }

    static List<string> Pages(MainWindow w)
    {
        var pages = new List<string> { "start", "studios" };
        pages.AddRange(MainWindow.StudioPages.Values);
        pages.AddRange(w.PageModules.Keys);
        return pages.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p == "start" ? "" : p == "studios" ? " " : p, StringComparer.Ordinal).ToList();
    }

    /// <summary>Startseite: ganzes Fenster (Leiste, Dock, Kacheln). Sonst der Seiteninhalt plus Fensterknöpfe.</summary>
    static Control Scope(MainWindow w, string page) => page == "start" ? w : w.ActivePageRoot ?? w;

    static IEnumerable<Button> Candidates(MainWindow w, string page)
    {
        var scope = Scope(w, page);
        var buttons = scope.GetVisualDescendants().OfType<Button>().Where(Visible);
        if (page != "start")
            buttons = buttons.Concat(w.GetVisualDescendants().OfType<Button>().Where(b => Visible(b) && b.Name is { } n && (n.StartsWith("win-", StringComparison.Ordinal) || n.StartsWith("task-", StringComparison.Ordinal))));
        return buttons.Where(b => !Exempt(b)).Distinct();
    }

    static IEnumerable<Button> MainActions(MainWindow w, string page) =>
        Candidates(w, page).Where(b => HasEffect(b) && b.Name != "btn-help");

    static bool Visible(Button b) => b.IsEffectivelyVisible && b.IsEffectivelyEnabled;

    static bool Exempt(Button b) => b is ToggleButton || b.TemplatedParent is not null;

    static bool HasEffect(Button b)
    {
        if (b.Command is not null || b.Flyout is not null) return true;
        if (Handlers.GetValue(b) is not IDictionary map || !map.Contains(Button.ClickEvent)) return false;
        return map[Button.ClickEvent] is ICollection { Count: > 0 };
    }

    static string Describe(Button b)
    {
        var text = b.Content as string;
        if (string.IsNullOrWhiteSpace(text))
            text = string.Join(" ", b.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        if (text.Length > 80) text = text[..80];
        return (b.Name ?? "(ohne Name)") + " „" + text + "“";
    }

    static void Pump()
    {
        for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
    }
}
