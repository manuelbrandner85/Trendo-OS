using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0680: Programm-Hülle absturzsicher – Wiederherstellung, zweites Fenster, Änderung von außen, beschädigte Dateien; am Beispiel „Kleidung &amp; Produkte“.
/// Ein „Absturz“ ist hier ein Fenster, das ohne Speichern und ohne Schließen liegen bleibt; ein neues Fenster arbeitet auf demselben Datenordner weiter.
/// (Der echte Prozessabbruch mitten im Schreiben steht in den Kern-Tests <c>ProgramCrashTests</c>.)</summary>
public class ProgramCrashUiTests
{
    const string Key = "mode-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    /// <summary>Neues Fenster auf einem vorhandenen Datenordner – wie ein Neustart nach dem Absturz.</summary>
    static MainWindow OpenOn(string root)
    {
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert);
        w.Show(); Dispatcher.UIThread.RunJobs();
        return w;
    }

    /// <summary>Fenster „abstürzen“ lassen: kein Schließen, kein Speichern; seine Sperren bleiben liegen wie nach einem beendeten Prozess.</summary>
    static void Crash(MainWindow w) => w.ProgLocks.Abandon();

    static int Pieces(MainWindow w) => w.ProgramActive(Key)!.List("pieces").Count;

    [AvaloniaFact(DisplayName = "T-0680 Absturz, nie gespeichert: Dokument ändern, Sicherung läuft, Fenster stürzt ab – das neue Fenster bietet „Wiederherstellen“ mit Zeitpunkt an und bringt die Änderung zurück; die Sicherung verschwindet erst nach dem Speichern")]
    public void Unsaved_document_is_offered_after_crash()
    {
        var (w, _, root) = Ui.Open();
        w.Show(Key); Pump();
        w.Find<Button>("prog-tpl-rock").Press(); Pump();
        var before = Pieces(w);
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        w.ProgramAutosaveAll(); // der 20-Sekunden-Takt
        var backup = Path.Combine(root, "programme", "sicherung");
        var saved = Assert.Single(Directory.GetFiles(backup, "*.tdoc"));

        // Solange das erste Fenster lebt, ist sein Dokument für ein zweites Fenster kein „abgestürztes“
        var other = OpenOn(root); other.Show(Key); Pump();
        Assert.Null(other.TryFind<Button>("prog-restore-0"));

        Crash(w);
        var w2 = OpenOn(root); w2.Show(Key); Pump();
        Assert.NotNull(w2.Find<StackPanel>("prog-restore"));
        Assert.Contains(w2.GetLogicalTexts(), t => t.Contains("Rock") && t.Contains("gesichert heute um"));
        Assert.NotNull(w2.TryFind<Button>("prog-discard-0"));
        w2.Find<Button>("prog-restore-0").Press(); Pump();
        var d = w2.ProgramActive(Key)!;
        Assert.Equal(before + 1, d.List("pieces").Count); Assert.True(d.Dirty);
        Assert.True(File.Exists(saved)); // Wiederherstellen löscht die Sicherung nicht – erst das Speichern
        // Stürzt auch dieses Fenster vor dem Speichern ab, ist die Arbeit immer noch da
        Crash(w2);
        var w3 = OpenOn(root); w3.Show(Key); Pump();
        w3.Find<Button>("prog-restore-0").Press(); Pump();
        Assert.Equal(before + 1, Pieces(w3));
        w3.Find<Button>("prog-save").Press(); Pump();
        Assert.False(File.Exists(saved));
        Assert.Equal(before + 1, ProgramDocument.Load(w3.ProgramActive(Key)!.Path!).List("pieces").Count);
        w3.Find<Button>("prog-close").Press(); Pump();
        Assert.Null(w3.TryFind<Button>("prog-restore-0")); // nichts mehr anzubieten
        w3.Close(); other.Close();
    }

    [AvaloniaFact(DisplayName = "T-0680 Absturz, gespeichertes Dokument: Die neuere Notsicherung neben der Datei wird beim Öffnen angeboten (Wiederherstellen / Verwerfen, mit Zeitpunkt); Wiederherstellen bringt die Änderung zurück und lässt sich rückgängig machen")]
    public void Newer_autosave_next_to_file_is_offered()
    {
        var (w, _, root) = Ui.Open();
        w.Show(Key); Pump();
        w.Find<Button>("prog-tpl-rock").Press(); Pump();
        w.Find<Button>("prog-save").Press(); Pump();
        var file = w.ProgramActive(Key)!.Path!; var before = Pieces(w);
        Thread.Sleep(30); // die Sicherung muss erkennbar neuer sein als die Datei
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        w.ProgramAutosaveAll();
        var side = ProgramDocument.AutosaveFile(file);
        Assert.True(File.Exists(side));
        Crash(w);

        // 1) Verwerfen
        File.Copy(side, side + ".kopie");
        var w2 = OpenOn(root); w2.Show(Key); Pump();
        w2.Find<Button>("prog-recent-0").Press(); Pump();
        Assert.Equal(before, Pieces(w2)); // gezeigt wird erst der gespeicherte Stand
        var text = w2.Find<TextBlock>("prog-recover-text").Text!;
        Assert.Contains("heute um", text); Assert.Contains("automatisch gesichert", text);
        // weiterarbeiten und speichern lässt das Angebot nicht still verschwinden
        w2.Find<Button>("prog-save").Press(); Pump();
        Assert.NotNull(w2.TryFind<Button>("prog-recover-yes"));
        w2.Find<Button>("prog-recover-no").Press(); Pump();
        Assert.Null(w2.TryFind<Button>("prog-recover-yes")); Assert.Equal(before, Pieces(w2));
        Assert.False(File.Exists(side)); Assert.False(File.Exists(ProgramDocument.RecoveryFile(file)));
        w2.Close();

        // 2) Wiederherstellen
        File.Move(side + ".kopie", side); File.SetLastWriteTimeUtc(side, DateTime.UtcNow);
        var w3 = OpenOn(root); w3.Show(Key); Pump();
        w3.Find<Button>("prog-recent-0").Press(); Pump();
        w3.Find<Button>("prog-recover-yes").Press(); Pump();
        Assert.Equal(before + 1, Pieces(w3)); Assert.True(w3.ProgramActive(Key)!.Dirty);
        Assert.Null(w3.TryFind<Button>("prog-recover-yes"));
        Assert.Contains("Strg+Z", w3.ProgramMessage(Key));
        Assert.True(File.Exists(side)); // noch nicht gespeichert: die Arbeit liegt weiter als Notsicherung da
        w3.Find<Button>("prog-save").Press(); Pump();
        Assert.Equal(before + 1, ProgramDocument.Load(file).List("pieces").Count);
        Assert.False(File.Exists(side)); Assert.False(File.Exists(ProgramDocument.RecoveryFile(file)));
        w3.Find<Button>("prog-undo").Press(); Pump();
        Assert.Equal(before, Pieces(w3));
        w3.Close();

        // 3) kaputte Notsicherung: übersprungen und gemeldet, das Dokument öffnet normal
        File.WriteAllText(side, "{\"format\":\"tdoc\",\"da"); File.SetLastWriteTimeUtc(side, DateTime.UtcNow.AddMinutes(1));
        var w4 = OpenOn(root); w4.Show(Key); Pump();
        w4.Find<Button>("prog-recent-0").Press(); Pump();
        Assert.Equal(before + 1, Pieces(w4));
        Assert.Contains("beschädigt und wurde übersprungen", w4.ProgramMessage(Key));
        Assert.Null(w4.TryFind<Button>("prog-recover-yes")); Assert.False(File.Exists(side));
        w4.Close();
    }

    [AvaloniaFact(DisplayName = "T-0680 Verlust nach Absturz gemessen: Mit automatischem Speichern steht eine Änderung nach der kurzen Pause von selbst in der Datei; das neue Fenster macht dort weiter")]
    public async Task Autosave_delay_measured()
    {
        var (w, _, root) = Ui.Open();
        w.AutoSaveDocs = true;
        w.Show(Key); Pump();
        w.Find<Button>("prog-tpl-rock").Press(); Pump();
        var before = Pieces(w);
        var sw = Stopwatch.StartNew();
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump(); // Änderung – ab jetzt läuft die Uhr, nichts wird von Hand gespeichert
        var d = w.ProgramActive(Key)!;
        while (d.Dirty && sw.Elapsed < TimeSpan.FromSeconds(15)) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
        var took = sw.Elapsed;
        Assert.False(d.Dirty, "Die Änderung wurde nicht von selbst gespeichert.");
        Assert.Equal(before + 1, ProgramDocument.Load(d.Path!).List("pieces").Count);
        Assert.True(took >= MainWindow.ProgramSaveDelay - TimeSpan.FromMilliseconds(200), "früher als die Pause: " + took);
        Assert.True(took < MainWindow.ProgramSaveDelay + TimeSpan.FromSeconds(3), "zu spät: " + took);
        Assert.True(MainWindow.ProgramAutosaveEvery <= TimeSpan.FromSeconds(25), "Der feste Takt muss mit Schreibzeit unter 30 s bleiben.");
        TestContext.Current.TestOutputHelper?.WriteLine($"Änderung stand nach {took.TotalMilliseconds:0} ms in der Datei (Pause {MainWindow.ProgramSaveDelay.TotalSeconds} s, fester Takt {MainWindow.ProgramAutosaveEvery.TotalSeconds} s).");
        Assert.Empty(Directory.Exists(Path.Combine(root, "programme", "sicherung")) ? Directory.GetFiles(Path.Combine(root, "programme", "sicherung"), "*.tdoc") : []);

        Crash(w);
        var w2 = OpenOn(root); w2.Show(Key); Pump();
        Assert.Null(w2.TryFind<Button>("prog-restore-0")); // nichts wiederherzustellen – es ist schon gespeichert
        w2.Find<Button>("prog-continue").Press(); Pump();
        Assert.Equal(before + 1, Pieces(w2));
        Assert.Null(w2.TryFind<Border>("prog-lock")); // die liegengebliebene Sperre des abgestürzten Fensters wurde übernommen
        Assert.False(w2.ProgramActive(Key)!.ReadOnly);
        w2.Close();
    }

    [AvaloniaFact(DisplayName = "T-0680 Zweites Fenster: dieselbe Datei öffnet dort nur zum Lesen – nichts wird still überschrieben; angeboten wird „Nur lesen“ oder „Als Kopie“; nach dem Schließen im ersten Fenster ist sie frei")]
    public void Second_window_cannot_overwrite()
    {
        var (w, _, root) = Ui.Open();
        w.Show(Key); Pump();
        w.Find<Button>("prog-tpl-rock").Press(); Pump();
        w.Find<Button>("prog-save").Press(); Pump();
        var file = w.ProgramActive(Key)!.Path!; var before = Pieces(w); var onDisk = File.ReadAllText(file);

        var w2 = OpenOn(root); w2.AutoSaveDocs = true; w2.Show(Key); Pump();
        w2.Find<Button>("prog-recent-0").Press(); Pump();
        var d2 = w2.ProgramActive(Key)!;
        Assert.True(d2.ReadOnly); Assert.True(w2.ProgramIsLocked(d2));
        var text = w2.Find<TextBlock>("prog-lock-text").Text!;
        Assert.Contains("anderen Trendo-Fenster", text); Assert.Contains("nur zum Lesen", text);
        Assert.Equal("Nur lesen", w2.Find<TextBlock>("prog-dirty").Text);
        // ändern und alles versuchen, was speichert: automatisch, Knopf, Strg+S-Weg
        w2.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        Assert.Equal(0, w2.ProgramAutosaveNow());
        w2.Find<Button>("prog-save").Press(); Pump();
        Assert.Contains("nur zum Lesen", w2.ProgramMessage(Key));
        w2.ProgramAutosaveAll();
        Assert.Equal(onDisk, File.ReadAllText(file)); // unverändert
        Assert.False(File.Exists(ProgramDocument.AutosaveFile(file))); // auch die Notsicherung des ersten Fensters bleibt unberührt
        // „Nur lesen“ bestätigt: Karte weg, Schutz bleibt
        w2.Find<Button>("prog-lock-readonly").Press(); Pump();
        Assert.Null(w2.TryFind<Border>("prog-lock")); Assert.True(d2.ReadOnly);
        Assert.Equal(0, w2.ProgramAutosaveNow()); Assert.Equal(onDisk, File.ReadAllText(file));

        // drittes Fenster: „Als Kopie öffnen“ – eigene Datei, das Original bleibt
        var w3 = OpenOn(root); w3.Show(Key); Pump();
        w3.Find<Button>("prog-recent-0").Press(); Pump();
        w3.Find<Button>("prog-lock-copy").Press(); Pump();
        var d3 = w3.ProgramActive(Key)!;
        Assert.Null(d3.Path); Assert.False(d3.ReadOnly); Assert.EndsWith("(Kopie)", d3.Title);
        w3.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        w3.Find<Button>("prog-save").Press(); Pump();
        Assert.NotEqual(file, d3.Path); Assert.Equal(before + 1, ProgramDocument.Load(d3.Path!).List("pieces").Count);
        Assert.Equal(onDisk, File.ReadAllText(file));
        // „Speichern unter“ auf die gesperrte Datei aus einem anderen Fenster: abgelehnt
        Assert.Throws<IOException>(() => w3.ProgramSave(Key, file));
        Assert.Equal(onDisk, File.ReadAllText(file));

        // erstes Fenster ändert, speichert und schließt das Dokument → „Nochmal prüfen“ im zweiten Fenster macht es dort frei
        w3.Find<Button>("prog-close").Press(); Pump();
        w3.Find<Button>("prog-recent-1").Press(); Pump(); // das Original (die Kopie steht jetzt oben)
        Assert.NotNull(w3.TryFind<Border>("prog-lock"));
        w3.Find<Button>("prog-lock-retry").Press(); Pump();
        Assert.Contains("noch im anderen Fenster", w3.ProgramMessage(Key));
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        w.Find<Button>("prog-save").Press(); Pump();
        w.Find<Button>("prog-close").Press(); Pump();
        w3.Find<Button>("prog-lock-retry").Press(); Pump();
        Assert.Null(w3.TryFind<Border>("prog-lock"));
        Assert.False(w3.ProgramActive(Key)!.ReadOnly); Assert.Equal(before + 1, Pieces(w3)); // und zeigt den neuesten Stand
        w3.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        w3.Find<Button>("prog-save").Press(); Pump();
        Assert.Equal(before + 2, ProgramDocument.Load(file).List("pieces").Count);
        w.Close(); w2.Close(); w3.Close();
    }

    [AvaloniaFact(DisplayName = "T-0680 Von außen geändert: Weder das automatische Speichern noch „Speichern“ überschreibt – es wird gefragt (meine speichern / als Kopie / die andere laden); die andere Fassung bleibt als Version")]
    public void Changed_outside_asks_first()
    {
        var (w, _, _) = Ui.Open();
        w.AutoSaveDocs = true;
        w.Show(Key); Pump();
        w.Find<Button>("prog-tpl-rock").Press(); Pump();
        w.Find<Button>("prog-save").Press(); Pump();
        var d = w.ProgramActive(Key)!; var file = d.Path!; var before = Pieces(w);

        void Outside(string name) { var o = ProgramDocument.Load(file); o.Set("name", name); o.Set("fremd", new string('x', name.Length + 40)); o.Save(); }
        Outside("Von außen");
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        Assert.Equal(0, w.ProgramAutosaveNow()); Pump(); // automatisches Speichern überschreibt nicht …
        Assert.Equal("Von außen", ProgramDocument.Load(file).Text("name"));
        Assert.True(w.ProgramHasConflict(d));
        Assert.Contains("außerhalb dieses Fensters geändert", w.Find<TextBlock>("prog-conflict-text").Text);
        Assert.True(File.Exists(ProgramDocument.AutosaveFile(file))); // … die eigene Arbeit ist trotzdem gesichert
        w.Find<Button>("prog-save").Press(); Pump();          // … und der Knopf auch nicht
        Assert.Equal("Von außen", ProgramDocument.Load(file).Text("name"));
        w.Find<Button>("prog-conflict-keep").Press(); Pump();
        Assert.Equal(before + 1, ProgramDocument.Load(file).List("pieces").Count);
        Assert.Null(w.TryFind<Border>("prog-conflict")); Assert.False(d.Dirty);
        Assert.Contains(ProgramDocument.Versions(file), v => ProgramDocument.Load(v.File).Text("name") == "Von außen");
        Assert.False(File.Exists(ProgramDocument.AutosaveFile(file)));

        // die andere Fassung laden
        Outside("Zweite fremde Fassung");
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        w.Find<Button>("prog-save").Press(); Pump();
        w.Find<Button>("prog-conflict-load").Press(); Pump();
        Assert.Equal("Zweite fremde Fassung", d.Text("name")); Assert.False(d.Dirty); Assert.False(w.ProgramHasConflict(d));
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.Equal(before + 2, Pieces(w)); // die eigene Fassung ist einen Schritt entfernt

        // als Kopie speichern
        Outside("Dritte fremde Fassung");
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        Assert.Equal(0, w.ProgramAutosaveNow()); Pump();
        w.Find<Button>("prog-conflict-copy").Press(); Pump();
        Assert.NotEqual(file, d.Path); Assert.Contains("(Kopie)", d.Path);
        Assert.Equal("Dritte fremde Fassung", ProgramDocument.Load(file).Text("name"));
        Assert.Equal(before + 3, ProgramDocument.Load(d.Path!).List("pieces").Count);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0680 Beschädigte Datei: verständliche Meldung statt leerem Dokument, der letzte gesicherte Stand wird angeboten und geöffnet; eine Datei aus einer neueren Fassung wird mit Hinweis abgelehnt; kaputte Sicherungen auf der Startseite werden gemeldet")]
    public void Damaged_file_and_newer_version()
    {
        var (w, _, root) = Ui.Open();
        w.Show(Key); Pump();
        w.Find<Button>("prog-tpl-rock").Press(); Pump();
        w.Find<Button>("prog-save").Press(); Pump();
        var file = w.ProgramActive(Key)!.Path!; var before = Pieces(w);
        w.Find<Button>("prog-cmd-piece-add").Press(); Pump();
        w.Find<Button>("prog-save").Press(); Pump(); // der erste Stand ist jetzt eine Version
        w.Find<Button>("prog-close").Press(); Pump();

        var good = File.ReadAllText(file);
        File.WriteAllText(file, good[..(good.Length / 3)]); // abgeschnitten – wie nach einem Absturz ohne sicheres Schreiben
        w.Find<Button>("prog-recent-0").Press(); Pump();
        Assert.Null(w.ProgramActive(Key)); // kein stilles leeres Dokument
        Assert.Contains("beschädigt", w.ProgramMessage(Key));
        var text = w.Find<TextBlock>("prog-rescue-text").Text!;
        Assert.Contains("beschädigt", text); Assert.Contains("zurückholen", text);
        Assert.Single(w.ProgramRescueOffers(Key));
        Assert.Contains("frühere Version", (string)w.Find<Button>("prog-rescue-0").Content!);
        w.Find<Button>("prog-rescue-0").Press(); Pump();
        var d = w.ProgramActive(Key)!;
        Assert.Equal((file, before, true), (d.Path, d.List("pieces").Count, d.Dirty));
        Assert.True(File.Exists(file + ".beschaedigt")); // die beschädigte Datei bleibt zur Sicherheit liegen
        w.Find<Button>("prog-save").Press(); Pump();
        Assert.Equal(before, ProgramDocument.Load(file).List("pieces").Count);
        Assert.Null(w.TryFind<Border>("prog-rescue"));
        w.Find<Button>("prog-close").Press(); Pump();

        // ohne jeden früheren Stand: klare Auskunft
        var lonely = Path.Combine(Path.GetDirectoryName(file)!, "Allein.tdoc"); File.WriteAllText(lonely, "{ kaputt");
        Assert.Throws<InvalidDataException>(() => w.ProgramOpen(Key, lonely));
        w.Show(Key); Pump();
        Assert.Contains("keinen früheren Stand", w.Find<TextBlock>("prog-rescue-text").Text);
        Assert.Null(w.TryFind<Button>("prog-rescue-0"));
        w.Find<Button>("prog-rescue-close").Press(); Pump();
        Assert.Null(w.TryFind<Border>("prog-rescue"));

        // neuere Fassung des Dateiformats: abgelehnt mit Hinweis, Datei unverändert
        var newer = Path.Combine(Path.GetDirectoryName(file)!, "Neuer.tdoc");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!.AsObject(); json["version"] = ProgramDocument.FormatVersion + 1; json["zusatz"] = "kennt dieses Programm nicht";
        File.WriteAllText(newer, json.ToJsonString()); var raw = File.ReadAllText(newer);
        var e = Assert.Throws<InvalidDataException>(() => w.ProgramOpen(Key, newer));
        Assert.Contains("neueren", e.Message); Assert.Contains("aktualisiere", e.Message);
        Assert.Null(w.ProgramActive(Key)); Assert.Equal(raw, File.ReadAllText(newer));
        Assert.Empty(w.ProgramRescueOffers(Key));

        // kaputte Sicherung im Sicherungsordner: Startseite zeigt sie als beschädigt, statt abzustürzen
        var backup = Path.Combine(root, "programme", "sicherung"); Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "SPEC-08-kaputt0000.tdoc"), "{\"format\":");
        w.Show(Key); Pump();
        Assert.Contains("beschädigt", w.Find<TextBlock>("prog-restore-broken").Text);
        Assert.Null(w.TryFind<Button>("prog-restore-0"));
        w.Find<Button>("prog-restore-broken-drop").Press(); Pump();
        Assert.Null(w.TryFind<TextBlock>("prog-restore-broken")); Assert.Empty(Directory.GetFiles(backup));
        w.Close();
    }
}
