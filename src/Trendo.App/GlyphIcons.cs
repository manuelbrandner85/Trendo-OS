using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>D3 (Vorgabe 13.11.2026 „nicht KI-generiert“): Knöpfe, die nur aus einem Zeichen bestehen (▶, ✕, ☰ …), bekommen automatisch ein
/// ruhiges Linien-Symbol statt des Sonderzeichens – überall, auch in künftigem Code. Die Pfade sind schlichte eigene Formen im 24er-Raster.
/// Der Knopf behält seinen Namen für Bildschirmleser (aus der Tabelle) und seinen Hinweis.</summary>
public static class GlyphIcons
{
    public sealed record Glyph(string Path, string Name);

    static Glyph G(string path, string name) => new(path, name);

    public static readonly IReadOnlyDictionary<string, Glyph> Map = new Dictionary<string, Glyph>
    {
        ["▶"] = G("M8,5V19L19,12Z", "Abspielen"), ["⏵"] = G("M8,5V19L19,12Z", "Abspielen"), ["⏯"] = G("M3,5V19L11,12ZM14,5H17V19H14ZM19,5H22V19H19Z", "Abspielen oder Pause"),
        ["⏸"] = G("M6,5H10V19H6ZM14,5H18V19H14Z", "Pause"), ["■"] = G("M6,6H18V18H6Z", "Stopp"), ["⏹"] = G("M6,6H18V18H6Z", "Stopp"),
        ["⏮"] = G("M6,6H8V18H6ZM9.5,12L18,18V6Z", "Zum Anfang"), ["⏭"] = G("M16,6H18V18H16ZM6,6V18L14.5,12Z", "Zum Ende"),
        ["⏪"] = G("M11,6V18L2.5,12ZM20.5,6V18L12,12Z", "Zurück"), ["⏩"] = G("M13,6V18L21.5,12ZM3.5,6V18L12,12Z", "Vor"),
        ["◀"] = G("M16,5V19L5,12Z", "Zurück"), ["⏴"] = G("M16,5V19L5,12Z", "Zurück"),
        ["✕"] = G("M6.4,5L5,6.4L10.6,12L5,17.6L6.4,19L12,13.4L17.6,19L19,17.6L13.4,12L19,6.4L17.6,5L12,10.6Z", "Schließen"),
        ["✗"] = G("M6.4,5L5,6.4L10.6,12L5,17.6L6.4,19L12,13.4L17.6,19L19,17.6L13.4,12L19,6.4L17.6,5L12,10.6Z", "Nein"),
        ["✖"] = G("M6.4,5L5,6.4L10.6,12L5,17.6L6.4,19L12,13.4L17.6,19L19,17.6L13.4,12L19,6.4L17.6,5L12,10.6Z", "Schließen"),
        ["✓"] = G("M9,16.2L4.8,12L3.4,13.4L9,19L21,7L19.6,5.6Z", "Ja"), ["✔"] = G("M9,16.2L4.8,12L3.4,13.4L9,19L21,7L19.6,5.6Z", "Ja"),
        ["☰"] = G("M3,6H21V8H3ZM3,11H21V13H3ZM3,16H21V18H3Z", "Menü"),
        ["▲"] = G("M7.4,15.4L12,10.8L16.6,15.4L18,14L12,8L6,14Z", "Nach oben"), ["▼"] = G("M7.4,8.6L12,13.2L16.6,8.6L18,10L12,16L6,10Z", "Nach unten"),
        ["↑"] = G("M11,20V7.8L5.4,13.4L4,12L12,4L20,12L18.6,13.4L13,7.8V20Z", "Nach oben"), ["↓"] = G("M13,4V16.2L18.6,10.6L20,12L12,20L4,12L5.4,10.6L11,16.2V4Z", "Nach unten"),
        ["←"] = G("M20,11V13H7.8L13.4,18.6L12,20L4,12L12,4L13.4,5.4L7.8,11Z", "Zurück"), ["→"] = G("M4,11V13H16.2L10.6,18.6L12,20L20,12L12,4L10.6,5.4L16.2,11Z", "Weiter"),
        ["‹"] = G("M15.4,7.4L14,6L8,12L14,18L15.4,16.6L10.8,12Z", "Zurück"), ["›"] = G("M8.6,16.6L10,18L16,12L10,6L8.6,7.4L13.2,12Z", "Weiter"),
        ["↻"] = G("M17.7,6.3A8,8 0 1,0 19.7,14H17.6A6,6 0 1,1 16.2,7.8L13,11H20V4Z", "Neu laden"), ["⟳"] = G("M17.7,6.3A8,8 0 1,0 19.7,14H17.6A6,6 0 1,1 16.2,7.8L13,11H20V4Z", "Neu laden"),
        ["↺"] = G("M6.3,6.3A8,8 0 1,1 4.3,14H6.4A6,6 0 1,0 7.8,7.8L11,11H4V4Z", "Rückgängig"),
        ["★"] = G("M12,3L14.6,9.2L21,9.6L16,13.8L17.6,20L12,16.6L6.4,20L8,13.8L3,9.6L9.4,9.2Z", "Favorit"),
        ["☆"] = G("M12,3L14.6,9.2L21,9.6L16,13.8L17.6,20L12,16.6L6.4,20L8,13.8L3,9.6L9.4,9.2ZM12,7.2L10.6,10.9L6.8,11.1L9.8,13.6L8.8,17.3L12,15.3L15.2,17.3L14.2,13.6L17.2,11.1L13.4,10.9Z", "Favorit"),
        ["🗑"] = G("M9,3V4H4V6H20V4H15V3ZM6,7V19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7ZM9,9H11V18H9ZM13,9H15V18H13Z", "Löschen"),
        ["✎"] = G("M3,17.2V21H6.8L17.8,10L14,6.2ZM20.7,7.1A1,1 0 0,0 20.7,5.7L18.3,3.3A1,1 0 0,0 16.9,3.3L15,5.2L18.8,9Z", "Bearbeiten"),
        ["✏"] = G("M3,17.2V21H6.8L17.8,10L14,6.2ZM20.7,7.1A1,1 0 0,0 20.7,5.7L18.3,3.3A1,1 0 0,0 16.9,3.3L15,5.2L18.8,9Z", "Bearbeiten"),
        ["⧉"] = G("M8,3H19A2,2 0 0,1 21,5V16H19V5H8ZM5,7H15A2,2 0 0,1 17,9V19A2,2 0 0,1 15,21H5A2,2 0 0,1 3,19V9A2,2 0 0,1 5,7ZM5,9V19H15V9Z", "Kopieren"),
        ["✂"] = G("M6,3A3,3 0 1,0 8.8,7.2L11,9.4L9.4,11L8.8,10.8A3,3 0 1,0 10.2,12.2L12,10.4L19,17.4H21V16L8.8,3.8A3,3 0 0,0 6,3ZM6,5A1,1 0 1,1 6,7A1,1 0 1,1 6,5ZM6,13A1,1 0 1,1 6,15A1,1 0 1,1 6,13ZM15,9L21,3H19L13.6,8.4Z", "Schneiden"),
        ["🔊"] = G("M3,9V15H7L12,20V4L7,9ZM14,7.2V9.3A3,3 0 0,1 14,14.7V16.8A5,5 0 0,0 14,7.2Z", "Lautstärke"), ["🔇"] = G("M3,9V15H7L12,20V4L7,9ZM15.6,9L14.2,10.4L15.8,12L14.2,13.6L15.6,15L17.2,13.4L18.8,15L20.2,13.6L18.6,12L20.2,10.4L18.8,9L17.2,10.6Z", "Ton aus"),
        ["⌫"] = G("M9,5H21V19H9L3,12ZM12.4,8L11,9.4L13.6,12L11,14.6L12.4,16L15,13.4L17.6,16L19,14.6L16.4,12L19,9.4L17.6,8L15,10.6Z", "Löschen"),
        ["⤢"] = G("M14,3H21V10H19V6.4L14.4,11L13,9.6L17.6,5H14ZM3,14H5V17.6L9.6,13L11,14.4L6.4,19H10V21H3Z", "Vergrößern"),
        ["⤡"] = G("M14,3H21V10H19V6.4L14.4,11L13,9.6L17.6,5H14ZM3,14H5V17.6L9.6,13L11,14.4L6.4,19H10V21H3Z", "Vergrößern"),
        ["⚙"] = G("M12,8A4,4 0 1,0 12,16A4,4 0 1,0 12,8ZM10.5,2H13.5L14,4.6L16.2,5.5L18.4,4L20.5,6.1L19,8.3L19.9,10.5L22,11V13L19.9,13.5L19,15.7L20.5,17.9L18.4,20L16.2,18.5L14,19.4L13.5,22H10.5L10,19.4L7.8,18.5L5.6,20L3.5,17.9L5,15.7L4.1,13.5L2,13V11L4.1,10.5L5,8.3L3.5,6.1L5.6,4L7.8,5.5L10,4.6Z", "Einstellungen"),
        ["🔍"] = G("M9.5,3A6.5,6.5 0 0,1 14.8,13.3L20.6,19.1L19.1,20.6L13.3,14.8A6.5,6.5 0 1,1 9.5,3ZM9.5,5A4.5,4.5 0 1,0 9.5,14A4.5,4.5 0 1,0 9.5,5Z", "Suchen"),
        ["📁"] = G("M3,5H10L12,7H21V19H3Z", "Ordner"), ["📂"] = G("M3,5H10L12,7H21V9H6L3,19ZM6.5,11H23L19.5,19H3.5Z", "Ordner öffnen"),
        ["💾"] = G("M5,3H17L21,7V21H3V3ZM6,5V9H15V5ZM12,13A3,3 0 1,0 12,19A3,3 0 1,0 12,13Z", "Speichern"),
        ["⬇"] = G("M11,4H13V15.2L17.6,10.6L19,12L12,19L5,12L6.4,10.6L11,15.2Z", "Herunterladen"), ["⬆"] = G("M13,20H11V8.8L6.4,13.4L5,12L12,5L19,12L17.6,13.4L13,8.8Z", "Hochladen"),
        ["⏏"] = G("M12,5L5,14H19ZM5,16H19V19H5Z", "Auswerfen"),
        ["●"] = G("M12,6A6,6 0 1,0 12,18A6,6 0 1,0 12,6Z", "Aufnahme"), ["⏺"] = G("M12,6A6,6 0 1,0 12,18A6,6 0 1,0 12,6Z", "Aufnahme"),
    };

    static bool _on;

    /// <summary>Einmal beim Start: jeder Knopf, dessen Inhalt nur ein bekanntes Zeichen ist, zeigt stattdessen das Linien-Symbol in seiner Schriftfarbe.</summary>
    public static void Enable()
    {
        if (_on) return;
        _on = true;
        ContentControl.ContentProperty.Changed.AddClassHandler<Button>((b, e) =>
        {
            if (e.NewValue is not string s || !Map.TryGetValue(s.Trim(), out var g)) return;
            var size = Math.Max(10, (double.IsNaN(b.FontSize) || b.FontSize <= 0 ? 13 : b.FontSize) * 1.05);
            var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(g.Path), Stretch = Stretch.Uniform, Width = size, Height = size, [!Shape.FillProperty] = b[!Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty] };
            if (string.IsNullOrEmpty(AutomationProperties.GetName(b))) AutomationProperties.SetName(b, g.Name);
            if (ToolTip.GetTip(b) is null) ToolTip.SetTip(b, g.Name);
            b.Tag ??= s.Trim(); // ursprüngliches Zeichen für Tests und Auswertung
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (Equals(b.Content, e.NewValue)) b.Content = path; });
        });
    }
}
