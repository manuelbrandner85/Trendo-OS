using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = System.IO.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Adressbuch (Masterprompt 131): Gruppen links, Liste mit Buchstaben in der Mitte, Kontaktkarte rechts; bearbeiten, Foto, Favorit,
/// Doppelte zusammenführen (nach „Ja“), Geburtstage (auch in den Kalender), vCard hinein/hinaus, E-Mail schreiben. Gleicher Speicher wie „E-Mail & Kontakte“.</summary>
public partial class MainWindow
{
    const string AB = "kontakte";
    string _abGroup = "alle", _abFind = ""; string? _abSel, _abMergeAsk, _abDeleteAsk; bool _abEdit;
    public string? AddressSelected => _abSel;
    public bool AddressEditing => _abEdit;
    /// <summary>Für Vorschaubilder: Kontakt wählen (und bearbeiten).</summary>
    public void AddressSelectForPreview(string id, bool edit = false) { _abSel = id; _abEdit = edit; }

    void InitAddressBook()
    {
        _showMap[AB] = AddressBookView; SysTabs.Add((AB, "Adressbuch")); PageModules[AB] = "SYS-09"; // Persönliche Daten (wie Post und Kalender)
        ExtraPages.Add(("Adressbuch", AB, ["adressbuch", "kontakte", "telefonnummer", "geburtstag", "vcard", "visitenkarte", "personen"]));
    }

    /// <summary>Gleicher Text → gleiche Zahl bei jedem Start (string.GetHashCode ist in .NET je Prozess zufällig).</summary>
    internal static int StableHash(string s) { unchecked { var h = 17; foreach (var ch in s) h = h * 31 + ch; return h & 0x7FFFFFFF; } }
    static readonly string[] AbColors = ["#2F86F6", "#22B35E", "#F08A3C", "#7C4DFF", "#E5484D", "#14B8A6", "#EC4899", "#D4A84B"];
    Control Avatar(Contact c, double size)
    {
        var initials = string.Concat(c.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
        var col = Color.Parse(AbColors[StableHash(c.Name) % AbColors.Length]);
        var g = new Grid { Width = size, Height = size, Children = { new Ellipse { Fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(col, 0), new GradientStop(Color.FromRgb((byte)(col.R * 0.55), (byte)(col.G * 0.55), (byte)(col.B * 0.55)), 1) } } },
            new TextBlock { Text = initials, Foreground = Brushes.White, FontSize = size * 0.38, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
        if (c.Photo.Length > 0 && File.Exists(c.Photo))
            try { using var s = File.OpenRead(c.Photo); g.Children.Add(new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), ClipToBounds = true, Child = new Image { Source = Bitmap.DecodeToWidth(s, (int)(size * 2)), Stretch = Stretch.UniformToFill } }); } catch (Exception) { }
        return g;
    }

    string _abSheet = "3475";
    Control AddressBookView()
    {
        var all = Mail.Contacts(); var today = DateOnly.FromDateTime(DateTime.Today);
        var bdays = Mail.Birthdays(today, 30); var dups = Mail.Duplicates(); var groups = Mail.GroupNames();
        // links: Gruppen
        var side = new StackPanel { Name = "ab-side", Spacing = 3, Width = 200 };
        Button Nav(string name, string label, string g, int n)
        {
            var on = _abGroup == g;
            var b = new Button { Name = name, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderBrush = on ? Brush.Parse("#D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(1),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = label, Foreground = Text, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis }, Pos(new TextBlock { Text = n.ToString(), Foreground = Muted, FontSize = 12 }, 1) } } };
            b.Click += (_, _) => { _abGroup = g; _abEdit = false; Show(AB); }; Hover(b); return b;
        }
        side.Children.Add(Nav("ab-g-alle", "Alle Kontakte", "alle", all.Count));
        side.Children.Add(Nav("ab-g-fav", "Favoriten", "fav", all.Count(c => c.Favorite)));
        side.Children.Add(Nav("ab-g-bday", "Bald Geburtstag", "bday", bdays.Count));
        side.Children.Add(Nav("ab-g-dups", "Doppelte", "dups", dups.Sum(d => d.Count - 1)));
        if (groups.Count > 0) side.Children.Add(H("Gruppen", 12));
        for (var i = 0; i < groups.Count; i++) side.Children.Add(Nav($"ab-g-{i}", "▣ " + groups[i].Name, "g:" + groups[i].Name, groups[i].Count));

        // Kopfleiste
        IReadOnlyList<Contact> Shown() => _abGroup switch
        {
            "fav" => Mail.Search(_abFind).Where(c => c.Favorite).ToList(),
            "bday" => bdays.Select(b => b.Contact).Where(c => Mail.Search(_abFind).Any(x => x.Id == c.Id)).ToList(),
            "dups" => dups.SelectMany(d => d).ToList(),
            var g when g.StartsWith("g:") => Mail.Search(_abFind, g[2..]),
            _ => Mail.Search(_abFind),
        };
        var file = new TextBox { Name = "ab-vcf-file", PlaceholderText = "vCard (.vcf) oder CSV (Google, Outlook) einlesen", Width = 300 };
        var sheet = new ComboBox { Name = "ab-label-sheet", ItemsSource = MailCenter.LabelSheets.Select(x => x.Name).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(MailCenter.LabelSheets, x => x.Id == _abSheet)), Width = 300 };
        sheet.SelectionChanged += (_, _) => _abSheet = MailCenter.LabelSheets[Math.Max(0, sheet.SelectedIndex)].Id;
        var head = new WrapPanel { Children = {
            Act("ab-new", "+ Neuer Kontakt", () => { var c = Mail.UpdateContact(new Contact(Guid.NewGuid().ToString("N")[..8], "Neuer Kontakt", "", Groups: _abGroup.StartsWith("g:") ? [_abGroup[2..]] : null)); _abSel = c.Id; _abEdit = true; Show(AB); }, true),
            new Border { Width = 10 }, file,
            Act("ab-vcf-import", "Einlesen", () => Try(AB, () => { var f = (file.Text ?? "").Trim('"', ' '); if (!File.Exists(f)) throw new ArgumentException("Diese Datei gibt es nicht."); var n = Path.GetExtension(f).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? Mail.ImportCsv(File.ReadAllText(f)) : Mail.ImportVcf(File.ReadAllText(f)); Say($"{n} Kontakt(e) eingelesen.", AB); })),
            Act("ab-vcf-export", "Alle als vCard sichern", () => Try(AB, () => { Directory.CreateDirectory(MailDraftFolder); var f = Path.Combine(MailDraftFolder, "kontakte.vcf"); File.WriteAllText(f, Mail.ExportVcf()); NotifyFileResult("Gespeichert: " + f); Say("Gespeichert: " + f, AB); })),
            Act("ab-csv-export", "Als CSV (Google, Outlook)", () => Try(AB, () => { Directory.CreateDirectory(MailDraftFolder); var f = Path.Combine(MailDraftFolder, "kontakte.csv"); File.WriteAllText(f, Mail.ExportCsv(), new System.Text.UTF8Encoding(true)); NotifyFileResult("Gespeichert: " + f); Say("Gespeichert: " + f, AB); })),
            Act("ab-fix-phones", "Nummern vereinheitlichen", () => Try(AB, () => { var n = Mail.FixPhones(); Say(n == 0 ? "Alle Nummern sind schon einheitlich." : $"{n} Kontakt(e): Nummern jetzt einheitlich (+49 …).", AB); })),
            sheet,
            Act("ab-labels", "Etiketten (PDF)", () => Try(AB, () =>
            {
                Directory.CreateDirectory(MailDraftFolder); var f = Path.Combine(MailDraftFolder, "etiketten.pdf"); var list = Shown();
                var pages = MailCenter.WriteLabels(list, f, _abSheet); var n = list.Count(c => c.Street.Trim().Length > 0 && c.City.Trim().Length > 0);
                NotifyFileResult("Gespeichert: " + f); Say($"{n} Etiketten auf {pages} Bogen – {f}" + (n < list.Count ? $" ({list.Count - n} ohne Anschrift übersprungen)" : ""), AB);
            })),
            Act("ab-bday-cal", "Geburtstage in den Kalender", () => Try(AB, () =>
            {
                var n = 0;
                foreach (var c in Mail.Contacts().Where(c => c.Birthday is not null))
                {
                    var next = MailCenter.NextBirthday(c.Birthday!.Value, today).ToDateTime(TimeOnly.MinValue);
                    Cal.Add(new Trendo.Core.Sys.CalEvent("geburtstag-" + c.Id, next, next.AddDays(1), $"{c.Name}", Notes: $"Geburtstag ({c.Birthday:dd.MM.yyyy})", AllDay: true, RemindMin: 24 * 60, Repeat: Trendo.Core.Sys.Repeat.Jaehrlich)); n++;
                }
                Say(n == 0 ? "Noch kein Kontakt hat einen Geburtstag eingetragen." : $"{n} Geburtstage stehen jährlich im Kalender (mit Erinnerung am Vortag).", AB);
            })) } };
        foreach (var c in head.Children.OfType<Control>()) c.Margin = new Thickness(0, 0, 6, 6);

        // Mitte: Liste
        var find = new TextBox { Name = "ab-find", Text = _abFind, PlaceholderText = "Name, Firma, Ort, Nummer …" };
        find.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _abFind = find.Text ?? ""; Show(AB); } };
        var shown = Shown();
        var list = new StackPanel { Name = "ab-list", Spacing = 1 };
        char? letter = null;
        for (var i = 0; i < shown.Count && i < 500; i++)
        {
            var c = shown[i]; var id = c.Id;
            if (_abGroup is "alle" or "fav" || _abGroup.StartsWith("g:"))
            {
                var l = char.ToUpperInvariant(c.Name.FirstOrDefault(char.IsLetter, '#'));
                if (l != letter) { letter = l; list.Children.Add(new TextBlock { Text = l.ToString(), Foreground = GoldText(), FontSize = 12, FontWeight = FontWeight.Bold, Margin = new Thickness(8, 8, 0, 2) }); }
            }
            var sub = _abGroup == "bday" ? bdays.First(b => b.Contact.Id == id) is var bd ? $"{bd.Next:dd.MM.} · wird {bd.Age}" : "" : c.Company.Length > 0 ? c.Company : c.Email.Length > 0 ? c.Email : c.Mobile.Length > 0 ? c.Mobile : c.Phone;
            var on = id == _abSel;
            var b = new Button { Name = $"ab-c-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 5), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderBrush = on ? Brush.Parse("#D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(1),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,10,*,Auto"), Children = { Avatar(c, 34),
                    Pos(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = c.Name, Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = sub, Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 2),
                    Pos(new TextBlock { Text = c.Favorite ? "★" : "", Foreground = Brush.Parse("#FACC15"), VerticalAlignment = VerticalAlignment.Center }, 3) } } };
            b.Click += (_, _) => { _abSel = id; _abEdit = false; _abDeleteAsk = null; Show(AB); }; Hover(b);
            list.Children.Add(b);
        }
        if (shown.Count == 0) list.Children.Add(P(_abGroup switch { "dups" => "Keine doppelten Kontakte.", "bday" => "In den nächsten 30 Tagen hat niemand Geburtstag.", _ => all.Count == 0 ? "Noch keine Kontakte. Oben „+ Neuer Kontakt“ oder eine vCard einlesen." : "Niemand passt zur Suche." }, Muted, 12.5));
        var middle = new StackPanel { Spacing = 6, Width = 330, Children = { find, new ScrollViewer { Content = list, MaxHeight = 560 } } };

        if (_abSel is null && shown.Count > 0 && _abGroup != "dups") _abSel = shown[0].Id; // wie Outlook „Personen“: gleich den ersten Kontakt zeigen
        // rechts: Karte oder Bearbeiten oder Doppelte
        Control right = _abGroup == "dups" ? AbDuplicates(dups) : _abSel is { } sid && all.FirstOrDefault(c => c.Id == sid) is { } sel ? (_abEdit ? AbEditor(sel) : AbCard(sel, today)) : P("Links einen Kontakt anklicken.", Muted, 13);
        var body = new Grid { Name = "ab-book", ColumnDefinitions = new ColumnDefinitions("Auto,12,Auto,14,*"), Children = { Glass2(side, 10), Pos(Glass2(middle, 10), 2), Pos(Glass2(new ScrollViewer { Content = right, MaxHeight = 600 }, 18), 4) } };
        return SysPage("Adressbuch", new StackPanel { Spacing = 8, Children = { head, body } });
    }

    Control AbCard(Contact c, DateOnly today)
    {
        var sp = new StackPanel { Name = "ab-card", Spacing = 10 };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { Avatar(c, 96), new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2, Children = {
            new TextBlock { Name = "ab-card-name", Text = c.Name, Foreground = Text, FontSize = 26, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Text = c.Company, Foreground = GoldText(), FontSize = 14, IsVisible = c.Company.Length > 0 },
            new TextBlock { Text = string.Join(" · ", c.Groups ?? []), Foreground = Muted, FontSize = 12, IsVisible = c.Groups is { Count: > 0 } } } } } });
        var acts = new WrapPanel();
        if (c.Email.Length > 0) acts.Children.Add(Act("ab-mail", "E-Mail schreiben", () => { _mailTo = c.Email; Show("post"); }, true));
        acts.Children.Add(Act("ab-edit", "Bearbeiten", () => { _abEdit = true; Show(AB); }));
        acts.Children.Add(Act("ab-fav", c.Favorite ? "Favorit" : "Favorit", () => { Mail.UpdateContact(c with { Favorite = !c.Favorite }); Show(AB); }, c.Favorite));
        acts.Children.Add(Act("ab-vcard", "Als vCard", () => Try(AB, () => { Directory.CreateDirectory(MailDraftFolder); var f = Path.Combine(MailDraftFolder, DevStudio.SafeName(c.Name) + ".vcf"); File.WriteAllText(f, MailCenter.Vcard(c)); NotifyFileResult("Gespeichert: " + f); Say("Gespeichert: " + f, AB); })));
        foreach (var b in acts.Children.OfType<Control>()) b.Margin = new Thickness(0, 0, 6, 6);
        sp.Children.Add(acts);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*"), Name = "ab-fields" };
        void Row(string label, string value) { if (value.Trim().Length == 0) return; grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); var r = grid.RowDefinitions.Count - 1;
            var l = new TextBlock { Text = label, Foreground = Muted, FontSize = 12.5, Margin = new Thickness(0, 6) }; Grid.SetRow(l, r); grid.Children.Add(l);
            var v = new SelectableTextBlock { Text = value, Foreground = Text, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5) }; Grid.SetRow(v, r); Grid.SetColumn(v, 1); grid.Children.Add(v); }
        Row("E-Mail", c.Email); Row("Mobil", c.Mobile); Row("Telefon", c.Phone); Row("Adresse", string.Join("\n", new[] { c.Street, c.City }.Where(x => x.Length > 0))); Row("Webseite", c.Website);
        if (c.Birthday is { } b2) { var next = MailCenter.NextBirthday(b2, today); Row("Geburtstag", $"{b2:dd.MM.yyyy} · wird am {next:dd.MM.} {next.Year - b2.Year} Jahre alt ({(next.DayNumber - today.DayNumber) switch { 0 => "heute! 🎂", 1 => "morgen", var d => $"in {d} Tagen" }})"); }
        Row("Notiz", c.Note);
        sp.Children.Add(grid);
        if (_abDeleteAsk != c.Id) sp.Children.Add(Act("ab-delete", "Kontakt löschen …", () => { _abDeleteAsk = c.Id; Show(AB); }));
        else sp.Children.Add(new Border { Name = "ab-delete-ask", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 6, Children = {
            new TextBlock { Text = $"„{c.Name}“ wirklich löschen?", Foreground = Text },
            new WrapPanel { Children = { Act("ab-delete-yes", "Ja, löschen", () => { Mail.RemoveContact(c.Id); _abSel = null; _abDeleteAsk = null; Say("Kontakt gelöscht.", AB); }, true), new Border { Width = 6 }, Act("ab-delete-no", "Nein", () => { _abDeleteAsk = null; Show(AB); }) } } } } });
        return sp;
    }

    Control AbEditor(Contact c)
    {
        TextBox T(string name, string label, string value, double w = 250) => new() { Name = "ab-e-" + name, Text = value, PlaceholderText = label, Width = w };
        var name = T("name", "Name", c.Name == "Neuer Kontakt" ? "" : c.Name); var company = T("company", "Firma", c.Company); var email = T("email", "E-Mail", c.Email);
        var mobile = T("mobile", "Mobil", c.Mobile); var phone = T("phone", "Telefon", c.Phone); var street = T("street", "Straße und Nr.", c.Street); var city = T("city", "PLZ und Ort", c.City);
        var bday = T("birthday", "Geburtstag (TT.MM.JJJJ)", c.Birthday?.ToString("dd.MM.yyyy") ?? ""); var web = T("web", "Webseite", c.Website);
        var groups = T("groups", "Gruppen, mit Komma (z. B. Familie, Verein)", string.Join(", ", c.Groups ?? [])); var note = new TextBox { Name = "ab-e-note", Text = c.Note, PlaceholderText = "Notiz", AcceptsReturn = true, Height = 56, Width = 510, TextWrapping = TextWrapping.Wrap };
        var photo = new TextBox { Name = "ab-e-photo-file", PlaceholderText = "Bilddatei für das Foto", Width = 250 };
        var save = Act("ab-e-save", "Speichern", () => Try(AB, () =>
        {
            DateOnly? b = null; var bt = (bday.Text ?? "").Trim();
            if (bt.Length > 0) b = DateOnly.TryParse(bt, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var d) ? d : throw new ArgumentException("Geburtstag bitte als TT.MM.JJJJ.");
            var saved = Mail.UpdateContact(c with { Name = name.Text ?? "", Company = company.Text?.Trim() ?? "", Email = email.Text ?? "", Mobile = mobile.Text?.Trim() ?? "", Phone = phone.Text?.Trim() ?? "", Street = street.Text?.Trim() ?? "", City = city.Text?.Trim() ?? "",
                Birthday = b, Website = web.Text?.Trim() ?? "", Note = note.Text?.Trim() ?? "", Groups = (groups.Text ?? "").Split(',').ToList() });
            var pf = (photo.Text ?? "").Trim('"', ' '); if (pf.Length > 0) { if (!File.Exists(pf)) throw new ArgumentException("Die Bilddatei gibt es nicht."); Mail.SetPhoto(saved.Id, File.ReadAllBytes(pf), Path.GetExtension(pf)); }
            _abEdit = false; Say("Gespeichert.", AB);
        }), true);
        var cancel = Act("ab-e-cancel", "Abbrechen", () => { if (c.Name == "Neuer Kontakt" && c.Email.Length == 0) { Mail.RemoveContact(c.Id); _abSel = null; } _abEdit = false; Show(AB); });
        Control L(string label, Control box) => new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 10, 6), Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11 }, box } };
        var fields = new WrapPanel { MaxWidth = 540, HorizontalAlignment = HorizontalAlignment.Left, Children = { L("Name", name), L("Firma", company), L("E-Mail", email), L("Webseite", web), L("Mobil", mobile), L("Telefon", phone), L("Straße und Nr.", street), L("PLZ und Ort", city),
            L("Geburtstag", bday), L("Foto (Bilddatei)", photo), L("Gruppen", groups), L("Notiz", note) } };
        return new StackPanel { Name = "ab-editor", Spacing = 8, Children = {
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H(c.Name == "Neuer Kontakt" ? "Neuer Kontakt" : "Bearbeiten", 18), Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { save, cancel } }, 1) } },
            fields } };
    }

    Control AbDuplicates(List<List<Contact>> dups)
    {
        var sp = new StackPanel { Name = "ab-dups", Spacing = 10, Children = { H("Doppelte Kontakte", 18), P("Gleicher Name, gleiche E-Mail oder gleiche Nummer. Beim Zusammenführen bleibt der erste, fehlende Angaben kommen aus den anderen.", Muted, 12) } };
        for (var i = 0; i < dups.Count; i++)
        {
            var g = dups[i]; var key = string.Join(",", g.Select(c => c.Id)); var idx = i;
            var row = new WrapPanel();
            foreach (var c in g) row.Children.Add(new Border { Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#10FFFFFF"), Width = 230,
                Child = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = c.Name, Foreground = Text, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = string.Join("\n", new[] { c.Email, c.Mobile, c.Phone, c.Company }.Where(x => x.Length > 0)), Foreground = Muted, FontSize = 11.5 } } } });
            Control act = _abMergeAsk != key ? Act($"ab-merge-{i}", "Zusammenführen …", () => { _abMergeAsk = key; Show(AB); }, true)
                : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = $"{g.Count} Einträge zu einem zusammenführen?", Foreground = Brush.Parse("#F5A524"), VerticalAlignment = VerticalAlignment.Center },
                    Act($"ab-merge-yes-{i}", "Ja", () => { var m = Mail.Merge(g[0].Id, g.Skip(1).Select(c => c.Id)); _abMergeAsk = null; _abSel = m.Id; Say($"Zusammengeführt: {m.Name}.", AB); }, true),
                    Act($"ab-merge-no-{i}", "Nein", () => { _abMergeAsk = null; Show(AB); }) } };
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { row, act } }));
        }
        if (dups.Count == 0) sp.Children.Add(P("Keine doppelten Kontakte gefunden.", Text, 13));
        return sp;
    }
}
