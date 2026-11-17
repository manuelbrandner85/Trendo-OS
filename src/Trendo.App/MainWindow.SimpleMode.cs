using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Stufe „Einfach“ (Wunsch 08.11.2026): weniger auf einen Blick, keine Funktion geht verloren. Alles Übrige liegt hinter „Mehr“ oder in Erweitert/Professionell.</summary>
public partial class MainWindow
{
    /// <summary>So viele Befehle des Programms bleiben in „Einfach“ direkt sichtbar, zusätzlich Speichern und Rückgängig.</summary>
    public const int SimpleCommands = 4;
    /// <summary>So viele Knöpfe zeigt eine Seite in „Einfach“ ungefähr, bevor der Rest hinter „Mehr anzeigen“ wandert.</summary>
    public const int SimplePageButtons = 14;

    private bool IsSimple => ProgramLevel == UserLevel.Einfach;
    /// <summary>T-0709: Büro-Studios, deren wichtigste Ausgaben (PDF, Word …) in „Einfach“ direkt im Menüband stehen statt hinter „Mehr“ –
    /// das Ergebnis ist dort der Zweck des Programms. Wert: wie viele der ersten <see cref="ProgramSpec.Exports"/> (höchstens <see cref="ProgramSpec.SimpleExports"/>).
    /// Zwei nur, wo beide in eine Menüband-Zeile passen (V-01: Arbeitsfläche beginnt im oberen Drittel); der Rest bleibt unter „Mehr“.</summary>
    internal static readonly Dictionary<string, int> OfficeDirectExports = new(StringComparer.OrdinalIgnoreCase)
    {
        [CvKey] = 2,          // Lebenslauf (PDF), Lebenslauf (Word)
        [DeckDesignKey] = 2,  // PowerPoint, PDF
        [CardKey] = 1,        // Druck-PDF mit Beschnitt
        [PwKey] = 1,          // Ergebnis als PDF
        [ScanKey] = 1,        // PDF (alle Seiten)
        // Nicht nötig: Verträge („Erinnerungen (Kalender)“ und „Kündigung schreiben“ stehen schon als Befehle direkt da) und
        // Formulare („Ausgefüllt speichern“ ist schon der PDF-Export) – ein zweiter Knopf würde dort nur das Menüband umbrechen.
    };
    /// <summary>T-0700: Markierung (Tag) für Befehle, die in „Einfach“ unter „Mehr“ gehören, z. B. „Bild-KI einrichten“, solange daneben „Jetzt einrichten“ steht.</summary>
    internal const string SimpleIntoMore = "einfach-mehr";

    private List<(string, List<Control>)> SimpleRibbon(List<(string Name, List<Control> Items)> groups, ProgramSpec spec)
    {
        var shown = groups.SelectMany(g => g.Items).Select(i => i.Name).ToHashSet();
        var intoMore = groups.SelectMany(g => g.Items).Where(i => Equals(i.Tag, SimpleIntoMore)).Select(i => i.Name).ToHashSet();
        var prefer = spec.SimplePrefer?.ToList() ?? [];
        int Rank(ProgramCmd c) => c.Primary ? -1 : prefer.IndexOf(c.Name) is var i && i >= 0 ? i : int.MaxValue; // Hauptaktion, dann Vorrang, dann Reihenfolge
        var direct = spec.Ribbon.SelectMany(g => g.Item2).OrderBy(Rank).Select(c => "prog-cmd-" + c.Name).Where(n => shown.Contains(n) && !intoMore.Contains(n)).Take(SimpleCommands).ToHashSet();
        direct.Add("prog-save"); direct.Add("prog-undo"); direct.Add("prog-blocker"); // V-09: der Hinweis „… fehlt – Jetzt einrichten“ bleibt neben der Hauptaktion
        if (spec.PrimaryExport is { } pe) direct.Add("prog-export-" + pe); // T-0700: Hauptaktion „Ergebnis speichern“ nie unter „Mehr“
        if (OfficeDirectExports.TryGetValue(spec.Key, out var nDirect)) // T-0709 (aus T-0705): in den Büro-Studios bleiben die wichtigsten Ausgaben direkt sichtbar – ruhig, nicht golden
            foreach (var x in spec.Exports.Take(Math.Min(nDirect, spec.SimpleExports))) direct.Add("prog-export-" + x.Name);
        var keep = groups.SelectMany(g => g.Items).Where(i => direct.Contains(i.Name ?? "")).ToList();
        var more = new Button { Name = "prog-more", Content = "Mehr", FontSize = 12.5, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), Foreground = Text };
        ToolTip.SetTip(more, "Alle weiteren Befehle");
        var list = new StackPanel { Name = "prog-more-list", Width = 360, Spacing = 8 };
        foreach (var (name, items) in groups)
        {
            var rest = items.Where(i => !direct.Contains(i.Name ?? "")).ToList();
            if (rest.Count == 0) continue;
            var row = new WrapPanel();
            foreach (var c in rest)
            {
                c.Margin = new Thickness(0, 0, 4, 4);
                if (c is Button { Flyout: null } b) b.Click += (_, _) => more.Flyout?.Hide();
                row.Children.Add(c);
            }
            list.Children.Add(new StackPanel { Spacing = 3, Children = { new TextBlock { Text = PlainWords.For(ProgramLevel, name), Foreground = Muted, FontSize = 11, FontWeight = FontWeight.SemiBold }, row } });
        }
        more.Flyout = new Flyout { Content = new ScrollViewer { MaxHeight = 480, Content = list } };
        keep.Add(more);
        return [("Start", keep)];
    }

    // ---------- Spiele bauen: einfache Ansicht ----------
    private bool _gameShowAll;

    /// <summary>Was als Nächstes dran ist: Name des Bereichs, Satz für die Person, Auftrag an die KI. Null = alles geplant.</summary>
    public static (string Area, string Text, string Prompt)? GameNextStep(JsonObject data)
    {
        var missing = GameDesign.Completeness(data).OrderBy(k => k.Value).FirstOrDefault();
        if (missing.Key is null || missing.Value >= 100) return null;
        var word = missing.Key switch { "Spielbibel" => "die Grundidee", "Systeme" => "die Spielregeln", "Menüs" => "die Menüs", "Ton" => "Geräusche und Musik", "Missionen" => "die Aufgaben im Spiel", var k => k };
        return (missing.Key, $"Deinem Spiel fehlen noch: {word}.", $"Ergänze {word} passend zu diesem Spiel.");
    }

    private Control GameNextCard(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-next", Spacing = 6, Children = { new TextBlock { Text = "Als Nächstes", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Text } } };
        var row = new WrapPanel();
        if (GameNextStep(d.Data) is { } next)
        {
            sp.Children.Add(P(next.Text, Text, 13));
            row.Children.Add(MSp(Act("game-next-ki", "Von der KI ergänzen lassen", () => GamePromptRun(next.Prompt, true), true)));
            if (next.Area is "Spielbibel" or "Figuren" or "Welt" or "Missionen")
                row.Children.Add(MSp(Act("game-next-self", "Selbst bearbeiten", () => { _gameShowAll = true; _gameSection = next.Area; Show(GK); })));
        }
        else if (d.Text("built").Length == 0)
        {
            sp.Children.Add(P("Dein Spiel ist fertig geplant. Jetzt kann Trendo es erstellen.", Text, 13));
            row.Children.Add(MSp(Act("game-next-build", "Spiel erstellen", () => ProgramRun(GK, () => GameBuild(d)), true)));
        }
        else
        {
            sp.Children.Add(P("Dein Spiel ist erstellt. Probier es aus.", Text, 13));
            row.Children.Add(MSp(Act("game-next-play", "Spielen", () => ProgramRun(GK, () => GamePlay(d)), true)));
        }
        sp.Children.Add(row);
        return WCard(sp);
    }

    /// <summary>„Einfach“: Zusatzknöpfe im Seitenkopf (Freie KI, Team) wandern in ein kleines „Mehr“.</summary>
    /// <remarks>V-13 (T-0684): Im selben Menü stehen die „Verwandten Programme“ (früher die Zeile „Gehört hierher:“ über der Seite) – in jeder Stufe.</remarks>
    private void SimplifyHeadExtras(Avalonia.Controls.Panel right, IReadOnlyList<Button>? related = null)
    {
        var extras = IsSimple ? right.Children.ToList() : [];
        if (extras.Count == 0 && related is not { Count: > 0 }) return;
        var more = new Button { Name = "page-head-more", Content = "Mehr", FontSize = 12, Padding = new Thickness(9, 2), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#22FFFFFF"), Foreground = Text, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Top };
        ToolTip.SetTip(more, related is { Count: > 0 } ? "Verwandte Programme und weitere Hilfen" : "Weitere Hilfen zu dieser Seite");
        var list = new StackPanel { Name = "page-head-more-list", Spacing = 6, MaxWidth = 340 };
        foreach (var c in extras) { right.Children.Remove(c); list.Children.Add(c); }
        if (related is { Count: > 0 }) list.Children.Add(RelatedList(related, more));
        more.Flyout = new Flyout { Content = list };
        right.Children.Add(more);
    }

    // ---------- 3D-Modelle erstellen ----------
    private bool _simple3dMore;
    /// <summary>T-0708: oberste Kopfzeile von „3D-Modelle erstellen“ (V-14-Gruppe, in „Einfach“ dazu die Eingabe).</summary>
    const string Head3DRow = "3d-head-row";

    private void Simplify3D(Control header, Control left, Control right, TextBox objName)
    {
        if (!IsSimple) return;
        var toggle = Act("3d-show-more", _simple3dMore ? "Weniger anzeigen" : "Mehr Werkzeuge anzeigen", () => { Keep(objName); _simple3dMore = !_simple3dMore; Show("3d"); });
        toggle.Margin = new Thickness(0, 4, 0, 0);
        var ls = (StackPanel)((ScrollViewer)left).Content!;
        ls.Children.Insert(2, toggle); // nach „Objekt“ und „Drucken & Modelle“
        var rsAll = (StackPanel)((ScrollViewer)right).Content!;
        if (header is Panel top && rsAll.Children.Count >= 3) // die Eingabe „Was soll dieses Studio tun?“ braucht die volle Breite
        {
            var prompt = rsAll.Children[^3]; rsAll.Children.Remove(prompt);
            if (top.Children.OfType<Grid>().FirstOrDefault(g => g.Name == Head3DRow) is { } row) { Grid.SetColumn(prompt, 1); row.Children.Add(prompt); } // T-0708: neben Speichern · Rückgängig …
            else top.Children.Add(prompt);
        }
        if (_simple3dMore) return;
        foreach (var c in ls.Children.Skip(3)) c.IsVisible = false; // Gespeichertes, Engine-Export, Vorschaubilder, Umwandeln
        var rs = (StackPanel)((ScrollViewer)right).Content!;
        rs.Children[^1].IsVisible = false; // komplexe Figuren über Blender
        if (header is Panel hp) foreach (var c in hp.Children.Where(c => c.Name != Head3DRow)) c.IsVisible = false; // Blender-Hinweis, Datei- und Exportzeile; die Zeile mit Speichern · Rückgängig … und der Eingabe bleibt (T-0708)
    }

    /// <summary>Wunsch 08.11.2026: Bambu Studio, Drucker und freie Modellseiten direkt aus dem 3D-Studio, in jeder Stufe.</summary>
    private Control PrintAndModelsCard(TextBox objName) => Card(new StackPanel { Name = "3d-print-card", Spacing = 6, Children = {
        H("Drucken & Modelle", 14),
        Act("3d-to-print", "Drucken mit Bambu Studio", () => SendToPrint(objName), true),
        new WrapPanel { Children = { MSp(Act("3d-printer", "Drucker steuern", () => Show("3d-druck"))), MSp(Act("3d-find-models", "Freie 3D-Modelle suchen", () => Show(BIB))) } } } });

    /// <summary>Aktuelles Modell als STL (Millimeter) speichern und auf der Seite „3D-Druck“ öffnen.</summary>
    private void SendToPrint(TextBox objName)
    {
        Keep(objName);
        if (_scene.Parts.Count == 0) { Say("Noch kein Teil. Füge zuerst eines hinzu oder hole dir ein Modell.", "3d"); return; }
        try
        {
            var mesh = Mesh3D.FromScene(_scene);
            if (mesh.Triangles == 0) { Say("Das Objekt hat keine Fläche.", "3d"); return; }
            var path = Mesh3D.Unique(Path.Combine(Folder3D, DevStudio.SafeName(_scene.Name) + ".stl"));
            Mesh3D.WriteStl(mesh, path); _last3dFile = path;
            Open3dPrint(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { Say("Das Modell ließ sich nicht für den Druck speichern: " + Trendo.Core.FriendlyError.Say(ex), "3d"); }
    }

    // ---------- Übrige Seiten: Rest hinter „Mehr anzeigen“ ----------
    private readonly HashSet<string> _pageMoreOpen = [];
    /// <summary>Seiten, die eine geschlossene Arbeitsfläche sind oder von anderen gepflegt werden; sie bleiben, wie sie sind.</summary>
    // T-0717: „ki-rollen“ – Auftrag, „Rolle starten“ und dessen Rückgängig/Wiederholen sind der Hauptinhalt; vorher blieb die Seite knapp unter der Grenze und ganz sichtbar
    private static readonly HashSet<string> NoPageMore = ["start", "studios", "ki-rollen"];
    private static readonly HashSet<string> NoPageMoreStudios = ["STUDIO-MUSIK-01", "STUDIO-TV-01", "SPEC-15", "CORE-08", "CORE-07"];

    private static StackPanel? MainStack(Control c)
    {
        for (var depth = 0; depth < 6; depth++)
            switch (c)
            {
                case StackPanel { Children.Count: >= 3 } s: return s;
                case StackPanel { Children.Count: >= 1 } s: c = s.Children[^1]; break;
                case Border { Child: { } b }: c = b; break;
                case ScrollViewer { Content: Control v }: c = v; break;
                default: return null;
            }
        return null;
    }

    private static int ButtonsIn(Control c) => (c is Button and not Avalonia.Controls.Primitives.ToggleButton ? 1 : 0) + c.GetLogicalDescendants().OfType<Button>().Count(b => b is not Avalonia.Controls.Primitives.ToggleButton); // Auswahlfelder zählen nicht als Knöpfe

    /// <summary>Block ohne Bedienelement, der nur aus Text besteht (Überschrift, Erklärsatz).</summary>
    internal static bool TextOnly(Control c) => c is TextBlock || c is Avalonia.Controls.Panel p && p.Children.Count > 0 && p.Children.All(x => x is TextBlock);

    static string? Heading(TextBlock t) => t.FontWeight >= FontWeight.SemiBold && t.Text is { Length: > 0 and <= 48 } s && !s.Contains(". ") ? s.Trim().TrimEnd(':') : null;

    /// <summary>Name eines Abschnitts: seine fette Überschrift oder der Kopf eines Klappbereichs. Null, wenn der Block keinen Namen trägt.</summary>
    internal static string? SectionName(Control c)
    {
        switch (c)
        {
            case TextBlock t: return Heading(t);
            case Expander { Header: string h }: return h;
            case Expander { Header: TextBlock ht }: return ht.Text;
            case Border { Child: { } bc }: return SectionName(bc);
            case Avalonia.Controls.Panel p:
                foreach (var k in p.Children.Take(2))
                    if (k is TextBlock or Border or Avalonia.Controls.Panel && SectionName(k) is { } n) return n;
                return null;
            default: return null;
        }
    }

    /// <summary>Steht direkt vor dem Schnitt eine Überschrift, deren Inhalt sonst ganz verschwände?</summary>
    static bool SectionHeadBefore(StackPanel main, int cut)
    {
        for (var j = cut - 1; j >= 0 && TextOnly(main.Children[j]); j--)
            if (SectionName(main.Children[j]) is not null) return true;
        return false;
    }

    /// <summary>V-06 (T-0684): Der Knopf nennt, was folgt – die Namen der eingeklappten Abschnitte. Ohne Namen bleibt die Zahl der Funktionen.</summary>
    internal static string MoreLabel(IReadOnlyList<Control> rest, int extra)
    {
        var names = rest.Select(SectionName).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => System.Text.RegularExpressions.Regex.Replace(n!, @"\s*[–(].*$", "").Trim()).Where(n => n.Length > 1).Distinct().ToList();
        if (names.Count == 0) return $"Mehr anzeigen ({extra} weitere Funktionen)";
        var shown = names.Take(2).ToList();
        return "Mehr anzeigen: " + string.Join(", ", shown) + (names.Count > shown.Count ? $" und {names.Count - shown.Count} weitere" : "");
    }

    /// <summary>„Einfach“: Die ersten Bereiche einer Seite bleiben, der Rest erscheint nach einem Klick. Reine Texte (Meldungen, Ergebnisse) bleiben immer sichtbar.</summary>
    private void SimplifyPage(string view, Control content)
    {
        if (!IsSimple || !_settings.SetupDone || NoPageMore.Contains(view) || (StudioIdForPage(view) is { } sid && NoPageMoreStudios.Contains(sid)) || Programs.ContainsKey(view) || MainStack(content) is not { } main) return;
        if (ButtonsIn(main) <= SimplePageButtons + 4) return;
        var sum = 0; var cut = -1;
        for (var i = 0; i < main.Children.Count; i++)
        {
            sum += ButtonsIn(main.Children[i]);
            if (sum > SimplePageButtons && i > 0) { cut = i; break; }
        }
        if (cut < 0) return;
        // V-06 (T-0684): „Mehr anzeigen“ versteckt nie den Hauptinhalt. Steht direkt vor dem Schnitt eine Überschrift (mit Erklärtext),
        // bleibt ihr Abschnitt offen – der Schnitt rückt bis vor die nächste Überschrift.
        if (SectionHeadBefore(main, cut))
        {
            cut++;
            while (cut < main.Children.Count && !(TextOnly(main.Children[cut]) && SectionName(main.Children[cut]) is not null)) cut++;
            if (cut >= main.Children.Count) return;
        }
        static bool IsMessage(Control c) => (c.Name ?? "") is { Length: > 0 } n && (n.EndsWith("-msg") || n.EndsWith("-state") || n.EndsWith("-status") || n.EndsWith("-result")); // Meldungen bleiben sichtbar
        var rest = main.Children.Skip(cut).Where(c => !IsMessage(c)).ToList();
        if (!rest.Any(c => ButtonsIn(c) > 0)) return;
        var open = _pageMoreOpen.Contains(view);
        var extra = rest.Sum(ButtonsIn);
        var toggle = new Button { Name = "page-more", Content = open ? "Weniger anzeigen" : MoreLabel(rest, extra), HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(18, 7), CornerRadius = new CornerRadius(10), Foreground = Text, Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), Margin = new Thickness(0, 6) };
        toggle.Click += (_, _) => { if (!_pageMoreOpen.Remove(view)) _pageMoreOpen.Add(view); Show(view); };
        main.Children.Insert(cut, toggle);
        if (!open) foreach (var c in rest) main.Children.Remove(c);
    }
}
