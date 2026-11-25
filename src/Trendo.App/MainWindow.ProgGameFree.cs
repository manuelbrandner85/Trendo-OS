using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0690: das frei gestaltbare Kachelspiel. Blickrichtung (von der Seite mit Schwerkraft oder von oben in vier Richtungen), eigene Bilder je Objektart
/// und die Prüfung des gebauten Spiels in einem echten Browser (Chrome oder Edge ohne Fenster, echte Tastendrücke) – nichts davon wird nachgebildet.</summary>
public partial class MainWindow
{
    /// <summary>Blickrichtung setzen (öffentlich für Tests und die KI): „seite“ oder „oben“ – ein Rückgängig-Schritt.</summary>
    public void GameSetView(ProgramDocument d, string view)
    {
        if (!GameStudio.Views.Any(v => v.Id == view)) throw new ArgumentException($"Unbekannte Blickrichtung „{view}“.");
        if (d.Text("browser") != "plattform") throw new InvalidOperationException("Die Blickrichtung gibt es beim selbst gemalten Kachelspiel.");
        if (GameDesign.View(d.Data) == view) return;
        d.Set("view", view, view == "oben" ? "Blick von oben" : "Blick von der Seite");
    }

    /// <summary>Eigenes Bild für eine Objektart setzen (Boden, Münze, Gegner …); leerer Pfad entfernt es (öffentlich für Tests und die KI).</summary>
    public void GameSetImage(ProgramDocument d, string kind, string file)
    {
        if (!GameStudio.ImageKinds.Any(i => i.Id == kind)) throw new ArgumentException($"Unbekannte Objektart „{kind}“.");
        if (file.Length > 0)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("Bild nicht gefunden.", file);
            if (Path.GetExtension(file).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp")) throw new ArgumentException("Bitte ein Bild (PNG, JPG, GIF oder WebP).");
        }
        d.Edit("Bild " + kind, x => { var o = x["images"] as JsonObject ?? []; if (file.Length == 0) o.Remove(kind); else o[kind] = file; x["images"] = o; });
    }

    /// <summary>Zeile über dem Level: Blickrichtung umschalten.</summary>
    Control GameViewRow(ProgramDocument d)
    {
        var cur = GameDesign.View(d.Data);
        var row = new WrapPanel { Name = "game-views", VerticalAlignment = VerticalAlignment.Center,
            Children = { new TextBlock { Text = "Blick", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }, new Border { Width = 6 } } };
        foreach (var (id, label, help) in GameStudio.Views)
        {
            var on = cur == id; var vv = id;
            var b = new Button { Name = "game-view-" + id, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 0),
                Background = on ? PaneBg : Brushes.Transparent, Foreground = on ? GoldText() : Text, BorderBrush = on ? EdgeLit : Edge, BorderThickness = new Thickness(1), FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal };
            ToolTip.SetTip(b, help);
            b.Click += (_, _) => ProgramRun(GK, () => GameSetView(d, vv));
            row.Children.Add(b);
        }
        row.Children.Add(new TextBlock { Text = cur == "oben" ? "Labyrinth, Abenteuer, Sammelspiel: laufen in alle vier Richtungen" : "Jump'n'Run: laufen und springen", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        return row;
    }

    /// <summary>Eigene Bilder je Objektart – ohne Bild zeichnet das Spiel die Form wie bisher.</summary>
    Control GameImagePanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-image-panel", Spacing = 4 };
        sp.Children.Add(H("Eigene Bilder", 13));
        sp.Children.Add(P("Ein Bild je Objektart (PNG, JPG, GIF, WebP) – es ersetzt im gebauten Spiel die gezeichnete Form.", Muted, 11));
        var have = d.Data["images"] as JsonObject ?? [];
        foreach (var (id, _, label) in GameStudio.ImageKinds)
        {
            var file = have[id].AsText(); var kind = id;
            var pick = Act($"game-image-pick-{id}", file is { Length: > 0 } ? "✓ " + Path.GetFileName(file) : "Bild…", async () =>
            {
                var got = PickOverride is { } fake ? await fake(new PickRequest($"game-image-{kind}", false, false, ["png", "jpg", "jpeg", "gif", "webp"]))
                    : (await PickOpenFiles(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Bild für „" + label + "“", FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp"] }] }))
                        .Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToArray();
                if (got is { Length: > 0 }) ProgramRun(GK, () => GameSetImage(d, kind, got[0]));
            });
            var row = new WrapPanel { Children = { new TextBlock { Text = label, Width = 96, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, pick } };
            if (file is { Length: > 0 }) row.Children.Add(Act($"game-image-del-{id}", "✕", () => ProgramRun(GK, () => GameSetImage(d, kind, ""))));
            sp.Children.Add(row);
        }
        return sp;
    }

    /// <summary>Ergebnis der letzten Browser-Prüfung (für Tests und die Anzeige).</summary>
    public string LastBrowserCheck { get; private set; } = "";

    /// <summary>Das gebaute Spiel in einem echten Browser prüfen: starten, die Figur mit echten Tastendrücken bewegen, Skriptfehler sammeln.
    /// Ohne Chrome/Edge: ehrliche Meldung, nichts wird nachgebildet. Liefert true, wenn das Spiel startet, die Figur sich bewegt und kein Fehler auftritt.</summary>
    public async Task<bool> GameBrowserCheck(ProgramDocument d)
    {
        var built = d.Text("built");
        if (built.Length == 0 || !File.Exists(Path.Combine(built, "index.html"))) throw new InvalidOperationException("Bitte zuerst „Bauen“ – geprüft wird das fertige Spiel.");
        if (d.Text("browser") != "plattform") throw new InvalidOperationException("Die Browser-Prüfung gibt es für das selbst gemalte Kachelspiel.");
        var errors = new List<string>();
        var top = GameDesign.View(d.Data) == "oben";
        // Start im Menü bestätigen, kurz stehen, dann jede Richtung einmal halten: mindestens eine muss die Figur bewegen
        var steps = new List<WebsitePreview.PlayStep> { new("until", "typeof pl==='object'&&typeof mode==='string'", 8000), new("down", "Enter", 80), new("up", "Enter", 250), new("eval", "mode"), new("eval", "pl.x+','+pl.y") };
        foreach (var key in top ? new[] { "ArrowRight", "ArrowDown", "ArrowLeft", "ArrowUp" } : new[] { "ArrowRight", "ArrowLeft" })
        { steps.Add(new("down", key, 450)); steps.Add(new("up", key, 60)); steps.Add(new("eval", "pl.x+','+pl.y")); }
        steps.Add(new("eval", "lives+'|'+over"));
        var r = await WebsitePreview.PlayAsync(built, steps, errors: errors);
        if (!r.Ok) { LastBrowserCheck = "Blockiert: " + r.Note; _progMsg[GK] = LastBrowserCheck; return false; }
        var ready = r.Values.ElementAtOrDefault(0) == "true"; var playing = r.Values.ElementAtOrDefault(1) == "play";
        var pos = r.Values.Skip(2).Take(r.Values.Count - 3).ToList();
        var moved = pos.Distinct().Count() > 1;
        var ok = ready && (playing || d.Data["menu"]?.GetValueKind() == System.Text.Json.JsonValueKind.False) && moved && errors.Count == 0;
        LastBrowserCheck = (ok ? "✓ Im echten Browser geprüft (" + r.Note + "): Spiel startet, die Figur bewegt sich mit den Tasten, keine Skriptfehler."
            : "⚠ Browser-Prüfung (" + r.Note + "): " + (!ready ? "das Spiel startet nicht" : !moved ? "die Figur bewegt sich nicht – ist der Start eingemauert?" : errors.Count > 0 ? string.Join(" · ", errors.Take(3)) : "das Spiel bleibt im Menü"));
        _progMsg[GK] = LastBrowserCheck;
        _core.Audit.Append(Principal.LocalOwner.Name, "game.browsercheck", (ok ? "ok " : "fehler ") + d.Text("title"));
        return ok;
    }
}
