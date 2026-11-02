using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Briefe und Bewerbungen mit Design (Wunsch 02.11.2026): im Brief-Programm Stil, Akzentfarbe, Schrift, Kopf-Gestaltung, Zeile unter dem Namen und Foto wählen –
/// und Claude arbeitet im offenen Brief selbst: Text UND Design (z. B. „Bewerbung als ZFA, zeitgemäß in Blau“) erscheinen sofort im Programm.</summary>
public partial class MainWindow
{
    public const string LetterStudio = "STUDIO-LETTER-01", LetterWorkFile = "Brief.json";
    static readonly JsonSerializerOptions LetterJsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    static readonly (string Hex, string Name)[] LetterColors = [("#1F5FA8", "Blau"), ("#0F2E5C", "Dunkelblau"), ("#0E7C86", "Petrol"), ("#2E7D5B", "Grün"), ("#8C2F39", "Bordeaux"), ("#B8862B", "Gold"), ("#4A4F57", "Anthrazit")];

    /// <summary>Bereich „Design“ im Brief-Formular.</summary>
    Control LetterDesignBox(LetterDraft d)
    {
        const string V = "brief";
        LetterDesigns.Normalize(d);
        var sp = new StackPanel { Name = "letter-design", Spacing = 6 };
        Button B(string name, string label, bool on, Action go)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6), Background = on ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { go(); _letterMsg = $"Design: {label} – siehst du rechts in der Vorschau."; _core.Audit.Append(Principal.LocalOwner.Name, "letter.design", d.Design); Show(V); };
            return b;
        }
        var styles = new WrapPanel();
        foreach (var s in LetterDesigns.Styles) { var b = B("letter-design-style-" + s.Id, s.Label, d.Design == s.Id, () => LetterDesigns.Apply(d, s.Id)); ToolTip.SetTip(b, s.Hint); styles.Children.Add(b); }
        sp.Children.Add(styles);
        if (!d.Designed) { sp.Children.Add(P("Klassisch ist der reine Brief nach DIN 5008. Wähle einen Stil für Farbe, Schrift und einen gestalteten Kopf – z. B. „Modern“ für eine zeitgemäße Bewerbung. Oder sag es Claude.", null, 11)); return sp; }
        var colors = new WrapPanel();
        foreach (var (hex, name) in LetterColors)
        {
            var c = new Button { Name = "letter-design-color-" + hex.TrimStart('#').ToLowerInvariant(), Width = 26, Height = 26, Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(13), Background = Brush.Parse(hex),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(d.Accent.Equals(hex, StringComparison.OrdinalIgnoreCase) ? 3 : 0) };
            ToolTip.SetTip(c, name); c.Click += (_, _) => { d.Accent = hex; _letterMsg = $"Farbe: {name} – siehst du rechts in der Vorschau."; Show(V); };
            colors.Children.Add(c);
        }
        sp.Children.Add(colors);
        var heads = new WrapPanel();
        foreach (var (id, label) in new[] { ("band", "Farbband oben"), ("linie", "Linie"), ("seitenleiste", "Seitenleiste"), ("keiner", "Ohne") })
            heads.Children.Add(B("letter-design-head-" + id, label, d.Header == id, () => d.Header = id));
        sp.Children.Add(heads);
        var fontBox = new ComboBox { Name = "letter-design-font", ItemsSource = LetterDesigns.Fonts, SelectedItem = d.Font, MinWidth = 160 };
        fontBox.SelectionChanged += (_, _) => { if (fontBox.SelectedItem is string f && f != d.Font) { d.Font = f; Show(V); } };
        var tagline = new TextBox { Name = "letter-design-tagline", Text = d.Tagline, PlaceholderText = "Zeile unter dem Namen, z. B. „Zahnmedizinische Fachangestellte“", MinWidth = 260 };
        tagline.LostFocus += (_, _) => { if ((tagline.Text ?? "") != d.Tagline) { d.Tagline = (tagline.Text ?? "").Trim(); Show(V); } };
        sp.Children.Add(new WrapPanel { Children = { fontBox, new Border { Width = 6 }, tagline } });
        var photo = new TextBox { Name = "letter-design-photo", Text = d.Photo, PlaceholderText = "Foto für die Bewerbung (PNG oder JPG)", MinWidth = 260 };
        var take = new Button { Name = "letter-design-photo-set", Content = "Übernehmen", FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(6, 0, 0, 0) };
        take.Click += (_, _) => { var f = (photo.Text ?? "").Trim().Trim('"'); if (f.Length == 0 || File.Exists(f)) { d.Photo = f; Show(V); } else Toast("Das Foto wurde nicht gefunden."); };
        sp.Children.Add(new WrapPanel { Children = { photo, take } });
        sp.Children.Add(P("Das Anschriftfeld bleibt nach DIN 5008 – der Brief passt weiter ins Fensterkuvert.", null, 11));
        return sp;
    }

    /// <summary>Für Tests: Brief-Design setzen wie im Formular.</summary>
    public void LetterSetDesign(string style, string? accent = null, string? tagline = null, string? photo = null)
    {
        var d = Letter; LetterDesigns.Apply(d, style);
        if (accent is not null) d.Accent = accent; if (tagline is not null) d.Tagline = tagline; if (photo is not null) d.Photo = photo;
        LetterDesigns.Normalize(d);
    }

    static string LetterToJson(LetterDraft d) => JsonSerializer.Serialize(d, LetterJsonOpts);

    /// <summary>Claude hat „Brief.json“ geändert: Text und Design ins Brief-Programm übernehmen (Foto relativ zum Projektordner).</summary>
    void ClaudeSyncLetter(string file)
    {
        try
        {
            var fresh = JsonSerializer.Deserialize<LetterDraft>(File.ReadAllText(file), LetterJsonOpts);
            if (fresh is null) return;
            var folder = Path.GetDirectoryName(file)!;
            if (fresh.Photo.Length > 0 && !Path.IsPathRooted(fresh.Photo)) fresh.Photo = Path.GetFullPath(Path.Combine(folder, fresh.Photo));
            if (fresh.Photo.Length > 0 && !File.Exists(fresh.Photo)) fresh.Photo = "";
            if (fresh.SignatureFile.Length > 0 && !File.Exists(fresh.SignatureFile)) fresh.SignatureFile = Letter.SignatureFile; // eigene Unterschrift bleibt
            LetterDesigns.Normalize(fresh);
            if (LetterToJson(fresh) == LetterToJson(Letter)) return;
            _letter = fresh; _letterGallery = false; _letterSaved = false;
            _letterMsg = "✳ Claude hat den Brief geändert – Text und Design siehst du in der Vorschau.";
            if (CurrentView == "brief") Show("brief");
        }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException) { _claudeMsg = "Claudes Brief ließ sich nicht übernehmen: " + e.Message; }
    }
}
