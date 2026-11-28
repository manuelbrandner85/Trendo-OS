using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0785 / QA-019: TCP-Transport zur Blender-MCP-Brücke – nur 127.0.0.1. Liegt in der App, damit der Kern ohne Netz-Abhängigkeit bleibt.</summary>
public sealed class LoopbackBlenderWire : IBlenderWire
{
    public async Task<(WireConnect State, Stream? Stream, string Detail)> ConnectAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        var c = new TcpClient { NoDelay = true };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout);
        try
        {
            await c.ConnectAsync(IPAddress.Loopback, port, cts.Token);
            return (WireConnect.Ok, c.GetStream(), ""); // der Strom besitzt die Verbindung: Dispose schließt sie
        }
        catch (SocketException e) { c.Dispose(); return (WireConnect.Refused, null, e.SocketErrorCode.ToString()); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { c.Dispose(); return (WireConnect.Timeout, null, "Zeitgrenze"); }
        catch { c.Dispose(); throw; }
    }

    public int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0); l.Start();
        try { return ((IPEndPoint)l.LocalEndpoint).Port; } finally { l.Stop(); }
    }

#pragma warning disable CA2255 // bewusst: der Transport steht bereit, sobald die App geladen ist (auch in Tests und im Render-Werkzeug)
    [ModuleInitializer]
    internal static void Install() => BlenderBridge.Wire ??= new LoopbackBlenderWire();
#pragma warning restore CA2255
}
