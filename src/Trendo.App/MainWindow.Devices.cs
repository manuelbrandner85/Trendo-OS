using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-07: Geräte und Laufwerke auf einen Blick, Probleme und Neues hervorgehoben.
/// T-0764: USB-Sticks und Speicherkarten sicher auswerfen (ohne Administratorrechte, nie das Systemlaufwerk oder Trendos eigenes Laufwerk),
/// neu eingesteckte Sticks erscheinen binnen Sekunden (Laufwerke werden alle 2 s nachgesehen, solange die Seite offen ist),
/// Geräte mit Problemcode zeigen einen verständlichen nächsten Schritt.</summary>
public partial class MainWindow
{
    public IProcessRunner? DeviceRunnerOverride { get; set; }
    public string? DeviceSysRootOverride { get; set; }
    /// <summary>T-0764 Tests: ersetzt die Laufwerksliste (eingesteckt/abgezogen nachstellen).</summary>
    public Func<IReadOnlyList<DriveSnapshot>>? DriveSourceOverride { get; set; }
    /// <summary>T-0764 Tests: ersetzt den echten Auswurf (cfgmgr32).</summary>
    public IDriveEjector? EjectorOverride { get; set; }
    /// <summary>T-0764 Tests: ersetzt die Zuordnung Pfad → Datenträger (Junction, zwei Partitionen auf einem Gerät nachstellen).</summary>
    public IDiskResolver? DiskResolverOverride { get; set; }
    public IReadOnlyList<DeviceInfo> Devices { get; private set; } = [];
    private IReadOnlyList<DeviceInfo> _devAdded = [], _devRemoved = []; private string _devMsg = "";
    private IReadOnlySet<string>? _devRemovable; // zuletzt gesehene Wechseldatenträger
    private DispatcherTimer? _devTimer;
    private bool _devEjecting;
    /// <summary>Wie oft die Laufwerke nachgesehen werden, solange „Geräte“ offen ist.</summary>
    public static readonly TimeSpan DevicePollInterval = TimeSpan.FromSeconds(2);

    private void InitSys07()
    {
        _showMap["geraete"] = DevicesView; SysTabs.Add(("geraete", "Geräte"));
        StudioPages["SYS-07"] = "geraete";
        ExtraPages.Add(("Geräte & Laufwerke", "geraete", ["geraet", "hardware", "usb", "drucker", "kamera", "laufwerk", "festplatte", "treiber", "auswerfen", "stick"]));
    }

    IReadOnlyList<DriveSnapshot> CurrentDrives() => (DriveSourceOverride ?? DeviceScanner.Drives)();

    public async Task ScanDevicesAsync()
    {
        var s = new DeviceScanner(DeviceRunnerOverride ?? new RealProcessRunner(), _core.DataRoot, DeviceSysRootOverride ?? "/sys") { DriveSource = CurrentDrives };
        Devices = await s.ScanAsync();
        (_devAdded, _devRemoved) = s.Diff(Devices);
        _devRemovable = DeviceScanner.RemovableRoots(CurrentDrives());
        _devMsg = $"{Devices.Count} Geräte gefunden{(Devices.Any(d => d.Problem) ? $", {Devices.Count(d => d.Problem)} mit Problem" : "")}.";
        _core.Audit.Append(CurrentPrincipal.Name, "devices.scan", $"{Devices.Count} geräte, neu={_devAdded.Count}, weg={_devRemoved.Count}");
    }

    /// <summary>T-0764: Laufwerke nachsehen (Timer alle 2 s, solange die Seite offen ist; Tests rufen es direkt). Ein neu eingesteckter oder abgezogener
    /// Wechseldatenträger aktualisiert die Liste sofort – ohne die langsame Gerätesuche. Liefert true, wenn sich etwas geändert hat.</summary>
    public bool DevicesPollNow()
    {
        if (Devices.Count == 0) return false; // erst nach der ersten Suche
        var drives = CurrentDrives();
        var now = DeviceScanner.RemovableRoots(drives);
        var before = _devRemovable ?? now;
        _devRemovable = now;
        if (now.SetEquals(before)) return false;
        var neu = now.Except(before).ToList(); var weg = before.Except(now).ToList();
        Devices = Devices.Where(d => !(d.Category == "Speicher" && d.Name.StartsWith("Laufwerk ", StringComparison.Ordinal)))
            .Concat(DeviceScanner.DriveEntries(drives)).OrderBy(d => d.Category).ThenBy(d => d.Name).ToList();
        _devAdded = Devices.Where(d => d.EjectRoot is { } r && neu.Contains(r.ToUpperInvariant())).ToList();
        _devRemoved = [];
        _devMsg = string.Join(" ", new[]
        {
            neu.Count > 0 ? "Neu eingesteckt: " + string.Join(", ", _devAdded.Select(d => d.Name)) + "." : "",
            weg.Count > 0 ? "Abgezogen: Laufwerk " + string.Join(", ", weg) + "." : "",
        }.Where(t => t.Length > 0));
        _core.Audit.Append(CurrentPrincipal.Name, "devices.drives", $"neu={string.Join(",", neu)} weg={string.Join(",", weg)}");
        if (CurrentView == "geraete") Show("geraete");
        return true;
    }

    void EnsureDeviceTimer()
    {
        if (_devTimer is not null || _opt.Isolated) return; // Tests rufen DevicesPollNow selbst
        _devTimer = new DispatcherTimer(DevicePollInterval, DispatcherPriority.Background, (_, _) => { if (CurrentView == "geraete" && !_devEjecting) DevicesPollNow(); });
        _devTimer.Start();
    }

    /// <summary>T-0764: Wechseldatenträger sicher auswerfen. Nie das Systemlaufwerk, nie das Laufwerk mit Trendo oder seinen Daten.</summary>
    public async Task<EjectResult> EjectDriveAsync(string root)
    {
        var drive = CurrentDrives().FirstOrDefault(d => string.Equals(d.Root, root, StringComparison.OrdinalIgnoreCase));
        if (drive is null) return Done(new(false, $"Laufwerk {root} ist nicht (mehr) da."));
        string[] protect = [Environment.SystemDirectory, AppContext.BaseDirectory, _core.DataRoot, _deps.ToolsRoot, _deps.VollpaketDir ?? "", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]; // T-0764: auch Vollpaket und Benutzerordner
        IDriveEjector? ejector = EjectorOverride ?? (OperatingSystem.IsWindows() ? new WindowsDriveEjector() : null);
        if (ejector is null) return Done(new(false, "Auswerfen geht hier nur unter Windows. Unter Linux bitte im Dateimanager auswerfen."));
        _devEjecting = true;
        try { return Done(await Task.Run(() => DeviceEject.Eject(drive.Root, drive.Type, protect, ejector, _core.Audit, CurrentPrincipal.Name, DiskResolverOverride))); }
        finally { _devEjecting = false; }

        EjectResult Done(EjectResult r)
        {
            if (r.Ok) DevicesPollNow();
            _devMsg = r.Message; // die Meldung des Auswurfs bleibt stehen
            if (CurrentView == "geraete") Show("geraete");
            return r;
        }
    }

    private Control DevicesView()
    {
        EnsureDeviceTimer();
        var sp = new StackPanel { Spacing = 6, Children = { P("Alles, was an deinem PC hängt. Trendo ändert keine Treiber – USB-Sticks und Speicherkarten kannst du hier sicher auswerfen.") } };
        var go = new Button { Name = "dev-scan", Content = Devices.Count == 0 ? "Geräte suchen" : "Neu suchen", Background = Accent };
        go.Click += async (_, _) => { go.IsEnabled = false; go.Content = "Suche …"; await ScanDevicesAsync(); Show("geraete"); };
        if (Devices.Count == 0 && _devMsg.Length == 0) { go.Padding = new Thickness(18, 10); sp.Children.Add(EmptyState("dev-empty", "🖨", "Welche Geräte hängen an deinem PC?", "Drucker, Kameras, USB-Sticks, Mikrofone, Bluetooth-Geräte und Bildschirme – Trendo zeigt sie dir mit Zustand und hilft, wenn etwas fehlt. Es wird nichts verändert.", go)); }
        else sp.Children.Add(go);
        sp.Children.Add(new TextBlock { Name = "dev-msg", Text = _devMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        if (_devAdded.Count > 0 && _devAdded.Count < Devices.Count) sp.Children.Add(P("Neu seit dem letzten Mal: " + string.Join(", ", _devAdded.Select(d => d.Name)), Brush.Parse("#4ADE80")));
        if (_devRemoved.Count > 0) sp.Children.Add(P("Nicht mehr da: " + string.Join(", ", _devRemoved.Select(d => d.Name)), Brush.Parse("#FACC15")));
        sp.Children.Add(DevicesPciCard()); // Alle Studios, Runde 9: Geräte ohne Namen benennen
        var probs = Devices.Where(d => d.Problem).ToList();
        if (probs.Count > 0)
        {
            sp.Children.Add(H("Braucht Aufmerksamkeit", 14));
            foreach (var (d, i) in probs.Select((d, i) => (d, i)))
                sp.Children.Add(WCard(new StackPanel { Children = { new TextBlock { Text = "⚠ " + d.Name, Foreground = Text },
                    new TextBlock { Name = "dev-hint-" + i, Text = $"Zustand: {d.Status}. Nächster Schritt: " + (d.Hint.Length > 0 ? d.Hint : "Gerät neu einstecken oder den PC neu starten. Hilft das nicht, den Treiber beim Hersteller laden."),
                        Foreground = Brush.Parse("#FACC15"), FontSize = 11, TextWrapping = TextWrapping.Wrap } } }));
        }
        foreach (var g in Devices.GroupBy(d => d.Category))
        {
            sp.Children.Add(H($"{g.Key} ({g.Count()})", 14));
            var box = new StackPanel { Name = "dev-group-" + g.Key, Spacing = 2 };
            foreach (var d in g)
            {
                var line = P($"{(d.Problem ? "⚠" : "✓")} {d.Name}{(d.Detail.Length > 0 ? " · " + d.Detail : "")}", d.Problem ? Brush.Parse("#FACC15") : Text, 12);
                if (d.EjectRoot is not { } root) { box.Children.Add(line); continue; }
                var ej = new Button { Name = "dev-eject-" + root[..1], Content = "Auswerfen", IsEnabled = !_devEjecting, Padding = new Thickness(10, 3) };
                ToolTip.SetTip(ej, "Sicher auswerfen – danach kannst du den Stick abziehen, ohne dass Daten verloren gehen.");
                ej.Click += async (_, _) => { ej.IsEnabled = false; ej.Content = "Wird ausgeworfen …"; await EjectDriveAsync(root); };
                box.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { line, Pos(ej, 1) } });
            }
            sp.Children.Add(WCard(box));
        }
        return SysPage("Geräte", sp);
    }
}
