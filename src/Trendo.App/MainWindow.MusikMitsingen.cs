using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik S5/S6 (freigegeben 03.11.2026): Mitsingen über die Begleitung (aus „Spuren trennen“) mit Treffer-Anzeige in Echtzeit,
/// die Aufnahme danach über die Begleitung mischen (Versatz, Lautstärke, Hall) und mastern – ein neues Lied „– mit meiner Stimme“.</summary>
public partial class MainWindow
{
    /// <summary>Tests: Mikrofon-Strom ersetzen (Blöcke 16 kHz mono an den Rückruf; gibt die Aufnahme zurück, wenn abgebrochen wird).</summary>
    public Func<Action<float[]>, CancellationToken, Task<string?>>? MicStreamOverride { get; set; }
    /// <summary>Tests: Zielmelodie ersetzen (Gesangsspur → JSON).</summary>
    public Func<string, Task<string?>>? MelodyOverride { get; set; }

    CancellationTokenSource? _singCts; SingAlong.Take? _singTake; string? _singSongId; TextBlock? _singLive;
    string? _takeFile; string? _takeFor; int _mixOffset = 120; double _mixVol; string _mixReverb = "raum";

    (MusicSong? Acc, MusicSong? Vocal) StemsOf(MusicSong s)
    {
        var all = Songs.All(); var root = s.Kind is "begleitung" or "gesang" && s.ParentId is { } pid ? pid : s.Id;
        return (all.FirstOrDefault(x => x.ParentId == root && x.Kind == "begleitung"), all.FirstOrDefault(x => x.ParentId == root && x.Kind == "gesang"));
    }

    async Task<SingAlong.Melody?> MelodyOf(MusicSong vocal)
    {
        var cache = vocal.File + ".melodie.json";
        string? json = File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null;
        if (json is null)
        {
            if (MelodyOverride is { } o) json = await o(vocal.File);
            else if (AnalyseReady)
            {
                var r = await KiPyRun(KiRuntime.MusikAnalyse, ["--melodie", "--gesangsspur", vocal.File, cache]);
                json = r.Ok && File.Exists(cache) ? await File.ReadAllTextAsync(cache) : null;
            }
            if (json is not null && MelodyOverride is not null) await File.WriteAllTextAsync(cache, json); // echtes Skript schreibt den Zwischenspeicher selbst
        }
        return json is null ? null : SingAlong.Melody.Parse(json);
    }

    /// <summary>S5: Mitsingen starten – Begleitung läuft, das Mikrofon wird aufgenommen und live mit der Gesangsmelodie verglichen.</summary>
    public async Task<bool> MsSingStart(string songId)
    {
        if (_singCts is not null || Songs.Get(songId) is not { } s) return false;
        var (acc, vocal) = StemsOf(s);
        if (acc is null || vocal is null) { _msMsg = "Zum Mitsingen zuerst „Spuren trennen“ – dann singst du über die Begleitung ohne den Originalgesang."; Show(MS); return false; }
        if (MelodyOverride is null && !AnalyseReady && !File.Exists(vocal.File + ".melodie.json")) { _msMsg = "Zum Mitsingen braucht Trendo die Song-Analyse (rechts beim Lied einrichten, einmalig ca. 300 MB)."; Show(MS); return false; }
        _msMsg = "Die Melodie wird vorbereitet …"; Show(MS);
        var mel = await MelodyOf(vocal);
        if (mel is null) { _msMsg = "Die Gesangsmelodie ließ sich nicht erkennen."; Show(MS); return false; }
        if (MicStreamOverride is null && (_opt.Isolated || FfmpegPath is null)) { _msMsg = "Mitsingen braucht ein Mikrofon und FFmpeg (unter „Programme“ einrichten)."; Show(MS); return false; }
        var dir = Path.Combine(WorkFolder("Musik"), DevStudio.SafeName(s.Title) + " – Aufnahmen"); Directory.CreateDirectory(dir);
        var take = Path.Combine(dir, $"Aufnahme {DateTime.Now:yyyy-MM-dd HH-mm-ss}.wav");
        _singTake = new SingAlong.Take(mel); _singSongId = songId; _singCts = new CancellationTokenSource(); _takeFile = null;
        _msMsg = "Sing mit! Am besten mit Kopfhörern, damit die Begleitung nicht mit aufgenommen wird."; Show(MS);
        MusicPlay([acc.File], 0, MS);
        var ct = _singCts.Token;
        void OnBlock(float[] block)
        {
            var hz = SingAlong.Yin(block, 16000);
            var v = _singTake!.Add(_mPos, hz);
            var last = _singTake.Last;
            _ui.Post(() =>
            {
                if (_singLive is null) return;
                _singLive.Text = (last.Target is double t ? $"Ziel {SingAlong.NoteName(t)}" : "Pause") + (last.SungMidi is double m ? $" · du {SingAlong.NoteName(m)}" : "") +
                    v switch { SingAlong.Verdict.Treffer => "  Treffer", SingAlong.Verdict.Hoeher => "  etwas höher", SingAlong.Verdict.Tiefer => "  etwas tiefer", _ => "" } +
                    $"   ·   {_singTake.HitPercent:0} % getroffen";
                _singLive.Foreground = v switch { SingAlong.Verdict.Treffer => Brush.Parse("#4ADE80"), SingAlong.Verdict.Hoeher or SingAlong.Verdict.Tiefer => Brush.Parse("#FACC15"), _ => Muted };
                if (double.IsFinite(_mDur) && _mDur > 1 && _mPos >= _mDur - 0.3) MsSingStop(); // Lied zu Ende
            });
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.mitsingen.start", songId);
        string? file = MicStreamOverride is { } o ? await o(OnBlock, ct) : await MicStream(take, OnBlock, ct);
        _audio?.Stop(); _mPaused = true;
        _singCts.Dispose(); _singCts = null;
        if (file is not null && File.Exists(file)) { _takeFile = file; _takeFor = songId; }
        _msMsg = _takeFile is null ? "Die Aufnahme ging nicht." : $"Aufnahme fertig – {_singTake.HitPercent:0} % der gesungenen Töne getroffen. Unten mischen, dann wird ein neues Lied daraus.";
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.mitsingen.ende", $"{_singTake.HitPercent:0}%");
        Show(MS);
        return _takeFile is not null;
    }

    public void MsSingStop() => _singCts?.Cancel();

    /// <summary>FFmpeg: Mikrofon gleichzeitig als WAV (44,1 kHz) speichern und als Rohstrom (16 kHz) für die Tonhöhe lesen.</summary>
    async Task<string?> MicStream(string take, Action<float[]> onBlock, CancellationToken ct)
    {
        if (FfmpegPath is not { } ff || DesktopCapture.Devices(ff).Microphones.FirstOrDefault() is not { } mic) return null;
        var psi = new ProcessStartInfo(ff) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-hide_banner", "-nostats", "-y", "-f", "dshow", "-audio_buffer_size", "40", "-i", "audio=" + mic.Id,
                     "-map", "0:a", "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", take, "-map", "0:a", "-ac", "1", "-ar", "16000", "-f", "s16le", "pipe:1" }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        if (p is null) return null;
        p.ErrorDataReceived += (_, _) => { }; p.BeginErrorReadLine();
        var reader = Task.Run(async () =>
        {
            var buf = new byte[2048 * 2]; var block = new float[2048]; var fill = 0;
            var s = p.StandardOutput.BaseStream;
            while (true)
            {
                int n; try { n = await s.ReadAsync(buf.AsMemory(fill * 2, buf.Length - fill * 2)); } catch (IOException) { break; }
                if (n <= 0) break;
                fill += n / 2;
                if (fill == block.Length)
                {
                    for (var i = 0; i < block.Length; i++) block[i] = BitConverter.ToInt16(buf, i * 2) / 32768f;
                    onBlock((float[])block.Clone()); fill = 0;
                }
            }
        });
        try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { }
        try { p.StandardInput.Write('q'); p.StandardInput.Flush(); await p.WaitForExitAsync(new CancellationTokenSource(5000).Token); }
        catch (Exception e) when (e is IOException or InvalidOperationException or OperationCanceledException) { try { p.Kill(); } catch (InvalidOperationException) { } }
        await reader;
        return File.Exists(take) ? take : null;
    }

    /// <summary>S6: Aufnahme über die Begleitung mischen und mastern → neues Lied „– mit meiner Stimme“.</summary>
    public async Task<MusicSong?> MsMixTake()
    {
        if (_takeFile is null || _takeFor is null || Songs.Get(_takeFor) is not { } s || StemsOf(s).Acc is not { } acc) return null;
        var ff = await MasterFfmpeg();
        if (ff is null) { _msMsg = "Zum Mischen braucht Trendo FFmpeg – unter „Programme“ einrichten."; Show(MS); return null; }
        var root = s.Kind is "begleitung" or "gesang" && s.ParentId is { } pid && Songs.Get(pid) is { } orig ? orig : s;
        var outFile = Path.Combine(Path.GetDirectoryName(_takeFile)!, DevStudio.SafeName(root.Title) + $" – mit meiner Stimme {DateTime.Now:HH-mm-ss}.wav");
        _msMsg = "Wird gemischt …"; Show(MS);
        var r = await MasterRun(ff, SingAlong.MixArgs(acc.File, _takeFile, outFile, _mixOffset, _mixVol, _mixReverb));
        if (!r.Ok || !File.Exists(outFile)) { _msMsg = "Mischen ging nicht: " + Short(r.Err); Show(MS); return null; }
        var song = Songs.Add(new MusicSong { Title = root.Title + " – mit meiner Stimme", Style = root.Style, Lyrics = root.Lyrics, Language = root.Language, Seconds = acc.Seconds,
            Bpm = root.Bpm, Key = root.Key, TimeSignature = root.TimeSignature, File = outFile, Kind = "aufnahme", ParentId = root.Id });
        await MsMaster(song.Id, quiet: true);
        _msSel = song.Id; _msMsg = "Gemischt und gemastert – dein Lied mit deiner Stimme ist in der Bibliothek.";
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.mischen", song.Id);
        Show(MS);
        return Songs.Get(song.Id);
    }

    Control MsSingPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-sing-panel", Spacing = 4 };
        sp.Children.Add(H("Mitsingen & aufnehmen", 13));
        var (acc, _) = StemsOf(s);
        if (acc is null) { sp.Children.Add(P("Erst „Spuren trennen“ – dann singst du über die Begleitung und Trendo zeigt, ob du die Töne triffst.", Muted, 11)); return sp; }
        if (_singCts is not null && _singSongId == s.Id)
        {
            _singLive = new TextBlock { Name = "ms-sing-live", Text = "Hör zu und sing mit …", Foreground = Muted, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(_singLive);
            sp.Children.Add(Act("ms-sing-stop", "Aufnahme beenden", MsSingStop));
            return sp;
        }
        sp.Children.Add(P("Die Begleitung läuft, du singst – Trendo zeigt live, ob du höher oder tiefer singen solltest. Am besten mit Kopfhörern.", Muted, 11));
        sp.Children.Add(Act("ms-sing", "Mitsingen starten", () => _ = MsSingStart(s.Id)));
        if (_takeFile is not null && _takeFor == s.Id)
        {
            var off = new NumericUpDown { Name = "ms-mix-offset", Minimum = -400, Maximum = 400, Increment = 10, Value = _mixOffset, FormatString = "0", Width = 110 };
            off.ValueChanged += (_, _) => _mixOffset = (int)(off.Value ?? 0);
            ToolTip.SetTip(off, "Ausgleich der Verzögerung von Kopfhörer/Mikrofon in Millisekunden (meist 80–150)");
            var vol = new Slider { Name = "ms-mix-vol", Minimum = -12, Maximum = 6, Value = _mixVol, Width = 140 };
            vol.ValueChanged += (_, _) => _mixVol = vol.Value;
            var rev = new ComboBox { Name = "ms-mix-reverb", ItemsSource = new[] { "Hall: aus", "Hall: Raum", "Hall: Halle" }, SelectedIndex = _mixReverb switch { "aus" => 0, "halle" => 2, _ => 1 } };
            rev.SelectionChanged += (_, _) => _mixReverb = rev.SelectedIndex switch { 0 => "aus", 2 => "halle", _ => "raum" };
            sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Versatz ms", Foreground = Muted, FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, off,
                new TextBlock { Text = "Stimme", Foreground = Muted, FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0) }, vol, rev } });
            sp.Children.Add(new WrapPanel { Children = { Act("ms-take-play", "Aufnahme anhören", () => MusicPlay([_takeFile], 0, MS)), new Border { Width = 6 }, Act("ms-mix", "Mischen & mastern", () => _ = MsMixTake()) } });
        }
        return sp;
    }
}
