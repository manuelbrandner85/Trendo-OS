using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0771: bekannter Wackelfall „The calling thread cannot access this object because a different thread owns it“ beim Aufräumen eines
/// Oberflächentests. Ursache (nachgestellt, reports/T-0771/avalonia-dispatcher-nachstellung.log): Hintergrund-Arbeit eines Tests griff nach dessen
/// Ende auf <c>Dispatcher.UIThread</c> zu, während der Testrahmen die Anwendung für den nächsten Test neu aufsetzte – der neue Dispatcher gehörte
/// dann dem Hintergrund-Thread. Trendo stellt Hintergrund-Ergebnisse jetzt über <see cref="Trendo.App.UiThread"/> zu.</summary>
public class T0771Dispatcher_Ui
{
    [AvaloniaFact(DisplayName = "T-0771: Hintergrund-Arbeit stellt nach dem Testende noch 3 s lang über UiThread.Post zu – der nächste Test startet sauber")]
    public void A_hintergrund_nach_testende()
    {
        Trendo.App.UiThread.Remember(); // wie das Hauptfenster
        new Thread(() => { var end = DateTime.UtcNow.AddSeconds(3); while (DateTime.UtcNow < end) { Trendo.App.UiThread.Post(() => { }); Thread.Sleep(1); } }) { IsBackground = true }.Start();
    }

    [AvaloniaFact(DisplayName = "T-0771: nächster Test – der Dispatcher gehört dem Oberflächen-Thread")]
    public void B_naechster_test() => Assert.True(Dispatcher.UIThread.CheckAccess());

    [AvaloniaFact(DisplayName = "T-0771: übernächster Test – der Dispatcher gehört dem Oberflächen-Thread")]
    public void C_uebernaechster_test() => Assert.True(Dispatcher.UIThread.CheckAccess());
}
