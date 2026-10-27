using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>STUDIO-LETTER-01: Briefstudio mit Vorgangsakten (eine Unterebene: Schnell schreiben · Akten · Akte · Prüfen & Ausgabe · Briefköpfe).</summary>
public partial class MainWindow
{
    public static readonly (string Key, string Label)[] LetterTabs =
        [("brief", "Schnell schreiben"), ("akten", "Akten"), ("akte", "Akte"), ("ausgabe", "Prüfen & Ausgabe"), ("briefkoepfe", "Briefköpfe")];
    private CaseStore Cases => new(ProjectsRoot, _core.Audit);
    private LetterCase? _case;
    public LetterCase? CurrentCase => _case;
    private string _caseMsg = "";
    private LetterApproval? _approval;

    private bool _letterPreview = true;
    private Action? _letterSync;

    private Control LetterNav()
    {
        var sp = new StackPanel { Spacing = 4 };
        foreach (var (k, l) in LetterTabs)
        {
            if (k is "akte" or "ausgabe" && _case is null) continue;
            var label = k switch { "brief" => "Neuer Brief", "akten" => "Meine Briefe", "briefkoepfe" => "Briefköpfe", "ausgabe" => "Prüfen und Ausgabe", _ => l };
            if (k == "akte" && _case is not null) label = "Akte: " + ShortTitle(_case.Title);
            var icon = k switch { "brief" => Mdi.FileDocumentEdit, "akten" => Mdi.Folder, "briefkoepfe" => Mdi.FileDocumentOutline, "ausgabe" => Mdi.Send, _ => Mdi.Folder };
            var b = new Button { Name = "letter-tab-" + k, Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ico(icon, 16, Text), new TextBlock { Text = label, Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Background = CurrentView == k ? Accent : Brushes.Transparent, Foreground = Text };
            Avalonia.Automation.AutomationProperties.SetName(b, label);
            b.Click += (_, _) => Show(k); sp.Children.Add(b);
        }
        var emp = new Button { Name = "letter-nav-empfaenger", Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ico(Mdi.AccountCircleOutline, 16, Text), new TextBlock { Text = "Empfänger", Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 8), Background = Brushes.Transparent, Foreground = Text };
        Avalonia.Automation.AutomationProperties.SetName(emp, "Empfänger");
        emp.Click += (_, _) =>
        {
            _letterMsg = "Empfänger: Firma, Name, Straße und PLZ Ort, eine Angabe pro Zeile.";
            Show("brief");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this).OfType<TextBox>().FirstOrDefault(t => t.Name == "letter-recipient")?.Focus());
        }; sp.Children.Add(emp);
        return new Border { Background = Brush.Parse(C("#55101830", "#CCFFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(8), Child = sp, Margin = new Thickness(0, 0, 10, 0) };
    }

    private Control LetterShell(Control body, Control? actions)
    {
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
            new TextBlock { Name = "page-title", Text = "Briefe schreiben", Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "Briefe, Vorlagen und Akten", Foreground = Muted, FontSize = 12 } } };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 10) };
        head.Children.Add(title);
        if (actions is not null) { Grid.SetColumn(actions, 1); head.Children.Add(actions); }
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*") };
        grid.Children.Add(head);
        if (_caseMsg.Length > 0) { var m = P(_caseMsg, Text); m.Name = "case-msg"; Grid.SetRow(m, 1); grid.Children.Add(m); }
        Detach(_hintHost); Grid.SetRow(_hintHost, 2); grid.Children.Add(_hintHost);
        Grid.SetRow(body, 3); grid.Children.Add(body);
        return Panel(grid, 12);
    }

    private Control LetterPage(string title, Control body)
    {
        _ = title;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("210,*") };
        grid.Children.Add(LetterNav());
        var scroll = new ScrollViewer { Content = body }; Grid.SetColumn(scroll, 1); grid.Children.Add(scroll);
        return LetterShell(grid, null);
    }
    private static string ShortTitle(string s) => s.Length > 22 ? s[..22] + "…" : s;
    private void SaveCase(string why) { if (_case is null) return; Cases.Save(_case, Principal.LocalOwner, why); }
    private static TextBox Box(string name, string ph, string val = "", double w = 300, bool multi = false) =>
        new() { Name = name, PlaceholderText = ph, Text = val, Width = w, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 60 : 0, HorizontalAlignment = HorizontalAlignment.Left };
    private static Control Row(params Control[] c) { var w = new WrapPanel(); foreach (var x in c) { x.Margin = new Thickness(0, 0, 6, 6); x.VerticalAlignment = VerticalAlignment.Center; if (x is TextBlock tb) tb.MaxWidth = 560; w.Children.Add(x); } return w; }
    private Button Btn(string name, string label, Action a, bool accent = false) { var b = new Button { Name = name, Content = label }; if (accent) b.Background = Accent; b.Click += (_, _) => { try { a(); } catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or ArgumentException or IOException) { _caseMsg = ex.Message; Show(CurrentView); } }; return b; }

    private Button Btn(string name, string label, Func<Task> a) { var b = new Button { Name = name, Content = label }; b.Click += async (_, _) => { try { await a(); } catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or ArgumentException or IOException) { _caseMsg = ex.Message; Show(CurrentView); } }; return b; }
    public void OpenCase(LetterCase c) { _case = c; _letter = c.Draft; _approval = null; _caseMsg = ""; }

    // ---------- Akten ----------
    private Control CasesView()
    {
        var sp = new StackPanel { Spacing = 6 };
        var title = Box("case-title", "Worum geht es? z. B. „Widerspruch Rentenbescheid“", w: 360);
        var cats = new ComboBox { Name = "case-category", ItemsSource = LetterKit.Categories.Select(c => c.Label).ToList(), SelectedIndex = 0, Width = 200 };
        sp.Children.Add(H("Neue Akte", 14));
        sp.Children.Add(Row(title, cats, Btn("case-create", "Akte anlegen", () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) throw new InvalidOperationException("Bitte einen Titel eingeben.");
            var cat = LetterKit.Categories[Math.Max(0, cats.SelectedIndex)];
            var c = Cases.Create(title.Text, cat.Id, Principal.LocalOwner);
            if (Cases.Letterheads(Principal.LocalOwner).FirstOrDefault() is { } h) { c.LetterheadId = h.Id; LetterKit.Compose(c, h); }
            OpenCase(c); _caseMsg = $"Akte „{c.Title}“ angelegt."; SaveCase("start"); Show("akte");
        }, true)));
        sp.Children.Add(P("Eine Akte hält alles zu einer Sache zusammen: Fakten, Ablauf, Anlagen, Fristen, Quellen, Eingangspost und alle Versionen des Briefs.", null, 11));
        var list = Cases.List(Principal.LocalOwner);
        sp.Children.Add(H($"Deine Akten ({list.Count})", 14));
        var i = 0;
        foreach (var c in list)
        {
            var b = new Button { Name = $"case-open-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = TileBg,
                Content = new StackPanel { Children = { new TextBlock { Text = c.Title, Foreground = Text }, P($"{LetterKit.Category(c.Category).Label} · {StateLabel(c.State)} · {c.Attachments.Count} Anlagen · {c.Deadlines.Count(d => d.Status == Verification.Ungeprueft)} ungeprüfte Fristen", null, 10.5) } } };
            b.Click += (_, _) => { OpenCase(c); Show("akte"); };
            sp.Children.Add(b);
        }
        return LetterPage("Akten", sp);
    }
    public static string StateLabel(DeliveryState s) => s switch { DeliveryState.Entwurf => "ENTWURF", DeliveryState.Exportiert => "EXPORTIERT (nicht versendet)", DeliveryState.Druckbereit => "DRUCKBEREIT", DeliveryState.FreigegebenFuerVersand => "FREIGEGEBEN – noch nicht versendet", _ => "VERSENDET (Nachweis vorhanden)" };

    // ---------- Akte ----------
    private Control CaseView()
    {
        if (_case is null) return CasesView();
        var c = _case; var sp = new StackPanel { Spacing = 8 };
        var cat = LetterKit.Category(c.Category);
        sp.Children.Add(P($"{cat.Label} · Status: {StateLabel(c.State)} · Rechtsraum: {c.Jurisdiction}", Text, 12.5));
        if (cat.Warning.Length > 0) sp.Children.Add(P("⚠ " + cat.Warning, Brush.Parse("#FACC15"), 11.5));
        // Verfahrensart
        var proc = Box("case-procedure", "Verfahrensart (z. B. Widerspruchsverfahren)", c.Procedure);
        sp.Children.Add(Row(proc, Btn("case-procedure-ok", c.ProcedureConfirmed ? "✓ Verfahrensart bestätigt" : "Verfahrensart bestätigen", () => { if (string.IsNullOrWhiteSpace(proc.Text)) throw new InvalidOperationException("Bitte die Verfahrensart eintragen."); c.Procedure = proc.Text!.Trim(); c.ProcedureConfirmed = true; SaveCase("Verfahren"); Show("akte"); })));
        // Rückfragen
        var q = LetterKit.Questions(c);
        if (q.Count > 0) { sp.Children.Add(H("Trendo braucht noch", 14)); foreach (var x in q.Take(10)) sp.Children.Add(P("? " + x, Text, 11.5)); }
        // Fakten
        sp.Children.Add(H("Fakten", 14));
        foreach (var (f, i) in c.Facts.Select((f, i) => (f, i))) sp.Children.Add(Row(P($"{f.Kind switch { FactKind.Tatsache => "Tatsache", FactKind.Vermutung => "Vermutung", _ => "Frage" }}: {f.Text}{(f.Source.Length > 0 ? $" (Quelle: {f.Source})" : "")}", Text, 12), Btn($"fact-del-{i}", "Entfernen", () => { c.Facts.RemoveAt(i); SaveCase("Fakt entfernt"); Show("akte"); })));
        var ft = Box("fact-text", "z. B. „Bescheid vom 03.09.2026 ging am 05.09.2026 zu.“", w: 380); var fs = Box("fact-source", "Beleg (optional)", w: 140);
        var fk = new ComboBox { Name = "fact-kind", ItemsSource = new[] { "Tatsache (belegt)", "Vermutung", "Frage" }, SelectedIndex = 0 };
        sp.Children.Add(Row(ft, fk, fs, Btn("fact-add", "Hinzufügen", () => { if (string.IsNullOrWhiteSpace(ft.Text)) return; c.Facts.Add(new(ft.Text.Trim(), (FactKind)Math.Max(0, fk.SelectedIndex), fs.Text?.Trim() ?? "")); SaveCase("Fakt"); Show("akte"); })));
        // Chronik
        sp.Children.Add(H("Ablauf (Chronik)", 14));
        foreach (var t in c.Timeline.OrderBy(t => t.Date)) sp.Children.Add(P($"{t.Date:dd.MM.yyyy}  {t.Text}{(t.Source.Length > 0 ? $"  [{t.Source}]" : "")}", Text, 12));
        var td = Box("tl-date", "TT.MM.JJJJ", w: 100); var tt = Box("tl-text", "Was ist passiert?", w: 300);
        sp.Children.Add(Row(td, tt, Btn("tl-add", "Eintragen", () => { if (!DateTime.TryParseExact(td.Text, "dd.MM.yyyy", De, System.Globalization.DateTimeStyles.None, out var dt)) throw new InvalidOperationException("Datum bitte als TT.MM.JJJJ."); c.Timeline.Add(new(dt, tt.Text?.Trim() ?? "")); SaveCase("Chronik"); Show("akte"); })));
        // Anlagen
        sp.Children.Add(H("Anlagen", 14));
        foreach (var a in c.Attachments.OrderBy(a => a.No)) sp.Children.Add(P($"Anlage {a.No}: {a.Title}  ·  SHA-256 {a.Sha256[..12]}…", Text, 12));
        sp.Children.Add(Row(Btn("att-add", "Anlage hinzufügen …", async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Anlage wählen", AllowMultiple = true });
            foreach (var f in files) if (f.TryGetLocalPath() is { } p) Cases.AddAttachment(c, p, Path.GetFileNameWithoutExtension(p), Principal.LocalOwner);
            Show("akte");
        }), Btn("att-renumber", "Neu nummerieren", () => { Cases.Renumber(c, Principal.LocalOwner); Show("akte"); })));
        // Eingangspost
        sp.Children.Add(H("Eingangspost (Brief, Bescheid, Scan)", 14));
        foreach (var inc in c.Incoming)
        {
            var an = LetterKit.Analyze(inc.Text);
            var box = new StackPanel { Spacing = 2, Children = { P($"{inc.Id} · {Path.GetFileName(inc.File)} · Texterkennung {inc.Confidence:0} %{(inc.Unreadable.Count > 0 ? $", {inc.Unreadable.Count} unsichere Stellen [?…?]" : "")}{(inc.Ephemeral ? " · forensisch: Löschung nach 120 h" : "")}", Text, 12) } };
            foreach (var w in an.InjectionWarnings) box.Children.Add(P("⚠ " + w, Brush.Parse("#F87171"), 11));
            if (an.Demands.Count > 0) box.Children.Add(P("Aufforderungen an dich: " + string.Join(" | ", an.Demands.Take(4)), Text, 11));
            if (an.Claims.Count > 0) box.Children.Add(P("Behauptungen des Absenders (nicht geprüft): " + string.Join(" | ", an.Claims.Take(3)), null, 11));
            if (an.References.Count + an.Amounts.Count > 0) box.Children.Add(P($"Zeichen: {string.Join(", ", an.References)}   Beträge: {string.Join(", ", an.Amounts)}", null, 11));
            box.Children.Add(Row(Btn($"inc-deadlines-{inc.Id}", "Termine suchen", () =>
            {
                var recv = c.Timeline.FirstOrDefault(t => t.Text.Contains("zugegangen", StringComparison.OrdinalIgnoreCase) || t.Text.Contains("erhalten", StringComparison.OrdinalIgnoreCase))?.Date;
                var found = LetterKit.FindDeadlines(inc.Text, inc.Id, recv).Where(d => !c.Deadlines.Any(x => x.Text == d.Text)).ToList();
                c.Deadlines.AddRange(found); SaveCase("Fristen gesucht"); _caseMsg = $"{found.Count} mögliche Termine gefunden – alle UNGEPRÜFT."; Show("akte");
            }), Btn($"inc-reply-{inc.Id}", "Antwort vorbereiten", () =>
            {
                c.Draft.Reference = an.References.FirstOrDefault() ?? c.Draft.Reference;
                foreach (var d in an.Demands.Take(5)) c.Facts.Add(new("Aufforderung im Schreiben: " + d, FactKind.Frage, inc.Id));
                SaveCase("Antwort vorbereitet"); _caseMsg = "Aufforderungen als offene Fragen übernommen. Bitte jede beantworten."; Show("akte");
            })));
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = box });
        }
        var ephemeral = new CheckBox { Name = "inc-ephemeral", Content = "Nur forensische Kopie (nach 120 Stunden automatisch löschen)", Foreground = Text };
        sp.Children.Add(Row(Btn("inc-add", "Scan/Foto einlesen …", async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Brief-Foto oder Scan", FileTypeFilter = [new("Bilder") { Patterns = Ocr.ImageExt.Select(e => "*" + e).ToList() }, new("Text") { Patterns = ["*.txt"] }] });
            foreach (var f in files) if (f.TryGetLocalPath() is { } p) await ImportIncoming(c, p, ephemeral.IsChecked == true);
            Show("akte");
        }), ephemeral));
        // Fristen
        sp.Children.Add(H("Fristen und Termine", 14));
        if (c.Deadlines.Count == 0) sp.Children.Add(P("Keine. Trendo trägt nie selbst Fristen ein – nur Vorschläge, die du mit Quelle bestätigst."));
        foreach (var (d, i) in c.Deadlines.Select((d, i) => (d, i)).ToList())
        {
            var src = Box($"dl-src-{i}", "Quelle (z. B. Rechtsbehelfsbelehrung S. 2)", w: 240);
            var line = P($"{(d.Date == DateTime.MinValue ? "Datum offen" : d.Date.ToString("dd.MM.yyyy"))} · {d.Text} · {(d.Status == Verification.Bestaetigt ? $"BESTÄTIGT ({d.ConfirmedSource})" : "UNGEPRÜFT")}", d.Status == Verification.Bestaetigt ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), 11.5);
            sp.Children.Add(line);
            if (d.Status != Verification.Bestaetigt)
                sp.Children.Add(Row(src, Btn($"dl-ok-{i}", "Mit Quelle bestätigen", () => { c.Deadlines[i] = LetterKit.Confirm(d, src.Text ?? "", Principal.LocalOwner); SaveCase("Frist bestätigt"); AddReminder(c, c.Deadlines[i]); Show("akte"); }),
                    Btn($"dl-no-{i}", "Verwerfen", () => { c.Deadlines[i] = d with { Status = Verification.Abgelehnt }; SaveCase("Frist verworfen"); Show("akte"); })));
        }
        // Quellen
        sp.Children.Add(H("Rechtsquellen", 14));
        var cites = LetterKit.Citations(c.Draft.Subject + "\n" + c.Draft.Body);
        if (cites.Count == 0) sp.Children.Add(P("Der Brief nennt keine Paragrafen oder Urteile."));
        foreach (var (ci, i) in cites.Select((x, i) => (x, i)))
        {
            var ok = c.Sources.FirstOrDefault(s => s.Verified && LetterKit.Norm(s.Citation) == LetterKit.Norm(ci));
            if (ok is not null) { sp.Children.Add(P($"✓ {ci} – {ok.Url} (abgerufen {ok.Retrieved:dd.MM.yyyy}, {ok.Jurisdiction})", Brush.Parse("#4ADE80"), 11.5)); continue; }
            var url = Box($"src-url-{i}", "Link zur amtlichen Fundstelle (https://…)", LetterKit.LookupUrl(ci), w: 380);
            sp.Children.Add(P($"✗ {ci} – nicht belegt", Brush.Parse("#F87171"), 11.5));
            sp.Children.Add(Row(url, Btn($"src-open-{i}", "Nachschlagen", () => { if (!_core.Offline && Browsers.Normalize(url.Text ?? "") is { } u) Launch(u.AbsoluteUri); }),
                Btn($"src-ok-{i}", "Ich habe es geprüft", () =>
                {
                    if (!LetterKit.IsOfficial(url.Text ?? "")) throw new InvalidOperationException("Bitte eine amtliche Quelle (z. B. gesetze-im-internet.de, eur-lex.europa.eu, Gerichts-Websites) über https angeben.");
                    c.Sources.Add(new(ci, url.Text!.Trim(), DateTime.Today, c.Jurisdiction, true)); SaveCase("Quelle"); Show("akte");
                })));
        }
        sp.Children.Add(Row(Btn("case-compose", "Brief aus der Akte zusammensetzen", () => { LetterKit.Compose(c, Cases.Letterhead(c.LetterheadId, Principal.LocalOwner)); _letter = c.Draft; SaveCase("zusammengesetzt"); _caseMsg = "Brief aktualisiert – im Reiter „Schnell schreiben“ bearbeiten oder direkt prüfen."; Show("ausgabe"); }, true),
            Btn("case-edit", "Brief bearbeiten", () => { _letter = c.Draft; Show("brief"); })));
        return LetterPage("Akte", sp);
    }

    public async Task ImportIncoming(LetterCase c, string path, bool ephemeral)
    {
        string text; double conf; List<string> bad;
        if (Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)) { text = await File.ReadAllTextAsync(path); conf = 100; bad = []; }
        else
        {
            var tess = ToolPath("tesseract");
            if (tess is null) { _caseMsg = "Texterkennung (Tesseract) ist nicht installiert – unter Zusatzprogramme nachinstallieren."; return; }
            var run = new RealProcessRunner();
            string? dir = null; var lang = "deu";
            if (Ocr.HasUserGerman(_core.DataRoot)) dir = Ocr.UserTessdata(_core.DataRoot);
            else if (!(await Ocr.LanguagesAsync(run, tess)).Contains("deu"))
            {
                if (_core.Offline) { _caseMsg = "Deutsches OCR-Sprachpaket fehlt, und Trendo ist offline."; return; }
                var (ok, m) = await Ocr.DownloadGermanAsync(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }.GetStreamAsync, _core.DataRoot, _core.Audit, Principal.LocalOwner);
                if (!ok) { _caseMsg = m + " Texterkennung läuft auf Englisch – Umlaute werden schlechter erkannt."; lang = "eng"; } else dir = Ocr.UserTessdata(_core.DataRoot);
            }
            var r = await Ocr.RunAsync(run, tess, path, default, dir, lang);
            if (!r.Ok) { _caseMsg = r.Message; return; }
            (text, conf, bad) = (r.Text, r.Confidence, r.Unreadable); _caseMsg = r.Message;
        }
        Cases.AddIncoming(c, path, text, conf, bad, Principal.LocalOwner, ephemeral);
    }

    /// <summary>Nur bestätigte Fristen werden zur Erinnerung (Kalenderdatei .ics in der Akte).</summary>
    private void AddReminder(LetterCase c, Deadline d)
    {
        var ics = Path.Combine(Cases.Dir(c.Id), $"frist-{d.Date:yyyyMMdd}.ics");
        var esc = (string s) => s.Replace("\\", "\\\\").Replace(",", "\\,").Replace(";", "\\;").Replace("\n", "\\n");
        File.WriteAllText(ics, $"BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Trendo OS//Briefstudio//DE\r\nBEGIN:VEVENT\r\nUID:{c.Id}-{d.Date:yyyyMMdd}@trendo\r\nDTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}\r\nDTSTART;VALUE=DATE:{d.Date:yyyyMMdd}\r\nSUMMARY:{esc("Frist: " + c.Title)}\r\nDESCRIPTION:{esc(d.Text + " – Quelle: " + d.ConfirmedSource)}\r\nBEGIN:VALARM\r\nTRIGGER:-P3D\r\nACTION:DISPLAY\r\nDESCRIPTION:Frist in 3 Tagen\r\nEND:VALARM\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n");
        _core.Audit.Append(Principal.LocalOwner.Name, "letter.deadline.calendar", $"{c.Id} {d.Date:yyyy-MM-dd}");
        _caseMsg = $"Frist bestätigt. Kalenderdatei angelegt: {Path.GetFileName(ics)} (Doppelklick übernimmt sie in deinen Kalender).";
    }

    // ---------- Prüfen & Ausgabe ----------
    private string? _finalFile;
    private Control OutputView()
    {
        if (_case is null) return CasesView();
        var c = _case; var sp = new StackPanel { Spacing = 6 };
        // Ton
        var tones = new WrapPanel();
        foreach (var t in LetterKit.Tones) { var b = Btn("tone-" + t.Id, (c.Tone == t.Id ? "✓ " : "") + t.Label, () => { LetterKit.ApplyTone(c, t.Id); SaveCase("Ton " + t.Id); Show("ausgabe"); }); b.Margin = new Thickness(0, 0, 6, 6); tones.Children.Add(b); }
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("Ton", 14), tones, P("Der Ton ändert nur Anrede, Schluss und einen Rahmensatz – Fakten, Beträge und Zeichen bleiben gleich.", null, 10.5) } });
        // Prüfung
        var issues = LetterKit.ValidateCase(c); var proof = Proofread.Check(c.Draft);
        sp.Children.Add(H($"Prüfung: {issues.Count(i => i.Blocking)} Pflichtpunkte · {issues.Count(i => !i.Blocking) + proof.Count} Hinweise", 14));
        foreach (var x in issues) sp.Children.Add(P($"{(x.Blocking ? "✗" : "•")} {x.Field}: {x.Message}", x.Blocking ? Brush.Parse("#F87171") : null, 11.5));
        foreach (var x in proof) sp.Children.Add(P("• Korrektur: " + x, null, 11.5));
        // Vorschau
        sp.Children.Add(H("Vorschau", 14));
        var d = c.Draft;
        sp.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(4), Padding = new Thickness(28, 18), Width = 520, HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = $"{d.SenderName} · {d.SenderStreet} · {d.SenderCity}", FontSize = 8, Foreground = Brushes.Black },
                new TextBlock { Text = d.Recipient, FontSize = 11, Foreground = Brushes.Black },
                new TextBlock { Text = $"{(d.Reference.Length > 0 ? "Zeichen: " + d.Reference + "\n" : "")}{d.Place}{(d.Place.Length > 0 ? ", " : "")}{d.Date.ToString("d. MMMM yyyy", De)}", FontSize = 11, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Right, TextAlignment = TextAlignment.Right },
                new TextBlock { Text = d.Subject, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = d.Salutation + "\n\n" + d.Body + "\n\n" + d.Closing + "\n\n\n" + d.SenderName + (d.Attachments.Length > 0 ? "\n\nAnlagen\n" + d.Attachments : ""), FontSize = 11, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap } } } });
        // Ausgabe
        var so = ToolPath("libreoffice");
        sp.Children.Add(H("Ausgabe", 14));
        async Task Export(string fmt)
        {
            if (issues.Any(i => i.Blocking)) throw new InvalidOperationException("Erst die Pflichtpunkte erledigen – so geht der Brief nicht raus.");
            var dir = Cases.Dir(c.Id); var stem = Letters.FileStem(d); var v = 1; while (File.Exists(Path.Combine(dir, $"{stem}-v{v}.docx"))) v++;
            var docx = Path.Combine(dir, $"{stem}-v{v}.docx"); Letters.WriteDocx(d, docx); _finalFile = docx;
            var msg = $"Word-Datei: {Path.GetFileName(docx)} (SHA-256 {LetterKit.Sha(docx)[..16]}…)";
            if (fmt != "docx")
            {
                if (so is null) throw new InvalidOperationException("Für PDF/ODT wird LibreOffice gebraucht (Zusatzprogramme).");
                var r = await PdfExport.ConvertToAsync(new RealProcessRunner(), so, docx, fmt);
                if (!r.Ok) throw new InvalidOperationException(r.Message);
                _finalFile = r.Pdf; msg += $" · {Path.GetFileName(r.Pdf)}: {r.Message}";
            }
            c.State = c.State == DeliveryState.Entwurf ? DeliveryState.Exportiert : c.State; _approval = null;
            SaveCase($"Export {fmt} v{v}"); _core.Audit.Append(Principal.LocalOwner.Name, "letter.export", $"{c.Id} {Path.GetFileName(_finalFile)} sha={LetterKit.Sha(_finalFile!)}");
            _caseMsg = msg; Show("ausgabe");
        }
        Button ExportBtn(string n, string l, string f) { var b = new Button { Name = n, Content = l, IsEnabled = f == "docx" || so is not null }; b.Click += async (_, _) => { try { await Export(f); } catch (InvalidOperationException ex) { _caseMsg = ex.Message; Show("ausgabe"); } }; return b; }
        sp.Children.Add(Row(ExportBtn("out-docx", "Word (DOCX)", "docx"), ExportBtn("out-odt", "OpenDocument (ODT)", "odt"), ExportBtn("out-pdf", "PDF zum Drucken", "pdf")));
        if (_finalFile is not null && File.Exists(_finalFile)) sp.Children.Add(Row(P("Letzte Ausgabe: " + Path.GetFileName(_finalFile), Text, 11.5), Btn("out-open", "Öffnen", () => Launch(_finalFile)), Btn("out-folder", "Ordner", () => Launch(Path.GetDirectoryName(_finalFile)!))));
        // Übersetzung (nur Entwurf)
        sp.Children.Add(H("Übersetzung (nur Entwurf, Deutsch bleibt maßgeblich)", 14));
        var langs = new WrapPanel();
        foreach (var (code, name) in new[] { ("en", "Englisch"), ("fr", "Französisch"), ("it", "Italienisch"), ("es", "Spanisch"), ("pl", "Polnisch"), ("tr", "Türkisch") })
        {
            var b = new Button { Name = "tr-" + code, Content = name, Margin = new Thickness(0, 0, 6, 6), IsEnabled = _kiConnected };
            b.Click += async (_, _) =>
            {
                b.IsEnabled = false;
                using var kiLabel = Trendo.Core.KiActivityHub.Label("Brief übersetzen"); var ans = await _ollama.ChatAsync(KiModelName, $"Übersetze den folgenden deutschen Brieftext ins {name}. Ändere keine Zahlen, Daten, Beträge, Aktenzeichen oder Namen. Gib nur die Übersetzung aus.\n\n" + LetterKit.FenceUntrusted(d.Body));
                c.Translations.RemoveAll(t => t.Language == code); c.Translations.Add(new(code, ans, DateTime.Now, LetterKit.ShaText(d.Body))); SaveCase("Übersetzung " + code); Show("ausgabe");
            };
            langs.Children.Add(b);
        }
        sp.Children.Add(langs);
        if (!_kiConnected) sp.Children.Add(P("Die lokale KI ist nicht bereit – Übersetzungen sind aus.", null, 10.5));
        foreach (var t in c.Translations)
        {
            var lost = LetterKit.LostFacts(d.Body, t.Text); var stale = t.MasterSha256 != LetterKit.ShaText(d.Body);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), Children = { P(d.Body, Text, 11), Pos(P(t.Text, Text, 11), 2) } };
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 4, Children = {
                P($"Deutsch ↔ {t.Language.ToUpperInvariant()} · ENTWURF – bitte von jemandem prüfen lassen, der die Sprache sicher kann" + (stale ? " · VERALTET: das Original wurde geändert" : ""), stale ? Brush.Parse("#FACC15") : null, 10.5),
                lost.Count > 0 ? P("⚠ Fehlt in der Übersetzung: " + string.Join(", ", lost), Brush.Parse("#F87171"), 11) : P("✓ Zahlen, Daten und Zeichen stimmen überein.", Brush.Parse("#4ADE80"), 11), grid } } });
        }
        // Versandweg + Freigabe
        sp.Children.Add(H("Versandweg und Freigabe", 14));
        var ch = new WrapPanel();
        foreach (var x in LetterKit.Channels) { var b = Btn("ch-" + x.Id, (c.FilingChannel == x.Id ? "✓ " : "") + x.Label, () => { c.FilingChannel = x.Id; _approval = null; SaveCase("Kanal " + x.Id); Show("ausgabe"); }); b.Margin = new Thickness(0, 0, 6, 6); ch.Children.Add(b); }
        sp.Children.Add(ch);
        if (LetterKit.Channels.FirstOrDefault(x => x.Id == c.FilingChannel) is { Id: not null } sel) sp.Children.Add(P(sel.Hint, null, 11));
        if (c.Category is "gericht" or "behoerde")
        {
            var law = new ComboBox { Name = "court-lawyer", ItemsSource = new[] { "Anwaltszwang: ungeklärt", "Anwaltszwang: nein (geprüft)", "Anwaltszwang: ja" }, SelectedIndex = c.LawyerRequired is null ? 0 : c.LawyerRequired == false ? 1 : 2 };
            law.SelectionChanged += (_, _) => { c.LawyerRequired = law.SelectedIndex switch { 1 => false, 2 => true, _ => null }; _approval = null; SaveCase("Anwaltszwang"); };
            var form = new CheckBox { Name = "court-form", Content = "Form, Signatur und zulässiger Weg sind geklärt (von mir geprüft)", IsChecked = c.FormConfirmed, Foreground = Text };
            form.IsCheckedChanged += (_, _) => { c.FormConfirmed = form.IsChecked == true; _approval = null; SaveCase("Form"); };
            sp.Children.Add(Row(law, form));
        }
        var (ready, blockers) = LetterKit.ReadyForDelivery(c);
        foreach (var b in blockers) sp.Children.Add(P("✗ " + b, Brush.Parse("#F87171"), 11.5));
        var approve = Btn("out-approve", "Endgültige Fassung freigeben", () =>
        {
            if (_finalFile is null) throw new InvalidOperationException("Bitte zuerst eine Ausgabe (PDF/DOCX) erzeugen.");
            _approval = LetterGate.Approve(c, _finalFile, Principal.LocalOwner, _core.Audit); SaveCase("freigegeben");
            _caseMsg = $"Freigegeben: {Path.GetFileName(_finalFile)} an {c.Draft.Recipient.Split('\n')[0]} per {c.FilingChannel}. Trendo verschickt nichts – bitte selbst versenden."; Show("ausgabe");
        }, ready);
        approve.IsEnabled = ready;
        var check = Btn("out-send", "Versenden …", () => { _caseMsg = LetterGate.TrySendExternally(c, _approval, _finalFile ?? "", _core.Audit, Principal.LocalOwner); Show("ausgabe"); });
        sp.Children.Add(Row(approve, check));
        var proofBox = Box("out-proof", "Nachweis, z. B. Einschreiben-Nr., Sendebericht, Eingangsstempel", c.DeliveryProof ?? "", w: 360);
        sp.Children.Add(Row(proofBox, Btn("out-sent", "Als versendet markieren", () => { LetterKit.MarkSent(c, proofBox.Text ?? ""); SaveCase("versendet"); Show("ausgabe"); })));
        sp.Children.Add(P($"Status: {StateLabel(c.State)}", Text, 12.5));
        sp.Children.Add(P("Trendo bietet Hilfe beim Formulieren, keine Rechtsberatung und keine Einreichung. Bei rechtlichen Risiken: Beratungsstelle, Verbraucherzentrale oder Anwalt.", null, 10.5));
        return LetterPage("Prüfen & Ausgabe", sp);
    }

    // ---------- Briefköpfe ----------
    private Control LetterheadsView()
    {
        var sp = new StackPanel { Spacing = 6 };
        var heads = Cases.Letterheads(Principal.LocalOwner);
        foreach (var h in heads)
        {
            var use = Btn("head-use-" + h.Id, _case?.LetterheadId == h.Id ? "✓ in dieser Akte" : "Für die Akte verwenden", () => { if (_case is null) throw new InvalidOperationException("Erst eine Akte öffnen."); _case.LetterheadId = h.Id; LetterKit.Compose(_case, h); SaveCase("Briefkopf"); Show("briefkoepfe"); });
            use.IsEnabled = _case is not null;
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = Row(P($"{(h.Kind == "firma" ? "Firma" : "Privat")}: {h.Name}, {h.Street}, {h.City} {h.Contact}", Text, 12), use) });
        }
        if (heads.Count == 0) sp.Children.Add(P("Noch kein Briefkopf. Lege einen privaten und – falls nötig – einen Firmen-Briefkopf an. Sie werden nie vermischt."));
        var kind = new ComboBox { Name = "head-kind", ItemsSource = new[] { "Privat", "Firma" }, SelectedIndex = 0 };
        var n = Box("head-name", "Name oder Firma", _settings.Name, 220); var s = Box("head-street", "Straße Nr.", _settings.SenderStreet, 180); var ci = Box("head-city", "PLZ Ort", _settings.SenderCity, 160); var co = Box("head-contact", "Telefon / E-Mail", _settings.SenderContact, 200);
        sp.Children.Add(H("Neuer Briefkopf", 14));
        sp.Children.Add(Row(kind, n, s, ci, co, Btn("head-save", "Speichern", () =>
        {
            if (string.IsNullOrWhiteSpace(n.Text) || string.IsNullOrWhiteSpace(ci.Text)) throw new InvalidOperationException("Name und PLZ/Ort sind Pflicht.");
            Cases.SaveLetterhead(new("", "", kind.SelectedIndex == 1 ? "firma" : "privat", n.Text.Trim(), s.Text?.Trim() ?? "", ci.Text.Trim(), co.Text?.Trim() ?? ""), Principal.LocalOwner); Show("briefkoepfe");
        }, true)));
        return LetterPage("Briefköpfe", sp);
    }
}
