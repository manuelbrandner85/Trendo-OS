using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Text;

namespace Trendo.App;

/// <summary>V-12 (T-0700, freigegeben 16.11.2026): Knöpfe „Überschrift“, „Fett“, „Liste“, „Verweis“, „Platzhalter“ über Schreibfeldern, die Markdown-Zeichen verstehen
/// (Bücher schreiben, Wissen für die KI, KI-Vorlagen). Niemand muss „#“, „**“, „[[…]]“ oder „{{…}}“ lernen; Getipptes geht weiter wie bisher.
/// Gemeinsamer Baustein – das Feld behält Namen, Speichern und Rückgängig (Strg+Z des Feldes).</summary>
public partial class MainWindow
{
    /// <summary>Leiste über <paramref name="box"/>; Knopfnamen „{prefix}-mark-{art}“.</summary>
    public Control WriteMarksBar(TextBox box, string prefix, params (WriteMarks.Kind Kind, string Label, string Tip)[] marks)
    {
        var bar = new WrapPanel { Name = prefix + "-marks", Margin = new Thickness(0, 0, 0, 6) };
        foreach (var (kind, label, tip) in marks)
        {
            var b = new Button { Name = $"{prefix}-mark-{kind.ToString().ToLowerInvariant()}", Content = label, FontSize = 12.5, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 0), Focusable = false,
                Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), Foreground = Text };
            ToolTip.SetTip(b, tip);
            var k = kind;
            b.Click += (_, _) => ApplyWriteMark(box, k);
            bar.Children.Add(b);
        }
        return bar;
    }

    /// <summary>Wendet die Auszeichnung auf die Markierung des Feldes an; das Feld meldet die Änderung wie beim Tippen (Speichern, Rückgängig).</summary>
    public static void ApplyWriteMark(TextBox box, WriteMarks.Kind kind)
    {
        var r = WriteMarks.Apply(box.Text ?? "", box.SelectionStart, box.SelectionEnd, kind);
        box.Text = r.Text;
        box.SelectionStart = r.SelStart; box.SelectionEnd = r.SelEnd;
        box.Focus();
    }

    /// <summary>Feld mit Leiste darüber (für Seiten, die das Feld direkt einhängen).</summary>
    Control WithWriteMarks(TextBox box, string prefix, params (WriteMarks.Kind Kind, string Label, string Tip)[] marks)
    {
        var bar = WriteMarksBar(box, prefix, marks);
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        return new DockPanel { Name = prefix + "-marks-host", Children = { bar, box } };
    }
}
