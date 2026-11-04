using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Code-Editor (selbst nachgebaut statt VSCodium, 04.11.2026): Ordner öffnen, Baum, Reiter, tippen, Strg+S, Suche, neue Datei, Testmodus startet nichts.</summary>
public class CodeEditor_Ui
{
    [AvaloniaFact(DisplayName = "Code-Editor UI: Projekt öffnen, Datei im Baum anklicken, tippen (●), Strg+S speichert, Suche springt zur Zeile, neue Datei, Ausführen startet im Testmodus nichts")]
    public void Edit_code()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(Path.Combine(proj, "src"));
        File.WriteAllText(Path.Combine(proj, "main.py"), "print('Hallo')\n");
        File.WriteAllText(Path.Combine(proj, "src", "rechnen.py"), "def plus(a, b):\n    return a + b\n# Suchwort hier\n");
        try
        {
            w.PickOverride = _ => Task.FromResult<string[]?>([proj]);
            w.Show("code"); Settle();
            RealClick(w, V<Button>(w, "ce-open-folder")); Settle(); Settle();
            Assert.Equal(proj, w.CodeRoot);
            Assert.EndsWith("main.py", w.CodeActiveFile); // Einstiegsdatei geht von selbst auf
            Assert.Equal("Python · UTF-8", w.Find<TextBlock>("ce-lang").Text);
            // Ordner src aufklappen und Datei öffnen
            RealClick(w, V<Button>(w, "ce-node-0")); Settle();
            RealClick(w, V<Button>(w, "ce-node-1")); Settle();
            Assert.EndsWith("rechnen.py", w.CodeActiveFile); Assert.Equal(2, w.CodeOpenFiles.Count);
            Assert.NotNull(w.CodeEditorForTests!.SyntaxHighlighting);
            // tippen: Reiter zeigt ●, Strg+S speichert
            var ed = w.CodeEditorForTests!; ed.Focus(); ed.TextArea.Caret.Offset = ed.Document.TextLength; Settle();
            ed.Document.Insert(ed.Document.TextLength, "print(plus(2, 3))\n"); Settle();
            Assert.True(w.CodeDirty(w.CodeActiveFile!));
            ed.TextArea.Focus(); w.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, "s"); Settle();
            Assert.False(w.CodeDirty(w.CodeActiveFile!));
            Assert.Contains("print(plus(2, 3))", File.ReadAllText(Path.Combine(proj, "src", "rechnen.py")));
            // Schließen mit Änderung wird abgefangen
            ed.Document.Insert(0, "# x\n"); Settle();
            Assert.Throws<InvalidOperationException>(() => w.CodeClose(1));
            w.CodeSave(); w.CodeClose(1); Assert.Single(w.CodeOpenFiles);
            // Suche im Projekt, Treffer springt zur Zeile
            w.Show("code"); Settle(); RealClick(w, V<Button>(w, "ce-panel-suche")); Settle();
            var q = w.Find<TextBox>("ce-search"); q.Text = "suchwort"; q.Focus(); w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, ""); Settle();
            RealClick(w, V<Button>(w, "ce-hit-0")); Settle();
            Assert.EndsWith("rechnen.py", w.CodeActiveFile); Assert.Equal(4, w.CodeEditorForTests!.TextArea.Caret.Line); // eine Zeile kam oben dazu
            // neue Datei, nur im Projekt
            w.CodeNewFile("src/neu.js"); Assert.True(File.Exists(Path.Combine(proj, "src", "neu.js")));
            Assert.Throws<InvalidOperationException>(() => w.CodeNewFile("../weg.js"));
            // Ausführen im Testmodus: nichts gestartet
            w.Show("code"); Settle(); RealClick(w, V<Button>(w, "ce-run")); Settle();
            Assert.Contains("Testmodus", w.Find<SelectableTextBlock>("ce-output").Text);
        }
        finally { w.Close(); }
    }
}
