using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MQTTnet;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 5 – SPEC-10 „Geräte überwachen“: Live-Messwerte über MQTT (Standard bei Sensoren, Shelly, Tasmota, Home Assistant)
/// mit MQTTnet 5.2 (MIT). Trendo liest nur (Abonnieren), sendet nie Befehle an Geräte. Jede Verbindung verknüpft ein Thema mit Anlage/Sensor des Zwillings.</summary>
public sealed record MqttLink(string Topic, string Asset, string Sensor, string? JsonField = null);

public static partial class TwinMqtt
{
    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9.-]{0,251}[A-Za-z0-9])?$")] private static partial Regex HostRx();

    /// <summary>Rechnername oder IP-Adresse ohne Schema/Pfad (z. B. „192.168.1.20“, „broker.local“).</summary>
    public static bool ValidHost(string host) => !string.IsNullOrWhiteSpace(host) && HostRx().IsMatch(host.Trim());
    public static bool ValidPort(int port) => port is > 0 and <= 65535;

    /// <summary>Gültiger MQTT-Themenfilter: „+“ und „#“ nur als ganze Ebene, „#“ nur am Ende, keine Steuerzeichen.</summary>
    public static bool ValidFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter) || filter.Length > 512 || filter.Any(char.IsControl)) return false;
        var parts = filter.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Contains('#') && (p != "#" || i != parts.Length - 1)) return false;
            if (p.Contains('+') && p != "+") return false;
        }
        return true;
    }

    /// <summary>Passt das Thema zum Filter (MQTT-Regeln mit „+“ für eine Ebene und „#“ für den Rest)?</summary>
    public static bool Matches(string filter, string topic)
    {
        var f = filter.Split('/'); var t = topic.Split('/');
        for (var i = 0; i < f.Length; i++)
        {
            if (f[i] == "#") return true;
            if (i >= t.Length) return false;
            if (f[i] != "+" && f[i] != t[i]) return false;
        }
        return f.Length == t.Length;
    }

    /// <summary>Messwert aus der Nachricht: reine Zahl („21,5“ oder „21.5“) oder JSON-Feld mit Punkt-Pfad („ENERGY.Power“); sonst null.</summary>
    public static double? ParseValue(string payload, string? jsonField = null)
    {
        var s = payload.Trim();
        if (string.IsNullOrEmpty(jsonField))
            return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
        try
        {
            using var d = JsonDocument.Parse(s); var e = d.RootElement;
            foreach (var part in jsonField.Split('.'))
            {
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(part, out var next)) return null;
                e = next;
            }
            return e.ValueKind switch
            {
                JsonValueKind.Number when e.TryGetDouble(out var n) && double.IsFinite(n) => n,
                JsonValueKind.String when double.TryParse(e.GetString()!.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n2) && double.IsFinite(n2) => n2,
                JsonValueKind.True => 1, JsonValueKind.False => 0,
                _ => null,
            };
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Eine eingegangene Nachricht auf alle passenden Verknüpfungen anwenden; Rückgabe = Zahl der gespeicherten Messwerte.</summary>
    public static int Apply(TwinHub hub, IEnumerable<MqttLink> links, string topic, string payload, DateTime time, bool save = true)
    {
        var n = 0;
        foreach (var l in links.Where(l => Matches(l.Topic, topic)))
            if (ParseValue(payload, l.JsonField) is { } v) { hub.Record(time, l.Asset, l.Sensor, v, save: false); n++; }
        if (n > 0 && save) hub.Save();
        return n;
    }

    /// <summary>Mit dem Broker verbinden, die Themen abonnieren und bis zum Abbruch Messwerte in den Zwilling schreiben (nur lesen).
    /// <paramref name="onValue"/> meldet jede gespeicherte Nachricht (Thema, Anzahl); <paramref name="run"/> legt fest, wo gespeichert wird (z. B. UI-Thread).</summary>
    public static async Task ListenAsync(TwinHub hub, string host, int port, IReadOnlyList<MqttLink> links, CancellationToken ct,
        Action<string, int>? onValue = null, string? user = null, string? password = null, Action<Action>? run = null)
    {
        if (!ValidHost(host) || !ValidPort(port)) throw new ArgumentException("Bitte Rechnername oder IP-Adresse und Port (1–65535) des MQTT-Brokers angeben.");
        if (links.Count == 0 || links.Any(l => !ValidFilter(l.Topic) || string.IsNullOrWhiteSpace(l.Asset) || string.IsNullOrWhiteSpace(l.Sensor)))
            throw new ArgumentException("Jede Verknüpfung braucht ein gültiges Thema (z. B. „haus/keller/temperatur“ oder „shelly/+/status“), Anlage und Sensor.");
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            var topic = e.ApplicationMessage.Topic; var payload = e.ApplicationMessage.ConvertPayloadToString() ?? "";
            if (payload.Length > 64_000) return Task.CompletedTask; // Messwerte sind klein – Riesen-Nachrichten ignorieren
            var time = DateTime.Now;
            void Store() { int n; lock (hub) n = Apply(hub, links, topic, payload, time); if (n > 0) onValue?.Invoke(topic, n); }
            if (run is not null) run(Store); else Store(); // App: im UI-Thread speichern, weil die Oberfläche den Zwilling dort liest
            return Task.CompletedTask;
        };
        var ob = new MqttClientOptionsBuilder().WithTcpServer(host.Trim(), port).WithClientId("trendo-" + Guid.NewGuid().ToString("N")[..10]).WithCleanSession();
        if (!string.IsNullOrEmpty(user)) ob = ob.WithCredentials(user, password ?? "");
        await client.ConnectAsync(ob.Build(), ct);
        var sb = factory.CreateSubscribeOptionsBuilder();
        foreach (var f in links.Select(l => l.Topic).Distinct()) sb = sb.WithTopicFilter(f);
        await client.SubscribeAsync(sb.Build(), ct);
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }
        if (client.IsConnected) await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), CancellationToken.None);
    }
}
