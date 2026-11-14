using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik M6 (freigegeben 04.11.2026): eigene Stimme lernen und Cover darin singen lassen (Applio/RVC, lokal).
/// Feste Regel: Trendo lernt nur aus Aufnahmen, die hier live mit dem Mikrofon entstehen, nach der Bestätigung „Das ist meine eigene Stimme“.
/// Dateien hochladen geht dafür nicht – so lässt sich keine fremde Stimme nachmachen. Alles bleibt auf diesem PC und lässt sich löschen.</summary>
public partial class MainWindow
{
    public const int OwnTakes = 10, OwnTakeSeconds = 30, OwnMinTakes = 6;
    const string OwnVoiceName = "trendo-eigene-stimme";
    bool _ovConsent, _ovBusy; string _ovMsg = "";

    /// <summary>Was in jeder Aufnahme zu tun ist – Singen und Sprechen in verschiedenen Lagen (so lernt die KI den ganzen Umfang).</summary>
    public static readonly string[] OwnTakeHints =
    [
        "Sing langsam von deinem tiefsten zu deinem höchsten Ton auf „a“ und wieder zurück.",
        "Sing den Refrain eines Lieds, das du gut kannst – ganz normal, wie du singst.",
        "Lies laut vor: „Am Morgen geht die Sonne auf, die Stadt erwacht, und ich mache mich auf den Weg.“",
        "Sing leise und weich, fast gehaucht – eine ruhige Melodie.",
        "Sing kräftig und laut – wie auf einer Bühne.",
        "Sing Töne lange aus: „ooo“, „eee“, „aaa“ in verschiedenen Höhen.",
        "Erzähl frei, was du heute gemacht hast.",
        "Sing eine Strophe eines Lieds in deiner bequemen Lage.",
        "Sing kurze, schnelle Silben: „la-la-la, da-da-da“ im Rhythmus.",
        "Sing noch einmal ein Lied deiner Wahl – mit Gefühl.",
    ];

    /// <summary>Tests: Ersatz für Applio (Argumente → Ergebnis) und fürs Mischen (Gesang, Begleitung, Ziel).</summary>
    public Func<string[], Task<Cli.Result>>? OwnVoiceRunOverride { get; set; }
    public Func<string, string, string, Task<bool>>? OwnVoiceMixOverride { get; set; }

    string OwnDir => Path.Combine(_core.DataRoot, "stimme-eigen");
    string OwnTakesDir => Path.Combine(OwnDir, "aufnahmen");
    string OwnReadyFile => Path.Combine(OwnDir, "gelernt.txt");
    public int OwnTakeCount => Directory.Exists(OwnTakesDir) ? Directory.GetFiles(OwnTakesDir, "teil*.wav").Length : 0;
    public bool OwnVoiceReady => File.Exists(OwnReadyFile);
    public bool OwnConsent { get => _ovConsent; set => _ovConsent = value; }
    public string OwnMessage => _ovMsg;

    void OvSay(string t) { _ovMsg = t; if (CurrentView == MS) Show(MS); }

    /// <summary>Eine Aufnahme (30 s) mit dem Mikrofon – nur nach Zustimmung.</summary>
    public async Task<bool> MsOwnVoiceRecord()
    {
        if (!_ovConsent) { OvSay("Bitte zuerst bestätigen: Das ist meine eigene Stimme."); return false; }
        if (_ovBusy) return false;
        var n = OwnTakeCount; if (n >= OwnTakes) { OvSay("Alle Aufnahmen sind da – jetzt „Stimme lernen“."); return false; }
        _ovBusy = true; OvSay($"Aufnahme {n + 1} von {OwnTakes} läuft ({OwnTakeSeconds} s): {OwnTakeHints[n % OwnTakeHints.Length]}");
        try
        {
            var wav = await RecordMic(OwnTakeSeconds);
            if (wav is null) { OvSay(_msMsg.Length > 0 ? _msMsg : "Die Aufnahme ging nicht."); return false; }
            Directory.CreateDirectory(OwnTakesDir);
            File.Move(wav, Path.Combine(OwnTakesDir, $"teil{n:00}.wav"), true);
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.eigen.aufnahme", $"{n + 1}");
            OvSay(n + 1 >= OwnMinTakes ? $"✓ Aufnahme {n + 1} gespeichert – genug zum Lernen (mehr Aufnahmen = natürlicher)." : $"✓ Aufnahme {n + 1} gespeichert – noch {OwnMinTakes - n - 1} bis zum Lernen.");
            return true;
        }
        finally { _ovBusy = false; if (CurrentView == MS) Show(MS); }
    }

    async Task<Cli.Result> OvRun(string[] args, int timeoutMs) => OwnVoiceRunOverride is { } o ? await o(args) : await KiPyRun(KiRuntime.Stimme, args, timeoutMs);

    /// <summary>Stimme lernen (Applio). Mit NVIDIA-Grafikkarte etwa 30–40 Minuten.</summary>
    public async Task<bool> MsOwnVoiceLearn()
    {
        if (!_ovConsent) { OvSay("Bitte zuerst bestätigen: Das ist meine eigene Stimme."); return false; }
        if (OwnTakeCount < OwnMinTakes) { OvSay($"Dafür braucht Trendo mindestens {OwnMinTakes} Aufnahmen (jetzt {OwnTakeCount})."); return false; }
        if (_ovBusy) return false;
        _ovBusy = true;
        try
        {
            if (OwnVoiceRunOverride is null)
            {
                if (_opt.Isolated) { OvSay("Testmodus: Trendo lernt keine Stimme."); return false; }
                if (!KiStore.RuntimeReady(KiRuntime.Stimme))
                {
                    OvSay("„Eigene Stimme“ wird eingerichtet (Applio, frei, MIT – einmalig mehrere GB mit PyTorch) …");
                    await KiPyInstall(KiLibrary.Models.First(m => m.Id == "musik-stimme"), KiPyApps.For(KiRuntime.Stimme)!);
                }
            }
            OvSay("Trendo lernt deine Stimme … (mit NVIDIA-Grafikkarte etwa 30–40 Minuten; du kannst weiterarbeiten)");
            var r = await OvRun(["lernen", OwnVoiceName, OwnTakesDir, "60"], 4 * 3600_000);
            var ok = r.Ok && r.Out.Contains("\"ok\": true");
            if (ok) { Directory.CreateDirectory(OwnDir); File.WriteAllText(OwnReadyFile, DateTime.Now.ToString("O")); }
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.eigen.gelernt", ok ? "ok" : "fehler");
            OvSay(ok ? "✓ Deine Stimme ist gelernt – unter Schritt 4 „Meine eigene Stimme“ wählen." : "Lernen ging nicht: " + Short(r.Err.Length > 0 ? r.Err : r.Out));
            return ok;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { OvSay("Einrichten ging nicht: " + e.Message); return false; }
        finally { _ovBusy = false; if (CurrentView == MS) Show(MS); }
    }

    /// <summary>Cover in der eigenen Stimme: Spuren trennen, Gesang umwandeln, mit der Begleitung mischen → neue Fassung.</summary>
    /// <param name="voice">V4/V6: Stimme aus der Bibliothek (Id) – leer = die eigene Mikrofon-Stimme.</param>
    public async Task<MusicSong?> MsOwnVoiceSing(MusicSong s, int semitones = 0, string? voice = null)
    {
        var bankVoice = voice is null ? null : Bank.Get(voice);
        if (voice is null && !OwnVoiceReady) { OvSay("Zuerst deine Stimme lernen."); return null; }
        if (voice is not null && bankVoice is not { Ready: true }) { OvSay("Diese Stimme ist noch nicht gelernt."); return null; }
        var model = voice ?? OwnVoiceName; var label = bankVoice is null ? "deine Stimme" : "Stimme " + bankVoice.Name;
        OvSay("Gesang und Begleitung werden getrennt …");
        var stems = await MsSplit(s.Id);
        var vocal = stems.FirstOrDefault(x => x.Kind == "gesang"); var acc = stems.FirstOrDefault(x => x.Kind == "begleitung");
        if (vocal is null || acc is null) { OvSay("Trennen ging nicht: " + _msMsg); return null; }
        OvSay($"Der Gesang wird in {(bankVoice is null ? "deiner Stimme" : "der Stimme „" + bankVoice.Name + "“")} gesungen …");
        var dir = Path.GetDirectoryName(s.File)!;
        var voc = Path.Combine(dir, Path.GetFileNameWithoutExtension(s.File) + $" – Gesang ({label}).wav");
        var r = await OvRun(["singen", model, vocal.File, voc, semitones.ToString(System.Globalization.CultureInfo.InvariantCulture)], 1_800_000);
        if (OwnVoiceRunOverride is not null && r.Ok && !File.Exists(voc)) File.WriteAllBytes(voc, [1]);
        if (!r.Ok || !File.Exists(voc)) { OvSay("Umwandeln ging nicht: " + Short(r.Err.Length > 0 ? r.Err : r.Out)); return null; }
        var mix = Path.Combine(dir, Path.GetFileNameWithoutExtension(s.File) + $" ({label}).mp3");
        bool ok;
        if (OwnVoiceMixOverride is { } m) ok = await m(voc, acc.File, mix);
        else
        {
            var ff = FfmpegPath ?? throw new InvalidOperationException("Zum Mischen braucht Trendo FFmpeg.");
            ok = (await Task.Run(() => Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-i", voc, "-i", acc.File,
                "-filter_complex", "[0:a]aresample=44100,volume=1.1[v];[1:a]aresample=44100[b];[v][b]amix=inputs=2:duration=longest:normalize=0,loudnorm=I=-14:TP=-1:LRA=11", "-c:a", "libmp3lame", "-q:a", "2", mix], timeoutMs: 600_000))).Ok;
        }
        if (!ok || !File.Exists(mix)) { OvSay("Mischen ging nicht."); return null; }
        var v = await MsImport(mix, split: false);
        if (v is null) return null;
        Songs.Update(v.Id, x => { x.Title = s.Title + $" ({label})"; x.Lyrics = s.Lyrics; x.Style = s.Style; });
        TryDelete(mix);
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.eigen.gesungen", v.Id);
        OvSay($"✓ „{s.Title}“ in {(bankVoice is null ? "deiner Stimme" : "der Stimme „" + bankVoice.Name + "“")} ist fertig.");
        return Songs.Get(v.Id);
    }

    /// <summary>Alles zur eigenen Stimme löschen: Aufnahmen und gelerntes Modell.</summary>
    public void MsOwnVoiceDelete()
    {
        try { if (Directory.Exists(OwnDir)) Directory.Delete(OwnDir, true); } catch (IOException) { }
        if (OwnVoiceRunOverride is null && !_opt.Isolated)
            try { var logs = Path.Combine(KiStore.PyPaths(KiRuntime.Stimme).Repo, "logs", OwnVoiceName); if (Directory.Exists(logs)) Directory.Delete(logs, true); } catch (IOException) { }
        _ovConsent = false; if (_cwVoice == "eigen") _cwVoice = "frei";
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.eigen.geloescht", "");
        OvSay("Deine Aufnahmen und die gelernte Stimme sind gelöscht.");
    }

    Control MsOwnVoicePanel()
    {
        var sp = new StackPanel { Name = "ov-panel", Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = "Meine eigene Stimme", Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
        if (OwnVoiceReady)
        {
            sp.Children.Add(P("✓ Deine Stimme ist gelernt. Wähle oben „Meine eigene Stimme“ – dann singt das Cover in deiner Stimme.", Muted, 11));
        }
        else
        {
            var consent = new CheckBox { Name = "ov-consent", IsChecked = _ovConsent, FontSize = 11.5, Content = new TextBlock { Text = "Das ist meine eigene Stimme. Ich nehme sie hier selbst mit dem Mikrofon auf.", TextWrapping = TextWrapping.Wrap } };
            consent.IsCheckedChanged += (_, _) => { _ovConsent = consent.IsChecked == true; Show(MS); };
            sp.Children.Add(consent);
            sp.Children.Add(P($"Trendo lernt nur Stimmen, die hier live aufgenommen werden – keine Dateien, keine fremden Stimmen. {OwnTakes} Aufnahmen à {OwnTakeSeconds} s (mindestens {OwnMinTakes}).", Muted, 11));
            var n = OwnTakeCount;
            var bar = new ProgressBar { Name = "ov-takes", Minimum = 0, Maximum = OwnTakes, Value = n, Height = 6 };
            sp.Children.Add(bar);
            if (n < OwnTakes) sp.Children.Add(P($"Nächste Aufnahme ({n + 1}): {OwnTakeHints[n % OwnTakeHints.Length]}", Text, 11.5));
            var rec = new Button { Name = "ov-record", Content = _ovBusy ? "Läuft …" : $"Aufnahme {Math.Min(n + 1, OwnTakes)} von {OwnTakes}", FontSize = 12, IsEnabled = _ovConsent && !_ovBusy && n < OwnTakes };
            rec.Click += async (_, _) => await MsOwnVoiceRecord();
            var learn = new Button { Name = "ov-learn", Content = "Stimme lernen", FontSize = 12, IsEnabled = _ovConsent && !_ovBusy && n >= OwnMinTakes, Background = n >= OwnMinTakes ? Accent : null, Foreground = n >= OwnMinTakes ? Brushes.White : null };
            learn.Click += async (_, _) => await MsOwnVoiceLearn();
            sp.Children.Add(new WrapPanel { Children = { rec, new Border { Width = 6 }, learn } });
        }
        if (OwnVoiceReady || OwnTakeCount > 0)
        {
            var del = new Button { Name = "ov-delete", Content = "Meine Stimme löschen", FontSize = 11.5, Padding = new Thickness(8, 2) };
            del.Click += (_, _) => { MsOwnVoiceDelete(); Show(MS); };
            sp.Children.Add(del);
        }
        if (_ovMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "ov-msg", Text = _ovMsg, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        return new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8), Child = sp };
    }
}
