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

public sealed class FusionStore(string dataRoot, AuditLog audit)
{
    private string Dir => Path.Combine(dataRoot, "verbund");
    private string NodesFile => Path.Combine(Dir, "geraete.json");
    private static readonly JsonSerializerOptions J = new() { WriteIndented = true };

    public List<FusionNode> Nodes() { try { return File.Exists(NodesFile) ? JsonSerializer.Deserialize<List<FusionNode>>(File.ReadAllText(NodesFile)) ?? [] : []; } catch (JsonException) { return []; } }
    private void Save(List<FusionNode> n) { Directory.CreateDirectory(Dir); File.WriteAllText(NodesFile, JsonSerializer.Serialize(n, J)); }

    /// <summary>Nach erfolgreicher Kopplung mit dem Begleiter (POST /pair) das Gerät speichern. Token gehört in den Schlüsselspeicher, nie in diese Datei.</summary>
    /// <summary>Nur private Netze (LAN) oder .local-Namen – nie das Internet.</summary>
    public static bool IsLanAddress(string a)
    {
        if (Regex.IsMatch(a, @"^[a-zA-Z0-9-]{1,63}\.local$")) return true;
        if (!IPAddress.TryParse(a, out var ip)) return false;
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
    }

    public (FusionNode? Node, string Message) AddPaired(PairResponse r, string address, int port, DateTime now, bool allowLoopback = false)
    {
        if (!(IsLanAddress(address) || (allowLoopback && address == "127.0.0.1"))) return (null, "Nur Adressen im Heimnetz sind erlaubt.");
        if (port is < 1024 or > 65535) return (null, "Port ungültig.");
        var kind = r.Kind switch { "Pi" => NodeKind.Pi, "Android" => NodeKind.Android, _ => NodeKind.Pc };
        var name = string.IsNullOrWhiteSpace(r.Name) ? "Gerät" : r.Name.Trim()[..Math.Min(40, r.Name.Trim().Length)];
        var n = Nodes(); n.RemoveAll(x => x.Address == address && x.Port == port);
        var node = new FusionNode(Guid.NewGuid().ToString("N")[..8], name, kind, address, port, r.Caps.Select(x => x.ToLowerInvariant()).Distinct().ToList(), now, now, true);
        n.Add(node); Save(n); audit.Append("local-owner", "fusion.paired", $"{node.Kind} {node.Name} v{r.Version}");
        return (node, $"„{node.Name}“ gekoppelt ({string.Join(", ", node.Caps)}).");
    }

    public static string TokenKey(string nodeId) => "verbund-token-" + nodeId;
    public static Uri ApiUri(FusionNode n, string path) => new($"http://{n.Address}:{n.Port}/{path.TrimStart('/')}");

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

// ---------- X-08: Handy-Brücke (Android über ADB, Kabel oder WLAN) ----------
public enum PhoneLink { Unbekannt, Kabel, Wlan }

public sealed record PhoneDevice(string Serial, string State, string Model, PhoneLink Link = PhoneLink.Unbekannt);

public static class MobileBridge
{
    /// <summary>Seriennummer, USB-Kennung oder WLAN-Adresse (IP:Port bzw. adb-tls-Name). Keine Steuerzeichen.</summary>
    public static bool SafeSerial(string s) => Regex.IsMatch(s, @"^[A-Za-z0-9.:_-]{3,128}$");
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
            .Select(m => new PhoneDevice(m.Groups[1].Value, m.Groups[2].Value, ModelOf(m.Groups[3].Value), LinkOf(m.Groups[1].Value, m.Groups[3].Value))).ToList();

    static string ModelOf(string rest) => Regex.Match(rest, @"model:(\S+)") is { Success: true } mm ? mm.Groups[1].Value.Replace('_', ' ') : "Android-Gerät";

    /// <summary>Kabel steht als usb: in der Zeile. WLAN ist eine IP mit Port oder der TLS-Name von adb.</summary>
    public static PhoneLink LinkOf(string serial, string rest)
    {
        if (rest.Contains("usb:", StringComparison.OrdinalIgnoreCase)) return PhoneLink.Kabel;
        if (serial.Contains("_adb-tls-connect", StringComparison.Ordinal) || serial.Contains("._adb._tcp", StringComparison.Ordinal)) return PhoneLink.Wlan;
        if (Regex.IsMatch(serial, @"^(?:\d{1,3}\.){3}\d{1,3}:\d{2,5}$")) return PhoneLink.Wlan;
        return PhoneLink.Unbekannt;
    }

    public static string LinkText(PhoneLink link) => link switch { PhoneLink.Kabel => "Kabel", PhoneLink.Wlan => "WLAN", _ => "Verbindung unbekannt" };

    public static string StateText(string s, PhoneLink link = PhoneLink.Unbekannt) => s switch
    {
        "device" => "verbunden",
        "unauthorized" => link == PhoneLink.Wlan ? "am Handy die WLAN-Freigabe bestätigen" : "am Handy „USB-Debugging zulassen“ bestätigen",
        "offline" => "getrennt",
        _ => s,
    };

    /// <summary>Datei aufs Handy (Download-Ordner). Nur nach Zustimmung, nur reguläre Dateien, nie Systempfade.</summary>
    public static (bool Ok, string Args, string Message) PushArgs(PhoneDevice d, string file, bool consent)
    {
        if (!consent) return (false, "", "Erst zustimmen.");
        if (d.State != "device") return (false, "", "Handy ist nicht freigegeben.");
        if (!File.Exists(file)) return (false, "", "Datei nicht gefunden.");
        if (!SafeSerial(d.Serial)) return (false, "", "Seriennummer ungültig.");
        var name = Path.GetFileName(file).Replace("\"", "");
        return (true, $"-s {d.Serial} push \"{file}\" \"/sdcard/Download/{name}\"", $"„{name}“ → Download-Ordner am Handy");
    }

    public static string? FindScrcpy(Func<string, bool> exists, string? path)
    {
        var exe = OperatingSystem.IsWindows() ? "scrcpy.exe" : "scrcpy";
        foreach (var d in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = Path.Combine(d, exe);
            if (exists(p)) return p;
        }
        return null;
    }

    public static IReadOnlyList<string> Guide =>
    [
        "Kabel: Entwickleroptionen, USB-Debugging an, Kabel anschließen, am Handy auf „Zulassen“ tippen",
        "WLAN ab Android 11: Entwickleroptionen, Drahtloses Debuggen an, dann hier „Per WLAN verbinden“",
        "Bluetooth: Dateien und Akkustand. Den Bildschirm zeigen geht per Bluetooth nicht",
        "adb und scrcpy installiert Trendo nur, wenn du zustimmst. Keine Treiber und keine offenen Ports von allein",
        "iPhone: nur Fotos und Dateien über die Programme von Apple oder Windows. Trendo steuert iPhones nicht",
    ];
}

// ---------- X-08: Android-Seite über adb (echte Befehle, mit Zustimmung) ----------
public sealed record PhoneInfo(string Model, string Android, int? Battery, bool Charging);

public static class AndroidSide
{
    static bool SafeSerial(string s) => MobileBridge.SafeSerial(s);

    public static async Task<PhoneInfo?> InfoAsync(IProcessRunner run, string adb, string serial)
    {
        if (!SafeSerial(serial)) return null;
        var (e1, model) = await run.RunAsync(adb, $"-s {serial} shell getprop ro.product.model");
        var (_, ver) = await run.RunAsync(adb, $"-s {serial} shell getprop ro.build.version.release");
        var (_, bat) = await run.RunAsync(adb, $"-s {serial} shell dumpsys battery");
        if (e1 != 0) return null;
        var (lvl, chg) = ParseBattery(bat);
        return new(model.Trim(), ver.Trim(), lvl, chg);
    }

    public static (int? Level, bool Charging) ParseBattery(string dumpsys)
    {
        int? lvl = Regex.Match(dumpsys, @"^\s*level:\s*(\d+)", RegexOptions.Multiline) is { Success: true } m ? int.Parse(m.Groups[1].Value) : null;
        var chg = Regex.IsMatch(dumpsys, @"^\s*(AC|USB|Wireless) powered:\s*true", RegexOptions.Multiline) || Regex.IsMatch(dumpsys, @"^\s*status:\s*2\b", RegexOptions.Multiline);
        return (lvl, chg);
    }

    /// <summary>Bildschirmfoto vom Handy in einen Ordner am PC – nur nach Zustimmung, temporäre Datei am Handy wird gelöscht.</summary>
    public static async Task<(bool Ok, string Message, string? File)> ScreenshotAsync(IProcessRunner run, string adb, string serial, string targetDir, bool consent, DateTime now)
    {
        if (!consent) return (false, "Erst zustimmen.", null);
        if (!SafeSerial(serial)) return (false, "Seriennummer ungültig.", null);
        Directory.CreateDirectory(targetDir);
        var dst = Path.Combine(targetDir, $"handy-{now:yyyyMMdd-HHmmss}.png");
        var (e1, _) = await run.RunAsync(adb, $"-s {serial} shell screencap -p /sdcard/trendo-shot.png");
        var (e2, _) = e1 == 0 ? await run.RunAsync(adb, $"-s {serial} pull /sdcard/trendo-shot.png \"{dst}\"") : (e1, "");
        await run.RunAsync(adb, $"-s {serial} shell rm -f /sdcard/trendo-shot.png");
        return e2 == 0 && File.Exists(dst) ? (true, $"Bildschirmfoto gespeichert: {Path.GetFileName(dst)}", dst) : (false, "Bildschirmfoto fehlgeschlagen (Handy entsperrt?).", null);
    }

    /// <summary>Neuestes Kamerafoto holen – nur Dateien aus DCIM/Camera, Name wird geprüft.</summary>
    public static async Task<(bool Ok, string Message, string? File)> LatestPhotoAsync(IProcessRunner run, string adb, string serial, string targetDir, bool consent)
    {
        if (!consent) return (false, "Erst zustimmen.", null);
        if (!SafeSerial(serial)) return (false, "Seriennummer ungültig.", null);
        var (e, o) = await run.RunAsync(adb, $"-s {serial} shell ls -t /sdcard/DCIM/Camera");
        var name = e == 0 ? o.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => Regex.IsMatch(l, @"^[\w.-]{1,120}\.(jpg|jpeg|png|heic|mp4)$", RegexOptions.IgnoreCase)) : null;
        if (name is null) return (false, "Kein Foto in DCIM/Camera gefunden.", null);
        Directory.CreateDirectory(targetDir); var dst = Path.Combine(targetDir, name);
        var (e2, _) = await run.RunAsync(adb, $"-s {serial} pull \"/sdcard/DCIM/Camera/{name}\" \"{dst}\"");
        return e2 == 0 && File.Exists(dst) ? (true, $"Foto geholt: {name}", dst) : (false, "Übertragung fehlgeschlagen.", null);
    }
}
