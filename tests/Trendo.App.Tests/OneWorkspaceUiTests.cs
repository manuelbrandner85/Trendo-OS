using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.App.Tests;

public class OneWorkspaceUiTests
{
    [AvaloniaFact(DisplayName = "Arbeitsplatz schreibt die Notiz und zeigt QA bestanden")]
    public void Blatt_in_der_Oberflaeche()
    {
        var (w, _, _) = Ui.Open();
        w.Width = 1400;
        w.Height = 900;
        w.Core.Projects.Create("umzug", "Umzug");
        w.Show("vorhaben");
        Dispatcher.UIThread.RunJobs();
        w.Find<Button>("auftrag-arbeitsplatz").Press();
        Assert.Equal("arbeitsplatz", w.CurrentView);
        var titel = w.Find<TextBox>("platz-titel");
        var text = w.Find<TextBox>("platz-text");
        titel.Text = "Umzug";
        text.Text = "Kartons beschriften und den Vertrag prüfen.";
        w.Find<Button>("platz-pruefen").Press();
        Assert.Equal("QA bestanden", w.Find<TextBlock>("platz-status").Text);
        Assert.Contains("blatt-v1.md", w.Find<TextBlock>("platz-pfad").Text);
        Assert.Contains("Bestanden", w.Find<TextBlock>("platz-pruefung").Text);
        Assert.Contains(ArbeitsplatzBlattAdapter.BoundaryHint, w.Find<TextBlock>("platz-vorschau").Text);
        Assert.False(w.Find<Button>("platz-freigabe").IsVisible);
        Assert.Empty(w.Core.Approvals.Pending());
        var body = w.Find<TextBox>("platz-inhalt");
        Assert.Contains("Vertrag prüfen", body.Text);
        body.Text = "Kartons beschriften, Vertrag prüfen, Schlüssel hinterlegen.";
        w.Find<Button>("platz-aendern").Press();
        Assert.Equal("QA bestanden", w.Find<TextBlock>("platz-status").Text);
        Assert.Contains("blatt-v2.md", w.Find<TextBlock>("platz-pfad").Text);
        Assert.Contains("Schlüssel hinterlegen", w.Core.Projects.ReadFile("umzug", "arbeitsplatz/blatt-v2.md"));
        SaveFrame(w, "arbeitsplatz-blatt.png");
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Briefstudio im Arbeitsplatz bleibt ohne Datei")]
    public void Brief_ohne_Datei()
    {
        var (w, _, _) = Ui.Open();
        w.Width = 1400;
        w.Height = 900;
        w.Core.Projects.Create("amt", "Amt");
        w.ShowWorkspace("STUDIO-LETTER-01");
        Dispatcher.UIThread.RunJobs();
        w.SubmitWorkspace("Amt", "Bitte um eine Frist.");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Nicht unterstützt", w.Find<TextBlock>("platz-status").Text);
        Assert.Contains("keine Aktion", w.Find<TextBlock>("platz-pruefung").Text);
        Assert.Equal("Noch keine Datei.", w.Find<TextBlock>("platz-pfad").Text);
        Assert.False(w.Find<Button>("platz-freigabe").IsVisible);
        Assert.DoesNotContain(Directory.EnumerateFiles(w.Core.Projects.ProjectFilesRoot("amt"), "*.*", SearchOption.AllDirectories), f => f.EndsWith(".docx") || f.EndsWith(".md"));
        SaveFrame(w, "arbeitsplatz-brief.png");
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Ohne Projekt bleibt der Arbeitsplatz bei der Rückfrage")]
    public void Ohne_Projekt_rueckfrage()
    {
        var (w, _, _) = Ui.Open();
        w.ShowWorkspace("X-11");
        Dispatcher.UIThread.RunJobs();
        w.Find<Button>("platz-pruefen").Press();
        Assert.Equal("Rückfrage", w.Find<TextBlock>("platz-status").Text);
        Assert.Contains("Projekt", w.Find<TextBlock>("platz-fragen").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Startbildschirm hat keinen Arbeitsplatz-Knopf")]
    public void Start_ohne_Arbeitsplatz()
    {
        var (w, _, _) = Ui.Open();
        w.Show("start");
        Dispatcher.UIThread.RunJobs();
        Assert.Null(w.TryFind<Button>("auftrag-arbeitsplatz"));
        Assert.Null(w.TryFind<TextBox>("platz-titel"));
        w.Close();
    }

    static void SaveFrame(MainWindow w, string name)
    {
        var dir = "/opt/cursor/artifacts";
        if (!Directory.Exists(dir)) return;
        for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
        var frame = w.CaptureRenderedFrame();
        if (frame is null) return;
        frame.Save(Path.Combine(dir, name));
    }
}
