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

    /// <summary>Echte Messung dieses PCs: Speicherplatz, Arbeitsspeicher der Trendo-App.</summary>
    public void MeasureThisPc()
    {
        var now = DateTime.Now;
        if (!Twin.Model.Assets.Any(a => a.Name == "Dieser PC")) { Twin.Define("Dieser PC", "Festplatte belegt", "%", 85, 95, kind: "Computer"); Twin.Define("Dieser PC", "Trendo-Arbeitsspeicher", "MB", 1500, 3000, kind: "Computer"); }
        var root = Path.GetPathRoot(_core.DataRoot) ?? "/"; var d = new DriveInfo(root);
        Twin.Record(now, "Dieser PC", "Festplatte belegt", Math.Round(100.0 * (d.TotalSize - d.AvailableFreeSpace) / d.TotalSize, 1), false);
        Twin.Record(now, "Dieser PC", "Trendo-Arbeitsspeicher", Math.Round(Environment.WorkingSet / 1048576.0, 0));
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
            sp.Children.Add(WCard(new StackPanel { Children = { new TextBlock { Name = $"twin-{s.Asset}-{s.Sensor}".Replace(' ', '_'), Text = $"● {s.Asset} – {s.Sensor}: {s.Level}", Foreground = col, FontWeight = FontWeight.SemiBold }, P(info, null, 11) } }));
        }
        sp.Children.Add(FachTool("twin-def", "Sensor anlegen", "Grenzen leer lassen, wenn es keine gibt. Beispiel: Heizung, Vorlauf, °C, Warnung ab 70, Alarm ab 80.",
            [("asset", "Anlage", "Heizung"), ("sensor", "Sensor", "Vorlauf"), ("unit", "Einheit", "°C"), ("wa", "Warnung ab", "70"), ("aa", "Alarm ab", "80"), ("ab", "Alarm unter", "")],
            v => { double N(string k) => v(k).Trim() == "" ? double.NaN : Fach.Num(v(k)); Twin.Define(v("asset"), v("sensor"), v("unit"), N("wa"), N("aa"), double.NaN, N("ab")); Show("zwilling"); return "Sensor angelegt."; }, "Anlegen"));
        sp.Children.Add(FachTool("twin-import", "Messwerte einlesen", "CSV mit Kopfzeile „Zeit;Anlage;Sensor;Wert“, z. B. aus einem Datenlogger, Home Assistant oder Excel.",
            [("file", "CSV-Datei", "")],
            v => { var f = v("file").Trim().Trim('"'); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht."); var (ok, bad) = Twin.Import(File.ReadAllText(f)); Show("zwilling"); return $"{ok} Messwerte übernommen{(bad > 0 ? $", {bad} Zeilen übersprungen" : "")}."; }, "Einlesen"));
        return FachPage("Digitaler Zwilling", sp);
    }
}
