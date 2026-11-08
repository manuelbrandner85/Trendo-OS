using System.Diagnostics;
using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 2: typst (Brief/Rechnung als PDF), mediamtx (eigener Sendeserver), streamlink (Live-Sender der Öffentlich-Rechtlichen).
/// Einrichtung nur nach Zustimmung über den Abhängigkeitskatalog (offizielle Downloads mit fester Prüfsumme).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Programmläufe nachbilden und Programme vorgeben ("" = nicht eingerichtet).</summary>
    public static Func<string, IReadOnlyList<string>, int, Cli.Result>? ToolRunOverride { get; set; }
    public static Dictionary<string, string> ToolExeOverride { get; } = [];
    string? ToolExe(string id) => ToolExeOverride.TryGetValue(id, out var o) ? (o.Length == 0 ? null : o) : _deps.ResolvePath(DependencyCatalog.Get(id));
    string _typstMsg = "", _mtxMsg = "", _slMsg = "";
    public string TypstMessage => _typstMsg; public string LiveServerMessage => _mtxMsg; public string LiveResolveMessage => _slMsg;

    async Task SetupTool(string id, string what, string view, Action<string> say)
    {
        var comp = DependencyCatalog.Get(id);
        var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", $"{comp.Name} ({what}) einrichten", comp.Id);
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        say($"{comp.Name} wird eingerichtet (offizieller Download, Prüfsumme wird geprüft) …"); Show(view);
        var ok = !_opt.Isolated && await Task.Run(() => _deps.InstallAsync(Principal.LocalOwner, comp));
        say(ok ? $"{comp.Name} ist eingerichtet." : _opt.Isolated ? "Testmodus: nichts installiert." : $"{comp.Name} konnte nicht eingerichtet werden – unter Zusatzprogramme erneut versuchen.");
        Show(view);
    }

    // ---------- typst: Brief / Rechnung als PDF ----------
    /// <summary>Setzt Brief bzw. Rechnung mit typst als PDF in den Ordner „Dokumente“ (öffentlich für Tests). Posten je Zeile „Menge;Artikel;Preis“.</summary>
    public void TypstLetter(string title, string body, string items)
    {
        var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE"); var rows = new List<(decimal, string, decimal)>();
        foreach (var l in items.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0))
        {
            var p = l.Split(';');
            if (p.Length != 3 || !decimal.TryParse(p[0].Trim(), de, out var q) || !decimal.TryParse(p[2].Trim().TrimEnd('€').Trim(), de, out var pr)) { _typstMsg = $"⚠ Posten „{l.Trim()}“ bitte als „Menge;Artikel;Preis“."; Show("doku-schreiben"); return; }
            rows.Add((q, p[1].Trim(), pr));
        }
        if (title.Trim().Length == 0) { _typstMsg = "⚠ Bitte einen Titel angeben."; Show("doku-schreiben"); return; }
        var dir = DokuOut; Directory.CreateDirectory(dir);
        var name = string.Concat(title.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-'); if (name.Length == 0) name = "brief";
        var typ = Path.Combine(dir, name + ".typ"); File.WriteAllText(typ, TypstDoc.Letter(title, body, rows));
        var (ok, msg) = TypstDoc.Compile(ToolExe("typst") ?? "", typ, Path.Combine(dir, name + ".pdf"), dir, run: ToolRunOverride);
        _typstMsg = ok ? $"✓ PDF gespeichert: {Path.GetFileName(msg)} (Quelle {Path.GetFileName(typ)} bleibt bearbeitbar)" : "⚠ " + msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "office.typst.pdf", ok ? Path.GetFileName(msg) : "fehler");
        Show("doku-schreiben");
    }

    Control TypstCard()
    {
        var sp = new StackPanel { Spacing = 6, Name = "typst-card" };
        sp.Children.Add(H("Brief oder Rechnung als PDF (typst)", 13));
        if (ToolExe("typst") is null)
        {
            sp.Children.Add(P("Setzt Briefe und Rechnungen in sauberem Druck-Layout als PDF – schnell, ohne Office. Kostenlos, Apache-2.0, ~22 MB.", null, 11));
            sp.Children.Add(Act("typst-setup", "typst einrichten", () => _ = SetupTool("typst", "PDF-Satz", "doku-schreiben", s => _typstMsg = s)));
        }
        else
        {
            var title = new TextBox { Name = "typst-title", PlaceholderText = "Titel, z. B. Rechnung Nr. 17" };
            var body = new TextBox { Name = "typst-body", AcceptsReturn = true, MinHeight = 70, TextWrapping = Avalonia.Media.TextWrapping.Wrap, PlaceholderText = "Text (Absätze mit Leerzeile)" };
            var items = new TextBox { Name = "typst-items", AcceptsReturn = true, MinHeight = 50, PlaceholderText = "Posten (optional), je Zeile: Menge;Artikel;Preis – z. B. 2;Stühle;59,50" };
            sp.Children.Add(title); sp.Children.Add(body); sp.Children.Add(items);
            sp.Children.Add(Act("typst-run", "Als PDF setzen", () => TypstLetter(title.Text ?? "", body.Text ?? "", items.Text ?? ""), true));
        }
        if (_typstMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "typst-msg", Text = _typstMsg, FontSize = 11.5, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return sp;
    }

    // ---------- mediamtx: eigener Sendeserver ----------
    Process? _mtx; string _mtxPath = "sendung"; bool _mtxOn, _mtxHooked;
    public bool LiveServerRunning => _mtxOn && (_mtx is null || !_mtx.HasExited);
    /// <summary>Für Tests: Start des Servers nachbilden (Programm, Argumente, Arbeitsordner) – true = gestartet.</summary>
    public static Func<string, IReadOnlyList<string>, string, bool>? LiveServerStartOverride { get; set; }

    public void LiveServerStart(string path)
    {
        if (LiveServerRunning) { _mtxMsg = "Der Sendeserver läuft schon."; Show("sendung"); return; }
        if (!LiveServer.ValidPath(path)) { _mtxMsg = "⚠ Sendungsname: nur a–z, 0–9, - und _ (bis 40 Zeichen)."; Show("sendung"); return; }
        var exe = ToolExe("mediamtx"); if (exe is null) { _mtxMsg = "⚠ mediamtx ist noch nicht eingerichtet."; Show("sendung"); return; }
        var work = Path.Combine(WorkFolder("Live"), "server"); var args = LiveServer.Prepare(work);
        if (LiveServerStartOverride is { } o) _mtxOn = o(exe, args, work);
        else if (_opt.Isolated) { _mtxMsg = "Testmodus: Sendeserver wird nicht gestartet."; Show("sendung"); return; }
        else
        {
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = work, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            try { _mtx = Process.Start(psi); _mtxOn = _mtx is not null; } catch (System.ComponentModel.Win32Exception e) { _mtxMsg = "⚠ Sendeserver startet nicht: " + e.Message; Show("sendung"); return; }
            if (!_mtxHooked) { _mtxHooked = true; Closed += (_, _) => { try { if (_mtx is { HasExited: false }) _mtx.Kill(); } catch (InvalidOperationException) { } }; }
        }
        if (!_mtxOn) { _mtxMsg = "⚠ Sendeserver startet nicht."; Show("sendung"); return; }
        _mtxPath = path;
        _mtxMsg = $"✓ Sendeserver bereit (nur auf diesem Rechner). In OBS unter Stream → Server eintragen: {LiveServer.PublishUrl(path)} – Ansehen: {LiveServer.WatchUrl(path)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "live.server.start", path);
        Show("sendung");
    }

    public void LiveServerStop()
    {
        try { if (_mtx is { HasExited: false }) _mtx.Kill(); } catch (InvalidOperationException) { }
        _mtx = null; _mtxOn = false; _mtxMsg = "Sendeserver beendet."; _core.Audit.Append(Principal.LocalOwner.Name, "live.server.stop", _mtxPath); Show("sendung");
    }

    Control LiveServerCard()
    {
        var sp = new StackPanel { Spacing = 6, Name = "mtx-card" };
        sp.Children.Add(H("Eigener Sendeserver (mediamtx)", 13));
        if (ToolExe("mediamtx") is null)
        {
            sp.Children.Add(P("Nimmt deinen Stream aus OBS an und gibt ihn zum Ansehen weiter – ohne Plattform, ohne Konto. Zuerst nur auf diesem Rechner. Kostenlos, MIT, ~29 MB.", null, 11));
            sp.Children.Add(Act("mtx-setup", "mediamtx einrichten", () => _ = SetupTool("mediamtx", "Sendeserver", "sendung", s => _mtxMsg = s)));
        }
        else if (LiveServerRunning)
            sp.Children.Add(Act("mtx-stop", "Sendeserver beenden", LiveServerStop));
        else
        {
            var name = new TextBox { Name = "mtx-path", Text = _mtxPath, Width = 200, PlaceholderText = "sendung" };
            sp.Children.Add(new WrapPanel { Children = { P("Sendungsname", null, 12), new Border { Width = 8 }, name, new Border { Width = 8 }, Act("mtx-start", "Sendeserver starten", () => LiveServerStart((name.Text ?? "").Trim()), true) } });
        }
        if (_mtxMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "mtx-msg", Text = _mtxMsg, FontSize = 11.5, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return sp;
    }

    // ---------- streamlink: Live-Sender der Öffentlich-Rechtlichen ----------
    /// <summary>Löst den Livestream eines freigegebenen Senders auf und spielt ihn im Trendo-Player (öffentlich für Tests).</summary>
    public async Task LiveChannelPlay(string url)
    {
        var exe = ToolExe("streamlink");
        if (exe is null) { _slMsg = "⚠ streamlink ist noch nicht eingerichtet."; Show("tv"); return; }
        _slMsg = "Livestream wird gesucht …"; Show("tv");
        var run = ToolRunOverride;
        var (ok, msg) = await Task.Run(() => LiveResolve.StreamUrl(exe, url, "best", run: run));
        _core.Audit.Append(Principal.LocalOwner.Name, "tv.live.oeffentlich", ok ? new Uri(url).Host : "fehler");
        if (!ok) { _slMsg = "⚠ " + msg; Show("tv"); return; }
        _slMsg = "▶ Läuft im Player."; _tvTab = "live"; Show("tv"); TvPlayUrl(msg, "main");
    }

    Control LiveChannelsCard()
    {
        var sp = new StackPanel { Spacing = 6, Name = "sl-card" };
        sp.Children.Add(H("Live im Trendo-Player (Öffentlich-Rechtliche)", 13));
        if (ToolExe("streamlink") is null)
        {
            sp.Children.Add(P("Spielt das laufende Programm von Das Erste und arte direkt in Trendo ab – ohne Browser. Kostenlos, BSD-2, ~85 MB. Keine Aufnahme, kein Login.", null, 11));
            sp.Children.Add(Act("sl-setup", "streamlink einrichten", () => _ = SetupTool("streamlink", "Live-Sender", "tv", s => _slMsg = s)));
        }
        else
        {
            var row = new WrapPanel(); var i = 0;
            foreach (var (n, u) in LiveResolve.Channels) { var url = u; row.Children.Add(Act($"sl-play-{i++}", "▶ " + n, () => _ = LiveChannelPlay(url), true)); }
            sp.Children.Add(row);
        }
        if (_slMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "sl-msg", Text = _slMsg, FontSize = 11.5, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return sp;
    }
}
