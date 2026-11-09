using System.Text;

namespace Trendo.Core;

/// <summary>Antwort eines Abrufs: Statuscode und Inhalt.</summary>
public sealed record WebReply(int Status, byte[] Body)
{
    public bool Ok => Status is >= 200 and < 300;
    public string Text => Encoding.UTF8.GetString(Body);
    /// <summary>Wirft bei einem Fehlerstatus eine verständliche Meldung.</summary>
    public WebReply EnsureOk() => Ok ? this : throw new IOException($"Der Dienst antwortet mit Fehler {Status}.");
}

/// <summary>Netzabruf für Kernfunktionen, die (nur nach Zustimmung) etwas aus dem Internet holen. Der Kern selbst hat keine Netz-Abhängigkeit (QA-019):
/// die App liefert die Umsetzung, Tests eine Attrappe.</summary>
public interface IWebFetch
{
    Task<WebReply> GetAsync(string url, CancellationToken ct = default);
}

/// <summary>Abruf mit Methode, Kopfzeilen und Inhalt, für Dienste mit eigenem Schlüssel der Person. Die App liefert die Umsetzung (QA-019).</summary>
public interface IWebSend
{
    Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default);
}
