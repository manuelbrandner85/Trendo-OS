using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

// ---------- SYS-01: Betriebsmodus (Windows-App, Linux-USB, Linux-PC, Raspberry Pi) ----------
public enum OsMode { WindowsApp, LinuxUsb, LinuxPc, Pi }
public enum CapState { Ok, Fehlt, Blockiert }
public sealed record ModeCapability(string Feature, CapState State, string Detail);

public static class TripleMode
{
    public static string Label(OsMode m) => m switch { OsMode.WindowsApp => "Windows-App", OsMode.LinuxUsb => "Linux vom USB-Stick", OsMode.LinuxPc => "Linux auf dem PC", _ => "Raspberry Pi" };

    /// <summary>Ermittelt den Modus aus echten Merkmalen. piModel = Inhalt von /proc/device-tree/model, rootRemovable = Systemlaufwerk ist Wechseldatenträger.</summary>
    public static OsMode Detect(bool isWindows, string? piModel, bool rootRemovable) =>
        isWindows ? OsMode.WindowsApp : piModel?.Contains("Raspberry Pi", StringComparison.OrdinalIgnoreCase) == true ? OsMode.Pi : rootRemovable ? OsMode.LinuxUsb : OsMode.LinuxPc;

    public static OsMode DetectHere(string sysRoot = "/")
    {
        if (OperatingSystem.IsWindows()) return OsMode.WindowsApp;
        string? model = null; try { var f = Path.Combine(sysRoot, "proc/device-tree/model"); if (File.Exists(f)) model = File.ReadAllText(f).Trim('\0', ' ', '\n'); } catch (IOException) { }
        bool removable = false;
        try { var d = DriveInfo.GetDrives().FirstOrDefault(x => x.Name == "/"); removable = d?.DriveType == DriveType.Removable; } catch (IOException) { }
        return Detect(false, model, removable);
    }

    /// <summary>Ehrliche Fähigkeitsmatrix: was im Modus geht, was fehlt (installierbar) und was grundsätzlich blockiert ist.</summary>
    public static IReadOnlyList<ModeCapability> Capabilities(OsMode m, Func<string, bool> has)
    {
        CapState T(string tool) => has(tool) ? CapState.Ok : CapState.Fehlt;
        var l = new List<ModeCapability>
        {
            new("Briefe & Office", T("soffice"), has("soffice") ? "LibreOffice gefunden" : "LibreOffice installieren"),
            new("Texterkennung", T("tesseract"), has("tesseract") ? "Tesseract gefunden" : "Tesseract installieren"),
            new("Lokale KI", T("ollama"), has("ollama") ? "Ollama gefunden" : "Ollama installieren"),
            new("Programme installieren", m == OsMode.WindowsApp ? T("winget") : T("apt"), m == OsMode.WindowsApp ? "winget (Benutzer)" : "Paketverwaltung des Systems"),
            new("Unreal Engine", m == OsMode.WindowsApp ? T("unreal") : CapState.Blockiert, m == OsMode.WindowsApp ? "nur geführt (Epic-Launcher)" : "Unreal gibt es für diesen Modus nicht offiziell"),
            new("Geräte (Windows-Treiber)", m == OsMode.WindowsApp ? CapState.Ok : CapState.Blockiert, m == OsMode.WindowsApp ? "Get-PnpDevice" : "Windows-Treiber sind hier nicht verfügbar"),
            new("GPIO / Sensoren", m == OsMode.Pi ? CapState.Ok : CapState.Blockiert, m == OsMode.Pi ? "GPIO am Pi" : "nur auf dem Raspberry Pi"),
            new("Daten bleiben nach Neustart", m == OsMode.LinuxUsb ? (has("persistence") ? CapState.Ok : CapState.Fehlt) : CapState.Ok, m == OsMode.LinuxUsb ? "nur mit Persistenz-Partition auf dem Stick" : "Festplatte"),
        };
        return l;
    }

    /// <summary>Plan für einen Linux-USB-Stick – nur Anleitung, Trendo beschreibt nie selbst einen Datenträger.</summary>
    public static IReadOnlyList<string> UsbPlan(long stickBytes) =>
    [
        stickBytes >= 16_000_000_000 ? "✓ Stick groß genug (≥ 16 GB)" : "⚠ Mindestens 16 GB nötig",
        "Offizielles Abbild laden (z. B. Ubuntu LTS) und Prüfsumme vergleichen",
        "Mit einem Werkzeug deiner Wahl (z. B. Rufus, balenaEtcher) selbst schreiben – Trendo flasht nicht",
        "Für bleibende Daten eine Persistenz-Partition anlegen",
        "Trendo-Linux-Paket auf den Stick kopieren und dort starten",
    ];
}

// ---------- CORE-04: Verbund aus PC, Pi und Android ----------
public enum NodeKind { Pc, Pi, Android }
public sealed record FusionNode(string Id, string Name, NodeKind Kind, string Address, int Port, List<string> Caps, DateTime PairedAt, DateTime? LastSeen = null, bool Online = false);
public sealed record PairCode(string Code, DateTime Expires);

public sealed class FusionStore(string dataRoot, AuditLog audit)
{
    private string Dir => Path.Combine(dataRoot, "verbund");
    private string NodesFile => Path.Combine(Dir, "geraete.json");
    private string CodeFile => Path.Combine(Dir, "kopplung.json");
    private static readonly JsonSerializerOptions J = new() { WriteIndented = true };

    public List<FusionNode> Nodes() { try { return File.Exists(NodesFile) ? JsonSerializer.Deserialize<List<FusionNode>>(File.ReadAllText(NodesFile)) ?? [] : []; } catch (JsonException) { return []; } }
    private void Save(List<FusionNode> n) { Directory.CreateDirectory(Dir); File.WriteAllText(NodesFile, JsonSerializer.Serialize(n, J)); }

    /// <summary>6-stelliger Einmal-Code, 10 Minuten gültig.</summary>
    public PairCode NewCode(DateTime now)
    {
        var c = new PairCode(RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"), now.AddMinutes(10));
        Directory.CreateDirectory(Dir); File.WriteAllText(CodeFile, JsonSerializer.Serialize(c));
        audit.Append("local-owner", "fusion.code", "neuer Kopplungscode");
        return c;
    }

    /// <summary>Nur private Netze (LAN) oder .local-Namen – nie das Internet.</summary>
    public static bool IsLanAddress(string a)
    {
        if (Regex.IsMatch(a, @"^[a-zA-Z0-9-]{1,63}\.local$")) return true;
        if (!IPAddress.TryParse(a, out var ip)) return false;
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
    }

    public (FusionNode? Node, string Message) Pair(string code, string name, NodeKind kind, string address, int port, IEnumerable<string> caps, DateTime now)
    {
        PairCode? c = null; try { if (File.Exists(CodeFile)) c = JsonSerializer.Deserialize<PairCode>(File.ReadAllText(CodeFile)); } catch (JsonException) { }
        if (c is null || c.Expires < now || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(c.Code), System.Text.Encoding.ASCII.GetBytes(code.Trim())))
        { audit.Append("local-owner", "fusion.pair.rejected", name); return (null, "Code falsch oder abgelaufen."); }
        if (!IsLanAddress(address)) return (null, "Nur Adressen im Heimnetz sind erlaubt.");
        if (port is < 1024 or > 65535) return (null, "Port ungültig.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40) return (null, "Name fehlt.");
        File.Delete(CodeFile); // Einmal-Code
        var n = Nodes(); var node = new FusionNode(Guid.NewGuid().ToString("N")[..8], name.Trim(), kind, address, port, caps.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToList(), now);
        n.Add(node); Save(n); audit.Append("local-owner", "fusion.paired", $"{node.Kind} {node.Name}");
        return (node, $"„{node.Name}“ gekoppelt.");
    }

    public bool Remove(string id) { var n = Nodes(); var ok = n.RemoveAll(x => x.Id == id) > 0; if (ok) { Save(n); audit.Append("local-owner", "fusion.removed", id); } return ok; }

    public void MarkSeen(string id, bool online, DateTime now) { var n = Nodes(); var i = n.FindIndex(x => x.Id == id); if (i < 0) return; n[i] = n[i] with { Online = online, LastSeen = online ? now : n[i].LastSeen }; Save(n); }

    public static Uri HealthUri(FusionNode n) => new($"http://{n.Address}:{n.Port}/health");

    /// <summary>Wählt das Gerät für eine Aufgabe: dieser PC zuerst, sonst ein erreichbares Gerät mit der Fähigkeit.</summary>
    public static (string Where, string Why) Route(string cap, IReadOnlyCollection<string> selfCaps, IEnumerable<FusionNode> nodes)
    {
        cap = cap.ToLowerInvariant();
        if (selfCaps.Contains(cap)) return ("Dieser PC", "kann das selbst");
        var n = nodes.Where(x => x.Online && x.Caps.Contains(cap)).OrderBy(x => x.Kind).FirstOrDefault();
        if (n is not null) return (n.Name, $"{n.Kind} im Heimnetz hat „{cap}“");
        var off = nodes.FirstOrDefault(x => x.Caps.Contains(cap));
        return ("", off is not null ? $"„{off.Name}“ könnte es, ist aber nicht erreichbar" : $"Kein Gerät im Verbund kann „{cap}“");
    }
}

// ---------- X-08: Handy-Brücke (Android über ADB, sonst Anleitung) ----------
public sealed record PhoneDevice(string Serial, string State, string Model);

public static class MobileBridge
{
    public static string? FindAdb(Func<string, bool> exists, string? localAppData, string? path)
    {
        var exe = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        var c = new List<string>();
        if (localAppData is not null) c.Add(Path.Combine(localAppData, "Android", "Sdk", "platform-tools", exe));
        foreach (var d in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) c.Add(Path.Combine(d, exe));
        return c.FirstOrDefault(exists);
    }

    public static IReadOnlyList<PhoneDevice> ParseDevices(string output) =>
        output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("List of devices") && !l.StartsWith("*"))
            .Select(l => Regex.Match(l, @"^(\S+)\s+(device|unauthorized|offline|recovery|sideload)\b(.*)$")).Where(m => m.Success)
            .Select(m => new PhoneDevice(m.Groups[1].Value, m.Groups[2].Value, Regex.Match(m.Groups[3].Value, @"model:(\S+)") is { Success: true } mm ? mm.Groups[1].Value.Replace('_', ' ') : "Android-Gerät")).ToList();

    public static string StateText(string s) => s switch { "device" => "verbunden", "unauthorized" => "am Handy „USB-Debugging zulassen“ bestätigen", "offline" => "getrennt", _ => s };

    /// <summary>Datei aufs Handy (Download-Ordner). Nur nach Zustimmung, nur reguläre Dateien, nie Systempfade.</summary>
    public static (bool Ok, string Args, string Message) PushArgs(PhoneDevice d, string file, bool consent)
    {
        if (!consent) return (false, "", "Erst zustimmen.");
        if (d.State != "device") return (false, "", "Handy ist nicht freigegeben.");
        if (!File.Exists(file)) return (false, "", "Datei nicht gefunden.");
        if (!Regex.IsMatch(d.Serial, @"^[A-Za-z0-9.:_-]{3,64}$")) return (false, "", "Seriennummer ungültig.");
        var name = Path.GetFileName(file).Replace("\"", "");
        return (true, $"-s {d.Serial} push \"{file}\" \"/sdcard/Download/{name}\"", $"„{name}“ → Download-Ordner am Handy");
    }

    public static IReadOnlyList<string> Guide =>
    [
        "Android: Entwickleroptionen → USB-Debugging an, Kabel anschließen, Freigabe am Handy bestätigen",
        "Werkzeug: Android Platform-Tools (offiziell von Google) – Trendo installiert sie nur nach Zustimmung",
        "Ohne Kabel: Windows „Smartphone-Link“ oder KDE Connect nutzen (offizielle Apps)",
        "iPhone: nur Fotos/Dateien über die Windows-Apps; Trendo steuert iOS nicht",
    ];
}
