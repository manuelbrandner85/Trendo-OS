using System.Net;
using System.Net.Sockets;
using Xunit;

/// <summary>T-0244: Test braucht eine Netzwerkadresse außer 127.0.0.1; ohne sie wird er als „übersprungen“ gemeldet statt still zu bestehen.</summary>
public sealed class NeedsLanFactAttribute : FactAttribute
{
    public NeedsLanFactAttribute()
    {
        try
        {
            if (!Dns.GetHostAddresses(Dns.GetHostName()).Any(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)))
                Skip = "Übersprungen – dieser PC hat keine Netzwerkadresse außer 127.0.0.1.";
        }
        catch (SocketException) { Skip = "Übersprungen – Netzwerkadressen nicht lesbar."; }
    }
}
