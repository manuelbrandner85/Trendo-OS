using System.Net;
using System.Net.Sockets;
using Trendo.Core;

namespace Trendo.Core.Tests;

/// <summary>QA-019: Test-Transport für die Blender-Tests im Kern (der Kern selbst kennt keine Sockets). Gleiches Verhalten wie die App-Umsetzung
/// <c>LoopbackBlenderWire</c>, die ein App-Test gegen einen echten Loopback-Port prüft.</summary>
sealed class BlenderTestWire : IBlenderWire
{
    public static void Install() => BlenderBridge.Wire ??= new BlenderTestWire();

    public async Task<(WireConnect State, Stream? Stream, string Detail)> ConnectAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        var c = new TcpClient { NoDelay = true };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout);
        try { await c.ConnectAsync(IPAddress.Loopback, port, cts.Token); return (WireConnect.Ok, c.GetStream(), ""); }
        catch (SocketException e) { c.Dispose(); return (WireConnect.Refused, null, e.SocketErrorCode.ToString()); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { c.Dispose(); return (WireConnect.Timeout, null, "Zeitgrenze"); }
        catch { c.Dispose(); throw; }
    }

    public int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0); l.Start();
        try { return ((IPEndPoint)l.LocalEndpoint).Port; } finally { l.Stop(); }
    }
}
