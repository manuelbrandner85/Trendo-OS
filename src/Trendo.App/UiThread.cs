using Avalonia.Threading;

namespace Trendo.App;

/// <summary>T-0771: Dispatcher der Oberfläche, einmal auf dem Oberflächen-Thread gemerkt (<see cref="Remember"/> im Hauptfenster).
/// Hintergrund-Arbeit (Prüfungen, Downloads, KI-Antworten …) stellt ihre Ergebnisse hierüber zu statt über <c>Dispatcher.UIThread</c>.
/// Grund: Der Zugriff auf <c>Dispatcher.UIThread</c> legt den Dispatcher an, falls es gerade keinen gibt – und bindet ihn an den GERADE
/// aufrufenden Thread. Im Oberflächen-Testrahmen (Avalonia Headless, eine Anwendung je Test) gibt es diesen Moment zwischen zwei Tests: Lief dann
/// noch eine Hintergrund-Arbeit des vorigen Tests, gehörte der neue Dispatcher dem Hintergrund-Thread, und der Rahmen scheiterte mit
/// „The calling thread cannot access this object because a different thread owns it“ (bekannter Wackelfall, nachgestellt in T-0771).
/// Im Programm selbst gibt es nur einen Dispatcher – dort ändert sich nichts.</summary>
public static class UiThread
{
    static Dispatcher? s_main;

    /// <summary>Auf dem Oberflächen-Thread aufrufen (Hauptfenster).</summary>
    public static void Remember() => s_main = Dispatcher.UIThread;

    /// <summary>NUR FÜR TESTS: vergisst den gemerkten Dispatcher (danach gilt wieder der von Avalonia). Bewusst nicht beim Schließen des Hauptfensters –
    /// sonst griffe späte Hintergrund-Arbeit wieder auf <c>Dispatcher.UIThread</c> zu, genau der Fall, den diese Klasse verhindert.</summary>
    internal static void ForgetForTests() => s_main = null;

    /// <summary>Ist ein Dispatcher gemerkt?</summary>
    internal static bool Remembered => s_main is not null;

    /// <summary>Der gemerkte Dispatcher; vor dem ersten Fenster der von Avalonia.</summary>
    public static Dispatcher Current => s_main ?? Dispatcher.UIThread;

    /// <summary>Wie <see cref="Dispatcher.Post(Action, DispatcherPriority)"/> auf dem gemerkten Dispatcher.</summary>
    public static void Post(Action action, DispatcherPriority priority = default) => Current.Post(action, priority);
}
