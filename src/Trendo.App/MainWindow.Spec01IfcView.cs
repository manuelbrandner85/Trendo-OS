using System.Text.Json;
using Avalonia.Controls;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 3 – SPEC-01: IFC-Dateien (BIM) in 3D ansehen. web-ifc (ThatOpen, MPL-2.0, unverändert) und three.js (MIT) als Seite unter Assets/ifc;
/// läuft ohne Netz, die IFC-Datei schickt Trendo über die WebView-Brücke. Die Ansicht füllt die Arbeitsfläche des Wohnungsplaners.</summary>
public partial class MainWindow
{
    string? _ifcViewFile; NativeWebView? _ifcView; string _ifcViewNote = "";
    public const long IfcViewMaxBytes = 200L * 1024 * 1024;
    public static string? IfcViewDirOverride { get; set; }
    public static string IfcViewDir => IfcViewDirOverride ?? Path.Combine(AppContext.BaseDirectory, "Assets", "ifc");
    public string? IfcViewFile => _ifcViewFile;
    public string IfcViewNote => _ifcViewNote;
    /// <summary>Für Tests: was an die Seite geschickt würde (statt InvokeScript).</summary>
    public Action<string>? IfcViewScriptOverride { get; set; }

    /// <summary>IFC-Datei in der 3D-Ansicht öffnen (öffentlich für Tests).</summary>
    public void ArchIfcViewOpen(string file)
    {
        if (!File.Exists(file) || !file.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)) { _progMsg[AK] = "Bitte eine IFC-Datei wählen."; Show(AK); return; }
        if (new FileInfo(file).Length > IfcViewMaxBytes) { _progMsg[AK] = "Die IFC-Datei ist größer als 200 MB."; Show(AK); return; }
        _ifcViewFile = file; _ifcView = null; _ifcViewNote = "";
        _progMsg[AK] = $"3D-Ansicht: {Path.GetFileName(file)} – Maus ziehen dreht, Rad zoomt.";
        _core.Audit.Append(Principal.LocalOwner.Name, "arch.ifc.ansehen", Path.GetFileName(file));
        Show(AK);
    }

    /// <summary>Aktuellen Grundriss als IFC exportieren und gleich ansehen.</summary>
    void ArchIfcViewCurrent(ProgramDocument d)
    {
        var file = Path.Combine(WorkFolder(Programs[AK].Folder), DevStudio.SafeName(d.Title) + ".ifc");
        try { ArchIfcExport(d, file); ArchIfcViewOpen(file); }
        catch (InvalidOperationException e) { _progMsg[AK] = e.Message; Show(AK); }
    }

    public void ArchIfcViewClose() { _ifcViewFile = null; _ifcView = null; _ifcViewNote = ""; Show(AK); }

    /// <summary>Beliebige IFC-Datei ansehen (z. B. vom Architekten) – öffentlich für Tests.</summary>
    public async Task ArchIfcPick() { if (await PickMedia("arch-ifc-datei", ["*.ifc"]) is { } f) ArchIfcViewOpen(f); }

    void IfcSend(string script) { if (IfcViewScriptOverride is { } o) o(script); else _ifcView?.InvokeScript(script); }

    /// <summary>Nachricht der Ansicht (öffentlich für Tests): „bereit“ → Datei schicken, „geladen“ → Bauteile merken, „fehler“ → Hinweis.</summary>
    public void IfcViewMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 10_000 || _ifcViewFile is null) return;
        try
        {
            using var doc = JsonDocument.Parse(body); var r = doc.RootElement; if (r.ValueKind != JsonValueKind.Object) return;
            var art = r.TryGetProperty("art", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
            int N(string k) => r.TryGetProperty(k, out var v) && v.TryGetInt32(out var n) ? n : 0;
            switch (art)
            {
                case "bereit":
                    var b64 = Convert.ToBase64String(File.ReadAllBytes(_ifcViewFile));
                    IfcSend($"window.trendoIfc(\"{b64}\",{JsonSerializer.Serialize(Path.GetFileNameWithoutExtension(_ifcViewFile))})");
                    break;
                case "geladen": _ifcViewNote = $"{N("waende")} Wände, {N("tueren")} Türen, {N("fenster")} Fenster, {N("raeume")} Räume"; break;
                case "fehler": _ifcViewNote = "⚠ " + (r.TryGetProperty("text", out var t) ? t.GetString() : "Fehler"); break;
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { _ifcViewNote = "⚠ IFC-Datei nicht lesbar."; }
    }

    /// <summary>Arbeitsfläche bei offener IFC-Ansicht (sonst null).</summary>
    Control? IfcViewArea()
    {
        if (_ifcViewFile is null || !File.Exists(Path.Combine(IfcViewDir, "index.html"))) return null;
        if (_opt.Isolated) return new Border { Name = "arch-ifc-area", Child = P("Testmodus: Die 3D-Ansicht wird nicht im Fenster geladen.", null, 12) };
        if (_ifcView is null)
        {
            _ifcView = new NativeWebView { Name = "arch-ifc-view" };
            _ifcView.WebMessageReceived += (_, e) => IfcViewMessage(e.Body);
            _ifcView.Navigate(new Uri(Path.Combine(IfcViewDir, "index.html")));
        }
        else if (_ifcView.Parent is Decorator old) old.Child = null;
        else if (_ifcView.Parent is Panel oldPanel) oldPanel.Children.Remove(_ifcView);
        return new Border { Name = "arch-ifc-area", CornerRadius = new Avalonia.CornerRadius(10), ClipToBounds = true, Child = _ifcView };
    }
}
