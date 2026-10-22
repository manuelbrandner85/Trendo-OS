using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>BUS-017/018/019/020/023/024/025: Betrieb.</summary>
public partial class MainWindow
{
    public IdeaList<Member> BizMembers => new(_core.DataRoot, BizIdea.Id, "mitglieder");
    public IdeaList<PlanTier> BizTiers => new(_core.DataRoot, BizIdea.Id, "tarife");
    public IdeaList<BizContact> BizContacts => new(_core.DataRoot, BizIdea.Id, "kontakte");
    public IdeaList<Product> BizProducts => new(_core.DataRoot, BizIdea.Id, "ware");
    public IdeaList<Ticket> BizTickets => new(_core.DataRoot, BizIdea.Id, "anfragen");
    public IdeaList<DayStats> BizStats => new(_core.DataRoot, BizIdea.Id, "zahlen");
    private string _giro = "";
    private int? _edContact, _edTier, _edMember, _edProduct, _edTicket;
    /// <summary>Tests setzen das, damit der CSV-Dialog ohne echtes Fenster eine Datei liefert.</summary>
    public Func<string, Task<string?>>? BizFileDialog { get; set; }

    private void InitW5Ops() => RegisterTab(BusinessTabs, "biz-betrieb", "Betrieb", "BUS-019", OpsView, "kunden", "crm", "mitglieder", "bezahlen", "girocode", "iban", "lager", "lieferant", "support", "anfragen", "auswertung", "statistik");

    private Control OpsView()
    {
        var i = BizIdea; var sp = new StackPanel { Spacing = 8, Children = { P("Alles für den Alltag deines Geschäfts – Kunden, Bezahlen, Ware, Anfragen und Zahlen."), IdeaHeader("biz-betrieb") } };
        var today = DateOnly.FromDateTime(DateTime.Today);
        TextBox T(string n, string ph, double w = 140) => new() { Name = n, PlaceholderText = ph, Width = w };

        // BUS-019 Kontakte
        var contacts = BizContacts.All();
        var cn = T("op-c-name", "Name"); var cf = T("op-c-firm", "Firma"); var cm = T("op-c-mail", "E-Mail", 170); var cv = T("op-c-value", "Wert €", 80); var cd = T("op-c-next", "Nächster Termin TT.MM.JJJJ", 160);
        var stage = new ComboBox { Name = "op-c-stage", ItemsSource = BusinessOps.Stages, SelectedIndex = 0, Width = 120 };
        Mark(cn);
        if (_edContact is int eci && eci >= 0 && eci < contacts.Count)
        {
            var e = contacts[eci]; cn.Text = e.Name; cf.Text = e.Company; cm.Text = e.Email; cv.Text = e.Value.ToString("0.##", De); cd.Text = e.Next?.ToString("dd.MM.yyyy", De);
            var si = Array.IndexOf(BusinessOps.Stages, e.Stage); if (si >= 0) stage.SelectedIndex = si;
        }
        var csec = new StackPanel { Spacing = 4, Children = { H("Kundenkontakte", 14), new WrapPanel { Children = { cn, cf, cm, stage, cv, cd, Act("op-c-add", _edContact is null ? "Speichern" : "Änderung speichern", () =>
        {
            if (string.IsNullOrWhiteSpace(cn.Text)) { BizNeed("biz-betrieb", "op-c-name", "Bitte den Namen des Kontakts eintragen."); return; }
            DateOnly? next = DateOnly.TryParse(cd.Text, De, out var d) ? d : null;
            var item = new BizContact(cn.Text!.Trim(), cf.Text ?? "", cm.Text ?? "", BusinessOps.Stages[Math.Max(0, stage.SelectedIndex)], next, ParseD(cv.Text));
            if (_edContact is int ei && ei >= 0 && ei < BizContacts.All().Count) { var l = BizContacts.All(); l[ei] = item; BizContacts.Save(l); _edContact = null; BizOk("biz-betrieb", $"Kontakt „{item.Name}“ geändert."); }
            else { BizContacts.Add(item); BizOk("biz-betrieb", $"Kontakt „{item.Name}“ gespeichert."); }
        }, true) } } } };
        var due = BusinessOps.Due(contacts, today);
        csec.Children.Add(new TextBlock { Name = "op-due", Text = due.Count == 0 ? "Heute steht nichts an." : $"Heute melden: {string.Join(", ", due.Select(x => x.Name))}", Foreground = due.Count > 0 ? Brush.Parse("#FACC15") : Muted, TextWrapping = TextWrapping.Wrap });
        foreach (var s in BusinessOps.Stages) csec.Children.Add(P($"{s}: {string.Join(", ", contacts.Where(x => x.Stage == s).Select(x => x.Name))}", null, 11));
        for (var ci = 0; ci < contacts.Count; ci++)
        {
            var idx = ci; var c = contacts[ci];
            csec.Children.Add(Row(P($"{c.Name} · {c.Stage}", Text, 11), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                Act($"op-c-edit-{ci}", "Ändern", () => { _edContact = idx; BizOk("biz-betrieb", $"„{c.Name}“ steht in den Feldern. Änder und speichere."); }),
                Act($"op-c-del-{ci}", "Löschen", () => { BizContacts.RemoveAt(idx); if (_edContact == idx) _edContact = null; BizOk("biz-betrieb", $"Kontakt „{c.Name}“ gelöscht."); }) } }));
        }
        csec.Children.Add(P($"Erwarteter Wert offener Angebote: {BusinessOps.PipelineValue(contacts):N0} €", Text, 11));
        sp.Children.Add(Card(csec));

        // BUS-017 Mitglieder
        var tiers = BizTiers.All(); var members = BizMembers.All();
        var tn = T("op-t-name", "Tarif, z. B. Basis", 120); var tp = T("op-t-price", "€/Monat", 80); Mark(tn);
        var mn = T("op-m-name", "Name"); var mt = T("op-m-team", "Team", 100); var mp = new ComboBox { Name = "op-m-plan", ItemsSource = tiers.Select(t => t.Name).ToList(), SelectedIndex = 0, Width = 120 };
        var mr = new ComboBox { Name = "op-m-role", ItemsSource = BusinessOps.Roles, SelectedIndex = 2, Width = 110 }; Mark(mn);
        if (_edTier is int eti && eti >= 0 && eti < tiers.Count) { tn.Text = tiers[eti].Name; tp.Text = tiers[eti].PriceMonth.ToString("0.##", De); }
        if (_edMember is int emi && emi >= 0 && emi < members.Count)
        {
            var e = members[emi]; mn.Text = e.Name; mt.Text = e.Team;
            var pi = tiers.FindIndex(t => t.Name == e.Plan); if (pi >= 0) mp.SelectedIndex = pi;
            var ri = Array.IndexOf(BusinessOps.Roles, e.Role); if (ri >= 0) mr.SelectedIndex = ri;
        }
        var msec = new StackPanel { Spacing = 4, Children = { H("Mitglieder und Tarife", 14),
            new WrapPanel { Children = { tn, tp, Act("op-t-add", _edTier is null ? "Tarif anlegen" : "Tarif speichern", () =>
            {
                if (string.IsNullOrWhiteSpace(tn.Text)) { BizNeed("biz-betrieb", "op-t-name", "Bitte den Namen des Tarifs eintragen."); return; }
                var tier = new PlanTier(tn.Text!.Trim(), ParseD(tp.Text));
                if (_edTier is int ei && ei >= 0 && ei < BizTiers.All().Count) { var l = BizTiers.All(); l[ei] = tier; BizTiers.Save(l); _edTier = null; BizOk("biz-betrieb", $"Tarif „{tier.Name}“ geändert."); }
                else { BizTiers.Add(tier); BizOk("biz-betrieb", $"Tarif „{tier.Name}“ angelegt, {tier.PriceMonth:0.##} € im Monat."); }
            }) } },
            new WrapPanel { Children = { mn, mt, mp, mr, Act("op-m-add", _edMember is null ? "Mitglied anlegen" : "Mitglied speichern", () =>
            {
                if (BizTiers.All().Count == 0) { BizNeed("biz-betrieb", "op-t-name", "Bitte zuerst einen Tarif anlegen. Danach kannst du ein Mitglied anlegen."); return; }
                if (string.IsNullOrWhiteSpace(mn.Text)) { BizNeed("biz-betrieb", "op-m-name", "Bitte den Namen des Mitglieds eintragen."); return; }
                var plans = BizTiers.All();
                var mem = new Member(mn.Text!.Trim(), "", plans[Math.Max(0, Math.Min(mp.SelectedIndex, plans.Count - 1))].Name, string.IsNullOrWhiteSpace(mt.Text) ? "Standard" : mt.Text!.Trim(), BusinessOps.Roles[Math.Max(0, mr.SelectedIndex)], today);
                if (_edMember is int ei && ei >= 0 && ei < BizMembers.All().Count)
                {
                    var l = BizMembers.All(); mem = mem with { Since = l[ei].Since, Email = l[ei].Email, Active = l[ei].Active }; l[ei] = mem; BizMembers.Save(l); _edMember = null;
                    BizOk("biz-betrieb", $"Mitglied „{mem.Name}“ geändert.");
                }
                else { BizMembers.Add(mem); BizOk("biz-betrieb", $"Mitglied „{mem.Name}“ angelegt."); }
            }) } },
            new TextBlock { Name = "op-mrr", Text = $"{members.Count(m => m.Active)} aktive Mitglieder · {BusinessOps.Mrr(members, tiers):N2} € pro Monat", Foreground = Text } } };
        for (var ti = 0; ti < tiers.Count; ti++)
        {
            var idx = ti; var t = tiers[ti];
            msec.Children.Add(Row(new TextBlock { Name = $"op-t-row-{ti}", Text = $"{t.Name}: {t.PriceMonth:0.##} €/Monat", Foreground = Text, FontSize = 11.5 }, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                Act($"op-t-edit-{ti}", "Ändern", () => { _edTier = idx; BizOk("biz-betrieb", $"Tarif „{t.Name}“ steht in den Feldern."); }),
                Act($"op-t-del-{ti}", "Löschen", () => { BizTiers.RemoveAt(idx); if (_edTier == idx) _edTier = null; BizOk("biz-betrieb", $"Tarif „{t.Name}“ gelöscht."); }) } }));
        }
        foreach (var g in members.GroupBy(m => m.Team)) msec.Children.Add(P($"Team {g.Key}: {string.Join(", ", g.Select(m => $"{m.Name} ({m.Role}, {m.Plan})"))}", null, 11));
        for (var mi = 0; mi < members.Count; mi++)
        {
            var idx = mi; var m = members[mi];
            msec.Children.Add(Row(P($"{m.Name} · {m.Plan}", Text, 11), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                Act($"op-m-edit-{mi}", "Ändern", () => { _edMember = idx; BizOk("biz-betrieb", $"Mitglied „{m.Name}“ steht in den Feldern."); }),
                Act($"op-m-del-{mi}", "Löschen", () => { BizMembers.RemoveAt(idx); if (_edMember == idx) _edMember = null; BizOk("biz-betrieb", $"Mitglied „{m.Name}“ gelöscht."); }) } }));
        }
        sp.Children.Add(Card(msec));

        // BUS-018 Bezahlen
        var amt = Math.Max(i.Price, 1);
        var psec = new StackPanel { Spacing = 4, Children = { H("Bezahlen", 14), P($"Gebühren bei {amt:0.##} € (Richtwerte Oktober 2026, ohne Gewähr):", null, 11) } };
        foreach (var o in BusinessOps.PayOptions) psec.Children.Add(P($"{o.Name}: {BusinessOps.Fee(o, amt):0.00} € – {o.Note}", Text, 11));
        var gn = T("op-g-name", "Empfänger", 160); var gi = T("op-g-iban", "IBAN", 220); var ga = T("op-g-amount", "Betrag", 80); var gz = T("op-g-purpose", "Verwendungszweck", 180);
        Mark(gi);
        psec.Children.Add(new WrapPanel { Children = { gn, gi, ga, gz, Act("op-g-make", "GiroCode erstellen", () =>
        {
            if (string.IsNullOrWhiteSpace(gn.Text) || string.IsNullOrWhiteSpace(gi.Text) || string.IsNullOrWhiteSpace(ga.Text)) { BizNeed("biz-betrieb", "op-g-iban", "Bitte Empfänger, IBAN und Betrag eintragen."); return; }
            try { _giro = BusinessOps.GiroCode(gn.Text ?? "", gi.Text ?? "", Finance.ParseAmount(ga.Text ?? ""), gz.Text ?? ""); var f = System.IO.Path.Combine(BusinessFolder, "girocode.txt"); File.WriteAllText(f, _giro); BizOk("biz-betrieb", "GiroCode-Inhalt gespeichert. Ein QR-Programm macht daraus das Bild für die Rechnung."); }
            catch (Exception ex) when (ex is ArgumentException or FormatException) { BizNeed("biz-betrieb", "op-g-iban", "Hat nicht geklappt: " + ex.Message); }
        }) } });
        if (_giro.Length > 0) psec.Children.Add(new SelectableTextBlock { Name = "op-g-out", Text = _giro, Foreground = Text, FontFamily = new FontFamily("monospace"), FontSize = 11 });
        psec.Children.Add(P("Online-Zahlungen (PayPal, Stripe) brauchen ein eigenes Konto beim Anbieter. Trendo verbindet sich noch nicht direkt damit.", null, 10.5));
        sp.Children.Add(Card(psec));

        // BUS-020 Ware
        var prods = BizProducts.All();
        var pn = T("op-p-name", "Produkt"); var ps = T("op-p-sup", "Lieferant"); var pc = T("op-p-cost", "Einkauf €", 80); var pp = T("op-p-price", "Verkauf €", 80); var pst = T("op-p-stock", "Bestand", 70); var pl = T("op-p-lead", "Liefertage", 80); var pd = T("op-p-daily", "Verkauf/Tag", 90);
        Mark(pn);
        if (_edProduct is int epi && epi >= 0 && epi < prods.Count)
        {
            var e = prods[epi]; pn.Text = e.Name; ps.Text = e.Supplier; pc.Text = e.Cost.ToString("0.##", De); pp.Text = e.Price.ToString("0.##", De); pst.Text = e.Stock.ToString(); pl.Text = e.LeadDays.ToString(); pd.Text = e.DailySales.ToString("0.##", De);
        }
        var wsec = new StackPanel { Spacing = 4, Children = { H("Ware und Lieferanten", 14), new WrapPanel { Children = { pn, ps, pc, pp, pst, pl, pd, Act("op-p-add", _edProduct is null ? "Hinzufügen" : "Änderung speichern", () =>
        {
            if (string.IsNullOrWhiteSpace(pn.Text)) { BizNeed("biz-betrieb", "op-p-name", "Bitte den Namen der Ware eintragen."); return; }
            var item = new Product(pn.Text!.Trim(), ps.Text ?? "", ParseD(pc.Text), ParseD(pp.Text), (int)ParseD(pst.Text), (int)ParseD(pl.Text), ParseD(pd.Text));
            if (_edProduct is int ei && ei >= 0 && ei < BizProducts.All().Count) { var l = BizProducts.All(); l[ei] = item; BizProducts.Save(l); _edProduct = null; BizOk("biz-betrieb", $"Ware „{item.Name}“ geändert."); }
            else { BizProducts.Add(item); BizOk("biz-betrieb", $"Ware „{item.Name}“ hinzugefügt."); }
        }) } } } };
        var k = 0;
        foreach (var p in prods)
        {
            var idx = k;
            var label = new TextBlock { Name = $"op-p-row-{k}", Text = $"{(BusinessOps.NeedsOrder(p) ? "⚠ nachbestellen: " + BusinessOps.OrderQty(p) + " Stück · " : "✓ ")}{p.Name} ({p.Supplier}) · Bestand {p.Stock} · Marge {BusinessOps.UnitMargin(p):P0}", Foreground = BusinessOps.NeedsOrder(p) ? Brush.Parse("#FACC15") : Text, FontSize = 11.5 };
            wsec.Children.Add(Row(label, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                Act($"op-p-edit-{k}", "Ändern", () => { _edProduct = idx; BizOk("biz-betrieb", $"„{p.Name}“ steht in den Feldern."); }),
                Act($"op-p-del-{k}", "Löschen", () => { BizProducts.RemoveAt(idx); if (_edProduct == idx) _edProduct = null; BizOk("biz-betrieb", $"Ware „{p.Name}“ gelöscht."); }) } }));
            k++;
        }
        sp.Children.Add(Card(wsec));

        // BUS-023 Anfragen
        var tickets = BizTickets.All(); var now = DateTimeOffset.Now;
        var tt = T("op-k-title", "Frage oder Problem", 240); var tc = T("op-k-cust", "Kunde"); var tpr = new ComboBox { Name = "op-k-prio", ItemsSource = BusinessOps.Priorities, SelectedIndex = 1, Width = 100 };
        Mark(tt);
        if (_edTicket is int eki && eki >= 0 && eki < tickets.Count) { tt.Text = tickets[eki].Title; tc.Text = tickets[eki].Customer; var pri = Array.IndexOf(BusinessOps.Priorities, tickets[eki].Priority); if (pri >= 0) tpr.SelectedIndex = pri; }
        var ksec = new StackPanel { Spacing = 4, Children = { H("Kundenanfragen", 14), new WrapPanel { Children = { tt, tc, tpr, Act("op-k-add", _edTicket is null ? "Aufnehmen" : "Änderung speichern", () =>
        {
            if (string.IsNullOrWhiteSpace(tt.Text)) { BizNeed("biz-betrieb", "op-k-title", "Bitte die Frage oder das Problem eintragen."); return; }
            if (_edTicket is int ei && ei >= 0 && ei < BizTickets.All().Count)
            {
                var l = BizTickets.All(); l[ei] = l[ei] with { Title = tt.Text!.Trim(), Customer = tc.Text ?? "", Priority = BusinessOps.Priorities[Math.Max(0, tpr.SelectedIndex)] }; BizTickets.Save(l); _edTicket = null;
                BizOk("biz-betrieb", $"Anfrage „{tt.Text.Trim()}“ geändert.");
            }
            else { BizTickets.Add(new(tt.Text!.Trim(), tc.Text ?? "", BusinessOps.Priorities[Math.Max(0, tpr.SelectedIndex)], DateTimeOffset.Now)); BizOk("biz-betrieb", $"Anfrage „{tt.Text.Trim()}“ aufgenommen."); }
        }) } } } };
        for (var n = 0; n < tickets.Count; n++)
        {
            var idx = n; var t = tickets[n]; if (t.State == "gelöst") continue;
            var ans = new TextBox { Name = $"op-k-ans-{n}", PlaceholderText = "Antwort", Width = 240 };
            ksec.Children.Add(Row(P($"{(BusinessOps.Overdue(t, now) ? "⚠ überfällig · " : "")}{t.Priority} · {t.Title} ({t.Customer})", Text, 11), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { ans,
                Act($"op-k-solve-{n}", "Gelöst", () => { var l = BizTickets.All(); l[idx] = l[idx] with { State = "gelöst", Answer = ans.Text ?? "" }; BizTickets.Save(l); BizOk("biz-betrieb", $"Anfrage „{t.Title}“ ist gelöst."); }),
                Act($"op-k-edit-{n}", "Ändern", () => { _edTicket = idx; BizOk("biz-betrieb", $"Anfrage „{t.Title}“ steht in den Feldern."); }),
                Act($"op-k-del-{n}", "Löschen", () => { BizTickets.RemoveAt(idx); if (_edTicket == idx) _edTicket = null; BizOk("biz-betrieb", $"Anfrage „{t.Title}“ gelöscht."); }) } }));
        }
        ksec.Children.Add(P($"{tickets.Count(t => t.State != "gelöst")} offen · {tickets.Count(t => BusinessOps.Overdue(t, now))} überfällig · {tickets.Count(t => t.State == "gelöst")} gelöst", null, 11));
        ksec.Children.Add(Act("op-k-faq", "Häufige Fragen aus gelösten Anfragen erstellen", () => { var f = System.IO.Path.Combine(BusinessFolder, DevStudio.SafeName(i.Name) + "-FAQ.md"); File.WriteAllText(f, BusinessOps.Faq(BizTickets.All())); Launch(f); }));
        sp.Children.Add(Card(ksec));

        // BUS-024 + 025 Zahlen
        var stats = BizStats.All();
        var sc = T("op-s-csv", "CSV: Datum;Besucher;Anmeldungen;Verkäufe", 300); Mark(sc);
        var zsec = new StackPanel { Spacing = 4, Children = { H("Auswertung", 14), new WrapPanel { Children = { sc, Act("op-s-import", "Übernehmen", async () =>
        {
            var raw = (sc.Text ?? "").Trim().Trim('"');
            if (raw.Length == 0) { var picked = await PickStatsFileAsync(); if (!string.IsNullOrEmpty(picked)) raw = picked; }
            if (raw.Length == 0) { BizNeed("biz-betrieb", "op-s-csv", "Bitte eine CSV-Datei wählen oder die Tageszahlen hier einfügen."); return; }
            try
            {
                string text;
                if (File.Exists(raw)) text = File.ReadAllText(raw);
                else if (raw.Contains(';') || raw.Contains('\n')) text = raw;
                else { BizNeed("biz-betrieb", "op-s-csv", "Diese Datei gibt es nicht. Wähl die CSV im Dialog oder füg die Zeilen ein."); return; }
                var add = BusinessOps.ImportStats(text);
                if (add.Count == 0) { BizNeed("biz-betrieb", "op-s-csv", "Keine Tageszahlen erkannt. Eine Zeile sieht so aus: 01.10.2026;120;4;1"); return; }
                var l = BizStats.All(); l.RemoveAll(x => add.Any(a => a.Date == x.Date)); l.AddRange(add); BizStats.Save(l.OrderBy(x => x.Date).ToList());
                BizOk("biz-betrieb", add.Count == 1 ? "1 Tag übernommen." : $"{add.Count} Tage übernommen.");
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or InvalidDataException) { BizNeed("biz-betrieb", "op-s-csv", "Die Zahlen ließen sich nicht lesen: " + ex.Message); }
        }) } } } };
        if (stats.Count > 0) { var tr = BusinessOps.Analyze(stats); zsec.Children.Add(new TextBlock { Name = "op-trend", Text = $"Letzte 7 Tage: {tr.SignupRate:P1} melden sich an, {tr.SaleRate:P1} kaufen (davor {tr.PrevSaleRate:P1}). {tr.Text}", Foreground = Text, TextWrapping = TextWrapping.Wrap }); }
        var pv = BusinessOps.Compare(i, Money.All(), new DateOnly(today.Year, 1, 1), today);
        zsec.Children.Add(new TextBlock { Name = "op-plan", Text = $"Plan: {pv.PlanProfitMonth:N0} € Gewinn/Monat · Wirklich (Seite „Geld“, dieses Jahr): {pv.ActualProfitMonth:N0} €/Monat. {pv.Text}", Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Card(zsec));
        return BusinessPage("Betrieb", sp);
    }

    private async Task<string?> PickStatsFileAsync()
    {
        if (BizFileDialog is { } over) return await over("CSV mit Tageszahlen");
        try
        {
            var r = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "CSV mit Tageszahlen", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("CSV") { Patterns = ["*.csv", "*.txt"] }] });
            return r.Count > 0 ? r[0].TryGetLocalPath() : null;
        }
        catch (Exception) { return null; }
    }
}
