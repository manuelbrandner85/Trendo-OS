using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 10 – SYS-12: gut lesbare Schrift für die ganze Oberfläche wählen. Atkinson Hyperlegible (Braille Institute, für Sehschwäche, SIL OFL 1.1),
/// unverändert im Setup (Assets/schriften mit Lizenztext). Festbreiten-Schriften in Code-Ansichten bleiben. OpenDyslexic wurde geprüft und verworfen: Unterlängen abgeschnitten, Layout zu breit.</summary>
public partial class MainWindow
{
    public static readonly (string Id, string Label, string Hint, string Family)[] ReadFonts =
    [
        ("", "Standard (Inter)", "Wie gestaltet.", ""),
        ("atkinson", "Atkinson Hyperlegible", "Für Sehschwäche: leicht verwechselbare Zeichen wie I, l und 1 sind klar verschieden.", "avares://TrendoOS/Assets/schriften#Atkinson Hyperlegible"),
    ];

    /// <summary>Gewählte Schrift anwenden (öffentlich für Tests). Ohne Wahl gilt wieder die Standardschrift.</summary>
    public void ApplyReadFont()
    {
        var family = ReadFonts.FirstOrDefault(f => f.Id == _settings.ReadFont).Family;
        var app = Application.Current;
        if (string.IsNullOrEmpty(family))
        {
            ClearValue(FontFamilyProperty);
            if (_readFontDefault is not null && app is not null) app.Resources["ContentControlThemeFontFamily"] = _readFontDefault;
            return;
        }
        var ff = new FontFamily(family);
        if (app is not null)
        {
            if (_readFontDefault is null && app.TryGetResource("ContentControlThemeFontFamily", null, out var cur)) _readFontDefault = cur;
            app.Resources["ContentControlThemeFontFamily"] = ff; // Eingabefelder und Knöpfe beziehen die Schrift aus dieser Ressource
        }
        FontFamily = ff;
    }
    object? _readFontDefault;

    /// <summary>Schrift wählen und sofort anwenden (öffentlich für Tests).</summary>
    public void SetReadFont(string id)
    {
        if (ReadFonts.All(f => f.Id != id)) return;
        _settings.ReadFont = id; SaveSettings(); ApplyReadFont();
    }

    Control ReadFontSection(string view)
    {
        var row = Line();
        foreach (var (id, label, _, family) in ReadFonts)
        {
            var on = _settings.ReadFont == id; var pick = id;
            var b = Act($"readfont-{(id.Length == 0 ? "standard" : id)}", (on ? "✓ " : "") + label, () => { SetReadFont(pick); Show(view); }, on);
            if (family.Length > 0) b.FontFamily = new FontFamily(family);
            row.Children.Add(b);
        }
        var hint = ReadFonts.First(f => f.Id == _settings.ReadFont).Hint;
        return Card(Section("Gut lesbare Schrift", "Für die ganze Oberfläche. " + hint, row));
    }
}
