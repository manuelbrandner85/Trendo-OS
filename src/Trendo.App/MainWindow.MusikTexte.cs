using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik V6 wie Suno (07.11.2026): Liedtext-Editor im Vollbild – Abschnitts-Etiketten an der Schreibmarke, Zeilen/Wörter/Abschnitte,
/// „Texter“ (eigene Beispieltexte als Schreibstil), Wunsch in eigenen Worten für eine Zeile oder den ganzen Text, drei Varianten einer Zeile, Reime auf ein Wort.
/// Der Text bleibt beim Wechseln erhalten (automatisch gespeichert im Erstellen-Feld).</summary>
public partial class MainWindow
{
    bool _msLyricsFull; string _msLyricistId = "", _msWish = "", _msLyricistName = ""; int _msLyCaret; List<string> _msLineVariants = []; int _msVariantLine = -1;
    public Lyricists LyricistStore => new(_core.DataRoot);
    public IReadOnlyList<string> MsLineVariants => _msLineVariants;
    public string MsLyricsText { get => _msLyrics; set => _msLyrics = value; }
    public void MsLyricsFullscreen(bool on) { _msLyricsFull = on; if (on) _msPro = true; if (CurrentView == MS) Show(MS); }

    Lyricist? CurrentLyricist() => _msLyricistId.Length == 0 ? null : LyricistStore.Get(_msLyricistId);

    async Task<string?> AskLyrics(string prompt)
    {
        try
        {
            return MusicLyricsOverride is { } o ? await o(prompt)
                : await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException) { return null; }
    }

    /// <summary>Eine Zeile nach Wunsch umschreiben lassen.</summary>
    public async Task<bool> MsLyricsLine(int line, string wish)
    {
        if (wish.Trim().Length == 0) { _msMsg = "Schreib kurz, was an der Zeile anders werden soll – z. B. „lustiger“ oder „mehr Bilder“."; Show(MS); return false; }
        try
        {
            var prompt = LyricsEditor.LinePrompt(_msLyrics, line, wish, CurrentLyricist());
            _msMsg = "Die Zeile wird umgeschrieben …"; Show(MS);
            var ans = await AskLyrics(prompt) ?? throw new InvalidOperationException("Die Text-KI hat gerade nichts geliefert.");
            var before = LyricsEditor.Lines(_msLyrics)[line];
            _msLyrics = LyricsEditor.ReplaceLine(_msLyrics, line, LyricsEditor.CleanLine(ans));
            _msMsg = $"Zeile {line + 1} geändert („{before}“ → „{LyricsEditor.Lines(_msLyrics)[line]}“).";
            return true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { _msMsg = e.Message; return false; }
        finally { Show(MS); }
    }

    /// <summary>Drei Varianten einer Zeile zur Auswahl holen.</summary>
    public async Task<int> MsLyricsVariants(int line)
    {
        try
        {
            var l = LyricsEditor.Lines(_msLyrics);
            if (line < 0 || line >= l.Length || l[line].Trim().Length == 0 || l[line].TrimStart().StartsWith('[')) throw new ArgumentException("Setz die Schreibmarke in eine Textzeile.");
            _msMsg = "Varianten entstehen …"; Show(MS);
            var ans = await AskLyrics(LyricsEditor.VariantsPrompt(_msLyrics, line, CurrentLyricist())) ?? throw new InvalidOperationException("Die Text-KI hat gerade nichts geliefert.");
            _msLineVariants = LyricsEditor.ParseVariants(ans); _msVariantLine = line;
            _msMsg = _msLineVariants.Count == 0 ? "Keine brauchbaren Varianten – bitte noch einmal." : $"{_msLineVariants.Count} Varianten für Zeile {line + 1} – eine anklicken übernimmt sie.";
            return _msLineVariants.Count;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { _msMsg = e.Message; return 0; }
        finally { Show(MS); }
    }

    public void MsLyricsPickVariant(int i)
    {
        if (i < 0 || i >= _msLineVariants.Count || _msVariantLine < 0) return;
        _msLyrics = LyricsEditor.ReplaceLine(_msLyrics, _msVariantLine, _msLineVariants[i]);
        _msMsg = $"Variante in Zeile {_msVariantLine + 1} übernommen."; _msLineVariants = []; _msVariantLine = -1; Show(MS);
    }

    /// <summary>Den ganzen Text nach Wunsch überarbeiten (Aufbau bleibt, KI-Floskeln werden danach geprüft).</summary>
    public async Task<bool> MsLyricsWhole(string wish)
    {
        if (wish.Trim().Length == 0 || _msLyrics.Trim().Length == 0) { _msMsg = "Erst Text und einen Wunsch eingeben."; Show(MS); return false; }
        _msMsg = "Der Text wird überarbeitet …"; Show(MS);
        var ans = await AskLyrics(LyricsEditor.WholePrompt(_msLyrics, wish, CurrentLyricist()));
        if (ans is not { Length: > 20 } || !ans.Contains('[')) { _msMsg = "Die Text-KI hat keinen vollständigen Liedtext geliefert – nichts geändert."; Show(MS); return false; }
        var (polished, left) = await MsPolishLyrics(ans.Trim().Trim('`').Trim());
        _msLyrics = polished; _msMsg = "✓ Text überarbeitet – Strg+Z im Feld macht es rückgängig." + FloskelHint(left); Show(MS);
        return true;
    }

    /// <summary>Knopf unter dem Liedtext (Erweitert): Vollbild-Editor öffnen.</summary>
    Control MsLyricsFullButton() => Act("ms-lyrics-full", "⛶ Vollbild-Editor", () => MsLyricsFullscreen(true));

    /// <summary>V6: Vollbild-Editor (Spalten 2–6).</summary>
    Control MsLyricsFullEditor()
    {
        var (lines, words, sections) = LyricsEditor.Stats(_msLyrics);
        var box = new TextBox { Name = "ms-lyrics-big", Text = _msLyrics, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 15, MinHeight = 420, CaretIndex = Math.Min(_msLyCaret, _msLyrics.Length),
            Watermark = "[Strophe]\nDein Text …\n[Refrain]\n…" };
        box.TextChanged += (_, _) => _msLyrics = box.Text ?? "";
        box.PropertyChanged += (_, e) => { if (e.Property == TextBox.CaretIndexProperty) _msLyCaret = box.CaretIndex; };
        int CurLine() { _msLyrics = box.Text ?? ""; _msLyCaret = box.CaretIndex; return LyricsEditor.LineAt(_msLyrics, box.CaretIndex); }
        var tags = new WrapPanel();
        foreach (var (label, _) in SongLibrary.Sections)
        {
            var l = label; var b = new Button { Name = "ms-lyf-tag-" + l, Content = "[" + l + "]", FontSize = 11.5, Padding = new Thickness(7, 2), Margin = new Thickness(0, 0, 4, 4), Background = Brush.Parse("#18FFFFFF") };
            b.Click += (_, _) => { var (t, c) = LyricsEditor.InsertSection(box.Text ?? "", box.CaretIndex, l); _msLyrics = t; _msLyCaret = c; Show(MS); };
            tags.Children.Add(b);
        }
        var head = new DockPanel { Children = {
            Dk(Act("ms-lyrics-full-close", "✕ Schließen", () => MsLyricsFullscreen(false)), Avalonia.Controls.Dock.Right),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { H("Liedtext", 16), new TextBlock { Name = "ms-lyf-stats", Text = $"{lines} Zeilen · {words} Wörter · {sections} Abschnitte · wird automatisch übernommen", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } } } };
        var left = new DockPanel { Children = { Dk(head, Avalonia.Controls.Dock.Top), Dk(tags, Avalonia.Controls.Dock.Top), box } };

        // Werkzeuge rechts
        var tools = new StackPanel { Spacing = 8, Width = 330 };
        var store = LyricistStore; var all = store.All();
        tools.Children.Add(H("Texter", 13));
        var who = new ComboBox { Name = "ms-lyricist", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "ohne Texter-Stil" }.Concat(all.Select(x => $"{x.Name} ({x.Examples.Count} Beispiele)")).ToList(),
            SelectedIndex = Math.Max(0, all.FindIndex(x => x.Id == _msLyricistId) + 1) };
        who.SelectionChanged += (_, _) => _msLyricistId = who.SelectedIndex <= 0 ? "" : all[who.SelectedIndex - 1].Id;
        tools.Children.Add(who);
        var tname = new TextBox { Name = "ms-lyricist-name", Text = _msLyricistName, Watermark = "Name, z. B. „Meine Balladen“", FontSize = 12 };
        tname.TextChanged += (_, _) => _msLyricistName = tname.Text ?? "";
        tools.Children.Add(tname);
        tools.Children.Add(Act("ms-lyricist-save", "Diesen Text als Beispiel speichern", () =>
        {
            try { var t = store.AddExample(tname.Text ?? "", box.Text ?? ""); _msLyricistId = t.Id; _msMsg = $"Texter „{t.Name}“: {t.Examples.Count} Beispiel(e) – neue Texte klingen ähnlich."; }
            catch (ArgumentException e) { _msMsg = e.Message; }
            Show(MS);
        }));
        tools.Children.Add(P("Nur eigene Texte als Beispiel – keine Liedtexte anderer.", Muted, 10.5));
        tools.Children.Add(H("Ändern mit KI", 13));
        var wish = new TextBox { Name = "ms-ly-wish", Text = _msWish, Watermark = "Wunsch, z. B. lustiger · mehr Bilder · Reimschema AABB", FontSize = 12, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44 };
        wish.TextChanged += (_, _) => _msWish = wish.Text ?? "";
        tools.Children.Add(wish);
        tools.Children.Add(new WrapPanel { Children = {
            Act("ms-ly-line", "Zeile an der Marke", () => { var l = CurLine(); _ = MsLyricsLine(l, wish.Text ?? ""); }),
            new Border { Width = 4 },
            Act("ms-ly-whole", "Ganzen Text", () => { _msLyrics = box.Text ?? ""; _ = MsLyricsWhole(wish.Text ?? ""); }) } });
        tools.Children.Add(Act("ms-ly-variants", "3 Varianten der Zeile", () => { var l = CurLine(); _ = MsLyricsVariants(l); }));
        for (var i = 0; i < _msLineVariants.Count; i++)
        {
            var k = i; var b = new Button { Name = $"ms-ly-pick-{i}", Content = new TextBlock { Text = _msLineVariants[i], TextWrapping = TextWrapping.Wrap, FontSize = 12 }, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brush.Parse("#18C9A227") };
            b.Click += (_, _) => MsLyricsPickVariant(k);
            tools.Children.Add(b);
        }
        tools.Children.Add(H("Reime", 13));
        tools.Children.Add(Act("ms-ly-rhyme", "Reime auf das markierte Wort", () =>
        {
            var sel = (box.SelectedText ?? "").Trim();
            if (sel.Length == 0) { var t = box.Text ?? ""; var c = Math.Clamp(box.CaretIndex, 0, t.Length); var s0 = t.LastIndexOfAny([' ', '\n'], Math.Max(0, c - 1)) + 1; var e0 = t.IndexOfAny([' ', '\n', ',', '.'], c); sel = t[s0..(e0 < 0 ? t.Length : e0)]; }
            _msRhymeWord = sel.Trim(',', '.', '!', '?'); _msRhymes = MsRhymes(_msRhymeWord);
            _msMsg = _msRhymes.Count == 0 ? $"Keine Reime auf „{_msRhymeWord}“ gefunden." : $"Reime auf „{_msRhymeWord}“: {string.Join(" · ", _msRhymes.Take(14))}";
            Show(MS);
        }));
        if (_msMsg.Length > 0) tools.Children.Add(new TextBlock { Name = "ms-msg", Text = _msMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,Auto") };
        Grid.SetColumn(left, 0); grid.Children.Add(left);
        var sv = new ScrollViewer { Content = tools }; Grid.SetColumn(sv, 2); grid.Children.Add(sv);
        return MsCard("ms-lyrics-full-card", grid);
    }
}
