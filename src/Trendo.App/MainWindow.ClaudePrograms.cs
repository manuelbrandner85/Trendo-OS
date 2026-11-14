using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Claude in den Programmen (Wünsche 02.11.2026): Markieren & Claude fragen, Ziehen in Claude, Änderungen im Programm annehmen oder verwerfen mit Vorher/Nachher,
/// Befehlsleiste Strg+K mit Claude, Claude schlägt nächste Schritte vor (abschaltbar), Wiederholungen als Abläufe und Projekt-Fortsetzung überall.</summary>
public partial class MainWindow
{
    // ───────────── Claude-Bereich gezielt öffnen ─────────────
    /// <summary>Claude-Bereich für dieses Studio öffnen (ist er für ein anderes Studio offen, wechselt er). Optional Text ins Eingabefeld und ein bestimmtes Projekt.</summary>
    ClaudeDock? ClaudeOpenFor(string studio, string? text = null, string? chatId = null)
    {
        if (Windows.Active?.Id is not { } id) return null;
        if (_claudeDocks.TryGetValue(id, out var open) && open.Studio != studio) ClaudeDockToggle(null); // anderes Studio: schließen …
        if (!_claudeDocks.ContainsKey(id)) ClaudeDockToggle(studio); // … und für dieses öffnen
        if (!_claudeDocks.TryGetValue(id, out var dock) || !_chatUis.TryGetValue(dock, out var ui)) return null;
        if (chatId is not null && !ui.Running && CChats.Get(studio, chatId) is { } cc) { ui.ChatId = chatId; if (cc.Doc is not null) ClaudeEnsureProgram(dock, cc); RenderChatList(dock); RenderChat(dock); }
        if (text is { Length: > 0 }) { ui.Input.Text = ((ui.Input.Text ?? "").TrimEnd() + (string.IsNullOrWhiteSpace(ui.Input.Text) ? "" : "\n\n") + text).TrimStart(); ui.Input.CaretIndex = ui.Input.Text.Length; }
        _ui.Post(() => ui.Input.Focus());
        return dock;
    }

    // ───────────── Markieren & Claude fragen ─────────────
    /// <summary>Was im Programm gerade markiert ist (öffentlich für Tests): Text-Auswahl, Zellbereich, aktuelle Folie. null = nichts markiert.</summary>
    public string? ClaudeSelection(string page)
    {
        switch (page)
        {
            case "text":
            {
                var blocks = TextDocument.Blocks; if (blocks.Count == 0) return null;
                var para = blocks[Math.Clamp(TextFocusBlock, 0, blocks.Count - 1)] as Para;
                return para is not null && _txSelLen > 0 && _txSel >= 0 && _txSel + _txSelLen <= para.Text.Length ? para.Text.Substring(_txSel, _txSelLen) : null;
            }
            case "tabelle":
            {
                var (r, c) = _shActive; var (ar, ac) = _shAnchor;
                var (r0, r1, c0, c1) = (Math.Min(r, ar), Math.Max(r, ar), Math.Min(c, ac), Math.Max(c, ac));
                if ((r1 - r0 + 1) * (c1 - c0 + 1) > 400) r1 = r0 + 400 / (c1 - c0 + 1) - 1; // große Bereiche gekürzt
                var rows = new List<string>();
                for (var y = r0; y <= r1; y++) rows.Add(string.Join("\t", Enumerable.Range(c0, c1 - c0 + 1).Select(x => ActiveSheet.Get(y, x)?.Raw ?? "")));
                var text = string.Join("\n", rows);
                return text.Trim().Length == 0 ? null : $"{Addr.Name(r0, c0)}{(r1 > r0 || c1 > c0 ? ":" + Addr.Name(r1, c1) : "")}\n{text}";
            }
            case "praesentation" when Deck.Slides.Count > 0:
                return Deck.Slides[Math.Clamp(DeckIndex, 0, Deck.Slides.Count - 1)].Title is { Length: > 0 } t ? $"Folie {DeckIndex + 1}: {t}" : null;
        }
        return null;
    }

    /// <summary>Markierung mit Claude besprechen: Claude öffnet sich für das Studio, die Markierung steht im Eingabefeld – du schreibst nur noch, was damit passieren soll.</summary>
    public bool ClaudeAskSelection(string page)
    {
        if (ClaudeStudioOf(page) is not { } studio) return false;
        var sel = ClaudeSelection(page) ?? (_lastTextSel is { } ls && ls.Page == page ? ls.Text : null); // auch Markierung in einem beliebigen Eingabefeld
        if (sel is { Length: > 2000 }) sel = sel[..2000] + " …";
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.markierung", page);
        return ClaudeOpenFor(studio, sel is null ? null : "Zu dieser Markierung:\n„" + sel + "“\n\n") is not null;
    }

    // ───────────── Ziehen in Claude ─────────────
    /// <summary>Dateien oder Text in den Claude-Knopf oder in den Chat ziehen: Dateien werden angehängt, Text kommt als Zitat ins Eingabefeld.</summary>
    void ClaudeAcceptDrop(Control target, Func<string?> studio)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Text) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        target.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];
            var text = files.Count == 0 ? e.DataTransfer.TryGetText() : null;
            if (studio() is { } s && ClaudeDropInto(s, files, text)) e.Handled = true;
        });
    }

    /// <summary>Abgelegtes an Claude geben (öffentlich für Tests).</summary>
    public bool ClaudeDropInto(string studio, IReadOnlyList<string> files, string? text)
    {
        if (files.Count == 0 && string.IsNullOrWhiteSpace(text)) return false;
        var dock = ClaudeOpenFor(studio, string.IsNullOrWhiteSpace(text) ? null : "„" + text!.Trim() + "“\n\n");
        if (dock is null) return false;
        if (files.Count > 0) ClaudeChatAttach(dock, files);
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.gezogen", files.Count > 0 ? $"{files.Count} Dateien" : "Text");
        return true;
    }

    // ───────────── Annehmen/Ablehnen und Vorher/Nachher im Programm ─────────────
    sealed class ProgChange { public required string DocId; public required ProgramDocument.Mark Before; public string After = ""; public bool ShowingBefore; }
    readonly Dictionary<string, ProgChange> _claudeChanges = [];

    /// <summary>Vor Claudes erster Änderung an einem Studio-Dokument den Stand merken – danach entscheidest du im Programm: behalten oder verwerfen.</summary>
    void ClaudeChangeBegin(string page, ProgramDocument doc)
    {
        if (_claudeChanges.TryGetValue(page, out var c) && c.DocId == doc.Id) return;
        _claudeChanges[page] = new ProgChange { DocId = doc.Id, Before = doc.Remember() };
    }
    void ClaudeChangeAfter(string page, ProgramDocument doc) { if (_claudeChanges.TryGetValue(page, out var c)) { c.After = doc.Data.ToJsonString(); c.ShowingBefore = false; } }

    public bool ClaudeChangePending(string page) => _claudeChanges.ContainsKey(page);

    /// <summary>Entscheidung im Programm (öffentlich für Tests): „behalten“, „verwerfen“, „vorher“, „nachher“.</summary>
    public void ClaudeChangeDecide(string page, string what)
    {
        if (!_claudeChanges.TryGetValue(page, out var c) || ProgramActive(page) is not { } doc || doc.Id != c.DocId) { _claudeChanges.Remove(page); return; }
        switch (what)
        {
            case "vorher" when !c.ShowingBefore: doc.Rollback(c.Before); c.ShowingBefore = true; _progMsg[page] = "Du siehst den Stand vor Claude – „Nachher“ zeigt Claudes Fassung."; break;
            case "nachher" when c.ShowingBefore: doc.ReplaceData(c.After); c.ShowingBefore = false; _progMsg[page] = "Du siehst Claudes Fassung."; break;
            case "behalten":
                if (c.ShowingBefore) doc.ReplaceData(c.After);
                _claudeChanges.Remove(page); _progMsg[page] = "✓ Claudes Änderungen übernommen – Strg+Z nimmt sie trotzdem noch zurück.";
                _core.Audit.Append(Principal.LocalOwner.Name, "claude.programm.behalten", page); break;
            case "verwerfen":
                if (!c.ShowingBefore) doc.Rollback(c.Before);
                _claudeChanges.Remove(page); _progMsg[page] = "✕ Claudes Änderungen verworfen – das Dokument ist wie vorher.";
                _core.Audit.Append(Principal.LocalOwner.Name, "claude.programm.verworfen", page); break;
        }
        if (CurrentView == page) Show(page);
    }

    /// <summary>Leiste im Programm: Claude hat das Dokument geändert – Vorher/Nachher vergleichen, behalten oder verwerfen.</summary>
    Control? ClaudeChangeBanner(string page, ProgramDocument? doc)
    {
        if (doc is null || !_claudeChanges.TryGetValue(page, out var c)) return null;
        if (c.DocId != doc.Id) return null;
        if (ClaudeNoClicks) // nichts zu bestätigen, nur die Möglichkeit, es zurückzuholen
        {
            var undo = new Button { Name = "claude-change-drop", Content = "Zurücknehmen", FontSize = 12, Padding = new Thickness(10, 3), Background = Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            undo.Click += (_, _) => ClaudeChangeDecide(page, "verwerfen");
            var compare = new Button { Name = c.ShowingBefore ? "claude-change-after" : "claude-change-before", Content = c.ShowingBefore ? "Nachher" : "Vorher", FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 0), Background = Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            compare.Click += (_, _) => ClaudeChangeDecide(page, c.ShowingBefore ? "nachher" : "vorher");
            var line = new DockPanel { Children = { Dk(undo, Avalonia.Controls.Dock.Right), Dk(compare, Avalonia.Controls.Dock.Right), new TextBlock { Text = $"Claude hat „{doc.Title}“ bearbeitet.", Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center } } };
            return Appear(CGlass(line, 12, new Thickness(12, 6), "claude-change"));
        }
        Button B(string name, string label, string what, bool on = false, bool main = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(8),
                Background = main ? ClaudeAccent : on ? Brush.Parse("#33E8C26A") : Brushes.Transparent, Foreground = main ? Brushes.White : Text, BorderBrush = on ? CGold : Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => ClaudeChangeDecide(page, what); return b;
        }
        var row = new WrapPanel { Children = {
            B("claude-change-before", "Vorher", "vorher", c.ShowingBefore), B("claude-change-after", "Nachher", "nachher", !c.ShowingBefore),
            new Border { Width = 10 }, B("claude-change-keep", "✓ Behalten", "behalten", main: true), B("claude-change-drop", "✕ Verwerfen", "verwerfen") } };
        var sp = new StackPanel { Spacing = 6, Children = {
            new TextBlock { Text = $"Claude hat „{doc.Title}“ geändert" + (c.ShowingBefore ? " – du siehst gerade den Stand vorher." : " – du siehst Claudes Fassung."), Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            row } };
        return Appear(CGlass(sp, 12, new Thickness(14, 8), "claude-change"));
    }

    // ───────────── Claude schlägt vor ─────────────
    bool ClaudeSuggestsOn => KiSourceSettings.Load(_core.DataRoot).ClaudeSuggests;

    /// <summary>Vorschläge als Knöpfe „Als Nächstes“ – ein Klick schickt sie an Claude.</summary>
    Control SuggestChips(ClaudeDock dock, IReadOnlyList<string> list)
    {
        var row = new WrapPanel { Name = "claude-chat-suggest", Margin = new Thickness(0, 2, 0, 0) };
        row.Children.Add(new TextBlock { Text = "Als Nächstes:", Foreground = CGold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) });
        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            var b = new Button { Name = $"claude-chat-suggest-{i}", Content = s, FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(14), Background = CGlassChip, Foreground = ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(b, "Claude macht das als Nächstes");
            b.Click += (_, _) => _ = ClaudeChatSend(dock, s);
            row.Children.Add(b);
        }
        return row;
    }
    static IBrush CGlassChip => Brush.Parse(ClaudeLight ? "#14000000" : "#1FFFFFFF");

    // ───────────── Wiederholungen automatisieren ─────────────
    string StudioName(ClaudeDock dock) => StudioCatalog.NameOf(dock.Studio ?? "", "Trendo");

    /// <summary>Auftrag als Ablauf merken (öffentlich für Tests über das aktive Fenster).</summary>
    public void ClaudeFlowSave(string text) { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) ClaudeFlowSave(d, text); }
    void ClaudeFlowSave(ClaudeDock dock, string text)
    {
        var t = ClaudeFlows.Save(Prompts, StudioName(dock), text);
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.ablauf.neu", StudioName(dock));
        Toast($"Als Ablauf gemerkt: „{t.Name}“ – ein Klick wiederholt ihn in jedem Projekt dieses Studios.");
        RenderChat(dock);
    }
    public IReadOnlyList<string> ClaudeFlowNames() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? ClaudeFlows.For(Prompts, StudioName(d)).Select(p => p.Name).ToList() : [];

    /// <summary>Gemerkte Abläufe über dem Eingabefeld – ein Klick schickt den Auftrag im offenen Projekt ab.</summary>
    void RenderFlows(ClaudeDock dock, ChatUi ui)
    {
        ui.Flows.Children.Clear();
        var flows = ClaudeFlows.For(Prompts, StudioName(dock));
        ui.Flows.IsVisible = flows.Count > 0;
        if (flows.Count == 0) return;
        ui.Flows.Children.Add(new TextBlock { Text = "⟳ Abläufe:", Foreground = CGold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4) });
        for (var i = 0; i < flows.Count; i++)
        {
            var f = flows[i];
            var b = new Button { Name = $"claude-flow-{i}", Content = f.Name, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 4), CornerRadius = new CornerRadius(12), Background = CGlassChip, Foreground = ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(b, "Wiederholen: " + (f.Text.Length > 200 ? f.Text[..200] + " …" : f.Text) + "\n(Bearbeiten und löschen im Prompt-Studio)");
            b.Click += (_, _) => { _core.Audit.Append(Principal.LocalOwner.Name, "claude.ablauf.start", f.Name); ui.Input.Text = f.Text; ClaudeChatSubmit(dock); };
            ui.Flows.Children.Add(b);
        }
    }

    /// <summary>Unter deiner letzten Nachricht: „Als Ablauf merken“ – kam der Auftrag schon öfter, sagt Trendo das dazu.</summary>
    Control? FlowSaveButton(ClaudeDock dock, string text)
    {
        if (text.Trim().Length < 4) return null;
        if (ClaudeFlows.For(Prompts, StudioName(dock)).Any(p => p.Text.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase))) return new TextBlock { Name = "claude-chat-flow-saved", Text = "⟳ ist ein Ablauf", Foreground = ClaudeMuted, FontSize = 11 };
        var repeats = ClaudeFlows.Repeats(CChats.All(ChatStudio(dock)), text);
        var b = new Button { Name = "claude-chat-flow-save", Content = repeats >= 2 ? $"⟳ Kam schon {repeats}× – als Ablauf merken" : "⟳ Als Ablauf merken", FontSize = 11, Padding = new Thickness(8, 2),
            Background = Brushes.Transparent, Foreground = repeats >= 2 ? CGold : ClaudeMuted, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
        ToolTip.SetTip(b, "Diesen Auftrag merken und später mit einem Klick wiederholen – in jedem Projekt dieses Studios");
        b.Click += (_, _) => ClaudeFlowSave(dock, text);
        return b;
    }

    // ───────────── Projekt-Fortsetzung überall ─────────────
    /// <summary>Claude-Projekt fortsetzen – auch aus einem anderen Studio: Studio öffnen, Claude öffnen, Projekt wählen (öffentlich für Tests).</summary>
    public void ClaudeContinue(string studio, string chatId)
    {
        if (PageForStudio(studio) is { } page && !string.Equals(ClaudeStudioOf(CurrentView), studio, StringComparison.OrdinalIgnoreCase)) Show(page);
        ClaudeOpenFor(studio, null, chatId);
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.weiter", studio);
    }

    /// <summary>Zusätzliche Knöpfe neben „✳ Claude“ auf jeder Studio-Seite: Markierung fragen, Weiter mit dem letzten Projekt.</summary>
    IEnumerable<Control> ClaudeBarExtras(string page, string studio)
    {
        yield return KieBarButton(studio); // kie.ai in jedem Studio (Freigabe 13.11.2026), auch bei offenem Claude-Bereich
        if (Windows.Active?.Id is { } aw && ClaudeDockOpen(aw)) yield break; // offen: alles steht schon im Claude-Bereich
        if (page is "text" or "tabelle" or "praesentation")
        {
            var ask = new Button { Name = "auftrag-claude-markierung", Content = "Markierung fragen", Height = 32 };
            ToolTip.SetTip(ask, "Was du markiert hast, mit Claude besprechen (Strg+Umschalt+A) – auch ziehen geht: Text oder Dateien einfach auf „Claude“ ziehen");
            ask.Click += (_, _) => ClaudeAskSelection(page);
            yield return ask;
        }
        if (CChats.All(studio).FirstOrDefault(c => c.Messages.Count > 0) is { } last)
        {
            var title = last.Title.Length > 22 ? last.Title[..21] + "…" : last.Title;
            var go = new Button { Name = "auftrag-claude-weiter", Content = "Weiter: " + title, Height = 32 };
            ToolTip.SetTip(go, $"Mit Claude weitermachen, wo du warst: „{last.Title}“ (zuletzt {last.Last.LocalDateTime:dd.MM. HH:mm})" + (last.Messages.LastOrDefault(m => m.Role == "user")?.Text is { } lt ? $"\nZuletzt: {(lt.Length > 120 ? lt[..120] + " …" : lt)}" : ""));
            go.Click += (_, _) => ClaudeContinue(studio, last.Id);
            yield return go;
        }
    }

    // ───────────── Befehlsleiste Strg+K ─────────────
    /// <summary>Treffer für die Befehlsleiste: Claude fragen, Markierung, Vorschläge, Abläufe, Projekte zum Weitermachen (auch aus anderen Studios).</summary>
    IEnumerable<(string Label, string Kind, Action Run, int Score)> ClaudePaletteHits(string q, Func<string, string, int> score)
    {
        var page = CurrentView;
        var studio = ClaudeBelongs(page) ? ClaudeStudioOf(page) : null;
        if (studio is not null && q.Length > 0)
        {
            var ask = q;
            yield return ($"Claude: „{q}“", "Claude in diesem Studio", () => { if (ClaudeOpenFor(studio) is { } d) _ = ClaudeChatSend(d, ask); }, 3);
        }
        if (studio is not null && ClaudeSelection(page) is not null && score("markierung an claude fragen", q) is > 0 and var s0)
            yield return ("Markierung an Claude", "Claude", () => ClaudeAskSelection(page), s0 + 1);
        if (studio is not null)
        {
            var lastChat = CChats.All(studio).FirstOrDefault(c => c.Messages.Count > 0);
            if (lastChat?.Messages.LastOrDefault(m => m.Role == "assistant") is { } am && ClaudeSuggestsOn)
                foreach (var sug in ClaudeChats.SplitSuggestions(am.Text).Suggestions)
                    if (score(sug, q) is > 0 and var s1) { var sx = sug; var cid = lastChat.Id; yield return ("" + sug, "Claude schlägt vor", () => { if (ClaudeOpenFor(studio, null, cid) is { } d) _ = ClaudeChatSend(d, sx); }, s1 + 1); }
            foreach (var f in ClaudeFlows.For(Prompts, StudioCatalog.NameOf(studio, "Trendo")))
                if (score("ablauf " + f.Name, q) is > 0 and var s2) { var ft = f.Text; yield return ("⟳ " + f.Name, "Claude-Ablauf", () => { if (ClaudeOpenFor(studio) is { } d) _ = ClaudeChatSend(d, ft); }, s2); }
        }
        foreach (var c in CChats.Recent(5))
            if (score("weiter " + c.Title, q) is > 0 and var s3) { var cs = c.Studio; var cid = c.Id; yield return ("Weiter: " + c.Title, "Claude-Projekt · " + StudioCatalog.NameOf(cs, cs), () => ClaudeContinue(cs, cid), s3); }
    }
}
