using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 3 (T-0614): Videos mit KI, KI-Agenten bauen, KI-Modelle vergleichen, Eigene KI trainieren.</summary>
public class KreativStudios3_Ui
{
    static string Export(MainWindow w, string key, string name, string root)
    {
        var e = MainWindow.Programs[key].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(key)!, Path.Combine(root, "export-" + key + "-" + name + e.Extension));
    }

    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.Press(); Settle(); }
    static void WaitFor(Func<bool> done) { for (var i = 0; i < 400 && !done(); i++) { Thread.Sleep(15); Settle(); } }
    static readonly byte[] Png1 = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    sealed class Ki(string name, bool up, string answer, params string[] models) : IKiBackend
    {
        public int Calls;
        public string Name => name;
        public string BaseUrl => "http://127.0.0.1:1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(up);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(models);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; return Task.FromResult(answer + " (" + model + ")"); }
    }

    [AvaloniaFact(DisplayName = "Kreativ & KI Teil 3: alle vier Studios haben eine eigene Seite und öffnen ihre Vorlagen")]
    public void Pages()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (studio, key) in new[] { ("STUDIO-VIDEOAI-01", "video-ki-programm"), ("STUDIO-AGENTS-01", "agenten-bauen-programm"), ("STUDIO-MODELS-01", "modelle-vergleichen-programm"), ("STUDIO-TRAIN-01", "ki-trainieren-programm") })
            {
                Assert.Equal(key, MainWindow.StudioPages[studio]);
                w.Show(key); Settle();
                RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle(); // Startseite mit Vorlagen
                Assert.NotNull(w.ProgramActive(key));
                w.ProgramNew(key, "leer"); w.Show(key); Settle(); // auch die leere Vorlage zeichnet ohne Fehler
                Assert.Equal("Unbenannt", w.ProgramActive(key)!.Title);
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Videos mit KI: Szenen planen, ohne Bild-KI klarer Hinweis, Bilder mit Herkunftsnachweis, eigenes Bild, Video mit Einblendung, nie überschreiben, Exporte")]
    public void VideoAi()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "video-ki-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var d = w.ProgramActive(K)!;
            RealClick(w, w.Find<Button>("prog-cmd-video-plan")); Settle();
            Assert.Equal(4, d.List("scenes").Count);
            Assert.Contains("4 Szenen", w.ProgramMessage(K));
            Assert.NotNull(w.Find<TextBox>("video-s3-caption"));
            Assert.Equal("Guten Morgen", w.Find<TextBox>("video-s3-caption").Text);
            var t0 = w.VideoBuild(d); WaitFor(() => t0.IsCompleted);
            Assert.Null(t0.Result); // ohne FFmpeg im Test oder ohne Bilder: nichts gebaut
            var t1 = w.VideoImages(d); WaitFor(() => t1.IsCompleted);
            Assert.Equal(0, t1.Result);
            Assert.Contains("nicht eingerichtet", w.ProgramMessage(K));

            // eigenes Bild für Szene 1: wird in den eigenen Ordner kopiert
            var mine = Path.Combine(root, "mein-bild.png"); File.WriteAllBytes(mine, Png1);
            w.PickOverride = _ => Task.FromResult<string[]?>([mine]);
            Tap(w, "video-s0-own"); WaitFor(() => ((System.Text.Json.Nodes.JsonObject)d.List("scenes")[0]!)["source"]?.ToString() == "eigen");
            var img0 = d.List("scenes")[0]!["image"]!.ToString();
            Assert.NotEqual(mine, img0); Assert.True(File.Exists(img0)); Assert.True(File.Exists(mine));

            var calls = new List<(string P, int Seed, int W, int H)>();
            MainWindow.ImageAiOverride = (p, seed, png, wi, he) => { calls.Add((p, seed, wi, he)); File.WriteAllBytes(png, Png1); return Task.FromResult(true); };
            List<string>? args = null; var capExisted = false;
            MainWindow.VideoAiFfmpegOverride = (a, outFile) => { args = [.. a]; capExisted = a.Where(x => x.Contains("text-")).All(File.Exists); File.WriteAllBytes(outFile, [0, 0, 0, 1]); return true; };
            try
            {
                var t2 = w.VideoImages(d); WaitFor(() => t2.IsCompleted);
                Assert.Equal(3, t2.Result); // Szene 1 hat schon ein eigenes Bild
                Assert.Equal(3, calls.Count);
                Assert.Contains("photorealistic", calls[0].P);
                Assert.Equal((1344, 768), (calls[0].W, calls[0].H)); // quer, SDXL-Größe
                Assert.All(d.List("scenes").Skip(1), s => Assert.True(File.Exists(s!["image"]!.ToString() + ".json")));
                w.Show(K); Settle();
                Assert.DoesNotContain(w.Find<StackPanel>("video-scenes").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "noch kein Bild");

                var mp4 = Path.Combine(root, "film.mp4");
                var t3 = w.VideoBuild(d, mp4); WaitFor(() => t3.IsCompleted);
                Assert.Equal(mp4, t3.Result);
                Assert.NotNull(args);
                Assert.Contains("-n", args!);
                Assert.Contains(args, a => a.Contains("xfade"));
                Assert.Equal(4 + 2, args.Count(a => a == "-i")); // vier Bilder, zwei Einblendungen
                Assert.True(capExisted);
                var prov = JsonDocument.Parse(File.ReadAllText(mp4 + ".json")).RootElement;
                Assert.Equal(4, prov.GetProperty("Scenes").GetArrayLength());
                Assert.Equal("eigen", prov.GetProperty("Scenes")[0].GetProperty("Source").GetString());
                Assert.Equal("ki", prov.GetProperty("Scenes")[1].GetProperty("Source").GetString());
                Assert.True(prov.GetProperty("AiGenerated").GetBoolean());
                // gleiche Datei noch einmal: wird nicht überschrieben
                args = null;
                var t4 = w.VideoBuild(d, mp4); WaitFor(() => t4.IsCompleted);
                Assert.Null(t4.Result); Assert.Null(args);
                Assert.Contains("nicht überschrieben", w.ProgramMessage(K));
                // Standardort: Ergebnis liegt im eigenen Video-Ordner und wird exportiert
                // gespeichert und wieder geöffnet: die Bilder gehören weiter zum Dokument
                var tdoc = w.ProgramSave(K, Path.Combine(root, "video.tdoc"));
                var reopened = Trendo.Core.Programs.ProgramDocument.Load(tdoc, "STUDIO-VIDEOAI-01");
                Assert.Equal(d.Text("dir"), reopened.Text("dir"));
                var t5 = w.VideoBuild(d); WaitFor(() => t5.IsCompleted);
                Assert.NotNull(t5.Result);
                Assert.Equal(4, File.ReadAllBytes(Export(w, K, "mp4", root)).Length);
                Assert.Contains("Einblendung „Guten Morgen“", File.ReadAllText(Export(w, K, "plan", root)));
                // zu lang: 5 × 15 s
                d.Set("idea", string.Join("\n", Enumerable.Range(1, 5).Select(i => $"Wiese {i}, 15 s")), "Idee");
                w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-cmd-video-plan")); Settle();
                Assert.Contains("länger als 60", w.ProgramMessage(K));
                Assert.Contains(w.Find<StackPanel>("video-checks").Children.OfType<TextBlock>(), t => t.Text!.StartsWith('✕'));
            }
            finally { MainWindow.ImageAiOverride = null; MainWindow.VideoAiFfmpegOverride = null; }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "KI-Agenten: Prüfung, Werkzeug mit Warnung, Speichern erst nach Ordnerwahl und nie überschreiben, Rolle im KI-Team, Export und Einlesen")]
    public void Agents()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(proj);
        try
        {
            const string K = "agenten-bauen-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            RealClick(w, w.Find<Button>("prog-cmd-agent-check")); Settle();
            Assert.StartsWith("✕", w.ProgramMessage(K));
            w.ProgramNew(K, "beispiel"); w.Show(K); Settle();
            var d = w.ProgramActive(K)!;
            RealClick(w, w.Find<Button>("prog-cmd-agent-check")); Settle();
            Assert.StartsWith("✓", w.ProgramMessage(K));
            Assert.StartsWith("---\nname: briefe-pruefen", w.Find<TextBox>("agent-md").Text);
            w.Find<CheckBox>("agent-tool-befehle").IsChecked = true; Settle();
            Assert.Contains("Bash", w.Find<TextBox>("agent-md").Text);
            Assert.Contains(w.Find<StackPanel>("agent-preview").Children.OfType<TextBlock>(), t => t.Text!.StartsWith("⚠ Vorsicht"));
            w.Find<CheckBox>("agent-tool-befehle").IsChecked = false; Settle();

            MainWindow.FolderPickOverride = _ => proj;
            try
            {
                var t = w.AgentSave(d); WaitFor(() => t.IsCompleted);
                var file = Path.Combine(proj, ".claude", "agents", "briefe-pruefen.md");
                Assert.Equal(file, t.Result);
                var (back, dropped) = AgentBlueprint.FromClaudeMarkdown(File.ReadAllText(file));
                Assert.Empty(dropped);
                Assert.Equal(["lesen", "suchen"], back.Tools);
                File.WriteAllText(file, "eigene Fassung");
                var t2 = w.AgentSave(d); WaitFor(() => t2.IsCompleted);
                Assert.Null(t2.Result);
                Assert.Equal("eigene Fassung", File.ReadAllText(file));
                Assert.Contains("nicht überschrieben", w.ProgramMessage(K));
            }
            finally { MainWindow.FolderPickOverride = null; }

            // Geheimnis im Text: nicht speicherbar, nicht exportierbar
            d.Set("instructions", d.Text("instructions") + "\nNutze den Schlüssel sk-ant-api03-abcdefghijklmnopqrstuvwx.", "Anweisungen");
            Assert.Throws<InvalidOperationException>(() => Export(w, K, "md", root));
            Assert.Throws<InvalidOperationException>(() => w.AgentToTeam(d));
            d.Undo();

            var role = w.AgentToTeam(d);
            Assert.Contains(w.Agents.CustomRoles(), r => r.Id == role.Id && r.Studio == "STUDIO-AGENTS-01" && !r.MayWriteFiles);
            Assert.Contains(w.Agents.RolesFor("STUDIO-AGENTS-01"), r => r.Id == role.Id);
            var roleJson = JsonDocument.Parse(File.ReadAllText(Export(w, K, "rolle", root))).RootElement;
            Assert.Equal("briefe-pruefen", roleJson.GetProperty("Name").GetString());
            var md = Export(w, K, "md", root);

            // Einlesen einer fremden Datei mit unbekanntem Werkzeug
            File.WriteAllText(md, File.ReadAllText(md).Replace("name: briefe-pruefen", "name: notizen-helfer").Replace("tools: Read, Grep, Glob", "tools: Read, NotebookEdit"));
            w.PickOverride = _ => Task.FromResult<string[]?>([md]);
            RealClick(w, w.Find<Button>("prog-cmd-agent-import")); WaitFor(() => d.Text("name") == "notizen-helfer");
            Assert.Equal(["lesen"], d.List("tools").Select(n => n!.ToString()));
            Assert.Contains("NotebookEdit", w.ProgramMessage(K));
            // fremder Helfer: erstes „Ausprobieren“ warnt nur, Claude bekommt noch nichts
            d.Set("probe", "Prüf diesen Satz.", "Probe");
            RealClick(w, w.Find<Button>("prog-cmd-agent-try")); Settle();
            Assert.StartsWith("⚠ Dieser Helfer stammt aus einer fremden Datei", w.ProgramMessage(K));
            Assert.Equal("ja", d.Text("fremd"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "KI-Modelle vergleichen: Quellen finden, fehlende Quelle ohne Absturz, Antworten gemischt und verdeckt, Sterne, Aufdecken, Ergebnistabelle, Exporte")]
    public void Models()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var a = new Ki("Quelle A", true, "Antwort von A mit fünf Wörtern", "a1");
        var b = new Ki("Quelle B", true, "Kurz von B", "b1", "b2");
        var c = new Ki("Quelle C", false, "", "c1");
        w.KiBackendsOverride = () => [a, b, c];
        try
        {
            const string K = "modelle-vergleichen-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var d = w.ProgramActive(K)!;
            var t0 = w.ModelsAsk(d); WaitFor(() => t0.IsCompleted);
            Assert.False(t0.Result); Assert.Contains("Quellen", w.ProgramMessage(K));
            var tf = w.ModelsFind(); WaitFor(() => tf.IsCompleted);
            Assert.Contains("3 Modelle", w.ProgramMessage(K));
            Assert.Contains(w.Find<StackPanel>("models-sources").Children.OfType<TextBlock>(), t => t.Text!.Contains("Quelle C: nicht erreichbar"));
            w.Find<CheckBox>("models-src-0").IsChecked = true; Settle(); // Quelle A · a1
            w.Find<CheckBox>("models-src-2").IsChecked = true; Settle(); // Quelle B · b2
            Assert.Equal(["Quelle A|a1", "Quelle B|b2"], d.List("sources").Select(n => n!.ToString()));
            // eine Quelle, die inzwischen weg ist
            d.Edit("Quelle", x => ((System.Text.Json.Nodes.JsonArray)x["sources"]!).Add("Quelle C|c1"));
            var t1 = w.ModelsAsk(d); WaitFor(() => t1.IsCompleted);
            Assert.True(t1.Result);
            Assert.Equal((1, 1), (a.Calls, b.Calls));
            Assert.Contains("2 von 3 Antworten", w.ProgramMessage(K));
            w.Show(K); Settle();
            var heads = Enumerable.Range(0, 3).Select(p => w.Find<TextBlock>($"models-head-0-{p}").Text!).ToList();
            Assert.All(heads, h => Assert.DoesNotContain("Quelle", h)); // verdeckt
            Assert.False(w.Find<Button>("models-reveal-0").IsEnabled);
            var round = JsonDocument.Parse(d.Data["rounds"]![0]!.ToJsonString()).RootElement;
            var order = round.GetProperty("order").EnumerateArray().Select(x => x.GetInt32()).ToList();
            var answers = round.GetProperty("answers").EnumerateArray().ToList();
            var posA = order.FindIndex(i => answers[i].GetProperty("source").GetString() == "Quelle A · a1");
            var posB = order.FindIndex(i => answers[i].GetProperty("source").GetString() == "Quelle B · b2");
            Assert.Contains("Antwort von A mit fünf Wörtern (a1)", answers[order[posA]].GetProperty("text").GetString());
            Tap(w, $"models-star-0-{posA}-5");
            Tap(w, $"models-star-0-{posB}-2");
            Assert.True(w.Find<Button>("models-reveal-0").IsEnabled); // die fehlende Quelle muss nicht bewertet werden
            Tap(w, "models-reveal-0");
            Assert.Contains("Quelle A · a1", w.Find<TextBlock>($"models-head-0-{posA}").Text);
            Assert.False(w.Find<Button>($"models-star-0-{posA}-1").IsEnabled);
            var table = w.Find<Grid>("models-table").Children.OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Equal("Quelle A · a1", table[6]); // erste Zeile nach den Spaltenköpfen: beste Quelle
            Assert.Contains("| Quelle A · a1 | 1 | 5,0 | 1 |", File.ReadAllText(Export(w, K, "md", root)));
            var csv = File.ReadAllText(Export(w, K, "csv", root));
            Assert.Contains(";Quelle C · c1;", csv);
            Assert.Contains(";nein;", csv);
            // zweite Frage, andere Mischung möglich, Tabelle über beide
            d.Set("question", "Was ist ein Komma?", "Frage");
            var t2 = w.ModelsAsk(d); WaitFor(() => t2.IsCompleted);
            Assert.Equal(2, d.List("rounds").Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Eigene KI trainieren: Beispiel trainieren, Genauigkeit und Verwechslungstabelle, Ausprobieren mit Sicherheit, Änderung verlangt neues Training, Modell und CSV laden, Exporte")]
    public void Train()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "ki-trainieren-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            RealClick(w, w.Find<Button>("prog-cmd-train-run")); Settle();
            Assert.Contains("zwei Kategorien", w.ProgramMessage(K));
            w.ProgramNew(K, "beispiel"); w.Show(K); Settle();
            var d = w.ProgramActive(K)!;
            Assert.Equal(30, d.List("examples").Count);
            RealClick(w, w.Find<Button>("prog-cmd-train-run")); Settle();
            Assert.StartsWith("✓ Trainiert mit 24 Beispielen, geprüft mit 6", w.ProgramMessage(K));
            var conf = w.Find<Grid>("train-confusion");
            Assert.Equal(9, conf.Children.OfType<Border>().Count()); // drei mal drei Felder
            Assert.Equal(3, conf.Children.OfType<TextBlock>().Count(t => Grid.GetRow(t) == 0)); // drei Spaltenköpfe
            w.Find<TextBox>("train-probe").Text = "Bitte zahlen Sie den offenen Betrag bis Montag"; Settle();
            Assert.StartsWith("Rechnung – ", w.Find<TextBlock>("train-probe-label").Text);
            var model = Export(w, K, "json", root);
            Assert.Equal(["Rechnung", "Termin", "Werbung"], TextClassifier.FromJson(File.ReadAllText(model)).Labels);
            Assert.Contains("## Verwechslungstabelle", File.ReadAllText(Export(w, K, "md", root)));
            var csv = Export(w, K, "csv", root);
            Assert.Equal(31, File.ReadAllLines(csv).Length);

            // Beispiel ändern: alte Prüfung gilt nicht mehr
            w.Find<TextBox>("train-label-0").Text = "Termin"; w.Find<TextBox>("train-label-0").Focus(); Settle();
            w.Find<TextBox>("train-text-1").Focus(); Settle();
            Assert.Equal("Termin", d.List("examples")[0]!["label"]!.ToString());
            Assert.Contains(w.Find<StackPanel>("train-result").Children.OfType<TextBlock>(), t => t.Text!.Contains("seit dem Training geändert"));
            Assert.Throws<InvalidOperationException>(() => Export(w, K, "md", root));

            // neues Dokument: Beispiele aus CSV, Modell aus Datei
            w.ProgramNew(K, "leer"); w.Show(K); Settle();
            var d2 = w.ProgramActive(K)!;
            w.PickOverride = _ => Task.FromResult<string[]?>([csv]);
            RealClick(w, w.Find<Button>("prog-cmd-train-import")); WaitFor(() => d2.List("examples").Count > 2);
            Assert.Equal(32, d2.List("examples").Count); // zwei leere Zeilen der Vorlage + 30
            w.PickOverride = _ => Task.FromResult<string[]?>([model]);
            w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Profi); w.Show(K); Settle();
            RealClick(w, w.Find<Button>("prog-cmd-train-load")); WaitFor(() => d2.Text("model").Length > 0);
            Assert.Contains("3 Kategorien", w.ProgramMessage(K));
            w.Show(K); Settle();
            Assert.Contains(w.Find<StackPanel>("train-result").Children.OfType<TextBlock>(), t => t.Text!.StartsWith("Geladenes Modell"));
            w.Find<TextBox>("train-probe").Text = "Nur heute Rabatt auf die neue Kollektion"; Settle();
            Assert.StartsWith("Werbung – ", w.Find<TextBlock>("train-probe-label").Text);
        }
        finally { w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Erweitert); w.Close(); }
    }
}
