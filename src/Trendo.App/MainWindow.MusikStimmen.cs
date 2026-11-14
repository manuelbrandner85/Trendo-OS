using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Trendo Musik V3/V4/V5/V6 (freigegeben 07.11.2026, „Alle ja“): Stimme aus einer Aufnahme lernen (nur eigene Stimme oder mit Einwilligung,
/// keine bekannten Künstler), neue Lieder in dieser Stimme singen lassen, neuen eigenen Text auf eine vorhandene Begleitung, Stimmen-Bibliothek mit Nachweis.</summary>
public partial class MainWindow
{
    public VoiceBank Bank => new(_core.DataRoot);
    /// <summary>V4: Stimme für neue Lieder – leer = KI-Stimme, „eigen“ = Mikrofon-Stimme, sonst Id aus der Bibliothek.</summary>
    public string MsSingVoice { get; set; } = "";
    string _vbName = "", _vbPerson = "", _vbStatement = "", _vbKind = "eigen", _vbNewLyrics = ""; bool _vbNotFamous;

    /// <summary>Gelernte Stimmen zur Auswahl (Id, Anzeige).</summary>
    public List<(string Id, string Label)> MsVoiceChoices()
    {
        var l = new List<(string, string)> { ("", "KI-Stimme (wie erzeugt)") };
        if (OwnVoiceReady) l.Add(("eigen", "Meine Stimme (Mikrofon)"));
        l.AddRange(Bank.All().Where(v => v.Ready).Select(v => (v.Id, v.Name + (v.Consent.Kind == "person" ? $" (Einwilligung {v.Consent.Person})" : " (eigene Stimme)"))));
        return l;
    }

    /// <summary>V3: Stimme aus dem Gesang eines Lieds lernen. Ohne gültige Einwilligung wird nichts angelegt.</summary>
    public async Task<VoiceBank.Voice?> MsVoiceLearnFromSong(MusicSong s, string name, VoiceBank.Consent consent, bool notFamous)
    {
        if (_ovBusy) return null;
        if (VoiceBank.ConsentProblem(consent, notFamous) is { } p) { OvSay(p); return null; }
        var (_, voc) = StemsOf(s);
        if (voc is null) { var made = await MsSplit(s.Id); voc = made.FirstOrDefault(x => x.Kind == "gesang"); }
        if (voc is null) { OvSay("Erst den Gesang abtrennen („Spuren trennen“)."); return null; }
        VoiceBank.Voice v;
        try { v = Bank.Add(name, "aufnahme", consent, notFamous, s.Id); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { OvSay(e.Message); return null; }
        int parts;
        try { parts = await Task.Run(() => Bank.Segment(v.Id, AudioFile.Read(voc.File))); }
        catch (Exception e) when (e is InvalidOperationException or IOException or InvalidDataException) { Bank.Delete(v.Id); OvSay("Gesangsspur nicht lesbar: " + e.Message); return null; }
        if (parts < VoiceBank.MinSegments)
        {
            Bank.Delete(v.Id);
            OvSay($"Zu wenig Gesang zum Lernen: {parts} von mindestens {VoiceBank.MinSegments} Abschnitten à {VoiceBank.SegmentSeconds} s – ein längeres Lied oder mehrere nehmen.");
            return null;
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.bibliothek.angelegt", $"{v.Id} {consent.Kind} {consent.Person}");
        _ovBusy = true;
        try
        {
            if (OwnVoiceRunOverride is null)
            {
                if (_opt.Isolated) { OvSay("Testmodus: Trendo lernt keine Stimme."); return v; }
                if (!KiStore.RuntimeReady(KiRuntime.Stimme)) { OvSay("Stimmen lernen wird eingerichtet (Applio, frei, MIT – einmalig mehrere GB) …"); await KiPyInstall(KiLibrary.Models.First(m => m.Id == "musik-stimme"), KiPyApps.For(KiRuntime.Stimme)!); }
            }
            OvSay($"Trendo lernt die Stimme „{v.Name}“ aus {parts} Abschnitten … (mit NVIDIA-Grafikkarte etwa 30–40 Minuten)");
            var r = await OvRun(["lernen", v.Id, Bank.TakesDir(v.Id), "60"], 4 * 3600_000);
            var ok = r.Ok && r.Out.Contains("\"ok\": true");
            if (ok) Bank.MarkReady(v.Id);
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.bibliothek.gelernt", ok ? v.Id : "fehler");
            OvSay(ok ? $"✓ Stimme „{v.Name}“ gelernt – beim Erstellen unter „Gesang in“ wählen." : "Lernen ging nicht: " + Short(r.Err.Length > 0 ? r.Err : r.Out));
            return Bank.Get(v.Id);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { OvSay("Einrichten ging nicht: " + e.Message); return Bank.Get(v.Id); }
        finally { _ovBusy = false; if (CurrentView == MS) Show(MS); }
    }

    /// <summary>V6: Stimme mit allen Aufnahmen und dem gelernten Modell löschen.</summary>
    public void MsVoiceDelete(string id)
    {
        Bank.Delete(id);
        if (OwnVoiceRunOverride is null && !_opt.Isolated)
            try { var logs = Path.Combine(KiStore.PyPaths(KiRuntime.Stimme).Repo, "logs", id); if (Directory.Exists(logs)) Directory.Delete(logs, true); } catch (IOException) { }
        if (MsSingVoice == id) MsSingVoice = "";
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.stimme.bibliothek.geloescht", id);
        OvSay("Stimme gelöscht – mit Aufnahmen, gelerntem Modell und Nachweis.");
    }

    /// <summary>V5: neuen eigenen Text auf die Begleitung eines Lieds singen lassen (die Musik bleibt, der Gesang ist neu).</summary>
    public async Task<List<MusicSong>> MsNewLyricsOnMusic(MusicSong s, string lyrics)
    {
        if (string.IsNullOrWhiteSpace(lyrics) || LyricTiming.Lines(lyrics).Count < 2) { _msMsg = "Bitte deinen eigenen Text einfügen (mindestens zwei Zeilen)."; Show(MS); return []; }
        var (acc, _) = StemsOf(s);
        if (acc is null) { var made = await MsSplit(s.Id); acc = made.FirstOrDefault(x => x.Kind == "begleitung"); }
        if (acc is null) { _msMsg = "Erst „Spuren trennen“ – dann gibt es die Begleitung."; Show(MS); return []; }
        var root = Songs.Get(s.Kind is "begleitung" or "gesang" && s.ParentId is { } p ? p : s.Id) ?? s;
        var secs = (int)Math.Max(10, root.Seconds > 0 ? root.Seconds : acc.Seconds);
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.neuertext", root.Id);
        return await MusicStudioCreate(new AceStepClient.Job(root.Style.Length > 0 ? root.Style : "Gesang", lyrics.Trim(), secs, root.Language.Length > 0 ? root.Language : "de", 1,
            root.Bpm, root.Key, root.TimeSignature, Task: "cover", CoverStrength: CoverStudio.Strength(1)), root.Title + " – neuer Text", acc.Id);
    }

    /// <summary>V4: nach dem Erzeugen in der gewählten Stimme singen lassen.</summary>
    async Task MsApplySingVoice(List<MusicSong> made)
    {
        if (MsSingVoice.Length == 0 || made.Count == 0) return;
        await MsOwnVoiceSing(made[0], 0, MsSingVoice == "eigen" ? null : MsSingVoice);
    }

    Control MsSingVoiceBox()
    {
        var choices = MsVoiceChoices();
        var box = new ComboBox { Name = "ms-sing-voice", ItemsSource = choices.Select(c => "Gesang in: " + c.Label).ToList(), SelectedIndex = Math.Max(0, choices.FindIndex(c => c.Id == MsSingVoice)), HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) MsSingVoice = choices[box.SelectedIndex].Id; };
        return box;
    }

    /// <summary>Stimmen-Bereich im Lied: Stimme aus diesem Gesang lernen, neuer Text auf diese Musik, Bibliothek.</summary>
    Control MsVoicePanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-voices", Spacing = 5, Margin = new Thickness(0, 10, 0, 0) };
        // V5
        sp.Children.Add(new TextBlock { Text = "NEUER TEXT AUF DIESE MUSIK", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold });
        var lyr = new TextBox { Name = "ms-newlyrics", Text = _vbNewLyrics, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60, FontSize = 12, Watermark = "Dein eigener Text – die Begleitung bleibt, der Gesang ist neu" };
        lyr.TextChanged += (_, _) => _vbNewLyrics = lyr.Text ?? "";
        sp.Children.Add(lyr); sp.Children.Add(Act("ms-newlyrics-go", "Mit neuem Text singen", () => _ = MsNewLyricsOnMusic(s, lyr.Text ?? "")));
        // V3
        sp.Children.Add(new TextBlock { Text = "STIMME AUS DIESEM GESANG LERNEN", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
        var name = new TextBox { Name = "vb-name", Text = _vbName, Watermark = "Name der Stimme", FontSize = 12 }; name.TextChanged += (_, _) => _vbName = name.Text ?? "";
        var kind = new ComboBox { Name = "vb-kind", ItemsSource = new[] { "Das ist meine eigene Stimme", "Eine andere Person hat eingewilligt" }, SelectedIndex = _vbKind == "person" ? 1 : 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        kind.SelectionChanged += (_, _) => { _vbKind = kind.SelectedIndex == 1 ? "person" : "eigen"; Show(MS); };
        sp.Children.Add(name); sp.Children.Add(kind);
        if (_vbKind == "person")
        {
            var person = new TextBox { Name = "vb-person", Text = _vbPerson, Watermark = "Name der Person", FontSize = 12 }; person.TextChanged += (_, _) => _vbPerson = person.Text ?? "";
            var st = new TextBox { Name = "vb-statement", Text = _vbStatement, Watermark = "Erklärung, z. B. „Erika Muster erlaubt am 07.11.2026 das Lernen ihrer Stimme für eigene Lieder.“", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
            st.TextChanged += (_, _) => _vbStatement = st.Text ?? "";
            sp.Children.Add(person); sp.Children.Add(st);
        }
        var famous = new CheckBox { Name = "vb-notfamous", IsChecked = _vbNotFamous, FontSize = 11.5, Content = new TextBlock { Text = "Keine bekannte Künstlerin, kein bekannter Künstler, keine prominente Person", TextWrapping = TextWrapping.Wrap } };
        famous.IsCheckedChanged += (_, _) => _vbNotFamous = famous.IsChecked == true;
        sp.Children.Add(famous);
        sp.Children.Add(Act("vb-learn", _ovBusy ? "Läuft …" : "Stimme lernen", () => _ = MsVoiceLearnFromSong(s, _vbName,
            new VoiceBank.Consent(_vbKind, _vbKind == "person" ? _vbPerson : "", DateTimeOffset.Now, _vbKind == "person" ? _vbStatement : "Das ist meine eigene Stimme."), _vbNotFamous)));
        // V6
        var voices = Bank.All();
        sp.Children.Add(new TextBlock { Text = $"STIMMEN-BIBLIOTHEK ({voices.Count + (OwnVoiceReady ? 1 : 0)})", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
        if (OwnVoiceReady) sp.Children.Add(P("• Meine Stimme (Mikrofon) – gelernt", Text, 11.5));
        foreach (var v in voices)
        {
            var del = new Button { Name = "vb-del-" + v.Id, Content = "🗑", FontSize = 11, Padding = new Thickness(6, 0) };
            var id = v.Id; del.Click += (_, _) => { MsVoiceDelete(id); Show(MS); };
            var who = v.Consent.Kind == "person" ? $"Einwilligung {v.Consent.Person} ({v.Consent.Date:dd.MM.yyyy})" : "eigene Stimme";
            sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = $"• {v.Name} – {who} – {(v.Ready ? "gelernt" : "noch nicht gelernt")} ", Foreground = Text, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }, del } });
        }
        if (_ovMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "vb-msg", Text = _ovMsg, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        return sp;
    }
}
