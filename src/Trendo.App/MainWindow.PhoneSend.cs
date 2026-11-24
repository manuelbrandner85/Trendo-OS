using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0767 (CORE-04/X-08): „Datei senden“ vom PC an ein bestimmtes gekoppeltes Handy, Entfernen mit sofortiger Sperre
/// und „Lauf aufzeichnen“ für den echten Handy-Lauf (anonymisiertes Protokoll mit Zeit, Größe und SHA-256).</summary>
public partial class MainWindow
{
    /// <summary>Protokoll des Handy-Laufs; schreibt nur, solange „Lauf aufzeichnen“ an ist.</summary>
    public PhoneRunLog PhoneRun { get; } = new();
    /// <summary>Größenlimit je Datei zwischen Handy und PC (Tests setzen es klein).</summary>
    public long PhoneMaxFileBytes { get; set; } = PhoneTransfer.DefaultMaxBytes;
    public string PhoneRunFolder => WorkFolder("Handy-Protokolle");
    /// <summary>Zuletzt gespeicherter Lauf-Bericht (Markdown).</summary>
    public string? PhoneRunReport { get; private set; }

    /// <summary>Handy entfernen: Schlüssel weg (der Handy-Zugang prüft ihn bei jeder Anfrage – damit sofort gesperrt),
    /// alles, was nur für dieses Handy bereitlag, wird gelöscht.</summary>
    public void PhoneRemoveDevice(string id, string name)
    {
        // Befund M2: Schlüssel-Hashes vor dem Löschen merken – nur eine Abweisung mit genau diesem alten Schlüssel belegt die Sperre
        var dev = PhoneAccess.Devices().FirstOrDefault(d => d.Id == id);
        PhoneAccess.Remove(id);
        try { PhoneTransfer.DropDevice(PhoneOutbox, id); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.removed", name);
        PhoneRun.RecordRemoved(id, dev is null ? [] : [dev.TokenHash, .. dev.More ?? []], note: "am PC entfernt, Schlüssel gelöscht");
        Toast($"{name} ist nicht mehr verbunden.");
        Show("handy");
    }

    DateTime _phonePurgedAt;

    /// <summary>Befund N4: Was länger als 24 Stunden für ein Handy bereitlag, verschwindet – beim Start des Handy-Zugangs und höchstens einmal je Minute.</summary>
    public void PhonePurgeOutbox(bool force = false)
    {
        if (!force && DateTime.UtcNow - _phonePurgedAt < TimeSpan.FromMinutes(1)) return;
        _phonePurgedAt = DateTime.UtcNow;
        try { PhoneTransfer.PurgeExpired(PhoneOutbox, DateTime.UtcNow); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Befund M3: ehrlich sagen, dass das Heimnetz ohne Verschlüsselung läuft.</summary>
    Control PhoneHomeNote() => new TextBlock
    {
        Name = "phone-home-note", TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Foreground = Brush.Parse("#F5C451"),
        Text = "Zuhause läuft die Verbindung unverschlüsselt durchs WLAN – nur im eigenen, passwortgeschützten WLAN nutzen. Unterwegs (Tailscale) ist sie verschlüsselt.",
    };

    async Task PhoneSendPickAsync(string id, string name)
    {
        var req = new PickRequest("phone-send", false, true, []);
        var files = PickOverride is { } fake ? await fake(req)
            : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = $"Datei an „{name}“ senden", AllowMultiple = true }))
                .Select(x => x.TryGetLocalPath()).OfType<string>().ToArray();
        if (files is null || files.Length == 0) return;
        foreach (var f in files) await PhoneSendFileAsync(id, f);
        Show("handy");
    }

    /// <summary>Legt eine Kopie für genau dieses Handy bereit (24 Stunden). Startet den Handy-Zugang, falls er noch aus ist.</summary>
    public async Task<bool> PhoneSendFileAsync(string id, string path)
    {
        if (!PhoneAccess.Devices().Any(d => d.Id == id)) { Toast("Dieses Handy ist nicht mehr verbunden."); return false; }
        try
        {
            var got = await PhoneTransfer.OfferAsync(path, PhoneOutbox, id, PhoneMaxFileBytes);
            _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.offerfile", $"{Path.GetFileName(got.Path)} ({got.Bytes} B, sha256 {got.Sha256[..16]})");
            PhoneRun.Record(PhoneRunLog.Steps.Provided, id, null, bytes: got.Bytes, sha: got.Sha256, note: "liegt 24 Stunden nur für dieses Handy bereit");
            await EnsurePhoneServerAsync();
            Toast($"„{Path.GetFileName(got.Path)}“ liegt für das Handy bereit. Auf dem Handy unter „Vom PC für dich“ antippen.");
            return true;
        }
        catch (PhoneTransfer.TooLargeException e) { Toast(e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Toast("Senden ging nicht: " + FriendlyError.Say(e)); }
        return false;
    }

    public void PhoneRunStart()
    {
        PhoneRun.Start();
        PhoneRunReport = null;
        _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.run.start", "Lauf-Aufzeichnung gestartet");
        Show("handy");
    }

    public string PhoneRunStop()
    {
        var (md, _, _) = PhoneRun.Stop(PhoneRunFolder, CrashLog.AppVersion());
        PhoneRunReport = md;
        _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.run.stop", Path.GetFileName(md));
        Toast("Lauf-Protokoll gespeichert (ohne Gerätenamen und Adressen).", ("Ordner öffnen", new Action(() => Launch(PhoneRunFolder))));
        Show("handy");
        return md;
    }

    Control PhoneRunCard()
    {
        var sp = new StackPanel { Name = "phone-run", Spacing = 6 };
        sp.Children.Add(H("Lauf aufzeichnen (Nachweis)", 14));
        if (!PhoneRun.Active)
        {
            sp.Children.Add(P("Schreibt mit: Koppeln, Dateien hin und her, Entfernen – mit Zeit, Größe und Prüfsumme (SHA-256).", Text, 12.5));
            sp.Children.Add(P("Ohne Gerätenamen, Adressen und Dateinamen. Bleibt auf diesem PC im Ordner „Handy-Protokolle“.", Muted, 12));
            var start = new Button { Name = "phone-run-start", Content = "Lauf aufzeichnen", Padding = new Thickness(10, 4) };
            start.Click += (_, _) => PhoneRunStart();
            var row = new WrapPanel { Children = { Sp(start) } };
            if (PhoneRunReport is { } rep)
            {
                var open = new Button { Name = "phone-run-folder", Content = "Protokolle öffnen", Padding = new Thickness(10, 4), Background = Brushes.Transparent };
                open.Click += (_, _) => Launch(PhoneRunFolder);
                row.Children.Add(Sp(open));
                sp.Children.Add(P("Zuletzt gespeichert: " + Path.GetFileName(rep), Muted, 12));
            }
            sp.Children.Add(row);
        }
        else
        {
            var n = PhoneRun.Entries.Count;
            sp.Children.Add(P($"● Aufzeichnung läuft seit {PhoneRun.Started.ToLocalTime():HH:mm:ss} · {n} Schritt(e)", Brush.Parse("#F5C451"), 12.5));
            sp.Children.Add(P("Der Reihe nach: koppeln · Datei vom Handy schicken · „Datei senden …“ und am Handy laden · Handy entfernen · am Handy „Aktualisieren“.", Muted, 12));
            foreach (var c in PhoneRunLog.Evaluate(PhoneRun.Entries))
                sp.Children.Add(new TextBlock { Text = (c.Ok ? "✓ " : "○ ") + c.Name, Foreground = c.Ok ? Brush.Parse("#4ADE80") : Muted, FontSize = 12.5 });
            var stop = new Button { Name = "phone-run-stop", Content = "Aufzeichnung beenden und speichern", Padding = new Thickness(10, 4) };
            stop.Click += (_, _) => PhoneRunStop();
            sp.Children.Add(new WrapPanel { Children = { Sp(stop) } });
        }
        sp.Children.Add(PhoneCableStatus());
        return WCard(sp);
    }

    bool? _adbFound;
    bool _adbProbing;

    /// <summary>Kabel-Weg ehrlich anzeigen: nur, wenn das Android-Werkzeug (adb) schon auf dem PC liegt. Trendo installiert hier nichts.
    /// Die Prüfung läuft im Hintergrund und ändert danach nur diese eine Zeile (kein Neuaufbau der Seite, sonst verliert man Eingaben).</summary>
    Control PhoneCableStatus()
    {
        var tb = new TextBlock { Name = "phone-cable-status", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        void Fill()
        {
            tb.Text = _adbFound switch
            {
                null => "Kabel-Weg: wird geprüft …",
                true => "✓ Kabel-Weg möglich: Das Android-Werkzeug (adb) ist auf diesem PC. Handy per USB anschließen, am Handy „USB-Debugging“ erlauben, dann unten unter „Profi“ „Handy suchen“.",
                false => "Kabel-Weg nicht verfügbar: Das Android-Werkzeug (adb) fehlt auf diesem PC. Dateien gehen trotzdem – über das WLAN mit „Datei senden …“ und der Handy-Seite.",
            };
            tb.Foreground = _adbFound == true ? Brush.Parse("#4ADE80") : Muted;
        }
        Fill();
        if (_adbFound is null && !_adbProbing)
        {
            _adbProbing = true;
            _ = Task.Run(async () =>
            {
                string? p = null;
                try { p = await ResolveAdbAsync(false); } catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                _ui.Post(() => { _adbFound = p is not null; _adbProbing = false; _cableStatus?.Invoke(); });
            });
        }
        _cableStatus = Fill;
        return tb;
    }
    Action? _cableStatus;
}
