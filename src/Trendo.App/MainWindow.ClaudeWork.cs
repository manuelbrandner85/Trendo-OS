using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Arbeiten mit Claude (Wünsche 02.11.2026): deine Unterlagen aus „Wissen“ (nur mit Freigabe je Projekt), große Aufträge in Etappen (nach der Abo-Pause
/// macht Claude weiter) und Veröffentlichen mit einem Klick (fertig verpacken, nichts wird hochgeladen).</summary>
public partial class MainWindow
{
    ClaudeDocs CDocs => new(_core.DataRoot);
    ClaudeStages CStages => new(_core.DataRoot);

    // ───────────── Deine Unterlagen ─────────────
    /// <summary>Freigegebene Unterlagen des Projekts in den Projektordner legen und den Hinweis für Claude liefern.</summary>
    string ClaudeDocsFor(string chatId, string folder)
    {
        var ids = CDocs.Shared(chatId);
        var items = Wissen.All().Where(i => ids.Contains(i.Id)).ToList();
        try { return ClaudeDocs.Write(folder, items); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <summary>Passende Einträge aus „Wissen“ zu einer Nachricht, die weder freigegeben noch abgelehnt sind – Trendo fragt dann, ob sie mitdürfen.</summary>
    IReadOnlyList<KnowledgeItem> ClaudeDocSuggestions(string chatId, string text)
    {
        var shared = CDocs.Shared(chatId);
        return Wissen.Search(text, 4).Where(h => h.Score > 0.5 && !shared.Contains(h.Item.Id) && !CDocs.Declined(chatId, h.Item.Id)).Select(h => h.Item).Take(3).ToList();
    }

    /// <summary>Feld „📎 Unterlagen“: alle Einträge aus „Wissen“, je Eintrag mitgeben oder nicht.</summary>
    Control DocsPanel(ClaudeDock dock, ClaudeChats.Chat? chat)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = "Deine Unterlagen", Foreground = CGold, FontSize = 15, FontWeight = FontWeight.SemiBold });
        sp.Children.Add(new TextBlock { Text = "Aus „Wissen“ geht nur an Claude, was du hier für dieses Projekt freigibst. Claude liest es im Projektordner und gibt nichts davon nach außen weiter.", Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (chat is null) { sp.Children.Add(new TextBlock { Text = "Schreib die erste Nachricht – dann kannst du Unterlagen freigeben.", Foreground = ClaudeFg, FontSize = 13 }); return Appear(CGlass(sp, name: "claude-docs")); }
        var all = Wissen.All().Take(40).ToList();
        if (all.Count == 0) sp.Children.Add(new TextBlock { Name = "claude-docs-empty", Text = "In „Wissen“ ist noch nichts – dort kannst du Notizen und Dokumente ablegen.", Foreground = ClaudeFg, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var shared = CDocs.Shared(chat.Id);
        for (var i = 0; i < all.Count; i++)
        {
            var it = all[i]; var on = shared.Contains(it.Id);
            var b = new Button { Name = $"claude-docs-toggle-{i}", Content = on ? "Freigegeben" : "Freigeben", FontSize = 11.5, Padding = new Thickness(10, 2), CornerRadius = new CornerRadius(8),
                Background = on ? ClaudeAccent : Brushes.Transparent, Foreground = on ? Brushes.White : ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(on ? 0 : 1), VerticalAlignment = VerticalAlignment.Top };
            b.Click += (_, _) => { if (on) CDocs.Unshare(chat.Id, it.Id); else CDocs.Share(chat.Id, it.Id); _core.Audit.Append(Principal.LocalOwner.Name, on ? "claude.unterlage.weg" : "claude.unterlage.frei", ChatStudio(dock)); RenderChat(dock); };
            sp.Children.Add(new DockPanel { Children = { Dk(b, Avalonia.Controls.Dock.Right), new StackPanel { Children = {
                new TextBlock { Text = it.Title, Foreground = ClaudeFg, FontSize = 13, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = it.Source + " · " + (it.Text.Length > 90 ? it.Text[..90].Replace('\n', ' ') + " …" : it.Text.Replace('\n', ' ')), Foreground = ClaudeMuted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } } } });
        }
        return Appear(CGlass(sp, name: "claude-docs"));
    }

    /// <summary>Nach einer Antwort: passende Unterlagen gefunden – „Für dieses Projekt freigeben?“ (Ja / Nein).</summary>
    void AddDocSuggestions(ClaudeDock dock, ChatUi ui, ClaudeChats.Chat chat)
    {
        if (chat.Messages.LastOrDefault(m => m.Role == "user") is not { } last) return;
        var hits = ClaudeDocSuggestions(chat.Id, last.Text);
        if (hits.Count == 0) return;
        var yes = new Button { Name = "claude-docs-suggest-yes", Content = "Ja, mitgeben", FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 6, 0), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        var no = new Button { Name = "claude-docs-suggest-no", Content = "Nein", FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 0, 0), Background = Brushes.Transparent, Foreground = ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        yes.Click += (_, _) => { foreach (var h in hits) CDocs.Share(chat.Id, h.Id); _core.Audit.Append(Principal.LocalOwner.Name, "claude.unterlage.frei", ChatStudio(dock)); Toast("Freigegeben – Claude nutzt die Unterlagen ab der nächsten Nachricht."); RenderChat(dock); };
        no.Click += (_, _) => { foreach (var h in hits) CDocs.Decline(chat.Id, h.Id); RenderChat(dock); };
        ui.Log.Children.Add(Appear(CGlass(new StackPanel { Spacing = 4, Children = {
            new TextBlock { Text = "Passende Unterlagen in „Wissen“ – für dieses Projekt an Claude mitgeben?", Foreground = CGold, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Name = "claude-docs-suggest", Text = string.Join(" · ", hits.Select(h => h.Title)), Foreground = ClaudeFg, FontSize = 13, TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, Children = { yes, no } } } }, name: "claude-docs-ask")));
    }

    public IReadOnlyList<string> ClaudeSharedDocs() => ClaudeChatCurrent() is { } c ? CDocs.Shared(c.Id) : [];

    // ───────────── Große Aufträge in Etappen ─────────────
    void InitClaudeStages()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        t.Tick += (_, _) => _ = ClaudeStagesTick();
        t.Start();
    }

    /// <summary>Abo wieder frei? Dann macht Claude in den unterbrochenen Projekten weiter – direkt, wenn der Claude-Bereich des Studios offen ist, sonst mit Hinweis und „Weitermachen“.</summary>
    public async Task ClaudeStagesTick()
    {
        if (CLimit.Active(DateTimeOffset.Now)) return;
        foreach (var s in CStages.Pending())
        {
            if (CChats.Get(s.Studio, s.ChatId) is not { } chat) { CStages.Done(s.ChatId); continue; }
            var dock = _claudeDocks.Values.FirstOrDefault(d => ChatStudio(d) == s.Studio && _chatUis.TryGetValue(d, out var u) && !u.Running);
            if (dock is not null && _chatUis.TryGetValue(dock, out var ui))
            {
                CStages.Done(s.ChatId);
                ui.ChatId = s.ChatId; RenderChatList(dock); RenderChat(dock);
                _core.Audit.Append(Principal.LocalOwner.Name, "claude.etappe.weiter", s.Studio);
                await ClaudeChatSend(dock, ClaudeStages.Resume + s.Request);
            }
            else if (ClaudeNoClicks) ClaudeContinue(s.Studio, s.ChatId); // Fenster für das Projekt öffnen, beim nächsten Takt macht Claude weiter
            else if (!_stageToasted.Contains(s.ChatId))
            {
                _stageToasted.Add(s.ChatId);
                Toast($"Claude ist wieder frei – im Projekt „{chat.Title}“ weitermachen?", ("Weitermachen", () => { ClaudeContinue(s.Studio, s.ChatId); _ = ClaudeStagesTick(); }));
            }
        }
    }
    readonly HashSet<string> _stageToasted = [];
    public IReadOnlyList<ClaudeStages.Stage> ClaudePendingStages() => CStages.Pending();

    // ───────────── Veröffentlichen ─────────────
    /// <summary>Feld „🚀 Veröffentlichen“: was passiert (verpacken, nichts hochladen) – erst nach „Ja“.</summary>
    Control PublishPanel(ClaudeDock dock, ClaudeChats.Chat? chat)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = "Veröffentlichen", Foreground = CGold, FontSize = 15, FontWeight = FontWeight.SemiBold });
        if (chat is null) { sp.Children.Add(new TextBlock { Text = "Noch kein Projekt.", Foreground = ClaudeFg, FontSize = 13 }); return Appear(CGlass(sp, name: "claude-publish")); }
        sp.Children.Add(new TextBlock { Text = $"Trendo verpackt „{chat.Title}“ fertig zum Weitergeben – als ZIP im Ordner „Veröffentlicht“ (ohne Hilfsdateien von Trendo und Claude). Hochgeladen oder verschickt wird dabei nichts; das Teilen machst du selbst.", Foreground = ClaudeFg, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        var go = new Button { Name = "claude-publish-yes", Content = "Ja, verpacken", FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 6, 0), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        var no = new Button { Name = "claude-publish-no", Content = "Abbrechen", FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 0, 0), Background = Brushes.Transparent, Foreground = ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        go.Click += (_, _) => ClaudePublishNow(dock, chat);
        no.Click += (_, _) => { if (_chatUis.TryGetValue(dock, out var ui)) { ui.Panel = null; RenderChat(dock); } };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { go, no } });
        return Appear(CGlass(sp, name: "claude-publish"));
    }

    public string? LastPublished { get; private set; }

    void ClaudePublishNow(ClaudeDock dock, ClaudeChats.Chat chat)
    {
        if (Programs.TryGetValue(dock.Page, out _) && ProgramActive(dock.Page) is { Path: not null, Dirty: true }) try { ProgramSave(dock.Page); } catch (Exception e) when (e is IOException or InvalidOperationException) { } // Stand im Programm zuerst sichern
        try
        {
            var zip = ClaudePublish.Pack(chat.Folder, WorkFolder("Veröffentlicht"), chat.Title);
            LastPublished = zip;
            _core.Audit.Append(Principal.LocalOwner.Name, "claude.veroeffentlicht", Path.GetFileName(zip));
            CChats.Add(ChatStudio(dock), chat.Id, new ClaudeChats.Msg("assistant", $"Verpackt: „{Path.GetFileName(zip)}“ im Ordner „Veröffentlicht“ – bereit zum Weitergeben. Hochgeladen wurde nichts.", [], DateTimeOffset.Now));
            var dir = Path.GetDirectoryName(zip)!;
            Toast("Fertig verpackt: " + Path.GetFileName(zip), ("Ordner öffnen", () => Launch(dir)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _claudeMsg = "Verpacken ging nicht: " + e.Message; }
        if (_chatUis.TryGetValue(dock, out var ui)) ui.Panel = null;
        RenderChat(dock);
    }
}
