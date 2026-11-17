using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace Trendo.App;

/// <summary>V3 (T-0663, freigegeben 15.11.2026): In der Stufe „Einfach“ trägt jeder Knopf, der nur ein Zeichen oder Symbol zeigt (↶, 🖌, F, K, U …),
/// zusätzlich ein kurzes Wort – genommen aus seinem Hinweis (ToolTip), sonst aus dem Namen für Bildschirmleser. Erweitert und Professionell bleiben kompakt.
/// Zentral für alle Seiten, auch künftige: ein Klassen-Handler beim Einhängen eines Knopfs. Name, Klick und Hinweis bleiben unverändert.</summary>
public static class SimpleWords
{
    /// <summary>Gilt die Stufe „Einfach“ für dieses Fenster? Setzt das Hauptfenster.</summary>
    public static Func<Control, bool>? IsSimple;

    /// <summary>Schneller Ausstieg: gilt „Einfach“ gerade in keinem Fenster, prüft der Handler keinen Knopf (kostet beim Seitenaufbau sonst Zeit).</summary>
    public static bool AnySimple;

    /// <summary>Knopf trägt schon das Wort (Inhalt von hier).</summary>
    public static readonly AttachedProperty<bool> WordedProperty = AvaloniaProperty.RegisterAttached<Button, bool>("Worded", typeof(SimpleWords));

    static bool _on;

    static IEnumerable<ILogical> SelfAndBelow(Control c) => new ILogical[] { c }.Concat(c.GetLogicalDescendants());

    /// <summary>Allgemein verständliche Zeichen, die kein Wort brauchen: Hilfe, Reiter schließen/neu, Tasten eines Rechners.</summary>
    public static bool Universal(Button b) => b.GetValue(KeepProperty) || b.Name is { } n && (n == "btn-help" || n.Contains("tab-close") || n.EndsWith("tab-new", StringComparison.Ordinal)
        || n.StartsWith("calc-", StringComparison.Ordinal) || n.StartsWith("rc-key-", StringComparison.Ordinal) // Rechnertasten
        || n.StartsWith("wiz-avatar-", StringComparison.Ordinal) // Bildchen zur Wahl
        || n.StartsWith("mode-marker-size-", StringComparison.Ordinal)); // Kleidergrößen S/M/L

    /// <summary>Kompakter Fachknopf mit fester Größe (z. B. Stumm/Solo einer Spur): bleibt ohne Wort, das Wort steht im Hinweis.</summary>
    public static readonly AttachedProperty<bool> KeepProperty = AvaloniaProperty.RegisterAttached<Button, bool>("Keep", typeof(SimpleWords));
    public static Button Keep(Button b) { b.SetValue(KeepProperty, true); return b; }

    /// <summary>V-05 (T-0684): Zeichen-Knopf, dessen Wort vom Zusammenhang abhängt (Pfeil = „Nächster Monat“, nicht „Abspielen“):
    /// Hinweis und Name für Bildschirmleser fest setzen. In „Einfach“ steht dieses Wort am Knopf.</summary>
    public static Button Named(Button b, string word) { ToolTip.SetTip(b, word); AutomationProperties.SetName(b, word); return b; }

    /// <summary>Hinweis setzen und den Knopf zurückgeben (für Zeichen-Knöpfe in Listen).</summary>
    public static T Tip<T>(T c, string tip) where T : Control { ToolTip.SetTip(c, tip); return c; }

    /// <summary>Text des Knopfs, wie ihn die Person liest.</summary>
    public static string Label(Button b) => b.Content switch
    {
        string s => s,
        TextBlock t => t.Text ?? "",
        Control c => string.Join(" ", SelfAndBelow(c).OfType<TextBlock>().Select(t => t.Text).Where(x => !string.IsNullOrWhiteSpace(x))),
        _ => ""
    };

    /// <summary>Mindestens zwei Buchstaben am Stück oder eine Zahl gelten als lesbar.</summary>
    public static bool Readable(string s) => System.Text.RegularExpressions.Regex.IsMatch(s, @"\p{L}{2,}|\d");

    /// <summary>Symbol-Knopf: Inhalt ist ein Zeichen, ein Linien-Symbol oder ein Bild-Symbol – nicht ein Farbfeld oder Vorschaubild (die zeigen ihre Wahl selbst).</summary>
    public static bool IsSymbolOnly(Button b)
    {
        if (Readable(Label(b))) return false;
        return b.Content switch
        {
            string s => s.Trim().Length > 0,
            TextBlock t => !string.IsNullOrWhiteSpace(t.Text),
            Path or PathIcon => true,
            Control c => SelfAndBelow(c).Any(x => x is Path or PathIcon || x is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text)),
            _ => false
        };
    }

    /// <summary>Kurzes Wort aus einem Hinweis: bis zur ersten Klammer, zum Gedankenstrich, Doppelpunkt oder Punkt; höchstens drei Wörter.</summary>
    public static string? Short(string? tip)
    {
        if (string.IsNullOrWhiteSpace(tip)) return null;
        var s = tip.Trim();
        foreach (var cut in new[] { "\n", " (", " – ", " - ", ": ", ". ", " / ", ", ", "…", "?" })
        {
            var i = s.IndexOf(cut, StringComparison.Ordinal);
            if (i > 0) s = s[..i];
        }
        s = s.Trim().TrimEnd('.', ':');
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 3) s = string.Join(' ', words.Take(3));
        return Readable(s) ? s : null;
    }

    /// <summary>Wort für diesen Knopf: Hinweis, sonst Name für Bildschirmleser, sonst der Name des Linien-Symbols.</summary>
    public static string? WordFor(Button b) =>
        Short(ToolTip.GetTip(b) as string) ?? Short(AutomationProperties.GetName(b)) ?? (GlyphIcons.GetGlyph(b) is { } g ? Short(GlyphIcons.NameFor(g, b.Name)) : null); // V-05: mehrdeutige Zeichen nie raten

    public static void Enable()
    {
        if (_on) return;
        _on = true;
        Control.LoadedEvent.AddClassHandler<Button>((b, _) => { Plain(b); Apply(b); });
        Control.LoadedEvent.AddClassHandler<CheckBox>((c, _) => Plain(c));
        ContentControl.ContentProperty.Changed.AddClassHandler<Button>((b, e) =>
        {
            // eigener Inhalt wechselt (z. B. Abspielen ↔ Pause): danach neu mit Wort versehen
            if (b.GetValue(WordedProperty) && e.NewValue is not null && !ReferenceEquals(e.NewValue, Tagged(b)))
            {
                b.SetValue(WordedProperty, false); b.SetValue(OriginalProperty, null);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (b.IsLoaded) Apply(b); }, Avalonia.Threading.DispatcherPriority.Background);
            }
        });
    }

    static readonly AttachedProperty<object?> WrapperProperty = AvaloniaProperty.RegisterAttached<Button, object?>("Wrapper", typeof(SimpleWords));
    static readonly AttachedProperty<object?> OriginalProperty = AvaloniaProperty.RegisterAttached<ContentControl, object?>("Original", typeof(SimpleWords));
    static readonly AttachedProperty<object?> OriginalTipProperty = AvaloniaProperty.RegisterAttached<ContentControl, object?>("OriginalTip", typeof(SimpleWords));
    static readonly AttachedProperty<double> OriginalWidthProperty = AvaloniaProperty.RegisterAttached<Button, double>("OriginalWidth", typeof(SimpleWords), double.NaN);
    static readonly AttachedProperty<bool> PlainedProperty = AvaloniaProperty.RegisterAttached<ContentControl, bool>("Plained", typeof(SimpleWords));
    static object? Tagged(Button b) => b.GetValue(WrapperProperty);

    /// <summary>Namen, deren Beschriftung Inhalt der Person sein kann (Claude-Bereich, PDF-Formularfelder, eigene Abläufe) – dort nie ersetzen.</summary>
    static bool PersonContent(Control c) => c.Name is not { Length: > 0 } n || n.StartsWith("claude-", StringComparison.Ordinal) || n.StartsWith("pdf-", StringComparison.Ordinal) || n.Contains("flow", StringComparison.Ordinal);

    /// <summary>V7: feste Beschriftung mit Fachwort in „Einfach“ durch das Alltagswort ersetzen; das Fachwort kommt in den Hinweis.</summary>
    public static void Plain(ContentControl c)
    {
        if (!AnySimple || c.GetValue(PlainedProperty) || c.Content is not string s || c.TemplatedParent is not null || PersonContent(c)) return;
        // V-11 (T-0700): Tastenkürzel („Teilen (S)“) wandert in den Hinweis
        var key = Trendo.Core.Programs.PlainWords.WithoutShortcut(s);
        if ((Trendo.Core.Programs.PlainWords.SimpleFor(c.Name!, s) ?? key?.Label) is not { } plain || IsSimple?.Invoke(c) != true) return;
        var tip = ToolTip.GetTip(c);
        c.SetValue(OriginalProperty, s); c.SetValue(OriginalTipProperty, tip); c.SetValue(PlainedProperty, true);
        ToolTip.SetTip(c, (tip is string { Length: > 0 } t ? t + Environment.NewLine : "") + (key is { } k && plain == k.Label ? "Taste: " + k.Key : "Fachwort: " + s));
        if (string.IsNullOrEmpty(AutomationProperties.GetName(c))) AutomationProperties.SetName(c, plain);
        c.Content = plain;
    }

    /// <summary>V-11 (T-0700): feste Texte und Platzhalter mit Fachwort in „Einfach“ durch Alltagswörter ersetzen (<see cref="Trendo.Core.Programs.PlainWords.SimpleTexts"/>),
    /// das Fachwort steht im Hinweis. Läuft nach jedem Seitenaufbau; beim Stufenwechsel wird die Seite ohnehin neu gebaut.</summary>
    public static int PlainTexts(Control root)
    {
        if (!AnySimple || IsSimple?.Invoke(root) != true) return 0;
        var n = 0;
        foreach (var c in root.GetLogicalDescendants().OfType<Control>().ToList())
        {
            if (c.TemplatedParent is not null) continue;
            if (c is TextBlock { Text: { Length: > 0 } tx } tb && tb.Inlines is not { Count: > 0 } && Trendo.Core.Programs.PlainWords.SimpleText(tx) is { } plain)
            { if (ToolTip.GetTip(tb) is null) ToolTip.SetTip(tb, "Fachwort: " + tx.Trim()); tb.Text = plain; n++; }
            else if (c is TextBox { PlaceholderText: { Length: > 0 } ph } box && Trendo.Core.Programs.PlainWords.SimpleText(ph) is { } plainPh)
            { if (ToolTip.GetTip(box) is null) ToolTip.SetTip(box, "Fachwort: " + ph.Trim()); box.PlaceholderText = plainPh; n++; }
        }
        return n;
    }

    /// <summary>Setzt das Wort neben das Symbol, wenn die Stufe „Einfach“ gilt und der Knopf im Seiteninhalt steht (nicht Fensterleiste, Hülle oder Claude-Bereich).</summary>
    public static void Apply(Button b)
    {
        if (!AnySimple || b.GetValue(WordedProperty) || b.TemplatedParent is not null || Universal(b)) return;
        if (IsSimple?.Invoke(b) != true) return;
        if (!IsSymbolOnly(b) || WordFor(b) is not { } word) return;
        var original = b.Content;
        var glyph = GlyphIcons.GetGlyph(b) ?? (original is string gs && GlyphIcons.Map.ContainsKey(gs.Trim()) ? gs.Trim() : null);
        object icon = original switch
        {
            string s when GlyphIcons.Map.TryGetValue(s.Trim(), out var g) => new Path { Data = Geometry.Parse(g.Path), Stretch = Stretch.Uniform, Width = 13, Height = 13, [!Shape.FillProperty] = b[!Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty] },
            string s => new TextBlock { Text = s, VerticalAlignment = VerticalAlignment.Center },
            Control c => c,
            _ => new TextBlock()
        };
        if (icon is Control ic && ic.Parent is Avalonia.Controls.Presenters.ContentPresenter or Button) b.Content = null; // aus dem Knopf lösen, bevor es ins neue Feld kommt
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Children = { (Control)icon, new TextBlock { Text = word, VerticalAlignment = VerticalAlignment.Center } } };
        b.SetValue(WrapperProperty, panel);
        b.SetValue(OriginalProperty, original is Control ? icon : original);
        b.SetValue(WordedProperty, true);
        // feste schmale Breite (Symbol-Knöpfe) würde das Wort abschneiden
        if (!double.IsNaN(b.Width) && b.Width < 120) { b.SetValue(OriginalWidthProperty, b.Width); b.MinWidth = b.Width; b.Width = double.NaN; }
        b.Content = panel;
        if (glyph is not null) b.SetValue(GlyphIcons.GlyphProperty, glyph); // Zeichen bleibt für Claude, Befehlsleiste und Tests abrufbar
    }

    /// <summary>Beim Wechsel der Stufe: in „Einfach“ alle geladenen Knöpfe nachziehen, sonst Wörter und Alltagswörter zurücknehmen.</summary>
    public static void Refresh(Control root)
    {
        foreach (var c in new[] { root }.Concat(root.GetLogicalDescendants().OfType<Control>()).OfType<ContentControl>().ToList())
        {
            if (IsSimple?.Invoke(c) == true) { if (c is Button b) { Plain(b); Apply(b); } else if (c is CheckBox cb) Plain(cb); continue; }
            if (c.GetValue(PlainedProperty))
            {
                c.SetValue(PlainedProperty, false);
                if (c.GetValue(OriginalProperty) is string os) c.Content = os;
                ToolTip.SetTip(c, c.GetValue(OriginalTipProperty));
            }
            if (c is Button wb && wb.GetValue(WordedProperty))
            {
                var glyph = GlyphIcons.GetGlyph(wb);
                var orig = wb.GetValue(OriginalProperty);
                wb.SetValue(WordedProperty, false);
                if (orig is Control oc && oc.Parent is Panel pp) pp.Children.Remove(oc);
                wb.SetValue(WrapperProperty, null);
                wb.Content = orig;
                if (!double.IsNaN(wb.GetValue(OriginalWidthProperty))) { wb.Width = wb.GetValue(OriginalWidthProperty); wb.SetValue(OriginalWidthProperty, double.NaN); }
                if (glyph is not null && orig is not string) wb.SetValue(GlyphIcons.GlyphProperty, glyph);
            }
        }
    }
}
