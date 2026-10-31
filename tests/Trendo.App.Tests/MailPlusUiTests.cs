using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Posteingang als Mailprogramm: Suche und Ordner „Gemerkt“.</summary>
public class MailPlus_Ui
{
    static List<string> Subjects(MainWindow w) => w.Find<StackPanel>("inbox-list").GetLogicalChildren().OfType<Button>().Select(b => ((TextBlock)((StackPanel)b.Content!).Children[0]).Text!).ToList();

    [AvaloniaFact(DisplayName = "Posteingang: Suche nach Absender/Betreff, Ordner „Gemerkt“ zeigt nur gemerkte Nachrichten")]
    public void Search_and_flag()
    {
        var (w, _, _) = Ui.Open();
        var now = DateTimeOffset.Now;
        w.Inbox = [new MailHeader(1, "Erika Muster <erika@example.org>", "Chorprobe Donnerstag", now, false), new MailHeader(2, "Stadtwerke", "Ihre Rechnung", now, true), new MailHeader(3, "Schule", "Elternabend", now, false)];
        w.Settings.MailFlagged.Add(2);
        w.Show("posteingang"); Settle();
        Assert.Equal(3, Subjects(w).Count);
        var q = w.Find<TextBox>("inbox-search"); q.Text = "erika";
        q.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyUpEvent, Key = Avalonia.Input.Key.Enter }); Settle();
        Assert.Equal(["Chorprobe Donnerstag"], Subjects(w));
        q = w.Find<TextBox>("inbox-search"); q.Text = "";
        q.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyUpEvent, Key = Avalonia.Input.Key.Back }); Settle();
        w.Find<Button>("inbox-folder-merker").Press(); Settle();
        Assert.Equal(["⭐ Ihre Rechnung"], Subjects(w));
    }
}
