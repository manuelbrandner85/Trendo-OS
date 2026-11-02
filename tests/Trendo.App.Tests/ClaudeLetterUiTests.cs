using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.43 – Briefe und Bewerbungen mit Design: im Programm wählbar (Stil, Farbe, Kopf, Zeile, Foto), in Vorschau und Word-Datei; Claude arbeitet im offenen Brief (Text und Design). Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeLetter_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    [AvaloniaFact(DisplayName = "Brief-Design im Programm: Stil „Modern“ mit Farbband, Farbe, Zeile unter dem Namen – Vorschau zeigt es, Word-Datei auch; klassisch bleibt reiner DIN-5008-Brief")]
    public void Design_in_program()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show("brief");
            Assert.Null(w.TryFind<Rectangle>("letter-design-band")); // klassisch
            w.Find<Button>("letter-design-style-modern").Press();
            Assert.Equal(("modern", "band"), (w.Letter.Design, w.Letter.Header));
            Assert.NotNull(w.TryFind<Rectangle>("letter-design-band"));
            w.Find<Button>("letter-design-color-0f2e5c").Press();
            Assert.Equal("#0F2E5C", w.Letter.Accent);
            w.LetterSetDesign("modern", tagline: "Zahnmedizinische Fachangestellte"); w.Show("brief");
            Assert.Equal("Zahnmedizinische Fachangestellte", w.Find<TextBlock>("letter-preview-tagline").Text);
            var docx = Path.Combine(root, "bewerbung.docx");
            w.Letter.SenderName = "Erika Muster"; w.Letter.Subject = "Bewerbung als ZFA";
            Letters.WriteDocx(w.Letter, docx);
            using var z = ZipFile.OpenRead(docx); using var r = new StreamReader(z.GetEntry("word/document.xml")!.Open());
            var xml = r.ReadToEnd();
            Assert.Contains("w:fill=\"0F2E5C\"", xml); Assert.Contains("Segoe UI", xml); Assert.Contains("Zahnmedizinische Fachangestellte", xml);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude im Brief-Studio: arbeitet im offenen Brief (Brief.json) – Text und Design (z. B. Bewerbung als ZFA, zeitgemäß in Blau) erscheinen sofort im Brief-Programm")]
    public async Task Claude_designs_letter()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = (args, dir, line, _) =>
        {
            calls.Add(args);
            var file = Path.Combine(dir, "Brief.json");
            var o = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            o["Subject"] = "Bewerbung als Zahnmedizinische Fachangestellte"; o["Body"] = "mit großem Interesse …";
            o["Design"] = "modern"; o["Accent"] = "#1F5FA8"; o["Header"] = "band"; o["Font"] = "Segoe UI"; o["Tagline"] = "Zahnmedizinische Fachangestellte";
            line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = file, content = "…" } } } } }));
            File.WriteAllText(file, o.ToJsonString());
            line(J(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = "Fertig.", session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("brief"); w.ClaudeDockToggle(MainWindow.LetterStudio); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Schreib mir eine Bewerbung als ZFA, zeitgemäß in Blau mit schönen Elementen"));
            var sys = calls[0].ToList(); Assert.Contains("Brief.json", sys[sys.IndexOf("--append-system-prompt") + 1]); Assert.Contains("Header = keiner | band", sys[sys.IndexOf("--append-system-prompt") + 1]);
            Assert.Equal("brief", w.CurrentView);
            Assert.Equal("Bewerbung als Zahnmedizinische Fachangestellte", w.Letter.Subject);
            Assert.Equal(("modern", "band", "#1F5FA8"), (w.Letter.Design, w.Letter.Header, w.Letter.Accent));
            w.Show("brief");
            Assert.NotNull(w.TryFind<Rectangle>("letter-design-band")); Assert.Equal("Zahnmedizinische Fachangestellte", w.Find<TextBlock>("letter-preview-tagline").Text);
            Assert.Single(calls); // im Brief gearbeitet – kein Zurückschicken
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
