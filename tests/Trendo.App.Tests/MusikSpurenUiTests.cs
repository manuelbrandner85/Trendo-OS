using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik: Spuren trennen mit Demucs (am 03.11.2026 echt geprüft); ohne Demucs erst „einrichten“.</summary>
[Collection("Claude")]
public class MusikSpurenUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    static MusicSong Song(string root)
    {
        var f = Path.Combine(root, "lied.mp3"); File.WriteAllBytes(f, "ID3"u8.ToArray());
        return new SongLibrary(root).Add(new MusicSong { Title = "Sommer", Style = "pop, female vocals", Lyrics = "[Refrain]\nSommer nimm uns mit", Seconds = 30, File = f });
    }

    [AvaloniaFact(DisplayName = "Spuren trennen: Begleitung und Gesang werden eigene Lieder (mit Herkunft), alle Spuren im Ordner „– Spuren“")]
    public async Task Split_song()
    {
        var (w, _, root) = Ui.Open();
        string? src = null, outDir = null;
        w.StemSplitOverride = (f, d) =>
        {
            src = f; outDir = d; Directory.CreateDirectory(d);
            foreach (var n in new[] { "gesang", "schlagzeug", "bass", "rest", "begleitung" }) File.WriteAllBytes(Path.Combine(d, n + ".wav"), [1, 2, 3]);
            return Task.FromResult(true);
        };
        try
        {
            var s = Song(root); w.MusicStudioSelect(s.Id); w.Show("musik-studio"); await Pump();
            w.Find<Button>("ms-stems").Press();
            for (var i = 0; i < 50 && new SongLibrary(root).All().Count < 3; i++) await Pump();
            Assert.Equal(s.File, src);
            Assert.EndsWith("Sommer – Spuren", outDir);
            var lib = new SongLibrary(root).All();
            var acc = Assert.Single(lib, x => x.Kind == "begleitung"); var voc = Assert.Single(lib, x => x.Kind == "gesang");
            Assert.Equal(("Sommer – Begleitung", s.Id, "[Instrumental]"), (acc.Title, acc.ParentId, acc.Lyrics));
            Assert.Equal(("Sommer – Gesang", s.Lyrics), (voc.Title, voc.Lyrics));
            Assert.Equal(Path.Combine(outDir!, "begleitung.wav"), acc.File);
            // gewählt ist die Begleitung; dort gibt es den Spuren-Ordner statt erneut trennen
            await Pump();
            Assert.NotNull(w.Find<Button>("ms-stems-folder"));
            Assert.DoesNotContain(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<Button>(), b => b.Name == "ms-stems");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Spuren trennen ohne Demucs: „einrichten“ lädt Demucs 4.0.1 mit PyTorch in eine eigene Umgebung")]
    public async Task Stems_setup()
    {
        var (w, _, root) = Ui.Open();
        var calls = new List<string>();
        w.KiStepOverride = (exe, args, cwd, env) => { lock (calls) calls.Add(string.Join(" ", args)); return Task.FromResult(new Cli.Result(0, "ok", "")); };
        w.NvidiaOverride = () => true;
        var uv = w.KiStore.RuntimeExe(KiRuntime.Uv); Directory.CreateDirectory(Path.GetDirectoryName(uv)!); File.WriteAllText(uv, "MZ"); // uv gilt als da
        try
        {
            var s = Song(root); w.MusicStudioSelect(s.Id); w.Show("musik-studio"); await Pump();
            Assert.DoesNotContain(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<Button>(), b => b.Name == "ms-stems");
            w.Find<Button>("ms-stems-setup").Press();
            for (var i = 0; i < 100 && !calls.Any(c => c.Contains("demucs==4.0.1")); i++) await Pump();
            Assert.Contains(calls, c => c.Contains("torch") && c.Contains("cu128"));
            Assert.Contains(calls, c => c.Contains("demucs==4.0.1"));
            Assert.DoesNotContain(calls, c => c.StartsWith("clone"));
        }
        finally { w.KiStepOverride = null; w.Close(); }
    }
}
