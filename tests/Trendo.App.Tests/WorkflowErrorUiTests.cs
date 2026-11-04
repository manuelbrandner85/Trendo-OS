using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Prüfung 04.11.2026: Fehler im Zeitplan der Abläufe wurden stumm verschluckt – jetzt Protokoll und sichtbarer Hinweis.</summary>
public class WorkflowError_Ui
{
    [AvaloniaFact(DisplayName = "Abläufe: Fehler im Zeitplan wird angezeigt und protokolliert, der nächste fehlerfreie Lauf nimmt den Hinweis wieder weg")]
    public async Task Schedule_error_visible()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show("ablaeufe");
            w.WfDueOverride = () => throw new InvalidOperationException("Zielordner fehlt");
            await w.WfTickAsync();
            Assert.Contains("Zielordner fehlt", w.LastWfError);
            Assert.Contains("Zielordner fehlt", w.Find<TextBlock>("wf-error").Text);
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "ablauf.zeitplan.fehler");
            w.WfDueOverride = () => Task.FromResult<IReadOnlyList<Trendo.Core.WfRun>>([]);
            await w.WfTickAsync(); w.Show("ablaeufe");
            Assert.Null(w.LastWfError);
            Assert.Null(w.TryFind<TextBlock>("wf-error"));
        }
        finally { w.Close(); }
    }
}
