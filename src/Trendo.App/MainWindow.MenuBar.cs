using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0710 (Wunsch 17.11.2026 „Das Menüband in den Programmen wie bei Windows, wo Datei usw. steht“, gewählt: klassische Menüleiste):
/// EIN Baustein für alle Programme – eine schmale Textzeile Datei · Bearbeiten · Ansicht · (fachliche Menüs) · KI · Hilfe. Ein Klick öffnet eine Liste mit
/// Tastenkürzeln rechts, Trennlinien, ausgegrauten Einträgen und Untermenüs. Alt bzw. F10 holt die Leiste, Alt+Buchstabe öffnet ein Menü (unterstrichener Buchstabe),
/// Pfeiltasten wandern, Enter löst aus, Esc schließt (der Fokus geht zurück in die Arbeit). Jeder Eintrag ruft denselben Handler wie der Knopf des Programms –
/// die Leiste hat keine eigene Befehlslogik (Modell: <see cref="ProgramMenu"/>, Trendo.Core).</summary>
public partial class MainWindow
{
    /// <summary>Name der Menüleiste (eine je Programmseite).</summary>
    public const string MenuBarName = "prog-menubar";
    /// <summary>Höhe der Menüleiste: eine Textzeile.</summary>
    public const double MenuBarHeight = 26;

    /// <summary>Zuletzt bedientes Element außerhalb einer Menüleiste – dorthin geht der Fokus nach Esc zurück, darauf wirken Ausschneiden/Kopieren/Einfügen.</summary>
    IInputElement? _menuWorkFocus;
    bool _menuKeysReady;

    /// <summary>Menüleiste aus dem Modell bauen. Einträge heißen „menu-&lt;Kennung&gt;“, Menüs „menubar-&lt;Titel&gt;“.</summary>
    public Menu MenuBarOf(IEnumerable<MenuTop> model)
    {
        EnsureMenuKeys();
        var menus = ProgramMenu.Normalize(model);
        var bar = new Menu { Name = MenuBarName, Height = MenuBarHeight, MinHeight = 0, Padding = new Thickness(0), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center, Tag = menus };
        Avalonia.Automation.AutomationProperties.SetName(bar, "Menüleiste – Alt oder F10");
        var keys = ProgramMenu.AccessKeys(menus.Select(m => m.Title).ToList(), fixedFirst: true);
        for (var i = 0; i < menus.Count; i++)
        {
            var m = menus[i];
            var top = new MenuItem { Name = "menubar-" + m.Title, Header = ProgramMenu.WithAccess(m.Title, keys[i]), Tag = keys[i], FontSize = 13, Padding = new Thickness(8, 2), MinHeight = 0, Height = MenuBarHeight - 2,
                Foreground = Text, Background = Brushes.Transparent };
            AddItems(top, m.Items, true);
            top.SubmenuOpened += (_, _) => Refresh(top); // ausgegraut/Häkchen erst beim Aufklappen fragen – der Stand ist dann aktuell
            bar.Items.Add(top);
        }
        // Reicht die Breite nicht (schmales Fenster, viele Fachmenüs), wandern die hinteren Menüs unter „…“ – wie die Menüleiste in VS Code; die Zeile bleibt eine Zeile
        var more = new MenuItem { Name = "menubar-mehr", Header = "…", Tag = '\0', FontSize = 13, Padding = new Thickness(8, 2), MinHeight = 0, Height = MenuBarHeight - 2, Foreground = Text, Background = Brushes.Transparent, IsVisible = false };
        ToolTip.SetTip(more, "Weitere Menüs");
        bar.Items.Add(more);
        var tops = bar.Items.OfType<MenuItem>().Where(t => !ReferenceEquals(t, more)).ToList();
        var widths = new double[tops.Count];
        _menuFit.AddOrUpdate(bar, avail =>
        {
            for (var i = 0; i < tops.Count; i++) if (tops[i].IsVisible && tops[i].DesiredSize.Width > 0) widths[i] = tops[i].DesiredSize.Width;
            if (widths.Any(x => x <= 0)) return;
            var show = tops.Count;
            if (widths.Sum() > avail)
            {
                var room = avail - 40; double used = 0; show = 0;
                while (show < tops.Count && used + widths[show] <= room) used += widths[show++];
                show = Math.Max(1, show);
            }
            var hidden = tops.Skip(show).ToList();
            if (hidden.Count == tops.Count - show && tops.Take(show).All(t => t.IsVisible) && hidden.All(t => !t.IsVisible) && more.IsVisible == hidden.Count > 0) return;
            for (var i = 0; i < tops.Count; i++) tops[i].IsVisible = i < show;
            more.Items.Clear();
            foreach (var h in hidden)
            {
                var i = tops.IndexOf(h);
                var proxy = new MenuItem { Header = ProgramMenu.WithAccess(menus[i].Title, keys[i]), Tag = keys[i] };
                AddItems(proxy, menus[i].Items, false);
                proxy.SubmenuOpened += (_, _) => Refresh(proxy);
                more.Items.Add(proxy);
            }
            more.IsVisible = hidden.Count > 0;
        });
        bar.Opened += (_, _) =>
        {
            // Avalonia öffnet bei Alt die zuletzt angelegte Leiste – gehört sie nicht zum aktiven Fenster, die richtige öffnen
            if (ActiveMenuBar() is { } right && !ReferenceEquals(right, bar))
            { bar.Close(); MenuActivate(right); }
        };
        bar.Closed += (_, _) =>
        {
            // Esc: wie unter Windows zurück an die Stelle, an der vorher gearbeitet wurde
            if (FocusManager?.GetFocusedElement() is MenuItem fm && fm.FindLogicalAncestorOfType<Menu>() == bar && _menuWorkFocus is Control back && back.IsAttachedToVisualTree() && back.IsEffectivelyEnabled) back.Focus();
        };
        bar.AddHandler(KeyDownEvent, (_, e) => MenuLetter(bar, e), RoutingStrategies.Bubble);
        return bar;

        void AddItems(MenuItem parent, IEnumerable<MenuCmd> items, bool named)
        {
            var list = items.ToList();
            var ik = ProgramMenu.AccessKeys(list.Where(x => !x.IsSeparator).Select(x => x.Label).ToList());
            var k = 0;
            foreach (var c in list)
            {
                if (c.IsSeparator) { parent.Items.Add(new Separator()); continue; }
                parent.Items.Add(ItemOf(c, ik[k++], named));
            }
        }

        MenuItem ItemOf(MenuCmd c, char key, bool named)
        {
            var label = ProgramMenu.WithAccess(c.Label, key);
            object header = label;
            if (c.Shortcut.Length > 0)
                header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinWidth = 220, Children = {
                    new AccessText { Text = label, VerticalAlignment = VerticalAlignment.Center },
                    Pos(new TextBlock { Name = "menu-keys", Text = c.Shortcut, Foreground = Muted, Margin = new Thickness(28, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }, 1) } };
            var mi = new MenuItem { Name = named ? "menu-" + c.Id : null, Header = header, Tag = new MenuTag(c, key) }; // Kopien unter „…“ ohne Namen (die Namen gehören der Leiste)
            if (c.Tip is { Length: > 0 } tip) ToolTip.SetTip(mi, tip);
            if (c.Checked is not null) mi.ToggleType = MenuItemToggleType.CheckBox;
            if (c.Sub is { } sub) AddItems(mi, sub, named);
            else mi.Click += (_, e) => { e.Handled = true; if (c.CanRun) c.Run!(); };
            if (c.Sub is not null) mi.SubmenuOpened += (_, _) => Refresh(mi);
            Paint(mi, c);
            return mi;
        }
    }

    sealed record MenuTag(MenuCmd Cmd, char Key);

    readonly System.Runtime.CompilerServices.ConditionalWeakTable<Menu, Action<double>> _menuFit = [];

    /// <summary>Menüleiste an die verfügbare Breite anpassen: was nicht passt, steht unter „…“.</summary>
    public void MenuBarFit(Menu bar, double avail) { if (_menuFit.TryGetValue(bar, out var fit)) fit(avail); }

    /// <summary>Ausgegraut, wenn nicht möglich; Untermenüs, wenn keiner ihrer Einträge möglich ist; Häkchen nach dem aktuellen Stand.</summary>
    static void Paint(MenuItem mi, MenuCmd c)
    {
        mi.IsEnabled = c.Sub is { } s ? ProgramMenu.Flat(s).Any(x => x.Sub is null && x.CanRun) : c.CanRun;
        if (c.Checked is { } chk) mi.IsChecked = chk();
    }

    static void Refresh(MenuItem parent)
    {
        foreach (var mi in parent.Items.OfType<MenuItem>()) if (mi.Tag is MenuTag t) Paint(mi, t.Cmd);
    }

    /// <summary>Menüleiste der gerade aktiven Programmseite (die erste, falls eine Seite mehrere zeigt).</summary>
    public Menu? ActiveMenuBar() => ActivePageRoot?.GetLogicalDescendants().OfType<Menu>().FirstOrDefault(m => m.Name == MenuBarName && m.IsEffectivelyVisible)
        ?? (Windows.Active is { } a && _frames.TryGetValue(a.Id, out var f) ? f.Frame.GetLogicalDescendants().OfType<ContentControl>().FirstOrDefault(c => c.Name == "win-menu-" + a.Id)?.Content as Menu : null);

    /// <summary>Leiste aktivieren wie Alt/F10 unter Windows: erstes Menü markiert, noch nichts aufgeklappt.</summary>
    public bool MenuActivate(Menu? bar = null)
    {
        bar ??= ActiveMenuBar();
        if (bar is null) return false;
        RememberWorkFocus();
        bar.Open();
        if (bar.Items.OfType<MenuItem>().FirstOrDefault(t => t.IsVisible) is { } first) first.Focus();
        return true;
    }

    /// <summary>Ein Menü der Leiste aufklappen (Alt+Buchstabe) und den ersten möglichen Eintrag markieren.</summary>
    public bool MenuOpenTop(char key, Menu? bar = null)
    {
        bar ??= ActiveMenuBar();
        var top = bar?.Items.OfType<MenuItem>().FirstOrDefault(t => t.Tag is char k && k != '\0' && char.ToUpperInvariant(k) == char.ToUpperInvariant(key));
        if (bar is null || top is null) return false;
        RememberWorkFocus();
        RememberWorkFocus();
        if (!top.IsVisible && bar.Items.OfType<MenuItem>().FirstOrDefault(t => t.Name == "menubar-mehr" && t.IsVisible) is { } more
            && more.Items.OfType<MenuItem>().FirstOrDefault(p => p.Tag is char pk && pk == (char)top.Tag!) is { } proxy)
        {
            bar.Open(); more.Focus(); more.Open(); proxy.Focus(); proxy.Open(); Refresh(proxy); // Menü steht gerade unter „…“
            proxy.Items.OfType<MenuItem>().FirstOrDefault(x => x.IsEffectivelyEnabled)?.Focus();
            return true;
        }
        bar.Open(); top.Focus(); top.Open(); Refresh(top);
        top.Items.OfType<MenuItem>().FirstOrDefault(x => x.IsEffectivelyEnabled)?.Focus();
        return true;
    }

    void RememberWorkFocus()
    {
        if (FocusManager?.GetFocusedElement() is { } f && f is not MenuItem) _menuWorkFocus = f;
    }

    /// <summary>Im offenen Menü: Buchstabe ohne Alt springt zum Eintrag mit diesem unterstrichenen Buchstaben (Untermenü öffnet, sonst ausführen).</summary>
    void MenuLetter(Menu bar, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift) || FocusManager?.GetFocusedElement() is not MenuItem cur) return;
        var ch = KeyChar(e.Key); if (ch == '\0') return;
        // offenes Untermenü der markierten Zeile (oberste Ebene) oder die Geschwister des markierten Eintrags
        var items = cur.Parent is MenuItem p ? p.Items.OfType<MenuItem>() : cur.IsSubMenuOpen ? cur.Items.OfType<MenuItem>() : bar.Items.OfType<MenuItem>();
        var hit = items.FirstOrDefault(i => i.IsEffectivelyEnabled && (i.Tag is MenuTag t ? t.Key : i.Tag is char c ? c : '\0') is var k && k != '\0' && char.ToUpperInvariant(k) == ch);
        if (hit is null) return;
        e.Handled = true;
        if (hit.Parent is Menu) { MenuOpenTop(ch, bar); return; }
        if (hit.ItemCount > 0) { hit.Focus(); hit.Open(); hit.Items.OfType<MenuItem>().FirstOrDefault(x => x.IsEffectivelyEnabled)?.Focus(); return; }
        bar.Close();
        hit.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    static char KeyChar(Key k) => k is >= Key.A and <= Key.Z ? (char)('A' + (k - Key.A)) : k is >= Key.D0 and <= Key.D9 ? (char)('0' + (k - Key.D0)) : '\0';

    /// <summary>Tasten für die Leiste im ganzen Fenster: F10 aktiviert, Alt+Buchstabe öffnet das Menü der aktiven Programmseite.
    /// Läuft nach den Bedienelementen (Bubble) – wer Alt+Buchstabe selbst nutzt (z. B. Alt+A im 3D-Netz), behält es. Alt allein übernimmt Avalonia (siehe <see cref="MenuBarOf"/>).</summary>
    void EnsureMenuKeys()
    {
        if (_menuKeysReady) return; _menuKeysReady = true;
        AddHandler(GotFocusEvent, (_, e) => { if (e.Source is IInputElement f and not MenuItem && (e.Source as Control)?.FindLogicalAncestorOfType<Menu>() is null) _menuWorkFocus = f; }, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Handled || SetupOverlayOpen) return;
            if (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.None) { if (ActiveMenuBar() is { } b) { if (b.IsOpen) b.Close(); else MenuActivate(b); e.Handled = true; } return; }
            if (e.KeyModifiers == KeyModifiers.Alt && KeyChar(e.Key) is var ch && ch != '\0' && MenuOpenTop(ch)) { e.Handled = true; return; }
            // T-0728: Nach dem Neuzeichnen einer Programmseite hat oft kein Element den Fokus – dann kommt die Taste nur beim Fenster an.
            // Die Kürzel der Menüleiste (Fachkürzel wie Videoschnitt S, Bildbearbeitung B) gelten trotzdem, wie in jedem Windows-Programm.
            if (CurrentView is { } cv && Programs.ContainsKey(cv) && FocusManager?.GetFocusedElement() is var fe && (fe is null || ReferenceEquals(fe, this))
                && ActiveMenuBar() is { Tag: IEnumerable<MenuTop> model } && MenuShortcut(model, e)) e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: false);
    }

    /// <summary>Tastenkürzel aus dem Modell auslösen (Programme der Hülle: Strg+O, Strg+Umschalt+S …). Einträge, die Textfelder oder das ganze Fenster selbst
    /// bedienen (Kopieren, Hilfe …), stehen nur zur Anzeige da. true = erledigt.</summary>
    public static bool MenuShortcut(IEnumerable<MenuTop> menus, KeyEventArgs e)
    {
        foreach (var (_, c) in ProgramMenu.WithShortcuts(menus))
        {
            if (ShowOnlyShortcut.Contains(c.Id) || ProgramMenu.Parse(c.Shortcut) is not { } p) continue;
            if (!Enum.TryParse<Key>(KeyName(p.Key), out var key) || key != e.Key) continue;
            var want = (p.Ctrl ? KeyModifiers.Control : 0) | (p.Shift ? KeyModifiers.Shift : 0) | (p.Alt ? KeyModifiers.Alt : 0);
            if (e.KeyModifiers != want) continue;
            // T-0728: Fachkürzel ohne Strg/Alt (Videoschnitt S, Bildbearbeitung B …) nie beim Tippen in Feldern
            if (!p.Ctrl && !p.Alt && TypingIn(e.Source)) continue;
            if (c.CanRun) c.Run!();
            return true;
        }
        return false;
    }

    /// <summary>Deutsche Tastennamen der Menüleiste → Avalonia-Taste (Entf, Leertaste, Pos1, Ende, Ziffern).</summary>
    static string KeyName(string k) => k.Length == 1 && char.IsDigit(k[0]) ? "D" + k : k switch { "Entf" => "Delete", "Leertaste" => "Space", "Pos1" => "Home", "Ende" => "End", _ => k };

    /// <summary>Tippt die Person gerade in ein Feld (Text, Zahl, Auswahl-Liste)? Dann gehören einfache Tasten dem Feld.</summary>
    static bool TypingIn(object? source) => source is Control c && (c is TextBox or ComboBox or ListBox or AutoCompleteBox or NumericUpDown
        || c.FindLogicalAncestorOfType<TextBox>() is not null || c.FindLogicalAncestorOfType<AutoCompleteBox>() is not null || c.FindLogicalAncestorOfType<NumericUpDown>() is not null);

    /// <summary>T-0728: Kürzel am Ende einer Menüband-Beschriftung („Teilen (S)“, „Löschen (Entf)“) – in der Menüleiste steht es rechts und gilt im Programm.</summary>
    internal static (string Label, string Keys) RibbonKey(string label)
    {
        var m = System.Text.RegularExpressions.Regex.Match(label, @"^(.*?)\s*\(((?:Strg|Alt|Umschalt)\+[^)]+|F\d{1,2}|Entf|Leertaste|Pos1|Ende|[A-Z0-9])\)\s*$");
        return m.Success ? (m.Groups[1].Value.Trim(), m.Groups[2].Value) : (label, "");
    }

    /// <summary>Kürzel, die schon jemand anderes ausführt: Textfelder (Ausschneiden, Kopieren, Einfügen, Alles markieren) und das Fenster (Hilfe, Befehlsleiste, Fokusmodus).</summary>
    internal static readonly HashSet<string> ShowOnlyShortcut = ["edit-cut", "edit-copy", "edit-paste", "edit-selectall", "help-show", "help-palette", "help-keys", "view-focus"];

    // ---------- gemeinsame Einträge ----------

    /// <summary>Bearbeiten: Ausschneiden · Kopieren · Einfügen · Alles markieren – wirken auf das zuletzt benutzte Textfeld der Seite (wie unter Windows).</summary>
    List<MenuCmd> ClipboardCmds()
    {
        TextBox? Box() => _menuWorkFocus is TextBox t && t.IsAttachedToVisualTree() && ActivePageRoot is { } r && t.GetVisualAncestors().Contains(r) ? t : null;
        bool Sel() => Box() is { } t && t.SelectionStart != t.SelectionEnd;
        return
        [
            new("edit-cut", "Ausschneiden", () => { if (Box() is { } t) { t.Focus(); t.Cut(); } }, ProgramMenu.Keys.Ausschneiden, () => Sel() && Box()!.IsReadOnly == false),
            new("edit-copy", "Kopieren", () => { if (Box() is { } t) { t.Focus(); t.Copy(); } }, ProgramMenu.Keys.Kopieren, Sel),
            new("edit-paste", "Einfügen", () => { if (Box() is { } t) { t.Focus(); t.Paste(); } }, ProgramMenu.Keys.Einfuegen, () => Box() is { IsReadOnly: false }),
            new("edit-selectall", "Alles markieren", () => { if (Box() is { } t) { t.Focus(); t.SelectAll(); } }, ProgramMenu.Keys.AllesMarkieren, () => Box() is not null),
        ];
    }

    /// <summary>Ansicht › Bedienstufe – dieselbe Umschaltung wie der Stufenschalter im Kopf.</summary>
    MenuCmd LevelMenu(string key) => new("view-level", "Bedienstufe", Sub: [.. Enumerable.Range(0, 3).Select(i =>
        new MenuCmd("view-level-" + UserLevel.Ids[i], UserLevel.Names[i], () => ProgramSetLevel(i, key), Checked: () => ProgramLevel == i))]);

    /// <summary>KI › Auftrag an die KI: blendet die Auftragszeile ein (wie „Auftragszeile zeigen“ im Menü „KI ▾“) und setzt den Cursor hinein –
    /// der Auftrag läuft über den einen Assistenzweg (<see cref="AssistRouteFor"/>, T-0707).</summary>
    MenuCmd KiOrder(string key) => new("ki-order", "Auftrag an die KI …", () =>
    {
        if (ActivePageRoot?.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt") is not { } box)
        {
            _promptBarOpen.Add(key); Show(key);
            box = ActivePageRoot?.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt");
        }
        if (box is not null) { _ui.Post(() => box.Focus()); _menuWorkFocus = box; }
    }, Enabled: () => ModuleOf(key) is not null && Trendo.Core.PromptAction.CapabilityCatalog.Find(ModuleOf(key)!) is not null || ActivePageRoot?.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Name == "auftrag-prompt") == true,
        Tip: "Schreib in normalen Worten, was entstehen oder sich ändern soll – du siehst zuerst den Plan und entscheidest dann");

    /// <summary>KI › Claude: dasselbe wie der Eintrag „Claude“ im Menü „KI ▾“.</summary>
    MenuCmd? KiClaude(string key) => ClaudeBelongs(key) && ClaudeStudioOf(key) is { } ks ? new MenuCmd("ki-claude", "Claude öffnen", () => ClaudeDockToggle(ks, null), Tip: "Claude für dieses Studio – mit deinem Abo") : null;

    /// <summary>Hilfe: Hilfe zum Programm (F1, wie „?“), alle Tastenkürzel, Befehl suchen.</summary>
    List<MenuCmd> HelpCmds(string key, string title) =>
    [
        new("help-show", $"Hilfe zu „{title}“", () => ShowHelp(key, force: true), ProgramMenu.Keys.Hilfe),
        new("help-keys", "Alle Tastenkürzel", ShowShortcuts, ProgramMenu.Keys.Tastenkuerzel),
        new("help-palette", "Befehl suchen …", () => OpenPalette(), ProgramMenu.Keys.Befehle),
    ];

    // ---------- Programme außerhalb der Hülle (T-0708-Umzugsliste): Einträge aus ihren vorhandenen Knöpfen ----------

    /// <summary>Ein Eintrag, der genau den vorhandenen Knopf (oder Menüeintrag) des Programms auslöst – gefunden über den Namen auf der Seite,
    /// auch in „Mehr …“-Klappfeldern. Fehlt das Element oder ist es gesperrt, ist der Eintrag ausgegraut.</summary>
    MenuCmd ByControl(Func<Control?> page, string name, string label, string shortcut = "", string? id = null)
    {
        Control? Find()
        {
            if (page() is not { } root) return null;
            foreach (var c in AllControls(root)) if (c.Name == name) return c;
            return null;
        }
        return new MenuCmd(id ?? name, label, () =>
        {
            switch (Find())
            {
                case MenuItem mi: mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); break;
                case Button { Flyout: { } f } b when b.IsAttachedToVisualTree(): f.ShowAt(b); break;
                case Button b: b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); break;
            }
        }, shortcut, () => Find() is { } c && c.IsEnabled && (c is not Button || c.GetLogicalAncestors().OfType<Control>().All(a => a.IsEnabled)));
    }

    /// <summary>Alle Bedienelemente einer Seite im logischen Baum – auch in Klappfeldern (Flyout), Kontextmenüs und Menüs der Knöpfe.</summary>
    static IEnumerable<Control> AllControls(Control root)
    {
        var stack = new Stack<ILogical>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var l = stack.Pop();
            if (l is Control c)
            {
                yield return c;
                if (c.ContextMenu is { } cm) foreach (var mi in cm.Items.OfType<Control>()) stack.Push(mi);
                if (c is Button { Flyout: Flyout { Content: ILogical fc } }) stack.Push(fc);
                if (c is Button { Flyout: MenuFlyout mf }) foreach (var mi in mf.Items.OfType<Control>()) stack.Push(mi);
            }
            foreach (var ch in l.LogicalChildren) stack.Push(ch);
        }
    }

    /// <summary>Einträge eines vorhandenen Klappmenüs (z. B. „Datei“ in Tabelle und Präsentation) übernehmen – jeder löst den Originaleintrag aus.
    /// Ein Kürzel in Klammern am Ende („Speichern  (Strg+S)“) wandert nach rechts.</summary>
    List<MenuCmd> MirrorMenu(Func<Control?> page, string ownerName)
    {
        var owner = page() is { } root ? AllControls(root).FirstOrDefault(c => c.Name == ownerName) : null;
        var items = owner?.ContextMenu?.Items.Cast<object>() ?? ((owner as Button)?.Flyout as MenuFlyout)?.Items.Cast<object>();
        var l = new List<MenuCmd>();
        if (items is null) return l;
        foreach (var o in items)
        {
            if (o is Separator) { l.Add(MenuCmd.Separator); continue; }
            if (o is not MenuItem { Name: { Length: > 0 } n } mi || mi.Header is not string h) continue;
            var (label, keys) = SplitShortcut(h);
            l.Add(ByControl(page, n, label, keys, "x-" + n));
        }
        return l;
    }

    static (string Label, string Keys) SplitShortcut(string header)
    {
        var m = System.Text.RegularExpressions.Regex.Match(header, @"^(.*?)\s*\(((?:Strg|Alt|Umschalt|F\d)[^)]*)\)\s*$");
        return m.Success ? (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim()) : (header.Trim(), "");
    }

    // ---------- Programme mit eigener Werkzeugleiste (V-14) und über ProgramLink angeschlossene ----------

    /// <summary>Programme außerhalb der Hülle mit Menüleiste: Seite → Menüs aus ihren vorhandenen Knöpfen. Kürzel stehen nur dort, wo das Programm sie selbst auslöst
    /// (geprüft im Code: Text <c>TextShortcut</c>, Tabelle/Präsentation <c>Command</c>, Geld <c>KeyDown</c>, CAD <c>CadWireInput</c>, 3D <c>Studio3DViewport</c>, Code-Editor).</summary>
    public static readonly IReadOnlyList<string> ExternalMenuPages = ["text", "tabelle", "praesentation", "finanzen", "brief", "posteingang", "cad", "3d", "code",
        // T-0711: über ProgramLink angeschlossen (Rückgängig/Wiederholen der Hülle) – „Bearbeiten“ entsteht aus ihren Knöpfen, sobald der Editor offen ist
        "editor", "post", "pdf", "musik-studio", "spiele", "recherche", "ki-wissen", "ki-vorlagen", "ki-skills", "nachbau"];

    /// <summary>Menüleiste der Seite, die gerade gebaut wird – <see cref="PresentPage"/> setzt sie in die Titelleiste ihres Fensters.</summary>
    Menu? _titleMenu;

    /// <summary>Menüleiste für ein Programm außerhalb der Hülle: Sie steht in der Titelleiste seines Fensters (wie in VS Code oder Office) – keine zusätzliche Zeile,
    /// die Arbeitsfläche von Text, 3D … wird nicht kleiner. Mac und GNOME behalten ihre eigene Art (Menüleiste oben bzw. ☰).</summary>
    Control WithExternalMenu(string key, Control view)
    {
        if (!(ExternalMenuPages.Contains(key) || _slots.Values.Any(x => x.Page == key)) || Programs.ContainsKey(key) || OsStyle && _style != "windows") return view;
        if (ExternalMenus(key, () => view) is { } menus) _titleMenu = MenuBarOf(menus);
        return view;
    }

    /// <summary>Menüleiste in der Titelleiste: was neben Zurück, Symbol und einem lesbaren Fenstertitel nicht passt, steht unter „…“.</summary>
    void FitTitleMenu(ContentControl slot)
    {
        if (slot.Parent is not Grid g) return;
        void Fit()
        {
            if (slot.Content is not Menu bar || g.Bounds.Width <= 0) return;
            var others = g.Children.Where(c => !ReferenceEquals(c, slot) && c.IsVisible && Grid.GetColumn(c) != 3).Sum(c => c.DesiredSize.Width + c.Margin.Left + c.Margin.Right);
            MenuBarFit(bar, g.Bounds.Width - others - 160); // 160 px bleiben für den Fenstertitel
        }
        // T-0730: OnSize ist bei jedem Aufbau ein neuer Delegat – „-= OnSize“ träfe den alten nie. Deshalb je Titelleiste genau einen Handler merken und vor dem neuen abmelden.
        if (TitleFitHandlers.TryGetValue(g, out var old)) g.SizeChanged -= old;
        EventHandler<SizeChangedEventArgs> handler = OnSize;
        TitleFitHandlers.AddOrUpdate(g, handler); g.SizeChanged += handler;
        Trendo.App.UiThread.Post(Fit, Avalonia.Threading.DispatcherPriority.Background);
        void OnSize(object? _, SizeChangedEventArgs e) => Trendo.App.UiThread.Post(Fit, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>T-0730: angemeldeter Größen-Handler je Titelleisten-Raster (schwach gehalten, verschwindet mit dem Raster).</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Grid, EventHandler<SizeChangedEventArgs>> TitleFitHandlers = new();

    List<MenuTop>? ExternalMenus(string key, Func<Control?> page)
    {
        MenuCmd B(string name, string label, string keys = "") => ByControl(page, name, label, keys);
        // Startseite (noch kein Dokument): dort gibt es das Klappmenü „Datei“ noch nicht – dann der Knopf der Startseite
        static List<MenuCmd> Or(List<MenuCmd> l, MenuCmd alt) => l.Count > 0 ? l : [alt];
        MenuCmd Sub(string id, string label, List<MenuCmd> items) => new(id, label, Sub: items);
        var K = ProgramMenu.Keys.Rueckgaengig; var Y = ProgramMenu.Keys.Wiederholen; var S = ProgramMenu.Keys.Speichern;
        List<MenuTop> m = key switch
        {
            "text" =>
            [
                new(ProgramMenu.Datei, [Sub("text-new-menu", "Neu", MirrorMenu(page, "text-new")), B("text-open", "Öffnen …"), MenuCmd.Separator, B("text-save", "Speichern", S), B("text-saveas", "Speichern unter …"),
                    MenuCmd.Separator, B("text-pdf", "Als PDF speichern"), B("text-print", "Drucken …")]),
                new(ProgramMenu.Bearbeiten, [B("text-undo", "Rückgängig", K), B("text-redo", "Wiederholen", Y), MenuCmd.Separator, B("text-find", "Suchen und Ersetzen", ProgramMenu.Keys.Suchen)]),
                new("Format", [B("text-bold", "Fett", "Strg+B"), B("text-italic", "Kursiv", "Strg+I"), B("text-underline", "Unterstrichen", "Strg+U"), MenuCmd.Separator,
                    B("text-bullets", "Aufzählung"), B("text-numbers", "Nummerierung"), B("text-list-in", "Listenebene tiefer", "Tab"), B("text-list-out", "Listenebene höher", "Umschalt+Tab"), MenuCmd.Separator,
                    Sub("text-align", "Ausrichtung", [B("text-align-left", "Linksbündig"), B("text-align-center", "Zentriert"), B("text-align-right", "Rechtsbündig"), B("text-align-justify", "Blocksatz")]),
                    MenuCmd.Separator, B("text-clear-format", "Formatierung löschen")]),
            ],
            "tabelle" => [new(ProgramMenu.Datei, Or(MirrorMenu(page, "tabelle-file"), B("tabelle-open", "Öffnen …"))), new(ProgramMenu.Bearbeiten, [B("tabelle-undo", "Rückgängig", K), B("tabelle-redo", "Wiederholen", Y)])],
            "praesentation" => [new(ProgramMenu.Datei, Or(MirrorMenu(page, "praesentation-file"), B("praesentation-open", "Öffnen …"))), new(ProgramMenu.Bearbeiten, [B("praesentation-undo", "Rückgängig", K), B("praesentation-redo", "Wiederholen", Y)])],
            "finanzen" =>
            [
                new(ProgramMenu.Datei, [B("geld-import-open", "Kontoauszug öffnen …", ProgramMenu.Keys.Oeffnen)]),
                new(ProgramMenu.Bearbeiten, [B("geld-undo", "Rückgängig", K), B("geld-redo", "Wiederholen", Y)]),
                new("Bereich", [.. GeldTabs.Select(t => B("geld-tab-" + t.Id, t.Label))]),
            ],
            "brief" =>
            [
                new(ProgramMenu.Datei, [B("letter-new", "Neuer Brief"), MenuCmd.Separator, B("letter-save", "Speichern"), B("letter-export", "Exportieren …"), B("letter-pdf", "Als PDF"), MenuCmd.Separator, B("letter-send", "Senden …")]),
                new(ProgramMenu.Bearbeiten, [B("letter-undo", "Rückgängig"), B("letter-redo", "Wiederholen"), MenuCmd.Separator, B("letter-check", "Brief prüfen")]),
            ],
            "posteingang" =>
            [
                new(ProgramMenu.Datei, [B("send-draft", "Entwurf speichern"), B("inbox-export", "Als .eml exportieren")]),
                new(ProgramMenu.Bearbeiten, [B("send-ki-undo", "KI-Text rückgängig")]),
            ],
            "cad" =>
            [
                new(ProgramMenu.Datei, [.. Or(MirrorMenu(page, "cad-file"), B("cad-save", "Speichern", S)).Select(c => c.Id == "x-cad-m-save" ? c with { Shortcut = S } : c)]),
                new(ProgramMenu.Bearbeiten, [B("cad-undo", "Rückgängig", K), B("cad-redo", "Wiederholen", Y)]),
            ],
            "3d" =>
            [
                new(ProgramMenu.Datei, [B("3d-save", "Speichern"), Sub("3d-export-menu", "Exportieren", MirrorMenu(page, "3d-export"))]),
                new(ProgramMenu.Bearbeiten, [B("3d-undo", "Rückgängig", K), B("3d-redo", "Wiederholen", Y)]),
            ],
            "code" =>
            [
                new(ProgramMenu.Datei, [B("ce-save", "Speichern", S), B("ce-save-all", "Alle speichern", "Strg+Umschalt+S")]),
                new(ProgramMenu.Bearbeiten, [B("ce-undo", "Rückgängig", K), B("ce-redo", "Wiederholen", Y)]),
                new("Ausführen", [B("ce-run", "Ausführen", "F5")]),
            ],
            // T-0715 (Hülle E3): Live streamen und Medien bearbeiten – „Datei“ ruft die vorhandenen Knöpfe, „Bearbeiten“ kommt unten aus ihrem ProgramLink
            "sendung" => [new(ProgramMenu.Datei, [B("bc-export", "Ablaufplan als CSV"), B("bc-chapters", "YouTube-Kapitel"), B("bc-cards", "Moderationskarten")])],
            "kreativ-medien" => [new(ProgramMenu.Datei, [B("media-cut", "Ausschnitt speichern"), B("img-save", "Bild speichern"), B("media-concat", "Clips aneinanderhängen"), MenuCmd.Separator, B("media-folder", "Medienordner öffnen")])],
            _ => [],
        };
        // T-0711: über ProgramLink angeschlossene Programme (Editor, E-Mail, PDF ordnen, Musik, Spiele, Recherche, Wissen, KI-Vorlagen, Skills, Nachbau) –
        // „Bearbeiten“ ruft ihre Knöpfe Rückgängig/Wiederholen der Hülle (Namen „<Vorsilbe>-undo/-redo“ aus LinkButtons)
        var linked = _slots.Values.Where(x => x.Page == key).ToList();
        if (linked.Count > 0)
        {
            var edit = new List<MenuCmd>();
            foreach (var x in linked)
            {
                if (edit.Count > 0) edit.Add(MenuCmd.Separator);
                var what = linked.Count > 1 ? " – " + x.One.Split(' ', 2)[^1] : "";
                edit.Add(B(x.Prefix + "-undo", "Rückgängig" + what)); edit.Add(B(x.Prefix + "-redo", "Wiederholen" + what));
            }
            m.Add(new(ProgramMenu.Bearbeiten, edit));
            if (key == "editor") m.Insert(0, new(ProgramMenu.Datei, [B("ed-new", "Neu"), B("ed-open", "Öffnen …"), B("ed-save", "Speichern", S)]));
        }
        if (m.Count == 0 && !ExternalMenuPages.Contains(key)) return null;
        var title = TitleOfPage(key);
        m.Add(new(ProgramMenu.Ansicht, ProgramViewCmds(key)));
        m.Add(new(ProgramMenu.Ki, ProgramKiCmds(key)));
        m.Add(new(ProgramMenu.Hilfe, HelpCmds(key, title)));
        return m;
    }

    string TitleOfPage(string key) => PresentTitle(key);

    // ---------- Programme der Hülle ----------

    /// <summary>Handler der Datei- und Bearbeiten-Befehle eines offenen Dokuments – dieselben wie die Knöpfe im Menüband.</summary>
    internal sealed record ShellActs(Action New, Action Open, Action Save, Action SaveAs, Action Close, Action Undo, Action Redo,
        Func<ProgramCmd, Action> Cmd, Func<ProgramExport, Action> Export, Func<(string File, DateTime When), Action> Version);

    /// <summary>Neues Dokument (auch aus einer Vorlage) – derselbe Handler wie die Vorlagen-Kacheln der Startseite.</summary>
    Action ProgramNewAct(string key, string? template = null) => () => ProgramRun(key, () => ProgramNew(key, template));
    /// <summary>Zuletzt geöffnetes Dokument öffnen – derselbe Handler wie die Liste „Zuletzt“.</summary>
    Action ProgramOpenAct(string key, string path) => () => ProgramRun(key, () => ProgramOpen(key, path));

    List<MenuCmd> ProgramFileStart(ProgramSpec spec)
    {
        var key = spec.Key;
        var l = new List<MenuCmd> { new("prog-new", "Neu", ProgramNewAct(key), ProgramMenu.Keys.Neu) };
        if (spec.Templates.Count > 1) l.Add(new("prog-new-from", "Neu aus Vorlage", Sub: [.. spec.Templates.Select(t => new MenuCmd("prog-tpl-" + t.Id, t.Name, ProgramNewAct(key, t.Id), Tip: t.Hint))]));
        l.Add(new("prog-open-pick", "Öffnen …", () => _ = ProgramOpenPick(key), ProgramMenu.Keys.Oeffnen));
        var recent = ProgRecent.Get(spec.Studio).Take(9).ToList();
        l.Add(new("prog-recent", "Zuletzt geöffnet", Sub: recent.Count == 0 ? [new MenuCmd("prog-recent-none", "Noch nichts gespeichert")]
            : [.. recent.Select((f, i) => new MenuCmd($"prog-recent-{i}", $"{i + 1} {Path.GetFileNameWithoutExtension(f)}", ProgramOpenAct(key, f), Tip: f))]));
        return l;
    }

    List<MenuCmd> ProgramViewCmds(string key) => [LevelMenu(key), new("view-focus", "Fokusmodus", ToggleFocusMode, ProgramMenu.Keys.Vollbild, Checked: () => FocusMode)];

    List<MenuCmd> ProgramKiCmds(string key) => KiClaude(key) is { } c ? [KiOrder(key), c] : [KiOrder(key)];

    /// <summary>Menüleiste der Startseite eines Programms (kein Dokument offen).</summary>
    List<MenuTop> ProgramStartMenus(ProgramSpec spec) =>
    [
        new(ProgramMenu.Datei, ProgramFileStart(spec)),
        new(ProgramMenu.Ansicht, ProgramViewCmds(spec.Key)),
        new(ProgramMenu.Ki, ProgramKiCmds(spec.Key)),
        new(ProgramMenu.Hilfe, [.. HelpCmds(spec.Key, spec.Title), MenuCmd.Separator, new("prog-intro-show", "Einführung", () => ProgramIntroShow(spec.Key))]),
    ];

    /// <summary>Ein vorhandener Knopf als Menüeintrag (Klick oder sein Klappfeld) – für Einträge, deren Knopf schon gebaut ist (Hilfe, Team, freie KI, Profi-Werkzeuge).</summary>
    static MenuCmd FromButton(Button b, string label, string? id = null) => new(id ?? b.Name ?? label, label, () =>
    {
        if (b.Flyout is { } f) { if (b.IsAttachedToVisualTree()) f.ShowAt(b); }
        else b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }, Enabled: () => b.IsEnabled && (b.Flyout is null || b.IsAttachedToVisualTree()));

    static string LabelOf(Button b) => (b.Content switch { string s => s, TextBlock t => t.Text ?? "", _ => b.Name ?? "" }).Trim().TrimStart('?', ' ').Trim();

    /// <summary>Menüleiste eines offenen Dokuments: Datei · Bearbeiten · Ansicht · Werkzeuge · Menüband-Gruppen des Programms · KI · Hilfe.
    /// In „Einfach“ enthält sie alles aus „Erweitert“ (keine Funktion verschwindet), Profi-Einträge nur in „Professionell“.</summary>
    internal List<MenuTop> ShellMenus(ProgramSpec spec, ProgramDocument d, ShellActs a, IReadOnlyList<Control> help, IReadOnlyList<Control> pro)
    {
        var key = spec.Key;
        var level = Math.Max(ProgramLevel, UserLevel.Erweitert);
        var file = new List<MenuCmd> { new("prog-new", "Neu", a.New, ProgramMenu.Keys.Neu) };
        file.AddRange(ProgramFileStart(spec).Skip(1)); // Vorlagen, Öffnen, Zuletzt
        file.Add(MenuCmd.Separator);
        file.Add(new("prog-save", "Speichern", a.Save, ProgramMenu.Keys.Speichern, () => !(d.ReadOnly && d.Path is not null)));
        file.Add(new("prog-saveas", "Speichern unter …", a.SaveAs, ProgramMenu.Keys.SpeichernUnter));
        var vs = d.Path is null ? [] : ProgramDocument.Versions(d.Path);
        file.Add(new("prog-versions", "Frühere Versionen", Sub: vs.Count == 0 ? [new MenuCmd("prog-versions-none", "Noch keine")] : [.. vs.Take(20).Select((v, i) => new MenuCmd($"prog-version-{i}", v.When.ToString("dd.MM.yyyy  HH:mm:ss"), a.Version(v)))]));
        if (spec.Exports.Count > 0)
        {
            file.Add(MenuCmd.Separator);
            file.Add(new("prog-export", "Exportieren", Sub: [.. spec.Exports.Select(x => new MenuCmd("prog-export-" + x.Name, x.Label, a.Export(x)))]));
        }
        file.Add(MenuCmd.Separator);
        file.Add(new("prog-close", "Schließen", a.Close, ProgramMenu.Keys.Schliessen));

        var edit = new List<MenuCmd>
        {
            new("prog-undo", d.UndoLabel is { } u ? "Rückgängig: " + u : "Rückgängig", a.Undo, ProgramMenu.Keys.Rueckgaengig, () => d.CanUndo),
            new("prog-redo", "Wiederholen", a.Redo, ProgramMenu.Keys.Wiederholen, () => d.CanRedo),
            MenuCmd.Separator,
        };
        edit.AddRange(ClipboardCmds());
        if (pro.Count > 0) { edit.Add(MenuCmd.Separator); foreach (var b in pro.OfType<Button>()) edit.Add(FromButton(b, LabelOf(b).Split(" (")[0] + " …")); }

        bool Hidden(string part) => _settings.HiddenPanels.Contains(key + ":" + part);
        var view = new List<MenuCmd>();
        if (spec.Tools.Count > 0) view.Add(new("view-tools", "Werkzeugleiste", () => ProgramTogglePanel(key, "werkzeuge"), Checked: () => !Hidden("werkzeuge")));
        if (spec.Properties is not null) view.Add(new("view-props", "Eigenschaften", () => ProgramTogglePanel(key, "eigenschaften"), Checked: () => !Hidden("eigenschaften") && !SimpleCollapsed(key)));
        if (spec.Bottom is { } bottom) view.Add(new("view-bottom", bottom.Title, () => ProgramTogglePanel(key, "unten"), Checked: () => !Hidden("unten")));
        view.Add(MenuCmd.Separator);
        view.AddRange(ProgramViewCmds(key));

        var menus = new List<MenuTop> { new(ProgramMenu.Datei, file), new(ProgramMenu.Bearbeiten, edit), new(ProgramMenu.Ansicht, view) };
        if (spec.Tools.Count > 0)
            menus.Add(new("Werkzeuge", [.. spec.Tools.Select(t => new MenuCmd("tool-" + t.Name, t.Label, () => ProgramRun(key, () => t.Select(d)), t.Shortcut, Checked: t.Active, Tip: t.Tip))]));
        foreach (var (g, cmds) in spec.Ribbon)
        {
            if (!UserLevel.Shows(level, spec.GroupLevels.GetValueOrDefault(g))) continue;
            var items = cmds.Where(c => UserLevel.Shows(level, spec.LevelOf(c))).Select(c => { var (lab, keys) = RibbonKey(c.Label); return new MenuCmd("prog-cmd-" + c.Name, lab, a.Cmd(c), keys, Tip: c.Tip); }).ToList();
            if (items.Count > 0) menus.Add(new(spec.MenuNames.GetValueOrDefault(g, g), items));
        }
        if (spec.Menus is { } extra) menus.AddRange(extra(d));
        menus.Add(new(ProgramMenu.Ki, ProgramKiCmds(key)));
        var helpItems = HelpCmds(key, spec.Title);
        helpItems.Add(MenuCmd.Separator);
        foreach (var b in help.OfType<Button>())
            helpItems.Add(b.Name switch
            {
                "prog-help" => FromButton(b, "Kurzhilfe"),
                "prog-intro-show" => FromButton(b, "Einführung"),
                "page-team" => FromButton(b, "Team des Studios"),
                _ => FromButton(b, LabelOf(b), "help-" + b.Name),
            });
        menus.Add(new(ProgramMenu.Hilfe, helpItems));
        return menus;
    }
}
