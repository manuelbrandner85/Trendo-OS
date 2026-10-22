namespace Trendo.Core;

public enum DriveMode { Simulation, DryRun, Live }

public sealed record DriveReport(bool Sent, string Detail);

/// <summary>Zeilen, die ein verbundenes Gerät bekommt. Simulation und Trockenlauf senden sie nicht.</summary>
public static class MotionText
{
    public const string Action = "robot.move";
    public const string Stop = "STOP";
    public static string Move(Joints j) => FormattableString.Invariant($"MOVE {j.Base:0.###} {j.Shoulder:0.###} {j.Elbow:0.###}");
    public static string Grip(bool close) => close ? "GRIP CLOSE" : "GRIP OPEN";
}

public static class MotionEndpoint
{
    public static bool TryParse(string? text, out string host, out int port)
    {
        host = ""; port = 0;
        var t = (text ?? "").Trim();
        if (t.Contains("://", StringComparison.Ordinal)) return false;
        var i = t.LastIndexOf(':');
        if (i <= 0 || i == t.Length - 1) return false;
        host = t[..i].Trim();
        if (host.Length == 0 || host.Contains(' ')) return false;
        return int.TryParse(t[(i + 1)..], out port) && port is > 0 and <= 65535;
    }
}

public interface IMotionLink : IDisposable
{
    bool IsOpen { get; }
    string Endpoint { get; }
    void Send(string line);
}

/// <summary>Merkt sich gesendete Zeilen. Für Prüfungen und den Trockenlauf-Vergleich.</summary>
public sealed class MemoryMotionLink : IMotionLink
{
    public bool IsOpen { get; set; } = true;
    public string Endpoint { get; set; } = "speicher";
    public List<string> Sent { get; } = [];
    public void Send(string line)
    {
        if (!IsOpen) throw new IOException("Kein Gerät verbunden.");
        Sent.Add(line);
    }
    public void Dispose() { }
}

/// <summary>CORE-09: Arm und Greifer. Voreinstellung ist Simulation. Echte Zeilen laufen nur durch das Freigabe-Tor, Not-Aus blockiert sofort.</summary>
public sealed class RobotDrive
{
    public DriveMode Mode { get; set; } = DriveMode.Simulation;
    public bool Emergency { get; private set; }
    public Joints? Pose { get; private set; }
    public IMotionLink? Link { get; set; }
    public List<string> Journal { get; } = [];

    public void EmergencyStop()
    {
        Emergency = true;
        Journal.Add(MotionText.Stop);
        if (Mode == DriveMode.Live && Link is { IsOpen: true })
        {
            try { Link.Send(MotionText.Stop); }
            catch (IOException) { Journal.Add("STOP nicht zugestellt"); }
        }
    }

    public void ReleaseEmergency()
    {
        if (!Emergency) throw new InvalidOperationException("Not-Aus ist nicht aktiv.");
        Emergency = false;
        Journal.Add("Not-Aus gelöst");
    }

    public DriveReport Execute(string command, Joints? poseAfter, ApprovalGate? gate, string? approvalId)
    {
        if (Emergency) throw new InvalidOperationException("Not-Aus ist aktiv. Erst lösen, dann wieder fahren.");
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Kein Befehl.");
        switch (Mode)
        {
            case DriveMode.Simulation:
                if (poseAfter is { } sim) Pose = sim;
                Journal.Add("Simulation: " + command);
                return new(false, "Simulation: " + command.Replace('\n', ' '));
            case DriveMode.DryRun:
                Journal.Add("Trockenlauf: " + command);
                return new(false, "Trockenlauf, nicht gesendet: " + command);
            default:
                if (Link is not { IsOpen: true }) throw new InvalidOperationException("Kein Gerät verbunden. Adresse eintragen und verbinden, oder bei Simulation bleiben.");
                if (gate is null || string.IsNullOrEmpty(approvalId)) throw new ApprovalException("Echte Bewegung braucht eine Freigabe.");
                gate.Execute(approvalId, MotionText.Action, command, () =>
                {
                    if (Emergency) throw new InvalidOperationException("Not-Aus hat die Fahrt gestoppt.");
                    foreach (var line in command.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (Emergency) throw new InvalidOperationException("Not-Aus hat die Fahrt gestoppt.");
                        Link.Send(line);
                    }
                    return true;
                });
                if (poseAfter is { } live) Pose = live;
                Journal.Add("Gesendet: " + command);
                return new(true, "Gesendet: " + command);
        }
    }
}
