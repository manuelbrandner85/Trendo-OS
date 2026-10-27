using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Musik-KI (Stufe 2 der KI-Bibliothek): ACE-Step 1.5 läuft als lokaler Server (nur 127.0.0.1), Trendo startet ihn bei Bedarf,
/// gibt Stil und Liedtext ab und holt das fertige Lied in den Ordner „KI-Ergebnisse“. Der Server endet mit Trendo.</summary>
public partial class MainWindow
{
    private Process? _aceServer;
    private static readonly HttpClient AceHttp = new() { Timeout = TimeSpan.FromMinutes(2) };
    /// <summary>Für Tests: ersetzt den lokalen Server.</summary>
    public AceStepClient? AceClientOverride { get; set; }
    private AceStepClient AceClient => AceClientOverride ?? new AceStepClient(AceHttp);
    /// <summary>Messung des Grafikspeichers (für Tests austauschbar).</summary>
    public Func<(double FreeGb, double TotalGb)?> GpuMemoryProbe { get; set; } = SystemMetrics.GpuMemory;

    /// <summary>Server starten (falls nicht schon da) und warten, bis er antwortet. Beim ersten Mal lädt ACE-Step seine Modelle – das dauert.</summary>
    private async Task<bool> EnsureAceServer(Action<string> say)
    {
        var client = AceClient;
        if (await client.HealthyAsync()) return true;
        if (AceClientOverride is not null) return false;
        var dir = KiStore.RuntimeDir(KiRuntime.AceStep);
        var (file, args) = AceStepClient.ServerCommand(dir);
        if (!File.Exists(file)) { say("ACE-Step ist noch nicht eingerichtet."); return false; }
        if (_aceServer is null || _aceServer.HasExited)
        {
            var psi = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            foreach (var (k, v) in AceStepClient.ServerEnvironment()) psi.Environment[k] = v;
            var log = Path.Combine(KiStore.Root, "acestep-server.log");
            _aceServer = Process.Start(psi);
            if (_aceServer is null) { say("ACE-Step konnte nicht gestartet werden."); return false; }
            void Log(string? l) { if (l is null) return; try { File.AppendAllText(log, l + Environment.NewLine); } catch (IOException) { } }
            _aceServer.OutputDataReceived += (_, e) => Log(e.Data); _aceServer.ErrorDataReceived += (_, e) => Log(e.Data);
            _aceServer.BeginOutputReadLine(); _aceServer.BeginErrorReadLine();
            Closed += (_, _) => { try { if (_aceServer is { HasExited: false }) _aceServer.Kill(true); } catch (InvalidOperationException) { } };
            _core.Audit.Append(Principal.LocalOwner.Name, "ki.musik.server", $"pid={_aceServer.Id}");
        }
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < TimeSpan.FromMinutes(40))
        {
            if (_aceServer.HasExited) { say("ACE-Step wurde beendet. Einzelheiten: ki/acestep-server.log im Trendo-Werkzeugordner."); return false; }
            if (await client.HealthyAsync()) return true;
            say($"ACE-Step startet … {(int)(DateTime.UtcNow - start).TotalMinutes} min (beim ersten Mal werden die Modelle geladen, ca. 10 GB)");
            await Task.Delay(3000);
        }
        say("ACE-Step antwortet nicht. Bitte später erneut versuchen.");
        return false;
    }

    private Control MusicTryPanel(KiLibraryModel m, TextBlock status)
    {
        var sp = new StackPanel { Spacing = 6 };
        var style = new TextBox { Name = "kilib-prompt", PlaceholderText = "Stil beschreiben, z. B. „fröhlicher Pop mit Klavier und warmer Frauenstimme, 120 BPM“", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 50 };
        var lyrics = new TextBox { Name = "kilib-lyrics", PlaceholderText = "Liedtext (optional). Leer = instrumental.\n[Verse]\n…\n[Chorus]\n…", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110 };
        var len = new ComboBox { Name = "kilib-length", ItemsSource = new[] { "30 Sekunden", "1 Minute", "2 Minuten", "3 Minuten" }, SelectedIndex = 1, Width = 150 };
        var lang = new ComboBox { Name = "kilib-language", ItemsSource = new[] { "Deutsch", "Englisch" }, SelectedIndex = 0, Width = 120 };
        var go = new Button { Name = "kilib-run", Content = "Musik erzeugen", Background = Accent };
        var warned = false;
        go.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(style.Text)) { status.Text = "Bitte zuerst den Stil beschreiben."; return; }
            // Grafikspeicher prüfen: ist er voll, erst verständlich warnen; zweiter Klick startet trotzdem
            if (!warned && AceStepClient.VramAdvice(await Task.Run(GpuMemoryProbe)) is { } advice)
            {
                warned = true; status.Text = advice; go.Content = "Trotzdem erzeugen (langsam)";
                _core.Audit.Append(Principal.LocalOwner.Name, "ki.musik.vram", "zu wenig frei");
                return;
            }
            go.IsEnabled = false;
            var seconds = len.SelectedIndex switch { 0 => 30, 2 => 120, 3 => 180, _ => 60 };
            var song = new AceStepClient.Song(style.Text!, lyrics.Text ?? "", seconds, lang.SelectedIndex == 1 ? "en" : "de");
            try
            {
                if (!await EnsureAceServer(t => status.Text = t)) { go.IsEnabled = true; return; }
                var client = AceClient;
                status.Text = "Das Lied entsteht …";
                var task = await client.SubmitAsync(song);
                _core.Audit.Append(Principal.LocalOwner.Name, "ki.musik.start", $"{seconds}s {song.Language}");
                var t0 = DateTime.UtcNow;
                while (true)
                {
                    await Task.Delay(AceClientOverride is null ? 3000 : 10);
                    var st = await client.QueryAsync(task);
                    if (st.Status == 2) throw new InvalidOperationException(st.Error ?? "Erzeugen fehlgeschlagen.");
                    if (st.Status == 1 && st.AudioUrl is { } url)
                    {
                        var o = Path.Combine(KiOutFolder, $"musik-{DateTime.Now:yyyyMMdd-HHmmss}.mp3");
                        await client.DownloadAsync(url, o);
                        _kiResult = o; _kiLibMsg = "Fertig. Gespeichert unter: " + o;
                        _core.Audit.Append(Principal.LocalOwner.Name, "ki.musik.done", Path.GetFileName(o));
                        Show("ki-bibliothek");
                        return;
                    }
                    status.Text = $"Das Lied entsteht … {(int)(DateTime.UtcNow - t0).TotalSeconds} s";
                    if (DateTime.UtcNow - t0 > TimeSpan.FromMinutes(30)) throw new InvalidOperationException("Das dauert ungewöhnlich lange – bitte erneut versuchen.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or TaskCanceledException or System.Text.Json.JsonException)
            {
                status.Text = "Hat nicht geklappt: " + ex.Message;
                go.IsEnabled = true;
            }
        };
        sp.Children.Add(style); sp.Children.Add(lyrics);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = "Länge:", Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, len, new TextBlock { Text = "Sprache:", Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, lang, go } });
        return sp;
    }
}
