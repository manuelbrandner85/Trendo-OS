using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
        var csec = new StackPanel { Spacing = 4, Children = { H("Kundenkontakte", 14), new WrapPanel { Children = { cn, cf, cm, stage, cv, cd, Act("op-c-add", "Speichern", () =>
        {
            if (string.IsNullOrWhiteSpace(cn.Text)) return;
            DateOnly? next = DateOnly.TryParse(cd.Text, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var d) ? d : null;
            BizContacts.Add(new(cn.Text!, cf.Text ?? "", cm.Text ?? "", BusinessOps.Stages[Math.Max(0, stage.SelectedIndex)], next, ParseD(cv.Text))); Show("biz-betrieb");
        }, true) } } } };
        var due = BusinessOps.Due(contacts, today);
        csec.Children.Add(new TextBlock { Name = "op-due", Text = due.Count == 0 ? "Heute steht nichts an." : $"Heute melden: {string.Join(", ", due.Select(x => x.Name))}", Foreground = due.Count > 0 ? Brush.Parse("#FACC15") : Muted, TextWrapping = TextWrapping.Wrap });
        foreach (var s in BusinessOps.Stages) csec.Children.Add(P($"{s}: {string.Join(", ", contacts.Where(x => x.Stage == s).Select(x => x.Name))}", null, 11));
        csec.Children.Add(P($"Erwarteter Wert offener Angebote: {BusinessOps.PipelineValue(contacts):N0} €", Text, 11));
        sp.Children.Add(Card(csec));

        // BUS-017 Mitglieder
        var tiers = BizTiers.All(); var members = BizMembers.All();
        var tn = T("op-t-name", "Tarif, z. B. Basis", 120); var tp = T("op-t-price", "€/Monat", 80);
        var mn = T("op-m-name", "Name"); var mt = T("op-m-team", "Team", 100); var mp = new ComboBox { Name = "op-m-plan", ItemsSource = tiers.Select(t => t.Name).ToList(), SelectedIndex = 0, Width = 120 };
        var mr = new ComboBox { Name = "op-m-role", ItemsSource = BusinessOps.Roles, SelectedIndex = 2, Width = 110 };
        var msec = new StackPanel { Spacing = 4, Children = { H("Mitglieder und Tarife", 14),
            new WrapPanel { Children = { tn, tp, Act("op-t-add", "Tarif anlegen", () => { if (!string.IsNullOrWhiteSpace(tn.Text)) { BizTiers.Add(new(tn.Text!, ParseD(tp.Text))); Show("biz-betrieb"); } }) } },
            new WrapPanel { Children = { mn, mt, mp, mr, Act("op-m-add", "Mitglied anlegen", () => { if (string.IsNullOrWhiteSpace(mn.Text) || tiers.Count == 0) return; BizMembers.Add(new(mn.Text!, "", tiers[Math.Max(0, mp.SelectedIndex)].Name, string.IsNullOrWhiteSpace(mt.Text) ? "Standard" : mt.Text!, BusinessOps.Roles[Math.Max(0, mr.SelectedIndex)], today)); Show("biz-betrieb"); }) } },
            new TextBlock { Name = "op-mrr", Text = $"{members.Count(m => m.Active)} aktive Mitglieder · {BusinessOps.Mrr(members, tiers):N2} € pro Monat", Foreground = Text } } };
        foreach (var g in members.GroupBy(m => m.Team)) msec.Children.Add(P($"Team {g.Key}: {string.Join(", ", g.Select(m => $"{m.Name} ({m.Role}, {m.Plan})"))}", null, 11));
        sp.Children.Add(Card(msec));

        // BUS-018 Bezahlen
        var amt = Math.Max(i.Price, 1);
        var psec = new StackPanel { Spacing = 4, Children = { H("Bezahlen", 14), P($"Gebühren bei {amt:0.##} € (Richtwerte Oktober 2026, ohne Gewähr):", null, 11) } };
        foreach (var o in BusinessOps.PayOptions) psec.Children.Add(P($"{o.Name}: {BusinessOps.Fee(o, amt):0.00} € – {o.Note}", Text, 11));
        var gn = T("op-g-name", "Empfänger", 160); var gi = T("op-g-iban", "IBAN", 220); var ga = T("op-g-amount", "Betrag", 80); var gz = T("op-g-purpose", "Verwendungszweck", 180);
        psec.Children.Add(new WrapPanel { Children = { gn, gi, ga, gz, Act("op-g-make", "GiroCode erstellen", () =>
        {
            try { _giro = BusinessOps.GiroCode(gn.Text ?? "", gi.Text ?? "", Finance.ParseAmount(ga.Text ?? ""), gz.Text ?? ""); var f = System.IO.Path.Combine(BusinessFolder, "girocode.txt"); File.WriteAllText(f, _giro); Say("GiroCode-Inhalt gespeichert. Ein QR-Programm macht daraus das Bild für die Rechnung.", "biz-betrieb"); }
            catch (Exception ex) when (ex is ArgumentException or FormatException) { Say("Hat nicht geklappt: " + ex.Message, "biz-betrieb"); }
        }) } });
        if (_giro.Length > 0) psec.Children.Add(new SelectableTextBlock { Name = "op-g-out", Text = _giro, Foreground = Text, FontFamily = new FontFamily("monospace"), FontSize = 11 });
        psec.Children.Add(P("Online-Zahlungen (PayPal, Stripe) brauchen ein eigenes Konto beim Anbieter. Trendo verbindet sich noch nicht direkt damit.", null, 10.5));
        sp.Children.Add(Card(psec));

        // BUS-020 Ware
        var prods = BizProducts.All();
        var pn = T("op-p-name", "Produkt"); var ps = T("op-p-sup", "Lieferant"); var pc = T("op-p-cost", "Einkauf €", 80); var pp = T("op-p-price", "Verkauf €", 80); var pst = T("op-p-stock", "Bestand", 70); var pl = T("op-p-lead", "Liefertage", 80); var pd = T("op-p-daily", "Verkauf/Tag", 90);
        var wsec = new StackPanel { Spacing = 4, Children = { H("Ware und Lieferanten", 14), new WrapPanel { Children = { pn, ps, pc, pp, pst, pl, pd, Act("op-p-add", "Hinzufügen", () =>
        { if (string.IsNullOrWhiteSpace(pn.Text)) return; BizProducts.Add(new(pn.Text!, ps.Text ?? "", ParseD(pc.Text), ParseD(pp.Text), (int)ParseD(pst.Text), (int)ParseD(pl.Text), ParseD(pd.Text))); Show("biz-betrieb"); }) } } } };
        var k = 0;
        foreach (var p in prods) wsec.Children.Add(new TextBlock { Name = $"op-p-row-{k++}", Text = $"{(BusinessOps.NeedsOrder(p) ? "⚠ nachbestellen: " + BusinessOps.OrderQty(p) + " Stück · " : "✓ ")}{p.Name} ({p.Supplier}) · Bestand {p.Stock} · Marge {BusinessOps.UnitMargin(p):P0}", Foreground = BusinessOps.NeedsOrder(p) ? Brush.Parse("#FACC15") : Text, FontSize = 11.5 });
        sp.Children.Add(Card(wsec));

        // BUS-023 Anfragen
        var tickets = BizTickets.All(); var now = DateTimeOffset.Now;
        var tt = T("op-k-title", "Frage oder Problem", 240); var tc = T("op-k-cust", "Kunde"); var tpr = new ComboBox { Name = "op-k-prio", ItemsSource = BusinessOps.Priorities, SelectedIndex = 1, Width = 100 };
        var ksec = new StackPanel { Spacing = 4, Children = { H("Kundenanfragen", 14), new WrapPanel { Children = { tt, tc, tpr, Act("op-k-add", "Aufnehmen", () => { if (string.IsNullOrWhiteSpace(tt.Text)) return; BizTickets.Add(new(tt.Text!, tc.Text ?? "", BusinessOps.Priorities[Math.Max(0, tpr.SelectedIndex)], DateTimeOffset.Now)); Show("biz-betrieb"); }) } } } };
        for (var n = 0; n < tickets.Count; n++)
        {
            var idx = n; var t = tickets[n]; if (t.State == "gelöst") continue;
            var ans = new TextBox { Name = $"op-k-ans-{n}", PlaceholderText = "Antwort", Width = 240 };
            ksec.Children.Add(Row(P($"{(BusinessOps.Overdue(t, now) ? "⚠ überfällig · " : "")}{t.Priority} · {t.Title} ({t.Customer})", Text, 11), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { ans,
                Act($"op-k-solve-{n}", "Gelöst", () => { var l = BizTickets.All(); l[idx] = l[idx] with { State = "gelöst", Answer = ans.Text ?? "" }; BizTickets.Save(l); Show("biz-betrieb"); }) } }));
        }
        ksec.Children.Add(P($"{tickets.Count(t => t.State != "gelöst")} offen · {tickets.Count(t => BusinessOps.Overdue(t, now))} überfällig · {tickets.Count(t => t.State == "gelöst")} gelöst", null, 11));
        ksec.Children.Add(Act("op-k-faq", "Häufige Fragen aus gelösten Anfragen erstellen", () => { var f = System.IO.Path.Combine(BusinessFolder, DevStudio.SafeName(i.Name) + "-FAQ.md"); File.WriteAllText(f, BusinessOps.Faq(BizTickets.All())); Launch(f); }));
        sp.Children.Add(Card(ksec));

        // BUS-024 + 025 Zahlen
        var stats = BizStats.All();
        var sc = T("op-s-csv", "CSV: Datum;Besucher;Anmeldungen;Verkäufe", 300);
        var zsec = new StackPanel { Spacing = 4, Children = { H("Auswertung", 14), new WrapPanel { Children = { sc, Act("op-s-import", "Übernehmen", () =>
        {
            try { var add = BusinessOps.ImportStats(File.ReadAllText(sc.Text?.Trim('"', ' ') ?? "")); var l = BizStats.All(); l.RemoveAll(x => add.Any(a => a.Date == x.Date)); l.AddRange(add); BizStats.Save(l.OrderBy(x => x.Date).ToList()); Say($"{add.Count} Tage übernommen.", "biz-betrieb"); }
            catch (IOException) { Say("Datei nicht gefunden.", "biz-betrieb"); }
        }) } } } };
        if (stats.Count > 0) { var tr = BusinessOps.Analyze(stats); zsec.Children.Add(new TextBlock { Name = "op-trend", Text = $"Letzte 7 Tage: {tr.SignupRate:P1} melden sich an, {tr.SaleRate:P1} kaufen (davor {tr.PrevSaleRate:P1}). {tr.Text}", Foreground = Text, TextWrapping = TextWrapping.Wrap }); }
        var pv = BusinessOps.Compare(i, Money.All(), new DateOnly(today.Year, 1, 1), today);
        zsec.Children.Add(new TextBlock { Name = "op-plan", Text = $"Plan: {pv.PlanProfitMonth:N0} € Gewinn/Monat · Wirklich (Seite „Geld“, dieses Jahr): {pv.ActualProfitMonth:N0} €/Monat. {pv.Text}", Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Card(zsec));
        return BusinessPage("Betrieb", sp);
    }
}
