using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0708 (B6 „Programm-Hülle für alle“, Etappe 1): Programme außerhalb der Hülle an deren Rückgängig- und Notsicherungssystem anschließen
/// (<see cref="ProgramLink"/>, kein zweites System). Die Notsicherung schreibt der Takt der Hülle (<see cref="ProgramAutosaveEvery"/>, 20 s) mit;
/// beim nächsten Öffnen zeigt das Programm eine Wiederherstellen-Karte. Erstes angeschlossenes Programm: „Briefe schreiben“ (bisher ohne Autosave und ohne Rückgängig).</summary>
public partial class MainWindow
{
    /// <summary>Tippen wird nach dieser Pause zu einem Rückgängig-Schritt zusammengefasst (und gilt ab dann für die Notsicherung).</summary>
    public static TimeSpan LinkTypingDelay { get; set; } = TimeSpan.FromSeconds(1);

    ProgramLink? _letterLink;
    DispatcherTimer? _letterTyping;
    string _letterRecoverMsg = "";

    /// <summary>Anschluss von „Briefe schreiben“ an Rückgängig und Notsicherung der Hülle.</summary>
    public ProgramLink LetterLink => _letterLink ??= new ProgramLink(LetterStudio, "brief", LetterState(), "Brief") { Locks = ProgLocks }; // T-0711: Sperre gegen ein zweites Fenster

    JsonNode LetterState() => JsonSerializer.SerializeToNode(Letter, LetterJsonOpts)!;
    static bool LetterWorth(LetterDraft d) => !string.IsNullOrWhiteSpace(d.Subject) || !string.IsNullOrWhiteSpace(d.Body) || !string.IsNullOrWhiteSpace(d.Recipient);

    /// <summary>Jetzigen Stand des Briefs als Schritt übernehmen (beim Neuaufbau der Seite nach jeder Aktion und nach einer Tipp-Pause).</summary>
    public bool LetterCommit(string label = "Brief geändert")
    {
        _letterTyping?.Stop();
        var title = Letter.Subject.Trim(); if (title.Length > 0) LetterLink.Doc.Title = title.Length > 60 ? title[..60] : title;
        return LetterLink.Commit(label, LetterState());
    }

    /// <summary>Ein Feld wurde getippt: nach <see cref="LinkTypingDelay"/> Pause wird daraus ein Rückgängig-Schritt.</summary>
    void LetterTyped()
    {
        if (_letterTyping is null) { _letterTyping = new DispatcherTimer { Interval = LinkTypingDelay }; _letterTyping.Tick += (_, _) => { LetterCommit("Text geschrieben"); LetterUndoRefresh(); }; }
        _letterTyping.Stop(); _letterTyping.Interval = LinkTypingDelay; _letterTyping.Start();
    }

    /// <summary>Rückgängig im Brief (bis 100 Schritte, wie die Hülle).</summary>
    public bool LetterUndo()
    {
        LetterCommit("Text geschrieben");
        if (LetterLink.Undo() is not { } s) return false;
        ApplyLetter(s); return true;
    }

    public bool LetterRedo()
    {
        LetterCommit("Text geschrieben");
        if (LetterLink.Redo() is not { } s) return false;
        ApplyLetter(s); return true;
    }

    void ApplyLetter(JsonNode s)
    {
        if (s.Deserialize<LetterDraft>(LetterJsonOpts) is not { } d) return;
        _letter = d; _letterSaved = false; _letterSerial = null;
    }

    /// <summary>Ein gespeicherter Brief wurde zum Weiterschreiben geöffnet: neuer Verlauf, gilt als gespeichert.</summary>
    void LetterOpened() { _letterTyping?.Stop(); LetterLink.Reset(LetterState(), "Brief", ProgBackup); }

    /// <summary>Nach dem Speichern im Briefe-Ordner: Notsicherung überflüssig.</summary>
    void LetterSavedNow() { LetterCommit(); LetterLink.Saved(ProgBackup); }

    Action? _letterUndoRefresh;
    void LetterUndoRefresh() => _letterUndoRefresh?.Invoke();

    /// <summary>Takt der Hülle: angeschlossene Programme sichern ungespeicherte Änderungen (atomar, programme/sicherung).</summary>
    void LinksAutosave()
    {
        if (_letterLink is not null)
            try
            {
                if (_letterTyping?.IsEnabled == true) LetterCommit("Text geschrieben");
                _letterLink.Autosave(ProgBackup, LetterWorth(Letter));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        SlotsAutosave(); // T-0711: die übrigen angeschlossenen Programme
        OfficeLinksAutosave(); // T-0714: Text, Tabelle, Präsentation, CAD, 3D (vorher eigener Weg „wiederherstellen/“)
        WebDesignAutosave(); // T-0716: Entwurf des visuellen Editors (Web Design Studio)
    }

    /// <summary>Gesicherte, nicht gespeicherte Briefe (neueste zuerst) – für die Wiederherstellen-Karte.</summary>
    public List<(string File, string Title, DateTime When)> LetterRecoveries() => LetterLink.Pending(ProgBackup);

    /// <summary>Gesicherten Brief zurückholen (als ungespeichert, Rückgängig nach dem Zurückholen leer) oder die Sicherung verwerfen.</summary>
    public void LetterRecover(int index, bool restore)
    {
        var all = LetterRecoveries(); if (index < 0 || index >= all.Count) return;
        var f = all[index].File;
        if (!restore) { ProgramLink.Discard(f, ProgLocks); _letterRecoverMsg = "Sicherung verworfen."; return; }
        LetterLink.Autosave(ProgBackup, LetterWorth(Letter)); // der offene Brief geht nicht verloren – er bleibt als Sicherung zum Zurückholen
        if (LetterLink.Restore(f, ProgBackup) is { } s) ApplyLetter(s);
        _letterStep = 3; _letterGallery = false;
        _letterRecoverMsg = "Brief wiederhergestellt – bitte speichern.";
        _core.Audit.Append(Principal.LocalOwner.Name, "letter.recover", all[index].Title);
    }

    /// <summary>Wiederherstellen-Karte über dem Brief: nicht gespeicherte Briefe aus einer früheren Sitzung oder einem Absturz.</summary>
    Control? LetterRecoveryCard()
    {
        List<(string File, string Title, DateTime When)> all;
        try { all = LetterRecoveries(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        if (all.Count == 0) return null;
        var rows = new StackPanel { Spacing = 6 };
        rows.Children.Add(new TextBlock { Text = all.Count == 1 ? "Ein Brief wurde nicht gespeichert, aber automatisch gesichert." : $"{all.Count} Briefe wurden nicht gespeichert, aber automatisch gesichert.", Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        for (var i = 0; i < Math.Min(3, all.Count); i++)
        {
            var k = i; var o = all[i];
            var yes = new Button { Name = $"letter-restore-{k}", Content = "Wiederherstellen", Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 0) };
            yes.Click += (_, _) => { try { LetterRecover(k, true); } catch (Exception ex) when (ex is InvalidDataException or IOException) { _letterRecoverMsg = "Zurückholen ging nicht: " + FriendlyError.Say(ex); } Show("brief"); };
            var no = new Button { Name = $"letter-discard-{k}", Content = "Verwerfen", Padding = new Thickness(12, 4) };
            no.Click += (_, _) => { LetterRecover(k, false); Show("brief"); };
            rows.Children.Add(new DockPanel { Children = {
                new StackPanel { Orientation = Orientation.Horizontal, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Children = { yes, no } },
                new TextBlock { Text = $"„{o.Title}“ · gesichert {o.When:dd.MM.yyyy, HH:mm} Uhr", Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center } } });
        }
        return new Border { Name = "letter-recovery", Background = Brush.Parse("#1AF5A524"), BorderBrush = Brush.Parse("#66F5A524"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 0, 8), Child = rows };
    }
}
