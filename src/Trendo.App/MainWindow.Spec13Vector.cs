using Avalonia.Controls;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Karten;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 1 – SPEC-13: Vektorkarten (.pmtiles mit MVT) im Karten-Studio mit MapLibre GL JS (BSD-3) im WebView.
/// Die Seite (Assets/karten) läuft ohne Netz; Kacheln kommen über die Brücke aus Trendos eigenem PMTiles-Leser, Schriften und Symbole aus dem Bündel.</summary>
public partial class MainWindow
{
    VectorMap? _vec; NativeWebView? _vecView; string _vecNote = "";
    public static string? KartenDirOverride { get; set; }
    public static string KartenDir => KartenDirOverride ?? Path.Combine(AppContext.BaseDirectory, "Assets", "karten");
    public string VectorMapNote => _vecNote;
    public VectorMap? VectorMapOpen => _vec;
    /// <summary>Für Tests: was an die Seite geschickt würde (statt InvokeScript).</summary>
    public Action<string>? VectorScriptOverride { get; set; }

    static bool IsVectorPack(string path)
    {
        if (!path.EndsWith(".pmtiles", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return false;
        try { using var p = new PmTiles(path); return p.Info.Type == PmTiles.TileType.Mvt; }
        catch (InvalidDataException) { return false; }
    }

    /// <summary>Vektorkarte öffnen (öffentlich für Tests).</summary>
    public void MapOpenVector(string path)
    {
        _vec?.Dispose(); _vec = new VectorMap(path); _vecView = null; MapPackPath = path; Tiles.Pack = null;
        var i = _vec.Info;
        _vecNote = $"Vektorkarte „{_vec.Name}“: Zoom {i.MinZoom}–{i.MaxZoom}, Gebiet {i.MinLat:0.###}–{i.MaxLat:0.###} N, {i.MinLon:0.###}–{i.MaxLon:0.###} O.";
        _core.Audit.Append(Principal.LocalOwner.Name, "karten.vektor.oeffnen", _vec.Name);
        Show(MK);
    }

    void VectorSend(string script) { if (VectorScriptOverride is { } o) o(script); else _vecView?.InvokeScript(script); }

    /// <summary>Nachricht der Kartenseite (öffentlich für Tests).</summary>
    public void VectorMapMessage(string? body)
    {
        var m = VectorMap.Parse(body); if (m is null || _vec is null) return;
        switch (m.Kind)
        {
            case "bereit": VectorSend(_vec.InitScript()); break;
            case "kachel": if (_vec.Answer(m) is { } s) VectorSend(s); break;
            case "geladen": _vecNote = $"Vektorkarte „{_vec.Name}“ angezeigt."; break;
            case "fehler": _vecNote = "Karte: " + m.Text; Dispatcher.UIThread.Post(() => Show(MK)); break;
        }
    }

    Control? VectorMapPane()
    {
        if (_vec is null && _vecNote.Length == 0) return null;
        var sp = new StackPanel { Spacing = 6, Name = "mk-vector" };
        sp.Children.Add(H("Vektorkarte (MapLibre)", 12));
        if (_vecNote.Length > 0) sp.Children.Add(new TextBlock { Name = "mk-vector-note", Text = _vecNote, FontSize = 12, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        if (_vec is null) return sp;
        var pageOk = File.Exists(Path.Combine(KartenDir, "index.html"));
        if (!pageOk) { sp.Children.Add(P("Karten-Bündel fehlt im Programm (Assets/karten) – bitte Trendo neu installieren.", Avalonia.Media.Brush.Parse("#F5A524"), 12)); return sp; }
        sp.Children.Add(P("Die Vektorkarte füllt die Arbeitsfläche. Zum Zeichnen von Routen auf der Rasterkarte die Vektorkarte schließen.", null, 11));
        sp.Children.Add(Act("mk-vector-close", "Vektorkarte schließen", () => { _vec?.Dispose(); _vec = null; _vecView = null; _vecNote = ""; MapPackPath = ""; Show(MK); }));
        sp.Children.Add(P("Kartendaten © OpenStreetMap-Mitwirkende (ODbL) · Stil Protomaps (BSD-3) · Schrift Noto (OFL).", null, 10.5));
        return sp;
    }

    /// <summary>Arbeitsfläche bei offener Vektorkarte: das Browserfenster mit MapLibre statt der Rasterkarte (echter Lauf 08.11.2026: im schmalen Inspektor ragte das
    /// native Fenster aus dem Bildlauf heraus und war zu klein).</summary>
    Control? VectorMapView()
    {
        if (_vec is null || !File.Exists(Path.Combine(KartenDir, "index.html"))) return null;
        if (_opt.Isolated) return new Border { Name = "mk-vector-area", Child = P("Testmodus: Die Vektorkarte wird nicht im Fenster geladen.", null, 12) };
        if (_vecView is null)
        {
            _vecView = new NativeWebView { Name = "mk-vector-view" };
            _vecView.WebMessageReceived += (_, e) => VectorMapMessage(e.Body);
            _vecView.Navigate(new Uri(Path.Combine(KartenDir, "index.html")));
        }
        else if (_vecView.Parent is Decorator old) old.Child = null;
        else if (_vecView.Parent is Panel oldPanel) oldPanel.Children.Remove(_vecView);
        return new Border { Name = "mk-vector-area", CornerRadius = new Avalonia.CornerRadius(10), ClipToBounds = true, Child = _vecView };
    }
}
