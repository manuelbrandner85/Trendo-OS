using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-09: Kontakte, E-Mail-Entwürfe für dein Mailprogramm, E-Mails lesen (.eml). Trendo verschickt nichts selbst.</summary>
public partial class MainWindow
{
    private MailCenter? _mail;
    public MailCenter Mail => _mail ??= new MailCenter(_core.DataRoot, _core.Audit);
    public static string MailDraftFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trendo E-Mail-Entwürfe");
    private string _mailMsg = "", _mailTo = "", _mailSubj = "", _mailBody = ""; private ParsedMail? _mailRead;
    public string? LastDraftFile { get; private set; }

    private void InitSys09()
    {
        _showMap["post"] = MailView; SysTabs.Add(("post", "E-Mail & Kontakte"));
        StudioPages["SYS-09"] = "post";
        ExtraPages.Add(("E-Mail & Kontakte", "post", ["mail", "email", "kontakt", "adresse", "nachricht", "vcard", "eml", "outlook", "thunderbird"]));
    }

    private Control MailView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Schreib hier in Ruhe. Trendo prüft die Nachricht und öffnet sie in deinem Mailprogramm – abschicken tust du selbst.") } };
        sp.Children.Add(H("Neue E-Mail", 14));
        var to = new TextBox { Name = "mail-to", PlaceholderText = "An (E-Mail, mehrere mit Komma)", Text = _mailTo };
        var subj = new TextBox { Name = "mail-subject", PlaceholderText = "Betreff", Text = _mailSubj };
        var body = new TextBox { Name = "mail-body", PlaceholderText = "Nachricht", Text = _mailBody, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
        MailDraft D() { _mailTo = to.Text ?? ""; _mailSubj = subj.Text ?? ""; _mailBody = body.Text ?? ""; return new(_mailTo, _mailSubj, _mailBody); }
        var check = new Button { Name = "mail-check", Content = "Prüfen" };
        check.Click += (_, _) => { var p = MailCenter.Check(D()); _mailMsg = p.Count == 0 ? "✓ Sieht gut aus." : string.Join(" ", p); Show("post"); };
        var open = new Button { Name = "mail-open", Content = "Im Mailprogramm öffnen", Background = Accent };
        open.Click += (_, _) =>
        {
            var d = D(); var p = MailCenter.Check(d);
            if (p.Any(x => x.Contains("fehlt") || x.Contains("gültige"))) { _mailMsg = string.Join(" ", p); Show("post"); return; }
            LastDraftFile = Mail.SaveDraft(d, MailDraftFolder, DateTimeOffset.Now); Launch(LastDraftFile);
            _mailMsg = $"Entwurf gespeichert und geöffnet ({Path.GetFileName(LastDraftFile)}). Prüfen und im Mailprogramm auf „Senden“ klicken.";
            Show("post");
        };
        var link = new Button { Name = "mail-link", Content = "Als Link (Webmail)" };
        link.Click += (_, _) => { var d = D(); Launch(MailCenter.MailTo(d)); _mailMsg = "Dein Standard-Mailprogramm öffnet sich mit der Nachricht."; Show("post"); };
        var ki = new Button { Name = "mail-ki", Content = "Freundlicher formulieren (KI)" };
        ki.Click += async (_, _) =>
        {
            var d = D(); if (string.IsNullOrWhiteSpace(d.Body)) { _mailMsg = "Schreib zuerst etwas."; Show("post"); return; }
            ki.IsEnabled = false;
            var a = await Intel.AskAsync(new KiRequest("Formuliere diese E-Mail freundlicher und klarer, ohne Inhalt zu ändern. Gib nur den neuen Text aus:\n\n" + d.Body, PreferredModel: KiModelName, UseKnowledge: false));
            if (a.Ok) { _mailBody = a.Text; _mailMsg = "Vorschlag der KI eingesetzt – bitte lesen, bevor du sendest."; } else _mailMsg = a.Text;
            Show("post");
        };
        var people = Mail.Contacts();
        var pick = new ComboBox { Name = "mail-pick", PlaceholderText = "Aus Kontakten", ItemsSource = people.Where(c => c.Email != "").Select(c => $"{c.Name} <{c.Email}>").ToList(), MinWidth = 200 };
        pick.SelectionChanged += (_, _) => { if (pick.SelectedIndex < 0) return; var c = people.Where(x => x.Email != "").ElementAt(pick.SelectedIndex); to.Text = string.IsNullOrWhiteSpace(to.Text) ? c.Email : to.Text.TrimEnd(',', ' ') + ", " + c.Email; };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { pick } });
        sp.Children.Add(to); sp.Children.Add(subj); sp.Children.Add(body);
        sp.Children.Add(new WrapPanel { Children = { MSp(check), MSp(open), MSp(link), MSp(ki) } });
        sp.Children.Add(new TextBlock { Name = "mail-msg", Text = _mailMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });

        sp.Children.Add(H($"Kontakte ({people.Count})", 14));
        var cn = new TextBox { Name = "contact-name", PlaceholderText = "Name", Width = 180 }; var ce = new TextBox { Name = "contact-email", PlaceholderText = "E-Mail", Width = 220 }; var cp = new TextBox { Name = "contact-phone", PlaceholderText = "Telefon", Width = 140 };
        var add = new Button { Name = "contact-add", Content = "Hinzufügen" };
        add.Click += (_, _) => { try { Mail.AddContact(cn.Text ?? "", ce.Text ?? "", cp.Text ?? ""); _mailMsg = "Kontakt gespeichert."; } catch (ArgumentException ex) { _mailMsg = ex.Message; } Show("post"); };
        var path = Field("contact-file", "Datei (.vcf zum Einlesen, .eml zum Lesen)", 320);
        var imp = new Button { Name = "contact-import", Content = "Öffnen" };
        imp.Click += (_, _) =>
        {
            var f = path.Text?.Trim().Trim('"') ?? "";
            if (!File.Exists(f)) _mailMsg = "Diese Datei gibt es nicht.";
            else if (f.EndsWith(".eml", StringComparison.OrdinalIgnoreCase)) { _mailRead = MailCenter.ParseEml(File.ReadAllText(f)); _mailMsg = ""; }
            else { var n = Mail.ImportVcf(File.ReadAllText(f)); _mailMsg = $"{n} Kontakt(e) eingelesen."; }
            Show("post");
        };
        var exp = new Button { Name = "contact-export", Content = "Kontakte sichern (.vcf)" };
        exp.Click += (_, _) => { Directory.CreateDirectory(MailDraftFolder); var f = Path.Combine(MailDraftFolder, "kontakte.vcf"); File.WriteAllText(f, Mail.ExportVcf()); _mailMsg = $"Gespeichert: {f}"; Show("post"); };
        sp.Children.Add(new WrapPanel { Children = { MSp(cn), MSp(ce), MSp(cp), MSp(add) } });
        sp.Children.Add(new WrapPanel { Children = { MSp(path), MSp(imp), MSp(exp) } });
        var i = 0;
        foreach (var c in people.Take(50))
        {
            var del = new Button { Name = $"contact-del-{i++}", Content = "Löschen", Padding = new Thickness(8, 2) };
            del.Click += (_, _) => { Mail.RemoveContact(c.Id); Show("post"); };
            sp.Children.Add(Row2(new StackPanel { Children = { new TextBlock { Text = c.Name, Foreground = Text }, P(string.Join(" · ", new[] { c.Email, c.Phone }.Where(x => x != "")), null, 11) } }, del));
        }
        if (_mailRead is { } m)
            sp.Children.Add(WCard(new StackPanel { Name = "mail-read", Spacing = 3, Children = { H(m.Subject, 14), P($"Von {m.From} · an {m.To} · {m.Date}", null, 11), new SelectableTextBlock { Text = m.Body, Foreground = Text, TextWrapping = TextWrapping.Wrap } } }));
        return SysPage("E-Mail & Kontakte", sp);
    }

    private static Control MSp(Control c) { var h = Wahl(c); h.Margin = new Thickness(0, 0, 6, 6); return h; }
}
