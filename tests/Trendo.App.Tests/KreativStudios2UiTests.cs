using System.IO.Compression;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 2 (T-0611): Bücher schreiben, Animation &amp; Motion, Fotos verwalten, Bilder mit KI.</summary>
public class KreativStudios2_Ui
{
    static string Export(MainWindow w, string key, string name, string root)
    {
        var e = MainWindow.Programs[key].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(key)!, Path.Combine(root, "export-" + key + e.Extension));
    }

    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.Press(); Settle(); }

    static void WaitFor(Func<bool> done) { for (var i = 0; i < 400 && !done(); i++) { Thread.Sleep(15); Settle(); } }

    [AvaloniaFact(DisplayName = "Kreativ & KI Teil 2: alle vier Studios haben eine eigene Seite und öffnen eine Vorlage")]
    public void Pages()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (studio, key, tpl) in new[] { ("STUDIO-BOOK-01", "buch-programm", "beispiel"), ("STUDIO-ANIM-01", "animation-programm", "ball"), ("STUDIO-PHOTOS-01", "fotos-verwalten-programm", "leer"), ("STUDIO-IMAGEAI-01", "bild-ki-programm", "beispiel") })
            {
                Assert.Equal(key, MainWindow.StudioPages[studio]);
                w.Show(key); Settle();
                RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
                Assert.NotNull(w.ProgramActive(key));
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Bücher: Kapitelübersicht, neues Kapitel, Wortziel, EPUB-Export lässt sich öffnen")]
    public void Book()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "buch-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var titles = w.Find<StackPanel>("book-overview").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Der Leuchtturmwärter", titles); Assert.Contains("Der Brief", titles);
            Assert.NotNull(w.Find<ProgressBar>("book-goal"));
            RealClick(w, w.Find<Button>("prog-cmd-book-chapter")); Settle();
            Assert.EndsWith("# Kapitel 3\n\n", w.ProgramActive(K)!.Text("text"));
            using var zip = ZipFile.OpenRead(Export(w, K, "epub", root));
            Assert.Equal("mimetype", zip.Entries[0].FullName);
            Assert.Contains(zip.Entries, e => e.FullName == "OEBPS/k002.xhtml");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Animation: Ebene anlegen, Schlüssel setzen, Zeitregler zeichnet neu, Bewegung anwenden, SVG und Video")]
    public void Anim()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "animation-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            RealClick(w, w.Find<Button>("prog-cmd-anim-add-rechteck")); Settle();
            var d = w.ProgramActive(K)!;
            Assert.Single(d.List("layers"));
            var x = w.Find<TextBox>("anim-val-x"); x.Text = "100"; Tap(w, "anim-key-x"); Settle();
            var s = w.Find<Slider>("anim-slider");
            Assert.Equal(1, s.Maximum); // nur ein Schlüssel bei 0 s
            RealClick(w, w.Find<Button>("prog-cmd-anim-move-einblenden")); Settle();
            Assert.NotNull(XElement.Parse(File.ReadAllText(Export(w, K, "svg", root))));
            w.Find<Slider>("anim-slider").Value = 0.5; Settle();
            Assert.Equal("0.50 s", w.Find<TextBlock>("anim-time").Text?.Replace(',', '.'));
            string? pattern = null;
            MainWindow.AnimVideoOverride = (p, fps, outFile) => { pattern = p; File.WriteAllBytes(outFile, [1]); return fps == 30; };
            try
            {
                var mp4 = Path.Combine(root, "a.mp4");
                var t = w.AnimVideo(d, mp4); WaitFor(() => t.IsCompleted);
                Assert.Equal(mp4, t.Result);
                Assert.NotNull(pattern);
                Assert.False(Directory.Exists(Path.GetDirectoryName(pattern))); // Bildfolge wieder weg
            }
            finally { MainWindow.AnimVideoOverride = null; }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Fotos verwalten: Ordner wählen, Probelauf ändert nichts, Einsortieren erst nach zweitem Klick, Rückgängig")]
    public void Photos()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var src = Path.Combine(root, "quelle"); var dst = Path.Combine(root, "ziel"); Directory.CreateDirectory(src);
        for (var i = 0; i < 3; i++) { var f = Path.Combine(src, $"p{i}.jpg"); File.WriteAllBytes(f, [(byte)i, 1, 2]); File.SetLastWriteTime(f, new DateTime(2026, 1 + i, 5)); }
        MainWindow.FolderPickOverride = title => title.Contains("aus") ? src : dst;
        try
        {
            const string K = "fotos-verwalten-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            Tap(w, "photos-source"); WaitFor(() => w.ProgramActive(K)!.Text("source") == src);
            Tap(w, "photos-target"); WaitFor(() => w.ProgramActive(K)!.Text("target") == dst);
            var d = w.ProgramActive(K)!;
            var t = w.PhotosScan(d); WaitFor(() => t.IsCompleted);
            Assert.Contains("Probelauf: 3", w.ProgramMessage(K));
            Assert.False(Directory.Exists(dst)); // Probelauf ändert nichts
            Tap(w, "photos-apply"); Settle();
            Assert.False(Directory.Exists(dst)); // erst fragen
            Assert.StartsWith("Ja, 3 Fotos kopieren", w.Find<Button>("photos-apply").Content?.ToString());
            Tap(w, "photos-apply"); Settle();
            Assert.Equal(3, Directory.GetFiles(dst, "*.jpg", SearchOption.AllDirectories).Length);
            Assert.Equal(3, Directory.GetFiles(src).Length);
            Assert.True(File.Exists(Path.Combine(dst, "2026", "02 Februar", "p1.jpg")));
            Tap(w, "photos-undo"); Settle();
            Assert.Empty(Directory.GetFiles(dst, "*.jpg", SearchOption.AllDirectories));
            Assert.Contains("Kopien entfernt", w.ProgramMessage(K));
        }
        finally { MainWindow.FolderPickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Bilder mit KI: ohne Bild-KI klarer Hinweis, gesperrte Beschreibung, Varianten mit Herkunftsnachweis, Galerie und ZIP")]
    public void ImageAi()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "bild-ki-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var d = w.ProgramActive(K)!;
            var t0 = w.ImgGenerate(d); WaitFor(() => t0.IsCompleted);
            Assert.Equal(0, t0.Result);
            Assert.Contains("nicht eingerichtet", w.ProgramMessage(K));
            var calls = new List<(string P, int Seed, int W, int H)>();
            var png1 = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            MainWindow.ImageAiOverride = (p, seed, png, wi, he) => { calls.Add((p, seed, wi, he)); File.WriteAllBytes(png, png1); return Task.FromResult(true); };
            try
            {
                d.Set("style", "aquarell", "Stil");
                var t = w.ImgGenerate(d); WaitFor(() => t.IsCompleted);
                Assert.Equal(2, t.Result);
                Assert.Equal(2, calls.Count); Assert.NotEqual(calls[0].Seed, calls[1].Seed);
                Assert.Contains("watercolor", calls[0].P);
                Assert.All(d.List("images"), n => Assert.True(File.Exists((string)n! + ".json")));
                w.Show(K); Settle();
                Assert.Equal(2, w.Find<WrapPanel>("img-gallery").Children.Count);
                using (var z = ZipFile.OpenRead(Export(w, K, "zip", root))) Assert.Equal(4, z.Entries.Count);
                d.Set("prompt", "porn", "Beschreibung");
                var t2 = w.ImgGenerate(d); WaitFor(() => t2.IsCompleted);
                Assert.Equal(0, t2.Result); Assert.Equal(2, calls.Count);
            }
            finally { MainWindow.ImageAiOverride = null; }
        }
        finally { w.Close(); }
    }
}
