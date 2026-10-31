using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Arch;

namespace Trendo.App;

/// <summary>SPEC-06 / T-0262: Lernen & Training als echtes Programm „Karteikarten“ (Vorbild Anki, nur Funktionsumfang):
/// Lernen mit großer Karte (Leertaste dreht, 1–4 bewertet, SM-2), Eintippen mit Tippfehler-Toleranz, Auswahl-Quiz, Kartenverwaltung als Tabelle,
/// Statistik mit 7-Tage-Vorschau, Problemkarten und Prüfungsplan. Daten bleiben in der bisherigen Lernbox. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    private LearnBox? _learn;
    public LearnBox Learn => _learn ??= new LearnBox(_core.DataRoot);
    private bool _reveal;
    public string LearnMode = "lernen";
    public string? LearnDeck;
    private string _learnSearch = "", _learnMsg = "", _quizSeedKey = "";
    private (Flashcard Card, IReadOnlyList<string> Options, int Correct)? _quiz;
    private string? _typedCard; private string _typedResult = "";

    private void InitSpec06() => RegisterTab(FachTabs, "lernen", "Karteikarten", "SPEC-06", LearnView, "lernen", "karteikarten", "vokabeln", "schule", "prüfung", "training", "quiz", "anki");

    /// <summary>Antwort der aktuellen Karte aufdecken (Knopf, Klick auf die Karte, Leertaste).</summary>
    public void LearnReveal() { _reveal = true; Show("lernen"); }

    /// <summary>Bewertung der aufgedeckten Karte (Knopf oder Taste 1–4).</summary>
    public void LearnGrade(Grade g)
    {
        if (Learn.DueCards(LearnDeck).FirstOrDefault() is not { } c || !_reveal) return;
        var next = Learn.Answer(c.Id, g); _reveal = false;
        _learnMsg = g == Grade.Falsch ? "Kommt morgen wieder." : $"Nächstes Mal in {next.IntervalDays} Tag{(next.IntervalDays == 1 ? "" : "en")}.";
        Show("lernen");
    }

    private Control LearnView()
    {
        // Kopf: Modus und Stapel
        var modes = new WrapPanel { Name = "learn-modes" };
        foreach (var (id, label) in new[] { ("lernen", "Lernen"), ("tippen", "Eintippen"), ("quiz", "Quiz"), ("karten", "Karten verwalten") })
        {
            var on = LearnMode == id; var mid = id;
            var b = new Button { Name = "learn-mode-" + id, Content = label, Padding = new Thickness(14, 6), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(10),
                Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { LearnMode = mid; _reveal = false; _quiz = null; _typedResult = ""; _learnMsg = ""; Show("lernen"); };
            modes.Children.Add(b);
        }
        var decks = new List<string> { "Alle Stapel" }; decks.AddRange(Learn.Decks);
        var deckBox = new ComboBox { Name = "learn-deck", Width = 200, ItemsSource = decks, SelectedIndex = LearnDeck is null ? 0 : Math.Max(0, decks.IndexOf(LearnDeck)) };
        deckBox.SelectionChanged += (_, _) => { var v = deckBox.SelectedIndex <= 0 ? null : decks[deckBox.SelectedIndex]; if (v != LearnDeck) { LearnDeck = v; _reveal = false; _quiz = null; Show("lernen"); } };
        modes.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(12, 0, 0, 6), VerticalAlignment = VerticalAlignment.Center, Children = { P("Stapel", null, 12), deckBox } });

        Control main = LearnMode switch { "tippen" => LearnTyped(), "quiz" => LearnQuiz(), "karten" => LearnCards(), _ => LearnStudy() };
        var mainBox = new Border { Name = "learn-main", CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(18), Child = main };
        var side = new Border { Name = "learn-side", Width = 300, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = new ScrollViewer { Content = LearnStats() } };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Children = { mainBox, Pos(side, 2) } };
        var root = new DockPanel { Name = "learn-root" };
        DockPanel.SetDock(modes, Avalonia.Controls.Dock.Top); root.Children.Add(modes); root.Children.Add(body);
        root.KeyDown += (_, e) =>
        {
            if (e.Source is TextBox) return;
            if (LearnMode == "lernen")
            {
                if (e.Key is Key.Space or Key.Enter && !_reveal) { _reveal = true; Show("lernen"); e.Handled = true; }
                else if (_reveal && e.Key is Key.D1 or Key.NumPad1) { LearnGrade(Grade.Falsch); e.Handled = true; }
                else if (_reveal && e.Key is Key.D2 or Key.NumPad2) { LearnGrade(Grade.Schwer); e.Handled = true; }
                else if (_reveal && e.Key is Key.D3 or Key.NumPad3 or Key.Space) { LearnGrade(Grade.Gut); e.Handled = true; }
                else if (_reveal && e.Key is Key.D4 or Key.NumPad4) { LearnGrade(Grade.Leicht); e.Handled = true; }
            }
            else if (LearnMode == "quiz" && _quiz is { } q && e.Key is >= Key.D1 and <= Key.D4) { LearnQuizAnswer((int)(e.Key - Key.D1)); e.Handled = true; }
        };
        root.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (LearnMode == "lernen") root.Focus(); }, Avalonia.Threading.DispatcherPriority.Input);
        root.Focusable = true;
        return FitPage("Karteikarten", new Panel(), root);
    }

    Control CardFace(string deck, string front, string? back)
    {
        var face = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 720 };
        face.Children.Add(new TextBlock { Text = deck, Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });
        face.Children.Add(new TextBlock { Name = "learn-front", Text = front, Foreground = Text, FontSize = 30, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        if (back is not null)
        {
            face.Children.Add(new Border { Height = 1, Background = Edge, Margin = new Thickness(0, 4) });
            face.Children.Add(new TextBlock { Name = "learn-back", Text = back, Foreground = Brush.Parse("#7FD3FF"), FontSize = 24, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        }
        return new Border { Name = "learn-card", MinHeight = 260, CornerRadius = new CornerRadius(18), Padding = new Thickness(28), BoxShadow = CardShadow,
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#FF1C3157"), 0), new GradientStop(Color.Parse("#FF122340"), 1) } },
            BorderBrush = Brush.Parse("#557FD3FF"), BorderThickness = new Thickness(1), Child = face };
    }

    Control LearnStudy()
    {
        var (total, due, _, _) = Learn.Progress(LearnDeck);
        var sp = new StackPanel { Spacing = 14 };
        if (_learnMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "learn-msg", Text = _learnMsg, Foreground = Brush.Parse("#4ADE80"), FontSize = 12.5 });
        var card = Learn.DueCards(LearnDeck).FirstOrDefault();
        if (card is null)
        {
            sp.Children.Add(CardFace(LearnDeck ?? "Alle Stapel", total == 0 ? "Noch keine Karten" : "✓ Für heute alles gelernt", total == 0 ? "Unter „Karten verwalten“ anlegen oder eine Liste einfügen." : "Morgen geht es weiter – die Vorschau rechts zeigt, wie viel kommt."));
            return sp;
        }
        sp.Children.Add(new ProgressBar { Name = "learn-bar", Minimum = 0, Maximum = Math.Max(1, due + Learn.ReviewedToday(LearnDeck)), Value = Learn.ReviewedToday(LearnDeck), Height = 6 });
        sp.Children.Add(P($"Noch {due} fällig", null, 12));
        var face = CardFace(card.Deck, card.Front, _reveal ? card.Back : null);
        face.PointerPressed += (_, _) => { if (!_reveal) { _reveal = true; Show("lernen"); } };
        sp.Children.Add(face);
        if (!_reveal)
        {
            var show = new Button { Name = "learn-show", Content = "Antwort zeigen  (Leertaste)", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(22, 10), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 15 };
            show.Click += (_, _) => { _reveal = true; Show("lernen"); }; sp.Children.Add(show);
        }
        else
        {
            var row = new UniformGrid { Name = "learn-grades", Columns = 4, HorizontalAlignment = HorizontalAlignment.Center, Width = 620 };
            foreach (var (g, label, key, col) in new[] { (Grade.Falsch, "Nochmal", "1", "#EF4444"), (Grade.Schwer, "Schwer", "2", "#F59E0B"), (Grade.Gut, "Gut", "3", "#22C55E"), (Grade.Leicht, "Leicht", "4", "#3B82F6") })
            {
                var gg = g; var days = Learn.PreviewDays(card.Id, g);
                var b = new Button { Name = "learn-grade-" + key, Margin = new Thickness(4), Padding = new Thickness(10, 8), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = Brush.Parse(col), Foreground = Brushes.White,
                    Content = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = $"{label} ({key})", FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center }, new TextBlock { Text = g == Grade.Falsch ? "morgen" : $"in {days} T.", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
                b.Click += (_, _) => LearnGrade(gg); row.Children.Add(b);
            }
            sp.Children.Add(row);
        }
        return sp;
    }

    Control LearnTyped()
    {
        var sp = new StackPanel { Spacing = 14 };
        // Nach der Antwort bleibt die beantwortete Karte stehen (sie ist dann nicht mehr fällig), bis „Weiter“ gedrückt wird
        var card = _typedResult.Length > 0 && _typedCard is { } tid ? Learn.Store.Cards.FirstOrDefault(c => c.Id == tid) : Learn.DueCards(LearnDeck).FirstOrDefault();
        if (card is null) { sp.Children.Add(CardFace(LearnDeck ?? "Alle Stapel", "✓ Nichts fällig", null)); return sp; }
        var answered = _typedCard == card.Id && _typedResult.Length > 0;
        sp.Children.Add(CardFace(card.Deck, card.Front, answered ? card.Back : null));
        if (!answered)
        {
            var box = new TextBox { Name = "learn-typed", PlaceholderText = "Antwort eintippen und Enter drücken", FontSize = 18, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Stretch };
            void Check()
            {
                var (ok, typo) = LearnBox.CheckTyped(card.Back, box.Text ?? "");
                _typedCard = card.Id; _typedResult = ok ? (typo ? "✓ Richtig (kleiner Tippfehler)" : "✓ Richtig") : $"✕ Leider falsch – richtig ist: {card.Back}";
                Learn.Answer(card.Id, ok ? (typo ? Grade.Schwer : Grade.Gut) : Grade.Falsch); Show("lernen");
            }
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Check(); e.Handled = true; } };
            box.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => box.Focus(), Avalonia.Threading.DispatcherPriority.Input);
            sp.Children.Add(box);
        }
        else
        {
            sp.Children.Add(new TextBlock { Name = "learn-typed-result", Text = _typedResult, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brush.Parse(_typedResult.StartsWith('✓') ? "#4ADE80" : "#F87171") });
            var next = new Button { Name = "learn-next", Content = "Weiter", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(22, 8), HorizontalAlignment = HorizontalAlignment.Center };
            next.Click += (_, _) => { _typedResult = ""; _typedCard = null; Show("lernen"); }; sp.Children.Add(next);
        }
        return sp;
    }

    public void LearnQuizAnswer(int index)
    {
        if (_quiz is not { } q) return;
        var ok = index == q.Correct; Learn.Answer(q.Card.Id, ok ? Grade.Gut : Grade.Falsch);
        _learnMsg = ok ? "✓ Richtig!" : $"✕ Richtig wäre: {q.Options[q.Correct]}"; _quiz = null; Show("lernen");
    }

    Control LearnQuiz()
    {
        var sp = new StackPanel { Spacing = 14 };
        if (_learnMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "learn-msg", Text = _learnMsg, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brush.Parse(_learnMsg.StartsWith('✓') ? "#4ADE80" : "#F87171") });
        try { _quiz ??= Learn.Quiz(LearnDeck); }
        catch (InvalidOperationException ex) { sp.Children.Add(CardFace(LearnDeck ?? "Alle Stapel", "Quiz nicht möglich", ex.Message)); return sp; }
        var q = _quiz.Value;
        sp.Children.Add(CardFace(q.Card.Deck, q.Card.Front, null));
        var grid = new UniformGrid { Name = "learn-options", Columns = 2, MaxWidth = 720 };
        for (var i = 0; i < q.Options.Count; i++)
        {
            var idx = i; var b = new Button { Name = $"learn-opt-{i}", Content = $"{i + 1}  {q.Options[i]}", Margin = new Thickness(5), Padding = new Thickness(14, 12), FontSize = 15, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
            b.Click += (_, _) => LearnQuizAnswer(idx); grid.Children.Add(b);
        }
        sp.Children.Add(grid);
        return sp;
    }

    Control LearnCards()
    {
        var sp = new StackPanel { Spacing = 8 };
        // Neue Karte
        var deck = new TextBox { Name = "learn-new-deck", Text = LearnDeck ?? "Allgemein", Width = 150, PlaceholderText = "Stapel" };
        var front = new TextBox { Name = "learn-new-front", PlaceholderText = "Frage", Width = 240 }; var back = new TextBox { Name = "learn-new-back", PlaceholderText = "Antwort", Width = 240 };
        void AddCard() { try { Learn.Add(deck.Text ?? "", front.Text ?? "", back.Text ?? ""); _learnMsg = "Karte angelegt."; LearnDeck = deck.Text?.Trim(); } catch (ArgumentException ex) { _learnMsg = "⚠ " + ex.Message; } Show("lernen"); }
        OnEnter(back, AddCard);
        var add = new Button { Name = "learn-add", Content = "＋ Karte", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(12, 5) }; add.Click += (_, _) => AddCard();
        sp.Children.Add(new WrapPanel { Children = { deck, new Border { Width = 6 }, front, new Border { Width = 6 }, back, new Border { Width = 6 }, add } });
        if (_learnMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "learn-msg", Text = _learnMsg, Foreground = Brush.Parse(_learnMsg.StartsWith('⚠') ? "#F87171" : "#4ADE80"), FontSize = 12 });
        var search = new TextBox { Name = "learn-search", Text = _learnSearch, PlaceholderText = "Suchen …", Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        search.TextChanged += (_, _) => { if (search.Text != _learnSearch) { _learnSearch = search.Text ?? ""; Show("lernen"); } };
        sp.Children.Add(search);
        var list = new StackPanel { Name = "learn-list", Spacing = 2 };
        var cards = Learn.Store.Cards.Where(c => (LearnDeck is null || c.Deck == LearnDeck) && (_learnSearch.Length == 0 || c.Front.Contains(_learnSearch, StringComparison.OrdinalIgnoreCase) || c.Back.Contains(_learnSearch, StringComparison.OrdinalIgnoreCase))).OrderBy(c => c.Deck).ThenBy(c => c.Front).Take(300).ToList();
        var k = 0;
        foreach (var c in cards)
        {
            var id = c.Id; var i = k++;
            var f = new TextBox { Name = $"learn-f-{i}", Text = c.Front }; var b = new TextBox { Name = $"learn-b-{i}", Text = c.Back };
            void Save() { if (f.Text != c.Front || b.Text != c.Back) try { Learn.Edit(id, f.Text ?? "", b.Text ?? ""); } catch (ArgumentException ex) { _learnMsg = "⚠ " + ex.Message; Show("lernen"); } }
            f.LostFocus += (_, _) => Save(); b.LostFocus += (_, _) => Save(); OnEnter(f, Save); OnEnter(b, Save);
            var del = new Button { Name = $"learn-del-{i}", Content = "✕", Padding = new Thickness(8, 2) }; ToolTip.SetTip(del, "Karte löschen");
            del.Click += (_, _) => { Learn.Delete(id); Show("lernen"); };
            var due = c.Due <= DateTime.Now ? "fällig" : $"in {(c.Due.Date - DateTime.Now.Date).Days} T.";
            list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,*,8,Auto,8,Auto"), Children = { f, Pos(b, 2), Pos(P(due + (c.Lapses >= 3 ? " ⚠" : ""), null, 11), 4), Pos(del, 6) } });
        }
        if (cards.Count == 0) list.Children.Add(P("Keine Karten gefunden.", null, 12));
        sp.Children.Add(new ScrollViewer { MaxHeight = 520, Content = list });
        // Liste einfügen
        var paste = new TextBox { Name = "learn-import-text", AcceptsReturn = true, MinHeight = 70, PlaceholderText = "Liste einfügen: eine Karte pro Zeile „Frage;Antwort“ (oder Tab, z. B. aus Excel/Anki)", TextWrapping = TextWrapping.Wrap };
        var imp = new Button { Name = "learn-import", Content = "Liste übernehmen", Padding = new Thickness(12, 5) };
        imp.Click += (_, _) => { var n = Learn.Import(deck.Text?.Trim() is { Length: > 0 } d ? d : "Allgemein", paste.Text ?? ""); _learnMsg = $"{n} Karte(n) übernommen."; Show("lernen"); };
        sp.Children.Add(new Expander { Name = "learn-import-box", Header = "Liste einfügen (Excel, Anki)", Content = new StackPanel { Spacing = 6, Children = { paste, imp } } });
        return sp;
    }

    Control LearnStats()
    {
        var sp = new StackPanel { Spacing = 6 };
        void Fact(string l, string v) => sp.Children.Add(Row(P(l, null, 12), new TextBlock { Text = v, Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold }));
        var (total, due, learned, hard) = Learn.Progress(LearnDeck); var (streak, today) = Learn.Streak();
        sp.Children.Add(H(LearnDeck ?? "Alle Stapel", 13));
        Fact("Karten", total.ToString()); Fact("Heute fällig", due.ToString()); Fact("Sitzen sicher (≥ 21 Tage)", learned.ToString()); Fact("Machen Mühe", hard.ToString());
        Fact("Lernserie", $"{streak} Tag{(streak == 1 ? "" : "e")}{(today ? " ✓ heute" : "")}");
        // 7-Tage-Vorschau
        var fc = Learn.Forecast();
        sp.Children.Add(H("Nächste 7 Tage", 12));
        sp.Children.Add(new Border { Name = "learn-forecast", Height = 110, CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = new ShapeView((w, h) =>
        {
            var s = new List<Shape>(); var max = Math.Max(1, fc.Max(x => x.Due)); var bw = w / fc.Count;
            for (var i = 0; i < fc.Count; i++)
            {
                var bh = (h - 30) * fc[i].Due / max; var x = i * bw;
                s.Add(new DPoly([(x + 4, h - 18 - bh), (x + bw - 4, h - 18 - bh), (x + bw - 4, h - 18), (x + 4, h - 18)], i == 0 ? "#F5A524" : "#2F86F6", null, 0, "DATEN"));
                s.Add(new DText(x + bw / 2, h - 8, i == 0 ? "heute" : fc[i].Day.ToString("ddd", De), 10, "#C9D6EA", "TEXT"));
                if (fc[i].Due > 0) s.Add(new DText(x + bw / 2, h - 26 - bh, fc[i].Due.ToString(), 10, "#FFFFFF", "TEXT", true));
            }
            return s;
        }, "#10203A") });
        var leeches = Learn.Leeches().Where(c => LearnDeck is null || c.Deck == LearnDeck).Take(5).ToList();
        if (leeches.Count > 0) { sp.Children.Add(H("Problemkarten", 12)); foreach (var c in leeches) sp.Children.Add(P($"⚠ {c.Front} ({c.Lapses}× vergessen)", null, 11.5)); }
        // Prüfungsplan
        sp.Children.Add(H("Prüfungsplan", 12));
        var calcs = new StackPanel();
        LiveCalc(calcs, "learn-exam", "Bis zur Prüfung", [("d", "Prüfungsdatum (TT.MM.JJJJ)", DateTime.Now.AddDays(21).ToString("dd.MM.yyyy"), null)],
            v => { if (!DateTime.TryParseExact(v("d").Trim(), "dd.MM.yyyy", De, System.Globalization.DateTimeStyles.None, out var d)) throw new FormatException("Datum bitte als TT.MM.JJJJ."); var p = Learn.ExamPlan(d, LearnDeck); return $"{p.NewPerDay} neue Karten pro Tag ({p.NewCards} neue, {p.DaysLeft} Tage). {p.Advice}"; });
        sp.Children.Add(calcs);
        // Stapel-Werkzeuge
        if (LearnDeck is { } dk)
        {
            sp.Children.Add(H("Stapel", 12));
            var name = new TextBox { Name = "learn-rename", Text = dk };
            void Rename() { if (!string.IsNullOrWhiteSpace(name.Text) && name.Text != dk && Learn.Decks.Contains(dk)) { Learn.RenameDeck(dk, name.Text); LearnDeck = name.Text.Trim(); Show("lernen"); } }
            name.LostFocus += (_, _) => Rename(); OnEnter(name, Rename); sp.Children.Add(name);
            var rev = new Button { Name = "learn-reverse", Content = "Umgekehrt lernen", Padding = new Thickness(10, 4) };
            rev.Click += (_, _) => { var n = Learn.AddReversed(dk); _learnMsg = $"{n} umgekehrte Karte(n) im Stapel „{dk} (umgekehrt)“."; Show("lernen"); };
            var exp = new Button { Name = "learn-export", Content = "Als CSV speichern", Padding = new Thickness(10, 4) };
            exp.Click += (_, _) => { var f = Path.Combine(WorkFolder("Karteikarten"), DevStudio.SafeName(dk) + ".csv"); File.WriteAllText(f, Learn.Export(dk)); _learnMsg = "Gespeichert: " + f; NotifyFileResult(_learnMsg); Show("lernen"); };
            sp.Children.Add(new WrapPanel { Children = { rev, new Border { Width = 4 }, exp } });
        }
        return sp;
    }
}
