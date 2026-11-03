using System.IO.Compression;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Line = Avalonia.Controls.Shapes.Line;
using Polyline = Avalonia.Controls.Shapes.Polyline;
using Path = System.IO.Path;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>F3 Briefe (Vorbild-Analyse „Nächste Schritte“): Unterschrift zeichnen, Rechtschreibung beim Tippen, Falz- und Lochmarken abschaltbar.</summary>
public class LetterF3UiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static Point At(MainWindow w, Control c, double x, double y) { c.BringIntoView(); Pump(); return c.TranslatePoint(new Point(x, y), w)!.Value; }

    static void Step3(MainWindow w)
    {
        w.Show("brief"); Pump();
        w.Find<Button>("letter-wiz-tab-3").Press(); Pump();
    }

    [AvaloniaFact(DisplayName = "Briefe F3: Unterschrift mit der Maus zeichnen → PNG unter dem Gruß (Vorschau und DOCX), Feld leeren, gemerkt für neue Briefe")]
    public void Draw_signature()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            Step3(w);
            Assert.False(w.Find<Button>("letter-sig-use").IsEnabled); Assert.False(w.Find<Button>("letter-sig-clear").IsEnabled); // erst nach dem ersten Strich
            Canvas pad = null!;
            void Stroke(params (double X, double Y)[] pts)
            {
                pad = w.Find<Canvas>("letter-sig-canvas");
                w.MouseDown(At(w, pad, pts[0].X, pts[0].Y), MouseButton.Left);
                foreach (var p in pts.Skip(1)) { w.MouseMove(At(w, pad, p.X, p.Y)); Pump(); }
                w.MouseUp(At(w, pad, pts[^1].X, pts[^1].Y), MouseButton.Left); Pump();
            }
            Stroke((30, 70), (60, 30), (90, 75), (130, 35), (170, 70), (220, 40));
            Stroke((40, 85), (240, 82));
            Assert.Equal(2, w.LetterSignatureStrokes);
            Assert.True(w.Find<Button>("letter-sig-use").IsEnabled);
            Assert.Equal(2, pad.Children.OfType<Polyline>().Count());
            w.Find<Button>("letter-sig-use").Press(); Pump();
            var f = w.Letter.SignatureFile;
            Assert.StartsWith(Path.Combine(root, "briefe", "unterschrift-gezeichnet-"), f);
            var (iw, ih) = XrPlus.ImageSize(File.ReadAllBytes(f)); Assert.True(iw > 500 && ih > 120, $"{iw}x{ih}");
            Assert.Equal(f, UserSettings.Load(root).LetterSignature);
            Assert.True(w.Find<Image>("letter-preview-signature").IsVisible);
            Assert.Equal(0, w.LetterSignatureStrokes); // Feld nach dem Übernehmen leer

            // Feld leeren
            Stroke((30, 50), (120, 60));
            w.Find<Button>("letter-sig-clear").Press(); Pump();
            Assert.Equal(0, w.LetterSignatureStrokes); Assert.Empty(w.Find<Canvas>("letter-sig-canvas").Children.OfType<Polyline>());

            // im DOCX
            var d = w.Letter; d.Recipient = "Firma\nStraße 1\n12345 Ort"; d.SenderName = "Erika Muster"; d.SenderStreet = "Weg 1"; d.SenderCity = "12345 Ort"; d.Subject = "Test"; d.Body = "Text";
            var docx = Path.Combine(root, "b.docx"); Letters.WriteDocx(d, docx);
            using var z = ZipFile.OpenRead(docx); Assert.NotNull(z.GetEntry("word/media/unterschrift.png"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Briefe F3: Falz- und Lochmarken – Haken aus nimmt sie aus Vorschau und Brief, Haken an bringt sie zurück")]
    public void Print_marks_toggle()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("brief"); Pump();
            int Marks() => w.Find<StackPanel>("letter-pages").GetLogicalDescendants().OfType<Line>().Count(l => Canvas.GetLeft(l) == 3);
            Assert.Equal(3, Marks());
            w.Find<Expander>("letter-more").IsExpanded = true; Pump();
            w.Find<CheckBox>("letter-print-marks").IsChecked = false; Pump();
            Assert.False(w.Letter.PrintMarks); Assert.Equal(0, Marks());
            Assert.Contains("Ohne Falz- und Lochmarken", w.Find<TextBlock>("letter-msg").Text);
            w.Find<CheckBox>("letter-print-marks").IsChecked = true; Pump();
            Assert.True(w.Letter.PrintMarks); Assert.Equal(3, Marks());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Briefe F3: Rechtschreibung beim Tippen – unbekanntes Wort rot in der Vorschau, Klick auf den Vorschlag ersetzt es, „+“ merkt ein Wort; ohne Wörterbuch ehrlicher Hinweis")]
    public void Spelling_while_typing()
    {
        string? src = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null && src is null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Trendo.sln"))) src = Path.Combine(dir.FullName, "artifacts", "vollpaket", "staging", "libreoffice", "App", "libreoffice", "share", "extensions", "dict-de");
        var (w, _, root) = Ui.Open();
        try
        {
            if (src is null || !File.Exists(Path.Combine(src, "de_DE_frami.dic")))
            {
                Step3(w); Assert.Contains("Wörterbuch fehlt", string.Join("|", w.GetLogicalTexts())); return; // ohne Vollpaket nur der Hinweis
            }
            var dst = Path.Combine(root, "werkzeuge", "libreoffice", "App", "libreoffice", "share", "extensions", "dict-de"); Directory.CreateDirectory(dst);
            foreach (var ext in new[] { ".dic", ".aff" }) File.Copy(Path.Combine(src, "de_DE_frami" + ext), Path.Combine(dst, "de_DE_frami" + ext));
            Step3(w);
            Assert.NotNull(w.TryFind<TextBlock>("letter-spell-ok"));
            w.Find<TextBox>("letter-body").Text = "Hiermit kündige ich zum [Datumm]. Vielen Dankee für Ihre Mühe.";
            w.LetterSpellNow(); Pump();
            Assert.Contains("Dankee", w.LetterSpellingMistakes); Assert.DoesNotContain("Datumm", w.LetterSpellingMistakes);
            var body = w.Find<TextBlock>("letter-preview-body");
            Assert.True(body.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text == "Dankee" && r.TextDecorations is { Count: > 0 }), string.Join("|", body.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text + ":" + (r.TextDecorations?.Count ?? -1))) + " / " + body.Text);
            var fix = w.Find<Button>("letter-spell-fix-0");
            Assert.StartsWith("Dankee → ", (string)fix.Content!);
            var better = ((string)fix.Content!)["Dankee → ".Length..];
            fix.Press(); Pump();
            Assert.Contains($"Vielen {better} für", w.Letter.Body); Assert.Contains($"Vielen {better} für", w.Find<TextBox>("letter-body").Text);
            Assert.NotNull(w.TryFind<TextBlock>("letter-spell-ok"));

            w.Find<TextBox>("letter-body").Text = "Grüße vom Trendoteam."; w.LetterSpellNow(); Pump();
            Assert.Contains("Trendoteam", w.LetterSpellingMistakes);
            w.Find<Button>("letter-spell-own-0").Press(); Pump();
            Assert.Empty(w.LetterSpellingMistakes);
            Assert.Contains("Trendoteam", File.ReadAllText(Path.Combine(root, "text", "eigene-woerter.txt")));
        }
        finally { w.Close(); }
    }
}
