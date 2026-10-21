using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.App.Tests;

public class Sys10_Office_Ui
{
    [AvaloniaFact(DisplayName = "SYS-10 (UI): Tabs; Aufgabe anlegen/erledigen; Tabelle anzeigen; Text zählen; Drucken nur nach Bestätigung")]
    public void Pages()
    {
        var (w, l, root) = Ui.Open();
        foreach (var k in new[] { "doku-pdf", "doku-schreiben", "doku-ordnen" }) { w.Show(k); Assert.Equal(k, w.CurrentView); }
        w.Show("doku-ordnen");
        w.Find<TextBox>("task-title").Text = "Steuer machen"; w.Find<TextBox>("task-due").Text = "01.01.2020"; w.Find<Button>("task-add").Press();
        Assert.Single(w.Tasks.Overdue(DateTime.Now)); w.Find<Button>("task-done-0").Press(); Assert.Empty(w.Tasks.Overdue(DateTime.Now));
        var csv = Path.Combine(root, "t.csv"); File.WriteAllText(csv, "A;B\n1;2\n");
        w.Find<TextBox>("table-file").Text = csv; w.Find<Button>("table-open").Press(); Assert.Equal(3, w.Find<StackPanel>("table-view").Children.Count);
        w.Show("doku-schreiben"); w.Find<TextBox>("text-input").Text = "Ein Satz. Noch einer."; w.Find<Button>("text-stats").Press();
        Assert.StartsWith("4 Wörter · 2 Sätze", ((TextBlock)w.Find<StackPanel>("text-result").Children[0]).Text);
        w.Show("doku-pdf"); var pdf = Path.Combine(root, "x.pdf"); File.WriteAllText(pdf, "%PDF-1.4");
        w.Find<TextBox>("print-file").Text = pdf; w.Find<Button>("print-ask").Press();
        Assert.NotNull(w.TryFind<Button>("print-yes")); w.Find<Button>("print-no").Press(); Assert.Null(w.TryFind<Button>("print-yes"));
        Assert.DoesNotContain(new Trendo.Core.AuditLog(Path.Combine(root, "audit", "audit.jsonl")).ReadAll(), e => e.Action == "print");
        w.Close();
    }
}
