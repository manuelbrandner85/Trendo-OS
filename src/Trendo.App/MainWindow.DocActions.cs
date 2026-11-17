using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Trendo.App;

/// <summary>V-14 (T-0700, freigegeben 16.11.2026): Speichern, Rückgängig, Wiederholen und Exportieren stehen in jedem Programm am selben Platz –
/// links oben, am Anfang der ersten Werkzeugzeile, in dieser Reihenfolge (wie das Menüband der Programm-Hülle: „Speichern“, „Rückgängig“ …).
/// KI („KI ▾“) steht in jedem Programm rechts oben in der KI-Leiste, Hilfe („?“) rechts im Seitenkopf. Programme außerhalb der Hülle (Text, Geld, Briefe …)
/// geben ihre vorhandenen Knöpfe hier hinein – Namen, Klick und Hinweise bleiben, nur der Platz ist überall gleich.</summary>
public partial class MainWindow
{
    /// <summary>Name der gemeinsamen Gruppe (für Tests und Claude).</summary>
    public const string DocActionsName = "doc-actions";

    /// <summary>Gemeinsamer Baustein: Gruppe aus vorhandenen Knöpfen in fester Reihenfolge Speichern · Rückgängig · Wiederholen · Exportieren (fehlende werden übersprungen).</summary>
    public static StackPanel DocActions(Control? save, Control? undo, Control? redo, Control? export = null)
    {
        var sp = new StackPanel { Name = DocActionsName, Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 4) };
        foreach (var c in new[] { save, undo, redo, export })
        {
            if (c is null) continue;
            if (c.Parent is Panel p) p.Children.Remove(c);
            c.Margin = new Thickness(0);
            sp.Children.Add(c);
        }
        return sp;
    }

    /// <summary>T-0709 (V-14): schlanker Knopf für die Gruppe <see cref="DocActions"/> in Programmen mit eigenem Menüband (Tabelle, Präsentation) –
    /// gleiche Größe wie in Trendo Text; <paramref name="gold"/> nur für „Speichern“ (die eine goldene Fläche des Programms).</summary>
    private Button DocBtn(string name, string label, Action go, string tip, string page, bool gold = false)
    {
        var b = new Button { Name = name, Content = label, Focusable = false, Padding = new Thickness(9, 4), MinWidth = 30, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (gold) b.Background = Accent;
        ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => { go(); Show(page); };
        return b;
    }
}
