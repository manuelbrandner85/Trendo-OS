using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;

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
        public string? ConfirmDelete, Renaming; public FolderSnapshot? LastSnapshot; public string? LastSnapshotChat;
        // Vorschau rechts (Wunsch 01.11.2026: was Claude baut, live sehen; vorher/nachher vergleichen)
        public Border Preview = new() { Name = "claude-preview" }; public ColumnDefinition PreviewCol = new(0, GridUnitType.Pixel);
        public string? PreviewFile; public string? PreviewOriginal; public string Compare = "nachher"; public NativeWebView? PreviewWeb; public string? PreviewNote;
        public List<string> Pending = []; public WrapPanel Chips = new() { Name = "claude-chat-attachments" };
        public bool AskFull; public Button? FullButton; public bool FullForNew, SafeForNew;
        public TextBlock ActiveKi = new(); public string? LastKi; public TextBlock Caps = new(); public int IdeaPage;
        public List<ClaudeDraft> Drafts = [];
        // Weiterschreiben, während Claude arbeitet (Wunsch 02.11.2026): Nachrichten warten und kommen gleich danach dran
        public Queue<string> Queue = new(); public StackPanel QueuePanel = new() { Name = "claude-chat-queue", Spacing = 6 };
        // 0.9.37: offenes Feld (Gedächtnis, Zeitreise), freihändig sprechen, bereits gezeigte Nachrichten (nur neue werden eingeblendet)
        public string? Panel; public bool HandsFree; public CancellationTokenSource? HandsCts; public string? ShownChat; public int Shown;
        // schmaler Claude-Bereich (Studio links offen): Projektliste einklappen, damit das Gespräch Platz hat
        public ColumnDefinition SideCol = new(230, GridUnitType.Pixel); public bool SideOpen; public Button? SideToggle;
        public WrapPanel Flows = new() { Name = "claude-flows", Margin = new Thickness(24, 0, 24, 2), IsVisible = false }; // gemerkte Abläufe
        public ClaudePlan? Plan; // was Claude im offenen Programm tun möchte (wartet auf „Ausführen“)
    }
    /// <summary>Spiel, das Claude im Spiele-Studio gebaut hat (wird dort oben gezeigt), mit Vorschaubild.</summary>
    string? _gameClaude, _gameClaudeShot;
    readonly Dictionary<ClaudeDock, ChatUi> _chatUis = [];

    ClaudeChats CChats => new(_core.DataRoot);
    /// <summary>Für Tests: statt in den Papierkorb (Ordner eines gelöschten Projekts).</summary>
    public static Action<string>? ChatTrashOverride { get; set; }

    /// <summary>Ordner, in dem die Projekte eines Studios liegen.</summary>
    /// <summary>Spiele und Webseiten landen im Ordner des jeweiligen Studios, damit sie dort auch in den eigenen Listen stehen.</summary>
    public string ClaudeChatRoot(string? studio) => studio switch
    {
        "CORE-06" => GameFolder,
        "CORE-07" => DevFolder,
        _ => Path.Combine(WorkFolder("Claude"), Regex.Replace(StudioCatalog.NameOf(studio ?? "", "Allgemein"), @"[\\/:*?""<>|]", "-")),
    };

    void BuildChatPane(ClaudeDock dock)
    {
        var ui = _chatUis[dock] = new ChatUi();
        ui.Scroll = new ScrollViewer { Name = "claude-chat-scroll", Content = ui.Log, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        ui.Title = new TextBlock { Name = "claude-chat-title", Foreground = ClaudeFg, FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ui.Input = new TextBox { Name = "claude-chat-input", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, MaxHeight = 200, FontSize = 14,
            PlaceholderText = "Schreib Claude, was du bauen möchtest …", Background = ClaudeInput, Foreground = ClaudeFg, BorderBrush = ClaudeLine, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 52, 10) };
        ui.Input.KeyDown += (_, e) => { if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { e.Handled = true; ClaudeChatSubmit(dock); } };
        ui.Send = new Button { Name = "claude-chat-send", Content = "↑", Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = ClaudeAccent, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 10, 10), HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(0) };
        ToolTip.SetTip(ui.Send, "Senden (Enter) · neue Zeile mit Umschalt+Enter");
        ui.Send.Click += (_, _) => { if (ui.Running && string.IsNullOrWhiteSpace(ui.Input.Text)) ui.Cts?.Cancel(); else ClaudeChatSubmit(dock); };
        var attach = new Button { Name = "claude-chat-attach", Content = "📎", Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = ClaudeItem, Foreground = ClaudeFg, FontSize = 15,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 50, 10), HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(0) };
        ToolTip.SetTip(attach, "Bilder oder Dateien anhängen (oder einfach hierher ziehen)");
        attach.Click += async (_, _) => await ClaudeChatAttachPick(dock);
        ui.Input.Padding = new Thickness(14, 10, 92, 10);
        var inputBox = new Grid { Margin = new Thickness(24, 0, 24, 6), Children = { ui.Input, attach, ui.Send } };
        ui.Chips = new WrapPanel { Name = "claude-chat-attachments", Margin = new Thickness(24, 0, 24, 4) };
        var hint = new TextBlock { Text = "Claude arbeitet mit deinem Abo im Ordner dieses Projekts und darf bei Bedarf ins Internet und Befehle ausführen („🔒 Sicher“ schaltet das für ein Projekt ab). Änderungen kannst du nach jeder Antwort zurücknehmen.", Foreground = ClaudeMuted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(26, 0, 26, 12), HorizontalAlignment = HorizontalAlignment.Center };
        var folder = new Button { Name = "claude-chat-folder", Content = "📁 Ordner", FontSize = 12, Padding = new Thickness(10, 3), Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        ToolTip.SetTip(folder, "Ordner dieses Projekts öffnen");
        folder.Click += (_, _) => { if (ui.ChatId is { } id && CChats.Get(ChatStudio(dock), id) is { } c) Launch(c.Folder); };
        var eye = new Button { Name = "claude-chat-preview-toggle", Content = "👁 Vorschau", FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 0), Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        ToolTip.SetTip(eye, "Vorschau ein- oder ausblenden – zeigt live, was Claude baut");
        eye.Click += (_, _) => { if (ui.PreviewCol.Width.Value > 0) ui.PreviewCol.Width = new GridLength(0); else ShowPreview(dock, ui.PreviewFile); };
        var full = new Button { Name = "claude-chat-full", FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 0), Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        ToolTip.SetTip(full, "Volle Claude-Funktionen für dieses Projekt: Befehle, Internet, deine eigenen Skills, Agenten, Mods (Plugins) und MCP-Werkzeuge – auch Computersteuerung, wenn du sie in Claude Code eingerichtet hast");
        full.Click += (_, _) => { ui.AskFull = !ui.AskFull; RenderChat(dock); };
        ui.FullButton = full;
        var app = new Button { Name = "claude-chat-app", Content = "Claude-App", FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 0), Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        ToolTip.SetTip(app, "Cowork, Claude Design und mehr: in der offiziellen Claude-App öffnen");
        app.Click += (_, _) => ClaudeOpenApp();
        ui.ActiveKi = new TextBlock { Name = "claude-chat-active", FontSize = 11.5, Foreground = ClaudeMuted, Margin = new Thickness(24, 0, 24, 4), TextWrapping = TextWrapping.Wrap };
        Button HeadBtn(string name, string label, string tip, Action a)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 0), Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => a(); return b;
        }
        void TogglePanel(string p) { ui.Panel = ui.Panel == p ? null : p; RenderChat(dock); }
        var memory = HeadBtn("claude-chat-memory", "🧠 Gedächtnis", "Projekt-Gedächtnis: was Claude sich für dieses Projekt dauerhaft merkt", () => TogglePanel("gedaechtnis"));
        var travel = HeadBtn("claude-chat-timeline", "🕘 Zeitreise", "Zeitreise: zu einem früheren Stand des Projekts zurück", () => TogglePanel("zeitreise"));
        var docs = HeadBtn("claude-chat-docs", "📎 Unterlagen", "Deine Unterlagen aus „Wissen“ für dieses Projekt an Claude freigeben – nur, was du auswählst", () => TogglePanel("unterlagen"));
        var publish = HeadBtn("claude-chat-publish", "🚀 Veröffentlichen", "Projekt fertig verpacken (ZIP im Ordner „Veröffentlicht“) – erst nach deinem Ja, hochgeladen wird nichts", () => TogglePanel("veroeffentlichen"));
        var github = HeadBtn("claude-chat-github", "⬆ GitHub", "Projekt als eigenes Repo auf GitHub hochladen – mit deiner GitHub-Anmeldung, erst nach deinem Ja", () => TogglePanel("github"));
        var netlify = HeadBtn("claude-chat-netlify", "🌐 Netlify", "Webseite auf Netlify veröffentlichen – öffentlich im Internet, erst nach deinem Ja", () => TogglePanel("netlify"));
        netlify.IsVisible = dock.Studio == "CORE-07"; // nur im Web-Studio: dort entstehen Webseiten
        var rules = HeadBtn("claude-chat-rules", "📚 Regeln", "Regeln dieses Studios: was Claude hier immer beachtet – eingebaut, aus GAMEFORGE (Spiele) und aus deinen Projekten gelernt (nur mit deinem Ja)", () => TogglePanel("regeln"));
        var review = HeadBtn("claude-chat-review", "🎓 Gutachter", "Zweite Meinung: ein Gutachter bewertet das Ergebnis mit Note 1–10 und sagt, was besser geht", () => _ = ClaudeReviewRun(dock));
        ui.SideToggle = new Button { Name = "claude-chat-side-toggle", Content = "☰", FontSize = 13, Padding = new Thickness(9, 3), Margin = new Thickness(0, 0, 8, 0), Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0), IsVisible = false };
        ToolTip.SetTip(ui.SideToggle, "Projekte dieses Studios ein- oder ausblenden");
        ui.SideToggle.Click += (_, _) => { ui.SideOpen = !ui.SideOpen; ui.SideCol.Width = new GridLength(ui.SideOpen ? 230 : 0); };
        // Kopf in zwei Zeilen: Titel, darunter die Knöpfe (umbrechend) – so passt alles auch in den schmalen Bereich neben dem Studio
        foreach (var hb in new Control[] { memory, travel, docs, rules, review, publish, github, netlify, full, app, eye, folder }) hb.Margin = new Thickness(0, 0, 6, 6);
        var head = new StackPanel { Margin = new Thickness(24, 12, 24, 2), Spacing = 6, Children = {
            new DockPanel { Children = { Dk(ui.SideToggle, Avalonia.Controls.Dock.Left), ui.Title } },
            new WrapPanel { Name = "claude-chat-head-tools", Children = { memory, travel, docs, rules, review, publish, github, netlify, full, app, eye, folder } } } };
        var bottom = new StackPanel { Children = { ClaudeToolBar(dock), ui.Flows, ui.Chips, inputBox, hint } };
        ui.Body = new Border { Child = ui.Scroll };
        ui.Caps = new TextBlock { Name = "claude-chat-caps", FontSize = 11, Foreground = ClaudeMuted, Margin = new Thickness(24, 0, 24, 6), TextWrapping = TextWrapping.Wrap, Text = ClaudeCapsText(LoadCaps()) };
        var main = new DockPanel { Background = GlassBg, Children = { Dk(head, Avalonia.Controls.Dock.Top), Dk(ui.ActiveKi, Avalonia.Controls.Dock.Top), Dk(ui.Caps, Avalonia.Controls.Dock.Top), Dk(bottom, Avalonia.Controls.Dock.Bottom), ui.Body } };
        Grid.SetColumn(main, 1);
        DragDrop.SetAllowDrop(main, true);
        main.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Text) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        main.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];
            if (paths.Count > 0) { ClaudeChatAttach(dock, paths); e.Handled = true; }
            else if (e.DataTransfer.TryGetText() is { Length: > 0 } dropped) { ui.Input.Text = ((ui.Input.Text ?? "").TrimEnd() + "\n„" + dropped.Trim() + "“\n").TrimStart(); e.Handled = true; } // Text aus einem Programm hierher gezogen
        });
        var side = new Border { Name = "claude-chat-side", Background = ClaudeBar, BorderBrush = ClaudeLine, BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(10), Child = new ScrollViewer { Content = ui.Side } };
        ui.PreviewCol = new ColumnDefinition(0, GridUnitType.Pixel);
        ui.SideCol = new ColumnDefinition(230, GridUnitType.Pixel);
        var cols = new ColumnDefinitions { ui.SideCol, new ColumnDefinition(1, GridUnitType.Star) { MinWidth = 380 }, ui.PreviewCol }; // das Gespräch behält immer genug Breite
        ui.Preview = new Border { Name = "claude-preview", Background = ClaudeBar, BorderBrush = ClaudeLine, BorderThickness = new Thickness(1, 0, 0, 0) };
        Grid.SetColumn(ui.Preview, 2);
        var grid = new Grid { ColumnDefinitions = cols, Children = { side, main, ui.Preview } };
        grid.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 1150;
            ui.SideToggle!.IsVisible = narrow;
            ui.SideCol.Width = new GridLength(!narrow || ui.SideOpen ? 230 : 0);
        };
        dock.ChatPane!.Child = grid;
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
            ToolTip.SetTip(open, "Fortsetzen (Doppelklick oder ✎: umbenennen)");
            // Wunsch 02.11.2026: Projekte umbenennen wie in der Claude-App – ✎, Doppelklick oder Rechtsklick
            void StartRename() { ui.Renaming = c.Id; ui.ConfirmDelete = null; RenderChatList(dock); }
            open.DoubleTapped += (_, e) => { e.Handled = true; StartRename(); };
            var ctx = new ContextMenu();
            var miRename = new MenuItem { Name = $"claude-chat-ctx-rename-{i}", Header = "✎ Umbenennen" }; miRename.Click += (_, _) => StartRename();
            var miDelete = new MenuItem { Name = $"claude-chat-ctx-del-{i}", Header = "✕ Löschen …" }; miDelete.Click += (_, _) => { ui.ConfirmDelete = c.Id; ui.Renaming = null; RenderChatList(dock); };
            ctx.Items.Add(miRename); ctx.Items.Add(miDelete); open.ContextMenu = ctx;
            var ren = new Button { Name = $"claude-chat-rename-{i}", Content = "✎", Padding = new Thickness(6, 2), Background = Brushes.Transparent, Foreground = ClaudeMuted, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(ren, "Projekt umbenennen"); ren.Click += (_, _) => StartRename();
            open.Click += (_, _) => { if (ui.Running) return; ui.ChatId = c.Id; ui.ConfirmDelete = null; if (c.Doc is not null) ClaudeEnsureProgram(dock, c); RenderChatList(dock); RenderChat(dock); }; // Projekt fortsetzen: sein Dokument im Programm
            var del = new Button { Name = $"claude-chat-del-{i}", Content = "✕", Padding = new Thickness(6, 2), Background = Brushes.Transparent, Foreground = ClaudeMuted, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(del, "Projekt löschen (Verlauf und Ordner – der Ordner kommt in den Papierkorb)");
            del.Click += (_, _) => { ui.ConfirmDelete = c.Id; RenderChatList(dock); };
            if (ui.Renaming == c.Id)
            {
                var box = new TextBox { Name = "claude-chat-rename-box", Text = c.Title, FontSize = 12.5, Margin = new Thickness(4, 2) };
                void Commit() { ClaudeChatRename(dock, c.Id, box.Text ?? ""); }
                box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; Commit(); } else if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; ui.Renaming = null; RenderChatList(dock); } };
                var ok = new Button { Name = "claude-chat-rename-ok", Content = "Speichern", Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 3), BorderThickness = new Thickness(0) };
                ok.Click += (_, _) => Commit();
                var cancel = new Button { Name = "claude-chat-rename-cancel", Content = "Abbrechen", Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 3), BorderThickness = new Thickness(0) };
                cancel.Click += (_, _) => { ui.Renaming = null; RenderChatList(dock); };
                ui.Side.Children.Add(new StackPanel { Spacing = 4, Margin = new Thickness(0, 2, 0, 6), Children = { box, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(4, 0), Children = { ok, cancel } } } });
                _ui.Post(() => { box.Focus(); box.SelectAll(); });
                continue;
            }
            ui.Side.Children.Add(new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), Dk(ren, Avalonia.Controls.Dock.Right), open } });
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
        ui.Input.IsEnabled = true; ui.Send.IsEnabled = true; // auch während Claude arbeitet: weiterschreiben
        ui.LastKi = chat?.Messages.LastOrDefault(m => m.Role == "assistant" && m.Ki is not null)?.Ki;
        ui.ActiveKi.Text = ActiveKiText(ui.LastKi);
        ui.ActiveKi.Foreground = CLimit.Active(DateTimeOffset.Now) ? ClaudeAccent : ClaudeMuted;
        var fullOn = chat is null ? ClaudeAlways && !ui.SafeForNew || ui.FullForNew : chat.FullWith(ClaudeAlways);
        if (ui.FullButton is { } fb) { fb.Content = fullOn ? "🔓 Internet & Befehle an" : "🔒 Sicher"; fb.Background = fullOn ? ClaudeAccent : ClaudeItem; }
        if (ui.AskFull) ui.Log.Children.Add(FullConsent(dock, chat));
        if (ui.Panel == "gedaechtnis") ui.Log.Children.Add(MemoryPanel(dock, chat));
        if (ui.Panel == "zeitreise") ui.Log.Children.Add(TimelinePanel(dock, chat));
        if (ui.Panel == "regeln") ui.Log.Children.Add(RulesPanel(dock));
        if (ui.Panel == "unterlagen") ui.Log.Children.Add(DocsPanel(dock, chat));
        if (ui.Panel == "veroeffentlichen") ui.Log.Children.Add(PublishPanel(dock, chat));
        if (ui.Panel == "github") ui.Log.Children.Add(GitHubPanel(dock, chat));
        if (ui.Panel == "netlify") ui.Log.Children.Add(NetlifyPanel(dock, chat));
        if (chat is null || chat.Messages.Count == 0)
            ui.Log.Children.Add(new StackPanel { Spacing = 8, Margin = new Thickness(0, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "✳", FontSize = 34, Foreground = ClaudeAccent, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Name = "claude-chat-welcome", Text = $"Was baust du heute in „{StudioCatalog.NameOf(dock.Studio ?? "", "Trendo")}“?", FontSize = 22, Foreground = ClaudeFg, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                new TextBlock { Text = "Claude bleibt hier beim Fachgebiet dieses Studios.", FontSize = 12.5, Foreground = ClaudeMuted, HorizontalAlignment = HorizontalAlignment.Center },
                IdeaChips(dock) } });
        else
        {
            if (ui.ShownChat != chat.Id) { ui.ShownChat = chat.Id; ui.Shown = chat.Messages.Count; } // beim Öffnen nicht alles einzeln einblenden
            for (var i = 0; i < chat.Messages.Count; i++)
            {
                var c = ChatMessage(dock, chat, chat.Messages[i], i == chat.Messages.Count - 1);
                ui.Log.Children.Add(i >= ui.Shown ? Appear(c, (i - ui.Shown) * 60) : c); // neue Nachrichten gleiten sanft herein
            }
            ui.Shown = chat.Messages.Count;
        }
        for (var i = 0; i < ui.Drafts.Count; i++) if (ui.Drafts[i].ChatId == chat?.Id) ui.Log.Children.Add(DraftCard(ui.Drafts[i], i)); // Entwürfe warten auf Freigabe
        if (ui.Plan is { } pl && pl.ChatId == chat?.Id) ui.Log.Children.Add(PlanCard(dock, ui, pl)); // wartet auf „Ausführen“
        AddLearnProposals(dock, ui); // gelernte Regeln warten auf Ja/Nein
        if (chat is not null && !ui.Running && chat.Messages.Count > 0 && chat.Messages[^1].Role == "assistant") AddDocSuggestions(dock, ui, chat); // passende Unterlagen? nur mit Ja
        AttachQueue(ui); RenderFlows(dock, ui);
        _ui.Post(() => ui.Scroll.ScrollToEnd(), DispatcherPriority.Background);
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
        if (m.Role == "review") return ReviewCard(dock, m, last);
        if (m.Role == "check")
        {
            var cp = new StackPanel { Spacing = 8, Children = { new SelectableTextBlock { Text = m.Text, Foreground = ClaudeFg, FontSize = 12.5, TextWrapping = TextWrapping.Wrap } } };
            // Bildschirmfoto, das Claude sich ansieht – der Nutzer sieht dasselbe
            if (m.Attachments?.FirstOrDefault() is { } shot && Path.Combine(chat.Folder, shot.Replace('/', Path.DirectorySeparatorChar)) is var sp0 && File.Exists(sp0))
                try { using var fs = File.OpenRead(sp0); cp.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, Child = new Image { Name = "claude-chat-shot", Source = new Avalonia.Media.Imaging.Bitmap(fs), MaxHeight = 220 } }); }
                catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException) { }
            return new Border { Name = "claude-chat-check", Background = Brush.Parse("#1AD97757"), BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Child = cp };
        }
        if (m.Role == "user")
        {
            var bubble = new StackPanel { Spacing = 4, Children = { new SelectableTextBlock { Text = m.Text, Foreground = ClaudeFg, FontSize = 14, TextWrapping = TextWrapping.Wrap } } };
            if (m.Attachments is { Count: > 0 } att) bubble.Children.Add(new TextBlock { Text = "📎 " + string.Join(", ", att.Select(Path.GetFileName)), Foreground = ClaudeMuted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            if (m.Context is { Length: > 0 } cx) bubble.Children.Add(new TextBlock { Text = "◎ " + cx, Foreground = ClaudeMuted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            if (m == chat.Messages.LastOrDefault(x => x.Role == "user") && FlowSaveButton(dock, m.Text) is { } fs) bubble.Children.Add(fs); // Wiederholungen automatisieren
            return new Border { Background = ClaudeItem, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 6 20 0 #40000000"), CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 10), HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 620, Child = bubble };
        }
        var sp = new StackPanel { Spacing = 6 };
        if (m.Steps.Count > 0)
        {
            // Zeitleiste der Arbeitsschritte mit Dauer (Wunsch 02.11.2026)
            var idx = chat.Messages.IndexOf(m);
            var began = chat.Messages.Take(Math.Max(idx, 0)).LastOrDefault(x => x.Role is "user" or "check")?.At;
            var took = began is { } b0 && m.At > b0 ? m.At - b0 : (TimeSpan?)null;
            sp.Children.Add(new Expander { Name = last ? "claude-chat-steps" : null, Header = $"{m.Steps.Count} Arbeitsschritt{(m.Steps.Count == 1 ? "" : "e")}" + (took is { } tk && tk.TotalSeconds >= 1 ? " · " + Duration(tk) : ""),
                Content = Timeline(m.Steps), IsExpanded = false, Foreground = ClaudeMuted, Background = Brushes.Transparent });
        }
        var (noSuggest, suggestions) = ClaudeChats.SplitSuggestions(ClaudeTools.Strip(m.Text));
        var (body, question, options) = ClaudeChats.SplitQuestion(noSuggest);
        foreach (var block in ChatBlocks(body)) sp.Children.Add(block);
        if (question is not null)
        {
            sp.Children.Add(new TextBlock { Name = last ? "claude-chat-question" : null, Text = question, Foreground = ClaudeFg, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            if (last)
            {
                var row = new WrapPanel();
                for (var i = 0; i < options.Count; i++)
                {
                    var o = options[i];
                    var b = new Button { Name = $"claude-chat-answer-{i}", Content = o, Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 6), Margin = new Thickness(0, 0, 6, 6), BorderThickness = new Thickness(1), BorderBrush = ClaudeAccent };
                    b.Click += (_, _) => _ = ClaudeChatSend(dock, o);
                    row.Children.Add(b);
                }
                sp.Children.Add(row);
            }
        }
        if (last && question is null && suggestions.Count > 0 && ClaudeSuggestsOn) sp.Children.Add(SuggestChips(dock, suggestions)); // Claude schlägt vor
        if (m.Used is { Count: > 0 } us) sp.Children.Add(new TextBlock { Name = last ? "claude-chat-used" : null, Text = "Genutzt: " + string.Join(" · ", us), Foreground = ClaudeMuted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        if (m.Ki is { Length: > 0 } ki) sp.Children.Add(new TextBlock { Name = last ? "claude-chat-ki" : null, Text = "Antwort von: " + ki, Foreground = ki == ClaudeKi ? ClaudeMuted : ClaudeAccent, FontSize = 10.5 });
        if (m.Tokens > 0) sp.Children.Add(new TextBlock { Name = last ? "claude-chat-usage" : null, Text = $"Abo-Nutzung dieser Antwort: ≈ {m.Tokens.ToString("N0", De)} Tokens", Foreground = ClaudeMuted, FontSize = 10.5 });
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

    /// <summary>Nachricht an Claude (öffentlich für Tests über das aktive Fenster). Ohne offenes Projekt legt die erste Nachricht eins an.</summary>
    public Task ClaudeChatSend(string text) => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? ClaudeChatSend(d, text) : Task.CompletedTask;
    public ClaudeChats.Chat? ClaudeChatCurrent() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) && _chatUis.TryGetValue(d, out var ui) && ui.ChatId is { } c ? CChats.Get(ChatStudio(d), c) : null;
    public bool ClaudeChatRunning() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) && _chatUis.TryGetValue(d, out var ui) && ui.Running;

    /// <param name="round">Durchgang beim echten Testen (0 = Nachricht vom Nutzer); <paramref name="shot"/>: Bildschirmfoto zur Prüfnachricht.</param>
    async Task ClaudeChatSend(ClaudeDock dock, string? text, bool check = false, int round = 0, string? shot = null)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.Running || string.IsNullOrWhiteSpace(text)) return;
        if (ClaudeNoClicks && _core.Approvals.Halted) { Toast("Der Not-Stopp ist aktiv. Claude startet erst nach „Fortsetzen“."); return; }
        text = text.Trim();
        var studio = ChatStudio(dock);
        var chats = CChats;
        var chat = ui.ChatId is { } cid ? chats.Get(studio, cid) : null;
        if (chat is null)
        {
            chat = chats.Create(studio, ClaudeChats.TitleOf(text), ClaudeChatRoot(dock.Studio), ClaudeStudioProjectFolder(dock));
            ui.ChatId = chat.Id; _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.neu", studio);
            if (ui.FullForNew) { chat = chats.SetFull(studio, chat.Id, true); ui.FullForNew = false; _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.voll.an", studio); }
            if (ui.SafeForNew) { chat = chats.SetFull(studio, chat.Id, false); ui.SafeForNew = false; }
        }
        // Claude arbeitet direkt im Programm des Studios: Dokument anlegen bzw. wieder öffnen, Programm zeigen (Wunsch 02.11.2026)
        if (!check) chat = ClaudeEnsureProgram(dock, chat);
        // Anhänge in den Ordner „anhang“ des Projekts; Claude liest sie von dort (Bilder kann es ansehen)
        var attached = new List<string>();
        foreach (var src in ui.Pending.Where(File.Exists))
            try
            {
                var dir = Path.Combine(chat.Folder, "anhang"); Directory.CreateDirectory(dir);
                var dst = Path.Combine(dir, Path.GetFileName(src)); for (var k = 2; File.Exists(dst); k++) dst = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(src)}-{k}{Path.GetExtension(src)}");
                File.Copy(src, dst); attached.Add("anhang/" + Path.GetFileName(dst));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        ui.Pending.Clear(); RenderChips(dock);
        var context = check ? null : ClaudeStudioContext(dock.Page);
        // Claude kennt das offene Programm: Seite lesen (nur wenn sie gerade offen ist) – Felder, Auswahl, Knöpfe; Claude kann es dann auch bedienen
        var pageState = !check && CurrentView == dock.Page ? ClaudeReadPage(dock.Page) : null;
        context ??= pageState is { } ps0 ? ps0.Program + (ps0.Title.Length > 0 ? " – " + ps0.Title : "") + (ps0.Selected is { Length: > 0 } sl0 ? $"; markiert: „{(sl0.Length > 80 ? sl0[..80] + " …" : sl0)}“" : "") : null;
        var prompt = text + (attached.Count > 0 ? "\n\n(Angehängt: " + string.Join(", ", attached) + ")" : "") + (context is not null ? "\n\n[Gerade im Studio: " + context + "]" : "") + (pageState is not null ? ClaudeActions.Prompt(pageState) : "");
        if (shot is not null) attached.Add(shot);
        chat = chats.Add(studio, chat.Id, new ClaudeChats.Msg(check ? "check" : "user", text, [], DateTimeOffset.Now, null, 0, attached.Count > 0 ? attached : null, context));
        ui.Input.Text = ""; ui.Running = true; ui.Cts = new CancellationTokenSource();
        ui.Send.Content = "■"; ToolTip.SetTip(ui.Send, "Anhalten");
        RenderChatList(dock); RenderChat(dock);

        // Live-Antwort
        var live = new StackPanel { Name = "claude-chat-live", Spacing = 6 };
        var stepsPanel = new StackPanel { Spacing = 2 }; var textPanel = new StackPanel { Spacing = 6 };
        var limited = CLimit.Active(DateTimeOffset.Now);
        var thinking = new TextBlock { Text = limited ? "🌐 Die Ersatz-KI denkt nach …" : "✳ Claude denkt nach …", Foreground = ClaudeAccent, FontSize = 13 };
        using var pulse = new CancellationTokenSource(); Pulse(thinking, pulse.Token); // lebendig: pulsiert, solange Claude arbeitet
        var skillChip = new Border { Name = "claude-chat-skill", IsVisible = false, Background = Brush.Parse("#26D97757"), BorderBrush = ClaudeAccent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Foreground = ClaudeFg, FontSize = 12 } };
        live.Children.Add(skillChip); live.Children.Add(stepsPanel); live.Children.Add(textPanel); live.Children.Add(thinking);
        var used = new List<string>();
        ui.Log.Children.Add(Appear(live)); AttachQueue(ui);
        var steps = new List<ClaudeChats.Step>(); var say = new System.Text.StringBuilder(); string? error = null; string? session = null; var ok = true; var tokens = 0; string? limitRaw = null; var ki = ClaudeKi;

        var folder = chat.Folder; Directory.CreateDirectory(folder);
        if (dock.Studio is not null) try { Skills.ExportForClaude(folder, null, dock.Studio); ClaudeTeamExport(folder, dock.Studio); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        // Studio-Dokument: ein offenes Fachprogramm-Dokument (.tdoc) liegt als Datei im Projekt – Claude bearbeitet es, Trendo übernimmt jede Änderung live (als ein Rückgängig-Schritt)
        var shell = Programs.ContainsKey(dock.Page) && ProgramActive(dock.Page) is not null;
        string? workFile = null;
        if (shell && ProgramActive(dock.Page) is { } doc)
            try { workFile = Regex.Replace(doc.Title, @"[\\/:*?""<>|]", "-") + ProgramDocument.Extension; File.WriteAllText(Path.Combine(folder, workFile), doc.Serialize()); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { workFile = null; }
        // Brief-Studio: der offene Brief ist das Dokument (Text UND Design) – Claude arbeitet darin, Trendo übernimmt jede Änderung live ins Brief-Programm
        var letterDoc = !shell && dock.Studio == LetterStudio;
        if (letterDoc)
        {
            if (CurrentView != "brief") ClaudeMoveToPage(dock, "brief");
            try { workFile = LetterWorkFile; File.WriteAllText(Path.Combine(folder, workFile), LetterToJson(Letter)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { workFile = null; }
        }
        var format = letterDoc && workFile is not null ? new StudioFormat(LetterDesigns.Rule, null, []) : StudioFormat.For(dock.Studio, dock.Page, shell);
        // Claude arbeitet IM Programm (Wunsch 02.11.2026, bei allen Studios): das Studio-Dokument ist das Projekt – eigene Dateien sind höchstens Ergänzung
        var programInfo = shell && workFile is not null && Programs.TryGetValue(dock.Page, out var pspec)
            ? $"\nWICHTIG – du arbeitest im Programm „{pspec.Title}“ von Trendo ({pspec.Sentence}). Das Projekt IST das Studio-Dokument „{workFile}“ im Projektordner: " +
              "Trage dein Ergebnis immer ZUERST dort ein – in die vorhandenen Felder unter \"data\" (" + string.Join(", ", ProgramActive(dock.Page)!.Data.Select(kv => kv.Key).Take(25)) + "), Aufbau und Feldnamen beibehalten, Listen ergänzen. " +
              "Trendo zeigt jede Änderung sofort im Programm. Ergänzende Dateien (z. B. ein spielbarer Prototyp) nur zusätzlich und erst danach. " +
              "Befehle des Programms im Menüband: " + string.Join(", ", pspec.Ribbon.SelectMany(g => g.Item2).Select(c => c.Label).Take(30)) + " – auslösen kannst du sie über die Knöpfe der Seite."
            : "";
        FolderSnapshot? snap = null;
        var point = CTimeline.NewStore(chat.Id); // Versionspunkt für die Zeitreise
        try { snap = FolderSnapshot.Take(folder, point); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var system = ClaudeChats.System(StudioCatalog.NameOf(dock.Studio ?? "", "Trendo"), ClaudeRoleFor(dock.Studio) + ClaudeTeamRule(dock.Studio), Skills.For(null, dock.Studio), format, workFile) + programInfo + LiveReplay.Rule + ClaudeStudioRules(dock, folder, chat.FullWith(ClaudeAlways)) + ClaudeDocsFor(chat.Id, folder) + CMemory.Prompt(chat.Id) + (ClaudeSuggestsOn ? ClaudeChats.SuggestRule : "");
        string? pendingFile = null;
        var model = KiSourceSettings.Load(_core.DataRoot).ClaudeModel;
        void OnLine(string l)
        {
            if (l.Contains("\"trendo_error\"")) { error = System.Text.Json.Nodes.JsonNode.Parse(l)?["text"]?.ToString(); return; }
            foreach (var ev in ClaudeCode.Parse(l, folder))
                _ui.Post(() =>
                {
                    switch (ev)
                    {
                        case ClaudeCode.Init i:
                            _mcpConnected = i.Connectors ?? [];
                            if (i.Session.Length > 0) session = i.Session;
                            if (i.Skills is not null || i.Connectors is not null || i.Plugins is not null) { SaveCaps(i); ui.Caps.Text = ClaudeCapsText(i); }
                            break;
                        case ClaudeCode.Say sy: say.AppendLine(sy.Text).AppendLine(); textPanel.Children.Clear(); foreach (var b in ChatBlocks(say.ToString())) textPanel.Children.Add(b); break;
                        case ClaudeCode.Tool t:
                        {
                            var icon = t.Badge switch { "Skill" => "🧩 ", "Agent" => "🤖 ", "Connector" => "🔌 ", "Befehl" => "⌨ ", "Internet" => "🌐 ", _ => "" };
                            var st = new ClaudeChats.Step(icon + t.Text, t.Changes); steps.Add(st); stepsPanel.Children.Add(TimelineRow(st, steps[0].At, true)); thinking.Text = "✳ " + t.Text + " …";
                            if (t.Badge is "Skill" or "Agent" or "Connector" && t.Detail is { } det)
                            {
                                var u = $"{icon}{t.Badge} {det}"; if (!used.Contains(u)) used.Add(u);
                                if (t.Badge == "Skill") { skillChip.IsVisible = true; ((TextBlock)skillChip.Child!).Text = $"🧩 Aktiver Skill: {det}"; }
                            }
                            if (t.Changes && t.File is { } tf) pendingFile = tf;
                            break;
                        }
                        case ClaudeCode.ToolDone td when td.Error: var se = new ClaudeChats.Step("Fehler: " + td.Text, false, true); steps.Add(se); stepsPanel.Children.Add(TimelineRow(se, steps[0].At, true)); pendingFile = null; break;
                        case ClaudeCode.ToolDone when pendingFile is { } pf: pendingFile = null; ClaudeLiveChanged(dock, Path.Combine(folder, pf), workFile, null); break; // live bei jedem Schritt
                        case ClaudeCode.Todo todo: thinking.Text = "✳ " + (todo.Items.FirstOrDefault(x => x.State == "in_progress").Text ?? "arbeitet") + " …"; break;
                        case ClaudeCode.Done dn: ok = dn.Ok; tokens = dn.Tokens; if (dn.Session.Length > 0) session = dn.Session; if (ClaudeCode.IsLimit(dn.Text)) { limitRaw = dn.Text; error = ClaudeCode.LimitText; } else if (say.Length == 0 && dn.Text.Length > 0) say.Append(dn.Text); break;
                    }
                    ui.Scroll.ScrollToEnd();
                });
        }
        var bridged = false;
        using var watch = limited ? null : ClaudeWatch(dock, folder, () => workFile); // jede Änderung im Projektordner live im Programm – egal, wie Claude sie macht
        if (!limited)
        {
            try
            {
                var mcpFile = chat.FullWith(ClaudeAlways) ? ClaudeStudioMcp(folder, dock.Studio) : null; // C5 (08.11.2026): Blender/Unreal/Godot direkt aus Claude
                await using var bridge = await ClaudeBridgeOpen(dock, folder, chat.Id); // Claude bedient die Programme selbst
                bridged = bridge is not null;
                await Task.Run(() => ClaudeRun(ClaudeWithMcp(ClaudeCode.Args(prompt, ClaudeCode.Phase.Chat, chat.Session, bridge is null ? system : system + TrendoMcp.HandsFreeRule, model is { Length: > 0 } ? model : null, chat.FullWith(ClaudeAlways) ? ClaudeCode.Level.Voll : ClaudeCode.Level.Sicher), mcpFile, bridge?.ConfigFile), folder, OnLine, ui.Cts.Token));
            }
            catch (OperationCanceledException) { error = "Angehalten."; }
            catch (InvalidOperationException e) { error = e.Message; }
            await _ui.InvokeAsync(() => { }, DispatcherPriority.Background); // ausstehende Ereignisse abarbeiten
            if (pendingFile is { } lastPf) ClaudeLiveChanged(dock, Path.Combine(folder, lastPf), workFile, null);
            if (limitRaw is null && ClaudeCode.IsLimit(error)) limitRaw = error;
            if (limitRaw is not null)
            {
                // Kontingent aufgebraucht: merken, bis wann – bis dahin übernimmt die Ersatz-KI (diese Nachricht gleich mit)
                var until = ClaudeLimit.ResetOf(limitRaw, DateTimeOffset.Now); CLimit.Set(until); limited = true; error = null; say.Clear();
                steps.Add(new ClaudeChats.Step($"Claude-Abo aufgebraucht – die Ersatz-KI übernimmt, Claude wieder ab {until.LocalDateTime:HH:mm} Uhr", false));
                _core.Audit.Append(Principal.LocalOwner.Name, "claude.kontingent.leer", until.ToString("O"));
            }
            else if (error is null) CLimit.Clear();
        }
        if (limited && error is null)
        {
            thinking.Text = "🌐 Die Ersatz-KI arbeitet …";
            var fb = await ClaudeFallbackRun(prompt, system, folder, ui.Cts.Token);
            if (fb.Error is not null) error = fb.Error;
            else
            {
                ki = fb.Ki; say.Append(fb.Text);
                foreach (var c in fb.Changes) steps.Add(new ClaudeChats.Step($"schreibt {c.Path} (+{c.Added} −{c.Removed})", true));
                _core.Audit.Append(Principal.LocalOwner.Name, "claude.ersatz", ki);
                if (!check) CStages.Add(chat.Id, studio, text); // Etappe: sobald Claude wieder frei ist, macht es hier weiter
            }
        }
        // bestellte KI-Werkzeuge (Bild, Musik, …) kostenlos auf dem PC ausführen – die Dateien zählen zu den Änderungen dieser Antwort
        if (error is null && ClaudeTools.Parse(say.ToString()).Count > 0) { thinking.Text = "🎨 Trendo erzeugt Bilder, Musik … (auf diesem PC)"; steps.AddRange(await ClaudeRunTools(dock, chat.Id, say.ToString(), folder)); }
        var changes = snap?.Changes() ?? [];
        var changed = changes.Where(c => !c.File.Replace('\\', '/').StartsWith(".versionen/", StringComparison.Ordinal)).Select(c => $"{c.File} ({c.Change})").ToList(); // Versionsablage ist kein Ergebnis
        var answer = say.ToString().Trim();
        var autoPlan = false;
        // Projekt-Gedächtnis: „Merken: …“-Zeilen übernehmen und als Arbeitsschritt zeigen
        // Claude bedient das Programm: Schritte erst als Plan, Ausführen nur nach Freigabe
        if (error is null && pageState is not null && ClaudeActions.Parse(answer, pageState) is { Count: > 0 } psteps)
        {
            ui.Plan = new ClaudePlan(dock.Page, pageState, psteps, chat.Id);
            autoPlan = bridged && ClaudeNoClicks && ClaudePlanSafe(dock.Page, pageState, psteps); // nur Claude, die Ersatz-KI bleibt beim Plan mit Knopf
        }
        var finished = TrendoMcp.IsFinished(answer); answer = TrendoMcp.StripFinish(answer);
        // selbstlernend: „Lernregel: …“ wird ein Vorschlag (gilt erst nach Ja)
        foreach (var lr in ClaudeLearnFrom(studio, answer, false, check, check ? null : text, chat.Title)) steps.Add(new ClaudeChats.Step("🎓 Regel-Vorschlag: " + lr.Text, false));
        answer = StudioLearning.Strip(answer);
        foreach (var keep in ClaudeMemory.Extract(answer)) if (CMemory.Add(chat.Id, keep)) steps.Add(new ClaudeChats.Step("🧠 Gemerkt: " + keep, false));
        answer = ClaudeMemory.Strip(answer);
        if (snap is not null) try { CTimeline.Keep(chat.Id, point, check ? "Nachbesserung: " + text.Split('\n')[0] : text.Split('\n')[0], changes.Count); } catch (IOException) { }
        if (ClaudeCode.IsLimit(error)) error = ClaudeCode.LimitText;
        if (error is not null) answer = (answer.Length > 0 ? answer + "\n\n" : "") + "⚠ " + error;
        else if (!ok && answer.Length == 0) answer = "⚠ Claude hat die Antwort nicht fertigbekommen.";
        if (session is { Length: > 0 }) chats.SetSession(studio, chat.Id, session);
        chats.Add(studio, chat.Id, new ClaudeChats.Msg("assistant", answer, steps, DateTimeOffset.Now, changed.Count > 0 ? changed : null, tokens, null, null, error is null ? ki : null, used.Count > 0 ? used : null));
        ui.LastSnapshot = changed.Count > 0 ? snap : null; ui.LastSnapshotChat = chat.Id;
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat", $"{studio} · {changed.Count} Änderungen");
        pulse.Cancel();
        ui.Running = false; ui.Cts = null; ui.Send.Content = "↑"; ToolTip.SetTip(ui.Send, "Senden (Enter) · neue Zeile mit Umschalt+Enter");
        RenderChatList(dock); RenderChat(dock);
        if (autoPlan) await ClaudeRunPlan(dock);
        await ClaudeAfterRun(dock, folder, format, workFile, snap, changes);
        // Echt testen, bis es läuft: Trendo prüft mit den Fachregeln, startet das Ergebnis (Browser unsichtbar, Syntaxprüfung) und zeigt Claude ein Bildschirmfoto –
        // Befunde gehen automatisch zurück an Claude, höchstens bis Durchgang ClaudeProbe.Rounds
        var rounds = ClaudeRounds;
        if (bridged && error is null && !finished && round + 1 < rounds && !_core.Approvals.Halted)
        {
            _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.weiter", $"Durchgang {round + 2}");
            await ClaudeChatSend(dock, TrendoMcp.ContinuePrompt, check: true, round: round + 1);
        }
        else if (round + 1 < rounds && error is null && changes.Count > 0 && !_core.Approvals.Halted)
        {
            var alive = changes.Where(c => c.Change != "gelöscht").Select(c => c.File).ToList();
            var found = TrendoAgent.Check(folder, alive).Where(c => !c.Ok).Select(c => c.Text).ToList();
            // am Programm vorbei gearbeitet? Dann zurück ins Studio-Dokument
            if (workFile is not null && !changes.Any(c => Path.GetFileName(c.File).Equals(workFile, StringComparison.OrdinalIgnoreCase)))
                found.Insert(0, $"Du hast nicht im Programm gearbeitet: Das Studio-Dokument „{workFile}“ ist unverändert. Trage dein Ergebnis jetzt dort ein ({(workFile == LetterWorkFile ? "Text und – falls gewünscht – Design in den Feldern des Briefs" : "unter \"data\", vorhandene Felder und Aufbau beibehalten")}) – das ist das Projekt, das der Nutzer im Programm sieht.");
            thinking.Text = "🧪 Trendo startet das Ergebnis und prüft es …";
            var probe = ClaudeLooks || found.Count > 0 ? await ClaudeProbeRun(folder, alive) : new ClaudeProbe.Result([], null);
            var look = round == 0 && ClaudeLooks && ki == ClaudeKi; // Bildschirmfoto ansehen kann nur Claude
            if (ClaudeProbe.Feedback(found, probe, look) is { } fb)
            {
                _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.pruefung", $"{found.Count + probe.Errors.Count} Punkte · Durchgang {round + 1}");
                await ClaudeChatSend(dock, fb, check: true, round: round + 1, shot: probe.Screenshot is not null ? ClaudeProbe.Shot : null);
            }
        }
        if (round == 0) ClaudeSoundAfter(dock, ui, chat.Id); // Ton: fertig, Rückfrage/Plan oder Fehler
        if (round == 0 && ui.Queue.Count > 0) { var next = ui.Queue.Dequeue(); RenderQueue(dock); await ClaudeChatSend(dock, next); } // wartende Nachricht gleich danach
    }

    /// <summary>Vorgabe „Claude darf bei Bedarf ins Internet und Befehle ausführen“ (KI-Einstellungen, Standard an).</summary>
    bool ClaudeAlways => KiSourceSettings.Load(_core.DataRoot).ClaudeAlwaysFull;

    /// <summary>Freigabe „Volle Funktionen“ für das Projekt – mit klarer Erklärung, was Claude dann darf; wieder abschaltbar.</summary>
    Control FullConsent(ClaudeDock dock, ClaudeChats.Chat? chat)
    {
        var ui = _chatUis[dock];
        var on = chat is null ? ClaudeAlways && !ui.SafeForNew || ui.FullForNew : chat.FullWith(ClaudeAlways);
        var sp = new StackPanel { Name = "claude-chat-full-ask", Spacing = 8 };
        sp.Children.Add(new TextBlock { Text = on ? "Claude darf in diesem Projekt bei Bedarf ins Internet und Befehle ausführen." : "Volle Claude-Funktionen für dieses Projekt freigeben?", Foreground = ClaudeFg, FontSize = 14, FontWeight = FontWeight.SemiBold });
        sp.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = ClaudeMuted, FontSize = 12.5, Text =
            "Dann darf Claude hier wie in Claude Code selbst: Befehle ausführen (z. B. Spiel starten, App bauen, Tests), ins Internet, deine eigenen Skills, Agenten, Mods (Plugins) und MCP-Werkzeuge nutzen – " +
            "auch Computersteuerung, wenn du sie in Claude Code eingerichtet hast. Was riskant ist, entscheidet Claude Code mit seinen eigenen Sicherheitsregeln; Trendo protokolliert die Freigabe. Ohne Freigabe arbeitet Claude nur im Projektordner." });
        var row = new WrapPanel();
        Button B(string name, string label, bool primary, Action a)
        {
            var b = new Button { Name = name, Content = label, Background = primary ? ClaudeAccent : ClaudeItem, Foreground = primary ? Brushes.White : ClaudeFg, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 6, 0), BorderThickness = new Thickness(0) };
            b.Click += (_, _) => a(); row.Children.Add(b); return b;
        }
        void Set(bool v)
        {
            ui.AskFull = false;
            if (ui.ChatId is { } id) { CChats.SetFull(ChatStudio(dock), id, v); _core.Audit.Append(Principal.LocalOwner.Name, v ? "claude.chat.voll.an" : "claude.chat.voll.aus", ChatStudio(dock)); }
            else { ui.FullForNew = v; ui.SafeForNew = !v; }
            RenderChat(dock);
        }
        if (on) B("claude-chat-full-off", "Zurück auf sicher", true, () => Set(false)); else B("claude-chat-full-yes", "Freigeben", true, () => Set(true));
        B("claude-chat-full-no", "Abbrechen", false, () => { ui.AskFull = false; RenderChat(dock); });
        sp.Children.Add(row);
        return new Border { Background = Brush.Parse("#14D97757"), BorderBrush = ClaudeAccent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10), Child = sp };
    }

    /// <summary>Offizielle Claude-App öffnen (Cowork, Claude Design …); ohne installierte App die Download-Seite.</summary>
    public void ClaudeOpenApp()
    {
        var app = ClaudeAppProbe is { } probe ? probe() : FindClaudeApp();
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.app", app is null ? "nicht installiert" : "öffnen");
        _claudeMsg = app is not null ? "Die Claude-App wird geöffnet." : "Die Claude-App ist auf diesem PC nicht installiert – die offizielle Download-Seite öffnet sich.";
        if (_opt.Isolated) { _claudeMsg = "Testmodus: " + _claudeMsg + " (nichts gestartet)"; return; }
        Launch(app ?? ClaudeDownload);
    }

    public const string ClaudeDownload = "https://claude.ai/download";
    /// <summary>Für Tests: statt auf dem PC zu suchen (Rückgabe: was geöffnet würde, null = nicht installiert).</summary>
    public static Func<string?>? ClaudeAppProbe { get; set; }

    /// <summary>Ist die offizielle Claude-App installiert? Klassische Installation (Programmdatei) oder App-Paket aus dem Store bzw. dem neuen Installer –
    /// beide melden „claude://“ bei Windows an; darüber öffnet Windows die App. Liefert, was zu öffnen ist, sonst null. Liest nur, ändert nichts.</summary>
    static string? FindClaudeApp()
    {
        var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (new[] { Path.Combine(la, "AnthropicClaude", "claude.exe"), Path.Combine(la, "Programs", "Claude", "Claude.exe") }.FirstOrDefault(File.Exists) is { } exe) return exe;
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"claude\shell\open\command");
            if (key?.GetValue(null) is string cmd && cmd.Contains("Claude.exe", StringComparison.OrdinalIgnoreCase)) return "claude://";
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return null;
    }

    /// <summary>Was Claude aus deinem eigenen Claude mitbringt (vom letzten Lauf gemerkt, je Person): Skills, Connectoren, Plugins, Agenten.</summary>
    string CapsFile => Path.Combine(_core.DataRoot, "claude-chats", "faehigkeiten.json");
    ClaudeCode.Init? LoadCaps() { try { return File.Exists(CapsFile) ? System.Text.Json.JsonSerializer.Deserialize<ClaudeCode.Init>(File.ReadAllText(CapsFile)) : null; } catch (Exception e) when (e is System.Text.Json.JsonException or IOException or NotSupportedException) { return null; } }
    void SaveCaps(ClaudeCode.Init i) { try { Directory.CreateDirectory(Path.GetDirectoryName(CapsFile)!); File.WriteAllText(CapsFile, System.Text.Json.JsonSerializer.Serialize(i)); } catch (IOException) { } }
    static string ClaudeCapsText(ClaudeCode.Init? i)
    {
        if (i is null) return "Aus deinem Claude: deine Skills, Plugins (Mods), Agenten und Connectoren stehen hier nach der ersten Antwort.";
        static string L(IReadOnlyList<string>? l, int n = 4) => l is { Count: > 0 } ? string.Join(", ", l.Take(n)) + (l.Count > n ? $" +{l.Count - n}" : "") : "–";
        return $"Aus deinem Claude: 🧩 {i.Skills?.Count ?? 0} Skills · 🔌 Connectoren: {L(i.Connectors)} · Plugins: {L(i.Plugins)} · 🤖 {i.Agents?.Count ?? 0} Agenten";
    }

    /// <summary>Zeile „Aktive KI“ oben im Chat: Claude oder die Ersatz-KI, mit Uhrzeit, ab wann Claude wieder übernimmt.</summary>
    string ActiveKiText(string? lastKi)
    {
        var l = CLimit;
        if (l.Active(DateTimeOffset.Now) && l.Until is { } u)
            return $"Aktive KI: {(lastKi is { } k && k != ClaudeKi ? k : "🌐 kostenlose Ersatz-KI")} – dein Claude-Abo ist gerade aufgebraucht, Claude übernimmt wieder ab {u.LocalDateTime:HH:mm} Uhr.";
        return "Aktive KI: " + ClaudeKi;
    }

    /// <summary>Eingabe abschicken – arbeitet Claude noch, kommt die Nachricht in die Warteschlange und gleich danach dran (öffentlich für Tests über das aktive Fenster).</summary>
    public void ClaudeChatSubmit(string text) { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) && _chatUis.TryGetValue(d, out var ui)) { ui.Input.Text = text; ClaudeChatSubmit(d); } }
    public int ClaudeChatQueued() => ActiveChatUi()?.Queue.Count ?? 0;
    void ClaudeChatSubmit(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || string.IsNullOrWhiteSpace(ui.Input.Text)) return;
        if (!ui.Running) { _ = ClaudeChatSend(dock, ui.Input.Text); return; }
        ui.Queue.Enqueue(ui.Input.Text.Trim()); ui.Input.Text = "";
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.wartet", ChatStudio(dock));
        RenderQueue(dock);
    }

    void AttachQueue(ChatUi ui) { (ui.QueuePanel.Parent as Panel)?.Children.Remove(ui.QueuePanel); ui.Log.Children.Add(ui.QueuePanel); }

    /// <summary>Wartende Nachrichten als Blasen: „wartet“, mit „Stopp & jetzt“ (Claude hält an und nimmt sie sofort auf).</summary>
    void RenderQueue(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui)) return;
        ui.QueuePanel.Children.Clear();
        var i = 0;
        foreach (var q in ui.Queue)
        {
            var now = new Button { Name = $"claude-chat-queue-now-{i++}", Content = "⏹ Stopp & jetzt", FontSize = 11.5, Padding = new Thickness(8, 2), Background = Brushes.Transparent, Foreground = ClaudeAccent, BorderThickness = new Thickness(1), BorderBrush = ClaudeAccent, CornerRadius = new CornerRadius(8) };
            ToolTip.SetTip(now, "Claude hält jetzt an und arbeitet gleich mit dieser Nachricht weiter – bisher Gemachtes bleibt");
            now.Click += (_, _) => ui.Cts?.Cancel();
            ui.QueuePanel.Children.Add(new Border { Background = ClaudeItem, BorderBrush = ClaudeLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 8), HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 620,
                Child = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = q, Foreground = ClaudeFg, FontSize = 14, TextWrapping = TextWrapping.Wrap },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = "⏳ wartet – Claude nimmt es gleich danach auf", Foreground = ClaudeMuted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, now } } } } });
        }
        ui.Scroll.ScrollToEnd();
    }

    /// <summary>Startideen passend zum Studio – anklicken schickt sie gleich ab.</summary>
    Control IdeaChips(ClaudeDock dock)
    {
        var row = new WrapPanel { Name = "claude-chat-ideas", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0), MaxWidth = 640 };
        var ui = _chatUis[dock];
        var used = CChats.All(ChatStudio(dock)).Select(c => c.Messages.FirstOrDefault()?.Text ?? c.Title).ToHashSet();
        var ideas = StudioIdeas.Pick(ClaudeChats.Ideas(dock.Studio, dock.Page), DateOnly.FromDateTime(DateTime.Today), used, ui.IdeaPage); // jeden Tag andere, genutzte zuletzt
        for (var i = 0; i < ideas.Count; i++)
        {
            var idea = ideas[i];
            var b = new Button { Name = $"claude-chat-idea-{i}", Content = idea, Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 7), Margin = new Thickness(0, 0, 8, 8), BorderThickness = new Thickness(0), FontSize = 12.5 };
            b.Click += (_, _) => _ = ClaudeChatSend(dock, idea);
            row.Children.Add(b);
        }
        var more = new Button { Name = "claude-chat-ideas-more", Content = "🔄 Andere Ideen", Background = Brushes.Transparent, Foreground = ClaudeMuted, CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 7), Margin = new Thickness(0, 0, 8, 8), BorderThickness = new Thickness(1), BorderBrush = ClaudeLine, FontSize = 12.5 };
        more.Click += (_, _) => { ui.IdeaPage++; RenderChat(dock); };
        row.Children.Add(more);
        return row;
    }

    /// <summary>Dateien anhängen (öffentlich für Tests über das aktive Fenster).</summary>
    public void ClaudeChatAttach(IEnumerable<string> files) { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) ClaudeChatAttach(d, files); }
    void ClaudeChatAttach(ClaudeDock dock, IEnumerable<string> files)
    {
        if (!_chatUis.TryGetValue(dock, out var ui)) return;
        foreach (var f in files) if (File.Exists(f) && !ui.Pending.Contains(f)) ui.Pending.Add(f);
        RenderChips(dock);
    }

    async Task ClaudeChatAttachPick(ClaudeDock dock)
    {
        string[]? got;
        if (PickOverride is { } fake) got = await fake(new PickRequest("claude-chat-attach", false, true, []));
        else got = (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Für Claude anhängen", AllowMultiple = true })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is { Length: > 0 }) ClaudeChatAttach(dock, got);
    }

    void RenderChips(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui)) return;
        ui.Chips.Children.Clear();
        for (var i = 0; i < ui.Pending.Count; i++)
        {
            var f = ui.Pending[i];
            var x = new Button { Name = $"claude-chat-attachment-del-{i}", Content = "✕", Padding = new Thickness(4, 0), Background = Brushes.Transparent, Foreground = ClaudeMuted, BorderThickness = new Thickness(0) };
            x.Click += (_, _) => { ui.Pending.Remove(f); RenderChips(dock); };
            ui.Chips.Children.Add(new Border { Background = ClaudeItem, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3, 4, 3), Margin = new Thickness(0, 0, 6, 4),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new TextBlock { Text = "📎 " + Path.GetFileName(f), Foreground = ClaudeFg, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, x } } });
        }
    }

    /// <summary>Projektordner aus dem Studio: Ist in „Webseite bauen“ eine Seite bzw. im Spiele-Studio ein Spiel offen, arbeitet Claude genau daran; sonst null (neuer Ordner).</summary>
    string? ClaudeStudioProjectFolder(ClaudeDock dock) => dock.Page switch
    {
        "dev" when _webDir is { } wd && Directory.Exists(wd) => wd,
        "spiele" when (_gameEdit ?? _gameClaude) is { } gd && Directory.Exists(gd) => gd,
        _ => null,
    };

    /// <summary>Was Claude gebaut hat, links im passenden Programm des Studios zeigen – so, als hätte man es selbst gemacht. Gibt true zurück, wenn das Studio etwas zeigt.</summary>
    async Task<bool> ClaudeShowInStudio(ClaudeDock dock, string folder, IReadOnlyList<string> files)
    {
        string? F(params string[] ext) => files.Select(f => Path.Combine(folder, f.Replace('/', Path.DirectorySeparatorChar))).FirstOrDefault(f => ext.Contains(Path.GetExtension(f).ToLowerInvariant()) && File.Exists(f));
        var index = File.Exists(Path.Combine(folder, "index.html")) ? Path.Combine(folder, "index.html") : F(".html", ".htm");
        switch (dock.Page)
        {
            case "dev" when index is not null:
                _webDir = folder; _webEditFile = index; _webPreview = null;
                if (!_opt.Isolated) await RefreshPreview();
                return true;
            case "spiele" or "spiele-programm" when index is not null:
                _gameClaude = folder; _gameClaudeShot = null;
                if (!_opt.Isolated) try { var shot = await WebsitePreview.CaptureAsync(folder, 520, 520, Path.Combine(folder, ".vorschau", "spiel.png")); _gameClaudeShot = shot.Ok ? shot.Path : null; } catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                return true;
            case "3d" when F(".gltf", ".glb", ".obj", ".stl") is { } model:
                Import3DFile(model); return true;
            case "editor" when F(".js", ".ts", ".py", ".cs", ".css", ".html", ".json", ".md", ".txt") is { } code:
                EditorOpen(code); return true;
        }
        return false;
    }

    /// <summary>Karte im Spiele-Studio: das Spiel, das Claude gerade gebaut hat – Vorschaubild, Spielen, Ordner.</summary>
    Control ClaudeGameCard(string dir)
    {
        var sp = new StackPanel { Name = "game-claude", Spacing = 6, Children = { H("✳ Von Claude: „" + Path.GetFileName(dir) + "“", 14) } };
        if (_gameClaudeShot is { } shot && File.Exists(shot)) try { sp.Children.Add(new Image { Source = new Avalonia.Media.Imaging.Bitmap(shot), MaxHeight = 260, HorizontalAlignment = HorizontalAlignment.Left }); } catch (Exception e) when (e is IOException or ArgumentException) { }
        var html = Path.Combine(dir, "index.html");
        sp.Children.Add(new WrapPanel { Children = { Act("game-claude-play", "Spielen", () => Launch(html), true), new Border { Width = 6 }, Act("game-claude-folder", "Ordner", () => Launch(dir)) } });
        return Card(sp);
    }

    /// <summary>Datei während der Antwort geändert: Studio-Dokument sofort übernehmen, Vorschau sofort neu zeigen.</summary>
    void ClaudeLiveChanged(ClaudeDock dock, string file, string? workFile, string? original)
    {
        if (!File.Exists(file)) return;
        var isWork = workFile is not null && Path.GetFileName(file).Equals(workFile, StringComparison.OrdinalIgnoreCase);
        if (isWork) { if (workFile == LetterWorkFile) ClaudeSyncLetter(file); else ClaudeSyncProgram(dock, file); }
        if (!isWork && StudioFormat.PreviewKind(file) != "none") ShowPreview(dock, file, original); // das Studio-Dokument zeigt das Programm selbst
    }

    /// <summary>Studio-Dokument aus der Datei übernehmen, die Claude bearbeitet hat (ein Rückgängig-Schritt im Studio).</summary>
    void ClaudeSyncProgram(ClaudeDock dock, string file)
    {
        if (ProgramActive(dock.Page) is not { } doc) return;
        try
        {
            var fresh = ProgramDocument.Load(file);
            if (fresh.Data.ToJsonString() == LiveTarget(dock.Page, doc)) return; // schon übernommen (oder wird gerade live eingespielt)
            ClaudeApplyLive(dock, doc, fresh.Data); // live – viele neue Teile erscheinen nacheinander
        }
        catch (InvalidDataException e) { _claudeMsg = "Claudes Änderung ließ sich nicht übernehmen: " + e.Message; }
        if (!dock.Max) Show(CurrentView);
    }

    /// <summary>Nach der Antwort: Hauptdatei in der Vorschau (mit vorher/nachher), neue Texte/Tabellen gleich im passenden Programm öffnen.</summary>
    async Task ClaudeAfterRun(ClaudeDock dock, string folder, StudioFormat? format, string? workFile, FolderSnapshot? snap, IReadOnlyList<(string File, string Change)> changes)
    {
        var alive = changes.Where(c => c.Change != "gelöscht").Select(c => c.File).ToList();
        var shown = alive.Count > 0 && await ClaudeShowInStudio(dock, folder, alive);
        shown |= workFile is not null && alive.Any(f => Path.GetFileName(f).Equals(workFile, StringComparison.OrdinalIgnoreCase));
        // Studio-Dokument abgleichen – auch wenn Claude es ohne Schreibwerkzeug geändert hat (z. B. per Befehl); gleiche Daten ändern nichts
        if (workFile is not null && alive.Any(f => Path.GetFileName(f).Equals(workFile, StringComparison.OrdinalIgnoreCase)))
        { if (workFile == LetterWorkFile) ClaudeSyncLetter(Path.Combine(folder, workFile)); else ClaudeSyncProgram(dock, Path.Combine(folder, workFile)); }
        if (StudioFormat.PickPreview(format, alive.Where(f => workFile is null || !Path.GetFileName(f).Equals(workFile, StringComparison.OrdinalIgnoreCase))) is { } rel)
        {
            var orig = snap is not null && changes.Any(c => c.File == rel && c.Change == "geändert") ? Path.Combine(snap.Store, rel.Replace('/', Path.DirectorySeparatorChar)) : null;
            ShowPreview(dock, Path.Combine(folder, rel.Replace('/', Path.DirectorySeparatorChar)), orig);
        }
        foreach (var rf in alive)
        {
            var f = Path.Combine(folder, rf.Replace('/', Path.DirectorySeparatorChar)); var ext = Path.GetExtension(f).ToLowerInvariant();
            if (workFile is not null && Path.GetFileName(f).Equals(workFile, StringComparison.OrdinalIgnoreCase)) continue;
            if (dock.Page == "text" && ext is ".md" or ".txt")
            {
                if (TextDirty) { _claudeMsg = $"„{Path.GetFileName(f)}“ ist fertig – dein offener Text ist noch nicht gespeichert, darum öffnet Trendo es nicht von selbst."; continue; }
                TextOpen(f); _claudeMsg = $"„{Path.GetFileName(f)}“ ist in Trendo Text geöffnet."; shown = true; break;
            }
            if (dock.Page == "tabelle" && ext == ".csv")
            {
                if (SheetDirty) { _claudeMsg = $"„{Path.GetFileName(f)}“ ist fertig – deine offene Tabelle ist noch nicht gespeichert, darum öffnet Trendo sie nicht von selbst."; continue; }
                SheetOpen(f); _claudeMsg = $"„{Path.GetFileName(f)}“ ist in Trendo Tabelle geöffnet."; shown = true; break;
            }
        }
        if (shown && dock.Max) ClaudeDockSetMax(dock, false); // Studio links einblenden: dort steht jetzt, was Claude gebaut hat
        if (shown) foreach (var b in dock.Panel.GetLogicalDescendantsOf<Button>().Where(b => b.Name == "claude-dock-max")) b.Content = "⤢ Groß";
        if (shown && CurrentView == dock.Page) Show(CurrentView);
        RenderPreview(dock);
    }

    /// <summary>Vorschau zeigen (öffentlich lesbar für Tests): Webseiten, Spiele, Grafiken in der Browser-Ansicht, sonst als Text; mit „vorher“, wenn es ein Original gibt.</summary>
    void ShowPreview(ClaudeDock dock, string? file, string? original = null)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || file is null || !File.Exists(file)) return;
        if (ui.PreviewFile != file || original is not null) { ui.PreviewOriginal = original; ui.Compare = "nachher"; }
        ui.PreviewFile = file;
        if (ui.PreviewCol.Width.Value == 0) ui.PreviewCol.Width = new GridLength(1, GridUnitType.Star);
        RenderPreview(dock);
    }

    public string? ClaudeChatPreviewFile() => ActiveChatUi()?.PreviewFile;
    public string? ClaudeChatPreviewSide() => ActiveChatUi()?.Compare;
    public void ClaudeChatCompare(string side) { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) && _chatUis.TryGetValue(d, out var ui) && ui.PreviewOriginal is not null) { ui.Compare = side; RenderPreview(d); } }
    ChatUi? ActiveChatUi() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) && _chatUis.TryGetValue(d, out var ui) ? ui : null;

    void RenderPreview(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.PreviewFile is not { } file) return;
        var shown = ui.Compare == "vorher" && ui.PreviewOriginal is { } o && File.Exists(o) ? o : file;
        var bar = new WrapPanel { Margin = new Thickness(10, 8) };
        void B(string name, string label, string tip, Action a, bool on = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 4), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0),
                Background = on ? ClaudeAccent : ClaudeItem, Foreground = on ? Brushes.White : ClaudeFg };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => a(); bar.Children.Add(b);
        }
        bar.Children.Add(new TextBlock { Name = "claude-preview-name", Text = Path.GetFileName(file), Foreground = ClaudeFg, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 4) });
        if (ui.PreviewOriginal is not null)
        {
            B("claude-preview-before", "Vorher", "So war es vor Claudes letzter Antwort", () => { ui.Compare = "vorher"; RenderPreview(dock); }, ui.Compare == "vorher");
            B("claude-preview-after", "Nachher", "So ist es jetzt", () => { ui.Compare = "nachher"; RenderPreview(dock); }, ui.Compare == "nachher");
            if (ui.LastSnapshot is not null)
            {
                if (!ClaudeNoClicks) B("claude-preview-keep", "✓ Behalten", "Änderung behalten", () => { ui.PreviewOriginal = null; ui.Compare = "nachher"; ui.LastSnapshot = null; RenderPreview(dock); RenderChat(dock); });
                B("claude-preview-undo", "↶ Zurücknehmen", "Änderungen der letzten Antwort rückgängig machen", () => { ClaudeChatUndo(dock); ui.PreviewOriginal = null; ui.Compare = "nachher"; RenderPreview(dock); });
            }
        }
        B("claude-preview-reload", "↻", "Neu laden", () => RenderPreview(dock));
        B("claude-preview-close", "✕", "Vorschau schließen", () => ui.PreviewCol.Width = new GridLength(0));
        Control view;
        if (StudioFormat.PreviewKind(shown) == "web")
        {
            if (_opt.Isolated) view = new Border { Name = "claude-preview-web", Child = new TextBlock { Text = "Testmodus: Vorschau von " + Path.GetFileName(shown) + " wird nicht geladen.", Foreground = ClaudeMuted, Margin = new Thickness(12) } };
            else
            {
                ui.PreviewWeb ??= new NativeWebView { Name = "claude-preview-web" };
                ui.PreviewWeb.Navigate(new Uri(new Uri(shown).AbsoluteUri + "?t=" + DateTime.Now.Ticks)); // neu laden, nicht aus dem Zwischenspeicher
                if (ui.PreviewWeb.Parent is Decorator dp) dp.Child = null; else if (ui.PreviewWeb.Parent is Panel pp) pp.Children.Remove(ui.PreviewWeb);
                view = ui.PreviewWeb;
            }
        }
        else
        {
            string text; try { text = File.ReadAllText(shown); } catch (IOException e) { text = e.Message; }
            if (text.Length > 30000) text = text[..30000] + "\n…";
            view = new ScrollViewer { Content = new SelectableTextBlock { Name = "claude-preview-text", Text = text, FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"), FontSize = 12.5, Foreground = ClaudeFg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) } };
        }
        var note = _claudeMsg.Length > 0 && (_claudeMsg.Contains("geöffnet") || _claudeMsg.Contains("noch nicht gespeichert")) ? new TextBlock { Name = "claude-preview-note", Text = _claudeMsg, Foreground = ClaudeAccent, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 0, 10, 6) } : null;
        var dpn = new DockPanel { Children = { Dk(bar, Avalonia.Controls.Dock.Top) } };
        if (note is not null) dpn.Children.Add(Dk(note, Avalonia.Controls.Dock.Top));
        dpn.Children.Add(view);
        ui.Preview.Child = dpn;
    }

    /// <summary>Änderungen der letzten Antwort zurücknehmen (öffentlich für Tests über das aktive Fenster).</summary>
    public void ClaudeChatUndo() { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) ClaudeChatUndo(d); }
    void ClaudeChatUndo(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.LastSnapshot is not { } snap || ui.LastSnapshotChat is not { } cid) return;
        snap.Restore(); ui.LastSnapshot = null;
        if (dock.Studio == LetterStudio && File.Exists(Path.Combine(snap.Root, LetterWorkFile))) ClaudeSyncLetter(Path.Combine(snap.Root, LetterWorkFile)); // Brief im Programm zurück
        CChats.Add(ChatStudio(dock), cid, new ClaudeChats.Msg("assistant", "↶ Die Änderungen der letzten Antwort sind zurückgenommen.", [], DateTimeOffset.Now));
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.zurueck", dock.Studio ?? "");
        RenderChat(dock);
    }

    /// <summary>Projekt löschen: Verlauf weg, Ordner in den Papierkorb (öffentlich für Tests über das aktive Fenster).</summary>
    /// <summary>Projekt umbenennen (nur der Name – Verlauf und Ordner bleiben). Öffentlich für Tests über das aktive Fenster.</summary>
    public void ClaudeChatRename(string id, string title) { if (Windows.Active?.Id is { } w && _claudeDocks.TryGetValue(w, out var d)) ClaudeChatRename(d, id, title); }
    void ClaudeChatRename(ClaudeDock dock, string id, string title)
    {
        if (!_chatUis.TryGetValue(dock, out var ui)) return;
        if (title.Trim().Length == 0) { ui.Renaming = null; RenderChatList(dock); return; } // leerer Name: alter Name bleibt
        var t = title.Trim(); if (t.Length > 80) t = t[..80];
        CChats.Rename(ChatStudio(dock), id, t);
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.projekt.umbenennen", ChatStudio(dock));
        ui.Renaming = null; RenderChatList(dock); if (ui.ChatId == id) RenderChat(dock);
    }

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
            CMemory.Delete(c.Id); CDocs.Delete(c.Id); CStages.Done(c.Id);
            var snapDir = Path.Combine(_core.DataRoot, "claude-chats", "sicherung", c.Id);
            if (Directory.Exists(snapDir)) Directory.Delete(snapDir, true); // eigene Sicherungskopie von Trendo, keine Nutzerdaten
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _claudeMsg = "Ordner ließ sich nicht verschieben: " + e.Message; }
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.chat.geloescht", c.Title);
        if (ui.ChatId == id) { ui.ChatId = CChats.All(ChatStudio(dock)).FirstOrDefault()?.Id; if (ui.LastSnapshotChat == id) ui.LastSnapshot = null; }
        RenderChatList(dock); RenderChat(dock);
    }
}
