using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Stufe 2, Teil 1 im Studio „Webseite bauen“: zentrale Gestaltungswerte für das ganze Projekt mit hellem und dunklem Satz (W6), Kontrast schon beim Wählen,
/// und Geräte-Ansichten mit frei wählbarer Breite (W1, Z4). Alles läuft über dieselben geprüften Aktionen wie bei der KI.</summary>
public partial class MainWindow
{
    static readonly string[] WebColorKeys = ["accent", "ink", "paper", "accentDunkel", "inkDunkel", "paperDunkel"];

    /// <summary>Werte aus dem Gestaltungsfeld als Gestaltungswerte (ungültige Farben bleiben draußen und werden gemeldet).</summary>
    public static (JsonObject Tokens, List<string> Invalid) WebDesignTokensFrom(IReadOnlyDictionary<string, string> values)
    {
        var t = new JsonObject(); var invalid = new List<string>();
        foreach (var (k, v) in values)
        {
            var s = v.Trim();
            if (WebColorKeys.Contains(k)) { if (System.Text.RegularExpressions.Regex.IsMatch(s, "^#[0-9a-fA-F]{6}$")) t[k] = s; else invalid.Add(WebDesignTokens.Names[k]); }
            else if (k == "dunkel") t[k] = s == "true";
            else if (k == "font") t[k] = s;
            else if (k is "radius" or "space") { if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) t[k] = n; else invalid.Add(k == "radius" ? "Rundung" : "Abstand"); }
        }
        return (t, invalid);
    }

    /// <summary>Kontrollzeile beim Wählen: was ungültig oder zu kontrastarm ist, sonst ein „passt“.</summary>
    public static string WebDesignTokensCheckText(IReadOnlyDictionary<string, string> values)
    {
        var (t, invalid) = WebDesignTokensFrom(values);
        var lines = invalid.Select(n => $"✗ {n}: bitte als #RRGGBB angeben.").Concat(WebDesignTokens.Check(t).Select(f => "✗ " + f.Text)).ToList();
        return lines.Count == 0 ? "✓ Kontrast passt (mindestens 4,5 : 1)." : string.Join("\n", lines);
    }

    // Meldung direkt unter „Gestaltung übernehmen“, nicht erst unten im Bereich (Live-Test 13.11.2026)
    string _wdTokNote = "";

    /// <summary>Gestaltungswerte übernehmen: als Aktionen auf der offenen Fassung, danach gelten sie für das ganze Projekt.</summary>
    public void WebDesignTokensApply(IReadOnlyDictionary<string, string> values)
    {
        var (t, invalid) = WebDesignTokensFrom(values);
        if (invalid.Count > 0) { _wdNote = _wdTokNote = "Nicht übernommen: " + string.Join(", ", invalid) + " ungültig."; Show("dev"); return; }
        var weak = WebDesignTokens.Check(t).Select(f => f.Text).ToList();
        if (weak.Count > 0) { _wdNote = _wdTokNote = "Nicht übernommen, der Kontrast reicht nicht: " + string.Join(" ", weak); Show("dev"); return; }
        _wdTokNote = "";
        var plan = new JsonArray(t.Select(kv => (JsonNode)new JsonObject { ["aktion"] = "gestaltungswert_setzen", ["name"] = kv.Key, ["wert"] = kv.Value?.DeepClone() }).ToArray());
        WebDesignHumanAction(new JsonObject { ["aktionen"] = plan }.ToJsonString());
    }

    public void WebDesignViewSet(string name, string width)
    {
        if (!int.TryParse(width.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)) { _wdNote = "Die Breite bitte als ganze Zahl in Pixeln."; Show("dev"); return; }
        WebDesignHumanAction(new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "ansicht_setzen", ["name"] = name.Trim(), ["breite"] = w }) }.ToJsonString());
    }

    public void WebDesignViewRemove(string name) =>
        WebDesignHumanAction(new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "ansicht_entfernen", ["name"] = name }) }.ToJsonString());

    Control WebDesignTokensPane()
    {
        var current = JsonNode.Parse(WebDesignCurrent())?["root"]?["props"] as JsonObject ?? new JsonObject();
        string Val(string k, string d) => current[k] is JsonValue v ? (v.TryGetValue(out string? s) ? s : v.ToJsonString()) : d;
        var sp = new StackPanel { Spacing = 6, Name = "web-gestaltung" };
        sp.Children.Add(new TextBlock { Text = "🎨 Gestaltung für das ganze Projekt", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Farben, Schrift, Rundung und Abstand gelten für die Seite, den Export und neue Varianten. Eine bestehende Variante behält ihre Gestaltung, bis du sie übernimmst. Der dunkle Satz greift, wenn das Gerät auf dunkel steht.", Muted, 12));
        var boxes = new Dictionary<string, TextBox>();
        var check = new TextBlock { Name = "web-tok-check", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var dark = new CheckBox { Name = "web-tok-dunkel", Content = "Dunklen Satz verwenden", Foreground = Text, IsChecked = Val("dunkel", "false") == "true" };
        var font = new ComboBox { Name = "web-tok-font", ItemsSource = WebDesign.Fonts, SelectedItem = Val("font", "klar"), Width = 120 };
        Dictionary<string, string> Values()
        {
            var v = boxes.ToDictionary(kv => kv.Key, kv => kv.Value.Text ?? "");
            v["dunkel"] = dark.IsChecked == true ? "true" : "false"; v["font"] = font.SelectedItem?.ToString() ?? "klar";
            return v;
        }
        void Recheck()
        {
            var text = WebDesignTokensCheckText(Values());
            check.Text = text; check.Foreground = text.StartsWith('✓') ? Muted : Brush.Parse("#F5A524");
        }
        var grid = new WrapPanel();
        foreach (var k in WebColorKeys)
        {
            var box = new TextBox { Name = "web-tok-" + k, Text = Val(k, WebDesignTokens.Defaults[k]), Width = 110 };
            box.TextChanged += (_, _) => Recheck(); // Kontrast beim Wählen, nicht erst beim Speichern
            boxes[k] = box;
            grid.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 10, 6), Children = { new TextBlock { Text = WebDesignTokens.Names[k], FontSize = 11.5, Foreground = Muted }, box } });
        }
        foreach (var (k, label, d) in new[] { ("radius", "Rundung px", "12"), ("space", "Abstand px", "24") })
        {
            var box = new TextBox { Name = "web-tok-" + k, Text = Val(k, d), Width = 70 }; boxes[k] = box;
            grid.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 10, 6), Children = { new TextBlock { Text = label, FontSize = 11.5, Foreground = Muted }, box } });
        }
        grid.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 10, 6), Children = { new TextBlock { Text = "Schrift", FontSize = 11.5, Foreground = Muted }, font } });
        dark.IsCheckedChanged += (_, _) => Recheck();
        sp.Children.Add(grid);
        sp.Children.Add(new WrapPanel { Children = { dark, new Border { Width = 12 }, Act("web-tok-apply", "Gestaltung übernehmen", () => WebDesignTokensApply(Values()), true) } });
        Recheck();
        sp.Children.Add(check);
        if (_wdTokNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-tok-note", Text = _wdTokNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#F5A524") });

        // W1, Z4: Ansichten
        sp.Children.Add(new TextBlock { Text = "📱 Geräte-Ansichten", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 8, 0, 0) });
        sp.Children.Add(P($"{WebDesignViews.Base} ist die Grundlage. Eine Ansicht gilt bis zu ihrer Breite; Anpassungen dort wirken auch in allen schmaleren Ansichten, nie nach oben. Anpassen im Editor unter „Anpassungen je Ansicht“.", Muted, 12));
        var views = new StackPanel { Name = "web-ansichten", Spacing = 3 };
        var vi = 0;
        foreach (var v in WebDesignViews.Of(current))
        {
            var name = v.Name;
            views.Children.Add(Row(P($"{v.Name} · bis {v.Breite} px", Text, 12.5), Act($"web-ansicht-weg-{vi++}", "Entfernen", () => WebDesignViewRemove(name))));
        }
        sp.Children.Add(views);
        var vName = new TextBox { Name = "web-ansicht-name", PlaceholderText = "Name, z. B. Großes Handy", Width = 200 };
        var vWidth = new TextBox { Name = "web-ansicht-breite", PlaceholderText = "Breite px", Width = 90 };
        sp.Children.Add(new WrapPanel { Children = { vName, new Border { Width = 6 }, vWidth, new Border { Width = 8 }, Act("web-ansicht-setzen", "Ansicht anlegen oder Breite ändern", () => WebDesignViewSet(vName.Text ?? "", vWidth.Text ?? "")) } });
        return new Border { Name = "web-gestaltung-card", Padding = new Thickness(14), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = sp };
    }
}
