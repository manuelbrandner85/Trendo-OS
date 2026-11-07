using Avalonia;
using Avalonia.Controls;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3, S3-02 (07.11.2026): Proxys erstellen und ein-/ausschalten. Vorschau mit Proxy, Export immer mit dem Original.</summary>
public partial class MainWindow
{
    bool _veProxyBusy;

    public async Task<List<VideoProxy.Made>> EditMakeProxies(ProgramDocument d)
    {
        var dir = Path.Combine(MediaFolder, ".proxy");
        if (FfmpegRunner(dir, out var why) is not { } run) { _progMsg[VK] = why!; Show(VK); return []; }
        _veProxyBusy = true; _progMsg[VK] = "Proxys werden erstellt …"; Show(VK);
        var data = (System.Text.Json.Nodes.JsonObject)d.Data.DeepClone(); List<VideoProxy.Made> made;
        try { made = await Task.Run(() => VideoProxy.MakeAll(run, data, dir)); }
        finally { _veProxyBusy = false; }
        var map = VideoProxy.Map(data);
        d.Edit("Proxys", x => { foreach (var (o, p) in map) VideoProxy.Set(x, o, p); x["useProxy"] = true; });
        _vePlayFrames.Clear(); _veFrames.Clear();
        var ok = made.Count(m => m.Proxy is not null); var bad = made.Where(m => m.Proxy is null).ToList();
        _progMsg[VK] = $"{ok} Proxy(s) bereit – die Vorschau nutzt sie, der Export immer die Originale." + (bad.Count > 0 ? " Nicht möglich: " + string.Join("; ", bad.Select(b => $"{Path.GetFileName(b.Original)} ({b.Note})")) : "");
        Show(VK);
        return made;
    }

    Control EditProxyPanel(ProgramDocument d)
    {
        var count = VideoProxy.Map(d.Data).Count;
        var make = new Button { Name = "ve-proxy-make", Content = _veProxyBusy ? "Proxys werden erstellt …" : count > 0 ? "Proxys auffrischen" : "Proxys erstellen (flüssige Vorschau)", IsEnabled = !_veProxyBusy, FontSize = 12, Margin = new Thickness(0, 0, 6, 0) };
        make.Click += async (_, _) => await EditMakeProxies(d);
        var use = new CheckBox { Name = "ve-proxy-use", Content = "Mit Proxys arbeiten", IsChecked = d.Flag("useProxy"), IsEnabled = count > 0, FontSize = 12 };
        use.IsCheckedChanged += (_, _) => { if ((use.IsChecked == true) != d.Flag("useProxy")) { d.Set("useProxy", use.IsChecked == true, "Proxys"); _vePlayFrames.Clear(); _veFrames.Clear(); Show(VK); } };
        ToolTip.SetTip(make, "Leichte Kopien (540p) für große Kameradateien – exportiert wird immer das Original");
        return new WrapPanel { Name = "ve-proxy", Margin = new Thickness(0, 0, 0, 6), Children = { make, use } };
    }
}
