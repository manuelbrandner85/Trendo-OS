using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 3 (T-0614): Videos mit KI, KI-Agenten bauen, KI-Modelle vergleichen, Eigene KI trainieren.
/// Nutzt Vorhandenes: lokale Bild-KI wie „Bilder mit KI“ (<see cref="CoverModel"/>, <see cref="ImageAiKit"/>), FFmpeg (<see cref="FfmpegPath"/>),
/// die eingerichteten KI-Quellen (<see cref="DefaultKiBackends"/> über <see cref="IntelligenceCore"/>), eigene Rollen des KI-Teams (<see cref="AgentCenter.AddCustomRole"/>),
/// Claude über den Zugang der Person (<see cref="ClaudeFromStudioBar"/>) und die kie.ai-Leiste.</summary>
public partial class MainWindow
{
    const string VideoAiKey = "video-ki-programm", AgentsKey = "agenten-bauen-programm", ModelsKey = "modelle-vergleichen-programm", TrainKey = "ki-trainieren-programm";

    private void InitKreativStudios3() { InitVideoAi(); InitAgentsStudio(); InitModels(); InitTrain(); }

    static readonly IBrush WarnBrush = Brush.Parse("#F5A524"), ErrBrush = Brush.Parse("#F87171"), OkBrush = Brush.Parse("#4ADE80");
    static IBrush StatusBrush(string msg) => msg.StartsWith('✕') ? ErrBrush : msg.StartsWith('⚠') ? WarnBrush : msg.StartsWith('✓') ? OkBrush : Muted;

    /// <summary>Feld, das beim Verlassen übernimmt (für Listenzeilen, deren Wert nicht direkt im Dokument liegt).</summary>
    TextBox KrField(string key, string name, string value, string placeholder, Action<string> apply, bool multi = false, double width = double.NaN)
    {
        var t = new TextBox { Name = name, Text = value, PlaceholderText = placeholder, Width = width, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap };
        void Commit() { if ((t.Text ?? "") != value) { var v = t.Text ?? ""; value = v; ProgramRun(key, () => apply(v)); } }
        t.LostFocus += (_, _) => Commit();
        OnEnter(t, Commit);
        return t;
    }

    static string Sha256File(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)); }
    static string Sha256Text(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    async Task ClaudeOrHint(string key, string studio, string text)
    {
        if (!await ClaudeFromStudioBar(studio, text)) { _progMsg[key] = "Claude ist gerade nicht verbunden. Unter „KI-Quellen“ verbindest du Claude mit deinem eigenen Zugang."; Show(key); }
    }

    // ---------------- Videos mit KI ----------------

    const string VideoSample = "Ein alter Leuchtturm auf einer Klippe im Morgenlicht, Zoom rein, 5 s, Text: Ein neuer Tag\nMöwen fliegen über das ruhige Meer, Schwenk nach rechts, 4 s\nEin kleines Fischerboot im Hafen, ruhig, 4 s\nSonnenstrahlen auf den Wellen, Zoom raus, 5 s, Text: Guten Morgen";

    static JsonObject VideoDoc(string idea) => new() { ["dir"] = Guid.NewGuid().ToString("N"), ["idea"] = idea, ["format"] = "quer", ["style"] = "foto", ["seed"] = "42", ["scenes"] = new JsonArray(), ["videos"] = new JsonArray() };

    static JsonObject SceneJson(VideoAiKit.Scene s, string source = "", int seed = 0) => new() { ["prompt"] = s.Prompt, ["sec"] = s.Seconds, ["motion"] = s.Motion, ["caption"] = s.Caption, ["image"] = s.Image, ["source"] = source, ["seed"] = seed };

    /// <summary>Eigener Bildordner des Dokuments. Die Kennung steht im Dokument, damit die Bilder nach dem Wiederöffnen dazugehören; fremde Werte (Pfade) zählen nicht.</summary>
    string VideoDir(ProgramDocument d) => Path.Combine(_core.DataRoot, "ki-videos", System.Text.RegularExpressions.Regex.IsMatch(d.Text("dir"), "^[0-9a-f]{32}$") ? d.Text("dir") : d.Id);

    /// <summary>Nur Bilder im eigenen Ordner dieses Dokuments (höchstens 25 MB) – eine fremde Projektdatei kann so keine anderen Dateien ins Video ziehen.</summary>
    bool VideoOwn(ProgramDocument d, string path)
    {
        if (!KreativSafe.LocalFile(path, KreativSafe.MaxImageBytes)) return false;
        var own = Path.GetFullPath(VideoDir(d)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(own, StringComparison.OrdinalIgnoreCase);
    }

    List<VideoAiKit.Scene> VideoScenes(ProgramDocument d) => d.List("scenes").Select(n => n as JsonObject ?? new JsonObject()).Select(o =>
    {
        var motion = Js(o["motion"]); var img = Js(o["image"]);
        return new VideoAiKit.Scene(Js(o["prompt"]), Math.Clamp(ProgramDocument.ToNumber(o["sec"], VideoAiKit.DefaultScene), VideoAiKit.MinScene, VideoAiKit.MaxScene),
            VideoAiKit.Motions.Any(m => m.Id == motion) ? motion : "zoom-rein", KreativSafe.Cut(Js(o["caption"]), 200), img.Length > 0 && VideoOwn(d, img) ? img : "");
    }).ToList();

    static void VideoEditScene(ProgramDocument d, int i, string label, Action<JsonObject> change) =>
        d.Edit(label, x => { if (x["scenes"] is JsonArray a && i >= 0 && i < a.Count && a[i] is JsonObject o) change(o); });

    void VideoPlan(ProgramDocument d)
    {
        if (_videoBusy) throw new InvalidOperationException("Es wird gerade gemalt oder gebaut – einen Moment.");
        var scenes = VideoAiKit.Plan(d.Text("idea"));
        if (scenes.Count == 0) throw new InvalidOperationException("Schreib zuerst, was im Video zu sehen sein soll – je Zeile eine Szene.");
        var old = d.List("scenes").OfType<JsonObject>().ToList();
        // gleiche Beschreibung an gleicher Stelle: das Bild bleibt
        d.Edit("Szenen geplant", x => x["scenes"] = new JsonArray(scenes.Select((s, i) =>
            (JsonNode)(i < old.Count && Js(old[i]["prompt"]) == s.Prompt ? SceneJson(s with { Image = Js(old[i]["image"]) }, Js(old[i]["source"]), (int)ProgramDocument.ToNumber(old[i]["seed"], 0)) : SceneJson(s))).ToArray()));
        var total = VideoAiKit.Total(scenes.Select(s => s.Seconds).ToList());
        _progMsg[VideoAiKey] = $"{scenes.Count} Szenen geplant, etwa {total.ToString("0.#", De)} Sekunden." + (total > VideoAiKit.MaxTotal ? " Das ist länger als 60 Sekunden – bitte kürzen." : " Jetzt „Bilder malen“ oder eigene Bilder wählen.");
    }

    /// <summary>Tests: Video bauen statt FFmpeg (Argumente, Ziel → erfolgreich?).</summary>
    public static Func<IReadOnlyList<string>, string, bool>? VideoAiFfmpegOverride { get; set; }
    bool _videoBusy;

    /// <summary>Malt jedes fehlende Standbild mit der lokalen Bild-KI – derselbe Weg wie „Bilder mit KI“ (Test: <see cref="ImageAiOverride"/>).</summary>
    public async Task<int> VideoImages(ProgramDocument d)
    {
        if (_videoBusy) { _progMsg[VideoAiKey] = "Es wird schon gearbeitet – einen Moment."; Show(VideoAiKey); return 0; }
        var scenes = VideoScenes(d);
        if (scenes.Count == 0) { _progMsg[VideoAiKey] = "Plane zuerst die Szenen."; Show(VideoAiKey); return 0; }
        var todo = scenes.Select((s, i) => (s, i)).Where(x => x.s.Image.Length == 0).ToList();
        if (todo.Count == 0) { _progMsg[VideoAiKey] = "✓ Jede Szene hat schon ein Bild."; Show(VideoAiKey); return 0; }
        if (todo.FirstOrDefault(x => ImageAiKit.Blocked(x.s.Prompt) is not null) is { s: not null } bad) { _progMsg[VideoAiKey] = $"Szene {bad.i + 1}: {ImageAiKit.Blocked(bad.s.Prompt)}"; Show(VideoAiKey); return 0; }
        var cm = ImageAiOverride is null ? CoverModel() : null;
        if (ImageAiOverride is null && (cm is null || _opt.Isolated)) { _progMsg[VideoAiKey] = "Die Bild-KI ist noch nicht eingerichtet. Oben „Bild-KI einrichten“ öffnet die KI-Bibliothek – oder wähle je Szene ein eigenes Bild."; Show(VideoAiKey); return 0; }
        var xl = cm?.Model.Id != "sd-turbo";
        var fmt = d.Text("format");
        var (w, h) = ImageAiKit.Size(fmt == "hoch" ? "hoch" : fmt == "quadrat" ? "quadrat" : "quer", xl ? 1024 : 512);
        var baseSeed = int.TryParse(d.Text("seed"), out var sd) ? sd : 42;
        var style = d.Text("style");
        var dir = VideoDir(d); Directory.CreateDirectory(dir);
        _videoBusy = true; var made = 0;
        try
        {
            foreach (var (s, i) in todo)
            {
                var seed = ImageAiKit.Seed(baseSeed, i);
                _progMsg[VideoAiKey] = $"Male Bild für Szene {i + 1} ({made + 1} von {todo.Count}) …"; Show(VideoAiKey);
                var png = Path.Combine(dir, $"szene-{i + 1}-{DateTime.Now:yyyyMMdd-HHmmss}-{seed}.png");
                bool ok;
                using (await GpuSlot("bild", "Bild für ein Video"))
                {
                    if (ImageAiOverride is { } o) ok = await o(ImageAiKit.FullPrompt(s.Prompt, style), seed, png, w, h);
                    else
                    {
                        var args = ImageAiKit.Args(KiStore.ModelFile(cm!.Value.Model, cm.Value.Model.Files[0]), s.Prompt, style, "", png, w, h, seed, xl);
                        var r = await Task.Run(() => RunWithInput(cm.Value.Exe, args, null));
                        if (!r.Ok || !File.Exists(png)) r = await Task.Run(() => RunWithInput(cm.Value.Exe, [.. args, "--offload-to-cpu"], null));
                        ok = r.Ok;
                    }
                }
                if (!ok || !File.Exists(png)) { _progMsg[VideoAiKey] = "Die Bild-KI hat kein Bild geliefert. Ist die Grafikkarte gerade mit etwas anderem beschäftigt?"; break; }
                ImageAiKit.WriteProvenance(png, new("stable-diffusion.cpp", cm?.Model.Id ?? "test", s.Prompt, style, "", seed, w, h, DateTime.UtcNow, ""));
                VideoEditScene(d, i, "Bild gemalt", o => { if (Js(o["prompt"]) == s.Prompt) { o["image"] = png; o["source"] = "ki"; o["seed"] = seed; } });
                made++;
            }
            if (made > 0)
            {
                _core.Audit.Append(Principal.LocalOwner.Name, "video-ki.bilder", $"{made} Bilder");
                if (made == todo.Count) _progMsg[VideoAiKey] = $"✓ {made} Bild(er) fertig. Jetzt „Video bauen“.";
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _progMsg[VideoAiKey] = ex.Message; }
        finally { _videoBusy = false; Show(VideoAiKey); }
        return made;
    }

    /// <summary>Eigenes Bild für eine Szene: wird in den Ordner des Dokuments kopiert, das Original bleibt unberührt.</summary>
    public async Task VideoOwnImage(ProgramDocument d, int i)
    {
        if (await PickMediaFile("video-bild", "Bild (PNG oder JPEG)", ["*.png", "*.jpg", "*.jpeg"]) is not { } f) return;
        ProgramRun(VideoAiKey, () =>
        {
            if (!KreativSafe.LocalFile(f, KreativSafe.MaxImageBytes)) throw new ArgumentException("Bitte ein Bild auf diesem Rechner wählen (höchstens 25 MB).");
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg")) throw new ArgumentException("Bitte ein PNG- oder JPEG-Bild.");
            var dir = VideoDir(d); Directory.CreateDirectory(dir);
            var dst = Path.Combine(dir, $"eigen-{i + 1}-{DateTime.Now:yyyyMMdd-HHmmss-fff}{ext}");
            File.Copy(f, dst, overwrite: false);
            VideoEditScene(d, i, "Eigenes Bild", o => { o["image"] = dst; o["source"] = "eigen"; o["seed"] = 0; });
            _progMsg[VideoAiKey] = $"Szene {i + 1}: eigenes Bild übernommen.";
        });
    }

    /// <summary>Einblendung als durchsichtiges Bild in Videogröße: dunkler Streifen unten mit weißer Schrift. Bleibt beim Zoomen ruhig stehen.</summary>
    static void VideoCaptionPng(string text, int w, int h, string file)
    {
        var size = Math.Max(18, Math.Min(w, h) / 16.0);
        var band = new Border
        {
            Background = Brush.Parse("#A6000000"), CornerRadius = new CornerRadius(size / 3), Padding = new Thickness(size * 0.8, size * 0.4),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(w * 0.06, 0, w * 0.06, h * 0.08),
            Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = size, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
        };
        var root = new Grid { Width = w, Height = h, Background = Brushes.Transparent, Children = { band } };
        root.Measure(new Size(w, h)); root.Arrange(new Rect(0, 0, w, h));
        using var bmp = new RenderTargetBitmap(new PixelSize(w, h));
        bmp.Render(root);
        bmp.Save(file);
    }

    /// <summary>Baut das MP4 mit FFmpeg: Kamerafahrt je Szene, Überblendung, Einblendungen. Nie überschreiben, Herkunftsnachweis daneben.</summary>
    public async Task<string?> VideoBuild(ProgramDocument d, string? output = null)
    {
        if (_videoBusy) { _progMsg[VideoAiKey] = "Es wird schon gearbeitet – einen Moment."; Show(VideoAiKey); return null; }
        var ff = VideoAiFfmpegOverride is null ? FfmpegPath : "ffmpeg";
        if (ff is null) { _progMsg[VideoAiKey] = "Für das Video fehlt FFmpeg. Du richtest es im Studio „Videos schneiden“ mit einem Klick ein."; Show(VideoAiKey); return null; }
        var scenes = VideoScenes(d);
        var sources = d.List("scenes").Select(n => Js((n as JsonObject)?["source"]) is { Length: > 0 } x ? x : "eigen").ToList(); // vor dem Start festhalten
        var problems = VideoAiKit.Check(scenes, needImages: true).Where(x => x.StartsWith('✕') || x.Contains("Noch kein Bild", StringComparison.Ordinal)).ToList();
        if (problems.Count > 0) { _progMsg[VideoAiKey] = problems[0].TrimStart('✕', '⚠', ' '); Show(VideoAiKey); return null; }
        var (w, h) = VideoAiKit.Size(d.Text("format"));
        output ??= Path.Combine(_core.DataRoot, "ki-videos", $"{AnimFileName(d.Title)}-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
        if (!KreativSafe.LocalPath(output)) { _progMsg[VideoAiKey] = "Bitte einen Speicherort auf diesem Rechner."; Show(VideoAiKey); return null; }
        if (File.Exists(output) || File.Exists(output + ".json")) { _progMsg[VideoAiKey] = "Diese Videodatei gibt es schon – sie wird nicht überschrieben."; Show(VideoAiKey); return null; }
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-videoki-" + Guid.NewGuid().ToString("N")[..8]);
        _videoBusy = true; _progMsg[VideoAiKey] = "Video entsteht …"; Show(VideoAiKey);
        try
        {
            Directory.CreateDirectory(tmp); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var clips = new List<VideoAiKit.Clip>();
            for (var i = 0; i < scenes.Count; i++)
            {
                string? cap = null;
                if (scenes[i].Caption.Length > 0) { cap = Path.Combine(tmp, $"text-{i + 1}.png"); VideoCaptionPng(scenes[i].Caption, w, h, cap); }
                clips.Add(new(scenes[i].Image, scenes[i].Seconds, scenes[i].Motion, cap));
            }
            var args = VideoAiKit.FfmpegArgs(clips, w, h, output);
            bool ok; var err = "";
            if (VideoAiFfmpegOverride is { } o) ok = o(args, output);
            else
            {
                var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                foreach (var a in args) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
                using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                try { err = await p.StandardError.ReadToEndAsync(limit.Token); await p.WaitForExitAsync(limit.Token); ok = p.ExitCode == 0; }
                catch (OperationCanceledException) { try { p.Kill(true); } catch (InvalidOperationException) { } ok = false; err = "FFmpeg hat zu lange gebraucht und wurde beendet."; }
            }
            if (!ok || !File.Exists(output))
            {
                var last = err.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
                _progMsg[VideoAiKey] = "FFmpeg konnte das Video nicht schreiben." + (last.Length > 0 ? " " + KreativSafe.Cut(last, 200) : "");
                return null;
            }
            var src = scenes.Select((s, i) => new VideoAiKit.SceneSource(s.Prompt, s.Seconds, s.Motion, s.Caption, i < sources.Count ? sources[i] : "eigen", Sha256File(s.Image))).ToList();
            VideoAiKit.WriteProvenance(output, new("Trendo OS – Videos mit KI (FFmpeg)", src, w, h, VideoAiKit.Total(scenes.Select(s => s.Seconds).ToList()), DateTime.UtcNow, "", src.Any(s => s.Source == "ki")));
            d.Edit("Video gebaut", x => { if (x["videos"] is not JsonArray a) x["videos"] = a = []; a.Add(output); });
            _core.Audit.Append(Principal.LocalOwner.Name, "video-ki.video", $"{scenes.Count} Szenen {w}x{h}");
            _progMsg[VideoAiKey] = "✓ Video fertig: " + output;
            NotifyFileResult(_progMsg[VideoAiKey]);
            return output;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception) { _progMsg[VideoAiKey] = ex.Message; return null; }
        finally { _videoBusy = false; try { Directory.Delete(tmp, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } Show(VideoAiKey); }
    }

    /// <summary>Nur eigene fertige Videos (im Video-Ordner von Trendo).</summary>
    List<string> VideoList(ProgramDocument d)
    {
        var own = Path.GetFullPath(Path.Combine(_core.DataRoot, "ki-videos")) + Path.DirectorySeparatorChar;
        return d.List("videos").Select(Js).Where(p => p.Length > 0 && KreativSafe.LocalFile(p) && Path.GetFullPath(p).StartsWith(own, StringComparison.OrdinalIgnoreCase)
            && Path.GetExtension(p).Equals(".mp4", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Echte KI-Videoclips (Kling) gibt es nur über das eigene kie.ai-Konto: öffnet das kie.ai-Feld oben in der Leiste.</summary>
    void VideoKie()
    {
        var b = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(x => x.Name == "auftrag-kie" && x.IsEffectivelyVisible);
        if (b?.Flyout is { } fl) { fl.ShowAt(b); _progMsg[VideoAiKey] = "Im kie.ai-Feld „Video“ wählen (Kling). Das kostet Guthaben auf deinem eigenen kie.ai-Konto."; }
        else _progMsg[VideoAiKey] = "Echte KI-Videoclips (Kling) gibt es über dein eigenes kie.ai-Konto: oben in der Leiste „kie.ai“ → „Video“. Das ist kostenpflichtig und freiwillig.";
    }

    private void InitVideoAi() => RegisterProgram(FachTabs, new ProgramSpec(VideoAiKey, "STUDIO-VIDEOAI-01", "Videos mit KI")
    {
        Sentence = "Kurze Videos aus einer Beschreibung: Szenen planen, Bilder lokal malen oder eigene wählen, mit Kamerafahrt und Überblendung als MP4 – höchstens 60 Sekunden.",
        Folder = "KI-Videos",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Neues Video", "Szenen selbst beschreiben", () => VideoDoc("")),
            new("beispiel", "Beispiel: Morgen am Meer", "Vier Szenen mit Einblendungen", () => VideoDoc(VideoSample)),
        ],
        Ribbon =
        [
            ("Video", [
                new ProgramCmd("video-plan", "Szenen planen", VideoPlan, Primary: true, Tip: "Eine Szene je Zeile (oder je Absatz). Dauer „5 s“, Bewegung „Zoom rein“ und „Text: …“ werden erkannt."),
                new ProgramCmd("video-images", "Bilder malen", d => _ = VideoImages(d), Tip: "Malt fehlende Bilder mit der lokalen Bild-KI – wie im Studio „Bilder mit KI“."),
                new ProgramCmd("video-build", "Video bauen", d => _ = VideoBuild(d), Tip: "Braucht FFmpeg. Kamerafahrt, Überblendung und Einblendungen."),
                new ProgramCmd("video-setup", "Bild-KI einrichten", d => Show("ki-bibliothek"), Tip: "Öffnet die KI-Bibliothek."),
            ]),
            ("Format", VideoAiKit.Formats.Select(f => new ProgramCmd("video-format-" + f.Id, f.Label, d => d.Set("format", f.Id, "Format " + f.Label))).ToList()),
            ("Stil", ImageAiKit.Styles.Where(s => s.Id is "foto" or "illustration" or "aquarell" or "3d").Select(s => new ProgramCmd("video-style-" + s.Id, s.Label, d => d.Set("style", s.Id, "Stil " + s.Label))).ToList()),
            ("KI", [
                new ProgramCmd("video-claude", "Szenen mit Claude verbessern", d => _ = ClaudeOrHint(VideoAiKey, "STUDIO-VIDEOAI-01", "Verbessere diesen Szenenplan für ein kurzes Video (höchstens 60 Sekunden). Schreib je Zeile eine Szene mit Bildbeschreibung, Dauer („5 s“), Bewegung (Zoom rein, Zoom raus, Schwenk nach links/rechts, ruhig) und optional „Text: …“. Keine echten Personen, keine fremden Marken:\n\n" + KreativSafe.Cut(d.Text("idea"), 6000)), Tip: "Nutzt deinen Claude-Zugang. Du entscheidest, was übernommen wird."),
                new ProgramCmd("video-kie", "Echter KI-Clip (kie.ai)", d => VideoKie(), Tip: "Bewegte KI-Clips (Kling) über dein eigenes kie.ai-Konto – kostenpflichtig, freiwillig."),
            ]),
        ],
        GroupLevels = new() { ["Stil"] = 1, ["KI"] = 1 },
        Exports =
        [
            new("mp4", "Neuestes Video (.mp4)", ".mp4", (d, f) => { var v = VideoList(d).LastOrDefault() ?? throw new InvalidOperationException("Noch kein Video gebaut."); File.Copy(v, f, true); if (File.Exists(v + ".json")) File.Copy(v + ".json", f + ".json", true); return f; }),
            new("plan", "Szenenplan (.md)", ".md", (d, f) => { File.WriteAllText(f, VideoAiKit.PlanText(VideoScenes(d), d.Title)); return f; }),
        ],
        Workspace = VideoAiWorkspace,
        Properties = VideoAiProperties,
        Status = d => { var s = VideoScenes(d); return $"{s.Count} Szenen · {VideoAiKit.Total(s.Select(x => x.Seconds).ToList()).ToString("0.#", De)} s · {s.Count(x => x.Image.Length > 0)} Bilder · " + (FfmpegPath is not null || VideoAiFfmpegOverride is not null ? "FFmpeg bereit" : "FFmpeg fehlt"); },
        Help = "1. Beschreibe das Video: je Zeile eine Szene, z. B. „Ein Leuchtturm im Morgenlicht, Zoom rein, 5 s, Text: Guten Morgen“. 2. „Szenen planen“. 3. „Bilder malen“ (lokale Bild-KI) oder je Szene ein eigenes Bild wählen. 4. „Video bauen“ – höchstens 60 Sekunden. Keine echten Personen, keine fremden Marken. Neben jedem Bild und Video liegt ein Herkunftsnachweis. Bewegte KI-Clips gibt es über dein eigenes kie.ai-Konto.",
    }, "video ki", "videos mit ki", "ki video", "video erzeugen", "text zu video", "ken burns", "diashow video", "kurzvideo", "erklärvideo", "kling");

    Control VideoAiWorkspace(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "video-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        var idea = KrBox(d, "idea", "video-idea", "Je Zeile eine Szene, z. B. „Ein Leuchtturm im Morgenlicht, Zoom rein, 5 s, Text: Guten Morgen“", true);
        idea.MinHeight = 110;
        var plan = new Button { Name = "video-go-plan", Content = "Szenen planen", Padding = new Thickness(16, 7), IsEnabled = !_videoBusy }; plan.Classes.Add("accent");
        plan.Click += (_, _) => ProgramRun(VideoAiKey, () => VideoPlan(d));
        sp.Children.Add(KrCard(new StackPanel { Spacing = 8, Children = { H("Was soll im Video zu sehen sein?", 14), idea, plan } }, "video-idea-card"));

        var scenes = VideoScenes(d);
        if (scenes.Count == 0) { sp.Children.Add(P("Noch keine Szenen. Beschreibung eintragen und „Szenen planen“.", Muted, 12)); return sp; }
        var total = VideoAiKit.Total(scenes.Select(s => s.Seconds).ToList());
        var list = new StackPanel { Name = "video-scenes", Spacing = 10 };
        list.Children.Add(H($"{scenes.Count} Szenen · {total.ToString("0.#", De)} von höchstens {VideoAiKit.MaxTotal:0} Sekunden", 14));
        list.Children.Add(new ProgressBar { Name = "video-length", Minimum = 0, Maximum = VideoAiKit.MaxTotal, Value = Math.Min(total, VideoAiKit.MaxTotal), Height = 6 });
        for (var i = 0; i < scenes.Count; i++)
        {
            var idx = i; var s = scenes[i];
            Control thumb;
            if (s.Image.Length > 0)
            {
                var img = new Image { Stretch = Stretch.UniformToFill, Width = 160, Height = 90 };
                try { using var fs = File.OpenRead(s.Image); img.Source = Bitmap.DecodeToWidth(fs, 320); }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { }
                thumb = new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = img };
            }
            else thumb = new Border { Width = 160, Height = 90, CornerRadius = new CornerRadius(6), Background = Brush.Parse("#0A0F1A"), Child = new TextBlock { Text = "noch kein Bild", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var prompt = KrField(VideoAiKey, $"video-s{idx}-prompt", s.Prompt, "Bildbeschreibung", v => VideoEditScene(d, idx, "Beschreibung", o => { if (o["prompt"]?.ToString() != v) { o["prompt"] = v; if (Js(o["source"]) == "ki") { o["image"] = ""; o["source"] = ""; } } }));
            var sec = KrField(VideoAiKey, $"video-s{idx}-sec", s.Seconds.ToString("0.#", CultureInfo.InvariantCulture), "s", v =>
            {
                if (!double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || x < VideoAiKit.MinScene || x > VideoAiKit.MaxScene) throw new FormatException($"Dauer zwischen {VideoAiKit.MinScene:0} und {VideoAiKit.MaxScene:0} Sekunden.");
                VideoEditScene(d, idx, "Dauer", o => o["sec"] = Math.Round(x, 1));
            }, width: 64);
            var motion = new ComboBox { Name = $"video-s{idx}-motion", ItemsSource = VideoAiKit.Motions.Select(m => m.Label).ToList(), SelectedIndex = Math.Max(0, VideoAiKit.Motions.ToList().FindIndex(m => m.Id == s.Motion)), MinWidth = 170 };
            motion.SelectionChanged += (_, _) => { var m = VideoAiKit.Motions[Math.Max(0, motion.SelectedIndex)].Id; if (m != s.Motion) ProgramRun(VideoAiKey, () => VideoEditScene(d, idx, "Bewegung", o => o["motion"] = m)); };
            var caption = KrField(VideoAiKey, $"video-s{idx}-caption", s.Caption, "Einblendung (optional)", v => VideoEditScene(d, idx, "Einblendung", o => o["caption"] = KreativSafe.Cut(v, 200)));
            var own = new Button { Name = $"video-s{idx}-own", Content = "Eigenes Bild …", Padding = new Thickness(8, 3) };
            own.Click += (_, _) => _ = VideoOwnImage(d, idx);
            var del = new Button { Name = $"video-s{idx}-del", Content = "Entfernen", Padding = new Thickness(8, 3), IsEnabled = !_videoBusy };
            del.Click += (_, _) => ProgramRun(VideoAiKey, () => d.Edit("Szene entfernt", x => { if (x["scenes"] is JsonArray a && idx < a.Count) a.RemoveAt(idx); }));
            var row2 = new WrapPanel { Children = { sec, new TextBlock { Text = "s", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) }, motion, new Border { Width = 8 }, own, new Border { Width = 6 }, del } };
            var right = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = $"Szene {idx + 1}" + (s.Image.Length > 0 ? Js((d.List("scenes")[idx] as JsonObject)?["source"]) == "eigen" ? " · eigenes Bild" : " · KI-Bild" : ""), Foreground = Text, FontWeight = FontWeight.SemiBold }, prompt, row2, caption } };
            list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,14,*"), Children = { thumb, Pos(right, 2) } });
        }
        sp.Children.Add(KrCard(list, "video-scenes-card"));
        var checks = VideoAiKit.Check(scenes, needImages: true);
        if (checks.Count > 0)
        {
            var cp = new StackPanel { Name = "video-checks", Spacing = 2 };
            foreach (var c in checks) cp.Children.Add(P(c, StatusBrush(c), 12));
            sp.Children.Add(cp);
        }
        var actions = new WrapPanel();
        var paint = new Button { Name = "video-go-images", Content = _videoBusy ? "Arbeitet …" : "Bilder malen", Padding = new Thickness(14, 6), IsEnabled = !_videoBusy };
        paint.Click += (_, _) => _ = VideoImages(d);
        var build = new Button { Name = "video-go-build", Content = _videoBusy ? "Arbeitet …" : "Video bauen", Padding = new Thickness(14, 6), Margin = new Thickness(8, 0, 0, 0), IsEnabled = !_videoBusy };
        build.Classes.Add("accent");
        build.Click += (_, _) => _ = VideoBuild(d);
        actions.Children.Add(paint); actions.Children.Add(build);
        sp.Children.Add(actions);
        var videos = VideoList(d);
        if (videos.Count > 0)
        {
            var vp = new StackPanel { Name = "video-done", Spacing = 4, Children = { H("Fertige Videos", 14) } };
            foreach (var (v, n) in videos.AsEnumerable().Reverse().Take(10).Select((v, n) => (v, n)))
            {
                var play = new Button { Name = $"video-play-{n}", Content = "Abspielen", Padding = new Thickness(8, 3) }; var path = v;
                play.Click += (_, _) => Launch(path);
                var folder = new Button { Name = $"video-folder-{n}", Content = "Ordner öffnen", Padding = new Thickness(8, 3), Margin = new Thickness(6, 0, 0, 0) };
                folder.Click += (_, _) => RevealInFolder(path);
                vp.Children.Add(new DockPanel { Children = { new StackPanel { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Orientation = Orientation.Horizontal, Children = { play, folder } }, new TextBlock { Text = Path.GetFileName(v), Foreground = Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis } } });
            }
            sp.Children.Add(KrCard(vp));
        }
        return sp;
    }

    Control VideoAiProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        var fmt = VideoAiKit.Formats.FirstOrDefault(f => f.Id == d.Text("format")); if (fmt.Id is null) fmt = VideoAiKit.Formats[0];
        var style = ImageAiKit.Styles.FirstOrDefault(s => s.Id == d.Text("style")) ?? ImageAiKit.Styles[0];
        sp.Children.Add(P($"Format: {fmt.Label} ({fmt.W} × {fmt.H}) · Stil: {style.Label}", Text, 12));
        sp.Children.Add(P("Startwert (gleich = gleiche Bilder)", null, 11)); sp.Children.Add(KrBox(d, "seed", "video-p-seed", "42"));
        sp.Children.Add(H("Regeln", 13));
        sp.Children.Add(P("Keine echten Personen, keine fremden Marken, Logos oder bekannte Figuren. Videos mit KI-Bildern als solche kennzeichnen, wenn du sie veröffentlichst.", Muted, 11));
        sp.Children.Add(P($"Jede Szene {VideoAiKit.MinScene:0}–{VideoAiKit.MaxScene:0} s, zusammen höchstens {VideoAiKit.MaxTotal:0} s. Überblendung {VideoAiKit.Fade.ToString("0.#", De)} s.", Muted, 11));
        return sp;
    }

    // ---------------- KI-Agenten bauen ----------------

    static JsonObject AgentDoc(AgentBlueprint a) => new()
    {
        ["name"] = a.Name, ["description"] = a.Description, ["instructions"] = a.Instructions, ["tools"] = new JsonArray(a.Tools.Select(t => (JsonNode)t).ToArray()),
        ["examples"] = a.Examples, ["limits"] = a.Limits, ["probe"] = "", ["studio"] = "STUDIO-AGENTS-01",
    };

    static AgentBlueprint AgentOf(ProgramDocument d) => new(d.Text("name").Trim(), d.Text("description").Trim(), d.Text("instructions"), d.List("tools").Select(Js).Where(t => t.Length > 0).Distinct().ToList(), d.Text("examples"), d.Text("limits"));

    static readonly AgentBlueprint AgentSample = new("briefe-pruefen", "Prüft Briefe und E-Mails auf Rechtschreibung, Ton und Vollständigkeit. Rufen, wenn ein Text vor dem Absenden durchgesehen werden soll.",
        "Du prüfst Briefe und E-Mails, bevor sie verschickt werden.\n\n1. Lies den ganzen Text.\n2. Markiere Rechtschreib- und Grammatikfehler mit Verbesserung.\n3. Prüfe den Ton: höflich, klar, passend zum Empfänger.\n4. Prüfe, ob Anliegen, Frist und Bitte am Schluss eindeutig sind.\n\nAntworte als Liste, wichtigste Punkte zuerst. Ändere nichts selbst, mach nur Vorschläge.",
        ["lesen", "suchen"], "Eingabe: „Sehr geehrte Frau Muster, anbei die Unterlagen. Mfg“\nGute Antwort: „1. „Mfg“ ausschreiben: „Mit freundlichen Grüßen“. 2. Kurz sagen, welche Unterlagen anbei sind.“",
        "Nichts selbst ändern oder senden. Keine persönlichen Daten aus dem Text weitergeben. Bei Rechtsfragen auf eine Fachberatung verweisen.");

    void AgentCheck(ProgramDocument d)
    {
        var f = AgentOf(d).Check();
        var errors = f.Count(x => x.Error);
        _progMsg[AgentsKey] = errors == 0 ? $"✓ Der Helfer ist vollständig{(f.Count > 0 ? $" ({f.Count} Hinweis{(f.Count == 1 ? "" : "e")})" : "")}." : $"✕ Noch {errors} Punkt{(errors == 1 ? "" : "e")} offen: {f.First(x => x.Error).Message}";
    }

    /// <summary>Speichert den Helfer als &lt;Projekt&gt;/.claude/agents/&lt;name&gt;.md – erst nach Ordnerwahl, vorhandene Dateien bleiben.</summary>
    public async Task<string?> AgentSave(ProgramDocument d)
    {
        var a = AgentOf(d);
        if (a.Check().FirstOrDefault(x => x.Error) is { } e) { _progMsg[AgentsKey] = "✕ " + e.Message; Show(AgentsKey); return null; }
        if (await PickFolderPath("Projektordner für den Helfer") is not { } dir) return null;
        string? file = null;
        ProgramRun(AgentsKey, () =>
        {
            file = a.SaveTo(dir);
            _core.Audit.Append(Principal.LocalOwner.Name, "agent.datei", a.Name);
            _progMsg[AgentsKey] = "✓ Gespeichert: " + file + " – Claude Code findet den Helfer in diesem Projekt.";
        });
        return file;
    }

    /// <summary>Übernimmt den Helfer als eigene Rolle ins KI-Team (vorhandenes Format „eigene Rollen“ von KI-Rollen und Teams).</summary>
    public AgentRole AgentToTeam(ProgramDocument d)
    {
        var a = AgentOf(d);
        if (a.Check().FirstOrDefault(x => x.Error) is { } e) throw new InvalidOperationException(e.Message);
        var (name, does, focus, write) = a.ToTrendoRole();
        var studio = d.Text("studio") is { Length: > 0 } s && StudioCatalog.For(s) is not null ? s : "STUDIO-AGENTS-01";
        var role = Agents.AddCustomRole(name, does, focus, studio, write, id => StudioCatalog.For(id)?.Name);
        _progMsg[AgentsKey] = $"✓ Als Rolle „{role.Name}“ im KI-Team von „{StudioCatalog.For(studio)?.Name}“ übernommen. Du findest sie unter „KI-Rollen und Teams“.";
        return role;
    }

    async Task AgentImport(ProgramDocument d)
    {
        if (await PickMediaFile("agent-datei", "Agent-Datei (.md)", ["*.md"]) is not { } f) return;
        ProgramRun(AgentsKey, () =>
        {
            if (!KreativSafe.LocalFile(f, 1024 * 1024)) throw new ArgumentException("Bitte eine Agent-Datei auf diesem Rechner (höchstens 1 MB).");
            var (a, dropped) = AgentBlueprint.FromClaudeMarkdown(File.ReadAllText(f));
            d.Edit("Agent-Datei geöffnet", x => { foreach (var (k, v) in AgentDoc(a)) if (k is not ("probe" or "studio")) x[k] = v?.DeepClone(); x["fremd"] = "ja"; });
            _progMsg[AgentsKey] = $"Helfer „{a.Name}“ geöffnet." + (dropped.Count > 0 ? $" ⚠ Nicht übernommen (nicht auf der sicheren Liste): {string.Join(", ", dropped)}." : "");
        });
    }

    string? _agentForeignAsk;

    void AgentTry(ProgramDocument d)
    {
        var a = AgentOf(d);
        if (a.Check().FirstOrDefault(x => x.Error) is { } e) throw new InvalidOperationException(e.Message);
        var probe = d.Text("probe").Trim();
        if (probe.Length == 0) throw new InvalidOperationException("Schreib rechts unter „Ausprobieren“, was der Helfer tun soll.");
        // fremde Datei: Claude arbeitet mit deinen Rechten – erst lesen, dann bewusst ein zweites Mal klicken
        if (d.Text("fremd") == "ja")
        {
            if (_agentForeignAsk != d.Id) { _agentForeignAsk = d.Id; _progMsg[AgentsKey] = "⚠ Dieser Helfer stammt aus einer fremden Datei. Lies Anweisungen und Werkzeuge, bevor Claude danach arbeitet. Nochmal „Ausprobieren“ klicken, wenn alles passt."; return; }
            _agentForeignAsk = null; d.Set("fremd", "", "Durchgesehen");
        }
        var tools = string.Join(", ", AgentBlueprint.AllTools.Where(t => a.Tools.Contains(t.Id)).Select(t => t.Label));
        _ = ClaudeOrHint(AgentsKey, "STUDIO-AGENTS-01", $"Arbeite für diesen einen Auftrag als der folgende Helfer. Halte dich genau an seine Anweisungen und Grenzen und nutze nur diese Werkzeuge: {tools}.\n\n{a.ToClaudeMarkdown()}\n\nAuftrag zum Ausprobieren:\n{KreativSafe.Cut(probe, 4000)}");
        _progMsg[AgentsKey] = "Claude probiert den Helfer rechts aus.";
    }

    private void InitAgentsStudio() => RegisterProgram(FachTabs, new ProgramSpec(AgentsKey, "STUDIO-AGENTS-01", "KI-Agenten bauen")
    {
        Sentence = "Eigene KI-Helfer entwerfen: Aufgabe, Anweisungen, erlaubte Werkzeuge, Beispiele und Grenzen – als Claude-Code-Agent oder als Rolle im KI-Team.",
        Folder = "KI-Agenten",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Neuer Helfer", "Alles selbst beschreiben", () => AgentDoc(new("", "", "", ["lesen"]))),
            new("beispiel", "Beispiel: Briefe prüfen", "Liest Briefe und macht Vorschläge", () => AgentDoc(AgentSample)),
        ],
        Ribbon =
        [
            ("Helfer", [
                new ProgramCmd("agent-check", "Prüfen", AgentCheck, Primary: true, Tip: "Kurzname, Pflichtfelder, Werkzeuge und ob ein Schlüssel im Text steht."),
                new ProgramCmd("agent-slug", "Kurzname vorschlagen", d => { var s = AgentBlueprint.Slug(d.Text("name")); if (s.Length < 3) throw new InvalidOperationException("Schreib zuerst einen Namen."); d.Set("name", s, "Kurzname"); }),
                new ProgramCmd("agent-import", "Agent-Datei öffnen …", d => _ = AgentImport(d), Tip: "Liest einen vorhandenen Claude-Code-Agenten (.md) ein."),
            ]),
            ("Übernehmen", [
                new ProgramCmd("agent-save", "In Projektordner speichern …", d => _ = AgentSave(d), Tip: "Legt .claude/agents/<name>.md an. Vorhandene Dateien werden nie überschrieben."),
                new ProgramCmd("agent-team", "Als Rolle ins KI-Team", d => AgentToTeam(d), Tip: "Eigene Rolle unter „KI-Rollen und Teams“."),
                new ProgramCmd("agent-try", "Mit Claude ausprobieren", AgentTry, Tip: "Nutzt deinen Claude-Zugang mit den Anweisungen des Helfers."),
            ]),
        ],
        Exports =
        [
            new("md", "Claude-Code-Agent (.md)", ".md", (d, f) => { var a = AgentOf(d); if (a.Check().FirstOrDefault(x => x.Error) is { } e) throw new InvalidOperationException(e.Message); File.WriteAllText(f, a.ToClaudeMarkdown()); return f; }),
            new("rolle", "Trendo-Rolle (.json)", ".json", (d, f) =>
            {
                var a = AgentOf(d); if (a.Check().FirstOrDefault(x => x.Error) is { } e) throw new InvalidOperationException(e.Message);
                var (name, does, focus, write) = a.ToTrendoRole(); var studio = StudioCatalog.For(d.Text("studio")) is not null ? d.Text("studio") : "STUDIO-AGENTS-01";
                var role = new AgentRole("eigen." + studio.ToLowerInvariant() + "." + a.Name, name, does, StudioTeams.Prompt(new StudioRole(a.Name, name, does, focus, write), StudioCatalog.For(studio)?.Name ?? studio), write, studio);
                File.WriteAllText(f, System.Text.Json.JsonSerializer.Serialize(role, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); return f;
            }),
        ],
        Workspace = AgentsWorkspace,
        Properties = AgentsProperties,
        Status = d => { var f = AgentOf(d).Check(); var e = f.Count(x => x.Error); return (e == 0 ? "Vollständig" : $"{e} offen") + $" · {AgentOf(d).Tools.Count} Werkzeuge · {AgentOf(d).ToClaudeMarkdown().Length:N0} Zeichen"; },
        Help = "Gib dem Helfer einen Kurznamen (klein, mit Bindestrichen), schreib in einem Satz, wofür er da ist, und darunter, wie er arbeiten soll. Wähle nur die Werkzeuge, die er wirklich braucht – „Befehle ausführen“ nur mit Vorsicht. Schreib nie Passwörter oder Schlüssel hinein. „Prüfen“ zeigt, was fehlt. Gespeichert wird erst, wenn du einen Projektordner wählst; vorhandene Dateien bleiben.",
    }, "agent", "agenten", "ki agent", "subagent", "eigener helfer", "assistent bauen", "claude agent", "ki helfer", "agent erstellen", "agenten bauen");

    Control AgentsWorkspace(ProgramDocument d)
    {
        var a = AgentOf(d); var findings = a.Check();
        IBrush? FieldBrush(string field) => findings.Any(f => f.Field == field && f.Error) ? ErrBrush : null;
        var form = new StackPanel { Name = "agent-form", Spacing = 6 };
        void Box(string key, string label, string hint, bool multi = false, double minH = 0)
        {
            form.Children.Add(P(label, FieldBrush(key) ?? Text, 12));
            var b = KrBox(d, key, "agent-" + key, hint, multi); if (minH > 0) b.MinHeight = minH;
            form.Children.Add(b);
        }
        Box("name", "Kurzname", "z. B. briefe-pruefen");
        Box("description", "Aufgabe (ein Satz: wofür, wann rufen)", "z. B. Prüft Briefe vor dem Absenden.");
        Box("instructions", "Anweisungen", "Wie soll der Helfer arbeiten? Schritte, Ton, Form der Antwort.", true, 150);
        form.Children.Add(P("Erlaubte Werkzeuge", FieldBrush("tools") ?? Text, 12));
        var tools = new WrapPanel { Name = "agent-tools" };
        foreach (var t in AgentBlueprint.AllTools)
        {
            var tid = t.Id;
            var cb = new CheckBox { Name = "agent-tool-" + t.Id, Content = t.Label + (t.Id == "befehle" ? " ⚠" : ""), IsChecked = a.Tools.Contains(t.Id), Margin = new Thickness(0, 0, 14, 0) };
            if (t.Warning is not null) ToolTip.SetTip(cb, t.Warning);
            cb.IsCheckedChanged += (_, _) => ProgramRun(AgentsKey, () => d.Edit("Werkzeuge", x =>
            {
                var cur = (x["tools"] as JsonArray)?.Select(Js).ToList() ?? [];
                if (cb.IsChecked == true) { if (!cur.Contains(tid)) cur.Add(tid); } else cur.Remove(tid);
                x["tools"] = new JsonArray(AgentBlueprint.AllTools.Where(z => cur.Contains(z.Id)).Select(z => (JsonNode)z.Id).ToArray());
            }));
            tools.Children.Add(cb);
        }
        form.Children.Add(tools);
        Box("examples", "Beispiele (optional)", "Eingabe und gute Antwort", true);
        Box("limits", "Grenzen", "Was der Helfer nie tun soll, z. B. nichts löschen, nichts senden.", true);

        var right = new StackPanel { Name = "agent-preview", Spacing = 6 };
        right.Children.Add(H("Prüfung", 14));
        if (findings.Count == 0) right.Children.Add(P("✓ Alles vollständig.", OkBrush, 12));
        foreach (var f in findings) right.Children.Add(P((f.Error ? "✕ " : "⚠ ") + f.Message, f.Error ? ErrBrush : WarnBrush, 12));
        right.Children.Add(H("Agent-Datei", 14));
        right.Children.Add(new TextBox { Name = "agent-md", Text = a.ToClaudeMarkdown(), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 12, MinHeight = 220 });
        return new Grid { ColumnDefinitions = new ColumnDefinitions("3*,14,2*"), Margin = new Thickness(4, 0, 10, 4), Children = { KrCard(form, "agent-card"), Pos(KrCard(right, "agent-preview-card"), 2) } };
    }

    Control AgentsProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(H("Ausprobieren", 13));
        var probe = KrBox(d, "probe", "agent-p-probe", "Was soll der Helfer zum Test tun?", true); probe.MinHeight = 70;
        sp.Children.Add(probe);
        var go = new Button { Name = "agent-p-try", Content = "Mit Claude ausprobieren", Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 0, 8) };
        go.Click += (_, _) => ProgramRun(AgentsKey, () => AgentTry(d));
        sp.Children.Add(go);
        sp.Children.Add(H("Rolle im KI-Team", 13));
        sp.Children.Add(P("Studio, in dem die Rolle mitarbeitet", null, 11));
        var studios = StudioCatalog.ById.OrderBy(kv => kv.Value.Name, StringComparer.CurrentCulture).ToList();
        var box = new ComboBox { Name = "agent-p-studio", ItemsSource = studios.Select(kv => kv.Value.Name).ToList(), SelectedIndex = Math.Max(0, studios.FindIndex(kv => kv.Key == d.Text("studio"))), HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0 && studios[box.SelectedIndex].Key != d.Text("studio")) ProgramRun(AgentsKey, () => d.Set("studio", studios[box.SelectedIndex].Key, "Studio der Rolle")); };
        sp.Children.Add(box);
        sp.Children.Add(P("Werkzeuge sind eine feste, sichere Liste. Ohne Werkzeug-Angabe dürfte ein Claude-Code-Agent alles – deshalb ist mindestens eines Pflicht.", Muted, 11));
        return sp;
    }

    // ---------------- KI-Modelle vergleichen ----------------

    /// <summary>Gefundene Quellen: Schlüssel „Quelle|Modell“, Anzeige, ob Internet.</summary>
    List<(string Key, string Label, bool Remote)>? _modelsFound; List<string> _modelsMissing = []; bool _modelsBusy;

    static JsonObject ModelsDoc(string question) => new() { ["question"] = question, ["sources"] = new JsonArray(), ["rounds"] = new JsonArray(), ["seed"] = "42", ["system"] = "" };

    static JsonObject RoundJson(ModelCompare.Round r) => new()
    {
        ["q"] = r.Question, ["revealed"] = r.Revealed,
        ["answers"] = new JsonArray(r.Answers.Select(a => (JsonNode)new JsonObject { ["source"] = a.Source, ["text"] = a.Text, ["sec"] = Math.Round(a.Seconds, 2), ["ok"] = a.Ok }).ToArray()),
        ["order"] = new JsonArray(r.Order.Select(i => (JsonNode)i).ToArray()), ["stars"] = new JsonArray(r.Stars.Select(i => (JsonNode)i).ToArray()),
    };

    /// <summary>Runden aus dem Dokument; eine beschädigte Runde (Reihenfolge passt nicht zu den Antworten) wird übersprungen.</summary>
    static List<ModelCompare.Round> ModelsRounds(ProgramDocument d)
    {
        var list = new List<ModelCompare.Round>();
        foreach (var n in d.List("rounds"))
        {
            // beschädigte Runde bleibt als leere Runde stehen, damit die Stellen zum gespeicherten Dokument passen
            if (n is not JsonObject o) { list.Add(new("", [], [], [], true)); continue; }
            var ans = (o["answers"] as JsonArray ?? []).OfType<JsonObject>().Select(a => new ModelCompare.Answer(Js(a["source"]), Js(a["text"]), Math.Max(0, ProgramDocument.ToNumber(a["sec"], 0)), a["ok"]?.GetValueKind() == System.Text.Json.JsonValueKind.True)).ToList();
            var order = (o["order"] as JsonArray ?? []).Select(n => (int)ProgramDocument.ToNumber(n, -1)).ToList();
            var stars = (o["stars"] as JsonArray ?? []).Select(n => Math.Clamp((int)ProgramDocument.ToNumber(n, 0), 0, 5)).ToList();
            if (order.Count != ans.Count || stars.Count != ans.Count || order.Distinct().Count() != ans.Count || order.Any(i => i < 0 || i >= ans.Count)) { list.Add(new(Js(o["q"]), [], [], [], true)); continue; }
            list.Add(new(Js(o["q"]), ans, order, stars, o["revealed"]?.GetValueKind() == System.Text.Json.JsonValueKind.True));
        }
        return list;
    }

    static void ModelsSaveRound(ProgramDocument d, int index, ModelCompare.Round r, string label) =>
        d.Edit(label, x => { if (x["rounds"] is JsonArray a && index >= 0 && index < a.Count) a[index] = RoundJson(r); });

    IReadOnlyList<IKiBackend> ModelsBackends() => KiBackendsOverride?.Invoke() ?? DefaultKiBackends();

    /// <summary>Fragt jede eingerichtete Quelle nach ihren Modellen. Nicht erreichbare Quellen werden genannt, nichts stürzt ab.</summary>
    public async Task ModelsFind()
    {
        if (_modelsBusy) return;
        var found = new List<(string, string, bool)>(); var missing = new List<string>();
        _modelsBusy = true; _progMsg[ModelsKey] = "KI-Quellen werden gesucht …"; Show(ModelsKey);
        try
        {
            foreach (var b in ModelsBackends())
            {
                try
                {
                    if (!await b.IsUpAsync()) { missing.Add($"{b.Name}: nicht erreichbar"); continue; }
                    var models = await b.ModelsAsync();
                    if (models.Count == 0) { missing.Add($"{b.Name}: kein Modell geladen"); continue; }
                    foreach (var m in models.Take(12)) found.Add(($"{b.Name}|{m}", $"{b.Name} · {m}", b is IRemoteKiBackend));
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { missing.Add($"{b.Name}: {KreativSafe.Cut(ex.Message, 120)}"); }
            }
        }
        finally { _modelsBusy = false; }
        _modelsFound = found; _modelsMissing = missing;
        _progMsg[ModelsKey] = found.Count == 0 ? "Keine KI-Quelle erreichbar. Unter „KI-Quellen“ richtest du eine KI auf diesem PC ein oder verbindest Claude mit deinem Zugang." : $"{found.Count} Modelle gefunden. Wähle mindestens zwei aus.";
        Show(ModelsKey);
    }

    /// <summary>Stellt die Frage nacheinander an jede gewählte Quelle (über den gemeinsamen KI-Kern), misst die Dauer und legt eine neue, gemischte Runde an.</summary>
    public async Task<bool> ModelsAsk(ProgramDocument d)
    {
        if (_modelsBusy) { _progMsg[ModelsKey] = "Es wird schon gefragt – einen Moment."; Show(ModelsKey); return false; }
        var q = d.Text("question").Trim();
        if (q.Length == 0) { _progMsg[ModelsKey] = "Schreib zuerst eine Frage."; Show(ModelsKey); return false; }
        var picked = d.List("sources").Select(Js).Where(s => s.Contains('|')).Distinct().ToList();
        if (picked.Count == 0) { _progMsg[ModelsKey] = "Wähle zuerst die KI-Quellen aus (oben „Quellen suchen“)."; Show(ModelsKey); return false; }
        var backends = ModelsBackends();
        var system = d.Text("system").Trim();
        var answers = new List<ModelCompare.Answer>();
        _modelsBusy = true;
        try
        {
            for (var i = 0; i < picked.Count; i++)
            {
                var (name, model) = (picked[i][..picked[i].IndexOf('|')], picked[i][(picked[i].IndexOf('|') + 1)..]);
                var label = $"{name} · {model}";
                _progMsg[ModelsKey] = $"Frage geht an {label} ({i + 1} von {picked.Count}) …"; Show(ModelsKey);
                var b = backends.FirstOrDefault(x => x.Name == name);
                if (b is null) { answers.Add(new(label, "Diese Quelle ist nicht mehr eingerichtet.", 0, false)); continue; }
                try
                {
                    if (!await b.IsUpAsync() || !(await b.ModelsAsync()).Contains(model)) { answers.Add(new(label, "Quelle oder Modell gerade nicht erreichbar.", 0, false)); continue; }
                    var core = new IntelligenceCore(_core.DataRoot, _core.Audit, () => [b]); // derselbe Kern wie überall, auf diese eine Quelle beschränkt
                    var a = await core.AskAsync(new KiRequest(q, system.Length > 0 ? system : IntelligenceCore.DefaultSystem, model, UseKnowledge: false, AllowRemote: true));
                    answers.Add(new(label, a.Text, Math.Round(a.Took.TotalSeconds, 2), a.Ok));
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { answers.Add(new(label, "Fehler: " + KreativSafe.Cut(ex.Message, 200), 0, false)); }
            }
            var baseSeed = int.TryParse(d.Text("seed"), out var s) ? s : 42;
            var round = ModelCompare.NewRound(q, answers, unchecked(baseSeed + d.List("rounds").Count * 7919));
            d.Edit("Frage gestellt", x => { if (x["rounds"] is not JsonArray a) x["rounds"] = a = []; a.Add(RoundJson(round)); });
            var ok = answers.Count(a => a.Ok);
            _progMsg[ModelsKey] = ok == 0 ? "Keine Quelle hat geantwortet. Unter „KI-Quellen“ siehst du, was fehlt." : $"{ok} von {answers.Count} Antworten da. Bewerte jede mit Sternen – danach wird aufgedeckt.";
            return ok > 0;
        }
        finally { _modelsBusy = false; Show(ModelsKey); }
    }

    public void ModelsRate(ProgramDocument d, int round, int position, int stars)
    {
        var rs = ModelsRounds(d);
        if (round < 0 || round >= rs.Count) throw new InvalidOperationException("Diese Frage gibt es nicht.");
        ModelsSaveRound(d, round, ModelCompare.Rate(rs[round], position, stars), "Sterne");
    }

    public void ModelsReveal(ProgramDocument d, int round)
    {
        var rs = ModelsRounds(d);
        if (round < 0 || round >= rs.Count) throw new InvalidOperationException("Diese Frage gibt es nicht.");
        ModelsSaveRound(d, round, ModelCompare.Reveal(rs[round]), "Aufgedeckt");
        _progMsg[ModelsKey] = "Aufgedeckt. Unten steht die Ergebnistabelle über alle Fragen.";
    }

    private void InitModels() => RegisterProgram(FachTabs, new ProgramSpec(ModelsKey, "STUDIO-MODELS-01", "KI-Modelle vergleichen")
    {
        Sentence = "Dieselbe Frage an mehrere KIs: Antworten nebeneinander mit Dauer und Tempo, blind bewerten, erst dann aufdecken – mit Ergebnistabelle über alle Fragen.",
        Folder = "KI-Vergleiche",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Neuer Vergleich", "Eigene Frage stellen", () => ModelsDoc("")),
            new("beispiel", "Beispiel: Alltagsfrage", "Eine Frage zum Einstieg", () => ModelsDoc("Erkläre in drei Sätzen, wie man einen Brief an die Hausverwaltung wegen einer kaputten Heizung schreibt.")),
        ],
        Ribbon =
        [
            ("Vergleich", [
                new ProgramCmd("models-ask", "Frage stellen", d => _ = ModelsAsk(d), Primary: true, Tip: "Geht nacheinander an jede gewählte Quelle. KIs auf diesem PC antworten ohne Internet."),
                new ProgramCmd("models-find", "Quellen suchen", d => _ = ModelsFind(), Tip: "Zeigt die eingerichteten KI-Quellen und ihre Modelle."),
                new ProgramCmd("models-next", "Nächste Frage", d => d.Set("question", "", "Neue Frage"), Tip: "Feld leeren – die bisherigen Runden bleiben."),
            ]),
            ("KI-Quellen", [
                new ProgramCmd("models-setup", "KI-Quellen einrichten", d => Show("ki-quellen"), Tip: "KI auf diesem PC einrichten oder Claude verbinden."),
            ]),
        ],
        Exports =
        [
            new("md", "Bericht (.md)", ".md", (d, f) => { File.WriteAllText(f, ModelCompare.Markdown(ModelsRounds(d), d.Title)); return f; }),
            new("csv", "Rohdaten (.csv)", ".csv", (d, f) => { File.WriteAllText(f, ModelCompare.Csv(ModelsRounds(d)), new System.Text.UTF8Encoding(true)); return f; }),
        ],
        Workspace = ModelsWorkspace,
        Properties = ModelsProperties,
        Status = d => { var r = ModelsRounds(d); return $"{r.Count} Fragen · {r.Count(x => x.Revealed)} aufgedeckt · {d.List("sources").Count} Quellen gewählt"; },
        Help = "1. „Quellen suchen“ und mindestens zwei Modelle anhaken. 2. Frage eintragen und „Frage stellen“. 3. Die Antworten stehen gemischt als A, B, C nebeneinander – bewerte jede mit 1 bis 5 Sternen. 4. „Aufdecken“ zeigt, welche KI was geschrieben hat. Unten wächst die Ergebnistabelle über alle Fragen. Claude nutzt deinen eigenen Zugang; KIs auf diesem PC bleiben offline.",
    }, "modelle vergleichen", "ki vergleich", "llm vergleich", "blindtest", "ki test", "welche ki ist besser", "modell vergleich", "benchmark", "chatbot vergleich");

    Control ModelsWorkspace(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "models-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        // Quellen
        var src = new StackPanel { Name = "models-sources", Spacing = 6 };
        var find = new Button { Name = "models-go-find", Content = _modelsBusy ? "Sucht …" : "Quellen suchen", Padding = new Thickness(12, 5), IsEnabled = !_modelsBusy };
        find.Click += (_, _) => _ = ModelsFind();
        src.Children.Add(new DockPanel { Children = { new Border { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Child = find }, H("KI-Quellen", 14) } });
        var picked = d.List("sources").Select(Js).ToHashSet();
        var shown = (_modelsFound ?? []).ToList();
        foreach (var k in picked.Where(k => shown.All(s => s.Key != k) && k.Contains('|'))) shown.Add((k, k.Replace("|", " · "), false)); // gewählt, aber noch nicht neu gesucht
        if (shown.Count == 0) src.Children.Add(P(_modelsFound is null ? "Noch nicht gesucht. „Quellen suchen“ zeigt die eingerichteten KIs." : "Keine KI-Quelle erreichbar.", Muted, 12));
        var boxes = new WrapPanel();
        for (var i = 0; i < shown.Count; i++)
        {
            var (key, label, remote) = shown[i];
            var cb = new CheckBox { Name = "models-src-" + i, Content = label + (remote ? " (Internet)" : ""), IsChecked = picked.Contains(key), Margin = new Thickness(0, 0, 16, 0) };
            cb.IsCheckedChanged += (_, _) => ProgramRun(ModelsKey, () => d.Edit("Quellen", x =>
            {
                var cur = (x["sources"] as JsonArray)?.Select(Js).ToList() ?? [];
                if (cb.IsChecked == true) { if (!cur.Contains(key)) cur.Add(key); } else cur.Remove(key);
                x["sources"] = new JsonArray(cur.Select(c => (JsonNode)c).ToArray());
            }));
            boxes.Children.Add(cb);
        }
        src.Children.Add(boxes);
        foreach (var m in _modelsMissing) src.Children.Add(P("⚠ " + m, WarnBrush, 11));
        sp.Children.Add(KrCard(src, "models-sources-card"));
        // Frage
        var q = KrBox(d, "question", "models-question", "Deine Frage an alle gewählten KIs", true); q.MinHeight = 70;
        var ask = new Button { Name = "models-go-ask", Content = _modelsBusy ? "Fragt …" : "Frage stellen", Padding = new Thickness(16, 7), IsEnabled = !_modelsBusy }; ask.Classes.Add("accent");
        ask.Click += (_, _) => _ = ModelsAsk(d);
        sp.Children.Add(KrCard(new StackPanel { Spacing = 8, Children = { q, ask } }, "models-question-card"));
        // Runden, neueste zuerst
        var rounds = ModelsRounds(d);
        for (var r = rounds.Count - 1; r >= 0; r--)
        {
            var round = rounds[r]; var ri = r;
            var card = new StackPanel { Name = "models-round-" + r, Spacing = 8 };
            card.Children.Add(H($"Frage {r + 1}: {KreativSafe.Cut(round.Question, 140)}", 14));
            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", round.Order.Select(_ => "*"))) };
            for (var p = 0; p < round.Order.Count; p++)
            {
                var pos = p; var a = round.Answers[round.Order[p]]; var stars = round.Stars[round.Order[p]];
                var col = new StackPanel { Spacing = 4, Margin = new Thickness(p == 0 ? 0 : 8, 0, 0, 0) };
                col.Children.Add(new TextBlock { Name = $"models-head-{r}-{p}", Text = $"Antwort {ModelCompare.Letter(p)}" + (round.Revealed ? " – " + a.Source : ""), Foreground = Accent, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
                col.Children.Add(P(a.Ok ? $"{a.Seconds.ToString("0.0", De)} s · {ModelCompare.Words(a.Text)} Wörter · {ModelCompare.WordsPerSecond(a).ToString("0.0", De)} Wörter/s" : "⚠ keine Antwort", a.Ok ? Muted : WarnBrush, 11));
                col.Children.Add(new TextBlock { Text = KreativSafe.Cut(a.Text.Trim(), 4000), Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
                if (a.Ok)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
                    for (var n = 1; n <= 5; n++)
                    {
                        var val = n;
                        var star = new Button { Name = $"models-star-{r}-{p}-{n}", Content = n <= stars ? "★" : "☆", Padding = new Thickness(6, 2), IsEnabled = !round.Revealed, FontSize = 15, Foreground = n <= stars ? Accent : Muted, Background = Brushes.Transparent };
                        ToolTip.SetTip(star, $"{n} von 5 Sternen");
                        star.Click += (_, _) => ProgramRun(ModelsKey, () => ModelsRate(d, ri, pos, val));
                        row.Children.Add(star);
                    }
                    col.Children.Add(row);
                }
                cols.Children.Add(Pos(col, p));
            }
            card.Children.Add(cols);
            if (!round.Revealed)
            {
                var rev = new Button { Name = "models-reveal-" + r, Content = "Aufdecken", Padding = new Thickness(12, 5), IsEnabled = ModelCompare.CanReveal(round) };
                if (!rev.IsEnabled) ToolTip.SetTip(rev, "Bewerte zuerst jede Antwort.");
                rev.Click += (_, _) => ProgramRun(ModelsKey, () => ModelsReveal(d, ri));
                card.Children.Add(rev);
            }
            sp.Children.Add(KrCard(card));
        }
        // Ergebnistabelle
        var table = ModelCompare.Table(rounds);
        if (table.Any(t => t.Rated > 0))
        {
            var g = new Grid { Name = "models-table", ColumnDefinitions = new ColumnDefinitions("2*,*,*,*,*,*") };
            string[] head = ["Quelle", "Sterne", "Siege", "Bewertet", "Dauer", "Wörter/s"];
            var rowsN = table.Count + 1;
            for (var i = 0; i < rowsN; i++) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < head.Length; c++) { var tb = new TextBlock { Text = head[c], Foreground = Muted, FontSize = 12 }; Grid.SetColumn(tb, c); g.Children.Add(tb); }
            for (var i = 0; i < table.Count; i++)
            {
                var t = table[i];
                string[] cells = [t.Source, t.Rated > 0 ? t.AvgStars.ToString("0.0#", De) : "–", t.Wins.ToString(De), t.Rated.ToString(De), t.AvgSeconds.ToString("0.0", De) + " s", t.AvgWordsPerSecond.ToString("0.0", De)];
                for (var c = 0; c < cells.Length; c++) { var tb = new TextBlock { Text = cells[c], Foreground = i == 0 && t.Rated > 0 ? Accent : Text, FontSize = 13, Margin = new Thickness(0, 3, 6, 3), TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(tb, c); Grid.SetRow(tb, i + 1); g.Children.Add(tb); }
            }
            sp.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = { H("Ergebnis über alle aufgedeckten Fragen", 14), g, P("Sterne = Schnitt deiner Bewertungen; Sieg = beste Bewertung einer Frage (Gleichstand zählt für alle).", Muted, 11) } }, "models-result-card"));
        }
        return sp;
    }

    Control ModelsProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Anweisung an alle KIs (optional)", null, 11)); sp.Children.Add(KrBox(d, "system", "models-p-system", "z. B. Antworte kurz und auf Deutsch.", true));
        sp.Children.Add(P("Startwert fürs Mischen", null, 11)); sp.Children.Add(KrBox(d, "seed", "models-p-seed", "42"));
        sp.Children.Add(H("Fair vergleichen", 13));
        sp.Children.Add(P("Alle bekommen dieselbe Frage und dieselbe Anweisung. Die Reihenfolge ist gemischt, damit du nicht weißt, wer was geschrieben hat. Die Dauer hängt auch von deinem Rechner ab.", Muted, 11));
        return sp;
    }

    // ---------------- Eigene KI trainieren ----------------

    static readonly (string Text, string Label)[] TrainSample =
    [
        ("Ihre Rechnung Nummer 2041 ist bis zum 30. fällig", "Rechnung"), ("Bitte überweisen Sie den offenen Betrag", "Rechnung"), ("Zahlungserinnerung für Ihre Bestellung", "Rechnung"),
        ("Anbei die Rechnung für die Reparatur", "Rechnung"), ("Der Betrag wird von Ihrem Konto abgebucht", "Rechnung"), ("Mahnung: Rechnung noch nicht bezahlt", "Rechnung"),
        ("Gutschrift über 12 Euro auf Ihr Konto", "Rechnung"), ("Ihre monatliche Abrechnung liegt vor", "Rechnung"), ("Bitte zahlen Sie die Gebühr bis Freitag", "Rechnung"),
        ("Rechnungsbetrag inklusive Mehrwertsteuer", "Rechnung"),
        ("Können wir den Termin auf Dienstag verschieben", "Termin"), ("Einladung zur Besprechung am Montag um 10 Uhr", "Termin"), ("Ihr Arzttermin ist am 14. um 9:30 Uhr", "Termin"),
        ("Treffen wir uns morgen im Café", "Termin"), ("Erinnerung: Elternabend am Donnerstag", "Termin"), ("Der Kurs beginnt nächste Woche um 18 Uhr", "Termin"),
        ("Bitte bestätigen Sie den Termin für die Wartung", "Termin"), ("Wann passt es dir für ein kurzes Gespräch", "Termin"), ("Die Sitzung wurde auf 15 Uhr verlegt", "Termin"),
        ("Neuer Termin für die Besichtigung der Wohnung", "Termin"),
        ("Nur heute 30 Prozent Rabatt auf alles", "Werbung"), ("Jetzt den Newsletter abonnieren und sparen", "Werbung"), ("Gratis Versand ab 20 Euro Bestellwert", "Werbung"),
        ("Entdecken Sie unsere neue Kollektion", "Werbung"), ("Exklusives Angebot nur für Sie", "Werbung"), ("Sichern Sie sich jetzt Ihren Gutschein", "Werbung"),
        ("Großer Sommerschlussverkauf hat begonnen", "Werbung"), ("Gewinnen Sie eine Reise beim Gewinnspiel", "Werbung"), ("Die besten Angebote der Woche im Überblick", "Werbung"),
        ("Testen Sie unser Abo einen Monat kostenlos", "Werbung"),
    ];

    static JsonObject TrainDoc(IEnumerable<(string Text, string Label)> ex) => new()
    {
        ["examples"] = new JsonArray(ex.Select(e => (JsonNode)new JsonObject { ["text"] = e.Text, ["label"] = e.Label }).ToArray()),
        ["share"] = "20", ["seed"] = "42", ["model"] = "", ["trained"] = "", ["probe"] = "",
    };

    static List<TextClassifier.Example> TrainExamples(ProgramDocument d) =>
        d.List("examples").Select(n => n as JsonObject ?? new JsonObject()).Select(o => new TextClassifier.Example(KreativSafe.Cut(Js(o["text"]), 5000), KreativSafe.Cut(Js(o["label"]).Trim(), 80))).ToList();

    static string TrainHash(ProgramDocument d) => Sha256Text(string.Join("\n", TrainExamples(d).Select(e => e.Text + "\t" + e.Label)) + "|" + d.Text("share") + "|" + d.Text("seed"));

    static double TrainShare(ProgramDocument d) => double.TryParse(d.Text("share").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? Math.Clamp(s, 0, 50) / 100 : 0.2;
    static int TrainSeed(ProgramDocument d) => int.TryParse(d.Text("seed"), out var s) ? s : 42;

    static TextClassifier? TrainModel(ProgramDocument d)
    {
        var json = d.Text("model"); if (json.Length == 0) return null;
        try { return TextClassifier.FromJson(json); }
        catch (Exception e) when (e is InvalidDataException or OverflowException or ArgumentException or InvalidOperationException or KeyNotFoundException) { return null; }
    }

    /// <summary>Auswertung des gespeicherten Modells mit derselben festen Prüfmenge – nur, wenn die Beispiele seit dem Training gleich geblieben sind.</summary>
    static (TextClassifier.Evaluation Eval, int Train)? TrainEval(ProgramDocument d)
    {
        if (TrainModel(d) is not { } m || d.Text("trained") != TrainHash(d)) return null;
        var (tr, te) = TextClassifier.Split(TrainExamples(d), TrainShare(d), TrainSeed(d));
        return te.Count == 0 ? null : (m.Evaluate(te), tr.Count);
    }

    public TextClassifier.Evaluation? TrainRun(ProgramDocument d)
    {
        var ex = TrainExamples(d);
        var warnings = TextClassifier.Warnings(ex);
        if (warnings.FirstOrDefault(w => w.StartsWith('✕')) is { } stop) throw new InvalidOperationException(stop.TrimStart('✕', ' '));
        var (tr, te) = TextClassifier.Split(ex, TrainShare(d), TrainSeed(d));
        var m = TextClassifier.Train(tr);
        var hash = TrainHash(d); // Fingerabdruck der Beispiele: ändern sie sich, gilt die Prüfung nicht mehr
        d.Edit("Trainiert", x => { x["model"] = m.ToJson(); x["trained"] = hash; });
        _core.Audit.Append(Principal.LocalOwner.Name, "ki-training", $"{tr.Count} Beispiele, {m.Labels.Count} Kategorien");
        if (te.Count == 0) { _progMsg[TrainKey] = $"✓ Trainiert mit {tr.Count} Beispielen. ⚠ Ohne Prüfanteil gibt es keine Genauigkeit."; return null; }
        var e = m.Evaluate(te);
        _progMsg[TrainKey] = $"✓ Trainiert mit {tr.Count} Beispielen, geprüft mit {te.Count}: Genauigkeit {(e.Accuracy * 100).ToString("0.#", De)} %." + (warnings.Count > 0 ? " " + warnings[0] : "");
        return e;
    }

    async Task TrainImport(ProgramDocument d)
    {
        if (await PickMediaFile("train-csv", "Beispiele (CSV)", ["*.csv", "*.txt"]) is not { } f) return;
        ProgramRun(TrainKey, () =>
        {
            if (!KreativSafe.LocalFile(f, 20L * 1024 * 1024)) throw new ArgumentException("Bitte eine CSV-Datei auf diesem Rechner (höchstens 20 MB).");
            var ex = TextClassifier.FromCsv(File.ReadAllText(f));
            if (ex.Count == 0) throw new InvalidDataException("In der Datei stehen keine Beispiele. Erwartet: erste Spalte Text, zweite Spalte Kategorie.");
            d.Edit("CSV importiert", x => { if (x["examples"] is not JsonArray a) x["examples"] = a = []; foreach (var e in ex) a.Add(new JsonObject { ["text"] = e.Text, ["label"] = e.Label }); });
            _progMsg[TrainKey] = $"{ex.Count} Beispiele importiert. Jetzt „Trainieren“.";
        });
    }

    async Task TrainLoad(ProgramDocument d)
    {
        if (await PickMediaFile("train-modell", "Trendo-Modell (.json)", ["*.json"]) is not { } f) return;
        ProgramRun(TrainKey, () =>
        {
            if (!KreativSafe.LocalFile(f, 50L * 1024 * 1024)) throw new ArgumentException("Bitte eine Modelldatei auf diesem Rechner (höchstens 50 MB).");
            var json = File.ReadAllText(f);
            TextClassifier m;
            try { m = TextClassifier.FromJson(json); } catch (Exception e) when (e is OverflowException or ArgumentException) { throw new InvalidDataException("Das Modell ist beschädigt."); }
            d.Edit("Modell geladen", x => { x["model"] = json; x["trained"] = ""; });
            _progMsg[TrainKey] = $"Modell geladen: {m.Labels.Count} Kategorien ({string.Join(", ", m.Labels.Take(8))}). Ausprobieren geht sofort.";
        });
    }

    private void InitTrain() => RegisterProgram(FachTabs, new ProgramSpec(TrainKey, "STUDIO-TRAIN-01", "Eigene KI trainieren")
    {
        Sentence = "Eine kleine KI mit eigenen Beispielen trainieren, die Texte in Kategorien sortiert – mit ehrlicher Prüfung und auf deinem Rechner.",
        Folder = "KI-Training",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Neues Modell", "Beispiele selbst eintragen", () => TrainDoc([("", ""), ("", "")])),
            new("beispiel", "Beispiel: E-Mails sortieren", "30 Beispiele: Rechnung, Termin, Werbung", () => TrainDoc(TrainSample)),
        ],
        Ribbon =
        [
            ("Training", [
                new ProgramCmd("train-run", "Trainieren", d => TrainRun(d), Primary: true, Tip: "Feste Aufteilung: ein Teil zum Lernen, der Rest zum Prüfen."),
                new ProgramCmd("train-add", "Beispiel hinzufügen", d => d.Edit("Beispiel hinzugefügt", x => { if (x["examples"] is not JsonArray a) x["examples"] = a = []; a.Add(new JsonObject { ["text"] = "", ["label"] = "" }); })),
                new ProgramCmd("train-import", "CSV importieren …", d => _ = TrainImport(d), Tip: "Erste Spalte Text, zweite Kategorie."),
            ]),
            ("Modell", [
                new ProgramCmd("train-load", "Modell laden …", d => _ = TrainLoad(d), Tip: "Ein gespeichertes Trendo-Modell (.json) öffnen."),
            ]),
        ],
        GroupLevels = new() { ["Modell"] = 1 },
        Exports =
        [
            new("json", "Modell (.json)", ".json", (d, f) => { File.WriteAllText(f, TrainModel(d)?.ToJson() ?? throw new InvalidOperationException("Noch nicht trainiert.")); return f; }),
            new("md", "Auswertung (.md)", ".md", (d, f) => { var e = TrainEval(d) ?? throw new InvalidOperationException("Erst trainieren (mit Prüfanteil)."); File.WriteAllText(f, TextClassifier.Report(e.Eval, e.Train, TextClassifier.Warnings(TrainExamples(d)), d.Title)); return f; }),
            new("csv", "Beispiele (.csv)", ".csv", (d, f) => { File.WriteAllText(f, "text;kategorie\n" + string.Join("\n", TrainExamples(d).Where(e => e.Text.Length > 0 || e.Label.Length > 0).Select(e => $"\"{e.Text.Replace("\"", "\"\"")}\";\"{e.Label.Replace("\"", "\"\"")}\"")) + "\n", new System.Text.UTF8Encoding(true)); return f; }),
        ],
        Workspace = TrainWorkspace,
        Properties = TrainProperties,
        Status = d => { var ex = TrainExamples(d); var e = TrainEval(d); return $"{ex.Count(x => x.Label.Length > 0)} Beispiele · {ex.Select(x => x.Label).Where(l => l.Length > 0).Distinct().Count()} Kategorien · " + (e is { } v ? $"Genauigkeit {(v.Eval.Accuracy * 100).ToString("0.#", De)} %" : TrainModel(d) is not null ? "Modell da" : "noch nicht trainiert"); },
        Help = "Trag Beispiele ein: links der Text, rechts die Kategorie (z. B. Rechnung, Termin, Werbung) – oder importiere eine CSV. „Trainieren“ lernt mit einem Teil der Beispiele und prüft mit dem Rest: Genauigkeit, Verwechslungen und je Kategorie Präzision und Trefferquote. Rechts kannst du einen neuen Text eingeben und siehst, wie sicher die KI ist. Gut wird es ab etwa 20 Beispielen je Kategorie. Alles bleibt auf deinem Rechner.",
    }, "ki trainieren", "eigene ki", "textklassifikator", "klassifizieren", "maschinelles lernen", "machine learning", "naive bayes", "kategorien lernen", "texte sortieren", "modell trainieren");

    Control TrainWorkspace(ProgramDocument d)
    {
        var ex = TrainExamples(d);
        var left = new StackPanel { Name = "train-examples", Spacing = 4 };
        left.Children.Add(H($"Beispiele ({ex.Count(e => e.Label.Length > 0)})", 14));
        left.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,150,Auto"), Children = { P("Text", Muted, 11), Pos(P("Kategorie", Muted, 11), 1) } });
        const int MaxRows = 300;
        for (var i = 0; i < Math.Min(ex.Count, MaxRows); i++)
        {
            var idx = i;
            void SetField(string field, string v) => d.Edit("Beispiel geändert", x => { if (x["examples"] is JsonArray a && idx < a.Count && a[idx] is JsonObject o) o[field] = v; });
            var t = KrField(TrainKey, $"train-text-{i}", ex[i].Text, "Text", v => SetField("text", v));
            var l = KrField(TrainKey, $"train-label-{i}", ex[i].Label, "Kategorie", v => SetField("label", v.Trim()));
            l.Margin = new Thickness(6, 0);
            var del = new Button { Name = $"train-del-{i}", Content = "✕", Padding = new Thickness(6, 2) };
            ToolTip.SetTip(del, "Beispiel entfernen");
            del.Click += (_, _) => ProgramRun(TrainKey, () => d.Edit("Beispiel entfernt", x => { if (x["examples"] is JsonArray a && idx < a.Count) a.RemoveAt(idx); }));
            left.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,150,Auto"), Children = { t, Pos(l, 1), Pos(del, 2) } });
        }
        if (ex.Count > MaxRows) left.Children.Add(P($"… und {ex.Count - MaxRows} weitere (alle werden trainiert, Export als CSV zeigt alle).", Muted, 11));
        var add = new Button { Name = "train-go-add", Content = "+ Beispiel", Padding = new Thickness(10, 4) };
        add.Click += (_, _) => ProgramRun(TrainKey, () => d.Edit("Beispiel hinzugefügt", x => { if (x["examples"] is not JsonArray a) x["examples"] = a = []; a.Add(new JsonObject { ["text"] = "", ["label"] = "" }); }));
        var run = new Button { Name = "train-go-run", Content = "Trainieren", Padding = new Thickness(14, 6), Margin = new Thickness(8, 0, 0, 0) }; run.Classes.Add("accent");
        run.Click += (_, _) => ProgramRun(TrainKey, () => TrainRun(d));
        left.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { add, run } });
        foreach (var w in TextClassifier.Warnings(ex)) left.Children.Add(P(w, StatusBrush(w), 12));

        var right = new StackPanel { Name = "train-result", Spacing = 8 };
        var model = TrainModel(d); var ev = TrainEval(d);
        if (model is null) right.Children.Add(P("Noch nicht trainiert. Beispiele eintragen und „Trainieren“.", Muted, 12));
        else
        {
            if (ev is { } e)
            {
                right.Children.Add(H($"Genauigkeit {(e.Eval.Accuracy * 100).ToString("0.#", De)} %", 18));
                right.Children.Add(P($"Gelernt aus {e.Train} Beispielen, geprüft mit {e.Eval.Count}, die die KI vorher nicht gesehen hat.", Muted, 11));
                var labels = e.Eval.Labels;
                var g = new Grid { Name = "train-confusion", ColumnDefinitions = new ColumnDefinitions("Auto," + string.Join(",", labels.Select(_ => "*"))) };
                for (var r = 0; r <= labels.Count; r++) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                for (var c = 0; c < labels.Count; c++) { var tb = new TextBlock { Text = labels[c], Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 0) }; Grid.SetColumn(tb, c + 1); g.Children.Add(tb); }
                var max = Math.Max(1, Enumerable.Range(0, labels.Count).SelectMany(r => Enumerable.Range(0, labels.Count).Select(c => e.Eval.Confusion[r, c])).Max());
                for (var r = 0; r < labels.Count; r++)
                {
                    var rl = new TextBlock { Text = labels[r], Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }; Grid.SetRow(rl, r + 1); g.Children.Add(rl);
                    for (var c = 0; c < labels.Count; c++)
                    {
                        var n = e.Eval.Confusion[r, c];
                        var cell = new Border { Margin = new Thickness(2), Padding = new Thickness(4, 3), CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromArgb((byte)(n == 0 ? 20 : 50 + 180 * n / max), r == c ? (byte)74 : (byte)248, r == c ? (byte)222 : (byte)113, r == c ? (byte)128 : (byte)113)),
                            Child = new TextBlock { Text = n.ToString(De), Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center } };
                        Grid.SetRow(cell, r + 1); Grid.SetColumn(cell, c + 1); g.Children.Add(cell);
                    }
                }
                right.Children.Add(P("Verwechslungstabelle: Zeile = richtige Kategorie, Spalte = erkannt als.", Muted, 11));
                right.Children.Add(g);
                foreach (var c in e.Eval.PerClass)
                    right.Children.Add(P($"{c.Label}: Präzision {(c.Precision * 100).ToString("0", De)} % · Trefferquote {(c.Recall * 100).ToString("0", De)} % · {c.Support} Prüfbeispiele", Text, 12));
            }
            else right.Children.Add(P(d.Text("trained").Length == 0 ? "Geladenes Modell – ohne eigene Prüfung." : "⚠ Die Beispiele haben sich seit dem Training geändert. „Trainieren“ macht die Prüfung neu.", WarnBrush, 12));
            right.Children.Add(H("Ausprobieren", 14));
            var result = new StackPanel { Name = "train-probe-result", Spacing = 3 };
            void Predict(string text)
            {
                result.Children.Clear();
                if (text.Trim().Length == 0) { result.Children.Add(P("Schreib einen neuen Text – die KI sagt, wohin er gehört.", Muted, 11)); return; }
                var p = model.Predict(text);
                result.Children.Add(new TextBlock { Name = "train-probe-label", Text = $"{p.Label} – {(p.Confidence * 100).ToString("0", De)} % sicher", Foreground = Accent, FontWeight = FontWeight.SemiBold, FontSize = 15 });
                if (p.KnownWords == 0) result.Children.Add(P("⚠ Kein Wort aus dem Text kam in den Beispielen vor – das ist geraten.", WarnBrush, 11));
                foreach (var (l, pr) in p.All.Take(6))
                    result.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("110,*,50"), Children = {
                        new TextBlock { Text = l, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                        Pos(new ProgressBar { Minimum = 0, Maximum = 1, Value = pr, Height = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) }, 1),
                        Pos(new TextBlock { Text = (pr * 100).ToString("0", De) + " %", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right }, 2) } });
            }
            var probe = KrBox(d, "probe", "train-probe", "Neuer Text, z. B. „Bitte zahlen Sie bis Montag“", true, Predict); probe.MinHeight = 60;
            right.Children.Add(probe); right.Children.Add(result);
            Predict(d.Text("probe"));
        }
        return new Grid { ColumnDefinitions = new ColumnDefinitions("3*,14,2*"), Margin = new Thickness(4, 0, 10, 4), Children = { KrCard(left, "train-card"), Pos(KrCard(right, "train-result-card"), 2) } };
    }

    Control TrainProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Prüfanteil in Prozent (0–50)", null, 11)); sp.Children.Add(KrBox(d, "share", "train-p-share", "20"));
        sp.Children.Add(P("Startwert der Aufteilung (gleich = gleiche Prüfmenge)", null, 11)); sp.Children.Add(KrBox(d, "seed", "train-p-seed", "42"));
        sp.Children.Add(H("So lernt die KI", 13));
        sp.Children.Add(P("Sie zählt, welche Wörter in welcher Kategorie vorkommen (Naive Bayes). Die Prüfbeispiele sieht sie beim Lernen nicht – so ist die Genauigkeit ehrlich. Präzision: wie oft „Rechnung“ stimmt, wenn sie „Rechnung“ sagt. Trefferquote: wie viele echte Rechnungen sie findet.", Muted, 11));
        return sp;
    }
}
