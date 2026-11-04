using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Projekte als Projektmappe (04.11.2026): anlegen per Name, wählen, Dateien hinzufügen, Ordner, Notiz, Umbenennen.</summary>
public class Projektmappe_Ui
{
    [AvaloniaFact(DisplayName = "Projektmappe UI: Projekt mit lesbarem Namen anlegen, Dateien hinzufügen (Auswahl ersetzt), Ordner anlegen, Notiz bleibt, Umbenennen, zweites Projekt wählen")]
    public void Projects()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show("projects"); Settle();
            Assert.NotNull(w.TryFind<Grid>("proj-workspace"));
            w.Find<TextBox>("txt-project-id").Text = "Umzug Sommer";
            RealClick(w, V<Button>(w, "btn-create-project")); Settle();
            Assert.Equal("umzug-sommer", w.ProjectSelected);
            var f1 = Path.Combine(root, "plan.pdf"); File.WriteAllBytes(f1, new byte[2048]); var f2 = Path.Combine(root, "kisten.xlsx"); File.WriteAllBytes(f2, new byte[100]);
            w.PickOverride = _ => Task.FromResult<string[]?>([f1, f2]);
            RealClick(w, V<Button>(w, "proj-add-file")); Settle(); Settle();
            Assert.Contains("2 Dateien", w.ProjectMessage);
            Assert.NotNull(w.TryFind<Grid>("proj-file-1"));
            w.Find<TextBox>("proj-newdir-name").Text = "Rechnungen"; RealClick(w, V<Button>(w, "proj-new-folder")); Settle();
            Assert.True(Directory.Exists(Path.Combine(w.Core.Projects.ProjectFilesRoot("umzug-sommer"), "Rechnungen")));
            w.Core.Projects.SetNote("umzug-sommer", "Kartons bestellen"); w.Show("projects"); Settle();
            Assert.Equal("Kartons bestellen", w.Find<TextBox>("proj-note").Text);
            w.Find<TextBox>("proj-rename").Text = "Umzug 2026"; RealClick(w, V<Button>(w, "proj-rename-go")); Settle();
            Assert.Equal("Umzug 2026", w.Core.Projects.List().Single(p => p.Id == "umzug-sommer").Name);
            w.Core.Projects.Create("garten", "Garten"); w.Show("projects"); Settle();
            RealClick(w, V<Button>(w, "proj-item-garten")); Settle();
            Assert.Equal("garten", w.ProjectSelected); Assert.NotNull(w.TryFind<Button>("btn-open-folder-garten"));
        }
        finally { w.Close(); }
    }
}
