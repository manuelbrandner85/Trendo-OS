using System.Net.Http.Headers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>KI-Bibliothek (Vorgabe 2026-10-26): kostenlose KI nach Arten sortiert (Chat, Bild, Video, Sprache, Vorlesen, Musik …),
/// laden mit einem Klick und direkt in Trendo ausprobieren. Laufzeiten und Modelle kommen nur von den offiziellen Quellen, mit Größen-
/// und Prüfsummenkontrolle, und liegen in „TrendoWerkzeuge/ki“ (geht bei der Deinstallation mit).</summary>
public partial class MainWindow
{
    private KiKind _kiLibKind = KiKind.Chat;
    private string? _kiTry;
    private string _kiLibMsg = "";
    private string? _kiResult;
    /// <summary>Für Tests: ersetzt den Download (Adresse, Dateiname) → (lokale Datei, erwartete SHA-256 oder null).</summary>
    public Func<string, string, Task<(string File, string? Sha256)>>? KiDownloadOverride { get; set; }
    /// <summary>Für Tests: ersetzt das Ausführen einer KI-Laufzeit.</summary>
    public Func<string, IReadOnlyList<string>, string?, Task<Cli.Result>>? KiRunOverride { get; set; }
    public KiLibraryStore KiStore => new(_deps.ToolsRoot);

    private void InitKiLibrary()
    {
        _showMap["ki-bibliothek"] = KiLibraryView; KiTabs.Add(("ki-bibliothek", "KI-Bibliothek"));
        PageModules["ki-bibliothek"] = "X-05"; // Freie KI-Quellen und Modelle
        ExtraPages.Add(("KI-Bibliothek", "ki-bibliothek", ["bild ki", "video ki", "musik", "sprache", "vorlesen", "diktat", "whisper", "stable diffusion", "modelle", "kostenlos"]));
    }

    public void KiLibraryShow(KiKind kind, string? tryId = null) { _kiLibKind = kind; _kiTry = tryId; _kiResult = null; Show("ki-bibliothek"); }

    private Control KiLibraryView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Kostenlose KI für alles – sie läuft auf deinem PC, ohne Konto und ohne Kosten. Wähle eine Art, lade ein Modell mit einem Klick und probiere es gleich hier aus.", Text, 13));
        var kinds = new WrapPanel();
        foreach (var (k, (name, _, _)) in KiLibrary.Kinds)
        {
            var kk = k; var on = k == _kiLibKind;
            var b = new Button { Name = "kilib-kind-" + k, Content = $"{name} ({KiLibrary.OfKind(k).Count()})", Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(16),
                Background = on ? Accent : Brush.Parse("#22FFFFFF"), Foreground = on ? Brushes.White : Text };
            b.Click += (_, _) => KiLibraryShow(kk);
            kinds.Children.Add(b);
        }
        sp.Children.Add(kinds);
        var (kindName, _, kindDesc) = KiLibrary.Kinds[_kiLibKind];
        sp.Children.Add(H(kindName, 15));
        sp.Children.Add(P(kindDesc, null, 12));
        if (_kiLibMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "kilib-msg", Text = _kiLibMsg, Foreground = Accent, TextWrapping = TextWrapping.Wrap });

        var store = KiStore;
        var have = LastScan.TryGetValue("ollama", out var ol) && ol.Up ? ol.Models : [];
        var hw = new HardwareInfo(TotalRam, null, 0);
        var runtimes = KiLibrary.OfKind(_kiLibKind).Select(m => m.Runtime).Distinct().Select(KiLibrary.RuntimeInfo);
        foreach (var rt in runtimes)
        {
            var ready = rt.Id == KiRuntime.Ollama ? ol.Up : store.RuntimeReady(rt.Id);
            if (ready && rt.Id != KiRuntime.Ollama && !rt.FromVollpaket)
            {
                var rid = rt.Id;
                sp.Children.Add(Row(P($"{rt.Name} ist eingerichtet ({store.RuntimeDir(rid)}).", Text, 11), ConfirmRemoveButton("rt:" + rid, "kilib-rt-remove-" + rid.ToString().ToLowerInvariant(), "ki-bibliothek", () =>
                {
                    try { _kiLibMsg = store.RemoveRuntime(rid, _core.Audit) ? $"{rt.Name} wurde entfernt. Modelle, die es brauchen, laden es beim nächsten Mal neu." : $"{rt.Name} war nicht eingerichtet."; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _kiLibMsg = $"{rt.Name} ist gerade in Benutzung: {ex.Message}"; }
                    return Task.CompletedTask;
                })));
            }
            sp.Children.Add(P($"Läuft mit {rt.Name} ({rt.License}): {rt.WhatItDoes} " +
                (ready ? "✓ eingerichtet." : rt.FromVollpaket ? (rt.Id == KiRuntime.Ollama ? "Startet automatisch, sobald die Trendo KI läuft." : "Kommt aus dem Vollpaket.") : $"Wird beim ersten Laden mit eingerichtet ({rt.Bytes / (1 << 20)} MB, {(rt.Url is null ? $"offizielle GitHub-Seite {rt.Repo}" : $"offizielles Paket {new Uri(rt.Url).Host}, verlinkt auf github.com/{rt.Repo}")})."), null, 11));
        }
        if (!ol.Up && KiLibrary.OfKind(_kiLibKind).Any(m => m.Runtime == KiRuntime.Ollama)) sp.Children.Add(OllamaHint("ki-bibliothek"));
        foreach (var m in KiLibrary.OfKind(_kiLibKind))
        {
            var fits = hw.RamBytes <= 0 || hw.RamBytes >= m.MinRamGb * 1024L * 1024 * 1024 * 0.9;
            var got = m.Runtime == KiRuntime.Ollama ? have.Contains(m.Tag) : store.ModelReady(m);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            if (!m.Ready)
            {
                buttons.Children.Add(new Button { Name = "kilib-get-" + m.Id, Content = "Kommt in einer nächsten Stufe", IsEnabled = false });
            }
            else if (got)
            {
                if (m.Runtime == KiRuntime.Ollama)
                {
                    var open = new Button { Name = "kilib-use-" + m.Id, Content = "In der Trendo KI nutzen" }; open.Click += (_, _) => Show("ki"); buttons.Children.Add(open);
                    var tag = m.Tag;
                    buttons.Children.Add(ConfirmRemoveButton("ollama:" + tag, "kilib-remove-" + m.Id, "ki-bibliothek", async () =>
                    {
                        var ok = await new OllamaBackend().DeleteAsync(tag);
                        _core.Audit.Append(Principal.LocalOwner.Name, ok ? "ki.model.remove" : "ki.model.remove.failed", tag);
                        _kiLibMsg = ok ? $"{m.Name} wurde entfernt." : $"{m.Name} ließ sich nicht entfernen (läuft Ollama?).";
                        await ScanSourcesAsync();
                    }));
                }
                else
                {
                    var t = new Button { Name = "kilib-try-" + m.Id, Content = "Ausprobieren", Background = Accent }; t.Click += (_, _) => KiLibraryShow(m.Kind, m.Id); buttons.Children.Add(t);
                    if (m.Files.Count > 0) buttons.Children.Add(RemoveKiButton(m));
                }
            }
            else
            {
                var label = m.OpenLicense ? "Laden" : $"Bedingungen „{m.License}“ annehmen & laden";
                var get = new Button { Name = "kilib-get-" + m.Id, Content = fits ? label : $"braucht {m.MinRamGb} GB Arbeitsspeicher", IsEnabled = fits && !_depBusy && !_core.Offline && (m.Runtime != KiRuntime.Ollama || ol.Up) };
                if (!get.IsEnabled) ToolTip.SetTip(get, !fits ? $"Braucht {m.MinRamGb} GB Arbeitsspeicher." : _core.Offline ? "Im Offline-Modus wird nichts geladen." : _depBusy ? "Gerade wird etwas eingerichtet – bitte kurz warten." : "Zuerst muss Ollama laufen – siehe Hinweis oben.");
                get.Click += async (_, _) => await KiLibraryInstall(m);
                buttons.Children.Add(get);
            }
            var info = new StackPanel { Children = {
                new TextBlock { Text = m.Name + (got ? "  ✓" : ""), Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold },
                P($"{m.GoodFor} · {(m.Bytes >= 1L << 30 ? $"{m.Bytes / (double)(1L << 30):0.0} GB" : $"{Math.Max(1, m.Bytes / (1 << 20))} MB")} · {(fits ? "passt zu deinem PC" : $"braucht {m.MinRamGb} GB Arbeitsspeicher")} · Lizenz: {m.License}", fits ? null : Brush.Parse("#FACC15"), 10.5),
                P("So geht's: " + m.HowTo, null, 10.5) } };
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { info, Pos(buttons, 1) } }));
            if (_kiTry == m.Id && got) sp.Children.Add(KiTryPanel(m));
        }
        return KiPage("KI-Bibliothek", sp);
    }

    private Button RemoveKiButton(KiLibraryModel m)
    {
        var key = "ki:" + m.Id; var armed = _confirmRemove.Contains(key);
        var b = new Button { Name = "kilib-remove-" + m.Id, Content = armed ? "Wirklich entfernen?" : "Entfernen", Background = armed ? Brush.Parse("#B91C1C") : null };
        b.Click += (_, _) =>
        {
            if (!_confirmRemove.Remove(key)) { _confirmRemove.Add(key); Show("ki-bibliothek"); return; }
            try { KiStore.RemoveModel(m, _core.Audit); _kiLibMsg = $"{m.Name} wurde entfernt."; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _kiLibMsg = $"{m.Name} ist gerade in Benutzung: {ex.Message}"; }
            if (_kiTry == m.Id) _kiTry = null;
            Show("ki-bibliothek");
        };
        return b;
    }

    /// <summary>Laden: Ollama-Modell über Ollama, sonst Laufzeit (falls nötig) und Modelldateien von den offiziellen Quellen. Der Klick ist die Zustimmung,
    /// bei Lizenzen mit eigenen Bedingungen steht das im Knopf. Freigabe und Ergebnis landen im Audit-Log.</summary>
    public async Task KiLibraryInstall(KiLibraryModel m)
    {
        if (_depBusy || !m.Ready) return;
        var req = _core.Approvals.Request(Principal.LocalOwner, "ki.pull", $"KI „{m.Name}“ laden ({m.License})", m.Id);
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        if (m.Runtime == KiRuntime.Ollama)
        {
            _depBusy = true; _kiLibMsg = $"{m.Name} wird geladen …"; Show("ki-bibliothek");
            try
            {
                var ok = await _core.Approvals.Execute(req.Id, "ki.pull", m.Tag, () => new OllamaBackend().PullAsync(m.Tag));
                _kiLibMsg = ok ? $"{m.Name} ist jetzt da und in der Trendo KI nutzbar." : $"{m.Name} konnte nicht geladen werden – Internet prüfen und erneut versuchen.";
            }
            finally { _depBusy = false; }
            await ScanSourcesAsync(); Show("ki-bibliothek");
            return;
        }
        _depBusy = true;
        BeginSetupOverlay($"{m.Name} wird eingerichtet");
        var store = KiStore;
        try
        {
            var rt = KiLibrary.RuntimeInfo(m.Runtime);
            var steps = m.Files.Count + (store.RuntimeReady(m.Runtime) || rt.FromVollpaket ? 0 : 1); var step = 0;
            if (!rt.FromVollpaket && !store.RuntimeReady(m.Runtime))
            {
                SetupOverlayProgress(step, steps, $"{rt.Name} wird geladen …");
                var (zip, _) = await KiDownload(rt.DownloadUrl, rt.Asset, rt.Bytes, step, steps);
                SetupOverlayProgress(step, steps, $"{rt.Name} wird entpackt …");
                try { await Task.Run(() => store.InstallRuntime(m.Runtime, zip, _core.Audit, Extract7z)); } finally { TryDelete(zip); }
                step++;
            }
            else if (rt.FromVollpaket && !store.RuntimeReady(m.Runtime)) throw new InvalidOperationException($"{rt.Name} fehlt noch – bitte unter „Programme“ aus dem Vollpaket einrichten.");
            foreach (var f in m.Files)
            {
                SetupToken.ThrowIfCancellationRequested();
                if (new FileInfo(store.ModelFile(m, f)) is { Exists: true } fi && fi.Length == f.Bytes) { step++; continue; }
                SetupOverlayProgress(step, steps, $"{f.FileName} wird geladen …");
                var (file, sha) = await KiDownload(f.Url, f.FileName, f.Bytes, step, steps);
                try { await Task.Run(() => store.InstallModelFile(m, f, file, sha, _core.Audit)); } finally { TryDelete(file); }
                step++;
            }
            SetupOverlayProgress(steps, steps, "Fertig ✓");
            _core.Audit.Append(Principal.LocalOwner.Name, "ki.library.install", $"{m.Id} {m.License}");
            _kiLibMsg = $"{m.Name} ist eingerichtet. Mit „Ausprobieren“ kannst du es gleich testen."; _kiTry = m.Id;
        }
        catch (OperationCanceledException) when (SetupCancelRequested) { _kiLibMsg = $"{m.Name}: Laden abgebrochen."; }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or UnauthorizedAccessException or TaskCanceledException or InvalidDataException)
        {
            _core.Audit.Append(Principal.LocalOwner.Name, "ki.library.failed", $"{m.Id}: {ex.Message}");
            _kiLibMsg = $"{m.Name}: Laden hat nicht geklappt – {ex.Message}";
        }
        finally { _depBusy = false; EndSetupOverlay(); }
        Show("ki-bibliothek");
    }

    static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    /// <summary>Erlaubte Download-Server: GitHub und Hugging Face (inkl. deren Download-Spiegel), nur HTTPS auf Port 443.</summary>
    internal static bool KiHostAllowed(Uri u) =>
        u.Scheme == Uri.UriSchemeHttps && u.Port == 443 &&
        (GitHubHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase) || u.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".hf.co", StringComparison.OrdinalIgnoreCase)
         || u.Host.Equals("files.acemusic.ai", StringComparison.OrdinalIgnoreCase) // offizielles ACE-Step-Paket, im README des Projekts verlinkt
         || KiSourceSetups.VendorHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase) // offizielle Installer der Hersteller (z. B. LM Studio)
         || u.Host.Equals("pkgs.tailscale.com", StringComparison.OrdinalIgnoreCase)); // offizielles Tailscale-Setup für den Handy-Zugang unterwegs

    /// <summary>7z mit dem mitgelieferten 7-Zip entpacken (ohne Shell, Ausgabe nur in den Zielordner).</summary>
    private void Extract7z(string archive, string target)
    {
        var seven = Path.Combine(_deps.ToolsRoot, "7zip", "7z.exe");
        if (!File.Exists(seven)) throw new InvalidOperationException("7-Zip fehlt – bitte unter „Programme“ aus dem Vollpaket einrichten.");
        var r = Cli.Run(seven, ["x", archive, "-o" + target, "-y", "-bso0", "-bsp0", "-snl-"], timeoutMs: 3_600_000);
        if (!r.Ok) throw new InvalidOperationException("Entpacken fehlgeschlagen: " + Short(r.Err.Length > 0 ? r.Err : r.Out));
    }

    /// <summary>Lädt eine Datei mit eigenen Weiterleitungen (nur erlaubte Server), Größenkontrolle und Fortschritt. Hugging Face nennt bei großen
    /// Dateien die SHA-256 im Kopf „X-Linked-Etag“ – die wird zurückgegeben und danach geprüft.</summary>
    /// <summary>Lesen mit Abbruch: bei „Abbrechen“ wird die halbe Datei gelöscht.</summary>
    static async Task<int> ReadOrDelete(Stream src, byte[] buf, CancellationToken ct, string partial)
    {
        try { return await src.ReadAsync(buf, ct); }
        catch (OperationCanceledException) { try { File.Delete(partial); } catch (IOException) { } throw; }
    }

    private async Task<(string File, string? Sha256)> KiDownload(string url, string fileName, long bytes, int step, int steps)
    {
        if (KiDownloadOverride is not null) return await KiDownloadOverride(url, fileName);
        var u = new Uri(url); string? sha = null;
        HttpResponseMessage? res = null;
        for (var i = 0; i < 8; i++)
        {
            if (!KiHostAllowed(u)) throw new InvalidOperationException("Unerwartete Download-Adresse: " + u.Host);
            var req = new HttpRequestMessage(HttpMethod.Get, u);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("TrendoOS", "1.0"));
            res = await GitHubHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (res.Headers.TryGetValues("X-Linked-Etag", out var et) && et.FirstOrDefault()?.Trim('"') is { Length: 64 } e && e.All(Uri.IsHexDigit)) sha ??= e.ToLowerInvariant();
            if ((int)res.StatusCode is >= 300 and < 400 && res.Headers.Location is { } loc) { u = loc.IsAbsoluteUri ? loc : new Uri(u, loc); res.Dispose(); res = null; continue; }
            res.EnsureSuccessStatusCode();
            break;
        }
        if (res is null) throw new InvalidOperationException("Zu viele Weiterleitungen.");
        // Herstellerseiten nennen die Größe vorher nicht: dann gilt die Angabe des Servers (höchstens 8 GB)
        if (bytes <= 0) bytes = res.Content.Headers.ContentLength is long len and > 0 ? len : 8L << 30;
        using (res)
        {
            var file = Path.Combine(_deps.ToolsRoot, "ki", ".laden", Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await using var src = await res.Content.ReadAsStreamAsync();
            await using (var dst = File.Create(file))
            {
                var buf = new byte[1 << 20]; long done = 0; int n; var last = DateTime.UtcNow;
                var ct = SetupToken;
                while ((n = await ReadOrDelete(src, buf, ct, file)) > 0)
                {
                    done += n;
                    if (done > bytes) throw new InvalidOperationException($"{fileName} ist größer als angegeben.");
                    await dst.WriteAsync(buf.AsMemory(0, n));
                    if ((DateTime.UtcNow - last).TotalMilliseconds > 300)
                    {
                        last = DateTime.UtcNow; var d = done;
                        Dispatcher.UIThread.Post(() => SetupOverlayProgress(step, steps, $"{fileName}: {d / (1 << 20):N0} von {bytes / (1 << 20):N0} MB"));
                    }
                }
            }
            return (file, sha);
        }
    }

    // ---------- Ausprobieren: jede Art direkt in Trendo ----------

    private string KiOutFolder => WorkFolder("KI-Ergebnisse");

    private Control KiTryPanel(KiLibraryModel m)
    {
        var sp = new StackPanel { Name = "kilib-panel", Spacing = 6 };
        var store = KiStore;
        var exe = store.RuntimeExe(m.Runtime);
        string F(int i) => store.ModelFile(m, m.Files[i]);
        string Out(string ext) => Path.Combine(KiOutFolder, $"{m.Id}-{DateTime.Now:yyyyMMdd-HHmmss}{ext}");
        var status = new TextBlock { Name = "kilib-status", Foreground = Text, TextWrapping = TextWrapping.Wrap, Text = _kiResult is null ? "" : "Ergebnis: " + _kiResult };
        async Task Run(Button b, IReadOnlyList<string> args, string? stdin, string resultFile, string busy)
        {
            b.IsEnabled = false; status.Text = busy;
            var r = KiRunOverride is not null ? await KiRunOverride(exe, args, stdin) : await Task.Run(() => RunWithInput(exe, args, stdin));
            _core.Audit.Append(Principal.LocalOwner.Name, "ki.library.run", $"{m.Id} exit={r.Exit}");
            if (r.Ok && File.Exists(resultFile)) { _kiResult = resultFile; _kiLibMsg = "Fertig. Gespeichert unter: " + resultFile; }
            else _kiLibMsg = "Hat nicht geklappt: " + Short(string.IsNullOrWhiteSpace(r.Err) ? r.Out : r.Err);
            Show("ki-bibliothek");
        }
        TextBox Prompt(string watermark) => new() { Name = "kilib-prompt", PlaceholderText = watermark, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
        switch (m.Kind)
        {
            case KiKind.BildErzeugen:
            {
                var p = Prompt("Beschreibe dein Bild, z. B. „ein Leuchtturm bei Sonnenuntergang, Ölgemälde“");
                var go = new Button { Name = "kilib-run", Content = "Bild erzeugen", Background = Accent };
                go.Click += async (_, _) =>
                {
                    if (string.IsNullOrWhiteSpace(p.Text)) { status.Text = "Bitte zuerst beschreiben, was zu sehen sein soll."; return; }
                    var o = Out(".png"); var steps = m.Id == "sdxl-turbo" ? "4" : "1";
                    await Run(go, ["-m", F(0), "-p", p.Text!, "-o", o, "--steps", steps, "--cfg-scale", "1.0", "-W", "512", "-H", "512"], null, o, "Das Bild entsteht … (je nach PC einige Sekunden bis Minuten)");
                };
                sp.Children.Add(p); sp.Children.Add(go);
                break;
            }
            case KiKind.Video:
            {
                var p = Prompt("Beschreibe die Szene, z. B. „eine Katze läuft durch hohes Gras, Zeitlupe“");
                var go = new Button { Name = "kilib-run", Content = "Video erzeugen", Background = Accent };
                go.Click += async (_, _) =>
                {
                    if (string.IsNullOrWhiteSpace(p.Text)) { status.Text = "Bitte zuerst die Szene beschreiben."; return; }
                    var o = Out(".webm");
                    await Run(go, ["-M", "vid_gen", "--diffusion-model", F(0), "--t5xxl", F(1), "--vae", F(2), "-p", p.Text!, "--cfg-scale", "6.0", "--sampling-method", "euler",
                        "-W", "832", "-H", "480", "--video-frames", "33", "--flow-shift", "3.0", "--offload-to-cpu", "-o", o], null, o, "Das Video entsteht … das kann einige Minuten dauern.");
                };
                sp.Children.Add(p); sp.Children.Add(go);
                break;
            }
            case KiKind.BildVerbessern:
            {
                var go = new Button { Name = "kilib-run", Content = "Bild wählen und 4-fach vergrößern", Background = Accent };
                go.Click += async (_, _) =>
                {
                    var src = await PickMedia("kilib-image", ["*.png", "*.jpg", "*.jpeg", "*.webp"]);
                    if (src is null) return;
                    var o = Out(".png");
                    await Run(go, ["-i", src, "-o", o, "-n", "realesrgan-x4plus"], null, o, "Das Bild wird vergrößert …");
                };
                sp.Children.Add(go);
                break;
            }
            case KiKind.SpracheZuText:
            {
                var go = new Button { Name = "kilib-run", Content = "Audio- oder Videodatei wählen und mitschreiben", Background = Accent };
                go.Click += async (_, _) =>
                {
                    var src = await PickMedia("kilib-audio", ["*.wav", "*.mp3", "*.m4a", "*.ogg", "*.flac", "*.mp4", "*.mkv", "*.webm"]);
                    if (src is null) return;
                    var wav = src;
                    if (!src.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        if (FfmpegPath is not { } ff) { status.Text = "Für dieses Format braucht Trendo FFmpeg (unter „Programme“ einrichten)."; return; }
                        wav = Path.Combine(Path.GetTempPath(), "trendo-whisper-" + Guid.NewGuid().ToString("N") + ".wav");
                        status.Text = "Ton wird vorbereitet …";
                        var c = await Task.Run(() => Cli.Run(ff, ["-y", "-i", src, "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav], timeoutMs: 600_000));
                        if (!c.Ok) { status.Text = "Ton konnte nicht vorbereitet werden: " + Short(c.Err); return; }
                    }
                    var o = Out("");
                    await Run(go, ["-m", F(0), "-f", wav, "-l", "de", "-otxt", "-of", o], null, o + ".txt", "Wird mitgeschrieben …");
                    if (!ReferenceEquals(wav, src)) TryDelete(wav);
                };
                sp.Children.Add(go);
                break;
            }
            case KiKind.Musik:
                sp.Children.Add(MusicTryPanel(m, status));
                break;
            case KiKind.TextZuSprache:
            {
                var p = Prompt("Text zum Vorlesen eingeben");
                var go = new Button { Name = "kilib-run", Content = "Vorlesen (als Audio speichern)", Background = Accent };
                go.Click += async (_, _) =>
                {
                    if (string.IsNullOrWhiteSpace(p.Text)) { status.Text = "Bitte zuerst einen Text eingeben."; return; }
                    var o = Out(".wav");
                    await Run(go, ["--model", F(0), "--output_file", o], p.Text, o, "Die Stimme spricht ein …");
                    if (File.Exists(o)) Launch(o);
                };
                sp.Children.Add(p); sp.Children.Add(go);
                break;
            }
        }
        sp.Children.Add(status);
        if (_kiResult is { } res && File.Exists(res))
        {
            if (res.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || res.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                var play = new Button { Name = "kilib-play-result", Content = "▶ Anhören", Background = Accent }; play.Click += (_, _) => Launch(res);
                sp.Children.Add(play);
            }
            if (res.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                try { sp.Children.Add(new Image { Name = "kilib-image-result", Source = new Bitmap(res), MaxHeight = 360, HorizontalAlignment = HorizontalAlignment.Left }); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException) { }
            else if (res.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                sp.Children.Add(new TextBox { Name = "kilib-text-result", Text = File.ReadAllText(res), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 300 });
            var open = new Button { Name = "kilib-open-result", Content = "Ergebnis öffnen" }; open.Click += (_, _) => Launch(res);
            var folder = new Button { Name = "kilib-open-folder", Content = "Ordner „KI-Ergebnisse“" }; folder.Click += (_, _) => Launch(KiOutFolder);
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { open, folder } });
        }
        return WCard(sp);
    }

    private async Task<string?> PickMedia(string id, string[] patterns)
    {
        var req = new PickRequest(id, false, false, [.. patterns.Select(p => p.TrimStart('*'))]);
        if (PickOverride is { } fake) return (await fake(req)) is [var f, ..] ? f : null;
        var got = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Datei wählen", FileTypeFilter = [new FilePickerFileType("Dateien") { Patterns = patterns }] });
        return got.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    /// <summary>Programm ohne Shell starten, optional Text über die Standardeingabe (Piper liest so). Zeitlimit 30 Minuten (Video).</summary>
    private static Cli.Result RunWithInput(string exe, IReadOnlyList<string> args, string? stdin)
    {
        if (stdin is null) return Cli.Run(exe, args, Path.GetDirectoryName(exe), 1_800_000);
        var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.StandardInputEncoding = new System.Text.UTF8Encoding(false);
        try
        {
            using var p = System.Diagnostics.Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            p.StandardInput.Write(stdin); p.StandardInput.Close();
            if (!p.WaitForExit(600_000)) { try { p.Kill(true); } catch (InvalidOperationException) { } return new(-2, "", "Zeitüberschreitung"); }
            p.WaitForExit();
            return new(p.ExitCode, o.Result, e.Result);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { return new(-1, "", ex.Message); }
    }
}
