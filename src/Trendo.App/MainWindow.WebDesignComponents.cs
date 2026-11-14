using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>W5 im Studio „Webseite bauen“: Auswahl zur Komponente machen, Komponenten einfügen, als eigene Vorlage speichern und Vorlagen in jedes Projekt holen.
/// Felder einer Verwendung überschreibt man im Editor; alles läuft über dieselben geprüften Aktionen wie bei der KI.</summary>
public partial class MainWindow
{
    string _wdCompName = "";
    /// <summary>Für Tests: Ordner der eigenen Vorlagen.</summary>
    public static string? WebDesignTemplatesOverride { get; set; }
    public string WebDesignTemplatesDir => WebDesignTemplatesOverride ?? Path.Combine(_core.DataRoot, "webseiten", WebDesignComponents.TemplatesDir);

    static string Plan(params JsonObject[] actions) => new JsonObject { ["aktionen"] = new JsonArray(actions.Cast<JsonNode>().ToArray()) }.ToJsonString();

    public void WebDesignComponentFromSelection(string name)
    {
        if (_wdSelection.Count == 0) { _wdNote = "Wähle zuerst im Editor unter „Auswählen“ die Bausteine aus, die zur Komponente werden sollen."; Show("dev"); return; }
        WebDesignHumanAction(Plan(new JsonObject { ["aktion"] = "komponente_erstellen", ["name"] = name.Trim(), ["ids"] = new JsonArray(_wdSelection.Select(s => (JsonNode)s).ToArray()) }));
        if (!_wdNote.StartsWith("Gespeichert", StringComparison.Ordinal)) return;
        _wdCompName = ""; WebDesignClearSelection();
    }

    public void WebDesignComponentInsert(string id) => WebDesignHumanAction(Plan(new JsonObject { ["aktion"] = "komponente_einfuegen", ["komponente"] = id }));

    public void WebDesignComponentSaveTemplate(string id)
    {
        var c = WebDesignComponents.Read(JsonNode.Parse(WebDesignCurrent())?["root"]?["props"]?["komponenten"]).FirstOrDefault(x => x.Id == id);
        if (c is null) { _wdNote = "Diese Komponente gibt es nicht mehr."; Show("dev"); return; }
        var (_, replaced) = WebDesignComponents.SaveTemplate(WebDesignTemplatesDir, c);
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.vorlage", c.Name);
        _wdNote = replaced ? $"Die Vorlage „{c.Name}“ ist durch diese Fassung ersetzt." : $"„{c.Name}“ ist als Vorlage gespeichert und steht in jedem Projekt bereit."; Show("dev");
    }

    /// <summary>Vorlage ins Projekt holen: als neue Komponente samt einer Verwendung am Seitenende.</summary>
    public void WebDesignTemplateInsert(string name)
    {
        var t = WebDesignComponents.Templates(WebDesignTemplatesDir).FirstOrDefault(x => x.Name == name);
        if (t.Blocks is null) { _wdNote = "Diese Vorlage gibt es nicht mehr."; Show("dev"); return; }
        var existing = WebDesignComponents.Read(JsonNode.Parse(WebDesignCurrent())?["root"]?["props"]?["komponenten"]);
        var baseName = name[..Math.Min(name.Length, 55)].TrimEnd();
        var finalName = baseName; for (var n = 2; existing.Any(c => c.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase)); n++) finalName = $"{baseName} {n}";
        var kid = "V-" + Guid.NewGuid().ToString("N")[..8];
        WebDesignHumanAction(Plan(
            new JsonObject { ["aktion"] = "komponente_definieren", ["name"] = finalName, ["komponente"] = kid, ["bausteine"] = WebDesignComponents.TemplateBlocks(t.Blocks) },
            new JsonObject { ["aktion"] = "komponente_einfuegen", ["komponente"] = kid }));
    }

    Control WebDesignComponentsPane()
    {
        var current = JsonNode.Parse(WebDesignCurrent()) as JsonObject;
        var defs = WebDesignComponents.Read(current?["root"]?["props"]?["komponenten"]);
        var sp = new StackPanel { Spacing = 6, Name = "web-komponenten" };
        sp.Children.Add(new TextBlock { Text = "Komponenten und Vorlagen", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Eine Gruppe von Bausteinen wird zur Komponente: Änderungen am Original wirken überall, einzelne Verwendungen überschreiben Felder im Editor unter „Felder“.", Muted, 12));
        var name = new TextBox { Name = "web-komp-name", Text = _wdCompName, PlaceholderText = "Name, z. B. Preiskarte", Width = 220 };
        name.TextChanged += (_, _) => _wdCompName = name.Text ?? "";
        var create = Act("web-komp-erstellen", _wdSelection.Count == 0 ? "Auswahl zur Komponente machen" : $"Auswahl ({_wdSelection.Count}) zur Komponente machen", () => WebDesignComponentFromSelection(name.Text ?? ""), _wdSelection.Count > 0);
        create.IsEnabled = _wdSelection.Count > 0; // erst im Editor unter „Auswählen“ Bausteine wählen
        sp.Children.Add(new WrapPanel { Children = { name, new Border { Width = 8 }, create } });
        var uses = new Dictionary<string, int>();
        void Count(JsonNode? n) { if (n is JsonArray a) foreach (var x in a) Count(x); else if (n is JsonObject o) { if (o["type"]?.ToString() == "Komponente" && o["props"]?["komponente"]?.ToString() is { } k) uses[k] = uses.GetValueOrDefault(k) + 1; foreach (var kv in o) if (kv.Key != "komponenten") Count(kv.Value); } }
        Count(current?["content"]); Count(current?["zones"]);
        var i = 0;
        foreach (var c in defs)
        {
            var id = c.Id; var n = i++;
            sp.Children.Add(Row(P($"{c.Name} · {c.Blocks.Count} Baustein(e) · {uses.GetValueOrDefault(id)} Verwendung(en)", Text, 12.5),
                new WrapPanel { Children = { Act($"web-komp-einfuegen-{n}", "Einfügen", () => WebDesignComponentInsert(id)), new Border { Width = 6 }, Act($"web-komp-vorlage-{n}", "Als Vorlage speichern", () => WebDesignComponentSaveTemplate(id)) } }));
        }
        var templates = WebDesignComponents.Templates(WebDesignTemplatesDir);
        if (templates.Count > 0)
        {
            sp.Children.Add(new TextBlock { Text = "Eigene Vorlagen", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 6, 0, 0) });
            var t = 0;
            foreach (var (tn, _) in templates) { var tname = tn; sp.Children.Add(Row(P(tname, Muted, 12), Act($"web-vorlage-{t++}", "Ins Projekt holen", () => WebDesignTemplateInsert(tname)))); }
        }
        return new Border { Name = "web-komponenten-card", Padding = new Thickness(14), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = sp };
    }
}
