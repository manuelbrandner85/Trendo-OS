using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-06: Ordner im Heimnetz teilen – nur lesen, geheimer Link, läuft ab.</summary>
public partial class MainWindow
{
    public ShareServer? ActiveShare { get; private set; }
    private string _shareMsg = "", _shareFolder = "";
    public IPAddress? ShareAddressOverride { get; set; }
    /// <summary>Fragt den Menschen, ob der Ordner geteilt werden darf. Standard: deutscher Dialog („Ablehnen“ ist Standard, Esc = Ablehnen).
    /// Tests setzen hier eine eigene Antwort ein.</summary>
    internal Func<string, TimeSpan, Task<bool>>? ShareConfirm { get; set; }
    /// <summary>Die zuletzt angefragte Freigabe (für Tests und Anzeige).</summary>
    public ApprovalRequest? LastShareRequest { get; private set; }

    private void InitSys06()
    {
        _showMap["teilen"] = ShareView; SysTabs.Add(("teilen", "Teilen im Heimnetz"));
        StudioPages["SYS-06"] = "teilen";
        ExtraPages.Add(("Ordner im Heimnetz teilen", "teilen", ["teilen", "freigabe", "netzwerk", "handy", "uebertragen", "wlan"]));
        Closing += OnClosingStopShare;
    }

    private bool _closingAfterShareStop;

    /// <summary>Nicht im UI-Thread blockieren: Schließen anhalten, Freigabe sauber beenden, dann wirklich schließen.</summary>
    private async void OnClosingStopShare(object? sender, WindowClosingEventArgs e)
    {
        if (ActiveShare is null || _closingAfterShareStop) return;
        e.Cancel = true;
        _closingAfterShareStop = true;
        try { await StopShareAsync(); }
        catch (Exception ex) { _core.Audit.Append(Principal.LocalOwner.Name, "share.stop.error", ex.GetType().Name); }
        Close();
    }

    public async Task<bool> StartShareAsync(string folder, TimeSpan dur, IPAddress ip)
    {
        await StopShareAsync();
        var req = _core.Approvals.Request(Principal.LocalOwner, "share.start", $"Ordner {Path.GetFileName(folder)} im Heimnetz teilen (nur lesen, {dur.TotalHours:0} h)", folder);
        LastShareRequest = req;
        var ok = false;
        try { ok = await (ShareConfirm ?? ConfirmShareDialog)(folder, dur); }
        catch { ok = false; }
        if (!ok)
        {
            _core.Approvals.Deny(req.Id, Principal.LocalOwner);
            _shareMsg = "Freigabe abgelehnt";
            return false;
        }
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        ActiveShare = _core.Approvals.Execute(req.Id, "share.start", folder, () => new ShareServer(new FolderShare(folder, dur), ip, _core.Audit));
        _shareMsg = "Freigabe läuft. Öffne den Link auf dem Handy oder einem anderen PC im selben WLAN.";
        return true;
    }

    private async Task<bool> ConfirmShareDialog(string folder, TimeSpan dur)
    {
        var hours = dur.TotalHours;
        var dauer = hours == 1 ? "1 Stunde" : $"{hours:0} Stunden";
        var dlg = new Window
        {
            Title = "Ordner teilen?", Width = 460, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var allow = new Button { Name = "share-confirm-allow", Content = "Erlauben" };
        var deny = new Button { Name = "share-confirm-deny", Content = "Ablehnen", IsDefault = true, IsCancel = true };
        allow.Click += (_, _) => dlg.Close(true);
        deny.Click += (_, _) => dlg.Close(false);
        dlg.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; dlg.Close(false); } };
        dlg.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10, Children = {
            new TextBlock { Text = "Soll dieser Ordner im Heimnetz geteilt werden?", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Name = "share-confirm-text", Text = $"Ordner: {folder}\nDauer: {dauer}\nAndere Geräte im selben WLAN können die Dateien nur lesen.", TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { deny, allow } } } };
        dlg.Opened += (_, _) => deny.Focus();
        return await dlg.ShowDialog<bool>(this);
    }

    public async Task StopShareAsync()
    {
        if (ActiveShare is null) return;
        await ActiveShare.DisposeAsync(); _core.Audit.Append(Principal.LocalOwner.Name, "share.stop", $"downloads={ActiveShare.Share.Downloads}");
        ActiveShare = null; _shareMsg = "Freigabe beendet.";
    }

    private Control ShareView()
    {
        if (ActiveShare is { Share.Active: false }) { _ = StopShareAsync(); _shareMsg = "Die Freigabe ist abgelaufen und wurde beendet."; }
        var sp = new StackPanel { Spacing = 6, Children = { P("Teile einen Ordner mit Geräten in deinem WLAN – zum Beispiel, um Fotos aufs Handy zu holen. Andere können nur lesen, nichts ändern. Der Link läuft von allein ab.") } };
        var ips = ShareAddressOverride is { } o ? [("Test", o)] : ShareServer.LanAddresses();
        if (ActiveShare is { } s)
        {
            sp.Children.Add(WCard(new StackPanel { Spacing = 4, Children = {
                new TextBlock { Text = "Freigabe läuft", Foreground = Brush.Parse("#4ADE80"), FontWeight = FontWeight.SemiBold },
                new SelectableTextBlock { Name = "share-url", Text = s.Url, Foreground = Text, FontSize = 15 },
                P($"Ordner: {s.Share.Folder} · gültig bis {s.Share.Expires:HH:mm} Uhr · {s.Share.Downloads} Download(s)", null, 11) } }));
            var stop = new Button { Name = "share-stop", Content = "Freigabe beenden", Background = Accent };
            stop.Click += async (_, _) => { await StopShareAsync(); Show("teilen"); };
            sp.Children.Add(stop);
        }
        else if (ips.Count == 0) sp.Children.Add(P("Dieser PC ist gerade mit keinem Heimnetz verbunden. Verbinde ihn mit dem WLAN oder Netzwerkkabel.", Brush.Parse("#FACC15")));
        else
        {
            var folder = new TextBox { Name = "share-folder", PlaceholderText = "Ordner, z. B. C:\\Users\\…\\Pictures", Text = _shareFolder, Width = 360 };
            var dur = new ComboBox { Name = "share-dur", ItemsSource = new[] { "1 Stunde", "4 Stunden", "24 Stunden" }, SelectedIndex = 0 };
            var net = new ComboBox { Name = "share-ip", ItemsSource = ips.Select(i => $"{i.Name} ({i.Ip})").ToList(), SelectedIndex = 0 };
            var go = new Button { Name = "share-start", Content = "Teilen starten", Background = Accent };
            go.Click += async (_, _) =>
            {
                _shareFolder = folder.Text?.Trim().Trim('"') ?? "";
                if (!Directory.Exists(_shareFolder)) { _shareMsg = "Diesen Ordner gibt es nicht."; Show("teilen"); return; }
                try { await StartShareAsync(_shareFolder, TimeSpan.FromHours(new[] { 1, 4, 24 }[Math.Max(0, dur.SelectedIndex)]), ips[Math.Max(0, net.SelectedIndex)].Ip); }
                catch (Exception ex) { _shareMsg = "Hat nicht geklappt: " + ex.Message; }
                Show("teilen");
            };
            sp.Children.Add(folder); sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { dur, net, go } });
            sp.Children.Add(P("Beim ersten Mal fragt Windows vielleicht, ob Trendo im privaten Netzwerk erreichbar sein darf. Wähle „Private Netzwerke“.", null, 11));
        }
        sp.Children.Add(new TextBlock { Name = "share-msg", Text = _shareMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        return SysPage("Teilen im Heimnetz", sp);
    }
}
