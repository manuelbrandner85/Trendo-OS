using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0005: Not-Stopp-Knopf in der Kopfleiste und Pause-Taste; Freigabeseite zeigt Hinweis und Vorschau.</summary>
public class NotStopp_Ui
{
    [AvaloniaFact(DisplayName = "T-0005 (UI): Not-Stopp-Knopf hält an, Freigabe gesperrt mit Hinweis; Fortsetzen; Pause-Taste stoppt ebenfalls")]
    public async Task Knopf_und_taste()
    {
        var (w, _, _) = Ui.Open();
        var r = w.Core.Approvals.Request(Principal.LocalOwner, "test.aktion", "Testaktion", "ziel", preview: "Datei a.txt wird nach b.txt verschoben");
        w.Show("approvals");
        var pv = w.Find<Border>("approval-preview-" + r.Id);
        Assert.Contains(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(pv).OfType<SelectableTextBlock>(), t => (t.Text ?? "").Contains("a.txt"));
        await w.HaltAll("Test");
        Assert.True(w.Core.Approvals.Halted);
        w.Show("approvals");
        Assert.NotNull(w.TryFind<Border>("notstopp-banner"));
        Assert.Null(w.TryFind<Button>("btn-approve-" + r.Id)); // verworfen: nicht mehr offen
        Assert.True(w.Bench.Drive.Emergency);
        w.Find<Button>("btn-notstopp-fortsetzen").Press();
        Assert.False(w.Core.Approvals.Halted);
        Assert.Null(w.TryFind<Border>("notstopp-banner"));
        w.Key(PhysicalKey.Pause);
        Assert.True(w.Core.Approvals.Halted);
        w.Close();
    }
}
