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
    /// <summary>Galerie offen? (Wunsch 03.11.2026: Vorlagen wie in Canva – mit Vorschaubild.)</summary>
    private bool _letterDesignAll;

    /// <summary>Bereich „Design“ im Brief-Formular: Vorlagen-Galerie mit echten Mini-Vorschauen, Farbpalette, zweite Farbe, Kopf-Layout, Schrift.</summary>
    Control LetterDesignBox(LetterDraft d)
    {
        const string V = "brief";
        LetterDesigns.Normalize(d);
        var sp = new StackPanel { Name = "letter-design", Spacing = 6 };
        void Done(string what) { _letterMsg = $"{what} – siehst du rechts in der Vorschau."; _core.Audit.Append(Principal.LocalOwner.Name, "letter.design", d.Design); Show(V); }
        Button B(string name, string label, bool on, Action go)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6), Background = on ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { go(); Done(label); };
            return b;
        }
        // Vorlagen-Galerie: jede Karte zeigt die erste Seite mit deinem Text im jeweiligen Design
        var gallery = new WrapPanel { Name = "letter-design-gallery" };
        var shown = _letterDesignAll ? LetterDesigns.Styles : LetterDesigns.Styles.Take(8).Concat(LetterDesigns.Styles.Where(x => x.Id == d.Design && LetterDesigns.Styles.ToList().IndexOf(x) >= 8)).ToArray();
        foreach (var st in shown)
        {
            var copy = d.Copy(); LetterDesigns.Apply(copy, st.Id); copy.PrintMarks = false;
            Control thumb;
            try
            {
                var page = LetterPages(copy, interactive: false)[0];
                foreach (var el in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(page).OfType<StyledElement>()) el.Name = null; // Namen nur in der großen Vorschau
                thumb = new Viewbox { Width = 104, Height = 147, Stretch = Stretch.Uniform, Child = page };
            }
            catch (Exception) { thumb = new Border { Width = 104, Height = 147, Background = Brushes.White }; }
            var on = d.Design == st.Id;
            var card = new Button
            {
                Name = "letter-design-style-" + st.Id, Padding = new Thickness(5), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(8),
                Background = on ? Accent : Brush.Parse("#14FFFFFF"), BorderBrush = on ? Accent : Edge, BorderThickness = new Thickness(on ? 2 : 1),
                Content = new StackPanel { Spacing = 4, Children = {
                    new Border { BoxShadow = BoxShadows.Parse("0 1 4 0 #55000000"), Child = thumb },
                    new TextBlock { Text = st.Label.Replace(" (DIN 5008)", ""), FontSize = 12, Foreground = on ? Brushes.White : Text, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 104 } } },
            };
            ToolTip.SetTip(card, st.Hint);
            var id = st.Id; card.Click += (_, _) => { var keep = d.Design == id ? (d.Accent, d.Accent2) : ((string, string)?)null; LetterDesigns.Apply(d, id); if (keep is { } k) (d.Accent, d.Accent2) = k; Done("Design: " + st.Label); };
            gallery.Children.Add(card);
        }
        sp.Children.Add(gallery);
        var more = new Button { Name = "letter-design-more", Content = _letterDesignAll ? "Weniger Vorlagen" : $"Alle {LetterDesigns.Styles.Length} Vorlagen zeigen …", FontSize = 12, Padding = new Thickness(10, 4) };
        more.Click += (_, _) => { _letterDesignAll = !_letterDesignAll; Show(V); };
        sp.Children.Add(more);
        if (!d.Designed) { sp.Children.Add(P("Klassisch ist der reine Brief nach DIN 5008. Wähle eine Vorlage für Farbe, Schrift und einen gestalteten Kopf – danach kannst du alles einzeln ändern. Oder sag es Claude.", null, 11)); return sp; }

        // Farbpalette (Akzent) und zweite Farbe
        WrapPanel Swatches(string prefix, string current, Action<string> set)
        {
            var w = new WrapPanel();
            foreach (var (hex, name) in LetterDesigns.Palette)
            {
                var c = new Button { Name = prefix + hex.TrimStart('#').ToLowerInvariant(), Width = 24, Height = 24, Margin = new Thickness(0, 0, 5, 5), CornerRadius = new CornerRadius(12), Background = Brush.Parse(hex),
                    BorderBrush = Brushes.White, BorderThickness = new Thickness(current.Equals(hex, StringComparison.OrdinalIgnoreCase) ? 3 : 0) };
                ToolTip.SetTip(c, name); var h = hex; c.Click += (_, _) => { set(h); Done("Farbe: " + name); };
                w.Children.Add(c);
            }
            var own = new TextBox { Name = prefix + "eigen", Width = 92, PlaceholderText = "#RRGGBB", Text = LetterDesigns.Palette.Any(x => x.Hex.Equals(current, StringComparison.OrdinalIgnoreCase)) ? "" : current, FontSize = 12 };
            own.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && System.Text.RegularExpressions.Regex.IsMatch((own.Text ?? "").Trim(), "^#[0-9A-Fa-f]{6}$")) { set(own.Text!.Trim()); Done("Eigene Farbe"); } };
            ToolTip.SetTip(own, "Eigene Farbe als Hex-Wert, dann Enter");
            w.Children.Add(own);
            return w;
        }
        sp.Children.Add(new TextBlock { Text = "Farbe", Foreground = Muted, FontSize = 12 });
        sp.Children.Add(Swatches("letter-design-color-", d.Accent, h => d.Accent = h));
        if (d.Header == "zweifarbig")
        {
            sp.Children.Add(new TextBlock { Text = "Zweite Farbe (Streifen)", Foreground = Muted, FontSize = 12 });
            sp.Children.Add(Swatches("letter-design-color2-", LetterDesigns.Second(d), h => d.Accent2 = h));
        }
        sp.Children.Add(new TextBlock { Text = "Kopf", Foreground = Muted, FontSize = 12 });
        var heads = new WrapPanel();
        foreach (var (id, label) in LetterDesigns.HeaderLabels)
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
        var d = Letter; var same = d.Design == style; var (a1, a2) = (d.Accent, d.Accent2);
        LetterDesigns.Apply(d, style);
        if (same) (d.Accent, d.Accent2) = (a1, a2); // gleiche Vorlage erneut: eigene Farbe bleibt
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
            _letterMsg = "Claude hat den Brief geändert – Text und Design siehst du in der Vorschau.";
            if (CurrentView == "brief") Show("brief");
        }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException) { _claudeMsg = "Claudes Brief ließ sich nicht übernehmen: " + Trendo.Core.FriendlyError.Say(e); }
    }
}
