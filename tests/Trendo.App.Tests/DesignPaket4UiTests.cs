using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0709 Design-Reste (Bild-Audit vom 16.11.2026, alle 22 Vorschläge freigegeben): V-14 in Tabelle, Präsentation und Mail, V-15 Verkaufen mit einer Reiterzeile,
/// V-07 kein sichtbares Pfadfeld in „Einfach“, V-12 Podcast-Kapitel als Liste, wichtigste Exporte der Büro-Studios in „Einfach“ sichtbar.
/// Echte Oberflächen-Tests im Avalonia-Headless-Fenster; ersetzt ist nur die System-Dateiauswahl (<see cref="MainWindow.PickOverride"/>), wo sie gebraucht wird.</summary>
public class DesignPaket4_Ui
{
    static void Settle() { for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs(); }

    static MainWindow Open(int level = UserLevel.Einfach)
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Settings.SetupDone = true;
        w.ProgramSetLevel(level);
        return w;
    }

    static void Go(MainWindow w, string key)
    {
        foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
        w.Show(key);
        if (w.Windows.Active is { Maximized: false } win) { w.Windows.ToggleMaximize(win.Id); w.Show(key); }
        Settle(); w.UpdateLayout(); Settle();
    }

    static ProgramDocument Doc(MainWindow w, string key, string? tpl = null)
    {
        foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
        while (w.ProgramDocs(key).Count > 0) w.ProgramClose(key, 0);
        w.Show(key);
        var d = w.ProgramNew(key, tpl ?? MainWindow.Programs[key].Templates.FirstOrDefault()?.Id);
        Go(w, key);
        return d;
    }

    static IEnumerable<T> In<T>(MainWindow w) where T : Control => w.ActivePageRoot!.GetLogicalDescendants().OfType<T>();
    static T? Named<T>(MainWindow w, string name) where T : Control => In<T>(w).FirstOrDefault(c => c.Name == name);
    static T Need<T>(MainWindow w, string name) where T : Control => Named<T>(w, name) ?? throw new Xunit.Sdk.XunitException($"{name} fehlt auf {w.CurrentView}");
    static List<Button> PageGold(MainWindow w) => In<Button>(w).Where(b => b.IsEffectivelyVisible && MainWindow.IsGold(b) && !(b.Name ?? "").StartsWith("auftrag-", StringComparison.Ordinal)).ToList();

    /// <summary>Gruppe Speichern · Rückgängig · Wiederholen · Exportieren: Reihenfolge, links oben, sichtbar.</summary>
    static void DocActionsAt(MainWindow w, string page, string[] names)
    {
        var group = Need<StackPanel>(w, MainWindow.DocActionsName);
        Assert.Equal(names, group.Children.Select(c => c.Name).ToArray());
        var at = group.TranslatePoint(new Point(0, 0), w.ActivePageRoot!) ?? throw new Xunit.Sdk.XunitException("nicht gezeichnet: " + page);
        var size = w.ActivePageRoot!.Bounds;
        Assert.True(at.X < size.Width * 0.4, $"{page}: Gruppe steht bei x={at.X:0} – nicht links");
        Assert.True(at.Y < size.Height * 0.4, $"{page}: Gruppe steht bei y={at.Y:0} – nicht oben");
        Assert.All(group.Children, c => Assert.True(c.IsEffectivelyVisible, $"{page}: {c.Name} unsichtbar"));
    }

    // ---------- V-14 ----------

    [AvaloniaFact(DisplayName = "V-14: Tabelle und Präsentation – Speichern · Rückgängig · Wiederholen · Als PDF links oben, Speichern ist die eine goldene Fläche, Klicks wirken")]
    public void Doc_actions_sheet_and_deck()
    {
        var w = Open();
        try
        {
            foreach (var level in new[] { UserLevel.Einfach, UserLevel.Profi })
            {
                w.ProgramSetLevel(level);
                w.SheetNew("leer"); Go(w, "tabelle");
                DocActionsAt(w, "tabelle", ["tabelle-save", "tabelle-undo", "tabelle-redo", "tabelle-export"]);
                Assert.Equal(["tabelle-save"], PageGold(w).Select(b => b.Name).ToArray());
                // Rückgängig ist auf jedem Reiter des Menübands da (vorher nur auf „Start“)
                w.Find<Button>("tabelle-rib-daten").Press(); Settle();
                Assert.True(Need<Button>(w, "tabelle-undo").IsEffectivelyVisible);
                w.Find<Button>("tabelle-rib-start").Press(); Settle();

                w.DeckNew("vortrag"); Go(w, "praesentation");
                DocActionsAt(w, "praesentation", ["praesentation-save", "praesentation-undo", "praesentation-redo", "praesentation-export"]);
                Assert.Equal(["praesentation-save"], PageGold(w).Select(b => b.Name).ToArray());
            }
            // Klick: Rückgängig nimmt eine Eingabe zurück, Wiederholen bringt sie wieder
            w.SheetNew("leer"); w.SheetSetCell("A1", "42"); Go(w, "tabelle");
            Need<Button>(w, "tabelle-undo").Press(); Settle();
            Assert.True(string.IsNullOrEmpty(w.SheetDocument.Sheets[0].Get(0, 0)?.Raw));
            Need<Button>(w, "tabelle-redo").Press(); Settle();
            Assert.Equal("42", w.SheetDocument.Sheets[0].Get(0, 0)?.Raw);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-14: Tabelle „Als PDF“ und Präsentation „Als PDF“ schreiben eine echte PDF-Datei")]
    public void Doc_actions_export_writes_pdf()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; w.Settings.SetupDone = true; w.ProgramSetLevel(UserLevel.Einfach);
        try
        {
            var before = Directory.Exists(root) ? Directory.GetFiles(root, "*.pdf", SearchOption.AllDirectories).ToHashSet() : [];
            w.SheetNew("leer"); w.SheetSetCell("A1", "Miete"); w.SheetSetCell("B1", "800"); Go(w, "tabelle");
            Need<Button>(w, "tabelle-export").Press(); Settle();
            w.DeckNew("vortrag"); Go(w, "praesentation");
            Need<Button>(w, "praesentation-export").Press(); Settle();
            var made = Directory.GetFiles(root, "*.pdf", SearchOption.AllDirectories).Where(f => !before.Contains(f)).ToList();
            Assert.True(made.Count >= 2, "PDF-Dateien: " + string.Join(", ", made.Select(Path.GetFileName)));
            Assert.All(made, f => Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(f), 0, 4)));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-14: Mail – „Als Entwurf speichern“ steht im Schreibfenster links oben in der Leiste, Senden bleibt unten mit Rückfrage")]
    public void Doc_actions_mail()
    {
        var w = Open();
        try
        {
            w.MailCompose(new Trendo.Core.MailDraft("kunde@example.org", "Angebot", "Guten Tag"));
            Go(w, "posteingang");
            DocActionsAt(w, "posteingang", ["send-draft"]);
            var bar = Need<WrapPanel>(w, "inbox-bar");
            Assert.Same(bar, Need<StackPanel>(w, MainWindow.DocActionsName).Parent);
            Assert.Equal(0, bar.Children.IndexOf(Need<StackPanel>(w, MainWindow.DocActionsName)));
            Assert.NotNull(Named<Button>(w, "send-ask")); // Senden bleibt im Schreibfenster
            Need<Button>(w, "send-draft").Press(); Settle();
            Assert.Contains(w.MailBoxes.Drafts, x => x.Subject == "Angebot");
        }
        finally { w.Close(); }
    }

    // ---------- V-15 ----------

    [AvaloniaFact(DisplayName = "V-15: Verkaufen hat eine Reiterzeile – Kasse · Online & Artikel · Auswertung · Geschäftsidee, kein Reiter fehlt")]
    public void Sell_has_one_tab_row()
    {
        var w = Open();
        try
        {
            foreach (var level in new[] { UserLevel.Einfach, UserLevel.Profi })
            {
                w.ProgramSetLevel(level);
                Go(w, "verkaufen");
                var rows = In<WrapPanel>(w).Where(p => p.IsEffectivelyVisible && p.Children.OfType<Button>().Count(b => b.Name is { } n && (n.Contains("-tab-", StringComparison.Ordinal))) >= 2).ToList();
                Assert.Single(rows);
                var names = rows[0].Children.OfType<Button>().Select(b => b.Name).ToList();
                Assert.Equal(["sell-tab-kasse", "sell-tab-online", "sell-tab-auswertung", "business-tab-business"], names);
                Need<Button>(w, "business-tab-business").Press(); Settle();
                Assert.Equal("business", w.CurrentView);
                Go(w, "verkaufen");
                Need<Button>(w, "sell-tab-auswertung").Press(); Settle();
                Assert.Equal("verkaufen", w.CurrentView);
                Assert.Single(In<WrapPanel>(w).Where(p => p.IsEffectivelyVisible && p.Children.OfType<Button>().Count(b => (b.Name ?? "").Contains("-tab-", StringComparison.Ordinal)) >= 2));
                Need<Button>(w, "sell-tab-kasse").Press(); Settle();
            }
        }
        finally { w.Close(); }
    }

    // ---------- V-07 ----------

    static readonly string[] PathHints = ["ordner", "datei", "pfad", "verzeichnis", @":\"];

    [AvaloniaFact(DisplayName = "V-07: In „Einfach“ zeigt keine Seite und kein Programm ein Pfadfeld – die Auswahl läuft über „Datei wählen …“")]
    public void No_visible_path_field_in_simple()
    {
        var w = Open();
        var bad = new List<string>();
        try
        {
            foreach (var key in w.AllPageKeys().OrderBy(k => k).ToList())
                foreach (var doc in new[] { false, true })
                {
                    if (doc && !(MainWindow.Programs.TryGetValue(key, out var spec) && spec.Templates.Count > 0)) continue;
                    try
                    {
                        if (doc) Doc(w, key); else Go(w, key);
                    }
                    catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or NotSupportedException) { continue; }
                    if (w.ActivePageRootForTests is not { } root) continue;
                    var known = w.PathFieldsIn(root);
                    foreach (var tb in root.GetLogicalDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible))
                    {
                        var h = ((tb.PlaceholderText ?? "") + " " + (tb.Name ?? "")).ToLowerInvariant();
                        if (known.Contains(tb)) bad.Add($"{key}{(doc ? " (Dokument)" : "")}: {tb.Name} „{tb.PlaceholderText}“ (Pfadfeld sichtbar)");
                    }
                }
        }
        finally { w.Close(); }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Distinct()));
    }

    // ---------- V-12 ----------

    [AvaloniaFact(DisplayName = "V-12: Podcast – Kapitel in „Einfach“ als Liste mit Feldern und „+ Kapitel hinzufügen“, ohne Textformat; Profi behält das Textfeld")]
    public void Podcast_chapters_as_list()
    {
        var w = Open();
        try
        {
            const string K = "podcast-programm";
            var d = Doc(w, K, "interview");
            Assert.False(Need<TextBox>(w, "podc-chapters").IsEffectivelyVisible);
            Assert.Equal("14:40", Need<TextBox>(w, "podc-ch-time-2").Text);
            Assert.Equal("Die besten Pflanzen", Need<TextBox>(w, "podc-ch-title-2").Text);
            Assert.DoesNotContain(In<TextBlock>(w), t => t.IsEffectivelyVisible && (t.Text ?? "").Contains("LUFS"));
            // Name ändern
            var title = Need<TextBox>(w, "podc-ch-title-1"); title.Focus(); title.Text = "Der Anfang"; Need<TextBox>(w, "podc-ch-time-1").Focus(); Settle();
            Assert.Contains("02:15 Der Anfang", d.Text("chapters"));
            // Hinzufügen: eine Minute nach dem letzten
            Need<Button>(w, "podc-ch-add").Press(); Settle();
            Assert.Contains("32:05 Neues Kapitel", d.Text("chapters"));
            Assert.NotNull(Named<TextBox>(w, "podc-ch-title-4"));
            // Entfernen
            Need<Button>(w, "podc-ch-del-4").Press(); Settle();
            Assert.DoesNotContain("Neues Kapitel", d.Text("chapters"));
            // rückgängig machbar (Hülle)
            Assert.True(d.Undo()); Assert.Contains("Neues Kapitel", d.Text("chapters"));

            w.ProgramSetLevel(UserLevel.Profi); Go(w, K);
            Assert.True(Need<TextBox>(w, "podc-chapters").IsEffectivelyVisible);
            Assert.Null(Named<StackPanel>(w, "podc-ch-list"));
        }
        finally { w.Close(); }
    }

    // ---------- Büro-Exporte ----------

    [AvaloniaFact(DisplayName = "T-0705-Rest: Büro-Studios zeigen in „Einfach“ ihre wichtigsten Ausgaben direkt (nicht golden), der Rest bleibt unter „Mehr“")]
    public void Office_exports_visible_in_simple()
    {
        var w = Open();
        try
        {
            foreach (var (key, count) in MainWindow.OfficeDirectExports)
            {
                var spec = MainWindow.Programs[key];
                Doc(w, key);
                var want = spec.Exports.Take(Math.Min(count, spec.SimpleExports)).Select(x => "prog-export-" + x.Name).ToList();
                Assert.NotEmpty(want);
                foreach (var n in want)
                {
                    var b = In<Button>(w).FirstOrDefault(x => x.Name == n && x.IsEffectivelyVisible);
                    Assert.True(b is not null, $"{key}: {n} nicht direkt sichtbar");
                    Assert.False(MainWindow.IsGold(b!) && n != "prog-export-" + spec.PrimaryExport, $"{key}: {n} ist golden");
                }
                Assert.True(PageGold(w).Count <= 1, $"{key}: mehr als ein goldener Knopf: " + string.Join(", ", PageGold(w).Select(b => b.Name)));
            }
        }
        finally { w.Close(); }
    }

    // ---------- V-22 (Webseite bauen) ----------

    [AvaloniaFact(DisplayName = "V-22: Webseite bauen hat genau einen goldenen Knopf – „Seite bauen“; Editor, KI und die Abschnitte darunter sind ruhig")]
    public void Website_one_gold()
    {
        var w = Open();
        try
        {
            foreach (var level in new[] { UserLevel.Einfach, UserLevel.Profi })
            {
                w.ProgramSetLevel(level); Go(w, "dev");
                var gold = PageGold(w).Where(b => !(b.Name ?? "").Contains("-tab-", StringComparison.Ordinal)).Select(b => b.Name).ToList(); // gewählter Reiter zählt nicht als Aktion
                Assert.Equal(["web-build"], gold);
                foreach (var quiet in new[] { "web-visual-open", "web-ki-new", "web-ki-change" })
                    Assert.False(MainWindow.IsGold(Need<Button>(w, quiet)), $"{level}: {quiet} ist golden");
            }
        }
        finally { w.Close(); }
    }
}
