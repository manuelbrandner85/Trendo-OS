using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.App.Tests;
using Xunit.v3;

// Volltest-Befund 17.11.2026: viele Tests ließen ihr Trendo-Fenster offen. Jeder weitere Test ordnete alle alten Fenster mit an –
// der Testprozess wuchs auf ~10 GB, der Lauf wurde langsam und wacklig. Nach jedem Test werden deshalb die Fenster geschlossen,
// die dieser Test geöffnet und offen gelassen hat. Fenster des laufenden Tests bleiben bis zu seinem Ende unberührt
// (Tests mit zwei Fenstern, z. B. Sperren, funktionieren weiter). Am Produkt ändert sich nichts.
[assembly: FensterAufraeumen]

namespace Trendo.App.Tests;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class FensterAufraeumenAttribute : BeforeAfterTestAttribute
{
    static readonly List<WeakReference<Window>> Offen = [];
    static readonly object Gate = new();
    static int _registriert;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (Interlocked.Exchange(ref _registriert, 1) == 0)
            Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => { lock (Gate) Offen.Add(new WeakReference<Window>(w)); }, RoutingStrategies.Direct);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (!Dispatcher.UIThread.CheckAccess()) return; // nur Tests auf dem UI-Thread (AvaloniaFact) öffnen Fenster
        List<Window> zu;
        lock (Gate)
        {
            zu = [.. Offen.Select(r => r.TryGetTarget(out var w) ? w : null).OfType<Window>()];
            Offen.Clear();
        }
        foreach (var w in zu)
            try { if (w.IsVisible) w.Close(); } catch (InvalidOperationException) { } // schon geschlossen oder im Schließen
        Dispatcher.UIThread.RunJobs();
    }
}
