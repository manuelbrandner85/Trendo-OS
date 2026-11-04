using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik M10: Verlängern mit dem Grundmodell (am 03.11.2026 echt geprüft); ohne Grundmodell erst „Grundmodell laden“.</summary>
[Collection("Claude")]
public class MusikExtendUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    sealed class FakeAce : HttpMessageHandler
    {
        public readonly List<string> Bodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return J("{\"status\":\"ok\"}");
                case "/release_task": var b = await r.Content!.ReadAsStringAsync(ct); lock (Bodies) Bodies.Add(b); return J("{\"data\":{\"task_id\":\"t1\"},\"code\":200}");
                case "/query_result": return J("{\"data\":[{\"task_id\":\"t1\",\"status\":1,\"result\":\"[{\\\"file\\\":\\\"/v1/audio?path=a.mp3\\\"}]\"}],\"code\":200}");
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3"u8.ToArray()) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    static MusicSong Song(string root)
    {
        var f = Path.Combine(root, "lied.mp3"); File.WriteAllBytes(f, "ID3"u8.ToArray());
        return new SongLibrary(root).Add(new MusicSong { Title = "Sommer", Style = "pop, female vocals", Lyrics = "[Strophe]\nDie Straße glänzt\n[Refrain]\nSommer nimm uns mit", Seconds = 30, File = f });
    }

    [AvaloniaFact(DisplayName = "M10 Verlängern: Abschnitt ab dem Ende (30 → 90 s) mit dem Grundmodell, Refrain kommt dazu, neues Lied „– länger“")]
    public async Task Extend_song()
    {
        var (w, _, root) = Ui.Open();
        var ace = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace)); w.AceBaseReadyOverride = true;
        try
        {
            var s = Song(root); w.MusicStudioSelect(s.Id); w.Show("musik-studio"); await Pump();
            Assert.Equal("um 1 Minute", w.Find<ComboBox>("ms-extend-by").SelectedItem);
            var made = await w.MsExtend(s.Id, 60); await Pump();
            var body = ace.Bodies.Single();
            Assert.Contains("repaint", body); Assert.Contains("30.0", body); Assert.Contains("90.0", body);
            Assert.Contains("name=audio_duration", body); Assert.Contains("\r\n\r\n90\r\n", body);
            Assert.Contains("Sommer nimm uns mit\n\n[Refrain]\nSommer nimm uns mit", body.Replace("\r\n", "\n"));
            Assert.Equal(AceStepClient.BaseModel, w.AceModel);
            var n = new SongLibrary(root).Get(made.Single().Id)!;
            Assert.Equal(("laenger", 90.0, "Sommer – länger"), (n.Kind, n.Seconds, n.Title));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M10 ohne Grundmodell: Hinweis auf das Grundmodell und „Grundmodell laden“ über das Download-Werkzeug von ACE-Step")]
    public async Task Base_model_setup()
    {
        var (w, _, root) = Ui.Open();
        var calls = new List<string>();
        w.AceBaseReadyOverride = false;
        w.KiStepOverride = (exe, args, cwd, env) => { calls.Add(string.Join(" ", args)); return Task.FromResult(new Cli.Result(0, "ok", "")); };
        try
        {
            var s = Song(root); w.MusicStudioSelect(s.Id); w.Show("musik-studio"); await Pump();
            Assert.DoesNotContain(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<Button>(), b => b.Name == "ms-extend");
            Assert.Contains("Grundmodell von ACE-Step", string.Join("|", w.GetLogicalTexts()));
            w.Find<Button>("ms-extend-setup").Press();
            for (var i = 0; i < 50 && calls.Count == 0; i++) await Pump();
            Assert.Contains("download_submodel('acestep-v15-base'", calls.Single());
        }
        finally { w.KiStepOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Lied-Ansicht übersichtlich: Registerkarten Lied, Bearbeiten, Spuren & Mitsingen, Musikvideo – jede zeigt nur ihre Werkzeuge, keine Funktion fehlt")]
    public async Task Detail_tabs()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var s = Song(root);
            w.MusicStudioSelect(s.Id); w.Show("musik-studio"); await Pump();
            bool Vis(string n) => w.Find<StackPanel>("ms-dtab-body-" + n).IsVisible;
            Assert.True(Vis("lied")); Assert.False(Vis("bearbeiten")); Assert.False(Vis("spuren")); Assert.False(Vis("video"));
            Assert.NotNull(w.TryFind<Button>("ms-master")); Assert.NotNull(w.TryFind<Button>("ms-delete"));
            w.Find<Button>("ms-dtab-bearbeiten").Press(); await Pump();
            Assert.True(Vis("bearbeiten")); Assert.False(Vis("lied"));
            Assert.True(w.Find<Button>("ms-repaint").IsEffectivelyVisible); Assert.True(w.Find<Button>("ms-cover").IsEffectivelyVisible);
            Assert.False(w.Find<Button>("ms-master").IsEffectivelyVisible);
            w.Find<Button>("ms-dtab-video").Press(); await Pump();
            Assert.True(Vis("video"));
            w.Find<Button>("ms-dtab-spuren").Press(); await Pump();
            Assert.True(Vis("spuren"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Wie bei Suno: „Erstellen“ mit „Mehr Optionen“ (alle Einstellungen bleiben), „⋯“-Menü je Lied mit allen Aktionen")]
    public async Task Suno_layout()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var s = Song(root);
            w.MusicStudioProMode(true); w.Show("musik-studio"); await Pump();
            Assert.False(w.Find<Expander>("ms-more").IsExpanded);
            foreach (var n in new[] { "ms-bpm", "ms-key", "ms-time", "ms-language", "ms-length", "ms-variants", "ms-instrumental", "ms-title", "ms-style", "ms-lyrics", "ms-create" })
                Assert.NotNull(w.TryFind<Control>(n));
            Assert.NotNull(w.Find<StackPanel>("ms-more-body"));
            Assert.NotNull(w.Find<Button>("ms-menu-0").Flyout);
            var ids = w.MsSongActions(s).Select(a => a.Id).ToList();
            Assert.Equal(["play", "fav", "edit", "stems", "video", "master", "mp3", "wav", "flac", "tovideo", "folder", "rename", "delete"], ids);
            w.MsSongActions(s).Single(a => a.Id == "video").Run(); await Pump();
            Assert.True(w.Find<StackPanel>("ms-dtab-body-video").IsVisible);
            w.MsSongActions(s).Single(a => a.Id == "fav").Run(); await Pump();
            Assert.True(new SongLibrary(root).Get(s.Id)!.Favorite);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "KI-Musikvideo A: „Lied verstehen“ trennt den Gesang ab, erkennt Stimmen, Zeilen und Thema und zeigt wer wann singt")]
    public async Task Understand_song()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var f = Path.Combine(root, "duett.mp3"); File.WriteAllBytes(f, "ID3"u8.ToArray());
            var s = new SongLibrary(root).Add(new MusicSong { Title = "Duett", Style = "pop, duet", Lyrics = "[Strophe – Sie]\nIch warte am Meer\n[Refrain – beide]\nWir zwei", Seconds = 20, File = f });
            var split = 0;
            w.StemSplitOverride = (src, dir) => { split++; Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, "begleitung.wav"), [1]); File.WriteAllBytes(Path.Combine(dir, "gesang.wav"), [1]); return Task.FromResult(true); };
            w.SectionsOverride = _ => Task.FromResult<string?>("""{"bpm":100,"duration":20,"sections":[{"start":0,"end":10,"energy":0.8},{"start":10,"end":20,"energy":1.3}],"beats":[0.6,1.2]}""");
            w.SingersOverride = v => { Assert.EndsWith("gesang.wav", v); return Task.FromResult<string?>("""{"singers":[{"id":0,"gender":"w","voice":"Alt"},{"id":1,"gender":"m","voice":"Tenor"}],"phrases":[{"start":1,"end":5,"singer":0},{"start":11,"end":15,"singer":1,"both":true}]}"""); };
            w.WordsOverride = _ => Task.FromResult<string?>(null);
            w.ThemeOverride = _ => Task.FromResult<string?>("""{"thema":"Sehnsucht","geschichte":"Zwei treffen sich am Meer.","stimmung":"warm","orte":["Strand"],"tageszeit":"Abend","farben":["Gold"]}""");
            w.MusicStudioSelect(s.Id); w.MsDetailTab("video"); w.Show("musik-studio"); await Pump();
            w.Find<Button>("ms-understand").Press();
            for (var i = 0; i < 100 && w.Understanding(s) is null; i++) await Pump();
            var u = w.Understanding(s)!;
            Assert.Equal(1, split);
            Assert.Equal("gesungene Phrasen", u.Source);
            Assert.Equal(["Sängerin", "beide"], u.Lines.Select(l => l.Who));
            Assert.Equal("Sehnsucht", u.Theme!.Topic);
            await Pump();
            Assert.Contains("Duett: Sängerin und Sänger", w.Find<TextBlock>("ms-msg").Text);
            Assert.Contains("Thema: Sehnsucht", w.Find<TextBlock>("ms-understand-theme").Text);
            Assert.Equal(2, w.Find<StackPanel>("ms-understand-lines").Children.Count);
            Assert.Equal(s.Id, w.Find<TextBlock>("ms-detail-title").Text == "Duett" ? s.Id : null); // Auswahl bleibt beim Lied, nicht bei der Gesangsspur

            // B: Drehbuch erstellen, Einstellung anklicken und ändern, Figur beschreiben, löschen, neu würfeln
            w.Find<Button>("ms-concept-performance").Press(); await Pump();
            w.Find<Button>("ms-script-plan").Press();
            for (var i = 0; i < 100 && w.Script(s) is null; i++) await Pump();
            var sc = w.Script(s)!;
            Assert.Equal("performance", sc.Concept);
            Assert.Equal(2, sc.Figures.Count);
            Assert.Contains(sc.Shots, x => x.Singing && x.Who == "beide");
            await Pump();
            w.Find<Button>("ms-shot-1").Press(); await Pump();
            Assert.NotNull(w.TryFind<StackPanel>("ms-shot-editor"));
            w.MsShotEdit(s.Id, 1, x => x with { Size = "Groß" }); await Pump();
            Assert.Contains("extreme close-up", w.Script(s)!.Shots[1].Prompt);
            w.MsFigureEdit(s.Id, "fig0", "a fictional woman with short silver hair, red coat"); await Pump();
            Assert.All(w.Script(s)!.Shots.Where(x => x.Who is "Sängerin" or "beide" && x.Singing), x => Assert.Contains("short silver hair", x.Prompt));
            var n = w.Script(s)!.Shots.Count; var seed = w.Script(s)!.Shots[1].Seed;
            w.Find<Button>("ms-shot-reroll").Press(); await Pump();
            Assert.NotEqual(seed, w.Script(s)!.Shots[1].Seed);
            w.Find<Button>("ms-shot-delete").Press(); await Pump();
            Assert.Equal(n - 1, w.Script(s)!.Shots.Count);
            Assert.Equal(20, w.Script(s)!.Shots[^1].End, 1); // keine Lücke am Ende

            // E: ein gemeinsames Projekt – Videoschnitt kennt Lied, Abschnitte, wer singt und das Drehbuch; fertiges Video landet am Lied
            w.MsToVideo(s); await Pump();
            Assert.Equal("schnitt", w.CurrentView);
            var d = w.ProgramActive("schnitt")!;
            Assert.Equal(s.Id, Trendo.Core.Media.VideoEdit.Song(d.Data)!.Value.Id);
            var mk = Trendo.Core.Media.VideoEdit.Markers(d.Data);
            Assert.Equal(2, mk.Count(m => m.Kind == "section"));
            Assert.Contains(mk, m => m.Kind == "line" && m.Who == "beide");
            Assert.Equal(w.Script(s)!.Shots.Count, mk.Count(m => m.Kind == "shot"));
            Assert.NotNull(w.TryFind<Border>("ve-song-link"));
            w.ExportAsyncOverride = (p, f, prog, ct) => { File.WriteAllText(f, "mp4"); return Task.FromResult(f); };
            await w.EditExportBackground(d); await Pump();
            var vid = Assert.Single(w.SongVideos(s));
            File.SetLastWriteTimeUtc(vid, DateTime.UtcNow.AddHours(-1)); // Lied danach geändert (z. B. gemastert)
            w.ReplaceAudioOverride = (v, song, o) => { File.WriteAllText(o, "neu"); return true; };
            var neu = w.MsVideoNewAudio(s.Id, vid);
            Assert.EndsWith("neuer Ton.mp4", neu); Assert.Equal(2, w.SongVideos(s).Count);
            w.MusicStudioSelect(s.Id); w.MsDetailTab("video"); w.Show("musik-studio"); await Pump();
            Assert.NotNull(w.TryFind<Button>("ms-vid-play-0"));
        }
        finally { w.Close(); }
    }
}
