using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Print3D;

/// <summary>Bambu Lab voll aus Trendo (Freigabe 05.11.2026, B1–B5 + WLAN): Drucker im Netz finden, Einrichtung (WLAN, Nur-LAN, Entwicklermodus),
/// Steuerbefehle (Licht, Tempo, Temperaturen, Lüfter), Filamente im AMS, Fehlermeldungen (HMS), Fertig-Meldung, mehrere Drucker.
/// Nur Auswerten und Befehle bauen – das Netz macht die App (QA-019).</summary>
public sealed record BambuFound(string Ip, string Serial, string Model, string Name, string Connect, bool Bound)
{
    /// <summary>Gerätecode → lesbarer Modellname.</summary>
    public string ModelName => Model.ToUpperInvariant() switch
    {
        "3DPRINTER-X1-CARBON" or "BL-P001" => "X1 Carbon", "3DPRINTER-X1" or "BL-P002" => "X1", "C11" => "P1P", "C12" => "P1S", "C13" => "X1E",
        "N1" => "A1 mini", "N2S" => "A1", "O1D" => "H2D", _ => Model, // unbekannte Codes unverändert zeigen
    };
}

public static class BambuDiscovery
{
    /// <summary>Port, auf dem Bambu-Drucker ihre Meldung (SSDP-ähnlich) ins Heimnetz schicken.</summary>
    public const int Port = 2021;

    /// <summary>Eine Meldung auswerten (Kopfzeilen wie „Location:“, „USN:“, „DevModel.bambu.com:“). null = kein Bambu-Drucker.</summary>
    public static BambuFound? Parse(string text)
    {
        var h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n'))
        {
            var i = line.IndexOf(':'); if (i <= 0) continue;
            h[line[..i].Trim()] = line[(i + 1)..].Trim();
        }
        if (!h.TryGetValue("NT", out var nt) || !nt.Contains("bambulab", StringComparison.OrdinalIgnoreCase)) return null;
        if (!h.TryGetValue("Location", out var ip) || !System.Net.IPAddress.TryParse(ip, out _)) return null;
        if (!h.TryGetValue("USN", out var usn) || usn.Length < 8) return null;
        return new(ip, usn.Trim().ToUpperInvariant(), h.GetValueOrDefault("DevModel.bambu.com", ""), h.GetValueOrDefault("DevName.bambu.com", "Bambu-Drucker"),
            h.GetValueOrDefault("DevConnect.bambu.com", ""), h.GetValueOrDefault("DevBind.bambu.com", "").Equals("occupied", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Liegt der Drucker im selben Netz wie dieser PC (gleiches /24-Netz einer eigenen Adresse)?</summary>
    public static bool SameNetwork(IEnumerable<string> ownIps, string printerIp)
    {
        if (!System.Net.IPAddress.TryParse(printerIp, out var p) || p.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var pb = p.GetAddressBytes();
        return ownIps.Any(o => System.Net.IPAddress.TryParse(o, out var a) && a.AddressFamily == p.AddressFamily && a.GetAddressBytes().Take(3).SequenceEqual(pb.Take(3)));
    }
}

/// <summary>Einrichtungshilfe je Drucker-Familie: ins WLAN bringen, Nur-LAN, Entwicklermodus, Zugangscode ablesen.</summary>
public static class BambuSetup
{
    public sealed record Step(string Id, string Title, string How);

    public static IReadOnlyList<Step> Steps(string modelName)
    {
        var touch = modelName.StartsWith("X1", StringComparison.OrdinalIgnoreCase) || modelName.StartsWith("H2", StringComparison.OrdinalIgnoreCase) || modelName.StartsWith("P2", StringComparison.OrdinalIgnoreCase) || modelName.StartsWith("A1", StringComparison.OrdinalIgnoreCase);
        var menu = touch ? "am Bildschirm des Druckers: Einstellungen (Zahnrad)" : "am Drucker mit dem Drehknopf: Einstellungen";
        return
        [
            new("wlan", "Drucker ins WLAN bringen", $"{menu} → WLAN → dein Heimnetz wählen und das WLAN-Passwort eingeben. Der Drucker funkt nur im 2,4-GHz-Band – zeigt dein Router zwei Netze, das 2,4-GHz-Netz nehmen. Danach steht dort die IP-Adresse."),
            new("lan", "„Nur LAN“ einschalten", $"{menu} → Netzwerk → „Nur LAN“ (LAN Only) an. Der Drucker bleibt im Heimnetz und spricht nicht mit der Bambu-Cloud."),
            new("dev", "Entwicklermodus einschalten", $"{menu} → Netzwerk → „Entwicklermodus“ (Developer Mode) an. Erst damit darf Trendo den Drucker steuern (Licht, Tempo, Druck starten). Ohne ihn kann Trendo nur zusehen. Bambu gibt für diesen Modus keine Hilfe."),
            new("code", "Zugangscode ablesen", $"{menu} → Netzwerk: dort steht der 8-stellige Zugangscode (Access Code). Ihn trägst du in Trendo ein – er bleibt im Geheimnisspeicher, nie in einer Datei."),
        ];
    }
}

/// <summary>Ein Filament-Fach im AMS (oder die externe Spule).</summary>
public sealed record BambuTray(int Ams, int Slot, string Type, string ColorHex, int RemainPercent, bool Active)
{
    public string Label => Type.Length == 0 ? "leer" : $"{Type} · {(RemainPercent >= 0 ? RemainPercent + " %" : "Rest unbekannt")}";
}

/// <summary>Fehlermeldung des Druckers (HMS) mit Klartext für den Bereich und Link zum Nachschlagen.</summary>
public sealed record BambuHms(long Attr, long Code)
{
    public string Id => $"{Attr >> 16:X4}_{Attr & 0xFFFF:X4}_{Code >> 16:X4}_{Code & 0xFFFF:X4}";
    public string Area => (Attr >> 24) switch
    {
        0x03 => "Bewegung und Mechanik", 0x05 => "Hauptplatine und Netz", 0x07 => "AMS (Filament-Wechsler)", 0x08 => "Druckkopf",
        0x0C => "Kamera und KI-Erkennung", 0x10 => "Steuerung", 0x12 => "AMS Lite", _ => "Drucker",
    };
    public string Severity => (Code >> 16) switch { 1 => "Schwerer Fehler", 2 => "Fehler", 3 => "Warnung", 4 => "Hinweis", _ => "Meldung" };
    public string Text => $"{Severity} im Bereich {Area} (Code {Id})";
    public string HelpUrl => $"https://e.bambulab.com/index.php?e={Id.Replace("_", "")}&s=device_hms&lang=de";
}

/// <summary>Weitere Werte aus den Statusmeldungen: WLAN-Signal, Tempo, Licht, Lüfter, AMS, Fehlermeldungen.</summary>
public sealed record BambuDetails
{
    public int WifiDbm { get; init; } = int.MinValue;
    public int SpeedLevel { get; init; }
    public bool? LightOn { get; init; }
    public int PartFanPercent { get; init; } = -1;
    public IReadOnlyList<BambuTray> Trays { get; init; } = [];
    public IReadOnlyList<BambuHms> Hms { get; init; } = [];

    public string WifiText => WifiDbm == int.MinValue ? "WLAN-Signal unbekannt (Kabel oder noch keine Meldung)" : WifiDbm >= -55 ? $"WLAN sehr gut ({WifiDbm} dBm)" : WifiDbm >= -67 ? $"WLAN gut ({WifiDbm} dBm)" : WifiDbm >= -75 ? $"WLAN schwach ({WifiDbm} dBm) – Drucker näher an den Router oder einen Verstärker nutzen" : $"WLAN sehr schwach ({WifiDbm} dBm) – Abbrüche möglich";
    public static string SpeedName(int level) => level switch { 1 => "Leise", 2 => "Normal", 3 => "Sport", 4 => "Maximal", _ => "unbekannt" };

    public BambuDetails Merge(string json)
    {
        JsonNode? root; try { root = JsonNode.Parse(json); } catch (JsonException) { return this; }
        if (root?["print"] is not JsonObject p) return this;
        var d = this;
        if (p["wifi_signal"] is JsonValue ws && ws.TryGetValue<string>(out var w) && int.TryParse(w.Replace("dBm", "", StringComparison.OrdinalIgnoreCase).Trim(), out var dbm)) d = d with { WifiDbm = dbm };
        if (Num(p["spd_lvl"]) is { } lvl) d = d with { SpeedLevel = (int)lvl };
        if (p["lights_report"] is JsonArray lights && lights.OfType<JsonObject>().FirstOrDefault(l => Str(l["node"]) == "chamber_light") is { } cl) d = d with { LightOn = Str(cl["mode"]) == "on" };
        if (Num(p["cooling_fan_speed"]) is { } fan) d = d with { PartFanPercent = (int)Math.Round(fan / 15.0 * 100) }; // Firmware meldet 0–15
        if (p["hms"] is JsonArray hms) d = d with { Hms = [.. hms.OfType<JsonObject>().Select(h => new BambuHms((long)(Num(h["attr"]) ?? 0), (long)(Num(h["code"]) ?? 0)))] };
        if (p["ams"] is JsonObject ams && ams["ams"] is JsonArray units)
        {
            var now = Str(ams["tray_now"]);
            var trays = new List<BambuTray>();
            foreach (var (u, ui) in units.OfType<JsonObject>().Select((u, i) => (u, i)))
                foreach (var t in (u["tray"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var slot = (int)(Num(t["id"]) ?? 0); var col = Str(t["tray_color"]);
                    trays.Add(new(ui + 1, slot + 1, Str(t["tray_type"]), col.Length >= 6 ? "#" + col[..6] : "", (int)(Num(t["remain"]) ?? -1), now == (ui * 4 + slot).ToString(CultureInfo.InvariantCulture)));
                }
            d = d with { Trays = trays };
        }
        return d;
    }
    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n?.ToString() ?? "";
    static double? Num(JsonNode? n) => n is JsonValue v && (v.TryGetValue<double>(out var d) || (v.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))) ? d : null;
}

/// <summary>Steuerbefehle (MQTT-Nachrichten wie Bambu Studio sie schickt), mit Grenzen geprüft.</summary>
public static class BambuCommands
{
    public static string Light(bool on, string seq) => new JsonObject { ["system"] = new JsonObject { ["sequence_id"] = seq, ["command"] = "ledctrl", ["led_node"] = "chamber_light", ["led_mode"] = on ? "on" : "off", ["led_on_time"] = 500, ["led_off_time"] = 500, ["loop_times"] = 0, ["interval_time"] = 0 } }.ToJsonString();
    public static string Speed(int level, string seq) => level is < 1 or > 4 ? throw new ArgumentException("Tempo 1 (leise) bis 4 (maximal).")
        : new JsonObject { ["print"] = new JsonObject { ["sequence_id"] = seq, ["command"] = "print_speed", ["param"] = level.ToString(CultureInfo.InvariantCulture) } }.ToJsonString();
    static string Gcode(string line, string seq) => new JsonObject { ["print"] = new JsonObject { ["sequence_id"] = seq, ["command"] = "gcode_line", ["param"] = line + "\n" } }.ToJsonString();
    /// <summary>Düse (0–300 °C), Bett (0–110 °C), Teile-Lüfter (0–100 %). 0 = aus.</summary>
    public static string Nozzle(int c, string seq) => c is < 0 or > 300 ? throw new ArgumentException("Düse 0 bis 300 °C.") : Gcode($"M104 S{c}", seq);
    public static string Bed(int c, string seq) => c is < 0 or > 110 ? throw new ArgumentException("Bett 0 bis 110 °C.") : Gcode($"M140 S{c}", seq);
    public static string Fan(int percent, string seq) => percent is < 0 or > 100 ? throw new ArgumentException("Lüfter 0 bis 100 %.") : Gcode($"M106 P1 S{(percent * 255 + 50) / 100}", seq);

    /// <summary>Gerade fertig geworden? (vorher druckend, jetzt fertig) – für die Fertig-Meldung.</summary>
    public static bool JustFinished(BambuStatus before, BambuStatus now) => before.State is "RUNNING" or "PAUSE" or "PREPARE" && now.State == "FINISH";
    public static bool JustFailed(BambuStatus before, BambuStatus now) => before.State is "RUNNING" or "PAUSE" or "PREPARE" && now.State == "FAILED";
}

/// <summary>Mehrere Drucker (Druckfarm): Liste im Datenordner, Zugangscodes nur im Geheimnisspeicher.</summary>
public static class BambuFleet
{
    public static string FileOf(string dir) => Path.Combine(dir, "drucker-liste.json");
    public static List<BambuPrinter> Load(string dir)
    {
        try { return File.Exists(FileOf(dir)) ? JsonSerializer.Deserialize<List<BambuPrinter>>(File.ReadAllText(FileOf(dir))) ?? [] : []; } catch (JsonException) { return []; }
    }
    public static void Save(string dir, IEnumerable<BambuPrinter> list) { Directory.CreateDirectory(dir); File.WriteAllText(FileOf(dir), JsonSerializer.Serialize(list.DistinctBy(p => p.Serial).ToList())); }
    public static List<BambuPrinter> Upsert(string dir, BambuPrinter p) { var l = Load(dir).Where(x => x.Serial != p.Serial).ToList(); l.Add(p); Save(dir, l); return l; }
    public static List<BambuPrinter> Remove(string dir, string serial) { var l = Load(dir).Where(x => x.Serial != serial).ToList(); Save(dir, l); return l; }
}

/// <summary>Bambu Studio über die Befehlszeile: Modell aufstellen, anordnen und für den Druck vorbereiten (.gcode.3mf).</summary>
public static class BambuStudioCli
{
    /// <summary>Argumente zum Slicen: <paramref name="model"/> (STL/3MF/STEP/OBJ) → <paramref name="out3mf"/>; Profile optional (von Bambu Studio exportierte JSON-Dateien).</summary>
    public static List<string> SliceArgs(string model, string out3mf, string? machineJson = null, string? processJson = null, string? filamentJson = null)
    {
        if (!out3mf.EndsWith(".gcode.3mf", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Ziel muss .gcode.3mf sein.");
        var a = new List<string> { "--orient", "1", "--arrange", "1" };
        var settings = string.Join(";", new[] { machineJson, processJson }.Where(x => !string.IsNullOrEmpty(x)));
        if (settings.Length > 0) { a.Add("--load-settings"); a.Add(settings); }
        if (!string.IsNullOrEmpty(filamentJson)) { a.Add("--load-filaments"); a.Add(filamentJson); }
        a.AddRange(["--slice", "0", "--export-3mf", out3mf, model]);
        return a;
    }
}
