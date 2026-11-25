using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Film;
using Trendo.Core.Kreativ;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0693: „Filme machen“ erzeugt einen Film. Jede Einstellung bekommt Material (eigener Clip, eigenes Foto, Storyboard-Bild oder eine Texttafel),
/// daraus entsteht der Rohschnitt als Zeitleiste des Videoschnitts (<see cref="FilmCut"/>) und über dessen FFmpeg-Weg ein MP4 – mit Fortschritt und Abbrechen.
/// „Im Videoschnitt weiterbearbeiten“ öffnet dieselbe Zeitleiste dort. Die KI schreibt Drehbuch und Shotliste ins offene Dokument; gerendert wird nur auf Klick.</summary>
public partial class MainWindow
{
    /// <summary>Gewählte Einstellung (ID wie „SZ01-E02“).</summary>
    public string? FilmSelected { get; set; }
    /// <summary>Tests: echter FFmpeg-Pfad für Tafeln, Länge und Rendern – auch im abgeschotteten Testmodus (sonst startet dort kein FFmpeg).</summary>
    public string? FilmFfmpegPath { get; set; }
    /// <summary>Tests: Ersatz für die KI beim Drehbuchschreiben (Auftrag → Antwort).</summary>
    public Func<string, Task<string?>>? FilmKiOverride { get; set; }
    /// <summary>Zuletzt gerenderter Film.</summary>
    public string? FilmLastOutput { get; private set; }
    public bool FilmRendering => _filmRender is not null;
    public double FilmRenderProgress { get; private set; }
    CancellationTokenSource? _filmRender; ProgressBar? _filmBar; bool _filmBusy, _filmFocusList;
    readonly HashSet<string> _filmThumbBusy = [];

    string? FilmFf => FilmFfmpegPath ?? (_opt.Isolated ? null : FfmpegPath);

    Func<IReadOnlyList<string>, Cli.Result>? FilmRunner(string cwd, out string? why)
    {
        why = null;
        if (FilmFfmpegPath is { } ff) return a => Cli.Run(ff, a, cwd, 30 * 60_000);
        return FfmpegRunner(cwd, out why);
    }

    string FilmDir(ProgramDocument d) { var p = Path.Combine(WorkFolder(Programs[FilmKey].Folder), DevStudio.SafeName(d.Title.Length > 0 ? d.Title : "Film") + "-Material"); Directory.CreateDirectory(p); return p; }
    string FilmTitle(ProgramDocument d) => FilmParsed(d).Title is { Length: > 0 } t ? t : d.Title;

    FilmShot? FilmShotSelected(ProgramDocument d)
    {
        var shots = FilmCut.Shots(d.Data);
        return shots.FirstOrDefault(s => s.Id == FilmSelected) ?? shots.FirstOrDefault();
    }

    void FilmSay(string msg) { _progMsg[FilmKey] = msg; _progErr.Remove(FilmKey); Show(FilmKey); }

    // ---------- Material ----------
    static readonly string[] FilmVideoExt = ["mp4", "mov", "mkv", "avi", "webm"], FilmImageExt = ["png", "jpg", "jpeg", "webp", "bmp"], FilmAudioExt = ["mp3", "wav", "flac", "m4a", "ogg"];

    async Task<string[]> FilmPick(string field, string title, bool multiple, string[] ext)
    {
        if (PickOverride is { } fake) return await fake(new PickRequest(field, false, multiple, ext)) ?? [];
        var got = await PickOpenFiles(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = title, AllowMultiple = multiple,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType(title) { Patterns = [.. ext.Select(e => "*." + e)] }] });
        return [.. got.Select(f => f.TryGetLocalPath()).OfType<string>()];
    }

    (double Length, bool HasAudio) FilmProbe(string file)
    {
        if (MediaStudio.IsImage(file)) return (0, false);
        var ff = FilmFf ?? throw new InvalidOperationException(_opt.Isolated ? "Im abgeschotteten Testmodus startet Trendo kein FFmpeg." : "Für Videos und Ton fehlt FFmpeg – unter Zusatzprogramme einrichten. Fotos gehen auch ohne.");
        var len = MediaStudio.Duration(ff, file) ?? throw new InvalidOperationException($"Die Länge von „{Path.GetFileName(file)}“ lässt sich nicht lesen. Ist das eine Video- oder Tondatei?");
        return (len, VideoEdit.HasAudioStream(ff, file));
    }

    /// <summary>Clips und Fotos ins Projekt holen (Dateiauswahl oder Ziehen-und-Ablegen). Ohne Einstellungen entsteht je Datei eine; sonst füllen die Dateien die Einstellungen
    /// ab der gewählten (<paramref name="shotId"/>) der Reihe nach – die erste wird ersetzt, danach nur noch Einstellungen ohne Material. Tondateien: die erste wird die Musik.
    /// Ein Schritt, rückgängig machbar. Liefert, wie viele Dateien übernommen wurden.</summary>
    public async Task<int> FilmImport(ProgramDocument d, IReadOnlyList<string> files, string? shotId = null)
    {
        try
        {
            var known = files.Where(File.Exists).Where(f => MediaStudio.Inputs.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
            if (known.Count == 0) { FilmSay(files.Count == 0 ? "Keine Datei gewählt." : "Das sind keine Videos, Fotos oder Tondateien. Es gehen MP4, MOV, MKV, AVI, WEBM, PNG, JPG, WEBP, MP3, WAV, FLAC, M4A, OGG."); return 0; }
            var probed = await Task.Run(() => known.Select(f => (File: f, Info: FilmProbe(f))).ToList());
            var pictures = probed.Where(p => !MediaStudio.IsAudio(p.File)).ToList(); var sound = probed.FirstOrDefault(p => MediaStudio.IsAudio(p.File));
            var done = 0; string note = "";
            d.Edit(known.Count == 1 ? "Material hinzugefügt" : $"{known.Count} Dateien hinzugefügt", x =>
            {
                if (sound.File is not null) { FilmCutOptions.Set(x, "music", sound.File); FilmCutOptions.Set(x, "musicLength", Math.Round(sound.Info.Length, 3)); done++; note = $" „{Path.GetFileName(sound.File)}“ ist jetzt die Musik."; }
                if (pictures.Count == 0) return;
                var shots = FilmCut.Shots(x);
                if (shots.Count == 0) { done += FilmCut.AddClipsAsShots(x, [.. pictures.Select(p => (p.File, p.Info.Length, p.Info.HasAudio))]); return; }
                // gezielt auf eine Einstellung gelegt: diese bekommt die erste Datei (auch als Ersatz); alle weiteren Dateien füllen nur Einstellungen ohne Material
                var k = shotId is null ? 0 : Math.Max(0, shots.FindIndex(s => s.Id == shotId)); var first = shotId is not null; var any = false;
                foreach (var p in pictures)
                {
                    while (!first && k < shots.Count && shots[k].Kind != "karte") k++;
                    if (k >= shots.Count) { note += $" {pictures.Count - pictures.IndexOf(p)} Datei(en) blieben übrig – alle Einstellungen haben schon Material. Zieh eine Datei direkt auf eine Einstellung, um sie zu ersetzen."; break; }
                    FilmCut.Assign(x, FilmCut.Shots(x)[k], p.File, p.Info.Length, p.Info.HasAudio);
                    if (!any) FilmSelected = shots[k].Id;
                    any = true; first = false; k++; done++;
                }
            });
            if (FilmCut.Shots(d.Data).Count > 0 && FilmSelected is null) FilmSelected = FilmCut.Shots(d.Data)[0].Id;
            _filmStep = "shots";
            _core.Audit.Append(Principal.LocalOwner.Name, "film.material", $"{done} Dateien");
            FilmSay(done == 0 ? "Nichts übernommen." + note : $"{done} Datei(en) übernommen." + note);
            return done;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException) { FilmSay(e.Message); return 0; }
    }

    public async Task<int> FilmPickMaterial(ProgramDocument d, string? shotId = null)
    {
        var got = await FilmPick("film-material", "Clips und Fotos wählen", shotId is null, [.. FilmVideoExt, .. FilmImageExt]);
        return got.Length == 0 ? 0 : await FilmImport(d, got, shotId);
    }

    public async Task FilmPickMusic(ProgramDocument d)
    {
        var got = await FilmPick("film-musik", "Musik wählen", false, FilmAudioExt);
        if (got.Length > 0) await FilmImport(d, [got[0]]);
    }

    /// <summary>Sprecher- oder Dialogton für die gewählte Einstellung.</summary>
    public async Task FilmPickVoice(ProgramDocument d)
    {
        if (FilmShotSelected(d) is not { } shot) { FilmSay("Erst eine Einstellung anklicken."); return; }
        var got = await FilmPick("film-ton", "Sprecherton wählen", false, FilmAudioExt);
        if (got.Length == 0) return;
        try
        {
            var info = await Task.Run(() => FilmProbe(got[0]));
            d.Edit("Sprecherton", x => FilmCut.SetVoice(x, shot, got[0], info.Length));
            FilmSay($"„{Path.GetFileName(got[0])}“ ist der Sprecherton von {shot.Id}.");
        }
        catch (InvalidOperationException e) { FilmSay(e.Message); }
    }

    void FilmSelect(ProgramDocument d, int by)
    {
        var shots = FilmCut.Shots(d.Data); if (shots.Count == 0) return;
        var i = Math.Clamp(Math.Max(0, shots.FindIndex(s => s.Id == FilmSelected)) + by, 0, shots.Count - 1);
        FilmSelected = shots[i].Id; _filmFocusList = true; Show(FilmKey);
    }

    void FilmMove(ProgramDocument d, int by)
    {
        if (FilmShotSelected(d) is not { } shot) throw new InvalidOperationException("Erst eine Einstellung anklicken.");
        var moved = false;
        d.Edit(by < 0 ? "Einstellung nach vorn" : "Einstellung nach hinten", x => moved = FilmCut.Move(x, shot, by));
        if (!moved) throw new InvalidOperationException("Hier ist die Szene zu Ende. Einstellungen tauschen ihren Platz nur innerhalb einer Szene – für andere Szenen verschiebst du die Zeile im Drehbuch.");
        var shots = FilmCut.Shots(d.Data); var sig = FilmCut.Signature(shot.Shot);
        FilmSelected = shots.FirstOrDefault(s => s.SceneIndex == shot.SceneIndex && s.Number == shot.Number + by && FilmCut.Signature(s.Shot) == sig)?.Id ?? FilmSelected;
        _filmFocusList = true;
    }

    void FilmUnassign(ProgramDocument d)
    {
        if (FilmShotSelected(d) is not { Material.HasPicture: true } shot) throw new InvalidOperationException("Diese Einstellung hat kein Material.");
        d.Edit("Material entfernt", x => FilmCut.Unassign(x, shot)); _filmFocusList = true;
        _progMsg[FilmKey] = $"{shot.Id} hat kein Material mehr – im Film steht dort eine Texttafel.";
    }

    // ---------- Storyboard-Bild mit der lokalen Bild-KI ----------
    /// <summary>Malt für die gewählte Einstellung ein Storyboard-Bild mit der lokalen Bild-KI (derselbe Weg wie „Bilder mit KI“; Test: <see cref="ImageAiOverride"/>).
    /// Ist keine Bild-KI eingerichtet, bleibt es bei der Texttafel – das wird gesagt, nichts wird vorgetäuscht.</summary>
    public async Task<bool> FilmStoryboardImage(ProgramDocument d)
    {
        if (_filmBusy) { FilmSay("Es wird schon gearbeitet – einen Moment."); return false; }
        if (FilmShotSelected(d) is not { } shot) { FilmSay("Erst eine Einstellung anklicken."); return false; }
        var looks = d.List("figures").Concat(d.List("places")).OfType<JsonObject>().Where(o => ((string?)o["look"] ?? "").Length > 0 &&
            (shot.Shot.Description.Contains((string?)o["name"] ?? "\0", StringComparison.OrdinalIgnoreCase) || shot.Scene.Location.Equals((string?)o["name"], StringComparison.OrdinalIgnoreCase) || shot.Scene.Characters.Contains((string?)o["name"] ?? "", StringComparer.OrdinalIgnoreCase)))
            .Select(o => (string)o["look"]!).ToList();
        var prompt = $"{shot.Shot.Type}, {shot.Shot.Description}, {shot.Scene.Location}{(shot.Scene.Time.Length > 0 ? ", " + shot.Scene.Time : "")}" + (looks.Count > 0 ? ", " + string.Join(", ", looks) : "") + ", Filmstill";
        if (ImageAiKit.Blocked(prompt) is { } blocked) { FilmSay(blocked); return false; }
        var cm = ImageAiOverride is null ? CoverModel() : null;
        if (ImageAiOverride is null && (cm is null || _opt.Isolated)) { FilmSay("Die Bild-KI ist noch nicht eingerichtet (KI-Bibliothek). Bis dahin steht im Film eine Texttafel – oder du wählst ein eigenes Bild."); return false; }
        var xl = cm?.Model.Id != "sd-turbo"; var o = FilmCutOptions.Of(d.Data);
        var (w, h) = ImageAiKit.Size(o.Height > o.Width ? "hoch" : "quer", xl ? 1024 : 512);
        var seed = ImageAiKit.Seed(42, shot.Number);
        var png = Path.Combine(FilmDir(d), $"storyboard-{shot.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        _filmBusy = true; FilmSay($"Male das Storyboard-Bild für {shot.Id} …");
        try
        {
            bool ok;
            using (await GpuSlot("bild", "Storyboard-Bild"))
            {
                if (ImageAiOverride is { } fake) ok = await fake(ImageAiKit.FullPrompt(prompt, "foto"), seed, png, w, h);
                else
                {
                    var args = ImageAiKit.Args(KiStore.ModelFile(cm!.Value.Model, cm.Value.Model.Files[0]), prompt, "foto", "", png, w, h, seed, xl);
                    var r = await Task.Run(() => RunWithInput(cm.Value.Exe, args, null));
                    if (!r.Ok || !File.Exists(png)) r = await Task.Run(() => RunWithInput(cm.Value.Exe, [.. args, "--offload-to-cpu"], null));
                    ok = r.Ok;
                }
            }
            if (!ok || !File.Exists(png)) { FilmSay("Die Bild-KI hat kein Bild geliefert. Ist die Grafikkarte gerade mit etwas anderem beschäftigt?"); return false; }
            ImageAiKit.WriteProvenance(png, new("stable-diffusion.cpp", cm?.Model.Id ?? "test", prompt, "foto", "", seed, w, h, DateTime.UtcNow, ""));
            d.Edit("Storyboard-Bild gemalt", x => { if (FilmCut.Shots(x).FirstOrDefault(s => s.Id == shot.Id) is { } now) FilmCut.Assign(x, now, png, 0, false, "ki"); });
            _core.Audit.Append(Principal.LocalOwner.Name, "film.storyboard", shot.Id);
            FilmSay($"Storyboard-Bild für {shot.Id} ist da (von der Bild-KI gemalt).");
            return true;
        }
        finally { _filmBusy = false; }
    }

    // ---------- Rohschnitt, Rendern, Übergabe ----------
    /// <summary>Rohschnitt bauen: Plan, fehlende Texttafeln erzeugen, Untertiteldatei schreiben, Projekt des Videoschnitts.</summary>
    async Task<(FilmCutPlan Plan, JsonObject Project, string? Srt)?> FilmBuild(ProgramDocument d, string srtFile)
    {
        var dir = FilmDir(d);
        var plan = FilmCut.Plan(d.Data, d.Title, Path.Combine(dir, "Tafeln"));
        if (plan.Shots.Count == 0) { FilmSay(plan.Problems.FirstOrDefault() ?? "Das Drehbuch hat noch keine Einstellungen."); return null; }
        if (plan.Cards.Any(c => !File.Exists(c.File)))
        {
            if (FilmRunner(dir, out var why) is not { } run) { FilmSay(why + " Für Titel und Texttafeln wird FFmpeg gebraucht."); return null; }
            var failed = await Task.Run(() => FilmCut.MakeCards(plan, run));
            if (failed.Count > 0) { FilmSay("Eine Texttafel konnte nicht erzeugt werden: " + failed[0]); return null; }
        }
        string? srt = null;
        if (plan.Subtitles.Count > 0 && plan.Options.Subtitles != FilmCutOptions.SubsNone) { srt = srtFile; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(srt))!); File.WriteAllText(srt, FilmCut.Srt(plan)); }
        return (plan, FilmCut.ToProject(plan, srt), srt);
    }

    /// <summary>„Film rendern“: echter Export als MP4 über den FFmpeg-Weg des Videoschnitts, im Hintergrund mit Fortschritt; <see cref="FilmRenderCancel"/> bricht ab und löscht die halbe Datei.</summary>
    public async Task<string?> FilmRender(ProgramDocument d)
    {
        if (_filmRender is not null) { FilmSay("Der Film wird gerade gerendert – du kannst abbrechen oder warten."); return null; }
        var folder = WorkFolder(Programs[FilmKey].Folder); var name = DevStudio.SafeName(FilmTitle(d));
        var file = Path.Combine(folder, name + ".mp4");
        for (var i = 2; File.Exists(file); i++) file = Path.Combine(folder, $"{name}-{i}.mp4");
        var cts = _filmRender = new CancellationTokenSource(); FilmRenderProgress = 0; _filmStep = "shots";
        FilmSay("Der Rohschnitt entsteht …");
        try
        {
            if (await FilmBuild(d, Path.ChangeExtension(file, ".srt")) is not { } cut) return null;
            _progMsg[FilmKey] = $"Der Film wird gerendert ({Clock(cut.Plan.Seconds)}) – du kannst weiterarbeiten."; Show(FilmKey);
            var progress = new Progress<double>(v => { FilmRenderProgress = v; if (_filmBar is { } b) b.Value = v * 100; });
            var opts = new VideoExport.Options(VideoExport.Presets[0], Subtitles: cut.Plan.Options.Subtitles == FilmCutOptions.SubsBurn ? cut.Srt : null);
            string o;
            if (FilmFfmpegPath is null && ExportAsyncOverride is { } fake) o = await fake(cut.Project, file, progress, cts.Token);
            else
            {
                var ff = FilmFf ?? throw new InvalidOperationException(_opt.Isolated ? "Im abgeschotteten Testmodus startet Trendo kein FFmpeg." : "Für das Rendern fehlt FFmpeg – unter Zusatzprogramme einrichten.");
                o = await VideoEdit.ExportAsync(ff, cut.Project, file, progress, cts.Token, opts);
            }
            FilmLastOutput = o;
            _core.Audit.Append(Principal.LocalOwner.Name, "film.render", Path.GetFileName(o));
            var notes = new List<string> { $"Film fertig: {o}" };
            if (cut.Srt is not null) notes.Add(cut.Plan.Options.Subtitles == FilmCutOptions.SubsBurn ? "Untertitel sind im Bild." : $"Untertitel liegen daneben ({Path.GetFileName(cut.Srt)}).");
            var cards = cut.Plan.Pieces.Count(p => p.Kind == "shot" && p.Card is not null);
            if (cards > 0) notes.Add($"{cards} Einstellung(en) ohne Material stehen als Texttafel im Film.");
            notes.AddRange(cut.Plan.Problems);
            _progMsg[FilmKey] = string.Join(" ", notes); NotifyFileResult(_progMsg[FilmKey]);
            return o;
        }
        catch (OperationCanceledException) { _progMsg[FilmKey] = "Rendern abgebrochen – die halbe Datei wurde gelöscht."; try { var srt = Path.ChangeExtension(file, ".srt"); if (File.Exists(srt)) File.Delete(srt); } catch (IOException) { } return null; }
        catch (Exception e) when (e is InvalidOperationException or IOException or FileNotFoundException or UnauthorizedAccessException) { _progMsg[FilmKey] = e.Message; return null; }
        finally { _filmRender = null; _filmBar = null; cts.Dispose(); Show(FilmKey); }
    }

    public void FilmRenderCancel() => _filmRender?.Cancel();

    /// <summary>„Im Videoschnitt weiterbearbeiten“: derselbe Rohschnitt als neues Projekt im Videoschnitt (dort frei schneiden, Farbe, Effekte, alle Exportformate).</summary>
    public async Task<ProgramDocument?> FilmToEditor(ProgramDocument d)
    {
        try
        {
            var title = FilmTitle(d);
            if (await FilmBuild(d, Path.Combine(FilmDir(d), DevStudio.SafeName(title) + ".srt")) is not { } cut) return null;
            var v = ProgramNew(VK); v.Title = title;
            v.Edit("Rohschnitt aus „Filme machen“", x => { x.Clear(); foreach (var (k, n) in cut.Project.ToList()) { cut.Project.Remove(k); x[k] = n; } });
            EditSelected = null;
            _progMsg[VK] = $"Rohschnitt aus „Filme machen“: {cut.Plan.Pieces.Count} Stücke, {Clock(cut.Plan.Seconds)}. Hier schneidest du frei weiter – das Drehbuch bleibt in „Filme machen“.";
            _core.Audit.Append(Principal.LocalOwner.Name, "film.schnitt", title);
            Show(VK);
            return v;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException) { FilmSay(e.Message); return null; }
    }

    // ---------- KI ----------
    /// <summary>Drehbuch oder Shotliste von außen ins offene Dokument übernehmen (KI-Auftrag, Fountain-Datei): ein Schritt, rückgängig machbar. Öffnet bei Bedarf ein neues Dokument.</summary>
    public ProgramDocument FilmAdoptScript(string fountain, string label)
    {
        var d = ProgramActive(FilmKey) ?? ProgramNew(FilmKey, "leer");
        var text = fountain.Replace("\r\n", "\n");
        d.Edit(label, x => x["script"] = text);
        if (Fountain.Parse(text).Title is { Length: > 0 } t && d.Title is "Unbenannt" or "") d.Title = t;
        return d;
    }

    readonly HashSet<string> _filmAdopted = [];

    /// <summary>Ergebnis des KI-Auftrags „Drehbuch-Gerüst“ (Auftragszeile): Das Drehbuch mit Shotliste kommt ins offene Dokument statt nur als Datei in den Projektordner.
    /// Jede Fassung genau einmal; „Rückgängig“ im Programm holt den vorigen Stand zurück.</summary>
    public bool FilmAdoptResult(Trendo.Core.PromptAction.StudioExecutionResult result)
    {
        if (result.Status != Trendo.Core.PromptAction.RunStatus.Verified || _promptProject is null) return false;
        if (result.Artifacts.FirstOrDefault(a => a.RelativePath.EndsWith("drehbuch.fountain", StringComparison.OrdinalIgnoreCase)) is not { } art) return false;
        if (!_filmAdopted.Add(result.RunId + "|" + art.RelativePath)) return false;
        try
        {
            var text = File.ReadAllText(_core.Projects.ResolveInProject(_promptProject, art.RelativePath));
            var (script, msg) = FilmWriter.Accept(text);
            if (script is null) return false;
            FilmAdoptScript(script, "Drehbuch aus dem KI-Auftrag"); _filmStep = "drehbuch"; FilmSelected = null;
            _progMsg[FilmKey] = $"Das Drehbuch-Gerüst steht im Dokument: {msg} Es ist ein Gerüst – die Szenen schreibst du aus (oder „Drehbuch mit KI schreiben“). „Rückgängig“ holt den alten Stand zurück.";
            if (CurrentView == FilmKey) Show(FilmKey);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { return false; }
    }

    /// <summary>KI-Aktion: aus der Idee ein Drehbuch mit Shotliste schreiben – ins offene Dokument, bearbeitbar und rückgängig machbar. Rendern bleibt ein eigener Klick.
    /// Liefert die KI kein lesbares Drehbuch oder ist keine KI erreichbar, bleibt das Dokument unverändert.</summary>
    public async Task<bool> FilmWrite(ProgramDocument d, string? idea = null)
    {
        if (_filmBusy) { FilmSay("Es wird schon gearbeitet – einen Moment."); return false; }
        idea = (idea ?? d.Text("logline")).Trim();
        if (idea.Length == 0) { _filmStep = "drehbuch"; FilmSay("Schreib rechts in einem Satz, worum es geht („Worum geht es?“). Daraus schreibt die KI das Drehbuch."); return false; }
        var prompt = FilmWriter.Prompt(idea, d.Text("genre"), d.Text("script"));
        _filmBusy = true; FilmSay("Die KI schreibt das Drehbuch …");
        try
        {
            string? answer;
            if (FilmKiOverride is { } fake) answer = await fake(prompt);
            else if (_opt.Isolated && !_kiConnected) { FilmSay("Im abgeschotteten Testmodus wird keine KI gefragt."); return false; }
            else
            {
                using var label = KiActivityHub.Label("Drehbuch schreiben");
                answer = await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : null;
            }
            if (answer is null) { FilmSay("Gerade ist keine KI erreichbar. Richte unter „KI“ ein lokales Modell ein oder verbinde Claude – am Drehbuch wurde nichts geändert."); return false; }
            var (script, msg) = FilmWriter.Accept(answer);
            if (script is null) { FilmSay(msg); return false; }
            if (d.Text("logline").Length == 0) d.Set("logline", idea, "Worum geht es");
            FilmAdoptScript(script, "Drehbuch von der KI");
            _filmStep = "drehbuch"; FilmSelected = null;
            _core.Audit.Append(Principal.LocalOwner.Name, "film.ki", msg);
            FilmSay($"Die KI hat das Drehbuch geschrieben: {msg} Lies es durch und ändere, was du willst – „Rückgängig“ holt den alten Stand zurück.");
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or TaskCanceledException) { FilmSay("Die KI hat nicht geantwortet: " + e.Message); return false; }
        finally { _filmBusy = false; }
    }

    /// <summary>Drehbuch aus einer Fountain-Datei übernehmen (auch aus anderen Programmen wie Highland, Fade In, Beat, Trelby …).</summary>
    public async Task FilmOpenFountain(ProgramDocument d)
    {
        var got = await FilmPick("film-fountain", "Drehbuch öffnen", false, ["fountain", "spmd", "txt"]);
        if (got.Length == 0) return;
        try
        {
            var text = File.ReadAllText(got[0]);
            var b = FilmBreakdowns.Of(Fountain.Parse(text));
            if (b.Scenes.Count == 0) { FilmSay($"In „{Path.GetFileName(got[0])}“ steht kein Szenenkopf wie „INNEN. KÜCHE – TAG“ – es wurde nichts übernommen."); return; }
            FilmAdoptScript(text, "Drehbuch geöffnet"); _filmStep = "drehbuch"; FilmSelected = null;
            FilmSay($"„{Path.GetFileName(got[0])}“ übernommen: {b.Scenes.Count} Szenen, {b.Roles.Count} Figuren, {b.ShotCount} Einstellungen.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { FilmSay("Die Datei lässt sich nicht lesen: " + e.Message); }
    }

    // ---------- Oberfläche: links Szenen und Einstellungen, Mitte Vorschau und Storyboard-Streifen ----------
    static readonly IBrush FilmCardBg = Brush.Parse("#12151B"), FilmGold = Brush.Parse("#E0B84A");

    Bitmap? FilmThumb(ProgramDocument d, FilmShot s, int width)
    {
        if (s.Material is not { HasPicture: true } m || !File.Exists(m.File)) return null;
        try
        {
            if (m.IsImage) { using var fs = File.OpenRead(m.File); return Bitmap.DecodeToWidth(fs, width); }
            var dir = Path.Combine(FilmDir(d), ".vorschau");
            var jpg = Path.Combine(dir, Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(m.File + File.GetLastWriteTimeUtc(m.File).Ticks + "|" + m.In.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))))[..14] + ".png");
            if (File.Exists(jpg)) { using var fs = File.OpenRead(jpg); return Bitmap.DecodeToWidth(fs, width); }
            if (FilmFf is { } ff && _filmThumbBusy.Add(jpg))
                _ = Task.Run(() => { Directory.CreateDirectory(dir); Cli.Run(ff, MediaStudio.FrameArgs(m.File, m.In + Math.Min(0.5, (m.Out - m.In) / 2), jpg), dir, 30000); })
                    .ContinueWith(_ => Dispatcher.UIThread.Post(() => { if (CurrentView == FilmKey && File.Exists(jpg)) Show(FilmKey); }), TaskScheduler.Default);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
        return null;
    }

    /// <summary>Bild einer Einstellung für Vorschau und Streifen: Foto, Standbild des Clips oder die Texttafel, wie sie im Film steht.</summary>
    Control FilmPicture(ProgramDocument d, FilmShot s, double width, bool large)
    {
        var o = FilmCutOptions.Of(d.Data); var height = width * o.Height / Math.Max(1, o.Width);
        if (height > width * 1.2) { height = width; width = height * o.Width / Math.Max(1, o.Height); }
        Control inner;
        if (FilmThumb(d, s, large ? 960 : 240) is { } bmp) inner = new Image { Source = bmp, Stretch = Stretch.Uniform };
        else if (s.Kind == "karte")
        {
            var f = large ? 1.0 : 0.34;
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2 * f };
            sp.Children.Add(new TextBlock { Text = s.Shot.Type + " · " + s.Scene.Location, Foreground = Brush.Parse("#A9AEB8"), FontSize = Math.Max(7, 13 * f), HorizontalAlignment = HorizontalAlignment.Center });
            foreach (var line in FilmCut.Wrap(s.Shot.Description, 44).Take(large ? 4 : 2)) sp.Children.Add(new TextBlock { Text = line, Foreground = Brushes.White, FontSize = Math.Max(7.5, 20 * f), HorizontalAlignment = HorizontalAlignment.Center });
            inner = sp;
        }
        else inner = new TextBlock { Text = s.Material is { } m && !File.Exists(m.File) ? "Datei fehlt" : large ? Path.GetFileName(s.Material!.File) : "Clip", Foreground = Brush.Parse("#A9AEB8"), FontSize = large ? 13 : 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        return new Border { Width = width, Height = height, Background = s.Kind == "karte" ? FilmCardBg : Brushes.Black, CornerRadius = new CornerRadius(large ? 8 : 5), ClipToBounds = true, Child = inner };
    }

    static string FilmKindLabel(FilmShot s) => s.Kind switch { "video" => "Clip", "bild" => s.Material!.Source == "ki" ? "Bild (KI)" : "Foto", _ => "Texttafel" };

    void FilmAcceptDrop(Control target, ProgramDocument d, Func<string?> shotId)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        target.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];
            if (files.Count == 0) return;
            e.Handled = true; _ = FilmImport(d, files, shotId());
        });
    }

    ContextMenu FilmShotMenu(ProgramDocument d, FilmShot s)
    {
        MenuItem Item(string name, string header, Action run, bool on = true) { var m = new MenuItem { Name = name, Header = header, IsEnabled = on }; m.Click += (_, _) => run(); return m; }
        void Sel() => FilmSelected = s.Id;
        return new ContextMenu { Name = "film-shot-menu", Items = {
            Item("film-menu-material", "Clip oder Foto zuweisen …", () => { Sel(); _ = FilmPickMaterial(d, s.Id); }),
            Item("film-menu-storyboard", "Storyboard-Bild malen (Bild-KI)", () => { Sel(); _ = FilmStoryboardImage(d); }),
            Item("film-menu-ton", "Sprecherton wählen …", () => { Sel(); _ = FilmPickVoice(d); }),
            Item("film-menu-entfernen", "Material entfernen (Entf)", () => { Sel(); ProgramRun(FilmKey, () => FilmUnassign(d)); }, s.Material is { HasPicture: true }),
            new Separator(),
            Item("film-menu-vor", "Nach vorn (Alt+↑)", () => { Sel(); ProgramRun(FilmKey, () => FilmMove(d, -1)); }),
            Item("film-menu-hinter", "Nach hinten (Alt+↓)", () => { Sel(); ProgramRun(FilmKey, () => FilmMove(d, 1)); }),
        } };
    }

    Control FilmProduction(ProgramDocument d)
    {
        var shots = FilmCut.Shots(d.Data); var b = FilmOf(d);
        var root = new Grid { Name = "film-production", ColumnDefinitions = new ColumnDefinitions("300,12,*"), Margin = new Thickness(4, 0, 10, 4), Focusable = true, Background = Brushes.Transparent };
        FilmAcceptDrop(root, d, () => null);

        if (shots.Count == 0)
        {
            // Leerzustand: der erste Schritt steht da
            var write = Act("film-empty-write", "Drehbuch schreiben", () => { _filmStep = "drehbuch"; Show(FilmKey); });
            var clips = Act("film-empty-clips", "Clips hinzufügen", () => _ = FilmPickMaterial(d));
            var empty = new StackPanel { Name = "film-empty", Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 520 };
            empty.Children.Add(H("Noch keine Einstellungen", 17));
            empty.Children.Add(P(b.Scenes.Count > 0 ? "Dein Drehbuch hat Szenen, aber noch keine Einstellungen. Oben „Einstellungen vorschlagen“ füllt sie – oder du legst gleich deine Clips und Fotos hier ab." : "Ein Film besteht aus Einstellungen. Schreib zuerst das Drehbuch – oder zieh deine Clips und Fotos hierher, dann wird aus jeder Datei eine Einstellung.", Muted, 13));
            empty.Children.Add(new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { write, new Border { Width = 8 }, clips } });
            Grid.SetColumnSpan(empty, 3); root.Children.Add(empty);
            return root;
        }
        var sel = shots.FirstOrDefault(s => s.Id == FilmSelected) ?? shots[0]; FilmSelected = sel.Id;

        // links: Szenen und Einstellungen
        var list = new StackPanel { Name = "film-shots", Spacing = 3 };
        foreach (var p in b.Problems) list.Children.Add(P("Bitte prüfen: " + p, Brush.Parse("#F5A524"), 11.5));
        var lastScene = -1;
        foreach (var s in shots)
        {
            if (s.SceneIndex != lastScene)
            {
                lastScene = s.SceneIndex;
                var head = H($"{s.Scene.Code} · {s.Scene.Place}. {s.Scene.Location}{(s.Scene.Time.Length > 0 ? " – " + s.Scene.Time : "")}", 12.5); head.TextWrapping = TextWrapping.Wrap; head.Margin = new Thickness(2, 8, 0, 2);
                list.Children.Add(head);
            }
            var on = s.Id == sel.Id;
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("74,*,46"), Children = {
                new TextBlock { Text = s.Id, Foreground = on ? Text : Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center },
                Pos(new StackPanel { Children = {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = s.Shot.Type, Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = FilmKindLabel(s), Foreground = s.Kind == "karte" ? Muted : FilmGold, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center } } },
                    new TextBlock { Text = s.Shot.Description, Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1),
                Pos(new TextBlock { Text = s.Seconds.ToString("0.#", De) + " s", Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }, 2) } };
            var row = new Button { Name = "film-shot-" + s.Id, Content = line, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 5), CornerRadius = new CornerRadius(7),
                Background = on ? Brush.Parse("#22E0B84A") : Brushes.Transparent, BorderBrush = on ? FilmGold : Brushes.Transparent, BorderThickness = new Thickness(1), ContextMenu = FilmShotMenu(d, s) };
            ToolTip.SetTip(row, $"{s.Id}: {s.Shot.Description}\nRechtsklick: Material zuweisen, entfernen, verschieben. Dateien hierher ziehen weist sie zu.");
            row.Click += (_, _) => { FilmSelected = s.Id; _filmFocusList = true; Show(FilmKey); };
            FilmAcceptDrop(row, d, () => s.Id);
            list.Children.Add(row);
        }
        root.Children.Add(new ScrollViewer { Content = list });

        // Mitte: Vorschau der gewählten Einstellung, darunter der Storyboard-Streifen
        var plan = FilmCut.Plan(d.Data, d.Title, Path.Combine(FilmDir(d), "Tafeln"));
        var mid = new DockPanel();
        var strip = new StackPanel { Name = "film-strip", Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var s in shots)
        {
            var tile = new Button { Name = "film-strip-" + s.Id, Padding = new Thickness(3), CornerRadius = new CornerRadius(7), Background = Brushes.Transparent, BorderBrush = s.Id == sel.Id ? FilmGold : Brushes.Transparent, BorderThickness = new Thickness(2), ContextMenu = FilmShotMenu(d, s),
                Content = new StackPanel { Spacing = 2, Children = { FilmPicture(d, s, 128, false), new TextBlock { Text = $"{s.Id} · {s.Seconds.ToString("0.#", De)} s", Foreground = Muted, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center } } } };
            tile.Click += (_, _) => { FilmSelected = s.Id; _filmFocusList = true; Show(FilmKey); };
            FilmAcceptDrop(tile, d, () => s.Id);
            strip.Children.Add(tile);
        }
        var stripBox = new Border { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Bottom, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(6), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#0B0D12"), BorderBrush = PaneEdge, BorderThickness = new Thickness(1),
            Child = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = strip } };
        mid.Children.Add(stripBox);

        var info = new StackPanel { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Bottom, Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        var what = sel.Material is { HasPicture: true } m ? $"{FilmKindLabel(sel)}: {Path.GetFileName(m.File)}" + (sel.Kind == "video" ? $" · Ausschnitt {m.In.ToString("0.#", De)}–{m.Out.ToString("0.#", De)} s von {m.SourceLength.ToString("0.#", De)} s" : "") + (File.Exists(m.File) ? "" : " · Datei fehlt")
            : "Noch kein Material – im Film steht diese Texttafel. Zieh einen Clip oder ein Foto hierher.";
        info.Children.Add(new TextBlock { Name = "film-sel-info", Text = $"{sel.Id} · {sel.Shot.Type} · {sel.Seconds.ToString("0.#", De)} s · {what}", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var acts = new WrapPanel();
        void AddAct(Button x) { x.Margin = new Thickness(0, 0, 6, 4); acts.Children.Add(x); }
        AddAct(Act("film-assign", sel.Material is { HasPicture: true } ? "Anderes Material …" : "Clip oder Foto zuweisen …", () => _ = FilmPickMaterial(d, sel.Id)));
        if (sel.Kind == "karte") AddAct(Act("film-storyboard", "Storyboard-Bild malen", () => _ = FilmStoryboardImage(d)));
        if (sel.Material is { HasPicture: true }) AddAct(Act("film-unassign", "Material entfernen", () => ProgramRun(FilmKey, () => FilmUnassign(d))));
        if (sel.Kind == "video" && !_opt.Isolated && File.Exists(sel.Material!.File)) AddAct(Act("film-play", "Clip abspielen", () => Launch(sel.Material!.File)));
        info.Children.Add(acts);
        if (_filmRender is not null)
        {
            _filmBar = new ProgressBar { Name = "film-render-bar", Minimum = 0, Maximum = 100, Value = FilmRenderProgress * 100, Height = 8, MinWidth = 220 };
            info.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _filmBar, Pos(Act("film-render-cancel", "Abbrechen", FilmRenderCancel), 1) } });
        }
        else if (FilmLastOutput is { } last && File.Exists(last))
        {
            var open = Act("film-open-result", "Film ansehen", () => { if (!_opt.Isolated) Launch(last); });
            var folder = Act("film-open-folder", "Ordner öffnen", () => { if (!_opt.Isolated) Launch(Path.GetDirectoryName(last)!); });
            info.Children.Add(new WrapPanel { Children = { new TextBlock { Name = "film-result", Text = "Fertig: " + Path.GetFileName(last), Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) }, open, new Border { Width = 6 }, folder } });
        }
        var cards = shots.Count(s => s.Kind == "karte");
        info.Children.Add(new TextBlock { Name = "film-plan-info", Text = $"Der Film: {plan.Pieces.Count} Stücke, {Clock(plan.Seconds)} · {shots.Count - cards} von {shots.Count} Einstellungen mit Material" + (plan.Subtitles.Count > 0 && plan.Options.Subtitles != FilmCutOptions.SubsNone ? $" · {plan.Subtitles.Count} Untertitel" : "") + (plan.Options.Music.Length > 0 ? " · Musik" : ""), Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        mid.Children.Add(info);
        var view = new Border { Name = "film-view", Background = Brush.Parse("#05070C"), CornerRadius = new CornerRadius(10), Padding = new Thickness(8), MinHeight = 120, Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = FilmPicture(d, sel, 720, true) } };
        FilmAcceptDrop(view, d, () => sel.Id);
        mid.Children.Add(view);
        Grid.SetColumn(mid, 2); root.Children.Add(mid);

        root.KeyDown += (_, e) =>
        {
            if (e.Source is TextBox) return;
            var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
            switch (e.Key)
            {
                case Key.Up when alt: ProgramRun(FilmKey, () => FilmMove(d, -1)); break;
                case Key.Down when alt: ProgramRun(FilmKey, () => FilmMove(d, 1)); break;
                case Key.Up: FilmSelect(d, -1); break;
                case Key.Down: FilmSelect(d, 1); break;
                case Key.Delete: ProgramRun(FilmKey, () => FilmUnassign(d)); break;
                case Key.Enter: _ = FilmPickMaterial(d, sel.Id); break;
                default: return;
            }
            e.Handled = true;
        };
        if (_filmFocusList) { _filmFocusList = false; Dispatcher.UIThread.Post(() => { if (root.IsAttachedToVisualTree()) root.Focus(); }, DispatcherPriority.Background); }
        return root;
    }

    // ---------- rechts: Eigenschaften der Einstellung und des Rohschnitts ----------
    Control FilmCutProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "film-cut-props", Spacing = 4 };
        var o = FilmCutOptions.Of(d.Data);
        TextBox Num(string name, string label, double value, Action<double> set)
        {
            var t = new TextBox { Name = name, Text = value.ToString("0.##", De), Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
            void Commit()
            {
                if (!double.TryParse((t.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) { t.Text = value.ToString("0.##", De); return; }
                if (Math.Abs(v - value) > 0.0005) ProgramRun(FilmKey, () => set(v));
            }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(P(label, null, 11)); sp.Children.Add(t);
            return t;
        }
        void Check(string name, string label, bool value, string key)
        {
            var c = new CheckBox { Name = name, Content = label, IsChecked = value, Foreground = Text, FontSize = 12 };
            c.IsCheckedChanged += (_, _) => { var v = c.IsChecked == true; if (v != value) ProgramRun(FilmKey, () => d.Edit(label, x => FilmCutOptions.Set(x, key, v))); };
            sp.Children.Add(c);
        }
        void Choice(string name, string label, (string Id, string Label)[] items, string value, Action<string> set)
        {
            var c = new ComboBox { Name = name, ItemsSource = items.Select(i => i.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(items, i => i.Id == value)), HorizontalAlignment = HorizontalAlignment.Stretch };
            c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0 && items[c.SelectedIndex].Id != value) ProgramRun(FilmKey, () => set(items[c.SelectedIndex].Id)); };
            sp.Children.Add(P(label, null, 11)); sp.Children.Add(c);
        }

        if (FilmShotSelected(d) is { } s)
        {
            sp.Children.Add(H("Einstellung " + s.Id, 13));
            sp.Children.Add(P($"{s.Shot.Type} · {FilmKindLabel(s)}", Text, 12));
            if (s.Kind == "video")
            {
                var m = s.Material!;
                Num("film-p-in", "Einstieg (Sekunde im Clip)", m.In, v => d.Edit("Einstieg", x => FilmCut.Trim(x, s, v, Math.Max(v + VideoEdit.MinLength, m.Out))));
                Num("film-p-out", "Ausstieg (Sekunde im Clip)", m.Out, v => d.Edit("Ausstieg", x => FilmCut.Trim(x, s, Math.Min(m.In, v - VideoEdit.MinLength), v)));
                sp.Children.Add(P($"Im Film: {s.Seconds.ToString("0.#", De)} s · der Clip ist {m.SourceLength.ToString("0.#", De)} s lang, geplant waren {s.Shot.Seconds.ToString("0.#", De)} s.", Muted, 11));
            }
            else Num("film-p-seconds", "Dauer in Sekunden", s.Shot.Seconds, v => d.Edit("Dauer", x => FilmCut.SetSeconds(x, s, v)));
            sp.Children.Add(P("Sprecherton", null, 11));
            var voice = s.Material is { Voice.Length: > 0 } mv ? Path.GetFileName(mv.Voice) : "keiner";
            var vrow = new WrapPanel { Children = { new TextBlock { Name = "film-p-voice", Text = voice, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, Act("film-voice-pick", "Wählen …", () => _ = FilmPickVoice(d)) } };
            if (s.Material is { Voice.Length: > 0 }) { vrow.Children.Add(new Border { Width = 4 }); vrow.Children.Add(Act("film-voice-remove", "Entfernen", () => ProgramRun(FilmKey, () => d.Edit("Sprecherton entfernt", x => FilmCut.SetVoice(x, s, "", 0))))); }
            sp.Children.Add(vrow);
        }

        sp.Children.Add(H("Rohschnitt", 13));
        Check("film-o-title", "Titel am Anfang", o.OpeningTitle, "title");
        Check("film-o-credits", "Abspann mit den Figuren", o.Credits, "credits");
        Choice("film-o-scenes", "Zwischentitel", [(FilmCutOptions.TitlesActs, "Je Akt („# Erster Akt“ im Drehbuch)"), (FilmCutOptions.TitlesScenes, "Je Szene"), (FilmCutOptions.TitlesNone, "Keine")], o.SceneTitles, v => d.Edit("Zwischentitel", x => FilmCutOptions.Set(x, "sceneTitles", v)));
        Num("film-o-fade", "Überblenden zwischen Szenen (Sekunden, 0 = harter Schnitt)", o.Fade, v => d.Edit("Überblenden", x => FilmCutOptions.Set(x, "fade", Math.Clamp(v, 0, 3))));
        Choice("film-o-subs", "Untertitel aus den Dialogen", [(FilmCutOptions.SubsFile, "Als Datei daneben (SRT)"), (FilmCutOptions.SubsBurn, "Ins Bild einbrennen"), (FilmCutOptions.SubsNone, "Keine")], o.Subtitles, v => d.Edit("Untertitel", x => FilmCutOptions.Set(x, "subs", v)));
        Choice("film-o-format", "Format", [("1280x720", "Querformat HD (1280 × 720)"), ("1920x1080", "Querformat Full HD (1920 × 1080)"), ("1080x1920", "Hochformat (1080 × 1920)"), ("1080x1080", "Quadrat (1080 × 1080)")], $"{o.Width}x{o.Height}",
            v => d.Edit("Format", x => { var p = v.Split('x'); FilmCutOptions.Set(x, "width", int.Parse(p[0])); FilmCutOptions.Set(x, "height", int.Parse(p[1])); }));
        sp.Children.Add(P("Musik", null, 11));
        var mrow = new WrapPanel { Children = { new TextBlock { Name = "film-o-music", Text = o.Music.Length > 0 ? Path.GetFileName(o.Music) + (File.Exists(o.Music) ? "" : " (Datei fehlt)") : "keine", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, Act("film-music-pick", "Wählen …", () => _ = FilmPickMusic(d)) } };
        if (o.Music.Length > 0) { mrow.Children.Add(new Border { Width = 4 }); mrow.Children.Add(Act("film-music-remove", "Entfernen", () => ProgramRun(FilmKey, () => d.Edit("Musik entfernt", x => { FilmCutOptions.Set(x, "music", ""); FilmCutOptions.Set(x, "musicLength", 0); })))); }
        sp.Children.Add(mrow);
        if (o.Music.Length > 0) Num("film-o-music-vol", "Lautstärke der Musik (1 = wie die Datei)", o.MusicVolume, v => d.Edit("Lautstärke der Musik", x => FilmCutOptions.Set(x, "musicVolume", Math.Clamp(v, 0, 2))));
        var credits = new TextBox { Name = "film-o-credits-text", Text = o.CreditsText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54, PlaceholderText = "z. B. Regie: Erika Muster" };
        credits.LostFocus += (_, _) => { if ((credits.Text ?? "") != o.CreditsText) d.Edit("Mitwirkende", x => FilmCutOptions.Set(x, "creditsText", credits.Text ?? "")); };
        sp.Children.Add(P("Mitwirkende im Abspann (eine Zeile je Eintrag)", null, 11)); sp.Children.Add(credits);
        return sp;
    }
}
