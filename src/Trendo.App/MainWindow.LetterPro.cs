using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Briefe schreiben (STUDIO-LETTER-01), Vorschläge Teil 4: F1 Vorlagen-Galerie, F2 Fristenrechner, F3 Serienbrief, F4 Unterschrift,
/// U1 DIN-A4-Vorschau im Maßstab, U2 Felder in der Vorschau anklicken, U3 Schrittleiste.</summary>
public partial class MainWindow
{
    private bool? _letterGallery;
    private bool _letterSaved;
    private Action? _letterStepSync;
    private string _letterDl = "";

    private bool LetterGalleryOpen(LetterDraft d) => _letterGallery ?? (d.Category == "frei" && d.Body.Trim().Length == 0);

    /// <summary>F1: Vorlagen nach Anlass, mit Betreff als Vorschau.</summary>
    private Control LetterGallery(LetterDraft d)
    {
        var sp = new StackPanel { Name = "letter-gallery", Spacing = 6 };
        foreach (var g in Letters.Groups)
        {
            var cats = Letters.Categories.Where(c => Letters.GroupOf(c) == g).ToList();
            if (cats.Count == 0) continue;
            sp.Children.Add(new TextBlock { Text = g, Foreground = Muted, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
            var wrap = new WrapPanel();
            foreach (var c in cats)
            {
                var card = new StackPanel { Spacing = 3, Children = {
                    new TextBlock { Text = c.Label, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = c.Subject.Length > 0 ? c.Subject : "Ohne Vorlage – du schreibst alles selbst.", Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 30 } } };
                if (c.Legal) card.Children.Add(new TextBlock { Text = "Fristen selbst prüfen", Foreground = Brush.Parse("#FACC15"), FontSize = 10 });
                var b = new Button { Name = "letter-gallery-" + c.Id, Width = 172, MinHeight = 74, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10),
                    HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, Background = c.Id == d.Category ? Accent : TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), Content = card };
                b.Click += (_, _) =>
                {
                    Letters.ApplyTemplate(d, c.Id); _letterGallery = false;
                    _letterMsg = c.Id == "frei" ? "Freier Brief: ohne Vorlage. Betreff und Text schreibst du selbst." : $"Vorlage „{c.Label}“ eingesetzt. Passe die eckigen Klammern an.";
                    Show("brief");
                };
                wrap.Children.Add(b);
            }
            sp.Children.Add(wrap);
        }
        return sp;
    }

    /// <summary>F2–F4: Frist berechnen, Serienbrief, Unterschrift.</summary>
    private Control LetterTools(LetterDraft d)
    {
        const string V = "brief";
        Border Box(string title, string help, params Control[] c)
        {
            var sp = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold }, P(help, null, 11) } };
            foreach (var x in c) sp.Children.Add(x);
            return new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Margin = new Thickness(0, 6, 0, 0), Child = sp };
        }
        WrapPanel Row(params Control[] c) { var w = new WrapPanel(); foreach (var x in c) { x.Margin = new Thickness(0, 0, 6, 6); x.VerticalAlignment = VerticalAlignment.Center; w.Children.Add(x); } return w; }
        Button B(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Background = accent ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) }; b.Click += (_, _) => go(); return b; }
        var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");

        // F2 Fristenrechner
        var start = new TextBox { Name = "letter-dl-start", Text = DateTime.Today.ToString("dd.MM.yyyy", de), Width = 110, PlaceholderText = "TT.MM.JJJJ" };
        var amount = new TextBox { Name = "letter-dl-amount", Text = "14", Width = 60 };
        var unit = new ComboBox { Name = "letter-dl-unit", ItemsSource = new[] { "Tage", "Wochen", "Monate" }, SelectedIndex = 0 };
        var outText = new TextBlock { Name = "letter-dl-out", Text = _letterDl, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        DateTime? Calc()
        {
            if (!DateTime.TryParseExact(start.Text?.Trim(), "d.M.yyyy", de, System.Globalization.DateTimeStyles.None, out var s)) { outText.Text = "Startdatum bitte als TT.MM.JJJJ."; return null; }
            if (!int.TryParse(amount.Text?.Trim(), out var n) || n is < 1 or > 1000) { outText.Text = "Bitte eine Zahl von 1 bis 1000 eintragen."; return null; }
            var (date, shifted) = Letters.Deadline(s, n, (string)unit.SelectedItem!);
            _letterDl = outText.Text = $"Frist endet am {date.ToString("dddd, dd.MM.yyyy", de)}{(shifted ? " (fiel aufs Wochenende → nächster Werktag)" : "")}. Feiertage und Fristbeginn bitte selbst prüfen.";
            return date;
        }
        var dl = Box("Frist berechnen", "Ab wann läuft die Frist, wie lang ist sie? Das Ergebnis kannst du in den Brief einsetzen oder in den Kalender eintragen.",
            Row(start, P("+"), amount, unit, B("letter-dl-calc", "Berechnen", () => Calc(), true)), outText,
            Row(B("letter-dl-insert", "In den Brief einsetzen", () => { if (Calc() is { } x) { _letterMsg = Letters.FillDate(d, x) ? "Datum eingesetzt." : "Im Brief steht kein [Datum] zum Ersetzen."; Show(V); } }),
                B("letter-dl-cal", "In den Kalender", () =>
                {
                    if (Calc() is not { } x) return;
                    Cal.Add(new CalEvent(Guid.NewGuid().ToString("N"), x, x.AddDays(1), "Frist: " + (d.Subject.Trim().Length > 0 ? d.Subject.Trim() : "Brief"), AllDay: true, RemindMin: 2 * 24 * 60));
                    _letterMsg = $"Im Kalender eingetragen: {x:dd.MM.yyyy}, mit Erinnerung zwei Tage vorher."; Show(V);
                })));

        // F3 Serienbrief
        var sf = new TextBox { Name = "letter-serial-file", PlaceholderText = "Tabelle mit Empfängern (XLSX, ODS oder CSV)", MinWidth = 260 };
        var serial = Box("Serienbrief", "Ein Brief je Zeile der Tabelle. Felder wie {Name} oder «Name» im Text werden ersetzt. Bleibt der Empfänger leer, setzt Trendo die Anschrift aus den Spalten Firma, Name (oder Vorname/Nachname), Straße, PLZ und Ort zusammen.",
            Row(sf, B("letter-serial-run", "Serienbrief erstellen", () => Try(V, () =>
            {
                var file = sf.Text?.Trim().Trim('"') ?? "";
                if (!File.Exists(file)) throw new ArgumentException("Bitte eine vorhandene Tabelle wählen.");
                var doc = SheetFiles.Open(file); var s = doc.Sheets[0]; var calc = new SheetCalc(doc);
                var cols = Math.Max(0, s.MaxCol) + 1;
                var headers = Enumerable.Range(0, cols).Select(c => calc.Display(s, 0, c).Trim()).ToList();
                var rows = Enumerable.Range(1, Math.Max(0, s.MaxRow)).Select(r => (IReadOnlyList<string>)Enumerable.Range(0, cols).Select(c => calc.Display(s, r, c)).ToList()).ToList();
                var tpl = d.Copy(); if (tpl.Recipient.Trim().Length == 0) tpl.Recipient = "";
                var missing = Letters.SerialFields(tpl).Where(f => !headers.Contains(f, StringComparer.CurrentCultureIgnoreCase)).ToList();
                if (missing.Count > 0) throw new ArgumentException("Diese Felder gibt es in der Tabelle nicht: " + string.Join(", ", missing));
                var letters = Letters.Serial(tpl, headers, rows);
                if (letters.Count == 0) throw new ArgumentException("Die Tabelle hat keine Datenzeilen.");
                var bad = letters.Select((l, i) => (i, Letters.Validate(l).FirstOrDefault(x => x.Blocking))).FirstOrDefault(x => x.Item2 is not null);
                if (bad.Item2 is { } issue) throw new ArgumentException($"Brief {bad.i + 1}: {issue.Field} – {issue.Message}");
                var dir = System.IO.Path.Combine(ProjectsRoot, "Briefe", $"Serienbrief-{DateTime.Now:yyyyMMdd-HHmm}"); Directory.CreateDirectory(dir);
                for (var i = 0; i < letters.Count; i++)
                {
                    var stem = new string(letters[i].Recipient.Split('\n')[0].Where(ch => !System.IO.Path.GetInvalidFileNameChars().Contains(ch)).ToArray()).Trim();
                    Letters.WriteDocx(letters[i], System.IO.Path.Combine(dir, $"{i + 1:000}-{(stem.Length > 0 ? stem : "Brief")}.docx"));
                }
                _core.Audit.Append(Principal.LocalOwner.Name, "letter.serial", $"{letters.Count} Briefe");
                _letterMsg = $"{letters.Count} Briefe gespeichert in {dir}"; Show(V); NotifyFileResult(_letterMsg);
            }), true)));

        // F4 Unterschrift
        var sig = new TextBox { Name = "letter-signature", Text = _settings.LetterSignature, PlaceholderText = "Bild deiner Unterschrift (PNG oder JPG)", MinWidth = 260 };
        var sign = Box("Unterschrift", "Ein Foto oder Scan deiner Unterschrift wird unter den Gruß gesetzt. Das ist keine qualifizierte elektronische Signatur.",
            Row(sig, B("letter-signature-set", "Übernehmen", () => Try(V, () =>
                {
                    var f = sig.Text?.Trim().Trim('"') ?? "";
                    if (!File.Exists(f) || XrPlus.ImageSize(File.ReadAllBytes(f)) is (0, _) or (_, 0)) throw new ArgumentException("Bitte ein PNG- oder JPG-Bild wählen.");
                    _settings.LetterSignature = d.SignatureFile = f; SaveSettings(); _letterMsg = "Unterschrift wird unter den Gruß gesetzt."; Show(V);
                })),
                B("letter-signature-off", "Ohne Unterschrift", () => { _settings.LetterSignature = d.SignatureFile = ""; SaveSettings(); Show(V); })));
        return new StackPanel { Children = { dl, serial, sign } };
    }

    /// <summary>U3: Schrittleiste – zeigt, was schon fertig ist, und springt zum passenden Feld.</summary>
    private Control LetterSteps(LetterDraft d)
    {
        var row = new StackPanel { Name = "letter-steps", Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        var steps = new (string Label, Func<bool> Done, Action Go)[]
        {
            ("Absender", () => d.SenderName.Trim().Length > 0 && d.SenderStreet.Trim().Length > 0 && d.SenderCity.Trim().Length > 0, () => FocusLetterField("letter-sender-name")),
            ("Empfänger", () => d.Recipient.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >= 3, () => FocusLetterField("letter-recipient")),
            ("Inhalt", () => d.Subject.Trim().Length > 0 && d.Body.Trim().Length > 0 && !d.Subject.Contains('[') && !d.Body.Contains('['), () => FocusLetterField("letter-subject")),
            ("Prüfen", () => !Letters.Validate(d).Any(x => x.Blocking), () => ClickLetter("letter-check")),
            ("Fertig", () => _letterSaved, () => ClickLetter("letter-save")),
        };
        var buttons = new List<Button>();
        for (var i = 0; i < steps.Length; i++)
        {
            var (label, _, go) = steps[i];
            if (i > 0) row.Children.Add(new TextBlock { Text = "›", Foreground = Muted, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
            var b = new Button { Name = $"letter-step-{i + 1}", FontSize = 12, Padding = new Thickness(12, 5), CornerRadius = new CornerRadius(14), BorderBrush = Edge, BorderThickness = new Thickness(1), Foreground = Text };
            b.Click += (_, _) => go();
            buttons.Add(b); row.Children.Add(b);
        }
        void Sync()
        {
            var current = Array.FindIndex(steps, s => !s.Done());
            for (var i = 0; i < steps.Length; i++)
            {
                var done = steps[i].Done();
                buttons[i].Content = (done ? "✓ " : $"{i + 1} ") + steps[i].Label;
                buttons[i].Background = i == current ? Accent : done ? Brush.Parse(C("#3322B35E", "#3322B35E")) : Brushes.Transparent;
            }
        }
        _letterStepSync = Sync; Sync();
        return row;
    }

    private void FocusLetterField(string name)
    {
        if (this.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == name) is { } t) { t.BringIntoView(); t.Focus(); t.CaretIndex = t.Text?.Length ?? 0; }
    }

    private void ClickLetter(string name)
    {
        if (this.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == name) is { } b) b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>U1: DIN-A4-Blatt im Maßstab (DIN 5008 Form B) mit Sichtfenster, Falz- und Lochmarken. U2: Klick auf einen Teil springt ins passende Feld.</summary>
    private Control LetterPreview(LetterDraft d)
    {
        const double Pt = 0.3528; // 1 pt in mm
        var ink = Brushes.Black; var grey = Brush.Parse("#5C6B7A");
        TextBlock T(string name, double pt, IBrush fg, string? field, bool bold = false)
        {
            var t = new TextBlock { Name = name, FontFamily = new FontFamily("Arial, Liberation Sans, Helvetica"), FontSize = pt * Pt, Foreground = fg, TextWrapping = TextWrapping.Wrap, LineHeight = pt * Pt * 1.2, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal };
            if (field is not null)
            {
                t.Cursor = new Cursor(StandardCursorType.Hand); ToolTip.SetTip(t, "Zum Bearbeiten klicken");
                t.PointerPressed += (_, e) => { FocusLetterField(field); e.Handled = true; };
            }
            return t;
        }
        var sender = T("letter-preview-sender", 9, grey, "letter-sender-name"); sender.TextAlignment = TextAlignment.Right; sender.Width = 85;
        var back = T("letter-preview-return", 7, grey, "letter-sender-street"); back.Width = 80; back.TextWrapping = TextWrapping.NoWrap; back.TextTrimming = TextTrimming.CharacterEllipsis;
        var recip = T("letter-preview-recipient", 11, ink, "letter-recipient"); recip.Width = 80;
        var date = T("letter-preview-date", 11, ink, "letter-place"); date.Width = 65;
        var subject = T("letter-preview-subject", 11, ink, "letter-subject", bold: true);
        var sal = T("letter-preview-salutation", 11, ink, "letter-salutation"); sal.Margin = new Thickness(0, 8.5, 0, 4.2);
        var body = T("letter-preview-body", 11, ink, "letter-body");
        var closing = T("letter-preview-closing", 11, ink, "letter-closing"); closing.Margin = new Thickness(0, 4.2, 0, 0);
        var sigImg = new Image { Name = "letter-preview-signature", Width = 45, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 1, 0, 0) };
        var name = T("letter-preview-name", 11, ink, "letter-sender-name"); name.Margin = new Thickness(0, 2, 0, 0);
        var att = T("letter-preview-attachments", 11, ink, "letter-attachments"); att.Margin = new Thickness(0, 6, 0, 0);
        var flow = new StackPanel { Width = 165, Children = { subject, sal, body, closing, sigImg, name, att } };
        Canvas.SetLeft(flow, 25); Canvas.SetTop(flow, 98.5);
        var more = new TextBlock { Name = "letter-preview-more", Text = "… weiter auf Seite 2", FontSize = 3, Foreground = grey, IsVisible = false };
        Canvas.SetLeft(more, 160); Canvas.SetTop(more, 287);
        var flowClip = new Canvas { Width = 210, Height = 297 - 20, ClipToBounds = true, Children = { flow } };
        flow.SizeChanged += (_, e) => more.IsVisible = 98.5 + e.NewSize.Height > 277;

        string? sigPath = null;
        void Sync()
        {
            sender.Text = string.Join("\n", new[] { d.SenderName, d.SenderStreet, d.SenderCity, d.SenderContact }.Where(s => !string.IsNullOrWhiteSpace(s)));
            back.Text = string.Join(" · ", new[] { d.SenderName, d.SenderStreet, d.SenderCity }.Where(s => !string.IsNullOrWhiteSpace(s)));
            recip.Text = d.Recipient;
            date.Text = (d.Reference.Trim().Length > 0 ? $"Ihr Zeichen: {d.Reference.Trim()}\n" : "") + (string.IsNullOrWhiteSpace(d.Place) ? "" : d.Place + ", ") + d.Date.ToString("d. MMMM yyyy", De);
            subject.Text = d.Subject; sal.Text = d.Salutation; body.Text = d.Body; closing.Text = d.Closing;
            name.Text = d.SenderName; name.Margin = new Thickness(0, File.Exists(d.SignatureFile) ? 1 : 12, 0, 0);
            att.Text = string.IsNullOrWhiteSpace(d.Attachments) ? "" : "Anlagen\n" + d.Attachments.Trim(); att.IsVisible = att.Text.Length > 0;
            if (sigPath != d.SignatureFile)
            {
                sigPath = d.SignatureFile;
                try { sigImg.Source = File.Exists(sigPath) ? new Avalonia.Media.Imaging.Bitmap(sigPath) : null; } catch (Exception) { sigImg.Source = null; }
                sigImg.IsVisible = sigImg.Source is not null;
            }
            _letterStepSync?.Invoke();
        }
        _letterSync = Sync; Sync();

        var page = new Canvas { Width = 210, Height = 297, Background = Brushes.White, ClipToBounds = true };
        void At(Control c, double x, double y) { Canvas.SetLeft(c, x); Canvas.SetTop(c, y); page.Children.Add(c); }
        // Sichtfenster (Anschriftfeld 85 × 45 mm, 20 mm von links, 45 mm von oben) – nur in der Vorschau sichtbar
        At(new Rectangle { Width = 85, Height = 45, Stroke = Brush.Parse("#9CB3C9"), StrokeThickness = 0.3, StrokeDashArray = [4, 3] }, 20, 45);
        foreach (var y in new[] { 105.0, 210.0 }) At(new Line { StartPoint = new Point(0, 0), EndPoint = new Point(5, 0), Stroke = grey, StrokeThickness = 0.3 }, 3, y);
        At(new Line { StartPoint = new Point(0, 0), EndPoint = new Point(7, 0), Stroke = grey, StrokeThickness = 0.3 }, 3, 148.5);
        At(sender, 105, 15);
        At(back, 25, 58);
        At(recip, 25, 62.7);
        At(date, 125, 62.7);
        page.Children.Add(flowClip);
        At(more, 150, 287);
        var sheet = new Border { Name = "letter-preview", Background = Brush.Parse("#E6EBF0"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10),
            Child = new Viewbox { Stretch = Stretch.Uniform, Child = new Border { BoxShadow = BoxShadows.Parse("0 1 4 0 #33000000"), Child = page } }, MinWidth = 180, VerticalAlignment = VerticalAlignment.Stretch };
        var wrap = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        wrap.Children.Add(new TextBlock { Text = "Vorschau (DIN A4, DIN 5008) – auf einen Teil klicken, um ihn zu bearbeiten", Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(sheet, 1); wrap.Children.Add(sheet);
        return wrap;
    }
}
