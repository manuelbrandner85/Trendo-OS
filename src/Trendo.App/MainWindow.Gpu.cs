using Trendo.Core;

namespace Trendo.App;

/// <summary>Grafikspeicher-Regel (Wunsch 04.11.2026): große KI-Aufträge nie gleichzeitig, nur wenn gebraucht. Alle großen Aufträge holen sich hier
/// ihren Platz (<see cref="GpuQueue"/>). Vor einem Auftrag, der nicht Musik ist, wird Trendos eigene Musik-KI beendet (sie hielt nach einem Lied
/// rund 7 GB fest); nach einem Lied bleibt sie nur kurz für das nächste geladen. Vor großen Aufträgen gibt die Text-KI ihr Modell frei.</summary>
public partial class MainWindow
{
    public GpuQueue Gpu { get; } = new();
    /// <summary>Wie lange die Musik-KI nach einem Lied geladen bleibt (für das nächste Lied), dann wird sie beendet.</summary>
    public TimeSpan AceIdle { get; set; } = TimeSpan.FromMinutes(3);
    CancellationTokenSource? _aceIdleCts;

    /// <summary>Platz für einen großen KI-Auftrag holen. <paramref name="onWait"/> meldet, wie viele vorher dran sind.</summary>
    async Task<IDisposable> GpuSlot(string kind, string title, CancellationToken ct = default, Action<int>? onWait = null)
    {
        var slot = await Gpu.AcquireAsync(kind, title, ct, onWait);
        _aceIdleCts?.Cancel(); _aceIdleCts = null;
        // Musik-KI nur beenden, wenn der freie Speicher für diesen Auftrag nicht reicht (sonst müsste sie nach jedem Cover-Bild neu starten)
        if (kind != "musik" && _aceServer is { HasExited: false } && FreeGpuGb() is double free && free < NeedGb(kind)) StopOwnAce($"braucht {NeedGb(kind):0} GB für {title}, frei {free:0.0} GB");
        if (!_opt.Isolated) await UnloadTextModels();
        return new Release(() => { if (kind == "musik") ScheduleAceIdleStop(); }, slot);
    }

    /// <summary>Tests: freier Grafikspeicher in GB.</summary>
    public Func<double?>? FreeGpuOverride { get; set; }
    double? FreeGpuGb() => FreeGpuOverride is { } o ? o() : _opt.Isolated ? null : SystemMetrics.GpuMemory()?.FreeGb;

    /// <summary>Ungefährer Bedarf je Auftrag in GB (gemessen bzw. laut Projektangaben).</summary>
    static double NeedGb(string kind) => kind switch { "bild" => 5, "verbessern" => 3, "video" => 10, nameof(KiRuntime.Spuren) => 3, _ => 6 };

    sealed class Release(Action after, IDisposable slot) : IDisposable
    {
        int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) { slot.Dispose(); after(); } }
    }

    /// <summary>Trendos eigene Musik-KI beenden (nur die selbst gestartete) – sie startet beim nächsten Lied von selbst neu.</summary>
    void StopOwnAce(string why)
    {
        if (_aceServer is not { HasExited: false } p) return;
        try { p.Kill(true); } catch (InvalidOperationException) { }
        _aceServer = null;
        _core.Audit.Append(Principal.LocalOwner.Name, "gpu.musik-ki.beendet", why);
    }

    void ScheduleAceIdleStop()
    {
        var cts = _aceIdleCts = new CancellationTokenSource();
        _ = Task.Delay(AceIdle, cts.Token).ContinueWith(t =>
        {
            if (t.IsCanceled || Gpu.Items.Count > 0) return;
            _ui.Post(() => { if (!cts.IsCancellationRequested && Gpu.Items.Count == 0) StopOwnAce("Leerlauf nach dem Lied"); });
        }, TaskScheduler.Default);
    }

    /// <summary>Modelle der Text-KI aus dem Grafikspeicher nehmen (sie bleiben installiert und laden bei der nächsten Frage neu).</summary>
    async Task UnloadTextModels()
    {
        try { foreach (var (name, _) in await _ollama.LoadedAsync()) await _ollama.UnloadAsync(name); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { }
    }
}
