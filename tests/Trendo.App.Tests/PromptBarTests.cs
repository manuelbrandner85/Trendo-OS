using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.App.Tests;

public class PromptBarTests
{
    [AvaloniaFact(DisplayName = "Briefseite zeigt die Auftragsleiste und meldet ehrlich, wenn nichts eingebaut ist")]
    public void Brief_leiste_ohne_erfundenes_Ergebnis()
    {
        var (w, _, _) = Ui.Open();
        w.Show("brief");
        Dispatcher.UIThread.RunJobs();
        var box = w.Find<TextBox>("auftrag-prompt");
        Assert.Equal("Was soll dieses Studio tun?", box.PlaceholderText);
        Assert.Equal("Noch nicht gestartet", w.Find<TextBlock>("auftrag-status").Text);
        Assert.Equal("Noch keine Vorschau.", w.Find<TextBlock>("auftrag-vorschau").Text);
        Assert.False(w.Find<Button>("auftrag-los").IsVisible);
        Assert.Contains("teilweise", w.Find<TextBlock>("auftrag-faehigkeit").Text);
        w.Find<Button>("auftrag-modus").Press();
        Assert.Equal("Einfach", w.Find<Button>("auftrag-modus").Content);
        Assert.Contains("Profi", w.Find<TextBlock>("auftrag-profi").Text);
        box.Text = "Schreibe einen Brief an das Amt.";
        w.Find<Button>("auftrag-pruefen").Press();
        for (var i = 0; i < 30; i++) Dispatcher.UIThread.RunJobs();
        Assert.Equal("Rückfrage", w.Find<TextBlock>("auftrag-status").Text);
        Assert.Contains("Projekt", w.Find<TextBlock>("auftrag-detail").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Briefseite zeigt den echten Brieftext in der Vorschau")]
    public void Brief_vorschau_zeigt_den_text()
    {
        var (w, _, _) = Ui.Open();
        w.Core.Projects.Create("briefprobe", "Briefprobe");
        w.WindowState = WindowState.Normal;
        w.Width = 1280;
        w.Height = 800;
        w.Show("brief");
        Dispatcher.UIThread.RunJobs();
        w.Find<TextBox>("auftrag-prompt").Text = """
            Absender: Anna Beispiel
            Strasse: Beispielweg 1
            Ort: 10115 Berlin
            Empfaenger: Amt für Beispiel
            Musterstraße 2
            10115 Berlin
            Betreff: Auskunft zu den Unterlagen
            Text: bitte senden sie mir die unterlagen zu.
            Datum: 2026-10-22
            """;
        w.Find<Button>("auftrag-pruefen").Press();
        Wait(w, () => w.Find<TextBlock>("auftrag-status").Text == "Plan steht");
        Assert.Contains("nichts geschrieben", w.Find<TextBlock>("auftrag-vorschau").Text);
        w.Find<Button>("auftrag-los").Press();
        Wait(w, () => w.Find<TextBlock>("auftrag-status").Text == "QA bestanden");
        Assert.Contains("Auskunft zu den Unterlagen", w.Find<TextBlock>("auftrag-vorschau").Text);
        var docx = Directory.GetFiles(w.Core.Projects.ProjectFilesRoot("briefprobe"), "*.docx", SearchOption.AllDirectories).Single();
        Assert.Contains("bitte senden sie mir die unterlagen zu.", Letters.ReadDocxText(docx));
        w.Close();
    }

    static void Wait(MainWindow w, Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(30);
        }
    }

    [AvaloniaFact(DisplayName = "Startbildschirm bekommt keine Auftragsleiste")]
    public void Start_ohne_Leiste()
    {
        var (w, _, _) = Ui.Open();
        w.Show("start");
        Dispatcher.UIThread.RunJobs();
        Assert.Null(w.TryFind<TextBox>("auftrag-prompt"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "3D-Seite zeigt die Auftragsleiste, ohne die Vorschau zu verkleinern")]
    public void Drei_d_leiste_bleibt_in_der_seite()
    {
        var (w, _, _) = Ui.Open();
        w.WindowState = WindowState.Normal;
        w.Width = 1366;
        w.Height = 768;
        w.Show("3d");
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(w.TryFind<TextBox>("auftrag-prompt"));
        w.Find<Button>("3d-add").Press();
        var img = w.Find<Image>("3d-preview-image");
        Assert.True(img.Bounds.Height >= 48, $"Vorschau nur {img.Bounds.Height} px hoch");
        w.Close();
    }

    [Fact(DisplayName = "Abdeckung und StudioCatalog nennen dieselben Kennungen, Titel und Bereiche")]
    public void Katalog_und_Abdeckung_stimmen()
    {
        Assert.Equal(StudioCatalog.ById.Count, CapabilityCatalog.Ids.Count);
        foreach (var (id, info) in StudioCatalog.ById)
        {
            var profile = CapabilityCatalog.Find(id);
            Assert.NotNull(profile);
            Assert.Equal(info.Name, profile.Title);
            Assert.Equal(info.Area, profile.Area);
        }
        Assert.Equal(54, StudioCatalog.ById.Count);
    }
}
