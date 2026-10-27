using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Briefe schreiben (Teil 4): Galerie, Schrittleiste, Frist in Brief/Kalender, Serienbrief, Unterschrift, A4-Vorschau.</summary>
public class LetterProUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    /// <summary>Kleines gültiges PNG (RGBA, schwarz), ohne Grafikbibliothek.</summary>
    static byte[] Png(int w, int h)
    {
        static uint Crc(byte[] d) { uint c = 0xFFFFFFFF; foreach (var b in d) { c ^= b; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; } return ~c; }
        static byte[] Be(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
        var ms = new MemoryStream(); ms.Write([0x89, 0x50, 0x4E, 0x47, 13, 10, 26, 10]);
        void Chunk(string type, byte[] data) { ms.Write(Be((uint)data.Length)); var td = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); ms.Write(td); ms.Write(Be(Crc(td))); }
        Chunk("IHDR", [.. Be((uint)w), .. Be((uint)h), 8, 6, 0, 0, 0]);
        var raw = new byte[h * (1 + 4 * w)]; for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) raw[y * (1 + 4 * w) + 1 + 4 * x + 3] = 255;
        var z = new MemoryStream(); using (var zs = new System.IO.Compression.ZLibStream(z, System.IO.Compression.CompressionLevel.Optimal, true)) zs.Write(raw);
        Chunk("IDAT", z.ToArray()); Chunk("IEND", []);
        return ms.ToArray();
    }

    [AvaloniaFact(DisplayName = "Briefe F1/U3: Galerie bei neuem Brief, Vorlage wählen schließt sie; Schrittleiste zeigt Fortschritt und springt zum Feld")]
    public void Gallery_and_steps()
    {
        var (w, _, _) = Ui.Open();
        w.Show("brief"); Pump();
        Assert.NotNull(w.TryFind<StackPanel>("letter-gallery"));
        w.Find<Button>("letter-gallery-widerruf").Press(); Pump();
        Assert.Null(w.TryFind<StackPanel>("letter-gallery"));
        Assert.StartsWith("Widerruf", w.Letter.Subject);
        Assert.StartsWith("1 ", (string)w.Find<Button>("letter-step-1").Content!);
        w.Find<TextBox>("letter-sender-name").Text = "Erika Muster"; w.Find<TextBox>("letter-sender-street").Text = "Hauptstraße 1"; w.Find<TextBox>("letter-sender-city").Text = "10115 Berlin"; Pump();
        Assert.StartsWith("✓", (string)w.Find<Button>("letter-step-1").Content!);
        w.Find<Button>("letter-step-2").Press(); Pump();
        Assert.True(w.Find<TextBox>("letter-recipient").IsFocused);
        // U2: Klick auf den Betreff im Blatt springt ins Feld
        var subj = w.Find<TextBlock>("letter-preview-subject");
        subj.RaiseEvent(new PointerPressedEventArgs(subj, new Pointer(1, PointerType.Mouse, true), w, new Point(), 0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Pump();
        Assert.True(w.Find<TextBox>("letter-subject").IsFocused);
        Assert.Contains(w.Find<Border>("letter-preview").GetLogicalDescendants(), c => c is Viewbox);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Briefe F2: Frist berechnen, in den Brief einsetzen und als ganztägigen Termin mit Erinnerung eintragen")]
    public void Deadline()
    {
        var (w, _, _) = Ui.Open();
        w.Show("brief"); Pump();
        w.Find<Button>("letter-gallery-mahnung").Press(); Pump();
        w.Find<TextBox>("letter-dl-start").Text = "27.10.2026"; w.Find<TextBox>("letter-dl-amount").Text = "14";
        w.Find<Button>("letter-dl-calc").Press(); Pump();
        Assert.Contains("10.11.2026", w.Find<TextBlock>("letter-dl-out").Text);
        w.Find<Button>("letter-dl-insert").Press(); Pump();
        Assert.Contains("bis zum 10.11.2026", w.Letter.Body);
        w.Find<TextBox>("letter-dl-start").Text = "27.10.2026"; w.Find<Button>("letter-dl-cal").Press(); Pump();
        var ev = Assert.Single(w.Cal.All());
        Assert.True(ev.AllDay); Assert.Equal(new DateTime(2026, 11, 10), ev.Start); Assert.StartsWith("Frist:", ev.Title);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Briefe F3/F4: Serienbrief aus CSV (ein DOCX je Zeile) und Unterschrift merken")]
    public void Serial_and_signature()
    {
        var (w, _, root) = Ui.Open();
        var s = UserSettings.Load(root); s.ProjectsFolder = Path.Combine(root, "projekte"); s.SetupDone = true; s.Save(root);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        w.Close(); w = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser")); w.Show(); Pump();
        w.Show("brief"); Pump();
        w.Find<Button>("letter-gallery-frei").Press(); Pump();
        w.Find<TextBox>("letter-sender-name").Text = "Erika Muster"; w.Find<TextBox>("letter-sender-street").Text = "Hauptstraße 1"; w.Find<TextBox>("letter-sender-city").Text = "10115 Berlin";
        w.Find<TextBox>("letter-subject").Text = "Einladung"; w.Find<TextBox>("letter-salutation").Text = "Liebe {Vorname},";
        w.Find<TextBox>("letter-body").Text = "wir feiern am Samstag."; Pump();
        var csv = Path.Combine(root, "gaeste.csv");
        File.WriteAllText(csv, "Vorname;Nachname;Straße;PLZ;Ort\nAnna;Beispiel;Ring 3;80331;München\nBen;Muster;Weg 4;20095;Hamburg\n");
        w.Find<TextBox>("letter-serial-file").Text = csv;
        w.Find<Button>("letter-serial-run").Press(); Pump();
        var dir = Assert.Single(Directory.GetDirectories(Path.Combine(root, "projekte", "Briefe"), "Serienbrief-*"));
        var files = Directory.GetFiles(dir, "*.docx").Order().ToList();
        Assert.Equal(2, files.Count);
        Assert.Contains("Liebe Anna,", Letters.ReadDocxText(files[0]));
        Assert.Contains("80331 München", Letters.ReadDocxText(files[0]));
        // F4: Unterschrift
        var png = Path.Combine(root, "sig.png");
        File.WriteAllBytes(png, Png(40, 16));
        w.Find<TextBox>("letter-signature").Text = png; w.Find<Button>("letter-signature-set").Press(); Pump();
        Assert.Equal(png, UserSettings.Load(root).LetterSignature);
        Assert.Equal(png, w.Letter.SignatureFile);
        w.Close();
    }
}
