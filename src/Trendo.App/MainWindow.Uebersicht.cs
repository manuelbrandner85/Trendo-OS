using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>V2 + V8 (T-0663, freigegeben 15.11.2026): In der Stufe „Einfach“ zeigen lange Listen zuerst nur die ersten Einträge mit „Alle … anzeigen“,
/// und lange Beschreibungen auf Übersichtsseiten stehen in einer Zeile – ein Klick zeigt den ganzen Text, der Hinweis ebenfalls. Nichts wird entfernt:
/// Einträge bleiben im Baum (Suche, Claude, Tests) und sind einen Klick entfernt. Erweitert und Professionell zeigen alles wie bisher.</summary>
public partial class MainWindow
{
    /// <summary>So viele Einträge zeigt eine lange Liste zuerst.</summary>
    public const int SimpleListKeep = 8;
    /// <summary>Ab so vielen gleichartigen Einträgen gilt eine Liste als lang.</summary>
    public const int SimpleListLong = 12;

    readonly HashSet<string> _simpleListsOpen = [];

    /// <summary>Übersichtsseiten, deren lange Beschreibungen in „Einfach“ einzeilig werden (Inhalte von Dokumenten und Chats nie).</summary>
    public static readonly IReadOnlySet<string> SimpleTextPages = new HashSet<string>
    {
        "components", "apps", "downloads", "hinzufuegen", "fortschritt", "lizenz", "hardware", "handy", "ki-skills", "studios", "ki-bibliothek",
        "ki-quellen", "ki-rollen", "desktop-symbole", "dev", "appcenter", "prog-pflege", "updates", "systemcheck", "einrichtung", "keys",
    };

    bool SimpleLevel => ProgramLevel == UserLevel.Einfach;

    /// <summary>In „Einfach“ ein zugeklappter Bereich „Mehr Werkzeuge …“ mit dem Inhalt; sonst steht der Inhalt offen da wie bisher.</summary>
    Control SimpleFold(string name, string header, params Control[] content)
    {
        var sp = new StackPanel { Spacing = 8 };
        foreach (var c in content) sp.Children.Add(c);
        if (!SimpleLevel) return sp;
        var ex = new Expander { Name = name, Header = header, Content = sp, IsExpanded = _simpleListsOpen.Contains("fold:" + name), HorizontalAlignment = HorizontalAlignment.Stretch };
        ex.Expanded += (_, _) => _simpleListsOpen.Add("fold:" + name); ex.Collapsed += (_, _) => _simpleListsOpen.Remove("fold:" + name); // bleibt beim Neuaufbau offen
        return ex;
    }

    /// <summary>V3/V7: gilt die Stufe „Einfach“ für dieses Element? Nur im Seiteninhalt eines Fensters oder auf dem Startbildschirm –
    /// nicht in Fensterleiste, Taskleiste, Hülle oder im Claude-Bereich.</summary>
    internal bool SimpleWordsHere(Control c)
    {
        if (!SimpleLevel) return false;
        foreach (var a in c.GetLogicalAncestors())
        {
            if (a is Control { Name: "claude-dock" }) return false;
            if (ReferenceEquals(a, _deskHost) || a is ContentControl cc && _frames.Values.Any(f => ReferenceEquals(f.Body, cc))) return true;
        }
        return false;
    }

    /// <summary>Gehört das Element zum Claude-Bereich oder zu einem dauerhaften Protokoll? Dort wird nie gekürzt (Chats und Inhalte der Person).</summary>
    static bool NeverShorten(Control c) => c.Name is "deps-log" || c.GetLogicalAncestors().OfType<Control>().Any(a => a.Name is "claude-dock" or "claude-chat-log" or "deps-log");

    /// <summary>Nach jedem Seitenaufbau: lange Listen kürzen, lange Beschreibungen einzeilig (nur in „Einfach“).</summary>
    void SimplifyView(string key, Control root)
    {
        if (key != CurrentView) return; // vom Handy angezeigt: die PC-Seite bleibt, wie sie ist
        if (!SimpleLevel) { foreach (var t in root.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.GetValue(OneLinedProperty)).ToList()) { t.SetValue(OneLinedProperty, false); t.MaxLines = 0; } return; }
        var i = 0;
        foreach (var p in root.GetLogicalDescendants().OfType<Panel>().ToList())
        {
            if (LongRun(p, SimpleTextPages.Contains(key)) is not { } run) continue;
            CollapseList(p, run, $"{key}:{p.Name ?? (i++).ToString()}");
        }
        if (!SimpleTextPages.Contains(key)) return;
        // V-06 (T-0684): Begrüßung und Leerzustände werden nie gekürzt – der erste lange Satz der Seite ist ihre Einleitung und bleibt ganz lesbar.
        var texts = root.GetLogicalDescendants().OfType<TextBlock>().ToList();
        var intro = PageIntro(texts);
        foreach (var t in texts) if (!ReferenceEquals(t, intro)) OneLine(t);
    }

    /// <summary>Einleitung einer Seite: der erste lange Satz unter den ersten vier Texten nach dem Seitentitel (Texte von Knöpfen zählen nicht).
    /// Ein langer Text weiter unten ist keine Einleitung und wird wie bisher gekürzt.</summary>
    internal static TextBlock? PageIntro(IReadOnlyList<TextBlock> texts)
    {
        var start = -1;
        for (var i = 0; i < texts.Count; i++) if (texts[i].Name == "page-title") start = i;
        if (start < 0) return null;
        return texts.Skip(start + 1).Where(t => t.Name != "page-sentence" && !string.IsNullOrWhiteSpace(t.Text) && t.FindLogicalAncestorOfType<Button>() is null).Take(4).FirstOrDefault(CanOneLine);
    }

    /// <summary>Überschrift, die direkt über einer Liste steht (im selben Feld oder bis zu drei Ebenen darüber) – damit „Alle … zeigen“ sagt, was kommt.</summary>
    internal static string? ListHeading(Control first)
    {
        Control cur = first;
        for (var depth = 0; depth < 4; depth++)
        {
            var up = cur.GetLogicalParent();
            while (up is not null and not Avalonia.Controls.Panel) { if (up is not (Border or ScrollViewer or ContentControl or Avalonia.Controls.Presenters.ContentPresenter)) return null; up = up.GetLogicalParent(); }
            if (up is not Avalonia.Controls.Panel parent) return null;
            for (var k = parent.Children.IndexOf(cur) - 1; k >= 0; k--)
            {
                var c = parent.Children[k];
                if (!c.IsVisible) continue;
                if (!TextOnly(c)) return null; // dazwischen liegt anderer Inhalt: die Überschrift gehört nicht zu dieser Liste
                if (SectionName(c) is { } n) return System.Text.RegularExpressions.Regex.Replace(n, @"\s*[–(].*$", "").Trim() is { Length: > 1 and <= 32 } clean ? clean : null;
            }
            cur = parent;
        }
        return null;
    }

    /// <summary>Eintrag einer Liste: Zeile oder Karte mit Knopf, oder ein Knopf mit längerem Text – kein Symbol einer Werkzeugleiste.</summary>
    static bool Entry(Control c) => c is Button b ? SimpleWords.Label(b).Length >= 15 : c.GetLogicalDescendants().OfType<Button>().Any();

    /// <summary>Längste Folge gleichartiger Einträge (mindestens <see cref="SimpleListLong"/>) in einer senkrechten Liste oder Kachelfläche.</summary>
    internal static List<Control>? LongRun(Panel p, bool anyRows = false)
    {
        if (p is not (StackPanel { Orientation: Orientation.Vertical } or WrapPanel)) return null;
        if (p.Name is "ki-menu-list" || p.FindLogicalAncestorOfType<Popup>() is not null || NeverShorten(p)) return null;
        if (p.Children.Any(c => c.Name?.StartsWith("einfach-alle", StringComparison.Ordinal) == true)) return null; // schon gekürzt (dauerhafte Liste)
        if (p.GetLogicalAncestors().OfType<Control>().Any(a => a.Name is "prog-workspace" or "prog-canvas")) return null;
        List<Control> best = [], cur = [];
        foreach (var c in p.Children)
        {
            if (!c.IsVisible || c.Name?.StartsWith("einfach-alle", StringComparison.Ordinal) == true) continue;
            var withButton = Entry(c);
            var entry = withButton || anyRows && c is not TextBlock; // Übersichtsseiten: auch Zeilen ohne Knopf (Statuslisten)
            // Zeilen mit Knopf dürfen verschieden gebaut sein (z. B. Download mit Warnhinweis); Zeilen ohne Knopf müssen gleich gebaut sein
            if (entry && (cur.Count == 0 || withButton && Entry(cur[0]) || cur[0].GetType() == c.GetType())) cur.Add(c);
            else { if (cur.Count > best.Count) best = cur; cur = entry ? [c] : []; }
        }
        if (cur.Count > best.Count) best = cur;
        return best.Count >= SimpleListLong ? best : null;
    }

    void CollapseList(Panel p, List<Control> kids, string id)
    {
        var open = _simpleListsOpen.Contains(id);
        var rest = kids.Skip(SimpleListKeep).ToList();
        foreach (var c in rest) c.IsVisible = open;
        var more = new Button { Name = "einfach-alle-" + (p.Name ?? "liste"), Margin = new Thickness(0, 6, 0, 6), HorizontalAlignment = HorizontalAlignment.Left };
        var what = ListHeading(kids[0]); // V-06 (T-0684): der Knopf nennt, was folgt („Vorlagen: alle 19 zeigen“)
        void Label(bool o) { more.Content = o ? "Weniger anzeigen" : what is null ? $"Alle {kids.Count} Einträge zeigen" : $"{what}: alle {kids.Count} zeigen"; ToolTip.SetTip(more, o ? "Nur die ersten Einträge zeigen" : $"Noch {rest.Count} weitere Einträge zeigen"); }
        Label(open);
        more.Click += (_, _) =>
        {
            if (!_simpleListsOpen.Add(id)) _simpleListsOpen.Remove(id);
            var o = _simpleListsOpen.Contains(id);
            foreach (var c in rest) c.IsVisible = o;
            Label(o);
        };
        p.Children.Insert(p.Children.IndexOf(kids[^1]) + 1, more);
    }

    /// <summary>Text ist in „Einfach“ einzeilig gekürzt (Klick schaltet um).</summary>
    static readonly AttachedProperty<bool> OneLinedProperty = AvaloniaProperty.RegisterAttached<TextBlock, bool>("OneLined", typeof(MainWindow));
    static readonly AttachedProperty<bool> OneLineHookedProperty = AvaloniaProperty.RegisterAttached<TextBlock, bool>("OneLineHooked", typeof(MainWindow));

    /// <summary>Käme dieser Text für die einzeilige Kürzung in Frage? Leerzustände (Name mit „empty“) nie.</summary>
    internal static bool CanOneLine(TextBlock t)
    {
        if (t.GetValue(OneLinedProperty) || t is SelectableTextBlock || t.Text is not { } s || s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 25) return false;
        if (t.FindLogicalAncestorOfType<TextBox>() is not null || t.FindLogicalAncestorOfType<Button>() is not null || t.MaxLines == 1 || NeverShorten(t)) return false;
        return !t.GetLogicalAncestors().OfType<Control>().Prepend(t).Any(a => a.Name is { } n && n.Contains("empty", StringComparison.Ordinal));
    }

    /// <summary>Lange Beschreibung (über 25 Wörter, kein Eingabefeld, kein Knopftext, kein Chat) auf eine Zeile – Klick zeigt alles, der Hinweis auch.
    /// Der Klick wird nicht verschluckt: eine anklickbare Karte darum herum öffnet sich weiter.</summary>
    internal static void OneLine(TextBlock t)
    {
        if (!CanOneLine(t) || t.Text is not { } s) return;
        t.SetValue(OneLinedProperty, true);
        t.MaxLines = 1; t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.Wrap;
        if (ToolTip.GetTip(t) is null) ToolTip.SetTip(t, s);
        t.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        if (t.GetValue(OneLineHookedProperty)) return;
        t.SetValue(OneLineHookedProperty, true);
        t.PointerPressed += (_, _) => { if (t.GetValue(OneLinedProperty)) t.MaxLines = t.MaxLines == 1 ? 0 : 1; };
    }
}
