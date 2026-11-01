using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Runde 7: KI fragen als Chatfenster – Gespräche speichern und wieder öffnen, Sprechblasen mit Code (Kopieren) und Tabelle, Enter sendet, umbenennen/löschen. KI nachgebildet.
public class ChatWindow_Ui
{
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    const string Answer = "Hier ein Rezept:\n\n| Zutat | Menge |\n|---|---|\n| Mehl | 500 g |\n\n```python\nprint('fertig')\n```\n\nViel Spaß!";

    [AvaloniaFact(DisplayName = "Chatfenster: Enter sendet, Antwort als Sprechblase mit Tabelle und Code (Kopieren), Gespräch gespeichert und wieder zu öffnen, umbenennen, löschen")]
    public async Task Chat_window()
    {
        var (w, _, _) = Ui.Open();
        w.KiBackendsOverride = () => [new FakeKi(Answer)];
        w.Show("ki-fragen");
        Assert.NotNull(w.TryFind<Border>("ask-empty"));
        var input = w.Find<TextBox>("ask-input"); input.Text = "Ein Kuchenrezept bitte";
        input.BringIntoView(); StartMenu_Ui.Settle(); StartMenu_Ui.RealClick(w, input); w.Key(PhysicalKey.Enter);
        await Until(() => w.CurrentChatId is not null && w.TryFind<Border>("ask-msg-1") is not null);
        Assert.Equal("Ein Kuchenrezept bitte", w.Chats.Get(w.CurrentChatId!)!.Title);
        Assert.NotNull(w.TryFind<Border>("ask-table-1"));
        w.Find<Button>("ask-code-copy-1-0").Press(); Assert.Equal("print('fertig')", w.LastCopied);
        w.Find<Button>("ask-copy-1").Press(); Assert.Equal(Answer, w.LastCopied);
        Assert.Contains("Test-KI", w.GetLogicalTexts().First(t => t.StartsWith("test")));
        // zweite Frage im selben Gespräch, Umschalt+Enter macht keine Anfrage
        input = w.Find<TextBox>("ask-input"); input.Text = "Und ohne Zucker?"; input.BringIntoView(); StartMenu_Ui.Settle(); StartMenu_Ui.RealClick(w, input); w.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        Assert.Equal(2, w.Chats.Get(w.CurrentChatId!)!.Messages.Count);
        w.Find<Button>("ask-send").Press();
        await Until(() => w.Chats.Get(w.CurrentChatId!)!.Messages.Count == 4);
        // neues Gespräch, altes wieder öffnen
        var id = w.CurrentChatId!;
        w.Find<Button>("ask-chat-new").Press(); Assert.Null(w.CurrentChatId); Assert.NotNull(w.TryFind<Border>("ask-empty"));
        w.Find<Button>("ask-chat-0").Press(); Assert.Equal(id, w.CurrentChatId); Assert.NotNull(w.TryFind<Border>("ask-msg-3"));
        w.Find<TextBox>("ask-chat-title").Text = "Kuchen"; w.Find<Button>("ask-chat-rename").Press();
        Assert.Equal("Kuchen", w.Chats.Get(id)!.Title);
        w.Find<Button>("ask-chat-del").Press(); w.Find<Button>("ask-chat-del").Press(); // zweistufig
        Assert.Empty(w.Chats.All()); Assert.Null(w.CurrentChatId);
    }
}
