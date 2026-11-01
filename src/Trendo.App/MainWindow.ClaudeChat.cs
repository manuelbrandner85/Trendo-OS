using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude-Chat im Studio (Wunsch 01.11.2026): sieht aus und verhält sich wie Claude – links die Projekte des Studios, rechts das Gespräch, unten das
/// Eingabefeld. Claude arbeitet über das installierte Claude Code des Nutzers (eigene Anmeldung je Person, Abo) direkt im Ordner des Projekts – gesichert:
/// nur im Projektordner, keine Befehle, kein Web. Die erste Nachricht legt das Projekt automatisch an; fortgesetzt wird über die Sitzung von Claude Code;
/// löschen entfernt Verlauf und Ordner (Papierkorb). Jede Antwort mit Änderungen lässt sich zurücknehmen.</summary>
public partial class MainWindow
{
    sealed class ChatUi
    {
        public string? ChatId; public bool Running; public CancellationTokenSource? Cts;
        public StackPanel Log = new() { Name = "claude-chat-log", Spacing = 14, Margin = new Thickness(24, 18, 24, 18) };
        public ScrollViewer Scroll = new(); public StackPanel Side = new() { Name = "claude-chat-list", Spacing = 4 };
        public TextBox Input = new(); public Button Send = new(); public TextBlock Title = new(); public Border Body = new();
        public string? ConfirmDelete; public FolderSnapshot? LastSnapshot; public string? LastSnapshotChat;
    }
    readonly Dictionary<ClaudeDock, ChatUi> _chatUis = [];

    ClaudeChats CChats => new(_core.DataRoot);
    /// <summary>Für Tests: statt in den Papierkorb (Ordner eines gelöschten Projekts).</summary>
    public static Action<string>? ChatTrashOverride { get; set; }

    /// <summary>Ordner, in dem die Projekte eines Studios liegen.</summary>
    public string ClaudeChatRoot(string? studio) =>
        Path.Combine(WorkFolder("Claude"), Regex.Replace(StudioCatalog.NameOf(studio ?? "", "Allgemein"), @"[\\/:*?""<>|]", "-"));

    void BuildChatPane(ClaudeDock dock)
    {
        var ui = _chatUis[dock] = new ChatUi();
        ui.Scroll = new ScrollViewer { Name = "claude-chat-scroll", Content = ui.Log, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        ui.Title = new TextBlock { Name = "claude-chat-title", Foreground = ClaudeFg, FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ui.Input = new TextBox { Name = "claude-chat-input", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, MaxHeight = 200, FontSize = 14,
            PlaceholderText = "Schreib Claude, was du bauen möchtest …", Background = Brush.Parse("#FF30302E"), Foreground = ClaudeFg, BorderBrush = ClaudeLine, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 52, 10) };
        ui.Input.KeyDown += (_, e) => { if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { e.Handled = true; _ = ClaudeChatSend(dock, ui.Input.Text); } };
        ui.Send = new Button { Name = "claude-chat-send", Content = "↑", Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = ClaudeAccent, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 10, 10), HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(0) };
        ToolTip.SetTip(ui.Send, "Senden (Enter) · neue Zeile mit Umschalt+Enter");
        ui.Send.Click += (_, _) => { if (ui.Running) ui.Cts?.Cancel(); else _ = ClaudeChatSend(dock, ui.Input.Text); };
        var inputBox = new Grid { Margin = new Thickness(24, 0, 24, 6), Children = { ui.Input, ui.Send } };
        var hint = new TextBlock { Text = "Claude arbeitet mit deinem Abo im Ordner dieses Projekts – keine Befehle, kein Internet. Änderungen kannst du nach jeder Antwort zurücknehmen.", Foreground = ClaudeMuted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(26, 0, 26, 12), HorizontalAlignment = HorizontalAlignment.Center };
        var folder = new Button { Name = "claude-chat-folder", Content = "📁 Ordner", FontSize = 12, Padding = new Thickness(10, 3), Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        ToolTip.SetTip(folder, "Ordner dieses Projekts öffnen");
        folder.Click += (_, _) => { if (ui.ChatId is { } id && CChats.Get(ChatStudio(dock), id) is { } c) Launch(c.Folder); };
        var head = new DockPanel { Margin = new Thickness(24, 12, 24, 4), Children = { Dk(folder, Avalonia.Controls.Dock.Right), ui.Title } };
        var bottom = new StackPanel { Children = { inputBox, hint } };
        ui.Body = new Border { Child = ui.Scroll };
        var main = new DockPanel { Background = ClaudeBg, Children = { Dk(head, Avalonia.Controls.Dock.Top), Dk(bottom, Avalonia.Controls.Dock.Bottom), ui.Body } };
        Grid.SetColumn(main, 1);
        var side = new Border { Name = "claude-chat-side", Background = ClaudeBar, BorderBrush = ClaudeLine, BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(10), Child = new ScrollViewer { Content = ui.Side } };
        dock.ChatPane!.Child = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*"), Children = { side, main } };
        ui.ChatId = CChats.All(ChatStudio(dock)).FirstOrDefault()?.Id; // zuletzt benutztes Projekt fortsetzen
        RenderChatList(dock); RenderChat(dock);
        _ = ClaudeChatReady(dock);
    }

    static string ChatStudio(ClaudeDock d) => d.Studio ?? "allgemein";

    static Control Dk(Control c, Avalonia.Controls.Dock d) { DockPanel.SetDock(c, d); return c; }

    /// <summary>Anmeldestand prüfen; ohne Claude Code oder ohne Anmeldung zeigt der Chat, was zu tun ist.</summary>
    async Task ClaudeChatReady(ClaudeDock dock)
    {
        await ClaudeCheck();
        if (_chatUis.TryGetValue(dock, out _)) RenderChat(dock);
    }

    void RenderChatList(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui)) return;
        ui.Side.Children.Clear();
        var add = new Button { Name = "claude-chat-new", Content = "＋ Neues Projekt", HorizontalAlignment = HorizontalAlignment.Stretch, Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), BorderThickness = new Thickness(0) };
        ToolTip.SetTip(add, "Neues Projekt – es entsteht mit deiner ersten Nachricht");
        add.Click += (_, _) => { ui.ChatId = null; ui.ConfirmDelete = null; RenderChatList(dock); RenderChat(dock); ui.Input.Focus(); };
        ui.Side.Children.Add(add);
        ui.Side.Children.Add(new TextBlock { Text = "Projekte in diesem Studio", Foreground = ClaudeMuted, FontSize = 11.5, Margin = new Thickness(4, 10, 0, 2) });
        var all = CChats.All(ChatStudio(dock));
        if (all.Count == 0) ui.Side.Children.Add(new TextBlock { Text = "Noch keine. Schreib Claude einfach – das Projekt entsteht automatisch.", Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 2) });
        for (var i = 0; i < all.Count; i++)
        {
            var c = all[i];
            var open = new Button { Name = $"claude-chat-item-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), BorderThickness = new Thickness(0),
                Background = c.Id == ui.ChatId ? ClaudeItem : Brushes.Transparent, Foreground = ClaudeFg,
                Content = new StackPanel { Children = { new TextBlock { Text = c.Title, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = c.Last.LocalDateTime.ToString("dd.MM. HH:mm", De), FontSize = 10.5, Foreground = ClaudeMuted } } } };
            ToolTip.SetTip(open, "Fortsetzen");
            open.Click += (_, _) => { if (ui.Running) return; ui.ChatId = c.Id; ui.ConfirmDelete = null; RenderChatList(dock); RenderChat(dock); };
            var del = new Button { Name = $"claude-chat-del-{i}", Content = "✕", Padding = new Thickness(6, 2), Background = Brushes.Transparent, Foreground = ClaudeMuted, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(del, "Projekt löschen (Verlauf und Ordner – der Ordner kommt in den Papierkorb)");
            del.Click += (_, _) => { ui.ConfirmDelete = c.Id; RenderChatList(dock); };
            ui.Side.Children.Add(new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), open } });
            if (ui.ConfirmDelete == c.Id)
            {
                var yes = new Button { Name = "claude-chat-del-yes", Content = "Löschen", Background = Brush.Parse("#FFB3261E"), Foreground = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 3), BorderThickness = new Thickness(0) };
                yes.Click += (_, _) => ClaudeChatDelete(dock, c.Id);
                var no = new Button { Name = "claude-chat-del-no", Content = "Behalten", Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 3), BorderThickness = new Thickness(0) };
                no.Click += (_, _) => { ui.ConfirmDelete = null; RenderChatList(dock); };
                ui.Side.Children.Add(new StackPanel { Spacing = 4, Margin = new Thickness(8, 0, 0, 6), Children = {
                    new TextBlock { Text = $"„{c.Title}“ mit Verlauf und Ordner löschen?", Foreground = ClaudeFg, FontSize = 11.5, TextWrapping = TextWrapping.Wrap },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { yes, no } } } });
            }
        }
    }

    void RenderChat(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui)) return;
        var chat = ui.ChatId is { } id ? CChats.Get(ChatStudio(dock), id) : null;
        ui.Title.Text = chat?.Title ?? $"Neues Projekt in „{StudioCatalog.NameOf(dock.Studio ?? "", "Trendo")}“";
        ui.Log.Children.Clear();
        var s = _claude;
        if (!s.Ready && ClaudeRunOverride is null)
        {
            ui.Log.Children.Add(ChatNotice(dock, s));
            ui.Input.IsEnabled = false; ui.Send.IsEnabled = false;
            return;
        }
        ui.Input.IsEnabled = !ui.Running; ui.Send.IsEnabled = true;
        if (chat is null || chat.Messages.Count == 0)
            ui.Log.Children.Add(new StackPanel { Spacing = 8, Margin = new Thickness(0, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "✳", FontSize = 34, Foreground = ClaudeAccent, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Name = "claude-chat-welcome", Text = $"Was baust du heute in „{StudioCatalog.NameOf(dock.Studio ?? "", "Trendo")}“?", FontSize = 22, Foreground = ClaudeFg, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                new TextBlock { Text = "Claude bleibt hier beim Fachgebiet dieses Studios.", FontSize = 12.5, Foreground = ClaudeMuted, HorizontalAlignment = HorizontalAlignment.Center } } });
        else foreach (var m in chat.Messages) ui.Log.Children.Add(ChatMessage(dock, chat, m, m == chat.Messages[^1]));
        Dispatcher.UIThread.Post(() => ui.Scroll.ScrollToEnd(), DispatcherPriority.Background);
    }

    Control ChatNotice(ClaudeDock dock, ClaudeCode.Status s)
    {
        var sp = new StackPanel { Name = "claude-chat-notice", Spacing = 10, Margin = new Thickness(0, 40, 0, 0), MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Center };
        void Btn(string name, string label, Func<Task> a)
        {
            var b = new Button { Name = name, Content = label, Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 6), BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Center };
            b.Click += async (_, _) => { await a(); await ClaudeCheck(true); RenderChat(dock); };
            sp.Children.Add(b);
        }
        sp.Children.Add(new TextBlock { Text = "✳", FontSize = 30, Foreground = ClaudeAccent, HorizontalAlignment = HorizontalAlignment.Center });
        if (!s.Installed)
        {
            sp.Children.Add(new TextBlock { Text = "Claude Code ist auf diesem PC noch nicht eingerichtet.", FontSize = 16, Foreground = ClaudeFg, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            sp.Children.Add(new TextBlock { Text = "Trendo richtet es aus der offiziellen Quelle von Anthropic ein (nur für dich, ohne Admin). Danach meldest du dich einmal mit deinem Claude an.", FontSize = 12.5, Foreground = ClaudeMuted, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            Btn("claude-chat-install", "Claude Code einrichten", ClaudeInstall);
        }
        else
        {
            sp.Children.Add(new TextBlock { Text = s.LoggedIn ? "Claude ist über einen API-Zugang angemeldet – Trendo nutzt nur dein Claude-Abo." : "Melde dich einmal mit deinem Claude an.", FontSize = 16, Foreground = ClaudeFg, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            sp.Children.Add(new TextBlock { Text = "Die Anmeldung läuft über Claude selbst im Browser – Trendo sieht und speichert weder Passwort noch Schlüssel. Jede Person in Trendo meldet sich mit ihrem eigenen Claude an.", FontSize = 12.5, Foreground = ClaudeMuted, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            Btn("claude-chat-login", "Mit Claude anmelden", ClaudeConnect);
        }
        var again = new Button { Name = "claude-chat-recheck", Content = "Neu prüfen", Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5), BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Center };
        again.Click += async (_, _) => { await ClaudeCheck(true); RenderChat(dock); };
        sp.Children.Add(again);
        if (_claudeMsg.Length > 0) sp.Children.Add(new TextBlock { Text = _claudeMsg, Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        return sp;
    }

    /// <summary>Eine Nachricht wie in Claude: deine rechts als Blase, Claudes Antwort als Text mit Arbeitsschritten; bei der letzten Antwort mit Änderungen „Zurücknehmen“.</summary>
    Control ChatMessage(ClaudeDock dock, ClaudeChats.Chat chat, ClaudeChats.Msg m, bool last)
    {
        if (m.Role == "user")
            return new Border { Background = ClaudeItem, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10), HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 620,
                Child = new SelectableTextBlock { Text = m.Text, Foreground = ClaudeFg, FontSize = 14, TextWrapping = TextWrapping.Wrap } };
        var sp = new StackPanel { Spacing = 6 };
        if (m.Steps.Count > 0)
        {
            var steps = new StackPanel { Spacing = 2 };
            foreach (var st in m.Steps) steps.Children.Add(ChatStep(st));
            sp.Children.Add(new Expander { Header = $"{m.Steps.Count} Arbeitsschritt{(m.Steps.Count == 1 ? "" : "e")}", Content = steps, IsExpanded = false, Foreground = ClaudeMuted, Background = Brushes.Transparent });
        }
        foreach (var block in ChatBlocks(m.Text)) sp.Children.Add(block);
        if (m.Changed is { Count: > 0 } ch)
        {
            sp.Children.Add(new TextBlock { Text = "Geändert: " + string.Join(", ", ch.Take(8)) + (ch.Count > 8 ? $" … (+{ch.Count - 8})" : ""), Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (last && _chatUis.TryGetValue(dock, out var ui) && ui.LastSnapshot is not null && ui.LastSnapshotChat == chat.Id)
            {
                var undo = new Button { Name = "claude-chat-undo", Content = "↶ Zurücknehmen", Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 3), BorderThickness = new Thickness(0) };
                ToolTip.SetTip(undo, "Die Änderungen dieser Antwort im Projektordner rückgängig machen");
                undo.Click += (_, _) => ClaudeChatUndo(dock);
                sp.Children.Add(undo);
            }
        }
        return sp;
    }

    Control ChatStep(ClaudeChats.Step st) => new TextBlock { Text = (st.Error ? "⚠ " : st.Changes ? "✎ " : "● ") + st.Text, Foreground = st.Error ? Brush.Parse("#FFF87171") : ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap };

    /// <summary>Antworttext in Absätze und Codeblöcke (```) zerlegen – Code in Festbreitenschrift auf dunklem Grund wie in Claude.</summary>
    IEnumerable<Control> ChatBlocks(string text)
    {
        var parts = text.Split("```");
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (i % 2 == 1)
            {
                var nl = p.IndexOf('\n'); var code = nl >= 0 ? p[(nl + 1)..] : p;
                yield return new Border { Background = Brush.Parse("#FF1A1A18"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8),
                    Child = new SelectableTextBlock { Text = code.TrimEnd(), FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"), FontSize = 12.5, Foreground = ClaudeFg, TextWrapping = TextWrapping.Wrap } };
            }
            else if (p.Trim().Length > 0)
                yield return new SelectableTextBlock { Text = Regex.Replace(p.Trim(), @"\*\*(.+?)\*\*", "$1"), Foreground = ClaudeFg, FontSize = 14, TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
        }
    }

    /// <summary>Nachricht an Claude (öffentlich für Tests über das aktive Fenster). Ohne offenes Projekt legt die erste Nachricht eins an.</summary>
    public Task ClaudeChatSend(string text) => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? ClaudeChatSend(d, text) : Task.CompletedTask;
    public ClaudeChats.Chat? ClaudeChatCurrent() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) && _chatUis.TryGetValue(d, out var ui) && ui.ChatId is { } c ? CChats.Get(ChatStudio(d), c) : null;
    public bool ClaudeChatRunning() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) && _chatUis.TryGetValue(d, out var ui) && ui.Running;

    async Task ClaudeChatSend(ClaudeDock dock, string? text)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.Running || string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();
        var studio = ChatStudio(dock);
        var chats = CChats;
        var chat = ui.ChatId is { } cid ? chats.Get(studio, cid) : null;
        if (chat is null) { chat = chats.Create(studio, ClaudeChats.TitleOf(text), ClaudeChatRoot(dock.Studio)); ui.ChatId = chat.Id; _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.neu", studio); }
        chat = chats.Add(studio, chat.Id, new ClaudeChats.Msg("user", text, [], DateTimeOffset.Now));
        ui.Input.Text = ""; ui.Running = true; ui.Cts = new CancellationTokenSource();
        ui.Send.Content = "■"; ToolTip.SetTip(ui.Send, "Anhalten");
        RenderChatList(dock); RenderChat(dock);

        // Live-Antwort
        var live = new StackPanel { Name = "claude-chat-live", Spacing = 6 };
        var stepsPanel = new StackPanel { Spacing = 2 }; var textPanel = new StackPanel { Spacing = 6 };
        var thinking = new TextBlock { Text = "✳ Claude denkt nach …", Foreground = ClaudeAccent, FontSize = 13 };
        live.Children.Add(stepsPanel); live.Children.Add(textPanel); live.Children.Add(thinking);
        ui.Log.Children.Add(live);
        var steps = new List<ClaudeChats.Step>(); var say = new System.Text.StringBuilder(); string? error = null; string? session = null; var ok = true;

        var folder = chat.Folder; Directory.CreateDirectory(folder);
        if (dock.Studio is not null) try { Skills.ExportForClaude(folder, null, dock.Studio); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        FolderSnapshot? snap = null;
        try { snap = FolderSnapshot.Take(folder, Path.Combine(_core.DataRoot, "claude-chats", "sicherung", chat.Id)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var system = ClaudeChats.System(StudioCatalog.NameOf(dock.Studio ?? "", "Trendo"), ClaudeRoleFor(dock.Studio), Skills.For(null, dock.Studio));
        var model = KiSourceSettings.Load(_core.DataRoot).ClaudeModel;
        void OnLine(string l)
        {
            if (l.Contains("\"trendo_error\"")) { error = System.Text.Json.Nodes.JsonNode.Parse(l)?["text"]?.ToString(); return; }
            foreach (var ev in ClaudeCode.Parse(l, folder))
                Dispatcher.UIThread.Post(() =>
                {
                    switch (ev)
                    {
                        case ClaudeCode.Init i when i.Session.Length > 0: session = i.Session; break;
                        case ClaudeCode.Say sy: say.AppendLine(sy.Text).AppendLine(); textPanel.Children.Clear(); foreach (var b in ChatBlocks(say.ToString())) textPanel.Children.Add(b); break;
                        case ClaudeCode.Tool t: var st = new ClaudeChats.Step(t.Text, t.Changes); steps.Add(st); stepsPanel.Children.Add(ChatStep(st)); thinking.Text = "✳ " + t.Text + " …"; break;
                        case ClaudeCode.ToolDone td when td.Error: var se = new ClaudeChats.Step("Fehler: " + td.Text, false, true); steps.Add(se); stepsPanel.Children.Add(ChatStep(se)); break;
                        case ClaudeCode.Todo todo: thinking.Text = "✳ " + (todo.Items.FirstOrDefault(x => x.State == "in_progress").Text ?? "arbeitet") + " …"; break;
                        case ClaudeCode.Done dn: ok = dn.Ok; if (dn.Session.Length > 0) session = dn.Session; if (say.Length == 0 && dn.Text.Length > 0) say.Append(dn.Text); break;
                    }
                    ui.Scroll.ScrollToEnd();
                });
        }
        try
        {
            await Task.Run(() => ClaudeRun(ClaudeCode.Args(text, ClaudeCode.Phase.Chat, chat.Session, system, model is { Length: > 0 } ? model : null), folder, OnLine, ui.Cts.Token));
        }
        catch (OperationCanceledException) { error = "Angehalten."; }
        catch (InvalidOperationException e) { error = e.Message; }
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); // ausstehende Ereignisse abarbeiten
        var changed = snap?.Changes().Select(c => $"{c.File} ({c.Change})").ToList() ?? [];
        var answer = say.ToString().Trim();
        if (error is not null) answer = (answer.Length > 0 ? answer + "\n\n" : "") + "⚠ " + error;
        else if (!ok && answer.Length == 0) answer = "⚠ Claude hat die Antwort nicht fertigbekommen.";
        if (session is { Length: > 0 }) chats.SetSession(studio, chat.Id, session);
        chats.Add(studio, chat.Id, new ClaudeChats.Msg("assistant", answer, steps, DateTimeOffset.Now, changed.Count > 0 ? changed : null));
        ui.LastSnapshot = changed.Count > 0 ? snap : null; ui.LastSnapshotChat = chat.Id;
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat", $"{studio} · {changed.Count} Änderungen");
        ui.Running = false; ui.Cts = null; ui.Send.Content = "↑"; ToolTip.SetTip(ui.Send, "Senden (Enter) · neue Zeile mit Umschalt+Enter");
        RenderChatList(dock); RenderChat(dock);
    }

    /// <summary>Änderungen der letzten Antwort zurücknehmen (öffentlich für Tests über das aktive Fenster).</summary>
    public void ClaudeChatUndo() { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) ClaudeChatUndo(d); }
    void ClaudeChatUndo(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.LastSnapshot is not { } snap || ui.LastSnapshotChat is not { } cid) return;
        snap.Restore(); ui.LastSnapshot = null;
        CChats.Add(ChatStudio(dock), cid, new ClaudeChats.Msg("assistant", "↶ Die Änderungen der letzten Antwort sind zurückgenommen.", [], DateTimeOffset.Now));
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.zurueck", dock.Studio ?? "");
        RenderChat(dock);
    }

    /// <summary>Projekt löschen: Verlauf weg, Ordner in den Papierkorb (öffentlich für Tests über das aktive Fenster).</summary>
    public void ClaudeChatDelete(string id) { if (Windows.Active?.Id is { } w && _claudeDocks.TryGetValue(w, out var d)) ClaudeChatDelete(d, id); }
    void ClaudeChatDelete(ClaudeDock dock, string id)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.Running) return;
        var c = CChats.Delete(ChatStudio(dock), id);
        ui.ConfirmDelete = null;
        if (c is null) { RenderChatList(dock); return; }
        try
        {
            if (Directory.Exists(c.Folder))
            {
                if (ChatTrashOverride is { } f) f(c.Folder);
                else if (_opt.Isolated) Directory.Delete(c.Folder, true); // Testmodus: Temp-Ordner, nie den echten Papierkorb
                else RecycleBin.Move(c.Folder);
            }
            var snapDir = Path.Combine(_core.DataRoot, "claude-chats", "sicherung", c.Id);
            if (Directory.Exists(snapDir)) Directory.Delete(snapDir, true); // eigene Sicherungskopie von Trendo, keine Nutzerdaten
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _claudeMsg = "Ordner ließ sich nicht verschieben: " + e.Message; }
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.geloescht", c.Title);
        if (ui.ChatId == id) { ui.ChatId = CChats.All(ChatStudio(dock)).FirstOrDefault()?.Id; if (ui.LastSnapshotChat == id) ui.LastSnapshot = null; }
        RenderChatList(dock); RenderChat(dock);
    }
}
