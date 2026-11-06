using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 4, S4-20 (06.11.2026): „Prüfen“ und „Freigeben“ im Audioeditor – Messprotokoll (Lautheit, True Peak, Übersteuerung,
/// Format, Phase) mit Herkunft/Lizenz und Einwilligung; bei Rot keine Freigabe.</summary>
public partial class MainWindow
{
    public AudioQa.Result? AuQaLast { get; private set; }

    IReadOnlyList<AudioQa.Source> AuQaSources(ProgramDocument d)
    {
        var lic = d.Text("license").Trim();
        if (lic.Length == 0) return [];
        var src = d.Text("source"); var name = src.Length > 0 ? Path.GetFileName(src) : AudioMulti.IsMulti(d.Data) ? "Mischung" : "Aufnahme";
        return [new AudioQa.Source(name, lic, d.Flag("voiceModel"), d.Flag("consent"))];
    }

    public AudioQa.Result AuQa(ProgramDocument d)
    {
        var a = AuRendered(d) ?? throw new InvalidOperationException("Erst eine Tondatei öffnen.");
        var t = AudioQa.TargetOf(d.Text("qaTarget", "streaming"));
        var r = AudioQa.Run(a, 24, t, AuQaSources(d)); // geprüft wird, was „Freigeben“ schreibt: WAV 24 Bit
        AuQaLast = r;
        _progMsg[AUK] = r.Blocked ? "Freigabe gesperrt: " + r.Reason : $"Geprüft für „{t.Label}“: {r.Checks.Count(c => c.Light == "gruen")} grün, {r.Checks.Count(c => c.Light == "gelb")} Hinweise – Freigabe möglich.";
        return r;
    }

    /// <summary>Prüfen und – nur ohne Rot – WAV 24 Bit plus Prüfbericht mit SHA-256 in einen Freigabe-Ordner schreiben.</summary>
    public string? AuRelease(ProgramDocument d)
    {
        var r = AuQa(d);
        if (r.Blocked) return null;
        var t = AudioQa.TargetOf(d.Text("qaTarget", "streaming"));
        var name = DevStudio.SafeName(d.Title); var root = WorkFolder(Programs[AUK].Folder);
        var folder = Path.Combine(root, "Freigabe – " + name);
        for (var i = 2; Directory.Exists(folder); i++) folder = Path.Combine(root, $"Freigabe – {name} {i}");
        Directory.CreateDirectory(folder);
        var wav = Path.Combine(folder, name + ".wav");
        AudioFile.WriteWav(AuRendered(d)!, wav, 24);
        var check = AudioQa.Run(wav, t, AuQaSources(d)); // die geschriebene Datei selbst noch einmal messen
        File.WriteAllText(Path.Combine(folder, "Prüfbericht.md"), AudioQa.Report(d.Title, t, check, [wav]));
        _core.Audit.Append(Principal.LocalOwner.Name, "audio.release", Path.GetFileName(folder));
        AuQaLast = check;
        _progMsg[AUK] = check.Blocked ? "Freigabe gesperrt: " + check.Reason : $"Freigegeben: {folder} (WAV 24 Bit und Prüfbericht).";
        return check.Blocked ? null : folder;
    }

    Control AuQaPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "au-qa", Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(H("Prüfen und freigeben", 13));
        var target = new ComboBox { Name = "au-qa-target", ItemsSource = AudioQa.Targets.Select(t => t.Label).ToList(), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            SelectedIndex = Math.Max(0, Array.FindIndex(AudioQa.Targets, t => t.Id == d.Text("qaTarget", "streaming"))) };
        var lic = new TextBox { Name = "au-license", Text = d.Text("license"), PlaceholderText = "Herkunft/Lizenz, z. B. eigene Aufnahme, CC BY 4.0 …" };
        var voice = new CheckBox { Name = "au-voice", Content = "enthält eine KI-Stimme", IsChecked = d.Flag("voiceModel") };
        var consent = new CheckBox { Name = "au-consent", Content = "Einwilligung der Person liegt vor", IsChecked = d.Flag("consent") };
        sp.Children.Add(target); sp.Children.Add(lic); sp.Children.Add(voice); sp.Children.Add(consent);
        void Save()
        {
            var t = AudioQa.Targets[Math.Max(0, target.SelectedIndex)].Id; var l = (lic.Text ?? "").Trim();
            if (t != d.Text("qaTarget", "streaming") || l != d.Text("license") || (voice.IsChecked == true) != d.Flag("voiceModel") || (consent.IsChecked == true) != d.Flag("consent"))
                d.Edit("Prüfangaben", x => { x["qaTarget"] = t; x["license"] = l; x["voiceModel"] = voice.IsChecked == true; x["consent"] = consent.IsChecked == true; });
        }
        var run = new Button { Name = "au-qa-run", Content = "Prüfen" };
        run.Click += (_, _) => ProgramRun(AUK, () => { Save(); AuQa(d); });
        var rel = new Button { Name = "au-release-run", Content = "Freigeben (WAV + Prüfbericht)", Background = Accent, Foreground = Brushes.White };
        rel.Click += (_, _) => ProgramRun(AUK, () => { Save(); AuRelease(d); });
        sp.Children.Add(new WrapPanel { Children = { run, new Border { Width = 6 }, rel } });
        if (AuQaLast is { } r)
            foreach (var c in r.Checks)
                sp.Children.Add(new TextBlock { Text = (c.Light switch { "rot" => "🔴 ", "gelb" => "🟡 ", _ => "🟢 " }) + c.Text, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        return sp;
    }
}
