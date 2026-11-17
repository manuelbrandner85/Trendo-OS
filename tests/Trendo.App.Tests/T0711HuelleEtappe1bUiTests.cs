using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0711 (B6 Etappe 1b): die übrigen elf bearbeitbaren Dokumente ohne Autosave hängen über ProgramLink an Rückgängig und Notsicherung der Hülle.
/// Je Programm echt über die Oberfläche: ändern (tippen bzw. Knöpfe) → Rückgängig über mehrere Schritte und Wiederholen mit den Knöpfen →
/// Takt der Hülle → Fenster ohne Speichern schließen → neues Fenster: Wiederherstellen-Karte → Wiederherstellen bringt den Stand zurück.</summary>
public class T0711_HuelleEtappe1b_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    static void Type(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); Settle(); RealClick(w, Get<TextBox>(w, name)); tb = Get<TextBox>(w, name); tb.Focus(); tb.CaretIndex = tb.Text?.Length ?? 0; Settle(); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    /// <summary>T-0740: in den Texteditor (AvaloniaEdit) tippen – wie <see cref="Type"/> für Textfelder.</summary>
    static void TypeEd(MainWindow w, string name, string text) { var ed = Get<AvaloniaEdit.TextEditor>(w, name); ed.BringIntoView(); Settle(); RealClick(w, ed); ed = Get<AvaloniaEdit.TextEditor>(w, name); ed.TextArea.Focus(); ed.CaretOffset = ed.Document.TextLength; Settle(); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    static void Pause() { var until = DateTime.Now + MainWindow.LinkTypingDelay + TimeSpan.FromMilliseconds(150); while (DateTime.Now < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } Settle(); }

    static MainWindow Reopen(string root)
    {
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher()), new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Width = 1600; w.Height = 1100; w.Show(); Settle(); return w;
    }

    static string[] Backups(string root, string studio) { var d = Path.Combine(root, "programme", "sicherung"); return Directory.Exists(d) ? Directory.GetFiles(d, studio + "-*.tdoc") : []; }

    /// <summary>Der ganze Ablauf für ein Programm. <paramref name="open"/> führt zur Arbeitsfläche, <paramref name="step1"/>/<paramref name="step2"/> ändern über die Oberfläche,
    /// <paramref name="state"/> liest den Stand des Programms.</summary>
    static void Cycle(string page, string prefix, string studio, Action<MainWindow, string> open, Action<MainWindow> step1, Action<MainWindow> step2, Func<MainWindow, string> state,
        Action<MainWindow>? reopen = null, Action<MainWindow, string>? also = null)
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            open(w, root); Settle();
            var s0 = state(w);
            Assert.False(Get<Button>(w, prefix + "-undo").IsEnabled, page + ": Rückgängig ist am Anfang schon an");
            step1(w); Pause(); var s1 = state(w); Assert.NotEqual(s0, s1);
            step2(w); Pause(); var s2 = state(w); Assert.NotEqual(s1, s2);
            Press(w, prefix + "-undo"); Assert.Equal(s1, state(w));
            Press(w, prefix + "-undo"); Assert.Equal(s0, state(w)); // mehrere Schritte zurück
            Press(w, prefix + "-redo"); Assert.Equal(s1, state(w));
            Press(w, prefix + "-redo"); Assert.Equal(s2, state(w));
            w.ProgramAutosaveAll(); // Takt der Hülle (alle 20 s)
            var file = Assert.Single(Backups(root, studio));
            Assert.True(ProgramDocument.Readable(file));

            // zweites Fenster mit demselben Datenordner, solange das erste noch läuft: bietet die Sicherung NICHT an (Sperre)
            var other = Reopen(root);
            try { (reopen ?? (x => open(x, root)))(other); Settle(); Assert.False(Has(other, prefix + "-recovery"), page + ": zweites Fenster bietet die Sicherung des ersten an"); }
            finally { other.Close(); }

            w.Close(); // ohne Speichern
            var w2 = Reopen(root);
            try
            {
                (reopen ?? (x => open(x, root)))(w2); Settle();
                Assert.True(Has(w2, prefix + "-recovery"), page + ": keine Wiederherstellen-Karte");
                Press(w2, prefix + "-restore-0");
                Assert.Equal(s2, state(w2));
                Assert.False(Has(w2, prefix + "-recovery"), page + ": zurückgeholte Sicherung wird erneut angeboten");
                Assert.True(File.Exists(file)); // bleibt, bis gespeichert ist
                also?.Invoke(w2, file);
            }
            finally { if (w2.IsVisible) w2.Close(); }
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    static void MakePdf(string file, params double[] widths)
    {
        using var d = new PdfSharp.Pdf.PdfDocument();
        foreach (var x in widths) { var p = d.AddPage(); p.Width = PdfSharp.Drawing.XUnit.FromPoint(x); p.Height = PdfSharp.Drawing.XUnit.FromPoint(600); }
        d.Save(file);
    }

    [AvaloniaFact(DisplayName = "T-0711 E-Mail „Schreiben“: Entwurf – Rückgängig, Notsicherung, Sperre gegen zweites Fenster, Karte, Zurückholen; als .eml gespeichert räumt die Sicherung weg")]
    public void Mail_draft()
    {
        var keep = MainWindow.MailDraftFolder;
        Cycle("post", "mail", "LINK-MAIL", (w, _) => w.Show("post"),
            w => Type(w, "mail-subject", "Termin"), w => Type(w, "mail-body", "Hallo, passt Montag?"),
            w => w.MailDraftSubject + "|" + w.MailDraftBody,
            also: (w, file) =>
            {
                MainWindow.MailDraftFolder = Path.Combine(Path.GetTempPath(), "trendo-t0711-mail-" + Guid.NewGuid().ToString("N")[..8]);
                try
                {
                    Type(w, "mail-to", "team@example.org"); Press(w, "mail-open");
                    Assert.Single(Directory.GetFiles(MainWindow.MailDraftFolder, "*.eml"));
                    Assert.False(File.Exists(file)); // gespeichert → Sicherung weg
                }
                finally { MainWindow.MailDraftFolder = keep; }
            });
    }

    [AvaloniaFact(DisplayName = "T-0711 Editor der Übersicht: alle Reiter – Rückgängig ganzer Schritte, Notsicherung, Karte, Zurückholen; Speichern (Textdatei im eigenen Format) räumt weg")]
    public void Editor_tabs()
    {
        Cycle("editor", "ed", "LINK-EDITOR", (w, _) => w.Show("editor"),
            w => Type(w, "ed-text", "Milch"), w => Type(w, "ed-text", ", Brot"),
            w => w.EdDocs[0].Text + "|" + w.EdDocs[0].Dirty,
            also: (w, file) =>
            {
                var f = Path.Combine(Path.GetTempPath(), "trendo-t0711-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
                try
                {
                    var expect = w.EdDocs[0].Text; Assert.EndsWith(", Brot", expect);
                    w.EditorSave(f);
                    Assert.Equal(expect, File.ReadAllText(f));
                    Assert.False(File.Exists(file));
                }
                finally { File.Delete(f); }
            });
    }

    [AvaloniaFact(DisplayName = "T-0711 PDF „Seiten ordnen“: Rückgängig und jetzt auch Wiederholen über die Hülle, Notsicherung, Karte öffnet die PDF wieder im Ordnen-Modus")]
    public void Pdf_organize()
    {
        string? pdf = null;
        Cycle("pdf", "pdf-org", "LINK-PDF",
            (w, root) => { pdf = Path.Combine(root, "Vertrag.pdf"); MakePdf(pdf, 200, 300, 400); w.OpenPdf(pdf); w.Show("pdf"); Settle(); Press(w, "pdf-organize-start"); },
            w => { w.PdfOrgSelect(0); w.Show("pdf"); Press(w, "pdf-org-right"); },
            w => { w.PdfOrgSelect(2); w.Show("pdf"); Press(w, "pdf-org-delete"); },
            w => string.Join(",", w.PdfOrgPages.Select(p => $"{p.Page}:{p.Rotate}")),
            reopen: w => w.Show("pdf"),
            also: (w, file) =>
            {
                Assert.True(w.PdfOrganizing); Assert.Equal(pdf, w.PdfPath);
                Press(w, "pdf-org-save");
                Assert.False(File.Exists(file));
                Assert.Equal(2, Trendo.Core.Sys.PdfTools.Read(w.PdfPath!).PageSizes.Count);
            });
    }

    [AvaloniaFact(DisplayName = "T-0711 Trendo Musik: Liedbeschreibung – Rückgängig, Notsicherung, Karte, Zurückholen")]
    public void Music_inputs()
    {
        Cycle("musik-studio", "ms-input", "LINK-MUSIK", (w, _) => { w.MusicStudioProMode(false); w.Show("musik-studio"); w.MsPage("erstellen"); },
            w => Type(w, "ms-desc", "Sommerlied"), w => Type(w, "ms-desc", " mit Gitarre"),
            w => w.MsDescText);
    }

    [AvaloniaFact(DisplayName = "T-0711 Trendo Musik: Notenrolle – Rückgängig und Wiederholen über die Hülle, Notsicherung, Karte, Zurückholen; MIDI speichern räumt weg")]
    public void Music_roll()
    {
        Cycle("musik-studio", "ms-roll", "LINK-MUSIK-NOTEN", (w, _) => { w.Show("musik-studio"); w.MsPage("studio"); Settle(); w.Find<Button>("ms-mode-noten").Press(); Settle(); },
            w => { w.MsRollToggle(60, 0); w.Show("musik-studio"); }, w => Press(w, "ms-roll-up"),
            w => string.Join(",", w.MsRoll.Notes.Select(n => n.Pitch + "@" + n.Start)),
            also: (w, file) => { var f = w.MsRollSave("Test-T0711"); try { Assert.True(File.Exists(f)); Assert.False(File.Exists(file)); } finally { File.Delete(f); } });
    }

    [AvaloniaFact(DisplayName = "T-0711 Spiele-Werkstatt: Tempo und Level-Raster – Rückgängig, Notsicherung, Karte öffnet das Spiel mit dem Entwurf")]
    public void Game_workshop()
    {
        Cycle("spiele", "game", "LINK-SPIELE",
            (w, _) => { var dir = Directory.GetDirectories(w.GameFolder).FirstOrDefault() ?? GameStudio.CreateBrowserGame(w.GameFolder, "breakout", "Test"); w.GameEditing = dir; w.Show("spiele"); },
            w => { Get<NumericUpDown>(w, "game-speed").Value = 8; Settle(); }, w => Press(w, "game-cell-0-0"),
            w => w.GameDraft is { } d ? $"{d["speed"]}|{d["level"]![0]}" : "-",
            reopen: w => w.Show("spiele"),
            also: (w, _) => { Assert.NotNull(w.GameEditing); Assert.Equal(8, (int)Get<NumericUpDown>(w, "game-speed").Value!); });
    }

    [AvaloniaFact(DisplayName = "T-0711 Recherche: Notizfeld – bleibt beim Neuzeichnen, Rückgängig, Notsicherung, Karte, Zurückholen")]
    public void Research_note()
    {
        Cycle("recherche", "rs-note", "LINK-RECHERCHE", (w, _) => w.Show("recherche"),
            w => Type(w, "rs-note", "Quelle A"), w => Type(w, "rs-note", " und B"),
            w => { w.Show("recherche"); return Get<TextBox>(w, "rs-note").Text ?? ""; }); // Neuzeichnen: das Getippte bleibt stehen
    }

    [AvaloniaFact(DisplayName = "T-0711 Wissen: Seiten-Editor – Rückgängig, Notsicherung, Karte öffnet den Editor mit dem Entwurf")]
    public void Wiki_editor()
    {
        Cycle("ki-wissen", "wissen", "LINK-WISSEN", (w, _) => { w.Show("ki-wissen"); Press(w, "wissen-new"); },
            w => Type(w, "wissen-title", "Stadtwerke"), w => Type(w, "wissen-text", "Kundennummer 4711"),
            w => (w.WikiEditing ? Get<TextBox>(w, "wissen-title").Text + "|" + Get<TextBox>(w, "wissen-text").Text : "-"),
            reopen: w => w.Show("ki-wissen"),
            also: (w, file) => { Press(w, "wissen-add"); Assert.False(File.Exists(file)); Assert.NotNull(w.Wissen.ByTitle("Stadtwerke")); });
    }

    [AvaloniaFact(DisplayName = "T-0711 KI-Vorlagen: Vorlagen-Editor – Rückgängig, Notsicherung, Karte, Zurückholen; Speichern räumt weg")]
    public void Prompt_editor()
    {
        Cycle("ki-vorlagen", "pedit", "LINK-VORLAGEN", (w, _) => { w.Show("ki-vorlagen"); Press(w, "prompt-new"); },
            w => Type(w, "pedit-name", "Kurzfassung"), w => TypeEd(w, "pedit-text", "Fasse in 3 Stichpunkten zusammen: {{Text}}"), // T-0740: Anweisungstext im Texteditor
            w => Has(w, "pedit-name") ? Get<TextBox>(w, "pedit-name").Text + "|" + Get<AvaloniaEdit.TextEditor>(w, "pedit-text").Text : "-",
            reopen: w => w.Show("ki-vorlagen"),
            also: (w, file) => { Press(w, "pedit-save"); Assert.False(File.Exists(file)); Assert.Contains(w.Prompts.All(), t => t.Name == "Kurzfassung"); });
    }

    [AvaloniaFact(DisplayName = "T-0711 Skills: Skill-Editor – Rückgängig, Notsicherung, Karte, Zurückholen")]
    public void Skill_editor()
    {
        Cycle("ki-skills", "skill", "LINK-SKILLS", (w, _) => { w.Show("ki-skills"); Press(w, "skill-new"); },
            w => Type(w, "skill-name", "Hausstil"), w => Type(w, "skill-body-edit", "- Du statt Sie"),
            w => Has(w, "skill-name") ? Get<TextBox>(w, "skill-name").Text + "|" + Get<TextBox>(w, "skill-body-edit").Text : "-",
            reopen: w => w.Show("ki-skills"));
    }

    [AvaloniaFact(DisplayName = "T-0711 Sauberer Nachbau: Beschreibung – Rückgängig, Notsicherung, Karte, Zurückholen; Speichern (BESCHREIBUNG.md, atomar) räumt weg")]
    public void Cleanroom_spec()
    {
        Cycle("nachbau", "clean", "LINK-NACHBAU",
            (w, _) => { if (!Directory.EnumerateDirectories(w.CleanFolder).Any()) CleanRoom.Create(w.CleanFolder, "Rechner", "ein Taschenrechner"); w.Show("nachbau"); },
            w => Type(w, "clean-spec-text", "\nPlus und Minus."), w => Type(w, "clean-spec-text", "\nSpeicher-Taste."),
            w => Get<TextBox>(w, "clean-spec-text").Text ?? "",
            also: (w, file) =>
            {
                Press(w, "clean-spec-save"); Assert.False(File.Exists(file));
                var spec = Path.Combine(Directory.GetDirectories(w.CleanFolder)[0], "1-Beschreibung", "BESCHREIBUNG.md");
                Assert.EndsWith("Speicher-Taste.", File.ReadAllText(spec).TrimEnd());
            });
    }
}
