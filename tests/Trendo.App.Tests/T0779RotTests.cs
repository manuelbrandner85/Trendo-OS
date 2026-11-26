using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0779 Rot-Grün: nur Namen und Kennungen, keine neuen Typen – übersetzt auch auf dem alten Stand (main 184fcbbfc) und ist dort rot.</summary>
public class T0779_Rot
{
    [AvaloniaFact(DisplayName = "T-0779 Rot-Grün: „Quiz erstellen“ und „Kurse erstellen“ sind Programme der Hülle in „Wissen & Lernen“ mit Vorlagen, Ausgaben und KI-Fachaktion")]
    public void Studios_exist()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (id, key, ki) in new[] { ("STUDIO-QUIZ-01", "quiz-programm", "quiz.fragen"), ("STUDIO-COURSE-01", "kurse-programm", "kurs.lektion") })
            {
                Assert.True(MainWindow.Programs.TryGetValue(key, out var spec), key + " fehlt");
                Assert.Equal(id, spec.Studio);
                Assert.Equal("Wissen & Lernen", StudioCatalog.For(id)?.Area);
                Assert.True(spec.Templates.Count >= 3); Assert.True(spec.Exports.Count >= 3);
                Assert.Contains(spec.KiActions, a => a.Def.Id == ki);
                Assert.Equal(key, w.PageForStudio(id));
            }
        }
        finally { w.Close(); }
    }
}
