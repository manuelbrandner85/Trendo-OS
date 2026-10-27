using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Print3D;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>N24 Trendo 3D-Druck mit Bambu Lab: Datei aus Trendo CAD oder vom PC, Vorbereiten (Slicen) in Bambu Studio, Drucker im Heimnetz mit
/// Status, Kamera und Fortschritt. Ein Druck startet nur nach ausdrücklicher Bestätigung – der Drucker heizt auf und bewegt sich.</summary>
public partial class MainWindow
{
    public string? PrintFile { get; private set; }
    public BambuPrinter? Printer { get; private set; }
    public BambuStatus? PrinterStatus => _bambu?.Status;
    public bool PrintConfirmOpen { get; private set; }
    private BambuClient? _bambu;
    private string _prMsg = "", _prCodeDraft = "";
    private byte[]? _prCamera;
    private DateTime _prLastDraw;

    private void InitPrint3D()
    {
        _showMap["3d-druck"] = PrintPageView;
        PageModules["3d-druck"] = "CORE-05";
        ExtraPages.Add(("Trendo 3D-Druck", "3d-druck", ["3d-druck", "3d druck", "drucken 3d", "bambu", "bambu lab", "slicer", "filament", "drucker a1", "p1s", "x1"]));
        Printer = LoadPrinter();
    }

    string PrintDir { get { var d = Path.Combine(_core.DataRoot, "3d-druck"); Directory.CreateDirectory(d); return d; } }
    BambuPrinter? LoadPrinter() { try { var f = Path.Combine(_core.DataRoot, "3d-druck", "drucker.json"); return File.Exists(f) ? JsonSerializer.Deserialize<BambuPrinter>(File.ReadAllText(f)) : null; } catch (JsonException) { return null; } }
    string? Thumb(BambuPrinter p) { var f = Path.Combine(PrintDir, $"zertifikat-{p.Serial}.txt"); return File.Exists(f) ? File.ReadAllText(f).Trim() : null; }
    void RememberThumb(BambuPrinter p, string t) => File.WriteAllText(Path.Combine(PrintDir, $"zertifikat-{p.Serial}.txt"), t);

    /// <summary>Datei für den Druck wählen (aus Trendo CAD oder vom PC) und die Seite öffnen.</summary>
    public void Open3dPrint(string file) { PrintFile = file; _prMsg = $"Datei: {Path.GetFileName(file)}"; Show("3d-druck"); }

    /// <summary>Drucker speichern: Name, IP-Adresse, Seriennummer; der Zugangscode kommt in den Geheimnisspeicher, nie in eine Datei.</summary>
    public void SavePrinter(string name, string ip, string serial, string? accessCode)
    {
        if (!System.Net.IPAddress.TryParse(ip.Trim(), out var addr) || !PhoneAccessLike(addr)) throw new ArgumentException("Bitte die IP-Adresse des Druckers im Heimnetz eintragen (z. B. 192.168.1.40).");
        if (serial.Trim().Length < 8) throw new ArgumentException("Die Seriennummer steht am Drucker unter Einstellungen → Gerät.");
        Printer = new BambuPrinter(name.Trim().Length > 0 ? name.Trim() : "Bambu-Drucker", ip.Trim(), serial.Trim().ToUpperInvariant());
        File.WriteAllText(Path.Combine(PrintDir, "drucker.json"), JsonSerializer.Serialize(Printer));
        if (accessCode is { Length: > 0 }) _core.Secrets.Set(Printer.SecretKey, accessCode.Trim());
        _core.Audit.Append(Principal.LocalOwner.Name, "3ddruck.drucker", $"{Printer.Name} {Printer.Ip}");
    }

    /// <summary>Nur Adressen im eigenen Netz (privat, Tailscale, Loopback) – der LAN-Modus ist nie aus dem Internet erreichbar.</summary>
    static bool PhoneAccessLike(System.Net.IPAddress a) => PhoneAccess.AllowedAddress(a);

    public async Task<bool> ConnectPrinter(Func<Task<Stream>>? openStream = null)
    {
        if (Printer is not { } p) { _prMsg = "Erst den Drucker eintragen."; return false; }
        var code = _core.Secrets.Get(p.SecretKey);
        if (code is null) { _prMsg = "Der Zugangscode fehlt (am Drucker: Einstellungen → Netzwerk → LAN-Modus)."; return false; }
        await DisconnectPrinter();
        try
        {
            var stream = openStream is not null ? await openStream() : await BambuNet.OpenTls(p.Ip, 8883, () => Thumb(p), t => RememberThumb(p, t), CancellationToken.None);
            var c = new BambuClient(p, code);
            c.Changed += _ => Dispatcher.UIThread.Post(() => { if (CurrentView == "3d-druck" && DateTime.UtcNow - _prLastDraw > TimeSpan.FromSeconds(1)) { _prLastDraw = DateTime.UtcNow; Show("3d-druck"); } });
            await c.ConnectAsync(stream);
            _bambu = c; _prMsg = $"Verbunden mit „{p.Name}“.";
            _core.Audit.Append(Principal.LocalOwner.Name, "3ddruck.verbunden", p.Name);
            return true;
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or InvalidOperationException or System.Security.Authentication.AuthenticationException or TimeoutException)
        {
            _prMsg = ex is System.Security.Authentication.AuthenticationException ? "Der Drucker meldet sich mit einem anderen Zertifikat als beim ersten Mal – aus Sicherheit nicht verbunden." : "Verbinden ging nicht: " + ex.Message + " – Ist der LAN-Modus am Drucker an und der PC im selben Netz?";
            return false;
        }
    }

    public async Task DisconnectPrinter() { if (_bambu is { } c) { _bambu = null; await c.DisposeAsync(); } }

    /// <summary>Datei hochladen und den Druck starten – nur über die Bestätigung <see cref="ConfirmPrint"/>.</summary>
    public void AskPrint()
    {
        if (PrintFile is null || !PrintFile.EndsWith(".gcode.3mf", StringComparison.OrdinalIgnoreCase)) { _prMsg = "Drucken geht mit einer in Bambu Studio vorbereiteten Datei (.gcode.3mf): dort „Slicen“ und „Exportieren“ wählen."; return; }
        if (_bambu is null) { _prMsg = "Erst mit dem Drucker verbinden."; return; }
        PrintConfirmOpen = true;
    }

    public async Task ConfirmPrint(bool yes, Func<string, Task>? upload = null)
    {
        PrintConfirmOpen = false;
        if (!yes || _bambu is not { } c || Printer is not { } p || PrintFile is not { } f) { _prMsg = "Druck nicht gestartet."; return; }
        try
        {
            _prMsg = "Datei wird zum Drucker übertragen …"; Show("3d-druck");
            if (upload is not null) await upload(f);
            else await BambuFtp.UploadAsync(p.Ip, _core.Secrets.Get(p.SecretKey) ?? "", f, () => Thumb(p), t => RememberThumb(p, t));
            await c.StartPrint(Path.GetFileName(f));
            _prMsg = $"Druck gestartet: {Path.GetFileName(f)}.";
            _core.Audit.Append(Principal.LocalOwner.Name, "3ddruck.start", $"{p.Name} {Path.GetFileName(f)}");
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException) { _prMsg = "Übertragen ging nicht: " + ex.Message; }
        Show("3d-druck");
    }

    async Task PrinterCommand(string cmd)
    {
        if (_bambu is null) return;
        try { await _bambu.Command(cmd); _prMsg = cmd switch { "pause" => "Pausiert.", "resume" => "Geht weiter.", _ => "Druck abgebrochen." }; _core.Audit.Append(Principal.LocalOwner.Name, "3ddruck." + cmd, Printer?.Name ?? ""); }
        catch (IOException ex) { _prMsg = "Befehl ging nicht: " + ex.Message; }
        Show("3d-druck");
    }

    async Task CameraSnapshot()
    {
        if (Printer is not { } p || _core.Secrets.Get(p.SecretKey) is not { } code) return;
        try { _prCamera = await BambuCameraNet.SnapshotAsync(p.Ip, code, () => Thumb(p), t => RememberThumb(p, t)); _prMsg = "Kamerabild aktualisiert."; }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException)
        {
            // X1-Drucker: Kamera über RTSPS mit FFmpeg
            if (FfmpegPath is { } ff)
            {
                var o = Path.Combine(Path.GetTempPath(), "trendo-kamera-" + Guid.NewGuid().ToString("N") + ".jpg");
                var r = await Task.Run(() => Cli.Run(ff, BambuCamera.RtspArgs(p.Ip, code, o), timeoutMs: 20_000));
                if (r.Ok && File.Exists(o)) { _prCamera = File.ReadAllBytes(o); TryDelete(o); _prMsg = "Kamerabild aktualisiert."; Show("3d-druck"); return; }
            }
            _prMsg = "Kamerabild ging nicht: " + ex.Message;
        }
        Show("3d-druck");
    }

    /// <summary>Bambu Studio: installiert über „Programme von GitHub“ oder im Standardordner.</summary>
    string? BambuStudioExe()
    {
        var gh = GitHubApps.List(_deps.ToolsRoot).FirstOrDefault(e => e.Repo.Equals("bambulab/BambuStudio", StringComparison.OrdinalIgnoreCase));
        if (gh is not null && Path.Combine(_deps.ToolsRoot, gh.Id, gh.Exe.Replace('/', Path.DirectorySeparatorChar)) is var exe && File.Exists(exe)) return exe;
        foreach (var p in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Programs" })
            if (Path.Combine(p, "Bambu Studio", "bambu-studio.exe") is var f && File.Exists(f)) return f;
        return null;
    }

    async Task PickPrintFile()
    {
        var req = new PickRequest("3d-druck-datei", false, false, ["3mf", "stl", "step", "obj"]);
        var f = PickOverride is { } fake ? (await fake(req))?.FirstOrDefault() : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Datei für den 3D-Druck", FileTypeFilter = [new FilePickerFileType("3D-Modelle") { Patterns = ["*.3mf", "*.stl", "*.step", "*.stp", "*.obj"] }] })).Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
        if (f is not null) Open3dPrint(f);
    }

    // ---------- Oberfläche ----------
    Control PrintPageView()
    {
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(PageHead("Trendo 3D-Druck"));
        // 1) Datei
        var file = new StackPanel { Spacing = 6, Children = { H("1 · Was soll gedruckt werden?", 15) } };
        file.Children.Add(P(PrintFile is null ? "Noch keine Datei. Aus Trendo CAD weitergeben oder eine Datei wählen (3MF, STL, STEP, OBJ)." : $"{Path.GetFileName(PrintFile)} · {new FileInfo(PrintFile).Length / 1024.0:0.#} KB" + (PrintFile.EndsWith(".gcode.3mf", StringComparison.OrdinalIgnoreCase) ? " · druckfertig" : " · noch vorzubereiten"), null, 13));
        var pick = new Button { Name = "print-pick", Content = "Datei wählen …" }; pick.Click += (_, _) => _ = PickPrintFile();
        var fromCad = new Button { Name = "print-from-cad", Content = "Aus Trendo CAD" }; fromCad.Click += (_, _) => { try { Open3dPrint(CadExportAs("3mf")); } catch (Trendo.Core.Cad.CadError ex) { _prMsg = ex.Message; Show("3d-druck"); } };
        file.Children.Add(new WrapPanel { Children = { pick, new Border { Width = 6 }, fromCad } });
        sp.Children.Add(Card(file));
        // 2) Vorbereiten in Bambu Studio
        var slicer = new StackPanel { Spacing = 6, Children = { H("2 · Vorbereiten (Slicen) in Bambu Studio", 15),
            P("Bambu Studio rechnet aus dem Modell die Bahnen für den Drucker: Drucker und Filament wählen, „Slice“, dann „Drucken“ oder „Exportieren“ als .gcode.3mf.", null, 13) } };
        var exe = BambuStudioExe();
        if (exe is not null)
        {
            var open = new Button { Name = "print-open-studio", Content = "In Bambu Studio öffnen", Background = Accent, IsEnabled = PrintFile is not null };
            open.Click += (_, _) => { if (PrintFile is { } f && !_opt.Isolated) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { ArgumentList = { f }, UseShellExecute = false }); _core.Audit.Append(Principal.LocalOwner.Name, "3ddruck.slicer", Path.GetFileName(f)); } catch (System.ComponentModel.Win32Exception ex) { _prMsg = ex.Message; } } };
            slicer.Children.Add(open);
        }
        else
        {
            var install = new Button { Name = "print-install-studio", Content = "Bambu Studio einrichten (kostenlos, AGPL)" };
            install.Click += (_, _) => _ = GitHubInstall(new OssRepo("bambulab/BambuStudio", "Slicer von Bambu Lab für die Drucker A1, P1 und X1.", "https://github.com/bambulab/BambuStudio", "https://github.com/bambulab/BambuStudio.git", 0, "AGPL-3.0", "C++", DateTimeOffset.UtcNow));
            slicer.Children.Add(install);
            slicer.Children.Add(P("Installation nur nach deiner Zustimmung, aus der offiziellen GitHub-Seite von Bambu Lab.", null, 12));
        }
        sp.Children.Add(Card(slicer));
        // 3) Drucker im Heimnetz
        var pr = new StackPanel { Spacing = 6, Children = { H("3 · Drucker im Heimnetz (LAN-Modus)", 15) } };
        var name = new TextBox { Name = "print-name", Text = Printer?.Name ?? "", PlaceholderText = "Name, z. B. Werkstatt", Width = 180 };
        var ip = new TextBox { Name = "print-ip", Text = Printer?.Ip ?? "", PlaceholderText = "IP, z. B. 192.168.1.40", Width = 170 };
        var serial = new TextBox { Name = "print-serial", Text = Printer?.Serial ?? "", PlaceholderText = "Seriennummer", Width = 180 };
        var code = new TextBox { Name = "print-code", Text = _prCodeDraft, PasswordChar = '•', PlaceholderText = Printer is not null && _core.Secrets.Get(Printer.SecretKey) is not null ? "Zugangscode gespeichert" : "Zugangscode", Width = 150 };
        code.TextChanged += (_, _) => _prCodeDraft = code.Text ?? "";
        var save = new Button { Name = "print-save", Content = "Speichern" };
        save.Click += (_, _) => { try { SavePrinter(name.Text ?? "", ip.Text ?? "", serial.Text ?? "", _prCodeDraft); _prCodeDraft = ""; _prMsg = "Drucker gespeichert."; } catch (ArgumentException ex) { _prMsg = ex.Message; } Show("3d-druck"); };
        pr.Children.Add(new WrapPanel { Children = { name, new Border { Width = 4 }, ip, new Border { Width = 4 }, serial, new Border { Width = 4 }, code, new Border { Width = 4 }, save } });
        pr.Children.Add(P("So findest du die Angaben: am Drucker Einstellungen → Netzwerk → „LAN-Modus“ einschalten; dort stehen IP-Adresse und Zugangscode. Die Seriennummer steht unter Einstellungen → Gerät. Der Zugangscode wird geschützt gespeichert.", null, 12));
        var connect = new Button { Name = "print-connect", Content = _bambu is null ? "Verbinden" : "Trennen", Background = _bambu is null ? Accent : null, IsEnabled = Printer is not null };
        connect.Click += async (_, _) => { if (_bambu is null) await ConnectPrinter(); else { await DisconnectPrinter(); _prMsg = "Getrennt."; } Show("3d-druck"); };
        pr.Children.Add(connect);
        if (_bambu?.Status is { } st)
        {
            var bar = new ProgressBar { Name = "print-progress", Value = st.Percent, Maximum = 100, Height = 10, Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
            pr.Children.Add(new TextBlock { Name = "print-state", Text = $"{st.StateText}{(st.File.Length > 0 ? $" · {st.File}" : "")}", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
            pr.Children.Add(bar);
            pr.Children.Add(P($"{st.Percent} % · noch {st.RemainingMin / 60} h {st.RemainingMin % 60} min · Schicht {st.Layer}/{st.TotalLayers} · Düse {st.Nozzle:0}/{st.NozzleTarget:0} °C · Bett {st.Bed:0}/{st.BedTarget:0} °C", null, 13));
            var pause = new Button { Name = "print-pause", Content = st.State == "PAUSE" ? "Weiter" : "Pause" }; pause.Click += (_, _) => _ = PrinterCommand(st.State == "PAUSE" ? "resume" : "pause");
            var stop = new Button { Name = "print-stop", Content = "Abbrechen …" }; stop.Click += (_, _) => { _prMsg = "Wirklich abbrechen? Dann „Ja, abbrechen“ drücken."; Show("3d-druck"); };
            var cam = new Button { Name = "print-camera", Content = "Kamerabild" }; cam.Click += (_, _) => _ = CameraSnapshot();
            var row = new WrapPanel { Children = { pause, new Border { Width = 4 }, stop, new Border { Width = 4 }, cam } };
            if (_prMsg.StartsWith("Wirklich abbrechen", StringComparison.Ordinal)) { var yes = new Button { Name = "print-stop-yes", Content = "Ja, abbrechen", Background = Brush.Parse("#DC2626") }; yes.Click += (_, _) => _ = PrinterCommand("stop"); row.Children.Add(new Border { Width = 4 }); row.Children.Add(yes); }
            pr.Children.Add(row);
            var printBtn = new Button { Name = "print-start", Content = "Hochladen und drucken …", Background = Accent, IsEnabled = PrintFile?.EndsWith(".gcode.3mf", StringComparison.OrdinalIgnoreCase) == true }; printBtn.Click += (_, _) => { AskPrint(); Show("3d-druck"); };
            pr.Children.Add(printBtn);
        }
        if (PrintConfirmOpen && Printer is { } pp && PrintFile is { } pf)
        {
            var yes = new Button { Name = "print-confirm-yes", Content = "Ja, jetzt drucken", Background = Accent }; yes.Click += (_, _) => _ = ConfirmPrint(true);
            var no = new Button { Name = "print-confirm-no", Content = "Abbrechen" }; no.Click += (_, _) => { _ = ConfirmPrint(false); Show("3d-druck"); };
            pr.Children.Add(new Border { Name = "print-confirm", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = $"„{pp.Name}“ druckt „{Path.GetFileName(pf)}“.", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                P("Der Drucker heizt auf und bewegt sich. Ist die Druckplatte leer und sauber, und ist das richtige Filament eingelegt?", null, 13),
                new WrapPanel { Children = { yes, new Border { Width = 6 }, no } } } } });
        }
        if (_prCamera is { } jpg) try { using var ms = new MemoryStream(jpg); pr.Children.Add(new Image { Name = "print-camera-image", Source = new Avalonia.Media.Imaging.Bitmap(ms), MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Left }); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException) { }
        sp.Children.Add(Card(pr));
        if (_prMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "print-msg", Text = _prMsg, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        return new Border { Name = "print-page-root", Background = Brush.Parse(C("#F2081329", "#F7F4F7FC")), CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Child = new ScrollViewer { Content = sp } };
    }
}
