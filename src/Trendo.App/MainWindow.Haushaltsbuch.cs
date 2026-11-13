using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Geld als Haushaltsbuch (Masterprompt 131): Monat blättern, Ausgaben als Ring (Klick filtert), Suche, Buchung in der Zeile ändern
/// mit Rückgängig, Fixkosten/Abos erkennen, Kontoauszug erst als Vorschau, Zuordnungsregeln sehen und anwenden.</summary>
public partial class MainWindow
{
    int _hbYear = DateTime.Today.Year, _hbMonth = DateTime.Today.Month; string? _hbCat; string _hbFind = ""; int _hbEdit = -1;
    readonly Stack<(string Label, List<Entry> Before)> _hbUndo = new();
    List<(Entry Entry, bool Duplicate)>? _hbImport; string _hbImportName = "";
    static readonly string[] HbColors = ["#F0644B", "#3D8BFF", "#F5A524", "#2FBF71", "#A855F7", "#14B8A6", "#EC4899", "#EAB308", "#64748B", "#0EA5E9", "#84CC16", "#F97316"];
    public string? HouseholdCategory => _hbCat;
    public string HouseholdUndoLabel => _hbUndo.TryPeek(out var u) ? u.Label : "";

    /// <summary>Änderung am Haushaltsbuch mit Rückgängig-Schritt.</summary>
    void HbChange(string label, Action a)
    {
        var before = Money.All();
        try { a(); _hbUndo.Push((label, before)); if (_hbUndo.Count > 30) { var keep = _hbUndo.Take(30).Reverse().ToList(); _hbUndo.Clear(); foreach (var k in keep) _hbUndo.Push(k); } }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or IOException) { Say(ex.Message, "finanzen"); return; }
        Show("finanzen");
    }

    static string HbColor(string cat) { var i = Array.IndexOf(FinanceExtra.AllCategories, cat); return HbColors[(i < 0 ? StableHash(cat) : i) % HbColors.Length]; }

    Control HouseholdView()
    {
        var all = Money.All(); var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        var month = HouseholdBook.Month(all, _hbYear, _hbMonth);
        var (inc, exp, left, rate) = HouseholdBook.Sum(month.Select(x => x.Entry));
        var title = new DateTime(_hbYear, _hbMonth, 1).ToString("MMMM yyyy", de);
        void Go(int d) { var t = new DateTime(_hbYear, _hbMonth, 1).AddMonths(d); _hbYear = t.Year; _hbMonth = t.Month; _hbCat = null; _hbEdit = -1; Show("finanzen"); }
        var undo = Act("hb-undo", _hbUndo.TryPeek(out var u) ? "↶ " + u.Label : "Rückgängig", () => { if (_hbUndo.TryPop(out var x)) { Money.Save(x.Before); Say("Rückgängig: " + x.Label, "finanzen"); Show("finanzen"); } });
        undo.IsEnabled = _hbUndo.Count > 0;
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Act("hb-prev", "◀", () => Go(-1)),
                new TextBlock { Name = "hb-month", Text = title, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = GoldText(), VerticalAlignment = VerticalAlignment.Center, MinWidth = 170, TextAlignment = TextAlignment.Center },
                Act("hb-next", "▶", () => Go(1)), Act("hb-today", "Heute", () => { _hbYear = DateTime.Today.Year; _hbMonth = DateTime.Today.Month; _hbCat = null; Show("finanzen"); }) } },
            Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { undo, Act("fin-to-sheet", "In Trendo Tabelle öffnen", () => Try("finanzen", () =>
            {
                var f = Path.Combine(BusinessFolder, $"Buchungen-{DateTime.Today:yyyy-MM-dd}.csv");
                File.WriteAllText(f, Finance.ExportCsv(Money.All()), new System.Text.UTF8Encoding(true)); SheetOpen(f); Show("tabelle");
            })) } }, 2) } };
        var kpis = new WrapPanel { Name = "hb-kpis", Children = {
            Kpi("hb-in", "Einnahmen", Eu(inc) + " €", title, "#2FBF71", "#167A45"),
            Kpi("hb-out", "Ausgaben", Eu(exp) + " €", $"{month.Count(x => x.Entry.Amount < 0)} Buchungen", "#F0644B", "#B2361F"),
            Kpi("hb-left", "Übrig", Eu(left) + " €", inc > 0 ? $"Sparquote {rate * 100:0} %" : "keine Einnahmen", left >= 0 ? "#3D8BFF" : "#F0644B", left >= 0 ? "#1E4FD8" : "#B2361F") } };

        // Links: Ring mit Kategorien (Klick filtert), Budget je Kategorie
        var cats = HouseholdBook.ByCategory(month.Select(x => x.Entry));
        var budgets = Budgets.All();
        var donut = new DonutChart { Name = "hb-donut", Width = 240, Height = 220, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Text, Muted = Muted, Center = Eu(exp) + " €", CenterSub = "Ausgaben",
            Slices = cats.Select(c => (c.Category, HbColor(c.Category), (double)c.Amount)).ToList(), Selected = _hbCat is null ? -1 : cats.FindIndex(c => c.Category == _hbCat) };
        donut.SliceClicked = i => { _hbCat = _hbCat == cats[i].Category ? null : cats[i].Category; _hbEdit = -1; Show("finanzen"); };
        HouseholdDonutForTests = donut;
        var legend = new StackPanel { Name = "hb-legend", Spacing = 2 };
        for (var i = 0; i < cats.Count; i++)
        {
            var (cat, amt) = cats[i]; var on = _hbCat == cat;
            var line = new StackPanel { Spacing = 2, Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), Background = Brush.Parse(HbColor(cat)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) },
                Pos(new TextBlock { Text = cat, Foreground = Text, FontSize = 12.5, FontWeight = on ? FontWeight.Bold : FontWeight.Normal }, 1),
                Pos(new TextBlock { Text = $"{Eu(amt)} € · {(exp > 0 ? amt / exp * 100 : 0):0} %", Foreground = Muted, FontSize = 11.5 }, 2) } } } };
            if (budgets.TryGetValue(cat, out var lim) && lim > 0)
            {
                var ratio = (double)(amt / lim); var col = ratio >= 1 ? "#F0644B" : ratio >= 0.9 ? "#F5A524" : "#2FBF71";
                line.Children.Add(new ProgressBar { Value = Math.Min(100, ratio * 100), Height = 4, MinHeight = 4, Foreground = Brush.Parse(col), Background = Brush.Parse("#22FFFFFF") });
                line.Children.Add(new TextBlock { Text = $"Budget {Eu(lim)} € – {(ratio >= 1 ? "überschritten" : $"noch {Eu(lim - amt)} €")}", Foreground = Brush.Parse(col), FontSize = 10.5 });
            }
            var b = new Button { Name = $"hb-cat-{i}", Content = line, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 4), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderThickness = new Thickness(0) };
            var cc = cat; b.Click += (_, _) => { _hbCat = _hbCat == cc ? null : cc; _hbEdit = -1; Show("finanzen"); };
            legend.Children.Add(b);
        }
        if (cats.Count == 0) legend.Children.Add(P("In diesem Monat noch keine Ausgaben.", Muted, 12));
        var leftCol = new StackPanel { Spacing = 8, Width = 300, Children = { donut, legend } };

        // Rechts: Buchungen des Monats
        var find = new TextBox { Name = "hb-find", Text = _hbFind, PlaceholderText = "Buchung suchen …", Width = 240, CornerRadius = new CornerRadius(8) };
        find.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _hbFind = find.Text ?? ""; Show("finanzen"); } };
        var tools = new WrapPanel { Children = { find } };
        if (_hbCat is not null) { var chip = Act("hb-cat-clear", $"Nur {_hbCat}", () => { _hbCat = null; Show("finanzen"); }, true); chip.Margin = new Thickness(6, 0, 0, 0); tools.Children.Add(chip); }
        if (_hbFind.Length > 0) { var chip = Act("hb-find-clear", $"„{_hbFind}“", () => { _hbFind = ""; Show("finanzen"); }); chip.Margin = new Thickness(6, 0, 0, 0); tools.Children.Add(chip); }
        var rows = month.Where(x => _hbCat is null || x.Entry.Category == _hbCat).Where(x => _hbFind.Length == 0 || x.Entry.Text.Contains(_hbFind, StringComparison.CurrentCultureIgnoreCase)).ToList();
        var table = new StackPanel { Name = "fin-table", Spacing = 1 };
        for (var k = 0; k < rows.Count; k++)
        {
            var (idx, e) = rows[k];
            if (idx == _hbEdit) { table.Children.Add(HouseholdEditor(idx, e)); continue; }
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("62,*,170,110,Auto,Auto"), Background = Brush.Parse(k % 2 == 0 ? "#0CFFFFFF" : "#00000000") };
            g.Children.Add(new TextBlock { Text = e.Date.ToString("dd.MM."), Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) });
            g.Children.Add(Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { new Border { Width = 4, Height = 18, CornerRadius = new CornerRadius(2), Background = Brush.Parse(HbColor(e.Category)) },
                new TextBlock { Text = e.Text, Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center } } }, 1));
            var catList = FinanceExtra.AllCategories.Contains(e.Category) ? FinanceExtra.AllCategories : [.. FinanceExtra.AllCategories, e.Category];
            var cb = new ComboBox { Name = $"fin-row-cat-{k}", ItemsSource = catList, SelectedItem = e.Category, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(4, 2) };
            var text = e.Text; var i0 = idx;
            cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is string s && s != e.Category) HbChange($"Kategorie „{s}“", () => { Money.SetCategory(i0, s); MoneyMemory.Learn(text, s); }); };
            g.Children.Add(Pos(cb, 2));
            g.Children.Add(Pos(new TextBlock { Text = Eu(e.Amount) + " €", Foreground = Brush.Parse(e.Amount < 0 ? "#F0644B" : "#2FBF71"), FontSize = 13, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, 3));
            g.Children.Add(Pos(Act($"hb-edit-{k}", "Ändern", () => { _hbEdit = i0; Show("finanzen"); }), 4));
            // T-0253: stornieren statt löschen – die Buchung bleibt sichtbar, eine Gegenbuchung gleicht sie aus
            var reversed = e.Text.StartsWith(Ledger.ReversalPrefix, StringComparison.Ordinal) || Ledger.IsReversed(all, e);
            var rev = Act($"fin-del-{k}", reversed ? "Storniert" : "Stornieren", () => HbChange("Stornieren", () => { Money.Reverse(i0, DateOnly.FromDateTime(DateTime.Today)); _core.Audit.Append(Principal.LocalOwner.Name, "finance.reverse", text); }));
            rev.IsEnabled = !reversed; g.Children.Add(Pos(rev, 5));
            table.Children.Add(g);
        }
        if (rows.Count == 0) table.Children.Add(P(month.Count == 0 ? "In diesem Monat noch keine Buchungen – oben eintragen oder einen Kontoauszug übernehmen." : "Keine Buchung passt zur Auswahl.", Muted, 12));
        var shownSum = rows.Sum(x => x.Entry.Amount);
        var rightCol = new StackPanel { Spacing = 6, Children = { tools, new ScrollViewer { Content = table, MaxHeight = 420 },
            new TextBlock { Name = "hb-sum", Text = $"{rows.Count} Buchungen · Summe {Eu(shownSum)} €", Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right } } };

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,16,*"), Children = { leftCol, Pos(rightCol, 2) } };
        var sp = new StackPanel { Name = "hb", Spacing = 10, Children = { head, kpis, body } };
        var page = new StackPanel { Spacing = 10, Children = { Card(sp) } };
        if (_hbImport is not null) page.Children.Add(HouseholdImportPreview());
        page.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), Children = { HouseholdRecurring(all), Pos(HouseholdRules(), 2) } });
        return page;
    }
    public DonutChart? HouseholdDonutForTests { get; private set; }

    Control HouseholdEditor(int idx, Entry e)
    {
        var date = new TextBox { Name = "hb-edit-date", Text = e.Date.ToString("dd.MM.yyyy"), Width = 105 };
        var text = new TextBox { Name = "hb-edit-text", Text = e.Text, Width = 240 };
        var amount = new TextBox { Name = "hb-edit-amount", Text = e.Amount.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE")), Width = 110 };
        var cat = new ComboBox { Name = "hb-edit-cat", ItemsSource = FinanceExtra.AllCategories, SelectedItem = FinanceExtra.AllCategories.Contains(e.Category) ? e.Category : "Sonstiges", Width = 150 };
        var save = Act("hb-edit-save", "Speichern", () => HbChange("Buchung ändern", () =>
        {
            var d = DateOnly.Parse(date.Text ?? "", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
            Money.Replace(idx, e with { Date = d, Text = (text.Text ?? "").Trim(), Amount = Finance.ParseAmount(amount.Text ?? ""), Category = (string)cat.SelectedItem! });
            if ((string)cat.SelectedItem! != e.Category) MoneyMemory.Learn(text.Text ?? "", (string)cat.SelectedItem!);
            _hbEdit = -1;
        }), true);
        var cancel = Act("hb-edit-cancel", "Abbrechen", () => { _hbEdit = -1; Show("finanzen"); });
        return new Border { Name = "hb-editor", Background = Brush.Parse("#22C9A227"), BorderBrush = Brush.Parse("#D4A84B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Margin = new Thickness(0, 2),
            Child = new WrapPanel { Children = { date, text, amount, cat, save, cancel } } };
    }

    /// <summary>Kontoauszug erst ansehen: geratene Kategorie je Zeile änderbar, Doppelte markiert – dann Ja oder Nein.</summary>
    public void HouseholdPreviewImport(string path)
    {
        try { _hbImport = HouseholdBook.Preview(Money.All(), Finance.ImportCsv(File.ReadAllText(path)), MoneyMemory); _hbImportName = Path.GetFileName(path); }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException or ArgumentException or InvalidDataException) { Say("Kontoauszug nicht lesbar: " + ex.Message, "finanzen"); _hbImport = null; }
        Show("finanzen");
    }

    Control HouseholdImportPreview()
    {
        var list = _hbImport!; var fresh = list.Count(x => !x.Duplicate);
        var rows = new StackPanel { Name = "hb-import-rows", Spacing = 1 };
        for (var i = 0; i < list.Count; i++)
        {
            var (e, dup) = list[i]; var ii = i;
            var cb = new ComboBox { Name = $"hb-imp-cat-{i}", ItemsSource = FinanceExtra.AllCategories, SelectedItem = FinanceExtra.AllCategories.Contains(e.Category) ? e.Category : "Sonstiges", FontSize = 12, Width = 160, IsEnabled = !dup };
            cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is string s) _hbImport![ii] = (_hbImport[ii].Entry with { Category = s }, _hbImport[ii].Duplicate); };
            rows.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("90,*,Auto,110,90"), Opacity = dup ? 0.45 : 1, Children = {
                new TextBlock { Text = e.Date.ToString("dd.MM.yyyy"), Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Text = e.Text, Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, 1),
                Pos(e.Amount > 0 ? new TextBlock { Text = "Einnahme", Foreground = Muted, FontSize = 12, Width = 160, VerticalAlignment = VerticalAlignment.Center } : cb, 2),
                Pos(new TextBlock { Text = Eu(e.Amount) + " €", Foreground = Brush.Parse(e.Amount < 0 ? "#F0644B" : "#2FBF71"), FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }, 3),
                Pos(new TextBlock { Text = dup ? "schon da" : "neu", Foreground = Brush.Parse(dup ? "#94A3B8" : "#60A5FA"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }, 4) } });
        }
        var yes = Act("hb-import-yes", $"Ja, {fresh} Buchungen übernehmen", () => HbChange("Kontoauszug übernehmen", () =>
        {
            var take = _hbImport!.Where(x => !x.Duplicate).Select(x => x.Entry).ToList();
            var guessed = HouseholdBook.Preview([], take, MoneyMemory).Select(x => x.Entry.Category).ToList();
            for (var i = 0; i < take.Count; i++) if (take[i].Amount < 0 && take[i].Category != guessed[i]) MoneyMemory.Learn(take[i].Text, take[i].Category); // vom Nutzer geändert → merken
            var n = Money.Import(take); BudgetWarn();
            _core.Audit.Append(Principal.LocalOwner.Name, "finance.import", $"{n} neu");
            Say($"{n} Buchungen übernommen.", "finanzen"); _hbImport = null;
            if (take.Count > 0) { var newest = take.Max(x => x.Date); _hbYear = newest.Year; _hbMonth = newest.Month; }
        }), true);
        yes.IsEnabled = fresh > 0;
        var no = Act("hb-import-no", "Nein, verwerfen", () => { _hbImport = null; Show("finanzen"); });
        return Card(new StackPanel { Name = "hb-import", Spacing = 6, Children = { H($"Kontoauszug „{_hbImportName}“ – {list.Count} Zeilen, {fresh} neu", 14),
            P("Prüfe die geratenen Kategorien. Was du hier änderst, merkt sich Trendo für den nächsten Auszug.", Muted, 11.5),
            new ScrollViewer { Content = rows, MaxHeight = 320 }, new WrapPanel { Children = { yes, new Border { Width = 6 }, no } } } });
    }

    Control HouseholdRecurring(List<Entry> all)
    {
        var rec = HouseholdBook.FindRecurring(all, DateOnly.FromDateTime(DateTime.Today));
        var sp = new StackPanel { Name = "hb-recurring", Spacing = 4 };
        sp.Children.Add(H(rec.Count == 0 ? "Fixkosten & Abos" : $"Fixkosten & Abos – etwa {Eu(rec.Sum(r => r.PerMonth))} € im Monat", 14));
        foreach (var r in rec)
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                new Border { Width = 4, Height = 30, CornerRadius = new CornerRadius(2), Background = Brush.Parse(HbColor(r.Category)), Margin = new Thickness(0, 0, 8, 0) },
                Pos(new StackPanel { Children = { new TextBlock { Text = r.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{r.Rhythm} · {r.Category} · nächste etwa am {r.Next:dd.MM.yyyy}", Foreground = Muted, FontSize = 11 } } }, 1),
                Pos(new TextBlock { Text = Eu(r.Amount) + " €", Foreground = Brush.Parse("#F0644B"), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, 2) } });
        if (rec.Count == 0) sp.Children.Add(P("Noch nichts erkannt. Sobald eine Ausgabe zweimal im passenden Abstand vorkommt (z. B. Miete, Streaming), steht sie hier.", Muted, 11.5));
        return Card(sp);
    }

    Control HouseholdRules()
    {
        var rules = MoneyMemory.All();
        var sp = new StackPanel { Name = "hb-rules", Spacing = 4 };
        sp.Children.Add(H("Zuordnungsregeln", 14));
        sp.Children.Add(P("Beginnt ein Buchungstext so, ordnet Trendo ihn dieser Kategorie zu. Regeln entstehen auch, wenn du eine Kategorie änderst.", Muted, 11.5));
        for (var i = 0; i < rules.Count && i < 40; i++)
        {
            var (key, cat) = rules[i];
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = {
                new TextBlock { Text = $"„{key} …“", Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Text = "→ " + cat, Foreground = Brush.Parse(HbColor(cat)), FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) }, 1),
                Pos(Act($"hb-rule-del-{i}", "✕", () => { MoneyMemory.Forget(key); Show("finanzen"); }), 2) } });
        }
        var text = new TextBox { Name = "hb-rule-text", PlaceholderText = "Text beginnt mit, z. B. Stadtwerke", Width = 220 };
        var cat2 = new ComboBox { Name = "hb-rule-cat", ItemsSource = FinanceExtra.AllCategories.Where(c => c != "Einnahmen").ToList(), SelectedIndex = 0, Width = 150 };
        var add = Act("hb-rule-add", "Regel anlegen", () => { if (CategoryMemory.Key(text.Text ?? "").Length == 0) { Say("Bitte ein Stichwort mit mindestens drei Buchstaben.", "finanzen"); return; } MoneyMemory.Learn(text.Text!, (string)cat2.SelectedItem!); Show("finanzen"); });
        var apply = Act("hb-rule-apply", "Auf alle Ausgaben anwenden", () => HbChange("Regeln anwenden", () =>
        {
            var a = Money.All(); var n = 0;
            for (var i = 0; i < a.Count; i++) if (a[i].Amount < 0 && MoneyMemory.Lookup(a[i].Text) is { } c && c != a[i].Category) { a[i] = a[i] with { Category = c }; n++; }
            Money.Save(a); Say(n == 0 ? "Alle Ausgaben passen schon zu den Regeln." : $"{n} Buchungen neu zugeordnet.", "finanzen");
        }));
        sp.Children.Add(new WrapPanel { Children = { text, cat2, add, apply } });
        return Card(sp);
    }
}
