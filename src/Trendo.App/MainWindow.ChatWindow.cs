using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>KI fragen als Chatfenster (Masterprompt 131, Runde 7): gespeicherte Gespräche links (neu, suchen, umbenennen, löschen),
/// Nachrichten als Sprechblasen rechts – Code mit „Kopieren“, Tabellen als Tabelle, jede Antwort kopieren oder als Dokument speichern;
/// Enter sendet, Umschalt+Enter neue Zeile, Stopp bricht ab; welche KI geantwortet hat, steht unter der Antwort.</summary>
public partial class MainWindow
{
    bool _askClaudeOpen;
    ChatStore? _chats; public ChatStore Chats => _chats ??= new ChatStore(_core.DataRoot);
    string? _chatId; string _chatSearch = ""; CancellationTokenSource? _askCts;
    /// <summary>Für Tests: zuletzt kopierter Text.</summary>
    public string LastCopied { get; private set; } = "";
    public string? CurrentChatId => _chatId;

    ChatStore.Chat CurrentChat() => _chatId is { } id && Chats.Get(id) is { } c ? c : new ChatStore.Chat();

    async void Copy(string text, string what)
    {
        LastCopied = text;
        try { if (Clipboard is { } cb) await cb.SetTextAsync(text); _askState = $"{what} kopiert."; }
        catch (Exception) { _askState = $"{what} ließ sich nicht kopieren."; }
        Show("ki-fragen");
    }

    /// <summary>Frage stellen (auch von außen/Tests). Speichert Frage und Antwort im aktuellen Gespräch.</summary>
    public async Task AskChat(string q)
    {
        q = q.Trim();
        if (q.Length == 0) { _askState = "Bitte schreib zuerst eine Frage."; Show("ki-fragen"); return; }
        if (_askCts is not null) return;
        _askCts = new CancellationTokenSource(); _askState = "Trendo denkt nach …"; _chatPending = q; Show("ki-fragen");
        try
        {
            using var kiLabel = KiActivityHub.Label("Trendo KI");
            var a = await Intel.AskAsync(new KiRequest(q, PreferredModel: KiModelName, History: _chat.ToList(), AllowRemote: true, Personal: true), _askCts.Token);
            LastAnswer = a.Text;
            if (a.Ok)
            {
                _chat.Add(new("user", q)); _chat.Add(new("assistant", a.Text));
                LearnFromChat(q);
                var c = Chats.Append(CurrentChat(), q, a.Text, a.Model is null ? null : $"{a.Model} · {a.Backend}"); _chatId = c.Id;
            }
            _askState = a.Ok ? $"Antwort von {a.Model} ({a.Backend}{(KiCatalog.Cloud.Any(c => c.Name == a.Backend) ? ", außerhalb des PCs" : "")}) in {a.Took.TotalSeconds:0.0} s{(a.Sources.Count > 0 ? " · Wissen: " + string.Join(", ", a.Sources) : "")}" : a.Text;
        }
        catch (OperationCanceledException) { _askState = "Abgebrochen."; }
        finally { _askCts?.Dispose(); _askCts = null; _chatPending = null; Show("ki-fragen"); }
    }
    string? _chatPending;

    void OpenChat(string? id)
    {
        _chatId = id; _chat.Clear();
        if (id is not null && Chats.Get(id) is { } c) foreach (var m in c.Messages) _chat.Add(new(m.Role, m.Content));
        _askState = ""; Show("ki-fragen");
    }

    private Control AskView()
    {
        var chat = CurrentChat();
        // ── links: Gespräche ──
        var left = new StackPanel { Name = "ask-chats", Spacing = 4 };
        left.Children.Add(Act("ask-chat-new", "＋ Neues Gespräch", () => OpenChat(null), true));
        var search = new TextBox { Name = "ask-chat-search", Text = _chatSearch, PlaceholderText = "Gespräche durchsuchen" };
        search.KeyUp += (_, e) => { if (e.Key == Key.Enter || (search.Text ?? "").Length == 0) { _chatSearch = search.Text ?? ""; Show("ki-fragen"); } };
        left.Children.Add(search);
        var all = Chats.All(_chatSearch); var i = 0;
        foreach (var c in all.Take(60))
        {
            var id = c.Id; var on = id == _chatId;
            var b = new Button { Name = $"ask-chat-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5),
                Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent,
                Content = new StackPanel { Children = { new TextBlock { Text = c.Title, Foreground = Text, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal },
                    new TextBlock { Text = $"{c.Updated.LocalDateTime.ToString("dd.MM. HH:mm", De)} · {c.Messages.Count / 2} Frage{(c.Messages.Count == 2 ? "" : "n")}", Foreground = Muted, FontSize = 10.5 } } } };
            b.Click += (_, _) => OpenChat(id);
            left.Children.Add(b);
        }
        if (all.Count == 0) left.Children.Add(P(_chatSearch.Length > 0 ? "Nichts gefunden." : "Noch keine Gespräche.", Muted, 11.5));
        if (_chatId is not null)
        {
            var title = new TextBox { Name = "ask-chat-title", Text = chat.Title, Margin = new Thickness(0, 10, 0, 0) };
            left.Children.Add(title);
            left.Children.Add(new WrapPanel { Children = {
                Act("ask-chat-rename", "Umbenennen", () => { try { Chats.Rename(chat.Id, title.Text ?? ""); _askState = "Umbenannt."; } catch (ArgumentException e) { _askState = e.Message; } Show("ki-fragen"); }),
                new Border { Width = 4 },
                RemoveButton("chat:" + chat.Id, "ask-chat-del", () => { Chats.Delete(chat.Id); _chatId = null; _chat.Clear(); _askState = "Gespräch gelöscht."; }),
                new Border { Width = 4 },
                Act("ask-chat-save", "Als Dokument", () => { var f = Path.Combine(WorkFolder("KI-Gespräche"), DevStudio.SafeName(chat.Title) + ".md"); File.WriteAllText(f, ChatStore.ToMarkdown(chat)); _askState = "Gespeichert: " + f; NotifyFileResult(_askState); Show("ki-fragen"); }) } });
        }

        left.Children.Add(KiMemoryPanel());

        // ── rechts: Verlauf ──
        var sp = new StackPanel { Spacing = 8 };
        // Umbau 04.11.2026: wie in Chat-Programmen zuerst das Gespräch – Claude-Einstellungen als kurze Zeile zum Aufklappen
        var cs = _claude;
        var state = !cs.Installed ? "Claude: nicht eingerichtet – aufklappen zum Einrichten" : !cs.Ready ? "Claude: nicht verbunden – aufklappen zum Verbinden" : "✓ Claude verbunden – Einstellungen";
        var cbox = new Expander { Name = "ask-claude-box", IsExpanded = _askClaudeOpen, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0, 6, 0, 0), Content = ClaudeCard(),
            Header = new TextBlock { Text = state, Foreground = cs.Ready ? Brush.Parse("#22B35E") : Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold } };
        cbox.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) _askClaudeOpen = cbox.IsExpanded; };
        sp.Children.Add(cbox);
        sp.Children.Add(P(KiSourceSettings.Load(_core.DataRoot).Cloud.Count == 0 ? "Stell eine Frage. Die Antwort kommt von der KI auf diesem PC – nichts geht ins Internet." : "Stell eine Frage. Zuerst antwortet die KI auf diesem PC. Nur wenn sie schweigt, springt dein eingeschalteter KI-Dienst ein – dann geht nur diese eine Frage hinaus, ohne Verlauf und ohne deine Dokumente.", Muted, 11.5));
        var log = new StackPanel { Name = "ask-log", Spacing = 8 };
        var input = new TextBox { Name = "ask-input", PlaceholderText = "Nachricht an die Trendo KI … (Enter sendet, Umschalt+Enter neue Zeile)", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54, MaxHeight = 180 };
        var send = new Button { Name = "ask-send", Content = "Senden", Background = Accent, IsEnabled = _askCts is null };
        send.Click += async (_, _) => { var q = input.Text ?? ""; if (q.Trim().Length > 0) input.Text = ""; await AskChat(q); };
        input.AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { e.Handled = true; send.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var stop = new Button { Name = "ask-stop", Content = "Stopp", IsVisible = _askCts is not null };
        stop.Click += (_, _) => _askCts?.Cancel();
        var clear = new Button { Name = "ask-clear", Content = "Neues Gespräch" };
        clear.Click += (_, _) => OpenChat(null);

        var msgs = chat.Messages;
        for (var k = 0; k < msgs.Count; k++) log.Children.Add(ChatBubble(msgs[k], k));
        if (_chatPending is { } pending)
        {
            log.Children.Add(ChatBubble(new ChatStore.Message("user", pending, DateTimeOffset.Now), -1));
            log.Children.Add(new TextBlock { Name = "ask-thinking", Text = "Trendo denkt nach …", Foreground = Muted, FontStyle = FontStyle.Italic });
        }
        if (msgs.Count == 0 && _chatPending is null)
        {
            Button Ask(string n, string q) => Chip(n, q, () => { input.Text = q; send.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); });
            sp.Children.Add(EmptyState("ask-empty", "💬", "Frag mich etwas", "Tippe unten deine Frage – oder probier ein Beispiel:",
                Ask("ask-ex-0", "Schreib eine freundliche Absage für einen Termin"), Ask("ask-ex-1", "Erkläre mir kurz, was eine Firewall macht"),
                Ask("ask-ex-2", "Plane mir ein Abendessen für 4 Personen"), Ask("ask-ex-3", "Fasse die Vorteile von Open Source in 3 Punkten zusammen")));
        }
        sp.Children.Add(log);
        // neueste Nachricht ins Bild holen
        if (log.Children.Count > 0 && log.Children[^1] is Control lastMsg) lastMsg.AttachedToVisualTree += (_, _) => _ui.Post(() => lastMsg.BringIntoView(), Avalonia.Threading.DispatcherPriority.Background);
        // Eingabe fest am unteren Rand (wie in Chatprogrammen) – der Verlauf rollt darüber
        var dock = new StackPanel { Name = "ask-dock", Spacing = 6, Children = {
            input,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { send, stop, clear } },
            new TextBlock { Name = "ask-state", Text = _askState, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } };
        if (_chat.Count >= 2)
        {
            var (lq, la) = (_chat[^2].Content, _chat[^1].Content);
            var good = new Button { Name = "ask-good", Content = "Gut" };
            good.Click += (_, _) => { Wissen.Feedback(lq, la, true); _askState = "Danke für deine Rückmeldung."; Show("ki-fragen"); };
            var fix = new TextBox { Name = "ask-fix", PlaceholderText = "Was ist richtig? Trendo merkt es sich.", Width = 320 };
            var bad = new Button { Name = "ask-bad", Content = "Verbessern" };
            bad.Click += (_, _) => { Wissen.Feedback(lq, la, false, fix.Text); _askState = string.IsNullOrWhiteSpace(fix.Text) ? "Danke. Schreib beim nächsten Mal dazu, was richtig ist – dann lernt Trendo daraus." : "Gemerkt. Trendo nutzt die Korrektur bei ähnlichen Fragen."; Show("ki-fragen"); };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { good, fix, bad } });
        }
        var hist = Intel.History(5);
        if (hist.Count > 0)
        {
            var hp = new StackPanel { Spacing = 2 };
            foreach (var h in hist) hp.Children.Add(P($"{h.At:dd.MM. HH:mm} · {h.Question}", null, 11));
            var del = new Button { Name = "ask-history-clear", Content = "Verlauf löschen" };
            del.Click += (_, _) => { Intel.ClearHistory(); Show("ki-fragen"); };
            hp.Children.Add(del);
            sp.Children.Add(new Expander { Name = "ask-history", Header = "Zuletzt gefragt", Content = hp });
        }

        var body = new Grid { Name = "ask-window", ColumnDefinitions = new ColumnDefinitions("240,12,*"), Children = {
            new Border { Name = "ask-side", CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), Child = new ScrollViewer { Content = left } },
            Pos(sp, 2) } };
        return Page("KI fragen", new StackPanel { Spacing = 8, Children = { KiNav(), body } }, dock);
    }

    /// <summary>Eine Nachricht als Sprechblase: Du rechts, Trendo links; Text, Code (mit Kopieren) und Tabellen getrennt dargestellt.</summary>
    Control ChatBubble(ChatStore.Message m, int k)
    {
        var mine = m.Role == "user";
        var content = new StackPanel { Spacing = 6 };
        var segs = mine ? [new ChatStore.Segment(ChatStore.Kind.Text, m.Content)] : ChatStore.Segments(m.Content);
        var c = 0;
        foreach (var s in segs)
        {
            switch (s.Kind)
            {
                case ChatStore.Kind.Code:
                    var code = s.Content;
                    content.Children.Add(new Border { Background = Brush.Parse("#0B1220"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8), Child = new StackPanel { Spacing = 4, Children = {
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = s.Language.Length > 0 ? s.Language : "Code", Foreground = Muted, FontSize = 10.5 },
                            Pos(Act($"ask-code-copy-{k}-{c++}", "Kopieren", () => Copy(code, "Code")), 1) } },
                        new SelectableTextBlock { Text = code, FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), Foreground = Brush.Parse("#E2E8F0"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap } } } });
                    break;
                case ChatStore.Kind.Table when s.Rows is { Count: > 0 } rows:
                    var g = new Grid(); var cols = rows.Max(r => r.Count);
                    for (var x = 0; x < cols; x++) g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    for (var y = 0; y < rows.Count; y++)
                    {
                        g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                        for (var x = 0; x < rows[y].Count; x++)
                        {
                            var cell = new Border { BorderBrush = Edge, BorderThickness = new Thickness(0, 0, 1, 1), Padding = new Thickness(8, 3), Background = y == 0 ? Brush.Parse("#1AC9A227") : null,
                                Child = new TextBlock { Text = rows[y][x], Foreground = Text, FontSize = 12.5, FontWeight = y == 0 ? FontWeight.SemiBold : FontWeight.Normal } };
                            Grid.SetRow(cell, y); Grid.SetColumn(cell, x); g.Children.Add(cell);
                        }
                    }
                    content.Children.Add(new Border { Name = $"ask-table-{k}", BorderBrush = Edge, BorderThickness = new Thickness(1, 1, 0, 0), CornerRadius = new CornerRadius(6), HorizontalAlignment = HorizontalAlignment.Left, Child = g });
                    break;
                default:
                    content.Children.Add(new SelectableTextBlock { Text = s.Content, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap });
                    break;
            }
        }
        if (!mine && k >= 0)
        {
            var text = m.Content;
            content.Children.Add(new WrapPanel { Children = {
                new TextBlock { Text = m.Model is { Length: > 0 } md ? md : "", Foreground = Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) },
                Act($"ask-copy-{k}", "Kopieren", () => Copy(text, "Antwort")),
                new Border { Width = 4 },
                Act($"ask-save-{k}", "Als Dokument", () => { var f = Path.Combine(WorkFolder("KI-Antworten"), $"Antwort {DateTime.Now:yyyy-MM-dd HH-mm-ss}.md"); File.WriteAllText(f, text); _askState = "Gespeichert: " + f; NotifyFileResult(_askState); Show("ki-fragen"); }) } });
        }
        return new Border { Name = k >= 0 ? $"ask-msg-{k}" : "ask-msg-pending", MaxWidth = 760, HorizontalAlignment = mine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = mine ? Brush.Parse("#33C9A227") : TileBg, BorderBrush = mine ? Accent : EdgeLit, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14, 14, mine ? 4 : 14, mine ? 14 : 4), Padding = new Thickness(12, 8), Child = content };
    }
}
