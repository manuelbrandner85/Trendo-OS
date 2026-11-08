using Avalonia;
using Avalonia.Controls;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 7 – CORE-06: Spiel-Effekte (Münze, Laser, Explosion, Power-up, Treffer, Sprung, Menü-Klick) nach dem gemeinfreien
/// sfxr-Verfahren in der Geräusch-Werkstatt. Jede Variante öffnet sich im Audioeditor und trägt Lizenz und Ereignis-Tag für die Bibliothek.</summary>
public partial class MainWindow
{
    string _auSfx = "muenze"; int _auSfxSeed = 1;

    /// <summary>Spiel-Effekt würfeln, als WAV in „Entwürfe“ ablegen und öffnen (öffentlich für Tests). Rückgabe: Dateipfad.</summary>
    public string AuSfxMake(string preset, int seed)
    {
        var a = Sfxr.Render(Sfxr.Preset(preset, seed));
        var dir = Path.Combine(FoleyFolder, "Entwürfe"); Directory.CreateDirectory(dir);
        var label = Sfxr.Presets.First(k => k.Id == preset).Label;
        var baseName = DevStudio.SafeName(label) + "-" + seed;
        var file = Path.Combine(dir, baseName + ".wav");
        for (var i = 2; File.Exists(file); i++) file = Path.Combine(dir, $"{baseName}-{i}.wav");
        AudioFile.WriteWav(a, file, 24);
        var d = AuOpen(file);
        d.Edit("Herkunft", x => { x["license"] = Sfxr.License; x["sfxPreset"] = preset; x["sfxSeed"] = seed.ToString(System.Globalization.CultureInfo.InvariantCulture); });
        _core.Audit.Append(Principal.LocalOwner.Name, "audio.sfx.make", $"{preset}#{seed}");
        _progMsg[AUK] = $"„{label}“ Variante {seed} erzeugt ({a.Seconds:0.00} s) – „Neue Variante“ würfelt weiter, „In die Bibliothek“ legt ihn ab.";
        return file;
    }

    Control AuSfxPanel()
    {
        var sp = new StackPanel { Name = "au-sfx", Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(new TextBlock { Text = "Spiel-Effekte (8-Bit)", FontWeight = Avalonia.Media.FontWeight.SemiBold, Foreground = Text, FontSize = 12 });
        var kind = new ComboBox { Name = "au-sfx-kind", ItemsSource = Sfxr.Presets.Select(k => k.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(Sfxr.Presets, k => k.Id == _auSfx)),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var again = new Button { Name = "au-sfx-make", Content = "Neue Variante" };
        again.Click += (_, _) => ProgramRun(AUK, () => { var id = Sfxr.Presets[Math.Max(0, kind.SelectedIndex)].Id; _auSfxSeed = id == _auSfx ? _auSfxSeed + 1 : 1; _auSfx = id; AuSfxMake(id, _auSfxSeed); });
        sp.Children.Add(kind); sp.Children.Add(again);
        sp.Children.Add(P("Selbst erzeugt, ohne fremde Samples – frei für eigene Spiele.", Muted, 10.5));
        return sp;
    }
}
