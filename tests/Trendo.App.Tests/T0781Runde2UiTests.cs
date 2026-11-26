using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0781, 2. Prüfrunde (MITTEL „Rechnen im Hintergrund“): ein spätes Ergebnis überschreibt nie neu Getipptes, gilt nur für seinen Stand,
/// fasst ein geschlossenes Dokument nicht an, Ausnahmen kommen als Meldung an, und gewartet wird ohne die Oberfläche anzuhalten.
/// Langsames Rechnen wird mit <see cref="MainWindow.MhKernFuerTests"/> nachgestellt: der kurze Versuch läuft in die Zeitgrenze, der Hintergrund wartet auf ein Tor.
public partial class T0781Wissen_Ui
{
    sealed class LangsamerKern : IDisposable
    {
        public readonly ManualResetEventSlim Tor = new(false);
        /// <summary>Wenn gesetzt: der zweite Aufruf im Hintergrund wartet auf dieses Tor (Reihenfolge der Ergebnisse festlegen).</summary>
        public ManualResetEventSlim? TorZweiter;
        public Exception? Werfe;
        public int Hintergrund;
        public LangsamerKern()
        {
            MainWindow.MhKernFuerTests = (a, zeit) =>
            {
                if (zeit is not null) throw new OperationCanceledException("Zeitgrenze (nachgestellt)");
                var nr = Interlocked.Increment(ref Hintergrund);
                Assert.True((nr == 2 && TorZweiter is { } z ? z : Tor).Wait(15000), "Tor nie geöffnet");
                if (Werfe is { } ex) throw ex;
                return MatheKern.Rechne(a);
            };
        }
        public void Dispose() { Tor.Set(); TorZweiter?.Set(); MainWindow.MhKernFuerTests = null; }
    }

    static void Klick(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }

    [AvaloniaFact(DisplayName = "T-0781 R2 Hintergrund: „Rechnen“ hält die Oberfläche nicht an; spätes Ergebnis lässt neu Getipptes stehen")]
    public async Task Spaet_ueberschreibt_getipptes_nicht()
    {
        var (w, _, _) = Ui.Open();
        using var k = new LangsamerKern();
        try
        {
            var d = OpenMathe(w);
            Tippe(w, "mh-eingabe", "2x = 4");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Klick(w, "prog-cmd-mh-run-rechnen");
            Assert.True(sw.ElapsedMilliseconds < 1500, $"Oberfläche stand {sw.ElapsedMilliseconds} ms"); // vorher: task.Wait(250) + synchron
            Assert.True(w.MhRechnetNoch); Assert.StartsWith("Rechnet", w.ProgramMessage(M));
            Assert.Empty(d.List("rechnungen"));
            Tippe(w, "mh-eingabe", "x + 1 = 7"); // während gerechnet wird: neu getippt, nicht abgeschickt
            k.Tor.Set();
            await Until(() => !w.MhRechnetNoch);
            Settle();
            Assert.Single(d.List("rechnungen"));
            Assert.Equal("2x = 4", d.List("rechnungen")[0]!["eingabe"]!.GetValue<string>());
            Assert.Equal("x + 1 = 7", w.Find<TextBox>("mh-eingabe").Text); // vorher: Eingabezeile geleert
            Assert.Contains("x = 2", w.ProgramMessage(M));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 R3 Hintergrund: schnelle Folge zweier verschiedener Rechnungen – beide kommen an, keine wird still verworfen")]
    public async Task Veraltetes_ergebnis_verworfen()
    {
        var (w, _, _) = Ui.Open();
        using var k = new LangsamerKern();
        try
        {
            var d = OpenMathe(w);
            Tippe(w, "mh-eingabe", "2x = 4"); Klick(w, "prog-cmd-mh-run-rechnen");
            Tippe(w, "mh-eingabe", "3x = 9"); Klick(w, "prog-cmd-mh-run-rechnen");
            await Until(() => Volatile.Read(ref k.Hintergrund) == 2);
            k.Tor.Set();
            await Until(() => !w.MhRechnetNoch);
            Settle();
            // T-0781 (3. Prüfrunde): vorher verschwand „2x = 4“ still – jetzt werden beide angehängt
            Assert.Equal(2, d.List("rechnungen").Count);
            Assert.Equal(["2x = 4", "3x = 9"], d.List("rechnungen").Select(r => r!["eingabe"]!.GetValue<string>()).Order().ToArray());
            Assert.Equal("", w.Find<TextBox>("mh-eingabe").Text ?? "");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 R3 Hintergrund: nur eine ältere Fassung derselben Zeile wird verworfen – mit Meldung „Ältere Rechnung verworfen“")]
    public async Task Gleiche_fassung_verworfen_mit_meldung()
    {
        var (w, _, _) = Ui.Open();
        using var k = new LangsamerKern();
        try
        {
            var d = OpenMathe(w);
            k.TorZweiter = new ManualResetEventSlim(false);
            Tippe(w, "mh-eingabe", "2x = 4"); Klick(w, "prog-cmd-mh-run-rechnen");
            Tippe(w, "mh-eingabe", "2x = 4"); Klick(w, "prog-cmd-mh-run-rechnen");
            await Until(() => Volatile.Read(ref k.Hintergrund) == 2);
            k.TorZweiter.Set(); // die neuere Fassung wird zuerst fertig …
            await Until(() => d.List("rechnungen").Count == 1);
            k.Tor.Set();        // … die ältere gleiche Fassung danach: verworfen, mit Meldung
            await Until(() => !w.MhRechnetNoch);
            Settle();
            Assert.Single(d.List("rechnungen"));
            Assert.Equal("2x = 4", d.List("rechnungen")[0]!["eingabe"]!.GetValue<string>());
            Assert.Contains("Ältere Rechnung verworfen", w.ProgramMessage(M));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 R2 Hintergrund: geschlossenes Dokument wird nicht angefasst; keine Zwangs-Navigation zurück zum Mathe-Helfer")]
    public async Task Geschlossenes_dokument_unberuehrt()
    {
        var (w, _, _) = Ui.Open();
        using var k = new LangsamerKern();
        try
        {
            var d = OpenMathe(w);
            Tippe(w, "mh-eingabe", "2x = 4"); Klick(w, "prog-cmd-mh-run-rechnen");
            var vorher = d.Data.ToJsonString();
            w.ProgramClose(M, w.ProgramDocs(M).IndexOf(d)); Settle();
            Assert.DoesNotContain(d, w.ProgramDocs(M));
            w.Show(N); Settle();
            k.Tor.Set();
            await Until(() => !w.MhRechnetNoch);
            Settle();
            Assert.Equal(vorher, d.Data.ToJsonString());
            Assert.Equal(N, w.CurrentView);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 R2 Hintergrund: Ausnahme beim Rechnen im Hintergrund wird zur Meldung, kein Absturz, nichts angelegt")]
    public async Task Ausnahme_im_hintergrund()
    {
        var (w, _, _) = Ui.Open();
        using var k = new LangsamerKern { Werfe = new NullReferenceException("nachgestellt") };
        try
        {
            var d = OpenMathe(w);
            Tippe(w, "mh-eingabe", "2x = 4"); Klick(w, "prog-cmd-mh-run-rechnen");
            k.Tor.Set();
            await Until(() => !w.MhRechnetNoch);
            Settle();
            Assert.Empty(d.List("rechnungen"));
            Assert.DoesNotContain("Rechnet", w.ProgramMessage(M));
            Assert.False(string.IsNullOrWhiteSpace(w.ProgramMessage(M)));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0781 R2 Hintergrund: Aufgabenblatt wird im Hintergrund gerechnet (vorher synchron je Zeile) – Oberfläche bleibt bedienbar")]
    public async Task Import_ohne_blockieren()
    {
        var (w, _, root) = Ui.Open();
        using var k = new LangsamerKern();
        try
        {
            var d = OpenMathe(w);
            var f = Path.Combine(root, "blatt-r3.txt");
            File.WriteAllText(f, "2x + 4 = 10\nx^2 = 9\nbruch: 1/2 + 1/3\n", new UTF8Encoding(false));
            w.PickOverride = _ => Task.FromResult<string[]?>([f]);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Klick(w, "prog-cmd-mh-import");
            Assert.True(sw.ElapsedMilliseconds < 1500, $"Oberfläche stand {sw.ElapsedMilliseconds} ms");
            Assert.Contains("rechnet 3 Aufgabe(n)", w.ProgramMessage(M));
            Assert.Empty(d.List("rechnungen"));
            k.Tor.Set();
            await Until(() => d.List("rechnungen").Count == 3);
            await Until(() => !w.MhRechnetNoch);
            Assert.Contains("3 Aufgabe(n) gerechnet", w.ProgramMessage(M));
        }
        finally { w.PickOverride = null; w.Close(); }
    }
}
