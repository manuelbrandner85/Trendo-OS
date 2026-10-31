using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>T-0254 (100): Text, Tabelle und Präsentation sichern ungespeicherte Änderungen automatisch (mit dem Zeitgeber der Programme, alle 30 s).
/// Nach einem Absturz oder Schließen ohne Speichern bietet das Programm „Wiederherstellen“ an. Gespeichert wird im eigenen Datenordner,
/// nie über die Originaldatei; nach dem Speichern oder Verwerfen wird die Sicherung gelöscht.</summary>
public partial class MainWindow
{
    string RecoveryDir => Path.Combine(_core.DataRoot, "wiederherstellen");
    string RecoveryFile(string kind) => Path.Combine(RecoveryDir, kind switch { "text" => "text.docx", "tabelle" => "tabelle.xlsx", _ => "praesentation.pptx" });

    /// <summary>Ungespeicherte Dokumente sichern (öffentlich für Tests).</summary>
    public void OfficeAutosave()
    {
        void Try(Action a) { try { Directory.CreateDirectory(RecoveryDir); a(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { } }
        if (TextDirty) Try(() => TextFiles.Save(TextDocument, RecoveryFile("text")));
        if (SheetDirty) Try(() => SheetFiles.Save(SheetDocument, RecoveryFile("tabelle")));
        if (DeckDirty) Try(() => SlideFiles.Save(Deck, RecoveryFile("praesentation")));
    }

    /// <summary>Nach dem Speichern: Sicherung ist überflüssig.</summary>
    void RecoveryClear(string kind) { try { if (File.Exists(RecoveryFile(kind))) File.Delete(RecoveryFile(kind)); } catch (IOException) { } }

    public bool RecoveryAvailable(string kind) => File.Exists(RecoveryFile(kind));

    /// <summary>Gesicherte Fassung laden (nicht als gespeichert markiert – bitte speichern) bzw. verwerfen.</summary>
    public void RecoveryDecide(string kind, bool restore)
    {
        var f = RecoveryFile(kind);
        if (!File.Exists(f)) return;
        if (restore)
        {
            switch (kind)
            {
                case "text": TextDocument = TextFiles.Open(f); TextPath = null; TextDirty = true; TextFocusBlock = 0; _txSel = _txSelLen = 0; _txUndo.Clear(); _txRedo.Clear(); _txMsg = "Wiederhergestellt – bitte speichern."; break;
                case "tabelle": SheetOpen(f); SheetPath = null; SheetDirty = true; break;
                default: DeckOpen(f); DeckPath = null; DeckDirty = true; break;
            }
            _core.Audit.Append(Principal.LocalOwner.Name, "office.recover", kind);
        }
        RecoveryClear(kind);
    }

    /// <summary>Hinweis mit „Wiederherstellen“ / „Verwerfen“, solange eine Sicherung da ist und nichts Neues ungespeichert ist.</summary>
    Control? RecoveryBanner(string kind, bool dirty)
    {
        if (dirty || !RecoveryAvailable(kind)) return null;
        var at = File.GetLastWriteTime(RecoveryFile(kind));
        var yes = new Button { Name = $"office-restore-{kind}", Content = "Wiederherstellen", Background = Accent, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 0), Focusable = false };
        yes.Click += (_, _) => { RecoveryDecide(kind, true); Show(kind); };
        var no = new Button { Name = $"office-discard-{kind}", Content = "Verwerfen", Padding = new Thickness(12, 4), Focusable = false };
        no.Click += (_, _) => { RecoveryDecide(kind, false); Show(kind); };
        return new Border { Name = $"office-recovery-{kind}", Background = Brush.Parse("#1AF5A524"), BorderBrush = Brush.Parse("#66F5A524"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 0, 8),
            Child = new DockPanel { Children = {
                new StackPanel { Orientation = Orientation.Horizontal, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Children = { yes, no } },
                new TextBlock { Text = $"Es gibt nicht gespeicherte Änderungen vom {at:dd.MM.yyyy, HH:mm} Uhr, die automatisch gesichert wurden.", Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center } } } };
    }

    /// <summary>Hinweis über eine Programmseite legen, ohne deren Aufbau zu ändern.</summary>
    Control WithRecovery(string kind, bool dirty, Control page) =>
        RecoveryBanner(kind, dirty) is { } b ? new DockPanel { Children = { new ContentControl { Content = b, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Top }, page } } : page;
}
