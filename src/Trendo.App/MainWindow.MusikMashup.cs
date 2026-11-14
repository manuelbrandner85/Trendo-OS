using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Trendo Musik V10 wie Suno (07.11.2026): Mashup (Gesang A über Begleitung B, Tempo und Tonart angeglichen) und Sample (Ausschnitt als Klangvorbild für ein neues Lied).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: FFmpeg-Läufe beim Mashup ersetzen.</summary>
    public Func<IReadOnlyList<string>, Cli.Result>? MsMashupRunOverride { get; set; }
    string? _msMashWith; double _msSampleFrom, _msSampleTo = 8; string _msSamplePrompt = ""; bool _msSampleInstr = true; int _msSampleLen = 60;

    Cli.Result MashRun(IReadOnlyList<string> args) => MsMashupRunOverride is { } o ? o(args) : FfmpegPath is { } ff ? Cli.Run(ff, args, WorkFolder("Musik"), 900_000) : new Cli.Result(1, "", "FFmpeg fehlt – unter Zusatzprogramme einrichten.");

    (double Bpm, string? Key) TempoKeyOf(MusicSong s)
    {
        var bpm = (double?)s.Bpm ?? s.Analysis?.Bpm ?? 0; var key = s.Key ?? (s.Analysis?.Key is { Length: > 0 } k ? k : null);
        if (bpm <= 0 && File.Exists(s.File)) bpm = BeatDetect.Find(AudioFile.Read(s.File)).Bpm;
        return (bpm, key);
    }

    /// <summary>Mashup: Gesang aus <paramref name="vocalSongId"/> über die Begleitung von <paramref name="backingSongId"/>. Fehlende Spuren werden erst getrennt.</summary>
    public async Task<MusicSong?> MsMashup(string vocalSongId, string backingSongId)
    {
        if (vocalSongId == backingSongId) { _msMsg = "Für ein Mashup zwei verschiedene Lieder wählen."; Show(MS); return null; }
        var a = Songs.Get(vocalSongId) ?? throw new InvalidOperationException("Lied A gibt es nicht mehr.");
        var b = Songs.Get(backingSongId) ?? throw new InvalidOperationException("Lied B gibt es nicht mehr.");
        if (StemsOf(a).Vocal is null) await MsSplit(a.Id);
        if (StemsOf(b).Acc is null) await MsSplit(b.Id);
        var voc = StemsOf(a).Vocal; var acc = StemsOf(b).Acc;
        if (voc is null || acc is null) { _msMsg = "Für ein Mashup müssen beide Lieder in Spuren getrennt sein – „Spuren trennen“ einrichten."; Show(MS); return null; }
        MusicMashup.Plan plan;
        try { var (ba, ka) = TempoKeyOf(a); var (bb, kb) = TempoKeyOf(b); plan = MusicMashup.Match(ba, ka, bb, kb); }
        catch (ArgumentException e) { _msMsg = e.Message; Show(MS); return null; }
        if (plan.Light == "rot") { _msMsg = "Mashup abgelehnt: " + plan.Text; Show(MS); return null; }
        _msMsg = "Mashup entsteht … " + plan.Text; Show(MS);
        var dir = WorkFolder("Musik"); var title = $"{a.Title} × {b.Title}";
        var tmp = Path.Combine(dir, $".mashup-{Guid.NewGuid():N}.wav");
        var outFile = Path.Combine(dir, DevStudio.SafeName(title) + " (Mashup).wav"); for (var n = 2; File.Exists(outFile); n++) outFile = Path.Combine(dir, DevStudio.SafeName(title) + $" (Mashup {n}).wav");
        try
        {
            var vocalIn = voc.File;
            if (Math.Abs(plan.Tempo - 1) > 0.002 || plan.Semitones != 0)
            {
                var r1 = await Task.Run(() => MashRun(TimePitch.Args(voc.File, tmp, plan.Tempo, plan.Semitones)));
                if (!r1.Ok || !File.Exists(tmp)) { _msMsg = "Angleichen ging nicht: " + (r1.Err.Trim().Split('\n').LastOrDefault() ?? ""); return null; }
                vocalIn = tmp;
            }
            var r2 = await Task.Run(() => MashRun(MusicMashup.MixArgs(vocalIn, acc.File, outFile)));
            if (!r2.Ok || !File.Exists(outFile)) { _msMsg = "Mischen ging nicht: " + (r2.Err.Trim().Split('\n').LastOrDefault() ?? ""); return null; }
            var m = Songs.Add(new MusicSong { Title = title, Style = $"Mashup: Gesang aus „{a.Title}“, Musik aus „{b.Title}“ · {plan.Text.Split(" – ")[0]}", Lyrics = a.Lyrics, Language = a.Language,
                Seconds = acc.Seconds, Bpm = b.Bpm, Key = b.Key, TimeSignature = b.TimeSignature, File = outFile, Kind = "mashup", ParentId = b.Id });
            _msSel = m.Id; _msMsg = $"✓ Mashup fertig – {plan.Text}";
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.mashup", $"{a.Id}+{b.Id} t={plan.Tempo} s={plan.Semitones}");
            return m;
        }
        finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { } Show(MS); }
    }

    /// <summary>Sample: Ausschnitt als Klangvorbild – ein neues Lied entsteht mit diesem Klang (Beschreibung, Länge, mit Gesang oder instrumental).</summary>
    public async Task<MusicSong?> MsSampleCreate(string songId, double from, double to, string prompt, int seconds, bool instrumental)
    {
        var s = Songs.Get(songId) ?? throw new InvalidOperationException("Dieses Lied gibt es nicht mehr.");
        if (MusicMashup.SampleProblem(from, to, s.Seconds) is { } p) { _msMsg = p; Show(MS); return null; }
        if (prompt.Trim().Length < 3) { _msMsg = "Beschreibe, was aus dem Sample werden soll – z. B. „Hip-Hop-Beat mit diesem Klavier“."; Show(MS); return null; }
        var sample = Path.Combine(WorkFolder("Musik"), ".roh", $"sample-{Guid.NewGuid():N}.wav"); Directory.CreateDirectory(Path.GetDirectoryName(sample)!);
        var r = await Task.Run(() => MashRun(CropArgs(s.File, sample, from, to, s.Seconds, keep: true)));
        if (!r.Ok || !File.Exists(sample)) { _msMsg = "Ausschnitt ging nicht: " + (r.Err.Trim().Split('\n').LastOrDefault() ?? ""); Show(MS); return null; }
        var lang = AceStepClient.Languages[Math.Clamp(_msLang, 0, AceStepClient.Languages.Length - 1)].Code;
        var job = instrumental ? new AceStepClient.Job(prompt.Trim(), "[Instrumental]", Math.Clamp(seconds, 10, 600), lang, 1, ReferenceAudio: sample)
            : new AceStepClient.Job(prompt.Trim(), "", Math.Clamp(seconds, 10, 600), lang, 1, SampleQuery: $"{prompt.Trim()}, sung in {(lang == "de" ? "German" : "English")}", ReferenceAudio: sample);
        var made = await MusicStudioCreate(job, (prompt.Trim().Length > 30 ? prompt.Trim()[..30] : prompt.Trim()) + " (Sample)");
        if (made.FirstOrDefault() is not { } m) return null;
        Songs.Update(m.Id, x => { x.Kind = "sample"; x.ParentId = s.Id; x.Style = prompt.Trim() + $" · Sample aus „{s.Title}“ {from:0.0}–{to:0.0} s"; });
        _msMsg = $"✓ Neues Lied aus dem Sample ({from:0.0}–{to:0.0} s von „{s.Title}“)."; Show(MS);
        return Songs.Get(m.Id);
    }

    /// <summary>Felder „Mashup“ und „Als Sample“ im Reiter Bearbeiten.</summary>
    Control MsMashupSamplePanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-mashup", Spacing = 4 };
        var others = Songs.Visible().Where(x => x.Id != s.Id && x.Kind is not ("gesang" or "begleitung" or "sound") && !x.Kind.StartsWith("spur-") && !x.Kind.StartsWith("ohne-")).ToList();
        sp.Children.Add(H("Mashup", 13));
        sp.Children.Add(P("Gesang dieses Lieds über die Musik eines anderen – Tempo und Tonart werden angeglichen (zu starke Änderung wird abgelehnt).", Muted, 11.5));
        var pick = new ComboBox { Name = "ms-mashup-with", HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12, ItemsSource = others.Select(x => "Musik aus: " + x.Title).ToList(), SelectedIndex = Math.Max(-1, others.FindIndex(x => x.Id == _msMashWith)) };
        pick.SelectionChanged += (_, _) => _msMashWith = pick.SelectedIndex >= 0 ? others[pick.SelectedIndex].Id : null;
        sp.Children.Add(pick);
        sp.Children.Add(Act("ms-mashup-go", "Mashup erstellen", () => { if (_msMashWith is { } w2) _ = MsMashup(s.Id, w2); else { _msMsg = "Erst das zweite Lied wählen."; Show(MS); } }));
        sp.Children.Add(H("Als Sample verwenden", 13));
        var from = new NumericUpDown { Name = "ms-sample-from", Minimum = 0, Maximum = (decimal)Math.Max(1, s.Seconds), Value = (decimal)Math.Min(_msSampleFrom, s.Seconds), Increment = 1, FormatString = "0.0", Width = 96 };
        var to = new NumericUpDown { Name = "ms-sample-to", Minimum = 0, Maximum = (decimal)Math.Max(1, s.Seconds), Value = (decimal)Math.Min(_msSampleTo, s.Seconds), Increment = 1, FormatString = "0.0", Width = 96 };
        var desc = new TextBox { Name = "ms-sample-prompt", Text = _msSamplePrompt, Watermark = "Was daraus werden soll, z. B. „Lo-Fi-Beat mit diesem Klavier“", FontSize = 12 };
        desc.TextChanged += (_, _) => _msSamplePrompt = desc.Text ?? "";
        var instr = new CheckBox { Name = "ms-sample-instr", Content = "Instrumental", IsChecked = _msSampleInstr, Foreground = Text, FontSize = 12 };
        instr.IsCheckedChanged += (_, _) => _msSampleInstr = instr.IsChecked == true;
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "von", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, from,
            new TextBlock { Text = "bis", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) }, to, new TextBlock { Text = "s", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) } } });
        sp.Children.Add(desc); sp.Children.Add(instr);
        sp.Children.Add(Act("ms-sample-go", "Neues Lied aus dem Sample", () =>
        {
            _msSampleFrom = (double)(from.Value ?? 0); _msSampleTo = (double)(to.Value ?? 0); _msSamplePrompt = desc.Text ?? "";
            _ = MsSampleCreate(s.Id, _msSampleFrom, _msSampleTo, _msSamplePrompt, _msSampleLen, _msSampleInstr);
        }));
        return sp;
    }
}
