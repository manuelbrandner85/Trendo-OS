using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Trendo.Core;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 8 – X-07: Reiter „Symbole“ – freie Symbolsätze von Iconify (Lizenz je Satz) laden, durchsuchen, als SVG speichern.
/// Nur freie Sätze werden angeboten; bei CC BY schreibt Trendo die Namensnennung in „Lizenzen.txt“. Daten werden gelesen, nichts ausgeführt.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Abruf nachbilden (Adresse → Text).</summary>
    public static Func<string, Task<string>>? IconFetchOverride { get; set; }
    string IconCache => Path.Combine(_core.DataRoot, "bibliothek", "symbole");
    List<IconSets.Set>? _iconSets; IconSets.Icons? _icons; string _iconPrefix = "mdi", _iconQuery = "";
    public string IconMessage { get; private set; } = "";
    public List<string> IconHits { get; private set; } = [];

    async Task<string> IconGet(string url, string cacheFile)
    {
        if (File.Exists(cacheFile)) return await File.ReadAllTextAsync(cacheFile);
        var text = IconFetchOverride is { } f ? await f(url) : await new HttpClient { Timeout = TimeSpan.FromMinutes(2) }.GetStringAsync(url);
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!); await File.WriteAllTextAsync(cacheFile, text);
        return text;
    }

    /// <summary>Satzliste laden (öffentlich für Tests).</summary>
    public async Task IconLoadSets()
    {
        try { _iconSets = IconSets.ParseCollections(await IconGet(IconSets.CollectionsUrl, Path.Combine(IconCache, "collections.json"))); IconMessage = ""; }
        catch (Exception e) when (e is HttpRequestException or IOException or FormatException or System.Text.Json.JsonException or TaskCanceledException) { IconMessage = "⚠ Symbolsätze nicht ladbar: " + Trendo.Core.FriendlyError.Say(e); }
        Show(MB);
    }

    /// <summary>Satz laden und suchen (öffentlich für Tests).</summary>
    public async Task IconSearch(string prefix, string query)
    {
        _iconPrefix = prefix; _iconQuery = query;
        var set = _iconSets?.FirstOrDefault(s => s.Prefix == prefix);
        if (set is null || set.Use == IconSets.Use.Ausgeschlossen) { IconMessage = "⚠ Diesen Symbolsatz bietet Trendo nicht an (Lizenz nicht frei genug)."; Show(MB); return; }
        try
        {
            if (_icons?.Prefix != prefix) _icons = new IconSets.Icons(await IconGet(IconSets.SetUrl(prefix), Path.Combine(IconCache, prefix + ".json")));
            IconHits = _icons.Search(query, 48);
            IconMessage = IconHits.Count == 0 ? "Nichts gefunden – englische Begriffe probieren (z. B. home, arrow, heart)." : $"{IconHits.Count} Symbole aus „{set.Name}“ ({set.LicenseTitle}).";
        }
        catch (Exception e) when (e is HttpRequestException or IOException or FormatException or System.Text.Json.JsonException or TaskCanceledException) { IconMessage = "⚠ Symbolsatz nicht ladbar: " + Trendo.Core.FriendlyError.Say(e); }
        Show(MB);
    }

    /// <summary>Symbol als SVG in „Assets/Symbole“ speichern (öffentlich für Tests).</summary>
    public string? IconSave(string name)
    {
        var set = _iconSets?.FirstOrDefault(s => s.Prefix == _iconPrefix);
        if (set is null || _icons is null) return null;
        try
        {
            var f = IconSets.Save(Path.Combine(AssetFolder, "Symbole"), set, _icons, name);
            _core.Audit.Append(Principal.LocalOwner.Name, "assets.icon", $"{set.Prefix}:{name} ({set.Spdx})");
            IconMessage = $"✓ Gespeichert: {Path.GetFileName(f)}" + (set.Use == IconSets.Use.MitNennung ? " – Namensnennung nötig, steht in „Lizenzen.txt“." : $" – {set.LicenseTitle}, frei nutzbar.");
            Show(MB); return f;
        }
        catch (InvalidOperationException e) { IconMessage = "⚠ " + e.Message; Show(MB); return null; }
    }

    static readonly Regex PathRx = new("<path\\b([^>]*)>", RegexOptions.Compiled);
    static string? Attr(string attrs, string name) => Regex.Match(attrs, "\\b" + name + "=\"([^\"]*)\"") is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Vorschau: Pfade des Symbols mit Avalonia zeichnen (Füllung/Linie wie im SVG, „currentColor“ = Textfarbe). Sonderformen (Kreis, Rechteck) zeigt die gespeicherte SVG-Datei.</summary>
    Control IconPreview(string svg, int size = 30)
    {
        var vb = Regex.Match(svg, "viewBox=\"([-0-9.]+) ([-0-9.]+) ([-0-9.]+) ([-0-9.]+)\"");
        double W(int i) => double.Parse(vb.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture);
        var canvas = new Canvas { Width = vb.Success ? W(3) : 24, Height = vb.Success ? W(4) : 24 };
        var g = Regex.Match(svg, "<g\\b([^>]*)>"); var group = g.Success ? g.Groups[1].Value : "";
        foreach (Match m in PathRx.Matches(svg))
        {
            var a = m.Groups[1].Value; var d = Attr(a, "d"); if (d is null) continue;
            IBrush? Brush(string? v) => v is null or "none" ? null : v == "currentColor" ? Text : Avalonia.Media.Brush.Parse(v.StartsWith("url(") ? "#888888" : v);
            var fill = Attr(a, "fill") ?? Attr(group, "fill"); var stroke = Attr(a, "stroke") ?? Attr(group, "stroke");
            try
            {
                var shape = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(d), Fill = fill is null && stroke is null ? Text : Brush(fill), Stroke = Brush(stroke),
                    StrokeThickness = double.TryParse(Attr(a, "stroke-width") ?? Attr(group, "stroke-width"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sw) ? sw : 1,
                    StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round };
                if (vb.Success) { Canvas.SetLeft(shape, -W(1)); Canvas.SetTop(shape, -W(2)); }
                canvas.Children.Add(shape);
            }
            catch (FormatException) { }
        }
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }

    Control IconsView()
    {
        var sp = new StackPanel { Name = "mb-icons", Spacing = 8 };
        sp.Children.Add(P("Über 200 Symbolsätze von Iconify – Trendo zeigt nur freie (MIT, Apache, OFL, CC0; CC BY mit Namensnennung). Gespeichert wird als SVG in „Assets/Symbole“.", null, 12));
        if (_iconSets is null) { sp.Children.Add(Act("mb-icons-load", "Symbolsätze laden", () => _ = IconLoadSets(), true)); }
        else
        {
            var free = _iconSets.Where(s => s.Use != IconSets.Use.Ausgeschlossen).ToList();
            var pick = new ComboBox { Name = "mb-icons-set", ItemsSource = free.Select(s => $"{s.Name} · {s.Total:N0} · {s.LicenseTitle}").ToList(), SelectedIndex = Math.Max(0, free.FindIndex(s => s.Prefix == _iconPrefix)), Width = 360 };
            var q = new TextBox { Name = "mb-icons-query", Text = _iconQuery, PlaceholderText = "z. B. home, arrow, heart", Width = 200 };
            sp.Children.Add(new WrapPanel { Children = { pick, new Border { Width = 6 }, q, new Border { Width = 6 },
                Act("mb-icons-search", "Suchen", () => _ = IconSearch(free[Math.Max(0, pick.SelectedIndex)].Prefix, q.Text ?? ""), true) } });
            sp.Children.Add(P($"{free.Count} freie Sätze, {_iconSets.Count - free.Count} wegen ihrer Lizenz ausgeblendet.", Muted, 11));
        }
        if (IconMessage.Length > 0) sp.Children.Add(new TextBlock { Name = "mb-icons-msg", Text = IconMessage, TextWrapping = TextWrapping.Wrap, Foreground = IconMessage.StartsWith('⚠') ? Avalonia.Media.Brush.Parse("#F97316") : Text, FontSize = 12 });
        if (_icons is not null && IconHits.Count > 0)
        {
            var grid = new WrapPanel { Name = "mb-icons-grid" };
            var i = 0;
            foreach (var n in IconHits)
            {
                var name = n; var svg = _icons.Svg(n, 24); if (svg is null) continue;
                var b = new Button { Name = $"mb-icon-{i++}", Width = 88, Height = 74, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(4), Background = TileBg,
                    Content = new StackPanel { Spacing = 4, Children = { IconPreview(svg), new TextBlock { Text = n, FontSize = 9.5, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center } } } };
                ToolTip.SetTip(b, $"{n} – Klick speichert als SVG");
                b.Click += (_, _) => IconSave(name);
                grid.Children.Add(b);
            }
            sp.Children.Add(grid);
        }
        return sp;
    }
}
