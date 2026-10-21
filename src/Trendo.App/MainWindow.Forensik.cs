using System.Net.NetworkInformation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-12: Sicherheits-Werkzeuge – Datei-Prüfsumme, wer lauscht auf diesem PC, Beweis-Fälle (lokal, max. 120 h).</summary>
public partial class MainWindow
{
    private ForensicsLab? _lab;
    public ForensicsLab Lab => _lab ??= new ForensicsLab(_core.DataRoot, _core.Audit);
    private string _forMsg = "", _hashOut = ""; private string? _forCase;

    private void InitSpec12()
    {
        _showMap["forensik"] = ForensicsView; SysTabs.Add(("forensik", "Sicherheits-Werkzeuge"));
        StudioPages["SPEC-12"] = "forensik";
        ExtraPages.Add(("Sicherheits-Werkzeuge & Beweise", "forensik", ["forensik", "pruefsumme", "hash", "sha256", "beweis", "port", "hacker", "virus"]));
        var n = Lab.PurgeExpired(); if (n > 0) _forMsg = $"{n} abgelaufene(r) Fall/Fälle wurde(n) gelöscht (nach 120 Stunden).";
    }

    public static IReadOnlyList<(int Port, string Where)> ListeningPorts() =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(e => (e.Port, System.Net.IPAddress.IsLoopback(e.Address) ? "nur dieser PC" : "auch von außen erreichbar"))
            .Distinct().OrderBy(p => p.Port).ToList();

    private Control ForensicsView()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Datei prüfen", 14));
        sp.Children.Add(P("Vergleiche die Prüfsumme (SHA-256) eines Downloads mit der Angabe des Herstellers. Stimmt sie, ist die Datei unverändert."));
        var file = new TextBox { Name = "for-file", PlaceholderText = "Datei", Width = 320 }; var exp = new TextBox { Name = "for-expected", PlaceholderText = "Prüfsumme vom Hersteller (optional)", Width = 320 };
        var calc = new Button { Name = "for-hash", Content = "Prüfen", Background = Accent };
        calc.Click += async (_, _) =>
        {
            var f = file.Text?.Trim().Trim('"') ?? ""; var e = exp.Text ?? "";
            if (!File.Exists(f)) { _hashOut = "Diese Datei gibt es nicht."; Show("forensik"); return; }
            var h = await Task.Run(() => ForensicsLab.Sha256(f));
            _hashOut = e.Trim() == "" ? $"SHA-256: {h}" : string.Equals(h, e.Trim().Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ? $"✓ Stimmt überein. Die Datei ist unverändert.\n{h}" : $"⚠ Stimmt NICHT überein. Datei nicht öffnen und neu laden.\n{h}";
            Show("forensik");
        };
        sp.Children.Add(new WrapPanel { Children = { MSp(file), MSp(exp), MSp(calc) } });
        sp.Children.Add(new SelectableTextBlock { Name = "for-hash-out", Text = _hashOut, Foreground = Text, TextWrapping = TextWrapping.Wrap });

        sp.Children.Add(H("Wer wartet auf Verbindungen?", 14));
        var ports = ListeningPorts();
        var outside = ports.Where(p => p.Where != "nur dieser PC").ToList();
        sp.Children.Add(P($"{ports.Count} offene Anschlüsse (Ports), davon {outside.Count} auch von außen erreichbar. Unbekannte Programme, die von außen erreichbar sind, solltest du prüfen.", Text));
        sp.Children.Add(new TextBlock { Name = "for-ports", Text = string.Join(", ", outside.Take(30).Select(p => p.Port)), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });

        sp.Children.Add(H("Beweise sichern", 14));
        sp.Children.Add(P("Sichert Dateien unverändert mit Prüfsumme und lückenloser Kette – nur auf diesem PC. Jeder Fall löscht sich nach spätestens 120 Stunden; 24 Stunden vorher warnt Trendo."));
        var title = new TextBox { Name = "for-case-title", PlaceholderText = "Name des Falls, z. B. „Betrugs-Mail März“", Width = 320 };
        var mk = new Button { Name = "for-case-new", Content = "Fall anlegen" };
        mk.Click += (_, _) => { var c = Lab.Create(title.Text ?? ""); _forCase = c.Id; _forMsg = $"Fall angelegt. Er löscht sich am {c.Expires:dd.MM. HH:mm} Uhr."; Show("forensik"); };
        sp.Children.Add(new WrapPanel { Children = { MSp(title), MSp(mk) } });
        sp.Children.Add(new TextBlock { Name = "for-msg", Text = _forMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        var i = 0;
        foreach (var c in Lab.Cases())
        {
            var n = i++; var st = Lab.State(c);
            var add = new TextBox { Name = $"for-add-file-{n}", PlaceholderText = "Datei hinzufügen", Width = 260 };
            var addB = new Button { Name = $"for-add-{n}", Content = "Sichern", Padding = new Thickness(8, 2) };
            addB.Click += (_, _) => { try { var it = Lab.Add(c, add.Text?.Trim().Trim('"') ?? ""); _forMsg = $"Gesichert: {it.Name} ({it.Sha256[..12]}…)"; } catch (Exception ex) { _forMsg = "Hat nicht geklappt: " + ex.Message; } Show("forensik"); };
            var ver = new Button { Name = $"for-verify-{n}", Content = "Bericht", Padding = new Thickness(8, 2) };
            ver.Click += (_, _) => { var f = Lab.Report(c); var (ok, it) = Lab.Verify(c); _forMsg = $"{(ok && it.All(x => x.Ok) ? "✓ Alles unverändert." : "⚠ Etwas wurde verändert!")} Bericht: {f}"; Launch(f); Show("forensik"); };
            var del = new Button { Name = $"for-del-{n}", Content = "Jetzt löschen", Padding = new Thickness(8, 2) };
            del.Click += (_, _) => { Lab.Delete(c.Id); _forMsg = "Fall gelöscht."; Show("forensik"); };
            sp.Children.Add(WCard(new StackPanel { Spacing = 3, Children = {
                new TextBlock { Text = $"{c.Title} · {c.Items.Count} Datei(en)", Foreground = Text, FontWeight = FontWeight.SemiBold },
                P(st == CaseState.LaeuftBaldAb ? $"⚠ Löscht sich bald: {c.Expires:dd.MM. HH:mm} Uhr. Bericht jetzt sichern, falls nötig." : $"Löscht sich am {c.Expires:dd.MM. HH:mm} Uhr.", st == CaseState.LaeuftBaldAb ? Brush.Parse("#FACC15") : null, 11),
                new WrapPanel { Children = { MSp(add), MSp(addB), MSp(ver), MSp(del) } } } }));
        }
        return SysPage("Sicherheits-Werkzeuge", sp);
    }
}
