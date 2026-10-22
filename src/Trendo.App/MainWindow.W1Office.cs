using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 1: Dokumente (SYS-10) und Briefstudio (STUDIO-LETTER-01).</summary>
public partial class MainWindow
{
    // ---------- SYS-10: Dokumente ----------
    private Control OfficeView()
    {
        var sp = new StackPanel { Spacing = 5 };
        var msg = P(""); msg.Name = "office-msg";
        var row = new WrapPanel();
        foreach (var (file, label) in new[] { ("DIN5008-Privatbrief-FormB.docx", "Neuer Privatbrief (Vorlage)"), ("DIN5008-Geschaeftsbrief-FormB.docx", "Neuer Geschäftsbrief (Vorlage)") })
        {
            var b = new Button { Name = "office-new-" + (file.Contains("Privat") ? "privat" : "geschaeft"), Content = label, Margin = new Thickness(0, 0, 6, 6) };
            b.Click += (_, _) =>
            {
                var dir = Path.Combine(ProjectsRoot, "Dokumente"); Directory.CreateDirectory(dir);
                var target = Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd-HHmm}-{Path.GetFileNameWithoutExtension(file)}.docx");
                using (var s = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/templates/{file}"))) using (var f = File.Create(target)) s.CopyTo(f);
                _core.Audit.Append(Principal.LocalOwner.Name, "office.new", Path.GetFileName(target));
                msg.Text = $"Angelegt: {Path.GetFileName(target)}"; Launch(target);
            };
            row.Children.Add(b);
        }
        var letter = new Button { Name = "office-brief", Content = "Im Briefstudio schreiben", Margin = new Thickness(0, 0, 6, 6), Background = Accent };
        letter.Click += (_, _) => Show("brief"); row.Children.Add(letter);
        row.Children.Add(OpenInButton("libreoffice", () => LatestOfficeDoc(), "office"));
        sp.Children.Add(row); sp.Children.Add(msg);
        var soffice = ToolPath("libreoffice");
        sp.Children.Add(P(soffice is null ? "LibreOffice ist nicht installiert – Öffnen nutzt dein Standardprogramm, PDF-Export ist aus." : "Zuletzt bearbeitet (Projektordner und Dokumente). „PDF“ wandelt mit LibreOffice im Hintergrund um.", null, 11));
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var i = 0;
        foreach (var f in FileShelf.Recent([ProjectsRoot, docs], FileShelf.Office, 30))
        {
            var r = (Border)FileRow(f, i, "doc");
            if (soffice is not null && f.Extension.ToLowerInvariant() is ".docx" or ".doc" or ".odt" or ".xlsx" or ".ods" or ".pptx" or ".odp" or ".rtf")
            {
                var pdf = new Button { Name = $"doc-pdf-{i}", Content = "PDF", FontSize = 11, Padding = new Thickness(10, 2) };
                pdf.Click += async (_, _) => { msg.Text = $"Erstelle PDF aus {f.Name} …"; var (ok, m, p) = await PdfExport.ConvertAsync(new RealProcessRunner(), soffice, f.FullName); msg.Text = ok ? $"{m}: {Path.GetFileName(p)}" : m; _core.Audit.Append(Principal.LocalOwner.Name, "office.pdf", $"{f.Name} ok={ok}"); };
                ((StackPanel)((Grid)r.Child!).Children[1]).Children.Insert(1, pdf);
            }
            sp.Children.Add(r); i++;
        }
        if (i == 0) sp.Children.Add(P("Noch keine Dokumente gefunden.", Text));
        return AppsPage("Dokumente", sp);
    }

    // ---------- STUDIO-LETTER-01: Briefstudio ----------
    private LetterDraft? _letter;
    public LetterDraft Letter => _letter ??= NewLetter();
    private string _letterMsg = "";

    private LetterDraft NewLetter() => new() { SenderName = _settings.Name, SenderStreet = _settings.SenderStreet, SenderCity = _settings.SenderCity, SenderContact = _settings.SenderContact,
        Place = System.Text.RegularExpressions.Regex.Replace(_settings.SenderCity, @"^\s*\d{4,5}\s*", "") };

    private Control LetterView()
    {
        var d = Letter;
        var form = new StackPanel { Spacing = 4 };
        var hiddenCats = new StackPanel { Height = 0, Opacity = 0, IsHitTestVisible = false };
        foreach (var c in Letters.Categories)
        {
            var b = new Button { Name = "letter-cat-" + c.Id, Content = c.Label, IsTabStop = false, IsHitTestVisible = false, Height = 0, Padding = new Thickness(0), Opacity = 0 };
            b.Click += (_, _) => { Letters.ApplyTemplate(d, c.Id); Show("brief"); };
            hiddenCats.Children.Add(b);
        }
        form.Children.Add(hiddenCats);
        var labels = Letters.Categories.Select(c => c.Label).ToList();
        var selected = Array.FindIndex(Letters.Categories, c => c.Id == d.Category);
        var combo = new ComboBox { Name = "letter-template", ItemsSource = labels, SelectedIndex = Math.Max(0, selected), HorizontalAlignment = HorizontalAlignment.Stretch };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0 || combo.SelectedIndex >= Letters.Categories.Length) return;
            var id = Letters.Categories[combo.SelectedIndex].Id;
            if (id == d.Category) return;
            Letters.ApplyTemplate(d, id); Show("brief");
        };
        form.Children.Add(FieldLabel("Vorlage")); form.Children.Add(combo);
        if (Letters.Category(d.Category).Legal)
            form.Children.Add(P("Hinweis: Trendo hilft beim Formulieren, ersetzt aber keine Rechtsberatung. Fristen und Ansprüche bitte selbst prüfen oder beraten lassen.", Brush.Parse("#FACC15"), 11));

        TextBox Field(string name, string label, string value, Action<string> set, bool multi = false)
        {
            var t = new TextBox { Name = "letter-" + name, Text = value, PlaceholderText = label, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 72 : 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            t.TextChanged += (_, _) => { set(t.Text ?? ""); _letterSync?.Invoke(); }; return t;
        }
        form.Children.Add(FieldLabel("Absender"));
        form.Children.Add(Field("sender-name", "Dein Name", d.SenderName, v => d.SenderName = v));
        form.Children.Add(Field("sender-street", "Straße und Hausnummer", d.SenderStreet, v => d.SenderStreet = v));
        form.Children.Add(Field("sender-city", "PLZ und Ort", d.SenderCity, v => d.SenderCity = v));
        form.Children.Add(Field("sender-contact", "Telefon / E-Mail (optional)", d.SenderContact, v => d.SenderContact = v));
        form.Children.Add(FieldLabel("Empfänger"));
        form.Children.Add(Field("recipient", "Firma, Name, Straße, PLZ Ort", d.Recipient, v => d.Recipient = v, multi: true));
        form.Children.Add(FieldLabel("Betreff"));
        form.Children.Add(Field("subject", "Betreff", d.Subject, v => d.Subject = v));
        form.Children.Add(FieldLabel("Anrede"));
        form.Children.Add(Field("salutation", "Anrede", d.Salutation, v => d.Salutation = v));
        form.Children.Add(FieldLabel("Inhalt"));
        var body = Field("body", "Text", d.Body, v => d.Body = v, multi: true); body.MinHeight = 120;
        form.Children.Add(body);
        form.Children.Add(P("Der Brief wird als Klartext nach DIN 5008 gespeichert.", Muted, 11));
        form.Children.Add(FieldLabel("Gruß"));
        form.Children.Add(Field("closing", "Gruß", d.Closing, v => d.Closing = v));
        form.Children.Add(FieldLabel("Ort und Anlagen"));
        form.Children.Add(Field("place", "Ort für das Datum", d.Place, v => d.Place = v));
        form.Children.Add(Field("reference", "Kundennummer oder Aktenzeichen (optional)", d.Reference, v => d.Reference = v));
        form.Children.Add(Field("attachments", "Anlagen, eine pro Zeile (optional)", d.Attachments, v => d.Attachments = v, multi: true));

        var issues = new StackPanel { Name = "letter-issues", Spacing = 2 };
        void Check()
        {
            issues.Children.Clear();
            var l = Letters.Validate(d);
            issues.Children.Add(P(l.Count == 0 ? "✓ Alles da – der Brief kann so raus." : $"{l.Count(x => x.Blocking)} Pflichtpunkte, {l.Count(x => !x.Blocking)} Hinweise:", l.Any(x => x.Blocking) ? Brush.Parse("#F87171") : Brush.Parse("#4ADE80"), 12));
            foreach (var x in l) issues.Children.Add(P($"{(x.Blocking ? "✗" : "•")} {x.Field}: {x.Message}", x.Blocking ? Text : null, 11.5));
        }
        var check = new Button { Name = "letter-check", Content = "Brief prüfen" }; check.Click += (_, _) => Check();
        var improve = new Button { Name = "letter-ki", Content = "Text von der KI verbessern lassen", IsEnabled = _kiConnected };
        if (!_kiConnected) ToolTip.SetTip(improve, "Die lokale KI ist nicht bereit");
        var msg = P(_letterMsg, Text); msg.Name = "letter-msg";
        var suggestion = new StackPanel { Spacing = 4 };
        async Task<string?> Save(bool asPdf, string verb)
        {
            Check();
            if (Letters.Validate(d).Any(x => x.Blocking)) { msg.Text = "Bitte zuerst die Pflichtpunkte ergänzen."; return null; }
            _settings.SenderStreet = d.SenderStreet; _settings.SenderCity = d.SenderCity; _settings.SenderContact = d.SenderContact;
            if (string.IsNullOrWhiteSpace(_settings.Name)) _settings.Name = d.SenderName;
            SaveSettings();
            var (docx, _, v) = new LetterArchive(ProjectsRoot).Save(d);
            _core.Audit.Append(Principal.LocalOwner.Name, "letter.save", $"{Path.GetFileName(docx)} v{v}");
            msg.Text = _letterMsg = $"{verb}: {Path.GetFileName(docx)}{(v > 1 ? $" (Version {v})" : "")}";
            if (asPdf && ToolPath("libreoffice") is { } so)
            {
                msg.Text = "Erstelle PDF …";
                var (ok, m, p) = await PdfExport.ConvertAsync(new RealProcessRunner(), so, docx);
                msg.Text = _letterMsg = ok ? $"Gespeichert: {Path.GetFileName(docx)} und {Path.GetFileName(p)}" : $"Word-Datei gespeichert, PDF fehlgeschlagen: {m}";
            }
            return docx;
        }
        async Task SafeSave(bool asPdf, string verb) { try { await Save(asPdf, verb); } catch (Exception ex) { msg.Text = $"Speichern fehlgeschlagen: {ex.Message}"; CrashLog.Write(_core.DataRoot, ex, "Briefstudio"); } }
        Button Act(string name, string label, string icon, bool accent = false)
        {
            var fg = accent ? Brushes.White : Text;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Ico(icon, 14, fg), new TextBlock { Text = label, Foreground = fg, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 } } };
            var b = new Button { Name = name, Content = row, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8) };
            if (accent) b.Background = Accent; return b;
        }
        var save = Act("letter-save", "Speichern", Mdi.FileDocumentEdit, true);
        var export = Act("letter-export", "Exportieren", Mdi.Folder);
        var pdf = Act("letter-pdf", "Als PDF", Mdi.FileDocumentOutline); pdf.IsEnabled = ToolPath("libreoffice") is not null;
        if (!pdf.IsEnabled) ToolTip.SetTip(pdf, "Braucht LibreOffice (Zusatzprogramme)");
        var send = Act("letter-send", "Senden", Mdi.Send);
        save.Click += async (_, _) => await SafeSave(false, "Gespeichert in Briefe");
        export.Click += async (_, _) => await SafeSave(false, "Exportiert nach Briefe");
        pdf.Click += async (_, _) => await SafeSave(true, "Gespeichert");
        send.Click += (_, _) =>
        {
            if (_case is null) { msg.Text = "Senden bleibt an der Freigabe. Trendo verschickt den Brief nicht selbst. Lege dafür eine Akte an."; return; }
            msg.Text = "Senden bleibt an der Freigabe. Trendo verschickt den Brief nicht selbst.";
            Show("ausgabe");
        };
        improve.Click += async (_, _) =>
        {
            improve.IsEnabled = false; suggestion.Children.Clear(); suggestion.Children.Add(P("Die KI schreibt …"));
            var ans = await _ollama.ChatAsync(KiModelName, "Verbessere diesen Brieftext für einen deutschen Brief nach DIN 5008: klar, höflich, sachlich. Ändere keine Fakten, Zahlen, Daten oder Namen. Lass Platzhalter in eckigen Klammern stehen. Gib nur den verbesserten Text ohne Anrede und Gruß zurück.\n\nBetreff: " + d.Subject + "\n\n" + d.Body);
            suggestion.Children.Clear();
            var take = new Button { Name = "letter-ki-take", Content = "Vorschlag übernehmen", Background = Accent };
            take.Click += (_, _) => { d.Body = ans; _core.Audit.Append(Principal.LocalOwner.Name, "letter.ki_accept", d.Subject); Show("brief"); };
            suggestion.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 6, Children = { P("Vorschlag der KI (bitte prüfen):", null, 11), P(ans, Text, 12), take } } });
            improve.IsEnabled = true;
        };
        var reset = new Button { Name = "letter-new", Content = "Neuer Brief" }; reset.Click += (_, _) => { _letter = NewLetter(); _letterMsg = ""; _letterPreview = true; Show("brief"); };
        form.Children.Add(new WrapPanel { Margin = new Thickness(0, 8, 0, 0), Children = { check, Sp(improve), Sp(reset) } });
        form.Children.Add(msg); form.Children.Add(issues); form.Children.Add(suggestion);
        var arch = new LetterArchive(ProjectsRoot).List().Take(8).ToList();
        if (arch.Count > 0)
        {
            form.Children.Add(H("Zuletzt geschrieben", 14));
            var i = 0;
            foreach (var f in arch)
            {
                var r = (Border)FileRow(new FileInfo(f), i, "letter-arch");
                var edit = new Button { Name = $"letter-edit-{i}", Content = "Weiterschreiben", FontSize = 11, Padding = new Thickness(10, 2), IsEnabled = LetterArchive.Load(f) is not null };
                edit.Click += (_, _) => { if (LetterArchive.Load(f) is { } old) { _letter = old; _letter.Date = DateTime.Today; Show("brief"); } };
                ((StackPanel)((Grid)r.Child!).Children[1]).Children.Insert(0, edit);
                form.Children.Add(r); i++;
            }
        }

        var preview = LetterPreview(d);
        var formScroll = new ScrollViewer { Content = new Border { Background = Brush.Parse(C("#44101830", "#B3FFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(12), Margin = new Thickness(0, 0, 8, 0), Child = form } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*,*") };
        grid.Children.Add(LetterNav());
        Grid.SetColumn(formScroll, 1); grid.Children.Add(formScroll);
        Grid.SetColumn(preview, 2); grid.Children.Add(preview);
        var toggle = new Button { Name = "letter-preview-toggle", Content = _letterPreview ? "Schreiben" : "Vorschau", IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
        void ApplyNarrow(double width)
        {
            var narrow = width > 0 && width < 860;
            toggle.IsVisible = narrow;
            if (!narrow) { formScroll.IsVisible = true; preview.IsVisible = true; return; }
            formScroll.IsVisible = !_letterPreview; preview.IsVisible = _letterPreview;
            toggle.Content = _letterPreview ? "Schreiben" : "Vorschau";
            grid.ColumnDefinitions = _letterPreview ? new ColumnDefinitions("200,0,*") : new ColumnDefinitions("200,*,0");
        }
        toggle.Click += (_, _) => { _letterPreview = !_letterPreview; ApplyNarrow(grid.Bounds.Width); };
        grid.SizeChanged += (_, e) => ApplyNarrow(e.NewSize.Width);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, Children = { save, Sp(export), Sp(pdf), Sp(send) } };
        var stack = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        stack.Children.Add(toggle); Grid.SetRow(grid, 1); stack.Children.Add(grid);
        return LetterShell(stack, actions);
    }

    private static TextBlock FieldLabel(string text) => new() { Text = text, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 8, 0, 2) };

    private Control LetterPreview(LetterDraft d)
    {
        var sender = new TextBlock { Name = "letter-preview-sender", FontSize = 10, Foreground = Brush.Parse("#5C6B7A"), TextWrapping = TextWrapping.Wrap };
        var recip = new TextBlock { Name = "letter-preview-recipient", FontSize = 12, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 22, 80, 0) };
        var date = new TextBlock { Name = "letter-preview-date", FontSize = 12, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Right };
        var subject = new TextBlock { Name = "letter-preview-subject", FontSize = 13, FontWeight = FontWeight.Bold, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 8) };
        var sal = new TextBlock { Name = "letter-preview-salutation", FontSize = 12, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap };
        var body = new TextBlock { Name = "letter-preview-body", FontSize = 12, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        var closing = new TextBlock { Name = "letter-preview-closing", FontSize = 12, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap };
        void Sync()
        {
            var lines = new[] { d.SenderName, d.SenderStreet, d.SenderCity, d.SenderContact }.Where(s => !string.IsNullOrWhiteSpace(s));
            sender.Text = string.Join("\n", lines);
            recip.Text = d.Recipient;
            date.Text = (string.IsNullOrWhiteSpace(d.Place) ? "" : d.Place + ", ") + d.Date.ToString("d. MMMM yyyy", De);
            subject.Text = d.Subject;
            sal.Text = d.Salutation;
            body.Text = d.Body;
            closing.Text = d.Closing + (string.IsNullOrWhiteSpace(d.SenderName) ? "" : "\n\n" + d.SenderName);
            if (!string.IsNullOrWhiteSpace(d.Attachments)) closing.Text += "\n\nAnlagen\n" + d.Attachments.Trim();
        }
        _letterSync = Sync; Sync();
        sender.TextAlignment = TextAlignment.Right; sender.HorizontalAlignment = HorizontalAlignment.Right;
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { Logo(22), new TextBlock { Text = "Trendo OS", Foreground = Brushes.Black, FontWeight = FontWeight.SemiBold, FontSize = 16, VerticalAlignment = VerticalAlignment.Center } } };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new Thickness(0, 0, 0, 8), Children = { brand, sender } };
        Grid.SetColumn(sender, 1);
        var page = new StackPanel { Children = { head, recip, date, subject, sal, body, closing } };
        var sheet = new Border { Name = "letter-preview", Background = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Child = new ScrollViewer { Content = page }, MinWidth = 180, VerticalAlignment = VerticalAlignment.Stretch };
        var wrap = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        wrap.Children.Add(new TextBlock { Text = "Vorschau", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        Grid.SetRow(sheet, 1); wrap.Children.Add(sheet);
        return wrap;
    }
    private static Control Sp(Control c) { c.Margin = new Thickness(6, 0, 0, 6); return c; }
    /// <summary>T-0203: zuletzt angelegtes Dokument im Ordner „Dokumente“.</summary>
    private string? LatestOfficeDoc()
    {
        var dir = Path.Combine(ProjectsRoot, "Dokumente");
        return Directory.Exists(dir) ? Directory.GetFiles(dir).Where(f => ExternalApps.Accepts(ExternalApps.Get("libreoffice"), f)).OrderByDescending(File.GetLastWriteTime).FirstOrDefault() : null;
    }
}
