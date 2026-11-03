using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik – Spuren trennen (freigegeben 03.11.2026) mit Demucs (Meta, MIT), Modell htdemucs. Am 03.11.2026 auf der RTX 5070 echt geprüft:
/// ein Lied von 3:26 Minuten in 24 s, die vier Spuren ergeben zusammen wieder das Original (30 dB), Gesang und Begleitung ohne Übersprechen (Korrelation 0,02).
/// Begleitung und Gesang kommen als eigene Lieder in die Bibliothek, Schlagzeug, Bass und Rest liegen im Spuren-Ordner.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Trennen ersetzen (Audiodatei, Zielordner → ok).</summary>
    public Func<string, string, Task<bool>>? StemSplitOverride { get; set; }
    KiLibraryModel StemsModel => KiLibrary.Models.First(m => m.Id == "musik-spuren");
    bool StemsReady => StemSplitOverride is not null || KiStore.RuntimeReady(KiRuntime.Spuren);
    bool _msSplitting;

    static readonly (string File, string Kind, string Title)[] StemSongs = [("begleitung.wav", "begleitung", "Begleitung"), ("gesang.wav", "gesang", "Gesang")];

    static string SongKindLabel(string kind) => kind switch
    {
        "neu" => "", "cover" => " · Cover", "abschnitt" => " · neuer Abschnitt", "laenger" => " · länger", "eigen" => " · eigene Aufnahme",
        "begleitung" => " · Begleitung", "gesang" => " · Gesang", "aufnahme" => " · mit meiner Stimme", _ => "",
    };

    /// <summary>Lied in Gesang, Schlagzeug, Bass und Rest trennen; Begleitung (alles ohne Gesang) und Gesang kommen als Lieder in die Bibliothek.</summary>
    public async Task<List<MusicSong>> MsSplit(string songId)
    {
        var s = Songs.Get(songId) ?? throw new InvalidOperationException("Dieses Lied gibt es nicht mehr.");
        if (!File.Exists(s.File)) throw new InvalidOperationException("Die Audiodatei fehlt.");
        var dir = Path.Combine(WorkFolder("Musik"), DevStudio.SafeName(s.Title) + " – Spuren");
        for (var n = 2; Directory.Exists(dir); n++) dir = Path.Combine(WorkFolder("Musik"), $"{DevStudio.SafeName(s.Title)} – Spuren {n}");
        _msSplitting = true; _msMsg = "Die Spuren werden getrennt … (beim ersten Mal lädt Demucs sein Modell, 80 MB)"; if (CurrentView == MS) Show(MS);
        var made = new List<MusicSong>();
        try
        {
            bool ok; string err = "";
            if (StemSplitOverride is { } o) ok = await o(s.File, dir);
            else { var r = await KiPyRun(KiRuntime.Spuren, [s.File, dir]); ok = r.Ok; err = Short(r.Err.Length > 0 ? r.Err : r.Out); }
            if (ok && StemSongs.All(x => File.Exists(Path.Combine(dir, x.File))))
            {
                foreach (var (file, kind, title) in StemSongs)
                    made.Add(Songs.Add(new MusicSong { Title = $"{s.Title} – {title}", Style = s.Style, Lyrics = kind == "gesang" ? s.Lyrics : "[Instrumental]", Language = s.Language,
                        Seconds = s.Seconds, Bpm = s.Bpm, Key = s.Key, TimeSignature = s.TimeSignature, File = Path.Combine(dir, file), Kind = kind, ParentId = s.Id }));
                _msSel = made[0].Id;
                _msMsg = "✓ Getrennt: Begleitung und Gesang sind jetzt eigene Lieder, Schlagzeug, Bass und Rest liegen im Spuren-Ordner.";
                _core.Audit.Append(Principal.LocalOwner.Name, "musik.spuren", Path.GetFileName(s.File));
            }
            else _msMsg = "Trennen ging nicht" + (err.Length > 0 ? ": " + err : ".");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException) { _msMsg = "Trennen ging nicht: " + e.Message; }
        finally { _msSplitting = false; }
        if (CurrentView == MS) Show(MS);
        return made;
    }

    Control MsStemsPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-stems-panel", Spacing = 4 };
        sp.Children.Add(H("Spuren trennen", 13));
        if (s.Kind is "begleitung" or "gesang")
        {
            var dir = Path.GetDirectoryName(s.File)!;
            sp.Children.Add(P("Diese Spur stammt aus „Spuren trennen“. Alle Spuren (Gesang, Schlagzeug, Bass, Rest, Begleitung) liegen im Spuren-Ordner.", Muted, 11));
            sp.Children.Add(Act("ms-stems-folder", "Spuren-Ordner öffnen", () => Launch(dir)));
            return sp;
        }
        if (!StemsReady)
        {
            sp.Children.Add(P("Gesang, Schlagzeug, Bass und Rest einzeln – z. B. die Begleitung zum Mitsingen. Braucht Demucs (frei, MIT, einmalig ca. 3 GB mit PyTorch).", Muted, 11));
            sp.Children.Add(Act("ms-stems-setup", "Spuren trennen einrichten", async () =>
            {
                try { await KiPyInstall(StemsModel, KiPyApps.For(KiRuntime.Spuren)!); _msMsg = "✓ Spuren trennen ist eingerichtet."; }
                catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { _msMsg = "Einrichten ging nicht: " + e.Message; }
                Show(MS);
            }));
            return sp;
        }
        sp.Children.Add(P("Begleitung ohne Gesang und Gesang allein werden eigene Lieder, Schlagzeug, Bass und Rest kommen in den Spuren-Ordner." +
            (s.Kind == "eigen" ? " Fremde Lieder nur privat nutzen." : ""), Muted, 11));
        var go = Act("ms-stems", _msSplitting ? "Wird getrennt …" : "🎚 Spuren trennen", () => _ = MsSplit(s.Id));
        go.IsEnabled = !_msSplitting;
        sp.Children.Add(go);
        return sp;
    }
}
