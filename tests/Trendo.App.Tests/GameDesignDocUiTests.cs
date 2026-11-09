using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0610: Ideen finden, geführter Weg nur mit Knöpfen, Spielbibel als Dokument, Chat je Abschnitt.</summary>
public class GameDesignDoc_Ui
{
    const string GK = "spiele-programm";
    const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    static async Task Settle() { Pump(); await Task.Delay(50); Pump(); }

    [AvaloniaFact(DisplayName = "Ideen: geführter Weg vom leeren Studio bis zum Spiel ohne Texteingabe; ohne KI aus dem Baukasten")]
    public async Task Path_without_typing()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.GamePromptOverride = (_, _) => throw new InvalidOperationException("keine KI");
        try
        {
            w.Show(GK); Pump();
            Assert.NotNull(w.Find<Border>("game-ideas-card"));
            w.Find<Button>("game-path-start").Press(); Pump();
            w.Find<Button>("game-path-0-2").Press(); Pump(); // Lustig
            w.Find<Button>("game-path-1-2").Press(); Pump(); // Knobeln
            w.Find<Button>("game-path-2-0").Press(); await Settle(); // klein
            Assert.True(w.GameIdeas.Count >= 3);
            Assert.All(w.GameIdeas, i => { Assert.Equal("Puzzle", i.Genre); Assert.Equal("retro", i.Scale); });
            var idea = w.GameIdeas[0];
            w.Find<Button>("game-idea-0-use").Press(); Pump();
            var d = w.ProgramActive(GK)!;
            Assert.Equal(idea.Title, d.Text("title"));
            Assert.Equal(idea.Pitch, w.Find<TextBox>("game-f-logline").Text); // die Spielbibel ist offen
            w.Find<Button>("prog-undo").Press(); Pump();
            Assert.NotEqual(idea.Title, d.Text("title")); // ein Schritt zurück
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Ideen: Einstellungen gehen an die KI, Ablehnung beim nächsten Lauf; ohne Bild-KI nur ein Hinweis")]
    public async Task Form_and_rating()
    {
        var (w, _, _) = Ui.Open();
        var asked = new List<string>();
        const string answer = """
            ```json
            [{"title":"Eins","pitch":"Text eins","theme":"wald"},{"title":"Zwei","pitch":"Text zwei","theme":"meer"},{"title":"Drei","pitch":"Text drei","theme":"berg"},{"title":"Vier","pitch":"Text vier","theme":"stadt"}]
            ```
            """;
        MainWindow.GamePromptOverride = (_, p) => { asked.Add(p); return Task.FromResult(answer); };
        try
        {
            w.Show(GK); Pump();
            w.Find<Button>("game-idea-form").Press(); Pump();
            w.Find<TextBox>("game-idea-keywords").Text = "Zeitreise";
            w.Find<ComboBox>("game-idea-genre").SelectedItem = "Horror";
            w.Find<ComboBox>("game-idea-style").SelectedItem = "Comic";
            w.Find<Button>("game-idea-run").Press(); await Settle();
            Assert.Contains("Zeitreise", asked[0]); Assert.Contains("Horror", asked[0]);
            Assert.Equal(4, w.GameIdeas.Count);
            Assert.All(w.GameIdeas, i => { Assert.Equal("Horror", i.Genre); Assert.Equal("Comic", i.Style); });

            w.Find<Button>("game-idea-0-image").Press(); await Settle();
            Assert.Contains("Kein Konzeptbild", w.Find<TextBlock>("game-idea-0-note").Text);

            w.Find<Button>("game-idea-0-dislike").Press(); Pump();
            w.Find<Button>("game-idea-1-like").Press(); Pump();
            w.Find<Button>("game-idea-more").Press(); await Settle();
            Assert.Contains("Nicht gefallen hat: wald", asked[1]); Assert.Contains("Gefallen hat: meer", asked[1]);
            Assert.DoesNotContain(w.GameIdeas, i => i.Theme == "wald");
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Ideen: Konzeptbild erscheint auf der Karte und landet im Abschnitt Idee")]
    public async Task Concept_image()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.GamePromptOverride = (_, _) => throw new InvalidOperationException("keine KI");
        MainWindow.KiToolOverride = (_, _, target) => { File.WriteAllBytes(target, Convert.FromBase64String(Png)); return Task.FromResult(true); };
        try
        {
            w.Show(GK); Pump();
            w.Find<Button>("game-idea-form").Press(); Pump();
            w.Find<Button>("game-idea-run").Press(); await Settle();
            w.Find<Button>("game-idea-0-image").Press(); await Settle();
            Assert.NotNull(w.TryFind<Control>("game-idea-0-picture"));
            w.Find<Button>("game-idea-0-use").Press(); Pump();
            Assert.Single(GameDocument.Images(w.ProgramActive(GK)!.Data, "idee"));
        }
        finally { MainWindow.GamePromptOverride = null; MainWindow.KiToolOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Dokument: Abschnitte verschieben und ergänzen, Bild einfügen und in die Ablage legen, Text ändern")]
    public void Document_sections()
    {
        var (w, _, root) = Ui.Open();
        var d = w.ProgramNew(GK, "retro-jump");
        var image = Path.Combine(root, "bild.png"); File.WriteAllBytes(image, Convert.FromBase64String(Png));
        w.PickOverride = _ => Task.FromResult<string[]?>([image]);
        w.GameSection = "Spielbibel"; w.Show(GK); Pump();
        w.Find<Button>("game-doc-idee-down").Press(); Pump();
        Assert.Equal(["kern", "idee"], GameDocument.Sections(d.Data).Select(s => s.Id).Take(2));
        Assert.False(w.Find<Button>("game-doc-kern-up").IsEnabled);
        w.Find<TextBox>("game-doc-new-title").Text = "Wirtschaft";
        w.Find<Button>("game-doc-add").Press(); Pump();
        var box = w.Find<TextBox>("game-doc-abschnitt-1-text");
        box.Focus(); Pump(); box.Text = "Münzen"; w.Find<TextBox>("game-doc-new-title").Focus(); Pump(); // Verlassen des Felds übernimmt den Text
        Assert.Contains("## Wirtschaft\n\nMünzen", GameDocument.Markdown(d.Data));
        w.Find<Button>("game-doc-abschnitt-1-img").Press(); Pump();
        Assert.Equal([image], GameDocument.Images(d.Data, "abschnitt-1"));
        w.Find<Button>("game-doc-abschnitt-1-image-0-vault").Press(); Pump();
        Assert.NotNull(w.Find<StackPanel>("game-doc-vault"));
        w.Find<ComboBox>("game-doc-vault-0-to").SelectedIndex = 0; Pump();
        Assert.Equal([image], GameDocument.Images(d.Data, "kern"));
        Assert.Null(w.TryFind<StackPanel>("game-doc-vault"));
        w.Find<Button>("game-doc-abschnitt-1-del").Press(); Pump();
        Assert.Null(GameDocument.Find(d.Data, "abschnitt-1"));
        w.Find<Button>("game-doc-figuren-goto").Press(); Pump();
        Assert.Equal("Figuren", w.GameSection);
    }

    [AvaloniaFact(DisplayName = "Chat: Vorschlag ändert erst nach Übernehmen und nur den Abschnitt; Verwerfen lässt alles; Rückgängig ein Schritt; Verlauf ohne Rückgängig")]
    public async Task Chat_preview_accept()
    {
        var (w, _, _) = Ui.Open();
        var asked = new List<(string Sys, string Prompt)>();
        MainWindow.GamePromptOverride = (s, p) => { asked.Add((s, p)); return Task.FromResult("Etwas ruhiger.\n```abschnitt\nAquarell, ruhig\n```"); };
        try
        {
            var d = w.ProgramNew(GK, "retro-jump");
            w.GameSection = "Spielbibel"; w.Show(GK); Pump();
            var (style, loop, undoBefore) = (d.Text("style"), d.Text("loop"), d.History.Count);
            w.Find<Button>("game-doc-stil-chat").Press(); Pump();
            w.Find<TextBox>("game-doc-chat-input").Text = "Mach es ruhiger";
            w.Find<Button>("game-doc-chat-send").Press(); await Settle();
            Assert.Contains("Stil", asked[0].Sys); Assert.Contains("Wunsch: Mach es ruhiger", asked[0].Prompt);
            Assert.NotNull(w.Find<StackPanel>("game-doc-preview"));
            Assert.Equal(style, d.Text("style")); Assert.Equal(undoBefore, d.History.Count); // noch nichts geändert
            Assert.Equal(2, GameDocument.Chat(d, "stil").Count);

            w.Find<Button>("game-doc-discard").Press(); Pump();
            Assert.Equal(style, d.Text("style")); Assert.Null(w.TryFind<StackPanel>("game-doc-preview"));

            w.Find<Button>("game-doc-chat-suggest").Press(); await Settle();
            w.Find<Button>("game-doc-accept").Press(); Pump();
            Assert.Equal("Aquarell, ruhig", d.Text("style")); Assert.Equal(loop, d.Text("loop"));
            Assert.Equal(undoBefore + 1, d.History.Count);
            w.Find<Button>("prog-undo").Press(); Pump();
            Assert.Equal(style, d.Text("style"));
            Assert.Equal(4, GameDocument.Chat(d, "stil").Count); // Verlauf bleibt
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    const string FigurenVorschlag = "Neue Liste.\n```json\n[{\"name\":\"Rabe\",\"role\":\"Held\",\"text\":\"Bote\"}]\n```";

    [AvaloniaFact(DisplayName = "Chat: Vorschlag bleibt beim Tab-Wechsel an seinem Spiel und erscheint im anderen Spiel nicht")]
    public async Task Pending_stays_with_document()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.GamePromptOverride = (_, _) => Task.FromResult(FigurenVorschlag);
        try
        {
            var a = w.ProgramNew(GK, "topdown");
            w.GameSection = "Spielbibel"; w.Show(GK); Pump();
            w.Find<Button>("game-doc-figuren-chat").Press(); Pump();
            w.Find<Button>("game-doc-chat-suggest").Press(); await Settle();
            Assert.NotNull(w.Find<StackPanel>("game-doc-preview"));

            var b = w.ProgramNew(GK, "action3d");
            var figures = b.Data["characters"]!.ToJsonString();
            w.Show(GK); Pump();
            Assert.Null(w.TryFind<StackPanel>("game-doc-preview")); Assert.Null(w.TryFind<Button>("game-doc-accept")); Assert.Null(w.TryFind<TextBox>("game-doc-chat-input"));
            w.GameDocAccept(); Pump(); // auch ein direkter Aufruf ändert das falsche Spiel nicht
            Assert.Equal(figures, b.Data["characters"]!.ToJsonString());
            Assert.Equal(2, a.List("characters").Count);

            w.Find<Button>("prog-tab-0").Press(); Pump(); // zurück: dort ist der Vorschlag noch
            w.Find<Button>("game-doc-accept").Press(); Pump();
            Assert.Equal("Rabe", (string?)a.List("characters")[0]!["name"]);
            Assert.Equal(figures, b.Data["characters"]!.ToJsonString());
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Chat: Tab-Wechsel während die KI arbeitet – Antwort landet nur beim gefragten Spiel")]
    public async Task Answer_arrives_after_switch()
    {
        var (w, _, _) = Ui.Open();
        var answer = new TaskCompletionSource<string>();
        MainWindow.GamePromptOverride = (_, _) => answer.Task;
        try
        {
            var a = w.ProgramNew(GK, "topdown");
            w.GameSection = "Spielbibel"; w.Show(GK); Pump();
            w.Find<Button>("game-doc-figuren-chat").Press(); Pump();
            w.Find<Button>("game-doc-chat-suggest").Press(); Pump();
            var b = w.ProgramNew(GK, "action3d");
            w.Show(GK); Pump();
            answer.SetResult(FigurenVorschlag); await Settle();
            Assert.Null(w.TryFind<StackPanel>("game-doc-preview"));
            Assert.Equal(2, GameDocument.Chat(a, "figuren").Count);
            Assert.Empty(GameDocument.Chat(b, "figuren"));
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Chat: Antwort der KI lässt getippten Text und Fokus stehen und holt niemanden aus einem anderen Programm")]
    public async Task Answer_keeps_typing()
    {
        var (w, _, _) = Ui.Open();
        var answer = new TaskCompletionSource<string>();
        MainWindow.GamePromptOverride = (_, _) => answer.Task;
        try
        {
            var d = w.ProgramNew(GK, "retro-jump");
            w.GameSection = "Spielbibel"; w.Show(GK); Pump();
            w.Find<Button>("game-doc-stil-chat").Press(); Pump();
            w.Find<Button>("game-doc-chat-suggest").Press(); Pump();
            var box = w.Find<TextBox>("game-f-loop");
            box.Focus(); Pump(); box.Text = "Noch nicht übernommen";
            answer.SetResult("Nur ein Rat."); await Settle();
            var after = w.Find<TextBox>("game-f-loop");
            Assert.Equal("Noch nicht übernommen", after.Text); Assert.True(after.IsFocused);
            w.Find<TextBox>("game-doc-new-title").Focus(); Pump();
            Assert.Equal("Noch nicht übernommen", d.Text("loop")); // Verlassen übernimmt wie immer

            var late = new TaskCompletionSource<string>();
            MainWindow.GamePromptOverride = (_, _) => late.Task;
            w.Find<Button>("game-doc-chat-suggest").Press(); Pump();
            var other = MainWindow.Programs.Keys.First(k => k != GK);
            w.Show(other); Pump();
            late.SetResult("Später."); await Settle();
            Assert.Equal(other, w.CurrentView);
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Export: PDF ist der erste Export (Stufe Einfach zeigt nur ihn) und schreibt eine Datei")]
    public void Pdf_export_reachable()
    {
        var (w, _, root) = Ui.Open();
        Assert.Equal("bible-pdf", MainWindow.Programs[GK].Exports[0].Name);
        w.ProgramNew(GK, "retro-jump");
        w.Show(GK); Pump();
        w.Find<Button>("prog-export-bible-pdf").Press(); Pump();
        Assert.NotEmpty(Directory.GetFiles(root, "*.pdf", SearchOption.AllDirectories));
    }

    [AvaloniaFact(DisplayName = "Dokument: Netzwerkpfad aus fremder Datei wird nicht geöffnet, die Spielbibel zeigt einen Hinweis")]
    public void Foreign_image_path()
    {
        var (w, _, _) = Ui.Open();
        var d = w.ProgramNew(GK, "retro-jump");
        d.Edit("Bild", x => GameDocument.AddImage(x, "idee", "\\\\server\\freigabe\\x.png"));
        w.GameSection = "Spielbibel"; w.Show(GK); Pump();
        Assert.Contains("nur lokale", w.Find<TextBlock>("game-doc-idee-image-0").Text);
    }

    [AvaloniaFact(DisplayName = "Dokument: kaputte doc-Felder aus KI oder fremder Datei stürzen nicht ab")]
    public void Broken_doc_fields()
    {
        var (w, _, _) = Ui.Open();
        var d = w.ProgramNew(GK, "retro-jump");
        d.ReplaceData("""{"title":"Kaputt","scale":"retro","docOrder":[1,{"a":2},"idee","idee","gibt-es-nicht"],"docCustom":[3,{"title":"ohne id"},{"id":"x","title":7}],"docImages":{"idee":"kein-array","stil":[5,{"a":1}]},"docVault":"nein"}""");
        w.GameSection = "Spielbibel"; w.Show(GK); Pump();
        Assert.True(w.TryFind<Button>("error-retry") is null);
        Assert.Equal("idee", GameDocument.Sections(d.Data)[0].Id);
        Assert.Contains("# Kaputt", GameDocument.Markdown(d.Data));
    }

    [AvaloniaFact(DisplayName = "Chat: ohne KI erscheint ein Hinweis im Verlauf, das Dokument bleibt unverändert")]
    public async Task Chat_without_ki()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.GamePromptOverride = (_, _) => throw new InvalidOperationException("Keine KI hat geantwortet.");
        try
        {
            var d = w.ProgramNew(GK, "retro-jump");
            w.GameSection = "Spielbibel"; w.Show(GK); Pump();
            var before = d.Data.ToJsonString();
            w.Find<Button>("game-doc-figuren-chat").Press(); Pump();
            w.Find<Button>("game-doc-chat-suggest").Press(); await Settle();
            Assert.Equal(before, d.Data.ToJsonString());
            Assert.Contains(GameDocument.Chat(d, "figuren"), l => l.Role == "Hinweis" && l.Text.Contains("Keine KI"));
        }
        finally { MainWindow.GamePromptOverride = null; }
    }
}
