using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026 (S1–S5): WhatsApp, Telegram, TikTok, Instagram und Facebook als Apps in Trendo.
/// Installieren legt die App an (Startmenü, Desktop), Öffnen startet die offizielle Webversion im eigenen App-Fenster,
/// Entfernen löscht auch den eigenen Anmeldespeicher. Telegram gibt es zusätzlich als offizielles Windows-Programm.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: App-Fenster starten (Browser, Argumente) statt echtem Browser.</summary>
    public Action<string, IReadOnlyList<string>>? WebAppLaunchOverride { get; set; }
    /// <summary>Für Tests: gefundene Browser vorgeben.</summary>
    public Func<IReadOnlyList<BrowserInfo>>? BrowsersOverride { get; set; }
    /// <summary>Für Tests: Telegram-Installer ausführen statt echtem Start.</summary>
    public Func<string, Task>? TelegramInstallerOverride { get; set; }
    string _webAppMsg = "";

    public bool WebAppInstalled(string id) => WebApps.IsInstalled(_core.DataRoot, id);

    public void InstallWebApp(string id)
    {
        WebApps.Install(_core.DataRoot, id, _core.Audit, Principal.LocalOwner.Name);
        SetOnDesktop("app:" + id, true);
        _hubMsg = $"{WebApps.Get(id)!.Name} ist installiert – im Startmenü und auf dem Desktop.";
    }

    public void RemoveWebApp(string id)
    {
        WebApps.Uninstall(_core.DataRoot, id, _core.Audit, Principal.LocalOwner.Name);
        SetOnDesktop("app:" + id, false);
        _hubMsg = $"{WebApps.Get(id)?.Name ?? id} wurde entfernt (auch die Anmeldung).";
    }

    /// <summary>App im eigenen Fenster öffnen. Nicht installiert → „Hinzufügen“ öffnen. Liefert eine Meldung bei Problemen.</summary>
    public bool OpenWebApp(string id)
    {
        if (WebApps.Get(id) is not { } app) return false;
        if (!WebAppInstalled(id)) { OpenAddHub("apps", app.Name); return false; }
        _webAppMsg = "";
        if (_core.Offline) { _webAppMsg = "Trendo ist offline – die App braucht Internet."; _hubMsg = _webAppMsg; return false; }
        var browser = WebApps.Browser(BrowsersOverride?.Invoke() ?? Browsers.Find());
        if (browser is null) { _hubMsg = _webAppMsg = "Für App-Fenster braucht es Microsoft Edge, Google Chrome oder Brave."; return false; }
        var args = WebApps.LaunchArgs(_core.DataRoot, app);
        _core.Audit.Append(Principal.LocalOwner.Name, "app.open", id);
        if (WebAppLaunchOverride is { } o) { o(browser.Path, args); return true; }
        try
        {
            var psi = new ProcessStartInfo(browser.Path) { UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { _hubMsg = _webAppMsg = $"{app.Name} ließ sich nicht öffnen: {e.Message}"; return false; }
    }

    /// <summary>Symbol einer App: Farbverlauf mit Zeichen (keine fremden Markenlogos).</summary>
    Control WebAppFace(WebApp a, double size) => new Border
    {
        Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse(a.C1), 0), new GradientStop(Color.Parse(a.C2), 1) } },
        Child = new TextBlock { Text = a.Emoji, FontSize = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
    };

    IEnumerable<AddItem> WebAppItems()
    {
        foreach (var a in WebApps.Catalog)
        {
            var id = a.Id;
            yield return new("app:" + id, "apps", a.Name, $"App · {a.What}{(a.Hint is null ? "" : " · " + a.Hint)}", WebAppInstalled(id),
                () => { InstallWebApp(id); return Task.CompletedTask; },
                () => { OpenWebApp(id); return Task.CompletedTask; },
                () => { RemoveWebApp(id); return Task.CompletedTask; }, null, "Offizielle Webversion");
        }
        var tg = WebApps.TelegramExe();
        yield return new("telegram-desktop", "apps", "Telegram (Windows-Programm)", "Offizielles Programm von telegram.org – installiert ohne Admin ins Benutzerprofil",
            File.Exists(tg), TelegramInstallAsync,
            () => { if (File.Exists(tg)) Launch(tg); return Task.CompletedTask; }, null, TelegramInstallAsync, "GPL-3.0");
    }

    async Task TelegramInstallAsync()
    {
        if (_core.Offline) { _hubMsg = "Trendo ist offline – zum Laden braucht es Internet."; return; }
        if (_depBusy) return;
        _depBusy = true; string? file = null;
        _core.Audit.Append(Principal.LocalOwner.Name, "app.telegram.install.start", WebApps.TelegramSetupUrl);
        BeginSetupOverlay("Telegram wird eingerichtet");
        try
        {
            SetupOverlayProgress(0, 3, "Offizielles Telegram-Setup wird geladen …");
            (file, _) = await KiDownload(WebApps.TelegramSetupUrl, "telegram-setup.exe", 0, 1, 3);
            SetupOverlayProgress(2, 3, "Installation läuft – bitte im Telegram-Setup auf „Weiter“ klicken …");
            if (TelegramInstallerOverride is { } run) await run(file);
            else
            {
                using var p = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                if (p is not null) await p.WaitForExitAsync(SetupToken);
            }
            var ok = File.Exists(WebApps.TelegramExe());
            _core.Audit.Append(Principal.LocalOwner.Name, ok ? "app.telegram.install.ok" : "app.telegram.install.failed", "");
            _hubMsg = ok ? "Telegram ist installiert." : "Telegram wurde nicht fertig installiert (abgebrochen?). Einfach noch einmal versuchen.";
            FinishSetupOverlay(ok ? "Telegram ist installiert" : "Telegram ist noch nicht installiert", _hubMsg, ok ? [] : [_hubMsg]);
        }
        catch (OperationCanceledException) when (SetupCancelRequested) { _hubMsg = "Abgebrochen."; }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _hubMsg = "Telegram ließ sich nicht einrichten: " + ex.Message;
            FinishSetupOverlay("Einrichten nicht möglich", _hubMsg, [ex.Message]);
        }
        finally { _depBusy = false; if (file is not null) TryDelete(file); }
    }

    /// <summary>Startmenü: installierte Apps mit Öffnen und Desktop-Schalter, dazu „Apps hinzufügen“.</summary>
    Control StartMenuApps()
    {
        var sp = new StackPanel { Spacing = 4, Name = "sm-apps" };
        sp.Children.Add(H("Apps", 15));
        var wrap = new WrapPanel();
        foreach (var a in WebApps.Catalog.Where(a => WebAppInstalled(a.Id)))
        {
            var id = a.Id;
            var open = new Button { Name = "sm-app-" + id, Padding = new Thickness(4), Margin = new Thickness(4), Background = Brushes.Transparent,
                Content = new StackPanel { Width = 84, Spacing = 4, Children = {
                    new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(14), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, Child = WebAppFace(a, 24) },
                    new TextBlock { Text = a.Name, Foreground = Text, FontSize = 11, TextAlignment = TextAlignment.Center } } } };
            Avalonia.Automation.AutomationProperties.SetName(open, a.Name);
            open.Click += (_, _) => { CloseStartMenu(); OpenWebApp(id); };
            var onDesk = OnDesktop("app:" + id);
            var desk = new Button { Name = "sm-app-desk-" + id, Content = onDesk ? "Vom Desktop" : "Auf den Desktop", FontSize = 10, Padding = new Thickness(4, 0), HorizontalAlignment = HorizontalAlignment.Center };
            desk.Click += (_, _) => { SetOnDesktop("app:" + id, !OnDesktop("app:" + id)); FillStartMenu(); };
            wrap.Children.Add(new StackPanel { Children = { open, desk } });
        }
        if (wrap.Children.Count > 0) sp.Children.Add(wrap);
        else sp.Children.Add(P("WhatsApp, Telegram, TikTok, Instagram und Facebook als Apps in Trendo – mit einem Klick hinzufügen.", Muted, 12));
        var add = new Button { Name = "sm-apps-add", Content = "＋ Apps hinzufügen", Padding = new Thickness(10, 4), Margin = new Thickness(0, 2, 0, 6) };
        add.Click += (_, _) => { CloseStartMenu(); OpenAddHub("apps"); };
        sp.Children.Add(add);
        return sp;
    }
}
