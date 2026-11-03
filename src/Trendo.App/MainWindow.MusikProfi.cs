using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik – hochprofessionell statt „nach KI“ (Wunsch 03.11.2026): jedes neue Lied wird gemastert (−14 LUFS, −1 dBTP, siehe <see cref="SongMaster"/>),
/// Liedtexte bekommen Songwriter-Regeln und werden auf KI-Floskeln geprüft (<see cref="LyricsCraft"/>); gefundene Floskeln ersetzt die Text-KI einmal gezielt.</summary>
public partial class MainWindow
{
    /// <summary>Tests: FFmpeg-Läufe ersetzen (Argumente → Ergebnis); der zweite Lauf muss die Zieldatei schreiben.</summary>
    public Func<string[], Task<Cli.Result>>? MasterRunOverride { get; set; }

    async Task<string?> MasterFfmpeg()
    {
        if (MasterRunOverride is not null) return "ffmpeg";
        if (_opt.Isolated) return null;
        if (_ffmpegResolved is { } c && File.Exists(c)) return c;
        var check = await FfmpegReady.Resolve(_deps);
        return check.Ok && check.Path is { } p && File.Exists(p) ? _ffmpegResolved = p : null;
    }

    Task<Cli.Result> MasterRun(string ffmpeg, string[] args) => MasterRunOverride is { } o ? o(args) : Task.Run(() => Cli.Run(ffmpeg, args, null, 600_000));

    /// <summary>Lied mastern (ersetzt die Datei, wenn beide Läufe klappen). Gibt false zurück, wenn FFmpeg fehlt oder ein Lauf scheitert.</summary>
    public async Task<bool> MsMaster(string songId, bool quiet = false)
    {
        var s = Songs.Get(songId);
        if (s is null || !File.Exists(s.File)) return false;
        var ff = await MasterFfmpeg();
        if (ff is null) { if (!quiet) { _msMsg = "Zum Mastern braucht Trendo FFmpeg – unter „Programme“ einrichten."; Show(MS); } return false; }
        var m = SongMaster.ParseMeasure((await MasterRun(ff, SongMaster.MeasureArgs(s.File))).Err);
        if (m is null) { if (!quiet) { _msMsg = "Mastern ging nicht: die Lautheit ließ sich nicht messen."; Show(MS); } return false; }
        var tmp = Path.Combine(Path.GetDirectoryName(s.File)!, $".trendo-master-{Guid.NewGuid():N}{Path.GetExtension(s.File)}");
        var r = await MasterRun(ff, SongMaster.ApplyArgs(s.File, tmp, m));
        if (!r.Ok || !File.Exists(tmp) || new FileInfo(tmp).Length < 1000)
        {
            TryDelete(tmp);
            if (!quiet) { _msMsg = "Mastern ging nicht: " + Short(r.Err); Show(MS); }
            return false;
        }
        File.Move(tmp, s.File, overwrite: true);
        Songs.Update(songId, x => x.Mastered = true);
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.master", $"{Path.GetFileName(s.File)} von {m.I} LUFS");
        if (!quiet) { _msMsg = $"✓ Gemastert: −14 LUFS, Spitzen höchstens −1 dBTP (vorher {m.I} LUFS)."; Show(MS); }
        return true;
    }

    /// <summary>Text der Text-KI auf KI-Floskeln prüfen und gefundene einmal gezielt ersetzen lassen. Gibt den Text und die übrigen Floskeln zurück.</summary>
    async Task<(string Text, List<string> Left)> MsPolishLyrics(string text)
    {
        var hits = LyricsCraft.FindPhrases(text);
        if (hits.Count == 0) return (text, hits);
        string? better = null;
        var prompt = LyricsCraft.RewritePrompt(text, hits);
        try { better = MusicLyricsOverride is { } o ? await o(prompt) : await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : null; }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException) { better = null; }
        better = better?.Trim().Trim('`').Trim();
        if (better is { Length: > 20 } && better.Contains('[')) { text = better; hits = LyricsCraft.FindPhrases(text); }
        return (text, hits);
    }

    static string FloskelHint(IReadOnlyList<string> left) => left.Count == 0 ? "" : " Noch abgegriffen klingt: " + string.Join(", ", left.Select(h => "„" + h + "“")) + " – am besten selbst umschreiben.";

    /// <summary>Knopf in den Text-Werkzeugen: eingegebenen Text prüfen.</summary>
    void MsCheckFloskeln()
    {
        var hits = LyricsCraft.FindPhrases(_msLyrics);
        _msMsg = _msLyrics.Trim().Length == 0 ? "Noch kein Liedtext da." : hits.Count == 0 ? "✓ Keine typischen KI-Floskeln gefunden." : "KI-Floskeln gefunden:" + FloskelHint(hits).Replace(" Noch abgegriffen klingt:", "");
        Show(MS);
    }
}
