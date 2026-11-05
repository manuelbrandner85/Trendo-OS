using System.Text.Json.Nodes;
using Trendo.Core.Print3D;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Bambu Lab voll aus Trendo (B1–B5 + WLAN): Finden, Einrichtung, Werte, Steuerbefehle, Fertig-Meldung, mehrere Drucker, Bambu-Studio-Befehlszeile.</summary>
public class BambuProTests
{
    const string Notify = "NOTIFY * HTTP/1.1\r\nHOST: 239.255.255.250:1990\r\nServer: Buildroot/2018.02-rc3 UPnP/1.0 ssdpd/1.8\r\nLocation: 192.168.1.40\r\nNT: urn:bambulab-com:device:3dprinter:1\r\nUSN: 01s00c123456789\r\nCache-Control: max-age=1800\r\nDevModel.bambu.com: C12\r\nDevName.bambu.com: Werkstatt\r\nDevSignal.bambu.com: -48\r\nDevConnect.bambu.com: lan\r\nDevBind.bambu.com: free\r\n\r\n";

    [Fact(DisplayName = "CORE-05 B1: Drucker im Netz finden – Bambu-Meldung ergibt IP, Seriennummer, Modell (P1S), Name, LAN-Modus; fremde Geräte und kaputte Meldungen fallen weg; gleiches Netz wie der PC")]
    public void Discovery()
    {
        var f = BambuDiscovery.Parse(Notify)!;
        Assert.Equal(("192.168.1.40", "01S00C123456789", "P1S", "Werkstatt", "lan", false), (f.Ip, f.Serial, f.ModelName, f.Name, f.Connect, f.Bound));
        Assert.Null(BambuDiscovery.Parse(Notify.Replace("bambulab", "fernseher")));
        Assert.Null(BambuDiscovery.Parse(Notify.Replace("Location: 192.168.1.40", "Location: kaputt")));
        Assert.Equal("XYZ", BambuDiscovery.Parse(Notify.Replace("C12", "XYZ"))!.ModelName); // unbekannter Code bleibt sichtbar
        Assert.True(BambuDiscovery.SameNetwork(["10.0.0.5", "192.168.1.23"], "192.168.1.40"));
        Assert.False(BambuDiscovery.SameNetwork(["192.168.2.23"], "192.168.1.40"));
    }

    [Fact(DisplayName = "CORE-05 B2/WLAN: Einrichtung in vier Schritten – ins WLAN (2,4 GHz), Nur LAN, Entwicklermodus (sonst nur Zusehen), Zugangscode; Bedienweg passend zum Modell")]
    public void Setup_steps()
    {
        var a1 = BambuSetup.Steps("A1");
        Assert.Equal(["wlan", "lan", "dev", "code"], a1.Select(s => s.Id));
        Assert.Contains("2,4-GHz", a1[0].How); Assert.Contains("nur zusehen", a1[2].How);
        Assert.Contains("Bildschirm", a1[0].How); Assert.Contains("Drehknopf", BambuSetup.Steps("P1S")[0].How);
    }

    [Fact(DisplayName = "CORE-05 B4: Statuswerte – WLAN-Signal in Klartext, Tempo-Stufe, Kammerlicht, Lüfter, AMS-Fächer mit Typ, Farbe, Rest und aktivem Fach, Fehlermeldungen mit Bereich und Link")]
    public void Details()
    {
        const string json = """
            {"print":{"wifi_signal":"-71dBm","spd_lvl":3,"cooling_fan_speed":"15","lights_report":[{"node":"chamber_light","mode":"on"}],
             "hms":[{"attr":117441024,"code":131074}],
             "ams":{"tray_now":"1","ams":[{"id":"0","tray":[{"id":"0","tray_type":"PLA","tray_color":"FF0000FF","remain":80},{"id":"1","tray_type":"PETG","tray_color":"00FF00FF","remain":35},{"id":"2"}]}]}}}
            """;
        var d = new BambuDetails().Merge(json);
        Assert.Equal(-71, d.WifiDbm); Assert.StartsWith("WLAN schwach", d.WifiText);
        Assert.Equal((3, true, 100), (d.SpeedLevel, d.LightOn, d.PartFanPercent)); Assert.Equal("Sport", BambuDetails.SpeedName(3));
        Assert.Equal(3, d.Trays.Count);
        Assert.Equal(("PLA", "#FF0000", 80, false), (d.Trays[0].Type, d.Trays[0].ColorHex, d.Trays[0].RemainPercent, d.Trays[0].Active));
        Assert.True(d.Trays[1].Active); Assert.Equal("leer", d.Trays[2].Label);
        var h = Assert.Single(d.Hms);
        Assert.Equal("0700_0200_0002_0002", h.Id); Assert.Contains("AMS", h.Area); Assert.Equal("Fehler", h.Severity); Assert.Contains("e=0700020000020002", h.HelpUrl);
        Assert.Equal(-71, d.Merge("""{"print":{"mc_percent":5}}""").WifiDbm); // Teilmeldung behält Werte
    }

    [Fact(DisplayName = "CORE-05 B4: Steuerbefehle wie Bambu Studio – Licht, Tempo 1–4, Düse bis 300 °C, Bett bis 110 °C, Lüfter in Prozent; außerhalb der Grenzen abgelehnt; Zeitraffer beim Druckstart")]
    public void Commands()
    {
        Assert.Equal("on", JsonNode.Parse(BambuCommands.Light(true, "1"))!["system"]!["led_mode"]!.GetValue<string>());
        Assert.Equal("4", JsonNode.Parse(BambuCommands.Speed(4, "2"))!["print"]!["param"]!.GetValue<string>());
        Assert.Equal("M104 S215\n", JsonNode.Parse(BambuCommands.Nozzle(215, "3"))!["print"]!["param"]!.GetValue<string>());
        Assert.Equal("M140 S60\n", JsonNode.Parse(BambuCommands.Bed(60, "4"))!["print"]!["param"]!.GetValue<string>());
        Assert.Equal("M106 P1 S128\n", JsonNode.Parse(BambuCommands.Fan(50, "5"))!["print"]!["param"]!.GetValue<string>());
        Assert.Throws<ArgumentException>(() => BambuCommands.Nozzle(350, "x")); Assert.Throws<ArgumentException>(() => BambuCommands.Bed(130, "x"));
        Assert.Throws<ArgumentException>(() => BambuCommands.Speed(5, "x")); Assert.Throws<ArgumentException>(() => BambuCommands.Fan(120, "x"));
        Assert.True(JsonNode.Parse(BambuClient.StartPayload("teil.gcode.3mf", 1, true, "9", timelapse: true))!["print"]!["timelapse"]!.GetValue<bool>());
    }

    [Fact(DisplayName = "CORE-05 B4: Fertig-Meldung – Wechsel von „Druckt“ zu „Fertig“ meldet einmal Erfolg, zu „Fehlgeschlagen“ einen Fehler; Leerlauf meldet nichts")]
    public void Finished_event()
    {
        var c = new BambuClient(new BambuPrinter("Werkstatt", "192.168.1.40", "01S00C123456789"), "12345678");
        var got = new List<bool>(); c.Finished += (_, ok) => got.Add(ok);
        c.Feed("""{"print":{"gcode_state":"IDLE"}}"""); c.Feed("""{"print":{"gcode_state":"RUNNING","mc_percent":50}}""");
        c.Feed("""{"print":{"gcode_state":"FINISH","mc_percent":100}}"""); c.Feed("""{"print":{"gcode_state":"FINISH"}}""");
        c.Feed("""{"print":{"gcode_state":"RUNNING"}}"""); c.Feed("""{"print":{"gcode_state":"FAILED"}}""");
        Assert.Equal([true, false], got);
    }

    [Fact(DisplayName = "CORE-05 B5/B3: Mehrere Drucker in einer Liste (gleiche Seriennummer ersetzt, ohne Zugangscode in der Datei); Bambu-Studio-Befehlszeile zum Slicen mit Profilen")]
    public void Fleet_and_cli()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-bambu-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            BambuFleet.Upsert(dir, new("Werkstatt", "192.168.1.40", "01S00C1"));
            BambuFleet.Upsert(dir, new("Büro", "192.168.1.41", "03W00A2"));
            var l = BambuFleet.Upsert(dir, new("Werkstatt neu", "192.168.1.42", "01S00C1"));
            Assert.Equal(2, l.Count); Assert.Contains(l, p => p.Name == "Werkstatt neu" && p.Ip == "192.168.1.42");
            Assert.DoesNotContain("12345678", File.ReadAllText(BambuFleet.FileOf(dir)));
            Assert.Single(BambuFleet.Remove(dir, "03W00A2"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        var a = BambuStudioCli.SliceArgs("teil.stl", "teil.gcode.3mf", "maschine.json", "druck.json", "pla.json");
        Assert.Equal(["--orient", "1", "--arrange", "1", "--load-settings", "maschine.json;druck.json", "--load-filaments", "pla.json", "--slice", "0", "--export-3mf", "teil.gcode.3mf", "teil.stl"], a);
        Assert.Throws<ArgumentException>(() => BambuStudioCli.SliceArgs("teil.stl", "teil.3mf"));
    }
}
