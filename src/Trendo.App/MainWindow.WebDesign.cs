using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Web Design Studio im Studio „Webseite bauen“ (CORE-07): visueller Editor (Puck + React Three Fiber, als Bündel im Programm) im eingebetteten WebView.
/// Speichern über die WebView-Brücke in <c>seite.trendo.json</c> des Projekts (mit Sicherung), Export als fertige Seite in <c>veroeffentlichen/</c>.</summary>
public partial class MainWindow
{
    bool _wdOpen; NativeWebView? _wdView; string _wdNote = ""; string? _wdDir;
    /// <summary>Für Tests: Ordner des Editor-Bündels (sonst neben dem Programm in Assets/webdesign).</summary>
    public static string? WebDesignEditorDirOverride { get; set; }
    public static string WebDesignEditorDir => WebDesignEditorDirOverride ?? Path.Combine(AppContext.BaseDirectory, "Assets", "webdesign");
    /// <summary>Projektordner der visuellen Seite: die gebaute Seite, sonst ein eigener Ordner im Datenbereich.</summary>
    public string WebDesignDir => _wdDir ??= _webDir ?? Path.Combine(_core.DataRoot, "webseiten", "visuell");
    public string WebDesignNote => _wdNote;

    /// <summary>Nachricht des Editors verarbeiten (öffentlich für Tests, sonst aus dem WebView).</summary>
    public void WebDesignMessage(string? body)
    {
        var m = WebDesign.ParseMessage(body); if (m is null) return;
        switch (m.Kind)
        {
            case "bereit": // Editor geladen → gespeicherte Seite hineinreichen
                WebDesignReload(WebDesignCurrent()); // auch nach Schließen und Öffnen: ein ungespeicherter Entwurf bleibt sichtbar
                break;
            case "speichern":
                var err = WebDesignStates.Write(WebDesignDir, m.Data ?? "", _wdVariant);
                _wdNote = err ?? $"Gespeichert ({DateTime.Now:HH:mm:ss}) – {Path.Combine(WebDesignDir, WebDesign.PageFile)}";
                _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.speichern", err ?? "ok");
                if (err is null) _wdDraft = null;
                Dispatcher.UIThread.Post(() => Show("dev"));
                break;
            case "entwurf": // ungespeicherte Änderung im Editor: Grundlage für die nächste KI-Änderung, nichts wird geschrieben
                if (WebDesign.Validate(m.Data).Json is { } draft) _wdDraft = draft;
                break;
            case "auswahl":
                var ids = (System.Text.Json.Nodes.JsonNode.Parse(m.Data ?? "{}")?["ids"] as System.Text.Json.Nodes.JsonArray)?.Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList() ?? [];
                if (!ids.SequenceEqual(_wdSelection)) { _wdSelection = ids; Dispatcher.UIThread.Post(() => Show("dev")); }
                break;
            case "aktion":
                WebDesignHumanAction(m.Data);
                break;
        }
    }

    public void WebDesignExport()
    {
        var (dir, err) = WebDesign.Export(WebDesignDir, WebDesignEditorDir);
        _wdNote = err ?? $"Fertige Webseite: {Path.Combine(dir!, "index.html")} – läuft ohne Internet, mit 3D und Bewegungen.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.export", err ?? "ok");
    }

    Control VisualEditorPane()
    {
        var sp = new StackPanel { Spacing = 8, Name = "web-visual" };
        sp.Children.Add(H("🎨 Visueller Editor", 15));
        sp.Children.Add(P("Seite wie in einem Profi-Programm gestalten: Bausteine ziehen oder doppelklicken, Eigenschaften rechts, Ebenen, Handy-/Tablet-/Desktop-Ansicht, Rückgängig, 3D-Szene und Bewegungen. „Speichern“ im Editor sichert die Seite im Projekt.", null, 12));
        var editorOk = File.Exists(Path.Combine(WebDesignEditorDir, "index.html"));
        sp.Children.Add(new WrapPanel { Children = {
            Act("web-visual-open", _wdOpen ? "Editor schließen" : "Editor öffnen", () => { _wdOpen = !_wdOpen; if (!_wdOpen) _wdView = null; Show("dev"); }, true),
            new Border { Width = 8 },
            Act("web-visual-export", "Als fertige Webseite exportieren", () => { WebDesignExport(); Show("dev"); }),
            new Border { Width = 8 },
            Act("web-visual-browser", "Export im Browser öffnen", () => { var f = Path.Combine(WebDesignDir, WebDesign.ExportDir, "index.html"); if (File.Exists(f)) Launch(f); else { _wdNote = "Zuerst exportieren."; Show("dev"); } }),
        } });
        if (!editorOk) sp.Children.Add(P("Editor-Bündel fehlt im Programm (Assets/webdesign) – bitte Trendo neu installieren.", Avalonia.Media.Brush.Parse("#F5A524"), 12));
        sp.Children.Add(WebDesignKiPane());
        sp.Children.Add(WebDesignTokensPane());
        if (_wdNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-visual-note", Text = _wdNote, FontSize = 12, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        if (_wdOpen && editorOk)
        {
            if (_opt.Isolated) sp.Children.Add(P("Testmodus: Der Editor wird nicht im Fenster geladen.", null, 12));
            else
            {
                if (_wdView is null)
                {
                    // Höhe nach dem Fenster: die Knöpfe darüber (Export) bleiben sichtbar, das Mausrad über dem Editor gehört dem Editor
                    _wdView = new NativeWebView { Name = "web-visual-view", Height = Math.Clamp(Bounds.Height - 330, 480, 1100) };
                    _wdView.WebMessageReceived += (_, e) => WebDesignMessage(e.Body);
                    _wdView.Navigate(new Uri(Path.Combine(WebDesignEditorDir, "index.html")));
                }
                else if (_wdView.Parent is Decorator old) old.Child = null; // Browserfenster behalten (Zustand bleibt), nur aus dem alten Rahmen lösen
                else if (_wdView.Parent is Panel oldPanel) oldPanel.Children.Remove(_wdView);
                sp.Children.Add(new Border { CornerRadius = new Avalonia.CornerRadius(10), ClipToBounds = true, Child = _wdView });
            }
        }
        return sp;
    }
}
