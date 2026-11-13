using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Notizbuch-Ausbau: Lese-Ansicht mit abhakbaren Checklisten, Verknüpfungen zwischen Seiten (fehlende anlegen), „Verweist hierher“, Bilder, PDF.</summary>
public class NotebookPlus_Ui
{
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }

    [Fact(DisplayName = "Notiz-Text: Titel, Schlagworte, Verknüpfungen, Checkliste zählen und umschalten, Verweise, PDF-Dokument")]
    public void Text_rules()
    {
        const string t = "# Einkauf\n- [ ] Milch\n- [x] Brot #haushalt\nSiehe [[Wochenplan]] und [[Rezepte]]";
        Assert.Equal("Einkauf", NoteText.Title(t)); Assert.Equal(["haushalt"], NoteText.Tags(t)); Assert.Equal(["Wochenplan", "Rezepte"], NoteText.Links(t));
        Assert.Equal((1, 2), NoteText.Checklist(t)); Assert.Equal((2, 2), NoteText.Checklist(NoteText.Toggle(t, 1))); Assert.Equal(t, NoteText.Toggle(t, 3));
        var back = NoteText.Backlinks([("A", "Einkauf", t), ("A", "Wochenplan", "Montag"), ("B", "Rezepte", "[[Wochenplan]]")], "Wochenplan");
        Assert.Equal([("A", "Einkauf"), ("B", "Rezepte")], back);
        var doc = NoteText.ToDoc("Einkauf", "- [ ] Milch\n[[Wochenplan]]"); Assert.Contains(doc.Paras, p => p.Text == "Milch"); Assert.Contains(doc.Paras, p => p.Text == "Wochenplan");
    }

    [AvaloniaFact(DisplayName = "Notizbuch UI: Checkliste einfügen, in der Lese-Ansicht abhaken (gespeichert), Verknüpfung legt fehlende Seite an, Verweist hierher, Als PDF")]
    public void Read_check_link()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var nb = w.Notizbuch; nb.AddSection("Allgemein"); nb.Write("Allgemein", "Einkauf", "- [ ] Milch\n- [ ] Brot\nWeiter im [[Wochenplan]]");
        w.NotebookOpen = ("Allgemein", "Einkauf"); w.NoteReadMode = true; w.Show("notizbuch"); Settle();
        Assert.NotNull(w.TryFind<StackPanel>("nb-reader"));
        var cb = V<CheckBox>(w, "nb-check-0"); cb.BringIntoView(); Settle(); RealClick(w, V<CheckBox>(w, "nb-check-0"));
        Assert.StartsWith("- [x] Milch", nb.Read("Allgemein", "Einkauf"));
        Assert.Equal(1, V<ProgressBar>(w, "nb-progress").Value);
        // Verknüpfung auf eine Seite, die es noch nicht gibt → wird angelegt und geöffnet
        Click(w, "nb-goto-2-0");
        Assert.True(nb.Exists("Allgemein", "Wochenplan")); Assert.Equal(("Allgemein", "Wochenplan"), w.NotebookOpen);
        Assert.Contains("Einkauf", string.Join(" ", w.Find<StackPanel>("nb-backlinks").Children.OfType<Button>().Select(b => b.Content as string)));
        // Zurück über „Verweist hierher“
        Click(w, "nb-back-0"); Assert.Equal(("Allgemein", "Einkauf"), w.NotebookOpen);
        // Als PDF
        Click(w, "nb-pdf");
        Assert.Single(Directory.GetFiles(root, "Einkauf.pdf", SearchOption.AllDirectories));
    }
}
