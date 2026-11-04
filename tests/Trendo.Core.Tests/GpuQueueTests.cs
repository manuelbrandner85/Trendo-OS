using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Grafikspeicher-Warteschlange: große KI-Aufträge laufen nacheinander, in Reihenfolge, abbrechbar.</summary>
public class GpuQueueTests
{
    [Fact(DisplayName = "Warteschlange: nie zwei große Aufträge gleichzeitig, Reihenfolge bleibt, „noch n vor dir“ wird gemeldet")]
    public async Task One_at_a_time()
    {
        var q = new GpuQueue(); var running = 0; var max = 0; var order = new List<string>();
        var gate = new TaskCompletionSource();
        async Task Job(string n) { var r = Interlocked.Increment(ref running); lock (order) { max = Math.Max(max, r); order.Add(n); } if (n == "Lied") await gate.Task; else await Task.Delay(5); Interlocked.Decrement(ref running); }
        var waits = new List<int>();
        var a = q.RunAsync("musik", "Lied", _ => Job("Lied"));
        await Task.Delay(20);
        var b = q.RunAsync("bild", "Cover", _ => Job("Cover"), onWait: n => { lock (waits) waits.Add(n); });
        var c = q.RunAsync("spuren", "Spuren", _ => Job("Spuren"), onWait: n => { lock (waits) waits.Add(n); });
        await Task.Delay(20);
        Assert.Equal(3, q.Items.Count); Assert.Equal("Lied", q.Current!.Title);
        Assert.Equal([1, 2], waits);
        gate.SetResult();
        await Task.WhenAll(a, b, c);
        Assert.Equal(1, max);
        Assert.Equal(["Lied", "Cover", "Spuren"], order);
        Assert.Empty(q.Items);
    }

    [Fact(DisplayName = "Warteschlange: wartender Auftrag lässt sich abbrechen, der laufende läuft weiter, danach ist sie wieder frei")]
    public async Task Cancel_waiting()
    {
        var q = new GpuQueue(); var gate = new TaskCompletionSource();
        var a = q.RunAsync("musik", "Lied", async _ => await gate.Task);
        await Task.Delay(20);
        using var cts = new CancellationTokenSource();
        var b = q.RunAsync("bild", "Cover", _ => Task.CompletedTask, cts.Token);
        await Task.Delay(20);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b);
        Assert.Single(q.Items);
        gate.SetResult(); await a;
        Assert.Empty(q.Items);
        Assert.Equal(7, await q.RunAsync("text", "frei", _ => Task.FromResult(7)));
    }

    [Fact(DisplayName = "Warteschlange: Platz holen und mit using freigeben; zweimal freigeben schadet nicht; Abbruch im Warten räumt auf")]
    public async Task Acquire_release()
    {
        var q = new GpuQueue();
        var first = await q.AcquireAsync("musik", "Lied");
        var waited = -1;
        var second = q.AcquireAsync("bild", "Cover", onWait: n => waited = n);
        await Task.Delay(20);
        Assert.False(second.IsCompleted); Assert.Equal(1, waited);
        first.Dispose(); first.Dispose();
        using (await second) Assert.Equal("Cover", q.Current!.Title);
        Assert.Empty(q.Items);
        using var block = await q.AcquireAsync("musik", "Lied");
        using var cts = new CancellationTokenSource(30);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => q.AcquireAsync("bild", "x", cts.Token));
        Assert.Single(q.Items);
    }
}
