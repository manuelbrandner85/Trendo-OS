using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Licensing;

namespace Trendo.App;

/// <summary>Commercial Phase 2 (Entscheidungen E1–E9 vom 13.11.2026): Seite „Konto, Lizenz &amp; Vollversion“ in den Einstellungen.
/// Zeigt die Edition, nimmt einen vom Lizenzserver signierten Freischaltcode an und listet die Lizenzen mitgelieferter Teile.
/// Kein Kauf, keine Zahlungsdaten, kein Konto-Passwort in Trendo: Kauf und Rechnung laufen später über den Zahlungsanbieter.</summary>
public partial class MainWindow
{
    private LicenseStore? _licenses;
    private string _licMsg = "";
    public LicenseStore Licenses => _licenses ??= new LicenseStore(_core.DataRoot, new LicenseVerifier(LicenseKeys.Production), _core.Audit);

    private void InitLizenz()
    {
        _showMap["lizenz"] = LizenzView; PageModules["lizenz"] = "SYS-12"; // gehört wie „Trendo-Updates“ zu den System-Einstellungen
        ExtraPages.Add(("Konto, Lizenz & Vollversion", "lizenz", ["lizenz", "vollversion", "kaufen", "freischalten", "freischaltcode", "edition", "complete", "konto", "rechnung", "drittanbieter", "open source"]));
    }

    private Control LizenzView()
    {
        var st = Licenses.Status(DateTimeOffset.Now);
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(WCard(new StackPanel { Spacing = 4, Children = {
            new TextBlock { Name = "lic-headline", Text = st.Headline, Foreground = Text, FontSize = 20, FontWeight = FontWeight.Bold },
            P(st.Detail, Text, 13),
            P(st.Entitlement is { } e ? $"Lizenznehmer: {e.Licensee} · Lizenz {e.LicenseId}" : "Keine Lizenz auf diesem Gerät.", null, 12),
            P($"Geräte-Kennung: {Licenses.DeviceId} (zufällig erzeugt, enthält keine Daten über dich oder deinen PC)", null, 11) } }));

        var box = new TextBox { Name = "lic-code", PlaceholderText = "Freischaltcode einfügen (beginnt mit TRENDO1.)", AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MaxLength = LicenseVerifier.MaxTokenLength };
        var apply = new Button { Name = "lic-apply", Content = "Freischalten", Background = Accent };
        apply.Click += (_, _) => { var (_, msg) = Licenses.Install(box.Text ?? "", Principal.LocalOwner.Name); _licMsg = msg; Show("lizenz"); };
        var remove = new Button { Name = "lic-remove", Content = "Lizenz von diesem Gerät entfernen", IsVisible = st.Entitlement is not null };
        remove.Click += (_, _) => { Licenses.Remove(Principal.LocalOwner.Name); _licMsg = "Lizenz entfernt."; Show("lizenz"); };
        sp.Children.Add(WCard(new StackPanel { Spacing = 6, Children = {
            new TextBlock { Text = "Freischalten", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
            P(Licenses.Enforced ? "Den Freischaltcode bekommst du nach dem Kauf in deinem Trendo-Konto." : "Der Lizenzserver ist noch nicht in Betrieb. Freischaltcodes können erst danach geprüft werden.", null, 12),
            box, new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { apply, remove } },
            new TextBlock { Name = "lic-msg", Text = _licMsg, Foreground = Accent, FontSize = 12, IsVisible = _licMsg.Length > 0 } } }));

        // Vor dem Kauf: ehrlich zeigen, welche Studios auf diesem PC laufen (Auftrag „Commercial Desktop Edition“, Punkt 6)
        var fit = new StackPanel { Name = "lic-hardware", Spacing = 6 };
        if (_hw is null) { StartHardwareScan(); fit.Children.Add(P("Dein PC wird geprüft …", Text, 12)); }
        else fit.Children.Add(StudioFitList(Trendo.Core.Compat.PerformanceProfiles.Classify(_hw), "lic-studios"));
        var hwBtn = new Button { Name = "lic-to-hardware", Content = "Systemcheck ansehen" };
        hwBtn.Click += (_, _) => Show("hardware");
        fit.Children.Add(hwBtn);
        sp.Children.Add(WCard(new StackPanel { Spacing = 4, Children = {
            new TextBlock { Text = "Vor dem Kauf: Was läuft auf deinem PC?", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
            P("Nicht jedes Studio ist auf jedem PC gleich schnell. Die Liste zeigt, was hier läuft, was eingeschränkt läuft und was ein Zusatzprogramm oder stärkere Hardware braucht.", null, 12), fit } }));

        sp.Children.Add(WCard(new StackPanel { Spacing = 4, Children = {
            new TextBlock { Text = "Vollversion kaufen", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
            P("Der Kauf ist noch nicht möglich. Er läuft später über einen Zahlungsanbieter, der auch die Rechnung ausstellt. Trendo speichert keine Kartendaten.", null, 12),
            P("Claude ist nicht enthalten. Für Funktionen mit Claude braucht jede Person ein eigenes Claude-Konto.", null, 12) } }));

        var lic = new StackPanel { Name = "lic-third-party", Spacing = 2 };
        lic.Children.Add(new TextBlock { Text = "Lizenzen mitgelieferter Teile", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
        foreach (var c in DependencyCatalog.All.Where(c => c.Mode is DeliveryMode.Bundled or DeliveryMode.Included))
            lic.Children.Add(P($"{c.Name}: {c.License}", null, 12));
        lic.Children.Add(P("Die vollständige Liste mit allen Bibliotheken steht in THIRD_PARTY.md und THIRD_PARTY_NUGET.md.", null, 11));
        sp.Children.Add(WCard(lic));
        return SysPage("Konto, Lizenz & Vollversion", sp);
    }
}
