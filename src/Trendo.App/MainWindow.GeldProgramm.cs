using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Business;

namespace Trendo.App;

/// <summary>T-0694: „Geld &amp; Rechnungen“ als vollwertiges Programm – Reiter Übersicht · Buchungen · Rechnungen · Auswertung · Kredit,
/// Rückgängig/Wiederholen (Strg+Z/Strg+Y) für Buchungen und Rechnungen, Kontoauszug per Datei oder Ziehen und Ablegen, Rechnung als PDF und E-Rechnung.
/// Alles bleibt auf diesem PC; es wird nichts versendet. Keine Steuerberatung.</summary>
public partial class MainWindow
{
    static readonly CultureInfo GeldDe = CultureInfo.GetCultureInfo("de-DE");
    public static readonly (string Id, string Label)[] GeldTabs = [("uebersicht", "Übersicht"), ("buchungen", "Buchungen"), ("rechnungen", "Rechnungen"), ("auswertung", "Auswertung"), ("kredit", "Kredit")];
    string _geldTab = Environment.GetEnvironmentVariable("TRENDO_GELD_TAB") is { Length: > 0 } t0 && GeldTabs.Any(x => x.Id == t0) ? t0 : "uebersicht"; // Bildprüfung: Reiter vorwählen
    public string GeldTab { get => _geldTab; set => _geldTab = value; }
    public RechnungsBuchStore GeldStore => new(_core.DataRoot);
    public Nummernkreis GeldKreis => new(_core.DataRoot);
    public RechnungsBuch GeldBuch() => GeldStore.Laden();
    string? _geldSel; public string? GeldAuswahl { get => _geldSel; set => _geldSel = value; }
    string _geldMsg = ""; public string GeldMeldung => _geldMsg;

    // ---------------------------------------------------------------- Rückgängig / Wiederholen
    sealed record GeldStand(string Label, string Ledger, string Buch);
    readonly Stack<GeldStand> _geldUndo = new(), _geldRedo = new();
    string? _geldFieldKey;
    public string GeldUndoLabel => _geldUndo.TryPeek(out var u) ? u.Label : "";
    public string GeldRedoLabel => _geldRedo.TryPeek(out var r) ? r.Label : "";
    GeldStand Jetzt(string label) => new(label, Money.Raw(), GeldStore.Roh());

    void GeldPush(GeldStand s)
    {
        _geldUndo.Push(s); _geldRedo.Clear();
        if (_geldUndo.Count > 60) { var keep = _geldUndo.Take(60).Reverse().ToList(); _geldUndo.Clear(); foreach (var k in keep) _geldUndo.Push(k); }
    }

    /// <summary>Änderung an Buchungen oder Rechnungen mit Rückgängig-Schritt. false = Fehler (Meldung steht da, nichts geändert).</summary>
    public bool GeldChange(string label, Action a, bool refresh = true)
    {
        var before = Jetzt(label); _geldFieldKey = null;
        try { a(); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            try { Money.RawSet(before.Ledger); GeldStore.RohSetzen(before.Buch); } catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException) { }
            _geldMsg = "⚠ " + ex.Message; Say(_geldMsg, "finanzen"); return false;
        }
        if (Money.Raw() != before.Ledger || GeldStore.Roh() != before.Buch) GeldPush(before);
        if (refresh) Show("finanzen");
        return true;
    }

    /// <summary>Tippen in ein Feld: ein Rückgängig-Schritt je Feld, nicht je Taste.</summary>
    void GeldFieldEdit(string field, string label, Action a)
    {
        var before = _geldFieldKey == field ? null : Jetzt(label);
        try { a(); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or IOException) { _geldMsg = "⚠ " + ex.Message; return; }
        if (before is not null && (Money.Raw() != before.Ledger || GeldStore.Roh() != before.Buch)) { GeldPush(before); _geldFieldKey = field; }
    }

    public bool GeldUndo()
    {
        if (!_geldUndo.TryPop(out var s)) return false;
        _geldRedo.Push(Jetzt(s.Label)); Money.RawSet(s.Ledger); GeldStore.RohSetzen(s.Buch); _geldFieldKey = null;
        _geldMsg = "Rückgängig: " + s.Label; Say(_geldMsg, "finanzen"); return true;
    }

    public bool GeldRedo()
    {
        if (!_geldRedo.TryPop(out var s)) return false;
        _geldUndo.Push(Jetzt(s.Label)); Money.RawSet(s.Ledger); GeldStore.RohSetzen(s.Buch); _geldFieldKey = null;
        _geldMsg = "Wiederholt: " + s.Label; Say(_geldMsg, "finanzen"); return true;
    }

    // ---------------------------------------------------------------- Programmrahmen
    Control GeldProgramm(Func<Control> uebersicht)
    {
        var level = ProgramLevel;
        var tabs = new WrapPanel { Name = "geld-tabs" };
        foreach (var (id, label) in GeldTabs)
        {
            var b = new Button { Name = "geld-tab-" + id, Content = label, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(14, 6), CornerRadius = new CornerRadius(10), Foreground = Text,
                Background = _geldTab == id ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderBrush = _geldTab == id ? Accent : Edge, BorderThickness = new Thickness(_geldTab == id ? 2 : 1), FontWeight = _geldTab == id ? FontWeight.SemiBold : FontWeight.Normal };
            var t = id; b.Click += (_, _) => { _geldTab = t; _geldMsg = ""; Show("finanzen"); };
            tabs.Children.Add(b);
        }
        var undo = Act("geld-undo", _geldUndo.TryPeek(out var u) ? "↶ " + u.Label : "↶ Rückgängig", () => GeldUndo()); undo.IsEnabled = _geldUndo.Count > 0; ToolTip.SetTip(undo, "Rückgängig (Strg+Z)");
        var redo = Act("geld-redo", "↷ Wiederholen", () => GeldRedo()); redo.IsEnabled = _geldRedo.Count > 0; ToolTip.SetTip(redo, "Wiederholen (Strg+Y)");
        // V-14 (T-0700): Rückgängig/Wiederholen am gemeinsamen Platz links oben (vorher rechts neben den Reitern)
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { DocActions(null, undo, redo), Pos(tabs, 1) } };
        var top = new StackPanel { Name = "geld-kopf", Spacing = 8, Children = { head } };
        var root = new StackPanel { Name = "geld", Spacing = 8, Focusable = true, Children = { top } };
        if (Money.RecoveryNote() is { } note) { top.Children.Add(GeldHinweis("geld-wiederhergestellt", "⚠ " + note)); Money.RecoveryNoteSeen(); }
        var store = GeldStore; var buch = store.Laden();
        if (store.Wiederhergestellt is { } n2) top.Children.Add(GeldHinweis("geld-buch-wiederhergestellt", "⚠ " + n2));
        if (_geldMsg.Length > 0) top.Children.Add(GeldHinweis("geld-msg", _geldMsg));
        // Alle Reiter werden gebaut, sichtbar ist einer – so finden Claude, Tests und Tastenkürzel jedes Feld. Die unsichtbaren liegen in einem eigenen Behälter vorn,
        // der sichtbare Reiter ist das letzte Kind: so wirkt „Einfach“ („Mehr anzeigen“) nur auf den offenen Reiter.
        var hidden = new StackPanel { Name = "geld-verborgen", IsVisible = false };
        top.Children.Add(hidden);
        Control? shown = null;
        void Add(string id, Func<Control> build)
        {
            var c = build(); c.Name = "geld-page-" + id;
            if (id == _geldTab) shown = c; else hidden.Children.Add(c);
        }
        Add("uebersicht", uebersicht);
        Add("buchungen", GeldBuchungen);
        Add("rechnungen", () => GeldRechnungen(buch, level));
        Add("auswertung", () => GeldAuswertungView(level));
        Add("kredit", () => GeldKredit(level));
        if (shown is not null) root.Children.Add(shown);
        root.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control || TopLevel.GetTopLevel(root)?.FocusManager?.GetFocusedElement() is TextBox) return;
            if (e.Key == Key.Z) { e.Handled = GeldUndo(); }
            else if (e.Key == Key.Y) { e.Handled = GeldRedo(); }
            else if (e.Key == Key.O) { e.Handled = true; _ = GeldImportPick(); }
        }, RoutingStrategies.Tunnel);
        return root;
    }

    Border GeldHinweis(string name, string text) => new()
    {
        Name = name, Background = Brush.Parse("#22C9A227"), BorderBrush = Accent, BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 8),
        Child = new TextBlock { Name = name + "-text", Text = text, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 12.5 },
    };

    static string GeldEuro(decimal d) => d.ToString("N2", GeldDe) + " €";

    // ---------------------------------------------------------------- Buchungen
    int _geldJahr = DateTime.Today.Year; string _geldFind = "";

    Control GeldBuchungen()
    {
        var all = Money.All();
        var open = Act("geld-import-open", "Kontoauszug öffnen …", () => _ = GeldImportPick(), true);
        ToolTip.SetTip(open, "CSV, CAMT.053 (XML) oder MT940 – oder die Datei einfach auf die Liste ziehen (Strg+O)");
        var find = new TextBox { Name = "geld-find", Text = _geldFind, PlaceholderText = "Suchen …", Width = 200 };
        find.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _geldFind = find.Text ?? ""; Show("finanzen"); } };
        var year = new TextBlock { Name = "geld-jahr", Text = _geldJahr.ToString(GeldDe), FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) };
        var bar = new WrapPanel { Children = { open, Act("geld-jahr-prev", "◀", () => { _geldJahr--; Show("finanzen"); }), year, Act("geld-jahr-next", "▶", () => { _geldJahr++; Show("finanzen"); }), find } };
        foreach (var c in bar.Children) c.Margin = new Thickness(0, 0, 6, 6);
        var rows = all.Select((e, i) => (i, e)).Where(x => x.e.Date.Year == _geldJahr && (_geldFind.Length == 0 || x.e.Text.Contains(_geldFind, StringComparison.CurrentCultureIgnoreCase) || x.e.Category.Contains(_geldFind, StringComparison.CurrentCultureIgnoreCase))).ToList();
        var table = new StackPanel { Name = "geld-tabelle", Spacing = 1 };
        table.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("110,*,120,160,100"), Margin = new Thickness(0, 0, 0, 4), Children = {
            P("Datum", Muted, 11.5), Pos(P("Text", Muted, 11.5), 1), Pos(P("Betrag €", Muted, 11.5), 2), Pos(P("Kategorie", Muted, 11.5), 3), Pos(P("MwSt", Muted, 11.5), 4) } });
        var vats = new[] { "ohne", "7 %", "19 %" };
        for (var k = 0; k < rows.Count && k < 400; k++)
        {
            var (idx, e) = rows[k]; var i0 = idx; var kk = k;
            var date = new TextBox { Name = $"geld-b-datum-{k}", Text = e.Date.ToString("dd.MM.yyyy", GeldDe), FontSize = 12, Margin = new Thickness(0, 0, 4, 0) };
            var text = new TextBox { Name = $"geld-b-text-{k}", Text = e.Text, FontSize = 12, Margin = new Thickness(0, 0, 4, 0) };
            var amount = new TextBox { Name = $"geld-b-betrag-{k}", Text = e.Amount.ToString("0.00", GeldDe), FontSize = 12, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 4, 0), Foreground = Brush.Parse(e.Amount < 0 ? "#F0644B" : "#2FBF71") };
            var cats = FinanceExtra.AllCategories.Contains(e.Category) ? FinanceExtra.AllCategories : [.. FinanceExtra.AllCategories, e.Category];
            var cat = new ComboBox { Name = $"geld-b-kat-{k}", ItemsSource = cats, SelectedItem = e.Category, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 4, 0) };
            var vat = new ComboBox { Name = $"geld-b-mwst-{k}", ItemsSource = vats, SelectedIndex = e.VatRate == LegalRates.VatStandard ? 2 : e.VatRate == LegalRates.VatReduced ? 1 : 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
            Entry Cur() => Money.All()[i0];
            text.TextChanged += (_, _) => { if ((text.Text ?? "").Trim().Length > 0 && text.Text != Cur().Text) GeldFieldEdit(text.Name!, "Text ändern", () => Money.Replace(i0, Cur() with { Text = text.Text!.Trim() })); };
            amount.TextChanged += (_, _) =>
            {
                try { var v = Finance.ParseAmount(amount.Text ?? ""); amount.BorderBrush = Edge; if (v != Cur().Amount) GeldFieldEdit(amount.Name!, "Betrag ändern", () => Money.Replace(i0, Cur() with { Amount = v })); }
                catch (Exception ex) when (ex is FormatException or OverflowException) { amount.BorderBrush = Brush.Parse("#F0644B"); }
            };
            void CommitDate()
            {
                if (!DateOnly.TryParse(date.Text ?? "", GeldDe, out var d)) { date.BorderBrush = Brush.Parse("#F0644B"); return; }
                if (d != Cur().Date) GeldChange("Datum ändern", () => Money.Replace(i0, Cur() with { Date = d }));
            }
            date.KeyDown += (_, a) => { if (a.Key == Key.Enter) CommitDate(); };
            date.LostFocus += (_, _) => CommitDate();
            cat.SelectionChanged += (_, _) => { if (cat.SelectedItem is string s && s != Cur().Category) GeldChange($"Kategorie „{s}“", () => { Money.SetCategory(i0, s); MoneyMemory.Learn(Cur().Text, s); }, refresh: false); };
            vat.SelectionChanged += (_, _) => { var r = vat.SelectedIndex switch { 1 => LegalRates.VatReduced, 2 => LegalRates.VatStandard, _ => 0m }; if (r != Cur().VatRate) GeldChange("MwSt ändern", () => Money.Replace(i0, Cur() with { VatRate = r }), refresh: false); };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*,120,160,100"), Background = Brush.Parse(k % 2 == 0 ? "#0CFFFFFF" : "#00000000"), Children = { date, Pos(text, 1), Pos(amount, 2), Pos(cat, 3), Pos(vat, 4) } };
            var menu = new ContextMenu();
            var miRev = new MenuItem { Name = $"geld-b-storno-{k}", Header = "Stornieren (Gegenbuchung)" }; miRev.Click += (_, _) => GeldChange("Stornieren", () => Money.Reverse(i0, DateOnly.FromDateTime(DateTime.Today)));
            var miDel = new MenuItem { Name = $"geld-b-loeschen-{k}", Header = "Löschen (mit Rückgängig)" }; miDel.Click += (_, _) => GeldChange("Buchung löschen", () => Money.RemoveAt(i0));
            var miDup = new MenuItem { Header = "Doppeln" }; miDup.Click += (_, _) => GeldChange("Buchung doppeln", () => Money.Add(Cur()));
            menu.Items.Add(miRev); menu.Items.Add(miDup); menu.Items.Add(miDel); row.ContextMenu = menu;
            table.Children.Add(row);
        }
        if (rows.Count == 0) table.Children.Add(P(all.Count == 0 ? "Noch keine Buchungen. Erster Schritt: Kontoauszug öffnen oder hierher ziehen – oder unter „Übersicht“ eine Buchung eintragen." : "Keine Buchung passt zur Auswahl.", Muted, 12.5));
        var drop = new Border { Name = "geld-drop", Background = Brushes.Transparent, Child = table, MinHeight = 120 };
        DragDrop.SetAllowDrop(drop, true);
        drop.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        drop.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var f = e.DataTransfer.TryGetFiles()?.Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
            if (f is not null) { GeldImport(f); e.Handled = true; }
        });
        var sum = rows.Sum(x => x.e.Amount);
        var sp = new StackPanel { Spacing = 8, Children = { bar, Card(new StackPanel { Spacing = 4, Children = { new ScrollViewer { Content = drop, MaxHeight = 520 },
            new TextBlock { Name = "geld-tabelle-summe", Text = $"{rows.Count} Buchungen · Summe {GeldEuro(sum)} · Rechtsklick auf eine Zeile: Stornieren, Doppeln, Löschen", Foreground = Muted, FontSize = 11.5 } } }) } };
        if (_hbImport is not null && _geldTab == "buchungen") sp.Children.Insert(1, HouseholdImportPreview());
        if (_geldMapping is not null) sp.Children.Insert(1, GeldMappingCard());
        return sp;
    }

    /// <summary>Datei wählen (Dialog) und als Kontoauszug ansehen.</summary>
    public async Task GeldImportPick()
    {
        var req = new PickRequest("geld-import", false, false, ["csv", "txt", "xml", "sta", "mta", "940"]);
        string[]? got;
        if (PickOverride is { } fake) got = await fake(req);
        else got = (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Kontoauszug öffnen", FileTypeFilter = [new("Kontoauszug (CSV, CAMT.053, MT940)") { Patterns = ["*.csv", "*.txt", "*.xml", "*.sta", "*.mta", "*.940"] }, new("Alle Dateien") { Patterns = ["*"] }] }))
            .Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is [var f, ..]) GeldImport(f);
    }

    // Spaltenzuordnung, wenn die CSV-Spalten nicht sicher erkannt wurden
    (string Path, SpaltenUnklarException Ex)? _geldMapping;
    BankAuszug? _geldAuszug; List<(BankImport.Zahlungsvorschlag V, bool On)> _geldVorschlaege = [];
    public BankAuszug? GeldAuszug => _geldAuszug;
    public IReadOnlyList<BankImport.Zahlungsvorschlag> GeldZahlungsvorschlaege => [.. _geldVorschlaege.Select(x => x.V)];

    /// <summary>Kontoauszug (CSV, CAMT.053, MT940) lesen und als Vorschau zeigen: Kategorien, Dubletten, Zahlungseingänge zu offenen Rechnungen. Übernommen wird erst mit „Ja“.</summary>
    public void GeldImport(string path, SpaltenZuordnung? map = null)
    {
        _geldTab = "buchungen"; _geldMapping = null;
        try
        {
            var mem = new SpaltenGedaechtnis(_core.DataRoot);
            var a = map is null ? BankImport.Lesen(path, mem) : BankImport.LesenText(BankImport.Decode(File.ReadAllBytes(path)), mem, map);
            var entries = BankImport.Buchungen(a.Zeilen, MoneyMemory);
            _hbImport = BankImport.Vorschau(Money.All(), entries); _hbImportName = Path.GetFileName(path); _geldAuszug = a;
            var fresh = _hbImport.Where(x => !x.Duplicate).Select(x => x.Entry).ToList();
            _geldVorschlaege = [.. BankImport.Zahlungsvorschlaege(GeldBuch(), fresh).Select(v => (v, v.Sicherheit is "sicher" or "teilzahlung"))];
            _geldMsg = $"{a.Format}: {a.Kontrolle}" + (a.Hinweise.Count > 0 ? " " + string.Join(" ", a.Hinweise) : "");
        }
        catch (SpaltenUnklarException ex) { _geldMapping = (path, ex); _hbImport = null; _geldMsg = ex.Message; }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException or ArgumentException or InvalidDataException or OverflowException)
        { _hbImport = null; _geldMsg = "⚠ Kontoauszug nicht lesbar: " + Trendo.Core.FriendlyError.Say(ex); }
        Show("finanzen");
    }

    Control GeldMappingCard()
    {
        var (path, ex) = _geldMapping!.Value;
        var cols = new List<string> { "— keine —" }; cols.AddRange(ex.Kopf.Select((h, i) => $"{i + 1}: {h}" + (i < ex.Beispiel.Length ? $" (z. B. „{ex.Beispiel[i]}“)" : "")));
        ComboBox Pick(string name, int sel) => new() { Name = name, ItemsSource = cols, SelectedIndex = sel + 1, MinWidth = 260 };
        var d = Pick("geld-map-datum", ex.Vorschlag.Datum); var b = Pick("geld-map-betrag", ex.Vorschlag.Betrag); var z = Pick("geld-map-zweck", ex.Vorschlag.Zweck); var g = Pick("geld-map-gegen", ex.Vorschlag.Gegenseite);
        var ok = Act("geld-map-ok", "Übernehmen und merken", () =>
        {
            var m = new SpaltenZuordnung(d.SelectedIndex - 1, b.SelectedIndex - 1, z.SelectedIndex - 1, g.SelectedIndex - 1);
            if (!m.Brauchbar) { _geldMsg = "⚠ Bitte mindestens Datum und Betrag zuordnen."; Show("finanzen"); return; }
            new SpaltenGedaechtnis(_core.DataRoot).Merken(ex.Kopf, m);
            GeldImport(path, m);
        }, true);
        return Card(new StackPanel { Name = "geld-map", Spacing = 6, Children = { H("Welche Spalte ist was?", 14), P("Die Spalten dieser Datei kenne ich noch nicht. Zeig es mir einmal – beim nächsten Auszug derselben Bank geht es von selbst.", Muted, 11.5),
            Line(P("Datum", Text), d), Line(P("Betrag", Text), b), Line(P("Verwendungszweck", Text), z), Line(P("Empfänger/Auftraggeber", Text), g),
            Line(ok, Act("geld-map-cancel", "Abbrechen", () => { _geldMapping = null; _geldMsg = ""; Show("finanzen"); })) } });
    }

    /// <summary>Teil der Import-Vorschau: Zahlungseingänge, die zu offenen Rechnungen passen – vorgeschlagen, nicht von selbst gebucht.</summary>
    Control? GeldVorschlagCard()
    {
        if (_geldVorschlaege.Count == 0) return null;
        var sp = new StackPanel { Name = "geld-vorschlaege", Spacing = 4, Children = { H("Zahlungseingänge zu offenen Rechnungen", 13.5) } };
        for (var i = 0; i < _geldVorschlaege.Count; i++)
        {
            var (v, on) = _geldVorschlaege[i]; var ii = i;
            var cb = new CheckBox { Name = $"geld-vorschlag-{i}", IsChecked = on, Content = new TextBlock { Text = $"{v.Nummer}: {GeldEuro(v.Betrag)} – {v.Grund}" + (v.Sicherheit == "unsicher" ? " (bitte prüfen)" : ""), Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 12 } };
            cb.IsCheckedChanged += (_, _) => _geldVorschlaege[ii] = (_geldVorschlaege[ii].V, cb.IsChecked == true);
            sp.Children.Add(cb);
        }
        sp.Children.Add(P("Angehakte Rechnungen werden mit „Ja, übernehmen“ als bezahlt markiert.", Muted, 11));
        return sp;
    }

    /// <summary>Beim Übernehmen eines Auszugs: angehakte Vorschläge zuordnen. Die Bankbuchung bekommt den Steuersatz der Rechnung (bei mehreren Sätzen anteilig aufgeteilt).</summary>
    List<Entry> GeldZahlungenAnwenden(List<Entry> take)
    {
        var on = _geldVorschlaege.Where(x => x.On && x.V.Sicherheit != "pruefen").Select(x => x.V).ToList();
        if (on.Count == 0) return take;
        var fresh = _hbImport!.Where(x => !x.Duplicate).Select(x => x.Entry).ToList();
        var buch = GeldBuch(); var res = new List<Entry>(take);
        foreach (var v in on)
        {
            var bank = fresh[v.Zeile]; var beleg = buch.Vorgaenge.FirstOrDefault(x => x.Nummer == v.Nummer); if (beleg is null) continue;
            var parts = Rechnungen.ZahlungZuordnen(beleg, v.Betrag, bank.Date, "Kontoauszug " + _hbImportName);
            var at = res.IndexOf(bank); if (at < 0) continue;
            res.RemoveAt(at);
            res.InsertRange(at, parts.Count == 1 ? [bank with { VatRate = parts[0].VatRate, Category = "Einnahmen" }] : parts.Select(p => bank with { Amount = p.Amount, VatRate = p.VatRate, Category = "Einnahmen", Text = bank.Text + $" ({p.VatRate:0.##} %)" }));
        }
        GeldStore.Speichern(buch);
        _geldVorschlaege = [];
        return res;
    }

    // ---------------------------------------------------------------- Rechnungen
    Control GeldRechnungen(RechnungsBuch buch, int level)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var neu = Act("geld-re-neu", "+ Neue Rechnung", () => GeldNeuerBeleg("rechnung"), true);
        var angebot = Act("geld-an-neu", "+ Angebot", () => GeldNeuerBeleg("angebot"));
        var list = new StackPanel { Name = "geld-re-liste", Spacing = 2 };
        var ordered = buch.Vorgaenge.OrderByDescending(v => v.Entwurf).ThenByDescending(v => v.Datum).ThenByDescending(v => v.Nummer).ToList();
        foreach (var v in ordered)
        {
            var st = Rechnungen.Status(v, today); var s = Rechnungen.Summe(v);
            var color = st.Id switch { "bezahlt" or "angenommen" => "#2FBF71", "ueberfaellig" => "#F0644B", "entwurf" => "#94A3B8", "storniert" => "#64748B", _ => "#F5A524" };
            var who = v.KundeAnschrift.Split('\n')[0];
            var b = new Button { Name = "geld-re-" + (v.Entwurf ? v.Id : v.Nummer), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 5), CornerRadius = new CornerRadius(8),
                Background = _geldSel == v.Id ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderThickness = new Thickness(0),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                    new StackPanel { Children = { new TextBlock { Text = $"{Rechnungen.Name(v.Art)} {(v.Entwurf ? "(Entwurf)" : v.Nummer)}", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5 },
                        new TextBlock { Text = (who.Length > 0 ? who + " · " : "") + st.Text, Foreground = Brush.Parse(color), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis } } },
                    Pos(new TextBlock { Text = GeldEuro(s.Brutto), Foreground = Text, VerticalAlignment = VerticalAlignment.Center, FontSize = 12.5 }, 1) } } };
            var id = v.Id; b.Click += (_, _) => { _geldSel = id; Show("finanzen"); };
            var menu = new ContextMenu();
            void Mi(string header, Action a) { var mi = new MenuItem { Header = header }; mi.Click += (_, _) => a(); menu.Items.Add(mi); }
            if (!v.Entwurf) { Mi("PDF öffnen", () => GeldOeffnen(v)); if (v.Art == "rechnung" && !v.Storniert && Rechnungen.Offen(v) > 0) Mi("Als bezahlt markieren", () => GeldZahlung(id, Rechnungen.Offen(v), today)); }
            if (v.Entwurf) Mi("Entwurf löschen", () => GeldChange("Entwurf löschen", () => { var bb = GeldBuch(); bb.Vorgaenge.RemoveAll(x => x.Id == id); GeldStore.Speichern(bb); }));
            if (v.Art == "rechnung" && !v.Entwurf && !v.Storniert) Mi("Stornieren", () => GeldStorno(id));
            b.ContextMenu = menu;
            list.Children.Add(b);
        }
        if (ordered.Count == 0) list.Children.Add(P("Noch keine Rechnung. Erster Schritt: „+ Neue Rechnung“ – deine Firmendaten trägst du einmal unter „Meine Firma“ ein.", Muted, 12));
        var offen = buch.Vorgaenge.Where(v => v.Art == "rechnung" && !v.Entwurf && !v.Storniert).Sum(Rechnungen.Offen);
        var ueber = buch.Vorgaenge.Count(v => Rechnungen.Status(v, today).Id == "ueberfaellig");
        var left = new StackPanel { Spacing = 6, Width = 320, Children = { Line(neu, angebot),
            new TextBlock { Name = "geld-re-offen", Text = $"Offen: {GeldEuro(offen)}" + (ueber > 0 ? $" · {ueber} überfällig" : ""), Foreground = ueber > 0 ? Brush.Parse("#F0644B") : Muted, FontSize = 12 },
            new ScrollViewer { Content = list, MaxHeight = 560 } } };
        var sel = buch.Vorgaenge.FirstOrDefault(v => v.Id == _geldSel);
        Control right = sel is null ? P("Wähle links einen Beleg oder lege eine neue Rechnung an.", Muted, 12.5) : GeldBelegEditor(buch, sel, level);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,16,*"), Children = { Card(left), Pos(Card(right), 2) } };
        return new StackPanel { Spacing = 8, Children = { grid, GeldFirmaCard(buch, level), GeldKiCard(), GeldSchnellRechnung() } };
    }

    /// <summary>Neuen Entwurf anlegen (Rechnung oder Angebot) – mit Rückgängig.</summary>
    public Vorgang GeldNeuerBeleg(string art)
    {
        Vorgang? v = null;
        GeldChange(art == "angebot" ? "Neues Angebot" : "Neue Rechnung", () =>
        {
            var b = GeldBuch(); v = Rechnungen.Neu(b, art, DateOnly.FromDateTime(DateTime.Today)); v.Positionen.Add(new Position { Ust = b.Firma.Kleinunternehmer ? 0 : LegalRates.VatStandard });
            b.Vorgaenge.Add(v); GeldStore.Speichern(b); _geldSel = v.Id; _geldTab = "rechnungen";
        });
        return v!;
    }

    /// <summary>Beleg im gespeicherten Buch ändern (für Felder, Tests und die KI).</summary>
    public void GeldBeleg(string id, Action<RechnungsBuch, Vorgang> change, string label = "Rechnung ändern", bool refresh = true)
    {
        GeldChange(label, () =>
        {
            var b = GeldBuch(); var v = b.Vorgaenge.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Diesen Beleg gibt es nicht mehr.");
            change(b, v); GeldStore.Speichern(b);
        }, refresh);
    }

    void GeldBelegFeld(string id, string field, Action<RechnungsBuch, Vorgang> change) =>
        GeldFieldEdit(field, "Rechnung ändern", () =>
        {
            var b = GeldBuch(); var v = b.Vorgaenge.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Diesen Beleg gibt es nicht mehr.");
            if (!v.Entwurf) throw new InvalidOperationException("Ein festgeschriebener Beleg lässt sich nicht mehr ändern – bei Fehlern stornieren.");
            change(b, v); GeldStore.Speichern(b);
        });

    Control GeldBelegEditor(RechnungsBuch buch, Vorgang v, int level)
    {
        var id = v.Id; var edit = v.Entwurf; var s = Rechnungen.Summe(v);
        var sp = new StackPanel { Name = "geld-editor", Spacing = 6 };
        sp.Children.Add(H($"{Rechnungen.Name(v.Art)} {(v.Entwurf ? "– Entwurf (Nummer kommt beim Festschreiben)" : v.Nummer)}", 15));
        TextBox Box(string name, string text, string hint, double w, Action<RechnungsBuch, Vorgang, string> set, bool multi = false)
        {
            var t = new TextBox { Name = name, Text = text, PlaceholderText = hint, Width = w, IsReadOnly = !edit, AcceptsReturn = multi, Height = multi ? 78 : double.NaN, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap };
            t.TextChanged += (_, _) => { if (edit) GeldBelegFeld(id, name, (b, x) => set(b, x, t.Text ?? "")); GeldLive(); };
            ToolTip.SetTip(t, hint); return t;
        }
        var kunde = Box("geld-kunde", v.KundeAnschrift, "Kunde: Name, Straße, PLZ Ort (je Zeile)", 300, (_, x, t) => x.KundeAnschrift = t.Replace("\r", ""), true);
        var mail = Box("geld-kunde-mail", v.KundeEmail, "E-Mail des Kunden (für XRechnung)", 220, (_, x, t) => x.KundeEmail = t.Trim());
        var refx = Box("geld-referenz", v.Referenz, "Bestellnummer / Leitweg-ID", 220, (_, x, t) => x.Referenz = t.Trim());
        var datum = Box("geld-datum", BizJson.TextOf(v.Datum), "Rechnungsdatum TT.MM.JJJJ", 120, (_, x, t) => { if (BizJson.Datum(t) is { } d) x.Datum = BizJson.Iso(d); });
        var leist = Box("geld-leistung", v.Leistung, "Leistungsdatum oder -zeitraum", 170, (_, x, t) => x.Leistung = t.Trim());
        var ziel = Box("geld-ziel", v.ZahlungszielTage.ToString(GeldDe), "Zahlungsziel in Tagen", 70, (_, x, t) => { if (int.TryParse(t, out var n) && n is >= 0 and < 366) x.ZahlungszielTage = n; });
        sp.Children.Add(new WrapPanel { Children = { kunde, new StackPanel { Spacing = 4, Margin = new Thickness(8, 0), Children = { mail, refx, Line(datum, leist, ziel) } } } });
        // Positionen
        var saetze = buch.Steuer.Erlaubt().Select(x => x.ToString("0.##", GeldDe) + " %").ToList();
        var pos = new StackPanel { Name = "geld-positionen", Spacing = 2 };
        pos.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,70,100,100,70,90,110,34"), Children = { P("Leistung", Muted, 11), Pos(P("Menge", Muted, 11), 1), Pos(P("Einheit", Muted, 11), 2), Pos(P("Einzelpreis €", Muted, 11), 3), Pos(P("Rabatt %", Muted, 11), 4), Pos(P("USt", Muted, 11), 5), Pos(P("Gesamt netto", Muted, 11), 6) } });
        for (var i = 0; i < v.Positionen.Count; i++)
        {
            var p = v.Positionen[i]; var ii = i;
            TextBox Cell(string n, string t, Action<Position, string> set, bool right = false)
            {
                var tb = new TextBox { Name = $"geld-pos-{n}-{i}", Text = t, IsReadOnly = !edit, FontSize = 12, Margin = new Thickness(0, 0, 4, 0), TextAlignment = right ? TextAlignment.Right : TextAlignment.Left };
                tb.TextChanged += (_, _) => { if (edit) GeldBelegFeld(id, tb.Name!, (_, x) => { if (ii < x.Positionen.Count) set(x.Positionen[ii], tb.Text ?? ""); }); GeldLive(); };
                return tb;
            }
            static decimal Num(string t) => BizJson.TryBetrag(t.Replace("%", ""), out var d) ? d : throw new FormatException($"„{t}“ ist keine Zahl.");
            var unit = new ComboBox { Name = $"geld-pos-einheit-{i}", ItemsSource = Rechnungen.Einheiten.Contains(p.Einheit) ? Rechnungen.Einheiten : [.. Rechnungen.Einheiten, p.Einheit], SelectedItem = p.Einheit, IsEnabled = edit, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 4, 0) };
            unit.SelectionChanged += (_, _) => { if (edit && unit.SelectedItem is string u && u != p.Einheit) GeldBelegFeld(id, unit.Name!, (_, x) => x.Positionen[ii].Einheit = u); };
            var ust = new ComboBox { Name = $"geld-pos-ust-{i}", ItemsSource = saetze, SelectedItem = p.Ust.ToString("0.##", GeldDe) + " %", IsEnabled = edit && !v.Kleinunternehmer, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 4, 0) };
            ust.SelectionChanged += (_, _) => { if (edit && ust.SelectedItem is string u) GeldBelegFeld(id, ust.Name!, (_, x) => x.Positionen[ii].Ust = Num(u)); GeldLive(); };
            var del = Act($"geld-pos-del-{i}", "✕", () => GeldBeleg(id, (_, x) => x.Positionen.RemoveAt(ii), "Position entfernen")); del.IsEnabled = edit;
            pos.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,70,100,100,70,90,110,34"), Children = {
                Cell("text", p.Text, (x, t) => x.Text = t), Pos(Cell("menge", BizJson.Num(p.Menge), (x, t) => x.Menge = Num(t), true), 1), Pos(unit, 2),
                Pos(Cell("preis", p.Preis.ToString("0.00##", GeldDe), (x, t) => x.Preis = Num(t), true), 3), Pos(Cell("rabatt", p.Rabatt == 0 ? "" : BizJson.Num(p.Rabatt), (x, t) => x.Rabatt = t.Trim().Length == 0 ? 0 : Num(t), true), 4), Pos(ust, 5),
                Pos(new TextBlock { Name = $"geld-pos-summe-{i}", Text = GeldEuro(Rechnungen.PosNetto(p)), Foreground = Text, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, 6), Pos(del, 7) } });
        }
        var addPos = Act("geld-pos-neu", "+ Position", () => GeldBeleg(id, (b, x) => x.Positionen.Add(new Position { Ust = x.Kleinunternehmer ? 0 : b.Steuer.Erlaubt().First() }), "Position hinzufügen")); addPos.IsEnabled = edit;
        var artikel = new ComboBox { Name = "geld-artikel", ItemsSource = buch.Artikel.Select(a => $"{a.Name} ({GeldEuro(a.Preis)}/{a.Einheit})").ToList(), PlaceholderText = "Artikel übernehmen …", MinWidth = 220, IsEnabled = edit && buch.Artikel.Count > 0 };
        artikel.SelectionChanged += (_, _) => { if (artikel.SelectedIndex >= 0) { var a = buch.Artikel[artikel.SelectedIndex]; GeldBeleg(id, (_, x) => x.Positionen.Add(Rechnungen.AusArtikel(a)), "Artikel übernehmen"); } };
        sp.Children.Add(pos); sp.Children.Add(Line(addPos, artikel));
        if (!v.Kleinunternehmer && v.Positionen.Any(p => p.Ust == 0)) sp.Children.Add(Box("geld-befreiung", v.Befreiung, "Grund für 0 % (z. B. „Steuerfreie Leistung nach § 4 Nr. … UStG“)", 520, (_, x, t) => x.Befreiung = t.Trim()));
        // Summen und Pflichtangaben (live)
        var sums = new TextBlock { Name = "geld-summen", Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        var check = new StackPanel { Name = "geld-pflicht", Spacing = 1 };
        _geldLive = () =>
        {
            var b = GeldBuch(); var x = b.Vorgaenge.FirstOrDefault(y => y.Id == id); if (x is null) return;
            var su = Rechnungen.Summe(x);
            sums.Text = $"Netto {GeldEuro(su.Netto)}" + string.Concat(su.JeSatz.Where(_ => !x.Kleinunternehmer).Select(g => $" · USt {g.Satz.ToString("0.##", GeldDe)} %: {GeldEuro(g.Steuer)}")) + $" · Gesamt {GeldEuro(su.Brutto)}" + (x.Kleinunternehmer ? " · " + Rechnungen.KleinunternehmerHinweis : "");
            check.Children.Clear();
            if (x.Art != "rechnung") return;
            foreach (var c in Rechnungen.Pruefen(b, x))
                check.Children.Add(new TextBlock { Text = (c.Ok ? "✓ " : "✗ ") + c.Text + (c.Ok ? "" : " – " + c.Hinweis), Foreground = Brush.Parse(c.Ok ? "#2FBF71" : "#F0644B"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        };
        _geldLive();
        sp.Children.Add(sums);
        if (v.Art == "rechnung") sp.Children.Add(new Expander { Name = "geld-pflicht-box", Header = "Pflichtangaben nach § 14 UStG", IsExpanded = edit, Content = check });
        // Aktionen: genau eine goldene je Zustand
        var actions = new WrapPanel { Name = "geld-aktionen" };
        void A(Button b) { b.Margin = new Thickness(0, 0, 6, 6); actions.Children.Add(b); }
        if (v.Entwurf)
        {
            A(Act("geld-festschreiben", v.Art == "rechnung" ? "Festschreiben und PDF erstellen" : $"{Rechnungen.Name(v.Art)} festschreiben und PDF erstellen", () => GeldFestschreiben(id), true));
            A(Act("geld-vorschau", "Vorschau-PDF", () => GeldOeffnen(v)));
        }
        else
        {
            A(Act("geld-pdf", "PDF öffnen", () => GeldOeffnen(v)));
            if (v.Art == "rechnung" && !v.Storniert)
            {
                A(Act("geld-zugferd", "E-Rechnung (ZUGFeRD-PDF)", () => GeldERechnung(id, false), Rechnungen.Offen(v) > 0 && !File.Exists(GeldDatei(v, ".pdf").Replace(".pdf", "-zugferd.pdf"))));
                if (level >= Trendo.Core.Programs.UserLevel.Erweitert) A(Act("geld-xrechnung", "XRechnung (XML)", () => GeldERechnung(id, true)));
                if (Rechnungen.Offen(v) > 0)
                {
                    var betrag = new TextBox { Name = "geld-zahlung-betrag", Text = Rechnungen.Offen(v).ToString("0.00", GeldDe), Width = 100 };
                    var am = new TextBox { Name = "geld-zahlung-datum", Text = DateTime.Today.ToString("dd.MM.yyyy", GeldDe), Width = 100 };
                    A(Act("geld-zahlung", "Zahlungseingang zuordnen", () => { try { GeldZahlung(id, Finance.ParseAmount(betrag.Text ?? ""), DateOnly.Parse(am.Text ?? "", GeldDe)); } catch (FormatException) { _geldMsg = "⚠ Bitte Betrag und Datum (TT.MM.JJJJ) prüfen."; Show("finanzen"); } }));
                    actions.Children.Add(Line(betrag, am));
                }
                A(Act("geld-storno", "Stornieren", () => GeldStorno(id)));
            }
            if (v.Art is "angebot" or "auftrag" && !v.Storniert) A(Act("geld-weiter", "→ Rechnung daraus machen", () => GeldWeiter(id)));
            if (v.Zahlungen.Count > 0) sp.Children.Add(P("Zahlungen: " + string.Join(" · ", v.Zahlungen.Select(z => $"{BizJson.TextOf(z.Datum)} {GeldEuro(z.Betrag)}" + (z.Hinweis.Length > 0 ? $" ({z.Hinweis})" : ""))), Muted, 11.5));
        }
        sp.Children.Add(actions);
        sp.Children.Add(P("Es wird nichts versendet. Die Dateien liegen im Ordner „Business/Rechnungen“. Keine Steuerberatung.", Muted, 11));
        return sp;
    }
    Action? _geldLive;
    void GeldLive() => _geldLive?.Invoke();

    string GeldOrdner { get { var d = Path.Combine(BusinessFolder, "Rechnungen"); Directory.CreateDirectory(d); return d; } }
    string GeldDatei(Vorgang v, string ext) => Path.Combine(GeldOrdner, (v.Entwurf ? "Entwurf-" + v.Id : v.Nummer) + ext);

    /// <summary>Festschreiben: Pflichtangaben prüfen (fehlt etwas, steht genau da was), Nummer vergeben, PDF schreiben. Rückgabe: PDF-Pfad oder null.</summary>
    public string? GeldFestschreiben(string id)
    {
        string? pdf = null;
        var ok = GeldChange("Festschreiben", () =>
        {
            var b = GeldBuch(); var v = b.Vorgaenge.First(x => x.Id == id);
            var miss = Rechnungen.Fehlend(b, v);
            if (miss.Count > 0) throw new InvalidOperationException("Noch nicht vollständig – " + string.Join(" ", miss));
            Rechnungen.Festschreiben(b, v, GeldKreis);
            GeldStore.Speichern(b);
            pdf = GeldDatei(v, ".pdf"); RechnungPdf.Write(b, v, pdf, _settings.InvoiceLogo);
            _core.Audit.Append(Principal.LocalOwner.Name, "finance.invoice", v.Nummer);
            _geldMsg = $"✓ {Rechnungen.Name(v.Art)} {v.Nummer} festgeschrieben und als PDF gespeichert: {pdf}";
        }, refresh: false);
        if (!ok) return null;
        Say(_geldMsg, "finanzen"); return pdf;
    }

    void GeldOeffnen(Vorgang v)
    {
        try
        {
            var b = GeldBuch(); var x = b.Vorgaenge.First(y => y.Id == v.Id); var f = GeldDatei(x, ".pdf");
            if (x.Entwurf || !File.Exists(f)) RechnungPdf.Write(b, x, f, _settings.InvoiceLogo);
            Launch(f); _geldMsg = "PDF: " + f;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { _geldMsg = "⚠ " + ex.Message; }
        Show("finanzen");
    }

    /// <summary>E-Rechnung schreiben: ZUGFeRD-PDF (false) oder XRechnung-XML (true). Danach wieder eingelesen – die Beträge müssen stimmen. Rückgabe: Pfad oder null.</summary>
    public string? GeldERechnung(string id, bool xrechnung)
    {
        var b = GeldBuch(); var v = b.Vorgaenge.First(x => x.Id == id);
        var miss = Rechnungen.ERechnungFehlend(b, v, xrechnung);
        if (miss.Count > 0) { _geldMsg = "⚠ Für die " + (xrechnung ? "XRechnung" : "E-Rechnung") + " fehlt noch: " + string.Join(" ", miss); Say(_geldMsg, "finanzen"); return null; }
        try
        {
            string f; ERechnung.Gelesen back;
            if (xrechnung) { f = Path.Combine(GeldOrdner, ERechnung.FileName(v.Nummer)); var xml = Rechnungen.XRechnung(b, v); AtomicFile.WriteAllBytes(f, xml); back = ERechnung.ReadDetailed(File.ReadAllBytes(f)); }
            else { f = GeldDatei(v, "-zugferd.pdf"); AtomicFile.WriteAllBytes(f, ZugferdPdf.Build(b, v, _settings.InvoiceLogo)); back = ERechnung.ReadDetailed(ZugferdPdf.Extract(f) ?? throw new InvalidDataException("Die eingebettete Rechnung fehlt.")); }
            var s = Rechnungen.Summe(v);
            if (back.Gross != s.Brutto || back.Vat != s.Steuer || back.Number != v.Nummer) throw new InvalidDataException($"Kontrolle fehlgeschlagen: gelesen {GeldEuro(back.Gross)}, erwartet {GeldEuro(s.Brutto)}.");
            _core.Audit.Append(Principal.LocalOwner.Name, xrechnung ? "finance.xrechnung" : "finance.zugferd", v.Nummer);
            _geldMsg = $"✓ {(xrechnung ? "XRechnung" : "E-Rechnung (ZUGFeRD)")} {v.Nummer} gespeichert und wieder eingelesen – {GeldEuro(back.Gross)} brutto stimmt: {f}. Nichts wurde versendet.";
            Say(_geldMsg, "finanzen"); return f;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or InvalidOperationException) { _geldMsg = "⚠ " + ex.Message; Say(_geldMsg, "finanzen"); return null; }
    }

    /// <summary>Zahlungseingang zuordnen: Rechnung (teil)bezahlt, Einnahme(n) je Steuersatz ins Haushaltsbuch – ein Rückgängig-Schritt.</summary>
    public void GeldZahlung(string id, decimal betrag, DateOnly am) => GeldChange("Zahlung zuordnen", () =>
    {
        var b = GeldBuch(); var v = b.Vorgaenge.First(x => x.Id == id);
        var entries = Rechnungen.ZahlungZuordnen(v, betrag, am, "von Hand");
        GeldStore.Speichern(b); Money.Import(entries);
        _geldMsg = $"Zahlung {GeldEuro(betrag)} zu {v.Nummer} zugeordnet – {Rechnungen.Status(v, DateOnly.FromDateTime(DateTime.Today)).Text}.";
    });

    public void GeldStorno(string id) => GeldChange("Stornieren", () =>
    {
        var b = GeldBuch(); var v = b.Vorgaenge.First(x => x.Id == id);
        var s = Rechnungen.Stornieren(b, v, DateOnly.FromDateTime(DateTime.Today), GeldKreis); b.Vorgaenge.Add(s);
        GeldStore.Speichern(b); RechnungPdf.Write(b, s, GeldDatei(s, ".pdf"), _settings.InvoiceLogo); _geldSel = s.Id;
        _geldMsg = $"{v.Nummer} storniert – Stornorechnung {s.Nummer} als PDF gespeichert.";
    });

    public void GeldWeiter(string id) => GeldChange("Rechnung aus Angebot", () =>
    {
        var b = GeldBuch(); var v = b.Vorgaenge.First(x => x.Id == id);
        var n = Rechnungen.Weiter(b, v, "rechnung", DateOnly.FromDateTime(DateTime.Today)); n.Referenz = v.Referenz; n.KundeEmail = v.KundeEmail;
        b.Vorgaenge.Add(n); GeldStore.Speichern(b); _geldSel = n.Id;
        _geldMsg = $"Rechnungsentwurf aus {Rechnungen.Name(v.Art)} {v.Nummer} angelegt – prüfen und festschreiben.";
    });

    Control GeldFirmaCard(RechnungsBuch buch, int level)
    {
        var f = buch.Firma;
        TextBox Fld(string name, string text, string hint, double w, Action<Firma, string> set)
        {
            var t = new TextBox { Name = name, Text = text, PlaceholderText = hint, Width = w }; ToolTip.SetTip(t, hint);
            t.TextChanged += (_, _) => GeldFieldEdit(name, "Meine Firma ändern", () => { var b = GeldBuch(); set(b.Firma, (t.Text ?? "").Trim()); GeldStore.Speichern(b); });
            return t;
        }
        var klein = new CheckBox { Name = "geld-firma-klein", IsChecked = f.Kleinunternehmer, Content = new TextBlock { Text = "Kleinunternehmer (§ 19 UStG – keine Umsatzsteuer)", Foreground = Text } };
        klein.IsCheckedChanged += (_, _) => GeldChange("Kleinunternehmer", () => { var b = GeldBuch(); b.Firma.Kleinunternehmer = klein.IsChecked == true; foreach (var v in b.Vorgaenge.Where(v => v.Entwurf)) v.Kleinunternehmer = b.Firma.Kleinunternehmer; GeldStore.Speichern(b); }, refresh: false);
        var sp = new StackPanel { Spacing = 4, Children = {
            P("Einmal eintragen – steht dann auf jeder Rechnung. Bleibt nur auf diesem PC.", Muted, 11.5),
            Line(Fld("geld-firma-name", f.Name, "Name oder Firma", 240, (x, v) => x.Name = v), Fld("geld-firma-strasse", f.Strasse, "Straße und Hausnummer", 220, (x, v) => x.Strasse = v), Fld("geld-firma-ort", f.PlzOrt, "PLZ Ort", 180, (x, v) => x.PlzOrt = v)),
            Line(Fld("geld-firma-steuernr", f.SteuerNr, "Steuernummer", 160, (x, v) => x.SteuerNr = v), Fld("geld-firma-ustid", f.UstId, "USt-IdNr. (DE…)", 150, (x, v) => x.UstId = v),
                Fld("geld-firma-mail", f.Email, "E-Mail", 200, (x, v) => x.Email = v), Fld("geld-firma-tel", f.Telefon, "Telefon", 140, (x, v) => x.Telefon = v)),
            Line(Fld("geld-firma-iban", f.Iban, "IBAN", 240, (x, v) => x.Iban = v.Replace(" ", "")), Fld("geld-firma-bank", f.Bank, "Bank", 160, (x, v) => x.Bank = v),
                Fld("geld-firma-ziel", f.Zahlungsziel.ToString(GeldDe), "Zahlungsziel Tage", 80, (x, v) => { if (int.TryParse(v, out var n) && n is >= 0 and < 366) x.Zahlungsziel = n; }), klein) } };
        if (f.Iban.Length > 0 && !ERechnung.IbanValid(f.Iban)) sp.Children.Add(P("⚠ Die IBAN hat eine falsche Prüfziffer.", Brush.Parse("#F0644B"), 11.5));
        if (level >= Trendo.Core.Programs.UserLevel.Erweitert)
        {
            var saetze = new TextBox { Name = "geld-steuersaetze", Text = string.Join("; ", buch.Steuer.Erlaubt().Select(x => x.ToString("0.##", GeldDe))), Width = 140 };
            var stand = new TextBox { Name = "geld-steuer-stand", Text = buch.Steuer.Stand, Width = 110 };
            var save = Act("geld-steuer-save", "Sätze übernehmen", () => GeldChange("Steuersätze", () => { var b = GeldBuch(); b.Steuer.Saetze = Steuersaetze.Lesen(saetze.Text ?? ""); b.Steuer.Stand = (stand.Text ?? "").Trim(); GeldStore.Speichern(b); }));
            sp.Children.Add(Line(P("Umsatzsteuersätze (%):", Text), saetze, P("Stand:", Text), stand, save));
            sp.Children.Add(P($"Vorgabe nach {buch.Steuer.Quelle}. Ändert sich ein Satz, hier eintragen und das Stand-Datum anpassen. Keine Steuerberatung.", Muted, 11));
        }
        return new Expander { Name = "geld-firma", Header = "Meine Firma" + (f.Name.Length == 0 ? " – noch nicht eingetragen" : ": " + f.Name), IsExpanded = f.Name.Length == 0 && _geldTab == "rechnungen", Content = sp };
    }

    /// <summary>Rechnung aus einer Beschreibung anlegen (Zeilen „Kunde: …“, „Position: Leistung | Menge | Einheit | Preis | USt | Rabatt“). Ergebnis ist ein Entwurf im offenen Buch,
    /// mit Rückgängig – festgeschrieben und als PDF/E-Rechnung gespeichert wird mit den Knöpfen. Claude erreicht das Feld über die Seitenbrücke (feld_setzen, klicken).</summary>
    Control GeldKiCard()
    {
        var box = new TextBox { Name = "geld-ki-text", AcceptsReturn = true, Height = 90, Width = 620, TextWrapping = TextWrapping.Wrap,
            PlaceholderText = "Kunde: Beispiel GmbH, Hauptstraße 5, 54321 Beispielhausen\nPosition: Beratung | 6 | Std. | 85 | 19\nLeistung: November 2026" };
        var go = Act("geld-ki-anlegen", "Als Rechnungsentwurf anlegen", () => GeldEntwurfAusText(box.Text ?? ""));
        return new Expander { Name = "geld-ki", Header = "Rechnung aus Beschreibung (auch für Claude)", Content = new StackPanel { Spacing = 4, Children = { box, go,
            P("Daraus entsteht ein echter Entwurf mit Positionen und Summen – prüfen, festschreiben, als PDF oder E-Rechnung speichern. Es wird nichts versendet.", Muted, 11) } } };
    }

    /// <summary>Entwurf aus Text (Kunde, Positionen, Leistung, Referenz, Art). Rückgabe: der Entwurf oder null mit Meldung.</summary>
    public Vorgang? GeldEntwurfAusText(string text)
    {
        Vorgang? res = null;
        GeldChange("Rechnung aus Beschreibung", () =>
        {
            var b = GeldBuch(); var v = Rechnungen.AusText(b, text, DateOnly.FromDateTime(DateTime.Today));
            b.Vorgaenge.Add(v); GeldStore.Speichern(b); _geldSel = v.Id; _geldTab = "rechnungen"; res = v;
            var miss = v.Art == "rechnung" ? Rechnungen.Fehlend(b, v) : [];
            _geldMsg = $"Entwurf angelegt: {v.Positionen.Count} Positionen, {GeldEuro(Rechnungen.Summe(v).Brutto)}." + (miss.Count > 0 ? " Noch offen: " + string.Join(" ", miss) : " Alle Pflichtangaben da – du kannst festschreiben.");
        });
        return res;
    }

    // ---------------------------------------------------------------- Auswertung
    int _geldAwJahr = DateTime.Today.Year; int? _geldAwMonat;

    Control GeldAuswertungView(int level)
    {
        var a = GeldAuswertung.Rechnen(Money.All(), _geldAwJahr, _geldAwMonat);
        var months = new List<string> { "ganzes Jahr" }; months.AddRange(Enumerable.Range(1, 12).Select(m => GeldDe.DateTimeFormat.GetMonthName(m)));
        var mon = new ComboBox { Name = "geld-aw-monat", ItemsSource = months, SelectedIndex = _geldAwMonat ?? 0, MinWidth = 140 };
        mon.SelectionChanged += (_, _) => { var n = mon.SelectedIndex <= 0 ? (int?)null : mon.SelectedIndex; if (n != _geldAwMonat) { _geldAwMonat = n; Show("finanzen"); } };
        var head = Line(Act("geld-aw-prev", "◀", () => { _geldAwJahr--; Show("finanzen"); }), new TextBlock { Name = "geld-aw-jahr", Text = _geldAwJahr.ToString(GeldDe), FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = GoldText() },
            Act("geld-aw-next", "▶", () => { _geldAwJahr++; Show("finanzen"); }), mon,
            Act("geld-aw-pdf", "Als PDF speichern", () => GeldExport("pdf"), true), Act("geld-aw-xlsx", "Als Excel (XLSX)", () => GeldExport("xlsx")), Act("geld-aw-csv", "Als CSV", () => GeldExport("csv")));
        var kpis = new WrapPanel { Name = "geld-aw-kpis", Children = {
            Kpi("geld-aw-ein", "Einnahmen netto", GeldEuro(a.EinnahmenNetto), a.Titel, "#2FBF71", "#167A45"),
            Kpi("geld-aw-aus", "Ausgaben netto", GeldEuro(a.AusgabenNetto), a.Titel, "#C9A227", "#8A6A12"),
            Kpi("geld-aw-ueberschuss", "Überschuss", GeldEuro(a.Ueberschuss), "Einnahmen − Ausgaben", "#C9A227", "#8A6A12"),
            Kpi("geld-aw-ust", "USt − Vorsteuer", GeldEuro(a.Zahllast), $"{GeldEuro(a.Umsatzsteuer)} − {GeldEuro(a.Vorsteuer)}", "#64748B", "#334155") } };
        var chart = new BarChart { Name = "geld-aw-chart", Height = 200, Labels = MonthShort, Foreground = Muted };
        chart.Series.Add(("Einnahmen", "#2FBF71", a.Monate.Select(m => (double)m.Einnahmen).ToArray()));
        chart.Series.Add(("Ausgaben", "#F0644B", a.Monate.Select(m => (double)m.Ausgaben).ToArray()));
        Control Cats(string title, IReadOnlyList<KategorieSumme> list)
        {
            var sp = new StackPanel { Spacing = 2, Children = { H(title, 13.5) } };
            foreach (var k in list) sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,110,90,110"), Children = { new TextBlock { Text = $"{k.Kategorie} ({k.Anzahl})", Foreground = Text, FontSize = 12 },
                Pos(new TextBlock { Text = GeldEuro(k.Netto), Foreground = Text, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right }, 1), Pos(new TextBlock { Text = GeldEuro(k.Steuer), Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right }, 2),
                Pos(new TextBlock { Text = GeldEuro(k.Brutto), Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right }, 3) } });
            if (list.Count == 0) sp.Children.Add(P("Keine.", Muted, 12));
            return sp;
        }
        var sp = new StackPanel { Spacing = 8, Children = { head, kpis, Card(new StackPanel { Children = { H($"Einnahmen und Ausgaben {_geldAwJahr}", 14), chart } }),
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), Children = { Card(Cats("Einnahmen je Kategorie (netto · Steuer · brutto)", a.Einnahmen)), Pos(Card(Cats("Ausgaben je Kategorie (netto · Steuer · brutto)", a.Ausgaben)), 2) } } } };
        sp.Children.Add(FinanceExtras()); // Logo und Steuerberater-ZIP
        if (level >= Trendo.Core.Programs.UserLevel.Erweitert) sp.Children.Add(GeldDatevCard());
        sp.Children.Add(P(GeldAuswertung.Hinweis + " ELSTER: bewusst nicht eingebaut – die Übermittlung ans Finanzamt machst du im ELSTER-Portal oder dein Steuerberater.", Muted, 11));
        return sp;
    }

    /// <summary>Auswertung des gewählten Zeitraums speichern ("csv", "xlsx" oder "pdf"). Rückgabe: Pfad.</summary>
    public string? GeldExport(string format)
    {
        try
        {
            var a = GeldAuswertung.Rechnen(Money.All(), _geldAwJahr, _geldAwMonat);
            var f = Path.Combine(BusinessFolder, $"Auswertung-{(_geldAwMonat is { } m ? $"{_geldAwJahr}-{m:00}" : _geldAwJahr.ToString(GeldDe))}.{format}");
            switch (format)
            {
                case "csv": AtomicFile.WriteAllBytes(f, [.. System.Text.Encoding.UTF8.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes(GeldAuswertung.Csv(a))]); break;
                case "xlsx": GeldAuswertung.Xlsx(a, f); break;
                default: GeldAuswertung.Pdf(a, f, GeldBuch().Firma.Name); break;
            }
            _geldMsg = "Gespeichert: " + f; Say(_geldMsg, "finanzen"); return f;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _geldMsg = "⚠ " + Trendo.Core.FriendlyError.Say(ex); Say(_geldMsg, "finanzen"); return null; }
    }

    Control GeldDatevCard()
    {
        var k = DatevKonten.Laden(_core.DataRoot);
        var berater = Field("geld-datev-berater", "Beraternummer", 120, k.Berater); var mandant = Field("geld-datev-mandant", "Mandantennummer", 130, k.Mandant); var bank = Field("geld-datev-bank", "Bankkonto (z. B. 1200)", 150, k.Bank);
        var go = Act("geld-datev", "DATEV-Buchungsstapel speichern", () => GeldDatev(berater.Text ?? "", mandant.Text ?? "", bank.Text ?? ""));
        return Card(Section("DATEV-Buchungsstapel für den Steuerberater", Datev.Kennzeichnung + $" Konten: {k.Kontenrahmen}, Stand {k.Stand}; anpassbar in finanzen/datev-konten.json.", Line(berater, mandant, bank, go)));
    }

    public string? GeldDatev(string berater, string mandant, string bank)
    {
        try
        {
            var k = DatevKonten.Laden(_core.DataRoot); k.Berater = berater.Trim(); k.Mandant = mandant.Trim(); if (bank.Trim().Length > 0) k.Bank = bank.Trim(); k.Speichern(_core.DataRoot);
            var f = Datev.Schreiben(Money.All(), _geldAwJahr, k, BusinessFolder);
            _core.Audit.Append(Principal.LocalOwner.Name, "finance.datev", _geldAwJahr.ToString(GeldDe));
            _geldMsg = $"DATEV-Buchungsstapel {_geldAwJahr} gespeichert: {f} – {Datev.Kennzeichnung}"; Say(_geldMsg, "finanzen"); return f;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _geldMsg = "⚠ " + Trendo.Core.FriendlyError.Say(ex); Say(_geldMsg, "finanzen"); return null; }
    }

    // ---------------------------------------------------------------- Kredit
    string _krBetrag = "10.000", _krZins = "5", _krMonate = "60", _krRate = "", _krSonder = "0", _krBindung = "", _krGebuehr = "0";
    public KreditPlan? GeldKreditPlan { get; private set; }

    Control GeldKredit(int level)
    {
        TextBox F(string name, string hint, string text, Action<string> set, double w = 120) { var t = new TextBox { Name = name, Text = text, PlaceholderText = hint, Width = w }; ToolTip.SetTip(t, hint); t.TextChanged += (_, _) => set(t.Text ?? ""); return t; }
        var calc = Act("geld-kredit-rechnen", "Tilgungsplan berechnen", () => GeldKreditRechnen(), true);
        var fields = new StackPanel { Spacing = 4, Children = {
            Line(P("Kreditbetrag €", Text), F("geld-kredit-betrag", "z. B. 10.000", _krBetrag, v => _krBetrag = v), P("Sollzins % p. a.", Text), F("geld-kredit-zins", "z. B. 5", _krZins, v => _krZins = v, 80)),
            Line(P("Dauer (Monate)", Text), F("geld-kredit-monate", "z. B. 60", _krMonate, v => _krMonate = v, 80), P("oder Monatsrate €", Text), F("geld-kredit-rate", "leer = aus der Dauer", _krRate, v => _krRate = v)) } };
        if (level >= Trendo.Core.Programs.UserLevel.Erweitert)
            fields.Children.Add(Line(P("Sondertilgung je Jahr €", Text), F("geld-kredit-sonder", "0", _krSonder, v => _krSonder = v, 100), P("Zinsbindung (Monate)", Text), F("geld-kredit-bindung", "leer = ganze Dauer", _krBindung, v => _krBindung = v), P("Gebühren €", Text), F("geld-kredit-gebuehr", "0", _krGebuehr, v => _krGebuehr = v, 90)));
        fields.Children.Add(calc);
        var sp = new StackPanel { Spacing = 8, Children = { Card(new StackPanel { Spacing = 6, Children = { H("Kredit-Rechner (Annuitätendarlehen)", 15), fields, P(Kredit.Hinweis, Muted, 11) } }) } };
        if (GeldKreditPlan is { } p)
        {
            sp.Children.Add(new WrapPanel { Name = "geld-kredit-kpis", Children = {
                Kpi("geld-kredit-rate-kpi", "Monatsrate", GeldEuro(p.Monatsrate), p.Laufzeit, "#C9A227", "#8A6A12"),
                Kpi("geld-kredit-zinsen", "Zinsen gesamt", GeldEuro(p.ZinsenGesamt), "Sondertilgungen " + GeldEuro(p.SonderGesamt), "#C9A227", "#8A6A12"),
                Kpi("geld-kredit-rest", "Restschuld am Ende", GeldEuro(p.Restschuld), $"nach {p.Monate} Monaten", "#64748B", "#334155"),
                Kpi("geld-kredit-eff", "Effektivzins (Näherung)", p.EffektivPa.ToString("0.00", GeldDe) + " %", "p. a.", "#2FBF71", "#167A45") } });
            var chart = new BarChart { Name = "geld-kredit-chart", Height = 180, Labels = [.. p.Jahre.Select(j => j.Jahr.ToString(GeldDe))], Foreground = Muted };
            chart.Series.Add(("Zinsen", "#F0644B", p.Jahre.Select(j => (double)j.Zins).ToArray())); chart.Series.Add(("Tilgung", "#2FBF71", p.Jahre.Select(j => (double)j.Tilgung).ToArray()));
            var rows = new StackPanel { Name = "geld-kredit-plan", Spacing = 1 };
            rows.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("60,100,*,*,*,*,*"), Children = { P("Monat", Muted, 11), Pos(P("Datum", Muted, 11), 1), Pos(P("Rate", Muted, 11), 2), Pos(P("Zinsen", Muted, 11), 3), Pos(P("Tilgung", Muted, 11), 4), Pos(P("Sondertilgung", Muted, 11), 5), Pos(P("Restschuld", Muted, 11), 6) } });
            foreach (var r in p.Raten.Take(600))
                rows.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("60,100,*,*,*,*,*"), Children = { P(r.Monat.ToString(GeldDe), Text, 11.5), Pos(P(r.Datum.ToString("dd.MM.yyyy", GeldDe), Text, 11.5), 1), Pos(P(GeldEuro(r.Rate), Text, 11.5), 2),
                    Pos(P(GeldEuro(r.Zins), Text, 11.5), 3), Pos(P(GeldEuro(r.Tilgung), Text, 11.5), 4), Pos(P(r.Sonder == 0 ? "" : GeldEuro(r.Sonder), Text, 11.5), 5), Pos(P(GeldEuro(r.Rest), Text, 11.5), 6) } });
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("Zinsen und Tilgung je Jahr", 14), chart,
                Line(Act("geld-kredit-pdf", "Tilgungsplan als PDF", () => GeldKreditExport("pdf")), Act("geld-kredit-csv", "Als CSV", () => GeldKreditExport("csv"))), new ScrollViewer { Content = rows, MaxHeight = 380 } } }));
        }
        return sp;
    }

    public KreditPlan? GeldKreditRechnen()
    {
        try
        {
            decimal Num(string t, string what) => BizJson.TryBetrag(t.Replace("%", ""), out var d) ? d : throw new FormatException($"Bitte {what} als Zahl eintragen.");
            int? Months(string t) => t.Trim().Length == 0 ? null : int.TryParse(t.Trim(), out var n) ? n : throw new FormatException("Bitte die Monate als ganze Zahl eintragen.");
            var rate = _krRate.Trim().Length == 0 ? (decimal?)null : Num(_krRate, "die Monatsrate");
            GeldKreditPlan = Kredit.Plan(Num(_krBetrag, "den Kreditbetrag"), Num(_krZins, "den Zins"), rate is null ? Months(_krMonate) ?? throw new FormatException("Bitte Dauer oder Monatsrate angeben.") : null, rate,
                DateOnly.FromDateTime(DateTime.Today), _krSonder.Trim().Length == 0 ? 0 : Num(_krSonder, "die Sondertilgung"), null, Months(_krBindung), _krGebuehr.Trim().Length == 0 ? 0 : Num(_krGebuehr, "die Gebühren"));
            _geldMsg = "";
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException) { GeldKreditPlan = null; _geldMsg = "⚠ " + ex.Message; }
        _geldTab = "kredit"; Show("finanzen");
        return GeldKreditPlan;
    }

    public string? GeldKreditExport(string format)
    {
        if (GeldKreditPlan is not { } p) return null;
        var f = Path.Combine(BusinessFolder, $"Tilgungsplan-{DateTime.Today:yyyy-MM-dd}.{format}");
        if (format == "csv") AtomicFile.WriteAllBytes(f, [.. System.Text.Encoding.UTF8.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes(Kredit.Csv(p))]); else Kredit.Pdf(p, f);
        _geldMsg = "Gespeichert: " + f; Say(_geldMsg, "finanzen"); return f;
    }
}
