using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>SYS-03 Netz & Sicherheit (freigegebener Katalog, 21.10.2026).</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> NetzTabs = [];
    private string _reader = "", _readerTitle = "", _netStatus = "";
    private string? _dlStatus;
    private CancellationTokenSource? _dlCts;
    private readonly IBrush Warn = Brush.Parse("#F97316");

    private void InitSys03()
    {
        RegisterTab(NetzTabs, "netz-pruefen", "Sicher prüfen", "SYS-03", NetCheckView, "prüfsumme", "sha256", "phishing", "link prüfen", "kurzlink", "quarantäne", "defender", "virus", "formular");
        RegisterTab(NetzTabs, "netz-laden", "Laden & Speichern", "SYS-03", NetLoadView, "download manager", "fortsetzen", "lesemodus", "als pdf", "offline", "webseite speichern", "downloads sortieren");
        RegisterTab(NetzTabs, "netz-browser", "Browser", "SYS-03", NetBrowserView, "lesezeichen", "inkognito", "privat", "tor", "standardbrowser", "suchmaschine", "verlauf löschen", "proxy", "netzwerk");
    }
    private Control NetzPage(string title, Control body) => W45Page(title, NetzTabs, "netz", body);
    private Control Hints(string name, List<string> h, string ok) => new StackPanel { Name = name, Children = { h.Count == 0 ? P(ok, Text) : P("⚠ " + string.Join("\n⚠ ", h), Warn) } };

    // ---------- Sicher prüfen ----------
    private Control NetCheckView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "netz-pruefen";

        var hf = Field("hash-file", "Heruntergeladene Datei"); var he = Field("hash-expected", "Prüfsumme von der Herstellerseite", 420); var ho = new StackPanel { Name = "hash-result" };
        sp.Children.Add(Card(Section("Prüfsumme vergleichen", "So erkennst du, ob eine Datei beschädigt oder verändert wurde. Trendo erkennt MD5, SHA-1, SHA-256 und SHA-512 automatisch.",
            Line(hf, he, Act("hash-run", "Vergleichen", () => Try(V, () =>
            {
                var (ok, algo, act) = Web.VerifyHash(Clean(hf), he.Text ?? ""); ho.Children.Clear();
                ho.Children.Add(P(ok ? $"✓ Stimmt überein ({algo})." : $"✗ Stimmt NICHT überein ({algo}). Datei nicht öffnen.\nBerechnet: {act}", ok ? Brush.Parse("#22C55E") : Brush.Parse("#EF4444"), 12.5));
                _core.Audit.Append("app", "hash.verify", $"{Path.GetFileName(Clean(hf))} {algo} {(ok ? "ok" : "abweichend")}");
            }), true)), ho)));

        var lu = Field("link-url", "Adresse oder Link, z. B. aus einer Mail", 420); var lo = new StackPanel { Name = "link-result" };
        sp.Children.Add(Card(Section("Link prüfen", "Zeigt, wohin ein Link wirklich führt, und warnt vor typischen Tricks (ähnliche Namen, Sonderzeichen, Kurzlinks). Keine Garantie.",
            Line(lu, Act("link-run", "Prüfen", () => Try(V, () =>
            {
                lo.Children.Clear(); var t = Clean(lu); var h = Web.PhishingHints(t);
                if (Uri.TryCreate(t.Contains("://") ? t : "https://" + t, UriKind.Absolute, out var u)) lo.Children.Add(P(Web.Explain(u), Text));
                lo.Children.Add(Hints("link-hints", h, "Keine typischen Warnzeichen gefunden."));
            }), true), Act("link-resolve", "Kurzlink auflösen (fragt den Dienst)", async () =>
            {
                if (_core.Offline) { Say("Offline-Modus: Kurzlinks werden nicht aufgelöst.", V); return; }
                if (!Uri.TryCreate(Clean(lu), UriKind.Absolute, out var u) || !Web.IsShort(u)) { Say("Das ist kein bekannter Kurzlink.", V); return; }
                try { var z = await WebTools.ResolveShortAsync(u); Say(z is null ? "Der Dienst nennt kein Ziel." : "Echtes Ziel: " + z.AbsoluteUri + (Web.PhishingHints(z.AbsoluteUri) is { Count: > 0 } w ? "\n⚠ " + string.Join("\n⚠ ", w) : ""), V); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { Say("Kurzlink-Dienst nicht erreichbar.", V); }
            })), lo)));

        var fu = Field("form-url", "Adresse der Seite", 300); var ft = new TextBox { Name = "form-text", PlaceholderText = "Was willst du dort eintragen? (wird nicht gespeichert)", Width = 420, AcceptsReturn = true, MinHeight = 50 }; var fo = new StackPanel { Name = "form-result" };
        sp.Children.Add(Card(Section("Vor dem Absenden prüfen", "Bevor du Bankdaten, Passwörter oder Ausweisnummern in ein Formular tippst: kurz hier prüfen. Trendo sendet nie selbst etwas ab.",
            Line(fu, ft, Act("form-run", "Prüfen", () => { fo.Children.Clear(); fo.Children.Add(Hints("form-hints", Web.FormHints(Clean(fu), ft.Text ?? ""), "Nichts Heikles erkannt. Trotzdem: Nur auf Seiten eintragen, die du selbst aufgerufen hast.")); })), fo)));

        var qf = Field("q-file", "Verdächtige Datei"); var ql = new StackPanel { Spacing = 2 }; var qi = 0;
        foreach (var q in Web.QuarantineList(_core.DataRoot)) { var id = q.Id; ql.Children.Add(Row(P($"{Path.GetFileName(q.Original)} – seit {q.At:dd.MM.yyyy}", Text, 11.5), Act($"q-restore-{qi++}", "Zurücklegen", () => Try(V, () => { Web.Restore(_core.DataRoot, id); Say("Zurückgelegt.", V); })))); }
        if (qi == 0) ql.Children.Add(P("Die Quarantäne ist leer."));
        var mp = OperatingSystem.IsWindows() ? Environment.ExpandEnvironmentVariables(Web.MpCmdRun) : null;
        sp.Children.Add(Card(Section("Quarantäne und Virenprüfung", "Verdächtige Dateien werden weggesperrt (umbenannt, nicht startbar). Die Prüfung nutzt den Windows-Virenschutz Defender – ohne Administratorrechte.",
            Line(qf, Act("q-add", "In Quarantäne", () => Try(V, () => { Web.Quarantine(_core.DataRoot, Clean(qf), _core.Audit); Say("Weggesperrt.", V); }), true),
                Act("q-scan", "Mit Defender prüfen", () => Try(V, () =>
                {
                    if (mp is null || !File.Exists(mp)) throw new InvalidOperationException("Nicht möglich: Windows Defender ist hier nicht vorhanden (BLOCKED).");
                    var r = Cli.Run(mp, Web.DefenderArgs(Clean(qf)), timeoutMs: 300000); _core.Audit.Append("app", "defender.scan", $"{Path.GetFileName(Clean(qf))} exit={r.Exit}");
                    Say(Web.DefenderResult(r.Exit, r.Out + r.Err), V);
                }))), ql)));
        return NetzPage("Sicher prüfen", sp);
    }

    // ---------- Laden & Speichern ----------
    private Control NetLoadView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "netz-laden"; var off = _core.Offline;
        var du = Field("dl-url", "Adresse der Datei (https://…)", 420);
        var bar = new ProgressBar { Name = "dl-progress", Width = 300, Minimum = 0, Maximum = 100 };
        sp.Children.Add(Card(Section("Download mit Fortsetzen", off ? "Offline-Modus: Downloads sind ausgeschaltet." : "Bricht der Download ab, macht „Weiter laden“ dort weiter, wo er aufgehört hat. Die Datei landet im Download-Ordner.",
            Line(du, Act("dl-start", "Laden / Weiter laden", async () =>
            {
                if (off) { Say("Offline-Modus: Downloads sind ausgeschaltet. Unter Einstellungen kannst du das Internet wieder erlauben.", V); return; }
                if (!Uri.TryCreate(Clean(du), UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) { Say("Bitte eine http- oder https-Adresse.", V); return; }
                var target = Path.Combine(DownloadTarget, Web.SafeFileName(u)); _dlCts = new();
                _dlStatus = "Lädt …"; var prog = new Progress<(long Done, long Total)>(p => { if (p.Total > 0) bar.Value = 100.0 * p.Done / p.Total; });
                try { var n = await WebTools.DownloadAsync(u.AbsoluteUri, target, prog, _dlCts.Token); _dlStatus = $"Fertig: {Path.GetFileName(target)} ({ResourceMonitor.Size(n)}), Prüfsumme {J2.Sha(target)[..12]}…"; _core.Audit.Append("app", "download", $"{u.Host} {Path.GetFileName(target)}"); }
                catch (OperationCanceledException) { _dlStatus = "Angehalten – „Weiter laden“ macht dort weiter."; }
                catch (HttpRequestException ex) { _dlStatus = "Abgebrochen: " + ex.Message + " – „Weiter laden“ versucht es erneut."; }
                Say(_dlStatus, V);
            }, true), Act("dl-stop", "Anhalten", () => { if (_dlCts is { IsCancellationRequested: false } c && _dlStatus == "Lädt …") { c.Cancel(); Say("Wird angehalten …", V); } else Say("Gerade läuft kein Download.", V); }), bar), P(_dlStatus ?? "", Text, 11))));

        var sf = Field("dlsort-folder", "Ordner", 340, Browsers.DownloadsFolder()); var so = new StackPanel { Name = "dlsort-preview" };
        sp.Children.Add(Card(Section("Downloads aufräumen", "Sortiert nach Art in Unterordner (Dokumente, Bilder, Programme …). Erst Vorschau, rückgängig machbar über „Dateien“.",
            Line(sf, Act("dlsort-sim", "Vorschau", () => Try(V, () => { so.Children.Clear(); var p = Web.SortPlan(Clean(sf)); so.Children.Add(P($"{p.Count} Dateien:", Text)); foreach (var g in p.GroupBy(m => Path.GetFileName(Path.GetDirectoryName(m.To)))) so.Children.Add(P($"{g.Key}: {g.Count()}", null, 11)); })),
                Act("dlsort-apply", "Sortieren", () => Try(V, () => { var p = Web.SortPlan(Need(sf, "den Ordner zum Aufräumen")); if (p.Count == 0) { Say("Nichts zu sortieren – der Ordner ist schon aufgeräumt.", V); return; } _lastUndo = Renamer.Apply(p, Path.Combine(_core.DataRoot, "werkzeuge", "rueckgaengig")); Say($"{p.Count} Dateien einsortiert.", V); })),
                Act("dlsort-undo", "Rückgängig", () => Try(V, () => { if (_lastUndo is null) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen."); Renamer.Undo(_lastUndo); _lastUndo = null; Say("Zurückgesetzt.", V); }))), so)));

        var pu = Field("page-url", "Adresse einer Webseite", 420);
        var reader = new SelectableTextBlock { Name = "reader-text", Text = _reader, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 13.5, MaxWidth = 760 };
        sp.Children.Add(Card(Section("Webseite lesen, speichern, merken", off ? "Offline-Modus: Webseiten werden nicht geladen." : "Lesemodus zeigt nur den Text (ohne Werbung, ohne Skripte). Speichern legt eine Offline-Kopie oder ein PDF in den Projektordner.",
            Line(pu, Act("page-read", "Lesemodus", () => PageJob(pu, V, "Lesemodus", async u => { var h = await WebTools.GetHtmlAsync(u); _readerTitle = Web.Title(h); _reader = Web.ReaderText(h); return ""; }), true),
                Act("page-offline", "Offline speichern", () => PageJob(pu, V, "Offline speichern", async u => { var h = await WebTools.GetHtmlAsync(u); var f = Path.Combine(WorkFolder("webseiten"), Web.SafeFileName(new Uri(u.GetLeftPart(UriPartial.Authority) + "/" + (Web.Title(h).Length > 0 ? Web.Title(h) : u.Host))) + ".html"); await File.WriteAllTextAsync(f, Web.OfflineCopy(h, u)); return "Gespeichert: " + f; })),
                Act("page-pdf", "Als PDF", () => PageJob(pu, V, "Als PDF", async u => SavePdf(u, await WebTools.GetHtmlAsync(u)))),
                Act("page-note", "Als Notiz merken", () => Try(V, () => { if (!Uri.TryCreate(Clean(pu), UriKind.Absolute, out var u)) throw new ArgumentException("Bitte eine vollständige Adresse."); new NoteStore(_core.DataRoot).Add(Web.WebNote(u, _readerTitle.Length > 0 ? _readerTitle : u.Host)); Say("Als Notiz gespeichert – über die Suche (Strg+K) wiederzufinden.", V); }))),
            _reader.Length > 0 ? new StackPanel { Spacing = 6, Children = { H(_readerTitle, 16), reader } } : new Panel())));
        return NetzPage("Laden & Speichern", sp);
    }

    private string DownloadTarget { get { var d = Browsers.DownloadsFolder(); try { Directory.CreateDirectory(d); return d; } catch (Exception) { return WorkFolder("downloads"); } } }

    private async void PageJob(TextBox url, string view, string what, Func<Uri, Task<string>> job)
    {
        if (_core.Offline) { Say($"„{what}“ geht nicht im Offline-Modus – Webseiten werden nicht geladen.", view); return; }
        var u = Browsers.Normalize(Clean(url)); if (u is null || u.AbsoluteUri.StartsWith(Web.SearchUrl(_core.DataRoot))) { Say($"„{what}“: Bitte zuerst eine Webseiten-Adresse eingeben.", view); return; }
        Say($"„{what}“ läuft …", view);
        try { var m = await job(u); Say(m, view); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException) { Say("Hat nicht geklappt: " + ex.Message, view); }
    }

    public string SavePdf(Uri u, string html)
    {
        var b = Browsers.Find().FirstOrDefault(x => x.Id is "chrome" or "edge" or "brave")?.Path ?? Cli.Find("google-chrome") ?? Cli.Find("chromium") ?? Cli.Find("msedge")
            ?? throw new InvalidOperationException("Dafür braucht es Chrome, Edge oder Brave.");
        var pdf = Path.Combine(WorkFolder("webseiten"), $"{u.Host}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
        var tmp = Path.Combine(Path.GetTempPath(), $"trendo-druck-{Guid.NewGuid():N}.html"); File.WriteAllText(tmp, Web.PrintableA4(html, u));
        try { Cli.Run(b, Web.PrintToPdfArgs(new Uri(tmp), pdf), timeoutMs: 90000); } finally { File.Delete(tmp); }
        if (!File.Exists(pdf)) throw new InvalidOperationException("Das PDF wurde nicht erstellt.");
        var sha = J2.Sha(pdf); _core.Audit.Append("app", "web.pdf", $"{u.Host} sha256={sha}");
        return $"PDF gespeichert: {pdf} (Prüfsumme {sha[..12]}…)";
    }

    // ---------- Browser ----------
    private Control NetBrowserView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "netz-browser";
        var browsers = Browsers.Find();

        var bt = Field("bm-title", "Name", 160); var bu = Field("bm-url", "https://…", 260); var bf = Field("bm-folder", "Ordner (optional)", 140); var bi = Field("bm-import", "Lesezeichen-Datei (HTML-Export)", 300);
        var list = new StackPanel { Spacing = 2 }; var bms = Bookmarks.Load(_core.DataRoot);
        foreach (var g in bms.GroupBy(b => b.Folder).Take(10))
        {
            list.Children.Add(P(g.Key.Length > 0 ? "📁 " + g.Key : "Ohne Ordner", Text, 11.5));
            foreach (var b in g.Take(15)) { var url = b.Url; list.Children.Add(Act("bm-open-" + list.Children.Count, b.Title.Length > 0 ? b.Title : url, () => { if (!_core.Offline) Launch(url); })); }
        }
        sp.Children.Add(Card(Section("Lesezeichen", $"Deine Lesezeichen auf diesem Rechner ({bms.Count}). Einlesen aus Chrome, Edge oder Firefox: dort „Lesezeichen exportieren“ wählen und die HTML-Datei hier angeben.",
            Line(bt, bu, bf, Act("bm-add", "Speichern", () => Try(V, () => { Bookmarks.Add(_core.DataRoot, new(bt.Text?.Trim() ?? "", Clean(bu), bf.Text?.Trim() ?? "")); Show(V); }), true)),
            Line(bi, Act("bm-import-run", "Einlesen", () => Try(V, () => { var n = Bookmarks.ImportHtml(File.ReadAllText(Clean(bi))); foreach (var b in n) Bookmarks.Add(_core.DataRoot, b); Say($"{n.Count} Lesezeichen eingelesen.", V); })),
                Act("bm-export", "Als HTML speichern", () => Try(V, () => { var f = Path.Combine(WorkFolder("lesezeichen"), "lesezeichen.html"); File.WriteAllText(f, Bookmarks.ExportHtml(Bookmarks.Load(_core.DataRoot))); Say("Gespeichert: " + f, V); }))), list)));

        var priv = Line(); foreach (var b in browsers) if (Web.PrivateArg(b.Id) is { } arg) { var path = b.Path; priv.Children.Add(Act("priv-" + b.Id, b.Name + " privat", () => { if (!_core.Offline) _core.Integrations.LaunchDirect(Principal.LocalOwner, path, _core.Audit, arg); })); }
        var tor = Web.TorPaths.Select(Environment.ExpandEnvironmentVariables).FirstOrDefault(File.Exists);
        sp.Children.Add(Card(Section("Privat surfen", "Ein privates Fenster speichert keinen Verlauf. Der Tor-Browser verschleiert zusätzlich deinen Standort – Trendo öffnet nur die offizielle Seite.",
            browsers.Count == 0 ? P("Kein bekannter Browser gefunden.") : priv,
            Line(tor is not null ? Act("tor-open", "Tor-Browser starten", () => _core.Integrations.LaunchDirect(Principal.LocalOwner, tor, _core.Audit)) : Act("tor-get", "Tor-Browser offiziell holen", () => { if (_core.Offline) { Say("Offline-Modus: Die Tor-Seite kann nicht geöffnet werden.", V); return; } Launch(Web.TorOfficial); Say("Offizielle Tor-Seite geöffnet – nach der Installation hier „Tor-Browser starten“.", V); })))));

        var eng = Line(); var cur = Web.Engine(_core.DataRoot);
        foreach (var (id, name, _) in Web.Engines) eng.Children.Add(Act("engine-" + id, (cur == id ? "✓ " : "") + name, () => { Web.SetEngine(_core.DataRoot, id); Show(V); }, cur == id));
        sp.Children.Add(Card(Section("Suchmaschine", "Gilt für die Trendo-Suche (Strg+K → „Im Internet suchen“) und das Adressfeld unter Browser & Downloads.", eng)));

        var steps = new StackPanel { Spacing = 4 };
        foreach (var (id, name, st) in Web.ClearHistory.Where(c => browsers.Count == 0 || browsers.Any(b => b.Id == c.Id))) steps.Children.Add(P($"{name}: " + string.Join(" → ", st), null, 11.5));
        sp.Children.Add(Card(Section("Standardbrowser und Verlauf", "Trendo zeigt nur an und erklärt – es ändert nichts an deinen Browsern.",
            P("Standardbrowser: " + DefaultBrowser(), Text), Line(Act("default-settings", "Windows-Einstellung öffnen", () => Launch("ms-settings:defaultapps"))), H("Verlauf löschen – so geht's", 13), steps)));

        string proxy;
        try { proxy = ReadProxy(); } catch (Exception) { proxy = "Konnte nicht gelesen werden."; }
        sp.Children.Add(Card(Section("Netzwerk", "Zeigt, ob Internet und Namensauflösung (DNS) funktionieren. Im Offline-Modus wird nichts geprüft.",
            Line(Act("net-check", "Verbindung prüfen", async () =>
            {
                if (_core.Offline) { _netStatus = "Offline-Modus ist an – Trendo geht nicht ins Internet."; Show(V); return; }
                var (dns, web) = await WebTools.NetworkCheckAsync();
                _netStatus = (dns ? "✓ Namensauflösung klappt" : "✗ Namensauflösung klappt nicht") + " · " + (web ? "✓ Internet erreichbar" : "✗ Internet nicht erreichbar"); Show(V);
            }), P(_netStatus, Text)), P("Proxy: " + proxy, null, 11.5))));
        return NetzPage("Browser", sp);
    }

    private static string DefaultBrowser()
    {
        if (!OperatingSystem.IsWindows()) return "nur unter Windows ablesbar";
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
        return Web.BrowserForProgId(k?.GetValue("ProgId") as string);
    }

    private static string ReadProxy()
    {
        bool? on = null; string? server = null, pac = null;
        if (OperatingSystem.IsWindows())
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            on = k?.GetValue("ProxyEnable") is int i ? i == 1 : null; server = k?.GetValue("ProxyServer") as string; pac = k?.GetValue("AutoConfigURL") as string;
        }
        return Web.ProxyInfo(on, server, pac, Environment.GetEnvironmentVariable("HTTPS_PROXY") ?? Environment.GetEnvironmentVariable("HTTP_PROXY"));
    }
}

static class J2 { public static string Sha(string f) { using var s = File.OpenRead(f); return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)).ToLowerInvariant(); } }
