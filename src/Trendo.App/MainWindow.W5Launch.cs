using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>BUS-016/026–030: Start & Übergabe.</summary>
public partial class MainWindow
{
    public IdeaList<LegalInfo> BizLegal => new(_core.DataRoot, BizIdea.Id, "impressum");
    public string BizSite => System.IO.Path.Combine(BusinessFolder, DevStudio.SafeName(BizIdea.Name) + "-webseite");
    public string BizBackupDir => System.IO.Path.Combine(_core.DataRoot, "business-sicherung");
    private bool _bizConsumers = true;
    private string? _bizUrlState;

    private void InitW5Launch() => RegisterTab(BusinessTabs, "biz-start", "Start & Übergabe", "BUS-030", LaunchView, "webseite bauen", "impressum", "datenschutz", "dsgvo", "veroeffentlichen", "hosting", "domain", "startklar", "sicherung");

    private LegalInfo Legal => BizLegal.All().FirstOrDefault() ?? new("", "", "", "");

    public async Task BizCheckUrl(string url)
    {
        if (_core.Offline || !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https") { _bizUrlState = "Nur https-Adressen, und Trendo muss online sein."; Show("biz-start"); return; }
        try { using var r = await OssHttp.SendAsync(new HttpRequestMessage(HttpMethod.Head, u)); _bizUrlState = $"{u.Host} antwortet ({(int)r.StatusCode}) – {DateTime.Now:HH:mm}"; }
        catch (Exception ex) { _bizUrlState = $"{u.Host} nicht erreichbar: {ex.Message}"; }
        Show("biz-start");
    }

    private Control LaunchView()
    {
        var i = BizIdea; var lg = Legal;
        var sp = new StackPanel { Spacing = 8, Children = { P("Vom Plan zum Start: Webseite bauen, Pflichtangaben prüfen, Paket zum Hochladen erstellen, sichern. Trendo lädt nichts hoch."), IdeaHeader("biz-start") } };
        TextBox T(string n, string v, string ph, double w = 200) => new() { Name = n, Text = v, PlaceholderText = ph, Width = w };
        var o = T("ln-owner", lg.Owner, "Dein Name / Firma"); var s = T("ln-street", lg.Street, "Straße Hausnummer"); var c = T("ln-city", lg.City, "PLZ Ort", 160);
        var m = T("ln-mail", lg.Email, "E-Mail"); var ph = T("ln-phone", lg.Phone, "Telefon (optional)", 150); var vat = T("ln-vat", lg.VatId, "USt-IdNr. (optional)", 150);
        var small = new CheckBox { Name = "ln-small", IsChecked = lg.SmallBusiness, Content = new TextBlock { Text = "Kleinunternehmer", Foreground = Text } };
        var cons = new CheckBox { Name = "ln-consumers", IsChecked = _bizConsumers, Content = new TextBlock { Text = "Ich verkaufe an Privatkunden", Foreground = Text } };
        LegalInfo Read() { var l = new LegalInfo(o.Text ?? "", s.Text ?? "", c.Text ?? "", m.Text ?? "", ph.Text ?? "", vat.Text ?? "", small.IsChecked == true); BizLegal.Save([l]); _bizConsumers = cons.IsChecked == true; return l; }
        sp.Children.Add(Card(new StackPanel { Spacing = 5, Children = { H("1. Pflichtangaben", 14), new WrapPanel { Children = { o, s, c, m, ph, vat } }, new WrapPanel { Children = { small, cons } } } }));

        sp.Children.Add(Card(new StackPanel { Spacing = 5, Children = { H("2. Webseite", 14), P("5 Seiten: Start, Preise, Kontakt, Impressum, Datenschutz – in deinen Markenfarben, ohne Cookies.", null, 11),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                Act("ln-build", "Webseite bauen", () => { var dir = BusinessLaunch.BuildSite(BusinessFolder, i, Read(), BizTiers.All(), _brandColor); _core.Audit.Append(Principal.LocalOwner.Name, "business.site", i.Id); Say("Webseite gebaut. Prüfe sie unten.", "biz-start"); }, true),
                Act("ln-open", "Ansehen", () => Launch(System.IO.Path.Combine(BizSite, "index.html"))) } } } }));

        if (Directory.Exists(BizSite))
        {
            var chk = new StackPanel { Spacing = 2, Children = { H("3. Prüfen", 14) } };
            var n = 0;
            foreach (var x in BusinessLaunch.Compliance(BizSite, lg, _bizConsumers).Concat(BusinessLaunch.Quality(BizSite).Where(q => !q.Ok)))
                chk.Children.Add(new TextBlock { Name = $"ln-check-{n++}", Text = $"{(x.Ok ? "✓" : "✗")} {x.Area}: {x.Label}{(x.Ok ? "" : " – " + x.Fix)}", Foreground = x.Ok ? Text : Brush.Parse("#F97316"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            if (BusinessLaunch.Quality(BizSite).All(q => q.Ok)) chk.Children.Add(P("✓ Qualität: keine toten Links, alle Bilder beschrieben, nur https.", Text, 11.5));
            chk.Children.Add(P("Technische Prüfung, keine Rechtsberatung.", null, 10));
            sp.Children.Add(Card(chk));
            var dom = T("ln-domain", "", "Domain (optional), z. B. meine-firma.de", 240);
            sp.Children.Add(Card(new StackPanel { Spacing = 5, Children = { H("4. Veröffentlichen vorbereiten", 14), new WrapPanel { Children = { dom, Act("ln-package", "Paket erstellen", () =>
            {
                try { var z = BusinessLaunch.Package(BizSite, (dom.Text ?? "").Trim().ToLowerInvariant()); Say($"Paket {System.IO.Path.GetFileName(z)} erstellt. Anleitung: VEROEFFENTLICHEN.md", "biz-start"); }
                catch (ArgumentException ex) { Say(ex.Message, "biz-start"); }
            }) } } } }));
        }
        var url = T("ln-url", "", "Adresse deiner Seite, z. B. https://…", 260);
        var bsec = new StackPanel { Spacing = 5, Children = { H("5. Sichern und beobachten", 14), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            Act("ln-backup", "Jetzt sichern", () => { var z = BusinessLaunch.Backup(_core.DataRoot, i.Id, BizBackupDir); Say("Gesichert: " + System.IO.Path.GetFileName(z), "biz-start"); }),
            url, Act("ln-url-check", "Erreichbar?", () => _ = BizCheckUrl(url.Text ?? "")) } } } };
        var backups = Directory.Exists(BizBackupDir) ? Directory.GetFiles(BizBackupDir, $"business-{DevStudio.SafeName(i.Id)}-*.zip").OrderByDescending(x => x).ToList() : [];
        if (backups.Count > 0) bsec.Children.Add(Row(P($"{backups.Count} Sicherungen, neueste: {System.IO.Path.GetFileName(backups[0])}", null, 11), Act("ln-restore", "Neueste wiederherstellen", () => { var k = BusinessLaunch.Restore(backups[0], _core.DataRoot); CurrentIdea = null; Say($"{k} Dateien wiederhergestellt.", "biz-start"); })));
        if (_bizUrlState is not null) bsec.Children.Add(P(_bizUrlState, Text, 11));
        sp.Children.Add(Card(bsec));

        var (ready, checks) = BusinessLaunch.Handover(i, Experiments.All(), Directory.Exists(BizSite) ? BizSite : null, lg, _bizConsumers, BizBackupDir);
        var hsec = new StackPanel { Spacing = 2, Children = { new TextBlock { Name = "ln-ready", Text = ready ? "Startklar ✓" : $"Noch nicht startklar – {checks.Count(x => !x.Ok)} offene Punkte", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = ready ? Brush.Parse("#22C55E") : Brush.Parse("#FACC15") } } };
        foreach (var x in checks) hsec.Children.Add(P($"{(x.Ok ? "✓" : "○")} {x.Label}{(x.Ok ? "" : " – " + x.Fix)}", x.Ok ? Text : null, 11));
        sp.Children.Add(Card(hsec));
        return BusinessPage("Start & Übergabe", sp);
    }
}
