using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3, S3-13 (07.11.2026): KI-Video im Videoschnitt – Beschreibung, Zufallswert, Länge und Qualität;
/// der Clip kommt mit Herkunftsnachweis (…​.herkunft.json) in die Zeitleiste. Ohne Modell oder ohne Ergebnis: erklärter Fehler, nichts eingefügt.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: eingerichtetes Video-Modell ersetzen (die Erzeugung selbst ersetzt <see cref="VideoKiOverride"/>).</summary>
    public GenVideo.Model? EditGenModelOverride { get; set; }
    public string? EditGenLastClip { get; private set; }
    bool _veGenBusy;
    string _veGenPrompt = "", _veGenSeed = "", _veGenQuality = GenVideo.Final.Id; // Standard: Endfassung (Vorschau mit 12 Schritten zeigt teils Kachel-Artefakte, gemessen 07.11.2026)
    double _veGenSeconds = 2;

    GenVideo.Model? EditGenModel()
    {
        if (EditGenModelOverride is { } o) return o;
        if (Model("wan2.2-szenen") is not { } m) return null;
        return new GenVideo.Model(m.Id, m.Name, m.License, KiStore.ModelFile(m, m.Files[0]), KiStore.ModelFile(m, m.Files[1]), KiStore.ModelFile(m, m.Files[2]));
    }

    public async Task<string?> EditGenVideo(ProgramDocument d, string prompt, long seed, double seconds, GenVideo.Quality q, string? startImage = null)
    {
        var m = EditGenModel(); var req = new GenVideo.Request(prompt, seed, seconds, q, startImage);
        if (GenVideo.Problem(req, m) is { } p) { _progMsg[VK] = p; Show(VK); return null; }
        if (_opt.Isolated && VideoKiOverride is null) { _progMsg[VK] = "Im abgeschotteten Testmodus startet Trendo keine Video-KI."; Show(VK); return null; }
        var dir = Path.Combine(MediaFolder, "KI-Video"); Directory.CreateDirectory(dir);
        if (FfmpegRunner(dir, out var why) is not { } run) { _progMsg[VK] = why!; Show(VK); return null; }
        var name = $"KI {DateTime.Now:yyyy-MM-dd HHmmss} s{seed}";
        var raw = Path.Combine(dir, name + ".roh.webm"); var clip = Path.Combine(dir, name + ".mp4");
        _veGenBusy = true; _progMsg[VK] = $"KI-Video entsteht ({q.Label}, {seconds:0.#} s, Zufallswert {seed}) – das kann einige Minuten dauern …"; Show(VK);
        try
        {
            bool ok;
            using (await GpuSlot("video", "KI-Video im Videoschnitt"))
                ok = await Tool(KiStore.RuntimeExe(KiRuntime.StableDiffusionCpp), GenVideo.Args(req, m!, raw), CancellationToken.None);
            if (!ok || !File.Exists(raw) || new FileInfo(raw).Length == 0) { _progMsg[VK] = "Die Video-KI hat keinen Clip geliefert – nichts eingefügt."; return null; }
            int w = (int)Math.Max(16, d.Number("width", 1280)), h = (int)Math.Max(16, d.Number("height", 720)), fps = (int)Math.Max(1, d.Number("fps", 30));
            var r = await Task.Run(() => run(GenVideo.NormalizeArgs(raw, seconds, w, h, fps, clip)));
            if (!r.Ok || !File.Exists(clip)) { _progMsg[VK] = "Der KI-Clip ließ sich nicht ins Projektformat bringen: " + (r.Err.Trim().Split('\n').LastOrDefault() ?? ""); return null; }
            var prov = GenVideo.Provenance(req, m!, "stable-diffusion.cpp", KiLibrary.RuntimeInfo(KiRuntime.StableDiffusionCpp).Tag, clip, raw, DateTime.UtcNow);
            GenVideo.WriteProvenance(clip, prov);
            TryDelete(raw);
            var title = "KI: " + (prompt.Trim().Length > 40 ? prompt.Trim()[..40] + "…" : prompt.Trim());
            d.Edit("KI-Video", x => { VideoEdit.Add(x, VideoEdit.NewClip(clip, seconds, false)); VideoEdit.SetClip(x, VideoEdit.Clips(x).Count - 1, 0, title); });
            EditGenLastClip = clip;
            _progMsg[VK] = $"KI-Clip in der Zeitleiste: {Path.GetFileName(clip)} – Herkunftsnachweis daneben (Beschreibung, Zufallswert {seed}, {m!.Name}, {m.License}).";
            return clip;
        }
        finally { _veGenBusy = false; Show(VK); }
    }

    Control EditGenVideoPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ve-genvideo", Spacing = 5, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(H("KI-Video (lokal)", 13));
        var m = EditGenModel();
        if (m is null)
        {
            sp.Children.Add(P("Kein Video-Modell eingerichtet. In der KI-Bibliothek unter „Videos erzeugen“ „Wan 2.2 – Filmszenen“ laden (Apache-2.0, läuft auf diesem PC).", Muted, 11));
            sp.Children.Add(Nav2("KI-Bibliothek öffnen", "ki-bibliothek"));
            return sp;
        }
        var prompt = new TextBox { Name = "ve-gen-prompt", Text = _veGenPrompt, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 50, FontSize = 12, PlaceholderText = "Beschreibung, z. B. „Kaffee wird in eine Tasse gegossen, Morgenlicht, Nahaufnahme“" };
        prompt.TextChanged += (_, _) => _veGenPrompt = prompt.Text ?? "";
        if (_veGenSeed.Length == 0) _veGenSeed = GenVideo.NewSeed().ToString();
        var seed = new TextBox { Name = "ve-gen-seed", Text = _veGenSeed, Width = 130, FontSize = 12 };
        seed.TextChanged += (_, _) => _veGenSeed = seed.Text ?? "";
        var dice = Act("ve-gen-dice", "🎲", () => { _veGenSeed = GenVideo.NewSeed().ToString(); seed.Text = _veGenSeed; });
        var secs = new NumericUpDown { Name = "ve-gen-seconds", Minimum = 0.5m, Maximum = 5, Increment = 0.5m, Value = (decimal)_veGenSeconds, Width = 110, FormatString = "0.0" };
        secs.ValueChanged += (_, _) => _veGenSeconds = (double)(secs.Value ?? 2);
        var qual = new ComboBox { Name = "ve-gen-quality", ItemsSource = GenVideo.Qualities.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, GenVideo.Qualities.ToList().FindIndex(x => x.Id == _veGenQuality)) };
        qual.SelectionChanged += (_, _) => _veGenQuality = GenVideo.Qualities[Math.Max(0, qual.SelectedIndex)].Id;
        sp.Children.Add(prompt);
        sp.Children.Add(new WrapPanel { Children = { P("Zufallswert", Muted, 11), seed, dice, new Border { Width = 8 }, P("Sekunden", Muted, 11), secs, new Border { Width = 8 }, qual } });
        var go = Act("ve-gen-run", _veGenBusy ? "KI-Video entsteht …" : "KI-Clip erzeugen und einfügen", () =>
        {
            if (_veGenBusy) return;
            if (!long.TryParse(_veGenSeed.Trim(), out var s)) { _progMsg[VK] = "Der Zufallswert muss eine ganze Zahl sein."; Show(VK); return; }
            _ = EditGenVideo(d, _veGenPrompt, s, _veGenSeconds, GenVideo.Qualities.First(x => x.Id == _veGenQuality));
        });
        go.IsEnabled = !_veGenBusy;
        sp.Children.Add(go);
        if (EditGenLastClip is { } c && File.Exists(c))
        {
            var v = GenVideo.VerifyProvenance(c);
            sp.Children.Add(P((v is null ? "✓ Nachweis passt: " : "⚠ " + v + " ") + Path.GetFileName(GenVideo.ProvenancePath(c)), v is null ? Text : Brush.Parse("#FACC15"), 11));
        }
        sp.Children.Add(P($"{m.Name} · {m.License} · gleicher Zufallswert und gleiche Beschreibung ergeben denselben Clip. Jeder Clip ist als KI-erzeugt gekennzeichnet.", Muted, 10.5));
        return sp;
    }
}
