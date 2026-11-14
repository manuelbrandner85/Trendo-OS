using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

public class KreativKits3Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-kk3-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    // ---------------- Videos mit KI ----------------

    [Fact(DisplayName = "Videos mit KI: Szenen je Zeile oder Absatz, Dauer, Bewegung und Einblendung erkannt, Gesamtlänge mit Überblendung, Prüfung bis 60 s")]
    public void VideoKi()
    {
        var s = VideoAiKit.Plan("1. Ein Leuchtturm bei Sonnenaufgang, Zoom rein, 5 s\n- Möwen über dem Meer, Schwenk nach rechts Text: Guten Morgen\n• Ein Boot im Hafen (3 Sekunden) ruhig\n\n");
        Assert.Equal(3, s.Count);
        Assert.Equal(("Ein Leuchtturm bei Sonnenaufgang", 5.0, "zoom-rein", ""), (s[0].Prompt, s[0].Seconds, s[0].Motion, s[0].Caption));
        Assert.Equal(("Möwen über dem Meer", "rechts", "Guten Morgen"), (s[1].Prompt, s[1].Motion, s[1].Caption));
        Assert.Equal((3.0, "ruhig"), (s[2].Seconds, s[2].Motion));
        Assert.Equal(VideoAiKit.DefaultScene, s[1].Seconds);
        // Absätze: Zeilen eines Absatzes gehören zusammen, ohne Angabe wechseln die Bewegungen
        var p = VideoAiKit.Plan("Ein Wald\nim Nebel\n\nEine Lichtung\n\nEin Bach");
        Assert.Equal(["Ein Wald im Nebel", "Eine Lichtung", "Ein Bach"], p.Select(x => x.Prompt));
        Assert.Equal(["zoom-rein", "rechts", "zoom-raus"], p.Select(x => x.Motion));
        Assert.Empty(VideoAiKit.Plan("  \n "));
        // Bewegung nur als ganzes Wort, Zahl mit „s“ in einem Wort ist keine Dauer
        var w = VideoAiKit.Plan("Ein ruhiges Meer im Stil der 1980s")[0];
        Assert.Equal(("Ein ruhiges Meer im Stil der 1980s", VideoAiKit.DefaultScene, "zoom-rein"), (w.Prompt, w.Seconds, w.Motion));
        Assert.Equal(VideoAiKit.MaxScene, VideoAiKit.Plan("Lange Szene 99 s")[0].Seconds); // gekappt
        Assert.Equal(VideoAiKit.MaxScenes, VideoAiKit.Plan(string.Join("\n", Enumerable.Range(1, 30).Select(i => "Szene " + i))).Count);

        Assert.Equal(0, VideoAiKit.Total([]));
        Assert.Equal(4, VideoAiKit.Total([4]));
        Assert.Equal(11, VideoAiKit.Total([4, 4, 4])); // 12 s minus zwei Überblendungen à 0,5 s
        Assert.Contains(VideoAiKit.Check([], false), x => x.StartsWith('✕'));
        var tooLong = Enumerable.Range(0, 5).Select(i => new VideoAiKit.Scene("Wiese", 15, "ruhig", "")).ToList(); // 75 − 2 = 73 s
        Assert.Contains(VideoAiKit.Check(tooLong, false), x => x.StartsWith('✕') && x.Contains("höchstens 60"));
        Assert.Empty(VideoAiKit.Check(tooLong.Take(4).ToList(), false)); // 60 − 1,5 = 58,5 s
        Assert.Contains(VideoAiKit.Check([new("porn", 4, "ruhig", "")], false), x => x.StartsWith('✕'));
        Assert.Empty(VideoAiKit.Check([new("porn", 4, "ruhig", "", @"C:\eigen.png")], false)); // eigenes Bild: keine Beschreibung nötig
        Assert.Contains(VideoAiKit.Check([new("Wiese", 4, "ruhig", "")], true), x => x.StartsWith('⚠') && x.Contains("Bild"));
        Assert.Contains(VideoAiKit.Check([new("Wiese", 4, "kreisen", "")], false), x => x.Contains("Unbekannte Bewegung"));
        Assert.Contains("2. Möwen über dem Meer – 4 s, Schwenk nach rechts, Einblendung „Guten Morgen“", VideoAiKit.PlanText(s, "Morgen"));
    }

    [Fact(DisplayName = "Videos mit KI: FFmpeg-Argumente – nie überschreiben, Bilder vor Einblendungen, zoompan je Bewegung, Überblendungen mit richtigem Versatz")]
    public void VideoKi_Args()
    {
        var clips = new List<VideoAiKit.Clip> { new("a.png", 4, "zoom-rein"), new("b.png", 4, "links", "cap-b.png"), new("c.png", 3, "ruhig") };
        var a = VideoAiKit.FfmpegArgs(clips, 1280, 720, "out.mp4");
        Assert.Equal(["-hide_banner", "-nostdin", "-n"], a.Take(3));
        Assert.DoesNotContain("-y", a);
        Assert.Equal(["a.png", "b.png", "c.png", "cap-b.png"], a.Select((x, i) => (x, i)).Where(t => t.i > 0 && a[t.i - 1] == "-i").Select(t => t.x));
        var f = a[a.ToList().IndexOf("-filter_complex") + 1];
        Assert.Contains("[0:v]scale=2560:1440", f);
        Assert.Contains("zoompan=z='1+0.25*on/119'", f); // 4 s × 30 Bilder
        Assert.Contains("x='(iw-iw/zoom)*(1-on/119)'", f); // Schwenk nach links
        Assert.Contains("[b1][3:v]overlay=0:0", f); // Einblendung der zweiten Szene ist Eingang 3
        Assert.Contains("[s0][s1]xfade=transition=fade:duration=0.5:offset=3.5[x1]", f);
        Assert.Contains("[x1][s2]xfade=transition=fade:duration=0.5:offset=7[vout]", f);
        Assert.Equal("out.mp4", a[^1]);
        Assert.Contains("[s0]null[vout]", VideoAiKit.Filter([new("a.png", 2, "zoom-raus")], 720, 1280));
        Assert.Throws<ArgumentException>(() => VideoAiKit.FfmpegArgs([], 1280, 720, "o.mp4"));
        Assert.Throws<ArgumentException>(() => VideoAiKit.FfmpegArgs(Enumerable.Range(0, 5).Select(_ => new VideoAiKit.Clip("a.png", 15, "ruhig")).ToList(), 1280, 720, "o.mp4"));
        Assert.Equal((720, 1280), VideoAiKit.Size("hoch"));
        Assert.Equal((1280, 720), VideoAiKit.Size("unbekannt"));
    }

    [FfmpegFact(DisplayName = "Videos mit KI echt mit FFmpeg: drei Standbilder mit Einblendung ergeben ein MP4 mit der geplanten Länge, vorhandene Datei bleibt")]
    public void VideoKi_Ffmpeg()
    {
        var ff = Cli.Find("ffmpeg")!; var dir = Tmp();
        try
        {
            string Img(string name, string color, string size = "640x480")
            {
                var f = Path.Combine(dir, name);
                var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", $"color=c={color}:s={size}", "-frames:v", "1", f], dir, 60000);
                Assert.True(File.Exists(f), r.Err); return f;
            }
            var clips = new List<VideoAiKit.Clip> { new(Img("a.png", "red"), 2, "zoom-rein"), new(Img("b.png", "green", "300x900"), 2, "rechts", Img("cap.png", "black@0.5", "640x360")), new(Img("c.png", "blue"), 1.5, "ruhig") };
            var mp4 = Path.Combine(dir, "video.mp4");
            var run = Cli.Run(ff, VideoAiKit.FfmpegArgs(clips, 640, 360, mp4), dir, 120000);
            Assert.True(run.Ok && File.Exists(mp4), run.Err);
            var probe = Cli.Run(ff, ["-hide_banner", "-i", mp4], dir, 60000).Err;
            var m = Regex.Match(probe, @"Duration: (\d+):(\d+):(\d+\.\d+)");
            Assert.True(m.Success, probe);
            var sec = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) + 60 * int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            Assert.InRange(sec, VideoAiKit.Total([2, 2, 1.5]) - 0.15, VideoAiKit.Total([2, 2, 1.5]) + 0.15);
            Assert.Contains("640x360", probe);
            var size = new FileInfo(mp4).Length;
            var before = File.ReadAllBytes(mp4); File.SetLastWriteTimeUtc(mp4, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Cli.Run(ff, VideoAiKit.FfmpegArgs(clips, 640, 360, mp4), dir, 120000); // „-n“: vorhandene Datei bleibt unangetastet
            Assert.Equal(before, File.ReadAllBytes(mp4)); Assert.Equal(2026, File.GetLastWriteTimeUtc(mp4).Year); Assert.Equal(1, File.GetLastWriteTimeUtc(mp4).Month);
            Assert.Equal(size, new FileInfo(mp4).Length);
            var prov = VideoAiKit.WriteProvenance(mp4, new("Trendo", [new("rot", 2, "zoom-rein", "", "eigen", "")], 640, 360, 4.5, DateTime.UtcNow, ""));
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(mp4))), JsonDocument.Parse(File.ReadAllText(prov)).RootElement.GetProperty("Sha256").GetString(), ignoreCase: true);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---------------- KI-Agenten bauen ----------------

    static AgentBlueprint Agent(params string[] tools) => new("texte-pruefen", "Prüft Texte auf Rechtschreibung: ruft man bei \"Korrektur\"", "Lies den Text genau.\nMarkiere Fehler und schlag Verbesserungen vor.", tools.Length == 0 ? ["lesen", "suchen"] : tools, "Eingabe: Ein Brief\nAusgabe: Liste der Fehler", "Nichts löschen.\nNichts senden.");

    [Fact(DisplayName = "KI-Agenten: Prüfung von Kurzname, Pflichtfeldern, Werkzeugen und Geheimnissen; Befehle nur mit Warnung")]
    public void Agenten()
    {
        Assert.True(Agent().Ok);
        Assert.Equal("texte-pruefen", AgentBlueprint.Slug("Texte prüfen!"));
        Assert.Equal("helfer-3d-modelle", AgentBlueprint.Slug("3D Modelle"));
        Assert.Contains((Agent() with { Name = "Texte Prüfen" }).Check(), f => f.Field == "name" && f.Error && f.Message.Contains("texte-pruefen"));
        Assert.Contains((Agent() with { Name = "" }).Check(), f => f.Field == "name" && f.Error);
        Assert.Contains((Agent() with { Description = "" }).Check(), f => f.Field == "description" && f.Error);
        Assert.Contains((Agent() with { Description = new string('a', 301) }).Check(), f => f.Field == "description" && f.Error);
        Assert.Contains((Agent() with { Instructions = "kurz" }).Check(), f => f.Field == "instructions" && f.Error);
        Assert.Contains((Agent() with { Tools = [] }).Check(), f => f.Field == "tools" && f.Error); // ohne Liste dürfte er alles
        Assert.Contains((Agent() with { Tools = ["lesen", "root"] }).Check(), f => f.Error && f.Message.Contains("root"));
        var bash = Agent("lesen", "befehle").Check();
        Assert.True(bash.All(f => !f.Error));
        Assert.Contains(bash, f => f.Field == "tools" && !f.Error && f.Message.StartsWith("Vorsicht"));
        foreach (var secret in new[] { "sk-ant-api03-abcdefghijklmnopqrstuvwx", "sk-proj-ABCDEFGHIJKLMNOPQRST", "AKIAABCDEFGHIJKLMNOP", "ghp_" + new string('a', 36), "Passwort: geheim123", "api_key = 0123456789abcdef", "-----BEGIN RSA PRIVATE KEY-----" })
        {
            Assert.True(AgentBlueprint.HasSecret(secret), secret);
            Assert.Contains((Agent() with { Instructions = "Nutze den Zugang " + secret + " für alles." }).Check(), f => f.Field == "instructions" && f.Error);
        }
        Assert.False(AgentBlueprint.HasSecret("Passwort: niemals weitergeben"));
        Assert.Contains((Agent() with { Name = "yes" }).Check(), f => f.Field == "name" && f.Error);
        Assert.Contains((Agent() with { Description = "Eins\rZwei" }).Check(), f => f.Field == "description" && f.Error);
        Assert.False(AgentBlueprint.HasSecret("Frag nach dem Passwort nie im Chat. Die Taste sk ist kein Schlüssel."));
        Assert.Contains((Agent() with { Limits = "" }).Check(), f => f.Field == "limits" && !f.Error);
    }

    [Fact(DisplayName = "KI-Agenten: Export als Claude-Code-Subagent und Wieder-Einlesen ergeben denselben Helfer; fremde Werkzeuge werden gemeldet")]
    public void Agenten_Rundlauf()
    {
        var a = Agent("lesen", "schreiben", "befehle");
        var md = a.ToClaudeMarkdown();
        Assert.StartsWith("---\nname: texte-pruefen\ndescription: \"Prüft Texte auf Rechtschreibung: ruft man bei \\\"Korrektur\\\"\"\ntools: Read, Edit, Write, Bash\n---\n\nLies den Text genau.", md);
        Assert.Contains("\n## Beispiele\n\nEingabe: Ein Brief", md);
        Assert.Contains("\n## Grenzen\n\nNichts löschen.\nNichts senden.\n", md);
        var (b, dropped) = AgentBlueprint.FromClaudeMarkdown(md);
        Assert.Empty(dropped);
        Assert.Equal((a.Name, a.Description, a.Instructions, a.Examples, a.Limits), (b.Name, b.Description, b.Instructions, b.Examples, b.Limits));
        Assert.Equal(a.Tools.Order(), b.Tools.Order());
        Assert.Equal(md, b.ToClaudeMarkdown());
        // Datei von Hand mit Windows-Zeilenenden, unbekanntem Werkzeug, ohne Abschnitte
        var (c, d2) = AgentBlueprint.FromClaudeMarkdown("\uFEFF---\r\nname: notizen\r\ndescription: Hilft bei Notizen\r\ntools: Read, Grep, NotebookEdit\r\nmodel: sonnet\r\n---\r\nSchreib kurze Notizen.\r\n");
        Assert.Equal(("notizen", "Hilft bei Notizen", "Schreib kurze Notizen.", "", ""), (c.Name, c.Description, c.Instructions, c.Examples, c.Limits));
        Assert.Equal(["lesen", "suchen"], c.Tools);
        Assert.Equal(["NotebookEdit"], d2);
        Assert.Throws<InvalidDataException>(() => AgentBlueprint.FromClaudeMarkdown("Nur Text"));
        Assert.Equal("Nur Anweisungen.", AgentBlueprint.FromClaudeMarkdown("---\n---\nNur Anweisungen.").Agent.Instructions); // leerer Kopf
        Assert.Throws<InvalidDataException>(() => AgentBlueprint.FromClaudeMarkdown("---\nname: x\n"));
        var role = a.ToTrendoRole();
        Assert.Equal("texte-pruefen", role.Name);
        Assert.True(role.MayWriteFiles);
        Assert.Contains("Grenzen: Nichts löschen. Nichts senden.", role.Focus);
        Assert.False(Agent().ToTrendoRole().MayWriteFiles);
    }

    [Fact(DisplayName = "KI-Agenten: Speichern legt .claude/agents/<name>.md an und überschreibt nie; ungültiger Helfer wird nicht gespeichert")]
    public void Agenten_Speichern()
    {
        var dir = Tmp();
        try
        {
            var f = Agent().SaveTo(dir);
            Assert.Equal(Path.Combine(dir, ".claude", "agents", "texte-pruefen.md"), f);
            Assert.Equal(Agent().ToClaudeMarkdown(), File.ReadAllText(f));
            File.WriteAllText(f, "eigene Fassung");
            Assert.Throws<IOException>(() => Agent().SaveTo(dir));
            Assert.Equal("eigene Fassung", File.ReadAllText(f));
            Assert.Throws<InvalidOperationException>(() => (Agent() with { Name = "andere", Instructions = "Schlüssel sk-ant-api03-abcdefghijklmnopqrstuvwx" }).SaveTo(dir));
            Assert.False(File.Exists(Path.Combine(dir, ".claude", "agents", "andere.md")));
            Assert.Throws<ArgumentException>(() => Agent().SaveTo(@"\\rechner\freigabe"));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---------------- KI-Modelle vergleichen ----------------

    [Fact(DisplayName = "KI-Modelle vergleichen: Mischen fest je Startwert, Sterne nur 1–5, Aufdecken erst nach allen Sternen, Tabelle mit Schnitt und Siegen, Berichte")]
    public void Modelle()
    {
        var sh = ModelCompare.Shuffle(5, 42);
        Assert.Equal(sh, ModelCompare.Shuffle(5, 42));
        Assert.Equal([0, 1, 2, 3, 4], sh.Order());
        Assert.Contains(Enumerable.Range(1, 20), s => !ModelCompare.Shuffle(5, s).SequenceEqual(sh)); // anderer Startwert, andere Reihenfolge
        Assert.Contains(Enumerable.Range(1, 20), s => ModelCompare.Shuffle(3, s)[0] != 0); // nicht immer die erste Quelle vorn
        Assert.Empty(ModelCompare.Shuffle(0, 1));
        Assert.Equal(3, ModelCompare.Words("Das ist gut."));
        Assert.Equal(2.5, ModelCompare.WordsPerSecond(new("x", "eins zwei drei vier fünf", 2, true)));
        Assert.Equal(0, ModelCompare.WordsPerSecond(new("x", "", 0, false)));

        var ans = new List<ModelCompare.Answer> { new("Quelle A", "Berlin ist die Hauptstadt.", 2, true), new("Quelle B", "Berlin.", 1, true), new("Quelle C", "Nicht erreichbar", 0, false) };
        var r = ModelCompare.NewRound("Hauptstadt?", ans, 7);
        Assert.False(ModelCompare.CanReveal(r));
        Assert.Throws<ArgumentException>(() => ModelCompare.Rate(r, 0, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModelCompare.Rate(r, 3, 4));
        int Pos(string src) => r.Order.ToList().FindIndex(i => ans[i].Source == src);
        r = ModelCompare.Rate(r, Pos("Quelle A"), 5);
        Assert.False(ModelCompare.CanReveal(r));
        Assert.DoesNotContain("Quelle A", ModelCompare.Markdown([r], "Test").Split("## Frage 1")[1]); // verdeckt
        Assert.Contains("verdeckt", ModelCompare.Csv([r]));
        Assert.Throws<InvalidOperationException>(() => ModelCompare.Reveal(r));
        r = ModelCompare.Rate(r, Pos("Quelle B"), 3);
        Assert.True(ModelCompare.CanReveal(r)); // die fehlgeschlagene Quelle muss nicht bewertet werden
        r = ModelCompare.Reveal(r);
        Assert.Throws<InvalidOperationException>(() => ModelCompare.Rate(r, 0, 1));

        var r2 = ModelCompare.NewRound("Zwei plus zwei?", [new("Quelle A", "Vier.", 1, true), new("Quelle B", "Das ergibt vier.", 2, true)], 8);
        r2 = ModelCompare.Rate(ModelCompare.Rate(r2, 0, 4), 1, 4); // Gleichstand
        r2 = ModelCompare.Reveal(r2);
        var r3 = ModelCompare.NewRound("Noch offen", [new("Quelle A", "x", 1, true)], 9); // nicht aufgedeckt: zählt nicht für Sterne
        var t = ModelCompare.Table([r, r2, r3]);
        Assert.Equal(["Quelle A", "Quelle B", "Quelle C"], t.Select(x => x.Source));
        var a = t[0];
        Assert.Equal((3, 2, 4.5, 2, 1.33, 0), (a.Rounds, a.Rated, a.AvgStars, a.Wins, a.AvgSeconds, a.Failures));
        Assert.Equal((3.5, 1), (t[1].AvgStars, t[1].Wins));
        Assert.Equal((0, 0, 1), (t[2].Rated, t[2].Wins, t[2].Failures));

        var md = ModelCompare.Markdown([r, r2], "Vergleich");
        Assert.Contains("| Quelle A | 2 | 4,5 | 2 |", md);
        Assert.Contains("– Quelle B", md);
        Assert.Contains("Keine Antwort", md);
        var csv = ModelCompare.Csv([r, r2]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(6, csv.Length);
        Assert.StartsWith("frage;quelle;buchstabe", csv[0]);
        Assert.Contains(csv, l => l.StartsWith("Hauptstadt?;Quelle A;") && l.EndsWith(";5;ja"));
        Assert.Contains("\"a;b\"", ModelCompare.Csv([ModelCompare.NewRound("a;b", [new("Q", "x", 1, true)], 1)]));
    }

    // ---------------- Eigene KI trainieren ----------------

    [Fact(DisplayName = "Eigene KI: Wortzerlegung klein, mit Umlauten, Bindestrich trennt, Füllwörter weg")]
    public void Training_Woerter()
    {
        Assert.Equal(["größe", "übermaß", "mail", "2026"], TextClassifier.Tokenize("Die Größe und das Übermaß der E-Mail 2026!"));
        Assert.Equal(["die", "katze"], TextClassifier.Tokenize("Die Katze", removeStopwords: false));
        Assert.Empty(TextClassifier.Tokenize(null));
    }

    [Fact(DisplayName = "Eigene KI: bekanntes Lehrbuch-Beispiel (Manning u. a., Kap. 13) ergibt genau die erwartete Wahrscheinlichkeit")]
    public void Training_Lehrbuch()
    {
        // Introduction to Information Retrieval, Beispiel 13.1: P(ja|d5) ∝ 3/4·(3/7)³·(1/14)², P(nein|d5) ∝ 1/4·(2/9)⁵
        var m = TextClassifier.Train([new("Chinese Beijing Chinese", "ja"), new("Chinese Chinese Shanghai", "ja"), new("Chinese Macao", "ja"), new("Tokyo Japan Chinese", "nein")], removeStopwords: false);
        Assert.Equal(6, m.VocabularySize);
        var p = m.Predict("Chinese Chinese Chinese Tokyo Japan");
        var yes = 0.75 * Math.Pow(3.0 / 7, 3) * Math.Pow(1.0 / 14, 2); var no = 0.25 * Math.Pow(2.0 / 9, 5);
        Assert.Equal("ja", p.Label);
        Assert.Equal(yes / (yes + no), p.Confidence, 10);
        Assert.Equal(1, p.All.Sum(x => x.Probability), 10);
        Assert.Equal(5, p.KnownWords);
        Assert.Equal(0.75, m.Predict("völlig unbekannt").Confidence, 10); // nur die Häufigkeit der Kategorie zählt
        Assert.Throws<InvalidOperationException>(() => TextClassifier.Train([new("a b", "x"), new("c d", "x")]));
        Assert.Throws<ArgumentException>(() => TextClassifier.Train([new("a b", "x"), new("c d", "y")], alpha: 0));
    }

    static List<TextClassifier.Example> Mails()
    {
        string[] money = ["rechnung", "betrag", "überweisung", "zahlung", "mahnung", "konto", "euro", "fällig"];
        string[] meet = ["termin", "uhrzeit", "treffen", "kalender", "besprechung", "montag", "verschieben", "raum"];
        var l = new List<TextClassifier.Example>();
        for (var i = 0; i < 10; i++)
        {
            l.Add(new($"Bitte {money[i % 8]} und {money[(i + 3) % 8]} prüfen, {money[(i + 5) % 8]}", "Geld"));
            l.Add(new($"Wir brauchen {meet[i % 8]} und {meet[(i + 3) % 8]} für {meet[(i + 5) % 8]}", "Termin"));
        }
        return l;
    }

    [Fact(DisplayName = "Eigene KI: feste Aufteilung je Kategorie, klar trennbare Daten ergeben 100 %, Verwechslungstabelle, Präzision und Trefferquote stimmen")]
    public void Training()
    {
        var all = Mails();
        var (tr, te) = TextClassifier.Split(all, 0.2, 42);
        Assert.Equal((16, 4), (tr.Count, te.Count));
        Assert.Equal(2, te.Count(e => e.Label == "Geld"));
        var (tr2, te2) = TextClassifier.Split(all, 0.2, 42);
        Assert.Equal(te.Select(e => e.Text), te2.Select(e => e.Text)); // gleicher Startwert, gleiche Aufteilung
        Assert.Contains(Enumerable.Range(1, 10), s => !TextClassifier.Split(all, 0.2, s).Test.Select(e => e.Text).SequenceEqual(te.Select(e => e.Text)));
        Assert.Empty(TextClassifier.Split(all, 0, 1).Test);
        Assert.Single(TextClassifier.Split([new("a", "x"), new("b", "x"), new("c", "y")], 0.1, 1).Test); // mindestens eins, wenn zwei da sind; „y“ hat nur eins
        var m = TextClassifier.Train(tr);
        var e = m.Evaluate(te);
        Assert.Equal(1.0, e.Accuracy);
        Assert.Equal(["Geld", "Termin"], e.Labels);
        Assert.Equal(2, e.Confusion[0, 0]); Assert.Equal(0, e.Confusion[0, 1]);

        // von Hand: eine Termin-Mail klingt nach Geld → 3 von 4 richtig
        var m2 = TextClassifier.Train(all);
        var test = new List<TextClassifier.Example> { new("Die Rechnung ist fällig", "Geld"), new("Überweisung auf mein Konto", "Geld"), new("Termin am Montag im Raum", "Termin"), new("Zahlung für den Betrag", "Termin") };
        var ev = m2.Evaluate(test);
        Assert.Equal(0.75, ev.Accuracy);
        Assert.Equal(2, ev.Confusion[0, 0]); Assert.Equal(1, ev.Confusion[1, 0]); Assert.Equal(1, ev.Confusion[1, 1]);
        var geld = ev.PerClass.Single(c => c.Label == "Geld"); var termin = ev.PerClass.Single(c => c.Label == "Termin");
        Assert.Equal((2, 0.6667, 1.0, 0.8), (geld.Support, geld.Precision, geld.Recall, geld.F1));
        Assert.Equal((1.0, 0.5), (termin.Precision, termin.Recall));
        var rep = TextClassifier.Report(ev, 20, ["⚠ Hinweis"], "Post");
        Assert.Contains("Genauigkeit: **75 %**", rep);
        Assert.Contains("| Termin | 1 | 1 |", rep);
        Assert.Contains("- ⚠ Hinweis", rep);
        var p = m2.Predict("Bitte die Mahnung und den Betrag prüfen");
        Assert.Equal("Geld", p.Label); Assert.True(p.Confidence > 0.9);
    }

    [Fact(DisplayName = "Eigene KI: Hinweise bei wenigen Beispielen, CSV-Import mit Kopfzeile und Anführungszeichen, Modell als JSON im Rundlauf")]
    public void Training_Datei()
    {
        var w = TextClassifier.Warnings([new("a b", "x"), new("c d", "x"), new("e f", "y"), new("g h", ""), new("!!", "y")]);
        Assert.Contains(w, x => x.StartsWith('⚠') && x.Contains("„x“ hat nur 2 Beispiele"));
        Assert.Contains(w, x => x.Contains("1 Beispiel ohne Kategorie"));
        Assert.Contains(w, x => x.Contains("ohne verwertbare Wörter"));
        Assert.Contains(TextClassifier.Warnings([new("a b", "x")]), x => x.StartsWith('✕'));
        Assert.Empty(TextClassifier.Warnings(Mails()));

        var csv = TextClassifier.FromCsv("\uFEFFtext;kategorie\n\"Hallo; wie geht's\";Gruß\nRechnung offen;Geld\n;\nNur Text\n");
        Assert.Equal([("Hallo; wie geht's", "Gruß"), ("Rechnung offen", "Geld")], csv.Select(e => (e.Text, e.Label)));
        Assert.Single(TextClassifier.FromCsv("a,b\nc,d", 1));

        var m = TextClassifier.Train(Mails());
        var back = TextClassifier.FromJson(m.ToJson());
        Assert.Equal(m.Labels, back.Labels);
        Assert.Equal(m.VocabularySize, back.VocabularySize);
        foreach (var t in new[] { "Termin verschieben", "Mahnung Konto", "etwas ganz anderes" })
            Assert.Equal(m.Predict(t).Confidence, back.Predict(t).Confidence, 12);
        Assert.Throws<InvalidDataException>(() => TextClassifier.FromJson("{}"));
        // fremde, präparierte Datei: leere Einträge und überlaufende Zahlen werden abgelehnt, nicht abgestürzt
        Assert.Throws<InvalidDataException>(() => TextClassifier.FromJson("{\"Format\":\"trendo-textklassifikator\",\"Version\":1,\"Alpha\":1,\"RemoveStopwords\":true,\"Docs\":{\"a\":1,\"b\":1},\"Counts\":{\"a\":null}}"));
        Assert.Throws<InvalidDataException>(() => TextClassifier.FromJson("{\"Format\":\"trendo-textklassifikator\",\"Version\":1,\"Alpha\":1,\"RemoveStopwords\":true,\"Docs\":{\"a\":2147483647,\"b\":2147483647},\"Counts\":{}}"));
        Assert.Throws<InvalidDataException>(() => TextClassifier.FromJson("kein json"));
        Assert.Throws<InvalidDataException>(() => TextClassifier.FromJson(m.ToJson().Replace("\"Version\": 1", "\"Version\": 9")));
    }
}

public partial class PromptActionTests
{
    [Fact(DisplayName = "Kreativ & KI Teil 3: Szenenplan, Agent-Datei, Fragebogen und trainiertes Modell entstehen als echte Dateien im Projekt")]
    public void Kreativ_Studios3_schreiben_Dateien()
    {
        using var fx = new Fixture();
        var wf = new StudioWorkflow(fx.Core, PromptActions.CreateDefault());
        string Run(string studio, string prompt, string file, string id)
        {
            var done = wf.Los(fx.Project, wf.Start(fx.Req(studio, prompt, id: id), fx.Ctx()).RunId, fx.Ctx());
            Assert.Equal(RunStatus.Verified, done.Status);
            return File.ReadAllText(fx.Core.Projects.ResolveInProject(fx.Project, done.Artifacts.Single(a => a.RelativePath.EndsWith(file)).RelativePath));
        }
        Assert.Contains("2. Möwen – 3 s", Run("STUDIO-VIDEOAI-01", "Titel: Morgen am Meer\nSzenen:\nEin Leuchtturm 5 s\nMöwen 3 s", "szenenplan.md", "vid-1"));
        var md = Run("STUDIO-AGENTS-01", "Name: Texte prüfen\nAufgabe: Prüft Briefe auf Fehler", "texte-pruefen.md", "agt-1");
        Assert.StartsWith("---\nname: texte-pruefen\n", md);
        Assert.Contains("tools: Read, Grep, Glob", md);
        Assert.Contains("2. Was ist ein Komma?", Run("STUDIO-MODELS-01", "Fragen:\nWas ist ein Satz?\nWas ist ein Komma?", "fragebogen.md", "mod-1"));
        var json = Run("STUDIO-TRAIN-01", "Beispiele:\nRechnung offen = Geld\nBetrag fällig = Geld\nTermin am Montag = Termin\nTreffen verschieben = Termin", "modell.json", "trn-1");
        Assert.Equal(["Geld", "Termin"], Trendo.Core.Kreativ.TextClassifier.FromJson(json).Labels);
        var ad = PromptActions.CreateDefault().Single(a => a.StudioId == "STUDIO-AGENTS-01");
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["name"] = "x", ["aufgabe"] = "y", ["anweisungen"] = "Nutze sk-ant-api03-abcdefghijklmnopqrstuvwx immer und überall." }));
        Assert.NotEmpty(PromptActions.CreateDefault().Single(a => a.StudioId == "STUDIO-TRAIN-01").Validate(new Dictionary<string, string> { ["beispiele"] = "a = x\nb = x" }));
        Assert.All(PromptActions.CreateDefault().Where(a => a.StudioId is "STUDIO-VIDEOAI-01" or "STUDIO-AGENTS-01" or "STUDIO-MODELS-01" or "STUDIO-TRAIN-01"), a => Assert.False(a.CanPerformSideEffect));
    }
}
