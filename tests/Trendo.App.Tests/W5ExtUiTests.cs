using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_X04_Ui
{
    [AvaloniaFact(DisplayName = "X-04 (UI): Beispiel installieren, im Baukasten öffnen, ausschalten sperrt Benutzen")]
    public void Use()
    {
        var (w, _, _) = Ui.Open();
        w.Show("erweiterungen");
        w.Find<Button>("ext-sample").Press();
        Assert.Empty(w.Exts.All()); // T-0241: erst nach der Rückfrage
        w.Find<Button>("ext-ask-yes").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Single(w.Exts.All());
        w.Find<Button>("ext-use-0").Press();
        Assert.Equal("baukasten", w.CurrentView);
        Assert.Equal("Visitenkarte", w.BuilderPage.Title);
        w.Show("erweiterungen");
        w.Find<ToggleSwitch>("ext-on-0").IsChecked = false; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(w.Find<Button>("ext-use-0").IsEnabled);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0241 (UI): Not-Stopp verhindert das Installieren; Entfernen fragt vorher; Abbrechen ändert nichts")]
    public void Halt_and_remove()
    {
        var (w, _, _) = Ui.Open();
        w.Show("erweiterungen");
        w.Core.Approvals.Halt(Trendo.Core.Principal.LocalOwner, "Test");
        w.Find<Button>("ext-sample").Press();
        w.Find<Button>("ext-ask-yes").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Empty(w.Exts.All());
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Not-Stopp"));
        w.Core.Approvals.Resume(Trendo.Core.Principal.LocalOwner);
        w.Find<Button>("ext-sample").Press();
        w.Find<Button>("ext-ask-yes").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Single(w.Exts.All());
        w.Find<Button>("ext-remove-0").Press();
        Assert.Single(w.Exts.All());
        w.Find<Button>("ext-ask-no").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Single(w.Exts.All());
        w.Find<Button>("ext-remove-0").Press();
        w.Find<Button>("ext-ask-yes").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Empty(w.Exts.All());
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "ext.remove");
        w.Close();
    }
}
