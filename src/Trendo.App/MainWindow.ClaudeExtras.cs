using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude „besser als Claude“ (Wünsche 02.11.2026): schöne Antworten (Markdown, Tabellen, farbiger Code, Kopieren), Glas-Design mit Gold-Blau-Lichtkanten,
/// lebendige Animationen, Projekt-Gedächtnis, Zeitreise, Gutachter mit Note, echt testen bis es läuft, Claude schaut sich sein Ergebnis an, freihändig sprechen.</summary>
public partial class MainWindow
{
    // ───────────── Glas-Design ─────────────
    static IBrush GlassEdge => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse(ClaudeLight ? "#CCC9A040" : "#B3E8C26A"), 0), new GradientStop(Color.Parse(ClaudeLight ? "#33000000" : "#26FFFFFF"), 0.45), new GradientStop(Color.Parse(ClaudeLight ? "#CC3B6FC9" : "#B35B8FE0"), 1) },
    };
    static IBrush GlassFill => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse(ClaudeLight ? "#F2FFFFFF" : "#2EFFFFFF"), 0), new GradientStop(Color.Parse(ClaudeLight ? "#CCF7F5EE" : "#0DFFFFFF"), 1) },
    };
    static IBrush GlassBg => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = ClaudeLight
            ? [new GradientStop(Color.Parse("#FFFAF9F5"), 0), new GradientStop(Color.Parse("#FFF4F3EE"), 0.6), new GradientStop(Color.Parse("#FFEAF0FA"), 1)]
            : [new GradientStop(Color.Parse("#FF282725"), 0), new GradientStop(Color.Parse("#FF23242B"), 0.6), new GradientStop(Color.Parse("#FF1C2233"), 1)],
    };
    static IBrush CGold => Brush.Parse(ClaudeLight ? "#FF9A6B12" : "#FFE8C26A");
    static IBrush CBlue => Brush.Parse(ClaudeLight ? "#FF2459B8" : "#FF8DB4F2");
    static readonly IBrush CodeBg = Brush.Parse("#E6141416"); // Code bleibt dunkel – wie in Claude

    /// <summary>Glas-Fläche: halbtransparenter Verlauf, Lichtkante von Gold nach Blau, weicher Schatten.</summary>
    static Border CGlass(Control child, double radius = 14, Thickness? pad = null, string? name = null) => new()
    {
        Name = name, Background = GlassFill, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radius), Padding = pad ?? new Thickness(14, 10),
        BoxShadow = BoxShadows.Parse("0 8 28 0 #59000000"), Child = child,
    };

    // ───────────── Lebendige Animationen ─────────────
    /// <summary>Sanft einblenden und von unten hereingleiten (neue Nachrichten, Karten).</summary>
    static T Appear<T>(T c, int delayMs = 0) where T : Control
    {
        c.Opacity = 0; c.RenderTransform = TransformOperations.Parse("translateY(10px)");
        c.Transitions = [
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(320), Easing = new Avalonia.Animation.Easings.CubicEaseOut(), Delay = TimeSpan.FromMilliseconds(delayMs) },
            new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(380), Easing = new Avalonia.Animation.Easings.CubicEaseOut(), Delay = TimeSpan.FromMilliseconds(delayMs) },
        ];
        Dispatcher.UIThread.Post(() => { c.Opacity = 1; c.RenderTransform = TransformOperations.Parse("translateY(0px)"); }, DispatcherPriority.Background);
        return c;
    }

    /// <summary>„Claude denkt nach“ pulsiert sanft, bis die Antwort fertig ist (Zeitgeber wird mit <paramref name="ct"/> beendet).</summary>
    static void Pulse(Control c, CancellationToken ct)
    {
        c.Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(650) }];
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        t.Tick += (_, _) => { if (ct.IsCancellationRequested) { t.Stop(); c.Opacity = 1; return; } c.Opacity = c.Opacity > 0.7 ? 0.4 : 1; };
        // sofort anhalten – beim Ende der Antwort und wenn die Anzeige verschwindet (kein Zeitgeber läuft weiter, wenn das Fenster zu ist)
        void Stop() { if (Dispatcher.UIThread.CheckAccess()) t.Stop(); else Dispatcher.UIThread.Post(t.Stop); }
        ct.Register(Stop);
        c.DetachedFromVisualTree += (_, _) => Stop();
        t.Start();
    }

    // ───────────── Zeitleiste der Arbeitsschritte ─────────────
    static string Duration(TimeSpan t) => t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} min {t.Seconds} s" : $"{Math.Max(1, (int)t.TotalSeconds)} s";

    /// <summary>Arbeitsschritte als Zeitleiste: Punkt (blau = lesen/denken, gold = ändern, rot = Fehler), Linie, Text und Zeit seit Beginn.</summary>
    Control Timeline(IReadOnlyList<ClaudeChats.Step> steps)
    {
        var sp = new StackPanel { Name = "claude-timeline-steps", Spacing = 0 };
        var start = steps.FirstOrDefault()?.At;
        var timed = steps.Count > 1 && steps.Max(s => s.At) - steps.Min(s => s.At) > TimeSpan.FromSeconds(0.5); // ältere Verläufe ohne echte Zeiten: keine Zeiten zeigen
        for (var i = 0; i < steps.Count; i++) sp.Children.Add(TimelineRow(steps[i], timed ? start : null, i < steps.Count - 1));
        return sp;
    }

    Control TimelineRow(ClaudeChats.Step st, DateTimeOffset? start, bool line)
    {
        var color = st.Error ? Brush.Parse("#FFF87171") : st.Changes ? CGold : CBlue;
        var dot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = color, Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        var rail = new Border { Width = 2, Background = ClaudeLine, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0), IsVisible = line };
        var text = new TextBlock { Text = st.Text, Foreground = st.Error ? color : ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 2, 6, 6) };
        var when = new TextBlock { Text = start is { } s0 && st.At is { } a ? "+" + (a - s0).ToString(@"m\:ss") : "", Foreground = ClaudeMuted, FontSize = 10.5, Margin = new Thickness(0, 3, 0, 0) };
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*,Auto") };
        Grid.SetColumn(text, 1); Grid.SetColumn(when, 2);
        g.Children.Add(rail); g.Children.Add(dot); g.Children.Add(text); g.Children.Add(when);
        return g;
    }

    // ───────────── Geräusche ─────────────
    /// <summary>Für Tests: Ton nachbilden (Art: fertig, frage, fehler).</summary>
    public static Action<string>? SoundOverride { get; set; }
    bool ClaudeSoundsOn => KiSourceSettings.Load(_core.DataRoot).ClaudeSounds;

    /// <summary>Nach einer Antwort: fertig, Rückfrage/Plan wartet oder Fehler – je ein dezenter Ton (abschaltbar).</summary>
    void ClaudeSoundAfter(ClaudeDock dock, ChatUi ui, string chatId)
    {
        if (!ClaudeSoundsOn || CChats.Get(ChatStudio(dock), chatId)?.Messages.LastOrDefault(x => x.Role == "assistant") is not { } m) return;
        var kind = m.Text.StartsWith('⚠') ? "fehler" : ui.Plan is not null || ClaudeChats.SplitQuestion(ClaudeChats.SplitSuggestions(ClaudeTools.Strip(m.Text)).Text).Question is not null || ui.Drafts.Any(d => d.ChatId == chatId) ? "frage" : "fertig";
        ClaudeSound(kind);
    }

    /// <summary>Ton abspielen – Trendo erzeugt ihn selbst (keine fremden Dateien): fertig = zwei helle Töne aufwärts, frage = ein Ton, fehler = zwei tiefe abwärts.</summary>
    void ClaudeSound(string kind)
    {
        if (SoundOverride is { } o) { o(kind); return; }
        if (_opt.Isolated || !OperatingSystem.IsWindows()) return;
        try
        {
            var file = Path.Combine(Path.GetTempPath(), $"trendo-klang-{kind}.wav");
            if (!File.Exists(file)) File.WriteAllBytes(file, Chime(kind switch { "fehler" => [330, 247], "frage" => [740], _ => [784, 1175] }));
            PlaySound(file, IntPtr.Zero, 0x00020001); // SND_FILENAME | SND_ASYNC: spielt im Hintergrund
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Kurzer, weicher Glockenton als WAV (16 Bit, mono): je Note 140 ms mit sanftem Ein- und Ausklang.</summary>
    public static byte[] Chime(IReadOnlyList<double> notes)
    {
        const int rate = 22050; var per = (int)(rate * 0.14);
        var samples = new short[per * notes.Count];
        for (var n = 0; n < notes.Count; n++)
            for (var i = 0; i < per; i++)
            {
                var env = Math.Min(1, i / (rate * 0.008)) * Math.Exp(-4.0 * i / per);
                var v = Math.Sin(2 * Math.PI * notes[n] * i / rate) * 0.8 + Math.Sin(4 * Math.PI * notes[n] * i / rate) * 0.2;
                samples[n * per + i] = (short)(v * env * 0.22 * short.MaxValue);
            }
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + samples.Length * 2); w.Write("WAVE"u8); w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8); w.Write(samples.Length * 2);
        foreach (var s in samples) w.Write(s);
        return ms.ToArray();
    }

    // ───────────── Schöne Antworten ─────────────
    int _codeBlocks;

    /// <summary>Antworttext wie in Claude: Überschriften, Absätze mit Fett/Kursiv/Code/Links, Listen, Tabellen, Zitate und Codeblöcke mit Farben und „Kopieren“.</summary>
    IEnumerable<Control> ChatBlocks(string text)
    {
        foreach (var b in ChatMarkdown.Parse(text))
            switch (b)
            {
                case ChatMarkdown.Heading h:
                    var ht = RichText(h.Text, h.Level switch { 1 => 21, 2 => 18, _ => 15.5 }); ht.FontWeight = FontWeight.SemiBold; ht.Margin = new Thickness(0, h.Level <= 2 ? 8 : 4, 0, 0);
                    yield return ht; break;
                case ChatMarkdown.Para p when p.Text.EndsWith(':') && p.Text.Length <= 40: // Zwischenüberschrift wie „Stärken:“
                    var lt = RichText(p.Text); lt.FontWeight = FontWeight.SemiBold; lt.Foreground = CGold; lt.Margin = new Thickness(0, 4, 0, 0);
                    yield return lt; break;
                case ChatMarkdown.Para p: yield return RichText(p.Text); break;
                case ChatMarkdown.ListBlock l:
                {
                    var sp = new StackPanel { Spacing = 4, Margin = new Thickness(4, 0, 0, 0) };
                    for (var i = 0; i < l.Items.Count; i++)
                    {
                        var mark = new TextBlock { Text = l.Ordered ? $"{l.Start + i}." : "•", Foreground = l.Ordered ? CGold : ClaudeAccent, FontSize = 14, MinWidth = l.Ordered ? 24 : 16, FontWeight = FontWeight.SemiBold };
                        var row = new DockPanel { Children = { Dk(mark, Avalonia.Controls.Dock.Left), RichText(l.Items[i]) } };
                        sp.Children.Add(row);
                    }
                    yield return sp; break;
                }
                case ChatMarkdown.Table t: yield return MdTable(t); break;
                case ChatMarkdown.Quote q:
                    var qt = RichText(q.Text); qt.Foreground = ClaudeMuted; qt.FontStyle = FontStyle.Italic;
                    yield return new Border { BorderBrush = CGold, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 2), Child = qt }; break;
                case ChatMarkdown.Rule: yield return new Border { Height = 1, Background = GlassEdge, Margin = new Thickness(0, 6) }; break;
                case ChatMarkdown.Code c: yield return CodeBlock(c); break;
            }
    }

    /// <summary>Fließtext mit Fett, Kursiv, Inline-Code und anklickbaren Links.</summary>
    SelectableTextBlock RichText(string text, double size = 14)
    {
        var tb = new SelectableTextBlock { Foreground = ClaudeFg, FontSize = size, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.5, Inlines = [] };
        foreach (var s in ChatMarkdown.Inline(text))
            switch (s.Kind)
            {
                case ChatMarkdown.Kind.Bold: tb.Inlines!.Add(new Run(s.Text) { FontWeight = FontWeight.Bold }); break;
                case ChatMarkdown.Kind.Italic: tb.Inlines!.Add(new Run(s.Text) { FontStyle = FontStyle.Italic }); break;
                case ChatMarkdown.Kind.Code: tb.Inlines!.Add(new Run(s.Text) { FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"), Background = ClaudeItem, Foreground = Brush.Parse(ClaudeLight ? "#FFB0442A" : "#FFF0B28F"), FontSize = size - 1 }); break;
                case ChatMarkdown.Kind.Link:
                    var url = s.Url!;
                    var link = new TextBlock { Text = s.Text, Foreground = CBlue, TextDecorations = TextDecorations.Underline, Cursor = new Cursor(StandardCursorType.Hand), FontSize = size };
                    ToolTip.SetTip(link, url + " – im Browser öffnen");
                    link.PointerPressed += (_, e) => { e.Handled = true; _core.Audit.Append(Principal.LocalOwner.Name, "claude.link", new Uri(url).Host); if (!_opt.Isolated) Launch(url); };
                    tb.Inlines!.Add(new InlineUIContainer(link) { BaselineAlignment = BaselineAlignment.TextBottom }); break;
                default: tb.Inlines!.Add(new Run(s.Text)); break;
            }
        return tb;
    }

    /// <summary>Tabelle als Glas-Raster: Kopfzeile in Gold, Zeilen abwechselnd hinterlegt.</summary>
    Control MdTable(ChatMarkdown.Table t)
    {
        var cols = Math.Max(t.Head.Count, t.Rows.Count == 0 ? 0 : t.Rows.Max(r => r.Count));
        var g = new Grid { Name = "claude-md-table" };
        for (var c = 0; c < cols; c++) g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        void Cell(int r, int c, string text, bool head)
        {
            var tb = RichText(text, 13); if (head) { tb.FontWeight = FontWeight.SemiBold; tb.Foreground = CGold; }
            var cell = new Border { Padding = new Thickness(10, 6), Child = tb, BorderBrush = Brush.Parse("#33FFFFFF"), BorderThickness = new Thickness(0, 0, c < cols - 1 ? 1 : 0, head ? 1 : 0),
                Background = head ? Brush.Parse("#1AE8C26A") : r % 2 == 0 ? Brush.Parse("#0AFFFFFF") : Brushes.Transparent };
            Grid.SetRow(cell, r); Grid.SetColumn(cell, c); g.Children.Add(cell);
        }
        g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var c = 0; c < cols; c++) Cell(0, c, c < t.Head.Count ? t.Head[c] : "", true);
        for (var r = 0; r < t.Rows.Count; r++)
        {
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < cols; c++) Cell(r + 1, c, c < t.Rows[r].Count ? t.Rows[r][c] : "", false);
        }
        return CGlass(new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = g }, 10, new Thickness(0));
    }

    /// <summary>Codeblock wie im Editor: Sprache oben, „Kopieren“-Knopf, Schlüsselwörter, Texte, Kommentare und Zahlen farbig.</summary>
    Control CodeBlock(ChatMarkdown.Code c)
    {
        var tb = new SelectableTextBlock { FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"), FontSize = 12.5, Foreground = Brush.Parse("#FFE6E4DC"), TextWrapping = TextWrapping.Wrap, Inlines = [] };
        foreach (var tok in CodeColors.Tokens(c.Lang, c.Text))
            tb.Inlines!.Add(new Run(tok.Text) { Foreground = tok.Kind switch
            {
                CodeColors.Kind.Keyword => Brush.Parse("#FFC792EA"), CodeColors.Kind.String => Brush.Parse("#FFC3E88D"), CodeColors.Kind.Comment => Brush.Parse("#FF7F8C98"),
                CodeColors.Kind.Number => Brush.Parse("#FFF78C6C"), CodeColors.Kind.Tag => Brush.Parse("#FF82AAFF"), _ => Brush.Parse("#FFE6E4DC"),
            }, FontStyle = tok.Kind == CodeColors.Kind.Comment ? FontStyle.Italic : FontStyle.Normal });
        var n = _codeBlocks++;
        var copy = new Button { Name = $"claude-code-copy-{n}", Content = "Kopieren", FontSize = 11, Padding = new Thickness(8, 2), Background = Brushes.Transparent, Foreground = ClaudeMuted, BorderThickness = new Thickness(0) };
        ToolTip.SetTip(copy, "Code in die Zwischenablage kopieren");
        copy.Click += async (_, _) => { ResearchCopy(c.Text); copy.Content = "✓ Kopiert"; await Task.Delay(1500); copy.Content = "Kopieren"; };
        var head = new DockPanel { Margin = new Thickness(12, 4, 4, 0), Children = { Dk(copy, Avalonia.Controls.Dock.Right), new TextBlock { Text = c.Lang.Length > 0 ? c.Lang : "code", Foreground = CGold, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } } };
        return new Border { Name = "claude-code", Background = CodeBg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Child = new StackPanel { Children = { head, new Border { Padding = new Thickness(12, 4, 12, 10), Child = tb } } } };
    }

    // ───────────── Projekt-Gedächtnis ─────────────
    ClaudeMemory CMemory => new(_core.DataRoot);
    ClaudeTimeline CTimeline => new(_core.DataRoot);

    /// <summary>Gedächtnis des Projekts: was gilt, löschen, ergänzen.</summary>
    Control MemoryPanel(ClaudeDock dock, ClaudeChats.Chat? chat)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = "Projekt-Gedächtnis", Foreground = CGold, FontSize = 15, FontWeight = FontWeight.SemiBold });
        sp.Children.Add(new TextBlock { Text = "Das merkt sich Claude für dieses Projekt – es gilt bei jeder Nachricht, auch nach Tagen. Claude ergänzt es selbst, wenn du etwas Dauerhaftes sagst.", Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (chat is null) { sp.Children.Add(new TextBlock { Text = "Schreib die erste Nachricht – dann hat das Projekt ein Gedächtnis.", Foreground = ClaudeFg, FontSize = 13 }); return Appear(CGlass(sp, name: "claude-memory")); }
        var items = CMemory.Items(chat.Id);
        if (items.Count == 0) sp.Children.Add(new TextBlock { Name = "claude-memory-empty", Text = "Noch nichts gemerkt.", Foreground = ClaudeFg, FontSize = 13 });
        for (var i = 0; i < items.Count; i++)
        {
            var idx = i;
            var del = new Button { Name = $"claude-memory-del-{i}", Content = "✕", FontSize = 11, Padding = new Thickness(6, 1), Background = Brushes.Transparent, Foreground = ClaudeMuted, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(del, "Vergessen");
            del.Click += (_, _) => { CMemory.Remove(chat.Id, idx); _core.Audit.Append(Principal.LocalOwner.Name, "claude.gedaechtnis.weg", ChatStudio(dock)); RenderChat(dock); };
            sp.Children.Add(new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), new TextBlock { Text = "• " + items[i], Foreground = ClaudeFg, FontSize = 13, TextWrapping = TextWrapping.Wrap } } });
        }
        var add = new TextBox { Name = "claude-memory-input", PlaceholderText = "Etwas, das immer gelten soll – z. B. „Farben: Dunkelblau und Gold“", FontSize = 12.5, Background = ClaudeInput, Foreground = ClaudeFg, BorderBrush = ClaudeLine, CornerRadius = new CornerRadius(8) };
        var ok = new Button { Name = "claude-memory-add", Content = "Merken", FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(6, 0, 0, 0), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        void Add() { if (add.Text is { Length: > 0 } t && CMemory.Add(chat.Id, t)) _core.Audit.Append(Principal.LocalOwner.Name, "claude.gedaechtnis.neu", ChatStudio(dock)); RenderChat(dock); }
        ok.Click += (_, _) => Add();
        add.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Add(); } };
        sp.Children.Add(new DockPanel { Margin = new Thickness(0, 4, 0, 0), Children = { Dk(ok, Avalonia.Controls.Dock.Right), add } });
        return Appear(CGlass(sp, name: "claude-memory"));
    }

    /// <summary>Für Tests: Gedächtnis-Einträge des offenen Projekts.</summary>
    public IReadOnlyList<string> ClaudeMemoryItems() => ClaudeChatCurrent() is { } c ? CMemory.Items(c.Id) : [];

    // ───────────── Zeitreise ─────────────
    Control TimelinePanel(ClaudeDock dock, ClaudeChats.Chat? chat)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = "Zeitreise", Foreground = CGold, FontSize = 15, FontWeight = FontWeight.SemiBold });
        sp.Children.Add(new TextBlock { Text = "Vor jeder Antwort mit Änderungen sichert Trendo das Projekt. Spring zu jedem Stand zurück – den Stand von jetzt sichert Trendo vorher, du kannst also jederzeit wieder vor.", Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var points = chat is null ? [] : CTimeline.Points(chat.Id);
        if (points.Count == 0) sp.Children.Add(new TextBlock { Name = "claude-timeline-empty", Text = "Noch keine Versionspunkte – sie entstehen, sobald Claude etwas ändert.", Foreground = ClaudeFg, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var go = new Button { Name = $"claude-timeline-go-{i}", Content = "Hierhin zurück", FontSize = 11.5, Padding = new Thickness(10, 3), Background = Brushes.Transparent, Foreground = CGold, BorderBrush = CGold, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            ToolTip.SetTip(go, "Projekt auf den Stand vor dieser Antwort bringen");
            go.Click += (_, _) => ClaudeTravel(dock, p.Id);
            var dot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = i == 0 ? CGold : CBlue, Margin = new Thickness(0, 5, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            var info = new StackPanel { Children = {
                new TextBlock { Text = p.Label, Foreground = ClaudeFg, FontSize = 13, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = $"{p.At.LocalDateTime:dd.MM. HH:mm} · vor dieser Antwort · {p.Changes} Änderung{(p.Changes == 1 ? "" : "en")} danach", Foreground = ClaudeMuted, FontSize = 11 } } };
            sp.Children.Add(new DockPanel { Margin = new Thickness(0, 2), Children = { Dk(dot, Avalonia.Controls.Dock.Left), Dk(go, Avalonia.Controls.Dock.Right), info } });
        }
        return Appear(CGlass(sp, name: "claude-timeline"));
    }

    public IReadOnlyList<ClaudeTimeline.Point> ClaudeTimelinePoints() => ClaudeChatCurrent() is { } c ? CTimeline.Points(c.Id) : [];
    public void ClaudeTravel(int index) { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) && ClaudeTimelinePoints() is { } ps && index < ps.Count) ClaudeTravel(d, ps[index].Id); }

    void ClaudeTravel(ClaudeDock dock, string pointId)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.Running || ui.ChatId is not { } cid || CChats.Get(ChatStudio(dock), cid) is not { } chat) return;
        var p = CTimeline.Points(cid).FirstOrDefault(x => x.Id == pointId);
        try
        {
            CTimeline.Travel(cid, pointId, chat.Folder);
            CChats.Add(ChatStudio(dock), cid, new ClaudeChats.Msg("assistant", $"Zeitreise: Das Projekt ist jetzt auf dem Stand vor „{p?.Label}“ ({p?.At.LocalDateTime:dd.MM. HH:mm}). Der Stand davor ist als „Vor der Zeitreise“ gesichert.", [], DateTimeOffset.Now));
            _core.Audit.Append(Principal.LocalOwner.Name, "claude.zeitreise", ChatStudio(dock));
            ui.LastSnapshot = null; ui.Panel = null;
            if (ui.PreviewFile is { } pf) ShowPreview(dock, pf);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { _claudeMsg = "Zeitreise ging nicht: " + e.Message; }
        RenderChat(dock);
    }

    // ───────────── Gutachter ─────────────
    /// <summary>Für Tests: Gutachter-Lauf nachbilden (Eingabe: Auftrag an den Gutachter, Rückgabe: Gutachten).</summary>
    public static Func<string, Task<string>>? ReviewOverride { get; set; }
    public Task ClaudeReviewRun() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? ClaudeReviewRun(d) : Task.CompletedTask;

    /// <summary>Zweite Meinung: eine eigene Claude-Sitzung sieht sich das Ergebnis an (ändert nichts) und gibt eine Note 1–10 mit Stärken, Schwächen und Verbesserungen.</summary>
    async Task ClaudeReviewRun(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.Running || ui.ChatId is not { } cid || CChats.Get(ChatStudio(dock), cid) is not { } chat || chat.Messages.Count == 0) return;
        var studio = ChatStudio(dock); var name = StudioCatalog.NameOf(dock.Studio ?? "", "Trendo");
        var request = chat.Messages.LastOrDefault(m => m.Role == "user")?.Text ?? chat.Title;
        var files = chat.Messages.Where(m => m.Changed is not null).SelectMany(m => m.Changed!).Select(c => c.Split(" (")[0]).Distinct().ToList();
        var prompt = ClaudeReview.Prompt(name, request, files);
        ui.Running = true; ui.Cts = new CancellationTokenSource();
        var wait = Appear(CGlass(new TextBlock { Text = "Der Gutachter sieht sich das Ergebnis an …", Foreground = CGold, FontSize = 13 }, name: "claude-review-wait"));
        ui.Log.Children.Add(wait); Pulse(wait, ui.Cts.Token); ui.Scroll.ScrollToEnd();
        string text; string? ki = ClaudeKi;
        try
        {
            if (ReviewOverride is { } o) text = await o(prompt);
            else if (CLimit.Active(DateTimeOffset.Now))
            {
                var fb = await ClaudeFallbackRun(prompt, ClaudeReview.Rule, chat.Folder, ui.Cts.Token);
                text = fb.Error is { } fe ? "⚠ " + fe : fb.Text; ki = fb.Ki;
            }
            else
            {
                var say = new System.Text.StringBuilder();
                await Task.Run(() => ClaudeRun(ClaudeCode.Args(prompt, ClaudeCode.Phase.Ask, null, ClaudeReview.Rule, null, ClaudeCode.Level.Sicher), chat.Folder,
                    l => { foreach (var ev in ClaudeCode.Parse(l, chat.Folder)) if (ev is ClaudeCode.Say s) say.AppendLine(s.Text); else if (ev is ClaudeCode.Done d && say.Length == 0) say.Append(d.Text); }, ui.Cts.Token));
                text = say.ToString().Trim();
            }
        }
        catch (OperationCanceledException) { text = "⚠ Gutachten angehalten."; }
        catch (InvalidOperationException e) { text = "⚠ " + e.Message; }
        if (text.Length == 0) text = "⚠ Der Gutachter hat nichts geliefert.";
        ClaudeLearnFrom(studio, text, true, false, null, chat.Title); text = StudioLearning.Strip(text); // Gutachten lehrt das Studio (Vorschlag, gilt erst nach Ja)
        CChats.Add(studio, cid, new ClaudeChats.Msg("review", text, [], DateTimeOffset.Now, null, 0, null, null, ki));
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.gutachten", $"{studio} · Note {ClaudeReview.Grade(text)?.ToString() ?? "–"}");
        ui.Cts.Cancel(); ui.Running = false; ui.Cts = null;
        RenderChat(dock);
    }

    /// <summary>Gutachten-Karte: große Note, Text, „Verbessern lassen“.</summary>
    Control ReviewCard(ClaudeDock dock, ClaudeChats.Msg m, bool last)
    {
        var grade = ClaudeReview.Grade(m.Text);
        var badge = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(32), BorderBrush = GlassEdge, BorderThickness = new Thickness(2), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
            Background = grade is >= 8 ? Brush.Parse("#2634D399") : grade is >= 5 ? Brush.Parse("#26E8C26A") : Brush.Parse("#26F87171"),
            Child = new TextBlock { Name = last ? "claude-review-grade" : null, Text = grade is { } g ? $"{g}/10" : "–", Foreground = ClaudeFg, FontSize = 17, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var body = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = "Gutachten – zweite Meinung", Foreground = CGold, FontSize = 14, FontWeight = FontWeight.SemiBold } } };
        foreach (var b in ChatBlocks(System.Text.RegularExpressions.Regex.Replace(m.Text, @"^[ \t*_]*Note\b[^\n]*\n?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline))) body.Children.Add(b); // Note steht schon groß links
        if (last && grade is < 10 && !m.Text.StartsWith('⚠'))
        {
            var fix = new Button { Name = "claude-review-improve", Content = "Verbessern lassen", FontSize = 12, Padding = new Thickness(12, 4), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left };
            ToolTip.SetTip(fix, "Claude setzt die Verbesserungen des Gutachters um");
            fix.Click += (_, _) => _ = ClaudeChatSend(dock, ClaudeReview.Improve(m.Text));
            body.Children.Add(fix);
        }
        if (m.Ki is { Length: > 0 } ki && !IsClaudeKi(ki)) body.Children.Add(new TextBlock { Text = "Gutachten von: " + ki, Foreground = ClaudeAccent, FontSize = 10.5 });
        var card = CGlass(new DockPanel { Children = { Dk(badge, Avalonia.Controls.Dock.Left), body } }, name: "claude-review");
        return last ? Appear(card) : card;
    }

    // ───────────── Echt testen und hinschauen ─────────────
    /// <summary>Für Tests: echten Start nachbilden (Ordner, geänderte Dateien → Befunde und Bildschirmfoto).</summary>
    public static Func<string, IReadOnlyList<string>, Task<ClaudeProbe.Result>>? ProbeOverride { get; set; }
    /// <summary>Vorgabe „Claude schaut sich sein Ergebnis an“ (KI-Einstellungen, Standard an).</summary>
    bool ClaudeLooks => KiSourceSettings.Load(_core.DataRoot).ClaudeLooks;

    /// <summary>Ergebnis echt starten: Webseiten und Spiele im unsichtbaren Browser (Bildschirmfoto, Skriptfehler, fehlende Dateien), Python und JavaScript auf Syntax, JSON auf Gültigkeit.
    /// Trendo führt dabei kein Programm des Projekts aus – nur die Seite im abgeschotteten Browser und die Syntaxprüfung.</summary>
    async Task<ClaudeProbe.Result> ClaudeProbeRun(string folder, IReadOnlyList<string> files)
    {
        if (ProbeOverride is { } o) return await o(folder, files);
        var errors = ClaudeProbe.JsonErrors(folder, files).ToList();
        if (_opt.Isolated) return new(errors, null); // Testmodus startet keinen Browser und kein Programm
        string? shot = null;
        var web = files.Any(f => Path.GetExtension(f).ToLowerInvariant() is ".html" or ".htm" or ".js" or ".css") && File.Exists(Path.Combine(folder, "index.html"));
        if (web)
            try
            {
                var console = new List<string>();
                var png = Path.Combine(folder, ClaudeProbe.Shot.Replace('/', Path.DirectorySeparatorChar));
                var r = await WebsitePreview.CaptureAsync(folder, 1280, 800, png, null, default, console);
                if (r.Ok) shot = png;
                errors.AddRange(console.Take(15));
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception) { }
        foreach (var rel in files)
        {
            var full = Path.Combine(folder, rel.Replace('/', Path.DirectorySeparatorChar));
            var ext = Path.GetExtension(full).ToLowerInvariant();
            if (!File.Exists(full)) continue;
            (string Exe, string[] Args)? check = ext switch
            {
                ".py" when (Cli.Find("python") ?? Cli.Find("py")) is { } py => (py, ["-I", "-m", "py_compile", full]),
                ".js" or ".mjs" when Cli.Find("node") is { } node => (node, ["--check", full]),
                _ => null,
            };
            if (check is not { } c) continue;
            try
            {
                var psi = new ProcessStartInfo(c.Exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = folder };
                foreach (var a in c.Args) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi); if (p is null) continue;
                var err = p.StandardError.ReadToEndAsync(); _ = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(15000)) { try { p.Kill(true); } catch (InvalidOperationException) { } continue; }
                if (p.ExitCode != 0) errors.Add($"{rel}: " + string.Join(" ", (await err).Replace(folder, ".").Split('\n').Where(x => x.Trim().Length > 0).TakeLast(3)).Trim());
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        return new(errors, shot);
    }

    // ───────────── Freihändig sprechen ─────────────
    /// <summary>Für Tests: Vorlesen nachbilden (Text, der vorgelesen würde).</summary>
    public static Action<string>? SpeakOverride { get; set; }
    public bool ClaudeHandsFree() => ActiveChatUi()?.HandsFree ?? false;
    public Task ClaudeHandsFreeToggle() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? ClaudeHandsFreeToggle(d) : Task.CompletedTask;

    /// <summary>Freihändig: zuhören → Pause erkennen → abschicken → Antwort vorlesen → wieder zuhören. Ein Klick beendet es.</summary>
    async Task ClaudeHandsFreeToggle(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui)) return;
        ui.HandsFree = !ui.HandsFree;
        var btn = dock.Panel.GetLogicalDescendantsOf<Button>().FirstOrDefault(b => b.Name == "claude-tool-freihaendig");
        if (btn is not null) { btn.Content = ui.HandsFree ? "Freihändig aus" : "Freihändig"; btn.Background = ui.HandsFree ? ClaudeAccent : ClaudeItem; }
        _core.Audit.Append(Principal.LocalOwner.Name, ui.HandsFree ? "claude.freihaendig.an" : "claude.freihaendig.aus", ChatStudio(dock));
        if (!ui.HandsFree) { ui.HandsCts?.Cancel(); ui.Input.PlaceholderText = "Schreib Claude, was du bauen möchtest …"; return; }
        ui.HandsCts = new CancellationTokenSource(); var ct = ui.HandsCts.Token;
        while (ui.HandsFree && !ct.IsCancellationRequested)
        {
            ui.Input.PlaceholderText = "Ich höre zu – sprich einfach, nach einer kurzen Pause schicke ich es ab.";
            var heard = await ListenOnce(ct);
            if (ct.IsCancellationRequested || !ui.HandsFree) break;
            if (heard is null) { ui.HandsFree = false; if (btn is not null) { btn.Content = "Freihändig"; btn.Background = ClaudeItem; } break; } // kein Mikrofon o. Ä. – Hinweis steht im Eingabefeld
            if (heard.Trim().Length < 2) continue;
            await ClaudeChatSend(dock, heard);
            var answer = ClaudeChatCurrent()?.Messages.LastOrDefault(m => m.Role == "assistant")?.Text;
            if (answer is { Length: > 0 } && ui.HandsFree) await Speak(ChatMarkdown.Plain(ClaudeChats.SplitSuggestions(ClaudeTools.Strip(answer)).Text), ct);
        }
    }

    /// <summary>Einmal zuhören, bis nach dem Sprechen eine Pause kommt (höchstens 60 Sekunden), dann auf dem PC in Text umwandeln. null = geht hier nicht.</summary>
    async Task<string?> ListenOnce(CancellationToken ct)
    {
        if (DictateOverride is { } o) return await o();
        if (_opt.Isolated) return null;
        if (FfmpegPath is not { } ff || DesktopCapture.Devices(ff).Microphones.FirstOrDefault() is not { } mic) { if (ActiveChatUi() is { } u) u.Input.PlaceholderText = "Freihändig braucht ein Mikrofon und FFmpeg (unter „Programme“ einrichten)."; return null; }
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-freihaendig-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tmp);
        var wav = Path.Combine(tmp, "diktat.wav");
        var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-hide_banner", "-y", "-f", "dshow", "-i", "audio=" + mic.Id, "-af", "silencedetect=noise=-35dB:d=1.4", "-t", "60", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", wav }) psi.ArgumentList.Add(a);
        var spoke = false; var stop = new TaskCompletionSource();
        using var p = Process.Start(psi);
        if (p is null) return null;
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not { } l) return;
            if (l.Contains("silence_end")) spoke = true; // nach Stille kommt Sprache
            else if (l.Contains("silence_start") && spoke) stop.TrySetResult(); // nach dem Sprechen wieder Stille → fertig
        };
        p.BeginErrorReadLine();
        using (ct.Register(() => stop.TrySetResult())) await Task.WhenAny(stop.Task, p.WaitForExitAsync(CancellationToken.None));
        try { if (!p.HasExited) { p.StandardInput.Write('q'); p.StandardInput.Flush(); await p.WaitForExitAsync(new CancellationTokenSource(5000).Token); } }
        catch (Exception e) when (e is IOException or InvalidOperationException or OperationCanceledException) { try { p.Kill(); } catch (InvalidOperationException) { } }
        string? text = "";
        if (!ct.IsCancellationRequested && spoke && File.Exists(wav))
        {
            var (ok, _) = await KiToolRun("mitschreiben", "diktat.wav", tmp, "diktat.txt");
            var txt = Path.Combine(tmp, "diktat.txt"); if (ok && File.Exists(txt)) text = File.ReadAllText(txt).Trim();
        }
        try { Directory.Delete(tmp, true); } catch (IOException) { }
        return text;
    }

    /// <summary>Antwort auf dem PC vorlesen (Piper) und abwarten, bis sie zu Ende ist.</summary>
    async Task Speak(string text, CancellationToken ct)
    {
        if (text.Length > 1200) text = text[..1200];
        if (SpeakOverride is { } o) { o(text); return; }
        if (_opt.Isolated || text.Trim().Length == 0) return;
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-vorlesen-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tmp);
        try
        {
            var (ok, _) = await KiToolRun("vorlesen", text, tmp, "antwort.wav");
            var wav = Path.Combine(tmp, "antwort.wav");
            if (!ok || !File.Exists(wav) || ct.IsCancellationRequested) return;
            if (OperatingSystem.IsWindows())
            {
                await Task.Run(() => PlaySound(wav, IntPtr.Zero, 0x00020000 /* SND_FILENAME, synchron */), ct);
            }
            else Play(wav);
        }
        catch (OperationCanceledException) { }
        finally { try { Directory.Delete(tmp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern bool PlaySound(string file, IntPtr module, uint flags);
}
