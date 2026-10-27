using System.Net.Security;
using System.Net.Sockets;
using Trendo.Core;

namespace Trendo.App;

/// <summary>O1/O2: Netzverbindungen für E-Mail (TLS mit normaler Zertifikatsprüfung). Die Protokolle selbst liegen im Kern (QA-019).</summary>
public static class MailNet
{
    public static async Task<Stream> Tls(string host, int port, CancellationToken ct)
    {
        var tcp = new TcpClient { ReceiveTimeout = 30000, SendTimeout = 30000 };
        await tcp.ConnectAsync(host, port, ct);
        var ssl = new SslStream(tcp.GetStream(), false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);
        return ssl;
    }

    public static async Task<Stream> Plain(string host, int port, CancellationToken ct)
    {
        var tcp = new TcpClient { ReceiveTimeout = 30000, SendTimeout = 30000 };
        await tcp.ConnectAsync(host, port, ct);
        return tcp.GetStream();
    }

    /// <summary>STARTTLS: bestehende Verbindung auf TLS heben.</summary>
    public static Func<Stream, Task<Stream>> StartTls(string host) => async s =>
    {
        var ssl = new SslStream(s, false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host });
        return ssl;
    };

    public static async Task<ImapSession> OpenImap(MailAccount a, string password, CancellationToken ct = default)
    {
        var imap = new ImapSession(await Tls(a.ImapHost, a.ImapPort, ct));
        await imap.GreetingAsync(ct); await imap.LoginAsync(a.User, password, ct);
        return imap;
    }

    public static async Task SendAsync(MailAccount a, string password, MailDraft d, CancellationToken ct = default)
    {
        var stream = a.SmtpImplicitTls ? await Tls(a.SmtpHost, a.SmtpPort, ct) : await Plain(a.SmtpHost, a.SmtpPort, ct);
        await using (stream)
        {
            var smtp = new SmtpSession(stream, a.SmtpImplicitTls ? null : StartTls(a.SmtpHost));
            await smtp.SendAsync(a.User, password, a.Email, SmtpSession.Recipients(d), SmtpSession.Compose(a, d, DateTimeOffset.Now), ct);
        }
    }
}
