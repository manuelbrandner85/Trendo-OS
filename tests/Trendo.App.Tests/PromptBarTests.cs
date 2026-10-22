using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
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
        Assert.Contains("nicht eingebaut", w.Find<TextBlock>("auftrag-faehigkeit").Text);
        box.Text = "Schreibe einen Brief an das Amt.";
        w.Find<Button>("auftrag-pruefen").Press();
        for (var i = 0; i < 30; i++) Dispatcher.UIThread.RunJobs();
        Assert.Equal("Rückfrage", w.Find<TextBlock>("auftrag-status").Text);
        Assert.Contains("Projekt", w.Find<TextBlock>("auftrag-detail").Text);
        w.Close();
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
