using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Paket E (Wunsch 02.11.2026): Foto → 3D-Modell (TripoSR) und Geräusche (MMAudio) in der KI-Bibliothek, Vorschläge in den Studios.
/// Alle Programmläufe sind nachgebildet – kein echtes Laden, kein echtes Python.
public class KiPy_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok) { for (var i = 0; i < 200 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Pump(); Assert.True(ok()); }

    sealed record Call(string Exe, string[] Args, string Cwd, Dictionary<string, string> Env);

    static Func<string, IReadOnlyList<string>, string, IReadOnlyDictionary<string, string>, Task<Cli.Result>> Fake(List<Call> calls, string commit)
        => (exe, args, cwd, env) =>
        {
            lock (calls) calls.Add(new(exe, [.. args], cwd, env.ToDictionary()));
            if (args is ["venv", ..]) { var py = Path.Combine(args[^1], "Scripts", "python.exe"); Directory.CreateDirectory(Path.GetDirectoryName(py)!); File.WriteAllText(py, "MZ"); }
            if (args is ["-C", _, "rev-parse", "HEAD"]) return Task.FromResult(new Cli.Result(0, commit + "\n", ""));
            // Startskripte: Ergebnisdatei anlegen (Argument 3 = Ziel)
            if (args.Count > 2 && args[0].EndsWith("trendo_triposr.py")) File.WriteAllText(args[3], "glTF");
            if (args.Count > 2 && args[0].EndsWith("trendo_mmaudio.py")) File.WriteAllText(args[2], "RIFF");
            return Task.FromResult(new Cli.Result(0, "", ""));
        };

    static void FakeUv(MainWindow w) { var uv = w.KiStore.RuntimeExe(KiRuntime.Uv); Directory.CreateDirectory(Path.GetDirectoryName(uv)!); File.WriteAllText(uv, "MZ"); }

    [AvaloniaFact(DisplayName = "3D aus Bild: TripoSR nach Klick einrichten (Stand geprüft, eigene Umgebung), Foto wählen → GLB → „Im 3D-Studio öffnen“; falscher Stand wird nicht eingerichtet")]
    public async Task TripoSR_install_and_run()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var m = KiLibrary.Models.First(x => x.Id == "triposr"); var app = KiPyApps.For(KiRuntime.TripoSR)!;
            var calls = new List<Call>(); FakeUv(w); w.NvidiaOverride = () => true;

            // falscher Stand: abgelehnt
            w.KiStepOverride = Fake(calls, "0000000000000000000000000000000000000000");
            await w.KiLibraryInstall(m);
            Assert.False(w.KiStore.RuntimeReady(KiRuntime.TripoSR));
            Assert.Contains("geprüften Stand", w.Find<TextBlock>("kilib-msg").Text);

            calls.Clear(); w.KiStepOverride = Fake(calls, app.Commit);
            await w.KiLibraryInstall(m);
            Assert.True(w.KiStore.RuntimeReady(KiRuntime.TripoSR)); Assert.True(w.KiStore.ModelReady(m));
            Assert.Contains(calls, c => c.Args is ["pip", "install", ..] && c.Args.Contains("https://download.pytorch.org/whl/cu128"));
            Assert.All(calls, c => Assert.Equal("1", c.Env["UV_NO_MODIFY_PATH"]));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "ki.pyapp.fertig" && a.Detail.Contains(app.Commit));

            // ausprobieren
            var foto = Path.Combine(root, "stuhl.png"); File.WriteAllBytes(foto, [0x89, 0x50, 0x4E, 0x47]);
            w.PickOverride = _ => Task.FromResult<string[]?>([foto]);
            w.KiLibraryShow(KiKind.Modell3D, m.Id); Pump();
            Assert.True(w.Find<CheckBox>("kilib-3d-texture").IsChecked);
            w.Find<Button>("kilib-run").Press();
            await Until(() => w.TryFind<Button>("kilib-open-3d") is not null);
            var run = calls.Last(); Assert.EndsWith("trendo_triposr.py", run.Args[0]); Assert.Equal(foto, run.Args[2]); Assert.Contains("--texture", run.Args);
            Assert.EndsWith(".glb", run.Args[3]);
            Assert.Equal(w.KiStore.RuntimeDir(KiRuntime.TripoSR), run.Cwd);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Geräusche: MMAudio mit Hinweis „nur nicht-kommerziell“, Beschreibung + Länge → WAV zum Anhören; ohne Beschreibung kein Lauf")]
    public async Task MMAudio_run()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var m = KiLibrary.Models.First(x => x.Id == "mmaudio"); var app = KiPyApps.For(KiRuntime.MMAudio)!;
            var calls = new List<Call>(); FakeUv(w); w.NvidiaOverride = () => false; w.KiStepOverride = Fake(calls, app.Commit);
            await w.KiLibraryInstall(m);
            Assert.True(w.KiStore.ModelReady(m));
            Assert.Contains(calls, c => c.Args.Contains("https://download.pytorch.org/whl/cpu")); // ohne NVIDIA: Prozessor-Fassung
            Assert.Contains(calls, c => c.Args.Contains("--editable"));
            w.KiLibraryShow(KiKind.Geraeusche, m.Id); Pump();
            Assert.Contains("nicht-kommerzielle", string.Join("|", w.GetLogicalTexts()));
            var n = calls.Count;
            w.Find<Button>("kilib-run").Press(); Pump();
            Assert.Equal(n, calls.Count); // ohne Beschreibung nichts gestartet
            w.Find<TextBox>("kilib-prompt").Text = "rain on a tin roof";
            w.Find<ComboBox>("kilib-sound-len").SelectedIndex = 2;
            w.Find<Button>("kilib-run").Press();
            await Until(() => w.TryFind<Button>("kilib-play-result") is not null);
            var run = calls.Last(); Assert.EndsWith("trendo_mmaudio.py", run.Args[0]);
            Assert.Equal("rain on a tin roof", run.Args[run.Args.ToList().IndexOf("--prompt") + 1]); Assert.Equal("10", run.Args[run.Args.ToList().IndexOf("--duration") + 1]);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Vorschlag 36: Studios zeigen „Freie KI hier“ – 3D-Studio schlägt „3D aus Bild“ vor, der Klick öffnet die KI-Bibliothek (geladen wird erst dort); jede Studio-Zuordnung gibt es wirklich")]
    public void Suggestions_in_studios()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var sid in KiSuggestions.Studios) Assert.NotNull(StudioCatalog.For(sid));
            w.Show("3d"); Pump();
            var b = w.Find<Button>("page-freeki-" + KiKind.Modell3D);
            Assert.Equal("3D aus Bild", b.Content);
            b.Press(); Pump();
            Assert.Equal("ki-bibliothek", w.CurrentView);
            Assert.NotNull(w.TryFind<Button>("kilib-get-triposr"));
        }
        finally { w.Close(); }
    }
}
