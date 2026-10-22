using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-10: Digitaler Zwilling – Anlagen überwachen; „Dieser PC“ liefert echte Live-Werte.</summary>
public partial class MainWindow
{
    private TwinHub? _twin;
    public TwinHub Twin => _twin ??= new TwinHub(_core.DataRoot);

    private void InitSpec10() => RegisterTab(FachTabs, "zwilling", "Digitaler Zwilling", "SPEC-10", TwinView, "digitaler zwilling", "digital twin", "sensor", "messwerte", "überwachung", "anlage", "maschine", "iot");

    /// <summary>Festplatte in Prozent belegt und Arbeitsspeicher der App in MB. Dieselben Grenzen wie bisher: Platte 85/95, Speicher 1500/3000.</summary>
    public readonly record struct PcSample(double DiskUsedPercent, double WorkingSetMb);

    /// <summary>Quelle für „Diesen PC messen“. null = echte Festplatte und echter Arbeitsspeicher, unverändert.</summary>
    public Func<PcSample>? PcSampleSource { get; set; }

    /// <summary>Liest die echten Werte dieses PCs (bisherige Formel).</summary>
    public static PcSample ReadLivePc(string dataRoot)
    {
        var root = Path.GetPathRoot(dataRoot) ?? "/";
        var d = new DriveInfo(root);
        var disk = Math.Round(100.0 * (d.TotalSize - d.AvailableFreeSpace) / d.TotalSize, 1);
        var ram = Math.Round(Environment.WorkingSet / 1048576.0, 0);
        return new PcSample(disk, ram);
    }

    /// <summary>Messung dieses PCs: Speicherplatz, Arbeitsspeicher der Trendo-App.</summary>
    public void MeasureThisPc()
    {
        var now = DateTime.Now;
        if (!Twin.Model.Assets.Any(a => a.Name == "Dieser PC")) { Twin.Define("Dieser PC", "Festplatte belegt", "%", 85, 95, kind: "Computer"); Twin.Define("Dieser PC", "Trendo-Arbeitsspeicher", "MB", 1500, 3000, kind: "Computer"); }
        var m = PcSampleSource?.Invoke() ?? ReadLivePc(_core.DataRoot);
        Twin.Record(now, "Dieser PC", "Festplatte belegt", m.DiskUsedPercent, false);
        Twin.Record(now, "Dieser PC", "Trendo-Arbeitsspeicher", m.WorkingSetMb);
    }

    private Control TwinView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Ein digitaler Zwilling ist das Abbild einer echten Anlage – Heizung, Maschine, Solaranlage oder dieser PC. Trendo zeigt Zustand und Trend und warnt, bevor eine Grenze erreicht wird.", Text));
        var me = new Button { Name = "twin-pc", Content = "Diesen PC jetzt messen", Background = Accent };
        me.Click += (_, _) => { MeasureThisPc(); Show("zwilling"); };
        sp.Children.Add(me);
        foreach (var s in Twin.States())
        {
            var col = s.Level switch { TwinLevel.Alarm => Brush.Parse("#F87171"), TwinLevel.Warnung => Brush.Parse("#FACC15"), TwinLevel.Ok => Brush.Parse("#4ADE80"), _ => Muted };
            var info = s.Last is null ? "noch keine Werte" : $"{Fach.F(s.Last.Value, 2)} {s.Unit} · {s.At:dd.MM. HH:mm}" + (s.TrendPerHour is { } t ? $" · Trend {(t >= 0 ? "+" : "")}{Fach.F(t, 3)} {s.Unit}/h" : "") + (s.HoursToAlarm is { } h ? h < 48 ? $" · ⚠ Alarm in etwa {Fach.F(h, 1)} h" : $" · Alarm frühestens in {Fach.F(h / 24, 0)} Tagen" : "");
            sp.Children.Add(WCard(new StackPanel { Children = { new TextBlock { Name = $"twin-{s.Asset}-{s.Sensor}".Replace(' ', '_'), Text = $"{Dot(s.Level switch { TwinLevel.Alarm => Trendo.Core.Sys.A11y.Level.Problem, TwinLevel.Warnung => Trendo.Core.Sys.A11y.Level.Hinweis, _ => Trendo.Core.Sys.A11y.Level.Gut })} {s.Asset} – {s.Sensor}: {s.Level}", Foreground = col, FontWeight = FontWeight.SemiBold }, P(info, null, 11) } }));
        }
        sp.Children.Add(FachTool("twin-def", "Sensor anlegen", "Grenzen leer lassen, wenn es keine gibt. Beispiel: Heizung, Vorlauf, °C, Warnung ab 70, Alarm ab 80.",
            [("asset", "Anlage", "Heizung"), ("sensor", "Sensor", "Vorlauf"), ("unit", "Einheit", "°C"), ("wa", "Warnung ab", "70"), ("aa", "Alarm ab", "80"), ("ab", "Alarm unter", "")],
            v => { double N(string k) => v(k).Trim() == "" ? double.NaN : Fach.Num(v(k)); Twin.Define(v("asset"), v("sensor"), v("unit"), N("wa"), N("aa"), double.NaN, N("ab")); Show("zwilling"); return "Sensor angelegt."; }, "Anlegen"));
        sp.Children.Add(FachTool("twin-import", "Messwerte einlesen", "CSV mit Kopfzeile „Zeit;Anlage;Sensor;Wert“, z. B. aus einem Datenlogger, Home Assistant oder Excel.",
            [("file", "CSV-Datei", "")],
            v => { var f = v("file").Trim().Trim('"'); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht."); var (ok, bad) = Twin.Import(File.ReadAllText(f)); Show("zwilling"); return $"{ok} Messwerte übernommen{(bad > 0 ? $", {bad} Zeilen übersprungen" : "")}."; }, "Einlesen"));
        (string, string, string)[] AS = [("asset", "Anlage", "Heizung"), ("sensor", "Sensor", "Vorlauf")];
        List<Reading> Ser(Func<string, string> v) { var r = TwinPlus.Series(Twin.Model, v("asset").Trim(), v("sensor").Trim()); return r.Count > 0 ? r : throw new InvalidOperationException("Dieser Sensor hat noch keine Werte."); }
        sp.Children.Add(FachGroup("twin-eval", "Auswerten", false,
            FachTool("twin-events", "Ereignisse", "Wann hat die Ampel gewechselt? Das Band verhindert Flattern um eine Grenze (0 = aus).", [.. AS, ("band", "Band", "2")],
                v => { var e = TwinPlus.Events(Twin.Model, v("asset").Trim(), v("sensor").Trim(), Fach.Num(v("band"))); return e.Count == 0 ? "Keine Wechsel." : string.Join("\n", e.TakeLast(20).Select(x => $"{x.Time:dd.MM. HH:mm}: {x.From} → {x.To} ({Fach.F(x.Value, 2)})")); }, "Anzeigen"),
            FachTool("twin-span", "Kennzahlen", "Zeitraum als „TT.MM.JJJJ“; leer = alles.", [.. AS, ("from", "Von", ""), ("to", "Bis", "")],
                v => { DateTime? D(string k, bool end) => v(k).Trim() == "" ? null : DateTime.Parse(v(k).Trim(), Fach.De) is var d && end && d.TimeOfDay == TimeSpan.Zero ? d.AddDays(1).AddTicks(-1) : DateTime.Parse(v(k).Trim(), Fach.De); var s = TwinPlus.Span(Ser(v), D("from", false), D("to", true)); return $"{s.Count} Werte · min {Fach.F(s.Min, 2)} · max {Fach.F(s.Max, 2)} · Mittel {Fach.F(s.Mean, 2)}"; }, "Berechnen"),
            FachTool("twin-hours", "Betriebsstunden", "Wie lange lag der Wert auf oder über der Schwelle (z. B. Leistung > 0 = läuft)?", [.. AS, ("thr", "Schwelle", "1")],
                v => $"{Fach.F(TwinPlus.HoursAbove(Ser(v), Fach.Num(v("thr"))), 1)} Stunden in Betrieb", "Zählen"),
            FachTool("twin-fc", "Was-wäre-wenn", "Trend der letzten 24 h fortschreiben.", [.. AS, ("h", "Stunden voraus", "24")],
                v => $"In {Fach.F(Fach.Num(v("h")), 0)} h etwa {Fach.F(TwinPlus.Forecast(Ser(v), Fach.Num(v("h"))), 2)} – nur eine Schätzung, wenn alles so weiterläuft.", "Schätzen"),
            FachTool("twin-kwh", "Energie", "Sensor misst Leistung in kW → verbrauchte oder erzeugte kWh.", [("asset", "Anlage", "Solar"), ("sensor", "Sensor", "Leistung")],
                v => $"{Fach.F(TwinPlus.EnergyKwh(Ser(v)), 2)} kWh im erfassten Zeitraum", "Berechnen")));
        sp.Children.Add(FachGroup("twin-manage", "Verwalten", false,
            FachTool("twin-stale", "Stille Sensoren", "Welche Sensoren haben länger nichts geliefert?", [("min", "Minuten", "60")],
                v => { var s = TwinPlus.Stale(Twin.Model, DateTime.Now, Fach.Num(v("min"))); return s.Count == 0 ? "✓ Alle Sensoren sind aktuell." : string.Join("\n", s.Select(x => $"⚠ {x.Asset} – {x.Sensor}: {(x.Last is { } t ? $"zuletzt {t:dd.MM. HH:mm}" : "noch nie")}")); }, "Prüfen"),
            FachTool("twin-derive", "Abgeleiteter Sensor", "Neuer Wert = Faktor × Wert + Versatz (z. B. °C → °F: 1,8 und 32).", [.. AS, ("name", "Neuer Name", "Vorlauf °F"), ("unit", "Einheit", "°F"), ("a", "Faktor", "1,8"), ("b", "Versatz", "32")],
                v => { var n = TwinPlus.Derive(Twin, v("asset").Trim(), v("sensor").Trim(), v("name").Trim(), v("unit"), Fach.Num(v("a")), Fach.Num(v("b"))); Show("zwilling"); return $"{n} Werte berechnet."; }, "Anlegen"),
            FachTool("twin-export", "Exportieren", "Werte eines Sensors als CSV (lässt sich wieder einlesen).", [.. AS],
                v => { var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "zwilling"), $"{v("asset").Trim()}-{v("sensor").Trim()}.csv".Replace(' ', '_')); foreach (var c in Path.GetInvalidFileNameChars()) p = Path.Combine(Path.GetDirectoryName(p)!, Path.GetFileName(p).Replace(c, '_')); _ = Ser(v); File.WriteAllText(p, TwinPlus.ExportCsv(Twin.Model, v("asset").Trim(), v("sensor").Trim())); return "Gespeichert: " + p; }, "Exportieren"),
            FachTool("twin-rename", "Umbenennen", "Neuen Namen eingeben – die Werte bleiben erhalten.", [.. AS, ("new", "Neuer Name", "")],
                v => { TwinPlus.Rename(Twin, v("asset").Trim(), v("sensor").Trim(), v("new")); Show("zwilling"); return "Umbenannt."; }, "Umbenennen"),
            FachTool("twin-del", "Löschen", "Entfernt den Sensor mit allen Werten. Zur Sicherheit den Namen noch einmal eintippen.", [.. AS, ("confirm", "Name wiederholen", "")],
                v => { if (!v("confirm").Trim().Equals(v("sensor").Trim(), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Bitte den Sensornamen zur Bestätigung wiederholen."); var n = TwinPlus.Delete(Twin, v("asset").Trim(), v("sensor").Trim()); Show("zwilling"); return $"Gelöscht ({n} Werte)."; }, "Löschen"),
            FachTool("twin-maint", "Wartungsplan", "Wartung alle X Betriebsstunden.", [("run", "Betriebsstunden jetzt", "1200"), ("last", "Letzte Wartung bei", "1000"), ("int", "Intervall (h)", "500")],
                v => { var m = TwinPlus.Maintenance(Fach.Num(v("run")), Fach.Num(v("int")), Fach.Num(v("last"))); return m.Due ? $"⚠ Wartung fällig (seit {Fach.F(-m.Remaining, 0)} h, geplant bei {Fach.F(m.NextAt, 0)} h)." : $"Nächste Wartung bei {Fach.F(m.NextAt, 0)} h – noch {Fach.F(m.Remaining, 0)} h."; }, "Planen"),
            FachTool("twin-trim", "Speicher begrenzen", "Nur die neuesten Werte behalten, ältere verwerfen.", [("max", "Höchstens Werte", "100000")],
                v => { var n = TwinPlus.Trim(Twin.Model, (int)Fach.Num(v("max"))); Twin.Save(); return n == 0 ? "Nichts zu verwerfen." : $"{Fach.F(n, 0)} alte Werte verworfen."; }, "Begrenzen")));
        return FachPage("Digitaler Zwilling", sp);
    }
}
