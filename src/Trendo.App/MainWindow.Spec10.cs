using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-10 / T-0265: Digitaler Zwilling als Programm „Anlagen-Monitor“ – Anlagenbaum, Kacheln mit Mini-Verlauf, Zeitdiagramm mit Grenzen, Ampelwechseln und Prognose,
/// Sensor-Einstellungen und Auswertung; „Dieser PC“ liefert echte Live-Werte. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    private TwinHub? _twin;
    public TwinHub Twin => _twin ??= new TwinHub(_core.DataRoot);

    private void InitSpec10() => RegisterTab(FachTabs, "zwilling", "Anlagen-Monitor", "SPEC-10", TwinView, "digitaler zwilling", "anlagen-monitor", "dashboard", "grafana", "home assistant", "digital twin", "sensor", "messwerte", "überwachung", "anlage", "maschine", "iot");

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

    // ---------- T-0265: Programm „Anlagen-Monitor“ (Vorbild Grafana/Home Assistant, nur Funktionsumfang) ----------
    public string? TwinAsset, TwinSensor;
    public string TwinRange = "7d";
    private double _twForecast = 24;
    private string _twMsg = "";
    private Avalonia.Threading.DispatcherTimer? _twLive;
    public bool TwinLive => _twLive is { IsEnabled: true };

    /// <summary>Sensor wählen (Klick im Anlagenbaum oder auf eine Kachel).</summary>
    public void TwinSelect(string asset, string sensor) { TwinAsset = asset; TwinSensor = sensor; _twMsg = ""; Show("zwilling"); }

    /// <summary>„Live“: diesen PC alle 5 Sekunden messen, solange die Seite offen ist.</summary>
    public void TwinToggleLive()
    {
        if (TwinLive) { _twLive!.Stop(); Show("zwilling"); return; }
        _twLive ??= new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _twLive.Tick -= TwinLiveTick; _twLive.Tick += TwinLiveTick; _twLive.Start();
        MeasureThisPc(); TwinAsset = "Dieser PC"; TwinSensor ??= "Trendo-Arbeitsspeicher"; Show("zwilling");
    }
    void TwinLiveTick(object? s, EventArgs e) { if (CurrentView != "zwilling") { _twLive?.Stop(); return; } MeasureThisPc(); Show("zwilling"); }

    void TwinRun(Action a) { try { a(); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or FormatException or UnauthorizedAccessException) { _twMsg = "⚠ " + ex.Message; } Show("zwilling"); }

    static IBrush LevelBrush(TwinLevel l) => Brush.Parse(l switch { TwinLevel.Alarm => "#F87171", TwinLevel.Warnung => "#FACC15", TwinLevel.Ok => "#4ADE80", _ => "#8FA3BF" });

    private Control TwinView()
    {
        var states = Twin.States();
        if (TwinAsset is null || !Twin.Model.Assets.Any(a => a.Name == TwinAsset)) { TwinAsset = Twin.Model.Assets.FirstOrDefault()?.Name; TwinSensor = null; }
        var asset = Twin.Model.Assets.FirstOrDefault(a => a.Name == TwinAsset);
        if (asset is not null && (TwinSensor is null || !asset.Sensors.Any(s => s.Name == TwinSensor))) TwinSensor = asset.Sensors.FirstOrDefault()?.Name;
        Button Btn(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; if (accent) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => go(); return b; }

        // Kopf: Werkzeugleiste
        var bar = new WrapPanel { Name = "twin-bar" };
        bar.Children.Add(Btn("twin-pc", "Diesen PC jetzt messen", () => TwinRun(() => { MeasureThisPc(); TwinAsset = "Dieser PC"; }), true));
        bar.Children.Add(Btn("twin-live", TwinLive ? "■ Live stoppen" : "● Live (alle 5 s)", TwinToggleLive));
        if (Twin.Model.Assets.Count == 0 || !Twin.Model.Assets.Any(a => a.Name == "Heizung")) bar.Children.Add(Btn("twin-demo", "Beispielanlagen laden", () => TwinRun(() => { var n = TwinDemo.Seed(Twin, DateTime.Now); TwinAsset = "Kühlraum"; TwinSensor = "Temperatur"; _twMsg = $"{Fach.F(n, 0)} Beispielwerte geladen (erfunden)."; })));
        foreach (var (id, label) in new[] { ("24h", "24 h"), ("7d", "7 Tage"), ("30d", "30 Tage"), ("all", "Alles") })
        {
            var on = TwinRange == id; var rid = id;
            var b = Btn("twin-range-" + id, label, () => { TwinRange = rid; Show("zwilling"); }); if (on) { b.Background = Brush.Parse("#C9A227"); b.Foreground = Brushes.White; }
            bar.Children.Add(b);
        }
        if (_twMsg.Length > 0) bar.Children.Add(new TextBlock { Name = "twin-msg", Text = _twMsg, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(8, 0), Foreground = Brush.Parse(_twMsg.StartsWith('⚠') ? "#F87171" : "#4ADE80"), FontSize = 12 });

        // Links: Anlagenbaum
        var tree = new StackPanel { Name = "twin-tree", Spacing = 2 };
        foreach (var a in Twin.Model.Assets)
        {
            var worst = states.Where(s => s.Asset == a.Name).Select(s => s.Level).DefaultIfEmpty(TwinLevel.Unbekannt).Max(l => l == TwinLevel.Unbekannt ? -1 : (int)l);
            tree.Children.Add(new TextBlock { Text = a.Name + (a.Kind.Length > 0 ? $"  · {a.Kind}" : ""), Foreground = worst >= 0 ? LevelBrush((TwinLevel)worst) : Text, FontWeight = FontWeight.SemiBold, FontSize = 13, Margin = new Thickness(0, 6, 0, 2) });
            foreach (var s in states.Where(x => x.Asset == a.Name))
            {
                var on = a.Name == TwinAsset && s.Sensor == TwinSensor; var (an, sn) = (a.Name, s.Sensor);
                var b = new Button { Name = $"twin-node-{a.Name}-{s.Sensor}".Replace(' ', '_'), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left, Padding = new Thickness(8, 3),
                    Background = on ? Brush.Parse("#C9A22733") : Brushes.Transparent, BorderThickness = new Thickness(0),
                    Content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { new Avalonia.Controls.Shapes.Ellipse { Width = 9, Height = 9, Fill = LevelBrush(s.Level) }, new TextBlock { Text = s.Sensor, Foreground = Text, FontSize = 12 } } } };
                b.Click += (_, _) => TwinSelect(an, sn); tree.Children.Add(b);
            }
        }
        if (Twin.Model.Assets.Count == 0) tree.Children.Add(P("Noch keine Anlage. „Diesen PC jetzt messen“, Beispielanlagen laden oder unten einen Sensor anlegen.", null, 12));
        // Sensor anlegen / CSV einlesen
        var addAsset = new TextBox { Name = "twin-add-asset", PlaceholderText = "Anlage (z. B. Heizung)", Text = TwinAsset ?? "" };
        var addSensor = new TextBox { Name = "twin-add-sensor", PlaceholderText = "Sensor (z. B. Vorlauf)" };
        var addUnit = new TextBox { Name = "twin-add-unit", PlaceholderText = "Einheit", Width = 70 };
        tree.Children.Add(H("Sensor anlegen", 12));
        tree.Children.Add(addAsset); tree.Children.Add(addSensor);
        tree.Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { addUnit, Btn("twin-add", "＋ Anlegen", () => TwinRun(() => { if (string.IsNullOrWhiteSpace(addAsset.Text) || string.IsNullOrWhiteSpace(addSensor.Text)) throw new ArgumentException("Bitte Anlage und Sensor eintragen."); Twin.Define(addAsset.Text.Trim(), addSensor.Text.Trim(), addUnit.Text?.Trim() ?? ""); TwinAsset = addAsset.Text.Trim(); TwinSensor = addSensor.Text.Trim(); _twMsg = "Sensor angelegt – Grenzen rechts eintragen."; })) } });
        tree.Children.Add(H("Messwerte einlesen", 12));
        var csv = Field("twin-csv-path", "CSV-Datei „Zeit;Anlage;Sensor;Wert“ (.csv)", 150); tree.Children.Add(csv);
        tree.Children.Add(Btn("twin-csv-load", "Einlesen", () => TwinRun(() => { var f = Need(csv, "die CSV-Datei"); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht."); var (ok, bad) = Twin.Import(File.ReadAllText(f)); _twMsg = $"{ok} Messwerte übernommen{(bad > 0 ? $", {bad} Zeilen übersprungen" : "")}."; })));
        tree.Children.Add(TwinMqttPanel()); // Alle Studios, Runde 5: Live-Messwerte über MQTT
        var left = new Border { Name = "twin-left", Width = 230, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), Child = new ScrollViewer { Content = tree } };

        // Mitte: Kacheln der Anlage + großes Diagramm
        var center = new DockPanel();
        var tiles = new WrapPanel { Name = "twin-tiles" };
        foreach (var s in states.Where(x => x.Asset == TwinAsset))
        {
            var (an, sn) = (s.Asset, s.Sensor); var col = s.Level switch { TwinLevel.Alarm => "#F87171", TwinLevel.Warnung => "#FACC15", TwinLevel.Ok => "#4ADE80", _ => "#8FA3BF" };
            var info = s.Last is null ? "noch keine Werte" : $"{s.At:dd.MM. HH:mm}" + (s.TrendPerHour is { } t ? $" · {(t >= 0 ? "▲" : "▼")} {Fach.F(Math.Abs(t), 2)}/h" : "");
            var tile = new Border { Name = $"twin-tile-{s.Asset}-{s.Sensor}".Replace(' ', '_'), Width = 200, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(12), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Background = s.Sensor == TwinSensor ? Brush.Parse("#404040") : Brush.Parse("#2E2E2E"), BorderBrush = Brush.Parse(col), BorderThickness = new Thickness(s.Sensor == TwinSensor ? 2 : 1),
                Child = new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Name = $"twin-{s.Asset}-{s.Sensor}".Replace(' ', '_'), Text = $"{Dot(s.Level switch { TwinLevel.Alarm => Trendo.Core.Sys.A11y.Level.Problem, TwinLevel.Warnung => Trendo.Core.Sys.A11y.Level.Hinweis, _ => Trendo.Core.Sys.A11y.Level.Gut })} {s.Sensor}: {s.Level}", Foreground = Brush.Parse(col), FontSize = 12, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = s.Last is null ? "–" : $"{Fach.F(s.Last.Value, 1)} {s.Unit}", Foreground = Text, FontSize = 24, FontWeight = FontWeight.Bold },
                    new Border { Height = 30, Child = new ShapeView((w, h) => TwinChart.Spark(Twin.Model, an, sn, w, h, col), "#00000000") },
                    new TextBlock { Text = info + (s.HoursToAlarm is { } hh && hh < 48 ? $"\n⚠ Alarm in etwa {Fach.F(hh, 1)} h" : ""), Foreground = s.HoursToAlarm is { } h2 && h2 < 48 ? Brush.Parse("#FACC15") : Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } } };
            tile.PointerPressed += (_, _) => TwinSelect(an, sn);
            tiles.Children.Add(tile);
        }
        DockPanel.SetDock(tiles, Avalonia.Controls.Dock.Top); center.Children.Add(tiles);
        // T4 (07.11.2026): ohne Anlage ein Leerzustand mit den beiden Startwegen statt leerer Fläche
        if (Twin.Model.Assets.Count == 0)
            center.Children.Add(EmptyState("twin-empty", "diagramm", "Noch nichts zu überwachen",
                "Trendo zeigt hier je Sensor eine Kachel mit aktuellem Wert, Verlauf und Ampel – und darunter das große Diagramm mit Vorhersage.",
                Act("twin-empty-pc", "Diesen PC jetzt messen", () => TwinRun(() => { MeasureThisPc(); TwinAsset = "Dieser PC"; }), true),
                Act("twin-empty-demo", "Beispielanlagen laden", () => TwinRun(() => { var n = TwinDemo.Seed(Twin, DateTime.Now); TwinAsset = "Kühlraum"; TwinSensor = "Temperatur"; _twMsg = $"{Fach.F(n, 0)} Beispielwerte geladen (erfunden)."; }))));
        if (TwinAsset is { } ca && TwinSensor is { } cs)
            center.Children.Add(new Border { Name = "twin-chart", CornerRadius = new CornerRadius(12), ClipToBounds = true, MinHeight = 220, Child = new ShapeView((w, h) => TwinChart.Build(Twin.Model, ca, cs, w, h, TwinRange, _twForecast), "#212121") });
        var mid = new Border { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = center };

        // Rechts: Sensor-Eigenschaften und Auswertung
        var right = new Border { Name = "twin-side", Width = 300, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = new ScrollViewer { Content = TwinSide() } };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*,12,Auto"), Children = { left, Pos(mid, 2), Pos(right, 4) } };
        var root = new DockPanel { Name = "twin-root" };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); root.Children.Add(bar); root.Children.Add(body);
        return FitPage("Anlagen-Monitor", new Panel(), root);
    }

    Control TwinSide()
    {
        var sp = new StackPanel { Spacing = 5 };
        var a = Twin.Model.Assets.FirstOrDefault(x => x.Name == TwinAsset); var s = a?.Sensors.FirstOrDefault(x => x.Name == TwinSensor);
        if (a is null || s is null) { sp.Children.Add(P("Links einen Sensor wählen.", null, 12)); return sp; }
        var (an, sn) = (a.Name, s.Name);
        var rs = TwinPlus.Series(Twin.Model, an, sn);
        sp.Children.Add(H($"{an} – {sn}", 13));
        // Grenzen
        void Lim(string name, string label, double cur, Action<TwinSensor, double> set)
        {
            var t = new TextBox { Name = name, Text = double.IsNaN(cur) ? "" : cur.ToString("0.##", De), Width = 80, PlaceholderText = "–" };
            void Commit() { var txt = (t.Text ?? "").Trim(); var v = txt == "" ? double.NaN : double.TryParse(txt, System.Globalization.NumberStyles.Float, De, out var x) ? x : cur; if (!(double.IsNaN(v) && double.IsNaN(cur)) && v != cur) { set(s, v); Twin.Save(); Show("zwilling"); } }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            sp.Children.Add(Row(P(label, Text, 12), t));
        }
        var unit = new TextBox { Name = "twin-p-unit", Text = s.Unit, Width = 80 };
        void UnitCommit() { if ((unit.Text ?? "") != s.Unit) { s.Unit = unit.Text ?? ""; Twin.Save(); Show("zwilling"); } }
        unit.LostFocus += (_, _) => UnitCommit(); OnEnter(unit, UnitCommit);
        sp.Children.Add(Row(P("Einheit", Text, 12), unit));
        Lim("twin-p-wa", "Warnung ab", s.WarnAbove, (x, v) => x.WarnAbove = v); Lim("twin-p-aa", "Alarm ab", s.AlarmAbove, (x, v) => x.AlarmAbove = v);
        Lim("twin-p-wb", "Warnung unter", s.WarnBelow, (x, v) => x.WarnBelow = v); Lim("twin-p-ab", "Alarm unter", s.AlarmBelow, (x, v) => x.AlarmBelow = v);
        // Kennzahlen im gewählten Zeitraum
        if (rs.Count > 0)
        {
            var (from, to) = TwinChart.Range(rs, TwinRange); var span = TwinPlus.Span(rs, from, to);
            sp.Children.Add(H("Im Zeitraum", 12));
            sp.Children.Add(new SelectableTextBlock { Name = "twin-stats", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Text = $"{span.Count} Werte · min {Fach.F(span.Min, 2)} · max {Fach.F(span.Max, 2)} · Mittel {Fach.F(span.Mean, 2)} {s.Unit}" + (s.Unit.Equals("kW", StringComparison.OrdinalIgnoreCase) ? $"\nEnergie {Fach.F(TwinPlus.EnergyKwh(rs.Where(r => r.Time >= from && r.Time <= to).ToList()), 1)} kWh" : "") });
            var ev = TwinPlus.Events(Twin.Model, an, sn, TwinChart.EventBand(rs)).Where(e => e.Time >= from && e.Time <= to).ToList();
            sp.Children.Add(H($"Ampelwechsel ({ev.Count})", 12));
            sp.Children.Add(new SelectableTextBlock { Name = "twin-events", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Text = ev.Count == 0 ? "keine" : string.Join("\n", ev.TakeLast(8).Select(x => $"{x.Time:dd.MM. HH:mm}: {x.From} → {x.To} ({Fach.F(x.Value, 1)})")) });
        }
        // Prognose
        var fc = new TextBox { Name = "twin-p-fc", Text = _twForecast.ToString("0", De), Width = 80 };
        void FcCommit() { if (double.TryParse(fc.Text, System.Globalization.NumberStyles.Float, De, out var v) && v >= 0 && v != _twForecast) { _twForecast = Math.Min(v, 24 * 30); Show("zwilling"); } }
        fc.LostFocus += (_, _) => FcCommit(); OnEnter(fc, FcCommit);
        sp.Children.Add(Row(P("Prognose (Stunden)", Text, 12), fc));
        if (rs.Count >= 3 && _twForecast > 0) sp.Children.Add(new TextBlock { Name = "twin-fc-out", Text = $"In {Fach.F(_twForecast, 0)} h etwa {Fach.F(TwinPlus.Forecast(rs, _twForecast), 2)} {s.Unit} (Trend der letzten 24 h)", Foreground = Brush.Parse("#C084FC"), FontSize = 11, TextWrapping = TextWrapping.Wrap });
        // Auswertung und Verwaltung
        var calcs = new StackPanel { Spacing = 6 };
        LiveCalc(calcs, "twin-hours", "Betriebsstunden", [("thr", $"Läuft ab ({s.Unit})", "1", null)],
            v => { if (rs.Count == 0) throw new InvalidOperationException("Dieser Sensor hat noch keine Werte."); var gap = TwinPlus.GapHours(rs); return $"{Fach.F(TwinPlus.HoursAbove(rs, Fach.Num(v("thr"))), 1)} Stunden in Betrieb" + (gap > 0 ? $" · {Fach.F(gap, 1)} h Datenlücke nicht mitgerechnet" : ""); });
        LiveCalc(calcs, "twin-maint", "Wartungsplan", [("run", "Betriebsstunden jetzt", "1200", null), ("last", "Letzte Wartung bei", "1000", null), ("int", "Intervall (h)", "500", null)],
            v => { var m = TwinPlus.Maintenance(Fach.Num(v("run")), Fach.Num(v("int")), Fach.Num(v("last"))); return m.Due ? $"⚠ Wartung fällig (seit {Fach.F(-m.Remaining, 0)} h)" : $"Nächste Wartung bei {Fach.F(m.NextAt, 0)} h – noch {Fach.F(m.Remaining, 0)} h"; });
        LiveCalc(calcs, "twin-stale", "Stille Sensoren", [("min", "länger als (Minuten)", "60", null)],
            v => { var st = TwinPlus.Stale(Twin.Model, DateTime.Now, Fach.Num(v("min"))); return st.Count == 0 ? "✓ Alle Sensoren sind aktuell." : string.Join("\n", st.Select(x => $"⚠ {x.Asset} – {x.Sensor}: {(x.Last is { } t ? $"zuletzt {t:dd.MM. HH:mm}" : "noch nie")}")); });
        sp.Children.Add(new Expander { Name = "twin-calcs", Header = "Betriebsstunden, Wartung, stille Sensoren", Content = calcs, Margin = new Thickness(0, 6, 0, 0) });
        var tools = new StackPanel { Spacing = 4 };
        Button B(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4) }; b.Click += (_, _) => TwinRun(go); return b; }
        var dn = new TextBox { Name = "twin-derive-name", PlaceholderText = "Neuer Name, z. B. Vorlauf °F" }; var da = new TextBox { Name = "twin-derive-a", Text = "1,8", Width = 60 }; var db = new TextBox { Name = "twin-derive-b", Text = "32", Width = 60 };
        tools.Children.Add(P("Abgeleiteter Sensor: Faktor × Wert + Versatz", null, 11)); tools.Children.Add(dn);
        tools.Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { da, db, B("twin-derive", "Anlegen", () => { var nm = dn.Text?.Trim() ?? ""; if (nm.Length == 0) throw new ArgumentException("Bitte einen Namen für den neuen Sensor eintragen."); var n = TwinPlus.Derive(Twin, an, sn, nm, "", Fach.Num(da.Text ?? ""), Fach.Num(db.Text ?? "")); TwinSensor = nm; _twMsg = $"{n} Werte berechnet."; }) } });
        var rn = new TextBox { Name = "twin-rename-to", PlaceholderText = "Neuer Name" };
        tools.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,4,Auto"), Children = { rn, Pos(B("twin-rename", "Umbenennen", () => { TwinPlus.Rename(Twin, an, sn, rn.Text ?? ""); TwinSensor = rn.Text?.Trim(); _twMsg = "Umbenannt."; }), 2) } });
        tools.Children.Add(new WrapPanel { Children = {
            B("twin-export", "CSV speichern", () => { if (rs.Count == 0) throw new InvalidOperationException("Dieser Sensor hat noch keine Werte."); var p = Path.Combine(WorkFolder("Anlagen-Monitor"), DevStudio.SafeName($"{an}-{sn}") + ".csv"); File.WriteAllText(p, TwinPlus.ExportCsv(Twin.Model, an, sn)); _twMsg = "Gespeichert: " + p; NotifyFileResult(_twMsg); }),
            new Border { Width = 4 },
            B("twin-svg", "Diagramm (SVG)", () => { var p = Path.Combine(WorkFolder("Anlagen-Monitor"), DevStudio.SafeName($"{an}-{sn}") + ".svg"); File.WriteAllText(p, TwinChart.Svg(Twin.Model, an, sn, TwinRange, _twForecast)); _twMsg = "Gespeichert: " + p; NotifyFileResult(_twMsg); }) } });
        var conf = new TextBox { Name = "twin-del-confirm", PlaceholderText = $"„{sn}“ zur Bestätigung" };
        tools.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,4,Auto"), Children = { conf, Pos(B("twin-del", "Löschen", () => { if (!(conf.Text ?? "").Trim().Equals(sn, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Bitte den Sensornamen zur Bestätigung wiederholen."); var n = TwinPlus.Delete(Twin, an, sn); TwinSensor = null; _twMsg = $"Gelöscht ({n} Werte)."; }), 2) } });
        tools.Children.Add(B("twin-trim", "Speicher begrenzen (100 000 Werte)", () => { var n = TwinPlus.Trim(Twin.Model, 100_000); Twin.Save(); _twMsg = n == 0 ? "Nichts zu verwerfen." : $"{Fach.F(n, 0)} alte Werte verworfen."; }));
        sp.Children.Add(H("Sensor verwalten", 12)); sp.Children.Add(tools);
        return sp;
    }
}
