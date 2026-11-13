using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Einrichtung „Trendo OS auf USB-Stick / externe SSD“ (Wunsch 04.11.2026): Abbild aus dem neuesten Release erkennen, Laufwerk wählen,
/// Löschen bestätigen (Laufwerksbuchstabe eintippen), Abbild laden und prüfen, mit Administratorrechten schreiben, zurücklesen,
/// danach Boot-Taste je Hersteller erklären. Den verschlüsselten Speicherbereich legt Trendo OS beim ersten Start selbst an.</summary>
public partial class MainWindow
{
    TrendoImage.Manifest? _imgManifest;
    string? _wizDrive;
    string _imgStatus = ""; double _imgProgress; bool _imgBusy; bool? _imgOk;
    /// <summary>Tests: Schreiben ersetzen (Abbildpfad, Laufwerk, Beschreibung, SSD?) – nie ein echtes Laufwerk.</summary>
    public Func<string, string, TrendoImage.Manifest, bool, Task<(bool Ok, string Text)>>? ImageWriteOverride { get; set; }
    /// <summary>Gefundenes Abbild (Bildschirmfotos dürfen es setzen).</summary>
    public TrendoImage.Manifest? ImageManifest { get => _imgManifest; set => _imgManifest = value; }
    public void SetupPickDrive(string letter) { _wizDrive = letter; if (_wizFull) Show("einrichtung-start"); }

    string ImageDir => Path.Combine(_core.DataRoot, "abbild");

    /// <summary>Tests: Entfernen vom PC ersetzen (nie die echte Deinstallation).</summary>
    public Action? UninstallPcOverride { get; set; }

    /// <summary>Nur Stick/SSD gewählt: Abbild-Zwischenablage löschen, Daten zum Löschen markieren und Trendo still deinstallieren.
    /// Die Deinstallation (Velopack) entfernt Programm und Werkzeuge; der Merker sorgt dafür, dass auch die Daten dieser Einrichtung gehen.</summary>
    void StickOnlyFinish()
    {
        try { if (Directory.Exists(ImageDir)) Directory.Delete(ImageDir, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        _core.Audit.Append(Principal.LocalOwner.Name, "setup.nur-stick", "Trendo wird vom PC entfernt");
        if (UninstallPcOverride is { } o) { o(); return; }
        if (_opt.Isolated) return;
        var update = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) ?? "", "Update.exe");
        if (!File.Exists(update)) { _imgStatus = "Trendo läuft hier ohne Installation – auf diesem PC ist nichts zu entfernen. Du kannst das Fenster schließen."; Show("einrichtung-abbild"); return; }
        try { File.WriteAllText(Path.Combine(_core.DataRoot, UninstallPrompt.StickOnlyMarker), "nur Stick/SSD gewählt"); } catch (IOException) { }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(update) { UseShellExecute = false, ArgumentList = { "--uninstall", "-s" } }); }
        catch (System.ComponentModel.Win32Exception e) { _imgStatus = "Entfernen ging nicht: " + e.Message + " – bitte über Windows „Apps“ deinstallieren."; Show("einrichtung-abbild"); return; }
        (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    /// <summary>Liegt im neuesten Release ein Trendo-OS-Abbild? (beim Start der Einrichtung im Hintergrund geprüft)</summary>
    public async Task CheckImageAsync(CancellationToken ct = default)
    {
        if (_core.Offline || (_opt.Isolated && VollpaketOnlineOverride is null)) return; // Tests gehen nie ins Internet
        try
        {
            Directory.CreateDirectory(ImageDir);
            var f = Path.Combine(ImageDir, TrendoImage.ManifestName);
            await VollpaketGet(TrendoImage.LatestBase + TrendoImage.ManifestName, f, 1 << 20, null, ct);
            _imgManifest = TrendoImage.Parse(await File.ReadAllTextAsync(f, ct));
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException) { _imgManifest = null; }
        _ui.Post(() => { if (_wizFull && CurrentView == "einrichtung-start") Show("einrichtung-start"); });
    }

    /// <summary>Abbild laden (Teile mit Prüfsumme), zusammensetzen, auf das Laufwerk schreiben und zurücklesen.</summary>
    public async Task<(bool Ok, string Text)> WriteImageToDrive(string letter, bool ssd, CancellationToken ct = default)
    {
        if (_imgManifest is not { } m) return (false, "Kein Trendo-OS-Abbild gefunden.");
        var dir = Path.Combine(ImageDir, m.Version); Directory.CreateDirectory(dir);
        var img = Path.Combine(dir, $"TrendoOS-{m.Version}.img");
        _core.Audit.Append(Principal.LocalOwner.Name, "abbild.schreiben.start", $"{m.Version} → {letter} ({(ssd ? "SSD" : "Stick")})");
        void Say(string t, double p) => _ui.Post(() => { if (!_imgBusy) return; _imgStatus = t; _imgProgress = p; if (CurrentView == "einrichtung-abbild") Show("einrichtung-abbild"); });
        try
        {
            if (!File.Exists(img) || new FileInfo(img).Length != m.Bytes)
            {
                long before = 0;
                foreach (var p in m.Parts)
                {
                    var f = Path.Combine(dir, p.File);
                    if (!File.Exists(f) || new FileInfo(f).Length != p.Bytes)
                    {
                        var b0 = before;
                        await VollpaketGet(TrendoImage.LatestBase + p.File, f, p.Bytes, d => Say($"Trendo OS wird geladen: {(b0 + d) >> 20:N0} von {m.Bytes >> 20:N0} MB", 0.6 * (b0 + d) / m.Bytes), ct);
                    }
                    before += p.Bytes;
                }
                Say("Abbild wird geprüft …", 0.6);
                if (await TrendoImage.JoinAsync(m, dir, img, null, ct) is { } bad) { foreach (var p in m.Parts) TryDelete(Path.Combine(dir, p.File)); return (false, bad + " Die Teile werden beim nächsten Versuch neu geladen."); }
                foreach (var p in m.Parts) TryDelete(Path.Combine(dir, p.File)); // Platz sparen: das geprüfte Abbild reicht
            }
            Say("Trendo OS wird auf das Laufwerk geschrieben und danach Bit für Bit geprüft – Windows fragt gleich nach der Erlaubnis.", 0.65);
            var r = ImageWriteOverride is { } o ? await o(img, letter, m, ssd) : await ImageWriter.StartElevatedAsync(img, letter, m, ssd, dir);
            _core.Audit.Append(Principal.LocalOwner.Name, r.Ok ? "abbild.schreiben.ok" : "abbild.schreiben.fehler", r.Text);
            return r;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            _core.Audit.Append(Principal.LocalOwner.Name, "abbild.schreiben.fehler", e.GetType().Name);
            return (false, e is TaskCanceledException ? "Abgebrochen." : "Laden fehlgeschlagen – bitte Internet prüfen und erneut versuchen. Bereits geladene Teile bleiben erhalten.");
        }
    }

    Control SetupImageView()
    {
        var sp = new StackPanel { Name = "wiz-img", Spacing = 12 };
        var drive = Drives().FirstOrDefault(d => d.Name == _wizDrive);
        var label = drive.Name is null ? _wizDrive ?? "?" : $"{(drive.Label.Length > 0 ? drive.Label : "Laufwerk")} ({drive.Name}, {drive.Gb:0} GB)";
        sp.Children.Add(new TextBlock { Text = _wizUse == "ssd" ? "Trendo OS auf die externe SSD" : "Trendo OS auf den USB-Stick", FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = GoldText() });
        sp.Children.Add(P($"Ziel: {label} · Trendo OS {_imgManifest?.Version} · {(_imgManifest?.Bytes ?? 0) / 1e9:0.0} GB", Text, 14));

        if (_imgOk is true)
        {
            sp.Children.Add(new TextBlock { Name = "wiz-img-done", Text = "" + _imgStatus, Foreground = Brush.Parse("#4ADE80"), FontSize = 15, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(H("So startest du Trendo OS", 16));
            sp.Children.Add(P("PC neu starten und gleich nach dem Einschalten mehrmals die Boot-Taste drücken, dann das Laufwerk wählen. Windows bleibt dabei unberührt – ohne Laufwerk startet der PC wie immer.", Text, 13));
            var keys = new WrapPanel { Name = "wiz-img-keys" };
            foreach (var (maker, key) in TrendoImage.BootKeys)
                keys.Children.Add(new Border { Background = Brush.Parse("#14FFFFFF"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5), Margin = new Thickness(0, 0, 6, 6),
                    Child = new TextBlock { Text = $"{maker}: {key}", Foreground = Text, FontSize = 12 } });
            sp.Children.Add(keys);
            sp.Children.Add(P("Beim ersten Start fragt Trendo OS nach einem Passwort und legt damit den verschlüsselten Speicherbereich auf dem restlichen Platz an – dort bleiben deine Dateien, Programme und KI-Modelle.", Muted, 12));
            if (_settings.SetupDone) // Trendo ist auf diesem PC schon eingerichtet: nur fertig, nie vom PC entfernen
            {
                sp.Children.Add(Act("wiz-img-close", "Fertig", () => { LeaveSetupFullscreen(); Show("modus"); }, true));
                return sp;
            }
            sp.Children.Add(P("Du hast nur den Stick bzw. die SSD gewählt. Mit „Fertig“ entfernt Trendo sich wieder von diesem PC – Windows bleibt genau so, wie es vorher war.", Text, 13));
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = {
                Act("wiz-img-next", "Zusätzlich als Programm auf diesem PC nutzen", () => { _wizUse = "programm"; Show("einrichtung"); }),
                Act("wiz-img-finish", "Fertig – Trendo vom PC entfernen", () => StickOnlyFinish(), true) } });
            return sp;
        }

        sp.Children.Add(new Border { Background = Brush.Parse("#33DC2626"), BorderBrush = Brush.Parse("#DC2626"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10),
            Child = P($"Achtung: Alles auf {label} wird gelöscht. Das Windows-Laufwerk und interne Festplatten schützt Trendo – sie lassen sich hier nicht wählen.", Text, 13) });
        var confirm = new TextBox { Name = "wiz-img-confirm", Width = 120, PlaceholderText = (_wizDrive ?? "").TrimEnd(':'), IsEnabled = !_imgBusy, HorizontalAlignment = HorizontalAlignment.Left };
        var write = new Button { Name = "wiz-img-write", Content = "Löschen und Trendo OS schreiben", Background = Brush.Parse("#B91C1C"), Padding = new Thickness(18, 8), IsEnabled = false };
        confirm.TextChanged += (_, _) => write.IsEnabled = !_imgBusy && string.Equals((confirm.Text ?? "").Trim().TrimEnd(':'), (_wizDrive ?? "").TrimEnd(':'), StringComparison.OrdinalIgnoreCase);
        write.Click += async (_, _) =>
        {
            if (_wizDrive is not { } d) return;
            _imgBusy = true; _imgOk = null; _imgStatus = "Wird vorbereitet …"; _imgProgress = 0; Show("einrichtung-abbild");
            var (ok, text) = await WriteImageToDrive(d, _wizUse == "ssd");
            _imgBusy = false; _imgOk = ok; _imgStatus = text; _imgProgress = ok ? 1 : 0; Show("einrichtung-abbild");
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
            new TextBlock { Text = "Zur Bestätigung den Laufwerksbuchstaben eintippen:", Foreground = Text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center }, confirm } });
        if (_imgBusy || _imgStatus.Length > 0)
        {
            sp.Children.Add(new ProgressBar { Name = "wiz-img-bar", Minimum = 0, Maximum = 1, Value = _imgProgress, IsIndeterminate = _imgBusy && _imgProgress >= 0.65, Height = 8 });
            sp.Children.Add(new TextBlock { Name = "wiz-img-status", Text = _imgStatus, Foreground = _imgOk is false ? Brush.Parse("#F87171") : Text, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        }
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = {
            Act("wiz-img-back", "Zurück", () => { if (!_imgBusy) Show("einrichtung-start"); }), write } });
        return sp;
    }
}
