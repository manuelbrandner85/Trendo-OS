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
        var sp = new StackPanel { Spacing = 6 };
        // Briefart
        var cats = new WrapPanel();
        foreach (var c in Letters.Categories)
        {
            var b = new Button { Name = "letter-cat-" + c.Id, Content = c.Label, Margin = new Thickness(0, 0, 6, 6), Background = d.Category == c.Id ? Accent : null };
            b.Click += (_, _) =>
            {
                Letters.ApplyTemplate(d, c.Id);
                if (c.Id == "frei") { d.Subject = ""; d.Body = ""; _letterMsg = "Freier Brief: ohne Vorlage. Betreff und Text schreibst du selbst."; }
                else _letterMsg = $"Vorlage „{c.Label}“ eingesetzt. Passe die eckigen Klammern an.";
                Show("brief");
            };
            cats.Children.Add(b);
        }
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("1. Welche Art Brief?", 14), cats } });
        if (Letters.Category(d.Category).Legal)
            sp.Children.Add(P("Hinweis: Trendo hilft beim Formulieren, ersetzt aber keine Rechtsberatung. Fristen und Ansprüche bitte selbst prüfen oder beraten lassen.", Brush.Parse("#FACC15"), 11));

        TextBox Field(string name, string label, string value, Action<string> set, bool multi = false, double w = 300)
        {
            var t = new TextBox { Name = "letter-" + name, Text = value, PlaceholderText = label, Width = w, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 80 : 0, HorizontalAlignment = HorizontalAlignment.Left };
            t.TextChanged += (_, _) => set(t.Text ?? ""); return t;
        }
        var sender = new StackPanel { Spacing = 4, Children = {
            Field("sender-name", "Dein Name", d.SenderName, v => d.SenderName = v), Field("sender-street", "Straße und Hausnummer", d.SenderStreet, v => d.SenderStreet = v),
            Field("sender-city", "PLZ und Ort", d.SenderCity, v => d.SenderCity = v), Field("sender-contact", "Telefon / E-Mail (optional)", d.SenderContact, v => d.SenderContact = v) } };
        var rec = new StackPanel { Spacing = 4, Children = {
            Field("recipient", "Empfänger: Firma\nFrau/Herrn Name\nStraße Nr.\nPLZ Ort", d.Recipient, v => d.Recipient = v, multi: true),
            Field("reference", "Kunden-/Vertragsnummer, Aktenzeichen (optional)", d.Reference, v => d.Reference = v), Field("place", "Ort für das Datum", d.Place, v => d.Place = v) } };
        sp.Children.Add(H("2. Von wem an wen?", 14));
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { sender, rec } });
        sp.Children.Add(H("3. Was steht drin?", 14));
        sp.Children.Add(Field("subject", "Betreff", d.Subject, v => d.Subject = v, w: 616));
        sp.Children.Add(Field("salutation", "Anrede", d.Salutation, v => d.Salutation = v, w: 616));
        var body = Field("body", "Text", d.Body, v => d.Body = v, multi: true, w: 616); body.MinHeight = 140;
        sp.Children.Add(body);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { Field("closing", "Gruß", d.Closing, v => d.Closing = v), Field("attachments", "Anlagen (eine pro Zeile, optional)", d.Attachments, v => d.Attachments = v, multi: true) } });

        // Prüfen, KI, Speichern
        var issues = new StackPanel { Name = "letter-issues", Spacing = 2 };
        void Check()
        {
            issues.Children.Clear();
            var l = Letters.Validate(d);
            issues.Children.Add(P(l.Count == 0 ? "✓ Alles da – der Brief kann so raus." : $"{l.Count(x => x.Blocking)} Pflichtpunkte, {l.Count(x => !x.Blocking)} Hinweise:", l.Any(x => x.Blocking) ? Brush.Parse("#F87171") : Brush.Parse("#4ADE80"), 12));
            foreach (var x in l) issues.Children.Add(P($"{(x.Blocking ? "✗" : "•")} {x.Field}: {x.Message}", x.Blocking ? Text : null, 11.5));
        }
        var check = new Button { Name = "letter-check", Content = "Brief prüfen" }; check.Click += (_, _) => Check();
        var save = new Button { Name = "letter-save", Content = "Als Word-Datei speichern", Background = Accent };
        var pdf = new Button { Name = "letter-pdf", Content = "Speichern + PDF", IsEnabled = ToolPath("libreoffice") is not null };
        if (!pdf.IsEnabled) ToolTip.SetTip(pdf, "Braucht LibreOffice (Zusatzprogramme)");
        var improve = new Button { Name = "letter-ki", Content = "Text von der KI verbessern lassen", IsEnabled = _kiConnected };
        if (!_kiConnected) ToolTip.SetTip(improve, "Die lokale KI ist nicht bereit");
        var msg = P(_letterMsg, Text); msg.Name = "letter-msg";
        var suggestion = new StackPanel { Spacing = 4 };
        async Task<string?> Save(bool asPdf)
        {
            Check();
            if (Letters.Validate(d).Any(x => x.Blocking)) { msg.Text = "Bitte zuerst die Pflichtpunkte ergänzen."; return null; }
            _settings.SenderStreet = d.SenderStreet; _settings.SenderCity = d.SenderCity; _settings.SenderContact = d.SenderContact;
            if (string.IsNullOrWhiteSpace(_settings.Name)) _settings.Name = d.SenderName;
            SaveSettings();
            var (docx, _, v) = new LetterArchive(ProjectsRoot).Save(d);
            _core.Audit.Append(Principal.LocalOwner.Name, "letter.save", $"{Path.GetFileName(docx)} v{v}");
            msg.Text = _letterMsg = $"Gespeichert in Briefe: {Path.GetFileName(docx)}{(v > 1 ? $" (Version {v})" : "")}";
            if (asPdf && ToolPath("libreoffice") is { } so)
            {
                msg.Text = "Erstelle PDF …";
                var (ok, m, p) = await PdfExport.ConvertAsync(new RealProcessRunner(), so, docx);
                msg.Text = _letterMsg = ok ? $"Gespeichert: {Path.GetFileName(docx)} und {Path.GetFileName(p)}" : $"Word-Datei gespeichert, PDF fehlgeschlagen: {m}";
            }
            return docx;
        }
        async Task SafeSave(bool asPdf) { try { await Save(asPdf); } catch (Exception ex) { msg.Text = $"Speichern fehlgeschlagen: {ex.Message}"; CrashLog.Write(_core.DataRoot, ex, "Briefstudio"); } }
        save.Click += async (_, _) => await SafeSave(false);
        pdf.Click += async (_, _) => await SafeSave(true);
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
        var reset = new Button { Name = "letter-new", Content = "Neuer Brief" }; reset.Click += (_, _) => { _letter = NewLetter(); _letterMsg = "Neuer Brief. Betreff und Text schreibst du selbst."; Show("brief"); };
        sp.Children.Add(H("4. Prüfen und speichern", 14));
        sp.Children.Add(new WrapPanel { Children = { check, Sp(save), Sp(pdf), Sp(improve), Sp(reset) } });
        sp.Children.Add(msg); sp.Children.Add(issues); sp.Children.Add(suggestion);

        // Archiv
        var arch = new LetterArchive(ProjectsRoot).List().Take(8).ToList();
        if (arch.Count > 0)
        {
            sp.Children.Add(H("Zuletzt geschrieben", 14));
            var i = 0;
            foreach (var f in arch)
            {
                var r = (Border)FileRow(new FileInfo(f), i, "letter-arch");
                var edit = new Button { Name = $"letter-edit-{i}", Content = "Weiterschreiben", FontSize = 11, Padding = new Thickness(10, 2), IsEnabled = LetterArchive.Load(f) is not null };
                edit.Click += (_, _) => { if (LetterArchive.Load(f) is { } old) { _letter = old; _letter.Date = DateTime.Today; Show("brief"); } };
                ((StackPanel)((Grid)r.Child!).Children[1]).Children.Insert(0, edit);
                sp.Children.Add(r); i++;
            }
        }
        return LetterPage("Schnell schreiben", sp);
    }
    private static Control Sp(Control c) { c.Margin = new Thickness(6, 0, 0, 6); return c; }
}
