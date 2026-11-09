using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0610: Spieldesign im Programm „Spiele bauen“. Ideen finden (mit Einstellungen oder geführt nur mit Knöpfen), die Spielbibel als Dokument aus Abschnitten
/// und ein Chat je Abschnitt, dessen Vorschläge erst nach dem Übernehmen wirken.</summary>
public partial class MainWindow
{
    const string GcAny = "egal";
    string _gcMode = ""; int _gcStep; bool _gcBusy; string _gcNote = "";
    readonly Dictionary<string, string> _gcAnswers = []; readonly HashSet<string> _gcLiked = [], _gcDisliked = [];
    readonly Dictionary<string, string> _gcImages = [], _gcImageNotes = [];
    GameConcept.Options _gcOpts = new(); List<GameConcept.Idea> _gcIdeas = []; int _gcSeed = Environment.TickCount;
    readonly HashSet<string> _gcSeen = []; bool _gameStale;
    (string Doc, string Section)? _gdChat; bool _gdBusy; (ProgramDocument Doc, string Id, string Text)? _gdPending;

    /// <summary>Aktuelle Ideen (für Tests).</summary>
    public IReadOnlyList<GameConcept.Idea> GameIdeas => _gcIdeas;

    /// <summary>Einmalige Frage an die KI: mit Claude, wenn angemeldet, sonst Internet-KI, dann KI auf dem PC.</summary>
    async Task<(string Text, string Ki)> GameAsk(string system, string prompt)
    {
        if (GamePromptOverride is { } o) return (await o(system, prompt), "Test-KI");
        var useClaude = GamePromptClaude;
        var all = DefaultKiBackends().Where(b => b is not IMainKiBackend || (useClaude && b is ClaudeKiBackend)).OrderBy(b => b is ClaudeKiBackend ? 0 : b is IRemoteKiBackend ? 1 : 2).ToList();
        var a = await new IntelligenceCore(_core.DataRoot, _core.Audit, () => all).AskAsync(new KiRequest(prompt, system, UseKnowledge: false, AllowRemote: true));
        if (!a.Ok) throw new InvalidOperationException("Keine KI hat geantwortet: " + a.Text + " – richte auf der KI-Seite eine kostenlose KI ein oder melde Claude an.");
        return (a.Text, KiLabel(a, all));
    }

    /// <summary>Nach einer KI-Antwort neu zeichnen, ohne etwas zu stören: Wer woanders arbeitet, sieht das Ergebnis beim Zurückkommen; ein gerade bearbeitetes Feld behält Text, Fokus und Schreibmarke.</summary>
    void GameRefresh()
    {
        if (CurrentView != GK) { _gameStale = true; return; }
        _gameStale = false;
        var focused = FocusManager?.GetFocusedElement() is TextBox { Name: { } n } box && n.StartsWith("game-", StringComparison.Ordinal) ? box : null; // Namen der Spiele-Seite gibt es in keinem anderen Fenster
        var (name, text, caret) = (focused?.Name, focused?.Text, focused?.CaretIndex ?? 0);
        Show(GK);
        if (name is not null && this.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == name) is { } again) { again.Text = text; again.CaretIndex = caret; Avalonia.Threading.Dispatcher.UIThread.Post(() => again.Focus(), Avalonia.Threading.DispatcherPriority.Background); } // erst nach dem Neuzeichnen, sonst geht der Fokus wieder verloren
    }

    // ───────────── Ideen ─────────────

    public async Task GameIdeasRun(GameConcept.Options options)
    {
        if (_gcBusy) return;
        if (options != _gcOpts) _gcSeen.Clear();
        foreach (var i in _gcIdeas) _gcSeen.Add(i.Theme);
        _gcBusy = true; _gcOpts = options; _gcNote = "Ideen werden gesucht …"; Show(GK);
        _core.Audit.Append(Principal.LocalOwner.Name, "game.ideen", options.Genre);
        try
        {
            List<GameConcept.Idea> ideas = []; var source = "";
            try
            {
                var (text, ki) = await GameAsk(GameConcept.Instructions(), GameConcept.Prompt(options, _gcLiked, _gcDisliked, _gcIdeas.Select(i => i.Title)));
                ideas = GameConcept.Parse(text, options, _gcDisliked, ++_gcSeed); source = "erstellt von " + ki;
            }
            catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException) { }
            if (ideas.Count < 3) { ideas = GameConcept.Local(options, ++_gcSeed, _gcLiked, _gcDisliked, _gcSeen); source = "ohne KI aus dem Ideen-Baukasten – mit einer eingerichteten KI werden die Ideen freier"; }
            _gcIdeas = ideas; _gcImages.Clear(); _gcImageNotes.Clear(); _gcNote = $"{ideas.Count} Ideen, {source}.";
        }
        finally { _gcBusy = false; GameRefresh(); }
    }

    void GameIdeaRate(GameConcept.Idea idea, bool like)
    {
        var (add, other) = like ? (_gcLiked, _gcDisliked) : (_gcDisliked, _gcLiked);
        if (!add.Add(idea.Theme)) add.Remove(idea.Theme);
        other.Remove(idea.Theme);
        Show(GK);
    }

    async Task GameIdeaImage(GameConcept.Idea idea)
    {
        var folder = GameMediaFolder(ProgramActive(GK)); Directory.CreateDirectory(folder);
        var file = $"medien/konzept-{DateTime.Now:yyyyMMdd-HHmmss}-{Math.Abs(idea.Title.GetHashCode()) % 1000}.png";
        _gcImageNotes[idea.Title] = "Bild wird erzeugt …"; Show(GK);
        var (ok, msg) = await KiToolRun("bild", GameConcept.ImagePrompt(idea), folder, file);
        if (ok) { _gcImages[idea.Title] = Path.GetFullPath(Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar))); _gcImageNotes.Remove(idea.Title); }
        else _gcImageNotes[idea.Title] = "Kein Konzeptbild: " + msg;
        GameRefresh();
    }

    public void GameIdeaUse(GameConcept.Idea idea)
    {
        var d = ProgramNew(GK, "leer");
        var bible = GameConcept.ToBible(idea);
        d.Edit("Aus Idee: " + idea.Title, x =>
        {
            foreach (var (k, v) in bible) x[k] = v?.DeepClone();
            if (_gcImages.TryGetValue(idea.Title, out var image)) GameDocument.AddImage(x, "idee", image);
        });
        _core.Audit.Append(Principal.LocalOwner.Name, "game.idee.uebernommen", idea.Title);
        _progMsg[GK] = $"„{idea.Title}“ ist angelegt – jetzt die Spielbibel ausbauen. Rückgängig mit Strg+Z.";
        _gcIdeas = []; _gcMode = ""; _gcNote = ""; _gameSection = "Spielbibel"; _gameShowAll = true;
        Show(GK);
    }

    void GamePathChoose(GameConcept.PathStep step, string value)
    {
        _gcAnswers[step.Id] = value;
        if (_gcStep + 1 < GameConcept.Path.Length) { _gcStep++; Show(GK); return; }
        _gcMode = ""; _gcStep = 0;
        _ = GameIdeasRun(GameConcept.PathOptions(_gcAnswers));
    }

    static ComboBox GameChoice(string name, string[] values, string current) =>
        new() { Name = name, ItemsSource = new[] { GcAny }.Concat(values).ToList(), SelectedItem = current.Length > 0 ? current : GcAny, MinWidth = 150, Margin = new Thickness(0, 0, 8, 6) };

    static string GameChosen(ComboBox c) => c.SelectedItem is string s && s != GcAny ? s : "";

    Control GameIdeasCard()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(new TextBlock { Text = "💡 Noch keine Idee?", FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Text });
        var open = new WrapPanel { Children = {
            Act("game-idea-form", "Ideen nach meinen Wünschen", () => { _gcMode = _gcMode == "form" ? "" : "form"; Show(GK); }),
            new Border { Width = 8 },
            Act("game-path-start", "Führ mich zu einer Idee", () => { _gcMode = "weg"; _gcStep = 0; _gcAnswers.Clear(); Show(GK); }, true) } };
        sp.Children.Add(open);
        if (_gcMode == "form")
        {
            var keywords = new TextBox { Name = "game-idea-keywords", Text = _gcOpts.Keywords, PlaceholderText = "Stichworte, zum Beispiel: Zeitreise, Garten, Roboter" };
            var genre = GameChoice("game-idea-genre", GameDirector.Genres, _gcOpts.Genre);
            var platform = GameChoice("game-idea-platform", GameConcept.Platforms, _gcOpts.Platform);
            var style = GameChoice("game-idea-style", GameConcept.Styles, _gcOpts.Style);
            var perspective = GameChoice("game-idea-perspective", GameConcept.Perspectives, _gcOpts.Perspective);
            var run = Act("game-idea-run", _gcBusy ? "Sucht …" : "Ideen finden", () => GameIdeasRun(new GameConcept.Options(keywords.Text ?? "", GameChosen(genre), GameChosen(platform), GameChosen(style), GameChosen(perspective))), true);
            run.IsEnabled = !_gcBusy;
            sp.Children.Add(keywords);
            sp.Children.Add(new WrapPanel { Children = { genre, platform, style, perspective } });
            sp.Children.Add(run);
        }
        if (_gcMode == "weg")
        {
            var step = GameConcept.Path[_gcStep];
            var row = new WrapPanel { Name = "game-path" };
            for (var i = 0; i < step.Options.Length; i++)
            {
                var value = step.Options[i].Value;
                var b = Act($"game-path-{_gcStep}-{i}", step.Options[i].Label, () => GamePathChoose(step, value)); b.Margin = new Thickness(0, 0, 8, 6);
                row.Children.Add(b);
            }
            sp.Children.Add(P($"Schritt {_gcStep + 1} von {GameConcept.Path.Length}: {step.Question}", Text, 13));
            sp.Children.Add(row);
        }
        if (_gcNote.Length > 0) sp.Children.Add(P(_gcNote, Muted, 12));
        for (var i = 0; i < _gcIdeas.Count; i++) sp.Children.Add(GameIdeaCard(_gcIdeas[i], i));
        if (_gcIdeas.Count > 0)
        {
            var more = Act("game-idea-more", _gcBusy ? "Sucht …" : "↻ Neue Ideen nach meinen Bewertungen", () => GameIdeasRun(_gcOpts));
            more.IsEnabled = !_gcBusy;
            sp.Children.Add(more);
        }
        return new Border { Name = "game-ideas-card", Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 10), CornerRadius = new CornerRadius(16), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = sp };
    }

    Control GameIdeaCard(GameConcept.Idea idea, int i)
    {
        var liked = _gcLiked.Contains(idea.Theme); var disliked = _gcDisliked.Contains(idea.Theme);
        var sp = new StackPanel { Spacing = 5, Children = {
            H(idea.Title, 14),
            P(idea.Pitch, Text, 12.5),
            P(idea.Loop, Muted, 12),
            P($"{idea.Genre} · {idea.Style} · {idea.Perspective} · {idea.Platform} · {GameDesign.ScaleOf(idea.Scale).Name}", Muted, 11.5) } };
        if (_gcImages.TryGetValue(idea.Title, out var image)) sp.Children.Add(GameThumb(image, $"game-idea-{i}-picture", 260));
        if (_gcImageNotes.TryGetValue(idea.Title, out var note)) sp.Children.Add(new TextBlock { Name = $"game-idea-{i}-note", Text = note, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new WrapPanel { Children = {
            Act($"game-idea-{i}-use", "✓ Daraus Spiel machen", () => GameIdeaUse(idea), true), new Border { Width = 6 },
            Act($"game-idea-{i}-like", liked ? "👍 Gefällt" : "👍", () => GameIdeaRate(idea, true)), new Border { Width = 6 },
            Act($"game-idea-{i}-dislike", disliked ? "👎 Gefällt nicht" : "👎", () => GameIdeaRate(idea, false)), new Border { Width = 6 },
            Act($"game-idea-{i}-image", "🖼 Konzeptbild", () => GameIdeaImage(idea)) } });
        return new Border { Name = $"game-idea-{i}", Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Background = Brush.Parse(C("#14FFFFFF", "#08000000")), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = sp };
    }

    Control GameThumb(string file, string name, double width)
    {
        if (!GameDocument.IsLocalImage(file)) return new TextBlock { Name = name, Text = "Bild nicht geöffnet (nur lokale PNG- und JPG-Dateien): " + Path.GetFileName(file), Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
        try
        {
            using var fs = File.OpenRead(file);
            return new Image { Name = name, Source = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(fs, (int)width * 2), Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NullReferenceException or UnauthorizedAccessException)
        {
            return new TextBlock { Name = name, Text = "Bild nicht lesbar: " + Path.GetFileName(file), Foreground = Muted, FontSize = 11.5 };
        }
    }

    // ───────────── Spielbibel als Dokument ─────────────

    private Control GameBible(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 8 };
        void Field(string key, string label)
        {
            var t = new TextBox { Name = "game-f-" + key, Text = d.Text(key), PlaceholderText = label, TextWrapping = TextWrapping.Wrap };
            void Commit() { if ((t.Text ?? "") != d.Text(key)) { d.Set(key, t.Text ?? "", label); Show(GK); } }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(P(label, null, 11.5)); sp.Children.Add(t);
        }
        Field("genre", "Genre"); Field("audience", "Zielgruppe"); Field("platforms", "Plattformen");
        var sections = GameDocument.Sections(d.Data);
        for (var i = 0; i < sections.Count; i++) sp.Children.Add(GameDocCard(d, sections[i], i, sections.Count));
        var title = new TextBox { Name = "game-doc-new-title", PlaceholderText = "Neuer Abschnitt, zum Beispiel: Wirtschaft im Spiel", Width = 320 };
        sp.Children.Add(new WrapPanel { Children = { title, new Border { Width = 8 }, Act("game-doc-add", "＋ Abschnitt hinzufügen", () => ProgramRun(GK, () => d.Edit("Abschnitt hinzufügen", x => GameDocument.AddCustom(x, title.Text ?? "")))) } });
        var vault = GameDocument.Vault(d.Data);
        if (vault.Count > 0) sp.Children.Add(GameDocVault(d, vault, sections));
        return sp;
    }

    Control GameDocCard(ProgramDocument d, GameDocument.Section s, int index, int count)
    {
        var id = s.Id; var pre = "game-doc-" + id; var chatOpen = _gdChat is { } open && open.Doc == d.Id && open.Section == id;
        Button Small(string suffix, string label, Action a, bool enabled = true) { var b = Act(pre + suffix, label, a); b.FontSize = 11.5; b.Padding = new Thickness(8, 3); b.Margin = new Thickness(0, 0, 4, 0); b.IsEnabled = enabled; return b; }
        Control heading;
        if (s.Custom)
        {
            var title = new TextBox { Name = pre + "-title", Text = s.Title, FontSize = 14, Width = 260 };
            void CommitTitle() { if ((title.Text ?? "").Trim() is { Length: > 0 } v && v != s.Title) { d.Edit("Abschnitt benennen", x => GameDocument.SetTitle(x, id, v)); Show(GK); } }
            title.LostFocus += (_, _) => CommitTitle(); OnEnter(title, CommitTitle);
            heading = title;
        }
        else heading = H(s.Title, 14);
        heading.Margin = new Thickness(0, 0, 12, 0);
        var head = new WrapPanel { Children = { heading,
            Small("-up", "▲", () => ProgramRun(GK, () => d.Edit("Abschnitt verschieben", x => GameDocument.Move(x, id, -1))), index > 0),
            Small("-down", "▼", () => ProgramRun(GK, () => d.Edit("Abschnitt verschieben", x => GameDocument.Move(x, id, 1))), index < count - 1),
            Act(pre + "-img", "🖼 Bild", () => GameDocPickImage(d, id)),
            new Border { Width = 4 },
            Small("-chat", chatOpen ? "💬 Chat schließen" : "💬 Mit KI besprechen", () => { _gdChat = chatOpen ? null : (d.Id, id); _gdPending = null; Show(GK); }) } };
        if (s.Custom) head.Children.Add(Small("-del", "Entfernen", () => ProgramRun(GK, () => { d.Edit("Abschnitt entfernen", x => GameDocument.RemoveCustom(x, id)); GameDocument.ClearChat(d, id); })));
        var sp = new StackPanel { Spacing = 6, Children = { head } };
        if (s.Editable)
        {
            var multi = s.Kind == GameDocument.SectionKind.Text;
            var t = new TextBox { Name = s.Custom ? pre + "-text" : "game-f-" + s.Key, Text = GameDocument.Text(d.Data, s), PlaceholderText = s.Kind == GameDocument.SectionKind.Lines ? "Mit Komma getrennt" : s.Title, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 56 : 0 };
            void Commit() { if ((t.Text ?? "") != GameDocument.Text(d.Data, s)) { d.Edit(s.Title, x => GameDocument.SetText(x, id, t.Text ?? "")); Show(GK); } }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(t);
        }
        else
        {
            var body = GameDocument.Text(d.Data, s);
            sp.Children.Add(P(body.Length > 0 ? body : "Noch leer.", body.Length > 0 ? Text : Muted, 12));
            var target = s.Kind switch { GameDocument.SectionKind.Characters => "Figuren", GameDocument.SectionKind.World => "Welt", GameDocument.SectionKind.Quests => "Missionen", _ => "Produktion" };
            sp.Children.Add(Small("-goto", "Bearbeiten unter „" + target + "“", () => { _gameSection = target; Show(GK); }));
        }
        var images = GameDocument.Images(d.Data, id);
        if (images.Count > 0)
        {
            var strip = new WrapPanel { Name = pre + "-images" };
            for (var i = 0; i < images.Count; i++)
            {
                var file = images[i];
                strip.Children.Add(new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 8, 6), Children = {
                    GameThumb(file, $"{pre}-image-{i}", 150),
                    Small($"-image-{i}-vault", "In die Ablage", () => ProgramRun(GK, () => d.Edit("Bild in die Ablage", x => GameDocument.ToVault(x, id, file)))) } });
            }
            sp.Children.Add(strip);
        }
        if (chatOpen) sp.Children.Add(GameDocChat(d, s));
        return new Border { Name = pre, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12), Background = Brush.Parse(C("#14FFFFFF", "#08000000")), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = sp };
    }

    async Task GameDocPickImage(ProgramDocument d, string id)
    {
        var got = PickOverride is { } fake ? await fake(new PickRequest($"game-doc-{id}-img", false, false, ["png", "jpg", "jpeg"]))
            : (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Bild für den Abschnitt", FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }] }))
                .Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToArray();
        if (got is { Length: > 0 }) ProgramRun(GK, () => d.Edit("Bild einfügen", x => GameDocument.AddImage(x, id, got[0])));
    }

    Control GameDocVault(ProgramDocument d, List<string> vault, List<GameDocument.Section> sections)
    {
        var sp = new StackPanel { Name = "game-doc-vault", Spacing = 6, Children = { H("Ablage", 14), P("Diese Bilder sind in keinem Abschnitt und kommen nicht ins PDF.", Muted, 12) } };
        for (var i = 0; i < vault.Count; i++)
        {
            var file = vault[i];
            var to = new ComboBox { Name = $"game-doc-vault-{i}-to", ItemsSource = sections.Select(s => s.Title).ToList(), PlaceholderText = "In Abschnitt verschieben", MinWidth = 200 };
            to.SelectionChanged += (_, _) =>
            {
                if (to.SelectedIndex < 0) return;
                var target = sections[to.SelectedIndex].Id;
                ProgramRun(GK, () => d.Edit("Bild aus der Ablage", x => GameDocument.FromVault(x, target, file)));
            };
            sp.Children.Add(new WrapPanel { Children = { GameThumb(file, $"game-doc-vault-{i}-image", 120), new Border { Width = 8 }, to } });
        }
        return new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = sp };
    }

    // ───────────── Chat je Abschnitt ─────────────

    public async Task GameDocChatSend(string id, string question)
    {
        if (_gdBusy || string.IsNullOrWhiteSpace(question) || ProgramActive(GK) is not { } d || GameDocument.Find(d.Data, id) is not { } s) return;
        var history = GameDocument.Chat(d, id);
        GameDocument.AddChat(d, id, "du", question.Trim());
        _gdBusy = true; _gdPending = null; Show(GK);
        _core.Audit.Append(Principal.LocalOwner.Name, "game.abschnitt.chat", id);
        try
        {
            var (answer, ki) = await GameAsk(GameDocument.ChatInstructions(s), GameDocument.ChatPrompt(d.Data, s, question, history));
            var reply = GameDocument.ParseReply(answer, s);
            GameDocument.AddChat(d, id, ki, reply.Text.Length > 0 ? reply.Text : "Hier ist mein Vorschlag.");
            if (reply.Suggestion is { } text) _gdPending = (d, id, text);
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException) { GameDocument.AddChat(d, id, "Hinweis", e.Message); }
        finally { _gdBusy = false; GameRefresh(); }
    }

    /// <summary>Vorschlag übernehmen: ein Schritt, rückgängig machbar, nur dieser Abschnitt.</summary>
    public void GameDocAccept()
    {
        if (_gdPending is not { } p || !ReferenceEquals(ProgramActive(GK), p.Doc) || GameDocument.Find(p.Doc.Data, p.Id) is not { } s) return;
        var d = p.Doc;
        ProgramRun(GK, () => { d.Edit("KI-Vorschlag: " + s.Title, x => GameDocument.Apply(x, s, p.Text)); _gdPending = null; _progMsg[GK] = $"Vorschlag für „{s.Title}“ übernommen. Rückgängig mit Strg+Z."; });
    }

    Control GameDocChat(ProgramDocument d, GameDocument.Section s)
    {
        var id = s.Id;
        var sp = new StackPanel { Name = "game-doc-chat", Spacing = 5 };
        foreach (var l in GameDocument.Chat(d, id)) sp.Children.Add(P($"{(l.Role == "du" ? "Du" : l.Role)}: {l.Text}", l.Role == "du" ? Text : Muted, 12));
        if (_gdPending is { } p && ReferenceEquals(p.Doc, d) && p.Id == id)
        {
            var preview = new StackPanel { Name = "game-doc-preview", Spacing = 2, Children = { P("Vorschlag (noch nicht übernommen):", Text, 12.5) } };
            foreach (var line in GameDocument.Preview(d.Data, s, p.Text).Take(14)) preview.Children.Add(P(line, Muted, 12));
            sp.Children.Add(preview);
            sp.Children.Add(new WrapPanel { Children = { Act("game-doc-accept", "✓ Übernehmen", GameDocAccept, true), new Border { Width = 8 }, Act("game-doc-discard", "✕ Verwerfen", () => { _gdPending = null; Show(GK); }) } });
        }
        var input = new TextBox { Name = "game-doc-chat-input", PlaceholderText = "Frage oder Wunsch zu diesem Abschnitt", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44 };
        var send = Act("game-doc-chat-send", _gdBusy ? "KI arbeitet …" : "Senden", () => GameDocChatSend(id, input.Text ?? ""), true);
        var suggest = Act("game-doc-chat-suggest", "💡 Vorschlag machen", () => GameDocChatSend(id, "Mache einen konkreten Verbesserungsvorschlag für diesen Abschnitt."));
        send.IsEnabled = suggest.IsEnabled = !_gdBusy;
        sp.Children.Add(input);
        sp.Children.Add(new WrapPanel { Children = { send, new Border { Width = 8 }, suggest } });
        return sp;
    }
}
