using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

public partial class MainWindow
{
    public WlanPairing? WlanSession { get; private set; }
    public bool PhoneAskAdb { get; private set; }
    public bool PhoneAskScrcpy { get; private set; }
    public string? ScrcpyOverride { get; set; }
    /// <summary>Tests: adb als fehlend melden, auch wenn es auf dem Rechner liegt.</summary>
    public Func<string?>? AdbLocator { get; set; }
    public Func<string?>? ScrcpyLocator { get; set; }
    public IBluetoothGateway? BluetoothOverride { get; set; }
    public IReadOnlyList<BtDevice> BtFound { get; private set; } = [];
    public string PhoneDetail { get; private set; } = "";
    public string PhoneFolder => WorkFolder("Vom Handy");
    private CancellationTokenSource? _btReceive;

    private IBluetoothGateway Bt() => BluetoothOverride ?? BluetoothLink.Create(Runner);

    public async Task ScanPhonesAsync()
    {
        PhoneDetail = "";
        var (ok, err) = await LoadPhonesAsync();
        if (!ok) { W7Say(err ?? "Handy-Suche fehlgeschlagen.", "handy"); return; }
        W7Say(Phones.Count == 0 ? "Kein Handy gefunden. Kabel, USB-Debugging oder WLAN prüfen." : Phones.Count == 1 ? "1 Handy gefunden." : $"{Phones.Count} Handys gefunden.", "handy");
    }

    public async Task<(bool Ok, string? Error)> LoadPhonesAsync()
    {
        var adb = await ResolveAdbAsync(true);
        if (adb is null) { Phones = []; return (false, "Android-Werkzeug (adb) fehlt. Trendo installiert es nur, wenn du zustimmst."); }
        PhoneAskAdb = false;
        var (e, o) = await Runner.RunAsync(adb, "devices -l");
        Phones = e == 0 ? MobileBridge.ParseDevices(o) : [];
        return e == 0 ? (true, null) : (false, $"adb meldet Fehler (Exit {e}).");
    }

    public async Task PushToPhoneAsync(PhoneDevice d, string file, bool consent)
    {
        var (ok, args, m) = MobileBridge.PushArgs(d, file, consent);
        if (!ok) { W7Say(m, "handy"); return; }
        var adb = await ResolveAdbAsync(true);
        if (adb is null) { W7Say("Android-Werkzeug (adb) fehlt. Trendo installiert es nur, wenn du zustimmst.", "handy"); return; }
        var (e, _) = await Runner.RunAsync(adb, args);
        _core.Audit.Append("local-owner", "phone.push", $"{System.IO.Path.GetFileName(file)} sha={LetterKit.Sha(file)[..16]} {d.Link} exit={e}");
        W7Say(e == 0 ? m + " ✓" : $"Übertragung fehlgeschlagen (Exit {e}).", "handy");
    }

    private string AdbPath() => AdbOverride ?? MobileBridge.FindAdb(File.Exists, Environment.GetEnvironmentVariable("LOCALAPPDATA"), Environment.GetEnvironmentVariable("PATH")) ?? "adb";

    public async Task PhoneInfoAsync(PhoneDevice d)
    {
        var i = await AndroidSide.InfoAsync(Runner, AdbPath(), d.Serial);
        W7Say(i is null ? "Handy antwortet nicht." : $"{i.Model} · Android {i.Android}" + (i.Battery is { } b ? $" · Akku {b} %{(i.Charging ? " (lädt)" : "")}" : "") + $" · {MobileBridge.LinkText(d.Link)}", "handy");
    }

    public async Task PhoneShotAsync(PhoneDevice d, bool consent)
    {
        var (ok, m, f) = await AndroidSide.ScreenshotAsync(Runner, AdbPath(), d.Serial, PhoneFolder, consent, DateTime.Now);
        if (ok) _core.Audit.Append("local-owner", "phone.screenshot", $"{d.Link} sha={LetterKit.Sha(f!)[..16]}");
        W7Say(m, "handy");
    }

    public async Task PhonePhotoAsync(PhoneDevice d, bool consent)
    {
        var (ok, m, f) = await AndroidSide.LatestPhotoAsync(Runner, AdbPath(), d.Serial, PhoneFolder, consent);
        if (ok) _core.Audit.Append("local-owner", "phone.photo", $"{System.IO.Path.GetFileName(f)} {d.Link} sha={LetterKit.Sha(f!)[..16]}");
        W7Say(m, "handy");
    }

    public async Task PhoneMirrorAsync(PhoneDevice d, bool consent)
    {
        var (ok, args, m) = PhoneActions.MirrorArgs(d.Serial, consent);
        if (!ok) { W7Say(m, "handy"); return; }
        var scrcpy = await ResolveScrcpyAsync(true);
        if (scrcpy is null) { W7Say("scrcpy fehlt. Damit zeigen wir den Handy-Bildschirm. Trendo installiert es nur, wenn du zustimmst.", "handy"); return; }
        PhoneAskScrcpy = false;
        W7Say("Bildschirm wird gezeigt. Das Fenster schließen beendet die Ansicht. Das geht per Kabel und per WLAN.", "handy");
        var (e, o) = await RunScrcpyAsync(scrcpy, args, AdbPath());
        _core.Audit.Append("local-owner", "phone.mirror", $"{d.Link} exit={e}");
        W7Say(e == 0 ? "Ansicht beendet." : "Bildschirm zeigen fehlgeschlagen. " + PhoneActions.FirstLine(o), "handy");
    }

    public async Task PhoneAppsAsync(PhoneDevice d, bool consent)
    {
        var (ok, args, m) = PhoneActions.PackageArgs(d.Serial, consent);
        if (!ok) { W7Say(m, "handy"); return; }
        var (e, o) = await Runner.RunAsync(AdbPath(), args);
        var apps = e == 0 ? PhoneActions.ParsePackages(o) : [];
        PhoneDetail = apps.Count == 0 ? "Keine selbst installierten Apps gemeldet." : "Apps:\n" + string.Join("\n", apps);
        _core.Audit.Append("local-owner", "phone.apps", $"{d.Link} {apps.Count}");
        W7Say(e == 0 ? m : "Apps konnten nicht gelesen werden.", "handy");
    }

    public async Task PhoneNotesAsync(PhoneDevice d, bool consent)
    {
        var (ok, args, _) = PhoneActions.NoticeArgs(d.Serial, consent);
        if (!ok) { W7Say("Erst zustimmen.", "handy"); return; }
        var (e, o) = await Runner.RunAsync(AdbPath(), args);
        var notes = e == 0 ? PhoneActions.ParseNotifications(o) : [];
        PhoneDetail = notes.Count == 0 ? "Keine Überschriften gemeldet." : "Benachrichtigungen (nur die Überschrift):\n" + string.Join("\n", notes.Select(n => $"{n.App}: {n.Title}"));
        _core.Audit.Append("local-owner", "phone.notes", $"{d.Link} {notes.Count}");
        W7Say(e == 0 ? "Überschriften gelesen. Der Text der Meldungen bleibt auf dem Handy." : "Benachrichtigungen konnten nicht gelesen werden.", "handy");
    }

    public void PrepareWlan(bool consent)
    {
        if (!consent) { W7Say("Erst zustimmen. Setze den Haken und tippe dann auf „Per WLAN verbinden“.", "handy"); return; }
        WlanSession = WirelessAdb.CreateSession();
        W7Say("QR-Code ist bereit. Am Handy „Gerät mit QR-Code koppeln“ öffnen und den Code scannen – Trendo koppelt dann von selbst. Ein neuer Code ersetzt den vorigen.", "handy");
        _ = AutoPairWlanAsync(WlanSession);
    }

    /// <summary>Vorher musste man nach dem Scannen „Kopplung abschließen“ drücken; bis dahin hatte das Handy oft schon aufgegeben.
    /// Jetzt sucht Trendo jede Sekunde (bis 3 Minuten) nach dem Handy und koppelt automatisch.</summary>
    async Task AutoPairWlanAsync(WlanPairing session)
    {
        var adb = await ResolveAdbAsync(true);
        if (adb is null) return;
        var end = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < end && ReferenceEquals(WlanSession, session))
        {
            var (e, o) = await Runner.RunAsync(adb, "mdns services");
            if (e == 0 && !_wlanBusy && WirelessAdb.ParseMdns(o).Any(s => s.Type == WirelessAdb.PairingType && s.Name == session.ServiceName))
            {
                await FinishWlanAsync(true);
                return;
            }
            await Task.Delay(1000);
        }
        if (ReferenceEquals(WlanSession, session))
            W7Say("Das Handy hat sich nicht gemeldet. Gleiches WLAN? Am Handy „Gerät mit QR-Code koppeln“ öffnen und den neuen Code scannen.", "handy");
    }

    public async Task FinishWlanAsync(bool consent)
    {
        if (!consent) { W7Say("Erst zustimmen.", "handy"); return; }
        if (WlanSession is null) { W7Say("Zuerst „Per WLAN verbinden“ tippen, damit der QR-Code da ist.", "handy"); return; }
        if (_wlanBusy) return; // automatische Suche und Klick nicht doppelt
        _wlanBusy = true;
        try { await FinishWlanCoreAsync(); } finally { _wlanBusy = false; }
    }

    bool _wlanBusy;

    async Task FinishWlanCoreAsync()
    {
        var session = WlanSession!;
        var adb = await ResolveAdbAsync(true);
        if (adb is null) { W7Say("Android-Werkzeug (adb) fehlt. Trendo installiert es nur, wenn du zustimmst.", "handy"); return; }
        MdnsService? pairing = null;
        var mdnsExit = 0;
        var mdnsOut = "";
        for (var i = 0; i < 4 && pairing is null; i++)
        {
            (mdnsExit, mdnsOut) = await Runner.RunAsync(adb, "mdns services");
            pairing = WirelessAdb.ParseMdns(mdnsOut).FirstOrDefault(s => s.Type == WirelessAdb.PairingType && s.Name == session.ServiceName);
            if (pairing is null && i < 3 && mdnsExit == 0) await Task.Delay(300);
        }
        if (pairing is null)
        {
            W7Say(mdnsExit != 0
                ? "Suche im WLAN fehlgeschlagen. adb braucht eine mDNS-Erkennung (Bonjour oder Avahi)."
                : "Das Handy hat den Code noch nicht angenommen. Am Handy „Gerät mit QR-Code koppeln“ öffnen, scannen, dann hier noch einmal „Kopplung abschließen“.", "handy");
            return;
        }
        var (ok, args, denied) = WirelessAdb.PairArgs(pairing.Host, pairing.Port, session.Password, true);
        if (!ok) { W7Say(denied, "handy"); return; }
        var (pe, po) = await Runner.RunAsync(adb, args);
        _core.Audit.Append("local-owner", "phone.wlan.pair", pairing.Host);
        if (!WirelessAdb.PairOk(pe, po)) { W7Say("Kopplung abgelehnt. Der Code gilt nur kurz. „Per WLAN verbinden“ macht einen neuen.", "handy"); return; }
        var connect = WirelessAdb.ParseMdns(mdnsOut).FirstOrDefault(s => s.Type == WirelessAdb.ConnectType && s.Host == pairing.Host);
        if (connect is null)
        {
            var (e2, o2) = await Runner.RunAsync(adb, "mdns services");
            connect = WirelessAdb.ParseMdns(o2).FirstOrDefault(s => s.Type == WirelessAdb.ConnectType && s.Host == pairing.Host);
            if (e2 != 0 && connect is null) { W7Say("Gekoppelt, aber der Verbindungsport fehlt. Trag ihn unter „Verbindungsport“ ein.", "handy"); return; }
        }
        if (connect is null) { W7Say("Gekoppelt, aber das Handy meldet noch keinen Verbindungsport. Kurz warten und „Mit Code koppeln“ ist hier der falsche Weg – tippe noch einmal auf „Kopplung abschließen“.", "handy"); return; }
        await ConnectWlanAsync(adb, connect.Host, connect.Port, WirelessAdb.WifiGuid(connect.Name), true);
        WlanSession = null;
    }

    public async Task PairWithCodeAsync(string host, string pairPort, string code, string connectPort, bool consent)
    {
        if (!consent) { W7Say("Erst zustimmen.", "handy"); return; }
        host = host.Trim();
        if (!int.TryParse(pairPort.Trim(), out var pp)) { W7Say("Kopplungsport ungültig.", "handy"); return; }
        var (ok, args, msg) = WirelessAdb.PairArgs(host, pp, code.Trim(), true);
        if (!ok) { W7Say(msg, "handy"); return; }
        var adb = await ResolveAdbAsync(true);
        if (adb is null) { W7Say("Android-Werkzeug (adb) fehlt. Trendo installiert es nur, wenn du zustimmst.", "handy"); return; }
        var (e, o) = await Runner.RunAsync(adb, args);
        _core.Audit.Append("local-owner", "phone.wlan.pair", host);
        if (!WirelessAdb.PairOk(e, o)) { W7Say("Kopplung abgelehnt. Stimmt der Code? Die Kopplung am Handy gilt nur kurz.", "handy"); return; }
        int cp;
        if (!int.TryParse(connectPort.Trim(), out cp))
        {
            var (_, mo) = await Runner.RunAsync(adb, "mdns services");
            var hit = WirelessAdb.ParseMdns(mo).FirstOrDefault(s => s.Type == WirelessAdb.ConnectType && s.Host == host)
                ?? WirelessAdb.ParseMdns(mo).FirstOrDefault(s => s.Type == WirelessAdb.LegacyType && s.Host == host);
            if (hit is null) { W7Say("Gekoppelt. Der Verbindungsport steht am Handy unter „Drahtloses Debuggen“. Trag ihn ein.", "handy"); return; }
            cp = hit.Port;
        }
        await ConnectWlanAsync(adb, host, cp, null, true);
    }

    public async Task ReconnectPhoneAsync(string id, bool consent)
    {
        if (!consent) { W7Say("Erst zustimmen. Setze den Haken bei „Wieder verbinden“.", "handy"); return; }
        var phone = new KnownPhones(_core.DataRoot).All().FirstOrDefault(p => p.Id == id);
        if (phone is null) { W7Say("Dieses Handy ist nicht mehr gemerkt.", "handy"); return; }
        var adb = await ResolveAdbAsync(true);
        if (adb is null) { W7Say("Android-Werkzeug (adb) fehlt. Trendo installiert es nur, wenn du zustimmst.", "handy"); return; }
        var (_, o) = await Runner.RunAsync(adb, "mdns services");
        var (ok, host, port, msg) = WirelessAdb.PickReconnect(phone, WirelessAdb.ParseMdns(o), true);
        if (!ok) { W7Say(msg, "handy"); return; }
        _core.Audit.Append("local-owner", "phone.wlan.reconnect", host);
        await ConnectWlanAsync(adb, host, port, phone.Guid, true);
    }

    private async Task ConnectWlanAsync(string adb, string host, int port, string? guid, bool consent)
    {
        var (ok, args, msg) = WirelessAdb.ConnectArgs(host, port, consent);
        if (!ok) { W7Say(msg, "handy"); return; }
        var (e, o) = await Runner.RunAsync(adb, args);
        if (!WirelessAdb.ConnectOk(e, o)) { W7Say("Verbinden fehlgeschlagen. Ist „Drahtloses Debuggen“ noch an?", "handy"); return; }
        var (loaded, _) = await LoadPhonesAsync();
        var model = Phones.FirstOrDefault(p => p.Link == PhoneLink.Wlan)?.Model ?? "Android-Handy";
        new KnownPhones(_core.DataRoot).Remember(model, host, port, guid, DateTime.Now);
        _core.Audit.Append("local-owner", "phone.wlan.connect", $"{host}:{port}");
        W7Say(loaded ? $"Verbunden per WLAN ({host})." : $"Verbunden per WLAN ({host}). Die Geräteliste konnte nicht gelesen werden.", "handy");
    }

    public async Task InstallPhoneToolAsync(string id)
    {
        var comp = DependencyCatalog.Get(id);
        var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", $"{comp.Name} installieren", comp.Id);
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        W7Say($"{comp.Name} wird installiert …", "handy");
        var ok = await _deps.InstallAsync(Principal.LocalOwner, comp);
        if (id == "adb") PhoneAskAdb = !ok;
        if (id == "scrcpy") PhoneAskScrcpy = !ok;
        W7Say(ok
            ? $"{comp.Name} ist installiert."
            : $"{comp.Name} konnte nicht installiert werden. Unter Linux kannst du das Paket „{id}“ aus den Paketquellen holen. Trendo ändert keine Treiber.", "handy");
    }

    public void LaterPhoneTool(string id)
    {
        if (id == "adb") PhoneAskAdb = false;
        if (id == "scrcpy") PhoneAskScrcpy = false;
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.declined", id);
        W7Say(id == "adb" ? "adb bleibt ungeinstalliert. Ohne adb kein Kabel und kein WLAN." : "scrcpy bleibt ungeinstalliert. Der Bildschirm bleibt aus.", "handy");
    }

    public async Task SearchBluetoothAsync()
    {
        W7Say("Suche Bluetooth-Geräte …", "handy");
        var scan = await Bt().SearchAsync();
        BtFound = scan.Devices;
        W7Say(scan.Message, "handy");
    }

    public async Task PairBluetoothAsync(string id, bool consent)
    {
        if (!consent) { W7Say("Erst zustimmen. Setze den Haken und tippe dann auf „Koppeln“.", "handy"); return; }
        var (ok, m) = await Bt().PairAsync(id, true);
        if (ok) _core.Audit.Append("local-owner", "phone.bt.pair", id);
        W7Say(m, "handy");
    }

    public async Task SendBluetoothAsync(string id, string file, bool consent)
    {
        if (!consent) { W7Say("Erst zustimmen.", "handy"); return; }
        var (ok, m) = await Bt().SendAsync(id, file, true);
        if (ok) _core.Audit.Append("local-owner", "phone.bt.send", System.IO.Path.GetFileName(file));
        W7Say(m, "handy");
    }

    public async Task ReceiveBluetoothAsync(bool consent)
    {
        if (!consent) { W7Say("Erst zustimmen. Setze den Haken und tippe dann auf „Datei empfangen“.", "handy"); return; }
        StopBluetoothReceive();
        _btReceive = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        W7Say("Empfang ist 45 Sekunden an. Schick jetzt die Datei vom Handy. Danach endet der Empfang von allein.", "handy");
        try
        {
            var (ok, m) = await Bt().ReceiveAsync(PhoneFolder, true, _btReceive.Token);
            if (ok) _core.Audit.Append("local-owner", "phone.bt.receive", "ok");
            W7Say(m, "handy");
        }
        catch (OperationCanceledException)
        {
            W7Say("Empfang beendet. Es bleibt kein Empfang im Hintergrund.", "handy");
        }
    }

    public async Task BatteryBluetoothAsync(string id, bool consent)
    {
        if (!consent) { W7Say("Erst zustimmen.", "handy"); return; }
        var (level, m) = await Bt().BatteryAsync(id, true);
        var devs = BtFound.ToList();
        var i = devs.FindIndex(d => d.Id == id);
        if (i >= 0 && level is { } n) devs[i] = devs[i] with { BatteryPercent = n };
        BtFound = devs;
        W7Say(m, "handy");
    }

    private void StopBluetoothReceive()
    {
        try { _btReceive?.Cancel(); } catch (ObjectDisposedException) { }
    }

    private async Task<string?> ResolveAdbAsync(bool ask)
    {
        if (AdbOverride is not null) return AdbOverride;
        if (AdbLocator is not null)
        {
            var forced = AdbLocator();
            if (forced is null && ask) PhoneAskAdb = true;
            return forced;
        }
        var found = MobileBridge.FindAdb(File.Exists, Environment.GetEnvironmentVariable("LOCALAPPDATA"), Environment.GetEnvironmentVariable("PATH"));
        if (found is not null) return found;
        var check = await _deps.CheckAsync(DependencyCatalog.Get("adb"));
        if (check.Ok && check.Path is { } p) return p;
        if (ask) PhoneAskAdb = true;
        return null;
    }

    private async Task<string?> ResolveScrcpyAsync(bool ask)
    {
        if (ScrcpyOverride is not null) return ScrcpyOverride;
        if (ScrcpyLocator is not null)
        {
            var forced = ScrcpyLocator();
            if (forced is null && ask) PhoneAskScrcpy = true;
            return forced;
        }
        var found = MobileBridge.FindScrcpy(File.Exists, Environment.GetEnvironmentVariable("PATH"));
        if (found is not null) return found;
        var check = await _deps.CheckAsync(DependencyCatalog.Get("scrcpy"));
        if (check.Ok && check.Path is { } p) return p;
        if (ask) PhoneAskScrcpy = true;
        return null;
    }

    private async Task<(int Exit, string Output)> RunScrcpyAsync(string file, string args, string? adb)
    {
        if (Runner is not RealProcessRunner) return await Runner.RunAsync(file, args);
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = System.IO.Path.GetTempPath(),
        };
        if (!string.IsNullOrEmpty(adb)) psi.Environment["ADB"] = adb;
        using var p = Process.Start(psi)!;
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync();
        return (p.ExitCode, sb.ToString());
    }

    private Control PhoneView()
    {
        var sp = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                PhoneEasyCard(),
                H("Profi: Bildschirm und Apps steuern (Android, Entwicklermodus)", 15),
                P("Für Bildschirm spiegeln, Apps und Benachrichtigungen: Android per Kabel oder „Drahtloses Debuggen“. Dateien, Bildschirm, Apps und Benachrichtigungen gehen nur, wenn du es hier erlaubst. Nichts startet von allein."),
            },
        };
        W7Msgs(sp);
        if (PhoneDetail.Length > 0) sp.Children.Add(WCard(new TextBlock { Name = "phone-detail", Text = PhoneDetail, Foreground = Text, TextWrapping = TextWrapping.Wrap }));
        ToolAsk(sp, "adb");
        ToolAsk(sp, "scrcpy");
        sp.Children.Add(Act("phone-scan", "Handy suchen", () => _ = ScanPhonesAsync(), true));
        var i = 0;
        foreach (var d in Phones)
        {
            var dev = d;
            var file = new TextBox { Name = $"phone-file-{i}", PlaceholderText = "Datei, z. B. Brief als PDF", Width = 280 };
            var ok = new CheckBox { Name = $"phone-ok-{i}", Content = "Ich erlaube diesen Zugriff" };
            bool Yes() => ok.IsChecked == true;
            sp.Children.Add(WCard(new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Name = $"phone-dev-{i}", Text = $"📱 {d.Model} – {MobileBridge.StateText(d.State, d.Link)} · {MobileBridge.LinkText(d.Link)}", Foreground = Text, TextWrapping = TextWrapping.Wrap },
                    new WrapPanel { Children = { file, ok, Act($"phone-push-{i}", "Senden", () => _ = PushToPhoneAsync(dev, file.Text?.Trim('"', ' ') ?? "", Yes())) } },
                    new WrapPanel
                    {
                        Children =
                        {
                            Act($"phone-info-{i}", "Details", () => _ = PhoneInfoAsync(dev)),
                            Act($"phone-shot-{i}", "Bildschirmfoto holen", () => _ = PhoneShotAsync(dev, Yes())),
                            Act($"phone-photo-{i}", "Neuestes Foto holen", () => _ = PhonePhotoAsync(dev, Yes())),
                            Act($"phone-mirror-{i}", "Bildschirm zeigen", () => _ = PhoneMirrorAsync(dev, Yes())),
                            Act($"phone-apps-{i}", "Apps anzeigen", () => _ = PhoneAppsAsync(dev, Yes())),
                            Act($"phone-notes-{i}", "Benachrichtigungen", () => _ = PhoneNotesAsync(dev, Yes())),
                        },
                    },
                },
            }));
            i++;
        }

        var wlan = new StackPanel { Spacing = 6 };
        wlan.Children.Add(new TextBlock { Text = "Per WLAN verbinden", Foreground = Text, FontSize = 15 });
        wlan.Children.Add(P("Android 11 oder neuer. Handy und PC im selben WLAN.", null, 11));
        var stepNo = 0;
        foreach (var step in WirelessAdb.PhoneSteps)
            wlan.Children.Add(new TextBlock { Name = $"phone-step-{stepNo++}", Text = stepNo + ". " + step, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        var wlanOk = new CheckBox { Name = "phone-wlan-ok", Content = "Ich will dieses Handy per WLAN verbinden" };
        wlan.Children.Add(wlanOk);
        wlan.Children.Add(new WrapPanel
        {
            Children =
            {
                Act("phone-wlan", "Per WLAN verbinden", () => PrepareWlan(wlanOk.IsChecked == true), true),
                Act("phone-wlan-finish", "Kopplung abschließen", () => _ = FinishWlanAsync(wlanOk.IsChecked == true)),
            },
        });
        if (WlanSession is { } session)
        {
            wlan.Children.Add(new TextBlock { Name = "phone-wlan-ready", Text = "QR-Code ist bereit. Am Handy jetzt scannen.", Foreground = Text, TextWrapping = TextWrapping.Wrap });
            wlan.Children.Add(QrView(session.QrPayload));
        }
        var host = new TextBox { Name = "phone-wlan-host", PlaceholderText = "IP, z. B. 192.168.1.20", Width = 180 };
        var pairPort = new TextBox { Name = "phone-wlan-pairport", PlaceholderText = "Kopplungsport", Width = 110 };
        var code = new TextBox { Name = "phone-wlan-code", PlaceholderText = "Code, 6 Stellen", Width = 120 };
        var connPort = new TextBox { Name = "phone-wlan-port", PlaceholderText = "Verbindungsport", Width = 120 };
        wlan.Children.Add(P("Oder den Zahlencode vom Handy eintragen. Den Verbindungsport zeigt „Drahtloses Debuggen“, nicht das Kopplungsfenster.", null, 11));
        wlan.Children.Add(new WrapPanel { Children = { host, pairPort, code, connPort, Act("phone-wlan-code-go", "Mit Code koppeln", () => _ = PairWithCodeAsync(host.Text ?? "", pairPort.Text ?? "", code.Text ?? "", connPort.Text ?? "", wlanOk.IsChecked == true)) } });
        sp.Children.Add(WCard(wlan));

        var known = new KnownPhones(_core.DataRoot).All();
        if (known.Count > 0)
        {
            var box = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Bekannte WLAN-Handys", Foreground = Text } } };
            var k = 0;
            foreach (var phone in known)
            {
                var id = phone.Id;
                var again = new CheckBox { Name = $"phone-known-ok-{k}", Content = "Wieder verbinden" };
                box.Children.Add(new WrapPanel
                {
                    Children =
                    {
                        new TextBlock { Name = $"phone-known-{k}", Text = $"{phone.Model} · {phone.Host} · WLAN", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(0, 0, 8, 0) },
                        again,
                        Act($"phone-known-go-{k}", "Wieder verbinden", () => _ = ReconnectPhoneAsync(id, again.IsChecked == true)),
                    },
                });
                k++;
            }
            sp.Children.Add(WCard(box));
        }

        var bt = new StackPanel { Spacing = 6 };
        bt.Children.Add(new TextBlock { Text = "Per Bluetooth", Foreground = Text, FontSize = 15 });
        bt.Children.Add(new TextBlock { Name = "phone-bt-hint", Text = BluetoothLink.LimitHint, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        bt.Children.Add(Act("phone-bt-search", "Geräte suchen", () => _ = SearchBluetoothAsync(), true));
        var b = 0;
        foreach (var dev in BtFound)
        {
            var id = dev.Id;
            var ok = new CheckBox { Name = $"phone-bt-ok-{b}", Content = "Ich will dieses Gerät nutzen" };
            var file = new TextBox { Name = $"phone-bt-file-{b}", PlaceholderText = "Datei senden", Width = 220 };
            var bat = dev.BatteryPercent is { } pct ? $" · Akku {pct} %" : "";
            bt.Children.Add(new TextBlock { Name = $"phone-bt-{b}", Text = $"{dev.Name}{(dev.Paired ? " · gekoppelt" : "")}{bat}", Foreground = Text, TextWrapping = TextWrapping.Wrap });
            bt.Children.Add(new WrapPanel
            {
                Children =
                {
                    ok,
                    Act($"phone-bt-pair-{b}", "Koppeln", () => _ = PairBluetoothAsync(id, ok.IsChecked == true)),
                    Act($"phone-bt-bat-{b}", "Akkustand", () => _ = BatteryBluetoothAsync(id, ok.IsChecked == true)),
                    file,
                    Act($"phone-bt-send-{b}", "Datei senden", () => _ = SendBluetoothAsync(id, file.Text?.Trim('"', ' ') ?? "", ok.IsChecked == true)),
                },
            });
            b++;
        }
        var recvOk = new CheckBox { Name = "phone-bt-recv-ok", Content = "Ich will eine Datei empfangen" };
        bt.Children.Add(new WrapPanel { Children = { recvOk, Act("phone-bt-recv", "Datei empfangen", () => _ = ReceiveBluetoothAsync(recvOk.IsChecked == true)) } });
        bt.Children.Add(P("Unter Windows öffnet sich für Dateien der Bluetooth-Dialog. Unter Linux geht die Datei direkt an das gewählte Gerät, wenn obexftp da ist.", null, 11));
        sp.Children.Add(WCard(bt));

        var g = new StackPanel { Spacing = 2 };
        foreach (var s in MobileBridge.Guide) g.Children.Add(P("• " + s, null, 11.5));
        sp.Children.Add(WCard(g));
        return SysPage("Handy", sp);
    }

    private void ToolAsk(StackPanel sp, string id)
    {
        if (id == "adb" && !PhoneAskAdb) return;
        if (id == "scrcpy" && !PhoneAskScrcpy) return;
        var comp = DependencyCatalog.Get(id);
        sp.Children.Add(WCard(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = comp.Name + " fehlt", Foreground = Text },
                P(comp.Name + " – Lizenz " + comp.License + ". " + comp.Reason, null, 11),
                new WrapPanel
                {
                    Children =
                    {
                        Act($"phone-{id}-yes", "Ja, einrichten", () => _ = InstallPhoneToolAsync(id), true),
                        Act($"phone-{id}-later", "Später", () => LaterPhoneTool(id)),
                    },
                },
            },
        }));
    }

    private static Control QrView(string payload)
    {
        var matrix = QrMatrix.Encode(payload);
        var n = matrix.GetLength(0);
        const int quiet = 4;
        const int cell = 5;
        var side = (n + quiet * 2) * cell;
        var canvas = new Canvas { Name = "phone-qr", Width = side, Height = side, Background = Brushes.White };
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                if (matrix[y, x])
                {
                    var rect = new Rectangle { Width = cell, Height = cell, Fill = Brushes.Black };
                    Canvas.SetLeft(rect, (x + quiet) * cell);
                    Canvas.SetTop(rect, (y + quiet) * cell);
                    canvas.Children.Add(rect);
                }
        return new Border { Background = Brushes.White, Padding = new Avalonia.Thickness(8), Child = canvas, HorizontalAlignment = HorizontalAlignment.Left };
    }
}
