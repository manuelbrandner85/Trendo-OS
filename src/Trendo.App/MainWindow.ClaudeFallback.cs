using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Claude ist nie weg (Wunsch 02.11.2026): Ist das Abo-Kontingent aufgebraucht, übernimmt sofort eine kostenlose KI – zuerst die stärksten Internet-KIs
/// mit kostenlosem Konto, dann die KI auf dem PC –, und sobald das Abo wieder frei ist, geht die nächste Nachricht wieder an Claude. Welche KI gerade arbeitet,
/// steht immer oben im Chat und unter jeder Antwort. Dazu die kostenlosen Medien-KIs der KI-Bibliothek als Werkzeuge (Claude bestellt sie, oder per Knopf).</summary>
public partial class MainWindow
{
    public ClaudeLimit CLimit => new(_core.DataRoot);
    /// <summary>Für Tests: Ersatz-KI nachbilden (Auftrag, Anweisungen → Antwort).</summary>
    public static Func<string, string, CancellationToken, Task<KiAnswer>>? ClaudeFallbackOverride { get; set; }
    /// <summary>Für Tests: KI-Werkzeug nachbilden (Art, Eingabe, Zieldatei → geklappt).</summary>
    public static Func<string, string, string, Task<bool>>? KiToolOverride { get; set; }
    /// <summary>Für Tests: Diktat nachbilden (liefert den erkannten Text).</summary>
    public static Func<Task<string?>>? DictateOverride { get; set; }
    public const string ClaudeKi = "✳ Claude (dein Abo)";

    /// <summary>Anzeigename einer KI: Internet-KIs mit 🌐, KI auf dem PC mit 💻.</summary>
    static string KiLabel(KiAnswer a, IReadOnlyList<IKiBackend> backends)
    {
        var remote = backends.FirstOrDefault(b => b.Name == a.Backend) is IRemoteKiBackend;
        return $"{(remote ? "🌐" : "💻")} {a.Backend}{(a.Model is { Length: > 0 } m ? " · " + m : "")} ({(remote ? "kostenlos im Internet" : "auf diesem PC")})";
    }

    /// <summary>Ersatz-KI ohne Claude: Internet-KIs zuerst, dann die KI auf dem PC; Änderungen wie beim Trendo-Agenten (vollständige Dateien im Projekt).</summary>
    async Task<(string Text, string Ki, IReadOnlyList<AgentChange> Changes, string? Error)> ClaudeFallbackRun(string prompt, string system, string folder, CancellationToken ct)
    {
        var all = DefaultKiBackends().Where(b => b is not IMainKiBackend).OrderBy(b => b is IRemoteKiBackend ? 0 : 1).ToList();
        KiAnswer a;
        var ask = ClaudeFallback.Prompt(prompt, system, folder);
        if (ClaudeFallbackOverride is { } o) a = await o(ask, system, ct);
        else
        {
            var core = new IntelligenceCore(_core.DataRoot, _core.Audit, () => all);
            a = await core.AskAsync(new KiRequest(ask, "Du bist die Ersatz-KI im Claude-Bereich von Trendo OS. Antworte auf Deutsch.", UseKnowledge: false, AllowRemote: true), ct);
        }
        if (!a.Ok) return ("", "–", [], "Auch keine Ersatz-KI hat geantwortet: " + a.Text + " – richte auf der KI-Seite eine kostenlose Internet-KI ein oder lade eine KI für diesen PC.");
        var changes = TrendoAgent.ParseChanges(a.Text, folder);
        if (changes.Count > 0) TrendoAgent.Apply(folder, changes);
        var tools = ClaudeTools.Parse(ClaudeFallback.ToolText(a.Text)).Select(t => $"[[trendo:{t.Kind} | {t.Input} | {t.File}]]");
        var summary = ClaudeFallback.Summary(a.Text);
        return (summary + (tools.Any() ? "\n" + string.Join("\n", tools) : ""), KiLabel(a, all), changes, null);
    }

    /// <summary>Werkzeug-Bestellungen ausführen (Claude oder Ersatz-KI): jede Datei landet im Projekt; gibt die Schritte für den Verlauf zurück.</summary>
    async Task<List<ClaudeChats.Step>> ClaudeRunTools(string answer, string folder)
    {
        var steps = new List<ClaudeChats.Step>();
        foreach (var call in ClaudeTools.Parse(answer))
        {
            var (ok, msg) = await KiToolRun(call.Kind, call.Input, folder, call.File);
            steps.Add(new ClaudeChats.Step(ok ? $"{ClaudeTools.Kinds[call.Kind].Label}: {call.File}" : msg, ok, !ok));
        }
        return steps;
    }

    /// <summary>Ein kostenloses KI-Werkzeug der KI-Bibliothek ausführen (auf dem PC). Fehlt das Modell, sagt Trendo, wo es sich einrichten lässt.</summary>
    public async Task<(bool Ok, string Msg)> KiToolRun(string kind, string input, string folder, string file)
    {
        if (!ClaudeTools.Kinds.TryGetValue(kind, out var info)) return (false, $"Unbekanntes Werkzeug „{kind}“.");
        var target = Path.GetFullPath(Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)) return (false, "Zieldatei liegt nicht im Projekt.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string Src() => Path.GetFullPath(Path.Combine(folder, input.Replace('/', Path.DirectorySeparatorChar)));
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.werkzeug", $"{kind} {Path.GetFileName(target)}");
        if (KiToolOverride is { } o) return await o(kind, input, target) ? (true, "") : (false, $"{info.Label} hat nicht geklappt.");
        if (_opt.Isolated) return (false, $"Testmodus: {info.Label} wird nicht ausgeführt.");
        var store = KiStore;
        var m = KiLibrary.Models.FirstOrDefault(x => x.Kind == info.Kind && store.ModelReady(x) && store.RuntimeReady(x.Runtime));
        if (m is null) return (false, $"Für „{info.Label}“ ist noch keine kostenlose KI eingerichtet – in der KI-Bibliothek mit einem Klick (KI → Bibliothek).");
        var exe = store.RuntimeExe(m.Runtime); string F(int i) => store.ModelFile(m, m.Files[i]);
        Cli.Result r;
        switch (kind)
        {
            case "bild":
                r = await Task.Run(() => RunWithInput(exe, ["-m", F(0), "-p", input, "-o", target, "--steps", m.Id == "sdxl-turbo" ? "4" : "1", "--cfg-scale", "1.0", "-W", "512", "-H", "512"], null)); break;
            case "video":
                r = await Task.Run(() => RunWithInput(exe, ["-M", "vid_gen", "--diffusion-model", F(0), "--t5xxl", F(1), "--vae", F(2), "-p", input, "--cfg-scale", "6.0", "--sampling-method", "euler", "-W", "832", "-H", "480", "--video-frames", "33", "--flow-shift", "3.0", "--offload-to-cpu", "-o", target], null)); break;
            case "verbessern":
                if (!File.Exists(Src())) return (false, $"Bild „{input}“ gibt es im Projekt nicht.");
                r = await Task.Run(() => RunWithInput(exe, ["-i", Src(), "-o", target, "-n", "realesrgan-x4plus"], null)); break;
            case "vorlesen":
                r = await Task.Run(() => RunWithInput(exe, ["--model", F(0), "--output_file", target], input)); break;
            case "mitschreiben":
                if (!File.Exists(Src())) return (false, $"Aufnahme „{input}“ gibt es im Projekt nicht.");
                var outBase = Path.ChangeExtension(target, null);
                r = await Task.Run(() => RunWithInput(exe, ["-m", F(0), "-f", Src(), "-l", "de", "-otxt", "-of", outBase], null));
                if (r.Ok && File.Exists(outBase + ".txt") && !File.Exists(target)) File.Move(outBase + ".txt", target);
                break;
            case "musik":
                try
                {
                    if (!await EnsureAceServer(_ => { })) return (false, "Die Musik-KI ließ sich nicht starten.");
                    var client = AceClient; var task = await client.SubmitAsync(new AceStepClient.Song(input, "", 60, "de"));
                    for (var t0 = DateTime.UtcNow; DateTime.UtcNow - t0 < TimeSpan.FromMinutes(30); await Task.Delay(3000))
                    {
                        var st = await client.QueryAsync(task);
                        if (st.Status == 2) return (false, "Musik: " + (st.Error ?? "Erzeugen fehlgeschlagen."));
                        if (st.Status == 1 && st.AudioUrl is { } url) { await client.DownloadAsync(url, target); return (true, ""); }
                    }
                    return (false, "Musik: dauert ungewöhnlich lange.");
                }
                catch (Exception e) when (e is HttpRequestException or InvalidOperationException or IOException or TaskCanceledException or System.Text.Json.JsonException) { return (false, "Musik: " + e.Message); }
            default: return (false, "Unbekanntes Werkzeug.");
        }
        return r.Ok && File.Exists(target) ? (true, "") : (false, $"{info.Label} hat nicht geklappt: " + Short(string.IsNullOrWhiteSpace(r.Err) ? r.Out : r.Err));
    }

    /// <summary>Werkzeugleiste über dem Eingabefeld: Bild, Video, Verbessern, Musik, Vorlesen, Diktieren – kostenlos auf dem PC.</summary>
    Control ClaudeToolBar(ClaudeDock dock)
    {
        var row = new WrapPanel { Name = "claude-tools", Margin = new Thickness(24, 0, 24, 4) };
        void B(string kind, string label, string tip, Func<Task> a)
        {
            var b = new Button { Name = "claude-tool-" + kind, Content = label, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 4), CornerRadius = new CornerRadius(12), Background = ClaudeItem, Foreground = ClaudeFg, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(b, tip); b.Click += async (_, _) => await a(); row.Children.Add(b);
        }
        B("bild", "🎨 Bild", "Bild aus dem Text im Eingabefeld erzeugen (kostenlos, auf dem PC)", () => ClaudeToolButton(dock, "bild"));
        B("video", "🎬 Video", "Kurzes Video aus dem Text im Eingabefeld (starke Grafikkarte nötig)", () => ClaudeToolButton(dock, "video"));
        B("verbessern", "✨ Verbessern", "Ein Bild wählen und 4-fach vergrößern", () => ClaudeToolButton(dock, "verbessern"));
        B("musik", "🎵 Musik", "Musikstück nach der Beschreibung im Eingabefeld", () => ClaudeToolButton(dock, "musik"));
        B("vorlesen", "🔊 Vorlesen", "Claudes letzte Antwort vorlesen", () => ClaudeToolButton(dock, "vorlesen"));
        B("diktieren", "🎙 Diktieren", "Sprechen statt tippen – auf dem PC erkannt, ohne Cloud", () => ClaudeDictate(dock));
        return row;
    }

    /// <summary>Werkzeug per Knopf (öffentlich für Tests über das aktive Fenster).</summary>
    public Task ClaudeToolButton(string kind) => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? ClaudeToolButton(d, kind) : Task.CompletedTask;

    async Task ClaudeToolButton(ClaudeDock dock, string kind)
    {
        if (!_chatUis.TryGetValue(dock, out var ui) || ui.Running) return;
        var studio = ChatStudio(dock); var chats = CChats;
        var chat = ui.ChatId is { } cid ? chats.Get(studio, cid) : null;
        string input;
        if (kind == "vorlesen") input = chat?.Messages.LastOrDefault(m => m.Role == "assistant")?.Text is { Length: > 0 } t ? ClaudeTools.Strip(t) : "";
        else if (kind == "verbessern")
        {
            var got = PickOverride is { } fake ? await fake(new PickRequest("claude-tool-verbessern", false, false, ["png", "jpg", "jpeg", "webp"])) : null;
            if (got is null && PickOverride is null) got = (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Bild zum Verbessern" })).Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToArray();
            input = got is { Length: > 0 } ? got[0] : "";
        }
        else input = ui.Input.Text?.Trim() ?? "";
        if (input.Length == 0) { ui.Input.PlaceholderText = kind == "vorlesen" ? "Noch keine Antwort zum Vorlesen." : "Beschreib zuerst hier, was entstehen soll – dann noch einmal auf den Knopf."; ui.Input.Focus(); return; }
        if (chat is null) { chat = chats.Create(studio, ClaudeChats.TitleOf(kind == "vorlesen" ? "Vorlesen" : input), ClaudeChatRoot(dock.Studio), ClaudeStudioProjectFolder(dock)); ui.ChatId = chat.Id; }
        var label = ClaudeTools.Kinds[kind].Label;
        if (kind == "verbessern") { var dst = Path.Combine(chat.Folder, "anhang", Path.GetFileName(input)); Directory.CreateDirectory(Path.GetDirectoryName(dst)!); if (!File.Exists(dst)) File.Copy(input, dst); input = "anhang/" + Path.GetFileName(dst); }
        var file = $"medien/{kind}-{DateTime.Now:yyyyMMdd-HHmmss}{ClaudeTools.Kinds[kind].Ext}";
        chats.Add(studio, chat.Id, new ClaudeChats.Msg("user", (kind is "bild" or "video" or "musik" ? $"{label}: " : "") + (kind == "vorlesen" ? "Letzte Antwort vorlesen" : input), [], DateTimeOffset.Now));
        ui.Input.Text = ""; ui.Running = true; RenderChatList(dock); RenderChat(dock);
        ui.Log.Children.Add(new TextBlock { Text = $"⏳ {label} … (kostenlos auf diesem PC)", Foreground = ClaudeAccent, FontSize = 13 });
        var (ok, msg) = await KiToolRun(kind, input, chat.Folder, file);
        ui.Running = false;
        var full = Path.Combine(chat.Folder, file.Replace('/', Path.DirectorySeparatorChar));
        chats.Add(studio, chat.Id, new ClaudeChats.Msg("assistant", ok ? $"{label}: fertig – {file}" : "⚠ " + msg, ok ? [new ClaudeChats.Step($"{label}: {file}", true)] : [], DateTimeOffset.Now, ok ? [file + " (neu)"] : null, 0, null, null, "🆓 KI-Bibliothek (auf diesem PC)"));
        RenderChatList(dock); RenderChat(dock);
        if (ok)
        {
            if (StudioFormat.PreviewKind(full) != "none") ShowPreview(dock, full);
            if (kind == "vorlesen" && !_opt.Isolated) Launch(full);
        }
    }

    /// <summary>Diktieren: einmal klicken startet die Aufnahme (Mikrofon, FFmpeg), noch einmal klicken beendet sie – der erkannte Text steht dann im Eingabefeld.</summary>
    Process? _dictation; string? _dictationFile;
    public Task ClaudeDictate() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? ClaudeDictate(d) : Task.CompletedTask;
    async Task ClaudeDictate(ClaudeDock dock)
    {
        if (!_chatUis.TryGetValue(dock, out var ui)) return;
        var btn = dock.Panel.GetLogicalDescendantsOf<Button>().FirstOrDefault(b => b.Name == "claude-tool-diktieren");
        if (DictateOverride is { } o) { if (await o() is { Length: > 0 } t) ui.Input.Text = ((ui.Input.Text ?? "") + " " + t).Trim(); return; }
        if (_opt.Isolated) { ui.Input.PlaceholderText = "Testmodus: Diktieren nimmt nichts auf."; return; }
        if (_dictation is null)
        {
            if (FfmpegPath is not { } ff) { ui.Input.PlaceholderText = "Zum Diktieren braucht Trendo FFmpeg (unter „Programme“ einrichten)."; return; }
            var mic = DesktopCapture.Devices(ff).Microphones.FirstOrDefault();
            if (mic is null) { ui.Input.PlaceholderText = "Kein Mikrofon gefunden."; return; }
            _dictationFile = Path.Combine(Path.GetTempPath(), "trendo-diktat-" + Guid.NewGuid().ToString("N") + ".wav");
            var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
            foreach (var a in new[] { "-hide_banner", "-y", "-f", "dshow", "-i", "audio=" + mic.Id, "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", _dictationFile }) psi.ArgumentList.Add(a);
            _dictation = Process.Start(psi); _dictation?.BeginErrorReadLine();
            if (btn is not null) btn.Content = "⏹ Fertig";
            ui.Input.PlaceholderText = "🎙 Ich höre zu … zum Beenden auf „Fertig“ klicken.";
            return;
        }
        try { _dictation.StandardInput.Write('q'); _dictation.StandardInput.Flush(); await _dictation.WaitForExitAsync(new CancellationTokenSource(5000).Token); } catch (Exception e) when (e is IOException or InvalidOperationException or OperationCanceledException) { try { _dictation.Kill(); } catch (InvalidOperationException) { } }
        _dictation = null; if (btn is not null) btn.Content = "🎙 Diktieren";
        if (_dictationFile is not { } wav || !File.Exists(wav)) { ui.Input.PlaceholderText = "Die Aufnahme hat nicht geklappt."; return; }
        ui.Input.PlaceholderText = "Wird erkannt …";
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-diktat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp); File.Move(wav, Path.Combine(tmp, "diktat.wav"));
        var (ok, msg) = await KiToolRun("mitschreiben", "diktat.wav", tmp, "diktat.txt");
        var txt = Path.Combine(tmp, "diktat.txt");
        if (ok && File.Exists(txt)) ui.Input.Text = ((ui.Input.Text ?? "") + " " + File.ReadAllText(txt).Trim()).Trim();
        else ui.Input.PlaceholderText = msg;
        try { Directory.Delete(tmp, true); } catch (IOException) { }
    }
}
