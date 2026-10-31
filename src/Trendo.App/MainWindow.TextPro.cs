using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Office;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0230: Menüband von Trendo Text – Einfügen, Entwurf, Layout, Referenzen, Sendungen, Überprüfen, Ansicht, KI (W1–W9).</summary>
public partial class MainWindow
{
    private string _txRibbon = "start";
    private bool _txNav, _txComments, _txAccess, _txMerge;
    private double _txZoom = 1;
    private (ParaStyle Style, Fmt Fmt, double? Size)? _txPainter;
    private (List<string> Headers, List<List<string>> Rows, string File)? _txData;
    private Dictionary<(int, int), int> _txFootNos = [], _txEndNos = [];
    private TextBox? _txPromptBox; private (string Name, string Label, string Initial, Action<string> Ok)? _txPrompt;

    public string TextRibbon { get => _txRibbon; set => _txRibbon = value; }

    // ---------- Öffentliche Wege (auch für Tests und die KI) ----------
    /// <summary>W2/W5: Markierung an der Auswahl (Link, Kommentar) bzw. am Cursor (Fuß-/Endnote).</summary>
    public bool TextAddMark(MarkKind kind, string value)
    {
        if (CurPara() is not { } p || value.Trim().Length == 0) { _txMsg = "Erst Text markieren bzw. den Cursor setzen."; return false; }
        if (kind is MarkKind.Link or MarkKind.Comment && _txSelLen == 0) { _txMsg = kind == MarkKind.Link ? "Erst den Text markieren, der zum Link werden soll." : "Erst die Stelle markieren, zu der der Kommentar gehört."; return false; }
        Snapshot(); TextDoc.AddMark(p, _txSel, _txSelLen, kind, value.Trim()); Changed();
        _txMsg = kind switch { MarkKind.Link => "Link eingefügt.", MarkKind.Comment => "Kommentar eingefügt.", MarkKind.Footnote => "Fußnote eingefügt – sie steht unten auf der Seite.", _ => "Endnote eingefügt – sie steht am Ende des Dokuments." };
        return true;
    }

    public void TextInsertToc() { InsertBlock(new TocBlock()); _txMsg = "Inhaltsverzeichnis eingefügt – es entsteht aus den Überschriften 1–3."; }

    public void TextInsertSymbol(string s)
    {
        if (CurPara() is not { } p) return;
        Snapshot(); var at = Math.Clamp(_txSel, 0, p.Text.Length); TextDoc.ReplaceAt(p, at, _txSelLen, s); _txSel = at + s.Length; _txSelLen = 0; Changed();
    }

    public void TextSetPage(Action<PageSetup> change) { Snapshot(); change(TextDocument.Page); TextDirty = true; }

    /// <summary>W1: Format übertragen – erster Aufruf merkt sich Absatzart und Zeichenformat, zweiter überträgt sie auf die Auswahl.</summary>
    public void TextFormatPainter()
    {
        if (CurPara() is not { } p) return;
        if (_txPainter is null) { var (f, s) = p.Text.Length == 0 ? (Fmt.None, null) : p.At(Math.Clamp(_txSel, 0, p.Text.Length - 1)); _txPainter = (p.Style, f, s); _txMsg = "Format gemerkt – jetzt Ziel markieren und erneut „Format übertragen“."; return; }
        var (st, fm, sz) = _txPainter.Value; _txPainter = null;
        Snapshot(); p.Style = st;
        if (_txSelLen > 0)
        {
            foreach (var flag in new[] { Fmt.Bold, Fmt.Italic, Fmt.Underline, Fmt.Strike, Fmt.Super, Fmt.Sub }) TextDoc.Format(p, _txSel, _txSelLen, flag, fm.HasFlag(flag));
            TextDoc.Size(p, _txSel, _txSelLen, sz);
        }
        Changed(); _txMsg = "Format übertragen.";
    }

    /// <summary>W7: mit einer älteren Fassung vergleichen – Unterschiede erscheinen als Änderungen zum Annehmen/Ablehnen.</summary>
    public void TextCompareWith(string olderFile)
    {
        var older = TextFiles.Open(olderFile);
        Snapshot(); TextDocument = TextTools.Compare(older, TextDocument); TextFocusBlock = 0; TextDirty = true;
        _txMsg = TextTools.HasChanges(TextDocument) ? "Unterschiede markiert: grün = neu, rot = entfernt. Unter „Überprüfen“ annehmen oder ablehnen." : "Keine Unterschiede gefunden.";
    }

    public void TextResolveChanges(bool accept) { Snapshot(); var n = TextTools.Resolve(TextDocument, accept); TextFocusBlock = Math.Min(TextFocusBlock, TextDocument.Blocks.Count - 1); Changed(); _txMsg = $"{n} Änderung(en) {(accept ? "angenommen" : "abgelehnt")}."; }

    /// <summary>W6: Datenquelle für den Serienbrief (CSV, XLSX, ODS) – erste Zeile sind die Feldnamen.</summary>
    public void TextMergeSource(string file)
    {
        var d = SheetFiles.Open(file); var s = d.Sheets[0]; var calc = new SheetCalc(d);
        var cols = Math.Max(0, s.MaxCol) + 1;
        var headers = Enumerable.Range(0, cols).Select(c => calc.Display(s, 0, c).Trim()).ToList();
        var rows = Enumerable.Range(1, Math.Max(0, s.MaxRow)).Select(r => Enumerable.Range(0, cols).Select(c => calc.Display(s, r, c)).ToList()).Where(r => r.Any(x => x.Trim().Length > 0)).ToList();
        _txData = (headers, rows, file); _txMerge = true;
        _txMsg = $"{rows.Count} Datensätze aus „{Path.GetFileName(file)}“. Felder mit « » einfügen, dann „Serienbrief erstellen“.";
    }

    public string? TextMergeCreate()
    {
        if (_txData is not { } data) { _txMsg = "Erst eine Datenquelle wählen (Tabelle mit Feldnamen in Zeile 1)."; return null; }
        var merged = TextTools.MailMerge(TextDocument, data.Headers, data.Rows);
        var dir = Path.Combine(ProjectsRoot, "Dokumente", "Serienbriefe"); Directory.CreateDirectory(dir);
        var name = Path.GetFileNameWithoutExtension(TextPath ?? "Serienbrief");
        var docx = Path.Combine(dir, $"{name} – Serienbrief.docx"); TextFiles.Save(merged, docx); TextPdf.Export(merged, Path.ChangeExtension(docx, ".pdf"));
        _txMsg = $"Serienbrief mit {data.Rows.Count} Briefen gespeichert: {Path.GetFileName(docx)} (und als PDF).";
        _core.Audit.Append(Principal.LocalOwner.Name, "text.serienbrief", $"{data.Rows.Count}");
        Toast(_txMsg, ("Ordner öffnen", new Action(() => Launch(dir))));
        return docx;
    }

    IEnumerable<string> MergeAddresses()
    {
        if (_txData is not { } d) yield break;
        int Col(params string[] names) => d.Headers.FindIndex(h => names.Any(n => h.Equals(n, StringComparison.CurrentCultureIgnoreCase)));
        int vn = Col("Vorname"), nn = Col("Nachname", "Name"), str = Col("Straße", "Strasse", "Adresse"), plz = Col("PLZ", "Postleitzahl"), ort = Col("Ort", "Stadt"), firma = Col("Firma");
        foreach (var r in d.Rows)
        {
            string V(int i) => i >= 0 && i < r.Count ? r[i].Trim() : "";
            if (nn < 0 && str < 0) { yield return string.Join("\n", r.Where(x => x.Trim().Length > 0)); continue; }
            yield return string.Join("\n", new[] { V(firma), $"{V(vn)} {V(nn)}".Trim(), V(str), $"{V(plz)} {V(ort)}".Trim() }.Where(x => x.Length > 0));
        }
    }

    public string? TextLabels()
    {
        if (_txData is null) { _txMsg = "Erst eine Datenquelle mit Adressen wählen."; return null; }
        var f = DefaultTextName(" – Etiketten.pdf"); var pages = TextTools.Labels(MergeAddresses(), f);
        _txMsg = $"Etiketten gespeichert: {Path.GetFileName(f)} ({pages} Seite{(pages == 1 ? "" : "n")}, 3 × 8 je Blatt, 70 × 37 mm)."; return f;
    }

    public string? TextEnvelopes()
    {
        if (_txData is null) { _txMsg = "Erst eine Datenquelle mit Adressen wählen."; return null; }
        var sender = string.Join("\n", new[] { _settings.Name, _settings.SenderStreet, _settings.SenderCity }.Where(x => x.Trim().Length > 0));
        var f = DefaultTextName(" – Umschläge.pdf"); var n = TextTools.Envelopes(sender, MergeAddresses(), f);
        _txMsg = $"Umschläge (DIN lang) gespeichert: {Path.GetFileName(f)} ({n} Stück)."; return f;
    }

    /// <summary>108-49 (Entscheidung 31.10.2026): Vorschlag der KI – erst zeigen, dann auf Wunsch übernehmen. Absatz als Index, weil „Zurück“ das Dokument austauscht.</summary>
    private (int Block, int Start, int Len, string Old, string New)? _txAiProp;
    public bool TextAiHasProposal => _txAiProp is not null;

    /// <summary>Vorschlag übernehmen (nur wenn der Text an der Stelle noch derselbe ist) oder verwerfen.</summary>
    public void TextAiDecide(bool accept)
    {
        if (_txAiProp is not { } pr) return;
        _txAiProp = null;
        if (!accept) { _txMsg = "Vorschlag verworfen – dein Text bleibt unverändert."; return; }
        if (pr.Block >= TextDocument.Blocks.Count || TextDocument.Blocks[pr.Block] is not Para p || pr.Start + pr.Len > p.Text.Length || p.Text.Substring(pr.Start, pr.Len) != pr.Old)
        { _txMsg = "Der Text hat sich inzwischen geändert – Vorschlag nicht übernommen."; return; }
        Snapshot(); TextDoc.ReplaceAt(p, pr.Start, pr.Len, pr.New); TextFocusBlock = pr.Block; _txSel = pr.Start; _txSelLen = pr.New.Length; Changed();
        _txMsg = "Vorschlag übernommen – mit „Zurück“ rückgängig machen.";
    }

    /// <summary>Vorschlag mit markierten Änderungen (entfernt durchgestrichen, neu hervorgehoben) und „Übernehmen“/„Verwerfen“.</summary>
    private Control? TextAiProposalCard()
    {
        if (_txAiProp is not { } pr) return null;
        var tb = new SelectableTextBlock { Name = "text-ai-proposal", TextWrapping = TextWrapping.Wrap, Foreground = Text, FontSize = 13 };
        foreach (var (t, k) in TextTools.DiffWords(pr.Old, pr.New))
        {
            var run = new Avalonia.Controls.Documents.Run(t);
            if (k < 0) { run.Foreground = Brush.Parse("#F87171"); run.TextDecorations = TextDecorations.Strikethrough; }
            else if (k > 0) { run.Foreground = Brush.Parse("#4ADE80"); run.FontWeight = FontWeight.SemiBold; }
            tb.Inlines!.Add(run);
        }
        var accept = new Button { Name = "text-ai-accept", Content = "Übernehmen", Background = Accent, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 0), Focusable = false };
        accept.Click += (_, _) => { TextAiDecide(true); Show("text"); };
        var reject = new Button { Name = "text-ai-reject", Content = "Verwerfen", Padding = new Thickness(12, 4), Focusable = false };
        reject.Click += (_, _) => { TextAiDecide(false); Show("text"); };
        return new Border { Name = "text-ai-card", Background = Brush.Parse("#1A2F86F6"), BorderBrush = Brush.Parse("#552F86F6"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Margin = new Thickness(0, 4, 0, 4),
            Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = "✨ Vorschlag der KI – noch nichts geändert", Foreground = Text, FontWeight = FontWeight.SemiBold },
                tb, new StackPanel { Orientation = Orientation.Horizontal, Children = { accept, reject } } } } };
    }

    /// <summary>W9: Absatz bzw. Auswahl von der KI umschreiben lassen (Umschreiben, Kürzen, Freundlicher, Förmlicher) – als Vorschlag.</summary>
    public async Task TextAiRewrite(string how)
    {
        if (CurPara() is not { } p || p.Text.Trim().Length == 0) { _txMsg = "Erst in einen Absatz mit Text klicken."; return; }
        // Markierung kann nach „Zurück“ über das Absatzende zeigen – dann den ganzen Absatz nehmen
        var (start, len) = _txSelLen > 0 && _txSel >= 0 && _txSel + _txSelLen <= p.Text.Length ? (_txSel, _txSelLen) : (0, p.Text.Length);
        var src = p.Text.Substring(start, len);
        var task = how switch
        {
            "kuerzen" => "Kürze diesen Text deutlich, ohne wichtige Inhalte zu verlieren.",
            "freundlich" => "Formuliere diesen Text freundlicher und wärmer.",
            "foermlich" => "Formuliere diesen Text förmlicher und sachlicher.",
            _ => "Formuliere diesen Text klarer und besser lesbar um.",
        };
        _txMsg = "Die KI schreibt …"; Show("text");
        try
        {
            using var kiLabel = Trendo.Core.KiActivityHub.Label("Text bearbeiten"); var a = await Intel.AskAsync(new KiRequest($"{task} Antworte nur mit dem neuen Text, auf Deutsch, ohne Anführungszeichen:\n\n{src}", PreferredModel: KiModelName, UseKnowledge: false));
            var neu = (a.Text ?? "").Trim().Trim('"', '„', '“');
            if (neu.Length == 0) { _txMsg = "Die KI hat nichts geliefert. Ist ein Sprachmodell in der KI-Bibliothek geladen?"; }
            else if (neu == src) _txMsg = "Die KI hat nichts zu verbessern gefunden.";
            else { _txAiProp = (TextFocusBlock, start, len, src, neu); _txMsg = "Vorschlag der KI – erst prüfen, dann „Übernehmen“ oder „Verwerfen“."; }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException) { _txMsg = "Die KI ist gerade nicht erreichbar: " + ex.Message; }
        Show("text");
    }

    /// <summary>W9: Vorlesen mit Piper (Stimme aus der KI-Bibliothek), Ergebnis als WAV.</summary>
    public async Task<string?> TextReadAloud()
    {
        var model = KiLibrary.Models.FirstOrDefault(m => m.Kind == KiKind.TextZuSprache && KiStore.ModelReady(m));
        if (model is null || !KiStore.RuntimeReady(KiRuntime.Piper)) { _txMsg = "Zum Vorlesen eine Stimme in der KI-Bibliothek laden (Bereich „Text zu Sprache“)."; Show("text"); return null; }
        var text = _txSelLen > 0 && CurPara() is { } p ? p.Text.Substring(_txSel, _txSelLen) : TextDocument.PlainText();
        if (text.Trim().Length == 0) return null;
        var o = Path.Combine(KiOutFolder, $"vorlesen-{DateTime.Now:yyyyMMdd-HHmmss}.wav");
        _txMsg = "Die Stimme liest ein …"; Show("text");
        var r = await Task.Run(() => RunWithInput(KiStore.RuntimeExe(KiRuntime.Piper), ["--model", KiStore.ModelFile(model, model.Files[0]), "--output_file", o], text));
        if (!r.Ok || !File.Exists(o)) { _txMsg = "Vorlesen ging nicht: " + Short(r.Err); Show("text"); return null; }
        _txMsg = "Vorlesen bereit."; Launch(o); Show("text"); return o;
    }

    /// <summary>W9: Mitschreiben einer Audiodatei mit Whisper (offline) – der Text wird an der Cursorstelle eingefügt.</summary>
    public async Task TextTranscribe()
    {
        var model = KiLibrary.Models.FirstOrDefault(m => m.Kind == KiKind.SpracheZuText && KiStore.ModelReady(m));
        if (model is null || !KiStore.RuntimeReady(KiRuntime.WhisperCpp)) { _txMsg = "Zum Mitschreiben ein Whisper-Modell in der KI-Bibliothek laden (Bereich „Sprache zu Text“)."; Show("text"); return; }
        var src = await PickMedia("text-audio", ["*.wav", "*.mp3", "*.m4a", "*.ogg"]);
        if (src is null) return;
        var wav = src;
        if (!src.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            if (FfmpegPath is not { } ff) { _txMsg = "Für dieses Format braucht Trendo FFmpeg (unter „Programme“ einrichten)."; Show("text"); return; }
            wav = Path.Combine(Path.GetTempPath(), "trendo-diktat-" + Guid.NewGuid().ToString("N") + ".wav");
            var c = await Task.Run(() => Cli.Run(ff, ["-y", "-i", src, "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav], timeoutMs: 600_000));
            if (!c.Ok) { _txMsg = "Ton konnte nicht vorbereitet werden: " + Short(c.Err); Show("text"); return; }
        }
        _txMsg = "Wird mitgeschrieben …"; Show("text");
        var o = Path.Combine(Path.GetTempPath(), "trendo-diktat-" + Guid.NewGuid().ToString("N"));
        var r = await Task.Run(() => RunWithInput(KiStore.RuntimeExe(KiRuntime.WhisperCpp), ["-m", KiStore.ModelFile(model, model.Files[0]), "-f", wav, "-l", "de", "-otxt", "-of", o], null));
        if (!ReferenceEquals(wav, src)) TryDelete(wav);
        if (!r.Ok || !File.Exists(o + ".txt")) { _txMsg = "Mitschreiben ging nicht: " + Short(r.Err); Show("text"); return; }
        var text = string.Join(" ", File.ReadAllLines(o + ".txt").Select(l => l.Trim()).Where(l => l.Length > 0)); TryDelete(o + ".txt");
        Snapshot(); TextDocument.Blocks.Insert(Math.Min(TextFocusBlock + 1, TextDocument.Blocks.Count), new Para(text)); TextFocusBlock++; Changed();
        _txMsg = "Mitgeschrieben und eingefügt."; Show("text");
    }

    // ---------- Menüband ----------
    Control TextRibbonTabs()
    {
        var row = new StackPanel { Name = "text-ribbon-tabs", Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var (id, label) in new[] { ("start", "Start"), ("einfuegen", "Einfügen"), ("entwurf", "Entwurf"), ("layout", "Layout"), ("referenzen", "Referenzen"), ("sendungen", "Sendungen"), ("pruefen", "Überprüfen"), ("ansicht", "Ansicht"), ("ki", "KI") })
        {
            var key = id;
            var b = new Button { Name = "text-rib-" + id, Content = label, Focusable = false, Padding = new Thickness(12, 4), Background = _txRibbon == id ? Brush.Parse("#332F86F6") : Brushes.Transparent, FontWeight = _txRibbon == id ? FontWeight.SemiBold : FontWeight.Normal };
            b.Click += (_, _) => { _txRibbon = key; Show("text"); };
            row.Children.Add(b);
        }
        return row;
    }

    Button XB(string name, string icon, string label, Action go, string tip, bool on = false) => SB(name, icon, label, go, tip, on, "text");

    Control MenuButton(string name, string icon, string label, string tip, params (string Header, Action Run)[] items)
    {
        var b = XB(name, icon, label, () => { }, tip);
        var menu = new ContextMenu(); var i = 0;
        foreach (var (h, run) in items) { var r = run; var mi = new MenuItem { Name = $"{name}-{i++}", Header = h }; mi.Click += (_, _) => { r(); Show("text"); }; menu.Items.Add(mi); }
        b.ContextMenu = menu; b.Click += (_, _) => menu.Open(b);
        return b;
    }

    /// <summary>Kleines Eingabefeld unter dem Menüband (für Link, Kommentar, Fußnote, Wasserzeichen …).</summary>
    void TextAsk(string name, string label, string initial, Action<string> ok)
    {
        // Auswahl jetzt merken: das Eingabefeld nimmt dem Absatz gleich den Fokus
        var (block, sel, len) = (TextFocusBlock, _txSel, _txSelLen);
        _txPrompt = (name, label, initial, v => { TextFocusBlock = block; _txSel = sel; _txSelLen = len; ok(v); });
        Show("text");
    }

    Control? TextPromptBar()
    {
        if (_txPrompt is not { } pr) return null;
        var tb = new TextBox { Name = pr.Name, Text = pr.Initial, Width = 420 };
        _txPromptBox = tb;
        var ok = new Button { Name = pr.Name + "-ok", Content = "OK", Background = Accent, Focusable = false };
        var cancel = new Button { Content = "Abbrechen", Focusable = false };
        void Done() { var v = tb.Text ?? ""; _txPrompt = null; pr.Ok(v); Show("text"); }
        ok.Click += (_, _) => Done(); cancel.Click += (_, _) => { _txPrompt = null; Show("text"); };
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Done(); e.Handled = true; } else if (e.Key == Key.Escape) { _txPrompt = null; Show("text"); e.Handled = true; } };
        Dispatcher.UIThread.Post(() => { tb.Focus(); tb.SelectAll(); });
        return new Border { Name = "text-prompt-bar", Background = Brush.Parse("#222F86F6"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 0, 6),
            Child = new WrapPanel { Children = { new TextBlock { Text = pr.Label, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), FontSize = 13 }, tb, new Border { Width = 6 }, ok, new Border { Width = 4 }, cancel } } };
    }

    /// <summary>W1: Formatvorlagen-Galerie mit Vorschau.</summary>
    Control StyleGallery()
    {
        var row = new StackPanel { Name = "text-style-gallery", Orientation = Orientation.Horizontal, Spacing = 4 };
        var cur = CurPara()?.Style;
        foreach (var s in Enum.GetValues<ParaStyle>())
        {
            var st = s;
            var preview = new TextBlock { Text = TextDoc.StyleName(s), FontSize = Math.Min(17, 9 + TextDoc.StyleSize(s) * 0.35), FontWeight = s is ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = s == ParaStyle.Quote ? FontStyle.Italic : FontStyle.Normal, Foreground = s is ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 ? Brush.Parse(TextDocument.Page.HeadingColor) : s == ParaStyle.Subtitle ? Brush.Parse("#595959") : Brushes.Black };
            var b = new Button { Name = "text-gallery-" + s.ToString().ToLowerInvariant(), Focusable = false, Width = 104, Height = 44, Padding = new Thickness(6, 2), Background = Brushes.White, BorderThickness = new Thickness(cur == s ? 2 : 1), BorderBrush = cur == s ? Accent : Brush.Parse("#FFD8DEE8"), Content = preview, HorizontalContentAlignment = HorizontalAlignment.Left };
            ToolTip.SetTip(b, $"Absatzart „{TextDoc.StyleName(s)}“");
            b.Click += (_, _) => { if (CurPara() is { } p) { Snapshot(); p.Style = st; Changed(); } Show("text"); };
            row.Children.Add(b);
        }
        row.Children.Add(XB("text-painter", "🖌", _txPainter is null ? "Format übertragen" : "Format einfügen", TextFormatPainter, "Format übertragen: erst Quelle, dann Ziel", _txPainter is not null));
        return new ScrollViewer { Content = row, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 4) };
    }

    Control TextRibbonBody()
    {
        var g = new WrapPanel { Name = "text-ribbon-body" };
        var pg = TextDocument.Page; var p = CurPara();
        switch (_txRibbon)
        {
            case "einfuegen":
                g.Children.Add(RibbonGroup("Verweise",
                    XB("text-link", "🔗", "Link", () => TextAsk("text-prompt", "Adresse für den markierten Text (z. B. https://…):", "https://", v => TextAddMark(MarkKind.Link, v)), "Markierten Text verlinken"),
                    XB("text-comment", "💬", "Kommentar", () => TextAsk("text-prompt", "Kommentar zur markierten Stelle:", "", v => TextAddMark(MarkKind.Comment, v)), "Kommentar an die Markierung heften"),
                    MenuButton("text-symbol", "Ω", "Symbol", "Sonderzeichen einfügen", new[] { "€", "©", "®", "™", "§", "°", "±", "×", "÷", "→", "←", "✓", "•", "…", "„“", "‚‘", "–", "—", "½", "²", "³", "µ", "♥", "☺" }.Select(s => (s, new Action(() => TextInsertSymbol(s == "„“" ? "„“" : s)))).ToArray())));
                g.Children.Add(RibbonGroup("Seite",
                    XB("text-header", "▔", "Kopfzeile", () => TextAsk("text-prompt", "Text der Kopfzeile (oben auf jeder Seite):", pg.Header, v => TextSetPage(x => x.Header = v.Trim())), "Kopfzeile bearbeiten", pg.Header.Length > 0),
                    XB("text-footer", "▁", "Fußzeile", () => TextAsk("text-prompt", "Text der Fußzeile (unten auf jeder Seite):", pg.Footer, v => TextSetPage(x => x.Footer = v.Trim())), "Fußzeile bearbeiten", pg.Footer.Length > 0),
                    XB("text-pagenumbers", "#", "Seitenzahlen", () => TextSetPage(x => x.PageNumbers = !x.PageNumbers), "„Seite 1 von 3“ unten auf jeder Seite", pg.PageNumbers),
                    XB("text-toc", "☰", "Verzeichnis", TextInsertToc, "Inhaltsverzeichnis aus den Überschriften"),
                    XB("text-pagebreak2", "⤓", "Seitenumbruch", () => InsertBlock(new PageBreak()), "Neue Seite beginnen")));
                break;
            case "entwurf":
                var designs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                foreach (var (id, name, font, color) in PageSetup.Designs)
                {
                    var did = id;
                    var b = new Button { Name = "text-design-" + id, Focusable = false, Width = 96, Height = 50, Background = Brushes.White, BorderThickness = new Thickness(pg.Design == id ? 3 : 1), BorderBrush = pg.Design == id ? Accent : Brush.Parse("#FFD8DEE8"),
                        Content = new StackPanel { Children = { new TextBlock { Text = name, FontFamily = new FontFamily(font + ", Arial"), Foreground = Brush.Parse(color), FontWeight = FontWeight.Bold, FontSize = 13 }, new TextBlock { Text = "Aa Bb", FontFamily = new FontFamily(font + ", Arial"), Foreground = Brushes.Black, FontSize = 11 } } } };
                    ToolTip.SetTip(b, $"Design „{name}“: Schrift {font}");
                    b.Click += (_, _) => { TextSetPage(x => x.Design = did); _txMsg = $"Design „{name}“."; Show("text"); };
                    designs.Children.Add(b);
                }
                g.Children.Add(RibbonGroup("Designs", designs));
                g.Children.Add(RibbonGroup("Seitenhintergrund",
                    MenuButton("text-watermark", "💧", "Wasserzeichen", "Schräger Hinweis hinter dem Text", ("Keins", () => TextSetPage(x => x.Watermark = "")), ("ENTWURF", () => TextSetPage(x => x.Watermark = "ENTWURF")), ("VERTRAULICH", () => TextSetPage(x => x.Watermark = "VERTRAULICH")), ("MUSTER", () => TextSetPage(x => x.Watermark = "MUSTER")),
                        ("Eigener Text …", () => TextAsk("text-prompt", "Text des Wasserzeichens:", pg.Watermark, v => TextSetPage(x => x.Watermark = v.Trim())))),
                    XB("text-border", "▢", "Seitenrand", () => TextSetPage(x => x.PageBorder = !x.PageBorder), "Linie um jede Seite", pg.PageBorder)));
                break;
            case "layout":
                g.Children.Add(RibbonGroup("Seite einrichten",
                    MenuButton("text-margins", "⬚", "Ränder", "Seitenränder", PageSetup.Margins.Select(m => ($"{m.Name} ({m.Left.ToString("0.##")} cm)", new Action(() => TextSetPage(x => x.SetMargins(m.Id))))).ToArray()),
                    XB("text-orient", pg.Landscape ? "▭" : "▯", pg.Landscape ? "Quer" : "Hoch", () => TextSetPage(x => x.Landscape = !x.Landscape), "Hoch- oder Querformat", pg.Landscape),
                    MenuButton("text-columns", "▥", $"Spalten: {pg.Columns}", "Text in Spalten setzen (im PDF und in Word)", ("Eine", () => TextSetPage(x => x.Columns = 1)), ("Zwei", () => TextSetPage(x => x.Columns = 2)), ("Drei", () => TextSetPage(x => x.Columns = 3))),
                    XB("text-hyphen", "‐", "Silbentrennung", () => TextSetPage(x => x.Hyphenate = !x.Hyphenate), "Automatische Silbentrennung in Word/LibreOffice einschalten", pg.Hyphenate)));
                g.Children.Add(RibbonGroup("Absatz",
                    XB("text-indent-more", "⇥", "Einzug +", () => { if (CurPara() is { } cp) { Snapshot(); cp.IndentCm = Math.Min(10, cp.IndentCm + 0.5); Changed(); } }, "Absatz weiter einrücken"),
                    XB("text-indent-less", "⇤", "Einzug −", () => { if (CurPara() is { } cp) { Snapshot(); cp.IndentCm = Math.Max(0, cp.IndentCm - 0.5); Changed(); } }, "Einzug verringern"),
                    MenuButton("text-spacing", "↕", "Abstand", "Abstand nach dem Absatz", ("Standard", () => SetSpace(null)), ("Kein Abstand", () => SetSpace(0)), ("6 pt", () => SetSpace(6)), ("12 pt", () => SetSpace(12)), ("18 pt", () => SetSpace(18)))));
                break;
            case "referenzen":
                g.Children.Add(RibbonGroup("Verzeichnis & Notizen",
                    XB("text-toc2", "☰", "Inhaltsverzeichnis", TextInsertToc, "Inhaltsverzeichnis aus den Überschriften 1–3"),
                    XB("text-footnote", "¹", "Fußnote", () => TextAsk("text-prompt", "Text der Fußnote (steht unten auf der Seite):", "", v => TextAddMark(MarkKind.Footnote, v)), "Fußnote am Cursor"),
                    XB("text-endnote", "ⁱ", "Endnote", () => TextAsk("text-prompt", "Text der Endnote (steht am Ende):", "", v => TextAddMark(MarkKind.Endnote, v)), "Endnote am Cursor")));
                g.Children.Add(new TextBlock { Text = "Bildbeschriftung und Alternativtext stehen direkt am Bild.", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) });
                break;
            case "sendungen":
                g.Children.Add(RibbonGroup("Serienbrief",
                    XB("text-merge-source", "📇", "Datenquelle", () => _ = PickMergeSource(), "Tabelle (CSV, Excel, LibreOffice) mit Feldnamen in Zeile 1"),
                    MenuButton("text-merge-field", "«»", "Feld einfügen", "Platzhalter für ein Feld an der Cursorstelle", (_txData?.Headers ?? []).Where(h => h.Length > 0).Select(h => (h, new Action(() => TextInsertSymbol($"«{h}»")))).DefaultIfEmpty(("Erst Datenquelle wählen", () => { })).ToArray()),
                    XB("text-merge-run", "✉", "Serienbrief", () => TextMergeCreate(), "Für jede Zeile der Tabelle einen Brief erzeugen (DOCX und PDF)", _txData is not null)));
                g.Children.Add(RibbonGroup("Adressen",
                    XB("text-labels", "🏷", "Etiketten", () => { if (TextLabels() is { } f) Toast(_txMsg, ("Öffnen", new Action(() => Launch(f)))); }, "Adressetiketten 3 × 8 (70 × 37 mm)"),
                    XB("text-envelopes", "✉", "Umschläge", () => { if (TextEnvelopes() is { } f) Toast(_txMsg, ("Öffnen", new Action(() => Launch(f)))); }, "Umschläge DIN lang mit Absender aus den Einstellungen")));
                break;
            case "pruefen":
                g.Children.Add(RibbonGroup("Korrektur",
                    XB("text-spell2", "abc", "Rechtschreibung", () => { _txSpellOn = !_txSpellOn; if (_txSpellOn) RunSpelling(); else _txMistakes = []; }, "Rechtschreibung prüfen", _txSpellOn),
                    XB("text-comments", "💬", "Kommentare", () => _txComments = !_txComments, "Alle Kommentare anzeigen", _txComments),
                    XB("text-access", "♿", "Barrierefreiheit", () => _txAccess = !_txAccess, "Dokument auf Barrieren prüfen (Alternativtexte, Überschriften, Links)", _txAccess)));
                g.Children.Add(RibbonGroup("Änderungen",
                    XB("text-compare", "⇄", "Vergleichen", () => _ = PickCompare(), "Mit einer älteren Fassung vergleichen"),
                    XB("text-accept", "✓", "Annehmen", () => TextResolveChanges(true), "Alle Änderungen annehmen", false),
                    XB("text-reject", "✕", "Ablehnen", () => TextResolveChanges(false), "Alle Änderungen ablehnen", false)));
                break;
            case "ansicht":
                g.Children.Add(RibbonGroup("Ansicht",
                    XB("text-nav", "☰", "Navigation", () => _txNav = !_txNav, "Überschriften als Navigationsbereich", _txNav),
                    XB("text-read", "📖", "Lesemodus", OpenReadMode, "Ruhige Leseansicht im Vollbild (Esc beendet)"),
                    MenuButton("text-zoom", "🔍", $"{_txZoom:P0}", "Zoom", ("75 %", () => _txZoom = 0.75), ("100 %", () => _txZoom = 1), ("125 %", () => _txZoom = 1.25), ("150 %", () => _txZoom = 1.5)),
                    XB("text-focus", "⛶", "Vollbild", ToggleFocusMode, "Vollbild/Fokusmodus (F11)")));
                break;
            case "ki":
                g.Children.Add(RibbonGroup("Mit der KI schreiben",
                    XB("text-ai-rewrite", "✨", "Umschreiben", () => _ = TextAiRewrite("um"), "Markierung bzw. Absatz klarer formulieren"),
                    XB("text-ai-short", "✂", "Kürzen", () => _ = TextAiRewrite("kuerzen"), "Kürzer fassen"),
                    XB("text-ai-friendly", "☺", "Freundlicher", () => _ = TextAiRewrite("freundlich"), "Freundlicher formulieren"),
                    XB("text-ai-formal", "🎩", "Förmlicher", () => _ = TextAiRewrite("foermlich"), "Förmlicher formulieren")));
                g.Children.Add(RibbonGroup("Sprache",
                    XB("text-ai-read", "🔊", "Vorlesen", () => _ = TextReadAloud(), "Text mit einer natürlichen Stimme vorlesen (offline, Piper)"),
                    XB("text-ai-dictate", "🎙", "Mitschreiben", () => _ = TextTranscribe(), "Audiodatei mitschreiben lassen (offline, Whisper)")));
                break;
        }
        return g;
    }

    void SetSpace(double? pt) { if (CurPara() is { } cp) { Snapshot(); cp.SpaceAfterPt = pt; Changed(); } }

    async Task PickMergeSource()
    {
        var req = new PickRequest("text-merge-source", false, false, ["csv", "xlsx", "ods"]);
        var f = PickOverride is { } fake ? (await fake(req))?.FirstOrDefault() : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Datenquelle wählen", FileTypeFilter = [new FilePickerFileType("Tabellen") { Patterns = ["*.csv", "*.xlsx", "*.ods"] }] })).Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
        if (f is null) return;
        try { TextMergeSource(f); } catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException) { _txMsg = "Datenquelle ging nicht: " + ex.Message; }
        Show("text");
    }

    async Task PickCompare()
    {
        var f = await PickText(false, "");
        if (f is null) return;
        try { TextCompareWith(f); } catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException) { _txMsg = "Vergleichen ging nicht: " + ex.Message; }
        Show("text");
    }

    // ---------- W8: Lesemodus ----------
    public Window? ReadModeWindow { get; private set; }

    void OpenReadMode()
    {
        var sp = new StackPanel { Spacing = 14, MaxWidth = 760, Margin = new Thickness(40, 50), HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var b in TextDocument.Blocks)
            switch (b)
            {
                case Para p when p.Text.Length > 0:
                    sp.Children.Add(new TextBlock { Text = (p.List == ListKind.Bullet ? "•  " : "") + p.Text, TextWrapping = TextWrapping.Wrap, FontSize = TextDoc.StyleSize(p.Style) * 1.75, LineHeight = TextDoc.StyleSize(p.Style) * 2.6,
                        FontWeight = p.Style is ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 ? FontWeight.Bold : FontWeight.Normal, FontFamily = new FontFamily("Georgia, serif"), Foreground = Brush.Parse("#2B2B2B") });
                    break;
                case Picture pic:
                    try { using var ms = new MemoryStream(pic.Data); sp.Children.Add(new Image { Source = new Avalonia.Media.Imaging.Bitmap(ms), MaxWidth = 700 }); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException) { }
                    break;
            }
        var w = new Window { Title = "Lesemodus", WindowState = WindowState.FullScreen, Background = Brush.Parse("#FBF7EE"), Content = new ScrollViewer { Name = "text-read-view", Content = sp } };
        w.KeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
        w.Closed += (_, _) => ReadModeWindow = null;
        ReadModeWindow = w; w.Show();
    }

    // ---------- Seitenleiste: Navigation, Kommentare, Barrierefreiheit, Serienbrief ----------
    IEnumerable<Control> TextProPanels()
    {
        if (_txNav)
        {
            var box = new StackPanel { Name = "text-nav-panel", Spacing = 2, Children = { H("Navigation", 13) } };
            var toc = TextTools.Toc(TextDocument);
            if (toc.Count == 0) box.Children.Add(P("Noch keine Überschriften. Absatzart „Überschrift 1“ wählen – dann erscheint hier die Gliederung.", null, 12));
            foreach (var e in toc)
            {
                var block = e.Block;
                var b = new Button { Name = $"text-nav-{block}", Content = e.Text, Focusable = false, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(6 + (e.Level - 1) * 14, 3, 6, 3), FontWeight = e.Level == 1 ? FontWeight.SemiBold : FontWeight.Normal, Background = Brushes.Transparent };
                b.Click += (_, _) => FocusPara(block, 0);
                box.Children.Add(b);
            }
            yield return Glass2(box, 10);
        }
        if (_txComments)
        {
            var box = new StackPanel { Name = "text-comments-panel", Spacing = 4, Children = { H("Kommentare", 13) } };
            var any = false;
            for (var bi = 0; bi < TextDocument.Blocks.Count; bi++)
                if (TextDocument.Blocks[bi] is Para p)
                    foreach (var m in p.Marks.Where(m => m.Kind == MarkKind.Comment).ToList())
                    {
                        any = true; var block = bi; var mark = m; var para = p;
                        var quote = m.Length > 0 ? "„" + p.Text.Substring(m.Start, Math.Min(m.Length, p.Text.Length - m.Start)) + "“" : "";
                        var del = new Button { Content = "Erledigt", Focusable = false, Padding = new Thickness(6, 1), FontSize = 12 };
                        del.Click += (_, _) => { Snapshot(); para.Marks.Remove(mark); Changed(); Show("text"); };
                        var go = new Button { Content = "Zeigen", Focusable = false, Padding = new Thickness(6, 1), FontSize = 12 };
                        go.Click += (_, _) => { TextFocusBlock = block; _txSel = mark.Start; _txSelLen = mark.Length; Show("text"); };
                        box.Children.Add(new Border { Background = Brush.Parse("#33FACC15"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 6),
                            Child = new StackPanel { Spacing = 3, Children = { P(m.Value, null, 12.5), P(quote, null, 12), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { go, del } } } } });
                    }
            if (!any) box.Children.Add(P("Keine Kommentare. Text markieren → „Einfügen“ → „Kommentar“.", null, 12));
            yield return Glass2(box, 10);
        }
        if (_txAccess)
        {
            var issues = TextTools.Accessibility(TextDocument);
            var box = new StackPanel { Name = "text-access-panel", Spacing = 4, Children = { H("Barrierefreiheit", 13) } };
            if (issues.Count == 0) box.Children.Add(P("Keine Barrieren gefunden ✓", Brush.Parse("#4ADE80"), 12.5));
            foreach (var i in issues) box.Children.Add(new StackPanel { Spacing = 1, Children = { P("⚠ " + i.Text, Brush.Parse("#FACC15"), 12.5), P(i.Fix, null, 12) } });
            yield return Glass2(box, 10);
        }
        if (_txMerge && _txData is { } d)
        {
            var box = new StackPanel { Name = "text-merge-panel", Spacing = 4, Children = { H("Serienbrief", 13), P($"Datenquelle: {Path.GetFileName(d.File)} · {d.Rows.Count} Zeilen", null, 12), P("Felder: " + string.Join(", ", d.Headers.Where(h => h.Length > 0)), null, 12) } };
            var used = TextTools.Fields(TextDocument);
            var missing = used.Where(u => !d.Headers.Any(h => h.Equals(u, StringComparison.CurrentCultureIgnoreCase))).ToList();
            if (missing.Count > 0) box.Children.Add(P("Unbekannte Felder im Text: " + string.Join(", ", missing), Brush.Parse("#FACC15"), 12));
            yield return Glass2(box, 10);
        }
    }
}
