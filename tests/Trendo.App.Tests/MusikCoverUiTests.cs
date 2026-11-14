using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using SkiaSharp;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 03.11.2026: Cover-Bilder in Trendo Musik „perfekt wie ein Song-Cover“ – Motiv (Bild-KI nachgebildet), echte Schrift, Layouts, MP3 mit Cover.</summary>
[Collection("Claude")]
public class MusikCoverUiTests
{
    static async Task Pump() { for (var i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); } }

    /// <summary>Nachgebildete Bild-KI: ein 1024er-Motiv (Verlauf) – oder ein echtes Motiv aus TRENDO_COVER_MOTIV zum Ansehen.</summary>
    static Task<bool> FakeMotif(string prompt, int seed, string png)
    {
        if (Environment.GetEnvironmentVariable("TRENDO_COVER_MOTIV") is { } real && File.Exists(real)) { File.Copy(real, png, true); return Task.FromResult(true); }
        using var s = SKSurface.Create(new SKImageInfo(1024, 1024));
        using var p = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(1024, 1024), [SKColors.DarkOrange, SKColors.MidnightBlue], SKShaderTileMode.Clamp) };
        s.Canvas.DrawRect(0, 0, 1024, 1024, p);
        using var img = s.Snapshot(); using var d = img.Encode(SKEncodedImageFormat.Png, 90); File.WriteAllBytes(png, d.ToArray());
        return Task.FromResult(true);
    }

    static MusicSong AddSong(MainWindow w, string root)
    {
        var dir = Path.Combine(root, "musik-test"); Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "lied.mp3");
        File.WriteAllBytes(f, [.. "ID3"u8.ToArray(), 3, 0, 0, 0, 0, 0, 2, 0xAA, 0xBB, 0xFF, 0xFB, 0x90, 0x00, 1, 2, 3, 4]); // kleiner alter Tag + Ton
        var s = new SongLibrary(root).Add(new MusicSong { Title = "Nachts im Blue Note", Style = "ruhige Jazz-Ballade, Saxophon", Seconds = 30, File = f });
        w.MusicStudioSelect(s.Id); w.Show("musik-studio");
        return s;
    }

    [AvaloniaFact(DisplayName = "Musik-Cover: „Cover-Bild erstellen“ – Motiv zum Genre (Jazz-Club), 1400 × 1400 JPEG mit Titel und Name, erscheint in Bibliothek und Lied; Layout wechseln, Neu würfeln")]
    public async Task Make_cover()
    {
        var (w, _, root) = Ui.Open();
        string? seenPrompt = null;
        MainWindow.CoverImageOverride = (p, s, png) => { seenPrompt = p; return FakeMotif(p, s, png); };
        try
        {
            var s = AddSong(w, root); await Pump();
            w.Find<TextBox>("ms-art-artist").Text = "Erika Muster";
            w.Find<Button>("ms-art-make").Press(); await Pump();
            for (var i = 0; i < 100 && (new SongLibrary(root).Get(s.Id)!.CoverFile.Length == 0); i++) await Pump();
            var song = new SongLibrary(root).Get(s.Id)!;
            Assert.True(File.Exists(song.CoverFile), w.TryFind<TextBlock>("ms-msg")?.Text);
            Assert.Contains("jazz club", seenPrompt); Assert.Contains("no text", seenPrompt);
            Assert.Equal("Erika Muster", song.Artist);
            using (var bmp = SKBitmap.Decode(song.CoverFile)) { Assert.Equal(1400, bmp.Width); Assert.Equal(1400, bmp.Height); }
            Assert.Equal((byte)0xFF, File.ReadAllBytes(song.CoverFile)[0]); // JPEG
            if (Environment.GetEnvironmentVariable("TRENDO_COVER_OUT") is { } outDir) File.Copy(song.CoverFile, Path.Combine(outDir, "cover-unten.jpg"), true);
            Assert.True(w.Find<Border>("ms-detail").GetLogicalDescendants().OfType<Image>().Any());
            Assert.Equal("Neu würfeln", w.Find<Button>("ms-art-make").Content);

            var first = song.CoverFile;
            w.Find<Button>("ms-art-layout-mittig").Press(); await Pump();
            for (var i = 0; i < 100 && new SongLibrary(root).Get(s.Id)!.CoverLayout != "mittig"; i++) await Pump();
            song = new SongLibrary(root).Get(s.Id)!;
            Assert.Equal("mittig", song.CoverLayout); Assert.True(File.Exists(song.CoverFile));
            if (Environment.GetEnvironmentVariable("TRENDO_COVER_OUT") is { } outDir2) File.Copy(song.CoverFile, Path.Combine(outDir2, "cover-mittig.jpg"), true);
            var seed = song.CoverSeed;
            w.Find<Button>("ms-art-make").Press(); await Pump();
            for (var i = 0; i < 100 && new SongLibrary(root).Get(s.Id)!.CoverSeed == seed; i++) await Pump();
            Assert.NotEqual(seed, new SongLibrary(root).Get(s.Id)!.CoverSeed);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(first)!, "*.jpg")); // altes Cover ersetzt
        }
        finally { MainWindow.CoverImageOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Musik-Cover ohne Bild-KI: gestaltete Farbfläche mit Schrift und ehrlicher Hinweis; „MP3 mit Cover“ bettet das Bild in die Datei ein (alter Tag ersetzt, Ton unverändert)")]
    public async Task Without_ai_and_mp3()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var s = AddSong(w, root); await Pump();
            Assert.Contains("SDXL Turbo einrichten", string.Join("|", w.GetLogicalTexts()));
            var f = await w.MsMakeCover(s.Id, 5, "oben", null, "Erika Muster"); await Pump();
            Assert.True(File.Exists(f));
            Assert.Contains("Farbfläche", w.Find<TextBlock>("ms-msg").Text);
            w.Find<Button>("ms-export-mp3c").Press(); await Pump();
            var target = Path.Combine(Path.GetDirectoryName(s.File)!, "lied (mit Cover).mp3");
            var mp3 = File.ReadAllBytes(target);
            Assert.Equal(File.ReadAllBytes(f!), SongCover.ReadMp3Cover(mp3));
            Assert.Equal(new byte[] { 0xFF, 0xFB, 0x90, 0x00, 1, 2, 3, 4 }, mp3[^8..]); // Ton bleibt, alter Tag weg
        }
        finally { w.Close(); }
    }
}
