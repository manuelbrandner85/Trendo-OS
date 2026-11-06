using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 4, S4-17 (06.11.2026): Geräusch-Werkstatt im Audioeditor – Geräusch erzeugen (öffnet sich zum Bearbeiten),
/// in die Geräusch-Bibliothek legen (Name, Ereignis-Tags, Lizenz) und als Paket für Spiel oder Film ausgeben (nur mit gültiger Lizenz).</summary>
public partial class MainWindow
{
    string FoleyFolder => Path.Combine(WorkFolder(Programs[AUK].Folder), "Geräusche");
    public Foley.PackResult? AuFoleyLastPack { get; private set; }
    string _auFoleyKind = "regen"; double _auFoleySeconds = 10, _auFoleyIntensity = 0.6;

    /// <summary>Geräusch erzeugen, als WAV ablegen und im Audioeditor öffnen.</summary>
    public string AuFoleyMake(string kind, double seconds, double intensity)
    {
        var a = Foley.Make(kind, seconds, intensity, Environment.TickCount & 0xFFFF);
        var dir = Path.Combine(FoleyFolder, "Entwürfe"); Directory.CreateDirectory(dir);
        var label = Foley.Kinds.First(k => k.Id == kind).Label;
        var file = Path.Combine(dir, DevStudio.SafeName(label) + ".wav");
        for (var i = 2; File.Exists(file); i++) file = Path.Combine(dir, $"{DevStudio.SafeName(label)}-{i}.wav");
        AudioFile.WriteWav(a, file, 24);
        var d = AuOpen(file);
        d.Edit("Herkunft", x => { x["license"] = Foley.OwnLicense; x["foleyKind"] = kind; });
        _progMsg[AUK] = $"„{label}“ erzeugt ({seconds:0.#} s) – jetzt bearbeiten oder „In die Bibliothek“.";
        return file;
    }

    public Foley.Entry AuFoleyAdd(ProgramDocument d, string name, string tags, string license)
    {
        var a = AuRendered(d) ?? throw new InvalidOperationException("Erst einen Ton öffnen oder erzeugen.");
        var src = d.Text("foleyKind").Length > 0 ? "Trendo Geräusch-Werkstatt" : Path.GetFileName(d.Text("source"));
        var e = Foley.Add(FoleyFolder, a, name, tags.Split(',', ';'), license, src);
        _core.Audit.Append(Principal.LocalOwner.Name, "audio.foley.add", e.Name);
        _progMsg[AUK] = $"„{e.Name}“ liegt in der Geräusch-Bibliothek" + (e.Publishable ? "." : " – ohne gültige Lizenz, wird beim Paket ausgeschlossen.");
        return e;
    }

    public Foley.PackResult AuFoleyPack()
    {
        var target = Path.Combine(WorkFolder(Programs[AUK].Folder), $"Geräusche-Paket {DateTime.Now:yyyy-MM-dd HHmm}");
        var r = Foley.Pack(FoleyFolder, target); AuFoleyLastPack = r;
        _core.Audit.Append(Principal.LocalOwner.Name, "audio.foley.pack", $"{r.Included.Count} drin, {r.Excluded.Count} ausgeschlossen");
        _progMsg[AUK] = $"Paket: {r.Included.Count} Geräusche mit Lizenzen.csv und Ereignisse.json in {r.Folder}" + (r.Excluded.Count > 0 ? $" – {r.Excluded.Count} ausgeschlossen: " + string.Join("; ", r.Excluded.Select(x => $"{x.Entry.Name} ({x.Reason})")) : ".");
        return r;
    }

    Control AuFoleyPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "au-foley", Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(H("Geräusch-Werkstatt", 13));
        var kind = new ComboBox { Name = "au-foley-kind", ItemsSource = Foley.Kinds.Select(k => k.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(Foley.Kinds, k => k.Id == _auFoleyKind)), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var len = new NumericUpDown { Name = "au-foley-len", Value = (decimal)_auFoleySeconds, Minimum = 1, Maximum = 120, Increment = 1, FormatString = "0", FontSize = 12 };
        var inten = new Slider { Name = "au-foley-int", Minimum = 0, Maximum = 1, Value = _auFoleyIntensity };
        sp.Children.Add(kind);
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Sekunden ", Foreground = Muted, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, len } });
        sp.Children.Add(new TextBlock { Text = "Stärke", Foreground = Muted, FontSize = 11 }); sp.Children.Add(inten);
        var make = new Button { Name = "au-foley-make", Content = "Erzeugen und öffnen" };
        make.Click += (_, _) => ProgramRun(AUK, () =>
        {
            _auFoleyKind = Foley.Kinds[Math.Max(0, kind.SelectedIndex)].Id; _auFoleySeconds = (double)(len.Value ?? 10); _auFoleyIntensity = inten.Value;
            AuFoleyMake(_auFoleyKind, _auFoleySeconds, _auFoleyIntensity);
        });
        sp.Children.Add(make);
        if (AuRendered(d) is not null)
        {
            var ev = Foley.Kinds.FirstOrDefault(k => k.Id == d.Text("foleyKind")).Event ?? "";
            var name = new TextBox { Name = "au-foley-name", Text = d.Title, PlaceholderText = "Name" };
            var tags = new TextBox { Name = "au-foley-tags", Text = ev, PlaceholderText = "Ereignisse/Tags, z. B. Schritt.Kies, Loop" };
            var lic = new TextBox { Name = "au-foley-license", Text = d.Text("license"), PlaceholderText = "Lizenz (z. B. CC0 1.0) – leer/unbekannt = nicht veröffentlichen" };
            sp.Children.Add(name); sp.Children.Add(tags); sp.Children.Add(lic);
            var add = new Button { Name = "au-foley-add", Content = "In die Bibliothek" };
            add.Click += (_, _) => ProgramRun(AUK, () => AuFoleyAdd(d, (name.Text ?? "").Trim(), tags.Text ?? "", lic.Text ?? ""));
            var pack = new Button { Name = "au-foley-pack", Content = "Paket ausgeben" };
            pack.Click += (_, _) => ProgramRun(AUK, () => AuFoleyPack());
            sp.Children.Add(new WrapPanel { Children = { add, new Border { Width = 6 }, pack } });
        }
        var count = Foley.Find(FoleyFolder, null).Count;
        sp.Children.Add(P(count == 0 ? "Bibliothek noch leer." : $"Bibliothek: {count} Geräusche.", Muted, 11));
        return sp;
    }
}
