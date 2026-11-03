using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik – Musikvideo: Shotliste, Clips (eigene, Pexels), Rendern mit Look/Balken/Liedtext, 16:9 und 9:16, Videoschnitt.</summary>
[Collection("Claude")]
public class MusikVideoUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    const string Abschnitte = """{"bpm": 120.0, "duration": 24.0, "sections": [{"start": 0.0, "end": 12.0, "energy": 0.5}, {"start": 12.0, "end": 24.0, "energy": 1.4}]}""";

    static (MusicSong Orig, MusicSong Voc) Library(string root)
    {
        var lib = new SongLibrary(root);
        string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
        var o = lib.Add(new MusicSong { Title = "Sommer", Style = "beach at sunset, acoustic guitar, female vocals", Lyrics = "[Strophe]\nAm Kiosk brennt noch Licht\n[Refrain]\nDu wartest am Gleis", Seconds = 24, File = F("lied.mp3") });
        lib.Add(new MusicSong { Title = "Sommer – Begleitung", Seconds = 24, File = F("beg.wav"), Kind = "begleitung", ParentId = o.Id });
        var v = lib.Add(new MusicSong { Title = "Sommer – Gesang", Seconds = 24, File = F("ges.wav"), Kind = "gesang", ParentId = o.Id });
        return (o, v);
    }

    [AvaloniaFact(DisplayName = "Musikvideo: Shotliste aus den Abschnitten, eigene Clips, Rendern in 16:9 und 9:16 mit Look, Balken und Liedtext; dann in den Videoschnitt")]
    public async Task Plan_and_render()
    {
        var (w, _, root) = Ui.Open();
        var (orig, voc) = Library(root);
        w.SectionsOverride = f => Task.FromResult<string?>(f == orig.File ? Abschnitte : null);
        w.MelodyOverride = _ => Task.FromResult<string?>("""{"hop": 0.5, "midi": [null, 60, 60, 60, null, null, null, 62, 62, 62, null]}""");
        var runs = new List<string[]>();
        w.MasterRunOverride = a => { runs.Add(a); File.WriteAllBytes(a[^1], new byte[2000]); return Task.FromResult(new Cli.Result(0, "", "")); };
        var clipA = Path.Combine(root, "a.mp4"); var clipB = Path.Combine(root, "b.mp4"); File.WriteAllBytes(clipA, [1]); File.WriteAllBytes(clipB, [1]);
        w.PickOverride = _ => Task.FromResult<string[]?>([clipA, clipB]);
        try
        {
            w.MusicStudioSelect(orig.Id); w.Show("musik-studio"); await Pump();
            var shots = await w.MsVideoPlan(orig.Id); await Pump();
            Assert.NotNull(shots);
            Assert.Equal(24, shots!.Sum(s => s.Length), 3);
            Assert.NotNull(w.Find<StackPanel>("ms-video-shots"));
            w.Find<Button>("ms-video-own").Press(); await Pump();
            Assert.Equal("2 Clips gewählt", w.Find<TextBlock>("ms-video-clips").Text);
            w.Find<ComboBox>("ms-video-format").SelectedIndex = 2; w.Find<ComboBox>("ms-video-look").SelectedIndex = 1; await Pump();
            var made = await w.MsVideoRender(orig.Id); await Pump();
            Assert.Equal(2, made.Count);
            Assert.Contains(made, m => m.EndsWith("Musikvideo 16x9.mp4")); Assert.Contains(made, m => m.EndsWith("Musikvideo 9x16.mp4"));
            var clipRuns = runs.Where(r => r.Contains("-an")).ToList();
            Assert.Equal(shots.Count * 2, clipRuns.Count);
            Assert.Contains(clipRuns, r => r.Contains(clipA)); Assert.Contains(clipRuns, r => r.Contains(clipB));
            Assert.Contains(clipRuns, r => string.Join(" ", r).Contains("look-warm.cube") && string.Join(" ", r).Contains("drawbox"));
            var join = runs.Where(r => r.Contains("concat")).ToList();
            Assert.Equal(2, join.Count); Assert.All(join, r => Assert.Contains(orig.File, r));
            Assert.Contains(join, r => string.Join(" ", r).Contains("liedtext-16x9.ass"));
            var ass = await File.ReadAllTextAsync(Directory.GetFiles(Path.GetDirectoryName(made[0])!, "liedtext-16x9.ass").Single());
            Assert.Contains("Am Kiosk brennt noch Licht", ass); Assert.Contains("Du wartest am Gleis", ass);
            w.Find<Button>("ms-video-edit-16x9").Press(); await Pump();
            Assert.Equal("schnitt", w.CurrentView);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Musikvideo V3: ohne Schlüssel ehrlicher Hinweis; mit Schlüssel Suche nach Bildwörtern aus dem Stil, Clips geladen, Schlüssel im Tresor statt im Feld, Quellen notiert")]
    public async Task Pexels_clips()
    {
        var (w, _, root) = Ui.Open(); var core = w.Core;
        var (orig, _) = Library(root);
        Uri? asked = null; string? usedKey = null; var downloads = new List<string>();
        w.PexelsFetchOverride = (u, k) => { asked = u; usedKey = k; return Task.FromResult<string?>("""
            {"videos":[{"id":7,"duration":12,"url":"https://www.pexels.com/video/7/","user":{"name":"Erika Muster"},"video_files":[
              {"link":"https://videos.pexels.com/7-hd.mp4","width":1920,"height":1080,"file_type":"video/mp4"},{"link":"https://videos.pexels.com/7-sd.mp4","width":640,"height":360,"file_type":"video/mp4"}]},
             {"id":8,"duration":2,"url":"u","user":{"name":"x"},"video_files":[{"link":"https://videos.pexels.com/8.mp4","width":1920,"height":1080,"file_type":"video/mp4"}]}]}
            """); };
        w.StockDownloadOverride = (u, f) => { downloads.Add(u.AbsoluteUri); File.WriteAllBytes(f, [1]); return Task.FromResult(true); };
        try
        {
            w.MusicStudioSelect(orig.Id); w.Show("musik-studio"); await Pump();
            Assert.Equal(0, await w.MsVideoStock(orig.Id));
            Assert.Contains("eigenen kostenlosen Schlüssel", w.Find<TextBlock>("ms-msg").Text);
            w.Find<TextBox>("ms-video-pexels-key").Text = "abcdefghijklmnopqrstuvwxyz0123456789"; await Pump();
            Assert.Equal(1, await w.MsVideoStock(orig.Id)); await Pump();
            Assert.Contains("query=beach%20at%20sunset%20acoustic%20guitar", asked!.AbsoluteUri);
            Assert.Equal("abcdefghijklmnopqrstuvwxyz0123456789", usedKey);
            Assert.Equal(["https://videos.pexels.com/7-hd.mp4"], downloads); // nur Clips ab 4 s, passende Größe
            Assert.Equal("abcdefghijklmnopqrstuvwxyz0123456789", core.Secrets.Get("pexels-api-key"));
            Assert.Equal("", w.Find<TextBox>("ms-video-pexels-key").Text ?? "");
            Assert.Contains("Erika Muster", await File.ReadAllTextAsync(Directory.GetFiles(root, "QUELLEN.txt", SearchOption.AllDirectories).Single()));
        }
        finally { core.Secrets.Delete("pexels-api-key"); w.Close(); }
    }
}
