using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 5 – SPEC-10: Live-Messwerte über MQTT (MQTTnet, MIT). Trendo abonniert nur (liest), sendet keine Befehle.
/// Broker und Verknüpfungen werden im Datenordner gemerkt; ein Passwort nie – es gilt nur bis Trendo beendet wird.</summary>
public partial class MainWindow
{
    sealed record MqttSettings(string Host = "127.0.0.1", int Port = 1883, string User = "", string Links = "");
    CancellationTokenSource? _mqttCts; string _mqttMsg = ""; int _mqttCount; string _mqttPass = "";
    public bool TwinMqttRunning => _mqttCts is { IsCancellationRequested: false };
    public string TwinMqttMessage => _mqttMsg;
    public int TwinMqttCount => _mqttCount;
    string MqttFile => Path.Combine(_core.DataRoot, "zwilling", "mqtt.json");
    MqttSettings MqttLoad() { try { return File.Exists(MqttFile) ? JsonSerializer.Deserialize<MqttSettings>(File.ReadAllText(MqttFile)) ?? new() : new(); } catch (JsonException) { return new(); } }
    void MqttStore(MqttSettings s) { Directory.CreateDirectory(Path.GetDirectoryName(MqttFile)!); File.WriteAllText(MqttFile, JsonSerializer.Serialize(s)); }

    /// <summary>Verknüpfungen aus Zeilen „Thema;Anlage;Sensor[;JSON-Feld]“ – Zeilen mit # sind Kommentare.</summary>
    public static List<MqttLink> ParseMqttLinks(string text)
    {
        var list = new List<MqttLink>();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var l = raw.Trim(); if (l.Length == 0 || l.StartsWith('#')) continue;
            var p = l.Split(';').Select(x => x.Trim()).ToArray();
            if (p.Length is < 3 or > 4 || !TwinMqtt.ValidFilter(p[0]) || p[1].Length == 0 || p[2].Length == 0)
                throw new ArgumentException($"Zeile „{l}“: bitte „Thema;Anlage;Sensor“ oder „Thema;Anlage;Sensor;JSON-Feld“.");
            list.Add(new MqttLink(p[0], p[1], p[2], p.Length == 4 && p[3].Length > 0 ? p[3] : null));
        }
        if (list.Count == 0) throw new ArgumentException("Bitte mindestens eine Verknüpfung eintragen, z. B. „haus/keller/temp;Keller;Temperatur“.");
        return list;
    }

    /// <summary>Mit dem Broker verbinden und Messwerte empfangen (öffentlich für Tests).</summary>
    public void TwinMqttStart(string host, int port, string user, string password, string links)
    {
        if (TwinMqttRunning) return;
        try
        {
            var parsed = ParseMqttLinks(links);
            if (!TwinMqtt.ValidHost(host) || !TwinMqtt.ValidPort(port)) throw new ArgumentException("Bitte Rechnername oder IP-Adresse und Port des MQTT-Brokers angeben.");
            foreach (var l in parsed) if (!Twin.Model.Assets.Any(a => a.Name == l.Asset && a.Sensors.Any(s => s.Name == l.Sensor))) Twin.Define(l.Asset, l.Sensor, "");
            MqttStore(new MqttSettings(host.Trim(), port, user.Trim(), links)); _mqttPass = password;
            _mqttCount = 0; _mqttCts = new CancellationTokenSource(); var ct = _mqttCts.Token;
            _mqttMsg = $"Verbinde mit {host.Trim()}:{port} …";
            _core.Audit.Append(Principal.LocalOwner.Name, "zwilling.mqtt.start", $"{host.Trim()}:{port} {parsed.Count} Themen");
            _ = Task.Run(async () =>
            {
                try
                {
                    await TwinMqtt.ListenAsync(Twin, host, port, parsed, ct, (topic, n) =>
                    {
                        _mqttCount += n; // läuft im UI-Thread (siehe run)
                        _mqttMsg = $"Live: {_mqttCount} Werte empfangen (zuletzt „{topic}“, {DateTime.Now:HH:mm:ss})."; if (CurrentView == "zwilling") Show("zwilling");
                    }, user.Length > 0 ? user : null, password.Length > 0 ? password : null, run: a => Dispatcher.UIThread.Post(a));
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    Dispatcher.UIThread.Post(() => { _mqttCts = null; _mqttMsg = "⚠ MQTT: " + Trendo.Core.FriendlyError.Say(e); if (CurrentView == "zwilling") Show("zwilling"); });
                }
            }, ct);
        }
        catch (ArgumentException e) { _mqttMsg = "⚠ " + e.Message; }
        Show("zwilling");
    }

    public void TwinMqttStop()
    {
        _mqttCts?.Cancel(); _mqttCts = null; _mqttPass = "";
        _core.Audit.Append(Principal.LocalOwner.Name, "zwilling.mqtt.stopp", $"{_mqttCount} Werte");
        _mqttMsg = $"MQTT getrennt ({_mqttCount} Werte empfangen)."; Show("zwilling");
    }

    Control TwinMqttPanel()
    {
        var st = MqttLoad();
        var sp = new StackPanel { Spacing = 4, Name = "twin-mqtt" };
        var host = new TextBox { Name = "twin-mqtt-host", Text = st.Host, PlaceholderText = "Broker, z. B. 192.168.1.20" };
        var port = new TextBox { Name = "twin-mqtt-port", Text = st.Port.ToString(), Width = 70 };
        var user = new TextBox { Name = "twin-mqtt-user", Text = st.User, PlaceholderText = "Benutzer (optional)" };
        var pass = new TextBox { Name = "twin-mqtt-pass", PasswordChar = '•', Text = _mqttPass, PlaceholderText = "Passwort (wird nicht gespeichert)" };
        var links = new TextBox { Name = "twin-mqtt-links", Text = st.Links, AcceptsReturn = true, MinHeight = 60, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            PlaceholderText = "Je Zeile: Thema;Anlage;Sensor[;JSON-Feld]\nhaus/keller/temp;Keller;Temperatur\ntele/+/SENSOR;Steckdose;Leistung;ENERGY.Power" };
        sp.Children.Add(P("Nur lesen: Trendo abonniert Themen, schickt nie Befehle an Geräte.", null, 10.5));
        sp.Children.Add(host); sp.Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { P("Port", null, 11), port } });
        sp.Children.Add(user); sp.Children.Add(pass); sp.Children.Add(links);
        sp.Children.Add(TwinMqttRunning
            ? Act("twin-mqtt-stop", "MQTT trennen", TwinMqttStop)
            : Act("twin-mqtt-start", "MQTT verbinden", () => TwinMqttStart(host.Text ?? "", int.TryParse(port.Text, out var p) ? p : 0, user.Text ?? "", pass.Text ?? "", links.Text ?? ""), true));
        if (_mqttMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "twin-mqtt-msg", Text = _mqttMsg, FontSize = 11, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return new Expander { Name = "twin-mqtt-box", Header = "Live über MQTT", IsExpanded = TwinMqttRunning || _mqttMsg.Length > 0, Content = sp, Margin = new Avalonia.Thickness(0, 6, 0, 0) };
    }
}
