using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik M10 (freigegeben 03.11.2026): Lied verlängern mit dem Grundmodell von ACE-Step. Am 03.11.2026 auf der RTX 5070 echt geprüft:
/// 30 → 60 s in 9 s, der Anfang bleibt unverändert (Ähnlichkeit 1,0), der neue Teil schließt an. Spuren trennen und „Begleitung dazu“ lieferten dabei
/// nachgemessen keine getrennten Spuren – darum nicht eingebaut. Turbo- und Grundmodell passen nicht zugleich in 12 GB: Trendo lädt jeweils eins.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Grundmodell gilt als vorhanden.</summary>
    public bool? AceBaseReadyOverride { get; set; }
    bool AceBaseReady => AceBaseReadyOverride ?? File.Exists(Path.Combine(KiStore.RuntimeDir(KiRuntime.AceStep), "checkpoints", AceStepClient.BaseModel, "model.safetensors"));
    int _msExtendBy = 60;

    /// <summary>Lied um <paramref name="seconds"/> verlängern: neue Gesamtlänge, alles nach dem bisherigen Ende wird neu erzeugt, der Anfang bleibt.</summary>
    public Task<List<MusicSong>> MsExtend(string songId, int seconds)
    {
        var s = Songs.Get(songId) ?? throw new InvalidOperationException("Dieses Lied gibt es nicht mehr.");
        var len = (int)Math.Round(s.Seconds > 0 ? s.Seconds : s.Analysis?.Duration ?? 0);
        if (len < 10) throw new ArgumentException("Die Länge des Lieds ist unbekannt – zuerst analysieren.");
        var total = Math.Min(600, len + seconds);
        if (total <= len) throw new ArgumentException("Länger als 10 Minuten geht nicht.");
        // der neue Teil braucht Text: den Refrain noch einmal anhängen (bei Instrumental bleibt es instrumental)
        var lyr = s.Lyrics;
        try { if (lyr.Trim().Length > 0 && lyr != "[Instrumental]") lyr = SongTools.RepeatChorus(lyr); } catch (ArgumentException) { }
        return MusicStudioCreate(new AceStepClient.Job(s.Style.Length > 0 ? s.Style : "music", lyr, total, s.Language, 1, s.Bpm, s.Key, s.TimeSignature,
            Task: "repaint", RepaintStart: len, RepaintEnd: total), s.Title, s.Id, AceStepClient.BaseModel);
    }

    /// <summary>Grundmodell einmalig laden (ca. 4,5 GB, MIT) – mit dem Download-Werkzeug von ACE-Step selbst.</summary>
    async Task MsLoadBaseModel()
    {
        var dir = KiStore.RuntimeDir(KiRuntime.AceStep);
        var py = Path.Combine(dir, "python_embeded", "python.exe");
        if (KiStepOverride is null && !File.Exists(py)) { _msMsg = "Zuerst ACE-Step in der KI-Bibliothek unter „Musik“ einrichten."; Show(MS); return; }
        _msMsg = "Das Grundmodell wird geladen (ca. 4,5 GB, einmalig) …"; Show(MS);
        var code = "import sys; sys.path.insert(0, '.'); from acestep.model_downloader import download_submodel, get_checkpoints_dir; " +
                   "ok, msg = download_submodel('" + AceStepClient.BaseModel + "', get_checkpoints_dir()); print(msg); sys.exit(0 if ok else 1)";
        var r = await KiStep(py, ["-c", code], dir, AceStepClient.ServerEnvironment(), 3_600_000);
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.grundmodell", $"exit={r.Exit}");
        _msMsg = r.Ok ? "✓ Grundmodell geladen – jetzt „Verlängern“." : "Laden ging nicht: " + Short(r.Err.Length > 0 ? r.Err : r.Out);
        Show(MS);
    }

    Control MsExtendPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-extend-panel", Spacing = 4 };
        sp.Children.Add(H("Verlängern", 13));
        if (!AceBaseReady)
        {
            sp.Children.Add(P("Braucht das Grundmodell von ACE-Step (einmalig ca. 4,5 GB). Spuren trennen bietet Trendo nicht an – nachgemessen lieferte ACE-Step dabei keine sauber getrennten Spuren.", Muted, 11));
            sp.Children.Add(Act("ms-extend-setup", "Grundmodell laden", () => _ = MsLoadBaseModel()));
            return sp;
        }
        var by = new ComboBox { Name = "ms-extend-by", Width = 140, ItemsSource = new[] { "um 30 Sekunden", "um 1 Minute", "um 2 Minuten" }, SelectedIndex = _msExtendBy switch { 30 => 0, 120 => 2, _ => 1 } };
        by.SelectionChanged += (_, _) => _msExtendBy = by.SelectedIndex switch { 0 => 30, 2 => 120, _ => 60 };
        var go = Act("ms-extend", "Verlängern", () =>
        {
            try { _ = MsExtend(s.Id, _msExtendBy); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { _msMsg = e.Message; Show(MS); }
        });
        sp.Children.Add(P("Der Anfang bleibt, Trendo setzt das Lied fort. Dafür lädt die Musik-KI kurz das Grundmodell (ca. 1–2 Minuten), danach beim nächsten neuen Lied wieder das schnelle.", Muted, 11));
        sp.Children.Add(new WrapPanel { Children = { by, new Border { Width = 6 }, go } });
        return sp;
    }
}
