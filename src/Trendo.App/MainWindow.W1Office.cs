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
        if (_w45Msg.Length > 0) sp.Children.Add(ResultCard(_w45Msg));
        var msg = P(""); msg.Name = "office-msg";
        // A2 (07.11.2026): Startseite wie in einem modernen Office-Paket – große „Neu“-Karten mit Symbol, darunter „Zuletzt bearbeitet“
        var row = new WrapPanel { Name = "office-new-cards" };
        Button NewCard(string name, string icon, string c1, string c2, string title, string sub, Action run)
        {
            var b = new Button { Name = name, Width = 210, Height = 124, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(14),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 6, Children = { Chip(icon, c1, c2, 38),
                    new TextBlock { Text = title, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = sub, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => run(); Hover(b); return b;
        }
        row.Children.Add(NewCard("office-text", Mdi.FileDocumentEdit, "#8FC2FF", "#2F6FE0", "Dokument", "Texte schreiben und gestalten – Trendo Text", () => Show("text")));
        row.Children.Add(NewCard("office-tabelle", Mdi.ViewGrid, "#86EFAC", "#15803D", "Tabelle", "Rechnen, Listen, Diagramme", () => Show("tabelle")));
        row.Children.Add(NewCard("office-deck", Mdi.PlayBox, "#FDBA74", "#C2410C", "Präsentation", "Folien mit Vorlagen", () => Show("praesentation")));
        row.Children.Add(NewCard("office-pdf", Mdi.FileDocument, "#FCA5A5", "#B91C1C", "PDF", "Ansehen, ausfüllen, zusammenführen", () => Show("pdf")));
        row.Children.Add(NewCard("office-brief", Mdi.EmailEdit, "#F5D27A", "#9C7419", "Brief", "Mit Vorlagen und A4-Vorschau", () => Show("brief")));
        foreach (var (file, label) in new[] { ("DIN5008-Privatbrief-FormB.docx", "Privatbrief (Word)"), ("DIN5008-Geschaeftsbrief-FormB.docx", "Geschäftsbrief (Word)") })
            row.Children.Add(NewCard("office-new-" + (file.Contains("Privat") ? "privat" : "geschaeft"), Mdi.FileDocumentOutline, "#CBD5E1", "#475569", label, "DIN-5008-Vorlage als .docx", () =>
            {
                var dir = Path.Combine(ProjectsRoot, "Dokumente"); Directory.CreateDirectory(dir);
                var target = Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd-HHmm}-{Path.GetFileNameWithoutExtension(file)}.docx");
                using (var s = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/templates/{file}"))) using (var f = File.Create(target)) s.CopyTo(f);
                _core.Audit.Append(Principal.LocalOwner.Name, "office.new", Path.GetFileName(target));
                msg.Text = $"Angelegt: {Path.GetFileName(target)}"; Launch(target);
            }));
        sp.Children.Add(new TextBlock { Text = "NEU", Foreground = Muted, FontSize = 11, FontWeight = FontWeight.Bold, LetterSpacing = 0.6, Margin = new Thickness(2, 4, 0, 4) });
        var openIn = OpenInButton("libreoffice", () => LatestOfficeDoc(), "office");
        sp.Children.Add(row); sp.Children.Add(new WrapPanel { Children = { openIn } }); sp.Children.Add(msg);
        sp.Children.Add(new TextBlock { Text = "ZULETZT BEARBEITET", Foreground = Muted, FontSize = 11, FontWeight = FontWeight.Bold, LetterSpacing = 0.6, Margin = new Thickness(2, 10, 0, 4) });
        var soffice = ToolPath("libreoffice");
        sp.Children.Add(P(soffice is null ? "LibreOffice ist nicht installiert – Öffnen nutzt dein Standardprogramm, PDF-Export ist aus." : "Zuletzt bearbeitet (Projektordner und Dokumente). „PDF“ wandelt mit LibreOffice im Hintergrund um.", null, 11));
        var docs = UserFolder(Environment.SpecialFolder.MyDocuments);
        var i = 0;
        foreach (var f in FileShelf.Recent([ProjectsRoot, docs], FileShelf.Office, 30))
        {
            var r = (Border)FileRow(f, i, "doc");
            if (Trendo.Core.Office.OfficeConvert.CanConvert(f.FullName))
            {
                var pdf = new Button { Name = $"doc-pdf-{i}", Content = "PDF", FontSize = 11, Padding = new Thickness(10, 2) };
                pdf.Click += async (_, _) =>
                {
                    msg.Text = $"Erstelle PDF aus {f.Name} …";
                    var (ok, m, p) = await Task.Run(() => { try { var o = Trendo.Core.Office.OfficeConvert.ToPdf(f.FullName); return (true, "PDF erstellt (ohne Zusatzprogramm)", o); } catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or InvalidOperationException or System.Xml.XmlException) { return (false, "PDF ging nicht: " + ex.Message, ""); } });
                    msg.Text = ok ? $"{m}: {Path.GetFileName(p)}" : m; _core.Audit.Append(Principal.LocalOwner.Name, "office.pdf", $"{f.Name} ok={ok}"); };
                ((StackPanel)((Grid)r.Child!).Children[1]).Children.Insert(1, pdf);
            }
            sp.Children.Add(r); i++;
        }
        if (i == 0) sp.Children.Add(EmptyState("office-empty", "ordner", "Noch keine Dokumente", "Oben ein neues Dokument, eine Tabelle oder Präsentation anlegen – zuletzt bearbeitete Dateien erscheinen dann hier.")); // G6
        return AppsPage("Dokumente", sp);
    }

    // ---------- STUDIO-LETTER-01: Briefstudio ----------
    private LetterDraft? _letter;
    /// <summary>Zuletzt erzeugtes PDF des Briefs (für Senden) und ob die Senden-Karte offen ist.</summary>
    private string? _letterPdf; private bool _letterSendOpen;
    public string? LetterPdfFile => _letterPdf;
    /// <summary>Zuletzt erzeugter Umschlag (DIN lang) zum Brief.</summary>
    private string? _letterEnvelope;
    public string? LetterEnvelopeFile => _letterEnvelope;

    /// <summary>Exportieren: Ziel und Format wählen (PDF oder Word).</summary>
    private async Task<string?> PickLetterTarget(string stem)
    {
        var req = new PickRequest("letter-export", false, false, ["pdf", "docx"]);
        if (PickOverride is { } fake) return (await fake(req)) is [var f, ..] ? f : null;
        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions { Title = "Brief exportieren", SuggestedFileName = stem, DefaultExtension = "pdf",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("PDF") { Patterns = ["*.pdf"] }, new Avalonia.Platform.Storage.FilePickerFileType("Word-Dokument") { Patterns = ["*.docx"] }] });
        return file is null ? null : Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(file);
    }

    /// <summary>Senden (Wunsch 03.11.2026): per E-Mail mit dem PDF im Anhang – das Mailprogramm öffnet den Entwurf, abschicken tust du selbst –,
    /// oder ausdrucken und per Post. Trendo verschickt nichts von sich aus.</summary>
    private Control LetterSendCard(LetterDraft d, string pdf)
    {
        var emails = Mail.Contacts().Where(c => c.Email.Trim().Length > 0 && d.Recipient.Contains(c.Name.Trim().Length > 0 ? c.Name.Trim() : "\u0000", StringComparison.OrdinalIgnoreCase)).Select(c => c.Email.Trim()).Distinct().ToList();
        var to = new TextBox { Name = "letter-send-to", Text = string.Join(", ", emails), PlaceholderText = "E-Mail-Adresse des Empfängers (kannst du auch im Mailprogramm eintragen)", MinWidth = 300 };
        var mail = new Button { Name = "letter-send-mail", Content = "Per E-Mail senden …", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(12, 6) };
        ToolTip.SetTip(mail, "Öffnet dein Mailprogramm mit dem Brief als PDF im Anhang – dort prüfst du und klickst auf „Senden“");
        mail.Click += (_, _) =>
        {
            var body = $"{(d.Salutation.Trim().Length > 0 ? d.Salutation.Trim() : "Guten Tag,")}\n\nanbei sende ich Ihnen mein Schreiben „{d.Subject.Trim()}“ als PDF.\n\n{d.Closing.Trim()}\n{d.SenderName.Trim()}";
            var draft = new MailDraft((to.Text ?? "").Trim(), d.Subject.Trim(), body, "", [pdf]);
            LastDraftFile = Mail.SaveDraft(draft, MailDraftFolder, DateTimeOffset.Now);
            Launch(LastDraftFile);
            _core.Audit.Append(Principal.LocalOwner.Name, "letter.send.mail", Path.GetFileName(pdf));
            _letterMsg = "✓ Dein Mailprogramm öffnet den Entwurf mit dem PDF im Anhang – dort prüfen und auf „Senden“ klicken."; _letterSendOpen = false; Show("brief");
        };
        var print = new Button { Name = "letter-send-print", Content = "Drucken (per Post)", Padding = new Thickness(12, 6) };
        ToolTip.SetTip(print, "Öffnet das PDF – dort drucken, an den Marken falten und in einen Fensterumschlag stecken");
        print.Click += (_, _) => { Launch(pdf); _letterMsg = "PDF geöffnet – drucken, an den Falzmarken falten, in den Fensterumschlag."; _letterSendOpen = false; Show("brief"); };
        // Umschlag für genau diesen Brief wie „Umschläge“ in Word: DIN lang, Absender klein oben links, Anschrift im Empfängerfeld
        var envelope = new Button { Name = "letter-send-envelope", Content = "Umschlag (DIN lang)", Padding = new Thickness(12, 6) };
        ToolTip.SetTip(envelope, "Für Briefe ohne Fensterumschlag: Umschlag mit Absender und Anschrift als PDF zum Bedrucken");
        envelope.Click += (_, _) =>
        {
            if (d.Recipient.Trim().Length == 0) { _letterMsg = "Für den Umschlag fehlt die Anschrift des Empfängers."; Show("brief"); return; }
            var sender = string.Join("\n", new[] { d.SenderName, d.SenderStreet, d.SenderCity }.Select(x => x.Trim()).Where(x => x.Length > 0));
            var f = Path.Combine(Path.GetDirectoryName(pdf)!, Path.GetFileNameWithoutExtension(pdf) + " – Umschlag.pdf");
            Trendo.Core.Office.TextTools.Envelopes(sender, [d.Recipient.Trim()], f);
            _letterEnvelope = f; Launch(f);
            _core.Audit.Append(Principal.LocalOwner.Name, "letter.envelope", Path.GetFileName(f));
            _letterMsg = $"✓ Umschlag gespeichert: {Path.GetFileName(f)} – im Drucker als Umschlag DIN lang einlegen."; Show("brief");
        };
        var folder = new Button { Name = "letter-send-folder", Content = "Ordner öffnen", Padding = new Thickness(12, 6) };
        folder.Click += (_, _) => Launch(Path.GetDirectoryName(pdf)!);
        var close = new Button { Name = "letter-send-close", Content = "Schließen", Padding = new Thickness(12, 6) };
        close.Click += (_, _) => { _letterSendOpen = false; Show("brief"); };
        var row = new WrapPanel { Children = { mail, Sp(print), Sp(envelope), Sp(folder), Sp(close) } };
        if (_case is not null) { var rel = new Button { Name = "letter-send-case", Content = "Zur Freigabe in der Akte", Padding = new Thickness(12, 6) }; rel.Click += (_, _) => Show("ausgabe"); row.Children.Add(Sp(rel)); }
        return new Border { Name = "letter-send-card", Background = TileBg, BorderBrush = Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10), Margin = new Thickness(0, 4, 0, 8),
            Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = "Brief senden", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                P($"Fertig als PDF: {Path.GetFileName(pdf)}. Trendo schickt nichts von sich aus – du entscheidest im Mailprogramm oder druckst ihn aus.", null, 11.5),
                to, row } } };
    }
    public LetterDraft Letter => _letter ??= NewLetter();
    private string _letterMsg = "";

    private LetterDraft NewLetter() => new() { SenderName = _settings.Name, SenderStreet = _settings.SenderStreet, SenderCity = _settings.SenderCity, SenderContact = _settings.SenderContact,
        Place = System.Text.RegularExpressions.Regex.Replace(_settings.SenderCity, @"^\s*\d{4,5}\s*", ""), SignatureFile = _settings.LetterSignature };

    private Control LetterView()
    {
        var d = Letter;
        var form = new StackPanel { Spacing = 4 };
        var hiddenCats = new StackPanel { Height = 0, Opacity = 0, IsHitTestVisible = false };
        foreach (var c in Letters.Categories)
        {
            var b = new Button { Name = "letter-cat-" + c.Id, Content = c.Label, IsTabStop = false, IsHitTestVisible = false, Height = 0, Padding = new Thickness(0), Opacity = 0 };
            b.Click += (_, _) =>
            {
                Letters.ApplyTemplate(d, c.Id);
                _letterMsg = c.Id == "frei" ? "Freier Brief: ohne Vorlage. Betreff und Text schreibst du selbst." : $"Vorlage „{c.Label}“ eingesetzt. Passe die eckigen Klammern an.";
                Show("brief");
            };
            hiddenCats.Children.Add(b);
        }
        // Wunsch 02.11.2026: Briefe ganz einfach – Assistent in 3 Schritten (Was? → Von wem an wen? → Text & Design); alles Weitere unter „Mehr“
        StackPanel Step(int n, string title) => new() { Name = $"letter-wiz-{n}", Spacing = 4, IsVisible = _letterAll || _letterStep == n, Children = { H(title, 15) } };
        var s1 = Step(1, "1 · Was möchtest du schreiben?"); var s2 = Step(2, "2 · Von wem an wen?"); var s3 = Step(3, "3 · Text und Design");
        Button Nav(string name, string label, int to, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, Padding = new Thickness(14, 6), Margin = new Thickness(0, 8, 6, 0), CornerRadius = new CornerRadius(10) };
            if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
            b.Click += (_, _) => { _letterStep = to; _letterMsg = to switch { 1 => "Schritt 1 von 3: Was möchtest du schreiben?", 2 => "Schritt 2 von 3: Von wem an wen?", _ => "Schritt 3 von 3: Text und Design – dann „Fertig“." }; Show("brief"); }; return b; // sichtbar, wo man ist
        }
        var wiz = new WrapPanel { Name = "letter-wizard" };
        foreach (var (n, label) in new[] { (1, "1  Was?"), (2, "2  An wen?"), (3, "3  Text & Design") })
        {
            var on = !_letterAll && _letterStep == n;
            var b = new Button { Name = $"letter-wiz-tab-{n}", Content = label, Padding = new Thickness(14, 6), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(16), Background = on ? Accent : TileBg, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            var nn = n; b.Click += (_, _) => { _letterStep = nn; _letterAll = false; Show("brief"); }; wiz.Children.Add(b);
        }
        var all = new CheckBox { Name = "letter-wiz-all", Content = "Alles auf einer Seite", IsChecked = _letterAll, Foreground = Text, Margin = new Thickness(6, 0, 6, 6) };
        all.IsCheckedChanged += (_, _) => { _letterAll = all.IsChecked == true; Show("brief"); };
        var clear = new Button { Name = "letter-clear", Content = "Alles leeren", Margin = new Thickness(0, 0, 0, 6) };
        clear.Click += (_, _) =>
        {
            var before = System.Text.Json.JsonSerializer.Serialize(d);
            _letter = NewLetter(); _letterSaved = false; _letterStep = 1; _letterGallery = null;
            Toast("Alle Eingaben geleert.", ("Rückgängig", () => { if (System.Text.Json.JsonSerializer.Deserialize<LetterDraft>(before) is { } old) { _letter = old; Show("brief"); } }));
            Show("brief");
        };
        wiz.Children.Add(all); wiz.Children.Add(clear);
        form.Children.Add(wiz);
        form.Children.Add(s1); form.Children.Add(s2); form.Children.Add(s3);
        s1.Children.Add(hiddenCats);
        var galleryOpen = LetterGalleryOpen(d);
        var galleryToggle = new Button { Name = "letter-gallery-toggle", Content = galleryOpen ? "Vorlagen ausblenden" : $"Vorlage: {Letters.Category(d.Category).Label} – andere wählen …", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
        galleryToggle.Click += (_, _) => { _letterGallery = !galleryOpen; Show("brief"); };
        s1.Children.Add(FieldLabel("Vorlage")); s1.Children.Add(galleryToggle);
        if (galleryOpen) s1.Children.Add(LetterGallery(d));
        if (Letters.Category(d.Category).Legal)
            s1.Children.Add(P("Hinweis: Trendo hilft beim Formulieren, ersetzt aber keine Rechtsberatung. Fristen und Ansprüche bitte selbst prüfen oder beraten lassen.", Brush.Parse("#FACC15"), 11));
        s1.Children.Add(Nav("letter-wiz-next-1", "Weiter →", 2, true));

        TextBox Field(string name, string label, string value, Action<string> set, bool multi = false)
        {
            var t = new TextBox { Name = "letter-" + name, Text = value, PlaceholderText = label, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 72 : 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            t.TextChanged += (_, _) => { set(t.Text ?? ""); _letterSync?.Invoke(); }; return t;
        }
        s2.Children.Add(FieldLabel("Absender (wird gemerkt)"));
        s2.Children.Add(Field("sender-name", "Dein Name", d.SenderName, v => d.SenderName = v));
        s2.Children.Add(Field("sender-street", "Straße und Hausnummer", d.SenderStreet, v => d.SenderStreet = v));
        s2.Children.Add(Field("sender-city", "PLZ und Ort", d.SenderCity, v => d.SenderCity = v));
        s2.Children.Add(Field("sender-contact", "Telefon / E-Mail (optional)", d.SenderContact, v => d.SenderContact = v));
        s2.Children.Add(FieldLabel("Empfänger"));
        s2.Children.Add(Field("recipient", "Firma, Name, Straße, PLZ Ort", d.Recipient, v => d.Recipient = v, multi: true));
        s2.Children.Add(new WrapPanel { Children = { Nav("letter-wiz-back-2", "← Zurück", 1), Nav("letter-wiz-next-2", "Weiter →", 3, true) } });
        // Wunsch 03.11.2026: Designs wie in Canva – gleich oben erreichbar
        var toDesign = new Button { Name = "letter-goto-design", Content = "Design wählen (" + LetterDesigns.Styles.Length + " Vorlagen)", Margin = new Thickness(0, 4, 0, 0) };
        toDesign.Click += (_, _) =>
        {
            LetterNote("Vorlage anklicken – rechts siehst du sofort, wie dein Brief damit aussieht. Farbe, Kopf und Schrift kannst du danach ändern.");
            Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this).OfType<Control>().FirstOrDefault(c => c.Name == "letter-design-gallery")?.BringIntoView();
        };
        s3.Children.Add(toDesign);
        s3.Children.Add(FieldLabel("Betreff"));
        var subjectBox = Field("subject", "Betreff", d.Subject, v => d.Subject = v);
        s3.Children.Add(subjectBox);
        s3.Children.Add(FieldLabel("Anrede"));
        s3.Children.Add(Field("salutation", "Anrede", d.Salutation, v => d.Salutation = v));
        s3.Children.Add(FieldLabel("Inhalt"));
        var body = Field("body", "Text", d.Body, v => d.Body = v, multi: true); body.MinHeight = 160;
        s3.Children.Add(body);
        s3.Children.Add(LetterSpellBar(d, subjectBox, body)); // F3: Rechtschreibung beim Tippen
        s3.Children.Add(P("Der Brief wird nach DIN 5008 gesetzt.", Muted, 11));
        s3.Children.Add(FieldLabel("Gruß"));
        s3.Children.Add(Field("closing", "Gruß", d.Closing, v => d.Closing = v));
        s3.Children.Add(LetterSignBox(d)); // F3: Unterschrift zeichnen oder Bild
        var more = new StackPanel { Spacing = 4 };
        more.Children.Add(FieldLabel("Ort und Anlagen"));
        more.Children.Add(Field("place", "Ort für das Datum", d.Place, v => d.Place = v));
        more.Children.Add(Field("reference", "Kundennummer oder Aktenzeichen (optional)", d.Reference, v => d.Reference = v));
        more.Children.Add(Field("attachments", "Anlagen, eine pro Zeile (optional)", d.Attachments, v => d.Attachments = v, multi: true));

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
            var blocking = Letters.Validate(d).Where(x => x.Blocking).ToList();
            if (blocking.Count > 0)
            {
                // Wunsch 03.11.2026: nicht still scheitern – sagen, was fehlt, und hinspringen
                var b0 = blocking[0];
                var hint = $"Noch nicht fertig – {b0.Field}: {b0.Message}" + (blocking.Count > 1 ? $" (und {blocking.Count - 1} weitere Punkte, siehe „Brief prüfen“)" : "");
                _letterMsg = msg.Text = hint; Toast(hint);
                var field = b0.Field switch { "Absender" => "letter-sender-name", "Empfänger" => "letter-recipient", "Betreff" => "letter-subject", "Text" => "letter-body", _ => "letter-subject" };
                LetterGo(field, hint);
                return null;
            }
            _settings.SenderStreet = d.SenderStreet; _settings.SenderCity = d.SenderCity; _settings.SenderContact = d.SenderContact;
            if (string.IsNullOrWhiteSpace(_settings.Name)) _settings.Name = d.SenderName;
            SaveSettings();
            var (docx, _, v) = new LetterArchive(ProjectsRoot).Save(d);
            _letterSaved = true; _letterStepSync?.Invoke();
            _core.Audit.Append(Principal.LocalOwner.Name, "letter.save", $"{Path.GetFileName(docx)} v{v}");
            msg.Text = _letterMsg = $"{verb}: {Path.GetFileName(docx)}{(v > 1 ? $" (Version {v})" : "")}";
            if (asPdf)
            {
                // Wunsch 03.11.2026: „Als PDF“ ohne LibreOffice – Trendo setzt das PDF selbst (gleiche Lage wie die Vorschau)
                var pdfFile = Path.ChangeExtension(docx, ".pdf");
                await Task.Run(() => LetterPdf.Write(d.Copy(), pdfFile));
                _letterPdf = pdfFile;
                msg.Text = _letterMsg = $"{verb}: {Path.GetFileName(docx)} und {Path.GetFileName(pdfFile)}";
                NotifyFileResult(_letterMsg);
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
        var pdf = Act("letter-pdf", "Als PDF", Mdi.FileDocumentOutline);
        ToolTip.SetTip(pdf, "Brief als PDF speichern (im Ordner „Briefe“)");
        var send = Act("letter-send", "Senden", Mdi.Send);
        save.Click += async (_, _) => await SafeSave(false, "Gespeichert in Briefe");
        ToolTip.SetTip(export, "Speichern unter … – Ort und Format (PDF oder Word) selbst wählen");
        export.Click += async (_, _) =>
        {
            try
            {
                if (await Save(false, "Gespeichert") is null) return;
                var target = await PickLetterTarget(Letters.FileStem(d));
                if (target is null) { msg.Text = "Exportieren abgebrochen."; return; }
                if (Path.GetExtension(target).Equals(".docx", StringComparison.OrdinalIgnoreCase)) Letters.WriteDocx(d.Copy(), target);
                else { if (!target.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) target += ".pdf"; await Task.Run(() => LetterPdf.Write(d.Copy(), target)); }
                _core.Audit.Append(Principal.LocalOwner.Name, "letter.export", Path.GetFileName(target));
                msg.Text = _letterMsg = "Exportiert: " + target; NotifyFileResult(_letterMsg);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { msg.Text = "Exportieren fehlgeschlagen: " + ex.Message; }
        };
        pdf.Click += async (_, _) => await SafeSave(true, "Als PDF gespeichert");
        ToolTip.SetTip(send, "Per E-Mail (PDF im Anhang) oder zum Drucken – abschicken tust du selbst");
        send.Click += async (_, _) =>
        {
            await SafeSave(true, "Bereit zum Senden");
            if (_letterPdf is null || !File.Exists(_letterPdf)) return;
            _letterSendOpen = true; Show("brief");
        };
        improve.Click += async (_, _) =>
        {
            improve.IsEnabled = false; suggestion.Children.Clear(); suggestion.Children.Add(P("Die KI schreibt …"));
            using var kiLabel = Trendo.Core.KiActivityHub.Label("Brief verbessern"); var ans = await _ollama.ChatAsync(KiModelName, "Verbessere diesen Brieftext für einen deutschen Brief nach DIN 5008: klar, höflich, sachlich. Ändere keine Fakten, Zahlen, Daten oder Namen. Lass Platzhalter in eckigen Klammern stehen. Gib nur den verbesserten Text ohne Anrede und Gruß zurück.\n\nBetreff: " + d.Subject + "\n\n" + d.Body);
            suggestion.Children.Clear();
            var take = new Button { Name = "letter-ki-take", Content = "Vorschlag übernehmen", Background = Accent };
            take.Click += (_, _) => { d.Body = ans; _core.Audit.Append(Principal.LocalOwner.Name, "letter.ki_accept", d.Subject); Show("brief"); };
            suggestion.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 6, Children = { P("Vorschlag der KI (bitte prüfen):", null, 11), P(ans, Text, 12), take } } });
            improve.IsEnabled = true;
        };
        var reset = new Button { Name = "letter-new", Content = "Neuer Brief" }; reset.Click += (_, _) => { _letter = NewLetter(); _letterGallery = null; _letterSaved = false; _letterStep = 1; _letterMsg = "Neuer Brief. Betreff und Text schreibst du selbst."; _letterPreview = true; Show("brief"); };
        s3.Children.Add(new WrapPanel { Margin = new Thickness(0, 8, 0, 0), Children = { check, Sp(improve), Sp(reset) } });
        s3.Children.Add(issues); s3.Children.Add(suggestion);
        s3.Children.Add(FieldLabel("Design")); s3.Children.Add(LetterDesignBox(d)); // Wunsch 02.11.2026: Bewerbung & Co. zeitgemäß gestalten
        var done = new Button { Name = "letter-wiz-done", Content = "✓ Fertig – Brief speichern", Padding = new Thickness(18, 8), Margin = new Thickness(0, 10, 6, 0), CornerRadius = new CornerRadius(10), Background = Accent, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold };
        done.Click += async (_, _) => await SafeSave(pdf.IsEnabled, "Fertig – gespeichert");
        s3.Children.Add(new WrapPanel { Children = { Nav("letter-wiz-back-3", "← Zurück", 2), done } });
        form.Children.Insert(1, msg);
        if (_letterSendOpen && _letterPdf is { } sentPdf && File.Exists(sentPdf)) form.Children.Insert(2, LetterSendCard(d, sentPdf));
        form.Children.Add(new Expander { Name = "letter-more", Header = "Mehr – Ort, Anlagen, Frist, Serienbrief, Druck", IsExpanded = _letterAll, HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 4, Children = { more, LetterTools(d) } } });
        var arch = new LetterArchive(ProjectsRoot).List().Take(8).ToList();
        if (arch.Count > 0)
        {
            form.Children.Add(H("Zuletzt geschrieben", 14));
            var i = 0;
            foreach (var f in arch)
            {
                var r = (Border)FileRow(new FileInfo(f), i, "letter-arch");
                var edit = new Button { Name = $"letter-edit-{i}", Content = "Weiterschreiben", FontSize = 11, Padding = new Thickness(10, 2), IsEnabled = LetterArchive.Load(f) is not null };
                edit.Click += (_, _) => { if (LetterArchive.Load(f) is { } old) { _letter = old; _letter.Date = DateTime.Today; _letterStep = 3; Show("brief"); } };
                var del = new Button { Name = $"letter-del-{i}", Content = "🗑", FontSize = 11, Padding = new Thickness(8, 2) };
                ToolTip.SetTip(del, "Brief löschen (kommt in den Papierkorb, zurückholbar)");
                del.Click += (_, _) => LetterTrashed(new LetterArchive(ProjectsRoot).Delete(f), "brief");
                ((StackPanel)((Grid)r.Child!).Children[1]).Children.Insert(0, edit);
                ((StackPanel)((Grid)r.Child!).Children[1]).Children.Insert(1, del);
                form.Children.Add(r); i++;
            }
        }

        var preview = LetterPreview(d);
        var formScroll = new ScrollViewer { Content = new Border { Background = Brush.Parse(C("#44222222", "#B3FFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(12), Margin = new Thickness(0, 0, 8, 0), Child = form } };
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
        var stack = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        var steps = LetterSteps(d);
        stack.Children.Add(steps); Grid.SetRow(toggle, 1); stack.Children.Add(toggle); Grid.SetRow(grid, 2); stack.Children.Add(grid);
        return LetterShell(stack, actions);
    }

    private static TextBlock FieldLabel(string text) => new() { Text = text, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 8, 0, 2) };

    private static Control Sp(Control c) { c.Margin = new Thickness(6, 0, 0, 6); return c; }
    /// <summary>T-0203: zuletzt angelegtes Dokument im Ordner „Dokumente“.</summary>
    private string? LatestOfficeDoc()
    {
        var dir = Path.Combine(ProjectsRoot, "Dokumente");
        return Directory.Exists(dir) ? Directory.GetFiles(dir).Where(f => ExternalApps.Accepts(ExternalApps.Get("libreoffice"), f)).OrderByDescending(File.GetLastWriteTime).FirstOrDefault() : null;
    }
}
