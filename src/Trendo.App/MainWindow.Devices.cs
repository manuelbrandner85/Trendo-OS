using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-07: Geräte und Laufwerke auf einen Blick, Probleme und Neues hervorgehoben.</summary>
public partial class MainWindow
{
    public IProcessRunner? DeviceRunnerOverride { get; set; }
    public string? DeviceSysRootOverride { get; set; }
    public IReadOnlyList<DeviceInfo> Devices { get; private set; } = [];
    private IReadOnlyList<DeviceInfo> _devAdded = [], _devRemoved = []; private string _devMsg = "";

    private void InitSys07()
    {
        _showMap["geraete"] = DevicesView; SysTabs.Add(("geraete", "Geräte"));
        StudioPages["SYS-07"] = "geraete";
        ExtraPages.Add(("Geräte & Laufwerke", "geraete", ["geraet", "hardware", "usb", "drucker", "kamera", "laufwerk", "festplatte", "treiber"]));
    }

    public async Task ScanDevicesAsync()
    {
        var s = new DeviceScanner(DeviceRunnerOverride ?? new RealProcessRunner(), _core.DataRoot, DeviceSysRootOverride ?? "/sys");
        Devices = await s.ScanAsync();
        (_devAdded, _devRemoved) = s.Diff(Devices);
        _devMsg = $"{Devices.Count} Geräte gefunden{(Devices.Any(d => d.Problem) ? $", {Devices.Count(d => d.Problem)} mit Problem" : "")}.";
        _core.Audit.Append(Principal.LocalOwner.Name, "devices.scan", $"{Devices.Count} geräte, neu={_devAdded.Count}, weg={_devRemoved.Count}");
    }

    private Control DevicesView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Alles, was an deinem PC hängt. Trendo liest nur – es ändert keine Treiber.") } };
        var go = new Button { Name = "dev-scan", Content = Devices.Count == 0 ? "Geräte suchen" : "Neu suchen", Background = Accent };
        go.Click += async (_, _) => { go.IsEnabled = false; go.Content = "Suche …"; await ScanDevicesAsync(); Show("geraete"); };
        sp.Children.Add(go);
        sp.Children.Add(new TextBlock { Name = "dev-msg", Text = _devMsg, Foreground = Text });
        if (_devAdded.Count > 0 && _devAdded.Count < Devices.Count) sp.Children.Add(P("Neu seit dem letzten Mal: " + string.Join(", ", _devAdded.Select(d => d.Name)), Brush.Parse("#4ADE80")));
        if (_devRemoved.Count > 0) sp.Children.Add(P("Nicht mehr da: " + string.Join(", ", _devRemoved.Select(d => d.Name)), Brush.Parse("#FACC15")));
        var probs = Devices.Where(d => d.Problem).ToList();
        if (probs.Count > 0)
        {
            sp.Children.Add(H("Braucht Aufmerksamkeit", 14));
            foreach (var d in probs) sp.Children.Add(WCard(new StackPanel { Children = { new TextBlock { Text = "⚠ " + d.Name, Foreground = Text }, P($"Zustand: {d.Status}. Tipp: Gerät neu einstecken oder den PC neu starten. Hilft das nicht, den Treiber beim Hersteller laden.", Brush.Parse("#FACC15"), 11) } }));
        }
        foreach (var g in Devices.GroupBy(d => d.Category))
        {
            sp.Children.Add(H($"{g.Key} ({g.Count()})", 14));
            var box = new StackPanel { Name = "dev-group-" + g.Key, Spacing = 2 };
            foreach (var d in g) box.Children.Add(P($"{(d.Problem ? "⚠" : "✓")} {d.Name}{(d.Detail.Length > 0 ? " · " + d.Detail : "")}", d.Problem ? Brush.Parse("#FACC15") : Text, 12));
            sp.Children.Add(WCard(box));
        }
        return SysPage("Geräte", sp);
    }
}
