using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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

    /// <summary>T-0734: Dateien, die mit der nächsten Frage mitgehen (Knopf „Datei anhängen“ oder Ablegen auf dem Chat).</summary>
    readonly List<string> _askFiles = [];
    public IReadOnlyList<string> AskFiles => _askFiles;
    /// <summary>T-0734: bisher geschriebene Antwort, während sie entsteht (wird aus dem KI-Faden gesetzt, die Anzeige folgt über den UI-Faden).</summary>
    volatile string _streamText = ""; SelectableTextBlock? _streamBlock;
    public string AskStreamText => _streamText;
    /// <summary>Für Tests: Text, den die Sprechblase „ask-stream“ gerade zeigt.</summary>
    public string? AskStreamShown => _streamBlock?.Text;

    /// <summary>T-0734: Dateien an die nächste Frage hängen (höchstens <see cref="KiAttachments.MaxFiles"/>). Nicht lesbare Arten werden mit Hinweis abgelehnt.</summary>
    public void AskAttach(IEnumerable<string> paths)
    {
        var skipped = new List<string>();
        foreach (var p in paths)
        {
            if (!File.Exists(p) || !KiAttachments.IsSupported(p) || _askFiles.Count >= KiAttachments.MaxFiles) { skipped.Add(Path.GetFileName(p)); continue; }
            if (!_askFiles.Contains(p, StringComparer.OrdinalIgnoreCase)) _askFiles.Add(p);
        }
        _askState = skipped.Count > 0 ? $"Nicht angehängt: {string.Join(", ", skipped)} – möglich sind Text (TXT, MD, CSV), PDF, Word (DOCX, ODT) und Bilder, höchstens {KiAttachments.MaxFiles} Dateien." : _askFiles.Count > 0 ? AskAttachHint() : _askState;
        Show("ki-fragen");
    }

    async Task AskAttachPick()
    {
        string[]? got;
        if (PickOverride is { } fake) got = await fake(new PickRequest("ask-attach", false, true, KiAttachments.Patterns));
        else got = (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Datei an die Frage hängen", AllowMultiple = true,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Text, PDF, Word, Bild") { Patterns = KiAttachments.Patterns }] })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is { Length: > 0 }) AskAttach(got);
    }

    /// <summary>T-0734: Wohin gehen die angehängten Dateien? Bilder bleiben immer auf diesem PC; Text geht an Claude (dein Abo), wenn Claude die Haupt-KI ist, sonst nur an die KI auf diesem PC.</summary>
    public string AskAttachHint()
    {
        var img = _askFiles.Any(KiAttachments.IsImage); var txt = _askFiles.Any(f => !KiAttachments.IsImage(f));
        var set = KiSourceSettings.Load(_core.DataRoot);
        var claude = ClaudeEmbedApproved && set.ClaudeMain && _claude.Ready && !_core.Offline;
        var parts = new List<string>();
        if (txt) parts.Add(claude ? "Der Text der Datei geht mit deiner Frage an Claude (dein Abo, außerhalb des PCs). Antwortet Claude nicht, liest ihn die KI auf diesem PC." : "Der Text der Datei bleibt auf diesem PC – nur die KI auf diesem PC liest ihn.");
        if (img) parts.Add("Bilder bleiben auf diesem PC und gehen nur an ein Bildmodell der KI auf diesem PC (z. B. qwen2.5vl).");
        parts.Add("An andere KI-Dienste im Internet geht kein Anhang.");
        return string.Join(" ", parts);
    }

    /// <summary>Frage stellen (auch von außen/Tests). Speichert Frage und Antwort im aktuellen Gespräch.</summary>
    public async Task AskChat(string q)
    {
        q = q.Trim();
        if (q.Length == 0 && _askFiles.Count > 0) q = _askFiles.All(KiAttachments.IsImage) ? "Was ist auf dem Bild zu sehen?" : "Fasse die angehängte Datei kurz zusammen.";
        if (q.Length == 0) { _askState = "Bitte schreib zuerst eine Frage."; Show("ki-fragen"); return; }
        if (_askCts is not null) return;
        // T-0734: Anhänge vor dem Senden lesen – geht etwas nicht, bleibt alles stehen und die Meldung sagt, was fehlt
        var files = new List<KiAttachment>();
        foreach (var f in _askFiles)
            try { files.Add(KiAttachments.Load(f, Tool("pdftotext"))); }
            catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException) { _askState = e is InvalidOperationException ? e.Message : $"„{Path.GetFileName(f)}“ lässt sich nicht lesen: {e.Message}"; Show("ki-fragen"); return; }
        var shown = files.Count > 0 ? q + "\n\n📎 " + string.Join(", ", files.Select(f => f.Name)) : q;
        _askCts = new CancellationTokenSource(); _askState = files.Count > 0 ? "Trendo liest die Datei und denkt nach …" : "Trendo denkt nach …"; _chatPending = shown; _streamText = ""; Show("ki-fragen");
        try
        {
            using var kiLabel = KiActivityHub.Label("Trendo KI");
            void OnText(string t) { _streamText = t; _ui.Post(() => { if (_streamBlock is { } sb) sb.Text = _streamText.Length > 0 ? _streamText : "Trendo denkt nach …"; }); }
            var a = await Intel.AskAsync(new KiRequest(q, PreferredModel: KiModelName, History: _chat.ToList(), AllowRemote: true, Personal: true, Attachments: files, OnText: OnText), _askCts.Token);
            LastAnswer = a.Text;
            if (a.Ok)
            {
                _chat.Add(new("user", KiAttachments.Compose(q, files))); _chat.Add(new("assistant", a.Text)); // Folgefragen kennen den Dateitext
                LearnFromChat(q);
                var c = Chats.Append(CurrentChat(), shown, a.Text, a.Model is null ? null : $"{a.Model} · {a.Backend}"); _chatId = c.Id;
                _askFiles.Clear();
            }
            _askState = a.Ok ? $"Antwort von {a.Model} ({a.Backend}{(KiCatalog.Cloud.Any(c => c.Name == a.Backend) || a.Backend?.StartsWith("Claude", StringComparison.Ordinal) == true ? ", außerhalb des PCs" : "")}) in {a.Took.TotalSeconds:0.0} s{(a.Sources.Count > 0 ? " · Wissen: " + string.Join(", ", a.Sources) : "")}" : a.Text;
        }
        catch (OperationCanceledException)
        {
            // T-0734: was bis zum Stopp geschrieben war, bleibt im Gespräch stehen (wie in Chat-Programmen)
            if (_streamText.Trim().Length > 0)
            {
                var part = _streamText.Trim() + "\n\n(abgebrochen)";
                _chat.Add(new("user", KiAttachments.Compose(q, files))); _chat.Add(new("assistant", part));
                var c = Chats.Append(CurrentChat(), shown, part, "abgebrochen"); _chatId = c.Id; _askFiles.Clear();
            }
            _askState = "Abgebrochen.";
        }
        finally { _askCts?.Dispose(); _askCts = null; _chatPending = null; _streamText = ""; _streamBlock = null; Show("ki-fragen"); }
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
                Act("ask-chat-save", "Als Dokument", () => { var f = Path.Combine(WorkFolder("KI-Gespräche"), DevStudio.SafeName(chat.Title) + ".md"); AtomicFile.WriteAllText(f, ChatStore.ToMarkdown(chat)); /* T-0717: atomar */ _askState = "Gespeichert: " + f; NotifyFileResult(_askState); Show("ki-fragen"); }) } });
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
        var al = AskLink; LinkForm(al, "eingabe", ("text", input)); // T-0717: noch nicht gesendete Nachricht als Entwurf (Rückgängig, Notsicherung, Karte)
        send.Click += async (_, _) => { var q = input.Text ?? ""; if (q.Trim().Length > 0) { input.Text = ""; LinkClose(al); } await AskChat(q); };
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
            // T-0734: die Antwort wächst sichtbar in ihrer Sprechblase, während sie entsteht
            _streamBlock = new SelectableTextBlock { Name = "ask-stream-text", Text = _streamText.Length > 0 ? _streamText : "Trendo denkt nach …", Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap };
            log.Children.Add(new Border { Name = "ask-stream", MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left, Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14, 14, 14, 4), Padding = new Thickness(12, 8), Child = new StackPanel { Spacing = 4, Children = { _streamBlock,
                    new TextBlock { Name = "ask-thinking", Text = "schreibt … – „Stopp“ bricht ab", Foreground = Muted, FontSize = 10.5, FontStyle = FontStyle.Italic } } } });
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
        var (aUndo, aRedo) = LinkButtons(al);
        // T-0734: Datei anhängen (Knopf oder auf den Chat ziehen) – mit klarem Hinweis, wohin die Datei geht
        var attach = new Button { Name = "ask-attach", Content = "📎 Datei anhängen", IsEnabled = _askCts is null };
        ToolTip.SetTip(attach, "Text, PDF, Word oder Bild an die Frage hängen – oder die Datei einfach auf den Chat ziehen");
        attach.Click += async (_, _) => await AskAttachPick();
        var chips = new WrapPanel { Name = "ask-files", IsVisible = _askFiles.Count > 0 };
        for (var f = 0; f < _askFiles.Count; f++)
        {
            var path = _askFiles[f];
            var x = new Button { Name = $"ask-file-del-{f}", Content = "✕", Padding = new Thickness(4, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Muted };
            ToolTip.SetTip(x, "Anhang entfernen");
            x.Click += (_, _) => { _askFiles.Remove(path); _askState = _askFiles.Count > 0 ? AskAttachHint() : ""; Show("ki-fragen"); };
            chips.Children.Add(new Border { Name = $"ask-file-{f}", Background = Brush.Parse("#22C9A227"), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3, 4, 3), Margin = new Thickness(0, 0, 6, 4),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new TextBlock { Text = (KiAttachments.IsImage(path) ? "🖼 " : "📄 ") + Path.GetFileName(path), Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, x } } });
        }
        var hint = new TextBlock { Name = "ask-attach-hint", Text = _askFiles.Count > 0 ? "🔒 " + AskAttachHint() : "", IsVisible = _askFiles.Count > 0, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        var dock = new StackPanel { Name = "ask-dock", Spacing = 6, Children = {
            chips, hint,
            input,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { send, stop, attach, clear, aUndo, aRedo } },
            new TextBlock { Name = "ask-state", Text = _askState, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } };
        if (LinkRecoveryCard(al) is { } askCard) dock.Children.Insert(0, askCard); // T-0717: nicht gesendete Nachricht aus einer früheren Sitzung
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
        // T-0734: Dateien auf den Chat (oder die Eingabe) ziehen hängt sie an die Frage
        foreach (var target in new Control[] { body, dock })
        {
            DragDrop.SetAllowDrop(target, true);
            target.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
            target.AddHandler(DragDrop.DropEvent, (_, e) =>
            {
                var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
                if (paths.Count > 0) { e.Handled = true; AskAttach(paths); }
            });
        }
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
