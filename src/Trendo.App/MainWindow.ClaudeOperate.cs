using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude kennt und bedient jedes Programm (Wunsch 02.11.2026): Trendo liest die offene Seite (Überschriften, Markierung, Felder, Auswahllisten, Knöpfe) und gibt sie
/// Claude mit; Claude kann Felder ausfüllen, Einträge wählen und Knöpfe drücken – erst nach „Ausführen“, Knöpfe mit Wirkung nach außen deutlich markiert.
/// Dazu „✳ Claude fragen“ per Rechtsklick auf alles im Programm und Claude im Menüband jedes Fachprogramms.</summary>
public partial class MainWindow
{
    // ───────────── Claude kennt jedes Programm ─────────────
    (string Page, string Text)? _lastTextSel; // zuletzt markierter Text in einem Eingabefeld (bleibt, auch wenn der Fokus in den Chat wandert)

    void InitClaudeOperate()
    {
        void Remember(object? src)
        {
            if (src is TextBox tb && !InsideClaude(tb) && tb.PasswordChar == default && tb.SelectedText is { Length: > 0 } s) _lastTextSel = (CurrentView, s);
        }
        AddHandler(PointerReleasedEvent, (_, e) => Remember(e.Source), RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(KeyUpEvent, (_, e) => Remember(e.Source), RoutingStrategies.Bubble, true);
        AddHandler(ContextRequestedEvent, OnClaudeContext, RoutingStrategies.Tunnel);
    }

    static bool InsideClaude(Control c) => c.GetSelfAndLogicalAncestors().OfType<Control>().Any(a => a.Name is "claude-dock" or "auftrag-leiste" or "command-palette" or "toast-host" or "claude-plan");

    /// <summary>Was auf der offenen Seite steht – Felder, Auswahllisten, Knöpfe, sichtbare Texte, Markierung (öffentlich für Tests). Geheimnisfelder bleiben draußen.</summary>
    public ClaudeActions.Page? ClaudeReadPage(string page)
    {
        if (ActivePageRoot is not { } root) return null;
        var all = PageControls(root).Where(c => c.IsVisible && !InsideClaude(c)).ToList();
        var fields = new List<ClaudeActions.Field>(); var choices = new List<ClaudeActions.Choice>(); var buttons = new List<ClaudeActions.Button>(); var texts = new List<string>();
        foreach (var c in all)
            switch (c)
            {
                case TextBox tb when tb.Name is { Length: > 0 } n && tb.IsEffectivelyEnabled && !tb.IsReadOnly && tb.PasswordChar == default:
                {
                    var label = tb.PlaceholderText ?? Avalonia.Automation.AutomationProperties.GetName(tb) ?? n;
                    if (ClaudeActions.Secret(label, n)) break;
                    fields.Add(new(n, label, tb.Text ?? "")); break;
                }
                case ComboBox cb when cb.Name is { Length: > 0 } n && cb.IsEffectivelyEnabled:
                {
                    var opts = cb.Items.Select(ItemText).Where(x => x.Length > 0).Take(40).ToList();
                    if (opts.Count > 0) choices.Add(new(n, Avalonia.Automation.AutomationProperties.GetName(cb) ?? (ToolTip.GetTip(cb) as string) ?? n, cb.SelectedItem is { } si ? ItemText(si) : null, opts));
                    break;
                }
                case Button b when b.Name is { Length: > 0 } n && b.IsEffectivelyEnabled && !n.StartsWith("claude-", StringComparison.Ordinal) && !n.StartsWith("auftrag-", StringComparison.Ordinal) && n is not ("btn-help" or "prog-claude"):
                {
                    var label = ButtonText(b);
                    if (label.Length is > 0 and <= 60) buttons.Add(new(n, label));
                    break;
                }
                case TextBlock t when t.Text is { Length: >= 3 and <= 160 } s && t.FindLogicalAncestorOfType<Button>() is null && texts.Count < 40 && !texts.Contains(s):
                    texts.Add(s); break;
            }
        fields.AddRange(ClaudeExtraFields(page)); // Programm-eigene Inhalte, die keine Eingabefelder sind (z. B. Level in „Spiele bauen“)
        fields.AddRange(ClaudeMusicFields(page)); // Trendo Musik: Claude als Produzent (Text, Stil, Mastering-Ziel)
        var program = Windows.Active?.Title is { Length: > 0 } wt ? wt : StudioCatalog.NameOf(ModuleOf(page) ?? "", page);
        var title = ClaudeFocus(page) ?? (Programs.ContainsKey(page) && ProgramActive(page) is { } doc ? "Dokument „" + doc.Title + "“" : "");
        var sel = ClaudeSelection(page) ?? (_lastTextSel is { } ls && ls.Page == page ? ls.Text : null);
        return new(program, title, sel, texts, fields, choices, buttons.GroupBy(b => b.Name).Select(g => g.First()).ToList());
    }

    /// <summary>Alle Elemente der Seite – auch die in Aufklapp-Menüs (Flyout) von Knöpfen, die erst beim Aufklappen sichtbar werden.</summary>
    static IEnumerable<Control> PageControls(Control root)
    {
        foreach (var c in root.GetLogicalDescendants().OfType<Control>())
        {
            yield return c;
            if (c is Button { Flyout: Flyout { Content: Control fc } })
                foreach (var f in fc.GetSelfAndLogicalDescendants().OfType<Control>()) yield return f;
        }
    }

    /// <summary>Pause zwischen zwei Bedien-Schritten, damit man sieht, wo Claude arbeitet (Tests: 0).</summary>
    public static int ClaudeStepDelay { get; set; } = 350;

    /// <summary>Claude zeigt, wo es arbeitet: das Element leuchtet kurz mit Gold-Blau-Kante auf.</summary>
    void ClaudeFlash(Control c)
    {
        if (c is TemplatedControl tc)
        {
            var (brush, thick) = (tc.BorderBrush, tc.BorderThickness);
            tc.BorderBrush = GlassEdge; tc.BorderThickness = new Thickness(2.5);
            DispatcherTimer.RunOnce(() => { tc.BorderBrush = brush; tc.BorderThickness = thick; }, TimeSpan.FromMilliseconds(1300));
        }
        else if (c is Border b)
        {
            var (brush, thick) = (b.BorderBrush, b.BorderThickness);
            b.BorderBrush = GlassEdge; b.BorderThickness = new Thickness(2.5);
            DispatcherTimer.RunOnce(() => { b.BorderBrush = brush; b.BorderThickness = thick; }, TimeSpan.FromMilliseconds(1300));
        }
        c.BringIntoView();
        _claudeFlashed = c.Name;
    }
    string? _claudeFlashed;
    /// <summary>Für Tests: zuletzt aufgeleuchtetes Element.</summary>
    public string? ClaudeLastFlashed => _claudeFlashed;

    /// <summary>Genauere Leser für die wichtigsten Programme: was gerade offen bzw. ausgewählt ist (Kalendertag, PDF-Seite, Postfach, 3D-Szene, Programm-Dokument).</summary>
    public string? ClaudeFocus(string page)
    {
        static string Cut(string s, int n) => s.Length > n ? s[..n] + "…" : s;
        switch (page)
        {
            case "pdf" when PdfPath is { } pp: return $"PDF „{Path.GetFileName(pp)}“, Seite {PdfPage + 1} von {PdfPages}";
            case "kalender": return $"Kalender, {CalDay.ToString("dddd, dd.MM.yyyy", De)}, Ansicht {CalMode}" + (CalSelected is { } cs ? $", Termin ausgewählt ({cs})" : "");
            case "posteingang" when Inbox.Count > 0:
                return $"Posteingang mit {Inbox.Count} Mails; neueste: " + string.Join(" · ", Inbox.OrderByDescending(m => m.Date).Take(5).Select(m => $"{Cut(m.From, 30)}: „{Cut(m.Subject, 60)}“" + (m.Seen ? "" : " (ungelesen)")));
            case "3d":
                return $"3D-Szene „{_scene.Name}“ mit {_scene.Parts.Count} Teilen" + (_scene.Parts.Count > 0 ? ": " + string.Join(", ", _scene.Parts.Take(12).Select(p => p.Name)) : "") + (Selected is { } sp ? $"; ausgewählt: {sp.Name}" : "");
        }
        if (page == GK && ClaudeGameFocus() is { } gf) return gf; // Level in voller Länge für Claude
        if (page == MS && ClaudeMusicFocus() is { } mf) return mf; // Trendo Musik: Lied samt Produzenten-Prüfung
        if (Programs.TryGetValue(page, out var spec) && ProgramActive(page) is { } d)
        {
            var parts = d.Data.Select(kv => kv.Value is System.Text.Json.Nodes.JsonArray a ? $"{kv.Key}: {a.Count}" : null).OfType<string>().Take(10).ToList();
            return $"{spec.Title}: Dokument „{d.Title}“" + (parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : "") + (d.Dirty ? ", ungespeichert" : "");
        }
        return null;
    }

    static string ItemText(object? o) => o switch
    {
        null => "",
        string s => s,
        ContentControl cc => cc.Content as string ?? (cc.Content as TextBlock)?.Text ?? cc.Content?.ToString() ?? "",
        TextBlock tb => tb.Text ?? "",
        _ => o.ToString() ?? "",
    };

    // ───────────── Claude bedient jedes Programm ─────────────
    sealed record ClaudePlan(string Page, ClaudeActions.Page State, IReadOnlyList<ClaudeActions.Step> Steps, string ChatId);

    /// <summary>Plan-Karte im Chat: was Claude im Programm tun möchte – „Ausführen“ oder „Nein“.</summary>
    Control PlanCard(ClaudeDock dock, ChatUi ui, ClaudePlan plan)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = $"🛠 Claude möchte in „{plan.State.Program}“:", Foreground = CGold, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        var risky = false;
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var s = plan.Steps[i]; var r = ClaudeActions.StepRisky(s, plan.State); risky |= r;
            var pv = ClaudeExtraPreview(plan.Page, s) ?? ClaudeMusicPreview(plan.Page, s);
            sp.Children.Add(new TextBlock { Name = $"claude-plan-step-{i}", Text = $"{i + 1}. {ClaudeActions.Describe(s, plan.State)}" + (r ? "  ⚠ wirkt nach außen" : ""), Foreground = r ? Brush.Parse("#FFF8B26A") : ClaudeFg, FontSize = 13, TextWrapping = TextWrapping.Wrap });
            if (pv is not null) sp.Children.Add(pv);
        }
        if (risky) sp.Children.Add(new TextBlock { Text = "⚠ Darunter ist etwas, das nach außen wirkt oder sich nicht zurücknehmen lässt (z. B. senden, löschen, kaufen). Es passiert nur, wenn du „Ausführen“ klickst.", Foreground = Brush.Parse("#FFF8B26A"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var run = new Button { Name = "claude-plan-run", Content = risky ? "▶ Ausführen – ich bin einverstanden" : "▶ Ausführen", FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 6, 0), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        var no = new Button { Name = "claude-plan-no", Content = "✕ Nein", FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 0, 0), Background = Brushes.Transparent, Foreground = ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        run.Click += async (_, _) => await ClaudeRunPlan(dock);
        no.Click += (_, _) => { ui.Plan = null; CChats.Add(ChatStudio(dock), plan.ChatId, new ClaudeChats.Msg("assistant", "✕ Nicht ausgeführt – im Programm hat sich nichts geändert.", [], DateTimeOffset.Now)); RenderChat(dock); };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { run, no } });
        return Appear(CGlass(sp, name: "claude-plan"));
    }

    public bool ClaudePlanPending() => ActiveChatUi()?.Plan is not null;
    public Task ClaudeRunPlan() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? ClaudeRunPlan(d) : Task.CompletedTask;

    /// <summary>Plan ausführen: Schritt für Schritt auf der Seite (Felder füllen, wählen, klicken). Bei Fachprogrammen danach „Behalten/Verwerfen“ im Programm.</summary>
    async Task ClaudeRunPlan(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.Plan is not { } plan) return;
        ui.Plan = null;
        if (CurrentView != plan.Page) Show(plan.Page);
        var doc = Programs.ContainsKey(plan.Page) ? ProgramActive(plan.Page) : null;
        if (doc is not null) ClaudeChangeBegin(plan.Page, doc);
        var before = doc?.Data.ToJsonString();
        var done = new List<string>(); var failed = new List<string>();
        foreach (var s in plan.Steps)
        {
            var root = ActivePageRoot;
            var target = root is null ? null : PageControls(root).FirstOrDefault(c => c.Name == s.Target && !InsideClaude(c));
            if (target is not null) { ClaudeFlash(target); if (ClaudeStepDelay > 0) await Task.Delay(ClaudeStepDelay); } // Claude zeigt, wo es arbeitet
            var text = ClaudeActions.Describe(s, plan.State);
            switch (s.Kind, target)
            {
                case ("feld", TextBox tb) when tb.IsEffectivelyEnabled: tb.Text = s.Value; done.Add(text); break;
                case ("wahl", ComboBox cb) when cb.IsEffectivelyEnabled && cb.Items.FirstOrDefault(i => ItemText(i).Equals(s.Value, StringComparison.OrdinalIgnoreCase)) is { } item: cb.SelectedItem = item; done.Add(text); break;
                case ("klick", Button b) when b.IsEffectivelyEnabled: b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); done.Add(text); break;
                default: if (ClaudeApplyExtra(plan.Page, s) || ClaudeMusicApply(plan.Page, s)) done.Add(text); else failed.Add(text); break;
            }
            await _ui.InvokeAsync(() => { }, DispatcherPriority.Background); // Seite zeichnet sich nach jedem Schritt neu
        }
        if (doc is not null)
        {
            if (doc.Data.ToJsonString() != before) { ClaudeChangeAfter(plan.Page, doc); _progMsg[plan.Page] = "✳ Claude hat im Programm gearbeitet – oben vergleichen, behalten oder verwerfen."; }
            else _claudeChanges.Remove(plan.Page);
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.bedient", $"{plan.Page} · {done.Count} Schritte" + (plan.Steps.Any(s => ClaudeActions.StepRisky(s, plan.State)) ? " · mit Wirkung nach außen" : ""));
        var msg = "🛠 Im Programm erledigt:\n" + string.Join("\n", done.Select(d => "- " + d)) + (failed.Count > 0 ? "\n\n⚠ Nicht möglich (Element nicht mehr da oder gesperrt):\n" + string.Join("\n", failed.Select(f => "- " + f)) : "");
        CChats.Add(ChatStudio(dock), plan.ChatId, new ClaudeChats.Msg("assistant", msg, [], DateTimeOffset.Now));
        if (CurrentView == plan.Page) Show(plan.Page);
        RenderChat(dock);
    }

    // ───────────── Rechtsklick „✳ Claude fragen“ ─────────────
    /// <summary>Rechtsklick irgendwo im Programm: „✳ Claude fragen“ – in vorhandene Menüs eingefügt, sonst als eigenes kleines Menü. Eingabefelder behalten ihr Menü (dort: Strg+Umschalt+A).</summary>
    void OnClaudeContext(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Source is not Control src || InsideClaude(src) || src.FindLogicalAncestorOfType<TextBox>(true) is not null) return;
        var studio = ClaudeBelongs(CurrentView) ? ClaudeStudioOf(CurrentView) : null;
        var what = ClaudeTargetText(src);
        if (what is null) return;
        MenuItem Item() { var mi = new MenuItem { Name = "claude-context-ask", Header = "✳ Claude fragen", Tag = what }; mi.Click += (s, _) => ClaudeContextAsk((s as MenuItem)?.Tag as string ?? what, studio); return mi; }
        // vorhandenes Menü (eigenes oder eines Elternteils) um den Eintrag ergänzen
        foreach (var c in src.GetSelfAndLogicalAncestors().OfType<Control>())
        {
            if (c.ContextMenu is { } cm) { if (!cm.Items.OfType<MenuItem>().Any(m => m.Name == "claude-context-ask")) cm.Items.Insert(0, Item()); else ((MenuItem)cm.Items.OfType<MenuItem>().First(m => m.Name == "claude-context-ask")).Tag = what; return; }
            if (c.ContextFlyout is MenuFlyout mf) { foreach (var old in mf.Items.OfType<MenuItem>().Where(m => m.Name == "claude-context-ask").ToList()) mf.Items.Remove(old); mf.Items.Insert(0, Item()); return; }
            if (c == ActivePageRoot) break;
        }
        var menu = new ContextMenu { Items = { Item() } };
        menu.Open(src);
        e.Handled = true;
    }

    /// <summary>Was mit Rechtsklick gemeint ist: Text, Listeneintrag, Zelle, Knopf, Bild.</summary>
    static string? ClaudeTargetText(Control src)
    {
        foreach (var c in src.GetSelfAndLogicalAncestors().OfType<Control>().Take(6))
        {
            switch (c)
            {
                case TextBlock t when t.Text is { Length: > 0 } s: return Cut(s);
                case Image img: return "ein Bild" + (ToolTip.GetTip(img) is string tip ? $" ({tip})" : "");
                case ListBoxItem or TreeViewItem or Button or Border:
                    var texts = c.GetLogicalDescendants().OfType<TextBlock>().Select(x => x.Text).Where(x => x is { Length: > 0 }).Take(6).ToList();
                    if (texts.Count > 0) return Cut(string.Join(" · ", texts!));
                    break;
            }
        }
        return null;
        static string Cut(string s) => s.Length > 800 ? s[..800] + " …" : s;
    }

    /// <summary>„✳ Claude fragen“ zu einem Element (öffentlich für Tests): Claude öffnet sich für das Studio der Seite – oder allgemein – mit dem Element im Eingabefeld.</summary>
    public bool ClaudeContextAsk(string what, string? studio = null)
    {
        studio ??= ClaudeBelongs(CurrentView) ? ClaudeStudioOf(CurrentView) : null;
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.rechtsklick", CurrentView);
        var text = $"Zu „{what}“:\n\n";
        if (studio is null)
        {
            if (Windows.Active?.Id is not { } id) return false;
            if (!_claudeDocks.ContainsKey(id)) ClaudeDockToggle(null, text);
            return _claudeDocks.ContainsKey(id);
        }
        return ClaudeOpenFor(studio, text) is not null;
    }
}
